// Scene kit: "free" splats and a couple of shared props.
//
// Most of a scene is SDF shapes sampled into surfels. Free splats are emitted
// one at a time instead: a grass blade, a palm leaflet or a fallen leaf is a
// single stretched splat, and a glow is a few nested translucent ones.

import { sphere, cone, box, cyl } from "./sdf.js";
import { rgb, TAU } from "./island.js";
import { SUN } from "./light.js";

export const KIND = { lit: 0, emissive: 1, sway: 2, glow: 3 };
export const UP = [0, 1, 0];
export function quatFromTo(a, b) {
  const d = a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
  if (d < -0.9999) return [1, 0, 0, 0];
  const q = [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0], 1 + d], l = Math.hypot(...q);
  return q.map((v) => v / l);
}
export const norm = (v) => { const l = Math.hypot(...v) || 1; return [v[0] / l, v[1] / l, v[2] / l]; };
const FLAT = quatFromTo([0, 0, 1], UP); // thin axis up: something lying on the ground

export function makeFree(c) {
  const { rnd } = c, list = [];
  const f = {
    list,
    push(p, s, q, col, kind = KIND.lit, alpha = 1) { list.push([...p, ...s, ...q, ...col, kind, alpha]); },
    // a blade: a dark lower half and a lighter, leaning tip
    blade(x, y, z, h, w, dark, light) {
      const a = rnd() * TAU, lean = 0.15 + rnd() * 0.3, d = norm([Math.cos(a) * lean, 1, Math.sin(a) * lean]), q = quatFromTo(UP, d);
      f.push([x + d[0] * h * 0.25, y + h * 0.25, z + d[2] * h * 0.25], [w, h * 0.3, w], q, dark, KIND.sway);
      f.push([x + d[0] * h * 0.7, y + h * 0.68, z + d[2] * h * 0.7], [w * 0.8, h * 0.3, w * 0.8], q, light, KIND.sway);
    },
    // n blades wherever ok(x, z) holds and nothing solid is in the way
    grass(n, ok, { h = [0.08, 0.2], w = 0.011, colors, r = c.R }) {
      for (let i = 0, tries = 0; i < n && tries < n * 60; tries++) {
        const a = rnd() * TAU, rr = Math.sqrt(rnd()) * r, x = Math.cos(a) * rr, z = Math.sin(a) * rr;
        if (!ok(x, z)) continue;
        const y = c.height(x, z);
        if (c.s.dist(x, y + 0.06, z) < 0.035) continue;
        const k = Math.floor(rnd() * colors.length);
        f.blade(x, y - 0.01, z, h[0] + rnd() * (h[1] - h[0]), w, colors[k][0], colors[k][1]);
        i++;
      }
    },
    dots(n, ok, { y = [0.08, 0.2], size = 0.016, colors, r = c.R, kind = KIND.lit }) {
      for (let i = 0, tries = 0; i < n && tries < n * 60; tries++) {
        const a = rnd() * TAU, rr = Math.sqrt(rnd()) * r, x = Math.cos(a) * rr, z = Math.sin(a) * rr;
        if (!ok(x, z)) continue;
        const y0 = c.height(x, z);
        if (c.s.dist(x, y0 + 0.06, z) < 0.035) continue;
        f.push([x, y0 + y[0] + rnd() * (y[1] - y[0]), z], [size, size, size], [0, 0, 0, 1], colors[Math.floor(rnd() * colors.length)], kind);
        i++;
      }
    },
    // things lying flat on the ground (fallen leaves, petals)
    litter(n, ok, { size = 0.022, colors, r = c.R }) {
      for (let i = 0, tries = 0; i < n && tries < n * 60; tries++) {
        const a = rnd() * TAU, rr = Math.sqrt(rnd()) * r, x = Math.cos(a) * rr, z = Math.sin(a) * rr;
        if (!ok(x, z)) continue;
        const y = c.height(x, z);
        if (c.s.dist(x, y + 0.06, z) < 0.03) continue;
        f.push([x, y + 0.012, z], [size, size * 0.7, 0.004], FLAT, colors[Math.floor(rnd() * colors.length)], KIND.lit);
        i++;
      }
    },
    // soft halo: nested translucent discs that dither denser towards the middle
    glow(x, y, z, r, col) {
      for (const [k, a] of [[1, 0.14], [0.62, 0.3], [0.32, 0.55]]) f.push([x, y, z], [r * k, r * k, r * k], [0, 0, 0, 1], col, KIND.glow, a);
    },
  };
  return f;
}

// ---- shared pieces ---------------------------------------------------------
export function campfire(c, M, x, z, lights, embers, power = 1.5) {
  const { s } = c, y = c.height(x, z);
  s.bound(x, y + 0.12, z, 0.4);
  for (let k = 0; k < 9; k++) s.add(sphere(x + Math.cos(k * 0.698) * 0.17, y + 0.02, z + Math.sin(k * 0.698) * 0.17, 0.045), M.rock);
  for (let k = 0; k < 4; k++) s.add(cone([x + Math.cos(k * 1.57) * 0.14, y + 0.02, z + Math.sin(k * 1.57) * 0.14], [x, y + 0.15, z], 0.028, 0.022), M.dark);
  s.add(sphere(x, y + 0.13, z, 0.07), M.flame).add(sphere(x + 0.012, y + 0.21, z, 0.05), M.flame, 0.02).add(sphere(x, y + 0.29, z + 0.012, 0.026), M.flame, 0.02);
  s.bound();
  lights.push({ pos: [x, y + 0.32, z], color: [1.0, 0.55, 0.2], power });
  embers.push([x, y + 0.25, z]);
}
export const stoneLantern = (c, M, x, z, lights, free) => {
  const { s } = c, y = c.height(x, z);
  s.bound(x, y + 0.3, z, 0.45);
  s.add(cyl(x, y + 0.03, z, 0.1, 0.03, 0.01), M.stone).add(cyl(x, y + 0.2, z, 0.035, 0.15, 0.008), M.stone).add(box(x, y + 0.37, z, 0.085, 0.02, 0.085, 0.006), M.stone);
  s.add(box(x, y + 0.45, z, 0.058, 0.06, 0.058, 0.006), M.glow);
  for (const [dx, dz] of [[1, 1], [1, -1], [-1, 1], [-1, -1]]) s.add(box(x + dx * 0.058, y + 0.45, z + dz * 0.058, 0.012, 0.062, 0.012, 0.003), M.stone);
  [[0.13, 0.02, 0.53], [0.095, 0.02, 0.565], [0.055, 0.02, 0.6]].forEach(([w, hh, yy]) => s.add(box(x, y + yy, z, w, hh, w, 0.008), M.roof));
  s.add(sphere(x, y + 0.64, z, 0.025), M.roof);
  s.bound();
  lights.push({ pos: [x, y + 0.45, z], color: [1.0, 0.72, 0.32], power: 0.95 });
  free.glow(x, y + 0.45, z, 0.26, rgb("#ffcf5a"));
  c.grow(y + 0.75);
};

// Light shafts: beams of sunlight in the air, as a particle entry for a scene.
// Each beam is a row of long, faint, translucent splats laid end to end along
// the sun's direction, scattered a little sideways so the beam has streaks.
// The pixel pass blends them over what is behind and snaps the result to the
// palette, so a beam reads as a flat band of muted colour.
//   beams: [{ to: [x, y, z] where it lands, len, r (half width) }]
export function shafts(rnd, beams, { colors, alpha = 0.3, n = 14 } = {}) {
  const dir = [-SUN[0], -SUN[1], -SUN[2]], q = quatFromTo(UP, dir);
  const side = norm([-dir[2], 0, dir[0]]); // across the beam, horizontal
  const splats = [];
  for (const b of beams) {
    const len = b.len ?? 2.2, r = b.r ?? 0.14;
    for (let k = 0; k < n; k++) {
      const u = (k + 0.5) / n, w = (rnd() - 0.5) * r * 1.6, t = len * (1 - u), thick = (r * (0.3 + 0.4 * rnd())) / 1.5;
      splats.push([b.to[0] - dir[0] * t + side[0] * w, b.to[1] - dir[1] * t + side[1] * w, b.to[2] - dir[2] * t + side[2] * w,
        thick, (len / n) * 1.1, thick, alpha * (0.45 + 0.55 * u), ...q]);
    }
  }
  return { mode: "ray", colors, center: [0, 0, 0], splats };
}
