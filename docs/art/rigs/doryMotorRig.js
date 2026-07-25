/* Hidden Harbours — dory outboard motor layer (M2 bake recipe, ADR-0006 — PASS 2 for doryIsoRig.js
   build:'motor'; same overlay pattern as puntIsoRig.js MOTOR / skiffMotorRig.js). A LITTLE tiller
   two-stroke — the dory's only engine option, and the smallest in the fleet: the whole powerhead
   stands 0.27 m above the clamp against the punt outboard's 0.42, on a 0.23 x 0.27 m cowl (punt:
   0.30 x 0.35) with a 76 mm two-blade prop (punt: 96 mm) on a slim leg. Two-stroke read: the integral
   aluminium fuel tank IS the top cover (brass filler cap), wooden recoil-start T-grip across its aft
   edge, twist-grip throttle collar on a short tiller, single teal fleet band on the pan shoulder, no
   cowl paint (grey-black, rust freckles).

   It clamps to the dory's transom MOTOR BOARD, so the swivel axis registers to doryIsoRig MOUNT
   { x:0, y:-2.34, z:0.80 } — a full 0.24 m higher than the punt's clamp, hence the tucked-up leg and
   the near-horizontal tiller that reaches the stern bench (HELM).
   Cell 196x164, pivot (98,92) = hull origin; the dory hull cell is 160x156 pivot (80,88) —
   ALIGN BY PIVOT, NOT CORNERS. Parts: 'upper' = clamp + cowl + tank + tiller (ALWAYS over the hull),
   'lower' = leg + plate + skeg + prop (UNDER the hull for stern-away headings, MOTOR.behind=[3,4,5]).
   renderMotor(dir,{steer:-1..1|steerDeg, tilt:0..40, part, elev, roll, pitch, heave}) — pass the
   hull's rock(i) values so the layers never shear. tillerGrip(dir,opts) -> motor-cell {x,y} of the
   grip (operator's aft hand); clampPoint(dir,opts) -> motor-cell {x,y} of the clamp top (FX anchor).
   Exposes globalThis.DoryMotor = { MOTOR, renderMotor, tillerGrip, clampPoint, GRIP,
   MOTO,STEEL,TRIM,GOLD,WOOD,IRON,KEY }. */
(function (root) {
  const S = 32, DEG = Math.PI/180, DEFAULT_ELEV = 40;
  const MG = { W:196, H:164, cx:98, cy:92 };
  const MOTOR = { steerFrames:9, maxSteer:32, tiltMax:40, behind:[3,4,5],
    parts:['upper','lower'], variants:['stock'],
    W:MG.W, H:MG.H, pivot:{x:MG.cx, y:MG.cy}, hullPivot:{x:80, y:88},
    angle:(f)=>-32 + (64*f)/8 };               // sheet col f (0..8) -> steer degrees, col 4 dead ahead
  const YA = -2.38, ZT = 0.80;                 // swivel axis (just aft of the motor board) / clamp height

  const MOTO  = ['#101317','#1d2127','#2b323a','#3d454e','#525c63','#6b767b','#8a9499'];  // engine grey-blacks
  const STEEL = ['#3a4148','#565f66','#7a858c','#9fabb1','#c3ced2','#e6edee'];            // bare-alu tank
  const TRIM  = ['#0d3f3c','#14554e','#1c7367','#2ba39a','#49b8aa'];                      // fleet teal band
  const GOLD  = ['#7a5a1c','#a8842a','#e0b13a'];                                          // brass filler cap
  const WOOD  = ['#33271b','#473627','#5e4630','#6b4f35','#8a6a48','#9a7853','#a98352'];  // dory ramp — tiller + pull grip
  const IRON  = ['#20180f','#2a2014','#3a2c1c'];                                          // clamp hardware + rust
  const KEY   = '#1c140d';                                                                // dory keyline
  const MATS = { moto:{ramp:MOTO,off:0}, blk:{ramp:MOTO,off:-2}, steel:{ramp:STEEL,off:0}, alu:{ramp:STEEL,off:-3},
                 trim:{ramp:TRIM,off:-1}, gold:{ramp:GOLD,off:-1}, wood:{ramp:WOOD,off:0}, iron:{ramp:IRON,off:-2} };
  const RINDEX = {}; [MOTO,STEEL,TRIM,GOLD,WOOD,IRON].forEach(r=>r.forEach((c,i)=>{ RINDEX[c]={r,i}; }));
  const GAIN = 3.0, BIAS = 2.7;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  const v_sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]], v_add=(a,b)=>[a[0]+b[0],a[1]+b[1],a[2]+b[2]];
  const v_mul=(a,s)=>[a[0]*s,a[1]*s,a[2]*s], v_norm=(a)=>{const m=Math.hypot(a[0],a[1],a[2])||1;return [a[0]/m,a[1]/m,a[2]/m];};
  const v_cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
  // motor-local frame: origin on the swivel axis at keel datum; +y forward (inboard), z absolute.
  // steer: about the vertical axis (+steer = tiller to port, prop to starboard -> boat turns starboard).
  // tilt: about the lateral axis at clamp height (leg kicks aft-up, tiller drops inboard).
  function mxform(opts){
    const sd = opts.steerDeg!=null ? opts.steerDeg : Math.max(-1,Math.min(1,opts.steer||0))*MOTOR.maxSteer;
    const sa=sd*DEG, ta=Math.max(0,Math.min(MOTOR.tiltMax,opts.tilt||0))*DEG;
    const cs=Math.cos(sa), ss=Math.sin(sa), ct2=Math.cos(ta), st2=Math.sin(ta);
    return (p)=>{
      const [x,y,z]=p;
      const y1 = y*ct2 + (z-ZT)*st2, z1 = ZT - y*st2 + (z-ZT)*ct2;   // tilt
      const x2 = x*cs - y1*ss, y2 = x*ss + y1*cs;                      // steer
      return [x2, YA+y2, z1];
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
  function tube(A,B2,rad,mat,b,xf){
    const P0=xf(A), P1=xf(B2);
    const ax=v_norm(v_sub(P1,P0)); let up=[0,0,1]; if(Math.abs(ax[2])>0.9) up=[0,1,0];
    const r=v_norm(v_cross(ax,up)), u=v_cross(r,ax);
    const ring=(P)=>[ v_add(v_add(P,v_mul(r,rad)),v_mul(u,rad)), v_add(v_add(P,v_mul(r,-rad)),v_mul(u,rad)),
                      v_add(v_add(P,v_mul(r,-rad)),v_mul(u,-rad)), v_add(v_add(P,v_mul(r,rad)),v_mul(u,-rad)) ];
    const r0=ring(P0), r1=ring(P1), out=[];
    for(let k=0;k<4;k++){ const k2=(k+1)%4; out.push({v:[r0[k],r0[k2],r1[k2],r1[k]],mat,b:b||0,db:-0.15}); }
    out.push({v:r1.slice(),mat,b:(b||0)+0.25,db:-0.15});
    return out;
  }
  const GRIP = [0, 0.60, 0.808];   // tiller grip centre, motor-local

  // ---- cowling: lofted 8-sided shell (chamfered section, domed crown, aft-leaned) — small two-stroke ----
  function ringOf(s){
    const z=s[0], hx=s[1], hy=s[2], yc=s[3], kx=0.55, ky=0.60;
    return [[hx,-hy*ky],[hx,hy*ky],[hx*kx,hy],[-hx*kx,hy],[-hx,hy*ky],[-hx,-hy*ky],[-hx*kx,-hy],[hx*kx,-hy]]
      .map(([x,y])=>[x, y+yc, z]);
  }
  function cowlFaces(X){
    // squat little powerhead: the fuel tank IS the top cover, so the cowl itself is only 105 mm deep
    const prof = [
      [0.845,0.106,0.126,-0.106],   // pan bottom
      [0.880,0.114,0.134,-0.106],   // pan lip (widest)
      [0.908,0.110,0.130,-0.108],   // teal band top
      [0.950,0.106,0.124,-0.110],   // shoulder — tank lands here
    ];
    const bands = [['blk',-0.45],['trim',-0.15],['moto',0.10]];
    const rings = prof.map(s=>ringOf(s).map(X));
    const fs=[];
    for(let i=0;i<rings.length-1;i++){
      const lo=rings[i], hi=rings[i+1], [mat,b]=bands[i];
      for(let k=0;k<8;k++){ const k2=(k+1)%8; fs.push({v:[lo[k],lo[k2],hi[k2],hi[k]],mat,b,db:0}); }
    }
    fs.push({v:rings[rings.length-1].slice(),mat:'moto',b:0.32,db:0});          // shoulder cap (tank sits on it)
    fs.push({v:rings[0].slice().reverse(),mat:'blk',b:-0.75,db:0});             // pan underside
    return fs;
  }
  function cowlDecals(X){
    const fs=[], q=(x0,y0,z0,y1,z1,mat,b)=>{   // proud side quad at constant x, outward-facing
      const v = x0<0 ? [[x0,y1,z0],[x0,y0,z0],[x0,y0,z1],[x0,y1,z1]] : [[x0,y0,z0],[x0,y1,z0],[x0,y1,z1],[x0,y0,z1]];
      fs.push({v:v.map(X),mat,b,db:-0.02});
    };
    q( 0.109,-0.180,0.916,-0.098,0.940,'moto',1.4);   // scuffed paint on the cowl sides
    q(-0.109,-0.146,0.914,-0.062,0.938,'moto',1.2);
    q( 0.115,-0.176,0.852,-0.132,0.872,'iron',0.4);   // pan rust freckles
    q(-0.115,-0.100,0.850,-0.054,0.870,'iron',0.3);
    return fs;
  }
  function motorFaces(opts){
    const X=mxform(opts), I=(p)=>[p[0], YA+p[1], p[2]];
    const part=opts.part||'all', up=part!=='lower', lo=part!=='upper';
    let fs=[];
    if(up){
      // clamp assembly — fixed to the transom board, does NOT swivel (I frame)
      fs=fs.concat(box([0,0.075,0.735],[0.078,0.030,0.070],'iron',-0.15,0,I));   // clamp plate hugging the board's aft face
      fs=fs.concat(box([0,0.055,0.745],[0.072,0.055,0.052],'iron',-0.40,0,I));   // yoke body
      for(const sx of [-1,1]) fs=fs.concat(box([sx*0.052,0.104,0.706],[0.013,0.014,0.030],'iron',0.15,-0.02,I));  // clamp screws
      fs=fs.concat(box([0,0.010,0.812],[0.050,0.050,0.045],'blk',-0.20,0,X));    // swivel housing (bridges yoke to powerhead)
      fs=fs.concat(cowlFaces(X)).concat(cowlDecals(X));
      fs=fs.concat(box([0,-0.110,0.995],[0.096,0.112,0.045],'alu',-0.35,0,X));  // integral alu fuel tank = the top cover
      fs=fs.concat(box([0,-0.098,1.048],[0.022,0.022,0.010],'gold',0.15,-0.02,X));// brass filler cap, on top
      fs=fs.concat(box([0,-0.208,1.058],[0.038,0.013,0.012],'wood',0.35,-0.02,X));// recoil-start T-grip
      fs=fs.concat(box([-0.114,0.016,0.922],[0.012,0.028,0.011],'iron',0.30,-0.02,X)); // gear lever
      fs=fs.concat(tube([0,0.020,0.848],[0,0.470,0.818],0.026,'moto',0.25,X));   // tiller arm
      fs=fs.concat(tube([0,0.440,0.820],[0,0.505,0.815],0.042,'blk',-0.10,X));   // twist-grip throttle collar
      fs=fs.concat(tube([0,0.505,0.815],[0,0.690,0.801],0.046,'blk',0.20,X));    // rubber tiller grip
    }
    if(lo){
      fs=fs.concat(box([0,-0.108,0.660],[0.040,0.048,0.150],'moto',-0.25,0,X));  // upper leg fairing
      fs=fs.concat(box([0,-0.115,0.380],[0.035,0.042,0.160],'moto',-0.30,0,X));  // mid leg (shaft)
      fs=fs.concat(box([0,-0.152,0.330],[0.012,0.004,0.030],'iron',0.35,-0.02,X));// tell-tale / rust streak
      fs=fs.concat(box([0,-0.118,0.185],[0.072,0.088,0.011],'moto',-0.12,0,X));  // cavitation plate
      fs=fs.concat(box([0,-0.128,0.140],[0.030,0.072,0.038],'moto',-0.30,0,X));  // gearcase bullet
      fs=fs.concat(box([0,-0.155,0.082],[0.012,0.062,0.038],'moto',-0.45,0,X));  // skeg
      fs=fs.concat(box([0,-0.208,0.140],[0.010,0.036,0.040],'alu',-0.10,0,X));  // bare-alu two-blade prop
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
    const PW=MG.W, PH=MG.H, B=camBasis(opts);
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
          const w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area;
          const w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area;
          const w2=1-w0-w1;
          if(w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const d=w0*a.d+w1*b.d+w2*c.d, deff=d-(f.db||0);
          const i=y*PW+x;
          if(deff<zbuf[i]){
            zbuf[i]=deff;
            const base=Math.floor(fidx);
            const idx=base+((fidx-base)>BAYER[x&3][y&3]?1:0)+M.off;
            col[i]=M.ramp[Math.max(0,Math.min(M.ramp.length-1,idx))];
          }
        }
      }
    }
    const out=col.slice();
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
  function tillerGrip(dir, opts){   // grip centre in MOTOR-cell coords, for the operator's aft hand
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const g=mxform(opts)(GRIP), p=projVert(g[0],g[1],g[2],camBasis(opts));
    return { x:p.sx, y:p.sy };
  }
  function clampPoint(dir, opts){   // clamp-top point in MOTOR-cell coords (alignment check / FX anchor)
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const p=projVert(0, YA+0.055, ZT, camBasis(opts));
    return { x:p.sx, y:p.sy };
  }

  root.DoryMotor = { MOTOR, renderMotor, tillerGrip, clampPoint, GRIP, YA, ZT,
    MOTO, STEEL, TRIM, GOLD, WOOD, IRON, KEY };
})(typeof globalThis!=='undefined'?globalThis:window);
