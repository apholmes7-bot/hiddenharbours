/* Hidden Harbours — parametric LOBSTER BUOYS, one per fleet hull (8 variants).
   Each fisher's pots are marked by a buoy in that boat's registered colours. Primary band
   = the hull's dominant tint (sampled from Art/UI/Roster/*); secondary + spindle chosen
   distinct so no two buoys read alike (as real buoy law requires). 32 px = 1 m · no AA ·
   transparent PNG · upper-left key light · 1px #171a14 keyline. Ramps derived from each
   base colour (lighten/darken) so everything stays in the sampled fleet palette.

   Spar-buoy shape: weighted toggle → egg float (2 paint bands) → neck → spindle stick +
   ball top. 24×48, bottom-centre pivot (12, 46).
   → LobsterBuoys.png (192×48 = 8 × 24×48), order = FLEET below.

   Exposes globalThis.LobsterBuoys with:
     W, H, FLEET (names), SCHEMES, pivot, render(i)/renderScheme(s) -> Uint8ClampedArray. */
(function (root) {
  const W = 24, H = 48, PX = 12, PY = 46;

  // fleet order + colour schemes { pri (hull tint), sec (2nd band), top (spindle) }
  const FLEET = ['Dory','Punt','CapeIslander','LobsterBoat','SideDragger','SternTrawler','CoastalPacket','Tanker'];
  const SCHEMES = {
    Dory:         { pri:'#74543a', sec:'#d8c8a0', top:'#b5403a' },  // brown & cream, red tip
    Punt:         { pri:'#2ba39a', sec:'#eef0ea', top:'#e0b13a' },  // teal & white, gold tip
    CapeIslander: { pri:'#7fa08c', sec:'#2a3b52', top:'#eef0ea' },  // sage & navy, white tip
    LobsterBoat:  { pri:'#b5403a', sec:'#eef0ea', top:'#1d2226' },  // red & white, black tip
    SideDragger:  { pri:'#9c4a3c', sec:'#e0b13a', top:'#2a3b52' },  // rust & yellow, navy tip
    SternTrawler: { pri:'#4a5a66', sec:'#e0862e', top:'#eef0ea' },  // steel & orange, white tip
    CoastalPacket:{ pri:'#9c2f24', sec:'#4e7d52', top:'#e0d8c4' },  // deep red & green, bone tip
    Tanker:       { pri:'#8a8f94', sec:'#9c2f24', top:'#1d2226' },  // steel-grey & red, black tip
  };
  const OUT='#171a14';

  function newBuf(){ return { key:new Array(W*H).fill(''), mat:new Array(W*H).fill(null) }; }
  const idx=(x,y)=>y*W+x, inb=(x,y)=>x>=0&&x<W&&y>=0&&y<H;
  function put(b,x,y,m){ x=Math.round(x); y=Math.round(y); if(!inb(x,y))return; b.key[idx(x,y)]='mid'; b.mat[idx(x,y)]=m; }
  function putK(b,x,y,m,k){ x=Math.round(x); y=Math.round(y); if(!inb(x,y))return; b.key[idx(x,y)]=k; b.mat[idx(x,y)]=m; }
  function ellipse(b,cx,cy,rx,ry,m){ for(let y=Math.floor(cy-ry);y<=Math.ceil(cy+ry);y++)for(let x=Math.floor(cx-rx);x<=Math.ceil(cx+rx);x++){ const dx=(x-cx)/(rx+0.001),dy=(y-cy)/(ry+0.001); if(dx*dx+dy*dy<=1) put(b,x,y,m); } }
  function taper(b,x0,y0,x1,y1,r0,r1,m){ const dx=x1-x0,dy=y1-y0,L2=dx*dx+dy*dy||1;
    const minx=Math.floor(Math.min(x0,x1)-Math.max(r0,r1)),maxx=Math.ceil(Math.max(x0,x1)+Math.max(r0,r1));
    const miny=Math.floor(Math.min(y0,y1)-Math.max(r0,r1)),maxy=Math.ceil(Math.max(y0,y1)+Math.max(r0,r1));
    for(let y=miny;y<=maxy;y++)for(let x=minx;x<=maxx;x++){ let t=((x-x0)*dx+(y-y0)*dy)/L2; t=Math.max(0,Math.min(1,t)); const px=x0+dx*t,py=y0+dy*t; if(Math.hypot(x-px,y-py)<=r0+(r1-r0)*t) put(b,x,y,m); } }
  function rect(b,x0,y0,w,h,m){ for(let y=y0;y<y0+h;y++)for(let x=x0;x<x0+w;x++)put(b,x,y,m); }

  // paint-band assignment: PRI lower float, SEC upper float, TOP spindle+ball, DARK toggle/eye
  function drawBuoy(b){
    // toggle / keel weight at the base
    ellipse(b, PX, PY-2, 3.2, 2.6, 'DARK');
    rect(b, PX-1, PY-6, 2, 5, 'DARK');            // short stem to the float
    // egg float body
    ellipse(b, PX, 28, 8, 11, 'PRI');
    // upper band = SEC (top third of the float)
    for(let y=17;y<=24;y++)for(let x=0;x<W;x++){ const i=idx(x,y); if(b.mat[i]==='PRI') b.mat[i]='SEC'; }
    // thin dark separator ring between the bands
    for(let x=0;x<W;x++){ const i=idx(x,25); if(b.mat[i]==='PRI'||b.mat[i]==='SEC') b.key[i]='__sep'; }
    // neck taper up to the spindle
    taper(b, PX, 19, PX, 12, 3.2, 1.4, 'SEC');
    // spindle stick + ball top
    rect(b, PX-1, 4, 2, 9, 'TOP');
    ellipse(b, PX, 3, 2.2, 2.2, 'TOP');
    // rope eye at the keel (small loop hint)
    putK(b, PX-3, PY-1, 'DARK','mid'); putK(b, PX+3, PY-1, 'DARK','mid');
  }

  // ---- shade / outline / colourise (upper-left key, ramps derived per band) ----
  function domeShade(b){
    for(let y=0;y<H;y++)for(let x=0;x<W;x++){
      const i=idx(x,y), m=b.mat[i]; if(!m||m==='__out'||b.key[i]==='__sep') continue;
      if(b.key[i]!=='mid') continue;
      const Lv=-((x-PX)*0.7+(y-28)*0.6);
      b.key[i]= Lv>6?'hi': Lv>-4?'mid': Lv>-13?'sh':'dp';
    }
  }
  function outline(b){
    const add=[];
    for(let y=0;y<H;y++)for(let x=0;x<W;x++){ if(b.key[idx(x,y)])continue;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]) if(inb(x+dx,y+dy)&&b.key[idx(x+dx,y+dy)]&&b.mat[idx(x+dx,y+dy)]!=='__out'){ add.push([x,y]); break; } }
    for(const [x,y] of add){ b.key[idx(x,y)]='out'; b.mat[idx(x,y)]='__out'; }
  }
  function hex2rgb(h){ return [parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]; }
  function rgb2hex(r,g,b){ return '#'+[r,g,b].map(v=>Math.max(0,Math.min(255,Math.round(v))).toString(16).padStart(2,'0')).join(''); }
  const light=(hex,f)=>{ const [r,g,b]=hex2rgb(hex); return rgb2hex(r+(255-r)*f, g+(255-g)*f, b+(255-b)*f); };
  const dark =(hex,f)=>{ const [r,g,b]=hex2rgb(hex); return rgb2hex(r*f, g*f, b*f); };
  function rampColour(base, k){ if(k==='hi') return light(base,0.32); if(k==='sh') return dark(base,0.7); if(k==='dp') return dark(base,0.46); return base; }

  function toRGBA(b, S){
    const bands={ PRI:S.pri, SEC:S.sec, TOP:S.top, DARK:'#25211a' };
    const out=new Uint8ClampedArray(W*H*4);
    for(let i=0;i<W*H;i++){ const k=b.key[i], m=b.mat[i]; if(!k){ out[i*4+3]=0; continue; }
      let hex;
      if(m==='__out'||k==='out') hex=OUT;
      else if(k==='__sep') hex=dark(bands[m]||'#000', 0.4);
      else hex=rampColour(bands[m]||'#888', k);
      const [r,g,bl]=hex2rgb(hex); out[i*4]=r; out[i*4+1]=g; out[i*4+2]=bl; out[i*4+3]=255; }
    return out;
  }

  function renderScheme(S){ const b=newBuf(); drawBuoy(b); domeShade(b); outline(b); return toRGBA(b, S); }
  function render(i){ return renderScheme(SCHEMES[FLEET[i]]); }

  root.LobsterBuoys = { W, H, FLEET, SCHEMES, pivot:{x:PX,y:PY}, render, renderScheme };
})(typeof globalThis!=='undefined'?globalThis:window);
