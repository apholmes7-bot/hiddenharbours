/* Hidden Harbours — parametric ISO Cape Islander (M2 bake recipe, ADR-0006 — same pipeline as
   consoleIsoRig.js / puntIsoRig.js / doryIsoRig.js). PASS 1: full hull + forward wheelhouse + mast.
   Tier 2, the hub workboat: ~12.8 m LOA displacement lobster boat. The classic Nova-Scotia lines —
   high flared "Cape Island" bow, a sweeping sheer dropping to low freeboard aft, a plumb wide-ish
   transom, and a forward-raked WHEELHOUSE (the dated house — windscreen top overhangs the bow) set forward of
   amidships over a whaleback foredeck, leaving a long open working cockpit aft. Inboard diesel (no outboard layer): a wet-exhaust stack by the house and a
   short signal mast with a hauling boom over the cockpit. Sage-green topsides + dark-green boot,
   gold cove line, cream house, wood cockpit sole — palette-clamped (KTC) to the slice CapeIslander art.
   Fixed 3/4 turntable camera (elev 40deg default, adjustable), 45deg steps, flat-facet shading from a
   fixed upper-left key, z-buffered, ordered dither, 1px keyline post-pass, NO AA. 32 px = 1 m.

   Single cell 456x420, pivot (228,258) = boat origin (amidships, keel bottom, centreline), pinned
   every heading so a direction swap never shifts placement. No outboard (inboard engine). Deck anchors
   are baked from day one: helmSeat(dir,opts) -> wheelhouse operator; haulerMount(dir,opts) -> starboard
   hauling block; tubMounts(dir,opts) -> cockpit tote anchors; navMounts(dir,opts) -> {port,star,stern,
   mast} nav-light points for the night bake. Pass the hull's rock(i) values so overlays ride the wave.
   Exposes globalThis.CapeIslanderIso = { W,H,PX,DIRS,pivot,order,ROCK,rock(i),render(dir,opts),
   helmSeat,HELM, haulerMount,HAULER, tubMounts,TUBS, navMounts,
   HULL,BOOT,CREAM,WOOD,GLAS,GOLD,IRON,MOTO,KEY }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 456, H = 420, cx = 228, cy = 258;   // cell + pivot (projection of boat origin)
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;
  const ROCK = { frames: 8, rollA: 2.6, pitchA: 1.5, heaveA: 1.1, period: 3.4 };  // big displacement hull — slow, stiff roll
  function rockMotion(i, frames){
    frames = frames || ROCK.frames;
    const a = 2*Math.PI*(((i%frames)+frames)%frames)/frames;
    return { roll: ROCK.rollA*Math.sin(a), pitch: ROCK.pitchA*Math.sin(a+Math.PI/2), heave: ROCK.heaveA*Math.sin(a) };
  }
  const L = 12.8, TH = 0.05, DECK = 0.72;
  const NSEG = 24;

  // ---- palette ramps dark->light (KTC: sampled from Art/UI/Roster/CapeIslander.png + Art/Boats/CapeIslander.png) ----
  const HULL  = ['#1e2a24','#2e453a','#3f5e4f','#5c7d6b','#7fa08c','#98b8a3','#b3ccbb'];  // sage-green topsides
  const BOOT  = ['#111b16','#18271f','#22362b','#2d4738','#3a5a48'];                       // dark-green bottom / boot
  const CREAM = ['#8f8974','#a9a28a','#c2bca1','#d5cfb7','#e3ddcb','#efe9d7','#f6f1e3'];  // wheelhouse + buff bulwark liner
  const WOOD  = ['#4a3a2b','#5a4634','#6f6450','#80734f','#94865f','#b3a787','#cdc2a4'];  // cockpit sole / foredeck planking
  const GLAS  = ['#141d22','#22303a','#33424d','#46647a','#6a90a0'];                       // window glass (sea-grey)
  const GOLD  = ['#6f5119','#a8842a','#d9a838','#e7c14a'];                                 // cove line / brass
  const IRON  = ['#141a17','#20291f','#2f3d32','#43554a'];                                 // dark green-black fittings
  const MOTO  = ['#101317','#1d2127','#2b323a','#3d454e','#525c63','#6b767b','#8a9499'];  // mast, stack, metal
  const KEY   = '#0f1712';
  const MATS = { hull:{ramp:HULL,off:0}, boot:{ramp:BOOT,off:0}, cream:{ramp:CREAM,off:0},
                 wood:{ramp:WOOD,off:0}, glas:{ramp:GLAS,off:0}, gold:{ramp:GOLD,off:-1},
                 iron:{ramp:IRON,off:0}, moto:{ramp:MOTO,off:0}, blk:{ramp:MOTO,off:-2}, dark:{ramp:MOTO,off:-3} };
  const RINDEX = {}; [HULL,BOOT,CREAM,WOOD,GLAS,GOLD,IRON,MOTO].forEach(r=>r.forEach((c,i)=>{ RINDEX[c]={r,i}; }));
  const GAIN = 3.0, BIAS = 2.7;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  // ---- offsets table: stern(0) -> bow(8). [sheerHalf, bottomHalf, depth, keelZ] (m) ----
  // sweeping sheer: low aft (freeboard for hauling), rising to a high flared bow. Fine forward entry.
  const T = [
    [1.55,1.02,1.34,0.05],   // 0 transom
    [1.86,1.30,1.36,0.01],   // 1
    [2.02,1.44,1.42,0.00],   // 2
    [2.10,1.48,1.50,0.00],   // 3
    [2.10,1.44,1.62,0.00],   // 4 amidships
    [2.00,1.28,1.80,0.02],   // 5
    [1.74,0.96,2.06,0.09],   // 6
    [1.18,0.52,2.42,0.24],   // 7 flared bow
    [0.10,0.05,2.86,0.50],   // 8 high stem
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
  function dfrac(st){ return Math.max(0.04, Math.min(0.98, (DECK - st.kz)/st.dep)); }

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
      f([P(-1,-1,1),P(1,-1,1),P(1,1,1),P(-1,1,1)]),      // +z top
      f([P(-1,1,-1),P(1,1,-1),P(1,-1,-1),P(-1,-1,-1)]),  // -z bottom
      f([P(-1,1,1),P(1,1,1),P(1,1,-1),P(-1,1,-1)]),      // +y front
      f([P(1,-1,1),P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1)]),  // -y back
      f([P(1,1,1),P(1,-1,1),P(1,-1,-1),P(1,1,-1)]),      // +x right
      f([P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1)]),  // -x left
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
  // proud axis-aligned panels (outward normal along the named axis) for house glazing/joinery
  const DBP = 0.05;
  const frontPanel=(y,xa,xb,za,zb,mat,b)=>({v:[[xa,y,zb],[xb,y,zb],[xb,y,za],[xa,y,za]],mat,b:b||0,db:DBP});
  const backPanel =(y,xa,xb,za,zb,mat,b)=>({v:[[xb,y,zb],[xa,y,zb],[xa,y,za],[xb,y,za]],mat,b:b||0,db:DBP});
  const rightPanel=(x,ya,yb,za,zb,mat,b)=>({v:[[x,yb,zb],[x,ya,zb],[x,ya,za],[x,yb,za]],mat,b:b||0,db:DBP});
  const leftPanel =(x,ya,yb,za,zb,mat,b)=>({v:[[x,ya,zb],[x,yb,zb],[x,yb,za],[x,ya,za]],mat,b:b||0,db:DBP});

  // ---- face list ----
  const F = [];
  const face=(v,mat,b,db)=>F.push({v,mat:mat||'hull',b:b||0,db:db||0});
  const boxF=(c,h,mat,b,db)=>{ F.push.apply(F, box(c,h,mat,b,db)); };
  const tubeF=(A,B2,rad,mat,b)=>{ F.push.apply(F, tube(A,B2,rad,mat,b)); };
  // ---- rounded-corner glazing: octagon glass + dark trim frame, auto-oriented to any face ----
  function objNormal(a,b,c){ const ux=b[0]-a[0],uy=b[1]-a[1],uz=b[2]-a[2], vx=c[0]-a[0],vy=c[1]-a[1],vz=c[2]-a[2];
    return [uy*vz-uz*vy, uz*vx-ux*vz, ux*vy-uy*vx]; }
  function faceO(v, outward, mat, b, db){ const n=objNormal(v[0],v[1],v[2]);
    if(n[0]*outward[0]+n[1]*outward[1]+n[2]*outward[2] < 0) v=v.slice().reverse();
    return {v, mat, b:b||0, db:(db==null?DBP:db)}; }
  function rrect(ua,ub,va,vb,c){ return [[ua+c,va],[ub-c,va],[ub,va+c],[ub,vb-c],[ub-c,vb],[ua+c,vb],[ua,vb-c],[ua,va+c]]; }
  function winRR(mapUV, outward, ua,ub,va,vb, cut, mat, b){
    return faceO(rrect(ua,ub,va,vb,cut).map(([u,v])=>mapUV(u,v)), outward, mat, b); }
  function glaze(mk, outward, ua,ub,va,vb, glassB, cut){ cut=cut||0.10;
    F.push(winRR(mk(0.03),  outward, ua-0.06,ub+0.06, va-0.055,vb+0.055, cut+0.03, 'iron', -0.15));  // dark trim frame
    F.push(winRR(mk(0.065), outward, ua,ub, va,vb, cut, 'glas', glassB));                            // rounded glass
  }
  // outer paint scheme, frac 0(keel)->1(sheer): dark-green boot, sage topsides, gold cove, sheer strake
  const OB = [ [0,0.32,'boot',-0.2,0], [0.32,0.87,'hull',0,0], [0.87,0.92,'gold',0.3,0.01], [0.92,1,'hull',0.18,0] ];

  // wheelhouse envelope (boat coords): forward of amidships, over the whaleback foredeck
  const HX = 1.32, HY0 = 0.5, HY1 = 2.9, HZ0 = DECK, HZ1 = 2.98;   // half-width, aft y, fwd y, floor, eaves
  const ROOFZ = 3.02;

  (function build(){
    // ---- hull shell ----
    for(const side of [-1,1]){
      for(let i=0;i<NSEG;i++){
        const u0=i/NSEG, u1=(i+1)/NSEG;
        for(const [f0,f1,mat,b,db] of OB)
          face([skin(side,u0,f0),skin(side,u1,f0),skin(side,u1,f1),skin(side,u0,f1)],mat,b,db);
        // inner bulwark liner (buff), deck line -> sheer, 2 bands.
        // Only in the open cockpit (aft of the house) — forward of it the liner is hidden by the
        // house + foredeck, and its inset collapses against the flare, z-fighting cream through the
        // green topside. A small negative db keeps the outer skin winning any residual tie.
        const sa=station(u0), sb=station(u1), fa=dfrac(sa), fb=dfrac(sb);
        if(sa.y <= HY0){
          const LT=0.96;   // stop the liner just below the sheer so it can't z-fight the outer skin / covering board at the rail
          for(let k=0;k<2;k++){
            const g0a=fa+(LT-fa)*k/2, g1a=fa+(LT-fa)*(k+1)/2;
            const g0b=fb+(LT-fb)*k/2, g1b=fb+(LT-fb)*(k+1)/2;
            face([skin(side,u1,g0b,1),skin(side,u0,g0a,1),skin(side,u0,g1a,1),skin(side,u1,g1b,1)],'cream',-1.5,-0.03);
          }
        }
        // bottom (boot)
        face([skin(-1,u0,0),skin(-1,u1,0),skin(1,u1,0),skin(1,u0,0)],'boot',-1.0);
        // covering board — dark-brown wood rail cap, widened, runs the full sheer both sides
        const oa=skin(side,u0,1),ob=skin(side,u1,1),ia=skin(side,u0,1,1),ib=skin(side,u1,1,1);
        const inb=(p)=>[p[0]-side*0.32,p[1],p[2]-0.004];
        face([oa,ob,inb(ib),inb(ia)],'wood',-3.7,0.03);
      }
    }
    // ---- cockpit sole (wood), transom -> foredeck bulkhead ----
    const SOLE_U = 0.74;
    const DSEG=20;
    const dw=(u)=>{ const st=station(u); return (lerp(st.wb,st.ws,dfrac(st))-TH)*0.96; };
    for(let i=0;i<DSEG;i++){
      const u0=SOLE_U*i/DSEG, u1=SOLE_U*(i+1)/DSEG;
      face([[-dw(u0),station(u0).y,DECK],[dw(u0),station(u0).y,DECK],[dw(u1),station(u1).y,DECK],[-dw(u1),station(u1).y,DECK]],'wood',-0.35);
    }
    // washboards (dark-brown side decks) along the cockpit sheer, aft of the house.
    // Winding forced up-facing per side so both read the same dark-brown wood (matches the covering board).
    const WB=0.42;
    for(const side of [-1,1]){
      for(let i=0;i<DSEG;i++){
        const u0=SOLE_U*i/DSEG, u1=SOLE_U*(i+1)/DSEG;
        if(station(u0).y > HY0-0.05) continue;   // stop at the house front
        const sa=station(u0), sb=station(u1);
        const xo0=side*(sa.ws-TH), xi0=side*(sa.ws-TH-WB), z0=sa.kz+sa.dep-0.02;
        const xo1=side*(sb.ws-TH), xi1=side*(sb.ws-TH-WB), z1=sb.kz+sb.dep-0.02;
        const q = side>0 ? [[xi0,sa.y,z0],[xo0,sa.y,z0],[xo1,sb.y,z1],[xi1,sb.y,z1]]
                         : [[xo0,sa.y,z0],[xi0,sa.y,z0],[xi1,sb.y,z1],[xo1,sb.y,z1]];
        face(q,'wood',-3.6);
      }
    }
    // ---- transom: paint bands carried across ----
    const tp=(s,f)=>skin(s,0,f);
    for(const [f0,f1,mat,b] of OB)
      face([tp(-1,f1),tp(1,f1),tp(1,f0),tp(-1,f0)], mat, (b||0)-0.8, 0.005);
    (function(){ const s0=station(0), zt=s0.kz+s0.dep, wsx=s0.ws-TH;   // covering board across the transom top (closes the dark-brown rail aft)
      face([[-wsx,s0.y,zt],[wsx,s0.y,zt],[wsx,s0.y+0.26,zt-0.004],[-wsx,s0.y+0.26,zt-0.004]],'wood',-3.6,0.03); })();
    // ---- whaleback foredeck (wood), following the sheer, forward of the house ----
    const FSEG=8, DROP=0.05, FCAP=0.985;
    const fz=(u)=>{ const st=station(u); return st.kz+st.dep-DROP; };
    const fw=(u)=>{ const st=station(u); return Math.max(0.02,(st.ws-TH)*0.92); };
    for(let i=0;i<FSEG;i++){
      const u0=SOLE_U+(FCAP-SOLE_U)*i/FSEG, u1=SOLE_U+(FCAP-SOLE_U)*(i+1)/FSEG;
      face([[-fw(u0),station(u0).y,fz(u0)],[fw(u0),station(u0).y,fz(u0)],[fw(u1),station(u1).y,fz(u1)],[-fw(u1),station(u1).y,fz(u1)]],'wood',0.5,-0.02);
    }
    (function(){ const u=SOLE_U, wv=fw(u)*0.80, z=fz(u), y=station(u).y;   // bulkhead riser under the foredeck lip (inset so its edges clear the hull sheer)
      face([[-wv,y,z],[wv,y,z],[wv,y,DECK],[-wv,y,DECK]],'cream',-1.6,-0.03);
    })();
    boxF([0,5.55,fz(0.965)+0.05],[0.05,0.13,0.05],'iron',0.2,-0.02);   // bow bitt
    // ---- engine box / hatch amidships in the cockpit ----
    boxF([0,-1.25,DECK+0.20],[0.62,0.72,0.20],'cream',-0.2);
    boxF([0,-1.25,DECK+0.41],[0.66,0.76,0.02],'wood',0.5);             // hatch lid
    // ---- stern cleats ----
    for(const s of [-1,1]) boxF([s*(station(0).ws-0.22),-6.05,station(0).kz+station(0).dep+0.03],[0.05,0.09,0.05],'iron',0.15,-0.02);

    // ---- WHEELHOUSE (forward-raked front — the dated house: front-window TOP overhangs toward the bow) ----
    const FYb=2.54, FYt=3.10;                                   // front wall bottom vs top y (top leans forward, to the bow)
    const AL=[-HX,HY0,HZ0], AR=[HX,HY0,HZ0], ALt=[-HX,HY0,HZ1], ARt=[HX,HY0,HZ1];
    const FLb=[-HX,FYb,HZ0], FRb=[HX,FYb,HZ0], FLt=[-HX,FYt,HZ1], FRt=[HX,FYt,HZ1];
    face([AL,ALt,FLt,FLb],'cream',-0.1);                        // port wall (-x)
    face([AR,FRb,FRt,ARt],'cream',-1.0);                        // starboard wall (+x, shaded)
    face([FLt,FRt,FRb,FLb],'cream',0.35);                       // front wall (forward-raked)
    face([AL,AR,ARt,ALt],'cream',-0.7);                         // aft wall (-y, into cockpit)
    boxF([0,(HY0+FYt)/2,ROOFZ+0.05],[HX+0.11,(FYt-HY0)/2+0.11,0.055],'cream',0.6);   // roof slab (overhangs the raked brow)
    // raked-front proud panel helper (outward normal = forward + down, following the rake)
    const _rny=(HZ1-HZ0), _rnz=-(FYt-FYb), _rn=Math.hypot(_rny,_rnz), nY=_rny/_rn, nZ=_rnz/_rn;
    const yFront=(z)=> FYb + (FYt-FYb)*(z-HZ0)/(HZ1-HZ0);
    const frontRaked=(xa,xb,za,zb,mat,b,pr)=>{ pr=(pr==null?0.03:pr);
      const yt=yFront(zb)+nY*pr, yb2=yFront(za)+nY*pr, zt=zb+nZ*pr, zbo=za+nZ*pr;
      return {v:[[xa,yt,zt],[xb,yt,zt],[xb,yb2,zbo],[xa,yb2,zbo]],mat,b:b||0,db:DBP}; };
    // three-pane raked windscreen — rounded corners + dark trim, cream mullions between panes
    for(const [xa,xb] of [[-1.04,-0.50],[-0.34,0.34],[0.50,1.04]])
      glaze((pr)=>((x,z)=>[x, yFront(z)+nY*pr, z+nZ*pr]), [0,nY,nZ], xa,xb, 1.98,2.58, 0.5, 0.05);
    // side windows — two smaller, lightly-rounded, trimmed lights per side
    for(const side of [-1,1]){
      const b0=side<0?-0.15:-1.05, mk=(pr)=>((y,z)=>[side*(HX+pr),y,z]);
      glaze(mk, [side,0,0], HY0+0.26, 1.42, 1.98, 2.44, b0, 0.05);   // aft light
      glaze(mk, [side,0,0], 1.62, 2.36, 1.98, 2.44, b0, 0.05);       // fwd light
    }
    // aft face: door opening (dark) + small stbd light, on the cockpit side (proud)
    const AY=HY0-0.03;
    F.push(backPanel(AY,-0.34,0.40, HZ0+0.02,2.34,'dark',-0.5));         // doorway (open, dark)
    glaze((pr)=>((x,z)=>[x, AY-pr, z]), [0,-1,0], 0.62,HX-0.14, 2.36,2.68, -0.25, 0.05);  // small aft light (rounded+trim)
    // ---- wet-exhaust stack by the house, starboard aft corner ----
    tubeF([0.86,0.34,DECK],[0.86,0.30,2.42],0.085,'moto',-0.1);
    boxF([0.86,0.30,2.46],[0.11,0.11,0.05],'blk',-0.3);                     // stack cap
    // ---- signal mast on the house roof (slightly raked) + spreader + hauling boom over the cockpit ----
    tubeF([0,2.46,ROOFZ+0.04],[0,2.36,4.42],0.055,'moto',0.1);            // mast
    tubeF([-0.42,2.42,3.94],[0.42,2.42,3.94],0.035,'moto',0.0);           // spreader
    tubeF([0,2.40,3.86],[0,0.10,3.08],0.045,'wood',0.35);                 // hauling boom aft over cockpit
    boxF([0,2.36,4.46],[0.05,0.05,0.07],'iron',0.3,-0.02);                 // masthead
    // ---- hauling block on the starboard washboard ----
    (function(){ const st=station(0.30), z=st.kz+st.dep; boxF([1.30,st.y,z+0.10],[0.10,0.12,0.10],'iron',0.2);
      tubeF([1.18,st.y,z+0.14],[1.42,st.y,z+0.14],0.05,'moto',0.3); })();  // roller
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
      const M = MATS[f.mat] || MATS.hull;
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

  // ---- deck anchors (cell coords; pass rock(i) so they ride the wave) ----
  const HELM = { x:0, y:1.35, z:DECK+0.02 };                  // skipper stands at the wheel, forward in the house
  function helmSeat(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), p=projVert(HELM.x, HELM.y, HELM.z, B);
    return { x:p.sx, y:p.sy };
  }
  const HAULER = (function(){ const st=station(0.30); return { x:1.30, y:st.y, z:st.kz+st.dep+0.14 }; })();
  function haulerMount(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), p=projVert(HAULER.x, HAULER.y, HAULER.z, B);
    return { x:p.sx, y:p.sy };
  }
  // cockpit tote anchors (boat-local; two rows aft of the house + one at the transom quarter)
  const TUBS = [ {x:-0.78,y:-1.9,z:DECK}, {x:0.78,y:-1.9,z:DECK},
                 {x:-0.78,y:-3.4,z:DECK}, {x:0.78,y:-3.4,z:DECK}, {x:0,y:-4.8,z:DECK} ];
  function tubMounts(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts);
    return TUBS.map(m=>{ const p=projVert(m.x,m.y,m.z,B); return {x:p.sx, y:p.sy}; });
  }
  // nav-light points for the night bake: bow port/star sidelights, stern light, masthead
  function navMounts(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), s7=station(0.965), s0=station(0);
    const pt=(x,y,z)=>{ const p=projVert(x,y,z,B); return {x:p.sx,y:p.sy}; };
    return {
      port:  pt(-(s7.ws-0.10), s7.y-0.2, s7.kz+s7.dep+0.06),
      star:  pt( (s7.ws-0.10), s7.y-0.2, s7.kz+s7.dep+0.06),
      stern: pt(0, s0.y+0.05, s0.kz+s0.dep+0.10),
      mast:  pt(0, 2.36, 4.46),
    };
  }

  root.CapeIslanderIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:cy}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'], HULL, BOOT, CREAM, WOOD, GLAS, GOLD, IRON, MOTO, KEY,
    render, ROCK, rock:rockMotion, helmSeat, HELM, haulerMount, HAULER, tubMounts, TUBS, navMounts };
})(typeof globalThis!=='undefined'?globalThis:window);
