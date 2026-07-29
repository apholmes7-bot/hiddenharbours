// Console Helm — diegetic control rig for the centre-console skiff (companion to
// tillerRig.js, same cool-graphite / rubber idiom). Head-on helm face: a big
// centred marine wheel that turns, an RPM tach and a FUEL gauge flanking it, a
// switch panel on the left (DECK / SPOT / motor START-STOP) and a side-mount
// binnacle on the right carrying ONE single-lever control — push it forward
// (up) for AHEAD, centre it for NEUTRAL, pull it aft (down) for ASTERN. The lever
// SWINGS on an arc about a base hub (baked upright once, rotated crisp at draw
// time) so it genuinely arcs through 3D rather than just shrinking. Throttle
// magnitude and gear are both read off that one lever.
(function (root) {
  const W = 600, TOPPAD = 40, H = 470 + TOPPAD;   // TOPPAD = headroom above the dash so a tall instrument can protrude
  const DEG = Math.PI / 180;

  // ---- palettes (shared with the tiller + the fleet KTC scheme) -------------
  const GRAPH  = ['#12171b','#1b2228','#28323a','#3a4750','#4f5e68','#6b7c86'];  // casings / panels
  const STEEL  = ['#232a30','#39434b','#556069','#7a8892','#9db0b8'];            // spokes / screws
  const RUBBER = ['#0b0e11','#11161a','#1a2126','#252e35','#333f47'];            // wheel rim / grips
  const PAINT  = ['#828f90','#9aa8a8','#b7c2bf','#d3dbd4','#e7ece5','#f5f7f1'];  // white console (fleet topsides)
  const TRIM   = ['#0d3f3c','#14554e','#1c7367','#2ba39a','#49b8aa'];            // teal cove band
  const GOLD   = '#e0b13a';
  const TEAL   = '#7fd6c9';
  const FACE   = '#080c0f';                       // gauge glass
  const TICK   = '#c6d0ce', TICKDIM = '#586a6d';
  const NEEDLE = '#e8863c', NEEDLE_HL = '#f6b268', NEEDLE_DK = '#7d4a28';
  const RED    = ['#7c2a20','#b83b2e','#e0554a'];
  const GREEN  = ['#1c6a3b','#2f9e57','#66d585'];
  const AMBER  = '#e6b53f';
  const LAMPOFF = '#16202a';
  const SPOT_ON = '#eef3ef', DECK_ON = TEAL;

  // ---- 3x5 bitmap font (subset of the watch face) ---------------------------
  const F = {
    'A':['.#.','#.#','###','#.#','#.#'],'B':['##.','#.#','##.','#.#','##.'],
    'C':['.##','#..','#..','#..','.##'],'D':['##.','#.#','#.#','#.#','##.'],
    'E':['###','#..','##.','#..','###'],'F':['###','#..','##.','#..','#..'],
    'G':['.##','#..','#.#','#.#','.##'],'H':['#.#','#.#','###','#.#','#.#'],
    'I':['###','.#.','.#.','.#.','###'],'K':['#.#','#.#','##.','#.#','#.#'],
    'L':['#..','#..','#..','#..','###'],'M':['#.#','###','###','#.#','#.#'],
    'N':['#.#','##.','###','.##','#.#'],'O':['.#.','#.#','#.#','#.#','.#.'],
    'P':['##.','#.#','##.','#..','#..'],'R':['##.','#.#','##.','#.#','#.#'],
    'S':['.##','#..','.#.','..#','##.'],'T':['###','.#.','.#.','.#.','.#.'],
    'U':['#.#','#.#','#.#','#.#','.#.'],'V':['#.#','#.#','#.#','.#.','.#.'],
    'W':['#.#','#.#','###','###','#.#'],'Y':['#.#','#.#','.#.','.#.','.#.'],
    '0':['###','#.#','#.#','#.#','###'],'1':['.#.','##.','.#.','.#.','###'],
    '2':['##.','..#','.#.','#..','###'],'3':['##.','..#','.#.','..#','##.'],
    '4':['#.#','#.#','###','..#','..#'],'5':['###','#..','##.','..#','##.'],
    '6':['.##','#..','##.','#.#','.#.'],'7':['###','..#','.#.','.#.','.#.'],
    '8':['.#.','#.#','.#.','#.#','.#.'],'9':['.#.','#.#','.##','..#','##.'],
    '/':['..#','..#','.#.','#..','#..'],'-':['...','...','###','...','...'],
    '.':['...','...','...','...','.#.'],' ':['...','...','...','...','...'],
  };
  function textW(str, s){ return String(str).length * (3*s + s) - s; }
  function text(ctx, str, x, y, s, col){
    ctx.fillStyle = col; str = String(str).toUpperCase();
    let cx = x;
    for (const ch of str){ const g = F[ch] || F[' '];
      for (let r=0;r<5;r++) for (let c=0;c<3;c++) if (g[r][c]==='#') ctx.fillRect(cx+c*s, y+r*s, s, s);
      cx += 3*s + s; }
    return cx - s;
  }
  function textC(ctx, str, cx, y, s, col){ text(ctx, str, Math.round(cx - textW(str,s)/2), y, s, col); }

  // ---- crisp primitives -----------------------------------------------------
  const cv = (w,h)=>{ const c=document.createElement('canvas'); c.width=Math.round(w); c.height=Math.round(h); return c; };
  function rowInset(v, h, r){
    if (v < r)      { const dy=r-v;          return r - Math.floor(Math.sqrt(Math.max(0,r*r-dy*dy))); }
    if (v >= h - r) { const dy=v-(h-r)+1;    return r - Math.floor(Math.sqrt(Math.max(0,r*r-dy*dy))); }
    return 0;
  }
  function rrect(ctx, x, y, w, h, r, fill){
    ctx.fillStyle = fill; r = Math.max(0, Math.min(r, Math.floor(Math.min(w,h)/2)));
    for (let v=0; v<h; v++){ const i=rowInset(v,h,r); ctx.fillRect(x+i, y+v, w-2*i, 1); }
  }
  function circle(ctx, cx, cy, rad, fill){
    ctx.fillStyle = fill;
    for (let dy=-rad; dy<=rad; dy++){ const dx=Math.floor(Math.sqrt(Math.max(0,rad*rad-dy*dy))); ctx.fillRect(cx-dx, cy+dy, 2*dx+1, 1); }
  }
  function ring(ctx, cx, cy, rO, rI, fill){
    ctx.fillStyle = fill;
    for (let dy=-rO; dy<=rO; dy++){
      const xo = Math.floor(Math.sqrt(Math.max(0, rO*rO - dy*dy)));
      if (Math.abs(dy) < rI){ const xi = Math.floor(Math.sqrt(Math.max(0, rI*rI - dy*dy)));
        ctx.fillRect(cx-xo, cy+dy, xo-xi, 1); ctx.fillRect(cx+xi, cy+dy, xo-xi, 1);
      } else ctx.fillRect(cx-xo, cy+dy, 2*xo+1, 1);
    }
  }
  function thickLine(ctx, x0, y0, x1, y1, w, fill){
    ctx.fillStyle = fill; const dx=x1-x0, dy=y1-y0, L=Math.max(1,Math.hypot(dx,dy)), n=Math.ceil(L);
    const px=-dy/L, py=dx/L, hw=(w-1)/2;
    for (let i=0;i<=n;i++){ const t=i/n, X=x0+dx*t, Y=y0+dy*t;
      for (let j=-hw;j<=hw;j++) ctx.fillRect(Math.round(X+px*j), Math.round(Y+py*j), 1, 1); }
  }
  function screw(ctx, cx, cy, r, ramp){
    ramp = ramp || STEEL;
    circle(ctx, cx, cy, r, ramp[0]); circle(ctx, cx, cy, r-1, ramp[2]); circle(ctx, cx-1, cy-1, Math.max(1,r-2), ramp[3]);
    ctx.fillStyle = ramp[0]; ctx.fillRect(cx-r+1, cy, 2*r-1, 1);
  }
  const dir = (a)=>({ x:Math.sin(a), y:-Math.cos(a) });   // angle from 12 o'clock, +=clockwise

  // ---- angle maps (game value -> drawn angle, degrees) ----------------------
  const ANG = {
    rpm:  (v)=> -135 + 270*clamp01(v),
    wheel:(s)=> Math.max(-1,Math.min(1,s)) * 150,
  };
  const FSWEEP = 200;                              // fuel dial arc (deg), E .. F across the top
  const fuelPhi = (v)=> -FSWEEP/2 + FSWEEP*clamp01(v);
  function clamp01(v){ return Math.max(0, Math.min(1, v==null?0:v)); }

  // ---- geometry (rig-local == canvas px) ------------------------------------
  const CONSOLE = { x:14, y:44, w:572, h:396, r:20 };
  const WHEEL   = { cx:300, cy:256, r:112 };                        // bigger wheel
  const RPM     = { cx:104, cy:148, r:56 };
  const FUEL    = { cx:496, cy:148, r:56 };
  const SWPANEL = { x:30, y:288, w:134, h:148, r:12 };
  const SW = {
    start: { cx:97,  cy:328, r:18, off:-20, run:32 },
    deck:  { x:58,  y:362, w:22, h:42, cx:69,  lampY:416 },
    spot:  { x:116, y:362, w:22, h:42, cx:127, lampY:416 },
  };
  const BINN    = { x:430, y:284, w:154, h:156, r:16 };
  // single throttle/shift lever — a separate 3D rig pivoting about this hub
  const DRIVE   = { px:505, pivotY:416, hitR:46 };
  const SPOTCAN = { x:330, y:22, w:46, h:16 };
  // compass (its own rig, CompassRig): a black dome binnacle on the crown centre.
  // Dome-only on the skiffs — when fitted it takes the sounder's dead-ahead spot.
  const COMPASS = { domeBox:{ x:308, y:-42, w:124, h:176 } };

  // ---- gauges ---------------------------------------------------------------
  function gaugeBezel(ctx, g){
    circle(ctx, g.cx, g.cy, g.r+6, RUBBER[0]);
    ring(ctx, g.cx, g.cy, g.r+5, g.r+1, GRAPH[3]);
    ctx.save(); ctx.beginPath(); ctx.rect(g.cx-g.r-6, g.cy-g.r-6, (g.r+6)*2, g.r+3); ctx.clip();
    ring(ctx, g.cx, g.cy, g.r+5, g.r+1, GRAPH[5]); ctx.restore();
    circle(ctx, g.cx, g.cy, g.r, FACE);
    ring(ctx, g.cx, g.cy, g.r, g.r-2, '#101a1d');
    ctx.fillStyle = 'rgba(150,180,185,0.05)'; ctx.fillRect(g.cx-g.r+3, g.cy-g.r+5, g.r, 2);
  }
  function radialTick(ctx, g, deg, rOut, rIn, w, col){
    const d = dir(deg*DEG);
    thickLine(ctx, g.cx+d.x*rIn, g.cy+d.y*rIn, g.cx+d.x*rOut, g.cy+d.y*rOut, w, col);
  }
  // shared needle from an arbitrary pivot toward an arbitrary tip
  function drawNeedle(ctx, g, px, py, tx, ty){
    const dx=tx-px, dy=ty-py, L=Math.hypot(dx,dy)||1, ux=dx/L, uy=dy/L, tail=Math.min(15, L*0.3);
    thickLine(ctx, px, py, tx, ty, 3, NEEDLE);
    thickLine(ctx, px, py, px+ux*(L-2), py+uy*(L-2), 1, NEEDLE_HL);
    thickLine(ctx, px, py, px-ux*tail, py-uy*tail, 4, NEEDLE_DK);
    const hr=Math.round(g.r*0.14)+1;
    circle(ctx, px, py, hr, STEEL[1]); circle(ctx, px, py, hr-1, STEEL[3]); circle(ctx, px-1, py-1, Math.max(1,hr-3), STEEL[4]);
  }
  function gaugeRPM(ctx, g, rpm){
    gaugeBezel(ctx, g);
    for (let i=0;i<=12;i++){ const v=i/12, deg=-135+270*v, major=(i%2===0);
      radialTick(ctx, g, deg, g.r-3, major?g.r-12:g.r-8, major?2:1, major?TICK:TICKDIM); }
    for (let i=0;i<=10;i++){ const v=0.84+0.16*i/10, deg=-135+270*v; radialTick(ctx, g, deg, g.r-3, g.r-7, 2, RED[1]); }
    for (let n=0;n<=6;n++){ const v=n/6, d=dir((-135+270*v)*DEG), rr=g.r-22;
      textC(ctx, String(n), g.cx+d.x*rr, g.cy+d.y*rr-2, 1, n>=5?RED[2]:TICK); }
    textC(ctx, 'X100', g.cx, g.cy-4, 1, TICKDIM);
    textC(ctx, 'RPM', g.cx, g.cy+g.r-18, 2, TEAL);                 // label lowered toward the bottom
    const rd=dir(ANG.rpm(rpm)*DEG), len=g.r-13;
    drawNeedle(ctx, g, g.cx, g.cy, g.cx+rd.x*len, g.cy+rd.y*len);
  }
  function gaugeFUEL(ctx, g, fuel, low, blink){
    gaugeBezel(ctx, g);
    const drop=Math.round(g.r*0.42), py=g.cy+drop, rimR=g.r-6;    // pivot lowered; wide arc uses more dial
    for (let i=0;i<=10;i++){ const v=i/10, deg=fuelPhi(v), major=(i%1e9===0)||(i%2===0);
      radialTick(ctx, g, deg, g.r-3, major?g.r-12:g.r-8, major?2:1, major?TICK:TICKDIM); }
    for (let i=0;i<=3;i++){ const v=0.10*i/3, deg=fuelPhi(v); radialTick(ctx, g, deg, g.r-3, g.r-7, 2, RED[1]); }
    { const d=dir(fuelPhi(0)*DEG),   rr=g.r-15; textC(ctx, 'E', g.cx+d.x*rr, g.cy+d.y*rr-3, 2, RED[2]); }
    { const d=dir(fuelPhi(1)*DEG),   rr=g.r-15; textC(ctx, 'F', g.cx+d.x*rr, g.cy+d.y*rr-3, 2, TICK); }
    { const d=dir(fuelPhi(0.5)*DEG), rr=g.r-16; ctx.fillStyle=TICK; ctx.fillRect(g.cx+d.x*rr-1, g.cy+d.y*rr-2, 2, 4); }
    textC(ctx, 'FUEL', g.cx, g.cy+g.r-16, 2, TEAL);               // label lowered toward the bottom
    circle(ctx, g.cx, py-14, 3, (low && blink) ? AMBER : LAMPOFF);
    const d=dir(fuelPhi(fuel)*DEG);
    drawNeedle(ctx, g, g.cx, py, g.cx+d.x*rimR, g.cy+d.y*rimR);
  }

  // ---- gauge night backlight (amber instrument lighting) --------------------
  function gaugeNight(ctx, g){
    ctx.save();
    ctx.beginPath(); ctx.arc(g.cx, g.cy, g.r-1, 0, Math.PI*2); ctx.clip();
    ctx.globalCompositeOperation = 'lighter';
    const gr = ctx.createRadialGradient(g.cx, g.cy, 2, g.cx, g.cy, g.r);
    gr.addColorStop(0,'rgba(255,172,60,0.46)'); gr.addColorStop(0.6,'rgba(228,146,46,0.24)'); gr.addColorStop(1,'rgba(190,116,38,0.08)');
    ctx.fillStyle = gr; ctx.fillRect(g.cx-g.r, g.cy-g.r, g.r*2, g.r*2);
    ctx.restore();
    ctx.save(); ctx.globalCompositeOperation = 'lighter';
    const b = ctx.createRadialGradient(g.cx, g.cy, g.r*0.55, g.cx, g.cy, g.r+16);
    b.addColorStop(0,'rgba(255,172,60,0.18)'); b.addColorStop(1,'rgba(255,172,60,0)');
    ctx.fillStyle = b; ctx.fillRect(g.cx-g.r-18, g.cy-g.r-18, (g.r+18)*2, (g.r+18)*2);
    ctx.restore();
  }

  // ---- wheel ----------------------------------------------------------------
  function buildWheel(r){
    const kn = Math.round(r*0.13)+3, pad = 3, S = (r+kn+pad)*2, c = cv(S,S), g = c.getContext('2d');
    const cx = S/2, cy = S/2, tw = Math.round(r*0.16)+5;
    ring(g, cx, cy, r, r-tw, RUBBER[1]);
    ring(g, cx, cy, r-1, r-tw+1, RUBBER[2]);
    g.save(); g.beginPath(); g.rect(cx-r, cy-r, r, r); g.clip();
    ring(g, cx, cy, r-1, r-tw+2, RUBBER[4]); g.restore();
    ring(g, cx, cy, r, r-1, RUBBER[0]);
    ring(g, cx, cy, r-tw+1, r-tw, RUBBER[0]);
    const sw = Math.round(r*0.14)+1, ri = r-tw+1, ro = Math.round(r*0.24);
    [90,210,330].forEach(deg=>{ const d=dir(deg*DEG);
      thickLine(g, cx+d.x*ro, cy+d.y*ro, cx+d.x*ri, cy+d.y*ri, sw, STEEL[1]);
      thickLine(g, cx+d.x*ro, cy+d.y*ro, cx+d.x*(ri-1), cy+d.y*(ri-1), 2, STEEL[3]); });
    circle(g, cx, cy, ro+3, STEEL[0]); circle(g, cx, cy, ro, STEEL[2]); circle(g, cx-2, cy-2, ro-2, STEEL[3]);
    circle(g, cx, cy, 4, STEEL[0]);
    const kd = dir(0), kx = cx+kd.x*(r-tw/2), ky = cy+kd.y*(r-tw/2);
    circle(g, kx, ky, kn, RUBBER[0]); circle(g, kx, ky, kn-1, GRAPH[3]); circle(g, kx-1, ky-2, kn-3, GRAPH[5]);
    return { c, px:cx, py:cy };
  }

  // ---- single throttle/shift lever — a separate 3D rig (Art/leverRig.js) ----
  // ONE control: push forward (up) = AHEAD, centre = NEUTRAL, pull aft (down) =
  // ASTERN. Throttle magnitude = |sig|, gear = sign(sig). The lever is its own
  // little 3D rig that PIVOTS about this hub — it swings on a real arc with true
  // foreshortening and its sprite frames bake straight out of it. Here we only
  // draw the hub and composite LeverRig.render(sig) at the pivot.
  const LEVER = 'graphite';
  const _lever = ()=> (typeof window!=='undefined') && window.LeverRig;
  function driveHandle(sig){ const R=_lever(); if(!R) return { x:DRIVE.px, y:DRIVE.pivotY-80 }; const o=R.handleOffset(sig, LEVER); return { x:DRIVE.px+o.dx, y:DRIVE.pivotY+o.dy }; }
  function driveFromPoint(x, y){ const R=_lever(); return R ? R.sigFromOffset(x-DRIVE.px, y-DRIVE.pivotY, LEVER) : 0; }
  const driveThrottle = (sig)=> Math.min(1, Math.abs(sig));
  const driveGear = (sig)=> sig < -0.04 ? 'R' : 'F';
  function driveHub(ctx){
    circle(ctx, DRIVE.px, DRIVE.pivotY, 20, GRAPH[0]);
    circle(ctx, DRIVE.px, DRIVE.pivotY, 17, GRAPH[2]);
    circle(ctx, DRIVE.px-1, DRIVE.pivotY-2, 14, GRAPH[3]);
    ring(ctx, DRIVE.px, DRIVE.pivotY, 20, 18, RUBBER[0]);
    screw(ctx, DRIVE.px, DRIVE.pivotY, 3, GRAPH);
  }

  // ---- switches -------------------------------------------------------------
  function toggle(ctx, s, on, lampCol){
    rrect(ctx, s.x-3, s.y-3, s.w+6, s.h+6, 5, GRAPH[0]);
    rrect(ctx, s.x-3, s.y-3, s.w+6, 2, 2, GRAPH[3]);
    screw(ctx, s.x-1, s.y-1, 2, GRAPH); screw(ctx, s.x+s.w+1, s.y+s.h+1, 2, GRAPH);
    rrect(ctx, s.x, s.y, s.w, s.h, 4, '#05080a');
    const midY = s.y + s.h/2, cx = s.x + s.w/2;
    const up = on, ty = up ? s.y+3 : midY+1, by = up ? midY-1 : s.y+s.h-3;
    rrect(ctx, cx-4, ty, 8, by-ty, 3, STEEL[1]);
    rrect(ctx, cx-4, up?ty:by-6, 8, 6, 3, STEEL[3]);
    ctx.fillStyle = STEEL[4]; ctx.fillRect(cx-2, up?ty+1:by-4, 3, 2);
    circle(ctx, cx, midY, 3, GRAPH[5]);
    circle(ctx, s.cx, s.lampY, 3, on ? lampCol : LAMPOFF);
    if (on){ ctx.fillStyle='rgba(255,255,255,0.55)'; ctx.fillRect(s.cx-1, s.lampY-1, 2, 2); }
  }
  function buildKey(){
    const bowW=15, bowH=20, shaft=11, pad=6;
    const w=bowW+pad*2, h=bowH+shaft+pad*2, c=cv(w,h), g=c.getContext('2d');
    const px=Math.round(w/2), py=h-4;
    thickLine(g, px, py, px, py-shaft, 4, STEEL[2]);
    thickLine(g, px-1, py, px-1, py-shaft, 1, STEEL[4]);
    const by = py-shaft-bowH;
    rrect(g, px-Math.round(bowW/2), by, bowW, bowH, 6, RUBBER[1]);
    rrect(g, px-Math.round(bowW/2)+1, by+1, bowW-2, Math.round(bowH*0.5), 5, RUBBER[3]);
    circle(g, px, by+Math.round(bowH*0.5), 4, RUBBER[0]);
    circle(g, px, by+Math.round(bowH*0.5), 3, GRAPH[2]);
    return { c, px, py };
  }
  function drawIgnition(ctx, s, running){
    circle(ctx, s.cx, s.cy, s.r, GRAPH[0]);
    circle(ctx, s.cx, s.cy, s.r-2, GRAPH[3]);
    circle(ctx, s.cx-1, s.cy-2, s.r-4, GRAPH[4]);
    ring(ctx, s.cx, s.cy, s.r-1, s.r-3, STEEL[3]);
    circle(ctx, s.cx, s.cy, 5, '#05080a');
    { const dOff=dir(s.off*DEG), dRun=dir(s.run*DEG);
      ctx.fillStyle=TICKDIM; ctx.fillRect(s.cx+dOff.x*(s.r+3)-1, s.cy+dOff.y*(s.r+3)-1, 2, 2);
      ctx.fillStyle=running?GREEN[2]:TICKDIM; ctx.fillRect(s.cx+dRun.x*(s.r+3)-1, s.cy+dRun.y*(s.r+3)-1, 2, 2); }
    blit(ctx, _key, s.cx, s.cy, running ? s.run : s.off);
    circle(ctx, s.cx, s.cy-s.r-6, 3, running?GREEN[2]:LAMPOFF);
    textC(ctx, 'OFF', s.cx-s.r-1, s.cy+s.r+2, 1, TICKDIM);
    textC(ctx, 'RUN', s.cx+s.r+2, s.cy+s.r+2, 1, running?GREEN[2]:TICKDIM);
  }

  function blit(ctx, part, x, y, ang, scale){
    ctx.save(); ctx.translate(x, y); if(ang) ctx.rotate(ang*DEG); if(scale && scale!==1) ctx.scale(scale, scale); ctx.imageSmoothingEnabled=false;
    ctx.drawImage(part.c, -part.px, -part.py); ctx.restore();
  }

  // ---- full helm face -------------------------------------------------------
  function paint(ctx, o){
    o = o || {};
    const running = !!o.running;
    const drive = Math.max(-1, Math.min(1, o.drive==null?0:o.drive));
    const throttle = driveThrottle(drive), gear = driveGear(drive);
    const fuel = clamp01(o.fuel);
    const rpm = running ? clamp01(o.rpm!=null ? o.rpm : (0.11 + 0.89*throttle)) : 0;
    const steer = Math.max(-1,Math.min(1, o.steer==null?0:o.steer));
    const deck = !!o.deck, spot = !!o.spot, blink = o.blink?1:0;
    const night = !!o.night;
    const lowFuel = fuel < 0.13;
    ctx.clearRect(0,0,W,H);
    ctx.imageSmoothingEnabled = false;
    ctx.save(); ctx.translate(0, TOPPAD);   // drop the whole dash by the headroom; brow instruments may reach up into it

    // ---- windscreen + spotlight can on the rail ----
    rrect(ctx, 175, 26, 250, 22, 5, 'rgba(58,118,128,0.30)');
    rrect(ctx, 175, 26, 250, 3, 2, 'rgba(143,201,196,0.35)');
    thickLine(ctx, 175, 27, 425, 27, 2, STEEL[2]);
    thickLine(ctx, 175, 47, 425, 47, 2, STEEL[1]);
    rrect(ctx, SPOTCAN.x, SPOTCAN.y, SPOTCAN.w, SPOTCAN.h, 5, GRAPH[1]);
    rrect(ctx, SPOTCAN.x, SPOTCAN.y, SPOTCAN.w, 3, 2, GRAPH[3]);
    circle(ctx, SPOTCAN.x+SPOTCAN.w-4, SPOTCAN.y+SPOTCAN.h/2, 6, GRAPH[0]);
    circle(ctx, SPOTCAN.x+SPOTCAN.w-4, SPOTCAN.y+SPOTCAN.h/2, 5, spot?SPOT_ON:'#20323a');
    if (spot){ circle(ctx, SPOTCAN.x+SPOTCAN.w-5, SPOTCAN.y+SPOTCAN.h/2-1, 2, '#ffffff'); }

    // ---- console body (white, teal cove, gold pin) ----
    rrect(ctx, CONSOLE.x, CONSOLE.y, CONSOLE.w, CONSOLE.h, CONSOLE.r, PAINT[0]);
    rrect(ctx, CONSOLE.x+1, CONSOLE.y+1, CONSOLE.w-2, CONSOLE.h-4, CONSOLE.r, PAINT[1]);
    rrect(ctx, CONSOLE.x+3, CONSOLE.y+3, CONSOLE.w-6, 10, CONSOLE.r-4, PAINT[3]);
    rrect(ctx, CONSOLE.x+3, CONSOLE.y+3, CONSOLE.w-6, 4, CONSOLE.r-4, PAINT[4]);
    rrect(ctx, CONSOLE.x+3, CONSOLE.y+CONSOLE.h-9, CONSOLE.w-6, 6, 4, PAINT[0]);
    rrect(ctx, CONSOLE.x+8, CONSOLE.y+56, CONSOLE.w-16, 5, 2, TRIM[3]);
    ctx.fillStyle = GOLD; ctx.fillRect(CONSOLE.x+8, CONSOLE.y+62, CONSOLE.w-16, 1);
    screw(ctx, CONSOLE.x+11, CONSOLE.y+11, 2); screw(ctx, CONSOLE.x+CONSOLE.w-11, CONSOLE.y+11, 2);
    screw(ctx, CONSOLE.x+11, CONSOLE.y+CONSOLE.h-11, 2); screw(ctx, CONSOLE.x+CONSOLE.w-11, CONSOLE.y+CONSOLE.h-11, 2);
    if (deck){ ctx.save(); ctx.globalAlpha=0.14; rrect(ctx, CONSOLE.x+10, CONSOLE.y+68, CONSOLE.w-20, 14, 6, DECK_ON);
      ctx.globalAlpha=0.06; rrect(ctx, CONSOLE.x+10, CONSOLE.y+82, CONSOLE.w-20, 30, 6, DECK_ON); ctx.restore(); }

    const compass = o.compass==='dome' ? 'dome' : 'none';
    const heading = o.heading||0;

    // ---- flush sounder (centre brow) — swaps DEPTH <-> SONAR. When the dome
    // compass shares the brow it slides to port (narrower) so BOTH are fitted. ----
    { const shift = compass==='dome';
      if (o.finder==='fish' && window.FishRig){
        const bw=shift?126:148, bh=shift?150:172, bx=shift?170:226, by=142-bh;
        rrect(ctx, bx-1, by+1, bw+2, bh+3, 13, 'rgba(0,0,0,0.28)');
        window.FishRig.paintInto(ctx, bx, by, bw, bh, { depth:18.4, tempC:13.8, night:night, range:20,
          fish:o.fish || window.FishRig.defaultSchool(), fishID:true, phase:o.phase||0 });
      } else if (window.DepthRig){
        const bw=shift?126:148, bh=86, bx=shift?170:226, by=56;
        rrect(ctx, bx-1, by+2, bw+2, bh+4, 13, 'rgba(0,0,0,0.28)');
        window.DepthRig.paintInto(ctx, bx, by, bw, bh, { depth:18.4, ft:false, night:night, armed:true, alarm:3, tempC:13.8, blink:blink });
      }
    }

    // ---- gauges flanking the wheel ----
    gaugeRPM(ctx, RPM, rpm);
    gaugeFUEL(ctx, FUEL, fuel, lowFuel, blink);
    if (night){ gaugeNight(ctx, RPM); gaugeNight(ctx, FUEL); }

    // ---- switch panel (left) ----
    rrect(ctx, SWPANEL.x-2, SWPANEL.y-2, SWPANEL.w+4, SWPANEL.h+4, SWPANEL.r+1, RUBBER[0]);
    rrect(ctx, SWPANEL.x, SWPANEL.y, SWPANEL.w, SWPANEL.h, SWPANEL.r, GRAPH[1]);
    rrect(ctx, SWPANEL.x+2, SWPANEL.y+2, SWPANEL.w-4, 6, SWPANEL.r-3, GRAPH[3]);
    screw(ctx, SWPANEL.x+8, SWPANEL.y+8, 2, GRAPH); screw(ctx, SWPANEL.x+SWPANEL.w-8, SWPANEL.y+8, 2, GRAPH);
    drawIgnition(ctx, SW.start, running);
    toggle(ctx, SW.deck, deck, DECK_ON);
    toggle(ctx, SW.spot, spot, SPOT_ON);
    textC(ctx, 'DECK', SW.deck.cx, SW.deck.lampY+7, 1, TICKDIM);
    textC(ctx, 'SPOT', SW.spot.cx, SW.spot.lampY+7, 1, TICKDIM);

    // ---- binnacle casing (right) ----
    rrect(ctx, BINN.x-2, BINN.y-2, BINN.w+4, BINN.h+4, BINN.r+1, RUBBER[0]);
    rrect(ctx, BINN.x, BINN.y, BINN.w, BINN.h, BINN.r, GRAPH[1]);
    rrect(ctx, BINN.x+2, BINN.y+2, BINN.w-4, 8, BINN.r-3, GRAPH[3]);
    rrect(ctx, BINN.x+3, BINN.y+BINN.h-8, BINN.w-6, 5, 4, GRAPH[0]);
    screw(ctx, BINN.x+9, BINN.y+10, 2, GRAPH); screw(ctx, BINN.x+BINN.w-9, BINN.y+10, 2, GRAPH);
    screw(ctx, BINN.x+9, BINN.y+BINN.h-10, 2, GRAPH); screw(ctx, BINN.x+BINN.w-9, BINN.y+BINN.h-10, 2, GRAPH);
    // F / N / R detents engraved on the housing (the lever arcs between them)
    textC(ctx, 'F', 570, 300, 1, TEAL);
    textC(ctx, 'N', 570, 318, 1, TICKDIM);
    textC(ctx, 'R', 570, 336, 1, RED[2]);
    // emergency lanyard tab, lower-left
    rrect(ctx, BINN.x+8, BINN.y+BINN.h-30, 15, 12, 3, GRAPH[0]); rrect(ctx, BINN.x+10, BINN.y+BINN.h-28, 11, 4, 2, RED[1]);

    // ---- the lever hub only — the lever itself is composited separately now ----
    driveHub(ctx);

    // ---- steering column + wheel (centred, rotated) ----
    thickLine(ctx, WHEEL.cx, WHEEL.cy, WHEEL.cx, CONSOLE.y+CONSOLE.h-6, 18, GRAPH[2]);
    thickLine(ctx, WHEEL.cx-5, WHEEL.cy+10, WHEEL.cx-5, CONSOLE.y+CONSOLE.h-6, 3, GRAPH[4]);
    circle(ctx, WHEEL.cx, WHEEL.cy, 20, GRAPH[1]);
    blit(ctx, _wheel, WHEEL.cx, WHEEL.cy, ANG.wheel(steer));

    // ---- compass: dome binnacle on the crown (its own rig) ----
    if (compass==='dome' && window.CompassRig){
      window.CompassRig.paintDome(ctx, COMPASS.domeBox.x, COMPASS.domeBox.y, COMPASS.domeBox.w, COMPASS.domeBox.h, { heading, night });
    }

    // ---- spotlight beam overlay ----
    if (spot){ ctx.save(); ctx.globalAlpha=0.10; ctx.fillStyle=SPOT_ON;
      const lx = SPOTCAN.x+SPOTCAN.w-4, ly = SPOTCAN.y+SPOTCAN.h/2;
      ctx.beginPath(); ctx.moveTo(lx, ly); ctx.lineTo(lx+96, ly-24); ctx.lineTo(lx+96, ly+18); ctx.closePath(); ctx.fill(); ctx.restore(); }
    ctx.restore();
  }

  // upright parts baked once
  const _key   = buildKey();
  const _wheel = buildWheel(WHEEL.r);
  function render(o){ const c = cv(W,H); paint(c.getContext('2d'), o); return c; }

  root.ConsoleRig = {
    W, H, TOPPAD, DEG, dir, maxSteer:45, wheelTurn:150, ANG,
    driveHandle, driveFromPoint, driveThrottle, driveGear,
    CONSOLE, WHEEL, RPM, FUEL, SWPANEL, SW, BINN, DRIVE, SPOTCAN, COMPASS,
    paint, render,
  };
})(typeof globalThis!=='undefined'?globalThis:window);
