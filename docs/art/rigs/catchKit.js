/* Hidden Harbours — CATCH KIT: one item factory for every container fill (catch-handling pass).
   The diegetic rule: containers fill with the catch's OWN rigs — a tote of cod shows cod
   (FishIso deck lays), a tray of lobster shows the lobster rig, buckets of mussels show
   mussels. Any container rig computes WHERE (its projected fill-surface slots); this kit
   answers WHAT: seeded, monotonic item lists (growing a fill never moves earlier items)
   and ready-tinted canvases, including the ROTTEN state (spoil 0..1 = green shift +
   dither mottle on any item; green particle FX stay runtime — colour = SPOIL).
   Needs whichever rigs the requested catch uses: FishIso (fish), Lobster, RockCrab,
   Shellfish. CATCHES = fish species + lobster/crab/mussel/clam + mixed.
   Exposes globalThis.CatchKit = { SPOIL, CATCHES, item(kind,{variant,scale,spoil}),
   fillItems(catchKey,fill,seed), FRAC, maxN, particles(seed,n) }. */
(function (root) {
  const SPOIL='#7d9a46', SPRGB=[125,154,70];
  const BAYER=[[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));
  const FRAC={ empty:0, few:0.25, half:0.55, full:0.85, brim:1 };
  const FISH=['cod','haddock','pollock','mackerel'];
  const CATCHES=FISH.concat(['lobster','crab','mussel','clam','mixed']);
  const MAXN={ cod:18, haddock:20, pollock:20, mackerel:24, lobster:24, crab:24, mussel:24, clam:24, mixed:22 };
  function mulberry(seed){ let a=seed>>>0; return function(){ a|=0; a=(a+0x6D2B79F5)|0;
    let t=Math.imul(a^(a>>>15),1|a); t=(t+Math.imul(t^(t>>>7),61|t))^t; return ((t^(t>>>14))>>>0)/4294967296; }; }

  function tintSpoil(rgba, w, h, spoil){
    if (!spoil) return rgba;
    const out=new Uint8ClampedArray(rgba);
    for (let y=0;y<h;y++) for (let x=0;x<w;x++){
      const i=(y*w+x)*4; if (out[i+3]===0) continue;
      const lum=out[i]+out[i+1]+out[i+2];
      if (lum<70) continue;                                 // keylines stay dark
      const m=spoil*(0.40 + (BAYER[x&3][y&3] < spoil*0.55 ? 0.28 : 0));
      out[i]  =Math.round(out[i]*(1-m)+SPRGB[0]*m);
      out[i+1]=Math.round(out[i+1]*(1-m)+SPRGB[1]*m);
      out[i+2]=Math.round(out[i+2]*(1-m)+SPRGB[2]*m);
    }
    return out;
  }
  const cache={};
  function toCanvas(rgba,w,h){
    const cv=document.createElement('canvas'); cv.width=w; cv.height=h;
    cv.getContext('2d').putImageData(new ImageData(rgba,w,h),0,0);
    return cv;
  }
  // -> {canvas,w,h,ax,ay} — (ax,ay) = ground-contact anchor inside the cell
  function item(kind, opts){
    opts=opts||{};
    const v=opts.variant||0, sc=opts.scale||1, sp=Math.max(0,Math.min(1,opts.spoil||0));
    const k=kind+'|'+v+'|'+sc.toFixed(2)+'|'+sp.toFixed(2);
    if (cache[k]) return cache[k];
    let rgba,w,h,ax,ay;
    if (FISH.indexOf(kind)>=0){
      const F=root.FishIso; if(!F) return null;
      rgba=F.render([2,6,3,5][v%4], { species:kind, rest:'deck', frame:v%4, scale:sc, spoil:sp });
      w=F.W; h=F.H; ax=F.pivot.x; ay=F.pivot.y;
      cache[k]={ canvas:toCanvas(rgba,w,h), w,h,ax,ay };
      return cache[k];
    }
    if (kind==='lobster' || kind==='crab'){
      const CR=root.Crustacean;
      if (CR){                                            // scalable rebuild — replots at any size
        rgba=CR.render(kind,{ pose:'walk', frame:v%4, ang:[0.4,2.2,3.7,5.3][v%4], scale:sc });
        w=CR.W; h=CR.H; ax=CR.pivot.x; ay=CR.pivot.y;
      } else if (kind==='lobster'){
        const L=root.Lobster; if(!L) return null;
        rgba=L.renderDeck(L.FRAMES[[0,2,4,6][v%4]]); w=L.W; h=L.H; ax=24; ay=30;
      } else {
        const C=root.RockCrab; if(!C) return null;
        rgba=C.renderDeck(C.FRAMES[[0,2,4][v%3]]); w=C.W; h=C.H; ax=24; ay=46;
      }
    } else {
      const S=root.Shellfish; if(!S) return null;
      rgba=S.renderItem(kind, v%S.VARIANTS); w=S.IW; h=S.IH; ax=S.ipivot.x; ay=S.ipivot.y;
    }
    rgba=tintSpoil(rgba,w,h,sp);
    cache[k]={ canvas:toCanvas(rgba,w,h), w,h,ax,ay };
    return cache[k];
  }
  // seeded, monotonic: index i's item never changes as fill grows.
  // `count` (optional) overrides the fraction×MAXN count — containers pass their slot
  // capacity so FULL/BRIM genuinely heap to the visible layers.
  function fillItems(catchKey, fill, seed, count){
    const frac=FRAC[fill]!=null?FRAC[fill]:0;
    const maxN=MAXN[catchKey]||8;
    const n=count!=null ? Math.round(frac*count) : Math.round(frac*maxN);
    const rng=mulberry((seed||7)*2654435761);
    const out=[];
    for (let i=0;i<n;i++){
      let kind=catchKey;
      if (catchKey==='mixed'){
        const pool=['mackerel','haddock','lobster','crab','pollock','cod'];
        kind=pool[Math.floor(rng()*pool.length)];
      } else rng();
      out.push({ kind, variant:Math.floor(rng()*4), scale:(kind==='mussel'||kind==='clam')?1:0.82+rng()*0.3 });
    }
    return out;
  }
  // green rot motes — deterministic spec, pages/games animate it
  function particles(seed, n){
    const rng=mulberry((seed||3)*40503);
    const out=[];
    for (let i=0;i<(n||6);i++) out.push({ ox:(rng()-0.5)*14, oy:-2-rng()*4, phase:rng()*6.28, speed:0.008+rng()*0.008 });
    return out;
  }
  root.CatchKit = { SPOIL, CATCHES, FRAC, MAXN, item, fillItems, tintSpoil, particles };
})(typeof globalThis!=='undefined'?globalThis:window);
