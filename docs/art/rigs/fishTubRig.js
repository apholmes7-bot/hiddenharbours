/* Hidden Harbours — parametric ISO fish tub (M2 bake recipe, ADR-0006 — same pipeline as the boat rigs).
   Small round stave tub (~0.6 m across, two-hand carry) that rides the dory (x1) and the punt (x2).
   Fixed 3/4 turntable camera (elev 40deg default), 45deg steps, flat-facet shading from the upper-left
   key, z-buffered, ordered dither, 1px keyline, NO AA. 32 px = 1 m.

   Cell 44x40, pivot (22,30) = tub base centre. Fills: 'empty' | 'water' | 'catch'.
   RIDING A BOAT — the split is rotation vs translation:
     - the BOAT rig's tubMounts(dir, rockOpts) anchor carries ALL translation (position + heave);
     - the tub bakes ONLY the matching roll/pitch about its own base: render(dir,{fill,roll,pitch}).
   Pin tub pivot to the anchor px and the two layers never shear. heave passed here is ignored.
   Exposes globalThis.FishTubIso = { W,H,PX,DIRS,pivot,order,FILLS,render,WOOD,IRON,ROPE,KEEP,WATER,BAND,KEY }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 44, H = 40, cx = 22, cy = 30;
  const DEG = Math.PI / 180, DEFAULT_ELEV = 40;
  // ramps dark->light. Stave wood = dory ramp; keeper/band keyed to the FishTray tote palette.
  const WOODR = ['#33271b','#473627','#5e4630','#6b4f35','#8a6a48','#9a7853','#a98352'];
  const IRONR = ['#181510','#241f16','#332b1d','#453a26'];
  const ROPE  = ['#4e3f28','#6b5a3a','#8a774c','#a8925e'];
  const KEEP  = ['#152a25','#213f37','#345a4e','#517d6c'];
  const WATER = ['#0d272e','#143741','#1d4b57','#2a626f'];
  const BAND  = ['#8a3c28','#c85a3f','#e07a5a'];
  const SHEEN = ['#c2d6da','#e2eff1'];
  const KEY = '#1c140d';
  const MATS = { wood:{ramp:WOODR,off:0}, iron:{ramp:IRONR,off:-1}, rope:{ramp:ROPE,off:-1},
                 keep:{ramp:KEEP,off:-1}, water:{ramp:WATER,off:-1}, band:{ramp:BAND,off:-1}, sheen:{ramp:SHEEN,off:-1} };
  const RINDEX = {}; [WOODR,IRONR,ROPE,KEEP,WATER,BAND,SHEEN].forEach(r=>r.forEach((c,i)=>{ RINDEX[c]={r,i}; }));
  const GAIN = 3.0, BIAS = 2.7;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  // ---- geometry: tapered staved tub, base r 0.24 -> rim r 0.295, h 0.42, two iron hoops ----
  const HT=0.42, RB=0.24, RT=0.295, RIN=0.255;
  const rOf=(z)=>RB+(RT-RB)*(z/HT);
  const C8=0.414;
  const oct=(r,z)=>[[r,-r*C8,z],[r,r*C8,z],[r*C8,r,z],[-r*C8,r,z],[-r,r*C8,z],[-r,-r*C8,z],[-r*C8,-r,z],[r*C8,-r,z]];
  const JIT=[0.18,-0.12,0.10,-0.05,0.16,-0.10,0.06,-0.16];   // per-stave paint variance
  const v_sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]], v_add=(a,b)=>[a[0]+b[0],a[1]+b[1],a[2]+b[2]];
  const v_mul=(a,s)=>[a[0]*s,a[1]*s,a[2]*s], v_norm=(a)=>{const m=Math.hypot(a[0],a[1],a[2])||1;return [a[0]/m,a[1]/m,a[2]/m];};
  const v_cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
  function tube(A,B2,rad,mat,b){
    const ax=v_norm(v_sub(B2,A)); let up=[0,0,1]; if(Math.abs(ax[2])>0.9) up=[0,1,0];
    const r=v_norm(v_cross(ax,up)), u=v_cross(r,ax);
    const ring=(P)=>[v_add(v_add(P,v_mul(r,rad)),v_mul(u,rad)),v_add(v_add(P,v_mul(r,-rad)),v_mul(u,rad)),
                     v_add(v_add(P,v_mul(r,-rad)),v_mul(u,-rad)),v_add(v_add(P,v_mul(r,rad)),v_mul(u,-rad))];
    const r0=ring(A), r1=ring(B2), out=[];
    for(let k=0;k<4;k++){ const k2=(k+1)%4; out.push({v:[r0[k],r0[k2],r1[k2],r1[k]],mat,b:b||0,db:-0.1}); }
    return out;
  }
  function box(c,h,mat,b,db){
    const P=(sx,sy,sz)=>[c[0]+sx*h[0],c[1]+sy*h[1],c[2]+sz*h[2]];
    const f=(v)=>({v,mat,b:b||0,db:db||0});
    return [ f([P(-1,-1,1),P(1,-1,1),P(1,1,1),P(-1,1,1)]), f([P(-1,1,-1),P(1,1,-1),P(1,-1,-1),P(-1,-1,-1)]),
      f([P(-1,1,1),P(1,1,1),P(1,1,-1),P(-1,1,-1)]), f([P(1,-1,1),P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1)]),
      f([P(1,1,1),P(1,-1,1),P(1,-1,-1),P(1,1,-1)]), f([P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1)]) ];
  }
  function shell(){
    const fs=[];
    const bands=[[0,.10,'wood',0,1],[.10,.145,'iron',-0.05,0],[.145,.30,'wood',0,1],[.30,.345,'iron',0.05,0],[.345,HT,'wood',0.08,1]];
    for(const [z0,z1,mat,b,stave] of bands){
      const lo=oct(rOf(z0),z0), hi=oct(rOf(z1),z1);
      for(let k=0;k<8;k++){ const k2=(k+1)%8; fs.push({v:[lo[k],lo[k2],hi[k2],hi[k]],mat,b:b+(stave?JIT[k]:0),db:0}); }
    }
    const rim=oct(RT,HT), rin=oct(RIN,HT);
    for(let k=0;k<8;k++){ const k2=(k+1)%8; fs.push({v:[rim[k],rim[k2],rin[k2],rin[k]],mat:'wood',b:0.55,db:0}); }   // rim cap
    fs.push({v:oct(RB,0).reverse(),mat:'wood',b:-0.6,db:0});                                                        // underside
    for(const s of [-1,1]){   // rope becket handles, port & starboard
      const A=[s*0.285,-0.095,0.395], M=[s*0.350,0,0.340], B2=[s*0.285,0.095,0.395];
      fs.push(...tube(A,M,0.022,'rope',0.15), ...tube(M,B2,0.022,'rope',0.05));
    }
    return fs;
  }
  function fillFaces(fill){
    const zf = fill==='catch'?0.30 : fill==='water'?0.25 : 0.05;
    const fs=[], top=oct(RIN,HT), bot=oct(RIN-0.01,zf);
    for(let k=0;k<8;k++){ const k2=(k+1)%8; fs.push({v:[top[k],top[k2],bot[k2],bot[k]],mat:'wood',b:-1.25,db:0}); }  // inner wall
    const capMat = fill==='catch'?'keep' : fill==='water'?'water':'wood';
    fs.push({v:oct(RIN-0.012,zf+0.002),mat:capMat,b:fill==='empty'?-0.9:0.15,db:0});
    const pip=(x,y,z,w,h,mat,b)=>fs.push({v:[[x-w,y-h,z],[x+w,y-h,z],[x+w,y+h,z],[x-w,y+h,z]],mat,b,db:-0.02});
    if(fill==='empty'){ pip(0.03,-0.02,0.056,0.05,0.02,'sheen',-0.4); }                       // wet floor sheen
    if(fill==='water'){ pip(-0.06,0.05,0.256,0.05,0.02,'sheen',0.35); pip(0.08,-0.06,0.256,0.03,0.015,'sheen',0.1); pip(0.02,-0.01,0.256,0.065,0.022,'water',0.7); }
    if(fill==='catch'){                                                                        // keeper heap + one rust crab
      fs.push(...box([0.07,-0.06,0.315],[0.075,0.065,0.030],'keep',0.35));
      fs.push(...box([-0.075,0.055,0.312],[0.065,0.060,0.026],'keep',0.10));
      fs.push(...box([-0.010,-0.020,0.318],[0.050,0.045,0.030],'band',0.20));
      pip(0.07,-0.06,0.350,0.030,0.012,'band',0.45); pip(-0.075,0.055,0.342,0.025,0.012,'band',0.30);
      pip(0.045,0.030,0.352,0.020,0.012,'sheen',-0.2);
    }
    return fs;
  }
  const FILLS=['empty','water','catch'], FCACHE={};
  function facesFor(fill){ if(!FCACHE[fill]) FCACHE[fill]=shell().concat(fillFaces(fill)); return FCACHE[fill]; }

  // ---- rasterizer (shared recipe) ----
  function normal(a,b,c){
    const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
    const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m];
  }
  function shadeOf(n,se,ce){ return n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2]; }
  function camBasis(opts){
    const dir=opts.dir||0, th=dir*Math.PI/4;
    const e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    const roll=(opts.roll||0)*DEG, pitch=(opts.pitch||0)*DEG;
    return { ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e),
      cr:Math.cos(roll), sr:Math.sin(roll), cq:Math.cos(pitch), sq:Math.sin(pitch), heave:0 };
  }
  function projVert(x,y,z,B){
    const x1=x*B.cr+z*B.sr, z1=-x*B.sr+z*B.cr;
    const y2=y*B.cq - z1*B.sq, z2=y*B.sq + z1*B.cq;
    const xr=x1*B.ct - y2*B.stt, yr=x1*B.stt + y2*B.ct, zr=z2;
    return { xr,yr,zr, sx:cx+xr*S, sy:cy-(yr*B.se+zr*B.ce)*S, d:(yr*B.ce-zr*B.se) };
  }
  function _paint(faces, opts){
    const B=camBasis(opts);
    const zbuf=new Float32Array(W*H).fill(Infinity);
    const col=new Array(W*H).fill(null);
    const dep=new Float32Array(W*H);
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
      let n=normal(rv[0],rv[1],rv[2]);
      let sh=shadeOf(n,B.se,B.ce); if(sh<0 && ((f.b||0)<=-1)) sh=shadeOf([-n[0],-n[1],-n[2]],B.se,B.ce)*0.9;
      const fidx = sh*GAIN + BIAS + (f.b||0);
      const M = MATS[f.mat] || MATS.wood;
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
    const out=new Array(W*H).fill(null);
    for(let i=0;i<W*H;i++) out[i]=col[i];
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){   // inner depth-edge separation
      const i=y*W+x; if(!col[i]) continue;
      for(const [dx,dy] of [[1,0],[0,1]]){
        const nx=x+dx, ny=y+dy; if(nx>=W||ny>=H) continue;
        const j=ny*W+nx; if(!col[j]) continue;
        if(Math.abs(dep[i]-dep[j])>0.30){
          const far=dep[i]>dep[j]?i:j, e=RINDEX[col[far]];
          if(e && e.i>0) out[far]=e.r[Math.max(0,e.i-2)];
        }
      }
    }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){    // external keyline
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
    opts = (typeof opts==='number') ? {elev:opts} : (opts||{});
    const fill = FILLS.includes(opts.fill) ? opts.fill : 'empty';
    return _toRGBA(_paint(facesFor(fill), Object.assign({}, opts, {dir})));
  }
  root.FishTubIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:cy}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'], FILLS,
    WOOD:WOODR, IRON:IRONR, ROPE, KEEP, WATER, BAND, KEY, render };
})(typeof globalThis!=='undefined'?globalThis:window);
