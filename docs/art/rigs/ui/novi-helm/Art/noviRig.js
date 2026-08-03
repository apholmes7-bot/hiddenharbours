// Novi Helm — diegetic control rig for the MODERN Nova Scotia lobster boat
// (the Cape Islander's contemporary sister; SAME pointer + render contract as
// capeRig.js / consoleRig.js so the game swaps them freely). This is the current
// downeast pilothouse dash: a moulded WHITE gelcoat console with a graphite
// instrument fascia, a stainless destroyer wheel with a black hub cap, two round
// BLACK-FACE gauges (RPM tach + FUEL) with crisp white ticks, ice-blue segmented
// value arcs and ice-blue needles, a bank of backlit rocker switches on a
// carbon-fibre strip (left), a black/stainless side-binnacle carrying ONE chrome
// single-lever throttle/shift control (push up=AHEAD, centre=NEUTRAL, pull
// down=ASTERN — shares Art/leverRig.js), and a brow of THREE equal edge-to-edge
// glass MFD mounts: a swappable DEPTH<->SONAR sounder plus TWO upgradeable
// screens for RADAR and GPS/plotter (own rigs later) exposed via NoviRig.paintRadar
// / paintGps. Brand wordmarks are original (no trademark).
(function (root) {
  const W = 600, TOPPAD = 54, H = 494 + TOPPAD;   // headroom so the portrait sonar reaches above the dash
  const DEG = Math.PI / 180;

  // ---- palettes (Novi: white gelcoat + graphite fascia + ice-blue accent) ---
  const GEL    = ['#7f8b8d','#98a4a5','#b6c0bf','#cdd6d3','#e2e8e3','#f2f5ef'];  // moulded white gelcoat console
  const GRAPH  = ['#0d1216','#151d22','#212c33','#33414a','#4a5a64','#69808b'];  // dark instrument fascia / recesses
  const CHROME = ['#1e242a','#3a454d','#647079','#9fb0b6','#cdd9db','#eff6f6'];  // polished stainless
  const STEEL  = ['#232a30','#39434b','#556069','#7a8892','#9db0b8'];            // screws
  const RUBBER = ['#0b0e11','#11161a','#1a2126','#252e35','#333f47'];            // black grips / caps
  const ICE    = ['#0a2632','#0f4a63','#1d7fa8','#3aa8d4','#6fccef','#a9e4ff'];  // ice-blue accent ramp
  const GBLK   = '#0b0f12', GBLK2 = '#141b20';                                   // black gauge dial
  const TKW    = '#e9f0f2', TKD = '#5c6b73';                                     // white ticks / dim ticks
  const SEGOFF = '#16242b';                                                       // unlit arc segment
  const RED    = ['#3f120d','#7c2a20','#b3372a','#e0554a','#f59183','#ffb7ac'];
  const GREEN  = ['#1c6a3b','#2f9e57','#66d585'];
  const AMBER  = '#e6b53f';                                                       // warning lamps only
  const GLASS  = '#050c0f';                                                       // dark screen glass
  const INK    = '#dfe8ea', INKDIM = '#7d949e';

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
  function rrPath(p, x, y, w, h, r){ p.moveTo(x+r,y); p.arcTo(x+w,y,x+w,y+h,r); p.arcTo(x+w,y+h,x,y+h,r); p.arcTo(x,y+h,x,y,r); p.arcTo(x,y,x+w,y,r); p.closePath(); }
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
  // KEPT dimensionally in step with capeRig so the shared DC pointer-picking and
  // the composited lever line up exactly across the two rigs.
  const SHELL = { x:12, y:34, w:576, h:452, r:22 };                 // moulded white console shell
  const DASH  = { x:26, y:48, w:548, h:424, r:14 };                 // gelcoat face inside the shell
  const WHEEL = { cx:300, cy:328, r:104 };
  const RPM   = { cx:92,  cy:214, r:50 };
  const FUEL  = { cx:508, cy:214, r:50 };
  const NAME  = { cx:300, cy:452, rx:78, ry:13 };
  // brow — THREE equal flush glass mounts (left / centre / right). Which
  // instrument sits in which slot is a caller parameter (o.layout).
  const SLOTW = 150, SLOTY = 18, SLOTH = 104, BROWB = 122;
  const SLOTS = [ { x:52 }, { x:225 }, { x:398 } ];
  const DEFAULT_LAYOUT = ['sounder','radar','gps'];
  // compass (its OWN rig, CompassRig): a black dome binnacle on the crown centre —
  // it rises into TOPPAD and DISPLACES the centre brow cluster — OR a white flush
  // Ritchie sunk into the dash face centre, so the brow keeps all three glass mounts.
  const COMPASS = { domeBox:{ x:226, y:-50, w:148, h:182 }, flushBox:{ x:260, y:132, w:80, h:80 } };
  function slotBox(i, portrait){ const s=SLOTS[i]||SLOTS[0];
    return portrait ? { x:s.x, y:BROWB-150, w:SLOTW, h:150 } : { x:s.x, y:SLOTY, w:SLOTW, h:SLOTH }; }
  // rocker switch bank (left) on a carbon strip, two columns
  const BANK  = { x:34, y:298, w:154, h:178, r:9 };
  const ROCK  = { w:56, h:18, colX:[BANK.x+16, BANK.x+82], row0:322, dy:27, rows:6 };
  // push-to-start / key switch (by the throttle)
  const IGN   = { cx:400, cy:454, r:17, off:-22, run:30 };
  // black/stainless side-binnacle housing + the single-lever pivot
  const BINN  = { x:430, y:296, w:154, h:180, r:14 };
  const DRIVE = { px:507, pivotY:456, hitR:46 };
  const LEVER = 'chrome';

  // ---- gauges (black face, white ticks, ice-blue segmented arc + needle) -----
  function gaugePod(ctx, g){
    const w=g.r*2+30, h=g.r*2+30, x=g.cx-w/2, y=g.cy-h/2;
    rrect(ctx, x+2, y+3, w, h, 15, 'rgba(4,8,10,0.5)');            // drop shadow
    rrect(ctx, x, y, w, h, 15, GRAPH[1]);
    rrect(ctx, x+1, y+1, w-2, h-2, 14, GRAPH[2]);
    rrect(ctx, x+3, y+3, w-6, 5, 12, GRAPH[3]);                   // top sheen
    rrect(ctx, x+3, y+h-4, w-6, 3, 2, GRAPH[0]);
    screw(ctx, x+8, y+8, 2, CHROME); screw(ctx, x+w-8, y+8, 2, CHROME);
    screw(ctx, x+8, y+h-8, 2, CHROME); screw(ctx, x+w-8, y+h-8, 2, CHROME);
  }
  function gaugeBezel(ctx, g){
    circle(ctx, g.cx, g.cy, g.r+6, RUBBER[0]);
    ring(ctx, g.cx, g.cy, g.r+5, g.r+2, CHROME[2]);               // slim brushed ring
    ctx.save(); ctx.beginPath(); ctx.rect(g.cx-g.r-6, g.cy-g.r-6, (g.r+6)*2, g.r+3); ctx.clip();
    ring(ctx, g.cx, g.cy, g.r+5, g.r+2, CHROME[5]); ctx.restore();
    ring(ctx, g.cx, g.cy, g.r+1, g.r, RUBBER[0]);                 // black inner lip
    circle(ctx, g.cx, g.cy, g.r, GBLK2);                          // black dial
    circle(ctx, g.cx, g.cy, g.r-1, GBLK);
    ctx.fillStyle = 'rgba(255,255,255,0.05)'; ctx.fillRect(g.cx-g.r+5, g.cy-g.r+6, g.r, 2);
  }
  function radialTick(ctx, g, deg, rOut, rIn, w, col){
    const d = dir(deg*DEG); thickLine(ctx, g.cx+d.x*rIn, g.cy+d.y*rIn, g.cx+d.x*rOut, g.cy+d.y*rOut, w, col);
  }
  function drawNeedle(ctx, g, px, py, tx, ty){
    const dx=tx-px, dy=ty-py, L=Math.hypot(dx,dy)||1, ux=dx/L, uy=dy/L, tail=Math.min(16, L*0.32);
    thickLine(ctx, px, py, px-ux*tail, py-uy*tail, 4, '#0a0d0f');          // black counterweight
    thickLine(ctx, px, py, tx, ty, 3, ICE[3]);                            // ice shaft
    thickLine(ctx, px, py, px+ux*(L-2), py+uy*(L-2), 1, '#eaf7ff');        // hot core
    const hr=Math.round(g.r*0.15)+1;
    circle(ctx, px, py, hr, CHROME[1]); circle(ctx, px, py, hr-1, CHROME[3]); circle(ctx, px-1, py-1, Math.max(1,hr-3), CHROME[5]);
  }
  function gaugeRPM(ctx, g, rpm){
    gaugePod(ctx, g); gaugeBezel(ctx, g);
    const N=32;
    for (let i=0;i<=N;i++){ const v=i/N, deg=-135+270*v, red=v>=0.84, lit=v<=rpm+0.001;
      radialTick(ctx, g, deg, g.r-3, g.r-7, 2, lit ? (red?RED[3]:ICE[4]) : (red?'#3a1712':SEGOFF)); }
    for (let n=0;n<=6;n++){ const v=n/6, d=dir((-135+270*v)*DEG), rr=g.r-20;
      textC(ctx, String(n), g.cx+d.x*rr, g.cy+d.y*rr-2, 1, n>=5?RED[4]:TKW); }
    textC(ctx, 'X1000', g.cx, g.cy-8, 1, TKD);
    textC(ctx, 'RPM',  g.cx, g.cy+g.r-18, 1, ICE[4]);
    const rd=dir(ANG.rpm(rpm)*DEG), len=g.r-12;
    drawNeedle(ctx, g, g.cx, g.cy, g.cx+rd.x*len, g.cy+rd.y*len);
  }
  function gaugeFUEL(ctx, g, fuel, low, blink){
    gaugePod(ctx, g); gaugeBezel(ctx, g);
    const drop=Math.round(g.r*0.42), py=g.cy+drop, rimR=g.r-6, N=24;
    for (let i=0;i<=N;i++){ const v=i/N, deg=fuelPhi(v), lowseg=v<=0.12, lit=v<=fuel+0.001;
      radialTick(ctx, g, deg, g.r-3, g.r-7, 2, lit ? (lowseg?RED[3]:ICE[4]) : (lowseg?'#3a1712':SEGOFF)); }
    { const d=dir(fuelPhi(0)*DEG),   rr=g.r-15; textC(ctx, 'E', g.cx+d.x*rr, g.cy+d.y*rr-3, 1, RED[4]); }
    { const d=dir(fuelPhi(1)*DEG),   rr=g.r-15; textC(ctx, 'F', g.cx+d.x*rr, g.cy+d.y*rr-3, 1, TKW); }
    { const d=dir(fuelPhi(0.5)*DEG), rr=g.r-15; ctx.fillStyle=TKW; ctx.fillRect(g.cx+d.x*rr-1, g.cy+d.y*rr-2, 2, 4); }
    textC(ctx, 'FUEL', g.cx, g.cy+g.r-18, 1, ICE[4]);
    circle(ctx, g.cx, g.cy-13, 3, (low && blink) ? AMBER : '#2a3942');
    const d=dir(fuelPhi(fuel)*DEG);
    drawNeedle(ctx, g, g.cx, py, g.cx+d.x*rimR, g.cy+d.y*rimR);
  }
  function gaugeNight(ctx, g){
    ctx.save(); ctx.beginPath(); ctx.arc(g.cx, g.cy, g.r-1, 0, Math.PI*2); ctx.clip();
    ctx.globalCompositeOperation = 'lighter';
    const gr = ctx.createRadialGradient(g.cx, g.cy, 2, g.cx, g.cy, g.r);
    gr.addColorStop(0,'rgba(111,204,239,0.38)'); gr.addColorStop(0.6,'rgba(58,168,212,0.18)'); gr.addColorStop(1,'rgba(29,127,168,0.05)');
    ctx.fillStyle = gr; ctx.fillRect(g.cx-g.r, g.cy-g.r, g.r*2, g.r*2); ctx.restore();
  }

  // ---- stainless destroyer wheel + black hub cap ----------------------------
  function buildWheel(r){
    const pad = 4, S = (r+pad)*2, c = cv(S,S), g = c.getContext('2d');
    const cx = S/2, cy = S/2, tw = Math.round(r*0.12)+4;
    ring(g, cx, cy, r, r-tw, CHROME[1]);
    ring(g, cx, cy, r-1, r-tw+1, CHROME[3]);
    g.save(); g.beginPath(); g.rect(cx-r, cy-r, r*2, r); g.clip();
    ring(g, cx, cy, r-1, r-tw+2, CHROME[5]); g.restore();
    ring(g, cx, cy, r, r-1, CHROME[0]);
    ring(g, cx, cy, r-tw+1, r-tw, CHROME[0]);
    const sw = Math.round(r*0.10)+1, ri = r-tw+1, ro = Math.round(r*0.24);
    [90,210,330].forEach(deg=>{ const d=dir(deg*DEG);
      thickLine(g, cx+d.x*ro, cy+d.y*ro, cx+d.x*ri, cy+d.y*ri, sw, CHROME[2]);
      thickLine(g, cx+d.x*ro, cy+d.y*ro, cx+d.x*(ri-1), cy+d.y*(ri-1), 2, CHROME[5]); });
    // hub — stainless collar with a matte black cap + ice centre dot
    circle(g, cx, cy, ro+3, CHROME[0]); circle(g, cx, cy, ro, CHROME[2]); circle(g, cx-2, cy-2, ro-2, CHROME[4]);
    circle(g, cx, cy, ro-3, RUBBER[1]); circle(g, cx-1, cy-2, ro-6, RUBBER[3]);
    circle(g, cx, cy, 3, ICE[3]); circle(g, cx, cy, 2, ICE[5]);
    return { c, px:cx, py:cy };
  }

  // ---- single throttle/shift lever hooks (Art/leverRig.js, chrome spec) -----
  const _lever = ()=> (typeof window!=='undefined') && window.LeverRig;
  function driveHandle(sig){ const R=_lever(); if(!R) return { x:DRIVE.px, y:DRIVE.pivotY-80 }; const o=R.handleOffset(sig, LEVER); return { x:DRIVE.px+o.dx, y:DRIVE.pivotY+o.dy }; }
  function driveFromPoint(x, y){ const R=_lever(); return R ? R.sigFromOffset(x-DRIVE.px, y-DRIVE.pivotY, LEVER) : 0; }
  const driveThrottle = (sig)=> Math.min(1, Math.abs(sig));
  const driveGear = (sig)=> sig < -0.04 ? 'R' : 'F';
  function driveHub(ctx){
    circle(ctx, DRIVE.px, DRIVE.pivotY, 20, '#04070a');
    circle(ctx, DRIVE.px, DRIVE.pivotY, 18, CHROME[2]);
    circle(ctx, DRIVE.px-1, DRIVE.pivotY-2, 15, CHROME[4]);
    circle(ctx, DRIVE.px, DRIVE.pivotY, 12, RUBBER[1]);
    ring(ctx, DRIVE.px, DRIVE.pivotY, 20, 18, '#04070a');
    screw(ctx, DRIVE.px, DRIVE.pivotY, 3, CHROME);
  }

  // ---- carbon-fibre twill + backlit rocker switch ---------------------------
  function carbon(ctx, x, y, w, h, r){
    rrect(ctx, x, y, w, h, r, '#0a0d10');
    ctx.save(); ctx.beginPath(); const p=new Path2D(); rrPath(p, x, y, w, h, r); ctx.clip(p);
    for (let yy=y; yy<y+h; yy+=4) for (let xx=x; xx<x+w; xx+=4){
      const cell=((Math.floor((xx-x)/4)+Math.floor((yy-y)/4))%2)===0;
      ctx.fillStyle = cell ? '#12181c' : '#0b0f12'; ctx.fillRect(xx, yy, 4, 4);
      ctx.fillStyle = 'rgba(150,170,180,0.10)';
      if (cell) ctx.fillRect(xx, yy, 2, 2); else ctx.fillRect(xx+2, yy+2, 2, 2);
    }
    ctx.restore();
  }
  function rocker(ctx, x, y, on, label){
    rrect(ctx, x-2, y-2, ROCK.w+4, ROCK.h+4, 4, '#04070a');       // gasket
    rrect(ctx, x, y, ROCK.w, ROCK.h, 3, RUBBER[0]);               // well
    rrect(ctx, x+1, y+1, ROCK.w-2, ROCK.h-2, 2, on?'#122029':'#171c20');  // convex paddle
    const lensH = Math.round(ROCK.h*0.52);
    rrect(ctx, x+3, y+2, ROCK.w-6, lensH-1, 1, on?ICE[2]:'#0e161b');       // legend lens
    if (on){ rrect(ctx, x+4, y+3, ROCK.w-8, 1, 0, ICE[4]);
      ctx.fillStyle='rgba(180,230,255,0.5)'; ctx.fillRect(x+ROCK.w-9, y+3, 3, lensH-3); }
    textC(ctx, label, x+ROCK.w/2, y+3, 1, on?'#eaf7ff':'#4a5a62');         // legend on the lens
    rrect(ctx, x+3, y+ROCK.h-5, ROCK.w-6, 3, 1, '#0a0e11');                // lower ridge
  }
  function breakerBank(ctx, deck, spot){
    rrect(ctx, BANK.x-3, BANK.y-3, BANK.w+6, BANK.h+6, BANK.r+2, 'rgba(4,8,10,0.55)');
    carbon(ctx, BANK.x, BANK.y, BANK.w, BANK.h, BANK.r);
    rrect(ctx, BANK.x+2, BANK.y+2, BANK.w-4, 2, 1, CHROME[3]);            // stainless top hairline
    screw(ctx, BANK.x+8, BANK.y+8, 2, CHROME); screw(ctx, BANK.x+BANK.w-8, BANK.y+8, 2, CHROME);
    screw(ctx, BANK.x+8, BANK.y+BANK.h-8, 2, CHROME); screw(ctx, BANK.x+BANK.w-8, BANK.y+BANK.h-8, 2, CHROME);
    const colA = ['DECK','BILGE','NAV','ANCH','PUMP','HORN'];
    const colB = ['SPOT','WIPE','CABIN','INST','ACC','VHF'];
    for (let i=0;i<ROCK.rows;i++){ const y=ROCK.row0 + i*ROCK.dy;
      rocker(ctx, ROCK.colX[0], y, i===0?deck:false, colA[i]);
      rocker(ctx, ROCK.colX[1], y, i===0?spot:false, colB[i]);
    }
  }

  // ---- ignition key switch --------------------------------------------------
  function buildKey(){
    const bowW=14, bowH=18, shaft=10, pad=6, w=bowW+pad*2, h=bowH+shaft+pad*2, c=cv(w,h), g=c.getContext('2d');
    const px=Math.round(w/2), py=h-4;
    thickLine(g, px, py, px, py-shaft, 4, CHROME[3]);
    thickLine(g, px-1, py, px-1, py-shaft, 1, CHROME[5]);
    const by = py-shaft-bowH;
    rrect(g, px-Math.round(bowW/2), by, bowW, bowH, 6, RUBBER[1]);
    rrect(g, px-Math.round(bowW/2)+1, by+1, bowW-2, Math.round(bowH*0.5), 5, RUBBER[3]);
    circle(g, px, by+Math.round(bowH*0.5), 4, '#04070a'); circle(g, px, by+Math.round(bowH*0.5), 3, RUBBER[2]);
    return { c, px, py };
  }
  function drawIgnition(ctx, s, running){
    circle(ctx, s.cx, s.cy, s.r+2, '#04070a');
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

  // ---- edge-to-edge glass MFD mount (RADAR / GPS) ---------------------------
  // A thin glossy black glass bezel is ALWAYS drawn (this IS the reserved space).
  // fitted -> a standby MFD placeholder (dim ice UI + label + LED), ready for the
  //           future RadarRig / GpsRig to paintInto the glass box.
  // !fitted -> a flush black-glass blanking screen (no display fitted).
  function glassBezel(ctx, b){
    rrect(ctx, b.x-4, b.y-4, b.w+8, b.h+8, 7, '#04070a');            // outer shadow
    rrect(ctx, b.x-3, b.y-3, b.w+6, b.h+6, 6, '#0a0e12');            // black glass frame
    rrect(ctx, b.x-3, b.y-3, b.w+6, 3, 4, '#1b2228');               // frame top sheen
    ctx.fillStyle='rgba(159,176,182,0.20)'; ctx.fillRect(b.x-1, b.y-2, 12, 1);  // brand hairline
  }
  function screenMount(ctx, b, kind, fitted, night, phase, painter){
    glassBezel(ctx, b);
    if (!fitted){
      rrect(ctx, b.x, b.y, b.w, b.h, 4, '#06090c');
      rrect(ctx, b.x+2, b.y+2, b.w-4, b.h-4, 3, GLASS);
      ctx.fillStyle='rgba(255,255,255,0.03)'; ctx.fillRect(b.x+3, b.y+3, b.w-6, 2);
      textC(ctx, kind, b.cx!=null?b.cx:b.x+b.w/2, b.y+b.h/2-9, 2, '#27323b');
      textC(ctx, 'NO DISPLAY FITTED', b.x+b.w/2, b.y+b.h/2+7, 1, '#1e2c34');
      circle(ctx, b.x+b.w-8, b.y+7, 2, '#20303a');
      return;
    }
    rrect(ctx, b.x, b.y, b.w, b.h, 4, '#02070a');
    rrect(ctx, b.x+2, b.y+2, b.w-4, b.h-4, 3, GLASS);
    if (painter){ painter(ctx, b.x+2, b.y+2, b.w-4, b.h-4, { night, phase }); }
    else {
      const cx=b.x+b.w/2, cy=b.y+b.h/2, base = night ? '#123a4e' : '#0f3346';
      const acc = night ? ICE[3] : ICE[4];
      if (kind==='RADAR'){
        for (let i=1;i<=3;i++) ring(ctx, cx, cy+6, i*Math.round(b.h*0.16), i*Math.round(b.h*0.16)-1, base);
        const sw = (phase||0)*0.9, d = dir(sw); ctx.save(); ctx.globalAlpha=0.55;
        thickLine(ctx, cx, cy+6, cx+d.x*b.h*0.42, cy+6+d.y*b.h*0.42, 2, acc); ctx.restore();
      } else {
        ctx.fillStyle=base;
        for (let gx=b.x+10; gx<b.x+b.w-8; gx+=14) ctx.fillRect(gx, b.y+8, 1, b.h-16);
        for (let gy=b.y+10; gy<b.y+b.h-8; gy+=13) ctx.fillRect(b.x+8, gy, b.w-16, 1);
        thickLine(ctx, cx-14, cy+8, cx+2, cy-6, 2, acc);
        thickLine(ctx, cx+2, cy-6, cx+16, cy+2, 2, acc);
        circle(ctx, cx+16, cy+2, 2, ICE[5]);
      }
      ctx.save(); ctx.globalCompositeOperation='lighter'; ctx.fillStyle = night?'rgba(111,204,239,0.09)':'rgba(58,168,212,0.08)';
      ctx.fillRect(b.x+2, b.y+2, b.w-4, b.h-4); ctx.restore();
      textC(ctx, kind, cx, b.y+8, 2, ICE[5]);
      textC(ctx, 'STANDBY \u00b7 UPGRADEABLE', cx, b.y+b.h-13, 1, night?ICE[2]:ICE[1]);
    }
    ctx.fillStyle='rgba(255,255,255,0.05)'; ctx.fillRect(b.x+3, b.y+3, b.w-6, 2);   // glare
    circle(ctx, b.x+b.w-8, b.y+7, 2, night?ICE[3]:ICE[4]);                          // LED
  }

  // ---- brushed stainless nameplate ------------------------------------------
  function nameplate(ctx, n){
    const w=n.rx*2, h=n.ry*2+6, x=n.cx-n.rx, y=n.cy-n.ry-3;
    rrect(ctx, x-1, y+2, w+2, h, 4, 'rgba(4,8,10,0.5)');
    rrect(ctx, x, y, w, h, 4, CHROME[2]);
    ctx.save(); ctx.beginPath(); const p=new Path2D(); rrPath(p, x, y, w, h, 4); ctx.clip(p);
    for (let i=0;i<h;i+=2){ ctx.fillStyle='rgba(255,255,255,0.05)'; ctx.fillRect(x, y+i, w, 1); }  // brushed hairlines
    ctx.fillStyle='rgba(255,255,255,0.22)'; ctx.fillRect(x, y+1, w, Math.round(h*0.34));
    ctx.restore();
    rrect(ctx, x+1, y+h-3, w-2, 2, 1, CHROME[0]);
    textC(ctx, 'NOVI', n.cx, n.cy-6, 2, '#0f1519');
    textC(ctx, 'HARBOUR MARINE', n.cx, n.cy+5, 1, '#2a3138');
    circle(ctx, x+7, n.cy, 2, ICE[3]); circle(ctx, x+w-7, n.cy, 2, ICE[3]);
  }

  // ---- moulded gelcoat face + console shell ---------------------------------
  function dashFace(ctx){
    rrect(ctx, DASH.x, DASH.y, DASH.w, DASH.h, DASH.r, GEL[3]);
    ctx.save(); ctx.beginPath(); const p=new Path2D(); rrPath(p, DASH.x, DASH.y, DASH.w, DASH.h, DASH.r); ctx.clip(p);
    const gg=ctx.createLinearGradient(0, DASH.y, 0, DASH.y+DASH.h);
    gg.addColorStop(0,'rgba(255,255,255,0.24)'); gg.addColorStop(0.34,'rgba(255,255,255,0.04)');
    gg.addColorStop(0.7,'rgba(30,42,48,0.05)'); gg.addColorStop(1,'rgba(20,30,34,0.18)');
    ctx.fillStyle=gg; ctx.fillRect(DASH.x, DASH.y, DASH.w, DASH.h);
    // moulded reveal under the brow + faint side draft lines
    ctx.fillStyle='rgba(20,30,34,0.12)'; ctx.fillRect(DASH.x, BROWB+7, DASH.w, 1);
    ctx.fillStyle='rgba(255,255,255,0.14)'; ctx.fillRect(DASH.x, BROWB+8, DASH.w, 1);
    ctx.fillStyle='rgba(255,255,255,0.05)'; ctx.fillRect(DASH.x+3, DASH.y+3, 2, DASH.h-6);
    ctx.restore();
  }
  function consoleShell(ctx){
    rrect(ctx, SHELL.x, SHELL.y, SHELL.w, SHELL.h, SHELL.r, GEL[1]);
    rrect(ctx, SHELL.x+1, SHELL.y+1, SHELL.w-2, SHELL.h-2, SHELL.r-1, GEL[3]);
    rrect(ctx, SHELL.x+2, SHELL.y+2, SHELL.w-4, SHELL.h-4, SHELL.r-2, GEL[4]);
    rrect(ctx, SHELL.x+3, SHELL.y+3, SHELL.w-6, 4, SHELL.r-2, GEL[5]);           // top bevel highlight
    // graphite reveal groove around the inner face + ice cove hairline
    rrect(ctx, DASH.x-4, DASH.y-4, DASH.w+8, DASH.h+8, DASH.r+3, GRAPH[1]);
    rrect(ctx, DASH.x-3, DASH.y-3, DASH.w+6, DASH.h+6, DASH.r+2, GRAPH[3]);
    rrect(ctx, DASH.x-3, DASH.y-3, DASH.w+6, 1, 0, 'rgba(58,168,212,0.55)');
    screw(ctx, SHELL.x+11, SHELL.y+11, 2, CHROME); screw(ctx, SHELL.x+SHELL.w-11, SHELL.y+11, 2, CHROME);
    screw(ctx, SHELL.x+11, SHELL.y+SHELL.h-11, 2, CHROME); screw(ctx, SHELL.x+SHELL.w-11, SHELL.y+SHELL.h-11, 2, CHROME);
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

    // ---- console: white gelcoat shell + moulded face ----
    consoleShell(ctx);
    dashFace(ctx);

    // deck / spot working-light washes (cool white) + a faint ice night ambient
    if (deck){ ctx.save(); ctx.globalCompositeOperation='lighter'; ctx.globalAlpha=0.09;
      rrect(ctx, DASH.x, DASH.y, DASH.w, DASH.h, DASH.r, '#eaf6ff'); ctx.restore(); }
    if (spot){ ctx.save(); ctx.globalCompositeOperation='lighter'; ctx.globalAlpha=0.13;
      const gg=ctx.createRadialGradient(WHEEL.cx, 150, 20, WHEEL.cx, 150, 220);
      gg.addColorStop(0,'#f2f8ff'); gg.addColorStop(1,'rgba(242,248,255,0)'); ctx.fillStyle=gg;
      ctx.fillRect(DASH.x, DASH.y, DASH.w, DASH.h); ctx.restore(); }
    if (night){ ctx.save(); ctx.globalCompositeOperation='lighter'; ctx.globalAlpha=0.07;
      const gg=ctx.createLinearGradient(0, DASH.y+TOPPAD, 0, DASH.y+DASH.h);
      gg.addColorStop(0,'#6fccef'); gg.addColorStop(1,'rgba(111,204,239,0)'); ctx.fillStyle=gg;
      rrect(ctx, DASH.x, DASH.y, DASH.w, DASH.h, DASH.r, '#6fccef'); ctx.restore(); }

    const compass = (o.compass==='dome'||o.compass==='flush') ? o.compass : 'none';
    const heading = o.heading||0;


    // ---- brow: three player-swappable edge-to-edge glass mounts ----
    const layout = Array.isArray(o.layout) && o.layout.length===3 ? o.layout : DEFAULT_LAYOUT;
    const drawSounder=(b, fish)=>{
      glassBezel(ctx, b);
      if (fish && window.FishRig){ window.FishRig.paintInto(ctx, b.x, b.y, b.w, b.h, { depth:18.4, tempC:13.8, night, range:20,
        fish:o.fish || window.FishRig.defaultSchool(), fishID:true, phase:o.phase||0 }); }
      else if (window.DepthRig){ window.DepthRig.paintInto(ctx, b.x, b.y, b.w, b.h, { depth:18.4, ft:false, night, armed:true, alarm:3, tempC:13.8, blink }); }
      ctx.fillStyle='rgba(255,255,255,0.05)'; ctx.fillRect(b.x+3, b.y+3, b.w-6, 2);
      circle(ctx, b.x+b.w-8, b.y+7, 2, night?ICE[3]:ICE[4]);
    };
    for (let i=0;i<3;i++){ if (compass==='dome' && i===1) continue; const id=layout[i];
      if (id==='sounder'){ const fish=o.finder==='fish'; drawSounder(slotBox(i, fish), fish); }
      else if (id==='radar'){ const rp=(root.NoviRig && root.NoviRig.paintRadar) || (window.RadarRig && window.RadarRig.paintInto) || null; screenMount(ctx, slotBox(i,true), 'RADAR', radar, night, o.phase||0, rp); }
      else if (id==='gps'){ screenMount(ctx, slotBox(i,false), 'GPS', gps, night, o.phase||0, root.NoviRig && root.NoviRig.paintGps); }
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

    // ---- rocker switch bank (left) ----
    breakerBank(ctx, deck, spot);

    // ---- side-binnacle housing (black/stainless, right) ----
    rrect(ctx, BINN.x-2, BINN.y-2, BINN.w+4, BINN.h+4, BINN.r+1, '#04070a');
    rrect(ctx, BINN.x, BINN.y, BINN.w, BINN.h, BINN.r, GRAPH[1]);
    rrect(ctx, BINN.x+1, BINN.y+1, BINN.w-2, BINN.h-4, BINN.r-1, GRAPH[2]);
    rrect(ctx, BINN.x+3, BINN.y+3, BINN.w-6, 5, BINN.r-3, GRAPH[3]);
    rrect(ctx, BINN.x+3, BINN.y+BINN.h-7, BINN.w-6, 4, 3, '#04070a');
    screw(ctx, BINN.x+9, BINN.y+10, 2, CHROME); screw(ctx, BINN.x+BINN.w-9, BINN.y+10, 2, CHROME);
    screw(ctx, BINN.x+9, BINN.y+BINN.h-10, 2, CHROME); screw(ctx, BINN.x+BINN.w-9, BINN.y+BINN.h-10, 2, CHROME);
    // stainless throttle guide slot the lever rides in
    rrect(ctx, DRIVE.px-6, BINN.y+16, 12, BINN.h-34, 6, '#04070a');
    rrect(ctx, DRIVE.px-4, BINN.y+18, 8, BINN.h-38, 4, GRAPH[0]);
    // F / N / R detents engraved on the housing
    textC(ctx, 'F', 586, 316, 1, ICE[4]);
    textC(ctx, 'N', 586, 336, 1, INKDIM);
    textC(ctx, 'R', 586, 356, 1, RED[3]);

    // ---- ignition key (by the throttle) ----
    drawIgnition(ctx, IGN, running);

    // ---- lever hub (lever itself composited separately by the DC) ----
    driveHub(ctx);

    // ---- steering column + stainless wheel ----
    thickLine(ctx, WHEEL.cx, WHEEL.cy, WHEEL.cx, DASH.y+DASH.h-6, 18, CHROME[1]);
    thickLine(ctx, WHEEL.cx-5, WHEEL.cy+10, WHEEL.cx-5, DASH.y+DASH.h-6, 3, CHROME[3]);
    circle(ctx, WHEEL.cx, WHEEL.cy, 20, CHROME[1]);
    blit(ctx, _wheel, WHEEL.cx, WHEEL.cy, ANG.wheel(steer));

    // ---- builder's plate (relocated low-centre, clear of the compass) ----
    nameplate(ctx, NAME);

    ctx.restore();
  }

  const _key   = buildKey();
  const _wheel = buildWheel(WHEEL.r);
  function render(o){ const c = cv(W,H); paint(c.getContext('2d'), o); return c; }

  root.NoviRig = {
    W, H, TOPPAD, DEG, dir, maxSteer:45, wheelTurn:150, ANG,
    driveHandle, driveFromPoint, driveThrottle, driveGear,
    SHELL, DASH, WHEEL, RPM, FUEL, NAME, SLOTS, SLOTW, SLOTY, SLOTH, BROWB, DEFAULT_LAYOUT, BANK, ROCK, IGN, BINN, DRIVE, COMPASS,
    SW: { start:IGN, deck:{ x:ROCK.colX[0], y:ROCK.row0, w:ROCK.w, h:ROCK.h },
          spot:{ x:ROCK.colX[1], y:ROCK.row0, w:ROCK.w, h:ROCK.h } },
    slotBox, paint, render,
    // hooks for the future separate rigs — set NoviRig.paintRadar / paintGps to
    // a function(ctx,x,y,w,h,{night,phase}) and the mount will host it:
    paintRadar: null, paintGps: null,
  };
})(typeof globalThis!=='undefined'?globalThis:window);
