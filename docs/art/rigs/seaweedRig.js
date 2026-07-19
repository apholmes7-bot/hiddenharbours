/* Hidden Harbours — parametric SEAWEED CLUMPS (floating rockweed). Atmospheric water
   prop. Drifts on open water, rafts into bigger mats, catches on buoy lines, strands on
   the beach at low tide. Seen at mid zooms — silhouette + colour do the work, not detail.

   Flat-on-the-water ¾ top-down: a loose mat of rockweed hugging the surface. Muted, dark
   tones off the water/foam palette — olive, brown-green, a touch of ochre on the fringe. It
   sits QUIETLY on the water: darker than the sea, never competing with the buoys. Soft rim
   (dark olive, no hard black keyline) so it reads as lying on the surface, with a lighter
   olive fringe where it breaks the surface.

   Three size tiers are SEPARATE sprites (the game merges small into big — not an animation):
     wisp  ~12×8  — a strand or two, barely a smudge.
     clump ~20×14 — a loose tangle, irregular edge.
     mat    32×24 — a raft with interior texture + a lighter breaking-surface fringe.
   Each tier has two float variants (a/b) for scatter variety, plus a matte 'beach' variant
   (same shape as 'a', darker/flatter) for weed stranded on sand.

   Exposes globalThis.Seaweed with:
     TIERS {wisp,clump,mat} -> {w,h}
     render(tier, variant, env) -> { w, h, rgba:Uint8ClampedArray(w*h*4) }
       tier: 'wisp'|'clump'|'mat' · variant: 'a'|'b' · env: 'float'|'beach'
   Works in the run_script sandbox (bake) and in the browser (live preview). */
(function (root) {
  const TIERS = { wisp:{w:12,h:8}, clump:{w:20,h:14}, mat:{w:32,h:24} };

  // float ramp (olive / brown-green + ochre fringe)
  const FLOAT = { dp:'#242f1c', sh:'#2e3b22', mid:'#3b4b2b', hi:'#586b38', ochMid:'#7d6a3a', ochHi:'#968049' };
  // beached ramp — darker, browner, matte (no bright breaking-surface fringe)
  const BEACH = { dp:'#1b2416', sh:'#26301c', mid:'#313c24', hi:'#414f2c', ochMid:'#5f5230', ochHi:'#6f6136' };

  function mulberry32(a){ return function(){ a|=0; a=a+0x6D2B79F5|0; let t=Math.imul(a^a>>>15,1|a); t=t+Math.imul(t^t>>>7,61|t)^t; return ((t^t>>>14)>>>0)/4294967296; }; }

  const CFG = {
    wisp:  { cores:2, coreRX:2.6, coreRY:1.7, fronds:4,  frondLen:2.6, fringe:0.30, mottle:0.10, och:1 },
    clump: { cores:3, coreRX:4.4, coreRY:3.0, fronds:8,  frondLen:3.6, fringe:0.42, mottle:0.16, och:3 },
    mat:   { cores:6, coreRX:6.4, coreRY:4.6, fronds:14, frondLen:5.0, fringe:0.55, mottle:0.22, och:6 },
  };
  const SEED = { wispa:101, wispb:102, clumpa:201, clumpb:202, mata:301, matb:302 };

  function build(tier, variant){
    const { w, h } = TIERS[tier], cfg = CFG[tier];
    const key=new Array(w*h).fill(''), mat=new Array(w*h).fill(0);   // mat: 0 empty,1 weed,2 ochre
    const idx=(x,y)=>y*w+x, inb=(x,y)=>x>=0&&x<w&&y>=0&&y<h;
    const rng = mulberry32(SEED[tier+variant] || SEED[tier+'a']);
    const cx=w/2, cy=h/2;
    const put=(x,y,m)=>{ x=Math.round(x); y=Math.round(y); if(inb(x,y)){ key[idx(x,y)]='mid'; mat[idx(x,y)]=m||1; } };
    const ell=(ex,ey,rx,ry)=>{ for(let y=Math.floor(ey-ry);y<=Math.ceil(ey+ry);y++)for(let x=Math.floor(ex-rx);x<=Math.ceil(ex+rx);x++){ const dx=(x-ex)/(rx+0.001),dy=(y-ey)/(ry+0.001); if(dx*dx+dy*dy<=1) put(x,y,1); } };
    const taper=(x0,y0,x1,y1,r0,r1)=>{ const dx=x1-x0,dy=y1-y0,L2=dx*dx+dy*dy||1; const mnx=Math.floor(Math.min(x0,x1)-Math.max(r0,r1)),mxx=Math.ceil(Math.max(x0,x1)+Math.max(r0,r1)),mny=Math.floor(Math.min(y0,y1)-Math.max(r0,r1)),mxy=Math.ceil(Math.max(y0,y1)+Math.max(r0,r1)); for(let y=mny;y<=mxy;y++)for(let x=mnx;x<=mxx;x++){ let t=((x-x0)*dx+(y-y0)*dy)/L2; t=Math.max(0,Math.min(1,t)); const px=x0+dx*t,py=y0+dy*t; if(Math.hypot(x-px,y-py)<=r0+(r1-r0)*t) put(x,y,1); } };

    // overlapping core blobs — irregular mat
    for(let i=0;i<cfg.cores;i++){
      const ox=(rng()-0.5)*cfg.coreRX*1.5, oy=(rng()-0.5)*cfg.coreRY*1.4;
      ell(cx+ox, cy+oy, cfg.coreRX*(0.55+rng()*0.6), cfg.coreRY*(0.55+rng()*0.6));
    }
    // fingering fronds from the edge outward
    for(let i=0;i<cfg.fronds;i++){
      const a=rng()*Math.PI*2, r0=Math.min(cfg.coreRX,cfg.coreRY)*0.7;
      const sx=cx+Math.cos(a)*r0, sy=cy+Math.sin(a)*r0*0.7;
      const len=cfg.frondLen*(0.6+rng()*0.8);
      taper(sx,sy, cx+Math.cos(a)*(r0+len), cy+Math.sin(a)*(r0+len)*0.72, 1.2, 0.35);
    }
    return { w, h, key, mat, idx, inb, rng, cx, cy };
  }

  function finish(blob, env){
    const { w, h, key, mat, idx, inb, rng } = blob;
    const P = env==='beach' ? BEACH : FLOAT;
    const src = key.slice();
    const filled=(x,y)=> inb(x,y) && src[idx(x,y)];
    // dark soft rim (sits in the water) — outer 1px of the silhouette
    for(let y=0;y<h;y++)for(let x=0;x<w;x++){ if(!src[idx(x,y)])continue;
      const edge = !filled(x-1,y)||!filled(x+1,y)||!filled(x,y-1)||!filled(x,y+1);
      if(edge) key[idx(x,y)]='dp';
    }
    // breaking-surface fringe (float only): lighter olive just inside the upper-left rim
    if(env!=='beach'){
      for(let y=0;y<h;y++)for(let x=0;x<w;x++){ if(key[idx(x,y)]!=='mid')continue;
        const nearRim = (inb(x-1,y)&&src[idx(x-1,y)]&&key[idx(x-1,y)]==='dp') || (inb(x,y-1)&&src[idx(x,y-1)]&&key[idx(x,y-1)]==='dp');
        if(nearRim && blob.rng()<0.85) key[idx(x,y)]='hi';
      }
    }
    // interior mottle — darker tangles + a few lighter flecks
    const mo = CFG[Object.keys(TIERS).find(t=>TIERS[t].w===w)]?.mottle ?? 0.16;
    for(let y=0;y<h;y++)for(let x=0;x<w;x++){ if(key[idx(x,y)]!=='mid')continue;
      const r=rng(); if(r<mo) key[idx(x,y)]='sh'; else if(env!=='beach' && r>0.94) key[idx(x,y)]='hi';
    }
    // ochre flecks on the fringe
    const ochN = CFG[Object.keys(TIERS).find(t=>TIERS[t].w===w)]?.och ?? 3;
    let placed=0, tries=0;
    while(placed<ochN && tries++<200){ const x=Math.floor(rng()*w), y=Math.floor(rng()*h); const i=idx(x,y);
      if(src[i] && (key[i]==='dp'||key[i]==='hi')){ mat[i]=2; key[i]= rng()<0.5?'hi':'mid'; placed++; }
    }
    return P;
  }

  function hex2rgb(h){ return [parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]; }
  function toRGBA(blob, P){
    const { w, h, key, mat } = blob, out=new Uint8ClampedArray(w*h*4);
    for(let i=0;i<w*h;i++){ const k=key[i]; if(!k){ out[i*4+3]=0; continue; }
      let hex;
      if(mat[i]===2) hex = (k==='hi') ? P.ochHi : P.ochMid;
      else hex = k==='hi'?P.hi : k==='sh'?P.sh : k==='dp'?P.dp : P.mid;
      const [r,g,b]=hex2rgb(hex); out[i*4]=r; out[i*4+1]=g; out[i*4+2]=b; out[i*4+3]=255; }
    return out;
  }

  function render(tier, variant='a', env='float'){
    const shapeVariant = env==='beach' ? 'a' : variant;
    const blob = build(tier, shapeVariant);
    const P = finish(blob, env);
    return { w:blob.w, h:blob.h, rgba: toRGBA(blob, P) };
  }

  root.Seaweed = { TIERS, FLOAT, BEACH, render };
})(typeof globalThis!=='undefined'?globalThis:window);
