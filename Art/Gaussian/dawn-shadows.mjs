import fs from "node:fs";
import path from "node:path";
import { domeSite, flowerTrees } from "./observatory.mjs";

// Project the painted structures onto the same sunlight axis as the terrain baker.
export function shadeDawnGround(folder, lightFile, includeTerrain = true) {
  const side = 640, extent = 16, cell = 2*extent/side;
  const height = new Float32Array(side*side).fill(-10);
  const toCell = v => Math.floor((v+extent)/cell);
  const source = new Map();
  function ply(name) {
    if (!source.has(name)) {
      const bytes = fs.readFileSync(path.join(folder,name+".ply"));
      const offset = bytes.indexOf("end_header\n") + 11;
      if (offset<11 || (bytes.length-offset)%56 !== 0) throw Error("Invalid painted PLY: "+name);
      source.set(name,{bytes,offset});
    }
    return source.get(name);
  }
  function cast(name, anchor, yaw = 0, scale = 1) {
    const {bytes,offset}=ply(name), a=yaw*Math.PI/180, c=Math.cos(a),s=Math.sin(a);
    for(let j=offset;j<bytes.length;j+=56) {
      const x=bytes.readFloatLE(j)*scale,y=bytes.readFloatLE(j+4)*scale+anchor[1],z=bytes.readFloatLE(j+8)*scale;
      const col=toCell(anchor[0]+c*x+s*z+y*.65/.77), row=toCell(anchor[2]-s*x+c*z+y*.52/.77);
      if(col>=0 && col<side && row>=0 && row<side) {
        const id=row*side+col; height[id]=Math.max(height[id],y);
      }
    }
  }
  cast("crescent-pavilion",domeSite);cast("watchtower",[7.66,0,5.72]);
  flowerTrees.forEach(([x,z],i)=>cast("coral-tree",[x,0,z],119*i-36,1.12-i*.08));
  function shade(x,y,z) {
    const col=toCell(x+y*.65/.77),row=toCell(z+y*.52/.77);
    if(col<2 || row<2 || col>=side-2 || row>=side-2) return 0;
    let blocked=0;
    for(let v=-1;v<=1;v++)for(let u=-1;u<=1;u++){
      if(height[(row+v)*side+col+u]>y+.055)blocked+=(2-Math.abs(v))*(2-Math.abs(u));
    }
    return blocked/16;
  }
  if(includeTerrain) {
    const {bytes,offset}=ply("hub-world");
    for(let j=offset;j<bytes.length;j+=56){
      const y=bytes.readFloatLE(j+4);
      if(y<-.10)continue;
      // The grass blades have narrow, equal cross-sections. Bring their baked pigment
      // into the meadow's daylight ramp so individual tufts read as foliage.
      const width=Math.exp(bytes.readFloatLE(j+12)), depth=Math.exp(bytes.readFloatLE(j+20));
      if(y<.22 && width<.0101 && Math.abs(width-depth)<.0001) {
        for(const [k,pigment] of [.54,.64,.36].entries()){
          const p=j+40+4*k,v=bytes.readFloatLE(p)*.2820948+.5;
          bytes.writeFloatLE((v*.54+pigment*.46-.5)/.2820948,p);
        }
      }
      const strength=shade(bytes.readFloatLE(j),y,bytes.readFloatLE(j+8));
      if(!strength)continue;
      for(const [k,absorption] of [0.28,0.22,0.12].entries()){
        const p=j+40+4*k,v=bytes.readFloatLE(p)*.2820948+.5;
        bytes.writeFloatLE((v*(1-strength*absorption)-.5)/.2820948,p);
      }
    }
    fs.writeFileSync(path.join(folder,"hub-world.ply"),bytes);
  }
  const light=fs.readFileSync(lightFile),n=light.readInt32LE(4),origin=light.readFloatLE(8),h=light.readFloatLE(12);
  for(let j=0;j<n;j++)for(let i=0;i<n;i++)light[16+j*n+i]=Math.round(light[16+j*n+i]*(1-.85*shade(origin+i*h,.60,origin+j*h)));
  fs.writeFileSync(lightFile,light);
}
