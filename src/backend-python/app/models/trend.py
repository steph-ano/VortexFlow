from pydantic import BaseModel, Field
from typing import List, Optional
from datetime import datetime
import uuid

class TrendMetrics(BaseModel):
    volume: int
    sentiment: float
    engagement: float

class TrendData(BaseModel):
    platform: str
    hashtags: List[str]
    source: str
    metrics: TrendMetrics
    timestamp: datetime
    raw_data: Optional[dict] = None

class TrendsProcessedEvent(BaseModel):
    eventId: str
    platform: str
    hashtags: List[str]
    source: str
    timestamp: str
    metrics: TrendMetrics
