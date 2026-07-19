/* Hidden Harbours — parametric ROPE PROPS. Deck dressing for the wharf & boats.
   ¾ read. 32 px = 1 m · no AA · transparent PNG · upper-left key light · 1px #171a14
   keyline. Manila/hemp rope (Wood/Earth-warm bone ramp, the same ROPE ramp used on the
   pots) with a diagonal "lay" hatch so it reads as twisted line; galvanised cleat; tarred
   whipping twine. Nothing new invented.

   Three props (each 40×32, bottom-centre pivot (20,30)):
     • coil  — flaked coil hank with a running end + a whipped bitter end.
     • cleat — galvanised horn cleat with a figure-8 belay.
     • whip  — a rope end finished with a tarred whipping + a short fray beyond.
   → RopeProps.png (120×32 = 3 × 40×32), order = KINDS below.

   Exposes globalThis.RopeProps with:
     W, H, PAL, KINDS, pivot, render(kind)/renderIndex(i) -> Uint8ClampedArray. */
(function (root) {
  const W = 40, H = 32, PX = 20, PY = 30;
  const KINDS = ['coil','cleat','whip'];

  const HEX = {
    out:'#171a14',
    rp:'#a98f66', rpHi:'#cdbe97', rpSh:'#7a6242', rpDp:'#54432c',   // manila rope
    wh:'#2b2620', whHi:'#4a4236',                                    // tarred whipping twine
    cl:'#71807f', clHi:'#a6b6b8', clSh:'#49555a', clDp:'#2c3539',   // galvanised cleat
    blt:'#3a4446',                                                   // bolt heads
  };
  const MAT = {
    ROPE:{ mid:'rp', hi:'rpHi', sh:'rpSh', dp:'rpDp' },
    WHIP:{ mid:'wh', hi:'whHi', sh:'wh', dp:'wh' },
    CLEAT:{ mid:'cl', hi:'clHi', sh:'clSh', dp:'clDp' },
    BOLT:{ mid:'blt', hi:'blt', sh:'blt', dp:'blt' },
  };

  function newBuf(){ return { key:new Array(W*H).fill(''), mat:new Array(W*H).fill(null) }; }
  const idx=(x,y)=>y*W+x, inb=(x,y)=>x>=0&&x<W&&y>=0&&y<H;
  function put(b,x,y,m){ x=Math.round(x); y=Math.round(y); if(!inb(x,y))return; b.key[idx(x,y)]='mid'; b.mat[idx(x,y)]=m; }
  function putK(b,x,y,m,k){ x=Math.round(x); y=Math.round(y); if(!inb(x,y))return; b.key[idx(x,y)]=k; b.mat[idx(x,y)]=m; }
  function ellipse(b,cx,cy,rx,ry,m){ for(let y=Math.floor(cy-ry);y<=Math.ceil(cy+ry);y++)for(let x=Math.floor(cx-rx);x<=Math.ceil(cx+rx);x++){ const dx=(x-cx)/(rx+0.001),dy=(y-cy)/(ry+0.001); if(dx*dx+dy*dy<=1) put(b,x,y,m); } }
  function ringEll(b,cx,cy,rx,ry,thick,m){ // elliptical annulus (a coil loop)
    for(let y=Math.floor(cy-ry-thick);y<=Math.ceil(cy+ry+thick);y++)for(let x=Math.floor(cx-rx-thick);x<=Math.ceil(cx+rx+thick);x++){
      const d=Math.hypot((x-cx)/(rx+0.001),(y-cy)/(ry+0.001)); if(d<=1+thick/rx && d>=1-thick/rx) put(b,x,y,m); } }
  function taper(b,x0,y0,x1,y1,r0,r1,m){ const dx=x1-x0,dy=y1-y0,L2=dx*dx+dy*dy||1;
    const minx=Math.floor(Math.min(x0,x1)-Math.max(r0,r1)),maxx=Math.ceil(Math.max(x0,x1)+Math.max(r0,r1));
    const miny=Math.floor(Math.min(y0,y1)-Math.max(r0,r1)),maxy=Math.ceil(Math.max(y0,y1)+Math.max(r0,r1));
    for(let y=miny;y<=maxy;y++)for(let x=minx;x<=maxx;x++){ let t=((x-x0)*dx+(y-y0)*dy)/L2; t=Math.max(0,Math.min(1,t)); const px=x0+dx*t,py=y0+dy*t; if(Math.hypot(x-px,y-py)<=r0+(r1-r0)*t) put(b,x,y,m); } }
  const capsule=(b,x0,y0,x1,y1,r,m)=>taper(b,x0,y0,x1,y1,r,r,m);
  function rect(b,x0,y0,w,h,m){ for(let y=y0;y<y0+h;y++)for(let x=x0;x<x0+w;x++)put(b,x,y,m); }

  // ---- props ---------------------------------------------------------------
  function drawCoil(b){
    const cx=17, cy=17;
    // flaked coil: 3 concentric rope loops
    ringEll(b, cx, cy, 12, 7.5, 2.2, 'ROPE');
    ringEll(b, cx, cy, 8.2, 5.2, 2.0, 'ROPE');
    ringEll(b, cx, cy, 4.4, 3.0, 1.8, 'ROPE');
    // running end curling off to the lower-right
    capsule(b, cx+11, cy+4, 30, 26, 2.0, 'ROPE');
    capsule(b, 30, 26, 36, 24, 1.9, 'ROPE');
    // whipped bitter end on the running end
    for(let k=0;k<4;k++) putK(b, 35-k*0 , 24, 'WHIP','mid');
    ellipse(b, 36, 24, 2.0, 2.0, 'WHIP');
    ellipse(b, 36, 24, 1.1, 1.1, 'WHIP','hi');
  }

  function drawCleat(b){
    const cx=20, base=22;
    // galvanised horn cleat drawn as a "bone": centre bar + two raised horn knobs + bolts.
    rect(b, cx-9, base-2, 18, 5, 'CLEAT');                 // base bar
    ellipse(b, cx-11, base, 3.4, 3.8, 'CLEAT');            // left horn knob
    ellipse(b, cx+11, base, 3.4, 3.8, 'CLEAT');            // right horn knob
    ellipse(b, cx, base, 3.0, 3.0, 'CLEAT');              // centre boss
    putK(b, cx-9, base+2, 'BOLT','mid'); putK(b, cx+9, base+2, 'BOLT','mid');
    // figure-8 rope belay wrapping the two horns
    capsule(b, 4, 29, cx-8, base+1, 2.2, 'ROPE');          // standing part in, low-left
    capsule(b, cx-9, base+3, cx+8, base-4, 2.1, 'ROPE');   // cross ↗
    capsule(b, cx-8, base-4, cx+9, base+3, 2.1, 'ROPE');   // cross ↘ (the 8)
    capsule(b, cx+8, base-1, 36, 29, 2.1, 'ROPE');         // running end out, low-right
    // redraw the horn knobs on top so the rope reads as wrapped UNDER the horns
    ellipse(b, cx-11, base-1, 3.0, 3.2, 'CLEAT');
    ellipse(b, cx+11, base-1, 3.0, 3.2, 'CLEAT');
  }

  function drawWhip(b){
    // a clean rope end lying ¾, finished with a tarred whipping + a short fray beyond
    capsule(b, 4, 13, 27, 21, 2.8, 'ROPE');               // the rope, gentle diagonal
    // whipping band (tight tarred turns) near the end — alternating turn stripes
    for(let x=26;x<=31;x++) for(let y=15;y<=25;y++){ const i=idx(x,y); if(b.mat[i]==='ROPE'){ b.mat[i]='WHIP'; b.key[i]=((x)&1)?'mid':'hi'; } }
    // three frayed strands splaying beyond the whipping
    for(const [ex,ey] of [[35,17],[36,20],[35,23]]){ taper(b, 31,20, ex,ey, 1.5, 0.5, 'ROPE'); putK(b, ex,ey, 'ROPE','hi'); }
  }

  // ---- lay hatch / shade / outline / colourise -----------------------------
  function layHatch(b){
    // diagonal twist stripes on rope so it reads as laid line (darken every 3rd diagonal)
    for(let y=0;y<H;y++)for(let x=0;x<W;x++){ const i=idx(x,y); if(b.mat[i]!=='ROPE') continue; const k=b.key[i];
      if((x - y + 300) % 3 === 0){ b.key[i]= k==='hi'?'mid': k==='mid'?'sh': k==='sh'?'dp':k; } }
  }
  function domeShade(b, cx, cy){
    for(let y=0;y<H;y++)for(let x=0;x<W;x++){ const i=idx(x,y), m=b.mat[i]; if(!m||m==='__out'||m==='WHIP'||m==='BOLT') continue; if(b.key[i]!=='mid') continue;
      const Lv=-((x-cx)*0.6+(y-cy)*0.7); b.key[i]= Lv>6?'hi': Lv>-4?'mid': Lv>-12?'sh':'dp'; }
  }
  function shade(b){
    const src=b.key.slice(), mat=b.mat;
    for(let y=0;y<H;y++)for(let x=0;x<W;x++){ const i=idx(x,y); if(src[i]!=='mid') continue; const m=mat[i];
      const up=y>0&&src[idx(x,y-1)]&&mat[idx(x,y-1)]===m, lf=x>0&&src[idx(x-1,y)]&&mat[idx(x-1,y)]===m;
      const dn=y<H-1&&src[idx(x,y+1)]&&mat[idx(x,y+1)]===m, rt=x<W-1&&src[idx(x+1,y)]&&mat[idx(x+1,y)]===m;
      if(!up||!lf) b.key[i]='hi'; else if(!dn||!rt) b.key[i]='sh'; }
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
  function toRGBA(b){ const out=new Uint8ClampedArray(W*H*4);
    for(let i=0;i<W*H;i++){ const k=b.key[i]; if(!k){ out[i*4+3]=0; continue; } const [r,g,bl]=hex2rgb(colourOf(b.mat[i],k)); out[i*4]=r; out[i*4+1]=g; out[i*4+2]=bl; out[i*4+3]=255; } return out; }

  function render(kind){
    const b=newBuf();
    if(kind==='cleat') drawCleat(b); else if(kind==='whip') drawWhip(b); else drawCoil(b);
    domeShade(b, kind==='coil'?17:20, kind==='coil'?17:20);
    layHatch(b); shade(b); outline(b);
    return toRGBA(b);
  }
  function renderIndex(i){ return render(KINDS[i]); }

  root.RopeProps = { W, H, PAL:HEX, KINDS, pivot:{x:PX,y:PY}, render, renderIndex };
})(typeof globalThis!=='undefined'?globalThis:window);
