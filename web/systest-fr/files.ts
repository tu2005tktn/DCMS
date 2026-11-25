import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';
import { EVIDENCE } from './helpers';

// Small synthetic files for upload tests.
function crc32(buf: Buffer) {
  let c, crc = 0xffffffff;
  for (let n = 0; n < buf.length; n++) { c = (crc ^ buf[n]) & 0xff; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; crc = (crc >>> 8) ^ c; }
  return (crc ^ 0xffffffff) >>> 0;
}
export function png(seed = 0, w = 64, h = 48): Buffer {
  const rows: Buffer[] = [];
  for (let y = 0; y < h; y++) { const r = Buffer.alloc(1 + w * 3); for (let x = 0; x < w * 3; x++) r[1 + x] = (x * 3 + y * 5 + seed) % 256; rows.push(r); }
  const chunk = (t: string, d: Buffer) => { const len = Buffer.alloc(4); len.writeUInt32BE(d.length); const td = Buffer.concat([Buffer.from(t), d]); const c = Buffer.alloc(4); c.writeUInt32BE(crc32(td)); return Buffer.concat([len, td, c]); };
  const ihdr = Buffer.alloc(13); ihdr.writeUInt32BE(w, 0); ihdr.writeUInt32BE(h, 4); ihdr[8] = 8; ihdr[9] = 2;
  return Buffer.concat([Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]), chunk('IHDR', ihdr), chunk('IDAT', zlib.deflateSync(Buffer.concat(rows))), chunk('IEND', Buffer.alloc(0))]);
}
export function file(name: string, data: Buffer) {
  const dir = path.join(EVIDENCE, '..', 'upload-files'); fs.mkdirSync(dir, { recursive: true });
  const p = path.join(dir, name); fs.writeFileSync(p, data); return p;
}
export const XRAY = 'C:\\Xray-AI\\demo_images\\01_Nhieu_sau_rang_nang\\03_Benh_an_R018_7_sau_rang.jpg';
