/* Hidden Harbours — parametric LOBSTER/CRAB POTS. Two builds × wet/dry.
   ¾ front view to match the deck props. 32px = 1m. Upper-left key light. No AA.
   1px #171a14 keyline. KTC palette (Wood/Earth ramp + galvanised steel + tarred-net dark;
   rockweed + bone for the wet dressing) — nothing new invented.

   • wood — traditional half-round bow-top wooden-lath parlour trap.
   • wire — modern vinyl-coated wire-mesh trap (canonical rebuild of LobsterTrap.png).
   Each renders DRY or WET (darker/saturated + drips, sheen, rockweed, barnacles).

   Exposes globalThis.LobsterPots with:
     W, H, PAL, KINDS, render(kind, wet) -> Uint8ClampedArray(W*H*4)
   Works in the run_script sandbox (bake) and in the browser (live preview). */
(function (root) {
  const W = 44, H = 36;

  const HEX = {
    out:'#171a14',
    // wood laths (Wood/Earth ramp)
    wdHi:'#b9975f', wd:'#8c6a45', wdSh:'#63482f', wdDp:'#3a2a1c',
    // heavier frame timber (darker)
    frHi:'#7d5c3b', fr:'#5a4028', frSh:'#3a2a1a', frDp:'#241a11',
    // galvanised / vinyl wire (cool steel)
    wrHi:'#a6b6b8', wr:'#71807f', wrSh:'#49555a', wrDp:'#2c3539',
    // tarred net twine
    ntHi:'#6c6656', nt:'#474134', ntSh:'#2b271e',
    // rope bridle (bone-warm)
    rpHi:'#cdbe97', rp:'#a98f66', rpSh:'#7a6242',
    // wet dressing
    swHi:'#8a9a45', sw:'#5c6e2c', swSh:'#39471d',   // rockweed
    boHi:'#eadfca', bo:'#c9b791', boSh:'#9a875f',   // barnacle / bone
    wa:'#bcd2d4', waHi:'#e2eeef',                    // water sheen / drip
  };
  const MAT = {
    WOOD:{ mid:'wd', hi:'wdHi', sh:'wdSh', dp:'wdDp' },
    FRAME:{ mid:'fr', hi:'frHi', sh:'frSh', dp:'frDp' },
    WIRE:{ mid:'wr', hi:'wrHi', sh:'wrSh', dp:'wrDp' },
    NET:{ mid:'nt', hi:'ntHi', sh:'ntSh', dp:'ntSh' },
    ROPE:{ mid:'rp', hi:'rpHi', sh:'rpSh', dp:'rpSh' },
    WEED:{ mid:'sw', hi:'swHi', sh:'swSh', dp:'swSh' },
    BONE:{ mid:'bo', hi:'boHi', sh:'boSh', dp:'boSh' },
    WATER:{ mid:'wa', hi:'waHi', sh:'wa', dp:'wa' },
  };
  // materials that get the "wet" darken; WEED/BONE/WATER stay bright
  const STRUCT = { WOOD:1, FRAME:1, WIRE:1, NET:1, ROPE:1 };

  function newBuf(){ return { key:new Array(W*H).fill(''), mat:new Array(W*H).fill(null) }; }
  const idx=(x,y)=>y*W+x, inb=(x,y)=>x>=0&&x<W&&y>=0&&y<H;
  function put(b,x,y,m){ x=Math.round(x); y=Math.round(y); if(!inb(x,y))return; b.key[idx(x,y)]='mid'; b.mat[idx(x,y)]=m; }
  function putK(b,x,y,m,k){ x=Math.round(x); y=Math.round(y); if(!inb(x,y))return; b.key[idx(x,y)]=k; b.mat[idx(x,y)]=m; }
  function rect(b,x0,y0,w,h,m){ for(let y=y0;y<y0+h;y++)for(let x=x0;x<x0+w;x++)put(b,x,y,m); }
  function ellipse(b,cx,cy,rx,ry,m,k){
    for(let y=Math.floor(cy-ry);y<=Math.ceil(cy+ry);y++)for(let x=Math.floor(cx-rx);x<=Math.ceil(cx+rx);x++){
      const dx=(x-cx)/(rx+0.001),dy=(y-cy)/(ry+0.001); if(dx*dx+dy*dy<=1){ k?putK(b,x,y,m,k):put(b,x,y,m); } }
  }
  function line(b,x0,y0,x1,y1,fn){ x0=Math.round(x0);y0=Math.round(y0);x1=Math.round(x1);y1=Math.round(y1);
    let dx=Math.abs(x1-x0),dy=Math.abs(y1-y0),sx=x0<x1?1:-1,sy=y0<y1?1:-1,e=dx-dy;
    for(;;){ fn(x0,y0); if(x0===x1&&y0===y1)break; const e2=2*e; if(e2>-dy){e-=dy;x0+=sx;} if(e2<dx){e+=dx;y0+=sy;} } }

  // ---- wooden bow-top (half-round) parlour trap ----------------------------
  function drawWood(b){
    const cx=22, baseY=31, L=6, R=38;
    const archTop=(x)=>{ const t=(x-cx)/((R-L)/2); return 11 + t*t*6; };   // shallow arch, peak y=11
    // body fill
    for(let x=L;x<=R;x++){ const yt=Math.round(archTop(x)); for(let y=yt;y<=baseY;y++) put(b,x,y,'WOOD'); }
    // horizontal lath gaps (dark interior between slats) — every 3px
    for(let y=Math.round(archTop(cx))+2; y<baseY; y+=3){
      for(let x=L+1;x<=R-1;x++){ const yt=Math.round(archTop(x)); if(y>yt+1&&b.mat[idx(x,y)]==='WOOD') b.key[idx(x,y)]='dp'; }
    }
    // arched bows (ribs) — a few lighter vertical arcs crossing the laths
    for(const bx of [12,22,32]) for(let x=bx-0;x<=bx;x++) for(let y=Math.round(archTop(bx));y<=baseY;y++){ if(b.mat[idx(x,y)]==='WOOD') b.key[idx(x,y)]='hi'; }
    // end frames (heavier timber posts, left & right)
    rect(b,L-1,Math.round(archTop(L)),3,baseY-Math.round(archTop(L))+1,'FRAME');
    rect(b,R-1,Math.round(archTop(R)),3,baseY-Math.round(archTop(R))+1,'FRAME');
    // base runners (stick out at the feet)
    rect(b,L-2,baseY-1,R-L+5,2,'FRAME');
    // net funnel entrance (the "head") — dark oval with a black hole
    ellipse(b,cx,25,7,4,'NET'); ellipse(b,cx,25,3.4,2.2,'NET','dp');
    putK(b,cx,25,'NET','out');
    // rope bridle arcing over the top, gathered to a becket loop at the peak
    for(let x=L+3;x<=R-3;x++){ const y=Math.round(archTop(x))-3 - Math.sin((x-L)/(R-L)*Math.PI)*1.5; putK(b,x,Math.round(y),'ROPE',(x&1)?'mid':'hi'); }
    ellipse(b,cx,Math.round(archTop(cx))-6,2.4,2.6,'ROPE'); ellipse(b,cx,Math.round(archTop(cx))-6,1,1.2,'NET','out');
    return { baseY };
  }

  // ---- modern wire-mesh trap ------------------------------------------------
  function drawWire(b){
    const baseY=31, L=6, R=38, topF=13, topB=8;
    // top face (parallelogram, ¾)
    for(let x=L;x<=R;x++){ const t=(x-L)/(R-L); const yb=topF, yt=topB + (1-Math.abs(t-0.5)*2)*0; for(let y=topB;y<=topF;y++) put(b,x,y,'WIRE'); }
    for(let x=L;x<=R;x++){ for(let y=topB;y<topF;y++) put(b,x,y,'WIRE'); }
    // front face
    for(let x=L;x<=R;x++) for(let y=topF;y<=baseY;y++) put(b,x,y,'WIRE');
    // mesh grid — vertical + horizontal 1px darker lines leave wire squares
    for(let x=L+2;x<R;x+=3) for(let y=topB;y<=baseY;y++){ if(b.mat[idx(x,y)]==='WIRE') b.key[idx(x,y)]='sh'; }
    for(let y=topB+2;y<=baseY;y+=3) for(let x=L;x<=R;x++){ if(b.mat[idx(x,y)]==='WIRE') b.key[idx(x,y)]= b.key[idx(x,y)]==='sh'?'dp':'sh'; }
    // frame edges (heavier)
    for(let x=L;x<=R;x++){ if(b.mat[idx(x,topB)])b.key[idx(x,topB)]='hi'; if(b.mat[idx(x,baseY)])b.mat[idx(x,baseY)]='FRAME'; }
    for(let y=topB;y<=baseY;y++){ b.mat[idx(L,y)]&&(b.mat[idx(L,y)]='FRAME'); b.mat[idx(R,y)]&&(b.mat[idx(R,y)]='FRAME'); }
    // divider (kitchen | parlour) + top ridge line
    for(let y=topB;y<=baseY;y++) if(b.mat[idx(24,y)]==='WIRE') b.mat[idx(24,y)]='FRAME';
    for(let x=L;x<=R;x++) if(b.mat[idx(x,topF)]==='WIRE') b.key[idx(x,topF)]='sh';
    // wooden runner base
    rect(b,L-1,baseY-1,R-L+3,2,'WOOD');
    rect(b,L+1,baseY+1,3,1,'WOOD'); rect(b,R-3,baseY+1,3,1,'WOOD');
    // net head entrance
    ellipse(b,15,24,4.5,3,'NET'); ellipse(b,15,24,2,1.4,'NET','dp'); putK(b,15,24,'NET','out');
    return { baseY };
  }

  // ---- wet dressing: rockweed, barnacles, drips, sheen ---------------------
  function addWet(b, kind){
    const R=38, L=6;
    // rockweed draped over the top, 2 strands with little bladders
    const strands = kind==='wood' ? [[15,10],[30,12]] : [[13,10],[31,11]];
    for(const [sx,sy] of strands){
      let x=sx,y=sy;
      for(let s=0;s<12;s++){ putK(b,x,y,'WEED', s%4===0?'hi':'mid'); if(s%3===2) putK(b,x+1,y,'WEED','sh'); x+=(s%2?1:-1); y+=1; }
      putK(b,x,y,'WEED','hi');
    }
    // barnacle cluster (lower-right)
    for(const [bx,by] of [[33,28],[35,29],[34,27],[36,28]]){ putK(b,bx,by,'BONE','hi'); putK(b,bx,by+1,'BONE','sh'); }
    // top-left sheen highlight
    for(let x=L+2;x<L+12;x++){ const yy = kind==='wood'? Math.round(11+((x-22)/16)*((x-22)/16)*6)-0 : 8; if(b.mat[idx(x,yy)]) putK(b,x,yy,'WATER','hi'); }
    // drips + droplets at the base
    for(const dx of [12,20,28,34]){ putK(b,dx,32,'WATER','mid'); putK(b,dx,33,'WATER','mid'); if(dx%8===0) putK(b,dx,34,'WATER','hi'); }
  }

  // ---- shade / outline / colourise -----------------------------------------
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
  function toRGBA(b, wet){
    const out=new Uint8ClampedArray(W*H*4);
    for(let i=0;i<W*H;i++){ const k=b.key[i]; if(!k){ out[i*4+3]=0; continue; }
      let [r,g,bl]=hex2rgb(colourOf(b.mat[i],k));
      if(wet && STRUCT[b.mat[i]]){ r*=0.78; g*=0.82; bl*=0.9; }   // wet = darker, a touch cooler
      out[i*4]=r; out[i*4+1]=g; out[i*4+2]=bl; out[i*4+3]=255; }
    return out;
  }

  function render(kind, wet){
    const b=newBuf();
    if(kind==='wire') drawWire(b); else drawWood(b);
    if(wet) addWet(b, kind);
    shade(b); outline(b);
    return toRGBA(b, !!wet);
  }

  root.LobsterPots = { W, H, PAL:HEX, KINDS:['wood','wire'], pivot:{x:22,y:32}, render };
})(typeof globalThis!=='undefined'?globalThis:window);
