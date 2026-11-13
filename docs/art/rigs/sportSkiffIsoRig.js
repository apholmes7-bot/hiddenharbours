/* Hidden Harbours — parametric ISO SPORT centre-console skiff (ADR-0006 bake recipe), rebuilt to the
   hull layout (docs/art/hull-layout-spec.md) under her own file name, global and ids.
   The console skiff's sport sister: the same ~7.0 m envelope in fibreglass with a raked sport stem and
   finer entry, gelcoat-smooth with twin stripes high on the topsides, stainless rub rail, bow pulpit,
   stern grabs, a domed bimini on a stainless frame, a leaning post at the helm and a moulded non-skid
   sole (no wood). Remote steer; the outboard is NOT drawn here: it is its own pivoting overlay
   (skiffMotorRig.js) riding motorMount, one for the single and two for the twin.
   Cell 244x216, pivot (122,120), 8 dirs, 45deg steps, upper-left key, ordered dither, 1px keyline,
   NO AA. 32 px = 1 m. Rock loop tuned livelier (lighter glass hull).

   THE HULL (hull id 'sport_skiff_iso', through byId): an OPEN boat. No room (interiorFaces() is empty),
   no door leaf (doorFaces() is empty), no house.
     levels  main_deck  the sole and what stands on it: console, wheel, leaning post, aft bench
             foredeck   the raised casting deck, its bow cleat and the pulpit
             hull       the glass hull, the rub rail, the transom, the motor pad, the stern grab rails
             rigging    the T-top: stainless frame (role rig) and the bimini's canvas (role roof)
     cut     'deck'     nothing lifts
             'cockpit'  the T-top lifts, so a figure under or beside it is seen
     parts   the wheel is face group tag 1 (state wheel:false drops it)
     paint   PAINTS / matsFor(scheme): the gelcoat, the stripes and bottom, and the canvas
   render(dir, opts) paints every face in build order in opts.scheme (default 'gelcoat-white', her
   paint since pass 1). The walk is written to her walk file by tools/rig-recipes/hull-walk.mjs.

   Anchors (px, pass rock(i) values so overlays ride the wave): motorMount(dir,opts),
   helmSeat(dir,opts), tubMounts(dir,opts), pilotStand(dir,opts); in metres MOUNT, HELM, TUBS, PILOT. */
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

  // ramps dark->light (the default scheme's; PAINTS carries the others)
  const PAINT = ['#5d6a70','#7e8c90','#a3b0b1','#c2cdca','#dde5df','#eef0ea','#f7f8f3'];  // white gelcoat
  const TRIM  = ['#0d3f3c','#14554e','#1c7367','#2ba39a','#49b8aa'];                      // teal stripes + bottom
  const STEEL = ['#3a4148','#565f66','#7a858c','#9fabb1','#c3ced2','#e6edee'];            // stainless rails/frame
  const DECKF = ['#6a7069','#848a82','#9ca29a','#b4bab0','#cad0c5'];                      // moulded non-skid sole
  const CANV  = ['#2a5750','#3d7469','#559182','#74ad97','#97c6ab'];                      // T-top canvas + cushions
  const GLAS  = ['#16333c','#24505a','#3a7680','#5fa3a6','#8fc9c4'];                      // windscreen glass
  const MOTO  = ['#101317','#1d2127','#2b323a','#3d454e','#525c63','#6b767b','#8a9499'];  // wheel, dark fittings
  const KEY   = '#101a19';

  // ---- the paint axis (hull-layout spec §5). A scheme sets three ramps: the topsides' gelcoat (mat paint), the
  // stripes and bottom (mat trim) and the canvas of the bimini and cushions (mat canv). The deck mouldings (mat gel:
  // inner skin, console, bench base, motor pad) stay white glass in every scheme, as do steel, sole, glass and wheel.
  const PAINTS = [
    { id: 'gelcoat-white', label: 'GELCOAT WHITE', note: 'White gelcoat, twin teal stripes, teal canvas: her paint since pass 1.',
      paint: PAINT, trim: TRIM, canv: CANV },
    { id: 'atlantic-navy', label: 'ATLANTIC NAVY', note: 'Navy topsides over a white deck, gold stripes, navy canvas.',
      paint: ['#0f1a28','#15263a','#1b324c','#23405f','#30557a','#456d93','#6288ad'],
      trim: ['#6e5626','#8f7235','#b08f48','#c9a45a','#dcbd7c'],
      canv: ['#141f2e','#1f3148','#2b4462','#3b5a7e','#52749a'] },
    { id: 'cranberry', label: 'CRANBERRY', note: 'Cranberry topsides, charcoal stripes and canvas.',
      paint: ['#3e1610','#5a2218','#7a3022','#96402c','#a8452f','#bd5d45','#d07c63'],
      trim: ['#15181b','#1f2428','#2a2f33','#3b4147','#4e555c'],
      canv: ['#1a1d21','#272c31','#353b41','#474e55','#5c646c'] },
    { id: 'squall-grey', label: 'SQUALL GREY', note: 'Grey topsides, a signal-orange stripe, slate canvas.',
      paint: ['#3c4349','#4f575e','#646d74','#7a848b','#919ba1','#a9b2b6','#c2c9cb'],
      trim: ['#7a2e1c','#963c24','#b24c2e','#cc6240','#de8260'],
      canv: ['#2c3137','#3b4249','#4c545c','#5f6871','#747e87'] },
    { id: 'tar-black', label: 'TAR BLACK', note: 'Black topsides, a yellow stripe, oxblood canvas.',
      paint: ['#0b0d10','#12151a','#1a1f25','#242a31','#30373f','#3f4750','#525b64'],
      trim: ['#7c6a2c','#9c873a','#b9a24c','#d1bb66','#e2d08a'],
      canv: ['#5a1f17','#772a1e','#943827','#ad4a34','#c2634a'] },
  ];
  const DEFAULT_SCHEME = 'gelcoat-white';
  const PAINT_BY = {}; PAINTS.forEach(p => { PAINT_BY[p.id] = p; });
  const _mc = {};
  // matsFor(id) is matsFor({ scheme: id }) (or { paint: id }); an unknown id paints the default. Every paint dithers at
  // full scale (dith 1): the painter thresholds every face against BAYER. No room, so no roomMats.
  function matsFor(id) {
    if (id && typeof id === 'object') id = id.scheme != null ? id.scheme : id.paint;
    const key = PAINT_BY[id] ? id : DEFAULT_SCHEME; if (_mc[key]) return _mc[key];
    const P = PAINT_BY[key];
    const MATS = { paint:{ramp:P.paint,off:0,dith:1}, gel:{ramp:PAINT,off:0,dith:1}, trim:{ramp:P.trim,off:-1,dith:1},
                   steel:{ramp:STEEL,off:0,dith:1}, deckf:{ramp:DECKF,off:0,dith:1}, canv:{ramp:P.canv,off:0,dith:1},
                   glas:{ramp:GLAS,off:0,dith:1}, moto:{ramp:MOTO,off:0,dith:1}, blk:{ramp:MOTO,off:-2,dith:1} };
    const RINDEX = {}; [P.paint,PAINT,P.trim,STEEL,DECKF,P.canv,GLAS,MOTO].forEach(r=>r.forEach((c,i)=>{ RINDEX[c]={r,i}; }));
    const ramps = [], at = {}, M = {};
    for (const k in MATS) { const r = MATS[k].ramp, s = r.join(','); if (at[s] == null) { at[s] = ramps.length; ramps.push(r.slice()); }
      M[k] = { ramp: at[s], off: MATS[k].off, dith: MATS[k].dith }; }
    return (_mc[key] = { scheme: key, MATS, RINDEX, ramps, M, roomMats: [] });
  }
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

  // ---- the fittings build() draws and walk() keeps the walker clear of ----
  const CON = { X0:0.34, XT:0.30, Y0:-0.25, Y1:0.55, YT:0.40, ZTF:1.16, ZTA:1.20 };   // the centre console
  const WHEEL = { hub:[0, CON.Y0-0.07, 0.95], rad:0.09 };                             // the wheel on the console's aft face
  const POST = { y:-0.86, seatZ:0.80 };                                               // the leaning post's cushion
  const BENCH = { y:-3.10, seatZ:0.575 };                                             // the aft bench's cushion
  const FORE = { u0:0.80, span:0.185, DROP:0.07 };                                    // the casting deck: its stations, its drop under the sheer
  const CLEAT = [0, 3.16];                                                            // the bow cleat, on the casting deck
  const TWIN_X = 0.34;                                                                // the twin's outboards, each side of the centreline

  // ---- face list. Each face carries the section build() drew it in; faceList() tags by section. ----
  const F = [];
  let cSec = 'hull';
  const sec=(s)=>{ cSec=s; };
  const face=(v,mat,b,db)=>F.push({v,mat:mat||'paint',b:b||0,db:db||0,sec:cSec});
  const boxF=(c,h,mat,b,db)=>{ for(const f of box(c,h,mat,b,db)){ f.sec=cSec; F.push(f); } };
  const tubeF=(A,B2,rad,mat,b)=>{ for(const f of tube(A,B2,rad,mat,b)){ f.sec=cSec; F.push(f); } };
  // sport scheme: gelcoat topsides, twin stripes high on them, stainless rub rail at sheer
  const OB = [ [0,0.35,'paint',0,0], [0.35,0.70,'paint',0,0], [0.70,0.755,'trim',0.25,0.005],
               [0.755,0.80,'paint',0.1,0], [0.80,0.88,'trim',0,0.005], [0.88,1,'paint',0,0] ];
  const fz=(u)=>{ const st=station(u); return st.kz+st.dep-FORE.DROP; };
  (function build(){
    for(const side of [-1,1]){
      for(let i=0;i<NSEG;i++){
        const u0=i/NSEG, u1=(i+1)/NSEG;
        sec('hull');
        for(const [f0,f1,mat,b,db] of OB)
          face([skin(side,u0,f0),skin(side,u1,f0),skin(side,u1,f1),skin(side,u0,f1)],mat,b,db);
        const sa=station(u0), sb=station(u1), fa=dfrac(sa), fb=dfrac(sb);
        for(let k=0;k<2;k++){
          const g0a=fa+(1-fa)*k/2, g1a=fa+(1-fa)*(k+1)/2;
          const g0b=fb+(1-fb)*k/2, g1b=fb+(1-fb)*(k+1)/2;
          face([skin(side,u1,g0b,1),skin(side,u0,g0a,1),skin(side,u0,g1a,1),skin(side,u1,g1b,1)],'gel',-1.6);
        }
        face([skin(-1,u0,0),skin(-1,u1,0),skin(1,u1,0),skin(1,u0,0)],'trim',-1.0);
        // stainless rub rail capping the sheer
        sec('rail');
        const oa=skin(side,u0,1),ob=skin(side,u1,1),ia=skin(side,u0,1,1),ib=skin(side,u1,1,1);
        const inb=(p)=>[p[0]-side*TH*1.4,p[1],p[2]];
        face([oa,ob,inb(ib),inb(ia)],'steel',0.7,0.03);
      }
    }
    // moulded non-skid sole
    sec('sole');
    const DSEG=16;
    const dw=(u)=>{ const st=station(u); return (lerp(st.wb,st.ws,dfrac(st))-TH)*0.96; };
    for(let i=0;i<DSEG;i++){
      const u0=0.80*i/DSEG, u1=0.80*(i+1)/DSEG;
      face([[-dw(u0),station(u0).y,DECK],[dw(u0),station(u0).y,DECK],[dw(u1),station(u1).y,DECK],[-dw(u1),station(u1).y,DECK]],'deckf',-0.35);
    }
    // transom bands
    sec('hull');
    const tp=(s,f)=>skin(s,0,f);
    for(const [f0,f1,mat,b] of OB)
      face([tp(-1,f1),tp(1,f1),tp(1,f0),tp(-1,f0)], mat, (b||0)-0.8, 0.005);
    // moulded motor pad, transom top centre
    sec('pad');
    (function(){ const st=station(0), zt=st.kz+st.dep, zb=st.kz+st.dep*0.45, y=st.y-0.03, hx=0.17;
      face([[-hx,y,zt],[hx,y,zt],[hx,y,zb],[-hx,y,zb]],'gel',-0.3,-0.03);
      face([[-hx,y,zt],[hx,y,zt],[hx,st.y+0.08,zt],[-hx,st.y+0.08,zt]],'gel',0.6,-0.03);
    })();
    // moulded foredeck (non-skid): the casting deck
    sec('foredeck');
    const FSEG=5;
    const fw=(u)=>{ const st=station(u); return Math.max(0.02,(st.ws-TH)*0.94); };
    const fy=(u)=>{ const st=station(u); return st.y+rakeAt(u,(fz(u)-st.kz)/st.dep); };
    for(let i=0;i<FSEG;i++){
      const u0=FORE.u0+FORE.span*i/FSEG, u1=FORE.u0+FORE.span*(i+1)/FSEG;
      face([[-fw(u0),fy(u0),fz(u0)],[fw(u0),fy(u0),fz(u0)],[fw(u1),fy(u1),fz(u1)],[-fw(u1),fy(u1),fz(u1)]],'deckf',0.5);
    }
    sec('step');
    (function(){ const u=FORE.u0, wv=fw(u), z=fz(u), y=station(u).y;
      face([[-wv,y,z],[wv,y,z],[wv,y,DECK],[-wv,y,DECK]],'gel',-1.9);
    })();
    sec('cleat');
    boxF([CLEAT[0],CLEAT[1],fz(0.94)+0.045],[0.028,0.10,0.038],'steel',0.5,-0.02);   // stainless bow cleat
    // ---- bow pulpit rail (stainless): stanchions + raked rail to a centre apex ----
    sec('pulpit');
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
      sec(s<0?'grab_p':'grab_s');
      const z0=0.72, z1=0.98;
      tubeF([s*0.88,-3.05,z0],[s*0.88,-3.02,z1],0.019,'steel',0.3);
      tubeF([s*0.88,-2.45,z0],[s*0.88,-2.48,z1],0.019,'steel',0.3);
      tubeF([s*0.88,-3.02,z1],[s*0.88,-2.48,z1],0.019,'steel',0.5);
    }
    // ---- centre console (moulded, raked windscreen, wheel aft) ----
    sec('console');
    (function(){
      const X0=CON.X0, XT=CON.XT, Y0=CON.Y0, Y1=CON.Y1, YT=CON.YT, Z0=DECK, ZTF=CON.ZTF, ZTA=CON.ZTA;
      const A=[-X0,Y0,Z0], B=[X0,Y0,Z0], C=[X0,Y1,Z0], D=[-X0,Y1,Z0];
      const E=[-XT,Y0,ZTA], Fq=[XT,Y0,ZTA], G=[XT,YT,ZTF], Hq=[-XT,YT,ZTF];
      face([Hq,G,C,D],'gel',0.55,-0.01);
      face([E,Fq,G,Hq],'gel',0.85,-0.01);
      face([A,B,Fq,E],'gel',-0.75,-0.01);
      face([B,C,G,Fq],'gel',-0.15,-0.01);
      face([D,A,E,Hq],'gel',-0.15,-0.01);
      // sport flash on the console front, in the stripes' colour
      face([[-0.26,0.435,1.05],[0.26,0.435,1.05],[0.285,0.475,0.83],[-0.285,0.475,0.83]],'trim',0.35,-0.02);
      face([[-0.225,0.26,1.42],[0.225,0.26,1.42],[0.27,0.40,1.165],[-0.27,0.40,1.165]],'glas',0.9,-0.02);
      tubeF([-0.24,0.255,1.44],[0.24,0.255,1.44],0.021,'steel',0.6);        // windscreen grab rail
      sec('wheel');
      boxF([0,Y0-0.045,WHEEL.hub[2]],[WHEEL.rad,0.018,WHEEL.rad],'blk',0.25,-0.02);
      boxF([WHEEL.hub[0],WHEEL.hub[1],WHEEL.hub[2]],[0.03,0.012,0.03],'steel',0.7,-0.03);
    })();
    // ---- leaning post at the helm: stainless frame + cushion ----
    sec('post');
    (function(){
      const R=0.02;
      for(const s of [-1,1]){
        tubeF([s*0.22,-0.72,DECK],[s*0.20,-0.76,0.76],R,'steel',0.3);
        tubeF([s*0.22,-1.00,DECK],[s*0.20,-0.96,0.76],R,'steel',0.3);
        tubeF([s*0.20,-0.76,0.76],[s*0.20,-0.96,0.76],R,'steel',0.5);
      }
      tubeF([-0.22,POST.y,0.30],[0.22,POST.y,0.30],0.018,'steel',0.2);      // footrest bar
      boxF([0,POST.y,POST.seatZ],[0.27,0.15,0.05],'canv',0.5);              // cushion
      boxF([0,POST.y,0.745],[0.24,0.13,0.012],'gel',0.2);                   // cushion base plate
    })();
    // aft bench: moulded base + cushion
    sec('bench');
    boxF([0,BENCH.y,0.42],[0.62,0.12,0.12],'gel',-0.6);
    boxF([0,BENCH.y,BENCH.seatZ],[0.70,0.155,0.038],'canv',0.45);
    // ---- T-top: stainless frame + low-profile canvas ----
    for(const s of [-1,1]){
      sec(s<0?'leg_aft_p':'leg_aft_s');
      tubeF([s*0.40,-0.88,DECK],[s*0.355,-0.94,1.92],0.023,'steel',0.2);
      sec(s<0?'leg_fwd_p':'leg_fwd_s');
      tubeF([s*0.40, 0.48,DECK],[s*0.355, 0.54,1.92],0.023,'steel',0.2);
      sec(s<0?'brace_p':'brace_s');
      tubeF([s*0.355,-0.94,1.30],[s*0.355, 0.54,1.30],0.018,'steel',0.4);   // side brace
    }
    sec('frame');
    tubeF([-0.355,-0.94,1.89],[0.355,-0.94,1.89],0.019,'steel',0.4);
    tubeF([-0.355, 0.54,1.89],[0.355, 0.54,1.89],0.019,'steel',0.4);
    sec('canopy');
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
  // _paint(faces, opts, MAT): MAT is a scheme's paint table (matsFor). The keyline's darker step reads its RINDEX.
  function _paint(faces, opts, MAT){
    const PW=W, PH=H, MATS=MAT.MATS, RINDEX=MAT.RINDEX;
    const B=camBasis(opts);
    const zbuf=new Float32Array(PW*PH).fill(Infinity);
    const col=new Array(PW*PH).fill(null);
    const dep=new Float32Array(PW*PH);
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
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
    return _toRGBA(_paint(faceList(opts), Object.assign({}, opts, {dir}), matsFor(opts)));
  }
  const MOUNT = { x:0, y:-L/2, z:T[0][3]+T[0][2] };
  function motorMount(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), p=projVert(MOUNT.x, MOUNT.y-0.03, MOUNT.z, B);
    return { x:p.sx, y:p.sy };
  }
  const HELM = { x:0, y:POST.y, z:0.87 };   // leaning-post cushion top
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

  // ==================================================================================================
  // THE HULL LAYOUT (docs/art/hull-layout-spec.md). faceList() gives build()'s faces in build order with the
  // spec's tags, from the section each was drawn in.
  // ==================================================================================================
  const HULL_ID = 'sport_skiff_iso', RIG_FILE = 'sportSkiffIsoRig.js', EXPORT_SYMBOL = 'SportSkiffIso', LABEL = 'SPORT SKIFF';
  const LV = { hull: 0, main_deck: 1, house: 2, bridge: 3, below: 4, rigging: 5, foredeck: 6, top: 7 };
  const RO = { shell: 0, wall: 1, roof: 2, lining: 3, part: 4, floor: 5, fit: 6, tall: 7, trim: 8, rig: 9 };
  const GHOST_ROLES = ['shell', 'wall', 'roof', 'lining', 'part', 'tall', 'trim'];
  // build()'s sections: [level, role, tag]; tag 1 the wheel
  const SECTIONS = { hull: ['hull', 'shell', 0], rail: ['hull', 'trim', 0], pad: ['hull', 'fit', 0], grab_p: ['hull', 'trim', 0], grab_s: ['hull', 'trim', 0],
    sole: ['main_deck', 'floor', 0], step: ['main_deck', 'wall', 0], console: ['main_deck', 'fit', 0], wheel: ['main_deck', 'fit', 1],
    post: ['main_deck', 'fit', 0], bench: ['main_deck', 'fit', 0],
    foredeck: ['foredeck', 'floor', 0], cleat: ['foredeck', 'fit', 0], pulpit: ['foredeck', 'trim', 0],
    leg_aft_p: ['rigging', 'rig', 0], leg_aft_s: ['rigging', 'rig', 0], leg_fwd_p: ['rigging', 'rig', 0], leg_fwd_s: ['rigging', 'rig', 0],
    brace_p: ['rigging', 'rig', 0], brace_s: ['rigging', 'rig', 0], frame: ['rigging', 'rig', 0], canopy: ['rigging', 'roof', 0] };
  const ORDER = ['N','NE','E','SE','S','SW','W','NW'];
  const FRAME = 'metres; +x starboard, +y bow, +z up; origin amidships, keel bottom, centreline; polygons counter-clockwise seen from above';
  // the walker: radius = the largest collider of character rig 10.3's cast (0.23 m), stepTol = v3's, as on every layout hull
  const WALK = { radius: 0.23, stepTol: 0.34 };
  const RULE = 'A step is legal when the target point is inside an area polygon and outside its holes, and |area z - player z| < stepTol. ' +
    'Ramps interpolate z along their axis (x or y) between a0 and a1, and hold z0 / z1 beyond them. cut = the cutaway key while standing there; ' +
    'cutHi / cutLo replace it above / below zMid (stairs and ladders). Polygons are player-centre: already inset by radius, so the reader never insets them again.';
  const WHEEL_GEO = { rad: WHEEL.rad, rake: 0 };          // the wheel plate's half-width; rake 0: it stands vertical on the console's aft face
  // she floats 0.19 m deep (her hull mesh's resting draught since the waterline pass); the sole is 0.09 m over the water
  const DWL = 0.19, COCKPIT_Z = DECK;
  const LOA = L;                                          // her design length, transom to stem at the keel; the raked stem overhangs RAKE more at the sheer
  const FORE_Z = fz(FORE.u0);                             // the casting deck's aft edge, its lowest
  const q3 = (v) => Math.round(v * 1000) / 1000, v3 = (p) => [q3(p[0]), q3(p[1]), q3(p[2])];
  const xyz = (p) => ({ x: q3(p[0]), y: q3(p[1]), z: q3(p[2]) });

  // state: { wheel }; tower: none on this hull
  function stateOf(o) { o = (o && typeof o === 'object') ? o : {}; return { wheel: o.wheel !== false }; }
  function faceList(state, tower) { const s = stateOf(state), out = [];
    for (const f of F) { const t = SECTIONS[f.sec];
      if (t[2] === 1 && !s.wheel) continue;
      const g = { v: f.v.map((p) => p.slice()), mat: f.mat, b: f.b, db: f.db, lv: LV[t[0]], ro: RO[t[1]], it: false }; if (t[2]) g.tag = t[2]; out.push(g); }
    return out; }
  function partFaces(id, state) { const tag = ({ wheel: 1 })[id]; return tag ? faceList(state).filter((f) => f.tag === tag) : []; }
  function interiorFaces() { return []; }
  function doorFaces(t) { return []; }
  function fitCheck(tol) { return { tol: tol == null ? 0.01 : tol, faces: 0, outside: 0, worst: 0, note: 'an open boat: no room faces to check' }; }
  const CUTAWAY = { ghost: { deck: [], cockpit: ['rigging'] }, gate: { deck: [], cockpit: ['rigging'] }, shows: { deck: null, cockpit: null },
    note: 'gate lists are complete lift sets in ghost’s vocabulary (whole levels, or level:role). deck lifts nothing; cockpit lifts the T-top. No key shows a room: she has none.' };
  function geometry() {
    return {
      schema: 'hidden-harbours/hull-geometry@1', rig: RIG_FILE, exportSymbol: EXPORT_SYMBOL, hull: HULL_ID, units: 'metres', frame: FRAME,
      ids: Object.assign({}, LV), roles: Object.assign({}, RO), ghostRoles: GHOST_ROLES.slice(), dwlZ: DWL,
      riggingClass: 'rigging: the T-top. Its stainless legs, braces and cross bars are role rig; the bimini’s canvas is role roof. It lifts under cut key cockpit.',
      levels: [
        { id: 'main_deck', deck: 'cockpit', sole: DECK, ceil: null, kind: 'open', soleZ: DECK, ceilingZ: null,
          ceiling: { kind: 'open', lid: null, note: 'the sole is sky; the T-top over the helm (rigging) lifts under cut key cockpit' } },
        { id: 'foredeck', deck: 'casting_deck', sole: q3(FORE_Z), ceil: null, kind: 'open', soleZ: q3(FORE_Z), ceilingZ: null,
          ceiling: { kind: 'open', lid: null, note: 'the raised casting deck forward: a surface, not a walk area (see _excluded casting_deck)' } },
      ],
      cutaway: JSON.parse(JSON.stringify(CUTAWAY)),
      parts: [
        { id: 'wheel', group: 'tag', tag: 1, state: 'wheel', lv: 'main_deck', ro: 'fit', hub: v3(WHEEL.hub), axis: [0, -1, 0], rake_deg: WHEEL_GEO.rake, radius: WHEEL_GEO.rad,
          note: 'the wheel on the console’s aft face: it stands vertical and turns about its fore-and-aft axis through the hub' },
      ],
      states: { wheel: 'true|false' },
    };
  }
  // renderCut(dir, key, opts): the gate's lift set dropped (whole levels, or level:role), painted as render paints. No
  // key shows a room: she has none
  function renderCut(dir, key, opts) { opts = (typeof opts === 'number') ? { elev: opts } : (opts || {});
    if (!Object.prototype.hasOwnProperty.call(CUTAWAY.gate, key)) throw new Error('renderCut: no cut key ' + key);
    const whole = new Set(), roles = new Map();
    for (const e of CUTAWAY.gate[key]) { const k = e.indexOf(':'); if (k < 0) { whole.add(LV[e]); continue; }
      const l = LV[e.slice(0, k)]; if (!roles.has(l)) roles.set(l, new Set()); roles.get(l).add(RO[e.slice(k + 1)]); }
    const FL = faceList(opts).filter((f) => !(whole.has(f.lv) || (roles.has(f.lv) && roles.get(f.lv).has(f.ro))));
    return _toRGBA(_paint(FL, Object.assign({}, opts, { dir }), matsFor(opts))); }

  // ---- the walk: written to her walk file by tools/rig-recipes/hull-walk.mjs, never by hand ----
  // The floor the walker's disc is held on: the inner skin at the sole's height (the drawn sole stops 4 % short of it),
  // transom to the casting deck's step. A footprint per fitting: the plan box of its section's faces.
  const SOLE_N = 16;
  const soleIn = (u) => { const st = station(u); return lerp(st.wb, st.ws, dfrac(st)) - TH; };
  const FLOOR_POLY = (() => { const rt = [], lt = []; for (let i = 0; i <= SOLE_N; i++) { const u = FORE.u0 * i / SOLE_N, w = soleIn(u), y = station(u).y; rt.push([w, y]); lt.unshift([-w, y]); }
    return rt.concat(lt); })();
  function footprint(name) { const b = { x0: Infinity, x1: -Infinity, y0: Infinity, y1: -Infinity, z0: Infinity, z1: -Infinity };
    for (const f of F) if (f.sec === name) for (const p of f.v) { b.x0 = Math.min(b.x0, p[0]); b.x1 = Math.max(b.x1, p[0]);
      b.y0 = Math.min(b.y0, p[1]); b.y1 = Math.max(b.y1, p[1]); b.z0 = Math.min(b.z0, p[2]); b.z1 = Math.max(b.z1, p[2]); }
    return b; }
  function wPip(p, poly) { let c = false; for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) { const a = poly[i], b = poly[j];
      if ((a[1] > p[1]) !== (b[1] > p[1]) && p[0] < (b[0] - a[0]) * (p[1] - a[1]) / (b[1] - a[1]) + a[0]) c = !c; } return c; }
  function wEdgeD(p, poly) { let d = Infinity; for (let i = 0; i < poly.length; i++) { const a = poly[i], b = poly[(i + 1) % poly.length], dx = b[0] - a[0], dy = b[1] - a[1], L2 = dx * dx + dy * dy;
      const t = L2 ? Math.max(0, Math.min(1, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / L2)) : 0; d = Math.min(d, Math.hypot(p[0] - a[0] - t * dx, p[1] - a[1] - t * dy)); } return d; }
  const wBoxD = (p, b) => Math.hypot(Math.max(b.x0 - p[0], 0, p[0] - b.x1), Math.max(b.y0 - p[1], 0, p[1] - b.y1));
  // what stands in the walker's way between the sole and head height: the canopy (1.56 m over the sole at its binding)
  // and the frame's cross bars are over the helm, where nobody walks
  const OBSTACLES = ['bench', 'post', 'console', 'wheel', 'leg_aft_p', 'leg_aft_s', 'leg_fwd_p', 'leg_fwd_s', 'brace_p', 'brace_s', 'grab_p', 'grab_s'];
  const FP = {}; OBSTACLES.forEach((n) => { FP[n] = footprint(n); });
  // a centre point fits where the walker's disc lies on the floor and clear of every fitting (1e-9: a point exactly
  // a radius from a fitting fits, whatever the subtraction rounds to)
  function walkFits(x, y) { const R = WALK.radius, p = [x, y];
    return wPip(p, FLOOR_POLY) && wEdgeD(p, FLOOR_POLY) >= R - 1e-9 && OBSTACLES.every((n) => wBoxD(p, FP[n]) >= R - 1e-9); }
  function walk() {
    const R = WALK.radius, mm = (v) => Math.round(v * 1000);
    // the fitting interval around a seed on row y, in millimetres: [lo, hi], or null where the seed does not fit
    const row = (y, seed) => { if (!walkFits(seed, y)) return null; let lo = mm(seed), hi = mm(seed);
      while (hi < 2000 && walkFits((hi + 1) / 1000, y)) hi++; while (lo > -2000 && walkFits((lo - 1) / 1000, y)) lo--; return [lo / 1000, hi / 1000]; };
    // the narrowest of the rows ya..yb (every millimetre): the interval that fits on all of them
    const span = (ya, yb, seed) => { let lo = -Infinity, hi = Infinity; for (let k = mm(ya); k <= mm(yb); k++) { const r = row(k / 1000, seed);
        if (!r) throw new Error(RIG_FILE + ': walk: no room at y ' + k / 1000 + ' x ' + seed); lo = Math.max(lo, r[0]); hi = Math.min(hi, r[1]); } return [lo, hi]; };
    const up = (v) => Math.ceil(v * 1000 - 1e-6) / 1000, down = (v) => Math.floor(v * 1000 + 1e-6) / 1000;
    const legOut = FP.leg_fwd_s.x1, legFront = FP.leg_fwd_s.y1;
    // the rows the areas are cut on
    const yAft = up(FP.bench.y1 + R);                     // a radius forward of the aft bench's cushion
    const yRail = up(FP.grab_s.y1 + R);                   // a radius forward of the stern grab rails' forward posts
    const yPost = down(FP.post.y0 - R);                   // a radius aft of the leaning post
    const yLeg = up(legFront);                            // the forward face of the T-top's forward legs
    const yBow = up(Math.max(FP.console.y1, legFront) + R); // a radius forward of the console and the forward legs
    const yStep = down(station(FORE.u0).y - R);           // a radius aft of the casting deck's step
    // the cockpit: the aft bench to the leaning post, as wide as the grab rails and then the hull let it be
    const a = span(yAft, yRail, 0)[1], b = row(yPost, 0)[1];
    const cockpit = { id: 'cockpit', cut: 'cockpit', z: DECK, poly: [[-a, yAft], [a, yAft], [a, yRail], [b, yPost], [-b, yPost], [-a, yRail]], holes: [],
      note: 'the aft bench to the leaning post: ' + q3(2 * a) + ' m of centre room between the stern grab rails, ' + q3(2 * b) + ' m at the post' };
    // the side passages: past the leaning post, the T-top's legs and the console, between them and the hull's side
    const seed = legOut + R + 0.004;
    const lane = span(yPost, yLeg, seed), xi = lane[0], oA = row(yPost, seed)[1], oB = row(yBow, 0)[1], STRIP = 0.03, iB = q3(oB - STRIP);
    // forward of the legs the passage bends in round them toward the bow: a row every 20 mm along the hull's side, STRIP
    // wide at most, its inner edge a millimetre off the legs' corner where that is nearer (its chords cut the arc by less)
    const bend = []; for (let k = mm(yLeg); k < mm(yBow); k += 20) { const last = bend[bend.length - 1], r = row(k / 1000, last ? last[1] : seed);
      if (!r) throw new Error(RIG_FILE + ': walk: the side passage closes at y ' + k / 1000); bend.push([k / 1000, Math.max(q3(r[0] + 0.001), q3(r[1] - STRIP)), r[1]]); }
    const side = (s, id, name) => { const P = [[xi, yPost], [oA, yPost]].concat(bend.map((r) => [r[2], r[0]]), [[oB, yBow], [iB, yBow]], bend.slice().reverse().map((r) => [r[1], r[0]]));
      const poly = s > 0 ? P : P.map((p) => [-p[0], p[1]]).reverse();
      return { id, cut: 'cockpit', z: DECK, poly, holes: [],
        note: 'the ' + name + ' passage past the helm: ' + q3(soleInAt(legFront) - legOut) + ' m between the T-top’s forward leg and the hull’s side, ' + q3(lane[1] - lane[0]) +
          ' m of centre room at the walker’s radius. It widens aft and opens into the bow forward of the console.' }; };
    const starboard = side(1, 'starboard_side', 'starboard'), port = side(-1, 'port_side', 'port');
    // the bow: the sole forward of the console, to the casting deck's step
    const c = row(yBow, 0)[1], d = row(yStep, 0)[1];
    const bow = { id: 'bow', cut: 'deck', z: DECK, poly: [[-c, yBow], [c, yBow], [d, yStep], [-d, yStep]], holes: [],
      note: 'the sole forward of the console, to the casting deck’s step: ' + q3(2 * c) + ' m of centre room aft, ' + q3(2 * d) + ' m at the step' };
    const areas = [cockpit, port, starboard, bow];
    // every outline is checked against the rule it was cut by: each vertex and each edge (every 2 mm) fits
    for (const ar of areas) for (let i = 0; i < ar.poly.length; i++) { const p = ar.poly[i], q = ar.poly[(i + 1) % ar.poly.length], n = Math.max(1, Math.ceil(Math.hypot(q[0] - p[0], q[1] - p[1]) / 0.002));
      for (let k = 0; k <= n; k++) { const x = p[0] + (q[0] - p[0]) * k / n, y = p[1] + (q[1] - p[1]) * k / n;
        if (!walkFits(x, y)) throw new Error(RIG_FILE + ': walk: area ' + ar.id + ' leaves the floor at ' + q3(x) + ', ' + q3(y)); } }
    const spawn = { x: 0, y: q3((yAft + yPost) / 2), area: 'cockpit' };
    const helmGap = q3(FP.wheel.y0 - FP.post.y1), wellGap = q3(FP.bench.y0 - station(0).y), sheer = station(0.5).kz + station(0.5).dep;
    const PERCH = [HELM.x, HELM.y, HELM.z], STAND = [PILOT.x, PILOT.y, DECK], BENCH_SEAT = [0, BENCH.y, q3(FP.bench.z1)];
    const BOW_CLEAT = [CLEAT[0], CLEAT[1], fz(0.94) + 0.045];
    const _excluded = [
      { id: 'helm', of: 'area', z: DECK, reason: 'the standing room at the wheel: ' + helmGap + ' m between the leaning post and the wheel, under the T-top and between its side braces. ' +
        'Narrower than the walker’s ' + q3(2 * R) + ' m: no centre point fits. The helm is taken from the cockpit, behind the post (interact helm); the pilot stands here (anchors.pilot).' },
      { id: 'casting_deck', of: 'area', z: q3(FORE_Z), reason: 'the raised casting deck forward: ' + q3(FORE_Z - DECK) + ' m over the sole at its step (over stepTol ' + WALK.stepTol + ' m) and rising to ' +
        q3(fz(FORE.u0 + FORE.span)) + ' m at the stem. Cockpit-only by the owner’s ruling of 2026-07-22: mooring is a cast line, nobody stands at the bow cleat.' },
      { id: 'transom_well', of: 'area', z: DECK, reason: 'the well between the aft bench and the transom: ' + wellGap + ' m fore and aft, narrower than the walker’s ' + q3(2 * R) +
        ' m. The outboards are worked from the cockpit, over the bench.' },
      { id: 'gunwales', of: 'area', z: q3(sheer), reason: 'the gunwales: a ' + q3(TH * 1.4) + ' m rub-rail cap ' + q3(sheer - DECK) + ' m over the sole and ' + q3(sheer - DWL) +
        ' m over the water. Stepped over to board, not walked. She has no ladder: a swimmer comes aboard over them (ladderOn).', waitsOn: ['ladderOn'] },
      { id: 'leaning_post', of: 'seat', at: xyz(PERCH), reason: 'the leaning post: its cushion, ' + q3(HELM.z - DECK) + ' m over the sole, is the helm’s perch (anchors.helm), not a standing surface. Perching waits on sit.',
        waitsOn: ['sit'] },
      { id: 'aft_bench', of: 'seat', at: xyz(BENCH_SEAT), reason: 'the aft bench across the stern, its cushion ' + q3(FP.bench.z1 - DECK) + ' m over the sole: a seat. Sitting waits on sit.', waitsOn: ['sit'] },
      { id: 'washboards', of: 'washboards', reason: 'an open boat: no side deck. The gunwales are stepped over, not walked (gunwales).' },
      { id: 'grab_rails', of: 'cleat', reason: 'the stern grab rails on each quarter, ' + q3(FP.grab_s.z1 - DECK) + ' m over the sole: handholds, not mooring points. No stern cleat is drawn: she lies to her bow cleat (bow_1), as she has since her first sidecar.' },
      { id: 'pulpit', of: 'cleat', reason: 'the bow pulpit’s stainless rail: a handhold over the casting deck, not a mooring point. The bow line is the cleat’s (bow_1).' },
    ];
    const on = (ar, x, y) => ({ x: q3(x), y: q3(y), z: ar.z });
    const interact = [
      { id: 'helm', action: 'enter_helm', label: 'Helm', prompt: 'Take the wheel', at: xyz(WHEEL.hub), reach: on(cockpit, 0, yPost), area: 'cockpit',
        seat: xyz(PERCH), stand: xyz(STAND),
        note: 'a standing console: the pilot stands between it and the leaning post, whose cushion is the perch (seat). Taken from the cockpit, behind the post.' },
      { id: 'seat_aft_bench', action: 'enter_ride', label: 'Aft bench', prompt: 'Ride', at: xyz(BENCH_SEAT), reach: on(cockpit, 0, yAft), area: 'cockpit', waitsOn: ['sit'] },
    ];
    const cleats = [
      { id: 'bow_1', type: 'cleat', pos: v3(BOW_CLEAT), note: 'the stainless cleat on the casting deck: the bow line, cast to it (nobody stands there)' },
    ];
    const mount = (x) => ({ x: q3(x), y: q3(MOUNT.y), z: q3(MOUNT.z) });
    return {
      schema: 'hidden-harbours/hull-walk@1', rig: RIG_FILE, exportSymbol: EXPORT_SYMBOL, hull: HULL_ID, label: LABEL, kind: 'open',
      units: 'metres', frame: FRAME,
      loa_m: q3(LOA), loh_m: q3(L + RAKE), beam_m: q3(2 * Math.max.apply(null, T.map((t) => t[0]))), DWL, cockpitZ: COCKPIT_Z,
      cell: { W, H, pivot: { x: cx, y: cy }, pxPerM: PX, defaultElev: DEFAULT_ELEV, order: ORDER.slice() },
      tags: { lv: Object.assign({}, LV), ro: Object.assign({}, RO), ghostRoles: GHOST_ROLES.slice() },
      walk: { radius: R, stepTol: WALK.stepTol, rule: RULE, spawn, areas },
      house: null,
      anchors: { helm: xyz(PERCH), wheel: xyz(WHEEL.hub), pilot: xyz(STAND),
        seats: [Object.assign({ id: 'leaning_post' }, xyz(PERCH)), Object.assign({ id: 'aft_bench' }, xyz(BENCH_SEAT))],
        tubs: TUBS.map((t, i) => Object.assign({ id: ['aft_port', 'aft_star', 'bow'][i] }, xyz([t.x, t.y, t.z]))),
        painter: xyz(BOW_CLEAT),
        driveMounts: [mount(MOUNT.x)], driveMountsTwin: [mount(-TWIN_X), mount(TWIN_X)] },
      interact, cleats, washboards: [], _excluded,
      rock: Object.assign({}, ROCK),
      qa: { fitCheck: fitCheck() },
    };
  }
  function soleInAt(y) { return soleIn((y + L / 2) / L); }

  const API = { W, H, PX, DIRS:8, pivot:{x:cx,y:cy}, defaultElev:DEFAULT_ELEV,
    order:ORDER.slice(), PAINT, TRIM, STEEL, DECKF, CANV, GLAS, MOTO, KEY,
    render, ROCK, rock:rockMotion, motorMount, MOUNT, helmSeat, HELM, TUBS, tubMounts, PILOT, pilotStand,
    // the hull layout: the hull, through byId('sport_skiff_iso'); the global carries her members under the same names
    id: HULL_ID, label: LABEL, kind: 'open', hullIds: [HULL_ID], defaultHull: HULL_ID, LV, RO, GHOST_ROLES,
    PAINTS, matsFor, schemeIds: PAINTS.map((p) => p.id), defaultScheme: DEFAULT_SCHEME,
    faceList, partFaces, interiorFaces, doorFaces, geometry, walk, walkFits, fitCheck, renderCut,
    DWL, cockpitZ: COCKPIT_Z, loa: LOA, GAIN, BIAS, LN, BAYER,
    cell: { W, H, pivot: { x: cx, y: cy }, PX, defaultElev: DEFAULT_ELEV }, WHEEL_GEO };
  API.byId = (id) => (id === HULL_ID ? API : null);
  root.SportSkiffIso = API;
})(typeof globalThis!=='undefined'?globalThis:window);
