"""DCMS dental AI service. Only the DCMS API should call it; keep it off the public network.

Run: uvicorn app.main:app --host 127.0.0.1 --port 8090   (from the ai-service folder)
Contract: docs/specs/AI-CARIES-DETECTION.vi.md section 4.
"""
from __future__ import annotations

import io
import logging
import os
import secrets
from contextlib import asynccontextmanager
from pathlib import Path

from fastapi import FastAPI, File, Header, HTTPException, UploadFile
from PIL import Image, UnidentifiedImageError

from .detector import Detector, DetectorSettings, analyze

log = logging.getLogger("dcms.ai")
MAX_BYTES = 20 * 1024 * 1024
MAX_SIDE = 8000
Image.MAX_IMAGE_PIXELS = MAX_SIDE * MAX_SIDE


def _load_dotenv() -> None:
    env = Path(__file__).resolve().parent.parent / ".env"
    if env.is_file():
        for line in env.read_text(encoding="utf-8").splitlines():
            if "=" in line and not line.lstrip().startswith("#"):
                key, value = line.split("=", 1)
                os.environ.setdefault(key.strip(), value.strip())


def _float(name: str, default: float) -> float:
    value = os.environ.get(name, "").strip()
    return float(value) if value else default


def _device() -> str:
    wanted = os.environ.get("AI_DEVICE", "").strip()
    if wanted:
        return wanted
    import torch
    return "cuda" if torch.cuda.is_available() else "cpu"


state: dict = {"detectors": [], "device": "cpu", "error": None}


@asynccontextmanager
async def lifespan(_: FastAPI):
    _load_dotenv()
    try:
        state["device"] = _device()
        caries = os.environ.get("AI_CARIES_WEIGHTS", "").strip()
        if not caries:
            raise RuntimeError("AI_CARIES_WEIGHTS is not set")
        detectors = [Detector(DetectorSettings(
            kind="caries", weights=Path(caries),
            min_confidence=_float("AI_CARIES_MIN_CONFIDENCE", 0.25),
            single_source_min_confidence=_float("AI_CARIES_SINGLE_SOURCE_MIN_CONFIDENCE", 0.35),
            keep_labels=("caries_lesion",)), state["device"])]
        impacted = os.environ.get("AI_IMPACTED_WEIGHTS", "").strip()
        if impacted:
            detectors.append(Detector(DetectorSettings(
                kind="impacted_third_molar", weights=Path(impacted),
                min_confidence=_float("AI_IMPACTED_MIN_CONFIDENCE", 0.25),
                single_source_min_confidence=_float("AI_IMPACTED_MIN_CONFIDENCE", 0.25),
                tile_size=(100000, 100000),  # whole-image model; tiling would split molars
                keep_labels=("impacted_third_molar",)), state["device"]))
        state["detectors"] = detectors
        log.info("Loaded models %s on %s", [d.name for d in detectors], state["device"])
    except Exception as exc:  # keep serving /health so operators can see why
        state["error"] = str(exc)
        log.exception("Model loading failed")
    yield


app = FastAPI(title="DCMS Dental AI", version="1.0", lifespan=lifespan)


@app.get("/health")
def health():
    return {"status": "ok" if state["detectors"] else "unavailable", "device": state["device"],
            "models": [d.info() for d in state["detectors"]], "error": state["error"]}


@app.post("/v1/xray/analyze")
async def analyze_xray(file: UploadFile = File(...), x_api_key: str | None = Header(default=None)):
    expected = os.environ.get("AI_SERVICE_API_KEY", "")
    if expected and not secrets.compare_digest(x_api_key or "", expected):
        raise HTTPException(401, "Invalid API key")
    if not state["detectors"]:
        raise HTTPException(503, "Model is not loaded")
    if file.content_type not in ("image/jpeg", "image/png"):
        raise HTTPException(400, "Only image/jpeg and image/png are accepted")
    data = await file.read(MAX_BYTES + 1)
    if len(data) > MAX_BYTES:
        raise HTTPException(413, "Image is larger than 20 MiB")
    try:
        image = Image.open(io.BytesIO(data))
        image.load()
    except (UnidentifiedImageError, OSError, Image.DecompressionBombError):
        raise HTTPException(400, "Image cannot be decoded")
    if max(image.size) > MAX_SIDE:
        raise HTTPException(400, f"Image side exceeds {MAX_SIDE} px")
    # Sync inference in FastAPI's threadpool via a plain def would also work; run_in_threadpool keeps the loop free.
    from starlette.concurrency import run_in_threadpool
    return await run_in_threadpool(analyze, state["detectors"], image.convert("RGB"), state["device"])
