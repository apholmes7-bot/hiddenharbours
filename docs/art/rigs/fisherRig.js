/* Hidden Harbours — parametric pixel fisher, HAUL kit.
   Side profile, facing RIGHT (line/rail to the right). 32x32px = 1m.
   Frame 32 wide x 64 tall. Pivot = bottom-centre (16,64); feet planted near y=62.
   Single implied key light = upper-LEFT. No AA. KTC palette only.

   Exposes globalThis.FisherHaul with:
     PAL, W, H, FRAMES (ordered names), FRAME_COUNT,
     poseFor(name)  -> pose object
     renderRGBA(pose) -> Uint8ClampedArray(W*H*4)   (nearest-neighbour ready)
   Works in the run_script sandbox (bake) and in the browser (live preview). */
(function (root) {
  const W = 32, H = 64;

  // ---- KTC palette (sampled from existing character art) --------------------
  const HEX = {
    out:  '#171a14',      // keyline / darkest
    // oilskin (yellow) ramp
    oilHi:'#f2cf6a', oil:'#e0b13a', oilSh:'#b07d1f', oilDp:'#946218',
    // skin
    skHi: '#e0a981', sk:'#c98d63', skSh:'#9c6843',
    // trousers (navy) — existing char navy + Cool-Sea dark
    navHi:'#4a5d66', nav:'#33424a', navSh:'#16242e',
    // boots (near-black) — Fog-Grey dark + Cool-Sea darkest (master ramps)
    booHi:'#3a4248', boo:'#2a343b', booSh:'#16242e',
    // rope / warp + wood — Wood/Earth master ramp + bone highlight
    ropHi:'#d8d2c0', rop:'#b49a74', ropSh:'#8c6a45',
    // gloves — dark oilskin cuff, tan palm (Wood/Earth)
    gloHi:'#8c6a45', glo:'#6b4f35', gloSh:'#241a14',
  };
  // materials: mid + up-left rim (hi) + down-right rim (sh)
  const MAT = {
    OIL:  { mid:'oil',  hi:'oilHi', sh:'oilSh', dp:'oilDp' },
    SKIN: { mid:'sk',   hi:'skHi',  sh:'skSh' },
    NAVY: { mid:'nav',  hi:'navHi', sh:'navSh' },
    BOOT: { mid:'boo',  hi:'booHi', sh:'booSh' },
    ROPE: { mid:'rop',  hi:'ropHi', sh:'ropSh' },
    GLOVE:{ mid:'glo',  hi:'gloHi', sh:'gloSh' },
  };

  // ---- buffers --------------------------------------------------------------
  // key buffer stores a colour-key string per pixel ('' = transparent).
  // matbuf stores the material name so the light pass can pick the right ramp.
  function newBuf() { return { key:new Array(W*H).fill(''), mat:new Array(W*H).fill(null) }; }
  const idx = (x,y)=> y*W+x;
  const inb = (x,y)=> x>=0&&x<W&&y>=0&&y<H;

  function put(b,x,y,matName){ x=Math.round(x); y=Math.round(y); if(!inb(x,y))return; b.key[idx(x,y)]='mid'; b.mat[idx(x,y)]=matName; }
  function putKey(b,x,y,matName,keyName){ x=Math.round(x); y=Math.round(y); if(!inb(x,y))return; b.key[idx(x,y)]=keyName; b.mat[idx(x,y)]=matName; }

  // filled disc / ellipse
  function ellipse(b,cx,cy,rx,ry,mat){
    for(let y=Math.floor(cy-ry);y<=Math.ceil(cy+ry);y++)
      for(let x=Math.floor(cx-rx);x<=Math.ceil(cx+rx);x++){
        const dx=(x-cx)/(rx+0.001), dy=(y-cy)/(ry+0.001);
        if(dx*dx+dy*dy<=1) put(b,x,y,mat);
      }
  }
  function rect(b,x0,y0,w,h,mat){ for(let y=y0;y<y0+h;y++)for(let x=x0;x<x0+w;x++)put(b,x,y,mat); }
  // thick segment (capsule)
  function capsule(b,x0,y0,x1,y1,r,mat){
    const minx=Math.floor(Math.min(x0,x1)-r), maxx=Math.ceil(Math.max(x0,x1)+r);
    const miny=Math.floor(Math.min(y0,y1)-r), maxy=Math.ceil(Math.max(y0,y1)+r);
    const dx=x1-x0, dy=y1-y0, L2=dx*dx+dy*dy||1;
    for(let y=miny;y<=maxy;y++)for(let x=minx;x<=maxx;x++){
      let t=((x-x0)*dx+(y-y0)*dy)/L2; t=Math.max(0,Math.min(1,t));
      const px=x0+dx*t, py=y0+dy*t, d=Math.hypot(x-px,y-py);
      if(d<=r) put(b,x,y,mat);
    }
  }
  // 1px poly-line (rope) with optional key override
  function polyline(b,pts,mat,keyName){
    for(let i=0;i<pts.length-1;i++){
      let [x0,y0]=pts[i],[x1,y1]=pts[i+1];
      x0=Math.round(x0);y0=Math.round(y0);x1=Math.round(x1);y1=Math.round(y1);
      let dx=Math.abs(x1-x0),dy=Math.abs(y1-y0),sx=x0<x1?1:-1,sy=y0<y1?1:-1,err=dx-dy;
      for(;;){ keyName?putKey(b,x0,y0,mat,keyName):put(b,x0,y0,mat);
        if(x0===x1&&y0===y1)break; const e2=2*err; if(e2>-dy){err-=dy;x0+=sx;} if(e2<dx){err+=dx;y0+=sy;} }
    }
  }

  // contact shadow: darken already-opaque BODY pixels within r of a segment,
  // so an overlapping limb reads against the torso (a 1px halo of separation).
  function contactShadow(b,x0,y0,x1,y1,r){
    const minx=Math.floor(Math.min(x0,x1)-r), maxx=Math.ceil(Math.max(x0,x1)+r);
    const miny=Math.floor(Math.min(y0,y1)-r), maxy=Math.ceil(Math.max(y0,y1)+r);
    const dx=x1-x0, dy=y1-y0, L2=dx*dx+dy*dy||1;
    for(let y=miny;y<=maxy;y++)for(let x=minx;x<=maxx;x++){
      if(!inb(x,y)) continue; const i=idx(x,y); const m=b.mat[i]; if(!b.key[i]) continue;
      let t=((x-x0)*dx+(y-y0)*dy)/L2; t=Math.max(0,Math.min(1,t));
      const px=x0+dx*t, py=y0+dy*t; if(Math.hypot(x-px,y-py)>r) continue;
      if(m==='OIL') b.key[i]='dp'; else if(m==='NAVY'||m==='BOOT') b.key[i]='sh';
    }
  }

  // ---- geometry / IK --------------------------------------------------------
  const lerp=(a,b,t)=>a+(b-a)*t;
  const V=(x,y)=>({x,y});
  function lerpV(a,b,t){ return V(lerp(a.x,b.x,t),lerp(a.y,b.y,t)); }
  const easeIO=t=> t<0.5?2*t*t:1-Math.pow(-2*t+2,2)/2;
  const easeOut=t=>1-(1-t)*(1-t);
  // 2-bone IK: shoulder S, hand Hn, bone lengths l1,l2, bend sign (+1 elbow down/out)
  function elbow(S,Hn,l1,l2,sign){
    let dx=Hn.x-S.x, dy=Hn.y-S.y, d=Math.hypot(dx,dy)||0.001;
    const dc=Math.min(d,l1+l2-0.2);
    const a=(l1*l1-l2*l2+dc*dc)/(2*dc);         // along
    const h=Math.sqrt(Math.max(0,l1*l1-a*a));   // perpendicular
    const ux=dx/d, uy=dy/d, nx=-uy, ny=ux;
    return V(S.x+ux*a+nx*h*sign, S.y+uy*a+ny*h*sign);
  }

  // ---- one full hand grab+return along the haul path ------------------------
  // u:0..1. 0..0.5 = power (grip, low-right -> up across chest). 0.5..1 = recovery (air, back down-right).
  const GRAB = V(24.5, 45.5);   // low by the rail
  const HAUL = V(13.5, 31.5);   // up & across to far hip/chest
  function handPath(u){
    if(u<0.5){ const t=easeIO(u/0.5); return { p:lerpV(GRAB,HAUL,t), grip:true, power:Math.sin((u/0.5)*Math.PI) }; }
    const t=easeIO((u-0.5)/0.5);
    const p=lerpV(HAUL,GRAB,t);
    p.x += Math.sin(t*Math.PI)*2.4;   // swing outboard (right) through the air
    p.y -= Math.sin(t*Math.PI)*2.0;   // lift a touch
    return { p, grip:false, power:0 };
  }

  // ---- pose builder ---------------------------------------------------------
  // A pose = joints in frame pixels + flags. Feet pinned; x pivot 16.
  function baseSkeleton(lean, kneeBend){
    // lean: -1 forward .. +1.4 back(away from rail = leftwards)
    const hipY = 43 - kneeBend*1.6;
    const hip  = V(16 - lean*1.3, hipY);
    const sx   = 16.8 - lean*3.4;               // shoulder swings back(left) on the heave
    const sy   = 28.0 - lean*0.9 + kneeBend*0.7;
    const shoulder = V(sx, sy);
    // head rides the shoulder, faces right; tips back on the heave
    const neck = V(sx+0.4 - lean*0.7, sy-3.6);
    const head = V(sx+1.6 - lean*1.9, sy-7.2);
    return { hip, shoulder, neck, head, lean, kneeBend };
  }

  function cyclePose(phase){
    const A = handPath(phase);
    const B = handPath((phase+0.5)%1);
    const power = Math.max(A.power, B.power);
    const lean = -0.15 + power*1.05;            // pump back on each grip
    const kneeBend = 0.35 + power*0.6;
    const sk = baseSkeleton(lean, kneeBend);
    return Object.assign(sk, { handA:A.p, gripA:A.grip, handB:B.p, gripB:B.grip, rope:'taut' });
  }

  const POSES = {
    // 6-frame hand-over-hand cycle
    c0: ()=>cyclePose(0.00), c1: ()=>cyclePose(0.166), c2: ()=>cyclePose(0.333),
    c3: ()=>cyclePose(0.50), c4: ()=>cyclePose(0.666), c5: ()=>cyclePose(0.833),
    // lean-back strain hold — both fists hauled in low to the hip, body tipped hard back, deep sit
    strain: ()=>{ const sk=baseSkeleton(1.35, 1.15);
      return Object.assign(sk,{ handA:V(15.5,40.5), gripA:true, handB:V(18.5,43.5), gripB:true, rope:'taut', strain:true }); },
    // relaxed ease — near-upright, weight neutral, hands loose on a slack line
    ease: ()=>{ const sk=baseSkeleton(-0.28, 0.1);
      return Object.assign(sk,{ handA:V(22.5,38.5), gripA:true, handB:V(24.5,42.5), gripB:false, rope:'slack' }); },
  };
  const FRAMES = ['c0','c1','c2','c3','c4','c5','strain','ease'];

  // ---- draw a pose ----------------------------------------------------------
  function drawPose(p){
    const b=newBuf();
    const { hip, shoulder, neck, head } = p;

    // ---- FAR leg + boot (drawn first, sits behind) ----
    const backHipX = hip.x-2.3, frontHipX = hip.x+2.4;
    const kneeDrop = 6.5 - p.kneeBend*0.6;
    const backKnee = V(backHipX-0.6, hip.y+kneeDrop);
    const backFoot = V(11.0, 61.5);
    const frontKnee= V(frontHipX+1.2, hip.y+kneeDrop-0.6);
    const frontFoot= V(21.5, 61.5);
    // far (back) leg
    capsule(b, backHipX,hip.y, backKnee.x,backKnee.y, 2.2, 'NAVY');
    capsule(b, backKnee.x,backKnee.y, backFoot.x,backFoot.y-2, 2.1, 'NAVY');
    // far boot
    ellipse(b, backFoot.x, backFoot.y-1, 3.0, 2.6, 'BOOT');
    rect(b, backFoot.x-2, backFoot.y+1, 6, 1, 'BOOT');

    // ---- TORSO (oilskin) — a tall egg leaning with the body ----
    const midX=(hip.x+shoulder.x)/2, midY=(hip.y+shoulder.y)/2;
    ellipse(b, midX+0.8, midY, 6.3, 10.6, 'OIL');          // main body
    ellipse(b, shoulder.x+0.3, shoulder.y+1.5, 5.1, 5.2, 'OIL'); // upper chest / shoulders
    ellipse(b, hip.x+1.2, hip.y-1.0, 5.9, 4.8, 'OIL');     // skirt of the oilskin over hips
    if(p.lean>0.6) ellipse(b, hip.x+3.6, hip.y-0.6, 3.0, 3.4, 'OIL'); // front hem flares on a hard heave
    // waist seam — a darker band splitting jacket from the skirt
    const seamY=Math.round(hip.y-3.2);
    for(let x=Math.round(hip.x)-7;x<=Math.round(hip.x)+7;x++){ if(inb(x,seamY)&&b.mat[idx(x,seamY)]==='OIL') b.key[idx(x,seamY)]='dp'; }

    // ---- FRONT leg + boot (over the oilskin hem) ----
    contactShadow(b, frontHipX,hip.y, frontKnee.x,frontKnee.y, 3.4);   // shade far leg / hem behind it
    capsule(b, frontHipX,hip.y+0.5, frontKnee.x,frontKnee.y, 2.3, 'NAVY');
    capsule(b, frontKnee.x,frontKnee.y, frontFoot.x,frontFoot.y-2, 2.2, 'NAVY');
    ellipse(b, frontFoot.x, frontFoot.y-1, 3.2, 2.7, 'BOOT');
    rect(b, frontFoot.x-2, frontFoot.y+1, 7, 1, 'BOOT');

    // ---- HEAD: skin face + sou'wester ----
    ellipse(b, head.x, head.y+0.3, 3.5, 3.9, 'SKIN');       // face/jaw
    // sou'wester crown
    ellipse(b, head.x-0.2, head.y-2.4, 3.7, 2.9, 'OIL');
    // brim — sweeps to the right & down (facing right), long tail at back-left
    capsule(b, head.x-3.6, head.y-0.4, head.x+4.2, head.y+0.3, 1.5, 'OIL'); // front brim right
    capsule(b, head.x-3.8, head.y-0.6, head.x-5.4, head.y+1.8, 1.4, 'OIL'); // neck flap back-left
    // nose nub (faces right)
    put(b, head.x+3.5, head.y+1, 'SKIN'); putKey(b,head.x+3.5,head.y+1,'SKIN','hi');
    // eye
    const ex=Math.round(head.x+2.1), ey=Math.round(head.y+0.2);
    if(inb(ex,ey)&&b.mat[idx(ex,ey)]==='SKIN'){ b.key[idx(ex,ey)]='out'; b.mat[idx(ex,ey)]='__out'; }

    // ---- ARMS reach to the hands (contact-shadowed so they read off the torso) ----
    const shA = V(shoulder.x+1.8, shoulder.y+0.8);   // near/front shoulder
    const shB = V(shoulder.x-1.2, shoulder.y+1.2);   // far shoulder (slightly back)
    const eB = elbow(shB, p.handB, 6.5, 7.0, +1);
    const eA = elbow(shA, p.handA, 6.8, 7.2, +1);
    // far arm
    contactShadow(b, shB.x,shB.y, eB.x,eB.y, 3.0); contactShadow(b, eB.x,eB.y, p.handB.x,p.handB.y, 2.7);
    capsule(b, shB.x,shB.y, eB.x,eB.y, 2.0, 'OIL');
    capsule(b, eB.x,eB.y, p.handB.x,p.handB.y, 1.7, 'OIL');
    // near arm (on top)
    contactShadow(b, shA.x,shA.y, eA.x,eA.y, 3.2); contactShadow(b, eA.x,eA.y, p.handA.x,p.handA.y, 2.9);
    capsule(b, shA.x,shA.y, eA.x,eA.y, 2.2, 'OIL');
    capsule(b, eA.x,eA.y, p.handA.x,p.handA.y, 1.8, 'OIL');

    // ---- ROPE / warp (under the fists) ----
    drawRope(b, p);
    // ---- FISTS clamp the rope, drawn last ----
    drawHand(b, p.handB, p.gripB);
    drawHand(b, p.handA, p.gripA);

    // ---- shade + outline + colourise ----
    shade(b);
    outline(b);
    return toRGBA(b);
  }

  function drawHand(b, h, grip){
    ellipse(b, h.x, h.y, 1.9, 1.8, 'GLOVE');       // fist
    if(grip){ putKey(b,h.x+1,h.y-1,'GLOVE','hi'); }
  }

  function drawRope(b, p){
    // the hauled warp: comes over the rail at lower-right, up through the lower gripping hand,
    // slack to the other hand, then a small coil already hauled at the feet (lower-left).
    const rail = V(28, 63);                 // exits frame, down to the pot
    // which hand is lower / gripping the working end
    let low=p.handA, hi=p.handB;
    if(p.handB.y>p.handA.y){ low=p.handB; hi=p.handA; }
    if(p.rope==='slack'){
      // drooping bight between hands + down to rail
      polyline(b, bezier(low, V((low.x+rail.x)/2+2, low.y+7), rail, 8), 'ROPE');
      polyline(b, bezier(hi, V((hi.x+low.x)/2, Math.max(hi.y,low.y)+5), low, 6), 'ROPE');
    } else {
      // taut working end from rail up to the low hand, short link to high hand
      polyline(b, [ [rail.x,rail.y],[low.x+1,low.y+1],[low.x,low.y] ], 'ROPE');
      polyline(b, [ [low.x,low.y],[ (low.x+hi.x)/2, (low.y+hi.y)/2 - 1 ],[hi.x,hi.y] ], 'ROPE');
    }
    // hauled coil at the feet (left), always
    polyline(b, [ [8,60],[10,58],[13,59],[11,61],[8,60] ], 'ROPE');
    polyline(b, [ [9,61],[12,60] ], 'ROPE');
  }
  function bezier(a,c,d,n){ const out=[]; for(let i=0;i<=n;i++){ const t=i/n, mt=1-t;
    out.push([ mt*mt*a.x+2*mt*t*c.x+t*t*d.x, mt*mt*a.y+2*mt*t*c.y+t*t*d.y ]); } return out; }

  // ---- light pass: pick hi/mid/sh per material from up-left / down-right rim -
  function shade(b){
    const src=b.key.slice(), mat=b.mat;
    for(let y=0;y<H;y++)for(let x=0;x<W;x++){
      const i=idx(x,y); if(!src[i]) continue; if(src[i]!=='mid') continue; // keep forced keys
      const m=mat[i];
      const up = y>0 && !!src[idx(x,y-1)] && mat[idx(x,y-1)]===m;
      const lf = x>0 && !!src[idx(x-1,y)] && mat[idx(x-1,y)]===m;
      const dn = y<H-1 && !!src[idx(x,y+1)] && mat[idx(x,y+1)]===m;
      const rt = x<W-1 && !!src[idx(x+1,y)] && mat[idx(x+1,y)]===m;
      if(!up||!lf) b.key[i]='hi';           // up-left rim → highlight
      else if(!dn||!rt) b.key[i]='sh';      // down-right rim → shadow
    }
    // deepen the lower-right belly of the oilskin for volume
    for(let y=0;y<H;y++)for(let x=0;x<W;x++){
      const i=idx(x,y); if(mat[i]!=='OIL'||b.key[i]!=='sh') continue;
      const dn=idx(x,y+1), rt=idx(x+1,y);
      if(inb(x,y+1)&&mat[dn]==='OIL'&&b.key[dn]==='sh'&&inb(x+1,y)&&mat[rt]==='OIL') { /* keep */ }
    }
  }

  // ---- outline: dark keyline on transparent pixels touching the silhouette --
  function outline(b){
    const add=[];
    for(let y=0;y<H;y++)for(let x=0;x<W;x++){
      if(b.key[idx(x,y)]) continue;
      let touch=false;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ if(inb(x+dx,y+dy)&&b.key[idx(x+dx,y+dy)]&&b.mat[idx(x+dx,y+dy)]!=='__out'){ touch=true; break; } }
      if(touch) add.push(i2(x,y));
    }
    for(const [x,y] of add){ b.key[idx(x,y)]='out'; b.mat[idx(x,y)]='__out'; }
  }
  function i2(x,y){ return [x,y]; }

  // ---- colourise ------------------------------------------------------------
  function hex2rgb(h){ return [parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]; }
  function colourOf(matName,key){
    if(matName==='__out'||key==='out') return HEX.out;
    const m=MAT[matName]; if(!m) return HEX.out;
    const name = key==='hi'?m.hi : key==='sh'?m.sh : key==='dp'?(m.dp||m.sh) : m.mid;
    return HEX[name];
  }
  function toRGBA(b){
    const out=new Uint8ClampedArray(W*H*4);
    for(let i=0;i<W*H;i++){
      const k=b.key[i]; if(!k){ out[i*4+3]=0; continue; }
      const [r,g,bl]=hex2rgb(colourOf(b.mat[i],k));
      out[i*4]=r; out[i*4+1]=g; out[i*4+2]=bl; out[i*4+3]=255;
    }
    return out;
  }

  root.FisherHaul = {
    W, H, PAL:HEX, FRAMES, FRAME_COUNT:FRAMES.length,
    CYCLE:['c0','c1','c2','c3','c4','c5'],
    poseFor:(n)=>POSES[n](), renderRGBA:(pose)=>drawPose(pose),
    renderName:(n)=>drawPose(POSES[n]()),
  };
})(typeof globalThis!=='undefined'?globalThis:window);
