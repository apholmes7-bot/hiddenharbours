/* Hidden Harbours — parametric ISO SPORT centre-console skiff (M2 bake recipe, ADR-0006 — same
   pipeline as consoleIsoRig.js). PASS 1: hull + console + bimini. The console skiff's sport sister:
   same ~7.0 m envelope in fibreglass with a raked sport stem and finer entry, gelcoat-smooth with
   twin teal stripes, stainless rub rail, bow pulpit, stern grabs, domed bimini on a stainless frame
   (vs the workboat's gabled awning), leaning post at
   the helm and moulded non-skid sole (no wood). Remote steer; outboard ships as its own pivoting
   layer NEXT PASS. Same cell 244x216, pivot (122,120), 8 dirs, 45deg steps, upper-left key, ordered
   dither, 1px keyline, NO AA. 32 px = 1 m. Rock loop tuned livelier (lighter glass hull).
   Anchors: motorMount(dir,opts), helmSeat(dir,opts), tubMounts(dir,opts) — pass rock(i) values so
   overlays ride the wave. Exposes globalThis.SportSkiffIso = { W,H,PX,DIRS,pivot,order,ROCK,rock(i),
   render(dir,opts), motorMount,MOUNT, helmSeat,HELM, TUBS,tubMounts,
   PAINT,TRIM,STEEL,DECKF,CANV,GLAS,MOTO,KEY }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 244, H = 216, cx = 122, cy = 120;
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;
  const ROCK = { frames: 8, rollA: 3.8, pitchA: 2.2, heaveA: 1.5, period: 2.4 };  // light glass hull, livelier
  function rockMotion(i, frames){
    frames = frames || ROCK.frames;
    const a = 2*Math.PI*(((i%frames)+frames)%frames)/frames;
    return { roll: ROCK.rollA*Math.sin(a), pitch: ROCK.pitchA*Math.sin(a+Math.PI/2), heave: ROCK.heaveA*Math.sin(a) };
  }
  const L = 7.0, TH = 0.045, DECK = 0.28;
  const NSEG = 20;

  // ramps dark->light
  const PAINT = ['#5d6a70','#7e8c90','#a3b0b1','#c2cdca','#dde5df','#eef0ea','#f7f8f3'];  // white gelcoat
  const TRIM  = ['#0d3f3c','#14554e','#1c7367','#2ba39a','#49b8aa'];                      // teal stripes + bottom
  const STEEL = ['#3a4148','#565f66','#7a858c','#9fabb1','#c3ced2','#e6edee'];            // stainless rails/frame
  const DECKF = ['#6a7069','#848a82','#9ca29a','#b4bab0','#cad0c5'];                      // moulded non-skid sole
  const CANV  = ['#2a5750','#3d7469','#559182','#74ad97','#97c6ab'];                      // T-top canvas + cushions
  const GLAS  = ['#16333c','#24505a','#3a7680','#5fa3a6','#8fc9c4'];                      // windscreen glass
  const MOTO  = ['#101317','#1d2127','#2b323a','#3d454e','#525c63','#6b767b','#8a9499'];  // wheel, dark fittings
  const KEY   = '#101a19';
  const MATS = { paint:{ramp:PAINT,off:0}, trim:{ramp:TRIM,off:-1}, steel:{ramp:STEEL,off:0},
                 deckf:{ramp:DECKF,off:0}, canv:{ramp:CANV,off:0}, glas:{ramp:GLAS,off:0},
                 moto:{ramp:MOTO,off:0}, blk:{ramp:MOTO,off:-2} };
  const RINDEX = {}; [PAINT,TRIM,STEEL,DECKF,CANV,GLAS,MOTO].forEach(r=>r.forEach((c,i)=>{ RINDEX[c]={r,i}; }));
  const GAIN = 3.0, BIAS = 2.7;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  // ---- offsets: stern(0) -> bow(8). [sheerHalf, bottomHalf, depth, keelZ] — same 7 m envelope,
  // a touch more flare and stem rake than the workboat.
  const T = [
    [0.92,0.74,0.66,0.06],
    [1.05,0.84,0.64,0.01],
    [1.12,0.90,0.63,0.00],
    [1.15,0.92,0.62,0.00],
    [1.13,0.89,0.62,0.00],
    [1.07,0.79,0.65,0.02],
    [0.88,0.52,0.70,0.07],
    [0.58,0.26,0.80,0.18],
    [0.07,0.03,0.84,0.32],
  ];
  const lerp = (a,b,t)=>a+(b-a)*t;
  function station(u){
    const f=Math.max(0,Math.min(8,u*8)), i=Math.min(7,Math.floor(f)), fr=f-i;
    const A=T[i], B=T[i+1];
    return { ws:lerp(A[0],B[0],fr), wb:lerp(A[1],B[1],fr), dep:lerp(A[2],B[2],fr),
             kz:lerp(A[3],B[3],fr), y:-L/2+u*L };
  }
  const RAKE=0.18;   // sport stem: sheer runs forward of the keel at the bow
  const rakeAt=(u,frac)=>RAKE*Math.pow(Math.max(0,(u-0.85)/0.15),1.5)*frac;
  function skin(side,u,frac,inset){
    const st=station(u);
    const ws=st.ws-(inset?TH:0), wb=st.wb-(inset?TH*0.6:0), dep=st.dep-(inset?0.02:0);
    return [ side*lerp(wb,ws,frac), st.y+rakeAt(u,frac), st.kz+lerp(0,dep,frac) ];
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
  // sport scheme: white gelcoat, twin teal stripes high on the topsides, stainless rub rail at sheer
  const OB = [ [0,0.35,'paint',0,0], [0.35,0.70,'paint',0,0], [0.70,0.755,'trim',0.25,0.005],
               [0.755,0.80,'paint',0.1,0], [0.80,0.88,'trim',0,0.005], [0.88,1,'paint',0,0] ];
  (function build(){
    for(const side of [-1,1]){
      for(let i=0;i<NSEG;i++){
        const u0=i/NSEG, u1=(i+1)/NSEG;
        for(const [f0,f1,mat,b,db] of OB)
          face([skin(side,u0,f0),skin(side,u1,f0),skin(side,u1,f1),skin(side,u0,f1)],mat,b,db);
        const sa=station(u0), sb=station(u1), fa=dfrac(sa), fb=dfrac(sb);
        for(let k=0;k<2;k++){
          const g0a=fa+(1-fa)*k/2, g1a=fa+(1-fa)*(k+1)/2;
          const g0b=fb+(1-fb)*k/2, g1b=fb+(1-fb)*(k+1)/2;
          face([skin(side,u1,g0b,1),skin(side,u0,g0a,1),skin(side,u0,g1a,1),skin(side,u1,g1b,1)],'paint',-1.6);
        }
        face([skin(-1,u0,0),skin(-1,u1,0),skin(1,u1,0),skin(1,u0,0)],'trim',-1.0);
        // stainless rub rail capping the sheer
        const oa=skin(side,u0,1),ob=skin(side,u1,1),ia=skin(side,u0,1,1),ib=skin(side,u1,1,1);
        const inb=(p)=>[p[0]-side*TH*1.4,p[1],p[2]];
        face([oa,ob,inb(ib),inb(ia)],'steel',0.7,0.03);
      }
    }
    // moulded non-skid sole
    const DSEG=16;
    const dw=(u)=>{ const st=station(u); return (lerp(st.wb,st.ws,dfrac(st))-TH)*0.96; };
    for(let i=0;i<DSEG;i++){
      const u0=0.80*i/DSEG, u1=0.80*(i+1)/DSEG;
      face([[-dw(u0),station(u0).y,DECK],[dw(u0),station(u0).y,DECK],[dw(u1),station(u1).y,DECK],[-dw(u1),station(u1).y,DECK]],'deckf',-0.35);
    }
    // transom bands
    const tp=(s,f)=>skin(s,0,f);
    for(const [f0,f1,mat,b] of OB)
      face([tp(-1,f1),tp(1,f1),tp(1,f0),tp(-1,f0)], mat, (b||0)-0.8, 0.005);
    // moulded motor pad, transom top centre
    (function(){ const st=station(0), zt=st.kz+st.dep, zb=st.kz+st.dep*0.45, y=st.y-0.03, hx=0.17;
      face([[-hx,y,zt],[hx,y,zt],[hx,y,zb],[-hx,y,zb]],'paint',-0.3,-0.03);
      face([[-hx,y,zt],[hx,y,zt],[hx,st.y+0.08,zt],[-hx,st.y+0.08,zt]],'paint',0.6,-0.03);
    })();
    // moulded foredeck (non-skid)
    const FSEG=5, DROP=0.07;
    const fz=(u)=>{ const st=station(u); return st.kz+st.dep-DROP; };
    const fw=(u)=>{ const st=station(u); return Math.max(0.02,(st.ws-TH)*0.94); };
    const fy=(u)=>{ const st=station(u); return st.y+rakeAt(u,(fz(u)-st.kz)/st.dep); };
    for(let i=0;i<FSEG;i++){
      const u0=0.80+0.185*i/FSEG, u1=0.80+0.185*(i+1)/FSEG;
      face([[-fw(u0),fy(u0),fz(u0)],[fw(u0),fy(u0),fz(u0)],[fw(u1),fy(u1),fz(u1)],[-fw(u1),fy(u1),fz(u1)]],'deckf',0.5);
    }
    (function(){ const u=0.80, wv=fw(u), z=fz(u), y=station(u).y;
      face([[-wv,y,z],[wv,y,z],[wv,y,DECK],[-wv,y,DECK]],'paint',-1.9);
    })();
    boxF([0,3.16,fz(0.94)+0.045],[0.028,0.10,0.038],'steel',0.5,-0.02);   // stainless bow cleat
    // ---- bow pulpit rail (stainless): stanchions + raked rail to a centre apex ----
    (function(){
      const R=0.021;
      const p1=(s)=>[s*0.66, 2.24, 0.888], t1=(s)=>[s*0.63, 2.28, 1.168];   // stanchion 1
      const p2=(s)=>[s*0.41, 2.92, 1.030], t2=(s)=>[s*0.39, 2.95, 1.310];   // stanchion 2
      const apex=[0, 3.44, 1.42];                                            // rides the raked stem
      const aft=(s)=>[s*0.845, 1.68, 0.762];                                 // rail drop to gunwale
      for(const s of [-1,1]){
        tubeF(p1(s), t1(s), R, 'steel', 0.3);
        tubeF(p2(s), t2(s), R, 'steel', 0.3);
        tubeF(aft(s), t1(s), R, 'steel', 0.5);
        tubeF(t1(s), t2(s), R, 'steel', 0.5);
        tubeF(t2(s), apex, R, 'steel', 0.5);
      }
    })();
    // ---- stern grab rails (stainless), each quarter ----
    for(const s of [-1,1]){
      const z0=0.72, z1=0.98;
      tubeF([s*0.88,-3.05,z0],[s*0.88,-3.02,z1],0.019,'steel',0.3);
      tubeF([s*0.88,-2.45,z0],[s*0.88,-2.48,z1],0.019,'steel',0.3);
      tubeF([s*0.88,-3.02,z1],[s*0.88,-2.48,z1],0.019,'steel',0.5);
    }
    // ---- centre console (moulded, raked windscreen, wheel aft) ----
    (function(){
      const X0=0.34, XT=0.30, Y0=-0.25, Y1=0.55, YT=0.40, Z0=DECK, ZTF=1.16, ZTA=1.20;
      const A=[-X0,Y0,Z0], B=[X0,Y0,Z0], C=[X0,Y1,Z0], D=[-X0,Y1,Z0];
      const E=[-XT,Y0,ZTA], Fq=[XT,Y0,ZTA], G=[XT,YT,ZTF], Hq=[-XT,YT,ZTF];
      face([Hq,G,C,D],'paint',0.55,-0.01);
      face([E,Fq,G,Hq],'paint',0.85,-0.01);
      face([A,B,Fq,E],'paint',-0.75,-0.01);
      face([B,C,G,Fq],'paint',-0.15,-0.01);
      face([D,A,E,Hq],'paint',-0.15,-0.01);
      // teal sport flash on the console front
      face([[-0.26,0.435,1.05],[0.26,0.435,1.05],[0.285,0.475,0.83],[-0.285,0.475,0.83]],'trim',0.35,-0.02);
      face([[-0.225,0.26,1.42],[0.225,0.26,1.42],[0.27,0.40,1.165],[-0.27,0.40,1.165]],'glas',0.9,-0.02);
      tubeF([-0.24,0.255,1.44],[0.24,0.255,1.44],0.021,'steel',0.6);        // windscreen grab rail
      boxF([0,Y0-0.045,0.95],[0.09,0.018,0.09],'blk',0.25,-0.02);
      boxF([0,Y0-0.07,0.95],[0.03,0.012,0.03],'steel',0.7,-0.03);
    })();
    // ---- leaning post at the helm: stainless frame + teal cushion ----
    (function(){
      const R=0.02;
      for(const s of [-1,1]){
        tubeF([s*0.22,-0.72,DECK],[s*0.20,-0.76,0.76],R,'steel',0.3);
        tubeF([s*0.22,-1.00,DECK],[s*0.20,-0.96,0.76],R,'steel',0.3);
        tubeF([s*0.20,-0.76,0.76],[s*0.20,-0.96,0.76],R,'steel',0.5);
      }
      tubeF([-0.22,-0.86,0.30],[0.22,-0.86,0.30],0.018,'steel',0.2);        // footrest bar
      boxF([0,-0.86,0.80],[0.27,0.15,0.05],'canv',0.5);                     // cushion
      boxF([0,-0.86,0.745],[0.24,0.13,0.012],'paint',0.2);                  // cushion base plate
    })();
    // aft bench: moulded base + teal cushion
    boxF([0,-3.10,0.42],[0.62,0.12,0.12],'paint',-0.6);
    boxF([0,-3.10,0.575],[0.70,0.155,0.038],'canv',0.45);
    // ---- T-top: stainless frame + low-profile teal canvas ----
    for(const s of [-1,1]){
      tubeF([s*0.40,-0.88,DECK],[s*0.355,-0.94,1.92],0.023,'steel',0.2);
      tubeF([s*0.40, 0.48,DECK],[s*0.355, 0.54,1.92],0.023,'steel',0.2);
      tubeF([s*0.355,-0.94,1.30],[s*0.355, 0.54,1.30],0.018,'steel',0.4);   // side brace
    }
    tubeF([-0.355,-0.94,1.89],[0.355,-0.94,1.89],0.019,'steel',0.4);
    tubeF([-0.355, 0.54,1.89],[0.355, 0.54,1.89],0.019,'steel',0.4);
    (function(){   // domed bimini: cambered athwartships (vs the workboat's fore-aft gable), shallow binding all round
      const Ya=-1.06, Yf=0.68, DROP=0.045;
      const BX=[-0.54,-0.27,0,0.27,0.54], BZ=[1.885,1.965,1.99,1.965,1.885];
      const BB=[0.5,0.32,0.14,-0.02];   // per-strip light, port catches the key
      for(let k=0;k<4;k++)
        face([[BX[k+1],Ya,BZ[k+1]],[BX[k+1],Yf,BZ[k+1]],[BX[k],Yf,BZ[k]],[BX[k],Ya,BZ[k]]],'canv',BB[k]);
      for(let k=0;k<4;k++){
        face([[BX[k],Yf,BZ[k]],[BX[k+1],Yf,BZ[k+1]],[BX[k+1],Yf,BZ[k+1]-DROP],[BX[k],Yf,BZ[k]-DROP]],'canv',-0.5);
        face([[BX[k+1],Ya,BZ[k+1]],[BX[k],Ya,BZ[k]],[BX[k],Ya,BZ[k]-DROP],[BX[k+1],Ya,BZ[k+1]-DROP]],'canv',-1.0);
      }
      face([[-0.54,Ya,1.885],[-0.54,Yf,1.885],[-0.54,Yf,1.84],[-0.54,Ya,1.84]],'canv',-0.6);
      face([[0.54,Yf,1.885],[0.54,Ya,1.885],[0.54,Ya,1.84],[0.54,Yf,1.84]],'canv',-0.8);
    })();
  })();

  // ---- rasterizer (shared recipe) ----
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
  const MOUNT = { x:0, y:-L/2, z:T[0][3]+T[0][2] };
  function motorMount(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), p=projVert(MOUNT.x, MOUNT.y-0.03, MOUNT.z, B);
    return { x:p.sx, y:p.sy };
  }
  const HELM = { x:0, y:-0.86, z:0.87 };   // leaning-post cushion top
  function helmSeat(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), p=projVert(HELM.x, HELM.y, HELM.z, B);
    return { x:p.sx, y:p.sy };
  }
  const TUBS = [ {x:-0.48,y:-1.95,z:DECK}, {x:0.48,y:-1.95,z:DECK}, {x:0,y:1.30,z:DECK} ];
  function tubMounts(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts);
    return TUBS.map(m=>{ const p=projVert(m.x,m.y,m.z,B); return {x:p.sx, y:p.sy}; });
  }

  // pilot foot-contact on the sole, standing at the wheel just aft of the console — rides the wave
  const PILOT = { x:0, y:-0.55 };
  function pilotStand(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), p=projVert(PILOT.x, PILOT.y, DECK, B);
    return { x:p.sx, y:p.sy };
  }

  root.SportSkiffIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:cy}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'], PAINT, TRIM, STEEL, DECKF, CANV, GLAS, MOTO, KEY,
    render, ROCK, rock:rockMotion, motorMount, MOUNT, helmSeat, HELM, TUBS, tubMounts, PILOT, pilotStand };
})(typeof globalThis!=='undefined'?globalThis:window);
