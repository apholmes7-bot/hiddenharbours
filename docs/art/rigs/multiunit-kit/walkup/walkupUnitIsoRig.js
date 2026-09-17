/* Hidden Harbours — parametric ISO WALK-UP INTERIOR rig (ADR-0006 bake pipeline, SAME turntable +
   camera + shading as walkupIsoRig.js / rowhouseUnitIsoRig.js / interiorIsoRig.js / the fleet).
   Dollhouse cutaway of a double-loaded corridor block: one FLAT, one FLOORPLATE, or the whole stack.
   32 px = 1 m. Registers to Art/walkupIsoRig.js to the pixel.

   THREE HARD DEPENDENCIES, ALL DELIBERATE:
     Art/walkupIsoRig.js      every dimension. This rig MEASURES NOTHING — unit interior boxes, band
                              depths, the corridor, the core's stair and lift boxes, storey heights,
                              wall thicknesses and the entry-door position all come from shell(opts).
     Art/interiorPropRig.js   every piece of furniture. This rig PLACES props, it does not model
                              them; each comes through PropIso.emit() carrying PropIso's own ramps,
                              so a bed here is the same model and colours as a bed anywhere else.
     Art/_interiorPlacer.js   every placement RULE — keep-clear zones, wall-slide retries, run-prop
                              shrink-to-fit, head-to-wall beds, rejection reporting. Shared with the
                              terrace so the collision logic lives in one place.
   Load order: walkupIsoRig.js, interiorPropRig.js, _interiorPlacer.js, then this file.

   THE FLAT. A corridor flat is two bands deep, and that is the whole plan:
     SERVICE BAND (at the corridor, no daylight — honest for this building type)
       kitchen | bath | store/utility | entry hall, with a short internal corridor along the band's
       inner edge giving a door to every room. The front door lands in the hall.
     FACADE BAND (all the daylight)
       living room + every bedroom, side by side, each with a window. Bedrooms NEVER sit in the
       service band: a windowless bedroom is not a bedroom.
   A plate can only hold as many bedrooms as the facade band can honestly light and fit, so a unit
   publishes bedsBuilt beside bedsRequested rather than silently making 2 m closets.

   WHY THE INTERIORS DO NOT LOOK LIKE THE TERRACE. Same two tiers, different material world:
     basic    sheet-vinyl floors, magnolia painted plaster, matt grey wet-room tile, flush white
              joinery, exposed concrete soffits. A built-in panelled bath, no shower, one bathroom,
              and NO washing machine in the flat — the block's residents use the shared laundry in
              the core, which is what makes that room matter to a schedule.
     luxury   wide oak boards, EXPOSED BRICK on the inside face of the facade (the cannery-loft
              signature), lime plaster elsewhere, terrazzo wet rooms, blackened-steel joinery. Open
              kitchen borrowing the living room's light, walk-in shower, an ensuite once there are
              two bedrooms, stone worktops, and an in-suite laundry in the utility room.

   Exposes globalThis.WalkupUnitIso = { W,H,PX,pivot,order,defaultElev, FINISHES,TIERS,ROOMKINDS,
     VERBS,PRESETS, dims(opts), plan(opts), rooms(opts), interior(opts), gameplaySections(opts),
     render(dir,opts), anchors(dir,opts), project(dir,p,elev) }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 1536, H = 1500, cx = 772, groundY = 900;
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;

  const FINISHES = {
    floorVinyl:   ['#3b3833','#4a4640','#5a554d','#6b655b','#7c7568','#8d8578'],
    floorOakWide: ['#43301f','#5c4530','#74583d','#8c6d4d','#a3835f','#b99a75'],
    concPolish:   ['#44484b','#54585c','#666a6e','#7a7e82','#8e9296','#a2a6aa'],
    wallMagnolia: ['#6f6a5c','#847e6d','#9a9382','#b0a897','#c5bdab','#d9d1bf'],
    wallPlaster:  ['#8f8e88','#a8a79f','#c0bfb6','#d5d4cb','#e6e5dd','#f3f2ec'],
    brickExposed: ['#3a2523','#50332d','#664239','#7c5345','#936854','#a87f66'],
    tileGreyMatt: ['#54585b','#666a6d','#7a7e81','#8e9295','#a2a6a9','#b6babd'],
    terrazzo:     ['#6b6a64','#807e77','#95938b','#a9a79f','#bdbbb3','#d1cfc7'],
    joinWhite:    ['#8f948e','#a9ada5','#c2c5bc','#d7dad0','#e9ebe1','#f5f7ed'],
    joinSteel:    ['#22252a','#2e3238','#3c4148','#4c525a','#5e646d','#71787f'],
    soffitConc:   ['#3b3f42','#4a4e51','#5b5f62','#6d7174','#7f8386','#919598'],
    metal:        ['#464d51','#5a6267','#727c81','#8c979c','#a6b1b5','#c2cccf'],
  };
  const GLASS_DAY   = ['#7d949b','#94aab0','#abbfc4','#c2d4d8'];
  const GLASS_NIGHT = ['#232831','#2a2f3a','#343a46','#414855'];
  const CAVITY = ['#0d1013','#141a1e','#1b2328','#232c32'];
  const KEY = '#1a1c22';

  const TIERS = {
    basic: {
      floor:'floorVinyl', wet:'tileGreyMatt', wall:'wallMagnolia', facadeWall:'wallMagnolia',
      join:'joinWhite', soffit:'soffitConc',
      openKitchen:false, ensuite:false, inSuiteLaundry:false, shower:false, weather:0.22,
      kit:{ era:'modern', wood:'pine', paint:'white', worktop:'slate', fabric:'blue', fabric2:'cream',
            cold:'fridge', cook:'range', sink:'wetSink', seat:'sofa', wc:1, tub:1 },
    },
    luxury: {
      floor:'floorOakWide', wet:'terrazzo', wall:'wallPlaster', facadeWall:'brickExposed',
      join:'joinSteel', soffit:'concPolish',
      openKitchen:true, ensuite:true, inSuiteLaundry:true, shower:true, weather:0.05,
      kit:{ era:'modern', wood:'walnut', paint:'sage', worktop:'marble', fabric:'sage', fabric2:'cream',
            cold:'fridge', cook:'range', sink:'wetSink', seat:'sofa', wc:1, tub:1 },
    },
  };
  const ROOMKINDS = ['hall','corridor','living','kitchen','bed','bath','ensuite','store','laundry',
                     'lobby','stairhall','lift','lockers','landing'];
  const VERBS = ['sleep','cook','wash_dishes','sit','dine','toilet','bathe','vanity','laundry',
                 'store','desk','entry','balcony','stair','lift','mail'];
  const PRESETS = {
    walkupFlat2:   { tier:'basic',  units:8,  storeys:2, beds:2, focus:'unit',  storey:0 },
    walkupFlat1:   { tier:'basic',  units:4,  storeys:2, beds:1, focus:'unit',  storey:0 },
    walkupPlate:   { tier:'basic',  units:12, storeys:3, beds:2, focus:'floor', storey:1 },
    walkupLobby:   { tier:'basic',  units:12, storeys:3, beds:2, focus:'floor', storey:0 },
    canneryFlat3:  { tier:'luxury', units:12, storeys:3, beds:3, focus:'unit',  storey:1 },
    canneryPlate:  { tier:'luxury', units:12, storeys:3, beds:2, focus:'floor', storey:1 },
    canneryStack:  { tier:'luxury', units:12, storeys:3, beds:2, focus:'all' },
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
  function tileTex(sp){ const SP=sp||0.31;
    return (u,v)=>{ const su=((u%SP)+SP)%SP, sv=((v%SP)+SP)%SP;
      if(su<0.026||sv<0.026) return -1;
      const t=hash2(Math.floor(u/SP), Math.floor(v/SP)); return t>0.90?1:0; }; }
  function terrazzoTex(){ return (u,v)=>{ const t=hash2(Math.floor(u*11), Math.floor(v*11));
      return t<0.16?-1:(t>0.86?1:0); }; }
  function vinylTex(){ return (u,v)=>{ const t=hash2(Math.floor(u*5), Math.floor(v*5));
      return t<0.10?-1:(t>0.95?1:0); }; }
  function brickTex(){ const CO=0.086, BR=0.235;
    return (u,v)=>{ const row=Math.floor(v/CO), f=((v%CO)+CO)%CO;
      const off=(row&1)*BR*0.5, su=(((u+off)%BR)+BR)%BR;
      if(f<0.026) return -3; if(su<0.024) return -2;
      const t=hash2(Math.floor((u+off)/BR), row); return t<0.20?-1:(t>0.90?1:0); }; }
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
          const d=w0*a.d+w1*b.d+w2*c.d, deff=d-f.db, i=y*W+x;
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
    const EX = root.WalkupIso;
    if(!EX) throw new Error('WalkupUnitIso needs Art/walkupIsoRig.js loaded first');
    const tier = (opts.tier==='luxury') ? 'luxury' : 'basic';
    const T = TIERS[tier];
    const sh = EX.shell(opts);
    const dm = EX.dims(opts);
    return {
      tier, T, sh, dm, opts,
      focus: opts.focus || 'unit',
      storeySel: opts.storey!=null ? opts.storey : 0,
      unitSel: opts.unit!=null ? opts.unit : 0,
      furnish: opts.furnish!==false,
      // Manor precedent: draw the shown level with its floor on the pivot row, so the baker gets one
      // sprite per storey in a common frame. Every z drops by that storey's storeyZ and nothing
      // shifts sideways; storeyZ itself does NOT change. Defaults false, so every picture drawn
      // before this option existed is byte-identical.
      floorAtPivot: !!opts.floorAtPivot,
      partT: 0.12, cutFrac: 0.60,
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
      wallTile:{ramp:wth(FINISHES.tileGreyMatt)}, join:{ramp:wth(FINISHES[T.join])},
      soffit:{ramp:wth(FINISHES[T.soffit])}, metal:{ramp:wth(FINISHES.metal)},
      conc:{ramp:wth(FINISHES.concPolish)},
      glass:{ramp: night?GLASS_NIGHT:GLASS_DAY, off: night?0:1},
      cavity:{ramp:CAVITY},
    };
  }

  // ================= the flat program =================
  // The interior rig owns the ROOM PROGRAM; every dimension it is measured from comes from
  // shell(opts). Bedrooms only ever sit in the facade band, so a unit builds as many as that band
  // can light and fit — bedsBuilt is published beside bedsRequested rather than faking closets.
  const MIN_LIVING_W = 2.90, MIN_BED_W = 2.30;
  function layoutFlat(b, U){
    const I=U.interior, bands=U.bands, sgn=I.inward, w=I.w, d=I.d;
    const T=b.T, lux=b.tier==='luxury', DW = lux?0.90:0.82;
    const sb=bands.serviceD, corrD=bands.corridorD, hallW=bands.hallW;
    const X=(u)=>I.x0+u, Y=(v)=>I.yCorridor + sgn*v;
    const rect=(u0,u1,v0,v1)=>({ x0:X(Math.min(u0,u1)), x1:X(Math.max(u0,u1)),
      y0:Math.min(Y(v0),Y(v1)), y1:Math.max(Y(v0),Y(v1)) });

    const bedsBuilt = clamp(Math.floor((w - MIN_LIVING_W)/MIN_BED_W), 1, U.beds);
    let livW = clamp(w*0.38, MIN_LIVING_W, 4.60);
    livW = Math.min(livW, w - bedsBuilt*MIN_BED_W);
    const bedW = (w - livW)/bedsBuilt;

    // SERVICE BAND: an ordered allocation across the band, not fixed widths. Mins are honoured
    // first so even a narrow flat gets a kitchen and a bathroom; surplus then grows each room to
    // its target and its cap, and only a genuine surplus buys a second bathroom. That is what
    // stops a wide flat producing a 10 m2 laundry beside a 2 m galley.
    const zoneW = w - hallW;
    const specs=[
      { key:'kit',   min:1.90, target:Math.min(livW-0.30, 3.90), cap:4.20, always:true },
      { key:'bath',  min:1.85, target:2.40, cap:2.90, always:true },
      { key:'util',  min:1.50, target:2.10, cap:2.60 },
      { key:'bath2', min:1.75, target:2.20, cap:2.55 },
    ];
    const got={}; let left=zoneW;
    for(const sp of specs) if(sp.always){ got[sp.key]=sp.min; left-=sp.min; }
    for(const sp of specs) if(!sp.always && left>=sp.min){ got[sp.key]=sp.min; left-=sp.min; }
    for(const key of ['target','cap']) for(const sp of specs){
      if(got[sp.key]==null || left<=0) continue;
      const grow=Math.min(left, Math.max(0, sp[key]-got[sp.key]));
      got[sp.key]+=grow; left-=grow;
    }
    if(left>0.01){ got.kit+=left; left=0; }
    const kitW=got.kit, bathW=got.bath, utilW=got.util||0, bath2W=got.bath2||0;

    const rooms=[], parts=[], doors=[];
    const room=(id,kind,extra,u0,u1,v0,v1)=>{ const r=Object.assign({ id:U.id+'_'+id, kind,
      unit:U.id, storey:U.storey }, rect(u0,u1,v0,v1), extra||{});
      r.area=r3((r.x1-r.x0)*(r.y1-r.y0)); rooms.push(r); return r; };
    const part=(axis,plane,a0,a1,gaps)=>parts.push({ axis, plane, a0:Math.min(a0,a1), a1:Math.max(a0,a1),
      gaps:gaps||[], storey:U.storey, unit:U.id });
    const partU=(u, v0,v1, gaps)=>part('y', X(u), Math.min(Y(v0),Y(v1)), Math.max(Y(v0),Y(v1)), gaps);
    const partV=(v, u0,u1, gaps)=>part('x', Y(v), X(u0), X(u1), gaps);
    const door=(axis,plane,c,clearW,from,to,kind)=>doors.push({ axis, plane, c, clearW,
      from:U.id+'_'+from, to:(to==='corridor_block'?'corridor_s'+U.storey:U.id+'_'+to),
      kind:kind||'door', storey:U.storey, unit:U.id });
    const doorU=(u, v, cw, from,to,kind)=>door('y', X(u), Y(v), cw, from,to,kind);
    const doorV=(v, u, cw, from,to,kind)=>door('x', Y(v), X(u), cw, from,to,kind);

    // --- rooms
    room('kitchen','kitchen',{ open:T.openKitchen }, 0,kitW, 0,sb);
    // wet rooms sit next to each other across the band — kitchen | bath | bath2 | utility | hall
    const wetU=[]; const edges=[]; let uAt=kitW;
    const band=(id,kind,wid,extra)=>{ if(!(wid>0)) return;
      room(id, kind, extra, uAt, uAt+wid, 0, sb-corrD);
      wetU.push([id, uAt+wid/2]); uAt+=wid; edges.push(uAt); };
    band('bath','bath', bathW, { wet:true });
    band('bath2','bath', bath2W, { wet:true, second:true });
    band(T.inSuiteLaundry?'laundry':'store', T.inSuiteLaundry?'laundry':'store', utilW,
         { wet:T.inSuiteLaundry });
    const icorr   = room('corridor','corridor',{ route:true }, kitW,zoneW, sb-corrD,sb);
    const hall    = room('hall','hall',{ route:true, entry:true }, zoneW,w, 0,sb);
    const living  = room('living','living',{ open:T.openKitchen, glazedFacade:true }, 0,livW, sb,d);
    const beds=[];
    for(let i=0;i<bedsBuilt;i++){
      const u0=livW+i*bedW;
      beds.push(room('bed'+(i+1),'bed',{ primary:i===0, glazedFacade:true,
        glazedEnd: U.glazed.end && i===bedsBuilt-1 }, u0, u0+bedW, sb, d));
    }

    // --- partitions. Every one carries its storey; a partition that is not storey-filtered
    // reserves space on floors it is not on, which is how walls end up drawn through beds.
    partV(sb-corrD, kitW, zoneW, wetU.map(([,c])=>[X(c-DW/2), X(c+DW/2)]));
    for(const [id,c] of wetU) doorV(sb-corrD, c, DW, 'corridor', id);
    for(let i=0;i<edges.length-1;i++) partU(edges[i], 0, sb-corrD, []);

    // kitchen wall on u=kitW, with its door into the internal corridor
    const kdV = sb - corrD/2;
    partU(kitW, 0, sb, [[Math.min(Y(kdV-DW/2),Y(kdV+DW/2)), Math.max(Y(kdV-DW/2),Y(kdV+DW/2))]]);
    doorU(kitW, kdV, DW, 'kitchen','corridor');
    // hall wall, solid: the hall meets the internal corridor in the open, at v>sb-corrD
    partU(zoneW, 0, sb-corrD, []);

    // facade-band boundary at v=sb. Living and every bedroom get a door off circulation; the
    // kitchen either opens into the living room (luxury) or has a door to it (basic).
    const livDoorU = (kitW+livW)/2;
    const gapsAtSb=[];
    if(T.openKitchen) gapsAtSb.push([X(0.10), X(kitW-0.10)]);              // wide opening
    else { gapsAtSb.push([X(kitW/2-DW/2), X(kitW/2+DW/2)]); }
    gapsAtSb.push([X(livDoorU-DW/2), X(livDoorU+DW/2)]);
    for(const bd of beds){ const c=(bd.x0+bd.x1)/2 - I.x0;
      gapsAtSb.push([X(c-DW/2), X(c+DW/2)]); }
    partV(sb, 0, w, gapsAtSb);
    if(T.openKitchen) doorV(sb, kitW/2, kitW-0.20, 'kitchen','living','open');
    else doorV(sb, kitW/2, DW, 'kitchen','living');
    doorV(sb, livDoorU, DW, 'living','corridor');
    for(const bd of beds){ const c=(bd.x0+bd.x1)/2 - I.x0;
      const near = c >= zoneW ? 'hall' : 'corridor';
      doorV(sb, c, DW, bd.id.replace(U.id+'_',''), near); }

    // bedroom / living party walls, solid
    partU(livW, sb, d, []);
    for(let i=1;i<bedsBuilt;i++) partU(livW+i*bedW, sb, d, []);

    // hall to internal corridor: an open threshold, not a door — they are one space
    doorU(zoneW, sb - corrD/2, corrD - 0.06, 'hall','corridor','open');

    // the front door, off the building corridor into the hall
    doors.push({ axis:'x', plane:U.openings.entry.y, c:U.openings.entry.x, clearW:U.openings.entry.clearW,
      from:'corridor_s'+U.storey, to:U.id+'_hall', kind:'entry', storey:U.storey, unit:U.id });

    const baths = rooms.filter(v=>v.kind==='bath').length;
    return { rooms, parts, doors, bedsBuilt, bedsRequested:U.beds, baths,
      split:{ livW:r3(livW), bedW:r3(bedW), kitW:r3(kitW), bathW:r3(bathW), bath2W:r3(bath2W), utilW:r3(utilW),
              hallW:r3(hallW), serviceD:r3(sb), corridorD:r3(corrD) },
      box:{ x0:I.x0, x1:I.x1, yC:I.yCorridor, yFa:I.yFacade, w, d, sgn } };
  }

  // ================= the core program =================
  function layoutCore(b, storey){
    const sh=b.sh, C=sh.core, yR=sh.block.yRear, yF=sh.block.yFront, t=sh.wallT;
    const rooms=[], parts=[], doors=[], DW=b.tier==='luxury'?0.95:0.88;
    const stair=C.stair, lift=C.lift;
    const room=(id,kind,extra,x0,x1,y0,y1)=>{ const r=Object.assign({ id:id+'_s'+storey, kind,
      core:true, storey }, { x0:r3(Math.min(x0,x1)), x1:r3(Math.max(x0,x1)),
      y0:r3(Math.min(y0,y1)), y1:r3(Math.max(y0,y1)) }, extra||{});
      r.area=r3((r.x1-r.x0)*(r.y1-r.y0)); rooms.push(r); return r; };
    const part=(axis,plane,a0,a1,gaps)=>parts.push({ axis, plane, a0:Math.min(a0,a1), a1:Math.max(a0,a1),
      gaps:gaps||[], storey, core:true });

    // front: the lobby on the ground, a landing above
    const frontY0 = stair.y1, frontY1 = yF - t;
    if(storey===0) room('lobby','lobby',{ route:true, entry:true, mail:true }, C.x0, C.x1, frontY0, frontY1);
    else           room('landing','landing',{ route:true }, C.x0, C.x1, frontY0, frontY1);
    // middle: the stair hall, with the lift opening off it
    room('stairhall','stairhall',{ route:true }, C.x0, C.x1, stair.y0, stair.y1);
    room('lift','lift',{ route:true, shaft:true }, lift.x0, lift.x1, lift.y0, lift.y1);
    // rear: the shared laundry and the lockers on the ground, a store above
    const rearY0 = yR + t, rearY1 = stair.y0;
    if(storey===0){
      const split = rearY0 + (rearY1-rearY0)*0.52;
      room('laundry','laundry',{ wet:true, shared:true }, C.x0, C.x1, rearY0, split);
      if(C.lockers.present) room('lockers','lockers',{ shared:true }, C.x0, C.x1, split, rearY1);
      part('x', split, C.x0, C.x1, [[C.x0+0.55, C.x0+0.55+DW]]);
      doors.push({ axis:'x', plane:split, c:C.x0+0.55+DW/2, clearW:DW,
        from:'laundry_s0', to:'lockers_s0', kind:'door', storey:0, core:true });
      part('x', rearY1, C.x0, C.x1, [[C.x1-1.30, C.x1-1.30+DW]]);
      doors.push({ axis:'x', plane:rearY1, c:C.x1-1.30+DW/2, clearW:DW,
        from:'stairhall_s0', to:(C.lockers.present?'lockers_s0':'laundry_s0'), kind:'door', storey:0, core:true });
    } else {
      room('store','store',{ shared:true }, C.x0, C.x1, rearY0, rearY1);
      part('x', rearY1, C.x0, C.x1, [[C.x1-1.30, C.x1-1.30+DW]]);
      doors.push({ axis:'x', plane:rearY1, c:C.x1-1.30+DW/2, clearW:DW,
        from:'stairhall_s'+storey, to:'store_s'+storey, kind:'door', storey, core:true });
    }
    // lobby / stair hall: an open threshold, not a door — you walk straight through
    doors.push({ axis:'x', plane:frontY0, c:(C.x0+C.x1)/2, clearW:Math.min(2.4, C.x1-C.x0-0.4),
      from:(storey===0?'lobby_s0':'landing_s'+storey), to:'stairhall_s'+storey, kind:'open', storey, core:true });
    // stair hall to the block corridor: open
    doors.push({ axis:'y', plane:C.x1, c:0, clearW:Math.min(2.0, sh.corridor.w+0.2),
      from:'stairhall_s'+storey, to:'corridor_s'+storey, kind:'open', storey, core:true });
    // the lift car opens east into the stair hall
    doors.push({ axis:'y', plane:lift.x1, c:(lift.y0+lift.y1)/2, clearW:1.10,
      from:'lift_s'+storey, to:'stairhall_s'+storey, kind:'lift', storey, core:true });
    part('y', lift.x1, lift.y0, lift.y1, [[(lift.y0+lift.y1)/2-0.55, (lift.y0+lift.y1)/2+0.55]]);
    part('x', lift.y0, lift.x0, lift.x1, []);
    part('x', lift.y1, lift.x0, lift.x1, []);
    return { rooms, parts, doors, stair, lift };
  }

  // one flight per storey, in the stair hall, rising toward the rear
  // EXACT FLIGHT: rise is the unrounded riseTotal/steps the drawing already climbs, so
  // steps x rise === floorRise to the bit. Rounding it to 3 dp lost 0.5 mm a step and left the top
  // tread below the plate it lands on — the manor's 18 x 0.197 = 3.546-vs-3.55 defect, restated.
  function coreStair(b, storey){
    const st=b.sh.core.stair, sh=b.sh;
    const STEPS=15, run=(st.y1-st.y0-0.30)/STEPS;
    const x0=st.x1-1.18, x1=st.x1-0.14;
    const floorRise=sh.ceilH+sh.floorT;
    const f={ storey, x0:r3(x0), x1:r3(x1), y0:r3(st.y0+0.15), y1:r3(st.y0+0.15+STEPS*run),
      steps:STEPS, rise:floorRise/STEPS, run:r3(run), floorRise,
      voidY0:r3(st.y0+0.15), voidY1:r3(st.y0+0.15+STEPS*run*0.55), dir:'rises_to_front', core:true };
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
    const out=[], sh=b.sh, T=b.T, lux=b.tier==='luxury';
    const ctx={ blockers:[], anchors:[], openings:[], mats:{}, clear:[], taken:[], rejects:[],
                clearAll:[], voidsAll:[], pn:0, storey:0, weather:b.weather, night:b.night };
    const P=planAll(b);
    const ss=storeysShown(b), us=unitsShown(b);
    const showCore = b.focus!=='unit';
    const CUT = sh.ceilH*b.cutFrac, partT=b.partT;
    const floorT=sh.floorT;
    const boardT = lux?boardTex(0.21):vinylTex();
    const wetT   = lux?terrazzoTex():tileTex(0.31);
    const hardT  = lux?terrazzoTex():tileTex(0.24);
    // PER-ROOM MATERIAL CONTRAST (manor precedent). A plate finished in one board from the lobby to
    // the back of the last bedroom reads as one undifferentiated room at this scale. Each kind takes
    // the material it would really have: boards where you live, tile where it is wet, the harder
    // shared floor through the circulation. No hue is invented — these are the tier's own ramps.
    const floorMatOf=(r)=>{
      if(r.wet || r.kind==='bath' || r.kind==='ensuite' || r.kind==='laundry') return ['wet', wetT];
      if(r.kind==='kitchen') return lux ? ['floor', boardT] : ['wet', wetT];
      if(r.kind==='corridor'||r.kind==='lobby'||r.kind==='stairhall'||r.kind==='landing'||
         r.kind==='lockers'||r.kind==='store') return ['conc', hardT];
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
      const shaft = showCore ? { x0:sh.core.lift.x0+partT, x1:sh.core.lift.x1-partT,
                                 y0:sh.core.lift.y0+partT, y1:sh.core.lift.y1-partT, tag:'the lift shaft' } : null;
      if(shaft) voids.push(shaft);
      for(const v of voids) if(v) ctx.voidsAll.push(Object.assign({storey:s}, v));

      // ---- rooms on this storey
      const rooms=[];
      for(const U of us) if(U.storey===s) rooms.push(...P.layouts[U.id].rooms);
      if(showCore){
        rooms.push(...core.rooms);
        rooms.push({ id:'corridor_s'+s, kind:'corridor', core:true, storey:s, route:true,
          x0:sh.corridor.x0, x1:sh.corridor.x1, y0:sh.corridor.y0, y1:sh.corridor.y1,
          area:r3((sh.corridor.x1-sh.corridor.x0)*(sh.corridor.y1-sh.corridor.y0)) });
      }
      // ---- parts + doors on this storey
      const parts=[], doors=[];
      for(const U of us) if(U.storey===s){ parts.push(...P.layouts[U.id].parts); doors.push(...P.layouts[U.id].doors); }
      if(showCore){ parts.push(...core.parts); doors.push(...core.doors); }

      // ---- keep-clear: walls, doorways, the flight, the wells, the shaft, THE LANDINGS.
      // The landings go in before any furniture: the floor you step off a flight onto is route, and
      // a chest of drawers standing on it is a stair nobody can use.
      const landings=[];
      if(showCore){
        if(flight) landings.push(flight.landings.find(l=>l.end==='bottom'));
        if(below)  landings.push(below.landings.find(l=>l.end==='top'));
      }
      PL.clearFor(ctx, { parts, doors, partT,
        voids: below&&showCore?[PL.stairVoid(below)]:[], flights: flight&&showCore?[flight]:[],
        landings, shafts: shaft?[shaft]:[] });
      for(const c of ctx.clear) ctx.clearAll.push(Object.assign({storey:s}, c));

      // ---- floor plates, one per room, each in its own material
      for(const r of rooms){
        if(r.kind==='lift') continue;
        const [fm, ft] = floorMatOf(r);
        PL.slabMinus(out, r.x0,r.x1, r.y0,r.y1, zF, fm, 0.10, ft, voids);
      }
      // joist band under the plate, round the shown extent
      const ext = rooms.reduce((a,r)=>({ x0:Math.min(a.x0,r.x0), x1:Math.max(a.x1,r.x1),
        y0:Math.min(a.y0,r.y0), y1:Math.max(a.y1,r.y1) }), {x0:1e9,x1:-1e9,y0:1e9,y1:-1e9});
      wallQ(out, ext.x0, ext.y1, ext.x1, ext.y1, zF-floorT, zF-0.002, 'soffit', null, -0.15);
      wallQ(out, ext.x1, ext.y0, ext.x0, ext.y0, zF-floorT, zF-0.002, 'soffit', null, -0.15);
      wallQ(out, ext.x1, ext.y1, ext.x1, ext.y0, zF-floorT, zF-0.002, 'soffit', null, -0.15);
      wallQ(out, ext.x0, ext.y0, ext.x0, ext.y1, zF-floorT, zF-0.002, 'soffit', null, -0.15);

      // ---- the facade wall, inside face. Luxury leaves it as exposed brick — the loft signature.
      for(const U of us) if(U.storey===s){
        const L=P.layouts[U.id], bx=L.box, fy=bx.yFa, nrm=-bx.sgn;
        decalY(out, fy, nrm, Math.min(bx.x0,bx.x1), Math.max(bx.x0,bx.x1), zF, zF+CUT,
               lux?'facadeWall':'wall', 0.15, lux?brickTex():null, false, 0.02);
        if(U.glazed.end){
          const ex = bx.x1 > bx.x0 ? bx.x1 : bx.x0;
          decalX(out, ex, 1, Math.min(bx.yC,fy), Math.max(bx.yC,fy), zF, zF+CUT,
                 lux?'facadeWall':'wall', 0.10, lux?brickTex():null, false, 0.02);
        }
      }
      // ---- partitions + door leaves, this storey only
      for(const p of parts) if(p.storey===s) PL.drawPartition(out, ctx, p, zF, zF+CUT, partT, 'wall', 0.22, 0.16);
      for(const d of doors) if(d.storey===s && d.kind!=='entry' && d.kind!=='open' && d.kind!=='lift')
        PL.drawLeaf(out, d, zF, CUT-0.10, 'join', partT);

      // ---- furniture
      if(b.furnish){
        for(const U of us) if(U.storey===s){
          const L=P.layouts[U.id];
          for(const r of L.rooms) furnishFlat(out, ctx, PL, r, b, zF, U, L);
        }
        if(showCore) for(const r of core.rooms) furnishCore(out, ctx, PL, r, b, zF);
      }

      // ---- the stair, and the lift car
      if(showCore){
        if(flight) drawFlight(out, ctx, PL, flight, b, zF, sh);
        drawLift(out, ctx, PL, b, zF, s, sh);
        const C=sh.core;
        PL.A(ctx,'stair','stairhall_s'+s, (C.stair.x0+C.stair.x1)/2, C.stair.y1-0.5, zF, {end:'landing'});
        PL.A(ctx,'lift','lift_s'+s, C.lift.x1-0.55, (C.lift.y0+C.lift.y1)/2, zF, {side:'car'});
        PL.A(ctx,'lift','stairhall_s'+s, C.lift.x1+0.62, (C.lift.y0+C.lift.y1)/2, zF, {side:'call'});
      }
      for(const U of us) if(U.storey===s){
        const L=P.layouts[U.id];
        PL.A(ctx,'entry', U.id+'_hall', U.openings.entry.x, U.openings.entry.y + (U.interior.inward*0.55), zF);
        if(U.openings.loggiaBox){
          const lb=U.openings.loggiaBox, lx=(lb.x0+lb.x1)/2;
          PL.A(ctx,'balcony', U.id+'_living', lx, lb.y + lb.inward*0.75, zF, {kind:'loggia'});
        }
      }
      ctx.openings.push(...doors.filter(d=>d.storey===s).map(d=>({ axis:d.axis, plane:r3(d.plane),
        c:r3(d.c), clearW:d.clearW, from:d.from, to:d.to, kind:d.kind, storey:s })));
    }
    return { faces:out, ctx, plan:P };
  }

  // ---- flights, cars ------------------------------------------------------
  function drawFlight(out, ctx, PL, st, b, z, sh){
    const n=st.steps, run=(st.y1-st.y0)/n, rise=(sh.ceilH+sh.floorT)/n;
    for(let i=0;i<n;i++){
      const y0=st.y0+i*run, y1=y0+run, zz=z+rise*(i+1);
      PL.P(out,ctx,{x0:st.x0,x1:st.x1, y0,y1, z0:Math.max(z, zz-rise-0.02), z1:zz,
        mat:'conc', tex:treadTex(), b:0.05, topB:0.42, blocker:false});
    }
    ctx.blockers.push({kind:'stair', storey:st.storey, x0:st.x0,x1:st.x1,y0:st.y0,y1:st.y1,h:r3(sh.ceilH)});
    // balusters every second tread plus a continuous raked handrail — posts alone read as loose rails
    const hx=st.x0-0.03, RH=0.92;
    for(let i=0;i<=n;i+=2){ const y=st.y0+i*run, zz=z+rise*(i+1);
      PL.P(out,ctx,{x0:hx-0.028,x1:hx+0.028, y0:y-0.01,y1:y+0.05, z0:Math.max(z,zz-rise), z1:zz+RH,
        mat:'metal', blocker:false}); }
    const zBot=z+rise+RH, zTop=z+rise*n+RH;
    out.push(F([[hx-0.035, st.y0, zBot],[hx+0.035, st.y0, zBot],
                [hx+0.035, st.y1, zTop],[hx-0.035, st.y1, zTop]], 'metal', 0.55, 0, null, null));
    out.push(F([[hx+0.035, st.y0, zBot-0.055],[hx+0.035, st.y1, zTop-0.055],
                [hx+0.035, st.y1, zTop],[hx+0.035, st.y0, zBot]], 'metal', 0.10, 0, null, null));
    PL.A(ctx,'stair','stairhall_s'+st.storey, (st.x0+st.x1)/2, st.y0+0.25, z, {end:'bottom'});
    PL.A(ctx,'stair','stairhall_s'+st.storey, (st.x0+st.x1)/2, st.y1-0.25, z+(sh.ceilH+sh.floorT), {end:'top'});
  }
  function drawLift(out, ctx, PL, b, z, storey, sh){
    const L=sh.core.lift, h=Math.min(2.30, sh.ceilH-0.12);
    // shaft walls read as a lined void; the car sits at this storey
    for(const xv of [L.x0]) decalX(out, xv, 1, L.y0, L.y1, z, z+h, 'metal', -0.35, null, false, 0.02);
    slab(out, [[L.x0,L.y0],[L.x1,L.y0],[L.x1,L.y1],[L.x0,L.y1]], z+0.02, 'metal', -0.10);
    boxSolid(out, L.x0+0.06, L.x1-0.06, L.y0+0.06, L.y1-0.06, z+0.02, z+0.06, 'metal', null, 0.15, 0.3);
    for(const yv of [L.y0, L.y1]) decalY(out, yv, yv<0?1:-1, L.x0+0.04, L.x1-0.04, z+0.04, z+h-0.06, 'metal', -0.20, null, false, 0.03);
    // car doors, part open, on the east face
    const cyc=(L.y0+L.y1)/2;
    decalX(out, L.x1, 1, L.y0+0.06, cyc-0.24, z+0.04, z+h-0.10, 'metal', 0.45, null, true, 0.05);
    decalX(out, L.x1, 1, cyc+0.24, L.y1-0.06, z+0.04, z+h-0.10, 'metal', 0.45, null, true, 0.05);
    decalX(out, L.x1, 1, cyc-0.24, cyc+0.24, z+0.04, z+h-0.10, 'cavity', 0, null, true, 0.06);
    // call panel beside the doors, in the stair hall
    decalX(out, L.x1+0.34, 1, cyc+0.52, cyc+0.70, z+1.02, z+1.34, 'metal', 0.8, null, true, 0.07);
    ctx.blockers.push({kind:'lift', storey, x0:r3(L.x0),x1:r3(L.x1),y0:r3(L.y0),y1:r3(L.y1),h:r3(h)});
  }

  // ---- furnishing: the tier picks from PropIso, the placer owns where -------
  function furnishFlat(out, ctx, PL, r, b, z, U, L){
    const T=b.T, K=T.kit, lux=b.tier==='luxury';
    const put=(name,opts,pl)=>PL.put(out,ctx,r,z,name,opts,Object.assign({room:r.id},pl||{}));
    const any=(name,opts,walls,pl)=>PL.putAny(out,ctx,r,z,name,opts,walls,Object.assign({room:r.id},pl||{}));
    const A=(verb,x,y,extra)=>PL.A(ctx,verb,r.id,x,y,z,extra);
    // ROOM-SIZED RUG (manor precedent). A 0.9 m mat adrift in the middle of a floor reads as a
    // stain; a rug that runs the room anchors the furniture standing on it. fitRun shrinks the run
    // to the wall it is given, so a length past the room width simply fills it. Laid with
    // ignoreClear so it passes under the furniture rather than fighting it for the slot.
    const rug=(fx,fy,pad)=>PL.put(out,ctx,r,z,'rug',
      {len:Math.max(0, (r.x1-r.x0)-(pad!=null?pad:0.62)), fabric:K.fabric, fabric2:K.fabric2},
      {room:r.id, wall:'free', fx:fx!=null?fx:0.5, fy:fy!=null?fy:0.5, ignoreClear:true});
    // which edge is the facade, in wall terms, so a headboard can avoid the glazing
    const facadeWall = (r.y1 >= Math.max(L.box.yC, L.box.yFa) - 0.01) ? 'front' : 'rear';
    const corridorWall = facadeWall==='front' ? 'rear' : 'front';

    switch(r.kind){
      case 'living': {
        // the dining table goes in FIRST, at the end nearest the kitchen (a corridor flat has no
        // separate dining room), so the sitting group takes the far end rather than the middle
        if(r.area>16.5){
          const tb = put('table', {len:0.15, wood:K.wood}, {wall:'free', fx:0.26, fy:0.30});
          if(tb){ const n=(tb.x1-tb.x0)>1.5?3:2;
            for(let i=0;i<n;i++){ const tx=tb.x0+(tb.x1-tb.x0)*(i+0.5)/n;
              for(const sg of [-1,1]){ const ty = sg<0? tb.y0-0.30 : tb.y1+0.30;
                if(ty<r.y0+0.28||ty>r.y1-0.28) continue;
                PL.put(out,ctx,{x0:tx-0.30,x1:tx+0.30,y0:ty-0.24,y1:ty+0.24}, z, 'chair',
                  {wood:K.wood, variant:1, fabric:K.fabric}, {wall:'free', face: sg<0?'S':'N', room:r.id});
                A('dine', tx, ty, {seat:i+(sg<0?'a':'b')}); } } }
        }
        const sofa = any('sofa', {len:0.35, fabric:K.fabric, fabric2:K.fabric2, wood:K.wood},
                         [corridorWall,'left','right'], {along:0.82, inset:0.14});
        if(sofa){ const n=Math.max(2, Math.round(Math.max(sofa.x1-sofa.x0, sofa.y1-sofa.y0)/0.74));
          const horiz=(sofa.x1-sofa.x0)>(sofa.y1-sofa.y0);
          for(let i=0;i<n;i++) A('sit',
            horiz? sofa.x0+(sofa.x1-sofa.x0)*(i+0.5)/n : sofa.x0-0.34,
            horiz? (facadeWall==='front'? sofa.y1+0.34 : sofa.y0-0.34) : sofa.y0+(sofa.y1-sofa.y0)*(i+0.5)/n,
            {seat:i}); }
        const ac = any('armchair', {fabric:K.fabric, fabric2:K.fabric2, wood:'walnut'}, ['left','right'], {along:0.82});
        if(ac) A('sit', ac.x1+0.36, (ac.y0+ac.y1)/2, {seat:'armchair'});
        if(r.area>13.0) rug(0.5, 0.66);
        if(r.area>15.0) put('roundTable', {wood:K.wood}, {wall:'free', fx:0.48, fy:0.74});
        break;
      }
      case 'kitchen': {
        // a galley: counter run and cooker on the party wall, sink and fridge opposite
        const ct = any('counter', {len:0.15, paint:K.paint, wood:'pine', worktop:K.worktop},
                       ['left','right'], {along:0.0, margin:0.12});
        const st = any(K.cook, {wood:'oak'}, ['left','right'], {along:1.0, margin:0.12});
        if(st) A('cook', (st.x0+st.x1)/2 + ((st.x0-r.x0)<(r.x1-st.x1)?0.52:-0.52), (st.y0+st.y1)/2);
        const sk = any(K.sink, {paint:K.paint, wood:'pine', worktop:K.worktop},
                       ['right','left',corridorWall], {along:0.28});
        if(sk) A('wash_dishes', (sk.x0+sk.x1)/2 + ((sk.x0-r.x0)<(r.x1-sk.x1)?0.50:-0.50), (sk.y0+sk.y1)/2);
        const fr = any(K.cold, {paint: lux?'steel':'white', wood:'oak'}, ['right','left',corridorWall], {along:0.86});
        if(fr) A('store', (fr.x0+fr.x1)/2, (fr.y0+fr.y1)/2 + 0.55, {what:'fridge'});
        decalY(out, r.y0, 1, r.x0+0.10, r.x1-0.10, z+0.92, z+(lux?1.48:1.40), 'wallTile', 0.20, subwayTex(), false, 0.05);
        break;
      }
      case 'bed': {
        const variant = r.primary ? 0 : ((r.x1-r.x0)<2.65 ? 1 : 0);
        if(r.area>8.5) rug(0.5, 0.52, 0.88);
        // the headboard wants a SOLID wall: the facade is glazing and the corridor wall carries the
        // door, so the party walls come first
        const bd = any('bed', {variant, wood:K.wood, fabric: lux?'sage':'blue', fabric2:'cream'},
                       ['left','right',corridorWall,facadeWall], {along:0.5, inset:0.16});
        if(bd){
          const alongY=(bd.y1-bd.y0)>(bd.x1-bd.x0);
          const size = ['double','single','wide'][variant];
          // ONE SLEEP ANCHOR PER SIDE YOU CAN ACTUALLY GET TO. A double used to claim both sides
          // whatever the room did, so a bed pushed into a 2.4 m alcove published a standing spot
          // inside the party wall and resident_slots counted a person who could never reach a bed.
          // A side earns its anchor only if the gap between the mattress and the room edge takes a
          // body: 0.22 m of shoulder plus the 0.36 m reach the anchor sits at.
          const NEED=0.60;
          const gap=(sd)=>alongY ? (sd==='left'? bd.x0-r.x0 : r.x1-bd.x1)
                                 : (sd==='left'? bd.y0-r.y0 : r.y1-bd.y1);
          let sides=(variant===1?['right']:['left','right']).filter(sd=>gap(sd)>=NEED);
          if(!sides.length){
            // an alcove bed: no side clears, so the FOOT is the way in and there is one sleeper
            const fx=alongY ? (bd.x0+bd.x1)/2 : (r.x1-bd.x1>bd.x0-r.x0 ? bd.x1+0.36 : bd.x0-0.36);
            const fy=alongY ? (r.y1-bd.y1>bd.y0-r.y0 ? bd.y1+0.36 : bd.y0-0.36) : (bd.y0+bd.y1)/2;
            A('sleep', clamp(fx, r.x0+0.28, r.x1-0.28), clamp(fy, r.y0+0.28, r.y1-0.28),
              {side:'foot', size, approach:'foot_only'});
            sides=[];
          }
          const pads=[];
          for(const sd of sides){
            const ax = alongY ? (sd==='left'? bd.x0-0.36 : bd.x1+0.36) : (bd.x0+bd.x1)/2;
            const ay = alongY ? (bd.y0+bd.y1)/2 : (sd==='left'? bd.y0-0.36 : bd.y1+0.36);
            const px=clamp(ax, r.x0+0.28, r.x1-0.28), py=clamp(ay, r.y0+0.28, r.y1-0.28);
            A('sleep', px, py, {side:sd, size}); pads.push([px,py]);
          }
          // hold those pads: the wardrobe and the dresser come next and would otherwise stand on
          // the way in to the bed they are meant to serve
          PL.reserveStand(ctx, pads, 0.26, 'the way in to the bed');
        }
        const wd = any('wardrobe', {paint:K.paint, wood:'pine'}, [corridorWall,'left','right'], {along:0.12});
        if(wd){ const wx=(wd.x0+wd.x1)/2, wy=(wd.y0+wd.y1)/2 + (facadeWall==='front'?0.52:-0.52);
          A('store', wx, wy, {what:'wardrobe'});
          PL.reserveStand(ctx, [[wx,wy]], 0.26, 'the way in to the wardrobe'); }
        if(r.area>9.5) any('dresser', {paint: lux?'sage':'blue', wood:'pine'}, ['right','left',corridorWall], {along:0.85});
        break;
      }
      case 'bath': case 'ensuite': {
        const wc = any('toilet', {variant:K.wc}, [corridorWall,'left','right'], {along:0.06});
        if(wc) A('toilet', (wc.x0+wc.x1)/2, (wc.y0+wc.y1)/2 + (facadeWall==='front'?0.44:-0.44));
        const vn = any('vanity', {variant:(r.x1-r.x0)<1.75?2:0, len:0.0, paint:K.paint, wood:'pine', worktop:K.worktop},
                       [corridorWall,'right','left'], {along:1.0});
        if(vn) A('vanity', (vn.x0+vn.x1)/2, (vn.y0+vn.y1)/2 + (facadeWall==='front'?0.44:-0.44), {basin:0});
        // A small flat's bathroom cannot hold a WC, a basin AND a 1.6 m bath. Ask for the tier's
        // preferred fixture, then fall back to a cubicle rather than leaving the room with nothing
        // to wash in — and clear the rejects, because the fallback is a design answer, not a miss.
        const mark = ctx.rejects.length;
        let fx = null, kind = null;
        if(!T.shower){ fx = any('tub', {variant:1}, [facadeWall,'left','right'], {along:0.5}); if(fx) kind='tub'; }
        if(!fx){ fx = any('shower', {worktop:'greentile'}, [facadeWall,'left','right'], {along:0.04}); if(fx) kind='shower'; }
        if(fx){ ctx.rejects.length = mark;
          A('bathe', (fx.x0+fx.x1)/2, (fx.y0+fx.y1)/2 + (facadeWall==='front'?-0.46:0.46), {fixture:kind}); }
        any('mirror', {wood: lux?'driftwood':'walnut'}, ['left','right',corridorWall], {along:0.5});
        decalY(out, r.y0, 1, r.x0+0.05, r.x1-0.05, z+0.02, z+1.28, 'wallTile', 0.16, subwayTex(), false, 0.04);
        break;
      }
      case 'laundry': {
        const wm = any('washer', {variant: r.area<3.6?1:0, paint:'white'}, [corridorWall,'left','right'], {along:0.06});
        if(wm) A('laundry', (wm.x0+wm.x1)/2, (wm.y0+wm.y1)/2 + (facadeWall==='front'?0.44:-0.44));
        if(r.area>5.6) any('counter', {len:0.0, paint:K.paint, wood:'pine', worktop:K.worktop}, [corridorWall,'right','left'], {along:1.0});
        any('shelf', {len:0.2, wood:'pine'}, ['left','right'], {along:0.5});
        break;
      }
      case 'store': {
        any('shelf', {len:0.3, wood:'pine'}, ['left','right',corridorWall], {along:0.5});
        any('crate', {wood:'pine'}, [corridorWall,facadeWall], {along:0.88});
        A('store', (r.x0+r.x1)/2, (r.y0+r.y1)/2, {what:'unit_store'});
        break;
      }
      case 'hall': {
        if((r.x1-r.x0)>1.55 && (r.y1-r.y0)>2.2)
          any('shelf', {len:0.0, wood:'pine'}, ['left','right'], {along:0.85});
        break;
      }
      default: break;      // internal corridor stays clear — it is the route, not a room
    }
  }

  function furnishCore(out, ctx, PL, r, b, z){
    const lux=b.tier==='luxury', K=b.T.kit;
    const any=(name,opts,walls,pl)=>PL.putAny(out,ctx,r,z,name,opts,walls,Object.assign({room:r.id},pl||{}));
    const A=(verb,x,y,extra)=>PL.A(ctx,verb,r.id,x,y,z,extra);
    switch(r.kind){
      case 'lobby': {
        // the mail wall is what makes a lobby a lobby, and what a postal routine needs
        const mb = any('mailboxes', {len:0.7}, ['left','right','front'], {along:0.30});
        if(mb) A('mail', (mb.x0+mb.x1)/2, (mb.y0+mb.y1)/2 - 0.62, {what:'mailboxes'});
        const bn = any(lux?'bench':'bench', {len:0.15, wood:lux?'walnut':'pine'}, ['right','left'], {along:0.78});
        if(bn) for(let i=0;i<2;i++) A('sit', bn.x0+(bn.x1-bn.x0)*(i+0.5)/2, (bn.y0+bn.y1)/2 - 0.36, {seat:i});
        if(lux){
          const ac = any('armchair', {fabric:K.fabric, fabric2:K.fabric2, wood:'walnut'}, ['front','left'], {along:0.18});
          if(ac) A('sit', (ac.x0+ac.x1)/2, (ac.y0+ac.y1)/2 - 0.38, {seat:'lobby'});
          any('rug', {len:0.3, fabric:K.fabric, fabric2:K.fabric2}, ['free'], {fx:0.5, fy:0.5, ignoreClear:true});
        }
        break;
      }
      case 'laundry': {
        // the shared laundry: the basic tier has no machine in the flat, so this room is on a route
        const n = (r.x1-r.x0) > 4.2 ? 3 : 2;
        for(let i=0;i<n;i++){
          const wm = PL.put(out,ctx,r,z,'washer',{variant:0, paint:'white'},
            {wall:'front', along:i/(n-1||1), room:r.id});
          if(wm) A('laundry', (wm.x0+wm.x1)/2, wm.y1+0.46, {machine:i});
        }
        any('counter', {len:0.5, paint:'white', wood:'pine', worktop:'slate'}, ['rear','left','right'], {along:0.5});
        any('bench', {len:0.0, wood:'pine'}, ['left','right'], {along:0.85});
        break;
      }
      case 'lockers': {
        for(const wl of ['front','rear']){
          const lk = PL.put(out,ctx,r,z,'lockers',{len:0.6}, {wall:wl, along:0.5, room:r.id});
          if(lk) A('store', (lk.x0+lk.x1)/2, wl==='front'? lk.y1+0.52 : lk.y0-0.52, {what:'locker_bank'});
        }
        any('crate', {wood:'pine'}, ['left','right'], {along:0.5});
        break;
      }
      case 'store': {
        any('lockers', {len:0.3}, ['rear','front'], {along:0.5});
        any('shelf', {len:0.2, wood:'pine'}, ['left','right'], {along:0.5});
        A('store', (r.x0+r.x1)/2, (r.y0+r.y1)/2, {what:'core_store'});
        break;
      }
      default: break;      // lobby landing, stair hall, corridor and lift stay clear
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
      const rnd=mulberry32(5521);
      for(let i=0;i<N;i++){ const m=nbuf[i]; if(!m||!rbuf[i]) continue;
        if((m==='floor'||m==='wall'||m==='facadeWall') && rnd()<wx*0.05)
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
  // roomH and storeyRise are on BOTH dims() and anchors(), the cottage-room contract: the game
  // builds walls, ceiling, the stair opening and the colliders from these two numbers, and until a
  // rig published them the baker guessed and got a different figure from the one the rig drew.
  //   storeyRise  floor-to-floor rise to the storey above == storeyZ[s+1] - storeyZ[s]
  //                                                       == stair floorRise == steps x rise
  //               null on the top storey (the key is present, the value is null)
  //   roomH       clear floor-to-ceiling height == storeyRise - floorT. This is the wall plate the
  //               rig actually draws, not a guess made from it.
  function heightsOf(b, s){
    const sh=b.sh, top=sh.storeys-1;
    const rise = s<top ? r3(sh.storeyZ[s+1]-sh.storeyZ[s]) : null;
    return { storeyRise:rise, roomH:r3(sh.ceilH) };
  }
  function dims(opts){ const b=resolve(opts||{});
    const sel=storeysShown(b), s0=sel[0]!=null?sel[0]:0, hh=heightsOf(b,s0);
    return { tier:b.tier, storeys:b.sh.storeys, units:b.sh.units.length, focus:b.focus,
      ceilH:b.sh.ceilH, floorT:b.sh.floorT, storeyZ:b.sh.storeyZ, partT:b.partT,
      storey:s0, roomH:hh.roomH, storeyRise:hh.storeyRise,
      roomHByStorey:b.sh.storeyZ.map((_,s)=>heightsOf(b,s).roomH),
      storeyRiseByStorey:b.sh.storeyZ.map((_,s)=>heightsOf(b,s).storeyRise),
      floorAtPivot:b.floorAtPivot,
      corridor:b.sh.corridor, core:b.sh.core }; }
  function plan(opts){ const b=resolve(opts||{}), P=planAll(b);
    return { storeys:b.sh.storeys,
      units:b.sh.units.map(U=>({ id:U.id, storey:U.storey, side:U.side, beds:U.beds,
        bedsBuilt:P.layouts[U.id].bedsBuilt, bedsRequested:P.layouts[U.id].bedsRequested,
        baths:P.layouts[U.id].baths, split:P.layouts[U.id].split, rooms:P.layouts[U.id].rooms,
        thresholds:P.layouts[U.id].doors })),
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
      contract:'32 px = 1 m · same origin, camera and pivot as Art/walkupIsoRig.js',
      tier:b.tier, focus:b.focus, storeys:b.sh.storeys,
      storeysShown:ss, blockUnits:b.sh.units.length,
      units:us.map(U=>({ id:U.id, storey:U.storey, side:U.side, col:U.col, built:true,
        beds:U.beds, bedsBuilt:P.layouts[U.id].bedsBuilt, bedsRequested:P.layouts[U.id].bedsRequested,
        baths:P.layouts[U.id].baths, interior:U.interior, glazed:U.glazed, split:P.layouts[U.id].split,
        rooms:P.layouts[U.id].rooms, thresholds:P.layouts[U.id].doors })),
      cores: (b.focus==='unit' ? [] : ss.map(s=>({ storey:s, rooms:P.cores[s].rooms, thresholds:P.cores[s].doors }))),
      corridor:b.sh.corridor, stairs:P.stairs.filter(v=>ss.indexOf(v.storey)>=0),
      anchors:ctx.anchors, blockers:ctx.blockers, openings:ctx.openings,
      unplaced:ctx.rejects, keepClear:ctx.clearAll, stairVoids:ctx.voidsAll,
      counts:{ rooms:(function(){ let n=0; for(const s of storeysShown(b)){
                 for(const U of unitsShown(b)) if(U.storey===s) n+=P.layouts[U.id].rooms.length;
                 if(b.focus!=='unit') n += P.cores[s].rooms.length+1; } return n; })(),
               anchors:ctx.anchors.length,
               blockers:ctx.blockers.length, verbs },
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
  // Terrace precedent, now through the shared writer: the room rig owns SOLE, the interior
  // THRESHOLDs, STAIRS, INTERACT and BLOCKERS, because a dwelling's routes are all indoors.
  // WalkupIso.gameplayAll() merges these and stamps this file's hash beside its own.
  function gameplaySections(opts){
    const BG=root.BuildingGameplay; if(!BG) return null;
    const b=resolve(Object.assign({}, opts||{}, {focus:'all', storey:'stack', floorAtPivot:false}));
    const built=build(b), ctx=built.ctx, P=built.plan, sh=b.sh;
    const rooms=[], doors=[], stairs=[];
    // A walk-up stacks units AND puts a front and a rear flat at the same x, so an x-only lookup
    // hands half the building to the wrong household. Match on all three: storey, x span, and
    // which side of the corridor the point falls.
    const unitAt=(x,y,storey)=>sh.units.find(v=>v.storey===storey &&
        x>=v.interior.x0-0.01 && x<=v.interior.x1+0.01 &&
        y>=Math.min(v.interior.yCorridor,v.interior.yFacade)-0.01 &&
        y<=Math.max(v.interior.yCorridor,v.interior.yFacade)+0.01) || null;
    const unitIdAt=(x,y,storey)=>{ const u=unitAt(x,y,storey); return u?u.id:null; };
    for(const U of sh.units){
      const L=P.layouts[U.id];
      for(const r of L.rooms) rooms.push(Object.assign({}, r, {unit:U.id, uid:U.id+'.'+r.id}));
      for(const d of L.doors) doors.push(Object.assign({}, d, {unit:U.id}));
    }
    for(let s=0;s<sh.storeys;s++){
      const C=P.cores[s];
      for(const r of C.rooms) rooms.push(Object.assign({}, r, {unit:null, uid:r.id, core:true}));
      for(const d of C.doors) doors.push(Object.assign({}, d, {unit:null, core:true}));
      rooms.push({ id:'corridor_s'+s, uid:'corridor_s'+s, kind:'corridor', core:true, storey:s,
        route:true, unit:null, x0:sh.corridor.x0, x1:sh.corridor.x1,
        y0:sh.corridor.y0, y1:sh.corridor.y1,
        area:r3((sh.corridor.x1-sh.corridor.x0)*(sh.corridor.y1-sh.corridor.y0)) });
    }
    for(const st of P.stairs) stairs.push(Object.assign({}, st, {unit:null, core:true,
      id:'core.stair_s'+st.storey }));
    const blk=ctx.blockers.map(q=>Object.assign({ unit:unitIdAt((q.x0+q.x1)/2,(q.y0+q.y1)/2,q.storey) }, q));
    const anch=ctx.anchors.map(a=>Object.assign({ unit:unitIdAt(a.x,a.y,a.storey) }, a));
    return BG.sections({ rooms, doors, stairs, voids:ctx.voidsAll, blockers:blk, anchors:anch,
      rejects:ctx.rejects, storeyZ:sh.storeyZ, ceilH:sh.ceilH,
      roomH:(s)=>heightsOf(b,s).roomH, storeyRise:(s)=>heightsOf(b,s).storeyRise,
      unitOf:unitIdAt, interiorRig:'Art/walkupUnitIsoRig.js' });
  }

  function project(dir, p, elev){ const v=projVert(p[0],p[1],p[2],camBasis({dir,elev})); return {x:v.sx,y:v.sy}; }

  root.WalkupUnitIso = { W, H, PX, pivot:{x:cx,y:groundY}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'], FINISHES, TIERS, ROOMKINDS, VERBS, PRESETS, KEY,
    dims, plan, rooms, interior, gameplaySections, render, anchors, project };
})(typeof globalThis!=='undefined'?globalThis:window);
