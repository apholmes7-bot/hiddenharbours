/* Hidden Harbours — FISHING ROD tool rig (M2 bake recipe, ADR-0006 — TOOL KIT PASS 1).
   Tools are rigs too: the rod bakes on the SAME fixed 3/4 turntable as the fleet and the
   character (45deg steps, upper-left key, dither, 1px keyline, no AA, 32 px = 1 m) and pins to
   any character's baked handR anchor — the outboard-motor mount pattern (skiffMotorRig.js).
   Cell 112x112, pivot (56,72) = GRIP CENTRE, pinned every heading / pitch / bend.
   3 TIERS (length + thickness + fittings): cane 0.95 m / coaster 1.25 m / deepwater 1.55 m.
   The blank is plotted procedurally (unbroken 1-2px taper at any pitch — thin quads would gap);
   grip / reel seat / reel / crank are true 3D solids. Line + bobber + splash are RUNTIME FX,
   never baked — project() maps character-local 3D points to screen px for them.
   render(dir,{tier,pitch,yaw,bend,elev,rest}) — pitch/yaw in RADIANS from CharacterIso.tool;
   rest:'ground' (flat prop, 8 headings via dir, pivot = ground under the grip) | 'stored'
   (upright, butt on the ground — racks / stowed slots);
   bend 0..1 flexes the top 60% of the blank. tip(dir,opts) -> rod-cell px of the tip (line FX
   anchor); tipLocal(opts) -> the 3D local tip point. CAST = short/long distances (m), x castMul.
   Exposes globalThis.RodIso = { W,H,pivot,order,TIERS,CAST,behind,defaultElev,KEY,LINE,BOBBER,
   render,tip,tipLocal,project }. */
(function (root) {
  const S = 32, DEG = Math.PI / 180, DEFAULT_ELEV = 40;
  const W = 112, H = 112, cx = 56, cy = 72;
  const KEY = '#101a19', LINE = '#cfd4cc';
  const BOBBER = { red:'#cf3626', dark:'#7c1a15', white:'#eef0ea' };
  // fleet master ramps only — no invented colours
  const CORK  = ['#6b4a2c','#8c6a45','#a98352','#c2a06b','#d8c290'];
  const WOOD  = ['#33271b','#473627','#5e4630','#6b4f35','#8a6a48'];
  const NAVY  = ['#0e1526','#172644','#223764','#2f4c88','#4166ac'];
  const GRAPH = ['#101317','#1d2127','#2b323a','#3d454e','#525c63'];
  const RUST  = ['#4a100e','#7c1a15','#a8241b','#cf3626','#e2573c'];
  const STEEL = ['#3a4148','#565f66','#7a858c','#9fabb1','#c3ced2'];
  const TEAL  = ['#0d3f3c','#14554e','#1c7367','#2ba39a','#49b8aa'];
  const TIERS = {
    cane:  { label:'T1 · CANE',      len:0.95, thick:1, gripRad:0.036, blank:WOOD,  blankIdx:3, grip:WOOD,  gripOff:-1, reel:null,  knob:null,  wrap:null, castMul:1.00,
             blurb:'whittled alder pole — line tied straight to the tip' },
    coast: { label:'T2 · COASTER',   len:1.25, thick:1, gripRad:0.048, blank:NAVY,  blankIdx:3, grip:CORK,  gripOff:0,  reel:RUST,  knob:STEEL, wrap:null, castMul:1.25,
             blurb:'navy blank, cork grip, little red baitcaster' },
    deep:  { label:'T3 · DEEPWATER', len:1.55, thick:2, gripRad:0.052, blank:GRAPH, blankIdx:3, grip:CORK,  gripOff:0,  reel:STEEL, knob:RUST,  wrap:TEAL, castMul:1.50,
             blurb:'graphite blank, teal wraps, stainless reel, red drag knob' },
  };
  const ORDER = ['cane','coast','deep'];
  const CAST = { short:{dist:1.6, apex:0.85, ms:520}, long:{dist:2.9, apex:1.35, ms:800} }; // metres, x castMul

  const GAIN = 3.0, BIAS = 2.7, EDGE = 0.12;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  const ID=(p)=>p;
  const v_sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]], v_add=(a,b)=>[a[0]+b[0],a[1]+b[1],a[2]+b[2]];
  const v_mul=(a,s)=>[a[0]*s,a[1]*s,a[2]*s], v_norm=(a)=>{ const m=Math.hypot(a[0],a[1],a[2])||1; return [a[0]/m,a[1]/m,a[2]/m]; };
  const v_cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
  function box(c,h,mat,b,db,xf){
    xf=xf||ID;
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
  function tube(A,B2,rad,mat,b){
    const ax=v_norm(v_sub(B2,A)); let up=[0,0,1]; if(Math.abs(ax[2])>0.9) up=[0,1,0];
    const r=v_norm(v_cross(ax,up)), u=v_cross(r,ax);
    const ring=(P)=>[ v_add(v_add(P,v_mul(r,rad)),v_mul(u,rad)), v_add(v_add(P,v_mul(r,-rad)),v_mul(u,rad)),
                      v_add(v_add(P,v_mul(r,-rad)),v_mul(u,-rad)), v_add(v_add(P,v_mul(r,rad)),v_mul(u,-rad)) ];
    const r0=ring(A), r1=ring(B2), out=[];
    for(let k=0;k<4;k++){ const k2=(k+1)%4; out.push({v:[r0[k],r0[k2],r1[k2],r1[k]],mat,b:b||0,db:-0.05}); }
    out.push({v:r1.slice(),mat,b:b||0,db:-0.05});
    out.push({v:r0.slice().reverse(),mat,b:b||0,db:-0.05});
    return out;
  }

  // rod-local frame: origin = grip centre; +y = character forward. pitch 0 = level, 90deg = straight
  // up, >90deg = laid back over the shoulder. bend sags the outer blank toward the ground.
  function atFn(t, pitch, yaw, bend, zOff){
    const cp=Math.cos(pitch), sp=Math.sin(pitch), cyw=Math.cos(yaw), syw=Math.sin(yaw);
    const D=[syw*cp, cyw*cp, sp], z0=zOff||0;
    const at=(s)=>{
      const q=Math.max(0, s/t.len-0.40)/0.60;
      return [D[0]*s, D[1]*s, D[2]*s + z0 - bend*q*q*t.len*0.24];
    };
    at.D=D;
    return at;
  }
  function facesOf(t, at){
    const F=[]; const add=(fs)=>{ for(const f of fs) F.push(f); };
    add(box(at(-0.150),[0.021,0.021,0.021],'blk',-0.2,0));            // butt cap
    add(tube(at(-0.125), at(0.15), t.gripRad, 'grip', -0.05));        // grip
    if(t.reel){
      add(tube(at(0.15), at(0.21), t.gripRad*0.72, 'blk', -0.3));     // reel seat
      let n=v_cross(at.D,[0,0,1]); if(Math.hypot(n[0],n[1],n[2])<1e-4) n=[1,0,0]; n=v_norm(n);
      let dn=v_norm(v_cross(n,at.D)); if(dn[2]>0) dn=v_mul(dn,-1);    // hangs below the blank
      const c=v_add(at(0.185), v_mul(dn,0.058));
      add(box(c,[0.032,0.032,0.038],'reel',0.1,-0.02));               // reel body
      add(box(v_add(c,v_mul(n,0.045)),[0.011,0.011,0.011],'knob',0.5,-0.03)); // crank / drag knob
    }
    return F;
  }

  function makeMats(t){
    const MATS = { grip:{ramp:t.grip,off:t.gripOff||0}, blk:{ramp:GRAPH,off:-1},
                   reel:{ramp:t.reel||STEEL,off:0}, knob:{ramp:t.knob||RUST,off:0} };
    const RINDEX = {};
    [t.grip,t.blank,GRAPH,t.reel||STEEL,t.knob||RUST,t.wrap||TEAL].forEach(r=>r.forEach((c,i)=>{ RINDEX[c]={r,i}; }));
    return { MATS, RINDEX };
  }
  function camBasis(opts){
    // ADR-0006 fix: CW azimuth, kept in sync with characterIsoRig so the mount still aligns.
    const dir=opts.dir||0, th=-dir*Math.PI/4;
    const e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    return { ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e) };
  }
  function projVert(x,y,z,B){
    const xr=x*B.ct - y*B.stt, yr=x*B.stt + y*B.ct, zr=z;
    return { xr,yr,zr, sx:cx+xr*S, sy:cy-(yr*B.se+zr*B.ce)*S, d:(yr*B.ce-zr*B.se) };
  }
  function normal(a,b,c){
    const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
    const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m];
  }
  function shadeOf(n, se, ce){ return n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2]; }

  function _paint(t, o, pitch, yaw, bend, zOff){
    const B=camBasis(o);
    const {MATS,RINDEX}=makeMats(t);
    const at=atFn(t,pitch,yaw,bend,zOff);
    const faces=facesOf(t,at);
    const zbuf=new Float32Array(W*H).fill(Infinity);
    const col=new Array(W*H).fill(null);
    const dep=new Float32Array(W*H);
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
      let n=normal(rv[0],rv[1],rv[2]);
      let sh=shadeOf(n, B.se, B.ce); if(sh<0 && ((f.b||0)<=-1)) sh=shadeOf([-n[0],-n[1],-n[2]], B.se, B.ce)*0.9;
      const fidx = sh*GAIN + BIAS + (f.b||0);
      const M = MATS[f.mat] || MATS.blk;
      for(let tt=1;tt+1<rv.length;tt++) fillTri(rv[0],rv[tt],rv[tt+1]);
      function fillTri(a,b,c){
        const minX=Math.max(0,Math.floor(Math.min(a.sx,b.sx,c.sx)));
        const maxX=Math.min(W-1,Math.ceil(Math.max(a.sx,b.sx,c.sx)));
        const minY=Math.max(0,Math.floor(Math.min(a.sy,b.sy,c.sy)));
        const maxY=Math.min(H-1,Math.ceil(Math.max(a.sy,b.sy,c.sy)));
        const area=(b.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b.sy-a.sy);
        if(Math.abs(area)<1e-6) return;
        for(let y=minY;y<=maxY;y++) for(let x=minX;x<=maxX;x++){
          const px=x+0.5, py=y+0.5;
          let w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area;
          let w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area;
          let w2=1-w0-w1;
          if(w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const d=w0*a.d+w1*b.d+w2*c.d, deff=d-(f.db||0);
          const i=y*W+x;
          if(deff<zbuf[i]){
            zbuf[i]=deff; dep[i]=d;
            let base=Math.floor(fidx);
            let idx=base+((fidx-base)>BAYER[x&3][y&3]?1:0)+M.off;
            col[i]=M.ramp[Math.max(0,Math.min(M.ramp.length-1,idx))];
          }
        }
      }
    }
    // procedural blank — unbroken taper at any pitch
    const g0=0.16, steps=Math.ceil((t.len-g0)*S*2.2);
    const plot=(x,y,c,d)=>{ if(x<0||x>=W||y<0||y>=H||!c) return; const i=y*W+x; if(d-0.03<zbuf[i]){ zbuf[i]=d-0.03; dep[i]=d; col[i]=c; } };
    let px0=null, py0=null;
    for(let i=0;i<=steps;i++){
      const s=g0+(t.len-g0)*i/steps, frac=(s-g0)/(t.len-g0);
      const p=at(s), v=projVert(p[0],p[1],p[2],B);
      const x=Math.floor(v.sx), y=Math.floor(v.sy);
      let ramp=t.blank, bi=t.blankIdx;
      if(t.wrap && frac>0.06 && frac<0.16){ ramp=t.wrap; bi=3; }
      const idx=Math.max(0,Math.min(ramp.length-1, frac<0.30 ? bi-1 : (frac>0.86 ? bi+1 : bi)));
      plot(x,y,ramp[idx],v.d);
      if(t.thick===2 && frac<0.60 && px0!=null && (x!==px0||y!==py0)){
        if(Math.abs(x-px0)>=Math.abs(y-py0)) plot(x,y+1,ramp[Math.max(0,idx-2)],v.d);
        else plot(x+1,y,ramp[Math.max(0,idx-2)],v.d);
      }
      px0=x; py0=y;
    }
    const out=new Array(W*H).fill(null);
    for(let i=0;i<W*H;i++) out[i]=col[i];
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){
      const i=y*W+x; if(!col[i]) continue;
      for(const [dx,dy] of [[1,0],[0,1]]){
        const nx=x+dx, ny=y+dy; if(nx>=W||ny>=H) continue;
        const j=ny*W+nx; if(!col[j]) continue;
        if(Math.abs(dep[i]-dep[j])>EDGE){
          const far=dep[i]>dep[j]?i:j, e=RINDEX[col[far]];
          if(e && e.i>0) out[far]=e.r[Math.max(0,e.i-2)];
        }
      }
    }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){
      const i=y*W+x; if(out[i]) continue;
      let touch=false;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){
        const nx=x+dx, ny=y+dy;
        if(nx>=0&&nx<W&&ny>=0&&ny<H&&col[ny*W+nx]){ touch=true; break; }
      }
      if(touch) out[i]=KEY;
    }
    return out;
  }
  function _toRGBA(out){
    const rgba=new Uint8ClampedArray(W*H*4);
    for(let i=0;i<W*H;i++){
      const c=out[i]; if(!c){ rgba[i*4+3]=0; continue; }
      rgba[i*4]=parseInt(c.slice(1,3),16); rgba[i*4+1]=parseInt(c.slice(3,5),16);
      rgba[i*4+2]=parseInt(c.slice(5,7),16); rgba[i*4+3]=255;
    }
    return rgba;
  }

  function resolveR(dir, opts){
    opts=opts||{};
    const t=TIERS[opts.tier]||TIERS.coast, o=Object.assign({},opts,{dir});
    if(opts.rest==='ground')   // flat prop — rests on reel + grip; pivot = ground under the grip
      return { o, t, pitch:0, yaw:0, bend:0, zOff:t.reel?0.095:t.gripRad };
    if(opts.rest==='stored')   // upright, butt on the ground, near-vertical lean
      return { o, t, pitch:86*DEG, yaw:0, bend:0.03, zOff:0.16 };
    return { o, t,
             pitch:opts.pitch!=null?opts.pitch:56*DEG,
             yaw:opts.yaw!=null?opts.yaw:16*DEG,
             bend:opts.bend||0, zOff:0 };
  }
  function render(dir, opts){
    const {o,t,pitch,yaw,bend,zOff}=resolveR(dir,opts);
    return _toRGBA(_paint(t,o,pitch,yaw,bend,zOff));
  }
  function tipLocal(opts){
    const {t,pitch,yaw,bend,zOff}=resolveR(0,opts);
    return atFn(t,pitch,yaw,bend,zOff)(t.len);
  }
  function tip(dir, opts){
    const {o,t,pitch,yaw,bend,zOff}=resolveR(dir,opts);
    const p=atFn(t,pitch,yaw,bend,zOff)(t.len);
    const v=projVert(p[0],p[1],p[2],camBasis(o));
    return { x:v.sx, y:v.sy };
  }
  // character-local 3D point -> screen px OFFSET from the character's ground pivot (line/bobber FX)
  function project(dir, p, elev){
    const B=camBasis({dir, elev});
    const xr=p[0]*B.ct - p[1]*B.stt, yr=p[0]*B.stt + p[1]*B.ct;
    return { dx:xr*S, dy:-(yr*B.se+p[2]*B.ce)*S };
  }

  root.RodIso = { W, H, pivot:{x:cx,y:cy}, order:ORDER, TIERS, CAST, REST:['ground','stored'],
    behind:[7,0,1],   // rod layers under the sprite for the away facings (NW / N / NE)
    defaultElev:DEFAULT_ELEV, KEY, LINE, BOBBER,
    render, tip, tipLocal, project };
})(typeof globalThis!=='undefined'?globalThis:window);
