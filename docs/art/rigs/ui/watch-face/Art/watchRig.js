// Watch Face — diegetic time/date rig for Hidden Harbours (the "start" clock).
// A resin digital watch in the F-series idiom: squared case, illuminator top,
// grey bezel, four pushers, LCD. EVERYTHING on the LCD is procedural — a 7-seg
// numeral renderer + a 3x5 bitmap font — so any time / date / season / weekday /
// day-night state draws from parameters. No sprite bakes.
//
// Calendar is canon (repo: Core/Time/Calendar.cs, Environment/GameClock.cs):
//   4 seasons  EarlySpring/HighSummer/TheTurn/HardWinter, 28 days each
//   7-day week Monday..Sunday, one tunable Market Day
//   day/night  night = hour < dawn(6) || hour >= dusk(19), wraps midnight
// Brand wordmark is original (not the reference's) — same layout, no trademark.
(function () {
  const W = 340, H = 356;

  // ---- palettes -------------------------------------------------------------
  const RESIN = ['#050607', '#0c0f11', '#15191c', '#20262a', '#2c343a'];   // black rubber
  const BEZEL = ['#5b636a', '#767f86', '#8b949b', '#a3acb2', '#c2c9cd'];   // grey face ring
  const STEEL = ['#3a434a', '#5c676f', '#828d94', '#a7b2b8', '#cfd7db'];   // pushers/screws
  const INK   = '#0a0c0d', INK_SOFT = '#c9cfc6';
  // LCD — day (positive grey-green) and night (green EL backlight)
  const DAY   = { bg1: '#cbd2c4', bg2: '#aeb6a6', seg: '#1c2119', off: 'rgba(26,32,22,0.07)', dim: '#4a5346' };
  const NIGHT = { bg1: '#43c162', bg2: '#2c9a46', seg: '#0b3415', off: 'rgba(9,42,18,0.12)', dim: '#0f5222', glow: '#8ff0a2' };

  // ---- 3x5 bitmap font ------------------------------------------------------
  const F = {
    'A':['.#.','#.#','###','#.#','#.#'], 'B':['##.','#.#','##.','#.#','##.'],
    'C':['.##','#..','#..','#..','.##'], 'D':['##.','#.#','#.#','#.#','##.'],
    'E':['###','#..','##.','#..','###'], 'F':['###','#..','##.','#..','#..'],
    'G':['.##','#..','#.#','#.#','.##'], 'H':['#.#','#.#','###','#.#','#.#'],
    'I':['###','.#.','.#.','.#.','###'], 'J':['..#','..#','..#','#.#','.#.'],
    'K':['#.#','#.#','##.','#.#','#.#'], 'L':['#..','#..','#..','#..','###'],
    'M':['#.#','###','###','#.#','#.#'], 'N':['#.#','##.','###','.##','#.#'],
    'O':['.#.','#.#','#.#','#.#','.#.'], 'P':['##.','#.#','##.','#..','#..'],
    'Q':['.#.','#.#','#.#','.#.','..#'], 'R':['##.','#.#','##.','#.#','#.#'],
    'S':['.##','#..','.#.','..#','##.'], 'T':['###','.#.','.#.','.#.','.#.'],
    'U':['#.#','#.#','#.#','#.#','.#.'], 'V':['#.#','#.#','#.#','.#.','.#.'],
    'W':['#.#','#.#','###','###','#.#'], 'X':['#.#','#.#','.#.','#.#','#.#'],
    'Y':['#.#','#.#','.#.','.#.','.#.'], 'Z':['###','..#','.#.','#..','###'],
    '0':['###','#.#','#.#','#.#','###'], '1':['.#.','##.','.#.','.#.','###'],
    '2':['##.','..#','.#.','#..','###'], '3':['##.','..#','.#.','..#','##.'],
    '4':['#.#','#.#','###','..#','..#'], '5':['###','#..','##.','..#','##.'],
    '6':['.##','#..','##.','#.#','.#.'], '7':['###','..#','.#.','.#.','.#.'],
    '8':['.#.','#.#','.#.','#.#','.#.'], '9':['.#.','#.#','.##','..#','##.'],
    '-':['...','...','###','...','...'], '/':['..#','..#','.#.','#..','#..'],
    '.':['...','...','...','...','.#.'], ':':['...','.#.','...','.#.','...'], ' ':['...','...','...','...','...'],
  };
  function textW(str, s) { return str.length * (3 * s + s) - s; }   // trailing gap removed
  function text(ctx, str, x, y, s, color) {
    ctx.fillStyle = color; str = String(str).toUpperCase();
    let cx = x;
    for (const ch of str) {
      const g = F[ch] || F[' '];
      for (let r = 0; r < 5; r++) for (let c = 0; c < 3; c++) if (g[r][c] === '#') ctx.fillRect(cx + c * s, y + r * s, s, s);
      cx += 3 * s + s;
    }
    return cx - s;
  }

  // ---- primitives -----------------------------------------------------------
  function rowInset(v, h, r) {
    if (v < r)      { const dy = r - v;           return r - Math.floor(Math.sqrt(Math.max(0, r * r - dy * dy))); }
    if (v >= h - r) { const dy = v - (h - r) + 1; return r - Math.floor(Math.sqrt(Math.max(0, r * r - dy * dy))); }
    return 0;
  }
  function rrect(ctx, x, y, w, h, r, fill) {
    ctx.fillStyle = fill; r = Math.max(0, Math.min(r, Math.floor(Math.min(w, h) / 2)));
    for (let v = 0; v < h; v++) { const i = rowInset(v, h, r); ctx.fillRect(x + i, y + v, w - 2 * i, 1); }
  }
  function rrectGrad(ctx, x, y, w, h, r, c1, c2) {
    const g = ctx.createLinearGradient(0, y, 0, y + h); g.addColorStop(0, c1); g.addColorStop(1, c2);
    ctx.save(); ctx.beginPath();
    // simple clipped fill (AA rounded ok on the case)
    const rr = Math.min(r, w / 2, h / 2);
    ctx.moveTo(x + rr, y); ctx.arcTo(x + w, y, x + w, y + h, rr); ctx.arcTo(x + w, y + h, x, y + h, rr);
    ctx.arcTo(x, y + h, x, y, rr); ctx.arcTo(x, y, x + w, y, rr); ctx.closePath();
    ctx.fillStyle = g; ctx.fill(); ctx.restore();
  }
  function circle(ctx, cx, cy, rad, fill) {
    ctx.fillStyle = fill;
    for (let dy = -rad; dy <= rad; dy++) { const dx = Math.floor(Math.sqrt(Math.max(0, rad * rad - dy * dy))); ctx.fillRect(cx - dx, cy + dy, 2 * dx + 1, 1); }
  }
  function screw(ctx, cx, cy) {
    circle(ctx, cx, cy, 5, STEEL[0]); circle(ctx, cx, cy, 4, STEEL[2]); circle(ctx, cx - 1, cy - 1, 3, STEEL[3]);
    ctx.fillStyle = STEEL[0]; ctx.fillRect(cx - 3, cy - 1, 6, 2);   // slot
  }
  function pusher(ctx, x, y, side) {                 // side: -1 left, +1 right
    rrect(ctx, x, y, 14 * side > 0 ? 14 : 14, 22, 4, RESIN[1]);
    const bx = side < 0 ? x - 8 : x + 14;
    rrect(ctx, side < 0 ? x - 9 : x + 13, y + 5, 10, 12, 3, STEEL[1]);
    rrect(ctx, side < 0 ? x - 9 : x + 13, y + 5, 10, 4, 2, STEEL[3]);
  }

  // ---- 7-segment numerals ---------------------------------------------------
  const SEG = {
    '0':'abcdef','1':'bc','2':'abged','3':'abgcd','4':'fgbc','5':'afgcd',
    '6':'afgedc','7':'abc','8':'abcdefg','9':'abfgcd','-':'g',' ':'',
  };
  function hbar(ctx, cx, cy, len, t) {
    const h = t / 2, l = len / 2;
    ctx.beginPath(); ctx.moveTo(cx - l, cy); ctx.lineTo(cx - l + h, cy - h);
    ctx.lineTo(cx + l - h, cy - h); ctx.lineTo(cx + l, cy); ctx.lineTo(cx + l - h, cy + h);
    ctx.lineTo(cx - l + h, cy + h); ctx.closePath(); ctx.fill();
  }
  function vbar(ctx, cx, cy, len, t) {
    const h = t / 2, l = len / 2;
    ctx.beginPath(); ctx.moveTo(cx, cy - l); ctx.lineTo(cx + h, cy - l + h);
    ctx.lineTo(cx + h, cy + l - h); ctx.lineTo(cx, cy + l); ctx.lineTo(cx - h, cy + l - h);
    ctx.lineTo(cx - h, cy - l + h); ctx.closePath(); ctx.fill();
  }
  // draw one digit in box (x,y,w,h); on-segments onCol, off-ghost offCol (or null)
  function seg7(ctx, x, y, w, h, ch, onCol, offCol) {
    const on = SEG[ch] == null ? '' : SEG[ch], t = Math.max(3, w * 0.19);
    const midY = y + h / 2, hlen = w - t * 0.7, vlen = h / 2 - t * 0.7;
    const put = (k, drawFn) => { ctx.fillStyle = on.indexOf(k) >= 0 ? onCol : offCol; if (ctx.fillStyle) drawFn(); };
    if (offCol || on) {
      put('a', () => hbar(ctx, x + w / 2, y, hlen, t));
      put('g', () => hbar(ctx, x + w / 2, midY, hlen, t));
      put('d', () => hbar(ctx, x + w / 2, y + h, hlen, t));
      put('f', () => vbar(ctx, x, y + h / 4, vlen, t));
      put('b', () => vbar(ctx, x + w, y + h / 4, vlen, t));
      put('e', () => vbar(ctx, x, y + h * 3 / 4, vlen, t));
      put('c', () => vbar(ctx, x + w, y + h * 3 / 4, vlen, t));
    }
  }
  // draw a right-to-left string of small 7-seg digits, return left x
  function seg7Str(ctx, str, rightX, y, dw, dh, gap, onCol, offCol) {
    let x = rightX;
    for (let i = str.length - 1; i >= 0; i--) { x -= dw; seg7(ctx, x, y, dw, dh, str[i], onCol, offCol); x -= gap; }
    return x + gap;
  }

  // ---- day/night + label helpers (mirror the C# canon) ----------------------
  const WD = ['MO', 'TU', 'WE', 'TH', 'FR', 'SA', 'SU'];
  const SEASON_ABBR = ['SPR', 'SUM', 'TRN', 'WIN'];
  const SEASON_FULL = ['Early Spring', 'High Summer', 'The Turn', 'Hard Winter'];
  function isNight(hour, dawn, dusk) { dawn = dawn == null ? 6 : dawn; dusk = dusk == null ? 19 : dusk; return hour < dawn || hour >= dusk; }
  function pad2(n) { n = Math.floor(n); return (n < 10 ? '0' : '') + n; }

  // ---- LCD content ----------------------------------------------------------
  function lcd(ctx, X, Y, WW, HH, o, P) {
    // hour formatting
    let hh, ampm = '';
    if (o.use24) { hh = pad2(o.h); }
    else { let h12 = o.h % 12; if (h12 === 0) h12 = 12; hh = (h12 < 10 ? ' ' : '') + h12; ampm = o.h < 12 ? 'AM' : 'PM'; }
    const mm = pad2(o.m), ss = pad2(o.s), date = pad2(o.date);

    // --- top strip: indicators (left) + weekday & date (right) ---
    const topY = Y + 8;
    // little bell / alarm / chrono glyphs (kept subtle)
    text(ctx, o.market ? 'MKT' : 'AL', X + 6, topY + 1, 2, P.dim);
    // signal/oscillator ticks
    for (let i = 0; i < 3; i++) { ctx.fillStyle = P.dim; ctx.fillRect(X + 30 + i * 4, topY + 2 + (2 - i), 2, 3 + i * 2); }
    // weekday (letters) + date (small 7-seg), right-aligned
    const dRight = X + WW - 8;
    const dLeft = seg7Str(ctx, date, dRight, topY - 1, 13, 15, 4, P.seg, P.off);
    text(ctx, o.dow, dLeft - textW(o.dow, 3) - 8, topY + 1, 3, P.seg);

    // --- AM/PM (left of main) ---
    const mainY = Y + 40, mainH = 60, dw = 32, dh = mainH;
    const gapD = 10, colonPad = 13, colonW = 8;   // wider spacing for legibility
    if (!o.use24 && ampm) text(ctx, ampm, X + 6, mainY + 6, 3, P.seg);

    // --- main HH:MM (centred, digits well separated) ---
    const groupW = dw * 4 + gapD * 2 + colonPad * 2 + colonW;
    let gx = X + Math.round((WW - groupW) / 2);
    seg7(ctx, gx, mainY, dw, dh, hh[0], hh[0] === ' ' ? null : P.seg, hh[0] === ' ' ? null : P.off); gx += dw + gapD;
    seg7(ctx, gx, mainY, dw, dh, hh[1], P.seg, P.off); gx += dw + colonPad;
    // colon
    const cX = gx + Math.round((colonW - 6) / 2);
    ctx.fillStyle = P.seg; ctx.fillRect(cX, mainY + dh * 0.30, 6, 6); ctx.fillRect(cX, mainY + dh * 0.62, 6, 6);
    gx += colonW + colonPad;
    // minutes
    seg7(ctx, gx, mainY, dw, dh, mm[0], P.seg, P.off); gx += dw + gapD;
    seg7(ctx, gx, mainY, dw, dh, mm[1], P.seg, P.off);

    // --- bottom: season + year (left), seconds (right) ---
    const botY = Y + HH - 30;
    text(ctx, o.season, X + 8, botY + 4, 3, P.seg);
    text(ctx, 'Y' + o.year, X + 8 + textW(o.season, 3) + 8, botY + 4, 3, P.dim);
    seg7Str(ctx, ss, X + WW - 8, botY, 17, 22, 5, P.seg, P.off);
  }

  // ---- full watch, upright ---------------------------------------------------
  function paint(ctx, o) {
    o = o || {};
    o = {
      h: o.h == null ? 6 : o.h, m: o.m || 0, s: o.s || 0, use24: !!o.use24,
      dow: o.dow || 'MO', date: o.date == null ? 1 : o.date,
      season: o.season || 'SPR', year: o.year == null ? 1 : o.year,
      market: !!o.market, light: !!o.light,
      night: o.night == null ? isNight((o.h == null ? 6 : o.h) + (o.m || 0) / 60) : !!o.night,
    };
    const backlit = o.night || o.light;      // green glow on at night or while LIGHT held
    const P = backlit ? NIGHT : DAY;
    ctx.clearRect(0, 0, W, H);

    // strap lugs (cropped top & bottom)
    rrect(ctx, 118, 0, 104, 16, 6, RESIN[0]); rrect(ctx, 118, H - 16, 104, 16, 6, RESIN[0]);
    // case body
    rrectGrad(ctx, 10, 8, 320, 340, 50, RESIN[3], RESIN[1]);
    rrect(ctx, 16, 14, 308, 328, 44, RESIN[1]);
    // top illuminator label
    { const s = 2, str = 'ILLUMINATOR', tw = textW(str, s); text(ctx, str, Math.round((W - tw) / 2), 36, s, '#b7bec2'); }
    // side pushers + guards
    pusher(ctx, 16, 118, -1); pusher(ctx, 16, 214, -1); pusher(ctx, 310, 118, 1); pusher(ctx, 310, 214, 1);

    // grey bezel face
    rrectGrad(ctx, 40, 60, 260, 236, 24, BEZEL[3], BEZEL[1]);
    rrect(ctx, 44, 64, 252, 228, 20, BEZEL[2]);
    screw(ctx, 58, 80); screw(ctx, 282, 80); screw(ctx, 58, 276); screw(ctx, 282, 276);
    // brand wordmark (original) + model no.
    { const s = 4, str = 'HARBOUR', tw = textW(str, s); text(ctx, str, Math.round((W - tw) / 2), 74, s, INK); }
    text(ctx, 'H-108', 232, 78, 2, BEZEL[0]);
    // pusher function labels
    text(ctx, 'START/STOP', 54, 100, 2, INK);
    { const str = 'ALARM CHRONO'; text(ctx, str, 288 - textW(str, 2), 100, 2, INK); }
    text(ctx, 'MODE', 54, 268, 2, INK);
    { const str = 'LIGHT'; text(ctx, str, 288 - textW(str, 2), 268, 2, INK); }
    text(ctx, 'WATER RESIST', Math.round((W - textW('WATER RESIST', 2)) / 2), 300, 2, '#aeb6ba');

    // LCD window (recessed)
    const LX = 58, LY = 116, LW = 224, LH = 148;
    if (backlit) {   // green bloom over the bezel
      const g = ctx.createRadialGradient(LX + LW / 2, LY + LH / 2, 20, LX + LW / 2, LY + LH / 2, LW * 0.8);
      g.addColorStop(0, 'rgba(120,240,150,0.45)'); g.addColorStop(1, 'rgba(120,240,150,0)');
      ctx.fillStyle = g; ctx.fillRect(28, 88, 284, 210);
    }
    rrect(ctx, LX - 4, LY - 4, LW + 8, LH + 8, 12, RESIN[0]);   // dark surround
    rrectGrad(ctx, LX, LY, LW, LH, 8, P.bg1, P.bg2);            // glass
    ctx.fillStyle = 'rgba(255,255,255,0.10)'; ctx.fillRect(LX + 4, LY + 3, LW - 8, 2);   // top glare
    lcd(ctx, LX, LY, LW, LH, o, P);
  }

  function render(o) { const c = document.createElement('canvas'); c.width = W; c.height = H; paint(c.getContext('2d'), o); return c; }

  window.WatchRig = {
    W, H, paint, render, isNight, pad2,
    WEEKDAYS: WD, SEASON_ABBR, SEASON_FULL,
    hit: { light: { x: 226, y: 260, w: 96, h: 26 }, mode: { x: 44, y: 260, w: 96, h: 26 } },
  };
})();
