/* Hidden Harbours — ISO MOTOR YACHT 84, PASS C: her exterior and its finishes, her helm, her rooms, doors and cut
   (ADR-0006 bake recipe).
   25.50 m LOA (the swim platform's aft edge to the stem's head, the garage's door down), 24.42 m of hull at the deck,
   6.14 m beam, DWL 1.80 over the keel's bottom: a planing motor yacht with a raked bow and a long, low sheer, hard
   chines over a vee bottom, a keel on the centreline, a transom raked top aft with the garage's mouth over the swim
   platform, one low house with a dark band of glazing all round, a hardtop over the aft deck on two raked blades, a
   windscreen from the hardtop's front down to the forward coachroof, and a plain mast on the hardtop.

   Same camera, rasteriser and conventions as the fleet; the machinery (colour, rasteriser, the outline and ring-loft
   helpers, the mesh-true half-breadth, the box, cylinder, dome and hull-patch helpers) is copied from
   expeditionYacht120IsoRig.js. 32 px = 1 m, elev 40 default, 8 facings at 45°, flat facets, ordered dither,
   depth-edge darkening, no AA, and no outline ring (ADR 0031: KEYLINE_DEFAULT is false, render(dir, { outline: true })
   is the A/B). Frame +x stbd, +y bow, +z up; origin amidships (the middle of loa), keel bottom, centreline; the pivot
   is that origin, pinned at every facing.

   Her lines are pass A's reading (docs/art/rigs/yacht-84/PLAN.md, A3) and stay (rule 9); nothing below her waterline
   is read. Pass B lays her detail on them (PLAN.md, B1), each piece a plain shape that earns its pixel at ×3 (rule 7),
   and pass C brings its finishes and fittings closer to the owner's pictures (PLAN.md, C2):
     the house    the glazing band and its few mullions, the windscreen's post, the coachroof's teak and its sunpads
     the hardtop  its two blades, flared at the foot and the head; its rounded rim and its underside in warm wood; a
                  sun deck on its top (decision 11), teak inside a white rim, its rail, two loungers and a table,
                  reached by a ladder from the aft deck through a hatch; a dome ahead of the mast
     the mast     its crossbar and lights, the radar's bar, the masthead light and a whip
     the decks    teak as a ramp, not lines, on the aft deck, the side decks, the foredeck and the swim platform; the
                  bulwark's rail, few stanchions, a firm top rail and a lower rail; the aft deck's two tables, their
                  four armchairs and its hatch; the foredeck's hatch, the crew's way in (decision 8)
     the stern    the garage's mouth, its back wall in warm wood lit by three washes, and, to starboard of it, the
                  steps from the platform up to the aft deck; a small port in each of the transom's quarters; the
                  swim platform is the garage's door (spec §4.4), down by default (decision 2) and shut up in the
                  transom's plane; the swim ladder hangs from its aft edge; the flagstaff, with no flag
     the bow      the windlass, the anchor in its pocket, the bow roller, the cleats and the fairleads
     the hull     her rows of ports (four a side are grilles), the boot line and the rubbing strake
     the gear     two shafts, struts, propellers and rudders, at modest detail
   The paints (decision 7): white, the default, and the 120's navy and champagne; a MATS entry for each surface that
   can be recoloured on its own (rule 6). The wear is light (decision 10): none is drawn.

   Her states: garage 0..1, the door, 0 shut and 1 down (the default); ladder true|false, the swim ladder hung (with
   the door down only) or stowed (the default). faceList(state) and partFaces(id, state) take them; a bad value throws.
   render's doorOpen (or doors) 0..1, a number or { leaf id: t }, poses the leaves (doorFaces(t)); shut by default, open
   under a cutaway.

   No buried faces (charter §1): below LOWER_SOLE nothing is hidden from the sea. Each solid of the running gear ends
   at the skin it meets, and none has a face pointing straight up inside it: the rings put their corners at k·45°
   about the axis, the ladder's rungs have no bottoms, and the blades are single faces lit from either side.

   The tags are ADR 0048's (hull-layout-1 §4.2-4.3). Every face carries lv, ro and it; it is false on her exterior and
   on her leaves, and true on her room (interiorFaces(), PLAN.md C4), which keeps inside her shell (fitCheck).
     hull       the shell to the sheer, the transom and its ports, the ports and grilles, the strake, the anchor's
                pocket, the fairleads; the running gear (hull and part, as the spec tags the drives)
     main_deck  the swim platform (the door) and its ladder, the steps, the aft and side decks, the bulwark's lining, cap
                and rail, the hardtop's blades, the aft deck's tables, chairs and hatch, the sun deck's ladder, the
                cleats aft
     house      the house's walls, glazing and mullions, the windscreen and its post, the coachroof and its pads; its
                roof is the hardtop's underside. Its room: the saloon, the galley, the dining and the helm (the dining
                and the helm 0.60 lower, decision 13), the saloon's aft doors
     below      the garage's reveal behind its mouth: its sole, head (its roof, for the cut), sides and back, and its
                inner door. Its room: the master cabin and its bath, the guest and VIP cabins and their heads, the
                corridor and the spiral stair, the engine room (shut, decision 5) and the tank room, the crew's mess,
                head and cabin (decision 8) and the forepeak
     foredeck   the foredeck, its step, lining, cap and rail, its hatch, the windlass, the roller, the anchor, its cleats
     top        the sun deck, its rim, rail, loungers and table, the dome, the flagstaff
     rigging    the mast and its fittings

   Her decks: MAIN_DECK (cockpitZ) 3.75, the house's sole the same; GARAGE_SOLE and PLATFORM_Z 2.15, 0.35 over the DWL
   (charter §1: no open sole under her waterline); LOWER_SOLE 1.50, her lowest sole, its own constant for the intake's
   CabinSoleHulls row, and the lowest face of her room. Pass C draws the rooms, the doors and the cut (PLAN.md, C4
   and C5); the walk is pass E's. The cut's keys (CUT_KEYS): deck, house and below; renderCut(dir, key) draws it.

   Exposes globalThis.MotorYacht84Iso = { id, label, kind, hullIds, defaultHull, byId(id), loa, loaText, loh, L, DWL,
     beam, draft, cockpitZ, MAIN_DECK, HOUSE_SOLE, LOWER_SOLE, GARAGE_SOLE, PLATFORM_Z, W, H, PX, DIRS, pivot, order,
     defaultElev, PAINTS, paintRamps, schemeIds, defaultScheme, matsFor(scheme), MATS, GAIN, BIAS, LN, BAYER, KEY, EDGE,
     KEYLINE_DEFAULT, cell, LV, RO, GHOST_ROLES, LEVEL_IDS, ROLES, FRAME, loft, states, faceList(state),
     partFaces(id, state), faceCount, DOOR, LADDER, STEPS, CLEATS, GEAR, render(dir, opts), ROCK, rock(i),
     project(p, dir, opts), DINING_SOLE, interiorFaces(), interiorFaceCount, fitCheck(tol), doorFaces(t),
     leafTimes(opts), renderCut(dir, key, opts), CUT_KEYS, HELM, WHEEL_GEO, ROOMS, BEDS, IDOORS, IFLIGHTS, STAIR,
     geometry(), DOORS, DOOR_SET, partsList(), WALK, CUTAWAY, GHOST, liftOf(mode, key), walk() }. render's opts carry
     the state and the scheme; render(dir, { channel: 'levels' }) paints each level flat, and { channel: 'parts' } each
     part flat, for the profile check. walk() is her walk (pass E): hull-layout-2's writer writes the walk from it, never
     by hand.
     Plain script, no DOM. */
(function (root) {
  const PX = 32, S = 32, DEG = Math.PI / 180, DEFAULT_ELEV = 40;
  const KEYLINE_DEFAULT = false;                        // ADR 0031: no outline ring; render(dir, { outline: true }) is the A/B

  // ================================= COLOUR =================================
  function oklchHex(Lp, C, h) {
    const hr = h * DEG, a = C * Math.cos(hr), b = C * Math.sin(hr);
    const l_ = Lp + 0.3963377774 * a + 0.2158037573 * b, m_ = Lp - 0.1055613458 * a - 0.0638541728 * b, s_ = Lp - 0.0894841775 * a - 1.2914855480 * b;
    const l = l_ * l_ * l_, m = m_ * m_ * m_, s = s_ * s_ * s_;
    const r = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s, g = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s, bb = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;
    const enc = (u) => { u = u <= 0.0031308 ? 12.92 * u : 1.055 * Math.pow(Math.max(u, 0), 1 / 2.4) - 0.055; const n = Math.round(Math.max(0, Math.min(1, u)) * 255); return (n < 16 ? '0' : '') + n.toString(16); };
    return '#' + enc(r) + enc(g) + enc(bb);
  }
  function mkRamp(n, L0, L1, C, h) { const o = []; for (let i = 0; i < n; i++) { const t = n === 1 ? 0 : i / (n - 1); o.push(oklchHex(L0 + (L1 - L0) * t, C * (1 - 0.12 * t), h)); } return o; }
  // the fleet's ramps, as the 120 has them
  const HULLW = ['#7f878c', '#9ea6a9', '#bcc3c2', '#d5dbd7', '#e9ede7', '#f4f7f1', '#fcfdf8'];
  const BOOT = ['#090c11', '#0f131a', '#161c25', '#202734', '#2b3341'];
  const CREAM = ['#8a9297', '#a7afb2', '#c4cbca', '#dbe0dc', '#ecefe9', '#f6f8f2', '#fdfef9'];
  const GLAS = ['#05090c', '#0a1218', '#101a22', '#17242e', '#20323f'];
  const STEEL = ['#3a4148', '#565f66', '#7a858c', '#9fabb1', '#c3ced2', '#e6edee'];
  const IRON = ['#0d1013', '#161a20', '#222931', '#323b45'];
  const BLUE = ['#0d2a4e', '#17457a', '#245f9c', '#3878ba', '#5391cf'];
  const TEAK = ['#3f2814', '#54351d', '#6c4626', '#855a31', '#9c6e40', '#b28553'];
  // the rooms' own (C4), on the numbers read off the saloon's and the master cabin's pictures: a warm red-brown joinery,
  // a pale warm linen (the upholstery, the carpets, the lower walls), a light oak (the soles), a blue-grey (the saloon's
  // rug), a dark navy (its cabinet, the master's nightstands), and a pale daylight glass, her glazing seen from inside
  // (the 120's)
  const WOODR = ['#34190c', '#4c2816', '#6f3c22', '#974e25', '#b25f2e', '#c86f3a'];
  const LINEN = ['#6e6050', '#8a7a69', '#a69683', '#c1b19d', '#d8cab6', '#eadfcf', '#f7efe4'];
  const OAK = ['#6b5440', '#876b52', '#a38566', '#bd9e7c', '#d3b694', '#e4cbab'];
  const SLATE = ['#2c3236', '#3b4247', '#4c5459', '#5e676b', '#727b7e', '#868f91'];
  const NAVYR = ['#0b1124', '#111a36', '#18244b', '#213062', '#2c3d7a'];
  const GLASSD = ['#7d9ea6', '#a1c2c6', '#c2dcdd', '#d8ebec', '#e8f4f4', '#f4fafa'];
  const KEY = '#0d1418';
  const TEAK_B = -0.5;                                  // the teak's shade on a deck: index 4.9 of its 0..5 at elev 40
  // decision 7: white, the default, and the 120's navy and champagne, its numbers
  const PAINTS = [
    { id: 'white', label: 'WHITE', note: 'White topsides and house, a thin dark boot line, a dark bottom.', literal: { top: HULLW, boot: BOOT, stripe: BOOT, house: CREAM }, mainHouse: 'house' },
    { id: 'navy', label: 'NAVY', note: 'Navy topsides, a white house, a cream boot line.', top: [0.22, 0.50, 0.086, 264], boot: [0.14, 0.27, 0.042, 264], stripe: [0.70, 0.95, 0.030, 85], literal: { house: CREAM }, mainHouse: 'house' },
    { id: 'champagne', label: 'CHAMPAGNE', note: 'Champagne topsides and house walls, white upper works, a blue boot line, a dark bottom.', top: [0.60, 0.94, 0.028, 80], literal: { boot: BOOT, stripe: BLUE, house: CREAM }, mainHouse: 'top' },
  ];
  const DEFAULT = 'white', PAINT_BY = {}; PAINTS.forEach(p => { PAINT_BY[p.id] = p; });
  const _rc = {};
  function paintRamps(id) {
    const p = PAINT_BY[id] || PAINT_BY[DEFAULT]; if (_rc[p.id]) return _rc[p.id];
    const lit = p.literal || {}, R = {};
    R.top = lit.top || mkRamp(7, p.top[0], p.top[1], p.top[2], p.top[3]);
    R.boot = lit.boot || mkRamp(5, p.boot[0], p.boot[1], p.boot[2], p.boot[3]);
    R.stripe = lit.stripe || mkRamp(5, p.stripe[0], p.stripe[1], p.stripe[2], p.stripe[3]);
    R.house = lit.house || CREAM; R.mainHouse = p.mainHouse === 'top' ? R.top : R.house;
    return (_rc[p.id] = R);
  }
  const LVRAMP = { hull: mkRamp(5, 0.36, 0.74, 0.06, 250), main_deck: mkRamp(5, 0.40, 0.80, 0.11, 55), house: mkRamp(5, 0.40, 0.80, 0.14, 20), bridge: mkRamp(5, 0.40, 0.80, 0.11, 145),
    below: mkRamp(5, 0.36, 0.74, 0.10, 330), rigging: mkRamp(5, 0.40, 0.80, 0.06, 200), foredeck: mkRamp(5, 0.40, 0.82, 0.11, 95), top: mkRamp(5, 0.40, 0.80, 0.11, 300) };
  // the parts channel: one flat colour a part, read back by the profile check
  const PTCOL = { hull: '#e6194b', deck: '#3cb44b', lining: '#2e7d32', platform: '#9e9e9e', house: '#4363d8', glass: '#42d4f4', wing: '#f032e6', hardtop: '#ffe119', pillar: '#911eb4', mast: '#f58231',
    rail: '#808000', fit: '#9a6324', gear: '#000075', stair: '#aaffc3', garage: '#800000', ladder: '#fabed4', mullion: '#469990', port: '#dcbeff', strake: '#fffac8',
    room: '#bfef45', wheel: '#a9a9a9', door: '#ffd8b1' };
  // matsFor(scheme): the paint table (hull-layout spec §5) { scheme, MATS, RINDEX, ramps, M, roomMats }. scheme is a PAINTS id,
  // or a state { scheme | paint }; none, or an unknown id, is 'white'. Every paint dithers (dith 1). ramps are MATS' ramps
  // deduplicated by content and M indexes them; roomMats names the rooms' own finishes. Each surface that can be
  // recoloured on its own has its own entry (rule 6): the strake, the teak, the pads and the gear among them, and the
  // warm wood of her joinery (C1: the hardtop's underside, the garage's back wall), on the teak's ramp. The rooms' (C3,
  // C4): woodr their warm red-brown joinery, dash the helm's dark dash (the teak's ramp, three steps down), glasr their
  // dark glass (the helm's screens, the wardrobes' doors), metal their steel, linen their pale upholstery, carpets and
  // lower walls, oak their soles, slate the saloon's rug, navyr their navy pieces, glassd her glazing seen from inside,
  // and whiter their whites (the pads' cream). Every ramp is two colours or more, the parts channel's flat ones doubled
  const _mc = {}, ROOM_MATS = ['woodr', 'dash', 'glasr', 'metal', 'linen', 'oak', 'slate', 'navyr', 'glassd', 'whiter'];
  function matsFor(id) {
    if (id && typeof id === 'object') id = id.scheme != null ? id.scheme : id.paint;
    const scheme = PAINT_BY[id] ? id : DEFAULT; if (_mc[scheme]) return _mc[scheme];
    const R = paintRamps(scheme);
    const MATS = { hull: { ramp: R.top, off: 0 }, boot: { ramp: R.boot, off: 0 }, stripe: { ramp: R.stripe, off: 0 }, strake: { ramp: STEEL, off: 0 }, cream: { ramp: R.house, off: 0 },
      mainhouse: { ramp: R.mainHouse, off: 0 }, teak: { ramp: TEAK, off: 0 }, wood: { ramp: TEAK, off: 0 }, pad: { ramp: CREAM, off: 0 }, glas: { ramp: GLAS, off: 0 }, steel: { ramp: STEEL, off: 0 }, iron: { ramp: IRON, off: 0 },
      gear: { ramp: STEEL, off: -1 }, blk: { ramp: R.boot, off: -1 },
      woodr: { ramp: WOODR, off: 0 }, dash: { ramp: TEAK, off: -3 }, glasr: { ramp: GLAS, off: 0 }, metal: { ramp: STEEL, off: 0 },
      linen: { ramp: LINEN, off: 0 }, oak: { ramp: OAK, off: 0 }, slate: { ramp: SLATE, off: 0 }, navyr: { ramp: NAVYR, off: 0 }, glassd: { ramp: GLASSD, off: 0 }, whiter: { ramp: CREAM, off: 0 } };
    for (const k in LVRAMP) MATS['lv_' + k] = { ramp: LVRAMP[k], off: 0 };
    for (const k in PTCOL) MATS['pt_' + k] = { ramp: [PTCOL[k], PTCOL[k]], off: 0 };
    for (const k in MATS) MATS[k].dith = 1;
    const RINDEX = {}; [R.top, R.boot, R.stripe, R.house, TEAK, CREAM, GLAS, STEEL, IRON, WOODR, LINEN, OAK, SLATE, NAVYR, GLASSD].concat(Object.values(LVRAMP)).forEach(r => r.forEach((c, i) => { RINDEX[c] = { r, i }; }));
    const ramps = [], at = {}, M = {};
    for (const k in MATS) { const r = MATS[k].ramp, s = r.join(','); if (at[s] == null) { at[s] = ramps.length; ramps.push(r.slice()); } M[k] = { ramp: at[s], off: MATS[k].off, dith: MATS[k].dith }; }
    return (_mc[scheme] = { scheme, MATS, RINDEX, ramps, M, roomMats: ROOM_MATS.slice() });
  }

  // ================================= RASTERISER (the fleet's locked recipe) =================================
  const GAIN = 3.0, BIAS = 2.7, EDGE = 0.30;             // EDGE: the depth step (m) whose far side the depth edge darkens
  const LN = (() => { const v = [-0.42, 0.72, 0.52]; const m = Math.hypot(...v); return v.map(c => c / m); })();
  const BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]].map(r => r.map(v => (v + 0.5) / 16));
  function normal(a, b, c) { const ux = b.xr - a.xr, uy = b.yr - a.yr, uz = b.zr - a.zr, vx = c.xr - a.xr, vy = c.yr - a.yr, vz = c.zr - a.zr;
    let nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx; const m = Math.hypot(nx, ny, nz) || 1; return [nx / m, ny / m, nz / m]; }
  function shadeOf(n, se, ce) { return n[0] * LN[0] + (n[1] * se + n[2] * ce) * LN[1] + (-n[1] * ce + n[2] * se) * LN[2]; }
  function camBasis(o) { const th = (o.dir || 0) * Math.PI / 4, e = (o.elev != null ? o.elev : DEFAULT_ELEV) * DEG, roll = (o.roll || 0) * DEG, pitch = (o.pitch || 0) * DEG;
    return { ct: Math.cos(th), stt: Math.sin(th), se: Math.sin(e), ce: Math.cos(e), cr: Math.cos(roll), sr: Math.sin(roll), cq: Math.cos(pitch), sq: Math.sin(pitch), heave: (o.heave || 0) }; }
  function projVert(x, y, z, B, G) { const x1 = x * B.cr + z * B.sr, z1 = -x * B.sr + z * B.cr, y2 = y * B.cq - z1 * B.sq, z2 = y * B.sq + z1 * B.cq;
    const xr = x1 * B.ct - y2 * B.stt, yr = x1 * B.stt + y2 * B.ct, zr = z2; return { xr, yr, zr, sx: G.cx + xr * S, sy: G.cy - (yr * B.se + zr * B.ce) * S - B.heave, d: (yr * B.ce - zr * B.se) }; }
  function _paint(faces, opts, G, MAT) {
    const PW = G.W, PH = G.H, MATS = MAT.MATS, RINDEX = MAT.RINDEX, B = camBasis(opts);
    const zbuf = new Float32Array(PW * PH).fill(Infinity), col = new Array(PW * PH).fill(null), dep = new Float32Array(PW * PH);
    const GP = opts._ghost || null;                               // a cutaway's ghost round the player, on the ghost roles
    for (const f of faces) {
      const rv = f.v.map(([x, y, z]) => projVert(x, y, z, B, G)), g = GP && GHOST_RO.has(f.ro) ? GP : null;
      const n = normal(rv[0], rv[1], rv[2]);
      let sh = shadeOf(n, B.se, B.ce); if (sh < 0 && ((f.b || 0) <= -1)) sh = shadeOf([-n[0], -n[1], -n[2]], B.se, B.ce) * 0.9;
      const fidx = sh * GAIN + BIAS + (f.b || 0), M = MATS[f.mat] || MATS.hull;
      for (let t = 1; t + 1 < rv.length; t++) fillTri(rv[0], rv[t], rv[t + 1]);
      function fillTri(a, b, c) {
        const minX = Math.max(0, Math.floor(Math.min(a.sx, b.sx, c.sx))), maxX = Math.min(PW - 1, Math.ceil(Math.max(a.sx, b.sx, c.sx)));
        const minY = Math.max(0, Math.floor(Math.min(a.sy, b.sy, c.sy))), maxY = Math.min(PH - 1, Math.ceil(Math.max(a.sy, b.sy, c.sy)));
        const area = (b.sx - a.sx) * (c.sy - a.sy) - (c.sx - a.sx) * (b.sy - a.sy); if (Math.abs(area) < 1e-6) return;
        for (let y = minY; y <= maxY; y++) for (let x = minX; x <= maxX; x++) {
          const px = x + 0.5, py = y + 0.5;
          const w0 = ((b.sx - px) * (c.sy - py) - (c.sx - px) * (b.sy - py)) / area, w1 = ((c.sx - px) * (a.sy - py) - (a.sx - px) * (c.sy - py)) / area, w2 = 1 - w0 - w1;
          if (w0 < -0.001 || w1 < -0.001 || w2 < -0.001) continue;
          const d = w0 * a.d + w1 * b.d + w2 * c.d, deff = d - (f.db || 0), i = y * PW + x;
          if (g && d < g.dP) { const ex = (px - g.cx) * g.irx, ey = (py - g.cy) * g.iry, e = ex * ex + ey * ey; if (e < 1) { const r = Math.sqrt(e); if (r < g.core || (r - g.core) * g.icore < BAYER[x & 3][y & 3]) continue; } }
          if (deff < zbuf[i]) { zbuf[i] = deff; dep[i] = d; const base = Math.floor(fidx); const idx = base + ((fidx - base) > BAYER[x & 3][y & 3] ? 1 : 0) + M.off;
            col[i] = M.ramp[Math.max(0, Math.min(M.ramp.length - 1, idx))]; }
        }
      }
    }
    const out = col.slice();
    for (let y = 0; y < PH; y++) for (let x = 0; x < PW; x++) { const i = y * PW + x; if (!col[i]) continue;
      for (const [dx, dy] of [[1, 0], [0, 1]]) { const nx = x + dx, ny = y + dy; if (nx >= PW || ny >= PH) continue; const j = ny * PW + nx; if (!col[j]) continue;
        if (Math.abs(dep[i] - dep[j]) > EDGE) { const far = dep[i] > dep[j] ? i : j, e = RINDEX[col[far]]; if (e && e.i > 0) out[far] = e.r[Math.max(0, e.i - 2)]; } } }
    if (opts.outline != null ? !!opts.outline : KEYLINE_DEFAULT)   // the retired outline ring, kept as the A/B
      for (let y = 0; y < PH; y++) for (let x = 0; x < PW; x++) { const i = y * PW + x; if (out[i]) continue; let touch = false;
        for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) { const nx = x + dx, ny = y + dy; if (nx >= 0 && nx < PW && ny >= 0 && ny < PH && col[ny * PW + nx]) { touch = true; break; } }
        if (touch) { out[i] = KEY; dep[i] = Infinity; } }
    out.dep = dep; return out;
  }
  function _toRGBA(out, PW, PH) { const rgba = new Uint8ClampedArray(PW * PH * 4); if (out.dep) rgba.dep = out.dep;
    for (let i = 0; i < PW * PH; i++) { const c = out[i]; if (!c) { rgba[i * 4 + 3] = 0; continue; }
      rgba[i * 4] = parseInt(c.slice(1, 3), 16); rgba[i * 4 + 1] = parseInt(c.slice(3, 5), 16); rgba[i * 4 + 2] = parseInt(c.slice(5, 7), 16); rgba[i * 4 + 3] = 255; }
    return rgba; }

  // ================================= MATH =================================
  const lerp = (a, b, t) => a + (b - a) * t, clamp = (v, a, b) => v < a ? a : (v > b ? b : v), clamp01 = (t) => clamp(t, 0, 1);
  const v_sub = (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]], v_add = (a, b) => [a[0] + b[0], a[1] + b[1], a[2] + b[2]], v_mul = (a, s) => [a[0] * s, a[1] * s, a[2] * s];
  const v_norm = (a) => { const m = Math.hypot(a[0], a[1], a[2]) || 1; return [a[0] / m, a[1] / m, a[2] / m]; }, v_cross = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
  const r3 = (v) => +(+v).toFixed(3), smooth = (t) => t * t * (3 - 2 * t);
  function mono(pts) {
    pts = pts.slice().sort((a, b) => a[0] - b[0]); const n = pts.length, xs = pts.map(p => p[0]), ys = pts.map(p => p[1]); if (n === 1) return () => ys[0];
    const d = []; for (let i = 0; i < n - 1; i++) d.push((ys[i + 1] - ys[i]) / (xs[i + 1] - xs[i]));
    const m = [d[0]]; for (let i = 1; i < n - 1; i++) { if (d[i - 1] * d[i] <= 0) m.push(0); else { const h0 = xs[i] - xs[i - 1], h1 = xs[i + 1] - xs[i], w1 = 2 * h1 + h0, w2 = h1 + 2 * h0; m.push((w1 + w2) / (w1 / d[i - 1] + w2 / d[i])); } } m.push(d[n - 2]);
    return function (x) { if (x <= xs[0]) return ys[0] + m[0] * (x - xs[0]); if (x >= xs[n - 1]) return ys[n - 1] + m[n - 1] * (x - xs[n - 1]);
      let i = 0; while (i < n - 2 && x > xs[i + 1]) i++; const h = xs[i + 1] - xs[i], t = (x - xs[i]) / h, t2 = t * t, t3 = t2 * t;
      return (2 * t3 - 3 * t2 + 1) * ys[i] + (t3 - 2 * t2 + t) * h * m[i] + (-2 * t3 + 3 * t2) * ys[i + 1] + (t3 - t2) * h * m[i + 1]; };
  }
  function newell(v) { let nx = 0, ny = 0, nz = 0; for (let i = 0; i < v.length; i++) { const a = v[i], b = v[(i + 1) % v.length]; nx += (a[1] - b[1]) * (a[2] + b[2]); ny += (a[2] - b[2]) * (a[0] + b[0]); nz += (a[0] - b[0]) * (a[1] + b[1]); } return [nx, ny, nz]; }
  function splitBy(v, g) { const neg = [], pos = [], n = v.length;
    for (let i = 0; i < n; i++) { const a = v[i], b = v[(i + 1) % n], ga = g(a), gb = g(b); if (ga <= 0) neg.push(a); else pos.push(a);
      if ((ga <= 0 && gb > 0) || (ga > 0 && gb <= 0)) { const t = ga / (ga - gb); const p = [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t]; neg.push(p); pos.push(p); } }
    return [neg.length >= 3 ? neg : null, pos.length >= 3 ? pos : null]; }

  // ================================= PRINCIPAL DIMENSIONS =================================
  const LOA = 25.5, yA = -LOA / 2, yB = LOA / 2, L = 24.42, yTT = yB - L;   // the platform's aft edge; the stem's head; the hull at the deck, from the transom's top
  const DWL = 1.80, BEAM = 6.14;                                             // decision 12 (the draft) and decision 14 (the beam), both leans
  const MAIN_DECK = 3.75, HOUSE_SOLE = MAIN_DECK, GARAGE_SOLE = 2.15, PLATFORM_Z = GARAGE_SOLE, LOWER_SOLE = 1.50;   // LOWER_SOLE: her lowest sole (charter §1)
  const DINING_SOLE = HOUSE_SOLE - 0.60;                                     // decision 13: the dining and the helm, 0.60 under the saloon
  const ZTT = 3.78, KTR = 0.48 / 1.98, yTr = (z) => yTT + (ZTT - z) * KTR;  // the transom: its top 1.98 over the DWL, raked top aft, its foot 0.48 forward at the DWL

  // ---- the lines (A3): the sheer, the deck's half-breadth, the stem; and, designed under the waterline, the forefoot ----
  const fSz = mono([[yTT, 3.78], [-11.25, 3.86], [-10.45, 3.892], [-8.01, 3.911], [-5.56, 3.929], [-3.12, 3.948], [-0.68, 3.985], [1.77, 4.022], [4.21, 4.059],
    [6.66, 4.096], [9.10, 4.188], [11.54, 4.336], [yB, 4.336]]);
  const fSx0 = mono([[yTT, 2.90], [-9.64, 2.96], [-7.85, 3.00], [-6.53, 3.00], [-4.97, 3.03], [0.41, 3.03], [2.22, 3.07], [4.00, 3.05], [5.43, 3.01], [6.35, 2.98],
    [7.62, 2.82], [9.03, 2.50], [9.74, 2.30], [10.48, 1.99], [11.19, 1.56], [11.88, 1.08], [12.27, 0.755], [12.62, 0.32], [yB, 0]]);
  const fSx = (y) => Math.max(0, fSx0(Math.min(y, yB)));
  const TUCK = mono([[0, 0.91], [0.15, 0.45], [0.28, 0.15], [0.43, 0.05], [0.6, 0]]);   // the deck's rounded stern corners: in from fSx, by the distance from the transom
  const tuck = (d) => d >= 0.6 ? 0 : Math.max(0, TUCK(Math.max(0, d)));
  const stemY = mono([[0.95, 9.10], [1.05, 9.80], [1.20, 10.32], [1.40, 10.68], [1.60, 10.93], [1.819, 11.14], [1.893, 11.18], [2.152, 11.32], [2.374, 11.47], [2.596, 11.62],
    [2.818, 11.81], [3.040, 11.99], [3.263, 12.25], [3.485, 12.47], [3.929, 12.66], [4.336, yB]]);
  // the chine: at the waterline aft, rising forward to meet the stem
  const yTC = yTr(1.75), Y_CEND = stemY(2.70);
  const fCz = mono([[yTC, 1.75], [-6, 1.80], [0, 1.86], [4, 1.95], [7, 2.12], [9, 2.32], [10.5, 2.55], [Y_CEND, 2.70]]);
  const fCx0 = mono([[yTC, 2.80], [-6, 2.86], [0, 2.90], [3, 2.88], [6, 2.62], [8, 2.20], [9.5, 1.65], [10.7, 0.95], [Y_CEND, 0]]);
  const fCx = (y) => Math.max(0, fCx0(Math.min(y, Y_CEND)));
  // the vee bottom's centreline (15° of deadrise at the transom, 18° amidships, finer forward) into the forefoot; the keel
  // under it reaches z 0 between y -5.5 and 1.0, and its pad closes to nothing at the forefoot
  const Z_KE = 0.95, Y_KEND = stemY(Z_KE), yTK = yTr(1.00);
  const fCL = mono([[yTK, 1.00], [-6, 0.95], [0, 0.92], [4, 0.92], [7, 0.93], [Y_KEND, Z_KE]]);
  const fFin = mono([[-9.2, 1.30], [-8.4, 0.95], [-7.5, 0.45], [-6.4, 0.08], [-5.5, 0.0], [1.0, 0.0], [2.6, 0.12], [4.0, 0.42], [5.0, 0.75], [5.8, 1.05]]);
  const fKz = (y) => Math.min(fCL(y), fFin(y)), keelDepth = (y) => fCL(y) - fKz(y);
  const kTaper = (y) => clamp01((Y_KEND - y) / 2.0);
  const kwb = (y) => (0.04 + 0.06 * clamp01(keelDepth(y) / 0.25)) * kTaper(y), kwr = (y) => (0.08 + 0.12 * clamp01(keelDepth(y) / 0.25)) * kTaper(y);
  const fPB = mono([[yTC, 1.10], [0, 1.12], [6, 1.20], [Y_CEND, 1.25]]);                     // the bottom's convexity
  const fPF = mono([[yTT, 0.90], [-4, 0.95], [2, 1.00], [6, 1.25], [9, 1.60], [yB, 2.00]]);   // the topsides' flare, strongest at the bow

  // ---- the shell: rows keel -> sheer, each ending on the stem line at its own height (the 120's way), each starting on
  // the raked transom. K0 the keel's bottom on the centreline, K1 its bottom corner, K2 its root; B the bottom; C the
  // chine; T the topsides; S the sheer ----
  const GB = [0.28, 0.58, 0.84], FT = [0.30, 0.62, 0.86], R_C = 3 + GB.length, R_S = R_C + FT.length + 1, NR = R_S + 1;
  const ROWF = [0, 0, 0].concat(GB.map(() => 0), [0], FT, [1]);   // each row's fraction up the topsides: the stern's corners fade with it
  function rowsAt(s) {   // the rows' (x, z) at s, each line at its own station
    const yK = yTK + s * (Y_KEND - yTK), yC = yTC + s * (Y_CEND - yTC), yS = yTT + s * (yB - yTT);
    const zcl = fCL(yK), zk = fKz(yK), xb = kwb(yK), xr = kwr(yK), xc = fCx(yC), zc = fCz(yC), pB = fPB(yC), xs = fSx(yS), zs = fSz(yS), pF = fPF(yS);
    const zr = zcl + (zc - zcl) * (xc > 1e-6 ? Math.pow(clamp01(xr / xc), pB) : 0), P = [[0, zk], [xb, zk], [xr, zr]];
    for (const g of GB) P.push([xr + (xc - xr) * g, zr + (zc - zr) * Math.pow(g, pB)]);
    P.push([xc, zc]);
    for (const f of FT) P.push([xc + (xs - xc) * Math.pow(f, pF), zc + (zs - zc) * f]);
    P.push([xs, zs]);
    return P;
  }
  const ROW0 = rowsAt(0), ROW1 = rowsAt(1);
  const YT_R = ROW0.map(p => yTr(p[1])), YEND = ROW1.map((p, r) => r < 3 ? Y_KEND : Math.min(yB, stemY(p[1])));
  function section(s) {
    return rowsAt(s).map((p, r) => { const y = YT_R[r] + s * (YEND[r] - YT_R[r]); let x = p[0];
      if (ROWF[r] > 0) x = Math.max(0, x - tuck(y - YT_R[r]) * smooth(ROWF[r]));
      return [x, y, p[1]]; });
  }
  const SSET = new Set(); for (let i = 0; i <= 48; i++) SSET.add(+(0.5 - 0.5 * Math.cos(Math.PI * i / 48)).toFixed(5));
  for (const d of [0.03, 0.06, 0.10, 0.15, 0.21, 0.28, 0.35, 0.43, 0.52, 0.62]) SSET.add(+(d / L).toFixed(5));           // the stern's rounded corners
  for (let y = -9.4; y <= -5.2; y += 0.42) SSET.add(+((y - yTK) / (Y_KEND - yTK)).toFixed(5));                             // the keel's heel
  for (let y = 0.8; y <= 6.2; y += 0.6) SSET.add(+((y - yTK) / (Y_KEND - yTK)).toFixed(5));                                // the keel's toe
  const SS = [...SSET].filter(s => s >= 0 && s <= 1).sort((a, b) => a - b), SEC = SS.map(section), NSEC = SEC.length;

  // mesh-true half-breadth: the shell quads sliced at y = const, x(z) tabulated (the 120's)
  const HDY = 0.05, HDZ = 0.04, HNY = Math.ceil(L / HDY) + 1, HNZ = Math.ceil(4.6 / HDZ) + 1, HX = new Float32Array(HNY * HNZ);
  for (let iy = 0; iy < HNY; iy++) { const yc = Math.min(yB - 1e-4, Math.max(yTT + 1e-4, yTT + iy * HDY)), sg = [];
    for (let i = 0; i + 1 < NSEC; i++) { const A = SEC[i], B2 = SEC[i + 1]; let y0 = 1e9, y1 = -1e9; for (let k = 0; k < NR; k++) { y0 = Math.min(y0, A[k][1], B2[k][1]); y1 = Math.max(y1, A[k][1], B2[k][1]); } if (y0 > yc || y1 < yc) continue;
      for (let k = 0; k + 1 < NR; k++) { const q = [A[k], B2[k], B2[k + 1], A[k + 1]], pts = [];
        for (let e = 0; e < 4; e++) { const a = q[e], b = q[(e + 1) % 4], da = a[1] - yc, db = b[1] - yc; if ((da <= 0 && db > 0) || (da > 0 && db <= 0)) { const t = da / (da - db); pts.push([a[0] + (b[0] - a[0]) * t, a[2] + (b[2] - a[2]) * t]); } }
        if (pts.length >= 2) sg.push(pts[0], pts[1]); } }
    let lo = -1, hi = -1;
    for (let iz = 0; iz < HNZ; iz++) { const zc = iz * HDZ; let best = -1;
      for (let s = 0; s < sg.length; s += 2) { const p = sg[s], r = sg[s + 1]; if ((p[1] - zc) * (r[1] - zc) > 0) continue; const dz = r[1] - p[1];
        const x = Math.abs(dz) < 1e-9 ? Math.min(p[0], r[0]) : p[0] + (r[0] - p[0]) * (zc - p[1]) / dz; if (best < 0 || x < best) best = x; }
      HX[iy * HNZ + iz] = best; if (best >= 0) { if (lo < 0) lo = iz; hi = iz; } }
    for (let iz = 0; iz < HNZ; iz++) { const k = iy * HNZ + iz; if (lo < 0 || iz < lo) HX[k] = 0; else if (iz > hi) HX[k] = HX[iy * HNZ + hi]; else if (HX[k] < 0) HX[k] = HX[k - 1]; } }
  function halfAtZ(y, z) { if (y < yTT || y > yB) return 0;
    const fy = (y - yTT) / HDY, iy = Math.min(HNY - 2, Math.max(0, Math.floor(fy))), ty = clamp01(fy - iy), fz = clamp(z / HDZ, 0, HNZ - 1.001), iz = Math.floor(fz), tz = fz - iz;
    const a = HX[iy * HNZ + iz], b = HX[iy * HNZ + iz + 1], c = HX[(iy + 1) * HNZ + iz], d = HX[(iy + 1) * HNZ + iz + 1];
    return Math.max(0, Math.min(lerp(a, b, tz), lerp(c, d, tz))); }
  const sheerX = (y) => Math.max(0, fSx(y) - tuck(y - yTT));
  const BEAM_LOFT = 2 * Math.max(...SEC.map(sec => sec[R_S][0]));

  // ================================= FACE EMISSION =================================
  // ADR 0048's ids, as the 120 writes them: every face carries lv, ro and it. PART names each face's part for the parts
  // channel only; it is never a face's field
  const LV = { hull: 0, main_deck: 1, house: 2, bridge: 3, below: 4, rigging: 5, foredeck: 6, top: 7 };
  const RO = { shell: 0, wall: 1, roof: 2, lining: 3, part: 4, floor: 5, fit: 6, tall: 7, trim: 8, rig: 9 };
  const GHOST_ROLES = ['shell', 'wall', 'roof', 'lining', 'part', 'tall', 'trim'], LV_NAME = Object.keys(LV), RO_NAME = Object.keys(RO), GHOST_RO = new Set(GHOST_ROLES.map(n => RO[n]));
  // F is her exterior, IF her rooms (it true: pass C), each face pushed to TG, the list being drawn
  const F = [], IF = [], PART = new WeakMap(); let TG = F, cLV = LV.hull, cRO = RO.shell, cPT = 'hull', cIT = false;
  const tagOf = (T, k, n) => { if (T[n] == null) throw new Error('MotorYacht84Iso: no ' + k + ' id ' + n); return T[n]; };
  const at = (lv, ro, pt) => { cLV = tagOf(LV, 'lv', lv); if (ro) cRO = tagOf(RO, 'ro', ro); if (pt) cPT = pt; };
  const put = (f) => { f.lv = cLV; f.ro = cRO; f.it = cIT; PART.set(f, cPT); TG.push(f); };
  function Q(v, out, mat, b, db) { const n = newell(v); if (Math.hypot(n[0], n[1], n[2]) < 1e-9) return; if (n[0] * out[0] + n[1] * out[1] + n[2] * out[2] < 0) v = v.slice().reverse(); put({ v, mat, b: b || 0, db: db || 0 }); }
  function capture(fn) { const s = TG, tmp = []; TG = tmp; fn(); TG = s; return tmp; }
  function tubeQ(A, B2, r0, r1, mat, b) { const ax = v_norm(v_sub(B2, A)); let up = [0, 0, 1]; if (Math.abs(ax[2]) > 0.9) up = [0, 1, 0];
    const u = v_norm(v_cross(ax, up)), w = v_cross(u, ax);
    const ring = (P, r) => [0, 1, 2, 3].map(k => { const a = k * Math.PI / 2 + Math.PI / 4; return v_add(P, v_add(v_mul(u, Math.cos(a) * r), v_mul(w, Math.sin(a) * r))); });
    const R0 = ring(A, r0), R1 = ring(B2, r1 == null ? r0 : r1), mid = v_mul(v_add(A, B2), 0.5);
    for (let k = 0; k < 4; k++) { const k2 = (k + 1) % 4, q = [R0[k], R0[k2], R1[k2], R1[k]], c = v_mul(v_add(v_add(q[0], q[1]), v_add(q[2], q[3])), 0.25); Q(q, v_sub(c, mid), mat, b, -0.15); } }
  function bands(v, out, BD, bb) { let rest = v;
    for (const [zt, mat, b, db] of BD) { if (!rest) break; if (zt === Infinity) { Q(rest, out, mat, (b || 0) + (bb || 0), db); break; }
      const [lo, hi] = splitBy(rest, (p) => p[2] - zt); if (lo) Q(lo, out, mat, (b || 0) + (bb || 0), db); rest = hi; } }
  // ---- the 120's solids: a box (skip names the faces left out), a cylinder or cone on z, a dome on a drum ----
  function boxQ(x0, x1, y0, y1, z0, z1, mat, b, db, topMat, skip) { const s = skip || '';
    if (s.indexOf('t') < 0) Q([[x0, y0, z1], [x1, y0, z1], [x1, y1, z1], [x0, y1, z1]], [0, 0, 1], topMat || mat, b, db);
    if (s.indexOf('b') < 0) Q([[x0, y0, z0], [x1, y0, z0], [x1, y1, z0], [x0, y1, z0]], [0, 0, -1], mat, (b || 0) - 1.2, db);
    if (s.indexOf('f') < 0) Q([[x0, y1, z0], [x1, y1, z0], [x1, y1, z1], [x0, y1, z1]], [0, 1, 0], mat, b, db);
    if (s.indexOf('a') < 0) Q([[x0, y0, z0], [x1, y0, z0], [x1, y0, z1], [x0, y0, z1]], [0, -1, 0], mat, b, db);
    if (s.indexOf('r') < 0) Q([[x1, y0, z0], [x1, y1, z0], [x1, y1, z1], [x1, y0, z1]], [1, 0, 0], mat, b, db);
    if (s.indexOf('l') < 0) Q([[x0, y0, z0], [x0, y1, z0], [x0, y1, z1], [x0, y0, z1]], [-1, 0, 0], mat, b, db); }
  function cylZ(cx, cy, z0, z1, r, n, mat, b, topMat, r1) { const R1 = r1 == null ? r : r1, P = (rr, a, z) => [cx + rr * Math.cos(a), cy + rr * Math.sin(a), z];
    for (let k = 0; k < n; k++) { const a0 = k / n * 2 * Math.PI, a1 = (k + 1) / n * 2 * Math.PI, am = (a0 + a1) / 2; Q([P(r, a0, z0), P(r, a1, z0), P(R1, a1, z1), P(R1, a0, z1)], [Math.cos(am), Math.sin(am), 0], mat, b, 0); }
    if (topMat !== null) { const top = []; for (let k = 0; k < n; k++) top.push(P(R1, k / n * 2 * Math.PI, z1)); Q(top, [0, 0, 1], topMat || mat, (b || 0) + 0.2, 0); } }
  function domeZ(cx, cy, z0, r, hCyl, n, mat, b, hDome) { const h = hDome == null ? r : hDome; cylZ(cx, cy, z0, z0 + hCyl, r, n, mat, b, null);   // hDome: a flattened dome
    const rings = [[r, z0 + hCyl], [r * 0.87, z0 + hCyl + h * 0.5], [r * 0.5, z0 + hCyl + h * 0.87], [0.001, z0 + hCyl + h]];
    for (let i = 0; i + 1 < rings.length; i++) { const [ra, za] = rings[i], [rb, zb] = rings[i + 1];
      for (let k = 0; k < n; k++) { const a0 = k / n * 2 * Math.PI, a1 = (k + 1) / n * 2 * Math.PI, am = (a0 + a1) / 2, P = (rr, a, z) => [cx + rr * Math.cos(a), cy + rr * Math.sin(a), z];
        Q([P(ra, a0, za), P(ra, a1, za), P(rb, a1, zb), P(rb, a0, zb)], [Math.cos(am) * (1 - i * 0.3), Math.sin(am) * (1 - i * 0.3), 0.3 + i * 0.35], mat, (b || 0) + 0.1 * i, 0); } } }
  // ---- holes: a face less rectangles { x0, x1, y0, y1 } (k 1, a deck) or { x0, x1, z0, z1 } (k 2, the transom); a lid on
  // a deck; a patch laid on the hull's skin (the 120's), its ends rounded over 'ends' of its length ----
  const span = (v, k) => { let lo = Infinity, hi = -Infinity; for (const p of v) { if (p[k] < lo) lo = p[k]; if (p[k] > hi) hi = p[k]; } return [lo, hi]; };
  function holed(v, holes, k) { const a = k === 1 ? 'y' : 'z'; let parts = [v];
    for (const r of holes || []) { const w0 = r[a + '0'], w1 = r[a + '1'], nx = [];
      for (const p of parts) { const x = span(p, 0), w = span(p, k);
        if (x[1] <= r.x0 || x[0] >= r.x1 || w[1] <= w0 || w[0] >= w1) { nx.push(p); continue; }
        let rest = p; for (const g of [(q) => q[0] - r.x0, (q) => r.x1 - q[0], (q) => q[k] - w0, (q) => w1 - q[k]]) { if (!rest) break; const s = splitBy(rest, g); if (s[0]) nx.push(s[0]); rest = s[1]; } }
      parts = nx; }
    return parts; }
  function putH(v, out, mat, b, db, holes) { holed(v, holes, 1).forEach(p => Q(p, out, mat, b, db)); }
  function lidOn(x0, x1, y0, y1, h, zf, mat, b) { const z0 = zf(y0), z1 = zf(y1);
    Q([[x0, y0, z0 + h], [x1, y0, z0 + h], [x1, y1, z1 + h], [x0, y1, z1 + h]], [0, 0, 1], mat, (b || 0) + 0.2, 0);
    Q([[x0, y0, z0], [x1, y0, z0], [x1, y0, z0 + h], [x0, y0, z0 + h]], [0, -1, 0], mat, b, 0); Q([[x0, y1, z1], [x1, y1, z1], [x1, y1, z1 + h], [x0, y1, z1 + h]], [0, 1, 0], mat, b, 0);
    Q([[x0, y0, z0], [x0, y1, z1], [x0, y1, z1 + h], [x0, y0, z0 + h]], [-1, 0, 0], mat, b, 0); Q([[x1, y0, z0], [x1, y1, z1], [x1, y1, z1 + h], [x1, y0, z0 + h]], [1, 0, 0], mat, b, 0); }
  const nodup = (v) => v.filter((p, i) => { const q = v[(i + v.length - 1) % v.length]; return Math.abs(p[0] - q[0]) + Math.abs(p[1] - q[1]) + Math.abs(p[2] - q[2]) > 1e-9; });
  // cleanPoly(v): v without its repeated corners, or null when what is left has no area (the 120's)
  function cleanPoly(v) { const o = []; for (const p of v) { const q = o[o.length - 1]; if (!q || Math.abs(p[0] - q[0]) + Math.abs(p[1] - q[1]) + Math.abs(p[2] - q[2]) > 1e-9) o.push(p); }
    while (o.length > 2) { const a = o[0], q = o[o.length - 1]; if (Math.abs(a[0] - q[0]) + Math.abs(a[1] - q[1]) + Math.abs(a[2] - q[2]) > 1e-9) break; o.pop(); }
    const n = newell(o); return o.length >= 3 && Math.hypot(n[0], n[1], n[2]) > 2e-8 ? o : null; }
  const role = (ro) => { cRO = tagOf(RO, 'ro', ro); };
  function hullPatch(y0, y1, z0, z1, mat, b, off, ends, db, n) {
    const N = n || 10, e = (t) => { const k = Math.min(1, Math.min(t, 1 - t) / (ends || 0.16)); return Math.sqrt(Math.max(0, 1 - (1 - k) * (1 - k))); };
    for (let i = 0; i < N; i++) { const ta = i / N, tb = (i + 1) / N, ya = lerp(y0, y1, ta), yb = lerp(y0, y1, tb), zm = (z0 + z1) / 2, ha = (z1 - z0) / 2 * e(ta), hb = (z1 - z0) / 2 * e(tb);
      for (const sd of [-1, 1]) { const P = (y, z) => [sd * (halfAtZ(y, z) + off), y, z]; Q(nodup([P(ya, zm - ha), P(yb, zm - hb), P(yb, zm + hb), P(ya, zm + ha)]), [sd, 0, 0], mat, b, db == null ? 0.05 : db); } } }
  // the skin: a point is inside her shell when it lies within the mesh-true half-breadth at its height; toSkin walks from
  // P (outside) along dir to the skin, and stops a hair inside it
  const inHull = (p) => Math.abs(p[0]) < halfAtZ(p[1], p[2]);
  function toSkin(P, dir, tMax) { let lo = 0, hi = tMax; for (let i = 0; i < 40; i++) { const m = (lo + hi) / 2; if (inHull(v_add(P, v_mul(dir, m)))) hi = m; else lo = m; } return v_add(P, v_mul(dir, hi)); }

  // ---- plan outlines + ring lofts (the house, the hardtop), as the fleet builds them ----
  const NN = 14, NSD = 18, NCR = 5, NAF = 4, NC = NN + 1 + NSD + NCR, NP = NC + NAF;
  function outline(R, o) {
    const e = o.e || 2.2, rA = Math.min(o.rA || 0.5, R.W * 0.5), P = [];
    const yS1 = R.yA + rA, yN = Math.max(R.yF - R.N, yS1 + 0.02), N = R.yF - yN;
    for (let i = 0; i <= NN; i++) { const th = (i / NN) * Math.PI / 2; P.push([R.W * Math.pow(Math.sin(th), 2 / e), yN + N * Math.pow(Math.cos(th), 2 / e)]); }
    for (let i = 1; i <= NSD; i++) P.push([R.W, lerp(yN, yS1, i / NSD)]);
    for (let i = 1; i <= NCR; i++) { const a = (i / NCR) * Math.PI / 2; P.push([R.W - rA + rA * Math.cos(a), R.yA + rA - rA * Math.sin(a)]); }
    for (let i = 1; i <= NAF; i++) P.push([(R.W - rA) * (1 - i / NAF), R.yA]);
    return P;
  }
  const pnorm = (P, j) => { const a = P[Math.max(0, j - 1)], b = P[Math.min(P.length - 1, j + 1)]; const tx = b[0] - a[0], ty = b[1] - a[1], m = Math.hypot(tx, ty) || 1; return [-ty / m, tx / m]; };
  function ringTable(tbl) { const fs = [1, 2, 3, 4].map(c => mono(tbl.map(r => [r[0], r[c]]))); return (z) => ({ z, yF: fs[0](z), yA: fs[1](z), W: fs[2](z), N: fs[3](z) }); }
  function ringsFrom(tbl, zs, o) { const f = ringTable(tbl); return zs.map(z => ({ z, P: outline(f(z), o) })); }
  function ringAt(RG, z) { let k = 0; while (k < RG.length - 2 && z > RG[k + 1].z) k++; const A = RG[k], B2 = RG[k + 1], t = clamp01((z - A.z) / (B2.z - A.z));
    return { z, P: A.P.map((p, j) => [lerp(p[0], B2.P[j][0], t), lerp(p[1], B2.P[j][1], t)]) }; }
  function hwAt(P, y) { if (y > P[0][1]) return 0; for (let j = 0; j + 1 < NC; j++) { const a = P[j], b = P[j + 1]; if ((a[1] - y) * (b[1] - y) <= 0) { const t = a[1] === b[1] ? 0 : (a[1] - y) / (a[1] - b[1]); return lerp(a[0], b[0], t); } } return 0; }
  function loftWalls(RG, mat, b) {
    for (let k = 0; k + 1 < RG.length; k++) { const A = RG[k], B2 = RG[k + 1], zf = k / (RG.length - 1);
      for (let j = 0; j + 1 < NC; j++) for (const sd of [-1, 1]) { const a0 = A.P[j], a1 = A.P[j + 1], b0 = B2.P[j], b1 = B2.P[j + 1], n0 = pnorm(A.P, j), n1 = pnorm(A.P, j + 1), nx = (n0[0] + n1[0]) / 2, ny = (n0[1] + n1[1]) / 2;
        Q([[sd * a0[0], a0[1], A.z], [sd * a1[0], a1[1], A.z], [sd * b1[0], b1[1], B2.z], [sd * b0[0], b0[1], B2.z]], [sd * nx, ny, 0.04], mat, (b || 0) + 0.02 + 0.40 * Math.max(0, ny) - 0.45 * Math.max(0, -ny) + 0.12 * zf, 0); } } }
  function aftWall(RG, mat, b) { for (let k = 0; k + 1 < RG.length; k++) { const A = RG[k], B2 = RG[k + 1], EA = A.P[NC - 1][0], EB = B2.P[NC - 1][0], ya = A.P[NC][1], yb = B2.P[NC][1];
    Q([[-EA, ya, A.z], [EA, ya, A.z], [EB, yb, B2.z], [-EB, yb, B2.z]], [0, -1, 0.05], mat, (b || 0) - 0.45, 0); } }
  function clipSeg(pa, pb, y0, y1) { const ya = pa[1], yb = pb[1]; if (Math.abs(yb - ya) < 1e-9) return (ya >= y0 && ya <= y1) ? [0, 1] : null;
    let t0 = (y0 - ya) / (yb - ya), t1 = (y1 - ya) / (yb - ya); if (t0 > t1) { const t = t0; t0 = t1; t1 = t; } t0 = Math.max(0, t0); t1 = Math.min(1, t1); return t1 - t0 > 1e-4 ? [t0, t1] : null; }
  // a patch laid on a ring loft's nose + sides between two heights (windows, doors, glazing bands)
  function patchOn(RG, y0, y1, z0, z1, mat, b, off, db) {
    const nz = Math.max(1, Math.ceil((z1 - z0) / 0.30));
    for (let s = 0; s < nz; s++) { const za = lerp(z0, z1, s / nz), zb = lerp(z0, z1, (s + 1) / nz), A = ringAt(RG, za).P, B2 = ringAt(RG, zb).P;
      for (let j = 0; j + 1 < NC; j++) { const ca = clipSeg(A[j], A[j + 1], y0, y1), cb = clipSeg(B2[j], B2[j + 1], y0, y1); if (!ca || !cb) continue;
        const n = pnorm(A, j), P = (R, t, z, sd) => { const p = [lerp(R[j][0], R[j + 1][0], t), lerp(R[j][1], R[j + 1][1], t)]; return [sd * (p[0] + off * n[0]), p[1] + off * n[1], z]; };
        for (const sd of [-1, 1]) Q([P(A, ca[0], za, sd), P(A, ca[1], za, sd), P(B2, cb[1], zb, sd), P(B2, cb[0], zb, sd)], [sd * n[0], n[1], 0.05], mat, b, db); } } }
  // a glazing band on a ring loft: every outline segment from zLo to zTop, stood off the wall along its normal, cut to
  // y0..y1 and to the band's bottom zb(y), so that the band runs round the nose without a seam; its slabs break at the
  // loft's own rings, so that the glass lies on the walls' facets
  function glassOn(RG, y0, y1, zLo, zTop, zb, off, mat, b, db) {
    const ZB = [zLo].concat(RG.map(r => r.z).filter(z => z > zLo + 1e-6 && z < zTop - 1e-6), [zTop]), ZS = [zLo];
    for (let i = 0; i + 1 < ZB.length; i++) { const n = Math.max(1, Math.ceil((ZB[i + 1] - ZB[i]) / 0.30)); for (let k = 1; k <= n; k++) ZS.push(lerp(ZB[i], ZB[i + 1], k / n)); }
    for (let s = 0; s + 1 < ZS.length; s++) { const za = ZS[s], zc = ZS[s + 1], A = ringAt(RG, za).P, B2 = ringAt(RG, zc).P;
      for (let j = 0; j + 1 < NC; j++) { const na = pnorm(A, j), nb = pnorm(A, j + 1), nx = (na[0] + nb[0]) / 2, ny = (na[1] + nb[1]) / 2;
        for (const sd of [-1, 1]) { const P = (R, k, z) => { const n = pnorm(R, k); return [sd * (R[k][0] + off * n[0]), R[k][1] + off * n[1], z]; };
          let v = [P(A, j, za), P(A, j + 1, za), P(B2, j + 1, zc), P(B2, j, zc)];
          v = splitBy(v, (p) => y0 - p[1])[0]; if (v) v = splitBy(v, (p) => p[1] - y1)[0]; if (v) v = splitBy(v, (p) => zb(p[1]) - p[2])[0];
          if (v) Q(v, [sd * nx, ny, 0.05], mat, b, db); } } } }
  // a surface over a half-plan outline whose height is a function of y, strip by strip from the centreline, less its holes
  function slab(P, zf, out, mat, b, holes) {
    for (let j = 0; j + 1 < NP; j++) { const a = P[j], c = P[j + 1]; if (Math.abs(a[1] - c[1]) < 1e-6) continue;
      for (const sd of [-1, 1]) putH([[0, a[1], zf(a[1])], [sd * a[0], a[1], zf(a[1])], [sd * c[0], c[1], zf(c[1])], [0, c[1], zf(c[1])]], out, mat, b, 0, holes); } }

  // ================================= THE DOORS' LEAVES (pass C) =================================
  // the 120's: door() names a leaf in its wall's plane (n 0 a side wall, an x-plane, its opening along y; n 1 an end
  // wall, a y-plane, its opening along x; o the side it opens to), and leafCut(doors, fn) draws fn's faces less each
  // opening, the pieces cut out kept as that leaf's faces. leafPose(D, t) moves them: a slide stands off its wall and
  // runs along it, a hinge turns about its post toward o. The leaves are never in faceList: doorFaces(t) gives them
  const LEAVES = [], LEAF = {};
  function door(id, n, w, u0, u1, z0, z1, o, pose) { const D = Object.assign({ id, n, w, u0, u1, z0, z1, o, faces: [] }, pose); LEAVES.push(D); LEAF[id] = D; return D; }
  function leafCut(doors, fn) { const s = TG, tmp = []; TG = tmp; fn(); TG = s;
    const keep = (f, v) => { const g = Object.assign({}, f, { v }); PART.set(g, PART.get(f)); return g; };
    for (const f of tmp) { let parts = [f.v];
      for (const D of doors) { const u = 1 - D.n, nx = [];
        for (const v of parts) { let c = 0, lo = Infinity, hi = -Infinity, zl = Infinity, zh = -Infinity;
          for (const p of v) { c += p[D.n] / v.length; lo = Math.min(lo, p[u]); hi = Math.max(hi, p[u]); zl = Math.min(zl, p[2]); zh = Math.max(zh, p[2]); }
          if (Math.abs(c - D.w) > 0.12 || hi <= D.u0 + 1e-6 || lo >= D.u1 - 1e-6 || zh <= D.z0 + 1e-6 || zl >= D.z1 - 1e-6) { nx.push(v); continue; }
          const outs = []; let rest = v;
          for (const g of [(p) => p[u] - D.u0, (p) => D.u1 - p[u], (p) => p[2] - D.z0, (p) => D.z1 - p[2]]) { if (!rest) break; const sp = splitBy(rest, g); if (sp[0]) outs.push(sp[0]); rest = sp[1]; }
          rest = rest && cleanPoly(rest); if (!rest) { nx.push(v); continue; }
          D.faces.push(keep(f, rest)); for (const q of outs) { const c2 = cleanPoly(q); if (c2) nx.push(c2); } }
        parts = nx; }
      if (parts.length === 1 && parts[0] === f.v) TG.push(f); else for (const v of parts) TG.push(keep(f, v)); } }
  function leafPose(D, t) { if (!(t > 1e-4)) return D.faces; let M;
    if (D.kind === 'slide') { const k = clamp01(t / 0.15), d = D.dir * t * D.travel, dx = (D.n === 1 ? d : 0) + D.o[0] * D.out * k, dy = (D.n === 0 ? d : 0) + D.o[1] * D.out * k; M = (p) => [p[0] + dx, p[1] + dy, p[2]]; }
    else { const hx = D.n === 0 ? D.w : D.hu, hy = D.n === 0 ? D.hu : D.w, e = Math.sign((D.u0 + D.u1) / 2 - D.hu), fx = D.n === 1 ? e : 0, fy = D.n === 0 ? e : 0;
      const a = Math.sign(fx * D.o[1] - fy * D.o[0]) * t * (D.swing || 90) * DEG, c = Math.cos(a), s = Math.sin(a);
      M = (p) => { const dx = p[0] - hx, dy = p[1] - hy; return [hx + dx * c - dy * s, hy + dx * s + dy * c, p[2]]; }; }
    return D.faces.map(f => { const g = Object.assign({}, f, { v: f.v.map(M) }); PART.set(g, PART.get(f)); return g; }); }

  // ================================= HULL SHELL =================================
  at('hull', 'shell', 'hull');
  const HB = [[DWL + 0.02, 'boot', -0.3, 0], [DWL + 0.14, 'stripe', 0.1, 0.004], [Infinity, 'hull', 0, 0]];   // the bottom, the thin boot line, the topsides
  const mx = (p, sd) => [sd * p[0], p[1], p[2]];
  const OUTK = (k, sd) => k === 0 ? [0, 0, -1] : k === 1 ? [sd, 0, 0] : k < R_C ? [sd * 0.4, 0, -1] : [sd, 0, 0.06];
  // the keel's flat ends where it is narrower than 5 mm: a sliver there is a face no view of the sea can hold
  for (let i = 0; i + 1 < NSEC; i++) { const A = SEC[i], B2 = SEC[i + 1];
    for (let k = 0; k + 1 < NR; k++) if (k > 0 || Math.max(A[1][0], B2[1][0]) >= 0.005) for (const sd of [-1, 1]) bands([mx(A[k], sd), mx(B2[k], sd), mx(B2[k + 1], sd), mx(A[k + 1], sd)], OUTK(k, sd), HB, k < R_C ? -0.15 : 0); }
  // the transom: one raked plane, laid strip by strip between the rows so that every piece is convex, less the garage's
  // mouth over the platform and, to starboard of it, the slot of the steps up to the aft deck
  const MOUTH = { x0: -1.85, x1: 1.05, z0: PLATFORM_Z, z1: 3.45 }, SLOT = { x0: 1.20, x1: 1.85, z0: PLATFORM_Z, z1: 99 };
  { const s0 = SEC[0], TB = [[DWL + 0.02, 'boot', -0.9, 0], [DWL + 0.14, 'stripe', -0.5, 0.004], [Infinity, 'hull', -0.7, 0]];
    for (const f of capture(() => { for (let k = 0; k + 1 < NR; k++) { const a = s0[k], c = s0[k + 1]; bands([[a[0], a[1], a[2]], [c[0], c[1], c[2]], [-c[0], c[1], c[2]], [-a[0], a[1], a[2]]], [0, -1, 0], TB, 0); } }))
      for (const v of holed(f.v, [MOUTH, SLOT], 2)) { const g = Object.assign({}, f, { v }); PART.set(g, PART.get(f)); TG.push(g); } }
  // the garage behind its mouth (level below), in shadow: its teak sole, its white head and sides, and its back wall in
  // warm wood (C1), lit by the washes of three lights in its head (the lights themselves, tucked under the head, never
  // show through the mouth). C4: it runs forward to the master cabin's aft bulkhead (Y_GB, u 0.237), and its back wall
  // holds the inner door to her, a hinged leaf that opens aft into the garage, three steps above the cabin's sole
  const Y_GB = -6.71;
  { const { x0, x1, z0, z1 } = MOUTH, yb = Y_GB, P = (x, z) => [x, yTr(z), z];
    const GI = door('garage_inner', 1, yb, -1.75, -1.05, z0, z1 - 0.05, [0, -1], { kind: 'hinge', hu: -1.75 });
    at('below', 'floor', 'garage'); Q([P(x0, z0), P(x1, z0), [x1, yb, z0], [x0, yb, z0]], [0, 0, 1], 'teak', TEAK_B - 2.2, 0);
    at('below', 'roof'); Q([P(x0, z1), P(x1, z1), [x1, yb, z1], [x0, yb, z1]], [0, 0, -1], 'cream', -2.2, 0); at('below', 'lining');
    Q([P(x0, z0), [x0, yb, z0], [x0, yb, z1], P(x0, z1)], [1, 0, 0], 'cream', -1.4, 0); Q([P(x1, z0), [x1, yb, z0], [x1, yb, z1], P(x1, z1)], [-1, 0, 0], 'cream', -1.4, 0);
    leafCut([GI], () => Q([[x0, yb, z0], [x1, yb, z0], [x1, yb, z1], [x0, yb, z1]], [0, -1, 0], 'wood', -0.2, 0));
    for (const f of GI.faces) { f.b = -0.7; f.ro = RO.wall; PART.set(f, 'door'); }
    for (const x of [-0.70, 0, 0.70]) Q([[x - 0.20, yb - 0.004, z1 - 0.62], [x + 0.20, yb - 0.004, z1 - 0.62], [x + 0.12, yb - 0.004, z1 - 0.06], [x - 0.12, yb - 0.004, z1 - 0.06]], [0, -1, 0], 'wood', 0.9, 0.01); }

  // ================================= THE SWIM PLATFORM: THE GARAGE'S DOOR (main_deck) =================================
  // 4.60 m wide, its top at PLATFORM_Z, from the transom to loa's aft end (A3: 1.08 m aft of the transom's top); its
  // sides run 0.70 m aft, then taper in to its rounded aft corners. It is the garage's door (spec §4.4, by state): it
  // turns about its hinge, the transom's line at PLATFORM_Z, from down (garage 1, the default: decision 2's loa) to shut
  // (garage 0), up in the transom's plane. Its own list, never in F: faceList(state) and partFaces('garage', state)
  const PLAT = { x: 2.30, t: 0.25, s: 0.70, xc: 1.928, r: 0.30 };
  const HINGE = { y: yTr(PLATFORM_Z), z: PLATFORM_Z }, DOOR_SHUT = Math.atan2(1, KTR);
  let DOOR_UNDER = null;
  const DOOR_F = capture(() => {
    const z1 = PLATFORM_Z, z0 = z1 - PLAT.t, cy = (yA + HINGE.y) / 2, A = [PLAT.x, HINGE.y - PLAT.s], C = [PLAT.xc, yA];
    const um = Math.hypot(A[0] - C[0], A[1] - C[1]), u = [(A[0] - C[0]) / um, (A[1] - C[1]) / um], th = Math.acos(-u[0]), tl = PLAT.r / Math.tan(th / 2);
    const O = [C[0] - tl, yA + PLAT.r], aT = Math.atan2(C[1] + u[1] * tl - O[1], C[0] + u[0] * tl - O[0]), HALF = [A];
    for (let i = 0; i <= 4; i++) { const a = lerp(aT, -Math.PI / 2, i / 4); HALF.push([O[0] + PLAT.r * Math.cos(a), O[1] + PLAT.r * Math.sin(a)]); }
    const pts = (z) => { const o = [[PLAT.x, yTr(z)]].concat(HALF), n = o.length; for (let i = n - 1; i >= 0; i--) o.push([-o[i][0], o[i][1]]); return o; };
    const T1 = pts(z1), T0 = pts(z0); PLAT.plan = T1;
    at('main_deck', 'floor', 'platform'); Q(T1.map(p => [p[0], p[1], z1]), [0, 0, 1], 'teak', TEAK_B, 0);
    at('main_deck', 'trim'); Q(T0.map(p => [p[0], p[1], z0]), [0, 0, -1], 'hull', -1.2, 0); DOOR_UNDER = TG[TG.length - 1];
    for (let i = 0; i + 1 < T1.length; i++) { const a0 = T0[i], c0 = T0[i + 1], a1 = T1[i], c1 = T1[i + 1];
      Q([[a0[0], a0[1], z0], [c0[0], c0[1], z0], [c1[0], c1[1], z1], [a1[0], a1[1], z1]], [(a1[0] + c1[0]) / 2, (a1[1] + c1[1]) / 2 - cy, 0], 'hull', 0.1, 0); }
    // the swim ladder's head plate, flush in the platform's aft edge: stowed, only it shows (the 120's)
    at('main_deck', 'fit', 'ladder'); Q([[-0.27, yA + 0.03, z1 + 0.013], [0.27, yA + 0.03, z1 + 0.013], [0.27, yA + 0.11, z1 + 0.013], [-0.27, yA + 0.11, z1 + 0.013]], [0, 0, 1], 'steel', 0.1, 0.004);
  });
  // the door at t (0 shut, 1 down): turned about the hinge; its underside, shut, is the transom's
  function doorAt(t) { const tt = clamp01(t), ph = (1 - tt) * DOOR_SHUT; if (ph < 1e-9) return DOOR_F;
    const c = Math.cos(ph), s = Math.sin(ph), R = (p) => { const dy = p[1] - HINGE.y, dz = p[2] - HINGE.z; return [p[0], HINGE.y + c * dy + s * dz, HINGE.z - s * dy + c * dz]; };
    return DOOR_F.map(f => { const g = Object.assign({}, f, { v: f.v.map(R) }); if (f === DOOR_UNDER) g.b = lerp(-0.7, -1.2, tt); PART.set(g, PART.get(f)); return g; }); }
  // the swim ladder (the 120's): telescopic, hung off the platform's aft edge into the water (state ladder, with the
  // door down); its rungs have no bottoms, so that the sea sees each one's top from below
  const LADDER = { half: 0.24, rung: 0.24, rungs: Math.ceil((PLATFORM_Z - DWL + 0.6) / 0.24), y: yA - 0.04 };
  const LADDER_DEP = capture(() => { at('main_deck', 'fit', 'ladder'); const ZP = PLATFORM_Z, y = LADDER.y, h = LADDER.half, zb = ZP - LADDER.rungs * LADDER.rung - 0.06;
    for (const x of [-h, h]) { tubeQ([x, y, ZP + 0.013], [x, y, zb], 0.02, 0.02, 'steel', 0.3); tubeQ([x, y, ZP], [x, y, ZP + 0.55], 0.02, 0.02, 'steel', 0.3);
      tubeQ([x, y, ZP + 0.55], [x, yA + 0.07, ZP + 0.55], 0.02, 0.02, 'steel', 0.3); tubeQ([x, yA + 0.07, ZP + 0.55], [x, yA + 0.07, ZP + 0.013], 0.02, 0.02, 'steel', 0.3); }
    for (let k = 1; k <= LADDER.rungs; k++) { const z = ZP - k * LADDER.rung; boxQ(-h + 0.02, h - 0.02, y - 0.09, y + 0.01, z - 0.015, z + 0.015, 'steel', 0.2, 0.01, null, 'b'); }
    LADDER.bottom = r3(zb); });
  // the steps from the platform up to the aft deck, in the transom's slot at the starboard quarter (A4): eight risers
  // of 0.20, seven teak treads of 0.25, between the slot's two walls
  const STEPS = { x0: SLOT.x0, x1: SLOT.x1, n: 8, rise: (MAIN_DECK - PLATFORM_Z) / 8, going: 0.25, y0: HINGE.y };
  STEPS.yTop = STEPS.y0 + (STEPS.n - 1) * STEPS.going;
  { const { x0, x1, n, going, y0 } = STEPS, zk = (k) => PLATFORM_Z + k * STEPS.rise, yk = (k) => y0 + k * going;
    at('main_deck', 'lining', 'stair');
    for (let k = 0; k < n; k++) Q([[x0, yk(k), zk(k)], [x1, yk(k), zk(k)], [x1, yk(k), zk(k + 1)], [x0, yk(k), zk(k + 1)]], [0, -1, 0], 'cream', -0.2, 0);
    for (const [x, sd] of [[x0, 1], [x1, -1]]) { Q([[x, y0, PLATFORM_Z], [x, yTr(MAIN_DECK), MAIN_DECK], [x, y0, MAIN_DECK]], [sd, 0, 0], 'cream', 0.05, 0);
      for (let k = 1; k < n; k++) Q([[x, yk(k - 1), zk(k)], [x, yk(k), zk(k)], [x, yk(k), MAIN_DECK], [x, yk(k - 1), MAIN_DECK]], [sd, 0, 0], 'cream', 0.05, 0); }
    at('main_deck', 'floor');
    for (let k = 1; k < n; k++) Q([[x0, yk(k - 1), zk(k)], [x1, yk(k - 1), zk(k)], [x1, yk(k), zk(k)], [x0, yk(k), zk(k)]], [0, 0, 1], 'teak', TEAK_B, 0); }

  // ================================= THE DECKS, THE BULWARK'S LINING AND CAP =================================
  // the aft deck and the side decks at MAIN_DECK; one step up at Y_FD to the foredeck, which follows the sheer
  const Y_FD = 6.85, FD_DROP = 0.12;
  const deckZ = (y) => y < Y_FD ? MAIN_DECK : fSz(y) - FD_DROP;
  // the bulwark's inside at y; aft of the hull's table's first full row (yT1: its first cell is nil under the
  // transom's raked top) that row's, so that the transom's cap, the deck's first strip and its lining reach the corners
  const yT1 = yTT + HDY, xin = (y, z) => Math.max(0.02, Math.min(sheerX(y) - 0.005, halfAtZ(Math.max(y, yT1), (z != null ? z : deckZ(y)) + 0.03) - 0.10));
  // ================================= THE HOUSE =================================
  // one low house, 2.37 m a side, its aft face at u 0.242 (A3's plan) and its rounded front where the profile has it
  // (its glass still seen at f 0.75; the plan puts the front at u 0.743, 0.6 m further aft); its walls rise to the
  // coachroof (ZCR) forward and on to the hardtop's underside aft; a dark band of glazing all round (one material in
  // pass A), and the windscreen from the hardtop's front down to the coachroof
  const HW = 2.37, HYA = -6.58, ZCR = 5.20, Y_PANEL = -5.30, Y_WS = 0.60;
  const H0 = ringsFrom([[MAIN_DECK, 6.80, HYA, HW, 2.30], [4.39, 6.645, HYA, HW, 2.20], [ZCR, 5.67, HYA, HW, 1.60]], [MAIN_DECK, 4.39, 4.80, ZCR], { e: 2.2, rA: 0.30 });
  const HU = ringsFrom([[ZCR, 2.10, HYA, HW, 1.50], [6.00, 1.25, HYA, HW, 1.50], [6.25, 1.03, HYA, HW, 1.50]], [ZCR, 6.00, 6.25], { e: 2.2, rA: 0.30 });
  const P_H0 = H0[0].P, Y_HF = P_H0[0][1];
  // the hardtop's underside (A3: 4.147 over the DWL at u 0.10-0.15, 4.221 at 0.25-0.35, 4.30-4.33 at 0.40-0.53), and its
  // edge, down to the windscreen's head (6.00) at its front
  const zHU = mono([[-9.35, 5.940], [-8.01, 5.947], [-5.565, 6.021], [-3.123, 6.021], [-1.902, 6.100], [0.55, 6.130]]);
  const zHB = (y) => y <= 0.55 ? zHU(y) : lerp(zHU(0.55), 6.00, clamp01((y - 0.55) / 0.70));

  { // the decks, strip by strip; alongside the house they are its side decks, each from inside its wall to the lining
    const yd0 = yTr(MAIN_DECK) + 0.004, YS = new Set([yd0, HYA, Y_HF, Y_FD, yB - 0.03]);
    for (const d of [0.04, 0.08, 0.13, 0.19, 0.26, 0.34, 0.43, 0.53, 0.65, 0.80]) YS.add(yd0 + d);
    for (let y = -10.5; y < 11.5; y += 0.5) YS.add(y);
    for (let y = P_H0[NN][1]; y < Y_HF; y += 0.15) YS.add(y);
    for (let y = 11.5; y < yB - 0.03; y += 0.2) YS.add(y);
    const YD = [...YS].filter(y => y >= yd0 && y <= yB - 0.03).sort((a, b) => a - b);
    // teak as a ramp, not lines (rule 7), less the steps' well; the lining and the cap in the house's white
    const WELL = { x0: STEPS.x0, x1: STEPS.x1, y0: yd0 - 0.01, y1: STEPS.yTop };
    for (let i = 0; i + 1 < YD.length; i++) { const ya = YD[i], yb = YD[i + 1], fwd = ya >= Y_FD - 1e-9, za = fwd ? deckZ(ya) : MAIN_DECK, zb = fwd ? deckZ(yb) : MAIN_DECK;
      const xa = xin(ya, za), xb = xin(yb, zb), lvl = fwd ? 'foredeck' : 'main_deck';
      at(lvl, 'floor', 'deck');
      if (yb <= HYA + 1e-9 || ya >= Y_HF - 1e-9) putH([[-xa, ya, za], [xa, ya, za], [xb, yb, zb], [-xb, yb, zb]], [0, 0, 1], 'teak', TEAK_B, 0, [WELL]);
      else { const ha = Math.max(0, hwAt(P_H0, ya) - 0.04), hb = Math.max(0, hwAt(P_H0, yb) - 0.04);
        for (const sd of [-1, 1]) Q([[sd * ha, ya, za], [sd * xa, ya, za], [sd * xb, yb, zb], [sd * hb, yb, zb]], [0, 0, 1], 'teak', TEAK_B, 0); }
      for (const sd of [-1, 1]) {
        at(lvl, 'lining', 'lining'); Q([[sd * xa, ya, za], [sd * xb, yb, zb], [sd * xb, yb, fSz(yb)], [sd * xa, ya, fSz(ya)]], [-sd, 0, 0], 'cream', -0.25, 0);
        at(lvl, 'trim'); Q([[sd * xa, ya, fSz(ya)], [sd * sheerX(ya), ya, fSz(ya)], [sd * sheerX(yb), yb, fSz(yb)], [sd * xb, yb, fSz(yb)]], [0, 0, 1], 'cream', 0.3, 0); } }
    // the cap across the transom's top, open over the steps, and the riser of the step up to the foredeck
    at('main_deck', 'trim', 'lining'); { const xc = xin(yd0); putH([[-xc, yTT, ZTT], [xc, yTT, ZTT], [xc, yd0 + 0.10, ZTT], [-xc, yd0 + 0.10, ZTT]], [0, 0, 1], 'cream', 0.3, 0, [{ x0: WELL.x0, x1: WELL.x1, y0: yTT - 0.01, y1: yd0 + 0.11 }]); }
    at('foredeck', 'floor', 'deck'); { const xs = Math.min(xin(Y_FD, MAIN_DECK), xin(Y_FD)); Q([[-xs, Y_FD, MAIN_DECK], [xs, Y_FD, MAIN_DECK], [xs, Y_FD, deckZ(Y_FD)], [-xs, Y_FD, deckZ(Y_FD)]], [0, -1, 0], 'cream', -0.3, 0); }
  }

  // the house to the coachroof: its walls, then its glass: the side band (A3's bottoms; its top under the aft panel, full
  // height through the saloon, under the coachroof's edge forward), the front, and the aft bulkhead
  // C4: the aft bulkhead's doors, two glass leaves that part from the centreline and slide outboard, outside the fixed
  // glass either side (house.door); the bulkhead and its glass rise to the saloon's ceiling (Z_SAL), the wall's foot
  // under them kept as the sill
  const Z_SAL = 5.88, SALOON_AFT = [-1, 1].map(sd => door('saloon_aft_' + (sd < 0 ? 'port' : 'stbd'), 1, HYA, sd < 0 ? -0.99 : 0, sd < 0 ? 0 : 0.99, 3.80, Z_SAL, [0, -1],
    { kind: 'slide', dir: sd, travel: 0.95, out: 0.07, side: sd }));
  at('house', 'wall', 'house'); loftWalls(H0, 'mainhouse', 0); leafCut(SALOON_AFT, () => aftWall(H0, 'mainhouse', 0));
  const zGB = mono([[-6.6, 3.96], [-5.56, 3.966], [-3.12, 3.985], [-0.68, 4.022], [1.77, 4.077], [4.21, 4.114], [6.66, 4.225]]);
  at('house', 'wall', 'glass');
  glassOn(H0, HYA + 0.38, Y_PANEL, 3.94, 5.19, zGB, 0, 'glas', -0.45, 0.035); glassOn(H0, Y_PANEL, Y_WS, 3.94, ZCR, zGB, 0, 'glas', -0.45, 0.035);
  glassOn(H0, Y_WS, 99, 3.94, 4.98, zGB, 0, 'glas', -0.45, 0.035);
  leafCut(SALOON_AFT, () => Q([[-1.60, HYA - 0.03, 3.80], [1.60, HYA - 0.03, 3.80], [1.60, HYA - 0.03, Z_SAL], [-1.60, HYA - 0.03, Z_SAL]], [0, -1, 0], 'glas', -0.5, 0.035));
  // above the coachroof, under the hardtop: glass forward of the aft panel (the windscreen and its wings at the nose),
  // every face cut at the hardtop's underside
  leafCut(SALOON_AFT, () => { for (const f of capture(() => { loftWalls(HU, 'mainhouse', 0); aftWall(HU, 'mainhouse', 0); })) {
    const side = f.v.every(p => Math.abs(Math.abs(p[0]) - HW) < 1e-6), cyf = f.v.reduce((s, p) => s + p[1], 0) / f.v.length, nose = !side && cyf > HYA + 0.5;
    (side ? splitBy(f.v, (p) => p[1] - Y_PANEL) : [null, f.v]).forEach((v, k) => { if (!v) return; const c = splitBy(v, (p) => p[2] - zHB(p[1]))[0]; if (!c) return;
      const glass = nose || (side && k === 1), g = Object.assign({}, f, { v: c, mat: glass ? 'glas' : f.mat, b: glass ? -0.45 : f.b });
      PART.set(g, nose ? 'wing' : glass ? 'glass' : 'house'); TG.push(g); }); } });
  for (const D of SALOON_AFT) D.faces = D.faces.filter(f => f.mat === 'glas');        // the bulkhead's pieces in the opening go: the leaves are the glass
  // the glazing's few, firm mullions (B1): on the band up to the coachroof's edge, and on up to the hardtop's underside
  // aft of the windscreen; and the windscreen's one post, on the centreline (C1: its wings' corners run frameless)
  const MULLIONS = [-5.30, -3.33, -1.37, 2.80, 4.90];
  at('house', 'wall', 'mullion');
  for (const y of MULLIONS) { patchOn(H0, y - 0.05, y + 0.05, zGB(y), y < Y_WS ? ZCR : 4.98, 'steel', -1.6, 0.012, 0.04);
    if (y < Y_WS) for (const sd of [-1, 1]) { const x = sd * (HW + 0.012); Q([[x, y - 0.05, ZCR], [x, y + 0.05, ZCR], [x, y + 0.05, zHB(y + 0.05)], [x, y - 0.05, zHB(y - 0.05)]], [sd, 0, 0], 'steel', -1.6, 0.04); } }
  { const post = (j, sds) => { for (let k = 0; k + 1 < HU.length; k++) for (const sd of sds) { const A = HU[k], B2 = HU[k + 1], n = pnorm(A.P, j);
      const P = (R, e) => { const m = pnorm(R.P, j), p = R.P[j]; return [sd * (p[0] + e * m[1] + 0.012 * m[0]), p[1] - e * m[0] + 0.012 * m[1], R.z]; };
      const v = splitBy([P(A, -0.05), P(A, 0.05), P(B2, 0.05), P(B2, -0.05)], (p) => p[2] - zHB(p[1]))[0]; if (v) Q(v, [sd * n[0], n[1], 0], 'steel', -1.6, 0.04); } };
    post(0, [1]); }
  // the coachroof: the house's top ring less the upper house's footprint, in teak, and its two sunpads
  at('house', 'roof', 'house');
  { const A = ringAt(H0, ZCR).P, B2 = HU[0].P;
    for (let j = 0; j + 1 < NP; j++) for (const sd of [-1, 1]) Q([[sd * A[j][0], A[j][1], ZCR], [sd * A[j + 1][0], A[j + 1][1], ZCR], [sd * B2[j + 1][0], B2[j + 1][1], ZCR], [sd * B2[j][0], B2[j][1], ZCR]], [0, 0, 1], 'teak', TEAK_B, 0); }
  at('house', 'roof', 'fit');
  for (const sd of [-1, 1]) lidOn(sd < 0 ? -1.15 : 0.15, sd < 0 ? -0.15 : 1.15, 2.40, 3.90, 0.06, () => ZCR, 'pad', -0.4);

  // ================================= THE HARDTOP (top) =================================
  // from y -9.35 to the windscreen's head, 5.50 m wide; its top 6.23 at its aft edge, 6.34 to 6.42 forward (A3); a
  // sun deck on level top (decision 11's lean); its underside is the house's roof
  const HT = { yA: -9.35, W: 2.75 };
  const PB_ = outline({ yF: 1.25, yA: HT.yA, W: HT.W, N: 1.95 }, { e: 2.2, rA: 0.90 }), PT_ = outline({ yF: 0.61, yA: HT.yA + 0.05, W: HT.W - 0.07, N: 1.70 }, { e: 2.2, rA: 0.85 });
  const zHT = mono([[HT.yA + 0.05, 6.23], [-8.9, 6.31], [-8.0, 6.336], [-5.56, 6.373], [-3.12, 6.392], [-0.68, 6.41], [0.61, 6.42]]);
  // the sun deck: teak inside a white rim 0.15 wide, less the ladder's hatch (A3's plan: the ladder's foot at u 0.192 to
  // 0.227, v +0.199 to +0.316), 0.90 across and 0.76 back about the ladder (decision 11, the desk's size of 10-27)
  const HATCH_T = { x0: 1.13, x1: 2.03, y0: -7.66, y1: -6.90 };
  const PI_ = PT_.map((p, j) => { const n = pnorm(PT_, j), q = [p[0] - 0.15 * n[0], p[1] - 0.15 * n[1]]; if (j === 0 || j === NP - 1) q[0] = 0; return q; });
  at('top', 'floor', 'hardtop');
  for (let j = 0; j + 1 < NP; j++) { const a = PT_[j], c = PT_[j + 1], ai = PI_[j], ci = PI_[j + 1];
    for (const sd of [-1, 1]) Q([[sd * a[0], a[1], zHT(a[1])], [sd * c[0], c[1], zHT(c[1])], [sd * ci[0], ci[1], zHT(ci[1])], [sd * ai[0], ai[1], zHT(ai[1])]], [0, 0, 1], 'cream', 0.25, 0); }
  slab(PI_, zHT, [0, 0, 1], 'teak', TEAK_B, [HATCH_T]);
  // its fascia, rounded at its foot and its top (C1): three bands from the underside's edge up to the rim, the two
  // middle rings stood 0.05 out along the outline's normal
  at('top', 'trim');
  { const TB = [0, 0.35, 0.75, 1], BU = [0, 0.05, 0.05, 0];
    const ring = (k, i) => { const B = PB_[k], T = PT_[k], m = pnorm(PB_, k), t = TB[i], ex = (k === 0 || k === NP - 1) ? 0 : BU[i];
      return [lerp(B[0], T[0], t) + ex * m[0], lerp(B[1], T[1], t) + BU[i] * m[1], lerp(zHB(B[1]), zHT(T[1]), t)]; };
    for (let j = 0; j + 1 < NP; j++) { const nn = pnorm(PB_, j), bb = 0.15 + 0.35 * Math.max(0, nn[1]) - 0.4 * Math.max(0, -nn[1]);
      for (let i = 0; i + 1 < TB.length; i++) { const v = [ring(j, i), ring(j + 1, i), ring(j + 1, i + 1), ring(j, i + 1)];
        for (const sd of [-1, 1]) Q(v.map(p => [sd * p[0], p[1], p[2]]), [sd * nn[0], nn[1], 0.3], 'cream', bb, 0); } } }
  { const { x0, x1, y0, y1 } = HATCH_T, E = (x, y, k) => [x, y, k ? zHT(y) : zHB(y)];   // the hatch's four sides
    Q([E(x0, y0, 0), E(x1, y0, 0), E(x1, y0, 1), E(x0, y0, 1)], [0, 1, 0], 'cream', -0.9, 0); Q([E(x0, y1, 0), E(x1, y1, 0), E(x1, y1, 1), E(x0, y1, 1)], [0, -1, 0], 'cream', -0.9, 0);
    Q([E(x0, y0, 0), E(x0, y1, 0), E(x0, y1, 1), E(x0, y0, 1)], [1, 0, 0], 'cream', -0.9, 0); Q([E(x1, y0, 0), E(x1, y1, 0), E(x1, y1, 1), E(x1, y0, 1)], [-1, 0, 0], 'cream', -0.9, 0); }
  // its underside, the house's roof, in warm wood (C1); its down-lights are left out, since no view from above sees it
  at('house', 'roof'); slab(PB_, zHB, [0, 0, -1], 'wood', -1.6, [HATCH_T]);
  // its two blades, raked top forward (A3: the foot at f 0.165, the head at f 0.22): each a six-sided section, its chord
  // fore and aft, grown evenly from the foot to the head, and flared onto the deck and up into the underside (C1): the
  // foot's chord 1.4 times and its thickness 1.8 times over its lowest 0.15, the head's 1.6 and 1.9 times over its top
  // 0.20, the head drawn 0.04 inboard. Each stands against the house's side (pass E's ruling 4): its centre at x ±2.34,
  // 0.28 inboard of A3's, so that the side deck outboard of it keeps the walker's width. It ends at the house's skin, as
  // the gear ends at the hull's: about its aft corners the house's walls are the same at every height (H0 and HU), its
  // side, the corner's five chords and its aft face, each a half-plane (HP: + on the house's side). A face of a blade
  // they cut is drawn as its pieces outside them, each convex; a face they do not cut, as it is
  at('main_deck', 'tall', 'pillar');
  let BLADE = null;   // the starboard blade's stations, uncut (walk(): the side decks stop at it)
  { const hex = (c, t) => [[0, c / 2], [t / 2, c / 4], [t / 2, -c / 4], [0, -c / 2], [-t / 2, -c / 4], [-t / 2, c / 4]], TS = [0, 0.05, 0.15, 0.80, 0.93, 1], zH = zHB(-6.30) + 0.01;
    const HP = (sd) => [[NN, NN + NSD]].concat(Array.from({ length: NCR }, (_, i) => [NN + NSD + i, NN + NSD + i + 1]), [[NC - 1, NP - 1]]).map(([i, j]) => {
      const a = P_H0[i], b = P_H0[j], ex = b[0] - a[0], ey = b[1] - a[1], m = Math.hypot(ex, ey); return (p) => ((sd * p[0] - a[0]) * ey - (p[1] - a[1]) * ex) / m; });
    for (const sd of [-1, 1]) { const H = HP(sd), RG = TS.map(t => { const fF = Math.pow(1 - clamp01(t / 0.15), 2), fH = Math.pow(clamp01((t - 0.80) / 0.20), 2);
        const c = lerp(0.45, 0.70, t) * (1 + 0.4 * fF + 0.6 * fH), th = lerp(0.10, 0.156, t) * (1 + 0.8 * fF + 0.92 * fH), C = [sd * (2.34 - 0.04 * fH), lerp(-7.64, -6.30, t), lerp(MAIN_DECK, zH, t)];
        return { C, P: hex(c, th).map(p => [C[0] + p[0], C[1] + p[1], t === 1 ? zHB(C[1] + p[1]) + 0.01 : C[2]]) }; });
      if (sd > 0) BLADE = RG;
      for (let r = 0; r + 1 < RG.length; r++) { const A = RG[r].P, B2 = RG[r + 1].P, mid = v_mul(v_add(RG[r].C, RG[r + 1].C), 0.5);
        for (let k = 0; k < 6; k++) { const k2 = (k + 1) % 6, q = [A[k], A[k2], B2[k2], B2[k]], c = v_mul(v_add(v_add(q[0], q[1]), v_add(q[2], q[3])), 0.25), outs = [];
          let rest = q; for (const g of H) { if (!rest) break; const s = splitBy(rest, g); if (s[0]) outs.push(s[0]); rest = s[1]; }
          for (const v of rest ? outs : [q]) Q(v, v_sub(c, mid), 'cream', 0.1, 0); } } } }
  // the sun deck's rail (A3: its top 4.869 over the DWL, over f 0.17 to 0.48): a top tube on five posts a side
  at('top', 'fit', 'rail');
  { const YR = [-7.52]; for (let y = -7.0; y < -1.09; y += 0.5) YR.push(y); YR.push(-1.09, -0.8, -0.5, -0.25, 0.05);
    const ZR = DWL + 4.869, X = (y) => hwAt(PT_, y) - 0.08;
    for (const sd of [-1, 1]) { for (let i = 0; i + 1 < YR.length; i++) tubeQ([sd * X(YR[i]), YR[i], ZR], [sd * X(YR[i + 1]), YR[i + 1], ZR], 0.025, 0.025, 'steel', 0.3);
      for (const y of [-7.52, -6.0, -4.35, -2.2, 0.05]) tubeQ([sd * X(y), y, zHT(y)], [sd * X(y), y, ZR], 0.02, 0.02, 'steel', 0.3); } }
  // its two loungers, their backs forward, and its table
  at('top', 'fit', 'fit');
  for (const sd of [-1, 1]) { const x0 = sd < 0 ? -1.10 : 0.30, x1 = sd < 0 ? -0.30 : 1.10, zf = (y) => zHT(y) - 0.01;
    lidOn(x0, x1, -4.90, -3.10, 0.15, zf, 'pad', -0.4); lidOn(x0, x1, -3.10, -2.90, 0.31, zf, 'pad', -0.2); }
  { const z0 = zHT(-6.15); boxQ(-0.35, 0.35, -6.45, -5.85, z0 + 0.40, z0 + 0.44, 'teak', TEAK_B + 0.3, 0, null, 'b'); cylZ(0, -6.15, z0, z0 + 0.40, 0.05, 6, 'steel', 0, null); }
  // its ladder from the aft deck's starboard quarter up through the hatch, raked top forward, and the grab posts at its
  // head (decision 11: a climb of kind ladder); no bar across the posts' heads (pass E): the climber tops out between
  // them, onto the sun deck
  at('main_deck', 'fit', 'ladder');
  const SUN_LADDER = { x: [1.30, 1.86], y0: -7.85, k: 0.3436, zt: 6.38, pitch: 0.28, yPost: -6.88 };
  { const L = SUN_LADDER, yz = (z) => L.y0 + L.k * (z - MAIN_DECK), zt = L.zt;
    for (const x of L.x) tubeQ([x, yz(MAIN_DECK), MAIN_DECK], [x, yz(zt), zt], 0.025, 0.025, 'steel', 0.3);
    for (let z = MAIN_DECK + L.pitch; z < zt - 0.05; z += L.pitch) tubeQ([L.x[0], yz(z), z], [L.x[1], yz(z), z], 0.018, 0.018, 'steel', 0.2);
    at('top', 'fit'); const yg = L.yPost, zg = zHT(yg);
    for (const x of L.x) tubeQ([x, yg, zg], [x, yg, zg + 0.85], 0.022, 0.022, 'steel', 0.3); }

  // ================================= THE MAST (rigging) =================================
  // a plain raked pylon on the hardtop, its head 6.59 over the DWL (A3); its crossbar near 6.0 with a light at each end,
  // the radar's bar on its face, the masthead light and a whip reaching 6.832 at f 0.419 (A3), each a plain shape
  at('rigging', 'rig', 'mast');
  const MAST = { zT: DWL + 6.591, base: [[0.30, -2.00], [0.30, 0.07], [-0.30, 0.07], [-0.30, -2.00]], top: [[0.15, -1.10], [0.15, -0.30], [-0.15, -0.30], [-0.15, -1.10]] };
  { const zT = MAST.zT, base = MAST.base.map(p => [p[0], p[1], zHT(p[1])]), top = MAST.top.map(p => [p[0], p[1], zT]);
    for (let k = 0; k < 4; k++) { const k2 = (k + 1) % 4, q = [base[k], base[k2], top[k2], top[k]]; Q(q, [(q[0][0] + q[1][0]) / 2, (q[0][1] + q[1][1]) / 2 + 0.965, 0.2], 'cream', 0.1, 0); }
    Q(top, [0, 0, 1], 'cream', 0.3, 0); }
  at('rigging', 'fit', 'fit');
  { const zT = MAST.zT, zX = DWL + 6.0, edge = (y0, z0, y1) => lerp(y0, y1, (zX - z0) / (zT - z0));
    const yX = (edge(-2.00, zHT(-2.00), -1.10) + edge(0.07, zHT(0.07), -0.30)) / 2, hX = lerp(0.30, 0.15, (zX - zHT(-2.00)) / (zT - zHT(-2.00)));
    MAST.yX = yX;   // walk(): the sun deck keeps clear of the crossbar's lights
    for (const sd of [-1, 1]) { tubeQ([sd * hX, yX, zX], [sd * 0.55, yX, zX], 0.03, 0.03, 'cream', 0.1); boxQ(sd < 0 ? -0.60 : 0.50, sd < 0 ? -0.50 : 0.60, yX - 0.05, yX + 0.05, zX, zX + 0.10, 'steel', 0); }
    boxQ(-0.08, 0.08, -0.15, 0.02, 7.38, 7.45, 'steel', 0); boxQ(-0.60, 0.60, -0.04, 0.08, 7.45, 7.53, 'cream', 0.2);
    boxQ(-0.05, 0.05, -0.75, -0.65, zT, zT + 0.08, 'steel', 0); tubeQ([0, -1.10, zT], [0, -1.44, DWL + 6.832], 0.015, 0.015, 'steel', 0.3); }
  // the dome on the sun deck, forward of the mast (A3: 4.91 to 5.28 over the DWL, at f 0.47 to 0.51; set 0.21 forward
  // of that, so that it clears the mast's face), on its pedestal; and the flagstaff at the hardtop's aft end, with no flag
  at('top', 'fit', 'fit');
  cylZ(0, 0.45, zHT(0.45) - 0.01, DWL + 4.91, 0.12, 8, 'cream', 0, null); domeZ(0, 0.45, DWL + 4.91, 0.43, 0.08, 12, 'cream', 0.1, 0.29);
  tubeQ([0, -9.345, 6.05], [0, -9.76, 7.19], 0.018, 0.018, 'steel', 0.3);

  // ================================= THE FITTINGS =================================
  // the bulwark's rail all round (A3: its top 0.67 above the sheer forward, 0.69 aft; C1: rising to 0.74 over the
  // last metre to the stem's head): a firm top rail on few stanchions, across the transom over the garage's mouth, open
  // over the steps and down their inboard side; and under it a lower rail at 0.40 of its height wherever it runs (C1),
  // sweeping up to meet it at the stem's head, on every other point amidships
  at('main_deck', 'fit', 'rail');
  // the top rail in plan: its line, its tube, its points down the side (ys), and line, its starboard half from the
  // transom by the steps' slot round the stern's corner to the stem (the port half its mirror); walk() keeps off it
  const RAIL = { x: (y) => sheerX(y) - 0.07, r: 0.028, ys: [-11.60, -11.55, -11.50, -11.45, -11.40, -11.30, -11.15, -11.0] };
  for (let y = -10.4; y < 9.5; y += 0.6) RAIL.ys.push(y); for (let y = 9.5; y < 12.3; y += 0.3) RAIL.ys.push(y); RAIL.ys.push(12.30, 12.45, 12.55);
  RAIL.line = [[STEPS.x1 + 0.05, -11.60]].concat(RAIL.ys.map(y => [RAIL.x(y), y]), [[0.16, 12.61], [0, 12.63]]);
  { const RX = RAIL.x, hS = (y) => 0.67 + 0.02 * clamp01((-9 - y) / 2.6) + 0.07 * smooth(clamp01((y - 11.6) / 1.0)), ZS = ZTT + 0.69, RP = (sd, y) => [sd * RX(y), y, fSz(y), fSz(y) + hS(y)];
    const YSIDE = RAIL.ys;
    const PL = [[1.15, STEPS.yTop - 0.15, MAIN_DECK, ZS], [1.15, -11.60, ZTT, ZS]].concat(YSIDE.map(y => RP(-1, y)),
      [[-0.16, 12.61], [0, 12.63], [0.16, 12.61]].map(([x, y]) => [x, y, fSz(y), fSz(y) + hS(y)]), YSIDE.slice().reverse().map(y => RP(1, y)), [[STEPS.x1 + 0.05, -11.60, ZTT, ZS]]);
    const lvl = (y) => y < Y_FD ? 'main_deck' : 'foredeck', post = (p) => { at(lvl(p[1]), 'fit'); tubeQ([p[0], p[1], p[2]], [p[0], p[1], p[3]], 0.022, 0.022, 'steel', 0.3); };
    const run = (P, r) => { for (let i = 0; i + 1 < P.length; i++) { const a = P[i], c = P[i + 1]; at(lvl((a[1] + c[1]) / 2), 'fit'); tubeQ(a, c, r, r, 'steel', 0.3); } };
    run(PL.map(p => [p[0], p[1], p[3]]), RAIL.r);
    const fr = (y) => 0.40 + 0.60 * smooth(clamp01((y - 11.6) / 1.03)), lo = (p) => [p[0], p[1], p[2] + fr(p[1]) * (p[3] - p[2])];
    const YLO = YSIDE.filter((y, i) => y < -10.9 || y > 9.45 || i % 2 === 0);
    run([PL[0], PL[1]].concat(YLO.map(y => RP(-1, y))).map(lo), 0.024); run([PL[PL.length - 1]].concat(YLO.map(y => RP(1, y))).map(lo), 0.024);
    [PL[0], PL[1], PL[PL.length - 1], RP(-1, -11.60), RP(1, -11.60)].forEach(post);
    for (const y of [-11.30, -9.228, -6.786, -4.344, -1.902, 0.54, 2.982, 5.424, 7.866, 10.308, 11.9]) for (const sd of [-1, 1]) post(RP(sd, y)); }
  // the aft deck's two tables (A3's plan: u 0.094 to 0.171), each with two armchairs (C1: teak frames, white cushions,
  // one at each end, facing it), and its hatch (u 0.190 to 0.209); the foredeck's hatch (u 0.780 to 0.801, opened aft
  // to u 0.772 by pass E's ruling 1), the crew's way in (decision 8)
  at('main_deck', 'fit', 'fit');
  boxQ(-1.33, -0.53, -9.77, -8.97, 4.45, 4.49, 'teak', TEAK_B + 0.3, 0, null, 'b'); cylZ(-0.93, -9.37, MAIN_DECK, 4.45, 0.06, 6, 'steel', 0, null);
  cylZ(0.41, -9.37, 4.45, 4.49, 0.45, 12, 'teak', TEAK_B + 0.3); cylZ(0.41, -9.37, MAIN_DECK, 4.45, 0.06, 6, 'steel', 0, null);
  for (const x of [-0.93, 0.41]) for (const sd of [-1, 1]) { const Z = MAIN_DECK, cy = -9.37 + sd * 1.0, yo = cy + sd * 0.29, yi = cy + sd * 0.21, yf = cy - sd * 0.29, tb = TEAK_B + 0.3;
    boxQ(x - 0.29, x + 0.29, cy - 0.29, cy + 0.29, Z, Z + 0.42, 'teak', tb, 0, 'pad', 'b');
    boxQ(x - 0.29, x + 0.29, Math.min(yo, yi), Math.max(yo, yi), Z + 0.42, Z + 0.80, 'teak', tb, 0, null, sd < 0 ? 'bf' : 'ba');
    Q([[x - 0.29, yi, Z + 0.42], [x + 0.29, yi, Z + 0.42], [x + 0.29, yi, Z + 0.80], [x - 0.29, yi, Z + 0.80]], [0, -sd, 0], 'pad', 0, 0);
    for (const ax of [-1, 1]) { const xa = x + ax * 0.29, xb = x + ax * 0.23; boxQ(Math.min(xa, xb), Math.max(xa, xb), Math.min(yi, yf), Math.max(yi, yf), Z + 0.42, Z + 0.62, 'teak', tb, 0, null, sd < 0 ? 'ba' : 'bf'); } }
  lidOn(-0.289, 0.215, -7.905, -7.42, 0.03, () => MAIN_DECK, 'cream', 0);
  at('foredeck', 'fit', 'fit');
  // the foredeck's hatch over the crew's mess, and the crew's ladder under it (decision 8; walk(): the crew's climb). The
  // hatch 0.79 x 0.75, which every build of kit 10.4 fits (pass E's ruling 1): its forward edge kept, opened aft over the
  // climber; the ladder's rails 0.46 apart, so that 0.42 m is left past the starboard one (ruling 9)
  const HATCH_F = { x0: -0.395, x1: 0.395, y0: 6.926, y1: 7.676 }, CREW_LADDER = { y: 7.62, half: 0.23, pitch: 0.28 };
  lidOn(HATCH_F.x0, HATCH_F.x1, HATCH_F.y0, HATCH_F.y1, 0.05, deckZ, 'cream', 0);
  // the bow: the windlass and its chain to the roller, the roller over the stem, the anchor at its tip in its pocket;
  // neither stands forward of the stem's head, so her loa stays 25.50 (decision 2)
  { const y = 11.0, z0 = deckZ(y);
    boxQ(-0.18, 0.18, y - 0.20, y + 0.20, z0 - 0.02, z0 + 0.12, 'steel', 0, 0, null, 'b'); cylZ(0, y, z0 + 0.12, z0 + 0.28, 0.13, 8, 'iron', 0.1);
    tubeQ([0, y + 0.13, z0 + 0.20], [0, 12.45, 4.36], 0.02, 0.02, 'iron', 0); }
  boxQ(-0.10, 0.10, 12.30, 12.75, deckZ(12.30), 4.40, 'steel', 0.1, 0, null, 'b'); boxQ(-0.10, 0.10, 12.50, 12.70, 3.95, 4.20, 'steel', 0, 0, null, 'a');
  at('hull', 'shell', 'fit'); hullPatch(12.25, 12.62, 3.85, 4.25, 'blk', -0.3, 0.004, 0.3, 0.05, 4);
  // the cleats, three a side, and a fairlead in the bulwark by each
  const CLEATS = [];
  for (const y of [-10.9, 0.9, 10.6]) { const deck = y < Y_FD ? 'main_deck' : 'foredeck', z = deckZ(y);
    at(deck, 'fit', 'fit');
    for (const sd of [-1, 1]) { const x = sd * (xin(y) - 0.12);
      boxQ(x - 0.04, x + 0.04, y - 0.07, y + 0.07, z, z + 0.05, 'steel', 0.1, 0, null, 'b'); boxQ(x - 0.025, x + 0.025, y - 0.16, y + 0.16, z + 0.05, z + 0.08, 'steel', 0.2, 0, null, 'b');
      CLEATS.push({ deck, at: [r3(x), r3(y), r3(z)] }); }
    at('hull', 'shell', 'fit'); hullPatch(y - 0.12, y + 0.12, fSz(y) - 0.30, fSz(y) - 0.17, 'blk', -0.3, 0.004, 0.5, 0.05, 4); }
  // her ports (A3: 11 a side in 7 groups, ovals and slits; each one's ends and heights over the DWL, B1), dark glass
  // laid on the skin; their ends count from the transom's top, as A3's stations do. Ports 1 and 2 (the garage's) and 8
  // and 9 (the engine room's) are grilles (C1): a light oval round a dark slot (their slats, under a pixel, read as one)
  const PORTS = [[-8.672, -8.188, 1.02, 1.24, 0.5, 6], [-7.893, -7.375, 1.02, 1.24, 0.5, 6], [-5.524, -5.116, 1.02, 1.09, 0.3, 4], [-4.744, -4.339, 1.02, 1.09, 0.3, 4],
    [-1.783, -1.338, 1.055, 1.13, 0.3, 4], [-0.969, -0.562, 1.055, 1.13, 0.3, 4], [0.142, 0.513, 1.055, 1.13, 0.3, 4], [3.216, 3.734, 1.13, 1.39, 0.5, 6], [3.992, 4.510, 1.13, 1.39, 0.5, 6],
    [6.732, 7.104, 1.13, 1.24, 0.3, 4], [8.251, 8.584, 1.13, 1.24, 0.3, 4]], GRILLES = [0, 1, 7, 8];
  at('hull', 'shell', 'port');
  PORTS.forEach(([y0, y1, z0, z1, ends, n], i) => { const za = DWL + z0, zb = DWL + z1;
    if (GRILLES.indexOf(i) < 0) { hullPatch(y0, y1, za, zb, 'glas', -0.4, 0.006, ends, 0.03, n); return; }
    hullPatch(y0, y1, za, zb, 'steel', 0, 0.006, ends, 0.03, n);
    hullPatch(lerp(y0, y1, 0.15), lerp(y0, y1, 0.85), lerp(za, zb, 0.35), lerp(za, zb, 0.65), 'iron', 0, 0.010, 0.3, 0.05, 4); });
  // the transom's two quarter ports (C1), outboard of the garage's mouth and of the steps' slot: small ovals of dark
  // glass, 0.22 by 0.15, laid on its raked plane 0.52 under its top
  for (const sd of [-1, 1]) { const zc = ZTT - 0.52, v = [];
    for (let k = 0; k < 10; k++) { const a = k / 10 * 2 * Math.PI, z = zc + 0.075 * Math.sin(a); v.push([sd * 2.00 + 0.11 * Math.cos(a), yTr(z) - 0.006, z]); }
    Q(v, [0, -1, -KTR], 'glas', -0.4, 0.03); }
  // the rubbing strake: a steel band 0.08 to 0.13 under the sheer, 0.04 proud, from the transom's corner to the stem
  at('hull', 'trim', 'strake');
  { const y0 = yTr(3.70) + 0.01, YR = [0, 0.05, 0.10, 0.17, 0.25, 0.35, 0.47].map(d => y0 + d);
    for (let y = YR[YR.length - 1] + 0.6; y < 8; y += 0.6) YR.push(y); for (let y = 8; y < 12.60 - 1e-6; y += 0.25) YR.push(y); YR.push(12.60);
    const P = (sd, y, k, o) => { const z = fSz(y) - (k ? 0.08 : 0.13); return [sd * (halfAtZ(Math.max(y, yT1), z) + o), y, z]; };
    for (const sd of [-1, 1]) {
      for (let i = 0; i + 1 < YR.length; i++) { const a = YR[i], c = YR[i + 1];
        Q([P(sd, a, 0, 0.04), P(sd, c, 0, 0.04), P(sd, c, 1, 0.04), P(sd, a, 1, 0.04)], [sd, 0, 0], 'strake', -0.6, 0.02);
        Q([P(sd, a, 1, 0.04), P(sd, c, 1, 0.04), P(sd, c, 1, 0), P(sd, a, 1, 0)], [0, 0, 1], 'strake', -0.3, 0); }
      for (const [y, d] of [[YR[0], -1], [YR[YR.length - 1], 1]]) Q([P(sd, y, 0, 0), P(sd, y, 0, 0.04), P(sd, y, 1, 0.04), P(sd, y, 1, 0)], [0, d, 0], 'strake', -0.3, 0); } }

  // ================================= THE RUNNING GEAR (hull, part) =================================
  // two shafts, each from the skin aft to its propeller, a strut ahead of the propeller and a rudder aft of it, at
  // modest detail. No buried faces (charter §1): each solid ends at the skin it meets; the rings put their corners at
  // k·45° about the axis, so that no face points straight up inside one; the blades are single faces lit from either
  // side; the rudder is open at the skin and closed at its foot
  const GEAR = { x: 1.10, yP: -9.20, zP: 0.65, slope: 0.158, rP: 0.45, rS: 0.05, yStrut: -8.55, yRudder: [-10.55, -10.0], zRudder: 0.35, blades: 4 };
  at('hull', 'part', 'gear');
  { const ax = v_norm([0, 1, GEAR.slope]), u = [1, 0, 0], w = v_cross(u, ax), zS = (y) => GEAR.zP + (y - GEAR.yP) * GEAR.slope;
    const ring = (C, r) => [0, 1, 2, 3, 4, 5, 6, 7].map(k => { const a = k * Math.PI / 4; return v_add(C, v_add(v_mul(u, Math.cos(a) * r), v_mul(w, Math.sin(a) * r))); });
    const dirAt = (k) => { const a = (k + 0.5) * Math.PI / 4; return v_add(v_mul(u, Math.cos(a)), v_mul(w, Math.sin(a))); };
    for (const sd of [-1, 1]) { const X = sd * GEAR.x, at3 = (y) => [X, y, zS(y)];
      // the hub, and the shaft from its face forward to the skin
      const HF = ring(at3(-9.10), 0.10), HA = ring(at3(-9.32), 0.07), G0 = ring(at3(-9.10), GEAR.rS), E = G0.map(p => toSkin(p, ax, (9.10 - 4.0) / ax[1]));
      for (let k = 0; k < 8; k++) { const k2 = (k + 1) % 8; Q([HF[k], HF[k2], HA[k2], HA[k]], dirAt(k), 'gear', 0, 0); Q([G0[k], G0[k2], E[k2], E[k]], dirAt(k), 'gear', 0.2, 0); }
      Q(HF.slice(), ax, 'gear', 0, 0); Q(HA.slice(), v_mul(ax, -1), 'gear', 0, 0);
      // the strut, from the shaft's top up to the skin; the rudder, from its foot up to the skin
      const fin = (x0, x1, y0, y1, zb, closed) => { const B = [[x0, y0], [x1, y0], [x1, y1], [x0, y1]].map(([x, y]) => [x, y, zb(y)]), T = B.map(p => toSkin(p, [0, 0, 1], 1.5));
        for (let k = 0; k < 4; k++) { const k2 = (k + 1) % 4, m = [(B[k][0] + B[k2][0]) / 2 - (x0 + x1) / 2, (B[k][1] + B[k2][1]) / 2 - (y0 + y1) / 2, 0]; Q([B[k], B[k2], T[k2], T[k]], m, 'gear', 0.1, 0); }
        if (closed) Q(B, [0, 0, -1], 'gear', -1.2, 0); };
      fin(X - 0.025, X + 0.025, GEAR.yStrut - 0.125, GEAR.yStrut + 0.125, (y) => zS(y) + 0.035, false);
      fin(X - 0.035, X + 0.035, GEAR.yRudder[0], GEAR.yRudder[1], () => GEAR.zRudder, true);
      // the propeller: four blades pitched 30°, the port one turned the other way
      const C = at3(GEAR.yP);
      for (let i = 0; i < GEAR.blades; i++) { const ph = Math.PI / 4 + i * Math.PI / 2, rad = v_add(v_mul(u, Math.cos(ph)), v_mul(w, Math.sin(ph))), tan = v_add(v_mul(u, -Math.sin(ph)), v_mul(w, Math.cos(ph)));
        const cd = v_add(v_mul(tan, Math.cos(30 * DEG) * sd), v_mul(ax, Math.sin(30 * DEG))), P = (r, c) => v_add(C, v_add(v_mul(rad, r), v_mul(cd, c)));
        Q([P(0.08, -0.08), P(0.08, 0.08), P(GEAR.rP, 0.12), P(GEAR.rP, -0.12)], v_cross(rad, cd), 'gear', -1, 0); } } }

  // ================================= THE ROOMS (pass C): ADR 0048's interior =================================
  // Tagged mesh in IF (it true): the house's rooms (lv house) and the lower deck's (lv below). render() draws IF only
  // under a cutaway, and renderCut(dir, key) draws the key's shown level over the rest, so that her plain exterior is
  // unchanged. Every face keeps inside the shell (fitCheck): the lining LIN inside the house's walls, and inside the
  // hull's skin below the deck
  const LIN = 0.10;
  // the house's lining at z: its ring there (its ring at MAIN_DECK under the deck: the lining runs straight down to the
  // dining's sole) moved LIN in along its normal; hIn(y, z) its half-breadth at y
  const insetP = (P, d) => P.map((p, j) => { if (j === 0) return [0, p[1] - d]; if (j === NP - 1) return [0, p[1] + d]; const n = pnorm(P, j); return [Math.max(0, p[0] - d * n[0]), p[1] - d * n[1]]; });
  const houseIn = (z) => insetP(ringAt(H0, clamp(z, MAIN_DECK, ZCR)).P, LIN), hIn = (y, z) => hwAt(houseIn(z), y);
  let HELM = null, WHEEL_GEO = null, STAIR = null;
  // the rooms' records (C4, for pass E's walk and the review): ROOMS, the berths (BEDS), the openings in her partitions
  // (IDOORS, each with its leaf), the flights (IFLIGHTS) and the spiral stair (STAIR)
  const ROOMS = [], BEDS = [], IDOORS = [], IFLIGHTS = [];
  {
    TG = IF; cIT = true;
    // ---- the helm (C3): at the house's forward end, on the dining's level (decisions 9 and 13), after its picture in
    // plain shapes. The console runs from lining to lining, its aft face at YC: a wide dark dash, its lip over the aft
    // face, rising to the bezel's foot and level from there to the windows; two large screens side by side in one
    // bezel (plain panes: no text, figure, dial or readout); a compass dome before them; the wheel, a warm wood rim on
    // a steel hub and three spokes, raked as a car's is (its axis aft and up, at the helmsman); a lever box to
    // starboard and a small stick to port; the console's cheeks, either end, in warm wood ----
    at('house', 'fit', 'room');
    const SH = DINING_SOLE, YC = 5.30, YK = 5.65, ZL = 4.10, ZD = 4.20, ZK = 4.26;
    const zDash = (y) => lerp(ZL, ZD, clamp01((y - YC) / (YK - YC)));
    const XK = hIn(YK, ZK) - 0.005, XCH = XK - 0.20;            // the cheeks: from the lining in to XCH, aft of YK
    // the dash's top, in strips 0.05 deep from the lip to the lining's front: between the cheeks aft of YK, out to the
    // lining forward of it
    { const yT = houseIn(ZD)[0][1], YS = []; for (let y = YC; y < yT - 0.025; y = r3(y + 0.05)) YS.push(y); YS.push(yT);
      for (let i = 0; i + 1 < YS.length; i++) { const ya = YS[i], yb = YS[i + 1], za = zDash(ya), zb = zDash(yb);
        const ha = ya < YK - 1e-6 ? XCH : hIn(ya + 1e-4, za) - 0.005, hb = yb < YK + 1e-6 ? XCH : Math.max(0, hIn(yb - 1e-4, zb) - 0.005);
        Q(nodup([[-ha, ya, za], [ha, ya, za], [hb, yb, zb], [-hb, yb, zb]]), [0, 0, 1], 'dash', -1.8, 0); } }
    Q([[-XCH, YC, SH], [XCH, YC, SH], [XCH, YC, ZL], [-XCH, YC, ZL]], [0, -1, 0], 'dash', 0.5, 0);
    // the cheeks: each from the lining in to XCH, its top over the dash; the lining's side is the lining's
    for (const sd of [-1, 1]) { const y0 = YC - 0.02, y1 = YK, w0 = hIn(y0, ZK) - 0.005, X = (x) => sd * x;
      Q([[X(XCH), y0, ZK], [X(w0), y0, ZK], [X(XK), y1, ZK], [X(XCH), y1, ZK]], [0, 0, 1], 'woodr', 0.2, 0);
      Q([[X(XCH), y0, SH], [X(w0), y0, SH], [X(w0), y0, ZK], [X(XCH), y0, ZK]], [0, -1, 0], 'woodr', 0.5, 0);
      Q([[X(XCH), y0, SH], [X(XCH), y1, SH], [X(XCH), y1, ZK], [X(XCH), y0, ZK]], [-sd, 0, 0], 'woodr', 0.2, 0);
      Q([[X(XCH), y1, ZD], [X(XK), y1, ZD], [X(XK), y1, ZK], [X(XCH), y1, ZK]], [0, 1, 0], 'woodr', 0.2, 0); }
    // the bezel, raked back 20° from its foot on the dash, and its two panes, 0.004 proud of it
    const BZ = { x: 0.85, y: 5.66, z: ZD, h: 0.56, t: 0.05, rake: 20, pane: [0.77, 0.44] };
    { const u = [0, Math.sin(BZ.rake * DEG), Math.cos(BZ.rake * DEG)], n = [0, -u[2], u[1]], P = (x, s, o) => [x, BZ.y + u[1] * s + n[1] * o, BZ.z + u[2] * s + n[2] * o];
      Q([P(-BZ.x, 0, 0), P(BZ.x, 0, 0), P(BZ.x, BZ.h, 0), P(-BZ.x, BZ.h, 0)], n, 'dash', 0.5, 0);
      Q([P(-BZ.x, BZ.h, 0), P(BZ.x, BZ.h, 0), P(BZ.x, BZ.h, -BZ.t), P(-BZ.x, BZ.h, -BZ.t)], u, 'dash', 0.3, 0);
      Q([P(-BZ.x, 0, -BZ.t), P(BZ.x, 0, -BZ.t), P(BZ.x, BZ.h, -BZ.t), P(-BZ.x, BZ.h, -BZ.t)], v_mul(n, -1), 'dash', 0, 0);
      for (const sd of [-1, 1]) Q([P(sd * BZ.x, 0, 0), P(sd * BZ.x, 0, -BZ.t), P(sd * BZ.x, BZ.h, -BZ.t), P(sd * BZ.x, BZ.h, 0)], [sd, 0, 0], 'dash', 0.3, 0);
      const s0 = (BZ.h - BZ.pane[1]) / 2, s1 = s0 + BZ.pane[1];
      for (const sd of [-1, 1]) { const x0 = sd < 0 ? -0.025 - BZ.pane[0] : 0.025, x1 = x0 + BZ.pane[0];
        Q([P(x0, s0, 0.004), P(x1, s0, 0.004), P(x1, s1, 0.004), P(x0, s1, 0.004)], n, 'glasr', -2.0, 0.004); } }
    // the compass dome on the dash, between the wheel and the bezel: a dark drum, a dark glass dome
    { const y = 5.50, z = zDash(y - 0.09) - 0.005; cylZ(0, y, z, z + 0.07, 0.09, 8, 'dash', 0.6, null); domeZ(0, y, z + 0.07, 0.085, 0, 8, 'glasr', 0.4, 0.06); }
    // the lever box to starboard of the wheel, and the small stick to port, black, on the dash
    boxQ(0.40, 0.62, 5.34, 5.56, zDash(5.34) - 0.01, zDash(5.56) + 0.09, 'glasr', 0.8, 0, null, 'b');
    { const z = zDash(5.38) - 0.005; boxQ(-0.50, -0.40, 5.38, 5.48, z, z + 0.05, 'glasr', 0.8, 0, null, 'b'); tubeQ([-0.45, 5.43, z + 0.05], [-0.45, 5.43, z + 0.15], 0.02, 0.02, 'glasr', 0.6); }
    // the wheel: its hub 1.02 over the sole, 0.18 aft of the console, its axis raked 25° (aft and up); a rim of 16 bars,
    // three spokes (to port, to starboard, down), a hub on its column, which runs 0.20 up the axis into the console
    at('house', 'fit', 'wheel');
    WHEEL_GEO = { rad: 0.24, rimIn: 0.212, rake: 25, seg: 16, hubR: 0.05, spokes: [0, 180, 270], col: 0.20 };
    const HUB = [0, YC - 0.18, r3(SH + 1.02)];
    { const G2 = WHEEL_GEO, rk = G2.rake * DEG, n = [0, -Math.cos(rk), Math.sin(rk)], up = [0, Math.sin(rk), Math.cos(rk)], tr = (G2.rad - G2.rimIn) / 2;
      const R = (a, r) => v_add(HUB, v_add([Math.cos(a * DEG) * r, 0, 0], v_mul(up, Math.sin(a * DEG) * r)));
      for (let k = 0; k < G2.seg; k++) tubeQ(R(k * 360 / G2.seg, G2.rad - tr), R((k + 1) * 360 / G2.seg, G2.rad - tr), tr * Math.SQRT2, tr * Math.SQRT2, 'woodr', 0.3);
      for (const a of G2.spokes) tubeQ(R(a, G2.hubR), R(a, G2.rimIn), 0.014, 0.014, 'metal', 0.3);
      const h0 = v_add(HUB, v_mul(n, -0.03)), h1 = v_add(HUB, v_mul(n, 0.035)), ax = v_norm(v_sub(h1, h0)), u = v_norm(v_cross(ax, [0, 0, 1])), w = v_cross(u, ax);
      tubeQ(h0, h1, G2.hubR, G2.hubR, 'metal', 0.2);
      Q([0, 1, 2, 3].map(k => { const a = k * Math.PI / 2 + Math.PI / 4; return v_add(h1, v_add(v_mul(u, Math.cos(a) * G2.hubR), v_mul(w, Math.sin(a) * G2.hubR))); }), n, 'metal', 0.4, 0);
      tubeQ(h0, v_add(HUB, v_mul(n, -G2.col)), 0.035, 0.035, 'dash', 0.3); }
    // the helm's numbers for pass E (spec §6.8, §8): the wheel's centre, and the helmsman's standing place, 0.50 aft of
    // it on the sole, facing forward (anchors.helm, anchors.wheel, anchors.pilot; helm_idle stands)
    HELM = { wheel: HUB.map(r3), axis: [0, r3(-Math.cos(WHEEL_GEO.rake * DEG)), r3(Math.sin(WHEEL_GEO.rake * DEG))], stand: [0, r3(HUB[1] - 0.50), r3(SH)], face: [0, 1], clip: 'helm_idle',
      console: { y: YC, x: [r3(-XK), r3(XK)], lip: ZL, dash: ZD, bezel: { x: BZ.x, foot: [BZ.y, BZ.z], h: BZ.h, rake: BZ.rake, panes: BZ.pane } },
      note: 'a standing helm: the helmsman stands 0.50 aft of the wheel\'s hub, on the dining\'s sole, facing forward; the wheel turns about its axis, which points aft and up at the helmsman' };
  }
  {
    // ---- the room kit (C4): the 120's, in her finishes: the low walls linen to the dado, warm wood (woodr) above,
    // the soles and treads oak, the whites her cream (whiter), her glazing from inside glassd. E is the level's
    // envelope, the half-breadth a fitting may reach at (y, z): below, the hull's skin less the lining, and under the
    // plinth (PL, the shelf the hull's flare leaves outboard of the sole) its riser; in the house, its lining ----
    const DADO = 0.95, PT = 0.06, DH = 2.00, PLZ = LOWER_SOLE + 0.35;
    const ZLC = (y) => y < 1.15 ? 3.60 : y < 1.40 ? 3.40 : y < 1.65 ? 3.20 : y < Y_FD ? 3.00 : deckZ(y) - 0.15;    // the lower deck's ceiling, under the house's steps down
    let S0 = LOWER_SOLE, CEIL = 3.60, LVL = 'below', PL = PLZ;
    const EB = (y, z) => Math.max(0, halfAtZ(y, z < PL ? S0 : Math.min(z, ZLC(y))) - LIN), EH = (y, z) => z <= ZCR + 1e-9 ? hIn(y, z) : Math.min(hIn(y, ZCR), hwAt(insetP(ringAt(HU, z).P, LIN), y));
    let E = EB;
    const level = (lv, sole, ceil, pl) => { LVL = lv; S0 = sole; CEIL = ceil; PL = pl != null ? pl : -Infinity; E = lv === 'below' ? EB : EH; at(lv, 'fit', 'room'); };
    const tag = (ro, fn) => { const k = cRO; role(ro); fn(); cRO = k; };
    const envMin = (y0, y1, z0, z1) => { let h = Infinity; for (let i = 0; i <= 4; i++) for (let k = 0; k <= 3; k++) h = Math.min(h, E(lerp(y0, y1, i / 4), lerp(z0, z1, k / 3))); return h; };
    const fitBox = (x0, x1, y0, y1, z0, z1, mat, b, topMat, skip) => { const h = envMin(y0, y1, z0, z1), a = Math.max(x0, -h), c = Math.min(x1, h);
      if (!(c - a >= 0.02)) throw new Error('MotorYacht84Iso: no room for a box at x ' + x0 + '..' + x1 + ', y ' + y0 + '..' + y1 + ', z ' + r3(z0) + '..' + r3(z1));
      boxQ(a, c, y0, y1, z0, z1, mat, b, 0, topMat, skip); return [a, c]; };
    const fb = (x0, x1, y0, y1, h0, h1, mat, b, topMat, skip) => fitBox(x0, x1, y0, y1, S0 + h0, S0 + h1, mat, b, topMat, skip != null ? skip : (h0 <= 0 ? 'b' : ''));
    const fb2 = (x0, x1, y0, y1, h0, h1, mat, b, topMat, skip) => { fb(x0, x1, y0, y1, h0, h1, mat, b, topMat, skip); fb(-x1, -x0, y0, y1, h0, h1, mat, b, topMat, skip); };
    const tall = (x0, x1, y0, y1, h0, h1, mat, b, topMat) => tag('tall', () => fb(x0, x1, y0, y1, h0, h1, mat, b, topMat));
    // a berth: its base in warm wood from h0 (the sole, or the plinth), its mattress pale, its top a floor (spec §4.5)
    const bed = (id, head, x0, x1, y0, y1, h0, top) => { h0 = h0 || 0; top = top || 0.60; fb(x0, x1, y0, y1, h0, top - 0.16, 'woodr', -0.3);
      let x; tag('floor', () => { x = fb(x0 + 0.02, x1 - 0.02, y0 + 0.02, y1 - 0.02, top - 0.16, top, 'whiter', 0.2, null, 'b'); });
      BEDS.push({ id, lv: LVL, x: x.map(r3), y: [r3(y0), r3(y1)], topZ: r3(S0 + top), head }); return x; };
    const room = (id, label, x, y, o) => { o = o || {}; const walk = o.walk !== false;
      ROOMS.push(Object.assign({ id, label, lv: LVL, sole: r3(o.sole != null ? o.sole : S0), ceil: r3(o.ceil != null ? o.ceil : CEIL), x: x.map(r3), y: y.map(r3), walk }, walk ? {} : { _excluded: true }, o.note ? { note: o.note } : {})); };
    const carpet = (x0, x1, y0, y1, mat, holes) => { const h = envMin(y0, y1, S0, S0 + 0.02) - 0.02, a = Math.max(x0, -h), c = Math.min(x1, h), z = S0 + 0.008;
      tag('floor', () => putH([[a, y0, z], [c, y0, z], [c, y1, z], [a, y1, z]], [0, 0, 1], mat || 'linen', 0.1, 0, holes)); };
    const rectCut = (v, ax, u0, u1, z0, z1) => { const outs = []; let rest = v;
      for (const g of [(p) => p[ax] - u0, (p) => u1 - p[ax], (p) => p[2] - z0, (p) => z1 - p[2]]) { if (!rest) break; const s = splitBy(rest, g); if (s[0]) outs.push(s[0]); rest = s[1]; }
      return [rest && cleanPoly(rest), outs.map(cleanPoly).filter(Boolean)]; };
    // lineQ: a lining's quad less its openings ([u0, u1, z0, z1, mat | 'hole'] along axis ax), each glazed in glassd
    const lineQ = (v, out, mat, b, ops, ax) => { let rest = [v];
      for (const w of ops || []) { const nx = [];
        for (const p of rest) { const [inr, outs] = rectCut(p, ax, w[0], w[1], w[2], w[3]); outs.forEach(q => nx.push(q)); if (inr && w[4] !== 'hole') Q(inr, out, w[4] || 'glassd', -0.4); }
        rest = nx; }
      rest.forEach(p => Q(p, out, mat, b)); };
    const zv = (f, y) => typeof f === 'function' ? f(y) : f;
    const hullX = (y, z) => halfAtZ(y, z) - LIN;
    // the hull's lining either side, station by station from zLo up through its bands ([top, mat]), capped at its head
    const hullLining = (ys, zLo, bands, ops) => tag('lining', () => {
      for (let i = 0; i + 1 < ys.length; i++) { const ya = ys[i], yb = ys[i + 1], ym = (ya + yb) / 2, P = (sd, y, z) => [sd * hullX(y, z), y, z];
        for (const sd of [-1, 1]) { let za = zv(zLo, ya + 1e-4), zb = zv(zLo, yb - 1e-4);
          for (const [top, m] of bands) { const ta = zv(top, ya + 1e-4), tb = zv(top, yb - 1e-4); if (ta - za < 1e-3 && tb - zb < 1e-3) continue;
            lineQ([P(sd, ya, za), P(sd, yb, zb), P(sd, yb, tb), P(sd, ya, ta)], [-sd, 0, 0], zv(m, ym), sd < 0 ? 0.1 : -0.1, ops, 1); za = ta; zb = tb; }
          Q([P(sd, ya, za), [sd * (hullX(ya, za) + LIN - 0.005), ya, za], [sd * (hullX(yb, zb) + LIN - 0.005), yb, zb], P(sd, yb, zb)], [0, 0, 1], 'whiter', 0.3); } } });
    // the plinth either side: its riser at the sole's edge, its top out to the lining
    const plinth = (ys) => tag('floor', () => { for (let i = 0; i + 1 < ys.length; i++) { const ya = ys[i], yb = ys[i + 1];
      for (const sd of [-1, 1]) { const ra = sd * (halfAtZ(ya, S0) - LIN), rb = sd * (halfAtZ(yb, S0) - LIN), oa = sd * hullX(ya, PLZ), ob = sd * hullX(yb, PLZ);
        tag('lining', () => Q([[ra, ya, S0], [rb, yb, S0], [rb, yb, PLZ], [ra, ya, PLZ]], [-sd, 0, 0], 'woodr', -0.5));
        Q([[ra, ya, PLZ], [rb, yb, PLZ], [ob, yb, PLZ], [oa, ya, PLZ]], [0, 0, 1], 'linen', 0.2); } } });
    // a lining across the hull (ax 1, at y c) or along it (ax 0, at x c), low and high, less its openings; u0/u1 may be
    // functions of z (the hull's skin), d a cap of that depth on its head
    const flatLining = (ax, c, u0, u1, z0, z1, out, ops, zs) => tag('lining', () => {
      const U = (u, z) => typeof u === 'function' ? u(z) : u, W = (u, z) => ax ? [u, c, z] : [c, u, z];
      const Z = [...new Set([z0, z1].concat(zs || [], z0 + DADO < z1 ? [z0 + DADO] : []).map(r3))].filter(z => z >= z0 - 1e-6 && z <= z1 + 1e-6).sort((p, q) => p - q);
      for (let k = 0; k + 1 < Z.length; k++) { const za = Z[k], zb = Z[k + 1], m = zb <= z0 + DADO + 1e-6 ? 'linen' : 'woodr';
        lineQ([W(U(u0, za + 1e-6), za), W(U(u1, za + 1e-6), za), W(U(u1, zb - 1e-6), zb), W(U(u0, zb - 1e-6), zb)], out, m, 0, ops, ax ? 0 : 1); } });
    // a lid over a void (its own trim, so that a cut that lifts the deck above leaves it), from x0 out to the skin
    const lid = (y0, y1, n, zAt, xIn) => tag('trim', () => { xIn = xIn || 0; const hAt = (y) => halfAtZ(y, zAt(y)) - 0.01;
      for (let i = 0; i < n; i++) { const ya = lerp(y0, y1, i / n), yb = lerp(y0, y1, (i + 1) / n), za = zAt(ya), zb = zAt(yb), ha = hAt(ya), hb = hAt(yb); if (Math.min(ha, hb) <= xIn + 0.02) continue;
        if (xIn > 0) for (const sd of [-1, 1]) Q([[sd * xIn, ya, za], [sd * ha, ya, za], [sd * hb, yb, zb], [sd * xIn, yb, zb]], [0, 0, 1], 'whiter', -0.3);
        else Q([[-ha, ya, za], [ha, ya, za], [hb, yb, zb], [-hb, yb, zb]], [0, 0, 1], 'whiter', -0.3); } });
    // a guard rail on posts, 0.90 over z (or over a point's own z, its third, where it rakes down a flight)
    const rail = (pts, z) => tag('fit', () => { const Z = (p) => p[2] != null ? p[2] : z;
      for (let i = 0; i + 1 < pts.length; i++) tubeQ([pts[i][0], pts[i][1], Z(pts[i]) + 0.90], [pts[i + 1][0], pts[i + 1][1], Z(pts[i + 1]) + 0.90], 0.025, 0.025, 'metal', 0.3);
      pts.forEach(p => tubeQ([p[0], p[1], Z(p) + 0.03], [p[0], p[1], Z(p) + 0.90], 0.022, 0.022, 'metal', 0.1)); });
    // a straight flight (the 120's): n risers of rise from zFoot, going dir along y from yFoot; its top tread level with
    // the floor it climbs to
    const flight = (id, x0, x1, yFoot, dir, n, rise, zFoot) => { const going = 0.26, yTop = yFoot + dir * (n - 1) * going, zTop = zFoot + n * rise;
      for (let k = 1; k < n; k++) { const ya = yFoot + dir * (k - 1) * going, yb = ya + dir * going, zt = zFoot + k * rise, lo = Math.min(ya, yb), hi = Math.max(ya, yb);
        tag('fit', () => boxQ(x0, x1, lo, hi, zFoot, zt, 'whiter', 0, 0, 'oak', 'b')); }
      const f = { id, lv: LVL, x: [r3(x0), r3(x1)], foot: [r3((x0 + x1) / 2), r3(yFoot - dir * 0.30), r3(zFoot)], top: [r3((x0 + x1) / 2), r3(yTop + dir * 0.60), r3(zTop)], risers: n, rise: r3(rise), going, width: r3(x1 - x0) };
      IFLIGHTS.push(f); return f; };
    // an interior door's leaf: a thin warm-wood panel that fills its opening shut and turns on its post into the room
    // it serves (o); tagged its level and wall, not interior, as every leaf is (spec §4.6)
    const ileaf = (id, ax, c, u0, u1, z1, L) => { const D = door(id, ax, c, u0, u1, S0, z1, L.o, { kind: 'hinge', hu: L.hu, swings: L.swings, label: L.label, lv: LVL });
      const s = TG, sv = [cLV, cRO, cPT, cIT]; TG = D.faces; cIT = false; at(LVL, 'wall', 'door'); const t = 0.02;
      if (ax) boxQ(u0, u1, c - t, c + t, S0, z1, 'woodr', -0.2, 0, null, 'b'); else boxQ(c - t, c + t, u0, u1, S0, z1, 'woodr', -0.2, 0, null, 'b');
      TG = s; [cLV, cRO, cPT, cIT] = sv; return D; };
    // a partition (the 120's part): at y c across her (ax 1) or at x c along her (ax 0), from u0 to u1 (beyond ±9: to the
    // skin), its openings [u0, u1, id, { o, hu }] each with its leaf; its faces either side, its head and ends capped
    const part = (ax, c, u0, u1, doors, o) => { o = o || {}; doors = (doors || []).slice().sort((p, q) => p[0] - q[0]);
      const z0 = o.z0 != null ? o.z0 : S0, z1 = o.z1 != null ? o.z1 : CEIL, hd = (d) => Math.min(z1 - 0.05, z0 + DH), t = PT / 2;
      const lo = (z) => u0 <= -9 ? -E(c, z) : u0, hi = (z) => u1 >= 9 ? E(c, z) : u1, W = (u, z, s) => ax ? [u, c + s * t, z] : [c + s * t, u, z];
      const zs = [z0, z1]; if (z0 + DADO < z1) zs.push(z0 + DADO); if (doors.length) zs.push(hd(doors[0])); if (u0 <= -9 || u1 >= 9) { if (PL > z0 && PL < z1) zs.push(PL); for (let z = z0 + 0.45; z < z1 - 0.1; z += 0.45) zs.push(z); }
      const Z = [...new Set(zs.map(r3))].sort((p, q) => p - q);
      tag('part', () => {
        for (let k = 0; k + 1 < Z.length; k++) { const za = Z[k], zb = Z[k + 1], mat = o.mat || (zb <= z0 + DADO + 1e-6 ? 'linen' : 'woodr'), pcs = []; let l = [lo(za + 1e-6), lo(zb - 1e-6)];
          for (const d of doors) if (zb <= hd(d) + 1e-6) { pcs.push([l, [d[0], d[0]]]); l = [d[1], d[1]]; }
          pcs.push([l, [hi(za + 1e-6), hi(zb - 1e-6)]]);
          for (const [a, r] of pcs) if (r[0] - a[0] > 1e-4 || r[1] - a[1] > 1e-4) for (const s of [-1, 1]) Q([W(a[0], za, s), W(r[0], za, s), W(r[1], zb, s), W(a[1], zb, s)], ax ? [0, s, 0] : [s, 0, 0], mat, s < 0 ? -0.15 : 0.1); }
        Q([W(lo(z1 - 1e-6), z1, -1), W(hi(z1 - 1e-6), z1, -1), W(hi(z1 - 1e-6), z1, 1), W(lo(z1 - 1e-6), z1, 1)], [0, 0, 1], 'woodr', 0.3);
        for (const [u, e, f] of [[u0, -1, u0 > -9], [u1, 1, u1 < 9]]) if (f) Q([W(u, z0, -1), W(u, z0, 1), W(u, z1, 1), W(u, z1, -1)], ax ? [e, 0, 0] : [0, e, 0], 'woodr', 0.1);
        doors.forEach((d) => { const zh = hd(d); for (const [u, e] of [[d[0], 1], [d[1], -1]]) Q([W(u, z0, -1), W(u, z0, 1), W(u, zh, 1), W(u, zh, -1)], ax ? [e, 0, 0] : [0, e, 0], 'woodr', -0.4);
          Q([W(d[0], zh, -1), W(d[1], zh, -1), W(d[1], zh, 1), W(d[0], zh, 1)], [0, 0, -1], 'whiter', -0.8);
          const lf = d[3] ? ileaf(d[2], ax, c, d[0], d[1], zh, d[3]) : null;
          IDOORS.push({ id: d[2], lv: LVL, axis: ax, at: r3(c), u: [r3(d[0]), r3(d[1])], clear: r3(d[1] - d[0]), head: r3(zh - z0), leaf: lf ? lf.id : null }); }); }); };
    const ysOf = (y0, y1, step, keep) => { const s = new Set([y0, y1].concat(keep || [])), n = Math.ceil((y1 - y0) / step); for (let i = 1; i < n; i++) s.add(r3(lerp(y0, y1, i / n)));
      return [...s].filter(y => y >= y0 && y <= y1).sort((a, b) => a - b); };
    // a fitting from the sole inboard, carried on outboard over the plinth to the skin (a wardrobe, a locker)
    const full = (x0, x1, y0, y1, h1, mat, b, topMat) => { const [a, c] = fb(x0, x1, y0, y1, 0, h1, mat, b, topMat);
      if (x1 >= 9) fb(c, 9, y0, y1, PLZ - S0, h1, mat, b, topMat, 'bl'); if (x0 <= -9) fb(-9, a, y0, y1, PLZ - S0, h1, mat, b, topMat, 'br'); };
    const XS = (sd) => (a, b) => sd < 0 ? [-b, -a] : [a, b];
    const strips = (ys, z, hAt, mat, holes) => tag('floor', () => { for (let i = 0; i + 1 < ys.length; i++) { const ya = ys[i], yb = ys[i + 1], ha = hAt(ya), hb = hAt(yb);
      putH([[-ha, ya, z], [ha, ya, z], [hb, yb, z], [-hb, yb, z]], [0, 0, 1], mat, -1.4, 0, holes); } });
    const portOps = (y0, y1) => PORTS.filter(p => p[1] > y0 && p[0] < y1).map(p => [p[0], p[1], DWL + p[2], DWL + p[3]]);
    const PORT_YS = [].concat(...PORTS.map(p => [p[0], p[1]]));
    TG = IF; cIT = true;
    // ---- the lower deck (C4; level below; its sole at LOWER_SOLE, the lowest interior face of the mesh). Aft to forward:
    // the master cabin, full beam, behind the garage's inner door, its bath to port and its wardrobes forward; the guest
    // (port) and the VIP (starboard) cabins either side of the corridor, each with its head; the spiral stair's foot in
    // the corridor; the engine room behind a shut door (decision 5) and her tanks under the dining and the helm, lidded
    // and off the walk; and forward under the foredeck the crew's mess, head and cabin, their sole raised (CREW_SOLE)
    // where the hull narrows (decision 8: their way in is the foredeck hatch's ladder) ----
    const Y_M0 = Y_GB + 0.005, Y_MF = -3.85, Y_C0 = -3.24, Y_HD = -0.03, Y_LK = 1.15, Y_ER = 2.10, Y_CR = 6.85, Y_CB = 7.95, Y_CF = 9.36, XC = 0.70, CREW_SOLE = 2.00;
    // the spiral stair (decision 6, redrawn in pass E on the desk's 10-27 lean, its option 2): from the corridor's sole to
    // the dining's in nine rises, 20° a tread, a half turn clockwise seen from above (up 'right'), round a newel drum of
    // radius ri; its treads out to ro, its rail on balusters at rr. The walking line, midway between the drum and the
    // rail, at (ri + rr) / 2; the well over it the stair's plan, from the saloon's sole through the house's steps to
    // the dining's
    const SP = { c: [0.32, 1.04], ri: 0.40, ro: 0.98, rr: 0.96, n: 9, turn: 20 }, WELL_S = { x0: -0.67, x1: r3(SP.c[0] + 0.02), y0: r3(SP.c[1] - SP.ro - 0.02), y1: r3(SP.c[1] + SP.ro + 0.02) };
    level('below', LOWER_SOLE, 3.60, PLZ);
    { const ys = ysOf(Y_M0, Y_ER, 0.55, [Y_MF, Y_C0, Y_HD, Y_LK, 1.40, 1.65].concat(PORT_YS));
      strips(ys, S0, (y) => halfAtZ(y, S0) - LIN, 'oak');
      plinth(ys);
      hullLining(ys, PLZ, [[S0 + DADO, 'linen'], [ZLC, 'woodr']], portOps(Y_M0, Y_ER));
      // the ceiling, under the house's sole and the aft deck, stepping down under the house's steps: dark in the master
      // cabin (coffered, its beams below), pale forward of it
      tag('roof', () => { for (let i = 0; i + 1 < ys.length; i++) { const ya = ys[i], yb = ys[i + 1], z = ZLC((ya + yb) / 2), ha = hullX(ya, z), hb = hullX(yb, z), dk = yb <= Y_MF + 1e-6;
          putH([[-ha, ya, z], [ha, ya, z], [hb, yb, z], [-hb, yb, z]], [0, 0, -1], dk ? 'woodr' : 'whiter', dk ? -1.1 : -0.6, 0, [WELL_S]); }
        for (const y of [1.15, 1.40, 1.65]) { const z0 = ZLC(y + 1e-4), z1 = ZLC(y - 1e-4), h = hullX(y, z0);
          for (const [a, c] of [[-h, WELL_S.x0], [WELL_S.x1, h]]) Q([[a, y, z0], [c, y, z0], [c, y, z1], [a, y, z1]], [0, 1, 0], 'whiter', -0.4); }
        for (const y of [-5.75, -4.80]) boxQ(-2.62, 2.62, y - 0.06, y + 0.06, 3.50, 3.60, 'woodr', -1.5, 0, null, 't');
        for (const x of [-1.00, 1.00]) boxQ(x - 0.06, x + 0.06, Y_M0 + 0.02, Y_MF - 0.02, 3.50, 3.60, 'woodr', -1.5, 0, null, 't'); }); }
    // the master cabin, full beam (u 0.237-0.349): the garage's inner door in its aft bulkhead, three steps up; a double
    // bed, its head aft, between navy nightstands; a settee on the starboard plinth under its two ovals; its bath to port,
    // forward; wardrobes with dark glass doors either side of its lobby; a pale carpet; the dark coffered ceiling above
    flatLining(1, Y_M0, (z) => -E(Y_M0, z), (z) => E(Y_M0, z), S0, 3.60, [0, 1, 0], [[-1.75, -1.05, GARAGE_SOLE, MOUTH.z1 - 0.05, 'hole']], [PLZ]);
    flight('garage_steps', -1.75, -1.05, Y_M0 + 0.52, -1, 3, (GARAGE_SOLE - S0) / 3, S0);
    bed('master', 'aft', -0.55, 1.05, -6.65, -4.65, 0, 0.62);
    fb(-0.65, 1.15, Y_M0 + 0.005, -6.65, 0, 1.30, 'woodr', 0.1);                                              // its headboard
    fb(-1.00, -0.62, -6.64, -6.26, 0, 0.55, 'navyr', 0.1); fb(1.12, 1.50, -6.64, -6.26, 0, 0.55, 'navyr', 0.1);
    fb(1.86, 9, -5.75, -4.10, PLZ - S0, 0.47, 'linen', 0.1, null, 'b'); fb(2.36, 9, -5.75, -4.10, 0.47, 1.02, 'linen', -0.2);
    part(1, -5.20, -9, -1.00, [[-1.75, -1.15, 'master_bath', { o: [0, 1], hu: -1.15, label: 'the master cabin\'s bath' }]]);
    part(0, -1.00, -5.20 + PT / 2, Y_C0 - PT / 2, []);
    tag('tall', () => fb(-1.62, -1.17, -3.80, -3.32, 0, 0.42, 'whiter', 0.2));                                  // the bath's head
    fb(-9, -1.92, -4.75, -3.40, PLZ - S0, 0.88, 'woodr', 0, 'whiter', 'b');                                       // its basin, on the plinth
    carpet(-9, -1.03, -5.17, Y_C0 - 0.03, 'whiter');
    tag('tall', () => { for (const [x0, x1] of [[-0.97, -0.55], [0.55, 9]]) { const x = fb(x0, x1, Y_MF, Y_C0 - 0.03, 0, 1.95, 'woodr', -0.2);
      Q([[x[0] + 0.03, Y_MF - 0.005, S0 + 0.06], [x[1] - 0.03, Y_MF - 0.005, S0 + 0.06], [x[1] - 0.03, Y_MF - 0.005, S0 + 1.90], [x[0] + 0.03, Y_MF - 0.005, S0 + 1.90]], [0, -1, 0], 'glasr', -0.2);
      if (x1 >= 9) fb(x[1], 9, Y_MF, Y_C0 - 0.03, PLZ - S0, 1.95, 'woodr', -0.2, null, 'bl'); } });
    carpet(-9, 9, -6.68, -5.23); carpet(-0.97, 9, -5.23, Y_MF); carpet(-0.52, 0.52, Y_MF, Y_C0 - 0.03);
    room('master', 'Master cabin', [-r3(E(-5.2, 2.5)), r3(E(-5.2, 2.5))], [Y_M0, Y_MF], { note: 'full beam; the garage\'s inner door three steps up in its aft bulkhead; its bath to port forward, its wardrobes and lobby forward of the bed' });
    room('master_bath', 'Master bath', [-r3(E(-4.2, 2.5)), -1.00], [-5.20, Y_C0]);
    // the guest (port) and the VIP (starboard) cabins either side of the corridor, each with its head forward, en suite;
    // her lockers outboard of the corridor where the house's steps lower the ceiling
    part(1, Y_C0, -9, 9, [[-0.45, 0.45, 'master', { o: [0, -1], hu: -0.45, label: 'the master cabin\'s' }]]);
    for (const [ya, yb, z1] of [[Y_C0 + PT / 2, Y_LK, 3.60], [Y_LK, 1.40, 3.40], [1.40, 1.65, 3.20], [1.65, Y_ER - PT / 2, 3.00]]) for (const sd of [-1, 1])
      part(0, sd * XC, ya, yb, ya < 0 ? [[-1.30, -0.60, sd < 0 ? 'guest' : 'vip', { o: [sd, 0], hu: -1.30, label: sd < 0 ? 'the guest cabin\'s' : 'the VIP cabin\'s' }]] : [], { z1 });
    for (const sd of [-1, 1]) { const X = XS(sd), hid = sd < 0 ? 'guest_head' : 'vip_head';
      part(1, Y_HD, ...X(XC + PT / 2, 9), [[...X(1.15, 1.75), hid, { o: [0, 1], hu: sd * 1.15, label: sd < 0 ? 'the guest cabin\'s head' : 'the VIP cabin\'s head' }]]);
      part(1, Y_LK, ...X(XC + PT / 2, 9), [], { z1: 3.60 });
      carpet(...X(XC + PT / 2, 9), Y_C0 + PT / 2, Y_HD - PT / 2); carpet(...X(XC + PT / 2, 9), Y_HD + PT / 2, Y_LK - PT / 2, 'whiter');
      fb(...X(1.92, 9), 0.10, 0.85, PLZ - S0, 0.88, 'woodr', 0, 'whiter', 'b');                                  // the head's basin, on the plinth
      tag('tall', () => { fb(...X(1.15, 1.60), 0.62, 1.10, 0, 0.42, 'whiter', 0.2); full(...X(XC + PT / 2, 9), Y_LK + PT / 2, Y_ER - PT / 2, 1.48, 'woodr', -0.2); }); }
    bed('guest_out', 'aft', -2.62, -1.86, Y_C0 + 0.06, -1.20, PLZ - S0, 0.62); bed('guest_in', 'aft', -1.62, -0.82, Y_C0 + 0.06, -1.20, 0, 0.62);
    { const y0 = Y_C0 + 0.06, y1 = -1.20; fb(0.80, 1.80, y0, y1, 0, 0.46, 'woodr', -0.3); fb(1.86, 2.45, y0, y1, PLZ - S0, 0.46, 'woodr', -0.3, null, 'b');
      let x; tag('floor', () => { x = fb(0.82, 2.43, y0 + 0.02, y1 - 0.02, 0.46, 0.62, 'whiter', 0.2, null, 'b'); });
      BEDS.push({ id: 'vip', lv: LVL, x: x.map(r3), y: [r3(y0), r3(y1)], topZ: r3(S0 + 0.62), head: 'aft' });
      fb(0.78, 2.47, Y_C0 + PT / 2, y0, 0.46, 1.30, 'woodr', 0.1); }                                             // its headboard
    room('guest', 'Guest cabin', [-r3(E(-1.6, 2.5)), -XC], [Y_C0, Y_HD], { note: 'two single berths, the outboard one on the plinth' });
    room('guest_head', 'Guest head', [-r3(E(0.6, 2.5)), -XC], [Y_HD, Y_LK]);
    room('vip', 'VIP cabin', [XC, r3(E(-1.6, 2.5))], [Y_C0, Y_HD], { note: 'a double berth across the sole and the plinth' });
    room('vip_head', 'VIP head', [XC, r3(E(0.6, 2.5))], [Y_HD, Y_LK]);
    room('corridor', 'Corridor', [-XC, XC], [Y_C0, Y_ER], { note: 'walked from the master cabin\'s door to the spiral stair\'s foot, at its aft end; to starboard of the stair\'s drum an alcove runs on under the house\'s steps, its head falling to 1.50, to the engine room\'s shut door' });
    // the spiral stair (lv below; SP): nine rises from the corridor's sole to the dining's, eight wedge treads and a
    // landing level with the dining's sole, turning clockwise seen from above from aft (270°) round to port and on to
    // forward (90°) about a newel drum, the drum's flat face to starboard over the alcove; a rail on balusters over the
    // treads' outer edge. It is stepped onto to port from the alcove's aft end and off to starboard onto the dining
    { const n = SP.n, rise = (DINING_SOLE - S0) / n, ZD = DINING_SOLE + 0.90, N = (a) => [Math.cos(a * DEG), Math.sin(a * DEG), 0], P = (r, a, z) => [SP.c[0] + r * Math.cos(a * DEG), SP.c[1] + r * Math.sin(a * DEG), z];
      for (let k = 1; k <= n; k++) { const a0 = 270 - k * SP.turn, a1 = a0 + SP.turn, am = a0 + SP.turn / 2, zt = S0 + k * rise, zb = zt - 0.05, c0 = Math.cos(a0 * DEG), s0 = Math.sin(a0 * DEG), c1 = Math.cos(a1 * DEG), s1 = Math.sin(a1 * DEG);
        tag(zt - S0 > 1.3 ? 'tall' : 'fit', () => {
          Q([P(SP.ri, a0, zt), P(SP.ro, a0, zt), P(SP.ro, am, zt), P(SP.ro, a1, zt), P(SP.ri, a1, zt)], [0, 0, 1], 'oak', k % 2 ? -0.4 : -1.4);
          Q([P(SP.ri, a0, zb), P(SP.ro, a0, zb), P(SP.ro, am, zb), P(SP.ro, a1, zb), P(SP.ri, a1, zb)], [0, 0, -1], 'whiter', -1.2);
          for (const [p, q] of [[a0, am], [am, a1]]) Q([P(SP.ro, p, zb), P(SP.ro, q, zb), P(SP.ro, q, zt), P(SP.ro, p, zt)], N((p + q) / 2), 'whiter', 0);
          Q([P(SP.ri, a0, zb), P(SP.ro, a0, zb), P(SP.ro, a0, zt), P(SP.ri, a0, zt)], [s0, -c0, 0], 'whiter', -0.3);
          Q([P(SP.ri, a1, zb), P(SP.ro, a1, zb), P(SP.ro, a1, zt), P(SP.ri, a1, zt)], [-s1, c1, 0], 'whiter', -0.3); });
        if (k < n) tag('fit', () => { tubeQ(P(SP.rr, am, zt), P(SP.rr, am, zt + 0.90), 0.015, 0.015, 'metal', 0.1);
          if (k + 1 < n) tubeQ(P(SP.rr, am, zt + 0.90), P(SP.rr, am - SP.turn, zt + rise + 0.90), 0.022, 0.022, 'metal', 0.3); }); }
      // the newel drum: half round to port, flat to starboard, from the corridor's sole to a rail's height over the
      // dining's, capped in wood
      tag('tall', () => { for (let a = 90; a < 270; a += SP.turn) Q([P(SP.ri, a, S0), P(SP.ri, a + SP.turn, S0), P(SP.ri, a + SP.turn, ZD), P(SP.ri, a, ZD)], N(a + SP.turn / 2), 'whiter', -0.2);
        Q([P(SP.ri, 270, S0), P(SP.ri, 90, S0), P(SP.ri, 90, ZD), P(SP.ri, 270, ZD)], [1, 0, 0], 'whiter', 0.1);
        const cap = []; for (let a = 90; a <= 270; a += SP.turn) cap.push(P(SP.ri, a, ZD)); Q(cap, [0, 0, 1], 'woodr', 0.2); });
      const wl = (SP.ri + SP.rr) / 2, ft = P(wl, 270 + SP.turn / 2, S0), tp = P(wl, 90 - SP.turn / 2, DINING_SOLE);
      STAIR = { id: 'spiral', lv: 'below', centre: SP.c.slice(), radius: SP.ro, newel: SP.ri, railR: SP.rr, risers: n, rise: r3(rise), treads: n - 1, turnDeg: SP.turn, up: 'right',
        walkR: r3(wl), going: r3(wl * SP.turn * DEG), landing: [90, 90 + SP.turn], turn: 'clockwise seen from above, from aft (270°) round to port (180°) and on to forward (90°): a half turn, its landing the last tread',
        foot: [r3(ft[0]), r3(ft[1]), r3(S0)], top: [r3(tp[0]), r3(tp[1]), r3(DINING_SOLE)], well: Object.assign({}, WELL_S),
        note: 'its foot in the corridor, stepped onto to port from the alcove at its aft end; its landing level with the dining\'s sole, stepped off to starboard at its forward end, the well\'s guard rail open there' }; }
    // the engine room (decision 5): its bulkhead and a shut steel door at the corridor's end, under its lid, off the
    // walk; her tanks forward of it under the dining and the helm, lidded likewise
    part(1, Y_ER, -9, 9, [], { z1: 3.00 });
    tag('part', () => { const y = Y_ER - PT / 2 - 0.005;                                                     // its door, at the alcove's end, under the stair's landing
      Q([[0.15, y, S0 + 0.05], [0.65, y, S0 + 0.05], [0.65, y, 2.95], [0.15, y, 2.95]], [0, -1, 0], 'metal', -0.4); });
    lid(Y_ER + PT / 2, Y_CR - 0.03, 8, () => 3.00);
    room('engine', 'Engine room', [-r3(E(3.3, 2.5)), r3(E(3.3, 2.5))], [Y_ER, 4.82], { walk: false, note: 'drawn shut, off the walk (decision 5): its door a shut steel panel, no leaf; under its lid at 3.00, under the dining' });
    room('tanks', 'Tank room', [-r3(E(5.8, 2.5)), r3(E(5.8, 2.5))], [4.82, Y_CR], { walk: false, note: 'a void under its lid, under the helm: her tanks, drawn, not walked' });
    // the voids outboard of the garage and under the platform's steps, lidded (their own trim) for the cut that lifts the
    // decks over them
    lid(yTr(MOUTH.z1) + 0.02, Y_GB - 0.005, 8, () => MOUTH.z1, -MOUTH.x0);
    tag('trim', () => Q([[MOUTH.x1, yTr(GARAGE_SOLE) + 0.02, GARAGE_SOLE], [SLOT.x1, yTr(GARAGE_SOLE) + 0.02, GARAGE_SOLE], [SLOT.x1, Y_GB - 0.005, GARAGE_SOLE], [MOUTH.x1, Y_GB - 0.005, GARAGE_SOLE]], [0, 0, 1], 'whiter', -0.6));
    room('garage', 'Garage', [MOUTH.x0, MOUTH.x1], [r3(yTr(GARAGE_SOLE)), Y_GB], { sole: GARAGE_SOLE, ceil: MOUTH.z1, note: 'her exterior, not the room\'s mesh: its sole, head, sides and back wall are in faceList, lv below; open onto the platform when the door is down; its watercraft are anchors only (pass E)' });
    // the crew's quarters, under the foredeck (decision 8): the mess, its ladder up through the foredeck hatch, a settee
    // and table to port; their head to starboard; their cabin forward, two berths athwartships, one over the other
    level('below', CREW_SOLE, ZLC(Y_CR + 0.03));
    { const ys = ysOf(Y_CR, Y_CF, 0.5, [Y_CB].concat(PORT_YS));
      strips(ys, S0, (y) => E(y, S0), 'oak');
      hullLining(ys, S0, [[S0 + DADO, 'linen'], [ZLC, 'woodr']], portOps(Y_CR, Y_CF));
      // the deckhead, open under the foredeck's hatch (HATCH_F) for the ladder, and the hatch's trunk up to the deck
      tag('roof', () => { const H = HATCH_F;
        const strip = (ya, yb, za, zb, ha, hb, t0, t1, open) => { const y0 = lerp(ya, yb, t0), y1 = lerp(ya, yb, t1), z0 = lerp(za, zb, t0), z1 = lerp(za, zb, t1), h0 = lerp(ha, hb, t0), h1 = lerp(ha, hb, t1);
          for (const [a0, a1, c0, c1] of open ? [[-h0, -h1, H.x0, H.x0], [H.x1, H.x1, h0, h1]] : [[-h0, -h1, h0, h1]]) Q([[a0, y0, z0], [c0, y0, z0], [c1, y1, z1], [a1, y1, z1]], [0, 0, -1], 'whiter', -0.6); };
        for (let i = 0; i + 1 < ys.length; i++) { const ya = ys[i], yb = ys[i + 1], za = ZLC(ya), zb = ZLC(yb), ha = hullX(ya, za), hb = hullX(yb, zb);
          const t0 = clamp01((H.y0 - ya) / (yb - ya)), t1 = clamp01((H.y1 - ya) / (yb - ya));
          if (t1 <= t0) { strip(ya, yb, za, zb, ha, hb, 0, 1, false); continue; }
          if (t0 > 0) strip(ya, yb, za, zb, ha, hb, 0, t0, false); strip(ya, yb, za, zb, ha, hb, t0, t1, true); if (t1 < 1) strip(ya, yb, za, zb, ha, hb, t1, 1, false); }
        const a = ZLC(H.y0), b = ZLC(H.y1), da = deckZ(H.y0), db = deckZ(H.y1);
        Q([[H.x0, H.y0, a], [H.x1, H.y0, a], [H.x1, H.y0, da], [H.x0, H.y0, da]], [0, 1, 0], 'whiter', -0.3);
        Q([[H.x1, H.y1, b], [H.x0, H.y1, b], [H.x0, H.y1, db], [H.x1, H.y1, db]], [0, -1, 0], 'whiter', -0.3);
        Q([[H.x0, H.y1, b], [H.x0, H.y0, a], [H.x0, H.y0, da], [H.x0, H.y1, db]], [1, 0, 0], 'whiter', -0.5);
        Q([[H.x1, H.y0, a], [H.x1, H.y1, b], [H.x1, H.y1, db], [H.x1, H.y0, da]], [-1, 0, 0], 'whiter', -0.5); }); }
    part(1, Y_CR + PT / 2, -9, 9, [], { z1: ZLC(Y_CR + 0.03) });
    tag('fit', () => { const yl = CREW_LADDER.y, h = CREW_LADDER.half, zt = ZLC(yl) - 0.02;
      for (const x of [-h, h]) tubeQ([x, yl, S0], [x, yl, zt], 0.02, 0.02, 'metal', 0.2);
      for (let z = S0 + CREW_LADDER.pitch; z < zt - 0.1; z += CREW_LADDER.pitch) tubeQ([-h, yl, z], [h, yl, z], 0.016, 0.016, 'metal', 0.3); });
    fb(-9, -1.20, 6.95, 7.85, 0, 0.45, 'linen', 0.1); fb(-9, -1.75, 6.95, 7.85, 0.45, 0.95, 'linen', -0.2); fb(-1.15, -0.62, 7.05, 7.70, 0, 0.72, 'woodr', 0, 'oak');
    part(0, XC, Y_CR + PT, Y_CB - PT / 2, [[6.98, 7.58, 'crew_head', { o: [1, 0], hu: 6.98, label: 'the crew\'s head' }]], { z1: ZLC(Y_CR + 0.06) });
    tag('tall', () => fb(1.20, 1.62, 7.45, 7.88, 0, 0.42, 'whiter', 0.2)); fb(1.40, 9, 6.92, 7.30, 0.55, 0.85, 'woodr', 0, 'whiter');
    carpet(XC + PT / 2, 9, Y_CR + PT, Y_CB - PT / 2, 'whiter');
    part(1, Y_CB, -9, 9, [[0.05, 0.65, 'crew_cabin', { o: [0, 1], hu: 0.65, label: 'the crew\'s cabin' }]], { z1: ZLC(Y_CB) });
    bed('crew_lower', 'port', -1.95, -0.02, Y_CB + 0.04, 8.72, 0, 0.62); bed('crew_upper', 'port', -1.95, -0.02, Y_CB + 0.04, 8.72, 1.18, 1.36);
    tag('tall', () => fb(-9, 9, 8.95, Y_CF - PT / 2, 0, 1.60, 'woodr', -0.2));                                    // the cabin's lockers forward
    carpet(-9, 9, Y_CB + PT / 2, 8.95);
    part(1, Y_CF, -9, 9, [], { z1: ZLC(Y_CF) });
    room('crew_mess', 'Crew mess', [-r3(E(7.4, 2.5)), XC], [Y_CR, Y_CB], { sole: CREW_SOLE, note: 'their way in: the ladder up through the foredeck hatch (decision 8)' });
    room('crew_head', 'Crew head', [XC, r3(E(7.4, 2.5))], [Y_CR, Y_CB], { sole: CREW_SOLE });
    room('crew_cabin', 'Crew cabin', [-r3(E(8.6, 2.5)), r3(E(8.6, 2.5))], [Y_CB, Y_CF], { sole: CREW_SOLE, note: 'two berths athwartships, one over the other' });
    // the double bulkhead and the forepeak, a void under its lid
    lid(Y_CF + PT / 2, yB - 0.13, 10, (y) => deckZ(y) - 0.15);
    room('forepeak', 'Forepeak', [-r3(E(10.5, 3.0)), r3(E(10.5, 3.0))], [Y_CF, r3(yB - 0.13)], { walk: false, note: 'a void under its lid, forward of the double bulkhead: her chain locker' });
    // ---- the house (C4; level house): the saloon aft on the main deck's level, the galley to port of the spiral stair's
    // well, and down three steps (decision 13) the dining and the helm forward, under the coachroof. Her lining follows
    // the house's walls (H0) to the coachroof and the upper house's (HU) on up to the saloon's ceiling, glazed from
    // inside (glassd) wherever she is glazed outside, the aft bulkhead's doorway open in it (its leaves are her
    // exterior's). The upper house's lining is the saloon's ceiling's own (its roof), so that a cut that lifts the
    // ceilings leaves the house's walls standing to the coachroof all round ----
    const Y_ST = 1.15, Y_S1 = 1.40, Y_S2 = 1.65, Z_DC = ZCR - 0.02, HB = { x0: -2.0, x1: 2.0, y0: HYA + 0.38, y1: -2.09 }, RUG = { x0: -1.30, x1: 1.30, y0: -5.00, y1: -2.70 };
    const zStep = (y) => y < Y_S1 ? 3.55 : y < Y_S2 ? 3.35 : DINING_SOLE;
    level('house', HOUSE_SOLE, Z_SAL);
    // the soles, oak: the saloon's laid in a chevron herringbone within a plain border (rule 7: a ramp, plank by plank),
    // left out under its rug; the steps' treads and risers; the dining's sole; each less the spiral stair's well
    tag('floor', () => { const P = houseIn(MAIN_DECK);
      slab(P, () => HOUSE_SOLE, [0, 0, 1], 'oak', -1.4, [WELL_S, HB, { x0: -9, x1: 9, y0: Y_ST, y1: 99 }]);
      slab(P, () => DINING_SOLE, [0, 0, 1], 'oak', -1.4, [{ x0: -9, x1: 9, y0: -99, y1: Y_S2 }, WELL_S]);
      for (const [ya, yb] of [[Y_ST, Y_S1], [Y_S1, Y_S2]]) { const z = zStep(ya + 1e-4), ha = hIn(ya, MAIN_DECK), hb = hIn(yb, MAIN_DECK);
        putH([[-ha, ya, z], [ha, ya, z], [hb, yb, z], [-hb, yb, z]], [0, 0, 1], 'oak', ya < Y_S1 ? -0.4 : -2.4, 0, [WELL_S]); }
      for (const y of [Y_ST, Y_S1, Y_S2]) { const z0 = zStep(y + 1e-4), z1 = y === Y_ST ? HOUSE_SOLE : zStep(y - 1e-4), h = hIn(y, MAIN_DECK);
        for (const [a, c] of [[-h, WELL_S.x0], [WELL_S.x1, h]]) Q([[a, y, z0], [c, y, z0], [c, y, z1], [a, y, z1]], [0, 1, 0], 'oak', -0.6); }
      const W = 0.40, D = 0.20, NX = Math.round((HB.x1 - HB.x0) / W);
      for (let i = 0; i < NX; i++) { const x0 = HB.x0 + i * W, x1 = x0 + W, s0 = i % 2 ? D : 0, s1 = i % 2 ? 0 : D;
        for (let j = -1; HB.y0 + j * D < HB.y1; j++) { const y = HB.y0 + j * D;
          if (x0 >= RUG.x0 && x1 <= RUG.x1 && y >= RUG.y0 && y + 2 * D <= RUG.y1) continue;
          let v = [[x0, y + s0, HOUSE_SOLE], [x1, y + s1, HOUSE_SOLE], [x1, y + s1 + D, HOUSE_SOLE], [x0, y + s0 + D, HOUSE_SOLE]];
          v = splitBy(v, (p) => HB.y0 - p[1])[0]; if (v) v = splitBy(v, (p) => p[1] - HB.y1)[0]; v = v && cleanPoly(v);
          if (v) Q(v, [0, 0, 1], 'oak', (i + j) % 2 ? -0.4 : -2.4); } } });
    // the walls of the dining's well, from its sole and the steps up to the main deck's level, round the house's lining
    tag('lining', () => { const P = houseIn(MAIN_DECK);
      for (let j = 0; j + 1 < NC; j++) { const a = P[j], c = P[j + 1], n = pnorm(P, j); if (Math.max(a[1], c[1]) <= Y_ST + 1e-6 || Math.abs(c[1] - a[1]) < 1e-9) continue;
        const ts = [0, 1]; for (const y of [Y_ST, Y_S1, Y_S2]) { const t = (y - a[1]) / (c[1] - a[1]); if (t > 1e-6 && t < 1 - 1e-6) ts.push(t); } ts.sort((p, q) => p - q);
        for (let i = 0; i + 1 < ts.length; i++) { const pa = [lerp(a[0], c[0], ts[i]), lerp(a[1], c[1], ts[i])], pb = [lerp(a[0], c[0], ts[i + 1]), lerp(a[1], c[1], ts[i + 1])], ym = (pa[1] + pb[1]) / 2, z0 = zStep(ym);
          if (ym < Y_ST) continue;
          for (const sd of [-1, 1]) Q([[sd * pa[0], pa[1], z0], [sd * pb[0], pb[1], z0], [sd * pb[0], pb[1], MAIN_DECK], [sd * pa[0], pa[1], MAIN_DECK]], [-sd * n[0], -n[1], 0], 'linen', sd < 0 ? 0.1 : -0.1); } } });
    // the stair's well, through the saloon's sole, the steps and the dining's: its rim, band by band from each sole
    // down to the lower deck's ceiling under it; a guard rail round it, raked down the steps, open to starboard over
    // the steps' lower half and the dining's edge, where the stair's landing is stepped off
    tag('floor', () => { const W = WELL_S, zs = (y) => (y < Y_ST ? HOUSE_SOLE : zStep(y)) + 0.005;
      Q([[W.x0, W.y0, ZLC(W.y0)], [W.x1, W.y0, ZLC(W.y0)], [W.x1, W.y0, zs(W.y0)], [W.x0, W.y0, zs(W.y0)]], [0, 1, 0], 'oak', -0.6);
      Q([[W.x0, W.y1, ZLC(W.y1)], [W.x1, W.y1, ZLC(W.y1)], [W.x1, W.y1, zs(W.y1)], [W.x0, W.y1, zs(W.y1)]], [0, -1, 0], 'oak', -0.8);
      for (const [ya, yb] of [[W.y0, Y_ST], [Y_ST, Y_S1], [Y_S1, Y_S2], [Y_S2, W.y1]]) { const z0 = ZLC((ya + yb) / 2), z1 = zs((ya + yb) / 2);
        Q([[W.x0, ya, z0], [W.x0, yb, z0], [W.x0, yb, z1], [W.x0, ya, z1]], [1, 0, 0], 'oak', -0.7);
        Q([[W.x1, ya, z0], [W.x1, yb, z0], [W.x1, yb, z1], [W.x1, ya, z1]], [-1, 0, 0], 'oak', -0.9); } });
    { const W = WELL_S, xp = W.x0 - 0.03, xs = W.x1 + 0.03, ya = W.y0 - 0.03, yf = W.y1 + 0.03;
      rail([[xs, Y_S1, zStep(Y_S1 - 1e-4)], [xs, Y_ST], [xs, ya], [xp, ya], [xp, Y_ST], [xp, Y_S2, DINING_SOLE], [xp, yf, DINING_SOLE], [xs, yf, DINING_SOLE]], HOUSE_SOLE); }
    // the lining (the 120's ringLining): a ring loft's outline inset by LIN, band by band between the heights zs, a run
    // of one x (her sides) or one y (her aft face) as one quad less its openings (ops along y on the sides and round the
    // nose, along x across the aft face), linen to dado and woodr above, capped at its head
    const ringLining = (RG, zs, dado, opsY, opsX) => {
      const inset = (R, d) => R.P.map((p, j) => { if (j === 0) return [0, p[1] - d]; if (j === NP - 1) return [0, p[1] + d];
        const n = pnorm(R.P, j); return [Math.max(0, p[0] - d * n[0]), p[1] - d * n[1]]; });
      const RS = zs.map(z => { const R = ringAt(RG, z); return { z, P: inset(R, LIN), O: inset(R, 0.005), N: R.P }; });
      for (let k = 0; k + 1 < RS.length; k++) { const A = RS[k], B2 = RS[k + 1], m = B2.z <= dado + 1e-6 ? 'linen' : 'woodr', last = k + 2 === RS.length;
        for (const sd of [-1, 1]) for (let j = 0; j + 1 < NP;) { let j2 = j + 1;
          const eq = (c) => (q) => Math.abs(A.P[q][c] - A.P[j][c]) < 1e-6 && Math.abs(B2.P[q][c] - B2.P[j][c]) < 1e-6, ex = eq(0), ey = eq(1);
          if (ex(j + 1)) { while (j2 + 1 < NP && ex(j2 + 1)) j2++; } else if (ey(j + 1)) { while (j2 + 1 < NP && ey(j2 + 1)) j2++; }
          const a0 = A.P[j], a1 = A.P[j2], b0 = B2.P[j], b1 = B2.P[j2], n = pnorm(A.N, j), aft = j >= NC - 1 && Math.abs(a0[1] - a1[1]) < 1e-6;
          lineQ([[sd * a0[0], a0[1], A.z], [sd * a1[0], a1[1], A.z], [sd * b1[0], b1[1], B2.z], [sd * b0[0], b0[1], B2.z]], [-sd * n[0], -n[1], 0], m, sd < 0 ? 0.1 : -0.1, aft ? opsX : opsY, aft ? 0 : 1);
          if (last) { const o0 = B2.O[j], o1 = B2.O[j2]; Q([[sd * b0[0], b0[1], B2.z], [sd * b1[0], b1[1], B2.z], [sd * o1[0], o1[1], B2.z], [sd * o0[0], o0[1], B2.z]], [0, 0, 1], 'whiter', 0.3); }
          j = j2; } } };
    // her windows from inside, between her mullions (the side band's bottom, zGB, at each one's forward end), and the
    // aft bulkhead's doorway (a hole: the leaves are her exterior's) between its fixed glass
    const WIN = [[HYA + 0.38, Y_PANEL - 0.05, 5.19], [Y_PANEL + 0.05, -3.38, ZCR], [-3.28, -1.42, ZCR], [-1.32, Y_WS, ZCR], [Y_WS, 2.75, 4.98], [2.85, 4.85, 4.98], [4.95, 99, 4.98]]
      .map(([a, b, t]) => [a, b, r3(zGB(Math.min(b, 6.66)) + 0.01), t]);
    const AFT_OPS = [[-1.60, -0.99, 3.80, Z_SAL], [-0.99, 0.99, 3.80, Z_SAL, 'hole'], [0.99, 1.60, 3.80, Z_SAL]];
    tag('lining', () => ringLining(H0, [MAIN_DECK, 3.96, 4.39, 4.80, ZCR], 3.96, WIN, AFT_OPS));
    tag('roof', () => ringLining(HU, [Z_DC, Z_SAL], 0, [[Y_PANEL + 0.05, -3.38, Z_DC, Z_SAL], [-3.28, -1.42, Z_DC, Z_SAL], [-1.32, 99, Z_DC, Z_SAL]], AFT_OPS));
    // the aft doorway's reveal, from the bulkhead to the lining: its jambs and its threshold
    tag('lining', () => { for (const sd of [-1, 1]) Q([[sd * 0.99, HYA + 0.005, 3.80], [sd * 0.99, HYA + LIN, 3.80], [sd * 0.99, HYA + LIN, Z_SAL], [sd * 0.99, HYA + 0.005, Z_SAL]], [-sd, 0, 0], 'woodr', -0.2); });
    tag('floor', () => Q([[-0.99, HYA + 0.005, 3.80], [0.99, HYA + 0.005, 3.80], [0.99, HYA + LIN, 3.80], [-0.99, HYA + LIN, 3.80]], [0, 0, 1], 'oak', 0.1));
    // the saloon (after its picture): two pale sofas facing each other across it, a blue-grey rug between them and a
    // small round table on it; a blue cabinet forward of the starboard sofa
    for (const sd of [-1, 1]) { const X = XS(sd);
      fb(...X(1.45, 9), -5.10, -2.60, 0, 0.42, 'linen', 0.1); fb(...X(2.00, 9), -5.10, -2.60, 0.42, 0.88, 'linen', -0.2);
      for (const [y0, y1] of [[-5.10, -4.90], [-2.80, -2.60]]) fb(...X(1.45, 2.00), y0, y1, 0.42, 0.62, 'linen', 0.3); }
    tag('floor', () => { const z = HOUSE_SOLE + 0.008; Q([[RUG.x0, RUG.y0, z], [RUG.x1, RUG.y0, z], [RUG.x1, RUG.y1, z], [RUG.x0, RUG.y1, z]], [0, 0, 1], 'slate', 0.2); });
    cylZ(0, -3.85, HOUSE_SOLE + 0.008, HOUSE_SOLE + 0.40, 0.10, 8, 'woodr', -0.3, null); cylZ(0, -3.85, HOUSE_SOLE + 0.40, HOUSE_SOLE + 0.45, 0.45, 12, 'woodr', 0.1);
    fb(1.10, 9, -2.40, -1.95, 0, 0.80, 'navyr', 0.2);
    // the galley, along the port side abreast of the well: its counter in warm wood under a pale top, a dark hob and a
    // steel sink let into it, a tall unit at its aft end
    { const [a, c] = fb(-9, -1.62, -0.88, 1.10, 0, 0.90, 'woodr', -0.2, 'whiter'), z = HOUSE_SOLE + 0.905;
      tag('tall', () => fb(-9, -1.62, -1.38, -0.88, 0, 1.40, 'woodr', -0.3, 'whiter'));
      Q([[a + 0.08, -0.60, z], [c - 0.06, -0.60, z], [c - 0.06, -0.05, z], [a + 0.08, -0.05, z]], [0, 0, 1], 'glasr', 0.2);
      Q([[a + 0.10, 0.30, z], [c - 0.08, 0.30, z], [c - 0.08, 0.80, z], [a + 0.10, 0.80, z]], [0, 0, 1], 'metal', -0.2); }
    // the ceilings: the saloon's under the upper house, pale between dark beams (after its picture); forward and outboard
    // of the upper house the coachroof's underside, over the steps, the dining and the helm
    tag('roof', () => { slab(insetP(ringAt(HU, Z_SAL).P, LIN), () => Z_SAL, [0, 0, -1], 'whiter', -0.8);
      const PB = insetP(ringAt(HU, Z_SAL - 0.12).P, LIN);
      for (let y = -6.0; y < 0.5; y += 1.0) { const h = Math.min(hwAt(PB, y - 0.06), hwAt(PB, y + 0.06)); boxQ(-h, h, y - 0.06, y + 0.06, Z_SAL - 0.12, Z_SAL, 'woodr', -1.4, 0, null, 't'); }
      const A = houseIn(Z_DC), B2 = insetP(HU[0].P, LIN);
      for (let j = 0; j + 1 < NP; j++) for (const sd of [-1, 1])
        Q([[sd * A[j][0], A[j][1], Z_DC], [sd * A[j + 1][0], A[j + 1][1], Z_DC], [sd * B2[j + 1][0], B2[j + 1][1], Z_DC], [sd * B2[j][0], B2[j][1], Z_DC]], [0, 0, -1], 'whiter', -0.6); });
    room('saloon', 'Saloon', [-r3(hIn(-4, 4.5)), r3(hIn(-4, 4.5))], [HYA + LIN, -1.40], { note: 'its sofas, rug, table and cabinet; its doorway aft to the aft deck (house.door), the spiral stair\'s well forward' });
    room('galley', 'Galley', [-r3(hIn(0, 4.5)), WELL_S.x0], [-1.40, Y_ST], { note: 'open to the saloon; the spiral stair\'s well to starboard of it' });
    { const h = r3(hIn(1.4, MAIN_DECK));
      for (const [id, x0, x1, side] of [['house_steps_port', -h, WELL_S.x0, 'port'], ['house_steps_stbd', WELL_S.x1, h, 'starboard']]) { const xm = r3((x0 + x1) / 2);
        IFLIGHTS.push({ id, lv: 'house', x: [x0, x1], foot: [xm, r3(Y_S2 + 0.30), DINING_SOLE], top: [xm, r3(Y_ST - 0.30), HOUSE_SOLE],
          risers: 3, rise: 0.20, going: 0.25, width: r3(x1 - x0), note: 'the house\'s steps down to the dining (decision 13), to ' + side + ' of the spiral stair\'s well' }); } }
    // the dining, down the steps (its sole DINING_SOLE, under the coachroof): a warm wood table, a pale bench along the
    // port side and three chairs to starboard of the table, a settee along the starboard side; the helm forward (C3)
    level('house', DINING_SOLE, Z_DC);
    fb(-1.45, -0.25, 2.15, 3.95, 0.70, 0.74, 'woodr', 0.2); fb(-1.05, -0.65, 2.55, 3.55, 0, 0.70, 'woodr', -0.4);
    fb(-9, -1.70, 2.00, 4.10, 0, 0.45, 'linen', 0.1); fb(-9, -2.05, 2.00, 4.10, 0.45, 0.90, 'linen', -0.2);
    for (const y of [2.45, 3.05, 3.65]) { fb(-0.15, 0.30, y - 0.22, y + 0.22, 0, 0.45, 'woodr', -0.2, 'linen'); fb(0.24, 0.30, y - 0.22, y + 0.22, 0.45, 0.92, 'woodr', 0); }
    fb(1.70, 9, 1.90, 3.60, 0, 0.45, 'linen', 0.1); fb(2.05, 9, 1.90, 3.60, 0.45, 0.90, 'linen', -0.2);
    room('dining', 'Dining', [-r3(hIn(3, MAIN_DECK)), r3(hIn(3, MAIN_DECK))], [Y_S2, 4.30], { note: 'down the house\'s steps; its ceiling the coachroof\'s underside, the upper house\'s windscreen over the steps' });
    room('helm', 'Helm', [-r3(hIn(4.6, MAIN_DECK)), r3(hIn(4.6, MAIN_DECK))], [4.30, r3(houseIn(MAIN_DECK)[0][1])], { note: 'a standing helm (HELM), on the dining\'s sole' });
    at('house', 'fit', 'room');
    TG = F; cIT = false;
  }
  // fitCheck(tol): her rooms against what holds them (spec §4.5): the hull's skin to the deck at the side, the main
  // house's walls (H0) and the upper house's (HU). A vertex counts against the first container it is inside the
  // reach of, by how far it stands beyond that container's half-breadth
  const FIT = [(y, z) => (y >= yTT && y <= yB && z >= 0 && z <= fSz(y) + 0.02) ? halfAtZ(y, z) : 0,
    (y, z) => (y >= HYA - 0.02 && y <= Y_HF + 0.02 && z >= DINING_SOLE - 0.02 && z <= ZCR + 0.02) ? hwAt(ringAt(H0, clamp(z, MAIN_DECK, ZCR)).P, y) : 0,
    (y, z) => (z >= ZCR - 0.02 && z <= zHB(y) + 0.02) ? hwAt(ringAt(HU, z).P, y) : 0];
  function outBy(p) { let best = Infinity; for (const c of FIT) { const h = c(p[1], p[2]); if (h > 0) best = Math.min(best, Math.abs(p[0]) - h); } return best; }
  function fitCheck(tol) { tol = tol == null ? 0.01 : tol; let worst = 0, worstAt = null, outside = 0, vertices = 0;
    for (const f of IF) { let m = -Infinity; for (const p of f.v) { vertices++; const e = outBy(p); m = Math.max(m, e); if (e !== Infinity && e > worst) { worst = e; worstAt = p.map(r3); } }
      if (m > tol) outside++; }
    return { tol, faces: IF.length, vertices, outside, worst: r3(worst), worstAt: r3(worst) > 0 ? worstAt : null }; }
  // the doors' leaves (spec §4.6, the 120's): doorFaces(t), t a number (every leaf), true or false, or { leaf id: t,
  // doorOpen: t }; leafTimes(opts) gives each leaf's t for a render: opts.doors, else opts.doorOpen, else open (1) under
  // a cutaway and shut (0) without one. Shut, each leaf closes its opening exactly; faceList never holds them
  function leafT(t, D) { if (t == null || t === false) return 0; if (t === true) return 1; if (typeof t === 'number') return clamp01(t);
    const v = t[D.id] != null ? t[D.id] : t.doorOpen; return v === true ? 1 : clamp01(+v || 0); }
  function doorFaces(t) { let out = []; for (const D of LEAVES) out = out.concat(leafPose(D, leafT(t, D))); return out; }
  function leafTimes(opts) { const base = opts.doors != null ? opts.doors : (opts.doorOpen != null ? opts.doorOpen : (opts.cutaway ? 1 : 0)), m = {};
    for (const D of LEAVES) m[D.id] = leafT(base, D); return m; }

  // ================================= RENDER / CELL / API =================================
  const G = { W: 880, H: 680, cx: 440, cy: 412 };             // every state × facing × the rock's frames clears the edge by ≥ 24 px; pass B's hung ladder grew the foot from 656
  // her states (spec §4.4): the garage's door and the swim ladder. A bad value throws; keys she does not have are let be
  // (render's opts carry the state with the view)
  const STATES = { garage: '0..1: the garage\'s door, 0 shut (up in the transom\'s plane) and 1 down (the swim platform; the default)',
    ladder: 'true|false: the swim ladder hung off the platform\'s aft edge into the water (with the door down only) or stowed (false, the default: only its head plate shows)',
    doorOpen: '0..1 (render option; the leaves are doorFaces(t), t a number or { leaf id: t })' };
  function stateOf(s) { if (s == null) s = {}; if (typeof s !== 'object') throw new Error('MotorYacht84Iso: a state is an object, not ' + s);
    const garage = s.garage == null ? 1 : s.garage, ladder = s.ladder == null ? false : s.ladder;
    if (typeof garage !== 'number' || !(garage >= 0 && garage <= 1)) throw new Error('MotorYacht84Iso: garage is 0..1, not ' + garage);
    if (typeof ladder !== 'boolean') throw new Error('MotorYacht84Iso: ladder is true or false, not ' + ladder);
    return { garage, ladder }; }
  // partFaces(id, state): a part's faces in a state: 'garage' the door, 'ladder' the swim ladder hung (none when stowed),
  // 'wheel' the helm's wheel (the room's), a leaf by its id at state's doors (render's leafTimes), or a door's leaves
  function partFaces(id, state) { const s = stateOf(state);
    if (id === 'garage') return doorAt(s.garage).slice();
    if (id === 'ladder') return s.ladder && s.garage >= 0.999 ? LADDER_DEP.slice() : [];
    if (id === 'wheel') return IF.filter(f => PART.get(f) === 'wheel');
    if (LEAF[id]) return leafPose(LEAF[id], leafTimes(state || {})[id]);
    const d = doorsOf().by[id]; if (d) return [].concat(...d.leaves.map(l => partFaces(l, state)));
    throw new Error('MotorYacht84Iso: no part ' + id); }
  function faceList(state) { const s = stateOf(state); return F.concat(partFaces('garage', s), partFaces('ladder', s)); }

  // ================================= THE CUT (C5) =================================
  // A key is where the player stands, and the level it shows: deck (the decks and the sun deck: nothing lifts), house
  // (the saloon, the galley, the dining and the helm) and below (the lower deck). Ghost: the levels above lift whole,
  // with the shown level's roof; its walls stay, ghosted round the player (GHOST, the 120's rule). Gate, in the lift-set
  // form (common §1): the ghost's levels, and the shown level's walls and roof. The lower deck has no walls or roof of
  // its own but the garage's head: both modes lift every level above it, and that head. A key's player is where its
  // preview stands (the aft deck, the saloon, the corridor)
  const CUT_KEYS = { deck: { shows: null, lift: [], player: [0, -9.40, MAIN_DECK] },
    house: { shows: 'house', lift: ['top', 'rigging'], player: [0, -4.40, HOUSE_SOLE] },
    below: { shows: 'below', lift: ['main_deck', 'house', 'top', 'rigging', 'foredeck', 'below:roof'], player: [0, -1.60, LOWER_SOLE] } };
  const CUTAWAY = { ghost: {}, gate: {} };
  for (const k in CUT_KEYS) { const c = CUT_KEYS[k], s = c.shows && c.shows !== 'below' ? c.shows : null;
    CUTAWAY.ghost[k] = c.lift.concat(s ? [s + ':roof'] : []); CUTAWAY.gate[k] = c.lift.concat(s ? [s + ':wall', s + ':roof'] : []); }
  const GHOST = { rx: 1.25, ry: 1.10, above: 0.95, margin: 0.20, core: 0.60 }, _lift = {};
  // the faces a cut lifts, from CUTAWAY's lists: a level whole ('top') or one role of it ('house:roof')
  function liftOf(mode, key) { const m = CUTAWAY[mode] ? mode : 'ghost', k = m + '|' + key; if (k in _lift) return _lift[k];
    const L2 = CUTAWAY[m][key]; if (!L2) throw new Error('MotorYacht84Iso: no cutaway level ' + key + ' (one of ' + Object.keys(CUT_KEYS).join(', ') + ')');
    const lv = new Set(), lr = new Set(); L2.forEach(s => { const [a, b] = s.split(':'); if (b) lr.add(LV[a] * 16 + RO[b]); else lv.add(LV[a]); });
    return (_lift[k] = L2.length ? (f) => lv.has(f.lv) || lr.has(f.lv * 16 + f.ro) : null); }
  // the ghost's screen ellipse: centred GHOST.above over the player's feet, rx × ry m at S px/m; a ghost-role face nearer
  // the camera than the player less the margin is discarded inside it (solid to core, Bayer-dithered beyond)
  function ghostParams(B, p, g) { g = Object.assign({}, GHOST, g || {}); const q = Array.isArray(p) ? p : [p.x, p.y, p.z], c = projVert(q[0], q[1], q[2] + g.above, B, G);
    return { cx: c.sx, cy: c.sy, irx: 1 / (g.rx * S), iry: 1 / (g.ry * S), dP: c.d - g.margin, core: g.core, icore: 1 / (1 - g.core) }; }
  function boxOf(fs) { const b = { x0: Infinity, x1: -Infinity, y0: Infinity, y1: -Infinity, z0: Infinity, z1: -Infinity };
    for (const f of fs) for (const p of f.v) { b.x0 = Math.min(b.x0, p[0]); b.x1 = Math.max(b.x1, p[0]); b.y0 = Math.min(b.y0, p[1]); b.y1 = Math.max(b.y1, p[1]); b.z0 = Math.min(b.z0, p[2]); b.z1 = Math.max(b.z1, p[2]); }
    return b; }
  // render(dir, opts): her exterior (faceList). opts.cutaway = { mode: 'ghost' | 'gate', level: a CUT_KEYS key, player:
  // {x, y, z} or [x, y, z] (left out, no ghost), ghost: GHOST overrides } adds the rooms (IF; opts.interior false leaves
  // them out), lifts the key's set and ghosts the ghost roles round the player, as the 120 does
  function render(dir, opts) { opts = (typeof opts === 'number') ? { elev: opts } : (opts || {});
    const cw = opts.cutaway, key = cw ? (cw.level || cw.key || 'deck') : null;
    let faces = faceList(opts).concat(doorFaces(leafTimes(opts)));
    if (cw) { const lift = liftOf(cw.mode, key); if (opts.interior !== false) faces = faces.concat(IF); if (lift) faces = faces.filter(f => !lift(f)); }
    if (opts.channel === 'levels') faces = faces.map(f => ({ v: f.v, mat: 'lv_' + (LV_NAME[f.lv] || 'hull'), b: (f.b || 0) * 0.5, db: f.db, lv: f.lv, ro: f.ro }));
    else if (opts.channel === 'parts') faces = faces.map(f => ({ v: f.v, mat: 'pt_' + (PART.get(f) || 'hull'), b: 0, db: f.db, lv: f.lv, ro: f.ro }));
    const o = Object.assign({}, opts, { dir });
    if (cw && cw.player) o._ghost = ghostParams(camBasis(o), cw.player, cw.ghost);
    return _toRGBA(_paint(faces, o, G, matsFor(opts)), G.W, G.H); }
  // renderCut(dir, key, opts): her exterior less what the gate lifts under key (geometry().cutaway.gate), with the shown
  // level's room, its faces turned to the camera only, moved toward it by the hull's bounding diagonal (each face's own
  // db kept), as the bake places the room. renderCut(dir, 'deck', opts) is render(dir, opts)
  function renderCut(dir, key, opts) { opts = (typeof opts === 'number') ? { elev: opts } : (opts || {});
    if (!Object.prototype.hasOwnProperty.call(CUTAWAY.gate, key)) throw new Error('MotorYacht84Iso: renderCut has no cut key ' + key);
    const lift = liftOf('gate', key), all = faceList(opts).concat(doorFaces(leafTimes(opts))), ext = lift ? all.filter(f => !lift(f)) : all, o = Object.assign({}, opts, { dir }), room = [];
    if (CUT_KEYS[key].shows) { const lv = LV[CUT_KEYS[key].shows], B = camBasis(o), b = boxOf(all), shift = Math.hypot(b.x1 - b.x0, b.y1 - b.y0, b.z1 - b.z0);
      for (const f of IF) { if (f.lv !== lv || (lift && lift(f))) continue; const rv = f.v.map(p => projVert(p[0], p[1], p[2], B, G)); let ny = 0, nz = 0;
        for (let i = 0; i < rv.length; i++) { const a = rv[i], c = rv[(i + 1) % rv.length]; ny += (a.zr - c.zr) * (a.xr + c.xr); nz += (a.xr - c.xr) * (a.yr + c.yr); }
        if (-ny * B.ce + nz * B.se < 0) continue;
        room.push(Object.assign({}, f, { db: (f.db || 0) + shift })); } }
    return _toRGBA(_paint(ext.concat(room), o, G, matsFor(opts)), G.W, G.H); }
  const ROCK ={ frames: 8, rollA: 0.9, pitchA: 0.45, heaveA: 0.6, period: 6.0 };
  function rock(i, frames) { frames = frames || ROCK.frames; const a = 2 * Math.PI * (((i % frames) + frames) % frames) / frames;
    return { roll: ROCK.rollA * Math.sin(a), pitch: ROCK.pitchA * Math.sin(a + Math.PI / 2), heave: ROCK.heaveA * Math.sin(a) }; }
  function project(p, dir, opts) { const o = Object.assign({}, (typeof opts === 'number' ? { elev: opts } : opts || {}), { dir }); const q = projVert(p[0], p[1], p[2], camBasis(o), G); return { x: q.sx, y: q.sy, d: q.d }; }

  const HULL_ID = 'motor_yacht_84', LABEL = 'MOTOR YACHT 84', RIG_FILE = 'motorYacht84IsoRig.js', EXPORT_SYMBOL = 'MotorYacht84Iso';
  const FRAME = 'metres; +x starboard, +y bow, +z up; origin amidships, keel bottom, centreline; polygons counter-clockwise seen from above';
  const LOA_TEXT = '25.50 m · 83′8″', COCKPIT_Z = MAIN_DECK;
  // ================================= HULL-LAYOUT-1: THE DOORS, THE PARTS, GEOMETRY (C4, C5) =================================
  // the walker: radius = the largest collider of character rig 10.3's cast (0.23 m), stepTol = v3's, as on the fleet's walks
  const WALK = { radius: 0.23, stepTol: 0.34 };
  const clone = (o) => JSON.parse(JSON.stringify(o)), copyLists = (m) => Object.fromEntries(Object.entries(m).map(([k, v]) => [k, v.slice()]));
  // the most common level and role of a set of faces (a part's or a leaf's tags), and every role it has
  function tagsOf(faces) { if (!faces.length) return { lv: 'house', ro: 'fit', roles: ['fit'] };
    const lv = {}, ro = {}; for (const f of faces) { lv[f.lv] = (lv[f.lv] || 0) + 1; ro[f.ro] = (ro[f.ro] || 0) + 1; }
    const top = (m) => +Object.keys(m).sort((a, b) => m[b] - m[a] || a - b)[0];
    return { lv: LV_NAME[top(lv)], ro: RO_NAME[top(ro)], roles: Object.keys(ro).map(Number).sort((a, b) => a - b).map(k => RO_NAME[k]) }; }
  // The doors (the walk's house.door and house.doors[]), the 120's: one per opening with a leaf, the saloon's slider pair
  // one door of two leaves. An end-wall door (face aft or forward) has the spec's fields: y its wall, x0..x1 the opening
  // across it. A side-wall door keeps them a box in the hull's frame: x0..x1 the leaf's thickness through its wall at x,
  // y0..y1 the opening along it, y its middle. face is the side it opens to; sillZ the opening's foot; clearAt the t at
  // which the opening first passes the walker (2 × WALK.radius): a hinged leaf of width w leaves w (1 − cos θ), a slider
  // its travel × t (each leaf of the pair). keepClear is a hinged leaf's swept arc in plan. The engine room's door is
  // drawn shut, a steel panel with no leaf (decision 5), and is not one of them
  const DOOR_SET = [
    { id: 'saloon_aft', leaves: ['saloon_aft_port', 'saloon_aft_stbd'], label: 'Saloon doors', note: 'the saloon\'s aft doors onto the aft deck: a glass slider pair, each leaf sliding outboard' },
    { id: 'garage_inner', leaves: ['garage_inner'], label: 'Garage door', swings: 'aft, into the garage', note: 'the garage\'s inner door, in the master cabin\'s aft bulkhead at the head of its three steps' },
    { id: 'master', leaves: ['master'], label: 'Master cabin door', swings: 'aft, into the master cabin', note: 'the master cabin\'s door, at the corridor\'s aft end' },
    { id: 'master_bath', leaves: ['master_bath'], label: 'Bath door', swings: 'forward, into the bath', note: 'the master cabin\'s bath, to port forward of her bed' },
    { id: 'guest', leaves: ['guest'], label: 'Guest cabin door', swings: 'to port, into the guest cabin', note: 'the guest cabin\'s door, off the corridor to port' },
    { id: 'guest_head', leaves: ['guest_head'], label: 'Head door', swings: 'forward, into the head', note: 'the guest cabin\'s head, forward of it' },
    { id: 'vip', leaves: ['vip'], label: 'VIP cabin door', swings: 'to starboard, into the VIP cabin', note: 'the VIP cabin\'s door, off the corridor to starboard' },
    { id: 'vip_head', leaves: ['vip_head'], label: 'Head door', swings: 'forward, into the head', note: 'the VIP cabin\'s head, forward of it' },
    { id: 'crew_head', leaves: ['crew_head'], label: 'Crew head door', swings: 'to starboard, into the crew head', note: 'the crew\'s head, off the crew mess to starboard' },
    { id: 'crew_cabin', leaves: ['crew_cabin'], label: 'Crew cabin door', swings: 'forward, into the crew cabin', note: 'the crew cabin, forward of the crew mess' } ];
  let _doors = null;
  function doorsOf() { if (_doors) return _doors;
    const need = 2 * WALK.radius, doors = [], by = {};
    for (const S2 of DOOR_SET) { const Ls = S2.leaves.map(id => LEAF[id]), D = Ls[0], faces = [].concat(...Ls.map(l => l.faces)), T = tagsOf(faces), b = boxOf(faces);
      const side = D.n === 0, face = side ? (D.o[0] < 0 ? 'port' : 'starboard') : (D.o[1] < 0 ? 'aft' : 'forward'), z0 = r3(Math.min(...Ls.map(l => l.z0))), z1 = r3(Math.max(...Ls.map(l => l.z1)));
      const u0 = Math.min(...Ls.map(l => l.u0)), u1 = Math.max(...Ls.map(l => l.u1)), d = { id: S2.id, kind: D.kind, face };
      if (side) Object.assign(d, { y: r3((u0 + u1) / 2), x0: r3(b.x0), x1: r3(b.x1), x: r3(D.w), y0: r3(u0), y1: r3(u1) }); else Object.assign(d, { y: r3(D.w), x0: r3(u0), x1: r3(u1) });
      Object.assign(d, { z0, z1, sillZ: z0 }); let width, clearAt;
      if (D.kind === 'slide') { width = r3(Ls.reduce((s, l) => s + Math.min(l.travel, l.u1 - l.u0), 0)); clearAt = r3(Math.min(1, need / Ls.reduce((s, l) => s + l.travel, 0)));
        Object.assign(d, { travel: D.travel, opensTo: face, clearAt, leaves: S2.leaves.slice() }); }
      else { const w = D.u1 - D.u0, deg = D.swing || 90, th = Math.acos(clamp(1 - need / w, -1, 1)) / DEG, g = side ? [D.w, D.hu] : [D.hu, D.w]; width = r3(w); clearAt = r3(Math.min(1, th / deg));
        Object.assign(d, { hinge: { x: r3(g[0]), y: r3(g[1]), z0, z1 }, maxDeg: deg, swings: S2.swings, opensTo: face, clearAt, keepClear: sweptArc(D, g, deg), leaves: S2.leaves.slice() }); }
      Object.assign(d, { leaf: 'doorFaces({ ' + S2.leaves.map(l => l + ': t').join(', ') + ' })', lv: T.lv, ro: T.ro, label: S2.label,
        clear: { width, height: r3(d.z1 - d.sillZ) }, note: S2.note + (T.roles.length > 1 ? '; its leaf ' + (S2.leaves.length > 1 ? 'faces are' : 'is') + ' tagged ' + T.lv + ' / ' + T.roles.join(', ') : '') });
      doors.push(d); by[d.id] = d; }
    return (_doors = { door: by.saloon_aft, doors, by }); }
  // a hinged leaf's swept arc in plan: the hinge, then the leaf's free end from shut to maxDeg (counter-clockwise)
  function sweptArc(D, g, deg) { const w = D.u1 - D.u0, e = D.n === 0 ? [0, Math.sign((D.u0 + D.u1) / 2 - D.hu)] : [Math.sign((D.u0 + D.u1) / 2 - D.hu), 0];
    const out = [D.o[0], D.o[1]], pts = [[r3(g[0]), r3(g[1])]], N = 8;
    for (let i = 0; i <= N; i++) { const a = deg * DEG * i / N, c = Math.cos(a), s = Math.sin(a); pts.push([r3(g[0] + w * (e[0] * c + out[0] * s)), r3(g[1] + w * (e[1] * c + out[1] * s))]); }
    let A = 0; for (let i = 0; i < pts.length; i++) { const p = pts[i], q = pts[(i + 1) % pts.length]; A += p[0] * q[1] - q[0] * p[1]; }
    return A < 0 ? [pts[0]].concat(pts.slice(1).reverse()) : pts; }
  // the hull-layout parts (geometry().parts): the posed pieces, the state that moves them, the wheel, and the leaves
  let _parts = null;
  function partsList() { if (_parts) return clone(_parts); const D = doorsOf(), leafDoor = {}; for (const d of D.doors) for (const l of d.leaves) leafDoor[l] = d.id;
    const gd = tagsOf(doorAt(1)), ld = tagsOf(LADDER_DEP), wh = tagsOf(partFaces('wheel'));
    _parts = [
      { id: 'garage', group: 'state', state: 'garage', lv: gd.lv, ro: gd.ro, alsoRoles: gd.roles.filter(r => r !== gd.ro), hinge: [0, r3(HINGE.y), r3(HINGE.z)], axis: [1, 0, 0],
        note: 'the garage\'s door, turning about its hinge: down (1, the default) it is the swim platform, shut (0) it stands in the transom\'s plane; partFaces(\'garage\', state)' },
      { id: 'ladder', group: 'state', state: 'ladder', lv: ld.lv, ro: ld.ro, note: 'the swim ladder hung off the platform\'s aft edge (state ladder, with the door down); stowed, only its head plate shows, in the exterior' },
      { id: 'wheel', group: 'room', lv: wh.lv, ro: wh.ro, hub: HELM.wheel.slice(), axis: HELM.axis.slice(),
        note: 'the helm\'s wheel, in the room (interiorFaces()), its hub and axis HELM\'s; it takes no position yet (helm_idle); partFaces(\'wheel\')' } ]
      .concat(LEAVES.map(L0 => { const T = tagsOf(L0.faces); return { id: L0.id, group: 'doorFaces', door: leafDoor[L0.id], lv: T.lv, ro: T.ro, kind: L0.kind,
        note: 'doorFaces({ ' + L0.id + ': t }); its door is house.doors[] ' + leafDoor[L0.id] }; }));
    return clone(_parts); }
  // geometry() (spec §4.7): her names and tags, her levels, the cut (CUT_KEYS), the doors, the parts and the states
  const GHOST_RULE = 'role ∈ ghostRoles && depth < playerDepth − ' + GHOST.margin.toFixed(2) + ' m && inside the reveal ellipse (rx ' + GHOST.rx.toFixed(2) + ' m, ry ' + GHOST.ry.toFixed(2) +
    ' m of screen at 32 px/m, centred ' + GHOST.above.toFixed(2) + ' m above the feet) → discard; solid < ' + GHOST.core.toFixed(2) + ' R, Bayer 4×4 dither ' + GHOST.core.toFixed(2) + '–1.0 R';
  const CUT_NOTE = 'one key per place the player stands: deck (the open decks: the aft and side decks, the foredeck and the sun deck; nothing lifts), house (the saloon, ' +
    'the galley, the dining and the helm: the sun deck and the mast lift whole, and the house\'s roof) and below (the lower deck: every level above it lifts whole, and ' +
    'the garage\'s head, its one roof). Ghost lifts the shown level\'s roof and ghosts its walls round the player; gate lifts its walls too. The gate lists are complete ' +
    'lift sets in ghost\'s vocabulary. The open levels share deck: a player on any of them stands under the sky, with nothing over them to lift';
  function geometry() {
    const lev = (id, deck, sole, ceil, of, more) => Object.assign({ id, deck, sole: r3(sole), ceil: ceil == null ? null : r3(ceil), kind: ceil == null ? 'open' : 'hard', soleZ: r3(sole), ceilingZ: ceil == null ? null : r3(ceil),
      ceiling: ceil == null ? { kind: 'open', lid: null, note: of } : { kind: 'hard', lid: (more && more.lid) || null, z: r3(ceil), of } }, more && more.note ? { note: more.note } : {});
    return { schema: 'hidden-harbours/hull-geometry@1', rig: RIG_FILE, exportSymbol: EXPORT_SYMBOL, hull: HULL_ID, units: 'metres', frame: FRAME,
      ids: Object.assign({}, LV), roles: Object.assign({}, RO), ghostRoles: GHOST_ROLES.slice(), dwlZ: DWL,
      riggingClass: 'rigging: the mast and its fittings; the hardtop\'s top is the sun deck (top), its underside the house\'s roof (house:roof)',
      levels: [
        lev('below', 'lower_sole', LOWER_SOLE, 3.60, 'the main deck: the saloon\'s sole (house) and the aft deck (main_deck)', { lid: 'house', note: 'the lower deck: the master cabin aft (the garage ' +
          'aft of her is her exterior, its sole ' + GARAGE_SOLE.toFixed(2) + '), the guest and VIP cabins and their heads either side of the corridor, the spiral stair, the engine room ' +
          '(drawn shut, decision 5) and the tank room under the dining and the helm, the crew forward (their sole 2.00) and the forepeak; the engine room, the tank room and the ' +
          'forepeak are drawn, not walked' }),
        lev('main_deck', 'aft_deck', MAIN_DECK, null, 'the aft deck under the hardtop\'s overhang (ghosted, not cut) and the side decks; the swim platform is at ' + PLATFORM_Z.toFixed(2)),
        lev('house', 'saloon_sole', HOUSE_SOLE, Z_SAL, 'the upper house\'s ceiling, role roof on lv house; the sun deck over it is lv top', { note: 'the saloon and the galley on ' +
          HOUSE_SOLE.toFixed(2) + '; the dining and the helm 0.60 lower, on ' + DINING_SOLE.toFixed(2) + ' (decision 13), under the coachroof\'s underside at 5.18' }),
        lev('foredeck', 'foredeck', deckZ(Y_FD), null, 'the foredeck is sky: one step up from the side decks at y ' + Y_FD.toFixed(2) + ', it rises with the sheer to the bow'),
        lev('top', 'sun_deck', zHT(-6.88), null, 'the sun deck on the hardtop is sky, at its ladder\'s head; it rises from ' + r3(zHT(HT.yA + 0.05)) + ' aft to ' + r3(zHT(0.61)) + ' forward') ],
      cutaway: { ghost: copyLists(CUTAWAY.ghost), gate: copyLists(CUTAWAY.gate), shows: Object.fromEntries(Object.entries(CUT_KEYS).map(([k, c]) => [k, c.shows])), note: CUT_NOTE,
        players: Object.fromEntries(Object.entries(CUT_KEYS).map(([k, c]) => [k, c.player.map(r3)])), ghostRule: GHOST_RULE, ghostParams: Object.assign({}, GHOST) },
      door: clone(doorsOf().door), parts: partsList(), states: Object.assign({}, STATES) };
  }
  // ================================= HULL-LAYOUT-2: HER WALK (pass E, charter §7) =================================
  // walk() is her walk: hull-layout-2's writer (hull-walk.mjs --write --fit) writes it out as
  // Art/gameplay/motorYacht84IsoRig.motor_yacht_84.walk.json and fits kit 10.4 to it, and that file is never edited by
  // hand (a FAIL is fixed here, then rewritten). Each polygon is where the walker's centre may stand: WALK.radius from
  // every face between its floor + 0.34 (lower, it steps over) and + 1.80, and in from every drop but a climb's landing.
  // Each deck of rooms is a hard level with its own key (house, below): a floor at its declared sole and every other
  // height 0.10 or more from it, each height one piece, every indoor ramp a climb with a kind, both ends of every climb
  // on a floor, every leafed door onto a floor within 0.10 of its sill (B26 to B30). What she has and the walker does
  // not stand on is in _excluded, with its reason; each seat is sat from its own deck's floor, within 0.60 m of its
  // outline (the writer's SEAT_REACH), never from a floor under another deck
  const NL_RULE = 'A step is legal when the target point is inside an area polygon and outside its holes, and |area z - player z| < stepTol. ' +
    'Ramps interpolate z along their axis (x or y) between a0 and a1, and hold z0 / z1 beyond them. cut = the cutaway key while standing there; ' +
    'cutHi / cutLo replace it above / below zMid (stairs and ladders). Polygons are player-centre: already inset by radius, so the reader never insets them again.';
  function nlHouseDecks(levels) { const decks = {};
    for (const l of levels) { const d = { deck: l.deck, kind: l.kind, soleZ: l.sole, ceilZ: l.ceil };
      if (l.kind === 'open') d.open = true;
      if (l.ceil != null && l.ceilingZ != null && Math.abs(l.ceilingZ - l.ceil) > 0.0005) d.ceilLowZ = l.ceilingZ;
      decks[l.id] = d; }
    return { levels: levels.filter(l => l.kind === 'hard').map(l => l.id), decks }; }
  function walk() {
    const R = WALK.radius, D = doorsOf(), q = r3, A = [], X = [], I = [], MD = MAIN_DECK, HS = HOUSE_SOLE, DS = DINING_SOLE, LS = LOWER_SOLE, WR = R + 0.025;
    const CS = ROOMS.find(r => r.id === 'crew_mess').sole, crewHead = (y) => deckZ(y) - 0.15, Z_DC = ZCR - 0.02;
    // the polygons: tidy rounds them, drops repeated and straight-through corners and turns them counter-clockwise
    const sArea = (P) => { let s = 0; for (let i = 0; i < P.length; i++) { const p = P[i], c = P[(i + 1) % P.length]; s += p[0] * c[1] - c[0] * p[1]; } return s / 2; };
    const tidy = (P) => { const o = P.map(p => [q(p[0]), q(p[1])]); let cut = true;
      while (cut && o.length > 3) { cut = false;
        for (let k = 0; k < o.length && o.length > 3; k++) { const a = o[(k + o.length - 1) % o.length], p = o[k], c = o[(k + 1) % o.length];
          if (Math.abs((p[0] - a[0]) * (c[1] - a[1]) - (p[1] - a[1]) * (c[0] - a[0])) < 1e-7) { o.splice(k, 1); k--; cut = true; } } }
      return sArea(o) < 0 ? o.reverse() : o; };
    const rect = (x0, y0, x1, y1) => [[x0, y0], [x1, y0], [x1, y1], [x0, y1]];
    const span = (a, b, n) => Array.from({ length: n + 1 }, (_, i) => a + (b - a) * i / n);
    // an arc about c from a0 to a1 (degrees) in n chords; out: pushed out so that its chords stay outside the circle
    const arc = (c, r, a0, a1, n, out) => { const rr = out ? r / Math.cos(Math.abs(a1 - a0) / n / 2 * DEG) : r; return span(a0, a1, n).map(a => [c[0] + rr * Math.cos(a * DEG), c[1] + rr * Math.sin(a * DEG)]); };
    const disc = (c, r, n) => arc(c, r, 0, 360 * (n - 1) / n, n - 1, true), flipX = (P) => P.map(p => [-p[0], p[1]]).reverse();
    const pip = (P, x, y) => { let c = false; for (let i = 0, j = P.length - 1; i < P.length; j = i++) { const a = P[i], b = P[j];
      if ((a[1] > y) !== (b[1] > y) && x < (b[0] - a[0]) * (y - a[1]) / (b[1] - a[1]) + a[0]) c = !c; } return c; };
    const segD = (p, a, b) => { const dx = b[0] - a[0], dy = b[1] - a[1], l2 = dx * dx + dy * dy, t = l2 ? clamp01(((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / l2) : 0; return Math.hypot(p[0] - a[0] - t * dx, p[1] - a[1] - t * dy); };
    const edgeD = (P, p) => Math.min(...P.map((a, i) => segD(p, a, P[(i + 1) % P.length])));
    const zAt = (a, x, y) => { if (!a.ramp) return a.z; const r = a.ramp; return lerp(r.z0, r.z1, clamp01(((r.axis === 'x' ? x : y) - r.a0) / (r.a1 - r.a0))); };
    const pt = (x, y, z) => ({ x: q(x), y: q(y), z: q(z) });
    // a point of an area: (x, y) when it is inside, else the nearest point of its outline moved just inside
    const inside = (a, p) => pip(a.poly, p[0], p[1]) && !a.holes.some(h => pip(h, p[0], p[1])) && [a.poly].concat(a.holes).every(P => edgeD(P, p) > 0.002);
    const nearOn = (a, x, y) => { if (inside(a, [x, y])) return [q(x), q(y)];
      let b = null;
      for (const P of [a.poly].concat(a.holes)) P.forEach((u, i) => { const v = P[(i + 1) % P.length], dx = v[0] - u[0], dy = v[1] - u[1], l = Math.hypot(dx, dy);
        const t = clamp01(((x - u[0]) * dx + (y - u[1]) * dy) / (l * l)), c = [u[0] + t * dx, u[1] + t * dy], d = Math.hypot(x - c[0], y - c[1]);
        if (!b || d < b.d) b = { d, c, n: [-dy / l, dx / l] }; });
      const dirs = [b.n, [-b.n[0], -b.n[1]]].concat(Array.from({ length: 8 }, (_, k) => [Math.cos(k * 45 * DEG), Math.sin(k * 45 * DEG)]));
      for (const e of [0.005, 0.01, 0.02, 0.04]) { let best = null;
        for (const v of dirs) { const p = [q(b.c[0] + e * v[0]), q(b.c[1] + e * v[1])], h = Math.hypot(p[0] - x, p[1] - y); if (inside(a, p) && (!best || h < best.h)) best = { p, h }; }
        if (best) return best.p; }
      throw new Error('MotorYacht84Iso.walk: no point of ' + a.id + ' near ' + q(x) + ', ' + q(y)); };
    const byId = (id) => A.find(a => a.id === id), reach = (id, x, y) => { const a = byId(id), p = nearOn(a, x, y); return pt(p[0], p[1], zAt(a, p[0], p[1])); };
    // the records: a floor (its z, or a ramp) and a climb (its kind, cuts, zMid and ramp), each with what more it carries
    const MORE = ['onlyWhen', 'headroom', 'headroomMax', 'door', 'squeeze', 'treads', 'spiral', 'ladder', 'waitsOn', 'note'];
    const more = (a, o) => { for (const k of MORE) if (o && o[k] != null) a[k] = o[k]; return a; };
    const floor = (id, cut, z, poly, o) => { const a = { id, cut }; if (typeof z === 'number') a.z = q(z); else a.ramp = z;
      a.poly = tidy(poly); a.holes = ((o && o.holes) || []).map(tidy); A.push(more(a, o)); return a; };
    const climb = (id, kind, cuts, zMid, ramp, poly, o) => { const a = { id, kind, cut: cuts[0], cutHi: cuts[1], cutLo: cuts[2], zMid: q(zMid), ramp, poly: tidy(poly), holes: [] };
      A.push(more(a, o)); return a; };
    const yRamp = (a0, a1, z0, z1) => ({ axis: 'y', a0: q(a0), a1: q(a1), z0: q(z0), z1: q(z1) });
    // a surface z = f(y) as one ramp, its chord from y0 to y1, and how far the surface strays from it (worst)
    const chord = (f, y0, y1) => { const r = yRamp(y0, y1, f(y0), f(y1)); let worst = 0;
      for (let i = 0; i <= 40; i++) { const y = lerp(y0, y1, i / 40); worst = Math.max(worst, Math.abs(f(y) - lerp(r.z0, r.z1, (y - r.a0) / (r.a1 - r.a0)))); }
      return { ramp: r, worst }; };
    const lnorm = (a, b) => { const dx = b[0] - a[0], dy = b[1] - a[1], l = Math.hypot(dx, dy); return [-dy / l, dx / l]; };
    const insetConvex = (P, d) => P.map((p, i) => { const n1 = lnorm(P[(i + P.length - 1) % P.length], p), n2 = lnorm(p, P[(i + 1) % P.length]), k = d / (1 + n1[0] * n2[0] + n1[1] * n2[1]);
      return [p[0] + k * (n1[0] + n2[0]), p[1] + k * (n1[1] + n2[1])]; });
    const clipY = (P, y1) => { const o = []; P.forEach((p, i) => { const c = P[(i + 1) % P.length], pin = p[1] <= y1, cin = c[1] <= y1;
      if (pin) o.push(p); if (pin !== cin) { const t = (y1 - p[1]) / (c[1] - p[1]); o.push([p[0] + t * (c[0] - p[0]), y1]); } }); return o; };
    const splice = (P, y, pts) => { for (let i = 0; i < P.length; i++) { const a = P[i], c = P[(i + 1) % P.length];
        if (Math.abs(a[1] - y) < 1e-6 && Math.abs(c[1] - y) < 1e-6 && a[0] > c[0]) return P.slice(0, i + 1).concat(pts, P.slice(i + 1)); }
      throw new Error('MotorYacht84Iso.walk: no edge at y ' + y); };
    const bisect = (f, a, b) => { let fa = f(a); for (let i = 0; i < 60; i++) { const m = (a + b) / 2, fm = f(m); if ((fa < 0) === (fm < 0)) { a = m; fa = fm; } else b = m; } return (a + b) / 2; };
    // the deck's outboard edge for the walker: inside the bulwark's lining, and the radius off the top rail's line
    // (RAIL) measured square to it, where it sweeps in round the stern's corners and to the stem as much as along the
    // side. The house's outside at the walker's knees (MAIN_DECK + stepTol, where its walls stand furthest out) pushed
    // out by the radius and the mullions' 0.012 proud of the walls
    const offRail = (x, y) => RAIL.line.every((c, i) => !i || segD([Math.abs(x), y], RAIL.line[i - 1], c) >= R + RAIL.r - 1e-6);
    const side = (y) => { let a = 0, b = Math.min(xin(y) - R, sheerX(y) - 0.098 - R); if (offRail(b, y)) return b;
      for (let k = 0; k < 40; k++) { const m = (a + b) / 2; if (offRail(m, y)) a = m; else b = m; } return a; };
    const hOut = insetP(ringAt(H0, MD + WALK.stepTol).P, -(R + 0.015)), hO = (y) => hwAt(hOut, y);
    // inside: the house's lining (H0) at z inset by the radius; the upper house's (HU, over the saloon, its front raked
    // aft going up) where the walker's band reaches above the coachroof's underside; and below, the hull's lining
    // (EB: at the sole's breadth under the plinth pl) over the band to zTop, the walker's disc round each y
    const hInR = (y, z) => hwAt(insetP(ringAt(H0, clamp(z, MD, ZCR)).P, LIN + R), y);
    const xU = (y, z, x) => z <= Z_DC + 1e-6 ? x : Math.min(x, hwAt(insetP(ringAt(HU, z).P, LIN + R), y));
    const xHull = (y, sole, pl, zTop) => { let m = Infinity;
      for (let i = 0; i <= 8; i++) { const dy = (i / 4 - 1) * R, s = Math.sqrt(Math.max(0, R * R - dy * dy));
        for (let k = 0; k <= 6; k++) { const z = lerp(sole + 0.34, zTop, k / 6); m = Math.min(m, halfAtZ(y + dy, z < pl ? sole : z) - LIN - s); } }
      return m; };
    // the blades: the side decks stop R aft of the starboard one at the walker's head (yBl0) and start R forward of it
    // (yS0); between them the deck is the washboard's (pinch: the least room between a blade and the rail through the
    // walker's band, its section taken between its stations; room: the least the walker's centre has there)
    const BL = BLADE.map(s => ({ z: s.C[2], y0: Math.min(...s.P.map(p => p[1])), y1: Math.max(...s.P.map(p => p[1])), x0: Math.min(...s.P.map(p => p[0])) }));
    const bladeAt = (z) => { let k = 0; while (k < BL.length - 2 && z > BL[k + 1].z) k++; const a = BL[k], b = BL[k + 1], t = clamp01((z - a.z) / (b.z - a.z)); return { y0: lerp(a.y0, b.y0, t), y1: lerp(a.y1, b.y1, t) }; };
    const yBl0 = q(Math.min(...BL.map(s => s.y0)) - R), yS0 = q(bladeAt(MD + 1.80).y1 + R), xB = q(Math.min(...BL.filter(s => s.z <= MD + 1.80).map(s => s.x0)) - R);
    const hexAt = (z) => { let k = 0; while (k < BLADE.length - 2 && z > BLADE[k + 1].C[2]) k++; const a = BLADE[k], b = BLADE[k + 1], t = clamp01((z - a.C[2]) / (b.C[2] - a.C[2]));
      return a.P.map((p, j) => [lerp(p[0], b.P[j][0], t), lerp(p[1], b.P[j][1], t)]); };
    const band = span(MD + WALK.stepTol, MD + 1.80, 24).map(hexAt).reduce((o, h) => o.concat(h), []);
    const pinch = q(Math.min(...band.map(p => sheerX(p[1]) - 0.098 - p[0]))), room = q(Math.min(...band.map(p => side(p[1]) - R - p[0])));

    // ---- the house (key house): the saloon and the galley on HOUSE_SOLE; the dining and the helm 0.60 down, on DINING_SOLE
    const xH = q(hIn(0, MD) - R), yH = q(HYA + LIN + R), HF = IFLIGHTS.find(f => f.id === 'house_steps_stbd'), Y_ST = q(HF.top[1] + 0.30), Y_S2 = q(HF.foot[1] - 0.30);
    const treadsOf = (f) => Array.from({ length: f.risers - 1 }, (_, i) => ({ z: q(f.foot[2] + (f.risers - 1 - i) * f.rise), depth: q(f.going) }));
    const zRamp = (y) => lerp(HS, DS, clamp01((y - HF.top[1]) / (HF.foot[1] - HF.top[1])));   // the steps' walk z (their ramp)
    floor('saloon', 'house', HS, [[-xH, yH], [xH, yH], [xH, -5.10 - R], [1.45 - R, -5.10 - R], [1.45 - R, -2.40 - R], [1.10 - R, -2.40 - R], [1.10 - R, -1.95 + R], [xH, -1.95 + R]]
      .concat(span(-0.20, Y_ST, 14).map(y => [xU(y, HS + 1.80, xH), y]), [[0.37 + WR, Y_ST], [0.37 + WR, 0.01 - WR], [-0.70 - WR, 0.01 - WR], [-0.70 - WR, Y_ST]], span(Y_ST, -0.20, 14).map(y => [-xU(y, HS + 1.80, 1.62 - R), y]), [[-1.62 + R, -1.38 - R], [-xH, -1.38 - R],
      [-xH, -2.60 + R], [-1.45 + R, -2.60 + R], [-1.45 + R, -5.10 - R], [-xH, -5.10 - R]]),
      { holes: [disc([0, -3.85], 0.45 + R, 16)], headroom: q(Z_SAL - 0.12 - HS), headroomMax: q(Z_SAL - HS),
        note: 'the saloon and the galley, from the doorway (house.door) forward to the house\'s steps: round the sofas, the cabinet, the galley\'s counters and the stair\'s well and its rail; the coffee table a hole. ' +
          q(Z_SAL - 0.12 - HS) + ' m under the beams, ' + q(Z_SAL - HS) + ' m between them' });
    floor('saloon_door', 'house', HS, rect(D.door.x0 + R, HYA - R - 0.04, D.door.x1 - R, yH + 0.10), { headroom: q(D.door.z1 - HS), door: D.door.id,
      note: 'the saloon\'s doorway (house.door: two leaves sliding apart), from the aft deck under the hardtop into the saloon: a wall while shut, passable from doorOpen ' + D.door.clearAt });
    const xD = (y) => Math.min(hInR(y, MD), hInR(y, 4.39), hInR(y, DS + 1.80));
    floor('dining_helm', 'house', DS, [[0.34, Y_S2 + 0.005], [1.70 - R, Y_S2 + 0.005], [1.70 - R, 3.60 + R]].concat(span(3.60 + R, 5.30 - R, 8).map(y => [xD(y), y]),
      [[0.26 + R, 5.30 - R], [0.26 + R, 5.00 - R], [-0.26 - R, 5.00 - R], [-0.26 - R, 5.30 - R]], span(5.30 - R, 4.10 + R, 6).map(y => [-xD(y), y]),
      [[-1.70 + R, 4.10 + R], [-1.70 + R, 3.95 + R], [-0.25 + R, 3.95 + R], [-0.25 + R, 3.87 + R], [0.30 + R, 3.87 + R], [0.30 + R, 2.07 + WR], [0.37 + WR, 2.07 + WR], [0.37 + WR, 2.07 - WR],
        [0.34, 2.07 - WR]]),
      { headroom: q(Z_DC - DS), note: 'the dining and the helm, under the coachroof (' + q(Z_DC - DS) + ' m): from the starboard steps\' foot and the spiral stair\'s head, between the chairs and the settee, ' +
        'across forward of the table and the bench to the console, round the wheel\'s pedestal; the helmsman stands here (anchors.helm)' });
    climb('house_steps_stbd', 'stair', ['house', 'house', 'house'], (HS + DS) / 2, yRamp(HF.top[1], HF.foot[1], HS, DS),
      [[0.37 + WR, HF.top[1]]].concat(span(HF.top[1], 1.90 - R, 41).map(y => [xU(y, zRamp(y) + 1.80, xH), y]), [[1.70 - R, 1.90 - R], [1.70 - R, HF.foot[1]], [0.37 + WR, HF.foot[1]]]),
      { headroom: 1.80, headroomMax: 2.34, treads: treadsOf(HF), note: 'the house\'s steps to starboard of the stair\'s well (decision 13): ' + HF.risers + ' risers of ' + HF.rise + ', treads ' + HF.going +
        ', from the saloon down to the dining; outboard, the radius in from the upper house\'s raked front, 1.80 m over them at the least' });
    const SC = STAIR.centre, SR0 = q(STAIR.newel + R), SR1 = q(STAIR.railR - R - 0.02);
    climb('spiral', 'stair', ['below', 'house', 'below'], (LS + DS) / 2, yRamp(SC[1] - SR1, SC[1] + SR1, LS, DS), arc(SC, SR1, 80, 280, 20).concat(arc(SC, SR0, 280, 80, 20, true)),
      { treads: Array.from({ length: STAIR.treads }, (_, i) => ({ z: q(LS + (STAIR.treads - i) * (DS - LS) / STAIR.risers), depth: STAIR.going })),
        spiral: { centre: SC.slice(), up: STAIR.up, turnDeg: STAIR.turnDeg, going: STAIR.going },
        note: 'the spiral stair (decision 6, option 2): ' + STAIR.risers + ' risers of ' + STAIR.rise + ' turning ' + STAIR.turnDeg + '° each, clockwise going up, from the corridor (aft, 270°) round to port and up ' +
          'to the dining (forward, 90°); the walker keeps between the newel and the rail (' + SR0 + ' to ' + SR1 + ' m from its centre). Its ramp runs along y, so that it strays from the helix ' +
          'by up to ' + q(Math.max(...span(90, 270, 36).map(a => Math.abs((LS + (270 - a) / 180 * (DS - LS)) - (LS + (Math.sin(a * DEG) * SR1 + SR1) / (2 * SR1) * (DS - LS))))))  + ' m mid-turn' });

    // ---- the lower deck (key below), on LOWER_SOLE: the corridor; the master cabin aft, its bath; the guest and VIP
    // cabins and their heads either side of the corridor; the garage aft, up its steps; the crew forward, on their sole
    const hB = q(ROOMS.find(r => r.id === 'corridor').ceil - LS), mC = ROOMS.find(r => r.id === 'master').ceil;
    const xM = (y0, y1) => q(Math.min(1.63, ...span(y0, y1, 8).map(y => xHull(y, LS, LS + 0.35, Math.min(LS + 1.80, mC)))));
    floor('corridor', 'below', LS, [[-0.44, -2.98], [0.44, -2.98], [0.44, 0.41], [0.39, 0.41], [0.39, -0.17], [-0.44, -0.17]], { headroom: hB,
      note: 'the corridor from the master\'s door forward to the spiral stair\'s foot (its alcove to starboard), between the guest and the VIP cabins' });
    floor('master_door', 'below', LS, rect(-0.22, -3.55, 0.22, -2.93), { headroom: hB, door: 'master', note: 'the master cabin\'s doorway' });
    floor('master_lobby', 'below', LS, rect(-0.32, -4.10, 0.32, -3.50), { headroom: hB, note: 'inside the master\'s door, to the foot of the berth' });
    floor('master_fwd', 'below', LS, rect(-0.74, -4.42, xM(-4.42, -4.08), -4.08), { headroom: hB, note: 'across the cabin at the berth\'s foot' });
    floor('master_stbd', 'below', LS, rect(1.28, -6.03, xM(-6.03, -4.40), -4.40), { headroom: hB, note: 'down the berth\'s starboard side, by the settee' });
    floor('master_port', 'below', LS, rect(-1.607, -6.03, -0.78, -5.46), { headroom: hB, note: 'aft to port of the berth: the bath\'s door and the garage\'s steps' });
    floor('master_squeeze', 'below', LS, rect(-0.82, -5.55, -0.70, -4.30), { headroom: hB, squeeze: { gapW: 0.42, ceilZ: null },
      note: 'between the berth\'s port side and the bath\'s wall: 0.42 m, a squeeze (the berth 0.10 to 0.15 m to starboard would open it)' });
    floor('master_bath_door', 'below', LS, rect(-1.52, -5.50, -1.38, -4.90), { headroom: hB, door: 'master_bath', note: 'the bath\'s doorway' });
    floor('master_bath', 'below', LS, rect(-1.602, -4.94, -1.26, -4.03), { headroom: hB, note: 'the master\'s bath, between its basin and its shower' });
    const GU = [['guest_door', rect(-1.00, -0.97, -0.40, -0.83), 'guest', 'the guest cabin\'s doorway, off the corridor'],
      ['guest', rect(-1.570, -0.97, -0.96, -0.29), null, 'the guest cabin\'s floor forward of its two berths'],
      ['guest_head_door', rect(-1.52, -0.33, -1.38, 0.27), 'guest_head', 'the guest head\'s doorway'],
      ['guest_head', rect(-1.555, 0.23, -0.96, 0.39), null, 'the guest head\'s floor']];
    for (const [id, P, door, note] of GU) floor(id, 'below', LS, P, { headroom: hB, door: door || undefined, note });
    for (const [id, P, door] of GU) floor(id.replace('guest', 'vip'), 'below', LS, flipX(P), { headroom: hB, door: door ? door.replace('guest', 'vip') : undefined, note: 'as ' + id + ', to starboard' });
    const GF = IFLIGHTS.find(f => f.id === 'garage_steps'), gy0 = GF.foot[1] - 0.30, gy1 = GF.top[1] + 0.60;
    climb('garage_steps', 'stair', ['below', 'below', 'below'], (LS + GARAGE_SOLE) / 2, yRamp(gy0 + GF.going / 2, gy1 - GF.going / 2, LS, GARAGE_SOLE), rect(GF.x[0] + R, gy1 - GF.going / 2, GF.x[1] - R, -6.0),
      { headroom: 1.25, treads: treadsOf(GF), door: 'garage_inner', note: 'from the master cabin aft up ' + GF.risers + ' steps through the garage\'s inner door (garage_inner) to its sole, under the aft deck' });
    floor('garage', 'below', GARAGE_SOLE, [[-1.62, -11.045], [0.82, -11.045], [0.82, Y_GB - R], [-1.28, Y_GB - R], [-1.28, Y_GB], [-1.52, Y_GB], [-1.52, Y_GB - R], [-1.62, Y_GB - R]],
      { headroom: q(MOUTH.z1 - GARAGE_SOLE), note: 'the garage under the aft deck: its two watercraft\'s places are anchors (anchors.watercraft, decision 4), not drawn; ' + q(MOUTH.z1 - GARAGE_SOLE) + ' m under its deckhead, so a duck' });
    floor('garage_mouth', 'below', GARAGE_SOLE, rect(MOUTH.x0 + R, -11.95, MOUTH.x1 - R, -10.95), { onlyWhen: { garage: 1 }, headroom: q(MOUTH.z1 - GARAGE_SOLE),
      note: 'the garage\'s mouth, out onto the platform while its door is down (garage 1)' });
    const crew = (id, P, o) => { const ys = P.map(p => p[1]), h0 = crewHead(Math.min(...ys)) - CS, h1 = crewHead(Math.max(...ys)) - CS;
      return floor(id, 'below', CS, P, Object.assign({ headroom: q(Math.min(h0, h1)), headroomMax: q(Math.max(h0, h1)) }, o)); };
    crew('crew_mess', rect(-0.39, 7.14, 0.44, 7.37), { note: 'the crew\'s mess at the foot of their ladder, aft of it: its table and settee to port (see _excluded crew_mess_table)' });
    crew('crew_squeeze', rect(0.35, 7.30, 0.44, 7.95), { squeeze: { gapW: 0.42, ceilZ: q(crewHead(7.30) - CS) }, note: 'past the ladder\'s starboard rail to the cabin\'s door: 0.42 m, a squeeze' });
    crew('crew_cabin_door', rect(0.30, 7.86, 0.42, 8.30), { door: 'crew_cabin', note: 'the crew cabin\'s doorway' });
    crew('crew_cabin', [[0.21, 8.21]].concat(span(8.21, 8.72, 6).map(y => [Math.min(lerp(1.831, 1.652, (y - 8.21) / 0.51), xHull(y, CS, -Infinity, CS + 1.80)), y]), [[0.21, 8.72]]), { note: 'the crew cabin\'s floor beside their bunks' });
    const yL1 = q(HATCH_F.y1 + R), yL0 = q(CREW_LADDER.y - 0.25);
    climb('crew_ladder', 'ladder', ['below', 'deck', 'below'], (CS + deckZ(yL1)) / 2, yRamp(yL0, yL1, CS, deckZ(yL1)), rect(HATCH_F.x0 + R, yL0, HATCH_F.x1 - R, yL1),
      { ladder: { via: 'hatch', hatchW: q(HATCH_F.x1 - HATCH_F.x0), hatchL: q(HATCH_F.y1 - HATCH_F.y0), rise: q(deckZ(CREW_LADDER.y) - CS), pitch: CREW_LADDER.pitch, rake: 0 },
        note: 'the crew\'s ladder (decision 8): from their mess up through the foredeck\'s hatch onto the foredeck, upright, its rungs ' + CREW_LADDER.pitch + ' apart' });

    // ---- the deck (key deck): the platform, its steps, the aft deck, the side decks, the foredeck and the sun deck
    const yP = q(yTr(ZTT) - R), xs0 = STEPS.x0 + R, xs1 = STEPS.x1 - R, sy0 = q(STEPS.y0 - STEPS.going / 2), sy1 = q(STEPS.y0 + (STEPS.n - 0.5) * STEPS.going);
    floor('platform', 'deck', PLATFORM_Z, tidy(splice(tidy(clipY(insetConvex(tidy(PLAT.plan), R), yP)), yP, [[xs1, yP], [xs1, sy0], [xs0, sy0], [xs0, yP]])),
      { onlyWhen: { garage: 1 }, note: 'the swim platform (the garage\'s door, down), ' + q(PLATFORM_Z - DWL) + ' m over the waterline; R aft of the transom\'s top, into the steps\' slot; ' +
        'up from the water by its swim ladder (see _excluded platform_ladder)' });
    climb('platform_steps', 'stair', ['deck', 'deck', 'deck'], (PLATFORM_Z + MD) / 2, yRamp(sy0, sy1, PLATFORM_Z, MD), rect(xs0, sy0, xs1, sy1),
      { onlyWhen: { garage: 1 }, treads: Array.from({ length: STEPS.n - 1 }, (_, i) => ({ z: q(PLATFORM_Z + (STEPS.n - 1 - i) * STEPS.rise), depth: STEPS.going })),
        note: 'the steps in the transom\'s slot at the starboard quarter: ' + STEPS.n + ' risers of ' + q(STEPS.rise) + ', treads ' + STEPS.going + ', from the platform up to the aft deck' });
    const yAD = -11.60 + RAIL.r + R, yLad = q(SUN_LADDER.y0 + SUN_LADDER.k * 1.80 + 0.025 + R);
    // round the stern's corners, where the rail sweeps in (side), the edge in short steps
    const yCor = span(yAD, -10.89, 12);
    floor('aft_deck_aft', 'deck', MD, [[1.15 - RAIL.r - R, yAD], [1.15 - RAIL.r - R, -10.89]].concat(yCor.slice().reverse().map(y => [-side(y), y])),
      { note: 'the aft deck (pieces aft_deck_*), on MAIN_DECK: across the stern inside the transom\'s rail, to the steps\' inboard rail' });
    floor('aft_deck_port', 'deck', MD, [[-1.56, -10.94], [-1.56, -7.80], [-xB, -7.80], [-xB, yBl0]].concat(span(yBl0, -10.94, 8).map(y => [-side(y), y])),
      { note: 'down the port side, outboard of the port table and its chairs, to the blade' });
    floor('aft_deck_mid', 'deck', MD, [[-0.41, -10.90], [-0.11, -10.90], [-0.11, -9.808]].concat(arc([0.41, -9.37], 0.45 + R, 220, 140, 4, true),
      [[-0.11, -8.932], [-0.11, -7.84], [-0.41, -7.84], [-0.41, -8.74], [-0.53 + R, -8.74], [-0.53 + R, -10.0], [-0.41, -10.0]]),
      { note: 'between the two tables and their chairs: ' + q(-0.04 - (-0.53)) + ' m between the tables' });
    floor('aft_deck_stbd', 'deck', MD, [[STEPS.x1 + R, yAD]].concat(yCor.concat(span(-10.89, yBl0, 8).slice(1)).map(y => [side(y), y]),
      [[xB, yBl0], [xB, -8.10], [SUN_LADDER.x[0] - 0.025 - R, -8.10], [SUN_LADDER.x[0] - 0.025 - R, -7.80], [0.41 + 0.29 + R, -7.80], [0.41 + 0.29 + R, -9.37 + 1.0 - 0.29 - R],
        [0.41 + 0.45 + R, -9.37 + 1.0 - 0.29 - R], [0.41 + 0.45 + R, -9.42], [STEPS.x1 + R, -9.42]]),
      { note: 'outboard of the steps\' slot, and forward of it beside the starboard table to the sun deck\'s ladder\'s foot, and on past the table\'s forward chair to port of ' +
        'the ladder, into aft_deck_fwd: ' + q(SUN_LADDER.x[0] - 0.025 - (0.41 + 0.29)) + ' m between them; the blade stands ' + q(xB + R - SUN_LADDER.x[1] - 0.025) +
        ' m outboard of the ladder, under the walker\'s width, so no way forward between them' });
    floor('aft_deck_fwd', 'deck', MD, [[-xB, -7.85], [SUN_LADDER.x[0] - 0.025 - R, -7.85], [SUN_LADDER.x[0] - 0.025 - R, yLad], [xB, yLad], [xB, HYA - 0.03 - R], [-xB, HYA - 0.03 - R]],
      { note: 'under the hardtop, from the tables to the saloon\'s doorway, between the blades; to port of the sun deck\'s ladder and forward of it' });
    const yS1 = q(Y_FD + 0.01);
    // outboard, the main deck's lining to the step (xin at MAIN_DECK, not the foredeck's height), 0.008 inside its chords
    const sideS = span(yS0, yS1, 24).map(y => [Math.min(side(y), xin(y, MD) - 0.008 - R), y]).concat([[hO(yS1), yS1]], hOut.filter(p => p[1] > yS0 && p[1] < yS1), [[hO(yS0), yS0]]);
    floor('side_deck_stbd', 'deck', MD, sideS, { note: 'the starboard side deck, from forward of the blade (the washboard aft of it, washboards[]) to the foredeck\'s step, between the house and the rail' });
    floor('side_deck_port', 'deck', MD, flipX(sideS), { note: 'as side_deck_stbd' });
    // the foredeck: one step up from the side decks at Y_FD, then rising with the sheer; each piece the chord of deckZ
    const nose = hOut.filter(p => p[1] > Y_FD), fit = { foredeck: 0, sunDeck: 0 };
    const fdPiece = (id, y0, y1, P, o) => { const c = chord(deckZ, y0, y1); fit.foredeck = Math.max(fit.foredeck, c.worst); return floor(id, 'deck', c.ramp, P, o); };
    fdPiece('foredeck_1', Y_FD, 9.0, span(Y_FD, 9.0, 12).map(y => [side(y), y]).concat(span(9.0, Y_FD, 12).map(y => [-side(y), y]), [[-hO(Y_FD), Y_FD]], flipX(nose), nose, [[hO(Y_FD), Y_FD]]),
      { note: 'the foredeck (pieces foredeck_*), from the step at y ' + Y_FD + ' round the house\'s front, over the crew\'s hatch (its lid flush, 0.05 m), each piece a ramp along y on the sheer\'s chord' });
    fdPiece('foredeck_2', 8.98, 10.57, span(8.98, 10.57, 10).map(y => [side(y), y]).concat(span(10.57, 8.98, 10).map(y => [-side(y), y])), {});
    let yE = 10.55; while (yE < 12.30 && side(yE + 0.01) >= 0.05) yE = q(yE + 0.01);
    fdPiece('foredeck_3', 10.55, 11.42, span(10.55, 11.42, 8).map(y => [side(y), y]).concat(span(11.42, 10.55, 8).map(y => [-side(y), y])),
      { holes: [rect(-0.18, 10.80, 0.18, 11.20)], note: 'round the windlass, a hole' });
    fdPiece('foredeck_4', 11.40, yE, span(11.40, yE, 10).map(y => [side(y), y]).concat(span(yE, 11.40, 10).map(y => [-side(y), y])), { note: 'to the bow' });
    // the sun deck on the hardtop, inside its rail (PI_) by the radius; each piece the chord of zHT
    const PIo = insetP(PI_, R), xo = (y) => hwAt(PIo, y);
    const sunPiece = (id, y0, y1, P, o) => { const c = chord(zHT, y0, y1); fit.sunDeck = Math.max(fit.sunDeck, c.worst); return floor(id, 'deck', c.ramp, P, o); };
    const yHa = q(HATCH_T.y0 - R), aftR = [[xo(yHa), yHa]].concat(PIo.filter(p => p[1] < yHa));
    sunPiece('sun_deck_aft', Math.min(...PIo.map(p => p[1])), yHa, aftR.concat(flipX(aftR)), { note: 'the sun deck (pieces sun_deck_*) on the hardtop, inside its rail by the radius: aft of its hatch' });
    sunPiece('sun_deck_hatch', -7.91, -6.88, rect(-xo(-7.91), -7.91, HATCH_T.x0 - R, -6.88), { note: 'beside the hatch, R from its opening' });
    const yM0 = HATCH_T.y1, yM1 = q(-4.90 - R), aP = Math.asin((yM0 - SUN_LADDER.yPost) / (0.022 + R)) / DEG, post = (x) => arc([x, SUN_LADDER.yPost], 0.022 + R, 180 - aP, aP, 8, true);
    sunPiece('sun_deck_mid', yM0, yM1, [[-xo(yM0), yM0]].concat(post(SUN_LADDER.x[0]), post(SUN_LADDER.x[1]), [[xo(yM0), yM0], [xo(yM1), yM1], [-xo(yM1), yM1]]),
      { holes: [rect(-0.35 - R, -6.45 - R, 0.35 + R, -5.85 + R)], note: 'forward of the hatch, its forward edge the ladder\'s landing (not inset), round the grab posts, the table a hole' });
    for (const [id, x0, x1] of [['sun_deck_port', -xo(-4.0), -1.10 - R], ['sun_deck_centre', -0.30 + R, 0.30 - R], ['sun_deck_stbd', 1.10 + R, xo(-4.0)]])
      sunPiece(id, -5.15, -2.65, rect(x0, -5.15, x1, -2.65), { note: 'beside and between the loungers (their pads are cushions, kept off by the radius)' });
    const yU = -2.67, yb = bisect(y => xo(y) - (0.60 + R), -2.0, 0.61), yX = MAST.yX, xl = 0.60 + R, xr = 0.30 + R;
    const fwdR = span(yU, yb, 12).map(y => [xo(y), y]).concat([[xl, -0.04 - R], [xr, -0.04 - R], [xr, yX + 0.05 + R], [xl, yX + 0.05 + R], [xl, yX - 0.05 - R], [xr, yX - 0.05 - R], [xr, -2.00 - R]]);
    sunPiece('sun_deck_fwd', yU, yb, fwdR.concat(flipX(fwdR)), { note: 'forward of the loungers, either side of the mast, its crossbar\'s lights and the radar\'s bar, to where the deck narrows under ' + q(2 * xl) + ' m' });
    const yT1 = -6.80, xL = (SUN_LADDER.x[0] + SUN_LADDER.x[1]) / 2;
    climb('sun_deck_ladder', 'ladder', ['deck', 'deck', 'deck'], (MD + zHT(yT1)) / 2, yRamp(-8.10, yT1, MD, zHT(yT1)), rect(xL - 0.025, -8.10, xL + 0.025, yT1),
      { ladder: { via: 'hatch', hatchW: q(HATCH_T.x1 - HATCH_T.x0), hatchL: q(HATCH_T.y1 - HATCH_T.y0), rise: q(zHT(HATCH_T.y1) - MD), pitch: SUN_LADDER.pitch, rake: q(Math.atan(SUN_LADDER.k) / DEG) },
        note: 'the sun deck\'s ladder (decision 11): from the aft deck\'s starboard quarter up through the hardtop\'s hatch, raked top forward, out between the grab posts' });

    // ---- _excluded: what she has and the walker does not stand on, each with its reason
    const ex = (o) => { X.push(o); return o; }, hDC = q(Z_DC - DS);
    ex({ id: 'dining_port', of: 'area', z: DS, headroom: hDC, poly: tidy([[-1.70, 1.66], [-0.955, 1.66], [-0.955, 1.815], [-1.45, 1.815], [-1.45, 4.10], [-1.70, 4.10]]),
      reason: 'the dining\'s floor to port: the port steps\' foot and the strip between the bench and the table, 0.25 m: narrower than the walker; the bench is sat from it (sit_bench)' });
    ex({ id: 'crew_head', of: 'area', z: CS, headroom: q(crewHead(7.30) - CS), poly: tidy(rect(0.73, 7.30, 1.20, 7.88)),
      reason: 'the crew\'s head, beside its WC: under the walker\'s width; used by its interact (toilet_crew_head)' });
    ex({ id: 'crew_mess_table', of: 'area', z: CS, headroom: q(crewHead(6.95) - CS), poly: tidy(rect(-1.20, 6.95, -0.62, 7.85)),
      reason: 'the crew mess\'s table and the floor at the settee\'s front: the settee is sat from it (sit_crew_settee)' });
    ex({ id: 'engine_room', of: 'area', z: LS, reason: 'the engine room, drawn shut (decision 5): not walked' });
    ex({ id: 'tanks', of: 'area', z: LS, reason: 'the tank room under the dining and the helm: a void under its lid, not walked' });
    ex({ id: 'forepeak', of: 'area', z: CS, reason: 'the forepeak forward of the crew: a void under its lid, not walked' });
    ex({ id: 'coachroof', of: 'area', z: ZCR, reason: 'the coachroof forward of the hardtop: no rail, no way up' });
    ex({ id: 'windlass', of: 'area', z: q(deckZ(11.0) + 0.28), reason: 'the windlass\'s top, a hole in foredeck_3: worked from beside it (interact windlass)' });
    ex({ id: 'plinths', of: 'area', reason: 'the berths\' plinths, the counters, the cabinets and the tables: furniture, not standing surfaces' });
    ex({ id: 'sun_pads', of: 'area', z: q(zHT(-4.0) + 0.15), reason: 'the loungers\' pads: cushions, lain on, not stood on; the walk keeps the radius off them' });
    ex({ id: 'side_deck_blades', of: 'area', z: MD, reason: 'the side decks beside the hardtop\'s blades (y ' + yBl0 + ' to ' + yS0 + '): ' + pinch + ' m between a blade and the rail at its narrowest, the walker\'s ' + q(2 * R) + ' and more, but its centre has ' + room +
      ' m there, too little for an area; crossed on the washboards (washboards[]) by the washboard\'s rule, the way to the side decks and the foredeck' });
    ex({ id: 'house_steps_port', of: 'climb', kind: 'stair', at: pt(-1.47, (Y_ST + Y_S2) / 2, (HS + DS) / 2),
      reason: 'the house\'s port steps (decision 13) end in dining_port, 0.25 m between the bench and the table: a dead end, so they are left off the walk' });
    ex({ id: 'platform_ladder', of: 'climb', kind: 'ladder', at: pt(0, LADDER.y, PLATFORM_Z), ladder: { via: 'swim', platZ: q(PLATFORM_Z - DWL), platD: q(yP - LADDER.y) },
      reason: 'the swim ladder off the platform\'s aft edge (state ladder): from the water, not a walk area (swim)' });
    for (const b of BEDS) ex({ id: 'berth_' + b.id, of: 'berth', z: b.topZ, reason: 'the ' + b.id.replace('_', ' ') + ' berth: lain on, not stood on; sleeping is its interact (sleep_' + b.id + ')' });
    const SEATS = [['sofa_port', 'Sofa', -1.725, -3.85, 4.17], ['sofa_stbd', 'Sofa', 1.725, -3.85, 4.17], ['chair_aft', 'Dining chair', 0.075, 2.45, 3.60], ['chair_mid', 'Dining chair', 0.075, 3.05, 3.60],
      ['chair_fwd', 'Dining chair', 0.075, 3.65, 3.60], ['settee', 'Settee', 1.875, 2.75, 3.60], ['bench', 'Bench', -1.875, 3.05, 3.60], ['armchair_port_aft', 'Armchair', -0.93, -10.37, 4.17],
      ['armchair_port_fwd', 'Armchair', -0.93, -8.37, 4.17], ['armchair_stbd_aft', 'Armchair', 0.41, -10.37, 4.17], ['armchair_stbd_fwd', 'Armchair', 0.41, -8.37, 4.17],
      ['lounger_port', 'Lounger', -0.90, -4.00, zHT(-4.00) + 0.14], ['lounger_stbd', 'Lounger', 0.90, -4.00, zHT(-4.00) + 0.14], ['master_settee', 'Settee', 2.1, -4.9, 1.97],
      ['crew_settee', 'Settee', -1.5, 7.4, 2.45]].map(([id, label, x, y, z]) => ({ id, label, at: pt(x, y, z) }));
    for (const s of SEATS) ex({ id: s.id, of: 'seat', at: s.at, reason: 'sat on (sit_' + s.id + '), from its own deck\'s floor' });

    // ---- the interacts: the helm, the doors, the galley, the heads, the tables, the windlass, the swim, the berths, the seats
    const act = (o) => { I.push(o); return o; }, on = (id, x, y) => ({ reach: reach(id, x, y), area: id });
    const offW = (xid, x, y) => { const e = X.find(o => o.id === xid), p = nearOn({ id: xid, poly: e.poly, holes: [] }, x, y); return { reach: pt(p[0], p[1], e.z), area: null, offWalk: xid }; };
    act(Object.assign({ id: 'helm', action: 'enter_helm', label: 'Helm', prompt: 'Take the wheel', at: pt(...HELM.wheel), seat: null, stand: pt(...HELM.stand) }, on('dining_helm', HELM.stand[0], HELM.stand[1]),
      { note: 'a standing helm (no seat): the helmsman stands ' + q(HELM.wheel[1] - HELM.stand[1]) + ' m aft of the wheel\'s hub, its centre ' + q(HELM.wheel[2] - HELM.stand[2]) + ' m over the sole' }));
    const dAt = (d) => d.face === 'port' || d.face === 'starboard' ? [d.x, d.y] : [(d.x0 + d.x1) / 2, d.y];
    const door = (d, id, x, y) => act(Object.assign({ id: d.id, action: 'open_close', label: d.label, prompt: 'Open · shut', state: d.id === D.door.id ? 'doorOpen' : d.id, clearAt: d.clearAt,
      at: pt(...dAt(d), (d.sillZ + d.z1) / 2) }, on(id, x, y)));
    door(D.door, 'saloon_door', 0, HYA - 0.17);
    for (const [k, id, x, y] of [['garage_inner', 'garage', -1.40, -6.80], ['master', 'master_door', 0, -3.0], ['master_bath', 'master_bath_door', -1.45, -5.30], ['guest', 'guest_door', -0.70, -0.90],
      ['guest_head', 'guest_head_door', -1.45, 0], ['vip', 'vip_door', 0.70, -0.90], ['vip_head', 'vip_head_door', 1.45, 0], ['crew_head', 'crew_mess', 0.44, 7.28], ['crew_cabin', 'crew_cabin_door', 0.35, 7.80]])
      door(D.by[k], id, x, y);
    act(Object.assign({ id: 'garage', action: 'open_close', label: 'Transom door', prompt: 'Open · shut', state: 'garage', at: pt(-0.40, yTr(2.80), 2.80) }, on('garage', -0.40, -10.90),
      { note: 'the garage\'s door in the transom: down (garage 1) it is the swim platform' }));
    act(Object.assign({ id: 'stove', action: 'cook', label: 'Stove', prompt: 'Cook · brew up', at: pt(-1.95, -0.325, 4.655) }, on('saloon', -1.39, -0.325)));
    act(Object.assign({ id: 'sink', action: 'wash_dishes', label: 'Galley sink', prompt: 'Wash up', at: pt(-1.95, 0.55, 4.655) }, on('saloon', -1.39, 0.55)));
    for (const [id, x, y, area] of [['toilet_master_bath', -1.395, -3.56, 'master_bath'], ['toilet_guest_head', -1.375, 0.86, 'guest_head'], ['toilet_vip_head', 1.375, 0.86, 'vip_head']])
      act(Object.assign({ id, action: 'toilet', label: 'Head', prompt: 'Use the head', at: pt(x, y, 1.92) }, on(area, x, y)));
    act(Object.assign({ id: 'toilet_crew_head', action: 'toilet', label: 'Head', prompt: 'Use the head', at: pt(1.41, 7.665, 2.42) }, offW('crew_head', 0.96, 7.66)));
    act(Object.assign({ id: 'dine_port', action: 'dine', label: 'Table', prompt: 'Dine', at: pt(-0.93, -9.37, 4.49) }, on('aft_deck_port', -0.93, -9.37)));
    act(Object.assign({ id: 'dine_stbd', action: 'dine', label: 'Table', prompt: 'Dine', at: pt(0.41, -9.37, 4.49) }, on('aft_deck_stbd', 0.41, -9.37)));
    act(Object.assign({ id: 'windlass', action: 'anchor', label: 'Windlass', prompt: 'Let go / weigh anchor', at: pt(0, 11.0, deckZ(11.0) + 0.20) }, on('foredeck_2', 0, 10.55)));
    act(Object.assign({ id: 'swim', action: 'enter_water', label: 'Swim platform', prompt: 'Swim', at: pt(0, (yA + HINGE.y) / 2, PLATFORM_Z) }, on('platform', 0, yA + R)));
    const BED = { master: ['master_fwd', 'double', 'Master berth'], guest_out: ['guest', 'single', 'Guest berth'], guest_in: ['guest', 'single', 'Guest berth'], vip: ['vip', 'double', 'VIP berth'],
      crew_lower: ['crew_cabin', 'bunk', 'Crew bunk'], crew_upper: ['crew_cabin', 'bunk', 'Crew bunk'] };
    for (const b of BEDS) act(Object.assign({ id: 'sleep_' + b.id, action: 'sleep', label: BED[b.id][2], prompt: 'Sleep · save', at: pt((b.x[0] + b.x[1]) / 2, (b.y[0] + b.y[1]) / 2, b.topZ) },
      on(BED[b.id][0], (b.x[0] + b.x[1]) / 2, (b.y[0] + b.y[1]) / 2)));
    // each seat's floor as the writer finds it: the nearest below it within SEAT_REACH, never one under another deck
    const SEAT_REACH = 0.60, floors = A.filter(a => !a.kind).concat(X.filter(e => e.of === 'area' && typeof e.z === 'number' && Array.isArray(e.poly)).map(e => Object.assign({ holes: [] }, e)));
    for (const s of SEATS) { let best = null;
      for (const a of floors) { const z = zAt(a, s.at.x, s.at.y), hm = a.headroomMax != null ? a.headroomMax : a.headroom; if (!(z < s.at.z) || (hm != null && hm < s.at.z - z - 1e-9)) continue;
        const d = pip(a.poly, s.at.x, s.at.y) && !a.holes.some(h => pip(h, s.at.x, s.at.y)) ? 0 : edgeD(a.poly, [s.at.x, s.at.y]);
        if (d <= SEAT_REACH + 1e-9 && (!best || d < best.d - 1e-9 || (Math.abs(d - best.d) <= 1e-9 && z > best.z))) best = { a, d, z }; }
      if (!best) throw new Error('MotorYacht84Iso.walk: no floor within ' + SEAT_REACH + ' m of the seat ' + s.id);
      act(Object.assign({ id: 'sit_' + s.id, action: 'sit', label: s.label, prompt: 'Sit', at: s.at }, byId(best.a.id) ? on(best.a.id, s.at.x, s.at.y) : offW(best.a.id, s.at.x, s.at.y))); }

    // ---- the anchors, the cleats and the washboards
    const deckhead = { crew_lower: 3.18, crew_upper: q(Math.min(...span(7.99, 8.72, 8).map(crewHead))) };
    const anchors = { helm: pt(...HELM.stand), wheel: pt(...HELM.wheel), pilot: pt(...HELM.stand), ladder: pt(0, LADDER.y, PLATFORM_Z), platform: pt(0, (yA + HINGE.y) / 2, PLATFORM_Z),
      watercraft: [Object.assign({ id: 'watercraft_port' }, pt(-1.15, -9.30, GARAGE_SOLE)), Object.assign({ id: 'watercraft_stbd' }, pt(0.25, -9.30, GARAGE_SOLE))],
      berths: BEDS.map(b => ({ id: 'berth_' + b.id, interact: 'sleep_' + b.id, kind: BED[b.id][1], topZ: b.topZ, head: b.head, deckheadZ: deckhead[b.id] || q(ROOMS.find(r => r.id === 'master').ceil),
        poly: tidy(rect(b.x[0], b.y[0], b.x[1], b.y[1])) })) };
    const cleats = CLEATS.map(c => ({ id: (c.at[1] < -5 ? 'stern_' : c.at[1] < 5 ? 'mid_' : 'bow_') + (c.at[0] < 0 ? 'port' : 'star'), type: 'cleat', pos: c.at.slice() }));
    const washboards = [-1, 1].map(sd => ({ side: sd < 0 ? 'port' : 'starboard', width_m: q(sheerX(yS0) - 0.098 - HW), pinch_m: pinch,
      outer_edge: span(-8.30, yS0, 6).map(y => [q(sd * xin(y)), q(y), MD]),
      note: sd < 0 ? 'the side deck past the hardtop\'s blade, from the aft deck to side_deck_port: ' + pinch + ' m between the blade and the rail at its narrowest, the walker\'s ' + q(2 * R) + ' and more; ' +
        'climbed onto with Space from the aft deck and walked by the washboard\'s rule' : 'as port' }));
    return { schema: 'hidden-harbours/hull-walk@1', rig: RIG_FILE, exportSymbol: EXPORT_SYMBOL, hull: HULL_ID, label: LABEL, kind: 'ship', units: 'metres', frame: FRAME,
      loa_m: LOA, loh_m: L, beam_m: r3(BEAM_LOFT), DWL, cockpitZ: COCKPIT_Z,
      cell: { W: G.W, H: G.H, pivot: { x: G.cx, y: G.cy }, pxPerM: PX, defaultElev: DEFAULT_ELEV, order: ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'] },
      tags: { lv: Object.assign({}, LV), ro: Object.assign({}, RO), ghostRoles: GHOST_ROLES.slice() },
      walk: { radius: R, stepTol: WALK.stepTol, rule: NL_RULE, spawn: { x: -1.2, y: -7.3, area: 'aft_deck_fwd', note: 'on the aft deck, under the hardtop' }, areas: A },
      house: Object.assign({ door: clone(D.door), doors: clone(D.doors) }, nlHouseDecks(geometry().levels)), anchors, interact: I, cleats, washboards, _excluded: X,
      rock: Object.assign({}, ROCK), qa: { fitCheck: fitCheck(), surfaceFit: { foredeck: q(fit.foredeck), sunDeck: q(fit.sunDeck), tol: 0.02 } } };
  }
  const cell = { W: G.W, H: G.H, PX, pivot: { x: G.cx, y: G.cy }, defaultElev: DEFAULT_ELEV, cx: G.cx, cy: G.cy, S };
  const loft = { station: section, rowsAt, rows: NR, halfAtZ, sheerZ: fSz, sheerX, chineZ: fCz, chineX: fCx, keelZ: fKz, bottomZ: fCL, stemY, transomY: yTr, L, LOA, DWL, beam: r3(BEAM_LOFT),
    levels: { keel: 0, lowerSole: LOWER_SOLE, dwl: DWL, garageSole: GARAGE_SOLE, platform: PLATFORM_Z, mainDeck: MAIN_DECK, houseSole: HOUSE_SOLE, coachroof: ZCR, foredeckStep: Y_FD },
    shade: { GAIN, BIAS, LN, BAYER, KEY, EDGE, KEYLINE_DEFAULT }, cell };
  const API = { id: HULL_ID, label: LABEL, kind: 'ship', hullIds: [HULL_ID], defaultHull: HULL_ID, rigFile: RIG_FILE, exportSymbol: EXPORT_SYMBOL,
    loa: LOA, loaText: LOA_TEXT, loh: L, L, DWL, beam: r3(BEAM_LOFT), draft: DWL, cockpitZ: COCKPIT_Z, MAIN_DECK, HOUSE_SOLE, LOWER_SOLE, GARAGE_SOLE, PLATFORM_Z,
    W: G.W, H: G.H, PX, DIRS: 8, pivot: { x: G.cx, y: G.cy }, order: ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'], defaultElev: DEFAULT_ELEV,
    PAINTS, paintRamps, matsFor, get MATS() { return matsFor(DEFAULT).MATS; }, schemeIds: PAINTS.map(p => p.id), defaultScheme: DEFAULT, GAIN, BIAS, LN, BAYER, KEY, EDGE, KEYLINE_DEFAULT, cell,
    LV, RO, GHOST_ROLES, LEVEL_IDS: LV, ROLES: RO, FRAME, loft, states: STATES, faceList, partFaces, faceCount: faceList().length,
    DOOR: { part: 'garage', hinge: [0, r3(HINGE.y), r3(HINGE.z)], axis: [1, 0, 0], shutDeg: r3(DOOR_SHUT / DEG), note: 'garage 1 down (the platform), 0 shut; it turns about the hinge, top aft' },
    LADDER, STEPS: Object.assign({}, STEPS, { rise: r3(STEPS.rise), y0: r3(STEPS.y0), yTop: r3(STEPS.yTop) }), CLEATS, GEAR, render, ROCK, rock, project, _G: G,
    DINING_SOLE, interiorFaces: () => IF.slice(), interiorFaceCount: IF.length, fitCheck, doorFaces, leafTimes, renderCut, CUT_KEYS, HELM, WHEEL_GEO, ROOMS, BEDS, IDOORS, IFLIGHTS, get STAIR() { return STAIR; },
    geometry, get DOORS() { return clone(doorsOf().doors); }, DOOR_SET: DOOR_SET.map(d => Object.assign({}, d, { leaves: d.leaves.slice() })), partsList, WALK, CUTAWAY, GHOST, liftOf, walk };
  API.byId = (id) => id === HULL_ID ? API : null;
  root.MotorYacht84Iso = API;
})(typeof globalThis !== 'undefined' ? globalThis : window);
