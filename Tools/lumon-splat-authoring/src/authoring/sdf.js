// Tiny signed-distance-field toolkit: primitives, a shape builder with
// per-primitive materials, and a sampler that turns a shape's surface into
// oriented surfels (position + normal) on a regular grid.

export const sphere = (cx, cy, cz, r) => (x, y, z) => Math.hypot(x - cx, y - cy, z - cz) - r;

export const ellipsoid = (cx, cy, cz, rx, ry, rz) => (x, y, z) => {
  const px = x - cx, py = y - cy, pz = z - cz;
  const k0 = Math.hypot(px / rx, py / ry, pz / rz);
  const k1 = Math.hypot(px / (rx * rx), py / (ry * ry), pz / (rz * rz));
  return k1 < 1e-9 ? -Math.min(rx, ry, rz) : (k0 * (k0 - 1)) / k1;
};

// capsule whose radius goes from r1 at a to r2 at b
export const cone = (a, b, r1, r2) => {
  const bx = b[0] - a[0], by = b[1] - a[1], bz = b[2] - a[2];
  const bb = bx * bx + by * by + bz * bz;
  return (x, y, z) => {
    const px = x - a[0], py = y - a[1], pz = z - a[2];
    let t = (px * bx + py * by + pz * bz) / bb;
    t = t < 0 ? 0 : t > 1 ? 1 : t;
    return Math.hypot(px - bx * t, py - by * t, pz - bz * t) - (r1 + (r2 - r1) * t);
  };
};

export const displace = (f, g) => (x, y, z) => f(x, y, z) + g(x, y, z);

const smin = (a, b, k) => {
  if (k <= 0) return a < b ? a : b;
  const h = Math.max(k - Math.abs(a - b), 0) / k;
  return Math.min(a, b) - h * h * k * 0.25;
};

export function shape() {
  const adds = [], subs = [];
  let bound = null;
  const far = (b, x, y, z) => Math.hypot(x - b[0], y - b[1], z - b[2]) - b[3];
  const api = {
    // bound(cx, cy, cz, r): bounding sphere for the primitives added next.
    // Far-away props are skipped without being evaluated; bound() clears it.
    bound(cx, cy, cz, r) { bound = cx === undefined ? null : [cx, cy, cz, r]; return api; },
    add(f, mat = 0, k = 0) { adds.push({ f, mat, k, b: bound }); return api; },
    sub(f, mat = 0) { subs.push({ f, mat, b: bound }); return api; },
    dist(x, y, z) {
      let d = 1e9;
      for (let i = 0; i < adds.length; i++) {
        const a = adds[i];
        if (a.b && far(a.b, x, y, z) > d + a.k) continue;
        d = smin(d, a.f(x, y, z), a.k);
      }
      for (let i = 0; i < subs.length; i++) {
        const u = subs[i];
        if (u.b && far(u.b, x, y, z) > 0.05) continue;
        const s = -u.f(x, y, z);
        if (s > d) d = s;
      }
      return d;
    },
    mat(x, y, z) {
      let best = 1e9, m = 0;
      for (const a of adds) {
        if (a.b && far(a.b, x, y, z) > best) continue;
        const d = a.f(x, y, z);
        if (d < best) { best = d; m = a.mat; }
      }
      for (const s of subs) {
        if (s.b && far(s.b, x, y, z) > 0.05) continue;
        if (-s.f(x, y, z) > best - 1e-3) return s.mat;
      }
      return m;
    },
  };
  return api;
}

// Sample the zero level set on a grid of spacing h inside box [min,max].
// Returns surfels: p (xyz), n (xyz), ao, mat.
export function sampleSurface(sh, min, max, h) {
  const out = { p: [], n: [], ao: [], mat: [] };
  const B = 4, bh = B * h;
  const e = h * 0.5;
  const grad = (x, y, z) => [
    sh.dist(x + e, y, z) - sh.dist(x - e, y, z),
    sh.dist(x, y + e, z) - sh.dist(x, y - e, z),
    sh.dist(x, y, z + e) - sh.dist(x, y, z - e),
  ];
  for (let bz = min[2]; bz < max[2]; bz += bh)
    for (let by = min[1]; by < max[1]; by += bh)
      for (let bx = min[0]; bx < max[0]; bx += bh) {
        // skip blocks that are clearly far from the surface
        if (Math.abs(sh.dist(bx + bh / 2, by + bh / 2, bz + bh / 2)) > bh * 1.6) continue;
        for (let k = 0; k < B; k++)
          for (let j = 0; j < B; j++)
            for (let i = 0; i < B; i++) {
              let x = bx + (i + 0.5) * h, y = by + (j + 0.5) * h, z = bz + (k + 0.5) * h;
              let d = sh.dist(x, y, z);
              if (Math.abs(d) > h * 1.2) continue;
              let g = grad(x, y, z);
              let gl = Math.hypot(g[0], g[1], g[2]) / (2 * e) || 1;
              if (Math.abs(d) / gl > h * 0.6) continue;
              // two Newton steps onto the surface
              for (let it = 0; it < 2; it++) {
                const s = d / (gl * gl) / (2 * e);
                x -= g[0] * s; y -= g[1] * s; z -= g[2] * s;
                d = sh.dist(x, y, z);
                g = grad(x, y, z);
                gl = Math.hypot(g[0], g[1], g[2]) / (2 * e) || 1;
              }
              const inv = 1 / (gl * 2 * e);
              const nx = g[0] * inv, ny = g[1] * inv, nz = g[2] * inv;
              // ambient occlusion: how much closer is the surface than it
              // should be when stepping out along the normal
              let occ = 0;
              for (const s of [2, 5, 10]) {
                const dd = sh.dist(x + nx * s * h, y + ny * s * h, z + nz * s * h);
                occ += Math.max(0, 1 - dd / (s * h));
              }
              out.p.push(x, y, z);
              out.n.push(nx, ny, nz);
              out.ao.push(Math.max(0, 1 - occ / 2.2));
              out.mat.push(sh.mat(x, y, z));
            }
      }
  return out;
}

// 3D Worley noise: returns [F1, F2-F1] in cell units.
export function worley(x, y, z, seed = 0) {
  const xi = Math.floor(x), yi = Math.floor(y), zi = Math.floor(z);
  let f1 = 9, f2 = 9;
  for (let dz = -1; dz <= 1; dz++)
    for (let dy = -1; dy <= 1; dy++)
      for (let dx = -1; dx <= 1; dx++) {
        const cx = xi + dx, cy = yi + dy, cz = zi + dz;
        const px = cx + h1(cx, cy, cz, seed), py = cy + h1(cy, cz, cx, seed + 17), pz = cz + h1(cz, cx, cy, seed + 31);
        const d = Math.hypot(x - px, y - py, z - pz);
        if (d < f1) { f2 = f1; f1 = d; } else if (d < f2) f2 = d;
      }
  return [f1, f2 - f1];
}
function h1(a, b, c, s) {
  let h = (a | 0) * 374761393 + (b | 0) * 668265263 + (c | 0) * 2147483647 + s * 1274126177;
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  h ^= h >>> 16;
  return (h >>> 0) / 4294967296;
}

// rounded box (centre, half sizes, corner radius) and upright capped cylinder
export const box = (cx, cy, cz, hx, hy, hz, r = 0) => (x, y, z) => {
  const qx = Math.abs(x - cx) - hx + r, qy = Math.abs(y - cy) - hy + r, qz = Math.abs(z - cz) - hz + r;
  return Math.hypot(Math.max(qx, 0), Math.max(qy, 0), Math.max(qz, 0)) + Math.min(Math.max(qx, qy, qz), 0) - r;
};
export const cyl = (cx, cy, cz, radius, halfH, r = 0) => (x, y, z) => {
  const dx = Math.hypot(x - cx, z - cz) - radius + r, dy = Math.abs(y - cy) - halfH + r;
  return Math.hypot(Math.max(dx, 0), Math.max(dy, 0)) + Math.min(Math.max(dx, dy), 0) - r;
};
