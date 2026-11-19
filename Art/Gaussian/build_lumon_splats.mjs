// Lumon original procedural Gaussian-splat assets.
// node Art/Gaussian/build_lumon_splats.mjs
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { shape, sampleSurface, ellipsoid, cone, box } from "../../Tools/lumon-splat-authoring/src/authoring/sdf.js";
import { bake, rgb, mix, sstep } from "../../Tools/lumon-splat-authoring/src/authoring/island.js";
import { makeLighting } from "../../Tools/lumon-splat-authoring/src/authoring/light.js";
import { quatFromTo } from "../../Tools/lumon-splat-authoring/src/authoring/scene-kit.js";
import { vnoise3 } from "../../Tools/lumon-splat-authoring/src/noise.js";
import { armillaryBindings, armillaryGimbals, armillaryMotion, buildObservatory, observatoryObstacles, SKY, telescopes } from "./observatory.mjs";
import { eggShell } from "./egg-shell.mjs";
import { lumonCreature } from "./lumon-creatures.mjs";
import { flameTongues, forgeFireData, makeFlame } from "./forge-fire.mjs";
import { makePaintedForge, makePaintedBench } from "./painted-forge.mjs";

import { shadeDawnGround } from "./dawn-shadows.mjs";
import { makePavilion, makeCoralTree, makeMeteor, makeArcade, makeWatchtower, makeScopeFittings } from "./dawn-architecture.mjs";
import { stationPigment } from "./station-pigment.mjs";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(HERE, "../..");
const OUT = path.join(ROOT, "Assets/StreamingAssets/Lumon/Splats");
fs.mkdirSync(OUT, { recursive: true });

export const PALETTE = [
  "#292344", "#393358", "#514873", "#77718f", "#b7abc4",
  "#e1cba6", "#262c39", "#673447", "#9b4d52", "#d47b4e",
  "#176862", "#289b68", "#65c96d", "#c0eb72", "#61ead4",
  "#ffca55", "#ff8056", "#ed5d91", "#62a9e8", "#bb8bdd",
];
const stations = [
  { id:"Team", label:"EQUIPE", position:[-5.55,0,-2.4], approach:[-5.15,0,-1.05], radius:0.78, obstacle:0.82 },
  { id:"Nest", label:"NINHO", position:[-1.15,0,-6.05], approach:[-1.15,0,-4.7], radius:0.72, obstacle:0.72 },
  { id:"EggShop", label:"OVOS", position:[-5.85,0,2.65], approach:[-4.45,0,2.6], radius:0.8, obstacle:0.86 },
  { id:"Fusion", label:"FUSÃO", position:[4.6,0,-4.85], approach:[3.2,0,-4.6], radius:0.82, obstacle:0.85 },
  { id:"Improve", label:"APRIMORAR", position:[5.95,0,1.85], approach:[4.55,0,1.65], radius:0.78, obstacle:0.8 },
  { id:"Expeditions", label:"EXPEDIÇÕES", position:[0.7,0,6.45], approach:[0.7,0,5.05], radius:0.78, obstacle:0.76 },
];

function buildShapeAsset(construct, paint, h, ext, bottom, top) {
  const s=shape(); construct(s);
  const lighting = makeLighting(s, [], PALETTE, { quantize: false });
  const surf=sampleSurface(s,[-ext,bottom,-ext],[ext,top,ext],h);
  const col=new Uint8Array(surf.mat.length*4);
  for(let i=0;i<surf.mat.length;i++) {
    const x=surf.p[i*3],y=surf.p[i*3+1],z=surf.p[i*3+2],n=[surf.n[i*3],surf.n[i*3+1],surf.n[i*3+2]];
    const base=paint(surf.mat[i],x,y,z,n);
    const shaded=lighting.shade(base,x,y,z,...n,{ao:surf.ao[i]});
    const k=mix(base,shaded,0.55);
    col.set([k[0]*255,k[1]*255,k[2]*255,0],i*4);
  }
  return {p:new Float32Array(surf.p),n:new Float32Array(surf.n),col,materials:surf.mat,count:surf.mat.length,h};
}
function guardian() {
  const coat = 0, skin = 1, hair = 2, ink = 3, cape = 4, boot = 5, brass = 6, linen = 7;
  const trousers = 8, jade = 9, leather = 10, seam = 11, lining = 12;
  const colors = [
    "#325965", "#c98966", "#493635", "#2b2831", "#74465b", "#554042", "#bc9063",
    "#d9c9aa", "#6a5e58", "#75b7ae", "#825942", "#527783", "#4d3042",
  ].map(rgb);
  const faceShadow = rgb("#a76556");
  const lip = rgb("#8c5046");
  const iris = rgb("#62403a");
  const hairLight = rgb("#806057");

  function paint(material, x, y, z, normal) {
    let color = colors[material];
    if (material === skin && y > 1.03 && z > 0.102 && normal[2] > 0.17) {
      const side = Math.abs(x);
      const mask = (cx, cy, width, height) => {
        const distance = Math.hypot((side - cx) / width, (y - cy) / height);
        return 1 - sstep(0.70, 1.05, distance);
      };
      color = mix(color, faceShadow, mask(0.096, 1.133, 0.046, 0.024) * 0.55);
      color = mix(color, colors[hair], mask(0.064, 1.205, 0.043, 0.016) * 0.94);
      color = mix(color, iris, mask(0.065, 1.165, 0.035, 0.016) * 0.86);
      const smile = 1.092 + side * 0.07;
      const mouth = (1 - sstep(0.004, 0.0095, Math.abs(y - smile))) * (1 - sstep(0.021, 0.039, side));
      color = mix(color, lip, mouth * 0.85);
    }
    if (material === hair && y > 1.245) {
      const sweep = 1 - sstep(0.026, 0.052, Math.abs(x + 0.06 + (y - 1.28) * 0.89));
      color = mix(color, hairLight, sweep * 0.53 * sstep(-0.2, 0.75, normal[1]));
    }
    if (material === cape && z < -0.045) {
      const fold = 1 - sstep(0.022, 0.065, Math.abs(x - 0.075 - (y - 0.78) * 0.36));
      color = mix(color, colors[lining], fold * 0.30);
    }
    return color;
  }

  return buildShapeAsset(s => {
    s.add(ellipsoid(0, 0.762, 0.009, 0.181, 0.195, 0.115), coat);
    s.add(box(0, 0.626, 0.016, 0.141, 0.108, 0.10, 0.026), coat);
    s.add(box(0, 0.608, 0.016, 0.152, 0.031, 0.104, 0.021), leather);
    s.add(box(0.008, 0.612, 0.119, 0.039, 0.029, 0.017, 0.008), brass);
    s.add(box(0.008, 0.612, 0.137, 0.020, 0.013, 0.009, 0.005), leather);
    s.add(cone([-0.114, 0.692, 0.094], [0.116, 0.854, 0.114], 0.019, 0.021), leather);
    s.add(box(-0.184, 0.558, 0.089, 0.076, 0.099, 0.052, 0.025), leather);
    s.add(box(-0.183, 0.603, 0.129, 0.072, 0.042, 0.022, 0.013), seam);
    s.add(ellipsoid(-0.176, 0.586, 0.153, 0.018, 0.019, 0.012), brass);

    s.add(ellipsoid(0, 0.970, 0.025, 0.067, 0.082, 0.070), linen);
    s.add(ellipsoid(0, 1.169, 0.023, 0.143, 0.182, 0.131), skin);
    s.add(ellipsoid(0, 1.107, 0.042, 0.112, 0.091, 0.111), skin, 0.018);
    s.add(ellipsoid(0, 1.138, 0.152, 0.023, 0.032, 0.029), skin, 0.009);
    s.add(ellipsoid(-0.025, 1.291, -0.012, 0.150, 0.112, 0.138), hair);
    s.add(ellipsoid(0.004, 1.200, -0.083, 0.151, 0.144, 0.106), hair);
    s.add(cone([-0.129, 1.177, 0.074], [-0.042, 1.321, 0.101], 0.033, 0.069), hair);
    s.add(cone([0.125, 1.164, 0.055], [0.084, 1.309, 0.074], 0.023, 0.051), hair);
    s.add(ellipsoid(0.069, 1.114, -0.151, 0.067, 0.087, 0.069), hair);
    s.add(cone([0.069, 1.103, -0.17], [0.089, 0.998, -0.18], 0.053, 0.038), hair);
    s.add(ellipsoid(0.087, 1.014, -0.183, 0.041, 0.022, 0.043), leather);
    s.add(cone([0.088, 1.003, -0.183], [0.069, 0.963, -0.16], 0.036, 0.015), hair);

    for (const side of [-1, 1]) {
      const leg = (side < 0 ? 1 : 5) * 256;
      const arm = leg + 3 * 256;
      s.add(ellipsoid(side * 0.139, 1.162, 0.020, 0.024, 0.040, 0.031), skin);
      s.add(ellipsoid(side * 0.063, 1.165, 0.143, 0.023, 0.015, 0.014), ink);
      s.add(ellipsoid(side * 0.049, 1.170, 0.148, 0.009, 0.006, 0.009), linen);
      s.add(cone([side * 0.080, 0.548, 0.005], [side * 0.106, 0.257, 0.018], 0.078, 0.055), leg + trousers);
      s.add(cone([side * 0.106, 0.258, 0.018], [side * 0.115, 0.072, 0.046], 0.059, 0.054), leg + boot);
      s.add(box(side * 0.115, 0.052, 0.072, 0.065, 0.046, 0.102, 0.027), leg + boot);
      s.add(box(side * 0.115, 0.017, 0.073, 0.067, 0.017, 0.107, 0.012), leg + ink);
      s.add(cone([side * 0.156, 0.859, 0.002], [side * 0.218, 0.677, 0.025], 0.076, 0.057), arm + coat);
      s.add(cone([side * 0.218, 0.677, 0.025], [side * 0.235, 0.600, 0.051], 0.056, 0.042), arm + linen);
      s.add(ellipsoid(side * 0.239, 0.569, 0.054, 0.035, 0.056, 0.031), arm + skin);
    }

    s.add(ellipsoid(0.048, 0.895, -0.031, 0.193, 0.076, 0.117), cape);
    s.add((x, y, z) => {
      const t = Math.max(0, Math.min(1, (0.91 - y) / 0.27));
      const left = -0.097 + t * 0.035;
      const right = 0.217 - t * 0.028;
      const drape = -0.112 - 0.034 * t + 0.056 * t * t + (x - 0.075) ** 2 * 0.45;
      const hem = 0.664 - x * 0.14;
      return Math.max(left - x, x - right, hem - y, y - 0.924, Math.abs(z - drape) - 0.022);
    }, cape);
    s.add(ellipsoid(0.105, 0.879, 0.124, 0.040, 0.041, 0.022), brass);
    s.add(ellipsoid(0.103, 0.882, 0.144, 0.026, 0.027, 0.015), jade);
  }, (tag, ...surface) => paint(tag & 255, ...surface), 0.0155, 0.43, -0.03, 1.43);
}

function guardianBinding(region, height) {
  if (region === 0) return [0, 0, 0];
  if (region === 4 || region === 8) return [0, region, 1 - sstep(0.77, 0.89, height)];
  if (height >= 0.38) return [0, region, 1 - sstep(0.43, 0.57, height)];
  if (height >= 0.155) return [region, region + 1, 1 - sstep(0.205, 0.31, height)];
  return [region + 1, region + 2, 1 - sstep(0.086, 0.144, height)];
}

function creature(kind) {
  const { construct, paint } = lumonCreature(kind);
  return buildShapeAsset(construct, paint, 0.021, 0.98, -0.05, 1.64);
}
function egg(tier) {
  const rare = tier === "rare";
  const [shade, body, accent] = (rare ? ["#453d5a", "#827f9c", "#cdb489"] : ["#79675b", "#d6c5a0", "#a46f53"]).map(rgb);
  return buildShapeAsset(s => {
    s.add(eggShell(0, 0.50, 0, 0.355, 0.47, 0.35), 0);
  }, (m, x, y, z) => {
    const a = Math.atan2(z, x);
    const fragments = vnoise3(x * 7.3, y * 5.7, z * 7.3);
    if (!rare) {
      const flecks = vnoise3(x * 35, y * 30, z * 35);
      return mix(mix(shade, body, 0.53 + 0.46 * fragments), accent, sstep(0.64, 0.82, flecks) * 0.68);
    }
    const vein = Math.abs(Math.sin(a * 3 + y * 11 + fragments * 3.4));
    return mix(mix(shade, body, 0.36 + 0.61 * fragments), accent, 1 - sstep(0.025, 0.22, vein));
  }, 0.021, 0.52, -0.01, 1.03);
}

function writePly(name,baked,free=[],parts=[]) {
  const count=baked.count+free.length+parts.reduce((n,p)=>n+p.count,0), rows=Buffer.alloc(count*14*4), header=Buffer.from([
    "ply","format binary_little_endian 1.0",`comment Original procedural Lumon asset: ${name}`,"element vertex "+count,
    ...["x","y","z","scale_0","scale_1","scale_2","rot_1","rot_2","rot_3","rot_0","f_dc_0","f_dc_1","f_dc_2","opacity"].map(n=>"property float "+n),"end_header",""
  ].join("\n"),"ascii");
  const set=(i,p,s,q,c,a=1)=>{
    const o=i*56; rows.writeFloatLE(p[0],o);rows.writeFloatLE(p[1],o+4);rows.writeFloatLE(p[2],o+8);
    for(let k=0;k<3;k++) rows.writeFloatLE(Math.log(Math.max(0.0001,s[k])),o+12+k*4);
    for(let k=0;k<3;k++) rows.writeFloatLE(q[k],o+24+k*4);
    rows.writeFloatLE(q[3],o+36);
    for(let k=0;k<3;k++) rows.writeFloatLE((c[k]-0.5)/0.2820948,o+40+k*4);
    rows.writeFloatLE(Math.log(Math.max(0.0001,a)/Math.max(0.0001,1-a)),o+52);
  };
  let index=0;
  for(const layer of [baked,...parts]) {
    for(let i=0;i<layer.count;i++) {
      const j=i*3,p=[layer.p[j],layer.p[j+1],layer.p[j+2]],n=[layer.n[j],layer.n[j+1],layer.n[j+2]],q=quatFromTo([0,0,1],n),r=layer.h*.9;
      set(index++,p,[r,r,layer.h*.18],q,[layer.col[i*4]/255,layer.col[i*4+1]/255,layer.col[i*4+2]/255],layer.alpha?.[i] ?? 1);
    }
  }
  free.forEach(f=>set(index++,f.slice(0,3),f.slice(3,6),f.slice(6,10),f.slice(10,13),f[14]??1));
  fs.writeFileSync(path.join(OUT,name+".ply"),Buffer.concat([header,rows]));
  console.log(`${name}: ${count} splats, ${(rows.length/1048576).toFixed(2)} MiB`);
}

function bakeStation(scene, { id, volume, x, z }) {
  const h=.016;
  const surf=sampleSurface(volume,[x-.90,.02,z-.90],[x+.90,1.96,z+.90],h);
  const count=surf.mat.length, col=new Uint8Array(count*4);
  for(let i=0;i<count;i++) {
    const j=i*3, px=surf.p[j], py=surf.p[j+1], pz=surf.p[j+2], n=surf.n.slice(j,j+3);
    const mat=surf.mat[i], ink=scene.paint(mat,px,py,pz,n);
    let rgb=stationPigment(id,mat,px-x,py,pz-z,n,ink.c);
    if(!ink.emissive) rgb=scene.lighting.shade(rgb,px,py,pz,...n,{ao:surf.ao[i]});
    for(let k=0;k<3;k++) col[i*4+k]=Math.round(Math.max(0,Math.min(1,rgb[k]))*255);
  }
  console.log(id+": "+count+" station surfels");
  return {p:Float32Array.from(surf.p),n:Float32Array.from(surf.n),h,col,count};
}

function writeWorldLight(scene) {
  const field = scene.lighting.staticShadows();
  const side = 193, minimum = -9.6, spacing = 0.10, height = 0.60;
  const headerBytes = 16;
  const data = Buffer.alloc(headerBytes + side * side);
  data.write("LML1");
  data.writeInt32LE(side, 4);
  data.writeFloatLE(minimum, 8);
  data.writeFloatLE(spacing, 12);
  for (let row = 0; row < side; row++) {
    for (let col = 0; col < side; col++) {
      const visibility = field.visibility(minimum + col * spacing, height, minimum + row * spacing, 0.65);
      data[headerBytes + row * side + col] = Math.round(visibility * 255);
    }
  }
  fs.writeFileSync(path.join(ROOT, "Assets/Resources/Lumon/ObservatoryLight.bytes"), data);
  console.log(`Observatory light: ${side}×${side} visibility samples at Y=${height}`);
}

function writeArmillaryMotion(scene) {
  const pivot = armillaryMotion.pivot;
  const extent = 0.625, h = 0.020;
  const source = armillaryGimbals();
  const surfels = sampleSurface(source, pivot.map(v => v - extent), pivot.map(v => v + extent), h);
  const count = surfels.mat.length;
  if (count === 0) throw new Error("The armillary gimbals produced no splats");
  const local = new Float32Array(count * 3);
  const normals = new Float32Array(surfels.n);
  const color = new Uint8Array(count * 4);
  const bindings = Buffer.alloc(8 + count * 4);
  bindings.write("LAR1");
  bindings.writeUInt32LE(count, 4);
  for (let i = 0; i < count; i++) {
    const offset = i * 3;
    const [x, y, z] = surfels.p.slice(offset, offset + 3);
    local.set([x - pivot[0], y - pivot[1], z - pivot[2]], offset);
    const n = surfels.n.slice(offset, offset + 3);
    const material = surfels.mat[i];
    const bone = armillaryBindings[material];
    if (!bone) throw new Error(`Missing armillary motion assignment for ${material}`);
    const painted = scene.paint(material, x, y, z, n);
    const lit = painted.emissive ? painted.c : scene.lighting.shade(painted.c, x, y, z, ...n, { ao: surfels.ao[i] });
    color.set(lit.map(channel => Math.max(0, Math.min(255, channel * 255))), i * 4);
    bindings.writeUInt32LE(bone | (bone << 8), 8 + i * 4);
  }
  writePly("armillary-gimbals", { p: local, n: normals, col: color, count, h });
  fs.writeFileSync(path.join(ROOT, "Assets/Resources/Lumon/ArmillaryRig.bytes"), bindings);
}

function writeForgeFire() {
  for (const tongue of flameTongues) {
    const { field, shade } = makeFlame(tongue);
    const h = 0.0105;
    const surf = sampleSurface(field, [-0.17, -0.031, -0.14], [0.17, tongue.height + 0.021, 0.14], h);
    const count = surf.mat.length;
    const color = new Uint8Array(count * 4);
    const alpha = new Float32Array(count);
    for (let i = 0; i < count; i++) {
      const pigment = shade(...surf.p.slice(i * 3, i * 3 + 3));
      color.set(pigment.color.map(value => Math.round(value * 255)), i * 4);
      alpha[i] = pigment.opacity;
    }
    writePly(`forge-${tongue.id}`, { p: surf.p, n: surf.n, col: color, alpha, count, h });
  }
}

const forgeFire = forgeFireData(stations.find(station => station.id === "Improve"));
function writePaintedStations() {
  for (const [name, model] of [["forge-painted", makePaintedForge()], ["bench-painted", makePaintedBench()]]) {
    model.save(path.join(OUT, name+".ply"));
    console.log(name+": "+model.count+" visible surfels");
  }
}
function writeDawnArchitecture() {
  const parts = [
    ["crescent-pavilion", makePavilion], ["coral-tree", makeCoralTree], ["forge-meteor", makeMeteor],
    ["terrace-bay", makeArcade], ["watchtower", makeWatchtower], ["scope-fittings", ()=>makeScopeFittings(telescopes)],
  ];
  for (const [name, make] of parts) {
    const model = make();
    model.save(path.join(OUT, name + ".ply"));
    console.log(name + ": " + model.count + " surfels");
  }
}
const lightFile = path.join(ROOT, "Assets/Resources/Lumon/ObservatoryLight.bytes");
const manifestPath = path.join(OUT, "hub-manifest.json");
const started = Date.now();
const jobs = new Map([
  ["hub-world", () => {
    const scene = buildObservatory(stations, { separateGimbals: true, separateForge: true, fineStations: true });
    const baked = bake(scene.c, scene, { h: 0.041, bottom: -2.6, ext: 10.25, lighting: scene.lighting });
    writePly("hub-world", baked, scene.free, scene.stationSurfaces.map(station => bakeStation(scene,station)));
    writeWorldLight(scene);
    writeDawnArchitecture();
    shadeDawnGround(OUT,lightFile);
    writeArmillaryMotion(scene);
    writeForgeFire();
    writePaintedStations();
    const manifest = { version:2,seed:84721,palette:PALETTE,background:SKY,spawn:[3.60,0.08,1.42],stations,obstacles:observatoryObstacles,armillary:armillaryMotion,forgeFire,worldRadius:9.0,pixelSize:1,camera:{yaw:32,pitch:32,distance:14,orthographicSize:8.8,target:[0,0.23,0]} };
    manifest.paintedStations = true;
    fs.writeFileSync(manifestPath, JSON.stringify(manifest, null, 2));
  }],
  ["dawn-architecture", writeDawnArchitecture],
  ["painted-stations", writePaintedStations],
  ["forge-fire", () => {
    const manifest = JSON.parse(fs.readFileSync(manifestPath, "utf8"));
    const station = manifest.stations.find(station => station.id === "Improve");
    writeForgeFire();
    manifest.forgeFire = forgeFireData(station);
    fs.writeFileSync(manifestPath, JSON.stringify(manifest, null, 2));
  }],
  ["world-light", () => { writeWorldLight(buildObservatory(stations)); writeDawnArchitecture(); shadeDawnGround(OUT, lightFile, false); }],
  ["armillary-motion", () => writeArmillaryMotion(buildObservatory(stations))],
  ["guardian", () => {
    const baked = guardian();
    const soleRotation = quatFromTo([0,0,1], [0,1,0]);
    const shadows = [-0.115, 0.115].map(x => [
      x, -0.025, 0.071, 0.064, 0.099, 0.003, ...soleRotation, 0.071, 0.091, 0.091, 0, 0.43,
    ]);
    const count = baked.count + shadows.length;
    const binding = Buffer.alloc(8 + count * 4);
    binding.write("LGR1");
    binding.writeUInt32LE(count, 4);
    for (let i = 0; i < baked.count; i++) {
      const [first, second, blend] = guardianBinding(baked.materials[i] >> 8, baked.p[i * 3 + 1]);
      binding.writeUInt32LE((first | second << 8 | Math.round(blend * 65535) << 16) >>> 0, 8 + i * 4);
    }
    for (let i = 0; i < shadows.length; i++) {
      const bone = 9 + i;
      binding.writeUInt32LE(bone | bone << 8, 8 + (baked.count + i) * 4);
    }
    writePly("guardian", baked, shadows);
    fs.writeFileSync(path.join(ROOT, "Assets/Resources/Lumon/GuardianRig.bytes"), binding);
  }],
  ...["brasa", "gota", "broto"].map(kind => [`lumon-${kind}`, () => writePly(`lumon-${kind}`, creature(kind))]),
  ...["common", "rare"].map(tier => [`egg-${tier}`, () => writePly(`egg-${tier}`, egg(tier))]),
]);
const selected = process.argv.slice(2);
for (const name of selected) {
  if (!jobs.has(name)) {
    console.error(`Unknown asset "${name}". Choose from: ${[...jobs.keys()].join(", ")}`);
    process.exit(1);
  }
}
const partialWorldJobs = new Set(["dawn-architecture", "world-light", "armillary-motion", "forge-fire", "painted-stations"]);
for (const name of new Set(selected.length ? selected : [...jobs.keys()].filter(name => !partialWorldJobs.has(name)))) jobs.get(name)();
console.log(`Lumon splat source complete in ${((Date.now() - started) / 1000).toFixed(1)} s`);
