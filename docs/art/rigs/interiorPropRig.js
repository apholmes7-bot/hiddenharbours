/* Hidden Harbours — parametric ISO INTERIOR-PROP rig (ADR-0006 bake pipeline, SAME turntable +
   camera + shading as interiorIsoRig.js / houseIsoRig.js / the fleet). Free-standing FURNITURE &
   APPLIANCES the compositor drops onto the interior floor grid (Phase 3). Each prop is one
   parametric 3D model baked to pixel sheets through the SHARED 3/4 camera: 45deg steps, elev 40deg,
   flat-facet shading from the fixed upper-LEFT key, z-buffered, ordered dither, per-face uv texture,
   depth-edge darkening, 1px keyline, NO AA. 32 px = 1 m. All 8 facings fall out of one model, so a
   prop rotates in lockstep with the room and the villagers standing beside it.

   PROP ORIGIN: floor-centre of the prop footprint (== the room-rig pivot), so PropIso.render(name,dir)
   drops straight onto InteriorIso's floor point with no offset maths. Front face is -Y.

   CATALOGUE (name -> spec): seating(chair,stool,bench,armchair) · tables(table,roundTable) · bed ·
   storage(dresser,wardrobe,shelf,seaChest,barrel,crate) · decor(rug) · KITCHEN(counter,stove,sink,
   icebox,hutch). Each reads a resolved spec:
     wood:'pine'|'oak'|'walnut'|'driftwood'   paint:BODY key|null (painted body overrides wood)
     fabric/fabric2:BODY key (quilts, cushions, rugs)   variant:int   len:0..1 (runs: counter/bench/shelf)
     weather:0..1 (scuffs)   night:bool (warm-lamp tint to match the room)
   Exposes globalThis.PropIso = { W,H,PX,pivot,order,defaultElev, WOODS,IRON,TIN,BRASS,CERAMIC,BODY,
     TRIM,PROPS,CATS, list(), footprint(name,opts), render(name,dir,opts), project(dir,p,elev) }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 460, H = 460, cx = 230, groundY = 330;
  const DEG = Math.PI / 180, DEFAULT_ELEV = 40;

  // ---- palettes (harbour master ramps, shared with the room + fleet so props match) ----
  const BODY = {
    greyShingle: ['#4c463f','#5d564c','#6f665a','#82786a','#968b7b','#a99d8c'],
    white:       ['#8c928c','#a6aaa2','#bfc2b9','#d5d8cf','#e7e9e0','#f3f4ec'],
    cream:       ['#8a6f3c','#a6884b','#c2a35f','#d8bd7c','#e9d59d','#f5e7c1'],
    red:         ['#4a130f','#671b14','#88271c','#a33124','#bd4230','#d25a42'],
    sage:        ['#3a4636','#4a5843','#5c6b52','#718063','#889777','#a1ae90'],
    blue:        ['#33454a','#43585d','#556d72','#6a848a','#849ea3','#a3b9bd'],
    gold:        ['#5e4a12','#7c6119','#987a26','#b39440','#c8ab5e','#dbc182'],
    plum:        ['#2e2333','#3f3047','#523f5d','#664f73','#7d648b','#9079a1'],
  };
  const WOODS = {
    pine:     ['#6b4e2b','#7e5d34','#8f6d40','#a5824f','#bb9a63','#d0b47e'],
    oak:      ['#553d23','#6b4e30','#7f603b','#957749','#ab8d5f','#c1a578'],
    walnut:   ['#2f2018','#412c20','#54402e','#68533e','#7d6851','#947f66'],
    driftwood:['#4a4d4b','#5d605c','#727571','#898b85','#9fa199','#b5b6ad'],
  };
  const TRIM    = ['#9aa09a','#b4b8b0','#ccd0c7','#e0e2da','#eef0e8','#f8f9f2'];
  const IRON    = ['#111216','#1c1e23','#2a2d33','#3a3e46','#4d525a','#636970']; // cast iron
  const TIN     = ['#565b5f','#6d7276','#868b8f','#a0a5a8','#bbbfc1','#d6d9da']; // galvanised / tin
  const BRASS   = ['#59410f','#795819','#977326','#b28e3b','#c8a757','#dcc17d'];
  const CERAMIC = ['#868b89','#a3a8a4','#c0c4be','#d8dcd4','#ebeee6','#f7f9f2'];
  // countertop finishes (selectable via spec.worktop) — dark→light ramps, keyed to the harbour palette
  const WORKTOPS = {
    slate:      ['#3a3d43','#494d54','#5b6067','#70757c','#868b91','#9ca1a6'],
    soapstone:  ['#22262a','#2f353a','#3f474d','#525b61','#69727a','#828b92'],
    marble:     ['#8f918c','#a9aaa3','#c3c4bb','#d8d9d0','#e9eae1','#f6f7ef'],
    sandstone:  ['#7a6544','#957c53','#ad9366','#c4ac80','#d8c39d','#e9d9bd'],
    butcher:    ['#6b4e2b','#7e5d34','#8f6d40','#a5824f','#bb9a63','#d0b47e'],
    greentile:  ['#26362f','#33473c','#456155','#5c8071','#79a08d','#9abfa9'],
    bluetile:   ['#25353a','#33484f','#456068','#5c8088','#7aa0a8','#9abfc6'],
    redtile:    ['#3f1712','#5c2119','#7a3024','#96412f','#b0553f','#c66d55'],
  };
  const STONE   = ['#3a3d43','#494d54','#5b6067','#70757c','#868b91','#9ca1a6'];
  const GLASSD  = ['#7d9ea6','#a1c2c6','#cfe6e8'], GLASSN=['#141d2b','#233247','#3d5570'];
  const FIRE    = ['#7a2a10','#b5541a','#e59433','#f6cf6a'];
  const KEY     = '#1a1c22';

  // ---- shading (identical recipe to interiorIsoRig) ----
  const GAIN = 3.1, BIAS = 2.55, EDGE = 0.16;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));
  function mulberry32(a){return function(){a|=0;a=a+0x6D2B79F5|0;let t=Math.imul(a^a>>>15,1|a);t=t+Math.imul(t^t>>>7,61|t)^t;return((t^t>>>14)>>>0)/4294967296;};}
  function hex2rgb(h){ return [parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]; }
  function rgb2hex(r,g,b){ const h=(n)=>Math.max(0,Math.min(255,Math.round(n))).toString(16).padStart(2,'0'); return '#'+h(r)+h(g)+h(b); }
  function mix(a,b,t){ const A=hex2rgb(a),B=hex2rgb(b); return rgb2hex(A[0]+(B[0]-A[0])*t,A[1]+(B[1]-A[1])*t,A[2]+(B[2]-A[2])*t); }
  function desat(hex,t){ const [r,g,b]=hex2rgb(hex); const l=0.3*r+0.59*g+0.11*b; return rgb2hex(r+(l-r)*t,g+(l-g)*t,b+(l-b)*t); }
  function hash2(a,b){ let h=(a*374761393 + b*668265263)>>>0; h=(h^(h>>13))*1274126177>>>0; return ((h^(h>>16))>>>0)/4294967296; }

  function camBasis(opts){ const dir=opts.dir||0, th=dir*Math.PI/4, e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    return { th, ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e) }; }
  function projVert(x,y,z,B){ const xr=x*B.ct - y*B.stt, yr=x*B.stt + y*B.ct, zr=z;
    return { xr,yr,zr, sx:cx+xr*S, sy:groundY-(yr*B.se+zr*B.ce)*S, d:(yr*B.ce-zr*B.se) }; }
  function normal(a,b,c){ const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx; const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m]; }
  function shadeOf(n, se, ce){ return n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2]; }

  // ---- face builders (outward-normal winding) ----
  function F(v,mat,b,db,uv,tex,flat){ return { v, mat, b:b||0, db:db||0, uv:uv||null, tex:tex||null, flat:!!flat }; }
  function wall(out, x0,y0,x1,y1, z0,z1, mat, tex, b){ const L=Math.hypot(x1-x0,y1-y0);
    out.push(F([[x0,y0,z0],[x1,y1,z0],[x1,y1,z1],[x0,y0,z1]], mat, b||0, 0, [[0,z0],[L,z0],[L,z1],[0,z1]], tex)); }
  function slab(out, pts, z, mat, b, tex){ const uv=tex?pts.map(p=>[p[0],p[1]]):null;
    out.push(F(pts.map(p=>[p[0],p[1],z]), mat, b||0, 0, uv, tex)); }
  // solid box: -Y front, +Y back, +X right, -X left, top (outward normals)
  function box(out, x0,x1,y0,y1,z0,z1, mat, b, tex, noTop){
    wall(out, x0,y0, x1,y0, z0,z1, mat, tex, b);
    wall(out, x1,y1, x0,y1, z0,z1, mat, tex, b);
    wall(out, x1,y0, x1,y1, z0,z1, mat, tex, b);
    wall(out, x0,y1, x0,y0, z0,z1, mat, tex, b);
    if(!noTop) slab(out, [[x0,y0],[x1,y0],[x1,y1],[x0,y1]], z1, mat, (b||0)+0.28, tex);
  }
  function leg(out, x,y, r, z0,z1, mat, b){ box(out, x-r,x+r, y-r,y+r, z0,z1, mat, b||0); }
  // N-gon prism (round: stools, barrels, pedestals, pump)
  function prism(out, cxp,cyp, r, z0,z1, n, mat, b, texTop){
    const p=[]; for(let i=0;i<n;i++){ const a=(i/n)*Math.PI*2; p.push([cxp+Math.cos(a)*r, cyp+Math.sin(a)*r]); }
    for(let i=0;i<n;i++){ const A=p[i], Bp=p[(i+1)%n]; wall(out, A[0],A[1], Bp[0],Bp[1], z0,z1, mat, null, b); }
    slab(out, p, z1, mat, (b||0)+0.22, texTop||null);
  }
  // decals on a prop face (recessed drawer/door panels, hardware, glass)
  function decalY(out, yv, ny, xs,xe, z0,z1, mat, b, tex, flat, db){ const e=0.02*ny, uw=xe-xs, uh=z1-z0;
    const P = ny>0 ? [[xs,yv+e,z0],[xe,yv+e,z0],[xe,yv+e,z1],[xs,yv+e,z1]] : [[xe,yv+e,z0],[xs,yv+e,z0],[xs,yv+e,z1],[xe,yv+e,z1]];
    out.push(F(P, mat, b||0, db!=null?db:0.05, tex?[[0,0],[uw,0],[uw,uh],[0,uh]]:null, tex||null, flat)); }
  function decalX(out, xv, nx, ys,ye, z0,z1, mat, b, tex, flat, db){ const e=0.02*nx, uw=ye-ys, uh=z1-z0;
    const P = nx>0 ? [[xv+e,ye,z0],[xv+e,ys,z0],[xv+e,ys,z1],[xv+e,ye,z1]] : [[xv+e,ys,z0],[xv+e,ye,z0],[xv+e,ye,z1],[xv+e,ys,z1]];
    out.push(F(P, mat, b||0, db!=null?db:0.05, tex?[[0,0],[uw,0],[uw,uh],[0,uh]]:null, tex||null, flat)); }
  const putOn=(axis)=>(out,plane,nrm,a0,a1,z0,z1,mat,bias,db,tex,flat)=> axis==='y'
      ? decalY(out,plane,nrm,a0,a1,z0,z1,mat,bias,tex||null, flat!==false&&!tex?true:!!flat, db)
      : decalX(out,plane,nrm,a0,a1,z0,z1,mat,bias,tex||null, flat!==false&&!tex?true:!!flat, db);
  // grid of recessed panels (drawers/doors) on the FRONT (-Y) face; knob = brass pull
  function panelsY(out, y, xa,xb, z0,z1, rows,cols, knob){ const put=putOn('y'), m=0.045;
    for(let r=0;r<rows;r++) for(let c=0;c<cols;c++){
      const pa=xa+(xb-xa)*(c/cols)+m, pb=xa+(xb-xa)*((c+1)/cols)-m;
      const q0=z0+(z1-z0)*(r/rows)+m, q1=z0+(z1-z0)*((r+1)/rows)-m;
      put(out,y,-1, pa,pb, q0,q1, 'body', -1.6, 0.05);            // recessed field
      put(out,y,-1, pa,pb, q1-0.02,q1, 'body', 0.8, 0.06);        // top lip catch
      put(out,y,-1, pa,pa+0.02, q0,q1, 'body', -0.8, 0.06);       // left reveal
      if(knob){ const kx=(pa+pb)/2, kz = rows>1 ? q1-0.07 : (q0+q1)/2;
        put(out,y,-1, kx-0.03,kx+0.03, kz-0.03,kz+0.03, 'brass', 0.9, 0.08); } }
  }
  function plankTex(pw){ pw=pw||0.26; return (u,v)=>{ const f=((u%pw)+pw)%pw; if(f<0.03) return -2;
    const p=Math.floor(u/pw); return hash2(p|0,0)<0.5?0:(hash2(p|0,3)<0.4?-1:0); }; }
  function quiltTex(){ const c=0.30; return (u,v)=>{ const a=Math.floor(u/c),b=Math.floor(v/c);
    const fu=((u%c)+c)%c, fv=((v%c)+c)%c; if(fu<0.028||fv<0.028) return -1; return ((a+b)&1)?1:-1; }; }
  function weaveTex(){ const c=0.09; return (u,v)=>{ const fu=((u%c)+c)%c, fv=((v%c)+c)%c;
    return (fu<c*0.5)^(fv<c*0.5)?0:-1; }; }

  // ================= CATALOGUE =================
  const CATS = ['seating','tables','bed','storage','kitchen','bath','decor'];

  function pChair(out,s){ const w=0.44,d=0.42,seat=0.46,bh=0.94,lt=0.045, uph=(s.variant===1);
    const xs=[-w/2+0.06,w/2-0.06], ys=[-d/2+0.06,d/2-0.06];
    for(const x of xs) for(const y of ys) leg(out,x,y,lt,0,seat, 'wood',0);
    box(out,-w/2,w/2,-d/2,d/2, seat-0.055,seat, 'wood',0.1);
    if(uph) box(out,-w/2+0.03,w/2-0.03,-d/2+0.03,d/2-0.03, seat,seat+0.06, 'fabric',0.2);
    for(const x of xs) leg(out,x, d/2-0.06, lt, seat,bh, 'wood',0);
    for(let k=0;k<3;k++){ const z=seat+0.16+k*0.19; box(out,-w/2+0.06,w/2-0.06, d/2-0.075,d/2-0.035, z,z+0.075,'wood',0.05); }
  }
  function pStool(out,s){ const r=0.20, seat=0.47, lt=0.04;
    for(let i=0;i<3;i++){ const a=i/3*Math.PI*2+0.4; leg(out, Math.cos(a)*r*0.7, Math.sin(a)*r*0.7, lt, 0,seat,'wood',0); }
    prism(out,0,0,r, seat-0.05,seat, 12,'wood',0.12);
  }
  function pBench(out,s){ const w=0.9+ (s.len||0)*1.2, d=0.36, seat=0.45, lt=0.05;
    for(const x of [-w/2+0.08,w/2-0.08]) for(const y of [-d/2+0.06,d/2-0.06]) leg(out,x,y,lt,0,seat,'wood',0);
    box(out,-w/2,w/2,-d/2,d/2, seat-0.05,seat, 'wood',0.1, plankTex(0.22));
  }
  function pArmchair(out,s){ const w=0.72,d=0.72,seat=0.42,bh=0.86,arm=0.58;
    box(out,-w/2,w/2,-d/2,d/2, 0,seat, 'fabric',0);                          // base
    box(out,-w/2+0.05,w/2-0.05,-d/2+0.05,d/2-0.05, seat,seat+0.1, 'fabric2',0.25);  // cushion
    box(out,-w/2,w/2, d/2-0.16,d/2, seat,bh, 'fabric',0.05);                 // back
    for(const x of [-w/2, w/2-0.14]) box(out,x,x+0.14, -d/2,d/2-0.14, seat-0.02,arm, 'fabric',0.1); // arms
    box(out,-w/2,w/2,-d/2,d/2, 0,0.08, 'wood',-0.3);                         // plinth shadow
  }
  function pTable(out,s){ const w=1.1+(s.len||0)*0.9, d=0.78, top=0.74, lt=0.055;
    for(const x of [-w/2+0.09,w/2-0.09]) for(const y of [-d/2+0.09,d/2-0.09]) leg(out,x,y,lt,0,top-0.06,'wood',0);
    box(out,-w/2+0.07,w/2-0.07, -d/2+0.07,d/2-0.07, top-0.14,top-0.06,'wood',-0.4,null,true); // apron
    box(out,-w/2,w/2,-d/2,d/2, top-0.06,top, 'top',0.12, plankTex(0.24));
  }
  function pRoundTable(out,s){ const r=0.62, top=0.73;
    prism(out,0,0,0.08, 0,top-0.05, 10,'wood',0);                            // pedestal
    prism(out,0,0,0.34, 0,0.06, 12,'wood',-0.2);                             // foot
    prism(out,0,0,r, top-0.05,top, 20,'top',0.12);
  }
  function pBed(out,s){ const w=1.5, d=2.05, mat=0.52, foot=0.36, head=0.95;
    for(const x of [-w/2+0.07,w/2-0.07]) for(const y of [-d/2+0.07,d/2-0.07]) leg(out,x,y,0.06,0,mat-0.06,'wood',0);
    box(out,-w/2,w/2,-d/2,d/2, mat-0.14,mat-0.04, 'wood',-0.2);              // rail
    box(out,-w/2+0.03,w/2-0.03,-d/2+0.03,d/2-0.03, mat-0.04,mat+0.12, 'fabric',0.15, quiltTex()); // quilt
    box(out,-w/2+0.05,w/2-0.05, -d/2+0.1,-d/2+0.54, mat+0.06,mat+0.2, 'fabric2',0.3);   // pillow (head end, -Y front)
    box(out,-w/2,w/2, -d/2,-d/2+0.08, 0,head, 'wood',0.05);                  // headboard (-Y front, tall)
    box(out,-w/2,w/2, d/2-0.08,d/2, 0,foot, 'wood',0.05);                    // footboard (+Y back, short)
  }
  function pDresser(out,s){ const w=1.0,d=0.5,ht=0.9;
    box(out,-w/2,w/2,-d/2,d/2, 0,ht, 'body',0);
    box(out,-w/2-0.02,w/2+0.02,-d/2-0.01,d/2+0.01, ht,ht+0.05,'body',0.35);  // top overhang
    panelsY(out,-d/2, -w/2+0.05,w/2-0.05, 0.06,ht-0.05, 3,2, true);
    box(out,-w/2,w/2,-d/2,d/2, 0,0.07,'wood',-0.4,null,true);                // plinth
  }
  function pWardrobe(out,s){ const w=1.05,d=0.58,ht=1.95;
    box(out,-w/2,w/2,-d/2,d/2, 0,ht, 'body',0);
    box(out,-w/2-0.03,w/2+0.03,-d/2-0.02,d/2+0.02, ht,ht+0.08,'body',0.4);   // cornice
    panelsY(out,-d/2, -w/2+0.06,w/2-0.06, 0.12,ht-0.08, 1,2, true);
    box(out,-w/2,w/2,-d/2,d/2, 0,0.1,'wood',-0.4,null,true);
  }
  function pShelf(out,s){ const w=0.9+(s.len||0)*0.8,d=0.3,ht=1.7, tiers=4;
    for(const x of [-w/2+0.04,w/2-0.04]) box(out,x-0.04,x+0.04,-d/2,d/2, 0,ht,'wood',0);
    for(let i=0;i<=tiers;i++){ const z=ht*(i/tiers); box(out,-w/2,w/2,-d/2,d/2, z-0.03,z,'wood',0.1, plankTex(0.3)); }
    box(out,-w/2,w/2, d/2-0.04,d/2, 0,ht,'wood',-0.5);                       // back
  }
  function pSeaChest(out,s){ const w=0.9,d=0.5,ht=0.5;
    box(out,-w/2,w/2,-d/2,d/2, 0,ht*0.62, 'wood', 0, plankTex(0.2));
    // domed lid (two angled slabs)
    out.push(F([[-w/2,-d/2,ht*0.62],[w/2,-d/2,ht*0.62],[w/2,0,ht],[-w/2,0,ht]], 'wood', 0.15, 0.05, null, null));
    out.push(F([[w/2,d/2,ht*0.62],[-w/2,d/2,ht*0.62],[-w/2,0,ht],[w/2,0,ht]], 'wood', 0.05, 0.05, null, null));
    for(const y of [-d/2-0.001,d/2+0.001]) wall(out, -w/2,y*0, 0,0, 0,0,'iron',null,0); // noop guard
    for(const x of [-w*0.3,w*0.3]) decalY(out,-d/2,-1, x-0.03,x+0.03, 0,ht*0.62,'iron',0.2,null,true,0.06); // straps
    decalY(out,-d/2,-1, -0.06,0.06, ht*0.3,ht*0.42,'brass',0.9,null,true,0.07); // latch
  }
  function pBarrel(out,s){ const r=0.32, ht=0.92;
    prism(out,0,0,r*0.86, 0,ht, 14,'wood',0);
    prism(out,0,0,r, ht*0.28,ht*0.42, 14,'iron',0.1);                        // hoops
    prism(out,0,0,r, ht*0.62,ht*0.76, 14,'iron',0.1);
    prism(out,0,0,r*0.9, ht,ht+0.01, 14,'wood',0.3);                         // lid
  }
  function pCrate(out,s){ box(out,-0.3,0.3,-0.3,0.3, 0,0.58,'wood',0, plankTex(0.16));
    for(const z of [0.03,0.55]) decalY(out,-0.3,-1,-0.3,0.3,z,z+0.03,'wood',0.4,null,true,0.06);
  }
  function pRug(out,s){ const w=1.5+(s.len||0)*0.8, d=1.05;
    slab(out, [[-w/2,-d/2],[w/2,-d/2],[w/2,d/2],[-w/2,d/2]], 0.012, 'fabric', 0.1, weaveTex());
    slab(out, [[-w/2+0.1,-d/2+0.1],[w/2-0.1,-d/2+0.1],[w/2-0.1,d/2-0.1],[-w/2+0.1,d/2-0.1]], 0.02, 'fabric2', 0.25, weaveTex());
  }
  // ---- kitchen ----
  function pCounter(out,s){ const w=1.3+(s.len||0)*1.5, d=0.62, ht=0.9;
    box(out,-w/2,w/2,-d/2,d/2, 0,ht-0.05, 'body',0);
    panelsY(out,-d/2, -w/2+0.05,w/2-0.05, 0.06,ht-0.1, 1, Math.max(2,Math.round(w/0.6)), true);
    box(out,-w/2-0.03,w/2+0.03,-d/2-0.03,d/2+0.03, ht-0.05,ht, 'worktop',0.25, null); // worktop
    box(out,-w/2,w/2,-d/2,d/2, 0,0.07,'wood',-0.4,null,true);                // toe kick
  }
  function pStove(out,s){ const w=0.78,d=0.66,ht=0.9, dbl=(s.variant===1);
    box(out,-w/2,w/2,-d/2,d/2, 0.12,ht, 'iron',0);                           // body
    box(out,-w/2,w/2, -d/2,d/2, 0,0.16, 'iron',-0.3);                        // base
    box(out,-w/2-0.03,w/2+0.03,-d/2-0.03,d/2+0.03, ht,ht+0.06,'iron',0.4);   // cooktop lip
    // oven door + handle on -Y
    decalY(out,-d/2,-1, -w/2+0.08,w/2-0.08, 0.24,ht-0.14, 'iron',-1.4,null,true,0.06);
    decalY(out,-d/2,-1, -w/2+0.14,w/2-0.14, ht-0.28,ht-0.2, 'brass',0.8,null,true,0.08); // handle
    decalY(out,-d/2,-1, -0.02,0.1, 0.45,0.62, 'fire', 0.0,null,true,0.09);   // firebox glow
    // stovepipe up the +Y
    box(out, -0.07,0.07, d/2-0.16,d/2-0.02, ht+0.06, ht+0.9, 'iron',0.1);
    if(dbl) box(out, w/2-0.02,w/2+0.34, -d/2+0.05,d/2-0.05, ht-0.4,ht, 'iron',0.05); // warming shelf
    // cooktop rings
    for(const rx of dbl?[-0.2,0.05,0.28]:[-0.16,0.16]) prism(out, rx,0.02, 0.11, ht+0.06,ht+0.08, 10,'iron',-0.6);
  }
  function pSink(out,s){ const w=1.0,d=0.6,ht=0.9;
    box(out,-w/2,w/2,-d/2,d/2, 0,ht-0.06, 'body',0);                         // dry-sink cabinet
    panelsY(out,-d/2, -w/2+0.05,-0.02, 0.06,ht-0.14, 1,1, true);
    box(out,-w/2-0.02,w/2+0.02,-d/2-0.02,d/2+0.02, ht-0.06,ht, 'worktop',0.2); // counter
    // basin recess (+ side toward back)
    decalY(out,-d/2+0.001,1, 0.06,w/2-0.06, ht-0.06,ht+0.001,'ceramic',-1.2,null,true,0.02);
    slab(out, [[0.06,-0.06],[w/2-0.06,-0.06],[w/2-0.06,d/2-0.12],[0.06,d/2-0.12]], ht-0.12, 'ceramic',-1.0);
    // gooseneck pump on +Y edge
    box(out, w/2-0.28,w/2-0.2, d/2-0.14,d/2-0.06, ht,ht+0.28,'brass',0.2);
    box(out, w/2-0.28,w/2-0.02, d/2-0.13,d/2-0.07, ht+0.22,ht+0.28,'brass',0.3);
  }
  function pIcebox(out,s){ const w=0.72,d=0.6,ht=1.15;
    box(out,-w/2,w/2,-d/2,d/2, 0.09,ht, 'body',0);
    box(out,-w/2-0.02,w/2+0.02,-d/2-0.02,d/2+0.02, ht,ht+0.05,'body',0.35);
    for(const y of [-d/2+0.06,d/2-0.06]) for(const x of [-w/2+0.06,w/2-0.06]) leg(out,x,y,0.045,0,0.09,'wood',-0.2);
    // upper small door + lower door, chunky brass latches
    panelsY(out,-d/2, -w/2+0.06,w/2-0.06, ht*0.62,ht-0.05, 1,1, false);
    panelsY(out,-d/2, -w/2+0.06,w/2-0.06, 0.14,ht*0.6, 1,1, false);
    for(const z of [ht*0.75, ht*0.4]) decalY(out, w/2-0.001,1, d/2-0.16,d/2-0.05, z,z+0.12,'brass',0.9,null,true,0.06);
    for(const z of [ht*0.75, ht*0.4]) decalY(out,-d/2,-1, w/2-0.14,w/2-0.06, z,z+0.1,'brass',0.9,null,true,0.07);
  }
  function pHutch(out,s){ const w=1.05,d=0.5,ht=1.95, cnt=0.92;
    box(out,-w/2,w/2,-d/2,d/2, 0,cnt, 'body',0);                             // base cabinet
    panelsY(out,-d/2, -w/2+0.05,w/2-0.05, 0.08,cnt-0.08, 1,2, true);
    box(out,-w/2-0.02,w/2+0.02,-d/2,d/2, cnt,cnt+0.05,'worktop',0.2);          // counter
    const uw=w-0.14, uy=d*0.5;                                               // upper flush to the back wall
    box(out,-uw/2,uw/2, uy-0.32,uy, cnt+0.05,ht, 'body',0);                  // upper (shallower)
    box(out,-uw/2-0.03,uw/2+0.03, uy-0.34,uy+0.02, ht,ht+0.08,'body',0.4);   // cornice
    // glazed doors (front -Y of the upper is at y= uy-0.32)
    const uy0=uy-0.32; decalY(out,uy0,-1, -uw/2+0.05,uw/2-0.05, cnt+0.12,ht-0.06,'glass',0.0,null,true,0.06);
    for(const x of [-uw/4, uw/4]) decalY(out,uy0,-1, x-0.02,x+0.02, cnt+0.12,ht-0.06,'wood',0.4,null,true,0.08); // muntins
    for(let i=1;i<3;i++){ const z=cnt+0.12+(ht-0.18-cnt)*(i/3); decalY(out,uy0,-1,-uw/2+0.05,uw/2-0.05,z-0.015,z+0.015,'wood',0.3,null,true,0.08); } // shelves through glass
  }

  // ---- extra appliances ----
  function pWetSink(out,s){ const w=1.12,d=0.62,ht=0.9;
    box(out,-w/2,w/2,-d/2,d/2, 0,ht-0.06, 'body',0);
    panelsY(out,-d/2, -w/2+0.05,w/2-0.05, 0.06,ht-0.14, 1,2, true);
    box(out,-w/2-0.02,w/2+0.02,-d/2-0.02,d/2+0.02, ht-0.06,ht, 'worktop',0.2);
    slab(out, [[-0.36,-0.14],[0.36,-0.14],[0.36,0.18],[-0.36,0.18]], ht-0.12, 'ceramic',-1.0);   // porcelain basin well
    decalY(out,-0.14+0.001,1, -0.36,0.36, ht-0.12,ht+0.001,'ceramic',-1.2,null,true,0.02);
    box(out, -0.05,0.05, d/2-0.16,d/2-0.06, ht,ht+0.32,'tin',0.2);                               // running-water riser
    box(out, -0.04,0.17, d/2-0.15,d/2-0.07, ht+0.26,ht+0.32,'tin',0.3);                          // gooseneck
    box(out, 0.13,0.19, d/2-0.14,d/2-0.08, ht+0.2,ht+0.28,'tin',0.1);                            // spout
    for(const tx of [-0.19,0.19]) box(out, tx-0.03,tx+0.03, d/2-0.13,d/2-0.07, ht,ht+0.09,'brass',0.3); // hot/cold taps
    box(out,-w/2,w/2,-d/2,d/2, 0,0.07,'wood',-0.4,null,true);
  }
  function pRange(out,s){ const w=1.24,d=0.72,ht=0.9;
    box(out,-w/2,w/2,-d/2,d/2, 0.14,ht, 'iron',0);
    box(out,-w/2,w/2,-d/2,d/2, 0,0.18, 'iron',-0.3);
    box(out,-w/2-0.03,w/2+0.03,-d/2-0.03,d/2+0.03, ht,ht+0.06,'iron',0.4);
    for(const [a,b] of [[-w/2+0.07,-0.03],[0.03,w/2-0.07]]){ decalY(out,-d/2,-1, a,b, 0.26,ht-0.14,'iron',-1.4,null,true,0.06);
      const hx=(a+b)/2; decalY(out,-d/2,-1, hx-0.11,hx+0.11, ht-0.3,ht-0.22,'brass',0.8,null,true,0.08); }   // twin ovens + handles
    decalY(out,-d/2,-1, -0.5,-0.4, 0.42,0.64,'fire',0.0,null,true,0.09);                         // firebox glow
    box(out,-w/2,w/2, d/2-0.12,d/2, ht+0.06,ht+0.52,'iron',0.1);                                 // backsplash
    box(out,-w/2-0.03,w/2+0.03, d/2-0.17,d/2, ht+0.46,ht+0.52,'iron',0.3);                       // warming shelf
    box(out, w/2-0.24,w/2-0.1, d/2-0.14,d/2-0.02, ht+0.52,ht+1.02,'iron',0.1);                   // stovepipe
    for(const rx of [-0.36,-0.12,0.12,0.36]) prism(out, rx,-0.06, 0.1, ht+0.06,ht+0.08, 10,'iron',-0.6);  // 4 burners
    box(out,-w/2+0.04,w/2-0.04, -d/2-0.06,-d/2-0.02, ht-0.2,ht-0.14,'brass',0.4);                // towel rail
  }
  function pCupboard(out,s){ const w=1.0,d=0.55,ht=2.0;
    box(out,-w/2,w/2,-d/2,d/2, 0,ht, 'body',0);
    box(out,-w/2-0.03,w/2+0.03,-d/2-0.02,d/2+0.02, ht,ht+0.08,'body',0.4);
    panelsY(out,-d/2, -w/2+0.06,w/2-0.06, 1.02,ht-0.1, 1,2, true);
    panelsY(out,-d/2, -w/2+0.06,w/2-0.06, 0.12,0.96, 1,2, true);
    box(out,-w/2-0.02,w/2+0.02, d/2-0.5,d/2-0.46, 0.98,1.02,'wood',0.2);                          // mid rail hint
    box(out,-w/2,w/2,-d/2,d/2, 0,0.1,'wood',-0.4,null,true);
  }
  function pWashtub(out,s){ const r=0.34,ht=0.46,stand=0.34;
    for(let i=0;i<4;i++){ const a=i/4*Math.PI*2+0.4; leg(out,Math.cos(a)*r*0.66,Math.sin(a)*r*0.66,0.035,0,stand,'wood',0); }
    box(out,-r*0.55,r*0.55,-r*0.55,r*0.55, stand-0.05,stand,'wood',-0.2);                        // stand shelf
    prism(out,0,0,r, stand,stand+ht, 16,'tin',0);
    prism(out,0,0,r*1.03, stand+ht-0.06,stand+ht, 16,'tin',0.3);                                 // rolled rim
    prism(out,0,0,r*0.9, stand+ht,stand+ht+0.004, 16,'glass',-0.5);                              // water
    prism(out,0,0,r, stand+ht*0.3,stand+ht*0.42, 16,'tin',0.15);                                 // banding
  }
  // ---- bathroom ----
  function pTub(out,s){ const w=0.8,d=1.6, z0=0.16,rim=0.5;
    for(const x of [-w/2+0.08,w/2-0.08]) for(const y of [-d/2+0.12,d/2-0.12]) leg(out,x,y,0.055,0,z0,'brass',0);  // claw feet
    box(out,-w/2,w/2,-d/2,d/2, z0,z0+rim, 'ceramic',0);
    box(out,-w/2-0.02,w/2+0.02,-d/2-0.02,d/2+0.02, z0+rim-0.06,z0+rim,'ceramic',0.3);            // rolled rim
    slab(out,[[-w/2+0.08,-d/2+0.1],[w/2-0.08,-d/2+0.1],[w/2-0.08,d/2-0.1],[-w/2+0.08,d/2-0.1]], z0+rim-0.09,'glass',-0.6); // water
    box(out,-0.05,0.05, d/2-0.14,d/2-0.04, z0+rim,z0+rim+0.2,'brass',0.2);                        // faucet (back +Y)
    box(out,-0.09,0.09, d/2-0.13,d/2-0.05, z0+rim+0.15,z0+rim+0.2,'brass',0.3);
  }
  function pToilet(out,s){ const d=0.72;
    prism(out,0,-0.1,0.13, 0,0.12, 12,'ceramic',0.1);                                            // flared foot
    prism(out,0,-0.1,0.17, 0.1,0.34, 12,'ceramic',0);                                            // pedestal column
    prism(out,0,-0.1,0.21, 0.32,0.42, 16,'ceramic',0.1);                                         // bowl body
    prism(out,0,-0.1,0.16, 0.42,0.43, 16,'iron',-1.3);                                           // dark water
    slab(out,[[-0.19,-0.28],[0.19,-0.28],[0.19,0.03],[-0.19,0.03]], 0.42,'wood',0.15);           // wooden seat
    out.push(F([[-0.18,0.02,0.44],[0.18,0.02,0.44],[0.18,0.12,0.74],[-0.18,0.12,0.74]],'wood',0.25,0.05,null,null)); // raised lid
    box(out,-0.22,0.22, d/2-0.14,d/2, 1.28,1.66,'wood',0.1, plankTex(0.18));                     // high cistern
    box(out,-0.24,0.24, d/2-0.16,d/2+0.01, 1.62,1.7,'wood',0.3);                                 // cistern lid
    box(out,-0.03,0.03, d/2-0.1,d/2-0.04, 0.42,1.28,'brass',0.1);                                // downpipe
    box(out, 0.15,0.18, d/2-0.15,d/2-0.11, 0.82,1.28,'brass',-0.2);                              // pull chain
    box(out, 0.14,0.19, d/2-0.16,d/2-0.1, 0.74,0.84,'brass',0.3);                                // chain pull
  }
  function pWashstand(out,s){ const w=0.82,d=0.48,ht=0.82;
    for(const x of [-w/2+0.06,w/2-0.06]) for(const y of [-d/2+0.06,d/2-0.06]) leg(out,x,y,0.045,0,ht-0.08,'wood',0);
    box(out,-w/2,w/2,-d/2,d/2, ht-0.1,ht-0.02, 'stone',0.1);                                     // marble top
    box(out,-w/2+0.05,w/2-0.05,-d/2+0.05,d/2-0.05, 0.2,0.25,'wood',-0.2, plankTex(0.2));         // lower shelf
    prism(out,-0.14,0.03,0.17, ht-0.02,ht+0.11, 16,'ceramic',0.1);                               // basin
    prism(out,-0.14,0.03,0.11, ht+0.09,ht+0.11, 16,'ceramic',-0.5);                              // basin hollow
    prism(out,0.24,0.02,0.09, ht-0.02,ht+0.24, 12,'ceramic',0.15);                               // ewer/pitcher
    box(out,0.24,0.36, 0.0,0.05, ht+0.16,ht+0.22,'ceramic',0.2);                                 // spout
    box(out, w/2,w/2+0.04, -d/2+0.06,d/2-0.06, ht-0.42,ht-0.37,'brass',0.3);                     // towel bar
  }
  function pTowelRail(out,s){ const w=0.6,ht=0.92;
    for(const x of [-w/2,w/2]){ box(out,x-0.05,x+0.05,-0.17,0.17, 0,0.05,'wood',-0.2);            // feet
      box(out,x-0.028,x+0.028,-0.02,0.02, 0,ht,'wood',0); }                                       // uprights
    for(const z of [ht*0.44,ht*0.68,ht-0.02]) box(out,-w/2,w/2, -0.022,0.022, z-0.02,z+0.02,'wood',0.2); // bars
    box(out,-0.22,0.12, -0.03,0.03, ht*0.68-0.36,ht*0.68+0.03,'fabric',0.1, plankTex(0.16));      // hung towel
  }
  function pMirror(out,s){ const w=0.56,ht=1.5;
    for(const x of [-w/2,w/2]){ box(out,x-0.045,x+0.045,-0.22,0.22, 0,0.05,'wood',-0.2);          // base feet
      box(out,x-0.03,x+0.03,-0.015,0.015, 0,ht,'wood',0); }                                       // posts
    box(out,-w/2+0.05,w/2-0.05, -0.03,0.03, 0.22,ht-0.04,'wood',0.1);                             // frame
    decalY(out,-0.03,-1, -w/2+0.09,w/2-0.09, 0.28,ht-0.12,'glass',0.2,null,true,0.06);            // glass
    decalY(out,-0.03,-1, -w/2+0.13,-w/2+0.25, 0.55,ht-0.3,'trim',0.5,null,true,0.08);             // sheen
    box(out,-w/2+0.02,w/2-0.02, -0.045,0.045, ht-0.09,ht,'wood',0.3);                             // crest
  }

  const PROPS = {
    chair:      { label:'Chair',        cat:'seating', foot:[0.44,0.42], variants:['ladderback','upholstered'], def:{wood:'oak'},   build:pChair },
    stool:      { label:'Stool',        cat:'seating', foot:[0.4,0.4],   variants:['round'],                    def:{wood:'pine'},  build:pStool },
    bench:      { label:'Bench',        cat:'seating', foot:[1.6,0.36],  variants:['plain'], run:true,          def:{wood:'pine'},  build:pBench },
    armchair:   { label:'Armchair',     cat:'seating', foot:[0.72,0.72], variants:['wing'],                     def:{paint:'red',fabric:'red',fabric2:'cream',wood:'walnut'}, build:pArmchair },
    table:      { label:'Table',        cat:'tables',  foot:[2.0,0.78],  variants:['rect'], run:true,           def:{wood:'oak'},   build:pTable },
    roundTable: { label:'Round Table',  cat:'tables',  foot:[1.24,1.24], variants:['pedestal'],                 def:{wood:'walnut'},build:pRoundTable },
    bed:        { label:'Bed',          cat:'bed',     foot:[1.5,2.05],  variants:['quilted'],                  def:{wood:'walnut',fabric:'sage',fabric2:'cream'}, build:pBed },
    dresser:    { label:'Dresser',      cat:'storage', foot:[1.0,0.5],   variants:['6-drawer'],                 def:{paint:'blue',wood:'pine'},  build:pDresser },
    wardrobe:   { label:'Wardrobe',     cat:'storage', foot:[1.05,0.58], variants:['2-door'],                   def:{paint:'sage',wood:'pine'},  build:pWardrobe },
    shelf:      { label:'Shelf',        cat:'storage', foot:[1.7,0.3],   variants:['open'], run:true,           def:{wood:'pine'},  build:pShelf },
    seaChest:   { label:'Sea Chest',    cat:'storage', foot:[0.9,0.5],   variants:['domed'],                    def:{wood:'walnut'},build:pSeaChest },
    barrel:     { label:'Barrel',       cat:'storage', foot:[0.64,0.64], variants:['hooped'],                   def:{wood:'oak'},   build:pBarrel },
    crate:      { label:'Crate',        cat:'storage', foot:[0.6,0.6],   variants:['slatted'],                  def:{wood:'pine'},  build:pCrate },
    rug:        { label:'Rug',          cat:'decor',   foot:[2.3,1.05],  variants:['bordered'], run:true,       def:{fabric:'red',fabric2:'gold'}, build:pRug },
    counter:    { label:'Counter run',  cat:'kitchen', foot:[2.8,0.62],  variants:['cabinets'], run:true,       def:{paint:'cream',wood:'pine',worktop:'slate'}, build:pCounter },
    stove:      { label:'Cook stove',   cat:'kitchen', foot:[1.12,0.66], variants:['single','double'],          def:{wood:'oak'},   build:pStove },
    sink:       { label:'Dry sink',     cat:'kitchen', foot:[1.0,0.6],   variants:['pump'],                     def:{paint:'blue',wood:'pine',worktop:'slate'},  build:pSink },
    icebox:     { label:'Icebox',       cat:'kitchen', foot:[0.72,0.6],  variants:['oak'],                      def:{paint:'white',wood:'oak'},  build:pIcebox },
    hutch:      { label:'Dish hutch',   cat:'kitchen', foot:[1.05,0.5],  variants:['glazed'],                   def:{paint:'sage',wood:'pine',worktop:'butcher'},  build:pHutch },
    wetSink:    { label:'Wet sink',     cat:'kitchen', foot:[1.12,0.62], variants:['piped'],                    def:{paint:'white',wood:'pine',worktop:'marble'}, build:pWetSink },
    range:      { label:'Range oven',   cat:'kitchen', foot:[1.24,0.72], variants:['twin-oven'],                def:{wood:'oak'},   build:pRange },
    cupboard:   { label:'Larder',       cat:'kitchen', foot:[1.0,0.55],  variants:['4-door'],                   def:{paint:'cream',wood:'pine'}, build:pCupboard },
    washtub:    { label:'Wash tub',     cat:'kitchen', foot:[0.68,0.68], variants:['tin'],                      def:{wood:'oak'},   build:pWashtub },
    tub:        { label:'Bath tub',     cat:'bath',    foot:[0.8,1.6],   variants:['clawfoot'],                 def:{},             build:pTub },
    toilet:     { label:'Water closet', cat:'bath',    foot:[0.44,0.7],  variants:['high-cistern'],             def:{},             build:pToilet },
    washstand:  { label:'Washstand',    cat:'bath',    foot:[0.82,0.48], variants:['ewer'],                     def:{wood:'walnut'},build:pWashstand },
    towelRail:  { label:'Towel rail',   cat:'bath',    foot:[0.6,0.4],   variants:['draped'],                   def:{wood:'pine',fabric:'white'}, build:pTowelRail },
    mirror:     { label:'Cheval mirror',cat:'bath',    foot:[0.56,0.45], variants:['standing'],                 def:{wood:'walnut'},build:pMirror },
  };

  function resolve(name, opts){ opts=opts||{}; const P=PROPS[name]||PROPS.chair;
    const g=(k,d)=> opts[k]!=null?opts[k] : (P.def[k]!=null?P.def[k]:d);
    return { name, wood:g('wood','pine'), paint: opts.paint!==undefined?opts.paint:(P.def.paint||null),
      fabric:g('fabric','red'), fabric2:g('fabric2','cream'),
      variant: opts.variant!=null?opts.variant:0, len: opts.len!=null?opts.len:(P.run?0.4:0),
      worktop: opts.worktop!=null?opts.worktop:(P.def.worktop||'slate'),
      weather: opts.weather!=null?opts.weather:0.2, night:!!opts.night }; }

  function makeMats(s){ const wx=s.weather, night=s.night;
    const grime=r=>r.map(c=>mix(desat(c,wx*0.25),'#403627',wx*0.12));
    const warm=r=>night?r.map(c=>mix(mix(c,'#c98b3f',0.12),'#241a10',0.2)):r;
    const t=r=>warm(grime(r));
    const wood=WOODS[s.wood]||WOODS.pine, paint=s.paint?(BODY[s.paint]||BODY.sage):null;
    const bodyRamp = paint||wood, worktop=WORKTOPS[s.worktop]||WORKTOPS.slate;
    return { wood:{ramp:t(wood)}, body:{ramp:t(bodyRamp)}, top:{ramp:t(wood)}, worktop:{ramp:t(worktop)},
      iron:{ramp:t(IRON)}, tin:{ramp:t(TIN)}, brass:{ramp:t(BRASS)}, stone:{ramp:t(STONE)},
      ceramic:{ramp:t(CERAMIC)}, fabric:{ramp:t(BODY[s.fabric]||BODY.red)}, fabric2:{ramp:t(BODY[s.fabric2]||BODY.cream)},
      glass:{ramp: night?GLASSN:GLASSD}, fire:{ramp:FIRE}, trim:{ramp:t(TRIM)} }; }

  // ---- rasteriser (identical recipe; single object → always keyline) ----
  function paint(faces, B, MATS){ const N=W*H;
    const zbuf=new Float32Array(N).fill(Infinity), dep=new Float32Array(N);
    const rbuf=new Array(N).fill(null), ibuf=new Int16Array(N), nbuf=new Array(N).fill(null);
    for(const f of faces){ const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
      let n=normal(rv[0],rv[1],rv[2]); let sh=shadeOf(n, B.se, B.ce);
      if(sh<0 && (f.b<=-1)) sh=shadeOf([-n[0],-n[1],-n[2]], B.se, B.ce)*0.9;
      const fidx=sh*GAIN+BIAS+f.b; const M=MATS[f.mat]||MATS.body, ramp=M.ramp, tex=f.tex, uv=f.uv, flat=f.flat;
      for(let t=1;t+1<rv.length;t++) fillTri(rv[0],rv[t],rv[t+1],0,t,t+1);
      function fillTri(a,b,c,ia,ib,ic){
        const minX=Math.max(0,Math.floor(Math.min(a.sx,b.sx,c.sx))), maxX=Math.min(W-1,Math.ceil(Math.max(a.sx,b.sx,c.sx)));
        const minY=Math.max(0,Math.floor(Math.min(a.sy,b.sy,c.sy))), maxY=Math.min(H-1,Math.ceil(Math.max(a.sy,b.sy,c.sy)));
        const area=(b.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b.sy-a.sy); if(Math.abs(area)<1e-6) return;
        const ua=uv?uv[ia]:null, ub=uv?uv[ib]:null, uc=uv?uv[ic]:null;
        for(let y=minY;y<=maxY;y++) for(let x=minX;x<=maxX;x++){ const px=x+0.5, py=y+0.5;
          const w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area, w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area, w2=1-w0-w1;
          if(w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const d=w0*a.d+w1*b.d+w2*c.d, deff=d-f.db, i=y*W+x;
          if(deff<zbuf[i]){ zbuf[i]=deff; dep[i]=d; nbuf[i]=f.mat; let fi=fidx;
            if(tex&&uv){ const uu=w0*ua[0]+w1*ub[0]+w2*uc[0], vv=w0*ua[1]+w1*ub[1]+w2*uc[1]; fi+=tex(uu,vv); }
            let idx; if(flat){ idx=Math.round(fi); } else { const base=Math.floor(fi); idx=base+((fi-base)>BAYER[x&3][y&3]?1:0); }
            idx=Math.max(0,Math.min(ramp.length-1,idx)); rbuf[i]=ramp; ibuf[i]=idx; } }
      }
    }
    return { rbuf, ibuf, nbuf, dep };
  }
  function post(bufs, s){ const { rbuf, ibuf, nbuf, dep }=bufs, N=W*H, out=new Array(N).fill(null);
    for(let i=0;i<N;i++){ if(rbuf[i]) out[i]=rbuf[i][ibuf[i]]; }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!rbuf[i]) continue;
      for(const [dx,dy] of [[1,0],[0,1]]){ const nx=x+dx,ny=y+dy; if(nx>=W||ny>=H) continue; const j=ny*W+nx; if(!rbuf[j]) continue;
        if(Math.abs(dep[i]-dep[j])>EDGE){ const far=dep[i]>dep[j]?i:j; out[far]=rbuf[far][Math.max(0,ibuf[far]-2)]; } } }
    if(s.weather>0.02){ const rnd=mulberry32(701|((s.variant*53)|0));
      for(let i=0;i<N;i++){ const m=nbuf[i]; if(!m||!rbuf[i]) continue;
        if((m==='wood'||m==='body'||m==='top'||m==='fabric'||m==='stone') && rnd()<s.weather*0.05)
          out[i]=rbuf[i][Math.max(0,ibuf[i]-1)]; } }
    if(s.night){ for(let y=1;y<H-1;y++) for(let x=1;x<W-1;x++){ const i=y*W+x; if(nbuf[i]!=='fire') continue;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1],[1,-1],[-1,-1]]){ const j=(y+dy)*W+(x+dx); if(out[j]&&nbuf[j]!=='fire') out[j]=mix(out[j],'#f0b45a',0.24); } } }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!out[i]) continue; let n=0;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy; if(nx>=0&&nx<W&&ny>=0&&ny<H&&out[ny*W+nx]) n++; }
      if(n===0){ out[i]=null; rbuf[i]=null; } }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(out[i]) continue; let touch=false;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy; if(nx>=0&&nx<W&&ny>=0&&ny<H&&rbuf[ny*W+nx]){ touch=true; break; } }
      if(touch) out[i]=KEY; }
    return out;
  }
  function toRGBA(cols){ const rgba=new Uint8ClampedArray(W*H*4);
    for(let i=0;i<W*H;i++){ const c=cols[i]; if(!c){ rgba[i*4+3]=0; continue; } const [r,g,bl]=hex2rgb(c); rgba[i*4]=r;rgba[i*4+1]=g;rgba[i*4+2]=bl;rgba[i*4+3]=255; }
    return rgba;
  }

  function render(name, dir, opts){ opts=(typeof opts==='number')?{elev:opts}:(opts||{});
    const s=resolve(name,opts), B=camBasis({dir,elev:opts.elev}), MATS=makeMats(s), out=[];
    (PROPS[name]||PROPS.chair).build(out, s);
    return toRGBA(post(paint(out,B,MATS), s));
  }
  function footprint(name, opts){ const s=resolve(name,opts), P=PROPS[name]||PROPS.chair;
    const w=P.foot[0]+(P.run?(s.len*(name==='table'?0.9:name==='counter'?1.5:name==='rug'?0.8:1.2)):0);
    return { w, d:P.foot[1] }; }
  function project(dir, p, elev){ const v=projVert(p[0],p[1],p[2],camBasis({dir,elev})); return {x:v.sx,y:v.sy}; }
  function list(){ return Object.keys(PROPS); }

  root.PropIso = { W, H, PX, pivot:{x:cx,y:groundY}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'], WOODS, WORKTOPS, IRON, TIN, BRASS, CERAMIC, STONE, BODY, TRIM, KEY,
    PROPS, CATS, list, footprint, render, project };
})(typeof globalThis!=='undefined'?globalThis:window);
