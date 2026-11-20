/* COAST SPECIES rig (2026-11-20): the six current-region species that had no art of
   their own (docs/art/fish-sprites-owed.md), drawn by the catch pass-2 rigs and nothing else.
   ADDITIVE ONLY. fishIsoRig2.js, crustaceanRig2.js and shellfishRig2.js are not edited and their
   ORDER / KINDS lists are not touched, so every cell they drew before draws byte for byte the same.
   This file only adds:
   · three finfish to FishIso2.SPECIES (smelt, tomcod, cunner) on the rig's own loft, ramps, light,
     dither, poses and mass law, with a LOOK entry each so the plate look draws the gill cover and
     the eye where the body is deep enough for them (never at world scale: these fish are 5-10 px);
   · their marks (mottling, the smelt's band edge, the tomcod's barbel), painted after the rig's own
     paint on the pixels the rig left dry, so a wet (tinted) water cell is the rig's alone;
   · ICON: one deck lay (row 2, frame 0, as the tote and the other icons) per species at an ICON
     scale, so the catch card shows the fish with its marks rather than a 7 px world sprite scaled up;
   · the green crab, the quahog and the surf clam, as recolours and reshapes of the rig's crab and clam
     drawings (greenCrab(), shell()), drawn at deck and icon size.
   Exposes globalThis.CoastSpecies = { ORDER, ICON, LOOK, SPECIES, iconCell, greenCrab, crabDeckSheet,
   shellIcon, PAL }. Needs FishIso2 (and Crustacean2 / Shellfish2 for the crab and the clams). */
(function (root) {
  const F = root.FishIso2;
  if (!F) throw new Error('coastSpeciesRig: FishIso2 must load first');

  // ---- the three finfish ------------------------------------------------------------------------
  // len/girth/flat as the rig's own; mass = len x girth^2 x scale^3 x 390 x massK (FishIso2.hold),
  // so each def's weight band lands on a real length: smelt 0.15-0.26 m, tomcod 0.18-0.32, cunner 0.17-0.31.
  const SPECIES = {
    // a little silver pencil: slim, translucent olive back, a bright silver band, white belly
    smelt:  { label:'RAINBOW SMELT', len:0.20, girth:0.017, flat:0.55, stripes:'none', dorsals:1, range:[0.76,1.30], massK:2,
              shoal:true, flash:true, spine:'flank',
              back:'#55704f', flank:'#9fb0ab', belly:'#dfe5e3', fin:'#5d7166', band:'#e4ecf2', bandEdge:'#7f9a72' },
    // a cod in miniature: olive-brown, mottled darker, yellowish-olive sides, pale belly, a chin barbel
    tomcod: { label:'ATLANTIC TOMCOD', len:0.24, girth:0.036, flat:0.72, stripes:'none', dorsals:2, range:[0.74,1.35], massK:1,
              back:'#5b5431', flank:'#7f7748', belly:'#aa9f6e', fin:'#4a4428', spots:'#38331c', mottle:0.30, barbel:true, spine:'back' },
    // a stout wrasse: deep, compact, mottled brown-green with rust, a paler belly
    cunner: { label:'CUNNER', len:0.22, girth:0.050, flat:0.60, stripes:'none', dorsals:1, range:[0.78,1.41], massK:1,
              back:'#4a4f30', flank:'#5f5c3a', belly:'#837b55', fin:'#463c28', spots:'#5e3a22', mottle:0.42, spine:'back' },
  };
  const ORDER = ['smelt','tomcod','cunner'];
  const LOOK = {
    smelt:  ['the gill cover', 'the eye, its pupil and iris', 'the silver band, its upper edge'],
    tomcod: ['the gill cover', 'the eye, its pupil and iris', 'darker mottling over the back and the upper flank', 'the chin barbel'],
    cunner: ['the gill cover', 'the eye, its pupil and iris', 'darker blotches, brown-green and rust'],
  };
  // icon scale: the catch card's picture, about 40 px long (a cod's icon is 38)
  const ICON = { smelt:6.4, tomcod:5.2, cunner:5.6 };
  for (const k of ORDER){ F.SPECIES[k] = SPECIES[k]; F.LOOK[k] = LOOK[k]; }

  // ---- ramps, the rig's own law ------------------------------------------------------------------
  const hex2rgb=(h)=>[parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)];
  const rgb2hex=(r,g,b)=>'#'+[r,g,b].map(v=>Math.max(0,Math.min(255,Math.round(v))).toString(16).padStart(2,'0')).join('');
  const light=(hex,f)=>{ const [r,g,b]=hex2rgb(hex); return rgb2hex(r+(255-r)*f, g+(255-g)*f, b+(255-b)*f); };
  const dark =(hex,f)=>{ const [r,g,b]=hex2rgb(hex); return rgb2hex(r*f, g*f, b*f); };
  const mkRamp=(base)=>[dark(base,0.42), dark(base,0.66), base, light(base,0.17), light(base,0.34)];
  const hsh=(x,y,s)=>{ let h=(Math.imul(x,374761393)+Math.imul(y,668265263)+Math.imul(s||0,1274126177))|0;
    h=Math.imul(h^(h>>>13),1103515245); return ((h^(h>>>16))>>>0)/4294967296; };
  const rampIndex=(ramps)=>{ const m={}; for (const [name,r] of Object.entries(ramps)) r.forEach((c,i)=>{ m[c]={name,i}; }); return m; };

  // ---- the marks, on the dry pixels the rig drew ---------------------------------------------------
  // A pixel is dry when its colour is exactly one of the species' ramp steps (a wet one is mixed with the
  // water and keeps the rig's tint). Mottles are 2x2 blotches hashed in cell space, a ramp step down into
  // the spot ramp; the smelt's band takes the band colour on its lit flank pixels.
  function marks(sp, key, rgba, W, H){
    const R={ back:mkRamp(sp.back), flank:mkRamp(sp.flank), belly:mkRamp(sp.belly), fin:mkRamp(sp.fin), spot:mkRamp(sp.spots||sp.back) };
    const at=rampIndex({ back:R.back, flank:R.flank }), seed=key.length*31+key.charCodeAt(0);
    const get=(i)=>rgb2hex(rgba[i*4],rgba[i*4+1],rgba[i*4+2]);
    const put=(i,hex)=>{ const [r,g,b]=hex2rgb(hex); rgba[i*4]=r; rgba[i*4+1]=g; rgba[i*4+2]=b; };
    let n=0; for (let i=0;i<W*H;i++) if (rgba[i*4+3]===255) n++;
    if (sp.mottle){
      const blob=n>=120?2:1;                           // at world scale a blotch is one pixel
      for (let y=0;y<H;y++) for (let x=0;x<W;x++){ const i=y*W+x; if (rgba[i*4+3]!==255) continue;
        const e=at[get(i)]; if (!e) continue;
        const bx=Math.floor(x/blob), by=Math.floor(y/blob);
        if (hsh(bx,by,seed)>=sp.mottle) continue;
        put(i, R.spot[Math.max(0,Math.min(4,e.i-(e.name==='back'?1:0)))]); }
    }
    return rgba;
  }
  // the smelt's silver band on a side-on lay (the icon): in each column of the body, the pixels from 35% to
  // 60% of the way down take the band ramp at the step the rig shaded them, and the row above it the
  // olive edge, so the stripe reads as a line along the fish, not a recolour of it
  function band(sp, rgba, W, H){
    const B=mkRamp(sp.band), E=mkRamp(sp.bandEdge);
    const ramps={ back:mkRamp(sp.back), flank:mkRamp(sp.flank), belly:mkRamp(sp.belly) }, at=rampIndex(ramps);
    const get=(i)=>rgb2hex(rgba[i*4],rgba[i*4+1],rgba[i*4+2]);
    const put=(i,hex)=>{ const [r,g,b]=hex2rgb(hex); rgba[i*4]=r; rgba[i*4+1]=g; rgba[i*4+2]=b; };
    for (let x=0;x<W;x++){ let y0=-1, y1=-1;
      for (let y=0;y<H;y++){ const i=y*W+x; if (rgba[i*4+3]===255 && at[get(i)]){ if (y0<0) y0=y; y1=y; } }
      if (y0<0 || y1-y0<4) continue;
      const h=y1-y0+1, a=y0+Math.round(h*0.35), b=y0+Math.round(h*0.60);
      for (let y=a;y<=b;y++){ const i=y*W+x, e=at[get(i)]; if (e) put(i, B[Math.max(1,Math.min(4,e.i+1))]); }
      const i=(a-1)*W+x, e=at[get(i)]; if (e) put(i, E[Math.max(0,Math.min(4,e.i))]); }
    return rgba;
  }
  const render0=F.render;
  F.render=function(dir, opts){
    const out=render0(dir, opts), key=opts&&opts.species;
    if (!SPECIES[key] || (opts.look && opts.look!=='plate')) return out;
    return marks(SPECIES[key], key, out, F.W, F.H);
  };

  // ---- the icon cell: the deck lay at icon scale, with the barbel ----------------------------------
  // Row 2, frame 0, as FishIconBuilder cuts every other icon; the tomcod's barbel is a 2 px line under
  // the chin, found as the lowest body pixel nearest the snout.
  function iconCell(key){
    const sp=SPECIES[key]; if (!sp) return null;
    const rgba=F.render(2, { species:key, rest:'deck', frame:0, scale:ICON[key] });
    if (sp.band) band(sp, rgba, F.W, F.H);
    if (sp.barbel){
      const W=F.W, H=F.H; let sx=-1, sy=-1;
      // the snout: the opaque pixel furthest along the head (dir 2's deck lay has the head to the right)
      for (let x=W-1;x>=0 && sx<0;x--) for (let y=0;y<H;y++) if (rgba[(y*W+x)*4+3]===255){ sx=x; sy=y; break; }
      if (sx>=0){
        let bx=sx-2, by=-1; for (let y=H-1;y>=0;y--) if (rgba[(y*W+bx)*4+3]===255){ by=y; break; }
        const c=hex2rgb(mkRamp(sp.belly)[4]);
        for (const [dx,dy] of [[0,1],[0,2]]){ const x=bx+dx, y=by+dy; if (x<0||y<0||x>=W||y>=H) continue;
          const i=(y*W+x)*4; rgba[i]=c[0]; rgba[i+1]=c[1]; rgba[i+2]=c[2]; rgba[i+3]=255; }
      }
    }
    return rgba;
  }

  // ---- the green crab: the rig's crab, recoloured, the five teeth its own ------------------------------
  // The rock crab's ramps, as crustaceanRig2.js builds them from its P1.crab palette ([dp, sh, mid, hi, hi
  // lightened 0.22]); each step maps to the same step of the green crab's. The glint (hi halfway to white)
  // maps to the green's glint.
  const DEG2=Math.PI/180;
  const ramp5=(m)=>[m.dp,m.sh,m.mid,m.hi,m.top||light(m.hi,0.22)];
  const ROCK = { CARA:{mid:'#b25e3e',hi:'#cf7a52',sh:'#8a4530',dp:'#5f2c20'}, LEG:{mid:'#8f4630',hi:'#ad5a3c',sh:'#5a281d',dp:'#5a281d'},
                 RUST:{mid:'#ad5a3c',hi:'#cf7a52',sh:'#5a281d',dp:'#5a281d'}, CREAM:{mid:'#cdb890',hi:'#ece0c8',sh:'#9c7f57',dp:'#9c7f57'} };
  const GREEN = { CARA:{mid:'#304a22',hi:'#41602c',sh:'#223618',dp:'#14210e',top:'#55733a'}, LEG:{mid:'#3b4b23',hi:'#4f6231',sh:'#27321a',dp:'#27321a'},
                  RUST:{mid:'#4f6231',hi:'#537036',sh:'#27321a',dp:'#27321a'}, CREAM:{mid:'#b3ad55',hi:'#d3cf86',sh:'#86853a',dp:'#86853a'} };
  const MOTTLE='#8c8a3c';
  const crabMap=(()=>{ const m={};
    for (const k of ['LEG','RUST','CREAM','CARA']){ const a=ramp5(ROCK[k]), b=ramp5(GREEN[k]); a.forEach((c,i)=>{ m[c]=b[i]; }); }
    m[light(ROCK.CARA.hi,0.5)]=light(GREEN.CARA.hi,0.5); return m; })();
  const CARA_G=new Set(ramp5(GREEN.CARA).concat([light(GREEN.CARA.hi,0.5)]));
  function recolour(rgba, map){
    const keys=Object.keys(map).map(h=>[h,hex2rgb(h)]);
    for (let i=0;i<rgba.length/4;i++){ if (!rgba[i*4+3]) continue;
      let to=map[rgb2hex(rgba[i*4],rgba[i*4+1],rgba[i*4+2])];
      if (!to){ let best=1e9; for (const [k,c] of keys){ const d=(c[0]-rgba[i*4])**2+(c[1]-rgba[i*4+1])**2+(c[2]-rgba[i*4+2])**2; if (d<best){ best=d; to=map[k]; } } }
      const c=hex2rgb(to); rgba[i*4]=c[0]; rgba[i*4+1]=c[1]; rgba[i*4+2]=c[2]; }
    return rgba;
  }
  // yellow mottling on the shell: 2x2 blotches (1 px under 60 shell pixels), hashed in cell space
  function crabMottle(rgba, W, H){
    let n=0; for (let i=0;i<W*H;i++) if (rgba[i*4+3] && CARA_G.has(rgb2hex(rgba[i*4],rgba[i*4+1],rgba[i*4+2]))) n++;
    const blob=n>=60?2:1, c=hex2rgb(MOTTLE), dk=ramp5(GREEN.CARA)[0];
    for (let y=0;y<H;y++) for (let x=0;x<W;x++){ const i=y*W+x; if (!rgba[i*4+3]) continue;
      const h=rgb2hex(rgba[i*4],rgba[i*4+1],rgba[i*4+2]); if (!CARA_G.has(h) || h===dk) continue;
      if (hsh(Math.floor(x/blob),Math.floor(y/blob),23)<0.12){ rgba[i*4]=c[0]; rgba[i*4+1]=c[1]; rgba[i*4+2]=c[2]; } }
    return rgba;
  }
  // the five teeth, on the plate (from above, the front up): the shell's middle row is its widest run of
  // shell pixels; the rig's nine marginal teeth are cleared and five sharp ones a side drawn from the eye
  // socket to the widest point, with three low lobes between the eyes
  function fiveTeeth(rgba, W, H, s){
    const isC=(x,y)=>{ if (x<0||y<0||x>=W||y>=H) return false; const i=y*W+x; return rgba[i*4+3] && CARA_G.has(rgb2hex(rgba[i*4],rgba[i*4+1],rgba[i*4+2])); };
    const rx=0.065*32*s, ry=0.045*32*s;
    let best=0, cy=0, cx=0;
    for (let y=0;y<H;y++){ let run=0, x0=0; for (let x=0;x<=W;x++){ if (x<W && isC(x,y)){ if (!run) x0=x; run++; } else { if (run>best){ best=run; cy=y+0.5; cx=x0+run/2; } run=0; } } }
    const put=(x,y,hex)=>{ if (x<0||y<0||x>=W||y>=H) return; const i=(y*W+x)*4, c=hex2rgb(hex); rgba[i]=c[0]; rgba[i+1]=c[1]; rgba[i+2]=c[2]; rgba[i+3]=255; };
    const R=(x,y)=>Math.hypot((x+0.5-cx)/rx,(y+0.5-cy)/ry), A=(x,y)=>Math.atan2(cy-(y+0.5), Math.abs(x+0.5-cx))/DEG2;
    for (let y=0;y<H;y++) for (let x=0;x<W;x++) if (isC(x,y) && R(x,y)>1.0 && R(x,y)<1.14 && A(x,y)>2 && A(x,y)<50) rgba[(y*W+x)*4+3]=0;
    const G=ramp5(GREEN.CARA), tri=(p,fn)=>{ let x0=W,x1=0,y0=H,y1=0; for (const q of p){ x0=Math.min(x0,Math.floor(q[0])); x1=Math.max(x1,Math.ceil(q[0])); y0=Math.min(y0,Math.floor(q[1])); y1=Math.max(y1,Math.ceil(q[1])); }
      for (let y=Math.max(0,y0);y<=Math.min(H-1,y1);y++) for (let x=Math.max(0,x0);x<=Math.min(W-1,x1);x++){ const px=x+0.5, py=y+0.5; let ins=false;
        for (let a=0,b=p.length-1;a<p.length;b=a++){ const xa=p[a][0], ya=p[a][1], xb=p[b][0], yb=p[b][1]; if ((ya>py)!==(yb>py) && px<(xb-xa)*(py-ya)/(yb-ya)+xa) ins=!ins; }
        if (ins) fn(x,y); } };
    const at=(deg,r,sd)=>[cx+sd*Math.cos(deg*DEG2)*rx*r, cy-Math.sin(deg*DEG2)*ry*r];
    for (const sd of [-1,1]) for (let k=0;k<5;k++){                          // five a side, behind the eye (52 deg) to the widest (6 deg)
      const f0=52-46*k/5, f1=52-46*(k+1)/5, tip=f1+(f0-f1)*0.25;
      tri([at(f0,0.94,sd), at(tip,1.20,sd), at(f1,0.94,sd)], (x,y)=>{ if (!rgba[(y*W+x)*4+3] || R(x,y)<1.0) put(x,y, sd<0 ? G[3] : G[2]); });
      const n=at(f1,0.97,sd); put(Math.floor(n[0]),Math.floor(n[1]),G[0]); }
    for (const d of [-8,0,8]){ const p=at(90+d,1.04,1); put(Math.floor(p[0]),Math.floor(p[1]),G[2]); }   // the three lobes between the eyes
    return rgba;
  }
  function needCrab(){ if (!root.Crustacean2) throw new Error('coastSpeciesRig: Crustacean2 must load first'); return root.Crustacean2; }
  // a green crab cell: the rig's crab (opts as Crustacean2.render's), recoloured and mottled
  function greenCrab(opts){ const C=needCrab(); return crabMottle(recolour(C.render('crab', opts), crabMap), C.W, C.H); }
  // the icon: the rig's plate crab (from above, legs across) recoloured, the five teeth its own
  const CRAB_ICON=5.0;
  function greenCrabIcon(){ const C=needCrab(); const a=recolour(C.renderPlate('crab', CRAB_ICON), crabMap);
    fiveTeeth(a, C.PW, C.PH, CRAB_ICON); return { W:C.PW, H:C.PH, rgba:crabMottle(a, C.PW, C.PH) }; }
  // the pot-deck sheet, as the rock crab's (8 cells of 48 x 48: crawl 6, rear, defend): walk 0-3 and sidle
  // 0 and 2 (a crab crawls sideways), rear, defend, facing the camera (dir 0), at the deck's scale
  const CRAB_DECK=2.4, DECK_CELL=48;
  const DECK_POSES=[['walk',0],['walk',1],['walk',2],['walk',3],['sidle',0],['sidle',2],['rear',0],['defend',0]];
  function crabDeckSheet(){
    const C=needCrab(), cells=DECK_POSES.map(([pose,frame])=>greenCrab({ dir:0, pose, frame, scale:CRAB_DECK }));
    let x0=C.W,x1=0,y0=C.H,y1=0;
    for (const c of cells) for (let y=0;y<C.H;y++) for (let x=0;x<C.W;x++) if (c[(y*C.W+x)*4+3]){ x0=Math.min(x0,x); x1=Math.max(x1,x); y0=Math.min(y0,y); y1=Math.max(y1,y); }
    const ox=Math.round((x0+x1+1)/2-DECK_CELL/2), oy=Math.round((y0+y1+1)/2-DECK_CELL/2), N=DECK_POSES.length, Wd=DECK_CELL*N;
    const out=new Uint8ClampedArray(Wd*DECK_CELL*4);
    cells.forEach((c,k)=>{ for (let y=0;y<DECK_CELL;y++) for (let x=0;x<DECK_CELL;x++){ const sx=x+ox, sy=y+oy; if (sx<0||sy<0||sx>=C.W||sy>=C.H) continue;
      const si=(sy*C.W+sx)*4, di=(y*Wd+k*DECK_CELL+x)*4; for (let j=0;j<4;j++) out[di+j]=c[si+j]; } });
    return { W:Wd, H:DECK_CELL, rgba:out, poses:DECK_POSES };
  }

  // ---- the quahog and the surf clam: the rig's clam plate, reshaped and recoloured ---------------------
  // Shellfish2.renderPlate draws SIZES.clam's len x wid with the siphon out of the gape. A hard clam shows
  // no siphon, so for the call the siphon is set to nothing and the size to the species', then both are put
  // back. The clam's five tones map to the species'. The surf clam's dorsal margin is cut straight from the
  // beak to each end (triangular, thick at the hinge) with a brown skin at the margin; the quahog's ventral
  // lip takes a purple step.
  const SHELLS = {
    quahog:   { len:0.100, wid:0.086, scale:9.6, ramp:['#5d5752','#7b746c','#a59d92','#c9c2b6','#e2ddd2'], rim:'#6c5070' },
    surfclam: { len:0.160, wid:0.112, scale:7.6, ramp:['#7a6648','#9e8962','#cdbb92','#e6d9b6','#f4ecd6'], edge:'#6e5638', tri:true },
  };
  const CLAM_PAL=['#8a6a48','#9c7f57','#c9b083','#e7d8b0','#f3ead0'];       // shellfishRig2.js PAL.clam, verbatim
  function shellIcon(key){
    const S=root.Shellfish2; if (!S) throw new Error('coastSpeciesRig: Shellfish2 must load first');
    const sp=SHELLS[key], Z=S.SIZES.clam, P=S.PLATE_SIPHON, z0={ len:Z.len, wid:Z.wid }, p0=Object.assign({}, P);
    let a; try { Z.len=sp.len; Z.wid=sp.wid; P.out=0; P.base=0; P.tip=0; a=S.renderPlate('clam', sp.scale); }
    finally { Z.len=z0.len; Z.wid=z0.wid; Object.assign(P, p0); }
    const W=S.PW, H=S.PH, map={}; CLAM_PAL.forEach((c,i)=>{ map[c]=sp.ramp[i]; }); recolour(a, map);
    let x0=W,x1=0,y0=H,y1=0; for (let y=0;y<H;y++) for (let x=0;x<W;x++) if (a[(y*W+x)*4+3]){ x0=Math.min(x0,x); x1=Math.max(x1,x); y0=Math.min(y0,y); y1=Math.max(y1,y); }
    if (sp.tri){
      let bx=x0; for (let x=x0;x<=x1;x++) if (a[(y0*W+x)*4+3]){ bx=x; break; }
      const midY=y0+(y1-y0)*0.55;
      for (let y=y0;y<=y1;y++) for (let x=x0;x<=x1;x++){ const i=(y*W+x)*4; if (!a[i+3]) continue;
        const t=x<bx ? (bx-x)/Math.max(1,bx-x0) : (x-bx)/Math.max(1,x1-bx), lim=y0+t*(midY-y0)*0.92;
        if (y<lim) a[i+3]=0; }
      const e=hex2rgb(sp.edge);
      for (let y=y0;y<=y1;y++) for (let x=x0;x<=x1;x++){ const i=(y*W+x)*4; if (!a[i+3]) continue;
        const open=[[1,0],[-1,0],[0,1]].some(([dx,dy])=>{ const X=x+dx, Y=y+dy; return X<0||Y<0||X>=W||Y>=H||!a[(Y*W+X)*4+3]; });
        if (open && y>midY){ a[i]=e[0]; a[i+1]=e[1]; a[i+2]=e[2]; } }
    }
    if (sp.rim){
      const c=hex2rgb(sp.rim), cy=(y0+y1)/2;
      for (let y=Math.ceil(cy);y<=y1;y++) for (let x=x0;x<=x1;x++){ const i=(y*W+x)*4; if (!a[i+3]) continue;
        if (y+1>=H || !a[((y+1)*W+x)*4+3]){ if ((x+y)%3!==0){ a[i]=c[0]; a[i+1]=c[1]; a[i+2]=c[2]; } } }
    }
    return { W, H, rgba:a };
  }

  root.CoastSpecies = { ORDER, ICON, LOOK, SPECIES, iconCell, marks, mkRamp, greenCrab, greenCrabIcon, crabDeckSheet, shellIcon, SHELLS,
    CRAB_ICON, CRAB_DECK, DECK_POSES };
})(typeof window!=='undefined'?window:globalThis);
