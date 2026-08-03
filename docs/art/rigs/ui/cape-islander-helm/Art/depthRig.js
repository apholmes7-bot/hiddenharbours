// Depth Sounder — diegetic depth-finder rig for Hidden Harbours, sized to flush-
// mount on BOTH centre-console dashes (consoleRig.js / sportRig.js). A compact
// landscape instrument in the F-series idiom: graphite case, recessed LCD, three
// side pushers (SET + two ALARM). EVERYTHING on the LCD is procedural — a big
// 7-seg depth readout, a small water-temp line, unit word, and a shallow-water
// alarm — so any depth / unit / temp / alarm / day-night state draws from params.
// No sprite bakes. Layout is box-parameterised: drawUnit(ctx,X,Y,W,H,o) renders
// the whole instrument crisply at ANY size (big in the showcase, small on a dash).
// Brand wordmark is original (not the reference's) — same layout, no trademark.
(function (root) {
  const W = 480, H = 330;                 // standalone sprite size

  // ---- palettes -------------------------------------------------------------
  const CASE  = ['#12171b','#1b2228','#28323a','#3a4750','#4f5e68','#6b7c86'];  // graphite (matches the fleet)
  const RESIN = ['#050708','#0c1013','#141b1f'];                                // dark LCD surround
  const STEEL = ['#232a30','#39434b','#556069','#7a8892','#9db0b8'];            // screws
  const TEAL  = '#7fd6c9', TEAK = '#c9a86a';
  const RED   = ['#7c2a20','#b83b2e','#e0554a'];
  // LCD — DAY positive grey-green (matches the Watch Face), NIGHT amber backlight
  const DAY   = { bg1:'#cbd2c4', bg2:'#aeb6a6', seg:'#1c2119', off:'rgba(26,32,22,0.07)', dim:'#54604f',
                  keyPlate:'#2b5a76', keyEdge:'#173648', legend:'#e2eef6', mark:'#bfe0f0', bloom:null };
  const NIGHT = { bg1:'#4a3305', bg2:'#2b1d04', seg:'#ffc247', off:'rgba(255,182,64,0.11)', dim:'#c7942f',
                  keyPlate:'#3a2c0c', keyEdge:'#201604', legend:'#ffe0a0', mark:'#ffd77a', bloom:'#ffbe3d' };

  // ---- 3x5 bitmap font ------------------------------------------------------
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
  function textC(ctx, str, cx, y, s, col){ text(ctx, str, Math.round(cx - textW(str,s)/2), y, s, col); }
  // little degree ring, drawn superscript
  function degMark(ctx, x, y, k, col, bg){ ctx.fillStyle=col; ctx.fillRect(x,y,k,k); ctx.fillStyle=bg; ctx.fillRect(x+1,y+1,Math.max(1,k-2),Math.max(1,k-2)); }

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
  function rrectStroke(ctx, x, y, w, h, r, fill){                 // 1px rounded outline
    ctx.fillStyle = fill; r = Math.max(0, Math.min(r, Math.floor(Math.min(w,h)/2)));
    x=Math.round(x); y=Math.round(y); w=Math.round(w); h=Math.round(h);
    for (let v=0; v<h; v++){ const i=rowInset(v,h,r);
      if (v===0||v===h-1){ ctx.fillRect(x+i, y+v, w-2*i, 1); }
      else { ctx.fillRect(x+i, y+v, 1, 1); ctx.fillRect(x+w-1-i, y+v, 1, 1); } }
  }
  function circle(ctx, cx, cy, rad, fill){
    ctx.fillStyle = fill; cx=Math.round(cx); cy=Math.round(cy);
    for (let dy=-rad; dy<=rad; dy++){ const dx=Math.floor(Math.sqrt(Math.max(0,rad*rad-dy*dy))); ctx.fillRect(cx-dx, cy+dy, 2*dx+1, 1); }
  }
  function screw(ctx, cx, cy, r){
    circle(ctx, cx, cy, r, STEEL[0]); circle(ctx, cx, cy, r-1, STEEL[2]); circle(ctx, cx-1, cy-1, Math.max(1,r-2), STEEL[3]);
    ctx.fillStyle = STEEL[0]; ctx.fillRect(cx-r+1, cy, 2*r-1, 1);
  }

  // ---- 7-segment numerals (shared idiom with the Watch Face) ----------------
  const SEG = { '0':'abcdef','1':'bc','2':'abged','3':'abgcd','4':'fgbc','5':'afgcd',
                '6':'afgedc','7':'abc','8':'abcdefg','9':'abfgcd','-':'g',' ':'' };
  function hbar(ctx, cx, cy, len, t){ const h=t/2, l=len/2;
    ctx.beginPath(); ctx.moveTo(cx-l,cy); ctx.lineTo(cx-l+h,cy-h); ctx.lineTo(cx+l-h,cy-h);
    ctx.lineTo(cx+l,cy); ctx.lineTo(cx+l-h,cy+h); ctx.lineTo(cx-l+h,cy+h); ctx.closePath(); ctx.fill(); }
  function vbar(ctx, cx, cy, len, t){ const h=t/2, l=len/2;
    ctx.beginPath(); ctx.moveTo(cx,cy-l); ctx.lineTo(cx+h,cy-l+h); ctx.lineTo(cx+h,cy+l-h);
    ctx.lineTo(cx,cy+l); ctx.lineTo(cx-h,cy+l-h); ctx.lineTo(cx-h,cy-l+h); ctx.closePath(); ctx.fill(); }
  function seg7(ctx, x, y, w, h, ch, onCol, offCol){
    const on = SEG[ch]==null?'':SEG[ch], t=Math.max(3, w*0.19);
    const midY=y+h/2, hlen=w-t*0.7, vlen=h/2-t*0.7;
    const put=(k,fn)=>{ ctx.fillStyle = on.indexOf(k)>=0 ? onCol : offCol; if(ctx.fillStyle) fn(); };
    if (offCol || on){
      put('a',()=>hbar(ctx,x+w/2,y,hlen,t));      put('g',()=>hbar(ctx,x+w/2,midY,hlen,t));
      put('d',()=>hbar(ctx,x+w/2,y+h,hlen,t));     put('f',()=>vbar(ctx,x,y+h/4,vlen,t));
      put('b',()=>vbar(ctx,x+w,y+h/4,vlen,t));      put('e',()=>vbar(ctx,x,y+h*3/4,vlen,t));
      put('c',()=>vbar(ctx,x+w,y+h*3/4,vlen,t));
    }
  }
  // measure / draw a numeric string that may contain a decimal point
  function numW(str, dw, gap, dpW){ let w=0; for(const ch of str){ w += (ch==='.'?dpW:dw)+gap; } return Math.max(0, w-gap); }
  function drawNum(ctx, str, x, y, dw, dh, gap, dpW, onCol, offCol){
    let cx=x;
    for(const ch of str){
      if(ch==='.'){ ctx.fillStyle=onCol; ctx.fillRect(Math.round(cx), Math.round(y+dh-dpW), dpW, dpW); cx+=dpW+gap; }
      else { seg7(ctx, Math.round(cx), y, dw, dh, ch, onCol, offCol); cx+=dw+gap; }
    }
  }

  // ---- value helpers --------------------------------------------------------
  const M2FT = 3.28084;
  function fmtDepth(m, ft){ const v = ft ? m*M2FT : m; if (v>=100) return String(Math.round(v)); return v.toFixed(1); }
  function fmtSet(m, ft){ const v = ft ? m*M2FT : m; return v.toFixed(1); }

  // ---- layout (box-parameterised; both drawUnit & hit-testing use this) ------
  function layout(X, Y, WW, HH){
    const pad = Math.max(3, Math.round(WW*0.045));
    const brandH = Math.max(7, Math.round(HH*0.13));
    const colW = Math.max(16, Math.round(WW*0.215));
    const bodyH = HH - pad*2 - brandH;
    const lcd = { x:X+pad, y:Y+pad, w:WW-pad*3-colW, h:bodyH };
    const col = { x:X+WW-pad-colW, y:Y+pad, w:colW, h:bodyH };
    const bgp = Math.max(2, Math.round(col.h*0.05));
    const bh  = Math.floor((col.h - bgp*2)/3);
    const buttons = [0,1,2].map(i=>({ x:col.x, y:col.y+i*(bh+bgp), w:col.w, h:bh }));
    const brand = { x:X+pad, y:Y+HH-brandH-Math.round(pad*0.3), w:WW-pad*2, h:brandH };
    return { pad, brandH, colW, lcd, col, buttons, brand };
  }

  // ---- one pusher key -------------------------------------------------------
  function key(ctx, b, label, glyph, P, fs, lit){
    rrect(ctx, b.x, b.y, b.w, b.h, Math.max(3,Math.round(b.h*0.24)), CASE[0]);         // recess
    const ix=b.x+2, iy=b.y+2, iw=b.w-4, ih=b.h-4, r=Math.max(2,Math.round(ih*0.22));
    rrect(ctx, ix, iy, iw, ih, r, P.keyEdge);
    rrect(ctx, ix, iy, iw, ih-2, r, P.keyPlate);
    rrect(ctx, ix+2, iy+2, iw-4, Math.max(1,Math.round(ih*0.22)), r-1, 'rgba(255,255,255,0.14)'); // top gloss
    const cx=b.x+b.w/2, cy=b.y+b.h/2;
    // label
    text(ctx, label, Math.round(cx-textW(label,fs)/2), Math.round(iy+ih*0.20), fs, P.legend);
    // up / down diamond indicator (glyph: 'set' | 'up' | 'dn')
    const dcy = Math.round(iy+ih*0.68), s = Math.max(2, fs);
    ctx.fillStyle = P.mark;
    if (glyph==='up'){ for(let i=0;i<s+1;i++) ctx.fillRect(cx-i, dcy-s+ i, i*2+1, 1); }
    else if (glyph==='dn'){ for(let i=0;i<s+1;i++) ctx.fillRect(cx-(s-i), dcy-s+ i, (s-i)*2+1, 1); }
    else { const q=s+1; for(let i=-q;i<=q;i++){ const wd=(q-Math.abs(i)); ctx.fillRect(cx-wd, dcy+i, wd*2+1, 1); } } // diamond
    if (lit){ ctx.fillStyle='rgba(255,190,60,0.12)'; rrect(ctx, ix, iy, iw, ih-2, r, 'rgba(255,190,60,0.10)'); }
  }

  // ---- LCD content ----------------------------------------------------------
  function lcdContent(ctx, L, o, P, fs){
    const m = Math.max(4, Math.round(L.h*0.08));
    const triggered = o.armed && o.depth <= o.alarm;
    // top row: DEPTH label (left) + alarm indicator (right)
    text(ctx, 'DEPTH', L.x+m, L.y+m, fs, P.seg);
    const alTxt = fs>=2 ? 'AL '+fmtSet(o.alarm,o.ft) : 'AL';
    const alW = textW(alTxt, Math.max(1,fs-1));
    const alColor = triggered ? (o.blink?RED[2]:P.dim) : (o.armed?P.dim:P.off && P.dim);
    text(ctx, alTxt, L.x+L.w-m-alW-(fs+2), L.y+m, Math.max(1,fs-1), o.armed?alColor:P.dim);
    { const lampC = triggered ? (o.blink?RED[2]:RED[0]) : (o.armed?P.dim:P.off); const k=Math.max(2,fs);
      ctx.fillStyle=lampC; ctx.fillRect(L.x+L.w-m-k, L.y+m, k, k); }

    // main 7-seg depth number, centred in the upper body
    const str = fmtDepth(o.depth, o.ft);
    const dh  = Math.round(L.h*0.46);
    const dw  = Math.round(dh*0.58);
    const t   = Math.max(2, Math.round(dw*0.19));
    const gap = Math.max(3, Math.round(dw*0.30));
    const dpW = Math.max(2, Math.round(dw*0.26));
    const total = numW(str, dw, gap, dpW);
    const nx = Math.round(L.x + (L.w - total)/2 - L.w*0.02);   // nudge left, leave room for unit
    const ny = Math.round(L.y + L.h*0.30);
    const numCol = (triggered && o.blink) ? RED[2] : P.seg;
    drawNum(ctx, str, nx, ny, dw, dh, gap, dpW, numCol, P.off);

    // unit word (bottom-right)
    const unit = o.ft ? 'FEET' : 'METRES';
    const us = Math.max(1, fs-1);
    text(ctx, unit, L.x+L.w-m-textW(unit,us), L.y+L.h-m-5*us, us, P.seg);

    // water temp (bottom-left) — or SHALLOW banner when triggered
    const ts = Math.max(1, fs-1);
    if (triggered){
      const bw = textW('SHALLOW', ts)+8, bx=L.x+m-2, by=L.y+L.h-m-5*ts-3;
      if (o.blink){ rrect(ctx, bx, by, bw, 5*ts+6, 2, RED[1]); text(ctx, 'SHALLOW', bx+4, by+3, ts, '#fff2ee'); }
      else { text(ctx, 'SHALLOW', bx+4, by+3, ts, RED[2]); }
    } else {
      const tv = fs>=2 ? o.tempC.toFixed(1) : String(Math.round(o.tempC));
      let tx = text(ctx, tv, L.x+m, L.y+L.h-m-5*ts, ts, P.dim);
      const k=Math.max(2,ts); degMark(ctx, tx+ts+1, L.y+L.h-m-5*ts, k, P.dim, P.bg2); 
      text(ctx, 'C', tx+ts+1+k+ts, L.y+L.h-m-5*ts, ts, P.dim);
    }
  }

  // ---- the whole instrument, fitted to (X,Y,WW,HH) --------------------------
  function drawUnit(ctx, X, Y, WW, HH, o){
    o = o || {};
    o = { depth:o.depth==null?26.2:+o.depth, ft:!!o.ft, night:!!o.night,
          armed:o.armed==null?true:!!o.armed, alarm:o.alarm==null?3:+o.alarm,
          tempC:o.tempC==null?14.2:+o.tempC, blink:o.blink?1:0 };
    const P = o.night ? NIGHT : DAY;
    const fs = HH>=200 ? 3 : HH>=112 ? 2 : 1;
    const L = layout(X, Y, WW, HH);
    ctx.imageSmoothingEnabled = false;

    // night bloom behind the glass
    if (P.bloom){
      const g = ctx.createRadialGradient(L.lcd.x+L.lcd.w/2, L.lcd.y+L.lcd.h/2, L.lcd.h*0.1,
                                         L.lcd.x+L.lcd.w/2, L.lcd.y+L.lcd.h/2, L.lcd.w*0.85);
      g.addColorStop(0,'rgba(255,190,60,0.34)'); g.addColorStop(1,'rgba(255,190,60,0)');
      ctx.fillStyle=g; ctx.fillRect(X-6, Y-6, WW+12, HH+12);
    }

    // case body
    const cr = Math.max(4, Math.round(HH*0.11));
    rrect(ctx, X, Y, WW, HH, cr, RESIN[0]);
    rrect(ctx, X+1, Y+1, WW-2, HH-2, cr-1, CASE[1]);
    rrect(ctx, X+2, Y+2, WW-4, Math.max(2,Math.round(HH*0.10)), cr-2, CASE[3]);   // top bevel light
    rrect(ctx, X+2, Y+HH-Math.max(2,Math.round(HH*0.06)), WW-4, Math.max(2,Math.round(HH*0.05)), 3, CASE[0]); // bottom shade

    // recessed LCD
    const lr = Math.max(3, Math.round(L.lcd.h*0.10));
    rrect(ctx, L.lcd.x-3, L.lcd.y-3, L.lcd.w+6, L.lcd.h+6, lr+1, RESIN[0]);
    rrect(ctx, L.lcd.x-2, L.lcd.y-2, L.lcd.w+4, L.lcd.h+4, lr+1, RESIN[2]);
    const gg = ctx.createLinearGradient(0, L.lcd.y, 0, L.lcd.y+L.lcd.h);
    gg.addColorStop(0, P.bg1); gg.addColorStop(1, P.bg2);
    rrect(ctx, L.lcd.x, L.lcd.y, L.lcd.w, L.lcd.h, lr, P.bg1);
    ctx.save(); ctx.fillStyle=gg; { // gradient clipped to the rounded glass
      const path=new Path2D(); const rr=lr;
      path.moveTo(L.lcd.x+rr,L.lcd.y); path.arcTo(L.lcd.x+L.lcd.w,L.lcd.y,L.lcd.x+L.lcd.w,L.lcd.y+L.lcd.h,rr);
      path.arcTo(L.lcd.x+L.lcd.w,L.lcd.y+L.lcd.h,L.lcd.x,L.lcd.y+L.lcd.h,rr);
      path.arcTo(L.lcd.x,L.lcd.y+L.lcd.h,L.lcd.x,L.lcd.y,rr); path.arcTo(L.lcd.x,L.lcd.y,L.lcd.x+L.lcd.w,L.lcd.y,rr); path.closePath();
      ctx.fill(path); } ctx.restore();
    ctx.fillStyle='rgba(255,255,255,0.10)'; ctx.fillRect(L.lcd.x+3, L.lcd.y+2, L.lcd.w-6, 2);   // top glare
    lcdContent(ctx, L.lcd, o, P, fs);
    // alarm border flash across the whole glass
    if (o.armed && o.depth<=o.alarm && o.blink){ rrectStroke(ctx, L.lcd.x, L.lcd.y, L.lcd.w, L.lcd.h, lr, RED[2]); }

    // side pushers
    const ks = fs;
    key(ctx, L.buttons[0], 'SET',   'set', P, ks, o.night);
    key(ctx, L.buttons[1], 'ALARM', 'up',  P, ks, o.night);
    key(ctx, L.buttons[2], 'ALARM', 'dn',  P, ks, o.night);

    // brand strip (original wordmark, tri-tone like the reference layout)
    const bs = Math.max(1, fs-1), by = L.brand.y + Math.round((L.brand.h-5*bs)/2);
    text(ctx, 'DS-2', L.brand.x+1, by, bs, TEAL);
    textC(ctx, 'HARBOUR', X+WW/2, by, bs, TEAK);
    { const s='DEPTH'; text(ctx, s, L.brand.x+L.brand.w-textW(s,bs), by, bs, DAY.keyPlate==='#2b5a76'?'#5b93b8':'#5b93b8'); }
  }

  // ---- standalone sprite ----------------------------------------------------
  function paint(ctx, o){
    ctx.clearRect(0,0,W,H);
    const X=Math.round(W*0.06), Y=Math.round(H*0.11), WW=W-2*X, HH=Math.round(H*0.78);
    drawUnit(ctx, X, Y, WW, HH, o);
  }
  function render(o){ const c=cv(W,H); paint(c.getContext('2d'), o); return c; }

  root.DepthRig = {
    W, H, paint, render, drawUnit, paintInto:drawUnit, layout,
    fmtDepth, fmtSet, M2FT,
  };
})(typeof globalThis!=='undefined'?globalThis:window);
