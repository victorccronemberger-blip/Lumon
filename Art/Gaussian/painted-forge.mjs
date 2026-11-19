// Portable Y-up forge study. Local origin is directly under the fire.
import { PaintedSurface, blend, dot } from "./painted-surface.mjs";
import { addFirePose } from "./painted-flame.mjs";
import { fileURLToPath } from "node:url";
import path from "node:path";

const palettes = {
  brick: ["502d36", "9e4d39", "c97650", "eda479"],
  sandstone: ["625360", "a18b86", "cfb596", "ecd6ac"],
  footing: ["454253", "736b76", "a39a99", "cbbcaf"],
  paving: ["645b68", "a19699", "cbbba8", "e1cfb4"],
  iron: ["242b3b", "424655", "5e6170", "92909a"],
  collar: ["303849", "59667a", "899bb0", "c3d0d4"],
  steel: ["394555", "708191", "a6b8c4", "e1e9dc"],
  wood: ["4e3438", "835247", "ae7451", "ce9b63"],
  charcoal: ["332e3a", "563a3b", "8e4637", "d27645"],
  joint: ["312e39", "463c46", "564b53", "66575b"],
  pine: ["243c45", "375952", "5f805f", "9caf78"],
  tip: ["324d4a", "567457", "85a268", "b5c885"],
};
for (const key of Object.keys(palettes))
  palettes[key] = palettes[key].map((h) => [0, 2, 4].map((i) => parseInt(h.slice(i, i + 2), 16)));
const saturate = (x) => Math.max(0, Math.min(1, x));
const frac = (x) => x - Math.floor(x);
const hash = (x) => frac(Math.sin(x * 127.1 + 71.7) * 43758.5453);
const smooth = (t) => t * t * (3 - 2 * t);
function noise(x, y) {
  const a = Math.floor(x),
    b = Math.floor(y),
    u = smooth(frac(x)),
    v = smooth(frac(y));
  const k = a + b * 79;
  return (hash(k) * (1 - u) + hash(k + 1) * u) * (1 - v) + (hash(k + 79) * (1 - u) + hash(k + 80) * u) * v;
}
const light = [0.2, 0.78, 0.59];
function value(material, t) {
  const row = palettes[material],
    f = saturate(t) * 3,
    i = Math.min(2, Math.floor(f));
  return blend(row[i], row[i + 1], f - i);
}
function painted(material, seed = 0, bias = 0) {
  return (p, n, u, v) => {
    const [x, y, z] = p;
    const brush = noise((x + z * 0.57) * 17 + seed * 9, y * 21 + z * 8);
    const wash = noise(x * 7 - z * 9 + seed, y * 9 + z * 5);
    let tone = 0.36 + 0.47 * dot(n, light) + bias + (hash(seed) - 0.5) * 0.125;
    tone += (brush > 0.62 ? 0.038 : brush < 0.31 ? -0.03 : 0) + (wash - 0.5) * 0.1;
    if (material === "wood") {
      const grain = Math.abs(Math.sin((x + z) * 93 + wash * 3 + seed));
      tone += grain < 0.16 ? -0.12 : grain > 0.95 ? 0.055 : 0;
    }
    if (material === "brick" || material === "sandstone" || material === "paving") {
      // Short, broken strata follow the bed of each block rather than covering it with grain.
      const ray = x * 0.83 + z * 0.56,
        course = y + (n[1] > 0.7 ? z * 0.68 : 0);
      const mark = noise(ray * 24 + seed * 2, course * 72 + wash * 1.9);
      if (mark > 0.74 && brush > 0.39) tone -= 0.115;
      if (mark < 0.17 && brush > 0.47) tone += 0.065;
    }
    return value(material, tone);
  };
}
const ring = (x, y, z, w, d, cut) => [
  [x - w + cut, y, z - d],
  [x - w, y, z - d + cut],
  [x - w, y, z + d - cut],
  [x - w + cut, y, z + d],
  [x + w - cut, y, z + d],
  [x + w, y, z + d - cut],
  [x + w, y, z - d + cut],
  [x + w - cut, y, z - d],
];
function join(s, lower, upper, paint) {
  for (let i = 0; i < lower.length; i++) {
    const j = (i + 1) % lower.length;
    s.quad(lower[i], lower[j], upper[j], upper[i], paint);
  }
}
function rivet(col, u, v, ru, rv) {
  const d = Math.hypot(u / ru, v / rv);
  if (d > 1.16) return col;
  if (d > 0.9) return blend(col, [36, 40, 48], 0.73);
  const h = saturate(0.52 - (0.3 * u) / ru + (0.41 * v) / rv);
  return value("steel", 0.21 + 0.74 * h);
}
function block(s, x, z, y0, y1, w, d, material, seed, bevel = 0.009, capPaint, sidePaint) {
  const cut = Math.min(w * 0.28, d * 0.3, bevel * 1.9);
  const b = Math.min(bevel, (y1 - y0) * 0.27);
  const top = ring(x, y1, z, w - b, d - b, cut * 0.8);
  const lower = ring(x, y0 + b, z, w, d, cut),
    upper = ring(x, y1 - b, z, w, d, cut);
  join(s, ring(x, y0, z, w - b, d - b, cut * 0.8), lower, painted(material, seed, -0.08));
  const facePaint = painted(material, seed);
  const masonry = ["brick", "footing", "sandstone"].includes(material);
  join(
    s,
    lower,
    upper,
    sidePaint ??
      ((p, n, u, v) => {
        const col = facePaint(p, n, u, v);
        if (!masonry) return col;
        const end = Math.abs(n[2]) > Math.abs(n[0]) ? w - Math.abs(p[0] - x) : d - Math.abs(p[2] - z);
        return blend(col, [56, 45, 51], p[1] - y0 < 0.012 ? 0.48 : end < 0.009 ? 0.38 : 0);
      }),
  );
  join(s, upper, top, painted(material, seed, 0.1));
  const paint = capPaint ?? painted(material, seed);
  s.quad(
    [x - w + b, y1, z + d - b],
    [x + w - b, y1, z + d - b],
    [x + w - b, y1, z - d + b],
    [x - w + b, y1, z - d + b],
    (p, n, u, v) =>
      Math.abs(p[0] - x) + Math.abs(p[2] - z) > w + d - 2 * b - cut * 0.8 ? null : paint(p, n, u, v),
  );
}
function courses(s, x, z, bottom, top, w, d, rows, count, material, seed) {
  block(s, x, z, bottom, top - 0.009, w - 0.013, d - 0.013, "joint", seed, 0.004);
  const h = (top - bottom) / rows,
    step = (2 * w) / count,
    sideStep = (2 * d) / Math.max(1, Math.round((2 * d) / step));
  for (let row = 0; row < rows; row++) {
    const lay = (half, pitch, place) => {
      for (let i = -1; i < Math.ceil((2 * half) / pitch); i++) {
        const a = Math.max(-half, -half + (i + (row % 2) * 0.5) * pitch);
        const b = Math.min(half, -half + (i + 1 + (row % 2) * 0.5) * pitch);
        if (b - a > 0.025) place((a + b) / 2, (b - a) / 2 - 0.003, i);
      }
    };
    const y = bottom + row * h + 0.002;
    for (const side of [-1, 1]) {
      lay(w, step, (c, r, i) =>
        block(
          s,
          x + c,
          z + side * (d - 0.021),
          y,
          y + h - 0.004,
          r,
          0.024,
          material,
          seed + row * 9 + i + side,
          0.005,
        ),
      );
      lay(d - 0.046, sideStep, (c, r, i) =>
        block(
          s,
          x + side * (w - 0.02),
          z + c,
          y,
          y + h - 0.004,
          0.024,
          r,
          material,
          seed + 48 + row * 9 + i + side,
          0.005,
        ),
      );
    }
  }
}
function foundation(s) {
  const x = -0.13,
    w = 0.635,
    d = 0.46;
  const curb = painted("footing", 2);
  block(s, x, 0, -0.018, 0.141, w, d, "footing", 2, 0.017, undefined, (p, n, u, v) => {
    const horizontal = Math.abs(n[2]) > 0.7 ? p[0] - x + w : p[2] + d;
    const t = frac(horizontal / 0.166);
    const col = curb(p, n, u, v);
    return t < 0.055 ? blend(col, [57, 52, 62], 0.66) : t < 0.082 ? blend(col, [210, 196, 173], 0.42) : col;
  });
  const stripes = 7,
    step = 0.16;
  for (let j = 0; j < stripes; j++) {
    for (let i = -1; i < Math.ceil((2 * w) / step); i++) {
      const a = Math.max(-w, -w + (i + (j % 2) * 0.5) * step),
        b = Math.min(w, -w + (i + 1 + (j % 2) * 0.5) * step);
      if (b - a < 0.02) continue;
      const material = j === 0 || j === stripes - 1 ? "footing" : "paving",
        tile = painted(material, 15 + j * 7 + i);
      block(
        s,
        x + (a + b) / 2,
        -d + ((j + 0.5) * 2 * d) / stripes,
        0.115,
        0.182,
        (b - a) * 0.5 - 0.003,
        d / stripes - 0.003,
        material,
        15 + j * 7 + i,
        0.007,
        (p, n, u, v) => {
          const dist = (cx, cz, w, h) =>
            Math.hypot(Math.max(0, Math.abs(p[0] - cx) - w), Math.max(0, Math.abs(p[2] - cz) - h));
          const contact = Math.min(dist(0.022, -0.116, 0.35, 0.281), dist(-0.444, 0.166, 0.131, 0.133));
          let col = tile(p, n, u, v);
          if (u < 0.05 || v > 0.94) col = blend(col, [65, 56, 64], 0.52);
          else if (u > 0.953 || v < 0.05) col = blend(col, [227, 213, 184], 0.6);
          return blend(col, [55, 53, 67], 0.43 * saturate(1 - contact / 0.093) ** 1.3);
        },
      );
    }
  }
}
function arch(s) {
  const cy = 0.755,
    front = 0.226,
    back = 0.068,
    sectors = 9;
  const fill = painted("brick", 82);
  s.quad(
    [-0.347, cy, 0.128],
    [0.347, cy, 0.128],
    [0.347, 1.005, 0.128],
    [-0.347, 1.005, 0.128],
    (p, n, u, v) => {
      if ((p[0] / 0.255) ** 2 + ((p[1] - cy) / 0.21) ** 2 < 1) return null;
      const row = (p[1] - cy) / 0.087,
        joint = Math.min(frac(row), frac((p[0] + 0.43) / 0.178 + 0.5 * Math.floor(row)));
      return joint < 0.046 ? value("joint", 0.47) : fill(p, n, u, v);
    },
  );
  for (let i = 0; i < sectors; i++) {
    const key = i === 4,
      material = key ? "sandstone" : "brick";
    const a = (i * Math.PI) / sectors + 0.013,
      b = ((i + 1) * Math.PI) / sectors - 0.013;
    const point = (angle, outer, z, offset = 0) => [
      (outer ? 0.349 : 0.211) * Math.cos(angle),
      cy + (outer ? 0.262 + (key ? 0.029 : 0) : 0.165 - (key ? 0.016 : 0)) * Math.sin(angle) + offset,
      z,
    ];
    const face = [
      point(a, false, front),
      point(a, true, front),
      point(b, true, front),
      point(b, false, front),
    ];
    const center = face.reduce((out, p) => out.map((v, k) => v + p[k] / 4), [0, 0, 0]);
    const inset = face.map((p) => [
      p[0] + (center[0] - p[0]) * 0.11,
      p[1] + (center[1] - p[1]) * 0.11,
      p[2] + 0.008 + (key ? 0.012 : 0),
    ]);
    const rear = face.map((p) => [p[0], p[1], back]);
    for (let j = 0; j < 4; j++) {
      const k = (j + 1) % 4;
      s.quad(rear[j], rear[k], face[k], face[j], painted(material, 65 + i));
      s.quad(face[j], face[k], inset[k], inset[j], painted(material, 65 + i, 0.08));
    }
    s.quad(...inset, painted(material, 65 + i));
  }
}
function rivetHead(s, center, tangent, uphill, radius) {
  const normal = [
    tangent[1] * uphill[2] - tangent[2] * uphill[1],
    tangent[2] * uphill[0] - tangent[0] * uphill[2],
    tangent[0] * uphill[1] - tangent[1] * uphill[0],
  ];
  const spacing = radius / 4.4;
  for (let i = -4; i <= 4; i++)
    for (let j = -4; j <= 4; j++) {
      const u = i / 4.4,
        v = j / 4.4,
        radial = Math.hypot(u, v);
      if (radial > 1) continue;
      const crown = Math.sqrt(Math.max(0, 1 - radial * radial));
      const p = center.map(
        (v0, k) =>
          v0 + radius * (u * tangent[k] + v * uphill[k]) + normal[k] * (0.002 + crown * radius * 0.68),
      );
      const n = normal.map((val, k) => val * crown + 0.62 * (u * tangent[k] + v * uphill[k]));
      const tone = radial > 0.79 ? 0.22 : 0.43 + 0.52 * Math.max(0, dot(n, light));
      s.point(p, n, value("steel", tone), spacing);
    }
}
function hood(s) {
  const z = -0.09;
  const corners = (w, d, y) => [
    [-w, y, z - d],
    [-w, y, z + d],
    [w, y, z + d],
    [w, y, z - d],
  ];
  const bottom = corners(0.374, 0.304, 1.026),
    top = corners(0.149, 0.133, 1.403);
  for (let i = 0; i < 4; i++) {
    const j = (i + 1) % 4,
      base = painted("iron", 88 + i);
    s.quad(bottom[i], bottom[j], top[j], top[i], (p, n, u, v) => {
      const end = Math.min(u, 1 - u);
      if (end < 0.063) {
        let col = value("steel", 0.48 + 0.3 * dot(n, light) + (end < 0.013 ? 0.24 : end > 0.053 ? -0.23 : 0));
        for (const row of [0.12, 0.46, 0.81]) col = rivet(col, end - 0.032, v - row, 0.021, 0.021);
        return col;
      }
      const seam = Math.abs(frac(u * 3 + 0.5) - 0.5);
      let col = base(p, n, u, v);
      if (seam < 0.022) col = blend(col, [35, 38, 49], 0.78);
      else if (seam < 0.049) col = blend(col, [153, 148, 142], 0.45);
      const lap = v - (0.45 + 0.063 * (Math.floor(u * 3) % 2));
      if (Math.abs(lap) < 0.009) col = blend(col, lap < 0 ? [37, 37, 48] : [163, 153, 147], 0.64);
      const brushed = noise(u * 9 + i * 4, v * 7);
      if (brushed > 0.65 && noise(u * 38, v * 32) > 0.52) col = blend(col, [147, 139, 136], 0.18);
      return col;
    });
    const unit = (v) => v.map((k) => k / Math.hypot(...v));
    const tangent = unit(bottom[j].map((v, k) => v - bottom[i][k]));
    const mid = blend(bottom[i], bottom[j], 0.5),
      peak = blend(top[i], top[j], 0.5);
    const uphill = unit(peak.map((v, k) => v - mid[k]));
    for (const v of [0.13, 0.47, 0.81])
      for (const u of [0.035, 0.965]) {
        const center = blend(blend(bottom[i], bottom[j], u), blend(top[i], top[j], u), v);
        rivetHead(s, center, tangent, uphill, 0.01);
      }
  }
  block(s, 0, z, 0.986, 1.036, 0.38, 0.308, "steel", 45, 0.009);
  block(s, 0, z, 1.378, 1.433, 0.173, 0.15, "steel", 46, 0.008);
  block(s, 0, z, 1.427, 1.649, 0.144, 0.124, "collar", 54, 0.007);
  block(s, 0, z, 1.629, 1.66, 0.153, 0.132, "iron", 51, 0.004);
  block(s, 0, z, 1.658, 1.712, 0.132, 0.113, "joint", 52, 0.003);
  const steel = painted("steel", 53, 0.01);
  block(s, 0, z, 1.696, 1.764, 0.186, 0.167, "steel", 53, 0.01, (p, n, u, v) => {
    const du = Math.min(u, 1 - u),
      dv = Math.min(v, 1 - v);
    let col = steel(p, n, u, v);
    if (Math.min(du, dv) < 0.033) col = blend(col, [228, 236, 224], 0.88);
    else if (Math.min(du, dv) < 0.054) col = blend(col, [83, 96, 113], 0.5);
    const scuff = Math.abs(v - (0.28 + 0.49 * u + 0.014 * Math.sin(u * 19)));
    if (scuff < 0.011 && u > 0.24 && u < 0.41) col = blend(col, [220, 228, 222], 0.62);
    return rivet(col, du - 0.115, dv - 0.119, 0.055, 0.06);
  });
  for (const x of [-0.14, 0.14])
    for (const dz of [-0.121, 0.121]) rivetHead(s, [x, 1.766, z + dz], [1, 0, 0], [0, 0, -1], 0.014);
}
function anvil(s) {
  const x = -0.467,
    z = 0.187;
  courses(s, x, z, 0.18, 0.462, 0.133, 0.134, 3, 2, "brick", 201);
  block(s, x, z, 0.452, 0.488, 0.145, 0.145, "iron", 204, 0.008);
  const rings = [
    [0.489, 0.118, 0.114],
    [0.525, 0.123, 0.115],
    [0.584, 0.074, 0.071],
    [0.632, 0.068, 0.067],
    [0.677, 0.138, 0.094],
    [0.731, 0.149, 0.098],
  ];
  for (let i = 1; i < rings.length; i++) {
    const [a, aw, ad] = rings[i - 1],
      [b, bw, bd] = rings[i];
    join(
      s,
      ring(x, a, z, aw, ad, 0.011),
      ring(x, b, z, bw, bd, 0.011),
      painted(i >= 4 ? "steel" : "iron", 211 + i),
    );
  }
  const table = painted("steel", 219, 0.024);
  block(s, x, z, 0.72, 0.746, 0.15, 0.098, "steel", 219, 0.004, (p, n, u, v) => {
    const edge = Math.min(u, 1 - u, v, 1 - v);
    let col = table(p, n, u, v);
    if (edge < 0.034) col = blend(col, [223, 232, 223], 0.81);
    const scratch = Math.abs(v - (0.19 + 0.54 * u));
    if (scratch < 0.015 && u > 0.39 && u < 0.65) col = blend(col, [219, 227, 223], 0.55);
    if (Math.max(Math.abs(u - 0.84), Math.abs((v - 0.31) * 0.67)) < 0.048) col = value("iron", 0.2);
    return col;
  });
  const nose = (dx, y, ry, rz) => [
    [x + dx, y + ry, z - rz * 0.52],
    [x + dx, y + ry, z + rz * 0.52],
    [x + dx, y, z + rz],
    [x + dx, y - ry, z + rz * 0.48],
    [x + dx, y - ry, z - rz * 0.48],
    [x + dx, y, z - rz],
  ];
  const profiles = [
    nose(-0.144, 0.703, 0.039, 0.09),
    nose(-0.23, 0.705, 0.033, 0.071),
    nose(-0.328, 0.703, 0.012, 0.02),
    nose(-0.349, 0.705, 0.004, 0.006),
  ];
  for (let i = 1; i < profiles.length; i++)
    join(s, profiles[i - 1].toReversed(), profiles[i].toReversed(), painted("steel", 224));
}
function forge(s) {
  foundation(s);
  courses(s, 0, -0.095, 0.182, 0.518, 0.355, 0.284, 3, 4, "footing", 72);
  block(s, 0, -0.059, 0.499, 0.566, 0.378, 0.3, "sandstone", 41, 0.008);
  for (let i = 0; i < 5; i++)
    block(s, (i - 2) * 0.149, 0.169, 0.53, 0.593, 0.072, 0.092, "sandstone", 30 + i, 0.009);
  courses(s, 0, -0.298, 0.553, 1.012, 0.35, 0.093, 5, 4, "brick", 135);
  for (const side of [-1, 1]) {
    courses(s, side * 0.281, -0.097, 0.559, 0.986, 0.07, 0.215, 4, 1, "brick", 109 + side);
    courses(s, side * 0.283, 0.156, 0.564, 0.763, 0.067, 0.071, 2, 1, "sandstone", 65 + side);
  }
  block(s, 0, -0.022, 0.559, 0.61, 0.222, 0.172, "charcoal", 168, 0.01);
  for (let i = 0; i < 9; i++)
    block(
      s,
      ((i % 3) - 1) * 0.101 + (hash(i) - 0.5) * 0.035,
      Math.floor(i / 3) * 0.066 - 0.09,
      0.59,
      0.629 + hash(i + 61) * 0.024,
      0.049,
      0.043,
      "charcoal",
      179 + i,
      0.012,
    );
  arch(s);
  hood(s);
  anvil(s);
}

// Overlapping boughs hang from an actual trunk. Each skirt has scalloped tips and an underside.
function pine(s, x, z, height, width, seed) {
  block(s, x, z, 0.015, 0.48 * height, 0.043, 0.042, "wood", seed, 0.012);
  const tiers = [
    [0.17, 0.45, 1],
    [0.37, 0.4, 0.84],
    [0.55, 0.33, 0.67],
    [0.71, 0.29, 0.45],
  ];
  for (const [tier, [foot, span, radius]] of tiers.entries()) {
    const count = 10 - tier,
      phase = hash(seed + tier) * Math.PI * 2;
    const point = (azimuth, t) => {
      const angle = azimuth * count + phase + Math.sin(azimuth * 3 + phase) * 0.52;
      const lobe = (1 + Math.cos(angle)) * 0.5;
      // Round the tips underneath before tapering up toward the next overlapping whorl.
      const brim =
        t < 0.14 ? 0.62 + 0.38 * Math.sin(((t / 0.14) * Math.PI) / 2) : (1 - (t - 0.14) / 0.86) ** 0.76;
      const reach = width * radius * brim * (0.67 + 0.09 * lobe);
      const y = height * (foot + span * t - 0.044 * lobe * (1 - t) ** 2);
      const lean = 0.033 * (foot + span * t);
      return [x + lean + Math.cos(azimuth) * reach, y, z + Math.sin(azimuth) * reach];
    };
    const segments = 60,
      rows = 23;
    for (let j = 0; j < rows; j++)
      for (let i = 0; i < segments; i++) {
        const a = (2 * Math.PI * i) / segments,
          b = (2 * Math.PI * (i + 1)) / segments,
          t = j / rows,
          tt = (j + 1) / rows;
        s.quad(
          point(b, t),
          point(a, t),
          point(a, tt),
          point(b, tt),
          (p, n, u, v) => {
            const az = b + (a - b) * u,
              lift = t + (tt - t) * v;
            const wave = Math.cos(az * count + phase + 0.52 * Math.sin(az * 3 + phase));
            const wash = noise(p[0] * 21 + p[1] * 3, p[2] * 20 + p[1] * 6);
            const face = dot(n, light) + 0.12 * wave + 0.07 * (wash - 0.5);
            const tone = face < 0.04 ? 0.23 : face < 0.41 ? 0.4 : face < 0.7 ? 0.53 : 0.68;
            return value(tier > 1 ? "tip" : "pine", tone + tier * 0.027 - 0.09 - (lift < 0.11 ? 0.09 : 0));
          },
          0.009,
        );
      }
    const branches = [9, 8, 7, 5][tier];
    for (let i = 0; i < branches; i++) {
      const key = seed + 41 * tier + i;
      const angle = phase + (2 * Math.PI * (i + 0.2 * hash(key + 17))) / branches;
      const radial = [Math.cos(angle), Math.sin(angle)];
      const length = width * radius * (0.9 + 0.14 * hash(key));
      const wing = length * Math.sin(Math.PI / branches) * 1.21;
      const rootHeight = foot + span * (0.89 + 0.08 * hash(key + 3));
      const endHeight = foot + 0.008 + 0.048 * (hash(key + 8) - 0.5);
      // Closed sprays have a narrow shoulder, a broad three-lobed end and a cool underside.
      // Adjacent whorls are rotated so their tips overlap at different heights.
      function branch(t, roll) {
        const profile = Math.sin(Math.PI * t) ** 0.64;
        const lateral = Math.cos(roll),
          upper = Math.sin(roll);
        const fingers = Math.cos(lateral * 2.4 * Math.PI + 0.25 * key);
        const spread = wing * profile * (0.55 + 0.83 * t);
        const tip = t ** 2 * profile * (0.026 * fingers - 0.018 * lateral * lateral);
        const reach = length * (0.09 + 0.89 * t + 0.09 * profile * upper);
        const side = lateral * spread + 0.06 * length * t * Math.sin(key);
        const y =
          height * (rootHeight + (endHeight - rootHeight) * t - tip) + upper * length * 0.19 * profile;
        return [
          x + 0.028 * rootHeight + radial[0] * reach - radial[1] * side,
          y,
          z + radial[1] * reach + radial[0] * side,
        ];
      }
      function needles(p, n) {
        const grain = noise(p[0] * 23 + key, p[1] * 19 + p[2] * 22);
        const lightness = dot(n, light) + 0.1 * (grain - 0.5);
        const plane = lightness < -0.13 ? 0.22 : lightness < 0.28 ? 0.37 : lightness < 0.66 ? 0.51 : 0.65;
        return value(tier > 1 ? "tip" : "pine", plane + tier * 0.033 + (hash(key) - 0.5) * 0.065);
      }
      for (let row = 0; row < 20; row++)
        for (let side = 0; side < 24; side++) {
          const t = row / 20,
            tt = (row + 1) / 20;
          const a = (side * Math.PI) / 12,
            b = ((side + 1) * Math.PI) / 12;
          s.quad(branch(t, a), branch(tt, a), branch(tt, b), branch(t, b), needles, 0.009);
        }
    }
  }
}

export function makePaintedBench() {
  const s = new PaintedSurface(0.0105);
  for (const side of [-1, 1]) {
    const x = side * 0.43;
    block(s, x, 0, -0.01, 0.104, 0.095, 0.167, "footing", 42 + side, 0.016);
    block(s, x, -0.017, 0.101, 0.283, 0.06, 0.081, "iron", 51, 0.007);
    block(s, x, -0.129, 0.278, 0.66, 0.035, 0.046, "iron", 52, 0.005);
    block(s, x, 0.035, 0.314, 0.44, 0.027, 0.091, "iron", 53, 0.005);
  }
  function plank(x, z, y, w, h, d, seed) {
    const grain = (p, n) => {
      const longitudinal = p[0],
        across = p[2] * Math.max(n[1], 0) + p[1] * (1 - Math.max(n[1], 0));
      const knot = Math.hypot((longitudinal + 0.18 + 0.09 * hash(seed)) * 1.3, (across + 0.032) * 4.6);
      const wave = Math.sin(
        across * 152 + noise(longitudinal * 3 + seed, across * 12) * 4 + Math.exp(-knot * 10) * 3.5,
      );
      return value(
        "wood",
        0.55 +
          0.25 * dot(n, light) +
          0.048 * Math.floor(3 * noise(longitudinal * 6, across * 38 + seed)) -
          (wave < -0.8 ? 0.17 : 0),
      );
    };
    const timber = ring(x, y, z, w, d, 0.011),
      top = ring(x, y + h, z, w - 0.005, d - 0.005, 0.01);
    join(s, timber, ring(x, y + h - 0.007, z, w, d, 0.011), grain);
    join(s, ring(x, y + h - 0.007, z, w, d, 0.011), top, painted("wood", seed, 0.15));
    s.quad(
      [x - w + 0.009, y + h, z + d - 0.007],
      [x + w - 0.009, y + h, z + d - 0.007],
      [x + w - 0.009, y + h, z - d + 0.007],
      [x - w + 0.009, y + h, z - d + 0.007],
      (p, n, u, v) => {
        const col = grain(p, n);
        return rivet(col, Math.abs(p[0]) - 0.43, (v - 0.5) * 2 * d, 0.016, 0.016);
      },
    );
  }
  for (const z of [-0.098, 0.098]) plank(0, z, 0.286, 0.61, 0.073, 0.082, 32 + z);
  for (const y of [0.445, 0.573]) plank(0, -0.128, y, 0.603, 0.097, 0.048, 38 + y);
  for (const side of [-1, 1]) plank(side * 0.43, 0.035, 0.429, 0.046, 0.052, 0.126, 82);
  return s.finish(light);
}

function broadleaf(s, x, z, height, width, seed) {
  block(s, x, z, 0.012, height * 0.59, 0.054, 0.053, "wood", seed, 0.014);
  function crown(cx, cy, cz, rx, ry, rz, key, tier) {
    const phase = hash(key) * Math.PI * 2;
    const point = (az, el) => {
      const belt = Math.cos(el),
        rise = Math.sin(el);
      const fingers = Math.cos(az * 5 + 0.9 * Math.sin(az * 2 + phase) + phase);
      const bulge = 1 + 0.092 * fingers * belt + 0.048 * Math.sin(az * 3 - el * 4 + phase) * belt;
      return [
        cx + rx * belt * bulge * Math.cos(az) + 0.09 * rx * rise,
        cy + ry * (rise - (0.14 * fingers * belt * belt * (1 - rise)) / 2),
        cz + rz * belt * bulge * Math.sin(az),
      ];
    };
    const color = (p, n) => {
      const wash = noise(p[0] * 15 + p[1] * 2, p[2] * 16 + p[1] * 7);
      const incidence = dot(n, light) + 0.18 * (wash - 0.5);
      const band = incidence < -0.15 ? 0.21 : incidence < 0.2 ? 0.37 : incidence < 0.63 ? 0.53 : 0.7;
      const local = (p[1] - cy) / ry;
      const rim = local < -0.34 ? 0.08 : 0;
      return value(tier >= 2 ? "tip" : "pine", band + 0.053 * tier + (hash(key) - 0.5) * 0.08 - rim);
    };
    const N = 40,
      M = 19;
    for (let j = 0; j < M; j++)
      for (let i = 0; i < N; i++) {
        const a = (Math.PI * 2 * i) / N,
          b = (Math.PI * 2 * (i + 1)) / N;
        const lo = (j / M - 0.5) * Math.PI,
          hi = ((j + 1) / M - 0.5) * Math.PI;
        s.quad(point(b, lo), point(a, lo), point(a, hi), point(b, hi), color, 0.012);
      }
  }
  crown(x, 0.57 * height, z, width * 0.79, height * 0.29, width * 0.8, seed, 0);
  for (let tier = 0; tier < 3; tier++) {
    const count = [6, 5, 3][tier],
      base = [0.45, 0.65, 0.81][tier];
    for (let i = 0; i < count; i++) {
      const key = seed + 17 * tier + i;
      const a = (2 * Math.PI * (i + 0.33 * tier)) / count + 0.24 * hash(key);
      const reach = width * [0.48, 0.42, 0.24][tier];
      crown(
        x + reach * Math.cos(a),
        height * (base + 0.042 * hash(key + 20)),
        z + reach * Math.sin(a),
        width * (0.47 + 0.13 * hash(key)),
        height * (0.13 + 0.038 * hash(key + 4)),
        width * (0.46 + 0.13 * hash(key + 2)),
        key,
        tier,
      );
    }
  }
}

function wildflowers(s, cx, cz, seed, bloom = true) {
  for (let i = 0; i < 7; i++) {
    const a = hash(seed + i) * Math.PI * 2,
      step = hash(seed + 8 + i) * 0.12;
    const x = cx + Math.cos(a) * step,
      z = cz + Math.sin(a) * step;
    const tip = 0.075 + hash(seed + i + 20) * 0.064,
      dx = Math.cos(a) * 0.052,
      dz = Math.sin(a) * 0.052;
    const foot = [x, 0.012, z],
      mid = [x + dx * 0.49, tip * 0.57, z + dz * 0.49],
      top = [x + dx, tip, z + dz];
    const left = [mid[0] - dz * 0.2, mid[1], mid[2] + dx * 0.2],
      right = [mid[0] + dz * 0.2, mid[1], mid[2] - dx * 0.2];
    s.quad(foot, left, top, mid, () => [107, 138, 85], 0.008);
    s.quad(foot, mid, top, right, () => [66, 100, 75], 0.008);
    if (!bloom || i % 3 !== 0) continue;
    for (let j = 0; j < 5; j++) {
      const yaw = j * Math.PI * 0.4 + a,
        c = Math.cos(yaw),
        k = Math.sin(yaw);
      const petal = (r, w, h) => [top[0] + r * c - w * k, top[1] + h, top[2] + r * k + w * c];
      const pigment = i === 0 ? [197, 141, 153] : [224, 173, 175];
      s.quad(
        petal(0.002, 0, 0),
        petal(0.013, -0.009, 0.004),
        petal(0.024, 0, 0.006),
        petal(0.013, 0.009, 0.004),
        () => pigment,
        0.006,
      );
    }
    s.point([top[0], top[1] + 0.008, top[2]], [0, 1, 0], [241, 205, 122], 0.0065);
  }
}
export function makePaintedForge({ garden = true, spacing = 0.0075 } = {}) {
  const s = new PaintedSurface(spacing);
  forge(s);
  if (garden) {
    broadleaf(s, 0.575, -1.24, 1.59, 0.453, 32);
    pine(s, 0.077, -1.22, 1.04, 0.338, 91);
    wildflowers(s, -0.87, -0.34, 72);
    wildflowers(s, 0.6, -0.39, 109);
    wildflowers(s, -0.8, 0.29, 138);
    for (const [index, [x, z]] of [
      [-0.89, -0.63],
      [-0.98, -0.22],
      [-0.84, 0.04],
      [0.63, 0.29],
      [0.76, -0.27],
      [0.69, -0.76],
      [-0.61, -0.59],
      [0.48, -0.68],
      [-0.41, -0.96],
      [0.87, -0.6],
    ].entries())
      wildflowers(s, x, z, 183 + index * 9, false);
  }
  return s.finish(light);
}
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  if (!process.argv[2])
    throw Error(
      "Usage: node painted-forge.mjs output.ply [--forge-only] [--phase 0.75 | --cold] [--no-spill]",
    );
  const model = makePaintedForge({ garden: !process.argv.includes("--forge-only") });
  const arg = process.argv.indexOf("--phase");
  if (!process.argv.includes("--cold"))
    addFirePose(
      model,
      arg === -1 ? 0.75 : Number(process.argv[arg + 1]),
      !process.argv.includes("--no-spill"),
    );
  model.save(process.argv[2]);
  console.log(model.count + " painted surfels");
}
