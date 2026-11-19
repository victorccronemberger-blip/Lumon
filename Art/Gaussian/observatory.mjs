import { box, cone, cyl, ellipsoid, shape } from "../../Tools/lumon-splat-authoring/src/authoring/sdf.js";
import { makeCtx, mix, rgb, sstep, TAU } from "../../Tools/lumon-splat-authoring/src/authoring/island.js";
import { makeFree, norm } from "../../Tools/lumon-splat-authoring/src/authoring/scene-kit.js";
import { mulberry32, vnoise3 } from "../../Tools/lumon-splat-authoring/src/noise.js";
import { eggShell } from "./egg-shell.mjs";
import { bakeDirectionalShadow } from "./directional-shadow.mjs";
import { forgeAngle, hearthX } from "./forge-fire.mjs";

export const SKY = "#dfbca1";

const materials = {
  grass: ["#384f65", "#699075", "#b1bf72"],
  turfEdge: ["#39485d", "#788668", "#b2aa73"],
  cliff: ["#374868", "#847c83", "#ccccb1"],
  basalt: ["#415577", "#848991", "#c6bba4"],
  weatheredLedge: ["#495378", "#998778", "#e5cbaa"],
  rock: ["#505c77", "#958991", "#d5c6ab"],
  paving: ["#626d87", "#cbb79b", "#f8e0b0"],
  pathStone: ["#6b7894", "#c6b299", "#f7ddb0"],
  plazaStone: ["#6d7491", "#d4b99b", "#ffe5b4"],
  pathBorder: ["#3a5b70", "#718a88", "#aeb59c"],
  surveyInlay: ["#846342", "#c79756", "#ffdc88"],
  mortar: ["#3a4349", "#747975", "#a7a58e"],
  slate: ["#153f57", "#368281", "#92c3ac"],
  ridge: ["#21485e", "#517977", "#a9ac8e"],
  iron: ["#252d38", "#3d4751", "#656a69"],
  wood: ["#40313d", "#855345", "#bd855a"],
  panel: ["#493d40", "#8b7059", "#bc9f7a"],
  benchWood: ["#3a3335", "#775841", "#a88a61"],
  benchStone: ["#303846", "#5b6670", "#92958a"],
  surveyStone: ["#333b49", "#616972", "#92958a"],
  bronze: ["#64503e", "#b48442", "#f2c574"],
  armillaryFace: ["#49352f", "#846044", "#b58658"],
  armillaryRim: ["#624d3a", "#a48253", "#d1ad72"],
  armillaryEnamel: ["#1a3441", "#294d58", "#527d80"],
  forgeSteel: ["#1e2936", "#526675", "#a0b2b3"],
  fusionSteel: ["#202b38", "#3d515f", "#789299"],
  fusionViolet: ["#322c4c", "#695d86", "#b599bd"],
  fusionCore: ["#25646f", "#52a1a3", "#b3e9d5"],
  fusionTrace: ["#4b938c", "#69b4a5", "#9bd2b8"],
  nestGlaze: ["#243842", "#3c686d", "#789497"],
  nestShell: ["#8c735b", "#c1a986", "#f0dfb6"],
  nestSpots: ["#755150", "#9c6c5a", "#bf9873"],
  nestHeat: ["#9a5949", "#c88457", "#efba76"],
  marketRareShell: ["#332b49", "#625779", "#a19ab6"],
  firebrick: ["#302730", "#66403b", "#9c5e46"],
  coal: ["#593031", "#9d4735", "#d46f40"],
  ember: ["#bd4a35", "#ed783f", "#ffcf78"],
  cloth: ["#613f55", "#b4554d", "#e98d64"],
  canvas: ["#6e5862", "#bc9c73", "#f0d9a1"],
  teal: ["#265165", "#3a8986", "#95d5b5"],
  purple: ["#36364d", "#6b5a7c", "#ada2ba"],
  glass: ["#ac704d", "#e2a76d", "#ffdfa0"],
  conifer: ["#254956", "#53836b", "#9cab67"],
  coniferTip: ["#33566a", "#608b74", "#aebd71"],
  flare: ["#ffd495", "#ffd495", "#ffe7b0"],
  crystal: ["#345b65", "#5da6ab", "#b6e5d5"],
};
const ramps = Object.fromEntries(Object.entries(materials).map(([name, colors]) => [name, colors.map(rgb)]));
const keyLight = norm([-0.65, 0.77, -0.52]);
const skyFill = [0.69, 0.76, 0.94];
const sunlight = [0.49, 0.35, 0.13];
const warmLight = [0.91, 0.54, 0.21];
const marketTurn = 1.94;
const armillaryCenter = [0.55, -0.55];
const rimFurniture = [
  { id: "bench-west", kind: "bench", x: -7.35, z: -1.2 },
  { id: "bench-north", kind: "bench", x: -3.3, z: -6.8 },
  { id: "bench-east", kind: "bench", x: 7.4, z: -1.3 },
  { id: "bench-southeast", kind: "bench", x: 3.25, z: 6.7 },
  { id: "survey-north", kind: "survey", x: 1.55, z: -7.8 },
  { id: "survey-south", kind: "survey", x: -3.5, z: 6.9 },
].map(({ id, kind, x, z }) => {
  const turn = Math.atan2(-x, -z);
  return { id, kind, x, z, turn, axis: [Math.cos(turn), 0, -Math.sin(turn)],
    halfSpan: kind === "bench" ? 0.45 : 0, radius: 0.29 };
});
const walkwayLights = [
  [-2.45, -0.03], [2.65, 0.25], [-6.35, 1.15], [1.65, 4.85], [-0.03, -4.9], [-4.75, -3.5], [3.55, -5.75], [-3.0, 3.8],
].map(([x, z], index) => ({ id: `lantern-${index + 1}`, kind: "lantern", x, z, radius: 0.195 }));
export const observatoryFurniture = [...rimFurniture, ...walkwayLights];
const treeGroups = [
  [-6.75,-3.55,1.95,0.7,0.31,-0.19], [-7.22,-3.05,1.24,1.42,0.28,-0.2],
  [6.88,2.91,1.59,2.38,0.275,0.24,"forge"], [7.03,2.44,1.04,0.63,0.312,0.10,"forge"],
  [-5.10,5.68,1.74,-0.36,0.30,-0.24], [-5.82,5.23,1.25,1.05,0.27,0.12],
  [-4.49,6.25,1.13,1.82,0.26,0.21], [2.64,-6.95,1.62,1.82,0.28,0.21],
  [3.18,-7.16,1.15,0.85,0.30,-0.12], [5.1,5.88,1.85,1.22,0.36,0.08],
  [5.68,5.42,1.33,2.15,0.31,-0.1], [-7.65,0.46,1.40,0.48,0.28,0.1]
];
export const telescopes = [[-4.95,-5.3,-0.9], [6.9,-3.05,1.65]];
// Painted architecture and planting are exported separately for finer surface spacing.
export const domeSite = [2.7, 0, 6.55];
export const flowerTrees = [[-4.95,0.22,-0.63,1.08], [4.85,4.65,0.84,1], [-2.95,-6.9,2.2,0.84]];
export const observatoryObstacles = [
  {id:"crescent-pavilion", position:domeSite, radius:1.17},
  ...flowerTrees.map(([x,z],i) => ({id:`coral-tree-${i}`,position:[x,0,z],radius:0.33})),
  { id: "armillary", position: [armillaryCenter[0], 0, armillaryCenter[1]], radius: 0.79 },
  ...treeGroups.map(([x,z],i) => ({ id: `pine-${i}`, position:[x,0,z], radius:0.24 })),
  ...telescopes.map(([x,z],i) => ({ id: `telescope-${i}`, position:[x,0,z], radius:0.66 })),
  ...observatoryFurniture.map(({ id, x, z, axis, halfSpan, radius }) => ({ id, position: [x, 0, z], axis, halfSpan, radius })),
];
const armillaryHeight = 1.55;
const meridian = ringFrame([0.54, -0.14, 0.83]);
const innerMeridian = ringFrame([-0.66, 0.31, 0.67]);
const horizon = ringFrame([0.30, 0.92, -0.23]);
const armillaryBands = {
  meridian: { frame: meridian, radius: 0.680, width: 0.063, depth: 0.045, face: "armillaryFace" },
  innerMeridian: { frame: innerMeridian, radius: 0.523, width: 0.043, depth: 0.033, face: "armillaryEnamel" },
  horizon: { frame: horizon, radius: 0.407, width: 0.034, depth: 0.027, face: "armillaryFace" },
};
export const armillaryMotion = {
  pivot: [armillaryCenter[0], armillaryHeight, armillaryCenter[1]],
  middleAxis: norm(cross(meridian.n, innerMeridian.n)),
  horizonAxis: norm(cross(innerMeridian.n, horizon.n)),
  middleSweep: 37,
  horizonSweep: 24,
  period: 18,
};
export const armillaryBindings = {
  innerMeridian: 1, movingPin: 1, movingPivot: 1,
  horizon: 2, crystalSpindle: 2, armillaryCrystal: 2,
};
const movingMaterials = {
  movingPin: "armillaryFace", movingPivot: "armillaryRim",
  crystalSpindle: "iron", armillaryCrystal: "crystal",
};
const atlasAngle = 53 * Math.PI / 180;
const atlasRise = Math.sin(atlasAngle), atlasRun = Math.cos(atlasAngle);
const atlasHeight = 0.985, atlasDepth = 0.035;
const chartCoasts = [
  [[-0.53, -0.05], [-0.42, -0.25], [-0.18, -0.31], [-0.04, -0.16], [0.12, -0.13], [0.18, 0.01],
    [0.04, 0.07], [-0.05, 0.20], [-0.18, 0.18], [-0.26, 0.32], [-0.40, 0.33], [-0.53, 0.21]],
  [[0.25, 0.13], [0.24, -0.05], [0.34, -0.21], [0.49, -0.18], [0.54, -0.02], [0.41, 0.08], [0.39, 0.23]],
];
const chartRoute = [[-0.36, -0.17], [-0.21, -0.105], [-0.24, 0.075], [-0.13, 0.16]];
const chartInk = Object.fromEntries(Object.entries({
  sea: "#2f5460", shoal: "#608787", shore: "#b1b797", land: "#849474", ridge: "#677b65",
  grid: "#adc0af", route: "#ebbd72", waypoint: "#f2d49d", enamel: "#31535b", plate: "#607878", stamp: "#b49a69",
}).map(([name, color]) => [name, rgb(color)]));

function beam(s, x, y, z, hx, hy, hz, material, turn = 0, tilt = 0) {
  const local = box(0, 0, 0, hx, hy, hz, 0.013);
  const [c, k, a, b] = [Math.cos(turn), Math.sin(turn), Math.cos(tilt), Math.sin(tilt)];
  s.add((px, py, pz) => {
    const dx = px - x, dy = py - y, dz = pz - z;
    const u = dx * c - dz * k, v = dx * k + dz * c;
    return local(u * a + dy * b, dy * a - u * b, v);
  }, material);
}

function beamBetween(s, start, end, width, depth, material) {
  const [x, y, z] = start, [xx, yy, zz] = end;
  const dx = xx - x, dy = yy - y, dz = zz - z;
  beam(s, (x + xx) / 2, (y + yy) / 2, (z + zz) / 2, Math.hypot(dx, dy, dz) / 2 + width,
    width, depth, material, -Math.atan2(dz, dx), Math.atan2(dy, Math.hypot(dx, dz)));
}

function orbitRing(s, x, y, z, radius, thickness, normal, material) {
  const [nx, ny, nz] = norm(normal);
  s.add((px, py, pz) => {
    const u = px - x, v = py - y, w = pz - z, axis = u * nx + v * ny + w * nz;
    const flat = Math.sqrt(Math.max(0, u * u + v * v + w * w - axis * axis));
    return Math.hypot(flat - radius, axis) - thickness;
  }, material);
}

function cross(a, b) {
  return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
}

function ringFrame(normal) {
  const n = norm(normal);
  const u = norm([n[2], 0, -n[0]]);
  return { n, u, v: cross(n, u) };
}

function ringBand(s, x, y, z, { frame, radius, width, depth }, material) {
  const [nx, ny, nz] = frame.n;
  s.add((px, py, pz) => {
    const u = px - x, v = py - y, w = pz - z;
    const axial = u * nx + v * ny + w * nz;
    const radial = Math.sqrt(Math.max(0, u * u + v * v + w * w - axial * axial));
    const rim = Math.abs(radial - radius) - (width - 0.007);
    const face = Math.abs(axial) - (depth - 0.007);
    return Math.hypot(Math.max(0, rim), Math.max(0, face)) + Math.min(0, Math.max(rim, face)) - 0.007;
  }, material);
}

function octagonalStep(s, x, y, z, radius, halfHeight, bevel, material) {
  s.add((px, py, pz) => {
    const u = Math.abs(px - x), v = Math.abs(pz - z), height = Math.abs(py - y);
    const edge = Math.max(u, v, (u + v) * Math.SQRT1_2);
    const inset = Math.max(0, height - (halfHeight - bevel));
    return Math.max(edge + inset - radius, height - halfHeight);
  }, material);
}

function hingeRings(s, center, outer, inner, outerRadius, innerRadius, pin = "armillaryFace", cap = "armillaryRim") {
  const axis = norm(cross(outer.n, inner.n));
  for (const side of [-1, 1]) {
    const start = center.map((value, i) => value + axis[i] * side * innerRadius);
    const end = center.map((value, i) => value + axis[i] * side * outerRadius);
    beamBetween(s, start, end, 0.021, 0.026, pin);
    s.add(ellipsoid(...end, 0.034, 0.034, 0.034), cap);
  }
}

function crystal(s, x, y, z, width, height, lean, material) {
  s.add((px, py, pz) => {
    const dy = py - y, u = px - x - dy * lean, v = pz - z;
    const radius = Math.max(Math.abs(u), Math.abs(v) * 0.86 + Math.abs(u) * 0.5);
    const shoulder = width - Math.max(0, dy - height * 0.18) * 0.72;
    return Math.max(radius - shoulder, Math.abs(dy) - height * 0.5);
  }, material);
}

function base(s, x, z, width, depth) {
  beam(s, x, 0.055, z, width, 0.075, depth, "rock");
  beam(s, x, 0.14, z, width - 0.045, 0.045, depth - 0.045, "paving");
}

function lantern(s, x, y, z, lights) {
  beam(s, x, y, z, 0.073, 0.091, 0.073, "glass");
  for (const dx of [-0.064, 0.064]) for (const dz of [-0.064, 0.064]) {
    beam(s, x + dx, y, z + dz, 0.013, 0.11, 0.013, "iron");
  }
  beam(s, x, y - 0.1, z, 0.1, 0.022, 0.1, "bronze");
  beam(s, x, y + 0.103, z, 0.095, 0.027, 0.095, "iron");
  beam(s, x, y + 0.145, z, 0.057, 0.027, 0.057, "bronze");
  lights.push({ x, y, z, power: 0.27 });
}

function parkBench(s, x, z, turn) {
  const cosine = Math.cos(turn), sine = Math.sin(turn);
  const slat = (u, y, v, hx, hy, hz, material) =>
    beam(s, x + u * cosine + v * sine, y, z - u * sine + v * cosine, hx, hy, hz, material, turn);
  s.bound(x, 0.34, z, 0.83);
  for (const side of [-1, 1]) {
    slat(side * 0.43, 0.048, 0, 0.092, 0.058, 0.166, "benchStone");
    slat(side * 0.43, 0.167, -0.018, 0.064, 0.103, 0.091, "iron");
    slat(side * 0.43, 0.273, 0, 0.075, 0.025, 0.176, "iron");
    slat(side * 0.43, 0.438, -0.134, 0.033, 0.179, 0.044, "iron");
    slat(side * 0.43, 0.385, 0.053, 0.031, 0.065, 0.116, "iron");
    slat(side * 0.43, 0.457, 0.034, 0.046, 0.034, 0.126, "benchWood");
  }
  for (const v of [-0.098, 0.098]) slat(0, 0.323, v, 0.61, 0.045, 0.080, "benchWood");
  for (const y of [0.496, 0.636]) slat(0, y, -0.128, 0.60, 0.054, 0.047, "benchWood");
  s.bound();
}

function surveyPost(s, x, z, turn, accent) {
  s.bound(x, 0.35, z, 0.48);
  octagonalStep(s, x, 0.052, z, 0.194, 0.069, 0.025, "basalt");
  s.add(cyl(x, 0.262, z, 0.125, 0.216, 0.021), "surveyStone");
  s.add(cyl(x, 0.413, z, 0.148, 0.049, 0.012), "benchStone");
  s.add(cyl(x, 0.461, z, 0.139, 0.030, 0.014), "bronze");
  const du = Math.cos(turn) * 0.055, dv = -Math.sin(turn) * 0.055;
  beam(s, x + du, 0.497, z + dv, 0.048, 0.022, 0.075, accent, turn);
  beam(s, x - du, 0.497, z - dv, 0.048, 0.022, 0.075, "armillaryEnamel", turn);
  s.bound();
}

function pathLantern(s, x, z, lights) {
  s.bound(x, 0.36, z, 0.48);
  octagonalStep(s, x, 0.042, z, 0.116, 0.052, 0.019, "benchStone");
  s.add(cyl(x, 0.202, z, 0.034, 0.143, 0.009), "iron");
  s.add(cyl(x, 0.135, z, 0.055, 0.035, 0.011), "bronze");
  s.add(cyl(x, 0.351, z, 0.090, 0.034, 0.012), "iron");
  s.add(cyl(x, 0.437, z, 0.067, 0.076, 0.008), "glass");
  for (let i = 0; i < 4; i++) {
    const a = (i + 0.5) * TAU / 4;
    const px = x + Math.cos(a) * 0.069, pz = z + Math.sin(a) * 0.069;
    s.add(cyl(px, 0.442, pz, 0.019, 0.091, 0.006), "bronze");
  }
  s.add(cyl(x, 0.523, z, 0.095, 0.036, 0.014), "armillaryEnamel");
  s.add((px, py, pz) =>
    Math.max(Math.hypot(px - x, pz - z) + (py - 0.536) * 1.15 - 0.094, 0.535 - py), "armillaryEnamel");
  s.add(ellipsoid(x, 0.624, z, 0.029, 0.033, 0.029), "bronze");
  lights.push({ x, y: 0.434, z, power: 0.26 });
  s.bound();
}

export function addObservatoryFurniture(s, { id, kind, x, z, turn }, lights) {
  if (kind === "lantern") pathLantern(s, x, z, lights);
  else if (kind === "bench") parkBench(s, x, z, turn);
  else surveyPost(s, x, z, turn, id === "survey-north" ? "teal" : "bronze");
}

function lodge(s, x, z, lights) {
  s.bound(x, 0.94, z, 1.34);
  octagonalStep(s, x, 0.052, z, 0.720, 0.072, 0.027, "rock");
  octagonalStep(s, x, 0.138, z, 0.674, 0.041, 0.021, "paving");
  beam(s, x, 0.236, z, 0.623, 0.079, 0.473, "mortar");
  s.add((px, py, pz) => Math.max(Math.abs(px - x) - 0.577, Math.abs(pz - z) - 0.426,
    0.29 - py, py - 1.677 + Math.abs(px - x) * 1.04), "panel");

  for (const end of [-1, 1]) {
    const faceZ = z + end * 0.439;
    for (const side of [-1, 1]) {
      beam(s, x + side * 0.552, 0.637, faceZ, 0.053, 0.329, 0.036, "wood");
      beamBetween(s, [x + side * 0.625, 1.005, faceZ], [x, 1.692, faceZ], 0.036, 0.044, "wood");
      beamBetween(s, [x + side * 0.51, 0.34, faceZ], [x + side * 0.26, 0.70, faceZ], 0.030, 0.037, "wood");
    }
    beam(s, x, 0.987, faceZ, 0.582, 0.044, 0.044, "wood");
    const badgeZ = z + end * 0.466;
    s.add((px, py, pz) => {
      const point = Math.max(0, 1.20 - py);
      return Math.max(Math.abs(px - x) + point * 0.98 - 0.153,
        Math.abs(py - 1.253) - 0.167, Math.abs(pz - badgeZ) - 0.045);
    }, "armillaryEnamel");
    const crestZ = badgeZ + end * 0.044;
    for (const level of [1.22, 1.32]) for (const side of [-1, 1])
      beamBetween(s, [x + side * 0.089, level - 0.046, crestZ], [x, level + 0.014, crestZ], 0.019, 0.018, "armillaryRim");
  }
  for (const side of [-1, 1]) {
    // The tile courses shorten at the eaves so the roof follows the cut corners.
    s.add((px, py, pz) => {
      const dx = (px - x) * side;
      const depth = 0.521 - 0.115 * sstep(0.49, 0.68, dx);
      return Math.max(-dx, dx - 0.691, Math.abs(pz - z) - depth, Math.abs(py - (1.688 - dx * 1.04)) - 0.056);
    }, "ridge");
    for (let row = 0; row < 4; row++) {
      const dx = 0.088 + 0.166 * row, height = 1.728 - dx * 1.04;
      const courseDepth = row === 3 ? 0.400 : 0.515;
      const halfTile = courseDepth / 4;
      for (let col = 0; col < 4; col++) {
        beam(s, x + side * dx, height, z + (col * 2 - 3) * halfTile,
          0.124, 0.042, halfTile - 0.005, (row + col + (side > 0 ? 1 : 0)) % 5 === 2 ? "ridge" : "slate", 0, -side * 0.805);
      }
    }
    const wallX = x + side * 0.591;
    beam(s, wallX, 0.669, z, 0.039, 0.173, 0.232, "wood");
    beam(s, wallX + side * 0.034, 0.682, z, 0.022, 0.107, 0.176, "glass");
    beam(s, wallX + side * 0.054, 0.678, z, 0.016, 0.114, 0.022, "wood");
    beam(s, wallX + side * 0.039, 0.545, z, 0.032, 0.032, 0.239, "mortar");
  }
  beam(s, x, 1.740, z, 0.068, 0.035, 0.548, "armillaryRim");
  beam(s, x + 0.305, 1.570, z - 0.195, 0.096, 0.219, 0.093, "mortar");
  beam(s, x + 0.305, 1.799, z - 0.195, 0.127, 0.043, 0.121, "rock");

  const door = (faceZ, width, bottom, spring, thickness, material) => {
    s.add((px, py, pz) => Math.max(bottom - py, Math.abs(pz - faceZ) - thickness,
      Math.hypot(px - x, Math.max(0, py - spring)) - width), material);
  };
  door(z + 0.447, 0.241, 0.179, 0.624, 0.047, "wood");
  door(z + 0.490, 0.171, 0.199, 0.602, 0.026, "teal");
  beam(s, x, 0.168, z + 0.514, 0.254, 0.041, 0.086, "paving");
  beam(s, x + 0.071, 0.523, z + 0.524, 0.026, 0.027, 0.022, "bronze");
  beam(s, x, 0.617, z - 0.453, 0.226, 0.147, 0.040, "wood");
  beam(s, x, 0.626, z - 0.487, 0.163, 0.099, 0.024, "glass");
  beam(s, x, 0.624, z - 0.515, 0.017, 0.107, 0.014, "wood");
  beam(s, x + 0.353, 0.814, z + 0.455, 0.053, 0.037, 0.113, "wood");
  lantern(s, x + 0.353, 0.676, z + 0.531, lights);
  // A pitched porch shelters the team entrance. The brackets carry the projecting eave.
  for (const side of [-1,1]) {
    beam(s,x+side*.152,.932,z+.523,.19,.031,.155,"slate",0,-side*.37);
    beamBetween(s,[x+side*.248,.724,z+.468],[x+side*.248,.878,z+.642],.025,.029,"wood");
    beam(s,x+side*.255,.875,z+.65,.061,.034,.035,"bronze");
  }
  beam(s,x,1.002,z+.535,.034,.029,.162,"bronze");
  s.bound();
}

function market(volume, x, z) {
  const s = orientedShape(volume, x, z, Math.cos(marketTurn), Math.sin(marketTurn));
  volume.bound(x, 0.78, z, 1.18);
  octagonalStep(s, 0, 0.055, 0, 0.698, 0.075, 0.029, "rock");
  octagonalStep(s, 0, 0.142, 0, 0.651, 0.044, 0.027, "paving");
  beam(s, 0, 0.407, -0.031, 0.540, 0.224, 0.308, "wood");
  beam(s, 0, 0.647, 0.028, 0.612, 0.052, 0.381, "panel");
  beam(s, 0, 0.372, 0.284, 0.456, 0.129, 0.040, "armillaryEnamel");
  beam(s, 0, 0.228, 0.306, 0.511, 0.039, 0.027, "bronze");

  for (const side of [-1, 1]) {
    const railX = side * 0.536;
    beam(s, railX, 0.797, -0.338, 0.054, 0.603, 0.065, "wood");
    beam(s, railX, 0.244, -0.327, 0.081, 0.068, 0.078, "bronze");
    beamBetween(s, [railX, 1.372, -0.466], [railX, 1.550, -0.049], 0.040, 0.056, "wood");
    beamBetween(s, [railX, 1.202, -0.345], [railX, 1.517, -0.089], 0.024, 0.044, "bronze");
    beamBetween(s, [side * 0.453, 0.251, 0.337], [side * 0.083, 0.455, 0.337], 0.025, 0.027, "armillaryRim");
    beam(s, side * 0.526, 0.447, 0.288, 0.056, 0.197, 0.060, "wood");
  }
  beam(s, 0, 1.550, -0.046, 0.643, 0.027, 0.030, "armillaryRim");
  beam(s, 0, 1.376, -0.465, 0.644, 0.028, 0.031, "wood");
  for (let i = -2; i <= 2; i++) {
    const stripeX = i * 0.243, material = i % 2 ? "canvas" : "cloth";
    s.add((px, py, pz) => {
      const u = px - stripeX, v = pz + 0.465;
      const sag = 0.020 * (1 - Math.min(1, (u / 0.122) ** 2));
      return Math.max(Math.abs(u) - 0.122, -v, v - 0.432, Math.abs(py - (1.402 + v * 0.413 - sag)) - 0.027);
    }, material);
    beam(s, stripeX, 1.521 - 0.012 * (i % 2 === 0 ? 1 : 0), -0.042, 0.120, 0.059, 0.023, material);
  }
  for (const side of [-1, 1]) {
    const productX = side * 0.237, productY = side < 0 ? 0.935 : 0.972;
    s.add(cyl(productX, 0.708, 0.221, 0.166, 0.028, 0.012), "armillaryEnamel");
    s.add(cyl(productX, productY - 0.195, 0.221, 0.141, side < 0 ? 0.025 : 0.051, 0.018), "armillaryRim");
    s.add(eggShell(productX, productY, 0.221, 0.135, 0.186), side < 0 ? "nestShell" : "marketRareShell");
  }
  // A suspended pan identifies the shop from its side and rear approach.
  beam(s,-.47,1.48,-.047,.181,.025,.026,"bronze");
  for(const sign of [-1,1])
    s.add(cone([-.636,1.46,-.047],[-.636,1.164,-.047+sign*.102],.013,.012),"bronze");
  s.add(cyl(-.636,1.153,-.047,.121,.024,.017),"bronze");
  for(let i=-2;i<=2;i++) {
    const dx=i*.243, material=i%2?"canvas":"cloth";
    s.add((px,py,pz)=>Math.max(Math.hypot((px-dx)*.84,py-1.495)-.100,py-1.495,Math.abs(pz+.039)-.022),material);
  }
  volume.bound();
}

function nest(s, x, z, lights) {
  s.bound(x, 0.55, z, 0.99);
  octagonalStep(s, x, 0.055, z, 0.592, 0.072, 0.027, "rock");
  octagonalStep(s, x, 0.145, z, 0.551, 0.045, 0.022, "paving");
  octagonalStep(s, x, 0.250, z, 0.465, 0.076, 0.022, "nestGlaze");
  s.add(cyl(x, 0.323, z, 0.409, 0.051, 0.027), "iron");
  s.add(cyl(x, 0.375, z, 0.406, 0.026, 0.014), "armillaryRim");

  // The recess leaves a solid floor under the shell and a lip around the warming element.
  const bowl = cyl(x, 0.443, z, 0.394, 0.125, 0.038);
  const pocket = cyl(x, 0.600, z, 0.304, 0.145, 0.028);
  s.add((px, py, pz) => Math.max(bowl(px, py, pz), -pocket(px, py, pz)), "nestGlaze");
  s.add(cyl(x, 0.451, z, 0.148, 0.035, 0.016), "iron");
  orbitRing(s, x, 0.552, z, 0.342, 0.025, [0, 1, 0], "armillaryRim");
  orbitRing(s, x, 0.514, z, 0.300, 0.018, [0, 1, 0], "nestHeat");
  s.add(eggShell(x, 0.802, z, 0.274, 0.362), "nestShell");

  for (let i = 0; i < 3; i++) {
    const angle = TAU * (i / 3 + 1 / 12);
    const at = (radius, y) => [x + Math.cos(angle) * radius, y, z + Math.sin(angle) * radius];
    const foot = at(0.425, 0.328), elbow = at(0.450, 0.507), tip = at(0.265, 0.655);
    beamBetween(s, foot, elbow, 0.047, 0.048, "nestGlaze");
    beamBetween(s, elbow, tip, 0.036, 0.041, "armillaryRim");
    beam(s, ...elbow, 0.054, 0.038, 0.052, "bronze", -angle);
    beam(s, ...tip, 0.046, 0.035, 0.041, "nestGlaze", -angle);
  }

  // The recessed status plate faces the path to the station.
  beam(s, x, 0.271, z + 0.458, 0.153, 0.058, 0.049, "iron");
  beam(s, x, 0.271, z + 0.503, 0.106, 0.034, 0.021, "bronze");
  for (const dx of [-0.052, 0.052]) beam(s, x + dx, 0.271, z + 0.526, 0.028, 0.016, 0.011, "nestHeat");
  // Enamel thermometer mounted on the outer casing, with a brass bezel and a red needle.
  const dial=[x+.29,.405,z+.335];
  const disc=(r,depth,offset,material)=>s.add((px,py,pz)=>Math.max(Math.hypot(px-dial[0],py-dial[1])-r,Math.abs(pz-dial[2]-offset)-depth),material);
  disc(.125,.039,0,"bronze");disc(.098,.014,.04,"canvas");
  beamBetween(s,[dial[0]-.049,.355,dial[2]+.06],[dial[0]+.050,.455,dial[2]+.06],.013,.015,"cloth");
  lights.push({ x, y: 0.544, z, power: 0.27, tint: [0.71, 0.38, 0.17], halo: false });
  s.bound();
}

function orientedShape(volume, x, z, cosine, sine) {
  return { add(shape, material) {
    volume.add((px, py, pz) => shape((px - x) * cosine - (pz - z) * sine, py, (px - x) * sine + (pz - z) * cosine), material);
  } };
}

function cutGem(s, x, y, z, radius, height, lean, material) {
  s.add((px, py, pz) => {
    const dy = py - y;
    const u = Math.abs(px - x - dy * lean), v = Math.abs(pz - z);
    const sides = Math.max(v, 0.866 * u + 0.5 * v);
    const taper = Math.max(0, dy / height - 0.25) * radius * 3.76 +
      Math.max(0, -dy / height - 0.36) * radius * 2.6;
    return Math.max(sides + taper - radius, Math.abs(dy) - height * 0.5);
  }, material);
}

function fusion(volume, x, z, lights) {
  const angle = -1.92, c = Math.cos(angle), k = Math.sin(angle);
  const s = orientedShape(volume, x, z, c, k);
  volume.bound(x, 0.68, z, 1.08);
  octagonalStep(s, 0, 0.057, 0, 0.578, 0.072, 0.028, "rock");
  octagonalStep(s, 0, 0.148, 0, 0.564, 0.044, 0.019, "paving");
  octagonalStep(s, 0, 0.245, 0, 0.506, 0.066, 0.027, "fusionSteel");
  octagonalStep(s, 0, 0.351, -0.082, 0.243, 0.078, 0.029, "iron");
  octagonalStep(s, 0, 0.445, -0.082, 0.212, 0.040, 0.015, "bronze");
  s.add(cyl(0, 0.509, -0.082, 0.169, 0.044, 0.020), "fusionSteel");

  for (const side of [-1, 1]) {
    const feed = side * 0.398;
    s.add(cyl(feed, 0.335, 0.217, 0.146, 0.074, 0.026), "iron");
    s.add(cyl(feed, 0.417, 0.217, 0.145, 0.028, 0.014), "bronze");
    cutGem(s, feed, 0.554, 0.217, 0.099, 0.278, -side * 0.23, "fusionViolet");

    // Split saddle and paired inlaid channels carry the two inputs into the central socket.
    for (const jaw of [-1, 1]) beam(s, feed + jaw * 0.109, 0.448, 0.217, 0.030, 0.055, 0.068, "fusionSteel");
    const start = [feed - side * 0.061, 0.377, 0.239];
    const elbow = [side * 0.236, 0.382, 0.164];
    const end = [side * 0.105, 0.494, 0.025];
    beamBetween(s, start, elbow, 0.061, 0.053, "armillaryRim");
    beamBetween(s, elbow, end, 0.062, 0.054, "armillaryRim");
    const inset = point => [point[0], point[1] + 0.049, point[2]];
    beamBetween(s, inset(start), inset(elbow), 0.017, 0.028, "fusionTrace");
    beamBetween(s, inset(elbow), inset(end), 0.017, 0.027, "fusionTrace");

    const foot = [side * 0.405, 0.289, -0.323];
    const knee = [side * 0.462, 0.629, -0.295];
    const shoulder = [side * 0.326, 1.009, -0.262];
    const bearing = [side * 0.265, 1.095, -0.128];
    beamBetween(s, foot, knee, 0.071, 0.060, "fusionSteel");
    beamBetween(s, knee, shoulder, 0.066, 0.056, "fusionSteel");
    beamBetween(s, shoulder, bearing, 0.059, 0.057, "fusionSteel");
    beamBetween(s, knee, shoulder, 0.024, 0.068, "armillaryRim");
    for(let j=1;j<=4;j++){
      const t=j/5;
      beam(s,knee[0]+(shoulder[0]-knee[0])*t,knee[1]+(shoulder[1]-knee[1])*t,
        knee[2]+(shoulder[2]-knee[2])*t,.084,.019,.077,"bronze",0,side*.30);
    }
    beam(s, ...bearing, 0.064, 0.071, 0.064, "bronze", 0, side * 0.28);
    beamBetween(s, bearing, [side * 0.139, 1.061, -0.113], 0.043, 0.049, "fusionSteel");
  }

  cutGem(s, 0, 0.970, -0.082, 0.166, 0.913, 0.045, "fusionCore");
  beam(s, 0, 0.258, 0.509, 0.129, 0.060, 0.059, "iron");
  for (const dx of [-0.061, 0.061]) beam(s, dx, 0.265, 0.562, 0.024, 0.022, 0.017, "fusionTrace");
  lights.push({ x: x - 0.08 * k, y: 0.80, z: z - 0.08 * c, power: 0.32, tint: [0.17, 0.52, 0.55], halo: false });
  volume.bound();
}

function forge(volume, x, z, lights) {
  // Turn the working face toward the playable path.
  const c = Math.cos(forgeAngle), k = Math.sin(forgeAngle);
  const s = orientedShape(volume, x, z, c, k);
  volume.bound(x, 0.82, z, 1.29);
  base(s, 0, 0, 0.63, 0.45);

  // Banked hearth, recessed hot face, projecting sill and arch stones.
  const hearth = hearthX;
  beam(s, hearth, 0.34, -0.12, 0.35, 0.18, 0.28, "rock");
  beam(s, hearth, 0.516, -0.064, 0.38, 0.052, 0.29, "mortar");
  beam(s, hearth, 0.759, -0.312, 0.33, 0.235, 0.084, "firebrick");
  for (const side of [-1, 1]) {
    beam(s, hearth + side * 0.293, 0.719, -0.097, 0.067, 0.17, 0.234, "firebrick");
    beam(s, hearth + side * 0.294, 0.685, 0.159, 0.062, 0.122, 0.067, "rock");
  }
  for (let i = 0; i < 7; i++) {
    const a = (i + 0.5) * Math.PI / 7;
    const dx = Math.cos(a), dy = Math.sin(a);
    beam(s, hearth + dx * 0.277, 0.789 + dy * 0.218, 0.143, 0.063, 0.070, 0.086,
      i === 3 ? "mortar" : "firebrick", 0, a + Math.PI / 2);
  }
  for (const [dx, dz, h, material] of [
    [-0.13, 0.04, 0.05, "coal"], [0.01, 0.06, 0.075, "ember"], [0.14, 0.045, 0.05, "coal"],
    [-0.08, -0.075, 0.115, "ember"], [0.08, -0.125, 0.13, "coal"],
  ]) beam(s, hearth + dx, 0.56 + h * 0.48, dz, 0.077, h, 0.063, material, dx * 4, dx * 1.8);

  // Tapered hood with folded seams, flue bands and a rain cap that leaves a vent gap.
  s.add((px, py, pz) => {
    const taper = Math.max(0, py - 1.04);
    return Math.max(Math.abs(px - hearth) - 0.366 + taper * 0.62,
      Math.abs(pz + 0.091) - 0.296 + taper * 0.51, Math.abs(py - 1.186) - 0.195);
  }, "iron");
  beam(s, hearth, 1.004, -0.091, 0.372, 0.033, 0.306, "forgeSteel");
  for (const side of [-1, 1]) {
    beamBetween(s, [hearth + side * 0.28, 1.031, 0.207], [hearth + side * 0.131, 1.363, 0.047], 0.016, 0.018, "forgeSteel");
    beam(s, hearth + side * 0.125, 1.642, -0.091, 0.023, 0.067, 0.119, "iron");
  }
  beam(s, hearth, 1.465, -0.091, 0.136, 0.128, 0.119, "ridge");
  beam(s, hearth, 1.405, -0.091, 0.161, 0.032, 0.145, "forgeSteel");
  beam(s, hearth, 1.709, -0.091, 0.183, 0.035, 0.163, "forgeSteel");

  // Anvil on a braced timber stand. The horn tapers independently of the steel table.
  const anvil = -0.31;
  beam(s, anvil, 0.344, 0.173, 0.142, 0.161, 0.152, "wood");
  beam(s, anvil, 0.473, 0.173, 0.151, 0.027, 0.16, "iron");
  beam(s, anvil, 0.531, 0.173, 0.122, 0.038, 0.114, "iron");
  beam(s, anvil, 0.613, 0.173, 0.079, 0.075, 0.08, "iron");
  beam(s, anvil, 0.7, 0.173, 0.161, 0.047, 0.102, "forgeSteel");
  s.add((px, py, pz) => {
    const u = px - anvil, cut = Math.max(0, -u - 0.12);
    return Math.max(Math.abs(u + 0.207) - 0.123, Math.abs(pz - 0.173) + cut * 0.395 - 0.094,
      py - 0.748 + cut * 0.18, 0.646 + cut * 0.36 - py);
  }, "forgeSteel");
  beamBetween(s, [anvil + 0.086, 0.748, 0.147], [anvil + 0.022, 0.767, 0.335], 0.021, 0.02, "wood");
  beam(s, anvil + 0.09, 0.788, 0.145, 0.062, 0.031, 0.038, "iron");
  lights.push({ x: x + hearth * c + 0.225 * k, y: 0.65, z: z - hearth * k + 0.225 * c, power: 0.43, halo: false });
  volume.bound();
}

function atlas(s, x, z) {
  s.bound(x, 0.67, z, 1.01);
  octagonalStep(s, x, 0.056, z, 0.684, 0.071, 0.024, "rock");
  octagonalStep(s, x, 0.149, z, 0.624, 0.043, 0.021, "paving");

  // The sloping work surface, folded rim, and hanging rolls share one joinery frame.
  const slab = (u, v, w, hx, hy, hz, material) => {
    const shape = box(u, v, w, hx, hy, hz, 0.009);
    s.add((px, py, pz) => {
      const dy = py - atlasHeight, dz = pz - z - atlasDepth;
      return shape(px - x, dy * atlasRise + dz * atlasRun, dy * atlasRun - dz * atlasRise);
    }, material);
  };
  slab(0, 0, 0, 0.600, 0.458, 0.046, "wood");
  slab(0, 0, 0.050, 0.514, 0.364, 0.018, "atlasChart");
  slab(0, 0, -0.047, 0.505, 0.353, 0.013, "atlasBack");
  for (const side of [-1, 1]) {
    slab(side * 0.55, 0, 0.047, 0.055, 0.45, 0.036, "panel");
    for (const end of [-1, 1]) slab(side * 0.545, end * 0.405, 0.066, 0.061, 0.052, 0.023, "bronze");
    const footX = x + side * 0.44;
    beamBetween(s, [footX, 0.21, z - 0.37], [footX, 0.75, z - 0.15], 0.066, 0.061, "wood");
    beamBetween(s, [footX, 0.21, z + 0.40], [footX, 1.131, z + 0.16], 0.071, 0.066, "wood");
    beamBetween(s, [footX, 0.42, z - 0.28], [footX, 0.59, z + 0.29], 0.038, 0.043, "panel");
    for (const shoeZ of [-0.37, 0.40]) beam(s, footX, 0.223, z + shoeZ, 0.082, 0.048, 0.081, "iron");
  }
  slab(0, -0.406, 0.062, 0.545, 0.049, 0.043, "panel");
  slab(0, 0.399, 0.036, 0.547, 0.050, 0.042, "panel");
  // Brass drafting dividers lie flat against the chart on the cartographer's side.
  const onChart=(u,v,w)=>[x+u,atlasHeight+v*atlasRise+w*atlasRun,z+atlasDepth+v*atlasRun-w*atlasRise];
  const hinge=onChart(.31,.17,.084);
  for(const u of [.20,.42]) s.add(cone(hinge,onChart(u,-.09,.084),.021,.009),"bronze");
  s.add(ellipsoid(...hinge,.037,.035,.035),"armillaryRim");
  beam(s, x, 0.448, z + 0.215, 0.491, 0.050, 0.201, "wood");
  const roll = (dx, y, dz, width, radius, material) => {
    const shape = cyl(0, 0, 0, radius, width, 0.008);
    s.add((px, py, pz) => shape(py - y, px - x - dx, pz - z - dz), material);
  };
  roll(-0.088, 0.552, 0.308, 0.338, 0.076, "canvas");
  roll(0.029, 0.543, 0.160, 0.312, 0.061, "canvas");
  for (const side of [-1, 1]) {
    roll(-0.088 + side * 0.228, 0.552, 0.308, 0.026, 0.079, "wood");
    roll(-0.088 + side * 0.347, 0.552, 0.308, 0.019, 0.036, "bronze");
  }
  s.bound();
}

function chartCoastDistance(x, z, points) {
  let inside = false, distance = Infinity;
  for (let i = 0; i < points.length; i++) {
    const a = points[i], b = points[(i + 1) % points.length];
    distance = Math.min(distance, segmentDistance(x, z, a, b));
    if ((a[1] > z) !== (b[1] > z) && x < a[0] + (b[0] - a[0]) * (z - a[1]) / (b[1] - a[1])) inside = !inside;
  }
  return inside ? -distance : distance;
}

function paintAtlas(x, y, z, nx, ny, nz, back) {
  const v = (y - atlasHeight) * atlasRise + (z - atlasDepth) * atlasRun;
  const w = (y - atlasHeight) * atlasRun - (z - atlasDepth) * atlasRise;
  const ink = chartInk;
  const facing = nx * keyLight[0] + ny * keyLight[1] + nz * keyLight[2];
  let color;
  if (back) {
    const angle = Math.atan2(v, x);
    const radius = Math.hypot(x, v);
    const ray = Math.max(0, Math.cos(4 * angle));
    const rose = 1 - sstep(0.002, 0.022, radius - (0.052 + 0.146 * ray ** 6));
    const rim = 1 - sstep(0.010, 0.025, Math.abs(radius - 0.23));
    color = mix(mix(ink.enamel, ink.plate, 0.16), ink.stamp, Math.max(rose, rim * 0.8));
    color = mix(ink.enamel, color, sstep(0.048, 0.058, -w));
  } else {
    const coast = Math.min(...chartCoasts.map(points => chartCoastDistance(x, v, points)));
    const edge = value => Math.abs(value - Math.round(value / 0.21) * 0.21);
    const grid = 1 - sstep(0.004, 0.014, Math.min(edge(x), edge(v)));
    const depth = Math.max(0, -coast);
    const elevation = sstep(0.046, 0.142, depth);
    const land = mix(ink.land, ink.ridge, elevation * 0.8);
    const shallows = mix(ink.sea, ink.shoal, 0.45 * (1 - sstep(0.0, 0.094, coast)));
    color = mix(shallows, land, 1 - sstep(-0.023, 0.010, coast));
    color = mix(color, ink.shore, (1 - sstep(0.004, 0.022, Math.abs(coast))) * 0.78);
    color = mix(color, ink.grid, grid * 0.18);
    let track = Infinity, point = Infinity;
    for (let i = 0; i < chartRoute.length; i++) {
      const pin = chartRoute[i];
      point = Math.min(point, Math.hypot(x - pin[0], v - pin[1]));
      if (i > 0) track = Math.min(track, segmentDistance(x, v, chartRoute[i - 1], pin));
    }
    color = mix(color, ink.sea, 0.66 * (1 - sstep(0.032, 0.044, Math.min(point, track + 0.012))));
    color = mix(color, ink.route, 1 - sstep(0.014, 0.027, track));
    color = mix(color, ink.waypoint, 1 - sstep(0.034, 0.052, point));
    color = mix(ink.shoal, color, sstep(0.058, 0.067, w));
  }
  const value = 0.66 + 0.34 * sstep(-0.30, 0.68, facing);
  return color.map(channel => channel * value);
}

function armillary(s, x, z, lights, parts = "all") {
  const cy = armillaryHeight;
  s.bound(x, 1.12, z, 1.53);
  if (parts !== "gimbals") {
    octagonalStep(s, x, 0.065, z, 0.71, 0.085, 0.035, "rock");
    octagonalStep(s, x, 0.145, z, 0.647, 0.045, 0.024, "paving");
    octagonalStep(s, x, 0.229, z, 0.461, 0.077, 0.02, "iron");
    octagonalStep(s, x, 0.435, z, 0.319, 0.15, 0.044, "slate");
    octagonalStep(s, x, 0.581, z, 0.379, 0.047, 0.02, "paving");
    octagonalStep(s, x, 0.656, z, 0.288, 0.046, 0.021, "armillaryFace");
    for (const side of [-1, 1]) {
      const ux = meridian.u[0] * side, uz = meridian.u[2] * side;
      const foot = [x + ux * 0.23, 0.66, z + uz * 0.23];
      const shoulder = [x + ux * 0.49, 0.91, z + uz * 0.49];
      const bearing = [x + ux * 0.695, cy, z + uz * 0.695];
      beamBetween(s, foot, shoulder, 0.078, 0.103, "armillaryFace");
      beamBetween(s, shoulder, bearing, 0.064, 0.075, "armillaryFace");
      beamBetween(s, foot, shoulder, 0.028, 0.113, "armillaryRim");
      beamBetween(s, shoulder, bearing, 0.024, 0.084, "armillaryRim");
      s.add(ellipsoid(...bearing, 0.109, 0.098, 0.099), "iron");
      const cap = [bearing[0] + ux * 0.067, cy, bearing[2] + uz * 0.067];
      s.add(ellipsoid(...cap, 0.062, 0.068, 0.058), "armillaryRim");
    }
    ringBand(s, x, cy, z, armillaryBands.meridian, "meridian");
    hingeRings(s, [x, cy, z], meridian, innerMeridian, 0.665, 0.530);
  }
  if (parts !== "mount") {
    ringBand(s, x, cy, z, armillaryBands.innerMeridian, "innerMeridian");
    ringBand(s, x, cy, z, armillaryBands.horizon, "horizon");
    hingeRings(s, [x, cy, z], innerMeridian, horizon, 0.511, 0.412, "movingPin", "movingPivot");
    const from = [x, cy, z].map((value, i) => value - horizon.u[i] * 0.39);
    const to = [x, cy, z].map((value, i) => value + horizon.u[i] * 0.39);
    beamBetween(s, from, to, 0.025, 0.028, "crystalSpindle");
    s.add((px, py, pz) => {
      const dy = py - cy, u = Math.abs(px - x - 0.18 * dy), w = Math.abs(pz - z);
      return Math.max(u, w, (u + w) * Math.SQRT1_2) + Math.abs(dy) * 0.62 - 0.151;
    }, "armillaryCrystal");
  }
  lights.push({ x, y: cy, z, power: 0.20 });
  s.bound();
}

export function armillaryGimbals(bone) {
  const volume = shape();
  const destination = bone == null ? volume : {
    bound: (...args) => volume.bound(...args),
    add: (dist, material, blend) => {
      if (armillaryBindings[material] === bone) volume.add(dist, material, blend);
    },
  };
  armillary(destination, ...armillaryCenter, [], "gimbals");
  return volume;
}

export function armillaryMount() {
  const volume = shape();
  armillary(volume, ...armillaryCenter, [], "mount");
  return volume;
}

function conifer(s, x, z, height, heading, spread, wind) {
  const cos = Math.cos(heading), sin = Math.sin(heading);
  s.bound(x, height * 0.48, z, height * 0.82);
  s.add(cone([x, -0.04, z], [x + wind * cos, height * 0.91, z - wind * sin], 0.066, 0.018), "wood");
  const tiers = [
    [0.24, 0.65, 1.0, "conifer"],
    [0.43, 0.85, 0.83, "conifer"],
    [0.63, 1.0, 0.61, "coniferTip"],
  ];
  for (const [foot, tip, width, material] of tiers) {
    const brim = height * spread * width;
    s.add((px, py, pz) => {
      const t = py / height;
      const u = (px - x) * cos - (pz - z) * sin - wind * t;
      const v = (px - x) * sin + (pz - z) * cos;
      const a = Math.abs(u), b = Math.abs(v) * 1.10;
      const radius = Math.max(a * 0.89 + b * 0.30, a * 0.43 + b * 0.83);
      const shelf = (tip - t) / (tip - foot);
      const branch = v + u * 0.32;
      const unevenFoot = height * foot + 0.032 * Math.cos(branch * 23 + width * 11) + 0.095 * u;
      return Math.max(radius - brim * Math.min(1, Math.max(0.018, shelf)), unevenFoot - py, py - tip * height);
    }, material);
  }
  s.bound();
}

function rockStratum(s, x, y, z, angle, width, halfHeight, depth, tilt, material) {
  const cos = Math.cos(angle), sin = Math.sin(angle);
  s.bound(x, y, z, Math.hypot(width, halfHeight, depth) + 0.18);
  s.add((px, py, pz) => {
    const dy = py - y;
    const u = (px - x) * sin - (pz - z) * cos - dy * tilt;
    const v = (px - x) * cos + (pz - z) * sin;
    const shear = Math.max(0, -dy) * 0.31;
    return Math.max(Math.abs(u + v * 0.19) - width + shear * 0.34, v - depth + shear, -v - depth,
      (Math.abs(u) + Math.max(0, v) * 0.73 - width * 0.84 - depth * 0.42) * 0.76,
      Math.abs(dy + u * tilt * 0.33 + v * 0.18) - halfHeight, py + 0.032);
  }, material);
  s.bound();
}

function observatoryCliff(s, planes, outline) {
  s.add((x, y, z) => {
    const lip = Math.max(0, -y - 0.047) * 0.73;
    return Math.max(outline(x, z) + lip, y, -0.185 - y);
  }, "grass");
  s.add((x, y, z) => {
    const depth = -y;
    const taper = 0.043 + 0.27 * depth + 0.70 * Math.max(0, depth - 0.38)
      - 0.625 * Math.max(0, depth - 0.64) + 0.59 * Math.max(0, depth - 1.22);
    const fault = (vnoise3(x * 1.21, y * 0.94, z * 1.08) - 0.5) * (0.13 + 0.16 * sstep(0.32, 1.19, depth));
    const undercut = -2.49 - y + vnoise3(x * 0.62, 6, z * 0.63) * 0.38;
    return Math.max(outline(x + 0.19 * y, z - 0.17 * y) + taper + fault, y + 0.064, undercut);
  }, "cliff");

  // Stagger the exposed strata around the rim, with quiet stretches between outcrops.
  const shelves = [
    [0.21, 0.60, 0.73, 0.56, 0.56, -0.22], [0.55, 0.43, 0.55, 0.50, 0.50, 0.34],
    [1.37, 0.61, 0.87, 0.56, 0.64, 0.28], [1.64, 0.49, 0.53, 0.51, 0.54, -0.31],
    [2.40, 0.63, 0.78, 0.63, 0.68, 0.26], [2.71, 0.52, 0.57, 0.48, 0.53, -0.25],
    [3.26, 0.49, 0.81, 0.65, 0.57, 0.31], [3.52, 0.70, 0.91, 0.75, 0.76, -0.30],
    [4.19, 0.63, 0.58, 0.48, 0.57, -0.15], [4.72, 0.54, 0.79, 0.61, 0.64, 0.27],
    [5.27, 0.54, 0.59, 0.49, 0.56, 0.32], [5.86, 0.62, 0.84, 0.66, 0.67, -0.25],
  ];
  for (const [i, [angle, width, depthY, height, thickness, tilt]] of shelves.entries()) {
    const dx = Math.cos(angle), dz = Math.sin(angle);
    let reach = Infinity;
    for (const [nx, nz, distance] of planes) {
      const alignment = dx * nx + dz * nz;
      if (alignment > 0) reach = Math.min(reach, distance / alignment);
    }
    const radius = reach - thickness + 0.06;
    rockStratum(s, dx * radius, -depthY, dz * radius, angle, width, height, thickness, tilt,
      i % 4 === 1 ? "weatheredLedge" : "basalt");
  }
}

function segmentDistance(x, z, a, b) {
  const dx = b[0] - a[0], dz = b[1] - a[1];
  const t = Math.min(1, Math.max(0, ((x - a[0]) * dx + (z - a[1]) * dz) / (dx * dx + dz * dz)));
  return Math.hypot(x - a[0] - dx * t, z - a[1] - dz * t);
}

function paveObservatory(s, stations, plaza) {
  const height = 0.010;
  const [courtX, courtZ] = [plaza[0] + 0.06, plaza[1] - 0.28];
  const plazaEdge = (x, z) => {
    const dx = x - courtX, dz = z - courtZ;
    const along = Math.abs(dx * 0.52 - dz * 0.854);
    const across = Math.abs(dx * 0.854 + dz * 0.52);
    return Math.max(along - 2.65, across - 2.35, (along * 0.59 + across * 0.81) - 2.77);
  };
  const surface = (distance, y) => Math.max(distance + Math.max(0, y + 0.014), y - height, -0.052 - y);
  s.bound(courtX, 0, courtZ, 3.85);
  s.add((x, y, z) => surface(plazaEdge(x, z), y), "pathStone");

  // Route the two northern approaches around the armillary plinth.
  const bends = { Fusion: [[1.46, 0.34], [1.66, -0.61]], Nest: [[-0.68, -0.23]] };
  const segments = [], routes = [];
  for (const { id, position: [x, , z], approach: [ax, , az], obstacle } of stations) {
    const reach = Math.hypot(ax - x, az - z);
    const threshold = [x + (ax - x) * (obstacle - 0.1) / reach, z + (az - z) * (obstacle - 0.1) / reach];
    const route = [plaza, ...(bends[id] ?? []), [ax, az], threshold];
    const width = id === "Expeditions" || id === "Team" ? 0.86 : 0.75;
    routes.push({ id, points: route });
    let run = 0;
    for (let i = 1; i < route.length; i++) {
      const [sx, sz] = route[i - 1], [ex, ez] = route[i];
      const length = Math.hypot(ex - sx, ez - sz);
      const ux = (ex - sx) / length, uz = (ez - sz) / length;
      const terminal = i === route.length - 1;
      const segment = { sx, sz, ux, uz, length, width, run, terminal, taperIn: i === 1 };
      segments.push(segment);
      run += length;
      s.bound((sx + ex) / 2, 0, (sz + ez) / 2, length / 2 + width + 0.17);
      s.add((px, y, pz) => {
        const [along, across] = pathCoordinates(segment, px, pz);
        return surface(pathEdge(segment, along, across), y);
      }, "pathStone");
    }
  }
  s.bound();
  const ringRadius = 4.62, ringWidth = 0.65;
  s.add((x,y,z) => surface(Math.abs(Math.hypot(x,z) - ringRadius) - ringWidth,y), "pathStone");

  function pavingAt(x, z) {
    const court = plazaEdge(x, z);
    let distance = court, pathDistance = Infinity;
    let along = 0, across = 0, threshold = 0;
    for (const segment of segments) {
      const [u, v] = pathCoordinates(segment, x, z);
      const edge = pathEdge(segment, u, v);
      distance = Math.min(distance, edge);
      if (edge >= pathDistance) continue;
      pathDistance = edge;
      along = u + segment.run;
      across = v;
      threshold = segment.terminal
        ? 1 - sstep(0.038, 0.071, Math.abs(u - segment.length * 0.54))
        : 0;
    }
    const radial = Math.hypot(x,z) - ringRadius, ringEdge = Math.abs(radial) - ringWidth;
    if (ringEdge < pathDistance) {
      distance = Math.min(distance,ringEdge);
      along = Math.atan2(z,x) * ringRadius; across = radial; threshold = 0;
    }
    return { along, across, distance, court, threshold, surveyX: x - plaza[0] + 0.29, surveyZ: z - plaza[1] };
  }

  return { at: pavingAt, height, routes, plaza };
}

function pathCoordinates({ sx, sz, ux, uz }, x, z) {
  const dx = x - sx, dz = z - sz;
  return [dx * ux + dz * uz, dz * ux - dx * uz];
}

function pathEdge({ width, length, terminal, taperIn }, along, across) {
  const entryFlare = taperIn ? 0.12 * (1 - sstep(0.31, 1.55, along)) : 0;
  const exitFlare = terminal ? 0.086 * sstep(0.0, length, along) : 0;
  const end = Math.max(-along, along - length);
  const side = Math.abs(across) - width - entryFlare - exitFlare;
  return Math.max(side, end - width * 0.55, (end + side - width * 0.21) * Math.SQRT1_2);
}

function nearestJoint(point, step) {
  return Math.abs(point - Math.round(point / step) * step);
}

function compassPoint(u, v, length, width) {
  const axial = Math.max(Math.abs(u), Math.abs(v));
  const lateral = Math.min(Math.abs(u), Math.abs(v));
  const edge = lateral - width * (1 - axial / length);
  return 1 - sstep(-0.014, 0.017, Math.max(edge, axial - length));
}

function paintPaving({ along, across, distance, court, threshold, surveyX, surveyZ }) {
  // Stagger the slabs on each run. Carry the course phase through bends.
  const course = 0.32, span = 0.54;
  const row = Math.floor(across / course + 0.5);
  const offset = along + row * span * 0.5;
  const column = Math.floor(offset / span);
  const pathJoint = Math.min(nearestJoint(across + course * 0.5, course), nearestJoint(offset, span));
  const pathTone = (vnoise3(column * 1.32, row * 1.71, 4.23) - 0.5) * 0.16 -
    (1 - sstep(0.006, 0.028, pathJoint)) * 0.24;
  const rim = 1 - sstep(0.077, 0.133, -distance);
  const plazaMix = 1 - sstep(-0.048, 0.04, court);
  let courtTone = 0, divider = 0, inlay = 0;
  if (plazaMix > 0) {
    const radius = Math.hypot(surveyX, surveyZ);
    const angle = Math.atan2(surveyZ, surveyX);
    const ring = Math.floor((radius - 0.66) / 0.36);
    const sectorAngle = Math.PI / 8;
    const azimuth = angle + (ring % 2) * sectorAngle * 0.5;
    const sector = Math.floor(azimuth / sectorAngle);
    const radialJoint = nearestJoint(radius - 0.66, 0.36);
    const spokeJoint = Math.sin(nearestJoint(azimuth, sectorAngle)) * radius;
    const joint = sstep(0.59, 0.72, radius) * (1 - sstep(0.006, 0.026, Math.min(radialJoint, spokeJoint)));
    courtTone = (vnoise3(sector * 1.22, ring * 1.57, 6.48) - 0.5) * 0.15 - joint * 0.24;
    divider = 1 - sstep(0.016, 0.046, Math.abs(radius - 0.64));
    const diagonalX = (surveyX + surveyZ) * Math.SQRT1_2;
    const diagonalZ = (surveyX - surveyZ) * Math.SQRT1_2;
    inlay = Math.max(compassPoint(surveyX, surveyZ, 0.54, 0.15),
      compassPoint(diagonalX, diagonalZ, 0.36, 0.056));
  }
  const thresholdBand = threshold * (1 - plazaMix);
  const border = Math.max(rim * (0.93 + pathTone * 0.18), divider * 0.79, thresholdBand * 0.74);
  return { plazaMix, border, inlay: inlay * plazaMix,
    tone: pathTone * (1 - plazaMix) + courtTone * plazaMix };
}

export function buildObservatory(stations, { separateGimbals = false, separateForge = false, fineStations = false } = {}) {
  const rng = mulberry32(84721);
  const c = makeCtx(9.8, 9.1, () => 0.6, rng), { s } = c;
  const lights = [], free = makeFree(c);
  const center = armillaryCenter;
  const atlasPosition = stations.find(({ id }) => id === "Expeditions")?.position;
  const marketPosition = stations.find(({ id }) => id === "EggShop")?.position;
  const rareCenter = marketPosition && [marketPosition[0] + 0.237 * Math.cos(marketTurn) + 0.221 * Math.sin(marketTurn),
    marketPosition[2] - 0.237 * Math.sin(marketTurn) + 0.221 * Math.cos(marketTurn)];
  const main = [-0.1, 0.55];
  const planes = Array.from({ length: 11 }, (_, i) => {
    const a = i * TAU / 11 + 0.09;
    return [Math.cos(a), Math.sin(a), 9.1 + rng() * 0.50];
  });
  const outline = (x, z) => {
    let edge = -Infinity;
    for (const [nx, nz, distance] of planes) edge = Math.max(edge, x * nx + z * nz - distance);
    return edge;
  };
  c.height = () => 0;
  c.grow(2.34);
  observatoryCliff(s, planes, outline);

  const paving = paveObservatory(s, stations, main);
  const occupied = (x, z, pad = 0) => Math.hypot(x - center[0], z - center[1]) < 0.78 + pad ||
    stations.some(({ position: [sx, , sz], obstacle }) => Math.hypot(x - sx, z - sz) < obstacle + pad);

  const props = { Team: lodge, Nest: nest, EggShop: market, Fusion: fusion, Improve: forge, Expeditions: atlas };
  const stationSurfaces = [];
  const groves = treeGroups;
  function placeProps(destination, lamps, armillaryParts = "all") {
    stations.forEach(({ id, position: [x, , z] }) => {
      if (id === "Improve" && separateForge && destination === s) {
        lamps.push({ x: x + hearthX * Math.cos(forgeAngle) + 0.225 * Math.sin(forgeAngle), y: 0.65,
          z: z - hearthX * Math.sin(forgeAngle) + 0.225 * Math.cos(forgeAngle), power: 0.43, halo: false });
      } else if (fineStations && destination === s && id !== "Improve") {
        const volume = shape();
        props[id]({add:volume.add,bound(){}}, x, z, lamps);
        stationSurfaces.push({ id, x, z, volume });
      } else props[id](destination, x, z, lamps);
    });
    armillary(destination, ...center, lamps, armillaryParts);
    for (const [x,z,turn] of telescopes) {
      destination.bound(x,0.8,z,2.1);
      octagonalStep(destination,x,0.035,z,1.32,0.055,0.04,"surveyStone");
      octagonalStep(destination,x,0.092,z,1.17,0.025,0.022,"paving");
      const axis = [Math.sin(turn)*0.87,0.49,Math.cos(turn)*0.87];
      const pt = d => [x+d*axis[0],1.32+d*axis[1],z+d*axis[2]];
      for(let i=0;i<3;i++) {
        const a=turn+i*TAU/3;
        beamBetween(destination,[x+Math.sin(a)*0.51,0.12,z+Math.cos(a)*0.51],[x,1.04,z],0.056,0.068,"wood");
      }
      destination.add(cyl(x,1.09,z,0.18,0.13,0.02),"bronze");
      destination.add(cone(pt(-0.52),pt(0.66),0.12,0.215),"armillaryEnamel");
      for(const [from,to,r] of [[-0.61,-0.48,0.12],[0.05,0.16,0.20],[0.57,0.7,0.235]])
        destination.add(cone(pt(from),pt(to),r,r),"armillaryRim");
      destination.add(cone(pt(0.696),pt(0.719),0.18,0.18),"fusionCore");
      destination.bound();
    }
    for (const tree of groves) {
      if (separateForge && destination === s && tree[6] === "forge") continue;
      conifer(destination, ...tree);
    }
    for (const furniture of observatoryFurniture) {
      if (separateForge && destination === s && furniture.kind === "bench") continue;
      addObservatoryFurniture(destination, furniture, lamps);
    }
  }
  placeProps(s, lights, separateGimbals ? "mount" : "all");

  const wild = (x, z) => outline(x, z) < -0.19 && paving.at(x, z).distance > 0.12 && !occupied(x, z, 0.06);
  const clustered = (x, z) => wild(x, z) && Math.hypot(x, z) > 3.22 &&
    (vnoise3(x * 1.15, 3, z * 1.15) > 0.59 || groves.some(([tx, tz]) => Math.hypot(x - tx, z - tz) < 0.76));
  free.grass(2100, clustered, {h: [0.09, 0.196], w: 0.010, colors: [[rgb("#334743"), rgb("#80916c")], [rgb("#354b45"), rgb("#8d8868")]]});
  free.grass(970, wild, {h: [0.037, 0.092], w: 0.010, colors: [[rgb("#334b44"), rgb("#77876a")], [rgb("#3b534b"), rgb("#768363")]]});
  free.litter(460, wild, {size: 0.023, colors: [rgb("#81886d"), rgb("#546e62"), rgb("#978c6a"), rgb("#71807e")]});
  free.dots(230, clustered, {size: 0.015, y: [0.078, 0.155], colors: [rgb("#9a8098"), rgb("#c2af89"), rgb("#728d88")]});
  const haloColor = rgb("#d9995e");
  for (const { x, y, z, power, halo = true } of lights) {
    if (power < 0.25 || !halo) continue;
    free.push([x, y, z], [0.12, 0.115, 0.12], [0, 0, 0, 1], haloColor, 3, 0.15);
  }
  let shadows;
  function staticShadows() {
    if (!shadows) {
      const casters = shape();
      placeProps(casters, []);
      shadows = bakeDirectionalShadow(casters, keyLight, 11.5, 0.058);
    }
    return shadows;
  }

  function paint(material, x, y, z, n) {
    if (material === "atlasChart" || material === "atlasBack") {
      return { c: paintAtlas(x - atlasPosition[0], y, z - atlasPosition[2], ...n, material === "atlasBack") };
    }
    const texture = vnoise3(x * 10, y * 9, z * 10) - 0.5;
    const band = armillaryBands[material];
    let metalAccent = 0;
    if (band?.frame === meridian) {
      const offset = [x - center[0], y - armillaryHeight, z - center[1]];
      const dot = (axis) => offset[0] * axis[0] + offset[1] * axis[1] + offset[2] * axis[2];
      const ux = dot(band.frame.u), vy = dot(band.frame.v);
      const radial = Math.abs(Math.hypot(ux, vy) - band.radius);
      const axial = Math.abs(dot(band.frame.n));
      const onFace = sstep(0.023, 0.044, axial);
      const border = sstep(0.033, 0.056, radial);
      const graduation = Math.atan2(vy, ux) * 12 / TAU;
      const mark = 1 - sstep(0.054, 0.135, Math.abs(graduation - Math.round(graduation)));
      metalAccent = onFace * Math.max(border * 0.48, mark * (1 - sstep(0.020, 0.038, radial)));
    }
    if (band) material = band.face;
    else material = movingMaterials[material] ?? material;
    if (material === "grass" && n[1] < 0.55) material = "turfEdge";
    const floor = material === "pathStone" ? paintPaving(paving.at(x, z)) : null;
    let grain = texture * (band || material === "fusionCore" || material === "nestShell" ? 0.045 : 0.15);
    if (material === "grass") grain += (vnoise3(x * 1.9, 2, z * 1.9) - 0.5) * 0.38;
    if (material === "cliff" || material === "basalt" || material === "weatheredLedge") {
      const face = vnoise3(x * 1.42, y * 0.82, z * 1.16);
      const bedding = -y + (vnoise3(x * 0.78, 4, z * 0.81) - 0.5) * 0.19;
      const seam = Math.min(Math.abs(bedding - 0.46), Math.abs(bedding - 0.92), Math.abs(bedding - 1.33));
      grain += (face - 0.5) * 0.28 + 0.08 * (1 - sstep(0.18, 0.96, bedding));
      grain -= 0.16 * (1 - sstep(0.023, 0.074, seam));
    }
    if (material === "wood" || material === "panel" || material === "benchWood")
      grain += (vnoise3(x * 15, y * 2.5, z * 13) - 0.5) * 0.20;
    const facing = n[0] * keyLight[0] + n[1] * keyLight[1] + n[2] * keyLight[2];
    let value = 0.30 + 0.34 * sstep(-0.25, 0.24, facing) + 0.30 * sstep(0.5, 0.81, facing) + grain;
    if (material === "paving") value += (vnoise3(x * 2.46, 4, z * 2.72) - 0.5) * 0.23;
    if (floor) value += floor.tone;
    let color = rampColor(ramps[material], value);
    if (material === "nestShell") {
      const freckles = sstep(0.68, 0.84, vnoise3(x * 17, y * 20, z * 17));
      const towardBase = 0.28 + 0.72 * (1 - sstep(0.72, 1.02, y));
      color = mix(color, rampColor(ramps.nestSpots, value), freckles * towardBase * 0.60);
    }
    if (material === "marketRareShell") {
      const azimuth = Math.atan2(z - rareCenter[1], x - rareCenter[0]);
      const vein = Math.abs(Math.sin(azimuth * 2 + (y - 0.972) * 20 + texture * 1.55));
      color = mix(color, ramps.armillaryRim[2], (1 - sstep(0.055, 0.20, vein)) * 0.64);
    }
    if (floor) {
      color = mix(color, rampColor(ramps.plazaStone, value), floor.plazaMix);
      color = mix(color, rampColor(ramps.pathBorder, value - 0.09), floor.border * 0.89);
      color = mix(color, rampColor(ramps.surveyInlay, value), floor.inlay * 0.74);
    }
    color = mix(color, ramps.armillaryRim[2], metalAccent * 0.54);
    return { c: color, emissive: material === "flare" || material === "ember" || material === "fusionTrace" || material === "nestHeat" };
  }

  function shade(color, x, y, z, nx, ny, nz, { ao }) {
    const low = y < -0.16 ? 0.85 + 0.15 * sstep(-2.3, -0.2, y) : 1;
    const contact = 0.76 + 0.24 * ao;
    const facing = nx * keyLight[0] + ny * keyLight[1] + nz * keyLight[2];
    const sunVisibility = y > -0.08 ? staticShadows().visibility(x, y, z, facing) : 1;
    const key = sstep(-0.10, 0.79, facing) * sunVisibility;
    const reflectedLight = [0, 0, 0];
    if (y > -0.07) for (const light of lights) {
      const delta = [light.x - x, light.y - y, light.z - z];
      const d2 = delta[0] ** 2 + delta[1] ** 2 + delta[2] ** 2;
      if (d2 > 0.92 || d2 < 0.015) continue;
      const facing = Math.max(0, (nx * delta[0] + ny * delta[1] + nz * delta[2]) / Math.sqrt(d2));
      const strength = light.power * (0.22 + facing * 0.78) * (1 - d2 / 0.92) ** 2;
      const tint = light.tint ?? warmLight;
      for (let i = 0; i < 3; i++) reflectedLight[i] += tint[i] * strength;
    }
    return color.map((value, i) => Math.min(1, value * low * contact * (skyFill[i] + sunlight[i] * key) + reflectedLight[i]));
  }

  return {
    c, paint, lighting: { shade, staticShadows }, lights, paving, stationSurfaces,
    get free() {
      return free.list.map(splat => {
        if (splat[13] === 3) return splat;
        const [x, y, z] = splat;
        const color = shade(splat.slice(10, 13), x, y, z, 0, 1, 0, { ao: 1 });
        return [...splat.slice(0, 10), ...color, ...splat.slice(13)];
      });
    },
  };
}

function rampColor([shadow, middle, highlight], value) {
  return value < 0.56 ? mix(shadow, middle, Math.max(0, value) / 0.56) :
    mix(middle, highlight, Math.min(1, (value - 0.56) / 0.44));
}

