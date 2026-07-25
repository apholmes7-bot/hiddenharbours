// Fish Finder — diegetic sonar rig for Hidden Harbours, a drop-in ALTERNATE for
// the Depth Sounder (depthRig.js): SAME graphite case idiom, SAME flush-mount
// footprint and SAME drawUnit(ctx,X,Y,WW,HH,o) contract, so a console/sport dash
// can carry EITHER instrument in the one cutout. Where the sounder shows a single
// 7-seg depth, the finder paints a live colour-sonar screen — a status strip, a
// big depth + water-temp overlay, a right-edge depth ruler, a procedural bottom
// contour (green fuzz -> amber -> red -> dark), and FISH marks the caller places
// ANYWHERE at ANY size. Everything is a parameter; no sprite bakes.
// Brand wordmark is original (FF-2 / HARBOUR / SONAR) — no trademark.
(function (root) {
  const W = 480, H = 660;                 // portrait sonar (2x tall); drawUnit still fits any box

  // ---- palettes (graphite case shared with the fleet) ----------------------
  const CASE  = ['#12171b','#1b2228','#28323a','#3a4750','#4f5e68','#6b7c86'];
  const RESIN = ['#050708','#0c1013','#141b1f'];
  const STEEL = ['#232a30','#39434b','#556069','#7a8892','#9db0b8'];
  const TEAL  = '#7fd6c9', TEAK = '#c9a86a', BLUE = '#5b93b8';
  const RED   = ['#7c2a20','#b83b2e','#e0554a'];

  // sonar water column + button plate, day / night backlight
  const DAY = {
    water1:'#1a4b5a', water2:'#081019', surf:'#e0554a', grid:'rgba(150,200,210,0.09)',
    depthFg:'#8fe6d8', tempFg:'#ffd08a', plate:'rgba(6,12,16,0.52)',
    tick:'#6fc0dc', tickNum:'#e0776b', strip:'#0a141a', stripFg:'#8fe6d8', stripDim:'#3c5560',
    keyPlate:'#2b5a76', keyEdge:'#173648', legend:'#e2eef6', mark:'#bfe0f0', bloom:null,
    // bottom-return bands, top(soft) -> deep(hard)
    hard:'#ffe6a6', band:['#4e9a4a','#c9b53c','#e0902f','#d0552c','#8a2a1c','#2a0e08'],
    fishOut:'#5a1c14', fishA:'#e0554a', fishB:'#e8863c', fishEye:'#2a0f0b', fishTag:'#ffe0b0',
  };
  const NIGHT = {
    water1:'#241a06', water2:'#080502', surf:'#ffb03a', grid:'rgba(255,190,90,0.08)',
    depthFg:'#ffd77a', tempFg:'#ffbe6a', plate:'rgba(10,6,2,0.55)',
    tick:'#c79a3f', tickNum:'#ffb877', strip:'#150d02', stripFg:'#ffd77a', stripDim:'#6b501c',
    keyPlate:'#3a2c0c', keyEdge:'#201604', legend:'#ffe0a0', mark:'#ffd77a', bloom:'#ffbe3d',
    hard:'#fff0c0', band:['#7a7a24','#b59a2c','#c97a24','#b8481f','#7a2416','#1c0a05'],
    fishOut:'#5a2410', fishA:'#ffb24a', fishB:'#ffcf7a', fishEye:'#2a1608', fishTag:'#ffe6bc',
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
    '/':['..#','..#','.#.','#..','#..'],' ':['...','...','...','...','...'],
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
  function rrectStroke(ctx, x, y, w, h, r, fill){
    ctx.fillStyle = fill; r = Math.max(0, Math.min(r, Math.floor(Math.min(w,h)/2)));
    x=Math.round(x); y=Math.round(y); w=Math.round(w); h=Math.round(h);
    for (let v=0; v<h; v++){ const i=rowInset(v,h,r);
      if (v===0||v===h-1){ ctx.fillRect(x+i, y+v, w-2*i, 1); }
      else { ctx.fillRect(x+i, y+v, 1, 1); ctx.fillRect(x+w-1-i, y+v, 1, 1); } }
  }
  function ellipse(ctx, cx, cy, rx, ry, fill){
    ctx.fillStyle = fill; cx=Math.round(cx); cy=Math.round(cy); rx=Math.max(1,Math.round(rx)); ry=Math.max(1,Math.round(ry));
    for (let dy=-ry; dy<=ry; dy++){ const dx=Math.floor(rx*Math.sqrt(Math.max(0,1-(dy*dy)/(ry*ry)))); ctx.fillRect(cx-dx, cy+dy, 2*dx+1, 1); }
  }
  function screw(ctx, cx, cy, r){
    ellipse(ctx, cx, cy, r, r, STEEL[0]); ellipse(ctx, cx, cy, r-1, r-1, STEEL[2]); ellipse(ctx, cx-1, cy-1, Math.max(1,r-2), Math.max(1,r-2), STEEL[3]);
    ctx.fillStyle = STEEL[0]; ctx.fillRect(cx-r+1, cy, 2*r-1, 1);
  }

  // ---- 7-segment numerals (big depth readout) -------------------------------
  const SEG = { '0':'abcdef','1':'bc','2':'abged','3':'abgcd','4':'fgbc','5':'afgcd',
                '6':'afgedc','7':'abc','8':'abcdefg','9':'abfgcd','-':'g',' ':'' };
  function hbar(ctx, cx, cy, len, t){ const h=t/2, l=len/2;
    ctx.beginPath(); ctx.moveTo(cx-l,cy); ctx.lineTo(cx-l+h,cy-h); ctx.lineTo(cx+l-h,cy-h);
    ctx.lineTo(cx+l,cy); ctx.lineTo(cx+l-h,cy+h); ctx.lineTo(cx-l+h,cy+h); ctx.closePath(); ctx.fill(); }
  function vbar(ctx, cx, cy, len, t){ const h=t/2, l=len/2;
    ctx.beginPath(); ctx.moveTo(cx,cy-l); ctx.lineTo(cx+h,cy-l+h); ctx.lineTo(cx+h,cy+l-h);
    ctx.lineTo(cx,cy+l); ctx.lineTo(cx-h,cy+l-h); ctx.lineTo(cx-h,cy-l+h); ctx.closePath(); ctx.fill(); }
  function seg7(ctx, x, y, w, h, ch, onCol){
    const on = SEG[ch]==null?'':SEG[ch], t=Math.max(2, w*0.2), midY=y+h/2, hlen=w-t*0.7, vlen=h/2-t*0.7;
    const put=(k,fn)=>{ if(on.indexOf(k)>=0){ ctx.fillStyle=onCol; fn(); } };
    put('a',()=>hbar(ctx,x+w/2,y,hlen,t));   put('g',()=>hbar(ctx,x+w/2,midY,hlen,t));
    put('d',()=>hbar(ctx,x+w/2,y+h,hlen,t)); put('f',()=>vbar(ctx,x,y+h/4,vlen,t));
    put('b',()=>vbar(ctx,x+w,y+h/4,vlen,t)); put('e',()=>vbar(ctx,x,y+h*3/4,vlen,t));
    put('c',()=>vbar(ctx,x+w,y+h*3/4,vlen,t));
  }
  function numW(str, dw, gap, dpW){ let w=0; for(const ch of str){ w += (ch==='.'?dpW:dw)+gap; } return Math.max(0, w-gap); }
  function drawNum(ctx, str, x, y, dw, dh, gap, dpW, onCol){
    let cx=x;
    for(const ch of str){
      if(ch==='.'){ ctx.fillStyle=onCol; ctx.fillRect(Math.round(cx), Math.round(y+dh-dpW), dpW, dpW); cx+=dpW+gap; }
      else { seg7(ctx, Math.round(cx), y, dw, dh, ch, onCol); cx+=dw+gap; }
    }
  }

  // ---- value helpers --------------------------------------------------------
  const M2FT = 3.28084;
  function fmtDepth(m, ft){ const v = ft ? m*M2FT : m; if (v>=100) return String(Math.round(v)); return v.toFixed(1); }
  function fmtSet(m, ft){ const v = ft ? m*M2FT : m; return v.toFixed(1); }
  const RANGE_STEPS = [10,20,40,60];      // metres — scale max choices
  function defaultSchool(){ return [
    { x:0.60, y:0.15, size:0.85 }, { x:0.52, y:0.285, size:1.15 },
    { x:0.76, y:0.41, size:1.0  }, { x:0.30, y:0.52,  size:1.35 } ]; }

  // ---- layout (box-parameterised; drawUnit + hit-testing both use this) -----
  function layout(X, Y, WW, HH){
    const pad = Math.max(3, Math.round(WW*0.04));
    const brandH = Math.max(7, Math.round(HH*0.115));
    const colW = Math.max(14, Math.round(WW*0.12));
    const bodyH = HH - pad*2 - brandH;
    const lcd = { x:X+pad, y:Y+pad, w:WW-pad*3-colW, h:bodyH };
    const col = { x:X+WW-pad-colW, y:Y+pad, w:colW, h:bodyH };
    const keyH = Math.max(14, Math.min(Math.round(col.w*0.72), Math.floor((col.h - Math.round(col.h*0.16))/3)));
    const span = col.h - keyH;
    const buttons = [0,1,2].map(i=>({ x:col.x, y:Math.round(col.y + span*(i/2)), w:col.w, h:keyH }));
    const brand = { x:X+pad, y:Y+HH-brandH-Math.round(pad*0.3), w:WW-pad*2, h:brandH };
    // regions inside the LCD glass
    const stripH = Math.max(9, Math.round(lcd.w*0.075));   // fixed-height chrome (tied to width, not the tall column)
    const scaleW = Math.max(14, Math.round(lcd.w*0.115));
    const status = { x:lcd.x, y:lcd.y, w:lcd.w, h:stripH };
    const ruler  = { x:lcd.x+lcd.w-scaleW, y:lcd.y+stripH, w:scaleW, h:lcd.h-stripH };
    const sonar  = { x:lcd.x, y:lcd.y+stripH, w:lcd.w-scaleW, h:lcd.h-stripH };
    return { pad, brandH, colW, lcd, col, buttons, brand, status, ruler, sonar };
  }
  // absolute pixel geometry of one fish mark inside a computed layout
  function fishGeom(L, f){
    const S = L.sonar;
    return { cx:S.x + (f.x||0)*S.w, cy:S.y + (f.y||0)*S.h, half:Math.max(4, Math.min(S.w,S.h)*0.06)*(f.size||1) };
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
    else { const q=s+1; for(let i=-q;i<=q;i++){ const wd=(q-Math.abs(i)); ctx.fillRect(cx-wd, dcy+i, wd*2+1, 1); } }
    if (lit){ rrect(ctx, ix, iy, iw, ih-2, r, 'rgba(255,190,60,0.10)'); }
  }

  // ---- a single fish mark ---------------------------------------------------
  function fish(ctx, cx, cy, half, dir, P, sel){
    cx=Math.round(cx); cy=Math.round(cy);
    const rx=half, ry=Math.max(2, half*0.52), body = half>ry*2.3 ? P.fishA : P.fishB;
    // outline
    ellipse(ctx, cx, cy, rx+1, ry+1, P.fishOut);
    // tail (behind, on -dir side)
    const tx = cx - dir*(rx-1), tl = Math.max(2, half*0.7), th = Math.max(2, ry*1.15);
    ctx.fillStyle = P.fishOut;
    for(let i=0;i<tl;i++){ const hh=Math.round(th*(i/tl)); ctx.fillRect(tx - dir*i - (dir<0?0:0), cy-hh, 1, hh*2+1); }
    ctx.fillStyle = body;
    for(let i=0;i<tl-1;i++){ const hh=Math.round((th-1)*(i/tl)); ctx.fillRect(tx - dir*i, cy-hh, 1, hh*2+1); }
    // body + belly sheen
    ellipse(ctx, cx, cy, rx, ry, body);
    if (ry>=3) ellipse(ctx, cx, cy+Math.round(ry*0.45), Math.round(rx*0.7), Math.max(1,Math.round(ry*0.35)), P.fishB);
    // eye near the front
    const ex = cx + dir*Math.round(rx*0.55);
    if (ry>=2){ ctx.fillStyle='#fff'; ctx.fillRect(ex-1, cy-1, 2, 2); ctx.fillStyle=P.fishEye; ctx.fillRect(ex, cy-1, 1, 1); }
    if (sel){ rrectStroke(ctx, cx-rx-3, cy-ry-3, (rx+3)*2, (ry+3)*2, 3, TEAL); }
  }

  // ---- status strip (signal / link / battery / volts) ----------------------
  function statusStrip(ctx, S, o, P, fs){
    ctx.fillStyle = P.strip; ctx.fillRect(S.x, S.y, S.w, S.h);
    ctx.fillStyle = 'rgba(255,255,255,0.05)'; ctx.fillRect(S.x, S.y, S.w, 1);
    const midY = S.y + Math.round((S.h-5*fs)/2), barBase = S.y+S.h-3, unit=Math.max(1,fs);
    let x = S.x + 4;
    text(ctx, 'S', x, midY, fs, P.stripFg); x += textW('S',fs)+3;
    const sBars = Math.round(4*clamp(o.sens,0,1));
    for(let i=0;i<4;i++){ const bh=2+i*Math.max(2,fs); ctx.fillStyle=i<sBars?P.stripFg:P.stripDim; ctx.fillRect(x+i*(unit+1), barBase-bh, unit, bh); }
    x += 4*(unit+1)+7;
    // transducer link
    text(ctx, 'T', x, midY, fs, P.stripFg); x += textW('T',fs)+3;
    const lBars = o.link?3:0;
    for(let i=0;i<3;i++){ const bh=2+i*Math.max(2,fs); ctx.fillStyle=i<lBars?P.stripFg:P.stripDim; ctx.fillRect(x+i*(unit+1), barBase-bh, unit, bh); }
    // battery + volts (right)
    const volts = (o.volts).toFixed(1)+'V';
    const vx = textR(ctx, volts, S.x+S.w-4, midY, fs, P.stripFg);
    const bw=Math.max(8,6*unit), bh=Math.max(5,4*unit), bx=Math.round(S.x+S.w-4-textW(volts,fs)-6-bw), by=S.y+Math.round((S.h-bh)/2);
    rrectStroke(ctx, bx, by, bw, bh, 1, P.stripFg); ctx.fillStyle=P.stripFg; ctx.fillRect(bx+bw, by+1, Math.max(1,unit), bh-2);
    const fill=Math.round((bw-2)*clamp(o.batt,0,1)); ctx.fillStyle = o.batt<0.2?RED[2]:P.stripFg; ctx.fillRect(bx+1, by+1, fill, bh-2);
  }

  // ---- the bottom contour + suspended fish -----------------------------------
  function clamp(v,a,b){ return v<a?a:(v>b?b:v); }
  function hashRnd(n){ const x=Math.sin(n*127.1+13.7)*43758.5453; return x-Math.floor(x); }

  function sonarView(ctx, S, R, o, P){
    // water column gradient
    const g = ctx.createLinearGradient(0, S.y, 0, S.y+S.h);
    g.addColorStop(0, P.water1); g.addColorStop(1, P.water2);
    ctx.fillStyle = g; ctx.fillRect(S.x, S.y, S.w, S.h);
    // faint horizontal grid at ruler divisions
    ctx.fillStyle = P.grid;
    for(let i=1;i<5;i++){ const y=Math.round(S.y + S.h*i/5); ctx.fillRect(S.x, y, S.w, 1); }
    // surface echo line
    ctx.fillStyle = P.surf; ctx.fillRect(S.x, S.y+1, S.w, 1);
    ctx.fillStyle = 'rgba(255,255,255,0.10)'; ctx.fillRect(S.x, S.y, S.w, 1);

    // bottom contour — average sits at the measured depth on the scale
    const ph = o.phase||0;
    const depthFrac = clamp(o.depth/o.range, 0.05, 0.985);
    const bottomAt = (u)=>{ // u in 0..1 across width, newest ping at right
      const w = 0.055*Math.sin(u*6.3 + ph*0.7) + 0.03*Math.sin(u*13.0 - ph*1.05) + 0.017*Math.sin(u*22.0 + ph*1.9);
      return clamp(depthFrac + w, 0.06, 0.995);
    };
    const bands = P.band;
    for(let px=0; px<S.w; px++){
      const u=px/S.w, cx=S.x+px;
      const by=Math.round(S.y + bottomAt(u)*S.h), floorY=S.y+S.h, hgt=floorY-by;
      if(hgt<=0) continue;
      // suspended green fuzz just above the return (weed / thermocline)
      if (hashRnd(px*2.3+Math.floor(ph))>0.86){ ctx.fillStyle=bands[0]; ctx.fillRect(cx, by-1-Math.floor(hashRnd(px)*3), 1, 1); }
      // colour bands top(soft)->deep(hard), boundaries jittered per column
      const j=(hashRnd(px)-0.5);
      const stops=[0.0, 0.12+0.04*j, 0.30+0.05*j, 0.52+0.05*j, 0.74+0.04*j, 1.0];
      for(let bI=0;bI<bands.length;bI++){
        const y0=Math.round(by + hgt*clamp(stops[bI],0,1));
        const y1=Math.round(by + hgt*clamp(stops[bI+1]==null?1:stops[bI+1],0,1));
        if(y1>y0){ ctx.fillStyle=bands[bI]; ctx.fillRect(cx, y0, 1, y1-y0); }
      }
      // hard-bottom top edge
      ctx.fillStyle=P.hard; ctx.fillRect(cx, by, 1, 1);
      // sparse dark speckle for texture
      if (hashRnd(px*5.1+7)>0.8){ const sy=by+2+Math.floor(hashRnd(px*1.7)*Math.max(1,hgt-3)); ctx.fillStyle='rgba(0,0,0,0.28)'; ctx.fillRect(cx, sy, 1, 1); }
    }
    // newest-ping brightening at the right edge
    ctx.fillStyle='rgba(255,255,255,0.05)'; ctx.fillRect(S.x+S.w-2, S.y, 2, S.h);

    // fish marks
    const list = o.fish||[]; const fs = S.h>=150?2:1;
    for(let i=0;i<list.length;i++){
      const f=list[i]; const gm=fishGeom({sonar:S}, f); const dir=(i%2===0)?1:-1;
      fish(ctx, gm.cx, gm.cy, gm.half, dir, P, o.sel===i);
      if (o.fishID){
        const dep=(f.y*o.range), tv=fmtSet(dep, o.ft);
        const tw=textW(tv,fs), tx=Math.round(gm.cx-tw/2), ty=Math.round(gm.cy-gm.half*0.55-6*fs);
        ctx.fillStyle=P.plate; ctx.fillRect(tx-2, ty-1, tw+4, 5*fs+2);
        text(ctx, tv, tx, ty, fs, P.fishTag);
      }
    }
  }

  // ---- right-edge depth ruler -----------------------------------------------
  function ruler(ctx, Rc, o, P, fs){
    ctx.fillStyle='rgba(0,0,0,0.28)'; ctx.fillRect(Rc.x, Rc.y, Rc.w, Rc.h);
    ctx.fillStyle=P.grid; ctx.fillRect(Rc.x, Rc.y, 1, Rc.h);
    const segs=5, unit=o.ft?o.range*M2FT:o.range;
    for(let i=0;i<=segs;i++){
      const y=Math.round(Rc.y + Rc.h*i/segs) - (i===segs?1:0);
      ctx.fillStyle=P.tick; ctx.fillRect(Rc.x+1, y, Math.round(Rc.w*0.45), 1);
      const val=Math.round(unit*i/segs), lbl=String(val);
      const ly=clamp(y - (i===0?0:(i===segs?5*fs:Math.round(5*fs/2))), Rc.y, Rc.y+Rc.h-5*fs);
      text(ctx, lbl, Rc.x+Math.round(Rc.w*0.5)+1, ly, fs, P.tickNum);
      if(i<segs){ const my=Math.round(Rc.y + Rc.h*(i+0.5)/segs); ctx.fillStyle=P.tick; ctx.fillRect(Rc.x+1, my, Math.round(Rc.w*0.28), 1); }
    }
  }

  // ---- big depth + temp overlay (top-left of the sonar) ---------------------
  function readoutOverlay(ctx, S, o, P){
    const str=fmtDepth(o.depth,o.ft), unit=o.ft?'FT':'M';
    const dh=Math.round(Math.min(S.w,S.h)*0.24), dw=Math.round(dh*0.56), t=Math.max(2,Math.round(dw*0.2));
    const gap=Math.max(2,Math.round(dw*0.3)), dpW=Math.max(2,Math.round(dw*0.26));
    const us=Math.max(1, S.h>=150?2:1);
    const total=numW(str,dw,gap,dpW), nx=S.x+Math.max(4,Math.round(S.w*0.03)), ny=S.y+Math.max(4,Math.round(S.w*0.045));
    // plate
    const uW=textW(unit,us), plW=total+uW+us+8, plH=dh+ (5*us) + 10;
    ctx.fillStyle=P.plate; rrect(ctx, nx-4, ny-4, plW+6, plH, 3, P.plate);
    drawNum(ctx, str, nx, ny, dw, dh, gap, dpW, P.depthFg);
    text(ctx, unit, nx+total+us, ny+2, us, P.depthFg);
    // temp under it
    const tv=o.tempC.toFixed(1), ts=us; let tx=text(ctx, tv, nx, ny+dh+5, ts, P.tempFg);
    const k=Math.max(2,ts); degMark(ctx, tx+ts+1, ny+dh+5, k, P.tempFg); text(ctx, 'C', tx+ts+1+k+ts, ny+dh+5, ts, P.tempFg);
  }

  // ---- the whole instrument, fitted to (X,Y,WW,HH) --------------------------
  function drawUnit(ctx, X, Y, WW, HH, o){
    o = o || {};
    o = { depth:o.depth==null?13.6:+o.depth, ft:!!o.ft, night:!!o.night,
          tempC:o.tempC==null?2.7:+o.tempC, range:o.range==null?20:+o.range,
          fish:Array.isArray(o.fish)?o.fish:defaultSchool(), sel:o.sel==null?-1:o.sel,
          fishID:o.fishID==null?true:!!o.fishID, phase:+o.phase||0,
          sens:o.sens==null?0.75:+o.sens, link:o.link==null?true:!!o.link,
          batt:o.batt==null?0.8:+o.batt, volts:o.volts==null?4.0:+o.volts };
    const P = o.night ? NIGHT : DAY;
    const fs = HH>=200 ? 3 : HH>=112 ? 2 : 1;
    const L = layout(X, Y, WW, HH);
    ctx.imageSmoothingEnabled = false;

    if (P.bloom){
      const g=ctx.createRadialGradient(L.lcd.x+L.lcd.w/2, L.lcd.y+L.lcd.h/2, L.lcd.h*0.1, L.lcd.x+L.lcd.w/2, L.lcd.y+L.lcd.h/2, L.lcd.w*0.85);
      g.addColorStop(0,'rgba(255,190,60,0.30)'); g.addColorStop(1,'rgba(255,190,60,0)');
      ctx.fillStyle=g; ctx.fillRect(X-6, Y-6, WW+12, HH+12);
    }
    // case body
    const cr=Math.max(4,Math.round(HH*0.11));
    rrect(ctx, X, Y, WW, HH, cr, RESIN[0]);
    rrect(ctx, X+1, Y+1, WW-2, HH-2, cr-1, CASE[1]);
    rrect(ctx, X+2, Y+2, WW-4, Math.max(2,Math.round(HH*0.10)), cr-2, CASE[3]);
    rrect(ctx, X+2, Y+HH-Math.max(2,Math.round(HH*0.06)), WW-4, Math.max(2,Math.round(HH*0.05)), 3, CASE[0]);

    // recessed LCD
    const lr=Math.max(3,Math.round(L.lcd.h*0.06));
    rrect(ctx, L.lcd.x-3, L.lcd.y-3, L.lcd.w+6, L.lcd.h+6, lr+1, RESIN[0]);
    rrect(ctx, L.lcd.x-2, L.lcd.y-2, L.lcd.w+4, L.lcd.h+4, lr+1, RESIN[2]);
    ctx.save();
    { const path=new Path2D(), rr=lr, x=L.lcd.x,y=L.lcd.y,w=L.lcd.w,h=L.lcd.h;
      path.moveTo(x+rr,y); path.arcTo(x+w,y,x+w,y+h,rr); path.arcTo(x+w,y+h,x,y+h,rr);
      path.arcTo(x,y+h,x,y,rr); path.arcTo(x,y,x+w,y,rr); path.closePath(); ctx.clip(path); }
    statusStrip(ctx, L.status, o, P, fs);
    sonarView(ctx, L.sonar, root.FishRig, o, P);
    ruler(ctx, L.ruler, o, P, fs);
    readoutOverlay(ctx, L.sonar, o, P);
    ctx.restore();
    ctx.fillStyle='rgba(255,255,255,0.08)'; ctx.fillRect(L.lcd.x+3, L.lcd.y+2, L.lcd.w-6, 1);   // glass glare

    // side pushers
    key(ctx, L.buttons[0], 'MODE',  'set', P, fs, o.night);
    key(ctx, L.buttons[1], 'RANGE', 'up',  P, fs, o.night);
    key(ctx, L.buttons[2], 'RANGE', 'dn',  P, fs, o.night);

    // brand strip (original wordmark)
    const bs=Math.max(1,fs-1), by=L.brand.y+Math.round((L.brand.h-5*bs)/2);
    text(ctx, 'FF-2', L.brand.x+1, by, bs, TEAL);
    { const s='HARBOUR'; text(ctx, s, Math.round(X+WW/2-textW(s,bs)/2), by, bs, TEAK); }
    { const s='SONAR'; text(ctx, s, L.brand.x+L.brand.w-textW(s,bs), by, bs, BLUE); }
  }

  // ---- standalone sprite ----------------------------------------------------
  function paint(ctx, o){
    ctx.clearRect(0,0,W,H);
    const X=Math.round(W*0.06), Y=Math.round(H*0.11), WW=W-2*X, HH=Math.round(H*0.78);
    drawUnit(ctx, X, Y, WW, HH, o);
  }
  function render(o){ const c=cv(W,H); paint(c.getContext('2d'), o); return c; }

  root.FishRig = {
    W, H, paint, render, drawUnit, paintInto:drawUnit, layout, fishGeom,
    fmtDepth, fmtSet, M2FT, RANGE_STEPS, defaultSchool,
  };
})(typeof globalThis!=='undefined'?globalThis:window);
