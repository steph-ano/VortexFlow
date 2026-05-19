# Etapa 1: Build dependencies
FROM python:3.11-slim AS builder
WORKDIR /app

RUN pip install --no-cache-dir "wheel>=0.46.2" poetry poetry-plugin-export

COPY src/backend-python/pyproject.toml src/backend-python/poetry.lock* ./

RUN poetry config virtualenvs.create false \
    && poetry export -f requirements.txt --output requirements.txt --without-hashes

# Etapa 2: Runtime
FROM python:3.11-slim AS final
WORKDIR /app

# Actualizar paquetes de seguridad del OS
RUN apt-get update && apt-get upgrade -y && rm -rf /var/lib/apt/lists/*

RUN groupadd -r appuser && useradd -r -g appuser appuser

# Instalar dependencias limpias
COPY --from=builder /app/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copiar aplicación
COPY src/backend-python/ ./

RUN chown -R appuser:appuser /app
USER appuser

ENV PYTHONUNBUFFERED=1

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
