/* Hidden Harbours — parametric wharf tile kit (square 32×32 near-plan grid, vertical S/diagonal faces).
   Shares the KTC pixel conventions of doryIsoRig / lighthouseIso: no AA, upper-left key light,
   quantised palette ramps (dory wood, oilskin yellow, galvanised steel, concrete, breakwater stone,
   creosote pile), hash-value noise, per-column faces + baked waterline foam.

   Coordinate model (matches Grass/Sand/SeaWater tiles + CottageIso/Lighthouse buildings):
     • Deck is a near-plan 32×32 square (tile top y0..31).
     • The camera looks from the SOUTH, so only S-facing edges show a tall vertical FACE,
       dropping into the water below the footprint. N/E/W open edges get a raised curb/rim only.
     • Every baked cell is 32 wide × CELL_H tall. Deck occupies y 0..31; the face/water area
       is y 32..CELL_H-1. Blit each cell at the tile's screen origin (deck top-left).

   API (browser or run_script via eval, exposes globalThis.WharfKit):
     WharfKit.TILE   = 32
     WharfKit.CELL_H = 56           // 32 deck + 24 face/foam
     WharfKit.MATERIALS            // ['float','lowpier','tallpier','quay']
     WharfKit.render(material, opts) -> {data:Uint8ClampedArray(32*CELL_H*4), w, h}
       opts = {
         open:{n,e,s,w} booleans  — side drops to water (curb; S also gets a face)
         cut: null|'ne'|'se'|'sw'|'nw'  — 45° diagonal deck edge on that corner (with face if S-ish)
         inner: null|'ne'|'se'|'sw'|'nw' — concave inner corner (curb wraps that corner)
         frame: 0..3              — float bob phase (ignored by fixed materials)
       }
*/
(function(root){
  const TILE=32, CELL_H=56;
  // ---- palettes (5-step ramps, dark→light; index 0 = key/outline) ----
  const PAL={
    wood : ['#33271b','#473627','#5e4630','#6b4f35','#8a6a48','#9a7853','#a98352'],
    grey : ['#31342f','#454a44','#5c625a','#747a70','#8a9084','#9ba193'],
    yel  : ['#7a5c22','#b8923c','#e0bd58'],
    conc : ['#453e33','#5f574a','#7d7261','#9c917b','#aca084'],
    stone: ['#252b31','#3a434c','#525a64','#6a727b','#848c94'],
    pile : ['#191308','#2a2014','#3a2c1c','#4a3826'],
    steel: ['#2e3338','#565d63','#98a0a8'],
    KEY  : '#1c140d',
    algae: '#3f4a33', dark:'#0d1a1e', wdark:'#122229', foam:'#d5e6e0', rust:'#7d5c48'
  };
  const FACE_H={ float:6, lowpier:11, tallpier:19, quay:16 };
  function hash(x,y,s){ let h=(x*374761393+y*668265263+((s|0))*1274126177)|0;
    h=Math.imul(h^(h>>>13),1274126177); return ((h^(h>>>16))>>>0)/4294967296; }
  function hex2rgb(h){ return [parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]; }

  function render(material, opts){
    opts=opts||{};
    const open=Object.assign({n:false,e:false,s:false,w:false}, opts.open||{});
    const cut=opts.cut||null, inner=opts.inner||null, frame=(opts.frame|0)%4;
    const fh=FACE_H[material]||12;
    const buf=new Uint8ClampedArray(TILE*CELL_H*4);
    const put=(x,y,hexOrRgb,a)=>{ if(x<0||x>=TILE||y<0||y>=CELL_H)return;
      const rgb=Array.isArray(hexOrRgb)?hexOrRgb:hex2rgb(hexOrRgb);
      const i=(y*TILE+x)*4; buf[i]=rgb[0];buf[i+1]=rgb[1];buf[i+2]=rgb[2];buf[i+3]=(a==null?255:a); };
    // float heave: whole deck rises/falls ±1px on a 4-frame loop; face compresses to match.
    const bob = material==='float' ? [0,-1,0,1][frame] : 0;

    // ---------- deck membership (which pixels are deck for this tile) ----------
    // 45° diagonal cut removes the named corner triangle; inner corner is full square (curb wraps).
    function inDeck(x,y){
      if(x<0||x>=TILE||y<0||y>=TILE)return false;
      if(cut==='se' && x+y > 31+ (0))      return x+y<=31? true:false; // handled below precisely
      return true;
    }
    // precise diagonal masks
    function deckMask(x,y){
      if(x<0||x>=TILE||y<0||y>=TILE)return false;
      switch(cut){
        case 'se': return (x + y) <= 31;
        case 'sw': return ((TILE-1-x) + y) <= 31;
        case 'ne': return (x + (TILE-1-y)) <= 31;
        case 'nw': return ((TILE-1-x)+(TILE-1-y)) <= 31;
        default: return true;
      }
    }
    const edgeOf=(x,y)=>{ // is this deck pixel on the open-water rim? returns which side or 'cut'
      if(!deckMask(x,y))return null;
      if(cut){ // diagonal rim: pixel whose outward diagonal neighbour is off-deck
        if(!deckMask(x+ (cut.includes('e')?1:-1), y+(cut.includes('s')?1:-1)))return 'cut';
      }
      if(open.n && y===0)return 'n';
      if(open.s && y===TILE-1)return 's';
      if(open.w && x===0)return 'w';
      if(open.e && x===TILE-1)return 'e';
      return null;
    };

    // ---------- deck texture per material ----------
    function deckColor(x,y){
      if(material==='quay'){
        let c=PAL.conc[3]; const n=hash(x,y,1);
        if(n<0.05)c=PAL.conc[4]; else if(n<0.12)c=PAL.conc[2];
        if(x%16===0 && y>2 && y<TILE-2)c=PAL.conc[1];       // slab joint
        if(y===TILE/2 && x%3<2)c=PAL.conc[1];
        const e1=((x-9)/9)**2+((y-8)/6)**2;                 // dust patch
        if(e1<1 && hash(x,y,3)<0.22)c=PAL.rust;
        return c;
      }
      if(material==='float'){
        const row=(y/3)|0; let c=PAL.grey[2+((hash(1,row,6)*3)|0)];
        if(y%9===8)c=PAL.grey[1];
        if(hash(x,y,8)<0.05)c=PAL.grey[5];
        return c;
      }
      // wood piers
      const row=(y/3)|0; let c=PAL.wood[2+((hash(0,row,9)*4)|0)];
      if(hash(x,y,4)<0.06)c=PAL.wood[5];
      if(y%9===8)c=PAL.wood[0];
      const plank = row%2 ? 21 : 11;
      if(x===plank)c=PAL.wood[1];
      return c;
    }
    // curb / rim color for an open straight edge (raised lip drawn on deck)
    function curbColor(side, depth){ // depth 0=outer .. 3 inner
      if(material==='float'){ // yellow safety curb
        if(depth===0)return PAL.yel[0];
        if(depth===1)return PAL.yel[2];
        return hash(side.length*7,depth,5)<0.15?PAL.yel[1]:PAL.yel[2];
      }
      if(material==='quay'){ // timber curb
        if(depth===0)return PAL.wood[0];
        if(depth===1)return PAL.wood[3];
        return hash(depth,side.length,4)<0.15?PAL.wood[2]:PAL.wood[3];
      }
      // wood pier: darker plank lip
      return depth===0?PAL.wood[0]:PAL.wood[1];
    }

    // ---------- paint deck ----------
    const CURB= (material==='float'||material==='quay')?3:2; // curb band width
    for(let y=0;y<TILE;y++)for(let x=0;x<TILE;x++){
      if(!deckMask(x,y))continue;
      const yy=y+bob;
      let c=deckColor(x,y);
      // curb band: distance from any open rim
      let cd=99, cside=null;
      const rim=edgeOf(x,y);
      if(open.n){ if(y<CURB){ if(y<cd){cd=y;cside='n';} } }
      if(open.s){ if(TILE-1-y<CURB){ if(TILE-1-y<cd){cd=TILE-1-y;cside='s';} } }
      if(open.w){ if(x<CURB){ if(x<cd){cd=x;cside='w';} } }
      if(open.e){ if(TILE-1-x<CURB){ if(TILE-1-x<cd){cd=TILE-1-x;cside='e';} } }
      if(cut){ // diagonal curb band along the cut
        const dd = cut==='se'? (31-(x+y)) : cut==='sw'? (31-((TILE-1-x)+y))
                 : cut==='ne'? (31-(x+(TILE-1-y))) : (31-((TILE-1-x)+(TILE-1-y)));
        if(dd<CURB && dd<cd){ cd=dd; cside='cut'; }
      }
      if(cd<CURB) c=curbColor(cside||'n', cd);
      put(x,yy,c,255);
      // fill the bob gap so no transparent seam at top when deck rises
      if(bob<0 && y===0) put(x,0,c,255);
    }
    if(bob>0){ // deck dropped: paint the exposed top row with deck texture
      for(let x=0;x<TILE;x++) if(deckMask(x,0)) put(x,0,deckColor(x,0),255);
    }

    // ---------- south face (+ diagonal-cut face) ----------
    function faceCol(k){
      if(material==='quay'){
        if(k===0)return '#241d15';
        if(k<fh-4)return PAL.conc[1];
        if(k<fh-2)return PAL.algae;
        return PAL.dark;
      }
      if(material==='float'){
        if(k<2)return '#2a2e29';
        return '#101c1f';
      }
      // wood pier face
      if(k===0)return '#241d15';
      if(k<4)return PAL.wood[1];
      if(k<fh-3)return PAL.wood[0];
      return PAL.dark;
    }
    // straight south face
    if(open.s){
      for(let x=0;x<TILE;x++){
        if(!deckMask(x,TILE-1))continue;
        const top=TILE+bob;
        for(let k=0;k<fh;k++) put(x, top+k, faceCol(k), 255);
        // stain streaks / plank seams
        if(material==='quay' && x%16===0) for(let k=1;k<fh-2;k++) put(x,top+k,'#3a342b',255);
        if((material==='tallpier'||material==='lowpier') && (x===11||x===21)) for(let k=1;k<fh;k++) put(x,top+k,PAL.wood[0],255);
        // waterline foam
        if(hash(x,0,13)<0.55) put(x, top+fh, PAL.foam,255);
        if(hash(x,0,14)<0.20) put(x, top+fh+1, PAL.foam,255);
      }
      // tall pier: piles + under-deck shadow + reflections
      if(material==='tallpier'){
        const top=TILE+bob;
        for(let x=0;x<TILE;x++) if(deckMask(x,TILE-1)) for(let k=2;k<fh;k++) if(faceCol(k)===PAL.dark) put(x,top+k,PAL.dark,255);
        for(const px of [6,16,26]){
          for(let k=4;k<fh+4;k++){ put(px-1,top+k,PAL.pile[2]); put(px,top+k,PAL.pile[1]); put(px+1,top+k,PAL.pile[0]); }
          for(let k=fh+4;k<fh+9;k++){ const wob=Math.round((hash(0,k,22)-0.5)*2); put(px+wob,top+k,PAL.wdark); }
          put(px,top+fh+3,PAL.foam);
        }
      }
      if(material==='lowpier'){ // slim piles
        const top=TILE+bob;
        for(const px of [8,24]){ for(let k=3;k<fh+2;k++){ put(px,top+k,PAL.pile[1]); put(px+1,top+k,PAL.pile[0]); } }
      }
    }
    // diagonal cut face (staircase along the 45° edge, S-facing cuts only get full face)
    if(cut && (cut==='se'||cut==='sw')){
      for(let x=0;x<TILE;x++)for(let y=0;y<TILE;y++){
        if(deckMask(x,y) && edgeOf(x,y)==='cut'){
          const top=y+1+bob;
          const ffh=Math.max(3, fh-((cut==='se'?(TILE-1-x):x)>>2)); // shorten toward the point a touch
          for(let k=0;k<ffh;k++) put(x, top+k, faceCol(k),255);
          if(hash(x,y,13)<0.5) put(x, top+ffh, PAL.foam,255);
        }
      }
    }
    return { data:buf, w:TILE, h:CELL_H };
  }

  root.WharfKit={ TILE, CELL_H, PAL, FACE_H, MATERIALS:['float','lowpier','tallpier','quay'], render, hash };
})(typeof globalThis!=='undefined'?globalThis:window);
