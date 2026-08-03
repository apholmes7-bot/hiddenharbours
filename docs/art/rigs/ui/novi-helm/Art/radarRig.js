// Radar — diegetic marine radar rig for Hidden Harbours. A THIRD brow instrument
// alongside the Depth Sounder (depthRig.js) and Fish Finder (fishRig.js): SAME
// graphite case idiom, SAME box-parameterised drawUnit(ctx,X,Y,WW,HH,o) / paintInto
// contract, and the SAME portrait footprint as the sonar so a console brow carries
// it at the sounder's height. The glass runs a live PPI (plan-position indicator):
// a rotating scan sweep with phosphor persistence, concentric range rings, a
// bearing scale, own-ship at centre, and ECHO TARGETS the game places anywhere —
// each a (bearing, range, size, KIND) the caller sets, so gameplay decides the
// shapes (coastline / vessel / buoy / rain cell / bird flock). Adjustable RANGE
// scale, HEAD-UP / NORTH-UP orientation, GAIN + SEA + RAIN clutter, an EBL bearing
// line + VRM range ring, and a guard-zone target alarm. Everything is a parameter;
// no sprite bakes. Brand wordmark is original (RD-4 / HARBOUR / RADAR) — no trademark.
(function (root) {
  const W = 480, H = 660;                 // portrait — matches the sonar footprint exactly
  const D = Math.PI / 180;

  // ---- palettes (graphite case shared with the fleet) ----------------------
  const CASE  = ['#12171b','#1b2228','#28323a','#3a4750','#4f5e68','#6b7c86'];
  const RESIN = ['#050708','#0c1013','#141b1f'];
  const STEEL = ['#232a30','#39434b','#556069','#7a8892','#9db0b8'];
  const TEAL  = '#7fd6c9', TEAK = '#c9a86a', BLUE = '#5b93b8';
  const RED   = ['#7c2a20','#b83b2e','#e0554a'];

  // day = green phosphor scope; night = warm amber (fleet night idiom)
  const DAY = {
    bg:'#05130d', bg2:'#020a07', ringDim:'rgba(90,200,150,0.16)', ringMaj:'rgba(120,220,170,0.34)',
    spoke:'rgba(90,200,150,0.08)',
    tick:'#3f9068', tickMaj:'#7fd6a8', tickNum:'#9fe6c0', ink:'#cfeede', dim:'#5f8f78',
    sweep:'#6bf0a0', sweepEdge:'#d6ffe6', glow:'111,240,160',
    head:'#e6f4ec', north:'#e0554a',
    ebl:'#6fccef', vrm:'#e6b53f', guard:'#e0554a', guardFill:'rgba(224,85,74,0.12)',
    echo:['#2f9e57','#7bc24a','#e6b53f','#e0554a'], land:'#c33b2b', landDim:'#7c2a20',
    rain:'rgba(120,196,224,0.55)', flock:'#8fe0b0',
    strip:'#04120c', stripFg:'#8fe0b0', stripDim:'#3c6a54', plate:'rgba(4,14,10,0.55)',
    keyPlate:'#245a4a', keyEdge:'#123528', legend:'#dff3e6', mark:'#bfe6cf',
  };
  const NIGHT = {
    bg:'#0c0702', bg2:'#050200', ringDim:'rgba(230,170,70,0.14)', ringMaj:'rgba(255,190,90,0.30)',
    spoke:'rgba(230,170,70,0.07)',
    tick:'#7a5a1c', tickMaj:'#c79a3f', tickNum:'#ffd07a', ink:'#ffe0a0', dim:'#8a6a30',
    sweep:'#ffcf6a', sweepEdge:'#fff0c8', glow:'255,190,90',
    head:'#ffe6bc', north:'#ff6f5c',
    ebl:'#7fd0ef', vrm:'#ffd77a', guard:'#ff6f5c', guardFill:'rgba(255,111,92,0.12)',
    echo:['#7a7a24','#b59a2c','#e0902f','#e0554a'], land:'#c85030', landDim:'#7a2a16',
    rain:'rgba(200,170,120,0.5)', flock:'#ffd77a',
    strip:'#150d02', stripFg:'#ffd77a', stripDim:'#6b501c', plate:'rgba(10,6,2,0.6)',
    keyPlate:'#3a2c0c', keyEdge:'#201604', legend:'#ffe0a0', mark:'#ffd77a',
  };

  // ---- 3x5 bitmap font (shared idiom with the fleet) ------------------------
  const F = {
    'A':['.#.','#.#','###','#.#','#.#'],'B':['##.','#.#','##.','#.#','##.'],
    'C':['.##','#..','#..','#..','.##'],'D':['##.','#.#','#.#','#.#','##.'],
    'E':['###','#..','##.','#..','###'],'F':['###','#..','##.','#..','#..'],
    'G':['.##','#..','#.#','#.#','.##'],'H':['#.#','#.#','###','#.#','#.#'],
    'I':['###','.#.','.#.','.#.','###'],'J':['..#','..#','..#','#.#','.#.'],
    'K':['#.#','#.#','##.','#.#','#.#'],'L':['#..','#..','#..','#..','###'],
    'M':['#.#','###','###','#.#','#.#'],'N':['#.#','##.','###','.##','#.#'],
    'O':['.#.','#.#','#.#','#.#','.#.'],'P':['##.','#.#','##.','#..','#..'],
    'Q':['.#.','#.#','#.#','.#.','..#'],'R':['##.','#.#','##.','#.#','#.#'],
    'S':['.##','#..','.#.','..#','##.'],'T':['###','.#.','.#.','.#.','.#.'],
    'U':['#.#','#.#','#.#','#.#','.#.'],'V':['#.#','#.#','#.#','.#.','.#.'],
    'W':['#.#','#.#','###','###','#.#'],'X':['#.#','#.#','.#.','#.#','#.#'],
    'Y':['#.#','#.#','.#.','.#.','.#.'],'Z':['###','..#','.#.','#..','###'],
    '0':['###','#.#','#.#','#.#','###'],'1':['.#.','##.','.#.','.#.','###'],
    '2':['##.','..#','.#.','#..','###'],'3':['##.','..#','.#.','..#','##.'],
    '4':['#.#','#.#','###','..#','..#'],'5':['###','#..','##.','..#','##.'],
    '6':['.##','#..','##.','#.#','.#.'],'7':['###','..#','.#.','.#.','.#.'],
    '8':['.#.','#.#','.#.','#.#','.#.'],'9':['.#.','#.#','.##','..#','##.'],
    '-':['...','...','###','...','...'],'.':['...','...','...','...','.#.'],
    '/':['..#','..#','.#.','#..','#..'],':':['...','.#.','...','.#.','...'],' ':['...','...','...','...','...'],
  };
  function textW(str, s){ return String(str).length * (3*s + s) - s; }
  function text(ctx, str, x, y, s, col){
    ctx.fillStyle = col; str = String(str).toUpperCase(); let cx = x;
    for (const ch of str){ const g = F[ch] || F[' '];
      for (let r=0;r<5;r++) for (let c=0;c<3;c++) if (g[r][c]==='#') ctx.fillRect(cx+c*s, y+r*s, s, s);
      cx += 3*s + s; }
    return cx - s;
  }
  function textR(ctx, str, xr, y, s, col){ return text(ctx, str, Math.round(xr-textW(str,s)), y, s, col); }
  function textC(ctx, str, cx, y, s, col){ return text(ctx, str, Math.round(cx-textW(str,s)/2), y, s, col); }
  function degMark(ctx, x, y, k, col){ ctx.fillStyle=col; ctx.fillRect(x,y,k,k); ctx.clearRect(x+1,y+1,Math.max(1,k-2),Math.max(1,k-2)); }

  // ---- crisp primitives -----------------------------------------------------
  const cv = (w,h)=>{ const c=document.createElement('canvas'); c.width=Math.round(w); c.height=Math.round(h); return c; };
  function rowInset(v, h, r){
    if (v < r)      { const dy=r-v;        return r - Math.floor(Math.sqrt(Math.max(0,r*r-dy*dy))); }
    if (v >= h - r) { const dy=v-(h-r)+1;  return r - Math.floor(Math.sqrt(Math.max(0,r*r-dy*dy))); }
    return 0;
  }
  function rrect(ctx, x, y, w, h, r, fill){
    ctx.fillStyle = fill; r = Math.max(0, Math.min(r, Math.floor(Math.min(w,h)/2)));
    x=Math.round(x); y=Math.round(y); w=Math.round(w); h=Math.round(h);
    for (let v=0; v<h; v++){ const i=rowInset(v,h,r); ctx.fillRect(x+i, y+v, w-2*i, 1); }
  }
  function circle(ctx, cx, cy, rad, fill){
    ctx.fillStyle=fill; cx=Math.round(cx); cy=Math.round(cy);
    for (let dy=-rad; dy<=rad; dy++){ const dx=Math.floor(Math.sqrt(Math.max(0,rad*rad-dy*dy))); ctx.fillRect(cx-dx, cy+dy, 2*dx+1, 1); }
  }
  function ring(ctx, cx, cy, rO, rI, fill){
    ctx.fillStyle=fill; cx=Math.round(cx); cy=Math.round(cy);
    for (let dy=-rO; dy<=rO; dy++){ const xo=Math.floor(Math.sqrt(Math.max(0,rO*rO-dy*dy)));
      if (Math.abs(dy)<rI){ const xi=Math.floor(Math.sqrt(Math.max(0,rI*rI-dy*dy)));
        ctx.fillRect(cx-xo, cy+dy, xo-xi, 1); ctx.fillRect(cx+xi, cy+dy, xo-xi, 1);
      } else ctx.fillRect(cx-xo, cy+dy, 2*xo+1, 1); }
  }
  function ringOutline(ctx, cx, cy, rad, fill){ ring(ctx, cx, cy, rad, rad-1, fill); }
  function ellipse(ctx, cx, cy, rx, ry, fill){
    ctx.fillStyle=fill; cx=Math.round(cx); cy=Math.round(cy); rx=Math.max(1,Math.round(rx)); ry=Math.max(1,Math.round(ry));
    for (let dy=-ry; dy<=ry; dy++){ const dx=Math.floor(rx*Math.sqrt(Math.max(0,1-(dy*dy)/(ry*ry)))); ctx.fillRect(cx-dx, cy+dy, 2*dx+1, 1); }
  }
  function thickLine(ctx, x0, y0, x1, y1, w, fill){
    ctx.fillStyle=fill; const dx=x1-x0, dy=y1-y0, L=Math.max(1,Math.hypot(dx,dy)), n=Math.ceil(L);
    const px=-dy/L, py=dx/L, hw=(w-1)/2;
    for (let i=0;i<=n;i++){ const t=i/n, X=x0+dx*t, Y=y0+dy*t;
      for (let j=-hw;j<=hw;j++) ctx.fillRect(Math.round(X+px*j), Math.round(Y+py*j), 1, 1); }
  }
  function screw(ctx, cx, cy, r){
    circle(ctx, cx, cy, r, STEEL[0]); circle(ctx, cx, cy, r-1, STEEL[2]); circle(ctx, cx-1, cy-1, Math.max(1,r-2), STEEL[3]);
    ctx.fillStyle = STEEL[0]; ctx.fillRect(cx-r+1, cy, 2*r-1, 1);
  }
  const dir = (a)=>({ x:Math.sin(a), y:-Math.cos(a) });          // 0 = up, clockwise
  const clamp = (v,a,b)=> v<a?a:(v>b?b:v);
  const norm360 = (h)=>{ h=((h%360)+360)%360; return h; };
  function hashRnd(n){ const x=Math.sin(n*127.1+13.7)*43758.5453; return x-Math.floor(x); }

  // ---- value helpers --------------------------------------------------------
  const RANGE_STEPS = [0.5, 1, 2, 3, 6, 12, 24];                  // nautical miles
  const KINDS = ['vessel','land','buoy','rain','flock'];
  const C8 = ['N','NE','E','SE','S','SW','W','NW'];
  function fmtNM(nm){ if (nm>=10) return String(Math.round(nm)); if (nm>=1) return nm.toFixed(1); const f=nm*8; const n=Math.round(f); return n===8?'1':(n+'/8'); }
  function ringInterval(scaleNM, rings){ return scaleNM/rings; }
  function cardinal8(h){ return C8[Math.round(norm360(h)/45)%8]; }
  function fmtDeg(h){ const d=Math.round(norm360(h)); return (d<10?'00':d<100?'0':'')+d; }
  function defaultScene(){ return [
    { brgTrue:308, rngNM:4.4, size:2.2, kind:'land' },
    { brgTrue:326, rngNM:5.0, size:2.5, kind:'land' },
    { brgTrue:344, rngNM:5.3, size:2.1, kind:'land' },
    { brgTrue:298, rngNM:3.7, size:1.7, kind:'land' },
    { brgTrue:64,  rngNM:2.3, size:1.05, kind:'vessel', crs:232, spd:0.8 },
    { brgTrue:118, rngNM:3.6, size:1.25, kind:'vessel', crs:300, spd:1.1 },
    { brgTrue:18,  rngNM:1.35, size:0.8, kind:'buoy' },
    { brgTrue:205, rngNM:4.2, size:1.5, kind:'rain' },
    { brgTrue:160, rngNM:2.0, size:1.1, kind:'flock' },
  ]; }

  // ---- scope bearing math ---------------------------------------------------
  function screenAngOf(brgTrue, o){ return o.orient==='north' ? brgTrue : (brgTrue - (o.heading||0)); }
  function blipXY(sc, t, o){
    const sa = screenAngOf(t.brgTrue||0, o), frac = (t.rngNM||0)/o.scaleNM, d = dir(sa*D);
    return { x: sc.cx + d.x*frac*sc.r, y: sc.cy + d.y*frac*sc.r, sa, frac,
             vis: frac<=1.001, half: Math.max(3, sc.r*0.05)*(t.size||1) };
  }
  function pickAt(sc, px, py, o){
    const dx=px-sc.cx, dy=py-sc.cy, rr=Math.hypot(dx,dy), frac=clamp(rr/sc.r,0,1);
    let sa = Math.atan2(dx, -dy)/D; sa=norm360(sa);
    const brgTrue = norm360(o.orient==='north' ? sa : (sa + (o.heading||0)));
    return { brgTrue, rngNM: +(frac*o.scaleNM).toFixed(3), frac, sa, inside: rr<=sc.r+3 };
  }

  // ---- layout (box-parameterised; drawUnit + hit-testing both use this) -----
  function layout(X, Y, WW, HH){
    const pad = Math.max(3, Math.round(WW*0.04));
    const brandH = Math.max(7, Math.round(HH*0.085));
    const colW = Math.max(14, Math.round(WW*0.12));
    const bodyH = HH - pad*2 - brandH;
    const lcd = { x:X+pad, y:Y+pad, w:WW-pad*3-colW, h:bodyH };
    const col = { x:X+WW-pad-colW, y:Y+pad, w:colW, h:bodyH };
    const keyH = Math.max(14, Math.min(Math.round(col.w*0.72), Math.floor((col.h - Math.round(col.h*0.16))/3)));
    const span = col.h - keyH;
    const buttons = [0,1,2].map(i=>({ x:col.x, y:Math.round(col.y + span*(i/2)), w:col.w, h:keyH }));
    const brand = { x:X+pad, y:Y+HH-brandH-Math.round(pad*0.3), w:WW-pad*2, h:brandH };
    // inside the LCD glass
    const stripH = Math.max(9, Math.round(lcd.w*0.075));
    const scopeSide = Math.min(lcd.w, lcd.h - stripH);
    const sr = Math.floor(scopeSide/2) - 2;
    const status = { x:lcd.x, y:lcd.y, w:lcd.w, h:stripH };
    const scope = { cx: Math.round(lcd.x + lcd.w/2), cy: Math.round(lcd.y + stripH + scopeSide/2), r: sr };
    const dataTop = lcd.y + stripH + scopeSide;
    const data = { x:lcd.x, y:dataTop, w:lcd.w, h: Math.max(0, lcd.y+lcd.h - dataTop) };
    return { pad, brandH, colW, lcd, col, buttons, brand, status, scope, data };
  }

  // ---- one pusher key -------------------------------------------------------
  function key(ctx, b, label, glyph, P, fs, lit){
    rrect(ctx, b.x, b.y, b.w, b.h, Math.max(3,Math.round(b.h*0.24)), CASE[0]);
    const ix=b.x+2, iy=b.y+2, iw=b.w-4, ih=b.h-4, r=Math.max(2,Math.round(ih*0.22));
    rrect(ctx, ix, iy, iw, ih, r, P.keyEdge);
    rrect(ctx, ix, iy, iw, ih-2, r, P.keyPlate);
    rrect(ctx, ix+2, iy+2, iw-4, Math.max(1,Math.round(ih*0.22)), r-1, 'rgba(255,255,255,0.14)');
    const cx=b.x+b.w/2, ls=Math.max(1, fs-1);
    text(ctx, label, Math.round(cx-textW(label,ls)/2), Math.round(iy+ih*0.24), ls, P.legend);
    const dcy = Math.round(iy+ih*0.70), s = Math.max(2, ls); ctx.fillStyle = P.mark;
    if (glyph==='up'){ for(let i=0;i<s+1;i++) ctx.fillRect(cx-i, dcy-s+i, i*2+1, 1); }
    else if (glyph==='dn'){ for(let i=0;i<s+1;i++) ctx.fillRect(cx-(s-i), dcy-s+i, (s-i)*2+1, 1); }
    else { ringOutline(ctx, cx, dcy, s, P.mark); ctx.fillStyle=P.mark; ctx.fillRect(cx-1, dcy-s-1, 2, 2); }
    if (lit){ rrect(ctx, ix, iy, iw, ih-2, r, 'rgba(255,190,60,0.10)'); }
  }

  // ---- status strip ---------------------------------------------------------
  function bars(ctx, x, midY, base, v, P, unit){
    const n=Math.round(4*clamp(v,0,1));
    for(let i=0;i<4;i++){ const bh=2+i*Math.max(2,unit); ctx.fillStyle=i<n?P.stripFg:P.stripDim; ctx.fillRect(x+i*(unit+1), base-bh, unit, bh); }
    return x + 4*(unit+1);
  }
  function statusStrip(ctx, S, o, P, fs){
    ctx.fillStyle=P.strip; ctx.fillRect(S.x, S.y, S.w, S.h);
    ctx.fillStyle='rgba(255,255,255,0.05)'; ctx.fillRect(S.x, S.y, S.w, 1);
    const midY = S.y + Math.round((S.h-5*fs)/2), base=S.y+S.h-3, unit=Math.max(1,fs);
    let x = S.x + 4;
    // range (left)
    x = text(ctx, fmtNM(o.scaleNM)+'NM', x, midY, fs, P.stripFg) + 8;
    // orientation + TX (centre-ish)
    const ori = o.orient==='north' ? 'N-UP' : 'H-UP';
    text(ctx, ori, x, midY, fs, P.stripFg); x += textW(ori,fs)+6;
    ctx.fillStyle = o.tx ? P.stripFg : P.stripDim; circle(ctx, x+2, midY+2*fs, Math.max(1,fs), o.tx?P.stripFg:P.stripDim);
    if (fs>=2){ text(ctx, o.tx?'TX':'STBY', x+4+fs, midY, fs, o.tx?P.stripFg:P.stripDim); }
    // gain / sea / rain (right)
    if (fs>=2){
      let rx = S.x + S.w - 4 - (4*(unit+1))*3 - textW('G',fs)*3 - 12;
      rx = text(ctx,'G',rx,midY,fs,P.stripDim)+2; rx = bars(ctx,rx,midY,base,o.gain,P,unit)+6;
      rx = text(ctx,'S',rx,midY,fs,P.stripDim)+2; rx = bars(ctx,rx,midY,base,o.sea,P,unit)+6;
      rx = text(ctx,'R',rx,midY,fs,P.stripDim)+2; bars(ctx,rx,midY,base,o.rain,P,unit);
    }
  }

  // ---- echo targets (shapes are gameplay-defined) ---------------------------
  function strengthIdx(size){ return size<0.8?0 : size<1.3?1 : size<1.9?2 : 3; }
  function sweepBright(sa, sweep, tx){ if(!tx) return 0.5; let d=norm360(sweep - sa); return clamp(1 - (d/300)*0.62, 0.38, 1); }
  function echo(ctx, sc, t, o, P, sweep){
    const g = blipXY(sc, t, o); if(!g.vis) return;
    const a = sweepBright(g.sa, sweep, o.tx) * (0.55 + 0.45*clamp((o.gain||0.7),0,1));
    const half = g.half, kind = t.kind||'vessel', size = t.size||1;
    ctx.save(); ctx.globalAlpha = clamp(a,0,1);
    if (kind==='land'){
      const col = P.land, w = Math.max(2, half*1.4);
      ellipse(ctx, g.x, g.y, w, Math.max(2,half*0.85), col);
      ellipse(ctx, g.x - half*0.7, g.y + half*0.3, Math.max(2,half*0.8), Math.max(1,half*0.55), col);
      ellipse(ctx, g.x + half*0.75, g.y - half*0.25, Math.max(2,half*0.7), Math.max(1,half*0.5), P.landDim);
      ctx.globalAlpha = clamp(a*0.5,0,1); ellipse(ctx, g.x, g.y, w+2, Math.max(2,half*0.95), P.landDim);
    } else if (kind==='buoy'){
      circle(ctx, g.x, g.y, Math.max(1,Math.round(half*0.5)), P.echo[1]);
      ctx.globalAlpha = clamp(a*0.6,0,1); ringOutline(ctx, g.x, g.y, Math.max(2,Math.round(half*0.9)), P.echo[1]);
    } else if (kind==='rain'){
      const n=Math.round(10+size*18), rad=half*1.9; ctx.fillStyle=P.rain;
      for(let i=0;i<n;i++){ const rr=hashRnd(i*3.1+t.brgTrue)*rad, aa=hashRnd(i*7.7+t.rngNM)*360*D;
        ctx.fillRect(Math.round(g.x+Math.sin(aa)*rr), Math.round(g.y-Math.cos(aa)*rr), 1, 1); }
    } else if (kind==='flock'){
      const n=Math.round(6+size*10), rad=half*1.2; ctx.fillStyle=P.flock;
      for(let i=0;i<n;i++){ const rr=hashRnd(i*2.3+t.brgTrue)*rad, aa=hashRnd(i*5.1+t.rngNM)*360*D;
        ctx.fillRect(Math.round(g.x+Math.sin(aa)*rr), Math.round(g.y-Math.cos(aa)*rr), 1, 1); }
    } else { // vessel — solid strong return
      const col = P.echo[strengthIdx(size)];
      ellipse(ctx, g.x, g.y, Math.max(2,half*0.9), Math.max(1,half*0.6), col);
      ctx.fillStyle=P.sweepEdge; ctx.fillRect(Math.round(g.x), Math.round(g.y), 1, 1);
      // motion trail (radar afterglow of a moving contact)
      if (o.trails && (t.spd||0)>0){
        const mv=dir(screenAngOf(t.crs||0, o)*D), step=sc.r*0.05*(t.spd||0);
        for(let k=1;k<=5;k++){ ctx.globalAlpha=clamp(a*(1-k/6)*0.7,0,1);
          ellipse(ctx, g.x-mv.x*step*k, g.y-mv.y*step*k, Math.max(1,half*0.5), Math.max(1,half*0.35), col); }
      }
    }
    // selection ring (editor only)
    if (o.sel===t.__i){ ctx.globalAlpha=1; ringOutline(ctx, g.x, g.y, Math.round(half+3), TEAL); }
    ctx.restore();
  }

  // ---- the PPI scope --------------------------------------------------------
  function scopeView(ctx, sc, o, P, fs, sweep){
    const cx=sc.cx, cy=sc.cy, R=sc.r;
    // glass backdrop (radial deepen)
    circle(ctx, cx, cy, R, P.bg);
    { const gg=ctx.createRadialGradient(cx, cy, 2, cx, cy, R); gg.addColorStop(0,'rgba(255,255,255,0.04)'); gg.addColorStop(0.7,'rgba(0,0,0,0)'); gg.addColorStop(1,'rgba(0,0,0,0.35)');
      ctx.save(); ctx.beginPath(); ctx.arc(cx,cy,R,0,Math.PI*2); ctx.clip(); ctx.fillStyle=gg; ctx.fillRect(cx-R,cy-R,R*2,R*2); ctx.restore(); }

    ctx.save(); ctx.beginPath(); ctx.arc(cx, cy, R, 0, Math.PI*2); ctx.clip();

    // sea-clutter speckle near centre (grows with SEA)
    if (o.sea>0.02){ const n=Math.round(o.sea*220); ctx.fillStyle=P.echo[0];
      for(let i=0;i<n;i++){ ctx.save(); ctx.globalAlpha=0.10+0.18*hashRnd(i*1.7); const rr=Math.pow(hashRnd(i*2.9),1.7)*R*0.5, aa=hashRnd(i*4.3)*360*D;
        ctx.fillRect(Math.round(cx+Math.sin(aa)*rr), Math.round(cy-Math.cos(aa)*rr), 1, 1); ctx.restore(); } }
    // rain-clutter haze band (grows with RAIN)
    if (o.rain>0.02){ ctx.save(); ctx.globalAlpha=0.08+0.14*o.rain; const n=Math.round(o.rain*260); ctx.fillStyle=P.rain;
      for(let i=0;i<n;i++){ const rr=(0.3+0.7*hashRnd(i*3.3))*R, aa=hashRnd(i*6.1)*360*D;
        ctx.fillRect(Math.round(cx+Math.sin(aa)*rr), Math.round(cy-Math.cos(aa)*rr), 1, 1); } ctx.restore(); }

    // range rings
    for (let i=1;i<=o.rings;i++){ const rr=Math.round(R*i/o.rings); ringOutline(ctx, cx, cy, rr, i===o.rings?P.ringMaj:P.ringDim); }
    // bearing spokes every 30
    for (let a=0;a<360;a+=30){ const d=dir(a*D); ctx.fillStyle=P.spoke;
      thickLine(ctx, cx+d.x*8, cy+d.y*8, cx+d.x*(R-2), cy+d.y*(R-2), 1, P.spoke); }

    // guard-zone sector
    if (o.guard && o.guard.on){
      const gz=o.guard, near=clamp(gz.near,0,1)*R, far=clamp(gz.far,0,1)*R;
      ctx.save(); ctx.globalCompositeOperation='lighter';
      ctx.beginPath();
      let a0=gz.from, a1=gz.to; if(a1<a0) a1+=360;
      for(let a=a0;a<=a1;a+=2){ const d=dir(a*D); const px=cx+d.x*far, py=cy+d.y*far; if(a===a0) ctx.moveTo(px,py); else ctx.lineTo(px,py); }
      for(let a=a1;a>=a0;a-=2){ const d=dir(a*D); ctx.lineTo(cx+d.x*near, cy+d.y*near); }
      ctx.closePath(); ctx.fillStyle=P.guardFill; ctx.fill(); ctx.restore();
      // edges
      [gz.from,gz.to].forEach(a=>{ const d=dir(a*D); thickLine(ctx, cx+d.x*near, cy+d.y*near, cx+d.x*far, cy+d.y*far, 1, P.guard); });
      ringSeg(ctx, cx, cy, near, gz.from, gz.to, P.guard); ringSeg(ctx, cx, cy, far, gz.from, gz.to, P.guard);
    }

    // sweep — phosphor trailing wedge + bright leading edge
    if (o.tx){
      ctx.save(); ctx.globalCompositeOperation='lighter';
      const span=64, steps=24;
      for(let i=0;i<steps;i++){ const a=sweep - span*(i/steps), al=0.16*(1-i/steps); const d=dir(a*D);
        thickLine(ctx, cx, cy, cx+d.x*R, cy+d.y*R, 2, 'rgba('+P.glow+','+al.toFixed(3)+')'); }
      ctx.restore();
      const d=dir(sweep*D);
      thickLine(ctx, cx, cy, cx+d.x*R, cy+d.y*R, 2, P.sweep);
      thickLine(ctx, cx, cy, cx+d.x*(R-1), cy+d.y*(R-1), 1, P.sweepEdge);
    }

    // targets
    const list=o.targets||[];
    for (let i=0;i<list.length;i++){ const t=list[i]; t.__i=i; echo(ctx, sc, t, o, P, sweep); }

    // guard alarm flash — any target inside the zone
    if (o.guard && o.guard.on && o.blink){
      const hit = list.some(t=>inGuard(t,o)); if(hit){ ctx.save(); ctx.globalAlpha=0.5; ringOutline(ctx, cx, cy, R-1, P.guard); ctx.restore(); }
    }

    // EBL (electronic bearing line)
    if (o.ebl && o.ebl.on){ const sa=screenAngOf(o.ebl.brg,o), d=dir(sa*D);
      dashRadial(ctx, cx, cy, d, R, P.ebl); }
    // VRM (variable range marker)
    if (o.vrm && o.vrm.on){ const rr=clamp((o.vrm.nm)/o.scaleNM,0,1)*R; dashRing(ctx, cx, cy, rr, P.vrm); }

    // heading line + own-ship
    const hla = o.orient==='north' ? (o.heading||0) : 0, hd=dir(hla*D);
    thickLine(ctx, cx, cy, cx+hd.x*R, cy+hd.y*R, 1, P.head);
    ctx.restore(); // unclip

    // bearing scale ticks around the rim
    for (let a=0;a<360;a+=5){ const d=dir(a*D), major=(a%30===0), rI=major?R-6:R-3;
      thickLine(ctx, cx+d.x*rI, cy+d.y*rI, cx+d.x*R, cy+d.y*R, major?1:1, major?P.tickMaj:P.tick); }
    if (fs>=2){ for (let a=0;a<360;a+=30){ const d=dir(a*D), rr=R-13;
      const lbl=(a<100?(a<10?'00':'0'):'')+a; textC(ctx, lbl, cx+d.x*rr, cy+d.y*rr-2, 1, P.tickNum); } }

    // north marker on the rim (red)
    { const na=screenAngOf(0,o), d=dir(na*D); thickLine(ctx, cx+d.x*(R-2), cy+d.y*(R-2), cx+d.x*(R+3), cy+d.y*(R+3), 3, P.north);
      if (fs>=2) textC(ctx, 'N', cx+d.x*(R+8), cy+d.y*(R+8)-2, 1, P.north); }

    // own-ship centre + heading pip
    const hd2=dir(hla*D);
    circle(ctx, cx, cy, Math.max(2,Math.round(R*0.02)), P.head);
    thickLine(ctx, cx, cy, cx+hd2.x*Math.max(6,R*0.10), cy+hd2.y*Math.max(6,R*0.10), 2, P.head);

    // range tag at the bottom of the scope
    if (fs>=2){ const tag=fmtNM(o.scaleNM)+' NM'; ctx.fillStyle=P.plate; const tw=textW(tag,fs);
      ctx.fillRect(cx-tw/2-3, cy+R-5*fs-6, tw+6, 5*fs+4); textC(ctx, tag, cx, cy+R-5*fs-4, fs, P.ink); }
  }
  function ringSeg(ctx, cx, cy, rad, a0, a1, col){ let e=a1; if(e<a0) e+=360; for(let a=a0;a<=e;a+=2){ const d=dir(a*D); ctx.fillStyle=col; ctx.fillRect(Math.round(cx+d.x*rad), Math.round(cy+d.y*rad), 1, 1); } }
  function dashRadial(ctx, cx, cy, d, R, col){ for(let r=6;r<R;r+=6){ ctx.fillStyle=col; ctx.fillRect(Math.round(cx+d.x*r), Math.round(cy+d.y*r), 2, 2); } }
  function dashRing(ctx, cx, cy, rad, col){ for(let a=0;a<360;a+=7){ const d=dir(a*D); ctx.fillStyle=col; ctx.fillRect(Math.round(cx+d.x*rad), Math.round(cy+d.y*rad), 1, 1); } }
  function inGuard(t, o){ const gz=o.guard; if(!gz||!gz.on) return false; const frac=(t.rngNM||0)/o.scaleNM; if(frac<gz.near||frac>gz.far) return false;
    let a=norm360(t.brgTrue||0), a0=norm360(gz.from), a1=norm360(gz.to); if(a1<a0) a1+=360; if(a<a0) a+=360; return a>=a0 && a<=a1; }

  // ---- data panel (only when the box is tall enough) ------------------------
  function dataPanel(ctx, Dp, o, P, fs){
    ctx.fillStyle=P.strip; ctx.fillRect(Dp.x, Dp.y, Dp.w, Dp.h);
    ctx.fillStyle=P.stripDim; ctx.fillRect(Dp.x, Dp.y, Dp.w, 1);
    const s=fs>=3?2:1, lh=Math.max(9, 5*s+4), colW=Dp.w/2, pad=6;
    const rows = [
      ['HDG', fmtDeg(o.heading)+'\u00b0 '+cardinal8(o.heading)],
      ['EBL', o.ebl&&o.ebl.on ? (fmtDeg(o.ebl.brg)+'\u00b0') : '---'],
      ['VRM', o.vrm&&o.vrm.on ? (fmtNM(o.vrm.nm)+' NM') : '---'],
      ['GAIN', Math.round((o.gain||0)*100)+'%'],
      ['SEA', Math.round((o.sea||0)*100)+'%'],
      ['RAIN', Math.round((o.rain||0)*100)+'%'],
      ['TGT', String((o.targets||[]).length)],
      ['GUARD', o.guard&&o.guard.on ? ((o.targets||[]).some(t=>inGuard(t,o))?'ALARM':'SET') : 'OFF'],
    ];
    let y = Dp.y + 4;
    for (let i=0;i<rows.length;i++){
      const col = i%2, rowi=Math.floor(i/2);
      const x = Dp.x + pad + col*colW, yy = y + rowi*lh;
      if (yy+5*s > Dp.y+Dp.h) break;
      let lx = text(ctx, rows[i][0], x, yy, s, P.dim) ;
      const alarm = rows[i][0]==='GUARD' && rows[i][1]==='ALARM';
      text(ctx, rows[i][1], x+Math.round(colW*0.44), yy, s, alarm?P.guard:P.ink);
    }
  }

  // ---- the whole instrument, fitted to (X,Y,WW,HH) --------------------------
  function drawUnit(ctx, X, Y, WW, HH, o){
    o = o || {};
    o = { scaleNM: o.scaleNM==null?6:+o.scaleNM, rings: o.rings==null?4:(o.rings|0)||4,
          orient: o.orient==='north'?'north':'head', heading:+o.heading||0,
          gain:o.gain==null?0.72:+o.gain, sea:o.sea==null?0.18:+o.sea, rain:o.rain==null?0.10:+o.rain,
          tx:o.tx==null?true:!!o.tx, trails:o.trails==null?true:!!o.trails, night:!!o.night,
          sweep:o.sweep==null?((+o.phase||0)*57)%360:norm360(o.sweep), phase:+o.phase||0, blink:o.blink?1:0,
          targets:Array.isArray(o.targets)?o.targets:defaultScene(), sel:o.sel==null?-1:o.sel,
          ebl:o.ebl||{on:false,brg:45}, vrm:o.vrm||{on:false,nm:2},
          guard:o.guard||{on:false,from:20,to:80,near:0.55,far:0.85} };
    const P = o.night ? NIGHT : DAY;
    const fs = HH>=200 ? 3 : HH>=112 ? 2 : 1;
    const L = layout(X, Y, WW, HH);
    ctx.imageSmoothingEnabled = false;

    // case body (graphite, shared with the fleet)
    const cr=Math.max(4,Math.round(HH*0.09));
    rrect(ctx, X, Y, WW, HH, cr, RESIN[0]);
    rrect(ctx, X+1, Y+1, WW-2, HH-2, cr-1, CASE[1]);
    rrect(ctx, X+2, Y+2, WW-4, Math.max(2,Math.round(HH*0.08)), cr-2, CASE[3]);
    rrect(ctx, X+2, Y+HH-Math.max(2,Math.round(HH*0.05)), WW-4, Math.max(2,Math.round(HH*0.04)), 3, CASE[0]);

    // recessed LCD
    const lr=Math.max(3,Math.round(L.lcd.h*0.05));
    rrect(ctx, L.lcd.x-3, L.lcd.y-3, L.lcd.w+6, L.lcd.h+6, lr+1, RESIN[0]);
    rrect(ctx, L.lcd.x-2, L.lcd.y-2, L.lcd.w+4, L.lcd.h+4, lr+1, RESIN[2]);
    ctx.save();
    { const path=new Path2D(), rr=lr, x=L.lcd.x,y=L.lcd.y,w=L.lcd.w,h=L.lcd.h;
      path.moveTo(x+rr,y); path.arcTo(x+w,y,x+w,y+h,rr); path.arcTo(x+w,y+h,x,y+h,rr);
      path.arcTo(x,y+h,x,y,rr); path.arcTo(x,y,x+w,y,rr); path.closePath(); ctx.clip(path); }
    ctx.fillStyle=P.bg2; ctx.fillRect(L.lcd.x, L.lcd.y, L.lcd.w, L.lcd.h);
    statusStrip(ctx, L.status, o, P, fs);
    scopeView(ctx, L.scope, o, P, fs, o.sweep);
    if (L.data.h >= 5*(fs>=3?2:1)+6 && fs>=2) dataPanel(ctx, L.data, o, P, fs);
    ctx.restore();
    ctx.fillStyle='rgba(255,255,255,0.06)'; ctx.fillRect(L.lcd.x+3, L.lcd.y+2, L.lcd.w-6, 1);   // glass glare

    // side pushers
    key(ctx, L.buttons[0], 'MODE',  'ring', P, fs, o.night);
    key(ctx, L.buttons[1], 'RANGE', 'up',   P, fs, o.night);
    key(ctx, L.buttons[2], 'RANGE', 'dn',   P, fs, o.night);

    // brand strip (original wordmark)
    const bs=Math.max(1,fs-1), by=L.brand.y+Math.round((L.brand.h-5*bs)/2);
    text(ctx, 'RD-4', L.brand.x+1, by, bs, TEAL);
    { const s='HARBOUR'; textC(ctx, s, X+WW/2, by, bs, TEAK); }
    { const s='RADAR'; text(ctx, s, L.brand.x+L.brand.w-textW(s,bs), by, bs, BLUE); }
  }

  // ---- standalone sprite ----------------------------------------------------
  function paint(ctx, o){
    ctx.clearRect(0,0,W,H);
    const X=Math.round(W*0.06), Y=Math.round(H*0.11), WW=W-2*X, HH=Math.round(H*0.78);
    drawUnit(ctx, X, Y, WW, HH, o);
  }
  function render(o){ const c=cv(W,H); paint(c.getContext('2d'), o); return c; }

  root.RadarRig = {
    W, H, DEG:D, dir, paint, render, drawUnit, paintInto:drawUnit, layout,
    blipXY, pickAt, inGuard, screenAngOf,
    fmtNM, fmtDeg, cardinal8, ringInterval, RANGE_STEPS, KINDS, C8, defaultScene,
  };
})(typeof globalThis!=='undefined'?globalThis:window);
