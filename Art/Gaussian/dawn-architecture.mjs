// Painted architectural models, in metres, Y-up. Each patch is a visible surface.
import { PaintedSurface, blend, dot } from "./painted-surface.mjs";
import { mulberry32 } from "../../Tools/lumon-splat-authoring/src/noise.js";
const tau = Math.PI * 2, sun = [-0.55, 0.78, -0.32];
const add = (a,b) => a.map((v,i)=>v+b[i]);
const sub = (a,b) => a.map((v,i)=>v-b[i]);
const mul = (a,k) => a.map(v=>v*k);
const unit = a => mul(a,1/Math.hypot(...a));
const cross = (a,b) => [a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
const pigment = {
  stone:[[107,118,147],[215,195,157],[255,231,187]], base:[[74,96,119],[151,158,151],[216,207,174]],
  gold:[[99,77,67],[203,148,67],[255,220,131]], rim:[[138,102,62],[231,172,69],[255,238,171]],
  copper:[[36,76,92],[63,141,137],[134,199,170]], dark:[[42,50,69],[83,76,76],[125,103,82]],
  silk:[[112,56,73],[193,76,62],[246,136,83]], bark:[[74,62,78],[127,86,72],[184,132,88]],
  leaves:[[37,71,79],[69,114,82],[129,153,83]], coral:[[126,51,64],[220,78,64],[255,145,87]],
  ember:[[111,44,47],[211,94,48],[255,191,71]]
};
function brush(key,bias=0){
  const [shadow,mid,lit] = pigment[key];
  return (p,n) => {
    const stroke = Math.sin(p[0]*8+p[1]*15+p[2]*7) + 0.53*Math.sin(p[2]*23-p[1]*9);
    const t = Math.max(0,Math.min(1,0.51+dot(n,sun)*0.49 + bias + Math.round(stroke*2)*0.012));
    return t<0.52 ? blend(shadow,mid,t/0.52) : blend(mid,lit,(t-0.52)/0.48);
  };
}
function quad(s,a,b,c,d,ink){s.quad(a,b,c,d,typeof ink==="string"?brush(ink):ink);}
function rod(s,a,b,r0,r1,ink,sides=10){
  const delta=unit(sub(b,a)), u=unit(cross(delta,Math.abs(delta[1])>0.92?[1,0,0]:[0,1,0])), v=cross(delta,u);
  const around=(p,r,t)=>add(p,add(mul(u,Math.cos(t)*r),mul(v,Math.sin(t)*r)));
  for(let i=0;i<sides;i++){
    const t=i*tau/sides, next=(i+1)*tau/sides;
    quad(s,around(a,r0,t),around(a,r0,next),around(b,r1,next),around(b,r1,t),ink);
    if(r1>0.001) quad(s,around(b,r1,t),around(b,r1,next),b,b,ink);
  }
}
const radial=(r,y,a)=>[r*Math.cos(a),y,r*Math.sin(a)];
function skirt(s,y0,r0,y1,r1,ink,n=32){
  for(let i=0;i<n;i++){
    let a=i*tau/n,b=(i+1)*tau/n;
    quad(s,radial(r0,y0,b),radial(r0,y0,a),radial(r1,y1,a),radial(r1,y1,b),ink);
  }
}
const frac=x=>x-Math.floor(x);
// Cut limestone: staggered joints with a sun-catching lip and a colder mortar bed.
function limestone(p,n){
  const y=p[1], h=.206, row=Math.floor(y/h);
  const angle=frac(Math.atan2(p[2],p[0])/tau);
  return ashlar(angle*12+row*.5,y/h,p,n,row);
}
function ashlar(along,up,p,n,row){
  const u=frac(along),v=frac(up);
  const tint=frac(Math.sin(Math.floor(along)*78.31+row*36.7)*1471);
  let c=brush("stone",(tint-.5)*.21-.06)(p,n);
  if(u<.038||v<.085) return blend(c,[69,73,87],.72);
  if(u<.094||v>.88) return blend(c,[246,223,178],.52);
  const fissure=.32+.15*Math.sin(v*4+tint*9);
  if(tint>.60 && v>.39 && Math.abs(u-fissure)<.016) c=blend(c,[89,87,94],.36);
  return c;
}
function roofPatina(p,n,plate,across,down){
  const value=frac(Math.sin(plate*78.31+9.7)*1471);
  let c=brush("copper",(value-.5)*.30-.06)(p,n);
  const stain=Math.sin(11*p[0]+6*p[2]+p[1]*2)+.6*Math.sin(22*p[2]-p[1]*13);
  if(stain>.64) c=blend(c,[119,172,147],.25);
  if(stain<-.79) c=blend(c,[52,87,96],.28);
  if(across<.046 || down<.10) return blend(c,[28,63,75],.64);
  if(across>.93 || down<.21) return blend(c,[154,200,164],.49);
  return c;
}
function roofBand(s,y,r,h,n) {
  skirt(s,y,r,y+.035,r+.029,"dark",n);
  skirt(s,y+.035,r+.029,y+h,r+.023,(p,normal)=>{
    const tooth=frac(Math.atan2(p[2],p[0])/tau*n*2);
    return brush(tooth<.21?"dark":"gold",tooth>.81?.10:0)(p,normal);
  },n);
  skirt(s,y+h,r+.023,y+h+.021,r,"rim",n);
}
function jewel(s,c,r,key){
  const pos=(a,t)=>add(c,[r[0]*Math.cos(a)*Math.sin(t),r[1]*Math.cos(t),r[2]*Math.sin(a)*Math.sin(t)]);
  for(let j=0;j<6;j++)for(let i=0;i<12;i++){
    const a=i*tau/12,b=(i+1)*tau/12,t=j*Math.PI/6,q=(j+1)*Math.PI/6;
    quad(s,pos(a,t),pos(b,t),pos(b,q),pos(a,q),key);
  }
}
export function makePavilion(){
  const s=new PaintedSurface(0.018);
  // Open stone loggia, with bolt capitals and radial voussoirs over each bay.
  for(const [y0,r0,y1,r1,k] of [[0,1.32,.10,1.32,"base"],[.10,1.32,.16,1.22,"stone"],[.16,1.22,.22,1.17,"stone"],[.22,1.17,.22,0,"stone"]]) skirt(s,y0,r0,y1,r1,k);
  const n=8, radius=1.04, column=a=>radial(radius,0,a);
  for(let i=0;i<n;i++){
    const a=i*tau/n, b=(i+1)*tau/n, A=column(a), B=column(b);
    const upright=y=>add(A,[0,y,0]);
    for(const [y0,r0,y1,r1,k] of [[.21,.16,.34,.16,"base"],[.34,.15,.42,.10,"stone"],[.42,.10,1.16,.085,"stone"],[1.16,.09,1.23,.14,"gold"],[1.23,.14,1.30,.16,"stone"]])
      rod(s,upright(y0),upright(y1),r0,r1,k,8);
    const center=mul(add(A,B),0.5), side=unit(sub(B,A)), out=unit(center), span=Math.hypot(...sub(B,A))*.5;
    const wedge=(t,r,d)=>add(add(center,mul(side,Math.cos(t)*r)),add([0,1.23+Math.sin(t)*r,0],mul(out,d)));
    for(let k=0;k<9;k++){
      const t=k*Math.PI/9+.007,q=(k+1)*Math.PI/9-.007,inner=span-.105,outer=span+.025+(k===4?.035:0);
      quad(s,wedge(t,inner,.073),wedge(q,inner,.073),wedge(q,outer,.073),wedge(t,outer,.073),(p,n,u,v)=>
        brush(u<.08||v<.12?"base":"stone",k===4?.13:(k%3-1)*.075)(p,n));
      quad(s,wedge(q,inner,-.073),wedge(t,inner,-.073),wedge(t,outer,-.073),wedge(q,outer,-.073),"stone");
      quad(s,wedge(q,inner,.073),wedge(t,inner,.073),wedge(t,inner,-.073),wedge(q,inner,-.073),"base");
    }
    if(i%2===1){
      const banner=(u,y,d)=>add(add(center,mul(side,u)),add([0,y,0],mul(out,d)));
      for(let k=0;k<8;k++){
        const x=-.17+.34*k/8, xe=x+.34/8;
        const hem=t=>.88+.16*Math.abs(t)/.17, fold=t=>.115+.025*Math.cos(t*32);
        quad(s,banner(x,hem(x),fold(x)),banner(xe,hem(xe),fold(xe)),banner(xe,1.54,fold(xe)),banner(x,1.54,fold(x)),(p,n,u,v)=>{
          const cross=((k+u)/8-.5)*2, rise=(v-.54)*2.5;
          const star=Math.abs(cross)**.65+Math.abs(rise)**.65 < .62;
          return brush(star||v<.079||Math.abs(cross)>.85?"rim":"silk",-.08)(p,n);
        });
      }
    }
  }
  skirt(s,1.61,1.12,1.68,1.27,"base");roofBand(s,1.65,1.27,.091,32);
  const cupola=(angle,t)=>radial(1.31*Math.cos(t),1.76+.77*Math.sin(t),angle);
  for(let j=0;j<9;j++)for(let i=0;i<32;i++){
    const t=j*Math.PI/18,q=(j+1)*Math.PI/18,a=i*tau/32,b=(i+1)*tau/32;
    quad(s,cupola(b,t),cupola(a,t),cupola(a,q),cupola(b,q),(p,n,u,v)=>{
      const column=i+1-u+(j%2)*.5;
      return roofPatina(p,n,j*32+Math.floor(column),frac(column),v);
    });
  }
  // Standing seams follow the dome; a folded copper apron casts a shadow at the eave.
  for(let i=0;i<8;i++){
    const a=i*tau/8;
    for(let j=0;j<8;j++)
      rod(s,cupola(a,j*Math.PI/18),cupola(a,(j+1)*Math.PI/18),.023,.021,"rim",6);
  }
  for(let i=0;i<32;i++){
    const a=i*tau/32,b=(i+1)*tau/32;
    const lip=(t,y)=>radial(1.296,y,t);
    quad(s,lip(a,1.73),lip((a+b)*.5,1.684),lip(b,1.73),lip((a+b)*.5,1.78),brush("copper",-.11));
  }
  rod(s,[0,2.50,0],[0,2.81,0],.17,.095,"gold",12);
  // Two curved boundaries meet at the horns. The section has a bevel on both sides.
  const U=[.848,0,-.53], N=[.53,0,.848], cy=3.77,R=1.13;
  const edge=(i,side,thickness,bevel=0)=>{
    const t=i/72, a=(55+250*t)*Math.PI/180;
    const end=Math.sin(t*Math.PI);
    const thick=(.34*end + .065*end**.4);
    const r=R-(side?thick:0) + bevel*end;
    return add(add(mul(U,Math.cos(a)*r),[0,cy+Math.sin(a)*r,0]),mul(N,thickness*end**.32));
  };
  for(let i=0;i<72;i++){
    for(const sign of [-1,1]){
      const p=(j,k)=>edge(j,k,sign*.105,k?.026:-.026);
      quad(s,p(i,0),p(i+1,0),p(i+1,1),p(i,1),(pt,n,u,v)=>{
        const crossLine=Math.abs(v-.50), tick=(i%4===0 && u<.32 && v>.60 && v<.82);
        const engraving=(crossLine>.333 && crossLine<.371)||tick;
        return brush(engraving?"dark":"gold",(i%9===0?-.06:0)+(sign<0?.09:-.02))(pt,n);
      });
      for(const k of [0,1])quad(s,p(i,k),edge(i,k,sign*.057),edge(i+1,k,sign*.057),p(i+1,k),brush("rim",.11));
    }
    for(const k of [0,1])quad(s,edge(i,k,-.057),edge(i+1,k,-.057),edge(i+1,k,.057),edge(i,k,.057),"gold");
    if(i%5===2)jewel(s,edge(i,1,-.112,.037),[.03,.035,.03],"rim");
  }
  const horn=edge(0,0,0), pendant=add(horn,[0,-.51,0]);
  rod(s,horn,pendant,.017,.017,"gold",6);jewel(s,pendant,[.13,.14,.13],"rim");
  return s.finish(sun);
}

export function makeCoralTree(){
  const s=new PaintedSurface(.025), rand=mulberry32(21649);
  const trunk=[[0,0,0],[.1,.56,.02],[-.04,1.05,.03],[.13,1.60,-.02],[.06,2.02,0]];
  for(let i=1;i<trunk.length;i++)rod(s,trunk[i-1],trunk[i],.21-.036*(i-1),.21-.036*i,"bark",9);
  for(let i=0;i<6;i++){
    const a=i*tau/6;
    rod(s,radial(.43,.025,a),trunk[1],.055,.13,"bark",6);
  }
  function foliage(c,scale){
    for(let j=0;j<35;j++){
      const a=rand()*tau,r=Math.sqrt(rand())*scale, leaf=add(c,[Math.cos(a)*r,(1-r/scale)*.20+rand()*.16,Math.sin(a)*r]);
      const t=rand()*tau, radialDir=[Math.cos(t),0,Math.sin(t)], side=[-Math.sin(t),0,Math.cos(t)];
      const reach=.16+rand()*.19,half=.065+rand()*.09, tip=add(leaf,add(mul(radialDir,reach),[0,-.075,0]));
      const mid=add(leaf,add(mul(radialDir,reach*.4),[0,.07,0]));
      const l=add(mid,mul(side,half)),rgt=add(mid,mul(side,-half)), key=j%3===1?"coral":"leaves";
      quad(s,leaf,l,tip,mid,brush(key,.03));quad(s,mid,tip,rgt,leaf,brush(key,-.08));
      if(j%3===1){
        for(let k=0;k<4;k++){
          const t=k*tau/4+rand()*.2, v=[Math.cos(t)*.088,.044,Math.sin(t)*.088], perpendicular=[-v[2]*.52,0,v[0]*.52];
          quad(s,mid,add(add(mid,mul(v,.58)),perpendicular),add(mid,v),add(add(mid,mul(v,.58)),mul(perpendicular,-1)),brush("coral",.14));
        }
      }
    }
  }
  for(let i=0;i<13;i++){
    const a=i*2.40+.3,reach=.42+rand()*.58, y=1.46+rand()*.78;
    const elbow=radial(reach*.55,y-.32,a),tip=radial(reach,y+.24,a+.22);
    rod(s,trunk[i%2+2],elbow,.095,.065,"bark",7);rod(s,elbow,tip,.066,.018,"bark",6);
    foliage(tip,.45+rand()*.22);
  }
  foliage([.1,2.54,.03],.54);
  return s.finish(sun);
}
export function makeMeteor(){
  const s=new PaintedSurface(.009);
  jewel(s,[0,0,0],[.15,.13,.125],(p,n)=>{
    const crack=Math.min(Math.abs(p[0]+p[1]*.64-.035),Math.abs(p[2]-.02-p[1]*.47),Math.abs(p[0]-.055+p[2]*.71));
    return crack<.009 ? [255,206,74] : brush("ember",-.06)(p,n);
  });
  return s.finish(sun);
}
export function makeArcade(){
  const s=new PaintedSurface(.032), half=1.42;
  const stone=(p,n)=>ashlar(p[0]/.35+.5*Math.floor(p[1]/.19),p[1]/.19,p,n,Math.floor(p[1]/.19));
  function wall(x,y,z){return [x,y,z];}
  for(const face of [-1,1]){
    const z=face*.13;
    for(let i=0;i<17;i++){
      const a=i*Math.PI/17,b=(i+1)*Math.PI/17;
      const edge=(t,r)=>wall(r*Math.cos(t),-.99+r*.71*Math.sin(t),z);
      quad(s,edge(a,1.22),edge(b,1.22),edge(b,1.41),edge(a,1.41),(p,n,u,v)=>
        brush(u<.10||v<.10?"base":"stone",i===8?.17:(i%3-1)*.09)(p,n));
      const l=edge(a,1.41),r=edge(b,1.41);
      quad(s,l,r,[r[0],.11,z],[l[0],.11,z],stone);
      quad(s,edge(a,1.22),edge(b,1.22),[...edge(b,1.22).slice(0,2),-z],[...edge(a,1.22).slice(0,2),-z],"base");
    }
    for(const sign of [-1,1]){
      const x=sign*(half-.10);
      quad(s,[x-.105,-2.15,z],[x+.105,-2.15,z],[x+.105,-.97,z],[x-.105,-.97,z],brush("base",.05));
    }
    quad(s,[-half,.1,z],[half,.1,z],[half,.21,z],[-half,.21,z],"base");
    quad(s,[-half,.21,z],[half,.21,z],[half,.255,z],[-half,.255,z],"gold");
  }
  quad(s,[-half,.26,-.17],[-half,.26,.17],[half,.26,.17],[half,.26,-.17],"stone");
  for(const sign of [-1,1]){
    const x=sign*(half-.11);
    rod(s,[x,-2.20,0],[x,-1.85,0],.215,.17,"base",8);
    rod(s,[x,-1.85,0],[x,-.93,0],.17,.14,"stone",8);
    rod(s,[x,-1.02,0],[x,-.91,0],.175,.17,"base",8);
  }
  return s;
}
export function makeWatchtower(){
  const s=new PaintedSurface(.019);
  skirt(s,-1.5,.81,.2,.78,limestone,8);skirt(s,.2,.76,1.82,.49,limestone,8);
  for(const y of [.22,.49,1.7,1.83])skirt(s,y,.79-.16*y,y+.08,.81-.16*y,"gold",8);
  // Deep lancet reveals on the four major faces. Each voussoir follows the point.
  for(let i=0;i<4;i++){
    const a=(i*2+.5)*tau/8, outward=radial(1,0,a), side=[-outward[2],0,outward[0]];
    const wall=(u,y,lift)=>add(mul(outward,(.76-(y-.2)*.27/1.62)*Math.cos(Math.PI/8)+lift),add(mul(side,u),[0,y,0]));
    const ring=[[-.122,.78],[.122,.78],[.122,1.14],[.077,1.24],[0,1.32],[-.077,1.24],[-.122,1.14]];
    for(let k=0;k<ring.length;k++){
      const [u,y]=ring[k],[v,h]=ring[(k+1)%ring.length], cy=1.03;
      quad(s,wall(u*1.43,cy+(y-cy)*1.20,.039),wall(v*1.43,cy+(h-cy)*1.20,.039),wall(v,h,.057),wall(u,y,.057),brush("stone",k%2?.13:-.18));
      quad(s,wall(u,y,.057),wall(v,h,.057),wall(v*.86,cy+(h-cy)*.91,.010),wall(u*.86,cy+(y-cy)*.91,.010),"dark");
      quad(s,wall(u*.86,cy+(y-cy)*.91,.010),wall(v*.86,cy+(h-cy)*.91,.010),wall(0,cy,.011),wall(0,cy,.011),brush("copper",-.28));
    }
    rod(s,wall(0,.796,.022),wall(0,1.274,.022),.017,.013,"gold",6);
    rod(s,wall(-.104,1.066,.021),wall(.104,1.066,.021),.013,.013,"dark",6);
  }
  for(let i=0;i<8;i++){
    const a=i*tau/8;
    rod(s,radial(.46,1.82,a),radial(.46,2.60,a),.062,.052,"stone",8);
    rod(s,radial(.46,2.04,a),radial(.46,2.04,a+tau/8),.025,.025,"gold",7);
  }
  rod(s,[0,2.15,0],[0,2.58,0],.026,.026,"dark",8);
  for(const [y,r,h,t] of [[2.02,.238,2.07,.236],[2.07,.231,2.13,.169],[2.13,.169,2.28,.127],[2.28,.127,2.32,.051]])
    skirt(s,y,r,h,t,brush("gold",-.10),16);
  jewel(s,[0,2.00,0],[.048,.059,.048],"dark");
  roofBand(s,2.56,.65,.077,12);
  skirt(s,2.65,.71,3.17,.06,(p,n)=>{
    const layer=(p[1]-2.65)/.127, col=Math.atan2(p[2],p[0])/tau*16+.5*(Math.floor(layer)%2);
    return roofPatina(p,n,Math.floor(col)+16*Math.floor(layer),frac(col),frac(layer));
  },12);
  rod(s,[0,3.12,0],[0,3.48,0],.048,.016,"rim",8);
  quad(s,[0,3.45,0],[.48,3.39,-.06],[.35,3.25,-.05],[0,3.27,0],"silk");
  return s.finish(sun);
}

// Both observatories retain their original tripod, rotation axis and usable footprint.
export function makeScopeFittings(sites){
  const s=new PaintedSurface(.014);
  for(const [x,z,turn] of sites){
    const center=[x,1.32,z], axis=unit([Math.sin(turn)*.87,.49,Math.cos(turn)*.87]);
    const right=[Math.cos(turn),0,-Math.sin(turn)],up=unit(cross(axis,right));
    const pt=(d,u=0,v=0)=>add(center,add(mul(axis,d),add(mul(right,u),mul(up,v))));
    const radialPoint=(d,r,a)=>pt(d,r*Math.cos(a),r*Math.sin(a));
    function tube(start,r0,end,r1,paint){
      for(let i=0;i<24;i++){
        const a=i*tau/24,b=(i+1)*tau/24;
        quad(s,radialPoint(start,r0,a),radialPoint(start,r0,b),radialPoint(end,r1,b),radialPoint(end,r1,a),paint);
      }
    }
    tube(-.77,.099,-.57,.151,"dark");
    tube(-.79,.103,-.75,.116,"rim");
    tube(-.61,.151,.58,.237,(p,n,u,v)=>brush(v<.04||v>.97?"dark":"copper",-.10)(p,n));
    for(const [d,r] of [[-.51,.176],[-.12,.204],[.14,.224],[.53,.25]]){
      tube(d,r-.010,d+.018,r+.008,"rim");
      tube(d+.018,r+.008,d+.068,r+.008,"gold");
      tube(d+.068,r+.008,d+.084,r-.006,"dark");
    }
    tube(.55,.248,.87,.252,brush("gold",-.11));
    tube(.87,.252,.957,.258,"copper");
    tube(.957,.258,.974,.239,"rim");
    tube(.974,.239,.966,.206,"dark");
    const glass=(p,n,u,v)=>{
      const delta=sub(p,pt(.963)), horizontal=dot(delta,right),vertical=dot(delta,up);
      const shine=vertical-.50*horizontal;
      return blend([25,57,79],[152,217,204],shine>.07 && shine<.105?.89:shine>.125?.46:.20+.13*vertical/.206);
    };
    tube(.966,.206,.956,0,glass);
    // An offset finder is clamped to the main barrel. The small gear marks altitude.
    for(const d of [-.21,.27]) rod(s,pt(d,0,.16),pt(d,0,.323),.032,.027,"gold",8);
    rod(s,pt(-.32,0,.322),pt(.40,0,.322),.042,.066,"dark",12);
    rod(s,pt(.37,0,.322),pt(.43,0,.322),.076,.073,"rim",12);
    const pin=pt(-.105,0,-.125);
    const clock=(u,v,side)=>add(pin,add(mul(right,side),add(mul(axis,u),mul(up,v))));
    for(let i=0;i<32;i++){
      const a=i*tau/32,b=(i+1)*tau/32;
      const at=(t,r,side)=>clock(Math.cos(t)*r,Math.sin(t)*r,side);
      quad(s,at(a,.166,.232),at(b,.166,.232),at(b,.120,.232),at(a,.120,.232),brush(i%4===0?"dark":"gold"));
      quad(s,at(a,.120,.232),at(b,.120,.232),pin,pin,"copper");
    }
    rod(s,clock(-.06,-.041,.234),clock(.085,.061,.234),.012,.014,"rim",6);
  }
  return s.finish(sun);
}
