import { deflateSync } from "node:zlib";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const output = join(root, "public", "assets");
mkdirSync(output, { recursive: true });

const crcTable = Array.from({ length: 256 }, (_, n) => {
  let c = n;
  for (let k = 0; k < 8; k += 1) c = (c & 1) ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
  return c >>> 0;
});

function crc32(buffer) {
  let crc = 0xffffffff;
  for (const byte of buffer) crc = crcTable[(crc ^ byte) & 0xff] ^ (crc >>> 8);
  return (crc ^ 0xffffffff) >>> 0;
}

function chunk(type, data) {
  const name = Buffer.from(type);
  const length = Buffer.alloc(4);
  length.writeUInt32BE(data.length);
  const checksum = Buffer.alloc(4);
  checksum.writeUInt32BE(crc32(Buffer.concat([name, data])));
  return Buffer.concat([length, name, data, checksum]);
}

function createIcon(size) {
  const stride = size * 4 + 1;
  const pixels = Buffer.alloc(stride * size);
  const margin = Math.max(2, Math.round(size * 0.16));
  const line = Math.max(1, Math.round(size * 0.08));

  for (let y = 0; y < size; y += 1) {
    pixels[y * stride] = 0;
    for (let x = 0; x < size; x += 1) {
      const index = y * stride + 1 + x * 4;
      const inside = x >= margin && x < size - margin && y >= margin && y < size - margin;
      const envelopeEdge = inside && (
        x < margin + line || x >= size - margin - line ||
        y < margin + line || y >= size - margin - line
      );
      const diagonal = inside && Math.abs(y - (margin + Math.abs(x - size / 2) * 0.55)) <= line;
      const accent = envelopeEdge || diagonal;
      pixels[index] = accent ? 94 : 23;
      pixels[index + 1] = accent ? 234 : 37;
      pixels[index + 2] = accent ? 212 : 84;
      pixels[index + 3] = 255;
    }
  }

  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(size, 0);
  ihdr.writeUInt32BE(size, 4);
  ihdr[8] = 8;
  ihdr[9] = 6;
  return Buffer.concat([
    Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]),
    chunk("IHDR", ihdr),
    chunk("IDAT", deflateSync(pixels)),
    chunk("IEND", Buffer.alloc(0)),
  ]);
}

for (const size of [16, 32, 64, 80, 128]) {
  writeFileSync(join(output, `icon-${size}.png`), createIcon(size));
}
