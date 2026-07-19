/* Hidden Harbours — parametric SHORELINE tile kit (Prince Edward Island red-sandstone coast).
   Companion to wharfKitRig.js. Same KTC pixel conventions: no AA, upper-left key light,
   quantised palette ramps, hash-value noise, per-column faces + baked waterline foam.
   Square 32x32 near-plan grid, camera from the SOUTH.

   Two render paths, dispatched by material:
     • FLAT materials (grass, beach, tidalDry, tidalWet, shallows, ledge):
         wharf-style auto-tile cell = 32 deck + face overhang.  8-way faces:
         S = tall face toward camera, E/W = short side faces, N = thin occluded lip.
         opts = { open:{n,e,s,w}, cut, inner, seed, frame }
     • BAND materials (cliff, dune): tall landforms assembled from stacked 32x32 tileable
         bands (cap / mid / toe) so any height 3..20 m builds from the same rows.
         opts = { band:'cap'|'mid'|'toe', shape:'ctr'|'endL'|'endR'|'turnL'|'turnR', gy:globalRowY, seed }

   API (globalThis.ShoreKit):
     ShoreKit.TILE=32  ShoreKit.CELL_H=56
     ShoreKit.FLAT   = ['grass','beach','tidalDry','tidalWet','shallows','ledge']
     ShoreKit.BAND   = ['cliff','dune']
     ShoreKit.render(material, opts) -> { data:Uint8ClampedArray, w, h }
     ShoreKit.stack(material, {shape, rows, seed}) -> full-height composite {data,w,h}  (hero column)
*/
(function(root){
  const TILE=32, CELL_H=56, DECK=32;

  const PAL={
    redrock:['#4a1e14','#6b2c1c','#87301f','#a04528','#bd6038','#d17c4c','#e0a06e'],
    redsoil:['#4a2114','#6c3019','#8a3f22','#a5502c','#bd6538'],
    sand   :['#9c5f30','#bd8149','#d4a061','#e6bf83','#f2d6a0','#f9ead0'],
    straw  :'#c9b06a',
    redsand:['#5e2718','#7c3520','#98462a','#b25e36','#c87a4c','#dc9868'],
    grn    :['#1c3216','#2f4a1d','#456b26','#628b32','#83a848','#a3c460'],
    spruce :['#0f1f0d','#182f15','#22421d','#305628','#3d6a30'],
    water  :['#123038','#184a4a','#2a6858','#469074','#6cb488','#96d0a0'],
    deep   :['#0d2233','#123141','#193f52','#255c6e'],       // open-sea navy -> teal (3501 deep water)
    KEY:'#171009', foam:'#eaf3ee', wetglint:'#c2d8e2', drift:'#6b543a',
    driftlt:'#8a7050', shadow:'#0c171a', seaweed:'#43502a'
  };
  const FACE_H={ grass:14, beach:8, tidalDry:5, tidalWet:5, shallows:0, ledge:20 };

  function hash(x,y,s){ let h=(x*374761393+y*668265263+((s|0))*1274126177)|0;
    h=Math.imul(h^(h>>>13),1274126177); return ((h^(h>>>16))>>>0)/4294967296; }
  function h2(a,b){ return hash(a,b,777); }
  function hex2rgb(h){ return [parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]; }
  function mix(a,b,t){ const A=Array.isArray(a)?a:hex2rgb(a),B=Array.isArray(b)?b:hex2rgb(b);
    return [A[0]+(B[0]-A[0])*t,A[1]+(B[1]-A[1])*t,A[2]+(B[2]-A[2])*t]; }
  // clear-water depth ramp: 0 = golden sand-glow at the shore -> 1 = deep navy open sea (refs 3507/3501)
  function waterDepth(d){ d=Math.max(0,Math.min(1,d));
    const stops=[mix(PAL.water[4],PAL.sand[3],0.55), PAL.water[4], PAL.water[3], PAL.water[2],
                 PAL.deep[3], PAL.deep[2], PAL.deep[1], PAL.deep[0]];
    const t=d*(stops.length-1), i=Math.min(stops.length-2,Math.floor(t));
    return mix(stops[i],stops[i+1],t-i); }

  function newBuf(h){ return new Uint8ClampedArray(TILE*h*4); }
  function mkPut(buf,h){ return (x,y,c,a)=>{ if(x<0||x>=TILE||y<0||y>=h)return;
    const rgb=Array.isArray(c)?c:hex2rgb(c); const i=(y*TILE+x)*4;
    buf[i]=rgb[0];buf[i+1]=rgb[1];buf[i+2]=rgb[2];buf[i+3]=(a==null?255:a); }; }

  // ---- shared strata function for red sandstone (continuous over global y) ----
  // irregular fracture cells (sheared grid) so jointing reads jagged, never a brick wall
  function strataCell(x,gy,seed){
    const row=Math.floor(gy/9);
    const shear=Math.round(hash(row,0,seed+33)*6)-3;      // whole course shears sideways
    const cx=Math.floor((x+shear)/9);
    const jog=Math.round(hash(cx,row,seed+34)*7)-3;       // each block juts up/down
    const cy=Math.floor((gy+jog)/9);
    return cx*131+cy*17+((cx*cy)&7);
  }
  function strataColor(x,gy,seed){
    // wobble the bedding so lines are never dead-horizontal
    const wob=Math.round((hash(Math.floor(x/5),0,seed+18)-0.5)*4 + Math.sin(x*0.5+seed)*1.3);
    const gyw=gy+wob;
    const band=Math.floor(gyw/4);
    let idx=2+Math.round(hash(0,band,seed)*3);
    idx=Math.max(1,Math.min(6,idx));
    let c=PAL.redrock[idx];
    if(gyw<14 && hash(x,gy,seed+27)<0.28)c=PAL.redrock[Math.min(6,idx+1)]; // sunlit upper ledges
    const n=hash(x,gy,seed+11);
    if(n<0.07)c=PAL.redrock[Math.max(1,idx-1)];          // grain speckle
    else if(n>0.93)c=PAL.redrock[Math.min(6,idx+1)];
    if((gyw&3)===0 && hash(x,band,seed+5)<0.5)c=PAL.redrock[Math.max(1,idx-2)]; // wobbly bedding shade
    // vertical erosion runnels — rain-carved flutes down the exposed face (ref 3501)
    const rn=Math.sin(x*0.85+seed*1.7)+Math.sin(x*0.33+seed*3.1)+(hash(x,0,seed+60)-0.5)*1.2;
    if(rn<-1.15)      c=PAL.redrock[Math.max(0,idx-2)];   // deep flute channel
    else if(rn<-0.5)  c=PAL.redrock[Math.max(1,idx-1)];   // channel flank
    else if(rn>1.7)   c=PAL.redrock[Math.min(6,idx+1)];   // sunlit rib between flutes
    // irregular polygonal cracks between fracture cells
    const id=strataCell(x,gy,seed);
    if(id!==strataCell(x-1,gy,seed) && hash(x,gy,seed+35)<0.85)c=PAL.redrock[1];               // vertical-ish crack
    if(id!==strataCell(x,gy-1,seed) && hash(x,gy,seed+36)<0.7)c=PAL.redrock[Math.max(0,idx-2)]; // under-ledge shadow
    if(hash(x,gy,seed+3)<0.05 && idx>=4)c=PAL.redrock[6]; // sunlit warm fleck
    return c;                                            // (noise already non-periodic; bands still key off global y)
  }

  // ============================================================ BAND (cliff/dune)
  function renderBand(material, opts){
    opts=opts||{}; const band=opts.band||'mid', shape=opts.shape||'ctr';
    const seed=(opts.seed|0), gy0=(opts.gy|0), feature=opts.feature||null;
    const h=32, buf=newBuf(h), put=mkPut(buf,h);

    // horizontal deck-membership for the vertical face, per shape (turning a headland)
    // returns {on:bool, recede:0..1} ; recede>0 = a shaded receding facet toward that side
    function col(x,y){
      switch(shape){
        case 'endL':  return { on:x>=4+Math.round(hash(0,y,seed)*2), edge:x<6 };
        case 'endR':  return { on:x<=27-Math.round(hash(9,y,seed)*2), edge:x>25 };
        case 'turnL': { const lim=Math.round((y/ (h-1))*20);            // face recedes to the left going down
                        return { on:x>=lim, recede:x<lim+5?1:0, edge:x<lim+2 }; }
        case 'turnR': { const lim=31-Math.round((y/(h-1))*20);
                        return { on:x<=lim, recede:x>lim-5?1:0, edge:x>lim-2 }; }
        default: return { on:true };
      }
    }

    if(material==='cliff'){
      const capGrass = band==='cap';
      const grassBot = 10;                               // v2: deeper marram fringe rolling over the lip
      for(let y=0;y<h;y++)for(let x=0;x<TILE;x++){
        const c=col(x,y); if(!c.on)continue;
        const gy=gy0+y;
        let px;
        if(capGrass && y<grassBot){
          // grass cap rolling over the lip; jagged eroded rock-line with slump gullies
          let fr=grassBot-1 - Math.round(hash(x,3,seed)*3);
          if(hash(x,7,seed+40)<0.16)fr-=4;               // deep erosion notch (slump gully)
          else if(hash(x,11,seed+41)<0.14)fr+=2;         // grass tongue hangs lower
          fr=Math.max(1,fr);
          if(y>fr){ px=strataColor(x,gy,seed); }        // below fringe = rock already
          else {
            let gi=3+Math.round(hash(x,y,seed+2)*2);
            px=PAL.grn[Math.max(1,Math.min(5,gi))];
            if(y===0)px=PAL.grn[Math.max(1,gi-1)];
            if(hash(x,y,seed+8)<0.14)px=PAL.grn[5];
            if(hash(x,y,seed+18)<0.10)px=PAL.straw;      // v2: dry straw blades in the marram
            if(y>=grassBot-2 && hash(x,y,seed+19)<0.5)px=mix(px,PAL.KEY,0.25); // undercut shadow at lip
          }
        } else {
          px=strataColor(x,gy,seed);
        }
        // toe: talus rubble + waterline at the base
        if(band==='toe' && y>=16){
          px=talus(x,y,seed);
        }
        if(c.recede) px=mix(px,PAL.KEY,0.26);            // receding facet shade
        if(c.edge)   px=mix(px,PAL.KEY,0.2);
        put(x,y,px);
      }
      if(band==='toe'){ waterlineRow(put,h,seed,shape); }
      if(band==='toe' && feature==='cave') carveCave(put,seed,col);
      // clinging shrubs / vines on the upper face (ref 3507) — denser green clumps
      if(band!=='toe'){
        const nT=band==='cap'?5:4;
        for(let t=0;t<nT;t++){ const gx=2+Math.floor(hash(t,band==='cap'?1:2,seed+30)*28);
          const gyv=(band==='cap'?11:3)+Math.floor(hash(t,9,seed+31)*17);
          if(col(gx,gyv).on){ const gi=hash(t,5,seed+32);
            put(gx,gyv,PAL.grn[3]); put(gx,gyv-1,PAL.grn[4]); if(gi<0.6)put(gx+1,gyv,PAL.grn[2]);
            if(gi<0.4){put(gx,gyv-2,PAL.grn[5]); put(gx+1,gyv-1,PAL.grn[3]);}         // taller clinging vine
            if(gi>0.7)put(gx-1,gyv,PAL.grn[2]); } }
      }
    } else { // dune — soft sand mound, marram grass
      for(let y=0;y<h;y++)for(let x=0;x<TILE;x++){
        const c=col(x,y); if(!c.on)continue;
        const gy=gy0+y;
        // vertical shade: lighter near top of whole dune, darker to base
        const tt=Math.min(1,gy/60);
        let base=mix(PAL.sand[4],PAL.sand[2],tt*0.8);      // v2: paler pink-cream dune (ref 3504)
        // wind ripple streaks (diagonal)
        if((x+ (gy>>1))%6===0 && hash(x,gy,seed)<0.6) base=mix(base,PAL.sand[1],0.5);
        if(hash(x,gy,seed+4)<0.06) base=PAL.sand[4];
        if(hash(x,gy,seed+7)<0.05) base=mix(PAL.sand[2],PAL.KEY,0.2);
        let px=base;
        if(band==='cap' && y<12){                        // marram grass crest
          const fr=11-Math.round(hash(x,1,seed)*4);
          if(y<fr){ let gi=3+Math.round(hash(x,y,seed+2)*2); px=PAL.grn[Math.max(2,Math.min(5,gi))]; if(hash(x,y,seed+9)<0.2)px=PAL.grn[5]; }
        }
        if(c.recede) px=mix(px,PAL.KEY,0.22);
        if(c.edge)   px=mix(px,PAL.KEY,0.16);
        put(x,y,px);
      }
      // marram tufts poking down the slope
      for(let t=0;t<5;t++){ const gx=2+Math.floor(hash(t,4,seed+30)*28);
        const gyv=(band==='cap'?12:2)+Math.floor(hash(t,9,seed+31)*18);
        if(col(gx,gyv).on){ put(gx,gyv,PAL.grn[3]); put(gx,gyv-1,PAL.grn[4]); put(gx,gyv-2,PAL.grn[2]); } }
      if(band==='toe'){ for(let x=0;x<TILE;x++){ if(!col(x,h-1).on)continue; // blend to beach sand
        put(x,h-1,PAL.sand[2]); put(x,h-2,mix(PAL.sand[2],PAL.sand[3],0.5)); } }
    }
    return { data:buf, w:TILE, h };
  }

  function talus(x,y,seed){
    // flat tumbled sandstone SLABS piled at the cliff foot (refs 3501/3502) — thin wide plates,
    // bright sunlit top edge, dark undercut gap, occasional sand showing between the piles.
    const lean=Math.round(Math.sin(y*0.45+seed*1.3)*2);       // the whole pile leans
    const rowH=2+Math.round(hash(0,y>>2,seed+47));            // 2..3 px plate thickness (varies per course)
    const ry=Math.floor(y/rowH);
    const off=Math.round(hash(0,ry,seed+45)*11);             // each course shifts sideways
    const slabW=7+Math.round(hash(ry,0,seed+46)*6);          // 7..13 px wide plates
    const key=x+off+lean;
    const sx=Math.floor(key/slabW);
    const rr=hash(sx,ry,seed+40);
    if(rr<0.13) return hash(x,y,seed+43)<0.5?PAL.sand[2]:PAL.sand[3];   // sand gap between slabs
    let idx=2+Math.round(rr*3); idx=Math.max(1,Math.min(6,idx));
    let c=PAL.redrock[idx];
    const ly=y%rowH;
    if(ly===0) c=PAL.redrock[Math.min(6,idx+2)];             // sunlit top plate edge
    else if(ly===rowH-1) c=PAL.redrock[1];                   // dark undercut gap
    if(key%slabW===0||key%slabW===slabW-1) c=PAL.redrock[1]; // vertical joints
    if(hash(x,y,seed+41)<0.05) c=PAL.redrock[5];             // catch-light fleck
    if(hash(x,y,seed+42)<0.03) c=mix(c,PAL.KEY,0.4);         // damp / moss shadow speck
    return c;
  }
  function carveCave(put,seed,col){
    // eroded sea-cave arch at the toe base: rounded top, dark receding throat, fallen blocks at the mouth
    const cx=15+Math.round((hash(0,0,seed+70)-0.5)*6);
    const halfW=7+Math.round(hash(2,0,seed+72)*2);        // 7..9 wide
    const archTop=12+Math.round(hash(1,0,seed+71)*2), floorY=31;
    for(let x=cx-halfW;x<=cx+halfW;x++){ if(x<0||x>=TILE)continue;
      const nx=(x-cx)/halfW;                              // -1..1
      const top=archTop+Math.round((1-Math.sqrt(Math.max(0,1-nx*nx)))*7); // arch curves down at the sides
      for(let y=top;y<=floorY;y++){ if(col && !col(x,y).on)continue;
        const depth=1-Math.abs(nx);                       // 0 side .. 1 centre (deepest/darkest)
        const vy=(y-top)/(floorY-top+0.001);              // 0 lintel .. 1 floor
        const dk=0.82 - 0.26*vy*(0.4+0.6*depth);          // back-wall bounce lightens the lower centre
        put(x,y, mix(PAL.redrock[2],PAL.KEY,Math.min(0.9,dk)));
      }
      if(nx<-0.15 && nx>-0.9) put(x,top-1,PAL.redrock[5]); // sunlit rim on the upper-left of the arch
      else put(x,top-1,PAL.redrock[1]);                    // shadow lintel elsewhere
    }
    for(let b=0;b<4;b++){ const bx=cx-halfW+1+Math.round(hash(b,3,seed+73)*(halfW*2-2));
      const bw=2+Math.round(hash(b,4,seed+74)), bh=2+Math.round(hash(b,5,seed+75));
      for(let yy=floorY-bh+1;yy<=floorY;yy++)for(let xx=bx;xx<bx+bw;xx++){ if(col&&!col(xx,yy).on)continue;
        put(xx,yy, yy===floorY-bh+1?PAL.redrock[4]:PAL.redrock[2+(b&1)]); } }
  }
  function waterlineRow(put,h,seed,shape){
    // uneven, meandering surf line at the cliff toe instead of a dead-straight foam row
    for(let x=0;x<TILE;x++){
      const top=h-1-Math.round((Math.sin(x*0.6+seed*1.7)+1)*1.5 + hash(x,0,seed+52)*1.6);
      for(let y=Math.max(0,top);y<h;y++){ put(x,y, y===top?PAL.foam:mix(PAL.water[2],PAL.foam,0.18)); }
      if(top>0 && hash(x,0,seed+51)<0.3) put(x,top-1,PAL.foam,170); // spray fleck above the line
    }
  }

  // ============================================================ FLAT (grass/beach/tidal/shallows/ledge)
  function renderFlat(material, opts){
    opts=opts||{};
    const open=Object.assign({n:false,e:false,s:false,w:false}, opts.open||{});
    const cut=opts.cut||null, seed=(opts.seed|0), frame=(opts.frame|0)%4;
    const fh=FACE_H[material]!=null?FACE_H[material]:12;
    const H=CELL_H, buf=newBuf(H), put=mkPut(buf,H);

    function deckMask(x,y){ if(x<0||x>=TILE||y<0||y>=DECK)return false;
      switch(cut){ case 'se':return (x+y)<=31; case 'sw':return ((TILE-1-x)+y)<=31;
        case 'ne':return (x+(TILE-1-y))<=31; case 'nw':return ((TILE-1-x)+(TILE-1-y))<=31; default:return true; } }
    const edgeCut=(x,y)=>{ if(!cut)return false;
      return deckMask(x,y) && !deckMask(x+(cut.includes('e')?1:-1), y+(cut.includes('s')?1:-1)); };

    function deckColor(x,y){
      if(material==='grass'){
        let gi=3+Math.round(hash(x,y,seed+1)*2); let c=PAL.grn[Math.max(2,Math.min(5,gi))];
        if(hash(x,y,seed+2)<0.08)c=PAL.grn[5];
        if(hash(x,y,seed+3)<0.05)c=PAL.grn[2];
        if(hash(x,y,seed+4)<0.015)c=PAL.redsoil[2];      // bare soil scuff
        return c;
      }
      if(material==='beach'){
        let si=2+Math.round(hash(x,y,seed+1)*2); let c=PAL.sand[Math.max(1,Math.min(4,si))];
        if(hash(x,y,seed+2)<0.05)c=PAL.sand[4];
        if(hash(x,y,seed+8)<0.05)c=PAL.sand[5];             // v2: near-white dry grains (ref 3507)
        if(hash(x,y,seed+3)<0.03)c=mix(PAL.sand[1],PAL.KEY,0.15);
        if(hash(x,y,seed+9)<0.014)c=PAL.seaweed;            // v2: a touch more wrack
        return c;
      }
      if(material==='tidalDry'||material==='tidalWet'){
        // rippled red wet sand: sinuous banded ripples
        const wob=Math.sin((x*0.55)+Math.cos(y*0.4+seed)*1.3)+Math.sin(y*0.9+seed*2);
        const rip=(y*1.0 + wob*2.2);
        const b=Math.floor(rip/3);
        let idx=2+Math.round(hash(0,b,seed+6)*2); idx=Math.max(1,Math.min(5,idx));
        let c=PAL.redsand[idx];
        if(Math.floor(rip)%3===0)c=PAL.redsand[Math.min(5,idx+1)];   // crest catch-light
        if(Math.floor(rip)%3===2)c=PAL.redsand[Math.max(0,idx-1)];   // trough shade
        if(material==='tidalWet'){
          // v2.1: broad standing pools reflecting flat sky (ref 3508) — large, rare, seed-varied
          const pool=Math.sin(x*0.13+seed*2.3)+Math.sin(y*0.11+seed*1.1)+Math.sin((x-y)*0.07+seed*0.6);
          if(pool>1.55){
            let p=pool<1.75? mix(PAL.wetglint,c,0.55) : hex2rgb(PAL.wetglint);
            if(Math.floor(rip)%5===0)p=mix(p,c,0.4);                  // ripple crest pokes through the pool
            if(hash(x,y,seed+frame*3)<0.06)p=mix(p,PAL.foam,0.6);     // shimmer
            return p;
          }
          c=mix(c,PAL.water[3],0.28);                                 // wet sheen darkens+cools
          const gl=(y*1.0+wob*2.2);
          if(Math.floor(gl)%3===0 && hash(x,b,seed+frame*3)<0.4)c=mix(c,PAL.wetglint,0.55); // sky glint
        }
        return c;
      }
      if(material==='shallows'){
        // clear water graded by depth (ref 3507): golden sand-glow near shore -> teal -> navy offshore.
        const depth=opts.depth!=null?opts.depth:0.5;
        const blob=Math.sin(x*0.17+seed*1.9)+Math.sin(y*0.21+seed*0.7)+Math.sin((x+y)*0.08+seed);
        const dloc=Math.max(0,Math.min(1, depth + blob*0.06 + (hash(x,y,seed+31)-0.5)*0.05));
        let c=waterDepth(dloc);
        if(dloc<0.45 && Math.floor(y*0.7+Math.sin(x*0.5+seed)*2)%3===0) c=mix(c,PAL.sand[3],0.22);
        if(dloc<0.12 && hash(x,y,seed+13)<0.5) c=mix(c,PAL.foam,0.7);
        if(hash(x,y,seed+frame*2)<0.05)c=PAL.water[5];              // ripple glint
        if(hash(x,y,seed+12)<0.012)c=hex2rgb(PAL.foam);             // sun sparkle
        return c;
      }
      if(material==='ledge'){                                        // small red rock shelf, flat top
        let idx=3+Math.round(hash(x>>2,y>>2,seed)*2); idx=Math.max(2,Math.min(6,idx));
        let c=PAL.redrock[idx];
        // v2.1: irregular jointing — offset per course, some joints dropped
        const crs=Math.floor(y/9), jx=(x+crs*4)%8;
        if(jx===0 && hash(Math.floor((x+crs*4)/8),crs,seed+23)<0.6)c=PAL.redrock[Math.max(1,idx-2)];
        if(y%9===0 && hash(x>>3,crs,seed+24)<0.75)c=PAL.redrock[Math.max(1,idx-2)];
        if(hash(x,y,seed+3)<0.06)c=PAL.redrock[6];
        if(hash(x,y,seed+7)<0.02)c=PAL.grn[3];                       // grass in cracks
        return c;
      }
      return PAL.sand[2];
    }

    function faceCol(k){
      if(material==='grass'){ if(k===0)return PAL.redsoil[1]; if(k<fh-3)return PAL.redsoil[2]; if(k<fh-1)return PAL.redsand[1]; return PAL.shadow; }
      if(material==='beach'){ if(k<fh-2)return PAL.sand[1]; return mix(PAL.sand[0],PAL.water[1],0.4); }
      if(material==='ledge'){ if(k===0)return PAL.redrock[1]; if(k<fh-4)return strataColor(0,k,seed); if(k<fh-1)return PAL.redrock[1]; return PAL.shadow; }
      if(k<2)return mix(PAL.redsand[1],PAL.water[1],0.3); return PAL.water[1];   // tidal/shallows tiny lip
    }

    // ================= organic land coverage (rounded corners + wavy coastline) =================
    // Closed sides fill to the tile border so same-material neighbours tile seamlessly; an OPEN
    // side pulls the shoreline in along a wavy contour; two adjacent open sides ROUND the outer
    // corner. Isolated tiles (all sides open) become rounded blobs, never squares. A foam-lace
    // ring then traces whatever organic silhouette results.
    const isWater = material==='shallows';
    const R=8, BASE=3;
    const anyOpen = open.n||open.e||open.s||open.w;
    function inset(side,p){ if(!open[side])return -2;                       // closed -> fills past the border
      const s=seed*2+({n:11,e:23,s:37,w:51})[side];
      return BASE + (Math.sin(p*0.5+s)+Math.sin(p*0.17+s*1.7))*0.9 + hash(p,0,s)*1.7; }
    const iN=[],iS=[],iW=[],iE=[];
    for(let p=0;p<TILE;p++){ iN[p]=inset('n',p); iS[p]=inset('s',p); iW[p]=inset('w',p); iE[p]=inset('e',p); }
    function isLand(x,y){
      if(x<0||x>=TILE||y<0||y>=DECK)return false;
      if(cut && !deckMask(x,y))return false;                               // keep the 45deg diagonal cut
      if(open.n && y < iN[x])return false;
      if(open.s && (DECK-1-y) < iS[x])return false;
      if(open.w && x < iW[y])return false;
      if(open.e && (TILE-1-x) < iE[y])return false;
      const cn=(cx,cy)=>{ const dx=x-cx,dy=y-cy, rr=R+(hash(x,y,seed+70)-0.5)*2.2; return dx*dx+dy*dy>rr*rr; };
      if(open.n&&open.w && x<BASE+R && y<BASE+R && cn(BASE+R,BASE+R))return false;
      if(open.n&&open.e && x>TILE-1-BASE-R && y<BASE+R && cn(TILE-1-BASE-R,BASE+R))return false;
      if(open.s&&open.w && x<BASE+R && y>DECK-1-BASE-R && cn(BASE+R,DECK-1-BASE-R))return false;
      if(open.s&&open.e && x>TILE-1-BASE-R && y>DECK-1-BASE-R && cn(TILE-1-BASE-R,DECK-1-BASE-R))return false;
      return true;
    }
    const land=new Uint8Array(TILE*DECK);
    for(let y=0;y<DECK;y++)for(let x=0;x<TILE;x++) land[y*TILE+x]=isLand(x,y)?1:0;
    const L=(x,y)=>{ if(x>=0&&x<TILE&&y>=0&&y<DECK)return land[y*TILE+x];
      return ((x<0&&!open.w)||(x>=TILE&&!open.e)||(y<0&&!open.n)||(y>=DECK&&!open.s))?1:0; }; // closed borders read as land

    // ---- deck + inner curb ----
    const CURB= material==='grass'?2 : material==='beach'?1 : 0;
    for(let y=0;y<DECK;y++)for(let x=0;x<TILE;x++){
      if(!land[y*TILE+x])continue;
      let c=deckColor(x,y);
      if(CURB){ let cd=99;
        if(open.n)cd=Math.min(cd,y-iN[x]); if(open.s)cd=Math.min(cd,(DECK-1-y)-iS[x]);
        if(open.w)cd=Math.min(cd,x-iW[y]); if(open.e)cd=Math.min(cd,(TILE-1-x)-iE[y]);
        if(cd<CURB) c = material==='grass'? (cd<1?PAL.redsoil[1]:PAL.grn[2]) : mix(PAL.sand[1],PAL.KEY,0.1);
      }
      put(x,y,c);
    }

    // ---- foam / wet-sand transition ring hugging the organic coastline ----
    if(anyOpen && !isWater){
      const ringW = material==='grass'?2:3;
      for(let y=0;y<DECK;y++)for(let x=0;x<TILE;x++){
        if(land[y*TILE+x])continue;
        let near=99;
        for(let dy=-ringW;dy<=ringW && near>1;dy++)for(let dx=-ringW;dx<=ringW;dx++){
          if(L(x+dx,y+dy)){ const d=Math.abs(dx)+Math.abs(dy); if(d<near)near=d; } }
        if(near>ringW)continue;
        if(material==='grass'){ put(x,y, near<=1?PAL.redsoil[1]:mix(PAL.redsoil[2],PAL.KEY,0.18), near<=1?255:200); }
        else if(near<=1){ if(hash(x,y,seed+60)<0.9)put(x,y,PAL.foam); }
        else if(near===2){ if(hash(x,y,seed+61)<0.5)put(x,y,PAL.foam,210); }
        else if(hash(x,y,seed+62)<0.25)put(x,y,PAL.foam,150);
      }
    }

    // ---- southern face toward the camera, following the wavy land contour ----
    if((open.s || (cut&&cut.indexOf('s')>=0)) && !isWater){
      for(let x=0;x<TILE;x++){
        let bot=-1; for(let y=DECK-1;y>=0;y--){ if(land[y*TILE+x]){ bot=y; break; } }
        if(bot<0)continue;
        const efh=Math.max(2, fh - Math.round(hash(x,7,seed+23)*2));
        for(let k=0;k<efh;k++)put(x,bot+1+k,faceCol(k));
        if(material==='grass'){ if(hash(x,0,seed+20)<0.3)for(let k=2;k<efh-1;k++)put(x,bot+1+k,mix(PAL.redsoil[1],PAL.KEY,0.2)); // erosion runnel
          if(hash(x,0,seed+21)<0.14){put(x,bot+1,PAL.grn[3]);put(x,bot+2,PAL.grn[2]);} }                                          // grass overhang
        if(hash(x,0,seed+13)<0.5)put(x,bot+1+efh,PAL.foam);
        if(hash(x,0,seed+14)<0.2)put(x,bot+2+efh,PAL.foam,170);
      }
    }

    // ---- ragged material fringe: this tile's edge laps raggedly onto the neighbour below/around ----
    // (grass tongues spilling onto sand, sand licking onto the next tile — softens same-plane seams)
    const fr=opts.fringe?String(opts.fringe):'';
    if(fr && !isWater){
      const tongue=(x,yBase,dir)=>{ const t=Math.round(hash(x,dir,seed+80)*3);
        for(let k=0;k<=t;k++){ if(hash(x,k+dir,seed+81)<0.85){ let c;
          if(material==='grass'){ let gi=3+Math.round(hash(x,k,seed+82)*2); c=PAL.grn[Math.max(2,Math.min(5,gi))]; }
          else { let si=2+Math.round(hash(x,k,seed+82)*2); c=PAL.sand[Math.max(1,Math.min(4,si))]; }
          put(x, yBase + dir*k, c); } } };
      if(fr.includes('s')) for(let x=0;x<TILE;x++){ tongue(x,DECK-1,1); if(hash(x,9,seed+83)<0.25)put(x,DECK-2,material==='grass'?PAL.redsoil[2]:PAL.sand[1]); }
      if(fr.includes('n')) for(let x=0;x<TILE;x++) tongue(x,0,1);
    }

    return { data:buf, w:TILE, h:H };
  }

  function render(material, opts){
    if(material==='cliff'||material==='dune')return renderBand(material,opts);
    return renderFlat(material,opts);
  }

  // hero: stack a full-height band column, returns 32 x (rows*32)
  function stack(material, o){ o=o||{}; const shape=o.shape||'ctr', rows=o.rows||5, seed=(o.seed|0);
    const H=rows*32, buf=newBuf(H), put=mkPut(buf,H);
    for(let r=0;r<rows;r++){ const band= r===0?'cap':(r===rows-1?'toe':'mid');
      const cell=renderBand(material,{band,shape,gy:r*32,seed});
      for(let y=0;y<32;y++)for(let x=0;x<TILE;x++){ const i=(y*TILE+x)*4; if(cell.data[i+3]){ const d=((r*32+y)*TILE+x)*4;
        buf[d]=cell.data[i];buf[d+1]=cell.data[i+1];buf[d+2]=cell.data[i+2];buf[d+3]=cell.data[i+3]; } } }
    return { data:buf, w:TILE, h:H };
  }

  function renderStack(o){ o=o||{}; const seed=(o.seed|0), size=o.size||'m';
    const W = size==='reef'?18 : size==='s'?12 : size==='m'?16 : 22;
    const H = size==='reef'?9  : size==='s'?19 : size==='m'?28 : 40;
    const buf=new Uint8ClampedArray(W*H*4);
    const put=(x,y,c,a)=>{ if(x<0||x>=W||y<0||y>=H)return; const rgb=Array.isArray(c)?c:hex2rgb(c);
      const i=(y*W+x)*4; buf[i]=rgb[0];buf[i+1]=rgb[1];buf[i+2]=rgb[2];buf[i+3]=(a==null?255:a); };
    const cx=(W-1)/2;
    // pure rock sprite (transparent bg) — the engine's shader draws water + foam behind/around it.
    if(size==='reef'){                                    // low awash skerry: a mound of slab rock
      for(let x=0;x<W;x++){ const top=H-2-Math.round((Math.sin(x*0.55+seed)+1)*2.0+hash(x,0,seed)*1.4);
        for(let y=Math.max(0,top);y<H;y++){ let c=talus(x+3,y+22,seed); if(y===top)c=PAL.redrock[5]; put(x,y,c); } }
      return {data:buf,w:W,h:H};
    }
    const baseY=H-1;
    for(let y=0;y<H;y++){
      const t=y/(H-1);
      let prof=Math.sin(Math.min(1,t*1.18)*Math.PI)*0.34+0.66;  // flat-ish top, fat body
      let hw=(W/2-1)*prof;
      if(y>baseY-3) hw*=0.6;                              // wave-cut undercut notch at the base
      hw+=(hash(0,y>>1,seed+3)-0.5)*2.4;                  // angular stepped silhouette
      hw=Math.max(1.2,hw);
      const L=Math.round(cx-hw), R=Math.round(cx+hw);
      for(let x=L;x<=R;x++){ if(x<0||x>=W)continue;
        let c=strataColor(x+5, y, seed);
        if(y%5===4) c=mix(c,PAL.KEY,0.34);                // horizontal bedding shadow line
        else if(y%5===0) c=mix(c,PAL.redrock[5],0.4);     // sunlit ledge lip under it
        if(x<cx-hw*0.3) c=mix(c,PAL.redrock[6],0.24);     // upper-left key light
        else if(x>cx+hw*0.3) c=mix(c,PAL.KEY,0.34);       // shaded right
        if(x===L||x===R) c=mix(c,PAL.KEY,0.42);           // dark rim
        if(y>baseY-3) c=mix(c,PAL.KEY,0.4);               // damp shaded foot
        put(x,y,c);
      }
    }
    if(size==='l'||size==='m'){ const gw=size==='l'?3:2;  // grass cap tuft on the bigger stacks
      for(let x=Math.round(cx)-gw;x<=Math.round(cx)+gw;x++){ if(hash(x,0,seed+5)<0.8){put(x,0,PAL.grn[3]);put(x,1,PAL.grn[4]);} } }
    return {data:buf,w:W,h:H};
  }

  root.ShoreKit={ TILE, CELL_H, PAL, FACE_H, FLAT:['grass','beach','tidalDry','tidalWet','shallows','ledge'],
    BAND:['cliff','dune'], render, renderBand, renderFlat, renderStack, stack, waterDepth, hash };
})(typeof globalThis!=='undefined'?globalThis:window);
