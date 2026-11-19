// Building blocks for islands and scenes: a context to build into, the
// floating island itself, a few shared props, and the bake step that turns
// the finished SDF scene into painted surfels.
//
//   y = 0 is the ground at the centre; everything is in viewer units
//   (a converted model is 2 units tall or wide).

import { sphere, ellipsoid, cone, displace, shape, sampleSurface, box, cyl } from "./sdf.js";
import { vnoise3, hexToRgb } from "../noise.js";

export const rgb = (hex) => hexToRgb(hex);
export const mul = (c, k) => [c[0] * k, c[1] * k, c[2] * k];
export const mix = (a, b, t) => [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t];
export const sstep = (a, b, x) => { const t = Math.max(0, Math.min(1, (x - a) / (b - a))); return t * t * (3 - 2 * t); };
export const TAU = Math.PI * 2;

export function makeCtx(R, rFlat, clear, rnd) {
  const c = { s: shape(), R, rFlat, rnd, top: 0.3, height: () => 0 };
  // a spot beyond the creature's reach in direction a
  c.place = (a, extra = 0.2, tall = true) => {
    const r = Math.min(R - 0.16, clear(a, tall) + extra);
    return [Math.cos(a) * r, Math.sin(a) * r, r];
  };
  // n directions for the big props, least-reached first: tall things go where
  // the creature never swings, so its attack side stays open
  c.angles = (n) => {
    const cand = Array.from({ length: 64 }, (_, i) => (i / 64) * TAU + rnd() * 0.05).sort((a, b) => clear(a, true) - clear(b, true));
    const picked = [];
    for (let sep = TAU / (n * 1.55); picked.length < n; sep *= 0.8)
      for (const a of cand) {
        if (picked.length >= n) break;
        if (picked.every((q) => Math.abs(Math.atan2(Math.sin(a - q), Math.cos(a - q))) >= sep)) picked.push(a);
      }
    return picked;
  };
  c.grow = (y) => (c.top = Math.max(c.top, y));
  return c;
}

// The island: a slab whose top is flat where the creature stands and gets
// bumpier (or rises into dunes / ledges) towards the rim, on a rough rock
// underside with hanging spikes and a few loose rocks drifting around it.
export function island(c, mTop, mUnder, { bump = 0.02, rise = 0, ripple = 0, rough = 0.09, topY = 0 } = {}) {
  const { s, R, rFlat, rnd } = c;
  const seed = rnd() * 50;
  const outline = (x, z) => { const a = Math.atan2(z, x); return R * (1 + 0.16 * (vnoise3(Math.cos(a) * 1.7 + seed, Math.sin(a) * 1.7 + seed, 3) - 0.5)); };
  c.height = (x, z) => {
    const r = Math.hypot(x, z), e = sstep(rFlat, rFlat + 0.3, r);
    return topY + e * (bump * (vnoise3(x * 5 + seed, 1, z * 5) - 0.5) * 2 + rise * sstep(rFlat + 0.1, R, r) * vnoise3(x * 1.4 + seed, 7, z * 1.4))
      + ripple * (0.3 + 0.7 * e) * Math.sin(x * 30 + 4 * vnoise3(x * 2.5, 2, z * 2.5) + z * 7);
  };
  s.add((x, y, z) => {
    const hh = 0.16, dx = Math.hypot(x, z) - outline(x, z) + 0.07, dy = Math.abs(y - c.height(x, z) + hh) - hh + 0.07;
    return Math.hypot(Math.max(dx, 0), Math.max(dy, 0)) + Math.min(Math.max(dx, dy), 0) - 0.07;
  }, mTop);
  const base = ellipsoid(0, topY - 0.12, 0, 0.92 * R, 0.78 * R, 0.92 * R);
  s.add((x, y, z) => Math.max(base(x, y, z) - rough * (vnoise3(x * 4 + seed, y * 4, z * 4) - 0.5) * 2 - 0.04 * vnoise3(x * 11, y * 11, z * 11), y - topY + 0.12), mUnder, 0.09);
  for (let i = 0; i < 8; i++) {
    const a = rnd() * TAU, r = Math.sqrt(rnd()) * 0.62 * R, x = Math.cos(a) * r, z = Math.sin(a) * r;
    const y0 = topY - 0.12 - 0.78 * R * Math.sqrt(1 - (r / (0.92 * R)) ** 2), len = (0.2 + rnd() * 0.5) * R * 0.6;
    s.bound(x, y0 - len / 2, z, len / 2 + 0.35);
    s.add(cone([x, y0 + 0.25, z], [x + (rnd() - 0.5) * 0.15, y0 - len, z + (rnd() - 0.5) * 0.15], 0.16, 0.012), mUnder, 0.08);
  }
  for (let i = 0; i < 6; i++) {
    const a = rnd() * TAU, r = R + 0.18 + rnd() * 0.35, x = Math.cos(a) * r, z = Math.sin(a) * r, y = topY - 0.75 + rnd() * 0.9, q = 0.05 + rnd() * 0.09;
    s.bound(x, y, z, q * 2);
    s.add(displace(ellipsoid(x, y, z, q * 1.2, q * 0.85, q), (px, py, pz) => -0.03 * vnoise3(px * 12, py * 12, pz * 12)), mUnder);
  }
  s.bound();
}

// ---- shared props ---------------------------------------------------------
export function tufts(c, mat, n, hgt = 0.2) {
  const { s, rnd } = c;
  for (let i = 0; i < n; i++) {
    const [x, z] = c.place(rnd() * TAU, 0.12 + rnd() * 0.6, false), y = c.height(x, z);
    s.bound(x, y + hgt / 2, z, hgt);
    for (let k = 0; k < 6; k++) {
      const a = rnd() * TAU, l = 0.03 + rnd() * 0.05, h = hgt * (0.55 + rnd() * 0.6);
      s.add(cone([x, y - 0.02, z], [x + Math.cos(a) * l, y + h, z + Math.sin(a) * l], 0.009, 0.002), mat);
    }
  }
  s.bound();
}
export function rocks(c, mat, n, size = 0.1) {
  const { s, rnd } = c;
  for (let i = 0; i < n; i++) {
    const [x, z] = c.place(rnd() * TAU, 0.1 + rnd() * 0.7, false), r = size * (0.5 + rnd()), y = c.height(x, z);
    s.bound(x, y, z, r * 1.8);
    s.add(displace(ellipsoid(x, y + r * 0.25, z, r * 1.3, r * 0.8, r), (px, py, pz) => -0.3 * r * vnoise3(px * 11, py * 11, pz * 11)), mat, 0.02);
  }
  s.bound();
}
export function bones(c, mat, n) {
  const { s, rnd } = c;
  for (let i = 0; i < n; i++) {
    const [x, z] = c.place(rnd() * TAU, 0.12 + rnd() * 0.5, false), y = c.height(x, z) + 0.02, a = rnd() * TAU, l = 0.14 + rnd() * 0.12;
    const b = [x + Math.cos(a) * l, y + 0.01, z + Math.sin(a) * l];
    s.bound(x, y, z, l + 0.06);
    s.add(cone([x, y, z], b, 0.013, 0.011), mat).add(sphere(x, y, z, 0.024), mat, 0.012).add(sphere(b[0], b[1], b[2], 0.022), mat, 0.012);
  }
  s.bound();
}
// upright jar from a radius profile
export function urn(c, mat, x, z, k = 1) {
  const y = c.height(x, z), prof = [0.05, 0.1, 0.11, 0.07, 0.045, 0.06];
  c.s.bound(x, y + 0.14 * k, z, 0.22 * k);
  for (let i = 0; i < prof.length - 1; i++) c.s.add(cone([x, y + i * 0.055 * k, z], [x, y + (i + 1) * 0.055 * k, z], prof[i] * k, prof[i + 1] * k), mat);
  c.s.bound();
}
export function fire(c, mMetal, mFlame, x, z, hgt, lights, embers, power = 1.2) {
  const { s } = c, y = c.height(x, z);
  s.bound(x, y + hgt / 2, z, hgt / 2 + 0.25);
  for (let k = 0; k < 3; k++) s.add(cone([x + Math.cos(k * 2.09) * 0.09, y - 0.01, z + Math.sin(k * 2.09) * 0.09], [x, y + hgt - 0.04, z], 0.014, 0.014), mMetal);
  s.add(ellipsoid(x, y + hgt, z, 0.12, 0.05, 0.12), mMetal).sub(ellipsoid(x, y + hgt + 0.045, z, 0.1, 0.045, 0.1), mMetal);
  s.add(sphere(x, y + hgt + 0.05, z, 0.065), mFlame).add(sphere(x + 0.012, y + hgt + 0.12, z, 0.045), mFlame, 0.02).add(sphere(x, y + hgt + 0.185, z + 0.012, 0.024), mFlame, 0.02);
  s.bound();
  lights.push({ pos: [x, y + hgt + 0.14, z], color: [1.0, 0.52, 0.18], power });
  embers.push([x, y + hgt + 0.12, z]);
  c.grow(y + hgt + 0.3);
}
// a row of rib arches along a spine, with a horned skull at one end
export function ribcage(c, mat, mDark, a, k = 1) {
  const { s } = c, [x, z, r] = c.place(a, 0.45 * k);
  const rx = Math.cos(a), rz = Math.sin(a), tx = -rz, tz = rx, y = c.height(x, z);
  s.bound(x, y + 0.4 * k, z, 1.0 * k);
  const sp = (t) => [x + tx * t, y + 0.1 * k, z + tz * t];
  s.add(cone(sp(-0.55 * k), sp(0.5 * k), 0.045 * k, 0.03 * k), mat);
  for (let i = 0; i < 6; i++) {
    const p = sp((-0.42 + i * 0.16) * k), hgt = (0.75 - Math.abs(i - 2.2) * 0.07) * k;
    for (const sd of [-1, 1]) {
      const q1 = [p[0] + rx * sd * 0.3 * k, p[1] + hgt * 0.5, p[2] + rz * sd * 0.3 * k], q2 = [p[0] + rx * sd * 0.2 * k, p[1] + hgt, p[2] + rz * sd * 0.2 * k];
      s.add(cone(p, q1, 0.03 * k, 0.024 * k), mat).add(cone(q1, q2, 0.024 * k, 0.01 * k), mat, 0.02);
    }
  }
  const h = sp(-0.72 * k), nose = sp(-0.95 * k);
  s.add(sphere(h[0], h[1] + 0.07 * k, h[2], 0.15 * k), mat).add(sphere(nose[0], nose[1] + 0.02, nose[2], 0.095 * k), mat, 0.08);
  for (const sd of [-1, 1]) {
    s.sub(sphere(h[0] + rx * sd * 0.1 * k - tx * 0.09 * k, h[1] + 0.1 * k, h[2] + rz * sd * 0.1 * k - tz * 0.09 * k, 0.045 * k), mDark);
    s.add(cone([h[0] + rx * sd * 0.1 * k, h[1] + 0.16 * k, h[2] + rz * sd * 0.1 * k], [h[0] + rx * sd * 0.3 * k, h[1] + 0.42 * k, h[2] + rz * sd * 0.3 * k], 0.035 * k, 0.006), mat);
  }
  s.bound();
  c.grow(y + 0.9 * k);
}
export function banner(c, mPole, mCloth, x, z, hgt = 1.5) {
  const y = c.height(x, z);
  c.s.bound(x, y + hgt / 2, z, hgt / 2 + 0.3);
  c.s.add(cone([x, y - 0.03, z], [x, y + hgt, z], 0.02, 0.014), mPole).add(sphere(x, y + hgt + 0.02, z, 0.03), mPole);
  c.s.add(box(x + 0.15, y + hgt - 0.2, z, 0.14, 0.17, 0.012, 0.008), mCloth).add(box(x + 0.2, y + hgt - 0.44, z, 0.07, 0.08, 0.012, 0.008), mCloth);
  c.s.bound();
  c.grow(y + hgt + 0.1);
}
export function deadTree(c, mat, x, z, hgt, lean = 0.2) {
  const { s, rnd } = c, y = c.height(x, z), a = rnd() * TAU;
  s.bound(x, y + hgt / 2, z, hgt * 0.8);
  let p = [x, y - 0.04, z];
  const pts = [p];
  for (let k = 1; k <= 4; k++) {
    const q = [x + Math.cos(a + k * 0.9) * lean * k * 0.3, y + (hgt * k) / 4, z + Math.sin(a + k * 0.9) * lean * k * 0.3];
    s.add(cone(p, q, 0.085 * (1.25 - k * 0.2) * hgt, 0.085 * (1.05 - k * 0.2) * hgt), mat, 0.02);
    pts.push((p = q));
  }
  for (let k = 0; k < 5; k++) s.add(cone([x, y + 0.08, z], [x + Math.cos(k * 1.26 + a) * 0.2 * hgt, y - 0.04, z + Math.sin(k * 1.26 + a) * 0.2 * hgt], 0.045 * hgt, 0.015), mat, 0.03);
  const tips = [];
  for (let k = 0; k < 5; k++) {
    const from = pts[2 + (k % 3)], da = a + k * 1.4, len = (0.28 + rnd() * 0.25) * hgt;
    const mid = [from[0] + Math.cos(da) * len * 0.6, from[1] + len * 0.35, from[2] + Math.sin(da) * len * 0.6];
    const tip = [from[0] + Math.cos(da + 0.4) * len, from[1] + len * 0.85, from[2] + Math.sin(da + 0.4) * len];
    s.add(cone(from, mid, 0.03 * hgt, 0.018 * hgt), mat, 0.015).add(cone(mid, tip, 0.018 * hgt, 0.005), mat, 0.01);
    tips.push(mid, tip);
  }
  s.bound();
  c.grow(y + hgt * 1.25);
  return tips;
}

// Sample a finished scene into painted surfels.
//   tint: optional average colour the ground should lean towards
//   shadowR: radius of the soft contact shadow at the centre (0 = none)
//   lighting: from makeLighting (light.js): bake sun, shadows and lamps into
//             each surfel and store its final palette colour (scenes)
export function bake(c, t, { tint = null, shadowR = 0, h = 0.0215, ext = c.R + 0.75, bottom = -0.3 - c.R * 1.25, lighting = null } = {}) {
  const surf = sampleSurface(c.s, [-ext, bottom, -ext], [ext, c.top + 0.15, ext], h);
  const count = surf.mat.length;
  const col = new Uint8Array(count * 4);
  const grey = tint ? (tint[0] + tint[1] + tint[2]) / 3 : 0;
  const lean = tint ? tint.map((v) => 0.5 + (v - grey) * 0.9) : null;
  for (let i = 0; i < count; i++) {
    const x = surf.p[i * 3], y = surf.p[i * 3 + 1], z = surf.p[i * 3 + 2];
    const n = [surf.n[i * 3], surf.n[i * 3 + 1], surf.n[i * 3 + 2]];
    let { c: k, emissive, leaf } = t.paint(surf.mat[i], x, y, z, n);
    if (!emissive) {
      if (lean) k = mix(k, [k[0] * lean[0] * 2, k[1] * lean[1] * 2, k[2] * lean[2] * 2], 0.2);
      if (!lighting) k = mul(k, 0.5 + 0.5 * surf.ao[i]);
      // darker towards the bottom of the island, soft contact shadow on top
      if (y < -0.2 && !t.noDepthShade) k = mul(k, 1 - 0.45 * sstep(-0.2, -c.R, y));
      if (shadowR && !lighting && n[1] > 0.3 && y < 0.08 && y > -0.1) k = mul(k, 1 - 0.36 * Math.exp(-(x * x + z * z) / (0.4 * shadowR * shadowR)));
      if (lighting) k = lighting.shade(k, x, y, z, n[0], n[1], n[2], { ao: surf.ao[i], leaf });
    }
    col[i * 4] = Math.max(0, Math.min(255, k[0] * 255));
    col[i * 4 + 1] = Math.max(0, Math.min(255, k[1] * 255));
    col[i * 4 + 2] = Math.max(0, Math.min(255, k[2] * 255));
    col[i * 4 + 3] = emissive ? 1 : 0;
  }
  return { p: new Float32Array(surf.p), n: new Float32Array(surf.n), col, count, h, R: c.R, top: c.top, bottom: -0.2 - c.R * 1.05, lights: t.lights, particles: t.particles };
}
