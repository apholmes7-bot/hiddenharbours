/* Hidden Harbours — parametric RED FOX (Vulpes vulpes). Ambient shore/town wildlife
   that wanders, then bolts. Top-down ¾ read to match the Rock Crab / catch icons.
   32 px = 1 m → ~0.8 m nose-to-tail. Single implied key light = upper-LEFT. No AA.
   1px #171a14 keyline. KTC palette ONLY — the rust coat is the rock-crab carapace ramp
   (GreywickHouseRed warmed toward Wood/Earth); cream belly/cheeks/tail-tip from the bone
   highlight; near-black ear-backs / socks / nose from the rust ramp's dark end into the
   keyline. Nothing new invented.

   FOUR-DIRECTIONAL (matches the Fisher.png convention: down, up, left, right).
   All poses are built in a forward/lateral frame that rotates per heading, then the SAME
   upper-left dome light is applied after placement, so every facing stays lit consistently.
     • down  = faces the camera: muzzle, eyes, cream blaze; tail streams up (away).
     • up    = the BACK view: dark ear-backs + nape, no face; tail toward camera (down).
     • right = ¾ side profile: nose leads right, one eye, tail trails left.
     • left  = mirror-X of right (per the PlayerHaul "mirror for the other tack" rule).

   Sheet: 8 cols × 15 rows of 32×32, centre pivot (16,16).  → Art/Characters/Fox.png (256×480)
     rows 0–3   WALK   down / up / left / right   (w?_0..7, loops, 4-beat gait)
     rows 4–7   TROT   down / up / left / right   (loops, diagonal pairs)
     rows 8–11  RUN    down / up / left / right   (loops, gather↔extend gallop, tail streams)
     row 12     IDLE   d0 d1 · u0 u1 · l0 l1 · r0 r1   (breathe + ear/tail flick, per dir)
     row 13     ALERT  d0 d1 · u0 u1 · l0 l1 · r0 r1   (ears up, head forward, per dir)
     row 14     sit_d0 sit_d1 · sit_r0 sit_r1 · sit_l0 sit_l1 · sleep0 sleep1
                (sit down / right / left[mirror]; sleep = curled nose-to-tail, dir-agnostic)

   Exposes globalThis.Fox with:
     W,H, PAL, COLS, ROWS, DIRS, SHEET (frame names, row-major), FRAMES(=SHEET), FRAME_COUNT,
     CYCLES {walk_down, walk_up, ..., run_right, idle_down, ..., sit_down, sit_right, sleep},
     renderFrame(name) -> Uint8ClampedArray(W*H*4)   (nearest-neighbour ready)
   Works in the run_script sandbox (bake) and in the browser (live preview). */
(function (root) {
  const W = 32, H = 32, COLS = 8, ROWS = 15;

  const HEX = {
    out:'#171a14',
    coat:'#b25e3e', coatHi:'#cf7a52', coatSh:'#8a4530', coatDp:'#5f2c20',
    crm:'#cdb890', crmHi:'#ece0c8', crmSh:'#9c7f57',
    sock:'#5f2c20', sockHi:'#8a4530', sockSh:'#241512',
    eye:'#241512',
  };
  const MAT = {
    COAT: { mid:'coat', hi:'coatHi', sh:'coatSh', dp:'coatDp' },
    CREAM:{ mid:'crm',  hi:'crmHi',  sh:'crmSh' },
    SOCK: { mid:'sock', hi:'sockHi', sh:'sockSh' },
  };

  // ---- buffers --------------------------------------------------------------
  function newBuf(w,h){ return { w, h, key:new Array(w*h).fill(''), mat:new Array(w*h).fill(null) }; }
  const idx = (b,x,y)=> y*b.w+x;
  const inb = (b,x,y)=> x>=0&&x<b.w&&y>=0&&y<b.h;
  const lerp=(a,b,t)=>a+(b-a)*t;
  function put(b,x,y,mat){ x=Math.round(x); y=Math.round(y); if(!inb(b,x,y))return; b.key[idx(b,x,y)]='mid'; b.mat[idx(b,x,y)]=mat; }
  function putKey(b,x,y,mat,k){ x=Math.round(x); y=Math.round(y); if(!inb(b,x,y))return; b.key[idx(b,x,y)]=k; b.mat[idx(b,x,y)]=mat; }

  function ellipse(b,cx,cy,rx,ry,mat){
    for(let y=Math.floor(cy-ry);y<=Math.ceil(cy+ry);y++)
      for(let x=Math.floor(cx-rx);x<=Math.ceil(cx+rx);x++){
        const dx=(x-cx)/(rx+0.001), dy=(y-cy)/(ry+0.001);
        if(dx*dx+dy*dy<=1) put(b,x,y,mat);
      }
  }
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
  function contact(b,x0,y0,x1,y1,r){
    const minx=Math.floor(Math.min(x0,x1)-r), maxx=Math.ceil(Math.max(x0,x1)+r);
    const miny=Math.floor(Math.min(y0,y1)-r), maxy=Math.ceil(Math.max(y0,y1)+r);
    const dx=x1-x0, dy=y1-y0, L2=dx*dx+dy*dy||1;
    for(let y=miny;y<=maxy;y++)for(let x=minx;x<=maxx;x++){
      if(!inb(b,x,y))continue; const i=idx(b,x,y); if(!b.key[i])continue;
      let t=((x-x0)*dx+(y-y0)*dy)/L2; t=Math.max(0,Math.min(1,t));
      const px=x0+dx*t, py=y0+dy*t; if(Math.hypot(x-px,y-py)>r) continue;
      const m=b.mat[i]; if(m==='COAT') b.key[i]= b.key[i]==='sh'?'dp':'sh'; else if(m) b.key[i]='sh';
    }
  }

  // ---- parts (all take absolute frame coords) -------------------------------
  function drawLeg(b, hx,hy, fx,fy, lift){
    const mx=lerp(hx,fx,0.55), my=lerp(hy,fy,0.55);
    if(lift<0.4) contact(b, hx,hy, fx,fy, 2.1);
    taper(b, hx,hy, mx,my, 2.1, 1.5, 'COAT');
    taper(b, mx,my, fx,fy, 1.4, 0.9, 'SOCK');
    putKey(b, Math.round(fx), Math.round(fy), 'SOCK','sh');
  }
  function drawTail(b, bx,by, dir, spread){
    const rads=[1.8,2.6,3.2,3.4,2.8,1.7];
    let x=bx, y=by, a=dir;
    for(let i=0;i<rads.length;i++){
      ellipse(b, x, y, rads[i], rads[i], i>=rads.length-2 ? 'CREAM' : 'COAT');
      a += spread; x += Math.cos(a)*1.8; y += Math.sin(a)*1.8;
    }
  }
  // ear from explicit base->tip. mode 'inner' = dark back + cream cup (front/side);
  // mode 'back' = the outside of the ear (up view): mostly dark, rust base edge.
  function drawEar(b, bx,by, tipx,tipy, mode){
    taper(b, bx,by, tipx,tipy, 2.6, 0.6, 'SOCK');
    if(mode==='back'){
      putKey(b, Math.round(bx), Math.round(by), 'COAT','sh');
      putKey(b, Math.round(lerp(bx,tipx,0.35)), Math.round(lerp(by,tipy,0.35)), 'COAT','dp');
    } else {
      const inx=lerp(bx,tipx,0.30), iny=lerp(by,tipy,0.30);
      const itx=lerp(bx,tipx,0.72), ity=lerp(by,tipy,0.72);
      taper(b, inx,iny, itx,ity, 1.2, 0.4, 'CREAM');
    }
    putKey(b, Math.round(tipx), Math.round(tipy), 'SOCK','sh');
  }

  // ---- direction frame ------------------------------------------------------
  // forward = toward the nose; lateral = the fox's right/left across the body.
  function dirVec(dir){
    if(dir==='up')    return { f:[0,-1], l:[1,0],  face:'back'  };
    if(dir==='right') return { f:[1,0],  l:[0,1],  face:'side'  };
    if(dir==='left')  return { f:[-1,0], l:[0,1],  face:'side'  };
    return                   { f:[0,1],  l:[1,0],  face:'front' }; // down
  }

  // Leg roots/feet in (forward a, lateral b). Front pair forward, hind pair back.
  const LEGDEF=[
    {ha:3.0,  hb:-3.4, fa:2.7,  fb:-3.7, front:true },  // fore-left
    {ha:3.0,  hb: 3.4, fa:2.7,  fb: 3.7, front:true },  // fore-right
    {ha:-4.0, hb:-3.9, fa:-1.7, fb:-4.0, front:false},  // hind-left
    {ha:-4.0, hb: 3.9, fa:-1.7, fb: 4.0, front:false},  // hind-right
  ];

  // draw a fully-resolved fox for a spec S (S carries dir + pose scalars)
  function drawFoxDir(b, S){
    const D=dirVec(S.dir), vert = D.f[1]!==0, bob=S.bob||0, st=S.stretch||0, br=S.breathe||0;
    const at=(a,bb)=>[16 + D.f[0]*a + D.l[0]*bb, 16 + D.f[1]*a + D.l[1]*bb + bob];
    const rr=(rf,rl)=> vert? [rl,rf] : [rf,rl];      // -> [rx,ry] for an axis-aligned ellipse
    const headA = 7.0 + st*1.0, noseA = 10.3 + st*1.2;

    // tail (behind the body, unless facing away → drawn on top later)
    const tb=at(-4.5 - st*0.4, 0);
    const tailDir=Math.atan2(-D.f[1], -D.f[0]) + (S.tailSway||0);
    const drawT=()=> drawTail(b, tb[0], tb[1], tailDir, S.tailSpread ?? 0.10);
    if(!S.tailFront) drawT();

    // legs
    for(const L of S.legs){
      const hip=at(L.ha, L.hb);
      const reachA = L.front? (L.reach||0) : -(L.reach||0)*0.9;
      const foot=at(L.ha + L.fa + reachA, L.fb);
      const lift=L.lift||0;
      const fx=lerp(hip[0],foot[0],1-0.30*lift), fy=lerp(hip[1],foot[1],1-0.30*lift)-lift*0.8;
      drawLeg(b, hip[0],hip[1], fx,fy, lift);
    }

    // body masses back → front
    const masses=[['H',-4.5-st*0.5,4.4+br,4.6],['W',-1.0,4.6,3.7+br],['C',3.0+st*0.3,4.0,4.1]];
    for(const [,a,rf,rl] of masses){ const c=at(a,0), [rx,ry]=rr(rf,rl); ellipse(b,c[0],c[1],rx,ry,'COAT'); }

    // neck + head
    const chestP=at(3.0+st*0.3,0), headP=at(headA,0);
    taper(b, chestP[0],chestP[1], headP[0],headP[1], 2.9, 2.5, 'COAT');
    { const [rx,ry]=rr(3.3,3.5); ellipse(b, headP[0],headP[1], rx,ry,'COAT'); }

    // ears — sit on the back of the head, flare outward & back
    const ebA=headA-1.4, etA=headA-2.9 - (S.earUp||0)*0.5;
    const ebB=2.3, etB=2.3 + 1.5 + (S.earUp||0)*1.2;
    const emode = D.face==='back' ? 'back' : 'inner';
    for(const s of [-1,1]){ const eb=at(ebA, s*ebB), et=at(etA, s*etB); drawEar(b, eb[0],eb[1], et[0],et[1], emode); }

    // face
    if(D.face==='front'){
      for(const s of [-1,1]){ const cp=at(headA-0.2, s*3.0); putKey(b,cp[0],cp[1],'CREAM','mid'); }
      const m0=at(headA+1.0,0), nose=at(noseA,0), npre=at(noseA-1.3,0);
      taper(b, m0[0],m0[1], nose[0],nose[1], 2.1, 1.1, 'CREAM');
      putKey(b, nose[0],nose[1],'SOCK','sh'); putKey(b, npre[0],npre[1],'SOCK','mid');
      const bz=at(3.4,0); taper(b, bz[0],bz[1], m0[0],m0[1], 1.7, 1.1, 'CREAM');
      for(const s of [-1,1]){ const e=at(headA+0.4, s*1.8);
        putKey(b,e[0],e[1],'SOCK','sh'); if(!S.eyesClosed) putKey(b,e[0]-1,e[1]-1,'CREAM','hi'); }
    } else if(D.face==='side'){
      const m0=at(headA+1.0, 0.5), nose=at(noseA, 0.5);
      taper(b, m0[0],m0[1], nose[0],nose[1], 1.9, 1.0, 'CREAM');
      putKey(b, nose[0],nose[1],'SOCK','sh');
      const cp=at(headA-0.4, -2.3); putKey(b,cp[0],cp[1],'CREAM','mid');
      const e=at(headA+0.6, -1.2); putKey(b,e[0],e[1],'SOCK','sh'); putKey(b,e[0]-1,e[1]-1,'CREAM','hi');
      const bz0=at(3.2,0.3), bz1=at(6.2,0.5); taper(b, bz0[0],bz0[1], bz1[0],bz1[1], 1.4, 1.0, 'CREAM');
    } else { // back — no face; cream cheek hints at the head sides + a darker nape
      for(const s of [-1,1]){ const cp=at(headA-0.9, s*2.5); putKey(b,cp[0],cp[1],'CREAM','sh'); }
      const np=at(5.2,0), i=idx(b,Math.round(np[0]),Math.round(np[1])); if(inb(b,Math.round(np[0]),Math.round(np[1]))&&b.mat[i]==='COAT') b.key[i]='sh';
    }

    if(S.tailFront) drawT();
  }

  // ---- gaits ----------------------------------------------------------------
  function gaitParams(g){
    if(g==='walk') return { stride:2.4, bob:0.7, stretch:0.0, lift:0.7, tail:'sway', offs:[0.0,0.5,0.75,0.25] };
    if(g==='trot') return { stride:3.2, bob:1.2, stretch:0.7, lift:1.1, tail:'sway', offs:[0.0,0.5,0.5,0.0] };
    return               { stride:4.0, bob:1.7, stretch:2.0, lift:1.3, tail:'stream', offs:[0.08,0.0,0.58,0.66] }; // run
  }
  function buildGait(dir, g, phase){
    const G=gaitParams(g), th=phase*Math.PI*2;
    const stretch=G.stretch*Math.sin(th);
    const bob=(g==='run')? -Math.cos(th)*G.bob : Math.sin(th*2)*G.bob;
    const legs=LEGDEF.map((d,i)=>{
      const a=((phase+G.offs[i])%1)*Math.PI*2;
      return { ...d, reach:Math.cos(a)*G.stride, lift:Math.max(0,Math.sin(a))*G.lift };
    });
    const sway = G.tail==='stream'? Math.sin(th)*0.13 : Math.sin(th+0.6)*0.34;
    return { dir, stretch, bob, legs, earUp:0.7,
      tailSpread: G.tail==='stream'?0.05:0.11, tailSway:sway, tailFront: dir==='up' };
  }

  // ---- stands (idle / alert) ------------------------------------------------
  function buildStand(dir, alert, v){
    const breathe = v? 0.4 : 0;
    const legs=LEGDEF.map(d=>({ ...d, reach:0, lift:0 }));
    const sway = alert? (v? -0.10 : 0) : (v? 0.42 : 0.06);
    return { dir, stretch: alert? (v?0.7:0.45) : 0, bob:0, breathe, legs,
      earUp: alert? 1.0 : (v? 0.62 : 0.78),
      tailSpread: alert? 0.04 : 0.13, tailSway:sway, tailFront: dir==='up' };
  }

  // ---- sit (down + right; left mirrors right) -------------------------------
  // Seated: broad low haunch behind, chest rising to a lifted head, forepaws planted
  // forward & together, hind folded. Built in the same forward/lateral frame.
  function buildSit(dir, v){
    const br=v?0.35:0;
    const legs=[
      {ha:3.4, hb:-1.7, fa:2.6, fb:-1.6, front:true,  reach:0, lift:0},  // fore paws together, forward
      {ha:3.4, hb: 1.7, fa:2.6, fb: 1.6, front:true,  reach:0, lift:0},
      {ha:-3.0,hb:-4.6, fa:0.2, fb:-4.9, front:false, reach:0, lift:0},  // hind folded wide & low
      {ha:-3.0,hb: 4.6, fa:0.2, fb: 4.9, front:false, reach:0, lift:0},
    ];
    return { dir, sit:true, breatheSit:br, legs,
      earUp:1.0, tailSpread:0.6, tailSway:0.0, tailFront: dir==='up' };
  }
  // sit uses a bespoke body (broad rear) but the shared parts otherwise
  function drawSit(b, S){
    const D=dirVec(S.dir), vert=D.f[1]!==0, br=S.breatheSit||0;
    const at=(a,bb)=>[16 + D.f[0]*a + D.l[0]*bb, 16 + D.f[1]*a + D.l[1]*bb];
    const rr=(rf,rl)=> vert? [rl,rf] : [rf,rl];
    const headA=5.6;
    // tail curls around the flank (behind)
    const tb=at(-3.4, D===null?0:2.2*(D.l[0]||0)+2.2*0);
    if(!S.tailFront) drawTail(b, tb[0], tb[1], Math.atan2(-D.f[1],-D.f[0])+0.5, 0.6);
    for(const L of S.legs){ const hip=at(L.ha,L.hb), foot=at(L.ha+L.fa, L.fb); drawLeg(b, hip[0],hip[1], foot[0],foot[1], 0); }
    // broad low haunch + rising trunk
    { const c=at(-3.2,0), [rx,ry]=rr(5.4+br,5.2+br); ellipse(b,c[0],c[1],rx,ry,'COAT'); }
    { const c=at(0.4,0),  [rx,ry]=rr(3.9,3.7); ellipse(b,c[0],c[1],rx,ry,'COAT'); }
    { const c=at(3.2,0),  [rx,ry]=rr(3.5,3.4); ellipse(b,c[0],c[1],rx,ry,'COAT'); }
    const chestP=at(3.2,0), headP=at(headA,0);
    taper(b, chestP[0],chestP[1], headP[0],headP[1], 2.7, 2.4, 'COAT');
    { const [rx,ry]=rr(3.3,3.4); ellipse(b, headP[0],headP[1], rx,ry,'COAT'); }
    const ebA=headA-1.3, etA=headA-2.9, ebB=2.2, etB=2.2+2.4;
    const emode=D.face==='back'?'back':'inner';
    for(const s of [-1,1]){ const eb=at(ebA,s*ebB), et=at(etA,s*etB); drawEar(b, eb[0],eb[1], et[0],et[1], emode); }
    if(D.face==='front'){
      const m0=at(headA+1.0,0), nose=at(headA+3.2,0);
      taper(b, m0[0],m0[1], nose[0],nose[1], 2.0,1.1,'CREAM'); putKey(b,nose[0],nose[1],'SOCK','sh');
      const bz=at(2.0,0); taper(b, bz[0],bz[1], m0[0],m0[1], 1.8,1.1,'CREAM');
      for(const s of [-1,1]){ const e=at(headA+0.4,s*1.7); putKey(b,e[0],e[1],'SOCK','sh'); putKey(b,e[0]-1,e[1]-1,'CREAM','hi'); }
    } else if(D.face==='side'){
      const m0=at(headA+1.0,0.5), nose=at(headA+3.2,0.5);
      taper(b, m0[0],m0[1], nose[0],nose[1], 1.9,1.0,'CREAM'); putKey(b,nose[0],nose[1],'SOCK','sh');
      const e=at(headA+0.6,-1.2); putKey(b,e[0],e[1],'SOCK','sh'); putKey(b,e[0]-1,e[1]-1,'CREAM','hi');
      const bz0=at(1.6,0.3), bz1=at(4.4,0.4); taper(b, bz0[0],bz0[1], bz1[0],bz1[1], 1.6,1.0,'CREAM');
    } else { for(const s of [-1,1]){ const cp=at(headA-0.9,s*2.3); putKey(b,cp[0],cp[1],'CREAM','sh'); } }
    if(S.tailFront) drawTail(b, tb[0], tb[1], Math.atan2(-D.f[1],-D.f[0])+0.5, 0.6);
  }

  // ---- sleep (bespoke curl; orientation-agnostic) ---------------------------
  function drawSleep(b, v){
    const cx=16, cy=16, br=v?0.5:0;
    drawTail(b, cx-4.5, cy+3.0, -Math.PI*0.62, 0.5);
    ellipse(b, cx-0.4, cy-0.3, 7.2+br, 6.0+br, 'COAT');
    ellipse(b, cx+2.4, cy-3.2, 4.6, 3.8, 'COAT');
    for(let t=0;t<Math.PI;t+=0.25){ const x=cx-0.4+Math.cos(t+0.6)*5.6, y=cy-0.3+Math.sin(t+0.6)*4.4;
      const i=idx(b,Math.round(x),Math.round(y)); if(inb(b,Math.round(x),Math.round(y))&&b.mat[i]==='COAT') b.key[i]='sh'; }
    ellipse(b, cx+3.6, cy+3.2, 3.0, 2.8, 'COAT');
    drawEar(b, cx+2.0, cy+1.6, cx+1.0, cy-0.6, 'inner');
    drawEar(b, cx+5.4, cy+2.0, cx+6.6, cy+0.2, 'inner');
    taper(b, cx+3.6, cy+2.6, cx+1.8, cy+1.4, 1.8, 1.0, 'CREAM');
    putKey(b, cx+1.8, cy+1.4,'SOCK','sh');
    putKey(b, cx+3.4, cy+2.6,'SOCK','sh');
    for(let t=0.4;t<2.0;t+=0.3){ const x=cx-0.4+Math.cos(t+1.4)*5.2, y=cy-0.3+Math.sin(t+1.4)*3.9;
      putKey(b, Math.round(x), Math.round(y), 'CREAM','mid'); }
  }

  // ---- light + outline + colourise (upper-left key) -------------------------
  function domeShade(b){
    const cx=16, cy=16;
    for(let y=0;y<b.h;y++)for(let x=0;x<b.w;x++){
      const i=idx(b,x,y), m=b.mat[i]; if(!m||b.key[i]!=='mid') continue;
      const Lv=-((x-cx)*0.55+(y-cy)*0.85);
      if(m==='COAT')      b.key[i]= Lv>7?'hi': Lv>-3?'mid': Lv>-12?'sh':'dp';
      else if(m==='CREAM')b.key[i]= Lv>5?'hi': Lv>-6?'mid':'sh';
      else                b.key[i]= Lv>2?'hi': Lv>-8?'mid':'sh';
    }
  }
  function shade(b){
    const src=b.key.slice(), mat=b.mat;
    for(let y=0;y<b.h;y++)for(let x=0;x<b.w;x++){
      const i=idx(b,x,y); if(src[i]!=='mid') continue; const m=mat[i];
      const up=y>0&&src[idx(b,x,y-1)]&&mat[idx(b,x,y-1)]===m;
      const lf=x>0&&src[idx(b,x-1,y)]&&mat[idx(b,x-1,y)]===m;
      const dn=y<b.h-1&&src[idx(b,x,y+1)]&&mat[idx(b,x,y+1)]===m;
      const rt=x<b.w-1&&src[idx(b,x+1,y)]&&mat[idx(b,x+1,y)]===m;
      if(!up||!lf) b.key[i]='hi'; else if(!dn||!rt) b.key[i]='sh';
    }
  }
  function outline(b){
    const add=[];
    for(let y=0;y<b.h;y++)for(let x=0;x<b.w;x++){
      if(b.key[idx(b,x,y)]) continue;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){
        if(inb(b,x+dx,y+dy)&&b.key[idx(b,x+dx,y+dy)]&&b.mat[idx(b,x+dx,y+dy)]!=='__out'){ add.push([x,y]); break; }
      }
    }
    for(const [x,y] of add){ b.key[idx(b,x,y)]='__o'; b.mat[idx(b,x,y)]='__out'; }
  }
  function hex2rgb(h){ return [parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]; }
  function colourOf(mat,k){
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
  function mirrorX(rgba,w,h){
    const out=new Uint8ClampedArray(w*h*4);
    for(let y=0;y<h;y++)for(let x=0;x<w;x++){ const s=(y*w+x)*4, d=(y*w+(w-1-x))*4;
      out[d]=rgba[s]; out[d+1]=rgba[s+1]; out[d+2]=rgba[s+2]; out[d+3]=rgba[s+3]; }
    return out;
  }

  // ---- frame registry -------------------------------------------------------
  const GAITS=['walk','trot','run'], DIRS=['down','up','left','right'];
  const CYCLES={};
  const SHEET=[];
  // rows 0-11 : gaits × dirs
  for(const g of GAITS) for(const d of DIRS){
    const names=[]; for(let k=0;k<8;k++) names.push(`${g}_${d[0]}${k}`);
    CYCLES[`${g}_${d}`]=names; SHEET.push(...names);
  }
  // row 12 idle, row 13 alert : d0 d1 u0 u1 l0 l1 r0 r1
  for(const st of ['idle','alert']){
    const row=[]; for(const d of DIRS){ const a=`${st}_${d[0]}0`, b=`${st}_${d[0]}1`; row.push(a,b); CYCLES[`${st}_${d}`]=[a,b]; }
    SHEET.push(...row);
  }
  // row 14 : sit d/r/l + sleep
  CYCLES.sit_down=['sit_d0','sit_d1']; CYCLES.sit_right=['sit_r0','sit_r1']; CYCLES.sit_left=['sit_l0','sit_l1'];
  CYCLES.sleep=['sleep0','sleep1'];
  SHEET.push('sit_d0','sit_d1','sit_r0','sit_r1','sit_l0','sit_l1','sleep0','sleep1');

  const DCODE={d:'down',u:'up',l:'left',r:'right'};
  function renderFrame(name){
    // left frames mirror the matching right frame (keeps light convention like PlayerHaul)
    let m;
    if((m=/^(walk|trot|run)_l(\d)$/.exec(name)))  return mirrorX(renderFrame(`${m[1]}_r${m[2]}`),W,H);
    if((m=/^(idle|alert)_l(\d)$/.exec(name)))      return mirrorX(renderFrame(`${m[1]}_r${m[2]}`),W,H);
    if(/^sit_l\d$/.test(name))                     return mirrorX(renderFrame(name.replace('_l','_r')),W,H);

    const b=newBuf(W,H);
    if((m=/^(walk|trot|run)_([dur])(\d)$/.exec(name))) drawFoxDir(b, buildGait(DCODE[m[2]], m[1], +m[3]/8));
    else if((m=/^(idle|alert)_([dur])(\d)$/.exec(name))) drawFoxDir(b, buildStand(DCODE[m[2]], m[1]==='alert', +m[3]));
    else if((m=/^sit_([dr])(\d)$/.exec(name))) drawSit(b, buildSit(DCODE[m[1]], +m[2]));
    else if(name==='sleep0') drawSleep(b,0);
    else if(name==='sleep1') drawSleep(b,1);
    domeShade(b); shade(b); outline(b);
    return toRGBA(b);
  }

  root.Fox = {
    W, H, COLS, ROWS, PAL:HEX, DIRS, GAITS, CYCLES, SHEET, FRAMES:SHEET, FRAME_COUNT:SHEET.length,
    renderFrame,
  };
})(typeof globalThis!=='undefined'?globalThis:window);
