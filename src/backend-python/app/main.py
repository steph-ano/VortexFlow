import json
import time as time_module
from contextlib import asynccontextmanager

import structlog
from fastapi import Depends, FastAPI, HTTPException, Security
from fastapi.security.api_key import APIKeyHeader
from opentelemetry import trace
from opentelemetry.exporter.otlp.proto.grpc.trace_exporter import OTLPSpanExporter
from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor
from opentelemetry.instrumentation.httpx import HTTPXClientInstrumentor
from opentelemetry.sdk.resources import Resource
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor
from prometheus_client import Counter, Histogram, make_asgi_app
from redis import Redis

from app.config import settings
from app.tasks.scraping import scrape_all_platforms

logger = structlog.get_logger()

otlp_endpoint = settings.OTLP_ENDPOINT if hasattr(settings, 'OTLP_ENDPOINT') else "http://localhost:4317"

resource = Resource.create(attributes={
    "service.name": "VortexFlow.Worker",
    "service.version": "1.0.0",
    "service.instance.id": settings.HOSTNAME if hasattr(settings, 'HOSTNAME') else "unknown"
})

provider = TracerProvider(resource=resource)
processor = BatchSpanProcessor(OTLPSpanExporter(endpoint=otlp_endpoint, insecure=True))
provider.add_span_processor(processor)
trace.set_tracer_provider(provider)

request_count = Counter(
    "vortexflow_http_requests_total",
    "Total HTTP requests",
    ["method", "endpoint", "status"]
)
request_duration = Histogram(
    "vortexflow_http_request_duration_seconds",
    "HTTP request duration in seconds",
    ["method", "endpoint"]
)

@asynccontextmanager
async def lifespan(app: FastAPI):
    HTTPXClientInstrumentor().instrument()
    yield

app = FastAPI(
    title="VortexFlow Tactical Specialist API",
    description="API for scraping and processing social media trends.",
    version="1.0.0",
    lifespan=lifespan
)

FastAPIInstrumentor.instrument_app(app)

metrics_app = make_asgi_app()
app.mount("/metrics", metrics_app)

api_key_header = APIKeyHeader(name="X-API-Key", auto_error=False)

@app.middleware("http")
async def metrics_middleware(request, call_next):
    start = time_module.time()
    response = await call_next(request)
    duration = time_module.time() - start
    request_count.labels(method=request.method, endpoint=request.url.path, status=response.status_code).inc()
    request_duration.labels(method=request.method, endpoint=request.url.path).observe(duration)
    return response

def get_api_key(api_key_header: str = Security(api_key_header)):
    if api_key_header == settings.API_KEY_INTERNAL:
        return api_key_header
    raise HTTPException(status_code=403, detail="Could not validate API KEY")

redis_client = Redis.from_url(settings.REDIS_URL)

@app.get("/health")
def health_check():
    health_status = {"status": "ok", "redis": "disconnected", "rabbitmq": "untested"}

    try:
        if redis_client.ping():
            health_status["redis"] = "connected"
    except Exception as e:
        logger.error(f"Redis health check failed: {e}")
        health_status["status"] = "degraded"

    return health_status

@app.post("/trigger-scrape")
def trigger_scrape(api_key: str = Depends(get_api_key)):
    logger.info("Scraping manual trigger received.")
    scrape_all_platforms.delay()
    return {"message": "Scraping tasks triggered successfully"}

@app.get("/trends/current")
def get_current_trends():
    keys = redis_client.keys("trend:current:*")
    trends = []

    for key in keys:
        data = redis_client.get(key)
        if data:
            trends.append(json.loads(data))

    return {"count": len(trends), "trends": trends}
