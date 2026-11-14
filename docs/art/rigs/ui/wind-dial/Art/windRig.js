// Wind Dial — diegetic wind instrument rig for Hidden Harbours. Its OWN rig (same
// render / paintInto / layout contract as compassRig.js and depthRig.js) so any
// console can fit it. Two grades off one dial geometry:
//
//   'dial'    — grade 1, ANALOG. A black face in a graphite case: a boat outline,
//               bow up; ONE needle that points where the APPARENT wind comes from;
//               the port half of the scale ring red, the starboard half green, the
//               close-hauled sectors printed bright; a two-drum knots window that
//               reads APPARENT wind speed. Night lights the ink red (the bulb).
//   'display' — grade 2, LCD. The same dial printed on a grey-green LCD with a
//               segmented pointer, plus four numbers — apparent angle and speed,
//               TRUE angle and speed — and the Beaufort force. The pointer follows
//               apparent or true (`follow`, the unit's one button). Night is the
//               sounders' amber backlight.
//
// Every number it draws is one SailDrive.ResolveWind already returns (Core,
// Boats/SailDrive.cs), through HelmWind.TryRead's grade gate: signed angles, ±180,
// off the bow, POSITIVE = wind over the STARBOARD bow; speeds in KNOTS. Beaufort is
// the true speed through the game's one table (UI/WindReadout.cs Beaufort, m/s).
// The rig never works the wind out; it only draws what it is handed.
//
// Two sizes per grade: 'card' (the raised card, W x H) and 'piece' (the band piece,
// 112 x 144 — the compass piece's width). Brand wordmark ("TELLTALE") is original.
(function (root) {
  const DEG = Math.PI / 180;
  const W = 340, H = 366;          // raised card (the compass card's canvas)
  const PW = 112, PH = 144;        // band piece (HelmBandGeometry.CompassW x H)

  // ---- tunable face printing (the sidecar mirrors these; see README) --------
  const CLOSE_HAULED = [25, 50];   // printed close-hauled sector, degrees off the bow, each side
  const CALM_KN = 1.0;             // below this the LCD shows dashes for that angle (Beaufort 0 ~ 0.97 kn)
  const KN_MAX = 99;               // two drums / two integer digits
  const MS_PER_KN = 0.5144444;     // SailDrive.MetresPerSecondPerKnot
  // WindReadout.BeaufortUpperBoundsMs, verbatim (WMO). PREVIEW COPY ONLY: the port
  // calls WindReadout.Beaufort(SailDrive.ToMetresPerSecond(trueKn)), never this.
  const BFT_MS = [0.5, 1.5, 3.3, 5.5, 7.9, 10.7, 13.8, 17.1, 20.7, 24.4, 28.4, 32.6];

  // ---- palettes -------------------------------------------------------------
  const CASE  = ['#12171b','#1b2228','#28323a','#3a4750','#4f5e68','#6b7c86'];  // graphite (the fleet's)
  const RESIN = ['#050708','#0c1013','#141b1f'];                                // dark LCD surround
  const STEEL = ['#232a30','#39434b','#556069','#7a8892','#9db0b8'];            // screws
  const CHR   = ['#1e242a','#3a454d','#647079','#9fb0b6','#cdd9db','#eff6f6'];  // hub / bezel steel
  const RED   = ['#2a0b08','#6e1f16','#b3372a','#e0554a','#ff6f5c','#ffb2a6'];  // compass red ramp
  // analog face: day and night (night = red bulb, as the compass)
  const DIAL_DAY   = { face:'#0b0f12', lip:'#04070a', ink:'#e9f0f2', dim:'#8a9aa2', hull:'#7d8f98', hullFill:'#11181c',
                       port:'#8e2a1f', stbd:'#1f7a3c', portCH:'#e0554a', stbdCH:'#46c46a',
                       needle:'#f4f1e6', needleSh:'#a7b0b2', tail:'#3a454d', drum:'#05080a', drumInk:'#eef3f2' };
  const DIAL_NIGHT = { face:'#120706', lip:'#04070a', ink:'#ff6f5c', dim:'#b3372a', hull:'#9a3326', hullFill:'#1a0a08',
                       port:'#6e1f16', stbd:'#1d4a28', portCH:'#e0554a', stbdCH:'#3c9a55',
                       needle:'#ffb2a6', needleSh:'#c45a4a', tail:'#3a1510', drum:'#080303', drumInk:'#ff8a78' };
  // LCD: the depth sounder's palettes, verbatim (depthRig.js:20-23) so the family matches
  const LCD_DAY   = { bg1:'#cbd2c4', bg2:'#aeb6a6', seg:'#1c2119', off:'rgba(26,32,22,0.07)', dim:'#54604f',
                      port:'#b3372a', stbd:'#2a8a48', glare:'rgba(255,255,255,0.07)' };
  const LCD_NIGHT = { bg1:'#4a3305', bg2:'#2b1d04', seg:'#ffc247', off:'rgba(255,182,64,0.11)', dim:'#c7942f',
                      port:'#8a2a18', stbd:'#3f6a1e', glare:'rgba(255,214,140,0.05)' };
  // the band housing, mirrored from HelmBandPlateRender (Rim/Face day+night) so the piece sits in the band
  const PLATE = { rimDay:'#161a1e', rimNight:'#080a0c', faceDay:'#3e464e', faceNight:'#1e1e1e',
                  insetX:4, insetY:8, radius:6, rimPx:2 };

  // ---- 3x5 bitmap font: depthRig.js's letters; BLOCK digits (the compass's open 3/6/9 read as one
  //      another at band size, measured on the 112 px piece) -----------------------
  const F = {
    'A':['.#.','#.#','###','#.#','#.#'],'B':['##.','#.#','##.','#.#','##.'],
    'E':['###','#..','##.','#..','###'],'F':['###','#..','##.','#..','#..'],
    'K':['#.#','#.#','##.','#.#','#.#'],'L':['#..','#..','#..','#..','###'],
    'N':['#.#','##.','###','.##','#.#'],'P':['##.','#.#','##.','#..','#..'],
    'R':['##.','#.#','##.','#.#','#.#'],'S':['.##','#..','.#.','..#','##.'],
    'T':['###','.#.','.#.','.#.','.#.'],'U':['#.#','#.#','#.#','#.#','.#.'],
    'W':['#.#','#.#','###','###','#.#'],
    '0':['###','#.#','#.#','#.#','###'],'1':['.#.','##.','.#.','.#.','###'],
    '2':['###','..#','###','#..','###'],'3':['###','..#','.##','..#','###'],
    '4':['#.#','#.#','###','..#','..#'],'5':['###','#..','###','..#','###'],
    '6':['###','#..','###','#.#','###'],'7':['###','..#','..#','..#','..#'],
    '8':['###','#.#','###','#.#','###'],'9':['###','#.#','###','..#','###'],
    '-':['...','...','###','...','...'],' ':['...','...','...','...','...'],
  };
  function textW(str, s){ return String(str).length * (3*s + s) - s; }
  function text(ctx, str, x, y, s, col){
    ctx.fillStyle = col; str = String(str).toUpperCase(); let cx = Math.round(x); y = Math.round(y);
    for (const ch of str){ const g = F[ch] || F[' '];
      for (let r=0;r<5;r++) for (let c=0;c<3;c++) if (g[r][c]==='#') ctx.fillRect(cx+c*s, y+r*s, s, s);
      cx += 3*s + s; }
    return cx - s;
  }
  // centred on (cx, cy) both ways — the compass's textC
  function textC(ctx, str, cx, cy, s, col){ text(ctx, str, Math.round(cx - textW(str,s)/2), Math.round(cy - (5*s)/2), s, col); }

  // ---- crisp primitives (compassRig.js's, unchanged) -------------------------
  const cv = (w,h)=>{ const c=document.createElement('canvas'); c.width=Math.round(w); c.height=Math.round(h); return c; };
  function rowInset(v, h, r){
    if (v < r)      { const dy=r-v;       return r - Math.floor(Math.sqrt(Math.max(0,r*r-dy*dy))); }
    if (v >= h - r) { const dy=v-(h-r)+1; return r - Math.floor(Math.sqrt(Math.max(0,r*r-dy*dy))); }
    return 0;
  }
  function rrect(ctx, x, y, w, h, r, fill){
    ctx.fillStyle=fill; r=Math.max(0,Math.min(r,Math.floor(Math.min(w,h)/2)));
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
  // signed dial angle of a pixel offset: 0 = up (the bow), + clockwise (starboard), ±180 = astern
  const angOf = (dx, dy)=> Math.atan2(dx, -dy) / DEG;
  // screen unit vector of a dial angle (the compass's dir())
  const dir = (deg)=>({ x:Math.sin(deg*DEG), y:-Math.cos(deg*DEG) });
  // Fill the pixels of an annulus whose dial angle lies in [a0, a1] (signed degrees). Pixel-scan,
  // so it is crisp, needs no path AA, and ports as two loops.
  function arcBand(ctx, cx, cy, rI, rO, a0, a1, fill){
    ctx.fillStyle=fill; cx=Math.round(cx); cy=Math.round(cy);
    const i2=rI*rI, o2=rO*rO;
    for (let dy=-rO; dy<=rO; dy++){
      let run=null;                                            // start of the open run (dx can be negative)
      for (let dx=-rO; dx<=rO+1; dx++){
        const d2=dx*dx+dy*dy, a=angOf(dx,dy);
        const inside = dx<=rO && d2>=i2 && d2<=o2 && a>=a0 && a<=a1;
        if (inside && run===null) run=dx;
        else if (!inside && run!==null){ ctx.fillRect(cx+run, cy+dy, dx-run, 1); run=null; }
      }
    }
  }
  // Vertical fill between two colours, one flat colour per row (the LCD's backing).
  function hex2rgb(h){ return [parseInt(h.substr(1,2),16), parseInt(h.substr(3,2),16), parseInt(h.substr(5,2),16)]; }
  function vgrad(ctx, x, y, w, h, c0, c1){
    const a=hex2rgb(c0), b=hex2rgb(c1);
    for (let v=0; v<h; v++){ const t = h>1 ? v/(h-1) : 0;
      ctx.fillStyle='rgb('+Math.round(a[0]+(b[0]-a[0])*t)+','+Math.round(a[1]+(b[1]-a[1])*t)+','+Math.round(a[2]+(b[2]-a[2])*t)+')';
      ctx.fillRect(Math.round(x), Math.round(y)+v, Math.round(w), 1); }
  }

  // ---- 7-segment numerals, all rectangles (crisp; a fillRect per lit bar) ----
  const SEG = { '0':'abcdef','1':'bc','2':'abged','3':'abgcd','4':'fgbc','5':'afgcd',
                '6':'afgedc','7':'abc','8':'abcdefg','9':'abfgcd','-':'g',' ':'' };
  function seg7(ctx, x, y, w, h, t, ch, on, off){
    const s = SEG[ch]==null ? '' : SEG[ch], hm = Math.floor((h - t)/2);
    const bars = {
      a:[x+t, y, w-2*t, t],          g:[x+t, y+hm, w-2*t, t],        d:[x+t, y+h-t, w-2*t, t],
      f:[x, y+t, t, hm-t],           b:[x+w-t, y+t, t, hm-t],
      e:[x, y+hm+t, t, h-t-hm-t],    c:[x+w-t, y+hm+t, t, h-t-hm-t] };
    for (const k in bars){ const col = s.indexOf(k)>=0 ? on : off; if (!col) continue;
      const b = bars[k]; ctx.fillStyle = col; ctx.fillRect(b[0], b[1], b[2], b[3]); }
  }
  // a numeral string; '.' is a t×t point that takes `dp` px of advance
  function numW(str, dw, gap, dp){ let w=0; for (const ch of str) w += (ch==='.'?dp:dw)+gap; return Math.max(0, w-gap); }
  function drawNum(ctx, str, x, y, dw, dh, t, gap, dp, on, off){
    let cx = Math.round(x);
    for (const ch of str){
      if (ch==='.'){ ctx.fillStyle=on; ctx.fillRect(cx, y+dh-t, t, t); cx += dp+gap; }
      else { seg7(ctx, cx, y, dw, dh, t, ch, on, off); cx += dw+gap; }
    }
  }

  // ---- value helpers (formatting only; no wind maths) -----------------------
  const clampAng = (a)=>{ a = +a || 0; a = ((a + 180) % 360 + 360) % 360 - 180; return a === -180 ? 180 : a; };
  const kn = (v)=> Math.max(0, +v || 0);
  /** Beaufort force for a TRUE wind speed in knots — preview copy of WindReadout.Beaufort. */
  function beaufort(trueKn){ const ms = kn(trueKn) * MS_PER_KN;
    for (let f=0; f<BFT_MS.length; f++) if (ms < BFT_MS[f]) return f; return 12; }
  /** Analog drum text: whole knots, two digits, 00..99. */
  const fmtDrum = (k)=>{ const v = Math.min(KN_MAX, Math.round(kn(k))); return (v<10?'0':'') + v; };
  /** LCD speed: one decimal, right-aligned in 4 cells ("12.4", " 9.8"), capped at 99.9. */
  const fmtKn = (k)=>{ const v = Math.min(99.9, Math.round(kn(k)*10)/10); const s = v.toFixed(1); return s.length < 4 ? ' '+s : s; };
  /** LCD angle: magnitude, three digits ("042"); dashes in a calm. */
  const fmtAng = (a, k)=>{ if (kn(k) < CALM_KN) return '---'; const d = Math.round(Math.abs(clampAng(a))); return (d<10?'00':d<100?'0':'')+d; };
  /** Side letter for a signed angle: S (starboard, +), P (port, −), blank dead ahead/astern or calm. */
  const side = (a, k)=>{ if (kn(k) < CALM_KN) return ' '; const d = Math.round(clampAng(a)); return (d===0||Math.abs(d)===180) ? ' ' : (d>0 ? 'S' : 'P'); };

  // ---- shared pieces --------------------------------------------------------
  // The boat, bow up: half-beam per row. Fine bow, full shoulders, a slightly narrower transom.
  function hullHalf(t){                    // t: 0 at the bow tip .. 1 at the transom
    if (t < 0.42){ const u = t/0.42; return Math.sqrt(u) * (1 - 0.18*(1-u)); }   // bow entry
    return 1 - 0.22 * Math.pow((t-0.42)/0.58, 1.6);                              // run aft
  }
  function hull(ctx, cx, top, len, beam, edge, fill, px){
    cx = Math.round(cx); top = Math.round(top); px = px || 2;
    const half = beam/2;
    let lastL = cx, lastR = cx;
    for (let v=0; v<=len; v++){
      const hw = Math.round(hullHalf(v/len) * half), L = cx-hw, R = cx+hw, y = top+v;
      ctx.fillStyle = fill; if (R-L-2*px > 0) ctx.fillRect(L+px, y, R-L-2*px+1, 1);
      ctx.fillStyle = edge;
      // join the edge to the previous row so a steep bow is unbroken
      const l0 = Math.min(L, lastL), l1 = Math.max(L, lastL), r0 = Math.min(R, lastR), r1 = Math.max(R, lastR);
      ctx.fillRect(l0, y, Math.max(px, l1-l0+1), 1); ctx.fillRect(r1-Math.max(px, r1-r0+1)+1, y, Math.max(px, r1-r0+1), 1);
      lastL = L; lastR = R;
    }
    const tw = Math.round(hullHalf(1)*half);
    ctx.fillStyle = edge; ctx.fillRect(cx-tw, top+len-px+1, 2*tw+1, px);           // transom
  }

  // The analog needle: a tapered blade from the hub to rTip at `ang`, a counterweight tail to rTail.
  function needle(ctx, cx, cy, ang, rTip, rTail, wide, P){
    const o = dir(ang), px = -o.y, py = o.x;
    const n = Math.ceil(rTip);
    for (let i=0; i<=n; i++){                                  // blade: wide at the hub, 1 px at the tip
      const w = Math.max(1, Math.round(wide * (1 - i/n))), X = cx + o.x*i, Y = cy + o.y*i;
      for (let j=-(w>>1); j<=((w-1)>>1); j++){
        ctx.fillStyle = j < 0 ? P.needleSh : P.needle;         // one shaded flank sells the bevel
        ctx.fillRect(Math.round(X+px*j), Math.round(Y+py*j), 1, 1);
      }
    }
    thickLine(ctx, cx, cy, cx - o.x*rTail, cy - o.y*rTail, Math.max(3, wide), P.tail);   // tail
  }

  // ---- GRADE 1: the analog dial -----------------------------------------------
  // Geometry per size, in canvas px. ONE table: render, layout() and the sidecar all read it.
  const DIAL_GEOM = {
    card:  { cx:170, cy:168, rCase:147, rBezel:143, rFace:133, band:[122,130], ch:[114,130],
             tick:[104,119], tickMajor:[100,119], labelR:88, hull:{ top:-50, len:94, beam:40, px:2 },
             needle:{ tip:118, tail:30, wide:6, hub:9 },
             drum:{ x:-21, y:56, w:42, h:23, s:3 }, knLabel:{ y:88, s:1 },
             wordmark:{ y:332, s:2 } },
    piece: { cx:56, cy:72, rCase:40, rBezel:39, rFace:36, band:[32,35], ch:[28,35],
             tick:[25,31], tickMajor:[23,31], labelR:0, hull:{ top:-15, len:27, beam:12, px:1 },
             needle:{ tip:31, tail:9, wide:3, hub:3 },
             drum:{ x:-8, y:13, w:17, h:12, s:2 }, knLabel:null, wordmark:null },
  };

  // The static face (everything but the needle and the drum digits): cached per size × night.
  const _faceCache = {};
  function dialFace(size, night){
    const key = 'dial|'+size+'|'+(night?1:0); if (_faceCache[key]) return _faceCache[key];
    const G = DIAL_GEOM[size], P = night ? DIAL_NIGHT : DIAL_DAY;
    const c = size==='card' ? cv(W,H) : cv(PW,PH), g = c.getContext('2d'), cx = G.cx, cy = G.cy;
    if (size==='card') caseBody(g, night); else plate(g, night);
    // bezel: cutout shadow, steel ring lit on its upper half, black inner lip
    circle(g, cx, cy, G.rCase, '#04070a');
    ring(g, cx, cy, G.rBezel, G.rFace+1, CHR[2]);
    g.save(); g.beginPath(); g.rect(cx-G.rBezel-1, cy-G.rBezel-1, (G.rBezel+1)*2, G.rBezel+1); g.clip();
    ring(g, cx, cy, G.rBezel, G.rFace+1, night ? CHR[2] : CHR[3]); g.restore();
    ring(g, cx, cy, G.rBezel, G.rBezel-1, CHR[1]);
    circle(g, cx, cy, G.rFace+1, P.lip);
    circle(g, cx, cy, G.rFace, P.face);
    // the colour ring: port half red, starboard half green; close-hauled sectors bright and deeper
    arcBand(g, cx, cy, G.band[0], G.band[1], -180, 0, P.port);
    arcBand(g, cx, cy, G.band[0], G.band[1], 0, 180, P.stbd);
    arcBand(g, cx, cy, G.ch[0], G.ch[1], -CLOSE_HAULED[1], -CLOSE_HAULED[0], P.portCH);
    arcBand(g, cx, cy, G.ch[0], G.ch[1], CLOSE_HAULED[0], CLOSE_HAULED[1], P.stbdCH);
    // graduations: every 10° on the card, every 30° on the piece; majors at 30°
    const step = size==='card' ? 10 : 30;
    for (let d=-170; d<=180; d+=step){
      const major = d % 30 === 0, r = major ? G.tickMajor : G.tick, o = dir(d);
      if (d === 0) continue;                                   // the bow carries its own mark
      thickLine(g, cx+o.x*r[0], cy+o.y*r[0], cx+o.x*r[1], cy+o.y*r[1], (major && size==='card') ? 2 : 1, major ? P.ink : P.dim);
    }
    // the bow mark at 0: a solid triangle pointing in
    const bt = G.tickMajor, bw = size==='card' ? 6 : 3;
    for (let v=0; v<=bt[1]-bt[0]; v++){ const hw = Math.round(bw * (1 - v/(bt[1]-bt[0])));
      g.fillStyle = P.ink; g.fillRect(cx-hw, cy-bt[1]+v, 2*hw+1, 1); }
    // the close-hauled ends: a 1 px dark cut on the bright band at each edge (reads as printed marks)
    [-CLOSE_HAULED[1], -CLOSE_HAULED[0], CLOSE_HAULED[0], CLOSE_HAULED[1]].forEach(d=>{
      const o = dir(d); thickLine(g, cx+o.x*G.ch[0], cy+o.y*G.ch[0], cx+o.x*G.ch[1], cy+o.y*G.ch[1], 1, P.face); });
    // numerals 30..150 each side (card only)
    if (G.labelR){
      [30,60,90,120,150].forEach(d=>{ [d,-d].forEach(a=>{ const o = dir(a);
        textC(g, String(d), cx+o.x*G.labelR, cy+o.y*G.labelR, 2, P.ink); }); });
    }
    // the boat, bow up
    hull(g, cx, cy+G.hull.top, G.hull.len, G.hull.beam, P.hull, P.hullFill, G.hull.px);
    // the knots window: a recessed slot with two drum cells
    const D = G.drum, dx = cx + D.x, dy = cy + D.y;
    rrect(g, dx-2, dy-2, D.w+4, D.h+4, 3, P.lip);
    rrect(g, dx-1, dy-1, D.w+2, D.h+2, 2, CHR[1]);
    g.fillStyle = P.drum; g.fillRect(dx, dy, D.w, D.h);
    g.fillStyle = CHR[1]; g.fillRect(dx + (D.w>>1), dy, 1, D.h);                 // drum divider
    if (G.knLabel) textC(g, 'KN', cx, cy + G.knLabel.y, G.knLabel.s, P.dim);
    if (G.wordmark) textC(g, 'TELLTALE', cx, G.wordmark.y, G.wordmark.s, night ? RED[2] : CASE[5]);
    return (_faceCache[key] = c);
  }

  // The live parts of the dial: drum digits, needle, hub, glass. Stateless.
  function paintDialLive(ctx, ox, oy, size, o){
    const G = DIAL_GEOM[size], P = o.night ? DIAL_NIGHT : DIAL_DAY, cx = ox+G.cx, cy = oy+G.cy;
    const D = G.drum, dx = cx + D.x, dy = cy + D.y, s = D.s, txt = fmtDrum(o.aws);
    const cw = D.w>>1, gh = 5*s, gw = 3*s;
    for (let i=0;i<2;i++) text(ctx, txt[i], dx + i*cw + Math.round((cw-gw)/2), dy + Math.round((D.h-gh)/2), s, P.drumInk);
    needle(ctx, cx, cy, clampAng(o.awa), G.needle.tip, G.needle.tail, G.needle.wide, P);
    circle(ctx, cx, cy, G.needle.hub, CHR[1]); circle(ctx, cx, cy, G.needle.hub-1, CHR[3]);
    circle(ctx, cx-1, cy-1, Math.max(1, G.needle.hub-3), CHR[5]);
    glassSheen(ctx, cx, cy, G.rFace, o.night);
  }

  // Flat-ish glass: a pixel arc highlight upper-left, a fainter one lower-right, both at mid-radius. Alpha fills
  // only (ports as blends). No hard glint: at band size a glint reads as a stray tick mark.
  function glassSheen(ctx, cx, cy, r, night){
    // inside the numerals and clear of the colour ring, so it never dulls the port/starboard print
    arcBand(ctx, cx, cy, Math.round(r*0.42), Math.round(r*0.48), -72, -28, night ? 'rgba(255,150,120,0.08)' : 'rgba(255,255,255,0.10)');
    arcBand(ctx, cx, cy, Math.round(r*0.44), Math.round(r*0.47), 112, 148, night ? 'rgba(255,150,120,0.05)' : 'rgba(170,205,225,0.07)');
  }

  // ---- GRADE 2: the LCD display -----------------------------------------------
  const LCD_GEOM = {
    card:  { win:{x:34,y:30,w:272,h:290,r:8}, glass:{x:42,y:38,w:256,h:274},
             cx:170, cy:126, band:[80,83], segR:[68,76], segStep:10, pointer:[20,66], hull:{ top:-30, len:56, beam:24, px:2 },
             mode:{ x:52, y:48, s:2 }, bft:{ x:290, y:46, labelS:1, dw:14, dh:24, t:3 },
             rows:[
               { key:'awa', x:52,  y:222, label:'AWA', kind:'angle' },
               { key:'aws', x:178, y:222, label:'AWS', kind:'speed' },
               { key:'twa', x:52,  y:268, label:'TWA', kind:'angle' },
               { key:'tws', x:178, y:268, label:'TWS', kind:'speed' } ],
             digit:{ dw:14, dh:24, t:3, gap:4, dp:4, labelS:1, sideS:3, labelH:9 },
             wordmark:{ y:338, s:2 } },
    piece: { win:{x:10,y:16,w:92,h:112,r:4}, glass:{x:13,y:19,w:86,h:106},
             cx:56, cy:58, band:[30,32], segR:[25,29], segStep:30, pointer:[7,24], hull:{ top:-11, len:21, beam:9, px:1 },
             mode:{ x:16, y:22, s:1 },
             speed:{ cx:56, y:98, dw:7, dh:13, t:2, gap:2, dp:2, labelY:92 } },
  };

  function lcdFace(size, night){
    const key = 'lcd|'+size+'|'+(night?1:0); if (_faceCache[key]) return _faceCache[key];
    const G = LCD_GEOM[size], P = night ? LCD_NIGHT : LCD_DAY;
    const c = size==='card' ? cv(W,H) : cv(PW,PH), g = c.getContext('2d'), cx = G.cx, cy = G.cy;
    if (size==='card') caseBody(g, night); else plate(g, night);
    const wn = G.win, gl = G.glass;
    rrect(g, wn.x-1, wn.y-1, wn.w+2, wn.h+2, wn.r+1, '#04070a');
    rrect(g, wn.x, wn.y, wn.w, wn.h, wn.r, RESIN[1]);
    g.fillStyle = RESIN[2]; g.fillRect(wn.x+wn.r, wn.y, wn.w-2*wn.r, 1);           // lit top lip
    vgrad(g, gl.x, gl.y, gl.w, gl.h, P.bg1, P.bg2);
    // printed red/green arc on the glass, outside the segment ring; close-hauled sectors thicker
    arcBand(g, cx, cy, G.band[0], G.band[1], -180, 0, P.port);
    arcBand(g, cx, cy, G.band[0], G.band[1], 0, 180, P.stbd);
    arcBand(g, cx, cy, G.band[0]-2, G.band[1]+1, -CLOSE_HAULED[1], -CLOSE_HAULED[0], P.port);
    arcBand(g, cx, cy, G.band[0]-2, G.band[1]+1, CLOSE_HAULED[0], CLOSE_HAULED[1], P.stbd);
    // the segment ring: every cell printed as a ghost, majors (30°) printed solid as the scale
    for (let d=-180+G.segStep; d<=180; d+=G.segStep){
      const o = dir(d), major = d % 30 === 0;
      thickLine(g, cx+o.x*G.segR[0], cy+o.y*G.segR[0], cx+o.x*G.segR[1], cy+o.y*G.segR[1], size==='card'?3:1, P.off);
      if (major) thickLine(g, cx+o.x*(G.segR[1]-(size==='card'?4:2)), cy+o.y*(G.segR[1]-(size==='card'?4:2)),
                              cx+o.x*G.segR[1], cy+o.y*G.segR[1], size==='card'?2:1, P.dim);
    }
    // the bow index and the printed boat
    const o0 = dir(0); thickLine(g, cx, cy-G.segR[1], cx, cy-G.segR[0]+2, size==='card'?3:1, P.seg);
    hull(g, cx, cy+G.hull.top, G.hull.len, G.hull.beam, P.dim, P.dim, G.hull.px);      // printed solid
    if (size==='card'){
      // printed legends: P / S either side, 30..150 numbers on the starboard side only (glass print)
      const D = G.digit;
      textC(g, 'P', cx - G.band[1] - 10, cy, 2, P.port); textC(g, 'S', cx + G.band[1] + 10, cy, 2, P.stbd);
      G.rows.forEach(rw=> text(g, rw.label, rw.x, rw.y, D.labelS, P.dim));
      text(g, 'BFT', G.bft.x - textW('BFT', 1), G.bft.y, 1, P.dim);
      text(g, 'KN', G.rows[1].x + 96, G.rows[1].y, 1, P.dim); text(g, 'KN', G.rows[3].x + 96, G.rows[3].y, 1, P.dim);
      g.fillStyle = P.dim; g.fillRect(gl.x+8, 210, gl.w-16, 1);                    // printed rule
      textC(g, 'TELLTALE', 170, G.wordmark.y, G.wordmark.s, night ? '#7a5a1c' : CASE[5]);
    } else {
      textC(g, 'KN', G.speed.cx + 30, G.speed.y + 7, 1, P.dim);
    }
    // a soft diagonal glare band (alpha rows, crisp)
    for (let v=0; v<gl.h; v++){ const x0 = gl.x + Math.round(gl.w*0.55) - Math.round(v*0.55);
      const x1 = Math.max(gl.x, x0), x2 = Math.min(gl.x+gl.w, x0 + Math.round(gl.w*0.16));
      if (x2 > x1){ g.fillStyle = P.glare; g.fillRect(x1, gl.y+v, x2-x1, 1); } }
    return (_faceCache[key] = c);
  }

  // The segmented pointer: the ring cell nearest the angle lit solid, then a dotted spoke.
  function lcdPointer(ctx, cx, cy, ang, G, P, size){
    const step = G.segStep, cell = Math.round(ang / step) * step, oc = dir(cell === -180 ? 180 : cell);
    thickLine(ctx, cx+oc.x*G.segR[0], cy+oc.y*G.segR[0], cx+oc.x*G.segR[1], cy+oc.y*G.segR[1], size==='card'?3:1, P.seg);
    const o = dir(ang), r0 = G.pointer[0], r1 = G.pointer[1], dot = size==='card' ? 2 : 1, pitch = size==='card' ? 4 : 2;
    ctx.fillStyle = P.seg;
    for (let r=r0; r<=r1; r+=pitch) ctx.fillRect(Math.round(cx+o.x*r - dot/2), Math.round(cy+o.y*r - dot/2), dot, dot);
    // arrowhead at the tip
    const tip = r1 + (size==='card'?2:1), back = size==='card'?8:3, half = size==='card'?4:2, px=-o.y, py=o.x;
    for (let i=0;i<=back;i++){ const w = Math.round(half*(i/back));
      for (let j=-w;j<=w;j++) ctx.fillRect(Math.round(cx+o.x*(tip-i)+px*j), Math.round(cy+o.y*(tip-i)+py*j), 1, 1); }
  }

  function paintLcdLive(ctx, ox, oy, size, o){
    const G = LCD_GEOM[size], P = o.night ? LCD_NIGHT : LCD_DAY, cx = ox+G.cx, cy = oy+G.cy;
    const followTrue = o.follow === 'true' && o.hasTrue !== false;
    const ang = followTrue ? o.twa : o.awa, spd = followTrue ? o.tws : o.aws;
    if (kn(spd) >= CALM_KN) lcdPointer(ctx, cx, cy, clampAng(ang), G, P, size);
    if (size === 'card'){
      // mode tag: an inverted box behind the source name
      const tag = followTrue ? 'TRUE' : 'APP', M = G.mode, tw = textW(tag, M.s);
      ctx.fillStyle = P.seg; ctx.fillRect(ox+M.x-2, oy+M.y-2, tw+4, 5*M.s+4);
      text(ctx, tag, ox+M.x, oy+M.y, M.s, P.bg1);                                // inverted: glass colour on ink
      // Beaufort, right-aligned under its legend
      const B = G.bft, f = String(beaufort(o.tws)), fw = numW(f, B.dw, 4, 0);
      drawNum(ctx, f, ox + B.x - fw, oy + B.y + 9, B.dw, B.dh, B.t, 4, 0, P.seg, P.off);
      text(ctx, 'F', ox + B.x - fw - 4 - textW('F', 2), oy + B.y + 9 + B.dh - 10, 2, P.seg);
      const D = G.digit;
      G.rows.forEach(rw=>{
        const x = ox + rw.x, y = oy + rw.y + D.labelH, k = rw.key;
        const isTrue = k === 'twa' || k === 'tws';
        if (isTrue && o.hasTrue === false){ drawNum(ctx, rw.kind==='angle' ? '---' : ' --', x + 18, y, D.dw, D.dh, D.t, D.gap, D.dp, P.seg, P.off); return; }
        if (rw.kind === 'angle'){
          const a = isTrue ? o.twa : o.awa, sp = isTrue ? o.tws : o.aws;
          text(ctx, side(a, sp), x, y + D.dh - 5*D.sideS, D.sideS, P.seg);
          drawNum(ctx, fmtAng(a, sp), x + 18, y, D.dw, D.dh, D.t, D.gap, D.dp, P.seg, P.off);
        } else {
          drawNum(ctx, fmtKn(isTrue ? o.tws : o.aws), x, y, D.dw, D.dh, D.t, D.gap, D.dp, P.seg, P.off);
        }
      });
    } else {
      const S = G.speed, label = followTrue ? 'TWS' : 'AWS', str = fmtKn(spd), w = numW(str, S.dw, S.gap, S.dp);
      textC(ctx, label, ox + G.mode.x + 6, oy + G.mode.y + 2, G.mode.s, P.dim);
      // no ghost cells on the piece: at 7 px a ghost 8 reads as a digit
      drawNum(ctx, str, ox + S.cx - Math.round(w/2) - 4, oy + S.y - 6, S.dw, S.dh, S.t, S.gap, S.dp, P.seg, null);
    }
  }

  // ---- housings -----------------------------------------------------------------
  // The raised card's case: graphite, four screws, a lit top edge.
  function caseBody(g, night){
    rrect(g, 14, 10, 312, 346, 18, '#04070a');
    rrect(g, 16, 12, 308, 342, 16, night ? CASE[0] : CASE[1]);
    rrect(g, 18, 14, 304, 338, 14, night ? CASE[1] : CASE[2]);
    g.fillStyle = night ? CASE[2] : CASE[4]; g.fillRect(32, 14, 276, 1);           // top highlight
    g.fillStyle = CASE[0]; g.fillRect(32, 351, 276, 1);                            // bottom shade
    [[34,28],[306,28],[34,338],[306,338]].forEach(p=>screw(g, p[0], p[1], 4));
  }
  // The band piece's housing: HelmBandPlateRender.Blank's recessed plate, so the piece sits in the band.
  function plate(g, night){
    const w = PW - 2*PLATE.insetX, h = PH - 2*PLATE.insetY;
    rrect(g, PLATE.insetX, PLATE.insetY, w, h, PLATE.radius, night ? PLATE.rimNight : PLATE.rimDay);
    rrect(g, PLATE.insetX+PLATE.rimPx, PLATE.insetY+PLATE.rimPx, w-2*PLATE.rimPx, h-2*PLATE.rimPx, PLATE.radius-1,
          night ? PLATE.faceNight : PLATE.faceDay);
  }

  // ---- entry points -------------------------------------------------------------
  function opts(o){
    o = o || {};
    return { grade: o.grade === 'display' ? 'display' : 'dial', size: o.size === 'piece' ? 'piece' : 'card',
             night: !!o.night, awa: clampAng(o.awa), aws: kn(o.aws),
             hasTrue: o.grade === 'display' && o.hasTrue !== false,
             twa: clampAng(o.twa), tws: kn(o.tws), follow: o.follow === 'true' ? 'true' : 'apparent' };
  }
  /** Draw the unit at its native size with its top-left at (x, y). */
  function paintAt(ctx, x, y, o){
    o = opts(o); const face = o.grade === 'display' ? lcdFace(o.size, o.night) : dialFace(o.size, o.night);
    ctx.imageSmoothingEnabled = false; ctx.drawImage(face, Math.round(x), Math.round(y));
    if (o.grade === 'display') paintLcdLive(ctx, Math.round(x), Math.round(y), o.size, o);
    else paintDialLive(ctx, Math.round(x), Math.round(y), o.size, o);
  }
  /** Fit the native sprite into a box, centred, at the largest WHOLE scale that fits (min 1). */
  function paintInto(ctx, x, y, w, h, o){
    const c = render(o), k = Math.max(1, Math.floor(Math.min(w / c.width, h / c.height)));
    ctx.save(); ctx.imageSmoothingEnabled = false;
    ctx.drawImage(c, Math.round(x + (w - c.width*k)/2), Math.round(y + (h - c.height*k)/2), c.width*k, c.height*k);
    ctx.restore(); return { k };
  }
  function render(o){ o = opts(o); const c = o.size === 'piece' ? cv(PW,PH) : cv(W,H); paintAt(c.getContext('2d'), 0, 0, o); return c; }

  /**
   * Hit and text geometry for the port, in native sprite px (origin top-left, y down). The same
   * numbers the renderer uses; windRig.sidecar.json is a checked copy of layout() for every grade × size.
   */
  function layout(grade, size){
    grade = grade === 'display' ? 'display' : 'dial'; size = size === 'piece' ? 'piece' : 'card';
    const out = { grade, size, w: size==='piece' ? PW : W, h: size==='piece' ? PH : H, boxes: {} };
    if (grade === 'dial'){
      const G = DIAL_GEOM[size];
      out.needle = { pivotX: G.cx, pivotY: G.cy, tipR: G.needle.tip, tailR: G.needle.tail, hubR: G.needle.hub, source: 'awa' };
      out.face = { cx: G.cx, cy: G.cy, r: G.rFace };
      out.boxes.aws = { x: G.cx + G.drum.x, y: G.cy + G.drum.y, w: G.drum.w, h: G.drum.h, format: 'drum2', glyphScale: G.drum.s };
    } else {
      const G = LCD_GEOM[size];
      out.needle = { pivotX: G.cx, pivotY: G.cy, tipR: G.pointer[1], tailR: 0, hubR: G.pointer[0], ringR: G.segR, cellDeg: G.segStep, source: 'follow' };
      out.face = { x: G.glass.x, y: G.glass.y, w: G.glass.w, h: G.glass.h };
      if (size === 'card'){
        const D = G.digit;
        G.rows.forEach(rw=>{
          const isA = rw.kind === 'angle';
          out.boxes[rw.key] = { x: rw.x, y: rw.y, w: isA ? 18 + numW('000', D.dw, D.gap, D.dp) : numW('00.0', D.dw, D.gap, D.dp),
                                h: D.labelH + D.dh, format: isA ? 'side+ang3' : 'kn4', digitH: D.dh };
        });
        const B = G.bft; out.boxes.bft = { x: B.x - numW('12', B.dw, 4, 0) - 4 - textW('F', 2), y: B.y, w: numW('12', B.dw, 4, 0) + 4 + textW('F', 2), h: 9 + B.dh, format: 'F+int', digitH: B.dh };
        out.boxes.mode = { x: G.mode.x - 2, y: G.mode.y - 2, w: textW('TRUE', G.mode.s) + 4, h: 5*G.mode.s + 4, format: 'APP|TRUE' };
        out.hits = { follow: { x: G.glass.x, y: G.glass.y, w: G.glass.w, h: 200 } };   // tap the dial: swap the pointer
      } else {
        const S = G.speed, w = numW('00.0', S.dw, S.gap, S.dp);
        out.boxes.speed = { x: S.cx - Math.round(w/2) - 4, y: S.y - 6, w, h: S.dh, format: 'kn4', digitH: S.dh, source: 'follow' };
      }
    }
    return out;
  }

  root.WindRig = {
    W, H, PW, PH, DEG, CLOSE_HAULED, CALM_KN, KN_MAX, BFT_MS, MS_PER_KN,
    render, paintAt, paintInto, layout,
    beaufort, fmtDrum, fmtKn, fmtAng, side, clampAng, dir,
  };
})(typeof globalThis!=='undefined'?globalThis:window);
