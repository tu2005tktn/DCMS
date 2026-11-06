# Dental reference assets

The examination viewer serves attributed reference meshes from `web/public/models/dental/`. Their source is University of Dundee, School of Dentistry, under CC BY 4.0. Per-file source URLs, pinned mirror revision, original/derived SHA-256, triangle counts and file sizes are in `manifest.json`; the visible viewer links `ATTRIBUTION.md`. Jaw models with CC BY-NC are excluded. These are educational references, not patient data or AI weights. The `.gitignore` exception only permits this attributed public directory.

The 26 templates cover eight upper/eight lower permanent tooth types and five upper/five lower primary types. The opposite side is reflected in the viewer. The arch LOD is cropped at the cervical origin; the separate detail LOD retains crown and roots. Shapes are uniformly scaled and oriented to an illustrative arch. Original textures are removed; enamel/root colors are added. All adapted meshes remain CC BY 4.0. DCMS has not received a professional anatomical review of these adaptations and does not use these shapes for measurements or diagnosis.

Rebuild from repository root with Node 22 and Python 3:

```powershell
npm install --prefix .artifacts/odontogram-asset-tools --no-audit --no-fund @gltf-transform/cli@4.4.0
python infrastructure/scripts/fetch-dental-models.py
node infrastructure/scripts/prepare-dental-models.mjs
```

The fetch script uses Windows curl with normal certificate verification, checks each original model's API license, and records source hashes. It downloads approximately 158 MB into `.artifacts/odontogram-assets/original`; these originals are not shipped. The converter uses glTF Transform 4.4.0 and Meshoptimizer, preserves geometric normals, simplifies with bounded error and writes the two local LODs. Runtime does not depend on conversion tools, Sketchfab, GitHub or a CDN. Attribution text is maintained alongside the manifest. Review the single-tooth and full-arch browser screenshots after rebuilding; file validity alone does not prove anatomical accuracy.
