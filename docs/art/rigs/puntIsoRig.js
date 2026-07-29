/* Hidden Harbours — parametric ISO motor punt (M2 bake recipe, ADR-0006 — same pipeline as doryIsoRig.js).
   PASS 1 hull + PASS 2 outboard motor layer. A tiller punt: flat-floored, beamier and slightly
   longer than the dory (~5.2 m LOA), wide low transom cut for an outboard. Fully painted: white
   topsides, teal sheer band + bottom, gold cove pinstripe, bare-wood interior. Fixed 3/4 turntable
   camera (elev 40deg default), 45deg steps, flat-facet shading from the upper-left key, z-buffered,
   ordered dither, 1px keyline, NO AA. 32 px = 1 m.

   HULL cell 184x168, pivot (92,94) = boat origin (amidships, keel bottom, centreline), pinned every heading.
   MOTOR is a separate overlay layer in its OWN wider cell 212x168, pivot (106,94) — align layers by
   PIVOT, not corners. The motor swivels about the transom clamp: renderMotor(dir,{variant:'basic'|'upgraded', steer,-1..1 |
   steerDeg, tilt, elev, roll, pitch, heave}); pass the hull's rock(i) values so both layers ride the
   same wave. tillerGrip(dir,opts) -> motor-cell {x,y} of the tiller grip for the operator's hand.

   PASS 3 adds:
   OARS — a third overlay layer in the HULL cell (same pivot): renderOars(dir,{state:'row'|'shipped'|'trailing',
   t:0..1, side, elev, roll, pitch, heave}) + oarHandles(dir,opts) for the rower's hands. She rows from the mid
   thwart on rowlock sockets baked into the gunwale, so the oars stack on the motor build (kicker won't start ->
   pull her home). Also baked: floor battens, breasthook + stem ring bolt (painter), quarter knees + a
   port-quarter ring bolt (stern line). Anchors: painter, sternEye, rowSeat, helmSeat, pilotStand, tubMounts.

   SPORT-MOTOR COLOURWAYS — the upgraded build's paint is data too: renderMotor(dir,{variant:'upgraded',
   motorScheme:'regatta-white'|'harbour-teal'|'signal-gold'|'oxblood'|'midnight'|'spruce'}) or
   motor:{cowl,accent,pan} for a free mix (same rampFrom envelope, same chipWall). The BASIC starter is
   deliberately one weathered colourway — it never reads those mats, so its sheet is fixed.

   COLOURWAYS — paint is data, not baked pixels. render(dir,{scheme:'harbour-white'|...}) picks a named preset;
   render(dir,{paint:{hull,trim,cove,bottom,interior}}) mixes any base colours (bottom defaults to trim,\n   interior to bare wood; oars and motor cowl keep their own ramps). rampFrom(hex,n) derives a fleet-legal
   ramp in OKLCH (locked lightness curve, chroma capped at C_CAP, shadows rotated cool / highlights warm), so a
   free colour choice can't leave the fleet's envelope. chipWall() returns the legal swatch grid for a picker.
   'harbour-white' keeps the hand-tuned pass-1 ramps -> bit-identical to the original bake.

   Exposes globalThis.PuntIso = { W,H,PX,DIRS,pivot,order,ROCK,rock(i),render(dir,opts),
   MOTOR, renderMotor, tillerGrip, motorMount, MOUNT, OAR, renderOars, oarHandles, OARLOCK_U,
   SCHEMES, schemeIds, defaultScheme, palette, rampFrom, chipWall, C_CAP,
   TUBS, tubMounts, PILOT, pilotStand, painter, sternEye, rowSeat, helmSeat,
   PAINT,TRIM,GOLD,WOOD,IRON,KEY }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 184, H = 168, cx = 92, cy = 94;
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;
  const ROCK = { frames: 8, rollA: 4.2, pitchA: 2.4, heaveA: 1.5, period: 2.6 };  // beamier boat, stiffer roll than the dory
  function rockMotion(i, frames){
    frames = frames || ROCK.frames;
    const a = 2*Math.PI*(((i%frames)+frames)%frames)/frames;
    return { roll: ROCK.rollA*Math.sin(a), pitch: ROCK.pitchA*Math.sin(a+Math.PI/2), heave: ROCK.heaveA*Math.sin(a) };
  }
  const L = 5.2, TH = 0.035, FLOOR = 0.06, SEAT = 0.30;
  const OARLOCK_U = 0.42;                // rowlock station — 0.31 m aft of the mid thwart, so a rower on it has the looms in hand
  const NSEG = 18;

  // paint ramps dark->light (KTC: keyed to the fleet Punt scheme — teal #2ba39a / white #eef0ea / gold #e0b13a)
  const PAINT = ['#5d6a70','#7e8c90','#a3b0b1','#c2cdca','#dde5df','#eef0ea','#f7f8f3'];  // white topsides
  const TRIM  = ['#0d3f3c','#14554e','#1c7367','#2ba39a','#49b8aa'];                      // teal sheer band + bottom
  const GOLD  = ['#7a5a1c','#a8842a','#e0b13a'];                                          // cove pinstripe + cowling
  const WOOD  = ['#33271b','#473627','#5e4630','#6b4f35','#8a6a48','#9a7853','#a98352'];  // bare interior (dory ramp)
  const IRON  = ['#20180f','#2a2014','#3a2c1c'];
  const MOTO  = ['#101317','#1d2127','#2b323a','#3d454e','#525c63','#6b767b','#8a9499'];  // engine grey-blacks
  const RED   = ['#4a100e','#7c1a15','#a8241b','#cf3626','#e2573c'];                      // upgrade stripe/decals
  const KEY   = '#101a19';

  // ---- paint mixer (OKLCH): any base colour -> a fleet-legal ramp -------------------------------
  // The rig owns the ramp SHAPE (lightness curve, chroma ceiling, hue rotation); the caller only
  // picks base colours. That is what keeps a free colour choice from going off-model.
  const clamp01=(v)=>v<0?0:v>1?1:v;
  const s2l=(c)=>c<=0.04045?c/12.92:Math.pow((c+0.055)/1.055,2.4);
  const l2s=(c)=>c<=0.0031308?c*12.92:1.055*Math.pow(c,1/2.4)-0.055;
  function hex2oklch(hex){
    const R=s2l(parseInt(hex.slice(1,3),16)/255), G=s2l(parseInt(hex.slice(3,5),16)/255), B=s2l(parseInt(hex.slice(5,7),16)/255);
    const l=Math.cbrt(0.4122214708*R+0.5363325363*G+0.0514459929*B);
    const m=Math.cbrt(0.2119034982*R+0.6806995451*G+0.1073969566*B);
    const s=Math.cbrt(0.0883024619*R+0.2817188376*G+0.6299787005*B);
    const Lo=0.2104542553*l+0.7936177850*m-0.0040720468*s;
    const A=1.9779984951*l-2.4285922050*m+0.4505937099*s;
    const Bb=0.0259040371*l+0.7827717662*m-0.8086757660*s;
    return { L:Lo, C:Math.hypot(A,Bb), h:(Math.atan2(Bb,A)*180/Math.PI+360)%360 };
  }
  function _lin(L,C,h){
    const a=C*Math.cos(h*DEG), b=C*Math.sin(h*DEG);
    const l_=L+0.3963377774*a+0.2158037573*b, m_=L-0.1055613458*a-0.0638541728*b, s_=L-0.0894841775*a-1.2914855480*b;
    const l=l_*l_*l_, m=m_*m_*m_, s=s_*s_*s_;
    return [ 4.0767416621*l-3.3077115913*m+0.2309699292*s,
            -1.2684380046*l+2.6097574011*m-0.3413193965*s,
            -0.0041960863*l-0.7034186147*m+1.7076147010*s ];
  }
  function oklch2hex(L,C,h){                 // gamut-fit by walking chroma down, so hue/value survive
    let c=C;
    for(let i=0;i<14;i++){ const v=_lin(L,c,h); if(v.every(x=>x>=-0.002&&x<=1.002)) break; c*=0.9; }
    const v=_lin(L,c,h).map(x=>('0'+Math.round(clamp01(l2s(x))*255).toString(16)).slice(-2));
    return '#'+v.join('');
  }
  const HUE_WARM=92, HUE_COOL=258;           // key light / sky fill — every ramp in the fleet leans these ways
  const C_CAP=0.115;                         // saturation ceiling: no neon paint in this harbour
  function rotToward(h,t,amt){ const d=((t-h+540)%360)-180; return (h+d*amt+360)%360; }
  function rampFrom(hex, n, o){
    o=o||{};
    const anchor=o.anchor!=null?o.anchor:0.78, g=o.gamma||0.88, b=hex2oklch(hex);
    const C0=Math.min(b.C, o.cap!=null?o.cap:C_CAP);
    const Ldk=Math.max(0.24, b.L*(o.floor!=null?o.floor:0.60));
    const Lhi=Math.min(0.985, b.L+(1-b.L)*(o.ceil!=null?o.ceil:0.38));
    const out=[];
    for(let i=0;i<n;i++){
      const t=i/(n-1), k=t-anchor;
      let Lv = t<anchor ? Ldk+(b.L-Ldk)*Math.pow(t/anchor,g) : b.L+(Lhi-b.L)*((t-anchor)/(1-anchor));
      const C = Math.max(0.003, C0*(k<0 ? 1+0.10*(-k) : 1-0.55*k));
      const h = rotToward(b.h, k<0?HUE_COOL:HUE_WARM, Math.min(0.14, Math.abs(k)*0.20));
      let hx = oklch2hex(Lv,C,h);
      for(let s=0; s<6 && out.length && hx===out[out.length-1]; s++){ Lv=Math.min(0.99,Lv+0.022); hx=oklch2hex(Lv,C,h); }
      out.push(hx);
    }
    return out;
  }
  // the legal swatch grid a picker offers: chroma peaks midtone, neutral column first
  const CHIP_HUES=[null,22,48,84,132,172,196,232,268,318];
  const CHIP_L=[0.30,0.44,0.57,0.70,0.83,0.94];
  function chipWall(){
    return CHIP_L.map(Lv=>CHIP_HUES.map(h=>{
      if(h==null) return oklch2hex(Lv,0.006,236);
      const env=Math.sin(Math.PI*clamp01((Lv-0.10)/0.88));
      return oklch2hex(Lv, Math.min(C_CAP, 0.018+0.098*env), h);
    }));
  }

  // ---- colourways: presets are just base colours; the mixer derives the ramps -------------------
  const SCHEMES = {
    'harbour-white': { name:'Harbour White', hull:'#eef0ea', trim:'#2ba39a', cove:'#e0b13a',
                       ramps:{ hull:PAINT, trim:TRIM, cove:GOLD },      // hand-tuned pass-1 bake, kept exact
                       note:'the fleet scheme — matches her buoy' },
    'dory-buff':     { name:'Dory Buff',    hull:'#e3d1a4', trim:'#8f3527', cove:'#3b3a33', note:'cream topsides, oxblood rail, bare interior' },
    'bottle-green':  { name:'Bottle Green', hull:'#31694c', trim:'#e2dcc7', cove:'#e0b13a', bottom:'#8c3f2c', interior:'#cdb98d', note:'dark green, cream sheer, buff-painted interior' },
    'squall-grey':   { name:'Squall Grey',  hull:'#8a99a1', trim:'#22354a', cove:'#c9ccc4', bottom:'#7c3a2a', interior:'#909c99', note:'workaday grey-blue, deck-grey interior' },
    'cranberry':     { name:'Cranberry',    hull:'#a8452f', trim:'#e6ddc6', cove:'#e0b13a', note:'bog red, cream boot top' },
    'tar-black':     { name:'Tar Black',    hull:'#2a2f33', trim:'#2ba39a', cove:'#e0b13a', note:'tarred topsides, teal rail' },
  };
  const DEFAULT_SCHEME='harbour-white';

  // ---- sport-motor colourways: the upgrade's cosmetic axis (cowl shell / flash decals / pan) ----
  // ONLY the 'upgraded' motor build reads these mats (cowl / red / blk) — the basic starter is one
  // weathered colourway by design, so its sheet never changes. Same mixer, same envelope as the hull.
  const MOTOR_SCHEMES = {
    'regatta-white': { name:'Regatta White', cowl:'#eef0ea', accent:'#cf3626',
                       ramps:{ cowl:PAINT, accent:RED, pan:MOTO },   // hand-tuned pass-2 bake, kept exact
                       note:'stock sport paint \u2014 white shell, red wrap' },
    'harbour-teal':  { name:'Harbour Teal',  cowl:'#2ba39a', accent:'#eef0ea', note:'fleet teal, white flash' },
    'signal-gold':   { name:'Signal Gold',   cowl:'#e0b13a', accent:'#2a2f33', note:'cove gold, charcoal flash' },
    'oxblood':       { name:'Oxblood',       cowl:'#8f3527', accent:'#e3d1a4', note:'deep red, cream flash' },
    'midnight':      { name:'Midnight',      cowl:'#22354a', accent:'#e0b13a', note:'navy shell, gold flash' },
    'spruce':        { name:'Spruce',        cowl:'#31694c', accent:'#e6ddc6', note:'bottle green, bone flash' },
  };
  const DEFAULT_MOTOR_SCHEME='regatta-white', PAN_BASE='#2b323a';

  const _pal={}; let _palOrder=[];
  function palette(o){
    o=o||{};
    const base = o.paint || SCHEMES[o.scheme] || SCHEMES[DEFAULT_SCHEME];
    const hull=base.hull||SCHEMES[DEFAULT_SCHEME].hull, trim=base.trim||SCHEMES[DEFAULT_SCHEME].trim,
          cove=base.cove||SCHEMES[DEFAULT_SCHEME].cove, bottom=base.bottom||trim, interior=base.interior||null;
    const MD = MOTOR_SCHEMES[DEFAULT_MOTOR_SCHEME];
    const mbase = o.motor || MOTOR_SCHEMES[o.motorScheme] || MD;
    const cowl=mbase.cowl||MD.cowl, accent=mbase.accent||MD.accent, pan=mbase.pan||PAN_BASE;
    const key=[hull,trim,cove,bottom,interior||'bare',base.ramps?'baked':'mix',
               cowl,accent,pan,mbase.ramps?'baked':'mix'].join('|');
    if(_pal[key]) return _pal[key];
    const R=base.ramps||{}, MR=mbase.ramps||{};
    const cowlR=MR.cowl||rampFrom(cowl,7), accR=MR.accent||rampFrom(accent,5,{anchor:0.75}),
          panR=MR.pan||rampFrom(pan,7,{anchor:0.72});
    const hullR=R.hull||rampFrom(hull,7), trimR=R.trim||rampFrom(trim,5,{anchor:0.75}),
          coveR=R.cove||rampFrom(cove,3,{anchor:0.85});
    const botR = bottom===trim ? trimR : rampFrom(bottom,5,{anchor:0.75});
    const intR = interior ? rampFrom(interior,7,{anchor:0.72}) : WOOD;
    const mats = { paint:{ramp:hullR,off:0}, trim:{ramp:trimR,off:-1}, gold:{ramp:coveR,off:-3},
                   bottom:{ramp:botR,off:-1}, wood:{ramp:intR,off:0}, oar:{ramp:WOOD,off:0},
                   iron:{ramp:IRON,off:-2}, moto:{ramp:MOTO,off:0}, blk:{ramp:panR,off:-2},
                   red:{ramp:accR,off:-1}, cowl:{ramp:cowlR,off:0} };
    const rindex={};
    [hullR,trimR,coveR,botR,intR,cowlR,accR,panR,WOOD,IRON,MOTO,RED,PAINT].forEach(r=>r.forEach((c,i)=>{ if(!rindex[c]) rindex[c]={r,i}; }));
    const out={ key, id:o.paint?'custom':(SCHEMES[o.scheme]?o.scheme:DEFAULT_SCHEME), name:base.name||'Custom',
                base:{hull,trim,cove,bottom,interior}, note:base.note||'',
                ramps:{hull:hullR,trim:trimR,cove:coveR,bottom:botR,interior:intR}, mats, rindex,
                motor:{ id:o.motor?'custom':(MOTOR_SCHEMES[o.motorScheme]?o.motorScheme:DEFAULT_MOTOR_SCHEME),
                        name:mbase.name||'Custom', note:mbase.note||'', base:{cowl,accent,pan},
                        ramps:{cowl:cowlR,accent:accR,pan:panR} } };
    _pal[key]=out; _palOrder.push(key);
    if(_palOrder.length>24){ delete _pal[_palOrder.shift()]; }
    return out;
  }
  const GAIN = 3.0, BIAS = 2.7;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  // ---- offsets table: stern(0) -> bow(8). [sheerHalf, bottomHalf, depth, keelZ] (m) ----
  // fuller than the dory: flat run aft, wide low transom to carry the outboard.
  const T = [
    [0.62,0.46,0.48,0.08],   // transom
    [0.72,0.52,0.46,0.02],
    [0.77,0.55,0.45,0.00],
    [0.79,0.56,0.44,0.00],
    [0.78,0.55,0.44,0.00],
    [0.74,0.51,0.45,0.01],
    [0.64,0.42,0.47,0.05],
    [0.45,0.26,0.51,0.12],
    [0.06,0.03,0.56,0.24],   // stem
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
  function floorPt(side,u){ const st=station(u); return [ side*(st.wb-TH*0.6)*0.94, st.y, st.kz+FLOOR ]; }

  // ---- hull face list ----
  const F = [];
  const ID = (p)=>p;                       // identity transform for the box/tube helpers below
  const face=(v,mat,b,db)=>F.push({v,mat:mat||'paint',b:b||0,db:db||0});
  // outer paint scheme: white body, gold cove line, teal sheer band  [f0, f1, mat, b, db]
  const OB = [ [0,1/3,'paint',0,0], [1/3,2/3,'paint',0,0], [2/3,0.79,'paint',0,0],
               [0.79,0.86,'gold',0.3,0.01], [0.86,1,'trim',0,0] ];
  (function build(){
    for(const side of [-1,1]){
      for(let i=0;i<NSEG;i++){
        const u0=i/NSEG, u1=(i+1)/NSEG;
        // outer skin bands
        for(const [f0,f1,mat,b,db] of OB)
          face([skin(side,u0,f0),skin(side,u1,f0),skin(side,u1,f1),skin(side,u0,f1)],mat,b,db);
        // plank seams within the white body (thin, biased forward)
        for(const f1 of [1/3,2/3]){ const fs=f1-0.06;
          face([skin(side,u0,fs),skin(side,u1,fs),skin(side,u1,f1),skin(side,u0,f1)],'paint',-2.2,0.02); }
        // inner skin (bare wood interior, 3 strakes)
        for(let k=0;k<3;k++){ const f0=k/3, f1=(k+1)/3;
          face([skin(side,u1,f0,1),skin(side,u0,f0,1),skin(side,u0,f1,1),skin(side,u1,f1,1)],'wood',-1.1); }
        // bottom (anti-foul, own colour slot) + interior floor (wood)
        face([floorPt(-1,u0),floorPt(1,u0),floorPt(1,u1),floorPt(-1,u1)],'wood',-0.4);
        face([skin(-1,u0,0),skin(-1,u1,0),skin(1,u1,0),skin(1,u0,0)],'bottom',-1.0);
        // gunwale cap (painted teal rail)
        const oa=skin(side,u0,1),ob=skin(side,u1,1),ia=skin(side,u0,1,1),ib=skin(side,u1,1,1);
        const inb=(p)=>[p[0]-side*TH*1.3,p[1],p[2]];
        face([oa,ob,inb(ib),inb(ia)],'trim',0.4,0.03);
      }
    }
    // transom: wide vertical stern board, paint bands carried across
    const tp=(s,f)=>skin(s,0,f);
    for(const [f0,f1,mat,b] of OB)
      face([tp(-1,f1),tp(1,f1),tp(1,f0),tp(-1,f0)], mat, (b||0)-0.8, 0.005);
    // motor pad: bare-wood clamp plate proud of the transom, top centre
    (function(){ const st=station(0), zt=st.kz+st.dep, zb=st.kz+st.dep*0.45, y=st.y-0.03, hx=0.15;
      face([[-hx,y,zt],[hx,y,zt],[hx,y,zb],[-hx,y,zb]],'wood',-0.55,-0.03);
      face([[-hx,y,zt],[hx,y,zt],[hx,st.y+0.06,zt],[-hx,st.y+0.06,zt]],'wood',0.35,-0.03);
    })();
    // thwarts: stern bench (operator, tiller reach) + mid + forward
    for(const [u,bd] of [[0.15,0.34],[0.48,0.20],[0.74,0.20]]){
      const st=station(u), hx=st.ws*0.90-TH, zTop=st.kz+SEAT, zBot=zTop-0.05;
      const y0=st.y-bd/2, y1=st.y+bd/2;
      face([[-hx,y0,zTop],[hx,y0,zTop],[hx,y1,zTop],[-hx,y1,zTop]],'wood',0.6);
      face([[-hx,y1,zTop],[hx,y1,zTop],[hx,y1,zBot],[-hx,y1,zBot]],'wood',-1.2);
      face([[hx,y0,zTop],[-hx,y0,zTop],[-hx,y0,zBot],[hx,y0,zBot]],'wood',-0.4);
    }

    // ---- pass-3 fit-out ----
    // floor battens: two runs flanking the centreline — footing over the bilge, and they read her length
    for(const sx of [-1,1]){
      const NB=8, uA=0.13, uB=0.85;
      for(let i=0;i<NB;i++){
        const ua=uA+(uB-uA)*i/NB, ub=uA+(uB-uA)*(i+1)/NB;
        const sa=station(ua), sb=station(ub);
        const x0=sx*0.09, x1=sx*0.23, za=sa.kz+FLOOR+0.028, zb=sb.kz+FLOOR+0.028;
        const q=[[x0,sa.y,za],[x1,sa.y,za],[x1,sb.y,zb],[x0,sb.y,zb]];
        face(sx>0?q:[q[1],q[0],q[3],q[2]],'wood',0.3,0.03);
      }
    }
    // breasthook closing the stem, and the iron ring bolt the painter lives on
    face([skin(-1,0.94,1),skin(1,0.94,1),skin(1,1,1),skin(-1,1,1)],'wood',0.9,0.04);
    function ringBolt(x,y,z,ro,ri,tk,b){   // 4-bar ring: nothing under the middle, so the hole reads as wood
      const bar=(ro-ri)/2, off=(ro+ri)/2;
      F.push(...box([x, y+off, z],[ro, bar, tk],'iron',b,-0.05,ID));
      F.push(...box([x, y-off, z],[ro, bar, tk],'iron',b-0.3,-0.05,ID));
      F.push(...box([x+off, y, z],[bar, ri, tk],'iron',b-0.12,-0.05,ID));
      F.push(...box([x-off, y, z],[bar, ri, tk],'iron',b-0.22,-0.05,ID));
    }
    (function(){ const st=station(0.962); ringBolt(0, st.y, st.kz+st.dep+0.05, 0.05, 0.018, 0.014, 0.6); })();
    // quarter knees tying the transom corners into the sides + the port-quarter ring bolt (stern line alongside)
    for(const side of [-1,1]){
      const c=skin(side,0,1), f=skin(side,0.075,1), inb=[side*0.30,-L/2,c[2]];
      face(side>0?[c,f,inb]:[c,inb,f],'wood',0.8,0.04);
    }
    ringBolt(-0.42, -L/2+0.20, T[0][3]+T[0][2]+0.028, 0.045, 0.016, 0.013, 0.5);
    // rowlock sockets + thole pins on the gunwale — she pulls home from the mid thwart when the kicker won't start
    for(const side of [-1,1]){
      const st=station(OARLOCK_U), x=side*(st.ws-TH*0.6), zt=st.kz+st.dep;
      F.push(...box([x, st.y, zt+0.014],[0.030,0.055,0.014],'iron',0.35,-0.03,ID));
      F.push(...box([x, st.y, zt+0.075],[0.019,0.019,0.050],'iron',0.55,-0.05,ID));
    }
  })();

  // ---- rasterizer (shared recipe; G overrides cell geometry for the motor layer) ----
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
    const PAL=palette(opts), MATS=PAL.mats, RINDEX=PAL.rindex;
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
  // outboard clamp point (transom top centre, just aft) — hull-cell coords
  const MOUNT = { x:0, y:-L/2, z:T[0][3]+T[0][2] };
  function motorMount(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), p=projVert(MOUNT.x, MOUNT.y-0.03, MOUNT.z, B);
    return { x:p.sx, y:p.sy };
  }

  // ---- fish-tub deck mounts (boat-local, on the floor, clear of the thwarts; punt carries 2) ----
  const TUBS = [ {x:0,y:-1.00}, {x:0,y:0.60} ].map(m=>{ const st=station((m.y+L/2)/L); return {x:m.x,y:m.y,z:st.kz+FLOOR}; });
  function tubMounts(dir, opts){   // hull-cell px anchors; pass rock(i) so anchors ride the wave (incl. heave)
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts);
    return TUBS.map(m=>{ const p=projVert(m.x,m.y,m.z,B); return {x:p.sx, y:p.sy}; });
  }

  // ---- outboard motor (separate pivoting layer; own wider cell, pivot-aligned to the hull) ----
  const MG = { W:212, H:168, cx:106, cy:94 };   // motor cell geometry
  const MOTOR = { steerFrames:9, maxSteer:32, tiltMax:40, behind:[3,4,5], parts:['upper','lower'], variants:['basic','upgraded'],
    colourVariant:'upgraded', schemes:MOTOR_SCHEMES, schemeIds:Object.keys(MOTOR_SCHEMES), defaultScheme:DEFAULT_MOTOR_SCHEME,
    W:MG.W, H:MG.H, pivot:{x:MG.cx, y:MG.cy},
    angle:(f)=>-32 + (64*f)/8 };                // sheet col f (0..8) -> steer degrees
  const YA = -L/2 - 0.06, ZT = MOUNT.z;         // swivel axis (just aft of transom) / clamp height
  const v_sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]], v_add=(a,b)=>[a[0]+b[0],a[1]+b[1],a[2]+b[2]];
  const v_mul=(a,s)=>[a[0]*s,a[1]*s,a[2]*s], v_norm=(a)=>{const m=Math.hypot(a[0],a[1],a[2])||1;return [a[0]/m,a[1]/m,a[2]/m];};
  const v_cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
  // motor-local frame: origin on the swivel axis at keel height; +y forward (inboard), z absolute.
  // steer: rotation about the vertical axis (+steer = tiller to port, prop to starboard -> boat turns starboard).
  // tilt: rotation about the lateral axis at clamp height (leg swings aft-up, tiller drops inboard).
  function mxform(opts){
    const sd = opts.steerDeg!=null ? opts.steerDeg : Math.max(-1,Math.min(1,opts.steer||0))*MOTOR.maxSteer;
    const sa=sd*DEG, ta=Math.max(0,Math.min(MOTOR.tiltMax,opts.tilt||0))*DEG;
    const cs=Math.cos(sa), ss=Math.sin(sa), ct2=Math.cos(ta), st2=Math.sin(ta);
    return (p)=>{
      let [x,y,z]=p;
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
    return out;
  }
  const GRIP_L = [0, 0.685, 0.627];   // tiller grip centre, motor-local
  // parts: 'upper' = bracket/cowl/tiller (above the sheer — ALWAYS composites over the hull);
  //        'lower' = leg/plate/skeg/prop (goes UNDER the hull for stern-away headings, MOTOR.behind)
  // ---- cowling: lofted 8-sided shell (chamfered cross-section, domed crown, aft-leaned) ----
  // profile slices [z, halfWidth, halfLength, yCentre]; two builds off one recipe:
  // 'basic'    — weathered grey/black starter (scuffs + pan rust);
  // 'upgraded' — ~15% larger, gloss-black pan, white top, red wrap stripe + side flash decals.
  function ringOf(s, SZ, ycS){
    const z=s[0], hx=s[1]*SZ, hy=s[2]*SZ, yc=s[3]*ycS, kx=0.55, ky=0.60;
    return [[hx,-hy*ky],[hx,hy*ky],[hx*kx,hy],[-hx*kx,hy],[-hx,hy*ky],[-hx,-hy*ky],[-hx*kx,-hy],[hx*kx,-hy]]
      .map(([x,y])=>[x, y+yc, z]);
  }
  function cowlFaces(X, up_){
    const SZ=up_?1.16:1, ycS=up_?1.06:1, zb=0.665, ZS=up_?1.22:1, Z=(z)=>zb+(z-zb)*ZS;
    const prof = [ [zb,.150,.175,-.130],[Z(.705),.158,.183,-.130],[Z(.735),.150,.172,-.135] ]
      .concat(up_?[[Z(.795),.147,.167,-.138]]:[])          // extra slice bounds the red stripe band
      .concat([[Z(.860),.143,.160,-.145],[Z(.925),.112,.126,-.155],[Z(.958),.062,.072,-.163]]);
    const bands = up_
      ? [['blk',-.3],['blk',-.12],['red',.18],['cowl',.12],['cowl',.28],['cowl',.42]]
      : [['moto',-.5],['moto',-.3],['moto',.02],['moto',.14],['moto',.26]];
    const cap = up_?['cowl',.5]:['moto',.34];
    const rings = prof.map(s=>ringOf(s,SZ,ycS).map(X));
    const fs=[];
    for(let i=0;i<rings.length-1;i++){
      const lo=rings[i], hi=rings[i+1], [mat,b]=bands[i];
      for(let k=0;k<8;k++){ const k2=(k+1)%8; fs.push({v:[lo[k],lo[k2],hi[k2],hi[k]],mat,b,db:0}); }
    }
    fs.push({v:rings[rings.length-1].slice(),mat:cap[0],b:cap[1],db:0});                 // crown cap
    fs.push({v:rings[0].slice().reverse(),mat:bands[0][0],b:bands[0][1]-0.3,db:0});      // pan underside
    const topZ = prof[prof.length-1][0];
    return fs.concat(box([0,-0.14,topZ+0.013],[0.05,0.04,0.013], up_?'blk':'moto', up_?0.2:-0.15, -0.02, X)); // lift handle
  }
  function cowlDecals(X, up_){
    const fs=[], q=(x0,y0,z0,y1,z1,mat,b)=>{   // proud side quad at constant x, outward-facing
      const v = x0<0 ? [[x0,y1,z0],[x0,y0,z0],[x0,y0,z1],[x0,y1,z1]] : [[x0,y0,z0],[x0,y1,z0],[x0,y1,z1],[x0,y0,z1]];
      fs.push({v:v.map(X),mat,b,db:-0.02});
    };
    if(up_){
      q( 0.172,-0.240,0.835,-0.070,0.895,'red',.3);    // side flash decals in the white band
      q(-0.172,-0.240,0.835,-0.070,0.895,'red',.3);
    } else {
      q( 0.154,-0.205,0.762,-0.115,0.792,'moto',1.5);  // worn-paint scuffs
      q(-0.154,-0.095,0.748,-0.030,0.775,'moto',1.3);
      q( 0.1605,-0.200,0.672,-0.150,0.700,'iron',0.4); // pan rust
      q(-0.1605,-0.115,0.668,-0.060,0.695,'iron',0.3);
    }
    return fs;
  }
  function motorFaces(opts){
    const X=mxform(opts), I=(p)=>[p[0], YA+p[1], p[2]];
    const up_ = opts.variant==='upgraded';
    const part=opts.part||'all', up=part!=='lower', lo=part!=='upper';
    const legS = up_?1.12:1, legM = up_?'blk':'moto';
    let fs=[];
    if(up){
      fs=fs.concat(box([0,0.05,0.545],[0.085,0.095,0.06],'moto',-0.45,0,I));   // clamp bracket (fixed to transom)
      fs=fs.concat(cowlFaces(X,up_)).concat(cowlDecals(X,up_));
      fs=fs.concat(tube([0,0.02,0.76],[0,0.60,0.645],0.030, up_?'moto':'iron', up_?0.3:0.15, X));  // tiller arm
      fs=fs.concat(tube([0,0.58,0.648],[0,0.79,0.606],0.044, up_?'blk':'wood', up_?0.05:0.35, X)); // tiller grip
    }
    if(lo){
      fs=fs.concat(box([0,-0.14,0.36],[0.05*legS,0.06*legS,0.33],legM,-0.3,0,X));        // mid leg
      fs=fs.concat(box([0,-0.14,0.085],[0.095*legS,0.11*legS,0.014],legM,-0.15,0,X));    // cavitation plate
      fs=fs.concat(box([0,-0.17*legS,0.02],[0.018,0.08*legS,0.05],legM,-0.4,0,X));       // skeg
      fs=fs.concat(box([0,-0.235*legS,0.085],[0.013,0.045*legS,0.048],'moto', up_?0.6:-0.55,0,X)); // prop
      if(!up_) fs=fs.concat(box([0,-0.203,0.30],[0.014,0.004,0.035],'iron',0.35,-0.02,X)); // rust streak on leg
    }
    return fs;
  }
  function renderMotor(dir, opts){
    opts = (typeof opts==='number') ? {elev:opts} : (opts||{});
    return _toRGBA(_paint(motorFaces(opts), Object.assign({}, opts, {dir}), false, MG), MG.W, MG.H);
  }
  function tillerGrip(dir, opts){   // grip centre in MOTOR-cell coords, for the operator's aft hand
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const g=mxform(opts)(GRIP_L), p=projVert(g[0],g[1],g[2],camBasis(opts),MG);
    return { x:p.sx, y:p.sy };
  }

  // ---- oars (third overlay layer — HULL cell + pivot, so it stacks straight onto either build) ----
  const OAR = { rowFrames:8, states:['row','shipped','trailing'], W, H, pivot:{x:cx,y:cy} };
  const O_LOUT=1.62, O_LIN=0.80, O_TS=0.032, O_BLEN=0.54, O_BW=0.115, O_BT=0.011;
  function oarlockPt(side){ const st=station(OARLOCK_U); return [ side*(st.ws-TH*0.2), st.y, st.kz+st.dep+0.078 ]; }
  function oarDir(side, sweepDeg, dipDeg){ const f=sweepDeg*DEG, p=dipDeg*DEG;
    return [ side*Math.cos(f)*Math.cos(p), -Math.sin(f)*Math.cos(p), -Math.sin(p) ]; }
  function oarPose(state, t){
    // shipped: not in the locks — slid aft along their own axis and canted inboard, lying across the thwarts
    if(state==='shipped'||state==='resting') return { sweep:-86, dip:-3, slide:-0.62, dx:-0.30, dz:-0.10 };
    if(state==='trailing') return { sweep: 74, dip: 13 };                    // dragging aft in the water
    const tt=(((t||0)%1)+1)%1, a=2*Math.PI*tt;                               // pulling: blade traces an ellipse
    return { sweep: 32*Math.sin(a), dip: 9 + 13*Math.cos(a) };   // catch 22deg down .. recovery 4deg clear of the water
  }
  function slab(quad, n, t, mat, b){       // flat board from 4 corners + thickness along n
    const o=v_mul(n,t), A=quad.map(p=>v_add(p,o)), Bq=quad.map(p=>v_sub(p,o)), fs=[];
    fs.push({v:A,mat,b:b||0,db:0});
    fs.push({v:[Bq[3],Bq[2],Bq[1],Bq[0]],mat,b:(b||0)-1.0,db:0});
    for(let k=0;k<4;k++){ const k2=(k+1)%4; fs.push({v:[A[k],A[k2],Bq[k2],Bq[k]],mat,b:(b||0)-0.5,db:0}); }
    return fs;
  }
  function oarSeat(side, pose){            // where the loom sits: the lock when pulling, shifted when shipped
    const O=oarlockPt(side), d=oarDir(side,pose.sweep,pose.dip);
    const P=[ O[0]+(pose.dx||0)*side, O[1], O[2]+(pose.dz||0) ];
    return { P: v_add(P, v_mul(d, pose.slide||0)), d };
  }
  function buildOar(side, pose){
    const seat=oarSeat(side,pose), P=seat.P, d=seat.d;
    const hand=v_sub(P, v_mul(d,O_LIN));                    // inboard end (the grip)
    const thr=v_add(P, v_mul(d,O_LOUT-O_BLEN));             // blade throat
    const tip=v_add(P, v_mul(d,O_LOUT));
    let lat=v_cross(d,[0,0,1]); if(Math.hypot(lat[0],lat[1],lat[2])<1e-4) lat=[1,0,0];
    lat=v_norm(lat); const nrm=v_norm(v_cross(lat,d));
    const fs=[];
    fs.push(...tube(hand, thr, O_TS, 'oar', 0.15, ID));                                     // loom
    fs.push(...tube(hand, v_add(hand, v_mul(d,0.17)), O_TS*1.22, 'oar', -0.35, ID));        // grip
    fs.push(...tube(v_sub(P,v_mul(d,0.055)), v_add(P,v_mul(d,0.055)), O_TS*1.4, 'iron', 0.05, ID)); // leather collar at the lock
    const w0=O_BW*0.42, w1=O_BW;
    fs.push(...slab([ v_add(thr,v_mul(lat,w0)), v_add(tip,v_mul(lat,w1)),
                      v_sub(tip,v_mul(lat,w1)), v_sub(thr,v_mul(lat,w0)) ], nrm, O_BT, 'oar', 0.45));
    return fs;
  }
  function sidePose(opts, side){ const o=(side<0?opts.port:opts.star)||{};
    return oarPose(o.state||opts.state||'row', (o.t!==undefined?o.t:opts.t)||0); }
  function oarFaces(opts){ const ss = opts.side==='port'?[-1]:opts.side==='star'?[1]:[-1,1];
    return ss.reduce((f,s)=>f.concat(buildOar(s,sidePose(opts,s))),[]); }
  function renderOars(dir, opts){
    opts = (typeof opts==='number') ? {elev:opts} : (opts||{});
    return _toRGBA(_paint(oarFaces(opts), Object.assign({}, opts, {dir}), false));
  }
  function oarHandles(dir, opts){          // per-side grip position (hull-cell px) for a rower's hands
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), res=[];
    for(const side of [-1,1]){
      const pose=sidePose(opts,side), seat=oarSeat(side,pose);
      const Hp=v_sub(seat.P, v_mul(seat.d,O_LIN)), p=projVert(Hp[0],Hp[1],Hp[2],B);
      res.push({ side:side<0?'port':'star', x:p.sx, y:p.sy });
    }
    return res;
  }

  // ---- anchors (hull-cell px; pass rock(i) so they ride the wave) ----
  function _anchor(P){ return function(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const p=projVert(P.x, P.y, P.z, camBasis(opts)); return { x:p.sx, y:p.sy };
  }; }
  const ROW_SEAT = (()=>{ const st=station(0.48); return { x:0, y:st.y, z:st.kz+SEAT }; })();   // mid thwart — the rowing station
  const HELM     = (()=>{ const st=station(0.15); return { x:0, y:st.y, z:st.kz+SEAT }; })();   // stern bench — tiller in reach
  const PAINTER  = (()=>{ const st=station(0.962); return { x:0, y:st.y, z:st.kz+st.dep+0.05 }; })();
  const STERN_EYE= { x:-0.42, y:-L/2+0.20, z:T[0][3]+T[0][2]+0.028 };
  const rowSeat=_anchor(ROW_SEAT), helmSeat=_anchor(HELM), painter=_anchor(PAINTER), sternEye=_anchor(STERN_EYE);

  // pilot foot-contact on the floor just forward of the stern bench (works the tiller) — rides the wave
  const PILOT = { x:0, y:-1.25 };
  function pilotStand(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const st=station((PILOT.y+L/2)/L), B=camBasis(opts), p=projVert(PILOT.x, PILOT.y, st.kz+FLOOR, B);
    return { x:p.sx, y:p.sy };
  }

  root.PuntIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:cy}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'], PAINT, TRIM, GOLD, WOOD, IRON, MOTO, RED, KEY,
    render, ROCK, rock:rockMotion, MOTOR, renderMotor, tillerGrip, motorMount, MOUNT, TUBS, tubMounts, PILOT, pilotStand,
    MOTOR_SCHEMES, motorSchemeIds:Object.keys(MOTOR_SCHEMES), defaultMotorScheme:DEFAULT_MOTOR_SCHEME,
    OAR, OARLOCK_U, renderOars, oarHandles, oarPose,
    ROW_SEAT, rowSeat, HELM, helmSeat, PAINTER, painter, STERN_EYE, sternEye,
    SCHEMES, schemeIds:Object.keys(SCHEMES), defaultScheme:DEFAULT_SCHEME, palette, rampFrom, chipWall, C_CAP };
})(typeof globalThis!=='undefined'?globalThis:window);
