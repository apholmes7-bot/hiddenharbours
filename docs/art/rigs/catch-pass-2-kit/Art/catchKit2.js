/* Hidden Harbours — CATCH KIT 2 (catch pass 2). catchKit.js is untouched. One item factory for
   every container fill across ALL FOURTEEN catch kinds, at STRICT WORLD SCALE. The diegetic rule
   holds: containers fill with the catch's OWN rigs — a tote of herring is ninety 8 px herring, a
   pail of mussels is a HEAP of 2 px mussels (Shellfish2.heap), a pot of lobster is the lofted
   Crustacean2 plotter scattered on the floor slots.
   Needs whichever rigs the catch uses: FishIso2 · Crustacean2 · Shellfish2.
     item(kind,{variant,scale,spoil})      -> {canvas,w,h,ax,ay} ready-tinted, ground anchor
     fillItems(catch,fill,seed,count)      -> seeded MONOTONIC item list (growing a fill never
                                              moves earlier items); small fish pack DENSE (extra
                                              jittered slots per slot); shellfish -> [] (isHeap)
     heap(kind,poly,depthPx,fill,seed)     -> {canvas,x,y}: the heap surface for a container
                                              opening (px polygon), lowered by (1-fill) x depthPx
     hold(kind,scale)                      -> {mass,hands} across fish / crustacean / handful
     SIZES                                 -> metres at scale 1 for every kind (the scale lineup)
   Exposes globalThis.CatchKit2 = { SPOIL,CATCHES,FISH,CRUST,SHELL,SIZES,FRAC,DENSE,item,
   fillItems,isHeap,heap,hold,tintSpoil,particles,mulberry }. */
(function (root) {
  const SPOIL='#7d9a46', SPRGB=[125,154,70];
  const BAYER=[[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));
  const FRAC={ empty:0, few:0.25, half:0.55, full:0.85, brim:1 };
  const FISH=['cod','haddock','pollock','mackerel','bass','flounder','herring'], CRUST=['lobster','crab'], SHELL=['mussel','clam','scallop','oyster','periwinkle'];
  const CATCHES=FISH.concat(CRUST,SHELL,['mixed']);
  const SIZES={
    cod:{len:0.70,label:'COD'}, haddock:{len:0.55,label:'HADDOCK'}, pollock:{len:0.60,label:'POLLOCK'}, mackerel:{len:0.45,label:'MACKEREL'},
    bass:{len:0.75,label:'STRIPED BASS'}, flounder:{len:0.35,label:'FLOUNDER'}, herring:{len:0.25,label:'HERRING'},
    lobster:{len:0.40,label:'LOBSTER'}, crab:{len:0.13,span:0.30,label:'ROCK CRAB'},
    mussel:{len:0.06,mass:0.012,label:'BLUE MUSSEL'}, clam:{len:0.07,mass:0.03,label:'SOFT-SHELL CLAM'}, scallop:{len:0.10,mass:0.08,label:'SEA SCALLOP'},
    oyster:{len:0.10,mass:0.09,label:'OYSTER'}, periwinkle:{len:0.025,mass:0.004,label:'PERIWINKLE'},
  };
  const DENSE={ herring:3, mackerel:1.5, flounder:1.5, crab:1.5 };
  function mulberry(seed){ let a=seed>>>0; return function(){ a|=0; a=(a+0x6D2B79F5)|0;
    let t=Math.imul(a^(a>>>15),1|a); t=(t+Math.imul(t^(t>>>7),61|t))^t; return ((t^(t>>>14))>>>0)/4294967296; }; }
  function tintSpoil(rgba, w, h, spoil){
    if (!spoil) return rgba;
    const out=new Uint8ClampedArray(rgba);
    for (let y=0;y<h;y++) for (let x=0;x<w;x++){
      const i=(y*w+x)*4; if (out[i+3]===0) continue;
      if (out[i]+out[i+1]+out[i+2]<70) continue;
      const m=spoil*(0.40+(BAYER[x&3][y&3]<spoil*0.55?0.28:0));
      out[i]=Math.round(out[i]*(1-m)+SPRGB[0]*m); out[i+1]=Math.round(out[i+1]*(1-m)+SPRGB[1]*m); out[i+2]=Math.round(out[i+2]*(1-m)+SPRGB[2]*m);
    }
    return out;
  }
  const cache={};
  function toCanvas(rgba,w,h){ const cv=document.createElement('canvas'); cv.width=w; cv.height=h; cv.getContext('2d').putImageData(new ImageData(rgba,w,h),0,0); return cv; }
  const isHeap=(kind)=>SHELL.indexOf(kind)>=0;
  function item(kind, opts){
    opts=opts||{};
    const v=opts.variant||0, sc=opts.scale||1, sp=Math.max(0,Math.min(1,opts.spoil||0));
    const k=kind+'|'+v+'|'+sc.toFixed(2)+'|'+sp.toFixed(2);
    if (cache[k]) return cache[k];
    let rgba,w,h,ax,ay;
    if (FISH.indexOf(kind)>=0){
      const F=root.FishIso2; if (!F) return null;
      rgba=F.render([2,6,3,5][v%4], { species:kind, rest:'deck', frame:v%4, scale:sc, spoil:sp }); w=F.W; h=F.H; ax=F.pivot.x; ay=F.pivot.y;
    } else if (CRUST.indexOf(kind)>=0){
      const CR=root.Crustacean2; if (!CR) return null;
      rgba=CR.render(kind, { pose:'walk', frame:v%4, ang:[0.4,2.2,3.7,5.3][v%4], scale:sc, spoil:sp }); w=CR.W; h=CR.H; ax=CR.pivot.x; ay=CR.pivot.y;
    } else {
      const SH=root.Shellfish2; if (!SH) return null;
      rgba=tintSpoil(SH.renderItem(kind, v%4, sc), SH.IW, SH.IH, sp); w=SH.IW; h=SH.IH; ax=SH.ipivot.x; ay=SH.ipivot.y;
    }
    return (cache[k]={ canvas:toCanvas(rgba,w,h), w,h,ax,ay });
  }
  // seeded + monotonic. `count` = the container's slot count; DENSE kinds add jittered extras
  // (jx,jy in px) so a tote of herring reads as a tote of herring, not eight fish in a box.
  function fillItems(catchKey, fill, seed, count){
    if (isHeap(catchKey)) return [];
    const frac=FRAC[fill]!=null?FRAC[fill]:0, dense=DENSE[catchKey]||1;
    const n=Math.round(frac*(count!=null?count:20)*dense);
    const rng=mulberry((seed||7)*2654435761), out=[];
    const F=root.FishIso2;
    for (let i=0;i<n;i++){
      let kind=catchKey;
      if (catchKey==='mixed'){ const pool=['herring','mackerel','haddock','lobster','crab','pollock','cod','bass','flounder']; kind=pool[Math.floor(rng()*pool.length)]; }
      else rng();
      const rg=(F&&F.SPECIES[kind]&&F.SPECIES[kind].range)||[0.85,1.15];
      const scale=Math.min(rg[1], Math.max(rg[0], 0.85+rng()*0.3));
      const jit=dense>1 && i>=n/dense;
      out.push({ kind, variant:Math.floor(rng()*4), scale, jx:jit?Math.round((rng()-0.5)*7):0, jy:jit?Math.round((rng()-0.5)*4):0 });
    }
    return out;
  }
  // the shellfish heap for a container: poly = [{dx,dy}] rim polygon in px (relative to the
  // container pivot), depthPx = rim-to-floor in px. Lowers the surface for partial fills; brim
  // crowns above the rim. Returns null for empty.
  function heap(kind, poly, depthPx, fill, seed, opts){
    const SH=root.Shellfish2; if (!SH) return null;
    const frac=FRAC[fill]!=null?FRAC[fill]:0; if (frac<=0) return null;
    const lower=Math.round((1-Math.min(1,frac/0.85))*depthPx), crown = fill==='brim' ? 2 : 0;
    const P=poly.map(p=>({ x:p.dx, y:p.dy+lower }));
    let x0=Infinity,y0=Infinity,x1=-Infinity,y1=-Infinity; for (const p of P){ x0=Math.min(x0,p.x); y0=Math.min(y0,p.y-crown); x1=Math.max(x1,p.x); y1=Math.max(y1,p.y); }
    x0=Math.floor(x0); y0=Math.floor(y0); const w=Math.ceil(x1)-x0+1, h=Math.ceil(y1)-y0+1;
    const inPoly=(px,py)=>{ let c=false; for (let i=0,j=P.length-1;i<P.length;j=i++){ const a=P[i], b=P[j];
      if ((a.y>py)!==(b.y>py) && px < (b.x-a.x)*(py-a.y)/(b.y-a.y)+a.x) c=!c; } return c; };
    const mx=(x0+x1)/2, my=(y0+y1)/2, rx=Math.max(1,(x1-x0)/2), ry=Math.max(1,(y1-y0)/2);
    const mask=(x,y)=>{ const X=x+x0, Y=y+y0; if (inPoly(X,Y)) return true;
      if (!crown) return false; const dx=(X-mx)/(rx*0.62), dy=(Y-my+crown+1)/(ry*0.9); return dx*dx+dy*dy<=1; };
    const dome=(x,y)=>-(((x+x0)-mx)/rx*0.6 + ((y+y0)-my)/ry*0.8);
    const k='H|'+kind+'|'+P.map(p=>p.x+','+p.y).join(';')+'|'+crown+'|'+(seed||9)+'|'+(opts&&opts.spoil?opts.spoil.toFixed(2):'0');
    if (cache[k]) return cache[k];
    let rgba=SH.heap(kind, w, h, seed||9, mask, { dome });
    if (opts && opts.spoil) rgba=tintSpoil(rgba, w, h, opts.spoil);
    return (cache[k]={ canvas:toCanvas(rgba,w,h), x:x0, y:y0, w, h });
  }
  function hold(kind, scale){
    if (FISH.indexOf(kind)>=0 && root.FishIso2) return root.FishIso2.hold(kind, scale);
    if (CRUST.indexOf(kind)>=0 && root.Crustacean2) return root.Crustacean2.hold(kind, scale);
    const m=(SIZES[kind]&&SIZES[kind].mass||0.02)*7*(scale||1);
    return { mass:Math.round(m*100)/100, hands:1, handful:true };
  }
  function particles(seed, n){
    const rng=mulberry((seed||3)*40503), out=[];
    for (let i=0;i<(n||6);i++) out.push({ ox:(rng()-0.5)*14, oy:-2-rng()*4, phase:rng()*6.28, speed:0.008+rng()*0.008 });
    return out;
  }
  root.CatchKit2 = { SPOIL, CATCHES, FISH, CRUST, SHELL, SIZES, FRAC, DENSE, item, fillItems, isHeap, heap, hold, tintSpoil, particles, mulberry };
})(typeof globalThis!=='undefined'?globalThis:window);
