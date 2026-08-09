/* Hidden Harbours — POT FAUNA: what actually comes up in a trap besides the keepers.
   The catch-handling rule holds — containers fill with the catch's OWN rigs — so a
   hauled trap needs the rest of the bottom: urchins, whelks, starfish, a sculpin, a
   snarl of rockweed, and the bait bag itself. Jonah crab and undersize shorts are the
   existing Crustacean plotter re-tinted / re-scaled, not new animals.

   Top-down 3/4 read to match the CatchKit items. 32 px = 1 m. Upper-left key. No AA.
   RINGLESS (ADR 0031). Cell 28x24, pivot (14,21) = ground contact, so every item drops
   straight onto a container's slot points.

   Exposes globalThis.TrapFauna (the new plots) and globalThis.TrapCatch — the wrapper
   CatchKit-shaped façade the containers actually call: item(kind), fillItems(mix, fill,
   seed, count), MIXES (what a trap of each sort comes up holding). */
(function (root) {
  const W=28, H=24, PX=14, PY=21;

  const RAMP = {
    URCH:['#141f10','#233317','#334a22','#48682f','#5f8a3f'],
    SPIN:['#0f1a12','#1b2a1c','#2a3d29','#3b5238','#4d6a49'],
    WHLK:['#3a2a1c','#63482f','#8c6a45','#b9975f','#d8bd8e'],
    STAR:['#5f2c20','#8a4530','#b25e3e','#cf7a52','#e29a72'],
    SCUL:['#241a11','#3f3524','#5c5136','#7d6f4b','#9c8d63'],
    SCUD:['#141009','#241d13','#33291b','#443724','#55452e'],
    KELP:['#1a2311','#26301a','#39471d','#5c6e2c','#8a9a45'],
    BAG :['#4a4318','#6d6224','#948436','#bda94c','#dcc86a'],
    HERR:['#2b3439','#49555a','#71807f','#a6b6b8','#cfdadb'],
    CREAM:['#9c7f57','#b49a74','#cdb890','#e0d2b2','#ece0c8'],
  };
  const KINDS=['urchin','whelk','starfish','sculpin','kelp','baitbag'];

  // ---- tiny plotter (2D, ringless) -----------------------------------------
  function buf(){ return { c:new Array(W*H).fill(null), s:new Array(W*H).fill(0) }; }
  const ix=(x,y)=>y*W+x, inb=(x,y)=>x>=0&&x<W&&y>=0&&y<H;
  function px(b,x,y,ramp,lv){ x=Math.round(x); y=Math.round(y); if(!inb(x,y))return;
    b.c[ix(x,y)]=ramp; b.s[ix(x,y)]=lv; }
  function ell(b,cx,cy,rx,ry,ramp,lv,dome){
    for(let y=Math.floor(cy-ry);y<=Math.ceil(cy+ry);y++)for(let x=Math.floor(cx-rx);x<=Math.ceil(cx+rx);x++){
      const dx=(x-cx)/(rx+.001), dy=(y-cy)/(ry+.001); const r2=dx*dx+dy*dy;
      if(r2>1) continue;
      let l=lv;
      if(dome){ const L=-(dx*0.62+dy*0.78); l=Math.max(0,Math.min(4, Math.round(2 + L*1.9 - r2*0.5))); }
      px(b,x,y,ramp,l);
    }
  }
  function tap(b,x0,y0,x1,y1,r0,r1,ramp,lv){
    const dx=x1-x0,dy=y1-y0,L2=dx*dx+dy*dy||1, R=Math.max(r0,r1);
    for(let y=Math.floor(Math.min(y0,y1)-R);y<=Math.ceil(Math.max(y0,y1)+R);y++)
    for(let x=Math.floor(Math.min(x0,x1)-R);x<=Math.ceil(Math.max(x0,x1)+R);x++){
      let t=((x-x0)*dx+(y-y0)*dy)/L2; t=Math.max(0,Math.min(1,t));
      const qx=x0+dx*t, qy=y0+dy*t, d=Math.hypot(x-qx,y-qy), rr=r0+(r1-r0)*t;
      if(d<=rr) px(b,x,y,ramp,lv!=null?lv:(d<rr*0.45?3:2));
    }
  }
  const rnd=(s)=>{ let a=s>>>0; return ()=>{ a|=0; a=(a+0x6D2B79F5)|0;
    let t=Math.imul(a^(a>>>15),1|a); t=(t+Math.imul(t^(t>>>7),61|t))^t; return ((t^(t>>>14))>>>0)/4294967296; }; };

  // ---- the animals ----------------------------------------------------------
  function drawUrchin(b,v,S){
    const cx=PX, cy=PY-4*S, r=5.2*S;
    for(let i=0;i<20;i++){                       // spines first, radiating
      const a=(i/20)*Math.PI*2 + v*0.31;
      const l=r*(1.35+((i+v)%3)*0.14);
      tap(b,cx+Math.cos(a)*r*0.5, cy+Math.sin(a)*r*0.42, cx+Math.cos(a)*l, cy+Math.sin(a)*l*0.8,
          0.9*S,0.35*S,'SPIN', (Math.cos(a)<0||Math.sin(a)<0)?3:1);
    }
    ell(b,cx,cy,r,r*0.82,'URCH',3,true);
    for(let i=0;i<5;i++){ const a=(i/5)*Math.PI*2+0.4;
      tap(b,cx,cy,cx+Math.cos(a)*r*0.86,cy+Math.sin(a)*r*0.7,0.6*S,0.5*S,'URCH',1); }
    px(b,cx-1,cy-1,'URCH',4);
  }
  function drawWhelk(b,v,S){
    const cx=PX+0.5, cy=PY-3.5*S;
    ell(b,cx,cy,5.0*S,4.0*S,'WHLK',3,true);       // body whorl
    ell(b,cx+1.4*S,cy-1.6*S,3.1*S,2.5*S,'WHLK',3,true);
    ell(b,cx+2.6*S,cy-2.9*S,1.8*S,1.5*S,'WHLK',4,true);
    for(let i=0;i<7;i++){                         // spiral suture
      const a=-0.7+i*0.42, rr=(4.6-i*0.5)*S;
      px(b,cx+Math.cos(a)*rr, cy+Math.sin(a)*rr*0.8,'WHLK',1);
    }
    tap(b,cx-3.6*S,cy+2.0*S,cx-5.4*S,cy+3.4*S,1.5*S,0.7*S,'WHLK',2);   // siphonal canal
    ell(b,cx-2.2*S,cy+1.8*S,2.0*S,1.4*S,'CREAM',3,true);               // aperture
  }
  function drawStar(b,v,S){
    const cx=PX, cy=PY-3*S, R=7.4*S;
    for(let i=0;i<5;i++){
      const a=(i/5)*Math.PI*2 - Math.PI/2 + v*0.25;
      tap(b,cx,cy,cx+Math.cos(a)*R,cy+Math.sin(a)*R*0.82,2.5*S,0.9*S,'STAR',
          (Math.cos(a)<0.1&&Math.sin(a)<0.1)?3:2);
    }
    ell(b,cx,cy,3.0*S,2.5*S,'STAR',3,true);
    for(let i=0;i<5;i++){                          // papulae dots down each arm
      const a=(i/5)*Math.PI*2 - Math.PI/2 + v*0.25;
      for(const t of [0.45,0.72]) px(b,cx+Math.cos(a)*R*t, cy+Math.sin(a)*R*t*0.82,'STAR',4);
    }
  }
  function drawSculpin(b,v,S){
    const cx=PX-1, cy=PY-4*S, g=rnd(11+v);
    tap(b,cx+3.5*S,cy,cx+10*S,cy+0.6*S,3.0*S,0.8*S,'SCUL',2);          // tapering body
    for(const sg of [-1,1]) tap(b,cx+4.5*S,cy+sg*1.4*S,cx+7.5*S,cy+sg*4.2*S,1.5*S,0.5*S,'SCUL',sg<0?3:1);
    ell(b,cx+1.5*S,cy,5.2*S,4.2*S,'SCUL',3,true);                      // the big flat head
    for(const sg of [-1,1]) tap(b,cx+1.0*S,cy+sg*3.4*S,cx-1.2*S,cy+sg*6.4*S,2.2*S,0.8*S,'SCUL',sg<0?4:1);
    tap(b,cx+9.6*S,cy+0.6*S,cx+12.4*S,cy+0.6*S,0.6*S,3.0*S,'SCUL',2);  // tail fan
    for(let i=0;i<16;i++){                                              // mottle
      const x=cx-2*S+g()*13*S, y=cy-4*S+g()*8*S;
      if(b.c[ix(Math.round(x),Math.round(y))]) px(b,x,y,'SCUD',2);
    }
    px(b,cx+0.2*S,cy-1.9*S,'SCUD',0); px(b,cx+0.2*S,cy+1.9*S,'SCUD',0);
    px(b,cx-0.6*S,cy-2.2*S,'CREAM',4);
    for(let x=-3;x<=4;x++) px(b,cx+3.6*S+x*0.5,cy+4.0*S,'CREAM',3);    // pale belly edge
  }
  function drawKelp(b,v,S){
    const g=rnd(5+v);
    for(let s=0;s<4;s++){
      let x=PX-6*S+s*3.6*S, y=PY-1;
      const sw=(g()-0.5)*1.4;
      for(let k=0;k<9;k++){
        tap(b,x,y,x+sw,y-2.0*S,1.5*S-k*0.08,1.3*S-k*0.08,'KELP', k%3===0?3:(k%3===1?2:1));
        if(k%2===1){ ell(b,x+ (s%2?1.8:-1.8)*S, y-1.0*S, 1.2*S,0.9*S,'KELP',4,true); }  // bladders
        x+=sw; y-=2.0*S;
      }
    }
  }
  function drawBag(b,v,S){
    const cx=PX, cy=PY-5*S;
    ell(b,cx,cy,5.0*S,5.6*S,'BAG',3,true);
    for(let i=0;i<7;i++){                            // mesh weave
      for(let y=-5;y<=5;y++){ const x=Math.round(cx-4.5*S+i*1.5*S+ (y%2?0.5:0));
        if(b.c[ix(x,Math.round(cy+y*S))]==='BAG') px(b,x,cy+y*S,'BAG',1); }
    }
    for(const [dx,dy] of [[-1.8,1.2],[1.4,2.6],[0.2,-1.4]])              // herring showing through
      ell(b,cx+dx*S,cy+dy*S,1.9*S,1.1*S,'HERR',3,true);
    tap(b,cx,cy-5.2*S,cx,cy-8.4*S,1.5*S,0.9*S,'BAG',2);                  // gathered neck
    tap(b,cx-0.5*S,cy-8.2*S,cx+4.2*S,cy-9.6*S,0.5*S,0.5*S,'BAG',4);      // becket to the trap
  }
  const DRAW={ urchin:drawUrchin, whelk:drawWhelk, starfish:drawStar,
               sculpin:drawSculpin, kelp:drawKelp, baitbag:drawBag };

  function render(kind, opts){
    opts=opts||{}; const v=opts.variant||0, S=opts.scale||1;
    const b=buf(); (DRAW[kind]||drawUrchin)(b, v, S);
    const out=new Uint8ClampedArray(W*H*4);
    const h2=(h)=>[parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)];
    for(let i=0;i<W*H;i++){
      const r=b.c[i]; if(!r){ out[i*4+3]=0; continue; }
      const [rr,gg,bb]=h2(RAMP[r][Math.max(0,Math.min(4,b.s[i]))]);
      out[i*4]=rr; out[i*4+1]=gg; out[i*4+2]=bb; out[i*4+3]=255;
    }
    return out;
  }
  root.TrapFauna = { W,H,pivot:{x:PX,y:PY}, KINDS, RAMP, render };

  // ---- TrapCatch: the CatchKit-shaped façade the containers call -------------
  const JONAH=[0.86,0.72,1.02];                     // jonah = rock crab, cooler & bigger
  const cache={};
  function toCanvas(rgba,w,h){
    const cv=document.createElement('canvas'); cv.width=w; cv.height=h;
    cv.getContext('2d').putImageData(new ImageData(rgba,w,h),0,0); return cv;
  }
  function item(kind, opts){
    opts=opts||{}; const v=opts.variant||0, sc=opts.scale||1, sp=Math.max(0,Math.min(1,opts.spoil||0));
    const K=root.CatchKit;
    if (kind==='jonah' || kind==='short'){
      const CR=root.Crustacean; if(!CR) return null;
      const k='J|'+kind+v+sc.toFixed(2)+sp.toFixed(2);
      if (cache[k]) return cache[k];
      let rgba=CR.render(kind==='jonah'?'crab':'lobster',
        { pose:'walk', frame:v%4, ang:[0.5,2.3,3.8,5.4][v%4], scale:sc*(kind==='jonah'?1.22:0.62) });
      if (kind==='jonah'){                            // shift the rust carapace cool + dark
        rgba=new Uint8ClampedArray(rgba);
        for(let i=0;i<rgba.length;i+=4){ if(!rgba[i+3])continue;
          rgba[i]=Math.round(rgba[i]*JONAH[0]); rgba[i+1]=Math.round(rgba[i+1]*JONAH[1]);
          rgba[i+2]=Math.round(Math.min(255,rgba[i+2]*JONAH[2]+16)); }
      }
      if (sp && K) rgba=K.tintSpoil(rgba,CR.W,CR.H,sp);
      return (cache[k]={ canvas:toCanvas(rgba,CR.W,CR.H), w:CR.W, h:CR.H, ax:CR.pivot.x, ay:CR.pivot.y });
    }
    if (KINDS.indexOf(kind)>=0){
      const k='F|'+kind+v+sc.toFixed(2)+sp.toFixed(2);
      if (cache[k]) return cache[k];
      let rgba=render(kind,{variant:v,scale:sc});
      if (sp && K) rgba=K.tintSpoil(rgba,W,H,sp);
      return (cache[k]={ canvas:toCanvas(rgba,W,H), w:W, h:H, ax:PX, ay:PY });
    }
    return K ? K.item(kind,opts) : null;
  }
  // what each sort of hauled trap comes up holding. weights, not counts.
  const MIXES = {
    lobster:  [['lobster',7],['short',2],['crab',1],['kelp',1],['urchin',1]],
    keepers:  [['lobster',10]],
    crab:     [['crab',6],['jonah',3],['whelk',1],['starfish',1],['kelp',1]],
    jonah:    [['jonah',8],['crab',2],['urchin',1]],
    mixed:    [['lobster',4],['crab',3],['jonah',2],['short',2],['sculpin',1],['urchin',1],['whelk',1],['starfish',1],['kelp',2]],
    bycatch:  [['sculpin',3],['urchin',3],['whelk',3],['starfish',3],['kelp',3],['crab',1]],
    shorts:   [['short',7],['crab',1],['kelp',1]],
    baited:   [['baitbag',1]],
  };
  const MIX_ORDER=['lobster','keepers','crab','jonah','mixed','bycatch','shorts','baited'];
  function pick(pool,r){ const tot=pool.reduce((a,p)=>a+p[1],0); let t=r*tot;
    for(const p of pool){ t-=p[1]; if(t<=0) return p[0]; } return pool[pool.length-1][0]; }
  const FRAC={ empty:0, few:0.25, half:0.55, full:0.85, brim:1 };
  // seeded + monotonic, exactly like CatchKit: growing a fill never moves earlier items.
  // Scales are TRAP scale, not icon scale — a 0.35 m keeper across a 0.54 m pot beam is
  // ~11 px, so the crustacean plotter is driven down near a third of its catch-icon size.
  function fillItems(mix, fill, seed, count){
    const pool=MIXES[mix]||MIXES.mixed, frac=FRAC[fill]!=null?FRAC[fill]:0;
    if (mix==='baited') return fill==='empty'?[]:[{kind:'baitbag',variant:0,scale:0.5}];
    const n=Math.round(frac*(count!=null?count:10));
    const rng=root.IsoSolid ? root.IsoSolid.mulberry((seed||7)*2654435761) : Math.random;
    const out=[];
    for(let i=0;i<n;i++){
      const kind=pick(pool,rng());
      const small=KINDS.indexOf(kind)>=0;
      out.push({ kind, variant:Math.floor(rng()*4),
                 scale: small ? 0.44+rng()*0.16 : (kind==='short'?0.52:0.40)+rng()*0.09 });
    }
    return out;
  }
  root.TrapCatch = { MIXES, MIX_ORDER, FRAC, item, fillItems, FAUNA:KINDS };
})(typeof globalThis!=='undefined'?globalThis:window);
