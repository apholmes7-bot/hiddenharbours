/* Hidden Harbours — parametric PEI WILDFLOWERS. Roadside/meadow/shore/dooryard dressing.
   Matches the Acadian-forest foliage conventions: 32 px = 1 m · no AA · transparent PNG ·
   binary alpha · upper-left key light · soft dark keyline (#1b2a22, sits in landscape) ·
   base-pivot (bottom-centre) for standing plants, centre-pivot for the ground patch.

   THREE TIERS (separate swap-in sprites, like the seaweed):
     single — one plant, true height (base pivot). Sheet = 4 sway frames × 3 bloom stages.
     clump  — a tight 2–4 stand, full bloom. Sheet = 4 sway frames.
     patch  — a low ground drift (small/atmospheric, centre pivot). Sheet = 4 sway frames × 2 variants.

   BLOOM STAGES (single): 0 bud → 1 full → 2 gone-to-seed (pods / fluff / hips / bare disc).
   SWAY: a per-scanline horizontal shear pinned at the base, oscillating over 4 frames — top
   sways most, base stays put, so it registers to the pivot with no drift.

   Archetypes keep it DRY; each species maps to one + a colour/size params block:
     spike  — vertical raceme: lupin, fireweed, goldenrod
     umbel  — flat lacy dome: Queen Anne's lace
     radial — disc + petals: oxeye daisy, wild rose, buttercup
     iris   — sword leaves + blue flag flower
     orchid — pouch flower: lady's slipper
   (clover rides along in the buttercup ground patch.)

   Exposes globalThis.Flowers:
     SPECIES [{key,name,latin,arch,tint,...}]  (lupins expand to 4 colour morphs)
     TIERS {single,clump,patch}  STAGES  SWAY
     renderSingle(key, stageIdx, swayFrame) -> {w,h,rgba}
     renderClump(key, swayFrame)            -> {w,h,rgba}
     renderPatch(key, variant)              -> {w,h,rgba}
   Runs in the run_script sandbox (bake) and the browser (live preview). */
(function (root) {
  const KEYLINE = '#1b2a22';

  // ---- tiers ---------------------------------------------------------------
  const TIERS = {
    single: { w:32, h:48, baseX:16, baseY:47 },
    clump:  { w:48, h:46, baseX:24, baseY:45 },
    patch:  { w:44, h:26, baseX:22, baseY:22 },
  };
  const STAGES = ['bud','full','seed'];
  const SWAY = 4;

  // ---- colour helpers ------------------------------------------------------
  function h2r(h){ return [parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]; }
  function r2h(r){ return '#'+r.map(v=>Math.max(0,Math.min(255,Math.round(v))).toString(16).padStart(2,'0')).join(''); }
  function mix(a,b,t){ const A=h2r(a),B=h2r(b); return r2h([0,1,2].map(i=>A[i]+(B[i]-A[i])*t)); }
  function light(hex,f){ return f>=0 ? mix(hex,'#ffffff',f) : mix(hex,'#000000',-f); }
  // 4-step ramp {hi,mid,sh,dp} from a base mid
  function ramp(base){ return { hi:light(base,0.26), mid:base, sh:light(base,-0.24), dp:light(base,-0.44) }; }

  const GREEN  = ramp('#4f8a5f');    // default stem/leaf
  const GREEN2 = ramp('#3c6f4a');    // darker leaf
  const DUNE   = ramp('#7f8a54');    // dune / dry grass green

  // ---- species -------------------------------------------------------------
  // arch params are read by the matching draw routine. h = plant height in px at single scale.
  const RAW = [
    { key:'Lupin', name:'Lupin', latin:'Lupinus polyphyllus', arch:'spike', ph:40,
      morphs:{ Purple:'#7d5aa8', Pink:'#d17aa6', Blue:'#5a72b8', White:'#e9edea' }, stem:GREEN2, spikeW:5, palmate:true, floretStep:2.3 },
    { key:'LadySlipper', name:"Lady's Slipper", latin:'Cypripedium acaule', arch:'orchid', ph:24,
      bloom:'#e58fb4', bloom2:'#6e3a4a', accent:'#f0eee6', stem:GREEN },
    { key:'Fireweed', name:'Fireweed', latin:'Chamaenerion angustifolium', arch:'spike', ph:42,
      morphs:{ Magenta:'#c85a90' }, stem:GREEN2, spikeW:4, palmate:false, seedFluff:true, floretStagger:true, floretStep:2.6 },
    { key:'QueenAnne', name:"Queen Anne's Lace", latin:'Daucus carota', arch:'umbel', ph:38,
      bloom:'#eef0ea', accent:'#c9cfbf', stem:GREEN },
    { key:'WildRose', name:'Wild Rose', latin:'Rosa virginiana', arch:'radial', ph:30,
      bloom:'#e58aa4', accent:'#e6c23c', petals:5, petalR:5, hip:'#b83a30', stem:GREEN2, bushy:true },
    { key:'Goldenrod', name:'Goldenrod', latin:'Solidago', arch:'spike', ph:40,
      morphs:{ Gold:'#e0b23a' }, stem:GREEN2, spikeW:6, plume:true },
    { key:'OxeyeDaisy', name:'Oxeye Daisy', latin:'Leucanthemum vulgare', arch:'radial', ph:32,
      bloom:'#eef0ea', accent:'#e6c23c', petals:10, petalR:6, stem:GREEN },
    { key:'BlueFlag', name:'Blue Flag Iris', latin:'Iris versicolor', arch:'iris', ph:38,
      bloom:'#5a6fb2', bloom2:'#3f5192', accent:'#e6c23c', stem:'#3f7a52' },
    { key:'Buttercup', name:'Buttercup & Clover', latin:'Ranunculus / Trifolium', arch:'ground', ph:22,
      bloom:'#f0c62c', clover:'#d79fbf', cloverW:'#e9e6df', stem:GREEN },
  ];

  // expand lupins/fireweed/goldenrod morphs into concrete species entries
  const SPECIES = [];
  for(const s of RAW){
    if(s.morphs){
      const names = Object.keys(s.morphs);
      for(const m of names){
        SPECIES.push(Object.assign({}, s, {
          key: s.key + (names.length>1 ? m : ''), morph:m, bloom:s.morphs[m],
          name: names.length>1 ? (m+' '+s.name) : s.name,
        }));
      }
    } else SPECIES.push(s);
  }
  const byKey = {}; SPECIES.forEach(s=>byKey[s.key]=s);

  // ---- buffer + primitive kit (shared with the tray rig style) -------------
  function Buf(w,h){ this.w=w; this.h=h; this.key=new Array(w*h).fill(''); this.mat=new Array(w*h).fill(null); }
  Buf.prototype.i=function(x,y){ return y*this.w+x; };
  Buf.prototype.in=function(x,y){ return x>=0&&x<this.w&&y>=0&&y<this.h; };
  Buf.prototype.put=function(x,y,m,k){ x=Math.round(x); y=Math.round(y); if(!this.in(x,y))return; this.key[this.i(x,y)]=k||'mid'; this.mat[this.i(x,y)]=m; };
  function ell(b,cx,cy,rx,ry,m,k){ for(let y=Math.floor(cy-ry);y<=Math.ceil(cy+ry);y++)for(let x=Math.floor(cx-rx);x<=Math.ceil(cx+rx);x++){ const dx=(x-cx)/(rx+.001),dy=(y-cy)/(ry+.001); if(dx*dx+dy*dy<=1) b.put(x,y,m,k); } }
  function dot(b,x,y,m,k){ b.put(x,y,m,k); }
  function line(b,x0,y0,x1,y1,m,k,thick){ const dx=x1-x0,dy=y1-y0,n=Math.max(Math.abs(dx),Math.abs(dy))||1; for(let i=0;i<=n;i++){ const x=x0+dx*i/n,y=y0+dy*i/n; if(thick){ for(let ox=-((thick-1)/2);ox<=(thick-1)/2;ox++) b.put(x+ox,y,m,k);} else b.put(x,y,m,k); } }
  function taper(b,x0,y0,x1,y1,r0,r1,m,k){ const dx=x1-x0,dy=y1-y0,L2=dx*dx+dy*dy||1; const mnx=Math.floor(Math.min(x0,x1)-Math.max(r0,r1)),mxx=Math.ceil(Math.max(x0,x1)+Math.max(r0,r1)),mny=Math.floor(Math.min(y0,y1)-Math.max(r0,r1)),mxy=Math.ceil(Math.max(y0,y1)+Math.max(r0,r1)); for(let y=mny;y<=mxy;y++)for(let x=mnx;x<=mxx;x++){ let t=((x-x0)*dx+(y-y0)*dy)/L2; t=Math.max(0,Math.min(1,t)); const px=x0+dx*t,py=y0+dy*t; if(Math.hypot(x-px,y-py)<=r0+(r1-r0)*t) b.put(x,y,m,k); } }
  function mulberry(a){ return function(){ a|=0;a=a+0x6D2B79F5|0;let t=Math.imul(a^a>>>15,1|a);t=t+Math.imul(t^t>>>7,61|t)^t;return((t^t>>>14)>>>0)/4294967296; }; }

  // ---- shade / outline / colourize -----------------------------------------
  function shade(b){ const src=b.key.slice(),mat=b.mat; for(let y=0;y<b.h;y++)for(let x=0;x<b.w;x++){ const i=b.i(x,y); if(src[i]!=='mid')continue; const m=mat[i];
    const up=y>0&&src[b.i(x,y-1)]&&mat[b.i(x,y-1)]===m, lf=x>0&&src[b.i(x-1,y)]&&mat[b.i(x-1,y)]===m, dn=y<b.h-1&&src[b.i(x,y+1)]&&mat[b.i(x,y+1)]===m, rt=x<b.w-1&&src[b.i(x+1,y)]&&mat[b.i(x+1,y)]===m;
    if(!up||!lf) b.key[i]='hi'; else if(!dn||!rt) b.key[i]='sh'; } }
  function outline(b){ const add=[]; for(let y=0;y<b.h;y++)for(let x=0;x<b.w;x++){ if(b.key[b.i(x,y)])continue; for(const[dx,dy]of[[1,0],[-1,0],[0,1],[0,-1],[1,1],[-1,-1],[1,-1],[-1,1]]) if(b.in(x+dx,y+dy)&&b.key[b.i(x+dx,y+dy)]&&b.mat[b.i(x+dx,y+dy)]!=='__out'){ add.push([x,y]); break; } } for(const[x,y]of add){ b.key[b.i(x,y)]='out'; b.mat[b.i(x,y)]='__out'; } }
  function colourize(b, ramps){ const out=new Uint8ClampedArray(b.w*b.h*4); for(let i=0;i<b.w*b.h;i++){ const k=b.key[i]; if(!k){ out[i*4+3]=0; continue; } let hex; const m=b.mat[i]; if(m==='__out'||k==='out') hex=KEYLINE; else { const rr=ramps[m]||ramps.STEM; hex = k==='hi'?rr.hi : k==='sh'?rr.sh : k==='dp'?rr.dp : rr.mid; } const c=h2r(hex); out[i*4]=c[0]; out[i*4+1]=c[1]; out[i*4+2]=c[2]; out[i*4+3]=255; } return out; }

  // build the ramp set for a species
  function rampsFor(s){
    const stem = typeof s.stem==='string' ? ramp(s.stem) : (s.stem||GREEN);
    return {
      STEM: stem, LEAF: stem, LEAF2: GREEN2,
      BLOOM: ramp(s.bloom||'#e58aa4'),
      BLOOM2: ramp(s.bloom2 || light(s.bloom||'#e58aa4',-0.2)),
      ACCENT: ramp(s.accent||'#e6c23c'),
      SEED: ramp('#b39a63'), POD: ramp('#8f9a54'), HIP: ramp(s.hip||'#b83a30'),
      DUNE: DUNE, WHITE: ramp('#e9e6df'), CLOVER: ramp(s.clover||'#d79fbf'),
    };
  }

  // ==== archetype draw routines =============================================
  // all draw a plant with its base at (cx, baseY); return nothing.
  // delicate stem: ~1px core the whole height (a soft keyline still frames it), never the old 3px trunk
  function drawStem(b,cx,baseY,topY,thick){ taper(b, cx, baseY, cx, topY, thick*0.28, Math.max(0.28, thick*0.12), 'STEM'); }

  function drawSpike(b,s,cx,baseY,scale,stage,rng){
    const ph=s.ph*scale, topY=baseY-ph, stemTop=topY+ph*0.12;
    drawStem(b,cx,baseY, topY, 2.2);
    // leaves
    if(s.palmate){ // lupin palmate whorls
      for(const ly of [0.26,0.46,0.66]){ const y=baseY-ph*ly, span=6.5-ly*2.5; for(let k=-4;k<=4;k++){ const a=k/4*1.05; const lx=cx+Math.sin(a)*(span+1.5)*scale, lyy=y-Math.cos(a)*span*scale-2; line(b,cx,y, lx, lyy, 'LEAF','',1); if(k%2===0) dot(b,(cx+lx)/2,(y+lyy)/2,'LEAF2','sh'); } }
    } else { for(const ly of [0.2,0.4,0.62]){ const y=baseY-ph*ly; for(const dir of [-1,1]){ taper(b,cx,y, cx+dir*6*scale, y-3*scale, 1.1,0.3,'LEAF'); } } }
    // florets up the raceme — each a shaded bead over a darker base, spaced & staggered so they read one-by-one
    const botY=baseY-ph*0.42, sw=(s.spikeW||5)*scale;
    const step=(s.plume? 1.4 : (s.floretStep||2.6))*scale;
    const N=Math.max(3, Math.round((botY-stemTop)/step));
    for(let j=0;j<=N;j++){ const t=j/N, y=stemTop+(botY-stemTop)*t; const wob=(rng()-0.5)*0.5;
      const half=sw*(0.32+0.68*t);            // widens downward
      if(stage==='seed'){
        if(s.seedFluff){ for(const dir of [-1,0,1]) dot(b,cx+dir*half*0.6+wob,y,'WHITE', dir===0?'mid':'hi'); }
        else if(s.plume){ ell(b,cx+wob,y,half*0.7,1.0,'SEED'); }
        else { if(j%2===0){ line(b,cx+wob, y, cx+wob+(j%4<2?1:-1)*half, y+1, 'POD','',1); } } // lupin pods
        continue;
      }
      if(stage==='bud' && t<0.72){ ell(b,cx+wob,y,1.1,1.0,'LEAF2'); continue; }   // lower buds closed & green
      if(s.plume){ for(const dir of [-1,1]){ line(b,cx, y, cx+dir*half, y-2, 'BLOOM','',1); dot(b,cx+dir*half,y-2,'BLOOM','hi'); line(b,cx,y, cx+dir*half*0.6, y+1,'BLOOM','sh',1); } dot(b,cx+wob,y,'BLOOM','hi'); continue; }
      if(s.floretStagger){                     // fireweed: alternating open 4-petal blooms
        const dir=(j%2)?1:-1, fx=cx+dir*half*0.5+wob, fy=y;
        ell(b,fx,fy,1.9,1.6,'BLOOM2');         // dark base = separation
        for(const [ox,oy] of [[-1,0],[1,0],[0,-1],[0,1]]) dot(b,fx+ox,fy+oy,'BLOOM');
        dot(b,fx-1,fy-1,'BLOOM','hi'); dot(b,fx,fy,'BLOOM','hi'); dot(b,fx,fy+1,'ACCENT','hi');
      } else {                                 // lupin: paired whorl beads, staggered per row
        const jit=(j%2)?0.7:-0.7;
        for(const dir of [-1,1]){ const fx=cx+dir*half*0.72+jit, fy=y;
          ell(b,fx,fy,1.7,1.5,'BLOOM2');       // dark base = separation rim between beads
          ell(b,fx-0.4,fy-0.5,1.05,0.95,'BLOOM');   // lit pea-flower bead
          dot(b,fx-0.9,fy-0.9,'BLOOM','hi');
          dot(b,fx+0.7,fy+0.9,'BLOOM2','sh');
        }
        if(j%2===0) dot(b,cx,y,'STEM','sh');   // sliver of green axis shows between L/R beads
      }
    }
    // tip buds
    if(stage!=='seed') for(let j=0;j<3;j++) ell(b,cx,stemTop-j*1.4,1.1,1.1,'LEAF2');
  }

  function drawUmbel(b,s,cx,baseY,scale,stage,rng){
    const ph=s.ph*scale, headY=baseY-ph;
    drawStem(b,cx,baseY,headY,2.0);
    for(const ly of [0.22,0.45]){ const y=baseY-ph*ly; for(const dir of [-1,1]){ for(let k=0;k<3;k++) line(b,cx+dir*k*1.5*scale, y-k*0.5, cx+dir*(k+1.5)*1.5*scale, y-2-k, 'LEAF','',1);} }
    const R=8*scale;
    if(stage==='bud'){ ell(b,cx,headY+1,3*scale,2.2*scale,'LEAF2'); return; }
    if(stage==='seed'){ // curled "bird's nest"
      for(let a=0;a<12;a++){ const an=a/12*Math.PI*2; line(b,cx,headY, cx+Math.cos(an)*R*0.5, headY-Math.abs(Math.sin(an))*2-1,'SEED','',1);} 
      ell(b,cx,headY,2.5*scale,1.4*scale,'SEED'); return;
    }
    // full: lacy flat dome — finer rays, an inner floret ring, and the single dark heart floret (Daucus signature)
    for(let a=0;a<28;a++){ const an=a/28*Math.PI*2; const rr=R*(0.66+rng()*0.36); const ex=cx+Math.cos(an)*rr, ey=headY-2 - Math.sin(an)*rr*0.5; line(b,cx,headY, ex,ey,'ACCENT','sh',1); dot(b,ex,ey,'BLOOM','hi'); dot(b,ex+(rng()<.5?1:-1),ey,'BLOOM'); if(rng()<0.5) dot(b,ex,ey-1,'WHITE','hi'); if(rng()<0.6) dot(b,(cx+ex)/2,(headY+ey)/2,'BLOOM'); }
    for(let a=0;a<13;a++){ const an=a/13*Math.PI*2+0.3; dot(b,cx+Math.cos(an)*R*0.4, headY-2-Math.sin(an)*R*0.22,'BLOOM','hi'); }
    ell(b,cx,headY-1,3*scale,1.6*scale,'BLOOM');
    dot(b,cx-1,headY-2,'BLOOM','hi');
    dot(b,cx,headY-1,'BLOOM2','sh');
  }

  function drawRadial(b,s,cx,baseY,scale,stage,rng){
    const ph=s.ph*scale, headY=baseY-ph, R=(s.petalR||6)*scale;
    drawStem(b,cx,baseY,headY,1.9);
    for(const ly of [0.28,0.55]){ const y=baseY-ph*ly; for(const dir of [-1,1]) taper(b,cx,y, cx+dir*6*scale, y-2*scale, 1.2,0.3, s.bushy?'LEAF2':'LEAF'); }
    if(s.bushy){ // a little foliage behind the bloom
      for(let a=0;a<5;a++){ const an=a/5*Math.PI*2; ell(b,cx+Math.cos(an)*R*0.9, headY+Math.sin(an)*R*0.6, 2*scale,1.6*scale,'LEAF2'); } }
    if(stage==='bud'){ ell(b,cx,headY,2.4*scale,2.6*scale,'LEAF2'); dot(b,cx,headY-2*scale,'BLOOM','sh'); return; }
    if(stage==='seed'){
      if(s.hip){ ell(b,cx,headY,3*scale,3*scale,'HIP'); dot(b,cx-1,headY-1,'HIP','hi'); for(const dx of [-1,1]) dot(b,cx+dx,headY-3*scale,'LEAF2','sh'); }
      else { ell(b,cx,headY,2.6*scale,2.4*scale,'ACCENT'); for(let a=0;a<8;a++){const an=a/8*Math.PI*2; dot(b,cx+Math.cos(an)*R*0.5,headY+Math.sin(an)*R*0.4,'SEED');} }
      return;
    }
    // full: petals then disc — petal-gap shadow for separation + a stippled stamen crown
    const NP=s.petals||8;
    for(let p=0;p<NP;p++){ const an=p/NP*Math.PI*2; const ex=cx+Math.cos(an)*R, ey=headY+Math.sin(an)*R*0.72;
      taper(b,cx,headY, ex,ey, 1.7*scale,0.8*scale,'BLOOM'); dot(b,ex,ey,'BLOOM','hi'); if(NP>=8) dot(b,ex-Math.cos(an)*1.1*scale, ey-Math.sin(an)*0.85*scale,'BLOOM','sh');
      const gan=an+Math.PI/NP; dot(b,cx+Math.cos(gan)*R*0.72, headY+Math.sin(gan)*R*0.5,'BLOOM','sh'); }
    ell(b,cx,headY,2.2*scale,1.9*scale,'ACCENT');
    for(let a=0;a<6;a++){ const an=a/6*Math.PI*2; dot(b,cx+Math.cos(an)*1.4*scale, headY+Math.sin(an)*1.2*scale,'ACCENT', a%2?'hi':'sh'); }
    dot(b,cx-1,headY-1,'ACCENT','hi'); dot(b,cx+1,headY+1,'ACCENT','sh');
  }

  function drawIris(b,s,cx,baseY,scale,stage,rng){
    const ph=s.ph*scale, headY=baseY-ph+4*scale;
    // sword leaves
    for(const dir of [-1,0,1]){ const tx=cx+dir*3*scale, ty=baseY-ph*(dir===0?1.02:0.9); taper(b,cx+dir*1.5*scale,baseY, tx,ty, 1.25*scale,0.35,'STEM'); }
    drawStem(b,cx,baseY,headY,1.7);
    if(stage==='bud'){ taper(b,cx,headY+3*scale, cx,headY-3*scale, 2*scale,0.6,'BLOOM2'); return; }
    if(stage==='seed'){ ell(b,cx,headY,2.4*scale,3.4*scale,'POD'); dot(b,cx-1,headY-1,'POD','hi'); return; }
    // full blue flag: 3 upright standards + 3 drooping falls, each fall veined toward the yellow signal
    for(let p=0;p<3;p++){ const an=(p/3)*Math.PI*2 - Math.PI/2; const ex=cx+Math.cos(an)*5*scale, ey=headY-2*scale+Math.sin(an)*2; taper(b,cx,headY, ex,ey-3*scale,1.5*scale,0.7*scale,'BLOOM'); dot(b,ex,ey-3*scale,'BLOOM','hi'); }
    for(let p=0;p<3;p++){ const an=(p/3)*Math.PI*2 + Math.PI/2; const ex=cx+Math.cos(an)*6*scale, ey=headY+3*scale+Math.abs(Math.sin(an))*2; taper(b,cx,headY, ex,ey,1.8*scale,0.9*scale,'BLOOM2'); line(b,cx,headY+1*scale, (cx+ex)/2,(headY+ey)/2,'ACCENT','sh',1); dot(b,ex,ey,'BLOOM','hi'); dot(b,(cx+ex)/2,(headY+ey)/2+1,'ACCENT','hi'); }
    ell(b,cx,headY+2*scale,1.3*scale,1.1*scale,'ACCENT'); dot(b,cx,headY+2*scale,'ACCENT','hi'); dot(b,cx,headY+3*scale,'ACCENT','sh');
    dot(b,cx-1,headY,'BLOOM','hi'); dot(b,cx+1,headY-1*scale,'BLOOM','hi');
  }

  function drawOrchid(b,s,cx,baseY,scale,stage,rng){
    const ph=s.ph*scale, headY=baseY-ph+2*scale;
    // two broad ribbed basal leaves
    for(const dir of [-1,1]){ ell(b,cx+dir*5*scale, baseY-5*scale, 4.5*scale, 2.4*scale,'LEAF'); line(b,cx+dir*1.5*scale,baseY-4*scale, cx+dir*8*scale,baseY-6*scale,'LEAF2','sh',1); line(b,cx+dir*2*scale,baseY-4.5*scale, cx+dir*7*scale,baseY-3.5*scale,'LEAF2','sh',1); }
    drawStem(b,cx,baseY-3*scale,headY,1.6);
    if(stage==='bud'){ ell(b,cx,headY,2*scale,2.6*scale,'LEAF2'); return; }
    if(stage==='seed'){ taper(b,cx,headY+3*scale, cx,headY-3*scale, 2*scale,0.8,'POD'); return; }
    // dorsal sepal + two twisted lateral petals (maroon), inflated pouch (pink) below
    for(const dir of [-1,1]){ taper(b,cx,headY-1*scale, cx+dir*5*scale, headY-4*scale, 1.1,0.4,'BLOOM2'); dot(b,cx+dir*5*scale,headY-4*scale,'BLOOM2','sh'); dot(b,cx+dir*3*scale,headY-2.5*scale,'BLOOM2','hi'); }
    taper(b,cx,headY-1*scale, cx, headY-6*scale, 1.3,0.5,'BLOOM2'); dot(b,cx,headY-6*scale,'BLOOM2','hi');
    ell(b,cx,headY+2*scale, 3.4*scale, 3.0*scale,'BLOOM');            // inflated pouch
    dot(b,cx-1,headY+1*scale,'BLOOM','hi'); dot(b,cx-1,headY,'BLOOM','hi');
    dot(b,cx,headY+3*scale,'BLOOM','sh'); dot(b,cx+1,headY+3*scale,'BLOOM','sh');
    ell(b,cx,headY,1.4*scale,0.8*scale,'BLOOM2');                     // dark pouch opening
    line(b,cx-1,headY+1*scale,cx+1,headY+3*scale,'BLOOM2','sh',1);    // vein
  }

  function drawGround(b,s,cx,baseY,scale,rng,spread){
    // low mound: trefoil leaves + clover poms + buttercup dots. spread = half-width in px.
    const n=Math.max(3,Math.round(spread*0.6));
    for(let i=0;i<n;i++){ const x=cx+(rng()*2-1)*spread, y=baseY-rng()*4*scale;
      // trefoil leaf
      for(let k=0;k<3;k++){ const an=k/3*Math.PI*2; const lx=x+Math.cos(an)*1.6*scale, ly=y+Math.sin(an)*1.2*scale; ell(b,lx,ly, 1.3*scale,1.0*scale,'LEAF'); dot(b,lx,ly,'LEAF2','sh'); }
      dot(b,x,y,'LEAF2','sh');
    }
    // heads
    for(let i=0;i<n;i++){ const x=cx+(rng()*2-1)*spread, y=baseY-2*scale-rng()*5*scale;
      if(rng()<0.5){ const cm=rng()<0.5?'CLOVER':'WHITE'; ell(b,x,y,1.8*scale,1.6*scale,cm); for(let k=0;k<6;k++){ const an=k/6*Math.PI*2; dot(b,x+Math.cos(an)*1.6*scale, y+Math.sin(an)*1.4*scale, cm, k%2?'hi':'sh'); } dot(b,x-1,y-1,cm,'hi'); }
      else { ell(b,x,y,1.4*scale,1.3*scale,'BLOOM'); dot(b,x,y,'ACCENT','sh'); dot(b,x-1,y-1,'BLOOM','hi'); dot(b,x-1,y,'WHITE','hi'); }
    }
  }

  function drawPlant(b,s,cx,baseY,scale,stage,rng){
    switch(s.arch){
      case 'spike': return drawSpike(b,s,cx,baseY,scale,stage,rng);
      case 'umbel': return drawUmbel(b,s,cx,baseY,scale,stage,rng);
      case 'radial':return drawRadial(b,s,cx,baseY,scale,stage,rng);
      case 'iris':  return drawIris(b,s,cx,baseY,scale,stage,rng);
      case 'orchid':return drawOrchid(b,s,cx,baseY,scale,stage,rng);
      case 'ground':return drawGround(b,s,cx,baseY,scale,rng,7*scale);
    }
  }

  // ---- sway shear: shift each scanline horizontally, pinned at baseY -------
  function swayShear(rgba,w,h,baseY,plantPx,frame,amp){
    if(frame==null) return rgba;
    const A=(amp==null?2.6:amp), curve = Math.sin(frame/SWAY*Math.PI*2);   // 0,+,0,-
    const out=new Uint8ClampedArray(w*h*4);
    for(let y=0;y<h;y++){ const hf=Math.max(0,Math.min(1,(baseY-y)/plantPx)); const dx=Math.round(A*curve*Math.pow(hf,1.3));
      for(let x=0;x<w;x++){ const sx=x-dx; if(sx<0||sx>=w) continue; const si=(y*w+sx)*4, di=(y*w+x)*4; out[di]=rgba[si]; out[di+1]=rgba[si+1]; out[di+2]=rgba[si+2]; out[di+3]=rgba[si+3]; } }
    return out;
  }

  // ---- public renders ------------------------------------------------------
  function renderSingle(key, stageIdx, swayFrame){
    const s=byKey[key]||SPECIES[0], T=TIERS.single, b=new Buf(T.w,T.h);
    const rng=mulberry(hashKey(key)+ (stageIdx||0)*13);
    drawPlant(b,s,T.baseX,T.baseY, 1.0, STAGES[stageIdx||0], rng);
    shade(b); outline(b);
    let rgba=colourize(b, rampsFor(s));
    rgba=swayShear(rgba,T.w,T.h,T.baseY, s.ph, swayFrame);
    return { w:T.w, h:T.h, rgba };
  }
  function renderClump(key, swayFrame){
    const s=byKey[key]||SPECIES[0], T=TIERS.clump, b=new Buf(T.w,T.h);
    const rng=mulberry(hashKey(key)+7);
    const offs = s.arch==='ground' ? [[0,1.0]] : [[-9,0.86],[9,0.82],[0,1.0],[-3,0.7]];
    // draw back (smaller/top) first — sort by scale asc so tallest in front
    offs.map(o=>({dx:o[0],sc:o[1]})).sort((a,c)=>a.sc-c.sc).forEach(o=>{
      drawPlant(b,s,T.baseX+o.dx, T.baseY - (1-o.sc)*4, o.sc, 'full', rng);
    });
    shade(b); outline(b);
    let rgba=colourize(b, rampsFor(s));
    rgba=swayShear(rgba,T.w,T.h,T.baseY, s.ph, swayFrame);
    return { w:T.w, h:T.h, rgba };
  }
  function renderPatch(key, variant, swayFrame){
    const s=byKey[key]||SPECIES[0], T=TIERS.patch, b=new Buf(T.w,T.h);
    const rng=mulberry(hashKey(key)+ (variant||0)*101 + 3);
    // a low drift: many short plants across the tile (atmospheric, ~½m tall)
    const isLupin = /^Lupin/.test(key);
    const nb = s.arch==='ground' ? 1 : 7;
    if(s.arch==='ground'){ drawGround(b,s,T.baseX,T.baseY+2, 0.9, rng, 18); }
    else for(let i=0;i<nb;i++){ const x=4+rng()*(T.w-8), by=T.baseY - rng()*3, sc=0.34+rng()*0.16;
      // for lupin mixed patch, vary the bloom colour per stalk
      let ss=s; if(isLupin){ const cols=['#7d5aa8','#d17aa6','#5a72b8','#e9edea']; ss=Object.assign({},s,{bloom:cols[Math.floor(rng()*cols.length)]}); }
      const bb=new Buf(T.w,T.h); drawPlant(bb,ss,x,by,sc,'full',rng); shade(bb); outline(bb);
      const rr=colourize(bb,rampsFor(ss)); stampOver(b,bb, rr);   // composite with its own colours
    }
    let rgba;
    if(s.arch==='ground'){ shade(b); outline(b); rgba=colourize(b, rampsFor(s)); }
    else rgba=b.__rgba || new Uint8ClampedArray(T.w*T.h*4);  // built up by stampOver
    rgba=swayShear(rgba,T.w,T.h,T.baseY, 16, swayFrame, 1.5);   // gentle breeze over the low mat
    return { w:T.w, h:T.h, rgba };
  }

  // patch helper: composite a coloured sub-render into b.__rgba (painter's order)
  function stampOver(b, bb, rgba){
    if(!b.__rgba) b.__rgba=new Uint8ClampedArray(b.w*b.h*4);
    for(let i=0;i<b.w*b.h;i++){ if(rgba[i*4+3]){ b.__rgba[i*4]=rgba[i*4]; b.__rgba[i*4+1]=rgba[i*4+1]; b.__rgba[i*4+2]=rgba[i*4+2]; b.__rgba[i*4+3]=255; } }
  }

  function hashKey(k){ let h=2166136261; for(let i=0;i<k.length;i++){ h^=k.charCodeAt(i); h=Math.imul(h,16777619); } return h>>>0; }

  root.Flowers = { SPECIES, byKey, TIERS, STAGES, SWAY, GREEN, DUNE,
    renderSingle, renderClump, renderPatch };
})(typeof globalThis!=='undefined'?globalThis:window);
