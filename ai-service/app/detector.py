"""Dental X-ray lesion detection: global + flipped + native-resolution tiles, fused by weighted box fusion.

Contract and thresholds: docs/specs/AI-CARIES-DETECTION.vi.md section 3.
"""
from __future__ import annotations

import hashlib
import threading
import time
from dataclasses import dataclass
from pathlib import Path

from PIL import Image

SCHEMA_VERSION = "1.0"


@dataclass(frozen=True)
class DetectorSettings:
    kind: str
    weights: Path
    min_confidence: float
    single_source_min_confidence: float
    raw_confidence: float = 0.10
    tile_size: tuple[int, int] = (960, 640)
    tile_overlap: float = 0.25
    fusion_iou: float = 0.35
    vote_bonus: float = 0.03
    keep_labels: tuple[str, ...] | None = None  # None keeps every class


def _sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def _iou(a: list[float], b: list[float]) -> float:
    ix = max(0.0, min(a[2], b[2]) - max(a[0], b[0]))
    iy = max(0.0, min(a[3], b[3]) - max(a[1], b[1]))
    inter = ix * iy
    union = (a[2] - a[0]) * (a[3] - a[1]) + (b[2] - b[0]) * (b[3] - b[1]) - inter
    return inter / union if union > 0 else 0.0


def _tiles(width: int, height: int, size: tuple[int, int], overlap: float) -> list[tuple[int, int, int, int]]:
    def starts(total: int, tile: int) -> list[int]:
        if total <= tile:
            return [0]
        step = max(1, int(tile * (1 - overlap)))
        out = list(range(0, total - tile, step))
        out.append(total - tile)
        return sorted(set(out))

    tw, th = size
    return [(x, y, min(x + tw, width), min(y + th, height)) for y in starts(height, th) for x in starts(width, tw)]


class Detector:
    def __init__(self, settings: DetectorSettings, device: str):
        from ultralytics import YOLO  # imported lazily so the module can be inspected without torch

        if not settings.weights.is_file():
            raise FileNotFoundError(f"Weights not found: {settings.weights}")
        self.settings = settings
        self.device = device
        self.name = settings.weights.stem
        self.sha256 = _sha256(settings.weights)
        self.model = YOLO(str(settings.weights))
        self.labels: dict[int, str] = dict(self.model.names)
        self._lock = threading.Lock()  # Ultralytics predictors are not thread-safe

    def info(self) -> dict:
        return {"kind": self.settings.kind, "name": self.name, "sha256": self.sha256, "loaded": True}

    def _predict(self, images: list[Image.Image], imgsz: int | None = None):
        kwargs = {"imgsz": imgsz} if imgsz else {}
        return self.model.predict(images, conf=self.settings.raw_confidence, device=self.device, verbose=False, **kwargs)

    def _candidates(self, result, source: str, dx: float = 0, dy: float = 0, flip_width: int | None = None) -> list[dict]:
        out = []
        boxes = result.boxes
        for xyxy, conf, cls in zip(boxes.xyxy.cpu().tolist(), boxes.conf.cpu().tolist(), boxes.cls.cpu().tolist()):
            label = self.labels.get(int(cls), str(int(cls)))
            if self.settings.keep_labels is not None and label not in self.settings.keep_labels:
                continue
            x1, y1, x2, y2 = xyxy
            if flip_width is not None:
                x1, x2 = flip_width - x2, flip_width - x1
            out.append({"box": [x1 + dx, y1 + dy, x2 + dx, y2 + dy], "conf": float(conf), "source": source, "label": label})
        return out

    def _fuse(self, cands: list[dict]) -> list[dict]:
        s = self.settings
        clusters: list[list[dict]] = []
        for c in sorted(cands, key=lambda c: c["conf"], reverse=True):
            for cl in clusters:
                if cl[0]["label"] == c["label"] and _iou(cl[0]["box"], c["box"]) >= s.fusion_iou:
                    cl.append(c)
                    break
            else:
                clusters.append([c])
        fused = []
        for cl in clusters:
            weights = sum(c["conf"] for c in cl)
            box = [sum(c["box"][i] * c["conf"] for c in cl) / weights for i in range(4)]
            sources = sorted({c["source"] for c in cl})
            conf = min(0.99, cl[0]["conf"] + s.vote_bonus * (len(sources) - 1))
            threshold = s.min_confidence if len(sources) > 1 else max(s.min_confidence, s.single_source_min_confidence)
            if conf >= threshold:
                fused.append({"box": box, "conf": conf, "sources": sources, "label": cl[0]["label"]})
        return fused

    def detect(self, image: Image.Image) -> list[dict]:
        width, height = image.size
        with self._lock:
            cands = self._candidates(self._predict([image])[0], "global")
            flipped = image.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
            cands += self._candidates(self._predict([flipped])[0], "flip", flip_width=width)
            boxes = _tiles(width, height, self.settings.tile_size, self.settings.tile_overlap)
            if len(boxes) > 1:
                crops = [image.crop(b) for b in boxes]
                # Native resolution: upscaling 960 px tiles to the model's 1280 training size loses most lesions.
                for b, res in zip(boxes, self._predict(crops, imgsz=max(self.settings.tile_size))):
                    cands += self._candidates(res, "tile", dx=b[0], dy=b[1])
        return self._fuse(cands)


def _region(box: list[float], width: int, height: int) -> tuple[str, str]:
    cx, cy = (box[0] + box[2]) / 2, (box[1] + box[3]) / 2
    jaw = "upper" if cy < height / 2 else "lower"
    side = "right" if cx < width / 2 else "left"  # panoramic convention: image left = patient right
    return jaw, side


def analyze(detectors: list[Detector], image: Image.Image, device: str) -> dict:
    start = time.monotonic()
    width, height = image.size
    findings = []
    for det in detectors:
        for f in det.detect(image):
            x1, y1, x2, y2 = (max(0.0, min(f["box"][0], width)), max(0.0, min(f["box"][1], height)),
                              max(0.0, min(f["box"][2], width)), max(0.0, min(f["box"][3], height)))
            if x2 - x1 < 1 or y2 - y1 < 1:
                continue
            jaw, side = _region([x1, y1, x2, y2], width, height)
            findings.append({"kind": det.settings.kind, "label": f["label"], "confidence": round(f["conf"], 4),
                             "box": {"x1": round(x1, 1), "y1": round(y1, 1), "x2": round(x2, 1), "y2": round(y2, 1)},
                             "jaw": jaw, "side": side, "sources": f["sources"]})
    findings.sort(key=lambda f: (f["kind"], -f["confidence"]))
    for i, f in enumerate(findings, 1):
        f["findingId"] = f"F{i:02d}"
    return {
        "schemaVersion": SCHEMA_VERSION,
        "models": [{k: v for k, v in d.info().items() if k != "loaded"} for d in detectors],
        "image": {"width": width, "height": height},
        "device": device,
        "elapsedMs": int((time.monotonic() - start) * 1000),
        "findings": [{"findingId": f.pop("findingId"), **f} for f in findings],
    }
