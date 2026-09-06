/* Hidden Harbours — SHELLFISH rig, PASS 2. shellfishRig.js is untouched.
   STRICT WORLD SCALE (owner's call, catch pass 2): 32 px = 1 m, so a blue mussel is 2x1 px, a
   soft-shell clam 2x2, a scallop or oyster 3x3, a periwinkle ONE pixel. Nothing is inflated to
   read alone — THE BEDS CARRY IT. A shellfish is texture; what the player sees in the world is
   a mussel bed on a tide-pool rock, an oyster clump on the mud, scallops on the sand, and a clam
   flat that spurts. Ringless (ADR 0031). Top-down 3/4 read (y x0.64 = sin 40deg), the same
   upper-left key as every other rig. Tones: the pass-1 mussel/clam ramps, shorelineRig's
   SCALLOP and PERI, yardIsoRig's crushed-oyster SHELL — nothing invented.
   ITEM   renderItem(kind,variant,scale) -> 8x8 cell, pivot (4,6) = ground contact — the odd loose
          shell on a deck. Container fills use heap().
   HAND   renderHandful(kind,variant) -> 8x8, pivot (4,4) = THE GRIP, a clutch (one per hand).
   BED    bed(kind,{w,h,seed,cover}) -> {w,h,rgba} alpha-masked texture to lay over rock / sand /
          mud: mussel · periwinkle · oyster · scallop. (A clam flat shows no clams: see FLAT.)
   FLAT   holes(seed,n,w,h) -> the keyholes a clam flat shows; spurt(hole,t) -> null | {rise 0..1,
          u} — the siphon spurt, the gameplay TELL for the dig. Draw a 1 px jet, rise x 4 px.
   HEAP   heap(kind,w,h,seed,mask,{dome}) -> rgba of heaped shells clipped to mask(x,y): THE
          container fill for shellfish at this scale (pail / hod / tray / tote openings).
   Exposes globalThis.Shellfish2 = { KINDS,SIZES,PAL,TINY,IW,IH,ipivot,hpivot,VARIANTS,
   renderItem,renderHandful,bed,holes,spurt,heap,mulberry }. */
(function (root) {
  const KINDS=['mussel','clam','scallop','oyster','periwinkle'], VARIANTS=4;
  const IW=8, IH=8, SQ=0.64;
  const SIZES={
    mussel:    { len:0.06,  wid:0.03, mass:0.012, label:'BLUE MUSSEL' },
    clam:      { len:0.07,  wid:0.05, mass:0.030, label:'SOFT-SHELL CLAM' },
    scallop:   { len:0.10,  wid:0.09, mass:0.080, label:'SEA SCALLOP' },
    oyster:    { len:0.10,  wid:0.07, mass:0.090, label:'OYSTER' },
    periwinkle:{ len:0.025, wid:0.02, mass:0.004, label:'PERIWINKLE' },
  };
  const PAL={
    mussel:    ['#10151a','#1b2430','#293747','#3d5064','#c2d6da'],
    clam:      ['#8a6a48','#9c7f57','#c9b083','#e7d8b0','#f3ead0'],
    scallop:   ['#7a4a34','#a8694a','#cf8f6a','#e2b98f','#f1d8b8'],
    oyster:    ['#4d4a42','#6b675c','#8f8a7c','#b1ab99','#dcd6c3'],
    periwinkle:['#2a2219','#4a3c2c','#7a6a4a','#9a8a66','#b8a985'],
  };
  const RGB={}; for (const k in PAL) RGB[k]=PAL[k].map(h=>[parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]);
  // the 1-2 px kinds are explicit pixel patterns (an ellipse test cannot honestly place 2 pixels)
  const TINY={
    mussel:     [ [[0,0,3],[1,0,2]], [[0,0,3],[0,1,1]], [[0,0,3],[1,1,1]], [[1,0,3],[0,1,2]] ],
    periwinkle: [ [[0,0,2]], [[0,0,3]], [[0,0,2]], [[0,0,1]] ],
    clam:       [ [[0,0,3],[1,0,2],[0,1,2],[1,1,1]], [[0,0,3],[1,0,3],[0,1,1],[1,1,1]], [[0,0,2],[1,0,3],[0,1,1],[1,1,2]], [[0,0,3],[1,0,2],[1,1,1]] ],
  };
  function mulberry(seed){ let a=seed>>>0; return function(){ a|=0; a=(a+0x6D2B79F5)|0;
    let t=Math.imul(a^(a>>>15),1|a); t=(t+Math.imul(t^(t>>>7),61|t))^t; return ((t^(t>>>14))>>>0)/4294967296; }; }
  function Buf(w,h){ this.w=w; this.h=h; this.k=new Int8Array(w*h).fill(-1); this.kind=new Array(w*h).fill(null); }
  // an ellipse-stamped shell (scallop / oyster, 3 px class), lit from the upper-left
  function stamp(b, kind, cx, cy, ang, ss){
    const Z=SIZES[kind], rx=Z.len*16*ss+0.3, ry=Z.wid*16*ss*SQ+0.3;
    const ca=Math.cos(ang), sa=Math.sin(ang), R=Math.ceil(Math.max(rx,ry))+1;
    for (let y=Math.floor(cy-R); y<=Math.ceil(cy+R); y++) for (let x=Math.floor(cx-R); x<=Math.ceil(cx+R); x++){
      if (x<0||y<0||x>=b.w||y>=b.h) continue;
      const dx=x+0.5-cx, dy=y+0.5-cy, u=(dx*ca+dy*sa)/rx, v=(-dx*sa+dy*ca)/ry, q=u*u+v*v;
      if (q>1) continue;
      const L=-(dx*0.55+dy*0.75)/Math.max(rx,ry) - q*0.35;
      b.k[y*b.w+x]= L>0.22?3 : L>-0.22?2 : 1; b.kind[y*b.w+x]=kind;
    }
    const set=(x,y,k)=>{ x=Math.floor(x); y=Math.floor(y); if (x<0||y<0||x>=b.w||y>=b.h) return; const j=y*b.w+x; if (b.kind[j]===kind) b.k[j]=k; };
    if (kind==='scallop'){ set(cx, cy-0.6, 4); set(cx-1.2, cy+0.8, 1); set(cx+1.2, cy+0.8, 1); }   // rib + ears
    if (kind==='oyster'){ set(cx+1.0, cy+0.6, 1); set(cx-0.9, cy-0.5, 4); }                        // rough lip + nacre
  }
  function place(b, kind, x, y, v){
    if (TINY[kind]){ const px=Math.floor(x), py=Math.floor(y);
      for (const [dx,dy,k] of TINY[kind][v%4]){ const X=px+dx, Y=py+dy; if (X<0||Y<0||X>=b.w||Y>=b.h) continue; b.k[Y*b.w+X]=k; b.kind[Y*b.w+X]=kind; } }
    else stamp(b, kind, Math.floor(x)+0.5, Math.floor(y)+0.5, [0,0.7,-0.5,1.2][v%4], 1);
  }
  function toRGBA(b){
    const out=new Uint8ClampedArray(b.w*b.h*4);
    for (let i=0;i<b.w*b.h;i++){ const k=b.k[i]; if (k<0){ out[i*4+3]=0; continue; }
      const c=RGB[b.kind[i]][k]; out[i*4]=c[0]; out[i*4+1]=c[1]; out[i*4+2]=c[2]; out[i*4+3]=255; }
    return out;
  }
  function renderItem(kind, variant, scale){
    kind=KINDS.indexOf(kind)>=0?kind:'mussel'; const b=new Buf(IW,IH), v=(variant||0)%VARIANTS;
    if (TINY[kind]) place(b, kind, 3, 5, v); else stamp(b, kind, 4, 5.5, [0,0.7,-0.5,1.2][v], scale||1);
    return toRGBA(b);
  }
  function renderHandful(kind, variant){
    kind=KINDS.indexOf(kind)>=0?kind:'mussel'; const b=new Buf(IW,IH), v=(variant||0)%2;
    const spots = TINY[kind] ? [[2,3],[4,3],[3,5],[5,5],[3,2]] : [[2,3],[5,3],[3,5]];
    spots.forEach(([x,y],i)=>place(b, kind, x+(v?1:0), y, (i+v)%4));
    return toRGBA(b);
  }
  // a seeded bed: coverage blobs so the rock/sand shows between, then shells until dense
  function bed(kind, o){
    o=o||{}; kind=KINDS.indexOf(kind)>=0?kind:'mussel';
    const w=o.w||48, h=o.h||32, cover=o.cover!=null?o.cover:0.85, rng=mulberry((o.seed||5)*7919);
    const b=new Buf(w,h), blobs=[], nb=Math.max(1, Math.round(2+cover*3));
    for (let i=0;i<nb;i++) blobs.push({ x:rng()*w, y:rng()*h, rx:w*(0.22+rng()*0.35)*cover, ry:h*(0.30+rng()*0.45)*cover });
    const inside=(x,y)=>blobs.some(B=>{ const dx=(x-B.x)/B.rx, dy=(y-B.y)/B.ry; return dx*dx+dy*dy<=1; });
    const dens={ mussel:1.6, periwinkle:0.14, oyster:0.45, scallop:0.05, clam:0 }[kind];
    const n=Math.round(w*h*dens);
    for (let i=0;i<n;i++){ const x=rng()*w, y=rng()*h; if (inside(x,y)) place(b, kind, x, y, Math.floor(rng()*4)); }
    if (kind==='mussel') for (let i=0;i<n*0.035;i++){ const x=Math.floor(rng()*w), y=Math.floor(rng()*h), j=y*w+x; if (b.kind[j]==='mussel') b.k[j]=4; }
    return { w, h, rgba:toRGBA(b) };
  }
  function holes(seed, n, w, h){
    const rng=mulberry((seed||3)*104729), out=[];
    for (let i=0;i<n;i++) out.push({ x:Math.floor(rng()*w), y:Math.floor(rng()*h), phase:rng()*9000, period:2600+rng()*5200, dur:420 });
    return out;
  }
  function spurt(hole, t){ const l=((t+hole.phase)%hole.period); if (l>hole.dur) return null; const u=l/hole.dur; return { rise:Math.sin(Math.PI*u), u }; }
  // heaped shells clipped to mask(x,y) (px, cell coords); dome(x,y) -> -1..1 lifts/darkens a step
  function heap(kind, w, h, seed, mask, o){
    o=o||{}; kind=KINDS.indexOf(kind)>=0?kind:'mussel';
    const b=new Buf(w,h), rng=mulberry((seed||9)*1299709), per=TINY[kind]?1.5:0.6, n=Math.round(w*h*per);
    for (let i=0;i<n;i++){ const x=rng()*w, y=rng()*h; if (mask && !mask(x,y)) continue; place(b, kind, x, y, Math.floor(rng()*4)); }
    for (let y=0;y<h;y++) for (let x=0;x<w;x++){ const j=y*w+x;
      if (mask && !mask(x+0.5,y+0.5)){ b.k[j]=-1; b.kind[j]=null; continue; }
      if (b.k[j]<0){ if (mask){ b.k[j]=1; b.kind[j]=kind; } else continue; }
      let k=b.k[j]; const dl=o.dome?o.dome(x,y):0;
      if (k<4) k=Math.max(0,Math.min(3, k+(dl>0.35?1 : dl<-0.35?-1 : 0)));
      if (k<4 && rng()<0.07) k=0;
      b.k[j]=k; }
    return toRGBA(b);
  }
  root.Shellfish2 = { KINDS, SIZES, PAL, TINY, IW, IH, ipivot:{x:4,y:6}, hpivot:{x:4,y:4}, VARIANTS,
    renderItem, renderHandful, bed, holes, spurt, heap, mulberry };
})(typeof globalThis!=='undefined'?globalThis:window);
