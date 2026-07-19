/* Hidden Harbours — BUCKETS + FISH TRAY carry-prop rig (M2 bake recipe, ADR-0006 — TOOL KIT PASS 3).
   Same fixed 3/4 turntable as the fleet / character / rod / shovel (45deg steps, upper-left key,
   ordered dither, 1px keyline, NO AA, 32 px = 1 m). Cell 48x52.

   THREE TIERS:  1 = galvanised STEEL PAIL (wire bail, wood roller grip, one hand)
                 2 = faded-blue PLASTIC PAIL (strap bail, moulded grip, one hand)
                 3 = FISH TRAY (shallow tote, two-hand carry by the near-rim grip slots)
   CATCHES: 'fish' (silver herring) | 'shell' (blue mussels + the odd clam) | 'crust'
   (banded keepers + rust crab). FILLS: empty / few / half / full / brim — monotonic read.

   TWO MODES, TWO PIVOTS:
   - CARRY  pivot (24,12) = the GRIP: pail bail apex -> pin to CharacterIso.carry() handL/handR;
     tray near-rim centre -> pin to carry().mid. opts.swing (RADIANS, from carry()) is the
     pendulum: rotation about the grip in the character's fore-aft plane.
   - REST   pivot (24,42) = base centre, PLACEABLE ON BOATS: the boat rig's mount anchor carries
     ALL translation (position + heave); this bake carries only the matching deck rock via
     opts.roll / opts.pitch (degrees) — the FishTubIso split, so the layers never shear.
   Exposes globalThis.BucketIso = { W,H,pivotCarry,pivotRest,TIERS,FILLS,CATCHES,ramps,KEY,
   defaultElev,render(dir,opts) }  opts = {tier,fill,catch,rest,swing,roll,pitch,elev}. */
(function (root) {
  const S = 32, DEG = Math.PI / 180, DEFAULT_ELEV = 40;
  const W = 48, H = 52;
  const PXC = { x:24, y:12 }, PXR = { x:24, y:42 };
  const KEY = '#101a19';
  // fleet master ramps + the FishTray tote plastic — no invented hues
  const STEEL = ['#3a4148','#565f66','#7a858c','#9fabb1','#c3ced2'];
  const PLAST = ['#33474f','#476069','#5f7d8a','#86a3ad','#a9c2c9'];
  const GRAPH = ['#101317','#1d2127','#2b323a','#3d454e','#525c63'];
  const WOOD  = ['#33271b','#473627','#5e4630','#6b4f35','#8a6a48'];
  const FISH  = ['#233642','#35505e','#4f707c','#7b98a0','#b7ccc9'];
  const SHELLF= ['#10151a','#1b2430','#293747','#3d5064'];
  const CRUST = ['#152a25','#213f37','#345a4e','#517d6c'];
  const BAND  = ['#8a3c28','#c85a3f','#e07a5a'];
  const CREAM = ['#c9b083','#e7d8b0'];
  const SHEEN = ['#c2d6da','#e2eff1'];
  const MATS = { steel:{ramp:STEEL,off:0}, plast:{ramp:PLAST,off:0}, grip:{ramp:GRAPH,off:0},
    wood:{ramp:WOOD,off:0}, fish:{ramp:FISH,off:0}, shell:{ramp:SHELLF,off:0}, crust:{ramp:CRUST,off:0},
    band:{ramp:BAND,off:-1}, cream:{ramp:CREAM,off:-1}, sheen:{ramp:SHEEN,off:-1} };
  const RINDEX = {}; [STEEL,PLAST,GRAPH,WOOD,FISH,SHELLF,CRUST,BAND,CREAM,SHEEN].forEach(r=>r.forEach((c,i)=>{ RINDEX[c]={r,i}; }));
  const GAIN = 3.0, BIAS = 2.7, EDGE = 0.13;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  const TIERS = {
    1:{ id:'pail',  label:'STEEL PAIL',   mat:'steel', rB:0.115, rT:0.150, h:0.28, bail:0.13, grip:'wood' },
    2:{ id:'tote',  label:'PLASTIC PAIL', mat:'plast', rB:0.150, rT:0.200, h:0.38, bail:0.15, grip:'grip' },
    3:{ id:'tray',  label:'FISH TRAY',    mat:'plast', LX:0.31,  LY:0.22,  h:0.18 },
  };
  const FILLS = ['empty','few','half','full','brim'];
  const FILLZ = { few:0.34, half:0.58, full:0.84, brim:0.96 };
  const CATCHES = ['fish','shell','crust'];

  // ---- solids ----
  const C8=0.414;
  const oct=(r,z)=>[[r,-r*C8,z],[r,r*C8,z],[r*C8,r,z],[-r*C8,r,z],[-r,r*C8,z],[-r,-r*C8,z],[-r*C8,-r,z],[r*C8,-r,z]];
  const JIT=[0.14,-0.10,0.08,-0.04,0.12,-0.08,0.05,-0.12];
  const v_sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]], v_add=(a,b)=>[a[0]+b[0],a[1]+b[1],a[2]+b[2]];
  const v_mul=(a,s)=>[a[0]*s,a[1]*s,a[2]*s], v_norm=(a)=>{const m=Math.hypot(a[0],a[1],a[2])||1;return [a[0]/m,a[1]/m,a[2]/m];};
  const v_cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
  function tube(A,B2,rad,mat,b){
    const ax=v_norm(v_sub(B2,A)); let up=[0,0,1]; if(Math.abs(ax[2])>0.9) up=[0,1,0];
    const r=v_norm(v_cross(ax,up)), u=v_cross(r,ax);
    const ring=(P)=>[v_add(v_add(P,v_mul(r,rad)),v_mul(u,rad)),v_add(v_add(P,v_mul(r,-rad)),v_mul(u,rad)),
                     v_add(v_add(P,v_mul(r,-rad)),v_mul(u,-rad)),v_add(v_add(P,v_mul(r,rad)),v_mul(u,-rad))];
    const r0=ring(A), r1=ring(B2), out=[];
    for(let k=0;k<4;k++){ const k2=(k+1)%4; out.push({v:[r0[k],r0[k2],r1[k2],r1[k]],mat,b:b||0,db:-0.05}); }
    out.push({v:r1.slice(),mat,b:b||0,db:-0.05});
    out.push({v:r0.slice().reverse(),mat,b:b||0,db:-0.05});
    return out;
  }
  function box(c,h,mat,b,db){
    const P=(sx,sy,sz)=>[c[0]+sx*h[0],c[1]+sy*h[1],c[2]+sz*h[2]];
    const f=(v)=>({v,mat,b:b||0,db:db||0});
    return [ f([P(-1,-1,1),P(1,-1,1),P(1,1,1),P(-1,1,1)]), f([P(-1,1,-1),P(1,1,-1),P(1,-1,-1),P(-1,-1,-1)]),
      f([P(-1,1,1),P(1,1,1),P(1,1,-1),P(-1,1,-1)]), f([P(1,-1,1),P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1)]),
      f([P(1,1,1),P(1,-1,1),P(1,-1,-1),P(1,1,-1)]), f([P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1)]) ];
  }
  const pip=(fs,x,y,z,w,h,mat,b)=>fs.push({v:[[x-w,y-h,z],[x+w,y-h,z],[x+w,y+h,z],[x-w,y+h,z]],mat,b,db:-0.02});

  // ---- contents (shared item kit; sizes scale with the vessel) --------------
  const SLOTS=[[-0.45,-0.30,0],[0.40,0.25,1],[-0.12,0.45,0],[0.32,-0.45,1],[-0.52,0.22,1]];
  function items(fs, kind, z, rc, n, bTop){
    const sc=Math.max(0.75, Math.min(1.25, rc/0.13));
    for(let i=0;i<n;i++){
      const [ux,uy,rot]=SLOTS[i%SLOTS.length];
      const x=ux*rc, y=uy*rc, bb=(bTop?0.30:0.18)-(i%3)*0.16;
      if(kind==='fish'){
        const l=0.058*sc, w=0.020*sc;
        fs.push(...box([x,y,z+0.012],[rot?w:l, rot?l:w, 0.012],'fish',bb,0));
        pip(fs, x+(rot?0:l*0.55), y+(rot?l*0.55:0), z+0.026, 0.012*sc, 0.008*sc, 'sheen', 0.2);   // belly glint at the head
        pip(fs, x-(rot?0:l*0.85), y-(rot?l*0.85:0), z+0.026, 0.008*sc, 0.008*sc, 'fish', -0.9);   // dark tail fork
      } else if(kind==='shell'){
        fs.push(...box([x,y,z+0.010],[0.026*sc,0.020*sc,0.011],'shell',bb,0));
        pip(fs, x, y, z+0.023, 0.008*sc, 0.006*sc, 'sheen', -0.1);                                // nacre lip glint
        if(i===1) fs.push(...box([x*0.4,y*0.4-0.02*sc,z+0.012],[0.020*sc,0.017*sc,0.010],'cream',bb,0));  // the odd clam
      } else {
        fs.push(...box([x,y,z+0.012],[0.042*sc,0.030*sc,0.013],'crust',bb,0));
        pip(fs, x+0.02*sc, y, z+0.027, 0.009*sc, 0.007*sc, 'band', 0.35);                          // claw band pip
        if(i===2) pip(fs, x-0.02*sc, y+0.01*sc, z+0.027, 0.007*sc, 0.006*sc, 'cream', 0);          // claw-tip cream
      }
    }
  }
  function capMatOf(kind){ return kind==='fish'?'fish':kind==='shell'?'shell':'crust'; }

  // ---- tier 1 & 2: round pail (rest frame — base centre at origin) ----------
  function pailFaces(T, fill, kind){
    const fs=[]; const rOf=(z)=>T.rB+(T.rT-T.rB)*(z/T.h);
    const bands=[[0,0.045,-0.15,0],[0.045,T.h*0.46,0,1],[T.h*0.46,T.h*0.52,-0.30,0],
                 [T.h*0.52,T.h-0.030,0,1],[T.h-0.030,T.h,0.35,0]];   // base curl / body / moulded ring / body / rolled rim
    const jam=T.mat==='steel'?0.5:0.22;
    for(const [z0,z1,bb,jt] of bands){
      const lo=oct(rOf(z0),z0), hi=oct(rOf(z1),z1);
      for(let k=0;k<8;k++){ const k2=(k+1)%8; fs.push({v:[lo[k],lo[k2],hi[k2],hi[k]],mat:T.mat,b:bb+(jt?JIT[k]*jam:0),db:0}); }
    }
    const rIn=T.rT-0.016;
    const rim=oct(T.rT,T.h), rin=oct(rIn,T.h);
    for(let k=0;k<8;k++){ const k2=(k+1)%8; fs.push({v:[rim[k],rim[k2],rin[k2],rin[k]],mat:T.mat,b:0.55,db:0}); }
    fs.push({v:oct(T.rB,0).reverse(),mat:T.mat,b:-0.6,db:0});
    for(const s of [-1,1])   // bail ears
      fs.push(...box([s*(T.rT+0.006),0,T.h-0.032],[0.013,0.019,0.023], T.mat==='steel'?'steel':'grip', 0.35, -0.02));
    // interior wall down to the fill line
    const zf = fill==='empty' ? 0.030 : T.h*FILLZ[fill];
    const bot=oct(Math.max(0.04,rOf(zf)-0.016), zf), tin=oct(rIn,T.h);
    for(let k=0;k<8;k++){ const k2=(k+1)%8; fs.push({v:[tin[k],tin[k2],bot[k2],bot[k]],mat:T.mat,b:-1.25,db:0}); }
    // fill cap + contents
    const rc=rOf(zf)-0.028;
    fs.push({v:oct(Math.max(0.035,rc+0.012), zf+0.002),mat:fill==='empty'?T.mat:capMatOf(kind),b:fill==='empty'?-1.0:0.05,db:0});
    if(fill==='empty'){ pip(fs,-rc*0.35,-rc*0.35,zf+0.005,0.024,0.012,'sheen',-0.35); }
    else {
      items(fs, kind, zf, rc, fill==='few'?1 : fill==='half'?2 : 3, false);
      if(fill==='brim'){   // heap breaks the rim silhouette
        fs.push(...box([0,0.008,T.h+0.014],[rc*0.72,rc*0.58,0.022],capMatOf(kind),0.22,0));
        items(fs, kind, T.h+0.036, rc*0.78, 2, true);
      }
    }
    // grip roller / sleeve at the bail apex (the pivot the hand pins to)
    const az=T.h+T.bail;
    fs.push(...tube([-0.045,0,az],[0.045,0,az], T.mat==='steel'?0.017:0.021, T.grip, 0.15));
    return fs;
  }
  // bail arc, sampled for the procedural plot (thin tubes would gap)
  function bailArc(T){
    const pts=[], R=T.rT+0.008, z0=T.h-0.028, za=T.h+T.bail, N=44;
    for(let i=0;i<=N;i++){ const th=Math.PI*i/N; pts.push([R*Math.cos(th), 0, z0+(za-z0)*Math.sin(th)]); }
    return pts;
  }

  // ---- tier 3: fish tray (rest frame — base centre at origin) ---------------
  function trayFaces(T, fill, kind){
    const fs=[], LX=T.LX, LY=T.LY, h=T.h, t=0.020;
    fs.push(...box([0,0,0.014],[LX-0.004,LY-0.004,0.014],'plast',-0.55,0));            // base slab (top = interior floor, in shadow)
    for(const s of [-1,1]){
      fs.push(...box([s*(LX-t),0,h/2],[t,LY,h/2],'plast', JIT[s+2]*0.2, 0));            // end walls
      fs.push(...box([0,s*(LY-t),h/2],[LX-2*t,t,h/2],'plast', s>0?-0.05:0.10, 0));      // long walls
    }
    for(const s of [-1,1]){                                                             // bright rim caps
      fs.push(...box([s*(LX-t),0,h-0.007],[t+0.003,LY+0.003,0.009],'plast',0.50,-0.01));
      fs.push(...box([0,s*(LY-t),h-0.007],[LX+0.003,t+0.003,0.009],'plast',0.50,-0.01));
    }
    for(const sy of [-1,1]) for(const sx of [-1,1])                                     // moulded grip slots, ±0.24
      fs.push(...box([sx*0.24, sy*(LY-t), h-0.028],[0.030,t+0.005,0.011],'grip',-0.6,-0.03));
    const zf = fill==='empty' ? 0.032 : h*FILLZ[fill];
    const ix=LX-2*t-0.006, iy=LY-2*t-0.006;
    fs.push({v:[[-ix,-iy,zf],[ix,-iy,zf],[ix,iy,zf],[-ix,iy,zf]],mat:fill==='empty'?'plast':capMatOf(kind),b:fill==='empty'?-0.95:0.05,db:0});
    if(fill==='empty'){ pip(fs,-ix*0.45,-iy*0.35,zf+0.005,0.045,0.018,'sheen',-0.35); }
    else {
      const R=[[-0.55,-0.38,0],[0.38,0.32,0],[-0.10,0.42,1],[0.55,-0.28,1],[-0.52,0.28,0],[0.08,-0.10,1]];
      const put=(list,z,scl,n,bT)=>{ for(let i=0;i<n;i++){ const [ux,uy,rot]=list[i%list.length];
        itemsRect(fs,kind,ux*ix*scl,uy*iy*scl,z,rot,(bT?0.30:0.18)-(i%3)*0.16); } };
      put(R, zf, 1, fill==='few'?2 : fill==='half'?4 : 5, false);
      if(fill==='brim'){ fs.push(...box([0,0,h+0.012],[ix*0.78,iy*0.66,0.020],capMatOf(kind),0.22,0)); put(R,h+0.032,0.7,3,true); }
    }
    return fs;
  }
  function itemsRect(fs, kind, x, y, z, rot, bb){
    if(kind==='fish'){
      const l=0.085, w=0.024;
      fs.push(...box([x,y,z+0.012],[rot?w:l, rot?l:w, 0.012],'fish',bb,0));
      pip(fs, x+(rot?0:l*0.55), y+(rot?l*0.55:0), z+0.026, 0.014, 0.009, 'sheen', 0.2);
      pip(fs, x-(rot?0:l*0.85), y-(rot?l*0.85:0), z+0.026, 0.010, 0.009, 'fish', -0.9);
    } else if(kind==='shell'){
      fs.push(...box([x,y,z+0.010],[0.030,0.023,0.011],'shell',bb,0));
      pip(fs, x, y, z+0.023, 0.009, 0.007, 'sheen', -0.1);
      if(rot) fs.push(...box([x+0.045,y-0.030,z+0.011],[0.022,0.018,0.010],'cream',bb-0.1,0));
    } else {
      fs.push(...box([x,y,z+0.012],[0.050,0.036,0.013],'crust',bb,0));
      pip(fs, x+0.024, y, z+0.028, 0.010, 0.008, 'band', 0.35);
      if(rot) pip(fs, x-0.024, y+0.012, z+0.028, 0.008, 0.007, 'cream', 0);
    }
  }

  const FCACHE={};
  function facesFor(tier, fill, kind){
    const key=tier+'|'+fill+'|'+kind;
    if(!FCACHE[key]) FCACHE[key] = tier===3 ? trayFaces(TIERS[3],fill,kind) : pailFaces(TIERS[tier],fill,kind);
    return FCACHE[key];
  }

  // ---- rasterizer (shared recipe; offset applied BEFORE roll/pitch so the
  //      rotation is about the mode pivot — grip in carry, base in rest) ------
  function camBasis(o){
    const dir=o.dir||0, th=dir*Math.PI/4;
    const e=(o.elev!=null?o.elev:DEFAULT_ELEV)*DEG;
    return { ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e),
      cr:Math.cos(o._roll||0), sr:Math.sin(o._roll||0), cq:Math.cos(o._pitch||0), sq:Math.sin(o._pitch||0),
      cx:o._cx, cy:o._cy, oy:o._oy||0, oz:o._oz||0 };
  }
  function projVert(x,y,z,B){
    y+=B.oy; z+=B.oz;
    const x1=x*B.cr+z*B.sr, z1=-x*B.sr+z*B.cr;
    const y2=y*B.cq - z1*B.sq, z2=y*B.sq + z1*B.cq;
    const xr=x1*B.ct - y2*B.stt, yr=x1*B.stt + y2*B.ct, zr=z2;
    return { xr,yr,zr, sx:B.cx+xr*S, sy:B.cy-(yr*B.se+zr*B.ce)*S, d:(yr*B.ce-zr*B.se) };
  }
  function normal(a,b,c){
    const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
    const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m];
  }
  function shadeOf(n,se,ce){ return n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2]; }
  function _paint(faces, o, bail, bailRamp){
    const B=camBasis(o);
    const zbuf=new Float32Array(W*H).fill(Infinity);
    const col=new Array(W*H).fill(null);
    const dep=new Float32Array(W*H);
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
      let n=normal(rv[0],rv[1],rv[2]);
      let sh=shadeOf(n,B.se,B.ce); if(sh<0 && ((f.b||0)<=-1)) sh=shadeOf([-n[0],-n[1],-n[2]],B.se,B.ce)*0.9;
      const fidx = sh*GAIN + BIAS + (f.b||0);
      const M = MATS[f.mat] || MATS.plast;
      for(let t=1;t+1<rv.length;t++) fillTri(rv[0],rv[t],rv[t+1]);
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
    if(bail){   // procedural bail plot — unbroken at any swing angle
      const plot=(x,y,c,d)=>{ if(x<0||x>=W||y<0||y>=H||!c) return; const i=y*W+x; if(d-0.03<zbuf[i]){ zbuf[i]=d-0.03; dep[i]=d; col[i]=c; } };
      let px0=null, py0=null;
      for(let i=0;i<bail.length;i++){
        const u=i/(bail.length-1);
        const p=bail[i], v=projVert(p[0],p[1],p[2],B);
        const x=Math.floor(v.sx), y=Math.floor(v.sy);
        const top=Math.sin(Math.PI*u);
        const idx=top>0.72?3:(top>0.3?2:1);
        plot(x,y,bailRamp[idx],v.d);
        if(px0!=null && (x!==px0||y!==py0)){
          if(Math.abs(x-px0)>=Math.abs(y-py0)) plot(x,y+1,bailRamp[Math.max(0,idx-1)],v.d);
          else plot(x+1,y,bailRamp[Math.max(0,idx-1)],v.d);
        }
        px0=x; py0=y;
      }
    }
    const out=new Array(W*H).fill(null);
    for(let i=0;i<W*H;i++) out[i]=col[i];
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){   // depth-edge separation
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
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){   // external keyline
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

  function render(dir, opts){
    opts=opts||{};
    const tier=TIERS[opts.tier]?+opts.tier:1, T=TIERS[tier];
    const fill=FILLS.includes(opts.fill)?opts.fill:(typeof opts.fill==='number'?FILLS[Math.max(0,Math.min(4,opts.fill|0))]:'empty');
    const kind=CATCHES.includes(opts.catch)?opts.catch:'fish';
    const rest=!!opts.rest;
    const o=Object.assign({},opts,{dir});
    if(rest){ o._cx=PXR.x; o._cy=PXR.y; o._oy=0; o._oz=0;
              o._roll=(opts.roll||0)*DEG; o._pitch=(opts.pitch||0)*DEG; }
    else {    o._cx=PXC.x; o._cy=PXC.y;
              o._oy = tier===3 ? T.LY : 0;
              o._oz = tier===3 ? -T.h : -(T.h+T.bail);
              o._roll=opts.tilt||0; o._pitch=opts.swing||0; }
    const bail = tier===3 ? null : bailArc(T);
    const bailRamp = tier===2 ? PLAST : STEEL;
    return _toRGBA(_paint(facesFor(tier,fill,kind), o, bail, bailRamp));
  }

  root.BucketIso = { W, H, pivotCarry:PXC, pivotRest:PXR, DIRS:8, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    TIERS, FILLS, CATCHES, KEY,
    ramps:{ STEEL, PLAST, GRAPH, WOOD, FISH, SHELLF, CRUST, BAND, CREAM, SHEEN },
    render };
})(typeof globalThis!=='undefined'?globalThis:window);
