/* Hidden Harbours — WHARF RIG, PASS 2 · THE PARTS (globalThis.WharfGeo2).
   The structure half of WharfRig2 (Art/wharfRig2.js): five-band ramps, the tidal frame, every family and fitting
   in metres, the fleet the berths are sized for, and the character v9.2 contract the ladders, cleats and ramps are
   built to. No camera and no light here: WharfRig2 rasterises these faces to a G-buffer and lights them from
   WeatherSky, the way TreeRig4 and the cliffs are lit.

   Model frame: +x along the module (its run), +y to its WATER face, +z up, z = 0 chart datum (lowest water).
   Floats are built with their waterline at z = 0; WharfRig2 lifts them onto the tide and the sea every frame.

   Pass 1 (Art/wharfIsoRig.js) is untouched. What changes about the structures:
     · fittings are sized for character v9.2: rungs 0.24 m (every creator body fits 0.211–0.273; pass 1's 0.30
       left 50 of 375 short), a 0.45 m ladder with grab hoops 1.0 m over the deck and a gap in the bull rail,
       the top rung one rung below the deck, the bottom rung 1 m under chart datum for a swimmer; cleats set
       0.30 m in; gangways solved to 1:3 at chart datum with 1.1 m clear width and 0.30 m treads
     · modules join: an end is 'free' or 'join' (a join drops the end wall, the rail return and one of the two
       pile rows), a side takes open spans where another module butts on, and deck texture phase runs from the
       module's −x end, so a chain of modules reads as one deck
     · piles, walls and the chain stop on the bed under them: s.bed is a number or bed(x, y)
     · age (new · seasoned · weathered · derelict) and season (summer · autumn · winter) change geometry as
       well as colour: growth comes with age, winter brings ice collars, icicles and spray rime, a derelict
       deck loses boards (published as holes)
     · new families finger · dolphin · catwalk; new styles quay 'block', pier deck 'concrete', float hull
       'concrete' and 'drum'; berths sized for the whole fleet, dory to gas tanker                            */
(function (root) {
  'use strict';
  const DEG = Math.PI / 180;
  const clamp = (v, a, b) => v < a ? a : v > b ? b : v;
  const h2r = (h) => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];
  const r2h = (r) => '#' + r.map(v => clamp(Math.round(v), 0, 255).toString(16).padStart(2, '0')).join('');
  const mix = (a, b, t) => { const A = h2r(a), B = h2r(b); return r2h([0, 1, 2].map(i => A[i] + (B[i] - A[i]) * t)); };
  const desat = (hex, t) => { const [r, g, b] = h2r(hex), l = 0.3 * r + 0.59 * g + 0.11 * b; return r2h([r + (l - r) * t, g + (l - g) * t, b + (l - b) * t]); };
  const r5 = (six) => [0, 1.25, 2.5, 3.75, 5].map(p => { const i = Math.floor(p), f = p - i; return f < 1e-6 ? six[i] : mix(six[i], six[Math.min(5, i + 1)], f); });
  function mulberry32(a) { return function () { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }; }
  function hash2(a, b) { let h = (a * 374761393 + b * 668265263) >>> 0; h = (h ^ (h >> 13)) * 1274126177 >>> 0; return ((h ^ (h >> 16)) >>> 0) / 4294967296; }

  // ============================ RAMPS (pass 1's six steps, resampled to the five bands) ==================
  const SIX = {
    wood: ['#553d23', '#6b4e30', '#7f603b', '#957749', '#ab8d5f', '#c1a578'], plank: ['#454037', '#565045', '#676054', '#7a7265', '#8d8477', '#a1988a'],
    pole: ['#33291f', '#42342a', '#523f33', '#63503f', '#75604c', '#8a7460'], iron: ['#111216', '#1c1e23', '#2a2d33', '#3a3e46', '#4d525a', '#636970'],
    galv: ['#565b5f', '#6d7276', '#868b8f', '#a0a5a8', '#bbbfc1', '#d6d9da'], alum: ['#3f4448', '#525a5e', '#687175', '#7f888c', '#98a0a3', '#b2b9bb'],
    conc: ['#55564f', '#686962', '#7c7d74', '#919287', '#a6a79b', '#bbbcae'], stone: ['#252b31', '#3a434c', '#525a64', '#6a727b', '#848c94', '#9aa2a9'],
    rubber: ['#141517', '#1d1f22', '#282b2f', '#34383d', '#43484d', '#54595f'], foam: ['#5c3a12', '#7d5019', '#9c6a24', '#b8853a', '#cda058', '#e0bd80'],
    rope: ['#5a4526', '#71592f', '#8a6f3d', '#a2874e', '#b89f66', '#cdb686'], yel: ['#6a4d17', '#8e6a20', '#b8923c', '#cfa945', '#e0bd58', '#eed37e'],
    poly: ['#1b3550', '#26496a', '#345f85', '#45789e', '#5c92b4', '#7aadc9'], barn: ['#6b6a5f', '#807f72', '#989685', '#b0ad9a', '#c6c3af', '#dcd9c4'],
    weed: ['#2b2a15', '#3b3a1c', '#4d4a22', '#605c2b', '#756f36', '#8a8244'], alg: ['#20301f', '#2c4029', '#3a5233', '#4a663f', '#5d7c4d', '#72925e'],
    sandst: ['#3a231a', '#50301f', '#6b3f28', '#845234', '#9c6a46', '#b3835d'], rust: ['#3a1c10', '#4f2614', '#6a3620', '#84462b', '#9c5b3a', '#b2724c'],
    steel: ['#2a2f33', '#3c454b', '#525c63', '#6d777e', '#889298', '#a2acb1'], hdpe: ['#1b1f22', '#262b2f', '#33393e', '#434a50', '#555d64', '#6a737a'],
    hdpeDeck: ['#3f4348', '#4e545a', '#5f666d', '#727a81', '#868f96', '#9aa4ab'],
    block: ['#3b3f44', '#4e5358', '#63686d', '#797e82', '#8f9497', '#a6aaac'],     // granite ashlar (heritage block quay)
    ice: ['#4f6a78', '#6a8794', '#8ea9b3', '#b3cad0', '#d4e3e6', '#eef6f6'],
    red: ['#3f1512', '#5a1d17', '#7a281e', '#973527', '#b04736', '#c6604c'],       // dolphin day-mark paint
    board: ['#7f7b6f', '#96927f', '#aeaa98', '#c3bfad', '#d5d2c2', '#e3e1d4'],     // white HDPE / gelcoat: cutting boards, dock boxes, pedestals (kept off paper-white)
  };
  // por (porosity: how wet reads) · gloss (glints) · hold (how readily snow stays) · cls · age law
  const MATDEF = {
    pole: [0.30, 0.25, 1, 'wood', 'creo'], wood: [0.32, 0.18, 1, 'wood', 'timber'], plank: [0.32, 0.18, 1, 'wood', 'deck'],
    conc: [0.26, 0.22, 1, 'conc', 'conc'], block: [0.12, 0.35, 1, 'stone', 'conc'], stone: [0.10, 0.55, 0.9, 'stone', null],
    sandst: [0.34, 0.30, 0.9, 'stone', null], iron: [0.05, 0.60, 0.8, 'metal', 'rust'], galv: [0.04, 0.70, 0.8, 'metal', 'galv'],
    alum: [0.03, 0.75, 0.7, 'metal', null], steel: [0.05, 0.60, 0.8, 'metal', 'rust'], rust: [0.12, 0.35, 0.9, 'metal', null],
    rubber: [0.02, 0.50, 0.6, 'rubber', null], foam: [0.02, 0.60, 0.6, 'plastic', null], poly: [0.02, 0.60, 0.6, 'plastic', null],
    hdpe: [0.02, 0.55, 0.7, 'plastic', null], hdpeDeck: [0.03, 0.40, 1, 'plastic', null], rope: [0.40, 0.05, 0.9, 'rope', null],
    yel: [0.08, 0.40, 1, 'paint', 'paint'], red: [0.08, 0.40, 1, 'paint', 'paint'], barn: [0.15, 0.30, 0.4, 'growth', null],
    weed: [0.20, 0.85, 0, 'growth', 'grow'], alg: [0.20, 0.80, 0, 'growth', 'grow'], ice: [0.00, 0.95, 0.7, 'ice', null], board: [0.04, 0.35, 1, 'plastic', 'paint'],
  };
  function ageCol(c, law, a, i) {
    if (law === 'timber' || law === 'deck') {
      if (a < 0.3) c = mix(c, law === 'deck' ? '#a88c5c' : '#b3945e', (0.3 - a) / 0.3 * 0.5);
      c = mix(desat(c, a * 0.30), '#8e8b82', a * 0.14);
      if (a > 0.72) c = mix(c, '#343a2c', (a - 0.72) / 0.28 * 0.30);
    } else if (law === 'creo') {
      c = a < 0.4 ? mix(c, '#1d1813', (0.4 - a) / 0.4 * 0.45) : mix(desat(c, (a - 0.4) * 0.4), '#6e6456', (a - 0.4) * 0.30);
    } else if (law === 'conc') {
      c = a < 0.3 ? mix(c, '#c9c7bb', (0.3 - a) / 0.3 * 0.35) : mix(c, '#4a4d45', (a - 0.3) * 0.28);
    } else if (law === 'rust') c = mix(c, SIX.rust[i], a * 0.45);
    else if (law === 'galv') c = mix(c, '#6d6a5e', a * 0.25);
    else if (law === 'paint') c = mix(desat(c, a * 0.45), '#8f8b7a', a * 0.25);
    return c;
  }
  const seasonGrow = (c, season) => season === 'autumn' ? mix(c, '#5a4424', 0.30) : season === 'winter' ? mix(desat(c, 0.4), '#3c3a2e', 0.25) : c;
  // every base carries its tide-frame transforms, so matAtZ can band ANY material it meets:
  //   S stain / creosote bleed · I ice scour · B barnacle crust · G weed film · R winter spray rime
  function makeMats(s) {
    const a = s.age, sea = s.season, M = {};
    for (const k in MATDEF) {
      const [por, gloss, hold, cls, law] = MATDEF[k];
      let six = SIX[k].map((c, i) => law && law !== 'grow' ? ageCol(c, law, a, i) : c);
      if (law === 'grow') six = six.map(c => seasonGrow(c, sea));
      const put = (key, sx, p2) => { M[key] = Object.assign({ ramp: r5(sx), por, gloss, hold, cls, base: k }, p2 || {}); };
      put(k, six);
      put(k + 'S', six.map(c => mix(desat(c, 0.34), '#1b241f', 0.42)), { por: Math.max(por, 0.2) });
      put(k + 'I', six.map(c => mix(desat(c, 0.48), '#c9cbbf', 0.18)));
      put(k + 'B', six.map(c => mix(desat(c, 0.42), '#a8a290', 0.46)), { por: 0.18, gloss: 0.3, hold: 0.4, cls: 'growth' });
      put(k + 'G', six.map(c => seasonGrow(mix(desat(c, 0.26), '#3a4020', 0.56), sea)), { gloss: 0.85, hold: 0, cls: 'growth' });
      put(k + 'R', six.map(c => mix(desat(c, 0.55), '#dfe9ea', 0.42)), { gloss: 0.95, hold: 0.7, cls: 'ice' });
    }
    return M;
  }

  // ============================ STRUCTURE TEXTURES (band offsets, −2 … +1) ====================================
  function plankTex(p) { p = p || 0.20; return (u, v) => { const f = ((u % p) + p) % p; if (f < 0.028) return -2; return hash2(Math.floor(u / p) | 0, Math.floor(v * 2.4) | 0) < 0.42 ? -1 : 0; }; }
  function grainTex(p) { p = p || 0.28; return (u, v) => { const f = ((v % p) + p) % p; if (f < 0.03) return -2; return hash2(Math.floor(v / p) | 0, Math.floor(u * 3) | 0) < 0.5 ? 0 : -1; }; }
  function sawnTex() { return (u, v) => { const h = hash2(Math.floor(u * 7) | 0, Math.floor(v * 22) | 0); return h < 0.3 ? -1 : (h > 0.9 ? 1 : 0); }; }
  function formTex(p) { p = p || 0.62; return (u, v) => { const f = ((v % p) + p) % p; if (f < 0.035) return -2; const g = ((u % 1.22) + 1.22) % 1.22; if (g < 0.03) return -1; return hash2(Math.floor(u * 5) | 0, Math.floor(v * 5) | 0) < 0.24 ? -1 : 0; }; }
  function ribTex(p) { p = p || 0.30; return (u) => { const f = ((u % p) + p) % p; return f < p * 0.30 ? -2 : (f > p * 0.72 ? 1 : 0); }; }
  function rockTex() { return (u, v) => { const h = hash2(Math.floor(u * 6) | 0, Math.floor(v * 6) | 0); return h < 0.26 ? -1 : (h > 0.84 ? 1 : 0); }; }
  function crustTex() { return (u, v) => { const h = hash2(Math.floor(u * 26) | 0, Math.floor(v * 26) | 0); return h < 0.30 ? -2 : (h > 0.70 ? 1 : 0); }; }
  function weedTex() { return (u, v) => { const h = hash2(Math.floor(u * 9) | 0, Math.floor(v * 17) | 0); return h < 0.34 ? -2 : (h > 0.8 ? 1 : 0); }; }
  function treadTex(p) { p = p || 0.30; return (u, v) => { const f = ((v % p) + p) % p; return f < 0.045 ? -2 : (f < 0.09 ? 1 : 0); }; }
  function rustTex() { return (u, v) => { const h = hash2(Math.floor(u * 9) | 0, Math.floor(v * 9) | 0); return h < 0.22 ? -2 : (h > 0.88 ? 1 : 0); }; }
  function sheetTex(p) { p = p || 0.62; return (u, v) => { const f = (((u % p) + p) % p) / p; if (f < 0.05) return -2; if (f < 0.16) return -1; if (f < 0.50) return 0; if (f < 0.66) return 1; return hash2(Math.floor(u / p) | 0, Math.floor(v * 2.2) | 0) < 0.35 ? -1 : 0; }; }
  function sheetWoodTex(p) { p = p || 0.30; return (u, v) => { const f = (((u % p) + p) % p) / p; if (f < 0.10) return -2; if (f > 0.90) return -1; return hash2(Math.floor(u / p) | 0, Math.floor(v * 1.7) | 0) < 0.42 ? -1 : 0; }; }
  function cubeTex(p) { p = p || 0.50; return (u, v) => { const fu = (((u % p) + p) % p) / p, fv = (((v % p) + p) % p) / p; if (fu < 0.07 || fv < 0.07) return -2; if (fu > 0.90 || fv > 0.90) return -1; return 0; }; }
  function gratingTex() { return (u, v) => { const fu = ((u % 0.10) + 0.10) % 0.10, fv = ((v % 0.034) + 0.034) % 0.034; return fu < 0.02 ? 0 : fv < 0.012 ? -1 : -2; }; }
  // granite ashlar: 0.46 m courses, blocks 0.9–1.5 m, staggered, a dark bed joint and a tooled face
  function blockTex() { return (u, v) => { const c = Math.floor(v / 0.46), fv = v - c * 0.46; if (fv < 0.035) return -2;
    const off = hash2(c, 7) * 1.2, bl = 0.9 + 0.6 * hash2(c, 11), k = Math.floor((u + off) / bl), fu = (u + off) - k * bl; if (fu < 0.03) return -2;
    const h = hash2(k * 31 + c, Math.floor(fv * 9)); return h < 0.2 ? -1 : h > 0.93 ? 1 : 0; }; }
  function paveTex() { return (u, v) => { const k = Math.floor(v / 0.6), f = v - k * 0.6; if (f < 0.03) return -2; const g = ((u + (k & 1) * 0.45) % 0.9 + 0.9) % 0.9; return g < 0.03 ? -2 : hash2(Math.floor(u * 4), Math.floor(v * 4)) < 0.2 ? -1 : 0; }; }
  function panelTex() { return (u, v) => { const f = ((v % 0.3) + 0.3) % 0.3; return f < 0.02 ? -1 : 0; }; }
  // a cast deck: saw-cut joints on the bay and its half, a sparse broad cure mottle and a few pits, so a big slab stays calm
  function slabTex(bay, wid) { bay = bay || 3.0; wid = wid || 2.4; return (u, v) => { const fu = ((u % bay) + bay) % bay, fv = ((v % wid) + wid) % wid;
    if (fu < 0.03) return -2; if (Math.abs(fu - bay / 2) < 0.015 || fv < 0.02) return -1;
    if (hash2(Math.floor(u / 0.9) + 7, Math.floor(v / 0.7)) < 0.10) return -1; return hash2(Math.floor(u * 16), Math.floor(v * 16)) < 0.025 ? -1 : 0; }; }
  function iceTex() { return (u, v) => { const h = hash2(Math.floor(u * 14) | 0, Math.floor(v * 5) | 0); return h < 0.22 ? -1 : h > 0.82 ? 1 : 0; }; }

  // ============================ FACES (outward winding; part · group · layer ride along) ======================
  // part: what the pixel is (deck, pile, ladder…) · grp: 0 fixed, else a moving group · lay: 0 floor layer (the
  // deck and everything under it), 1 raised (rails, bollards, grab hoops: what can stand in front of a figure)
  const CTX = { part: 'deck', grp: 0, lay: 0, ux: 0 };
  const at = (part, grp, lay) => { CTX.part = part; if (grp != null) CTX.grp = grp; CTX.lay = lay == null ? 0 : lay; };
  function F(v, mat, b, db, uv, tex) { return { v, mat, b: b || 0, db: db || 0, uv: uv || null, tex: tex || null, part: CTX.part, grp: CTX.grp, lay: CTX.lay }; }
  function wall(out, x0, y0, x1, y1, z0, z1, mat, tex, b) { const L = Math.hypot(x1 - x0, y1 - y0);
    out.push(F([[x0, y0, z0], [x1, y1, z0], [x1, y1, z1], [x0, y0, z1]], mat, b || 0, 0, [[0, z0], [L, z0], [L, z1], [0, z1]], tex)); }
  function slab(out, pts, z, mat, b, tex) { const uv = tex ? pts.map(p => [p[0] + CTX.ux, p[1]]) : null; out.push(F(pts.map(p => [p[0], p[1], z]), mat, b || 0, 0, uv, tex)); }
  function box(out, x0, x1, y0, y1, z0, z1, mat, b, tex, noTop) {
    wall(out, x0, y0, x1, y0, z0, z1, mat, tex, b); wall(out, x1, y1, x0, y1, z0, z1, mat, tex, b);
    wall(out, x1, y0, x1, y1, z0, z1, mat, tex, b); wall(out, x0, y1, x0, y0, z0, z1, mat, tex, b);
    if (!noTop) slab(out, [[x0, y0], [x1, y0], [x1, y1], [x0, y1]], z1, mat, (b || 0) + 0.28, tex);
  }
  const sub = (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
  const crs = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
  const nrm = (v) => { const m = Math.hypot(v[0], v[1], v[2]) || 1; return [v[0] / m, v[1] / m, v[2] / m]; };
  function tube(out, p0, p1, r, n, mat, b, tex, caps) {
    const u = nrm(sub(p1, p0)), a = Math.abs(u[2]) > 0.9 ? [1, 0, 0] : [0, 0, 1], e1 = nrm(crs(a, u)), e2 = nrm(crs(u, e1));
    n = n || 12; b = b || 0; const L = Math.hypot(p1[0] - p0[0], p1[1] - p0[1], p1[2] - p0[2]), arc = (2 * Math.PI * r) / n;
    const P = (i, end) => { const th = (i / n) * Math.PI * 2, c = Math.cos(th) * r, s = Math.sin(th) * r, q = end ? p1 : p0; return [q[0] + e1[0] * c + e2[0] * s, q[1] + e1[1] * c + e2[1] * s, q[2] + e1[2] * c + e2[2] * s]; };
    for (let i = 0; i < n; i++) { const j = (i + 1) % n; out.push(F([P(i, 0), P(j, 0), P(j, 1), P(i, 1)], mat, b, 0, tex ? [[i * arc, 0], [(i + 1) * arc, 0], [(i + 1) * arc, L], [i * arc, L]] : null, tex)); }
    if (caps !== false) { const top = [], bot = []; for (let i = 0; i < n; i++) { top.push(P(i, 1)); bot.push(P(n - 1 - i, 0)); } out.push(F(top, mat, b + 0.26)); out.push(F(bot, mat, b - 0.5)); }
  }
  const pipe = (out, x, y, r, z0, z1, mat, b, n, tex) => tube(out, [x, y, z0], [x, y, z1], r, n || 12, mat, b, tex);
  const beam = (out, p0, p1, r, mat, b, n) => tube(out, p0, p1, r, n || 4, mat, b);
  function cone(out, x, y, zTop, r, len, mat, n) { n = n || 5; const tip = [x, y, zTop - len];
    for (let i = 0; i < n; i++) { const a0 = i / n * Math.PI * 2, a1 = (i + 1) / n * Math.PI * 2;
      out.push(F([[x + Math.cos(a0) * r, y + Math.sin(a0) * r, zTop], [x + Math.cos(a1) * r, y + Math.sin(a1) * r, zTop], tip], mat, 0.1)); } }
  function torusZ(out, x, y, z, R, r, n, m, mat, b) {
    const P = (i, j) => { const th = i / n * Math.PI * 2, ph = j / m * Math.PI * 2, rr = R + r * Math.cos(ph); return [x + Math.cos(th) * rr, y + Math.sin(th) * rr, z + r * Math.sin(ph)]; };
    for (let i = 0; i < n; i++) for (let j = 0; j < m; j++) { const i2 = (i + 1) % n, j2 = (j + 1) % m; out.push(F([P(i, j), P(i2, j), P(i2, j2), P(i, j2)], mat, (b || 0) + Math.sin(j / m * Math.PI * 2) * 0.22)); }
  }
  function torusY(out, x, y, z, R, r, n, m, mat, b) {
    const P = (i, j) => { const th = i / n * Math.PI * 2, ph = j / m * Math.PI * 2, rr = R + r * Math.cos(ph); return [x + Math.cos(th) * rr, y + r * Math.sin(ph), z + Math.sin(th) * rr]; };
    for (let i = 0; i < n; i++) for (let j = 0; j < m; j++) { const i2 = (i + 1) % n, j2 = (j + 1) % m; out.push(F([P(i, j), P(i2, j), P(i2, j2), P(i, j2)], mat, (b || 0) + Math.sin(j / m * Math.PI * 2) * 0.22)); }
  }
  function decalY(out, yv, ny, xs, xe, z0, z1, mat, b, tex, db) { const e = 0.02 * ny, uw = xe - xs, uh = z1 - z0;
    const P = ny > 0 ? [[xs, yv + e, z0], [xe, yv + e, z0], [xe, yv + e, z1], [xs, yv + e, z1]] : [[xe, yv + e, z0], [xs, yv + e, z0], [xs, yv + e, z1], [xe, yv + e, z1]];
    out.push(F(P, mat, b || 0, db != null ? db : 0.05, tex ? [[0, 0], [uw, 0], [uw, uh], [0, uh]] : null, tex || null)); }
  function decalZ(out, zv, xs, xe, ys, ye, mat, b, tex, db) {
    out.push(F([[xs, ys, zv], [xe, ys, zv], [xe, ye, zv], [xs, ye, zv]], mat, b || 0, db != null ? db : 0.05, tex ? [[0, 0], [xe - xs, 0], [xe - xs, ye - ys], [0, ye - ys]] : null, tex || null)); }

  // ============================ THE TIDAL FRAME ==============================================================
  // z = 0 chart datum. Bands are pinned to the frame, not to the water, so a falling tide UNCOVERS them.
  // Growth comes with age (a new pile is clean), rime and ice collars with winter.
  function frame(s) {
    const R = Math.max(0.3, s.tideRange), a = s.age, w = s.season === 'winter';
    return { R, w: s.tide, hhw: R, mid: R * 0.5, stainTop: R + 0.10, iceBot: R * 0.90, iceTop: Math.min(R + 0.14, R * 1.04 + 0.10),
      barnTop: R * 0.80, barnBot: R * (0.40 + 0.30 * (1 - clamp((a - 0.15) / 0.5, 0, 1))),
      weedTop: R * (0.10 + 0.30 * clamp((a - 0.1) / 0.5, 0, 1)), weedBot: R * 0.06,
      rimeBot: R - 0.3, rimeTop: R + 0.9, collar: R * 0.62,
      on: { stain: a > 0.08, barn: a > 0.2, weed: a > 0.15, ice: w || a > 0.4, rime: w } };
  }
  function matAtZ(z, base, T, s) {
    const g = s.growth, on = T.on; let k = base;
    if (g.stain && on.stain && z <= T.stainTop) k = base + 'S';
    if (g.barn && on.barn && z <= T.barnTop && z >= T.barnBot) k = base + 'B';
    if (g.weed && on.weed && z <= T.weedTop && z >= T.weedBot) k = base + 'G';
    if (g.ice && on.ice && z <= T.iceTop && z >= T.iceBot) k = base + 'I';
    if (on.rime && z >= T.rimeBot && z <= T.rimeTop && base !== 'weed') k = base + 'R';
    return k;
  }
  function zSplit(z0, z1, base, T, s) {
    const cuts = [z0, z1];
    for (const c of [T.stainTop, T.barnTop, T.barnBot, T.weedTop, T.weedBot, T.iceBot, T.iceTop, T.rimeBot, T.rimeTop]) if (c > z0 + 0.01 && c < z1 - 0.01) cuts.push(c);
    cuts.sort((a, b) => a - b); const segs = [];
    for (let i = 0; i < cuts.length - 1; i++) { const a = cuts[i], b = cuts[i + 1]; if (b - a < 0.008) continue; segs.push({ a, b, mat: matAtZ((a + b) / 2, base, T, s) }); }
    return segs.length ? segs : [{ a: z0, b: z1, mat: matAtZ((z0 + z1) / 2, base, T, s) }];
  }
  const isBarn = k => /B$/.test(k), isWeed = k => /G$/.test(k) || k === 'weed';
  function texFor(k, base) { return isBarn(k) ? crustTex() : isWeed(k) ? weedTex() : /R$/.test(k) ? iceTex() : (base === 'conc' ? formTex() : base === 'stone' ? rockTex() : base === 'block' ? blockTex() : grainTex(0.30)); }
  function bandedPile(out, x, y, r, z0, z1, base, T, s, n, capMat) {
    n = n || 10;
    for (const g of zSplit(z0, z1, base, T, s)) { const rr = isBarn(g.mat) ? r * 1.07 : isWeed(g.mat) ? r * 1.04 : r;
      tube(out, [x, y, g.a], [x, y, g.b], rr, n, g.mat, isBarn(g.mat) ? 0.22 : isWeed(g.mat) ? -0.35 : 0, texFor(g.mat, base), false); }
    const topPts = []; for (let i = 0; i < n; i++) { const t = (i / n) * Math.PI * 2; topPts.push([x + Math.cos(t) * r, y - Math.sin(t) * r]); }
    slab(out, topPts, z1, matAtZ(z1, base, T, s), 0.30, base === 'conc' ? formTex() : grainTex(0.22));
    if (capMat) { torusZ(out, x, y, z1 + 0.02, r * 0.98, 0.035, 10, 5, capMat, 0.2); pipe(out, x, y, r * 0.34, z1 + 0.02, z1 + 0.09, capMat, 0.3, 8); }
    if (s.growth.weed && T.on.weed && T.weedTop > z0 && T.weedTop < z1) weedFringe(out, x, y, r, T.weedTop, s);
    if (T.on.rime && s.season === 'winter' && T.collar > z0 + 0.2 && T.collar < z1 - 0.2) {          // the ice collar a winter tide leaves
      tube(out, [x, y, T.collar - 0.16], [x, y, T.collar + 0.10], r + 0.09, n, 'ice', 0.1, iceTex(), true); }
  }
  function weedFringe(out, x, y, r, zTop, s) {
    const rnd = mulberry32(((x * 911 + y * 577 + s.variant * 31) | 0) >>> 0), n = (s.season === 'winter' ? 2 : 5) + ((rnd() * 3) | 0), k = s.season === 'winter' ? 0.45 : 1;
    for (let i = 0; i < n; i++) { const th = rnd() * Math.PI * 2, len = (0.22 + rnd() * 0.34) * k, px = x + Math.cos(th) * r * 0.94, py = y + Math.sin(th) * r * 0.94, bx = px + Math.cos(th) * 0.05, by = py + Math.sin(th) * 0.05;
      tube(out, [px, py, zTop + 0.02], [bx, by, zTop - len * 0.55], 0.032, 5, 'weed', -0.3, null, false);
      tube(out, [bx, by, zTop - len * 0.55], [bx + Math.cos(th) * 0.03, by + Math.sin(th) * 0.03, zTop - len], 0.022, 5, 'weed', -0.7, null, false); }
  }
  function bandedWall(out, x0, y0, x1, y1, z0, z1, base, T, s, b, texOv) {
    for (const g of zSplit(z0, z1, base, T, s)) { const gr = isBarn(g.mat) || isWeed(g.mat) || /R$/.test(g.mat);
      wall(out, x0, y0, x1, y1, g.a, g.b, g.mat, gr ? texFor(g.mat, base) : (texOv || texFor(g.mat, base)), (b || 0) + (isBarn(g.mat) ? 0.20 : isWeed(g.mat) ? -0.32 : 0)); }
  }
  function sheetWall(out, x0, y0, x1, y1, z0, top, kind, T, s, b) {
    const base = kind === 'steel' ? (s.age > 0.45 ? 'rust' : 'steel') : 'pole';
    bandedWall(out, x0, y0, x1, y1, z0, top - 0.20, base, T, s, b || 0, kind === 'steel' ? sheetTex(0.62) : sheetWoodTex(0.30));
    const L = Math.hypot(x1 - x0, y1 - y0), ux = (x1 - x0) / L, uy = (y1 - y0) / L, ox = uy * 0.06, oy = -ux * 0.06;
    if (kind === 'steel') { const wz = Math.max(z0 + 0.3, top - 0.75);
      for (const g of zSplit(wz - 0.09, wz + 0.09, 'steel', T, s)) wall(out, x0 + ox, y0 + oy, x1 + ox, y1 + oy, g.a, g.b, g.mat, null, (b || 0) + 0.25);
      const n = Math.max(2, Math.round(L / 1.9)); for (let i = 0; i < n; i++) { const t = (i + 0.5) / n; pipe(out, x0 + (x1 - x0) * t + ox * 1.8, y0 + (y1 - y0) * t + oy * 1.8, 0.055, wz - 0.055, wz + 0.055, matAtZ(wz, 'steel', T, s), 0.35, 6); }
    } else { const n = Math.max(2, Math.round(L / 1.2)); for (let i = 0; i <= n; i++) { const t = i / n; bandedPile(out, x0 + (x1 - x0) * t + ox, y0 + (y1 - y0) * t + oy, 0.15, top - 1.4, top - 0.06, 'pole', T, s, 8, s.capIron ? 'iron' : null); } }
  }
  function blockWall(out, x0, y0, x1, y1, z0, top, T, s, b) { bandedWall(out, x0, y0, x1, y1, z0, top, 'block', T, s, b, blockTex()); }

  // ============================ CHARACTER v9.2 — the numbers the fittings are built to =========================
  // From export/character-v9.2-import-kit (reports/worldfit.txt, CONTRACT in characterIsoRig9.js, rig 02df29ec…).
  // rung 0.24: inside every creator body's 0.211–0.273, and three rungs (0.72) is a board step every creator body makes (0.549–0.724),
  // so the top-out from the ladder's top station (lower foot three rungs down) is the board clip at railZ 0.72, exact for all 375
  const CHAR = { rev: '9.2', rig: 'characterIsoRig9.js', rigSha: '02df29ec88dffe9ad68b8761290f4ed66ef73e919346682be43c089be6f49144',
    rung: 0.24, rungFit: [0.211, 0.273], rungClamp: [0.18, 0.45], ladderW: 0.45, standoff: 0.275, hands: 0.17,
    railFit: [0.549, 0.724], railClamp: [0.06, 1.30], reachLift: [0, 1.012], gripRise: 0.095, want: [0.132, 0.265],
    loadFit: [0.718, 0.978], radius: [0.141, 0.229], fisherR: 0.20, footprint: [0.458, 0.37], head: 1.80,
    clips: { climb: 'ladderDown (reverse to climb)', tie: 'reach', haul: 'haul', step: 'board / boardDown', walk: 'walk', swim: 'swim / tread' } };
  const FIT = {
    ladder: { w: 0.45, rung: CHAR.rung, rungR: 0.021, stile: [0.06, 0.05], off: 0.16, below: 1.00, grab: 1.00, gap: 0.75 },
    tyre: { od: 1.00, sec: 0.28 }, foam: { od: 0.55, len: 1.10 },
    cleat: { len: 0.50, h: 0.22, set: 0.30 }, bollard: { h: 0.75, rBase: 0.16, rTop: 0.11, flange: 0.24, set: 0.60 },
    ring: { od: 0.30, plate: 0.42, set: 0.30 }, rail: { h: 1.05, mid: 0.55, post: 0.10, span: 1.80, pipeR: 0.024 },
    bull: { h: 0.30, w: 0.20, block: 1.2, lift: 0.10 }, dolphin: { r: 0.175, spread: 0.55, above: 1.20 },
    panel: { w: 1.6, h: 2.2, t: 0.14, stand: 0.55 }, gang: { clear: 1.10, rail: 1.00, mid: 0.50, tread: 0.30, maxSlope: 18.43, steep: 24 },
  };

  // ---- fittings ----
  function ladderAt(out, x, yFace, side, deckZ, T, s, botZ) {
    const L = FIT.ladder, hw = L.w / 2, y = yFace + side * L.off; botZ = botZ == null ? -L.below : botZ;
    at('ladder', CTX.grp, 0);
    for (const sx of [-1, 1]) { const px = x + sx * (hw + L.stile[1] / 2);
      for (const g of zSplit(botZ, deckZ + L.grab, 'galv', T, s)) box(out, px - L.stile[1] / 2, px + L.stile[1] / 2, y - L.stile[0] / 2, y + L.stile[0] / 2, g.a, g.b, g.mat, 0.1, null, true);
      at('ladder', CTX.grp, 1);                                                          // the grab hoop over the deck edge
      beam(out, [px, y, deckZ + L.grab], [px, yFace - side * 0.22, deckZ + L.grab], 0.024, 'galv', 0.25, 6);
      beam(out, [px, yFace - side * 0.22, deckZ + L.grab], [px, yFace - side * 0.22, deckZ], 0.024, 'galv', 0.1, 6);
      at('ladder', CTX.grp, 0); }
    for (let z = deckZ - L.rung; z > botZ + 0.04; z -= L.rung) beam(out, [x - hw, y, z], [x + hw, y, z], L.rungR, matAtZ(z, 'galv', T, s), 0.3, 6);
    for (const zz of [deckZ - 0.45, Math.max(botZ + 0.4, T.hhw * 0.5)]) if (zz < deckZ && zz > botZ)
      box(out, x - hw - 0.03, x + hw + 0.03, Math.min(yFace, y) + (side > 0 ? 0 : 0.0), Math.max(yFace, y), zz - 0.04, zz + 0.04, matAtZ(zz, 'galv', T, s), 0.05, null, true);
  }
  function rungList(deckZ, botZ) { const a = []; for (let z = deckZ - FIT.ladder.rung; z > botZ + 0.04; z -= FIT.ladder.rung) a.push(+z.toFixed(3)); return a; }
  function tyreAt(out, x, yFace, side, deckZ, hangTop, T, s) {
    const t = FIT.tyre, R = (t.od - t.sec) / 2, r = t.sec / 2, cz = hangTop - t.od / 2, y = yFace + side * (r + 0.03);
    at('tyre', CTX.grp, 0); torusY(out, x, y, cz, R, r, 12, 6, 'rubber', 0);
    for (const sx of [-0.10, 0.10]) { const zTop = deckZ - 0.16, links = Math.max(1, Math.round((zTop - (cz + R)) / 0.09));
      for (let i = 0; i < links; i++) { const z0 = zTop - i * 0.09; torusZ(out, x + sx, yFace + side * 0.05, z0, 0.035, 0.013, 6, 4, 'galv', 0.1); } }
    return { x, y, z: cz, top: hangTop, pivot: [x, yFace + side * 0.05, deckZ - 0.16] };
  }
  function foamAt(out, x, yFace, side, hangTop, T, s) {
    const f = FIT.foam, r = f.od / 2, y = yFace + side * (r + 0.02), zTop = hangTop, zBot = hangTop - f.len;
    at('fender', CTX.grp, 0);
    for (const g of zSplit(zBot, zTop, 'foam', T, s)) tube(out, [x, y, g.a], [x, y, g.b], r * (g.b > zTop - 0.06 ? 0.86 : 1), 10, g.mat, 0, null, false);
    const top = []; for (let i = 0; i < 10; i++) { const t = (i / 10) * Math.PI * 2; top.push([x + Math.cos(t) * r * 0.86, y - Math.sin(t) * r * 0.86]); }
    slab(out, top, zTop, 'foam', 0.3); pipe(out, x, y, 0.030, zTop, zTop + 0.16, 'galv', 0.2, 8);
    beam(out, [x, y, zTop + 0.14], [x, yFace + side * 0.04, zTop + 0.30], 0.024, 'rope', 0.1, 5);
  }
  function panelAt(out, x, yFace, side, deckZ, T, s) {                         // steel-faced panel on two cone fenders
    const P = FIT.panel, y0 = yFace + side * P.stand, zc = Math.max(T.mid, deckZ - P.h / 2 - 0.35), hw = P.w / 2;
    at('fender', CTX.grp, 0);
    for (const dz of [-P.h * 0.26, P.h * 0.26]) tube(out, [x, yFace + side * 0.02, zc + dz], [x, yFace + side * (P.stand - 0.02), zc + dz], 0.34, 10, 'rubber', -0.2, null, true);
    const ya = y0, yb = y0 + side * P.t;
    box(out, x - hw, x + hw, Math.min(ya, yb), Math.max(ya, yb), zc - P.h / 2, zc + P.h / 2, matAtZ(zc, s.age > 0.5 ? 'rust' : 'steel', T, s), 0.05, null, false);
    decalY(out, yb, side, x - hw + 0.06, x + hw - 0.06, zc - P.h / 2 + 0.06, zc + P.h / 2 - 0.06, matAtZ(zc, 'hdpe', T, s), 0.1, panelTex(), 0.03);
    for (const sx of [-1, 1]) { let zc2 = deckZ - 0.12; for (let i = 0; i < 6 && zc2 > zc + P.h / 2; i++, zc2 -= 0.09) torusZ(out, x + sx * hw * 0.7, yFace + side * 0.3, zc2, 0.035, 0.013, 6, 4, 'galv', 0.1); }
  }
  function cleatAt(out, x, y, deckZ, ang) {
    const c = FIT.cleat, hl = c.len / 2, ca = Math.cos(ang || 0), sa = Math.sin(ang || 0), P = (dx, dy) => [x + dx * ca - dy * sa, y + dx * sa + dy * ca];
    at('cleat', CTX.grp, 0);
    box(out, x - hl * 0.62, x + hl * 0.62, y - 0.085, y + 0.085, deckZ, deckZ + 0.045, 'iron', -0.55);
    for (const sx of [-1, 1]) { const p = P(sx * hl * 0.46, 0); pipe(out, p[0], p[1], 0.038, deckZ + 0.03, deckZ + c.h - 0.045, 'galv', 0.15, 8); }
    const a = P(-hl, 0), b = P(hl, 0); tube(out, [a[0], a[1], deckZ + c.h - 0.025], [b[0], b[1], deckZ + c.h - 0.025], 0.048, 8, 'galv', 0.3, null, true);
  }
  function bollardAt(out, x, y, deckZ) {
    const B = FIT.bollard; at('bollard', CTX.grp, 1);
    pipe(out, x, y, B.flange, deckZ, deckZ + 0.05, 'iron', 0.05, 12);
    for (let i = 0; i < 4; i++) { const z0 = deckZ + 0.05 + i * (B.h - 0.16) / 4, z1 = z0 + (B.h - 0.16) / 4, r0 = B.rBase + (B.rTop - B.rBase) * (i / 4);
      tube(out, [x, y, z0], [x, y, z1], r0, 12, i === 2 ? 'yel' : 'iron', i === 2 ? 0.3 : 0, null, false); }
    tube(out, [x, y, deckZ + B.h - 0.16], [x, y, deckZ + B.h - 0.05], B.rTop * 1.24, 12, 'iron', 0.15, null, false);
    pipe(out, x, y, B.rTop * 1.24, deckZ + B.h - 0.05, deckZ + B.h, 'iron', 0.35, 12);
  }
  function ringAt(out, x, y, deckZ) { const R = FIT.ring; at('ring', CTX.grp, 0);
    decalZ(out, deckZ + 0.004, x - R.plate / 2, x + R.plate / 2, y - R.plate * 0.34, y + R.plate * 0.34, 'iron', -0.45, rustTex(), 0.04);
    torusZ(out, x, y, deckZ + 0.03, R.od / 2 - 0.022, 0.022, 12, 5, 'iron', 0.05); }
  function railRun(out, x0, y0, x1, y1, deckZ, kind) {
    const R = FIT.rail, L = Math.hypot(x1 - x0, y1 - y0); if (L < 0.3) return; const n = Math.max(2, Math.round(L / R.span) + 1);
    at('rail', CTX.grp, 1);
    for (let i = 0; i < n; i++) { const t = i / (n - 1), px = x0 + (x1 - x0) * t, py = y0 + (y1 - y0) * t;
      if (kind === 'pipe') pipe(out, px, py, R.pipeR * 1.5, deckZ, deckZ + R.h, 'galv', 0.05, 8); else box(out, px - R.post / 2, px + R.post / 2, py - R.post / 2, py + R.post / 2, deckZ, deckZ + R.h, 'wood', 0.05, sawnTex()); }
    for (const z of [R.h, R.mid]) { if (kind === 'pipe') tube(out, [x0, y0, deckZ + z], [x1, y1, deckZ + z], R.pipeR, 8, 'galv', 0.2, null, true);
      else { const ux = (x1 - x0) / L, uy = (y1 - y0) / L; box(out, Math.min(x0, x1) - 0.02 - uy * 0.035, Math.max(x0, x1) + 0.02 - uy * 0.035, Math.min(y0, y1) - 0.055 + ux * 0.035, Math.max(y0, y1) + 0.055 + ux * 0.035, deckZ + z - 0.045, deckZ + z + 0.045, 'wood', 0.22, sawnTex()); } }
  }
  // the bull rail: a timber rail on spacer blocks, so a line passes under it; broken wherever the edge is used
  function bullRail(out, x0, x1, y, deckZ, mat, gaps) {
    const B = FIT.bull, runs = cutRuns(x0, x1, gaps); at('curb', CTX.grp, 0);
    for (const [a, b] of runs) { if (b - a < 0.25) continue;
      box(out, a, b, y - B.w / 2, y + B.w / 2, deckZ + B.lift, deckZ + B.h, mat, 0.18, sawnTex());
      const n = Math.max(1, Math.round((b - a) / B.block)); for (let i = 0; i <= n; i++) { const x = a + 0.12 + (b - a - 0.24) * (n ? i / n : 0.5);
        box(out, x - 0.12, x + 0.12, y - B.w / 2 + 0.02, y + B.w / 2 - 0.02, deckZ, deckZ + B.lift, mat, -0.3, null, true); } }
  }
  function cutRuns(x0, x1, gaps) { let runs = [[x0, x1]];
    for (const g of (gaps || [])) { const nx = []; for (const [a, b] of runs) { if (g[1] <= a || g[0] >= b) { nx.push([a, b]); continue; } if (g[0] > a) nx.push([a, g[0]]); if (g[1] < b) nx.push([g[1], b]); } runs = nx; }
    return runs; }
  function dolphinCluster(out, x, y, topZ, T, s, n) {
    const D = FIT.dolphin; n = n || 3; at('dolphin', 0, 0);
    for (let i = 0; i < n; i++) { const th = i * (Math.PI * 2 / n) - Math.PI / 2, bx = x + Math.cos(th) * D.spread, by = y + Math.sin(th) * D.spread, tx = x + Math.cos(th) * 0.16, ty = y + Math.sin(th) * 0.16, bed = s.bedAt(bx, by);
      for (const g of zSplit(bed, topZ, 'pole', T, s)) { const f0 = (g.a - bed) / (topZ - bed), f1 = (g.b - bed) / (topZ - bed);
        tube(out, [bx + (tx - bx) * f0, by + (ty - by) * f0, g.a], [bx + (tx - bx) * f1, by + (ty - by) * f1, g.b], isBarn(g.mat) ? D.r * 1.08 : D.r, 9, g.mat, isBarn(g.mat) ? 0.3 : 0, texFor(g.mat, 'pole'), false); } }
    for (const z of [topZ - 0.65, topZ - 1.45, Math.max(s.bedMin + 0.5, T.mid)]) if (z > s.bedMin && z < topZ) torusZ(out, x, y, z, D.spread * 0.62, 0.045, 12, 5, 'galv', 0.15);
    pipe(out, x, y, D.spread * 0.52, topZ, topZ + 0.10, 'wood', 0.3, 10);
  }
  function rubbleStone(out, x, y, zBase, r, mat, rnd, bias, tex) {
    const n = 5 + ((rnd() * 3) | 0), rot = rnd() * Math.PI * 2, sq = 0.74 + rnd() * 0.46, h = r * (0.40 + rnd() * 0.34), zb = zBase - r * (0.26 + rnd() * 0.22), base = [], top = [];
    for (let i = 0; i < n; i++) { const a = rot + (i / n) * Math.PI * 2 + (rnd() - 0.5) * 0.30, rr = r * (0.78 + rnd() * 0.30); base.push([x + Math.cos(a) * rr, y - Math.sin(a) * rr * sq]);
      const rt = rr * (0.62 + rnd() * 0.28); top.push([x + Math.cos(a) * rt + (rnd() - 0.5) * r * 0.16, y - Math.sin(a) * rt * sq + (rnd() - 0.5) * r * 0.16, zBase + h * (0.74 + rnd() * 0.34)]); }
    const tx = tex || rockTex();
    for (let i = 0; i < n; i++) { const j = (i + 1) % n; out.push(F([[base[i][0], base[i][1], zb], [base[j][0], base[j][1], zb], top[j], top[i]], mat, (bias || 0) - 0.35 + rnd() * 0.8, 0, [[0, 0], [r * 1.6, 0], [r * 1.6, h], [0, h]], tx)); }
    const apex = [x + (rnd() - 0.5) * r * 0.42, y + (rnd() - 0.5) * r * 0.42, zBase + h * (0.94 + rnd() * 0.20)];
    for (let i = 0; i < n; i++) { const j = (i + 1) % n; out.push(F([top[i], top[j], apex], mat, (bias || 0) + 0.05 + rnd() * 0.55, 0, [[0, 0], [r, 0], [r * 0.5, r]], tx)); }
    return apex[2];
  }

  // ============================ DECKS ========================================================================
  const DECK = { plank: 0.055, stringer: [0.10, 0.30], cap: [0.30, 0.25] };
  // planks run ACROSS the module (seams along +x, 0.20 m); a derelict deck loses boards — returned as holes
  function plankDeck(out, x0, x1, y0, y1, topZ, s, T) {
    const t = DECK.plank, holes = [];
    at('deck', CTX.grp, 0);
    if (s.age > 0.8 && s.family !== 'float' && s.family !== 'finger') {
      const rnd = mulberry32(s.variant * 131 + 7), n = Math.round((x1 - x0) / 0.2), gone = new Set();
      const k = Math.max(1, Math.round((x1 - x0) / 5));
      for (let i = 0; i < k; i++) { const b0 = 2 + Math.floor(rnd() * (n - 4)), w = 1 + Math.floor(rnd() * 2); for (let j = 0; j < w; j++) gone.add(b0 + j); }
      let run = null;
      for (let i = 0; i <= n; i++) { const xa = x0 + i * 0.2, dead = gone.has(i) || i === n;
        if (!dead && run == null) run = xa;
        if (dead && run != null) { slab(out, [[run, y0], [xa, y0], [xa, y1], [run, y1]], topZ, 'plank', 0.12, plankTex(0.20)); run = null; }
        if (gone.has(i) && i < n) { const ya = y0 + (y1 - y0) * (0.15 + rnd() * 0.3), yb = y1 - (y1 - y0) * (0.1 + rnd() * 0.25);
          slab(out, [[xa, y0], [xa + 0.2, y0], [xa + 0.2, ya], [xa, ya]], topZ, 'plank', 0.12, plankTex(0.20));
          slab(out, [[xa, yb], [xa + 0.2, yb], [xa + 0.2, y1], [xa, y1]], topZ - 0.02, 'plank', 0.12, plankTex(0.20));
          holes.push([+xa.toFixed(2), +ya.toFixed(2), +(xa + 0.2).toFixed(2), +yb.toFixed(2)]); } }
    } else slab(out, [[x0, y0], [x1, y0], [x1, y1], [x0, y1]], topZ, 'plank', 0.12, plankTex(0.20));
    wall(out, x0, y1, x1, y1, topZ - t, topZ, 'plank', grainTex(0.20), -0.1); wall(out, x1, y0, x0, y0, topZ - t, topZ, 'plank', grainTex(0.20), -0.35);
    if (s.ends.px !== 'join') wall(out, x1, y1, x1, y0, topZ - t, topZ, 'plank', grainTex(0.20), 0.0);
    if (s.ends.nx !== 'join') wall(out, x0, y0, x0, y1, topZ - t, topZ, 'plank', grainTex(0.20), -0.2);
    return holes;
  }
  function underFrame(out, x0, x1, y0, y1, topZ, s, T) {
    const st = DECK.stringer, z1 = topZ - DECK.plank, z0 = z1 - st[1], n = Math.max(2, Math.round((y1 - y0) / 0.75) + 1);
    at('frame', CTX.grp, 0);
    for (let i = 0; i < n; i++) { const t = i / (n - 1), y = y0 + 0.09 + (y1 - y0 - 0.18) * t;
      for (const g of zSplit(z0, z1, 'wood', T, s)) box(out, x0, x1, y - st[0] / 2, y + st[0] / 2, g.a, g.b, g.mat, -0.55, sawnTex(), true); }
  }
  // icicles off a winter deck edge, keyed on the variant so they stay put frame to frame
  function icicles(out, x0, x1, y, zTop, s) {
    if (s.season !== 'winter') return; const rnd = mulberry32(s.variant * 997 + Math.round(x0 * 10) + 3), n = Math.round((x1 - x0) * 1.3); at('ice', CTX.grp, 0);
    for (let i = 0; i < n; i++) { if (rnd() < 0.35) continue; cone(out, x0 + (x1 - x0) * rnd(), y, zTop, 0.02 + rnd() * 0.02, 0.10 + rnd() * rnd() * 0.45, 'ice', 5); }
  }

  // ============================ THE FLEET ====================================================================
  // loa / beam / draft from the hull rigs and their sidecars (shipyard SHIPS, hull.* in the gameplay files);
  // gun = gunwale / rail top above the waterline at the boarding point, sole = the deck a figure stands on,
  // both above the waterline — measured off the rigs where they publish it, else the class figure (src 'est').
  const HULLS = [
    { id: 'otter', label: 'Otter 8×8 amphib', rig: 'AmphibIso', loa: 3.1, beam: 1.56, draft: 0.52, gun: 0.48, sole: 0.20, cls: 'A', src: 'sidecar', slip: true },
    { id: 'dory', label: 'dory', rig: 'DoryIso', loa: 4.5, beam: 1.50, draft: 0.26, gun: 0.34, sole: -0.14, cls: 'A', src: 'shipyard' },
    { id: 'punt', label: 'punt', rig: 'PuntIso', loa: 5.2, beam: 1.58, draft: 0.28, gun: 0.28, sole: -0.16, cls: 'A', src: 'shipyard' },
    { id: 'bowrider', label: 'bowrider', rig: 'BowriderIso', loa: 5.64, beam: 2.29, draft: 0.40, gun: 0.62, sole: 0.02, cls: 'A', src: 'rig L' },
    { id: 'frc', label: '6.5 m FRC', rig: 'ZodiacIso', loa: 6.85, beam: 2.50, draft: 0.40, gun: 0.55, sole: 0.10, cls: 'A', src: 'rig loa' },
    { id: 'skiff', label: 'console skiff', rig: 'ConsoleIso', loa: 7.0, beam: 2.30, draft: 0.42, gun: 0.40, sole: 0.02, cls: 'A', src: 'shipyard' },
    { id: 'sportSkiff', label: 'sport skiff', rig: 'SportSkiffIso', loa: 7.0, beam: 2.54, draft: 0.45, gun: 0.60, sole: 0.12, cls: 'A', src: 'sidecar' },
    { id: 'hurricane', label: '7.3 m Hurricane', rig: 'ZodiacIso', loa: 7.47, beam: 2.60, draft: 0.45, gun: 0.58, sole: 0.12, cls: 'A', src: 'rig loa' },
    { id: 'lobsterIn', label: 'lobster boat, inshore', rig: 'LobsterBoatVariantsIso', loa: 8.6, beam: 3.55, draft: 0.95, gun: 0.95, sole: 0.25, cls: 'B', src: 'sidecar' },
    { id: 'sloop30', label: 'sloop 30', rig: 'SloopIso', loa: 9.4, beam: 2.98, draft: 1.90, gun: 0.85, sole: 0.60, cls: 'B', src: 'sidecar' },
    { id: 'goFast', label: 'go-fast', rig: 'GoFastIso', loa: 11.9, beam: 2.50, draft: 0.80, gun: 0.70, sole: 0.25, cls: 'B', src: 'rig L' },
    { id: 'lobster', label: 'lobster boat', rig: 'LobsterBoatIso', loa: 12.0, beam: 4.44, draft: 1.35, gun: 1.10, sole: 0.35, cls: 'B', src: 'shipyard' },
    { id: 'cape', label: 'Cape Islander', rig: 'CapeIslanderIso', loa: 12.8, beam: 4.40, draft: 1.40, gun: 1.15, sole: 0.40, cls: 'B', src: 'shipyard' },
    { id: 'lobsterOff', label: 'lobster boat, offshore', rig: 'LobsterBoatVariantsIso', loa: 14.6, beam: 5.05, draft: 1.55, gun: 1.20, sole: 0.45, cls: 'B', src: 'sidecar' },
    { id: 'sportFisher', label: 'sport fisher, convertible', rig: 'SportFisherIso2', loa: 16.2, beam: 5.16, draft: 1.40, gun: 1.10, sole: 0.55, cls: 'C', src: 'sidecar' },
    { id: 'dragger', label: 'side dragger', rig: 'SideDraggerIso', loa: 25.0, beam: 7.00, draft: 2.55, gun: 1.75, sole: 1.10, cls: 'C', src: 'shipyard' },
    { id: 'sloop88', label: 'sloop 88', rig: 'Sloop88Iso', loa: 27.0, beam: 6.70, draft: 4.60, gun: 1.45, sole: 1.20, cls: 'C', src: 'sidecar' },
    { id: 'skybridge', label: 'sport fisher, skybridge', rig: 'SportFisherIso2', loa: 27.4, beam: 7.32, draft: 1.90, gun: 1.60, sole: 1.05, cls: 'C', src: 'sidecar' },
    { id: 'trawler', label: 'stern trawler', rig: 'SternTrawlerIso', loa: 38.0, beam: 9.00, draft: 3.90, gun: 2.60, sole: 1.70, cls: 'D', src: 'rig L' },
    { id: 'trawlerMk2', label: 'stern trawler Mk II', rig: 'SternTrawlerMk2Iso', loa: 38.0, beam: 9.00, draft: 3.90, gun: 2.60, sole: 1.70, cls: 'D', src: 'shipyard' },
    { id: 'packet', label: 'coastal packet', rig: 'CoastalPacketIso', loa: 60.0, beam: 10.40, draft: 4.20, gun: 2.90, sole: 1.90, cls: 'D', src: 'shipyard' },
    { id: 'tanker', label: 'gas tanker', rig: 'TankerIso', loa: 110.0, beam: 17.40, draft: 6.20, gun: 3.40, sole: 2.40, cls: 'E', src: 'shipyard' },
  ];
  const HULL = {}; HULLS.forEach(h => { HULL[h.id] = h; });
  // berth classes: end clearance (each end), side clearance, under-keel clearance at chart datum, line, fender, where
  const CLASSES = {
    A: { label: 'small craft', maxLoa: 8, end: 0.6, side: 0.45, ukc: 0.3, line: '12–16 mm', moor: 'cleat · ring', fender: 'foam', at: ['finger', 'float', 'pier', 'crib', 'slipway'] },
    B: { label: 'inshore', maxLoa: 15, end: 1.2, side: 0.6, ukc: 0.5, line: '16–22 mm', moor: 'cleat', fender: 'tyre · foam', at: ['float', 'pier', 'crib', 'quay'] },
    C: { label: 'midshore', maxLoa: 28, end: 2.0, side: 1.0, ukc: 0.6, line: '22–28 mm', moor: 'bollard · cleat', fender: 'tyre · fender pile', at: ['pier', 'crib', 'quay'] },
    D: { label: 'large', maxLoa: 60, end: 4.0, side: 1.5, ukc: 0.8, line: '32–40 mm', moor: 'bollard', fender: 'panel', at: ['quay'] },
    E: { label: 'terminal', maxLoa: 120, end: 10.0, side: 2.0, ukc: 1.5, line: '40 mm + wires', moor: 'mooring dolphin', fender: 'breasting dolphin panel', at: ['jetty'] },
  };
  const slotOf = (h) => h.loa + 2 * CLASSES[h.cls].end;
  // Pack a face with the best MIX of hulls: the most length used, then the biggest hulls served (the sum of loa²) — two mixes
  // within tol of each other (12 % of the face, 0.6 m at least) count as equal, so deep water goes to the boats that need it
  function packBerths(rem, pool, depth, memo, sl, tol) {
    sl = sl || slotOf; tol = tol || 0.6; if (depth > 7 || rem < 3) return [];
    const key = Math.round(rem * 5) + '|' + depth; if (memo[key]) return memo[key];
    let best = [], bestUsed = 0, bestSq = 0;
    for (const h of pool) { const slot = sl(h); if (slot > rem + 1e-6) continue;
      const rest = packBerths(rem - slot, pool, depth + 1, memo, sl, tol), used = slot + rest.reduce((a, c) => a + sl(c), 0), sq = h.loa * h.loa + rest.reduce((a, c) => a + c.loa * c.loa, 0);
      if (used > bestUsed + tol || (used > bestUsed - tol && sq > bestSq)) { bestUsed = Math.max(bestUsed, used); bestSq = sq; best = [h].concat(rest); } }
    memo[key] = best; return best;
  }
  // Berths along one face [x0, x1] of a module (or a chain of them). kind: the face type in CLASSES[].at.
  // want: 'auto' | hull id | [hull ids] | class letter. depth: water at chart datum (−bed).
  function berthPlan(x0, x1, kind, want, depth, range) {
    const L = x1 - x0, okDepth = (h) => depth == null || depth >= h.draft + CLASSES[h.cls].ukc - 1e-6, fin = kind === 'finger', sl = fin ? (h) => h.loa * 0.8 + 0.3 : slotOf;
    let pool = HULLS.filter(h => CLASSES[h.cls].at.indexOf(kind) >= 0 && h.id !== 'otter');
    if (typeof want === 'string' && CLASSES[want]) pool = pool.filter(h => h.cls === want);
    let pack;
    if (Array.isArray(want)) pack = want.map(id => HULL[id]).filter(Boolean);
    else if (typeof want === 'string' && HULL[want]) { const h = HULL[want]; pack = []; let u = 0; while (u + sl(h) <= L + 1e-6 && pack.length < 12) { pack.push(h); u += sl(h); } if (!pack.length) pack = [h]; }
    // afloat at chart datum first; else, given the tide range, hulls that float for part of the tide (a drying berth); else none
    else { const afloat = pool.filter(okDepth), drying = range != null && depth != null ? pool.filter(h => depth + range - 0.2 >= h.draft + CLASSES[h.cls].ukc) : pool;
      pack = packBerths(L, afloat.length ? afloat : drying, 0, {}, sl, Math.max(0.6, 0.12 * L)).slice().sort((a, b) => b.loa - a.loa); if (!pack.length && pool.length && range == null) pack = [pool[0]]; }
    const used = pack.reduce((a, h) => a + sl(h), 0), gap = fin ? 0 : Math.max(0, (L - used) / (pack.length + 1));
    let cur = x0 + gap; const list = [];
    pack.forEach((h, i) => { const slot = sl(h), c = fin ? cur + h.loa / 2 : cur + slot / 2, C = CLASSES[h.cls];
      list.push({ id: 'berth' + i, hull: h.id, label: h.label, cls: h.cls, loa: h.loa, beam: h.beam, draft: h.draft, gun: h.gun, sole: h.sole,
        x: +c.toFixed(2), x0: +(c - h.loa / 2).toFixed(2), x1: +(c + h.loa / 2).toFixed(2), slot: [+cur.toFixed(2), +(cur + slot).toFixed(2)],
        fits: cur + slot <= x1 + 1e-6, afloatAtDatum: okDepth(h), ukc: C.ukc, line: C.line, fender: C.fender,
        fenderX: h.cls === 'A' ? [+(c - h.loa * 0.25).toFixed(2), +(c + h.loa * 0.25).toFixed(2)] : [+(c - h.loa * 0.32).toFixed(2), +c.toFixed(2), +(c + h.loa * 0.32).toFixed(2)],
        lines: [+(c - h.loa / 2 - 0.6).toFixed(2), +(c - h.loa * 0.15).toFixed(2), +(c + h.loa * 0.15).toFixed(2), +(c + h.loa / 2 + 0.6).toFixed(2)] });
      cur += slot + gap; });
    return { list, faceLen: +L.toFixed(2), kind, utilisation: L > 0 ? +(used / L).toFixed(2) : 0, overflow: used > L + 1e-6 };
  }
  // Which berths take which hull: the fleet against the families and the depth they are given
  function fleetFit(depthByKind) {
    return HULLS.map(h => { const C = CLASSES[h.cls], need = +(h.draft + C.ukc).toFixed(2);
      return { hull: h.id, label: h.label, cls: h.cls, loa: h.loa, beam: h.beam, draft: h.draft, slot: +slotOf(h).toFixed(2), needDepth: need, at: C.at.slice(),
        line: C.line, fender: C.fender, moor: C.moor, afloat: C.at.map(k => ({ at: k, ok: depthByKind && depthByKind[k] != null ? depthByKind[k] >= need : null })) }; });
  }

  root.WharfGeo2 = { DEG, clamp, h2r, r2h, mix, desat, r5, mulberry32, hash2, SIX, MATDEF, makeMats, CTX, at, F, wall, slab, box, tube, pipe, beam, cone, torusZ, torusY, decalY, decalZ,
    plankTex, grainTex, sawnTex, formTex, ribTex, rockTex, crustTex, weedTex, treadTex, rustTex, sheetTex, sheetWoodTex, cubeTex, gratingTex, blockTex, paveTex, panelTex, slabTex, iceTex,
    frame, matAtZ, zSplit, isBarn, isWeed, texFor, bandedPile, weedFringe, bandedWall, sheetWall, blockWall,
    CHAR, FIT, ladderAt, rungList, tyreAt, foamAt, panelAt, cleatAt, bollardAt, ringAt, railRun, bullRail, cutRuns, dolphinCluster, rubbleStone,
    DECK, plankDeck, underFrame, icicles, HULLS, HULL, CLASSES, slotOf, packBerths, berthPlan, fleetFit };
})(typeof globalThis !== 'undefined' ? globalThis : window);
