// Outboard Tiller — diegetic control rig for the motorized dory.
// One basic tiller handle: reads straight UP at centre, rotates about the
// clamp pivot with the outboard's own motion. Grip carries a flush start/stop
// button, an integrated throttle band, a red FORWARD/REVERSE shift rocker,
// telltale lamps and a clipped kill-cord. Drawn once in local space (pointing
// up); the stage rotates it.
(function () {
  const W = 120, H = 244, PVX = 60, PVY = 214;   // cell + clamp pivot (near base)

  // ---- palettes (cool graphite + rubber, upper-left key) --------------------
  const GRAPH  = ['#12171b', '#1b2228', '#28323a', '#3a4750', '#4f5e68', '#6b7c86'];
  const RUBBER = ['#0b0e11', '#11161a', '#1a2126', '#252e35', '#333f47'];
  const STEEL  = ['#232a30', '#39434b', '#556069', '#7a8892', '#9db0b8'];
  const GREEN  = ['#1c6a3b', '#2f9e57', '#66d585'];
  const RED    = ['#7c2a20', '#b83b2e', '#e0554a'];
  const REDOFF = '#241012';                     // unlit red half of the shift rocker
  const AMBER  = '#e6b53f', TEAL = '#7fd6c9';
  const LAMPOFF = '#16202a', SEGOFF = '#141c22';
  const KILL = ['#c23a2d', '#ef6a5b'], CORD = ['#8f2b22', '#c23a2d'];

  // 3x5 pixel glyphs for the shift rocker
  const GL = {
    F: ['###', '#..', '##.', '#..', '#..'],
    R: ['##.', '#.#', '##.', '#.#', '#.#'],
  };

  // ---- crisp primitives -----------------------------------------------------
  function rowInset(v, h, r) {
    if (v < r)          { const dy = r - v;              return r - Math.floor(Math.sqrt(Math.max(0, r * r - dy * dy))); }
    if (v >= h - r)     { const dy = v - (h - r) + 1;    return r - Math.floor(Math.sqrt(Math.max(0, r * r - dy * dy))); }
    return 0;
  }
  function rrect(ctx, x, y, w, h, r, fill) {
    ctx.fillStyle = fill; r = Math.max(0, Math.min(r, Math.floor(Math.min(w, h) / 2)));
    for (let v = 0; v < h; v++) { const i = rowInset(v, h, r); ctx.fillRect(x + i, y + v, w - 2 * i, 1); }
  }
  function circle(ctx, cx, cy, rad, fill) {
    ctx.fillStyle = fill;
    for (let dy = -rad; dy <= rad; dy++) { const dx = Math.floor(Math.sqrt(Math.max(0, rad * rad - dy * dy))); ctx.fillRect(cx - dx, cy + dy, 2 * dx + 1, 1); }
  }
  function triUp(ctx, cx, yTop, s, fill) { ctx.fillStyle = fill; for (let i = 0; i < s; i++) ctx.fillRect(cx - i, yTop + i, 2 * i + 1, 1); }
  function glyph(ctx, map, x, y, s, color) {
    ctx.fillStyle = color;
    for (let r = 0; r < map.length; r++) for (let c = 0; c < map[r].length; c++) if (map[r][c] === '#') ctx.fillRect(x + c * s, y + r * s, s, s);
  }
  function screw(ctx, cx, cy, r) {
    circle(ctx, cx, cy, r, GRAPH[0]); circle(ctx, cx, cy, r - 1, GRAPH[3]); circle(ctx, cx - 1, cy - 1, Math.max(1, r - 2), GRAPH[4]);
    ctx.fillStyle = GRAPH[0]; ctx.fillRect(cx - r + 1, cy, 2 * r - 1, 1);   // slot
  }
  // shaded cylinder: vertical highlight streak + optional grip ribs
  function cylinder(ctx, x, y, w, h, r, ramp, o) {
    o = o || {}; const n = ramp.length, hlU = w * (o.hl == null ? 0.36 : o.hl), span = w * 0.62;
    for (let v = 0; v < h; v++) {
      const ins = rowInset(v, h, r);
      const rib = o.rib && (y + v) >= (o.ribFrom == null ? -1 : o.ribFrom) && (y + v) <= (o.ribTo == null ? 1e9 : o.ribTo) && (v % (o.ribStep || 6) === 0);
      for (let u = ins; u < w - ins; u++) {
        let t = 1 - Math.min(1, Math.abs(u - hlU) / span);
        let idx = Math.round(t * (n - 1));
        if (v < 2) idx = Math.min(n - 1, idx + 1);
        if (rib) idx = Math.max(0, idx - 2);
        ctx.fillStyle = ramp[idx]; ctx.fillRect(x + u, y + v, 1, 1);
      }
    }
  }
  // coiled cord: overlapping loops walked from a→b, so it reads connected
  function cord(ctx, ax, ay, bx, by, loops, rad) {
    for (let i = 0; i <= loops; i++) {
      const t = i / loops, cx = Math.round(ax + (bx - ax) * t), cy = Math.round(ay + (by - ay) * t);
      circle(ctx, cx, cy, rad, CORD[0]);
      ctx.fillStyle = CORD[1]; ctx.fillRect(cx - rad + 1, cy - 1, 1, 1);   // per-loop sheen
    }
  }

  // ---- feature parts --------------------------------------------------------
  // throttle: segmented band recessed into a moulded rubber channel (subtle)
  function throttleBand(ctx, x, y, w, h, throttle) {
    rrect(ctx, x - 2, y - 3, w + 4, h + 6, 4, RUBBER[0]);    // rubber surround (part of grip)
    rrect(ctx, x - 2, y - 3, w + 4, 2, 2, RUBBER[3]);        // top lip catch-light
    rrect(ctx, x - 1, y - 1, w + 2, h + 2, 2, '#05080a');    // inner shadow
    const N = 10, gap = 1, segH = Math.floor((h - (N - 1) * gap) / N), lit = Math.round(throttle * N);
    for (let i = 0; i < N; i++) {
      const segY = y + h - segH - i * (segH + gap), on = i < lit;
      let col = SEGOFF;
      if (on) col = i < 6 ? GREEN[1] : i < 9 ? '#c99a34' : RED[1];   // muted, integrated
      rrect(ctx, x, segY, w, segH, 1, col);
      if (on) { ctx.fillStyle = 'rgba(255,255,255,0.20)'; ctx.fillRect(x + 1, segY, w - 2, 1); }
    }
    ctx.fillStyle = 'rgba(120,150,160,0.10)'; ctx.fillRect(x, y, Math.max(1, Math.floor(w / 3)), h);   // faint glass reflection
  }
  // start/stop: low dome flush-set into a moulded dish in the rubber
  function button(ctx, cx, cy, r, running, press) {
    circle(ctx, cx, cy, r + 4, RUBBER[0]);                  // recess shadow ring
    circle(ctx, cx, cy, r + 3, RUBBER[2]);                  // moulded bezel
    circle(ctx, cx - 1, cy - 2, r + 3, RUBBER[4]);          // bezel upper sheen
    circle(ctx, cx, cy, r + 1, '#05080a');                  // dish shadow
    const oy = press ? 1 : 0, C = running ? RED : GREEN;
    circle(ctx, cx, cy + oy, r, C[0]);
    circle(ctx, cx, cy + oy, r - 1, C[1]);
    circle(ctx, cx - 2, cy - 2 + oy, Math.max(2, r - 3), C[2]);   // upper-left sheen
    if (running) {                                          // stop square
      const s = Math.max(4, Math.round(r * 0.72)), s2 = Math.floor(s / 2);
      ctx.fillStyle = '#f2e7e4'; ctx.fillRect(cx - s2, cy - s2 + oy, s, s);
    } else {                                                // play triangle
      const s = Math.max(5, Math.round(r * 0.95)), x0 = cx - Math.floor(s * 0.32);
      ctx.fillStyle = '#eafaf0';
      for (let col = 0; col < s; col++) { const half = Math.round(((s - col) / 2) * 0.9); ctx.fillRect(x0 + col, cy - half + oy, 1, 2 * half + 1); }
    }
  }
  // red FORWARD/REVERSE shift rocker — active gear half lights red, letter brightens
  function shiftRocker(ctx, x, y, w, h, forward) {
    rrect(ctx, x - 3, y - 3, w + 6, h + 6, 5, RUBBER[0]);   // recess in rubber
    rrect(ctx, x - 3, y - 3, w + 6, 2, 2, RUBBER[3]);       // top catch-light
    rrect(ctx, x - 1, y - 1, w + 2, h + 2, 4, '#06090b');   // slot shadow
    const half = Math.floor((h - 3) / 2), topY = y + 1, botY = y + h - half - 1, midX = x + Math.floor(w / 2);
    // top half = FORWARD
    rrect(ctx, x + 1, topY, w - 2, half, 3, forward ? RED[0] : REDOFF);
    if (forward) { rrect(ctx, x + 2, topY + 1, w - 4, Math.floor(half * 0.42), 2, RED[1]); rrect(ctx, x + 3, topY + 1, w - 6, 1, 1, RED[2]); }
    // bottom half = REVERSE
    rrect(ctx, x + 1, botY, w - 2, half, 3, !forward ? RED[0] : REDOFF);
    if (!forward) { rrect(ctx, x + 2, botY, w - 4, Math.floor(half * 0.42), 2, RED[1]); rrect(ctx, x + 3, botY + 1, w - 6, 1, 1, RED[2]); }
    // centre pivot ridge
    ctx.fillStyle = '#05080a'; ctx.fillRect(x, y + Math.floor(h / 2) - 1, w, 2);
    ctx.fillStyle = STEEL[2]; ctx.fillRect(x, y + Math.floor(h / 2) - 1, w, 1);
    // engraved letters
    glyph(ctx, GL.F, midX - 3, topY + Math.floor(half / 2) - 5, 2, forward ? '#f4dede' : '#5a3335');
    glyph(ctx, GL.R, midX - 3, botY + Math.floor(half / 2) - 5, 2, !forward ? '#f4dede' : '#5a3335');
  }

  // ---- full handle, local space, pointing up --------------------------------
  function paint(ctx, o) {
    o = o || {};
    const throttle = Math.max(0, Math.min(1, o.throttle == null ? 0 : o.throttle));
    const running = !!o.running, press = !!o.press, warn = !!o.warn, blink = o.blink ? 1 : 0;
    const forward = o.gear !== 'R';
    ctx.clearRect(0, 0, W, H);

    // cowl (powerhead the tiller clamps to)
    rrect(ctx, 30, 200, 60, 32, 12, GRAPH[1]);
    rrect(ctx, 33, 203, 54, 8, 4, GRAPH[3]);                // moulded top vents
    for (let i = 0; i < 4; i++) { ctx.fillStyle = GRAPH[0]; ctx.fillRect(38 + i * 12, 204, 1, 6); }
    rrect(ctx, 36, 227, 48, 4, 2, GRAPH[0]);                // bottom lip
    screw(ctx, 40, 226, 2); screw(ctx, 80, 226, 2);
    circle(ctx, 72, 214, 3, TEAL); circle(ctx, 72, 214, 2, '#0e2b2a');   // maker nub

    // clamp collar + swivel bracket at the steering axis
    rrect(ctx, 45, 189, 30, 16, 4, GRAPH[4]);
    rrect(ctx, 43, 191, 4, 12, 1, GRAPH[3]); rrect(ctx, 73, 191, 4, 12, 1, GRAPH[3]);   // side plates
    rrect(ctx, 48, 190, 24, 3, 1, GRAPH[5]);
    circle(ctx, 60, 197, 3, GRAPH[5]); circle(ctx, 60, 197, 2, GRAPH[2]);               // pivot bolt

    // arm shaft with a ribbed rubber boot near the base
    cylinder(ctx, 46, 162, 28, 34, 9, GRAPH, { hl: 0.34 });
    for (let yy = 176; yy <= 192; yy += 4) { ctx.fillStyle = GRAPH[0]; ctx.fillRect(48, yy, 24, 1); ctx.fillStyle = GRAPH[4]; ctx.fillRect(48, yy - 1, 24, 1); }

    // kill-cord: clip plugged onto a red kill button, cord coiled down to the collar
    cord(ctx, 42, 182, 36, 200, 7, 3);
    circle(ctx, 36, 201, 3, CORD[0]);                        // terminal loop at the collar
    rrect(ctx, 34, 170, 17, 15, 3, GRAPH[0]);                // kill-switch housing
    rrect(ctx, 35, 171, 15, 3, 1, GRAPH[3]);
    circle(ctx, 42, 179, 4, KILL[0]); circle(ctx, 41, 178, 2, KILL[1]);   // red kill button
    rrect(ctx, 37, 175, 11, 6, 2, KILL[0]);                  // lanyard clip clamped over it
    rrect(ctx, 38, 176, 9, 2, 1, KILL[1]);
    ctx.fillStyle = '#3a0f0b'; ctx.fillRect(41, 177, 3, 2);  // clip eye the cord threads

    // telltale pod at the base of the grip
    rrect(ctx, 39, 150, 42, 15, 4, GRAPH[1]);
    rrect(ctx, 40, 151, 40, 2, 1, GRAPH[3]);
    circle(ctx, 50, 158, 3, running ? GREEN[2] : LAMPOFF);
    circle(ctx, 61, 158, 3, running ? TEAL : LAMPOFF);
    triUp(ctx, 72, 155, 6, (warn && blink) ? AMBER : LAMPOFF);

    // grip body (throttle twist body), ribbed only on the lower shaft-hand section
    cylinder(ctx, 36, 32, 48, 122, 16, RUBBER, { hl: 0.36, rib: true, ribStep: 7, ribFrom: 124, ribTo: 152 });
    ctx.fillStyle = 'rgba(0,0,0,0.22)'; ctx.fillRect(59, 40, 1, 92);   // moulding seam

    // trim rocker (subtle, left flank)
    rrect(ctx, 39, 66, 10, 26, 3, RUBBER[0]);
    rrect(ctx, 40, 67, 8, 11, 2, STEEL[2]); rrect(ctx, 40, 80, 8, 11, 2, STEEL[0]);
    ctx.fillStyle = STEEL[4]; ctx.fillRect(43, 70, 2, 2); ctx.fillStyle = STEEL[3]; ctx.fillRect(43, 86, 2, 1);

    throttleBand(ctx, 71, 60, 10, 90, throttle);   // right shoulder
    shiftRocker(ctx, 47, 84, 22, 34, forward);      // centre column, below the button
    button(ctx, 58, 50, 10, running, press);        // thumb rest, top of grip

    // grip end cap
    rrect(ctx, 40, 18, 40, 16, 7, RUBBER[1]);
    rrect(ctx, 44, 19, 32, 5, 3, RUBBER[4]);
  }

  function render(o) {
    const c = document.createElement('canvas'); c.width = W; c.height = H;
    paint(c.getContext('2d'), o); return c;
  }

  window.TillerRig = {
    W, H, pivot: { x: PVX, y: PVY }, maxSteer: 45,
    // local-space hit boxes for the stage's pointer picking
    hit: { button: { x: 58, y: 50, r: 16 }, shift: { x: 47, y: 84, w: 22, h: 34 } },
    paint, render, angle: (f) => f * 45,
  };
})();
