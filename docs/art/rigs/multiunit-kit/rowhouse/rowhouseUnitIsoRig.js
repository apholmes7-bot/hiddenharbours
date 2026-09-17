/* Hidden Harbours — parametric ISO ROWHOUSE UNIT INTERIOR rig (ADR-0006 bake pipeline, SAME
   turntable + camera + shading as rowhouseIsoRig.js / houseIsoRig.js / interiorIsoRig.js / the fleet).
   Dollhouse cutaway interiors for the terrace: a fully finished, furnished unit — plan, finishes,
   fixtures, furniture — baked through the SHARED 3/4 camera: 45deg steps, elev 40deg default,
   flat-facet shading, z-buffered, ordered dither, per-face uv texture, depth-edge darkening, NO AA.
   32 px = 1 m. All 8 facings fall out of one model.

   TWO HARD DEPENDENCIES, BOTH DELIBERATE. Art/rowhouseIsoRig.js supplies every dimension, and
   Art/interiorPropRig.js (PropIso) supplies every piece of furniture: this rig PLACES props, it
   does not model them. Each prop comes through PropIso.emit() into this rig's world space carrying
   PropIso's own ramps, so the bed in a rowhouse bedroom is the same model and the same colours as
   the bed baked standalone or dropped into a cottage room — there is one bed in the project, not
   two. A tier only PICKS from the catalogue (TIERS[].kit): basic draws the period fixtures (icebox,
   cook stove, dry sink and pump, clawfoot tub, high-cistern WC, washstand, wash tub), luxury the
   modern ones (fridge, range, wet sink, walk-in shower, basin vanity, close-coupled WC, sofa,
   washing machine). Load order: rowhouseIsoRig.js, interiorPropRig.js, then this file.

   THIS RIG MEASURES NOTHING. Every dimension comes from RowhouseIso.shell(opts) — wall thickness,
   party-wall thickness, floor thickness, per-storey floor levels, ceiling height, each unit's
   interior box and its openings. Same cell, same pivot, same metres as the exterior bake, so an
   interior composites under its own bay to the pixel. RowhouseIso is a HARD dependency (load
   Art/rowhouseIsoRig.js first); without it render() returns an empty buffer and warns rather than
   inventing a second set of numbers that could drift from the shell.

   PLAN GENERATOR. A rowhouse unit is narrow and deep, so the plan is a band system down the depth
   with a hall strip on the party wall carrying the stair. Ground: hall + stair, living, kitchen/
   dining (luxury: open-plan living-dining with an island, plus a powder room off the hall). Upper
   storeys: a full-width front bedroom, a middle band holding the landing and a bath / third bed /
   laundry, and a rear band splitting into a bedroom and the main bath. 1, 2 and 3 bed all resolve
   from that skeleton; a 3-storey luxury unit puts the primary suite on top with its own ensuite and
   walk-in. Room count, partition runs, door gaps and every piece of furniture are generated — nothing
   is hand-placed per configuration.

   TWO TIERS, one loft:
     basic    pine boards, painted plaster, laminate worktops, enamel fixtures, a clawfoot-era tub,
              painted joinery, a modest sofa and a scrubbed table. Bath is a tub-shower.
     luxury   oiled oak floors, stone tile in the wet rooms, stone worktops, an island with seating,
              walk-in shower with glass, double vanity, in-suite laundry, ensuite off the primary bed,
              taller ceilings, wider rooms, balcony doors on the upper storeys.

   CUTAWAY. Perimeter walls whose outward normal faces the camera are cut to a 0.55 m lip; the two
   far walls stand full height and carry the window reveals, the inside face of the front door and
   (luxury) the balcony doors. Partitions are always cut to 1.15 m so every room reads from above.
   Multi-storey views explode upward by 1.5 m so the lower plate stays legible.

   RENDER MODES (all through render(dir, opts)):
     focus:'unit'  + storey:0|1|2   one unit, one storey            <- the gameplay grain
     focus:'unit'  + storey:'stack' one unit, storeys exploded
     focus:'floor' + storey:n       every unit on that storey        <- the floorplate/building view
     focus:'all'                    the whole terrace, exploded

   GAMEPLAY PUBLISHING (interior(opts), consumed by the occupancy/schedule pass):
     rooms       id, kind, storey, box, area
     thresholds  every door: room pair, centre, clear width, kind
     anchors     ROUTINE points with verbs — sleep (per bed side), cook, wash_dishes, sit (per seat),
                 dine (per seat), toilet, bathe, vanity, laundry, store, desk, entry, balcony
     blockers    every wall run and furniture footprint with its height
     stairs      footprint, steps, rise, run, top and bottom landing points
   anchors(dir, opts) reports the same points in cell px per facing, so NPC routes and interaction
   prompts are runtime overlays on baked points.

   LIGHT: matches the shipped neighbours — LN = normalize([-0.42,0.72,0.52]), upper-left key. Same
   note as the exterior rig: art bible §1 rules the canonical key is top-of-frame; a terrace interior
   must match the terrace it sits in, so correcting it is a whole-set decision, not this rig's.
   KEYLINE: ringless by default (ADR-0031); {outline:true} kept as a live A/B.

   Exposes globalThis.RowhouseUnitIso = { W,H,PX,DIRS,pivot,order,defaultElev, TIERS,FINISHES,
     ROOMKINDS,VERBS,PRESETS, dims(opts), plan(opts), rooms(opts), interior(opts),
     render(dir,opts), anchors(dir,opts), project(dir,p,elev) }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 1320, H = 1160, cx = 660, groundY = 745;
  const DEG = Math.PI / 180, DEFAULT_ELEV = 40, KEYLINE_DEFAULT = false;
  const EXPLODE = 1.5, CUT_NEAR = 0.55, CUT_PART = 1.15;

  // ---- finish ramps, dark -> light (KTC master ramps) -----------------------
  const FINISHES = {
    floorPine:  ['#4a3a24','#5f4a2d','#7a6039','#957648','#ac8b5b','#c2a274'],
    floorOak:   ['#3e2f22','#54402e','#6b543c','#84694c','#9c805f','#b39875'],
    tileGrey:   ['#3a3f42','#4b5155','#5e6569','#71797d','#868e92','#9aa3a7'],
    tileStone:  ['#4a4438','#5e5747','#746c59','#8a816c','#a09781','#b6ad97'],
    plaster:    ['#7d786c','#948e80','#aaa494','#c0b9a8','#d4cdbb','#e6dfcd'],
    plasterLux: ['#7e7e7a','#949490','#aaaaa6','#c0c0bc','#d5d5d1','#e8e8e4'],
    joist:      ['#2b2118','#3a2d20','#4a3a29','#5b4834','#6d5740','#7f664c'],
    wallTile:   ['#5b6567','#6f7a7c','#848f91','#98a3a5','#acb7b9','#c0cbcd'],
    lamTop:     ['#2b2d30','#3a3d40','#4a4e51','#5b6063','#6d7275','#7f8487'],
    stoneTop:   ['#4b4b46','#5f5f59','#75756e','#8b8b83','#a1a199','#b7b7af'],
    joinCream:  ['#8a7f5e','#a3976f','#bcaf83','#d0c49a','#e1d7b5','#efe8ce'],
    joinSage:   ['#3a4636','#4a5843','#5c6b52','#718063','#889777','#a1ae90'],
    joinGraph:  ['#22262b','#2e333a','#3c424a','#4c535c','#5d6570','#6f7883'],
    upholTeal:  ['#2c3a3f','#3a4c52','#4a6066','#5b747b','#6d8990','#809ea5'],
    upholBoucle:['#3b3630','#4c463e','#5f584d','#726a5d','#867d6e','#9a9080'],
    bedding:    ['#8d8b82','#a5a29a','#bcb9b1','#d1cec6','#e3e0d8','#f2efe8'],
    furnWood:   ['#3a2a1c','#4e3926','#634a31','#7a5e3f','#91744f','#a88a63'],
    metal:      ['#4a4f52','#5e6467','#757c7f','#8d9497','#a5acaf','#bdc4c7'],
    brass:      ['#4a3a16','#63501f','#7d6829','#98813a','#b19a52','#c9b373'],
    steel:      ['#6a7073','#7d8386','#929899','#a6acad','#babfc0','#ced2d3'],
    enamel:     ['#8f9490','#a3a8a3','#b7bcb6','#c9cec8','#dadfd9','#eaefe9'],
    rugRust:    ['#3d2a2a','#523736','#684644','#7e5654','#946867','#a97c7a'],
    rugOlive:   ['#3a3f36','#4b5145','#5e6455','#727866','#868c79','#9aa08c'],
  };
  const GLASS_DAY   = ['#7d949b','#94aab0','#abbfc4','#c2d4d8'];
  const GLASS_NIGHT = ['#23283198','#2a2f3a','#343a46','#414855'].map(c=>c.slice(0,7));
  const SHOWERGLASS = ['#5f7a80','#74919a','#8aa8b0'];
  const CAVITY = ['#0d1013','#141a1e','#1b2328','#232c32'];
  const KEY = '#1a1c22';

  const TIERS = {
    basic: {
      floor:'floorPine', wet:'tileGrey', wall:'plaster', join:'joinCream',
      ensuite:false, laundry:false, openPlan:false, powder:false, balconyDoors:false, weather:0.20,
      // the tier only PICKS from the prop catalogue; geometry lives in Art/interiorPropRig.js
      kit:{ era:'period', wood:'oak', paint:'cream', worktop:'slate', fabric:'red', fabric2:'gold',
            cold:'icebox', cook:'stove', sink:'sink', seat:'bench', wc:0, tub:0, wash:'washtub' },
    },
    luxury: {
      floor:'floorOak', wet:'tileStone', wall:'plasterLux', join:'joinSage',
      ensuite:true, laundry:true, openPlan:true, powder:true, balconyDoors:true, weather:0.05,
      kit:{ era:'modern', wood:'walnut', paint:'sage', worktop:'marble', fabric:'blue', fabric2:'cream',
            cold:'fridge', cook:'range', sink:'wetSink', seat:'sofa', wc:1, tub:1, wash:'washer' },
    },
  };
  const ROOMKINDS = ['hall','stair','living','dining','kitchen','bed','bath','ensuite','powder','laundry','study','store','closet','landing'];
  const VERBS = ['sleep','cook','wash_dishes','sit','dine','toilet','bathe','vanity','laundry','store','desk','entry','balcony','stair'];
  const PRESETS = {
    millUnit2Bed:   { tier:'basic',  beds:2, storeys:2, units:4, focus:'unit',  storey:'stack' },
    millUnit3Bed:   { tier:'basic',  beds:3, storeys:2, units:4, focus:'unit',  storey:'stack' },
    millPlateUpper: { tier:'basic',  beds:2, storeys:2, units:4, focus:'floor', storey:1 },
    coastalSuite:   { tier:'luxury', beds:3, storeys:3, units:4, focus:'unit',  storey:'stack' },
    coastalGround:  { tier:'luxury', beds:3, storeys:3, units:4, focus:'floor', storey:0 },
    coastalTerrace: { tier:'luxury', beds:3, storeys:3, units:4, focus:'all' },
  };

  // ---- shading constants (fleet recipe) ----
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

  // ---- camera / projection (identical to the exterior rig) -------------------
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
  // a plane whose outward normal turns toward the camera occludes the interior
  function facesCamera(nx, ny, B){ return (nx*B.stt + ny*B.ct) < 0; }

  // ---- face builders --------------------------------------------------------
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

  // ---- textures: integer ramp deltas ----------------------------------------
  function boardTex(sp){ const SP=sp||0.145; return (u,v)=>{ const su=((u%SP)+SP)%SP;
    if(su<0.016) return -2; const t=hash2(Math.floor(u/SP), Math.floor(v*3)); return t<0.18?-1:(t>0.9?1:0); }; }
  function tileTex(sp){ const SP=sp||0.30; return (u,v)=>{ const su=((u%SP)+SP)%SP, sv=((v%SP)+SP)%SP;
    if(su<0.028||sv<0.028) return -2; const t=hash2(Math.floor(u/SP), Math.floor(v/SP)); return t<0.14?-1:(t>0.9?1:0); }; }
  function plasterTex(){ return (u,v)=>{ const t=hash2(Math.floor(u*8), Math.floor(v*8)); return t<0.055?-1:0; }; }
  function stoneTex(){ return (u,v)=>{ const t=hash2(Math.floor(u*11), Math.floor(v*11)); return t<0.13?-1:(t>0.93?1:0); }; }
  function weaveTex(){ return (u,v)=>{ const t=hash2(Math.floor(u*14), Math.floor(v*14)); return t<0.22?-1:(t>0.88?1:0); }; }
  function subwayTex(){ const BW=0.30, BH=0.15; return (u,v)=>{ const row=Math.floor(v/BH), off=(row&1)*BW*0.5;
    const f=((v%BH)+BH)%BH, su=(((u+off)%BW)+BW)%BW; return (f<0.022||su<0.022)?-2:0; }; }

  // ---- rasterizer (fleet recipe) --------------------------------------------
  function paint(faces, opts, MATS){
    const B=camBasis(opts), N=W*H;
    const zbuf=new Float32Array(N).fill(Infinity), dep=new Float32Array(N);
    const rbuf=new Array(N).fill(null), ibuf=new Int16Array(N), nbuf=new Array(N).fill(null);
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
      let n=normal(rv[0],rv[1],rv[2]);
      let sh=shadeOf(n, B.se, B.ce);
      if(sh<0 && (f.b<=-1)) sh=shadeOf([-n[0],-n[1],-n[2]], B.se, B.ce)*0.9;
      const fidx = sh*GAIN + BIAS + f.b;
      const M = MATS[f.mat] || MATS.wall;
      const ramp=M.ramp, off=M.off||0, tex=f.tex, uv=f.uv, flat=f.flat;
      for(let t=1;t+1<rv.length;t++) fillTri(rv[0],rv[t],rv[t+1], 0,t,t+1);
      function fillTri(a,b,c, ia,ib,ic){
        const minX=Math.max(0,Math.floor(Math.min(a.sx,b.sx,c.sx)));
        const maxX=Math.min(W-1,Math.ceil(Math.max(a.sx,b.sx,c.sx)));
        const minY=Math.max(0,Math.floor(Math.min(a.sy,b.sy,c.sy)));
        const maxY=Math.min(H-1,Math.ceil(Math.max(a.sy,b.sy,c.sy)));
        const area=(b.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b.sy-a.sy);
        if(Math.abs(area)<1e-6) return;
        const ua=uv?uv[ia]:null, ub=uv?uv[ib]:null, uc=uv?uv[ic]:null;
        for(let y=minY;y<=maxY;y++) for(let x=minX;x<=maxX;x++){
          const px=x+0.5, py=y+0.5;
          const w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area;
          const w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area;
          const w2=1-w0-w1;
          if(w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const d=w0*a.d+w1*b.d+w2*c.d, deff=d-f.db, i=y*W+x;
          if(deff<zbuf[i]){
            zbuf[i]=deff; dep[i]=d; nbuf[i]=f.mat;
            let fi=fidx;
            if(tex&&uv){ const uu=w0*ua[0]+w1*ub[0]+w2*uc[0], vv=w0*ua[1]+w1*ub[1]+w2*uc[1]; fi += tex(uu,vv); }
            let idx;
            if(flat){ idx=Math.round(fi)+off; }
            else { const base=Math.floor(fi); idx=base+((fi-base)>BAYER[x&3][y&3]?1:0)+off; }
            rbuf[i]=ramp; ibuf[i]=Math.max(0,Math.min(ramp.length-1,idx));
          }
        }
      }
    }
    return { rbuf, ibuf, nbuf, dep };
  }

  // ---- plan generator =======================================================
  function shellOf(o){
    const RI = root.RowhouseIso;
    if(!RI) return null;
    return RI.shell({ tier:o.tier, units:o.units, beds:o.beds, mix:o.mix, storeys:o.storeys,
      clad:o.clad, stagger:o.stagger });
  }
  function room(id,kind,storey,x0,x1,y0,y1,extra){
    return Object.assign({ id, kind, storey, x0:r3(x0), x1:r3(x1), y0:r3(y0), y1:r3(y1),
      area:r3(Math.abs((x1-x0)*(y1-y0))) }, extra||{});
  }
  // partition record: axis 'x' runs along x at y=plane; 'y' runs along y at x=plane
  function part(axis, plane, a0, a1, gaps){ return { axis, plane:r3(plane), a0:r3(a0), a1:r3(a1), gaps:(gaps||[]).map(g=>[r3(g[0]),r3(g[1])]) }; }
  function door(axis, plane, c, wClear, from, to, kind){
    return { axis, plane:r3(plane), c:r3(c), clearW:r3(wClear), from, to, kind:kind||'interior' };
  }

  function layoutUnit(u, sh, tier, storeys){
    const T=TIERS[tier], lux=tier==='luxury';
    const x0=u.interior.x0, x1=u.interior.x1, yF=u.interior.yFront, yR=u.interior.yRear;
    const w=x1-x0, d=yF-yR, beds=u.beds;
    const DW = lux?0.92:0.84;
    // the spine carries the flight AND a walking strip wide enough for a door to clear it
    const hallW = clamp(w*0.42, DW+1.30, 2.55), hx = x0+hallW;
    const rooms=[], parts=[], doors=[], stairs=[];

    // ================= THE SPINE =================
    // One stair strip against the party wall, in the SAME place on every storey, and a middle band
    // sized to hold a whole flight. That is what makes the flight below, the stairwell void and the
    // landing above line up: the hole always falls in the landing, never punched through a bedroom,
    // and no plate is ever laid across a rising flight. Every other band is measured off this one.
    const STEPS = 13, BM = 2.95, VOID_D = 1.10;
    const stW = Math.min(1.02, hallW-DW-0.26);
    const brBase = clamp(d*0.32, 3.10, 3.60);        // rear band == kitchen depth, so they align
    const bmY0 = yR + brBase, bmY1 = bmY0 + BM;      // the middle (landing) band
    const bf = yF - bmY1;                            // front band
    const stTop = bmY0 + 0.14, stRun = BM - 0.28;    // flight rises toward the rear, top at stTop
    const dGap = x0 + stW + 0.18;                    // doors sit in the landing's clear strip
    // EXACT FLIGHT: the rise published is the unrounded riseTotal/steps the drawing already climbs,
    // so steps x rise === floorRise to the bit. The rounded figure lost a fraction of a millimetre
    // a tread and left the top step short of the plate — the manor's 18 x 0.197 defect. Landings
    // travel with the flight so the placer can reserve them before anything is furnished.
    const floorRise = sh.ceilH + sh.floorT;
    const mkStair = (s)=>{
      const f={ storey:s, x0:r3(x0+0.10), x1:r3(x0+0.10+stW),
        y0:r3(stTop), y1:r3(stTop+stRun), steps:STEPS,
        rise:floorRise/STEPS, run:r3(stRun/STEPS), floorRise,
        voidY0:r3(stTop), voidY1:r3(stTop+VOID_D), dir:'rises_to_rear' };
      f.landings=[
        { tag:'the bottom landing', end:'bottom', storey:s, x0:r3(x0), x1:r3(x0+0.20+stW),
          y0:r3(f.y0-1.05), y1:f.y0 },
        { tag:'the top landing', end:'top', storey:s+1, x0:r3(x0), x1:r3(x0+0.20+stW),
          y0:f.y1, y1:r3(f.y1+1.05) },
      ];
      return f;
    };

    const partsFrom = (i, st)=>{ for(; i<parts.length; i++) parts[i].storey = st; };
    const doorsFrom = (i, st)=>{ for(; i<doors.length; i++) doors[i].storey = st; };

    // ================= GROUND =================
    const gP0 = parts.length, gD0 = doors.length;
    const kY = bmY0;
    const CORR_MIN = 0.95, POW_MIN = 1.50;
    const corrW = clamp(hallW - POW_MIN, CORR_MIN, 1.16);
    const powderFits = T.powder && (hallW - corrW) >= POW_MIN - 0.02;
    const entryD = 1.35, powD = powderFits ? 1.60 : 0;
    const pY0 = yF - entryD - powD;
    rooms.push(room('g_hall','hall',0, x0,hx, kY,pY0, {stair:true}));
    rooms.push(room('g_entry','hall',0, x0,hx, pY0+powD,yF, {entry:true}));
    // THE WC MUST NOT SIT ACROSS THE CIRCULATION. It takes the far side of its band and a corridor
    // runs past it along the party wall, so the front door still reaches the hall, the stair and the
    // rest of the unit. (A full-width powder band made the entire luxury unit unreachable.)
    const pwX0 = x0 + corrW;
    if(powD){
      rooms.push(room('g_pass','hall',0, x0,pwX0, pY0,pY0+powD, {pass:true}));
      rooms.push(room('g_powder','powder',0, pwX0,hx, pY0,pY0+powD));
    }
    rooms.push(room('g_living','living',0, hx,x1, kY,yF, {open:lux}));
    rooms.push(room('g_kitchen','kitchen',0, x0,x1, yR,kY, {open:lux, dining:true}));
    stairs.push(mkStair(0));

    // the spine wall down the party side; open plan stops it short so living flows to the kitchen
    if(!T.openPlan){
      parts.push(part('y', hx, kY, yF, [[pY0-1.05, pY0-1.05+DW]]));
      doors.push(door('y', hx, pY0-1.05+DW/2, DW, 'g_hall','g_living'));
      parts.push(part('x', kY, x0, x1, [[hx+0.45, hx+0.45+DW]]));
      doors.push(door('x', kY, hx+0.45+DW/2, DW, 'g_living','g_kitchen'));
    } else {
      const openAt = Math.max(kY+0.60, pY0-2.40);
      parts.push(part('y', hx, openAt, yF, [[pY0-1.05, pY0-1.05+DW]]));
      doors.push(door('y', hx, pY0-1.05+DW/2, DW, 'g_hall','g_living'));
      parts.push(part('x', kY, x0, hx, []));
      // the kitchen is OPEN to the living end along hx..x1 — a walk-through, not a door, but it is
      // still a threshold the routing pass has to see
      doors.push(door('x', kY, (hx+x1)/2, x1-hx, 'g_living','g_kitchen','open'));
    }
    if(powD){
      // the corridor is open at both ends; the WC is the only door off it
      parts.push(part('x', pY0+powD, pwX0, hx, []));
      doors.push(door('x', pY0+powD, (x0+pwX0)/2, pwX0-x0, 'g_entry','g_pass','open'));
      parts.push(part('x', pY0, pwX0, hx, []));
      doors.push(door('x', pY0, (x0+pwX0)/2, pwX0-x0, 'g_pass','g_hall','open'));
      parts.push(part('y', pwX0, pY0, pY0+powD, [[pY0+0.30, pY0+0.30+DW]]));
      doors.push(door('y', pwX0, pY0+0.30+DW/2, DW, 'g_pass','g_powder'));
    } else {
      parts.push(part('x', pY0, x0, hx, [[x0+0.45, x0+0.45+DW]]));
      doors.push(door('x', pY0, x0+0.45+DW/2, DW, 'g_entry','g_hall'));
    }
    doors.push(door('x', yF, u.openings.entry.x, u.openings.entry.clearW, 'outside','g_entry','entry'));

    partsFrom(gP0, 0); doorsFrom(gD0, 0);

    // ================= UPPER STOREYS =================
    // 2 storeys: every bed on one plate. 3 storeys: basic splits the beds two-then-one; luxury keeps
    // the top plate as a dedicated primary suite and puts the remaining beds below.
    const PLATE_CAP = 2;
    const bedsBuilt = storeys===2 ? Math.min(beds, PLATE_CAP) : Math.min(beds, 3);
    const upper=[];
    if(storeys===2) upper.push({ s:1, beds:bedsBuilt, suite:false });
    else if(lux){ upper.push({ s:1, beds:Math.min(PLATE_CAP, Math.max(0, bedsBuilt-1)), suite:false });
                  upper.push({ s:2, beds:1, suite:true }); }
    else { upper.push({ s:1, beds:Math.min(bedsBuilt, PLATE_CAP), suite:false });
           upper.push({ s:2, beds:Math.max(0, bedsBuilt-PLATE_CAP), suite:false }); }

    let bedNo=0, hasLaundry=false;
    for(const U of upper){
      const s=U.s, tag='s'+s+'_', nb=U.beds, isTop = s===storeys-1;
      const fY0=bmY1, rY1=bmY0;                       // the spine, unchanged on every storey
      const uP0 = parts.length, uD0 = doors.length;
      rooms.push(room(tag+'landing','landing',s, x0,hx, rY1,fY0, {stair:true}));

      // ONE landing serves three rooms: the front band, the middle band beside the stair, and the
      // rear band. Every door opens off the landing's clear strip, so none of them fouls the
      // flight. That caps a plate at two bedrooms — a 5 m terrace has no depth for more.
      if(U.suite){
        // primary suite floor: bedroom + ensuite across the front, the in-suite laundry in the
        // middle band beside the stair, and the rear band as a den. The rear band is a whole
        // bay-width room — giving that over to laundry made a 23 m2 utility room.
        const sx = x0 + w*0.62;
        bedNo++;
        rooms.push(room(tag+'bed_primary','bed',s, x0,sx, fY0,yF, {primary:true, beds:1, balcony:T.balconyDoors}));
        rooms.push(room(tag+'ensuite','ensuite',s, sx,x1, fY0,yF, {wet:true}));
        parts.push(part('x', fY0, x0, x1, [[dGap, dGap+DW]]));
        doors.push(door('x', fY0, dGap+DW/2, DW, tag+'landing', tag+'bed_primary'));
        parts.push(part('y', sx, fY0, yF, [[fY0+0.35, fY0+0.35+DW]]));
        doors.push(door('y', sx, fY0+0.35+DW/2, DW, tag+'bed_primary', tag+'ensuite'));

        const midKind = hasLaundry ? 'closet' : 'laundry';
        rooms.push(room(tag+midKind, midKind, s, hx,x1, rY1,fY0, {wet:midKind==='laundry'}));
        if(midKind==='laundry') hasLaundry=true;
        parts.push(part('y', hx, rY1, fY0, [[rY1+0.45, rY1+0.45+DW]]));
        doors.push(door('y', hx, rY1+0.45+DW/2, DW, tag+'landing', tag+midKind));

        rooms.push(room(tag+'study','study',s, x0,x1, yR,rY1, {}));
        parts.push(part('x', rY1, x0, x1, [[dGap, dGap+DW]]));
        doors.push(door('x', rY1, dGap+DW/2, DW, tag+'landing', tag+'study'));
      } else {
        // FRONT band: the main bedroom (luxury gives the first one an ensuite off it), or a study
        let frontId;
        if(nb>0){
          bedNo++; frontId = tag+'bed'+bedNo;
          const wantEnsuite = lux && T.ensuite && bedNo===1 && w>=4.6;
          const sx = x0 + w*0.64;
          if(wantEnsuite){
            rooms.push(room(frontId,'bed',s, x0,sx, fY0,yF, {primary:true, beds:1, balcony:T.balconyDoors}));
            rooms.push(room(tag+'ensuite','ensuite',s, sx,x1, fY0,yF, {wet:true}));
            parts.push(part('y', sx, fY0, yF, [[fY0+0.35, fY0+0.35+DW]]));
            doors.push(door('y', sx, fY0+0.35+DW/2, DW, frontId, tag+'ensuite'));
          } else {
            rooms.push(room(frontId,'bed',s, x0,x1, fY0,yF, {primary:bedNo===1, beds:1, balcony:T.balconyDoors&&s>0}));
          }
        } else { frontId = tag+'study'; rooms.push(room(frontId,'study',s, x0,x1, fY0,yF, {})); }
        parts.push(part('x', fY0, x0, x1, [[dGap, dGap+DW]]));
        doors.push(door('x', fY0, dGap+DW/2, DW, tag+'landing', frontId));

        // MIDDLE band beside the stair: the bathroom, straight off the landing
        rooms.push(room(tag+'bath','bath',s, hx,x1, rY1,fY0, {wet:true}));
        parts.push(part('y', hx, rY1, fY0, [[rY1+0.45, rY1+0.45+DW]]));
        doors.push(door('y', hx, rY1+0.45+DW/2, DW, tag+'landing', tag+'bath'));

        // REAR band: the second bedroom, or the in-suite laundry / a linen store
        let rearId, rearKind;
        if(nb>=2){ bedNo++; rearId=tag+'bed'+bedNo; rearKind='bed';
          rooms.push(room(rearId,'bed',s, x0,x1, yR,rY1, {beds:1}));
        } else { rearKind = lux ? 'study' : 'store'; rearId=tag+rearKind;
          rooms.push(room(rearId, rearKind, s, x0,x1, yR,rY1, {})); }
        parts.push(part('x', rY1, x0, x1, [[dGap, dGap+DW]]));
        doors.push(door('x', rY1, dGap+DW/2, DW, tag+'landing', rearId));
      }
      partsFrom(uP0, s); doorsFrom(uD0, s);
      if(!isTop) stairs.push(mkStair(s));
    }
    return { rooms, parts, doors, stairs, hallW, hx, bedsBuilt, bedsRequested:beds,
             spine:{ bmY0:r3(bmY0), bmY1:r3(bmY1), stW:r3(stW) },
             box:{x0,x1,yR,yF,w,d} };
  }

  function resolve(opts){
    const o=opts||{};
    const tier = o.tier==='luxury' ? 'luxury' : 'basic';
    const storeys = clamp(Math.round(o.storeys!=null?o.storeys:2), 2, 3);
    const beds = clamp(Math.round(o.beds!=null?o.beds:2), 1, 3);
    const units = clamp(Math.round(o.units!=null?o.units:4), 2, 6);
    const sh = shellOf({ tier, units, beds, mix:o.mix||'uniform', storeys, clad:o.clad, stagger:o.stagger });
    if(!sh) return null;
    const focus = o.focus==='floor' ? 'floor' : (o.focus==='all' ? 'all' : 'unit');
    const unitIdx = clamp(Math.round(o.unit!=null?o.unit:0), 0, sh.units.length-1);
    const unitList = focus==='unit' ? [sh.units[unitIdx]] : sh.units.slice();
    let storeyList;
    if(focus==='all') storeyList = unitList[0] ? range(maxStoreys(unitList)) : [0];
    else if(focus==='floor') storeyList = [clamp(Math.round(o.storey!=null?o.storey:0), 0, storeys-1)];
    else storeyList = (o.storey==='stack') ? range(unitList[0].storeys) : [clamp(Math.round(o.storey!=null?o.storey:0),0,unitList[0].storeys-1)];
    return {
      tier, T:TIERS[tier], sh, storeys, beds, units, focus, unitIdx, unitList, storeyList,
      explode: storeyList.length>1 ? (o.explode!=null?o.explode:EXPLODE) : 0,
      furnish: o.furnish!==false, night: !!o.night,
      // Manor precedent: draw the shown storey with its floor on the pivot row, so the baker gets
      // one sprite per storey in a common frame. Every z drops by that storey's storeyZ, nothing
      // shifts sideways and storeyZ itself does not change. Defaults false, so every picture drawn
      // before this option existed stays byte-identical.
      floorAtPivot: !!o.floorAtPivot,
      weather: o.weather!=null ? o.weather : TIERS[tier].weather,
      outline: o.outline!=null ? !!o.outline : KEYLINE_DEFAULT,
      dir: o.dir||0, elev: o.elev!=null?o.elev:DEFAULT_ELEV,
      layouts: unitList.map(u=>layoutUnit(u, sh, tier, u.storeys)),
    };
  }
  function range(n){ const a=[]; for(let i=0;i<n;i++) a.push(i); return a; }
  function maxStoreys(list){ return list.reduce((m,u)=>Math.max(m,u.storeys),1); }
  // roomH and storeyRise, the cottage-room contract: the game builds walls, ceiling, the stair
  // opening and the colliders from these two numbers. storeyRise is floor-to-floor and equals the
  // stair's floorRise and steps x rise; roomH is storeyRise minus the slab, which is the plate the
  // rig actually draws. Null rise on the top storey, key present.
  function heightsOf(b, s){
    const sh=b.sh, top=sh.storeyZ.length-1;
    return { storeyRise: s<top ? r3(sh.storeyZ[s+1]-sh.storeyZ[s]) : null, roomH:r3(sh.ceilH) };
  }

  // ---- materials: ARCHITECTURE ONLY ----------------------------------------
  // No furniture ramps here, on purpose. Each prop brings PropIso's own ramps in under a
  // per-instance prefix (see put()), so a bed here is the colour of a bed anywhere else.
  function makeMats(b){
    const T=b.T, wx=b.weather, night=b.night;
    const wth=(ramp)=>ramp.map(c=>{ let x=desat(c, wx*0.35); x=mix(x,'#6b675e',wx*0.18);
      if(night) x=mix(x,'#2a2216',0.20); return x; });
    return {
      floor:{ramp:wth(FINISHES[T.floor])}, wet:{ramp:wth(FINISHES[T.wet])},
      wall:{ramp:wth(FINISHES[T.wall])}, wallTile:{ramp:wth(FINISHES.wallTile)},
      joist:{ramp:wth(FINISHES.joist)}, join:{ramp:wth(FINISHES[T.join])},
      metal:{ramp:wth(FINISHES.metal)},
      glass:{ramp: night?GLASS_NIGHT:GLASS_DAY, off: night?0:1},
      cavity:{ramp:CAVITY},
    };
  }

  // ---- wall / partition drawing --------------------------------------------
  function segsOf(a0,a1,gaps){
    let list=[[a0,a1]];
    for(const g of (gaps||[])){
      const next=[];
      for(const [s0,s1] of list){
        if(g[1]<=s0 || g[0]>=s1){ next.push([s0,s1]); continue; }
        if(g[0]>s0+0.01) next.push([s0,g[0]]);
        if(g[1]<s1-0.01) next.push([g[1],s1]);
      }
      list=next;
    }
    return list;
  }
  function drawPartition(out, ctx, p, z0, z1, t, mat){
    for(const [s0,s1] of segsOf(p.a0,p.a1,p.gaps)){
      if(s1-s0<0.02) continue;
      if(p.axis==='x'){ boxSolid(out, s0,s1, p.plane-t/2,p.plane+t/2, z0,z1, mat, null, 0.40, 0.45);
        ctx.blockers.push({kind:'wall', storey:ctx.storey, x0:r3(s0),x1:r3(s1), y0:r3(p.plane-t/2), y1:r3(p.plane+t/2), h:r3(z1-z0)}); }
      else { boxSolid(out, p.plane-t/2,p.plane+t/2, s0,s1, z0,z1, mat, null, 0.40, 0.45);
        ctx.blockers.push({kind:'wall', storey:ctx.storey, x0:r3(p.plane-t/2),x1:r3(p.plane+t/2), y0:r3(s0),y1:r3(s1), h:r3(z1-z0)}); }
    }
  }
  function drawLeaf(out, d, z0, hLeaf, mat, t){
    // Door open 180deg, FLAT against the wall beside its opening. A leaf swung into the room reads
    // as a stray half-wall and collides with whatever stands against that wall — beds, most often.
    const th=0.042, lw=d.clearW*0.92, off=(t||0.12)/2+th/2+0.004;
    if(d.axis==='x'){ const e=d.c-d.clearW/2;
      boxSolid(out, e-lw, e, d.plane+off-th/2, d.plane+off+th/2, z0, z0+hLeaf, mat, null, 0.15, 0.4); }
    else { const e=d.c-d.clearW/2;
      boxSolid(out, d.plane+off-th/2, d.plane+off+th/2, e-lw, e, z0, z0+hLeaf, mat, null, 0.15, 0.4); }
  }

  // ---- furnishing: PropIso owns the geometry, this rig owns where it goes ====
  function P(out, ctx, o){
    boxSolid(out, o.x0,o.x1, o.y0,o.y1, o.z0,o.z1, o.mat, o.tex||null, o.b||0, o.topB!=null?o.topB:0.25);
    if(o.blocker!==false) ctx.blockers.push({ kind:o.kind||'prop', storey:ctx.storey, x0:r3(o.x0),x1:r3(o.x1),y0:r3(o.y0),y1:r3(o.y1), h:r3(o.z1) });
  }
  function A(ctx, verb, room, x, y, z, extra){ ctx.anchors.push(Object.assign({ verb, room, storey:ctx.storey, x:r3(x), y:r3(y), z:r3(z||0) }, extra||{})); }
  // The floor a published anchor stands on, held against the next piece into the room. Same rule as
  // the landings, applied to furniture: an approach is not part of a footprint, so nothing stopped a
  // dresser standing exactly where you get into the bed it serves. It is a PAD, not a strip —
  // reserving a bed's whole length on both sides takes the wall the wardrobe needs and evicts it
  // from every narrow bedroom in the row. The audit marches a 0.22 m body to a point, so a 0.52 m
  // pad on that point is exactly the floor that has to stay empty.
  function reserveStand(ctx, pts, rad, tag){
    const R = rad!=null ? rad : 0.26;
    for(const p of (pts||[])){
      if(!p || !Number.isFinite(p[0]) || !Number.isFinite(p[1])) continue;
      const c={ tag: tag||'a standing spot', x0:p[0]-R, x1:p[0]+R, y0:p[1]-R, y1:p[1]+R };
      ctx.clear.push(c);
      if(ctx.clearAll) ctx.clearAll.push(Object.assign({storey:ctx.storey}, c));
    }
  }
  function finite(r){ return Number.isFinite(r.x0)&&Number.isFinite(r.x1)&&Number.isFinite(r.y0)&&Number.isFinite(r.y1); }
  // NB: every comparison against a NaN bound is false, so an undefined rect would otherwise read as
  // "overlaps everything" and silently reject every prop in the unit. Guard it.
  function hits(a, c){ if(!finite(c)||!finite(a)) return false;
    return !(a.x1<=c.x0+0.02 || a.x0>=c.x1-0.02 || a.y1<=c.y0+0.02 || a.y0>=c.y1-0.02); }
  function blocked(ctx, rect){
    for(const c of ctx.clear) if(hits(rect,c)) return c.tag||'clearance';
    for(const t of ctx.taken) if(hits(rect,t)) return t.name||'a prop';
    return null;
  }

  const WALLFACE = { rear:'S', front:'N', left:'E', right:'W' };
  const OPPOSITE = { N:'S', S:'N', E:'W', W:'E' };
  // Place a prop against a wall of `r` (or free inside it). ctx.clear holds the stairwell void and
  // every doorway zone for this storey, ctx.taken the props already placed, so nothing lands on a
  // stair, across a doorway, or inside another piece; a blocked wall slot retries along the same
  // wall before giving up. Returns the world rect or null — callers guard their anchors on it, so a
  // tight room simply gets less kit rather than a collision.
  function put(out, ctx, r, z, name, opts, pl){
    const PI = root.PropIso; if(!PI || !PI.emit) return null;
    pl = pl || {};
    const spec = PI.PROPS[name];
    let face = pl.face || WALLFACE[pl.wall] || 'N';
    // a bed's front IS its headboard, so it faces the wall, not the room (same axis, opposite sense)
    if(spec && spec.headToWall && pl.wall && pl.wall!=='free') face = OPPOSITE[face] || face;
    const o = Object.assign({ weather:ctx.weather, night:ctx.night }, opts||{});
    const rot0 = (face==='E'||face==='W');
    if(spec && spec.runBase!=null && o.len!=null && pl.fitRun!==false){
      const avail = ((pl.wall==='left'||pl.wall==='right') ? (r.y1-r.y0) : (r.x1-r.x0))
                    - 2*(pl.margin!=null?pl.margin:0.13);
      const maxLen = (avail - spec.runBase) / spec.runK;
      if(maxLen < o.len) o.len = Math.max(0, maxLen);
    }
    const fp = PI.footprint(name, o), rot = rot0;
    const fw = rot?fp.d:fp.w, fd = rot?fp.w:fp.d;
    // a room rect's edge is the CENTRELINE of the wall on it, so the default inset and margin have
    // to clear half a partition or every flush piece buries itself in the plaster
    const m = pl.margin!=null?pl.margin:0.13, ins = pl.inset!=null?pl.inset:0.12;
    const rw = r.x1-r.x0, rd = r.y1-r.y0;
    const onX = (pl.wall==='rear' || pl.wall==='front'), onY = (pl.wall==='left' || pl.wall==='right');
    const rej=(why)=>{ ctx.rejects.push({ prop:name, room:(pl.room||r.id||'?'), storey:ctx.storey, why }); return null; };
    if(onX && (fw > rw-2*m || fd > rd-0.10)) return rej('room too small: needs '+fw.toFixed(2)+'x'+fd.toFixed(2)+', room '+rw.toFixed(2)+'x'+rd.toFixed(2));
    if(onY && (fd > rd-2*m || fw > rw-0.10)) return rej('room too small: needs '+fw.toFixed(2)+'x'+fd.toFixed(2)+', room '+rw.toFixed(2)+'x'+rd.toFixed(2));
    const cand=(t, dIns)=>{
      const off = ins + (dIns||0);
      let px, py;
      if(onX){ px = r.x0+m+fw/2 + Math.max(0, rw-2*m-fw)*t;
               py = pl.wall==='rear' ? r.y0+off+fd/2 : r.y1-off-fd/2; }
      else if(onY){ py = r.y0+m+fd/2 + Math.max(0, rd-2*m-fd)*t;
                    px = pl.wall==='left' ? r.x0+off+fw/2 : r.x1-off-fw/2; }
      else { px = r.x0 + rw*(pl.fx!=null?pl.fx:0.5); py = r.y0 + rd*(pl.fy!=null?pl.fy:0.5); }
      px += pl.dx||0; py += pl.dy||0;
      return { cx:px, cy:py, x0:px-fw/2, x1:px+fw/2, y0:py-fd/2, y1:py+fd/2 };
    };
    let rect = cand(pl.along!=null?pl.along:0.5);
    const first = !pl.ignoreClear && blocked(ctx, rect);
    if(first){
      let ok=null;
      // slide along the wall first; if the whole wall is spoken for, pull the piece off it a little
      const pullOff = (spec && spec.headToWall) ? [0] : [0, 0.24, 0.46];
      if(onX||onY){ outerW: for(const dIns of pullOff){
          for(const t of [0.5, 0, 1, 0.25, 0.75, 0.12, 0.88, 0.38, 0.62]){
            const c=cand(t, dIns);
            if(c.x0<r.x0+0.03||c.x1>r.x1-0.03||c.y0<r.y0+0.03||c.y1>r.y1-0.03) continue;
            if(!blocked(ctx,c)){ ok=c; break outerW; } } } }
      else { const bx=pl.fx!=null?pl.fx:0.5, by=pl.fy!=null?pl.fy:0.5;
        outer: for(const dy of [0, -0.10, 0.10, -0.20, 0.20, -0.30, 0.30])
          for(const dx of [0, -0.10, 0.10, -0.18, 0.18]){
            const t=clamp(bx+dx,0.10,0.90), v=clamp(by+dy,0.10,0.90);
            const c={ cx:r.x0+rw*t+(pl.dx||0), cy:r.y0+rd*v+(pl.dy||0) };
            c.x0=c.cx-fw/2; c.x1=c.cx+fw/2; c.y0=c.cy-fd/2; c.y1=c.cy+fd/2;
            if(c.x0<r.x0+0.04||c.x1>r.x1-0.04||c.y0<r.y0+0.04||c.y1>r.y1-0.04) continue;
            if(!blocked(ctx,c)){ ok=c; break outer; } } }
      if(!ok) return rej('no clear slot — blocked by '+first);
      rect=ok;
    }
    const prefix = 'q'+(ctx.pn++)+'_';
    const em = PI.emit(name, o, { x:rect.cx, y:rect.cy, z, face, prefix });
    for(const fc of em.faces) out.push(fc);
    for(const k in em.mats) ctx.mats[k] = em.mats[k];
    rect.name=name; rect.face=face; rect.h=em.h;
    ctx.blockers.push({ kind:name, storey:ctx.storey, x0:r3(rect.x0), x1:r3(rect.x1), y0:r3(rect.y0), y1:r3(rect.y1), h:r3(em.h) });
    if(!pl.ignoreClear) ctx.taken.push(rect);
    return rect;
  }

  // try a list of walls in order; a piece that lands is not reported as unplaced
  function putAny(out, ctx, r, z, name, opts, walls, pl){
    const mark = ctx.rejects.length;
    for(const wl of walls){
      const got = put(out, ctx, r, z, name, opts, Object.assign({}, pl||{}, {wall:wl}));
      if(got){ ctx.rejects.length = mark; return got; }
    }
    return null;
  }

  function diningSet(out, ctx, r, b, z, sub){
    const K=b.T.kit;
    const tb = put(out,ctx,sub,z,'table',{len: b.tier==='luxury'?0.5:0.25, wood:K.wood},{wall:'free', fx:0.5, fy:0.5});
    if(!tb) return;
    const n = (tb.x1-tb.x0)>1.55?3:2;
    for(let i=0;i<n;i++){ const cx0=tb.x0+(tb.x1-tb.x0)*(i+0.5)/n;
      for(const s of [-1,1]){
        const cy0 = s<0 ? tb.y0-0.30 : tb.y1+0.30;
        if(cy0-0.24 < sub.y0-0.04 || cy0+0.24 > sub.y1+0.04) continue;
        put(out,ctx,{x0:cx0-0.30,x1:cx0+0.30,y0:cy0-0.24,y1:cy0+0.24}, z, 'chair',
            {wood:K.wood, variant: b.tier==='luxury'?1:0, fabric:K.fabric},{wall:'free', face: s<0?'S':'N', room:(r&&r.id)||'dining'});
        A(ctx,'dine', r.id, cx0, cy0, z+0.46, {seat:i+(s<0?'n':'s')});
      } }
  }

  function furnishRoom(out, ctx, r, b, z){
    const lux = b.tier==='luxury', K = b.T.kit, PI = root.PropIso;
    const clampX=(v)=>Math.max(r.x0+0.28, Math.min(r.x1-0.28, v));
    switch(r.kind){
      case 'living': {
        // open plan eats here, beside the kitchen, so the kitchen keeps its island
        if(r.open && (r.y1-r.y0)>5.0) diningSet(out,ctx,r,b,z,{ x0:r.x0, x1:r.x1, y0:r.y0+0.20, y1:r.y0+2.55 });
        if(K.seat==='sofa'){
          const sf=putAny(out,ctx,r,z,'sofa',{len:0.4, fabric:K.fabric, fabric2:K.fabric2, wood:K.wood},
                          r.open?['right','rear','left','front']:['rear','right','left'],
                          {along:0.80, inset:0.13});
          if(sf){ const n=Math.max(2, Math.round(Math.max(sf.x1-sf.x0, sf.y1-sf.y0)/0.72));
            const horiz=(sf.x1-sf.x0)>(sf.y1-sf.y0);
            for(let i=0;i<n;i++) A(ctx,'sit',r.id,
              horiz? sf.x0+(sf.x1-sf.x0)*(i+0.5)/n : sf.x0-0.34,
              horiz? sf.y1+0.34 : sf.y0+(sf.y1-sf.y0)*(i+0.5)/n, z+0.42, {seat:i}); }
        } else {
          const bn=put(out,ctx,r,z,'bench',{len:0.3, wood:'pine'},{wall:'rear', along:0.5, inset:0.12});
          if(bn) for(let i=0;i<2;i++) A(ctx,'sit',r.id, bn.x0+(bn.x1-bn.x0)*(i+0.5)/2, bn.y1+0.34, z+0.45, {seat:i});
        }
        const ac=put(out,ctx,r,z,'armchair',{fabric:K.fabric, fabric2:K.fabric2, wood:'walnut'},{wall:'left', along:0.86});
        if(ac) A(ctx,'sit',r.id, ac.x1+0.36, ac.cy, z+0.42, {seat:'armchair'});
        put(out,ctx,r,z,'rug',{len:lux?0.5:0.25, fabric:K.fabric, fabric2:K.fabric2},{wall:'free', fx:0.5, fy:r.open?0.74:0.42, ignoreClear:true});
        if(r.area>13.5) put(out,ctx,r,z,'roundTable',{wood:K.wood},{wall:'free', fx:0.46, fy:r.open?0.70:0.48, room:r.id});
        if(!lux) put(out,ctx,r,z,'shelf',{len:0.0, wood:'pine'},{wall:'front', along:0.88});
        break;
      }
      case 'kitchen': {
        const sw = PI?PI.footprint(K.cook,{}).w:1.2, rw = r.x1-r.x0;
        const cLen = Math.max(0, Math.min(1, (rw-0.34-sw-1.30)/1.5));
        const ct = put(out,ctx,r,z,'counter',{len:cLen, paint:K.paint, wood:'pine', worktop:K.worktop},{wall:'rear', along:0, margin:0.12});
        const st = put(out,ctx,r,z,K.cook,{wood:'oak'},{wall:'rear', along:1, margin:0.12});
        if(st) A(ctx,'cook',r.id, st.cx, st.y1+0.46, z);
        if(ct||st) decalY(out, r.y0, 1, r.x0+0.12, r.x1-0.12, z+0.92, z+(lux?1.50:1.42), 'wallTile', 0.22, subwayTex(), false, 0.05);
        const sk = put(out,ctx,r,z,K.sink,{paint:K.paint, wood:'pine', worktop:K.worktop},{wall:'left', along:0.20});
        if(sk) A(ctx,'wash_dishes',r.id, sk.x1+0.46, sk.cy, z);
        const fr = put(out,ctx,r,z,K.cold,{paint: lux?'steel':'white', wood:'oak'},{wall:'right', along:0.14});
        if(fr) A(ctx,'store',r.id, fr.x0-0.46, fr.cy, z, {what:K.cold});
        if(!lux) putAny(out,ctx,r,z,'hutch',{paint:'sage', wood:'pine', worktop:'butcher'},['right','left','front'],{along:0.66});
        const front = { x0:r.x0, x1:r.x1, y0:r.y0+0.72, y1:r.y1-0.50 };
        if(r.open){
          // island with stone worktop and stools; the dining table lives in the open living end
          const isl = put(out,ctx,front,z,'counter',{len:0.35, paint:K.paint, wood:'pine', worktop:'marble'},{wall:'free', fx:0.5, fy:0.34});
          if(isl){ const ns=(isl.x1-isl.x0)>2.2?3:2;
            for(let i=0;i<ns;i++){ const sx=isl.x0+(isl.x1-isl.x0)*(i+0.5)/ns;
              put(out,ctx,{x0:sx-0.28,x1:sx+0.28,y0:isl.y1+0.16,y1:isl.y1+0.64}, z, 'stool',{wood:K.wood},{wall:'free'});
              A(ctx,'dine',r.id, sx, isl.y1+0.40, z+0.48, {seat:'stool'+i}); } }
        } else diningSet(out,ctx,r,b,z,front);
        break;
      }
      case 'bed': {
        const variant = r.primary ? (lux?2:0) : (r.area<9.5?1:0);
        // the headboard wants a solid wall — an interior partition or a party wall — so the glazed
        // elevations are tried last and the bed ends up head-to-wall, usually in a corner
        const solid = ['rear','left','right','front'].filter(wl =>
          !(wl==='front' && r.winFront) && !(wl==='rear' && r.winRear));
        const glazed = ['rear','left','right','front'].filter(wl => !solid.includes(wl));
        const bd = putAny(out,ctx,r,z,'bed',{variant, wood:K.wood, fabric: lux?'sage':'blue', fabric2:'cream'},
                          solid.concat(glazed), {along:0.5, inset:0.18, room:r.id});
        if(bd){
          // approach from the long sides, whichever way the headboard ended up facing
          const alongY = (bd.y1-bd.y0) > (bd.x1-bd.x0);
          const size = ['double','single','wide'][variant];
          // ONE SLEEP ANCHOR PER SIDE YOU CAN ACTUALLY GET TO. A double used to claim both sides
          // whatever the room did, so a bed in a narrow back bedroom published a standing spot
          // inside the party wall and resident_slots counted someone who could never reach a bed.
          const NEED=0.60;
          const gap=(sd)=>alongY ? (sd==='left'? bd.x0-r.x0 : r.x1-bd.x1)
                                 : (sd==='left'? bd.y0-r.y0 : r.y1-bd.y1);
          let sides=(variant===1?['right']:['left','right']).filter(sd=>gap(sd)>=NEED);
          const pads=[];
          if(!sides.length){
            // an alcove bed: no side clears, so the FOOT is the way in and it sleeps one
            const fx=alongY ? (bd.x0+bd.x1)/2 : (r.x1-bd.x1>bd.x0-r.x0 ? bd.x1+0.36 : bd.x0-0.36);
            const fy=alongY ? (r.y1-bd.y1>bd.y0-r.y0 ? bd.y1+0.36 : bd.y0-0.36) : (bd.y0+bd.y1)/2;
            const px=Math.max(r.x0+0.30, Math.min(r.x1-0.30, fx));
            const py=Math.max(r.y0+0.30, Math.min(r.y1-0.30, fy));
            A(ctx,'sleep', r.id, px, py, z, {side:'foot', size, approach:'foot_only'});
            pads.push([px,py]);
          }
          for(const sd of sides){
            const ax = alongY ? clampX(sd==='left'? bd.x0-0.36 : bd.x1+0.36) : (bd.x0+bd.x1)/2;
            const ay = alongY ? bd.cy-0.10 : (sd==='left'? bd.y0-0.36 : bd.y1+0.36);
            const py = Math.max(r.y0+0.30, Math.min(r.y1-0.30, ay));
            A(ctx,'sleep', r.id, ax, py, z, {side:sd, size});
            pads.push([ax,py]);
          }
          // hold those pads: the wardrobe and the dresser come next and would otherwise stand on
          // the way in to the bed they are meant to serve
          reserveStand(ctx, pads, 0.26, 'the way in to the bed');
        }
        const wd = put(out,ctx,r,z,'wardrobe',{paint:K.paint, wood:'pine'},{wall:'right', along:0.16});
        if(wd){ A(ctx,'store', r.id, wd.x0-0.46, wd.cy, z, {what:'wardrobe'});
          reserveStand(ctx, [[wd.x0-0.46, wd.cy]], 0.26, 'the way in to the wardrobe'); }
        putAny(out,ctx,r,z,'dresser',{paint: lux?'sage':'blue', wood:'pine'},['left','front','right'],{along:0.24});
        // ROOM-SIZED RUG (manor precedent): a rug that runs the room, not a mat adrift on it
        if(lux) put(out,ctx,r,z,'rug',{len:Math.max(0,(r.x1-r.x0)-0.88), fabric:'sage', fabric2:'cream'},
          {wall:'free', fx:0.5, fy:0.56, ignoreClear:true});
        break;
      }
      case 'bath': case 'ensuite': {
        const wc = put(out,ctx,r,z,'toilet',{variant:K.wc},{wall:'rear', along:0.04});
        if(wc) A(ctx,'toilet', r.id, wc.cx, wc.y1+0.44, z);
        if(K.era==='modern'){
          const vn = putAny(out,ctx,r,z,'vanity',{len:(r.x1-r.x0)>2.3?0.6:0.0, paint:K.paint, wood:'pine', worktop:'marble'},['rear','right','left'],{along:1});
          if(vn){ const bn=(vn.x1-vn.x0)>1.25?2:1;
            for(let i=0;i<bn;i++) A(ctx,'vanity', r.id, vn.x0+(vn.x1-vn.x0)*(i+0.5)/bn, vn.y1+0.44, z, {basin:i}); }
          const sh = putAny(out,ctx,r,z,'shower',{worktop:'greentile'},['front','left','right'],{along:0.04});
          if(sh) A(ctx,'bathe', r.id, sh.cx, sh.y0-0.46, z, {fixture:'shower'});
          if((r.x1-r.x0)>2.90 && r.area>9.5) put(out,ctx,r,z,'tub',{variant:1},{wall:'front', along:1});
        } else {
          const ws = putAny(out,ctx,r,z,'washstand',{wood:'walnut'},['rear','right','left'],{along:1});
          if(ws) A(ctx,'vanity', r.id, ws.cx, ws.y1+0.44, z);
          const tb = putAny(out,ctx,r,z,'tub',{variant:0},['front','left','right'],{along:0.5});
          if(tb) A(ctx,'bathe', r.id, tb.cx, tb.y0-0.46, z, {fixture:'tub'});
          put(out,ctx,r,z,'towelRail',{wood:'pine', fabric:'white'},{wall:'left', along:0.5});
        }
        putAny(out,ctx,r,z,'mirror',{wood: K.era==='modern'?'driftwood':'walnut'},['right','left','front','rear'],{along:0.5});
        // a unit with no utility room keeps its washer in the bathroom
        if(r.kind==='bath' && K.wash==='washer' && !ctx.hasLaundryRoom && !ctx.washerPlaced){
          const wm=putAny(out,ctx,r,z,'washer',{variant:1, paint:'white'},['left','right','front'],{along:0.92});
          if(wm){ ctx.washerPlaced=true; A(ctx,'laundry', r.id, wm.cx, wm.y1+0.44, z); }
        }
        decalY(out, r.y0, 1, r.x0+0.05, r.x1-0.05, z+0.02, z+1.30, 'wallTile', 0.18, subwayTex(), false, 0.04);
        break;
      }
      case 'powder': {
        // a powder room is corridor-width, so the WC and the basin sit on OPPOSITE walls
        const wc=putAny(out,ctx,r,z,'toilet',{variant:K.wc},['rear','left','right'],{along:0.5, room:r.id});
        if(wc) A(ctx,'toilet', r.id, wc.cx, wc.y1+0.42, z);
        // corridor-width WC gets the wall-hung basin, a wider one the cabinet vanity
        const narrow = (r.x1-r.x0) < 1.55;
        const vn=putAny(out,ctx,r,z,'vanity',{variant: narrow?2:0, len:0.0, paint:K.paint, wood:'pine', worktop:'marble'},
                        ['front','right','left'],{along:0.5, room:r.id});
        if(vn) A(ctx,'vanity', r.id, vn.cx, vn.y0-0.42, z);
        break;
      }
      case 'laundry': {
        if(K.wash==='washer'){
          const stacked = r.area < 4.2;
          const wm = put(out,ctx,r,z,'washer',{variant: stacked?1:0, paint:'white'},{wall:'rear', along:0.04});
          if(wm) A(ctx,'laundry', r.id, wm.cx, wm.y1+0.44, z);
          if(!stacked) put(out,ctx,r,z,'washer',{variant:0, paint:'white'},{wall:'rear', along:0.40});
          putAny(out,ctx,r,z,'counter',{len:0.0, paint:K.paint, wood:'pine', worktop:'marble'},['rear','left','right','front'],{along:1, room:r.id});
        } else {
          const wt = put(out,ctx,r,z,'washtub',{wood:'oak'},{wall:'rear', along:0.15});
          if(wt) A(ctx,'laundry', r.id, wt.cx, wt.y1+0.44, z);
          put(out,ctx,r,z,'shelf',{len:0.0, wood:'pine'},{wall:'front', along:0.5});
        }
        break;
      }
      case 'closet': {
        put(out,ctx,r,z,'shelf',{len:Math.max(0,Math.min(1,((r.x1-r.x0)-1.1)/0.8)), wood:'pine'},{wall:'rear', along:0.5});
        put(out,ctx,r,z,'wardrobe',{paint:K.paint, wood:'pine'},{wall:'left', along:0.78});
        A(ctx,'store', r.id, (r.x0+r.x1)/2, r.y0+0.80, z, {what:'walkin'});
        break;
      }
      case 'study': {
        const tb=put(out,ctx,r,z,'table',{len:0.0, wood:K.wood},{wall:'front', along:0.24, inset:0.34});
        if(tb){ put(out,ctx,{x0:tb.cx-0.30,x1:tb.cx+0.30,y0:tb.y0-0.54,y1:tb.y0-0.08}, z, 'chair',
                    {wood:K.wood, variant:1, fabric:'sage'},{wall:'free', face:'S'});
          A(ctx,'desk', r.id, tb.cx, tb.y0-0.31, z+0.46); }
        put(out,ctx,r,z,'shelf',{len:0.4, wood:'pine'},{wall:'rear', along:0.5});
        put(out,ctx,r,z,'armchair',{fabric:K.fabric, fabric2:K.fabric2, wood:'walnut'},{wall:'right', along:0.76});
        break;
      }
      case 'store': {
        put(out,ctx,r,z,'shelf',{len:0.3, wood:'pine'},{wall:'left', along:0.5});
        put(out,ctx,r,z,'crate',{wood:'pine'},{wall:'rear', along:0.88});
        put(out,ctx,r,z,'barrel',{wood:'oak'},{wall:'rear', along:0.58});
        A(ctx,'store', r.id, (r.x0+r.x1)/2, (r.y0+r.y1)/2, z);
        break;
      }
      case 'hall': {
        if(r.stair || r.entry || r.pass) break;  // stair hall, vestibule, corridor: route, not room
        if(lux){ const cs=putAny(out,ctx,r,z,'table',{len:0.0, wood:'walnut'},['right','left'],{along:0.30});
          if(cs) A(ctx,'store', r.id, cs.x0-0.44, cs.cy, z, {what:'console'}); }
        else { const bn=putAny(out,ctx,r,z,'bench',{len:0.0, wood:'pine'},['right','left'],{along:0.30});
          if(bn) A(ctx,'sit', r.id, bn.x0-0.36, bn.cy, z+0.45, {seat:'hall'}); }
        if((r.y1-r.y0)>1.90) putAny(out,ctx,r,z,'mirror',{wood: lux?'driftwood':'walnut'},['right','left','front'],{along:0.74});
        break;
      }
      default: break;   // landing stays clear — it is the route, not a room
    }
  }

  function fStair(out, ctx, st, b, z, ceil, mat){
    const n=st.steps, run=(st.y1-st.y0)/n, rise=(ceil+b.sh.floorT)/n;
    for(let i=0;i<n;i++){
      const y1=st.y1-i*run, y0=y1-run, zz=z+rise*(i+1);
      P(out,ctx,{x0:st.x0,x1:st.x1, y0,y1, z0:Math.max(z, zz-rise-0.02), z1:zz, mat:mat||'floor', b:0.05, topB:0.4, blocker:false});
    }
    ctx.blockers.push({kind:'stair', storey:st.storey, x0:st.x0,x1:st.x1,y0:st.y0,y1:st.y1,h:r3(ceil)});
    // balusters every second tread, then a continuous raked handrail joining their tops — posts on
    // their own read as loose railings floating in the room
    const hx=st.x1+0.03, RH=0.90;
    for(let i=0;i<=n;i+=2){ const y=st.y1-i*run, zz=z+rise*(i+1);
      P(out,ctx,{x0:hx-0.028,x1:hx+0.028, y0:y-0.05,y1:y+0.01, z0:Math.max(z,zz-rise), z1:zz+RH, mat:'metal', blocker:false}); }
    const zBot=z+rise+RH, zTop=z+rise*n+RH;
    quad(out, [hx-0.035, st.y1, zBot], [hx+0.035, st.y1, zBot],
              [hx+0.035, st.y0, zTop], [hx-0.035, st.y0, zTop], 'metal', 0.55);
    quad(out, [hx+0.035, st.y1, zBot-0.055], [hx+0.035, st.y0, zTop-0.055],
              [hx+0.035, st.y0, zTop], [hx+0.035, st.y1, zBot], 'metal', 0.1);
    quad(out, [hx-0.035, st.y0, zTop], [hx-0.035, st.y0, zTop-0.055],
              [hx-0.035, st.y1, zBot-0.055], [hx-0.035, st.y1, zBot], 'metal', -0.25);
    A(ctx,'stair', 'stair_s'+st.storey, (st.x0+st.x1)/2, st.y1-0.25, z, {end:'bottom', storey:st.storey});
    A(ctx,'stair', 'stair_s'+st.storey, (st.x0+st.x1)/2, st.y0+0.25, z+(ceil+b.sh.floorT), {end:'top', storey:st.storey+1});
  }

  // the hole a flight needs in the plate above it — published by the stair itself, so the plan and
  // the cut-out can never disagree. It falls inside the landing band by construction.
  function stairVoid(st){
    if(!st) return null;
    return { x0:st.x0-0.05, x1:st.x1+0.05, y0:st.voidY0-0.05, y1:st.voidY1 };
  }
  // a floor slab with the stairwell cut out of it
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

  // ---- build ================================================================
  function build(b){
    const out=[], ctx={ blockers:[], anchors:[], openings:[], mats:{}, clear:[], taken:[], rejects:[], pn:0,
                        weather:b.weather, night:b.night };
    const B=camBasis({dir:b.dir, elev:b.elev});
    const sh=b.sh, T=b.T, lux=b.tier==='luxury';
    const wallT=sh.wallT, partyT=sh.partyWallT, partT=lux?0.13:0.11;
    const ceil=sh.ceilH, floorT=sh.floorT;
    const boardT=lux?boardTex(0.165):boardTex(0.145);
    const tileT=tileTex(lux?0.36:0.30), plasT=plasterTex(), weaveT=weaveTex();
    const nUnits=sh.units.length;
    // floorAtPivot only makes sense when ONE storey is shown; a stacked view would collapse.
    const zShift = (b.floorAtPivot && b.storeyList.length===1) ? -sh.storeyZ[b.storeyList[0]] : 0;

    for(let ui=0; ui<b.unitList.length; ui++){
      const u=b.unitList[ui], L=b.layouts[ui];
      const first=u.bay===0, last=u.bay===nUnits-1;
      const lt=first?wallT:partyT/2, rt=last?wallT:partyT/2;
      for(const s of b.storeyList){
        if(s>=u.storeys) continue;
        const zF=sh.storeyZ[s] + s*b.explode + zShift;   // floor top
        const zC=zF+ceil;
        const rooms=L.rooms.filter(r=>r.storey===s);
        if(!rooms.length) continue;
        const bx=L.box;

        // ---- the stairwell. The flight arriving from below needs a hole in THIS plate, and both
        // that hole and the flight leaving this storey have to stay clear of furniture.
        ctx.storey = s;
        const voids = [ stairVoid(L.stairs.find(v=>v.storey===s-1)) ].filter(Boolean);
        ctx.clear.length = 0; ctx.taken.length = 0;
        ctx.clearAll = ctx.clearAll || [];
        for(const v of voids) ctx.clear.push(Object.assign({tag:'the stairwell'}, v));
        ctx.voidsAll = ctx.voidsAll || [];
        for(const v of voids) ctx.voidsAll.push(Object.assign({ storey:s, unit:u.id }, v));
        const upFlight = L.stairs.find(v=>v.storey===s);
        if(upFlight) ctx.clear.push({ tag:'the flight', x0:upFlight.x0-0.05, x1:upFlight.x1+0.05, y0:upFlight.y0-0.05, y1:upFlight.y1+0.05 });
        // LANDINGS BEFORE FURNITURE (manor precedent): the departure apron of the flight leaving
        // this storey and the arrival apron of the one that reached it. The floor you step off a
        // stair onto is route, and a chest standing there is a stair nobody can use.
        const dnFlight = L.stairs.find(v=>v.storey===s-1);
        for(const f of [upFlight, dnFlight]) if(f&&f.landings)
          for(const l of f.landings) if(l.storey===s) ctx.clear.push(l);
        for(const p of L.parts){
          if(p.storey!==s) continue;
          const t=partT/2+0.02;
          if(p.axis==='x') ctx.clear.push({ tag:'a wall', x0:p.a0, x1:p.a1, y0:p.plane-t, y1:p.plane+t });
          else ctx.clear.push({ tag:'a wall', x0:p.plane-t, x1:p.plane+t, y0:p.a0, y1:p.a1 });
        }
        for(const c of ctx.clear) ctx.clearAll.push(Object.assign({storey:s}, c));
        for(const d of L.doors){
          if(d.storey!==s) continue;
          const half=d.clearW/2+0.06, dep=0.46;
          if(d.axis==='x') ctx.clear.push({ tag:'a doorway', x0:d.c-half, x1:d.c+half, y0:d.plane-dep, y1:d.plane+dep });
          else ctx.clear.push({ tag:'a doorway', x0:d.plane-dep, x1:d.plane+dep, y0:d.c-half, y1:d.c+half });
        }

        // ---- floor plate: joist band round the edge (sides only, so over the stairwell you see
        // through to the flight below), then one slab per room so the wet rooms tile
        wallQ(out, bx.x0-lt, bx.yF+wallT, bx.x1+rt, bx.yF+wallT, zF-floorT, zF-0.002, 'joist', null, -0.15);
        wallQ(out, bx.x1+rt, bx.yR-wallT, bx.x0-lt, bx.yR-wallT, zF-floorT, zF-0.002, 'joist', null, -0.15);
        wallQ(out, bx.x1+rt, bx.yF+wallT, bx.x1+rt, bx.yR-wallT, zF-floorT, zF-0.002, 'joist', null, -0.15);
        wallQ(out, bx.x0-lt, bx.yR-wallT, bx.x0-lt, bx.yF+wallT, zF-floorT, zF-0.002, 'joist', null, -0.15);
        for(const r of rooms){
          const wet = r.wet || r.kind==='bath' || r.kind==='ensuite' || r.kind==='powder' || (r.kind==='kitchen'&&!lux);
          slabMinus(out, r.x0,r.x1, r.y0,r.y1, zF, wet?'wet':'floor', 0.1, wet?tileT:boardT, voids);
        }

        // ---- perimeter walls: far walls full height, near walls cut to a lip
        const planes=[
          { nx:0, ny:1,  axis:'x', plane:bx.yF, t:wallT, out:+1 },   // front
          { nx:0, ny:-1, axis:'x', plane:bx.yR, t:wallT, out:-1 },   // rear
          { nx:-1,ny:0,  axis:'y', plane:bx.x0, t:lt,    out:-1 },   // left party
          { nx:1, ny:0,  axis:'y', plane:bx.x1, t:rt,    out:+1 },   // right party
        ];
        for(const p of planes){
          const near=facesCamera(p.nx,p.ny,B);
          const z1 = near ? zF+CUT_NEAR : zC;
          if(p.axis==='x'){
            const y0 = p.out>0 ? p.plane : p.plane-p.t, y1 = p.out>0 ? p.plane+p.t : p.plane;
            boxSolid(out, bx.x0-lt, bx.x1+rt, y0,y1, zF, z1, 'wall', plasT, 0.35, 0.3);
            ctx.blockers.push({kind:'wall', storey:s, x0:r3(bx.x0-lt),x1:r3(bx.x1+rt),y0:r3(y0),y1:r3(y1),h:r3(ceil)});
          } else {
            const x0 = p.out>0 ? p.plane : p.plane-p.t, x1 = p.out>0 ? p.plane+p.t : p.plane;
            boxSolid(out, x0,x1, bx.yR-wallT, bx.yF+wallT, zF, z1, 'wall', plasT, 0.35, 0.3);
            ctx.blockers.push({kind:'wall', storey:s, x0:r3(x0),x1:r3(x1),y0:r3(bx.yR-wallT),y1:r3(bx.yF+wallT),h:r3(ceil)});
          }
          // openings read on the inside face of the FAR walls only
          if(near) continue;
          if(p.axis==='x'){
            const inner = p.out>0 ? p.plane : p.plane;      // inside face
            const nrm = p.out>0 ? -1 : 1;                    // pointing into the room
            const touching = rooms.filter(r=> Math.abs((p.out>0? r.y1 : r.y0) - p.plane) < 0.02 );
            for(const r of touching){
              if(r.kind==='closet'||r.kind==='store') continue;
              const cxr=(r.x0+r.x1)/2, isEntry = p.out>0 && s===0 && r.kind==='hall';
              if(isEntry){
                const e=u.openings.entry;
                decalY(out, inner, nrm, e.x-e.clearW/2, e.x+e.clearW/2, zF, zF+e.clearH, 'join', 0.1, null, true, 0.06);
                decalY(out, inner, nrm, e.x-e.clearW/2+0.07, e.x+e.clearW/2-0.07, zF+0.12, zF+e.clearH-0.12, 'join', -0.5, null, true, 0.07);
                ctx.openings.push({kind:'entry', room:r.id, storey:s, x:r3(e.x), y:r3(p.plane), w:e.clearW, h:e.clearH});
                continue;
              }
              const balcony = lux && p.out>0 && s>0 && (r.kind==='bed'||r.kind==='living');
              const sill = balcony ? 0.06 : (lux?0.55:0.92), wh = balcony ? 2.25 : (lux?2.05:1.52);
              const ww = balcony ? 1.10 : Math.min(lux?2.0:1.05, (r.x1-r.x0)*0.52);
              decalY(out, inner, nrm, cxr-ww/2-0.09, cxr+ww/2+0.09, zF+sill-0.09, zF+sill+wh+0.09, 'wall', 0.55, null, true, 0.05);
              decalY(out, inner, nrm, cxr-ww/2, cxr+ww/2, zF+sill, zF+sill+wh, 'glass', b.night?0:0.9, null, true, 0.06);
              if(!balcony) decalY(out, inner, nrm, cxr-ww/2-0.12, cxr+ww/2+0.12, zF+sill-0.14, zF+sill, 'wall', 0.8, null, true, 0.05);
              ctx.openings.push({kind:balcony?'balcony_door':'window', room:r.id, storey:s, x:r3(cxr), y:r3(p.plane), w:r3(ww), h:r3(wh), sill:r3(sill)});
              if(balcony) A(ctx,'balcony', r.id, cxr, p.plane-0.5, zF);
            }
          } else {
            // party wall: no openings, but a skirting line keeps it from reading blank
            decalX(out, p.out>0?p.plane:p.plane, p.out>0?-1:1, bx.yR, bx.yF, zF+0.02, zF+0.13, 'wall', 0.5, null, true, 0.05);
          }
        }

        // ---- partitions + door leaves
        for(const p of L.parts) if(p.storey===s) drawPartition(out, ctx, p, zF, zF+CUT_PART, partT, 'wall');
        for(const d of L.doors){ if(d.storey!==s || d.kind==='entry' || d.kind==='open') continue;
          const inThis = rooms.some(r=>r.id===d.to)||rooms.some(r=>r.id===d.from);
          if(inThis) drawLeaf(out, d, zF, CUT_PART-0.10, 'join', partT); }

        // ---- furniture: every piece is a PropIso prop (see furnishRoom / put)
        for(const r of rooms){
          r.winFront = Math.abs(r.y1 - bx.yF) < 0.06;   // street elevation
          r.winRear  = Math.abs(r.y0 - bx.yR) < 0.06;   // rear elevation
        }
        ctx.hasLaundryRoom = L.rooms.some(v=>v.kind==='laundry');
        if(b.furnish) for(const r of rooms) furnishRoom(out, ctx, r, b, zF);

        // ---- stair for this storey
        const st=L.stairs.find(v=>v.storey===s);
        if(st) fStair(out,ctx,st,b,zF,ceil,'floor');
        if(s===0) A(ctx,'entry', 'g_hall', u.openings.entry.x, bx.yF+0.35, zF);
      }
    }
    return { faces:out, ctx };
  }

  // ---- post pass ------------------------------------------------------------
  function post(bufs, b){
    const { rbuf, ibuf, nbuf, dep } = bufs;
    const N=W*H, out=new Array(N).fill(null);
    for(let i=0;i<N;i++){ if(rbuf[i]) out[i]=rbuf[i][ibuf[i]]; }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){
      const i=y*W+x; if(!rbuf[i]) continue;
      for(const [dx,dy] of [[1,0],[0,1]]){
        const nx=x+dx, ny=y+dy; if(nx>=W||ny>=H) continue;
        const j=ny*W+nx; if(!rbuf[j]) continue;
        if(Math.abs(dep[i]-dep[j])>EDGE){ const far=dep[i]>dep[j]?i:j;
          out[far]=rbuf[far][Math.max(0,ibuf[far]-2)]; }
      }
    }
    if(b.night){
      // warm interior light spilling from the glass
      for(let y=1;y<H-1;y++) for(let x=1;x<W-1;x++){ const i=y*W+x;
        if(nbuf[i]==='glass'){ for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const j=(y+dy)*W+(x+dx);
          if(out[j] && nbuf[j]!=='glass') out[j]=mix(out[j],'#f0c66a',0.14); } } }
    }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!out[i]) continue;
      let n=0;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy;
        if(nx>=0&&nx<W&&ny>=0&&ny<H&&out[ny*W+nx]) n++; }
      if(n===0){ out[i]=null; rbuf[i]=null; }
    }
    if(b.outline){
      for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(out[i]) continue;
        let touch=false;
        for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy;
          if(nx>=0&&nx<W&&ny>=0&&ny<H&&rbuf[ny*W+nx]){ touch=true; break; } }
        if(touch) out[i]=KEY; }
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
    opts = (typeof opts==='number')?{elev:opts}:(opts||{});
    const b=resolve(Object.assign({}, opts, {dir}));
    if(!b){ if(root.console) console.warn('[RowhouseUnitIso] RowhouseIso not loaded — load Art/rowhouseIsoRig.js first.');
      return new Uint8ClampedArray(W*H*4); }
    const built=build(b);
    const MATS=Object.assign(makeMats(b), built.ctx.mats);
    return toRGBA(post(paint(built.faces, {dir, elev:b.elev}, MATS), b));
  }

  // ---- published metrics ----------------------------------------------------
  function dims(opts){
    const b=resolve(opts||{});
    if(!b) return { Wd:0, Ln:0, topZ:0 };
    const xs=b.unitList.map(u=>[u.interior.x0,u.interior.x1]).flat();
    const ys=b.unitList.map(u=>[u.interior.yRear,u.interior.yFront]).flat();
    const topS=b.storeyList[b.storeyList.length-1];
    const s0=b.storeyList[0], hh=heightsOf(b, s0);
    return { tier:b.tier, focus:b.focus, storeys:b.storeys, storeyList:b.storeyList.slice(),
      x0:r3(Math.min(...xs)), x1:r3(Math.max(...xs)), Wd:r3(Math.max(...xs)-Math.min(...xs)+2*b.sh.wallT),
      yRear:r3(Math.min(...ys)), yFront:r3(Math.max(...ys)), Ln:r3(Math.max(...ys)-Math.min(...ys)+2*b.sh.wallT),
      ceilH:r3(b.sh.ceilH), explode:b.explode,
      storey:s0, roomH:hh.roomH, storeyRise:hh.storeyRise,
      roomHByStorey:b.sh.storeyZ.map((_,s)=>heightsOf(b,s).roomH),
      storeyRiseByStorey:b.sh.storeyZ.map((_,s)=>heightsOf(b,s).storeyRise),
      floorT:b.sh.floorT, storeyZ:b.sh.storeyZ.slice(), floorAtPivot:b.floorAtPivot,
      baseZ:r3(b.sh.storeyZ[b.storeyList[0]] + b.storeyList[0]*b.explode),
      topZ:r3(b.sh.storeyZ[topS] + topS*b.explode + b.sh.ceilH) };
  }
  function plan(opts){
    const b=resolve(opts||{}); if(!b) return null;
    return { tier:b.tier, units:b.unitList.map((u,i)=>({ id:u.id, beds:u.beds, storeys:u.storeys,
      interior:u.interior, rooms:b.layouts[i].rooms, doors:b.layouts[i].doors, stairs:b.layouts[i].stairs })) };
  }
  function rooms(opts){
    const b=resolve(opts||{}); if(!b) return [];
    const list=[];
    b.unitList.forEach((u,i)=>{ for(const r of b.layouts[i].rooms){
      list.push({ unit:u.id, id:r.id, kind:r.kind, storey:r.storey, area:r.area,
        w:r3(r.x1-r.x0), d:r3(r.y1-r.y0), primary:!!r.primary, wet:!!(r.wet||r.kind==='bath'||r.kind==='ensuite'||r.kind==='powder') }); } });
    return list;
  }
  // the whole gameplay payload for this view
  function interior(opts){
    const o=opts||{};
    const b=resolve(o); if(!b) return null;
    const { ctx }=build(b);
    const seen={}, anchors=[];
    for(const a of ctx.anchors){ const k=a.verb+'|'+a.room+'|'+a.storey+'|'+a.x+'|'+a.y; if(seen[k]) continue; seen[k]=1; anchors.push(a); }
    return {
      contract:'32 px = 1 m · same origin, pivot and metres as RowhouseIso · +y street/front · z from ground',
      tier:b.tier, focus:b.focus, storeyList:b.storeyList.slice(),
      shell:{ wallT:b.sh.wallT, partyWallT:b.sh.partyWallT, floorT:b.sh.floorT, ceilH:b.sh.ceilH, storeyZ:b.sh.storeyZ },
      units: b.unitList.map((u,i)=>({
        id:u.id, bay:u.bay, beds:u.beds, storeys:u.storeys, interior:u.interior,
        bedsBuilt:b.layouts[i].bedsBuilt, bedsRequested:b.layouts[i].bedsRequested,
        rooms:b.layouts[i].rooms,
        thresholds:b.layouts[i].doors.map(d=>({ axis:d.axis, plane:d.plane, c:d.c, clearW:d.clearW,
          from:d.from, to:d.to, kind:d.kind })),
        stairs:b.layouts[i].stairs,
      })),
      openings:ctx.openings, anchors, blockers:ctx.blockers, unplaced:ctx.rejects,
      keepClear:ctx.clearAll||[], stairVoids:ctx.voidsAll||[],
      counts:{ rooms:rooms(o).length, anchors:anchors.length, blockers:ctx.blockers.length,
        verbs:anchors.reduce((m,a)=>{ m[a.verb]=(m[a.verb]||0)+1; return m; },{}) },
    };
  }
  function anchors(dir, opts){
    opts=opts||{};
    const b=resolve(Object.assign({},opts,{dir})); if(!b) return { anchors:[] };
    const B=camBasis({dir, elev:b.elev});
    const { ctx }=build(b);
    const pj=(x,y,z)=>{ const v=projVert(x,y,z,B); return { x:Math.round(v.sx), y:Math.round(v.sy) }; };
    const s0=b.storeyList[0], hh=heightsOf(b, s0);
    const zShift=(b.floorAtPivot && b.storeyList.length===1) ? -b.sh.storeyZ[s0] : 0;
    return { anchors: ctx.anchors.map(a=>Object.assign({}, a, pj(a.x,a.y,a.z))),
      openings: ctx.openings.map(o=>Object.assign({}, o, pj(o.x,o.y,(b.sh.storeyZ[o.storey]||0)+(o.sill||0)+ (o.h||1)/2))),
      storey:s0, roomH:hh.roomH, storeyRise:hh.storeyRise,
      storeyZ:b.sh.storeyZ[s0], floorAtPivot:b.floorAtPivot,
      floor: pj(0, 0, (b.sh.storeyZ[s0]||0)+zShift),
      dims: dims(opts) };
  }
  // ---- gameplay sections owned by the INTERIOR rig ---------------------------
  // The terrace wrote all of this inline, because it was the first multi-unit dwelling in the
  // folder and there was nothing to share with. There are three phases now, so the writer moved to
  // Art/_buildingGameplay.js and this rig calls it: one implementation of SOLE, THRESHOLD, STAIRS,
  // INTERACT, BLOCKERS and the reach audit for the terrace, the walk-up and the stack. Three
  // dialects of one schema was the thing worth preventing.
  //
  // RowhouseIso.gameplayAll() merges what comes back and stamps this file's hash beside its own.
  function gameplaySections(opts){
    const BG=root.BuildingGameplay; if(!BG) return null;
    const b=resolve(Object.assign({}, opts||{}, {focus:'all', explode:0, floorAtPivot:false}));
    if(!b) return null;
    const { ctx }=build(b);
    const sh=b.sh;
    const rooms=[], doors=[], stairs=[];
    // A terrace is a row of single-household houses: x alone identifies the unit, because a party
    // wall runs the full height and nobody's plate overlaps anybody else's.
    const unitAt=(x)=>b.unitList.find(v=>x>=v.interior.x0-0.01 && x<=v.interior.x1+0.01) ||
      b.unitList.map(v=>({v, d:Math.min(Math.abs(x-v.interior.x0), Math.abs(x-v.interior.x1))}))
                .sort((p,q)=>p.d-q.d).map(o=>o.v)[0] || null;
    const unitIdAt=(x)=>{ const u=unitAt(x); return u?u.id:null; };
    b.unitList.forEach((u,i)=>{
      const L=b.layouts[i];
      for(const r of L.rooms) rooms.push(Object.assign({}, r, {unit:u.id, uid:u.id+'.'+r.id}));
      for(const d of L.doors) doors.push(Object.assign({}, d, {unit:u.id}));
      for(const s of L.stairs) stairs.push(Object.assign({}, s, {unit:u.id,
        id:'stair_s'+s.storey }));
    });
    const blk=(ctx.blockers||[]).map(q=>Object.assign({ unit:unitIdAt((q.x0+q.x1)/2) }, q));
    const anch=(ctx.anchors||[]).map(a=>Object.assign({ unit:unitIdAt(a.x) }, a));
    return BG.sections({ rooms, doors, stairs, voids:ctx.voidsAll||[], blockers:blk, anchors:anch,
      rejects:ctx.rejects, storeyZ:sh.storeyZ, ceilH:sh.ceilH,
      roomH:(s)=>heightsOf(b,s).roomH, storeyRise:(s)=>heightsOf(b,s).storeyRise,
      unitOf:(x)=>unitIdAt(x), interiorRig:'Art/rowhouseUnitIsoRig.js' });
  }

  function project(dir, p, elev){ const v=projVert(p[0],p[1],p[2],camBasis({dir,elev})); return {x:v.sx,y:v.sy}; }

  root.RowhouseUnitIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:groundY}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'], TIERS, FINISHES, ROOMKINDS, VERBS, PRESETS, KEY,
    KEYLINE_DEFAULT, EXPLODE, dims, plan, rooms, interior, gameplaySections, render, anchors, project };
})(typeof globalThis!=='undefined'?globalThis:window);
