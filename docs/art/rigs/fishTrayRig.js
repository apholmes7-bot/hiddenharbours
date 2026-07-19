/* Hidden Harbours — parametric DECK FISH TRAY (fill states). Deck-dressing prop.
   A shallow plastic fish tote sitting on the starboard quarter. The sprite NEVER
   rotates — it is drawn screen-upright regardless of the boat's heading — so it is
   one ¾ top-down view that reads sitting on a deck seen from any angle.

   Canvas 32×24. EVERY state renders on the identical canvas with the tray anchored
   to the same pixels (bottom-centre pivot 16,22); the game swaps states in place, so
   one pixel of drift would visibly jump. Fill is monotonic: each state reveals the
   same ordered pile of keepers, so the shell + earlier lobsters never move.

   Faded-blue plastic (reads against BOTH the white hull and the grey deck). Keepers
   are banded live lobsters — dark bluish-green shell, a warm band pip on the claws —
   with the odd rust crab in the mix. 32 px = 1 m → tray ≈ 0.9 m. No AA, binary alpha,
   1px #131b1e keyline. Player should read the fill in a half-second glance.

   States (index → tag): 0 empty · 1 few · 2 half · 3 full · 4 brimming.

   Exposes globalThis.FishTray with:
     W, H, PAL, STATES, STATE_COUNT, pivot,
     render(i) / renderState(name) -> Uint8ClampedArray(W*H*4)
   Works in the run_script sandbox (bake) and in the browser (live preview). */
(function (root) {
  const W = 32, H = 24, PX = 16, PY = 22;
  const STATES = ['empty', 'few', 'half', 'full', 'brim'];

  // ---- palette --------------------------------------------------------------
  const HEX = {
    key:'#131b1e',
    // faded-blue plastic tote
    wal:'#5f7d8a', walHi:'#86a3ad', walSh:'#476069', walDp:'#33474f',
    // interior floor (same plastic, in the tray's own shadow — darker/bluer)
    ins:'#3c525d', insHi:'#526a75', insSh:'#2b3c44', insDp:'#1d2a30',
    she:'#c2d6da', sheHi:'#e2eff1',                         // wet sheen
    // live lobster keeper — dark bluish-green
    sh:'#345a4e', shHi:'#517d6c', shSh:'#213f37', shDp:'#152a25',
    // rust rock crab (colour variety in the pile)
    cr:'#a24b2f', crHi:'#c3623c', crSh:'#6f321d', crDp:'#481f12',
    crm:'#e7d8b0',                                          // cream claw tip
    bnd:'#c85a3f', bndHi:'#e07a5a',                         // claw band
    sepL:'#12241e', sepLd:'#0b1712',                        // lobster separation rim
    sepC:'#3a1a0d', sepCd:'#280f06',                        // crab separation rim
  };
  const MAT = {
    WALL:{ mid:'wal', hi:'walHi', sh:'walSh', dp:'walDp' },
    INS: { mid:'ins', hi:'insHi', sh:'insSh', dp:'insDp' },
    SHEEN:{ mid:'she', hi:'sheHi', sh:'she', dp:'she' },
    SHELL:{ mid:'sh', hi:'shHi', sh:'shSh', dp:'shDp' },
    CRAB:{ mid:'cr', hi:'crHi', sh:'crSh', dp:'crDp' },
    CREAM:{ mid:'crm', hi:'crm', sh:'crm', dp:'crm' },
    BAND:{ mid:'bnd', hi:'bndHi', sh:'bnd', dp:'bnd' },
    SEPL:{ mid:'sepL', hi:'sepL', sh:'sepLd', dp:'sepLd' },   // flat dark — never lightens
    SEPC:{ mid:'sepC', hi:'sepC', sh:'sepCd', dp:'sepCd' },
  };

  // ---- buffer + primitive kit ----------------------------------------------
  function newBuf(){ return { key:new Array(W*H).fill(''), mat:new Array(W*H).fill(null) }; }
  const idx=(x,y)=>y*W+x, inb=(x,y)=>x>=0&&x<W&&y>=0&&y<H;
  function put(b,x,y,m){ x=Math.round(x); y=Math.round(y); if(!inb(x,y))return; b.key[idx(x,y)]='mid'; b.mat[idx(x,y)]=m; }
  function putK(b,x,y,m,k){ x=Math.round(x); y=Math.round(y); if(!inb(x,y))return; b.key[idx(x,y)]=k; b.mat[idx(x,y)]=m; }
  function ellipse(b,cx,cy,rx,ry,m,k){
    for(let y=Math.floor(cy-ry);y<=Math.ceil(cy+ry);y++)for(let x=Math.floor(cx-rx);x<=Math.ceil(cx+rx);x++){
      const dx=(x-cx)/(rx+0.001),dy=(y-cy)/(ry+0.001); if(dx*dx+dy*dy<=1){ k?putK(b,x,y,m,k):put(b,x,y,m); } }
  }
  function taper(b,x0,y0,x1,y1,r0,r1,m){
    const minx=Math.floor(Math.min(x0,x1)-Math.max(r0,r1)), maxx=Math.ceil(Math.max(x0,x1)+Math.max(r0,r1));
    const miny=Math.floor(Math.min(y0,y1)-Math.max(r0,r1)), maxy=Math.ceil(Math.max(y0,y1)+Math.max(r0,r1));
    const dx=x1-x0, dy=y1-y0, L2=dx*dx+dy*dy||1;
    for(let y=miny;y<=maxy;y++)for(let x=minx;x<=maxx;x++){
      let t=((x-x0)*dx+(y-y0)*dy)/L2; t=Math.max(0,Math.min(1,t));
      const px=x0+dx*t, py=y0+dy*t; if(Math.hypot(x-px,y-py)<=r0+(r1-r0)*t) put(b,x,y,m); }
  }
  function inRound(x,y,x0,y0,x1,y1,r){
    if(x<x0||x>x1||y<y0||y>y1) return false;
    const cx = x<x0+r ? x0+r : (x>x1-r ? x1-r : x);
    const cy = y<y0+r ? y0+r : (y>y1-r ? y1-r : y);
    return (x-cx)*(x-cx)+(y-cy)*(y-cy) <= r*r+0.6;
  }
  function roundRect(b,x0,y0,x1,y1,r,m){
    for(let y=y0;y<=y1;y++)for(let x=x0;x<=x1;x++) if(inRound(x,y,x0,y0,x1,y1,r)) put(b,x,y,m);
  }

  // ---- tray shell (identical every state) -----------------------------------
  // top opening outer rounded-rect [2,3]-[29,15] r3 ; wall depth 4 (front/side faces).
  const OX0=2, OY0=3, OX1=29, OY1=15, OR=3, DEPTH=4;
  const IX0=4, IY0=5, IX1=27, IY1=13, IR=2;                 // interior opening (floor)
  function drawTray(b){
    for(let d=DEPTH; d>=1; d--) roundRect(b, OX0, OY0+d, OX1, OY1+d, OR, 'WALL');   // extruded body → front/side walls
    roundRect(b, OX0, OY0, OX1, OY1, OR, 'WALL');            // top rim (rounded frame)
    // interior floor
    for(let y=IY0;y<=IY1;y++)for(let x=IX0;x<=IX1;x++) if(inRound(x,y,IX0,IY0,IX1,IY1,IR)) putK(b,x,y,'INS','mid');
    // front & lower-side wall faces sit in shadow (darker than the top rim)
    for(let y=0;y<H;y++)for(let x=0;x<W;x++){ const i=idx(x,y); if(b.mat[i]==='WALL' && y>=OY1) b.key[i]='sh'; }
    // moulded end-handle notches on the left/right rim
    for(const hx of [3,4,27,28]){ const i=idx(hx,9); if(b.mat[i]==='WALL') b.key[i]='dp'; }
    // wet sheen — pale diagonal glint on the floor, upper-left
    for(const [x,y] of [[7,6],[8,6],[9,7],[10,7],[11,8]]){ const i=idx(x,y); if(b.mat[i]==='INS') putK(b,x,y,'SHEEN','hi'); }
    putK(b,7,7,'SHEEN','mid');
  }

  // ---- one keeper (tiny lobster) or crab ------------------------------------
  // Each keeper draws a dark SEPARATION silhouette first (enlarged coarse shape); the body
  // paints on top leaving a ~1px dark rim, so back-to-front stacking reads as distinct animals
  // instead of one merged green mass.
  function drawKeeper(b, cx, cy, ang, sc, kind){
    const ax=Math.cos(ang), ay=Math.sin(ang), nx=-ay, ny=ax;
    if(kind==='crab'){
      ellipse(b, cx+0.3, cy+0.7, 3.9*sc, 3.0*sc, 'SEPC');                     // sep shadow
      for(const s of [-1,1]){ const clx=cx+ax*2.8*sc+nx*s*1.8*sc, cly=cy+ay*2.8*sc+ny*s*1.8*sc; ellipse(b, clx, cly, 1.8*sc, 1.5*sc, 'SEPC'); }
      ellipse(b, cx, cy, 3.2*sc, 2.4*sc, 'CRAB');                             // carapace
      for(const s of [-1,1]){                                                 // legs
        for(const d of [2.0,3.0]) putK(b, cx-ax*0.6*sc+nx*s*d*sc, cy-ay*0.6*sc+ny*s*d*sc, 'CRAB','sh');
        const clx=cx+ax*2.8*sc+nx*s*1.8*sc, cly=cy+ay*2.8*sc+ny*s*1.8*sc;     // claws forward
        ellipse(b, clx, cly, 1.2*sc, 1.0*sc, 'CRAB'); putK(b, Math.round(clx), Math.round(cly), 'CREAM','hi');
      }
      putK(b, Math.round(cx-nx*1.1*sc), Math.round(cy-ny*1.1*sc), 'CRAB','hi');   // shell glint
      putK(b, Math.round(cx+ax*1.1*sc), Math.round(cy+ay*1.1*sc), 'CRAB','sh');    // rear seam
      return;
    }
    // lobster: tail (−axis) → carapace (+axis)
    const hx=cx+ax*2.4*sc, hy=cy+ay*2.4*sc, tx=cx-ax*3.2*sc, ty=cy-ay*3.2*sc;
    taper(b, tx,ty, hx,hy, 1.8*sc, 2.8*sc, 'SEPL');                           // sep shadow: body
    ellipse(b, hx, hy, 2.9*sc, 2.4*sc, 'SEPL');
    for(const s of [-1,1]){                                                   // sep shadow: claws
      const arx=hx+ax*1.6*sc+nx*s*1.2*sc, ary=hy+ay*1.6*sc+ny*s*1.2*sc;
      const clx=hx+ax*3.0*sc+nx*s*1.7*sc, cly=hy+ay*3.0*sc+ny*s*1.7*sc;
      taper(b, arx,ary, clx,cly, 1.6*sc, 1.9*sc, 'SEPL');
    }
    taper(b, tx,ty, hx,hy, 1.1*sc, 2.1*sc, 'SHELL');                          // abdomen
    ellipse(b, hx, hy, 2.2*sc, 1.8*sc, 'SHELL');                              // carapace
    for(const s of [-1,0,1]) putK(b, Math.round(tx-ax*0.6+nx*s*1.3*sc), Math.round(ty-ay*0.6+ny*s*1.3*sc), 'SHELL','sh');  // tail fan
    for(const seg of [1.0,1.9]) putK(b, Math.round(cx-ax*seg*sc), Math.round(cy-ay*seg*sc), 'SHELL','sh');   // abdomen segment seams
    for(const s of [-1,1]){
      const arx=hx+ax*1.6*sc+nx*s*1.2*sc, ary=hy+ay*1.6*sc+ny*s*1.2*sc;
      const clx=hx+ax*3.0*sc+nx*s*1.7*sc, cly=hy+ay*3.0*sc+ny*s*1.7*sc;
      taper(b, arx,ary, clx,cly, 1.0*sc, 1.3*sc, 'SHELL');                    // claw arm
      ellipse(b, clx, cly, 1.3*sc, 1.1*sc, 'SHELL'); putK(b, Math.round(clx), Math.round(cly), 'SHELL','hi');
      putK(b, Math.round(arx), Math.round(ary), 'BAND','mid');                // band pip
    }
    putK(b, Math.round(hx-nx*0.9*sc), Math.round(hy-ny*0.9*sc), 'SHELL','hi');    // carapace glint
    putK(b, Math.round(hx+ax*0.6*sc+nx*0.9*sc), Math.round(hy+ay*0.6*sc+ny*0.9*sc), 'SHELL','dp');   // eye
    putK(b, Math.round(hx+ax*0.6*sc-nx*0.9*sc), Math.round(hy+ay*0.6*sc-ny*0.9*sc), 'SHELL','dp');
  }

  // pile of keepers, ordered; minState = first fill state it appears in.
  // drawn back-to-front (y ascending) so the ¾ stack occludes correctly.
  const PILE = [
    {x:10,y:11,a:0.2, sc:1.00,k:'lob', s:1},
    {x:17,y:12,a:2.7, sc:1.00,k:'lob', s:1},
    {x:22,y:10,a:1.4, sc:0.95,k:'lob', s:1},
    {x:8, y:9, a:3.6, sc:0.95,k:'lob', s:2},
    {x:14,y:10,a:5.4, sc:1.00,k:'crab',s:2},
    {x:20,y:12,a:0.9, sc:1.00,k:'lob', s:2},
    {x:25,y:8, a:2.2, sc:0.90,k:'lob', s:2},
    {x:7, y:7, a:0.5, sc:0.95,k:'lob', s:3},
    {x:12,y:8, a:4.2, sc:1.00,k:'lob', s:3},
    {x:17,y:7, a:1.9, sc:1.00,k:'lob', s:3},
    {x:22,y:7, a:3.3, sc:0.95,k:'crab',s:3},
    {x:26,y:11,a:5.0, sc:0.90,k:'lob', s:3},
    {x:14,y:3, a:1.4, sc:1.00,k:'lob', s:4},    // heaped highest — breaks the top rim silhouette
    {x:20,y:4, a:2.4, sc:0.95,k:'lob', s:4},    // second peak
    {x:10,y:16,a:0.3, sc:0.95,k:'lob', s:4},    // cresting over the front lip
    {x:23,y:16,a:2.9, sc:0.95,k:'lob', s:4},
  ];
  function drawPile(b, state, ct){
    const shown = PILE.filter(p=>state>=p.s).slice().sort((a,c)=>a.y-c.y);
    for(const p of shown){
      const k = ct==='lobster' ? 'lob' : ct==='crab' ? 'crab' : p.k;   // mixed keeps per-item kind
      drawKeeper(b, p.x, p.y, p.a, p.sc, k);
    }
  }

  // ---- shade / outline / colourise (upper-left key) -------------------------
  function shade(b){
    const src=b.key.slice(), mat=b.mat;
    for(let y=0;y<H;y++)for(let x=0;x<W;x++){
      const i=idx(x,y); if(src[i]!=='mid') continue; const m=mat[i];
      const up=y>0&&src[idx(x,y-1)]&&mat[idx(x,y-1)]===m, lf=x>0&&src[idx(x-1,y)]&&mat[idx(x-1,y)]===m;
      const dn=y<H-1&&src[idx(x,y+1)]&&mat[idx(x,y+1)]===m, rt=x<W-1&&src[idx(x+1,y)]&&mat[idx(x+1,y)]===m;
      if(!up||!lf) b.key[i]='hi'; else if(!dn||!rt) b.key[i]='sh';
    }
  }
  function outline(b){
    const add=[];
    for(let y=0;y<H;y++)for(let x=0;x<W;x++){ if(b.key[idx(x,y)])continue;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]) if(inb(x+dx,y+dy)&&b.key[idx(x+dx,y+dy)]&&b.mat[idx(x+dx,y+dy)]!=='__out'){ add.push([x,y]); break; } }
    for(const [x,y] of add){ b.key[idx(x,y)]='out'; b.mat[idx(x,y)]='__out'; }
  }
  function hex2rgb(h){ return [parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]; }
  function colourOf(m,k){ if(m==='__out'||k==='out') return HEX.key; const mm=MAT[m]; if(!mm)return HEX.key;
    const nm=k==='hi'?mm.hi:k==='sh'?mm.sh:k==='dp'?(mm.dp||mm.sh):mm.mid; return HEX[nm]; }
  function toRGBA(b){
    const out=new Uint8ClampedArray(W*H*4);
    for(let i=0;i<W*H;i++){ const k=b.key[i]; if(!k){ out[i*4+3]=0; continue; }
      const [r,g,bl]=hex2rgb(colourOf(b.mat[i],k)); out[i*4]=r; out[i*4+1]=g; out[i*4+2]=bl; out[i*4+3]=255; }
    return out;
  }

  function renderIndex(i, ct){
    const b=newBuf();
    drawTray(b);
    drawPile(b, i, ct||'mixed');
    shade(b); outline(b);
    return toRGBA(b);
  }
  function renderState(name, ct){ return renderIndex(Math.max(0, STATES.indexOf(name)), ct); }

  const CATCHES = ['lobster','crab','mixed'];
  root.FishTray = { W, H, PAL:HEX, STATES, STATE_COUNT:STATES.length, CATCHES, pivot:{x:PX,y:PY},
    render:renderIndex, renderState };
})(typeof globalThis!=='undefined'?globalThis:window);
