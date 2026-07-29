/* Hidden Harbours — ISO SHORELINE tile kit v7 (PEI red-sandstone coast).
   Replaces the near-plan shorelineKitRig for terrain: matches the ADR-0006/0022 boat bake
   (¾ camera from the SOUTH at 40° elevation, upper-left key light, band-edge-only Bayer
   dither WORLD-LOCKED to global pixel coords, no AA, KTC ramps).

   WATER CONTRACT (ADR 0010 / 0012 / 0023 — read from the repo):
   • The engine's shader owns ALL water: clip() at the live depth-0 tide contour, foam/swash
     riding it, and the displaced 3D surface whose lift fades to zero at the waterline
     (ShoreFadeMath). Therefore NO tile here bakes water, foam, waterline, or shallows pixels.
   • Rule-tiles carry TERRAIN-TYPE edges only (grass↔sand↔rock) + permanent landforms
     (cliff, dune). The live wet edge is never a tile.
   • The tide sweeps whole flats, so every ground material reads correctly dry AND submerged
     (the shader tints/wets it by depth).

   Grid: square 32×32, 32 px = 1 m (PPU 32 tilemap). Vertical cliff/dune bands are 32 px
   tall tiles (~1.3 m of face at the 40° camera). Ground noise is a pure function of GLOBAL
   pixel coords → tiles are seamless and never repeat; bake any (gx,gy).

   API (globalThis.ShoreIso):
     GROUND  = ['grass','marram','sand','ripple','shingle','shelf']
     ground(mat, {gx,gy,seed})                      -> {data,w:32,h:32}   opaque
     FRINGE_PIECES = 12 ('edN'..'inNW')
     fringe(mat, piece, {seed,lip})                 -> overlay on transparency (stamp over base tile)
     CLIFF_PIECES = ['faceS','cornSW','cornSE','sideW','sideE','innSW','innSE','diagSW','diagSE']
     cliff(piece, {band:'cap'|'mid'|'toe', gy, seed, feature:'cave'|null}) -> {data,w,h}
     dune(piece, {seed})                            -> single-band pieces (same names, no bands)
     stack(size 'reef'|'s'|'m'|'l', {seed})         -> pure-rock sprite (transparent)
     boulder(size 's'|'m'|'l', {seed})              -> slab boulder sprite
     column(piece, rows, {seed,feature})            -> stacked cliff column (cap+mid*+toe)
*/
(function(root){
  const TILE=32;
  const PAL={
    redrock:['#4a1e14','#6b2c1c','#87301f','#a04528','#bd6038','#d17c4c','#e0a06e'],
    redsoil:['#4a2114','#6c3019','#8a3f22','#a5502c','#bd6538'],
    sand   :['#9c5f30','#bd8149','#d4a061','#e6bf83','#f2d6a0','#f9ead0'],
    straw  :'#c9b06a',
    redsand:['#5e2718','#7c3520','#98462a','#b25e36','#c87a4c','#dc9868'],
    grn    :['#1c3216','#2f4a1d','#456b26','#628b32','#83a848','#a3c460'],
    KEY:'#171009', shadow:'#0c171a', seaweed:'#43502a',
    // hero-preview water only (never baked into tiles): repo DepthRamp endpoints
    depthShallow:'#8EA59C', depthDeep:'#0F2227', foam:'#eaf3ee'
  };
  const BAYER=[[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]];
  function hash(x,y,s){ let h=(x*374761393+y*668265263+((s|0))*1274126177)|0;
    h=Math.imul(h^(h>>>13),1274126177); return ((h^(h>>>16))>>>0)/4294967296; }
  function hex2rgb(h){ return [parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]; }
  function mixc(a,b,t){ const A=Array.isArray(a)?a:hex2rgb(a),B=Array.isArray(b)?b:hex2rgb(b);
    return [A[0]+(B[0]-A[0])*t,A[1]+(B[1]-A[1])*t,A[2]+(B[2]-A[2])*t]; }
  // world-locked ordered dither between two palette entries — the ONLY blending tool
  // (style law: solid bands, dithered edges, zero crawl).
  function dpick(wx,wy,a,b,t){ if(t<=0)return a; if(t>=1)return b;
    return ((BAYER[wy&3][wx&3]+0.5)/16 < t) ? b : a; }
  // shade/lighten a ramp colour by stepping the ramp with a dithered fraction
  function rampAt(ramp,f,wx,wy){ f=Math.max(0,Math.min(ramp.length-1,f));
    const i=Math.floor(f), fr=f-i;
    return dpick(wx,wy, ramp[i], ramp[Math.min(ramp.length-1,i+1)], fr); }

  function mkBuf(w,h){ const buf=new Uint8ClampedArray(w*h*4);
    const put=(x,y,c,a)=>{ if(x<0||x>=w||y<0||y>=h)return; const rgb=Array.isArray(c)?c:hex2rgb(c);
      const i=(y*w+x)*4; buf[i]=rgb[0];buf[i+1]=rgb[1];buf[i+2]=rgb[2];buf[i+3]=(a==null?255:a); };
    return {buf,put,w,h};
  }

  // ============================== GROUND MATERIALS (world-coord noise, seamless) ===============
  function groundColor(mat,wx,wy,seed){
    seed=seed|0;
    if(mat==='grass'){
      let f=2.6+hash(wx,wy,seed+1)*2.4;                    // ramp 2.6..5
      if(hash(wx>>2,wy>>2,seed+6)<0.22)f-=0.9;             // mowed-dark patchiness (4px cells)
      let c=rampAt(PAL.grn,f,wx,wy);
      if(hash(wx,wy,seed+4)<0.012)c=PAL.redsoil[2];        // bare red soil scuff
      if(hash(wx,wy,seed+5)<0.02)c=PAL.straw;              // dry blade
      return c;
    }
    if(mat==='marram'){
      let f=2.2+hash(wx,wy,seed+1)*2.2;
      let c=rampAt(PAL.grn,f,wx,wy);
      if(hash(wx>>1,wy,seed+2)<0.16)c=PAL.straw;           // heavy dry-straw blades
      if(hash(wx>>2,wy>>2,seed+3)<0.18)c=rampAt(PAL.sand,2.5+hash(wx,wy,seed+7),wx,wy); // sand showing through
      return c;
    }
    if(mat==='sand'){
      let f=2.4+hash(wx,wy,seed+1)*1.8;                    // pale dry PEI sand
      let c=rampAt(PAL.sand,f,wx,wy);
      if(hash(wx,wy,seed+2)<0.04)c=PAL.sand[5];            // near-white grains
      if(hash(wx,wy,seed+3)<0.018)c=rampAt(PAL.sand,1,wx,wy);
      if(hash(wx,wy,seed+9)<0.008)c=PAL.seaweed;           // fleck of wrack
      return c;
    }
    if(mat==='ripple'){
      // rippled red tidal flat — GEOLOGY not water: ripples continue seamlessly across tiles
      const wob=Math.sin(wx*0.42+Math.cos(wy*0.33+seed)*1.3)+Math.sin(wy*0.7+seed*2);
      const rip=wy+wob*2.6;
      const b=Math.floor(rip/4);
      let f=2.8+hash(0,b,seed+6)*1.6;
      let c=rampAt(PAL.redsand,f,wx,wy);
      const m=((Math.floor(rip)%4)+4)%4;
      if(m===0)c=dpick(wx,wy,c,rampAt(PAL.redsand,Math.min(5,f+1),wx,wy),0.55);  // crest catch-light
      if(m===2)c=dpick(wx,wy,c,rampAt(PAL.redsand,Math.max(1,f-0.9),wx,wy),0.5); // trough shade
      if(hash(wx,wy,seed+8)<0.006)c=PAL.seaweed;
      return c;
    }
    if(mat==='shingle'){
      // slab cobble shore: thin wide sandstone plates over sand
      const rowH=3+Math.round(hash(0,wy>>2,seed+47));
      const ry=Math.floor(wy/rowH), off=Math.round(hash(0,ry,seed+45)*11);
      const slabW=7+Math.round(hash(ry,0,seed+46)*6);
      const key=wx+off, sx=Math.floor(key/slabW);
      const rr=hash(sx,ry,seed+40);
      if(rr<0.16)return rampAt(PAL.sand,2+hash(wx,wy,seed+43),wx,wy);   // sand between slabs
      let f=2+rr*3;
      let c=rampAt(PAL.redrock,f,wx,wy);
      const ly=((wy%rowH)+rowH)%rowH;
      if(ly===0)c=rampAt(PAL.redrock,Math.min(6,f+2),wx,wy);            // sunlit plate lip
      else if(ly===rowH-1)c=PAL.redrock[1];                             // undercut gap
      if(((key%slabW)+slabW)%slabW===0)c=PAL.redrock[1];                // joint
      return c;
    }
    if(mat==='shelf'){
      // wave-cut red rock platform, flat jointed top
      let f=3+hash(wx>>2,wy>>2,seed)*2;
      let c=rampAt(PAL.redrock,f,wx,wy);
      const crs=Math.floor(wy/9), jx=((wx+crs*4)%8+8)%8;
      if(jx===0 && hash(Math.floor((wx+crs*4)/8),crs,seed+23)<0.6)c=PAL.redrock[Math.max(1,Math.floor(f)-2)];
      if(((wy%9)+9)%9===0 && hash(wx>>3,crs,seed+24)<0.75)c=PAL.redrock[Math.max(1,Math.floor(f)-2)];
      if(hash(wx,wy,seed+3)<0.05)c=PAL.redrock[6];
      if(hash(wx,wy,seed+7)<0.015)c=PAL.grn[3];                          // grass in a crack
      return c;
    }
    return PAL.sand[2];
  }
  function ground(mat,opts){ opts=opts||{}; const gx=(opts.gx|0)*TILE, gy=(opts.gy|0)*TILE, seed=opts.seed|0;
    const c=mkBuf(TILE,TILE);
    for(let y=0;y<TILE;y++)for(let x=0;x<TILE;x++)c.put(x,y,groundColor(mat,gx+x,gy+y,seed));
    return c;
  }

  // ============================== TERRAIN-TYPE FRINGES (overlay autotiles) =====================
  // Ragged tongue of material `mat` lapping from the named side onto the neighbour tile.
  // Stamp OVER the neighbour's ground tile. Grass gets a 2px soil under-shadow on S (camera-facing)
  // edges — the ~15 cm sod lip, the only "height" a flat transition carries.
  const FRINGE_PIECES=['edN','edE','edS','edW','coNE','coSE','coSW','coNW','inNE','inSE','inSW','inNW'];
  function fringeDepth(p,s,seed){ // wavy 4..8 px reach along an edge, param p 0..31
    return 4 + (Math.sin(p*0.5+s)+Math.sin(p*0.17+s*1.7))*1.6 + hash(p,0,s+seed)*2.2; }
  function fringe(mat,piece,opts){ opts=opts||{}; const seed=opts.seed|0, gx0=(opts.gx|0)*TILE, gy0=(opts.gy|0)*TILE;
    const c=mkBuf(TILE,TILE);
    const soil = mat==='grass'||mat==='marram';
    const N=piece.includes('N'), E=piece.includes('E'), S=piece.includes('S'), W=piece.includes('W');
    const inner=piece.startsWith('in'), edge=piece.startsWith('ed');
    function reach(side,p){ const sk=({n:11,e:23,s:37,w:51})[side]+seed*7; return fringeDepth(p,sk,seed); }
    function covered(x,y){
      const wx=gx0+x, wy=gy0+y;
      if(edge){ if(piece==='edN')return y<reach('n',wx); if(piece==='edS')return (TILE-1-y)<reach('s',wx);
        if(piece==='edW')return x<reach('w',wy); return (TILE-1-x)<reach('e',wy); }
      if(inner){ // concave: neighbour on the two named sides — fringe floods the corner quadrant
        const dy=N? y : (TILE-1-y), dx=E? (TILE-1-x) : (W? x : 99);
        return dy<reach(N?'n':'s',wx) || dx<reach(E?'e':'w',wy); }
      // outer corner: mat wraps two adjacent sides → rounded lobe in that corner
      const dy=N? y : (TILE-1-y), dx=E? (TILE-1-x) : x;
      const rn=reach(N?'n':'s',wx), re=reach(E?'e':'w',wy);
      return (dy*dy)/(rn*rn+1e-3) + (dx*dx)/(re*re+1e-3) < 1;
    }
    for(let y=0;y<TILE;y++)for(let x=0;x<TILE;x++){
      if(!covered(x,y))continue;
      // ragged pixel dropout at the tongue tip
      const tip = !covered(x,y+ (piece==='edN'||N&&!inner?1:-1)) || !covered(x+(W?-1:1),y);
      if(tip && hash(gx0+x,gy0+y,seed+81)<0.35)continue;
      c.put(x,y,groundColor(mat,gx0+x,gy0+y,seed));
    }
    // soil under-shadow on S-facing grass lips (the sod sits a lip proud)
    if(soil && (piece==='edN'||inner&&N)){ // mat to the N laps down: shadow line under its lowest pixels
      for(let x=0;x<TILE;x++){ let low=-1;
        for(let y=TILE-1;y>=0;y--){ const i=(y*TILE+x)*4; if(c.buf[i+3]){low=y;break;} }
        if(low>=0&&low<TILE-1){ c.put(x,low+1,PAL.redsoil[1],230); if(hash(gx0+x,0,seed+83)<0.4)c.put(x,low+2,PAL.redsoil[2],140); } }
    }
    return c;
  }

  // ============================== RED-SANDSTONE STRATA (shared face texture) ===================
  function strataCell(x,gy,seed){
    const row=Math.floor(gy/9);
    const shear=Math.round(hash(row,0,seed+33)*6)-3;
    const cx=Math.floor((x+shear)/9);
    const jog=Math.round(hash(cx,row,seed+34)*7)-3;
    const cy=Math.floor((gy+jog)/9);
    return cx*131+cy*17+((cx*cy)&7);
  }
  function strataColor(x,gy,seed){
    const wob=Math.round((hash(Math.floor(x/7),0,seed+18)-0.5)*4+Math.sin(x*0.23+seed)*1.6);
    const gyw=gy+wob;
    const region=Math.floor(gyw/12);
    const bedH=5+Math.round(hash(0,region,seed+90)*3);        // 5..8 px beds, varying by course
    const bed=Math.floor(gyw/bedH), ly=((gyw%bedH)+bedH)%bedH;
    let f=2.5+hash(0,bed,seed)*2.5; f=Math.max(1,Math.min(6,f));
    let c=rampAt(PAL.redrock,f,x,gy);
    if(ly===0)c=rampAt(PAL.redrock,Math.max(1,f-1.8),x,gy);   // bedding contact shade
    else if(ly===bedH-1&&hash(x>>2,bed,seed+91)<0.55)c=rampAt(PAL.redrock,Math.min(6,f+1),x,gy); // sunlit ledge lip
    if(gyw<12&&hash(x,gy,seed+27)<0.22)c=rampAt(PAL.redrock,Math.min(6,f+1),x,gy);
    const n=hash(x,gy,seed+11);
    if(n<0.06)c=rampAt(PAL.redrock,Math.max(1,f-1),x,gy);
    else if(n>0.94)c=rampAt(PAL.redrock,Math.min(6,f+1),x,gy);
    const rn=Math.sin(x*0.85+seed*1.7)+Math.sin(x*0.33+seed*3.1)+(hash(x,0,seed+60)-0.5)*1.2;
    if(rn<-1.5)c=rampAt(PAL.redrock,Math.max(1,f-1.5),x,gy);  // rain flute (soft)
    else if(rn>2.1)c=rampAt(PAL.redrock,Math.min(6,f+0.8),x,gy);
    const id=strataCell(x,gy,seed);
    if(id!==strataCell(x-1,gy,seed)&&hash(x,gy,seed+35)<0.55)c=rampAt(PAL.redrock,Math.max(1,f-1.5),x,gy);
    if(id!==strataCell(x,gy-1,seed)&&hash(x,gy,seed+36)<0.5)c=rampAt(PAL.redrock,Math.max(1,f-1.2),x,gy);
    if(hash(x,gy,seed+3)<0.04&&f>=4)c=PAL.redrock[6];
    return c;
  }
  // facet light law (upper-left key, like the boat bake):
  //   W-facing facet = +1.2 ramp steps lit · E-facing = dithered toward KEY · S face = base
  function facetShade(c,facing,wx,wy){
    if(facing==='w')return dpick(wx,wy,c,mixc(c,PAL.redrock[6],0.5),0.55);
    if(facing==='e')return dpick(wx,wy,c,mixc(c,PAL.KEY,0.55),0.6);
    return c;
  }

  function talus(x,y,seed){
    const lean=Math.round(Math.sin(y*0.45+seed*1.3)*2);
    const rowH=2+Math.round(hash(0,y>>2,seed+47));
    const ry=Math.floor(y/rowH);
    const off=Math.round(hash(0,ry,seed+45)*11);
    const slabW=7+Math.round(hash(ry,0,seed+46)*6);
    const key=x+off+lean, sx=Math.floor(key/slabW);
    const rr=hash(sx,ry,seed+40);
    if(rr<0.13)return hash(x,y,seed+43)<0.5?PAL.sand[2]:PAL.sand[3];
    let f=Math.max(1,Math.min(6,2+rr*3));
    let c=rampAt(PAL.redrock,f,x,y);
    const ly=((y%rowH)+rowH)%rowH;
    if(ly===0)c=rampAt(PAL.redrock,Math.min(6,f+2),x,y);
    else if(ly===rowH-1)c=PAL.redrock[1];
    if(((key%slabW)+slabW)%slabW===0)c=PAL.redrock[1];
    if(hash(x,y,seed+41)<0.05)c=PAL.redrock[5];
    return c;
  }

  // ============================== CLIFF (iso plateau faces, stackable bands) ===================
  const CLIFF_PIECES=['faceS','cornSW','cornSE','sideW','sideE','innSW','innSE','diagSW','diagSE'];
  // geometry helpers: silhouette + facet map per piece. Returns {on, facing:'s'|'w'|'e'|'cap', arris}
  function cliffGeom(piece,x,y,seed){
    const FW=9;                                          // corner side-facet width
    switch(piece){
      case 'faceS': return {on:true,facing:'s'};
      case 'cornSW': { // plateau corner: open to S and W → lit W facet on the left
        const lim=FW+Math.round(Math.sin(y*0.25+seed)*1.2);
        return {on:true,facing:x<lim?'w':'s',arris:x===lim,sil:x===0}; }
      case 'cornSE': { const lim=TILE-1-FW-Math.round(Math.sin(y*0.25+seed)*1.2);
        return {on:true,facing:x>lim?'e':'s',arris:x===lim,sil:x===TILE-1}; }
      case 'sideW': { // edge runs N–S, drop to the W: lit facet strip on the left, cap ground right
        const lim=6+Math.round(Math.sin(y*0.25+seed)*1.2);
        return {on:true,facing:x<lim?'w':'cap',arris:x===lim,sil:x===0}; }
      case 'sideE': { const lim=TILE-7-Math.round(Math.sin(y*0.25+seed)*1.2);
        return {on:true,facing:x>lim?'e':'cap',arris:x===lim,sil:x===TILE-1}; }
      case 'innSW': { // concave fold: S face meeting a W-going face — fold shadow on the left
        return {on:true,facing:'s',fold:x<6?(6-x)/6:0}; }
      case 'innSE': { return {on:true,facing:'s',fold:x>TILE-7?(x-(TILE-7))/6:0}; }
      case 'diagSW': { const lim=Math.round((y/(TILE-1))*26)+2;   // 45° face receding left-down
        return {on:x>=lim-2,facing:x<lim+FW-4?'w':'s',arris:Math.abs(x-(lim+FW-4))<1,sil:x<lim}; }
      case 'diagSE': { const lim=TILE-3-Math.round((y/(TILE-1))*26);
        return {on:x<=lim+2,facing:x>lim-FW+4?'e':'s',arris:Math.abs(x-(lim-FW+4))<1,sil:x>lim}; }
    }
    return {on:true,facing:'s'};
  }
  function cliff(piece,opts){ opts=opts||{}; const band=opts.band||'mid', seed=opts.seed|0,
      gx0=(opts.gx|0)*TILE,
      gy0=opts.gy!=null?(opts.gy|0):(band==='cap'?0:band==='mid'?32:64), feature=opts.feature||null;
    const c=mkBuf(TILE,TILE);
    const capH=10;                                        // grass rollover depth on cap band
    for(let y=0;y<TILE;y++)for(let x=0;x<TILE;x++){
      const g=cliffGeom(piece,x,y,seed); if(!g.on)continue;
      const gy=gy0+y, wx=gx0+x;
      let px;
      const isCapGround = g.facing==='cap';
      if(isCapGround && band!=='cap')continue;      // below the cap row, the lower ground tile shows through
      const rollOK = g.facing==='s';                // grass rolls over S faces only; side strips stay rock
      if(band==='cap' && ((y<capH&&rollOK)||isCapGround) ){
        if(isCapGround){ px=groundColor('grass',wx,gy,seed); }
        else{
          let fr=capH-1-Math.round(hash(wx,3,seed)*3);
          if(hash(wx,7,seed+40)<0.16)fr-=4;                // slump gully notch
          else if(hash(wx,11,seed+41)<0.14)fr+=2;          // grass tongue hangs lower
          fr=Math.max(1,fr);
          if(y>fr)px=strataColor(wx,gy,seed);
          else{
            px=groundColor('grass',wx,gy,seed);
            if(hash(wx,y,seed+18)<0.10)px=PAL.straw;
            if(y>=fr-1)px=dpick(wx,y,px,mixc(px,PAL.KEY,0.3),0.5);   // undercut shadow at the lip
          }
        }
      } else { px=strataColor(wx,gy,seed); }
      if(band==='toe' && y>=18 && g.facing!=='cap'){ px=talus(wx,y+gy0,seed); }  // slab-talus apron (NO waterline — shader owns the water)
      if(!isCapGround && band==='cap' && y>=capH) {} // face continues
      if(g.facing==='w'&&!isCapGround&&!(band==='cap'&&y<capH))px=facetShade(px,'w',wx,gy);
      if(g.facing==='e'&&!isCapGround&&!(band==='cap'&&y<capH))px=facetShade(px,'e',wx,gy);
      if(g.fold)px=dpick(wx,gy,px,mixc(px,PAL.KEY,0.45),g.fold*0.8);
      if(g.arris)px=mixc(px,PAL.redrock[6],0.45);         // sunlit corner arris
      if(g.sil)px=mixc(px,PAL.KEY,0.5);                   // silhouette rim on open edges
      c.put(x,y,px);
    }
    // side pieces: contact shadow where the facet meets lower ground + cap-edge sod lip
    if(piece==='sideW'||piece==='sideE'||piece==='cornSW'||piece==='cornSE'){
      const westSide=piece.endsWith('W');
      for(let y=0;y<TILE;y++){
        const x=westSide?0:TILE-1;
        if(band==='toe'){ c.put(x,y,mixc(PAL.shadow,PAL.redrock[1],0.3),200); }
      }
    }
    if(band==='toe'&&feature==='cave'&&(piece==='faceS'||piece==='innSW'||piece==='innSE'))carveCave(c,seed);
    // clinging shrubs on upper faces
    if(band!=='toe'){
      const nT=band==='cap'?4:3;
      for(let t=0;t<nT;t++){ const gx2=2+Math.floor(hash(t,band==='cap'?1:2,seed+30)*28);
        const gyv=(band==='cap'?12:3)+Math.floor(hash(t,9,seed+31)*16);
        const g=cliffGeom(piece,gx2,gyv,seed);
        if(g.on&&g.facing!=='cap'){ c.put(gx2,gyv,PAL.grn[3]); c.put(gx2,gyv-1,PAL.grn[4]);
          if(hash(t,5,seed+32)<0.5)c.put(gx2+1,gyv,PAL.grn[2]); } }
    }
    return c;
  }
  function carveCave(c,seed){
    const cx=15+Math.round((hash(0,0,seed+70)-0.5)*6);
    const halfW=7+Math.round(hash(2,0,seed+72)*2);
    const archTop=10+Math.round(hash(1,0,seed+71)*2), floorY=31;
    for(let x=cx-halfW;x<=cx+halfW;x++){ if(x<0||x>=TILE)continue;
      const nx=(x-cx)/halfW;
      const top=archTop+Math.round((1-Math.sqrt(Math.max(0,1-nx*nx)))*7);
      for(let y=top;y<=floorY;y++){
        const depth=1-Math.abs(nx), vy=(y-top)/(floorY-top+0.001);
        const dk=0.82-0.26*vy*(0.4+0.6*depth);
        c.put(x,y,mixc(PAL.redrock[2],PAL.KEY,Math.min(0.9,dk)));
      }
      if(nx<-0.15&&nx>-0.9)c.put(x,top-1,PAL.redrock[5]);
      else c.put(x,top-1,PAL.redrock[1]);
    }
    for(let b=0;b<4;b++){ const bx=cx-halfW+1+Math.round(hash(b,3,seed+73)*(halfW*2-2));
      const bw=2+Math.round(hash(b,4,seed+74)), bh=2+Math.round(hash(b,5,seed+75));
      for(let yy=floorY-bh+1;yy<=floorY;yy++)for(let xx=bx;xx<bx+bw;xx++)
        c.put(xx,yy,yy===floorY-bh+1?PAL.redrock[4]:PAL.redrock[2+(b&1)]); }
  }
  function column(piece,rows,opts){ opts=opts||{}; const seed=opts.seed|0;
    const H=rows*TILE, out=mkBuf(TILE,H);
    for(let r=0;r<rows;r++){ const band=r===0?'cap':(r===rows-1?'toe':'mid');
      const cell=cliff(piece,{band,gy:r*TILE,seed,feature:(band==='toe'?opts.feature:null)});
      for(let y=0;y<TILE;y++)for(let x=0;x<TILE;x++){ const i=(y*TILE+x)*4;
        if(cell.buf[i+3]){ const d=((r*TILE+y)*TILE+x)*4;
          out.buf[d]=cell.buf[i];out.buf[d+1]=cell.buf[i+1];out.buf[d+2]=cell.buf[i+2];out.buf[d+3]=cell.buf[i+3]; } } }
    return out;
  }

  // ============================== DUNE (single-band grass→sand slump bank) =====================
  function dune(piece,opts){ opts=opts||{}; const seed=opts.seed|0, gx0=(opts.gx|0)*TILE;
    const c=mkBuf(TILE,TILE), capH=11;
    for(let y=0;y<TILE;y++)for(let x=0;x<TILE;x++){
      const g=cliffGeom(piece,x,y,seed); if(!g.on)continue;
      const wx=gx0+x;
      let px;
      const isCapGround=g.facing==='cap';
      if(isCapGround||y<capH-Math.round(hash(wx,1,seed)*4)){
        px=groundColor('marram',wx,y,seed);
      } else {
        const tt=Math.min(1,(y-capH)/18);
        let f=4-tt*2.6+(hash(wx,y,seed+4)-0.5)*0.8;
        px=rampAt(PAL.sand,f,wx,y);
        if(((wx*2+y)%9===0)&&hash(wx,y,seed)<0.6)px=rampAt(PAL.sand,Math.max(0,f-1.4),wx,y); // wind streaks
        if(y>=TILE-4+Math.round(Math.sin(wx*0.35+seed)*1.5))px=dpick(wx,y,px,mixc(px,PAL.KEY,0.3),0.55); // slump-foot shadow, wavy
      }
      if(g.facing==='w'&&!isCapGround)px=dpick(wx,y,px,mixc(px,PAL.sand[5],0.5),0.45);
      if(g.facing==='e'&&!isCapGround)px=dpick(wx,y,px,mixc(px,PAL.KEY,0.35),0.5);
      if(g.fold)px=dpick(wx,y,px,mixc(px,PAL.KEY,0.3),g.fold*0.7);
      if(g.sil)px=mixc(px,PAL.KEY,0.35);
      c.put(x,y,px);
    }
    // marram tufts poking down the slump
    for(let t=0;t<5;t++){ const gx2=2+Math.floor(hash(t,4,seed+30)*28);
      const gyv=capH+1+Math.floor(hash(t,9,seed+31)*14);
      const g=cliffGeom(piece,gx2,gyv,seed);
      if(g.on&&g.facing!=='cap'){ c.put(gx2,gyv,PAL.grn[3]); c.put(gx2,gyv-1,PAL.grn[4]); c.put(gx2,gyv-2,PAL.grn[2]); } }
    return c;
  }

  // ============================== SPRITES (pure rock on transparency) ==========================
  function stack(size,opts){ opts=opts||{}; const seed=opts.seed|0;
    const W=size==='reef'?24:size==='s'?16:size==='m'?22:30;
    const H=size==='reef'?10:size==='s'?14:size==='m'?22:30;
    const c=mkBuf(W,H), cx=(W-1)/2;
    if(size==='reef'){
      for(let x=0;x<W;x++){ const top=H-2-Math.round((Math.sin(x*0.45+seed)+1)*2.2+hash(x,0,seed)*1.4);
        for(let y=Math.max(0,top);y<H;y++){ let col=talus(x+3,y+22,seed); if(y===top)col=PAL.redrock[5]; c.put(x,y,col); } }
      keyline(c); return c;
    }
    const baseY=H-1;
    for(let y=0;y<H;y++){
      const t=y/(H-1);
      let hw=(W/2-1)*(0.92-0.28*t*t);                   // fat layered slab, near-vertical sides
      if(y>baseY-3)hw*=0.72;                            // wave-cut undercut notch
      hw+=(hash(0,y>>1,seed+3)-0.5)*2.6;                // angular stepped silhouette
      hw=Math.max(2,hw);
      const L=Math.round(cx-hw),R=Math.round(cx+hw);
      for(let x=L;x<=R;x++){ if(x<0||x>=W)continue;
        let col=strataColor(x+5,y+seed*7,seed);
        if(y%4===3)col=dpick(x,y,col,mixc(col,PAL.KEY,0.5),0.6);       // bedding shade
        else if(y%4===0)col=dpick(x,y,col,PAL.redrock[5],0.45);        // sunlit ledge lip
        if(x<cx-hw*0.35)col=dpick(x,y,col,mixc(col,PAL.redrock[6],0.45),0.5);   // lit west
        else if(x>cx+hw*0.35)col=dpick(x,y,col,mixc(col,PAL.KEY,0.5),0.6);      // shaded east
        if(y>baseY-3)col=mixc(col,PAL.KEY,0.4);
        c.put(x,y,col);
      }
    }
    if(size==='l'||size==='m'){ const gw=size==='l'?5:3;
      for(let x=Math.round(cx)-gw;x<=Math.round(cx)+gw;x++)
        if(hash(x,0,seed+5)<0.85){c.put(x,0,PAL.grn[3]);c.put(x,1,PAL.grn[4]);} }
    keyline(c); return c;
  }
  function boulder(size,opts){ opts=opts||{}; const seed=opts.seed|0;
    const W=size==='s'?14:size==='m'?20:28, H=size==='s'?9:size==='m'?12:16;
    const c=mkBuf(W,H), cx=(W-1)/2, cy=H-2;
    for(let y=0;y<H;y++)for(let x=0;x<W;x++){
      const dx=(x-cx)/(W/2-0.5), dy=(y-cy)/(H-2);
      if(dx*dx+dy*dy*1.6>1+(hash(x,y,seed)-0.5)*0.25)continue;
      let f=3+hash(x>>1,y>>1,seed+2)*2;
      let col=rampAt(PAL.redrock,f,x,y);
      if(y%4===0)col=dpick(x,y,col,PAL.redrock[5],0.5);            // bedding
      if(dx<-0.3)col=dpick(x,y,col,mixc(col,PAL.redrock[6],0.4),0.5);
      if(dx>0.35)col=dpick(x,y,col,mixc(col,PAL.KEY,0.45),0.55);
      if(y>=H-2)col=mixc(col,PAL.KEY,0.35);
      c.put(x,y,col);
    }
    keyline(c); return c;
  }
  function keyline(c){ // 1px dark rim where opaque meets transparent (sprites only)
    const {buf,w,h}=c, K=hex2rgb(PAL.KEY);
    const solid=(x,y)=>x>=0&&x<w&&y>=0&&y<h&&buf[(y*w+x)*4+3]>0;
    for(let y=0;y<h;y++)for(let x=0;x<w;x++){ const i=(y*w+x)*4; if(!buf[i+3])continue;
      if(!solid(x-1,y)||!solid(x+1,y)||!solid(x,y-1)||!solid(x,y+1)){
        buf[i]=Math.round(buf[i]*0.35+K[0]*0.65);buf[i+1]=Math.round(buf[i+1]*0.35+K[1]*0.65);buf[i+2]=Math.round(buf[i+2]*0.35+K[2]*0.65); } }
  }

  root.ShoreIso={ TILE, PAL,
    GROUND:['grass','marram','sand','ripple','shingle','shelf'],
    FRINGE_PIECES, CLIFF_PIECES,
    ground, groundColor, fringe, cliff, column, dune, stack, boulder, strataColor, talus, hash,
    mkBuf, dpick, rampAt, mixc, hex2rgb };
})(typeof globalThis!=='undefined'?globalThis:window);
