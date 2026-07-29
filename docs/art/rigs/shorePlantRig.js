/* Hidden Harbours — SHORELINE PLANT RIG.  16 species across five tidal zones, one generator.
   The shore was the last habitat with no plant rig: the trees got one, the meadow flowers got one,
   and everything between the spruce line and the low-water mark was hand-drawn per tide state — so
   the same weed had three unrelated silhouettes depending on which artist drew which tide.

   WHAT THIS RIG KNOWS THAT THE OTHERS DON'T: the TIDE.  A shoreline plant is not one sprite with a
   wet variant. Height of water over its own ground decides four things at once, so they can never
   disagree:
     LAY   — algae need water to stand. Submerged rockweed is upright; drained, it collapses onto its
             rock in folds. That is the same skeleton with `lay` turned up, not a second drawing.
     WET   — glossy the moment it drains, bleached and near-black after a metre of fall. Derived from
             how far the tide has dropped past the plant's own base, so it is right for every zone at
             once (dead low leaves the fringe dripping and the high marsh bone dry).
     WATER — everything below the waterline takes the cold water ramp and loses contrast with depth.
             COLOUR ONLY: no dapple is baked, because the live shader's caustics are swell-driven and
             two uncorrelated patterns on one frond is worse than none. The crossing row is reported
             so a scene can line its water plane up with the bake.
     STILL — exposed limp weed does not move in air. The sway mode switches from breeze to surge at
             the waterline, which is most of why a low tide reads as low.

   THE RULES, enforced in code:
     1. MASS FLOOR, WITH A DECLARED EXEMPTION.  RIM_PX=2 must leave MIN_BODY=6 px behind it, so no
        BODY mass is emitted under MIN_R=5 px radius. But a grass blade IS 2 px wide — so blades,
        culms and fronds are declared STRAP: exempt from the floor, and in exchange forbidden a rim
        (they get a bake-derived lit spine and a dark edge instead). The exemption is a material, not
        a fudge, so the audit can measure it: 0 strap pixels may carry rim.
     2. SILHOUETTE, STRAP-AWARE.  Blades are spaced by arc length around the base and held apart at
        the tip; and the de-speckle pass SKIPS strap pixels — the tree rig's pass would have eaten
        every blade tip in this family, because a 1-px tip is signal here and noise there.
     3. THICKNESS-GATED RIM.  rim *= smoothstep(thickness) — unchanged from the tree rig, and now
        doing double duty as the enforcement of the rule-1 exemption.
     4. SUB-PIXEL DETAIL IS A DECISION.  At 32 px/m an Ascophyllum bladder is 0.6 px and a bayberry
        berry is 0.1 px. Nothing sub-pixel gets promoted to a mass: a bladder is a WIDTH BULGE on its
        strap, wax berries are single FLECK pixels. `report.promoted` says what was.

   SPEC: PPU 32 (32 px = 1 m) · ¾ from S at 40° (ADR-0006/0022, same camera as boats/rock/trees) ·
   bottom-centre HOLDFAST pivot · no AA · binary alpha · sheets ≤ 2048 px/axis.
   PALETTE: cold ambient bounce + ONE warm key. Wet weed is glossy, not neon.

   globalThis.ShorePlants:
     SPECIES · ZONES · TIDES · STAGES · PPU · RIM_PX · MIN_BODY · MIN_R · TIDE_M
     render(key,{variant,season,frame,tide,stage|size}) -> {w,h,pivot,rgba,masks,report,...}
     packMask(res)  R=key G=rim B=depth A=coverage      packState(res)  R=wet G=submerged B=strap A=α
     tideOf(t) · grey · massView · normalView · sheetSpec · cellOf
   Runs in the bake sandbox and in the browser. */
(function (root) {
  'use strict';

  const PPU = 32, RIM_PX = 2, MIN_BODY = 6;
  const MIN_R = Math.ceil((MIN_BODY + 2 * RIM_PX) / 2);   // 5 px radius → 10 px body mass
  const SWAY = 4, VARIANTS = 4;
  const SEASONS = ['summer', 'autumn', 'winter'];

  const ELEV = 40, CE = Math.cos(ELEV * Math.PI / 180), SE = Math.sin(ELEV * Math.PI / 180);
  const KEYLINE = '#101d21';
  const COLD = '#1d3b4a', WARM = '#e8b06a', WATER = '#27535e', FOAM = '#cfe2e0';

  // ---- tide -----------------------------------------------------------------
  // Nominal harbour range, mean low water → mean high water. The zone table below is in metres above
  // chart datum, so one number sets the whole staircase.
  const TIDE_M = 4.0;
  const DRY_M = 0.85;                                     // fall needed to go glossy → dried out
  const TIDES = [
    { key: 'low',   t: 0.00, label: 'DEAD LOW' },
    { key: 'ebb',   t: 0.30, label: 'EBB' },
    { key: 'half',  t: 0.55, label: 'HALF' },
    { key: 'flood', t: 0.80, label: 'FLOOD' },
    { key: 'high',  t: 1.00, label: 'HIGH' },
  ];
  const TIDE_KEYS = TIDES.map(t => t.key);
  const ZONES = [
    { key: 'fringe', name: 'Subtidal fringe',   base: 0.15, sub: 'bedrock · kelp',      note: 'bare only on spring lows' },
    { key: 'mid',    name: 'Mid intertidal',    base: 1.55, sub: 'cobble · rock',        note: 'bare twice a day' },
    { key: 'low',    name: 'Low marsh',         base: 2.55, sub: 'creek mud',            note: 'flooded every high water' },
    { key: 'high',   name: 'High marsh',        base: 3.35, sub: 'peat sod · brackish',  note: 'spring tides and surge only' },
    { key: 'upland', name: 'Upland & dune',     base: 4.65, sub: 'sand · headland',      note: 'never wetted — salt spray only' },
  ];
  const zoneOf = {}; ZONES.forEach(z => zoneOf[z.key] = z);

  // ---- vec / colour ----------------------------------------------------------
  const nrm = (v) => { const L = Math.hypot(v[0], v[1], v[2]) || 1; return [v[0] / L, v[1] / L, v[2] / L]; };
  const LIGHT = { key: nrm([-0.55, -0.66, 0.52]), rim: nrm([0.48, -0.28, -0.83]) };
  const HALF = nrm([LIGHT.key[0], LIGHT.key[1], LIGHT.key[2] + 1.0]);   // wet-gloss half vector
  const clamp = (v, a, b) => v < a ? a : v > b ? b : v;
  const smooth = (e0, e1, x) => { const t = clamp((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t); };
  const h2r = (h) => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];
  const r2h = (r) => '#' + r.map(v => clamp(Math.round(v), 0, 255).toString(16).padStart(2, '0')).join('');
  const mix = (a, b, t) => { const A = h2r(a), B = h2r(b); return r2h([0, 1, 2].map(i => A[i] + (B[i] - A[i]) * t)); };

  function rampOf(base, opt) {
    const o = opt || {};
    return {
      dp:  mix(mix(base, '#000000', 0.80), COLD, 0.36),
      sh:  mix(mix(base, '#000000', 0.56), COLD, 0.26),
      mid: mix(base, '#000000', 0.24),
      hi:  mix(base, WARM, 0.14),
      key: mix(mix(base, '#ffffff', 0.07), WARM, o.warm == null ? 0.34 : o.warm),
      rim: mix(WARM, '#fff3df', 0.14),
      gloss: mix(mix(base, '#ffffff', 0.55), WARM, 0.22),
    };
  }
  const WOOD_BASE = '#5a4634';
  const SNOW = { dp: '#5c7180', sh: '#7d93a0', mid: '#a8bcc4', hi: '#cfdde1', key: '#eef4f4', rim: '#fff6e6', gloss: '#fff' };

  // ---- species ---------------------------------------------------------------
  // h = the plant's TRUE standing height in world px (32 px = 1 m). unit says what one sprite IS:
  //   plant = one individual · clump = a hummock of many stems · mat = a square metre of cover.
  // limp = how much of its standing height it owes to the water (0 = holds itself up on air).
  const SPECIES = [
    { key: 'SugarKelp', name: 'Sugar Kelp', latin: 'Saccharina latissima', form: 'blade', zone: 'fringe',
      unit: 'plant', w: 70, h: 88, fol: '#7d6329', dry: '#33290f', bleach: '#b09a63', algae: true, limp: 0.92,
      blades: 3, bladeW: 7.5, ruffle: 1.5, sway: 3.4, stipe: true },
    { key: 'IrishMoss', name: 'Irish Moss', latin: 'Chondrus crispus', form: 'turf', zone: 'fringe',
      unit: 'mat', w: 46, h: 17, fol: '#7a3550', dry: '#3d1f2e', bleach: '#cdbb9c', algae: true, limp: 0.5,
      forks: 4, sway: 1.0 },
    { key: 'Eelgrass', name: 'Eelgrass', latin: 'Zostera marina', form: 'ribbonfan', zone: 'fringe',
      unit: 'clump', w: 62, h: 46, fol: '#416b34', dry: '#5d5a2c', bleach: '#a89a63', algae: false, limp: 0.62,
      blades: 11, bladeW: 2.1, sway: 3.0, brackish: true },
    { key: 'KnottedWrack', name: 'Knotted Wrack', latin: 'Ascophyllum nodosum', form: 'frond', zone: 'mid',
      unit: 'plant', w: 60, h: 52, fol: '#7d6b2c', dry: '#2e2711', bleach: '#9c8a52', algae: true, limp: 0.84,
      straps: 5, bladeW: 2.6, knots: 3.4, sway: 2.6 },
    { key: 'Bladderwrack', name: 'Bladderwrack', latin: 'Fucus vesiculosus', form: 'frond', zone: 'mid',
      unit: 'plant', w: 50, h: 34, fol: '#6b6b33', dry: '#2b2b13', bleach: '#938f5a', algae: true, limp: 0.80,
      straps: 6, bladeW: 3.2, knots: 5.5, midrib: true, sway: 2.2 },
    { key: 'SeaLettuce', name: 'Sea Lettuce', latin: 'Ulva lactuca', form: 'sheet', zone: 'mid',
      unit: 'mat', w: 44, h: 15, fol: '#5b8f3c', dry: '#6b7548', bleach: '#c2c9a0', algae: true, limp: 0.95,
      sheets: 4, sway: 2.0 },
    { key: 'Cordgrass', name: 'Saltmarsh Cordgrass', latin: 'Spartina alterniflora', form: 'culm', zone: 'low',
      unit: 'clump', w: 54, h: 58, fol: '#5c7d3a', fall: '#c2a552', limp: 0.0, culms: 7, bladeW: 2.4,
      panicle: true, sway: 1.9, brackish: true },
    { key: 'Glasswort', name: 'Glasswort', latin: 'Salicornia maritima', form: 'succulent', zone: 'low',
      unit: 'mat', w: 40, h: 15, fol: '#5f8a4e', fall: '#b8382f', limp: 0.0, stems: 9, sway: 0.8, winterBare: true },
    { key: 'SaltmeadowHay', name: 'Saltmeadow Hay', latin: 'Spartina patens', form: 'cowlick', zone: 'high',
      unit: 'mat', w: 58, h: 24, fol: '#7d8a4a', fall: '#cbb072', limp: 0.0, blades: 26, bladeW: 1.4,
      sway: 1.4, winterFlat: true },
    { key: 'BlackRush', name: 'Black Rush', latin: 'Juncus gerardii', form: 'rush', zone: 'high',
      unit: 'clump', w: 34, h: 21, fol: '#3b563e', fall: '#8a7d4a', limp: 0.0, culms: 15, sway: 1.0, seedHead: true },
    { key: 'Cattail', name: 'Cattail', latin: 'Typha latifolia', form: 'cattail', zone: 'high',
      unit: 'clump', w: 46, h: 66, fol: '#547a3c', fall: '#b8a05a', limp: 0.0, culms: 4, bladeW: 3.0,
      spike: '#5a3a24', sway: 2.2, brackish: true, fresh: true },
    { key: 'Threesquare', name: 'Threesquare Bulrush', latin: 'Schoenoplectus pungens', form: 'rush', zone: 'high',
      unit: 'clump', w: 36, h: 36, fol: '#4c7040', fall: '#a89250', limp: 0.0, culms: 9, stiff: true,
      sway: 1.5, seedHead: true, brackish: true, fresh: true },
    { key: 'MarramGrass', name: 'Marram Grass', latin: 'Ammophila breviligulata', form: 'dune', zone: 'upland',
      unit: 'clump', w: 50, h: 33, fol: '#7d9470', fall: '#bcb07a', limp: 0.0, culms: 17, sway: 1.7, rolled: true },
    { key: 'Bayberry', name: 'Bayberry', latin: 'Morella pensylvanica', form: 'shrub', zone: 'upland',
      unit: 'plant', w: 74, h: 58, fol: '#3c5f3c', fall: '#4a5c3a', limp: 0.0, rings: 5, sway: 1.1,
      fleck: '#aeb6a4', flecks: 15 },
    { key: 'SweetFern', name: 'Sweet Fern', latin: 'Comptonia peregrina', form: 'fernshrub', zone: 'upland',
      unit: 'plant', w: 56, h: 37, fol: '#5c7038', fall: '#9c5a2c', limp: 0.0, stems: 7, bladeW: 2.7, sway: 1.5 },
    { key: 'BeachPea', name: 'Beach Pea', latin: 'Lathyrus japonicus', form: 'sprawl', zone: 'upland',
      unit: 'mat', w: 78, h: 16, fol: '#648a5c', fall: '#8a8a4a', limp: 0.0, runners: 5, sway: 1.0,
      fleck: '#8464ad', flecks: 7 },
  ];
  const byKey = {}; SPECIES.forEach(s => { byKey[s.key] = s; s.worldH = s.h; s.z = zoneOf[s.zone]; });

  const STAGES = { new: 0.36, grown: 0.68, full: 1.00 };
  const STAGE_KEYS = ['new', 'grown', 'full'];
  function sizeOf(o) {
    if (o && typeof o.size === 'number') return clamp(o.size, 0.2, 1.3);
    if (o && STAGES[o.stage]) return STAGES[o.stage];
    return 1;
  }
  const stageName = (t) => t < 0.5 ? 'new' : t < 0.85 ? 'grown' : 'full';

  const hashKey = (k) => { let h = 2166136261; for (let i = 0; i < k.length; i++) { h ^= k.charCodeAt(i); h = Math.imul(h, 16777619); } return h >>> 0; };
  const rngOf = (a) => function () { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; };

  // ---- the tide, resolved for one species ------------------------------------
  // Everything downstream reads THIS object. One place decides, so lay/wet/water can't disagree.
  function tideOf(t, sp) {
    const tt = clamp(t == null ? 0.55 : t, 0, 1);
    const waterM = tt * TIDE_M;
    if (!sp) return { t: tt, waterM };
    const baseM = sp.z.base, hM = sp.worldH / PPU;
    const overM = waterM - baseM;                       // metres of water standing over its own foot
    const sub = clamp(overM / Math.max(0.12, hM), 0, 1.35);
    const drowned = sub >= 1;
    const exposed = overM <= 0;
    // wet: soaked while any part is under; then dries off over DRY_M of further fall. An upland plant
    // never gets a foot of tide over it, so this is 0 for the whole cycle without a special case.
    const wet = exposed ? clamp(1 - (-overM) / DRY_M, 0, 1) : 1;
    // lay: a limp plant owes its height to the water. Drained, it folds onto its substrate.
    const lay = clamp(sp.limp * (1 - smooth(0.02, 0.55, sub)), 0, 1);
    return {
      t: tt, waterM, baseM, overM, sub, drowned, exposed, wet, lay,
      mode: sub > 0.08 ? 'surge' : 'breeze',
      // the plant's legal band: below it, it drowns; above it, it dries out for good.
      legal: !(sp.z.key === 'upland' && overM > 0.05),
      still: exposed && sp.limp > 0.6,
    };
  }

  // ---- volume buffer ---------------------------------------------------------
  const M = { NONE: 0, BODY: 1, STRAP: 2, WOOD: 3, FLECK: 4 };
  const MAT_NAME = ['none', 'body', 'strap', 'wood', 'fleck'];
  function Vol(w, h) {
    const n = w * h;
    this.w = w; this.h = h;
    this.z = new Float32Array(n).fill(-1e9);
    this.nx = new Float32Array(n); this.ny = new Float32Array(n); this.nz = new Float32Array(n);
    this.mat = new Uint8Array(n); this.a = new Uint8Array(n); this.id = new Int16Array(n).fill(-1);
  }
  Vol.prototype.clearPx = function (i) { this.a[i] = 0; this.mat[i] = 0; this.z[i] = -1e9; this.id[i] = -1; };
  Vol.prototype.put = function (x, y, z, nx, ny, nz, mat, id) {
    if (x < 0 || y < 0 || x >= this.w || y >= this.h) return;
    const i = y * this.w + x;
    if (z <= this.z[i]) return;
    const L = Math.hypot(nx, ny, nz) || 1;
    this.z[i] = z; this.nx[i] = nx / L; this.ny[i] = ny / L; this.nz[i] = nz / L;
    this.mat[i] = mat; this.a[i] = 1; this.id[i] = id;
  };

  // ellipsoid front surface (body masses: shrub foliage, succulent joints, moss cushion)
  function blob(v, cx, cy, cz, rx, ry, rz, mat, id, seed) {
    const p1 = (seed || 0) * 1.7, p2 = (seed || 0) * 3.1;
    const x0 = Math.max(0, Math.floor(cx - rx * 1.3)), x1 = Math.min(v.w - 1, Math.ceil(cx + rx * 1.3));
    const y0 = Math.max(0, Math.floor(cy - ry * 1.3)), y1 = Math.min(v.h - 1, Math.ceil(cy + ry * 1.3));
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
      let u = (x + 0.5 - cx) / rx, w = (y + 0.5 - cy) / ry;
      const th = Math.atan2(w, u);
      const k = 1 + 0.13 * Math.sin(3 * th + p1) + 0.08 * Math.sin(5 * th + p2);
      u /= k; w /= k;
      const s = u * u + w * w;
      if (s > 1) continue;
      const t = Math.sqrt(1 - s), z = cz + t * rz;
      v.put(x, y, z, u / rx, w / ry, t / rz, mat, id);
    }
  }
  // swept sphere (stems, culms, rolled leaves, the cattail spike)
  function limb(v, x0, y0, z0, x1, y1, z1, r0, r1, mat, id, flat) {
    const dx = x1 - x0, dy = y1 - y0, L2 = dx * dx + dy * dy || 1e-6, R = Math.max(r0, r1) + 1;
    const ax = Math.max(0, Math.floor(Math.min(x0, x1) - R)), bx = Math.min(v.w - 1, Math.ceil(Math.max(x0, x1) + R));
    const ay = Math.max(0, Math.floor(Math.min(y0, y1) - R)), by = Math.min(v.h - 1, Math.ceil(Math.max(y0, y1) + R));
    for (let y = ay; y <= by; y++) for (let x = ax; x <= bx; x++) {
      let t = ((x + 0.5 - x0) * dx + (y + 0.5 - y0) * dy) / L2; t = clamp(t, 0, 1);
      const px = x0 + dx * t, py = y0 + dy * t, pz = z0 + (z1 - z0) * t, r = Math.max(0.62, r0 + (r1 - r0) * t);
      const ox = x + 0.5 - px, oy = y + 0.5 - py, d2 = ox * ox + oy * oy;
      if (d2 > r * r) continue;
      const k = Math.sqrt(Math.max(0, r * r - d2));
      v.put(x, y, pz + k, ox / r, oy / r * (flat ? 0.18 : 0.35), Math.max(0.25, k / r), mat, id);
    }
  }

  // ---- the new primitive: a STRAP ---------------------------------------------
  // A blade / frond / culm: a strip swept along a 3D path with a rounded cross-section, so the lit
  // spine and the dark edges are bake outputs of real normals — not a painted highlight. `twist`
  // rolls the cross-section out of the screen plane, which is how a blade foreshortens to 1 px and
  // then opens up again along its length. That roll is the whole reason grass reads as grass.
  function strap(v, path, o) {
    const id = o.id, mat = o.mat == null ? M.STRAP : o.mat;
    const hw0 = o.hw0, hw1 = o.hw1 == null ? 0.55 : o.hw1, thick = o.thick == null ? 0.9 : o.thick;
    const tw = o.twist || 0, twRate = o.twistRate || 0, curv = o.curv == null ? 0.85 : o.curv;
    const ruffle = o.ruffle || 0, knots = o.knots || 0, seed = o.seed || 0;
    // total path length in screen px, for arc-length parametrisation of width and twist
    const seg = [];
    let total = 0;
    for (let i = 1; i < path.length; i++) {
      const L = Math.hypot(path[i][0] - path[i - 1][0], path[i][1] - path[i - 1][1]);
      seg.push(L); total += L;
    }
    if (total < 0.5) return;
    let run = 0;
    for (let i = 1; i < path.length; i++) {
      const p0 = path[i - 1], p1 = path[i], L = seg[i - 1];
      const steps = Math.max(1, Math.ceil(L / 0.55));
      let tx = (p1[0] - p0[0]) / (L || 1), ty = (p1[1] - p0[1]) / (L || 1);
      const tz = (p1[2] - p0[2]) / (L || 1);
      for (let s = 0; s < steps; s++) {
        const f = (run + L * (s / steps)) / total;              // 0 base → 1 tip
        const cx = p0[0] + (p1[0] - p0[0]) * (s / steps);
        const cy = p0[1] + (p1[1] - p0[1]) * (s / steps);
        const cz = p0[2] + (p1[2] - p0[2]) * (s / steps);
        let hw = hw0 + (hw1 - hw0) * Math.pow(f, o.taper == null ? 1 : o.taper);
        // RULE 4: a bladder is 0.6 px across at this scale, so it is never a mass — it is a bulge in
        // the strap's own width. Same for the knots on knotted wrack, which is where its name is.
        if (knots) hw *= 1 + 0.42 * Math.max(0, Math.sin(f * Math.PI * knots + seed));
        if (ruffle) hw *= 1 + ruffle * 0.16 * Math.sin(f * 19 + seed * 3.3);
        const phi = tw + twRate * f + (o.wobble || 0) * Math.sin(f * 7 + seed);
        const cph = Math.cos(phi), sph = Math.sin(phi);
        // across-vector in screen, perpendicular to the path
        const axs = -ty, ays = tx;
        const across = Math.max(0.5, Math.abs(hw * cph));
        const us = Math.max(2, Math.ceil(across * 2 / 0.5));
        for (let k = 0; k <= us; k++) {
          const u = -1 + 2 * (k / us);
          const ox = axs * hw * u * cph, oy = ays * hw * u * cph;
          const oz = hw * u * sph + thick * Math.sqrt(Math.max(0, 1 - u * u));
          // face normal = tangent × across, then bowed across the width by `curv`
          const Ax = axs * cph, Ay = ays * cph, Az = sph;
          let nx = ty * Az - tz * Ay, ny = tz * Ax - tx * Az, nz = tx * Ay - ty * Ax;
          if (nz < 0) { nx = -nx; ny = -ny; nz = -nz; }        // face the camera
          nx += Ax * u * curv; ny += Ay * u * curv; nz += Az * u * curv * 0.5;
          v.put(Math.round(cx + ox), Math.round(cy + oy), cz + oz, nx, ny, nz, mat, id);
        }
      }
      run += L;
    }
  }

  // single-pixel detail (wax berries, pea bloom) — declared FLECK so it is exempt from the mass
  // floor and takes no rim. This is the honest home for anything under a pixel that must still read.
  function fleck(v, x, y, z, r, id) {
    const R = Math.max(0.7, r);
    for (let dy = -Math.ceil(R); dy <= Math.ceil(R); dy++) for (let dx = -Math.ceil(R); dx <= Math.ceil(R); dx++) {
      if (dx * dx + dy * dy > R * R) continue;
      v.put(Math.round(x) + dx, Math.round(y) + dy, z + 0.8, -0.4, -0.5, 0.76, M.FLECK, id);
    }
  }

  // ---- skeleton --------------------------------------------------------------
  // A blade's direction blends between standing and laid-out-on-the-ground by `lay`. Upright is
  // nearly vertical with a splay; laid is radial in the ground plane, which under the 40° camera
  // puddles it downslope around the holdfast — the drained-rockweed read, from one number.
  function dirOf(theta, lay, splay) {
    const sp = splay == null ? 0.22 : splay;
    const ux = sp * Math.sin(theta), uy = -1, uz = sp * Math.cos(theta);
    const lx = 0.97 * Math.sin(theta), ly = -0.07, lz = 0.97 * Math.cos(theta);
    return nrm([ux + (lx - ux) * lay, uy + (ly - uy) * lay, uz + (lz - uz) * lay]);
  }
  // walk a path from the base, bending progressively further toward laid/drooping along its length.
  // Two things happen as `lay` rises that a naive lerp misses: a limp frond CONCERTINAS (it puddles
  // around its holdfast, it does not stretch out flat), and it falls DOWNSLOPE — so the laid azimuth
  // swings toward the seaward fall of the rock rather than radiating evenly like a starfish.
  function path(x0, y0, z0, len, theta, lay, droop, segs, baseY, jitter, rng) {
    const pts = [[x0, y0, z0]];
    let x = x0, y = y0, z = z0;
    const n = segs || 7;
    let a = theta % (Math.PI * 2); if (a > Math.PI) a -= Math.PI * 2; if (a < -Math.PI) a += Math.PI * 2;
    for (let i = 0; i < n; i++) {
      const f = (i + 0.5) / n;
      const L = clamp(lay + droop * f * f, 0, 1);
      const dl = (len / n) * (1 - 0.34 * L);
      const d = dirOf(a * (1 - 0.42 * L), L, 0.22);
      x += d[0] * dl; y += d[1] * dl; z += d[2] * dl;
      if (jitter && rng) { x += (rng() * 2 - 1) * jitter; z += (rng() * 2 - 1) * jitter; }
      if (baseY != null && y > baseY - 0.4) y = baseY - 0.4;    // nothing goes under its own ground
      pts.push([x, y, z]);
    }
    return pts;
  }

  function build(sp, variant, season, size, tide) {
    const t = size == null ? 1 : size;
    const rng = rngOf(hashKey(sp.key) + variant * 7717 + (season === 'winter' ? 31 : season === 'autumn' ? 17 : 0) + Math.round(t * 997) + Math.round(tide.t * 613));
    const grow = smooth(0.2, 1, t);
    const H = Math.max(9, sp.worldH * t);
    const W = Math.max(14, sp.w * Math.pow(t, 0.7));
    const cx = Math.floor(W / 2) + 0.5, baseY = H + 2;
    const lay = tide.lay;
    const winter = season === 'winter';
    const flat = winter && sp.winterFlat ? 0.72 : 0;         // ice and snow lay the high marsh over
    const L = clamp(lay + flat, 0, 1);
    const straps = [], limbs = [], clumps = [], flecks = [];
    const promoted = [];
    const A = () => rng() * Math.PI * 2;

    switch (sp.form) {

      case 'blade': {   // sugar kelp: a stipe and long ruffled blades. Standing it is a column; drained
        // it is a heap of folds on the rock, which is the same path with `lay` up.
        const holdR = Math.max(2.2, 3.4 * grow);
        limbs.push([cx, baseY, 0, cx, baseY - holdR * 1.1, 0, holdR, holdR * 0.7, M.WOOD]);
        const nb = Math.max(1, Math.round(sp.blades * (0.5 + 0.5 * grow)));
        for (let i = 0; i < nb; i++) {
          const th = (i / nb) * Math.PI * 2 + rng() * 0.8;
          const len = H * (0.72 + rng() * 0.3);
          const stipeL = len * 0.22;
          const sp0 = path(cx, baseY - holdR, 0, stipeL, th, L * 0.8, 0.1, 3, baseY);
          limbs.push([sp0[0][0], sp0[0][1], sp0[0][2], sp0[sp0.length - 1][0], sp0[sp0.length - 1][1], sp0[sp0.length - 1][2], 1.6, 1.2, M.WOOD]);
          const tip = sp0[sp0.length - 1];
          const p = path(tip[0], tip[1], tip[2], len * 0.78, th, L, 0.55 + rng() * 0.4, 9, baseY, 0.5, rng);
          straps.push({ p, hw0: sp.bladeW * (0.55 + 0.45 * grow), hw1: sp.bladeW * 0.72, taper: 0.6,
            thick: 1.0, twist: 0.25 + rng() * 0.5, twistRate: 1.5 + rng(), ruffle: sp.ruffle,
            wobble: 0.5, seed: rng() * 6 });
        }
        promoted.push('blade edge ruffle folded into strap width');
        break;
      }

      case 'turf': {   // Irish moss: dichotomous flat branchlets over a cushion. At 32 PPU a single
        // frond is 3 px, so the SPRITE IS A MAT — the cushion is the mass, the fronds are its texture.
        const cw = W * 0.44, ch = H * 0.5;
        for (let i = 0; i < 5; i++) {
          const a = A(), rad = Math.sqrt(rng()) * cw * 0.62;
          clumps.push({ x: cx + Math.cos(a) * rad, y: baseY - ch * (0.28 + rng() * 0.2), z: Math.sin(a) * rad * 0.8,
            rx: Math.max(MIN_R, cw * 0.30), ry: Math.max(MIN_R * 0.62, ch * 0.42), rz: Math.max(MIN_R, cw * 0.28) });
        }
        const nf = Math.round(11 * (0.5 + 0.5 * grow));
        for (let i = 0; i < nf; i++) {
          const th = (i / nf) * Math.PI * 2 + rng() * 0.5, rad = cw * (0.2 + rng() * 0.7);
          const bx = cx + Math.cos(th) * rad, bz = Math.sin(th) * rad * 0.8;
          const len = H * (0.5 + rng() * 0.5);
          const p = path(bx, baseY - ch * 0.35, bz, len, th, clamp(L * 0.7 + 0.12, 0, 1), 0.3, 4, baseY);
          straps.push({ p, hw0: 1.5, hw1: 1.0, thick: 0.7, twist: rng() * 0.6, twistRate: 0.8, seed: rng() * 6, curv: 0.6 });
          // one fork — dichotomous is the whole look of Chondrus
          const mid = p[Math.max(1, p.length - 2)];
          for (const s of [-1, 1]) {
            const q = path(mid[0], mid[1], mid[2], len * 0.42, th + s * 0.5, clamp(L * 0.7 + 0.12, 0, 1), 0.35, 3, baseY);
            straps.push({ p: q, hw0: 1.1, hw1: 0.7, thick: 0.6, twist: rng() * 0.6, twistRate: 0.6, seed: rng() * 6, curv: 0.6 });
          }
        }
        promoted.push('one frond is 3 px — the unit is a mat, not a plant');
        break;
      }

      case 'ribbonfan': {   // eelgrass: ribbons from a sheath, fanned by arc length (rule 2)
        const nb = Math.max(3, Math.round(sp.blades * (0.45 + 0.55 * grow)));
        limbs.push([cx, baseY, 0, cx, baseY - H * 0.10, 0, 2.4, 1.8, M.WOOD]);
        for (let i = 0; i < nb; i++) {
          const th = (i / nb) * Math.PI * 2 + (rng() * 2 - 1) * 0.16;      // even ring, small jitter only
          const len = H * (0.62 + rng() * 0.42);
          const p = path(cx + Math.cos(th) * 1.6, baseY - H * 0.08, Math.sin(th) * 1.3, len, th, L, 0.42 + rng() * 0.35, 8, baseY, 0.35, rng);
          straps.push({ p, hw0: sp.bladeW, hw1: sp.bladeW * 0.55, thick: 0.75, twist: rng() * 1.1,
            twistRate: 1.2 + rng() * 1.4, seed: rng() * 6, curv: 0.7 });
        }
        break;
      }

      case 'frond': {   // rockweed: forking straps, bladders as width bulges, drapes when drained
        const holdR = Math.max(1.9, 2.8 * grow);
        limbs.push([cx, baseY, 0, cx, baseY - holdR, 0, holdR, holdR * 0.6, M.WOOD]);
        const ns = Math.max(2, Math.round(sp.straps * (0.45 + 0.55 * grow)));
        const forkOf = (p0, th, len, depth, hw) => {
          const p = path(p0[0], p0[1], p0[2], len, th, L, 0.30 + rng() * 0.3, 6, baseY, 0.35, rng);
          straps.push({ p, hw0: hw, hw1: hw * 0.66, thick: 0.85, twist: rng() * 0.8,
            twistRate: 0.9 + rng() * 0.8, knots: sp.knots, seed: rng() * 6,
            curv: sp.midrib ? 1.05 : 0.75 });
          if (depth > 0) {
            const tip = p[p.length - 1];
            for (const s of [-1, 1]) forkOf(tip, th + s * (0.38 + rng() * 0.3), len * (0.62 + rng() * 0.18), depth - 1, hw * 0.74);
          }
        };
        for (let i = 0; i < ns; i++) {
          const th = (i / ns) * Math.PI * 2 + rng() * 0.6;
          forkOf([cx + Math.cos(th) * 1.4, baseY - holdR * 0.8, Math.sin(th) * 1.1], th, H * (0.38 + rng() * 0.16), grow > 0.6 ? 2 : 1, sp.bladeW);
        }
        promoted.push('air bladders are 0.6 px — carried as strap width, never as masses');
        break;
      }

      case 'sheet': {   // sea lettuce: crumpled translucent sheets, almost no structure
        const ns = Math.max(2, Math.round(sp.sheets * (0.5 + 0.5 * grow)));
        for (let i = 0; i < ns; i++) {
          const th = (i / ns) * Math.PI * 2 + rng() * 0.9;
          const len = H * (0.7 + rng() * 0.5);
          const p = path(cx + (rng() * 2 - 1) * W * 0.16, baseY, (rng() * 2 - 1) * W * 0.12, len, th, clamp(L * 0.9 + 0.1, 0, 1), 0.5, 6, baseY, 0.6, rng);
          straps.push({ p, hw0: 5.2 + rng() * 2.4, hw1: 4.4, taper: 0.5, thick: 0.55,
            twist: 0.5 + rng() * 1.2, twistRate: 2.2 + rng() * 1.6, ruffle: 2.0, wobble: 0.9, seed: rng() * 6, curv: 0.95 });
        }
        break;
      }

      case 'culm': {   // cordgrass: stiff culms, two or three arching blades each, panicle at full size
        const nc = Math.max(2, Math.round(sp.culms * (0.4 + 0.6 * grow)));
        for (let i = 0; i < nc; i++) {
          const th = A(), rad = W * 0.20 * Math.sqrt(rng());
          const bx = cx + Math.cos(th) * rad, bz = Math.sin(th) * rad * 0.8;
          const hh = H * (0.62 + rng() * 0.42), tip = baseY - hh;
          const lean = (rng() * 2 - 1) * W * 0.09;
          limbs.push([bx, baseY, bz, bx + lean, tip, bz, 1.7, 1.0, M.STRAP]);
          const nbl = 2 + (rng() < 0.5 ? 1 : 0);
          for (let k = 0; k < nbl; k++) {
            const f = 0.28 + k * 0.26 + rng() * 0.1;
            const th2 = th + (k % 2 ? 1 : -1) * (0.8 + rng() * 0.8);
            const p = path(bx + lean * f, baseY - hh * f, bz, hh * (0.42 + rng() * 0.22), th2, 0.16, 0.72 + rng() * 0.4, 6, baseY, 0.2, rng);
            straps.push({ p, hw0: sp.bladeW, hw1: 0.6, taper: 0.85, thick: 0.7, twist: 0.3 + rng() * 0.9,
              twistRate: 1.6 + rng(), seed: rng() * 6, curv: 0.9 });
          }
          if (sp.panicle && t > 0.8 && rng() < 0.7) {
            const p = path(bx + lean, tip, bz, H * 0.16, th, 0.1, 0.5, 4, baseY);
            straps.push({ p, hw0: 1.9, hw1: 0.8, thick: 1.1, twist: 0.2, twistRate: 0.6,
              ruffle: 2.4, seed: rng() * 6, curv: 0.9 });
          }
        }
        break;
      }

      case 'succulent': {   // glasswort: jointed stems. A joint is 4 px at this scale — under the mass
        // floor — so it is carried the same way a bladder is: as a periodic BULGE in a strap's width.
        if (winter && sp.winterBare) {                    // frost kills it back to bleached sticks
          for (let i = 0; i < 7; i++) {
            const th = A(), rad = W * 0.2 * Math.sqrt(rng());
            limbs.push([cx + Math.cos(th) * rad, baseY, Math.sin(th) * rad * 0.8,
              cx + Math.cos(th) * rad * 1.3, baseY - H * (0.4 + rng() * 0.4), Math.sin(th) * rad, 1.1, 0.7, M.WOOD]);
          }
          break;
        }
        const ns = Math.max(3, Math.round(sp.stems * (0.4 + 0.6 * grow)));
        for (let i = 0; i < ns; i++) {
          const th = A(), rad = W * 0.24 * Math.sqrt(rng());
          const bx = cx + Math.cos(th) * rad, bz = Math.sin(th) * rad * 0.8;
          const hh = H * (0.5 + rng() * 0.5);
          const p = path(bx, baseY, bz, hh, th, 0.10, 0.14, 5, baseY);
          straps.push({ p, hw0: 2.5, hw1: 1.8, thick: 1.5, twist: 1.2, twistRate: 0.3,
            knots: Math.max(2, Math.round(hh / (MIN_R * 1.15))), seed: rng() * 6, curv: 1.15 });
          if (grow > 0.55 && rng() < 0.6) {                // a side branch or two once it is grown
            const q = p[Math.max(1, Math.round(p.length * 0.5))];
            const s = path(q[0], q[1], q[2], hh * 0.45, th + (rng() < 0.5 ? 0.7 : -0.7), 0.16, 0.2, 3, baseY);
            straps.push({ p: s, hw0: 2.0, hw1: 1.5, thick: 1.3, twist: 1.2, twistRate: 0.3,
              knots: 3, seed: rng() * 6, curv: 1.15 });
          }
        }
        promoted.push('succulent joints are 4 px — knots on a strap, the same answer as the bladders');
        break;
      }

      case 'cowlick': {   // saltmeadow hay: fine blades that all lie the same way in swirls
        const swirl = A();
        const nb = Math.max(6, Math.round(sp.blades * (0.4 + 0.6 * grow)));
        for (let i = 0; i < nb; i++) {
          const th = swirl + (rng() * 2 - 1) * 1.15;
          const rad = W * 0.34 * Math.sqrt(rng()), bx = cx + Math.cos(A()) * rad, bz = Math.sin(A()) * rad * 0.7;
          const len = H * (0.7 + rng() * 0.55);
          const p = path(bx, baseY, bz, len, th, clamp(0.42 + flat * 0.5, 0, 1), 0.62 + rng() * 0.3, 6, baseY, 0.3, rng);
          straps.push({ p, hw0: sp.bladeW, hw1: 0.55, taper: 0.9, thick: 0.6, twist: rng() * 1.3,
            twistRate: 1.1 + rng() * 1.2, seed: rng() * 6, curv: 0.75 });
        }
        promoted.push('blades are 1–3 px: STRAP, exempt from the floor, rim suppressed');
        break;
      }

      case 'rush': {   // black rush / threesquare: stiff round needles, seed head off to one side
        const nc = Math.max(4, Math.round(sp.culms * (0.4 + 0.6 * grow)));
        for (let i = 0; i < nc; i++) {
          const th = A(), rad = W * 0.26 * Math.sqrt(rng());
          const bx = cx + Math.cos(th) * rad, bz = Math.sin(th) * rad * 0.8;
          const hh = H * (0.6 + rng() * 0.44);
          const tipLean = sp.stiff ? 0.06 : 0.20;
          const ex = bx + Math.cos(th) * hh * tipLean, ez = bz + Math.sin(th) * hh * tipLean * 0.8;
          limbs.push([bx, baseY, bz, ex, baseY - hh, ez, 1.5, 0.75, M.STRAP]);
          if (sp.seedHead && t > 0.6 && rng() < 0.55) {
            const f = 0.78;
            flecks.push({ x: bx + (ex - bx) * f + Math.cos(th) * 2.2, y: baseY - hh * f, z: bz + (ez - bz) * f, r: 1.3 });
          }
        }
        break;
      }

      case 'cattail': {   // typha: strap leaves + the spike. The spike is 2 px because 2 px is the floor.
        const nc = Math.max(1, Math.round(sp.culms * (0.4 + 0.6 * grow)));
        for (let i = 0; i < nc; i++) {
          const th = A(), rad = W * 0.16 * Math.sqrt(rng());
          const bx = cx + Math.cos(th) * rad, bz = Math.sin(th) * rad * 0.8;
          const hh = H * (0.66 + rng() * 0.38);
          const lean = (rng() * 2 - 1) * W * 0.06;
          const spikeTop = baseY - hh;
          limbs.push([bx, baseY, bz, bx + lean, spikeTop + hh * 0.20, bz, 1.5, 1.2, M.STRAP]);
          if (t > 0.7) {
            // the head: brown spike over a thin tail. Two masses, both at the floor, no smaller.
            limbs.push([bx + lean, spikeTop + hh * 0.21, bz, bx + lean, spikeTop + hh * 0.055, bz, 2.1, 2.1, M.BODY, 'spike']);
            limbs.push([bx + lean, spikeTop + hh * 0.05, bz, bx + lean, spikeTop, bz, 0.9, 0.7, M.WOOD]);
          }
          const nbl = 2 + (rng() < 0.6 ? 1 : 0);
          for (let k = 0; k < nbl; k++) {
            const th2 = th + (k % 2 ? 1 : -1) * (0.7 + rng() * 0.9);
            const p = path(bx, baseY, bz, hh * (0.72 + rng() * 0.28), th2, 0.14, 0.66 + rng() * 0.34, 8, baseY, 0.25, rng);
            straps.push({ p, hw0: sp.bladeW, hw1: 0.7, taper: 0.9, thick: 0.8, twist: 0.35 + rng() * 0.9,
              twistRate: 1.5 + rng() * 1.1, seed: rng() * 6, curv: 0.95 });
          }
        }
        break;
      }

      case 'dune': {   // marram: leaves are ROLLED — a cylinder, not a strap. Different primitive.
        const nc = Math.max(5, Math.round(sp.culms * (0.4 + 0.6 * grow)));
        for (let i = 0; i < nc; i++) {
          const th = A(), rad = W * 0.20 * Math.sqrt(rng());
          const bx = cx + Math.cos(th) * rad, bz = Math.sin(th) * rad * 0.8;
          const hh = H * (0.55 + rng() * 0.5), arc = 0.20 + rng() * 0.35;
          const mx = bx + Math.cos(th) * hh * arc * 0.5, mz = bz + Math.sin(th) * hh * arc * 0.4;
          limbs.push([bx, baseY, bz, mx, baseY - hh * 0.62, mz, 1.5, 1.1, M.STRAP]);
          limbs.push([mx, baseY - hh * 0.62, mz, bx + Math.cos(th) * hh * arc, baseY - hh, bz + Math.sin(th) * hh * arc * 0.8, 1.1, 0.68, M.STRAP]);
        }
        // sand collects around the clump: a low body mass, which is the whole point of marram
        clumps.push({ x: cx, y: baseY - MIN_R * 0.5, z: 0, rx: W * 0.30, ry: MIN_R * 0.7, rz: W * 0.24, sand: true });
        promoted.push('rolled leaves are cylinders — swept sphere, not strap');
        break;
      }

      case 'shrub': {   // bayberry: the one form that behaves like a small tree
        const nr = Math.max(2, Math.round(sp.rings * (0.4 + 0.6 * grow)));
        const trunkR = Math.max(1.4, W * 0.030 * (0.6 + 0.4 * grow));
        limbs.push([cx, baseY, 0, cx, baseY - H * 0.24, 0, trunkR * 1.5, trunkR, M.WOOD]);
        const cw = Math.min(W * 0.42, ((W / 2) - 2.5) / 1.2), chh = H * 0.40, cyc = baseY - H * 0.58;
        for (let i = 0; i < 5; i++) {
          const a = A();
          limbs.push([cx, baseY - H * 0.22, 0, cx + Math.cos(a) * cw * 0.6, cyc + chh * 0.4, Math.sin(a) * cw * 0.5, trunkR, trunkR * 0.4, M.WOOD]);
        }
        const nl = Math.max(5, Math.round((2 * Math.PI * cw) / (2.2 * MIN_R)));
        for (let i = 0; i < nl; i++) {
          const a = (i / nl) * Math.PI * 2 + (rng() * 2 - 1) * 0.09;
          const rr = Math.max(MIN_R, cw * (0.24 + rng() * 0.06));
          clumps.push({ x: cx + Math.cos(a) * (cw - rr * 0.6), y: cyc + Math.sin(a) * (chh - rr * 0.55),
            z: (rng() * 2 - 1) * cw * 0.24, rx: rr * 1.04, ry: rr * 0.92, rz: rr });
        }
        for (let i = 0; i < nr * 3; i++) {
          const a = A(), rad = Math.sqrt(rng()) * 0.7, rr = Math.max(MIN_R, cw * (0.2 + rng() * 0.06));
          clumps.push({ x: cx + Math.cos(a) * cw * rad, y: cyc + (rng() * 2 - 1) * chh * rad,
            z: -cw * 0.2 + Math.sin(a) * cw * rad * 0.8, rx: rr, ry: rr * 0.9, rz: rr });
        }
        if (sp.flecks && !winter) for (let i = 0; i < Math.round(sp.flecks * grow); i++) {
          const a = A(), rad = Math.sqrt(rng()) * 0.9;
          flecks.push({ x: cx + Math.cos(a) * cw * rad, y: cyc + (rng() * 2 - 1) * chh * 0.85, z: cw * (0.3 + rng() * 0.5), r: 0.9, seed: 0 });
        }
        promoted.push('wax berries are 0.1 px — single FLECK pixels or nothing');
        break;
      }

      case 'fernshrub': {   // sweet fern: woody stems carrying long pinnate straps
        const ns = Math.max(2, Math.round(sp.stems * (0.4 + 0.6 * grow)));
        for (let i = 0; i < ns; i++) {
          const th = A(), rad = W * 0.14 * Math.sqrt(rng());
          const bx = cx + Math.cos(th) * rad, bz = Math.sin(th) * rad * 0.8;
          const hh = H * (0.6 + rng() * 0.42), lean = Math.cos(th) * hh * 0.24;
          limbs.push([bx, baseY, bz, bx + lean, baseY - hh, bz + Math.sin(th) * hh * 0.2, 1.6, 0.9, M.WOOD]);
          const nl = 5 + Math.round(rng() * 2);
          for (let k = 0; k < nl; k++) {
            const f = 0.22 + (k / nl) * 0.72;
            const th2 = th + (k % 2 ? 1 : -1) * (1.0 + rng() * 0.5);
            const p = path(bx + lean * f, baseY - hh * f, bz + Math.sin(th) * hh * 0.2 * f,
              hh * (0.24 + rng() * 0.14), th2, 0.30, 0.5, 4, baseY);
            straps.push({ p, hw0: sp.bladeW, hw1: 0.7, thick: 0.7, twist: 0.4 + rng() * 0.7,
              twistRate: 0.9, ruffle: 2.6, seed: rng() * 6, curv: 0.8 });
          }
        }
        promoted.push('pinnate lobes folded into strap edge ruffle');
        break;
      }

      case 'sprawl': {   // beach pea: runners over sand, paired leaflets, one bloom
        const nr = Math.max(2, Math.round(sp.runners * (0.4 + 0.6 * grow)));
        for (let i = 0; i < nr; i++) {
          const th = (i / nr) * Math.PI * 2 + rng() * 0.7;
          const len = W * (0.28 + rng() * 0.2);
          const p = path(cx, baseY - 1.4, 0, len, th, 0.88, 0.08, 6, baseY);
          straps.push({ p, hw0: 1.2, hw1: 0.7, thick: 0.7, twist: 0.2, twistRate: 0.3, seed: rng() * 6, curv: 0.6, mat: M.WOOD });
          for (let k = 1; k < p.length; k++) {
            if (rng() < 0.45) continue;
            for (const s of [-1, 1]) {
              const rr = Math.max(MIN_R * 0.8, 4.2);
              clumps.push({ x: p[k][0] + Math.cos(th + s * 1.5) * rr * 0.7, y: p[k][1] - 1.6,
                z: p[k][2] + Math.sin(th + s * 1.5) * rr * 0.6, rx: rr * 0.9, ry: rr * 0.5, rz: rr * 0.8 });
            }
          }
          if (sp.flecks && !winter && rng() < 0.7) {
            const q = p[Math.max(1, p.length - 3)];
            flecks.push({ x: q[0], y: q[1] - 3.2, z: q[2], r: 1.2, seed: 0 });
          }
        }
        promoted.push('leaflets are raised to the 10 px floor — the floor sets the grain of the mat');
        break;
      }
    }

    return { W, H, cx, baseY, straps, limbs, clumps, flecks, rng, lay: L, promoted, winter };
  }

  // ---- camera projection + measured cell -------------------------------------
  const pyRel = (g, y, z) => -(g.baseY - y) * CE + z * SE;
  const pzOf = (g, y, z) => z * CE + (g.baseY - y) * SE;
  const WOBBLE = 1.28;

  function extents(g) {
    let top = 1e9, bot = -1e9, xl = 1e9, xr = -1e9;
    const acc = (x, y, r) => {
      if (y - r < top) top = y - r; if (y + r > bot) bot = y + r;
      if (x - r < xl) xl = x - r; if (x + r > xr) xr = x + r;
    };
    for (const c of g.clumps) acc(c.x, pyRel(g, c.y, c.z), Math.max(Math.max(MIN_R, c.rx), Math.hypot(Math.max(MIN_R, c.ry) * CE, Math.max(MIN_R, c.rz) * SE)) * WOBBLE);
    for (const L of g.limbs) {
      const r = Math.max(L[6], L[7]) + 1.2;
      acc(L[0], pyRel(g, L[1], L[2]), r); acc(L[3], pyRel(g, L[4], L[5]), r);
    }
    for (const s of g.straps) {
      const r = Math.max(s.hw0, s.hw1 == null ? 0 : s.hw1) * (1 + 0.42 * (s.knots ? 1 : 0)) * (1 + 0.16 * (s.ruffle || 0)) + (s.thick || 1) + 1.2;
      for (const p of s.p) acc(p[0], pyRel(g, p[1], p[2]), r);
    }
    for (const f of g.flecks) acc(f.x, pyRel(g, f.y, f.z), (f.r || 1) + 1.4);
    if (top > bot) { top = -1; bot = 1; xl = 0; xr = 1; }
    return { top, bot, xl, xr };
  }

  // One cell per species per growth stage, sized off the union of every variant × season × TIDE it can
  // produce. The tide is in that union on purpose: a standing kelp and a drained one are wildly
  // different shapes, and a sheet is only a grid if one cell holds both.
  function cellOf(sp, size) {
    const t = size == null ? 1 : size, ck = 'c' + Math.round(t * 1000);
    if (!sp._cells) sp._cells = {};
    if (sp._cells[ck]) return sp._cells[ck];
    const MG = 3;
    let top = 1e9, bot = -1e9, xl = 1e9, xr = -1e9;
    for (const season of ['summer', 'winter']) for (let v = 0; v < VARIANTS; v++) for (const td of TIDES) {
      const e = extents(build(sp, v, season, t, tideOf(td.t, sp)));
      if (e.top < top) top = e.top;
      if (e.bot > bot) bot = e.bot;
      if (e.xl < xl) xl = e.xl;
      if (e.xr > xr) xr = e.xr;
    }
    const pivotY = Math.ceil(MG - top);
    const h = Math.ceil(pivotY + bot + MG) + 1;
    const dx = Math.max(0, Math.ceil(MG - xl));
    const w = Math.max(20, dx + Math.ceil(xr + MG) + 1);
    const cell = { w, h, dx, pivotX: Math.round(dx + (xl + xr) / 2), pivotY, pad: h - 1 - pivotY, size: t };
    sp._cells[ck] = cell;
    return cell;
  }

  // ---- RULE 2: de-speckle, STRAP-AWARE ---------------------------------------
  // The tree rig's pass killed any pixel with ≤1 neighbour. Run that here and every blade tip in the
  // family disappears — a 1-px tip is the authored end of a strap, not noise. So: body masses get
  // de-speckled, straps and flecks are left alone, and pinholes are filled everywhere.
  function despeckle(v) {
    let removed = 0, spared = 0;
    for (let pass = 0; pass < 2; pass++) {
      const kill = [];
      for (let y = 0; y < v.h; y++) for (let x = 0; x < v.w; x++) {
        const i = y * v.w + x; if (!v.a[i]) continue;
        let n = 0;
        if (x > 0 && v.a[i - 1]) n++;
        if (x < v.w - 1 && v.a[i + 1]) n++;
        if (y > 0 && v.a[i - v.w]) n++;
        if (y < v.h - 1 && v.a[i + v.w]) n++;
        if (n > 1) continue;
        if (v.mat[i] === M.STRAP || v.mat[i] === M.FLECK) { if (pass === 0) spared++; continue; }
        kill.push(i);
      }
      for (const i of kill) { v.clearPx(i); removed++; }
      if (!kill.length) break;
    }
    for (let y = 1; y < v.h - 1; y++) for (let x = 1; x < v.w - 1; x++) {
      const i = y * v.w + x; if (v.a[i]) continue;
      if (v.a[i - 1] && v.a[i + 1] && v.a[i - v.w] && v.a[i + v.w]) {
        const src = v.z[i - 1] > v.z[i + 1] ? i - 1 : i + 1;
        v.a[i] = 1; v.mat[i] = v.mat[src]; v.z[i] = v.z[src] - 0.2; v.id[i] = v.id[src];
        v.nx[i] = v.nx[src]; v.ny[i] = v.ny[src]; v.nz[i] = v.nz[src];
      }
    }
    return { removed, spared };
  }

  function distField(a, w, h) {
    const D = new Float32Array(w * h), BIG = 1e6;
    for (let i = 0; i < w * h; i++) D[i] = a[i] ? BIG : 0;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x; if (!D[i]) continue; let d = D[i];
      if (y > 0) { d = Math.min(d, D[i - w] + 3); if (x > 0) d = Math.min(d, D[i - w - 1] + 4); if (x < w - 1) d = Math.min(d, D[i - w + 1] + 4); }
      if (x > 0) d = Math.min(d, D[i - 1] + 3);
      D[i] = d;
    }
    for (let y = h - 1; y >= 0; y--) for (let x = w - 1; x >= 0; x--) {
      const i = y * w + x; if (!D[i]) continue; let d = D[i];
      if (y < h - 1) { d = Math.min(d, D[i + w] + 3); if (x > 0) d = Math.min(d, D[i + w - 1] + 4); if (x < w - 1) d = Math.min(d, D[i + w + 1] + 4); }
      if (x < w - 1) d = Math.min(d, D[i + 1] + 3);
      D[i] = d;
    }
    for (let i = 0; i < w * h; i++) D[i] /= 3;
    return D;
  }

  // ---- RULE 1 audit: the floor polices BODY masses. STRAP, WOOD and FLECK are LINEAR elements —
  // exempt by declaration, and policed instead by the rim gate, which is the thing the floor exists
  // to protect in the first place.
  function massReport(a, mat, D, w, h) {
    const lab = new Int32Array(w * h).fill(-1), stack = [], comps = [];
    for (let s = 0; s < w * h; s++) {
      if (!a[s] || lab[s] >= 0) continue;
      const id = comps.length; let px = 0, maxd = 0, body = 0, lin = 0;
      lab[s] = id; stack.push(s);
      while (stack.length) {
        const i = stack.pop(); px++; if (D[i] > maxd) maxd = D[i];
        if (mat[i] === M.BODY) body++; else lin++;
        const x = i % w, y = (i / w) | 0;
        if (x > 0 && a[i - 1] && lab[i - 1] < 0) { lab[i - 1] = id; stack.push(i - 1); }
        if (x < w - 1 && a[i + 1] && lab[i + 1] < 0) { lab[i + 1] = id; stack.push(i + 1); }
        if (y > 0 && a[i - w] && lab[i - w] < 0) { lab[i - w] = id; stack.push(i - w); }
        if (y < h - 1 && a[i + w] && lab[i + w] < 0) { lab[i + w] = id; stack.push(i + w); }
      }
      comps.push({ id, px, maxd, bodyDepth: Math.max(0, (maxd - RIM_PX) * 2), linear: lin >= body });
    }
    const real = comps.filter(c => c.px >= 6);
    const bodyComps = real.filter(c => !c.linear);
    const fail = bodyComps.filter(c => c.bodyDepth < MIN_BODY);
    let bodyPx = 0, total = 0;
    for (let i = 0; i < w * h; i++) if (a[i]) { total++; if (D[i] > RIM_PX) bodyPx++; }
    return {
      masses: real.length, bodyMasses: bodyComps.length, linearMasses: real.length - bodyComps.length,
      failed: fail.length, pass: fail.length === 0,
      minBody: bodyComps.length ? Math.round(Math.min.apply(null, bodyComps.map(c => c.bodyDepth)) * 10) / 10 : 0,
      bodyRatio: total ? Math.round(bodyPx / total * 100) : 0, lab, comps,
    };
  }

  // ---- shade ----------------------------------------------------------------
  const STEPS = [[0.07, 'dp'], [0.18, 'sh'], [0.36, 'mid'], [0.62, 'hi'], [1.4, 'key']];
  function bandOf(l) { for (let i = 0; i < STEPS.length; i++) if (l < STEPS[i][0]) return STEPS[i][1]; return 'key'; }
  function vnoise(x, y, s) { const n = Math.sin(x * 127.1 + y * 311.7 + s * 74.7) * 43758.5453; return n - Math.floor(n); }
  function snoise(x, y, s) {
    const xi = Math.floor(x), yi = Math.floor(y), fx = x - xi, fy = y - yi;
    const u = fx * fx * (3 - 2 * fx), vv = fy * fy * (3 - 2 * fy);
    const a = vnoise(xi, yi, s), b = vnoise(xi + 1, yi, s), c = vnoise(xi, yi + 1, s), d = vnoise(xi + 1, yi + 1, s);
    return a + (b - a) * u + (c - a) * vv + (a - b - c + d) * u * vv;
  }

  // one place decides a species' colour for a season and a tide. Algae dry from saturated to nearly
  // black, and the sun-bleachers go the other way — that is the same `wet` number, opposite sign.
  function plantColour(sp, season, tide) {
    let base = sp.fol;
    if (season === 'autumn' && sp.fall) base = mix(base, sp.fall, 0.82);
    else if (season === 'winter') base = sp.fall ? mix(base, sp.fall, 0.94) : mix(base, '#33555c', 0.30);
    if (sp.algae) {
      const dry = 1 - tide.wet;
      base = mix(base, sp.dry || mix(base, '#000000', 0.55), dry * 0.85);
      if (sp.bleach) base = mix(base, sp.bleach, Math.pow(dry, 2.2) * 0.45);
      if (season === 'winter') base = mix(base, '#4e5f5c', 0.22);
    }
    return base;
  }

  function shade(v, sp, season, D, o, tide, waterRow) {
    const w = v.w, h = v.h, n = w * h;
    const FOL = rampOf(plantColour(sp, season, tide));
    const WOODR = rampOf(mix(WOOD_BASE, COLD, 0.2), { warm: 0.42 });
    const SPIKE = rampOf(sp.spike || '#5a3a24', { warm: 0.3 });
    const FLK = rampOf(sp.fleck || '#c2c9b6', { warm: 0.2 });
    const SAND = rampOf('#8d8266', { warm: 0.28 });
    const rgba = new Uint8ClampedArray(n * 4);
    const mFront = new Uint8Array(n), mRim = new Uint8Array(n), mDepth = new Uint8Array(n);
    const mWet = new Uint8Array(n), mSub = new Uint8Array(n);
    let zmin = 1e9, zmax = -1e9;
    for (let i = 0; i < n; i++) if (v.a[i]) { if (v.z[i] < zmin) zmin = v.z[i]; if (v.z[i] > zmax) zmax = v.z[i]; }
    const zr = Math.max(1, zmax - zmin);
    const sd = (sp.key.length * 13 + sp.h) % 97;
    const K = LIGHT.key, R = LIGHT.rim;
    const RSl = Math.hypot(R[0], R[1]) || 1, RS = [R[0] / RSl, R[1] / RSl];
    const snowy = season === 'winter' && sp.z.key === 'upland';

    const OCC = new Float32Array(n);
    const ring = [[2, 0], [1, 1], [0, 2], [-1, 1], [-2, 0], [-1, -1], [0, -2], [1, -1], [3, 1], [-3, 1], [1, 3], [-1, 3]];
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x; if (!v.a[i]) continue;
      let occ = 0;
      for (const [dx, dy] of ring) {
        const jx = x + dx, jy = y + dy;
        if (jx < 0 || jy < 0 || jx >= w || jy >= h) continue;
        const j = jy * w + jx; if (!v.a[j]) continue;
        const dz = v.z[j] - v.z[i];
        if (dz > 0.8) occ += clamp(dz / 5, 0, 1);
      }
      OCC[i] = clamp(1 - occ / ring.length * 2.0, 0.26, 1);
    }

    // local mass thickness (max of the distance field in a window) — rule 3's input
    const TH = new Float32Array(n), tmp = new Float32Array(n), RAD = 5;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      let m = 0; for (let k = -RAD; k <= RAD; k++) { const jx = x + k; if (jx < 0 || jx >= w) continue; const d = D[y * w + jx]; if (d > m) m = d; }
      tmp[y * w + x] = m;
    }
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      let m = 0; for (let k = -RAD; k <= RAD; k++) { const jy = y + k; if (jy < 0 || jy >= h) continue; const d = tmp[jy * w + x]; if (d > m) m = d; }
      TH[y * w + x] = m;
    }

    let strapPx = 0, strapRimLeak = 0, bodyPx = 0, thinBody = 0, subPx = 0, glossPx = 0, inkPx = 0;

    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x;
      if (!v.a[i]) { rgba[i * 4 + 3] = 0; continue; }
      const nx = v.nx[i], ny = v.ny[i], nz = v.nz[i], d = D[i], mt = v.mat[i];
      const isStrap = mt === M.STRAP, isFleck = mt === M.FLECK;
      inkPx++;
      if (isStrap) strapPx++; else if (mt === M.BODY) { bodyPx++; if (TH[i] * 2 < MIN_BODY + 2 * RIM_PX) thinBody++; }

      const lam = Math.pow(Math.max(0, nx * K[0] + ny * K[1] + nz * K[2]), 1.32);
      const nl = Math.hypot(nx, ny) || 1;
      const back = Math.max(0, (nx * RS[0] + ny * RS[1]) / nl);
      const fres = Math.pow(1 - clamp(nz, 0, 1), 1.7);
      // RULE 3 + the rule-1 exemption in one line: straps and flecks are gated out entirely, and a
      // body mass too thin to hold a rim never gets one either.
      const gate = (isStrap || isFleck) ? 0 : smooth(4.0, 5.2, TH[i]);
      let rim = Math.pow(back, 1.15) * fres * gate * smooth(3.6, 0.8, d);
      if (isStrap && rim > 0.16) strapRimLeak++;

      const submerged = y > waterRow;
      const ao = clamp(0.28 + 0.72 * Math.exp(-Math.max(0, d - 1) / 6.0), 0.28, 1) * OCC[i];      const zf = 0.64 + 0.36 * ((v.z[i] - zmin) / zr);
      const sky = 0.46 + 0.54 * clamp(-ny, 0, 1);
      const tex = (snoise(x / 3.0, y / 3.0, sd) - 0.5) * 0.15 + (snoise(x / 1.5, y / 1.5, sd + 7) - 0.5) * 0.07;

      // strap relief: the edge of a blade steps DOWN a band, its spine keeps the lit one. Together
      // with the bowed normal this is what stops a fan of blades reading as one green smear.
      let edge = 0;
      if (isStrap) edge = d <= 1.05 ? -1 : d <= 2.05 ? -0.4 : 0;
      let relief = 0, bias = 0;
      if (mt === M.BODY) {
        const up = i - w, lf = i - 1, dn = i + w, rt = i + 1;
        if (y > 0 && v.a[up] && v.id[up] !== v.id[i] && v.z[up] < v.z[i] - 1.0) relief += 1;
        if (x > 0 && v.a[lf] && v.id[lf] !== v.id[i] && v.z[lf] < v.z[i] - 1.0) relief += 1;
        if (y < h - 1 && v.a[dn] && v.id[dn] !== v.id[i] && v.z[dn] > v.z[i] + 1.0) relief -= 1;
        if (x < w - 1 && v.a[rt] && v.id[rt] !== v.id[i] && v.z[rt] > v.z[i] + 1.0) relief -= 1;
        bias = (vnoise(v.id[i] * 7 + 1, v.id[i] * 3 + 5, 11) - 0.5) * 0.08;
      } else if (isStrap) {
        bias = (vnoise(v.id[i] * 11 + 3, v.id[i] * 5 + 2, 19) - 0.5) * 0.10;
      }

      let lum = 0.11 * sky * ao * zf + 1.05 * lam * ao * zf + tex + relief * 0.05 + edge * 0.075 + bias;

      // KIT LAW · no tile bakes water, and no tile bakes MOVING LIGHT. The live sprite-light shader's
      // caustics are field-driven off the real swell, so a dapple baked in here would put two
      // uncorrelated patterns on the same frond. Submergence bakes COLOUR ONLY: darker, cooler, and
      // lower contrast with depth — monotone in depth, no spatial pattern. The dapple lands on top at
      // runtime exactly as it lands on the seabed behind it.
      if (submerged) {
        subPx++;
        const dep = clamp((y - waterRow) / 26, 0, 1);
        const haze = 0.16 + 0.28 * dep;
        lum = lum * (1 - haze) + 0.34 * haze;
        lum *= 0.94 - dep * 0.10;
        rim *= 0.42;
      }

      let ramp = FOL;
      if (mt === M.WOOD) ramp = WOODR;
      else if (isFleck) ramp = FLK;
      else if (mt === M.BODY && v.sandIds.has(v.id[i])) ramp = SAND;
      if (v.spikeIds.has(v.id[i])) ramp = SPIKE;
      if (snowy && -ny > 0.44 && d > 0.9 && lam > 0.05) { ramp = SNOW; lum = 0.36 + lum * 0.68; }

      let band = bandOf(clamp(lum, 0, 1.2));
      // a strap too thin to hold a specular key gets capped one band down — the same argument as the
      // rim gate, applied to the top of the ramp. Without it a 2-px culm reads as a bleached wire.
      if (isStrap && TH[i] < 1.9 && band === 'key') band = 'hi';
      let hex = ramp[band];
      if (rim > 0.16) hex = mix(hex, ramp.rim, clamp((rim - 0.12) * 1.35, 0, 0.95));

      // WET GLOSS: a hard specular dot, only where the plant is actually wet. This is the single
      // strongest cue that a tide has just dropped — the weed goes from matte to glinting.
      let spec = 0;
      if (tide.wet > 0.05 && !isFleck) {
        spec = Math.pow(Math.max(0, nx * HALF[0] + ny * HALF[1] + nz * HALF[2]), 26) * tide.wet * (submerged ? 0.35 : 1);
        if (spec > 0.26 && d >= 0.9) { hex = mix(hex, ramp.gloss, clamp(spec * 1.5, 0, 0.9)); glossPx++; }
      }
      if (submerged) {
        const dep = clamp((y - waterRow) / 30, 0, 1);
        hex = mix(hex, WATER, 0.26 + 0.30 * dep);
      }

      const c = h2r(hex);
      rgba[i * 4] = c[0]; rgba[i * 4 + 1] = c[1]; rgba[i * 4 + 2] = c[2]; rgba[i * 4 + 3] = 255;
      mFront[i] = clamp(Math.round(lam * 255), 0, 255);
      mRim[i] = clamp(Math.round(rim * 255), 0, 255);
      mDepth[i] = clamp(Math.round(((v.z[i] - zmin) / zr) * 255), 0, 255);
      mWet[i] = clamp(Math.round(tide.wet * 255), 0, 255);
      mSub[i] = submerged ? 255 : 0;
    }

    // waterline foam: a single bright row where the surface crosses the plant. One row, not a glow.
    if (waterRow > 0 && waterRow < h - 1) {
      const wr = Math.round(waterRow), fm = h2r(mix(FOAM, WATER, 0.35));
      for (let x = 0; x < w; x++) {
        const i = wr * w + x; if (!v.a[i]) continue;
        rgba[i * 4] = fm[0]; rgba[i * 4 + 1] = fm[1]; rgba[i * 4 + 2] = fm[2];
      }
    }

    if (o.outline !== false) {
      const kl = h2r(KEYLINE), add = [];
      for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
        const i = y * w + x; if (v.a[i]) continue;
        for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
          const jx = x + dx, jy = y + dy; if (jx < 0 || jy < 0 || jx >= w || jy >= h) continue;
          if (v.a[jy * w + jx]) { add.push(i); dx = 2; dy = 2; }
        }
      }
      for (const i of add) { rgba[i * 4] = kl[0]; rgba[i * 4 + 1] = kl[1]; rgba[i * 4 + 2] = kl[2]; rgba[i * 4 + 3] = 255; }
    }
    return { rgba, masks: { front: mFront, rim: mRim, depth: mDepth, wet: mWet, sub: mSub },
      strapPx, strapRimLeak, bodyPx, thinBody, subPx, glossPx, inkPx, TH };
  }

  // ---- sway: breeze above the water, surge below -----------------------------
  // Exposed limp weed gets NO air sway at all. That stillness is most of why a low tide reads as low.
  function swayShear(buffers, w, h, baseY, frame, sp, tide, waterRow) {
    if (frame == null || !frame) return buffers;
    const curve = Math.sin(frame / SWAY * Math.PI * 2);
    const surge = tide.mode === 'surge';
    const airAmp = tide.still ? 0.2 : sp.sway * (tide.exposed ? 0.75 : 1);
    const wetAmp = sp.sway * 1.55;
    const out = buffers.map(b => new b.constructor(b.length));
    const stride = buffers.map(b => b.length / (w * h));
    for (let y = 0; y < h; y++) {
      const hf = clamp((baseY - y) / Math.max(1, baseY), 0, 1);
      let dx;
      if (surge && y > waterRow) {
        // a travelling wave, not a shear: the water column moves as one, phase-lagged with depth
        const lag = (y - waterRow) * 0.16;
        dx = Math.round(wetAmp * Math.sin(frame / SWAY * Math.PI * 2 - lag) * Math.pow(hf, 0.85));
      } else {
        dx = Math.round(airAmp * curve * Math.pow(hf, 1.5));
      }
      for (let x = 0; x < w; x++) {
        const sx = x - dx; if (sx < 0 || sx >= w) continue;
        for (let b = 0; b < buffers.length; b++) {
          const st = stride[b], si = (y * w + sx) * st, di = (y * w + x) * st;
          for (let c = 0; c < st; c++) out[b][di + c] = buffers[b][si + c];
        }
      }
    }
    return out;
  }

  // ---- public ----------------------------------------------------------------
  function render(key, o) {
    o = o || {};
    const sp = byKey[key] || SPECIES[0];
    const season = SEASONS.indexOf(o.season) >= 0 ? o.season : 'summer';
    const size = sizeOf(o);
    const variant = ((o.variant | 0) % VARIANTS + VARIANTS) % VARIANTS;
    const tide = tideOf(o.tide == null ? 0.55 : o.tide, sp);
    const g = build(sp, variant, season, size, tide);
    const cell = cellOf(sp, size);
    const v = new Vol(cell.w, cell.h);
    const pivot = { x: cell.pivotX, y: cell.pivotY };
    const py = (y, z) => pivot.y + pyRel(g, y, z);
    const pz = (y, z) => pzOf(g, y, z);
    const px = (x) => x + cell.dx;
    v.sandIds = new Set(); v.spikeIds = new Set();
    let id = 0, floored = 0;

    for (const L of g.limbs) {
      const me = id++;
      limb(v, px(L[0]), py(L[1], L[2]), pz(L[1], L[2]), px(L[3]), py(L[4], L[5]), pz(L[4], L[5]),
        L[6], L[7], L[8], me, L[8] === M.STRAP);
      if (L[9] === 'spike') v.spikeIds.add(me);
    }
    for (const c of g.clumps) {
      const me = id++;
      // RULE 1, enforced at the EMITTER rather than trusted to sixteen author sites: no body mass
      // leaves here under MIN_R in any axis. `floored` counts what had to be raised.
      const rx = Math.max(MIN_R, c.rx), ry = Math.max(MIN_R, c.ry), rz = Math.max(MIN_R, c.rz);
      if (c.rx < MIN_R || c.ry < MIN_R || c.rz < MIN_R) floored++;
      blob(v, px(c.x), py(c.y, c.z), pz(c.y, c.z), rx,
        Math.hypot(ry * CE, rz * SE), Math.hypot(ry * SE, rz * CE), M.BODY, me, me);
      if (c.sand) v.sandIds.add(me);
    }
    for (const s of g.straps) {
      strap(v, s.p.map(p => [px(p[0]), py(p[1], p[2]), pz(p[1], p[2])]), {
        id: id++, mat: s.mat, hw0: s.hw0, hw1: s.hw1, thick: s.thick, twist: s.twist,
        twistRate: s.twistRate, curv: s.curv, ruffle: s.ruffle, knots: s.knots, taper: s.taper,
        wobble: s.wobble, seed: s.seed,
      });
    }
    for (const f of g.flecks) {
      if (f.r == null) continue;
      fleck(v, px(f.x), py(f.y, f.z), pz(f.y, f.z), f.r, id++);
    }

    // where the tide crosses THIS sprite, in its own rows. Reported so a scene can line its water
    // plane up with the bake instead of guessing.
    const waterRow = tide.overM > 0 ? pivot.y - tide.overM * PPU * CE : 1e9;

    const dsp = despeckle(v);
    const D = distField(v.a, v.w, v.h);
    const audit = massReport(v.a, v.mat, D, v.w, v.h);
    const sh = shade(v, sp, season, D, o, tide, waterRow);

    let rgba = sh.rgba, mf = sh.masks.front, mr = sh.masks.rim, md = sh.masks.depth, mw = sh.masks.wet, ms = sh.masks.sub;
    if (o.frame) {
      const out = swayShear([rgba, mf, mr, md, mw, ms], v.w, v.h, pivot.y, o.frame, sp, tide, waterRow);
      rgba = out[0]; mf = out[1]; mr = out[2]; md = out[3]; mw = out[4]; ms = out[5];
    }

    const strapShare = Math.round(sh.strapPx / Math.max(1, sh.strapPx + sh.bodyPx) * 100);
    // a row outside the sprite is not a row: above the top means wholly submerged, below the bottom
    // means a dry bake. Either way a scene consumer gets null and reads `submerged` instead.
    const wr = (waterRow > v.h || waterRow <= 0) ? null : Math.round(waterRow);
    return {
      w: v.w, h: v.h, pivot, rgba, masks: { front: mf, rim: mr, depth: md, wet: mw, sub: ms },
      alpha: v.a, dist: D, mat: v.mat, nx: v.nx, ny: v.ny, nz: v.nz, thick: sh.TH,
      species: sp, season, variant, size, stage: stageName(size), tide,
      waterRow: wr, submerged: waterRow <= 0,
      straps: g.straps.length, limbs: g.limbs.length, clumps: g.clumps.length, flecks: g.flecks.length,
      report: {
        pass: audit.pass && sh.strapRimLeak === 0 && tide.legal,
        masses: audit.masses, bodyMasses: audit.bodyMasses, linearMasses: audit.linearMasses,
        failed: audit.failed, minBody: audit.minBody, bodyRatio: audit.bodyRatio, floored,
        despeckled: dsp.removed, spared: dsp.spared,
        strapRimLeak: sh.strapRimLeak, strapShare,
        thinBodyPct: Math.round(sh.thinBody / Math.max(1, sh.bodyPx) * 1000) / 10,
        subPct: Math.round(sh.subPx / Math.max(1, sh.inkPx) * 100),
        glossPx: sh.glossPx,
        metres: Math.round(sp.worldH * size / PPU * 100) / 100,
        promoted: g.promoted,
        legal: tide.legal,
        drowned: tide.drowned,
        underFloor: sp.worldH * size < 12,
      },
      _audit: audit,
    };
  }

  function packMask(res) {
    const n = res.w * res.h, out = new Uint8ClampedArray(n * 4);
    for (let i = 0; i < n; i++) {
      out[i * 4] = res.masks.front[i]; out[i * 4 + 1] = res.masks.rim[i];
      out[i * 4 + 2] = res.masks.depth[i]; out[i * 4 + 3] = res.rgba[i * 4 + 3];
    }
    return out;
  }
  // the tide bake: R = wetness · G = below the waterline · B = strap (no-rim) · A = coverage
  function packState(res) {
    const n = res.w * res.h, out = new Uint8ClampedArray(n * 4);
    for (let i = 0; i < n; i++) {
      out[i * 4] = res.masks.wet[i]; out[i * 4 + 1] = res.masks.sub[i];
      out[i * 4 + 2] = res.mat[i] === M.STRAP ? 255 : 0; out[i * 4 + 3] = res.rgba[i * 4 + 3];
    }
    return out;
  }
  function grey(mask, res, tint) {
    const n = res.w * res.h, out = new Uint8ClampedArray(n * 4), t = tint ? h2r(tint) : null;
    for (let i = 0; i < n; i++) {
      const a = res.rgba[i * 4 + 3]; if (!a) { out[i * 4 + 3] = 0; continue; }
      const g = mask[i];
      out[i * 4] = t ? g * t[0] / 255 : g; out[i * 4 + 1] = t ? g * t[1] / 255 : g; out[i * 4 + 2] = t ? g * t[2] / 255 : g;
      out[i * 4 + 3] = 255;
    }
    return out;
  }
  // rule-1 view: which pixels are body (and pass), which are the declared strap exemption
  function massView(res) {
    const n = res.w * res.h, out = new Uint8ClampedArray(n * 4);
    const RIMC = h2r('#e8b06a'), BODY = h2r('#2f6a4c'), CORE = h2r('#8fd6a0'), BAD = h2r('#d2453c'),
      STR = h2r('#5b93b8'), STRE = h2r('#9ec6de'), WD = h2r('#3f4f57'), FL = h2r('#d9b47c');
    for (let i = 0; i < n; i++) {
      if (!res.alpha[i]) { out[i * 4 + 3] = 0; continue; }
      const d = res.dist[i], mt = res.mat[i];
      let c;
      if (mt === M.STRAP) c = d <= 1.05 ? STRE : STR;
      else if (mt === M.FLECK) c = FL;
      else if (mt === M.WOOD) c = WD;
      else c = (res.thick[i] * 2 < MIN_BODY + 2 * RIM_PX) ? BAD : d <= RIM_PX ? RIMC : d >= RIM_PX + MIN_BODY / 2 ? CORE : BODY;
      out[i * 4] = c[0]; out[i * 4 + 1] = c[1]; out[i * 4 + 2] = c[2]; out[i * 4 + 3] = 255;
    }
    return out;
  }
  function tideView(res) {
    const n = res.w * res.h, out = new Uint8ClampedArray(n * 4);
    const SUB = h2r('#2f7d94'), WETC = h2r('#8fd6a0'), DRY = h2r('#a8956a');
    for (let i = 0; i < n; i++) {
      if (!res.alpha[i]) { out[i * 4 + 3] = 0; continue; }
      const base = res.masks.sub[i] ? SUB : res.masks.wet[i] > 128 ? WETC : DRY;
      const k = res.masks.sub[i] ? 1 : 0.35 + 0.65 * (res.masks.wet[i] / 255);
      out[i * 4] = base[0] * k; out[i * 4 + 1] = base[1] * k; out[i * 4 + 2] = base[2] * k; out[i * 4 + 3] = 255;
    }
    return out;
  }
  function normalView(res) {
    const n = res.w * res.h, out = new Uint8ClampedArray(n * 4);
    for (let i = 0; i < n; i++) {
      if (!res.alpha[i]) { out[i * 4 + 3] = 0; continue; }
      out[i * 4] = (res.nx[i] * 0.5 + 0.5) * 255; out[i * 4 + 1] = (-res.ny[i] * 0.5 + 0.5) * 255;
      out[i * 4 + 2] = (res.nz[i] * 0.5 + 0.5) * 255; out[i * 4 + 3] = 255;
    }
    return out;
  }

  function sheetSpec(key, size, axis) {
    const sp = byKey[key] || SPECIES[0], t = size == null ? 1 : size, cell = cellOf(sp, t);
    const cols = axis === 'tide' ? TIDES.length : VARIANTS;
    const w = cell.w * cols, h = cell.h * SWAY;
    return { cell: [cell.w, cell.h], cols, rows: SWAY, w, h, fits: w <= 2048 && h <= 2048, ppu: PPU,
      pivot: [cell.pivotX, cell.pivotY], pad: cell.pad, elev: ELEV, stage: stageName(t),
      axis: axis === 'tide' ? 'tide × sway' : 'variant × sway',
      metres: Math.round(sp.worldH * t / PPU * 100) / 100 };
  }

  // ---- the numbers the import gate is decided on ------------------------------
  // Cells stay UNIONED over the tide states on purpose: one cell and one ground-contact pivot per
  // species means a runtime tide-state swap is anchored by construction, and a state hop is a visible
  // artifact where sheet waste is only memory. Any exception is decided on measured numbers, per
  // species, and this is where they are measured. The silent failure is the 2048 axis cap.
  function inkOf(res) {
    let x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9;
    for (let y = 0; y < res.h; y++) for (let x = 0; x < res.w; x++) if (res.alpha[y * res.w + x]) {
      if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
    }
    return { w: Math.max(0, x1 - x0 + 1), h: Math.max(0, y1 - y0 + 1) };
  }
  const _auditCache = {};
  function sheetAudit(size) {
    const t = size == null ? 1 : size, ck = 'a' + Math.round(t * 1000);
    if (_auditCache[ck]) return _auditCache[ck];
    let bytes = 0, allFit = true, worstAll = 100, worstKey = null;
    const rows = SPECIES.map(sp => {
      const cell = cellOf(sp, t), vs = sheetSpec(sp.key, t), ts = sheetSpec(sp.key, t, 'tide');
      let worst = 1e9, worstTide = null, best = 0;
      for (const td of TIDES) {
        const r = render(sp.key, { variant: 0, season: 'summer', tide: td.t, size: t, frame: 0 });
        const ink = inkOf(r), ratio = ink.w * ink.h / (cell.w * cell.h);
        if (ratio < worst) { worst = ratio; worstTide = td.key; }
        if (ratio > best) best = ratio;
      }
      const px = vs.w * vs.h + ts.w * ts.h;
      bytes += px * 4;
      const fits = vs.fits && ts.fits;
      if (!fits) allFit = false;
      const wp = Math.round(worst * 100);
      if (wp < worstAll) { worstAll = wp; worstKey = sp.key; }
      return {
        key: sp.key, name: sp.name, zone: sp.zone, unit: sp.unit,
        cell: [cell.w, cell.h], pivot: [cell.pivotX, cell.pivotY],
        worstInk: wp, worstTide, bestInk: Math.round(best * 100),
        variantSheet: [vs.w, vs.h], tideSheet: [ts.w, ts.h], fits,
        headroom: 2048 - Math.max(vs.w, vs.h, ts.w, ts.h),
        kb: Math.round(px * 4 / 1024),
      };
    });
    const out = { rows, mb: Math.round(bytes / 1048576 * 100) / 100, fits: allFit, cap: 2048,
      worstInk: worstAll, worstInkSpecies: worstKey, stage: stageName(t), size: t };
    _auditCache[ck] = out;
    return out;
  }

  // The strap exemption is a CONTRACT, not a look. A consuming shader must branch on it from data —
  // never by guessing from the sprite — so the rig states it in machine-readable form, serialised
  // straight out of the live rig, which is why it cannot drift from the bake.
  function contract(size) {
    const audit = sheetAudit(size);
    return {
      rig: 'shorePlantRig', version: 1, generated: new Date().toISOString(),
      projection: { ppu: PPU, elev: ELEV, heightScale: +CE.toFixed(4), depthScale: +SE.toFixed(4),
        pivot: 'ground contact (holdfast / root crown), bottom-centre of the union cell',
        antialias: false, alpha: 'binary', keyline: KEYLINE },
      materials: {
        body:  { id: M.BODY,  linear: false, massFloorPx: MIN_R, carriesRim: true,
          note: 'obeys the mass floor: no radius under MIN_R in any axis, clamped at the emitter' },
        strap: { id: M.STRAP, linear: true,  massFloorPx: null, carriesRim: false,
          note: 'blades, culms, fronds, sheets. Exempt from the mass floor by declaration; forbidden a rim.' },
        wood:  { id: M.WOOD,  linear: true,  massFloorPx: null, carriesRim: 'thickness-gated',
          note: 'stems and holdfasts. Linear, so not policed by the floor; the rim gate decides per pixel.' },
        fleck: { id: M.FLECK, linear: true,  massFloorPx: null, carriesRim: false,
          note: 'single-pixel detail (wax berries, bloom). Flat, no rim, no relief.' },
      },
      // WHAT THE SHADER MUST KNOW. G is NOT re-used as a spine channel on straps: it is identically
      // zero there. The strap's lit spine is real N·L off the bowed cross-section normal, so it is
      // already in R with the rest of the key light — no per-material reinterpretation of a channel.
      bakes: {
        light: { file: '<key>_light.png',
          R: 'key light · N·L from the fixed upper-left key · ALL materials, straps included — the strap spine lives here',
          G: 'back rim · DEFINED FOR BODY AND WOOD ONLY. Identically 0 on strap and fleck pixels — not a spine, not reinterpreted. Gate every read on state.B.',
          B: 'surface depth, normalised per sprite',
          A: 'coverage' },
        state: { file: '<key>_tide.png',
          R: 'wetness 0–255 · gloss authority',
          G: '255 where the pixel is below the baked waterline',
          B: '255 on STRAP pixels — the no-rim flag. This is the branch. Read it, do not infer it.',
          A: 'coverage' },
      },
      water: {
        bakesWater: false, bakesCaustics: false, bakesMovingLight: false,
        note: 'Submergence bakes COLOUR only — darker, cooler, contrast falling monotonically with depth. No dapple, no pattern, no per-frame light. The live sprite-light shader owns the moving caustics so they stay correlated with the swell.',
      },
      tide: {
        rangeM: TIDE_M, dryFallM: DRY_M,
        steps: TIDES.map(t => ({ key: t.key, t: t.t, waterM: +(t.t * TIDE_M).toFixed(2) })),
        derives: ['lay', 'wet', 'submergence', 'swayMode'],
        input: 'water height over the plant\u2019s own ground = tideM - zone.base. Maps onto the painted seabed height plus the deterministic tide; no new sim.',
        waterRow: 'row index in sprite space where the surface crosses. null when the surface is outside the sprite — read `submerged` to tell which side.',
      },
      zones: ZONES.map(z => ({ key: z.key, name: z.name, baseM: z.base, substrate: z.sub })),
      sway: { frames: SWAY, modes: ['breeze', 'surge', 'still'],
        note: 'pinned at the pivot row; breeze above the waterline, phase-lagged surge below, still when drained and limp' },
      sheets: { axisA: 'variant × sway', axisB: 'tide × sway', cap: 2048,
        cellPolicy: 'ONE union cell and ONE pivot per species per growth stage, unioned over 4 variants × 2 seasons × 5 tides. Runtime tide-state swaps are pivot-stable by construction.',
        totalMb: audit.mb, allFit: audit.fits },
      species: SPECIES.map(s => {
        const a = audit.rows.find(r => r.key === s.key);
        return { key: s.key, name: s.name, latin: s.latin, zone: s.zone, baseM: s.z.base,
          heightM: +(s.worldH / PPU).toFixed(2), unit: s.unit, limp: s.limp, algae: !!s.algae,
          brackish: !!s.brackish, freshTolerant: !!s.fresh,
          cell: a.cell, pivot: a.pivot, variantSheet: a.variantSheet, tideSheet: a.tideSheet,
          worstInkPct: a.worstInk, worstInkTide: a.worstTide, fits2048: a.fits };
      }),
      asserts: [
        'every sheet axis <= 2048 (Unity downscales silently; only a pivot assert catches it)',
        'state.B (strap flag) must gate every read of light.G',
        'no strap pixel carries light.G > 0',
        'pivot is identical across all tide states of a species (union cell invariant)',
        'shader must not add a second caustic to an already-dappled sprite: none is baked',
      ],
    };
  }

  root.ShorePlants = {
    PPU, RIM_PX, MIN_BODY, MIN_R, SWAY, VARIANTS, SEASONS, SPECIES, byKey, LIGHT, KEYLINE, COLD, WARM,
    WATER, FOAM, ELEV, CE, SE, TIDE_M, DRY_M, TIDES, TIDE_KEYS, ZONES, zoneOf, STAGES, STAGE_KEYS,
    M, MAT_NAME, sizeOf, stageName, tideOf,
    render, packMask, packState, grey, massView, tideView, normalView, sheetSpec, cellOf, plantColour,
    inkOf, sheetAudit, contract,
  };
})(typeof globalThis !== 'undefined' ? globalThis : window);
