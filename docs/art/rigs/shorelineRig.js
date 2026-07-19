/* Hidden Harbours — parametric SHORELINE FINDS. Beachcomber dressing for the tideline,
   dunes, rocky shore, tide pools, and wharf. Matches the shore-rock conventions:
   32 px = 1 m · no AA · transparent PNG · binary alpha · upper-left key light ·
   soft dark keyline (#20211c, sits on sand) · CENTRE-ish pivot (each item lies flat where
   it stranded). Most finds are small (10–30 cm), so canvases are tight and every pixel counts.

   Each item = ONE object drawn ¾ flat-on-the-sand, with:
     2 SHAPE VARIANTS (a/b) — for natural-looking scatters (seeded RNG per item+variant).
     WET / DRY treatment — wet = darker, glossier (tideline, a specular glint + damp rim);
                            dry = matte, paler, sun-bleached (upper beach / dune).
   Sheet layout per item: cols = variant (a,b) · rows = state (wet, dry) → 2×2 grid.

   12 finds: Driftwood · SoftShellClam · Mussel · Scallop · Periwinkle · SeaGlass ·
             SandDollar · CrabMoult · GullFeather · Bone · Oyster · Starfish.

   Palette: bleached driftwood greys, shell creams, muted sea-glass tints — all muted to sit
   with the sand/rock tiles.

   Exposes globalThis.Shoreline:
     ITEMS [{key,name,note,w,h}]  STATES ['wet','dry']  VARIANTS 2
     render(key, variant, state) -> {w,h,rgba}
   Runs in the run_script sandbox (bake) and the browser (live preview). */
(function (root) {
  const KEYLINE = '#20211c';
  const STATES = ['wet','dry'];
  const VARIANTS = 2;

  // ---- colour helpers ------------------------------------------------------
  function h2r(h){ return [parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]; }
  function r2h(r){ return '#'+r.map(v=>Math.max(0,Math.min(255,Math.round(v))).toString(16).padStart(2,'0')).join(''); }
  function mix(a,b,t){ const A=h2r(a),B=h2r(b); return r2h([0,1,2].map(i=>A[i]+(B[i]-A[i])*t)); }
  function light(hex,f){ return f>=0 ? mix(hex,'#ffffff',f) : mix(hex,'#000000',-f); }
  function ramp(base){ return { hi:light(base,0.24), mid:base, sh:light(base,-0.22), dp:light(base,-0.42) }; }

  // base (dry) material mids — wet darkens+saturates at colourize time
  const MAT = {
    WOOD:'#b3a68c', WOOD2:'#8f8368', BARK:'#6e6250',
    SHELL:'#e6ddc8', SHELL2:'#cbbf a4'.replace(' ',''), SHELLD:'#b09a78',
    MUSSEL:'#3f4a63', MUSSELH:'#5f6f8c', PEARL:'#c9c6cf',
    SCALLOP:'#e2b98f', PERI:'#7a6a4a', WHELK:'#d8c39c',
    GLASS_G:'#7fae9a', GLASS_B:'#7fa2b8', GLASS_W:'#d7ddd6',
    DOLLAR:'#e3dcc9', CRAB:'#b6613a', CRABH:'#d07a4c',
    FEATH:'#e9e6de', FEATHD:'#b9b6ac', QUILL:'#8a8574',
    BONE:'#ded7c4', BONE2:'#c2b9a2', STAR:'#c96a4a', STARH:'#e08a5c',
    SAND:'#c9b48a',
  };

  const ITEMS = [
    { key:'Driftwood', name:'Driftwood', note:'Bleached log / plank / branch', w:32, h:16 },
    { key:'SoftShellClam', name:'Soft-shell Clam', note:'The digging clam (steamer)', w:18, h:14 },
    { key:'Mussel', name:'Blue Mussel', note:'Wedge shell / small cluster', w:18, h:14 },
    { key:'Scallop', name:'Scallop Shell', note:'Ribbed fan', w:18, h:16 },
    { key:'Periwinkle', name:'Periwinkle & Whelk', note:'Coiled snail shells', w:16, h:14 },
    { key:'SeaGlass', name:'Sea Glass', note:'Frosted green / blue / white', w:14, h:12 },
    { key:'SandDollar', name:'Sand Dollar', note:'Petal star on the disc', w:16, h:16 },
    { key:'CrabMoult', name:'Crab Moult', note:'Empty rock-crab carapace', w:20, h:16 },
    { key:'GullFeather', name:'Gull Feather', note:'White with grey vane', w:22, h:14 },
    { key:'Bone', name:'Weathered Bone', note:'Sun-bleached driftbone', w:22, h:12 },
    { key:'Oyster', name:'Oyster & Razor', note:'Rough cup / long razor shell', w:22, h:16 },
    { key:'Starfish', name:'Starfish', note:'Five-arm sea star', w:20, h:20 },
  ];
  const byKey={}; ITEMS.forEach(it=>byKey[it.key]=it);

  // ---- buffer + primitives -------------------------------------------------
  function Buf(w,h){ this.w=w; this.h=h; this.key=new Array(w*h).fill(''); this.mat=new Array(w*h).fill(null); }
  Buf.prototype.i=function(x,y){ return y*this.w+x; };
  Buf.prototype.in=function(x,y){ return x>=0&&x<this.w&&y>=0&&y<this.h; };
  Buf.prototype.put=function(x,y,m,k){ x=Math.round(x); y=Math.round(y); if(!this.in(x,y))return; this.key[this.i(x,y)]=k||'mid'; this.mat[this.i(x,y)]=m; };
  function ell(b,cx,cy,rx,ry,m,k){ for(let y=Math.floor(cy-ry);y<=Math.ceil(cy+ry);y++)for(let x=Math.floor(cx-rx);x<=Math.ceil(cx+rx);x++){ const dx=(x-cx)/(rx+.001),dy=(y-cy)/(ry+.001); if(dx*dx+dy*dy<=1) b.put(x,y,m,k); } }
  function ellRot(b,cx,cy,rx,ry,ang,m,k){ const ca=Math.cos(ang),sa=Math.sin(ang),R=Math.max(rx,ry); for(let y=Math.floor(cy-R);y<=Math.ceil(cy+R);y++)for(let x=Math.floor(cx-R);x<=Math.ceil(cx+R);x++){ const dx=x-cx,dy=y-cy; const u=(dx*ca+dy*sa)/(rx+.001), v=(-dx*sa+dy*ca)/(ry+.001); if(u*u+v*v<=1) b.put(x,y,m,k); } }
  function dot(b,x,y,m,k){ b.put(x,y,m,k); }
  function line(b,x0,y0,x1,y1,m,k,th){ const dx=x1-x0,dy=y1-y0,n=Math.max(Math.abs(dx),Math.abs(dy))||1; for(let i=0;i<=n;i++){ const x=x0+dx*i/n,y=y0+dy*i/n; if(th){ for(let o=-(th-1)/2;o<=(th-1)/2;o++) b.put(x,y+o,m,k);} else b.put(x,y,m,k);} }
  function taper(b,x0,y0,x1,y1,r0,r1,m,k){ const dx=x1-x0,dy=y1-y0,L2=dx*dx+dy*dy||1; const mnx=Math.floor(Math.min(x0,x1)-Math.max(r0,r1)),mxx=Math.ceil(Math.max(x0,x1)+Math.max(r0,r1)),mny=Math.floor(Math.min(y0,y1)-Math.max(r0,r1)),mxy=Math.ceil(Math.max(y0,y1)+Math.max(r0,r1)); for(let y=mny;y<=mxy;y++)for(let x=mnx;x<=mxx;x++){ let t=((x-x0)*dx+(y-y0)*dy)/L2; t=Math.max(0,Math.min(1,t)); const px=x0+dx*t,py=y0+dy*t; if(Math.hypot(x-px,y-py)<=r0+(r1-r0)*t) b.put(x,y,m,k);} }
  function mulberry(a){ return function(){ a|=0;a=a+0x6D2B79F5|0;let t=Math.imul(a^a>>>15,1|a);t=t+Math.imul(t^t>>>7,61|t)^t;return((t^t>>>14)>>>0)/4294967296; }; }
  function hashKey(k){ let h=2166136261; for(let i=0;i<k.length;i++){ h^=k.charCodeAt(i); h=Math.imul(h,16777619);} return h>>>0; }

  // ---- shade / outline / colourize -----------------------------------------
  function shade(b){ const src=b.key.slice(),mat=b.mat; for(let y=0;y<b.h;y++)for(let x=0;x<b.w;x++){ const i=b.i(x,y); if(src[i]!=='mid')continue; const m=mat[i];
    const up=y>0&&src[b.i(x,y-1)]&&mat[b.i(x,y-1)]===m, lf=x>0&&src[b.i(x-1,y)]&&mat[b.i(x-1,y)]===m, dn=y<b.h-1&&src[b.i(x,y+1)]&&mat[b.i(x,y+1)]===m, rt=x<b.w-1&&src[b.i(x+1,y)]&&mat[b.i(x+1,y)]===m;
    if(!up||!lf) b.key[i]='hi'; else if(!dn||!rt) b.key[i]='sh'; } }
  function outline(b){ const add=[]; for(let y=0;y<b.h;y++)for(let x=0;x<b.w;x++){ if(b.key[b.i(x,y)])continue; for(const[dx,dy]of[[1,0],[-1,0],[0,1],[0,-1],[1,1],[-1,-1],[1,-1],[-1,1]]) if(b.in(x+dx,y+dy)&&b.key[b.i(x+dx,y+dy)]&&b.mat[b.i(x+dx,y+dy)]!=='__out'){ add.push([x,y]); break; } } for(const[x,y]of add){ b.key[b.i(x,y)]='out'; b.mat[b.i(x,y)]='__out'; } }

  // wet: darker + slightly bluer/saturated; dry: paler, matte
  function colourize(b, state){
    const out=new Uint8ClampedArray(b.w*b.h*4);
    for(let i=0;i<b.w*b.h;i++){ const k=b.key[i]; if(!k){ out[i*4+3]=0; continue; }
      const m=b.mat[i]; let hex;
      if(m==='__out'||k==='out') hex=KEYLINE;
      else { let base=MAT[m]||'#c9b48a';
        if(state==='wet') base=mix(light(base,-0.18), '#22303a', 0.12);   // darken + faint cool cast
        else base=light(base,0.05);                                       // dry: a touch paler/matte
        const rr=ramp(base); hex = k==='hi'?rr.hi : k==='sh'?rr.sh : k==='dp'?rr.dp : rr.mid; }
      const c=h2r(hex); out[i*4]=c[0]; out[i*4+1]=c[1]; out[i*4+2]=c[2]; out[i*4+3]=255; }
    // wet specular glint: brighten one upper-left pixel cluster on the tallest mass
    if(state==='wet'){ addGlint(b,out); }
    return out;
  }
  function addGlint(b,out){ // find topmost filled run, drop a 2px hot glint just below its up-left edge
    for(let y=0;y<b.h;y++){ let x0=-1; for(let x=0;x<b.w;x++) if(b.key[b.i(x,y)]&&b.mat[b.i(x,y)]!=='__out'){ x0=x; break; }
      if(x0>=0){ for(const[gx,gy]of[[x0+1,y+1],[x0+2,y+1]]){ if(b.in(gx,gy)&&b.key[b.i(gx,gy)]&&b.mat[b.i(gx,gy)]!=='__out'){ const i=b.i(gx,gy); out[i*4]=Math.min(255,out[i*4]+55); out[i*4+1]=Math.min(255,out[i*4+1]+55); out[i*4+2]=Math.min(255,out[i*4+2]+58);} } return; } }
  }

  // ==== per-item draw =======================================================
  function draw(b, key, v, rng){
    const cx=b.w/2, cy=b.h/2;
    switch(key){
      case 'Driftwood': {
        const form=v; // 0=log, 1=plank/branch
        if(form===0){ const y=cy+1; taper(b,3,y+ (rng()<.5?1:0), b.w-3, y-1, 3.0,2.2,'WOOD');
          for(let x=4;x<b.w-3;x+=2) dot(b,x, y-1+Math.round((rng()-0.5)*1.5),'WOOD2','sh');   // grain
          ell(b,4,y,2.4,2.6,'BARK'); ell(b,b.w-4,y,2.2,2.4,'BARK');                            // cut ends
          dot(b,4,y-1,'WOOD','hi'); dot(b,b.w-4,y-1,'WOOD','hi');
        } else { const y=cy; line(b,2,y+2,b.w-2,y-1,'WOOD','',3);                              // plank
          for(let x=3;x<b.w-2;x+=3) dot(b,x, y+2-Math.round((x-2)/(b.w-4)*3),'WOOD2','sh');
          taper(b,b.w-6,y-2, b.w-1,y-4, 1.2,0.4,'BARK');                                       // a stray twig
        }
        break; }
      case 'SoftShellClam': {
        const ang=(v?0.5:-0.4);
        ellRot(b,cx,cy,6,4.4,ang,'SHELL');
        for(let r=1;r<5;r++) ellRot(b,cx,cy,6-r*1.1,4.4-r*0.8,ang,'SHELL', r%2?'sh':undefined);  // concentric growth lines
        ell(b,cx-1,cy-1,1.4,1.2,'SHELL','hi'); dot(b, cx- (v?3:-3), cy+2,'SHELLD','sh');       // umbo
        break; }
      case 'Mussel': {
        if(v===0){ ellRot(b,cx,cy,3.4,6.2, 0.5,'MUSSEL'); dot(b,cx-1,cy-2,'MUSSELH','hi'); dot(b,cx,cy,'PEARL','sh'); line(b,cx-2,cy-4,cx+2,cy+4,'MUSSEL','sh',1); }
        else { // small cluster of 3
          for(const [ox,oy,a] of [[-3,1,0.3],[2,-1,-0.4],[0,3,0.8]]){ ellRot(b,cx+ox,cy+oy,2.6,4.8,a,'MUSSEL'); dot(b,cx+ox-1,cy+oy-1,'MUSSELH','hi'); } }
        break; }
      case 'Scallop': {
        const baseY=cy+5; ell(b,cx,baseY-4,7,6,'SCALLOP');                    // fan body
        for(let x=-6;x<=6;x++){ const h=Math.sqrt(Math.max(0,1-(x/7)**2))*6; b.put(cx+x,baseY-h*0, 'SCALLOP'); }
        for(let r=-3;r<=3;r++){ const ang=r/3*0.9; line(b,cx,baseY, cx+Math.sin(ang)*6.5, baseY-Math.cos(ang)*10,'SCALLOP', r%2?'sh':'mid',1); }  // ribs
        ell(b,cx,baseY-1,2.2,1.6,'SHELLD');                                   // hinge ears
        dot(b,cx-3,baseY-8,'SCALLOP','hi');
        break; }
      case 'Periwinkle': {
        if(v===0){ // periwinkle: small round snail with banded whorls
          ell(b,cx,cy+1,4.8,4.4,'PERI');
          const ax=cx+1.8, ay=cy-1.2;                 // off-centre apex
          for(let t=0;t<5;t++){ const rr=4.6-t*0.95; ellRot(b, cx-(4.6-rr)*0.5, cy+1-(4.6-rr)*0.4, rr,rr*0.9, 0.2, 'WHELK', t%2?'sh':'mid'); }
          dot(b,ax,ay,'WHELK','hi'); dot(b,ax-1,ay,'PERI','sh');
          ell(b,cx-3,cy+3,1.5,1.1,'WHELK','hi');       // pale aperture lip
          dot(b,cx-4,cy+3,'PERI','sh');
        } else { // whelk: tall pointed spire
          taper(b,cx-1,cy+6, cx+2,cy-6, 4.6,0.6,'WHELK');   // body → spire
          for(let t=0;t<6;t++){ const yy=cy+4-t*2, rr=4.2-t*0.65; ellRot(b,cx-1+t*0.5,yy, rr,rr*0.7, 0.0,'WHELK', t%2?'sh':undefined); line(b,cx-1-rr+t*0.5,yy, cx-1+rr+t*0.5,yy,'PERI','sh',1); }  // whorl ridges
          ell(b,cx-3,cy+4,1.6,1.2,'WHELK','hi');      // aperture
          taper(b,cx-2,cy+5, cx-4,cy+7, 1.0,0.3,'PERI');   // siphon canal
          dot(b,cx+1,cy-5,'WHELK','hi');
        }
        break; }
      case 'SeaGlass': {
        const tint=['GLASS_G','GLASS_B','GLASS_W'][v===0?0:(rng()<.5?1:2)];
        ellRot(b,cx,cy, 4+rng(),3+rng(), rng()*1.5,tint);
        // frosted: scatter a few paler pixels, rounded corners already from ellipse
        for(let i=0;i<4;i++) dot(b,cx+Math.round((rng()-0.5)*5), cy+Math.round((rng()-0.5)*4), tint,'hi');
        dot(b,cx-1,cy-1,tint,'hi');
        break; }
      case 'SandDollar': {
        ell(b,cx,cy,6.5,6.2,'DOLLAR');
        for(let a=0;a<5;a++){ const an=a/5*Math.PI*2 - Math.PI/2; const ex=cx+Math.cos(an)*3.2, ey=cy+Math.sin(an)*3.2;
          for(let s=0;s<4;s++) dot(b,cx+Math.cos(an)*s*0.9, cy+Math.sin(an)*s*0.9,'DOLLAR','sh'); }   // 5-petal
        dot(b,cx,cy,'DOLLAR','sh'); dot(b,cx-2,cy-2,'DOLLAR','hi');
        break; }
      case 'CrabMoult': {
        ell(b,cx,cy+1,7,5,'CRAB');                              // carapace
        for(const s of [-1,1]){ for(const d of [2.2,3.4,4.4]) dot(b,cx+s*6, cy+ d-2,'CRAB','sh'); }  // leg stubs
        for(const s of [-1,1]) ell(b,cx+s*7,cy-3,1.8,1.4,'CRAB');   // claw hint
        for(let i=-2;i<=2;i++) dot(b,cx+i*2, cy-2,'CRABH','hi');    // front margin
        dot(b,cx-2,cy-1,'CRABH','hi');
        break; }
      case 'GullFeather': {
        // slim curved feather: bare calamus at base → pointed tip, vane kept thin so the shaft reads.
        const flip=v?-1:1;
        const bx=2, by=v?3:b.h-3, tx=b.w-2, ty=v?b.h-3:3;
        const seg=26;
        const pt=(t)=>{ const x=bx+(tx-bx)*t, y=by+(ty-by)*t - flip*Math.sin(t*Math.PI)*2.0; return [x,y]; };
        // vane first (so the shaft paints on top)
        for(let i=0;i<seg;i++){ const t=i/(seg-1); const [px,py]=pt(t);
          const [ax,ay]=pt(Math.min(1,t+0.03)); let nx=-(ay-py), ny=(ax-px); const L=Math.hypot(nx,ny)||1; nx/=L; ny/=L;
          if(t<0.16) continue;                                  // bare quill near base
          const grow=Math.sin((t-0.1)*Math.PI/0.95);            // fat around 55%, thin to a point
          const wUp=Math.max(0,(0.5+2.7*grow)), wDn=Math.max(0,(0.4+1.9*grow));   // asymmetric, thinner
          for(let s=1;s<=Math.round(wUp);s++){ const edge=s>=Math.round(wUp); b.put(px+nx*s, py+ny*s, edge?'FEATHD':'FEATH', edge?'sh':undefined);
            if(edge && i%2===0) b.put(px+nx*(s+0.4), py+ny*(s+0.4),'FEATHD','sh'); }   // barb ticks along the trailing edge
          for(let s=1;s<=Math.round(wDn);s++){ const edge=s>=Math.round(wDn); b.put(px-nx*s, py-ny*s, 'FEATHD', edge?'sh':undefined); }
        }
        // rachis on top — a continuous quill line that stays visible
        for(let i=0;i<seg;i++){ const t=i/(seg-1); const [px,py]=pt(t); b.put(px,py,'QUILL', t<0.2?'hi':undefined); if(t>0.2 && i%4===0) b.put(px,py,'QUILL','sh'); }
        taper(b, bx,by, pt(0.16)[0],pt(0.16)[1], 1.0,0.5,'QUILL');   // calamus
        dot(b,bx,by,'QUILL','hi');
        break; }
      case 'Bone': {
        const y=cy; taper(b,4,y,b.w-4,y, 1.6,1.6,'BONE');       // shaft
        for(const ex of [3,b.w-3]) for(const oy of [-2,2]) ell(b,ex,y+oy,2.0,1.8,'BONE');   // knobbed ends
        for(let x=6;x<b.w-6;x+=3) dot(b,x,y-1,'BONE','hi');
        dot(b,5,y-2,'BONE','hi'); dot(b,b.w-5,y-2,'BONE','hi');
        if(v===1) for(let x=5;x<b.w-5;x+=4) dot(b,x,y+1,'BONE2','sh');  // weather cracks
        break; }
      case 'Oyster': {
        if(v===0){ // rough cupped oyster
          ellRot(b,cx,cy,7,5.4,0.2,'SHELLD');
          for(let r=0;r<5;r++){ const rr=1+r*1.2; ellRot(b,cx-1,cy-0.5,7-rr,5.4-rr*0.7,0.2,'SHELL', r%2?'sh':undefined); }
          dot(b,cx-2,cy-2,'SHELL','hi'); dot(b,cx+3,cy+2,'SHELLD','sh');
        } else { // long razor clam
          taper(b,cx-9,cy+3,cx+9,cy-3, 2.0,1.6,'WHELK');
          for(let x=-8;x<=8;x+=2) dot(b,cx+x, cy - x*0.33,'SHELLD','sh');
          line(b,cx-8,cy+3,cx+8,cy-3,'SHELL','hi',1);
        }
        break; }
      case 'Starfish': {
        for(let a=0;a<5;a++){ const an=a/5*Math.PI*2 - Math.PI/2 + (v?0.3:0); taper(b,cx,cy, cx+Math.cos(an)*9, cy+Math.sin(an)*9, 3.0,0.7,'STAR'); }
        ell(b,cx,cy,3.2,3.0,'STAR');
        for(let a=0;a<5;a++){ const an=a/5*Math.PI*2 - Math.PI/2 + (v?0.3:0); for(let s=1;s<8;s++) if(s%2) dot(b,cx+Math.cos(an)*s, cy+Math.sin(an)*s,'STAR','sh'); }  // ridge
        dot(b,cx-1,cy-1,'STARH','hi'); dot(b,cx,cy,'STARH','hi');
        break; }
    }
  }

  function render(key, variant, state){
    const it=byKey[key]||ITEMS[0], b=new Buf(it.w,it.h);
    const rng=mulberry(hashKey(key)+ (variant||0)*137 + 5);
    draw(b, key, variant||0, rng);
    shade(b); outline(b);
    return { w:it.w, h:it.h, rgba: colourize(b, state||'dry') };
  }

  root.Shoreline = { ITEMS, byKey, STATES, VARIANTS, render };
})(typeof globalThis!=='undefined'?globalThis:window);
