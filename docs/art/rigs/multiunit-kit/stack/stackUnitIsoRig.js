/* Hidden Harbours — parametric ISO STACKED-FLATS INTERIOR rig (ADR-0006 bake pipeline, SAME
   turntable + camera + shading as Art/stackFlatsIsoRig.js, so a plan and the shell it came out of
   composite pixel for pixel). MULTI-UNIT CLASS 3, pass 2.

   THIS RIG MEASURES NOTHING. Every wall plane, floor level, ceiling height, band depth, opening and
   glazed edge is read from StackFlatsIso.shell(opts) — the station precedent. It owns the ROOM
   PROGRAM and the FURNITURE, and nothing else.

   THE PROGRAM OF A STACKED FLAT (why it is not a corridor flat). A class-3 flat runs the whole
   depth of the plot, so it has three daylit faces and no internal corridor to hide service rooms
   in. The plan is therefore a SPINE, not a band sandwich:
     · a HALL spine down the party side, from a street window at the front to the cross hall — the
       flat's front door opens into it off the common stair landing
     · a full-width PARLOUR at the street, with the porch door (and, on a 3-bed wide plot, a front
       chamber taken off its outer corner)
     · a BEDROOM off the spine, lit from the side wall
     · a CROSS HALL, the one full-width room, which is what lets a three-room service band at the
       yard end all open off circulation instead of off each other
     · at the yard: KITCHEN on the party side with the back door onto the service porch, BATHROOM
       in the middle, a second BEDROOM (or the dining room, on a 1-bed) at the outer corner
   Every habitable room touches an outside wall, and every room is entered from the hall or the
   cross hall — never through another room. The one exception is the front chamber a 3-bed carves
   off the parlour, and it is published as such.

   BEDROOMS ARE BUILT, NOT PROMISED. A bedroom only exists where the plan can light it and fit it,
   so a flat builds as many as its width allows: the third bedroom needs a parlour band wide enough
   for a 3.3 m parlour AND a 2.45 m chamber, which only the one-flat-per-floor plot has.
   bedsBuilt is published beside bedsRequested rather than faking a windowless closet.

   NO WALL CLIPS A PROP. Placement goes through Art/_interiorPlacer.js: partitions, doorway zones,
   the flight and the stairwell void are pushed as per-storey keep-clear rects before anything is
   placed, and every rejection is recorded with its reason and published as `unplaced`. Orientation
   is explicit: each room carries which of its edges are GLAZED and which are SOLID, and headboards,
   counters and wardrobes are offered the solid walls in order — a bed is never asked to stand on a
   window, and a run-length piece shrinks to the wall it is given rather than refusing to appear.

   THE TWO STYLES, inside:
     porch     THREE-DECKER — wide pine boards, magnolia plaster with a picture rail, white bead
               board, and a PERIOD kitchen: cook stove, dry sink with a pump, icebox, dish hutch.
               Clawfoot tub and a high-cistern WC. No machine in the flat — the laundry is the
               shared room at the foot of the common stair, which is what puts it on an NPC route.
     gambrel   CAPTAIN'S FLATS — wide oak, lime plaster, bead-boarded facade wall, oak joinery, and
               a fitted kitchen: range, wet sink, fridge, marble worktops. Walk-in shower, basin
               vanity, and a laundry pair in the flat's own utility corner of the kitchen.

   Exposes globalThis.StackUnitIso = { W,H,PX,pivot,order,defaultElev, FINISHES,TIERS,ROOMKINDS,
     VERBS,PRESETS, dims(opts), plan(opts), rooms(opts), interior(opts), gameplaySections(opts),
     render(dir,opts), anchors(dir,opts), project(dir,p,elev) }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 1280, H = 1320, cx = 640, groundY = 960;
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;

  const FINISHES = {
    floorPine:    ['#5a4326','#6d5330','#80643c','#947a4d','#a89060','#bda676'],
    floorOakWide: ['#43301f','#5c4530','#74583d','#8c6d4d','#a3835f','#b99a75'],
    floorLino:    ['#3b3833','#4a4640','#5a554d','#6b655b','#7c7568','#8d8578'],
    wallMagnolia: ['#6f6a5c','#847e6d','#9a9382','#b0a897','#c5bdab','#d9d1bf'],
    wallPlaster:  ['#8f8e88','#a8a79f','#c0bfb6','#d5d4cb','#e6e5dd','#f3f2ec'],
    wallBead:     ['#7e857e','#959b92','#adb2a7','#c3c7bb','#d7dacd','#e8ebde'],
    tileWhiteSq:  ['#6a6f70','#7e8384','#939798','#a8acac','#bcc0c0','#d1d4d4'],
    terrazzo:     ['#6b6a64','#807e77','#95938b','#a9a79f','#bdbbb3','#d1cfc7'],
    joinWhite:    ['#8f948e','#a9ada5','#c2c5bc','#d7dad0','#e9ebe1','#f5f7ed'],
    joinOak:      ['#4a3722','#5f4830','#75593c','#8a6d4b','#9f815d','#b39672'],
    soffitBead:   ['#6d726c','#828779','#979b8c','#abaf9f','#bfc2b2','#d3d6c5'],
    concPolish:   ['#44484b','#54585c','#666a6e','#7a7e82','#8e9296','#a2a6aa'],
    metal:        ['#464d51','#5a6267','#727c81','#8c979c','#a6b1b5','#c2cccf'],
  };
  const GLASS_DAY   = ['#7d949b','#94aab0','#abbfc4','#c2d4d8'];
  const GLASS_NIGHT = ['#232831','#2a2f3a','#343a46','#414855'];
  const CAVITY = ['#0d1013','#141a1e','#1b2328','#232c32'];
  const KEY = '#1a1c22';

  const TIERS = {
    porch: {
      floor:'floorPine', wet:'tileWhiteSq', wall:'wallMagnolia', facadeWall:'wallMagnolia',
      join:'joinWhite', soffit:'soffitBead',
      openKitchen:false, shower:false, inSuiteLaundry:false, pictureRail:true, weather:0.24,
      kit:{ era:'period', wood:'pine', paint:'white', worktop:'butcher', fabric:'blue', fabric2:'cream',
            cold:'icebox', cook:'stove', sink:'sink', seat:'sofa', wc:0, tub:0 },
    },
    gambrel: {
      floor:'floorOakWide', wet:'terrazzo', wall:'wallPlaster', facadeWall:'wallBead',
      join:'joinOak', soffit:'soffitBead',
      openKitchen:false, shower:true, inSuiteLaundry:true, pictureRail:false, weather:0.07,
      kit:{ era:'modern', wood:'walnut', paint:'sage', worktop:'marble', fabric:'sage', fabric2:'cream',
            cold:'fridge', cook:'range', sink:'wetSink', seat:'sofa', wc:1, tub:1 },
    },
  };
  const ROOMKINDS = ['hall','crosshall','living','chamber','kitchen','bed','bath','dining','store',
                     'vestibule','landing','stairhall','laundry','backhall'];
  const VERBS = ['sleep','cook','wash_dishes','sit','dine','toilet','bathe','vanity','laundry',
                 'store','desk','entry','porch','stair','mail'];
  const PRESETS = {
    deckerFlat2:  { tier:'porch',   storeys:3, perFloor:2, beds:2, focus:'unit',  storey:0 },
    deckerFlat3:  { tier:'porch',   storeys:3, perFloor:1, beds:3, focus:'unit',  storey:1 },
    deckerFloor:  { tier:'porch',   storeys:3, perFloor:2, beds:2, focus:'floor', storey:1 },
    deckerGround: { tier:'porch',   storeys:3, perFloor:2, beds:2, focus:'floor', storey:0 },
    captainsFlat: { tier:'gambrel', storeys:2, perFloor:1, beds:3, focus:'unit',  storey:0 },
    captainsFloor:{ tier:'gambrel', storeys:3, perFloor:2, beds:2, focus:'floor', storey:1 },
    captainsStack:{ tier:'gambrel', storeys:3, perFloor:2, beds:2, focus:'all' },
  };

  const GAIN = 3.1, BIAS = 2.55, EDGE = 0.16;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  function mulberry32(a){return function(){a|=0;a=a+0x6D2B79F5|0;let t=Math.imul(a^a>>>15,1|a);t=t+Math.imul(t^t>>>7,61|t)^t;return((t^t>>>14)>>>0)/4294967296;};}
  function hex2rgb(h){ return [parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]; }
  function rgb2hex(r,g,b){ const h=(n)=>Math.max(0,Math.min(255,Math.round(n))).toString(16).padStart(2,'0'); return '#'+h(r)+h(g)+h(b); }
  function mix(a,b,t){ const A=hex2rgb(a),B=hex2rgb(b); return rgb2hex(A[0]+(B[0]-A[0])*t,A[1]+(B[1]-A[1])*t,A[2]+(B[2]-A[2])*t); }
  function desat(hex,t){ const [r,g,b]=hex2rgb(hex); const l=0.3*r+0.59*g+0.11*b; return rgb2hex(r+(l-r)*t,g+(l-g)*t,b+(l-b)*t); }
  function hash2(a,b){ let h=(a*374761393 + b*668265263)>>>0; h=(h^(h>>13))*1274126177>>>0; return ((h^(h>>16))>>>0)/4294967296; }
  function clamp(v,a,b){ return v<a?a:(v>b?b:v); }
  function r3(v){ return Math.round(v*1000)/1000; }

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

  function F(v,mat,b,db,uv,tex,flat){ return { v, mat, b:b||0, db:db||0, uv:uv||null, tex:tex||null, flat:!!flat }; }
  function wallQ(out, x0,y0,x1,y1, z0,z1, mat, tex, b){
    const L=Math.hypot(x1-x0,y1-y0);
    out.push(F([[x0,y0,z0],[x1,y1,z0],[x1,y1,z1],[x0,y0,z1]], mat, b||0, 0, [[0,z0],[L,z0],[L,z1],[0,z1]], tex));
  }
  function slab(out, pts, z, mat, b, tex){
    const uv = tex ? pts.map(p=>[p[0],p[1]]) : null;
    out.push(F(pts.map(p=>[p[0],p[1],z]), mat, b||0, 0, uv, tex));
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

  function boardTex(sp){ const SP=sp||0.19;
    return (u,v)=>{ const su=((v%SP)+SP)%SP;
      if(su<0.020) return -1;
      const t=hash2(Math.floor(u*4), Math.floor(v/SP)); return t<0.14?-1:(t>0.93?1:0); }; }
  function beadTex(){ const SP=0.10;
    return (u,v)=>{ const su=((u%SP)+SP)%SP;
      if(su<0.018) return -2; if(su>SP-0.016) return 1;
      return 0; }; }
  function tileTex(sp){ const SP=sp||0.20;
    return (u,v)=>{ const su=((u%SP)+SP)%SP, sv=((v%SP)+SP)%SP;
      if(su<0.024||sv<0.024) return -1;
      const t=hash2(Math.floor(u/SP), Math.floor(v/SP)); return t>0.90?1:0; }; }
  function terrazzoTex(){ return (u,v)=>{ const t=hash2(Math.floor(u*11), Math.floor(v*11));
      return t<0.16?-1:(t>0.86?1:0); }; }
  function linoTex(){ return (u,v)=>{ const t=hash2(Math.floor(u*5), Math.floor(v*5));
      return t<0.10?-1:(t>0.95?1:0); }; }
  function subwayTex(){ const BW=0.20, BH=0.098;
    return (u,v)=>{ const row=Math.floor(v/BH), off=(row&1)*BW*0.5;
      const f=((v%BH)+BH)%BH, su=(((u+off)%BW)+BW)%BW;
      return (f<0.016||su<0.016) ? -1 : 0; }; }
  function treadTex(){ return (u,v)=>{ const t=hash2(Math.floor(u*6), Math.floor(v*6)); return t<0.15?-1:0; }; }

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
          const d=w0*a.d+w1*b.d+w2*c.d, deff=d-f.db;
          const i=y*W+x;
          if(deff<zbuf[i]){
            zbuf[i]=deff; dep[i]=d; nbuf[i]=f.mat;
            let fi=fidx;
            if(tex&&uv){ const uu=w0*ua[0]+w1*ub[0]+w2*uc[0], vv=w0*ua[1]+w1*ub[1]+w2*uc[1]; fi += tex(uu,vv); }
            let idx;
            if(flat){ idx=Math.round(fi)+off; }
            else { const base=Math.floor(fi); idx=base+((fi-base)>BAYER[x&3][y&3]?1:0)+off; }
            idx=Math.max(0,Math.min(ramp.length-1,idx));
            rbuf[i]=ramp; ibuf[i]=idx;
          }
        }
      }
    }
    return { rbuf, ibuf, nbuf, dep };
  }

  // ================= resolve =================
  function resolve(opts){
    opts = opts||{};
    const EX = root.StackFlatsIso;
    if(!EX) throw new Error('StackUnitIso needs Art/stackFlatsIsoRig.js loaded first');
    const tier = (opts.tier==='gambrel') ? 'gambrel' : 'porch';
    const T = TIERS[tier];
    return {
      tier, T, sh: EX.shell(opts), dm: EX.dims(opts), opts,
      focus: opts.focus || 'unit',
      storeySel: opts.storey!=null ? opts.storey : 0,
      unitSel: opts.unit!=null ? opts.unit : 0,
      furnish: opts.furnish!==false,
      // Manor precedent: draw the shown level with its floor on the pivot row so the baker gets one
      // sprite per storey in a common frame. Every z drops by that storey's storeyZ, nothing shifts
      // sideways, and storeyZ itself does not change. Defaults false — every picture drawn before
      // this option existed stays byte-identical.
      floorAtPivot: !!opts.floorAtPivot,
      partT: 0.11, cutFrac: 0.60,
      weather: opts.weather!=null ? opts.weather : T.weather,
      night: !!opts.night,
      elev: opts.elev!=null ? opts.elev : DEFAULT_ELEV,
    };
  }
  function makeMats(b){
    const T=b.T, wx=b.weather, night=b.night;
    const wth=(ramp)=>ramp.map(c=>{ let x=desat(c, wx*0.34); x=mix(x,'#6b675e',wx*0.18);
      if(night) x=mix(x,'#2a2216',0.20); return x; });
    return {
      floor:{ramp:wth(FINISHES[T.floor])}, wet:{ramp:wth(FINISHES[T.wet])},
      wall:{ramp:wth(FINISHES[T.wall])}, facadeWall:{ramp:wth(FINISHES[T.facadeWall])},
      wallTile:{ramp:wth(FINISHES.tileWhiteSq)}, join:{ramp:wth(FINISHES[T.join])},
      soffit:{ramp:wth(FINISHES[T.soffit])}, metal:{ramp:wth(FINISHES.metal)},
      conc:{ramp:wth(FINISHES.concPolish)}, lino:{ramp:wth(FINISHES.floorLino)},
      glass:{ramp: night?GLASS_NIGHT:GLASS_DAY, off: night?0:1},
      cavity:{ramp:CAVITY},
    };
  }

  // ================= the flat program =================
  // Measured from shell(): u runs from the flat's OUTER wall toward the party side, v from the
  // street wall toward the yard. Nothing here knows a metre the shell did not publish.
  const MIN_LIVING_W = 3.30, MIN_BED_W = 2.45, MIN_CHAMBER = 2.45;
  function layoutFlat(b, U){
    const I=U.interior, N=U.bands, T=b.T;
    const hs = I.hallSide, w = I.w, d = I.d;
    const DW = b.tier==='gambrel' ? 0.88 : 0.82;
    const X=(u)=> hs>0 ? I.x0 + u : I.x1 - u;          // u: 0 at the outer wall
    const Y=(v)=> I.yStreet - v;                        // v: 0 at the street wall
    const rect=(u0,u1,v0,v1)=>({ x0:r3(Math.min(X(u0),X(u1))), x1:r3(Math.max(X(u0),X(u1))),
      y0:r3(Math.min(Y(v0),Y(v1))), y1:r3(Math.max(Y(v0),Y(v1))) });
    // which room edge is which, in the placer's wall words
    const outerWall = hs>0 ? 'left' : 'right';          // the glazed side wall
    const spineWall = hs>0 ? 'right' : 'left';          // the party wall the hall runs down
    const streetWall = 'front', yardWall = 'rear';      // +y is the street

    const sw=N.hallW, pD=N.parlourD, mD=N.bedD, cD=N.crossD, rD=N.serviceD;
    const kitW=N.kitW, bathW=N.bathW;
    const outerW = w - sw;                              // the parlour band's width

    // a third bedroom only exists where the parlour band can seat a parlour AND a chamber
    const wantChamber = U.beds>=3 && outerW >= MIN_LIVING_W + MIN_CHAMBER;
    const chamberW = wantChamber ? clamp(outerW - MIN_LIVING_W, MIN_CHAMBER, 3.15) : 0;
    const livW = outerW - chamberW;
    const bed2Kind = U.beds>=2 ? 'bed' : 'dining';
    const bedsBuilt = 1 + (U.beds>=2?1:0) + (wantChamber?1:0);

    const rooms=[], parts=[], doors=[];
    const room=(id,kind,extra,u0,u1,v0,v1)=>{
      const r=Object.assign({ id:U.id+'_'+id, kind, unit:U.id, storey:U.storey,
        outerWall, spineWall, streetWall, yardWall }, rect(u0,u1,v0,v1), extra||{});
      r.area=r3((r.x1-r.x0)*(r.y1-r.y0)); rooms.push(r); return r; };
    const part=(axis,plane,a0,a1,gaps)=>parts.push({ axis, plane:r3(plane),
      a0:r3(Math.min(a0,a1)), a1:r3(Math.max(a0,a1)), gaps:gaps||[], storey:U.storey, unit:U.id });
    const partU=(u, v0,v1, gaps)=>part('y', X(u), Math.min(Y(v0),Y(v1)), Math.max(Y(v0),Y(v1)), gaps);
    const partV=(v, u0,u1, gaps)=>part('x', Y(v), Math.min(X(u0),X(u1)), Math.max(X(u0),X(u1)), gaps);
    const gapU=(v0,v1)=>[Math.min(Y(v0),Y(v1)), Math.max(Y(v0),Y(v1))];
    const gapV=(u0,u1)=>[Math.min(X(u0),X(u1)), Math.max(X(u0),X(u1))];
    const door=(axis,plane,c,clearW,from,to,kind)=>doors.push({ axis, plane:r3(plane), c:r3(c),
      clearW, from:U.id+'_'+from, to:(to&&to.indexOf('_')===0?to.slice(1):U.id+'_'+to),
      kind:kind||'door', storey:U.storey, unit:U.id });
    const doorU=(u, v, cw, from,to,kind)=>door('y', X(u), Y(v), cw, from,to,kind);
    const doorV=(v, u, cw, from,to,kind)=>door('x', Y(v), X(u), cw, from,to,kind);

    // ---- rooms ----------------------------------------------------------------
    // the hall spine: street window at its head, the flat's front door in the party wall
    const hall = room('hall','hall',{ route:true, entry:true, glazed:{street:true},
      solid:[spineWall, yardWall] }, outerW, w, 0, pD+mD);
    // the parlour, and the front chamber a wide 3-bed carves off its street corner
    const living = room('living','living',{ glazed:{street:true, outer:!wantChamber},
      solid:[spineWall, yardWall].concat(wantChamber?[outerWall]:[]) },
      chamberW, outerW, 0, pD);
    let chamber=null;
    if(wantChamber) chamber = room('bed3','bed',{ primary:false, chamber:true,
      glazed:{street:true, outer:true}, solid:[yardWall, hs>0?'right':'left'] }, 0, chamberW, 0, pD);
    // the bedroom off the spine, lit from the side wall
    const bed1 = room('bed1','bed',{ primary:true, glazed:{outer:true},
      solid:[spineWall, streetWall, yardWall] }, 0, outerW, pD, pD+mD);
    // the one full-width room: what lets the yard band open off circulation
    const cross = room('crosshall','crosshall',{ route:true }, 0, w, pD+mD, pD+mD+cD);
    // the yard band: kitchen at the party side with the back door, bath in the middle,
    // second bedroom (or dining) at the outer corner. The kitchen's own door is in its STREET
    // wall, so the wall it fits out along is the bathroom party wall, not that one.
    const kitchen = room('kitchen','kitchen',{ glazed:{yard:true},
      solid:[spineWall, outerWall], rearDoor:true }, w-kitW, w, pD+mD+cD, d);
    const bath = room('bath','bath',{ wet:true, glazed:{yard:true},
      solid:[outerWall, spineWall] }, w-kitW-bathW, w-kitW, pD+mD+cD, d);
    const bed2 = room(bed2Kind==='bed'?'bed2':'dining', bed2Kind,
      { primary:false, glazed:{yard:true, outer:true}, solid:[spineWall, streetWall] },
      0, w-kitW-bathW, pD+mD+cD, d);

    // ---- partitions. Every one carries its storey; an unfiltered partition reserves space on
    // floors it is not on, which is how walls end up drawn through beds.
    const livDoorV = pD*0.56, bedDoorV = pD + mD*0.52;
    partU(outerW, 0, pD+mD, [ gapU(livDoorV-DW/2, livDoorV+DW/2),
                              gapU(bedDoorV-DW/2, bedDoorV+DW/2) ]);
    doorU(outerW, livDoorV, DW, 'hall','living');
    doorU(outerW, bedDoorV, DW, 'hall','bed1');
    if(chamber){
      const cd = pD*0.62;
      partU(chamberW, 0, pD, [ gapU(cd-DW/2, cd+DW/2) ]);
      doorU(chamberW, cd, DW, 'living','bed3');
    }
    // parlour / bedroom wall, solid — the bedroom is entered from the hall
    partV(pD, 0, outerW, []);
    // bedroom / cross hall wall, solid; the hall meets the cross hall in the open
    partV(pD+mD, 0, outerW, []);
    doorV(pD+mD, w-sw/2, Math.min(sw-0.14, 1.9), 'hall','crosshall','open');
    // cross hall / yard band: a door into each of the three rooms
    const kc = w-kitW/2, bc = w-kitW-bathW/2, dc = (w-kitW-bathW)/2;
    partV(pD+mD+cD, 0, w, [ gapV(kc-DW/2,kc+DW/2), gapV(bc-DW/2,bc+DW/2), gapV(dc-DW/2,dc+DW/2) ]);
    doorV(pD+mD+cD, kc, DW, 'crosshall','kitchen');
    doorV(pD+mD+cD, bc, DW, 'crosshall','bath');
    doorV(pD+mD+cD, dc, DW, 'crosshall', bed2Kind==='bed'?'bed2':'dining');
    // kitchen / bath / bedroom party lines in the yard band, solid
    partU(w-kitW, pD+mD+cD, d, []);
    partU(w-kitW-bathW, pD+mD+cD, d, []);

    // ---- the doors in the shell: the front door off the landing, the porch and back doors
    doors.push({ axis:'y', plane:r3(U.openings.entry.x), c:r3(U.openings.entry.y),
      clearW:U.openings.entry.clearW, from:(U.storey===0?'vestibule_s0':'landing_s'+U.storey),
      to:U.id+'_hall', kind:'entry', storey:U.storey, unit:U.id });
    if(U.openings.porchDoor) doors.push({ axis:'x', plane:r3(U.openings.porchDoor.y),
      c:r3(U.openings.porchDoor.x), clearW:U.openings.porchDoor.clearW,
      from:U.id+'_hall', to:'front_porch_s'+U.storey, kind:'porch', storey:U.storey, unit:U.id });
    if(U.openings.rearDoor) doors.push({ axis:'x', plane:r3(U.openings.rearDoor.y),
      c:r3(U.openings.rearDoor.x), clearW:U.openings.rearDoor.clearW,
      from:U.id+'_kitchen', to:'rear_porch_s'+U.storey, kind:'porch', storey:U.storey, unit:U.id });

    return { rooms, parts, doors, bedsBuilt, bedsRequested:U.beds, baths:1,
      chamber:!!chamber, throughPlan:true,
      split:{ hallW:r3(sw), parlourW:r3(livW), chamberW:r3(chamberW), bedW:r3(outerW),
              kitW:r3(kitW), bathW:r3(bathW), bed2W:r3(w-kitW-bathW),
              parlourD:r3(pD), bedD:r3(mD), crossD:r3(cD), serviceD:r3(rD) },
      box:{ x0:I.x0, x1:I.x1, yStreet:I.yStreet, yRear:I.yRear, w, d, hs },
      keyRooms:{ hall:hall.id, living:living.id, bed1:bed1.id, cross:cross.id,
                 kitchen:kitchen.id, bath:bath.id, bed2:bed2.id } };
  }

  // ================= the common stair bay =================
  function layoutCore(b, storey){
    const sh=b.sh, C=sh.core;
    const rooms=[], parts=[], doors=[], DW=b.tier==='gambrel'?0.92:0.86;
    const room=(id,kind,extra,y0,y1)=>{ const r=Object.assign({ id:id+'_s'+storey, kind, core:true,
      storey, x0:r3(C.x0), x1:r3(C.x1), y0:r3(Math.min(y0,y1)), y1:r3(Math.max(y0,y1)) }, extra||{});
      r.area=r3((r.x1-r.x0)*(r.y1-r.y0)); rooms.push(r); return r; };
    const part=(plane,gaps)=>parts.push({ axis:'x', plane:r3(plane), a0:r3(C.x0), a1:r3(C.x1),
      gaps:gaps||[], storey, core:true });

    if(storey===0) room('vestibule','vestibule',{ route:true, entry:true, mail:true,
      solid:['left','right'] }, C.vestibule.y0, C.vestibule.y1);
    else room('landing','landing',{ route:true, solid:['left','right'] }, C.vestibule.y0, C.vestibule.y1);
    room('stairhall','stairhall',{ route:true }, C.stair.y0, C.stair.y1);
    room('backhall','backhall',{ route:true, meters:true }, C.backhall.y0, C.backhall.y1);
    if(storey===0) room('laundry','laundry',{ wet:true, shared:true, solid:['left','right','front'] },
      C.laundry.y0, C.laundry.y1);
    else room('store','store',{ shared:true, solid:['left','right','front'] }, C.laundry.y0, C.laundry.y1);

    // vestibule / stair hall and stair hall / back hall are open thresholds — you walk through
    doors.push({ axis:'x', plane:r3(C.vestibule.y0), c:r3((C.x0+C.x1)/2),
      clearW:Math.min(2.1, C.x1-C.x0-0.35), from:(storey===0?'vestibule_s0':'landing_s'+storey),
      to:'stairhall_s'+storey, kind:'open', storey, core:true });
    doors.push({ axis:'x', plane:r3(C.stair.y0), c:r3((C.x0+C.x1)/2),
      clearW:Math.min(1.9, C.x1-C.x0-0.5), from:'stairhall_s'+storey, to:'backhall_s'+storey,
      kind:'open', storey, core:true });
    // the back hall to the shared laundry (ground) or the store (above)
    const lc = C.x0 + 0.55 + DW/2;
    part(C.backhall.y0, [[C.x0+0.55, C.x0+0.55+DW]]);
    doors.push({ axis:'x', plane:r3(C.backhall.y0), c:r3(lc), clearW:DW,
      from:'backhall_s'+storey, to:(storey===0?'laundry_s0':'store_s'+storey),
      kind:'door', storey, core:true });
    // the street door, and the back door onto the service porch
    if(storey===0){
      doors.push({ axis:'x', plane:r3(C.frontDoor.y), c:r3(C.frontDoor.x), clearW:C.frontDoor.clearW,
        from:'street', to:'vestibule_s0', kind:'entry', storey:0, core:true });
      if(C.rearDoor) doors.push({ axis:'x', plane:r3(C.rearDoor.y), c:r3(C.rearDoor.x),
        clearW:C.rearDoor.clearW, from:'laundry_s0', to:'rear_porch_s0', kind:'entry',
        storey:0, core:true });
    } else if(C.rearStair){
      doors.push({ axis:'x', plane:r3(sh.block.yRear), c:r3(C.rearDoor?C.rearDoor.x:(C.x0+C.x1)/2),
        clearW:0.90, from:'store_s'+storey, to:'rear_porch_s'+storey, kind:'entry', storey, core:true });
    }
    return { rooms, parts, doors };
  }

  // one straight flight per storey, in the stair hall, rising toward the street
  // EXACT FLIGHT: the rise published is the unrounded riseTotal/steps the drawing already climbs,
  // so steps x rise === floorRise to the bit and the top tread lands ON the plate above it. The
  // rounded figure lost half a millimetre a step — the manor's 18 x 0.197 = 3.546-vs-3.55 defect.
  function coreStair(b, storey){
    const C=b.sh.core, sh=b.sh;
    const STEPS=16, span=(C.stair.y1-C.stair.y0-0.34);
    const run=span/STEPS;
    // a flight is a flight, not the width of the hall: 1.10 m of treads against the party side,
    // and the rest of the stair hall stays walkable landing
    const x0=C.x0+0.16, x1=Math.min(C.x1-0.16, C.x0+1.26);
    const floorRise=sh.ceilH+sh.floorT;
    const y0=C.stair.y0+0.17, y1=y0+STEPS*run;
    const f={ storey, x0:r3(x0), x1:r3(x1), y0:r3(y0), y1:r3(y1),
      steps:STEPS, rise:floorRise/STEPS, run:r3(run), floorRise,
      voidY0:r3(y0+STEPS*run*0.42), voidY1:r3(y1),
      dir:'rises_to_street', core:true };
    f.landings = [
      { tag:'the bottom landing', end:'bottom', storey, x0:r3(x0-0.10), x1:r3(x1+0.10),
        y0:r3(f.y0-1.05), y1:f.y0 },
      { tag:'the top landing', end:'top', storey:storey+1, x0:r3(x0-0.10), x1:r3(x1+0.10),
        y0:f.y1, y1:r3(f.y1+1.05) },
    ];
    return f;
  }

  // ================= plan =================
  function planAll(b){
    const sh=b.sh, layouts={}, cores={}, stairs=[];
    for(const U of sh.units) layouts[U.id]=layoutFlat(b,U);
    for(let s=0;s<sh.storeys;s++){ cores[s]=layoutCore(b,s); if(s<sh.storeys-1) stairs.push(coreStair(b,s)); }
    return { layouts, cores, stairs };
  }
  function storeysShown(b){
    const n=b.sh.storeys;
    if(b.focus==='all' || b.storeySel==='stack') return Array.from({length:n},(_,i)=>i);
    return [clamp(Math.round(+b.storeySel||0), 0, n-1)];
  }
  function unitsShown(b){
    const sh=b.sh, ss=storeysShown(b);
    if(b.focus==='unit'){
      const pool=sh.units.filter(u=>ss.indexOf(u.storey)>=0);
      const list=pool.length?pool:sh.units;
      return [list[clamp(Math.round(b.unitSel), 0, list.length-1)]];
    }
    return sh.units.filter(u=>ss.indexOf(u.storey)>=0);
  }

  // ================= build =================
  function build(b){
    const PL = root.InteriorPlacer && root.InteriorPlacer.create({ boxSolid, slab, r3, clamp });
    const out=[], sh=b.sh, T=b.T, lux=b.tier==='gambrel';
    const ctx={ blockers:[], anchors:[], openings:[], mats:{}, clear:[], taken:[], rejects:[],
                clearAll:[], voidsAll:[], pn:0, storey:0, weather:b.weather, night:b.night };
    if(!PL) return { faces:out, ctx, plan:planAll(b) };
    const P=planAll(b);
    const ss=storeysShown(b), us=unitsShown(b);
    const showCore = b.focus!=='unit';
    const CUT = sh.ceilH*b.cutFrac, partT=b.partT, floorT=sh.floorT;
    const boardT = boardTex(lux?0.21:0.24);
    const wetT   = lux?terrazzoTex():tileTex(0.20);
    const hardT  = lux?terrazzoTex():tileTex(0.26);
    // PER-ROOM MATERIAL CONTRAST (manor precedent). The flat already changed material for its wet
    // rooms and its service band; the shared bay now does too — a vestibule, a stair hall and a
    // landing are not a parlour and should not be finished like one. No hue is invented: every
    // ramp here is already in the tier's own table.
    const floorMatOf=(r)=>{
      if(r.wet || r.kind==='bath' || r.kind==='laundry') return ['wet', wetT];
      if(r.kind==='kitchen' || r.kind==='backhall') return lux ? ['floor', boardT] : ['lino', linoTex()];
      if(r.kind==='vestibule' || r.kind==='stairhall' || r.kind==='landing')
        return ['conc', hardT];
      return ['floor', boardT];
    };
    // floorAtPivot only makes sense when ONE storey is shown; a stacked view would collapse.
    const zShift = (b.floorAtPivot && ss.length===1) ? -sh.storeyZ[ss[0]] : 0;

    for(const s of ss){
      ctx.storey=s;
      ctx.clear.length=0; ctx.taken.length=0;
      const zF = sh.storeyZ[s] + zShift, zC = zF + sh.ceilH;
      const core=P.cores[s];
      const flight=P.stairs.find(v=>v.storey===s);
      const below=P.stairs.find(v=>v.storey===s-1);
      const voids=[];
      if(below && showCore) voids.push(PL.stairVoid(below));
      for(const v of voids) if(v) ctx.voidsAll.push(Object.assign({storey:s}, v));

      const rooms=[];
      for(const U of us) if(U.storey===s) rooms.push(...P.layouts[U.id].rooms);
      if(showCore) rooms.push(...core.rooms);

      const parts=[], doors=[];
      for(const U of us) if(U.storey===s){ parts.push(...P.layouts[U.id].parts); doors.push(...P.layouts[U.id].doors); }
      if(showCore){ parts.push(...core.parts); doors.push(...core.doors); }

      // keep-clear, and THE LANDINGS BEFORE THE FURNITURE. The floor you step off a flight onto is
      // route: a dresser standing on it is a stair nobody can use. Manor precedent — landings are
      // reserved before anything is furnished, not fitted around afterwards.
      const landings=[];
      if(showCore){
        if(flight) landings.push(flight.landings.find(l=>l.end==='bottom'));
        if(below)  landings.push(below.landings.find(l=>l.end==='top'));
      }
      PL.clearFor(ctx, { parts, doors, partT, landings,
        voids: (below&&showCore)?[PL.stairVoid(below)]:[], flights:(flight&&showCore)?[flight]:[] });
      for(const c of ctx.clear) ctx.clearAll.push(Object.assign({storey:s}, c));

      // ---- floor plates, one per room, each in its own material
      for(const r of rooms){
        const [fm, ft] = floorMatOf(r);
        PL.slabMinus(out, r.x0,r.x1, r.y0,r.y1, zF, fm, 0.10, ft, voids);
      }
      // joist band under the shown plate, so a floating plan reads as a floor
      const ext = rooms.reduce((a,r)=>({ x0:Math.min(a.x0,r.x0), x1:Math.max(a.x1,r.x1),
        y0:Math.min(a.y0,r.y0), y1:Math.max(a.y1,r.y1) }), {x0:1e9,x1:-1e9,y0:1e9,y1:-1e9});
      wallQ(out, ext.x0, ext.y1, ext.x1, ext.y1, zF-floorT, zF-0.002, 'soffit', null, -0.15);
      wallQ(out, ext.x1, ext.y0, ext.x0, ext.y0, zF-floorT, zF-0.002, 'soffit', null, -0.15);
      wallQ(out, ext.x1, ext.y1, ext.x1, ext.y0, zF-floorT, zF-0.002, 'soffit', null, -0.15);
      wallQ(out, ext.x0, ext.y0, ext.x0, ext.y1, zF-floorT, zF-0.002, 'soffit', null, -0.15);

      // ---- the outside walls, inside face. The shingle tier bead-boards its street wall.
      for(const U of us) if(U.storey===s){
        const bx=P.layouts[U.id].box, hs=bx.hs;
        const x0=Math.min(bx.x0,bx.x1), x1=Math.max(bx.x0,bx.x1);
        decalY(out, bx.yStreet, -1, x0, x1, zF, zF+CUT, 'facadeWall', 0.15, lux?beadTex():null, false, 0.02);
        decalY(out, bx.yRear,    1, x0, x1, zF, zF+CUT, 'wall', 0.12, null, false, 0.02);
        const ox = hs>0 ? x0 : x1;
        decalX(out, ox, hs>0?1:-1, bx.yRear, bx.yStreet, zF, zF+CUT, 'wall', 0.10, null, false, 0.02);
        // picture rail on the porch tier: a line that says plaster, not plasterboard
        if(T.pictureRail && CUT>1.9){
          decalY(out, bx.yStreet, -1, x0, x1, zF+1.86, zF+1.94, 'join', 0.30, null, true, 0.05);
          decalX(out, ox, hs>0?1:-1, bx.yRear, bx.yStreet, zF+1.86, zF+1.94, 'join', 0.30, null, true, 0.05);
        }
      }
      if(showCore){
        const C=sh.core;
        decalY(out, C.yStreet, -1, C.x0, C.x1, zF, zF+CUT, 'facadeWall', 0.15, lux?beadTex():null, false, 0.02);
        decalY(out, C.yRear,    1, C.x0, C.x1, zF, zF+CUT, 'wall', 0.12, null, false, 0.02);
        if(sh.perFloor===1) decalX(out, C.x1, -1, C.yRear, C.yStreet, zF, zF+CUT, 'wall', 0.10, null, false, 0.02);
      }

      // ---- partitions + door leaves, this storey only
      for(const p of parts) if(p.storey===s) PL.drawPartition(out, ctx, p, zF, zF+CUT, partT, 'wall', 0.22, 0.16);
      for(const d of doors) if(d.storey===s && d.kind!=='entry' && d.kind!=='open' && d.kind!=='porch')
        PL.drawLeaf(out, d, zF, CUT-0.10, 'join', partT);

      // ---- furniture
      if(b.furnish){
        for(const U of us) if(U.storey===s){
          const L=P.layouts[U.id];
          for(const r of L.rooms) furnishFlat(out, ctx, PL, r, b, zF, U, L);
        }
        if(showCore) for(const r of core.rooms) furnishCore(out, ctx, PL, r, b, zF);
      }

      // ---- the flight, and the routes an NPC needs
      if(showCore){
        if(flight) drawFlight(out, ctx, PL, flight, b, zF, sh);
        const C=sh.core;
        PL.A(ctx,'stair','stairhall_s'+s, (C.x0+C.x1)/2, C.stair.y0+0.5, zF, {end:'foot'});
        PL.A(ctx,'stair','stairhall_s'+s, (C.x0+C.x1)/2, C.stair.y1-0.5, zF, {end:'landing'});
        if(s===0) PL.A(ctx,'entry','vestibule_s0', C.frontDoor.x, C.frontDoor.y-0.75, zF, {what:'street_door'});
        if(C.rearStair) PL.A(ctx,'stair','backhall_s'+s, (C.x0+C.x1)/2, sh.core.yRear-0.1, zF, {end:'rear_stair'});
      }
      for(const U of us) if(U.storey===s){
        const L=P.layouts[U.id], hs=L.box.hs;
        PL.A(ctx,'entry', L.keyRooms.hall, U.openings.entry.x - hs*0.55, U.openings.entry.y, zF);
        if(U.openings.porchDoor) PL.A(ctx,'porch', L.keyRooms.hall,
          U.openings.porchDoor.x, U.openings.porchDoor.y-0.7, zF, {kind:'front_porch'});
        if(U.openings.rearDoor) PL.A(ctx,'porch', L.keyRooms.kitchen,
          U.openings.rearDoor.x, U.openings.rearDoor.y+0.7, zF, {kind:'rear_porch'});
      }
      ctx.openings.push(...doors.filter(d=>d.storey===s).map(d=>({ axis:d.axis, plane:r3(d.plane),
        c:r3(d.c), clearW:d.clearW, from:d.from, to:d.to, kind:d.kind, storey:s })));
    }
    return { faces:out, ctx, plan:P };
  }

  function drawFlight(out, ctx, PL, st, b, z, sh){
    const n=st.steps, run=(st.y1-st.y0)/n, rise=(sh.ceilH+sh.floorT)/n;
    for(let i=0;i<n;i++){
      const y0=st.y0+i*run, y1=y0+run, zz=z+rise*(i+1);
      PL.P(out,ctx,{x0:st.x0,x1:st.x1, y0,y1, z0:Math.max(z, zz-rise-0.02), z1:zz,
        mat:'join', tex:treadTex(), b:0.05, topB:0.40, blocker:false});
    }
    ctx.blockers.push({kind:'stair', storey:st.storey, x0:r3(st.x0),x1:r3(st.x1),
      y0:r3(st.y0),y1:r3(st.y1),h:r3(sh.ceilH)});
    // newel, raked handrail and balusters — posts alone read as loose rails
    const hx=st.x1-0.06;
    for(let i=0;i<=n;i+=2){
      const yy=st.y0+i*run, zz=z+rise*(i+1);
      boxSolid(out, hx-0.035, hx+0.035, yy-0.03, yy+0.03, zz, zz+0.82, 'join', null, 0.20, 0.34);
    }
    for(let i=0;i<n;i++){
      const yy=st.y0+i*run, zz=z+rise*(i+1)+0.82;
      boxSolid(out, hx-0.055, hx+0.055, yy, yy+run, zz-0.06, zz, 'join', null, 0.32, 0.40);
    }
    boxSolid(out, hx-0.075, hx+0.075, st.y0-0.09, st.y0+0.07, z, z+1.02, 'join', null, 0.26, 0.38);
  }

  // ---- furnishing: the tier picks from PropIso, the placer owns where ------
  // Orientation is explicit. Each room publishes which edges are GLAZED and which are SOLID, and
  // every wall-hugging piece is offered the solid list in order, so a headboard never stands on a
  // window and a counter never blocks the light.
  function furnishFlat(out, ctx, PL, r, b, z, U, L){
    const T=b.T, K=T.kit, lux=b.tier==='gambrel';
    const put=(name,opts,pl)=>PL.put(out,ctx,r,z,name,opts,Object.assign({room:r.id},pl||{}));
    const any=(name,opts,walls,pl)=>PL.putAny(out,ctx,r,z,name,opts,walls,Object.assign({room:r.id},pl||{}));
    const A=(verb,x,y,extra)=>PL.A(ctx,verb,r.id,x,y,z,extra);
    // ROOM-SIZED RUG (manor precedent). A small mat adrift on a plank floor reads as a stain at 32
    // px to the metre; a rug that runs the room is what tells you where the sitting group is.
    // fitRun shrinks the run to the wall it is given, so a length past the room simply fills it,
    // and ignoreClear lets it pass under the furniture instead of competing for the slot.
    const rug=(fx,fy,pad)=>PL.put(out,ctx,r,z,'rug',
      {len:Math.max(0, (r.x1-r.x0)-(pad!=null?pad:0.62)), fabric:K.fabric, fabric2:K.fabric2},
      {room:r.id, wall:'free', fx:fx!=null?fx:0.5, fy:fy!=null?fy:0.5, ignoreClear:true});
    const solid=(r.solid&&r.solid.length)?r.solid.slice():['rear','front','left','right'];
    // a headboard may only stand on a SOLID wall; a dresser or a shelf is low enough to sit under
    // a sash, so low pieces get the glazed walls as a fallback rather than going unplaced
    const lowWalls=solid.concat(['rear','front','left','right'].filter(w=>solid.indexOf(w)<0));
    const inward=(wl)=>wl==='rear'?[0,0.44]:(wl==='front'?[0,-0.44]:(wl==='left'?[0.44,0]:[-0.44,0]));

    switch(r.kind){
      case 'living': {
        // a parlour, not a lounge: the sofa on a solid wall, chairs turned in, the porch door clear
        const sf = any('sofa', {len:0.35, fabric:K.fabric, fabric2:K.fabric2, wood:K.wood},
                       solid, {along:0.34, inset:0.14});
        if(sf){ const horiz=(sf.x1-sf.x0)>(sf.y1-sf.y0);
          const n=Math.max(2, Math.round(Math.max(sf.x1-sf.x0, sf.y1-sf.y0)/0.74));
          for(let i=0;i<n;i++) A('sit',
            horiz? sf.x0+(sf.x1-sf.x0)*(i+0.5)/n : (sf.x0+sf.x1)/2 + ((sf.x0-r.x0)<(r.x1-sf.x1)?0.42:-0.42),
            horiz? ((sf.y0-r.y0)<(r.y1-sf.y1)? sf.y1+0.36 : sf.y0-0.36) : sf.y0+(sf.y1-sf.y0)*(i+0.5)/n,
            {seat:i}); }
        for(const wl of [solid[1]||solid[0], solid[0]]){
          const ac = any('armchair', {fabric:K.fabric2, fabric2:K.fabric, wood:'walnut'}, [wl], {along:0.86});
          if(ac){ const d=inward(wl); A('sit', (ac.x0+ac.x1)/2+d[0]*0.8, (ac.y0+ac.y1)/2+d[1]*0.8, {seat:'armchair'}); break; }
        }
        if(r.area>15.0) rug(0.46, 0.5);
        // a parlour big enough to dine in gets the table AND chairs; a smaller one gets the tea
        // table instead. Both at once is how a room ends up with chairs it cannot place.
        if(r.area>19.0){
          const tb = put('table', {len:0.10, wood:K.wood}, {wall:'free', fx:0.72, fy:0.24});
          if(tb){ const n=(tb.x1-tb.x0)>1.5?3:2;
            for(let i=0;i<n;i++){ const tx=tb.x0+(tb.x1-tb.x0)*(i+0.5)/n;
              for(const sg of [-1,1]){ const ty = sg<0? tb.y0-0.30 : tb.y1+0.30;
                if(ty<r.y0+0.28||ty>r.y1-0.28) continue;
                PL.put(out,ctx,{x0:tx-0.30,x1:tx+0.30,y0:ty-0.24,y1:ty+0.24}, z, 'chair',
                  {wood:K.wood, variant:0, fabric:K.fabric}, {wall:'free', face:sg<0?'S':'N', room:r.id});
                A('dine', tx, ty, {seat:i+(sg<0?'a':'b')}); } } }
        } else if(r.area>15.0) put('roundTable', {wood:K.wood}, {wall:'free', fx:0.46, fy:0.52});
        any('shelf', {len:0.2, wood:K.wood}, solid, {along:0.06});
        break;
      }
      case 'bed': {
        // the headboard wants a SOLID wall — never the street sash, never the side sash
        const variant = r.primary ? 0 : (Math.min(r.x1-r.x0, r.y1-r.y0)<2.70 ? 1 : 0);
        if(r.area>9.0) rug(0.5, 0.54, 0.88);
        const bd = any('bed', {variant, wood:K.wood, fabric: lux?'sage':'blue', fabric2:'cream'},
                       solid, {along:0.5, inset:0.15});
        if(bd){
          const alongY=(bd.y1-bd.y0)>(bd.x1-bd.x0);
          const size = ['double','single','wide'][variant];
          // ONE SLEEP ANCHOR PER SIDE YOU CAN ACTUALLY GET TO. A double used to claim both sides
          // whatever the room did: on captains2 the second bedroom published a left-hand standing
          // spot with no floor under it, and the reach audit duly returned no_clear_spot. A side
          // earns its anchor only when the gap between mattress and room edge takes a body —
          // 0.22 m of shoulder plus the 0.38 m the anchor stands off at.
          const NEED=0.62;
          const gap=(sd)=>alongY ? (sd==='left'? bd.x0-r.x0 : r.x1-bd.x1)
                                 : (sd==='left'? bd.y0-r.y0 : r.y1-bd.y1);
          let sides=(variant===1?['right']:['left','right']).filter(sd=>gap(sd)>=NEED);
          if(!sides.length){
            // an alcove bed: no side clears, so the FOOT is the way in and it sleeps one
            const fx=alongY ? (bd.x0+bd.x1)/2 : (r.x1-bd.x1>bd.x0-r.x0 ? bd.x1+0.38 : bd.x0-0.38);
            const fy=alongY ? (r.y1-bd.y1>bd.y0-r.y0 ? bd.y1+0.38 : bd.y0-0.38) : (bd.y0+bd.y1)/2;
            A('sleep', clamp(fx, r.x0+0.3, r.x1-0.3), clamp(fy, r.y0+0.3, r.y1-0.3),
              {side:'foot', size, approach:'foot_only'});
            sides=[];
          }
          const pads=[];
          for(const sd of sides){
            const ax = alongY ? (sd==='left'? bd.x0-0.38 : bd.x1+0.38) : (bd.x0+bd.x1)/2;
            const ay = alongY ? (bd.y0+bd.y1)/2 : (sd==='left'? bd.y0-0.38 : bd.y1+0.38);
            const px=clamp(ax, r.x0+0.3, r.x1-0.3), py=clamp(ay, r.y0+0.3, r.y1-0.3);
            A('sleep', px, py, {side:sd, size}); pads.push([px,py]);
          }
          // hold those pads: the wardrobe and the dresser come next and would otherwise stand on
          // the way in to the bed they are meant to serve
          PL.reserveStand(ctx, pads, 0.26, 'the way in to the bed');
        }
        const wd = any('wardrobe', {paint:K.paint, wood:'pine'}, lowWalls, {along:0.08});
        if(wd){ const d=inward(solid[0]);
          const wx=(wd.x0+wd.x1)/2+d[0]*0.9, wy=(wd.y0+wd.y1)/2+d[1]*0.9;
          A('store', wx, wy, {what:'wardrobe'});
          PL.reserveStand(ctx, [[wx,wy]], 0.26, 'the way in to the wardrobe'); }
        if(r.area>10.0) any('dresser', {paint: lux?'sage':'blue', wood:'pine'}, lowWalls, {along:0.9});
        if(!lux && r.primary) any('seaChest', {wood:'walnut'}, lowWalls, {along:0.62});
        break;
      }
      case 'bath': {
        const wc = any('toilet', {variant:K.wc}, solid, {along:0.06});
        if(wc) A('toilet', (wc.x0+wc.x1)/2, (wc.y0+wc.y1)/2 + 0.44);
        const vn = lux
          ? any('vanity', {variant:(r.x1-r.x0)<1.85?2:0, len:0.0, paint:K.paint, wood:'pine', worktop:K.worktop},
                lowWalls, {along:0.96})
          : any('washstand', {wood:'walnut'}, lowWalls, {along:0.96});
        if(vn) A('vanity', (vn.x0+vn.x1)/2, (vn.y0+vn.y1)/2 + 0.42, {basin:0});
        // ask for the tier's fixture, then fall back to a cubicle rather than leaving the room with
        // nothing to wash in — the fallback is a design answer, not a miss, so clear the rejects
        const mark = ctx.rejects.length;
        let fx=null, kind=null;
        if(!T.shower){ fx = any('tub', {variant:K.tub}, ['rear','front','left','right'], {along:0.5}); if(fx) kind='tub'; }
        if(!fx){ fx = any('shower', {worktop:'greentile'}, ['rear','left','right','front'], {along:0.04}); if(fx) kind='shower'; }
        if(fx){ ctx.rejects.length=mark; A('bathe', (fx.x0+fx.x1)/2, (fx.y0+fx.y1)/2 - 0.46, {fixture:kind}); }
        any('mirror', {wood: lux?'walnut':'pine'}, lowWalls, {along:0.5});
        any('towelRail', {wood:'pine', fabric:'white'}, lowWalls, {along:0.34});
        decalY(out, r.y0, 1, r.x0+0.05, r.x1-0.05, z+0.02, z+1.32, 'wallTile', 0.16, subwayTex(), false, 0.04);
        break;
      }
      case 'kitchen': {
        // a galley down the party wall and the bathroom wall, the sink under the yard window, the
        // back door and the doorway from the cross hall both left clear
        const ct = any('counter', {len:0.14, paint:K.paint, wood:'pine', worktop:K.worktop},
                       solid, {along:0.0, margin:0.12});
        const st = any(K.cook, {wood:'oak', variant:0}, solid, {along:1.0, margin:0.12});
        if(st) A('cook', (st.x0+st.x1)/2 + ((st.x0-r.x0)<(r.x1-st.x1)?0.54:-0.54), (st.y0+st.y1)/2);
        const sk = any(K.sink, {paint:K.paint, wood:'pine', worktop:K.worktop},
                       [solid[1]||solid[0], solid[0], 'rear'], {along:0.30});
        if(sk) A('wash_dishes', (sk.x0+sk.x1)/2, (sk.y0+sk.y1)/2 - 0.50);
        const fr = any(K.cold, {paint: lux?'steel':'white', wood:'oak'}, lowWalls, {along:0.84});
        if(fr) A('store', (fr.x0+fr.x1)/2, (fr.y0+fr.y1)/2 + 0.55, {what:lux?'fridge':'icebox'});
        if(!lux && r.area>9.0) any('hutch', {paint:'sage', wood:'pine', worktop:'butcher'}, lowWalls, {along:0.5});
        if(lux && T.inSuiteLaundry){
          const wm = any('washer', {variant:1, paint:'white'}, lowWalls, {along:0.56});
          if(wm) A('laundry', (wm.x0+wm.x1)/2, (wm.y0+wm.y1)/2 - 0.46, {machine:0});
        }
        if(!lux) decalY(out, r.y0, 1, r.x0+0.10, r.x1-0.10, z+0.92, z+1.44, 'wallTile', 0.20, subwayTex(), false, 0.05);
        break;
      }
      case 'dining': {
        const tb = put('table', {len:0.24, wood:K.wood}, {wall:'free', fx:0.5, fy:0.46});
        if(tb){ const n=(tb.x1-tb.x0)>1.7?3:2;
          for(let i=0;i<n;i++){ const tx=tb.x0+(tb.x1-tb.x0)*(i+0.5)/n;
            for(const sg of [-1,1]){ const ty = sg<0? tb.y0-0.30 : tb.y1+0.30;
              if(ty<r.y0+0.26||ty>r.y1-0.26) continue;
              PL.put(out,ctx,{x0:tx-0.30,x1:tx+0.30,y0:ty-0.24,y1:ty+0.24}, z, 'chair',
                {wood:K.wood, variant:0}, {wall:'free', face:sg<0?'S':'N', room:r.id});
              A('dine', tx, ty, {seat:i+(sg<0?'a':'b')}); } } }
        if(r.area>7.0) any('hutch', {paint:K.paint, wood:'pine', worktop:'butcher'}, lowWalls, {along:0.16});
        any('shelf', {len:0.1, wood:K.wood}, lowWalls, {along:0.86});
        break;
      }
      case 'hall': {
        // the entry hall: a bench to pull boots off, a shelf, and the boots themselves
        const bn = any('bench', {len:0.0, wood:K.wood}, [r.spineWall, r.yardWall], {along:0.72});
        if(bn) A('sit', (bn.x0+bn.x1)/2, (bn.y0+bn.y1)/2 - 0.34, {seat:'hall'});
        any('shelf', {len:0.0, wood:K.wood}, [r.spineWall, r.yardWall], {along:0.24});
        if(!lux) any('crate', {wood:'pine'}, [r.spineWall], {along:0.95});
        break;
      }
      case 'crosshall': {
        if(Math.min(r.x1-r.x0, r.y1-r.y0) > 1.5)
          any('shelf', {len:0.0, wood:K.wood}, ['left','right'], {along:0.5});
        break;      // otherwise the cross hall stays clear — it is the route, not a room
      }
      case 'store': {
        any('shelf', {len:0.3, wood:'pine'}, solid, {along:0.5});
        A('store', (r.x0+r.x1)/2, (r.y0+r.y1)/2, {what:'flat_store'});
        break;
      }
      default: break;
    }
  }

  function furnishCore(out, ctx, PL, r, b, z){
    const lux=b.tier==='gambrel', K=b.T.kit;
    const any=(name,opts,walls,pl)=>PL.putAny(out,ctx,r,z,name,opts,walls,Object.assign({room:r.id},pl||{}));
    const A=(verb,x,y,extra)=>PL.A(ctx,verb,r.id,x,y,z,extra);
    switch(r.kind){
      case 'vestibule': {
        // the mail wall is what makes a vestibule a vestibule, and what a postal routine needs
        const mb = any('mailboxes', {len:0.0}, ['left','right','front'], {along:0.16});
        if(mb) A('mail', (mb.x0+mb.x1)/2 + ((mb.x0-r.x0)<(r.x1-mb.x1)?0.5:-0.5), (mb.y0+mb.y1)/2, {what:'mailboxes'});
        const bn = any('bench', {len:0.0, wood:lux?'walnut':'pine'}, ['right','left'], {along:0.80});
        if(bn) A('sit', (bn.x0+bn.x1)/2 + ((bn.x0-r.x0)<(r.x1-bn.x1)?0.42:-0.42), (bn.y0+bn.y1)/2, {seat:0});
        if(lux) any('rug', {len:0.0, fabric:K.fabric, fabric2:K.fabric2}, ['free'], {fx:0.5, fy:0.5, ignoreClear:true});
        break;
      }
      case 'laundry': {
        // the porch tier has no machine in the flat, so this shared room sits on a route
        const n = (r.x1-r.x0) > 2.6 ? 2 : 1;
        for(let i=0;i<n;i++){
          const wm = PL.put(out,ctx,r,z,'washer',{variant:0, paint:'white'},
            {wall:'rear', along:n>1?i/(n-1):0.5, room:r.id});
          if(wm) A('laundry', (wm.x0+wm.x1)/2, wm.y1+0.46, {machine:i});
        }
        any('counter', {len:0.0, paint:'white', wood:'pine', worktop:'slate'}, ['front','left','right'], {along:0.5});
        any('washtub', {wood:'oak'}, ['left','right'], {along:0.86});
        any('shelf', {len:0.0, wood:'pine'}, ['left','right'], {along:0.12});
        break;
      }
      case 'store': {
        any('lockers', {len:0.2}, ['rear','front'], {along:0.5});
        any('crate', {wood:'pine'}, ['left','right'], {along:0.5});
        A('store', (r.x0+r.x1)/2, (r.y0+r.y1)/2, {what:'core_store'});
        break;
      }
      case 'backhall': {
        if(!lux) any('crate', {wood:'pine'}, ['left','right'], {along:0.85});
        break;
      }
      default: break;      // landing, stair hall stay clear
    }
  }

  // ================= post + render =================
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
    const wx=b.weather;
    if(wx>0.02){
      const rnd=mulberry32(6619);
      for(let i=0;i<N;i++){ const m=nbuf[i]; if(!m||!rbuf[i]) continue;
        if((m==='floor'||m==='wall'||m==='facadeWall'||m==='lino') && rnd()<wx*0.05)
          out[i]=rbuf[i][Math.max(0,Math.min(rbuf[i].length-1,ibuf[i]-1))]; }
    }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!out[i]) continue;
      let n=0;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy;
        if(nx>=0&&nx<W&&ny>=0&&ny<H&&out[ny*W+nx]) n++; }
      if(n===0){ out[i]=null; rbuf[i]=null; }
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
    const b=resolve(opts);
    const built=build(b);
    const MATS=Object.assign(makeMats(b), built.ctx.mats);
    return toRGBA(post(paint(built.faces, {dir, elev:b.elev}, MATS), b));
  }

  // ================= published =================
  // roomH and storeyRise sit on BOTH dims() and anchors(), the cottage-room contract: the game
  // builds walls, ceiling, the stair opening and the colliders from these two numbers, and until a
  // rig published them the baker guessed and got a figure different from the one the rig drew.
  //   storeyRise  floor-to-floor rise to the storey above == storeyZ[s+1] - storeyZ[s]
  //                                                       == stair floorRise == steps x rise
  //               null on the top storey (the key is present, the value is null)
  //   roomH       clear floor-to-ceiling height == storeyRise - floorT, the plate the rig draws
  function heightsOf(b, s){
    const sh=b.sh, top=sh.storeys-1;
    const rise = s<top ? r3(sh.storeyZ[s+1]-sh.storeyZ[s]) : null;
    return { storeyRise:rise, roomH:r3(sh.ceilH) };
  }
  function dims(opts){ const b=resolve(opts||{});
    const sel=storeysShown(b), s0=sel[0]!=null?sel[0]:0, hh=heightsOf(b,s0);
    return { tier:b.tier, storeys:b.sh.storeys, perFloor:b.sh.perFloor, units:b.sh.units.length,
      focus:b.focus, ceilH:b.sh.ceilH, floorT:b.sh.floorT, storeyZ:b.sh.storeyZ, partT:b.partT,
      storey:s0, roomH:hh.roomH, storeyRise:hh.storeyRise,
      roomHByStorey:b.sh.storeyZ.map((_,s)=>heightsOf(b,s).roomH),
      storeyRiseByStorey:b.sh.storeyZ.map((_,s)=>heightsOf(b,s).storeyRise),
      floorAtPivot:b.floorAtPivot,
      core:b.sh.core, porches:b.sh.porches }; }
  function plan(opts){ const b=resolve(opts||{}), P=planAll(b);
    return { storeys:b.sh.storeys, perFloor:b.sh.perFloor,
      units:b.sh.units.map(U=>({ id:U.id, storey:U.storey, col:U.col, side:U.side, beds:U.beds,
        bedsBuilt:P.layouts[U.id].bedsBuilt, bedsRequested:P.layouts[U.id].bedsRequested,
        baths:P.layouts[U.id].baths, chamber:P.layouts[U.id].chamber, split:P.layouts[U.id].split,
        rooms:P.layouts[U.id].rooms, thresholds:P.layouts[U.id].doors })),
      cores:Object.keys(P.cores).map(s=>({ storey:+s, rooms:P.cores[s].rooms, thresholds:P.cores[s].doors })),
      stairs:P.stairs }; }
  function rooms(opts){ const p=plan(opts);
    const out=[]; for(const u of p.units) out.push(...u.rooms);
    for(const c of p.cores) out.push(...c.rooms); return out; }
  function interior(opts){
    const b=resolve(opts||{}), built=build(b), P=built.plan, ctx=built.ctx;
    const verbs={}; for(const a of ctx.anchors) verbs[a.verb]=(verbs[a.verb]||0)+1;
    const ss=storeysShown(b), us=unitsShown(b);
    return {
      contract:'32 px = 1 m · same origin, camera and pivot as Art/stackFlatsIsoRig.js',
      tier:b.tier, focus:b.focus, storeys:b.sh.storeys, perFloor:b.sh.perFloor,
      storeysShown:ss, blockUnits:b.sh.units.length,
      units:us.map(U=>({ id:U.id, storey:U.storey, col:U.col, side:U.side, built:true,
        beds:U.beds, bedsBuilt:P.layouts[U.id].bedsBuilt, bedsRequested:P.layouts[U.id].bedsRequested,
        baths:P.layouts[U.id].baths, chamber:P.layouts[U.id].chamber,
        interior:U.interior, glazed:U.glazed, split:P.layouts[U.id].split,
        rooms:P.layouts[U.id].rooms, thresholds:P.layouts[U.id].doors })),
      cores: (b.focus==='unit' ? [] : ss.map(s=>({ storey:s, rooms:P.cores[s].rooms,
        thresholds:P.cores[s].doors }))),
      porches:b.sh.porches, stairs:P.stairs.filter(v=>ss.indexOf(v.storey)>=0),
      anchors:ctx.anchors, blockers:ctx.blockers, openings:ctx.openings,
      unplaced:ctx.rejects, keepClear:ctx.clearAll, stairVoids:ctx.voidsAll,
      counts:{ rooms:(function(){ let n=0; for(const s of ss){
                 for(const U of us) if(U.storey===s) n+=P.layouts[U.id].rooms.length;
                 if(b.focus!=='unit') n += P.cores[s].rooms.length; } return n; })(),
               anchors:ctx.anchors.length, blockers:ctx.blockers.length, verbs },
    };
  }
  function anchors(dir, opts){
    opts=opts||{}; const b=resolve(opts), B=camBasis({dir,elev:b.elev});
    const it=interior(opts);
    const ss=storeysShown(b);
    const zShift=(b.floorAtPivot && ss.length===1) ? -b.sh.storeyZ[ss[0]] : 0;
    const s0=ss[0]!=null?ss[0]:0, hh=heightsOf(b,s0);
    const pj=(x,y,z)=>{ const v=projVert(x,y,z,B); return {x:Math.round(v.sx),y:Math.round(v.sy)}; };
    return { anchors: it.anchors.map(a=>Object.assign({}, a, pj(a.x,a.y,a.z))),
             rooms: rooms(opts).map(r=>Object.assign({ id:r.id, kind:r.kind, storey:r.storey },
               pj((r.x0+r.x1)/2, (r.y0+r.y1)/2, (b.sh.storeyZ[r.storey]||0)+zShift))),
             storey:s0, roomH:hh.roomH, storeyRise:hh.storeyRise,
             storeyZ:b.sh.storeyZ[s0], floorAtPivot:b.floorAtPivot,
             floor: pj(0, 0, (b.sh.storeyZ[s0]||0)+zShift),
             dims: dims(opts) };
  }

  // ---- gameplay sections owned by the INTERIOR rig ---------------------------
  // Terrace precedent through the shared writer (Art/_buildingGameplay.js): the room rig owns SOLE,
  // the interior THRESHOLDs, STAIRS, INTERACT and BLOCKERS, because a dwelling's routes are indoors.
  // StackFlatsIso.gameplayAll() merges these and stamps this file's hash beside its own.
  function gameplaySections(opts){
    const BG=root.BuildingGameplay; if(!BG) return null;
    const b=resolve(Object.assign({}, opts||{}, {focus:'all', storey:'stack', floorAtPivot:false}));
    const built=build(b), ctx=built.ctx, P=built.plan, sh=b.sh;
    const rooms=[], doors=[], stairs=[];
    // A three-decker stacks one flat per column per storey, so the lookup needs the storey as well
    // as the x span — the flat above is a different household with its own furniture.
    const unitAt=(x,storey)=>sh.units.find(v=>v.storey===storey &&
        x>=v.interior.x0-0.01 && x<=v.interior.x1+0.01) || null;
    const unitIdAt=(x,y,storey)=>{ const u=unitAt(x,storey); return u?u.id:null; };
    for(const U of sh.units){
      const L=P.layouts[U.id];
      for(const r of L.rooms) rooms.push(Object.assign({}, r, {unit:U.id, uid:U.id+'.'+r.id}));
      for(const d of L.doors) doors.push(Object.assign({}, d, {unit:U.id}));
    }
    for(let s=0;s<sh.storeys;s++){
      const C=P.cores[s];
      for(const r of C.rooms) rooms.push(Object.assign({}, r, {unit:null, uid:r.id, core:true}));
      for(const d of C.doors) doors.push(Object.assign({}, d, {unit:null, core:true}));
    }
    for(const st of P.stairs) stairs.push(Object.assign({}, st, {unit:null, core:true,
      id:'core.stair_s'+st.storey }));
    const blk=ctx.blockers.map(q=>Object.assign({ unit:unitIdAt((q.x0+q.x1)/2,(q.y0+q.y1)/2,q.storey) }, q));
    const anch=ctx.anchors.map(a=>Object.assign({ unit:unitIdAt(a.x,a.y,a.storey) }, a));
    return BG.sections({ rooms, doors, stairs, voids:ctx.voidsAll, blockers:blk, anchors:anch,
      rejects:ctx.rejects, storeyZ:sh.storeyZ, ceilH:sh.ceilH,
      roomH:(s)=>heightsOf(b,s).roomH, storeyRise:(s)=>heightsOf(b,s).storeyRise,
      unitOf:unitIdAt, interiorRig:'Art/stackUnitIsoRig.js' });
  }

  function project(dir, p, elev){ const v=projVert(p[0],p[1],p[2],camBasis({dir,elev})); return {x:v.sx,y:v.sy}; }

  root.StackUnitIso = { W, H, PX, pivot:{x:cx,y:groundY}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'], FINISHES, TIERS, ROOMKINDS, VERBS, PRESETS, KEY,
    dims, plan, rooms, interior, gameplaySections, render, anchors, project };
})(typeof globalThis!=='undefined'?globalThis:window);
