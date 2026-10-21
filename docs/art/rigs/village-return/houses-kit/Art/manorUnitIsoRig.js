/* manorUnitIsoRig.js — THE MANOR INTERIOR, registered under ManorIso's exterior bake.

   Precedent: rowhouseUnitIsoRig (a dwelling's routes are all indoors, so this rig owns rooms,
   thresholds, stairs, blockers and anchors) and interiorPropRig (which owns every piece of
   furniture's geometry — this rig only decides where a piece goes and which way it faces).

   IT MEASURES NOTHING. Everything dimensional comes from ManorIso.shell(opts) / .resolved(opts) in
   the same metres the exterior bake used: wall thickness, each level's floor and ceiling height and
   interior box, the accent's own drum or box, and every baked opening. So an interior window is the
   exterior's sash, a fireplace stands under the exterior's stack, and the bay you see from the
   street is the alcove you sit in.

   THE PLAN LOGIC — one device, applied on every level, which is what makes a manor legible:

     A CENTRAL SPINE runs front to rear, centred on x=0 because the exterior's entrance is centred
     on x=0. Front band = entrance hall, middle band = stair hall, rear band = service passage.
     The spine is never interrupted: bands meet at cased openings (and one baize door), so the
     spine IS the corridor.

     THE MAIN FLIGHT sits in the spine's middle band against its left edge and rises to the rear.
     THE SERVICE FLIGHT sits in the rear band against its right edge and rises to the rear. Both are
     inside the spine on every level, so:
       · the hole each flight needs in the plate above it falls in the LANDING, never in a room;
       · a walking strip always survives beside the hole (spine width - flight width >= 1.1 m), so
         the corridor is not severed by its own stairwell;
       · the flight arrives at the top end of its band, one pace from the band threshold, so the
         landing is continuous with the corridor above and below.
     stairVoid() is published BY the stair, so the plan, the plate cut-out, the well rail and the
     keep-clear zone can never disagree.

     EVERY OTHER ROOM is a zone left or right of the spine (front / middle / rear band). A zone split
     along y leaves both halves touching the spine wall, so both get their own door onto it; only an
     x-split (a dressing room, a pantry) puts a room off its parent, and that room gets a door from
     its parent. That rule is what makes access provable rather than hoped for: audit() walks the
     door graph from the street and publishes any room it cannot reach — the answer is none.

     FURNITURE never lands on a wall band, a doorway zone, a flight, a stairwell void or a hearth:
     those rectangles are in ctx.clear before the first prop is placed, and put() slides a piece
     along its wall, then off it, before giving up and reporting itself unplaced. Orientation is not
     guessed — a piece placed against a wall faces into the room, a headboard faces the wall, and a
     settee takes the wall opposite the fireplace so it faces the fire.

   Exposes globalThis.ManorUnitIso = { W,H,PX,pivot,defaultElev,order, FINISHES,TIERS,ROOMKINDS,
     VERBS,PRESETS, render(dir,opts), dims(opts), plan(opts), rooms(opts), interior(opts),
     audit(opts), anchors(dir,opts), project(dir,p,elev) }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 960, H = 1180, cx = 480, groundY = 840;
  const DEG = Math.PI / 180, DEFAULT_ELEV = 40, KEYLINE_DEFAULT = false;
  const EXPLODE = 1.7, CUT_NEAR = 0.62, CUT_PART = 1.28;

  // ---- finish ramps, dark -> light (KTC master ramps; manor grades added by mixing, not inventing)
  const FINISHES = {
    floorOak:    ['#3e2f22','#54402e','#6b543c','#84694c','#9c805f','#b39875'],
    floorPine:   ['#4a3a24','#5f4a2d','#7a6039','#957648','#ac8b5b','#c2a274'],
    parquet:     ['#35271b','#4a3627','#5f4734','#775a42','#8e6f53','#a58768'],
    flagStone:   ['#4a4438','#5e5747','#746c59','#8a816c','#a09781','#b6ad97'],
    marbleTile:  ['#5c5e5a','#74766f','#8f918a','#a9aaa3','#c3c4bb','#d8d9d0'],
    tileGrey:    ['#3a3f42','#4b5155','#5e6569','#71797d','#868e92','#9aa3a7'],
    plaster:     ['#7d786c','#948e80','#aaa494','#c0b9a8','#d4cdbb','#e6dfcd'],
    plasterLux:  ['#7e7e7a','#949490','#aaaaa6','#c0c0bc','#d5d5d1','#e8e8e4'],
    limewash:    ['#6f6d62','#868375','#9d9a8a','#b4b0a0','#c9c5b5','#dcd8c9'],
    panelOak:    ['#2f2118','#412c20','#54402e','#68533e','#7d6851','#947f66'],
    joist:       ['#2b2118','#3a2d20','#4a3a29','#5b4834','#6d5740','#7f664c'],
    wallTile:    ['#5b6567','#6f7a7c','#848f91','#98a3a5','#acb7b9','#c0cbcd'],
    joinWhite:   ['#8c928c','#a6aaa2','#bfc2b9','#d5d8cf','#e7e9e0','#f3f4ec'],
    joinCream:   ['#8a7f5e','#a3976f','#bcaf83','#d0c49a','#e1d7b5','#efe8ce'],
    joinOak:     ['#553d23','#6b4e30','#7f603b','#957749','#ab8d5f','#c1a578'],
    metal:       ['#4a4f52','#5e6467','#757c7f','#8d9497','#a5acaf','#bdc4c7'],
    brass:       ['#4a3a16','#63501f','#7d6829','#98813a','#b19a52','#c9b373'],
    stoneTop:    ['#4b4b46','#5f5f59','#75756e','#8b8b83','#a1a199','#b7b7af'],
    carpetRed:   ['#3d2020','#52292a','#683434','#7e4140','#94504f','#a96361'],
    carpetOlive: ['#3a3f36','#4b5145','#5e6455','#727866','#868c79','#9aa08c'],
    damask:      ['#3a3323','#4d442f','#61563c','#766a4b','#8b7e5c','#a1936f'],
  };
  const GLASS_DAY   = ['#7d949b','#94aab0','#abbfc4','#c2d4d8'];
  const GLASS_NIGHT = ['#232831','#2a2f3a','#343a46','#414855'];
  const CAVITY = ['#0d1013','#141a1e','#1b2328','#232c32'];
  const KEY = '#1a1c22';

  // A tier is a whole finish + kit vocabulary, matched to the exterior tier of the same name.
  const TIERS = {
    secondEmpire: {
      label:'Mansard manor', floor:'parquet', hallFloor:'marbleTile', wet:'marbleTile',
      wall:'plasterLux', panel:'panelOak', join:'joinWhite', runner:'carpetRed', stair:'floorOak',
      wainscot:1.02, picture:true, weather:0.06,
      kit:{ era:'period', wood:'walnut', paint:'cream', worktop:'marble', fabric:'red', fabric2:'gold',
            bed:'fourPoster', seat:'settee', cook:'range', sink:'wetSink', cold:'icebox',
            mantel:0, wc:0, tub:0, desk:1 },
    },
    baronial: {
      label:'Stone manor', floor:'floorOak', hallFloor:'flagStone', wet:'tileGrey',
      wall:'limewash', panel:'panelOak', join:'joinOak', runner:'carpetOlive', stair:'floorOak',
      wainscot:1.18, picture:false, weather:0.18,
      kit:{ era:'period', wood:'oak', paint:'sage', worktop:'slate', fabric:'blue', fabric2:'cream',
            bed:'fourPoster', seat:'settee', cook:'stove', sink:'sink', cold:'icebox',
            mantel:1, wc:0, tub:0, desk:0 },
    },
  };
  const ROOMKINDS = ['vestibule','stairhall','passage','landing','drawing','dining','library','study',
    'billiard','morning','kitchen','scullery','pantry','bed','dressing','bath','wc','nursery',
    'servant','linen','boxroom','store','turret','sideHall','towerRoom','schoolroom'];
  const VERBS = ['sleep','dine','sit','cook','wash_dishes','toilet','bathe','vanity','desk','read',
    'music','fire','store','laundry','entry','stair','balcony'];
  const PRESETS = {
    mansardSeat:   { tier:'secondEmpire', size:0.5,  storeys:2, accent:'tower',  bay:'canted', level:0, focus:'stack' },
    mansardGrand:  { tier:'secondEmpire', size:0.85, storeys:3, accent:'tower',  bay:'canted', porte:true, level:0, focus:'stack' },
    stoneSeat:     { tier:'baronial',     size:0.5,  storeys:2, accent:'turret', bay:'oriel',  level:0, focus:'stack' },
    stoneGrand:    { tier:'baronial',     size:0.9,  storeys:3, accent:'turret', bay:'oriel',  level:0, focus:'stack' },
    receptionOnly: { tier:'secondEmpire', size:0.6,  storeys:2, accent:'tower',  bay:'canted', level:0, focus:'level' },
    chamberFloor:  { tier:'secondEmpire', size:0.6,  storeys:2, accent:'tower',  bay:'canted', level:1, focus:'level' },
    atticFloor:    { tier:'baronial',     size:0.6,  storeys:2, accent:'turret', bay:'oriel',  level:2, focus:'level' },
  };

  // ---- shading constants (fleet recipe, identical to every other iso rig) ----
  const GAIN = 3.1, BIAS = 2.55, EDGE = 0.16;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  function mulberry32(a){return function(){a|=0;a=a+0x6D2B79F5|0;let t=Math.imul(a^a>>>15,1|a);t=t+Math.imul(t^t>>>7,61|t)^t;return((t^t>>>14)>>>0)/4294967296;};}
  function hex2rgb(h){ return [parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]; }
  function rgb2hex(r,g,b){ const h=(n)=>Math.max(0,Math.min(255,Math.round(n))).toString(16).padStart(2,'0'); return '#'+h(r)+h(g)+h(b); }
  function mix(a,b,t){ const A=hex2rgb(a),B=hex2rgb(b); return rgb2hex(A[0]+(B[0]-A[0])*t,A[1]+(B[1]-A[1])*t,A[2]+(B[2]-A[2])*t); }
  function desat(hex,t){ const [r,g,b]=hex2rgb(hex); const l=0.3*r+0.59*g+0.11*b; return rgb2hex(r+(l-r)*t,g+(l-g)*t,b+(l-b)*t); }
  function hash2(a,b){ let h=(a*374761393 + b*668265263)>>>0; h=(h^(h>>13))*1274126177>>>0; return ((h^(h>>16))>>>0)/4294967296; }
  const r3=(v)=>+(+v).toFixed(3);
  const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));

  // ---- camera / projection (identical to ManorIso) ----
  function camBasis(opts){
    const dir=opts.dir||0, th=dir*Math.PI/4;
    const e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    return { ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e) };
  }
  function projVert(x,y,z,B){
    const xr=x*B.ct - y*B.stt, yr=x*B.stt + y*B.ct, zr=z;
    return { xr,yr,zr, sx:cx+xr*S, sy:groundY-(yr*B.se+zr*B.ce)*S, d:(yr*B.ce-zr*B.se) };
  }
  function normal(a,b,c){
    const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
    const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m];
  }
  function shadeOf(n, se, ce){ return n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2]; }
  function facesCamera(nx, ny, B){ return (nx*B.stt + ny*B.ct) < 0; }

  // ---- face builders (outward-normal winding) ----
  function F(v,mat,b,db,uv,tex,flat){ return { v, mat, b:b||0, db:db||0, uv:uv||null, tex:tex||null, flat:!!flat }; }
  function wallQ(out, x0,y0,x1,y1, z0,z1, mat, tex, b){
    const L=Math.hypot(x1-x0,y1-y0);
    out.push(F([[x0,y0,z0],[x1,y1,z0],[x1,y1,z1],[x0,y0,z1]], mat, b||0, 0, [[0,z0],[L,z0],[L,z1],[0,z1]], tex));
  }
  function slab(out, pts, z, mat, b, tex){
    const uv = tex ? pts.map(p=>[p[0],p[1]]) : null;
    out.push(F(pts.map(p=>[p[0],p[1],z]), mat, b||0, 0, uv, tex));
  }
  function quad(out, p0,p1,p2,p3, mat, b, tex){
    const u=Math.hypot(p1[0]-p0[0],p1[1]-p0[1],p1[2]-p0[2]);
    const v=Math.hypot(p3[0]-p0[0],p3[1]-p0[1],p3[2]-p0[2]);
    out.push(F([p0,p1,p2,p3], mat, b||0, 0, tex?[[0,0],[u,0],[u,v],[0,v]]:null, tex||null));
  }
  function boxSolid(out, x0,x1, y0,y1, z0,z1, mat, tex, b, topB){
    wallQ(out, x0,y0, x1,y0, z0,z1, mat, tex, b);
    wallQ(out, x1,y1, x0,y1, z0,z1, mat, tex, b);
    wallQ(out, x1,y0, x1,y1, z0,z1, mat, tex, b);
    wallQ(out, x0,y1, x0,y0, z0,z1, mat, tex, b);
    slab(out, [[x0,y0],[x1,y0],[x1,y1],[x0,y1]], z1, mat, (b||0)+(topB!=null?topB:0.25), tex);
  }
  function decalY(out, yv, ny, xs,xe, z0,z1, mat, b, tex, flat, db){
    const e=0.02*ny, uw=xe-xs, uh=z1-z0;
    const P = ny>0 ? [[xs,yv+e,z0],[xe,yv+e,z0],[xe,yv+e,z1],[xs,yv+e,z1]]
                   : [[xe,yv+e,z0],[xs,yv+e,z0],[xs,yv+e,z1],[xe,yv+e,z1]];
    out.push(F(P, mat, b||0, db!=null?db:0.06, tex?[[0,0],[uw,0],[uw,uh],[0,uh]]:null, tex||null, flat));
  }
  function decalX(out, xv, nx, ys,ye, z0,z1, mat, b, tex, flat, db){
    const e=0.02*nx, uw=ye-ys, uh=z1-z0;
    const P = nx>0 ? [[xv+e,ye,z0],[xv+e,ys,z0],[xv+e,ys,z1],[xv+e,ye,z1]]
                   : [[xv+e,ys,z0],[xv+e,ye,z0],[xv+e,ye,z1],[xv+e,ys,z1]];
    out.push(F(P, mat, b||0, db!=null?db:0.06, tex?[[0,0],[uw,0],[uw,uh],[0,uh]]:null, tex||null, flat));
  }
  function decalTop(out, x0,x1,y0,y1, z, mat, b, db){
    out.push(F([[x0,y0,z],[x1,y0,z],[x1,y1,z],[x0,y1,z]], mat, b||0, db!=null?db:0.05, null, null, true));
  }
  // n-gon arc prism: the turret drum's inner face, so a round accent gets a round room
  function arcWall(out, ox,oy, r, a0,a1, n, z0,z1, mat, tex, b, inward){
    for(let i=0;i<n;i++){
      const t0=a0+(a1-a0)*(i/n), t1=a0+(a1-a0)*((i+1)/n);
      const p0=[ox+Math.cos(t0)*r, oy+Math.sin(t0)*r], p1=[ox+Math.cos(t1)*r, oy+Math.sin(t1)*r];
      if(inward) wallQ(out, p0[0],p0[1], p1[0],p1[1], z0,z1, mat, tex, b);
      else wallQ(out, p1[0],p1[1], p0[0],p0[1], z0,z1, mat, tex, b);
    }
  }
  function arcSlab(out, ox,oy, r, a0,a1, n, z, mat, b, tex){
    const pts=[]; for(let i=0;i<=n;i++){ const t=a0+(a1-a0)*(i/n); pts.push([ox+Math.cos(t)*r, oy+Math.sin(t)*r]); }
    slab(out, pts, z, mat, b, tex);
  }

  // ---- textures: integer ramp deltas ----
  function boardTex(sp){ const SP=sp||0.16; return (u,v)=>{ const su=((u%SP)+SP)%SP;
    if(su<0.016) return -2; const t=hash2(Math.floor(u/SP), Math.floor(v*3)); return t<0.18?-1:(t>0.9?1:0); }; }
  function parquetTex(){ const B=0.42; return (u,v)=>{ const a=Math.floor(u/B), b=Math.floor(v/B);
    const fu=((u%B)+B)%B, fv=((v%B)+B)%B;
    if(fu<0.028||fv<0.028) return -2;
    const along=((a+b)&1) ? fv : fu, s=0.105;
    return (((along%s)+s)%s)<0.014 ? -1 : (hash2(a,b)>0.86?1:0); }; }
  function tileTex(sp){ const SP=sp||0.34; return (u,v)=>{ const su=((u%SP)+SP)%SP, sv=((v%SP)+SP)%SP;
    if(su<0.028||sv<0.028) return -2; const t=hash2(Math.floor(u/SP), Math.floor(v/SP)); return t<0.14?-1:(t>0.9?1:0); }; }
  function flagTex(){ const SP=0.62; return (u,v)=>{ const row=Math.floor(v/SP), off=(row&1)*SP*0.4;
    const su=(((u+off)%SP)+SP)%SP, sv=((v%SP)+SP)%SP;
    if(su<0.036||sv<0.036) return -2; const t=hash2(Math.floor((u+off)/SP),row); return t<0.3?-1:(t>0.86?1:0); }; }
  function plasterTex(){ return (u,v)=>{ const t=hash2(Math.floor(u*8), Math.floor(v*8)); return t<0.055?-1:0; }; }
  function panelTex(){ const B=0.72; return (u,v)=>{ const fu=((u%B)+B)%B; return (fu<0.05||fu>B-0.05)?-1:0; }; }
  function weaveTex(){ return (u,v)=>{ const t=hash2(Math.floor(u*14), Math.floor(v*14)); return t<0.22?-1:(t>0.88?1:0); }; }
  function subwayTex(){ const BW=0.30, BH=0.15; return (u,v)=>{ const row=Math.floor(v/BH), off=(row&1)*BW*0.5;
    const su=(((u+off)%BW)+BW)%BW, sv=((v%BH)+BH)%BH;
    return (su<0.024||sv<0.024)?-2:(hash2(Math.floor((u+off)/BW),row)<0.2?-1:0); }; }

  // ---- plan records ----
  function room(id,kind,level,x0,x1,y0,y1,extra){
    return Object.assign({ id, kind, level, x0:r3(x0), x1:r3(x1), y0:r3(y0), y1:r3(y1),
      area:r3(Math.abs((x1-x0)*(y1-y0))) }, extra||{});
  }
  function part(level, axis, plane, a0, a1, gaps){
    return { level, axis, plane:r3(plane), a0:r3(a0), a1:r3(a1), gaps:(gaps||[]).map(g=>[r3(g[0]),r3(g[1])]) };
  }
  function door(level, axis, plane, c, clearW, from, to, kind){
    return { level, axis, plane:r3(plane), c:r3(c), clearW:r3(clearW), from, to, kind:kind||'interior' };
  }

  // ---- THE LAYOUT ----------------------------------------------------------------------------
  // One pass over the levels the shell says are habitable. Bands and spine are computed ONCE, from
  // the reception level's box, so every plate shares them and the stairs line up by construction.
  function layout(sh, eb, T){
    const t=sh.wallT, hw=sh.Wd/2;
    const IX0=-hw+t, IX1=hw-t, IY0=-sh.Ln/2+t, IY1=sh.Ln/2-t;
    const Wi=IX1-IX0, Di=IY1-IY0;

    // habitable levels only. The raised basement inside the plinth is ~1 m of crawl space, so it is
    // reported as skipped rather than furnished with rooms nobody could stand up in.
    const habit = sh.levels.filter(L=>L.headroom>=2.0);
    const skipped = sh.levels.filter(L=>L.headroom<2.0)
      .map(L=>({ id:L.id, headroom:r3(L.headroom), why:'headroom under 2 m — plinth void, not a storey' }));
    // each level's own interior box, and the CORE box they all share. Stairs are anchored to the
    // core, so one flight footprint lands inside every plate it passes through — a mansard storey is
    // inset and a gable attic is cut back to where a body stands up under the slope.
    const boxes = habit.map(L=>{
      let x0=Math.max(IX0,L.box.x0), x1=Math.min(IX1,L.box.x1);
      let y0=Math.max(IY0,L.box.y0), y1=Math.min(IY1,L.box.y1);
      if(L.kind==='attic' && L.slope){
        const rise=(sh.Wd/2)*L.slope.pitch;
        const use=(sh.Wd/2)*(1-clamp(1.9/Math.max(rise,2.4),0.08,0.75));
        x0=Math.max(x0,-use); x1=Math.min(x1,use);
      }
      return { x0:r3(x0), x1:r3(x1), y0:r3(y0), y1:r3(y1) };
    });
    const core={ x0:Math.max.apply(null,boxes.map(b=>b.x0)), x1:Math.min.apply(null,boxes.map(b=>b.x1)),
                 y0:Math.max.apply(null,boxes.map(b=>b.y0)), y1:Math.min.apply(null,boxes.map(b=>b.y1)) };

    const sw = clamp(Wi*0.26, 2.90, 3.60);             // spine: a passage plus the service flight
    const xL = -sw/2, xR = sw/2;
    const mid = clamp(Di*0.30, 4.90, 5.80);            // the stair hall's depth, front to back
    const rem = Di - mid, fd = rem*0.54, rd = rem*0.46;
    const yF0 = IY1 - fd, yR1 = IY0 + rd;
    // the service passage bulges left in the rear band so the back flight has a run worth walking
    const pxL = Math.max(core.x0, xL-2.85);

    const DW = 0.95, DWs = 0.85;                       // principal / service door clear widths
    const STAIR_HEADROOM=2.00, STAIR_SLAB=0.30, LANDING=1.00;
    const hallW = xR - core.x0;
    const mainRun = Math.min(hallW-2.20, 5.60), mainD = 1.34;
    const svcRun  = Math.min(xR-pxL-2.20, 4.30), svcD = 1.06;
    const riseT = habit.length>1 ? habit[1].floorZ-habit[0].floorZ : sh.storeyH;                   // storey rise a flight has to climb
    const stepsFor=(run)=>Math.ceil(riseT/0.20);
    // BOTH flights run across their band, against a wall no room opens through: the main one along
    // the stair hall's front partition, the service one along the rear outer wall. That is what
    // frees every spine plane for doors — a flight laid along the spine wall would seal off the
    // rooms behind it, which is exactly the mistake this plan is built to avoid.
    const mkStair=(lv,kind)=>{
      const riseT=habit[lv+1].floorZ-habit[lv].floorZ;
      const stepsFor=()=>Math.ceil(riseT/0.20);
      const decorate=st=>{const run=st.x1-st.x0;
        // Count entire treads requiring the opening, including slab depth and a head envelope.
        const covered=Math.max(0,Math.floor((riseT-STAIR_SLAB-STAIR_HEADROOM)/(riseT/st.steps)));
        st.voidA0=st.x0; st.voidA1=r3(st.x1-covered*run/st.steps);
        st.floorRise=r3(riseT);st.headroom=STAIR_HEADROOM;st.slabThickness=STAIR_SLAB;
        st.bottomLanding={x0:st.x1,x1:r3(st.x1+LANDING),y0:st.y0,y1:st.y1};
        st.topLanding={x0:r3(st.x0-LANDING),x1:st.x0,y0:st.y0,y1:st.y1};
        return st;};
      if(kind==='main'){
        const x1=xR-1.10, x0=x1-mainRun, y1=yF0-0.30, y0=y1-mainD, n=stepsFor(mainRun);
        return decorate({ id:'main_l'+lv, kind:'main', level:lv, axis:'x', steps:n,
          x0:r3(x0), x1:r3(x1), y0:r3(y0), y1:r3(y1),
          going:r3(mainRun/n), rise:r3(riseT/n), railSide:'low', open:'x0',
          voidA0:0, voidA1:0 });
      }
      const x1=xR-1.10, x0=x1-svcRun, y0=core.y0+0.16, y1=y0+svcD, n=stepsFor(svcRun);
      return decorate({ id:'svc_l'+lv, kind:'svc', level:lv, axis:'x', steps:n,
        x0:r3(x0), x1:r3(x1), y0:r3(y0), y1:r3(y1),
        going:r3(svcRun/n), rise:r3(riseT/n), railSide:'high', open:'x0',
        voidA0:0, voidA1:0 });
    };
    // the hole in the plate above — published by the stair so nothing can disagree with it
    const voidOf=(st)=> !st ? null : { id:st.id, kind:st.kind, open:st.open,
      topLanding:st.topLanding, x0:st.voidA0, x1:st.voidA1, y0:r3(st.y0-0.07), y1:r3(st.y1+0.07) };

    const levels=[], rooms=[], parts=[], doors=[], stairs=[];
    const nMasonry = sh.storeys;

    habit.forEach((L,i)=>{
      const isAttic = L.kind==='attic';
      const box = boxes[i], bx0=box.x0, bx1=box.x1, by0=box.y0, by1=box.y1;
      const ceil = Math.min(L.headroom, isAttic?2.55:3.30);
      // The middle band's left zone is NOT a room — it belongs to the stair hall, which is why the
      // hall is a hall and not a corridor, and why the flight has an outer wall to lean on.
      const Z={
        LF:{x0:bx0,x1:xL,y0:yF0,y1:by1}, RF:{x0:xR,x1:bx1,y0:yF0,y1:by1},
        HALL:{x0:bx0,x1:xR,y0:yR1,y1:yF0}, RM:{x0:xR,x1:bx1,y0:yR1,y1:yF0},
        LR:{x0:bx0,x1:pxL,y0:by0,y1:yR1}, RR:{x0:xR,x1:bx1,y0:by0,y1:yR1},
        SF:{x0:xL,x1:xR,y0:yF0,y1:by1}, SR:{x0:pxL,x1:xR,y0:by0,y1:yR1},
      };
      const lv=i;
      const stMain = i < habit.length-1 ? mkStair(lv,'main') : null;
      const stSvc  = i < habit.length-1 ? mkStair(lv,'svc')  : null;
      const inMain = i>0 ? voidOf(mkStair(lv-1,'main')) : null;   // the hole THIS plate carries
      const inSvc  = i>0 ? voidOf(mkStair(lv-1,'svc'))  : null;
      const myVoids = [inMain,inSvc].filter(Boolean);

      // ---- what may not be crossed by a door on each spine plane, on this level. The principal
      // flight is inside the hall and touches no spine plane; the service flight crosses the rear
      // end of both, so its footprint and its void above are barred there.
      const forbidL=[[yF0-0.10,yF0+0.10],[yR1-0.10,yR1+0.10]];
      const forbidR=[[yF0-0.10,yF0+0.10],[yR1-0.10,yR1+0.10]];
      for(const s of [stSvc,inSvc]) if(s){
        const g=[s.y0-0.08, s.y1+0.08];
        forbidL.push(g); forbidR.push(g);
      }

      const R=(id,kind,z,extra)=>{ const r=room(id,kind,lv,z.x0,z.x1,z.y0,z.y1,extra); rooms.push(r); return r; };
      const okZone=(z)=>(z.x1-z.x0)>1.35 && (z.y1-z.y0)>1.35;
      // Pick a door centre on a spine plane: the room's centre if it is clear, else walk outward in
      // 50 mm steps. Sampling rather than a handful of guesses is what makes the answer reliable at
      // every size — and canJamb() asks the same question before a zone is ever split in two, so a
      // split never produces a room the door solver cannot serve.
      const centreOn=(r, forbid, w)=>{
        const lo=r.y0+w/2+0.28, hi=r.y1-w/2-0.28;
        if(hi<lo) return null;
        const mid0=clamp((r.y0+r.y1)/2, lo, hi);
        const free=(c)=>!forbid.some(g=>c>g[0]-w/2-0.02 && c<g[1]+w/2+0.02);
        if(free(mid0)) return mid0;
        for(let d=0.05; d<=hi-lo+0.001; d+=0.05){
          for(const c of [mid0-d, mid0+d]) if(c>=lo && c<=hi && free(c)) return c;
        }
        return null;
      };
      const canJamb=(y0,y1,w,forbid)=> centreOn({y0,y1}, forbid, w)!=null;
      const spineDoor=(r, side, w, kind)=>{
        const rear = r.y0 < yR1+0.01;
        const plane = side==='L' ? (rear?pxL:xL) : xR;
        const c = centreOn(r, side==='L'?forbidL:forbidR, w||DW);
        const sid = rear ? rId : (r.y1>yF0-0.01 ? fId : mId);
        if(c==null){ unplacedDoors.push({room:r.id, level:lv, why:'no clear jamb on the spine wall'}); return null; }
        const d=door(lv,'y',plane,c,w||DW, sid, r.id, kind||'interior');
        doors.push(d);
        (side==='L' ? (rear?gapsLR:gapsLF) : gapsR).push([c-(w||DW)/2, c+(w||DW)/2]);
        return d;
      };
      const innerDoor=(from, to, axis, plane, c, w)=>{
        doors.push(door(lv,axis,plane,c,w||DW, from.id, to.id, 'interior'));
        return { axis, plane, c, w:w||DW };
      };
      const gapsLF=[], gapsLR=[], gapsR=[], unplacedDoors=[];

      // ---- the spine: vestibule, stair hall, service passage — one route, never interrupted
      const fId='l'+lv+'_hallF', mId='l'+lv+'_hallM', rId='l'+lv+'_hallR';
      const tag=(s)=>'l'+lv+'_'+s;
      if(lv===0){
        R(fId,'vestibule',Z.SF,{ entry:true, floor:'hall' });
        R(mId,'stairhall',Z.HALL,{ stair:true, floor:'hall', well:true, carriage:!!eb.porte });
        R(rId,'passage',Z.SR,{ service:true, floor:'hall' });
      } else {
        R(fId,'landing',Z.SF,{ floor:'hall', upper:true });
        R(mId,'landing',Z.HALL,{ stair:true, floor:'hall', well:true, gallery:true });
        R(rId,'passage',Z.SR,{ service:true, floor:'hall' });
      }
      // vestibule to stair hall: a cased opening on the axis of the front door, full spine width
      doors.push(door(lv,'x',yF0, 0, sw-0.70, fId, mId, 'open'));
      // the baize door: the one door between the family's side of the spine and the service side
      const bz = clamp(pxL+0.62, pxL+0.55, xR-0.55);
      doors.push(door(lv,'x',yR1, bz, DWs, mId, rId, lv===0?'baize':'interior'));
      parts.push(part(lv,'x',yR1, pxL, xR, [[bz-DWs/2, bz+DWs/2]]));

      // ---- the rooms either side, by level programme. Fireplaces go on the INBOARD band walls,
      // back to back, so two flues share one stack — which is where the exterior's stacks stand.
      const K = i===0 ? 'reception' : (isAttic ? 'attic' : 'chamber');
      if(K==='reception'){
        const drawing = okZone(Z.LF) ? R(tag('drawing'),'drawing',Z.LF,{ fire:'rear', principal:true }) : null;
        // front-right: library, and the baronial oriel projects from this elevation
        const library = okZone(Z.RF) ? R(tag('library'),'library',Z.RF,{ fire:'rear' }) : null;
        const dining = okZone(Z.RM) ? R(tag('dining'),'dining',Z.RM,{ fire:'front', principal:true }) : null;
        const kitchen = okZone(Z.LR) ? R(tag('kitchen'),'kitchen',Z.LR,{ service:true, floor:'hall', backDoor:true }) : null;
        // rear-right splits along y, so BOTH halves keep the spine wall and their own door
        let scullery=null, pantry=null;
        if(okZone(Z.RR)){
          const cut = Z.RR.y0 + (Z.RR.y1-Z.RR.y0)*0.58;
          if(cut-Z.RR.y0>1.5 && Z.RR.y1-cut>1.5 &&
             canJamb(Z.RR.y0,cut,DWs,forbidR) && canJamb(cut,Z.RR.y1,DWs,forbidR)){
            scullery=R(tag('scullery'),'scullery',{x0:Z.RR.x0,x1:Z.RR.x1,y0:Z.RR.y0,y1:cut},{ service:true, wet:true, floor:'hall' });
            pantry  =R(tag('pantry'),'pantry',{x0:Z.RR.x0,x1:Z.RR.x1,y0:cut,y1:Z.RR.y1},{ service:true, floor:'hall' });
            parts.push(part(lv,'x',cut, Z.RR.x0, Z.RR.x1, []));
          } else scullery=R(tag('scullery'),'scullery',Z.RR,{ service:true, wet:true, floor:'hall' });
        }
        for(const [r,sd] of [[drawing,'L'],[library,'R'],[dining,'R'],[kitchen,'L'],[scullery,'R'],[pantry,'R']])
          if(r) spineDoor(r, sd, r.service?DWs:DW);
      } else if(K==='chamber'){
        const first = i===1;
        // front-left: the principal chamber with a dressing room behind it (x-split: the dressing
        // room does NOT touch the spine, so it opens off the chamber — the one inner door per plate)
        let bed1=null, dressing=null;
        if(okZone(Z.LF)){
          const cut = Z.LF.x0 + (Z.LF.x1-Z.LF.x0)*0.36;
          if(first && cut-Z.LF.x0>1.9){
            dressing=R(tag('dressing'),'dressing',{x0:Z.LF.x0,x1:cut,y0:Z.LF.y0,y1:Z.LF.y1},{});
            bed1=R(tag('bed1'),'bed',{x0:cut,x1:Z.LF.x1,y0:Z.LF.y0,y1:Z.LF.y1},{ principal:true, fire:'rear', beds:1 });
            const dc=clamp((Z.LF.y0+Z.LF.y1)/2+0.4, Z.LF.y0+0.85, Z.LF.y1-0.85);
            parts.push(part(lv,'y',cut, Z.LF.y0, Z.LF.y1, [[dc-DW/2,dc+DW/2]]));
            innerDoor(bed1,dressing,'y',cut,dc,DW);
          } else bed1=R(tag('bed1'),'bed',Z.LF,{ principal:first, fire:'rear', beds:1 });
        }
        const bed2 = okZone(Z.RF) ? R(tag('bed2'),'bed',Z.RF,{ beds:1, fire:'rear' }) : null;
        // right-middle: bathroom over the canted bay (a tub in the bay), water closet behind it
        let bath=null, wc=null;
        if(okZone(Z.RM)){
          const cut = Z.RM.y0 + Math.min(1.85, (Z.RM.y1-Z.RM.y0)*0.34);
          if(cut-Z.RM.y0>1.35 && Z.RM.y1-cut>2.2 &&
             canJamb(Z.RM.y0,cut,DWs,forbidR) && canJamb(cut,Z.RM.y1,DW,forbidR)){
            wc  =R(tag('wc'),'wc',{x0:Z.RM.x0,x1:Z.RM.x1,y0:Z.RM.y0,y1:cut},{ wet:true });
            bath=R(tag('bath'),'bath',{x0:Z.RM.x0,x1:Z.RM.x1,y0:cut,y1:Z.RM.y1},{ wet:true });
            parts.push(part(lv,'x',cut, Z.RM.x0, Z.RM.x1, []));
          } else bath=R(tag('bath'),'bath',Z.RM,{ wet:true });
        }
        const rearKind = (!first || nMasonry>2) && i===2 ? 'nursery' : 'bed';
        const rear = okZone(Z.LR) ? R(tag(rearKind==='nursery'?'nursery':'bed3'), rearKind, Z.LR, { beds:1, fire:'front' }) : null;
        let linen=null, servant=null;
        if(okZone(Z.RR)){
          const cut = Z.RR.y0 + (Z.RR.y1-Z.RR.y0)*0.58;
          if(cut-Z.RR.y0>1.5 && Z.RR.y1-cut>1.5 &&
             canJamb(Z.RR.y0,cut,DWs,forbidR) && canJamb(cut,Z.RR.y1,DWs,forbidR)){
            servant=R(tag('servant'),'servant',{x0:Z.RR.x0,x1:Z.RR.x1,y0:Z.RR.y0,y1:cut},{ beds:1, service:true });
            linen  =R(tag('linen'),'linen',{x0:Z.RR.x0,x1:Z.RR.x1,y0:cut,y1:Z.RR.y1},{ service:true });
            parts.push(part(lv,'x',cut, Z.RR.x0, Z.RR.x1, []));
          } else linen=R(tag('linen'),'linen',Z.RR,{ service:true });
        }
        for(const [r,sd] of [[bed1,'L'],[bed2,'R'],[bath,'R'],[wc,'R'],[rear,'L'],[servant,'R'],[linen,'R']])
          if(r) spineDoor(r, sd, r.service||r.kind==='wc'?DWs:DW);
      } else {
        // attic: servants' rooms, box room and linen off the same spine, served by the back stair.
        // The zones split in half along y so a servant's room is a servant's room and not a suite.
        const a=[];
        const cell=(z, aName,aKind, bName,bKind)=>{
          if(!okZone(z)) return;
          const side = z.x0>0 ? 'R' : 'L', d=z.y1-z.y0, fb = side==='R'?forbidR:forbidL;
          const beds=(k)=>k==='servant'?1:0;
          const cut=z.y0+d*0.5;
          if(bName && d>3.4 && canJamb(z.y0,cut,DWs,fb) && canJamb(cut,z.y1,DWs,fb)){
            a.push([R(tag(aName),aKind,{x0:z.x0,x1:z.x1,y0:cut,y1:z.y1},{service:true, beds:beds(aKind)}), side]);
            a.push([R(tag(bName),bKind,{x0:z.x0,x1:z.x1,y0:z.y0,y1:cut},{service:true, beds:beds(bKind)}), side]);
            parts.push(part(lv,'x',cut, z.x0, z.x1, []));
          } else a.push([R(tag(aName),aKind,z,{service:true, beds:beds(aKind)}), side]);
        };
        cell(Z.LF,'serv_a','servant','serv_b','servant');
        cell(Z.RF,'serv_c','servant','serv_d','servant');
        cell(Z.RM,'boxroom','boxroom');
        cell(Z.LR,'serv_e',Z.LR.x1-Z.LR.x0<2.05?'store':'servant','store','store');
        cell(Z.RR,'linen2','linen','store2','store');
        for(const [r,sd] of a) spineDoor(r, sd, DWs);
      }

      // ---- the accent, as a real interior room, in the accent's OWN geometry
      let accentRoom=null;
      const ac=sh.accent;
      if(ac && ac.kind==='turret'){
        // a drum on the front-right corner: an alcove off the front-right room on every level
        const host = rooms.filter(r=>r.level===lv && r.x1>xR+0.01 && r.y1>yF0-0.01)[0];
        accentRoom = { kind:'turret', host:host?host.id:fId, cx:ac.cx, cy:ac.cy, r:ac.rIn };
      } else if(ac && ac.kind==='tower'){
        const tw=Math.min(ac.w, sw+1.2);
        accentRoom = { kind:'tower', host:fId, x0:-tw/2, x1:tw/2, y0:IY1, y1:IY1+ac.proj,
          balcony: lv===1 };
      }
      // ---- projecting bay / oriel, as an alcove of the room it belongs to
      let alcove=null;
      if(eb.bay==='canted' && lv<nMasonry){
        const host = rooms.filter(r=>r.level===lv && r.x0>xL+0.01 && r.y0<0.01 && r.y1>-0.01)[0];
        if(host) alcove={ kind:'canted', host:host.id, side:'+X', x0:IX1, x1:IX1+1.15, y0:-1.72, y1:1.72 };
      } else if(eb.bay==='oriel' && lv===1){
        const oc = hw - 0.28*sh.Wd;
        const host = rooms.filter(r=>r.level===lv && r.y1>yF0-0.01 && oc>=r.x0 && oc<=r.x1)[0];
        if(host) alcove={ kind:'oriel', host:host.id, side:'+Y', x0:oc-1.18, x1:oc+1.18, y0:IY1, y1:IY1+1.20 };
      }

      // ---- partitions: the spine walls with every door's gap, and the band walls in the zones.
      // Across the middle band there IS no spine wall — the stair hall runs through to the outer
      // wall, which is the wall the principal flight leans on.
      parts.push(part(lv,'y',xL, yF0, box.y1, gapsLF));
      parts.push(part(lv,'y',pxL, box.y0, yR1, gapsLR));
      parts.push(part(lv,'y',xR, box.y0, box.y1, gapsR));
      if(box.x0 < xL-0.05) parts.push(part(lv,'x',yF0, box.x0, xL, []));
      if(box.x0 < pxL-0.05) parts.push(part(lv,'x',yR1, box.x0, pxL, []));
      if(box.x1 > xR+0.05){ parts.push(part(lv,'x',yF0, xR, box.x1, []));
        parts.push(part(lv,'x',yR1, xR, box.x1, [])); }

      levels.push({ index:lv, id:L.id, kind:L.kind, shellKind:L.kind, isAttic,
        floorZ:r3(L.floorZ), ceilZ:r3(L.floorZ+ceil), ceil:r3(ceil), headroom:r3(L.headroom),
        box, slope:L.slope||null, note:L.note||null,
        stairs:[stMain,stSvc].filter(Boolean), voids:myVoids,
        accent:accentRoom, alcove, unplacedDoors });
      if(stMain) stairs.push(stMain);
      if(stSvc) stairs.push(stSvc);
    });

    // the street door, and the tradesman's door into the kitchen — both from the shell's openings
    const entry = (sh.openings||[]).find(o=>o.kind==='door');
    doors.push(door(0,'x',IY1, entry?entry.x:0, entry?entry.w:1.9, 'outside', 'l0_hallF', 'entry'));
    const kit = rooms.find(r=>r.level===0 && r.kind==='kitchen');
    doors.push(door(0,'x',IY0, kit?kit.x0+0.95:pxL+0.8, 0.95, 'outside',
      kit?kit.id:'l0_hallR', 'service'));

    return { levels, rooms, parts, doors, stairs, skipped,
      spine:{ sw:r3(sw), xL:r3(xL), xR:r3(xR), pxL:r3(pxL), yF0:r3(yF0), yR1:r3(yR1),
              hallW:r3(hallW), mainRun:r3(mainRun), svcRun:r3(svcRun),
              mainGoing:r3(mainRun/stepsFor(mainRun)), mainRise:r3(riseT/stepsFor(mainRun)),
              svcGoing:r3(svcRun/stepsFor(svcRun)),
              walkFront:r3(sw), walkMid:r3(mid-mainD-0.30), walkRear:r3(rd-svcD-0.16) },
      box:{ x0:r3(IX0), x1:r3(IX1), y0:r3(IY0), y1:r3(IY1), Wi:r3(Wi), Di:r3(Di) } };
  }

  // ---- access audit: walk the door graph from the street ----
  function walkAccess(L){
    const adj={}, add=(a,b)=>{ (adj[a]=adj[a]||[]).push(b); };
    for(const d of L.doors){ add(d.from+'@'+d.level, d.to+'@'+d.level); add(d.to+'@'+d.level, d.from+'@'+d.level); }
    // a flight joins its own band on this level to the same band on the next
    for(const st of L.stairs){
      const band = st.kind==='main' ? '_hallM' : '_hallR';
      add('l'+st.level+band+'@'+st.level, 'l'+(st.level+1)+band+'@'+(st.level+1));
      add('l'+(st.level+1)+band+'@'+(st.level+1), 'l'+st.level+band+'@'+st.level);
    }
    const seen={}, q=['outside@0'], depth={'outside@0':0};
    seen['outside@0']=1;
    while(q.length){ const n=q.shift();
      for(const m of (adj[n]||[])) if(!seen[m]){ seen[m]=1; depth[m]=depth[n]+1; q.push(m); } }
    const out=[];
    for(const r of L.rooms){ const k=r.id+'@'+r.level;
      out.push({ id:r.id, kind:r.kind, level:r.level, area:r.area,
        reachable:!!seen[k], doors:depth[k]!=null?depth[k]:null }); }
    return out;
  }

  // ---- resolve -------------------------------------------------------------------------------
  function resolve(opts){
    const M=root.ManorIso; if(!M) return null;
    const o=opts||{};
    const sh=M.shell(o), eb=M.resolved(o);
    const tier = TIERS[eb.tier] ? eb.tier : 'secondEmpire';
    const T=TIERS[tier];
    const L=layout(sh,eb,T);
    const focus = o.focus==='level' ? 'level' : 'stack';
    const level = clamp(Math.round(o.level!=null?o.level:0), 0, L.levels.length-1);
    const levelList = focus==='level' ? [level] : L.levels.map((_,i)=>i);
    return { tier, T, sh, eb, L, focus, level, levelList,
      explode: levelList.length>1 ? (o.explode!=null?o.explode:EXPLODE) : 0,
      furnish: o.furnish!==false, night:!!o.night,
      surfaceCalm:clamp(Number(o.surfaceCalm)||0,0,1),
      weather: o.weather!=null?o.weather:T.weather,
      outline: o.outline!=null?!!o.outline:KEYLINE_DEFAULT,
      dir:o.dir||0, elev:o.elev!=null?o.elev:DEFAULT_ELEV };
  }

  function makeMats(b){
    const T=b.T, wx=b.weather, night=b.night;
    const wth=(ramp)=>ramp.map(c=>{ let x=desat(c, wx*0.35); x=mix(x,'#6b675e',wx*0.18);
      if(night) x=mix(x,'#2a2216',0.20); return x; });
    return {
      floor:{ramp:wth(FINISHES[T.floor])}, hallFloor:{ramp:wth(FINISHES[T.hallFloor])},
      wet:{ramp:wth(FINISHES[T.wet])}, wall:{ramp:wth(FINISHES[T.wall])},
      panel:{ramp:wth(FINISHES[T.panel])}, wallTile:{ramp:wth(FINISHES.wallTile)},
      joist:{ramp:wth(FINISHES.joist)}, join:{ramp:wth(FINISHES[T.join])},
      runner:{ramp:wth(FINISHES[T.runner])}, stone:{ramp:wth(FINISHES.stoneTop)},
      metal:{ramp:wth(FINISHES.metal)}, brass:{ramp:wth(FINISHES.brass)},
      damask:{ramp:wth(FINISHES.damask)},
      glass:{ramp: night?GLASS_NIGHT:GLASS_DAY}, cavity:{ramp:CAVITY},
    };
  }

  // ---- placement ------------------------------------------------------------------------------
  function P(out, ctx, o){
    boxSolid(out, o.x0,o.x1, o.y0,o.y1, o.z0,o.z1, o.mat, o.tex||null, o.b||0, o.topB!=null?o.topB:0.25);
    if(o.blocker!==false) ctx.blockers.push({ kind:o.kind||'prop', level:ctx.level,
      x0:r3(o.x0),x1:r3(o.x1),y0:r3(o.y0),y1:r3(o.y1), h:r3(o.z1) });
  }
  function A(ctx, verb, room, x, y, z, extra){
    ctx.anchors.push(Object.assign({ verb, room, level:ctx.level, x:r3(x), y:r3(y), z:r3(z||0) }, extra||{}));
  }
  function finite(r){ return Number.isFinite(r.x0)&&Number.isFinite(r.x1)&&Number.isFinite(r.y0)&&Number.isFinite(r.y1); }
  function hits(a,c){ if(!finite(c)||!finite(a)) return false;
    return !(a.x1<=c.x0+0.02 || a.x0>=c.x1-0.02 || a.y1<=c.y0+0.02 || a.y0>=c.y1-0.02); }
  function blocked(ctx, rect, tall){
    for(const c of ctx.clear) if(hits(rect,c)) return c.tag||'clearance';
    if(tall) for(const c of ctx.clearTall) if(hits(rect,c)) return c.tag||'a window';
    for(const t of ctx.taken) if(hits(rect,t)) return t.name||'a prop';
    return null;
  }
  const WALLFACE = { rear:'S', front:'N', left:'E', right:'W' };
  // pieces the programme would like but a room can do without: their absence is reported, but it is
  // not a plan failure the way a missing bed or a missing range would be
  const OPTIONAL = { seaChest:1, crate:1, barrel:1, mirror:1, towelRail:1, hutch:1, cupboard:1,
    longClock:1, piano:1, bookcase:1, dresser:1, rug:1, shelf:1, lockers:1, armchair:1,
    roundTable:1, stool:1, chair:1, hallStand:1, bench:1, washstand:1 };
  const OPPOSITE3 = { N:'S', S:'N', E:'W', W:'E' };
  const OPPWALL = { rear:'front', front:'rear', left:'right', right:'left' };

  // Place a prop against a wall of `r` (or free inside it). Nothing lands on a wall band, a doorway,
  // a flight, a stairwell void or a hearth: those are in ctx.clear before the first prop. A blocked
  // slot slides along the same wall, then pulls off it, then reports itself unplaced — a tight room
  // gets less kit rather than a collision.
  function put(out, ctx, r, z, name, opts, pl){
    const PI=root.PropIso; if(!PI||!PI.emit) return null;
    pl=pl||{};
    const spec=PI.PROPS[name]; if(!spec) return null;
    // A room rect's edge is the CENTRELINE of a partition on that side and the inner FACE of the
    // outer wall. Rather than carry two conventions into every call, the placement rect is pulled
    // in by half a partition plus a hair, and every candidate is measured off that. So `inset:0.02`
    // means flush against the plaster whichever kind of wall it is, and nothing lands inside one.
    const pad = pl.pad!=null?pl.pad:0.08;
    const R2 = { x0:r.x0+pad, x1:r.x1-pad, y0:r.y0+pad, y1:r.y1-pad };
    let face = pl.face || WALLFACE[pl.wall] || 'N';
    if(spec.headToWall && pl.wall && pl.wall!=='free') face = OPPOSITE3[face] || face;
    const o=Object.assign({ weather:ctx.weather, night:ctx.night }, opts||{});
    const rot=(face==='E'||face==='W');
    if(spec.runBase!=null && o.len!=null && pl.fitRun!==false){
      const avail=((pl.wall==='left'||pl.wall==='right')?(R2.y1-R2.y0):(R2.x1-R2.x0)) - 2*(pl.margin!=null?pl.margin:0.16);
      const maxLen=(avail-spec.runBase)/spec.runK;
      if(maxLen<o.len) o.len=Math.max(0,maxLen);
    }
    const fp=PI.footprint(name,o);
    const fw=rot?fp.d:fp.w, fd=rot?fp.w:fp.d;
    const m=pl.margin!=null?pl.margin:0.16, ins=pl.inset!=null?pl.inset:0.02;
    const rw=R2.x1-R2.x0, rd=R2.y1-R2.y0;
    const onX=(pl.wall==='rear'||pl.wall==='front'), onY=(pl.wall==='left'||pl.wall==='right');
    const rej=(why)=>{ ctx.rejects.push({ prop:name, room:(pl.room||r.id||'?'), level:ctx.level, why,
      optional: !!(pl.soft || OPTIONAL[name]) }); return null; };
    if(rw<0.2||rd<0.2) return rej('no floor to stand on');
    if(onX && (fw>rw-2*m || fd>rd-0.12)) return rej('room too small: needs '+fw.toFixed(2)+'×'+fd.toFixed(2)+', room '+rw.toFixed(2)+'×'+rd.toFixed(2));
    if(onY && (fd>rd-2*m || fw>rw-0.12)) return rej('room too small: needs '+fw.toFixed(2)+'×'+fd.toFixed(2)+', room '+rw.toFixed(2)+'×'+rd.toFixed(2));
    const cand=(t,dIns)=>{
      const off=ins+(dIns||0); let px,py;
      if(onX){ px=R2.x0+m+fw/2 + Math.max(0,rw-2*m-fw)*t; py = pl.wall==='rear' ? R2.y0+off+fd/2 : R2.y1-off-fd/2; }
      else if(onY){ py=R2.y0+m+fd/2 + Math.max(0,rd-2*m-fd)*t; px = pl.wall==='left' ? R2.x0+off+fw/2 : R2.x1-off-fw/2; }
      else { px=R2.x0+rw*(pl.fx!=null?pl.fx:0.5); py=R2.y0+rd*(pl.fy!=null?pl.fy:0.5); }
      px+=pl.dx||0; py+=pl.dy||0;
      return { cx:px, cy:py, x0:px-fw/2, x1:px+fw/2, y0:py-fd/2, y1:py+fd/2 };
    };
    let rect=cand(pl.along!=null?pl.along:0.5);
    const outside=(c)=> c.x0<R2.x0-0.001||c.x1>R2.x1+0.001||c.y0<R2.y0-0.001||c.y1>R2.y1+0.001;
    let first = !pl.ignoreClear && blocked(ctx, rect, pl.tall);
    if(!first && outside(rect)) first='the room itself';
    if(first){
      let ok=null;
      const pullOff = spec.headToWall ? [0] : [0,0.26,0.5];
      if(onX||onY){ outerW: for(const dIns of pullOff){
          for(const t of [0.5,0,1,0.25,0.75,0.12,0.88,0.38,0.62]){
            const c=cand(t,dIns);
            if(outside(c)) continue;
            if(!blocked(ctx,c,pl.tall)){ ok=c; break outerW; } } } }
      else { const bx=pl.fx!=null?pl.fx:0.5, by=pl.fy!=null?pl.fy:0.5;
        outer: for(const dy of [0,-0.12,0.12,-0.24,0.24,-0.36,0.36])
          for(const dx of [0,-0.12,0.12,-0.22,0.22]){
            const t=clamp(bx+dx,0.10,0.90), v=clamp(by+dy,0.10,0.90);
            const c={cx:R2.x0+rw*t+(pl.dx||0), cy:R2.y0+rd*v+(pl.dy||0)};
            c.x0=c.cx-fw/2; c.x1=c.cx+fw/2; c.y0=c.cy-fd/2; c.y1=c.cy+fd/2;
            if(outside(c)) continue;
            if(!blocked(ctx,c,pl.tall)){ ok=c; break outer; } } }
      if(!ok) return rej('no clear slot — blocked by '+first);
      rect=ok;
    }
    const prefix='q'+(ctx.pn++)+'_';
    const em=PI.emit(name,o,{ x:rect.cx, y:rect.cy, z, face, prefix });
    for(const fc of em.faces){ fc.propKind=name; fc.propRoom=pl.room||r.id; out.push(fc); }
    for(const k in em.mats) ctx.mats[k]=em.mats[k];
    rect.name=name; rect.face=face; rect.h=em.h; rect.wall=pl.wall||'free';
    ctx.blockers.push({ kind:name, level:ctx.level, room:(pl.room||r.id||null),
      x0:r3(rect.x0),x1:r3(rect.x1),y0:r3(rect.y0),y1:r3(rect.y1), h:r3(em.h), face });
    if(!pl.ignoreClear) ctx.taken.push(rect);
    return rect;
  }
  function putAny(out, ctx, r, z, name, opts, walls, pl){
    const mark=ctx.rejects.length;
    for(const wl of walls){
      const got=put(out,ctx,r,z,name,opts,Object.assign({},pl||{},{wall:wl}));
      if(got){ ctx.rejects.length=mark; return got; }
    }
    return null;
  }

  // ---- furnishing ----------------------------------------------------------------------------
  // A hearth is placed FIRST in any room that has one, because it owns the best wall and the seating
  // is arranged to face it. Its hearth stone goes into ctx.clear so nothing stands in the fire.
  function hearth(out, ctx, r, b, z){
    // a room too small to hold a hearth and the furniture it is meant to warm gets neither
    if(!r.fire || !b.eb.chimneys || ctx.fireBudget<=0 || r.area<13) return null;
    const K=b.T.kit;
    const fp=put(out,ctx,r,z,'fireplace',{ variant:K.mantel, worktop: K.mantel?'slate':'marble' },
      { wall:r.fire, along:0.5, inset:0.02, room:r.id, tall:true });
    if(!fp) return null;
    ctx.fireBudget--;
    const dep=0.55;
    const zone = (r.fire==='left'||r.fire==='right')
      ? { tag:'the hearth', x0: r.fire==='left'?fp.x1:fp.x0-dep, x1: r.fire==='left'?fp.x1+dep:fp.x0, y0:fp.y0, y1:fp.y1 }
      : { tag:'the hearth', x0:fp.x0, x1:fp.x1, y0: r.fire==='rear'?fp.y1:fp.y0-dep, y1: r.fire==='rear'?fp.y1+dep:fp.y0 };
    ctx.clear.push(zone);
    ctx.flues.push({ room:r.id, level:ctx.level, wall:r.fire, x:r3(fp.cx), y:r3(fp.cy) });
    const fx = (r.fire==='left') ? fp.x1+0.42 : (r.fire==='right') ? fp.x0-0.42 : fp.cx;
    const fy = (r.fire==='rear') ? fp.y1+0.42 : (r.fire==='front') ? fp.y0-0.42 : fp.cy;
    A(ctx,'fire',r.id, fx, fy, z, {host:'fireplace'});
    return fp;
  }
  function diningSet(out, ctx, r, b, z, sub, len, soft){
    const K=b.T.kit;
    // an exact-rect placement is not bounds-checked by put(), so every seat is clamped inside the
    // room first — a chair three centimetres into the plaster is still a chair in the plaster
    const cx_=(v)=>clamp(v, r.x0+0.32, r.x1-0.32), cy_=(v)=>clamp(v, r.y0+0.30, r.y1-0.30);
    const tb=put(out,ctx,sub,z,'table',{len:len!=null?len:0.9, wood:K.wood},{wall:'free', fx:0.5, fy:0.5, room:r.id, soft:soft});
    if(!tb) return null;
    const n=Math.max(2, Math.min(4, Math.round((tb.x1-tb.x0)/0.72)));
    for(let i=0;i<n;i++){ const cx0=cx_(tb.x0+(tb.x1-tb.x0)*(i+0.5)/n);
      for(const s of [-1,1]){
        const cy0 = cy_(s<0 ? tb.y0-0.32 : tb.y1+0.32);
        if(Math.abs(cy0-(s<0?tb.y0-0.32:tb.y1+0.32))>0.02) continue;
        if(cy0-0.24<sub.y0-0.05 || cy0+0.24>sub.y1+0.05) continue;
        put(out,ctx,{x0:cx0-0.30,x1:cx0+0.30,y0:cy0-0.24,y1:cy0+0.24}, z, 'chair',
          {wood:K.wood, variant:1, fabric:K.fabric},{wall:'free', pad:0, face: s<0?'S':'N', room:r.id, soft:true});
        A(ctx,'dine',r.id, cx0, cy0, z+0.46, {seat:i+(s<0?'n':'s')});
      } }
    for(const ex of [[tb.x0-0.34,'E'],[tb.x1+0.34,'W']]){
      if(ex[0]<sub.x0+0.1||ex[0]>sub.x1-0.1) continue;
      if(ex[0]<r.x0+0.3||ex[0]>r.x1-0.3) continue;
      put(out,ctx,{x0:ex[0]-0.24,x1:ex[0]+0.24,y0:cy_(tb.cy)-0.30,y1:cy_(tb.cy)+0.30}, z, 'chair',
        {wood:K.wood, variant:1, fabric:K.fabric},{wall:'free', pad:0, face:ex[1], room:r.id, soft:true});
      A(ctx,'dine',r.id, ex[0], tb.cy, z+0.46, {seat:'head'});
    }
    return tb;
  }
  function furnishRoom(out, ctx, r, b, z){
    const K=b.T.kit, bar=b.eb.tier==='baronial';
    const fp=hearth(out,ctx,r,b,z);
    // the wall opposite the fire is where the seating goes, so a placed piece faces the fire
    const facing = r.fire ? OPPWALL[r.fire] : 'rear';
    switch(r.kind){
      case 'vestibule': {
        putAny(out,ctx,r,z,'hallStand',{wood:K.wood},['left','right'],{along:0.22, tall:true, room:r.id});
        const bn=putAny(out,ctx,r,z,'bench',{len:0.2, wood:K.wood},['right','left'],{along:0.72, room:r.id});
        if(bn) A(ctx,'sit',r.id, bn.cx, bn.y1+0.34, z+0.45, {seat:'hall'});
        putAny(out,ctx,r,z,'longClock',{wood:'walnut'},['left','right','rear'],{along:0.94, tall:true, room:r.id});
        put(out,ctx,r,z,'rug',{len:0.3, fabric:K.fabric, fabric2:K.fabric2},{wall:'free', fx:0.5, fy:0.4, ignoreClear:true});
        A(ctx,'entry',r.id, 0, r.y1-0.75, z);
        break;
      }
      case 'stairhall': {
        // the well and the flight own this room: a console and a clock against the spine wall,
        // nothing in the middle, because the middle is the route
        const cs=putAny(out,ctx,r,z,'table',{len:0.2, wood:K.wood},['right','front','rear'],{along:0.72, room:r.id});
        if(cs) A(ctx,'store',r.id, cs.cx, cs.cy + (cs.face==='N'?-0.42:0.42), z, {what:'console'});
        putAny(out,ctx,r,z,'longClock',{wood:'walnut'},['right','rear','front'],{along:0.12, tall:true, room:r.id});
        const ac=putAny(out,ctx,r,z,'armchair',{fabric:K.fabric, fabric2:K.fabric2, wood:'walnut'},
          ['left','rear'],{along:0.5, room:r.id});
        if(ac) A(ctx,'sit',r.id, ac.cx, ac.cy, z+0.42, {seat:'hall'});
        if(r.carriage) A(ctx,'entry',r.id, r.x0+0.9, (r.y0+r.y1)/2, z, {kind:'carriage'});
        put(out,ctx,r,z,'rug',{len:0.8, fabric:K.fabric, fabric2:K.fabric2},{wall:'free', fx:0.72, fy:0.5, ignoreClear:true});
        break;
      }
      case 'drawing': {
        const st=putAny(out,ctx,r,z,K.seat,{len:0.5, fabric:K.fabric, fabric2:K.fabric2, wood:K.wood},
          [facing,'rear','front','left','right'],{along:0.5, inset:0.10, room:r.id});
        if(st){ const n=Math.max(2,Math.round(Math.max(st.x1-st.x0,st.y1-st.y0)/0.74));
          const horiz=(st.x1-st.x0)>(st.y1-st.y0);
          for(let i=0;i<n;i++) A(ctx,'sit',r.id,
            horiz? st.x0+(st.x1-st.x0)*(i+0.5)/n : (st.face==='E'?st.x1+0.36:st.x0-0.36),
            horiz? (st.face==='S'?st.y1+0.36:st.y0-0.36) : st.y0+(st.y1-st.y0)*(i+0.5)/n, z+0.44, {seat:i}); }
        for(const wl of [r.fire==='left'?'front':'left', r.fire==='right'?'rear':'right']){
          const ac=put(out,ctx,r,z,'armchair',{fabric:K.fabric2, fabric2:K.fabric, wood:'walnut'},
            {wall:wl, along:wl==='front'||wl==='rear'?0.24:0.78, room:r.id});
          if(ac) A(ctx,'sit',r.id, ac.cx, ac.cy - (ac.face==='N'?0.42:-0.42), z+0.42, {seat:'armchair'});
        }
        const pn=putAny(out,ctx,r,z,'piano',{paint: bar?null:'plum', wood:K.wood, fabric:K.fabric2},
          [facing==='rear'?'front':'rear','left','right'],{along:0.14, tall:true, room:r.id});
        if(pn) A(ctx,'music',r.id, pn.cx, pn.cy + (pn.face==='N'?-0.55:0.55), z+0.46);
        put(out,ctx,r,z,'roundTable',{wood:K.wood},{wall:'free', fx:0.5, fy:0.5, room:r.id});
        put(out,ctx,r,z,'rug',{len:0.7, fabric:K.fabric, fabric2:K.fabric2},{wall:'free', fx:0.5, fy:0.5, ignoreClear:true});
        break;
      }
      case 'dining': {
        const tb=diningSet(out,ctx,r,b,z,{x0:r.x0+0.5,x1:r.x1-0.5,y0:r.y0+0.55,y1:r.y1-0.55}, r.area>26?1.2:0.7);
        putAny(out,ctx,r,z,'sideboard',{len:0.3, paint:K.paint, wood:K.wood, worktop:K.worktop},
          [facing,'rear','front','left','right'],{along:0.5, tall:true, room:r.id});
        putAny(out,ctx,r,z,'hutch',{paint:K.paint, wood:'pine', worktop:'butcher'},['rear','front','left','right'],{along:0.06, tall:true, room:r.id});
        if(tb) put(out,ctx,r,z,'rug',{len:0.9, fabric:K.fabric2, fabric2:K.fabric},{wall:'free', fx:0.5, fy:0.5, ignoreClear:true});
        break;
      }
      case 'library': case 'study': case 'schoolroom': {
        // the desk goes in first: it wants the window wall, and its chair needs the room behind it
        const dk=putAny(out,ctx,r,z,'writingDesk',{variant:K.desk, paint:K.paint, wood:K.wood, fabric:K.fabric},
          ['front','rear','right','left'],{along:0.5, inset:0.62, room:r.id});
        if(dk){
          const back = dk.face==='N' ? {y:dk.y1+0.44, f:'S'} : dk.face==='S' ? {y:dk.y0-0.44, f:'N'} : null;
          const cxp = clamp(back?dk.cx:(dk.face==='E'?dk.x1+0.44:dk.x0-0.44), r.x0+0.32, r.x1-0.32);
          const cyp = clamp(back?back.y:dk.cy, r.y0+0.30, r.y1-0.30);
          put(out,ctx,{x0:cxp-0.26,x1:cxp+0.26,y0:cyp-0.24,y1:cyp+0.24}, z, 'chair',
            {wood:K.wood, variant:1, fabric:K.fabric},{wall:'free', pad:0, face: back?back.f:(dk.face==='E'?'W':'E'), room:r.id, soft:true});
          A(ctx,'desk',r.id, cxp, cyp, z+0.46);
        }
        const walls = r.kind==='library' ? [facing,'rear','front','left','right'] : ['rear','left','right','front'];
        putAny(out,ctx,r,z,'bookcase',{len:1.0, variant: r.kind==='library'?1:0, wood:K.wood, fabric:K.fabric, fabric2:K.fabric2},
          walls,{along:0.18, tall:true, room:r.id});
        putAny(out,ctx,r,z,'bookcase',{len:0.6, variant:0, wood:K.wood, fabric:K.fabric2, fabric2:K.fabric},
          walls.slice(1),{along:0.82, tall:true, room:r.id});
        if(r.area>15){ const ac=put(out,ctx,r,z,'armchair',{fabric:K.fabric, fabric2:K.fabric2, wood:'walnut'},
            {wall: r.fire==='left'?'right':'left', along:0.5, room:r.id});
          if(ac) A(ctx,'read',r.id, ac.cx, ac.cy, z+0.42); }
        put(out,ctx,r,z,'rug',{len:0.4, fabric:K.fabric, fabric2:K.fabric2},{wall:'free', fx:0.5, fy:0.5, ignoreClear:true});
        break;
      }
      case 'billiard': {
        const tb=put(out,ctx,r,z,'table',{len:1.0, wood:K.wood},{wall:'free', fx:0.5, fy:0.5, room:r.id});
        if(tb) A(ctx,'sit',r.id, tb.cx, tb.y0-0.5, z+0.42, {at:'billiard'});
        putAny(out,ctx,r,z,'bench',{len:0.5, wood:K.wood},['left','right','rear'],{along:0.5, room:r.id});
        putAny(out,ctx,r,z,'shelf',{len:0.2, wood:'pine'},['rear','front'],{along:0.12, tall:true, room:r.id});
        putAny(out,ctx,r,z,'armchair',{fabric:K.fabric, fabric2:K.fabric2, wood:'walnut'},['front','rear'],{along:0.85, room:r.id});
        break;
      }
      case 'morning': {
        const tb=put(out,ctx,r,z,'roundTable',{wood:K.wood},{wall:'free', fx:0.5, fy:0.45, room:r.id});
        if(tb) for(const [dx,dy,f] of [[-0.86,0,'E'],[0.86,0,'W'],[0,-0.86,'S'],[0,0.86,'N']]){
          const cxp=tb.cx+dx, cyp=tb.cy+dy;
          if(cxp<r.x0+0.4||cxp>r.x1-0.4||cyp<r.y0+0.4||cyp>r.y1-0.4) continue;
          put(out,ctx,{x0:cxp-0.24,x1:cxp+0.24,y0:cyp-0.24,y1:cyp+0.24}, z, 'chair',
            {wood:K.wood, variant:1, fabric:K.fabric2},{wall:'free', pad:0, face:f, room:r.id, soft:true});
          A(ctx,'dine',r.id, cxp, cyp, z+0.46);
        }
        put(out,ctx,r,z,'rug',{len:0.3, fabric:K.fabric2, fabric2:K.fabric},{wall:'free', fx:0.5, fy:0.45, ignoreClear:true});
        break;
      }
      case 'kitchen': {
        const ck=put(out,ctx,r,z,K.cook,{wood:'oak'},{wall:'rear', along:0.86, inset:0.04, room:r.id});
        if(ck) A(ctx,'cook',r.id, ck.cx, ck.y1+0.48, z);
        const ct=putAny(out,ctx,r,z,'counter',{len:0.35, paint:K.paint, wood:'pine', worktop:K.worktop},
          ['rear','front','left','right'],{along:0.0, inset:0.04, room:r.id});
        if(ct||ck) decalY(out, r.y0, 1, r.x0+0.14, r.x1-0.14, z+0.92, z+1.48, 'wallTile', 0.22, subwayTex(), false, 0.05);
        const sk=putAny(out,ctx,r,z,K.sink,{paint:K.paint, wood:'pine', worktop:K.worktop},['left','right'],{along:0.22, inset:0.04, room:r.id});
        if(sk) A(ctx,'wash_dishes',r.id, sk.face==='E'?sk.x1+0.46:sk.x0-0.46, sk.cy, z);
        const cd=putAny(out,ctx,r,z,K.cold,{paint:'white', wood:'oak'},['right','left','front'],{along:0.16, tall:true, room:r.id});
        if(cd) A(ctx,'store',r.id, cd.cx, cd.cy, z, {what:K.cold});
        putAny(out,ctx,r,z,'cupboard',{paint:K.paint, wood:'pine'},['front','right','left'],{along:0.84, tall:true, room:r.id});
        diningSet(out,ctx,r,b,z,{x0:r.x0+0.6,x1:r.x1-0.6,y0:r.y0+1.35,y1:r.y1-0.5}, 0.3, true);
        break;
      }
      case 'scullery': {
        const sk=put(out,ctx,r,z,'sink',{paint:K.paint, wood:'pine', worktop:'slate'},{wall:'rear', along:0.14, inset:0.04, room:r.id});
        if(sk) A(ctx,'wash_dishes',r.id, sk.cx, sk.y1+0.46, z);
        const wt=putAny(out,ctx,r,z,'washtub',{wood:'oak'},['rear','left','right'],{along:0.72, room:r.id});
        if(wt) A(ctx,'laundry',r.id, wt.cx, wt.y1+0.42, z);
        putAny(out,ctx,r,z,'shelf',{len:0.3, wood:'pine'},['left','right','front'],{along:0.5, tall:true, room:r.id});
        decalY(out, r.y0, 1, r.x0+0.06, r.x1-0.06, z+0.02, z+1.34, 'wallTile', 0.18, subwayTex(), false, 0.04);
        break;
      }
      case 'pantry': case 'linen': {
        putAny(out,ctx,r,z,'shelf',{len:0.4, wood:'pine'},['rear','left','right'],{along:0.5, tall:true, room:r.id});
        putAny(out,ctx,r,z,'cupboard',{paint:K.paint, wood:'pine'},['front','right','left'],{along:0.5, tall:true, room:r.id});
        if(r.kind==='pantry') putAny(out,ctx,r,z,'crate',{wood:'pine'},['left','right'],{along:0.88, room:r.id});
        A(ctx,'store',r.id,(r.x0+r.x1)/2,(r.y0+r.y1)/2,z,{what:r.kind});
        break;
      }
      case 'bed': case 'nursery': case 'servant': {
        const grand = r.principal && r.kind==='bed';
        const name = grand ? K.bed : 'bed';
        const variant = grand ? 0 : (r.kind==='servant'||r.area<11 ? 1 : 0);
        // a headboard wants a blank wall, so the glazed elevations are tried last
        const solid=['rear','left','right','front'].filter(w=>!r.win||!r.win[w]);
        const glazed=['rear','left','right','front'].filter(w=>!solid.includes(w));
        const mark=ctx.rejects.length;
        const bd=putAny(out,ctx,r,z,name,{variant, wood:K.wood, fabric:K.fabric, fabric2:K.fabric2},
          solid.concat(glazed),{along:0.5, inset:0.14, room:r.id})
          // a small room takes a single rather than going without a bed
          || (variant!==1 ? putAny(out,ctx,r,z,'bed',{variant:1, wood:K.wood, fabric:K.fabric, fabric2:K.fabric2},
               solid.concat(glazed),{along:0.5, inset:0.12, margin:0.10, room:r.id}) : null);
        if(bd) ctx.rejects.length=mark;
        if(bd){
          const alongY=(bd.y1-bd.y0)>(bd.x1-bd.x0);
          for(const sd of (variant===1?['a']:['a','b'])){
            const s = sd==='a'?-1:1;
            const ax = alongY ? clamp(bd.cx+s*((bd.x1-bd.x0)/2+0.38), r.x0+0.3, r.x1-0.3) : bd.cx;
            const ay = alongY ? bd.cy : clamp(bd.cy+s*((bd.y1-bd.y0)/2+0.38), r.y0+0.3, r.y1-0.3);
            A(ctx,'sleep',r.id, ax, ay, z, {side:sd, size: variant===1?'single':'double'});
          }
        }
        if(r.kind==='servant'){
          putAny(out,ctx,r,z,'seaChest',{wood:'pine'},['rear','front','left','right'],{along:0.86, room:r.id});
          putAny(out,ctx,r,z,'washstand',{wood:'pine'},['left','right','front'],{along:0.14, room:r.id});
          break;
        }
        const wd=putAny(out,ctx,r,z,'wardrobe',{paint:K.paint, wood:'pine'},['right','left','front','rear'],{along:0.14, tall:true, room:r.id});
        if(wd) A(ctx,'store',r.id, wd.cx, wd.cy, z, {what:'wardrobe'});
        putAny(out,ctx,r,z,'dresser',{paint:K.paint, wood:'pine'},['left','front','right','rear'],{along:0.82, tall:true, room:r.id});
        const ws=putAny(out,ctx,r,z,'washstand',{wood:K.wood},['front','left','right'],{along:0.5, room:r.id});
        if(ws) A(ctx,'vanity',r.id, ws.cx, ws.cy + (ws.face==='N'?-0.42:0.42), z);
        if(r.kind==='nursery'){
          const cot=putAny(out,ctx,r,z,'bed',{variant:1, wood:'pine', fabric:K.fabric2, fabric2:'cream'},['left','right','rear'],{along:0.86, room:r.id, soft:true});
          if(cot) A(ctx,'sleep',r.id, cot.cx, cot.cy, z, {side:'cot', size:'single'});
          putAny(out,ctx,r,z,'shelf',{len:0.1, wood:'pine'},['front','rear'],{along:0.5, tall:true, room:r.id});
        } else if(r.principal){
          const ac=putAny(out,ctx,r,z,'armchair',{fabric:K.fabric2, fabric2:K.fabric, wood:'walnut'},['front','rear','left','right'],{along:0.5, room:r.id});
          if(ac) A(ctx,'read',r.id, ac.cx, ac.cy, z+0.42);
        }
        put(out,ctx,r,z,'rug',{len:0.3, fabric:K.fabric2, fabric2:K.fabric},{wall:'free', fx:0.5, fy:0.5, ignoreClear:true});
        break;
      }
      case 'dressing': {
        putAny(out,ctx,r,z,'wardrobe',{paint:K.paint, wood:'pine'},['rear','front','left','right'],{along:0.2, tall:true, room:r.id});
        putAny(out,ctx,r,z,'wardrobe',{paint:K.paint, wood:'pine'},['rear','front','left','right'],{along:0.8, tall:true, room:r.id});
        putAny(out,ctx,r,z,'mirror',{wood:K.wood},['left','right','front'],{along:0.5, room:r.id});
        putAny(out,ctx,r,z,'seaChest',{wood:K.wood},['left','right'],{along:0.5, room:r.id});
        A(ctx,'store',r.id,(r.x0+r.x1)/2,(r.y0+r.y1)/2,z,{what:'dressing'});
        break;
      }
      case 'bath': {
        const tb=putAny(out,ctx,r,z,'tub',{variant:K.tub},['right','left','front','rear'],{along:0.5, inset:0.06, room:r.id});
        if(tb) A(ctx,'bathe',r.id, tb.face==='W'?tb.x0-0.46:tb.x1+0.46, tb.cy, z, {fixture:'tub'});
        const ws=putAny(out,ctx,r,z,'washstand',{wood:K.wood},['rear','front','left'],{along:0.14, room:r.id});
        if(ws) A(ctx,'vanity',r.id, ws.cx, ws.cy + (ws.face==='N'?-0.42:0.42), z);
        putAny(out,ctx,r,z,'towelRail',{wood:'pine', fabric:'white'},['left','right','rear'],{along:0.82, room:r.id});
        putAny(out,ctx,r,z,'mirror',{wood:K.wood},['front','rear','left','right'],{along:0.5, room:r.id});
        decalY(out, r.y0, 1, r.x0+0.06, r.x1-0.06, z+0.02, z+1.28, 'wallTile', 0.18, subwayTex(), false, 0.04);
        break;
      }
      case 'wc': {
        const wc=putAny(out,ctx,r,z,'toilet',{variant:K.wc},['rear','left','right'],{along:0.5, room:r.id});
        if(wc) A(ctx,'toilet',r.id, wc.cx, wc.cy + (wc.face==='N'?-0.44:0.44), z);
        putAny(out,ctx,r,z,'washstand',{wood:'pine'},['front','left','right'],{along:0.5, room:r.id});
        break;
      }
      case 'boxroom': case 'store': {
        putAny(out,ctx,r,z,'shelf',{len:0.4, wood:'pine'},['left','right','rear'],{along:0.5, tall:true, room:r.id});
        putAny(out,ctx,r,z,'seaChest',{wood:'pine'},['rear','front'],{along:0.18, room:r.id});
        putAny(out,ctx,r,z,'crate',{wood:'pine'},['rear','front'],{along:0.62, room:r.id});
        putAny(out,ctx,r,z,'barrel',{wood:'oak'},['front','rear'],{along:0.88, room:r.id});
        A(ctx,'store',r.id,(r.x0+r.x1)/2,(r.y0+r.y1)/2,z,{what:r.kind});
        break;
      }
      case 'sideHall': {
        const bn=putAny(out,ctx,r,z,'bench',{len:0.4, wood:K.wood},['rear','front'],{along:0.5, room:r.id});
        if(bn) A(ctx,'sit',r.id, bn.cx, bn.cy + (bn.face==='N'?-0.34:0.34), z+0.45, {seat:'carriage'});
        putAny(out,ctx,r,z,'hallStand',{wood:K.wood},['left','right'],{along:0.2, tall:true, room:r.id});
        putAny(out,ctx,r,z,'lockers',{},['left','right','rear'],{along:0.8, tall:true, room:r.id});
        A(ctx,'entry',r.id,(r.x0+r.x1)/2, r.y0+0.8, z, {kind:'carriage'});
        break;
      }
      case 'landing': {
        // a gallery round the well is a room you pass through, so it takes the wall and nothing else
        if(r.gallery && r.area>16){
          const st=putAny(out,ctx,r,z,'settee',{len:0.0, fabric:K.fabric2, fabric2:K.fabric, wood:K.wood},
            ['left','rear','front'],{along:0.5, room:r.id});
          if(st) A(ctx,'sit',r.id, st.cx, st.cy + (st.face==='N'?-0.4:0.4), z+0.44, {seat:'gallery'});
          putAny(out,ctx,r,z,'bookcase',{len:0.4, variant:0, wood:K.wood, fabric:K.fabric, fabric2:K.fabric2},
            ['rear','left','front'],{along:0.12, tall:true, room:r.id});
        } else if(r.upper){
          putAny(out,ctx,r,z,'cupboard',{paint:K.paint, wood:'pine'},['left','right'],{along:0.5, tall:true, room:r.id});
        }
        break;
      }
      case 'passage': case 'turret': case 'towerRoom': default: break;
    }
  }

  // ---- stairs, wells, plates -----------------------------------------------------------------
  // One builder for both flights, either axis. The bottom tread is at the HIGH end of the run axis
  // and it climbs toward the low end, which is where the void above it sits.
  function fStair(out, ctx, st, z, riseTotal){
    const n=st.steps, rise=riseTotal/n, alongX=st.axis==='x';
    const A1 = alongX? st.x1 : st.y1, A0 = alongX? st.x0 : st.y0;
    const run=(A1-A0)/n;
    const c0 = alongX? st.y0 : st.x0, c1 = alongX? st.y1 : st.x1;
    const mk=(a0,a1,b0,b1)=> alongX ? {x0:a0,x1:a1,y0:b0,y1:b1} : {x0:b0,x1:b1,y0:a0,y1:a1};
    for(let i=0;i<n;i++){
      const a1=A1-i*run, a0=a1-run, zz=z+rise*(i+1);
      P(out,ctx,Object.assign(mk(a0,a1,c0,c1),
        { z0:Math.max(z, zz-rise-0.02), z1:zz, mat:'stone', b:0.05, topB:0.4, blocker:false }));
      // a stair runner held off the string either side — the one strip of colour indoors
      const rb=mk(a0+0.012, a1-0.010, c0+0.17, c1-0.17);
      decalTop(out, rb.x0,rb.x1, rb.y0,rb.y1, zz+0.006, 'runner', 0.22, 0.05);
    }
    ctx.blockers.push({kind:'stair', level:ctx.level, sub:st.kind,
      x0:r3(st.x0),x1:r3(st.x1),y0:r3(st.y0),y1:r3(st.y1),h:r3(riseTotal)});
    // balusters every second tread with one continuous raked handrail over them, on the OPEN side
    const outer = st.railSide==='low' ? c0-0.05 : c1+0.05, RH=0.94;
    for(let i=0;i<n;i+=2){ const a=A1-i*run, zz=z+rise*(i+1);
      P(out,ctx,Object.assign(mk(a-0.055, a+0.02, outer-0.03, outer+0.03),
        { z0:Math.max(z,zz-rise), z1:zz+RH, mat:'metal', blocker:false })); }
    const zBot=z+rise+RH, zTop=z+rise*n+RH;
    const pt=(a,c,zz)=> alongX? [a,c,zz] : [c,a,zz];
    quad(out, pt(A1,outer-0.04,zBot), pt(A1,outer+0.04,zBot), pt(A0,outer+0.04,zTop), pt(A0,outer-0.04,zTop), 'join', 0.55);
    quad(out, pt(A1,outer+0.04,zBot-0.06), pt(A0,outer+0.04,zTop-0.06), pt(A0,outer+0.04,zTop), pt(A1,outer+0.04,zBot), 'join', 0.1);
    quad(out, pt(A0,outer-0.04,zTop), pt(A0,outer-0.04,zTop-0.06), pt(A1,outer-0.04,zBot-0.06), pt(A1,outer-0.04,zBot), 'join', -0.25);
    // the newel at the foot, the one piece of joinery you actually take hold of
    P(out,ctx,Object.assign(mk(A1-0.04, A1+0.15, outer-0.09, outer+0.09),
      { z0:z, z1:z+1.08, mat:'join', b:0.3, blocker:false }));
    const cm=(c0+c1)/2;
    A(ctx,'stair', st.id, alongX?A1+0.50:cm, alongX?cm:A1+0.50, z, {end:'bottom', flight:st.kind,fromLevel:ctx.level,toLevel:ctx.level+1});
    A(ctx,'stair', st.id, alongX?A0-0.50:cm, alongX?cm:A0-0.50, z+riseTotal, {end:'top', flight:st.kind,level:ctx.level+1,fromLevel:ctx.level,toLevel:ctx.level+1});
  }
  // THE AREA ABOVE THE STAIR: the plate is cut away over the flight and the open well is ringed
  // with a balustrade on three sides — the fourth is where the flight arrives and you step off.
  function wellRail(out, ctx, v, z){
    const RH=0.96, T=0.055, open=v.open||'x0';
    const bal=(x0,x1,y0,y1)=>{
      P(out,ctx,{x0,x1,y0,y1, z0:z, z1:z+0.09, mat:'join', b:0.35, blocker:false});
      P(out,ctx,{x0,x1,y0,y1, z0:z+RH-0.09, z1:z+RH, mat:'join', b:0.6, blocker:false});
      const along=(x1-x0)>(y1-y0), len=along?x1-x0:y1-y0, n=Math.max(2,Math.round(len/0.19));
      for(let i=0;i<n;i++){ const t=(i+0.5)/n;
        P(out,ctx,{ x0: along?x0+(x1-x0)*t-T/2:x0, x1: along?x0+(x1-x0)*t+T/2:x1,
          y0: along?y0:y0+(y1-y0)*t-T/2, y1: along?y1:y0+(y1-y0)*t+T/2,
          z0:z+0.09, z1:z+RH-0.09, mat:'metal', b:0.15, blocker:false }); }
    };
    if(open!=='x0') bal(v.x0-0.06, v.x0+0.02, v.y0-0.02, v.y1+0.02);
    if(open!=='x1') bal(v.x1-0.02, v.x1+0.06, v.y0-0.02, v.y1+0.02);
    if(open!=='y0') bal(v.x0-0.06, v.x1+0.06, v.y0-0.06, v.y0+0.02);
    if(open!=='y1') bal(v.x0-0.06, v.x1+0.06, v.y1-0.02, v.y1+0.06);
    ctx.blockers.push({kind:'well', level:ctx.level, x0:r3(v.x0),x1:r3(v.x1),y0:r3(v.y0),y1:r3(v.y1),h:0});
  }
  function slabMinus(out, x0,x1,y0,y1, z, mat, bias, tex, voids){
    let rects=[[x0,x1,y0,y1]];
    for(const v of (voids||[])){
      if(!v) continue;
      const next=[];
      for(const q of rects){
        const a0=q[0],a1=q[1],b0=q[2],b1=q[3];
        if(v.x1<=a0+0.001||v.x0>=a1-0.001||v.y1<=b0+0.001||v.y0>=b1-0.001){ next.push(q); continue; }
        const cx0=Math.max(a0,v.x0), cx1=Math.min(a1,v.x1), cy0=Math.max(b0,v.y0), cy1=Math.min(b1,v.y1);
        if(b0<cy0-0.001) next.push([a0,a1,b0,cy0]);
        if(cy1<b1-0.001) next.push([a0,a1,cy1,b1]);
        if(a0<cx0-0.001) next.push([a0,cx0,cy0,cy1]);
        if(cx1<a1-0.001) next.push([cx1,a1,cy0,cy1]);
      }
      rects=next;
    }
    for(const q of rects) if(q[1]-q[0]>0.02 && q[3]-q[2]>0.02)
      slab(out, [[q[0],q[2]],[q[1],q[2]],[q[1],q[3]],[q[0],q[3]]], z, mat, bias, tex);
  }
  function segsOf(a0,a1,gaps){
    let list=[[a0,a1]];
    for(const g of (gaps||[])){
      const next=[];
      for(const [s0,s1] of list){
        if(g[1]<=s0||g[0]>=s1){ next.push([s0,s1]); continue; }
        if(g[0]>s0+0.01) next.push([s0,g[0]]);
        if(g[1]<s1-0.01) next.push([g[1],s1]);
      }
      list=next;
    }
    return list;
  }
  function drawPartition(out, ctx, p, z0, z1, t, mat, tex){
    for(const [s0,s1] of segsOf(p.a0,p.a1,p.gaps)){
      if(s1-s0<0.02) continue;
      if(p.axis==='x'){ boxSolid(out, s0,s1, p.plane-t/2,p.plane+t/2, z0,z1, mat, tex, 0.40, 0.45);
        ctx.blockers.push({kind:'wall', level:ctx.level, x0:r3(s0),x1:r3(s1), y0:r3(p.plane-t/2), y1:r3(p.plane+t/2), h:r3(z1-z0)}); }
      else { boxSolid(out, p.plane-t/2,p.plane+t/2, s0,s1, z0,z1, mat, tex, 0.40, 0.45);
        ctx.blockers.push({kind:'wall', level:ctx.level, x0:r3(p.plane-t/2),x1:r3(p.plane+t/2), y0:r3(s0),y1:r3(s1), h:r3(z1-z0)}); }
    }
  }
  function drawLeaf(out, d, z0, hLeaf, mat, t){
    // open 180°, flat against the wall beside its opening — a leaf swung into the room reads as a
    // stray half-wall and fouls whatever stands against that wall
    const th=0.045, lw=d.clearW*0.92, off=(t||0.12)/2+th/2+0.004;
    if(d.axis==='x'){ const e=d.c-d.clearW/2;
      boxSolid(out, e-lw, e, d.plane+off-th/2, d.plane+off+th/2, z0, z0+hLeaf, mat, null, 0.15, 0.4); }
    else { const e=d.c-d.clearW/2;
      boxSolid(out, d.plane+off-th/2, d.plane+off+th/2, e-lw, e, z0, z0+hLeaf, mat, null, 0.15, 0.4); }
  }

  // ---- build ---------------------------------------------------------------------------------
  function build(b){
    const out=[], ctx={ blockers:[], anchors:[], openings:[], flues:[], mats:{},
      clear:[], clearTall:[], taken:[], rejects:[], clearAll:[], voidsAll:[], pn:0,
      weather:b.weather, night:b.night, level:0 };
    const B=camBasis({dir:b.dir, elev:b.elev});
    const L=b.L, T=b.T, sh=b.sh, eb=b.eb;
    const wallT=sh.wallT, partT=0.14, floorT=0.30;
    const soften=tex=>(u,v)=>tex(u,v)*(1-b.surfaceCalm*0.70);
    const boardT=soften(boardTex(0.17)), parqT=soften(parquetTex()), tileT=soften(tileTex(0.34)), flagT=soften(flagTex());
    const plasT=plasterTex(), panT=panelTex();
    const hallTex = T.hallFloor==='flagStone' ? flagT : tileT;
    const fieldTex = T.floor==='parquet' ? parqT : boardT;

    for(const li of b.levelList){
      const LV=L.levels[li]; if(!LV) continue;
      ctx.level=li;
      const zF=LV.floorZ + li*b.explode, zC=zF+LV.ceil;
      const rooms=L.rooms.filter(r=>r.level===li);
      const bx=LV.box;
      const voids=LV.voids;
      const isTop = li===b.levelList[b.levelList.length-1];

      // ---- where the accent and the bay PIERCE the outer wall. The wall is drawn in segments
      // round these, an arch is turned over them, and they are the only places the keep-clear band
      // along the masonry is absent — which is how an alcove gets furniture and a room does not get
      // a table buried in the front wall.
      const pierce=[];
      const pAdd=(axis,plane,a0,a1)=>pierce.push({axis, plane:r3(plane), a0:r3(a0), a1:r3(a1)});
      if(LV.alcove){ const a=LV.alcove;
        if(a.side==='+X') pAdd('y', bx.x1, a.y0, a.y1); else pAdd('x', bx.y1, a.x0, a.x1); }
      if(LV.accent && LV.accent.kind==='tower' && li>0){ const a=LV.accent;
        const hwid=Math.max(0.9, Math.min(1.45,(a.x1-a.x0)/2-0.15)); pAdd('x', bx.y1, -hwid, hwid); }
      if(LV.accent && LV.accent.kind==='turret'){ const a=LV.accent;   // the drum takes the corner
        pAdd('x', bx.y1, a.cx-a.r, bx.x1+wallT);
        pAdd('y', bx.x1, a.cy-a.r, bx.y1+wallT); }
      const pierceOn=(p)=>pierce.filter(q=>q.axis===p.axis && Math.abs(q.plane-p.plane)<0.01).map(q=>[q.a0,q.a1]);

      // ---- keep-clear: the outer walls, the partitions, doorways, both flights and both wells,
      // all before the first prop
      ctx.clear.length=0; ctx.clearTall.length=0; ctx.taken.length=0;
      ctx.fireBudget = (eb.chimneys||0)*2;   // one stack carries two flues on a level, back to back
      for(const v of voids){ ctx.clear.push(Object.assign({tag:'the stairwell'}, v));
        ctx.voidsAll.push(Object.assign({level:li}, v)); }
      for(const st of LV.stairs) ctx.clear.push({ tag:'the '+(st.kind==='main'?'main flight':'service flight'),
        x0:st.x0-0.08, x1:st.x1+0.08, y0:st.y0-0.08, y1:st.y1+0.30 });
      for(const st of LV.stairs)ctx.clear.push(Object.assign({tag:'bottom landing'},st.bottomLanding));
      for(const v of voids)ctx.clear.push(Object.assign({tag:'top landing'},v.topLanding));
      for(const p of L.parts){ if(p.level!==li) continue;
        const t=partT/2+0.01;
        if(p.axis==='x') ctx.clear.push({ tag:'a wall', x0:p.a0, x1:p.a1, y0:p.plane-t, y1:p.plane+t });
        else ctx.clear.push({ tag:'a wall', x0:p.plane-t, x1:p.plane+t, y0:p.a0, y1:p.a1 });
      }
      for(const d of L.doors){ if(d.level!==li) continue;
        const half=d.clearW/2+0.08, dep=0.52;
        if(d.axis==='x') ctx.clear.push({ tag:'a doorway', x0:d.c-half, x1:d.c+half, y0:d.plane-dep, y1:d.plane+dep });
        else ctx.clear.push({ tag:'a doorway', x0:d.plane-dep, x1:d.plane+dep, y0:d.c-half, y1:d.c+half });
      }
      for(const p of [{axis:'x',plane:bx.y1,out:1},{axis:'x',plane:bx.y0,out:-1},
                      {axis:'y',plane:bx.x1,out:1},{axis:'y',plane:bx.x0,out:-1}]){
        const lo=(p.axis==='x'?bx.x0:bx.y0)-wallT, hi=(p.axis==='x'?bx.x1:bx.y1)+wallT;
        const c0=p.out>0?p.plane:p.plane-wallT, c1=p.out>0?p.plane+wallT:p.plane;
        for(const s of segsOf(lo,hi,pierceOn(p))){
          if(s[1]-s[0]<0.02) continue;
          ctx.clear.push(p.axis==='x' ? { tag:'the outer wall', x0:s[0], x1:s[1], y0:c0, y1:c1 }
                                      : { tag:'the outer wall', x0:c0, x1:c1, y0:s[0], y1:s[1] });
        }
      }
      for(const c of ctx.clear) ctx.clearAll.push(Object.assign({level:li}, c));

      // ---- the plate: a joist band round the edge, then one slab per room so the wet rooms tile
      wallQ(out, bx.x0-wallT, bx.y1+wallT, bx.x1+wallT, bx.y1+wallT, zF-floorT, zF-0.002, 'joist', null, -0.15);
      wallQ(out, bx.x1+wallT, bx.y0-wallT, bx.x0-wallT, bx.y0-wallT, zF-floorT, zF-0.002, 'joist', null, -0.15);
      wallQ(out, bx.x1+wallT, bx.y1+wallT, bx.x1+wallT, bx.y0-wallT, zF-floorT, zF-0.002, 'joist', null, -0.15);
      wallQ(out, bx.x0-wallT, bx.y0-wallT, bx.x0-wallT, bx.y1+wallT, zF-floorT, zF-0.002, 'joist', null, -0.15);
      for(const r of rooms){
        const wet = r.wet || r.kind==='bath' || r.kind==='wc';
        const hall = r.floor==='hall';
        slabMinus(out, r.x0,r.x1, r.y0,r.y1, zF, wet?'wet':(hall?'hallFloor':'floor'), 0.1,
          wet?tileT:(hall?hallTex:fieldTex), voids);
      }

      // ---- perimeter: far walls full height with the exterior's own openings, near walls cut to a lip
      const planesL=[
        { nx:0, ny:1,  axis:'x', plane:bx.y1, out:+1, facing:'+Y', wall:'front' },
        { nx:0, ny:-1, axis:'x', plane:bx.y0, out:-1, facing:'-Y', wall:'rear'  },
        { nx:1, ny:0,  axis:'y', plane:bx.x1, out:+1, facing:'+X', wall:'right' },
        { nx:-1,ny:0,  axis:'y', plane:bx.x0, out:-1, facing:'-X', wall:'left'  },
      ];
      const wainscot=T.wainscot;
      // The exterior's own openings, read once for ALL FOUR elevations BEFORE anything is drawn or
      // placed. Which wall of a room is glazed decides where a headboard may stand, and that must
      // not depend on which way the camera happens to face — otherwise the eight baked facings
      // would each show different furniture.
      const winsOn=(p)=>{
        const lo = p.axis==='x' ? bx.x0 : bx.y0, hi = p.axis==='x' ? bx.x1 : bx.y1;
        const out2=[];
        for(const o of (sh.openings||[])){
          if(o.facing!==p.facing) continue;
          if(!(o.z>=LV.floorZ-0.05 && o.z<LV.floorZ+LV.ceil-0.2)) continue;
          const a=(p.axis==='x'?o.x:o.y);
          if(a<lo+0.25||a>hi-0.25) continue;
          const host=rooms.find(r=> a>=(p.axis==='x'?r.x0:r.y0) && a<=(p.axis==='x'?r.x1:r.y1) &&
            Math.abs((p.out>0 ? (p.axis==='x'?r.y1:r.x1) : (p.axis==='x'?r.y0:r.x0)) - p.plane)<0.06);
          out2.push({ o, a, host, sill:o.z-LV.floorZ, ww:o.w+0.30, wh:o.h });
        }
        return out2;
      };
      const winMap=planesL.map(p=>winsOn(p));
      planesL.forEach((p,pi)=>{ for(const w of winMap[pi]){
        const host=w.host;
        if(w.o.kind==='door' && li===0){
          ctx.openings.push({kind:'entry', room:host?host.id:null, level:li,
            x:r3(w.o.x), y:r3(w.o.y), w:r3(w.o.w), h:2.45});
          continue;
        }
        ctx.openings.push({kind:'window', room:host?host.id:null, level:li, facing:p.facing,
          x:r3(w.o.x), y:r3(w.o.y), w:r3(w.o.w), h:r3(w.wh), sill:r3(w.sill)});
        if(!host) continue;
        host.win=host.win||{}; host.win[p.wall]=true;
        const dep=0.34;                                  // nothing tall in front of glass
        ctx.clearTall.push(p.axis==='x'
          ? { tag:'a window', x0:w.a-w.ww/2, x1:w.a+w.ww/2,
              y0: p.out>0?p.plane-dep:p.plane, y1: p.out>0?p.plane:p.plane+dep }
          : { tag:'a window', x0: p.out>0?p.plane-dep:p.plane, x1: p.out>0?p.plane:p.plane+dep,
              y0:w.a-w.ww/2, y1:w.a+w.ww/2 });
      } });
      planesL.forEach((p,pi)=>{
        const near=facesCamera(p.nx,p.ny,B);
        const z1 = near ? zF+CUT_NEAR : zC;
        const lo=(p.axis==='x'?bx.x0:bx.y0)-wallT, hi=(p.axis==='x'?bx.x1:bx.y1)+wallT;
        const c0 = p.out>0 ? p.plane : p.plane-wallT, c1 = p.out>0 ? p.plane+wallT : p.plane;
        const gaps=pierceOn(p);
        for(const s of segsOf(lo,hi,gaps)){
          if(s[1]-s[0]<0.02) continue;
          if(p.axis==='x'){ boxSolid(out, s[0],s[1], c0,c1, zF-floorT, z1, 'wall', plasT, 0.35, 0.3);
            ctx.blockers.push({kind:'wall', level:li, x0:r3(s[0]),x1:r3(s[1]),y0:r3(c0),y1:r3(c1),h:r3(LV.ceil)}); }
          else { boxSolid(out, c0,c1, s[0],s[1], zF-floorT, z1, 'wall', plasT, 0.35, 0.3);
            ctx.blockers.push({kind:'wall', level:li, x0:r3(c0),x1:r3(c1),y0:r3(s[0]),y1:r3(s[1]),h:r3(LV.ceil)}); }
        }
        // an arch turned over each pierce: the opening is real, and the wall above it still carries
        const HD=2.34;
        if(z1>HD+0.06) for(const g of gaps){
          if(p.axis==='x'){ boxSolid(out, g[0],g[1], c0,c1, zF+HD, z1, 'wall', plasT, 0.35, 0.3);
            for(const s of [g[0],g[1]]) boxSolid(out, s-0.055,s+0.055, c0,c1, zF, zF+HD, 'join', null, 0.5, 0.5); }
          else { boxSolid(out, c0,c1, g[0],g[1], zF+HD, z1, 'wall', plasT, 0.35, 0.3);
            for(const s of [g[0],g[1]]) boxSolid(out, c0,c1, s-0.055,s+0.055, zF, zF+HD, 'join', null, 0.5, 0.5); }
        }
        if(near) return;
        const nrm = p.out>0 ? -1 : 1;
        const dec = p.axis==='x' ? (a0,a1,z0,z1,mat,bi,tex,flat,db)=>decalY(out,p.plane,nrm,a0,a1,z0,z1,mat,bi,tex,flat,db)
                                 : (a0,a1,z0,z1,mat,bi,tex,flat,db)=>decalX(out,p.plane,nrm,a0,a1,z0,z1,mat,bi,tex,flat,db);
        const dlo = p.axis==='x' ? bx.x0 : bx.y0, dhi = p.axis==='x' ? bx.x1 : bx.y1;
        // dado / wainscot band along the whole far elevation, and a picture rail above it
        dec(dlo+0.02, dhi-0.02, zF+0.02, zF+wainscot, 'panel', 0.30, panT, false, 0.045);
        dec(dlo+0.02, dhi-0.02, zF+wainscot, zF+wainscot+0.07, 'join', 0.75, null, true, 0.05);
        if(T.picture) dec(dlo+0.02, dhi-0.02, zF+LV.ceil-0.42, zF+LV.ceil-0.35, 'join', 0.7, null, true, 0.05);
        for(const w of winMap[pi]){
          const a=w.a, host=w.host, sill=w.sill, ww=w.ww, wh=w.wh;
          if(w.o.kind==='door' && li===0){
            dec(a-ww/2, a+ww/2, zF, zF+2.45, 'join', 0.1, null, true, 0.06);
            dec(a-ww/2+0.09, a+ww/2-0.09, zF+0.14, zF+2.31, 'join', -0.5, null, true, 0.07);
            continue;
          }
          dec(a-ww/2-0.10, a+ww/2+0.10, zF+sill-0.10, zF+sill+wh+0.12, 'join', 0.55, null, true, 0.05);
          dec(a-ww/2, a+ww/2, zF+sill, zF+sill+wh, 'glass', b.night?0:0.9, null, true, 0.06);
          dec(a-ww/2-0.13, a+ww/2+0.13, zF+sill-0.16, zF+sill, 'join', 0.85, null, true, 0.05);
          // heavy curtains either side, on the picture rail — the one soft thing on a far wall
          if(host && !host.service && !host.wet && li<sh.storeys){
            for(const s of [-1,1]) dec(a+s*(ww/2+0.05), a+s*(ww/2+0.34), zF+sill-0.24, zF+sill+wh+0.30,
              'damask', s<0?0.2:-0.1, weaveTex(), false, 0.08);
          }
        }
      });

      // ---- partitions and door leaves
      for(const p of L.parts) if(p.level===li) drawPartition(out,ctx,p, zF, zF+CUT_PART, partT, 'wall', plasT);
      for(const d of L.doors){
        if(d.level!==li || d.kind==='open') continue;
        const inThis = rooms.some(r=>r.id===d.to) || rooms.some(r=>r.id===d.from);
        if(!inThis) continue;
        if(d.kind==='entry'||d.kind==='service') continue;
        drawLeaf(out,d, zF, CUT_PART-0.10, 'join', partT);
        // cased architrave, so a doorway reads as a doorway and not a gap in a wall
        const t=partT/2, aTop=CUT_PART-0.02;
        if(d.axis==='x'){
          for(const s of [-1,1]) boxSolid(out, d.c+s*(d.clearW/2), d.c+s*(d.clearW/2+0.075), d.plane-t-0.02, d.plane+t+0.02, zF, aTop+zF, 'join', null, 0.5, 0.5);
        } else {
          for(const s of [-1,1]) boxSolid(out, d.plane-t-0.02, d.plane+t+0.02, d.c+s*(d.clearW/2), d.c+s*(d.clearW/2+0.075), zF, aTop+zF, 'join', null, 0.5, 0.5);
        }
      }

      // ---- alcoves: the bay, the oriel, the tower and the turret are rooms, not elevations
      const alc=LV.alcove;
      if(alc){
        const host=rooms.find(r=>r.id===alc.host);
        slab(out, [[alc.x0,alc.y0],[alc.x1,alc.y0],[alc.x1,alc.y1],[alc.x0,alc.y1]], zF,
          host&&host.floor==='hall'?'hallFloor':'floor', 0.14, fieldTex);
        if(alc.side==='+X'){
          boxSolid(out, alc.x0, alc.x1, alc.y0-partT, alc.y0, zF, zC, 'wall', plasT, 0.35, 0.3);
          boxSolid(out, alc.x0, alc.x1, alc.y1, alc.y1+partT, zF, zC, 'wall', plasT, 0.35, 0.3);
          if(!facesCamera(1,0,B)){
            decalX(out, alc.x1, -1, alc.y0+0.14, alc.y1-0.14, zF+0.55, zF+2.55, 'glass', b.night?0:0.9, null, true, 0.06);
            decalX(out, alc.x1, -1, alc.y0+0.02, alc.y1-0.02, zF+0.34, zF+0.55, 'join', 0.85, null, true, 0.05);
          }
        } else {
          boxSolid(out, alc.x0-partT, alc.x0, alc.y0, alc.y1, zF, zC, 'wall', plasT, 0.35, 0.3);
          boxSolid(out, alc.x1, alc.x1+partT, alc.y0, alc.y1, zF, zC, 'wall', plasT, 0.35, 0.3);
          if(!facesCamera(0,1,B)){
            decalY(out, alc.y1, -1, alc.x0+0.14, alc.x1-0.14, zF+0.55, zF+2.45, 'glass', b.night?0:0.9, null, true, 0.06);
            decalY(out, alc.y1, -1, alc.x0+0.02, alc.x1-0.02, zF+0.34, zF+0.55, 'join', 0.85, null, true, 0.05);
          }
        }
        if(b.furnish){
          const seat={x0:alc.x0+0.06,x1:alc.x1-0.06,y0:alc.y0+0.06,y1:alc.y1-0.06};
          const wl = alc.side==='+X' ? 'right' : 'front';
          const bn=put(out,ctx,seat,zF,'bench',{len:0.5, wood:T.kit.wood},{wall:wl, along:0.5, inset:0.04, room:alc.host});
          if(bn) A(ctx,'sit', alc.host, bn.cx, bn.cy + (alc.side==='+X'?0:0.34), zF+0.45, {seat:'window'});
        }
        ctx.openings.push({kind:alc.kind, room:alc.host, level:li,
          x:r3((alc.x0+alc.x1)/2), y:r3((alc.y0+alc.y1)/2), w:r3(alc.x1-alc.x0), h:2.0});
      }
      const ac=LV.accent;
      if(ac && ac.kind==='turret'){
        const n=12, a0=Math.PI*0.02, a1=Math.PI*1.98;
        arcSlab(out, ac.cx, ac.cy, ac.r, a0,a1, n, zF, 'floor', 0.12, fieldTex);
        // only the facets that stand outside the shell box are wall; the rest is the arch into the room
        for(let i=0;i<n;i++){
          const t0=a0+(a1-a0)*(i/n), t1=a0+(a1-a0)*((i+1)/n), tm=(t0+t1)/2;
          const mx=ac.cx+Math.cos(tm)*ac.r, my=ac.cy+Math.sin(tm)*ac.r;
          if(mx<bx.x1-0.04 && my<bx.y1-0.04) continue;
          const p0=[ac.cx+Math.cos(t0)*ac.r, ac.cy+Math.sin(t0)*ac.r];
          const p1=[ac.cx+Math.cos(t1)*ac.r, ac.cy+Math.sin(t1)*ac.r];
          wallQ(out, p1[0],p1[1], p0[0],p0[1], zF, zC, 'wall', plasT, 0.3);
          if((mx>bx.x1+0.1 || my>bx.y1+0.1) && (i%3===1) && !facesCamera(Math.cos(tm),Math.sin(tm),B))
            wallQ(out, p1[0],p1[1], p0[0],p0[1], zF+0.75, zF+2.35, 'glass', null, b.night?0:0.9);
        }
        if(b.furnish){
          const rr={x0:ac.cx-ac.r*0.62,x1:ac.cx+ac.r*0.5,y0:ac.cy-ac.r*0.62,y1:ac.cy+ac.r*0.5};
          const ch=put(out,ctx,rr,zF,'armchair',{fabric:T.kit.fabric2, fabric2:T.kit.fabric, wood:'walnut'},
            {wall:'free', fx:0.4, fy:0.4, room:ac.host});
          if(ch) A(ctx,'read', ac.host, ch.cx, ch.cy, zF+0.42, {at:'turret'});
        }
        ctx.openings.push({kind:'turret', room:ac.host, level:li, x:r3(ac.cx), y:r3(ac.cy), w:r3(ac.r*2), h:2.4});
      } else if(ac && ac.kind==='tower'){
        slab(out, [[ac.x0,ac.y0],[ac.x1,ac.y0],[ac.x1,ac.y1],[ac.x0,ac.y1]], zF, 'hallFloor', 0.14, hallTex);
        boxSolid(out, ac.x0-wallT, ac.x0, ac.y0, ac.y1+wallT, zF-floorT, zC, 'wall', plasT, 0.35, 0.3);
        boxSolid(out, ac.x1, ac.x1+wallT, ac.y0, ac.y1+wallT, zF-floorT, zC, 'wall', plasT, 0.35, 0.3);
        if(!facesCamera(0,1,B)){
          boxSolid(out, ac.x0-wallT, ac.x1+wallT, ac.y1, ac.y1+wallT, zF-floorT, zC, 'wall', plasT, 0.35, 0.3);
          if(li===0){
            decalY(out, ac.y1, -1, -0.95, 0.95, zF+0.02, zF+2.50, 'join', 0.15, null, true, 0.06);
            decalY(out, ac.y1, -1, -0.80, 0.80, zF+0.16, zF+2.34, 'join', -0.45, null, true, 0.07);
          } else {
            decalY(out, ac.y1, -1, -0.85, 0.85, zF+0.10, zF+2.42, 'glass', b.night?0:0.9, null, true, 0.06);
            decalY(out, ac.y1, -1, -1.00, 1.00, zF+2.42, zF+2.56, 'join', 0.8, null, true, 0.05);
          }
        }
        if(b.furnish && li>0){
          // inside the arch and past the masonry, not merely inside the tower's outline
          const hwid=Math.max(0.9, Math.min(1.45,(ac.x1-ac.x0)/2-0.15));
          const rr={x0:-hwid+0.06, x1:hwid-0.06, y0:ac.y0+0.06, y1:ac.y1-0.14};
          const tb=put(out,ctx,rr,zF,'roundTable',{wood:T.kit.wood},{wall:'free', fx:0.5, fy:0.45, room:ac.host});
          if(tb) A(ctx,'sit', ac.host, tb.cx, tb.cy-0.62, zF+0.44, {at:'tower'});
        }
        ctx.openings.push({kind: li===0?'porch':'tower', room:ac.host, level:li,
          x:r3((ac.x0+ac.x1)/2), y:r3((ac.y0+ac.y1)/2), w:r3(ac.x1-ac.x0), h:2.5});
        if(ac.balcony) A(ctx,'balcony', ac.host, 0, ac.y1-0.4, zF);
      }

      // ---- furniture
      if(b.furnish) for(const r of rooms) furnishRoom(out,ctx,r,b,zF);

      // ---- the flights on this level, and the well rail round the hole this plate carries
      const nxt=L.levels[li+1];
      const riseT = nxt ? (nxt.floorZ - LV.floorZ) : (LV.ceil+floorT);
      for(const st of LV.stairs) fStair(out,ctx,st, zF, riseT);
      for(const v of voids) wellRail(out,ctx,v, zF);

      if(li===0) A(ctx,'entry','l0_hallF', 0, bx.y1+0.4, zF);
    }
    return { faces:out, ctx };
  }

  // ---- raster --------------------------------------------------------------------------------
  function paint(faces, B, MATS){
    const N=W*H;
    const zbuf=new Float32Array(N).fill(Infinity), dep=new Float32Array(N);
    const rbuf=new Array(N).fill(null), ibuf=new Int16Array(N), nbuf=new Array(N).fill(null);
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
      let n=normal(rv[0],rv[1],rv[2]); let sh=shadeOf(n,B.se,B.ce);
      if(sh<0 && f.b<=-1) sh=shadeOf([-n[0],-n[1],-n[2]],B.se,B.ce)*0.9;
      const fidx=sh*GAIN+BIAS+f.b;
      const M=MATS[f.mat]||MATS.wall, ramp=M.ramp, tex=f.tex, uv=f.uv, flat=f.flat;
      for(let t=1;t+1<rv.length;t++) fillTri(rv[0],rv[t],rv[t+1],0,t,t+1);
      function fillTri(a,b,c,ia,ib,ic){
        const minX=Math.max(0,Math.floor(Math.min(a.sx,b.sx,c.sx))), maxX=Math.min(W-1,Math.ceil(Math.max(a.sx,b.sx,c.sx)));
        const minY=Math.max(0,Math.floor(Math.min(a.sy,b.sy,c.sy))), maxY=Math.min(H-1,Math.ceil(Math.max(a.sy,b.sy,c.sy)));
        const area=(b.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b.sy-a.sy); if(Math.abs(area)<1e-6) return;
        const ua=uv?uv[ia]:null, ub=uv?uv[ib]:null, uc=uv?uv[ic]:null;
        for(let y=minY;y<=maxY;y++) for(let x=minX;x<=maxX;x++){
          const px=x+0.5, py=y+0.5;
          const w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area;
          const w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area, w2=1-w0-w1;
          if(w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const d=w0*a.d+w1*b.d+w2*c.d, deff=d-f.db, i=y*W+x;
          if(deff<zbuf[i]){ zbuf[i]=deff; dep[i]=d; nbuf[i]=f.mat; let fi=fidx;
            if(tex&&uv){ const uu=w0*ua[0]+w1*ub[0]+w2*uc[0], vv=w0*ua[1]+w1*ub[1]+w2*uc[1]; fi+=tex(uu,vv); }
            let idx; if(flat){ idx=Math.round(fi); } else { const base=Math.floor(fi); idx=base+((fi-base)>BAYER[x&3][y&3]?1:0); }
            idx=Math.max(0,Math.min(ramp.length-1,idx)); rbuf[i]=ramp; ibuf[i]=idx; }
        }
      }
    }
    return { rbuf, ibuf, nbuf, dep };
  }
  function post(bufs, b){
    const { rbuf, ibuf, nbuf, dep }=bufs, N=W*H, out=new Array(N).fill(null);
    for(let i=0;i<N;i++){ if(rbuf[i]) out[i]=rbuf[i][ibuf[i]]; }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){
      const i=y*W+x; if(!rbuf[i]) continue;
      for(const [dx,dy] of [[1,0],[0,1]]){
        const nx=x+dx, ny=y+dy; if(nx>=W||ny>=H) continue;
        const j=ny*W+nx; if(!rbuf[j]) continue;
        if(Math.abs(dep[i]-dep[j])>EDGE){ const far=dep[i]>dep[j]?i:j; out[far]=rbuf[far][Math.max(0,ibuf[far]-2)]; }
      }
    }
    for(let y=1;y<H-1;y++) for(let x=1;x<W-1;x++){
      const i=y*W+x, m=nbuf[i];
      if(!m || m.indexOf('fire')<0) continue;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1],[1,-1],[-1,-1]]){
        const j=(y+dy)*W+(x+dx); if(out[j]&&nbuf[j]!==m) out[j]=mix(out[j],'#f0b45a',b.night?0.30:0.16); }
    }
    if(b.night) for(let y=1;y<H-1;y++) for(let x=1;x<W-1;x++){
      const i=y*W+x; if(nbuf[i]!=='glass') continue;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const j=(y+dy)*W+(x+dx);
        if(out[j]&&nbuf[j]!=='glass') out[j]=mix(out[j],'#f0c66a',0.14); }
    }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){
      const i=y*W+x; if(!out[i]) continue; let n=0;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy;
        if(nx>=0&&nx<W&&ny>=0&&ny<H&&out[ny*W+nx]) n++; }
      if(n===0){ out[i]=null; rbuf[i]=null; }
    }
    if(b.outline) for(let y=0;y<H;y++) for(let x=0;x<W;x++){
      const i=y*W+x; if(out[i]) continue; let touch=false;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy;
        if(nx>=0&&nx<W&&ny>=0&&ny<H&&rbuf[ny*W+nx]){ touch=true; break; } }
      if(touch) out[i]=KEY;
    }
    return out;
  }
  function toRGBA(cols){
    const rgba=new Uint8ClampedArray(W*H*4);
    for(let i=0;i<W*H;i++){ const c=cols[i]; if(!c){ rgba[i*4+3]=0; continue; }
      const [r,g,bl]=hex2rgb(c); rgba[i*4]=r; rgba[i*4+1]=g; rgba[i*4+2]=bl; rgba[i*4+3]=255; }
    return rgba;
  }
  function render(dir, opts){
    opts=(typeof opts==='number')?{elev:opts}:(opts||{});
    const b=resolve(Object.assign({},opts,{dir}));
    if(!b){ if(root.console) console.warn('[ManorUnitIso] ManorIso not loaded — load Art/manorIsoRig.js first.');
      return new Uint8ClampedArray(W*H*4); }
    const built=build(b);
    const MATS=Object.assign(makeMats(b), built.ctx.mats);
    const faces=root.CoastalPass?root.CoastalPass.apply('manorInterior',built.faces,MATS,b,opts,built.ctx):built.faces;
    return toRGBA(post(paint(faces, camBasis({dir,elev:b.elev}), MATS), b));
  }

  // ---- published contracts -------------------------------------------------------------------
  function dims(opts){
    const b=resolve(opts||{}); if(!b) return { Wd:0, Ln:0, topZ:0 };
    const L=b.L, first=L.levels[b.levelList[0]], last=L.levels[b.levelList[b.levelList.length-1]];
    return { tier:b.tier, label:b.T.label, focus:b.focus, levels:L.levels.length,
      levelList:b.levelList.slice(), Wd:L.box.Wi, Ln:L.box.Di,
      spine:L.spine, explode:b.explode,
      baseZ:r3(first.floorZ + b.levelList[0]*b.explode),
      topZ:r3(last.floorZ + b.levelList[b.levelList.length-1]*b.explode + last.ceil),
      area:r3(L.rooms.reduce((s,r)=>s+r.area,0)),
      rooms:L.rooms.length, skipped:L.skipped };
  }
  function plan(opts){
    const b=resolve(opts||{}); if(!b) return null;
    const L=b.L;
    return { tier:b.tier, spine:L.spine, box:L.box,
      levels:L.levels.map(lv=>({ index:lv.index, id:lv.id, kind:lv.kind, floorZ:lv.floorZ, ceil:lv.ceil,
        box:lv.box, isAttic:lv.isAttic, stairs:lv.stairs, voids:lv.voids, accent:lv.accent, alcove:lv.alcove,
        rooms:L.rooms.filter(r=>r.level===lv.index),
        parts:L.parts.filter(p=>p.level===lv.index),
        doors:L.doors.filter(d=>d.level===lv.index) })),
      skipped:L.skipped };
  }
  function rooms(opts){
    const b=resolve(opts||{}); if(!b) return [];
    return b.L.rooms.map(r=>({ id:r.id, kind:r.kind, level:r.level, area:r.area,
      w:r3(r.x1-r.x0), d:r3(r.y1-r.y0), principal:!!r.principal, service:!!r.service,
      wet:!!(r.wet||r.kind==='bath'||r.kind==='wc'), fire:r.fire||null }));
  }
  function audit(opts){
    const b=resolve(opts||{}); if(!b) return null;
    const acc=walkAccess(b.L);
    const built=build(Object.assign({},b,{levelList:b.L.levels.map((_,i)=>i), furnish:true, explode:0}));
    const unreachable=acc.filter(a=>!a.reachable);
    const sp=b.L.spine;
    return {
      rooms:acc, unreachable,
      unplacedDoors:b.L.levels.reduce((a,lv)=>a.concat(lv.unplacedDoors||[]),[]),
      unplacedProps:built.ctx.rejects,
      flues:built.ctx.flues,
      stairwells:built.ctx.voidsAll,
      checks:[
        { id:'access', ok:unreachable.length===0,
          note: unreachable.length===0 ? 'every room reachable from the street through doors and flights'
            : unreachable.length+' room(s) with no route' },
        { id:'walkway', ok: sp.walkMid>=1.05 && sp.walkRear>=1.05,
          note:'walking strip beside the flights: '+sp.walkMid.toFixed(2)+' m past the main, '+sp.walkRear.toFixed(2)+' m past the service' },
        { id:'voids_in_landing', ok:built.ctx.voidsAll.every(v=>{
            const room=b.L.rooms.find(r=>r.level===v.level && r.stair &&
              v.x0>=r.x0-0.15 && v.x1<=r.x1+0.15 && v.y0>=r.y0-0.15 && v.y1<=r.y1+0.15)
              || b.L.rooms.find(r=>r.level===v.level && r.service && r.floor==='hall' &&
              v.x0>=r.x0-0.15 && v.x1<=r.x1+0.15 && v.y0>=r.y0-0.15 && v.y1<=r.y1+0.15);
            return !!room; }),
          note:'every stairwell falls inside the stair hall or the service passage, so no plate is cut through a room' },
        { id:'props_clear', ok:built.ctx.rejects.filter(x=>!x.optional).length===0,
          note: (function(){ const hard=built.ctx.rejects.filter(x=>!x.optional).length, soft=built.ctx.rejects.length-hard;
            return hard===0
              ? 'every fitting a room needs found a clear slot'+(soft?'; '+soft+' optional piece(s) left out rather than overlapped':'')
              : hard+' needed fitting(s) with nowhere clear to stand'; })() },
      ],
    };
  }
  function interior(opts){
    const o=opts||{}; const b=resolve(o); if(!b) return null;
    const built=build(Object.assign({},b,{levelList:b.L.levels.map((_,i)=>i), explode:0}));
    const ctx=built.ctx, seen={}, anchors=[];
    for(const a of ctx.anchors){ const k=a.verb+'|'+a.room+'|'+a.level+'|'+a.x+'|'+a.y;
      if(seen[k]) continue; seen[k]=1; anchors.push(a); }
    return {
      contract:'32 px = 1 m · same origin, pivot and metres as ManorIso · +y front/entrance · z from ground',
      tier:b.tier, label:b.T.label,
      shell:{ wallT:b.sh.wallT, storeys:b.sh.storeys, storeyH:b.sh.storeyH, fH:b.sh.fH,
              eaveZ:b.sh.eaveZ, topZ:b.sh.topZ, roofForm:b.sh.roofForm, accent:b.sh.accent },
      spine:b.L.spine, box:b.L.box,
      levels:b.L.levels.map(lv=>({ index:lv.index, id:lv.id, kind:lv.kind, floorZ:lv.floorZ,
        ceil:lv.ceil, box:lv.box, isAttic:lv.isAttic })),
      rooms:b.L.rooms, stairs:b.L.stairs, stairVoids:ctx.voidsAll,
      thresholds:b.L.doors.map(d=>({ level:d.level, axis:d.axis, plane:d.plane, c:d.c,
        clearW:d.clearW, from:d.from, to:d.to, kind:d.kind })),
      openings:ctx.openings, flues:ctx.flues, anchors, blockers:ctx.blockers,
      keepClear:ctx.clearAll, unplaced:ctx.rejects, skipped:b.L.skipped,
      access:walkAccess(b.L),
      counts:{ rooms:b.L.rooms.length, doors:b.L.doors.length, anchors:anchors.length,
        blockers:ctx.blockers.length, props:ctx.blockers.filter(x=>x.kind!=='wall'&&x.kind!=='stair'&&x.kind!=='well').length,
        verbs:anchors.reduce((m,a)=>{ m[a.verb]=(m[a.verb]||0)+1; return m; },{}) },
    };
  }
  function anchors(dir, opts){
    opts=opts||{};
    const b=resolve(Object.assign({},opts,{dir})); if(!b) return { anchors:[] };
    const B=camBasis({dir, elev:b.elev});
    const built=build(b);
    const pj=(x,y,z)=>{ const v=projVert(x,y,z,B); return { x:Math.round(v.sx), y:Math.round(v.sy) }; };
    return { anchors: built.ctx.anchors.map(a=>Object.assign({},a,pj(a.x,a.y,a.z))),
      openings: built.ctx.openings.map(o=>Object.assign({},o,pj(o.x,o.y,0))),
      dims: dims(opts) };
  }
  function project(dir,p,elev){ const v=projVert(p[0],p[1],p[2],camBasis({dir,elev})); return {x:v.sx,y:v.sy}; }

  root.ManorUnitIso = { W,H,PX, pivot:{x:cx,y:groundY}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    FINISHES, TIERS, ROOMKINDS, VERBS, PRESETS,
    render, dims, plan, rooms, interior, audit, anchors, project, resolved:(o)=>resolve(o) };
})(typeof globalThis!=='undefined'?globalThis:window);
