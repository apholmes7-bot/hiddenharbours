/* Hidden Harbours — parametric ISO centre-console skiff (M2 bake recipe, ADR-0006 — same pipeline
   as puntIsoRig.js / doryIsoRig.js). PASS 1: hull + console + canopy. The step between the punt and
   the Cape Islander: ~7.0 m LOA, flared bow, wide low transom cut for an outboard, self-draining
   wood sole, painted interior. Centre console amidships with raked windscreen and wheel (remote
   steer — no tiller), helm seat just aft, small teal-canvas canopy on four legs over both.
   Fleet KTC scheme: white topsides, teal sheer band + bottom, gold cove pinstripe.
   Fixed 3/4 turntable camera (elev 40deg default), 45deg steps, flat-facet shading from the
   upper-left key, z-buffered, ordered dither, 1px keyline, NO AA. 32 px = 1 m.

   Cell 244x216, pivot (122,120) = boat origin (amidships, keel bottom, centreline), pinned every
   heading. Outboard ships as its own pivoting layer NEXT PASS (remote-steer variant of the punt
   motor); the transom clamp point is baked now: motorMount(dir,opts) -> cell {x,y}. Pass the hull's
   rock(i) values so overlays ride the same wave. helmSeat(dir,opts) -> cell {x,y} anchor for the
   seated operator sprite. tubMounts(dir,opts) -> 3 deck anchors for fish tubs.
   Exposes globalThis.ConsoleIso = { W,H,PX,DIRS,pivot,order,ROCK,rock(i),render(dir,opts),
   motorMount, MOUNT, helmSeat, HELM, TUBS, tubMounts, PAINT,TRIM,GOLD,CANV,GLAS,WOOD,IRON,MOTO,KEY }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 244, H = 216, cx = 122, cy = 120;
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;
  const ROCK = { frames: 8, rollA: 3.4, pitchA: 1.9, heaveA: 1.3, period: 2.8 };  // heavier hull, stiffer than the punt
  function rockMotion(i, frames){
    frames = frames || ROCK.frames;
    const a = 2*Math.PI*(((i%frames)+frames)%frames)/frames;
    return { roll: ROCK.rollA*Math.sin(a), pitch: ROCK.pitchA*Math.sin(a+Math.PI/2), heave: ROCK.heaveA*Math.sin(a) };
  }
  const L = 7.0, TH = 0.045, DECK = 0.28;
  const NSEG = 20;

  // paint ramps dark->light (KTC: fleet scheme — teal #2ba39a / white #eef0ea / gold #e0b13a)
  const PAINT = ['#5d6a70','#7e8c90','#a3b0b1','#c2cdca','#dde5df','#eef0ea','#f7f8f3'];  // white topsides + console
  const TRIM  = ['#0d3f3c','#14554e','#1c7367','#2ba39a','#49b8aa'];                      // teal sheer band + bottom + rails
  const GOLD  = ['#7a5a1c','#a8842a','#e0b13a'];                                          // cove pinstripe
  const CANV  = ['#2a5750','#3d7469','#559182','#74ad97','#97c6ab'];                      // sun-faded canopy canvas
  const GLAS  = ['#16333c','#24505a','#3a7680','#5fa3a6','#8fc9c4'];                      // windscreen glass
  const WOOD  = ['#33271b','#473627','#5e4630','#6b4f35','#8a6a48','#9a7853','#a98352'];  // sole + seats (dory ramp)
  const IRON  = ['#20180f','#2a2014','#3a2c1c'];
  const MOTO  = ['#101317','#1d2127','#2b323a','#3d454e','#525c63','#6b767b','#8a9499'];  // canopy legs, wheel, rails
  const KEY   = '#101a19';
  const MATS = { paint:{ramp:PAINT,off:0}, trim:{ramp:TRIM,off:-1}, gold:{ramp:GOLD,off:-3},
                 canv:{ramp:CANV,off:0}, glas:{ramp:GLAS,off:0},
                 wood:{ramp:WOOD,off:0}, iron:{ramp:IRON,off:-2},
                 moto:{ramp:MOTO,off:0}, blk:{ramp:MOTO,off:-2} };
  const RINDEX = {}; [PAINT,TRIM,GOLD,CANV,GLAS,WOOD,IRON,MOTO].forEach(r=>r.forEach((c,i)=>{ RINDEX[c]={r,i}; }));
  const GAIN = 3.0, BIAS = 2.7;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  // ---- offsets table: stern(0) -> bow(8). [sheerHalf, bottomHalf, depth, keelZ] (m) ----
  // fuller and deeper than the punt: flared bow, flat run aft, wide transom for the outboard.
  const T = [
    [0.92,0.74,0.66,0.06],   // transom
    [1.05,0.84,0.64,0.01],
    [1.12,0.90,0.63,0.00],
    [1.15,0.92,0.62,0.00],
    [1.13,0.89,0.62,0.00],
    [1.06,0.80,0.64,0.02],
    [0.90,0.62,0.68,0.07],
    [0.62,0.34,0.74,0.16],
    [0.07,0.03,0.82,0.30],   // raked stem
  ];
  const lerp = (a,b,t)=>a+(b-a)*t;
  function station(u){
    const f=Math.max(0,Math.min(8,u*8)), i=Math.min(7,Math.floor(f)), fr=f-i;
    const A=T[i], B=T[i+1];
    return { ws:lerp(A[0],B[0],fr), wb:lerp(A[1],B[1],fr), dep:lerp(A[2],B[2],fr),
             kz:lerp(A[3],B[3],fr), y:-L/2+u*L };
  }
  function skin(side,u,frac,inset){
    const st=station(u);
    const ws=st.ws-(inset?TH:0), wb=st.wb-(inset?TH*0.6:0), dep=st.dep-(inset?0.02:0);
    return [ side*lerp(wb,ws,frac), st.y, st.kz+lerp(0,dep,frac) ];
  }
  function dfrac(st){ return Math.max(0.04, Math.min(1, (DECK - st.kz)/st.dep)); }

  // ---- generic solids ----
  const ID=(p)=>p;
  const v_sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]], v_add=(a,b)=>[a[0]+b[0],a[1]+b[1],a[2]+b[2]];
  const v_mul=(a,s)=>[a[0]*s,a[1]*s,a[2]*s], v_norm=(a)=>{const m=Math.hypot(a[0],a[1],a[2])||1;return [a[0]/m,a[1]/m,a[2]/m];};
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
  function tube(A,B2,rad,mat,b,xf){
    xf=xf||ID;
    const P0=xf(A), P1=xf(B2);
    const ax=v_norm(v_sub(P1,P0)); let up=[0,0,1]; if(Math.abs(ax[2])>0.9) up=[0,1,0];
    const r=v_norm(v_cross(ax,up)), u=v_cross(r,ax);
    const ring=(P)=>[ v_add(v_add(P,v_mul(r,rad)),v_mul(u,rad)), v_add(v_add(P,v_mul(r,-rad)),v_mul(u,rad)),
                      v_add(v_add(P,v_mul(r,-rad)),v_mul(u,-rad)), v_add(v_add(P,v_mul(r,rad)),v_mul(u,-rad)) ];
    const r0=ring(P0), r1=ring(P1), out=[];
    for(let k=0;k<4;k++){ const k2=(k+1)%4; out.push({v:[r0[k],r0[k2],r1[k2],r1[k]],mat,b:b||0,db:-0.15}); }
    return out;
  }

  // ---- face list ----
  const F = [];
  const face=(v,mat,b,db)=>F.push({v,mat:mat||'paint',b:b||0,db:db||0});
  const boxF=(c,h,mat,b,db)=>{ F.push.apply(F, box(c,h,mat,b,db)); };
  const tubeF=(A,B2,rad,mat,b)=>{ F.push.apply(F, tube(A,B2,rad,mat,b)); };
  // outer paint scheme: white body, gold cove line, teal sheer band  [f0, f1, mat, b, db]
  const OB = [ [0,1/3,'paint',0,0], [1/3,2/3,'paint',0,0], [2/3,0.79,'paint',0,0],
               [0.79,0.86,'gold',0.3,0.01], [0.86,1,'trim',0,0] ];
  (function build(){
    for(const side of [-1,1]){
      for(let i=0;i<NSEG;i++){
        const u0=i/NSEG, u1=(i+1)/NSEG;
        // outer skin bands (smooth glass hull — no plank seams)
        for(const [f0,f1,mat,b,db] of OB)
          face([skin(side,u0,f0),skin(side,u1,f0),skin(side,u1,f1),skin(side,u0,f1)],mat,b,db);
        // inner skin, deck line to sheer (painted liner), 2 bands
        const sa=station(u0), sb=station(u1), fa=dfrac(sa), fb=dfrac(sb);
        for(let k=0;k<2;k++){
          const g0a=fa+(1-fa)*k/2, g1a=fa+(1-fa)*(k+1)/2;
          const g0b=fb+(1-fb)*k/2, g1b=fb+(1-fb)*(k+1)/2;
          face([skin(side,u1,g0b,1),skin(side,u0,g0a,1),skin(side,u0,g1a,1),skin(side,u1,g1b,1)],'paint',-1.6);
        }
        // bottom (teal anti-foul)
        face([skin(-1,u0,0),skin(-1,u1,0),skin(1,u1,0),skin(1,u0,0)],'trim',-1.0);
        // gunwale cap (teal rail)
        const oa=skin(side,u0,1),ob=skin(side,u1,1),ia=skin(side,u0,1,1),ib=skin(side,u1,1,1);
        const inb=(p)=>[p[0]-side*TH*1.4,p[1],p[2]];
        face([oa,ob,inb(ib),inb(ia)],'trim',0.4,0.03);
      }
    }
    // deck sole (wood), transom to foredeck bulkhead
    const DSEG=16;
    const dw=(u)=>{ const st=station(u); return (lerp(st.wb,st.ws,dfrac(st))-TH)*0.96; };
    for(let i=0;i<DSEG;i++){
      const u0=0.80*i/DSEG, u1=0.80*(i+1)/DSEG;
      face([[-dw(u0),station(u0).y,DECK],[dw(u0),station(u0).y,DECK],[dw(u1),station(u1).y,DECK],[-dw(u1),station(u1).y,DECK]],'wood',-0.35);
    }
    // transom: paint bands carried across
    const tp=(s,f)=>skin(s,0,f);
    for(const [f0,f1,mat,b] of OB)
      face([tp(-1,f1),tp(1,f1),tp(1,f0),tp(-1,f0)], mat, (b||0)-0.8, 0.005);
    // motor pad: wood clamp plate proud of the transom, top centre
    (function(){ const st=station(0), zt=st.kz+st.dep, zb=st.kz+st.dep*0.45, y=st.y-0.03, hx=0.17;
      face([[-hx,y,zt],[hx,y,zt],[hx,y,zb],[-hx,y,zb]],'wood',-0.55,-0.03);
      face([[-hx,y,zt],[hx,y,zt],[hx,st.y+0.08,zt],[-hx,st.y+0.08,zt]],'wood',0.35,-0.03);
    })();
    // foredeck (raised bow platform following the sheer)
    const FSEG=5, DROP=0.07;
    const fz=(u)=>{ const st=station(u); return st.kz+st.dep-DROP; };
    const fw=(u)=>{ const st=station(u); return Math.max(0.02,(st.ws-TH)*0.94); };
    for(let i=0;i<FSEG;i++){
      const u0=0.80+0.185*i/FSEG, u1=0.80+0.185*(i+1)/FSEG;
      face([[-fw(u0),station(u0).y,fz(u0)],[fw(u0),station(u0).y,fz(u0)],[fw(u1),station(u1).y,fz(u1)],[-fw(u1),station(u1).y,fz(u1)]],'wood',0.5);
    }
    (function(){ const u=0.80, wv=fw(u), z=fz(u), y=station(u).y;   // bulkhead under the foredeck lip
      face([[-wv,y,z],[wv,y,z],[wv,y,DECK],[-wv,y,DECK]],'paint',-1.9);
    })();
    boxF([0,3.10,fz(0.94)+0.045],[0.028,0.10,0.038],'iron',0.15,-0.02);   // bow cleat
    // ---- centre console (slanted front, raked windscreen, wheel on the aft face) ----
    (function(){
      const X0=0.34, XT=0.30, Y0=-0.25, Y1=0.55, YT=0.40, Z0=DECK, ZTF=1.16, ZTA=1.20;
      const A=[-X0,Y0,Z0], B=[X0,Y0,Z0], C=[X0,Y1,Z0], D=[-X0,Y1,Z0];
      const E=[-XT,Y0,ZTA], Fq=[XT,Y0,ZTA], G=[XT,YT,ZTF], Hq=[-XT,YT,ZTF];
      face([Hq,G,C,D],'paint',0.55,-0.01);                       // front slant
      face([E,Fq,G,Hq],'paint',0.85,-0.01);                      // top (helm panel)
      face([A,B,Fq,E],'paint',-0.75,-0.01);                      // aft face (helm side)
      face([B,C,G,Fq],'paint',-0.15,-0.01);                      // starboard side
      face([D,A,E,Hq],'paint',-0.15,-0.01);                      // port side
      // windscreen: raked glass off the front top edge + grab rail
      face([[-0.225,0.26,1.42],[0.225,0.26,1.42],[0.27,0.40,1.165],[-0.27,0.40,1.165]],'glas',0.9,-0.02);
      tubeF([-0.24,0.255,1.44],[0.24,0.255,1.44],0.021,'moto',0.2);
      // wheel + hub on the aft face
      boxF([0,Y0-0.045,0.95],[0.09,0.018,0.09],'blk',0.25,-0.02);
      boxF([0,Y0-0.07,0.95],[0.03,0.012,0.03],'moto',0.5,-0.03);
    })();
    // helm seat (centre, wheel reach) — pedestal box, wood slab, low backrest
    boxF([0,-0.86,0.45],[0.24,0.15,0.17],'paint',-0.35);
    boxF([0,-0.86,0.645],[0.30,0.185,0.035],'wood',0.6);
    boxF([0,-1.055,0.80],[0.28,0.028,0.14],'wood',-0.5);
    // aft bench across the stern
    boxF([0,-3.10,0.42],[0.62,0.12,0.12],'paint',-0.6);
    boxF([0,-3.10,0.575],[0.70,0.155,0.032],'wood',0.55);
    // ---- canopy: four legs + cambered teal canvas top with skirts ----
    for(const s of [-1,1]){
      tubeF([s*0.44,-0.88,DECK],[s*0.385,-0.96,1.90],0.024,'moto',-0.1);
      tubeF([s*0.44, 0.50,DECK],[s*0.385, 0.58,1.90],0.024,'moto',-0.1);
    }
    tubeF([-0.385,-0.96,1.87],[0.385,-0.96,1.87],0.019,'moto',-0.3);
    tubeF([-0.385, 0.58,1.87],[0.385, 0.58,1.87],0.019,'moto',-0.3);
    (function(){
      const Ya=-1.10, Yf=0.72, X=0.56, ZE=1.90, ZR=1.985, ZS=ZE-0.085;
      face([[0,Ya,ZR],[0,Yf,ZR],[-X,Yf,ZE],[-X,Ya,ZE]],'canv',0.45);        // port slope
      face([[X,Ya,ZE],[X,Yf,ZE],[0,Yf,ZR],[0,Ya,ZR]],'canv',0.2);           // starboard slope
      face([[-X,Yf,ZE],[X,Yf,ZE],[X,Yf,ZS],[-X,Yf,ZS]],'canv',-0.5);        // front skirt
      face([[X,Ya,ZE],[-X,Ya,ZE],[-X,Ya,ZS],[X,Ya,ZS]],'canv',-1.0);        // aft skirt
      face([[-X,Ya,ZE],[-X,Yf,ZE],[-X,Yf,ZS],[-X,Ya,ZS]],'canv',-0.6);      // port skirt
      face([[X,Yf,ZE],[X,Ya,ZE],[X,Ya,ZS],[X,Yf,ZS]],'canv',-0.8);          // starboard skirt
    })();
  })();

  // ---- rasterizer (shared recipe; G overrides cell geometry for future overlay layers) ----
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
  function projVert(x,y,z,B,G){
    const gx=G?G.cx:cx, gy=G?G.cy:cy;
    const x1=x*B.cr+z*B.sr, z1=-x*B.sr+z*B.cr;
    const y2=y*B.cq - z1*B.sq, z2=y*B.sq + z1*B.cq;
    const xr=x1*B.ct - y2*B.stt, yr=x1*B.stt + y2*B.ct, zr=z2;
    return { xr,yr,zr, sx:gx+xr*S, sy:gy-(yr*B.se+zr*B.ce)*S - B.heave, d:(yr*B.ce-zr*B.se) };
  }
  function _paint(faces, opts, doEdge, G){
    const PW=G?G.W:W, PH=G?G.H:H;
    const B=camBasis(opts);
    const zbuf=new Float32Array(PW*PH).fill(Infinity);
    const col=new Array(PW*PH).fill(null);
    const dep=new Float32Array(PW*PH);
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B,G));
      let n=normal(rv[0],rv[1],rv[2]);
      let sh=shadeOf(n, B.se, B.ce); if(sh<0 && ((f.b||0)<=-1)) sh=shadeOf([-n[0],-n[1],-n[2]], B.se, B.ce)*0.9;
      const fidx = sh*GAIN + BIAS + (f.b||0);
      const M = MATS[f.mat] || MATS.paint;
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
            zbuf[i]=deff; dep[i]=d;
            let base=Math.floor(fidx);
            let idx=base+((fidx-base)>BAYER[x&3][y&3]?1:0)+M.off;
            col[i]=M.ramp[Math.max(0,Math.min(M.ramp.length-1,idx))];
          }
        }
      }
    }
    const out=new Array(PW*PH).fill(null);
    for(let i=0;i<PW*PH;i++) out[i]=col[i];
    if(doEdge){
      for(let y=0;y<PH;y++) for(let x=0;x<PW;x++){
        const i=y*PW+x; if(!col[i]) continue;
        for(const [dx,dy] of [[1,0],[0,1]]){
          const nx=x+dx, ny=y+dy; if(nx>=PW||ny>=PH) continue;
          const j=ny*PW+nx; if(!col[j]) continue;
          if(Math.abs(dep[i]-dep[j])>0.30){
            const far=dep[i]>dep[j]?i:j, e=RINDEX[col[far]];
            if(e && e.i>0) out[far]=e.r[Math.max(0,e.i-2)];
          }
        }
      }
    }
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
  function _toRGBA(out, PW, PH){
    PW=PW||W; PH=PH||H;
    const rgba=new Uint8ClampedArray(PW*PH*4);
    for(let i=0;i<PW*PH;i++){
      const c=out[i]; if(!c){ rgba[i*4+3]=0; continue; }
      rgba[i*4]=parseInt(c.slice(1,3),16); rgba[i*4+1]=parseInt(c.slice(3,5),16);
      rgba[i*4+2]=parseInt(c.slice(5,7),16); rgba[i*4+3]=255;
    }
    return rgba;
  }
  function render(dir, opts){
    opts = (typeof opts==='number') ? {elev:opts} : (opts||{});
    return _toRGBA(_paint(F, Object.assign({}, opts, {dir}), true));
  }
  // outboard clamp point (transom top centre) — cell coords; motor layer ships next pass
  const MOUNT = { x:0, y:-L/2, z:T[0][3]+T[0][2] };
  function motorMount(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), p=projVert(MOUNT.x, MOUNT.y-0.03, MOUNT.z, B);
    return { x:p.sx, y:p.sy };
  }
  // helm seat anchor (seated operator sprite sits here, facing the bow)
  const HELM = { x:0, y:-0.86, z:0.68 };
  function helmSeat(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), p=projVert(HELM.x, HELM.y, HELM.z, B);
    return { x:p.sx, y:p.sy };
  }
  // fish-tub deck mounts (boat-local; carries 3 — two aft quarters, one forward of the console)
  const TUBS = [ {x:-0.48,y:-1.95,z:DECK}, {x:0.48,y:-1.95,z:DECK}, {x:0,y:1.30,z:DECK} ];
  function tubMounts(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts);
    return TUBS.map(m=>{ const p=projVert(m.x,m.y,m.z,B); return {x:p.sx, y:p.sy}; });
  }

  root.ConsoleIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:cy}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'], PAINT, TRIM, GOLD, CANV, GLAS, WOOD, IRON, MOTO, KEY,
    render, ROCK, rock:rockMotion, motorMount, MOUNT, helmSeat, HELM, TUBS, tubMounts };
})(typeof globalThis!=='undefined'?globalThis:window);
