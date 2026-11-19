// Coordinates in metres, local to each workstation. Pigments are in display RGB [0,1].
const fract = t => t - Math.floor(t);
const tone = (color, factor) => color.map(c => c * factor);
const mix = (a,b,t) => a.map((v,i)=>v*(1-t)+b[i]*t);
const ink = [.19,.19,.23], brass = [.92,.71,.36], ivory = [.96,.84,.63];
const seam = u => Math.min(fract(u),1-fract(u));
function blocks(c,u,y,w,h) {
  const row=Math.floor(y/h), v=fract(y/h), across=fract(u/w+row*.5);
  if(v<.09 || across<.047) return mix(c,ink,.50);
  if(v>.87 || across>.94) return mix(c,ivory,.25);
  return tone(c,.95+.11*fract(Math.sin(row*17+Math.floor(u/w+row*.5)*91)*124.3));
}
export function stationPigment(id,mat,x,y,z,n,c) {
  const a=Math.atan2(z,x), face=Math.abs(n[0])>Math.abs(n[2])?z:x;
  if(mat==="paving" || mat==="rock" || mat==="mortar") {
    if(n[1]>.58) return blocks(c,x,z,.31,.22);
    return blocks(c,a/Math.PI*1.7,y,.31,.115);
  }
  if(mat==="wood" || mat==="panel" || mat==="teal") {
    const axis=mat==="teal" || Math.abs(n[1])<.45 && (id==="Team" && Math.abs(x)>.48 || id==="EggShop" && y>.68);
    const across=axis?face:y+.32*z, along=axis?y:face;
    const board=fract(across/.146);
    if(board<.078) return mix(c,ink,.48);
    if(board>.91) return mix(c,ivory,.16);
    const grain=Math.sin(124*across+1.7*Math.sin(along*11)+1.1*Math.sin(24*along+across*31));
    return tone(c,grain>.67?.88:1.035);
  }
  if(id==="Team" && (mat==="slate" || mat==="ridge")) {
    const row=Math.floor(Math.abs(x)/.17), course=fract(Math.abs(x)/.17);
    const tile=fract(z/.255+(row%2)*.5);
    if(tile<.053 || course>.87) return mix(c,ink,.50);
    if(course>.75) return mix(c,ivory,.29);
    return tone(c,.93+.15*fract((row+3*Math.floor(z/.255+(row%2)*.5))*.618));
  }
  if(id==="EggShop" && (mat==="cloth" || mat==="canvas")) {
    const u=x*Math.cos(1.94)-z*Math.sin(1.94);
    const stripe=seam((u+.1215)/.243);
    if(stripe<.085) return mix(c,ivory,.7);
    return tone(c,.91+.12*Math.cos(u/.243*Math.PI*2));
  }
  if(mat==="nestShell") {
    // Dark shoulder spots and a reserved clear band keep the shell readable in the cradle.
    const radius=id==="Nest"?.274:.135;
    const scaled=(y-(id==="Nest"?.80:.935))/radius;
    const stipple=Math.cos(7*a+.5*Math.cos(scaled*7))+.83*Math.sin(9*scaled+Math.cos(a*3));
    if(scaled<.58 && stipple>1.22) return mix(c,[.60,.36,.26],.59);
    return mix(c,ivory,.14);
  }
  if(mat==="nestGlaze") {
    if(y>.20 && y<.304 && Math.abs(Math.cos(a*8))+.8*Math.abs(y-.253)/.052<.63) return mix(c,brass,.82);
    return tone(c,1.05);
  }
  if(mat==="fusionCore" || mat==="fusionViolet") {
    const direction=fract(Math.atan2(n[2],n[0])/(2*Math.PI)+.23);
    const band=y+.29*x-.21*z;
    if(seam(band/.25)<.060 && direction<.61) return mix(c,ivory,.39);
    return tone(c,direction<.32?1.15:.96);
  }
  if(mat==="bronze" || mat==="armillaryRim") return mix(c,brass,n[1]>.45?.38:.12);
  return c;
}
