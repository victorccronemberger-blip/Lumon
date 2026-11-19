// Baked lighting for scenes.
//
// A scene does not move, so its light is worked out once, at build time, for
// every splat, and the splat stores the final palette colour. That makes it
// affordable to do what the per-frame shader cannot:
//
//   - sun shadows: march from the splat towards the sun through the scene's
//     signed distance field; a soft shadow from how close the ray passes
//   - lamp light with shadows: fires and lanterns light what they can see
//   - light through leaves: foliage is lit from both sides and only half
//     shadowed, so a canopy glows instead of turning into a dark ball
//   - colour ramps: the lit colour is matched to the palette in OKLab with a
//     penalty for leaving the surface's own hue, so a shaded orange becomes a
//     darker orange or red, never a green that happens to be close. Shadows
//     lean cool and lit sides warm, the way a pixel artist shifts hues.
//
// The per-frame shader then only adds a little live lamp flicker and fog.

import { hexToRgb } from "../noise.js";

// same direction as the viewer's key light (src/render/shade.js)
export const SUN = (() => { const v = [0.45, 0.8, 0.4], l = Math.hypot(...v); return v.map((x) => x / l); })();
export const LIGHT = { ambLow: 0.48, ambHigh: 0.62, sun: 0.7, lamp: 1.0 };

// ---- colour ------------------------------------------------------------------
const toLin = (c) => (c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4);
export function oklab([r, g, b]) {
  r = toLin(Math.max(0, r)); g = toLin(Math.max(0, g)); b = toLin(Math.max(0, b));
  const l = Math.cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
  const m = Math.cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
  const s = Math.cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
  return [0.2104542553 * l + 0.793617785 * m - 0.0040720468 * s, 1.9779984951 * l - 2.428592205 * m + 0.4505937099 * s, 0.0259040371 * l + 0.7827717662 * m - 0.808675766 * s];
}
// hue as a direction in the a/b plane, shrunk towards 0 for greys so that
// greys and slightly tinted darks count as the same family
const smooth = (a, b, x) => { const t = Math.max(0, Math.min(1, (x - a) / (b - a))); return t * t * (3 - 2 * t); };
const hueDir = (lab) => { const c = Math.hypot(lab[1], lab[2]), k = 1 / (c + 0.035); return [lab[1] * k, lab[2] * k]; };

// palette: hex list, the first entry is the background and is never picked
export function makeRamps(palette, { huePenalty = 0.12 } = {}) {
  const cols = palette.slice(1).map(hexToRgb), labs = cols.map(oklab), dirs = labs.map(hueDir);
  // target: the lit colour wanted · base: the surface's own colour · pull: 0–1, how much
  // a coloured lamp may drag it into another hue (1 = no hue penalty at all)
  return function pick(target, base, pull = 0) {
    // the hue only binds a colourful surface, and binds less in deep shadow:
    // white and grey shade into whatever cool grey fits, darks may go navy
    const t = oklab(target), bl = oklab(base), hb = hueDir(bl);
    const sat = smooth(0.025, 0.07, Math.hypot(bl[1], bl[2])), lit = smooth(0.06, 0.3, t[0]);
    const pen = huePenalty * (1 - pull) * sat * lit;
    let best = 0, bd = Infinity;
    for (let i = 0; i < labs.length; i++) {
      const L = labs[i], dl = t[0] - L[0], da = t[1] - L[1], db = t[2] - L[2];
      const hx = dirs[i][0] - hb[0], hy = dirs[i][1] - hb[1];
      const c2 = L[1] * L[1] + L[2] * L[2]; // a grey surface stays out of strong colours
      const d = 1.4 * dl * dl + da * da + db * db + pen * (hx * hx + hy * hy) + 8 * (1 - sat) * c2;
      if (d < bd) { bd = d; best = i; }
    }
    return cols[best];
  };
}

// ---- light ---------------------------------------------------------------------
// sh: the scene's shape (sh.dist) · lights: [{ pos, color, power }]
export function makeLighting(sh, lights, palette, { quantize = true } = {}) {
  const pick = makeRamps(palette);

  // soft shadow: 1 = in full sun, 0 = fully shadowed
  function sunVis(x, y, z, nx, ny, nz, maxT = 4) {
    let t = 0.03, vis = 1;
    const ox = x + nx * 0.025, oy = y + ny * 0.025, oz = z + nz * 0.025;
    for (let i = 0; i < 48 && t < maxT; i++) {
      const h = sh.dist(ox + SUN[0] * t, oy + SUN[1] * t, oz + SUN[2] * t);
      if (h < 0.002) return 0;
      vis = Math.min(vis, (10 * h) / t);
      t += Math.min(Math.max(h, 0.012), 0.35);
    }
    return Math.max(0, Math.min(1, vis));
  }
  // can a lamp at l see this point? (hard-ish, with a little softness)
  function lampVis(x, y, z, nx, ny, nz, l, dist) {
    const dx = (l[0] - x) / dist, dy = (l[1] - y) / dist, dz = (l[2] - z) / dist;
    let t = 0.03, vis = 1;
    const ox = x + nx * 0.025, oy = y + ny * 0.025, oz = z + nz * 0.025;
    for (let i = 0; i < 32 && t < dist - 0.12; i++) {
      const h = sh.dist(ox + dx * t, oy + dy * t, oz + dz * t);
      if (h < 0.002) return 0.1;
      vis = Math.min(vis, (6 * h) / t);
      t += Math.max(h, 0.012);
    }
    return 0.1 + 0.9 * Math.max(0, Math.min(1, vis));
  }

  // base: the surface colour (0–1) · p, n: position and normal
  // o.leaf: foliage, lit through · o.ao: 0–1 occlusion from the sampler
  // o.up: a blade or flat litter, lit like the ground it stands on
  function shade(base, x, y, z, nx, ny, nz, o = {}) {
    const ao = o.ao ?? 1;
    let ndl = nx * SUN[0] + ny * SUN[1] + nz * SUN[2];
    if (o.leaf) ndl = 0.45 + 0.55 * Math.abs(ndl); // light comes through
    let sun = Math.max(0, ndl);
    if (sun > 0) {
      const v = sunVis(x, y, z, o.leaf ? SUN[0] : nx, o.leaf ? SUN[1] : ny, o.leaf ? SUN[2] : nz);
      sun *= o.leaf ? 0.35 + 0.65 * v : v;
    }
    const sky = LIGHT.ambLow + (LIGHT.ambHigh - LIGHT.ambLow) * (ny * 0.5 + 0.5);
    let k = sky * (0.55 + 0.45 * ao) + LIGHT.sun * sun;

    // lamps: warm light that can recolour what is right next to them
    let wr = 0, wg = 0, wb = 0;
    for (const l of lights) {
      const dx = l.pos[0] - x, dy = l.pos[1] - y, dz = l.pos[2] - z, d = Math.hypot(dx, dy, dz);
      if (d > 1.6 || d < 1e-4) continue;
      const facing = (nx * dx + ny * dy + nz * dz) / d;
      let w = ((Math.max(facing, 0) * 0.85 + 0.15) / (1 + d * d * 9)) * l.power * LIGHT.lamp;
      if (w < 0.01) continue;
      if (facing > -0.1) w *= lampVis(x, y, z, nx, ny, nz, l.pos, d);
      else w *= 0.3;
      wr += l.color[0] * w; wg += l.color[1] * w; wb += l.color[2] * w;
    }

    // cool shadows, warm light: shift the hue with how lit the surface is
    const lit = Math.min(1, sun / 0.5);
    const tint = [0.86 + 0.18 * lit, 0.92 + 0.08 * lit, 1.1 - 0.17 * lit];
    const target = [0, 1, 2].map((i) => base[i] * k * tint[i] + [wr, wg, wb][i] * (base[i] * 0.85 + 0.1));
    if (!quantize) return target.map((value) => Math.min(1, Math.max(0, value)));
    const lampLum = 0.3 * wr + 0.59 * wg + 0.11 * wb;
    const pull = Math.min(1, Math.max(0, (lampLum - 0.12) / 0.5));
    return pick(target, base, pull);
  }
  return { shade, sunVis, pick };
}
