export type DentalTemplate = { id: number; width: number; source: string };
export const dentalTemplates: DentalTemplate[] = [
  {
    "id": 21,
    "width": 1.05,
    "source": "https://sketchfab.com/3d-models/maxillary-left-central-incisor-c8a7c2d9280d4c92bc651cfa1459866a"
  },
  {
    "id": 22,
    "width": 0.85,
    "source": "https://sketchfab.com/3d-models/maxillary-lateral-incisor-5e89ddbfc6454e2e8e09c645574b8932"
  },
  {
    "id": 23,
    "width": 0.95,
    "source": "https://sketchfab.com/3d-models/maxillary-canine-bd930c9b9da14f2a9a8c9b130b0e08a2"
  },
  {
    "id": 24,
    "width": 0.95,
    "source": "https://sketchfab.com/3d-models/maxillary-first-premolar-f9b48a29d34f4923b683433f030c5c70"
  },
  {
    "id": 25,
    "width": 0.95,
    "source": "https://sketchfab.com/3d-models/maxillary-second-premolar-69f3142830064588b000b04bea0ee09f"
  },
  {
    "id": 26,
    "width": 1.2,
    "source": "https://sketchfab.com/3d-models/maxillary-first-molar-e719a474ef7e4bd7abec508f85f1e984"
  },
  {
    "id": 27,
    "width": 1.15,
    "source": "https://sketchfab.com/3d-models/maxillary-second-molar-e035713849d1438791306e25235ac452"
  },
  {
    "id": 28,
    "width": 1.05,
    "source": "https://sketchfab.com/3d-models/maxillary-third-molar-1b3c50ded70c4b6297d4526a733a9cf1"
  },
  {
    "id": 31,
    "width": 0.65,
    "source": "https://sketchfab.com/3d-models/mandibular-left-central-incisor-90dcbf474e5a4d97b8783b7eb2b9c4b7"
  },
  {
    "id": 32,
    "width": 0.73,
    "source": "https://sketchfab.com/3d-models/mandibular-left-lateral-incisor-00fa4f74e10b4769830bf60469c65e27"
  },
  {
    "id": 33,
    "width": 0.85,
    "source": "https://sketchfab.com/3d-models/mandibular-left-canine-1082011ab5aa46bb96b2af6a02a4ec0c"
  },
  {
    "id": 34,
    "width": 0.9,
    "source": "https://sketchfab.com/3d-models/mandibular-first-premolar-935637a703dc49eb9eeec9b15a8a5c4c"
  },
  {
    "id": 35,
    "width": 0.95,
    "source": "https://sketchfab.com/3d-models/mandibular-left-second-premolar-fe59fe04725446479bc1115bb12d0ad8"
  },
  {
    "id": 36,
    "width": 1.23,
    "source": "https://sketchfab.com/3d-models/mandibular-first-molar-e1c919d6603846eca873154eeededdd6"
  },
  {
    "id": 37,
    "width": 1.18,
    "source": "https://sketchfab.com/3d-models/mandibular-second-molar-b77dcbc5052e4740b87cdb1964649742"
  },
  {
    "id": 38,
    "width": 1.1,
    "source": "https://sketchfab.com/3d-models/mandibular-third-molar-561bb06b3b084b84978163906de1c2b5"
  },
  {
    "id": 61,
    "width": 0.8,
    "source": "https://sketchfab.com/3d-models/primary-upper-left-central-incisor-fdi-61-608b92d539504e7c849b0bb5096301a1"
  },
  {
    "id": 62,
    "width": 0.65,
    "source": "https://sketchfab.com/3d-models/primary-upper-left-lateral-incisor-fdi-62-81ff650c0fe14c308c13fc3b4ffd7dc5"
  },
  {
    "id": 63,
    "width": 0.72,
    "source": "https://sketchfab.com/3d-models/primary-upper-left-canine-fdi-63-905b91b732504abc8e0923c70cbe7efa"
  },
  {
    "id": 64,
    "width": 0.95,
    "source": "https://sketchfab.com/3d-models/primary-upper-left-first-molar-fdi-64-3a097dbbf59b45e380452cc473c1136f"
  },
  {
    "id": 65,
    "width": 1.1,
    "source": "https://sketchfab.com/3d-models/primary-upper-left-second-molar-fdi-65-2a1f94a0b3c14ffea25adba6c4ad0c21"
  },
  {
    "id": 71,
    "width": 0.5,
    "source": "https://sketchfab.com/3d-models/primary-lower-left-central-incisor-fdi-71-14f64de96c2b40bebaceb04ff582686f"
  },
  {
    "id": 72,
    "width": 0.6,
    "source": "https://sketchfab.com/3d-models/primary-lower-left-lateral-incisor-fdi-72-5db37732979d42858a63835c448ae938"
  },
  {
    "id": 73,
    "width": 0.65,
    "source": "https://sketchfab.com/3d-models/primary-lower-left-canine-fdi-73-7af5b4caf60f4453a4142b6845b9d898"
  },
  {
    "id": 74,
    "width": 0.9,
    "source": "https://sketchfab.com/3d-models/primary-lower-left-first-molar-fdi-74-4b693b87f0504c5c8b507bf4b9360ad0"
  },
  {
    "id": 75,
    "width": 1.02,
    "source": "https://sketchfab.com/3d-models/primary-lower-left-second-molar-fdi-75-3499c29351e94e96b7b5983ab4bc1a23"
  }
];
export function templateFor(tooth: number): DentalTemplate {
  const quadrant = Math.floor(tooth / 10);
  const base = quadrant <= 2 ? 20 : quadrant <= 4 ? 30 : quadrant <= 6 ? 60 : 70;
  const template = dentalTemplates.find(item => item.id === base + tooth % 10);
  if (!template) throw new Error('Không có mẫu răng FDI ' + tooth);
  return template;
}
export const isMirrored = (tooth: number) => [1, 4, 5, 8].includes(Math.floor(tooth / 10));
export function toothName(tooth: number) {
  const quadrant = Math.floor(tooth / 10), n = tooth % 10;
  const primary = quadrant >= 5;
  const name = n === 1 ? 'Răng cửa giữa' : n === 2 ? 'Răng cửa bên' : n === 3 ? 'Răng nanh'
    : primary ? `Răng hàm thứ ${n - 3}` : n <= 5 ? `Răng tiền hàm thứ ${n - 3}` : `Răng hàm thứ ${n - 5}`;
  const upper = [1, 2, 5, 6].includes(quadrant);
  const right = [1, 4, 5, 8].includes(quadrant);
  return `${name}${primary ? ' sữa' : ''} · hàm ${upper ? 'trên' : 'dưới'} bên ${right ? 'phải' : 'trái'}`;
}
export const modelUrl = (id: number, detail: boolean) => `${import.meta.env.BASE_URL}models/dental/${detail ? 'detail' : 'arch'}/${id}.glb`;
