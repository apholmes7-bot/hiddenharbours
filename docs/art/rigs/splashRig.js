/* Hidden Harbours — parametric SURFACE-BURST SPLASH (haul-break FX).
   Fires when a haul / catch breaks the surface. ¾ read: a foam crown erupts upward,
   droplets arc out, an expanding foam ring + ripple settle it. 32 px = 1 m · no AA ·
   transparent PNG · upper-left key light. Water palette (DepthRamp teal + foam white),
   thin dark-teal rim only on the outer silhouette — nothing new invented.

   • 8 frames × 48×48, pivot = surface point bottom-centre (24, 34).
     0 dome bulge → 1 column up → 2 peak crown + first droplets → 3 crown splits →
     4 droplets arc out, ring widens → 5 falling → 6 ripple rings → 7 last flecks.
   → SplashBurst.png (384×48).

   Exposes globalThis.SplashBurst with:
     W, H, PAL, FRAME_COUNT, pivot, render(i) / renderT(t) -> Uint8ClampedArray(W*H*4)
   Works in the run_script sandbox (bake) and in the browser (live preview). */
(function (root) {
  const W = 48, H = 48, FRAMES = 8, PX = 24, PY = 34;   // pivot = surface point

  const HEX = {
    out:'#16302d',
    foHi:'#f4faf8', fo:'#d8e7e4', foSh:'#a8c2bf',        // foam white ramp
    waHi:'#6fa09b', wa:'#4a7d78', waSh:'#305a56', waDp:'#1d3c39', // water teal ramp
  };
  const MAT = {
    FOAM:{ mid:'fo', hi:'foHi', sh:'foSh', dp:'foSh' },
    WATER:{ mid:'wa', hi:'waHi', sh:'waSh', dp:'waDp' },
  };

  function newBuf(){ return { key:new Array(W*H).fill(''), mat:new Array(W*H).fill(null) }; }
  const idx=(x,y)=>y*W+x, inb=(x,y)=>x>=0&&x<W&&y>=0&&y<H;
  function put(b,x,y,m){ x=Math.round(x); y=Math.round(y); if(!inb(x,y))return; b.key[idx(x,y)]='mid'; b.mat[idx(x,y)]=m; }
  function putK(b,x,y,m,k){ x=Math.round(x); y=Math.round(y); if(!inb(x,y))return; b.key[idx(x,y)]=k; b.mat[idx(x,y)]=m; }
  function ellipse(b,cx,cy,rx,ry,m,k){
    for(let y=Math.floor(cy-ry);y<=Math.ceil(cy+ry);y++)for(let x=Math.floor(cx-rx);x<=Math.ceil(cx+rx);x++){
      const dx=(x-cx)/(rx+0.001),dy=(y-cy)/(ry+0.001); if(dx*dx+dy*dy<=1){ k?putK(b,x,y,m,k):put(b,x,y,m); } }
  }
  function ring(b,cx,cy,rx,ry,m,k){
    // 1px-thick elliptical ring outline
    for(let a=0;a<Math.PI*2;a+=0.08){ putK(b, cx+Math.cos(a)*rx, cy+Math.sin(a)*ry, m, k||'mid'); }
  }
  function taper(b,x0,y0,x1,y1,r0,r1,m){
    const minx=Math.floor(Math.min(x0,x1)-Math.max(r0,r1)), maxx=Math.ceil(Math.max(x0,x1)+Math.max(r0,r1));
    const miny=Math.floor(Math.min(y0,y1)-Math.max(r0,r1)), maxy=Math.ceil(Math.max(y0,y1)+Math.max(r0,r1));
    const dx=x1-x0, dy=y1-y0, L2=dx*dx+dy*dy||1;
    for(let y=miny;y<=maxy;y++)for(let x=minx;x<=maxx;x++){
      let t=((x-x0)*dx+(y-y0)*dy)/L2; t=Math.max(0,Math.min(1,t));
      const px=x0+dx*t, py=y0+dy*t; if(Math.hypot(x-px,y-py)<=r0+(r1-r0)*t) put(b,x,y,m); }
  }
  const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));

  // deterministic droplet field: fixed angles/speeds so frames are consistent
  const DROPS = [
    {ang:-1.9, sp:15}, {ang:-1.4, sp:19}, {ang:-1.1, sp:13}, {ang:-0.7, sp:17},
    {ang:-2.5, sp:14}, {ang:-2.9, sp:12}, {ang:-0.35, sp:11}, {ang:-1.6, sp:22},
    {ang:-2.2, sp:18}, {ang:-0.55, sp:20}, {ang:-1.25, sp:24}, {ang:-2.7, sp:16},
  ];

  function drawSplash(b, t){
    // ---- foam ring at the surface (expands & thins) ----
    if(t>0.05 && t<0.98){
      const rr = 4 + t*17, ryy = 1.6 + t*5.2;
      const k = t<0.4 ? 'hi' : 'mid';
      ring(b, PX, PY, rr, ryy, 'FOAM', k);
      if(t<0.55) ring(b, PX, PY, rr-1.4, ryy-0.7, 'FOAM','mid');   // thicker early
    }
    // faint second ripple ring, later
    if(t>0.5){ ring(b, PX, PY, 6 + (t-0.5)*30, 2 + (t-0.5)*9, 'WATER','hi'); }

    // ---- central crown column (rises then falls) ----
    const crownT = clamp(t/0.62, 0, 1);            // crown lives in the first ~62%
    const h = Math.sin(crownT*Math.PI) * 24;       // 0 → 24 → 0
    if(h > 1.5){
      const topY = PY - h;
      const baseW = clamp(6 - t*3, 2.2, 6);
      if(t < 0.34){
        // single rising spout, foam-capped teardrop
        taper(b, PX, PY, PX, topY, baseW, 1.6, 'WATER');
        ellipse(b, PX, topY, 2.6, 2.4, 'FOAM');    // foam cap
        ellipse(b, PX, topY-1, 1.4, 1.4, 'FOAM','hi');
      } else {
        // crown splits into 2–3 diverging foam prongs
        const spread = (t-0.34)*30;
        for(const s of [-1, 0, 1]){
          const tx = PX + s*spread, ty = topY - Math.abs(s)*2;
          taper(b, PX, PY, tx, ty, baseW*0.6, 1.3, 'WATER');
          ellipse(b, tx, ty, 1.9, 1.9, 'FOAM');
          putK(b, Math.round(tx), Math.round(ty-1), 'FOAM','hi');
        }
        // wet water core still visible at the base
        taper(b, PX, PY, PX, topY+3, baseW, 2.0, 'WATER');
      }
    }

    // ---- base foam mound (brightest early, fades) ----
    if(t < 0.7){
      const mr = 5 + t*4;
      ellipse(b, PX, PY-1, mr, 2.6, 'FOAM', t<0.3?'hi':'mid');
      ellipse(b, PX-2, PY-2, 2.4, 1.6, 'FOAM','hi');    // upper-left glint
    }

    // ---- flying droplets (arc out under gravity) ----
    if(t > 0.14){
      const g = 42;
      DROPS.forEach((d,i)=>{
        const life = t - (0.14 + (i%3)*0.05);
        if(life <= 0 || life > 0.8) return;
        const vx = Math.cos(d.ang)*d.sp, vy = Math.sin(d.ang)*d.sp;
        const x = PX + vx*life;
        const y = PY + vy*life + 0.5*g*life*life;
        if(y > PY+1) return;                 // gone once it drops back to the surface
        const big = d.sp>15 && life<0.45;
        ellipse(b, x, y, big?2.0:1.3, big?2.0:1.3, 'FOAM', 'hi');
        putK(b, Math.round(x), Math.round(y+1), 'FOAM','mid');     // short trailing streak
        if(big) putK(b, Math.round(x), Math.round(y+2), 'FOAM','sh');
      });
    }
  }

  // ---- shade / outline / colourise (upper-left key) ------------------------
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
  function colourOf(m,k){ if(m==='__out'||k==='out') return HEX.out; const mm=MAT[m]; if(!mm)return HEX.out;
    const nm=k==='hi'?mm.hi:k==='sh'?mm.sh:k==='dp'?(mm.dp||mm.sh):mm.mid; return HEX[nm]; }
  function toRGBA(b){
    const out=new Uint8ClampedArray(W*H*4);
    for(let i=0;i<W*H;i++){ const k=b.key[i]; if(!k){ out[i*4+3]=0; continue; }
      const [r,g,bl]=hex2rgb(colourOf(b.mat[i],k)); out[i*4]=r; out[i*4+1]=g; out[i*4+2]=bl; out[i*4+3]=255; }
    return out;
  }

  function renderT(t){ const b=newBuf(); drawSplash(b, t); shade(b); outline(b); return toRGBA(b); }
  function render(i){ return renderT(i/(FRAMES-1)); }

  root.SplashBurst = { W, H, PAL:HEX, FRAME_COUNT:FRAMES, pivot:{x:PX,y:PY}, render, renderT };
})(typeof globalThis!=='undefined'?globalThis:window);
