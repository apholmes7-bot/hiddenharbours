/* Hidden Harbours — WHARF BUILDINGS, pass 2 · GEOMETRY (wharfBuildingRig2.geo.js → globalThis.WharfBuildingGeo2), 2026-09-26.
   Load before wharfBuildingRig2.js. Pass 1 (Art/wharfBuildingRig.js, globalThis.WharfBuilding) is untouched.

   The net shed, the storage barn and the fish plant, rebuilt the way the phase 5 outbuildings are built: real walls with
   openings (reveals, inside faces, cut caps), doors that open, furnished interiors, a loft over the gear floor, and every
   fixture a figure uses built to a v9.2 number. Faces are the shared record {v, mat, b, db, uv, tex, tag, em, fire}; model
   metres, +Y the gable with the main door (the show face), +X the long wall with the dock, z up from the ground.
   Weathering is laid in model space (textures and material patches keyed on the surface), so it does not swim between
   facings. assemble(o, dir) returns C: faces, plan segments, solids, ladders, stairs, stations, lamps, stacks.       */
(function (root) {
  'use strict';
  const DEG = Math.PI / 180, DEG100 = 100 * DEG;
  const clamp = (v, a, b) => v < a ? a : v > b ? b : v, r3 = (v) => Math.round(v * 1000) / 1000;
  function hsh(a, b, s) { let h = Math.imul(a | 0, 374761393) + Math.imul(b | 0, 668265263) + Math.imul(s | 0, 1274126177) | 0; h ^= h >>> 13; h = Math.imul(h, 1103515245) | 0; h ^= h >>> 16; return (h >>> 0) / 4294967296; }
  const h2 = (h) => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];
  const r2h = (r, g, b) => '#' + [r, g, b].map(n => Math.max(0, Math.min(255, Math.round(n))).toString(16).padStart(2, '0')).join('');
  const mix = (a, b, t) => { const A = h2(a), B = h2(b); return r2h(A[0] + (B[0] - A[0]) * t, A[1] + (B[1] - A[1]) * t, A[2] + (B[2] - A[2]) * t); };
  const desat = (c, t) => { const [r, g, b] = h2(c), l = 0.3 * r + 0.59 * g + 0.11 * b; return r2h(r + (l - r) * t, g + (l - g) * t, b + (l - b) * t); };
  const md = (a, m) => a - Math.floor(a / m) * m;

  // ---- palettes: pass 1's ramps kept (bodies, roofs, steel), the yard's for wood and interiors ----------------------------
  const BODY = {
    greyShingle: ['#4c463f', '#5d564c', '#6f665a', '#82786a', '#968b7b', '#a99d8c'], white: ['#8c928c', '#a6aaa2', '#bfc2b9', '#d5d8cf', '#e7e9e0', '#f3f4ec'],
    cream: ['#8a6f3c', '#a6884b', '#c2a35f', '#d8bd7c', '#e9d59d', '#f5e7c1'], red: ['#4a130f', '#671b14', '#88271c', '#a33124', '#bd4230', '#d25a42'],
    sage: ['#3a4636', '#4a5843', '#5c6b52', '#718063', '#889777', '#a1ae90'], blue: ['#33454a', '#43585d', '#556d72', '#6a848a', '#849ea3', '#a3b9bd'],
    rustOrange: ['#5c2a10', '#78380f', '#95491a', '#b05c27', '#c67338', '#d98d4f'], mustard: ['#5e4a12', '#7c6119', '#987a26', '#b39440', '#c8ab5e', '#dbc182'],
    teal: ['#143a38', '#1f4d4a', '#2c625e', '#3b7872', '#4d8f88', '#66a69d'], galv: ['#464d51', '#5a6267', '#727c81', '#8c979c', '#a6b1b5', '#c2cccf'],
    rustMetal: ['#3a1c10', '#552a17', '#6e3a22', '#8a4e2f', '#a5643f', '#bd7d52'],
  };
  const TRIM = ['#9aa09a', '#b4b8b0', '#ccd0c7', '#e0e2da', '#eef0e8', '#f8f9f2'];
  const STEEL = ['#2a2f33', '#3c454b', '#525c63', '#6d777e', '#889298', '#a2acb1'];
  const PAINT = { white: TRIM, red: BODY.red, blue: ['#1f3140', '#2a4152', '#365366', '#44667b', '#577b91', '#6d92a8'], green: ['#1c2f27', '#243d33', '#2f4d41', '#3c5f51', '#4d7464', '#638a79'],
    iron: ['#16181b', '#1f2226', '#2a2e33', '#373c42', '#454b52', '#555c64'], teal: BODY.teal, mustard: BODY.mustard, steel: STEEL, galv: BODY.galv };
  const ROOFS = { asphaltGrey: ['#23262b', '#2e333a', '#3c424a', '#4c535c', '#5d6570', '#6f7883'], asphaltBrown: ['#2a211a', '#3a2e23', '#4c3d2e', '#5f4d3a', '#736046', '#877254'],
    metalSeam: ['#424d52', '#556065', '#6c7c81', '#88999e', '#a4babe', '#c0d4d7'], corrugated: BODY.galv, rusted: ['#3a1c10', '#4f2614', '#6a3620', '#84462b', '#9c5b3a', '#b2724c'] };
  const RAMP = {
    stone: ['#33343a', '#42444b', '#54575d', '#666a70', '#7a7e84', '#8e9298'], conc: ['#4b4a45', '#5d5c56', '#706f68', '#84837b', '#98968d', '#aba99f'],
    cinder: ['#4a4842', '#5f5d55', '#77746a', '#8f8b7f', '#a4a094', '#b8b3a6'], steel: STEEL, galv: BODY.galv, rust: ROOFS.rusted,
    wood: ['#3f3a33', '#514a40', '#645b4f', '#786e60', '#8c8172', '#a19585'], timber: ['#4f3a24', '#63492d', '#785a39', '#8f7049', '#a6875d', '#bd9f74'],
    inside: ['#3a2e22', '#4a3b2c', '#5c4a37', '#6f5a43', '#836c51', '#977f62'], insideP: ['#4d5356', '#5f666a', '#737a7e', '#888f93', '#9ea5a8', '#b3babd'],
    floor: ['#3b3025', '#4b3d2f', '#5d4c3a', '#705c47', '#846e56', '#988066'], concF: ['#3e403f', '#4d504e', '#5e615f', '#707371', '#838684', '#969997'],
    cap: ['#8e8778', '#a79f8e', '#bdb5a2', '#d0c8b4', '#dfd8c5', '#ebe5d4'], rope: ['#5a4526', '#71592f', '#8a6f3d', '#a2874e', '#b89f66', '#cdb686'],
    net: ['#26302c', '#313d38', '#3d4a44', '#4a5851', '#586860', '#687870'], sign: ['#7d7566', '#8f8878', '#a89e88', '#c0b69e', '#d4cbb4', '#e4dcc6'],
    dark: ['#0e1114', '#151a1e', '#1d2429', '#252d33', '#2e373e', '#38434b'], glass: ['#27373d', '#33474d', '#40585f', '#50696f', '#62797e', '#7a9095'],
    glassHi: ['#6f8b90', '#86a2a6', '#a1bcbf', '#bcd4d6', '#cfe6e8', '#e2f2f3'], cp_warm: ['#5c3a1a', '#93602b', '#c88b3d', '#e6b35a', '#f6d68b', '#fff0c2'],
    cp_iron: ['#16181b', '#1f2226', '#2a2e33', '#373c42', '#454b52', '#555c64'], soot: ['#1a1716', '#221e1c', '#2b2624', '#35302d', '#403a37', '#4c4542'],
    moss: ['#2c3520', '#374329', '#435233', '#51623d', '#607348', '#708554'], bare: ['#3d3a34', '#4f4a42', '#625c52', '#766f64', '#8b8478', '#a09a8d'],
    tote: ['#1c3346', '#244259', '#2e526c', '#3a6380', '#4a7695', '#5d8aab'], box: ['#5a2410', '#7a3316', '#9b451e', '#b95a2a', '#d0723b', '#e28c52'],
    ice: ['#5f7880', '#7b949b', '#98b0b6', '#b5cbd0', '#d0e2e6', '#e8f3f5'], trapW: ['#23331f', '#2d4127', '#385031', '#44603b', '#527148', '#628356'],
    buoyR: ['#4a1210', '#681915', '#8a241c', '#a83226', '#c24533', '#d65d47'], buoyY: ['#6a5210', '#8c6d17', '#ad8a22', '#c9a533', '#dcbd4f', '#ead375'],
    buoyO: ['#6a2c0c', '#8c3c12', '#ae501a', '#c96626', '#dc7e38', '#ea9852'], buoyW: ['#7b807a', '#959992', '#afb2a9', '#c6c9bf', '#d9dbd1', '#e8e9e0'],
    roofGalv: BODY.galv,
  };
  // weather props by material: [porosity (rain darkening), gloss (wet glint), snow hold] — CoastalPass.light reads these
  const WPROP = { body: [0.22, 0.05, 0.3], bodyMetal: [0.02, 0.5, 0.3], trim: [0.12, 0.12, 0.8], door: [0.08, 0.3, 0.2], roofAsph: [0.12, 0.55, 1], roofMetal: [0.02, 0.6, 0.55],
    stone: [0.32, 0.1, 0.9], conc: [0.28, 0.1, 0.9], cinder: [0.3, 0.08, 0.9], wood: [0.36, 0.08, 1], timber: [0.3, 0.1, 1], steel: [0.02, 0.6, 0.6], galv: [0.02, 0.55, 0.5],
    rust: [0.06, 0.2, 0.75], inside: [0.2, 0.05, 0.3], insideP: [0.1, 0.2, 0.3], floor: [0.3, 0.1, 1], concF: [0.25, 0.3, 1], cap: [0.12, 0.1, 0.8], rope: [0.4, 0.05, 0.8],
    net: [0.4, 0.05, 0.6], sign: [0.2, 0.1, 0.8], moss: [0.5, 0.05, 1], bare: [0.4, 0.05, 0.9], roofGalv: [0.02, 0.6, 0.55], roofRust: [0.06, 0.2, 0.75], tote: [0.02, 0.4, 0.6], box: [0.05, 0.3, 0.7],
    ice: [0, 0.9, 0.9], glass: [0, 1, 0], glassHi: [0, 1, 0], cp_warm: [0, 0.9, 0], cp_iron: [0.02, 0.6, 0.6], dark: [0, 0, 0], soot: [0.3, 0.05, 0.6], trapW: [0.05, 0.2, 0.5],
    buoyR: [0.02, 0.5, 0.4], buoyY: [0.02, 0.5, 0.4], buoyO: [0.02, 0.5, 0.4], buoyW: [0.02, 0.5, 0.4] };

  // ---- textures (u along the face, v up it or up the slope; walls get v = z) -> band offset ------------------------------
  const TEX = {
    shingle: (u, v) => { const c = 0.18, row = Math.floor(v / c); if (md(v, c) < 0.028) return -1; const uu = u + (row & 1) * 0.12; if (md(uu, 0.24) < 0.022) return -1; return hsh(Math.floor(uu / 0.24), row, 7) < 0.14 ? -1 : 0; },
    clapboard: (u, v) => md(v, 0.2) < 0.034 ? -1 : 0,
    boardBatten: (u, v) => { const j = md(u, 0.3); return j < 0.05 ? 1 : j < 0.075 ? -1 : 0; },
    corrugated: (u, v) => { const f = md(u, 0.19) / 0.19; return f < 0.14 ? -1 : f < 0.36 ? 0 : f < 0.56 ? 1 : 0; },
    block: (u, v) => { const row = Math.floor(v / 0.2), uu = u + (row & 1) * 0.2; if (md(v, 0.2) < 0.022 || md(uu, 0.4) < 0.022) return -1; return hsh(Math.floor(uu / 0.4), row, 19) < 0.12 ? -1 : 0; },
    conc: (u, v) => { if (md(v, 0.6) < 0.018) return -1; if (md(u, 0.6) > 0.28 && md(u, 0.6) < 0.32 && md(v, 0.3) < 0.03) return -1; return hsh(Math.floor(u / 0.6), Math.floor(v / 0.6), 23) < 0.15 ? -1 : 0; },
    rubble: (u, v) => { const row = Math.floor(v / 0.16), uu = u + (row & 1) * 0.17; if (md(v, 0.16) < 0.03 || md(uu, 0.34) < 0.03) return -2; return hsh(Math.floor(uu / 0.34), row, 23) < 0.3 ? -1 : 0; },
    boards: (u, v) => { const i = Math.floor(u / 0.21); if (md(u, 0.21) < 0.018) return -1; return hsh(i, 3, 11) < 0.22 ? -1 : 0; },
    planks: (u, v) => { const i = Math.floor(v / 0.16); if (md(v, 0.16) < 0.016) return -1; if (md(u + i * 0.9, 1.8) < 0.02) return -1; return hsh(i, 9, 17) < 0.2 ? -1 : 0; },
    vboards: (u, v) => md(u, 0.16) < 0.02 ? -1 : (hsh(Math.floor(u / 0.16), 7, 29) < 0.2 ? -1 : 0),
    slats: (u, v) => md(v, 0.11) < 0.022 ? -1 : 0,
    grate: (u, v) => (md(u, 0.08) < 0.022 || md(v, 0.08) < 0.022) ? 0 : -1,
    mesh: (u, v) => (md(u, 0.07) < 0.016 || md(v, 0.07) < 0.016) ? 1 : -1,
    concFloor: (u, v) => { if (md(u, 2.4) < 0.025 || md(v, 2.4) < 0.025) return -1; return hsh(Math.floor(u / 0.5), Math.floor(v / 0.5), 31) < 0.1 ? -1 : 0; },
    ice: (u, v) => { const k = hsh(Math.floor(u * 9), Math.floor(v * 9), 37); return k < 0.3 ? -1 : k > 0.85 ? 1 : 0; },
    net: (u, v) => (md(u + v, 0.09) < 0.02 || md(u - v, 0.09) < 0.02) ? 1 : 0,
  };
  const SIDTEX = { shingle: TEX.shingle, clapboard: TEX.clapboard, boardBatten: TEX.boardBatten, corrugated: TEX.corrugated };
  const ROOFTEX = {
    asphaltGrey: (u, v) => { const c = 0.25, row = Math.floor(v / c); if (md(v, c) < 0.03) return -1; const uu = u + (row & 1) * 0.17; if (md(v, c) < 0.14 && md(uu, 0.34) < 0.02) return -1; return hsh(Math.floor(uu / 0.34), row, 43) < 0.12 ? -1 : 0; },
    asphaltBrown: (u, v) => { const c = 0.2, row = Math.floor(v / c); if (md(v, c) < 0.03) return -1; const uu = u + hsh(row, 1, 47) * 0.3, wd = 0.12 + 0.12 * hsh(Math.floor(uu / 0.2), row, 49); if (md(uu, wd) < 0.02) return -1; return hsh(Math.floor(uu / 0.2), row, 51) < 0.2 ? -1 : 0; },
    metalSeam: (u, v) => { const j = md(u, 0.45); return j < 0.03 ? -1 : j < 0.06 ? 1 : 0; },
    corrugated: (u, v) => { const f = md(u, 0.24) / 0.24; return f < 0.14 ? -1 : f < 0.34 ? 0 : f < 0.56 ? 1 : 0; },
    rusted: (u, v) => { const f = md(u, 0.24) / 0.24; let b = f < 0.14 ? -1 : (f > 0.34 && f < 0.56) ? 1 : 0; if (md(v, 2.3) < 0.05) b -= 1; return b; },
  };
  const WSTYLE = { twoOverTwo: { cols: 2, rows: 2 }, sixOverSix: { cols: 3, rows: 4 }, oneOverOne: { cols: 1, rows: 2 }, industrial: { cols: 3, rows: 3, steel: true } };

  // ---- types and presets (pass 1's massing ranges; the fish plant's floor rises to the dock) ---------------------------
  const TYPES = {
    shack: { label: 'Net shed', note: 'gear floor with a net loft over it, a ladder up, a stove; traps and buoys where the camera looks', shape: 'gable', pitch: 1.3, siding: 'shingle', body: 'greyShingle', door: 'doubleBarn', windows: 'twoOverTwo',
      base: 'none', cupola: 'none', roof: 'asphaltGrey', Wd: [3.6, 1.4], Ln: [4.5, 3.0], wallH: [2.9, 1.1], fH: 0.4, t: 0.12, loft: 'window', dock: false, hvac: false, stacks: 0, vents: false, sign: false,
      boom: false, winD: 0.35, plinth: 'stone', stove: true, goods: true, doorPaint: 'white', sched: 'fisher' },
    storage: { label: 'Storage barn', note: 'gambrel gear store: traps, bait freezer, a stair to the loft, a hoist over the loft door', shape: 'gambrel', pitch: 1.0, siding: 'boardBatten', body: 'red', door: 'slidingBarn',
      windows: 'twoOverTwo', base: 'none', cupola: 'cupola', roof: 'asphaltGrey', Wd: [5.2, 2.2], Ln: [6.5, 4.5], wallH: [3.6, 1.6], fH: 0.45, t: 0.14, loft: 'door', dock: false, hvac: false, stacks: 0,
      vents: true, sign: false, boom: false, winD: 0.3, plinth: 'stone', stove: false, goods: false, doorPaint: 'white', sched: 'store' },
    processing: { label: 'Fish plant', note: 'cutting floor at dock height, apron and hoist on the wharf side, truck dock, office mezzanine up a steel stair', shape: 'gable', pitch: 0.72, siding: 'corrugated',
      body: 'galv', door: 'rollUp', windows: 'industrial', base: 'block', cupola: 'monitor', roof: 'corrugated', Wd: [7.2, 2.6], Ln: [10, 6], wallH: [3.4, 1.6], fH: 1.2, t: 0.2, loft: 'none',
      dock: true, hvac: true, stacks: 2, vents: true, sign: true, boom: true, winD: 0.5, plinth: 'concrete', stove: false, goods: false, doorPaint: 'steel', sched: 'plant' },
  };
  const PRESETS = {
    netShed: { type: 'shack', body: 'greyShingle', siding: 'shingle', roof: 'asphaltGrey', door: 'doubleBarn', size: 0.2, weather: 0.6 },
    redShed: { type: 'shack', body: 'red', siding: 'boardBatten', roof: 'asphaltBrown', door: 'plank', size: 0.35, weather: 0.4 },
    tealShack: { type: 'shack', body: 'teal', siding: 'clapboard', roof: 'metalSeam', door: 'slidingBarn', size: 0.3, weather: 0.45 },
    gambrelBarn: { type: 'storage', body: 'blue', siding: 'boardBatten', roof: 'asphaltGrey', door: 'slidingBarn', cupola: 'cupola', size: 0.6, weather: 0.35 },
    iceHouse: { type: 'storage', body: 'white', siding: 'clapboard', roof: 'metalSeam', door: 'doubleBarn', size: 0.5, weather: 0.25 },
    fishPlant: { type: 'processing', body: 'galv', siding: 'corrugated', roof: 'corrugated', size: 0.7, weather: 0.5 },
    cannery: { type: 'processing', body: 'rustMetal', siding: 'corrugated', roof: 'rusted', size: 0.9, weather: 0.72, boom: true },
  };
  const SHAPES = ['gable', 'gambrel', 'shed'], SIDINGS = ['shingle', 'clapboard', 'boardBatten', 'corrugated'], DOORS = ['doubleBarn', 'slidingBarn', 'plank', 'rollUp', 'personnel'];
  const ROOF_KEYS = Object.keys(ROOFS), CUPOLAS = ['none', 'cupola', 'monitor'], WINDOWS = Object.keys(WSTYLE), PLINTHS = ['stone', 'concrete', 'sills'];

  // the top of the gable-end outline (and so of the roof) at model x
  function gableTop(s, x) { if (s.shape === 'shed') return s.eLo + (clamp(x, -s.hw, s.hw) + s.hw) / (2 * s.hw) * (s.eHi - s.eLo);
    const a = Math.min(Math.abs(x), s.hw);
    if (s.shape === 'gambrel') return a >= s.hw - s.kx ? s.eaveZ + (s.hw - a) / s.kx * (s.zk - s.eaveZ) : s.zk + (s.hw - s.kx - a) / (s.hw - s.kx) * (s.ridgeZ - s.zk);
    return s.eaveZ + (s.hw - a) * s.pitch; }
  const minTop = (s, x0, x1) => Math.min(gableTop(s, x0), gableTop(s, x1));

  function resolve(o) {
    o = o || {}; const type = TYPES[o.type] ? o.type : 'shack', T = TYPES[type], g = (k, d) => o[k] != null ? o[k] : (T[k] != null ? T[k] : d), pick = (k, list, d) => list.indexOf(g(k, d)) >= 0 ? g(k, d) : d;
    const s = { type, label: T.label, shape: pick('shape', SHAPES, 'gable'), size: clamp(o.size != null ? +o.size : 0.4, 0, 1), siding: pick('siding', SIDINGS, 'shingle'), base: g('base', 'none') === 'block' ? 'block' : 'none',
      body: Array.isArray(o.body) ? o.body : (BODY[g('body', 'greyShingle')] ? g('body', 'greyShingle') : 'greyShingle'), roof: pick('roof', ROOF_KEYS, 'asphaltGrey'), door: pick('door', DOORS, 'doubleBarn'),
      windows: pick('windows', WINDOWS, 'twoOverTwo'), winD: clamp(o.winDensity != null ? +o.winDensity : T.winD, 0.1, 1), cupola: pick('cupola', CUPOLAS, 'none'), pitch: +g('pitch', 1),
      loft: ['none', 'window', 'door'].indexOf(g('loft', 'none')) >= 0 ? g('loft', 'none') : 'none', dock: !!g('dock', false), hvac: !!g('hvac', false), stacks: clamp((o.stacks != null ? +o.stacks : T.stacks) | 0, 0, 3),
      vents: !!g('vents', false), sign: !!g('sign', false), boom: !!g('boom', false), stove: !!g('stove', false), goods: !!g('goods', false), plinth: pick('plinth', PLINTHS, 'stone'),
      pers: o.personnel != null ? !!o.personnel : type === 'processing', trim: o.trim || 'white', doorPaint: o.doorPaint || T.doorPaint,
      weather: clamp(o.weather != null ? +o.weather : 0.55, 0, 1), kept: clamp(o.kept != null ? +o.kept : 0.7, 0, 1), mirror: !!o.mirror, season: o.season === 'winter' ? 'winter' : 'summer',
      doorOpen: clamp(+(o.doorOpen || 0), 0, 1), bayOpen: clamp(+(o.bayOpen || 0), 0, 1), loftOpen: clamp(+(o.loftOpen || 0), 0, 1), persOpen: clamp(+(o.persOpen != null ? o.persOpen : (o.doorOpen || 0)), 0, 1),
      cut: o.cutaway === 'section', cutH: o.cutH != null ? clamp(+o.cutH, 0.2, 2) : 0.9, storey: o.storey === 'loft' ? 'loft' : 'ground', seed: o.seed | 0, sched: o.schedule || T.sched };
    s.metalBody = s.body === 'galv' || s.body === 'rustMetal';
    s.Wd = T.Wd[0] + s.size * T.Wd[1]; s.Ln = T.Ln[0] + s.size * T.Ln[1]; s.wallH = T.wallH[0] + s.size * T.wallH[1];
    s.fH = o.floorH != null ? clamp(+o.floorH, 0.15, 1.5) : T.fH; s.t = T.t; s.eaveZ = s.fH + s.wallH; s.hw = s.Wd / 2; s.hl = s.Ln / 2;
    const hw = s.hw, e = s.eaveZ;
    if (s.shape === 'gambrel') { s.kx = Math.min(1.0, hw * 0.3); s.zk = e + s.kx * 2.1; s.ridgeZ = s.zk + (hw - s.kx) * 0.5; }
    else if (s.shape === 'shed') { s.eLo = e - 0.2; s.eHi = e + hw * s.pitch * 1.1; s.ridgeZ = s.eHi; }
    else s.ridgeZ = e + hw * s.pitch;
    // the loft: a multiple of the 0.24 rung over the floor, only where a figure can stand under the roof (2.0 m)
    s.loftKind = null; s.loftZ = null; s.standHalf = 0;
    const strip = (z) => { let sh = -1; for (let x = 0; x <= hw; x += 0.02) { if (gableTop(s, x) - z >= 2.0) sh = x; else break; } return sh; };
    if (type === 'processing' && s.shape !== 'shed') { const z = s.fH + 2.64, sh = strip(z); if (sh >= 1.2) { s.loftKind = 'mezz'; s.loftZ = z; s.mezzD = 3.0; s.standHalf = sh; } }
    else if (type !== 'processing' && s.shape !== 'shed') { const n = type === 'storage' ? 12 : clamp(Math.floor((s.wallH - 0.35) / 0.24), 10, 12), z = s.fH + 0.24 * n, sh = strip(z);
      if (sh >= 0.55) { s.loftKind = 'loft'; s.loftZ = z; s.standHalf = sh; } }
    const dwT = { doubleBarn: Math.min(s.Wd * 0.62, 3.2), slidingBarn: Math.min(s.Wd * 0.56, 3.0), plank: 1.1, rollUp: Math.min(s.Wd * 0.6, 3.4), personnel: 1.0 };
    const dhT = { doubleBarn: Math.min(s.wallH * 0.84, 2.8), slidingBarn: Math.min(s.wallH * 0.84, 2.8), plank: 2.15, rollUp: Math.min(s.wallH * 0.86, 3.4), personnel: 2.1 };
    s.dw = dwT[s.door]; s.dh = dhT[s.door]; if (s.loftKind === 'loft') s.dh = Math.min(s.dh, s.loftZ - s.fH - 0.25); s.dh = Math.max(2.0, s.dh);
    if (s.loft === 'door' && s.loftKind !== 'loft') s.loft = 'window';
    return s;
  }

  function mats(s) {
    const wx = s.weather, un = 1 - s.kept;
    const grime = r => r.map(c => mix(desat(c, wx * 0.3), '#5f584c', wx * 0.14)), chalk = r => r.map(c => mix(desat(c, un * 0.4), '#a39b8a', un * 0.2)), salt = r => r.map(c => mix(c, '#6b6156', wx * 0.2));
    const M = {}; for (const k in RAMP) M[k] = { ramp: grime(RAMP[k]) };
    const bodyR = Array.isArray(s.body) ? s.body : BODY[s.body];
    M.body = { ramp: s.metalBody ? salt(bodyR) : chalk(grime(bodyR)) }; M.trim = { ramp: chalk(grime(PAINT[s.trim] || TRIM)) };
    M.door = { ramp: chalk(grime(PAINT[s.doorPaint] || TRIM)) }; M.roof = { ramp: grime(ROOFS[s.roof]) };
    for (const k of ['glass', 'glassHi', 'cp_warm', 'dark']) M[k] = { ramp: RAMP[k].slice() };
    // weathering patches read as the surface gone over, not as new colours: each is the surface's own ramp pulled part way
    const moss = grime(RAMP.moss), pull = (r, to, t) => r.map((c, i) => mix(c, to[Math.min(i, to.length - 1)], t));
    M.moss = { ramp: pull(M.roof.ramp, moss, 0.5) }; M.roofRust = { ramp: pull(M.roof.ramp, RAMP.rust, 0.5) }; M.roofGalv = { ramp: pull(M.roof.ramp, RAMP.galv, 0.55) }; M.bare = { ramp: pull(M.body.ramp, RAMP.bare, 0.45) };
    return M;
  }

  // ---- face builders (the outbuildings') ------------------------------------------------------------------------------
  function F(out, v, mat, o) { o = o || {}; const f = { v, mat, b: o.b || 0, db: o.db || 0, uv: o.uv || null, tex: o.tex || null }; if (o.tag) f.tag = o.tag; if (o.em) f.em = o.em; if (o.fire) f.fire = true; out.push(f); return f; }
  function box(out, x0, x1, y0, y1, z0, z1, mat, o) { o = o || {}; const q = { tag: o.tag, b: o.b || 0, tex: o.tex || null, em: o.em };
    F(out, [[x0, y0, z0], [x1, y0, z0], [x1, y0, z1], [x0, y0, z1]], mat, Object.assign({}, q, { uv: [[x0, z0], [x1, z0], [x1, z1], [x0, z1]] }));
    F(out, [[x1, y1, z0], [x0, y1, z0], [x0, y1, z1], [x1, y1, z1]], mat, Object.assign({}, q, { uv: [[x1, z0], [x0, z0], [x0, z1], [x1, z1]] }));
    F(out, [[x1, y0, z0], [x1, y1, z0], [x1, y1, z1], [x1, y0, z1]], mat, Object.assign({}, q, { uv: [[y0, z0], [y1, z0], [y1, z1], [y0, z1]] }));
    F(out, [[x0, y1, z0], [x0, y0, z0], [x0, y0, z1], [x0, y1, z1]], mat, Object.assign({}, q, { uv: [[y1, z0], [y0, z0], [y0, z1], [y1, z1]] }));
    if (!o.noTop) F(out, [[x0, y0, z1], [x1, y0, z1], [x1, y1, z1], [x0, y1, z1]], o.topMat || mat, { tag: o.tag, b: o.topB != null ? o.topB : (o.b || 0), tex: o.topTex || null, uv: o.topTex ? [[x0, y0], [x1, y0], [x1, y1], [x0, y1]] : null, em: o.em }); }
  const sub = (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]], crs = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
  const nrm = (a) => { const m = Math.hypot(a[0], a[1], a[2]) || 1; return [a[0] / m, a[1] / m, a[2] / m]; };
  function stick(out, a, b, t, mat, o) { o = o || {}; const u = nrm(sub(b, a)), p = Math.abs(u[2]) < 0.9 ? [0, 0, 1] : [1, 0, 0], v1 = nrm(crs(u, p)), v2 = crs(u, v1), h = t / 2;
    const c = (P, s1, s2) => [P[0] + (v1[0] * s1 + v2[0] * s2) * h, P[1] + (v1[1] * s1 + v2[1] * s2) * h, P[2] + (v1[2] * s1 + v2[2] * s2) * h];
    const A = [c(a, -1, -1), c(a, 1, -1), c(a, 1, 1), c(a, -1, 1)], B = [c(b, -1, -1), c(b, 1, -1), c(b, 1, 1), c(b, -1, 1)];
    for (let k = 0; k < 4; k++) { const k2 = (k + 1) % 4; F(out, [A[k], A[k2], B[k2], B[k]], mat, { tag: o.tag, b: o.b || 0 }); }
    F(out, B, mat, { tag: o.tag, b: o.b || 0 }); F(out, A, mat, { tag: o.tag, b: o.b || 0 }); }
  function cyl(out, x, y, r, z0, z1, n, mat, o) { o = o || {}; const P = []; for (let k = 0; k < n; k++) { const a = (k + 0.5) / n * 2 * Math.PI; P.push([x + Math.cos(a) * r, y + Math.sin(a) * r]); }
    const pr = 2 * Math.PI * r / n;
    for (let k = 0; k < n; k++) { const a = P[k], b = P[(k + 1) % n]; F(out, [[a[0], a[1], z0], [b[0], b[1], z0], [b[0], b[1], z1], [a[0], a[1], z1]], mat, { tag: o.tag, b: o.b || 0, tex: o.tex || null, uv: [[k * pr, z0], [(k + 1) * pr, z0], [(k + 1) * pr, z1], [k * pr, z1]] }); }
    if (!o.noTop) F(out, P.map(p => [p[0], p[1], z1]), o.topMat || mat, { tag: o.tag, b: o.topB != null ? o.topB : (o.b || 0), tex: o.topTex || null, uv: o.topTex ? P.map(p => [p[0], p[1]]) : null }); }
  function cylX(out, x0, x1, y, z, r, n, mat, o) { o = o || {}; const P = []; for (let k = 0; k < n; k++) { const a = (k + 0.5) / n * 2 * Math.PI; P.push([y + Math.cos(a) * r, z + Math.sin(a) * r]); }
    for (let k = 0; k < n; k++) { const a = P[k], b = P[(k + 1) % n]; F(out, [[x0, a[0], a[1]], [x1, a[0], a[1]], [x1, b[0], b[1]], [x0, b[0], b[1]]], mat, { tag: o.tag, b: o.b || 0 }); }
    F(out, P.map(p => [x0, p[0], p[1]]), o.endMat || mat, { tag: o.tag, b: o.b || 0 }); F(out, P.map(p => [x1, p[0], p[1]]), o.endMat || mat, { tag: o.tag, b: o.b || 0 }); }
  // a vertical prism over a convex plan quad (corners in order)
  function prism(out, P, z0, z1, mat, o) { o = o || {}; const n = P.length;
    for (let k = 0; k < n; k++) { const a = P[k], b = P[(k + 1) % n], L = Math.hypot(b[0] - a[0], b[1] - a[1]); F(out, [[a[0], a[1], z0], [b[0], b[1], z0], [b[0], b[1], z1], [a[0], a[1], z1]], mat, { tag: o.tag, b: o.b || 0, tex: o.tex || null, uv: [[0, z0], [L, z0], [L, z1], [0, z1]] }); }
    F(out, P.map(p => [p[0], p[1], z1]), o.topMat || mat, { tag: o.tag, b: o.topB != null ? o.topB : (o.b || 0), tex: o.topTex || null, uv: o.topTex ? P.map(p => [p[0], p[1]]) : null }); }

  // ---- walls: a convex outline in (u, z) with rectangular openings -----------------------------------------------------
  function clipAx(P, ax, v, keepGreater) { const out = []; for (let i = 0; i < P.length; i++) { const a = P[i], b = P[(i + 1) % P.length], ia = keepGreater ? a[ax] >= v - 1e-9 : a[ax] <= v + 1e-9, ib = keepGreater ? b[ax] >= v - 1e-9 : b[ax] <= v + 1e-9;
    if (ia) out.push(a); if (ia !== ib) { const t = (v - a[ax]) / (b[ax] - a[ax]), q = [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t]; q[ax] = v; out.push(q); } } return out; }
  const area2 = (P) => { let s = 0; for (let i = 0; i < P.length; i++) { const a = P[i], b = P[(i + 1) % P.length]; s += a[0] * b[1] - b[0] * a[1]; } return Math.abs(s) / 2; };
  function pieces(outline, holes) {
    const us = [...new Set(holes.flatMap(h => [h.u0, h.u1]))].sort((a, b) => a - b), umin = Math.min(...outline.map(p => p[0])), umax = Math.max(...outline.map(p => p[0]));
    const cuts = [umin, ...us.filter(u => u > umin + 1e-6 && u < umax - 1e-6), umax], res = [];
    for (let i = 0; i + 1 < cuts.length; i++) { const ua = cuts[i], ub = cuts[i + 1]; const P = clipAx(clipAx(outline, 0, ua, true), 0, ub, false); if (P.length < 3 || area2(P) < 1e-6) continue;
      const hs = holes.filter(h => h.u0 <= ua + 1e-6 && h.u1 >= ub - 1e-6).sort((a, b) => a.z0 - b.z0);
      if (!hs.length) { res.push(P); continue; }
      let zlo = -1e9; for (const h of hs) { const Q = clipAx(clipAx(P, 1, zlo, true), 1, h.z0, false); if (Q.length >= 3 && area2(Q) > 1e-6) res.push(Q); zlo = Math.max(zlo, h.z1); }
      const Q = clipAx(P, 1, zlo, true); if (Q.length >= 3 && area2(Q) > 1e-6) res.push(Q); }
    return res; }
  function wallFrame(p0, p1, n) { const L = Math.hypot(p1[0] - p0[0], p1[1] - p0[1]), d = [(p1[0] - p0[0]) / L, (p1[1] - p0[1]) / L];
    return { p0, p1, n, d, L, M: (u, z, off) => [p0[0] + d[0] * u + n[0] * off, p0[1] + d[1] * u + n[1] * off, z] }; }
  function fbox(out, M, u0, u1, z0, z1, o0, o1, mat, o) { o = o || {};
    const c = [M(u0, z0, o0), M(u1, z0, o0), M(u1, z1, o0), M(u0, z1, o0), M(u0, z0, o1), M(u1, z0, o1), M(u1, z1, o1), M(u0, z1, o1)], q = { tag: o.tag, b: o.b || 0, em: o.em, fire: o.fire };
    F(out, [c[4], c[5], c[6], c[7]], mat, Object.assign({}, q, { tex: o.tex || null, uv: [[u0, z0], [u1, z0], [u1, z1], [u0, z1]] }));
    if (!o.flush) F(out, [c[0], c[1], c[2], c[3]], mat, q);
    F(out, [c[0], c[1], c[5], c[4]], mat, q); F(out, [c[3], c[2], c[6], c[7]], o.topMat || mat, Object.assign({}, q, { b: o.topB != null ? o.topB : (o.b || 0) }));
    F(out, [c[0], c[3], c[7], c[4]], mat, q); F(out, [c[1], c[2], c[6], c[5]], mat, q); }
  function faceWorldN(C, n) { if (C.dir == null) return 0; const nx = C.s.mirror ? -n[0] : n[0], a = C.dir * Math.PI / 4; return nx * Math.sin(a) + n[1] * Math.cos(a); }
  const dropped = (C, n) => C.s.cut && faceWorldN(C, n) < -0.2;
  // clustered cell noise: a smooth field three cells across with a little grain, remapped to a flat distribution
  function clump(i, j, s) { const x = i / 3, y = j / 2.2, xi = Math.floor(x), yi = Math.floor(y), fx = x - xi, fy = y - yi, u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
    const c = (a, b) => hsh(xi + a, yi + b, s), n = (c(0, 0) * (1 - u) + c(1, 0) * u) * (1 - v) + (c(0, 1) * (1 - u) + c(1, 1) * u) * v, m = 0.72 * n + 0.28 * hsh(i, j, s + 3);
    return m < 0.5 ? 2 * m * m : 1 - 2 * (1 - m) * (1 - m); }
  /* one face split into model-space cells, each cell mat or alt by a hash (weathering that stays put between facings) */
  function patchy(C, P, M, mat, alt, prob, tex, tag, cu, cz, em, salt) { const out = C.out, sk = (salt | 0) * 131 + C.s.seed * 7 + 17;
    if (!alt || !prob) { if (mat) F(out, P.map(p => M(p[0], p[1])), mat, { uv: P.map(p => [p[0], p[1]]), tex, tag, em }); return; }
    let u0 = 1e9, u1 = -1e9, v0 = 1e9, v1 = -1e9; for (const p of P) { u0 = Math.min(u0, p[0]); u1 = Math.max(u1, p[0]); v0 = Math.min(v0, p[1]); v1 = Math.max(v1, p[1]); }
    for (let i = Math.floor(u0 / cu); i < Math.ceil(u1 / cu); i++) for (let j = Math.floor(v0 / cz); j < Math.ceil(v1 / cz); j++) {
      const Q = clipAx(clipAx(clipAx(clipAx(P, 0, i * cu, true), 0, (i + 1) * cu, false), 1, j * cz, true), 1, (j + 1) * cz, false); if (Q.length < 3 || area2(Q) < 1e-5) continue;
      const pr = typeof prob === 'function' ? prob(i, j, (j + 0.5) * cz) : prob, m = clump(i, j, sk) < pr ? alt : mat; if (!m) continue;
      F(out, Q.map(p => M(p[0], p[1])), m, { uv: Q.map(p => [p[0], p[1]]), tex, tag, em }); } }
  /* w: {p0, p1, n, outline, holes, mat, tex, inTex, inMat, t, tag, eave, z0, alt, altP} -> the wall frame; plan segments go to C */
  function wall(C, w) { const out = C.out, Wf = wallFrame(w.p0, w.p1, w.n), t = w.t || C.t, holes = w.holes || [], z0 = w.z0 != null ? w.z0 : C.fz;
    let outline = w.outline || [[0, z0], [Wf.L, z0], [Wf.L, w.eave], [0, w.eave]], capZ = null;
    const drop = dropped(C, w.n); Wf.drop = drop; Wf.holes = holes; Wf.z0 = z0; Wf.t = t;
    if (C.s.cut) { capZ = C.capFor(drop, w.eave); if (capZ != null) outline = clipAx(outline, 1, capZ, false); }
    if (outline.length >= 3) for (const P of pieces(outline, holes)) {
      patchy(C, P, (u, z) => Wf.M(u, z, 0), w.mat || 'body', w.alt, w.altP || 0, w.tex, w.tag, 0.8, 0.6, null, Math.round(w.p0[0] * 37 + w.p0[1] * 53));
      if (!C.lc) F(out, P.map(p => Wf.M(p[0], p[1], -t)), w.inMat || 'inside', { uv: P.map(p => [p[0], p[1]]), tex: w.inTex || TEX.boards, tag: 'wall.inside' }); }
    for (const h of holes) { const z1 = capZ != null ? Math.min(h.z1, capZ) : h.z1; if (z1 <= h.z0 + 1e-6 || C.lc) continue;
      const q = (u0, za, u1, zb) => F(out, [Wf.M(u0, za, 0), Wf.M(u1, zb, 0), Wf.M(u1, zb, -t), Wf.M(u0, za, -t)], h.rev || w.inMat || 'inside', { tag: w.tag, b: -1 });
      q(h.u0, h.z0, h.u0, z1); q(h.u1, h.z0, h.u1, z1); if (h.z0 > z0 + 0.01) q(h.u0, h.z0, h.u1, h.z0); if (capZ == null || h.z1 < capZ - 1e-6) q(h.u0, h.z1, h.u1, h.z1); }
    if (capZ != null && outline.length >= 3) {
      for (let i = 0; i < outline.length; i++) { const a = outline[i], b = outline[(i + 1) % outline.length];
        if (Math.abs(a[0] - b[0]) < 1e-6) continue; const zc = Math.max(a[1], b[1]); if (zc < capZ - 1e-6 && Math.abs(a[1] - b[1]) < 1e-6) continue;
        if (Math.abs(a[1] - capZ) < 1e-6 && Math.abs(b[1] - capZ) < 1e-6) { let segs = [[Math.min(a[0], b[0]), Math.max(a[0], b[0])]];
          for (const h of holes) if (h.z0 < capZ - 1e-6 && h.z1 > capZ + 1e-6) segs = segs.flatMap(([s0, s1]) => (h.u1 <= s0 || h.u0 >= s1) ? [[s0, s1]] : [[s0, h.u0], [h.u1, s1]].filter(q => q[1] - q[0] > 1e-3));
          for (const [s0, s1] of segs) F(out, [Wf.M(s0, capZ, 0.012), Wf.M(s1, capZ, 0.012), Wf.M(s1, capZ, -t), Wf.M(s0, capZ, -t)], 'cap', { tag: 'cut.cap', b: 1 }); }
        else if (Math.max(a[1], b[1]) > z0 + 0.05 && !(Math.abs(a[1] - z0) < 1e-6 && Math.abs(b[1] - z0) < 1e-6)) F(out, [Wf.M(a[0], a[1], 0.012), Wf.M(b[0], b[1], 0.012), Wf.M(b[0], b[1], -t), Wf.M(a[0], a[1], -t)], 'cap', { tag: 'cut.cap', b: 1 }); } }
    // plan segments: floor-level openings are gaps on the ground level; the loft level sees the whole wall
    const gaps = holes.filter(h => h.z0 < C.fz + 0.35 && h.walk !== false).map(h => [h.u0, h.u1]).sort((a, b) => a[0] - b[0]); let u = 0;
    for (const gp of gaps) { if (gp[0] > u) C.walls0.push({ a: Wf.M(u, 0, -t / 2), b: Wf.M(gp[0], 0, -t / 2), t }); u = Math.max(u, gp[1]); }
    if (u < Wf.L) C.walls0.push({ a: Wf.M(u, 0, -t / 2), b: Wf.M(Wf.L, 0, -t / 2), t });
    C.walls1.push({ a: Wf.M(0, 0, -t / 2), b: Wf.M(Wf.L, 0, -t / 2), t });
    Wf.capZ = capZ; return Wf; }
  // a cladding band laid over a wall (the cinderblock wainscot, the rust line at the foot of a steel wall)
  function skin(C, Wf, z0, z1, mat, tex, tag, o) { o = o || {}; const zt = Wf.capZ != null ? Math.min(z1, Wf.capZ) : z1; if (zt <= z0 + 0.02) return;
    for (const P of pieces([[0, z0], [Wf.L, z0], [Wf.L, zt], [0, zt]], Wf.holes)) {
      if (o.prob) patchy(C, P, (u, z) => Wf.M(u, z, 0.016), null, mat, o.prob, tex, tag, 0.45, 0.2, null, Math.round(Wf.p0[0] * 41 + Wf.p0[1] * 59) + 5); else F(C.out, P.map(p => Wf.M(p[0], p[1], 0.018)), mat, { uv: P.map(p => [p[0], p[1]]), tex, tag, db: 0.01 }); }
    if (o.flash && zt === z1) { let segs = [[0, Wf.L]]; for (const h of Wf.holes) if (h.z0 < z1 + 0.06 && h.z1 > z1) segs = segs.flatMap(([a, b]) => (h.u1 <= a || h.u0 >= b) ? [[a, b]] : [[a, h.u0], [h.u1, b]].filter(q => q[1] - q[0] > 0.02));
      for (const [a, b] of segs) fbox(C.out, Wf.M, a, b, z1, z1 + 0.06, 0, 0.038, 'steel', { tag }); } }
  function cornerBoards(C, Wf, z0, zA, zB, mat) { const cap = Wf.capZ != null ? Wf.capZ : 1e9, a = Math.min(zA, cap), b = Math.min(zB, cap);
    if (a > z0) fbox(C.out, Wf.M, 0, 0.1, z0, a, 0, 0.026, mat, { tag: 'wall.trim' }); if (b > z0) fbox(C.out, Wf.M, Wf.L - 0.1, Wf.L, z0, b, 0, 0.026, mat, { tag: 'wall.trim' }); }
  function casing(C, Wf, h, o) { o = o || {}; const cw = o.w || 0.08, z1 = Wf.capZ != null ? Math.min(h.z1 + cw, Wf.capZ) : h.z1 + cw, tg = o.tag || 'entry.frame', mat = o.mat || 'trim';
    if (z1 > h.z0) { fbox(C.out, Wf.M, h.u0 - cw, h.u0, h.z0, z1, 0, 0.028, mat, { tag: tg }); fbox(C.out, Wf.M, h.u1, h.u1 + cw, h.z0, z1, 0, 0.028, mat, { tag: tg }); }
    if (Wf.capZ == null || h.z1 + cw <= Wf.capZ) fbox(C.out, Wf.M, h.u0 - cw, h.u1 + cw, h.z1, h.z1 + cw, 0, 0.032, mat, { tag: tg }); }
  function windowIn(C, Wf, h, o) { o = o || {}; if (Wf.capZ != null && h.z0 >= Wf.capZ - 1e-6) return; const zt = Wf.capZ != null ? Math.min(h.z1, Wf.capZ) : h.z1, out = C.out, em = o.em;
    const st = WSTYLE[o.style || C.s.windows] || WSTYLE.twoOverTwo, mm = st.steel ? 'steel' : 'trim';
    F(out, [Wf.M(h.u0, h.z0, -0.05), Wf.M(h.u1, h.z0, -0.05), Wf.M(h.u1, zt, -0.05), Wf.M(h.u0, zt, -0.05)], 'glass', { tag: 'show.window', em });
    if (zt > h.z0 + 0.3) F(out, [Wf.M(h.u0 + (h.u1 - h.u0) * 0.08, zt - 0.14, -0.045), Wf.M(h.u0 + (h.u1 - h.u0) * 0.34, zt - 0.14, -0.045), Wf.M(h.u0 + (h.u1 - h.u0) * 0.22, zt - 0.05, -0.045)], 'glassHi', { tag: 'show.window', em });
    for (let k = 1; k < st.cols; k++) { const u = h.u0 + (h.u1 - h.u0) * k / st.cols; fbox(out, Wf.M, u - 0.018, u + 0.018, h.z0, zt, -0.05, -0.03, mm, { tag: 'show.window' }); }
    for (let k = 1; k < st.rows; k++) { const z = h.z0 + (h.z1 - h.z0) * k / st.rows, m = (st.rows % 2 === 0 && k === st.rows / 2) ? 0.028 : 0.016; if (z < zt - 0.02) fbox(out, Wf.M, h.u0, h.u1, z - m, z + m, -0.05, -0.03, mm, { tag: 'show.window' }); }
    casing(C, Wf, h, { tag: 'show.window', w: st.steel ? 0.05 : 0.07, mat: st.steel ? 'steel' : 'trim' });
    fbox(out, Wf.M, h.u0 - 0.1, h.u1 + 0.1, h.z0 - 0.05, h.z0, 0, 0.06, st.steel ? 'steel' : 'trim', { tag: 'show.window' });
    if (C.s.metalBody && C.s.weather > 0.2) { const k = Math.round(h.u0 * 10) + Math.round(h.z0 * 7) * 131; if (hsh(k, 1, 71) < C.s.weather) {
      const u = (h.u0 + h.u1) / 2 + (hsh(k, 2, 73) - 0.5) * (h.u1 - h.u0) * 0.6, len = 0.35 + hsh(k, 3, 75) * 0.75, z1 = h.z0 - 0.05, zb = Math.max(Wf.z0 + 0.05, z1 - len);
      if (z1 > zb + 0.1) F(out, [Wf.M(u - 0.05, zb, 0.014), Wf.M(u + 0.05, zb, 0.014), Wf.M(u + 0.075, z1, 0.014), Wf.M(u - 0.075, z1, 0.014)], 'rust', { tag: 'weather.rust', db: 0.01 }); } } }
  function leafFrame(Wf, hu, sgn, th) { const c = Math.cos(th), s = Math.sin(th); return (a, z, b) => Wf.M(hu + sgn * (a * c - b * s), z, a * s + b * c); }
  function ledgeLeaf(C, Wf, h, hingeAt, th, mat, o) { o = o || {}; const out = C.out, sgn = hingeAt === 'u0' ? 1 : -1, hu = hingeAt === 'u0' ? h.u0 : h.u1, wd = o.wd != null ? o.wd : h.u1 - h.u0 - 0.02, M = leafFrame(Wf, hu, sgn, th), tg = o.tag || 'door.main';
    const z0 = h.z0 + 0.012, z1 = h.z1 - 0.012; fbox(out, M, 0.01, wd, z0, z1, 0.0, 0.05, mat, { tag: tg, tex: TEX.vboards });
    const L = o.ledges || [z0 + 0.2, (z0 + z1) / 2 - 0.05, z1 - 0.32];
    for (const z of L) fbox(out, M, 0.06, wd - 0.05, z, z + 0.12, 0.05, 0.078, mat, { tag: tg, b: 1 });
    if (o.brace !== false) for (let k = 0; k + 1 < L.length; k++) stick(out, M(0.15, L[k] + 0.12, 0.066), M(wd - 0.13, L[k + 1], 0.066), 0.08, mat, { tag: tg, b: 1 });
    fbox(out, M, wd - 0.13, wd - 0.07, (z0 + z1) / 2 - 0.1, (z0 + z1) / 2 + 0.06, 0.05, 0.088, 'cp_iron', { tag: tg });
    for (const z of L) fbox(out, M, 0.01, Math.min(0.45, wd * 0.4), z + 0.035, z + 0.085, 0.078, 0.094, 'cp_iron', { tag: tg }); }
  function persLeaf(C, Wf, h, th, mat, tg) { const out = C.out, wd = h.u1 - h.u0 - 0.02, M = leafFrame(Wf, h.u0, 1, th), z0 = h.z0 + 0.01, z1 = h.z1 - 0.01;
    fbox(out, M, 0.01, wd, z0, z1, 0, 0.05, mat, { tag: tg });
    F(out, [M(wd * 0.28, z1 - 0.66, 0.052), M(wd * 0.72, z1 - 0.66, 0.052), M(wd * 0.72, z1 - 0.24, 0.052), M(wd * 0.28, z1 - 0.24, 0.052)], 'glass', { tag: tg, em: 'win:ground:pers' });
    fbox(out, M, wd - 0.14, wd - 0.07, (z0 + z1) / 2 - 0.06, (z0 + z1) / 2 + 0.04, 0.05, 0.09, 'cp_iron', { tag: tg }); }
  function slideTravel(Wf, h) { const dw = h.u1 - h.u0, two = dw > 2.0, roomL = Math.max(0, h.u0 - 0.14), roomR = Math.max(0, Wf.L - h.u1 - 0.14);
    return { two, L: two ? Math.min(dw / 2, roomL) : 0, R: Math.min(two ? dw / 2 : dw, roomR) }; }
  function slidingDoors(C, Wf, h, open) { const out = C.out, s = C.s, dw = h.u1 - h.u0, T = slideTravel(Wf, h), two = T.two, lw = two ? dw / 2 + 0.05 : dw + 0.1, z0 = h.z0 + 0.02, z1 = h.z1 + 0.06, sid = SIDTEX[s.siding];
    const slL = open * T.L, slR = open * T.R;
    const leaves = two ? [[h.u0 - 0.05 - slL, h.u0 - 0.05 - slL + lw], [h.u1 + 0.05 + slR - lw, h.u1 + 0.05 + slR]] : [[h.u0 - 0.05 + slR, h.u0 - 0.05 + slR + lw]];
    const ta = Math.min(...leaves.map(l => l[0])) - 0.12, tb = Math.max(...leaves.map(l => l[1])) + 0.12;
    fbox(out, Wf.M, Math.max(0.02, Math.min(ta, h.u0 - 0.2)), Math.min(Wf.L - 0.02, Math.max(tb, h.u1 + 0.2)), z1 + 0.04, z1 + 0.12, 0.02, 0.1, 'cp_iron', { tag: 'entry.track' });
    for (const [ua, ub] of leaves) { fbox(out, Wf.M, ua, ub, z0, z1, 0.1, 0.16, 'body', { tag: 'door.main', tex: sid });
      for (const [a0, a1, za, zb] of [[ua, ub, z0, z0 + 0.12], [ua, ub, z1 - 0.12, z1], [ua, ua + 0.12, z0, z1], [ub - 0.12, ub, z0, z1], [ua, ub, (z0 + z1) / 2 - 0.06, (z0 + z1) / 2 + 0.06]]) fbox(out, Wf.M, a0, a1, za, zb, 0.16, 0.19, 'trim', { tag: 'door.main' });
      stick(out, Wf.M(ua + 0.1, z0 + 0.12, 0.178), Wf.M(ub - 0.1, (z0 + z1) / 2 - 0.06, 0.178), 0.09, 'trim', { tag: 'door.main' });
      stick(out, Wf.M(ua + 0.1, z1 - 0.12, 0.178), Wf.M(ub - 0.1, (z0 + z1) / 2 + 0.06, 0.178), 0.09, 'trim', { tag: 'door.main' });
      for (const a of [ua + 0.3, ub - 0.3]) fbox(out, Wf.M, a - 0.05, a + 0.05, z1 - 0.02, z1 + 0.14, 0.1, 0.13, 'cp_iron', { tag: 'entry.track' }); }
    return two ? slL + slR : slR; }
  function rollUp(C, Wf, h, open, tg) { const out = C.out, zc = h.z0 + open * (h.z1 - h.z0 - 0.12);
    fbox(out, Wf.M, h.u0 - 0.14, h.u1 + 0.14, h.z1, h.z1 + 0.36, 0, 0.3, 'steel', { tag: 'entry.frame' });
    fbox(out, Wf.M, h.u0 - 0.12, h.u0, h.z0, h.z1, 0, 0.12, 'steel', { tag: 'entry.frame' }); fbox(out, Wf.M, h.u1, h.u1 + 0.12, h.z0, h.z1, 0, 0.12, 'steel', { tag: 'entry.frame' });
    if (h.z1 - zc > 0.05) { fbox(out, Wf.M, h.u0, h.u1, zc, h.z1, -0.02, 0.04, 'galv', { tag: tg, tex: TEX.slats }); fbox(out, Wf.M, h.u0, h.u1, zc, zc + 0.1, 0.04, 0.07, 'steel', { tag: tg });
      if (open < 0.3) fbox(out, Wf.M, (h.u0 + h.u1) / 2 - 0.15, (h.u0 + h.u1) / 2 + 0.15, zc + 0.45, zc + 0.55, 0.04, 0.09, 'steel', { tag: tg }); } }
  function louvre(C, Wf, c, z0, w, h) { const out = C.out; fbox(out, Wf.M, c - w / 2 - 0.06, c + w / 2 + 0.06, z0 - 0.06, z0 + h + 0.06, 0, 0.03, 'trim', { tag: 'show.vent' });
    fbox(out, Wf.M, c - w / 2, c + w / 2, z0, z0 + h, 0.03, 0.035, 'dark', { tag: 'show.vent' });
    const n = Math.max(3, Math.round(h / 0.14)); for (let i = 0; i < n; i++) { const z = z0 + h * (i + 0.5) / n; fbox(out, Wf.M, c - w / 2 + 0.03, c + w / 2 - 0.03, z - 0.022, z + 0.03, 0.035, 0.07, 'trim', { tag: 'show.vent' }); } }
  const cutsDoor = (Wq, h) => Wq.capZ != null && Wq.capZ < h.z1 - 0.01;
  function threshold(C, Wf, h) { F(C.out, [Wf.M(h.u0, h.z0 + 0.005, 0.02), Wf.M(h.u1, h.z0 + 0.005, 0.02), Wf.M(h.u1, h.z0 + 0.005, -Wf.t), Wf.M(h.u0, h.z0 + 0.005, -Wf.t)], 'trim', { tag: 'entry.threshold' }); }

  // ---- roofs ------------------------------------------------------------------------------------------------------------
  function roofPlane(C, O, V, ya, yb, L) { const s = C.s, M = (u, v) => [O[0] + V[0] * v, u, O[2] + V[2] * v], P = [[ya, 0], [yb, 0], [yb, L], [ya, L]], tex = ROOFTEX[s.roof];
    const alt = C.lc ? null : (s.roof === 'rusted' ? 'roofGalv' : (s.roof === 'metalSeam' || s.roof === 'corrugated') ? 'roofRust' : 'moss'), metal = alt !== 'moss';
    const base = s.roof === 'rusted' ? 0.3 * (1 - s.weather) : (metal ? 0.2 : 0.3) * s.weather * (1 - 0.6 * s.kept);
    patchy(C, P, M, 'roof', base > 0.01 ? alt : null, (i, j, v) => base * (1.2 - 0.8 * Math.min(1, v / L)), tex, 'roof', metal ? 0.24 : 0.6, metal ? 0.8 : 0.45, null, Math.round(O[0] * 29 + O[2] * 31 + V[0] * 7)); }
  function icicles(C, x, ya, yb, z) { if (C.s.season !== 'winter' || C.s.cut) return; for (let y = ya + 0.1, k = 0; y < yb - 0.05; y += 0.17, k++) { const q = hsh(k, Math.round(x * 10), 81); if (q > 0.62) continue;
    stick(C.out, [x, y, z], [x, y, z - 0.08 - 0.34 * hsh(k, 3, 83)], 0.035, 'ice', { tag: 'weather.icicle' }); } }
  function gableRoof(C, x0, x1, y0, y1, e, r, ov, rk) { if (C.s.cut) return; const out = C.out, xm = (x0 + x1) / 2, hw = (x1 - x0) / 2, sl = (r - e) / hw, ze = e - ov * sl, ya = y0 - rk, yb = y1 + rk, L = Math.hypot(hw + ov, r - ze);
    roofPlane(C, [x0 - ov, 0, ze], [(hw + ov) / L, 0, (r - ze) / L], ya, yb, L); roofPlane(C, [x1 + ov, 0, ze], [-(hw + ov) / L, 0, (r - ze) / L], ya, yb, L);
    const th = 0.09, tm = C.s.siding === 'corrugated' ? 'steel' : 'trim';
    for (const x of [x0 - ov, x1 + ov]) { F(out, [[x, ya, ze - th], [x, yb, ze - th], [x, yb, ze], [x, ya, ze]], tm, { tag: 'roof.fascia' }); icicles(C, x, ya, yb, ze - th); }
    for (const y of [ya, yb]) { F(out, [[x0 - ov, y, ze - th], [xm, y, r - th], [xm, y, r + 0.01], [x0 - ov, y, ze + 0.01]], tm, { tag: 'roof.rake' }); F(out, [[x1 + ov, y, ze - th], [xm, y, r - th], [xm, y, r + 0.01], [x1 + ov, y, ze + 0.01]], tm, { tag: 'roof.rake' }); }
    box(out, xm - 0.06, xm + 0.06, ya, yb, r - 0.02, r + 0.06, 'roof', { tag: 'roof.ridge', b: 1 }); }
  function gambrelRoof(C, hw, y0, y1, e, kx, zk, zr, ov, rk) { if (C.s.cut) return; const out = C.out, s1 = (zk - e) / kx, ze = e - ov * s1, P = [[-hw - ov, ze], [-hw + kx, zk], [0, zr], [hw - kx, zk], [hw + ov, ze]], ya = y0 - rk, yb = y1 + rk, tm = 'trim';
    for (let k = 0; k < 4; k++) { const lo = k < 2 ? P[k] : P[k + 1], hi = k < 2 ? P[k + 1] : P[k], L = Math.hypot(hi[0] - lo[0], hi[1] - lo[1]); roofPlane(C, [lo[0], 0, lo[1]], [(hi[0] - lo[0]) / L, 0, (hi[1] - lo[1]) / L], ya, yb, L); }
    for (const y of [ya, yb]) for (let k = 0; k < 4; k++) { const p = P[k], q = P[k + 1]; F(out, [[p[0], y, p[1] - 0.09], [q[0], y, q[1] - 0.09], [q[0], y, q[1] + 0.01], [p[0], y, p[1] + 0.01]], tm, { tag: 'roof.rake' }); }
    for (const x of [-hw - ov, hw + ov]) { F(out, [[x, ya, ze - 0.09], [x, yb, ze - 0.09], [x, yb, ze], [x, ya, ze]], tm, { tag: 'roof.fascia' }); icicles(C, x, ya, yb, ze - 0.09); }
    for (const x of [-hw + kx, hw - kx]) box(out, x - 0.05, x + 0.05, ya, yb, zk - 0.02, zk + 0.05, 'roof', { tag: 'roof.ridge', b: 1 });
    box(out, -0.06, 0.06, ya, yb, zr - 0.02, zr + 0.06, 'roof', { tag: 'roof.ridge', b: 1 }); }
  function monoRoofX(C, hw, y0, y1, eLo, eHi, ovLo, ovHi, rk) { if (C.s.cut) return; const out = C.out, sl = (eHi - eLo) / (2 * hw), zl = eLo - ovLo * sl, zh = eHi + ovHi * sl, xl = -hw - ovLo, xh = hw + ovHi, L = Math.hypot(xh - xl, zh - zl), ya = y0 - rk, yb = y1 + rk, tm = 'trim';
    roofPlane(C, [xl, 0, zl], [(xh - xl) / L, 0, (zh - zl) / L], ya, yb, L);
    for (const [x, z] of [[xl, zl], [xh, zh]]) F(out, [[x, ya, z - 0.1], [x, yb, z - 0.1], [x, yb, z], [x, ya, z]], tm, { tag: 'roof.fascia' });
    icicles(C, xl, ya, yb, zl - 0.1);
    for (const y of [ya, yb]) F(out, [[xl, y, zl - 0.1], [xh, y, zh - 0.1], [xh, y, zh + 0.01], [xl, y, zl + 0.01]], tm, { tag: 'roof.rake' }); }
  // the fish plant's clerestory monitor: its walls stand ON the roof (pass 1 hung them from the ridge, 1.2 m over the slope)
  function roofMonitor(C) { const s = C.s, out = C.out; if (s.cut || s.shape !== 'gable') return null; const sl = s.pitch, ml = s.Ln * 0.62, my0 = -ml / 2, my1 = ml / 2, mhw = Math.min(s.hw * 0.21, 1.6), zb = s.ridgeZ - mhw * sl, zt = s.ridgeZ + 0.95, mr = zt + mhw * 0.62, ov = 0.18, sid = SIDTEX[s.siding];
    for (const [x, nx] of [[-mhw, -1], [mhw, 1]]) { const Wm = wallFrame(nx < 0 ? [x, my0] : [x, my1], nx < 0 ? [x, my1] : [x, my0], [nx, 0]);
      F(out, [Wm.M(0, zb - 0.06, 0), Wm.M(ml, zb - 0.06, 0), Wm.M(ml, zt, 0), Wm.M(0, zt, 0)], 'body', { tag: 'roof.monitor', tex: sid, uv: [[0, zb], [ml, zb], [ml, zt], [0, zt]] });
      fbox(out, Wm.M, 0.2, ml - 0.2, zt - 0.62, zt - 0.14, -0.01, 0.014, 'glass', { tag: 'roof.monitor', em: 'win:ground:monitor' });
      for (let u = 0.2; u < ml - 0.2; u += 0.8) fbox(out, Wm.M, u - 0.02, u + 0.02, zt - 0.62, zt - 0.14, 0.014, 0.03, 'steel', { tag: 'roof.monitor' });
      fbox(out, Wm.M, 0, ml, zb - 0.06, zb + 0.08, 0, 0.03, 'steel', { tag: 'roof.flashing' }); }
    for (const y of [my0, my1]) for (const sg of [-1, 1]) { const q = [[0, y, s.ridgeZ - 0.06], [sg * mhw, y, zb - 0.06], [sg * mhw, y, zt], [0, y, mr]]; F(out, q, 'body', { tag: 'roof.monitor', tex: sid, uv: q.map(p => [p[0] * sg, p[2]]) }); }
    const ze = zt - ov * 0.62, L = Math.hypot(mhw + ov, mr - ze);
    roofPlane(C, [-mhw - ov, 0, ze], [(mhw + ov) / L, 0, (mr - ze) / L], my0 - ov, my1 + ov, L); roofPlane(C, [mhw + ov, 0, ze], [-(mhw + ov) / L, 0, (mr - ze) / L], my0 - ov, my1 + ov, L);
    box(out, -0.05, 0.05, my0 - ov, my1 + ov, mr - 0.02, mr + 0.05, 'roof', { tag: 'roof.ridge', b: 1 });
    return { mhw, my0, my1 }; }
  function cupola(C) { const s = C.s, out = C.out; if (s.cut || s.shape === 'shed') return; const cz = s.ridgeZ - 0.25, a = 0.45;
    box(out, -a, a, -a, a, cz, cz + 0.85, 'trim', { tag: 'show.cupola', noTop: true });
    for (const [p0, p1, n] of [[[-a, a], [a, a], [0, 1]], [[a, a], [a, -a], [1, 0]], [[a, -a], [-a, -a], [0, -1]], [[-a, -a], [-a, a], [-1, 0]]]) { const Q = wallFrame(p0, p1, n); fbox(out, Q.M, 0.14, 0.76, cz + 0.28, cz + 0.72, 0, 0.012, 'dark', { tag: 'show.cupola' }); for (let k = 0; k < 4; k++) fbox(out, Q.M, 0.14, 0.76, cz + 0.32 + k * 0.1, cz + 0.36 + k * 0.1, 0.012, 0.04, 'trim', { tag: 'show.cupola' }); }
    const ap = cz + 1.35; for (const [p, q] of [[[-0.56, -0.56], [0.56, -0.56]], [[0.56, -0.56], [0.56, 0.56]], [[0.56, 0.56], [-0.56, 0.56]], [[-0.56, 0.56], [-0.56, -0.56]]]) F(out, [[p[0], p[1], cz + 0.85], [q[0], q[1], cz + 0.85], [0, 0, ap]], 'roof', { tag: 'show.cupola' });
    stick(out, [0, 0, ap], [0, 0, ap + 0.5], 0.025, 'cp_iron', { tag: 'show.vane' }); stick(out, [-0.3, 0, ap + 0.38], [0.3, 0, ap + 0.38], 0.03, 'cp_iron', { tag: 'show.vane' }); F(out, [[0.3, 0, ap + 0.3], [0.42, 0, ap + 0.38], [0.3, 0, ap + 0.46]], 'cp_iron', { tag: 'show.vane' }); }
  // stacks and pipes stand ON the roof surface (pass 1 floated them 0.6 m over it); smoke anchors at the cap
  function stackOn(C, x, y, r, top, mat, round) { const s = C.s, out = C.out, zr = gableTop(s, x), slope = s.shape === 'gambrel' ? 2.1 : s.shape === 'shed' ? s.pitch * 0.55 : s.pitch, sink = 0.06 + slope * r * 1.2;
    if (round) { cyl(out, x, y, r, zr - sink, top, 8, mat, { tag: 'roof.stack', noTop: true }); cyl(out, x, y, r * 1.9, top + 0.1, top + 0.16, 8, mat, { tag: 'roof.stack' }); stick(out, [x, y, top], [x, y, top + 0.1], 0.03, mat, { tag: 'roof.stack' }); }
    else { box(out, x - r, x + r, y - r, y + r, zr - sink, top, mat, { tag: 'roof.stack' }); box(out, x - r * 1.35, x + r * 1.35, y - r * 1.35, y + r * 1.35, top, top + 0.08, mat, { tag: 'roof.stack', b: 1 });
      box(out, x - r * 1.55, x + r * 1.55, y - r * 1.55, y + r * 1.55, top + 0.2, top + 0.27, mat, { tag: 'roof.stack', b: 1 }); box(out, x - 0.03, x + 0.03, y - 0.03, y + 0.03, top + 0.08, top + 0.2, 'dark', { tag: 'roof.stack' }); }
    box(out, x - r * 1.7, x + r * 1.7, y - r * 1.7, y + r * 1.7, zr - sink, zr + 0.05, 'steel', { tag: 'roof.flashing', b: -1 });
    C.stacks.push([r3(x), r3(y), r3(top + (round ? 0.18 : 0.14))]); }

  // ---- stairs and ladders built to the v9.2 numbers ----------------------------------------------------------------------
  /* a straight flight descending from the top nosing line (centre `top`, height zTop) along the unit plan direction `dir`:
     n risers of (zTop - zBot)/n and n-1 treads of `going`. solid: each tread a block down to zBot; else a sawtooth on two
     stringers. rail: sides (+1 / -1, along the flight's right-hand normal) that get a handrail 0.9 over the nosings */
  function flight(C, o) { const out = C.out, n = o.n, r = (o.zTop - o.zBot) / n, g = o.going, d = o.dir, pv = [-d[1], d[0]], hw = o.width / 2, tg = o.tag || 'entry.steps';
    const at = (a, sd) => [o.top[0] + d[0] * a + pv[0] * sd, o.top[1] + d[1] * a + pv[1] * sd];
    for (let k = 1; k < n; k++) { const a0 = (k - 1) * g, a1 = k * g, z = o.zTop - k * r, zb = o.solid ? o.zBot : Math.max(o.zBot, z - r - 0.04);
      prism(out, [at(a0, -hw), at(a1, -hw), at(a1, hw), at(a0, hw)], zb, z, o.mat, { tag: tg, topTex: o.tex || null, tex: o.solid ? (o.sideTex || null) : null }); }
    if (!o.solid) for (const sd of [-hw - 0.03, hw + 0.03]) { const p0 = at(-0.02, sd), p1 = at((n - 1) * g + 0.05, sd); stick(out, [p0[0], p0[1], o.zTop - 0.12], [p1[0], p1[1], o.zBot + 0.05], 0.07, o.mat, { tag: tg }); }
    for (const side of (o.rail || [])) { const sd = side * (hw + 0.02), a1 = (n - 1) * g, p0 = at(0.12, sd), p1 = at(a1 - 0.1, sd), zA = o.zTop - r * 0.5 + 0.9, zB = o.zBot + r * 1.5 + 0.9;
      stick(out, [p0[0], p0[1], zA], [p1[0], p1[1], zB], 0.045, o.railMat || 'steel', { tag: tg });
      for (const [p, z] of [[p0, o.zTop - r], [p1, o.zBot + r]]) stick(out, [p[0], p[1], z], [p[0], p[1], z + 0.92], 0.045, o.railMat || 'steel', { tag: tg }); }
    const run = (n - 1) * g;
    return { n, riser: r, going: g, run, width: o.width, dir: d.slice(), top: at(0, 0), zTop: o.zTop, bottom: at(run, 0), zBot: o.zBot, pitchDeg: Math.atan2(r, g) / DEG,
      sides: [[at(0, -hw), at(run, -hw)], [at(0, hw), at(run, hw)]], rect: [at(0, -hw), at(run, -hw), at(run, hw), at(0, hw)] }; }
  function ladder(C, o) { const out = C.out, px = [-o.face[1], o.face[0]], hw = o.width / 2, top = o.zTop + o.rails, n = Math.round((o.zTop - o.z0) / o.rung), rungs = [];
    for (const sg of [-1, 1]) stick(out, [o.x + px[0] * hw * sg, o.y + px[1] * hw * sg, o.z0], [o.x + px[0] * hw * sg, o.y + px[1] * hw * sg, top], 0.06, 'timber', { tag: 'climb.ladder' });
    for (let k = 1; k < n; k++) { const z = o.z0 + k * o.rung; rungs.push(r3(z)); stick(out, [o.x - px[0] * hw, o.y - px[1] * hw, z], [o.x + px[0] * hw, o.y + px[1] * hw, z], 0.042, 'timber', { tag: 'climb.ladder' }); }
    return rungs; }
  function rail(C, a, b, z, o) { o = o || {}; const out = C.out, L = Math.hypot(b[0] - a[0], b[1] - a[1]), n = Math.max(1, Math.ceil(L / (o.span || 1.4))), mat = o.mat || 'steel', top = o.top || 1.0;
    for (let k = 0; k <= n; k++) { const t = k / n, x = a[0] + (b[0] - a[0]) * t, y = a[1] + (b[1] - a[1]) * t; stick(out, [x, y, z], [x, y, z + top], 0.05, mat, { tag: o.tag || 'entry.rail' }); }
    stick(out, [a[0], a[1], z + top], [b[0], b[1], z + top], 0.05, mat, { tag: o.tag || 'entry.rail' }); if (o.mid !== false) stick(out, [a[0], a[1], z + top / 2], [b[0], b[1], z + top / 2], 0.04, mat, { tag: o.tag || 'entry.rail' }); }
  function minus(r, h) { if (h.x0 >= r.x1 || h.x1 <= r.x0 || h.y0 >= r.y1 || h.y1 <= r.y0) return [r]; const x0 = Math.max(r.x0, h.x0), x1 = Math.min(r.x1, h.x1), y0 = Math.max(r.y0, h.y0), y1 = Math.min(r.y1, h.y1);
    return [{ x0: r.x0, x1: r.x1, y0: r.y0, y1: y0 }, { x0: r.x0, x1: r.x1, y0: y1, y1: r.y1 }, { x0: r.x0, x1: x0, y0, y1 }, { x0: x1, x1: r.x1, y0, y1 }].filter(q => q.x1 - q.x0 > 0.01 && q.y1 - q.y0 > 0.01); }
  function slab(C, R, holes, z, mat, tex, tag) { const out = C.out; let rects = [R]; for (const h of holes) rects = rects.flatMap(r => minus(r, h));
    for (const r of rects) { F(out, [[r.x0, r.y0, z], [r.x1, r.y0, z], [r.x1, r.y1, z], [r.x0, r.y1, z]], mat, { tag, tex, uv: [[r.x0, r.y0], [r.x1, r.y0], [r.x1, r.y1], [r.x0, r.y1]] });
      F(out, [[r.x0, r.y0, z - 0.2], [r.x1, r.y0, z - 0.2], [r.x1, r.y1, z - 0.2], [r.x0, r.y1, z - 0.2]], 'inside', { tag, b: -1 }); }
    for (const h of holes) { const x0 = Math.max(R.x0, h.x0), x1 = Math.min(R.x1, h.x1), y0 = Math.max(R.y0, h.y0), y1 = Math.min(R.y1, h.y1);
      for (const [a, b] of [[[x0, y0], [x1, y0]], [[x1, y0], [x1, y1]], [[x1, y1], [x0, y1]], [[x0, y1], [x0, y0]]]) F(out, [[a[0], a[1], z - 0.2], [b[0], b[1], z - 0.2], [b[0], b[1], z], [a[0], a[1], z]], 'timber', { tag, b: -1 }); } }

  // ---- goods and furnishings ---------------------------------------------------------------------------------------------
  function trap(out, x0, x1, y0, y1, z0) { box(out, x0, x1, y0, y1, z0 + 0.03, z0 + 0.38, 'trapW', { tag: 'yard.traps', tex: TEX.mesh, topTex: TEX.mesh });
    for (const y of [y0, y1 - 0.05]) box(out, x0 - 0.01, x1 + 0.01, y, y + 0.05, z0, z0 + 0.05, 'timber', { tag: 'yard.traps' });
    stick(out, [x0 + 0.05, (y0 + y1) / 2, z0 + 0.38], [x1 - 0.05, (y0 + y1) / 2, z0 + 0.38], 0.03, 'timber', { tag: 'yard.traps' }); }
  function buoy(out, x, y, z, col) { cyl(out, x, y, 0.1, z, z + 0.24, 8, col, { tag: 'show.buoys' }); cyl(out, x, y, 0.065, z + 0.24, z + 0.3, 8, col, { tag: 'show.buoys' }); stick(out, [x, y, z + 0.3], [x, y, z + 0.58], 0.024, 'timber', { tag: 'show.buoys' }); }
  function boxStack(C, x0, x1, y0, y1, z, n, mat) { const out = C.out; box(out, x0, x1, y0, y1, z, z + 0.1, 'timber', { tag: 'store.pallet', topTex: TEX.boards });
    for (let k = 0; k < n; k++) { const zz = z + 0.1 + k * 0.28, i = k % 2 ? 0.03 : 0; box(out, x0 + 0.03 + i, x1 - 0.03 - i, y0 + 0.03, y1 - 0.03, zz, zz + 0.27, mat, { tag: 'store.boxes', b: k === n - 1 ? 0 : -1 }); }
    return z + 0.1 + n * 0.28; }
  function bench(C, x0, x1, y0, y1, zTop, zF, mat) { const out = C.out; box(out, x0, x1, y0, y1, zTop - 0.05, zTop, mat || 'timber', { tag: 'store.bench', topTex: TEX.boards });
    for (const [x, y] of [[x0 + 0.05, y0 + 0.05], [x1 - 0.05, y0 + 0.05], [x0 + 0.05, y1 - 0.05], [x1 - 0.05, y1 - 0.05]]) box(out, x - 0.03, x + 0.03, y - 0.03, y + 0.03, zF, zTop - 0.05, mat || 'timber', { tag: 'store.bench' });
    box(out, x0 + 0.02, x1 - 0.02, y0 + 0.02, y1 - 0.02, zF + 0.22, zF + 0.25, mat || 'timber', { tag: 'store.bench' }); }

  // ---- the building ------------------------------------------------------------------------------------------------------
  function build(C) {
    const s = C.s, out = C.out, hw = s.hw, hl = s.hl, fz = s.fH, t = s.t, e = s.eaveZ, L = s.Wd;
    C.fz = fz; C.t = t;
    const xi0 = -hw + t, xi1 = hw - t, yi0 = -hl + t, yi1 = hl - t, showLoft = s.loftKind && !(s.cut && s.storey === 'ground');
    // plinth and floor
    const pm = s.plinth === 'concrete' ? 'conc' : s.plinth === 'sills' ? 'wood' : 'stone', ptex = s.plinth === 'concrete' ? TEX.conc : s.plinth === 'sills' ? TEX.boards : TEX.rubble;
    box(out, -hw - 0.05, hw + 0.05, -hl - 0.05, hl + 0.05, 0, fz, pm, { tag: 'wall.plinth', tex: ptex, noTop: true });
    F(out, [[xi0, yi0, fz], [xi1, yi0, fz], [xi1, yi1, fz], [xi0, yi1, fz]], s.type === 'processing' ? 'concF' : 'floor', { tag: 'store.floor', tex: s.type === 'processing' ? TEX.concFloor : TEX.planks, uv: [[xi0, yi0], [xi1, yi0], [xi1, yi1], [xi0, yi1]] });
    // openings
    const Hf = [], Hb = [], Hpx = [], Hnx = [], ww = 0.82, wh = s.type === 'processing' ? 0.9 : 1.12;
    const bandTop = s.base === 'block' ? fz + (s.type === 'processing' ? 1.1 : Math.min(1.2, s.wallH * 0.3)) : null;
    const sill = s.type === 'processing' ? Math.max(fz + 1.2, bandTop ? bandTop + 0.1 : 0) : Math.max(fz + 1.0, bandTop ? bandTop + 0.15 : 0);
    const door = { u0: hw - s.dw / 2, u1: hw + s.dw / 2, z0: fz, z1: fz + s.dh, kind: 'main' }; Hf.push(door); C.doorH = door;
    let pers = null; if (s.pers) { const pc = s.dw / 2 + 1.0; if (pc + 0.5 < hw - 0.25) { pers = { u0: hw + pc - 0.5, u1: hw + pc + 0.5, z0: fz, z1: fz + 2.1, kind: 'pers', x: pc }; Hf.push(pers); } }
    const peakX = s.shape === 'shed' ? hw * 0.35 : 0, room = (x0, x1) => minTop(s, x0, x1);
    let loftH = null, loftWin = null;
    if (s.loft === 'door' && s.loftKind === 'loft') { const x0 = peakX - 0.55, x1 = peakX + 0.55, z0 = s.loftZ, hgt = Math.min(1.3, room(x0 - 0.12, x1 + 0.12) - 0.15 - z0);
      if (hgt >= 0.95) { loftH = { u0: hw + x0, u1: hw + x1, z0, z1: z0 + hgt, walk: false, kind: 'loft', x: peakX }; Hf.push(loftH); } }
    if (!loftH && s.loft !== 'none') { const x0 = peakX - 0.36, x1 = peakX + 0.36, z0 = s.loftKind === 'loft' ? s.loftZ + 0.85 : e + (s.ridgeZ - e) * 0.42 - 0.35, hgt = Math.min(0.8, room(x0 - 0.1, x1 + 0.1) - 0.17 - z0);
      if (hgt >= 0.45 && z0 > door.z1 + 0.3) { loftWin = { u0: hw + x0, u1: hw + x1, z0, z1: z0 + hgt, walk: false, kind: 'win', room: s.loftKind ? 'loft' : 'ground' }; Hf.push(loftWin); } }
    for (const xw of [-hw * 0.42, hw * 0.42]) Hb.push({ u0: hw - xw - ww / 2, u1: hw - xw + ww / 2, z0: sill, z1: sill + wh, walk: false, kind: 'win', room: 'ground' });
    let backPeak = null; if (!s.vents && s.shape !== 'shed') { const z0 = s.loftKind === 'loft' ? s.loftZ + 0.85 : e + (s.ridgeZ - e) * 0.42 - 0.35, hgt = Math.min(0.8, room(-0.46, 0.46) - 0.17 - z0);
      if (hgt >= 0.45) { backPeak = { u0: hw - 0.36, u1: hw + 0.36, z0, z1: z0 + hgt, walk: false, kind: 'win', room: s.loftKind ? 'loft' : 'ground' }; Hb.push(backPeak); } }
    const bays = [];
    if (s.dock) { const n = s.Wd > 8 ? 3 : 2, bw = Math.min(2.6, s.Ln * 0.7 / n), bh = Math.max(2.1, Math.min(s.wallH * 0.7, 2.9, s.loftKind === 'mezz' ? s.loftZ - fz - 0.25 : 9));
      for (let i = 0; i < n; i++) { const c = -hl + s.Ln * ((i + 0.6) / (n + 0.2)); bays.push({ c, w: bw, h: bh, i }); Hpx.push({ u0: hl - c - bw / 2, u1: hl - c + bw / 2, z0: fz, z1: fz + bh, kind: 'bay', i }); } }
    const nW = Math.max(1, Math.round(s.Ln / 2.6 * (0.5 + s.winD))), winY = [];
    for (let i = 0; i < nW; i++) winY.push(-hl + s.Ln * ((i + 0.5) / nW));
    for (const c of winY) { if (!bays.some(b => Math.abs(c - b.c) < b.w / 2 + 0.75)) Hpx.push({ u0: hl - c - ww / 2, u1: hl - c + ww / 2, z0: sill, z1: sill + wh, walk: false, kind: 'win', room: 'ground', y: c });
      Hnx.push({ u0: c + hl - ww / 2, u1: c + hl + ww / 2, z0: sill, z1: sill + wh, walk: false, kind: 'win', room: 'ground', y: c }); }
    if (s.type === 'processing') { const zc = Math.max(e - 1.1, fz + 2.35, bays.length ? fz + bays[0].h + 0.2 : 0), ch = Math.min(0.8, e - 0.14 - zc), nH = Math.max(2, Math.round(s.Ln / 2.4));
      if (ch > 0.4) for (let i = 0; i < nH; i++) { const c = -hl + s.Ln * ((i + 0.5) / nH);
        Hpx.push({ u0: hl - c - 0.36, u1: hl - c + 0.36, z0: zc, z1: zc + ch, walk: false, kind: 'win', room: 'ground', style: 'industrial' });
        Hnx.push({ u0: c + hl - 0.36, u1: c + hl + 0.36, z0: zc, z1: zc + ch, walk: false, kind: 'win', room: 'ground', style: 'industrial' }); } }
    // walls
    const sid = SIDTEX[s.siding], wxTex = (u, v) => { let b = sid(u, v); if (s.weather > 0.05) { if (hsh(Math.floor(u / 0.9), Math.floor(v / 0.7), 91 + s.seed) < s.weather * 0.16) b -= 1; if (v < fz + 0.32 && hsh(Math.floor(u / 0.13), Math.floor(v / 0.1), 93) < s.weather * 0.55) b -= 1; } return b; };
    const peel = (!C.lc && !s.metalBody && s.siding !== 'shingle' && s.siding !== 'corrugated' && s.body !== 'greyShingle') ? clamp((1 - s.kept) * 0.36 * (0.3 + s.weather), 0, 0.3) : 0;
    const inTex = s.type === 'processing' ? TEX.block : TEX.boards, inMat = s.type === 'processing' ? 'insideP' : 'inside';
    const wO = (o) => Object.assign({ tex: wxTex, t, inTex, inMat, alt: peel > 0 ? 'bare' : null, altP: peel, z0: fz }, o);
    const eX = s.shape === 'shed' ? s.eHi : e, eN = s.shape === 'shed' ? s.eLo : e;
    const gab = s.shape === 'gambrel' ? [[0, fz], [L, fz], [L, e], [L - s.kx, s.zk], [hw, s.ridgeZ], [s.kx, s.zk], [0, e]] : [[0, fz], [L, fz], [L, e], [hw, s.ridgeZ], [0, e]];
    const fOut = s.shape === 'shed' ? [[0, fz], [L, fz], [L, s.eHi], [0, s.eLo]] : gab, bOut = s.shape === 'shed' ? [[0, fz], [L, fz], [L, s.eLo], [0, s.eHi]] : gab;
    C.capFor = (drop, eave) => { if (!s.cut) return null; if (s.storey === 'loft' && s.loftZ) return drop ? s.loftZ + s.cutH : eave; return drop ? fz + s.cutH : (s.loftZ ? Math.min(s.loftZ, eave) : eave); };
    const Wf = wall(C, wO({ p0: [-hw, hl], p1: [hw, hl], n: [0, 1], outline: fOut, holes: Hf, tag: 'show.front', eave: e }));
    const Wx = wall(C, wO({ p0: [hw, hl], p1: [hw, -hl], n: [1, 0], holes: Hpx, tag: 'wall.side', eave: eX }));
    const Wb = wall(C, wO({ p0: [hw, -hl], p1: [-hw, -hl], n: [0, -1], outline: bOut, holes: Hb, tag: 'wall.back', eave: e }));
    const Wn = wall(C, wO({ p0: [-hw, -hl], p1: [-hw, hl], n: [-1, 0], holes: Hnx, tag: 'wall.side', eave: eN }));
    C.W = { front: Wf, px: Wx, back: Wb, nx: Wn };
    const cm = s.siding === 'corrugated' ? 'steel' : 'trim';
    cornerBoards(C, Wf, fz, eN, eX, cm); cornerBoards(C, Wx, fz, eX, eX, cm); cornerBoards(C, Wb, fz, eX, eN, cm); cornerBoards(C, Wn, fz, eN, eN, cm);
    if (!s.cut) { fbox(out, Wx.M, 0, Wx.L, eX - 0.2, eX - 0.02, 0, 0.03, cm, { tag: 'wall.frieze' }); fbox(out, Wn.M, 0, Wn.L, eN - 0.2, eN - 0.02, 0, 0.03, cm, { tag: 'wall.frieze' }); }
    if (bandTop) for (const q of [Wf, Wx, Wb, Wn]) skin(C, q, fz, bandTop, 'cinder', TEX.block, 'wall.base', { flash: true });
    if (s.metalBody && s.weather > 0.2 && !C.lc) for (const q of [Wf, Wx, Wb, Wn]) skin(C, q, fz, fz + 0.2, 'rust', null, 'weather.rust', { prob: s.weather * 0.7 });
    // doors and windows
    let nWin = 0; const winIn = (Wq, h) => windowIn(C, Wq, h, { style: h.style, em: 'win:' + (h.room || 'ground') + ':' + (nWin++) });
    if (!cutsDoor(Wf, door)) { const th = s.doorOpen * DEG100;
      if (s.door === 'doubleBarn') { casing(C, Wf, door, { w: 0.11 }); const wd = (door.u1 - door.u0) / 2 - 0.015; ledgeLeaf(C, Wf, door, 'u0', th, 'door', { wd }); ledgeLeaf(C, Wf, door, 'u1', th, 'door', { wd });
        fbox(out, Wf.M, door.u0 - 0.2, door.u1 + 0.2, door.z1 + 0.11, door.z1 + 0.25, 0, 0.05, 'timber', { tag: 'entry.frame' }); C.doorTop = door.z1 + 0.25; C.clearW = door.u1 - door.u0 - 0.1; }
      else if (s.door === 'slidingBarn') { casing(C, Wf, door, { w: 0.1 }); const T = slideTravel(Wf, door); slidingDoors(C, Wf, door, s.doorOpen); C.doorTop = door.z1 + 0.2; C.clearW = Math.min(door.u1 - door.u0, T.L + T.R) - 0.05; }
      else if (s.door === 'plank') { casing(C, Wf, door, { w: 0.09 }); ledgeLeaf(C, Wf, door, 'u0', th, 'door', { ledges: [door.z0 + 0.3, door.z1 - 0.45] }); C.doorTop = door.z1 + 0.09; C.clearW = door.u1 - door.u0 - 0.1; }
      else if (s.door === 'rollUp') { rollUp(C, Wf, door, s.doorOpen, 'door.main'); C.doorTop = door.z1 + 0.36; C.clearW = door.u1 - door.u0; }
      else { casing(C, Wf, door, { w: 0.09 }); persLeaf(C, Wf, door, th, 'door', 'door.main'); C.doorTop = door.z1 + 0.09; C.clearW = door.u1 - door.u0 - 0.08; }
      if (pers && !cutsDoor(Wf, pers)) { casing(C, Wf, pers, { w: 0.08, mat: s.siding === 'corrugated' ? 'steel' : 'trim' }); persLeaf(C, Wf, pers, s.persOpen * DEG100, 'door', 'door.pers'); } else if (pers) threshold(C, Wf, pers); }
    else { threshold(C, Wf, door); if (pers) threshold(C, Wf, pers); C.doorTop = door.z1 + 0.2; C.clearW = door.u1 - door.u0 - 0.1; }
    for (const h of Hf) if (h.kind === 'win') winIn(Wf, h); for (const h of Hb) winIn(Wb, h); for (const h of Hpx) if (h.kind === 'win') winIn(Wx, h); for (const h of Hnx) winIn(Wn, h);
    for (const h of Hpx) if (h.kind === 'bay') { if (!cutsDoor(Wx, h)) rollUp(C, Wx, h, s.bayOpen, 'door.bay'); else threshold(C, Wx, h); }
    // gable peaks: vents, the sign
    const peakZ = e + (s.ridgeZ - e) * 0.42;
    if (s.vents && !loftH && !loftWin && s.shape !== 'shed' && !(s.cut)) { const vh = Math.min(1.0, room(-0.55, 0.55) - 0.2 - (peakZ - 0.2)); if (vh > 0.35) louvre(C, Wf, hw, peakZ - 0.2, 0.9, vh); }
    if (s.vents && s.shape !== 'shed' && !s.cut) { const vh = Math.min(1.0, room(-0.55, 0.55) - 0.2 - (peakZ - 0.2)); if (vh > 0.35) louvre(C, Wb, hw, peakZ - 0.2, 0.9, vh); }
    C.sign = null;
    if (s.sign && !(Wf.capZ != null && Wf.capZ < s.eaveZ - 1.3)) { let w = Math.min(s.Wd * 0.74, 4.4); const hS = 1.0, z0 = Math.max((C.doorTop || door.z1) + 0.28, e - 1.25), xb = s.boom ? boomX(s) : 1e9;
      for (let k = 0; k < 24 && (room(-w / 2 - 0.1, w / 2 + 0.1) - 0.16 < z0 + hS || w / 2 + 0.2 > xb - 0.12); k++) w -= 0.2;
      if (w >= 1.4) { fbox(out, Wf.M, hw - w / 2 - 0.1, hw + w / 2 + 0.1, z0 - 0.1, z0 + hS + 0.1, 0, 0.05, 'timber', { tag: 'show.sign' }); fbox(out, Wf.M, hw - w / 2, hw + w / 2, z0, z0 + hS, 0.05, 0.07, 'sign', { tag: 'show.sign' });
        C.sign = { x: 0, y: r3(hl + 0.07), z0: r3(z0), z1: r3(z0 + hS), w: r3(w) }; } }
    // roof and what stands on it
    if (s.shape === 'gambrel') gambrelRoof(C, hw, -hl, hl, e, s.kx, s.zk, s.ridgeZ, 0.28, 0.24);
    else if (s.shape === 'shed') monoRoofX(C, hw, -hl, hl, s.eLo, s.eHi, 0.32, 0.22, 0.24);
    else gableRoof(C, -hw, hw, -hl, hl, e, s.ridgeZ, 0.32, 0.26);
    const mon = s.cupola === 'monitor' ? roofMonitor(C) : null; if (s.cupola === 'cupola') cupola(C);
    if (!s.cut) { const ns = s.stacks, sx = -(mon ? mon.mhw + 0.75 : hw * 0.3); for (let i = 0; i < ns; i++) stackOn(C, sx, -hl + s.Ln * ((i + 1) / (ns + 1)), 0.13, s.ridgeZ + 1.1, 'steel', false);
      if (s.stove) stackOn(C, Math.min(0.4, hw * 0.2), -hl + 0.62, 0.075, s.ridgeZ + 0.5, 'soot', true); }
    // the interior, the entry, the outside
    C.lofted = showLoft && !C.lc;   // the lifecycle pass reads a single-skinned shell, as pass 1 drew it
    if (s.type === 'shack') interiorShack(C, { xi0, xi1, yi0, yi1, Hnx, winY, ww });
    else if (s.type === 'storage') interiorStorage(C, { xi0, xi1, yi0, yi1, Hpx });
    else interiorPlant(C, { xi0, xi1, yi0, yi1 });
    entry(C, door, pers);
    if (loftH) loftDoor(C, Wf, loftH);
    if (s.dock) dock(C, bays);
    if (s.boom) boom(C);
    if (s.hvac) { const hy = -hl + s.Ln * 0.32, x0 = -hw - 1.5, x1 = -hw - 0.2; box(out, x0, x1, hy - 0.8, hy + 0.8, 0, 0.1, 'conc', { tag: 'yard.pad' }); box(out, x0 + 0.1, x1 - 0.1, hy - 0.68, hy + 0.68, 0.1, 1.15, 'galv', { tag: 'yard.hvac' });
      for (let z = 0.3; z < 1.05; z += 0.14) fbox(out, wallFrame([x1 - 0.1, hy + 0.68], [x1 - 0.1, hy - 0.68], [1, 0]).M, 0.08, 1.28, z - 0.02, z + 0.02, 0, 0.012, 'dark', { tag: 'yard.hvac' });
      const fx = (x0 + x1) / 2; cyl(out, fx, hy, 0.4, 1.15, 1.17, 10, 'dark', { tag: 'yard.hvac' }); box(out, fx - 0.42, fx + 0.42, hy - 0.025, hy + 0.025, 1.17, 1.2, 'steel', { tag: 'yard.hvac' }); box(out, fx - 0.025, fx + 0.025, hy - 0.42, hy + 0.42, 1.17, 1.2, 'steel', { tag: 'yard.hvac' });
      C.solids0.push({ id: 'hvac', x0, x1: -hw, y0: hy - 0.8, y1: hy + 0.8 }); C.foot.push({ id: 'hvac', x0, x1, y0: hy - 0.8, y1: hy + 0.8, walk: false }); }
    lamps(C, door);
    if (s.goods && s.type === 'shack') shackGoods(C, Wx, Hpx);
  }
  function boomX(s) { return s.type === 'processing' ? clamp(s.hw * 0.55, s.dw / 2 + 0.35, s.hw - 0.5) : clamp(s.hw * 0.55, 0.5, s.hw - 0.35); }

  function entry(C, door, pers) { const s = C.s, out = C.out, hl = s.hl, fz = s.fH;
    if (s.type === 'processing') { apron(C, pers); return; }
    if (s.dw >= 1.6) { const L = Math.max(1.1, fz / 0.3), x0 = -s.dw / 2 - 0.15, x1 = s.dw / 2 + 0.15, mat = s.type === 'storage' ? 'stone' : 'wood', tex = mat === 'stone' ? TEX.rubble : TEX.boards;
      F(out, [[x0, hl + 0.01, fz], [x1, hl + 0.01, fz], [x1, hl + L, 0.02], [x0, hl + L, 0.02]], mat, { tag: 'entry.ramp', tex, uv: [[x0, 0], [x1, 0], [x1, L], [x0, L]] });
      for (const x of [x0, x1]) F(out, [[x, hl + 0.01, 0], [x, hl + 0.01, fz], [x, hl + L, 0.02], [x, hl + L, 0]], mat, { tag: 'entry.ramp', b: -1 });
      if (mat === 'wood') for (let a = 0.25; a < L - 0.1; a += 0.3) { const z = fz * (1 - a / L); box(out, x0 + 0.05, x1 - 0.05, hl + a - 0.025, hl + a + 0.025, z - 0.01, z + 0.025, 'timber', { tag: 'entry.ramp' }); }
      C.entry = { kind: 'ramp', len: r3(L), slopeDeg: r3(Math.atan2(fz, L) / DEG), x0, x1, y0: hl, y1: hl + L, zTop: fz };
      C.foot.push({ id: 'ramp', x0, x1, y0: hl, y1: hl + L, walk: true }); }
    else { const n = Math.max(1, Math.round(fz / 0.19)), fl = flight(C, { top: [0, hl], dir: [0, 1], width: s.dw + 0.4, n, zTop: fz, zBot: 0, going: 0.3, mat: 'wood', solid: true, tex: TEX.boards, tag: 'entry.steps' });
      C.entry = { kind: 'steps', n, riser: r3(fl.riser), going: 0.3, run: r3(fl.run), y1: hl + fl.run, zTop: fz }; C.foot.push({ id: 'steps', x0: -s.dw / 2 - 0.2, x1: s.dw / 2 + 0.2, y0: hl, y1: hl + Math.max(0.3, fl.run), walk: true }); }
    const far = C.entry.kind === 'ramp' ? C.entry.len : C.entry.run; C.approach = [0, hl + Math.max(1.5, far + 0.5)];
    C.entryPath = [[0, C.approach[1], 0], [0, hl + far + 0.2, 0], [0, hl + 0.15, fz], [0, hl - s.t - 0.6, fz]]; }
  function apron(C, pers) { const s = C.s, out = C.out, hw = s.hw, hl = s.hl, fz = s.fH, ad = 2.4, ax0 = -s.dw / 2 - 0.9, ax1 = Math.min(hw - 0.02, pers ? pers.x + 0.75 : s.dw / 2 + 0.9);
    box(out, ax0, ax1, hl + 0.01, hl + ad, 0, fz, 'conc', { tag: 'entry.apron', tex: TEX.conc, topMat: 'concF', topTex: TEX.concFloor });
    box(out, ax0, ax1, hl + ad - 0.05, hl + ad + 0.01, fz - 0.1, fz + 0.005, 'steel', { tag: 'entry.apron' });
    const n = Math.ceil(fz / 0.19 - 1e-9), sw = 1.1, fl = flight(C, { top: [ax0, hl + ad - sw / 2], dir: [-1, 0], width: sw, n, zTop: fz, zBot: 0, going: 0.28, mat: 'conc', solid: true, tex: TEX.concFloor, sideTex: TEX.conc, rail: [-1, 1], tag: 'entry.steps' });
    const xb = s.boom ? boomX(s) : null;
    { const y = hl + ad - 0.04; if (xb != null) { rail(C, [ax0 + 0.05, y], [xb - 0.72, y], fz); rail(C, [xb + 0.72, y], [ax1 - 0.04, y], fz); } else rail(C, [ax0 + 0.05, y], [ax1 - 0.04, y], fz);
      rail(C, [ax1 - 0.04, hl + 0.12], [ax1 - 0.04, y], fz); rail(C, [ax0 + 0.04, hl + 0.12], [ax0 + 0.04, hl + ad - sw - 0.02], fz); }
    C.segs0.push({ a: [ax0, hl + ad], b: [ax1, hl + ad] }, { a: [ax1, hl], b: [ax1, hl + ad] }, { a: [ax0, hl], b: [ax0, hl + ad - sw] }, { a: fl.sides[0][0], b: fl.sides[0][1] }, { a: fl.sides[1][0], b: fl.sides[1][1] });
    C.foot.push({ id: 'apron', x0: ax0, x1: ax1, y0: hl, y1: hl + ad, walk: true, z: fz }, { id: 'apronStair', x0: ax0 - fl.run, x1: ax0, y0: hl + ad - sw, y1: hl + ad, walk: true });
    C.stairs.push(Object.assign({ id: 'apronStair', outside: true, levels: [0, 0], landTop: [ax0 + 0.55, hl + ad - sw / 2], landBot: [ax0 - fl.run - 0.5, hl + ad - sw / 2] }, fl));
    C.apron = { x0: ax0, x1: ax1, y0: hl, y1: hl + ad, z: fz };
    C.entry = { kind: 'apron', stair: 'apronStair', zTop: fz };
    C.approach = [ax0 - fl.run - 1.5, hl + ad - sw / 2];
    C.entryPath = [[C.approach[0], C.approach[1], 0], [ax0 - fl.run - 0.3, hl + ad - sw / 2, 0], [ax0 + 0.5, hl + ad - sw / 2, fz], [0, hl + 0.7, fz], [0, hl - s.t - 0.8, fz]]; }
  function dock(C, bays) { const s = C.s, out = C.out, hw = s.hw, hl = s.hl, fz = s.fH, dx0 = hw + 0.02, dx1 = hw + 2.0, dy0 = -hl + 0.4, dy1 = hl - 0.4;
    box(out, dx0, dx1, dy0, dy1, 0, fz, 'conc', { tag: 'dock', tex: TEX.conc, topMat: 'concF', topTex: TEX.concFloor });
    box(out, dx1 - 0.05, dx1 + 0.01, dy0, dy1, fz - 0.12, fz + 0.005, 'steel', { tag: 'dock' });
    for (const b of bays) for (const sg of [-1, 1]) { const y = b.c + sg * (b.w / 2 - 0.2); box(out, dx1, dx1 + 0.12, y - 0.12, y + 0.12, fz - 0.5, fz - 0.08, 'dark', { tag: 'dock.bumper' }); }
    const n = Math.max(1, Math.ceil(fz / 0.19 - 1e-9)), sw = 1.1, fl = flight(C, { top: [dx1 - sw / 2, dy1], dir: [0, 1], width: sw, n, zTop: fz, zBot: 0, going: 0.28, mat: 'conc', solid: true, tex: TEX.concFloor, sideTex: TEX.conc, rail: [1], tag: 'dock.stair' });
    C.segs0.push({ a: [dx1, dy0], b: [dx1, dy1] }, { a: [dx0, dy0], b: [dx1, dy0] }, { a: [dx0, dy1], b: [dx1 - sw, dy1] }, { a: fl.sides[0][0], b: fl.sides[0][1] }, { a: fl.sides[1][0], b: fl.sides[1][1] });
    C.foot.push({ id: 'dock', x0: dx0, x1: dx1, y0: dy0, y1: dy1, walk: true, z: fz }, { id: 'dockStair', x0: dx1 - sw, x1: dx1, y0: dy1, y1: dy1 + fl.run, walk: true });
    C.stairs.push(Object.assign({ id: 'dockStair', outside: true, levels: [0, 0], landTop: [dx1 - sw / 2, dy1 - 0.55], landBot: [dx1 - sw / 2, dy1 + fl.run + 0.5] }, fl));
    C.dockR = { x0: dx0, x1: dx1, y0: dy0, y1: dy1, z: fz };
    if (s.type === 'processing') { const px0 = dx0 + 0.25, py0 = dy0 + 0.5, top = boxStack(C, px0, px0 + 1.0, py0, py0 + 0.8, fz, 3, 'tote');
      C.solids0.push({ id: 'dockPallet', x0: px0, x1: px0 + 1.0, y0: py0, y1: py0 + 0.8 });
      C.st.push({ id: 'dockPallet', verb: 'loadOut', clip: 'place', also: ['lift'], fixture: 'load', fixtureZ: r3(top - fz), at: [px0 + 0.5, py0 + 0.8, top], stand: [px0 + 0.5, py0 + 1.35], standZ: fz, level: 0, note: 'totes for the truck, stacked three high on a pallet on the dock' }); }
    C.bays = bays.map(b => ({ i: b.i, y: r3(b.c), width: r3(b.w), height: r3(b.h), z: fz })); }
  function boom(C) { const s = C.s, out = C.out, hl = s.hl, fz = s.fH, xb = boomX(s), W = C.W.front; if (W.capZ != null && W.capZ < s.eaveZ) return;
    const plant = s.type === 'processing', reach = plant ? 2.4 + 0.7 : 1.6, zm0 = plant ? fz + 2.3 : Math.max(C.doorTop || 0, s.fH + s.dh) + 0.3, zm1 = Math.min(gableTop(s, xb) - 0.15, s.eaveZ + 0.35);
    if (zm1 - zm0 < 0.8) return; const y = hl + 0.11, tip = [xb, hl + reach, zm1 - 0.25], hookZ = plant ? fz + 1.35 : 1.55;
    stick(out, [xb, y, zm0], [xb, y, zm1 + 0.1], 0.14, 'steel', { tag: 'show.boom' });
    stick(out, [xb, y, zm1], tip, 0.12, 'steel', { tag: 'show.boom' }); stick(out, [xb, y, zm1 - 1.0], [xb, hl + reach * 0.45, zm1 - 0.14], 0.08, 'steel', { tag: 'show.boom' });
    box(out, xb - 0.09, xb + 0.09, tip[1] - 0.07, tip[1] + 0.07, tip[2] - 0.26, tip[2] - 0.04, 'steel', { tag: 'show.boom' });
    stick(out, [xb, tip[1], tip[2] - 0.26], [xb, tip[1], hookZ + 0.1], 0.024, 'rope', { tag: 'show.boom' }); box(out, xb - 0.05, xb + 0.05, tip[1] - 0.03, tip[1] + 0.03, hookZ - 0.08, hookZ + 0.1, 'cp_iron', { tag: 'show.boom' });
    const stZ = plant ? fz : 0, stand = [xb, tip[1] - 0.5];
    C.st.push({ id: 'boom', verb: plant ? 'hoistTote' : 'hoist', clip: 'haul', fixture: null, at: [xb, tip[1], hookZ], stand, standZ: stZ, level: 0, note: plant ? 'tail the whip: the boom lifts totes from the boats onto the apron through the gap in the rail' : 'haul on the whip from the ground' }); }
  function loftDoor(C, Wf, h) { const s = C.s, out = C.out, hl = s.hl, hw = s.hw; if (cutsDoor(Wf, h)) return;
    casing(C, Wf, h, { w: 0.09 }); ledgeLeaf(C, Wf, h, 'u0', s.loftOpen * DEG100, 'door', { tag: 'door.loft', ledges: [h.z0 + 0.15, h.z1 - 0.28] });
    if (s.cut) return; const xc = h.x, hwd = (h.u1 - h.u0) / 2 + 0.3, ze = h.z1 + 0.22, zr = ze + hwd * 0.62, d = 0.9, far = C.entry && C.entry.kind === 'ramp' ? C.entry.len : 0.6;
    for (const sg of [-1, 1]) { const L = Math.hypot(hwd, zr - ze); roofPlaneLocal(out, [xc + sg * hwd, hl, ze], [xc, hl, zr], [xc, hl + d, zr], [xc + sg * hwd, hl + d, ze], s); }
    F(out, [[xc - hwd, hl + d, ze], [xc + hwd, hl + d, ze], [xc, hl + d, zr]], 'trim', { tag: 'show.hood' });
    for (const sg of [-1, 1]) stick(out, [xc + sg * (hwd - 0.08), hl + 0.02, ze - 0.4], [xc + sg * (hwd - 0.08), hl + d - 0.1, ze - 0.02], 0.06, 'timber', { tag: 'show.hood' });
    const zb = ze + 0.14, tipY = hl + Math.max(1.05, far + 0.1); stick(out, [xc, hl - 0.3, zb], [xc, tipY, zb], 0.14, 'timber', { tag: 'show.hoist' });
    cyl(out, xc, tipY - 0.08, 0.075, zb - 0.26, zb - 0.07, 8, 'cp_iron', { tag: 'show.hoist' }); stick(out, [xc, tipY - 0.08, zb - 0.26], [xc, tipY - 0.08, 1.05], 0.024, 'rope', { tag: 'show.hoist' });
    box(out, xc - 0.04, xc + 0.04, tipY - 0.11, tipY - 0.05, 0.97, 1.07, 'cp_iron', { tag: 'show.hoist' });
    C.st.push({ id: 'hoist', verb: 'hoist', clip: 'haul', fixture: null, at: [xc, tipY - 0.08, 1.05], stand: [xc, tipY + 0.37], standZ: 0, level: 0, note: 'haul the loft whip from the wharf; the beam runs out past the ramp' });
    C.st.push({ id: 'loftDoor', verb: 'takeHoisted', clip: 'lift', also: ['place'], fixture: 'load', fixtureZ: 0.85, at: [xc, hl - s.t, s.loftZ + 0.85], stand: [xc, hl - s.t - 0.62], standZ: s.loftZ, level: 1, note: 'swing the load in at the open loft door' }); }
  function roofPlaneLocal(out, a, b, c, d, s) { F(out, [a, b, c, d], 'roof', { tag: 'show.hood', tex: ROOFTEX[s.roof], uv: [[0, 0], [0, 1], [1, 1], [1, 0]] }); }
  function lamps(C, door) { const s = C.s, out = C.out, W = C.W.front, hl = s.hl, hw = s.hw, fz = s.fH; if (W.capZ != null && W.capZ < fz + s.dh + 0.4) return;
    if (s.type === 'processing') { const u = hw - s.dw / 2 - 0.5, z = fz + Math.min(s.dh, 2.6) + 0.2; fbox(out, W.M, u - 0.16, u + 0.16, z, z + 0.24, 0, 0.2, 'steel', { tag: 'entry.lamp' });
      fbox(out, W.M, u - 0.13, u + 0.13, z - 0.02, z + 0.02, 0.03, 0.18, 'cp_warm', { tag: 'entry.lamp', em: 'lamp:front' }); C.lamps.push({ id: 'front', kind: 'pack', p: W.M(u, z - 0.05, 0.25), pool: [u - hw, hl + 1.8], r: 4.2, c: '#ffb765' });
      if (C.bays) for (const b of C.bays) { const Wx = C.W.px, uu = hl - b.y, zz = fz + b.height + 0.42; if (Wx.capZ != null && Wx.capZ < zz + 0.25) continue; fbox(out, Wx.M, uu - 0.16, uu + 0.16, zz, zz + 0.24, 0, 0.2, 'steel', { tag: 'dock.lamp' });
        fbox(out, Wx.M, uu - 0.13, uu + 0.13, zz - 0.02, zz + 0.02, 0.03, 0.18, 'cp_warm', { tag: 'dock.lamp', em: 'lamp:bay' + b.i }); C.lamps.push({ id: 'bay' + b.i, kind: 'pack', p: Wx.M(uu, zz - 0.05, 0.25), pool: [hw + 1.6, b.y], r: 3.6, c: '#ffb765' }); }
      return; }
    const z = (C.doorTop || fz + s.dh) + 0.34; if (z + 0.25 > minTop(s, -0.3, 0.3) - 0.1) return;
    stick(out, W.M(hw, z, 0), W.M(hw, z + 0.12, 0.34), 0.03, 'cp_iron', { tag: 'entry.lamp' }); fbox(out, W.M, hw - 0.11, hw + 0.11, z - 0.02, z + 0.1, 0.26, 0.44, 'cp_iron', { tag: 'entry.lamp' });
    fbox(out, W.M, hw - 0.07, hw + 0.07, z - 0.08, z - 0.02, 0.3, 0.4, 'cp_warm', { tag: 'entry.lamp', em: 'lamp:door' });
    C.lamps.push({ id: 'door', kind: 'door', p: W.M(hw, z - 0.1, 0.35), pool: [0, hl + 1.3], r: 3.6, c: '#ffc774' }); }
  function shackGoods(C, Wx, Hpx) { const s = C.s, out = C.out, hw = s.hw, hl = s.hl;
    const x0 = s.dw / 2 + 0.3, y0 = hl + 0.12; trap(out, x0, x0 + 0.9, y0, y0 + 0.55, 0); trap(out, x0 + 0.95, x0 + 1.85, y0, y0 + 0.55, 0); trap(out, x0 + 0.45, x0 + 1.35, y0 + 0.02, y0 + 0.53, 0.4);
    C.solids0.push({ id: 'traps', x0, x1: x0 + 1.85, y0, y1: y0 + 0.55 }); C.foot.push({ id: 'traps', x0, x1: x0 + 1.85, y0, y1: y0 + 0.55, walk: false });
    C.st.push({ id: 'traps', verb: 'fetchTrap', clip: 'lift', also: ['place'], fixture: 'load', fixtureZ: 0.78, at: [x0 + 0.9, y0 + 0.28, 0.78], stand: [x0 + 0.9, y0 + 1.05], standZ: 0, level: 0, note: 'the top trap of the stack by the door; its lid is at 0.78' });
    const zB = s.fH + 1.25; if (Wx.capZ != null && Wx.capZ < zB + 0.75) return; const cols = ['buoyR', 'buoyY', 'buoyW', 'buoyO', 'buoyR', 'buoyY'], z = s.fH + 1.25, wins = Hpx.filter(h => h.kind === 'win' && h.z0 < z + 0.7);
    let k = 0; for (let u = 0.45; u < s.Ln - 0.4 && k < 6; u += 0.36) { if (wins.some(h => u > h.u0 - 0.26 && u < h.u1 + 0.26)) continue; const y = hl - u, zz = z + (k % 2) * 0.12;
      buoy(out, hw + 0.13, y, zz, cols[k]); stick(out, [hw + 0.02, y, zz + 0.66], [hw + 0.13, y, zz + 0.58], 0.02, 'rope', { tag: 'show.buoys' }); k++; } }

  function interiorShack(C, I) { const s = C.s, out = C.out, fz = s.fH, { xi0, xi1, yi0, yi1 } = I;
    if (s.loftKind === 'loft') { const lx = -Math.min(0.3, s.standHalf - 0.4), ya = yi0 + 1.1, rungY = ya + 0.16, hatch = { x0: lx - 0.45, x1: lx + 0.45, y0: ya, y1: ya + 0.95 };
      if (C.lofted) slab(C, { x0: xi0, x1: xi1, y0: yi0, y1: yi1 }, [hatch], s.loftZ, 'floor', TEX.planks, 'store.loft');
      const rungs = ladder(C, { x: lx, y: rungY, face: [0, -1], z0: fz, zTop: s.loftZ, width: 0.45, rung: 0.24, rails: C.lofted ? 1.0 : 0.2 });
      C.solids0.push({ id: 'ladder', x0: lx - 0.26, x1: lx + 0.26, y0: rungY - 0.03, y1: rungY + 0.03 }); C.holes1.push(hatch);
      C.ladders.push({ id: 'loftLadder', x: lx, rungY, face: [0, -1], z0: fz, zTop: s.loftZ, rungs, hatch, bottom: [lx, rungY + 0.275], land: [lx, ya - 0.55] });
      if (C.lofted) { const xp = -(s.standHalf + 0.2), xs = xp + 0.5, py0 = yi0 + 0.25, py1 = yi1 - (s.loft === 'door' ? 1.3 : 0.45), zp = s.loftZ + 0.95;
        stick(out, [xp, py0, zp], [xp, py1, zp], 0.06, 'timber', { tag: 'store.netPole' }); for (const y of [py0 + 0.05, py1 - 0.05]) stick(out, [xp, y, s.loftZ], [xp, y, zp], 0.06, 'timber', { tag: 'store.netPole' });
        F(out, [[xp - 0.02, py0 + 0.15, zp], [xp - 0.02, py1 - 0.15, zp], [xp - 0.34, py1 - 0.2, s.loftZ + 0.04], [xp - 0.34, py0 + 0.2, s.loftZ + 0.04]], 'net', { tag: 'store.nets', tex: TEX.net, uv: [[0, 0], [py1 - py0, 0], [py1 - py0, 1], [0, 1]] });
        F(out, [[xp + 0.02, py0 + 0.35, zp], [xp + 0.02, py1 - 0.3, zp], [xp + 0.2, py1 - 0.35, s.loftZ + 0.35], [xp + 0.2, py0 + 0.4, s.loftZ + 0.35]], 'net', { tag: 'store.nets', tex: TEX.net, uv: [[0, 0], [py1 - py0, 0], [py1 - py0, 1], [0, 1]] });
        C.solids1.push({ id: 'netPole', x0: xp - 0.4, x1: xp + 0.22, y0: py0, y1: py1 });
        const ym = (Math.max(py0, hatch.y1 + 0.35) + py1) / 2;
        C.st.push({ id: 'netPole', verb: 'stowNet', clip: 'reach', also: ['takeNet'], fixture: 'rest', fixtureZ: 0.95, at: [xp, ym, zp], stand: [xs, ym], standZ: s.loftZ, level: 1, note: 'nets hang over a pole 0.95 above the loft floor; the stand is inside the 2 m headroom strip' }); } }
    if (s.stove) { const sx0 = xi1 - 0.62, sx1 = xi1 - 0.1, sy0 = yi0 + 0.2, sy1 = yi0 + 0.62, zt = fz + 0.62;
      box(out, sx0, sx1, sy0, sy1, fz + 0.12, zt, 'soot', { tag: 'store.stove' }); for (const [x, y] of [[sx0 + 0.04, sy0 + 0.04], [sx1 - 0.04, sy0 + 0.04], [sx0 + 0.04, sy1 - 0.04], [sx1 - 0.04, sy1 - 0.04]]) box(out, x - 0.025, x + 0.025, y - 0.025, y + 0.025, fz, fz + 0.12, 'soot', { tag: 'store.stove' });
      F(out, [[sx0 - 0.006, sy0 + 0.1, fz + 0.24], [sx0 - 0.006, sy1 - 0.1, fz + 0.24], [sx0 - 0.006, sy1 - 0.1, fz + 0.46], [sx0 - 0.006, sy0 + 0.1, fz + 0.46]], 'dark', { tag: 'store.stove', em: 'fire:stove', fire: true });
      const pTop = s.loftKind ? s.loftZ - 0.2 : (s.cut ? fz + 2.0 : s.eaveZ - 0.1); cyl(out, (sx0 + sx1) / 2 + 0.08, (sy0 + sy1) / 2, 0.07, zt, pTop, 8, 'soot', { tag: 'store.stove', noTop: true });
      C.solids0.push({ id: 'stove', x0: sx0 - 0.05, x1: xi1, y0: yi0, y1: sy1 + 0.05 }); C.stove = [r3((sx0 + sx1) / 2), r3((sy0 + sy1) / 2), r3(fz + 0.4)];
      C.st.push({ id: 'stove', verb: 'stokeStove', clip: 'reach', fixture: 'rest', fixtureZ: 0.35, at: [sx0, (sy0 + sy1) / 2, fz + 0.35], stand: [sx0 - 0.55, (sy0 + sy1) / 2], standZ: fz, level: 0, note: 'the fire door is on the stove\'s room side' }); }
    if (!s.dock) { const by0 = yi0 + (s.stove ? 0.85 : 0.4), bl = clamp(yi1 - by0 - 1.6, 1.0, 2.0), by1 = by0 + bl, bx0 = xi1 - 0.6, bz = fz + 0.8;
      bench(C, bx0, xi1, by0, by1, bz, fz); cylX(out, xi1 - 0.35, xi1 - 0.2, by0 + 0.3, bz + 0.07, 0.07, 8, 'rope', { tag: 'store.twine' }); box(out, bx0 + 0.02, bx0 + 0.14, by1 - 0.3, by1 - 0.16, bz, bz + 0.12, 'cp_iron', { tag: 'store.vise' });
      C.solids0.push({ id: 'bench', x0: bx0, x1: xi1, y0: by0, y1: by1 }); const ym = (by0 + by1) / 2;
      C.st.push({ id: 'bench', verb: 'mendNet', clip: 'bench', also: ['bait'], fixture: 'bench', fixtureZ: 0.8, at: [bx0 + 0.3, ym, bz], stand: [bx0 - 0.42, ym], standZ: fz, level: 0, note: 'mended standing: v9.2 has no seated clip (requests: sit)' }); }
    { const blocked = I.Hnx.filter(h => h.kind === 'win').map(h => [h.u0 - hl0(s) - 0.12, h.u1 - hl0(s) + 0.12]); let span = null;
      for (let y1 = yi1 - 0.3; y1 - 1.1 > yi0 + 1.4; y1 -= 0.1) { const y0 = y1 - 1.1; if (!blocked.some(b => b[1] > y0 && b[0] < y1)) { span = [y0, y1]; break; } }
      if (span) { const [y0, y1] = span, ym = (y0 + y1) / 2; box(out, xi0, xi0 + 0.05, y0, y1, fz + 1.55, fz + 1.63, 'trim', { tag: 'store.pegs' });
        box(out, xi0 + 0.02, xi0 + 0.16, y0 + 0.1, y0 + 0.5, fz + 0.82, fz + 1.58, 'buoyY', { tag: 'store.oilskins' }); box(out, xi0 + 0.02, xi0 + 0.15, y0 + 0.6, y0 + 0.98, fz + 0.9, fz + 1.58, 'buoyO', { tag: 'store.oilskins' });
        C.solids0.push({ id: 'pegs', x0: xi0, x1: xi0 + 0.18, y0, y1 });
        C.st.push({ id: 'pegs', verb: 'takeOilskins', clip: 'reach', fixture: 'rest', fixtureZ: 1.0, at: [xi0 + 0.1, ym, fz + 1.0], stand: [xi0 + 0.62, ym], standZ: fz, level: 0 }); } } }
  const hl0 = (s) => s.hl;
  function interiorStorage(C, I) { const s = C.s, out = C.out, fz = s.fH, { xi0, xi1, yi0, yi1 } = I;
    if (s.loftKind === 'loft') { const n = Math.ceil((s.loftZ - fz) / 0.19 - 1e-9), r = (s.loftZ - fz) / n, g = 0.26, sw = 0.95, sx = xi0 + sw / 2, yTop = yi0 + 1.0;
      let kNeed = 0; for (let k = 1; k < n; k++) if (k * r - 0.2 < 2.0) kNeed = k; const opening = { x0: xi0 - 0.02, x1: xi0 + sw + 0.05, y0: yTop, y1: yTop + Math.min(n - 1, kNeed) * g + 0.02 };
      const fl = flight(C, { top: [sx, yTop], dir: [0, 1], width: sw, n, zTop: s.loftZ, zBot: fz, going: g, mat: 'timber', solid: false, tex: TEX.boards, rail: [-1], railMat: 'timber', tag: 'climb.stair' });
      if (C.lofted) { slab(C, { x0: xi0, x1: xi1, y0: yi0, y1: yi1 }, [opening], s.loftZ, 'floor', TEX.planks, 'store.loft');
        rail(C, [opening.x1 + 0.03, opening.y0 + 0.05], [opening.x1 + 0.03, opening.y1], s.loftZ, { mat: 'timber', top: 0.95, tag: 'store.loftRail' }); }
      C.holes1.push(opening); C.solids0.push({ id: 'stair', x0: xi0, x1: xi0 + sw, y0: yTop, y1: yTop + fl.run });
      C.stairs.push(Object.assign({ id: 'loftStair', levels: [0, 1], landTop: [sx, yTop - 0.5], landBot: [sx, yTop + fl.run + 0.5], opening }, fl));
      if (C.lofted) { const ry0 = yi0 + 0.6, ry1 = Math.min(yi1 - 1.2, ry0 + 2.2), rx = xi1 - 0.25, zr = s.loftZ + 0.95;
        stick(out, [rx, ry0, zr], [rx, ry1, zr], 0.05, 'timber', { tag: 'store.buoyRack' }); for (const y of [ry0, ry1]) stick(out, [rx, y, s.loftZ], [rx, y, zr], 0.05, 'timber', { tag: 'store.buoyRack' });
        const cols = ['buoyR', 'buoyY', 'buoyW', 'buoyO']; for (let k = 0, y = ry0 + 0.25; y < ry1 - 0.15; y += 0.34, k++) { buoy(out, rx + 0.02, y, zr - 0.62, cols[k % 4]); }
        C.solids1.push({ id: 'buoyRack', x0: rx - 0.14, x1: xi1, y0: ry0, y1: ry1 }); const ym = (ry0 + ry1) / 2;
        C.st.push({ id: 'buoyRack', verb: 'stowBuoys', clip: 'reach', also: ['takeBuoy'], fixture: 'rest', fixtureZ: 0.95, at: [rx, ym, zr], stand: [rx - 0.62, ym], standZ: s.loftZ, level: 1 }); } }
    const tY1 = yi1 - 0.45, tY0 = tY1 - 1.9, tx0 = xi1 - 0.58; for (const [a, b] of [[tY0, tY0 + 0.92], [tY0 + 0.98, tY1]]) { trap(out, tx0, xi1 - 0.02, a, b, fz); trap(out, tx0, xi1 - 0.02, a + 0.02, b - 0.02, fz + 0.4); }
    C.solids0.push({ id: 'traps', x0: tx0, x1: xi1, y0: tY0, y1: tY1 }); const tm = (tY0 + tY1) / 2;
    C.st.push({ id: 'traps', verb: 'fetchTrap', clip: 'lift', also: ['place'], fixture: 'load', fixtureZ: 0.78, at: [xi1 - 0.3, tm, fz + 0.78], stand: [xi1 - 1.2, tm], standZ: fz, level: 0 });
    const fy0 = yi0 + 0.3, fy1 = yi0 + 1.6, fx0 = xi1 - 0.72; box(out, fx0, xi1 - 0.04, fy0, fy1, fz, fz + 0.85, 'cap', { tag: 'store.freezer' }); box(out, fx0 - 0.01, xi1 - 0.03, fy0 - 0.01, fy1 + 0.01, fz + 0.79, fz + 0.81, 'trim', { tag: 'store.freezer', b: -1 });
    box(out, fx0 - 0.03, fx0, (fy0 + fy1) / 2 - 0.12, (fy0 + fy1) / 2 + 0.12, fz + 0.7, fz + 0.76, 'steel', { tag: 'store.freezer' });
    C.solids0.push({ id: 'freezer', x0: fx0, x1: xi1, y0: fy0, y1: fy1 });
    C.st.push({ id: 'freezer', verb: 'fetchBait', clip: 'lift', also: ['place'], fixture: 'load', fixtureZ: 0.85, at: [fx0, (fy0 + fy1) / 2, fz + 0.85], stand: [fx0 - 0.55, (fy0 + fy1) / 2], standZ: fz, level: 0, note: 'a chest freezer of bait: lift the lid, lift the box out' });
    const wins = I.Hpx.filter(h => h.kind === 'win').map(h => [s.hl - h.u1 - 0.1, s.hl - h.u0 + 0.1]), ry0 = fy1 + 0.3, ry1 = tY0 - 0.3;
    if (ry1 - ry0 >= 0.8 && !wins.some(w => w[1] > ry0 && w[0] < ry1)) { const ym = (ry0 + ry1) / 2; box(out, xi1 - 0.05, xi1, ry0, ry1, fz + 1.4, fz + 1.48, 'trim', { tag: 'store.pegs' });
      for (let y = ry0 + 0.25; y < ry1 - 0.15; y += 0.4) cylX(out, xi1 - 0.2, xi1 - 0.02, y, fz + 1.18, 0.2, 10, 'rope', { tag: 'store.rope' });
      C.solids0.push({ id: 'rope', x0: xi1 - 0.22, x1: xi1, y0: ry0, y1: ry1 }); C.st.push({ id: 'rope', verb: 'takeRope', clip: 'reach', fixture: 'rest', fixtureZ: 1.0, at: [xi1 - 0.12, ym, fz + 1.0], stand: [xi1 - 0.64, ym], standZ: fz, level: 0 }); }
    const bx0 = xi0 + 0.95 + 0.4, bx1 = Math.min(bx0 + 1.7, fx0 - 0.3); if (bx1 - bx0 >= 1.0) { bench(C, bx0, bx1, yi0, yi0 + 0.6, fz + 0.8, fz); C.solids0.push({ id: 'bench', x0: bx0, x1: bx1, y0: yi0, y1: yi0 + 0.6 });
      C.st.push({ id: 'bench', verb: 'paintBuoys', clip: 'bench', also: ['mendTrap'], fixture: 'bench', fixtureZ: 0.8, at: [(bx0 + bx1) / 2, yi0 + 0.3, fz + 0.8], stand: [(bx0 + bx1) / 2, yi0 + 1.02], standZ: fz, level: 0 }); } }
  function interiorPlant(C, I) { const s = C.s, out = C.out, fz = s.fH, { xi0, xi1, yi0, yi1 } = I;
    if (s.loftKind === 'mezz') { const yM = yi0 + s.mezzD, n = Math.ceil((s.loftZ - fz) / 0.19 - 1e-9), g = 0.26, sw = 1.0, xs = -clamp(s.standHalf - 0.6, 0.6, s.hw - s.t - 0.6);
      const fl = flight(C, { top: [xs, yM], dir: [0, 1], width: sw, n, zTop: s.loftZ, zBot: fz, going: g, mat: 'steel', solid: false, tex: TEX.grate, rail: [-1, 1], tag: 'climb.stair' });
      if (C.lofted) { slab(C, { x0: xi0, x1: xi1, y0: yi0, y1: yM }, [], s.loftZ, 'steel', TEX.grate, 'store.mezz'); box(out, xi0, xi1, yM - 0.06, yM, s.loftZ - 0.26, s.loftZ, 'steel', { tag: 'store.mezz', b: -1 });
        rail(C, [xi0 + 0.05, yM - 0.03], [xs - sw / 2 - 0.02, yM - 0.03], s.loftZ, { tag: 'store.mezzRail' }); rail(C, [xs + sw / 2 + 0.02, yM - 0.03], [xi1 - 0.05, yM - 0.03], s.loftZ, { tag: 'store.mezzRail' });
        const dx0 = -0.6, dx1 = 1.0; bench(C, dx0, dx1, yi0, yi0 + 0.7, s.loftZ + 0.8, s.loftZ, 'timber'); box(out, dx0 + 0.2, dx0 + 0.55, yi0 + 0.1, yi0 + 0.35, s.loftZ + 0.8, s.loftZ + 0.83, 'cap', { tag: 'store.ledger' });
        box(out, 1.25, 1.75, yi0, yi0 + 0.55, s.loftZ, s.loftZ + 1.3, 'steel', { tag: 'store.cabinet' }); for (let k = 1; k < 4; k++) box(out, 1.27, 1.73, yi0 + 0.55, yi0 + 0.565, s.loftZ + k * 0.32, s.loftZ + k * 0.32 + 0.03, 'dark', { tag: 'store.cabinet' });
        C.solids1.push({ id: 'desk', x0: dx0, x1: dx1, y0: yi0, y1: yi0 + 0.7 }, { id: 'cabinet', x0: 1.25, x1: 1.75, y0: yi0, y1: yi0 + 0.55 });
        C.st.push({ id: 'desk', verb: 'tally', clip: 'bench', fixture: 'bench', fixtureZ: 0.8, at: [0.2, yi0 + 0.35, s.loftZ + 0.8], stand: [0.2, yi0 + 1.12], standZ: s.loftZ, level: 1, note: 'the tally desk over the floor; standing, as v9.2 has no seated clip' },
          { id: 'cabinet', verb: 'fileTally', clip: 'reach', fixture: 'rest', fixtureZ: 0.95, at: [1.5, yi0 + 0.56, s.loftZ + 0.95], stand: [1.5, yi0 + 1.08], standZ: s.loftZ, level: 1 }); }
      for (const x of [-s.hw * 0.35, s.hw * 0.35]) if (Math.abs(x - xs) > sw / 2 + 0.2) { stick(out, [x, yM - 0.1, fz], [x, yM - 0.1, s.loftZ - 0.26], 0.14, 'steel', { tag: 'store.mezzColumn' }); C.solids0.push({ id: 'column', x0: x - 0.08, x1: x + 0.08, y0: yM - 0.18, y1: yM - 0.02 }); }
      C.solids0.push({ id: 'stair', x0: xs - sw / 2, x1: xs + sw / 2, y0: yM, y1: yM + fl.run });
      C.stairs.push(Object.assign({ id: 'mezzStair', levels: [0, 1], landTop: [xs, yM - 0.55], landBot: [xs, yM + fl.run + 0.55] }, fl)); C.mezzY = yM; }
    const yM = C.mezzY != null ? C.mezzY : yi0 + 1.0, xT = 1.05, yT0 = yM + 0.9, yT1 = yi1 - 2.6, nT = Math.max(1, Math.floor((yT1 - yT0 + 0.6) / 2.4)), tl = Math.min(1.8, (yT1 - yT0 - (nT - 1) * 0.6) / nT);
    if (tl >= 1.0) for (let k = 0; k < nT; k++) { const y0 = yT0 + k * (tl + 0.6), y1 = y0 + tl, yc = (y0 + y1) / 2; bench(C, xT - 0.45, xT + 0.45, y0, y1, fz + 0.72, fz, 'galv');
      box(out, xT - 0.3, xT + 0.3, yc - 0.4, yc + 0.1, fz + 0.72, fz + 0.76, 'ice', { tag: 'store.catch', tex: TEX.ice }); C.solids0.push({ id: 'table' + k, x0: xT - 0.45, x1: xT + 0.45, y0, y1 });
      C.st.push({ id: 'table' + k + 'W', verb: 'fillet', clip: 'chop', fixture: 'knife', fixtureZ: 0.72, at: [xT - 0.25, yc, fz + 0.72], stand: [xT - 0.88, yc], standZ: fz, level: 0 },
        { id: 'table' + k + 'E', verb: 'fillet', clip: 'chop', fixture: 'knife', fixtureZ: 0.72, at: [xT + 0.25, yc, fz + 0.72], stand: [xT + 0.88, yc], standZ: fz, level: 0 }); }
    const ty0 = yi1 - 1.8, top = boxStack(C, xi0 + 0.1, xi0 + 1.1, ty0, ty0 + 0.8, fz, 3, 'box'); C.solids0.push({ id: 'boxes', x0: xi0 + 0.1, x1: xi0 + 1.1, y0: ty0, y1: ty0 + 0.8 });
    C.st.push({ id: 'boxes', verb: 'stackBox', clip: 'place', also: ['lift'], fixture: 'load', fixtureZ: r3(top - fz), at: [xi0 + 1.1, ty0 + 0.4, top], stand: [xi0 + 1.62, ty0 + 0.4], standZ: fz, level: 0, note: 'fish boxes, three on a pallet; carry one as the tray (CARRIES.tray)' });
    const bx1 = xi0 + 1.5, by1 = yi0 + 1.3; box(out, xi0, bx1, yi0, yi0 + 0.05, fz, fz + 0.45, 'timber', { tag: 'store.iceBin' }); box(out, bx1 - 0.05, bx1, yi0, by1, fz, fz + 0.45, 'timber', { tag: 'store.iceBin' }); box(out, xi0, bx1, by1 - 0.05, by1, fz, fz + 0.3, 'timber', { tag: 'store.iceBin' });
    F(out, [[xi0, yi0 + 0.05, fz + 0.9], [bx1 - 0.05, yi0 + 0.05, fz + 0.75], [bx1 - 0.05, by1 - 0.05, fz + 0.3], [xi0, by1 - 0.05, fz + 0.36]], 'ice', { tag: 'store.ice', tex: TEX.ice, uv: [[0, 0], [1.5, 0], [1.5, 1.3], [0, 1.3]] });
    C.solids0.push({ id: 'iceBin', x0: xi0, x1: bx1, y0: yi0, y1: by1 });
    C.st.push({ id: 'iceBin', verb: 'shovelIce', clip: 'dig', fixture: null, at: [(xi0 + bx1) / 2, by1 - 0.3, fz + 0.3], stand: [(xi0 + bx1) / 2, by1 + 0.55], standZ: fz, level: 0, note: 'flake ice behind a 0.3 m board, so the dig clip works at the floor' });
    const sx0 = 0.25, sx1 = 1.85; bench(C, sx0, sx1, yi0, yi0 + 0.7, fz + 0.85, fz, 'galv'); box(out, sx0 + 0.3, sx0 + 0.8, yi0 + 0.1, yi0 + 0.6, fz + 0.85, fz + 0.9, 'steel', { tag: 'store.scale' }); box(out, sx0 + 0.52, sx0 + 0.58, yi0 + 0.1, yi0 + 0.16, fz + 0.9, fz + 1.3, 'steel', { tag: 'store.scale' }); cylX(out, sx0 + 0.5, sx0 + 0.6, yi0 + 0.13, fz + 1.38, 0.1, 10, 'cap', { tag: 'store.scale' });
    C.solids0.push({ id: 'scale', x0: sx0, x1: sx1, y0: yi0, y1: yi0 + 0.7 });
    C.st.push({ id: 'scale', verb: 'weigh', clip: 'place', also: ['lift'], fixture: 'load', fixtureZ: 0.85, at: [sx0 + 0.55, yi0 + 0.35, fz + 0.85], stand: [sx0 + 0.55, yi0 + 1.12], standZ: fz, level: 0 });
    const hx = xi1 - 0.7; if (hx > sx1 + 0.5) { cylX(out, hx - 0.12, hx + 0.12, yi0 + 0.14, fz + 1.1, 0.24, 10, 'buoyY', { tag: 'store.hose' }); box(out, hx - 0.03, hx + 0.03, yi0, yi0 + 0.14, fz + 1.05, fz + 1.15, 'steel', { tag: 'store.hose' });
      C.solids0.push({ id: 'hose', x0: hx - 0.25, x1: hx + 0.25, y0: yi0, y1: yi0 + 0.38 }); C.st.push({ id: 'hose', verb: 'hoseDown', clip: 'reach', fixture: 'rest', fixtureZ: 1.0, at: [hx, yi0 + 0.3, fz + 1.0], stand: [hx, yi0 + 0.9], standZ: fz, level: 0 }); } }

  // ---- assembly -----------------------------------------------------------------------------------------------------------
  function assemble(o, dir, lc) { const s = resolve(o), C = { s, out: [], walls0: [], walls1: [], segs0: [], solids0: [], solids1: [], holes1: [], ladders: [], stairs: [], st: [], lamps: [], stacks: [], foot: [], dir: s.cut ? (dir | 0) : null, lc: !!lc, pseed: 0 };
    build(C); C.foot.unshift({ id: 'building', x0: -s.hw - 0.05, x1: s.hw + 0.05, y0: -s.hl - 0.05, y1: s.hl + 0.05, walk: false });
    return C; }

  root.WharfBuildingGeo2 = { BODY, TRIM, PAINT, ROOFS, RAMP, WPROP, TEX, ROOFTEX, WSTYLE, TYPES, PRESETS, SHAPES, SIDINGS, DOORS, ROOF_KEYS, CUPOLAS, WINDOWS, PLINTHS,
    resolve, mats, assemble, gableTop, hsh, clamp, r3, mix };
})(typeof globalThis !== 'undefined' ? globalThis : window);
