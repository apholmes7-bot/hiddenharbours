/* Hidden Harbours — TREE GAMEPLAY SIDECARS (the writer).  globalThis.TREE_GAMEPLAY

   One sidecar per species, schema hidden-harbours/tree-gameplay@1, every number read off TreeRig4:
   geometry from model().g (build space: x east px, y DOWN px with baseY the ground, z SOUTH px, toward
   the camera), sprite numbers from cellOf / sheetSpec, and two measured maps from a rest frame (wind 0):
   the silhouette, and castShadow under a sunless sky (canopy shade + trunk contact, nothing else).

   Units are SCENE metres, 32 px = 1 m, as in every sidecar in Art/gameplay/. The trees bake at SCALE 0.6
   of true height, so a scene metre is not a true metre of tree; the true sizes ride beside them.

     TREE_GAMEPLAY.sidecar(R, key, o)             -> one species, the three hashes left null
     TREE_GAMEPLAY.stamp(obj, rig, sky, writer)   -> the same, hashes in canonical place (throws if any is missing)
     TREE_GAMEPLAY.fileName(key)                  -> 'treeIsoRig4.<key>.gameplay.json'
     o = { generated: 'YYYY-MM-DD', headM: 1.80 }

   Generated, never edited. If the rig moves its hash moves: re-run this, from Tree Rig Pass 4.dc.html
   (GAMEPLAY SIDECAR) or over the whole family. It measures 32 rest frames per species (4 stages × 2
   season groups × 4 variants) and clears the rig's model cache after each stage. */
(function (root) {
  'use strict';
  const SCHEMA = 'hidden-harbours/tree-gameplay@1', PX = 32, HEAD_M = 1.80;
  const WIND_LEVELS = { calm: 0, breeze: 0.3, wind: 0.6, gale: 1 };
  const NOSUN = { name: 'sunless (canopy shade only)', sunW: [0, 0, 1], sunV: [0, 0, 1], sunI: 0 };
  const r3 = (v) => Math.round(v * 1000) / 1000, m = (px) => r3(px / PX);
  const RECT0 = () => [1e9, -1e9, 1e9, -1e9];
  const rectAdd = (R, a, b) => { if (a < R[0]) R[0] = a; if (a > R[1]) R[1] = a; if (b < R[2]) R[2] = b; if (b > R[3]) R[3] = b; };
  const grp = () => ({ runs: 0, fol: 0, plan: RECT0(), r: 0, base: 1e9, limbBase: 1e9, top: 0, scr: RECT0(), shade: new Set() });

  // one variant of one season group, at rest
  function measure(R, key, stage, season, variant, acc, checks) {
    const fr = R.frame(key, { stage, season, variant, wind: { w: 0, gust: 0, dir: 1 }, frame: 0 });
    const mdl = fr.mdl, g = mdl.g, v = fr.v, piv = mdl.pivot, M = R.M, CE = R.CE;
    const cx = g.cx, by = g.baseY, tr = g.trunkR, reach = R.windReach(mdl.sp, mdl.size);
    const tag = key + ' ' + stage + ' ' + season + ' v' + variant;
    const ok = (c, msg) => { checks.n++; if (!c) checks.fail.push(tag + ': ' + msg); };
    const G = acc[season === 'winter' ? 'winter' : 'summer'];
    G.runs++;

    // the foot is the pivot: build cx lands on the centre of the pivot column
    ok(Math.abs(cx + mdl.cell.dx - (piv.x + 0.5)) < 1e-6, 'pivot is not the trunk-foot column');
    acc.trunkR = tr;
    for (let i = 0; i < 3; i++) { const L = g.limbs[i]; acc.footR = Math.max(acc.footR, Math.hypot(L[0] - cx, L[2]) + L[6]); }
    // the bole, measured one row above the buttresses where it stands clear against the sky on both
    // sides: centred on the pivot, as wide as the collider. A run with foliage at either end is part-hidden.
    const yb = piv.y - 1 - Math.ceil(tr * 1.6 * CE);
    const bark = (x) => x >= 0 && x < v.w && v.a[yb * v.w + x] && v.mat[yb * v.w + x] === M.BARK;
    const clear = (x) => x < 0 || x >= v.w || !v.a[yb * v.w + x];
    let x0 = piv.x, x1 = piv.x;
    if (yb >= 0 && bark(piv.x)) { while (bark(x0 - 1)) x0--; while (bark(x1 + 1)) x1++; }
    if (yb >= 0 && bark(piv.x) && clear(x0 - 1) && clear(x1 + 1)) {
      const c = (x0 + x1 + 1) / 2, hw = (x1 - x0 + 1) / 2;
      ok(Math.abs(c - (piv.x + 0.5)) <= 1, 'bole centre ' + c + ' is off the pivot column');
      ok(Math.abs(hw - tr * 1.1) <= 1.25, 'bole half-width ' + hw + ' px vs collider ' + (tr * 1.1).toFixed(2) + ' px');
      acc.boleSeen++;
    } else acc.boleHidden++;

    // crown: foliage clumps when leafed, outward limbs when bare
    const fol = !g.bare && g.clumps.length > 0;
    if (fol) {
      G.fol++;
      for (const c of g.clumps) {
        rectAdd(G.plan, c.x - c.rx - cx, c.z - c.rz); rectAdd(G.plan, c.x + c.rx - cx, c.z + c.rz);
        G.r = Math.max(G.r, Math.hypot(c.x - cx, c.z) + Math.max(c.rx, c.rz));
        G.base = Math.min(G.base, by - (c.y + c.ry)); G.top = Math.max(G.top, by - (c.y - c.ry));
      }
    }
    for (let i = 3; i < g.limbs.length; i++) {
      const L = g.limbs[i];
      for (const e of [[L[0], L[1], L[2], L[6]], [L[3], L[4], L[5], L[7]]]) {
        G.top = Math.max(G.top, by - e[1] + e[3]);
        const d = Math.hypot(e[0] - cx, e[2]);
        if (d <= tr * 2.5) continue;                                   // the bole and leaders are the trunk
        G.limbBase = Math.min(G.limbBase, by - e[1] - e[3]);
        if (!fol) {
          rectAdd(G.plan, e[0] - cx - e[3], e[2] - e[3]); rectAdd(G.plan, e[0] - cx + e[3], e[2] + e[3]);
          G.r = Math.max(G.r, d + e[3]); G.base = Math.min(G.base, by - e[1] - e[3]);
        }
      }
    }

    // silhouette, pivot-relative; the rest pose must leave the gale pad free on both sides
    let xl = 1e9, xr = -1e9;
    for (let y = 0; y < v.h; y++) for (let x = 0; x < v.w; x++) if (v.a[y * v.w + x]) { rectAdd(G.scr, x - piv.x, y - piv.y); if (x < xl) xl = x; if (x > xr) xr = x; }
    ok(xl >= reach && v.w - 1 - xr >= reach, 'rest silhouette eats the ' + reach + ' px wind pad (' + xl + ' / ' + (v.w - 1 - xr) + ')');

    // the sunless shadow: canopy shade (level 1) + the trunk contact (level 2), pivot-relative
    const sh = R.castShadow(fr, NOSUN);
    let cs = 0, cn = 0;
    for (let y = 0; y < sh.h; y++) for (let x = 0; x < sh.w; x++) {
      const lv = sh.lv[y * sh.w + x]; if (!lv) continue;
      const dx = sh.x0 + x - piv.x, dy = sh.y0 + y - piv.y;
      G.shade.add(dx + ',' + dy);
      if (lv === 2) { cs += dx; cn++; }
    }
    ok(cn > 0 && Math.abs(cs / cn) <= 0.5, 'trunk contact is not centred on the foot (' + (cn ? (cs / cn).toFixed(2) : '—') + ' px)');
  }

  // a set of shade pixels -> plan rect, centroid, equal-area ellipse, area (scene m)
  function shadeStats(set, SE) {
    let n = 0, se = 0, ss = 0, see = 0, sss = 0; const Rr = RECT0();
    for (const k of set) {
      const c = k.indexOf(','), e = +k.slice(0, c), s = (+k.slice(c + 1) + 0.5) / SE;
      n++; se += e; ss += s; see += e * e; sss += s * s;
      rectAdd(Rr, e - 0.5, s - 0.5 / SE); rectAdd(Rr, e + 0.5, s + 0.5 / SE);
    }
    if (!n) return null;
    const me = se / n, ms = ss / n, ve = Math.max(0, see / n - me * me), vs = Math.max(0, sss / n - ms * ms);
    // the ellipse of equal area, its aspect from the second moments (a 2σ fit overshoots a squarish patch)
    const A = n / SE, k = ve > 0 && vs > 0 ? Math.sqrt(ve / vs) : 1;
    return { plan_rect_m: Rr.map(m), centroid_m: [m(me), m(ms)], ellipse_r_m: [m(Math.sqrt(A / Math.PI * k)), m(Math.sqrt(A / Math.PI / k))], area_m2: r3(A / PX / PX) };
  }

  function groupOut(G, headM, SE, winterOf) {
    const foliage = G.fol > 0, base = foliage ? G.base : G.limbBase;
    const out = {
      foliage,
      plan_rect_m: G.plan.map(m),
      plan_r_m: m(G.r),
      base_m: base < 1e8 ? m(Math.max(0, base)) : null,
      limb_base_m: G.limbBase < 1e8 ? m(Math.max(0, G.limbBase)) : null,
      top_m: m(G.top),
      walk_under: base < 1e8 ? base / PX >= headM : true,
      screen_rect_px: [G.scr[0], G.scr[2], G.scr[1] + 1, G.scr[3] + 1],
      SHADE: shadeStats(G.shade, SE),
    };
    if (winterOf) out._note = winterOf;
    return out;
  }

  function stageBlock(R, key, stage, checks, headM, evergreen) {
    const sp = R.byKey[key], t = R.STAGES[stage], spec = R.sheetSpec(key, t);
    checks.n++; if (!spec.fits) checks.fail.push(key + ' ' + stage + ': sheet ' + spec.w + '×' + spec.h + ' is over the 2048 cap');
    const acc = { trunkR: 0, footR: 0, boleSeen: 0, boleHidden: 0, summer: grp(), winter: grp() };
    for (const season of ['summer', 'winter']) for (let v = 0; v < R.VARIANTS; v++) measure(R, key, stage, season, v, acc, checks);
    if (R.clearCache) R.clearCache();
    return {
      size: t,
      scene_height_m: r3(sp.worldH * t / PX),
      true_height_m: r3(sp.real * t),
      SPRITE: { cell_px: spec.cell, pivot_px: spec.pivot, pad_below_px: spec.pad, sheet_px: [spec.w, spec.h], fits_2048: spec.fits, wind_pad_px: spec.windReach },
      TRUNK: {
        shape: 'circle', centre_m: [0, 0], r_m: m(acc.trunkR * 1.1), foot_r_m: m(acc.footR), treatment: 'solid',
        measured: acc.boleSeen + ' of ' + (acc.boleSeen + acc.boleHidden) + ' rest frames show the bole clear of foliage above the buttresses; each agrees with r_m (see _checks)',
      },
      CANOPY: {
        summer_autumn: groupOut(acc.summer, headM, R.SE),
        winter: groupOut(acc.winter, headM, R.SE, evergreen ? 'Evergreen: leafed, a different draw of the same crown (the winter build seeds its own).' : null),
      },
    };
  }

  function sidecar(R, key, o) {
    o = o || {};
    const sp = R.byKey[key]; if (!sp || sp.key !== key) throw new Error('TREE_GAMEPLAY: no species ' + key);
    const headM = o.headM || HEAD_M, checks = { n: 0, fail: [] }, wv = R.windOf(sp);
    const bareWinter = sp.form === 'round' || sp.form === 'oval' || sp.form === 'larch';
    const STAGES = {};
    for (const st of R.STAGE_KEYS) STAGES[st] = stageBlock(R, key, st, checks, headM, !bareWinter);
    return {
      _: 'Hidden Harbours — tree gameplay sidecar, one species. Generated by Art/_treeGameplay.js from Art/treeIsoRig4.js; do not hand-edit — re-generate.',
      schema: SCHEMA,
      rig: 'Art/treeIsoRig4.js', sky: 'Art/weatherSky.js', writer: 'Art/_treeGameplay.js',
      exportSymbol: 'TreeRig4',
      derivedFromRigSha256: null, skyDerivedFromRigSha256: null, writerDerivedFromRigSha256: null,
      generated: o.generated || null,
      species: key,
      frame: {
        units: 'metres (scene)', scale_px_per_m: PX,
        origin: 'the trunk foot — ground level, centre of the bole. It IS the sprite pivot: the centre of the pivot column, on the top edge of the pivot row.',
        axes: '+x east (screen right) · +y south (toward the camera) · +z up',
        heading: 'A tree does not turn: one facing, ¾ from the south at 40° elevation. No facing split.',
        bake_scale: { scale: R.SCALE, px_per_true_m: R.M2PX, note: 'Trees bake at 0.6 of true height (bible). Every metre here is a scene metre at 32 px, the frame boats, buildings and characters share. true_* fields are the botanical sizes.' },
        screen: 'px fields are sprite px from the pivot pixel, +x right, +y down; rects are [left, top, right, bottom) with right and bottom exclusive.',
        plan_to_screen: 'screen_dx = east_px · screen_dy = south_px · sin40° − up_px · cos40°',
        rects: 'plan_rect_m = [east min, east max, south min, south max] from the foot',
        heights: 'base_m / limb_base_m / top_m are metres above the ground; base_m 0 = foliage to the ground. base_m is the lowest foliage when leafed and the lowest outward limb when bare.',
      },
      SPECIES: {
        name: sp.name, latin: sp.latin, form: sp.form, evergreen: !bareWinter, bare_in: bareWinter ? ['winter'] : [],
        true_height_m: sp.real, true_crown_m: sp.crown, true_dbh_m: sp.dbh, variants: R.VARIANTS,
        stems: sp.stems || 1,
        stems_note: sp.stems ? 'Two stems on the even variants once the crown passes 60 px. They part above the foot, so the collider stays one bole.' : 'One stem.',
      },
      SEASONS: {
        summer: 'leafed · CANOPY.summer_autumn',
        autumn: 'leafed, fall colour, same geometry as summer · CANOPY.summer_autumn',
        winter: bareWinter ? 'bare: limbs and twigs only · CANOPY.winter' : 'leafed · CANOPY.winter',
      },
      STAGES,
      WIND: {
        loop_frames: R.LOOP, sheet: '4 × 4, row-major, frame 0 top-left (TreeRig4.sheet)',
        levels: WIND_LEVELS, gust: 'WeatherSky.at default 0.4',
        dir: '±1 is screen x downwind. Not a mirror: the light is not symmetric, so bake each direction you use.',
        preview_fps: 8, preview_fps_range: [4, 12],
        species: { bend: wv[0], limb_sway_px_per_300px: wv[1], leaf_flutter: wv[2], bough_bob: wv[3],
          shimmer: sp.shimmer ? { amount: sp.shimmer[0], still_air_share: sp.shimmer[1] } : null },
        foot_fixed: true,
        law: 'Trunk lean ∝ w² plus a sway, both ∝ (height / H)^1.8, so zero at the ground: the trunk collider and the sort row never move. The crown moves, by at most SPRITE.wind_pad_px either side at a gale.',
        flutter: 'Per leaf (4–6 px broadleaf, 3–4 px aspen), 1–3 slots a loop each, more where the gust is passing. Render only; nothing in this file moves with it.',
        provenance: 'TreeRig4.windOf(sp) · windAt() · windReach() — the cell pad, a bound, not a measured swing',
      },
      SHADOW: {
        live: 'TreeRig4.castShadow(frame, sky) -> {x0, y0, w, h, lv} in cell px; shade the floor with WeatherSky.lightTile(tile, normals, sky, {level}).',
        levels: { 0: 'open', 1: 'canopy shade: the sky overhead more than 66% blocked. Stays under overcast.', 2: 'partial sun, or the trunk-foot contact', 3: 'full cast shadow (sun intensity above 0.25)' },
        contact: 'Ellipse on the foot, screen radii (2.4 · trunkR + 2) × (0.9 · trunkR + 1.5) px, level 2 in any weather.',
        no_live_projection: 'Skew the silhouette by sky.shear: screen px of shadow per px of height.',
        static: 'CANOPY.*.SHADE is the sunless part (canopy + contact, union of the four variants): plan rect, centroid, the ellipse of equal area centred on it (east and south radii, aspect from the second moments) and the area. The only piece of the shadow that does not move with the hour.',
        ground_only: true,
      },
      SORT: {
        key: 'The pivot row, i.e. the trunk foot. Y-sort against other sprites by their own foot.',
        overhang: 'The crown reaches CANOPY.*.plan_rect_m[3] south of the foot, toward the camera. A character standing there sorts in front of the whole tree and draws over the crown\'s front edge.',
      },
      _excluded: {
        INTERACT: 'Nothing on a tree is interactable: no chop or fell, no climb, no forage. Cones, seeds, sap and bark are not modelled.',
        states: 'No stump, felled, damaged or burnt state. Growth is the four stages, chosen at placement; there is no grow-over-time cue.',
        limb_colliders: 'Only the bole blocks. Limbs, twigs and foliage are overhead or see-through; their heights are data (base_m, limb_base_m), not colliders.',
        roots: 'The root buttresses are TRUNK.foot_r_m, data only, not a second collider.',
        tree_shadows: 'castShadow is ground-only: trees do not yet shadow each other or other sprites.',
        snow_load: 'Snow is paint on up-facing stamps and limb tops (sky.snow). No weight, no bending, no shedding.',
        spring: 'The seasons are summer, autumn and winter. No leaf-out or blossom.',
        surface: 'No footstep or litter data; the ground under a tree is the terrain kit\'s.',
      },
      _confirm: {
        head_height: 'walk_under tests base_m against a ' + headM.toFixed(2) + ' m standing head, a declared constant (ours). Re-test base_m against the real character.',
        sapling_blocking: 'Sapling boles are r ≈ 0.05 m. Published solid; whether a sapling blocks is a gameplay call.',
        low_skirt: 'Where walk_under is false the foliage comes below head height (fir and cedar skirts, young crowns). Brush, blocker or fade is a gameplay call; the rig only draws it.',
        shelter: 'SHADE is light, not weather. Whether it keeps rain or snow off is a gameplay call.',
        playback_rate: 'The rig has no clock. The page previews the 16-frame loop at 8 fps (a 2 s loop); the engine picks its own rate.',
        sort_overhang: 'See SORT. A split crown layer is the fix if a character under the canopy has to pass behind its front edge.',
        plan_shape: 'Crowns are built about half as deep (north–south) as they are wide, a view choice of the rig. plan_rect_m and SHADE are the footprint as drawn; plan_r_m is the round bound if gameplay wants circles.',
      },
      _checks: { checked: checks.n, failed: checks.fail.length, failures: checks.fail },
    };
  }

  function stamp(obj, rig, sky, writer) {
    const hex = (h) => typeof h === 'string' && /^[0-9a-f]{64}$/.test(h);
    if (!hex(rig) || !hex(sky) || !hex(writer)) throw new Error('TREE_GAMEPLAY.stamp: all three hashes are required — an unstamped sidecar is the defect');
    return Object.assign({}, obj, { derivedFromRigSha256: rig, skyDerivedFromRigSha256: sky, writerDerivedFromRigSha256: writer });
  }
  const fileName = (key) => 'treeIsoRig4.' + key + '.gameplay.json';

  root.TREE_GAMEPLAY = { SCHEMA, HEAD_M, WIND_LEVELS, sidecar, stamp, fileName };
})(typeof globalThis !== 'undefined' ? globalThis : window);
