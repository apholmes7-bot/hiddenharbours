/* Hidden Harbours — parametric ROCK CRAB (Cancer irroratus, the reef/rock crab catch).
   Top-down ¾ read to match the fish catch icons (Lobster/Cod/Clam). 32px = 1m.
   Single implied key light = upper-LEFT. No AA. 1px #171a14 keyline. KTC palette only
   (rust carapace from the GreywickHouseRed ramp warmed toward the Wood/Earth ramp; bone
   cream from the Wood/Earth highlight — nothing new invented).

   Two products off one crab:
     • ICON  — 48x32, centre pivot (24,16). Static catch/inventory sprite. → RockCrab.png
     • DECK  — 8 frames x 48x48, bottom-centre pivot (24,46). On-deck behaviour:
               frames 0–5 sideways scuttle/flop cycle (loops), 6 rear (mid claw-raise),
               7 defend (both claws up & open — the threat hold). → RockCrabDeck.png

   Exposes globalThis.RockCrab with:
     PAL, ICON_W, ICON_H, W, H, FRAMES, FRAME_COUNT, CYCLE,
     renderIcon() -> Uint8ClampedArray(ICON_W*ICON_H*4)
     poseFor(name) -> pose ; renderDeck(name)/renderPose(pose) -> Uint8ClampedArray(W*H*4)
   Works in the run_script sandbox (bake) and in the browser (live preview). */
(function (root) {
  const W = 48, H = 48, ICON_W = 48, ICON_H = 32;

  // ---- KTC palette ----------------------------------------------------------
  const HEX = {
    out:'#171a14',
    // carapace / claws — warm rust ramp (red ⇄ wood)
    shl:'#b25e3e', shlHi:'#cf7a52', shlSh:'#8a4530', shlDp:'#5f2c20',
    // bone cream — claw tips, leg tips, mouth underside
    crm:'#cdb890', crmHi:'#ece0c8', crmSh:'#9c7f57',
    // walking legs — deeper rust so they read off the shell
    leg:'#8f4630', legHi:'#ad5a3c', legSh:'#5a281d',
    // eyes / detail
    eye:'#241512',
  };
  const MAT = {
    SHELL:{ mid:'shl', hi:'shlHi', sh:'shlSh', dp:'shlDp' },
    CLAW: { mid:'shl', hi:'shlHi', sh:'shlSh', dp:'shlDp' },
    CREAM:{ mid:'crm', hi:'crmHi', sh:'crmSh' },
    LEG:  { mid:'leg', hi:'legHi', sh:'legSh' },
  };

  // ---- buffers --------------------------------------------------------------
  function newBuf(w,h){ return { w, h, key:new Array(w*h).fill(''), mat:new Array(w*h).fill(null) }; }
  const idx = (b,x,y)=> y*b.w+x;
  const inb = (b,x,y)=> x>=0&&x<b.w&&y>=0&&y<b.h;
  function put(b,x,y,mat){ x=Math.round(x); y=Math.round(y); if(!inb(b,x,y))return; b.key[idx(b,x,y)]='mid'; b.mat[idx(b,x,y)]=mat; }
  function putKey(b,x,y,mat,k){ x=Math.round(x); y=Math.round(y); if(!inb(b,x,y))return; b.key[idx(b,x,y)]=k; b.mat[idx(b,x,y)]=mat; }

  function ellipse(b,cx,cy,rx,ry,mat){
    for(let y=Math.floor(cy-ry);y<=Math.ceil(cy+ry);y++)
      for(let x=Math.floor(cx-rx);x<=Math.ceil(cx+rx);x++){
        const dx=(x-cx)/(rx+0.001), dy=(y-cy)/(ry+0.001);
        if(dx*dx+dy*dy<=1) put(b,x,y,mat);
      }
  }
  // tapered capsule: radius r0 at (x0,y0) → r1 at (x1,y1)
  function taper(b,x0,y0,x1,y1,r0,r1,mat){
    const minx=Math.floor(Math.min(x0,x1)-Math.max(r0,r1)), maxx=Math.ceil(Math.max(x0,x1)+Math.max(r0,r1));
    const miny=Math.floor(Math.min(y0,y1)-Math.max(r0,r1)), maxy=Math.ceil(Math.max(y0,y1)+Math.max(r0,r1));
    const dx=x1-x0, dy=y1-y0, L2=dx*dx+dy*dy||1;
    for(let y=miny;y<=maxy;y++)for(let x=minx;x<=maxx;x++){
      let t=((x-x0)*dx+(y-y0)*dy)/L2; t=Math.max(0,Math.min(1,t));
      const px=x0+dx*t, py=y0+dy*t, d=Math.hypot(x-px,y-py);
      if(d<=r0+(r1-r0)*t) put(b,x,y,mat);
    }
  }
  const capsule=(b,x0,y0,x1,y1,r,mat)=>taper(b,x0,y0,x1,y1,r,r,mat);

  // contact shadow — darken already-opaque pixels near a segment so overlaps read
  function contact(b,x0,y0,x1,y1,r){
    const minx=Math.floor(Math.min(x0,x1)-r), maxx=Math.ceil(Math.max(x0,x1)+r);
    const miny=Math.floor(Math.min(y0,y1)-r), maxy=Math.ceil(Math.max(y0,y1)+r);
    const dx=x1-x0, dy=y1-y0, L2=dx*dx+dy*dy||1;
    for(let y=miny;y<=maxy;y++)for(let x=minx;x<=maxx;x++){
      if(!inb(b,x,y))continue; const i=idx(b,x,y); if(!b.key[i])continue;
      let t=((x-x0)*dx+(y-y0)*dy)/L2; t=Math.max(0,Math.min(1,t));
      const px=x0+dx*t, py=y0+dy*t; if(Math.hypot(x-px,y-py)>r) continue;
      const m=b.mat[i]; if(m==='SHELL'||m==='CLAW') b.key[i]= b.key[i]==='sh'?'dp':'sh'; else if(m==='LEG') b.key[i]='sh';
    }
  }

  // ---- crab geometry --------------------------------------------------------
  // Drawn top-down: crab's FRONT = toward the BOTTOM of the frame (claws reach down).
  // cx = midline; cyC = carapace centre. pose = { legPhase, clawUp, breathe }.
  const lerp=(a,b,t)=>a+(b-a)*t;

  function shellHalf(dyRel){
    // half-width of the carapace at dy from centre. Compact fan, widest just forward,
    // leaving the frame's flanks for legs and the front for the claws.
    const ry=8.5, t=Math.max(-1,Math.min(1,dyRel/ry));
    const base=Math.sqrt(Math.max(0,1-t*t))*12.8;
    const flare=(dyRel>0? dyRel/ry:0)*1.5;
    return base+flare;
  }

  function drawLeg(b, rootX, rootY, ang, len, phase, side){
    // two-segment walking leg with a knee kink; foot shuffles with the gait phase
    const sh=Math.sin(phase)*1.5;
    const kx=rootX+Math.cos(ang)*len*0.52, ky=rootY+Math.sin(ang)*len*0.52;
    const nx=-Math.sin(ang), ny=Math.cos(ang);          // perpendicular → knee kink
    const kkx=kx+nx*0.9*side, kky=ky+ny*0.9*side;
    const fx=rootX+Math.cos(ang)*(len+sh);
    const fy=rootY+Math.sin(ang)*(len+sh)+0.6;           // tips settle a touch forward
    contact(b, rootX,rootY, kkx,kky, 2.4);
    taper(b, rootX,rootY, kkx,kky, 2.4, 1.7, 'LEG');     // stout coxa/merus
    taper(b, kkx,kky, fx,fy, 1.6, 0.7, 'LEG');           // pointed dactyl
    putKey(b, Math.round(fx), Math.round(fy), 'CREAM','hi');
  }

  function drawClaw(b, rootX, rootY, side, scale, gape, reach){
    // Top-down cheliped: arm reaches down-out to a pincer pointing down-forward.
    // reach pushes the whole claw forward (down); gape swings the movable finger open.
    const S=v=>side*v;
    const elx=rootX+S(3.5*scale),                        ely=rootY+(4.0+reach*1.4)*scale;
    const px =rootX+S((6.8+reach*1.6)*scale),            py =rootY+(10.0+reach*3.0)*scale;
    const fx =rootX+S((4.6+reach*1.0)*scale),            fy =rootY+(17.5+reach*3.2)*scale;   // fixed (inner) tip
    const mx =rootX+S((10.0+gape*3.4+reach*1.0)*scale),  my =rootY+(15.0-gape*4.0+reach*3.0)*scale; // movable (outer) tip
    contact(b, rootX,rootY, elx,ely, 3.0);
    taper(b, rootX,rootY, elx,ely, 2.2*scale, 2.6*scale, 'CLAW');   // merus/arm into the palm
    ellipse(b, px, py, 3.2*scale, 3.4*scale, 'CLAW');              // manus (palm)
    // two fingers from the palm front, splitting into an open pincer
    taper(b, px-S(0.8), py+2.0, fx, fy, 1.7*scale, 0.5*scale, 'CLAW');  // fixed / pollex (inner)
    taper(b, px+S(1.4), py+0.6, mx, my, 1.4*scale, 0.5*scale, 'CLAW');  // movable / dactyl (outer)
    putKey(b, Math.round(px+S(0.5)), Math.round(py+3.0), 'CLAW','dp');  // dark gape (the open pinch)
    // cream finger tips + upper-left palm glint
    putKey(b, Math.round(fx),Math.round(fy),'CREAM','hi');
    putKey(b, Math.round(mx),Math.round(my),'CREAM','hi');
    putKey(b, Math.round(px-1.6),Math.round(py-1.8),'CREAM','mid');
  }

  function drawCrab(b, cx, cyC, pose){
    const { legPhase=0, gape=0.1, reach=0.25, breathe=0 } = pose;
    cyC += breathe;

    // ---- 8 walking legs (drawn first; carapace covers the roots) ----
    // splay to the sides & rear so the front stays clear for the claws
    for(const side of [-1, 1]){
      for(let i=0;i<4;i++){
        const ry=-5 + i*4.0;
        const rootX=cx + side*(shellHalf(ry)-2.0);
        const rootY=cyC + ry;
        const spread = lerp(0.32, -0.72, i/3);    // front pair a little forward, rears trail back
        const ang = side>0 ? (0 - spread) : (Math.PI + spread);
        const len = lerp(10.5, 8.5, i/3);
        const ph = legPhase + i*0.9 + (side>0?0:Math.PI);
        drawLeg(b, rootX, rootY, ang, len, ph, side);
      }
    }

    // ---- carapace ----
    for(let dy=-9;dy<=9;dy++){
      const hw=shellHalf(dy); if(hw<=0) continue;
      for(let x=Math.round(cx-hw);x<=Math.round(cx+hw);x++) put(b,x,cyC+dy,'SHELL');
    }
    // scalloped front margin
    for(let x=Math.round(cx-10);x<=Math.round(cx+10);x+=3){
      const i=idx(b,x,Math.round(cyC+6.5)); if(b.mat[i]==='SHELL') b.key[i]='sh';
    }
    // dorsal H grooves
    for(let dy=-6;dy<=5;dy++){ const i=idx(b,cx,Math.round(cyC+dy)); if(b.mat[i]==='SHELL') b.key[i]='sh'; }
    for(const gx of [-6,6]) for(let dy=-1;dy<=4;dy++){
      const i=idx(b,Math.round(cx+gx),Math.round(cyC+dy)); if(b.mat[i]==='SHELL') b.key[i]='sh';
    }

    // ---- eyes at the front corners + mouth notch ----
    for(const ex of [-4.5, 4.5]){
      const sx=Math.round(cx+ex), sy=Math.round(cyC+7);
      putKey(b, sx, sy, 'CLAW','out'); putKey(b, sx, sy+1, 'CLAW','out');
      putKey(b, sx-1, sy, 'CREAM','hi');
    }
    putKey(b, cx, Math.round(cyC+8), 'CREAM','sh');

    // ---- claws at the front corners (crab's left = crusher, bigger) ----
    drawClaw(b, cx-7, cyC+5, -1, 1.22, gape, reach);
    drawClaw(b, cx+7, cyC+5, +1, 0.90, gape, reach);
  }

  // ---- poses ----------------------------------------------------------------
  function poseFor(name){
    if(name==='rear')   return { legPhase:0.6, gape:0.55, reach:0.50, breathe:-1 };
    if(name==='defend') return { legPhase:1.5, gape:1.00, reach:1.00, breathe:-2 };
    const m={s0:0,s1:1,s2:2,s3:3,s4:4,s5:5}[name] ?? 0;
    const t=m/6*Math.PI*2;
    return { legPhase:t, gape:0.12+Math.max(0,Math.sin(t))*0.16,
             reach:0.20+Math.max(0,Math.sin(t+1))*0.12, breathe:Math.round(Math.sin(t)*0.9) };
  }
  const FRAMES=['s0','s1','s2','s3','s4','s5','rear','defend'];
  const CYCLE=['s0','s1','s2','s3','s4','s5'];

  // ---- shade / outline / colourise (upper-left key) -------------------------
  function shade(b){
    const src=b.key.slice(), mat=b.mat;
    for(let y=0;y<b.h;y++)for(let x=0;x<b.w;x++){
      const i=idx(b,x,y); if(src[i]!=='mid') continue; const m=mat[i];
      const up = y>0        && src[idx(b,x,y-1)] && mat[idx(b,x,y-1)]===m;
      const lf = x>0        && src[idx(b,x-1,y)] && mat[idx(b,x-1,y)]===m;
      const dn = y<b.h-1    && src[idx(b,x,y+1)] && mat[idx(b,x,y+1)]===m;
      const rt = x<b.w-1    && src[idx(b,x+1,y)] && mat[idx(b,x+1,y)]===m;
      if(!up||!lf) b.key[i]='hi';
      else if(!dn||!rt) b.key[i]='sh';
    }
  }
  function outline(b){
    const add=[];
    for(let y=0;y<b.h;y++)for(let x=0;x<b.w;x++){
      if(b.key[idx(b,x,y)]) continue;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1],[1,1],[-1,1],[1,-1],[-1,-1]]){
        if(inb(b,x+dx,y+dy)&&b.key[idx(b,x+dx,y+dy)]&&b.key[idx(b,x+dx,y+dy)]!=='__o'){
          // 4-neighbour for cardinal, keep diagonals thin: only add on cardinal touch
          if(dx===0||dy===0){ add.push([x,y]); break; }
        }
      }
    }
    for(const [x,y] of add){ b.key[idx(b,x,y)]='__o'; b.mat[idx(b,x,y)]='__out'; }
  }
  function hex2rgb(h){ return [parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]; }
  function colourOf(mat,k){
    if(k==='out') return HEX.eye;
    if(mat==='__out'||k==='__o') return HEX.out;
    const m=MAT[mat]; if(!m) return HEX.out;
    const nm = k==='hi'?m.hi : k==='sh'?m.sh : k==='dp'?(m.dp||m.sh) : m.mid;
    return HEX[nm];
  }
  function toRGBA(b){
    const out=new Uint8ClampedArray(b.w*b.h*4);
    for(let i=0;i<b.w*b.h;i++){
      const k=b.key[i]; if(!k){ out[i*4+3]=0; continue; }
      const [r,g,bl]=hex2rgb(colourOf(b.mat[i],k));
      out[i*4]=r; out[i*4+1]=g; out[i*4+2]=bl; out[i*4+3]=255;
    }
    return out;
  }

  // domed interior shading — a single upper-left light across the whole body,
  // quantised into the 4-step ramp (rim pass then cleans the edges).
  function domeShade(b, cx, cyC){
    for(let y=0;y<b.h;y++)for(let x=0;x<b.w;x++){
      const i=idx(b,x,y), m=b.mat[i];
      if((m!=='SHELL'&&m!=='CLAW')||b.key[i]!=='mid') continue;
      const Lv=-((x-cx)*0.6+(y-cyC)*0.8);
      if(m==='CLAW') b.key[i]= Lv>3?'hi': Lv>-9?'mid':'sh';   // claws stay brighter (no deep dp)
      else b.key[i]= Lv>7?'hi': Lv>-3?'mid': Lv>-12?'sh':'dp';
    }
  }

  function renderPose(pose){
    const b=newBuf(W,H);
    drawCrab(b, 24, 22, pose);   // bottom-centre pivot (24,44)
    domeShade(b, 24, 22); shade(b); outline(b);
    return toRGBA(b);
  }
  function renderDeck(name){ return renderPose(poseFor(name)); }

  function renderIcon(){
    const b=newBuf(ICON_W,ICON_H);
    drawCrab(b, 24, 12, { legPhase:0.0, gape:0.08, reach:0.0, breathe:0 });  // centre pivot (24,16)
    domeShade(b, 24, 12); shade(b); outline(b);
    return toRGBA(b);
  }

  root.RockCrab = {
    W, H, ICON_W, ICON_H, PAL:HEX, FRAMES, FRAME_COUNT:FRAMES.length, CYCLE,
    poseFor, renderPose, renderDeck, renderIcon,
  };
})(typeof globalThis!=='undefined'?globalThis:window);
