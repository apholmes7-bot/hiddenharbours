/* Hidden Harbours — INSULATED FISH TOTE iso rig (M2 bake recipe — catch-handling pass).
   The big boxed tote that rides a fishing deck (Cape Islander up). Same fixed 3/4
   turntable as the fleet (45deg steps CW, upper-left key, dither, 1px keyline, no AA,
   32 px = 1 m). ~1.0 x 1.0 x 0.95 m: pallet-foot rails, ribbed moulded walls, rim,
   moulded X-brace lid. COLOURS = several, like reality — fleet master ramps only.
   LIDS: on · off (see the fill) · lean (lid against the +x wall).
   Cell 64x72, pivot (32,60) = ground under the centre — placeable on boat mount
   anchors exactly like the fish tub.
   FILLS with REAL items: the shell is genuinely HOLLOW when the lid is off — inner
   walls + floor bake with the shell, and slots(dir) returns 4 stacked layers of
   projected slot points rising from the floor (6 per layer, back-to-front, monotonic),
   so part-filled totes read as layers of catch visibly stacking toward the rim. The
   page/game blits CatchKit items onto them, clipped to opening(dir) (the projected rim
   quad — the front wall correctly occludes the low layers). Rot/spoil lives on the
   ITEMS (CatchKit), not the tote.
   Exposes globalThis.FishTote = { W,H,pivot,KEY,COLOURS,CORDER,LIDS,FILLS,FILLZ,
   defaultElev,render(dir,{colour,lid,fill,elev}),slots(dir,fill,elev),opening(dir,elev) }. */
(function (root) {
  const S = 32, DEG = Math.PI/180, DEFAULT_ELEV = 40;
  const W = 64, H = 72, cx = 32, cy = 60;
  const KEY = '#101a19';
  const NAVY  = ['#0e1526','#172644','#223764','#2f4c88','#4166ac'];
  const STEEL = ['#3a4148','#565f66','#7a858c','#9fabb1','#c3ced2'];
  const RUST  = ['#4a100e','#7c1a15','#a8241b','#cf3626','#e2573c'];
  const TEAL  = ['#0d3f3c','#14554e','#1c7367','#2ba39a','#49b8aa'];
  const PLAST = ['#33474f','#476069','#5f7d8a','#86a3ad','#a9c2c9'];
  const GRAPH = ['#101317','#1d2127','#2b323a','#3d454e','#525c63'];
  const COLOURS = { navy:{label:'NAVY',ramp:NAVY}, steel:{label:'STEEL GREY',ramp:STEEL},
    plast:{label:'FADED BLUE',ramp:PLAST}, rust:{label:'RUST RED',ramp:RUST}, teal:{label:'TEAL',ramp:TEAL} };
  const CORDER = ['navy','steel','plast','rust','teal'];
  const LIDS = ['on','off','lean'];
  const FILLS = ['empty','few','half','full','brim'];
  const FILLZ = { empty:0.16, few:0.28, half:0.45, full:0.62, brim:0.78 };

  const GAIN = 3.0, BIAS = 2.7, EDGE = 0.12;
  const LN = (()=>{ const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));
  const hex2rgb=(h)=>[parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)];
  const ID=(p)=>p;
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
  function camBasis(opts){
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
  const shadeOf=(n,se,ce)=>n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2];

  function facesOf(o){
    const lid = LIDS.indexOf(o.lid)>=0 ? o.lid : 'on';
    const F=[];
    const add=(fs)=>{ for(const f of fs) F.push(f); };
    // pallet-foot rails
    add(box([0,-0.30,0.055],[0.42,0.075,0.055],'feet',-0.3,0));
    add(box([0, 0.30,0.055],[0.42,0.075,0.055],'feet',-0.3,0));
    // body — top face dropped when the lid is off (the opening)
    const body=box([0,0,0.475],[0.47,0.47,0.36],'wall',0,0);
    add(lid==='on' ? body : body.slice(1));
    // moulded vertical ribs, all four walls
    for (const yk of [-0.30,-0.10,0.10,0.30]){
      add(box([ 0.478,yk,0.46],[0.014,0.045,0.28],'wall',0.35,-0.01));
      add(box([-0.478,yk,0.46],[0.014,0.045,0.28],'wall',0.35,-0.01));
      add(box([yk, 0.478,0.46],[0.045,0.014,0.28],'wall',0.35,-0.01));
      add(box([yk,-0.478,0.46],[0.045,0.014,0.28],'wall',0.35,-0.01));
    }
    // rim — a FRAME of four slabs, so the opening genuinely stays open
    add(box([0, 0.47,0.865],[0.49,0.022,0.035],'wall',0.15,0));
    add(box([0,-0.47,0.865],[0.49,0.022,0.035],'wall',0.15,0));
    add(box([ 0.47,0,0.865],[0.022,0.49,0.035],'wall',0.15,0));
    add(box([-0.47,0,0.865],[0.022,0.49,0.035],'wall',0.15,0));
    if (lid==='off' || lid==='lean'){
      // genuinely hollow: inner walls + floor, in the tote's own darker plastic
      F.push.apply(F, box([0,0,0.135],[0.435,0.435,0.010],'inner',-1.2,0.01));
      F.push.apply(F, box([ 0.445,0,0.485],[0.012,0.435,0.350],'inner',-0.9,0.01));
      F.push.apply(F, box([-0.445,0,0.485],[0.012,0.435,0.350],'inner',-0.9,0.01));
      F.push.apply(F, box([0, 0.445,0.485],[0.435,0.012,0.350],'inner',-0.9,0.01));
      F.push.apply(F, box([0,-0.445,0.485],[0.435,0.012,0.350],'inner',-0.9,0.01));
    }
    if (lid==='on'){
      add(box([0,0,0.925],[0.50,0.50,0.028],'lid',0.1,0));
      for (const sg of [1,-1]){                            // moulded X brace
        const ca=Math.cos(sg*Math.PI/4), sa=Math.sin(sg*Math.PI/4);
        const rz=(p)=>[p[0]*ca-p[1]*sa, p[0]*sa+p[1]*ca, p[2]];
        add(box([0,0,0.958],[0.56,0.025,0.007],'lid',0.4,-0.02,rz));
      }
    }
    if (lid==='lean'){
      // lid standing against the +x wall, 12 deg off vertical
      const ca=Math.cos(78*DEG), sa=Math.sin(78*DEG);
      const rot=(p)=>[p[0]*ca+p[2]*sa + 0.72, p[1], -p[0]*sa+p[2]*ca + 0.50];
      add(box([0,0,0],[0.50,0.50,0.028],'lid',0.1,0,rot));
      const rz2=(p)=>{ const q=[p[0]*0.707-p[1]*0.707, p[0]*0.707+p[1]*0.707, p[2]+0.033];
        return [q[0]*ca+q[2]*sa + 0.72, q[1], -q[0]*sa+q[2]*ca + 0.50]; };
      add(box([0,0,0],[0.56,0.025,0.007],'lid',0.4,-0.02,rz2));
      add(box([0,0,0],[0.025,0.56,0.007],'lid',0.4,-0.02,rz2));
    }
    return F;
  }
  function _paint(o){
    const B=camBasis(o);
    const ramp=(COLOURS[o.colour]||COLOURS.navy).ramp;
    const MATS={ wall:{ramp,off:0}, lid:{ramp,off:0}, feet:{ramp:GRAPH,off:0}, inner:{ramp,off:-1}, void:{ramp:GRAPH,off:0} };
    const RINDEX={}; [ramp,GRAPH].forEach(r=>r.forEach((c,i)=>{ RINDEX[c]={r,i}; }));
    const F=facesOf(o);
    const zbuf=new Float32Array(W*H).fill(Infinity);
    const col=new Array(W*H).fill(null);
    const dep=new Float32Array(W*H);
    for (const f of F){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
      const n=normal(rv[0],rv[1],rv[2]);
      const sh=shadeOf(n,B.se,B.ce);
      const fidx=sh*GAIN+BIAS+(f.b||0);
      const M=MATS[f.mat]||MATS.wall;
      for (let tt=1;tt+1<rv.length;tt++) fillTri(rv[0],rv[tt],rv[tt+1]);
      function fillTri(a,b,c){
        const minX=Math.max(0,Math.floor(Math.min(a.sx,b.sx,c.sx)));
        const maxX=Math.min(W-1,Math.ceil(Math.max(a.sx,b.sx,c.sx)));
        const minY=Math.max(0,Math.floor(Math.min(a.sy,b.sy,c.sy)));
        const maxY=Math.min(H-1,Math.ceil(Math.max(a.sy,b.sy,c.sy)));
        const area=(b.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b.sy-a.sy);
        if (Math.abs(area)<1e-6) return;
        for (let y=minY;y<=maxY;y++) for (let x=minX;x<=maxX;x++){
          const px=x+0.5, py=y+0.5;
          let w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area;
          let w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area;
          let w2=1-w0-w1;
          if (w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const d=w0*a.d+w1*b.d+w2*c.d, deff=d-(f.db||0);
          const i=y*W+x;
          if (deff<zbuf[i]){
            zbuf[i]=deff; dep[i]=d;
            let base=Math.floor(fidx);
            let idx=base+((fidx-base)>BAYER[x&3][y&3]?1:0)+M.off;
            col[i]=M.ramp[Math.max(0,Math.min(M.ramp.length-1,idx))];
          }
        }
      }
    }
    const out=col.slice();
    for (let y=0;y<H;y++) for (let x=0;x<W;x++){
      const i=y*W+x; if (!col[i]) continue;
      for (const [dx,dy] of [[1,0],[0,1]]){
        const nx=x+dx, ny=y+dy; if (nx>=W||ny>=H) continue;
        const j=ny*W+nx; if (!col[j]) continue;
        if (Math.abs(dep[i]-dep[j])>EDGE){
          const far=dep[i]>dep[j]?i:j, e=RINDEX[col[far]];
          if (e && e.i>0) out[far]=e.r[Math.max(0,e.i-2)];
        }
      }
    }
    for (let y=0;y<H;y++) for (let x=0;x<W;x++){
      const i=y*W+x; if (out[i]) continue;
      let touch=false;
      for (const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){
        const nx=x+dx, ny=y+dy;
        if (nx>=0&&nx<W&&ny>=0&&ny<H&&col[ny*W+nx]){ touch=true; break; }
      }
      if (touch) out[i]=KEY;
    }
    const rgba=new Uint8ClampedArray(W*H*4);
    for (let i=0;i<W*H;i++){
      const c=out[i]; if (!c){ rgba[i*4+3]=0; continue; }
      const [r,g,b]=hex2rgb(c);
      rgba[i*4]=r; rgba[i*4+1]=g; rgba[i*4+2]=b; rgba[i*4+3]=255;
    }
    return rgba;
  }
  function render(dir, opts){ return _paint(Object.assign({colour:'navy',lid:'on',fill:'empty'},opts||{},{dir})); }
  function mulberry(seed){ let a=seed>>>0; return function(){ a|=0; a=(a+0x6D2B79F5)|0;
    let t=Math.imul(a^(a>>>15),1|a); t=(t+Math.imul(t^(t>>>7),61|t))^t; return ((t^(t>>>14))>>>0)/4294967296; }; }
  // 4 stacked layers × 8 slots rising from the floor — back-to-front within each layer,
  // floor layer first: drawing the first N items always shows layers visibly stacking
  function slots(dir, fill, elev){
    const B=camBasis({dir, elev});
    const rng=mulberry(101), pts=[];
    for (let layer=0; layer<4; layer++){
      const lz=0.16 + layer*0.17;
      const lp=[];
      for (const gy of [-0.20,0.20]) for (const gx of [-0.30,-0.10,0.10,0.30]){
        const jx=(rng()-0.5)*0.09, jy=(rng()-0.5)*0.08;
        const v=projVert(gx+jx+(layer%2?0.06:0), gy+jy+(layer%2?0.05:0), lz, B);
        lp.push({ dx:Math.round(v.sx-cx), dy:Math.round(v.sy-cy), sy:v.sy });
      }
      lp.sort((a,b)=>a.sy-b.sy);
      pts.push.apply(pts, lp);
    }
    return pts;
  }
  // projected rim quad (z = rim top) — the clip region for composited items
  function opening(dir, elev){
    const B=camBasis({dir, elev});
    return [[-0.42,-0.42],[0.42,-0.42],[0.42,0.42],[-0.42,0.42]].map(([x,y])=>{
      const v=projVert(x,y,0.845,B);
      return { dx:Math.round(v.sx-cx), dy:Math.round(v.sy-cy) };
    });
  }
  root.FishTote = { W, H, pivot:{x:cx,y:cy}, KEY, COLOURS, CORDER, LIDS, FILLS, FILLZ,
    defaultElev:DEFAULT_ELEV, render, slots, opening };
})(typeof globalThis!=='undefined'?globalThis:window);
