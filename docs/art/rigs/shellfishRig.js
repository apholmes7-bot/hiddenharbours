/* Hidden Harbours — SHELLFISH items (blue mussel + soft-shell clam), catch-handling pass.
   Diegetic catch: these are the ACTUAL items that fill buckets / trays / totes and sit in
   the player's hand — no icons. Tones from the existing catch palette (bucket rig SHELLF /
   CREAM ramps). 32 px = 1 m · no AA · upper-left key · 1px #131b1e keyline.
   ITEM — 14×12 cell, pivot (7,10) = ground contact. 4 lay variants per kind (rotations).
   HANDFUL — 22×16 cell, pivot (11,8) = THE GRIP: a clutch of 3, pins to a CharacterIso
   hand anchor (one handful per hand).
   Exposes globalThis.Shellfish = { KINDS, IW,IH,ipivot, HW,HH,hpivot, VARIANTS,
   renderItem(kind,variant), renderHandful(kind,variant) -> Uint8ClampedArray. */
(function (root) {
  const IW=14, IH=12, HW=22, HH=16;
  const KEY='#131b1e';
  const PAL = {
    mussel: { mid:'#293747', hi:'#3d5064', sh:'#1b2430', dp:'#10151a', acc:'#c2d6da' },  // sheen glint
    clam:   { mid:'#c9b083', hi:'#e7d8b0', sh:'#9c7f57', dp:'#8a6a48', acc:'#8a6a48' },  // growth ring
  };
  const KINDS=['mussel','clam'], VARIANTS=4;

  function newBuf(w,h){ return { w,h, key:new Array(w*h).fill(''), mat:new Array(w*h).fill(null) }; }
  const idx=(b,x,y)=>y*b.w+x, inb=(b,x,y)=>x>=0&&x<b.w&&y>=0&&y<b.h;
  function put(b,x,y,m,k){ x=Math.round(x); y=Math.round(y); if(!inb(b,x,y))return; b.key[idx(b,x,y)]=k||'mid'; b.mat[idx(b,x,y)]=m; }
  function taper(b,x0,y0,x1,y1,r0,r1,m){
    const dx=x1-x0,dy=y1-y0,L2=dx*dx+dy*dy||1;
    const minx=Math.floor(Math.min(x0,x1)-Math.max(r0,r1)),maxx=Math.ceil(Math.max(x0,x1)+Math.max(r0,r1));
    const miny=Math.floor(Math.min(y0,y1)-Math.max(r0,r1)),maxy=Math.ceil(Math.max(y0,y1)+Math.max(r0,r1));
    for(let y=miny;y<=maxy;y++)for(let x=minx;x<=maxx;x++){
      let t=((x-x0)*dx+(y-y0)*dy)/L2; t=Math.max(0,Math.min(1,t));
      if(Math.hypot(x-(x0+dx*t),y-(y0+dy*t))<=r0+(r1-r0)*t) put(b,x,y,m); }
  }
  function ellipse(b,cx,cy,rx,ry,ang,m){
    const ca=Math.cos(ang),sa=Math.sin(ang);
    for(let y=Math.floor(cy-rx-ry);y<=Math.ceil(cy+rx+ry);y++)for(let x=Math.floor(cx-rx-ry);x<=Math.ceil(cx+rx+ry);x++){
      const u=(x-cx)*ca+(y-cy)*sa, v=-(x-cx)*sa+(y-cy)*ca;
      if((u*u)/(rx*rx)+(v*v)/(ry*ry)<=1) put(b,x,y,m); }
  }
  function shade(b,cx,cy){
    for(let y=0;y<b.h;y++)for(let x=0;x<b.w;x++){
      const i=idx(b,x,y); if(b.key[i]!=='mid'||!b.mat[i]) continue;
      const Lv=-((x-cx)*0.7+(y-cy)*0.7);
      b.key[i]= Lv>2.2?'hi': Lv>-2.2?'mid':'sh';
    }
  }
  function outline(b){
    const add=[];
    for(let y=0;y<b.h;y++)for(let x=0;x<b.w;x++){ if(b.key[idx(b,x,y)])continue;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]) if(inb(b,x+dx,y+dy)&&b.key[idx(b,x+dx,y+dy)]&&b.mat[idx(b,x+dx,y+dy)]!=='__o'){ add.push([x,y]); break; } }
    for(const [x,y] of add){ b.key[idx(b,x,y)]='out'; b.mat[idx(b,x,y)]='__o'; }
  }
  const hex2=(h)=>[parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)];
  function toRGBA(b,P){
    const out=new Uint8ClampedArray(b.w*b.h*4);
    for(let i=0;i<b.w*b.h;i++){ const k=b.key[i]; if(!k){ out[i*4+3]=0; continue; }
      let hex;
      if(b.mat[i]==='__o'||k==='out') hex=KEY;
      else if(b.mat[i]==='acc') hex=P.acc;
      else hex= k==='hi'?P.hi : k==='sh'?P.sh : k==='dp'?P.dp : P.mid;
      const [r,g,bl]=hex2(hex); out[i*4]=r; out[i*4+1]=g; out[i*4+2]=bl; out[i*4+3]=255; }
    return out;
  }
  // one shell drawn into any buffer at (cx,cy), rotation ang
  function drawShell(b, kind, cx, cy, ang){
    if(kind==='mussel'){
      // pointed teardrop: hinge tip -> broad butt
      const ca=Math.cos(ang),sa=Math.sin(ang);
      taper(b, cx-3.4*ca, cy-3.4*sa*0.7, cx+3.0*ca, cy+3.0*sa*0.7+1, 0.9, 2.5, 'sh1');
      put(b, cx-3.4*ca, cy-3.4*sa*0.7, 'sh1','dp');                    // dark hinge tip
      put(b, cx-1.2*ca+0.8, cy-1.2*sa-0.8, 'acc');                     // sheen glint
    } else {
      ellipse(b, cx, cy, 3.6, 2.5, ang*0.5, 'sh1');
      // growth arcs (lower-right) + hinge pip
      put(b, cx+1.6, cy+1.2, 'acc'); put(b, cx+2.4, cy+0.4, 'acc'); put(b, cx+0.6, cy+1.8, 'acc');
      put(b, cx-2.2, cy-1.4, 'sh1','dp');
    }
  }
  function renderItem(kind, variant){
    const b=newBuf(IW,IH), v=(variant||0)%VARIANTS;
    const ang=[0.3,1.1,-0.5,2.2][v];
    drawShell(b, kind, 7, 6, ang);
    shade(b,7,6); outline(b);
    return toRGBA(b, PAL[kind]||PAL.mussel);
  }
  function renderHandful(kind, variant){
    const b=newBuf(HW,HH), v=(variant||0)%2;
    const off=v?1:-1;
    drawShell(b, kind, 7,  9, 0.5*off);
    drawShell(b, kind, 14, 8, -0.9*off);
    drawShell(b, kind, 11, 12, 1.6);
    shade(b,10,9); outline(b);
    return toRGBA(b, PAL[kind]||PAL.mussel);
  }
  root.Shellfish = { KINDS, VARIANTS, IW, IH, ipivot:{x:7,y:10}, HW, HH, hpivot:{x:11,y:8},
    renderItem, renderHandful };
})(typeof globalThis!=='undefined'?globalThis:window);
