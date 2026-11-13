/* Hidden Harbours — parametric ISO Banks dory (ADR-0006 bake recipe), redrawn on the hull layout
   (docs/art/hull-layout-spec.md) under her own file name, global and ids.
   The boat the game opens on: a 4.5 m flat-bottomed Banks dory. Strong sheer to a high raked stem and a
   narrow raked tombstone transom; straight flared sides over a bottom a little over half her beam; three
   lapped strakes a side under a rail cap and rub strake; sawn frames, bottom boards, three thwarts with
   their knees, a breasthook, quarter knees and a stern knee, thole pins in pairs at two rowing stations,
   a stem-head ring bolt with her painter led aft to a coil, a bailer.
   Cell 160x156, pivot (80,88), 8 dirs, 45deg steps, 40deg camera, upper-left key, ordered dither by
   paint, 1px keyline, NO AA. 32 px = 1 m. Her length, beam, depth, sheer line, cell, pivot and rock are
   pass 1's; the bottom is wider (0.80 m amidships, was 0.52) and both ends rake.

   THE HULL (hull id 'dory_iso', through byId): an OPEN boat. No room (interiorFaces() is empty), no door
   leaf (doorFaces() is empty), no house.
     levels  main_deck  the bottom boards and what stands on them: thwarts, painter coil, bailer
             hull       planking, frames, rail, stem, transom, knees, breasthook, tholes, ring bolts
     cut     'deck'     nothing lifts
     state   motor      true = the outboard build (render also takes build:'motor'): a STERN BENCH and a
                        transom MOTOR BOARD (clamp pad, doubler, cheeks, bolts) join the rowing fit-out.
                        Oars, tholes and thwarts stay: the motor is an upgrade, not a conversion.
     paint   PAINTS / matsFor(scheme): the topsides, the rail and stem, and the inside
   render(dir, opts) paints the build in opts.scheme (default 'pine-tar', her oiled wood since pass 1).
   The walk is written to her walk file by tools/rig-recipes/hull-walk.mjs.

   The small outboard itself is its own overlay layer, doryMotorRig.js (globalThis.DoryMotor), pivot-
   aligned to this hull; this rig publishes the clamp point it registers to: motorMount(dir,opts).
   The oars are a separate layer through the same camera and pivot: renderOars, oarHandles, OAR.

   Anchors (px; pass rock(i) values so overlays ride the wave): motorMount, helmSeat, tubMounts, painter,
   sternEye, pilotStand; in metres MOUNT, HELM, TUBS, PAINTER, STERN_EYE, PILOT, PILOT_MOTOR. */
(function (root) {
  const PX = 32, S = 32;                 // px per metre / projection scale
  const W = 160, H = 156, cx = 80, cy = 88;   // cell + pivot (= projection of boat origin)
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;               // camera elevation, degrees above the horizon. Overridable per render.
  const ROCK = { frames: 8, rollA: 5.0, pitchA: 3.0, heaveA: 1.6, period: 2.6 };  // gentle wave-rock loop
  function rockMotion(i, frames){        // one frame of the rock cycle -> {roll,pitch,heave}
    frames = frames || ROCK.frames;
    const a = 2*Math.PI*(((i%frames)+frames)%frames)/frames;
    return { roll: ROCK.rollA*Math.sin(a), pitch: ROCK.pitchA*Math.sin(a+Math.PI/2), heave: ROCK.heaveA*Math.sin(a) };
  }
  const L = 4.5, TH = 0.035, FLOOR = 0.06, SEAT = 0.30, OARLOCK_U = 0.31;
  const STERN_U = 0.13;                  // stern-bench station (motor build) -> y = -1.665 m, 0.58 m fwd of the transom
  const FWD_LOCK_U = 0.57;               // the forward rowing station's tholes, the same 0.135 m abaft its thwart
  const THWARTS = [0.34, 0.60], BOW_THWART_U = 0.80;
  const HANDLINE_U = 0.47;               // the hand-line chock on the rail, between the rowing thwarts
  const BAILER = { x: 0.20, y: -1.05 };  // the bailer's place on the boards, abaft the after thwart
  const NSEG = 18;                       // length segments
  const FR = [0, 0.36, 0.69, 1];         // strake edges up the side: garboard, broad strake, sheer strake
  const SEAM = 0.085;                    // the lap's shadow under each edge, as a share of the side's height
  const BOARD_T = 0.03;                  // bottom boards over the bilge
  const CAP_RISE = 0.012, RUB = 0.022, INWALE = 0.03;   // rail cap over the sheer; rub strake outboard; inwale inboard
  const RAKE_AFT = 0.26, RAKE_AFT_U = 0.20;             // the tombstone: its foot 0.26 m forward of its head
  const RAKE_BOW = 0.42, RAKE_BOW_U = 0.70;             // the stem: its foot 0.42 m abaft its head

  // wood ramp dark->light (pass 1's, sampled from Art/Boats/Dory.png) + keyline + iron
  const RAMP = ['#33271b','#473627','#5e4630','#6b4f35','#8a6a48','#9a7853','#a98352'];
  const KEY = '#1c140d';
  const IRON = ['#20180f','#2a2014','#3a2c1c'];
  const PINE = ['#4f4130','#665540','#7d6950','#937d60','#a89172','#b9a384','#c8b496'];   // scrubbed boards: thwarts, bottom boards
  const OAK  = ['#4b3925','#675030','#846940','#a18453','#b99b67','#ccb17d','#ddc695'];   // worn rail cap (the default trim)
  const TAR  = ['#1d1610','#281f16','#34281c','#413223','#4e3d2b','#5b4832','#68533a'];   // tarred inside (the default inner)
  const ROPE = ['#6f6040','#8d7b53','#a99567','#c3af7f','#d7c699'];                       // hemp painter
  const TIN  = ['#33393b','#474f53','#5e676c','#78828a','#949da3'];                       // the bailer

  // ---- the paint axis (hull-layout spec §5). A scheme sets three ramps: the topsides and transom (mat paint), the
  // rail cap, rub strake and stem (mat trim) and the inside planking (mat inner). Frames, knees, thwarts, bottom
  // boards, iron, rope and tin are the same in every scheme.
  const PAINTS = [
    { id: 'pine-tar', label: 'PINE TAR', note: 'Oiled wood outside, a worn oak rail, tarred inside: her paint since pass 1.',
      paint: RAMP, trim: OAK, inner: TAR },
    { id: 'dory-buff', label: 'DORY BUFF', note: 'Dory buff topsides and inside, a green rail and stem: the Banks schooner’s dory.',
      paint: ['#5f4b22','#7e652c','#9d8038','#ba9b48','#d0b35e','#e0c779','#ebd897'],
      trim: ['#12261a','#183524','#20472f','#2b5b3c','#39714c','#4c8760','#659c78'],
      inner: ['#3b301b','#4f4124','#64532e','#7a663a','#8f7947','#a28b56','#b39d67'] },
    { id: 'harbour-white', label: 'HARBOUR WHITE', note: 'White topsides, a red rail and stem, grey inside.',
      paint: ['#5f6a6e','#808c8e','#a4afae','#c3ccc7','#dde3dc','#eeefe8','#f7f7f1'],
      trim: ['#3e1610','#5a2218','#7a3022','#96402c','#a8452f','#bd5d45','#d07c63'],
      inner: ['#353b3f','#464e52','#596266','#6d777b','#828c90','#97a1a4','#adb5b7'] },
    { id: 'squall-grey', label: 'SQUALL GREY', note: 'Workboat grey topsides, a white rail and stem, tarred inside.',
      paint: ['#33393f','#444c53','#586169','#6d777f','#848e95','#9ca5aa','#b5bcbf'],
      trim: ['#66706f','#869090','#a7b0ad','#c4ccc6','#dce2da','#edefe7','#f6f6f0'],
      inner: TAR },
    { id: 'tar-black', label: 'TAR BLACK', note: 'Tarred black topsides, an ochre rail and stem, tarred inside.',
      paint: ['#0b0c0e','#121417','#1a1d21','#24282d','#2f353b','#3d444b','#4f575e'],
      trim: ['#5c4a1e','#7a6228','#987c34','#b59644','#cdae5a','#dec274','#ead492'],
      inner: TAR },
  ];
  const DEFAULT_SCHEME = 'pine-tar';
  const PAINT_BY = {}; PAINTS.forEach(p => { PAINT_BY[p.id] = p; });
  const _mc = {};
  // matsFor(id) is matsFor({ scheme: id }) (or { paint: id }); an unknown id paints the default. dith is the share of
  // the Bayer threshold a paint takes: 0 = crisp bands (paint, iron), a little grain on bare wood and rope.
  function tableOf(MATS) {
    const RINDEX = {}; for (const k in MATS) MATS[k].ramp.forEach((c, i) => { if (!RINDEX[c]) RINDEX[c] = { r: MATS[k].ramp, i }; });
    const ramps = [], at = {}, M = {};
    for (const k in MATS) { const r = MATS[k].ramp, s = r.join(','); if (at[s] == null) { at[s] = ramps.length; ramps.push(r.slice()); }
      M[k] = { ramp: at[s], off: MATS[k].off, dith: MATS[k].dith }; }
    return { MATS, RINDEX, ramps, M, roomMats: [] };
  }
  function matsFor(id) {
    if (id && typeof id === 'object') id = id.scheme != null ? id.scheme : id.paint;
    const key = PAINT_BY[id] ? id : DEFAULT_SCHEME; if (_mc[key]) return _mc[key];
    const P = PAINT_BY[key], bare = key === DEFAULT_SCHEME;
    const T = tableOf({ paint:{ramp:P.paint,off:0,dith:bare?0.25:0}, trim:{ramp:P.trim,off:0,dith:bare?0.2:0}, inner:{ramp:P.inner,off:0,dith:0.2},
                        wood:{ramp:RAMP,off:0,dith:0.25}, boards:{ramp:PINE,off:0,dith:0.25}, iron:{ramp:IRON,off:-2,dith:0},
                        rope:{ramp:ROPE,off:-1,dith:0.3}, tin:{ramp:TIN,off:-1,dith:0} });
    T.scheme = key; return (_mc[key] = T);
  }
  // the oars' table: pass 1's inline ramp choice, written out (wood = RAMP, iron = IRON two steps down), full dither
  const OAR_MATS = tableOf({ wood:{ramp:RAMP,off:0,dith:1}, iron:{ramp:IRON,off:-2,dith:1} });
  const GAIN = 3.0, BIAS = 2.7;
  // light (screen basis: right, up, toward-camera) — upper-left key, a touch frontal
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  // ---- offsets table: stern(0) -> bow(8). [sheerHalf, bottomHalf, depth, keelZ] (m). The sheer's plan and profile
  // are pass 1's forward of the transom (so every anchor on it stands where it stood); the transom's head is the
  // narrow tombstone's (0.68 m, was 0.88) and the bottom is a dory's, a little over half the beam.
  const T = [
    [0.34,0.10,0.56,0.14],
    [0.60,0.30,0.50,0.06],
    [0.70,0.37,0.47,0.02],
    [0.74,0.40,0.46,0.00],
    [0.75,0.39,0.46,0.00],
    [0.72,0.35,0.47,0.01],
    [0.63,0.27,0.49,0.05],
    [0.45,0.15,0.53,0.14],
    [0.05,0.025,0.60,0.26],
  ];
  const lerp = (a,b,t)=>a+(b-a)*t;
  function station(u){
    const f=Math.max(0,Math.min(8,u*8)), i=Math.min(7,Math.floor(f)), fr=f-i;
    const A=T[i], B=T[i+1];
    return { ws:lerp(A[0],B[0],fr), wb:lerp(A[1],B[1],fr), dep:lerp(A[2],B[2],fr),
             kz:lerp(A[3],B[3],fr), y:-L/2+u*L };
  }
  // the ends rake: the sheer keeps its station, the bottom draws in under it (forward at the transom, aft at the stem)
  const rakeAft = (u) => RAKE_AFT*Math.pow(Math.max(0,(RAKE_AFT_U-u)/RAKE_AFT_U),1.5);
  const rakeBow = (u) => RAKE_BOW*Math.pow(Math.max(0,(u-RAKE_BOW_U)/(1-RAKE_BOW_U)),1.7);
  const rakeAt = (u,frac) => (rakeAft(u)-rakeBow(u))*(1-frac);
  // hull-skin point. side=+/-1, frac 0(bottom)->1(sheer). inset -> inner skin.
  function skin(side,u,frac,inset){
    const st=station(u);
    const ws=st.ws-(inset?TH:0), wb=st.wb-(inset?TH*0.6:0), dep=st.dep-(inset?0.02:0);
    return [ side*lerp(wb,ws,frac), st.y+rakeAt(u,frac), st.kz+lerp(0,dep,frac) ];
  }
  // inner half-width AT a given height — thwarts/benches must fit the flare at their LOWEST face,
  // otherwise the ends poke out through the planking (the hull narrows as you go down).
  function halfAt(u,z,inset){
    const st=station(u);
    const ws=st.ws-(inset?TH:0), wb=st.wb-(inset?TH*0.6:0), dep=st.dep-(inset?0.02:0);
    return lerp(wb, ws, Math.max(0,Math.min(1,(z-st.kz)/dep)));
  }
  const sheerZ = (u) => { const st=station(u); return st.kz+st.dep; };
  // the bilge floor runs out to the inner skin at its own height
  const floorHalf = (u) => Math.max(0, halfAt(u, station(u).kz+FLOOR, true) - 0.004);
  function floorPt(side,u){ const st=station(u); return [ side*floorHalf(u), st.y+rakeAt(u,FLOOR/st.dep), st.kz+FLOOR ]; }
  // the transom's planes at a height: its outer face, and its inner face a plank's thickness forward
  const TR = { y:-L/2, zTop:T[0][3]+T[0][2], zFoot:T[0][3], halfW:T[0][0] };
  const transomY = (z) => TR.y + RAKE_AFT*(1-Math.max(0,Math.min(1,(z-TR.zFoot)/T[0][2])));

  // ---- face list (boat space), built once. Each face carries the section it was drawn in (its level and role). ----
  const F = [];    // hull + rowing fit-out (both builds)
  const FM = [];   // motor-upgrade extras (state motor)
  let SEC = 'hull', OUT = F;
  // the corners in the order that turns the face's normal along n (the painter lights a face by its winding)
  function facing(v,n){
    const a=v[0],b=v[1],c=v[2], ux=b[0]-a[0],uy=b[1]-a[1],uz=b[2]-a[2], vx=c[0]-a[0],vy=c[1]-a[1],vz=c[2]-a[2];
    const nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
    return (nx*n[0]+ny*n[1]+nz*n[2])<0 ? v.slice().reverse() : v;
  }
  const face=(v,mat,b,db,n)=>OUT.push({v:n?facing(v,n):v,mat:mat||'wood',b:b||0,db:db||0,sec:SEC});
  function box(c,h,mat,b,db){            // axis-aligned block, outward winding
    const P=(sx,sy,sz)=>[c[0]+sx*h[0], c[1]+sy*h[1], c[2]+sz*h[2]];
    const f=(v)=>OUT.push({v,mat,b:b||0,db:db||0,sec:SEC});
    f([P(-1,-1,1),P(1,-1,1),P(1,1,1),P(-1,1,1)]);
    f([P(-1,1,-1),P(1,1,-1),P(1,-1,-1),P(-1,-1,-1)]);
    f([P(-1,1,1),P(1,1,1),P(1,1,-1),P(-1,1,-1)]);
    f([P(1,-1,1),P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1)]);
    f([P(1,1,1),P(1,-1,1),P(1,-1,-1),P(1,1,-1)]);
    f([P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1)]);
  }
  function ringBolt(x,y,z,ro,ri,tk,b){    // 4-bar iron ring; nothing under the middle, so the hole reads as wood
    const bar=(ro-ri)/2, off=(ro+ri)/2;
    box([x,y+off,z],[ro,bar,tk],'iron',b,-0.05);                     // fwd bar
    box([x,y-off,z],[ro,bar,tk],'iron',b-0.3,-0.05);                 // aft bar
    box([x-off,y,z],[bar,ri,tk],'iron',b,-0.05);                     // port bar
    box([x+off,y,z],[bar,ri,tk],'iron',b-0.25,-0.05);                // stbd bar
  }
  // a thwart: a board across the boat on the planking, its ends cut to the flare at its lower face
  function thwart(u,bd,th,topB,mar){
    const st=station(u), zTop=st.kz+SEAT, zBot=zTop-th, hx=halfAt(u,zBot,1)-(mar||0.006);
    const y0=st.y-bd/2, y1=st.y+bd/2;
    face([[-hx,y0,zTop],[hx,y0,zTop],[hx,y1,zTop],[-hx,y1,zTop]],'boards',topB,0,[0,0,1]);   // top
    face([[-hx,y1,zTop],[hx,y1,zTop],[hx,y1,zBot],[-hx,y1,zBot]],'wood',-0.5,0,[0,1,0]);      // fwd edge
    face([[hx,y0,zTop],[-hx,y0,zTop],[-hx,y0,zBot],[hx,y0,zBot]],'wood',-0.9,0,[0,-1,0]);     // aft edge
    face([[-hx,y0,zBot],[hx,y0,zBot],[hx,y1,zBot],[-hx,y1,zBot]],'wood',-1.4,0,[0,0,-1]);     // underside
    return { hx, y0, y1, zTop, zBot };
  }
  const SEAT_AT = {};   // thwart name -> its top's box, for the walk
  (function build(){
    // ---- planking: three lapped strakes a side outside, the same seams inside, the bottom and the bilge floor ----
    for(const side of [-1,1]){
      const out=[side,0,-0.5], inn=[-side,0,0.5];
      for(let i=0;i<NSEG;i++){
        const u0=i/NSEG, u1=(i+1)/NSEG;
        for(let k=0;k<FR.length-1;k++){
          const f0=FR[k], f1=FR[k+1];
          SEC='hull';
          face([skin(side,u0,f0),skin(side,u1,f0),skin(side,u1,f1),skin(side,u0,f1)],'paint',k===2?0.25:0,0,out);
          // the lap: the shadow each strake's lower edge throws on the one below (biased forward so it wins the coplanar test)
          if(k>0) face([skin(side,u0,f0-SEAM),skin(side,u1,f0-SEAM),skin(side,u1,f0),skin(side,u0,f0)],'paint',-2.2,0.02,out);
          SEC='lining';
          face([skin(side,u0,f0,1),skin(side,u1,f0,1),skin(side,u1,f1,1),skin(side,u0,f1,1)],'inner',0,0,inn);
          if(k>0) face([skin(side,u0,f0,1),skin(side,u1,f0,1),skin(side,u1,f0+SEAM,1),skin(side,u0,f0+SEAM,1)],'inner',-1.6,0.02,inn);
        }
        // the rail: a cap over the sheer, the rub strake's face outboard of it, the inwale's inboard
        SEC='rail';
        const zc0=sheerZ(u0)+CAP_RISE, zc1=sheerZ(u1)+CAP_RISE, s0=station(u0), s1=station(u1);
        const xo0=side*(s0.ws+RUB), xo1=side*(s1.ws+RUB), xi0=side*Math.max(0,s0.ws-TH-INWALE), xi1=side*Math.max(0,s1.ws-TH-INWALE);
        face([[xo0,s0.y,zc0],[xo1,s1.y,zc1],[xi1,s1.y,zc1],[xi0,s0.y,zc0]],'trim',0.55,0.03,[0,0,1]);
        face([[xo0,s0.y,zc0],[xo1,s1.y,zc1],[xo1,s1.y,zc1-0.06],[xo0,s0.y,zc0-0.06]],'trim',-1.3,0.02,[side,0,0]);
        face([[xi0,s0.y,zc0],[xi1,s1.y,zc1],[xi1,s1.y,zc1-0.05],[xi0,s0.y,zc0-0.05]],'trim',-1.1,0.02,[-side,0,0]);
      }
    }
    for(let i=0;i<NSEG;i++){
      const u0=i/NSEG, u1=(i+1)/NSEG;
      SEC='hull';   face([skin(-1,u0,0),skin(-1,u1,0),skin(1,u1,0),skin(1,u0,0)],'paint',-1.0,0,[0,0,-1]);       // the flat bottom
      SEC='lining'; face([floorPt(-1,u0),floorPt(1,u0),floorPt(1,u1),floorPt(-1,u1)],'inner',-0.7,0,[0,0,1]);   // the bilge
    }
    // ---- the tombstone transom: raked, narrow, its head capped; a sculling notch in the rowing boat ----
    SEC='transom';
    const SL=skin(-1,0,1), SR=skin(1,0,1), BR=skin(1,0,0), BL=skin(-1,0,0), fw=(p)=>[p[0],p[1]+TH,p[2]];
    face([SL,SR,BR,BL],'paint',-0.2,0,[0,-1,-0.4]);
    face([fw(SL),fw(SR),fw(BR),fw(BL)],'inner',-0.3,0,[0,1,0.4]);
    const zh=TR.zTop+CAP_RISE, hh=TR.halfW+RUB;
    face([[-hh,TR.y-0.012,zh],[hh,TR.y-0.012,zh],[hh,TR.y+TH+0.02,zh],[-hh,TR.y+TH+0.02,zh]],'trim',0.55,0.035,[0,0,1]);
    SEC='notch';
    face([[-0.05,TR.y-0.014,zh+0.002],[0.05,TR.y-0.014,zh+0.002],[0.05,TR.y+TH+0.022,zh+0.002],[-0.05,TR.y+TH+0.022,zh+0.002]],'iron',0,0.05,[0,0,1]);
    face([[-0.05,transomY(TR.zTop),TR.zTop],[0.05,transomY(TR.zTop),TR.zTop],[0.05,transomY(TR.zTop-0.07),TR.zTop-0.07],[-0.05,transomY(TR.zTop-0.07),TR.zTop-0.07]],'iron',0,0.05,[0,-1,-0.4]);
    // ---- the stem: its face closes the two skins; its head stands over the breasthook ----
    SEC='stem';
    face([skin(-1,1,0),skin(1,1,0),[0.05+RUB,L/2,sheerZ(1)],[-0.05-RUB,L/2,sheerZ(1)]],'trim',-0.1,0.012,[0,1,-0.7]);
    box([0,L/2-0.035,sheerZ(1)+0.045],[0.045,0.04,0.06],'trim',-0.3,0.03);
    // ---- sawn frames: one a side at each station, standing proud of the planking ----
    SEC='frames';
    for(let j=1;j<=7;j++) for(const side of [-1,1]){
      const u=j/8, w=0.024/L, P=(uu,f)=>{ const p=skin(side,uu,f,1); return [p[0]-side*0.028,p[1],p[2]]; };
      face([P(u-w,0.04),P(u+w,0.04),P(u+w,0.94),P(u-w,0.94)],'wood',0.3,0.02,[-side,0,0.5]);
    }
    // ---- bottom boards: two a side of the keel line, the outer pair nibbed to the bilge ----
    SEC='boards';
    for(const sx of [-1,1]) for(const lane of [[0.018,0.165],[0.195,0.345]]){
      const NB=14, uA=0.10, uB=0.87;
      for(let i=0;i<NB;i++){
        const ua=uA+(uB-uA)*i/NB, ub=uA+(uB-uA)*(i+1)/NB, pa=floorPt(1,ua), pb=floorPt(1,ub);
        const xa=Math.min(lane[1],pa[0]-0.025), xb=Math.min(lane[1],pb[0]-0.025);
        if(xa-lane[0]<0.05 || xb-lane[0]<0.05) continue;
        face([[sx*lane[0],pa[1],pa[2]+BOARD_T],[sx*xa,pa[1],pa[2]+BOARD_T],[sx*xb,pb[1],pb[2]+BOARD_T],[sx*lane[0],pb[1],pb[2]+BOARD_T]],'boards',0.1,0.03,[0,0,1]);
      }
    }
    // ---- thwarts: two rowing thwarts and a short bow thwart, each with a standing knee at either end ----
    const names=['thwart_aft','thwart_fwd'];
    THWARTS.forEach((u,i)=>{ SEC=names[i]; const t=thwart(u,0.22,0.045,0.5); SEAT_AT[names[i]]=t;
      for(const sx of [-1,1]) box([sx*(t.hx-0.03),station(u).y,t.zTop+0.04],[0.03,0.075,0.04],'wood',-0.3,0); });
    SEC='thwart_bow'; { const t=thwart(BOW_THWART_U,0.18,0.04,0.5); SEAT_AT.thwart_bow=t; }
    // ---- breasthook (the plate that closes the stem) + the stem-head RING BOLT the painter lives on ----
    SEC='breasthook';
    { const bh=(side,u)=>[side*Math.max(0,station(u).ws-TH-INWALE+0.004),station(u).y,sheerZ(u)+0.004];
      face([bh(-1,0.89),bh(1,0.89),bh(1,1),bh(-1,1)],'wood',0.9,0.02,[0,0,1]);
      face([bh(-1,0.89),bh(1,0.89),[bh(1,0.89)[0],bh(1,0.89)[1],bh(1,0.89)[2]-0.05],[bh(-1,0.89)[0],bh(-1,0.89)[1],bh(-1,0.89)[2]-0.05]],'wood',-0.8,0.02,[0,-1,0]); }
    SEC='ring_bow';
    (function(){ const st=station(0.965); ringBolt(0, st.y, st.kz+st.dep+0.03, 0.052, 0.022, 0.015, 0.6); })();
    // the painter: from the ring aft over the breasthook, down to the boards, to a coil abaft the bow thwart
    SEC='painter';
    { const st=station(0.965), hw=0.017, run=[[st.y-0.03,st.kz+st.dep+0.03],[station(0.89).y-0.01,sheerZ(0.89)+0.012],
        [station(0.845).y,station(0.845).kz+FLOOR+BOARD_T+0.012],[station(0.745).y,station(0.745).kz+FLOOR+BOARD_T+0.012]];
      for(let i=0;i+1<run.length;i++) face([[-hw,run[i][0],run[i][1]],[hw,run[i][0],run[i][1]],[hw,run[i+1][0],run[i+1][1]],[-hw,run[i+1][0],run[i+1][1]]],'rope',0.2,0.04,[0,-0.3,1]);
      const cy0=station(0.715).y, cz=station(0.715).kz+FLOOR+BOARD_T+0.03, N=8, ro=0.15, ri=0.07;
      for(let i=0;i<N;i++){ const a=2*Math.PI*i/N, b=2*Math.PI*(i+1)/N, P=(r,t)=>[r*Math.cos(t),cy0+r*Math.sin(t),cz];
        face([P(ri,a),P(ro,a),P(ro,b),P(ri,b)],'rope',(i%2)?0.5:-0.2,0.04,[0,0,1]); } }
    // ---- quarter knees (sheer-level plates tying the transom corners into the sides) + the port-quarter ring ----
    SEC='knees';
    for(const side of [-1,1]){
      const uf=0.085, c=[side*(TR.halfW-TH),TR.y+TH,TR.zTop], f=[side*(station(uf).ws-TH-INWALE),station(uf).y,sheerZ(uf)], inb=[side*0.10,TR.y+TH,TR.zTop];
      face([c,f,inb],'wood',0.8,0.04,[0,0,1]);
    }
    SEC='ring_stern';
    ringBolt(-0.30, -2.16, TR.zTop+0.026, 0.05, 0.018, 0.014, 0.5);
    // ---- the stern knee: the bracket on the centreline that ties the transom to the bottom ----
    SEC='stern_knee';
    { const hx=0.028, zA=TR.zFoot+0.36, zB=TR.zFoot+FLOOR, A=(x)=>[x,transomY(zA)+TH,zA], B=(x)=>[x,transomY(zB)+TH,zB];
      const uc=(transomY(zB)+TH+0.34+L/2)/L, C=(x)=>[x,-L/2+uc*L,station(uc).kz+FLOOR+0.005];
      face([A(hx),B(hx),C(hx)],'wood',-0.1,0.01,[1,0,0]); face([A(-hx),B(-hx),C(-hx)],'wood',-0.6,0.01,[-1,0,0]);
      face([A(-hx),A(hx),C(hx),C(-hx)],'wood',0.5,0.01,[0,0.6,0.8]); }
    // ---- thole pins: a pair a side at each rowing station, the oar's loom lying between them ----
    SEC='tholes';
    for(const lu of [OARLOCK_U,FWD_LOCK_U]) for(const side of [-1,1]) for(const dy of [-0.05,0.05]){
      const st=station(lu), z0=st.kz+st.dep+CAP_RISE;
      box([side*(st.ws-TH*0.5),st.y+dy,z0+0.058],[0.015,0.015,0.058],'iron',0.5,0.02);
    }
    // ---- the hand-line chock: a chafing block on the starboard rail between the thwarts, where the line comes in ----
    SEC='chock';
    { const st=station(HANDLINE_U); box([st.ws-TH*0.5,st.y,sheerZ(HANDLINE_U)+CAP_RISE+0.016],[0.034,0.075,0.016],'wood',-1.2,0.02); }
    // ---- the bailer: a tin scoop on the boards, abaft the after thwart and clear of the centreline ----
    SEC='bailer';
    { const u=(BAILER.y+L/2)/L, z=station(u).kz+FLOOR+BOARD_T; box([BAILER.x,BAILER.y,z+0.04],[0.06,0.085,0.04],'tin',0.4,0.02);
      box([BAILER.x,BAILER.y-0.125,z+0.055],[0.014,0.045,0.012],'tin',-0.6,0.02); }
  })();

  // ---- motor upgrade (state motor) ----
  // A clamp-on outboard needs two things this dory hasn't got: somewhere aft to SIT with the tiller in
  // reach, and enough transom to clamp to. The board stands plumb on the raked tombstone's head.
  const BOARD = { halfW:0.30, t:0.045, rise:0.10, drop:0.16 };   // clamp pad: 0.60 x 0.26 m, 45 mm thick, stands 100 mm proud of the sheer
  (function buildMotor(){
    OUT = FM;
    // stern bench — the new seating position (deeper than a rowing thwart so it reads as a seat, not a brace)
    SEC='bench';
    SEAT_AT.bench = thwart(STERN_U,0.26,0.055,0.75,0.016);
    // outer motor board: bolted across the transom's head, standing proud of the sheer = the clamp face.
    SEC='motor_board';
    const zT=TR.zTop+BOARD.rise, zB=TR.zTop-BOARD.drop, yA=TR.y-BOARD.t*2, yF=TR.y, hw=BOARD.halfW;
    face([[-hw,yA,zT],[hw,yA,zT],[hw,yF,zT],[-hw,yF,zT]],'wood',0.95,-0.06,[0,0,1]);      // top cap
    face([[hw,yA,zT],[-hw,yA,zT],[-hw,yA,zB],[hw,yA,zB]],'wood',0.45,-0.05,[0,-1,0]);     // aft clamp face
    face([[-hw,yF,zT],[hw,yF,zT],[hw,yF,TR.zTop],[-hw,yF,TR.zTop]],'wood',-0.4,-0.05,[0,1,0]);   // fwd face over the sheer
    for(const sx of [-1,1]){                                                              // end grain + the wedge to the raked transom
      face([[sx*hw,yF,zT],[sx*hw,yA,zT],[sx*hw,yA,zB],[sx*hw,yF,zB]],'wood',0.2,-0.05,[sx,0,0]);
      face([[sx*hw,yF,TR.zTop],[sx*hw,yF,zB],[sx*hw,transomY(zB),zB]],'wood',-0.2,-0.05,[sx,0,0]);
    }
    face([[-hw,yA,zB],[hw,yA,zB],[hw,transomY(zB),zB],[-hw,transomY(zB),zB]],'wood',-1.2,-0.05,[0,0,-1]);   // underside
    // inner doubler + two cheeks — spread the clamp load into the transom
    box([0, TR.y+0.075, TR.zTop-0.09],[0.275, 0.05, 0.07],'wood',-0.15,-0.05);
    for(const sx of [-1,1]) box([sx*0.245, TR.y+0.14, TR.zTop-0.1075],[0.035, 0.065, 0.0625],'wood',-0.35,-0.05);
    // bolt heads through the board (4)
    for(const sx of [-1,1]) for(const bzz of [TR.zTop-0.09, TR.zTop+0.045])
      box([sx*0.205, TR.y-BOARD.t*2-0.004, bzz],[0.022, 0.006, 0.022],'iron',0.6,-0.06);
    // motor lanyard eye on the inner doubler (the safety line that keeps a kicker out of the harbour)
    box([-0.115, TR.y+0.075, TR.zTop-0.005],[0.025,0.02,0.03],'iron',0.55,-0.06);
    OUT = F;
  })();

  // ---- shared rasterizer ----
  function normal(a,b,c){
    const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
    const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m];
  }
  function shadeOf(n, se, ce){
    let sh = n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2];
    return sh;
  }
  function camBasis(opts){
    const dir=opts.dir||0, th=dir*Math.PI/4;
    const e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    const roll=(opts.roll||0)*DEG, pitch=(opts.pitch||0)*DEG;
    return { ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e),
      cr:Math.cos(roll), sr:Math.sin(roll), cq:Math.cos(pitch), sq:Math.sin(pitch), heave:(opts.heave||0) };
  }
  function projVert(x,y,z,B){
    const x1=x*B.cr+z*B.sr, z1=-x*B.sr+z*B.cr;      // roll about fore-aft (Y)
    const y2=y*B.cq - z1*B.sq, z2=y*B.sq + z1*B.cq;  // pitch about beam (X)
    const xr=x1*B.ct - y2*B.stt, yr=x1*B.stt + y2*B.ct, zr=z2;
    return { xr,yr,zr, sx:cx+xr*S, sy:cy-(yr*B.se+zr*B.ce)*S - B.heave, d:(yr*B.ce-zr*B.se) };
  }
  // _paint(faces, opts, MAT, doEdge): MAT is a paint table (matsFor, or the oars'). A paint's dith is the share of the
  // Bayer threshold it takes (1 = pass 1's full ordered dither, 0 = a clean band edge at the half step).
  function _paint(faces, opts, MAT, doEdge){
    const MATS=MAT.MATS, RINDEX=MAT.RINDEX, DFLT=MATS.paint||MATS.wood;
    const B=camBasis(opts);
    const zbuf=new Float32Array(W*H).fill(Infinity);
    const col=new Array(W*H).fill(null);
    const dep=new Float32Array(W*H);
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
      let n=normal(rv[0],rv[1],rv[2]);
      let sh=shadeOf(n, B.se, B.ce); if(sh<0 && ((f.b||0)<=-1)) sh=shadeOf([-n[0],-n[1],-n[2]], B.se, B.ce)*0.9; // interior two-sided
      const fidx = sh*GAIN + BIAS + (f.b||0);
      const M = MATS[f.mat] || DFLT, dith = M.dith==null ? 1 : M.dith, ramp = M.ramp, rl = ramp.length-1;
      const base=Math.floor(fidx), fr=fidx-base;
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
            const thr = dith<=0 ? 0.5 : 0.5+(BAYER[x&3][y&3]-0.5)*dith;
            const idx=base+(fr>thr?1:0)+M.off;
            col[i]=ramp[idx<0?0:idx>rl?rl:idx];
          }
        }
      }
    }
    const out=new Array(W*H).fill(null);
    for(let i=0;i<W*H;i++) out[i]=col[i];
    if(doEdge){
      // inner depth-edge separation: darken the farther side of a big depth step
      for(let y=0;y<H;y++) for(let x=0;x<W;x++){
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
    }
    // keyline outline (external)
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
  function render(dir, opts){
    opts = (typeof opts==='number') ? {elev:opts} : (opts||{});
    return _toRGBA(_paint(faceList(opts), Object.assign({}, opts, {dir}), matsFor(opts), true));
  }

  // outboard clamp point — top centre of the motor board's aft (clamp) face, in hull-cell coords.
  // The small-motor layer registers its swivel axis to this point.
  const MOUNT = { x:0, y:TR.y-BOARD.t*2, z:TR.zTop+BOARD.rise };
  function motorMount(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), p=projVert(MOUNT.x, MOUNT.y-0.01, MOUNT.z, B);
    return { x:p.sx, y:p.sy };
  }
  // seated operator anchor: stern-bench top centre (motor build)
  const HELM = (()=>{ const st=station(STERN_U); return { x:0, y:st.y, z:st.kz+SEAT }; })();
  function helmSeat(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), p=projVert(HELM.x, HELM.y, HELM.z, B);
    return { x:p.sx, y:p.sy };
  }

  // ---- oars (separate overlay layer, same camera + pivot as the hull) ----
  const OAR = { rowFrames: 8, states: ['row','resting','trailing'] };
  const O_LOUT=1.45, O_LIN=0.85, O_TS=0.055, O_BLEN=0.5, O_BW=0.12;   // outboard/inboard len, loom radius, blade len/max half-width (m)
  const v_sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]], v_add=(a,b)=>[a[0]+b[0],a[1]+b[1],a[2]+b[2]];
  const v_mul=(a,s)=>[a[0]*s,a[1]*s,a[2]*s], v_norm=(a)=>{const m=Math.hypot(a[0],a[1],a[2])||1;return [a[0]/m,a[1]/m,a[2]/m];};
  const v_cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
  function oarlockPt(side){ const st=station(OARLOCK_U); return [ side*(st.ws-TH*0.15), st.y, st.kz+st.dep+0.04 ]; }
  function oarDir(side, sweepDeg, dipDeg){ const f=sweepDeg*DEG, p=dipDeg*DEG; return [ side*Math.cos(f)*Math.cos(p), -Math.sin(f)*Math.cos(p), -Math.sin(p) ]; }
  function oarPose(state, t){
    if(state==='resting')  return { sweep:-82, dip:-7 };   // shipped fore, lying along the gunwale
    if(state==='trailing') return { sweep: 76, dip: 15 };  // dragging aft in the water
    const tt=(((t||0)%1)+1)%1, a=2*Math.PI*tt;             // row cycle: blade traces an ellipse (dip+sweep)
    return { sweep: 30*Math.sin(a), dip: 6 + 22*Math.cos(a) };
  }
  function buildOar(side, pose){
    const O=oarlockPt(side), d=oarDir(side,pose.sweep,pose.dip);
    const Bt=v_add(O, v_mul(d, O_LOUT));                    // blade tip
    const Hp=v_sub(O, v_mul(d, O_LIN));                     // handle (straight lever through the lock)
    const ax=v_norm(v_sub(Bt,Hp)); let up=[0,0,1]; if(Math.abs(ax[2])>0.9) up=[0,1,0];
    const r=v_norm(v_cross(ax,up)), u=v_cross(r,ax);
    const ring=(P,rad)=>[ v_add(v_add(P,v_mul(r,rad)),v_mul(u,rad)), v_add(v_add(P,v_mul(r,-rad)),v_mul(u,rad)),
                          v_add(v_add(P,v_mul(r,-rad)),v_mul(u,-rad)), v_add(v_add(P,v_mul(r,rad)),v_mul(u,-rad)) ];
    const P0=v_add(Hp,v_mul(ax,0.02)), P1=v_sub(Bt,v_mul(ax,O_BLEN)), faces=[];
    const r0=ring(P0,O_TS), r1=ring(P1,O_TS);
    for(let k=0;k<4;k++){ const k2=(k+1)%4; faces.push({v:[r0[k],r0[k2],r1[k2],r1[k]],mat:'wood',b:0.3,db:-0.15}); }  // loom
    // paddle-shaped blade: narrow throat -> shoulder swell -> rounded tip (width profile along blade)
    const prof=[[0,0.05],[0.25,0.085],[0.6,O_BW],[0.85,O_BW*0.92],[1,0.035]];
    let prev=null;
    for(const [s,w] of prof){
      const P=v_add(P1,v_mul(ax,O_BLEN*s)), e0=v_add(P,v_mul(r,w)), e1=v_add(P,v_mul(r,-w));
      if(prev){ const q=[prev[0],e0,e1,prev[1]], sh=-0.35-0.1*s;
        faces.push({v:q,mat:'wood',b:sh,db:-0.2}); faces.push({v:[q[3],q[2],q[1],q[0]],mat:'wood',b:sh,db:-0.2}); }
      prev=[e0,e1];
    }
    // an oar is a fitting on the hull: every face declares the hull level, as the layout asks of any face this rig hands over
    faces.forEach((f)=>{ f.lv = LV.hull; });
    return faces;
  }
  function sidePose(opts, side){ const o=(side<0?opts.port:opts.star)||{}; return oarPose(o.state||opts.state||'row', (o.t!==undefined?o.t:opts.t)||0); }
  function oarFaces(opts){ const ss = opts.side==='port'?[-1]:opts.side==='star'?[1]:[-1,1];
    return ss.reduce((f,s)=>f.concat(buildOar(s,sidePose(opts,s))),[]); }
  function renderOars(dir, opts){
    opts = (typeof opts==='number') ? {elev:opts} : (opts||{});
    return _toRGBA(_paint(oarFaces(opts), Object.assign({}, opts, {dir}), OAR_MATS, false));
  }
  function oarHandles(dir, opts){   // per-side handle screen position (cell coords) for attaching a character's hands
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), res=[];
    for(const side of [-1,1]){ const pose=sidePose(opts,side), O=oarlockPt(side), d=oarDir(side,pose.sweep,pose.dip);
      const Hp=v_sub(O,v_mul(d,O_LIN)), p=projVert(Hp[0],Hp[1],Hp[2],B); res.push({side:side<0?'port':'star', x:p.sx, y:p.sy}); }
    return res;
  }

  // ---- fish-tub deck mount (one, between the thwarts — cargo rides behind the rower) ----
  const TUBS = [ {x:0,y:-0.14} ].map(m=>{ const st=station((m.y+L/2)/L); return {x:m.x,y:m.y,z:st.kz+FLOOR}; });
  function tubMounts(dir, opts){   // hull-cell px anchors; pass rock(i) so anchors ride the wave (incl. heave)
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts);
    return TUBS.map(m=>{ const p=projVert(m.x,m.y,m.z,B); return {x:p.sx, y:p.sy}; });
  }

  // bow painter eye — mooring / tow / docking line attach (both builds), in hull-cell coords
  const PAINTER = (()=>{ const st=station(0.965); return { x:0, y:st.y, z:st.kz+st.dep+0.055 }; })();
  function painter(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), p=projVert(PAINTER.x, PAINTER.y, PAINTER.z, B);
    return { x:p.sx, y:p.sy };
  }
  // port-quarter eye — the stern line when she lies alongside (both builds)
  const STERN_EYE = { x:-0.30, y:-2.16, z:T[0][3]+T[0][2]+0.045 };
  function sternEye(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), p=projVert(STERN_EYE.x, STERN_EYE.y, STERN_EYE.z, B);
    return { x:p.sx, y:p.sy };
  }
  // pilot foot-contact on the sole — rowing station (feet planted to work the oars), or the motor
  // station just forward of the stern bench when build:'motor'. Rides the wave.
  const PILOT = { x:0, y:-0.30 };
  const PILOT_MOTOR = { x:0, y:-1.28 };
  function pilotStand(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const P = (opts.build==='motor' || opts.motor===true) ? PILOT_MOTOR : PILOT;
    const st=station((P.y+L/2)/L), B=camBasis(opts), p=projVert(P.x, P.y, st.kz+FLOOR, B);
    return { x:p.sx, y:p.sy };
  }

  // ==================================================================================================
  // THE HULL LAYOUT (docs/art/hull-layout-spec.md). faceList() gives the build's faces in build order with the
  // spec's tags, from the section each was drawn in.
  // ==================================================================================================
  const HULL_ID = 'dory_iso', RIG_FILE = 'doryIsoRig.js', EXPORT_SYMBOL = 'DoryIso', LABEL = 'BANKS DORY';
  const LV = { hull: 0, main_deck: 1, house: 2, bridge: 3, below: 4, rigging: 5, foredeck: 6, top: 7 };
  const RO = { shell: 0, wall: 1, roof: 2, lining: 3, part: 4, floor: 5, fit: 6, tall: 7, trim: 8, rig: 9 };
  const GHOST_ROLES = ['shell', 'wall', 'roof', 'lining', 'part', 'tall', 'trim'];
  // build()'s sections: [level, role]
  const SECTIONS = { hull: ['hull', 'shell'], lining: ['hull', 'lining'], transom: ['hull', 'shell'], notch: ['hull', 'trim'], rail: ['hull', 'trim'], stem: ['hull', 'trim'],
    frames: ['hull', 'fit'], breasthook: ['hull', 'fit'], knees: ['hull', 'fit'], stern_knee: ['hull', 'fit'], tholes: ['hull', 'fit'], chock: ['hull', 'fit'],
    ring_bow: ['hull', 'fit'], ring_stern: ['hull', 'fit'], motor_board: ['hull', 'fit'],
    boards: ['main_deck', 'floor'], thwart_aft: ['main_deck', 'fit'], thwart_fwd: ['main_deck', 'fit'], thwart_bow: ['main_deck', 'fit'],
    painter: ['main_deck', 'fit'], bailer: ['main_deck', 'fit'], bench: ['main_deck', 'fit'] };
  const ORDER = ['N','NE','E','SE','S','SW','W','NW'];
  const FRAME = 'metres; +x starboard, +y bow, +z up; origin amidships, keel bottom, centreline; polygons counter-clockwise seen from above';
  // the walker: radius = the largest collider of character rig 10.3's cast (0.23 m), stepTol = v3's, as on every layout hull
  const WALK = { radius: 0.23, stepTol: 0.34 };
  const RULE = 'A step is legal when the target point is inside an area polygon and outside its holes, and |area z - player z| < stepTol. ' +
    'Ramps interpolate z along their axis (x or y) between a0 and a1, and hold z0 / z1 beyond them. cut = the cutaway key while standing there; ' +
    'cutHi / cutLo replace it above / below zMid (stairs and ladders). Polygons are player-centre: already inset by radius, so the reader never insets them again.';
  const q3 = (v) => Math.round(v * 1000) / 1000, q4 = (v) => Math.round(v * 10000) / 10000, v3 = (p) => [q3(p[0]), q3(p[1]), q3(p[2])];
  const xyz = (p) => ({ x: q4(p[0]), y: q4(p[1]), z: q4(p[2]) });
  // she floats 0.11 m deep (her hull mesh's resting draught since the waterline pass); the boards amidships are 0.02 m under it
  const DWL = 0.11, COCKPIT_Z = q3(FLOOR + BOARD_T);
  const LOA = L;                                          // transom head to stem head

  // state: { motor }; render's build:'motor' is the same switch. tower: none on this hull
  function stateOf(o) { o = (o && typeof o === 'object') ? o : {}; return { motor: o.motor === true || o.build === 'motor' }; }
  function tagged(f) { const t = SECTIONS[f.sec]; return { v: f.v.map((p) => p.slice()), mat: f.mat, b: f.b, db: f.db, lv: LV[t[0]], ro: RO[t[1]], it: false }; }
  function faceList(state, tower) { const s = stateOf(state), out = [];
    for (const f of F) { if (s.motor && f.sec === 'notch') continue; out.push(tagged(f)); }      // the board stands where the sculling notch is cut
    if (s.motor) for (const f of FM) out.push(tagged(f));
    return out; }
  function partFaces(id, state) { return id === 'motor_fit' ? FM.map(tagged) : []; }
  function interiorFaces() { return []; }
  function doorFaces(t) { return []; }
  function fitCheck(tol) { return { tol: tol == null ? 0.01 : tol, faces: 0, outside: 0, worst: 0, note: 'an open boat: no room faces to check' }; }
  const CUTAWAY = { ghost: { deck: [] }, gate: { deck: [] }, shows: { deck: null },
    note: 'gate lists are complete lift sets in ghost’s vocabulary (whole levels, or level:role). deck lifts nothing: an open boat with nothing over her.' };
  function geometry() {
    return {
      schema: 'hidden-harbours/hull-geometry@1', rig: RIG_FILE, exportSymbol: EXPORT_SYMBOL, hull: HULL_ID, units: 'metres', frame: FRAME,
      ids: Object.assign({}, LV), roles: Object.assign({}, RO), ghostRoles: GHOST_ROLES.slice(), dwlZ: DWL,
      levels: [
        { id: 'main_deck', deck: 'bottom_boards', sole: COCKPIT_Z, ceil: null, kind: 'open', soleZ: COCKPIT_Z, ceilingZ: null,
          ceiling: { kind: 'open', lid: null, note: 'an open boat: the boards are under the sky' } },
      ],
      cutaway: JSON.parse(JSON.stringify(CUTAWAY)),
      parts: [
        { id: 'motor_fit', group: 'state', state: 'motor', lv: 'hull', ro: 'fit', mount: v3([MOUNT.x, MOUNT.y, MOUNT.z]),
          note: 'the outboard build’s additions: the stern bench (main_deck, fit) and the transom motor board with its doubler, cheeks and bolts (hull, fit). partFaces(\'motor_fit\') gives them alone; the sculling notch leaves when they come' },
      ],
      states: { motor: 'true|false: the outboard build (render and the anchors also take build: \'motor\')' },
    };
  }
  // renderCut(dir, key, opts): the gate's lift set dropped (whole levels, or level:role), painted as render paints
  function renderCut(dir, key, opts) { opts = (typeof opts === 'number') ? { elev: opts } : (opts || {});
    if (!Object.prototype.hasOwnProperty.call(CUTAWAY.gate, key)) throw new Error('renderCut: no cut key ' + key);
    const whole = new Set(), roles = new Map();
    for (const e of CUTAWAY.gate[key]) { const k = e.indexOf(':'); if (k < 0) { whole.add(LV[e]); continue; }
      const l = LV[e.slice(0, k)]; if (!roles.has(l)) roles.set(l, new Set()); roles.get(l).add(RO[e.slice(k + 1)]); }
    const FL = faceList(opts).filter((f) => !(whole.has(f.lv) || (roles.has(f.lv) && roles.get(f.lv).has(f.ro))));
    return _toRGBA(_paint(FL, Object.assign({}, opts, { dir }), matsFor(opts), true)); }

  // ---- the walk: written to her walk file by tools/rig-recipes/hull-walk.mjs, never by hand ----
  // She is a boat of stations more than a deck, but a figure does move in her: along the bottom boards, stepping
  // over the thwarts (0.21 m over the boards, under stepTol). A centre point fits where it is over the bilge floor
  // and the walker's disc is inside the planking at thwart height, where the flare has opened the hull.
  const OUTLINE_N = 48;
  function outline(half, frac) { const rt = [], lt = [];
    for (let i = 0; i <= OUTLINE_N; i++) { const u = 0.01 + 0.98 * i / OUTLINE_N, st = station(u), w = half(u), y = st.y + rakeAt(u, frac(st)); rt.push([w, y]); lt.unshift([-w, y]); }
    return rt.concat(lt); }
  const FLOOR_POLY = outline(floorHalf, (st) => FLOOR / st.dep);
  const HIP_POLY = outline((u) => halfAt(u, station(u).kz + SEAT, true), (st) => SEAT / st.dep);
  function wPip(p, poly) { let c = false; for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) { const a = poly[i], b = poly[j];
      if ((a[1] > p[1]) !== (b[1] > p[1]) && p[0] < (b[0] - a[0]) * (p[1] - a[1]) / (b[1] - a[1]) + a[0]) c = !c; } return c; }
  function wEdgeD(p, poly) { let d = Infinity; for (let i = 0; i < poly.length; i++) { const a = poly[i], b = poly[(i + 1) % poly.length], dx = b[0] - a[0], dy = b[1] - a[1], L2 = dx * dx + dy * dy;
      const t = L2 ? Math.max(0, Math.min(1, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / L2)) : 0; d = Math.min(d, Math.hypot(p[0] - a[0] - t * dx, p[1] - a[1] - t * dy)); } return d; }
  function walkFits(x, y) { const p = [x, y];
    return wPip(p, FLOOR_POLY) && wPip(p, HIP_POLY) && wEdgeD(p, HIP_POLY) >= WALK.radius - 1e-9; }
  function walk() {
    const R = WALK.radius, mm = (v) => Math.round(v * 1000), END = 0.02, ROW = 0.25;
    // the widest half-breadth that fits on row y, in millimetres (0 where even the centreline does not)
    const half = (y) => { let h = 0; while (h < 1000 && walkFits((h + 1) / 1000, y)) h++; return walkFits(0, y) ? h / 1000 : 0; };
    // the ends: the last rows, a millimetre at a time, where a strip END either side of the centreline still fits
    let ya = mm(PILOT.y), yb = ya; while (ya > -3000 && half((ya - 1) / 1000) >= END) ya--; while (yb < 3000 && half((yb + 1) / 1000) >= END) yb++;
    const n = Math.ceil((yb - ya) / 1000 / ROW), rows = [];
    for (let i = 0; i <= n; i++) { const y = (ya + (yb - ya) * i / n) / 1000, yq = q3(y); rows.push([Math.max(END, Math.floor((half(yq) - 0.002) * 1000 + 1e-6) / 1000), yq]); }
    const poly = rows.map((r) => [r[0], r[1]]).concat(rows.slice().reverse().map((r) => [-r[0], r[1]]));
    const widest = Math.max.apply(null, rows.map((r) => r[0]));
    const floor = { id: 'floor', cut: 'deck', z: COCKPIT_Z, poly, holes: [],
      note: 'the bottom boards, stern knee to bow thwart: ' + q3(rows[n][1] - rows[0][1]) + ' m long and ' + q3(2 * widest) + ' m of centre room at its widest. Whole: the three thwarts cross it ' +
        q3(SEAT - FLOOR - BOARD_T) + ' m over the boards and are stepped over (under stepTol), as the owner ruled on 2026-07-22. z is the boards amidships; they rise ' +
        q3(station((rows[0][1] + L / 2) / L).kz) + ' m to the after end and ' + q3(station((rows[n][1] + L / 2) / L).kz) + ' m to the forward end with her rocker.' };
    const areas = [floor];
    // every outline is checked against the rule it was cut by: each vertex and each edge (every 2 mm) fits
    for (const ar of areas) for (let i = 0; i < ar.poly.length; i++) { const p = ar.poly[i], q = ar.poly[(i + 1) % ar.poly.length], m = Math.max(1, Math.ceil(Math.hypot(q[0] - p[0], q[1] - p[1]) / 0.002));
      for (let k = 0; k <= m; k++) { const x = p[0] + (q[0] - p[0]) * k / m, y = p[1] + (q[1] - p[1]) * k / m;
        if (!walkFits(x, y)) throw new Error(RIG_FILE + ': walk: area ' + ar.id + ' leaves the floor at ' + q3(x) + ', ' + q3(y)); } }
    const seatOf = (name, u) => [0, station(u).y, station(u).kz + SEAT];
    const ROW_AFT = seatOf('thwart_aft', THWARTS[0]), ROW_FWD = seatOf('thwart_fwd', THWARTS[1]), BOW_SEAT = seatOf('thwart_bow', BOW_THWART_U);
    const STAND = [PILOT.x, PILOT.y, station((PILOT.y + L / 2) / L).kz + FLOOR];
    const STAND_MOTOR = [PILOT_MOTOR.x, PILOT_MOTOR.y, station((PILOT_MOTOR.y + L / 2) / L).kz + FLOOR];
    const thole = (lu, side) => { const st = station(lu); return [side * (st.ws - TH * 0.15), st.y, st.kz + st.dep + 0.04]; };
    const railPt = (u, side) => [side * station(u).ws, station(u).y, sheerZ(u) + CAP_RISE];
    const stow = (side) => { const a = thole(OARLOCK_U, side), d = oarDir(side, -82, -7); return { id: side < 0 ? 'port' : 'star', lock: xyz(a), blade: xyz(v_add(a, v_mul(d, O_LOUT))), handle: xyz(v_sub(a, v_mul(d, O_LIN))) }; };
    const spawn = { x: PILOT.x, y: PILOT.y, area: 'floor' };
    const on = (ar, x, y) => ({ x: q3(x), y: q3(y), z: ar.z });
    const sheer = sheerZ(0.5);
    const _excluded = [
      { id: 'gunwales', of: 'area', z: q3(sheer), reason: 'the gunwales: a ' + q3(TH + RUB + INWALE) + ' m rail cap ' + q3(sheer - COCKPIT_Z) + ' m over the boards and ' + q3(sheer - DWL) +
        ' m over the water. Stepped over to board, not walked. She has no ladder: a swimmer comes aboard over them (ladderOn).', waitsOn: ['ladderOn'] },
      { id: 'bow', of: 'area', z: COCKPIT_Z, reason: 'forward of the walk’s end the bottom draws in under the raked stem: no centre point fits. The painter is worked from the bow thwart.' },
      { id: 'rowing_thwart', of: 'seat', at: xyz(ROW_AFT), reason: 'the after thwart: the rower’s seat (anchors.helm), ' + q3(SEAT - FLOOR - BOARD_T) + ' m over the boards. A seat and a step-over, not a standing surface.' },
      { id: 'forward_thwart', of: 'seat', at: xyz(ROW_FWD), reason: 'the forward thwart: the second rowing station and a passenger’s seat. Sitting waits on sit.', waitsOn: ['sit'] },
      { id: 'bow_thwart', of: 'seat', at: xyz(BOW_SEAT), reason: 'the short bow thwart: a seat for whoever tends the painter. Sitting waits on sit.', waitsOn: ['sit'] },
      { id: 'stern_bench', of: 'seat', at: xyz([HELM.x, HELM.y, HELM.z]), reason: 'the outboard build’s stern bench (state motor), the seated operator’s place (anchors.helmMotor). ' +
        'This walk describes the rowing hull, which is the mesh both dories wear; the bench is not in it.', waitsOn: ['motorBuildMesh'] },
      { id: 'washboards', of: 'washboards', reason: 'an open rowed boat: no side deck. The gunwales are stepped over, not walked (gunwales).' },
      { id: 'tholes', of: 'cleat', reason: 'the thole pins, a pair a side at each rowing station: they hold the oars, not a mooring line. She lies to her stem ring bolt (bow_1) and her port-quarter ring (stern_port).' },
    ];
    const interact = [
      { id: 'helm', action: 'enter_helm', label: 'Oars', prompt: 'Take the oars', at: xyz(ROW_AFT), reach: on(floor, PILOT.x, PILOT.y), area: 'floor',
        seat: xyz(ROW_AFT), stand: xyz(STAND),
        note: 'she is steered from the oars, and the oars are worked from the after thwart, facing aft: the tholes are ' + q3(station(THWARTS[0]).y - station(OARLOCK_U).y) + ' m abaft its centre. Taken from the boards forward of it.' },
      { id: 'seat_forward_thwart', action: 'enter_ride', label: 'Forward thwart', prompt: 'Ride', at: xyz(ROW_FWD), reach: on(floor, 0, q3(station(THWARTS[1]).y - 0.11 - 0.25)), area: 'floor', waitsOn: ['sit'] },
    ];
    const cleats = [
      { id: 'bow_1', type: 'ring_bolt', pos: [q4(PAINTER.x), q4(PAINTER.y), q4(PAINTER.z)], note: 'the iron ring bolt through the breasthook at the stem head: the painter lives on it' },
      { id: 'stern_port', type: 'ring_bolt', pos: [q4(STERN_EYE.x), q4(STERN_EYE.y), q4(STERN_EYE.z)], note: 'the ring through the port quarter knee: the stern line when she lies alongside' },
    ];
    return {
      schema: 'hidden-harbours/hull-walk@1', rig: RIG_FILE, exportSymbol: EXPORT_SYMBOL, hull: HULL_ID, label: LABEL, kind: 'open',
      units: 'metres', frame: FRAME,
      loa_m: q3(LOA), beam_m: q3(2 * Math.max.apply(null, T.map((t) => t[0]))), bottom_m: q3(2 * Math.max.apply(null, T.map((t) => t[1]))), DWL, cockpitZ: COCKPIT_Z,
      cell: { W, H, pivot: { x: cx, y: cy }, pxPerM: PX, defaultElev: DEFAULT_ELEV, order: ORDER.slice() },
      tags: { lv: Object.assign({}, LV), ro: Object.assign({}, RO), ghostRoles: GHOST_ROLES.slice() },
      walk: { radius: R, stepTol: WALK.stepTol, rule: RULE, spawn, areas },
      house: null,
      anchors: { helm: xyz(ROW_AFT), pilot: xyz(STAND),
        seats: [Object.assign({ id: 'rowing_thwart' }, xyz(ROW_AFT)), Object.assign({ id: 'forward_thwart' }, xyz(ROW_FWD)), Object.assign({ id: 'bow_thwart' }, xyz(BOW_SEAT))],
        rowing: [{ id: 'aft', seat: xyz(ROW_AFT), tholes: [xyz(thole(OARLOCK_U, -1)), xyz(thole(OARLOCK_U, 1))], note: 'the station the oars are baked to: oarlockPt' },
          { id: 'forward', seat: xyz(ROW_FWD), tholes: [xyz(thole(FWD_LOCK_U, -1)), xyz(thole(FWD_LOCK_U, 1))], note: 'the second pair of tholes: a second rower, or the same oars shifted forward to trim her' }],
        oarStow: [stow(-1), stow(1)],
        tubs: TUBS.map((t) => Object.assign({ id: 'midship' }, xyz([t.x, t.y, t.z]))),
        painter: xyz([PAINTER.x, PAINTER.y, PAINTER.z]), sternEye: xyz([STERN_EYE.x, STERN_EYE.y, STERN_EYE.z]),
        handline: [Object.assign({ id: 'star' }, xyz(railPt(HANDLINE_U, 1))), Object.assign({ id: 'port' }, xyz(railPt(HANDLINE_U, -1)))],
        bailer: xyz([BAILER.x, BAILER.y, station((BAILER.y + L / 2) / L).kz + FLOOR + BOARD_T]),
        helmMotor: xyz([HELM.x, HELM.y, HELM.z]), pilotMotor: xyz(STAND_MOTOR), driveMounts: [xyz([MOUNT.x, MOUNT.y, MOUNT.z])] },
      interact, cleats, washboards: [], _excluded,
      rock: Object.assign({}, ROCK),
      qa: { fitCheck: fitCheck() },
    };
  }

  root.DoryIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:cy}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'], BUILDS:['oars','motor'], defaultBuild:'oars',
    RAMP, KEY, IRON, render, ROCK, rock:rockMotion,
    renderOars, oarHandles, OAR, TUBS, tubMounts, PILOT, PILOT_MOTOR, pilotStand,
    STERN_U, BOARD, MOUNT, motorMount, HELM, helmSeat, PAINTER, painter, STERN_EYE, sternEye,
    // the hull layout: the hull, through byId('dory_iso'); the global carries her members under the same names
    id: HULL_ID, label: LABEL, kind: 'open', hullIds: [HULL_ID], defaultHull: HULL_ID, LV, RO, GHOST_ROLES,
    PAINTS, matsFor, schemeIds: PAINTS.map((p) => p.id), defaultScheme: DEFAULT_SCHEME,
    faceList, partFaces, interiorFaces, doorFaces, geometry, walk, walkFits, fitCheck, renderCut,
    DWL, cockpitZ: COCKPIT_Z, loa: LOA, GAIN, BIAS, LN, BAYER,
    cell: { W, H, pivot: { x: cx, y: cy }, PX, defaultElev: DEFAULT_ELEV } };
  root.DoryIso.byId = (id) => (id === HULL_ID ? root.DoryIso : null);
})(typeof globalThis!=='undefined'?globalThis:window);
