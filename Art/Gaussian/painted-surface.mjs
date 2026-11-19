// Surface patches in metres; pigments in display RGB [0,255]. No external packages.
import fs from "node:fs";

const clamp = (x, a, b) => Math.max(a, Math.min(b, x));
export const blend = (a, b, t) => a.map((v, i) => v + (b[i] - v) * t);
const difference = (a, b) => a.map((v, i) => v - b[i]);
const length = (a) => Math.hypot(...a);
const cross = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
const normalize = (v) => {
  const size = length(v);
  if (!Number.isFinite(size) || size < 1e-12) throw Error("Expected a nonzero finite normal");
  return v.map((x) => x / size);
};
export const dot = (a, b) => a.reduce((sum, x, i) => sum + x * b[i], 0);
const fields = "x y z scale_0 scale_1 scale_2 rot_1 rot_2 rot_3 rot_0 f_dc_0 f_dc_1 f_dc_2 opacity".split(
  " ",
);

export class PaintedSurface {
  constructor(spacing = 0.009) {
    if (!Number.isFinite(spacing) || spacing <= 0) throw Error("Expected a positive spacing");
    this.spacing = spacing;
    this.chunks = [];
    this.count = 0;
  }
  point(p, normal, pigment, spacing = this.spacing) {
    if (
      p.length !== 3 ||
      pigment.length !== 3 ||
      !p.every(Number.isFinite) ||
      !pigment.every(Number.isFinite) ||
      !Number.isFinite(spacing) ||
      spacing <= 0
    )
      throw Error("Invalid painted surfel");
    if (this.count % 4096 === 0) this.chunks.push(Buffer.alloc(4096 * 56));
    const row = this.chunks.at(-1);
    const i = (this.count++ % 4096) * 56;
    const n = normalize(normal);
    const w = Math.sqrt(Math.max(0, (1 + n[2]) / 2));
    const q = w < 0.00001 ? [1, 0, 0, 0] : [-n[1] / (2 * w), n[0] / (2 * w), 0, w];
    const tangent = Math.log(spacing * 0.72);
    const values = [
      ...p,
      tangent,
      tangent,
      Math.log(spacing * 0.16),
      ...q,
      ...pigment.map((v) => (clamp(v, 0, 255) / 255 - 0.5) / 0.2820948),
      9.2,
    ];
    for (let k = 0; k < 14; k++) row.writeFloatLE(values[k], i + k * 4);
  }
  // a,b are the bottom edge, d,c the top. The winding sets the outward normal.
  quad(a, b, c, d, paint, spacing = this.spacing) {
    const nu = Math.max(1, Math.ceil(Math.max(length(difference(a, b)), length(difference(c, d))) / spacing));
    const nv = Math.max(1, Math.ceil(Math.max(length(difference(a, d)), length(difference(b, c))) / spacing));
    for (let j = 0; j < nv; j++) {
      const v = (j + 0.5) / nv,
        left = blend(a, d, v),
        right = blend(b, c, v);
      const du = difference(right, left);
      for (let i = 0; i < nu; i++) {
        const u = (i + 0.5) / nu,
          dv = blend(difference(d, a), difference(c, b), u);
        const direction = cross(du, dv);
        if (length(direction) < 1e-12) continue;
        const n = normalize(direction),
          p = blend(left, right, u);
        const pigment = paint(p, n, u, v);
        if (pigment) this.point(p, n, pigment, spacing);
      }
    }
  }
  finish(sun) {
    // A depth atlas of the actual surfels gives the hood an overhang shadow and the
    // branch tiers contact. Gather visibility independently before dropping sealed interiors.
    const stride = 14,
      rows = new Float32Array(this.count * stride);
    for (let i = 0; i < this.count; i++) {
      const block = this.chunks[i >>> 12],
        start = (i % 4096) * 56;
      for (let k = 0; k < stride; k++) rows[i * stride + k] = block.readFloatLE(start + k * 4);
    }
    const seen = new Uint8Array(this.count);
    const project = (direction, key) => {
      const f = normalize(direction),
        r = normalize(cross(Math.abs(f[1]) > 0.97 ? [0, 0, 1] : [0, 1, 0], f)),
        up = cross(f, r);
      const pitch = key ? 0.011 : 0.019;
      let x0 = Infinity,
        x1 = -Infinity,
        y0 = Infinity,
        y1 = -Infinity;
      const U = new Float32Array(this.count),
        V = new Float32Array(this.count),
        Z = new Float32Array(this.count);
      for (let i = 0; i < this.count; i++) {
        const j = i * stride,
          x = rows[j],
          y = rows[j + 1],
          z = rows[j + 2];
        U[i] = (x * r[0] + y * r[1] + z * r[2]) / pitch;
        V[i] = (x * up[0] + y * up[1] + z * up[2]) / pitch;
        Z[i] = x * f[0] + y * f[1] + z * f[2];
        x0 = Math.min(x0, U[i]);
        x1 = Math.max(x1, U[i]);
        y0 = Math.min(y0, V[i]);
        y1 = Math.max(y1, V[i]);
      }
      x0 = Math.floor(x0) - 3;
      y0 = Math.floor(y0) - 3;
      const w = Math.ceil(x1) - x0 + 4,
        h = Math.ceil(y1) - y0 + 4;
      const map = new Float32Array(w * h).fill(-Infinity),
        ids = new Int32Array(this.count);
      for (let i = 0; i < this.count; i++) {
        const id = Math.floor(U[i]) - x0 + (Math.floor(V[i]) - y0) * w;
        ids[i] = id;
        map[id] = Math.max(map[id], Z[i]);
      }
      for (let i = 0; i < this.count; i++) {
        const id = ids[i],
          z = Z[i];
        if (z >= map[id] - 0.04) seen[i] = 1;
        if (!key) continue;
        let shadow = 0;
        for (const offset of [0, 1, -1, w, -w, w + 1, -w - 1, w - 1, -w + 1])
          shadow += clamp((map[id + offset] - z - 0.029) / 0.028, 0, 1) / 9;
        for (let channel = 0; channel < 3; channel++) {
          const j = i * stride + 10 + channel,
            tone = 0.5 + rows[j] * 0.2820948;
          rows[j] = (tone * (1 - shadow * [0.27, 0.25, 0.2][channel]) - 0.5) / 0.2820948;
        }
      }
    };
    project(sun, true);
    for (const elevation of [-0.75, -0.18, 0.32, 0.88])
      for (let a = 0; a < 10; a++) {
        const t = ((a + (elevation + 0.75) * 0.32) * Math.PI) / 5;
        project([Math.cos(t), elevation, Math.sin(t)], false);
      }
    project([0, 1, 0], false);
    project([0, -1, 0], false);
    let kept = 0;
    for (let i = 0; i < this.count; i++) {
      if (!seen[i]) continue;
      const block = this.chunks[kept >>> 12],
        start = (kept % 4096) * 56;
      for (let k = 0; k < stride; k++) block.writeFloatLE(rows[i * stride + k], start + k * 4);
      kept++;
    }
    this.count = kept;
    this.chunks.length = Math.ceil(kept / 4096);
    return this;
  }
  recolor(brush) {
    for (let i = 0; i < this.count; i++) {
      const row = this.chunks[i >>> 12],
        offset = (i % 4096) * 56;
      const read = (k) => row.readFloatLE(offset + k * 4);
      const [x, y, z, w] = [6, 7, 8, 9].map(read);
      const n = [2 * (x * z + w * y), 2 * (y * z - w * x), 1 - 2 * (x * x + y * y)];
      const color = [10, 11, 12].map((k) => 255 * (0.5 + 0.2820948 * read(k)));
      const result = brush([0, 1, 2].map(read), n, color);
      result.forEach((channel, k) =>
        row.writeFloatLE((clamp(channel, 0, 255) / 255 - 0.5) / 0.2820948, offset + 40 + k * 4),
      );
    }
  }
  save(file) {
    if (!this.count) throw Error("Empty surface");
    const header = [
      "ply",
      "format binary_little_endian 1.0",
      "element vertex " + this.count,
      ...fields.map((name) => "property float " + name),
      "end_header",
      "",
    ].join("\n");
    const data = this.chunks.slice();
    data[data.length - 1] = data.at(-1).subarray(0, (((this.count - 1) % 4096) + 1) * 56);
    fs.writeFileSync(file, Buffer.concat([Buffer.from(header), ...data]));
  }
}
