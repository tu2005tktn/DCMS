# DCMS AI service

FastAPI service that runs YOLO dental X-ray detection for DCMS. Specification and contract:
[docs/specs/AI-CARIES-DETECTION.vi.md](../docs/specs/AI-CARIES-DETECTION.vi.md).

- `GET /health`: model and device status.
- `POST /v1/xray/analyze`: multipart `file` (JPEG/PNG ≤ 20 MiB), optional `X-Api-Key` header. Returns lesion boxes in original image pixels.

Model weights are **not** in Git. Point `AI_CARIES_WEIGHTS` to an absolute path, e.g.
`C:\Xray-AI\artifacts\detector_weights\recommended_caries_best.pt`.

## Run locally (Windows)

```powershell
cd ai-service
py -3.10 -m venv .venv; .\.venv\Scripts\Activate.ps1
pip install torch --index-url https://download.pytorch.org/whl/cpu   # or the CUDA build
pip install -r requirements.txt
Copy-Item .env.example .env   # then edit paths
uvicorn app.main:app --host 127.0.0.1 --port 8090
Invoke-RestMethod http://127.0.0.1:8090/health
```

Then set `DENTAL_AI_BASE_URL=http://127.0.0.1:8090` in the repository `.env` and restart the API.

Results are decision support only, trained on panoramic radiographs; they are not a clinical diagnosis.
