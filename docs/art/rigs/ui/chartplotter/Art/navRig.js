// Chartplotter — diegetic marine GPS/chart navigator for Hidden Harbours. The FOURTH
// helm instrument alongside the Depth Sounder, Fish Finder and Radar: SAME graphite
// case idiom + box-parameterised drawUnit(ctx,X,Y,WW,HH,o) contract, but a LANDSCAPE
// glass (real chartplotters are wide) that paints a live vector nautical chart of the
// same water as the paper "Chart of the Hidden Harbours" — Greywick, Coddle Cove, the
// Sunkers, the Drownded flats, the Banks, the Rips. It draws: graduated depth-area
// shading (pale shallows -> white deeps), land, rocks, lateral buoys, marked waypoints
// (POI), the previous track breadcrumb, a planned magenta route, and AIS-style traffic.
// TWO faces from one rig: a compact CONSOLE view (chart + slim data/route bars), and a
// MAXIMIZED view that reveals the advanced kit — a layer/tool rail, a waypoint & route
// manager column, a range/bearing measure line, a tide/current readout and a split
// depth-profile strip. Everything is a parameter; no sprite bakes but the depth field
// (chart base). Brand wordmark is original (GN-12 / HARBOUR / CHARTPLOTTER) — no trademark.
(function (root) {
  const CONSOLE_W = 760, CONSOLE_H = 480, MAX_W = 980, MAX_H = 648;
  const D = Math.PI / 180;
  const WORLD_W = 1120, WORLD_H = 860, PXNM = 50;   // world px per nautical mile

  // ---- graphite case shared with the fleet ---------------------------------
  const CASE  = ['#12171b','#1b2228','#28323a','#3a4750','#4f5e68','#6b7c86'];
  const RESIN = ['#050708','#0c1013','#141b1f'];
  const STEEL = ['#232a30','#39434b','#556069','#7a8892','#9db0b8'];
  const TEAL  = '#7fd6c9', TEAK = '#c9a86a', BLUE = '#5b93b8';

  // day = bright day-chart; night = the real chartplotter dusk palette (dark water)
  const DAY = {
    frame:'#0a1418', strip:'#0b1a22', stripFg:'#bfe6df', stripDim:'#4c6f74', stripHi:'#7fd6c9', plate:'rgba(6,16,20,0.6)',
    land:'#e6d3a0', landEdge:'#b28a4e', dry:'#b7cf92', dryDot:'#8a9a5e',
    water:['#6fb8cf','#98cee0','#bfe0ea','#ddeef3','#f1f7f8'],      // vs -> deep
    grid:'rgba(40,70,90,0.18)', label:'#33506a', labelDim:'#5f7f8c',
    route:'#d23ccf', routeDim:'#8a2f88', track:'#3f86c0',
    wpt:{mark:'#c2417a', anchor:'#2f6fae', fuel:'#e0902f', hazard:'#d8443a', dest:'#2f9e57'},
    buoy:{port:'#d8443a', stbd:'#2f9e57', card:'#e6b53f', safe:'#d8443a', special:'#e6b53f'},
    rock:'#2a4152', traffic:'#2f7d4a', trafficSel:'#e0554a',
    ship:'#16324a', shipEdge:'#f2f7f8', head:'#d8443a', current:'#2f6fae',
    railPlate:'#123038', railEdge:'#0a1a20', railLit:'#1d5a52', railTxt:'#bfe6df', railDim:'#4c6f74',
    profBg:'#0b1a22', profBed:'#1c3a2a', profBedEdge:'#2f9e57', profSky:'#0d2230', profTxt:'#7fd6c9',
    keyPlate:'#245a4a', keyEdge:'#123528', legend:'#dff3e6', mark:'#bfe6cf',
  };
  const NIGHT = {
    frame:'#0a0d10', strip:'#120c04', stripFg:'#ffd77a', stripDim:'#6b501c', stripHi:'#ffbe3d', plate:'rgba(10,6,2,0.62)',
    land:'#2a2413', landEdge:'#5a4a28', dry:'#233018', dryDot:'#3f5228',
    water:['#1c4256','#153446','#102a3a','#0c2130','#081824'],
    grid:'rgba(120,150,170,0.10)', label:'#8fb0c0', labelDim:'#5f7f8c',
    route:'#ff6fe0', routeDim:'#a83f98', track:'#7fb0ff',
    wpt:{mark:'#ff8fc0', anchor:'#6fa8e0', fuel:'#ffb84f', hazard:'#ff6f5c', dest:'#6fe0a0'},
    buoy:{port:'#ff6f5c', stbd:'#6fe0a0', card:'#ffd77a', safe:'#ff6f5c', special:'#ffd77a'},
    rock:'#6f8ea0', traffic:'#6fe0a0', trafficSel:'#ff8f7a',
    ship:'#cfe0ea', shipEdge:'#0a0d10', head:'#ff6f5c', current:'#6fa8e0',
    railPlate:'#241a06', railEdge:'#120c02', railLit:'#5a4210', railTxt:'#ffd77a', railDim:'#6b501c',
    profBg:'#0d0a04', profBed:'#2a2008', profBedEdge:'#ffbe3d', profSky:'#0a1420', profTxt:'#ffd77a',
    keyPlate:'#3a2c0c', keyEdge:'#201604', legend:'#ffe0a0', mark:'#ffd77a',
  };

  // ---- 3x5 bitmap font (shared idiom with the fleet) ------------------------
  const F = {
    'A':['.#.','#.#','###','#.#','#.#'],'B':['##.','#.#','##.','#.#','##.'],'C':['.##','#..','#..','#..','.##'],
    'D':['##.','#.#','#.#','#.#','##.'],'E':['###','#..','##.','#..','###'],'F':['###','#..','##.','#..','#..'],
    'G':['.##','#..','#.#','#.#','.##'],'H':['#.#','#.#','###','#.#','#.#'],'I':['###','.#.','.#.','.#.','###'],
    'J':['..#','..#','..#','#.#','.#.'],'K':['#.#','#.#','##.','#.#','#.#'],'L':['#..','#..','#..','#..','###'],
    'M':['#.#','###','###','#.#','#.#'],'N':['#.#','##.','###','.##','#.#'],'O':['.#.','#.#','#.#','#.#','.#.'],
    'P':['##.','#.#','##.','#..','#..'],'Q':['.#.','#.#','#.#','.#.','..#'],'R':['##.','#.#','##.','#.#','#.#'],
    'S':['.##','#..','.#.','..#','##.'],'T':['###','.#.','.#.','.#.','.#.'],'U':['#.#','#.#','#.#','#.#','.#.'],
    'V':['#.#','#.#','#.#','.#.','.#.'],'W':['#.#','#.#','###','###','#.#'],'X':['#.#','#.#','.#.','#.#','#.#'],
    'Y':['#.#','#.#','.#.','.#.','.#.'],'Z':['###','..#','.#.','#..','###'],
    '0':['###','#.#','#.#','#.#','###'],'1':['.#.','##.','.#.','.#.','###'],'2':['##.','..#','.#.','#..','###'],
    '3':['##.','..#','.#.','..#','##.'],'4':['#.#','#.#','###','..#','..#'],'5':['###','#..','##.','..#','##.'],
    '6':['.##','#..','##.','#.#','.#.'],'7':['###','..#','.#.','.#.','.#.'],'8':['.#.','#.#','.#.','#.#','.#.'],
    '9':['.#.','#.#','.##','..#','##.'],
    '-':['...','...','###','...','...'],'.':['...','...','...','...','.#.'],'/':['..#','..#','.#.','#..','#..'],
    ':':['...','.#.','...','.#.','...'],'\u00b0':['##.','##.','...','...','...'],'+':['...','.#.','###','.#.','...'],
    '\u2192':['...','..#','###','..#','...'],' ':['...','...','...','...','...'],
  };
  function textW(str, s){ return String(str).length * (3*s + s) - s; }
  function text(ctx, str, x, y, s, col){
    ctx.fillStyle = col; str = String(str).toUpperCase(); let cx = Math.round(x); y=Math.round(y);
    for (const ch of str){ const g = F[ch] || F[' '];
      for (let r=0;r<5;r++) for (let c=0;c<3;c++) if (g[r][c]==='#') ctx.fillRect(cx+c*s, y+r*s, s, s);
      cx += 3*s + s; }
    return cx - s;
  }
  function textR(ctx, str, xr, y, s, col){ return text(ctx, str, Math.round(xr-textW(str,s)), y, s, col); }
  function textC(ctx, str, cx, y, s, col){ return text(ctx, str, Math.round(cx-textW(str,s)/2), y, s, col); }

  // ---- crisp primitives -----------------------------------------------------
  const cv = (w,h)=>{ const c=document.createElement('canvas'); c.width=Math.round(w); c.height=Math.round(h); return c; };
  function rowInset(v, h, r){
    if (v < r)      { const dy=r-v;       return r - Math.floor(Math.sqrt(Math.max(0,r*r-dy*dy))); }
    if (v >= h - r) { const dy=v-(h-r)+1; return r - Math.floor(Math.sqrt(Math.max(0,r*r-dy*dy))); }
    return 0;
  }
  function rrect(ctx, x, y, w, h, r, fill){
    ctx.fillStyle = fill; r = Math.max(0, Math.min(r, Math.floor(Math.min(w,h)/2)));
    x=Math.round(x); y=Math.round(y); w=Math.round(w); h=Math.round(h);
    for (let v=0; v<h; v++){ const i=rowInset(v,h,r); ctx.fillRect(x+i, y+v, w-2*i, 1); }
  }
  function circle(ctx, cx, cy, rad, fill){
    ctx.fillStyle=fill; cx=Math.round(cx); cy=Math.round(cy); rad=Math.round(rad);
    for (let dy=-rad; dy<=rad; dy++){ const dx=Math.floor(Math.sqrt(Math.max(0,rad*rad-dy*dy))); ctx.fillRect(cx-dx, cy+dy, 2*dx+1, 1); }
  }
  function ring(ctx, cx, cy, rO, rI, fill){
    ctx.fillStyle=fill; cx=Math.round(cx); cy=Math.round(cy);
    for (let dy=-rO; dy<=rO; dy++){ const xo=Math.floor(Math.sqrt(Math.max(0,rO*rO-dy*dy)));
      if (Math.abs(dy)<rI){ const xi=Math.floor(Math.sqrt(Math.max(0,rI*rI-dy*dy)));
        ctx.fillRect(cx-xo, cy+dy, xo-xi, 1); ctx.fillRect(cx+xi, cy+dy, xo-xi, 1);
      } else ctx.fillRect(cx-xo, cy+dy, 2*xo+1, 1); }
  }
  function ringOutline(ctx, cx, cy, rad, fill){ ring(ctx, cx, cy, Math.round(rad), Math.round(rad)-1, fill); }
  function thickLine(ctx, x0, y0, x1, y1, w, fill){
    ctx.fillStyle=fill; const dx=x1-x0, dy=y1-y0, L=Math.max(1,Math.hypot(dx,dy)), n=Math.ceil(L);
    const px=-dy/L, py=dx/L, hw=(w-1)/2;
    for (let i=0;i<=n;i++){ const t=i/n, X=x0+dx*t, Y=y0+dy*t;
      for (let j=-hw;j<=hw;j++) ctx.fillRect(Math.round(X+px*j), Math.round(Y+py*j), 1, 1); }
  }
  function dashLine(ctx, x0, y0, x1, y1, w, fill, on, off){
    const dx=x1-x0, dy=y1-y0, L=Math.max(1,Math.hypot(dx,dy)); let d=0; const ux=dx/L, uy=dy/L;
    while (d<L){ const a=d, b=Math.min(L, d+on); thickLine(ctx, x0+ux*a, y0+uy*a, x0+ux*b, y0+uy*b, w, fill); d+=on+off; }
  }
  function poly(ctx, pts, close){ ctx.beginPath(); pts.forEach((p,i)=> i?ctx.lineTo(p[0],p[1]):ctx.moveTo(p[0],p[1])); if(close) ctx.closePath(); }
  function screw(ctx, cx, cy, r){ circle(ctx, cx, cy, r, STEEL[0]); circle(ctx, cx, cy, r-1, STEEL[2]); circle(ctx, cx-1, cy-1, Math.max(1,r-2), STEEL[3]); ctx.fillStyle=STEEL[0]; ctx.fillRect(cx-r+1, cy, 2*r-1, 1); }
  const dir = (a)=>({ x:Math.sin(a), y:-Math.cos(a) });
  const clamp = (v,a,b)=> v<a?a:(v>b?b:v);
  const norm360 = (h)=>{ h=((h%360)+360)%360; return h; };

  // ---- the world (lifted from the paper Chart of the Hidden Harbours) -------
  const LAND = [
    { name:'IRONBOUND', pts:[[196,132],[250,120],[300,138],[322,172],[296,200],[250,196],[214,178]] },
    { name:null,        pts:[[330,150],[360,144],[378,166],[360,186],[334,178]] },
    { name:'PORT GREYWICK', pts:[[200,556],[260,542],[326,548],[360,572],[392,560],[414,586],[404,614],[420,650],[396,686],[348,690],[286,700],[224,690],[196,658],[176,632],[178,580]] },
    { name:'CODDLE COVE', pts:[[560,552],[636,542],[712,552],[742,586],[740,676],[684,686],[672,650],[700,624],[698,598],[668,586],[624,592],[606,618],[616,652],[566,600]] },
    { name:'ST PETERS', pts:[[420,726],[470,712],[532,716],[566,740],[556,792],[516,808],[462,806],[414,780],[398,752]] },
    { name:null,        pts:[[1064,360],[1120,360],[1120,760],[1064,760],[1048,620],[1050,470]] },
  ];
  const DRY = [[236,408],[300,392],[372,398],[416,420],[410,486],[360,496],[300,506],[250,492],[226,462],[218,430]];
  const BANKS = { cx:536, cy:254, rx:96, ry:56 };
  const SUNKERS = [[510,430],[560,426],[640,432],[662,462],[630,492],[558,496],[514,478]];
  const REGIONS = [
    { t:'IRONBOUND', x:258, y:164, sub:'storm islands' },
    { t:'THE BANKS', x:536, y:254, sub:'shoal water' },
    { t:'THE SUNKERS', x:588, y:462, sub:'reefs awash' },
    { t:'DROWNDED LANDS', x:322, y:448, sub:'dries at low water' },
    { t:'PORT GREYWICK', x:290, y:626, sub:'home' },
    { t:'CODDLE COVE', x:648, y:640, sub:'anchorage' },
    { t:'FUNDY RIPS', x:492, y:392, sub:'the narrows' },
    { t:'THE SHIPPING LANES', x:860, y:250, sub:'to the mainland' },
  ];
  const ROCKS = [ [540,452],[576,440],[614,456],[598,478],[556,484],[630,438],[522,470],[648,468], [308,166],[352,168], [188,150] ];
  const BUOYS = [
    { x:388, y:600, kind:'port' }, { x:430, y:588, kind:'stbd' },        // Greywick entrance
    { x:470, y:520, kind:'port' }, { x:512, y:500, kind:'stbd' },        // channel out
    { x:560, y:520, kind:'stbd' }, { x:604, y:560, kind:'port' },        // into Coddle
    { x:664, y:452, kind:'card' },                                        // N-cardinal over the Sunkers
    { x:600, y:256, kind:'safe' }, { x:700, y:270, kind:'special' },      // the lanes
    { x:800, y:288, kind:'special' }, { x:920, y:306, kind:'safe' },
    { x:486, y:706, kind:'port' }, { x:540, y:712, kind:'stbd' },         // St Peters opening
  ];

  function defaultWaypoints(){ return [
    { name:'FUEL',   x:312, y:640, kind:'fuel' },
    { name:'MARK 1', x:512, y:452, kind:'mark' },
    { name:'WRECK',  x:598, y:470, kind:'hazard' },
    { name:'ANCHOR', x:668, y:632, kind:'anchor' },
  ]; }
  function defaultTrack(){ return [
    [300,646],[326,616],[344,584],[356,548],[380,500],[404,462],[430,424],[452,392],[462,360],[470,336],
  ]; }
  function defaultRoute(){ return [
    { x:470, y:336 }, { x:512, y:452 }, { x:576, y:520 }, { x:648, y:592 }, { x:668, y:632 },
  ]; }
  function defaultTraffic(){ return [
    { name:'F/V DORY',   x:520, y:250, cog:196, sog:5.2, kind:'fish' },
    { name:'M/V ATLAS',  x:844, y:332, cog:248, sog:12.4, kind:'ship' },
    { name:'S/V TERN',   x:432, y:486, cog:44,  sog:4.1, kind:'sail' },
  ]; }
  const OWN = { x:470, y:336 };

  // ---- geometry helpers -----------------------------------------------------
  function inPoly(x, y, pts){ let inside=false; for(let i=0,j=pts.length-1;i<pts.length;j=i++){ const xi=pts[i][0],yi=pts[i][1],xj=pts[j][0],yj=pts[j][1];
    if (((yi>y)!==(yj>y)) && (x < (xj-xi)*(y-yi)/(yj-yi)+xi)) inside=!inside; } return inside; }
  function isLand(x,y){ for(const l of LAND) if(inPoly(x,y,l.pts)) return true; return false; }

  // ---- depth field + chart base (baked once per palette) --------------------
  let GRID=null;                                   // {g, gw, gh, G, depth}
  function buildGrid(){
    if (GRID) return GRID;
    const G=8, gw=Math.ceil(WORLD_W/G), gh=Math.ceil(WORLD_H/G);
    const land=new Uint8Array(gw*gh), dist=new Float32Array(gw*gh).fill(1e6);
    for (let gy=0;gy<gh;gy++) for (let gx=0;gx<gw;gx++){ const x=gx*G+G/2, y=gy*G+G/2;
      if (isLand(x,y)){ land[gy*gw+gx]=1; dist[gy*gw+gx]=0; } }
    const at=(gx,gy)=> (gx<0||gy<0||gx>=gw||gy>=gh)?1e6:dist[gy*gw+gx];
    for (let gy=0;gy<gh;gy++) for (let gx=0;gx<gw;gx++){ const i=gy*gw+gx; if(land[i]) continue;
      dist[i]=Math.min(dist[i], at(gx-1,gy)+1, at(gx,gy-1)+1, at(gx-1,gy-1)+1.414, at(gx+1,gy-1)+1.414); }
    for (let gy=gh-1;gy>=0;gy--) for (let gx=gw-1;gx>=0;gx--){ const i=gy*gw+gx; if(land[i]) continue;
      dist[i]=Math.min(dist[i], at(gx+1,gy)+1, at(gx,gy+1)+1, at(gx+1,gy+1)+1.414, at(gx-1,gy+1)+1.414); }
    const depth=new Float32Array(gw*gh);
    for (let gy=0;gy<gh;gy++) for (let gx=0;gx<gw;gx++){ const i=gy*gw+gx, x=gx*G+G/2, y=gy*G+G/2;
      if (land[i]){ depth[i]=-1; continue; }
      if (inPoly(x,y,DRY)){ depth[i]=0.3; continue; }
      let d = dist[i]*1.05 + 0.6;
      const bd=((x-BANKS.cx)/BANKS.rx)**2 + ((y-BANKS.cy)/BANKS.ry)**2; if (bd<1) d=Math.min(d, 2.2 + bd*3);
      if (inPoly(x,y,SUNKERS)) d=Math.min(d, 1.4);
      depth[i]=clamp(d, 0.4, 30);
    }
    GRID={ g:land, gw, gh, G, depth };
    return GRID;
  }
  function depthAt(x, y){ const G=buildGrid(); const gx=clamp(Math.floor(x/G.G),0,G.gw-1), gy=clamp(Math.floor(y/G.G),0,G.gh-1); return G.depth[gy*G.gw+gx]; }
  function bandOf(d){ return d<2?0 : d<5?1 : d<10?2 : d<18?3 : 4; }

  const BASE={};   // cache: BASE[night?1:0] = canvas
  function chartBase(night){
    const key=night?1:0; if (BASE[key]) return BASE[key];
    const P=night?NIGHT:DAY, G=buildGrid(), c=cv(WORLD_W, WORLD_H), ctx=c.getContext('2d');
    ctx.imageSmoothingEnabled=false;
    ctx.fillStyle=P.water[4]; ctx.fillRect(0,0,WORLD_W,WORLD_H);
    for (let gy=0;gy<G.gh;gy++) for (let gx=0;gx<G.gw;gx++){ const d=G.depth[gy*G.gw+gx]; if(d<0) continue;
      if (d<0.5){ ctx.fillStyle=P.dry; } else { ctx.fillStyle=P.water[bandOf(d)]; }
      ctx.fillRect(gx*G.G, gy*G.G, G.G, G.G); }
    // drying flats stipple
    ctx.fillStyle=P.dryDot; for(let i=0;i<70;i++){ const x=236+((i*61)%180), y=408+((i*97)%86); if(inPoly(x,y,DRY)) ctx.fillRect(x,y,1,1); }
    // land fills + coastline
    LAND.forEach(l=>{ poly(ctx,l.pts,true); ctx.fillStyle=P.land; ctx.fill(); ctx.strokeStyle=P.landEdge; ctx.lineWidth=1.5; ctx.stroke(); });
    // graticule
    ctx.fillStyle=P.grid; for(let x=0;x<WORLD_W;x+=PXNM) ctx.fillRect(x,0,1,WORLD_H); for(let y=0;y<WORLD_H;y+=PXNM) ctx.fillRect(0,y,WORLD_W,1);
    BASE[key]=c; return c;
  }

  // ---- view transform (world <-> screen) ------------------------------------
  function makeView(o, chart){
    const rng=+o.rangeNM||6, sc=chart.w/(rng*PXNM), phi=(o.orient==='head'?-(o.heading||0):0)*D;
    const cam=o.cam||OWN;
    return { sc, phi, cx:cam.x, cy:cam.y, ox:chart.x+chart.w/2, oy:chart.y+chart.h/2, cos:Math.cos(phi), sin:Math.sin(phi), chart };
  }
  function w2s(V, wx, wy){ const dx=wx-V.cx, dy=wy-V.cy; return { x:V.ox+(dx*V.cos-dy*V.sin)*V.sc, y:V.oy+(dx*V.sin+dy*V.cos)*V.sc }; }
  function s2w(V, sx, sy){ const ux=(sx-V.ox)/V.sc, uy=(sy-V.oy)/V.sc; return { x:V.cx+(ux*V.cos+uy*V.sin), y:V.cy+(-ux*V.sin+uy*V.cos) }; }

  // ---- layout (box-parameterised) -------------------------------------------
  function layout(X, Y, WW, HH, mode){
    const pad=Math.max(4,Math.round(WW*0.016)), brandH=Math.max(9,Math.round(HH*0.058)), colW=Math.max(30,Math.round(WW*0.072));
    const lcd={ x:X+pad, y:Y+pad, w:WW-pad*3-colW, h:HH-pad*2-brandH };
    const col={ x:X+WW-pad-colW, y:Y+pad, w:colW, h:lcd.h };
    const nKeys=4, gap=Math.max(2,Math.round(col.h*0.02)), knob=Math.round(col.w*0.9);
    const keyArea=col.h-knob-gap, keyH=Math.floor((keyArea-gap*(nKeys-1))/nKeys);
    const keys=[]; for(let i=0;i<nKeys;i++) keys.push({ x:col.x, y:col.y+i*(keyH+gap), w:col.w, h:keyH });
    const knobBox={ x:col.x, y:col.y+col.h-knob, w:col.w, h:knob };
    const brand={ x:X+pad, y:Y+HH-brandH-Math.round(pad*0.3), w:WW-pad*2, h:brandH };
    const topH=Math.max(16,Math.round(lcd.h*0.11)), botH=Math.max(13,Math.round(lcd.h*0.085));
    const topbar={ x:lcd.x, y:lcd.y, w:lcd.w, h:topH };
    const botbar={ x:lcd.x, y:lcd.y+lcd.h-botH, w:lcd.w, h:botH };
    const my=lcd.y+topH, mh=lcd.h-topH-botH;
    let rail=null, right=null, profile=null, railBtns=[], chart;
    if (mode==='max'){
      const railW=Math.max(30,Math.round(lcd.w*0.072)), rightW=Math.round(lcd.w*0.25), profH=Math.round(mh*0.24);
      rail={ x:lcd.x, y:my, w:railW, h:mh };
      right={ x:lcd.x+lcd.w-rightW, y:my, w:rightW, h:mh };
      chart={ x:lcd.x+railW, y:my, w:lcd.w-railW-rightW, h:mh-profH };
      profile={ x:lcd.x+railW, y:my+mh-profH, w:lcd.w-railW-rightW, h:profH };
      const ids=['LAND','ROCK','BUOY','POI','TRK','DPTH','TRFC','|','PAN','MRK','RTE','MSR'];
      const bh=Math.floor(rail.h/ids.length);
      ids.forEach((id,i)=>{ if(id!=='|') railBtns.push({ id, x:rail.x+2, y:rail.y+i*bh, w:rail.w-4, h:bh-1, tool:i>=8 }); });
    } else {
      chart={ x:lcd.x, y:my, w:lcd.w, h:mh };
    }
    chart.cx=chart.x+chart.w/2; chart.cy=chart.y+chart.h/2;
    return { pad, brandH, colW, lcd, col, keys, knobBox, brand, topbar, botbar, rail, right, profile, railBtns, chart, my, mh };
  }

  // ---- glyphs ---------------------------------------------------------------
  function ownShip(ctx, s, ang, P, sc){
    const r=Math.max(5, sc*0.14), a=ang*D, ca=Math.cos(a), sa=Math.sin(a);
    const pt=(fx,fy)=>({ x:s.x+(fx*ca-fy*sa), y:s.y+(fx*sa+fy*ca) });  // local: +y forward(up)=-screenY
    const hull=[pt(0,-r*1.4),pt(r*0.62,r*0.5),pt(r*0.34,r),pt(-r*0.34,r),pt(-r*0.62,r*0.5)];
    ctx.save(); poly(ctx,hull.map(p=>[p.x,p.y]),true); ctx.fillStyle=P.ship; ctx.fill(); ctx.lineWidth=1.4; ctx.strokeStyle=P.shipEdge; ctx.stroke(); ctx.restore();
    const hd=pt(0,-r*1.4); thickLine(ctx, s.x, s.y, hd.x, hd.y, 1, P.head);
  }
  function buoyGlyph(ctx, s, kind, P){
    const c=P.buoy[kind]||P.buoy.special, r=3;
    thickLine(ctx, s.x, s.y, s.x, s.y-8, 1, P.rock);
    if (kind==='card'){ ctx.fillStyle=c; ctx.fillRect(s.x-1,s.y-11,3,3); ctx.fillStyle=P.rock; ctx.fillRect(s.x,s.y-12,1,1); }
    circle(ctx, s.x, s.y, r, c);
    if (kind==='safe'){ ctx.fillStyle=P.shipEdge; ctx.fillRect(s.x-r, s.y-1, r*2+1, 1); }
    ctx.fillStyle='rgba(255,255,255,0.5)'; ctx.fillRect(s.x-1, s.y-1, 1, 1);
  }
  function rockGlyph(ctx, s, P){ ctx.fillStyle=P.rock; ctx.fillRect(s.x-2,s.y,5,1); ctx.fillRect(s.x,s.y-2,1,5);
    for(let a=0;a<360;a+=45){ const d=dir(a*D); ctx.fillRect(Math.round(s.x+d.x*5), Math.round(s.y+d.y*5), 1, 1); } }
  function wptGlyph(ctx, s, w, P, sel, fs){
    const c=P.wpt[w.kind]||P.wpt.mark;
    if (w.kind==='anchor'){ circle(ctx,s.x,s.y,2,c); ringOutline(ctx,s.x,s.y,5,c); ctx.fillStyle=c; ctx.fillRect(s.x,s.y-6,1,4); ctx.fillRect(s.x-3,s.y-3,7,1); }
    else if (w.kind==='hazard'){ circle(ctx,s.x,s.y,3,c); ctx.fillStyle=P.shipEdge; ctx.fillRect(s.x-1,s.y-1,3,1); ctx.fillRect(s.x-1,s.y+1,3,1); }
    else { // flag
      thickLine(ctx, s.x, s.y, s.x, s.y-11, 1, c); ctx.fillStyle=c; ctx.fillRect(s.x+1, s.y-11, 7, 5); circle(ctx,s.x,s.y,2,c);
    }
    if (sel){ ringOutline(ctx, s.x, s.y, 8, TEAL); }
    if (fs>=2 && w.name){ ctx.fillStyle=P.plate; const tw=textW(w.name,1); ctx.fillRect(s.x+4, s.y+3, tw+2, 7); text(ctx, w.name, s.x+5, s.y+4, 1, P.label); }
  }
  function trafficGlyph(ctx, s, t, P, fs){
    const c=t.__sel?P.trafficSel:P.traffic, a=(t.cog||0)*D, ca=Math.cos(a), sa=Math.sin(a), r=4;
    const pt=(fx,fy)=>[ s.x+(fx*ca-fy*sa), s.y+(fx*sa+fy*ca) ];
    poly(ctx,[pt(0,-r*1.4),pt(r*0.8,r),pt(-r*0.8,r)],true); ctx.fillStyle=c; ctx.fill();
    const h=pt(0,-r*2.2); thickLine(ctx, s.x, s.y, h[0], h[1], 1, c);
    if (fs>=2 && t.name){ text(ctx, t.name, s.x+5, s.y-3, 1, c); }
  }

  // ---- chart body -----------------------------------------------------------
  function drawChart(ctx, L, o, P){
    const ch=L.chart, V=makeView(o, ch), night=o.night, layers=o.layers||{};
    ctx.save(); ctx.beginPath(); ctx.rect(ch.x, ch.y, ch.w, ch.h); ctx.clip();
    ctx.fillStyle=P.water[4]; ctx.fillRect(ch.x, ch.y, ch.w, ch.h);
    // base chart (depth shading + land) via view transform
    if (layers.depth!==false){
      const base=chartBase(night); ctx.save(); ctx.imageSmoothingEnabled=false;
      ctx.translate(V.ox, V.oy); ctx.rotate(V.phi); ctx.scale(V.sc, V.sc); ctx.translate(-V.cx, -V.cy);
      ctx.drawImage(base, 0, 0); ctx.restore();
    } else {
      const base=chartBase(night);   // still show land when depth off — draw land only pass
      ctx.save(); ctx.imageSmoothingEnabled=false; ctx.globalAlpha=0.0; ctx.restore();
      // redraw land polys directly (flat water)
      LAND.forEach(l=>{ poly(ctx, l.pts.map(p=>{ const q=w2s(V,p[0],p[1]); return [q.x,q.y]; }), true); ctx.fillStyle=P.land; ctx.fill(); ctx.strokeStyle=P.landEdge; ctx.lineWidth=1.4; ctx.stroke(); });
    }
    const fs = ch.w>=360 ? 2 : 1;

    // region labels
    if (layers.names!==false && fs>=2){ REGIONS.forEach(rg=>{ const s=w2s(V, rg.x, rg.y);
      if (s.x<ch.x-30||s.x>ch.x+ch.w+30||s.y<ch.y-10||s.y>ch.y+ch.h+10) return;
      textC(ctx, rg.t, s.x, s.y, 1, P.label); }); }

    // previous track (breadcrumb)
    if (layers.track!==false){ const tr=o.track||[]; for(let i=1;i<tr.length;i++){ const a=w2s(V,tr[i-1][0],tr[i-1][1]), b=w2s(V,tr[i][0],tr[i][1]); dashLine(ctx,a.x,a.y,b.x,b.y,1,P.track,3,3); }
      tr.forEach(p=>{ const s=w2s(V,p[0],p[1]); ctx.fillStyle=P.track; ctx.fillRect(Math.round(s.x),Math.round(s.y),1,1); }); }

    // planned route (magenta) + legs
    const rt=o.route||[];
    if (layers.route!==false && rt.length>1){
      for(let i=1;i<rt.length;i++){ const a=w2s(V,rt[i-1].x,rt[i-1].y), b=w2s(V,rt[i].x,rt[i].y);
        thickLine(ctx,a.x,a.y,b.x,b.y,2,P.routeDim); dashLine(ctx,a.x,a.y,b.x,b.y,2,P.route,6,4);
        // marching direction chevrons
        const L2=Math.hypot(b.x-a.x,b.y-a.y), ux=(b.x-a.x)/L2, uy=(b.y-a.y)/L2, ph=((o.phase||0)*22)%18;
        for(let d=ph;d<L2;d+=18){ const cx=a.x+ux*d, cy=a.y+uy*d; ctx.fillStyle=P.route; ctx.fillRect(Math.round(cx-ux),Math.round(cy-uy),2,2); } }
      rt.forEach((p,i)=>{ const s=w2s(V,p.x,p.y); ringOutline(ctx,s.x,s.y,4,P.route); if(i>0){ circle(ctx,s.x,s.y,1,P.route);} });
    }

    // rocks
    if (layers.rocks!==false) ROCKS.forEach(r=>{ const s=w2s(V,r[0],r[1]); if(s.x<ch.x-6||s.x>ch.x+ch.w+6||s.y<ch.y-6||s.y>ch.y+ch.h+6) return; rockGlyph(ctx,s,P); });

    // buoys
    if (layers.buoys!==false) BUOYS.forEach(b=>{ const s=w2s(V,b.x,b.y); if(s.x<ch.x-10||s.x>ch.x+ch.w+10||s.y<ch.y-14||s.y>ch.y+ch.h+10) return; buoyGlyph(ctx,s,b.kind,P); });

    // current arrow at the Rips
    if (o.tide){ const cs=w2s(V, 494, 404), set=o.tide.set||300, dr=o.tide.drift||1.2, dd=dir(set*D), len=14+dr*8;
      const tip={ x:cs.x+dd.x*len, y:cs.y+dd.y*len }; dashLine(ctx, cs.x,cs.y, tip.x,tip.y, 1, P.current, 4, 2);
      const back=dir((set+180)*D), l=dir((set+150)*D), r=dir((set-150)*D);
      thickLine(ctx, tip.x,tip.y, tip.x+l.x*4, tip.y+l.y*4, 1, P.current); thickLine(ctx, tip.x,tip.y, tip.x+r.x*4, tip.y+r.y*4, 1, P.current);
    }

    // traffic (AIS)
    if (layers.traffic!==false){ (o.traffic||[]).forEach((t,i)=>{ t.__sel=(o.trafficSel===i); const s=w2s(V,t.x,t.y); if(s.x<ch.x-12||s.x>ch.x+ch.w+12||s.y<ch.y-12||s.y>ch.y+ch.h+12) return; trafficGlyph(ctx,s,t,P,fs); }); }

    // waypoints / POI
    if (layers.poi!==false){ (o.waypoints||[]).forEach((w,i)=>{ const s=w2s(V,w.x,w.y); if(s.x<ch.x-14||s.x>ch.x+ch.w+18||s.y<ch.y-14||s.y>ch.y+ch.h+10) return; wptGlyph(ctx,s,w,P,o.selWpt===i,fs); }); }

    // measure line (range & bearing)
    if (o.measure && o.measure.a){ const a=w2s(V,o.measure.a.x,o.measure.a.y);
      const b = o.measure.b ? w2s(V,o.measure.b.x,o.measure.b.y) : a;
      dashLine(ctx,a.x,a.y,b.x,b.y,1,P.stripHi,4,3); ringOutline(ctx,a.x,a.y,3,P.stripHi); ringOutline(ctx,b.x,b.y,3,P.stripHi);
      if (o.measure.b && fs>=2){ const mid={x:(a.x+b.x)/2,y:(a.y+b.y)/2}, brg=bearingWorld(o.measure.a,o.measure.b), dst=distWorldNM(o.measure.a,o.measure.b);
        const lbl=fmtDeg(brg)+'\u00b0 '+fmtNM(dst)+'NM'; ctx.fillStyle=P.plate; const tw=textW(lbl,1); ctx.fillRect(mid.x-tw/2-2, mid.y-4, tw+4, 8); textC(ctx, lbl, mid.x, mid.y-3, 1, P.stripHi); }
    }

    // own-ship (always) — heading-up in head mode
    const os=w2s(V, OWN.x, OWN.y), sang = o.orient==='head' ? 0 : (o.heading||0);
    ringOutline(ctx, os.x, os.y, Math.max(6, 8+3*Math.sin((o.phase||0)*2)), P.head);  // GPS breathing
    ownShip(ctx, os, sang, P, V.sc*PXNM);

    ctx.restore();

    // north arrow + scale bar (screen space)
    if (fs>=2){ northArrow(ctx, ch.x+ch.w-16, ch.y+16, o, P); scaleBar(ctx, ch.x+10, ch.y+ch.h-12, V, o, P); }
    ctx.strokeStyle=P.stripDim; ctx.lineWidth=1; ctx.strokeRect(ch.x+0.5, ch.y+0.5, ch.w-1, ch.h-1);
  }
  function northArrow(ctx, x, y, o, P){ const ang=(o.orient==='head'?-(o.heading||0):0)*D, d={x:Math.sin(ang),y:-Math.cos(ang)};
    thickLine(ctx, x, y+7, x+d.x*7, y+7+d.y*7, 1, P.label); ctx.fillStyle=P.buoy.port; ctx.fillRect(Math.round(x+d.x*7-1), Math.round(y+7+d.y*7-1), 2, 2); textC(ctx,'N',x+d.x*11,y+7+d.y*11-2,1,P.label); }
  function scaleBar(ctx, x, y, V, o, P){ // pick a round NM that fits ~ up to 90px
    const pxPerNM=V.sc*PXNM; let nm=1; const steps=[0.1,0.25,0.5,1,2,5,10]; for(const s of steps){ if(s*pxPerNM<=100) nm=s; } const w=Math.round(nm*pxPerNM);
    ctx.fillStyle=P.label; ctx.fillRect(x,y,w,1); ctx.fillRect(x,y-2,1,4); ctx.fillRect(x+w,y-2,1,4); text(ctx, fmtNM(nm)+'NM', x, y-9, 1, P.label); }

  // ---- on-glass chrome ------------------------------------------------------
  function fmtField(ctx, x, y, label, val, P, fs, valCol){ text(ctx, label, x, y, 1, P.stripDim); return text(ctx, val, x, y+7, fs, valCol||P.stripFg); }
  function topBar(ctx, B, o, P, fs, mode){
    ctx.fillStyle=P.strip; ctx.fillRect(B.x,B.y,B.w,B.h); ctx.fillStyle='rgba(255,255,255,0.05)'; ctx.fillRect(B.x,B.y,B.w,1);
    const y=B.y+Math.round((B.h-14)/2), depth=depthAt(OWN.x,OWN.y);
    let x=B.x+6;
    // GPS fix pip
    circle(ctx, x+2, y+7, 2, P.stripHi); x+=8; x=text(ctx,'GPS',x,y+3,1,P.stripHi)+6;
    x=fmtField(ctx,x,y,'POS', fmtPos(OWN.x,OWN.y), P, fs)+ (fs>=2?14:8);
    x=fmtField(ctx,x,y,'SOG', (o.sog||0).toFixed(1)+'KN', P, fs)+ (fs>=2?12:7);
    x=fmtField(ctx,x,y,'COG', fmtDeg(o.heading)+'\u00b0', P, fs)+ (fs>=2?12:7);
    x=fmtField(ctx,x,y,'DPTH', depth.toFixed(1)+'M', P, fs, depth<3?P.buoy.port:P.stripFg)+ (fs>=2?12:7);
    if (mode==='max'){ x=fmtField(ctx,x,y,'HDG', fmtDeg(o.heading)+'\u00b0 '+cardinal8(o.heading), P, fs)+14;
      if (o.tide){ x=fmtField(ctx,x,y,'TIDE', o.tide.height.toFixed(1)+'M '+(o.tide.rising?'\u2191':'\u2193'), P, fs)+12;
        fmtField(ctx,x,y,'SET', fmtDeg(o.tide.set)+'\u00b0/'+o.tide.drift.toFixed(1), P, fs); } }
    // right: orientation + range
    const oriTxt=o.orient==='head'?'HEAD-UP':'NORTH-UP'; textR(ctx, oriTxt, B.x+B.w-6, y, 1, P.stripHi);
    textR(ctx, fmtNM(o.rangeNM)+'NM', B.x+B.w-6, y+7, fs, P.stripFg);
  }
  function botBar(ctx, B, o, P, fs){
    ctx.fillStyle=P.strip; ctx.fillRect(B.x,B.y,B.w,B.h); ctx.fillStyle=P.stripDim; ctx.fillRect(B.x,B.y,B.w,1);
    const y=B.y+Math.round((B.h-(fs>=2?7:5))/2), rt=o.route||[];
    if (rt.length>1){ const nxt=rt[1], brg=bearingWorld(OWN,nxt), dtg=distWorldNM(OWN,nxt), tot=routeLenNM(rt), spd=Math.max(0.1,o.sog||6), eta=tot/spd*60;
      let x=B.x+6; ctx.fillStyle=P.route; ctx.fillRect(x,y+1,4,4); x+=8;
      x=text(ctx, 'ROUTE '+String(rt.length-1)+' LEGS', x, y, 1, P.stripFg)+12;
      x=text(ctx, 'NEXT '+fmtDeg(brg)+'\u00b0', x, y, 1, P.stripHi)+10;
      x=text(ctx, 'DTG '+fmtNM(dtg)+'NM', x, y, 1, P.stripFg)+10;
      text(ctx, 'TTG '+fmtHM(eta), x, y, 1, P.stripFg);
      textR(ctx, 'DIST '+fmtNM(tot)+'NM', B.x+B.w-6, y, 1, P.stripHi);
    } else { textC(ctx, '\u2014 NO ACTIVE ROUTE \u2014 SELECT ROUTE TOOL & TAP THE CHART \u2014', B.x+B.w/2, y, 1, P.stripDim); }
  }
  function railBtn(ctx, b, on, tool, P, fs){
    rrect(ctx, b.x, b.y, b.w, b.h, 2, on?P.railLit:P.railPlate); ctx.fillStyle=P.railEdge; ctx.fillRect(b.x,b.y,b.w,1);
    const s=b.w>=30?1:1; textC(ctx, b.id, b.x+b.w/2, b.y+Math.round((b.h-5)/2), s, on?P.railTxt:(tool?P.stripHi:P.railDim));
    if (on && !tool){ ctx.fillStyle=P.railTxt; ctx.fillRect(b.x+2, b.y+2, 2, 2); }
  }
  function rightCol(ctx, R, o, P, fs){
    ctx.fillStyle=P.strip; ctx.fillRect(R.x,R.y,R.w,R.h); ctx.fillStyle=P.railEdge; ctx.fillRect(R.x,R.y,1,R.h);
    let y=R.y+6; const x=R.x+6, s=1;
    text(ctx, 'WAYPOINTS', x, y, s, P.stripHi); textR(ctx, String((o.waypoints||[]).length), R.x+R.w-6, y, s, P.stripDim); y+=9;
    (o.waypoints||[]).slice(0,6).forEach((w,i)=>{ const brg=bearingWorld(OWN,w), dtg=distWorldNM(OWN,w), sel=o.selWpt===i;
      ctx.fillStyle=sel?P.railLit:'transparent'; if(sel) ctx.fillRect(R.x+2,y-1,R.w-4,9);
      ctx.fillStyle=(P.wpt[w.kind]||P.wpt.mark); ctx.fillRect(x, y+1, 3, 3);
      text(ctx, w.name.slice(0,7), x+6, y, s, sel?P.stripFg:P.stripDim);
      textR(ctx, fmtDeg(brg)+'\u00b0', R.x+R.w-30, y, s, P.stripDim); textR(ctx, fmtNM(dtg), R.x+R.w-6, y, s, P.stripFg); y+=9; });
    y+=4; ctx.fillStyle=P.stripDim; ctx.fillRect(R.x+4,y,R.w-8,1); y+=6;
    const rt=o.route||[];
    text(ctx, 'ROUTE', x, y, s, P.stripHi); textR(ctx, rt.length>1?(fmtNM(routeLenNM(rt))+'NM'):'\u2014', R.x+R.w-6, y, s, P.stripFg); y+=9;
    if (rt.length>1){ for(let i=1;i<rt.length && i<=5;i++){ const brg=bearingWorld(rt[i-1],rt[i]), leg=distWorldNM(rt[i-1],rt[i]);
      text(ctx, 'LEG '+i, x, y, s, P.stripDim); textR(ctx, fmtDeg(brg)+'\u00b0', R.x+R.w-30, y, s, P.stripDim); textR(ctx, fmtNM(leg), R.x+R.w-6, y, s, P.stripFg); y+=9; } }
    y+=4; ctx.fillStyle=P.stripDim; ctx.fillRect(R.x+4,y,R.w-8,1); y+=6;
    text(ctx, 'CURSOR', x, y, s, P.stripHi); y+=9;
    if (o.measure && o.measure.a && o.measure.b){ text(ctx,'RNG', x, y, s, P.stripDim); textR(ctx, fmtNM(distWorldNM(o.measure.a,o.measure.b))+'NM', R.x+R.w-6, y, s, P.stripFg); y+=9;
      text(ctx,'BRG', x, y, s, P.stripDim); textR(ctx, fmtDeg(bearingWorld(o.measure.a,o.measure.b))+'\u00b0', R.x+R.w-6, y, s, P.stripFg); y+=9; }
    else { text(ctx, 'MEASURE TOOL', x, y, s, P.stripDim); y+=9; text(ctx, 'TAP TWO POINTS', x, y, s, P.stripDim); y+=9; }
    y+=4; ctx.fillStyle=P.stripDim; ctx.fillRect(R.x+4,y,R.w-8,1); y+=6;
    if (o.tide){ text(ctx, 'TIDE & SET', x, y, s, P.stripHi); y+=9;
      text(ctx,'HT', x, y, s, P.stripDim); textR(ctx, o.tide.height.toFixed(1)+'M '+(o.tide.rising?'RIS':'FALL'), R.x+R.w-6, y, s, P.stripFg); y+=9;
      text(ctx,'SET', x, y, s, P.stripDim); textR(ctx, fmtDeg(o.tide.set)+'\u00b0 '+o.tide.drift.toFixed(1)+'KN', R.x+R.w-6, y, s, P.stripFg); }
  }
  function profileStrip(ctx, Pr, o, P, fs){
    ctx.fillStyle=P.profSky; ctx.fillRect(Pr.x,Pr.y,Pr.w,Pr.h); ctx.fillStyle=P.railEdge; ctx.fillRect(Pr.x,Pr.y,Pr.w,1);
    // sample depth ahead along heading for rangeNM*1.4
    const N=Math.min(Pr.w, 120), aheadNM=(+o.rangeNM||6)*1.3, hd=dir((o.heading||0)*D);
    let maxD=1; const ds=[]; for(let i=0;i<N;i++){ const t=i/(N-1), wx=OWN.x+hd.x*t*aheadNM*PXNM, wy=OWN.y+hd.y*t*aheadNM*PXNM; const d=Math.max(0,depthAt(wx,wy)); ds.push(d); if(d>maxD) maxD=d; }
    maxD=Math.max(6, Math.ceil(maxD/2)*2);
    const gy=Pr.y+9, gh=Pr.h-13, gx=Pr.x+2, gw=Pr.w-4;
    // seabed fill
    ctx.fillStyle=P.profBed; ctx.beginPath(); ctx.moveTo(gx, Pr.y+Pr.h);
    for(let i=0;i<N;i++){ const x=gx+i/(N-1)*gw, y=gy+clamp(ds[i]/maxD,0,1)*gh; ctx.lineTo(x,y); }
    ctx.lineTo(gx+gw, Pr.y+Pr.h); ctx.closePath(); ctx.fill();
    ctx.strokeStyle=P.profBedEdge; ctx.lineWidth=1; ctx.beginPath();
    for(let i=0;i<N;i++){ const x=gx+i/(N-1)*gw, y=gy+clamp(ds[i]/maxD,0,1)*gh; i?ctx.lineTo(x,y):ctx.moveTo(x,y); } ctx.stroke();
    text(ctx, 'DEPTH AHEAD', Pr.x+4, Pr.y+2, 1, P.profTxt);
    textR(ctx, '0M', Pr.x+Pr.w-4, gy-6, 1, P.stripDim); textR(ctx, maxD+'M', Pr.x+Pr.w-4, Pr.y+Pr.h-8, 1, P.stripDim);
  }

  // ---- one pusher key (fleet idiom) -----------------------------------------
  function key(ctx, b, label, glyph, P, fs, lit){
    rrect(ctx, b.x, b.y, b.w, b.h, Math.max(2,Math.round(b.h*0.2)), CASE[0]);
    const ix=b.x+2, iy=b.y+2, iw=b.w-4, ih=b.h-4, r=Math.max(2,Math.round(ih*0.2));
    rrect(ctx, ix, iy, iw, ih, r, P.keyEdge); rrect(ctx, ix, iy, iw, ih-1, r, P.keyPlate);
    rrect(ctx, ix+2, iy+2, iw-4, Math.max(1,Math.round(ih*0.24)), r-1, 'rgba(255,255,255,0.13)');
    textC(ctx, label, b.x+b.w/2, iy+Math.round(ih*0.32), 1, P.legend);
    const cx=b.x+b.w/2, dcy=Math.round(iy+ih*0.7), s=2; ctx.fillStyle=P.mark;
    if (glyph==='up'){ for(let i=0;i<s+1;i++) ctx.fillRect(cx-i, dcy-s+i, i*2+1, 1); }
    else if (glyph==='dn'){ for(let i=0;i<s+1;i++) ctx.fillRect(cx-(s-i), dcy-s+i, (s-i)*2+1, 1); }
    else if (glyph==='sq'){ ctx.strokeStyle=P.mark; ctx.strokeRect(cx-2.5, dcy-2.5, 5, 5); }
    else { circle(ctx, cx, dcy, 1, P.mark); }
    if (lit){ rrect(ctx, ix, iy, iw, ih-1, r, 'rgba(255,190,60,0.10)'); }
  }
  function knob(ctx, K, P){ const cx=K.x+K.w/2, cy=K.y+K.h/2, r=Math.floor(Math.min(K.w,K.h)/2)-1;
    circle(ctx,cx,cy,r,CASE[0]); circle(ctx,cx,cy,r-1,CASE[3]); circle(ctx,cx-1,cy-1,r-3,CASE[4]);
    ctx.fillStyle=CASE[0]; ctx.fillRect(cx-1, cy-r+2, 2, Math.max(3,r*0.5)); }

  // ---- the whole instrument -------------------------------------------------
  function drawUnit(ctx, X, Y, WW, HH, o){
    o = normOpts(o);
    const mode = o.view==='max' ? 'max' : 'console';
    const P = o.night ? NIGHT : DAY;
    const fs = HH>=320 ? 2 : 2;
    const L = layout(X, Y, WW, HH, mode);
    ctx.imageSmoothingEnabled=false;

    // case body
    const cr=Math.max(4,Math.round(HH*0.06));
    rrect(ctx, X, Y, WW, HH, cr, RESIN[0]); rrect(ctx, X+1, Y+1, WW-2, HH-2, cr-1, CASE[1]);
    rrect(ctx, X+2, Y+2, WW-4, Math.max(2,Math.round(HH*0.06)), cr-2, CASE[3]);
    rrect(ctx, X+2, Y+HH-Math.max(2,Math.round(HH*0.045)), WW-4, Math.max(2,Math.round(HH*0.03)), 3, CASE[0]);
    screw(ctx, X+7, Y+7, 3); screw(ctx, X+WW-7, Y+7, 3); screw(ctx, X+7, Y+HH-7, 3); screw(ctx, X+WW-7, Y+HH-7, 3);

    // recessed glass
    const lr=Math.max(3,Math.round(L.lcd.h*0.04));
    rrect(ctx, L.lcd.x-3, L.lcd.y-3, L.lcd.w+6, L.lcd.h+6, lr+1, RESIN[0]);
    rrect(ctx, L.lcd.x-2, L.lcd.y-2, L.lcd.w+4, L.lcd.h+4, lr+1, RESIN[2]);
    ctx.save();
    { const path=new Path2D(), rr=lr, x=L.lcd.x,y=L.lcd.y,w=L.lcd.w,h=L.lcd.h;
      path.moveTo(x+rr,y); path.arcTo(x+w,y,x+w,y+h,rr); path.arcTo(x+w,y+h,x,y+h,rr); path.arcTo(x,y+h,x,y,rr); path.arcTo(x,y,x+w,y,rr); path.closePath(); ctx.clip(path); }
    ctx.fillStyle=P.frame; ctx.fillRect(L.lcd.x, L.lcd.y, L.lcd.w, L.lcd.h);
    drawChart(ctx, L, o, P);
    topBar(ctx, L.topbar, o, P, fs, mode);
    botBar(ctx, L.botbar, o, P, fs);
    if (mode==='max'){
      L.railBtns.forEach(b=>{ const on = b.tool ? (o.tool===b.id.toLowerCase() || (b.id==='PAN'&&(!o.tool||o.tool==='pan'))) : layerOn(o, b.id); railBtn(ctx, b, on, b.tool, P, fs); });
      ctx.fillStyle=P.railEdge; ctx.fillRect(L.rail.x+L.rail.w-1, L.rail.y, 1, L.rail.h);
      rightCol(ctx, L.right, o, P, fs);
      profileStrip(ctx, L.profile, o, P, fs);
    }
    ctx.restore();
    ctx.fillStyle='rgba(255,255,255,0.05)'; ctx.fillRect(L.lcd.x+3, L.lcd.y+2, L.lcd.w-6, 1);

    // side keys + rotary
    key(ctx, L.keys[0], mode==='max'?'CNSL':'MAX', 'sq', P, fs, mode==='max');
    key(ctx, L.keys[1], 'MARK', 'dot', P, fs, o.tool==='mrk');
    key(ctx, L.keys[2], 'IN',   'up',  P, fs, false);
    key(ctx, L.keys[3], 'OUT',  'dn',  P, fs, false);
    knob(ctx, L.knobBox, P);

    // brand strip (original wordmark)
    const bs=1, by=L.brand.y+Math.round((L.brand.h-5*bs)/2);
    text(ctx, 'GN-12', L.brand.x+1, by, bs, TEAL);
    textC(ctx, 'HARBOUR', X+WW/2, by, bs, TEAK);
    { const s='CHARTPLOTTER'; text(ctx, s, L.brand.x+L.brand.w-textW(s,bs), by, bs, BLUE); }
  }

  function layerOn(o, id){ const m={LAND:'land',ROCK:'rocks',BUOY:'buoys',POI:'poi',TRK:'track',DPTH:'depth',TRFC:'traffic'}; const k=m[id]; return (o.layers||{})[k]!==false; }
  function normOpts(o){ o=o||{}; return {
    view:o.view==='max'?'max':'console', night:!!o.night, rangeNM:+o.rangeNM||6, orient:o.orient==='head'?'head':'north', heading:+o.heading||0, sog:o.sog==null?6.4:+o.sog,
    cam:o.cam||{x:OWN.x,y:OWN.y}, phase:+o.phase||0, tool:o.tool||'pan',
    layers:o.layers||{}, waypoints:o.waypoints||defaultWaypoints(), route:o.route||defaultRoute(), track:o.track||defaultTrack(),
    traffic:o.traffic||defaultTraffic(), tide:o.tide||tideState(0.35), measure:o.measure||null, selWpt:o.selWpt==null?-1:o.selWpt, trafficSel:o.trafficSel==null?-1:o.trafficSel,
  }; }

  // ---- format + nav math ----------------------------------------------------
  const RANGE_STEPS=[1,2,4,6,10,16];
  const C8=['N','NE','E','SE','S','SW','W','NW'];
  function fmtNM(nm){ nm=Math.abs(nm); if(nm>=10) return String(Math.round(nm)); if(nm>=1) return nm.toFixed(1); return nm.toFixed(2); }
  function fmtDeg(h){ const d=Math.round(norm360(h)); return (d<10?'00':d<100?'0':'')+d; }
  function cardinal8(h){ return C8[Math.round(norm360(h)/45)%8]; }
  function fmtHM(min){ if(!isFinite(min)) return '--:--'; min=Math.max(0,Math.round(min)); const h=Math.floor(min/60), m=min%60; return (h<10?'0':'')+h+':'+(m<10?'0':'')+m; }
  function fmtPos(wx, wy){ // fictional grid: N44 + , W63 + , from world px
    const lat=44 + (WORLD_H-wy)/WORLD_H*0.42, lon=63 + (WORLD_W-wx)/WORLD_W*0.5;
    const lm=(lat-Math.floor(lat))*60, om=(lon-Math.floor(lon))*60;
    return 'N'+Math.floor(lat)+' '+lm.toFixed(1)+' W'+Math.floor(lon)+' '+om.toFixed(1);
  }
  function bearingWorld(a, b){ const dx=(b.x-a.x), dy=(b.y-a.y); return norm360(Math.atan2(dx, -dy)/D); }
  function distWorldNM(a, b){ return Math.hypot(b.x-a.x, b.y-a.y)/PXNM; }
  function routeLenNM(rt){ let s=0; for(let i=1;i<rt.length;i++) s+=distWorldNM(rt[i-1],rt[i]); return s; }
  function tideState(phase){ phase=((phase%1)+1)%1; const height=2.1+1.5*Math.sin(phase*2*Math.PI), rising=Math.cos(phase*2*Math.PI)>0;
    return { phase, height, rising, set: rising?68:248, drift: 0.6+1.4*Math.abs(Math.sin(phase*2*Math.PI+0.5)) }; }

  // ---- standalone paint -----------------------------------------------------
  function paint(ctx, o){ o=o||{}; const max=o.view==='max'; const W=max?MAX_W:CONSOLE_W, H=max?MAX_H:CONSOLE_H;
    ctx.clearRect(0,0,W,H); const X=Math.round(W*0.02), Y=Math.round(H*0.03), WW=W-2*X, HH=H-Math.round(H*0.06); drawUnit(ctx, X, Y, WW, HH, o); }
  function render(o){ o=o||{}; const max=o.view==='max'; const c=cv(max?MAX_W:CONSOLE_W, max?MAX_H:CONSOLE_H); paint(c.getContext('2d'), o); return c; }

  root.NavRig = {
    CONSOLE_W, CONSOLE_H, MAX_W, MAX_H, WORLD_W, WORLD_H, PXNM, OWN, DEG:D,
    paint, render, drawUnit, paintInto:drawUnit, layout,
    makeView, w2s, s2w, isLand, depthAt,
    defaultWaypoints, defaultTrack, defaultRoute, defaultTraffic, tideState,
    fmtNM, fmtDeg, cardinal8, fmtHM, fmtPos, bearingWorld, distWorldNM, routeLenNM, norm360,
    RANGE_STEPS, C8, LAND, BUOYS, ROCKS, REGIONS,
  };
})(typeof globalThis!=='undefined'?globalThis:window);
