// Cape Islander Helm — diegetic control rig for the old-school Cape Islander
// wheelhouse (companion to consoleRig.js / sportRig.js, SAME pointer + render
// contract). This is the 1982 hub-boat dash: a MAHOGANY-framed panel faced in
// buff CORK laminate, a chrome destroyer wheel with a wood spinner knob, two
// round chrome-bezel gauges on cream faces (RPM tach + FUEL), stacked banks of
// red rocker breakers on the left, a cream side-binnacle on the right carrying
// ONE chrome single-lever throttle/shift control (push up=AHEAD, centre=NEUTRAL,
// pull down=ASTERN — shares Art/leverRig.js), and a brow that mounts THREE flush
// instruments at real spacing: a swappable DEPTH<->SONAR sounder (left) and TWO
// reserved cutouts for an OPTIONAL RADAR (centre) and OPTIONAL GPS/plotter
// (right) — those two are separate rigs, made later; here the mounts are drawn
// (fitted standby-screen placeholder, or a cork blanking cover) and their boxes
// are exposed as CapeRig.RADAR / CapeRig.GPS with paintRadar/paintGps hooks so
// the future rigs drop straight in. Brand wordmarks are original (no trademark).
(function (root) {
  const W = 600, TOPPAD = 54, H = 494 + TOPPAD;   // headroom so the portrait sonar reaches above the dash
  const DEG = Math.PI / 180;

  // ---- palettes (Cape Islander KTC: cork buff + mahogany + chrome) ----------
  const CORK   = ['#786a46','#94855a','#ac9c6c','#c0b082','#d2c393','#e0d3a8'];  // buff cork laminate dash face
  const WOOD   = ['#241a10','#38271a','#4d3722','#63472c','#7a5a3a','#916c46'];  // dark mahogany trim frame
  const WOODHI = '#a5804f';
  const CHROME = ['#12181c','#2f3a42','#586770','#8ea1a9','#c6d2d5','#f1f7f7'];  // stainless bezels / wheel
  const STEEL  = ['#232a30','#39434b','#556069','#7a8892','#9db0b8'];            // screws / spokes
  const RUBBER = ['#0b0e11','#11161a','#1a2126','#252e35','#333f47'];
  const BRASS  = ['#5f4715','#8a6a22','#b98f2f','#dbb043','#efc85a'];            // cove pin / nameplate
  const KNOB   = ['#4a2f16','#6f4823','#8f5f2e','#b57f3e','#d59f56'];            // wood spinner knob
  const FACE1  = '#e9e2cf', FACE2 = '#d7cdb2';                                   // aged cream gauge face
  const INK    = '#232823', INKDIM = '#8a825f';                                  // gauge print
  const NEEDLE = '#1f2422', NEEDLE_HL = '#3b433c', NEEDLE_DK = '#0c0f0d';        // black instrument needle
  const RED    = ['#3f120d','#7c2a20','#b3372a','#e0554a','#f59183','#ffb7ac'];
  const GREEN  = ['#1c6a3b','#2f9e57','#66d585'];
  const AMBER  = '#e6b53f';
  const TEAL   = '#7fd6c9', TEAK = '#c9a86a', BLUE = '#5b93b8';
  const GLASS  = '#070f11';                                                      // dark screen glass
  const LAMPOFF = '#3a1712';                                                     // unlit rocker lamp
  const SPOT_ON = '#ffe9c0', DECK_ON = '#ffdfa0';

  // ---- 3x5 bitmap font (full A-Z / 0-9) -------------------------------------
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
    '/':['..#','..#','.#.','#..','#..'],'-':['...','...','###','...','...'],
    '.':['...','...','...','...','.#.'],' ':['...','...','...','...','...'],
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
    ctx.fillStyle = fill; cx=Math.round(cx); cy=Math.round(cy);
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
  function ellipse(ctx, cx, cy, rx, ry, fill){
    ctx.fillStyle = fill; cx=Math.round(cx); cy=Math.round(cy); rx=Math.max(1,Math.round(rx)); ry=Math.max(1,Math.round(ry));
    for (let dy=-ry; dy<=ry; dy++){ const dx=Math.floor(rx*Math.sqrt(Math.max(0,1-(dy*dy)/(ry*ry)))); ctx.fillRect(cx-dx, cy+dy, 2*dx+1, 1); }
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
  const dir = (a)=>({ x:Math.sin(a), y:-Math.cos(a) });
  function clamp01(v){ return Math.max(0, Math.min(1, v==null?0:v)); }
  function hashRnd(n){ const x=Math.sin(n*127.1+13.7)*43758.5453; return x-Math.floor(x); }

  // ---- angle maps -----------------------------------------------------------
  const ANG = { rpm:(v)=> -135 + 270*clamp01(v), wheel:(s)=> Math.max(-1,Math.min(1,s)) * 150 };
  const FSWEEP = 200;
  const fuelPhi = (v)=> -FSWEEP/2 + FSWEEP*clamp01(v);

  // ---- geometry (rig-local == canvas px, before the TOPPAD translate) -------
  const PANEL = { x:12, y:34, w:576, h:452, r:16 };                 // mahogany frame outer
  const CORKF = { x:26, y:48, w:548, h:424, r:8 };                  // cork face inside the frame
  const WHEEL = { cx:300, cy:328, r:104 };
  const RPM   = { cx:92,  cy:214, r:50 };
  const FUEL  = { cx:508, cy:214, r:50 };
  const NAME  = { cx:300, cy:452, rx:78, ry:14 };
  // brow — THREE equal flush mounts (left / centre / right). Which instrument
  // sits in which slot is a caller parameter (o.layout): the player swaps them
  // in-game, so ANY slot can host the depth/sonar sounder, the radar or the gps.
  const SLOTW = 150, SLOTY = 18, SLOTH = 104, BROWB = 122;         // landscape box; the portrait sonar rises above BROWB
  const SLOTS = [ { x:52 }, { x:225 }, { x:398 } ];
  const DEFAULT_LAYOUT = ['sounder','radar','gps'];
  const COMPASS = { domeBox:{ x:226, y:-50, w:148, h:182 }, flushBox:{ x:260, y:132, w:80, h:80 } };
  function slotBox(i, portrait){ const s=SLOTS[i]||SLOTS[0];
    return portrait ? { x:s.x, y:BROWB-150, w:SLOTW, h:150 } : { x:s.x, y:SLOTY, w:SLOTW, h:SLOTH }; }
  // breaker banks (left), two columns of red rockers
  const BANK  = { x:34, y:298, w:154, h:178, r:9 };
  const ROCK  = { w:56, h:18, colX:[BANK.x+16, BANK.x+82], row0:322, dy:27, rows:6 };
  // ignition key switch (by the throttle)
  const IGN   = { cx:400, cy:454, r:17, off:-22, run:30 };
  // cream side-binnacle housing + the single-lever pivot
  const BINN  = { x:430, y:296, w:154, h:180, r:14 };
  const DRIVE = { px:507, pivotY:456, hitR:46 };
  const LEVER = 'chrome';

  // ---- gauges ---------------------------------------------------------------
  function gaugeBezel(ctx, g){
    circle(ctx, g.cx, g.cy, g.r+6, RUBBER[0]);
    ring(ctx, g.cx, g.cy, g.r+5, g.r+1, CHROME[2]);
    ctx.save(); ctx.beginPath(); ctx.rect(g.cx-g.r-6, g.cy-g.r-6, (g.r+6)*2, g.r+3); ctx.clip();
    ring(ctx, g.cx, g.cy, g.r+5, g.r+1, CHROME[5]); ctx.restore();
    circle(ctx, g.cx, g.cy, g.r, FACE1);
    ring(ctx, g.cx, g.cy, g.r, g.r-2, FACE2);
    ctx.fillStyle = 'rgba(255,255,255,0.28)'; ctx.fillRect(g.cx-g.r+4, g.cy-g.r+5, g.r, 2);
  }
  function radialTick(ctx, g, deg, rOut, rIn, w, col){
    const d = dir(deg*DEG); thickLine(ctx, g.cx+d.x*rIn, g.cy+d.y*rIn, g.cx+d.x*rOut, g.cy+d.y*rOut, w, col);
  }
  function drawNeedle(ctx, g, px, py, tx, ty){
    const dx=tx-px, dy=ty-py, L=Math.hypot(dx,dy)||1, ux=dx/L, uy=dy/L, tail=Math.min(14, L*0.3);
    thickLine(ctx, px, py, tx, ty, 3, NEEDLE);
    thickLine(ctx, px, py, px+ux*(L-2), py+uy*(L-2), 1, NEEDLE_HL);
    thickLine(ctx, px, py, px-ux*tail, py-uy*tail, 4, NEEDLE_DK);
    const hr=Math.round(g.r*0.15)+1;
    circle(ctx, px, py, hr, CHROME[1]); circle(ctx, px, py, hr-1, CHROME[3]); circle(ctx, px-1, py-1, Math.max(1,hr-3), CHROME[5]);
  }
  function gaugeRPM(ctx, g, rpm){
    gaugeBezel(ctx, g);
    for (let i=0;i<=12;i++){ const v=i/12, deg=-135+270*v, major=(i%2===0);
      radialTick(ctx, g, deg, g.r-3, major?g.r-12:g.r-8, major?2:1, major?INK:INKDIM); }
    for (let i=0;i<=10;i++){ const v=0.84+0.16*i/10, deg=-135+270*v; radialTick(ctx, g, deg, g.r-3, g.r-7, 2, RED[2]); }
    for (let n=0;n<=6;n++){ const v=n/6, d=dir((-135+270*v)*DEG), rr=g.r-21;
      textC(ctx, String(n), g.cx+d.x*rr, g.cy+d.y*rr-2, 1, n>=5?RED[2]:INK); }
    textC(ctx, 'X100', g.cx, g.cy-6, 1, INKDIM);
    textC(ctx, 'RPM',  g.cx, g.cy+g.r-19, 2, BRASS[1]);
    const rd=dir(ANG.rpm(rpm)*DEG), len=g.r-13;
    drawNeedle(ctx, g, g.cx, g.cy, g.cx+rd.x*len, g.cy+rd.y*len);
  }
  function gaugeFUEL(ctx, g, fuel, low, blink){
    gaugeBezel(ctx, g);
    const drop=Math.round(g.r*0.42), py=g.cy+drop, rimR=g.r-6;
    for (let i=0;i<=10;i++){ const v=i/10, deg=fuelPhi(v), major=(i%2===0);
      radialTick(ctx, g, deg, g.r-3, major?g.r-12:g.r-8, major?2:1, major?INK:INKDIM); }
    for (let i=0;i<=3;i++){ const v=0.10*i/3, deg=fuelPhi(v); radialTick(ctx, g, deg, g.r-3, g.r-7, 2, RED[2]); }
    { const d=dir(fuelPhi(0)*DEG),   rr=g.r-15; textC(ctx, 'E', g.cx+d.x*rr, g.cy+d.y*rr-3, 2, RED[2]); }
    { const d=dir(fuelPhi(1)*DEG),   rr=g.r-15; textC(ctx, 'F', g.cx+d.x*rr, g.cy+d.y*rr-3, 2, INK); }
    { const d=dir(fuelPhi(0.5)*DEG), rr=g.r-16; ctx.fillStyle=INK; ctx.fillRect(g.cx+d.x*rr-1, g.cy+d.y*rr-2, 2, 4); }
    textC(ctx, 'FUEL', g.cx, g.cy+g.r-17, 2, BRASS[1]);
    circle(ctx, g.cx, py-15, 3, (low && blink) ? AMBER : '#b9ad8e');
    const d=dir(fuelPhi(fuel)*DEG);
    drawNeedle(ctx, g, g.cx, py, g.cx+d.x*rimR, g.cy+d.y*rimR);
  }
  function gaugeNight(ctx, g){
    ctx.save(); ctx.beginPath(); ctx.arc(g.cx, g.cy, g.r-1, 0, Math.PI*2); ctx.clip();
    ctx.globalCompositeOperation = 'lighter';
    const gr = ctx.createRadialGradient(g.cx, g.cy, 2, g.cx, g.cy, g.r);
    gr.addColorStop(0,'rgba(255,172,60,0.40)'); gr.addColorStop(0.6,'rgba(228,146,46,0.20)'); gr.addColorStop(1,'rgba(190,116,38,0.06)');
    ctx.fillStyle = gr; ctx.fillRect(g.cx-g.r, g.cy-g.r, g.r*2, g.r*2); ctx.restore();
  }

  // ---- chrome destroyer wheel + wood spinner knob ---------------------------
  function buildWheel(r){
    const kn = Math.round(r*0.12)+3, pad = 3, S = (r+kn+pad)*2, c = cv(S,S), g = c.getContext('2d');
    const cx = S/2, cy = S/2, tw = Math.round(r*0.13)+4;
    ring(g, cx, cy, r, r-tw, CHROME[1]);
    ring(g, cx, cy, r-1, r-tw+1, CHROME[3]);
    g.save(); g.beginPath(); g.rect(cx-r, cy-r, r*2, r); g.clip();
    ring(g, cx, cy, r-1, r-tw+2, CHROME[5]); g.restore();
    ring(g, cx, cy, r, r-1, CHROME[0]);
    ring(g, cx, cy, r-tw+1, r-tw, CHROME[0]);
    const sw = Math.round(r*0.10)+1, ri = r-tw+1, ro = Math.round(r*0.22);
    [90,210,330].forEach(deg=>{ const d=dir(deg*DEG);
      thickLine(g, cx+d.x*ro, cy+d.y*ro, cx+d.x*ri, cy+d.y*ri, sw, CHROME[2]);
      thickLine(g, cx+d.x*ro, cy+d.y*ro, cx+d.x*(ri-1), cy+d.y*(ri-1), 2, CHROME[5]); });
    // hub — chrome ring with a turned brass cap
    circle(g, cx, cy, ro+3, CHROME[0]); circle(g, cx, cy, ro, CHROME[2]); circle(g, cx-2, cy-2, ro-2, CHROME[4]);
    circle(g, cx, cy, ro-4, BRASS[1]); circle(g, cx-1, cy-2, ro-6, BRASS[2]); circle(g, cx-1, cy-2, Math.max(1,ro-9), BRASS[3]);
    // wood spinner knob on the rim, upper-left
    const kd = dir(315*DEG), kx = cx+kd.x*(r-tw/2), ky = cy+kd.y*(r-tw/2);
    circle(g, kx, ky, kn+1, KNOB[0]); circle(g, kx, ky, kn, KNOB[2]); circle(g, kx-1, ky-1, kn-2, KNOB[3]);
    circle(g, kx-1, ky-2, Math.max(1,kn-4), KNOB[4]);
    return { c, px:cx, py:cy };
  }

  // ---- single throttle/shift lever hooks (Art/leverRig.js, chrome spec) -----
  const _lever = ()=> (typeof window!=='undefined') && window.LeverRig;
  function driveHandle(sig){ const R=_lever(); if(!R) return { x:DRIVE.px, y:DRIVE.pivotY-80 }; const o=R.handleOffset(sig, LEVER); return { x:DRIVE.px+o.dx, y:DRIVE.pivotY+o.dy }; }
  function driveFromPoint(x, y){ const R=_lever(); return R ? R.sigFromOffset(x-DRIVE.px, y-DRIVE.pivotY, LEVER) : 0; }
  const driveThrottle = (sig)=> Math.min(1, Math.abs(sig));
  const driveGear = (sig)=> sig < -0.04 ? 'R' : 'F';
  function driveHub(ctx){
    circle(ctx, DRIVE.px, DRIVE.pivotY, 20, CHROME[0]);
    circle(ctx, DRIVE.px, DRIVE.pivotY, 17, CHROME[2]);
    circle(ctx, DRIVE.px-1, DRIVE.pivotY-2, 14, CHROME[4]);
    ring(ctx, DRIVE.px, DRIVE.pivotY, 20, 18, RUBBER[0]);
    screw(ctx, DRIVE.px, DRIVE.pivotY, 3, CHROME);
  }

  // ---- red rocker breaker -----------------------------------------------------
  function rocker(ctx, x, y, on, label){
    rrect(ctx, x-2, y-2, ROCK.w+4, ROCK.h+4, 4, RUBBER[0]);       // gasket
    rrect(ctx, x, y, ROCK.w, ROCK.h, 3, '#0a0d10');               // well
    const half = Math.round(ROCK.h/2);
    // paddle rocks toward the lit (bottom) half when on
    const lit = on ? [x+1, y+half, ROCK.w-2, ROCK.h-half-1] : [x+1, y+1, ROCK.w-2, half-1];
    const dim = on ? [x+1, y+1, ROCK.w-2, half-1] : [x+1, y+half, ROCK.w-2, ROCK.h-half-1];
    rrect(ctx, dim[0], dim[1], dim[2], dim[3], 2, RED[0]);        // recessed dark half
    rrect(ctx, lit[0], lit[1], lit[2], lit[3], 2, on?RED[3]:RED[1]);
    if (on){ rrect(ctx, lit[0]+2, lit[1]+1, lit[2]-4, 2, 1, RED[4]);
      ctx.fillStyle='rgba(255,220,200,0.7)'; ctx.fillRect(x+ROCK.w-8, lit[1]+2, 3, 2); }
    // engraved legend above the rocker
    textC(ctx, label, x+ROCK.w/2, y-8, 1, on?'#d8ccae':INKDIM);
  }
  function breakerBank(ctx, deck, spot){
    // dark mounting sub-panel let into the cork
    rrect(ctx, BANK.x-2, BANK.y-2, BANK.w+4, BANK.h+4, BANK.r+1, RUBBER[0]);
    rrect(ctx, BANK.x, BANK.y, BANK.w, BANK.h, BANK.r, CHROME[1]);
    rrect(ctx, BANK.x+2, BANK.y+2, BANK.w-4, 5, BANK.r-3, CHROME[3]);
    rrect(ctx, BANK.x+3, BANK.y+BANK.h-6, BANK.w-6, 4, 3, RUBBER[0]);
    screw(ctx, BANK.x+8, BANK.y+8, 2, CHROME); screw(ctx, BANK.x+BANK.w-8, BANK.y+8, 2, CHROME);
    screw(ctx, BANK.x+8, BANK.y+BANK.h-8, 2, CHROME); screw(ctx, BANK.x+BANK.w-8, BANK.y+BANK.h-8, 2, CHROME);
    // two columns; DECK + SPOT are live (top of each column), rest are labelled circuits (unlit)
    const colA = ['DECK','BILGE','NAV','ANCH','PUMP','HORN'];
    const colB = ['SPOT','WIPE','CABIN','INST','ACC','VHF'];
    for (let i=0;i<ROCK.rows;i++){ const y=ROCK.row0 + i*ROCK.dy;
      rocker(ctx, ROCK.colX[0], y, i===0?deck:false, colA[i]);
      rocker(ctx, ROCK.colX[1], y, i===0?spot:false, colB[i]);
    }
  }
  function buildKey(){
    const bowW=14, bowH=18, shaft=10, pad=6, w=bowW+pad*2, h=bowH+shaft+pad*2, c=cv(w,h), g=c.getContext('2d');
    const px=Math.round(w/2), py=h-4;
    thickLine(g, px, py, px, py-shaft, 4, CHROME[3]);
    thickLine(g, px-1, py, px-1, py-shaft, 1, CHROME[5]);
    const by = py-shaft-bowH;
    rrect(g, px-Math.round(bowW/2), by, bowW, bowH, 6, KNOB[1]);
    rrect(g, px-Math.round(bowW/2)+1, by+1, bowW-2, Math.round(bowH*0.5), 5, KNOB[3]);
    circle(g, px, by+Math.round(bowH*0.5), 4, KNOB[0]); circle(g, px, by+Math.round(bowH*0.5), 3, KNOB[2]);
    return { c, px, py };
  }
  function drawIgnition(ctx, s, running){
    circle(ctx, s.cx, s.cy, s.r+2, RUBBER[0]);
    circle(ctx, s.cx, s.cy, s.r, CHROME[1]);
    circle(ctx, s.cx-1, s.cy-2, s.r-3, CHROME[3]);
    ring(ctx, s.cx, s.cy, s.r, s.r-2, CHROME[4]);
    circle(ctx, s.cx, s.cy, 5, '#05080a');
    { const dOff=dir(s.off*DEG), dRun=dir(s.run*DEG);
      ctx.fillStyle=INKDIM; ctx.fillRect(s.cx+dOff.x*(s.r+3)-1, s.cy+dOff.y*(s.r+3)-1, 2, 2);
      ctx.fillStyle=running?GREEN[2]:INKDIM; ctx.fillRect(s.cx+dRun.x*(s.r+3)-1, s.cy+dRun.y*(s.r+3)-1, 2, 2); }
    blit(ctx, _key, s.cx, s.cy, running ? s.run : s.off);
    textC(ctx, 'OFF', s.cx-s.r-2, s.cy+s.r+3, 1, INKDIM);
    textC(ctx, 'RUN', s.cx+s.r+3, s.cy+s.r+3, 1, running?GREEN[1]:INKDIM);
  }

  function blit(ctx, part, x, y, ang, scale){
    ctx.save(); ctx.translate(x, y); if(ang) ctx.rotate(ang*DEG); if(scale && scale!==1) ctx.scale(scale, scale); ctx.imageSmoothingEnabled=false;
    ctx.drawImage(part.c, -part.px, -part.py); ctx.restore();
  }

  // ---- flush screen mount (RADAR / GPS) -------------------------------------
  // The cutout + chrome bezel are ALWAYS drawn (this IS the reserved space).
  // fitted -> a standby-screen placeholder (dim iconographic hint + label + LED),
  //           ready for the future RadarRig / GpsRig to paintInto the glass box.
  // !fitted -> a cork blanking cover over the cutout.
  function screenMount(ctx, b, kind, fitted, night, phase, painter){
    rrect(ctx, b.x-5, b.y-5, b.w+10, b.h+10, 9, RUBBER[0]);            // gasket
    rrect(ctx, b.x-4, b.y-4, b.w+8, b.h+8, 8, CHROME[2]);             // chrome bezel
    ctx.save(); ctx.beginPath(); ctx.rect(b.x-4, b.y-4, b.w+8, 5); ctx.clip();
    rrect(ctx, b.x-4, b.y-4, b.w+8, 8, 8, CHROME[5]); ctx.restore();
    screw(ctx, b.x-1, b.y-1, 2, CHROME); screw(ctx, b.x+b.w+1, b.y-1, 2, CHROME);
    screw(ctx, b.x-1, b.y+b.h+1, 2, CHROME); screw(ctx, b.x+b.w+1, b.y+b.h+1, 2, CHROME);

    if (!fitted){
      rrect(ctx, b.x, b.y, b.w, b.h, 5, CORK[3]);                     // cork cover plate
      rrect(ctx, b.x+2, b.y+2, b.w-4, 4, 3, CORK[5]);
      rrect(ctx, b.x+2, b.y+b.h-5, b.w-4, 3, 2, CORK[1]);
      screw(ctx, b.x+7, b.y+7, 2, STEEL); screw(ctx, b.x+b.w-7, b.y+7, 2, STEEL);
      screw(ctx, b.x+7, b.y+b.h-7, 2, STEEL); screw(ctx, b.x+b.w-7, b.y+b.h-7, 2, STEEL);
      textC(ctx, kind, b.cx!=null?b.cx:b.x+b.w/2, b.y+b.h/2-9, 2, WOOD[2]);
      textC(ctx, 'BLANKING PLATE', b.x+b.w/2, b.y+b.h/2+6, 1, INKDIM);
      return;
    }

    // recessed dark glass
    rrect(ctx, b.x, b.y, b.w, b.h, 5, '#02090b');
    rrect(ctx, b.x+2, b.y+2, b.w-4, b.h-4, 4, GLASS);
    if (painter){ painter(ctx, b.x+2, b.y+2, b.w-4, b.h-4, { night, phase }); }
    else {
      const cx=b.x+b.w/2, cy=b.y+b.h/2, base = night ? '#3a2c0c' : '#123a36';
      const glow = night ? 'rgba(255,190,60,0.10)' : 'rgba(127,214,201,0.09)';
      if (kind==='RADAR'){
        for (let i=1;i<=3;i++) ring(ctx, cx, cy+6, i*Math.round(b.h*0.16), i*Math.round(b.h*0.16)-1, base);
        const sw = (phase||0)*0.9, d = dir(sw); ctx.save(); ctx.globalAlpha=0.5;
        thickLine(ctx, cx, cy+6, cx+d.x*b.h*0.42, cy+6+d.y*b.h*0.42, 2, night?AMBER:TEAL); ctx.restore();
      } else {
        ctx.fillStyle=base;
        for (let gx=b.x+10; gx<b.x+b.w-8; gx+=14) ctx.fillRect(gx, b.y+8, 1, b.h-16);
        for (let gy=b.y+10; gy<b.y+b.h-8; gy+=13) ctx.fillRect(b.x+8, gy, b.w-16, 1);
        thickLine(ctx, cx-14, cy+8, cx+2, cy-6, 2, night?AMBER:TEAL);
        thickLine(ctx, cx+2, cy-6, cx+16, cy+2, 2, night?AMBER:TEAL);
        circle(ctx, cx+16, cy+2, 2, night?'#ffd77a':'#a9e7dd');
      }
      ctx.save(); ctx.globalCompositeOperation='lighter'; ctx.fillStyle=glow;
      ctx.fillRect(b.x+2, b.y+2, b.w-4, b.h-4); ctx.restore();
      // label + standby line
      textC(ctx, kind, cx, b.y+8, 2, night?'#ffd77a':'#a9e7dd');
      textC(ctx, 'STANDBY \u00b7 OPTIONAL', cx, b.y+b.h-13, 1, night?'#8a6a2c':'#4e7d78');
    }
    // glass glare + standby LED
    ctx.fillStyle='rgba(255,255,255,0.06)'; ctx.fillRect(b.x+3, b.y+3, b.w-6, 2);
    circle(ctx, b.x+b.w-8, b.y+7, 2, night?AMBER:GREEN[1]);
  }

  // ---- brass nameplate --------------------------------------------------------
  function nameplate(ctx, n){
    ellipse(ctx, n.cx, n.cy+1, n.rx+2, n.ry+2, RUBBER[0]);
    ellipse(ctx, n.cx, n.cy, n.rx, n.ry, BRASS[0]);
    ellipse(ctx, n.cx, n.cy, n.rx-2, n.ry-2, BRASS[1]);
    ellipse(ctx, n.cx, n.cy-2, n.rx-4, n.ry-5, BRASS[2]);
    ctx.save(); ctx.beginPath(); ctx.rect(n.cx-n.rx, n.cy-n.ry, n.rx*2, n.ry); ctx.clip();
    ellipse(ctx, n.cx, n.cy-1, n.rx-3, n.ry-3, BRASS[3]); ctx.restore();
    textC(ctx, 'CAPE ISLANDER', n.cx, n.cy-6, 1, WOOD[0]);
    textC(ctx, 'HARBOUR MARINE', n.cx, n.cy+2, 1, WOOD[1]);
  }

  // ---- cork face with speckle -------------------------------------------------
  function corkFace(ctx){
    rrect(ctx, CORKF.x, CORKF.y, CORKF.w, CORKF.h, CORKF.r, CORK[3]);
    rrect(ctx, CORKF.x+1, CORKF.y+1, CORKF.w-2, CORKF.h-3, CORKF.r, CORK[3]);
    // vignette top-light
    ctx.save(); ctx.beginPath();
    { const x=CORKF.x,y=CORKF.y,w=CORKF.w,h=CORKF.h,r=CORKF.r,p=new Path2D();
      p.moveTo(x+r,y); p.arcTo(x+w,y,x+w,y+h,r); p.arcTo(x+w,y+h,x,y+h,r); p.arcTo(x,y+h,x,y,r); p.arcTo(x,y,x+w,y,r); p.closePath(); ctx.clip(p); }
    ctx.fillStyle='rgba(255,248,224,0.16)'; ctx.fillRect(CORKF.x, CORKF.y, CORKF.w, 22);
    ctx.fillStyle='rgba(30,22,10,0.14)'; ctx.fillRect(CORKF.x, CORKF.y+CORKF.h-26, CORKF.w, 26);
    // cork speckle
    const n = 1250;
    for (let i=0;i<n;i++){ const rx=hashRnd(i*1.7), ry=hashRnd(i*3.1+9), rv=hashRnd(i*5.3+2);
      const px=CORKF.x+Math.floor(rx*CORKF.w), py=CORKF.y+Math.floor(ry*CORKF.h);
      ctx.fillStyle = rv>0.66 ? CORK[5] : (rv>0.33 ? CORK[1] : CORK[2]);
      ctx.fillRect(px, py, 1, 1);
    }
    ctx.restore();
  }
  function woodFrame(ctx){
    rrect(ctx, PANEL.x, PANEL.y, PANEL.w, PANEL.h, PANEL.r, WOOD[0]);
    rrect(ctx, PANEL.x+1, PANEL.y+1, PANEL.w-2, PANEL.h-2, PANEL.r-1, WOOD[2]);
    rrect(ctx, PANEL.x+3, PANEL.y+3, PANEL.w-6, PANEL.h-6, PANEL.r-2, WOOD[3]);
    // grain streaks + bevel highlight along the top
    ctx.save(); ctx.beginPath();
    { const x=PANEL.x+3,y=PANEL.y+3,w=PANEL.w-6,h=PANEL.h-6,r=PANEL.r-2,p=new Path2D();
      p.moveTo(x+r,y); p.arcTo(x+w,y,x+w,y+h,r); p.arcTo(x+w,y+h,x,y+h,r); p.arcTo(x,y+h,x,y,r); p.arcTo(x,y,x+w,y,r); p.closePath(); ctx.clip(p); }
    for (let i=0;i<40;i++){ const yy=PANEL.y+4+hashRnd(i*2.3)*(PANEL.h-8); ctx.fillStyle= hashRnd(i)>0.5?WOODHI:WOOD[1];
      ctx.globalAlpha=0.35; ctx.fillRect(PANEL.x+4, Math.floor(yy), PANEL.w-8, 1); }
    ctx.globalAlpha=1; ctx.restore();
    rrect(ctx, PANEL.x+3, PANEL.y+3, PANEL.w-6, 3, PANEL.r-2, WOODHI);
    // brass cove pin around the cork edge
    ctx.strokeStyle=BRASS[2];
    rrect(ctx, CORKF.x-3, CORKF.y-3, CORKF.w+6, 2, 1, BRASS[2]);
    // corner bungs
    screw(ctx, PANEL.x+10, PANEL.y+10, 2, KNOB); screw(ctx, PANEL.x+PANEL.w-10, PANEL.y+10, 2, KNOB);
    screw(ctx, PANEL.x+10, PANEL.y+PANEL.h-10, 2, KNOB); screw(ctx, PANEL.x+PANEL.w-10, PANEL.y+PANEL.h-10, 2, KNOB);
  }

  // ---- full helm face -------------------------------------------------------
  function paint(ctx, o){
    o = o || {};
    const running = !!o.running;
    const drive = Math.max(-1, Math.min(1, o.drive==null?0:o.drive));
    const throttle = driveThrottle(drive);
    const fuel = clamp01(o.fuel);
    const rpm = running ? clamp01(o.rpm!=null ? o.rpm : (0.11 + 0.89*throttle)) : 0;
    const steer = Math.max(-1,Math.min(1, o.steer==null?0:o.steer));
    const deck = !!o.deck, spot = !!o.spot, blink = o.blink?1:0, night = !!o.night;
    const radar = o.radar==null?true:!!o.radar, gps = o.gps==null?true:!!o.gps;
    const lowFuel = fuel < 0.13;
    ctx.clearRect(0,0,W,H);
    ctx.imageSmoothingEnabled = false;
    ctx.save(); ctx.translate(0, TOPPAD);

    // ---- panel: mahogany frame + cork face ----
    woodFrame(ctx);
    corkFace(ctx);

    // deck / spot working-light washes over the cork
    if (deck){ ctx.save(); ctx.globalCompositeOperation='lighter'; ctx.globalAlpha=0.10;
      rrect(ctx, CORKF.x, CORKF.y, CORKF.w, CORKF.h, CORKF.r, DECK_ON); ctx.restore(); }
    if (spot){ ctx.save(); ctx.globalCompositeOperation='lighter'; ctx.globalAlpha=0.14;
      const gg=ctx.createRadialGradient(WHEEL.cx, 150, 20, WHEEL.cx, 150, 220);
      gg.addColorStop(0, SPOT_ON); gg.addColorStop(1,'rgba(255,233,192,0)'); ctx.fillStyle=gg;
      ctx.fillRect(CORKF.x, CORKF.y, CORKF.w, CORKF.h); ctx.restore(); }

    const compass = (o.compass==='dome'||o.compass==='flush') ? o.compass : 'none';
    const heading = o.heading||0;

    // ---- brow: three player-swappable flush mounts ----
    const layout = Array.isArray(o.layout) && o.layout.length===3 ? o.layout : DEFAULT_LAYOUT;
    const drawSounder=(b, fish)=>{
      rrect(ctx, b.x-5, b.y-4, b.w+10, b.h+9, 9, RUBBER[0]);
      rrect(ctx, b.x-4, b.y-3, b.w+8, b.h+7, 8, CHROME[2]);
      if (fish && window.FishRig){ window.FishRig.paintInto(ctx, b.x, b.y, b.w, b.h, { depth:18.4, tempC:13.8, night, range:20,
        fish:o.fish || window.FishRig.defaultSchool(), fishID:true, phase:o.phase||0 }); }
      else if (window.DepthRig){ window.DepthRig.paintInto(ctx, b.x, b.y, b.w, b.h, { depth:18.4, ft:false, night, armed:true, alarm:3, tempC:13.8, blink }); }
    };
    for (let i=0;i<3;i++){ if (compass==='dome' && i===1) continue; const id=layout[i];
      if (id==='sounder'){ const fish=o.finder==='fish'; drawSounder(slotBox(i, fish), fish); }
      else if (id==='radar'){ screenMount(ctx, slotBox(i,false), 'RADAR', radar, night, o.phase||0, root.CapeRig && root.CapeRig.paintRadar); }
      else if (id==='gps'){ screenMount(ctx, slotBox(i,false), 'GPS', gps, night, o.phase||0, root.CapeRig && root.CapeRig.paintGps); }
    }

    // ---- compass: its own rig (dome binnacle on the crown, or flush in the dash) ----
    if (window.CompassRig){
      if (compass==='dome') window.CompassRig.paintDome(ctx, COMPASS.domeBox.x, COMPASS.domeBox.y, COMPASS.domeBox.w, COMPASS.domeBox.h, { heading, night });
      else if (compass==='flush') window.CompassRig.paintFlush(ctx, COMPASS.flushBox.x, COMPASS.flushBox.y, COMPASS.flushBox.w, COMPASS.flushBox.h, { heading, night });
    }

    // ---- gauges ----
    gaugeRPM(ctx, RPM, rpm);
    gaugeFUEL(ctx, FUEL, fuel, lowFuel, blink);
    if (night){ gaugeNight(ctx, RPM); gaugeNight(ctx, FUEL); }

    // ---- breaker banks (left) ----
    breakerBank(ctx, deck, spot);

    // ---- side-binnacle housing (cream, right) ----
    rrect(ctx, BINN.x-2, BINN.y-2, BINN.w+4, BINN.h+4, BINN.r+1, RUBBER[0]);
    rrect(ctx, BINN.x, BINN.y, BINN.w, BINN.h, BINN.r, CORK[1]);
    rrect(ctx, BINN.x+1, BINN.y+1, BINN.w-2, BINN.h-4, BINN.r-1, CORK[2]);
    rrect(ctx, BINN.x+3, BINN.y+3, BINN.w-6, 6, BINN.r-3, CORK[4]);
    rrect(ctx, BINN.x+3, BINN.y+BINN.h-8, BINN.w-6, 5, 4, CORK[0]);
    screw(ctx, BINN.x+9, BINN.y+10, 2, CHROME); screw(ctx, BINN.x+BINN.w-9, BINN.y+10, 2, CHROME);
    screw(ctx, BINN.x+9, BINN.y+BINN.h-10, 2, CHROME); screw(ctx, BINN.x+BINN.w-9, BINN.y+BINN.h-10, 2, CHROME);
    // F / N / R detents engraved on the housing
    textC(ctx, 'F', 586, 316, 1, WOOD[0]);
    textC(ctx, 'N', 586, 336, 1, WOOD[1]);
    textC(ctx, 'R', 586, 356, 1, RED[2]);
    rrect(ctx, BINN.x+8, BINN.y+BINN.h-30, 15, 12, 3, RUBBER[0]); rrect(ctx, BINN.x+10, BINN.y+BINN.h-28, 11, 4, 2, RED[2]);

    // ---- ignition key (by the throttle) ----
    drawIgnition(ctx, IGN, running);

    // ---- lever hub (lever itself composited separately by the DC) ----
    driveHub(ctx);

    // ---- steering column + chrome wheel ----
    thickLine(ctx, WHEEL.cx, WHEEL.cy, WHEEL.cx, CORKF.y+CORKF.h-6, 18, CHROME[1]);
    thickLine(ctx, WHEEL.cx-5, WHEEL.cy+10, WHEEL.cx-5, CORKF.y+CORKF.h-6, 3, CHROME[3]);
    circle(ctx, WHEEL.cx, WHEEL.cy, 20, CHROME[1]);
    blit(ctx, _wheel, WHEEL.cx, WHEEL.cy, ANG.wheel(steer));

    // ---- builder's plate (relocated low-centre, clear of the compass) ----
    nameplate(ctx, NAME);

    ctx.restore();
  }

  const _key   = buildKey();
  const _wheel = buildWheel(WHEEL.r);
  function render(o){ const c = cv(W,H); paint(c.getContext('2d'), o); return c; }

  root.CapeRig = {
    W, H, TOPPAD, DEG, dir, maxSteer:45, wheelTurn:150, ANG,
    driveHandle, driveFromPoint, driveThrottle, driveGear,
    PANEL, CORKF, WHEEL, RPM, FUEL, NAME, SLOTS, SLOTW, SLOTY, SLOTH, BROWB, DEFAULT_LAYOUT, BANK, ROCK, IGN, BINN, DRIVE, COMPASS,
    SW: { start:IGN, deck:{ x:ROCK.colX[0], y:ROCK.row0, w:ROCK.w, h:ROCK.h },
          spot:{ x:ROCK.colX[1], y:ROCK.row0, w:ROCK.w, h:ROCK.h } },
    slotBox, paint, render,
    // hooks for the future separate rigs — set CapeRig.paintRadar / paintGps to
    // a function(ctx,x,y,w,h,{night,phase}) and the mount will host it:
    paintRadar: null, paintGps: null,
  };
})(typeof globalThis!=='undefined'?globalThis:window);
