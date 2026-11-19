import { cone, ellipsoid } from "../../Tools/lumon-splat-authoring/src/authoring/sdf.js";
import { mix, rgb, sstep } from "../../Tools/lumon-splat-authoring/src/authoring/island.js";
import { vnoise3 } from "../../Tools/lumon-splat-authoring/src/noise.js";

const ramps = Object.fromEntries(Object.entries({
  emberHide: ["#592932", "#ab4b37", "#e98750"],
  emberJaw: ["#905742", "#d3a267", "#f1ce86"],
  lavaPlate: ["#292739", "#4b3e56", "#786175"],
  lavaRim: ["#663035", "#a94736", "#df8050"],
  flame: ["#b44331", "#ef8641", "#ffd06e"],
  flameCore: ["#df6d36", "#ffa646", "#ffe59a"],
  waterSkin: ["#245069", "#3d8e9d", "#77c6c6"],
  waterShade: ["#263e59", "#3f6685", "#78a4b2"],
  waterBib: ["#476b82", "#91babb", "#c5ddcd"],
  wave: ["#2c5776", "#569db4", "#9cdbd9"],
  foam: ["#6299af", "#9fcbd2", "#e1e9d9"],
  sproutFur: ["#7c573d", "#bc9260", "#ebc690"],
  sproutBib: ["#937352", "#d6b98a", "#f1d9aa"],
  stalk: ["#38443a", "#687442", "#a2a25c"],
  leaf: ["#304639", "#677b3e", "#aaba63"],
  leafLight: ["#3a5340", "#83944b", "#c0c674"],
  claw: ["#564451", "#ae957b", "#dbbb97"],
  eye: ["#654747", "#ddbd91", "#f5e6c8"],
  iris: ["#192130", "#29293a", "#49414a"],
  gleam: ["#eddcba", "#f5e6c8", "#fff1ce"],
}).map(([material, colors]) => [material, colors.map(rgb)]));

function eyePair(s, x, y, z, width, height) {
  for (const side of [-1, 1]) {
    const cx = side * x;
    s.add(ellipsoid(cx, y, z, width * 1.13, height * 1.11, 0.043), "iris");
    s.add(ellipsoid(cx, y, z + 0.022, width, height, 0.032), "eye");
    s.add(ellipsoid(cx + 0.009, y + 0.003, z + 0.043, width * 0.55, height * 0.72, 0.022), "iris");
    s.add(ellipsoid(cx - 0.005, y + 0.021, z + 0.061, 0.016, 0.018, 0.009), "gleam");
  }
}

function leafBlade(s, [x, y, z], [endX, endY, endZ], width, thickness, material) {
  const dx = endX - x, dy = endY - y, dz = endZ - z;
  const length = Math.hypot(dx, dy, dz);
  const ux = dx / length, uy = dy / length, uz = dz / length;
  const inPlane = Math.hypot(ux, uy);
  const sx = uy / inPlane, sy = -ux / inPlane;
  s.add((px, py, pz) => {
    const a = px - x, b = py - y, c = pz - z;
    const along = a * ux + b * uy + c * uz;
    const side = a * sx + b * sy;
    const depth = (c - along * uz) / inPlane;
    const t = Math.max(0, Math.min(1, along / length));
    const breadth = Math.max(0.019, 0.032 * (1 - t) + width * Math.min(t * 3.7, (1 - t) * 1.75));
    const keel = thickness * (0.43 + 0.57 * Math.sin(Math.PI * t));
    const profile = (Math.abs(side) / breadth + Math.abs(depth) / keel - 1) * Math.min(breadth, keel);
    return Math.max(-along, along - length, profile);
  }, material);
}

function brasa(s) {
  s.add(ellipsoid(0, 0.38, -0.09, 0.43, 0.27, 0.56), "emberHide");
  s.add(ellipsoid(0, 0.54, -0.23, 0.49, 0.34, 0.55), "lavaPlate");
  s.add(ellipsoid(0, 0.41, -0.22, 0.498, 0.103, 0.56), "lavaRim");
  s.add(ellipsoid(0, 0.52, 0.25, 0.287, 0.24, 0.31), "emberHide", 0.022);
  s.add(ellipsoid(0, 0.427, 0.495, 0.242, 0.139, 0.184), "emberJaw", 0.014);
  s.add(ellipsoid(0, 0.508, 0.56, 0.192, 0.071, 0.14), "emberHide");
  eyePair(s, 0.129, 0.594, 0.485, 0.075, 0.085);
  for (const side of [-1, 1]) {
    for (const z of [-0.42, 0.155]) {
      s.add(cone([side * 0.284, 0.36, z], [side * 0.365, 0.13, z + 0.028], 0.115, 0.091), "emberHide");
      s.add(ellipsoid(side * 0.378, 0.083, z + 0.081, 0.114, 0.083, 0.147), "lavaRim");
      for (const toe of [-0.041, 0.041])
        s.add(ellipsoid(side * 0.378 + toe, 0.058, z + 0.198, 0.028, 0.040, 0.055), "claw");
    }
    s.add(ellipsoid(side * 0.082, 0.529, 0.674, 0.027, 0.018, 0.016), "iris");
  }
  leafBlade(s, [0.05, 0.705, 0.33], [0.12, 0.982, 0.25], 0.079, 0.089, "flame");
  leafBlade(s, [-0.04, 0.78, -0.34], [-0.20, 1.40, -0.45], 0.187, 0.165, "flame");
  leafBlade(s, [0.04, 0.80, -0.36], [0.25, 1.15, -0.44], 0.129, 0.097, "flame");
  leafBlade(s, [-0.075, 0.83, -0.231], [-0.14, 1.21, -0.313], 0.091, 0.078, "flameCore");
}

function gota(s) {
  s.add(ellipsoid(0, 0.34, 0.04, 0.343, 0.32, 0.28), "waterSkin");
  s.add(ellipsoid(0, 0.57, 0.11, 0.317, 0.226, 0.285), "waterSkin", 0.027);
  s.add(ellipsoid(0, 0.27, 0.229, 0.216, 0.233, 0.11), "waterBib");
  s.add(ellipsoid(0, 0.51, 0.289, 0.246, 0.125, 0.123), "waterBib");
  eyePair(s, 0.139, 0.622, 0.344, 0.064, 0.066);
  s.add(ellipsoid(0, 0.548, 0.405, 0.052, 0.030, 0.029), "waterShade");
  for (const side of [-1, 1]) {
    s.add(ellipsoid(side * 0.207, 0.081, 0.155, 0.133, 0.081, 0.195), "waterShade");
    s.add(cone([side * 0.216, 0.436, 0.112], [side * 0.38, 0.282, 0.13], 0.10, 0.07), "waterSkin");
    leafBlade(s, [side * 0.251, 0.574, 0.125], [side * 0.458, 0.751, 0.061], 0.078, 0.069, "wave");
  }
  const tail = [
    [[-0.13, 0.275, -0.13], 0.174], [[-0.35, 0.29, -0.475], 0.164],
    [[-0.585, 0.454, -0.454], 0.154], [[-0.659, 0.702, -0.357], 0.141],
    [[-0.594, 0.947, -0.209], 0.155], [[-0.423, 1.11, -0.113], 0.148],
    [[-0.264, 1.104, -0.102], 0.10], [[-0.221, 0.97, -0.084], 0.043],
  ];
  for (let i = 1; i < tail.length; i++) {
    const [from, a] = tail[i - 1], [to, b] = tail[i];
    s.add(cone(from, to, a, b), i > 5 ? "foam" : "wave", 0.017);
  }
}

function broto(s) {
  s.add(ellipsoid(0, 0.33, -0.025, 0.27, 0.28, 0.238), "sproutFur");
  s.add(ellipsoid(0, 0.364, 0.135, 0.18, 0.206, 0.13), "sproutBib");
  s.add(ellipsoid(0, 0.733, 0.022, 0.31, 0.264, 0.251), "sproutFur", 0.022);
  s.add(ellipsoid(0, 0.635, 0.221, 0.24, 0.116, 0.115), "sproutBib");
  eyePair(s, 0.128, 0.755, 0.250, 0.069, 0.078);
  s.add(ellipsoid(0, 0.677, 0.332, 0.049, 0.028, 0.026), "iris");
  for (const side of [-1, 1]) {
    s.add(ellipsoid(side * 0.175, 0.084, 0.106, 0.122, 0.084, 0.181), "sproutFur");
    s.add(cone([side * 0.207, 0.451, 0.014], [side * 0.296, 0.25, 0.083], 0.076, 0.057), "stalk");
    leafBlade(s, [side * 0.11, 0.827, 0.008], [side * 0.48, side > 0 ? 1.51 : 1.35, -0.065],
      side > 0 ? 0.22 : 0.174, 0.123, side > 0 ? "leafLight" : "leaf");
    leafBlade(s, [side * 0.10, 0.31, -0.191], [side * 0.46, 0.621, -0.388], 0.163, 0.091, "leaf");
  }
  leafBlade(s, [-0.05, 0.893, 0.122], [0.099, 1.093, 0.19], 0.095, 0.058, "leafLight");
}

const shellCenters = [[0, 0], [0, -0.76], [-0.67, -0.44], [0.67, -0.44],
  [0, 0.78], [-0.67, 0.42], [0.67, 0.42], [-1, 0], [1, 0]];

function shellInk(x, z) {
  let near = Infinity, next = Infinity, panel = 0;
  for (let i = 0; i < shellCenters.length; i++) {
    const [u, v] = shellCenters[i];
    const distance = (x / 0.49 - u) ** 2 + ((z + 0.23) / 0.54 - v) ** 2;
    if (distance < near) {
      next = near;
      near = distance;
      panel = i;
    } else if (distance < next) next = distance;
  }
  return { seam: 1 - sstep(0.022, 0.102, next - near), panel };
}

function pigment([shadow, middle, light], weight) {
  return weight <= 0.5 ? mix(shadow, middle, weight * 2) : mix(middle, light, (weight - 0.5) * 2);
}

export function lumonCreature(kind) {
  const construct = { brasa, gota, broto }[kind];
  if (!construct) throw new Error(`Unknown creature ${kind}`);
  const paint = (material, x, y, z, [nx, ny, nz]) => {
    const grain = vnoise3(x * 8, y * 7, z * 8) - 0.5;
    const facing = -0.39 * nx + 0.79 * ny + 0.40 * nz;
    let weight = 0.19 + 0.63 * sstep(-0.28, 0.86, facing) + grain * 0.12;
    if (material === "flame" || material === "flameCore") weight = 0.87 - 0.37 * sstep(0.74, 1.4, y) + grain * 0.10;
    if (material === "lavaPlate") {
      const { seam, panel } = shellInk(x, z);
      weight += ((panel * 7) % 11 - 5) * 0.019;
      return mix(pigment(ramps.lavaPlate, weight), pigment(ramps.flame, 0.76), seam * 0.90);
    }
    if (material === "wave") {
      const crest = 1 - sstep(0.08, 0.21, Math.abs(z + 0.313 - (y - 0.65) * 0.43));
      weight += crest * 0.14 * sstep(0.45, 0.92, y);
    }
    if (material === "leaf" || material === "leafLight") {
      weight -= 0.13 * sstep(0.11, 0.68, Math.abs(nx));
      if (y > 0.91 && Math.abs(x) > 0.10) {
        const height = (y - 0.827) / (x > 0 ? 0.683 : 0.523);
        const fromRib = Math.abs(Math.abs(x) - 0.11 - height * 0.37);
        const rib = 1 - sstep(0.012, 0.029, fromRib);
        const veinApex = height - fromRib * 1.62;
        const veinGap = Math.abs(veinApex - Math.round(veinApex * 4.2) / 4.2);
        const veins = (1 - sstep(0.019, 0.046, veinGap)) * (1 - sstep(0.087, 0.15, fromRib));
        weight += 0.20 * Math.max(rib, veins * 0.64) * (1 - sstep(0.79, 1.0, height));
      }
    }
    return pigment(ramps[material], Math.max(0, Math.min(1, weight)));
  };
  return { construct, paint };
}
