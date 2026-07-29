/* Hidden Harbours — TREE RIG.  Acadian forest, 10 species × 4 variants, one generator.
   Replaces the hand-drawn tree sheets: the trees were the only prop family with no rig, which is
   why their masks had to be guessed from alpha. This one builds real volume (spheres + swept-sphere
   limbs into a z-buffer), so every pixel carries a NORMAL — the front-light and back-rim channels
   are exact bake outputs for the whole family at once, not a per-sprite chore.

   THE THREE RULES, enforced in code (not in a style guide):
     1. MASS.  RIM_PX=2 rim must leave MIN_BODY=6 px of interior behind it, so no foliage blob is
        emitted below MIN_R = (6 + 2·2)/2 = 5 px radius. Rings whose radius can't carry that many
        clumps drop their count instead of shrinking their clumps (that is the Tree41 failure).
        `report.lobes` measures the result per connected mass — pass/fail, measured, per sprite.
     2. SILHOUETTE.  Outline is authored, not accidental: the outer lobes of every crown sit on an
        even angular ring; a de-speckle pass then removes any 1-px hair/notch before shading, so the
        rim channel traces a deliberate edge. `report.despeckled` counts what it had to remove.
     3. THICKNESS-GATED RIM.  rim *= smoothstep(2,4, localThickness) — a mass too thin to hold a rim
        never gets one, so thin twigs read as twigs instead of collapsing into flat glow.

   SPEC: PPU 32 (32 px = 1 m) · bottom-centre TRUNK pivot (wind sway anchors there) · no AA ·
   binary alpha · sheets ≤ 2048 px/axis (asserted in sheetSpec) · upper-left key (art bible §1).
   PALETTE: mechanism, not neon. Cold ambient (#1d3b4a bounce) + ONE warm key (#e8b06a). The rim is
   a warm edge in a cold wood, never a glowing forest.

   globalThis.TreeRig:
     SPECIES [{key,name,latin,form,w,h,fol,...}]  VARIANTS  SWAY  SEASONS  PPU  RIM_PX  MIN_BODY  MIN_R
     render(key, {variant, season, frame, mode}) -> {w,h,pivot,rgba,masks:{front,rim,depth},report,clumps}
     packMask(res) -> RGBA  (R=key light · G=back rim · B=depth · A=coverage)  — the shader bake
     grey(mask,w,h)  sheetSpec(key)  LIGHT
   Runs in the run_script bake sandbox and in the browser. */
(function (root) {
  'use strict';

  const PPU = 32, RIM_PX = 2, MIN_BODY = 6;
  const MIN_R = Math.ceil((MIN_BODY + 2 * RIM_PX) / 2);   // 5 px radius → 10 px clump
  const SWAY = 4, VARIANTS = 4;
  const SEASONS = ['summer', 'autumn', 'winter'];
  // ---- camera: the ADR-0006/0022 projection the rest of the world is baked on -------------------
  // ¾ from the SOUTH at 40°, orthographic. Height foreshortens by cos40, ground depth by sin40 —
  // the same numbers the boat, rock and shoreline bakes use, so a tree sits in the same world.
  const ELEV = 40, CE = Math.cos(ELEV * Math.PI / 180), SE = Math.sin(ELEV * Math.PI / 180);
  const KEYLINE = '#101d21';                               // cold soft keyline, sits in landscape
  const COLD = '#1d3b4a', WARM = '#e8b06a';                // the whole lighting story

  // ---- vec ------------------------------------------------------------------
  const nrm = (v) => { const L = Math.hypot(v[0], v[1], v[2]) || 1; return [v[0] / L, v[1] / L, v[2] / L]; };
  const LIGHT = {
    key: nrm([-0.55, -0.66, 0.52]),   // upper-LEFT, slightly toward camera  (art bible §1)
    rim: nrm([0.48, -0.28, -0.83]),   // behind & upper-right — the back-rim channel
  };
  const clamp = (v, a, b) => v < a ? a : v > b ? b : v;
  const smooth = (e0, e1, x) => { const t = clamp((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t); };

  // ---- colour ---------------------------------------------------------------
  const h2r = (h) => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];
  const r2h = (r) => '#' + r.map(v => clamp(Math.round(v), 0, 255).toString(16).padStart(2, '0')).join('');
  const mix = (a, b, t) => { const A = h2r(a), B = h2r(b); return r2h([0, 1, 2].map(i => A[i] + (B[i] - A[i]) * t)); };

  function folRamp(fol, season, fall) {
    // evergreens do not turn: only species with a `fall` colour change in autumn.
    const base = season === 'autumn' && fall ? mix(fol, fall, 0.78) : season === 'winter' ? mix(fol, '#2c4a4f', 0.34) : fol;
    return {
      dp:  mix(mix(base, '#000000', 0.80), COLD, 0.34),
      sh:  mix(mix(base, '#000000', 0.58), COLD, 0.24),
      mid: mix(base, '#000000', 0.26),
      hi:  mix(base, WARM, 0.15),
      key: mix(mix(base, '#ffffff', 0.07), WARM, 0.36),
      rim: mix(WARM, '#fff3df', 0.14),
    };
  }
  function barkRamp(bark, birch) {
    const b = birch ? mix(bark, COLD, 0.46) : bark;
    return {
      dp:  mix(mix(b, '#000000', 0.80), COLD, 0.40),
      sh:  mix(mix(b, '#000000', 0.58), COLD, 0.28),
      mid: mix(b, '#000000', 0.34),
      hi:  mix(b, WARM, birch ? 0.08 : 0.18),
      key: mix(mix(b, '#000000', 0.10), WARM, birch ? 0.18 : 0.40),
      rim: mix(b, WARM, birch ? 0.55 : 0.72),
    };
  }
  const SNOW = { dp: '#5c7180', sh: '#7d93a0', mid: '#a8bcc4', hi: '#cfdde1', key: '#eef4f4', rim: '#fff6e6' };

  // ---- species --------------------------------------------------------------
  // w/h are the established Acadian-set footprints (drop-in over the old PNGs). form drives the build.
  const SPECIES = [
    { key: 'RedSpruce',      name: 'Red Spruce',      latin: 'Picea rubens',        form: 'spire',  w: 104, h: 182, fol: '#356343', bark: '#5a4433', droop: 0.44, sway: 1.5, taper: 0.94, rings: 13 },
    { key: 'BlackSpruce',    name: 'Black Spruce',    latin: 'Picea mariana',       form: 'spire',  w: 84,  h: 180, fol: '#2c5740', bark: '#4e3b2d', droop: 0.42, sway: 1.3, taper: 0.78, rings: 12, gappy: 0.30 },
    { key: 'BalsamFir',      name: 'Balsam Fir',      latin: 'Abies balsamea',      form: 'spire',  w: 98,  h: 160, fol: '#356842', bark: '#55432f', droop: 0.28, sway: 1.4, taper: 1.00, rings: 12 },
    { key: 'WhitePine',      name: 'E. White Pine',   latin: 'Pinus strobus',       form: 'pine',   w: 126, h: 220, fol: '#3e7048', bark: '#5f4834', droop: 0.20, sway: 2.0, taper: 1.00, rings: 6 },
    { key: 'WhiteCedar',     name: 'E. White Cedar',  latin: 'Thuja occidentalis',  form: 'cedar',  w: 78,  h: 152, fol: '#386639', bark: '#6a4c37', droop: 0.34, sway: 1.2, taper: 1.00, rings: 18 },
    { key: 'Tamarack',       name: 'Tamarack',        latin: 'Larix laricina',      form: 'larch',  w: 96,  h: 168, fol: '#5d8133', bark: '#57422f', fall: '#d3a238', droop: 0.30, sway: 1.8, taper: 0.86, rings: 16, gappy: 0.18 },
    { key: 'WhiteBirch',     name: 'White Birch',     latin: 'Betula papyrifera',   form: 'oval',   w: 112, h: 182, fol: '#477534', bark: '#d8dcd4', fall: '#d9a832', birch: true, droop: 0.30, sway: 2.6, lobes: 11 },
    { key: 'RedMaple',       name: 'Red Maple',       latin: 'Acer rubrum',         form: 'round',  w: 136, h: 178, fol: '#3a6e30', bark: '#544639', fall: '#bf3f26', droop: 0.24, sway: 2.2, lobes: 13 },
    { key: 'RedOak',         name: 'Red Oak',         latin: 'Quercus rubra',       form: 'round',  w: 156, h: 170, fol: '#37602f', bark: '#4f4235', fall: '#a35429', droop: 0.18, sway: 1.9, lobes: 14, broad: 1.06 },
    { key: 'TremblingAspen', name: 'Trembling Aspen', latin: 'Populus tremuloides', form: 'oval',   w: 86,  h: 180, fol: '#5b8136', bark: '#b9bfae', fall: '#e0b03a', birch: true, droop: 0.22, sway: 3.1, lobes: 10 },
  ];
  const byKey = {}; SPECIES.forEach(s => byKey[s.key] = s);
  // `h` is the tree's TRUE height in world px (32 px = 1 m). The CELL is measured, not guessed — see
  // cellOf(): every variant and season is built, projected, and the union of their real screen
  // extents sets the cell and the pivot row.
  SPECIES.forEach(s => { s.worldH = s.h; });

  const hashKey = (k) => { let h = 2166136261; for (let i = 0; i < k.length; i++) { h ^= k.charCodeAt(i); h = Math.imul(h, 16777619); } return h >>> 0; };
  const rngOf = (a) => function () { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; };

  // ---- volume buffer (z-buffered surface with per-pixel normals) -------------
  const M = { NONE: 0, FOLIAGE: 1, BARK: 2, TWIG: 3 };
  function Vol(w, h) {
    const n = w * h;
    this.w = w; this.h = h;
    this.z = new Float32Array(n).fill(-1e9);
    this.nx = new Float32Array(n); this.ny = new Float32Array(n); this.nz = new Float32Array(n);
    this.mat = new Uint8Array(n); this.a = new Uint8Array(n); this.id = new Int16Array(n).fill(-1);
  }
  Vol.prototype.clearPx = function (i) { this.a[i] = 0; this.mat[i] = 0; this.z[i] = -1e9; this.id[i] = -1; };

  // ellipsoid front surface, with a low-frequency angular wobble so a clump reads as foliage rather
  // than a billiard ball. Deliberately LOW frequency (rule 2): it scallops the outline, never speckles it.
  function blob(v, cx, cy, cz, rx, ry, rz, mat, id, seed) {
    const p1 = (seed || 0) * 1.7, p2 = (seed || 0) * 3.1, p3 = (seed || 0) * 5.3;
    const x0 = Math.max(0, Math.floor(cx - rx * 1.3)), x1 = Math.min(v.w - 1, Math.ceil(cx + rx * 1.3));
    const y0 = Math.max(0, Math.floor(cy - ry * 1.3)), y1 = Math.min(v.h - 1, Math.ceil(cy + ry * 1.3));
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
      let u = (x + 0.5 - cx) / rx, w = (y + 0.5 - cy) / ry;
      const th = Math.atan2(w, u);
      // low-order harmonics only: this scallops the outline into leaf lobes, it never speckles it.
      // The 8θ term is weighted to the underside, where leaf mass actually hangs in teeth.
      const under = 0.5 + 0.5 * Math.sin(th);
      const k = 1 + 0.14 * Math.sin(3 * th + p1) + 0.09 * Math.sin(5 * th + p2) + 0.055 * under * Math.sin(8 * th + p3);
      u /= k; w /= k;
      const s = u * u + w * w;
      if (s > 1) continue;
      const t = Math.sqrt(1 - s), z = cz + t * rz, i = y * v.w + x;
      if (z <= v.z[i]) continue;
      let nx = u / rx, ny = w / ry, nz = t / rz; const L = Math.hypot(nx, ny, nz) || 1;
      v.z[i] = z; v.nx[i] = nx / L; v.ny[i] = ny / L; v.nz[i] = nz / L;
      v.mat[i] = mat; v.a[i] = 1; v.id[i] = id;
    }
  }
  // swept sphere (trunk / limb): cylinder-ish normals, tapered
  function limb(v, x0, y0, z0, x1, y1, z1, r0, r1, mat, id) {
    const dx = x1 - x0, dy = y1 - y0, L2 = dx * dx + dy * dy || 1e-6, R = Math.max(r0, r1);
    const ax = Math.max(0, Math.floor(Math.min(x0, x1) - R)), bx = Math.min(v.w - 1, Math.ceil(Math.max(x0, x1) + R));
    const ay = Math.max(0, Math.floor(Math.min(y0, y1) - R)), by = Math.min(v.h - 1, Math.ceil(Math.max(y0, y1) + R));
    for (let y = ay; y <= by; y++) for (let x = ax; x <= bx; x++) {
      let t = ((x + 0.5 - x0) * dx + (y + 0.5 - y0) * dy) / L2; t = clamp(t, 0, 1);
      const px = x0 + dx * t, py = y0 + dy * t, pz = z0 + (z1 - z0) * t, r = r0 + (r1 - r0) * t;
      if (r <= 0.35) continue;
      const ox = x + 0.5 - px, oy = y + 0.5 - py, d2 = ox * ox + oy * oy;
      if (d2 > r * r) continue;
      const k = Math.sqrt(r * r - d2), z = pz + k, i = y * v.w + x;
      if (z <= v.z[i]) continue;
      let nx = ox / r, ny = oy / r * 0.35, nz = k / r; const Ln = Math.hypot(nx, ny, nz) || 1;
      v.z[i] = z; v.nx[i] = nx / Ln; v.ny[i] = ny / Ln; v.nz[i] = nz / Ln;
      v.mat[i] = mat; v.a[i] = 1; v.id[i] = id;
    }
  }

  // ---- RULE 1: a ring only carries clumps it can carry at full size ----------
  // Never shrink a clump below MIN_R to fit more of them — drop the count instead.
  function ringPlan(R, want, rWant) {
    const r = Math.max(MIN_R, rWant);
    const n = Math.max(1, Math.min(want, Math.floor((2 * Math.PI * Math.max(R, 0.5)) / (2.15 * r))));
    return { n, r };
  }
  // RULE 1, vertical: at 32 PPU a 2 px rim + 6 px body = 10 px of bough, so a crown can only carry
  // floor(crownH / (2·MIN_R + gap)) tiers. Twenty whorls of needles is not a thing this scale can hold.
  function tierCount(crownH, want) {
    return clamp(Math.floor(crownH / (2 * MIN_R + 6)), 4, want);
  }
  // an open crown (larch tufts, cedar sprays) stacks tiers closer — its masses are small units, not
  // wide plates, so the vertical gap they need is smaller.
  function tierCountOpen(crownH, want) {
    return clamp(Math.floor(crownH / (MIN_R + 4)), 5, want);
  }

  // ---- growth stage ----------------------------------------------------------
  // One knob: `size` = fraction of the species' mature height. Everything else is derived, because a
  // sapling is not a shrunk adult — it keeps its lower branches (mature trees self-prune), carries a
  // proportionally thinner trunk, and is narrower for its height. MIN_R does NOT scale: at 32 PPU a
  // clump is 5 px or it is not a clump, so young trees carry FEWER masses rather than smaller ones.
  const STAGES = { sapling: 0.22, young: 0.45, pole: 0.70, mature: 1.00 };
  const STAGE_KEYS = ['sapling', 'young', 'pole', 'mature'];
  function sizeOf(o) {
    if (o && typeof o.size === 'number') return clamp(o.size, 0.12, 1.4);
    if (o && STAGES[o.stage]) return STAGES[o.stage];
    return 1;
  }
  const stageName = (t) => t < 0.33 ? 'sapling' : t < 0.58 ? 'young' : t < 0.85 ? 'pole' : 'mature';

  // ---- skeleton + crown builders --------------------------------------------
  function build(sp, variant, season, size) {
    const t = size == null ? 1 : size;
    const rng = rngOf(hashKey(sp.key) + variant * 7717 + (season === 'winter' ? 31 : 0) + Math.round(t * 997));
    const grow = smooth(0.18, 1, t);                       // 0 at seedling → 1 at full height
    const H = Math.max(26, sp.worldH * t);
    const W = Math.max(22, sp.w * Math.pow(t, 1.25));
    const cx = Math.floor(W / 2) + 0.5, baseY = H - 1.5;
    const bare = season === 'winter' && (sp.form === 'round' || sp.form === 'oval' || sp.form === 'larch');
    const scale = 0.94 + rng() * 0.12;
    const lean = (rng() * 2 - 1) * 0.05;
    const limbs = [], clumps = [];
    const conifer = sp.form === 'spire' || sp.form === 'pine' || sp.form === 'cedar' || sp.form === 'larch';
    const droopF = sp.droop * (0.5 + 0.5 * grow);
    // the leader must clear the cell top: `scale` can exceed 1, and the cap blob adds its own radius
    // above topY. Unclamped this sliced the spire flat on some variants.
    const topY = Math.max(MIN_R + 2, baseY - (H - 6) * scale);

    // trunk: root flare → leader. Young stems are proportionally slimmer.
    const trunkR = (conifer ? W * 0.052 : W * 0.062) * (0.62 + 0.38 * grow);
    // the trunk sits ON the tree's axis (z = 0). Under the 40° camera z moves a thing up the screen,
    // so the old "push it back so foliage covers it" hack would now lift the trunk into the crown;
    // occlusion comes from the view-axis depth key instead, which is what that key is for.
    const flare = trunkR * 1.9, tz = 0;
    limbs.push([cx, baseY, tz, cx + lean * H * 0.25, baseY - H * 0.10, tz, flare, trunkR * 1.12, M.BARK]);
    if (conifer) {
      limbs.push([cx + lean * H * 0.25, baseY - H * 0.10, tz, cx + lean * H * 0.7, topY, tz, trunkR * 1.12, 0.9, M.BARK]);
    } else {
      const forkY = baseY - H * (sp.form === 'oval' ? 0.42 : 0.34);
      limbs.push([cx + lean * H * 0.25, baseY - H * 0.10, tz, cx + lean * H * 0.5, forkY, tz, trunkR * 1.12, trunkR * 0.72, M.BARK]);
      const nb = sp.form === 'oval' ? 4 : 5;
      for (let i = 0; i < nb; i++) {
        const a = (i / nb) * Math.PI * 2 + rng() * 0.7;
        const spread = (sp.form === 'oval' ? 0.20 : 0.30) * (0.7 + rng() * 0.6);
        const ex = cx + Math.cos(a) * W * spread, ez = Math.sin(a) * W * spread * 0.7;
        const ey = forkY - H * (0.12 + rng() * 0.12);
        limbs.push([cx + lean * H * 0.5, forkY, tz, ex, ey, ez, trunkR * 0.72, trunkR * 0.26, M.BARK]);
        if (bare) for (let k = 0; k < 3; k++) {
          const t = 0.45 + k * 0.2;
          limbs.push([cx + (ex - cx) * t, forkY + (ey - forkY) * t, ez * t,
            ex + (rng() * 2 - 1) * W * 0.16, ey - H * (0.06 + rng() * 0.10), ez + (rng() * 2 - 1) * 8,
            trunkR * 0.34, 1.5, M.TWIG]);
        }
      }
    }

    if (bare) {
      if (conifer) {   // bare tamarack: thin boughs, no needles. Rule 3 means they take no rim.
        const crownBase = baseY - H * 0.20, crownH = crownBase - topY, rings = tierCount(crownH, sp.rings) + 2;
        const maxR = W * 0.50 * (sp.taper || 0.9);
        for (let i = 0; i < rings; i++) {
          const f = i / (rings - 1), y = crownBase + (topY - crownBase) * f;
          const R = maxR * Math.pow(1 - f, 0.72) * (0.85 + rng() * 0.3);
          const n = R < 6 ? 2 : 5;
          for (let k = 0; k < n; k++) {
            const a = rng() * Math.PI * 2 + k / n * Math.PI * 2;
            limbs.push([cx, y, tz, cx + Math.cos(a) * R, y + R * 0.34, tz + Math.sin(a) * R * 0.8, 2.4, 1.1, M.TWIG]);
          }
        }
      }
      return { W, H, cx, baseY, limbs, clumps, bare, conifer, rng };
    }

    if (conifer) {
      // young conifers are branched nearly to the ground; mature ones self-prune their lower boughs
      const cbFrac = (sp.form === 'pine' ? 0.46 : sp.form === 'cedar' ? 0.16 : 0.20) * (0.35 + 0.65 * grow);
      const crownBase = baseY - H * cbFrac;
      const crownH = crownBase - topY;
      const rings = (sp.form === 'larch' || sp.form === 'cedar') ? tierCountOpen(crownH, sp.rings) : tierCount(crownH, sp.rings);
      // the crown must FIT the cell: a bough reaches R and then adds its own radius on top, so the
      // widest point is ~1.7R. Sizing the crown off W/2 alone is what cropped the spruces into blocks.
      const maxR = ((W / 2) - 2.5) / 1.7 * (sp.taper || 0.9);
      for (let i = 0; i < rings; i++) {
        const f = i / (rings - 1);                                  // 0 base ring → 1 leader
        const y = crownBase + (topY - crownBase) * Math.pow(f, sp.form === 'pine' ? 0.86 : 1.0);
        // cedar tapers slowly but it DOES taper — held at 0.34 it was a cylinder, which is what read
        // as liquid: no silhouette event from base to top for the eye to hold on to.
        let R = maxR * Math.pow(1 - f, sp.form === 'cedar' ? 0.52 : sp.form === 'pine' ? 0.62 : 0.72);
        R *= sp.form === 'cedar' ? (0.94 + rng() * 0.14) : (0.88 + rng() * 0.24);
        const rWant = Math.max(MIN_R, R * (sp.form === 'cedar' ? 0.44 : 0.40));
        const plan = ringPlan(R, sp.form === 'pine' ? 5 : sp.form === 'larch' ? 6 : 7, rWant);
        const phase = rng() * Math.PI * 2;
        for (let k = 0; k < plan.n; k++) {
          if (sp.gappy && rng() < sp.gappy * (1 - f) && plan.n > 2) continue;
          const a = phase + (k / plan.n) * Math.PI * 2;
          const ca = Math.abs(Math.cos(a)), sa = Math.abs(Math.sin(a));
          const rr = plan.r * (0.92 + rng() * 0.2);

          if (sp.form === 'larch') {
            // a larch is WOOD you can see through, not a cloud: a bare branch out to a needle tuft.
            // Rule 3 keeps the rim off the branch, so it reads as a twig instead of glowing.
            const reach = R * (0.72 + rng() * 0.28);
            const ex = cx + Math.cos(a) * reach, ez = Math.sin(a) * reach * 0.82;
            const ey = y + reach * 0.20 * (0.5 + droopF);
            limbs.push([cx + lean * H * 0.5 * f, y, 0, ex, ey, ez, 2.0, 1.1, M.TWIG]);
            const tr = Math.max(MIN_R, rr * 0.78);
            clumps.push({ x: ex, y: ey, z: ez, rx: tr * 1.1, ry: tr * 0.82, rz: tr });
            continue;
          }

          if (sp.form === 'cedar') {
            // cedar hangs in flattened VERTICAL sprays. Tall-and-narrow clumps groove the column;
            // wide-and-flat ones (the spruce shape) just melted into each other.
            const reach = R * (0.70 + rng() * 0.30);
            const px = cx + Math.cos(a) * reach, pz = Math.sin(a) * reach * 0.82;
            const py = y + (rng() * 2 - 1) * rr * 0.55;
            clumps.push({
              x: px, y: py, z: pz,
              rx: Math.max(MIN_R, rr * 0.82),
              ry: Math.max(MIN_R, rr * 1.35),
              rz: Math.max(MIN_R, rr * 0.82),
            });
            continue;
          }

          const reach = R * (0.62 + rng() * 0.38);
          const px = cx + Math.cos(a) * reach, pz = Math.sin(a) * reach * 0.82;
          // per-bough vertical scatter, heaviest on the low rings — without it the bottom tier lands
          // on one scanline and the crown reads as a box with a ruled edge.
          const skirt = (1 - f) * (1 - f);
          const py = y + rr * droopF * (0.4 + ca * 0.9) + (rng() * 2 - 1) * rr * 0.30 + rng() * rr * 0.7 * skirt;
          // a bough is a FLATTENED PLATE swept outward — wide across the branch, MIN_R deep so the
          // 2 px rim still leaves 6 px of body top-to-bottom. Flatter = layered boughs, not blobs.
          clumps.push({
            x: px, y: py, z: pz,
            rx: Math.max(MIN_R, rr * (0.82 + ca * 0.85)),
            ry: Math.max(MIN_R, rr * 0.55),
            rz: Math.max(MIN_R, rr * (0.82 + sa * 0.85)),
          });
          // drooping bough tip — a LOBE, never a hair (rule 1)
          if (droopF > 0.25 && reach > R * 0.62) {
            const dr = Math.max(MIN_R, rr * 0.70);
            clumps.push({ x: px + Math.cos(a) * dr * 0.9, y: py + dr * (0.9 + droopF * 0.8), z: pz + Math.sin(a) * dr * 0.7, rx: dr * 0.95, ry: dr * 0.85, rz: dr * 0.9 });
          }
        }
        // spine mass keeps the crown one silhouette instead of a stack of islands. Set deep in z so
        // it reads as the dark heart of the tree, not more lit foliage. The larch does without — its
        // whole point is that you see the trunk through it.
        if (sp.form === 'larch') { /* open crown: no spine */ }
        else if (f < 0.94 && sp.form !== 'spire') clumps.push({ x: cx + lean * H * 0.5 * f, y: y + 3, z: -R * 0.9 - 3, rx: Math.max(MIN_R, R * 0.30), ry: Math.max(MIN_R, R * 0.40), rz: MIN_R });
        else if (f < 0.94) clumps.push({ x: cx + lean * H * 0.5 * f, y: y + 5, z: -R * 0.9 - 3, rx: MIN_R, ry: MIN_R * 1.3, rz: MIN_R * 0.8 });
      }
      // leader cap
      clumps.push({ x: cx + lean * H * 0.7, y: topY + MIN_R * 0.7, z: 0, rx: MIN_R * 1.05, ry: MIN_R * 1.25, rz: MIN_R });
    } else {
      // Same derive-from-cell rule as the conifers: a ring lobe sits at (cw - 0.6·rr) and then adds
      // its own radius plus the outline wobble on top, so the crown's widest point is ~1.22·cw.
      const cw = Math.min(W * 0.44 * (sp.broad || 1), ((W / 2) - 2.5) / 1.22), ch = H * (sp.form === 'oval' ? 0.32 : 0.28);
      // a young broadleaf is a narrow broom carried low, not a small ball on a stick
      const cyc = baseY - H * ((sp.form === 'oval' ? 0.60 : 0.54) * (0.72 + 0.28 * grow));
      // RULE 2: the outer silhouette is AUTHORED — lobes spaced by ARC LENGTH (not by angle, which
      // bunches them at the ends of a tall crown and leaves gaps at the sides), then interior fill.
      const NS = 240, cum = [0];
      let per = 0;
      for (let i = 1; i <= NS; i++) {
        const a0 = (i - 1) / NS * Math.PI * 2, a1 = i / NS * Math.PI * 2;
        per += Math.hypot((Math.cos(a1) - Math.cos(a0)) * cw, (Math.sin(a1) - Math.sin(a0)) * ch);
        cum.push(per);
      }
      const angAt = (t) => {
        const target = t * per; let lo = 0, hi = NS;
        while (lo < hi) { const m = (lo + hi) >> 1; if (cum[m] < target) lo = m + 1; else hi = m; }
        return lo / NS * Math.PI * 2;
      };
      const rAvg = Math.max(MIN_R + 1, cw * 0.255);
      const lobes = Math.max(sp.lobes || 12, Math.round(per / (1.7 * rAvg)));
      for (let i = 0; i < lobes; i++) {
        const a = angAt(i / lobes) - Math.PI / 2 + (rng() * 2 - 1) * 0.06;
        const rr = Math.max(MIN_R + 1, cw * (0.23 + rng() * 0.05));
        const ex = cx + Math.cos(a) * (cw - rr * 0.60), ey = cyc + Math.sin(a) * (ch - rr * 0.55);
        clumps.push({ x: ex, y: ey, z: (rng() * 2 - 1) * cw * 0.22, rx: rr * 1.06, ry: rr * 0.94, rz: rr });
        if (Math.sin(a) > 0.2 && droopF > 0.2) {          // hanging lower lobes, staggered
          const dr = Math.max(MIN_R, rr * 0.78);
          clumps.push({ x: ex + (rng() * 2 - 1) * 3, y: ey + dr * (0.55 + droopF + rng() * 0.9), z: (rng() * 2 - 1) * cw * 0.2, rx: dr, ry: dr * 0.9, rz: dr });
        }
      }
      const fill = Math.round(lobes * 1.7);
      for (let i = 0; i < fill; i++) {
        const a = rng() * Math.PI * 2, rad = Math.sqrt(rng()) * 0.74, ph = rng() * Math.PI - Math.PI / 2;
        const rr = Math.max(MIN_R, cw * (0.19 + rng() * 0.07));
        clumps.push({
          x: cx + Math.cos(a) * cw * rad, y: cyc + Math.sin(ph) * ch * rad * 0.92,
          z: -cw * 0.22 + Math.sin(a) * cw * rad * 0.8, rx: rr * 1.05, ry: rr * 0.9, rz: rr,
        });
      }
      // centre mass: a column of front clumps up the axis so the crown is a body, not a wreath
      const nc = Math.max(4, Math.round((ch * 1.5) / (rAvg * 1.15)));
      for (let i = 0; i < nc; i++) {
        const t = (i + 0.5) / nc, rr = Math.max(MIN_R, rAvg * (0.86 + rng() * 0.24));
        clumps.push({
          x: cx + (rng() * 2 - 1) * cw * 0.24, y: cyc - ch * 0.72 + ch * 1.44 * t,
          z: cw * (0.10 + rng() * 0.24), rx: rr * 1.05, ry: rr * 0.92, rz: rr,
        });
      }
    }
    return { W, H, cx, baseY, limbs, clumps, bare, conifer, rng };
  }

  // ---- camera projection + measured cell fit ---------------------------------
  // screen y relative to the trunk foot, and the view-axis depth key
  const pyRel = (g, y, z) => -(g.baseY - y) * CE + z * SE;
  const pzOf = (g, y, z) => z * CE + (g.baseY - y) * SE;
  const WOBBLE = 1.3;   // blob() can push its outline out by the sum of its harmonics

  function extents(g) {
    let top = 1e9, bot = -1e9, xl = 1e9, xr = -1e9;
    for (const c of g.clumps) {
      const y = pyRel(g, c.y, c.z), r = Math.hypot(c.ry * CE, c.rz * SE) * WOBBLE, rx = c.rx * WOBBLE;
      if (y - r < top) top = y - r;
      if (y + r > bot) bot = y + r;
      if (c.x - rx < xl) xl = c.x - rx;
      if (c.x + rx > xr) xr = c.x + rx;
    }
    for (const L of g.limbs) {
      // limb() is a swept sphere rasterised in SCREEN space: its base circle reaches the full radius
      // below the projected point, with no foreshortening on it.
      const r = Math.max(L[6], L[7]) + 1;
      for (const e of [[L[0], L[1], L[2]], [L[3], L[4], L[5]]]) {
        const y = pyRel(g, e[1], e[2]);
        if (y - r < top) top = y - r;
        if (y + r > bot) bot = y + r;
        if (e[0] - r < xl) xl = e[0] - r;
        if (e[0] + r > xr) xr = e[0] + r;
      }
    }
    if (top > bot) { top = -1; bot = 1; xl = 0; xr = 1; }
    return { top, bot, xl, xr };
  }

  // One cell per species, sized off the union of every variant × season it can produce, so a sheet
  // stays a regular grid and no build can clip against its own cell.
  function cellOf(sp, size) {
    const t = size == null ? 1 : size, ck = 'c' + Math.round(t * 1000);
    if (!sp._cells) sp._cells = {};
    if (sp._cells[ck]) return sp._cells[ck];
    const MG = 3;
    let top = 1e9, bot = -1e9, xl = 1e9, xr = -1e9;
    for (const season of ['summer', 'winter']) for (let v = 0; v < VARIANTS; v++) {
      const e = extents(build(sp, v, season, t));
      if (e.top < top) top = e.top;
      if (e.bot > bot) bot = e.bot;
      if (e.xl < xl) xl = e.xl;
      if (e.xr > xr) xr = e.xr;
    }
    const pivotY = Math.ceil(MG - top);
    const h = Math.ceil(pivotY + bot + MG) + 1;
    const dx = Math.max(0, Math.ceil(MG - xl));
    const wCell = Math.max(24, dx + Math.ceil(xr + MG) + 1);
    const cell = { w: wCell, h, dx, pivotX: Math.round(dx + (xl + xr) / 2), pivotY, pad: h - 1 - pivotY, size: t };
    sp._cells[ck] = cell;
    return cell;
  }

  // ---- RULE 2: de-speckle the silhouette before anything shades it -----------
  function despeckle(v) {
    let removed = 0;
    for (let pass = 0; pass < 2; pass++) {
      const kill = [];
      for (let y = 0; y < v.h; y++) for (let x = 0; x < v.w; x++) {
        const i = y * v.w + x; if (!v.a[i]) continue;
        let n = 0;
        if (x > 0 && v.a[i - 1]) n++;
        if (x < v.w - 1 && v.a[i + 1]) n++;
        if (y > 0 && v.a[i - v.w]) n++;
        if (y < v.h - 1 && v.a[i + v.w]) n++;
        if (n <= 1) kill.push(i);
      }
      for (const i of kill) { v.clearPx(i); removed++; }
      if (!kill.length) break;
    }
    // fill 1-px pinholes (they read as edge noise once the rim traces them)
    for (let y = 1; y < v.h - 1; y++) for (let x = 1; x < v.w - 1; x++) {
      const i = y * v.w + x; if (v.a[i]) continue;
      if (v.a[i - 1] && v.a[i + 1] && v.a[i - v.w] && v.a[i + v.w]) {
        const src = v.z[i - 1] > v.z[i + 1] ? i - 1 : i + 1;
        v.a[i] = 1; v.mat[i] = v.mat[src]; v.z[i] = v.z[src] - 0.2; v.id[i] = v.id[src];
        v.nx[i] = v.nx[src]; v.ny[i] = v.ny[src]; v.nz[i] = v.nz[src];
      }
    }
    return removed;
  }

  // chamfer distance-to-edge (3-4), in px
  function distField(a, w, h) {
    const D = new Float32Array(w * h), BIG = 1e6;
    for (let i = 0; i < w * h; i++) D[i] = a[i] ? BIG : 0;
    const rd = (i, d) => D[i] < d ? D[i] : d;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x; if (!D[i]) continue; let d = D[i];
      if (y > 0) { d = rd(i, D[i - w] + 3); if (x > 0) d = Math.min(d, D[i - w - 1] + 4); if (x < w - 1) d = Math.min(d, D[i - w + 1] + 4); }
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

  // ---- RULE 1 audit: does every mass keep a body behind the rim? -------------
  function massReport(a, D, w, h) {
    const lab = new Int32Array(w * h).fill(-1), stack = [];
    const comps = [];
    for (let s = 0; s < w * h; s++) {
      if (!a[s] || lab[s] >= 0) continue;
      const id = comps.length; let px = 0, maxd = 0;
      lab[s] = id; stack.push(s);
      while (stack.length) {
        const i = stack.pop(); px++; if (D[i] > maxd) maxd = D[i];
        const x = i % w, y = (i / w) | 0;
        if (x > 0 && a[i - 1] && lab[i - 1] < 0) { lab[i - 1] = id; stack.push(i - 1); }
        if (x < w - 1 && a[i + 1] && lab[i + 1] < 0) { lab[i + 1] = id; stack.push(i + 1); }
        if (y > 0 && a[i - w] && lab[i - w] < 0) { lab[i - w] = id; stack.push(i - w); }
        if (y < h - 1 && a[i + w] && lab[i + w] < 0) { lab[i + w] = id; stack.push(i + w); }
      }
      comps.push({ id, px, maxd, body: Math.max(0, (maxd - RIM_PX) * 2) });
    }
    const real = comps.filter(c => c.px >= 6);
    const fail = real.filter(c => c.body < MIN_BODY);
    let bodyPx = 0, total = 0;
    for (let i = 0; i < w * h; i++) if (a[i]) { total++; if (D[i] > RIM_PX) bodyPx++; }
    return {
      masses: real.length, failed: fail.length, pass: fail.length === 0,
      minBody: real.length ? Math.round(Math.min.apply(null, real.map(c => c.body)) * 10) / 10 : 0,
      bodyRatio: total ? Math.round(bodyPx / total * 100) : 0,
      lab, comps,
    };
  }

  // ---- shade ----------------------------------------------------------------
  // lum → ramp step. Weighted dark: a tree at night is mostly its own shadow, with lit caps.
  const STEPS = [[0.06, 'dp'], [0.17, 'sh'], [0.35, 'mid'], [0.62, 'hi'], [1.4, 'key']];
  function bandOf(l) { for (let i = 0; i < STEPS.length; i++) if (l < STEPS[i][0]) return STEPS[i][1]; return 'key'; }
  function vnoise(x, y, s) { const n = Math.sin(x * 127.1 + y * 311.7 + s * 74.7) * 43758.5453; return n - Math.floor(n); }
  // smooth 2-octave value noise — reads as leaf clusters instead of per-pixel dirt
  function snoise(x, y, s) {
    const xi = Math.floor(x), yi = Math.floor(y), fx = x - xi, fy = y - yi;
    const u = fx * fx * (3 - 2 * fx), vv = fy * fy * (3 - 2 * fy);
    const a = vnoise(xi, yi, s), b = vnoise(xi + 1, yi, s), c = vnoise(xi, yi + 1, s), d = vnoise(xi + 1, yi + 1, s);
    return a + (b - a) * u + (c - a) * vv + (a - b - c + d) * u * vv;
  }

  function shade(v, sp, season, D, opts) {
    const w = v.w, h = v.h, n = w * h;
    const FOL = folRamp(sp.fol, season, sp.fall), BARK = barkRamp(sp.bark, sp.birch);
    const rgba = new Uint8ClampedArray(n * 4);
    const mFront = new Uint8Array(n), mRim = new Uint8Array(n), mDepth = new Uint8Array(n);
    let zmin = 1e9, zmax = -1e9;
    for (let i = 0; i < n; i++) if (v.a[i]) { if (v.z[i] < zmin) zmin = v.z[i]; if (v.z[i] > zmax) zmax = v.z[i]; }
    const zr = Math.max(1, zmax - zmin);
    const sd = (sp.key.length * 13 + sp.h) % 97;
    const K = LIGHT.key, R = LIGHT.rim;
    const RSl = Math.hypot(R[0], R[1]) || 1, RS = [R[0] / RSl, R[1] / RSl];
    const snowy = season === 'winter' && v.conifer !== false;

    // screen-space occlusion — carves the clump crevices apart so the crown reads as MASSES,
    // not one cauliflower. Samples a ring; anything sitting well in front of this pixel occludes it.
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
      OCC[i] = clamp(1 - occ / ring.length * 2.1, 0.22, 1);
    }

    // local mass thickness = widest body within reach of this pixel (max of the distance field over
    // a small window). RULE 3 gates the rim on THIS, not on the pixel's own depth — the rim belongs
    // to the mass, so a mass too thin to hold one never lights up.
    const TH = new Float32Array(n), tmp = new Float32Array(n), RAD = 6;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      let m = 0; for (let k = -RAD; k <= RAD; k++) { const jx = x + k; if (jx < 0 || jx >= w) continue; const d = D[y * w + jx]; if (d > m) m = d; }
      tmp[y * w + x] = m;
    }
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      let m = 0; for (let k = -RAD; k <= RAD; k++) { const jy = y + k; if (jy < 0 || jy >= h) continue; const d = tmp[jy * w + x]; if (d > m) m = d; }
      TH[y * w + x] = m;
    }

    // RULE 1 audit, measured where it matters: how much of the sprite is mass too thin to hold a rim
    // (local thickness < 2·RIM_PX + MIN_BODY). Those are the pixels that collapse into flat glow.
    let thin = 0, tot = 0;
    for (let i = 0; i < n; i++) if (v.a[i] && v.mat[i] === M.FOLIAGE) { tot++; if (TH[i] * 2 < MIN_BODY + 2 * RIM_PX) thin++; }

    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x;
      if (!v.a[i]) { rgba[i * 4 + 3] = 0; continue; }
      const nx = v.nx[i], ny = v.ny[i], nz = v.nz[i], d = D[i];

      const lam = Math.pow(Math.max(0, nx * K[0] + ny * K[1] + nz * K[2]), 1.35);
      // back rim: how far this pixel's OUTWARD screen normal points along the back light's screen
      // direction. On a silhouette the normal lies in the screen plane — that is what a rim is.
      const nl = Math.hypot(nx, ny) || 1;
      const back = Math.max(0, (nx * RS[0] + ny * RS[1]) / nl);
      const fres = Math.pow(1 - clamp(nz, 0, 1), 1.7);
      // RULE 3: a mass too thin to hold a rim never gets one.
      const thick = smooth(4.0, 5.2, TH[i]);
      let rim = Math.pow(back, 1.15) * fres * thick * smooth(3.6, 0.8, d);
      // occlusion: crown interiors go dark; depth behind the front surface goes darker still
      const ao = clamp(0.26 + 0.74 * Math.exp(-Math.max(0, d - 1) / 6.5), 0.26, 1) * OCC[i];
      const zf = 0.62 + 0.38 * ((v.z[i] - zmin) / zr);
      const sky = 0.45 + 0.55 * clamp(-ny, 0, 1);
      const tex = (snoise(x / 3.1, y / 3.1, sd) - 0.5) * 0.17 + (snoise(x / 1.55, y / 1.55, sd + 7) - 0.5) * 0.075;
      // leaf relief: the leading edge where one clump laps over the one behind steps UP a band, the
      // pixel tucked under a nearer clump steps DOWN. This is what separates leaves from a green wall.
      let relief = 0, bias = 0;
      if (v.mat[i] === M.FOLIAGE) {
        const up = i - w, lf = i - 1, dn = i + w, rt = i + 1;
        if (y > 0 && v.a[up] && v.id[up] !== v.id[i] && v.z[up] < v.z[i] - 1.0) relief += 1;
        if (x > 0 && v.a[lf] && v.id[lf] !== v.id[i] && v.z[lf] < v.z[i] - 1.0) relief += 1;
        if (y > 0 && x > 0 && v.a[up - 1] && v.id[up - 1] !== v.id[i] && v.z[up - 1] < v.z[i] - 1.0) relief += 0.6;
        if (y < h - 1 && v.a[dn] && v.id[dn] !== v.id[i] && v.z[dn] > v.z[i] + 1.0) relief -= 1;
        if (x < w - 1 && v.a[rt] && v.id[rt] !== v.id[i] && v.z[rt] > v.z[i] + 1.0) relief -= 1;
        if (y < h - 1 && x < w - 1 && v.a[dn + 1] && v.id[dn + 1] !== v.id[i] && v.z[dn + 1] > v.z[i] + 1.0) relief -= 0.6;
        // every clump sits a hair off its neighbours in tone, so a crown reads as many leaf masses
        bias = (vnoise(v.id[i] * 7 + 1, v.id[i] * 3 + 5, 11) - 0.5) * 0.085;
      }

      let lum = 0.11 * sky * ao * zf + 1.06 * lam * ao * zf + tex + relief * 0.05 + bias;
      let ramp = v.mat[i] === M.FOLIAGE ? FOL : BARK;
      if (snowy && -ny > 0.42 && d > 0.9 && lam > 0.05) { ramp = SNOW; lum = 0.34 + lum * 0.7; }

      let band = bandOf(clamp(lum, 0, 1.2));
      let hex = ramp[band];
      if (rim > 0.16) hex = mix(hex, ramp.rim, clamp((rim - 0.12) * 1.35, 0, 0.95));

      const c = h2r(hex);
      rgba[i * 4] = c[0]; rgba[i * 4 + 1] = c[1]; rgba[i * 4 + 2] = c[2]; rgba[i * 4 + 3] = 255;
      mFront[i] = clamp(Math.round(lam * 255), 0, 255);
      mRim[i] = clamp(Math.round(rim * 255), 0, 255);
      mDepth[i] = clamp(Math.round(((v.z[i] - zmin) / zr) * 255), 0, 255);
    }

    // soft keyline: sits in the landscape, traces the (now deliberate) silhouette
    if (opts.outline !== false) {
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
    return { rgba, masks: { front: mFront, rim: mRim, depth: mDepth }, thin, tot, TH };
  }

  // ---- sway: per-scanline shear pinned at the trunk pivot --------------------
  function swayShear(buffers, w, h, baseY, frame, amp) {
    if (frame == null || !frame) return buffers;
    const curve = Math.sin(frame / SWAY * Math.PI * 2);
    const out = buffers.map(b => new b.constructor(b.length));
    const stride = buffers.map(b => b.length / (w * h));
    for (let y = 0; y < h; y++) {
      const hf = clamp((baseY - y) / Math.max(1, baseY), 0, 1);
      const dx = Math.round(amp * curve * Math.pow(hf, 1.6));
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

  // ---- public ---------------------------------------------------------------
  function render(key, o) {
    o = o || {};
    const sp = byKey[key] || SPECIES[0];
    const season = SEASONS.indexOf(o.season) >= 0 ? o.season : 'summer';
    const size = sizeOf(o);
    const variant = ((o.variant | 0) % VARIANTS + VARIANTS) % VARIANTS;
    const g = build(sp, variant, season, size);
    const cell = cellOf(sp, size);
    const v = new Vol(cell.w, cell.h);
    v.conifer = g.conifer;
    // project world (x, height, depth) through the 40° camera into the cell. A world ellipsoid comes
    // out as a camera-space ellipsoid, so normals fall out of the rasteriser already in view space.
    const pivot = { x: cell.pivotX, y: cell.pivotY };
    const py = (y, z) => pivot.y + pyRel(g, y, z);
    const pz = (y, z) => pzOf(g, y, z);
    const px = (x) => x + cell.dx;
    let id = 0;
    for (const L of g.limbs) {
      limb(v, px(L[0]), py(L[1], L[2]), pz(L[1], L[2]), px(L[3]), py(L[4], L[5]), pz(L[4], L[5]), L[6], L[7], L[8], id++);
    }
    for (const c of g.clumps) {
      blob(v, px(c.x), py(c.y, c.z), pz(c.y, c.z), c.rx,
        Math.hypot(c.ry * CE, c.rz * SE), Math.hypot(c.ry * SE, c.rz * CE), M.FOLIAGE, id, id++);
    }

    const despeckled = despeckle(v);
    const D = distField(v.a, v.w, v.h);
    const audit = massReport(v.a, D, v.w, v.h);
    const sh = shade(v, sp, season, D, o);

    let rgba = sh.rgba, mf = sh.masks.front, mr = sh.masks.rim, md = sh.masks.depth;
    if (o.frame) {
      const out = swayShear([rgba, mf, mr, md], v.w, v.h, pivot.y, o.frame, sp.sway);
      rgba = out[0]; mf = out[1]; mr = out[2]; md = out[3];
    }
    return {
      w: v.w, h: v.h, pivot, rgba, masks: { front: mf, rim: mr, depth: md },
      alpha: v.a, dist: D, mat: v.mat, nx: v.nx, ny: v.ny, nz: v.nz,
      clumps: g.clumps.length, limbs: g.limbs.length, species: sp, season, variant, size, stage: stageName(size),
      report: {
        pass: audit.pass && sh.thin / Math.max(1, sh.tot) <= 0.04, masses: audit.masses, failed: audit.failed,
        foliagePx: sh.tot,
        minBody: audit.minBody, bodyRatio: audit.bodyRatio, despeckled,
        thinPct: Math.round(sh.thin / Math.max(1, sh.tot) * 1000) / 10,
        metres: Math.round(sp.worldH * size / PPU * 10) / 10,
        // below this the mass floor bites: a 5 px clump minimum means a very small tree becomes a
        // 2–3 clump shrub, not a miniature tree. Worth knowing before someone asks for seedlings.
        underFloor: sp.worldH * size < 34,
      },
      thick: sh.TH,
      _audit: audit,
    };
  }

  // channel pack for the shader bake: R = key light · G = back rim · B = depth · A = coverage
  function packMask(res) {
    const n = res.w * res.h, out = new Uint8ClampedArray(n * 4);
    for (let i = 0; i < n; i++) {
      const a = res.rgba[i * 4 + 3];
      out[i * 4] = res.masks.front[i]; out[i * 4 + 1] = res.masks.rim[i];
      out[i * 4 + 2] = res.masks.depth[i]; out[i * 4 + 3] = a;
    }
    return out;
  }
  // grayscale channel → RGBA for preview
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
  // rule-1 view: rim band · body · mass too thin to carry a rim
  function massView(res) {
    const n = res.w * res.h, out = new Uint8ClampedArray(n * 4);
    const RIMC = h2r('#e8b06a'), BODY = h2r('#2f6a4c'), BAD = h2r('#d2453c'), CORE = h2r('#8fd6a0'), WOOD = h2r('#3f4f57');
    for (let i = 0; i < n; i++) {
      if (!res.alpha[i]) { out[i * 4 + 3] = 0; continue; }
      const d = res.dist[i], fol = res.mat[i] === M.FOLIAGE;
      const thinHere = fol && res.thick[i] * 2 < MIN_BODY + 2 * RIM_PX;
      const c = !fol ? WOOD : thinHere ? BAD : d <= RIM_PX ? RIMC : d >= RIM_PX + MIN_BODY / 2 ? CORE : BODY;
      out[i * 4] = c[0]; out[i * 4 + 1] = c[1]; out[i * 4 + 2] = c[2]; out[i * 4 + 3] = 255;
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

  function sheetSpec(key, size) {
    const sp = byKey[key] || SPECIES[0], t = size == null ? 1 : size, cell = cellOf(sp, t);
    const w = cell.w * VARIANTS, h = cell.h * SWAY;
    return { cell: [cell.w, cell.h], cols: VARIANTS, rows: SWAY, w, h, fits: w <= 2048 && h <= 2048, ppu: PPU,
      pivot: [cell.pivotX, cell.pivotY], pad: cell.pad, elev: ELEV, stage: stageName(t),
      metres: Math.round(sp.worldH * t / PPU * 10) / 10 };
  }

  root.TreeRig = {
    PPU, RIM_PX, MIN_BODY, MIN_R, SWAY, VARIANTS, SEASONS, SPECIES, byKey, LIGHT, KEYLINE, COLD, WARM, ELEV, CE, SE,
    STAGES, STAGE_KEYS, sizeOf, stageName,
    render, packMask, grey, massView, normalView, sheetSpec, cellOf, folRamp, barkRamp,
  };
})(typeof globalThis !== 'undefined' ? globalThis : window);
