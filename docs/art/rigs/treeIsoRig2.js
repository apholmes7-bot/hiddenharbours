/* Hidden Harbours — TREE RIG, PASS 2 (defined leaves + defined branches).
   Pass 1 built real volume and lit it correctly, but every crown came out of the same soft-ellipsoid
   cloud with per-pixel value noise on top, so the family read as artichokes: no leaf shapes, no
   branch shapes, no areas the eye can hold. Same camera, same lights, same three rules — what
   changed is WHAT gets built and HOW the surface is quantised:

     A. CROWNS ARE MASSES, NOT CLOUDS.  A broadleaf crown is 5–9 leaf MASSES (a core + its own ring
        of satellites, a floret), placed by arc length with deliberate gaps between them, plus a
        couple of hanging masses under the branch line. Every mass carries a mass id, so the shader
        can draw a hard edge where two masses meet instead of blending them into one green wall.
     B. LEAF CELLS, NOT NOISE.  The foliage surface is partitioned into ~5×3 px jittered Worley cells
        clipped to their clump — one leaf sprig each. A cell is shaded FLAT from its own mean, then
        its lower-right border steps down. That is the whole "defined leaf" mechanism: flat sprigs
        with dark edges, which is what the references do. The old per-pixel noise is gone.
     C. SERRATED OUTLINE.  blob() adds a triangular tooth wave, pitch ~4.5 px, amplitude ~1 px, on
        top of the low-order lobing — leaf teeth on the silhouette instead of a billiard edge. The
        de-speckle pass is now tooth-aware (8-neighbour test) so it stops eating them.
     D. BRANCHES YOU CAN SEE.  Broadleaves get primaries → secondaries aimed at each mass → visible
        twigs in the gaps; conifers keep a visible leader between tiers. Bark is banded in vertical
        striations (steps, not noise) and the root flare is 3 splayed buttresses with dark splits.

   THE THREE RULES still hold and are still measured per sprite:
     1. MASS — RIM_PX=2 rim leaves MIN_BODY=6 px of interior, so no clump below MIN_R=5 px radius;
        rings drop their count rather than shrink their clumps. Leaf cells subdivide the SURFACE,
        never the mass — a 5 px clump is still a 5 px clump.
     2. SILHOUETTE — authored: masses on an arc-length ring, teeth at a fixed pitch, then a
        tooth-aware de-speckle so nothing accidental survives into the rim channel.
     3. THICKNESS-GATED RIM — rim *= smoothstep(localThickness); a mass too thin to hold a rim
        never gets one.
     4. NO KEYLINE (ADR 0031).  The 1 px near-black ring is retired: rule 2's authored silhouette
        and rule 3's rim are what carry the tree's edge, and they were always the real drawing —
        the ring only traced what they had already decided. A tree is the AREA end of the perimeter
        law (0.11 ring px per painted px, against 0.39 on the shore plants), so this family paid
        least for the ring and loses least by dropping it. `{outline:true}` restores it for an A/B
        — see KEYLINE_DEFAULT. ⚠ Retiring it TIGHTENS coverage by 1 px, which is what makes the
        albedo/mask footprint equal the normal's; see packMask.

   SPEC: PPU 32 · ¾ from S at 40° (ADR-0006/0022) · bottom-centre TRUNK pivot · no AA · binary alpha
   · sheets ≤ 2048 px/axis · upper-left key. PALETTE: cold ambient (#1d3b4a) + ONE warm key (#e8b06a).

   globalThis.TreeRig2 — same surface as TreeRig:
     SPECIES  VARIANTS  SWAY  SEASONS  PPU  RIM_PX  MIN_BODY  MIN_R
     render(key,{variant,season,frame,size|stage,mode}) -> {w,h,pivot,rgba,masks:{front,rim,depth},report}
     packMask · grey · massView · normalView · leafView · sheetSpec · cellOf */
(function (root) {
  'use strict';

  const PPU = 32, RIM_PX = 2, MIN_BODY = 6;
  const MIN_R = Math.ceil((MIN_BODY + 2 * RIM_PX) / 2);   // 5 px radius → 10 px clump
  const SWAY = 4, VARIANTS = 4;
  const SEASONS = ['summer', 'autumn', 'winter'];
  // ---- camera: the ADR-0006/0022 projection the rest of the world is baked on -------------------
  const ELEV = 40, CE = Math.cos(ELEV * Math.PI / 180), SE = Math.sin(ELEV * Math.PI / 180);
  const KEYLINE = '#101d21';
  // ADR 0031 — the outline is retired from world art; the silhouette is carried by the form's own
  // dark side. Trees are wave 2 of the ADR's §4 "as each family is redone" (shore plants were the
  // pilot). The ring is not deleted: it is gated OFF by default and reachable with `{outline:true}`,
  // mirroring the engine's own `GameConfig.HullKeylineFlood` so the owner keeps a one-flag A/B.
  const KEYLINE_DEFAULT = false;
  const COLD = '#1d3b4a', WARM = '#e8b06a';

  // ---- leaf-cell grain: PER SPECIES ------------------------------------------
  // Pass 2a used one 7×5 lattice for the whole family, so a spruce's needles were shaped exactly like
  // an oak's leaves and the lattice showed through as a grid. A grain is now a foliage TYPE:
  //   w,h   cell size in px (32 px = 1 m). Anisotropy is the species read: needles are long and thin,
  //         cedar scale-sprays are tall and narrow, oak leaves are broad.
  //   rot   lattice rotation, radians — the single biggest anti-grid move: an axis-aligned lattice
  //         reads as a checker no matter how much you jitter the sites inside it.
  //   jit   site jitter, 0–1 of a cell. At 0.95 the lattice is barely recoverable by eye.
  //   warp  amplitude of a low-frequency domain warp applied before the lookup, in px. This is what
  //         makes a run of cells drift and clump like real foliage instead of tiling.
  //   tone  strength of the per-cell ±1-band tone break. A needle is 2 px tall — break it as hard as
  //         an oak leaf and the spray turns back into noise; an oak leaf is 8 px and needs the break
  //         to read as a separate leaf at all.
  //   corner set only on the round/lobed grains. A leaf's down-right CORNER steps two bands, not one
  //         — it is where one leaf laps over the next. On a needle spray it is wrong: needle cells are
  //         wide and 2 px tall, so their bottom borders line up into machined contour stripes. Needle
  //         grains are instead raked steeply (rot ≈ −40°) and kept short, which breaks the rows up.
  //   flip  fraction of cells that render TWO bands brighter instead of one — a leaf turned edge-on,
  //         pale underside to the light. Birch and aspen only: it is the shimmer those two are known
  //         for, and on a spruce it would just be sparkle.
  //   edge  which OUTLINE the grain cuts (see EDGES) — the silhouette has to agree with the cells.
  const GRAINS = {
    needle:  { w: 4.8, h: 2.2, rot: -0.62, jit: 0.95, warp: 3.2, tone: 0.11, edge: 'needle' },    // spruce / fir sprays
    pineTuft:{ w: 6.6, h: 2.6, rot: -0.78, jit: 0.98, warp: 3.6, tone: 0.13, edge: 'pineTuft' },   // long white-pine needles
    scale:   { w: 3.2, h: 7.2, rot:  0.14, jit: 0.90, warp: 2.2, tone: 0.12, edge: 'scale' },      // cedar flattened sprays
    tuft:    { w: 4.2, h: 3.8, rot:  0.62, jit: 1.00, warp: 2.4, tone: 0.14, edge: 'tuft', corner: 1 }, // larch rosettes
    broad:   { w: 8.4, h: 5.6, rot:  0.38, jit: 0.92, warp: 3.0, tone: 0.20, edge: 'broad', corner: 1 },  // oak / maple lobes
    small:   { w: 5.4, h: 4.4, rot: -0.55, jit: 0.96, warp: 2.6, tone: 0.17, edge: 'small', corner: 1, flip: 0.07 },  // birch / aspen rounds
  };

  // ---- silhouette edge profile: PER SPECIES ----------------------------------
  // Pass 2a cut one tooth wave for the whole family AND normalised its amplitude against the clump's
  // MEAN radius — so on a spruce bough plate (≈50 × 12 px) the teeth came out 1.7 px on the pointed
  // ends and 0.4 px along the long edges, i.e. invisible exactly where the needles are. Every plate
  // wider than it was tall ended up a smooth pebble. Pass 2b:
  //   · amp is in PIXELS and is divided by the LOCAL radius, so a tooth is a tooth on every axis;
  //   · teeth are spaced in pixels of ARC, not in radians, so they do not bunch at the plate tips;
  //   · flank/under/base weight WHERE the teeth bite — needle rows sit on a bough's long edges,
  //     broadleaf lobes go all the way round and hang heavier underneath.
  //   · amp2/pitch2 is a second, finer wave: a lobed oak leaf has teeth ON its lobes.
  const EDGES = {
    needle:  { pitch: 3.1, amp: 1.70, base: 0.30, under: 0.40, flank: 0.75, lobe: [0.085, 0.055] },
    pineTuft:{ pitch: 3.7, amp: 2.10, base: 0.32, under: 0.36, flank: 0.80, lobe: [0.095, 0.050] },
    scale:   { pitch: 5.6, amp: 1.35, base: 0.55, under: 0.30, flank: 0.30, lobe: [0.135, 0.075] },
    tuft:    { pitch: 2.7, amp: 1.45, base: 0.55, under: 0.35, flank: 0.35, lobe: [0.110, 0.080] },
    broad:   { pitch: 8.2, amp: 2.20, base: 0.60, under: 0.45, flank: 0.15, lobe: [0.150, 0.090], pitch2: 3.0, amp2: 0.75 },
    small:   { pitch: 4.3, amp: 1.40, base: 0.62, under: 0.38, flank: 0.20, lobe: [0.120, 0.075], pitch2: 2.1, amp2: 0.45 },
  };
  const GRAIN_BY_FORM = { spire: 'needle', pine: 'pineTuft', cedar: 'scale', larch: 'tuft', round: 'broad', oval: 'small' };
  const grainOf = (sp) => GRAINS[sp.grain || GRAIN_BY_FORM[sp.form] || 'broad'];
  const edgeOf = (sp) => EDGES[grainOf(sp).edge] || EDGES.broad;
  // how far past its nominal radius an outline can reach, in px — the cell has to leave room for it
  const eMaxOf = (E) => E.amp * (E.base + E.under + (E.flank || 0)) + (E.amp2 || 0) + 1;
  const LEAF_W = GRAINS.broad.w, LEAF_H = GRAINS.broad.h;

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

  // Six flat greens do the whole crown. Pass 2 widens the gap between `sh` and `mid` — the leaf
  // edges are drawn by a one-band step, so the bands have to be far enough apart to SHOW. And `dp` is
  // lifted off the floor: at 0.86 black it landed on (15,29,29), which is the landscape's own
  // background — so every shaded crown interior read as a HOLE punched through the tree instead of
  // the shadow between two florets.
  function folRamp(fol, season, fall) {
    const base = season === 'autumn' && fall ? mix(fol, fall, 0.78) : season === 'winter' ? mix(fol, '#2c4a4f', 0.34) : fol;
    return {
      dp:  mix(mix(base, '#000000', 0.74), COLD, 0.24),
      sh:  mix(mix(base, '#000000', 0.52), COLD, 0.22),
      mid: mix(base, '#000000', 0.22),
      hi:  mix(base, WARM, 0.16),
      key: mix(mix(base, '#ffffff', 0.10), WARM, 0.34),
      rim: mix(WARM, '#fff3df', 0.14),
    };
  }
  function barkRamp(bark, birch) {
    const b = birch ? mix(bark, COLD, 0.46) : bark;
    return {
      dp:  mix(mix(b, '#000000', 0.82), COLD, 0.42),
      sh:  mix(mix(b, '#000000', 0.60), COLD, 0.28),
      mid: mix(b, '#000000', 0.34),
      hi:  mix(b, WARM, birch ? 0.08 : 0.18),
      key: mix(mix(b, '#000000', 0.10), WARM, birch ? 0.18 : 0.40),
      rim: mix(b, WARM, birch ? 0.55 : 0.72),
    };
  }
  const SNOW = { dp: '#5c7180', sh: '#7d93a0', mid: '#a8bcc4', hi: '#cfdde1', key: '#eef4f4', rim: '#fff6e6' };

  // ---- species --------------------------------------------------------------
  // `masses` = how many leaf florets a mature crown carries. Below ~5 a broadleaf stops reading as a
  // tree and starts reading as a bunch of grapes; above ~9 at this scale the florets lose their gaps.
  const SPECIES = [
    { key: 'RedSpruce',      name: 'Red Spruce',      latin: 'Picea rubens',        form: 'spire',  w: 104, h: 182, fol: '#356343', bark: '#5a4433', droop: 0.44, sway: 1.5, taper: 0.94, rings: 13 },
    { key: 'BlackSpruce',    name: 'Black Spruce',    latin: 'Picea mariana',       form: 'spire',  w: 84,  h: 180, fol: '#2c5740', bark: '#4e3b2d', droop: 0.42, sway: 1.3, taper: 0.78, rings: 12, gappy: 0.30 },
    { key: 'BalsamFir',      name: 'Balsam Fir',      latin: 'Abies balsamea',      form: 'spire',  w: 98,  h: 160, fol: '#356842', bark: '#55432f', droop: 0.28, sway: 1.4, taper: 1.00, rings: 12 },
    { key: 'WhitePine',      name: 'E. White Pine',   latin: 'Pinus strobus',       form: 'pine',   w: 126, h: 220, fol: '#3e7048', bark: '#5f4834', droop: 0.20, sway: 2.0, taper: 1.00, rings: 6 },
    { key: 'WhiteCedar',     name: 'E. White Cedar',  latin: 'Thuja occidentalis',  form: 'cedar',  w: 78,  h: 152, fol: '#386639', bark: '#6a4c37', droop: 0.34, sway: 1.2, taper: 1.00, rings: 18 },
    { key: 'Tamarack',       name: 'Tamarack',        latin: 'Larix laricina',      form: 'larch',  w: 96,  h: 168, fol: '#5d8133', bark: '#57422f', fall: '#d3a238', droop: 0.30, sway: 1.8, taper: 0.86, rings: 16, gappy: 0.18 },
    { key: 'WhiteBirch',     name: 'White Birch',     latin: 'Betula papyrifera',   form: 'oval',   w: 112, h: 182, fol: '#477534', bark: '#d8dcd4', fall: '#d9a832', birch: true, droop: 0.30, sway: 2.6, masses: 8 },
    { key: 'RedMaple',       name: 'Red Maple',       latin: 'Acer rubrum',         form: 'round',  w: 136, h: 178, fol: '#3a6e30', bark: '#544639', fall: '#bf3f26', droop: 0.24, sway: 2.2, masses: 8 },
    { key: 'RedOak',         name: 'Red Oak',         latin: 'Quercus rubra',       form: 'round',  w: 156, h: 170, fol: '#37602f', bark: '#4f4235', fall: '#a35429', droop: 0.18, sway: 1.9, masses: 9, broad: 1.06 },
    { key: 'TremblingAspen', name: 'Trembling Aspen', latin: 'Populus tremuloides', form: 'oval',   w: 86,  h: 180, fol: '#5b8136', bark: '#b9bfae', fall: '#e0b03a', birch: true, droop: 0.22, sway: 3.1, masses: 8 },
  ];
  const byKey = {}; SPECIES.forEach(s => byKey[s.key] = s);
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
    this.mat = new Uint8Array(n); this.a = new Uint8Array(n);
    this.id = new Int16Array(n).fill(-1);     // clump / limb id
    this.mid = new Int16Array(n).fill(-1);    // MASS id — the floret or bough a clump belongs to
  }
  Vol.prototype.clearPx = function (i) { this.a[i] = 0; this.mat[i] = 0; this.z[i] = -1e9; this.id[i] = -1; this.mid[i] = -1; };

  // ellipsoid front surface. Two outline modifiers, both deliberate (rule 2):
  //   · low-order harmonics — lobes the eye reads as clumps of leaves
  //   · a tooth wave carrying the species' own edge profile (EDGES): needle rows on a bough's long
  //     edges, lobes-with-teeth on an oak, small round bumps on a birch.
  // Two things are fixed here versus pass 2a. The tooth amplitude is in PIXELS and is divided by the
  // LOCAL ellipse radius, so a 1.7 px tooth is 1.7 px on a plate's long edge as well as at its tip —
  // before, amplitude was normalised against the mean radius and every wide plate came out smooth.
  // And teeth are spaced along real ARC LENGTH (a 64-sample cumulative table per clump), so they do
  // not crowd into the pointed ends of a flat bough.
  function blob(v, cx, cy, cz, rx, ry, rz, mat, id, mid, seed, E) {
    E = E || EDGES.broad;
    const p1 = (seed || 0) * 1.7, p2 = (seed || 0) * 3.1, p3 = (seed || 0) * 5.3;
    const NA = 64, TAU = Math.PI * 2, arc = new Float32Array(NA + 1);
    let ax = rx, ay = 0, tot = 0;
    for (let i = 1; i <= NA; i++) {
      const a = i / NA * TAU, X = Math.cos(a) * rx, Y = Math.sin(a) * ry;
      tot += Math.hypot(X - ax, Y - ay); arc[i] = tot; ax = X; ay = Y;
    }
    if (tot < 1e-3) return;
    const nT = Math.max(4, Math.round(tot / E.pitch));
    const nT2 = E.pitch2 ? Math.max(6, Math.round(tot / E.pitch2)) : 0;
    const pad = eMaxOf(E) + 1;
    const x0 = Math.max(0, Math.floor(cx - rx * 1.3 - pad)), x1 = Math.min(v.w - 1, Math.ceil(cx + rx * 1.3 + pad));
    const y0 = Math.max(0, Math.floor(cy - ry * 1.3 - pad)), y1 = Math.min(v.h - 1, Math.ceil(cy + ry * 1.3 + pad));
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
      let u = (x + 0.5 - cx) / rx, w = (y + 0.5 - cy) / ry;
      const th = Math.atan2(w, u);
      const ct = Math.cos(th), stt = Math.sin(th);
      const under = 0.5 + 0.5 * stt, flank = Math.abs(stt);
      // arc fraction at this bearing — tooth phase in pixels of outline, not radians
      const fi = ((th + TAU) % TAU) / TAU * NA, i0 = fi | 0;
      const s = (arc[i0] + (arc[i0 + 1] - arc[i0]) * (fi - i0)) / tot;
      const rEff = Math.hypot(ct * rx, stt * ry) || 1;
      const tw = Math.abs(((s * nT + p3) % 1 + 1) % 1 * 2 - 1) - 0.5;          // triangle, ±0.5
      let bite = tw * E.amp * (E.base + E.under * under + (E.flank || 0) * flank);
      if (nT2) bite += (Math.abs(((s * nT2 + p2) % 1 + 1) % 1 * 2 - 1) - 0.5) * E.amp2;
      const k = 1 + E.lobe[0] * Math.sin(3 * th + p1) + E.lobe[1] * Math.sin(5 * th + p2) + bite / rEff;
      u /= k; w /= k;
      const sq = u * u + w * w;
      if (sq > 1) continue;
      const t = Math.sqrt(1 - sq), z = cz + t * rz, i = y * v.w + x;
      if (z <= v.z[i]) continue;
      let nx = u / rx, ny = w / ry, nz = t / rz; const L = Math.hypot(nx, ny, nz) || 1;
      v.z[i] = z; v.nx[i] = nx / L; v.ny[i] = ny / L; v.nz[i] = nz / L;
      v.mat[i] = mat; v.a[i] = 1; v.id[i] = id; v.mid[i] = mid;
    }
  }
  // swept sphere (trunk / limb): cylinder-ish normals, tapered
  function limb(v, x0, y0, z0, x1, y1, z1, r0, r1, mat, id, mid) {
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
      v.mat[i] = mat; v.a[i] = 1; v.id[i] = id; v.mid[i] = mid;
    }
  }

  // ---- RULE 1: a ring only carries clumps it can carry at full size ----------
  function ringPlan(R, want, rWant) {
    const r = Math.max(MIN_R, rWant);
    const n = Math.max(1, Math.min(want, Math.floor((2 * Math.PI * Math.max(R, 0.5)) / (2.15 * r))));
    return { n, r };
  }
  function tierCount(crownH, want) { return clamp(Math.floor(crownH / (2 * MIN_R + 8)), 4, want); }
  function tierCountOpen(crownH, want) { return clamp(Math.floor(crownH / (MIN_R + 6)), 5, want); }

  // ---- growth stage ----------------------------------------------------------
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
    const grow = smooth(0.18, 1, t);
    const H = Math.max(26, sp.worldH * t);
    const W = Math.max(22, sp.w * Math.pow(t, 1.25));
    const cx = Math.floor(W / 2) + 0.5, baseY = H - 1.5;
    const bare = season === 'winter' && (sp.form === 'round' || sp.form === 'oval' || sp.form === 'larch');
    const scale = 0.94 + rng() * 0.12;
    const lean = (rng() * 2 - 1) * 0.05;

    const limbs = [], clumps = [];
    let massN = 0;
    const conifer = sp.form === 'spire' || sp.form === 'pine' || sp.form === 'cedar' || sp.form === 'larch';
    const droopF = sp.droop * (0.5 + 0.5 * grow);
    const topY = Math.max(MIN_R + 2, baseY - (H - 6) * scale);

    // ---- trunk: 3 splayed root buttresses → bole → leader ----------------------
    // The buttresses are separate masses, so the shader puts a dark split between them: that is the
    // reference trunk read (a flared foot in three planes) instead of pass 1's smooth carrot.
    const trunkR = (conifer ? W * 0.052 : (sp.birch ? W * 0.040 : W * 0.056)) * (0.62 + 0.38 * grow);
    const tz = 0;
    const boleY = baseY - H * 0.10;
    const boleX = cx + lean * H * 0.25;
    // A root buttress is SHORT: it flares out under a metre of trunk, it is not a tripod leg. Lateral
    // reach ≈0.9 R, rise ≈1.5 R, and each one is its own mass so a dark split lands between them.
    const rise = trunkR * 1.6;
    for (let r = 0; r < 3; r++) {
      const a = -Math.PI / 2 + (r - 1) * 1.15 + (rng() * 2 - 1) * 0.12;
      const ex = cx + Math.cos(a) * trunkR * (0.85 + rng() * 0.25);
      const ez = Math.sin(a) * trunkR * 0.75;
      limbs.push([ex, baseY + 0.5, ez, cx, baseY - rise, tz, trunkR * 0.46, trunkR * 1.02, M.BARK, massN++]);
    }
    const boleM = massN++;
    limbs.push([cx, baseY - rise * 0.6, tz, boleX, boleY, tz, trunkR * 1.10, trunkR * 1.0, M.BARK, boleM]);
    if (conifer) {
      // THE LEADER. Pass 2a ran one straight limb at z = 0 from the bole to the tip, which under this
      // camera puts it in FRONT of every bough below it — a bright tan stick ruled straight down the
      // middle of the crown, and the single loudest "odd branch" in the family. It now runs BEHIND
      // the bough origins (world z pushed back) and tapers hard, so it survives only as a glimpse of
      // trunk in the gaps between tiers, which is what a real spire does.
      const midY = boleY - (boleY - topY) * 0.30;
      limbs.push([boleX, boleY, tz, cx + lean * H * 0.32, midY, -trunkR * 0.55, trunkR * 1.04, trunkR * 0.74, M.BARK, boleM]);
      limbs.push([cx + lean * H * 0.32, midY, -trunkR * 0.55, cx + lean * H * 0.7, topY + MIN_R * 0.6, -trunkR * 1.9, trunkR * 0.74, 0.8, M.BARK, boleM]);
    }

    if (bare && conifer) {   // bare tamarack: thin boughs, no needles. Rule 3 keeps the rim off them.
      const crownBase = baseY - H * 0.20, crownH = crownBase - topY, rings = tierCount(crownH, sp.rings) + 2;
      const maxR = W * 0.50 * (sp.taper || 0.9);
      for (let i = 0; i < rings; i++) {
        const f = i / (rings - 1), y = crownBase + (topY - crownBase) * f;
        const R = maxR * Math.pow(1 - f, 0.72) * (0.85 + rng() * 0.3);
        const n = R < 6 ? 2 : 4;
        for (let k = 0; k < n; k++) {
          const a = rng() * Math.PI * 2 + k / n * Math.PI * 2;
          const reach = R * (0.70 + rng() * 0.30);
          limbs.push([cx, y, tz, cx + Math.cos(a) * reach, y + reach * 0.46, tz + Math.sin(a) * reach * 0.8, 2.1, 0.9, M.TWIG, massN++]);
        }
      }
      return { W, H, cx, baseY, limbs, clumps, bare, conifer, rng, masses: massN, eMax: eMaxOf(edgeOf(sp)) };
    }

    if (conifer) {
      const cbFrac = (sp.form === 'pine' ? 0.46 : sp.form === 'cedar' ? 0.16 : 0.20) * (0.35 + 0.65 * grow);
      const crownBase = baseY - H * cbFrac;
      const crownH = crownBase - topY;
      const rings = (sp.form === 'larch' || sp.form === 'cedar') ? tierCountOpen(crownH, sp.rings) : tierCount(crownH, sp.rings);
      const maxR = ((W / 2) - 2.5) / 1.7 * (sp.taper || 0.9);
      for (let i = 0; i < rings; i++) {
        const f = i / (rings - 1);
        const y = crownBase + (topY - crownBase) * Math.pow(f, sp.form === 'pine' ? 0.86 : 1.0);
        let R = maxR * Math.pow(1 - f, sp.form === 'cedar' ? 0.52 : sp.form === 'pine' ? 0.62 : 0.72);
        R *= sp.form === 'cedar' ? (0.94 + rng() * 0.14) : (0.88 + rng() * 0.24);
        // DEPTH SQUASH (spires only). A whorl of boughs is a ring in PLAN; under a 40° camera the
        // front bough lands ~0.64·R lower on screen than the back one, which is the whole tier pitch —
        // so a radial whorl smears its own layers away. Flattening the crown along the view axis is
        // the standard iso cheat and it is what lets a spruce read as stacked plates.
        const zsq = sp.form === 'spire' ? 0.52 : 0.82;
        const rWant = Math.max(MIN_R, R * (sp.form === 'cedar' ? 0.44 : 0.40));
        const plan = ringPlan(R, sp.form === 'pine' ? 5 : sp.form === 'larch' ? 4 : sp.form === 'cedar' ? 6 : 7, rWant);
        const phase = rng() * Math.PI * 2;
        for (let k = 0; k < plan.n; k++) {
          if (sp.gappy && rng() < sp.gappy * (1 - f) && plan.n > 2) continue;
          const a = phase + (k / plan.n) * Math.PI * 2;
          const ca = Math.abs(Math.cos(a)), sa = Math.abs(Math.sin(a));
          const rr = plan.r * (0.92 + rng() * 0.2);
          const bm = massN++;                                  // one bough = one MASS = one edge

          if (sp.form === 'larch') {
            const reach = R * (0.72 + rng() * 0.28);
            const ex = cx + Math.cos(a) * reach, ez = Math.sin(a) * reach * 0.82;
            const ey = y + reach * 0.20 * (0.5 + droopF);
            limbs.push([cx + lean * H * 0.5 * f, y, 0, ex, ey, ez, 2.0, 1.1, M.TWIG, bm]);
            const tr = Math.max(MIN_R, rr * 0.78);
            clumps.push({ x: ex, y: ey, z: ez, rx: tr * 1.1, ry: tr * 0.82, rz: tr, m: bm });
            continue;
          }

          if (sp.form === 'cedar') {
            // alternate sprays reach short/long: that is what puts a groove between them instead of a
            // solid green column, and it scallops the silhouette on the way out.
            const alt = (k % 2) ? 0.62 : 0.98;
            const reach = R * alt * (0.92 + rng() * 0.16);
            const px = cx + Math.cos(a) * reach, pz = Math.sin(a) * reach * 0.7;
            const py = y + (rng() * 2 - 1) * rr * 0.4 - (alt > 0.8 ? rr * 0.3 : 0);
            clumps.push({ x: px, y: py, z: pz, m: bm,
              rx: Math.max(MIN_R, rr * 0.74), ry: Math.max(MIN_R, rr * 1.5), rz: Math.max(MIN_R, rr * 0.74) });
            continue;
          }

          // a spruce/fir/pine bough is a PLATE: the branch out to a spray, then the spray as two
          // stepped masses (inner and tip) so a tier has an edge halfway along it, not just at its end.
          const reach = R * (0.62 + rng() * 0.38);
          const px = cx + Math.cos(a) * reach, pz = Math.sin(a) * reach * zsq;
          const skirt = (1 - f) * (1 - f);
          const py = y + rr * droopF * (0.4 + ca * 0.9) + (rng() * 2 - 1) * rr * 0.30 + rng() * rr * 0.42 * skirt;
          // The feeder branch stops INSIDE the plate and sits behind its front face. Pass 2a ran it
          // to the plate's midpoint at z = pz·0.55 — in front of the needles on every southern bough,
          // so half the tiers had a bare stick lying across them.
          if (reach > R * 0.55 && f < 0.9) {
            limbs.push([cx + lean * H * 0.5 * f, y - 0.5, -trunkR * 0.7,
              px * 0.40 + cx * 0.60, py * 0.58 + y * 0.42, pz * 0.34,
              Math.max(1.5, trunkR * 0.36), 1.0, M.TWIG, bm]);
          }
          clumps.push({ x: px, y: py, z: pz, m: bm,
            rx: Math.max(MIN_R, rr * (0.86 + ca * 0.95)),
            ry: Math.max(MIN_R, rr * 0.44),
            rz: Math.max(MIN_R, rr * (0.86 + sa * 0.95) * (sp.form === 'spire' ? 0.62 : 1)) });
          // outboard TIP plate: the bough tapers to a point, which is what notches the silhouette
          if (reach > R * 0.5) {
            const tr = Math.max(MIN_R, rr * 0.66);
            clumps.push({ x: px + Math.cos(a) * rr * 0.95, y: py + rr * 0.18, z: pz + Math.sin(a) * rr * 0.8 * zsq, m: bm,
              rx: Math.max(MIN_R, tr * (0.78 + ca * 0.6)), ry: Math.max(MIN_R, tr * 0.42),
              rz: Math.max(MIN_R, tr * (0.78 + sa * 0.6) * (sp.form === 'spire' ? 0.62 : 1)) });
          }
          // inner shoulder of the same bough, stepped UP and BACK: the tier now has two planes.
          clumps.push({ x: px * 0.62 + cx * 0.38, y: py - rr * 0.42, z: pz * 0.62, m: bm,
            rx: Math.max(MIN_R, rr * 0.78), ry: Math.max(MIN_R, rr * 0.48),
            rz: Math.max(MIN_R, rr * 0.78 * (sp.form === 'spire' ? 0.7 : 1)) });
          if (droopF > 0.25 && reach > R * 0.62) {
            const dr = Math.max(MIN_R, rr * 0.62);
            clumps.push({ x: px + Math.cos(a) * dr * 0.9, y: py + dr * (0.55 + droopF * 0.55), z: pz + Math.sin(a) * dr * 0.7 * zsq,
              rx: dr * 1.0, ry: dr * 0.62, rz: Math.max(MIN_R, dr * 0.9 * (sp.form === 'spire' ? 0.66 : 1)), m: bm });
          }
        }
        // dark heart: set deep in z, its own mass, so tiers separate against it instead of merging.
        const hm = massN++;
        if (sp.form === 'larch') { /* open crown: you are meant to see the trunk */ }
        else if (f < 0.94 && sp.form !== 'spire') clumps.push({ x: cx + lean * H * 0.5 * f, y: y + 3, z: -R * 0.9 - 3, rx: Math.max(MIN_R, R * 0.30), ry: Math.max(MIN_R, R * 0.40), rz: MIN_R, m: hm });
        else if (f < 0.94) clumps.push({ x: cx + lean * H * 0.5 * f, y: y + 5, z: -R * 0.9 - 3, rx: MIN_R, ry: MIN_R * 1.3, rz: MIN_R * 0.8, m: hm });
      }
      clumps.push({ x: cx + lean * H * 0.7, y: topY + MIN_R * 0.7, z: 0, rx: MIN_R * 1.05, ry: MIN_R * 1.25, rz: MIN_R, m: massN++ });
      return { W, H, cx, baseY, limbs, clumps, bare, conifer, rng, masses: massN, eMax: eMaxOf(edgeOf(sp)) };
    }

    // ================= BROADLEAF =================================================
    // Pass 2: the crown is a set of FLORETS. Each floret is one mass — a core plus its own ring of
    // satellites — placed by arc length on the crown ellipse with gaps left BETWEEN florets. The
    // branch that feeds each floret is drawn, and it is visible in those gaps.
    const cw = Math.min(W * 0.44 * (sp.broad || 1), ((W / 2) - 2.5) / 1.22);
    const ch = H * (sp.form === 'oval' ? 0.34 : 0.30);
    const cyc = baseY - H * ((sp.form === 'oval' ? 0.60 : 0.54) * (0.72 + 0.28 * grow));
    const forkY = baseY - H * (sp.form === 'oval' ? 0.42 : 0.34);
    limbs.push([boleX, boleY, tz, cx + lean * H * 0.5, forkY, tz, trunkR * 1.08, trunkR * 0.70, M.BARK, boleM]);

    // arc-length parameterisation of the crown ellipse (rule 2: spacing is authored, not angular)
    const NS = 240, cum = [0];
    let per = 0;
    for (let i = 1; i <= NS; i++) {
      const a0 = (i - 1) / NS * Math.PI * 2, a1 = i / NS * Math.PI * 2;
      per += Math.hypot((Math.cos(a1) - Math.cos(a0)) * cw, (Math.sin(a1) - Math.sin(a0)) * ch);
      cum.push(per);
    }
    const angAt = (tt) => {
      const target = tt * per; let lo = 0, hi = NS;
      while (lo < hi) { const m = (lo + hi) >> 1; if (cum[m] < target) lo = m + 1; else hi = m; }
      return lo / NS * Math.PI * 2;
    };

    if (bare) {   // winter broadleaf: primaries, secondaries, twigs — no florets
      const nb = sp.form === 'oval' ? 4 : 5;
      for (let i = 0; i < nb; i++) {
        const a = (i / nb) * Math.PI * 2 + rng() * 0.7;
        const spread = (sp.form === 'oval' ? 0.20 : 0.30) * (0.7 + rng() * 0.6);
        const ex = cx + Math.cos(a) * W * spread, ez = Math.sin(a) * W * spread * 0.7;
        const ey = forkY - H * (0.12 + rng() * 0.12), pm = massN++;
        limbs.push([cx + lean * H * 0.5, forkY, tz, ex, ey, ez, trunkR * 0.70, trunkR * 0.26, M.BARK, pm]);
        for (let k = 0; k < 3; k++) {
          const tt = 0.45 + k * 0.2;
          limbs.push([cx + (ex - cx) * tt, forkY + (ey - forkY) * tt, ez * tt,
            ex + (rng() * 2 - 1) * W * 0.16, ey - H * (0.06 + rng() * 0.10), ez + (rng() * 2 - 1) * 8,
            trunkR * 0.34, 1.5, M.TWIG, pm]);
        }
      }
      return { W, H, cx, baseY, limbs, clumps, bare, conifer, rng, masses: massN, eMax: eMaxOf(edgeOf(sp)) };
    }

    const frBase = Math.max(MIN_R * 1.9, cw * 0.30);
    const nMass = clamp(Math.round(per / (2.15 * frBase)), 4, Math.round((sp.masses || 7) * (0.6 + 0.85 * grow)));
    // Two florets are held back as GAPS: an unbroken ring of florets is a wreath, and the references
    // all show sky between the outer masses on one side.
    const phase = rng() * 0.7;
    // Which florets are held back to leave sky: only ones on the sides or the underside. A gap on the
    // TOP edge reads as a bite out of the tree, which is what it looked like the first time.
    const gaps = {};
    { const want = nMass >= 8 ? 2 : 1;
      let picked = 0, off = Math.floor(rng() * nMass);
      for (let m = 0; m < nMass && picked < want; m++) {
        const mm = (m + off) % nMass;
        const aa = angAt((mm + phase) / nMass) - Math.PI / 2;
        const adj = gaps[(mm + 1) % nMass] || gaps[(mm + nMass - 1) % nMass] || gaps[(mm + 2) % nMass] || gaps[(mm + nMass - 2) % nMass];
        if (Math.sin(aa) > -0.15 && !adj) { gaps[mm] = 1; picked++; }
      }
    }
    const primaries = [];
    for (let m = 0; m < nMass; m++) {
      const a = angAt((m + phase) / nMass) - Math.PI / 2 + (rng() * 2 - 1) * 0.05;
      const shrink = gaps[m] ? 0.74 : 1;
      // floret radius: big enough that its own satellites clear MIN_R
      const fr = Math.max(MIN_R * 1.9, cw * (0.30 + rng() * 0.06)) * shrink;
      // ring radius: a floret centre sits at (cw − fr·0.52)·ringR and then adds its own radius, so
      // ringR ≈0.85 is what puts the outer edge ON the species footprint instead of inside it.
      const ringR = (0.85 + rng() * 0.08) * (0.96 + rng() * 0.08);
      const fx = cx + Math.cos(a) * (cw - fr * 0.52) * ringR + (rng() * 2 - 1) * 2;
      const fy = cyc + Math.sin(a) * (ch - fr * 0.48) * ringR;
      const fz = (rng() * 2 - 1) * cw * 0.30 + Math.cos(a) * 0 - (Math.sin(a) > 0 ? cw * 0.08 : -cw * 0.06);
      const mid = massN++;
      primaries.push({ x: fx, y: fy, z: fz, r: fr, a, m: mid });

      // core, set back; then a ring of satellites — upper ones forward (they catch the key), lower
      // ones back (they fall into the floret's own shade). This is what makes a floret read ROUND.
      clumps.push({ x: fx, y: fy, z: fz - fr * 0.30, rx: fr * 0.92, ry: fr * 0.80, rz: fr * 0.86, m: mid });
      const ns = fr > MIN_R * 2.6 ? 6 : 5;
      const sp0 = rng() * Math.PI * 2;
      for (let s = 0; s < ns; s++) {
        const sa = sp0 + s / ns * Math.PI * 2;
        const sr = Math.max(MIN_R, fr * (0.50 + rng() * 0.10));
        const up = -Math.sin(sa);                                    // +1 at the top of the floret
        clumps.push({
          x: fx + Math.cos(sa) * fr * (0.62 + rng() * 0.10),
          y: fy + Math.sin(sa) * fr * (0.56 + rng() * 0.10) * 0.92,
          z: fz + up * fr * 0.52 + (rng() * 2 - 1) * fr * 0.12,
          rx: sr * 1.04, ry: sr * 0.88, rz: sr, m: mid,
        });
      }
      // a floret on the underside hangs a smaller sibling below it, on its own mass. The drop is held
      // short and a bridge clump is stitched in at the halfway point: pass 2a let it fall up to 1.5·fr
      // and the hanger detached, which read as a floret floating free of the tree.
      if (Math.sin(a) > -0.05 && droopF > 0.18 && shrink === 1) {
        const dm = massN++, dr = Math.max(MIN_R * 1.35, fr * (0.52 + rng() * 0.16));
        const hang = fr * (0.56 + rng() * 0.36 + droopF * 0.34);
        const br = Math.max(MIN_R, fr * 0.46);
        clumps.push({ x: fx + (rng() * 2 - 1) * 2, y: fy + hang * 0.50, z: fz - dr * 0.12, rx: br * 1.02, ry: br * 0.92, rz: br, m: dm });
        clumps.push({ x: fx + (rng() * 2 - 1) * 3, y: fy + hang, z: fz - dr * 0.2, rx: dr * 0.95, ry: dr * 0.82, rz: dr * 0.9, m: dm });
        for (let s = 0; s < 3; s++) {
          const sa = -Math.PI / 2 + (s - 1) * 1.15, sr = Math.max(MIN_R, dr * 0.56);
          clumps.push({ x: fx + Math.cos(sa) * dr * 0.66 + (rng() * 2 - 1) * 2, y: fy + hang + Math.sin(sa) * dr * 0.58,
            z: fz - dr * 0.2 - Math.sin(sa) * dr * 0.4, rx: sr * 1.03, ry: sr * 0.86, rz: sr, m: dm });
        }
      }
    }

    // crown shoulders: two masses above the ring centre, so the top edge domes instead of ruling
    // flat across the widest florets.
    for (let s2 = 0; s2 < 2; s2++) {
      const sm = massN++, fr = Math.max(MIN_R * 1.7, cw * (0.25 + rng() * 0.05));
      const sxp = cx + (s2 ? 1 : -1) * cw * (0.14 + rng() * 0.16);
      const syp = cyc - ch * (0.34 + rng() * 0.12);
      clumps.push({ x: sxp, y: syp, z: cw * 0.10, rx: fr * 0.92, ry: fr * 0.80, rz: fr * 0.86, m: sm });
      for (let k = 0; k < 4; k++) {
        const sa = rng() * Math.PI * 2, sr = Math.max(MIN_R, fr * (0.52 + rng() * 0.10));
        clumps.push({ x: sxp + Math.cos(sa) * fr * 0.60, y: syp + Math.sin(sa) * fr * 0.52,
          z: cw * 0.10 - Math.sin(sa) * fr * 0.45, rx: sr * 1.04, ry: sr * 0.88, rz: sr, m: sm });
      }
    }

    // ---- the branch structure that carries them, drawn so the gaps show it ------
    // Primaries are pulled IN and pushed BACK from pass 2a's cw·0.38–0.54 at z = −cw·0.14: they were
    // ending in open sky on the crown's east side and reading as a bare limb sticking out of the
    // foliage with nothing on it (white birch v3 was the worst of them). A primary's job is to be
    // glimpsed between florets, so it lives inside the crown envelope and tapers to a twig.
    const nPri = sp.form === 'oval' ? 3 : 4;
    const priM = [];
    for (let i = 0; i < nPri; i++) {
      const a = -Math.PI / 2 + (i - (nPri - 1) / 2) * (2.0 / nPri) * 1.5 + (rng() * 2 - 1) * 0.2;
      const reach = cw * (0.25 + rng() * 0.13);
      const ex = cx + Math.cos(a) * reach, ez = Math.sin(a) * reach * 0.7 - cw * 0.20;
      const ey = forkY - H * (0.038 + rng() * 0.05);
      const pm = massN++; priM.push({ x: ex, y: ey, z: ez, m: pm });
      limbs.push([cx + lean * H * 0.5, forkY, tz, ex, ey, ez, trunkR * 0.66, trunkR * 0.20, sp.birch ? M.TWIG : M.BARK, pm]);
    }
    // one secondary per floret, from the nearest primary — the visible wood inside the crown. It stops
    // a good half-radius short of the floret core so its tip is always buried in leaves.
    for (const f of primaries) {
      let best = priM[0], bd = 1e9;
      for (const p of priM) { const d = Math.hypot(p.x - f.x, (p.y - f.y) * 0.8, p.z - f.z); if (d < bd) { bd = d; best = p; } }
      const tipx = f.x - Math.cos(f.a) * f.r * 0.62, tipy = f.y - Math.sin(f.a) * f.r * 0.56, tipz = f.z - f.r * 0.8;
      limbs.push([best.x, best.y, best.z, tipx, tipy, tipz, trunkR * 0.28, 1.3, M.TWIG, best.m]);
    }
    // interior filler: enough to keep the crown one silhouette, NOT enough to close the gaps. Sits
    // deep in z with its own mass so it reads as the shaded heart between the florets.
    const heartM = massN++;
    const fill = Math.max(5, Math.round(nMass * 1.2));
    for (let i = 0; i < fill; i++) {
      const a = rng() * Math.PI * 2, rad = 0.16 + rng() * 0.34;
      const up = -0.55 + rng() * 0.75;                 // biased into the top half of the crown
      const rr = Math.max(MIN_R, cw * (0.20 + rng() * 0.07));
      clumps.push({
        x: cx + Math.cos(a) * cw * rad, y: cyc + up * ch * (0.35 + rng() * 0.35),
        z: cw * (-0.04 + rng() * 0.30), rx: rr * 1.05, ry: rr * 0.9, rz: rr, m: heartM,
      });
    }
    return { W, H, cx, baseY, limbs, clumps, bare, conifer, rng, masses: massN, eMax: eMaxOf(edgeOf(sp)) };
  }

  // ---- camera projection + measured cell fit ---------------------------------
  // screen y relative to the trunk foot, and the view-axis depth key
  const pyRel = (g, y, z) => -(g.baseY - y) * CE + z * SE;
  const pzOf = (g, y, z) => z * CE + (g.baseY - y) * SE;
  const WOBBLE = 1.3;   // blob() can push its outline out by the sum of its harmonics

  function extents(g) {
    let top = 1e9, bot = -1e9, xl = 1e9, xr = -1e9;
    const eM = g.eMax || 3;   // the outline can bite OUT this far past the nominal radius
    for (const c of g.clumps) {
      const y = pyRel(g, c.y, c.z), r = Math.hypot(c.ry * CE, c.rz * SE) * WOBBLE + eM, rx = c.rx * WOBBLE + eM;
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

  // ---- RULE 2: de-speckle, TOOTH-AWARE (pass 2) -----------------------------
  function despeckle(v) {
    let removed = 0;
    for (let pass = 0; pass < 2; pass++) {
      const kill = [];
      for (let y = 0; y < v.h; y++) for (let x = 0; x < v.w; x++) {
        const i = y * v.w + x; if (!v.a[i]) continue;
        let n = 0, n8 = 0;
        if (x > 0 && v.a[i - 1]) n++;
        if (x < v.w - 1 && v.a[i + 1]) n++;
        if (y > 0 && v.a[i - v.w]) n++;
        if (y < v.h - 1 && v.a[i + v.w]) n++;
        for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
          if (!dx && !dy) continue;
          const jx = x + dx, jy = y + dy;
          if (jx < 0 || jy < 0 || jx >= v.w || jy >= v.h) continue;
          if (v.a[jy * v.w + jx]) n8++;
        }
        // PASS 2: a 1-px leaf TOOTH is authored (rule 2) — it has one orthogonal neighbour but sits
        // shoulder-to-shoulder with the rest of the edge, so it keeps 3+ of its eight. Only kill the
        // pixels that are actually hanging off nothing.
        if (n === 0 || (n === 1 && n8 <= 2)) kill.push(i);
      }
      for (const i of kill) { v.clearPx(i); removed++; }
      if (!kill.length) break;
    }
    // fill 1-px pinholes (they read as edge noise once the rim traces them)
    for (let y = 1; y < v.h - 1; y++) for (let x = 1; x < v.w - 1; x++) {
      const i = y * v.w + x; if (v.a[i]) continue;
      if (v.a[i - 1] && v.a[i + 1] && v.a[i - v.w] && v.a[i + v.w]) {
        const src = v.z[i - 1] > v.z[i + 1] ? i - 1 : i + 1;
        v.a[i] = 1; v.mat[i] = v.mat[src]; v.z[i] = v.z[src] - 0.2; v.id[i] = v.id[src]; v.mid[i] = v.mid[src];
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

  // ---- shade -----------------------------------------------------------------
  // Five flat bands + a rim. Pass 2 shades per LEAF CELL, not per pixel: the crown is quantised into
  // sprig-sized cells clipped to their clump, each cell takes ONE band from its own mean, and its
  // lower-right border steps down one. Flat sprigs with dark edges — the reference read.
  const STEPS = [[0.05, 'dp'], [0.145, 'sh'], [0.31, 'mid'], [0.56, 'hi'], [1.4, 'key']];
  const BANDS = ['dp', 'sh', 'mid', 'hi', 'key'];
  function bandOf(l) { for (let i = 0; i < STEPS.length; i++) if (l < STEPS[i][0]) return STEPS[i][1]; return 'key'; }
  // Foliage steps in BAND SPACE, not in luminance. A leaf edge worth 0.05 of lum lands inside the
  // same band most of the time and the crown goes back to being mottled soup; a leaf edge worth ONE
  // BAND always shows. Every foliage adjustment below is an integer number of ramp steps.
  function bandIdx(l) { for (let i = 0; i < STEPS.length; i++) if (l < STEPS[i][0]) return i; return 4; }
  function vnoise(x, y, s) { const n = Math.sin(x * 127.1 + y * 311.7 + s * 74.7) * 43758.5453; return n - Math.floor(n); }
  // smooth 2-octave value noise — used ONLY to warp the leaf lattice, never to tint a pixel
  function snoise(x, y, s) {
    const xi = Math.floor(x), yi = Math.floor(y), fx = x - xi, fy = y - yi;
    const u = fx * fx * (3 - 2 * fx), vv = fy * fy * (3 - 2 * fy);
    const a = vnoise(xi, yi, s), b = vnoise(xi + 1, yi, s), c = vnoise(xi, yi + 1, s), d = vnoise(xi + 1, yi + 1, s);
    return a + (b - a) * u + (c - a) * vv + (a - b - c + d) * u * vv;
  }

  // Worley over a ROTATED, domain-WARPED lattice: one cell = one leaf sprig / needle spray / scale
  // frond, shaped by the species' grain. Cell ids are stable per sprite (seeded off the species), so
  // a sway frame does not reshuffle the leaves.
  function leafField(w, h, s, G) {
    const LW = G.w, LH = G.h, jit = G.jit, ca = Math.cos(G.rot), sa = Math.sin(G.rot);
    // The lattice lives in a ROTATED frame, so it has to cover the sprite's rotated bounding box.
    // Pass 2a took the span from |w·ca| + |h·sa| but the ORIGIN from a single corner — so whichever
    // way the grain was rotated, a whole corner of the sprite landed outside the lattice, its
    // neighbour search found no site at all, and every pixel there fell back to cell 0. That is why
    // the big bough plates came out as one flat featureless tone: they genuinely WERE one leaf cell.
    // Measure the rotated box off all four corners and pad it for the domain warp.
    let mnx = 1e9, mxx = -1e9, mny = 1e9, mxy = -1e9;
    for (const c of [[0, 0], [w, 0], [0, h], [w, h]]) {
      const rx = c[0] * ca + c[1] * sa, ry = -c[0] * sa + c[1] * ca;
      if (rx < mnx) mnx = rx; if (rx > mxx) mxx = rx;
      if (ry < mny) mny = ry; if (ry > mxy) mxy = ry;
    }
    const pad = G.warp + 2;
    const ox = mnx - pad - LW * 2, oy = mny - pad - LH * 2;
    const gw = Math.ceil((mxx + pad - ox) / LW) + 3, gh = Math.ceil((mxy + pad - oy) / LH) + 3;
    const sx = new Float32Array(gw * gh), sy = new Float32Array(gw * gh);
    for (let j = 0; j < gh; j++) for (let i = 0; i < gw; i++) {
      const k = j * gw + i;
      sx[k] = ox + (i + (1 - jit) * 0.5 + jit * vnoise(i * 3 + 1, j * 5 + 2, s)) * LW;
      sy[k] = oy + (j + (1 - jit) * 0.5 + jit * vnoise(i * 7 + 53, j * 11 + 17, s + 3)) * LH;
    }
    const id = new Int32Array(w * h);
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      // domain warp first: drifts whole runs of cells so nothing tiles
      const wx = x + 0.5 + (snoise(x / 9.3, y / 7.1, s + 11) - 0.5) * 2 * G.warp;
      const wy = y + 0.5 + (snoise(x / 7.7, y / 9.9, s + 29) - 0.5) * 2 * G.warp;
      const rx = wx * ca + wy * sa, ry = -wx * sa + wy * ca;
      const gi = Math.floor((rx - ox) / LW), gj = Math.floor((ry - oy) / LH);
      let best = 1e9, bk = 0;
      for (let j = Math.max(0, gj - 1); j <= Math.min(gh - 1, gj + 1); j++)
        for (let i = Math.max(0, gi - 1); i <= Math.min(gw - 1, gi + 1); i++) {
          const k = j * gw + i, dx = (rx - sx[k]) / LW, dy = (ry - sy[k]) / LH;
          const d = dx * dx + dy * dy;
          if (d < best) { best = d; bk = k; }
        }
      id[y * w + x] = bk + 1;
    }
    return id;
  }

  function shade(v, sp, season, D, opts) {
    const w = v.w, h = v.h, n = w * h;
    const FOL = folRamp(sp.fol, season, sp.fall), BARK = barkRamp(sp.bark, sp.birch);
    // wood standing in leaf shadow: the same bark ramp pulled most of the way to the crown's own deep
    // green. Darkening the bark ramp alone sent a birch's buried limbs to its COLD blue end, which
    // read as a bruise in the middle of the crown — a branch under leaves takes the leaves' colour.
    const BARKB = {};
    for (const k of ['dp', 'sh', 'mid', 'hi', 'key', 'rim']) BARKB[k] = mix(BARK[k], FOL.dp, k === 'rim' ? 0.40 : 0.74);
    const rgba = new Uint8ClampedArray(n * 4);
    const mFront = new Uint8Array(n), mRim = new Uint8Array(n), mDepth = new Uint8Array(n);
    let zmin = 1e9, zmax = -1e9;
    for (let i = 0; i < n; i++) if (v.a[i]) { if (v.z[i] < zmin) zmin = v.z[i]; if (v.z[i] > zmax) zmax = v.z[i]; }
    const zr = Math.max(1, zmax - zmin);
    const sd = (sp.key.length * 13 + sp.h) % 97;
    const K = LIGHT.key, R = LIGHT.rim;
    const RSl = Math.hypot(R[0], R[1]) || 1, RS = [R[0] / RSl, R[1] / RSl];
    const snowy = season === 'winter' && v.conifer !== false;

    // screen-space occlusion — carves the crevices between masses apart
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

    // local mass thickness (max of the distance field over a window) — RULE 3 gates the rim on this
    const TH = new Float32Array(n), tmp = new Float32Array(n), RAD = 6;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      let m = 0; for (let k = -RAD; k <= RAD; k++) { const jx = x + k; if (jx < 0 || jx >= w) continue; const d = D[y * w + jx]; if (d > m) m = d; }
      tmp[y * w + x] = m;
    }
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      let m = 0; for (let k = -RAD; k <= RAD; k++) { const jy = y + k; if (jy < 0 || jy >= h) continue; const d = tmp[jy * w + x]; if (d > m) m = d; }
      TH[y * w + x] = m;
    }

    let thin = 0, tot = 0;
    for (let i = 0; i < n; i++) if (v.a[i] && v.mat[i] === M.FOLIAGE) { tot++; if (TH[i] * 2 < MIN_BODY + 2 * RIM_PX) thin++; }

    // ---- pass A: per-pixel lighting, accumulated per LEAF UNIT ----------------
    // A leaf unit = (Worley cell × clump), so a sprig never straddles two clumps and clump edges
    // stay crisp. Its mean drives one flat band for the whole unit; its most key-ward pixel is
    // remembered as the sprig's TIP, which gets a one-pixel specular in pass B.
    const G = grainOf(sp);
    const cellId = leafField(w, h, sd, G);
    const unit = new Int32Array(n).fill(-1);
    const lum0 = new Float32Array(n);
    const uSum = new Map();
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x; if (!v.a[i]) continue;
      const nx = v.nx[i], ny = v.ny[i], nz = v.nz[i], d = D[i];
      const lam = Math.pow(Math.max(0, nx * K[0] + ny * K[1] + nz * K[2]), 1.35);
      const ao = clamp(0.26 + 0.74 * Math.exp(-Math.max(0, d - 1) / 6.5), 0.26, 1) * OCC[i];
      const zf = 0.62 + 0.38 * ((v.z[i] - zmin) / zr);
      const sky = 0.45 + 0.55 * clamp(-ny, 0, 1);
      lum0[i] = 0.135 * sky * ao * zf + 1.22 * lam * ao * zf;
      mFront[i] = clamp(Math.round(lam * 255), 0, 255);
      mDepth[i] = clamp(Math.round(((v.z[i] - zmin) / zr) * 255), 0, 255);
      if (v.mat[i] !== M.FOLIAGE) continue;
      const u = cellId[i] * 2048 + (v.id[i] & 2047);
      unit[i] = u;
      const kw = x + y;                    // the key is upper-left, so smallest x+y is most lit
      const e = uSum.get(u);
      if (e) { e.s += lum0[i]; e.c++; if (kw < e.kw) { e.kw = kw; e.tip = i; } }
      else uSum.set(u, { s: lum0[i], c: 1, kw, tip: i });
    }
    // one flat value per unit, plus a per-unit tonal jitter and a per-MASS bias: a crown reads as a
    // set of florets each made of sprigs, instead of one smooth gradient.
    const uLum = new Map(), uTip = new Map();
    for (const [u, e] of uSum) { uLum.set(u, e.s / e.c); uTip.set(u, e.tip); }

    // ---- wood buried in the crown --------------------------------------------
    // A branch surrounded by leaves is in the crown's shadow. Pass 2a shaded every trunk pixel as if
    // it stood in the open, so each glimpse of leader or twig came out a bright tan stick — the
    // loudest "odd branch" in the family after the leader's z was wrong. Count the foliage around a
    // wood pixel and drop it one or two whole bands.
    const BRING = [[4, 0], [3, 3], [0, 4], [-3, 3], [-4, 0], [-3, -3], [0, -4], [3, -3],
                   [7, 0], [5, 5], [0, 7], [-5, 5], [-7, 0], [-5, -5], [0, -7], [5, -5]];
    const BURY = new Uint8Array(n);
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x; if (!v.a[i] || v.mat[i] === M.FOLIAGE) continue;
      let f = 0, t = 0;
      for (const [dx, dy] of BRING) {
        const jx = x + dx, jy = y + dy;
        if (jx < 0 || jy < 0 || jx >= w || jy >= h) continue;
        t++; const j = jy * w + jx;
        if (v.a[j] && v.mat[j] === M.FOLIAGE) f++;
      }
      BURY[i] = !t ? 0 : f * 2 >= t ? 2 : f * 3 >= t ? 1 : 0;
    }

    // ---- pass B: quantise, draw the edges -------------------------------------
    // All four are in RAMP STEPS. One step is a visible edge at 32 px; a fraction of one is mottle.
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x;
      if (!v.a[i]) { rgba[i * 4 + 3] = 0; continue; }
      const nx = v.nx[i], ny = v.ny[i], d = D[i];
      const nz = v.nz[i];

      const nl = Math.hypot(nx, ny) || 1;
      const back = Math.max(0, (nx * RS[0] + ny * RS[1]) / nl);
      const fres = Math.pow(1 - clamp(nz, 0, 1), 1.7);
      const thick = smooth(4.0, 5.2, TH[i]);
      let rim = Math.pow(back, 1.15) * fres * thick * smooth(3.6, 0.8, d);

      let lum, ramp, band = null;
      if (v.mat[i] === M.FOLIAGE) {
        const u = unit[i];
        const mB = (vnoise((v.mid[i] & 511) * 5 + 3, (v.mid[i] & 511) * 9 + 7, 29) - 0.5) * 0.06;
        let bi = bandIdx(uLum.get(u) + mB);
        // per-sprig tone break: a crown is not one gradient, it is a few hundred leaves each catching
        // the light slightly differently. Discrete, so it cannot smear into noise — and scaled by the
        // grain, because a 2 px needle broken as hard as an 8 px oak leaf is just noise again.
        const hj = vnoise(u & 8191, (u >>> 13) & 8191, 19);
        if (G.flip && hj > 1 - G.flip) bi += 2;
        else if (hj < G.tone) bi -= 1;
        else if (hj > 1 - G.tone) bi += 1;
        // Keep the cell's own body inside the ramp, but do NOT floor it: a cell already sitting in
        // shadow has nowhere to draw an outline and should not try. Detail in the light, mass in the
        // shade — flooring every cell at `sh` gave the whole crown a black net (crazy paving).
        bi = bi < 0 ? 0 : bi > 4 ? 4 : bi;
        const base = bi;
        const rt = x < w - 1 ? i + 1 : -1, dn = y < h - 1 ? i + w : -1;
        const up = y > 0 ? i - w : -1, lf = x > 0 ? i - 1 : -1;
        const uR = rt >= 0 && v.a[rt] ? unit[rt] : -1, uD = dn >= 0 && v.a[dn] ? unit[dn] : -1;
        // 1 · sprig outline: the down/right border of a LIT leaf cell is one step darker, and its
        // down-right corner two on the round/lobed grains — that corner is where one leaf laps the
        // next. Gated on the cell's own band, so a shaded plate stays a plate.
        if (base >= 2 && (uR !== u || uD !== u)) bi -= (G.corner && base >= 3 && uR !== u && uD !== u) ? 2 : 1;
        // 1b · sprig tip: the single most key-ward pixel of a lit cell takes one step UP. One bright
        // pixel per leaf is what makes a sprig legible at 32 px — shaded cells get none, or the crown
        // fills with sparkle.
        else if (base >= 3 && uTip.get(u) === i) bi += 1;
        // 2 · clump crevice / lip: tucked under a nearer clump, or lapping over a further one
        if (dn >= 0 && v.a[dn] && v.id[dn] !== v.id[i] && v.z[dn] > v.z[i] + 1.0) bi -= 1;
        else if (rt >= 0 && v.a[rt] && v.id[rt] !== v.id[i] && v.z[rt] > v.z[i] + 1.0) bi -= 1;
        if (up >= 0 && v.a[up] && v.id[up] !== v.id[i] && v.z[up] < v.z[i] - 1.0) bi += 1;
        else if (lf >= 0 && v.a[lf] && v.id[lf] !== v.id[i] && v.z[lf] < v.z[i] - 1.0) bi += 1;
        // 3 · floret / bough boundary: an extra step, so two masses never melt together. A conifer
        // lives on the shadow UNDER each bough, so it cuts twice as deep on a real overlap AND takes
        // one step on a plain adjacency — without that, the boughs of one tier sit at the same depth,
        // no rule fired between them, and the whole lower tier merged into one smooth lily pad (white
        // pine's lower left). A broadleaf floret only cuts where it is genuinely in front.
        const mc = v.conifer ? 2 : 1;
        const mDn = dn >= 0 && v.a[dn] && v.mid[dn] !== v.mid[i];
        const mRt = !mDn && rt >= 0 && v.a[rt] && v.mid[rt] !== v.mid[i];
        if (mDn || mRt) {
          const zn = mDn ? v.z[dn] : v.z[rt];
          bi -= zn > v.z[i] + 0.6 ? mc : (v.conifer ? 1 : 0);
        }
        bi = bi < 0 ? 0 : bi > 4 ? 4 : bi;
        band = BANDS[bi];
        lum = uLum.get(u);
        ramp = FOL;
      } else {
        // Bark steps in band space too, for the same reason the leaves do: a trunk needs two defined
        // edges (lit rim, shade line) and a few vertical striations, not a smooth brown gradient.
        let bi = bandIdx(lum0[i] - (sp.birch && v.mat[i] === M.TWIG ? 0.30 : 0));
        bi -= BURY[i];                                              // buried in the crown → in shade
        // Striations and the two bole edges are TRUNK detail — they need a trunk to sit on. On a 4 px
        // twig they land on every pixel at once and the limb comes out mottled, which is what made
        // the pale birch/aspen forks read as dirty static instead of bark.
        if (TH[i] >= 3.2) {
          const col = Math.floor((x + Math.floor(y / 9)) / 2);
          const sn = vnoise(col, 3, sd + 11);
          if (sn > 0.80) bi += 1; else if (sn < 0.24) bi -= 1;      // plate striations
          if (d < 1.7 && nx > 0.20) bi -= 1;                        // shade side of the bole
          else if (d < 1.7 && nx < -0.30) bi += 1;                  // lit side catches the key
        }
        if (x < w - 1 && v.a[i + 1] && v.mid[i + 1] !== v.mid[i] && v.z[i + 1] > v.z[i] + 0.4) bi -= 1;
        if (y < h - 1 && v.a[i + w] && v.mid[i + w] !== v.mid[i] && v.z[i + w] > v.z[i] + 0.4) bi -= 1;
        if (sp.birch && v.mat[i] === M.BARK && !BURY[i]) {          // lenticel dashes
          const bandY = Math.floor(y / 5), seg = Math.floor((x + bandY * 3) / 7);
          if (vnoise(seg, bandY, sd + 23) > 0.74 && (y % 5) < 2) bi -= 2;
        }
        bi = bi < 0 ? 0 : bi > 4 ? 4 : bi;
        band = BANDS[bi];
        lum = lum0[i];
        ramp = BURY[i] ? BARKB : BARK;
      }
      if (snowy && -ny > 0.42 && d > 0.9 && lum > 0.10) { ramp = SNOW; band = null; lum = 0.34 + lum * 0.7; }

      let hex = ramp[band || bandOf(clamp(lum, 0, 1.2))];
      if (rim > 0.16) hex = mix(hex, ramp.rim, clamp((rim - 0.12) * 1.35, 0, 0.95));

      const c = h2r(hex);
      rgba[i * 4] = c[0]; rgba[i * 4 + 1] = c[1]; rgba[i * 4 + 2] = c[2]; rgba[i * 4 + 3] = 255;
      mRim[i] = clamp(Math.round(rim * 255), 0, 255);
    }

    // KEYLINE — RETIRED BY DEFAULT (ADR 0031). It traced the (authored) silhouette, which is the
    // point: rule 2 had already decided that edge and rule 3's rim already lights it, so the ring
    // was restating a decision rather than making one. Switching it off is a PURE RING DELETION —
    // every pixel it touches has no geometry under it, so no painted pixel of any tree changes
    // value (proven per species, 0 violations). ⚠ It expands the opaque footprint, so retiring it
    // makes albedo/mask coverage EQUAL the normal's instead of 11% larger — see packMask.
    if (opts.outline === undefined ? KEYLINE_DEFAULT : opts.outline !== false) {
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
    return { rgba, masks: { front: mFront, rim: mRim, depth: mDepth }, thin, tot, TH, cellId, unit };
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
    const pivot = { x: cell.pivotX, y: cell.pivotY };
    const py = (y, z) => pivot.y + pyRel(g, y, z);
    const pz = (y, z) => pzOf(g, y, z);
    const px = (x) => x + cell.dx;
    let id = 0;
    const E = edgeOf(sp);
    for (const L of g.limbs) {
      limb(v, px(L[0]), py(L[1], L[2]), pz(L[1], L[2]), px(L[3]), py(L[4], L[5]), pz(L[4], L[5]), L[6], L[7], L[8], id++, L[9] == null ? 900 : L[9]);
    }
    for (const c of g.clumps) {
      blob(v, px(c.x), py(c.y, c.z), pz(c.y, c.z), c.rx,
        Math.hypot(c.ry * CE, c.rz * SE), Math.hypot(c.ry * SE, c.rz * CE), M.FOLIAGE, id, c.m == null ? 900 : c.m, id++, E);
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
      alpha: v.a, dist: D, mat: v.mat, nx: v.nx, ny: v.ny, nz: v.nz, mid: v.mid, unit: sh.unit,
      clumps: g.clumps.length, limbs: g.limbs.length, species: sp, season, variant, size, stage: stageName(size),
      report: {
        pass: audit.pass && sh.thin / Math.max(1, sh.tot) <= 0.04, masses: audit.masses, failed: audit.failed,
        foliagePx: sh.tot, florets: g.masses,
        leafCells: (() => { const s = new Set(); for (let i = 0; i < sh.unit.length; i++) if (sh.unit[i] >= 0) s.add(sh.unit[i]); return s.size; })(),
        minBody: audit.minBody, bodyRatio: audit.bodyRatio, despeckled,
        thinPct: Math.round(sh.thin / Math.max(1, sh.tot) * 1000) / 10,
        metres: Math.round(sp.worldH * size / PPU * 10) / 10,
        underFloor: sp.worldH * size < 34,
      },
      thick: sh.TH,
      _audit: audit,
    };
  }

  // leaf-cell view: every sprig in its own flat colour — the mechanism behind "defined leaves"
  function leafView(res) {
    const n = res.w * res.h, out = new Uint8ClampedArray(n * 4);
    for (let i = 0; i < n; i++) {
      if (!res.alpha[i]) { out[i * 4 + 3] = 0; continue; }
      if (res.unit[i] < 0) { out[i * 4] = 62; out[i * 4 + 1] = 74; out[i * 4 + 2] = 82; out[i * 4 + 3] = 255; continue; }
      const u = res.unit[i];
      out[i * 4] = 60 + 190 * vnoise(u & 8191, 1, 5);
      out[i * 4 + 1] = 60 + 190 * vnoise(u & 8191, 2, 9);
      out[i * 4 + 2] = 60 + 190 * vnoise(u & 8191, 3, 13);
      out[i * 4 + 3] = 255;
    }
    return out;
  }
  // mass view: every floret / bough in its own flat colour
  function massIdView(res) {
    const n = res.w * res.h, out = new Uint8ClampedArray(n * 4);
    for (let i = 0; i < n; i++) {
      if (!res.alpha[i]) { out[i * 4 + 3] = 0; continue; }
      const m = res.mid[i] & 511;
      out[i * 4] = 50 + 200 * vnoise(m * 3 + 1, 7, 3);
      out[i * 4 + 1] = 50 + 200 * vnoise(m * 3 + 2, 11, 6);
      out[i * 4 + 2] = 50 + 200 * vnoise(m * 3 + 3, 13, 9);
      out[i * 4 + 3] = 255;
    }
    return out;
  }

  // channel pack for the shader bake: R = key light · G = back rim · B = depth · A = coverage
  // A is the ALBEDO's alpha, so it inherits whatever the keyline did to the footprint. With the
  // ring retired (ADR 0031) that footprint is the geometry itself, so albedo, mask and normal now
  // all cover the SAME pixels — the ring used to make the first two 11% larger than the third, and
  // "light the keyline from the mask, never the normal" is advice about art that no longer exists.
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

  root.TreeRig2 = {
    PPU, RIM_PX, MIN_BODY, MIN_R, SWAY, VARIANTS, SEASONS, SPECIES, byKey, LIGHT, KEYLINE,
    KEYLINE_DEFAULT, COLD, WARM, ELEV, CE, SE,
    STAGES, STAGE_KEYS, sizeOf, stageName,
    render, packMask, grey, massView, normalView, leafView, massIdView, sheetSpec, cellOf, folRamp, barkRamp,
    LEAF_W, LEAF_H, M, GRAINS, grainOf, EDGES, edgeOf,
  };
})(typeof globalThis !== 'undefined' ? globalThis : window);
