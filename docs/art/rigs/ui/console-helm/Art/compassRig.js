// Compass — diegetic heading rig for Hidden Harbours. Its OWN separate rig
// (same render / paintInto contract as depthRig.js / fishRig.js) so any boat can
// equip it. Reads ONE parameter: the boat's heading in degrees (0..359, 0=N,
// clockwise) — the SAME canon the iso hulls use (N NE E SE S SW W NW / 45deg).
//
// TWO real units, one shared procedural card:
//   'dome'  — black bracket/binnacle globe compass. Sits ON TOP of the dash
//             (crown, centreline) — the unit that REPLACES a brow instrument
//             cluster. Dark card, white numerals, red north.
//   'flush' — white Ritchie-style flush-mount. Sinks INTO the dash face (a round
//             cutout like the gauges) — fit this when the player wants the three
//             glass instruments up top instead. Cream card, black numerals.
//
// The card is direct-reading: it stays aligned to magnetic north while the case
// (and its fixed RED lubber line at 12 o'clock) turns with the boat, so the
// heading always reads at the lubber. NIGHT floods the dome/glass RED (the bulb),
// wired from the helm's existing night flag. Brand wordmark is original.
(function (root) {
  const DEG = Math.PI / 180;
  const W = 340, H = 366;   // standalone canvas; each form is centred within

  // ---- palettes -------------------------------------------------------------
  const BLK   = ['#05080a','#0b1013','#141b20','#212b31','#33414a','#4a5a64'];  // black body ramp
  const CHR   = ['#1e242a','#3a454d','#647079','#9fb0b6','#cdd9db','#eff6f6'];  // stainless / knobs
  const WHT   = ['#8f9a99','#aab4b2','#c6cfcb','#dde4de','#eef2ec','#fbfdfa'];  // white gelcoat ramp
  const RED   = ['#2a0b08','#6e1f16','#b3372a','#e0554a','#ff6f5c','#ffb2a6'];  // red north / night bulb
  const DARKF = { face:'#0a0f14', ring:'#04070a', ink:'#e9f0f2', dim:'#7d949e', n:'#e0554a', rose:'#cfe0e6' }; // dome card
  const LITEF = { face:'#e7ece3', ring:'#c2cabf', ink:'#1a2226', dim:'#5c6b6a', n:'#c23a2b', rose:'#2a343a' }; // flush card

  // ---- 3x5 bitmap font (only what the card needs) ---------------------------
  const F = {
    'N':['#.#','##.','###','.##','#.#'],'E':['###','#..','##.','#..','###'],
    'S':['.##','#..','.#.','..#','##.'],'W':['#.#','#.#','###','###','#.#'],
    '0':['###','#.#','#.#','#.#','###'],'1':['.#.','##.','.#.','.#.','###'],
    '2':['##.','..#','.#.','#..','###'],'3':['##.','..#','.#.','..#','##.'],
    '4':['#.#','#.#','###','..#','..#'],'5':['###','#..','##.','..#','##.'],
    '6':['.##','#..','##.','#.#','.#.'],'7':['###','..#','.#.','.#.','.#.'],
    '8':['.#.','#.#','.#.','#.#','.#.'],'9':['.#.','#.#','.##','..#','##.'],
    ' ':['...','...','...','...','...'],
  };
  function textW(str, s){ return String(str).length * (3*s + s) - s; }
  function text(ctx, str, x, y, s, col){
    ctx.fillStyle = col; str = String(str).toUpperCase(); let cx = x;
    for (const ch of str){ const g = F[ch] || F[' '];
      for (let r=0;r<5;r++) for (let c=0;c<3;c++) if (g[r][c]==='#') ctx.fillRect(cx+c*s, y+r*s, s, s);
      cx += 3*s + s; }
    return cx - s;
  }
  function textC(ctx, str, cx, cy, s, col){ text(ctx, str, Math.round(cx - textW(str,s)/2), Math.round(cy - (5*s)/2), s, col); }

  // ---- crisp primitives -----------------------------------------------------
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
  function screw(ctx, cx, cy, r, ramp){ ramp=ramp||CHR;
    circle(ctx, cx, cy, r, ramp[0]); circle(ctx, cx, cy, r-1, ramp[2]); circle(ctx, cx-1, cy-1, Math.max(1,r-2), ramp[3]);
    ctx.fillStyle=ramp[0]; ctx.fillRect(cx-r+1, cy, 2*r-1, 1);
  }
  const dir = (a)=>({ x:Math.sin(a), y:-Math.cos(a) });
  function blit(ctx, part, x, y, ang){
    ctx.save(); ctx.translate(Math.round(x), Math.round(y)); if(ang) ctx.rotate(ang*DEG); ctx.imageSmoothingEnabled=false;
    ctx.drawImage(part.c, -part.px, -part.py); ctx.restore();
  }

  // ---- heading helpers (canon: 0=N clockwise) -------------------------------
  const C8 = ['N','NE','E','SE','S','SW','W','NW'];
  const norm = (h)=>{ h=((h%360)+360)%360; return h; };
  const cardinal8 = (h)=> C8[Math.round(norm(h)/45)%8];
  const fmtDeg = (h)=>{ const d=Math.round(norm(h)); return (d<10?'00':d<100?'0':'')+d; };

  // ---- the rotating card (baked once per face, blitted rotated by -heading) --
  // Card-local: North sits UP (angle 0). A mark at card-direction d is placed at
  // dir(d) — after the sprite is rotated by -heading the mark's screen angle is
  // (d - heading), so the heading mark lands at the top lubber. Marine "by tens"
  // numbering (30->3, 60->6 ...) with letters at the 8 points, red north rose.
  function buildCard(r, fc){
    const S=(r+2)*2, c=cv(S,S), g=c.getContext('2d'), cx=S/2, cy=S/2;
    circle(g, cx, cy, r, fc.ring);
    circle(g, cx, cy, r-1, fc.face);
    // degree graduations
    for (let d=0; d<360; d+=5){
      const a=d*DEG, o=dir(a), major=(d%30===0), mid=(d%15===0);
      const rO=r-3, rI=major? r-11 : mid? r-8 : r-6;
      const col = major ? fc.ink : fc.dim;
      thickLine(g, cx+o.x*rI, cy+o.y*rI, cx+o.x*rO, cy+o.y*rO, major?2:1, col);
    }
    // "by tens" numbers between the letters
    [[30,'3'],[60,'6'],[120,'12'],[150,'15'],[210,'21'],[240,'24'],[300,'30'],[330,'33']].forEach(([d,lab])=>{
      const o=dir(d*DEG), rr=r-20; textC(g, lab, cx+o.x*rr, cy+o.y*rr, 2, fc.ink);
    });
    // intercardinal letters
    [[45,'NE'],[135,'SE'],[225,'SW'],[315,'NW']].forEach(([d,lab])=>{
      const o=dir(d*DEG), rr=r-19; textC(g, lab, cx+o.x*rr, cy+o.y*rr, 2, fc.dim);
    });
    // cardinal letters (N red)
    [[0,'N',fc.n],[90,'E',fc.ink],[180,'S',fc.ink],[270,'W',fc.ink]].forEach(([d,lab,col])=>{
      const o=dir(d*DEG), rr=r-21; textC(g, lab, cx+o.x*rr, cy+o.y*rr, 3, col);
    });
    // compass rose — long N/S needle, short E/W, N half red
    const rr=r-30;
    const N=dir(0), Sd=dir(180), Ed=dir(90), Wd=dir(270);
    g.fillStyle=fc.rose;                                   // W/E short diamond
    g.beginPath(); g.moveTo(cx+Wd.x*rr*0.5, cy+Wd.y*rr*0.5); g.lineTo(cx, cy-4); g.lineTo(cx+Ed.x*rr*0.5, cy+Ed.y*rr*0.5); g.lineTo(cx, cy+4); g.closePath(); g.fill();
    g.fillStyle=fc.rose;                                   // S half (neutral)
    g.beginPath(); g.moveTo(cx, cy+Sd.y*rr); g.lineTo(cx-5, cy); g.lineTo(cx+5, cy); g.closePath(); g.fill();
    g.fillStyle=fc.n;                                      // N half (red)
    g.beginPath(); g.moveTo(cx, cy+N.y*rr); g.lineTo(cx-5, cy); g.lineTo(cx+5, cy); g.closePath(); g.fill();
    circle(g, cx, cy, 3, fc.ink); circle(g, cx, cy, 2, fc.face);
    return { c, px:cx, py:cy };
  }

  // baked cards (per face, one radius each)
  const CARD_R = 96;
  let _cardDark=null, _cardLite=null;
  const cardDark = ()=> _cardDark || (_cardDark=buildCard(CARD_R, DARKF));
  const cardLite = ()=> _cardLite || (_cardLite=buildCard(CARD_R, LITEF));

  // Draw a live card into a circular bezel: rotate to heading, add fixed lubber
  // + glass + (optional) red night flood. cx,cy,r are the glass centre/radius.
  function liveCard(ctx, cx, cy, r, heading, night, fc, fore){
    fore = fore || 1;                                      // <1 = tilted (viewed at a slope)
    const card = fc==='lite' ? cardLite() : cardDark();
    ctx.save();
    // orthographic tilt: rotate the card in its own plane FIRST, then squash the
    // whole bowl vertically about the centre so a straight-on card reads as a slope.
    if (fore!==1){ ctx.translate(cx, cy); ctx.scale(1, fore); ctx.translate(-cx, -cy); }
    ctx.beginPath(); ctx.arc(cx, cy, r, 0, Math.PI*2); ctx.clip();
    const sc = r/CARD_R;
    ctx.save(); ctx.translate(cx, cy); ctx.rotate(-heading*DEG); ctx.scale(sc, sc); ctx.imageSmoothingEnabled=false;
    ctx.drawImage(card.c, -card.px, -card.py); ctx.restore();
    if (night){                                            // red bulb flood
      ctx.save(); ctx.globalCompositeOperation='lighter';
      const gr=ctx.createRadialGradient(cx, cy+r*0.15, 2, cx, cy, r*1.05);
      gr.addColorStop(0,'rgba(255,80,60,0.55)'); gr.addColorStop(0.55,'rgba(224,64,44,0.30)'); gr.addColorStop(1,'rgba(150,26,18,0.06)');
      ctx.fillStyle=gr; ctx.fillRect(cx-r, cy-r, r*2, r*2); ctx.restore();
    }
    // ---- domed glass enclosure (sells the convex, tilted dome) ----
    ctx.save(); ctx.globalCompositeOperation='lighter';
    const gg=ctx.createRadialGradient(cx-r*0.34, cy-r*0.44, 1, cx-r*0.34, cy-r*0.44, r*1.0);
    gg.addColorStop(0,'rgba(255,255,255,0.20)'); gg.addColorStop(0.5,'rgba(255,255,255,0.05)'); gg.addColorStop(1,'rgba(255,255,255,0)');
    ctx.fillStyle=gg; ctx.fillRect(cx-r, cy-r, r*2, r*2);
    ctx.beginPath(); ctx.ellipse(cx, cy, r*0.9, r*0.9, 0, Math.PI*0.86, Math.PI*1.42);          // upper-left rim highlight
    ctx.lineWidth=Math.max(1.5, r*0.055); ctx.strokeStyle='rgba(255,255,255,0.26)'; ctx.stroke();
    ctx.beginPath(); ctx.ellipse(cx, cy, r*0.9, r*0.9, 0, Math.PI*-0.12, Math.PI*0.30);         // lower-right reflected light
    ctx.lineWidth=Math.max(1, r*0.04); ctx.strokeStyle= night?'rgba(255,150,120,0.16)':'rgba(170,205,225,0.12)'; ctx.stroke();
    ctx.restore();
    ellipse(ctx, Math.round(cx-r*0.38), Math.round(cy-r*0.42), Math.max(1,Math.round(r*0.08)), Math.max(1,Math.round(r*0.05)), 'rgba(255,255,255,0.5)');  // hard glint
    ctx.restore();
    // fixed lubber line at the front (12 o'clock) — crisp, drawn outside the squash
    const ry = r*fore, lub = night ? RED[4] : RED[3];
    thickLine(ctx, cx, cy-ry+2, cx, cy-ry*0.34, 3, night?RED[1]:'rgba(120,20,14,0.6)');
    thickLine(ctx, cx, cy-ry+2, cx, cy-ry*0.34, 1, lub);
  }

  // ---- FLUSH unit (white Ritchie, sinks into the dash) ----------------------
  // Box (x,y,w,h): the unit centres in it; radius from the smaller half.
  function paintFlush(ctx, x, y, w, h, o){
    o=o||{}; const heading=norm(o.heading||0), night=!!o.night;
    const cx=Math.round(x+w/2), cy=Math.round(y+h/2), R=Math.floor(Math.min(w,h)/2)-2;
    // cutout shadow the ring sits in
    circle(ctx, cx, cy, R+3, '#04070a');
    // white gelcoat bezel ring
    ring(ctx, cx, cy, R+2, R-6, WHT[2]);
    ctx.save(); ctx.beginPath(); ctx.rect(cx-R-2, cy-R-2, (R+2)*2, R+2); ctx.clip();
    ring(ctx, cx, cy, R+2, R-6, WHT[4]); ctx.restore();          // lit top half
    ring(ctx, cx, cy, R+2, R+1, WHT[0]);                          // outer lip
    ring(ctx, cx, cy, R-6, R-7, BLK[1]);                          // inner black lip
    // four flush screws
    [45,135,225,315].forEach(d=>{ const o2=dir(d*DEG); screw(ctx, cx+o2.x*(R-2), cy+o2.y*(R-2), 2, CHR); });
    // north index notch on the bezel (front)
    thickLine(ctx, cx, cy-R-1, cx, cy-R+4, 3, night?RED[4]:RED[3]);
    liveCard(ctx, cx, cy, R-7, heading, night, 'lite');
    return { cx, cy, r:R };
  }

  // ---- DOME unit (black bracket/binnacle, sits ON TOP of the dash) ----------
  // Box (x,y,w,h): base pinned to bottom, globe rises to the top.
  function paintDome(ctx, x, y, w, h, o){
    o=o||{}; const heading=norm(o.heading||0), night=!!o.night, fore=0.72;
    const cx=Math.round(x+w/2);
    const gr=Math.floor(Math.min(w*0.42, h*0.40));        // card-plane radius
    const ry=Math.round(gr*fore);                         // foreshortened (tilted) vertical radius
    const baseH=Math.round(ry*1.4);                       // cone body height, tied to the globe
    const capB=Math.round(gr*0.5*fore);                   // foot ellipse depth (same slope as the dome)
    const unitH=10+2*ry+baseH+capB, top=Math.round(y+Math.max(0,(h-unitH)/2));   // centre a short box, cap a tall one
    const gy=top+10+ry;                                   // globe centre
    const gBot=gy+ry;
    // ---- binnacle base: a truncated cone seen at the SAME forward slope ----
    const baseTop=gBot-4, bodyH=baseH, footY=baseTop+bodyH;    // footY = foot-ellipse centre
    const rt=Math.round(gr*0.58), rb=Math.round(gr*0.98), capT=Math.round(rt*fore);
    const wAt=(i)=>{ const t=i/bodyH; return rt+(rb-rt)*(t*t*0.40+t*0.60); };  // cone radius per row
    ellipse(ctx, cx, footY+3, rb+3, capB+3, 'rgba(4,7,10,0.5)');               // deck shadow
    ellipse(ctx, cx, footY, rb, capB, BLK[0]);                                 // foot disc (lower half reads as the rounded front)
    ellipse(ctx, cx, footY-1, rb-2, Math.max(1,capB-1), BLK[1]);
    for(let i=0;i<bodyH;i++){ const rr=wAt(i), cxl=Math.round(cx-rr), ww=Math.round(rr*2);
      const lw=Math.max(2,Math.round(ww*0.16)), rw=Math.max(2,Math.round(ww*0.12));
      ctx.fillStyle=BLK[2]; ctx.fillRect(cxl, baseTop+i, ww, 1);
      ctx.fillStyle=BLK[3]; ctx.fillRect(cxl, baseTop+i, lw, 1);               // left key light
      ctx.fillStyle=BLK[0]; ctx.fillRect(cxl+ww-rw, baseTop+i, rw, 1);         // right shade
    }
    ellipse(ctx, cx, baseTop, rt, capT, BLK[3]);                               // tilted top surface
    ellipse(ctx, cx, baseTop-1, Math.max(1,rt-2), Math.max(1,capT-1), BLK[4]);
    rrect(ctx, cx-18, baseTop+Math.round(bodyH*0.55)-4, 36, 9, 2, BLK[1]);      // brand plaque
    ctx.fillStyle=BLK[4]; ctx.fillRect(cx-12, baseTop+Math.round(bodyH*0.55), 24, 1);
    screw(ctx, cx-Math.round(rb*0.66), footY-Math.round(capB*0.1), 2, CHR); screw(ctx, cx+Math.round(rb*0.66), footY-Math.round(capB*0.1), 2, CHR);  // deck bolts
    // ---- gimbal yoke arms (top-cap edges -> pivots at the globe's widest sides) ----
    const armR=gr+4;
    [-1,1].forEach(s=>{
      thickLine(ctx, cx+s*(rt-3), baseTop+2, cx+s*armR, gy, 7, BLK[2]);
      thickLine(ctx, cx+s*(rt-3), baseTop+2, cx+s*armR, gy, 2, BLK[4]);
    });
    // ---- tilted globe housing (elliptical) ----
    ellipse(ctx, cx, gy+2, gr+4, ry+4, '#04070a');        // seat shadow
    ellipse(ctx, cx, gy, gr+3, ry+3, BLK[3]);             // black rim
    ellipse(ctx, cx, gy, gr+2, ry+2, CHR[2]);             // stainless ring
    ctx.save(); ctx.beginPath(); ctx.rect(cx-gr-3, gy-ry-3, (gr+3)*2, ry+3); ctx.clip();
    ellipse(ctx, cx, gy, gr+2, ry+2, CHR[5]); ctx.restore();   // lit upper half of the ring
    ellipse(ctx, cx, gy, gr, ry, '#04070a');              // inner lip
    thickLine(ctx, cx, gy-ry-3, cx, gy-ry+3, 3, night?RED[4]:RED[3]);   // north index (far rim)
    liveCard(ctx, cx, gy, gr-2, heading, night, 'dark', fore);
    // ---- thumb-screw knobs on the pivots ----
    [-1,1].forEach(s=>{
      circle(ctx, cx+s*(armR+1), gy, 8, '#04070a');
      circle(ctx, cx+s*(armR+1), gy, 7, CHR[1]);
      circle(ctx, cx+s*armR, gy-1, 5, CHR[3]);
      for (let k=0;k<8;k++){ const a=k*45*DEG, o2=dir(a); ctx.fillStyle=CHR[0]; ctx.fillRect(Math.round(cx+s*(armR+1)+o2.x*7)-1, Math.round(gy+o2.y*7)-1, 2, 2); }
      circle(ctx, cx+s*(armR+1), gy, 2, CHR[5]);
    });
    if (night){                                           // outer red bloom around the dome
      ctx.save(); ctx.globalCompositeOperation='lighter';
      const bl=ctx.createRadialGradient(cx, gy, gr*0.5, cx, gy, gr*1.6);
      bl.addColorStop(0,'rgba(255,70,50,0.15)'); bl.addColorStop(1,'rgba(255,70,50,0)');
      ctx.fillStyle=bl; ctx.fillRect(cx-gr*1.6, gy-gr*1.6, gr*3.2, gr*3.2); ctx.restore();
    }
    return { cx, cy:gy, r:gr };
  }

  // ---- unified entry points -------------------------------------------------
  function paintInto(ctx, x, y, w, h, o){
    o=o||{}; return (o.form==='dome') ? paintDome(ctx, x, y, w, h, o) : paintFlush(ctx, x, y, w, h, o);
  }
  function paint(ctx, o){
    o=o||{}; const form=o.form||'dome'; ctx.clearRect(0,0,W,H);
    if (form==='dome') paintDome(ctx, 30, 12, W-60, H-24, o);
    else { const s=Math.min(W,H)-70; paintFlush(ctx, (W-s)/2, (H-s)/2, s, s, o); }
  }
  function render(o){ const c=cv(W,H); paint(c.getContext('2d'), o); return c; }

  root.CompassRig = {
    W, H, DEG, dir, CARD_R,
    paint, render, paintInto, paintDome, paintFlush, liveCard,
    cardinal8, fmtDeg, norm, C8,
  };
})(typeof globalThis!=='undefined'?globalThis:window);
