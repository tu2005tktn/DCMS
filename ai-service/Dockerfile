# DCMS AI service, CPU-only for the production server (docs/specs/AI-CARIES-DETECTION.vi.md §10).
# Model weights are NOT in the image: mount them read-only at /models and set AI_CARIES_WEIGHTS.
FROM python:3.10-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1 \
    PIP_DISABLE_PIP_VERSION_CHECK=1 \
    AI_DEVICE=cpu \
    OMP_NUM_THREADS=2 \
    YOLO_CONFIG_DIR=/tmp/ultralytics

# OpenCV (pulled in by ultralytics) needs these shared libraries even when headless.
RUN apt-get update \
 && apt-get install -y --no-install-recommends libgl1 libglib2.0-0 \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
# CPU wheels (+cpu) come from the PyTorch index; their pure-Python dependencies come from PyPI.
RUN pip install torch torchvision --index-url https://download.pytorch.org/whl/cpu --extra-index-url https://pypi.org/simple
COPY requirements.txt .
RUN pip install -r requirements.txt
COPY app ./app

RUN useradd --system --uid 10001 --home /app ai && mkdir -p /tmp/ultralytics && chown ai /tmp/ultralytics
USER ai

EXPOSE 8090
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
  CMD python -c "import urllib.request,sys; sys.exit(0 if b'\"ok\"' in urllib.request.urlopen('http://127.0.0.1:8090/health', timeout=4).read() else 1)"
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8090"]
