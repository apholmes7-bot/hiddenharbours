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
   PLATE  renderPlate('clam',scale) -> PW x PH (160x96) at 32 px = 1 m x scale, the clam centred with
          its siphon: for a page that draws it larger than the world. Any other kind -> null.
   Exposes globalThis.Shellfish2 = { KINDS,SIZES,PAL,TINY,IW,IH,ipivot,hpivot,VARIANTS,
   renderItem,renderHandful,bed,holes,spurt,heap,mulberry,PW,PH,PLATE_SIPHON,MARKS,renderPlate }.
   CATCH REVIEW PASS B (2026-10-27; the owner's ruling of 10-27, "make them read, scale kept"): the
   beds read as shellfish, not gravel. Items, handfuls and heaps are untouched. bed() keeps its
   blobs and its call; their edges go ragged, and each shell is laid whole, back to front, casting
   a 1 px shadow down-right (black at SHADOW_A) on the ground under it. Mussels pack without
   overlapping, the dark between them the ramp's darkest step, a sheen on some lit ends; oysters
   heap in clumps of 4-9; scallops lie apart, each alone; periwinkles gather in clusters of 3-7.
   CATCH REVIEW PASS D (2026-10-28; the owner's ruling 2 of 10-28, a plate-size clam drawing in
   Shellfish2, which the plate draws): renderPlate, the clam side-on with its siphon, which the field
   guide plate asks for at its own page scale. The shell is SIZES.clam's 7 x 5 cm; the siphon is the
   lane's drawing, out 3 cm, 1.4 cm thick at the gape and 1.1 at its tip. Only the clam has a def, so
   only the clam has a plate. Items, handfuls, beds, flats and heaps are untouched.
   CATCH REVIEW PASS E (2026-10-28; the owner's ruling of 10-28, "Bigger, with field marks"): the plate
   fills the field guide's frame, so its cell is 160 x 96 (64 x 64 in pass D), and it carries the marks
   a field guide shows (MARKS): the growth lines close, crowding to the margin, two of them checks a
   step darker (PLATE_RINGS, kept 3 px apart at the drawing's size); the siphon ringed, its tip dark
   over its last twentieth. Items, handfuls, beds, flats and heaps are untouched. */
(function (root) {
  const KINDS=['mussel','clam','scallop','oyster','periwinkle'], VARIANTS=4;
  const IW=8, IH=8, SQ=0.64, SHADOW_A=96;
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
  function toRGBA(b, shade){
    const out=new Uint8ClampedArray(b.w*b.h*4);
    for (let i=0;i<b.w*b.h;i++){ const k=b.k[i]; if (k<0){ out[i*4+3]=shade&&shade[i]?SHADOW_A:0; continue; }
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
  // THE PLATE (catch review pass D): the clam drawn for a page, which draws it larger than the world.
  // Side-on: the left valve, beak up, the siphon out of the gape to the right. The shell is SIZES.clam's
  // len x wid, fuller fore than aft; lit from the upper-left down the clam's ramp with the fleet's 4x4
  // dither; a growth line is the margin scaled about the beak. The siphon (PLATE_SIPHON) sits behind
  // the shell, tapers, is lit from above, wrinkles every ring and darkens at its tip. Ringless.
  const PW=160, PH=96, PLATE_SIPHON={ out:0.030, base:0.014, tip:0.011, ring:0.010 };
  const PLATE_RINGS=[0.30,0.41,0.50,0.58,0.655,0.72,0.78,0.835,0.885,0.93,0.965], PLATE_CHECKS=[0.58,0.835];   // pass E
  const MARKS={ clam:['the growth lines close, crowding to the margin, two checks darker', 'the siphon out of the gape, ringed, dark at its tip', 'the beak fore of centre'] };
  const BAYER=[[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));   // isoSolid.js'
  function renderPlate(kind, scale){
    if (kind!=='clam') return null;
    const k=32*(scale||1), Z=SIZES.clam, sp=PLATE_SIPHON, AFT=0.28, BEAK=-0.1;
    const rx=Z.len/2*k, ry=Z.wid/2*k, cx=PW/2-sp.out*k/2, cy=PH/2;
    const RINGS=[]; for (const R of PLATE_RINGS) if (!RINGS.length || (R-RINGS[RINGS.length-1])*2*ry>=3) RINGS.push(R);
    const nv=(x,y)=>{ const u=(x-cx)/rx; return [u, (y-cy)/(ry*(u>0?1-AFT*u*u:1))]; };
    const inShell=(x,y)=>{ const [u,v]=nv(x,y); return u*u+v*v<=1; };
    const bx=cx+BEAK*rx, by=cy-ry*Math.sqrt(1-BEAK*BEAK);
    const gauge=(x,y)=>{ let lo=0, hi=1; for (let it=0;it<22;it++){ const t=(lo+hi)/2; if (inShell(bx+(x-bx)/t, by+(y-by)/t)) hi=t; else lo=t; } return hi; };
    const N=PW*PH, kk=new Int8Array(N).fill(-1), lo=new Int8Array(N), tg=new Float32Array(N).fill(2), shell=new Uint8Array(N);
    for (let y=0;y<PH;y++) for (let x=0;x<PW;x++){ const px=x+0.5, py=y+0.5, i=y*PW+x;
      if (!inShell(px,py)) continue; shell[i]=1; tg[i]=gauge(px,py);
      const [u,v]=nv(px,py), pos=2.2-1.6*(0.55*u+0.75*v)-(u*u+v*v), b=Math.floor(pos);
      lo[i]=Math.max(1,Math.min(3,b)); kk[i]=Math.max(1,Math.min(4,b+((pos-b)>BAYER[x&3][y&3]?1:0))); }
    // a growth line: the first pixel at or past a ring with a neighbour inside it, a step under its tone
    for (let y=0;y<PH;y++) for (let x=0;x<PW;x++){ const i=y*PW+x; if (!shell[i]) continue;
      for (const R of RINGS){ if (tg[i]<R) continue;
        if ([[-1,0],[0,-1],[1,0],[0,1]].some(([dx,dy])=>{ const X=x+dx, Y=y+dy; return X>=0&&Y>=0&&X<PW&&Y<PH&&shell[Y*PW+X]&&tg[Y*PW+X]<R; })){ kk[i]=Math.max(0,lo[i]-(PLATE_CHECKS.indexOf(R)>=0?2:1)); break; } } }
    const xe=cx+rx, xb=xe+sp.out*k, xa=cx+rx*0.7;
    for (let y=0;y<PH;y++) for (let x=0;x<PW;x++){ const px=x+0.5, py=y+0.5, i=y*PW+x;
      if (shell[i]||px<xa||px>xb) continue;
      const f=Math.max(0,(px-xe)/(xb-xe)), r=(sp.base+(sp.tip-sp.base)*f)/2*k, w=(py-cy)/r;
      if (Math.abs(w)>1) continue;
      const pos=1.5-1.1*w, b=Math.floor(pos);
      let kv=Math.max(0,Math.min(2,b+((pos-b)>BAYER[x&3][y&3]?1:0)));
      if ((px>xe && Math.floor((px-xe)/(sp.ring*k))!==Math.floor((px-1-xe)/(sp.ring*k))) || px+Math.max(1,(xb-xe)/20)>xb) kv=Math.max(0,kv-1);
      kk[i]=kv; }
    const out=new Uint8ClampedArray(N*4);
    for (let i=0;i<N;i++){ if (kk[i]<0) continue; const c=RGB.clam[kk[i]]; out[i*4]=c[0]; out[i*4+1]=c[1]; out[i*4+2]=c[2]; out[i*4+3]=255; }
    return out;
  }
  // a seeded bed (catch review pass B: the beds read as shellfish, not gravel). The coverage blobs
  // are pass 2's, their edges now ragged. Shells are laid back to front so each keeps its shape, and
  // each casts a 1 px shadow down and right (black at SHADOW_A) on whatever ground lies under it.
  // Mussels pack in rows that turn slowly across the bed, the dark between them the ramp's darkest
  // step and the sheen on their lit ends; oysters heap in clumps; scallops lie apart, each alone;
  // periwinkles gather in clusters. A clam flat shows no clams (see FLAT).
  function bed(kind, o){
    o=o||{}; kind=KINDS.indexOf(kind)>=0?kind:'mussel';
    const w=o.w||48, h=o.h||32, cover=o.cover!=null?o.cover:0.85, rng=mulberry((o.seed||5)*7919);
    const b=new Buf(w,h), blobs=[], nb=Math.max(1, Math.round(2+cover*3));
    for (let i=0;i<nb;i++) blobs.push({ x:rng()*w, y:rng()*h, rx:w*(0.22+rng()*0.35)*cover, ry:h*(0.30+rng()*0.45)*cover });
    const q=(x,y)=>{ let m=Infinity; for (const B of blobs){ const dx=(x-B.x)/B.rx, dy=(y-B.y)/B.ry; m=Math.min(m, dx*dx+dy*dy); } return m; };
    const keep=(x,y)=>{ const d=q(x,y); return d<=0.6 || (d<1.2 && rng()<(1.2-d)/0.6); };   // stragglers thin out past the edge
    const at=(x,y)=>x>=0&&y>=0&&x<w&&y<h&&b.k[y*w+x]>=0;
    const pts=[], around=(n, lo, hi, sx, sy)=>{ for (let i=0;i<n;i++){ const x=rng()*w, y=rng()*h; if (!keep(x,y)) continue;
      const m=lo+Math.floor(rng()*(hi-lo+1)); for (let j=0;j<m;j++) pts.push([x+(rng()+rng()-1)*sx, y+(rng()+rng()-1)*sy, Math.floor(rng()*4)]); } };
    if (kind==='mussel'){
      const p1=rng()*6.28, p2=rng()*6.28;
      const vOf=(x,y)=>{ const a=0.9*Math.sin(x/7+p1)+0.7*Math.sin(y/5+p2); return a>0.8?1 : a>0?2 : a>-0.8?0 : 3; };
      for (let i=0;i<Math.round(w*h*0.9);i++){ const X=rng()*w, Y=rng()*h; if (!keep(X,Y)) continue;      // packed, none overlapping
        const v=rng()<0.5 ? Math.floor(rng()*4) : vOf(X,Y);
        if (TINY.mussel[v].every(([dx,dy])=>!at(Math.floor(X)+dx, Math.floor(Y)+dy))) place(b, kind, X, Y, v); }
    }
    else if (kind==='oyster') around(Math.round(w*h*0.024), 4, 9, 2.6, 1.7);
    else if (kind==='periwinkle') around(Math.round(w*h*0.02), 3, 7, 1.6, 1.1);
    else if (kind==='scallop') for (let i=0;i<Math.round(w*h*0.05);i++){ const x=rng()*w, y=rng()*h, v=Math.floor(rng()*4); if (keep(x,y)) pts.push([x,y,v]); }
    pts.sort((P,Q)=>P[1]-Q[1] || P[0]-Q[0]);
    for (const [x,y,v] of pts){
      if (kind==='scallop'){ let clear=true; const X=Math.floor(x), Y=Math.floor(y);                 // each alone on the sand
        for (let yy=Y-2;yy<=Y+2&&clear;yy++) for (let xx=X-3;xx<=X+3;xx++) if (at(xx,yy)){ clear=false; break; }
        if (!clear) continue; }
      place(b, kind, x, y, v);
    }
    if (kind==='mussel'){ const gap=[];                                                           // the dark between the shells
      for (let y=0;y<h;y++) for (let x=0;x<w;x++) if (!at(x,y) && (q(x+0.5,y+0.5)<0.7 || (at(x-1,y)&&at(x+1,y)) || (at(x,y-1)&&at(x,y+1)))) gap.push(y*w+x);
      for (const j of gap){ b.k[j]=0; b.kind[j]='mussel'; } }
    if (kind==='mussel'||kind==='periwinkle') for (let j=0;j<w*h;j++) if (b.kind[j]===kind && b.k[j]===3 && rng()<(kind==='mussel'?0.07:0.14)) b.k[j]=4;
    const shade=new Uint8Array(w*h);
    for (let y=1;y<h;y++) for (let x=1;x<w;x++) if (!at(x,y) && at(x-1,y-1)) shade[y*w+x]=1;
    return { w, h, rgba:toRGBA(b, shade) };
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
    renderItem, renderHandful, bed, holes, spurt, heap, mulberry, PW, PH, PLATE_SIPHON, MARKS, renderPlate };
})(typeof globalThis!=='undefined'?globalThis:window);
