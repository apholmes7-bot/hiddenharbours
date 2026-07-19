/* Hidden Harbours — skiff outboard motor layer (M2 bake recipe, ADR-0006 — PASS 2 for the 7 m
   skiffs; same overlay pattern as the punt motor in puntIsoRig.js). One big remote-steer four-stroke
   that fits BOTH consoleIsoRig.js (workboat) and sportSkiffIsoRig.js (sport) — the two hulls share
   the transom section, clamp height and pivot, so this layer pins to either by PIVOT.
   No tiller: steering is remote from the helm, the whole engine swivels on the clamp.
   Two builds off one recipe: 'work' — graphite cowl, teal band; 'sport' — white cowl, teal band,
   stainless prop flash. Cell 272x216, pivot (136,120) = hull origin; hull cells are 244x216 pivot
   (122,120) — ALIGN BY PIVOT, NOT CORNERS. Parts: 'upper' = bracket + cowl (always over the hull),
   'lower' = leg + plate + skeg + prop (under the hull for stern-away headings, MOTOR.behind=[3,4,5]).
   mx = lateral clamp offset in metres (0 = centre). SPORT TWIN FIT (optional upgrade): blit the
   same single-engine sheets twice per heading at MOTOR.mountOffset(dir, mx) for mx of
   MOTOR.mounts.dual = [-0.34, +0.34] — draw the far engine first within each layer; renderMotor
   also takes mx directly for bespoke bakes.
   renderMotor(dir,{steer:-1..1|steerDeg, tilt:0..40, mx, part, variant, elev, roll, pitch, heave})
   — pass the hull's rock(i) values so the layers never shear. clampPoint(dir,opts) -> motor-cell
   {x,y} of the clamp top (FX anchor). Exposes globalThis.SkiffMotor =
   { MOTOR, renderMotor, clampPoint, PAINT,TRIM,STEEL,MOTO,KEY }. */
(function (root) {
  const S = 32, DEG = Math.PI/180, DEFAULT_ELEV = 40;
  const MG = { W:272, H:216, cx:136, cy:120 };
  const MOTOR = { steerFrames:9, maxSteer:30, tiltMax:40, behind:[3,4,5],
    parts:['upper','lower'], variants:['work','sport'],
    W:MG.W, H:MG.H, pivot:{x:MG.cx, y:MG.cy},
    angle:(f)=>-30 + (60*f)/8,                 // sheet col f (0..8) -> steer degrees, col 4 dead ahead
    mounts:{ single:[0], dual:[-0.34, 0.34] },
    // twin fit: the bake is orthographic, so a lateral clamp shift is an EXACT per-heading screen
    // offset — blit the same single-engine sheet twice. (Under rock the <4deg roll error is sub-pixel.)
    mountOffset:(dir,mx,elev)=>{ const th=dir*Math.PI/4, e=(elev!=null?elev:DEFAULT_ELEV)*DEG;
      return { dx: mx*Math.cos(th)*S, dy: -mx*Math.sin(th)*Math.sin(e)*S }; } };
  const L = 7.0, YA = -L/2 - 0.07, ZT = 0.72;  // swivel axis (just aft of transom) / clamp height — matches both skiff rigs' MOUNT

  const PAINT = ['#5d6a70','#7e8c90','#a3b0b1','#c2cdca','#dde5df','#eef0ea','#f7f8f3'];
  const TRIM  = ['#0d3f3c','#14554e','#1c7367','#2ba39a','#49b8aa'];
  const STEEL = ['#3a4148','#565f66','#7a858c','#9fabb1','#c3ced2','#e6edee'];
  const MOTO  = ['#101317','#1d2127','#2b323a','#3d454e','#525c63','#6b767b','#8a9499'];
  const KEY   = '#101a19';
  const MATS = { paint:{ramp:PAINT,off:0}, trim:{ramp:TRIM,off:-1}, steel:{ramp:STEEL,off:0},
                 moto:{ramp:MOTO,off:0}, blk:{ramp:MOTO,off:-2} };
  const RINDEX = {}; [PAINT,TRIM,STEEL,MOTO].forEach(r=>r.forEach((c,i)=>{ RINDEX[c]={r,i}; }));
  const GAIN = 3.0, BIAS = 2.7;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  const v_sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]], v_add=(a,b)=>[a[0]+b[0],a[1]+b[1],a[2]+b[2]];
  const v_mul=(a,s)=>[a[0]*s,a[1]*s,a[2]*s], v_norm=(a)=>{const m=Math.hypot(a[0],a[1],a[2])||1;return [a[0]/m,a[1]/m,a[2]/m];};
  const v_cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
  // motor-local frame: origin on the swivel axis at keel height; +y forward (inboard), z absolute.
  // steer: about the vertical axis; tilt: about the lateral axis at clamp height (leg swings aft-up).
  function mxform(opts){
    const sd = opts.steerDeg!=null ? opts.steerDeg : Math.max(-1,Math.min(1,opts.steer||0))*MOTOR.maxSteer;
    const sa=sd*DEG, ta=Math.max(0,Math.min(MOTOR.tiltMax,opts.tilt||0))*DEG;
    const cs=Math.cos(sa), ss=Math.sin(sa), ct2=Math.cos(ta), st2=Math.sin(ta);
    const mx=opts.mx||0;
    return (p)=>{
      let [x,y,z]=p;
      const y1 = y*ct2 + (z-ZT)*st2, z1 = ZT - y*st2 + (z-ZT)*ct2;
      const x2 = x*cs - y1*ss, y2 = x*ss + y1*cs;
      return [mx+x2, YA+y2, z1];
    };
  }
  function box(c,h,mat,b,db,xf){
    const P=(sx,sy,sz)=>xf([c[0]+sx*h[0], c[1]+sy*h[1], c[2]+sz*h[2]]);
    const f=(v)=>({v,mat,b:b||0,db:db||0});
    return [
      f([P(-1,-1,1),P(1,-1,1),P(1,1,1),P(-1,1,1)]),
      f([P(-1,1,-1),P(1,1,-1),P(1,-1,-1),P(-1,-1,-1)]),
      f([P(-1,1,1),P(1,1,1),P(1,1,-1),P(-1,1,-1)]),
      f([P(1,-1,1),P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1)]),
      f([P(1,1,1),P(1,-1,1),P(1,-1,-1),P(1,1,-1)]),
      f([P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1)]),
    ];
  }
  // ---- cowling: lofted 8-sided shell (chamfered section, domed crown, aft-leaned) — big four-stroke ----
  function ringOf(s){
    const z=s[0], hx=s[1], hy=s[2], yc=s[3], kx=0.55, ky=0.60;
    return [[hx,-hy*ky],[hx,hy*ky],[hx*kx,hy],[-hx*kx,hy],[-hx,hy*ky],[-hx,-hy*ky],[-hx*kx,-hy],[hx*kx,-hy]]
      .map(([x,y])=>[x, y+yc, z]);
  }
  function cowlFaces(X, sport){
    const prof = [
      [0.680,0.195,0.230,-0.165],
      [0.735,0.205,0.240,-0.165],
      [0.775,0.196,0.226,-0.172],
      [0.840,0.192,0.220,-0.176],
      [0.925,0.186,0.212,-0.184],
      [1.020,0.146,0.166,-0.196],
      [1.065,0.080,0.094,-0.205],
    ];
    const bands = sport
      ? [['blk',-0.35],['blk',-0.15],['trim',0.25],['paint',0.15],['paint',0.30],['paint',0.44]]
      : [['blk',-0.35],['blk',-0.15],['trim',0.22],['moto',0.10],['moto',0.24],['moto',0.38]];
    const cap = sport ? ['paint',0.52] : ['moto',0.45];
    const rings = prof.map(s=>ringOf(s).map(X));
    const fs=[];
    for(let i=0;i<rings.length-1;i++){
      const lo=rings[i], hi=rings[i+1], [mat,b]=bands[i];
      for(let k=0;k<8;k++){ const k2=(k+1)%8; fs.push({v:[lo[k],lo[k2],hi[k2],hi[k]],mat,b,db:0}); }
    }
    fs.push({v:rings[rings.length-1].slice(),mat:cap[0],b:cap[1],db:0});                 // crown cap
    fs.push({v:rings[0].slice().reverse(),mat:'blk',b:-0.6,db:0});                        // pan underside
    const topZ = prof[prof.length-1][0];
    return fs.concat(box([0,-0.175,topZ+0.015],[0.06,0.045,0.015], sport?'paint':'moto', 0.25, -0.02, X)); // lift handle
  }
  function cowlDecals(X, sport){
    const fs=[], q=(x0,y0,z0,y1,z1,mat,b)=>{
      const v = x0<0 ? [[x0,y1,z0],[x0,y0,z0],[x0,y0,z1],[x0,y1,z1]] : [[x0,y0,z0],[x0,y1,z0],[x0,y1,z1],[x0,y0,z1]];
      fs.push({v:v.map(X),mat,b,db:-0.02});
    };
    if(sport){
      q( 0.190,-0.300,0.860,-0.085,0.915,'trim',0.35);   // teal side flash in the white band
      q(-0.190,-0.300,0.860,-0.085,0.915,'trim',0.35);
    } else {
      q( 0.190,-0.290,0.855,-0.105,0.905,'steel',0.45);  // brushed badge plate
      q(-0.190,-0.290,0.855,-0.105,0.905,'steel',0.40);
    }
    return fs;
  }
  function motorFaces(opts){
    const X=mxform(opts), mx=opts.mx||0, I=(p)=>[mx+p[0], YA+p[1], p[2]];
    const sport = opts.variant==='sport';
    const part=opts.part||'all', up=part!=='lower', lo=part!=='upper';
    let fs=[];
    if(up){
      fs=fs.concat(box([0,0.06,0.575],[0.11,0.115,0.075],'steel',-0.2,0,I));      // clamp bracket (fixed to transom)
      fs=fs.concat(box([0,0.045,0.66],[0.055,0.06,0.02],'blk',-0.35,0,I));        // tilt tube cap
      fs=fs.concat(cowlFaces(X,sport)).concat(cowlDecals(X,sport));
    }
    if(lo){
      const legM = sport?'blk':'moto';
      fs=fs.concat(box([0,-0.16,0.375],[0.062,0.075,0.345],legM,-0.3,0,X));       // mid leg
      fs=fs.concat(box([0,-0.16,0.095],[0.115,0.135,0.016],legM,-0.15,0,X));      // cavitation plate
      fs=fs.concat(box([0,-0.20,0.028],[0.020,0.095,0.055],legM,-0.4,0,X));       // skeg
      fs=fs.concat(box([0,-0.285,0.095],[0.015,0.055,0.058],'steel',0.5,0,X));    // stainless prop
      fs=fs.concat(box([0,-0.235,0.30],[0.016,0.005,0.04],legM,0.25,-0.02,X));    // anode strip
    }
    return fs;
  }

  // ---- rasterizer (shared recipe, motor-cell geometry) ----
  function normal(a,b,c){
    const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
    const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m];
  }
  function shadeOf(n, se, ce){
    return n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2];
  }
  function camBasis(opts){
    const dir=opts.dir||0, th=dir*Math.PI/4;
    const e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    const roll=(opts.roll||0)*DEG, pitch=(opts.pitch||0)*DEG;
    return { ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e),
      cr:Math.cos(roll), sr:Math.sin(roll), cq:Math.cos(pitch), sq:Math.sin(pitch), heave:(opts.heave||0) };
  }
  function projVert(x,y,z,B){
    const x1=x*B.cr+z*B.sr, z1=-x*B.sr+z*B.cr;
    const y2=y*B.cq - z1*B.sq, z2=y*B.sq + z1*B.cq;
    const xr=x1*B.ct - y2*B.stt, yr=x1*B.stt + y2*B.ct, zr=z2;
    return { xr,yr,zr, sx:MG.cx+xr*S, sy:MG.cy-(yr*B.se+zr*B.ce)*S - B.heave, d:(yr*B.ce-zr*B.se) };
  }
  function _paint(faces, opts){
    const PW=MG.W, PH=MG.H;
    const B=camBasis(opts);
    const zbuf=new Float32Array(PW*PH).fill(Infinity);
    const col=new Array(PW*PH).fill(null);
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
      let n=normal(rv[0],rv[1],rv[2]);
      let sh=shadeOf(n, B.se, B.ce); if(sh<0) sh=shadeOf([-n[0],-n[1],-n[2]], B.se, B.ce)*0.9;
      const fidx = sh*GAIN + BIAS + (f.b||0);
      const M = MATS[f.mat] || MATS.moto;
      for(let t=1;t+1<rv.length;t++) fillTri(rv[0],rv[t],rv[t+1]);
      function fillTri(a,b,c){
        const minX=Math.max(0,Math.floor(Math.min(a.sx,b.sx,c.sx)));
        const maxX=Math.min(PW-1,Math.ceil(Math.max(a.sx,b.sx,c.sx)));
        const minY=Math.max(0,Math.floor(Math.min(a.sy,b.sy,c.sy)));
        const maxY=Math.min(PH-1,Math.ceil(Math.max(a.sy,b.sy,c.sy)));
        const area=(b.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b.sy-a.sy);
        if(Math.abs(area)<1e-6) return;
        for(let y=minY;y<=maxY;y++) for(let x=minX;x<=maxX;x++){
          const px=x+0.5, py=y+0.5;
          let w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area;
          let w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area;
          let w2=1-w0-w1;
          if(w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const d=w0*a.d+w1*b.d+w2*c.d, deff=d-(f.db||0);
          const i=y*PW+x;
          if(deff<zbuf[i]){
            zbuf[i]=deff;
            let base=Math.floor(fidx);
            let idx=base+((fidx-base)>BAYER[x&3][y&3]?1:0)+M.off;
            col[i]=M.ramp[Math.max(0,Math.min(M.ramp.length-1,idx))];
          }
        }
      }
    }
    const out=new Array(PW*PH).fill(null);
    for(let i=0;i<PW*PH;i++) out[i]=col[i];
    for(let y=0;y<PH;y++) for(let x=0;x<PW;x++){
      const i=y*PW+x; if(out[i]) continue;
      let touch=false;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){
        const nx=x+dx, ny=y+dy;
        if(nx>=0&&nx<PW&&ny>=0&&ny<PH&&col[ny*PW+nx]){ touch=true; break; }
      }
      if(touch) out[i]=KEY;
    }
    return out;
  }
  function _toRGBA(out){
    const rgba=new Uint8ClampedArray(MG.W*MG.H*4);
    for(let i=0;i<MG.W*MG.H;i++){
      const c=out[i]; if(!c){ rgba[i*4+3]=0; continue; }
      rgba[i*4]=parseInt(c.slice(1,3),16); rgba[i*4+1]=parseInt(c.slice(3,5),16);
      rgba[i*4+2]=parseInt(c.slice(5,7),16); rgba[i*4+3]=255;
    }
    return rgba;
  }
  function renderMotor(dir, opts){
    opts = (typeof opts==='number') ? {elev:opts} : (opts||{});
    return _toRGBA(_paint(motorFaces(opts), Object.assign({}, opts, {dir})));
  }
  function clampPoint(dir, opts){   // clamp-top point in MOTOR-cell coords (FX anchor / alignment check)
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const p=projVert((opts.mx||0), YA+0.06, ZT, camBasis(opts));
    return { x:p.sx, y:p.sy };
  }

  root.SkiffMotor = { MOTOR, renderMotor, clampPoint, PAINT, TRIM, STEEL, MOTO, KEY };
})(typeof globalThis!=='undefined'?globalThis:window);
