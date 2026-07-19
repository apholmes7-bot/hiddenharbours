/* Hidden Harbours — parametric LOBSTER (Homarus, live-caught). Crab-level detail.
   Horizontal read: claws FORWARD (left, -x), tail fan aft (right, +x). 32px = 1m.
   Single implied key light = upper-LEFT. No AA. 1px #141a18 keyline. KTC palette:
   live-lobster slate-teal carapace (cool-sea slate ramp) with a rust warm accent
   (antennae, mottle, leg tips, tail edge) off the GreywickHouseRed line, bone-cream
   joints/underside. Reads clearly apart from the rust rock crab.

   Two products off one lobster (mirrors the crab kit):
     • ICON  — 48x32, centre pivot (24,16). Static catch/inventory sprite. → Lobster.png
     • DECK  — 8 frames x 48x48, centre pivot (24,24). On-deck behaviour:
               frames 0–5 tail-flip / crawl cycle (loops), 6 rear (claws half-up),
               7 defend (both claws raised & gaping, tail cocked). → LobsterDeck.png

   Exposes globalThis.Lobster with:
     PAL, ICON_W, ICON_H, W, H, FRAMES, FRAME_COUNT, CYCLE,
     renderIcon() -> Uint8ClampedArray(ICON_W*ICON_H*4)
     poseFor(name) -> pose ; renderDeck(name)/renderPose(pose) -> Uint8ClampedArray(W*H*4)
   Works in the run_script sandbox (bake) and in the browser (live preview). */
(function (root) {
  const W = 48, H = 48, ICON_W = 48, ICON_H = 32;

  // ---- KTC palette ----------------------------------------------------------
  const HEX = {
    out:'#2a0f0b', eye:'#1a0705',
    // carapace / tail — classic lobster red (deeper & more crimson than the rust crab)
    car:'#c33a29', carHi:'#e5604a', carSh:'#8f2519', carDp:'#5c150e',
    // claws — a touch brighter red so they read off the shell
    clw:'#d04434', clwHi:'#f06e55', clwSh:'#9a2b1d', clwDp:'#651710',
    // walking legs — deeper red
    leg:'#a83122', legHi:'#c5493a', legSh:'#6c1b11',
    // warm amber accent — antennae, mottle, leg tips, tail trailing edge (pops on red)
    rst:'#e08a3e', rstHi:'#f4ab62', rstSh:'#a85c22',
    // bone cream — joints, claw tips, underside, tail fan membrane
    crm:'#f0e2c4', crmHi:'#fbf1da', crmSh:'#c2a877',
  };
  const MAT = {
    CARA:{ mid:'car', hi:'carHi', sh:'carSh', dp:'carDp' },
    CLAW:{ mid:'clw', hi:'clwHi', sh:'clwSh', dp:'clwDp' },
    TAIL:{ mid:'car', hi:'carHi', sh:'carSh', dp:'carDp' },
    LEG: { mid:'leg', hi:'legHi', sh:'legSh' },
    RUST:{ mid:'rst', hi:'rstHi', sh:'rstSh' },
    CREAM:{ mid:'crm', hi:'crmHi', sh:'crmSh' },
  };
  const DOMED = { CARA:1, CLAW:1, TAIL:1 };

  // ---- buffers (shared primitive kit) ---------------------------------------
  function newBuf(w,h){ return { w, h, key:new Array(w*h).fill(''), mat:new Array(w*h).fill(null) }; }
  const idx=(b,x,y)=>y*b.w+x, inb=(b,x,y)=>x>=0&&x<b.w&&y>=0&&y<b.h;
  function put(b,x,y,m){ x=Math.round(x); y=Math.round(y); if(!inb(b,x,y))return; b.key[idx(b,x,y)]='mid'; b.mat[idx(b,x,y)]=m; }
  function putK(b,x,y,m,k){ x=Math.round(x); y=Math.round(y); if(!inb(b,x,y))return; b.key[idx(b,x,y)]=k; b.mat[idx(b,x,y)]=m; }
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
  function contact(b,x0,y0,x1,y1,r){
    const minx=Math.floor(Math.min(x0,x1)-r), maxx=Math.ceil(Math.max(x0,x1)+r);
    const miny=Math.floor(Math.min(y0,y1)-r), maxy=Math.ceil(Math.max(y0,y1)+r);
    const dx=x1-x0, dy=y1-y0, L2=dx*dx+dy*dy||1;
    for(let y=miny;y<=maxy;y++)for(let x=minx;x<=maxx;x++){
      if(!inb(b,x,y))continue; const i=idx(b,x,y); if(!b.key[i])continue;
      let t=((x-x0)*dx+(y-y0)*dy)/L2; t=Math.max(0,Math.min(1,t));
      const px=x0+dx*t, py=y0+dy*t; if(Math.hypot(x-px,y-py)>r) continue;
      const m=b.mat[i]; if(DOMED[m]) b.key[i]= b.key[i]==='sh'?'dp':'sh'; else b.key[i]='sh'; }
  }
  const lerp=(a,b,t)=>a+(b-a)*t;

  // ---- lobster geometry (horizontal; claws to -x, tail to +x) ---------------
  function drawLeg(b, rootX, rootY, ang, len, phase){
    const sh=Math.sin(phase)*1.4;
    const kx=rootX+Math.cos(ang)*len*0.5, ky=rootY+Math.sin(ang)*len*0.5;
    const fx=rootX+Math.cos(ang)*(len+sh), fy=rootY+Math.sin(ang)*(len+sh);
    contact(b, rootX,rootY, kx,ky, 2.0);
    taper(b, rootX,rootY, kx,ky, 1.9, 1.3, 'LEG');
    taper(b, kx,ky, fx,fy, 1.3, 0.6, 'LEG');
    putK(b, Math.round(fx), Math.round(fy), 'RUST','hi');   // rusty dactyl tip
  }

  // claw reaches outward along (dirx,diry) on a visible arm; fingers open across the
  // forward direction (gape); lift rotates the whole claw up-forward (the rear/threat).
  function drawClaw(b, rootX, rootY, dirx, diry, scale, gape, lift){
    let dy=diry - lift*0.85, dx=dirx - lift*0.10; const dl=Math.hypot(dx,dy)||1; dx/=dl; dy/=dl;
    const ext=1+lift*0.5;
    const ex = rootX+dx*5.2*scale*ext,  ey = rootY+dy*5.2*scale*ext;      // elbow
    const mcx= rootX+dx*11.2*scale*ext, mcy= rootY+dy*11.2*scale*ext;     // manus centre
    const palmR=4.2*scale;
    const perpx=-dy, perpy=dx, gap=(2.2+gape*4.0)*scale, fwd=5.6*scale;
    const fbx=mcx+dx*fwd, fby=mcy+dy*fwd;                                  // fingers' forward base
    const fx=fbx+perpx*gap, fy=fby+perpy*gap;                              // fixed finger tip
    const mx=fbx-perpx*gap, my=fby-perpy*gap;                              // movable finger tip
    contact(b, rootX,rootY, ex,ey, 3.0);                                  // shadow where arm meets body
    taper(b, rootX,rootY, ex,ey, 2.3*scale, 2.7*scale, 'CLAW');           // merus/arm
    taper(b, ex,ey, mcx,mcy, 2.9*scale, 3.2*scale, 'CLAW');               // carpus into manus
    ellipse(b, mcx, mcy, palmR, palmR+0.4, 'CLAW');                        // big manus (palm)
    taper(b, mcx,mcy, fx,fy, 1.9*scale, 0.6*scale, 'CLAW');                // fixed finger
    taper(b, mcx,mcy, mx,my, 1.6*scale, 0.6*scale, 'CLAW');                // movable finger
    putK(b, Math.round(fbx), Math.round(fby), 'CLAW','dp');               // dark gape (the pinch)
    for(const [tx,ty] of [[fx,fy],[mx,my]]) putK(b, Math.round(tx),Math.round(ty),'CREAM','hi');
    for(const [wx,wy] of [[1.4,1.0],[-0.8,1.8]]) putK(b, Math.round(mcx+wx*scale),Math.round(mcy+wy*scale),'RUST','mid');
    putK(b, Math.round(mcx-1.4),Math.round(mcy-2.4),'CREAM','mid');       // upper-left glint
  }

  function drawTailFan(b, tx, ty, ang, scale){
    // telson (centre) + two uropods each side, spread WIDE perpendicular to the tail axis
    const nx=-Math.sin(ang), ny=Math.cos(ang), ax=Math.cos(ang), ay=Math.sin(ang);
    for(const s of [-2,-1,0,1,2]){
      const rootSpread=2.6*scale;
      const bx=tx+nx*s*rootSpread, by=ty+ny*s*rootSpread;                 // flap root
      const flapLen=(6.0-Math.abs(s)*0.6)*scale;
      const tipx=bx+ax*flapLen+nx*s*2.4*scale, tipy=by+ay*flapLen+ny*s*2.4*scale; // fan outward
      taper(b, bx,by, tipx, tipy, 2.3*scale, 1.2*scale, 'TAIL');
      // cream/rust trailing edge fringe
      putK(b, Math.round(tipx), Math.round(tipy), s===0?'CREAM':'RUST', s===0?'hi':'mid');
      putK(b, Math.round(tipx-ax), Math.round(tipy-ay), 'CREAM','sh');
    }
  }

  function drawLobster(b, cx, cy, pose){
    const { tailCurl=0.2, gape=0.08, lift=0.0, legPhase=0, antPhase=0, breathe=0 } = pose;
    cy += breathe;

    // ---- antennae (behind everything) : two long rust feelers sweeping forward ----
    for(const sgn of [-1, 1]){
      let x=cx-6, y=cy+sgn*2, ang=Math.PI + sgn*0.32 + Math.sin(antPhase+sgn)*0.06;
      for(let s=0;s<15;s++){ putK(b, x, y, 'RUST', s<2?'mid':(s%3?'sh':'mid')); x+=Math.cos(ang)*1.5; y+=Math.sin(ang)*1.5; ang+=sgn*0.03; }
    }
    // short antennules
    for(const sgn of [-1,1]){ let x=cx-6,y=cy+sgn*1; for(let s=0;s<5;s++){ putK(b,x,y,'RUST','sh'); x-=1.4; y+=sgn*0.5; } }

    // ---- legs (drawn before body) : 4 pairs along the thorax, splaying up & down ----
    for(const side of [-1, 1]){          // -1 = upper (-y), +1 = lower (+y)
      for(let i=0;i<4;i++){
        const rx=cx-2 + i*4.2;
        const a = side>0 ? (Math.PI*0.5 + 0.18) : (-Math.PI*0.5 - 0.18);   // fan slightly aft
        const len = lerp(10.5, 8.0, i/3);
        const ph = legPhase + i*0.8 + (side>0?0:Math.PI);
        drawLeg(b, rx, cy+side*4.5, a, len, ph);
      }
    }

    // ---- abdomen: 5 tapering segments from the carapace rear, curling with tailCurl ----
    let sx=cx+8, sy=cy, heading=0.0;   // heading 0 = +x (aft)
    const segLen=2.8;
    let lastAng=heading;
    for(let i=0;i<5;i++){
      const ry=lerp(6.6, 3.2, i/4);
      ellipse(b, sx, sy, 2.7, ry, 'TAIL');
      for(let dy2=-Math.round(ry);dy2<=Math.round(ry);dy2++){ const ii=idx(b,Math.round(sx-2.4),Math.round(sy+dy2)); if(b.mat[ii]==='TAIL') b.key[ii]='sh'; }
      lastAng=heading;
      sx+=Math.cos(heading)*segLen; sy+=Math.sin(heading)*segLen;
      heading += tailCurl*0.48;   // curl downward (+y)
    }
    drawTailFan(b, sx, sy, lastAng, 1.05);

    // ---- cephalothorax (carapace) : elongated dome over the leg/abdomen roots ----
    ellipse(b, cx+2, cy, 9.5, 7.2, 'CARA');
    // rostrum (pointed snout) toward the front
    taper(b, cx-6, cy, cx-11, cy, 2.4, 0.8, 'CARA');
    // dorsal midline groove + two lateral grooves
    for(let dx=-8;dx<=9;dx++){ const i=idx(b,Math.round(cx+2+dx),cy); if(b.mat[i]==='CARA') b.key[i]='sh'; }
    for(const gy of [-4,4]) for(let dx=-4;dx<=6;dx++){ const i=idx(b,Math.round(cx+2+dx),Math.round(cy+gy)); if(b.mat[i]==='CARA') b.key[i]='sh'; }
    // rust mottle speckles
    for(const [mx,my] of [[-2,-3],[3,-2],[5,2],[0,3],[-4,1],[7,-1]]){ const i=idx(b,Math.round(cx+2+mx),Math.round(cy+my)); if(b.mat[i]==='CARA') { b.mat[i]='RUST'; b.key[i]='sh'; } }

    // ---- eyes: two dark beads on short stalks at the front corners ----
    for(const sgn of [-1,1]){
      const exx=Math.round(cx-7), eyy=Math.round(cy+sgn*3);
      putK(b, exx, eyy, 'CARA','sh');
      putK(b, exx-1, eyy, 'CLAW','out');
      putK(b, exx-2, eyy, 'CLAW','out');
    }

    // ---- claws (front-most, biggest) : crusher reaches down-forward, pincer up-forward ----
    drawClaw(b, cx-4, cy+4, -0.90,  0.44, 1.34, gape, lift);   // crusher (bigger)
    drawClaw(b, cx-4, cy-4, -0.90, -0.44, 0.98, gape, lift);   // pincer / seizer
  }

  // ---- poses ----------------------------------------------------------------
  function poseFor(name){
    if(name==='rear')   return { tailCurl:0.16, gape:0.45, lift:0.50, legPhase:0.6, antPhase:0.6, breathe:-1 };
    if(name==='defend') return { tailCurl:0.02, gape:1.00, lift:1.00, legPhase:1.5, antPhase:1.2, breathe:-1 };
    const m={s0:0,s1:1,s2:2,s3:3,s4:4,s5:5}[name] ?? 0;
    const t=m/6*Math.PI*2;
    return { tailCurl:0.34+0.34*Math.sin(t), gape:0.08+Math.max(0,Math.sin(t))*0.14,
             lift:0.05+Math.max(0,Math.sin(t+1))*0.12, legPhase:t, antPhase:t*0.8,
             breathe:Math.round(Math.sin(t)*0.8) };
  }
  const FRAMES=['s0','s1','s2','s3','s4','s5','rear','defend'];
  const CYCLE=['s0','s1','s2','s3','s4','s5'];

  // ---- shade / outline / dome / colourise (upper-left key) ------------------
  function domeShade(b, cx, cy){
    for(let y=0;y<b.h;y++)for(let x=0;x<b.w;x++){
      const i=idx(b,x,y), m=b.mat[i]; if(!DOMED[m]||b.key[i]!=='mid') continue;
      const Lv=-((x-cx)*0.6+(y-cy)*0.8);
      if(m==='CLAW') b.key[i]= Lv>3?'hi': Lv>-9?'mid':'sh';
      else b.key[i]= Lv>7?'hi': Lv>-3?'mid': Lv>-12?'sh':'dp';
    }
  }
  function shade(b){
    const src=b.key.slice(), mat=b.mat;
    for(let y=0;y<b.h;y++)for(let x=0;x<b.w;x++){
      const i=idx(b,x,y); if(src[i]!=='mid') continue; const m=mat[i];
      const up=y>0&&src[idx(b,x,y-1)]&&mat[idx(b,x,y-1)]===m, lf=x>0&&src[idx(b,x-1,y)]&&mat[idx(b,x-1,y)]===m;
      const dn=y<b.h-1&&src[idx(b,x,y+1)]&&mat[idx(b,x,y+1)]===m, rt=x<b.w-1&&src[idx(b,x+1,y)]&&mat[idx(b,x+1,y)]===m;
      if(!up||!lf) b.key[i]='hi'; else if(!dn||!rt) b.key[i]='sh';
    }
  }
  function outline(b){
    const add=[];
    for(let y=0;y<b.h;y++)for(let x=0;x<b.w;x++){ if(b.key[idx(b,x,y)])continue;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]) if(inb(b,x+dx,y+dy)&&b.key[idx(b,x+dx,y+dy)]&&b.mat[idx(b,x+dx,y+dy)]!=='__out'){ add.push([x,y]); break; } }
    for(const [x,y] of add){ b.key[idx(b,x,y)]='out'; b.mat[idx(b,x,y)]='__out'; }
  }
  function hex2rgb(h){ return [parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]; }
  function colourOf(m,k){ if(k==='out') return HEX.eye; if(m==='__out') return HEX.out; const mm=MAT[m]; if(!mm)return HEX.out;
    const nm=k==='hi'?mm.hi:k==='sh'?mm.sh:k==='dp'?(mm.dp||mm.sh):mm.mid; return HEX[nm]; }
  function toRGBA(b){
    const out=new Uint8ClampedArray(b.w*b.h*4);
    for(let i=0;i<b.w*b.h;i++){ const k=b.key[i]; if(!k){ out[i*4+3]=0; continue; }
      const [r,g,bl]=hex2rgb(colourOf(b.mat[i],k)); out[i*4]=r; out[i*4+1]=g; out[i*4+2]=bl; out[i*4+3]=255; }
    return out;
  }

  function renderPose(pose){
    const b=newBuf(W,H);
    drawLobster(b, 24, 24, pose);   // centre pivot (24,24)
    domeShade(b, 22, 22); shade(b); outline(b);
    return toRGBA(b);
  }
  function renderDeck(name){ return renderPose(poseFor(name)); }
  function renderIcon(){
    const b=newBuf(ICON_W,ICON_H);
    drawLobster(b, 24, 16, { tailCurl:0.16, gape:0.10, lift:0.05, legPhase:0, antPhase:0, breathe:0 }); // centre (24,16)
    domeShade(b, 22, 12); shade(b); outline(b);
    return toRGBA(b);
  }

  root.Lobster = { W, H, ICON_W, ICON_H, PAL:HEX, FRAMES, FRAME_COUNT:FRAMES.length, CYCLE,
    poseFor, renderPose, renderDeck, renderIcon };
})(typeof globalThis!=='undefined'?globalThis:window);
