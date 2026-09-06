/* Hidden Harbours — SAIL RIG KIT writer (art side).  globalThis.SAIL_KIT

   Writes export/sail-rig-kit/ OFF THE LIVE RIGS — nothing in either sidecar is typed. Per hull:

     <stem>.js              the rig, byte-identical to Art/
     <stem>.gameplay.json   <Rig>.gameplayGeometry(), stamped   (schema hidden-harbours/boat-gameplay-geometry@1)
     <stem>.sailing.json    SAIL_KIT.sailing(), stamped         (schema hidden-harbours/boat-sailing@1 — NEW)

   The sailing sidecar is what a sailing game needs that the geometry file deliberately does not carry:
   the hull form integrated off the loft at the DWL (LWL, waterline beam, displacement, Cp, the waterline
   half-breadths), the sail plan's ratios, the points of sail with the builder pages' canon presets, the
   AUTO-TRIM law and a trim table, the heel law sampled over awa x aws, the named rig STATES and every
   TRANSITION between them (which INTERACT drives it, which opt moves), the tack and gybe boom paths, the
   controls map (INTERACT action -> rig opt), the apparent-from-true wind glue, a REFERENCE polar that
   closes the loop (true wind -> boat speed -> apparent wind -> the pose the sprite will show), the
   animation loop facts, and a measured sprite envelope over the sailing states. Every number that can
   come from the rig does: sailPose() is sampled, the loft is integrated, the bake is measured.

   Run from the project's script sandbox (run_script), one call:

     (0,eval)(await readFile('Art/_sidecarExport.js'));
     (0,eval)(await readFile('Art/_sailKit.js'));
     const summary = await SAIL_KIT.write({ readFile, saveFile, log, dir:'export/sail-rig-kit/' });

   write() re-reads and re-evaluates each rig source itself, hashes THOSE bytes, and stamps both sidecars
   with that hash (D1/D2: the stamp and the geometry cannot come from different bytes). It also refreshes
   Art/gameplay/<stem>.gameplay.json and Art/gameplay/<stem>.sailing.json so the builder pages' tripwires
   read the same files the kit ships. Requires _sidecarExport.js (stampRigSha, sha256Hex). */
(function (root) {
  const ORDER = ['N','NE','E','SE','S','SW','W','NW'];
  const DEG = Math.PI / 180;
  const r1 = (v) => +(+v).toFixed(1), r2 = (v) => +(+v).toFixed(2), r3 = (v) => +(+v).toFixed(3);
  const wrap180 = (a) => ((a + 180) % 360 + 360) % 360 - 180;

  const HULLS = {
    sloop30: { rig:'Art/sloopIsoRig.js', file:'sloopIsoRig.js', symbol:'SloopIso', folder:'sloop-30/', builder:'Sloop 30 Iso.dc.html',
               label:'Sloop 30 — 9.4 m fractional sloop', staysail:false, platform:false, awsRef:12,
               presets:{ in_irons:{awa:8,aws:12}, close_hauled:{awa:38,aws:14}, close_reach:{awa:60,aws:14}, beam_reach:{awa:90,aws:14}, broad_reach:{awa:135,aws:12}, run:{awa:172,aws:10} },
               grind:{ trim_jib:'jib', hoist_main:'main' }, grindWhere:{ jib:'the LEEWARD coaming primary', main:'the starboard cabin-top halyard winch (the 30 has no mainsheet winch — the mainsheet is a block and a pedestal cleat)' },
               headsailOnly:'down · pack open', envelopeAws:20 },
    sloop88: { rig:'Art/sloop88IsoRig.js', file:'sloop88IsoRig.js', symbol:'Sloop88Iso', folder:'sloop-88/', builder:'Sloop 88 Iso.dc.html',
               label:'Sloop 88 — 27.0 m masthead sloop with an inner forestay', staysail:true, platform:true, awsRef:14,
               presets:{ in_irons:{awa:8,aws:14}, close_hauled:{awa:38,aws:16}, close_reach:{awa:60,aws:16}, beam_reach:{awa:90,aws:16}, broad_reach:{awa:135,aws:14}, run:{awa:172,aws:12} },
               grind:{ trim_jib:'jib', trim_main:'main' }, grindWhere:{ jib:'the LEEWARD primary', main:'the LEEWARD mainsheet winch on the aft deck' },
               headsailOnly:'flaked in the boom', envelopeAws:24 }
  };

  // The builder pages' AUTO-TRIM, verbatim (Sloop 30 Iso / Sloop 88 Iso _autoTrim): sheet limits set so the
  // main sits at 18 deg of attack and the headsail at 16 — full fill — until the 86 / 85 deg limits cap them.
  function autoTrim(awa) {
    const a = Math.abs(awa); const bo = Math.max(0, Math.min(86, a - 18)); const jo = Math.max(0, Math.min(85, a - 16));
    return { main: +Math.max(0, Math.min(1, 1 - (bo - 4) / 82)).toFixed(2), jib: +Math.max(0, Math.min(1, 1 - (jo - 9) / 76)).toFixed(2) };
  }

  // ---- the hull form, integrated off the rig's own loft ----------------------------------------------
  function hullForm(R) {
    const X = R.loft, L = X.L, DWL = X.DWL, NU = 120, NZ = 24, RHO = 1025;
    const sectionArea = (u, zw) => { const st = X.station(u); if (st.kz >= zw) return 0;
      let A = 0, px = null; for (let k = 0; k <= NZ; k++) { const z = st.kz + (zw - st.kz) * k / NZ, x = X.halfAtZ(u, z, 0); if (px != null) A += (px + x) / 2 * (zw - st.kz) / NZ; px = x; } return 2 * A; };
    const at = (zw) => { let vol = 0, pA = null, pY = null, maxA = 0, y0 = null, y1 = null, bwl = 0;
      for (let i = 0; i <= NU; i++) { const u = i / NU, st = X.station(u), A = sectionArea(u, zw);
        if (A > 0) { const yw = X.skin(1, u, X.fracAtZ(st, zw), 0)[1]; if (y0 == null) y0 = yw; y1 = yw; bwl = Math.max(bwl, X.halfAtZ(u, zw, 0)); }
        if (A > maxA) maxA = A; if (pA != null) vol += (pA + A) / 2 * (st.y - pY); pA = A; pY = st.y; }
      const lwl = y0 == null ? 0 : y1 - y0;
      return { draft_m: r3(zw), lwl_m: r3(lwl), bwl_m: r3(2 * bwl), volume_m3: r3(vol), displacement_kg: Math.round(vol * RHO), midship_area_m2: r3(maxA), Cp: lwl > 0 ? r3(vol / (maxA * lwl)) : 0, wl_y: [r3(y0), r3(y1)] }; };
    const D = at(DWL);
    const NW = 16, wl = []; for (let i = 0; i <= NW; i++) { const u = i / NW, st = X.station(u); const x = st.kz < DWL ? X.halfAtZ(u, DWL, 0) : 0;
      wl.push([r3(X.skin(1, u, X.fracAtZ(st, Math.max(st.kz + 1e-4, DWL)), 0)[1]), r3(x)]); }
    const LWL_ft = D.lwl_m * 3.28084;
    return {
      LOA_m: L, LWL_m: D.lwl_m, BWL_m: D.bwl_m, canoe_body_draft_m: r3(DWL), waterline_y: D.wl_y,
      volume_m3: D.volume_m3, displacement_kg: D.displacement_kg, midship_area_m2: D.midship_area_m2, Cp: D.Cp,
      hull_speed_kn: r2(1.34 * Math.sqrt(LWL_ft)), DL_ratio: r1((D.displacement_kg / 1016.0469) / Math.pow(0.01 * LWL_ft, 3)),
      displacement_curve: [0.5, 0.75, 0.9, 1.0, 1.1, 1.2].map(f => { const d = at(DWL * f); return { draft_m: d.draft_m, sink_from_dwl_m: r3(DWL * f - DWL), displacement_kg: d.displacement_kg, lwl_m: d.lwl_m, bwl_m: d.bwl_m }; }),
      waterline_half_breadths: wl,
      provenance: 'trapezoidal integration of the rig loft (station/skin/halfAtZ/fracAtZ) below z = DWL over ' + NU + ' stations x ' + NZ + ' waterlines; seawater ' + RHO + ' kg/m3. CANOE BODY ONLY — the fin keel, bulb and rudder (WATERLINE.draft_m in the geometry sidecar) are not integrated. The bow rake shifts y by at most ' + (R.loft.L > 20 ? 0.06 : 0.10) + ' m and is read from skin(), not ignored.'
    };
  }

  // ---- the polar reference model -------------------------------------------------------------------
  // v(twa, tws) = min( v_hull * (1 - exp(-tws / tws_h)) * G(twa),  R(twa) * tws );  tws_h = 80 / (SA/D).
  // G is a cruiser polar shape against hull speed; R caps boat speed as a fraction of the TRUE wind so light air
  // cannot outrun itself (a displacement hull never sails faster than the wind, and dead downwind not past ~0.6 of it).
  const G_SHAPE = [[35, 0.62], [40, 0.72], [45, 0.78], [52, 0.85], [60, 0.90], [70, 0.96], [80, 0.99], [90, 1.00], [100, 1.02], [110, 1.03], [120, 1.02], [135, 0.97], [150, 0.90], [165, 0.82], [180, 0.76]];
  const R_CAP = [[35, 0.70], [45, 0.80], [60, 0.90], [90, 0.95], [120, 0.85], [150, 0.70], [180, 0.60]];
  const interp = (tab, x) => { if (x <= tab[0][0]) return tab[0][1]; for (let i = 1; i < tab.length; i++) { const a = tab[i - 1], b = tab[i]; if (x <= b[0]) return a[1] + (b[1] - a[1]) * (x - a[0]) / (b[0] - a[0]); } return tab[tab.length - 1][1]; };
  const gShape = (twa) => twa < 35 ? 0 : interp(G_SHAPE, twa);
  const rCap = (twa) => interp(R_CAP, twa);
  const apparent = (twa, tws, v) => { const t = Math.abs(twa) * DEG; const ax = tws * Math.cos(t) + v, ay = tws * Math.sin(t);
    return { awa: r1(Math.sign(twa || 1) * Math.atan2(ay, ax) / DEG), aws: r1(Math.hypot(ax, ay)) }; };

  // ---- painted-bbox envelope over the sailing states (measured off the alpha channel) ------------------
  function envelope(R, H) {
    const W = R.W, Hh = R.H;
    const bbox = (p) => { let x0 = 1e9, y0 = 1e9, x1 = -1, y1 = -1, n = 0;
      for (let y = 0; y < Hh; y++) { const row = y * W; for (let x = 0; x < W; x++) if (p[(row + x) * 4 + 3]) { n++; if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; } }
      return { x0, y0, x1, y1, n }; };
    const union = (bs) => bs.reduce((u, b) => ({ x0: Math.min(u.x0, b.x0), y0: Math.min(u.y0, b.y0), x1: Math.max(u.x1, b.x1), y1: Math.max(u.y1, b.y1) }), { x0: 1e9, y0: 1e9, x1: -1, y1: -1 });
    const arr = (b) => [b.x0, b.y0, b.x1, b.y1];
    const st = H.staysail ? { sfurl: 1 } : {};
    const states = {
      close_hauled_max_heel: Object.assign({ awa: 38, aws: H.envelopeAws, hoist: 1, furl: 0, cover: false, frame: 0 }, st, autoTrim(38)),
      run_boom_out: Object.assign({ awa: 172, aws: 12, hoist: 1, furl: 0, cover: false, frame: 0 }, st, autoTrim(172)),
      stored: Object.assign({ awa: 60, aws: 0, hoist: 0, furl: 1, cover: true, frame: 0 }, st)
    };
    const out = { elev_deg: R.defaultElev, cell: { w: W, h: Hh, pivot: [R.pivot.x, R.pivot.y] }, _units: 'cell px, [x0,y0,x1,y1] inclusive, measured off the alpha channel of render(dir, opts) at elev ' + R.defaultElev + ', frame 0, heel from the pose law', states: {} };
    const all = []; let inside = true;
    for (const k of Object.keys(states)) { const bs = [], by = {};
      for (let d = 0; d < 8; d++) { const b = bbox(R.render(d, states[k])); bs.push(b); by[ORDER[d]] = arr(b); if (b.x0 < 0 || b.y0 < 0 || b.x1 >= W || b.y1 >= Hh) inside = false; }
      const u = union(bs); all.push(...bs);
      out.states[k] = { opts: states[k], by_facing: by, union: arr(u), union_size: [u.x1 - u.x0 + 1, u.y1 - u.y0 + 1], painted_px: bs.map(b => b.n) }; }
    const u = union(all);
    out.union_everything = arr(u); out.union_everything_size = [u.x1 - u.x0 + 1, u.y1 - u.y0 + 1]; out.all_inside_cell = inside;
    out.note = 'close_hauled_max_heel is the widest heel the pose law produces (aws ' + H.envelopeAws + ' saturates it); run_boom_out is the boom at 86 deg; stored is the hull alone with the cover on. Pack from union_everything to hold the sailing envelope in one atlas cell.';
    return out;
  }

  // ---- the sailing sidecar ------------------------------------------------------------------------
  function sailing(R, g, H, env) {
    const sp = (o) => R.sailPose(o);
    const hf = hullForm(R);
    const S = g.SAIL, mainA = S.main.area_m2, jibA = S.jib.area_m2, stayA = S.staysail ? S.staysail.area_m2 : null;
    const SA = r2(mainA + jibA), SAD = r2(SA / Math.pow(hf.volume_m3, 2 / 3));
    const fs = R.RIG.forestay, I = r3(fs.head[2] - fs.foot[2]), J = r3(fs.foot[1] - R.RIG.mast.y);
    const hm = /min\((\d+(?:\.\d+)?),\s*(\d+(?:\.\d+)?)\*aws/.exec(S.pose_law.heel_deg || '');
    const HEEL_MAX = hm ? +hm[1] : sp({ awa: 90, aws: 200 }).heelDeg, HEEL_K = hm ? +hm[2] : r3(sp({ awa: 90, aws: 4, main: 0.2, jib: 0.2 }).heelDeg / (16 * 0.75));
    const trimmed = (awa, aws, extra) => sp(Object.assign({ awa, aws, frame: 0 }, autoTrim(awa), extra || {}));
    const report = (p) => { const o = { mode: p.mode, boom_deg: r1(p.boom), headsail_deg: r1(p.jibAngle) }; if (H.staysail) o.staysail_deg = r1(p.stayAngle);
      o.aoa_main_deg = r1(p.aoaMain); o.aoa_headsail_deg = r1(p.aoaJib); o.fill_main = r2(p.fillMain); o.fill_headsail = r2(p.fillJib); if (H.staysail) o.fill_staysail = r2(p.fillStay);
      o.luff_main = r2(p.flogMain); o.luff_headsail = r2(p.flogJib); o.stall_main = r2(p.stallMain); o.heel_deg = r1(p.heelDeg); o.sails_to = p.side < 0 ? 'port' : 'starboard'; return o; };
    const ids = (action) => g.INTERACT.filter(i => i.action === action).map(i => i.id);

    // points of sail — the builder pages' presets, and what the rig reports for each
    const BANDS = [['in_irons', 0, 25, 'pose law: |awa| < 25 with wind = in irons — every sail flogs whole, the boom wanders about the centreline'], ['close_hauled', 25, 50, ''], ['close_reach', 50, 80, ''], ['beam_reach', 80, 100, ''], ['broad_reach', 100, 150, ''], ['run', 150, 180, 'pose law: |awa| > 150 = the headsail is blanketed by the main (fill x0.4)']];
    const POINTS_OF_SAIL = BANDS.map(([id, a0, a1, note]) => { const pr = H.presets[id]; const opts = id === 'in_irons' ? { awa: pr.awa, aws: pr.aws, main: 1, jib: 1 } : Object.assign({ awa: pr.awa, aws: pr.aws }, autoTrim(pr.awa));
      const o = { id, awa_abs_deg: [a0, a1], builder_preset: Object.assign({ hoist: 1, furl: 0, cover: false }, H.staysail ? { sfurl: 1 } : {}, opts), rig_reports: report(sp(Object.assign({ frame: 0 }, opts))) }; if (note) o.note = note; return o; });
    POINTS_OF_SAIL.push({ id: 'becalmed', awa_abs_deg: 'any', builder_preset: Object.assign({ aws: 0, hoist: 1, furl: 0, cover: false }, H.staysail ? { sfurl: 1 } : {}), rig_reports: report(sp({ awa: 60, aws: 0, frame: 0 })), note: 'pose law: aws < 0.5 = becalmed — the boom hangs near the centreline, no fill, no heel' });

    // trim table at the reference wind
    const TRIM_TABLE = { aws_kn: H.awsRef, trim: 'AUTO_TRIM', frame: 0, rows: [] };
    for (let a = 0; a <= 180; a += 5) { const t = autoTrim(a), p = trimmed(a, H.awsRef); TRIM_TABLE.rows.push(Object.assign({ awa: a, main: t.main, jib: t.jib }, report(p))); }

    // heel over awa x aws
    const HEEL_AWA = [0, 15, 30, 45, 60, 75, 90, 105, 120, 135, 150, 165, 180], HEEL_AWS = [2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 24, 30];
    const HEEL = { law: S.pose_law.heel_deg, max_deg: HEEL_MAX, k: HEEL_K, saturates_at_aws_kn: r1(Math.sqrt(HEEL_MAX / HEEL_K)), side: 'to leeward — the sails\' side (sailPose().side: -1 = port)', axis: 'roll about the boat origin (canoe-body bottom); the waterline on the sprite tilts, the shader cut stays level',
      override: 'opts.heelDeg overrides the law per frame; opts.heel:false bakes level. Gameplay may run its own righting-moment model and feed the result in.',
      table: { trim: 'AUTO_TRIM', awa_deg: HEEL_AWA, aws_kn: HEEL_AWS, heel_deg: HEEL_AWA.map(a => HEEL_AWS.map(w => r1(trimmed(a, w).heelDeg))) } };

    // states + transitions
    const st = (o) => Object.assign({}, o, H.staysail ? { sfurl: 1 } : {});
    const STATES = [
      { id: 'sailing', label: 'full sail', opts: st({ hoist: 1, furl: 0, cover: false }), note: 'main up, headsail set' + (H.staysail ? ', staysail furled' : '') + '. The working state — every POINTS_OF_SAIL preset is this state plus wind and trim.' },
      { id: 'main_only', opts: st({ hoist: 1, furl: 1, cover: false }), note: 'headsail furled on the forestay (the roll wears the canvas slot)' },
      { id: 'headsail_only', opts: st({ hoist: 0, furl: 0, cover: false }), note: 'main down — ' + H.headsailOnly + ' — boom sheeted home (max 2 deg); the headsail alone draws' }
    ];
    if (H.staysail) STATES.push(
      { id: 'main_and_staysail', opts: { hoist: 1, furl: 1, sfurl: 0, cover: false }, note: 'genoa furled, the self-tacking staysail set — the builder strip \u201cgenoa furled \u00b7 staysail\u201d. Out of the genoa\u2019s shadow the staysail fills fully.' },
      { id: 'heavy_weather', opts: Object.assign({ hoist: 0.6, furl: 1, sfurl: 0, cover: false }, autoTrim(50)), note: 'the builder\u2019s STAYSAIL ONLY preset: main at 0.6 hoist (the only shortened-main state the rig has — no reef points are modelled), genoa furled, staysail set.' });
    STATES.push(
      { id: 'motoring', opts: st({ hoist: 0, furl: 1, cover: false }), note: 'every sail stowed, cover off. The rig models no engine, propeller, exhaust or wake — propulsion, sound and the water are the game\u2019s.' },
      { id: 'stored', opts: st({ hoist: 0, furl: 1, cover: true }), note: 'THE STORED STATE (builder: STOWED): ' + (H.staysail ? 'main flaked into the Park-Avenue boom with the boom cover zipped' : 'main flaked into the stack pack, zipped') + ', headsail furled' + (H.staysail ? ', staysail furled' : '') + '. cover is only honoured at hoist 0.' });
    if (H.platform) STATES.push({ id: 'platform_up', opts: { platform: 0 }, orthogonal: true, note: 'opts.platform 0 folds the swim platform up as a flat transom door over the stair (default 1 = down). Independent of the sail state; DECK swim_platform in the geometry sidecar is conditional on it.' });
    const tr = (id, param, from, to, action, note) => { const o = { id, param, from, to, drives: action ? { action, interact_ids: ids(action) } : null, grind_handle: (action && H.grind[action]) || null }; if (note) o.note = note; return o; };
    const TRANSITIONS = [
      tr('hoist_main', 'hoist', 0, 1, 'hoist_main', 'luff = P*hoist up the mast; run 1 -> 0 to lower. Continuous — sample as many frames as the cue wants; the builder strips show 0 / 0.5 / 1.'),
      tr('furl_headsail', 'furl', 0, 1, 'furl_jib', 'rolled onto the forestay; run 1 -> 0 to unfurl. The furling line runs down the port side deck to the coaming cleat.'),
      tr('cover', 'cover', false, true, null, 'only at hoist 0. No deck fitting drives it — a crew action at the boom; suggest the halyard_winch station.')
    ];
    if (H.staysail) TRANSITIONS.push(tr('furl_staysail', 'sfurl', 1, 0, null, 'electric furler on the inner forestay — no deck station modelled; the INTERACT furler note says it furls on its own. Default 1 = furled.'));
    if (H.platform) TRANSITIONS.push(tr('fold_platform', 'platform', 1, 0, 'fold_platform', '1 = down (swim platform + transom stair open), 0 = up (transom door shut over the stair).'));
    TRANSITIONS.push({ id: 'door', param: 'doorOpen', from: 0, to: 1, drives: { threshold: g.THRESHOLD.id }, cue: g.THRESHOLD.door_cue, note: 'the companionway — see THRESHOLD in the geometry sidecar; 8 baked frames, played reversed on exit' });

    // manoeuvres — the boom path the pose law produces through a tack and a gybe (auto-trimmed both ends)
    const sweep = (a0, a1, n, aws) => { const out = []; for (let i = 0; i <= n; i++) { const awa = a0 + (a1 - a0) * i / n, p = trimmed(awa, aws); const o = { awa: r1(wrap180(awa)), boom_signed_deg: r1(p.boomSigned), headsail_signed_deg: r1(p.jibSigned), mode: p.mode }; if (H.staysail) o.staysail_signed_deg = r1(p.staySigned); out.push(o); } return out; };
    const tack = sweep(40, -40, 8, H.presets.close_hauled.aws), gybe = sweep(170, 190, 8, H.presets.run.aws);
    const MANOEUVRES = {
      sign: 'signed angles: negative = boom / clew to PORT (wind over the starboard bow), positive = to starboard',
      tack: { from_awa: 40, to_awa: -40, aws_kn: H.presets.close_hauled.aws, boom_sweep_deg: r1(Math.abs(tack[0].boom_signed_deg - tack[tack.length - 1].boom_signed_deg)), through: 'in irons (|awa| < 25) — both sails flog, the boom wanders about the centreline, heel drops to 15%', path: tack },
      gybe: { from_awa: 170, to_awa: -170, aws_kn: H.presets.run.aws, boom_sweep_deg: r1(Math.abs(gybe[0].boom_signed_deg - gybe[gybe.length - 1].boom_signed_deg)), through: 'dead downwind — the boom flips side as awa crosses 180; the pose law has no slam or preventer, the swing is instantaneous between samples', path: gybe },
      boom_underside_over_cockpit_sole_m: S.boom.clearance_over_cockpit_sole_m, boom_end_over: S.boom.end_over,
      note: 'the sprite follows awa; a tack or gybe is gameplay turning the boat through the wind and feeding the rig the awa of each frame. Timing is gameplay\u2019s.'
    };

    // controls: INTERACT action -> rig opt
    const ctl = (action, param, range, effect, extra) => { const list = ids(action); if (!list.length) return null; return Object.assign({ action, interact_ids: list, param, range, effect, grind_handle: H.grind[action] || null }, extra || {}); };
    const CONTROLS = [
      ctl('enter_helm', 'steer', [-1, 1], 'renderWheel(dir, {steer}) turns the wheel' + (ids('enter_helm').length > 1 ? 's (twin wheels, one rudder — either station steers)' : '') + '; the rudder follows with opts.underbody', { sense: '-1 port lock .. +1 starboard lock' }),
      ctl('trim_main', 'main', [0, 1], 'boom limit = 4 + 82*(1-main) deg; the boom sits at min(limit, |awa|-6) to leeward. 1 = hardened (4 deg), 0 = eased (86 deg)', H.staysail ? { traveller_car: 'x = side * ' + R.RIG.traveller.hx + ' * 0.9 * (1-main)^0.7 across the aft-deck track (ANCHORS.travellerCar)' } : {}),
      ctl('trim_jib', 'jib', [0, 1], 'headsail limit = 9 + 76*(1-jib) deg; the clew sits at min(limit, |awa|-6). ' + (H.staysail ? 'The staysail is self-tacking: min(headsail angle, ' + R.STAY.maxDeg + ' deg), its car rides the curved track.' : 'The working sheet is the leeward one.')),
      ctl('hoist_main', 'hoist', [0, 1], 'luff = P*hoist; 0 parks the boom (max 2 deg) and grows the ' + (H.staysail ? 'flaked pile in the boom' : 'stack pack')),
      ctl('furl_jib', H.staysail ? 'furl (+ sfurl for the staysail)' : 'furl', [0, 1], 'rolled onto the forestay; rolled radius grows with furl, the UV strip is the canvas slot'),
      ctl('anchor', null, null, 'the anchor hangs on the bow roller arm; no chain-out or riding state in pass 1 — an anchored boat is a gameplay flag on the same sprite'),
      ctl('fold_platform', 'platform', [0, 1], '1 down, 0 up (transom door)')
    ].filter(Boolean);

    // wind frame + the true -> apparent glue
    const WIND = {
      awa: 'degrees, wrapped to -180..180 by the rig; + = wind over the STARBOARD bow (sails set to port)', aws: 'knots',
      from_true: { heading_deg: 'dir * 45 (N = 0, clockwise) plus any yaw the game runs between facings', twa_deg: 'wrap180(wind_from_deg - heading_deg)',
        aws_kn: 'sqrt(tws^2 + v^2 + 2*tws*v*cos(twa))', awa_deg: 'sign(twa) * atan2(tws*sin|twa|, tws*cos|twa| + v)', v: 'boat speed through the water, knots (POLAR_REFERENCE or the game\u2019s own)' },
      thresholds: { becalmed_below_aws_kn: 0.5, in_irons_below_awa_deg: 25, fill_saturates_at_aws_kn: 6, luff_below_aoa_deg: 8, stall_above_aoa_deg: 40, blanketed_above_awa_deg: 150, heel_saturates_at_aws_kn: r1(Math.sqrt(HEEL_MAX / HEEL_K)), heel_max_deg: HEEL_MAX },
      note: 'the rig only ever sees awa/aws/sheets. Everything before that — true wind, boat speed, leeway, current — is gameplay\u2019s; this block is the glue so the sprite agrees with the physics.'
    };

    // polar reference — true wind in, boat speed + the resulting apparent wind and rig pose out
    const twsH = r2(80 / SAD), vh = hf.hull_speed_kn;
    const TWS = [4, 6, 8, 10, 12, 14, 16, 18, 20, 25], TWA = G_SHAPE.map(g => g[0]);
    const speed = (twa, tws) => r2(Math.min(vh * (1 - Math.exp(-tws / twsH)) * gShape(twa), rCap(twa) * tws));
    const rows = [], vmg = [];
    for (const tws of TWS) { let up = null, dn = null;
      for (const twa of TWA) { const v = speed(twa, tws), ap = apparent(twa, tws, v), p = trimmed(ap.awa, ap.aws);
        rows.push({ twa, tws, v_kn: v, awa: ap.awa, aws: ap.aws, heel_deg: r1(p.heelDeg), mode: p.mode });
        const c = v * Math.cos(twa * DEG); if (twa < 90 && (!up || c > up.vmg_kn)) up = { twa, v_kn: v, vmg_kn: r2(c), awa: ap.awa }; if (twa > 90 && (!dn || -c > dn.vmg_kn)) dn = { twa, v_kn: v, vmg_kn: r2(-c), awa: ap.awa }; }
      vmg.push({ tws, upwind: up, downwind: dn }); }
    const POLAR_REFERENCE = {
      status: 'REFERENCE, art-side. Gameplay owns the polar and drive (SAIL.pose_law note in the geometry sidecar). This one exists so a first physics pass and the sprite agree; replace the model, keep the glue.',
      model: { v_kn: 'min( v_hull * (1 - exp(-tws / tws_h)) * G(twa),  R(twa) * tws )', v_hull_kn: vh, v_hull_rule: '1.34 * sqrt(LWL_ft) — the displacement-hull limit; G lets a reach run 3% over it', tws_h_kn: twsH, tws_h_rule: '80 / (SA/D) — the true wind that brings her to 63% of hull speed', G: G_SHAPE, G_rule: 'linear between the points; 0 below twa 35 (no-go)', R: R_CAP, R_rule: 'boat speed never exceeds this fraction of the TRUE wind — the light-air governor; linear between the points' },
      inputs: { LWL_m: hf.LWL_m, displacement_kg: hf.displacement_kg, upwind_sail_area_m2: SA, SA_D: SAD, DL_ratio: hf.DL_ratio },
      grid: { tws_kn: TWS, twa_deg: TWA }, best_vmg: vmg, rows,
      no_go: 'G = 0 below twa 35. Note the rig reads IN IRONS below awa 25, and awa is tighter than twa when she is moving (see rows where mode is "in irons" at twa 35–40): a boat sailed at the polar\u2019s edge shows flogging sails. Pinch to the sprite\u2019s 25 deg apparent, not the polar\u2019s 35 true.',
      surfing_planing: 'not modelled — the shape caps at 1.03 * v_hull. A performance hull on a broad reach in a big breeze will exceed it; that is gameplay\u2019s to add.'
    };

    const ANIMATION = { frames: 8, loop: 'phase = 2*pi*frame/8 — cloth flutter at the luff, the in-irons boom wander, the winch handle when opts.grind is set; rock:true adds rock(frame) (roll/pitch/heave) and boat interiors take the same pose',
      rock: { frames: R.ROCK.frames, roll_deg: R.ROCK.rollA, pitch_deg: R.ROCK.pitchA, heave_px: R.ROCK.heaveA, period_s: R.ROCK.period, suggested_ms_per_frame: Math.round(R.ROCK.period * 1000 / 8) },
      grind: { param: 'grind', values: ['jib', 'main'], where: H.grindWhere, note: 'a turning handle on that winch, one revolution per 8 frames; the LEEWARD winch is chosen from awa' },
      door_cue: g.THRESHOLD.door_cue, heel: 'a render param — heel is not animated by the rig, it follows aws/awa/fill each frame' };

    const out = {
      schema: 'hidden-harbours/boat-sailing@1', rig: H.file, exportSymbol: H.symbol,
      pairs_with: { gameplay_sidecar: H.file.replace('.js', '.gameplay.json'), same_rig_hash: true, uses: ['SAIL', 'INTERACT', 'THRESHOLD', 'WATERLINE', 'ANCHORS'] },
      units: 'metres · degrees · knots · kg',
      frame: g.frame,
      authoring: 'Generated by SAIL_KIT.sailing() (Art/_sailKit.js) off the live rig: sailPose() sampled for every pose number, the loft integrated for the hull form, the bake measured for the envelope, INTERACT read from the geometry sidecar generated in the same run. AUTO_TRIM is the builder page\u2019s law verbatim. POLAR_REFERENCE is a stated model over rig-derived inputs, flagged. Do not hand-edit — re-run SAIL_KIT.write().',
      HULL_FORM: hf,
      SAIL_PLAN: Object.assign({ rig: S.rig, I_m: I, J_m: J, P_m: S.main.P_m, E_m: S.main.E_m, main_area_m2: mainA, headsail_area_m2: jibA, headsail: S.jib.kind || 'jib', headsail_overlap: S.jib.overlap },
        stayA != null ? { staysail_area_m2: stayA, staysail_self_tacking_max_deg: R.STAY.maxDeg } : {},
        { upwind_area_m2: SA, SA_D: SAD, masthead_z: S.mast.head_z, boom_length_m: S.boom.length_m, boom_underside_over_cockpit_sole_m: S.boom.clearance_over_cockpit_sole_m, provenance: 'areas from SAIL in the geometry sidecar (main 0.5*P*E*roach factor; headsails 0.5*luff*LP); I = forestay head to forestay foot, J = forestay foot to the mast centreline at the deck (RIG consts)' }),
      AUTO_TRIM: { law: 'main sheet limit = clamp(|awa|-18, 0, 86) deg -> main = clamp01(1-(limit-4)/82); headsail limit = clamp(|awa|-16, 0, 85) -> jib = clamp01(1-(limit-9)/76)', meaning: '18 deg of attack on the main and 16 on the headsail — full fill (fill saturates at aoa 18) — until the sheet limits (86 / 85) cap them downwind', source: H.builder + ' _autoTrim(), the builder page\u2019s \u2726 AUTO-TRIM button; the trim every preset, strip and table in this file was sampled at' },
      POINTS_OF_SAIL, TRIM_TABLE, HEEL, STATES, TRANSITIONS, MANOEUVRES, CONTROLS, WIND, POLAR_REFERENCE, ANIMATION,
      SPRITE_ENVELOPE: env,
      _excluded: {
        polar_ownership: 'the polar is gameplay\u2019s (SAIL.pose_law). POLAR_REFERENCE is a reference model, not a claim about the boat.',
        reefing: 'no reef points modelled on either hull. hoist < 1 shortens the luff along the mast and grows the pile on the boom — the ' + (H.staysail ? 'heavy_weather state (hoist 0.6 + staysail)' : 'only shortened-main picture') + ' is the nearest thing to a reef.',
        engine: 'no engine, propeller, exhaust or wake. motoring is a sail state (everything stowed), the propulsion is the game\u2019s.',
        spinnaker: 'no downwind sail; dead downwind the headsail is blanketed and that is the picture.',
        leeway_current: 'not in the rig; the sprite has no crab angle parameter — heading is the facing.',
        anchoring_state: 'the anchor hangs on the roller in every state; no chain, no riding sail, no anchor light.'
      },
      _confirm: {
        polar_numbers: 'REFERENCE model — replace with the game\u2019s VPP; keep WIND.from_true and the pose loop.',
        no_go_coupling: 'the polar\u2019s 35 deg true vs the rig\u2019s 25 deg apparent in-irons band: rule which the player feels. Rows in POLAR_REFERENCE whose mode reads "in irons" are exactly the cells where the sprite would flog at the polar\u2019s speed' + (H.staysail ? ' — on the 88 that is twa 40 above ~10 kn true: either pinch less in the polar (lower G at 40) or narrow the rig\u2019s irons band for her (a rig edit; the hash moves).' : '.'),
        displacement: 'canoe body only (' + hf.displacement_kg + ' kg at the DWL); keel and bulb not integrated — add their volume before quoting a displacement.',
        heavy_weather_as_reef: H.staysail ? 'hoist 0.6 + staysail is the builder\u2019s preset, not a naval-architecture reef; rule whether it is a gameplay state.' : 'no reef state exists; rule whether hoist 0.7 reads as a reef or whether the rig needs reef points.',
        heel_override: 'the heel law is art-side (capped ' + HEEL_MAX + ' deg); gameplay may drive heelDeg from its own righting moment.',
        envelope: env ? 'measured at elev ' + env.elev_deg + ' only; the rigs\u2019 cell-fit sweeps cover elev 30–50 (see the rig header)' : 'not measured in this run'
      }
    };
    return out;
  }

  // ---- write the kit --------------------------------------------------------------------------------
  async function write(io) {
    const { readFile, saveFile, log, dir } = io;
    const SE = root.SidecarExport; if (!SE) throw new Error('SAIL_KIT: load Art/_sidecarExport.js first');
    const sums = [], summary = {};
    const add = async (path, text) => { await saveFile(path, text); sums.push([await SE.sha256Hex(text), path.replace(dir, '')]); };
    for (const key of Object.keys(HULLS)) { const H = HULLS[key], t0 = performance.now();
      const src = await readFile(H.rig); (0, eval)(src); const R = root[H.symbol]; if (!R || !R.gameplayGeometry) throw new Error('SAIL_KIT: ' + H.symbol + ' did not load');
      const sha = await SE.sha256Hex(src);
      const gameplay = SE.stampRigSha(R.gameplayGeometry(), sha);
      const env = io.envelope === false ? null : envelope(R, H);
      const sail = SE.stampRigSha(sailing(R, gameplay, H, env), sha);
      const gj = JSON.stringify(gameplay, null, 2), sj = JSON.stringify(sail, null, 2);
      const gName = H.file.replace('.js', '.gameplay.json'), sName = H.file.replace('.js', '.sailing.json');
      await add(dir + H.folder + H.file, src); await add(dir + H.folder + gName, gj); await add(dir + H.folder + sName, sj);
      await saveFile('Art/gameplay/' + gName, gj); await saveFile('Art/gameplay/' + sName, sj);
      summary[key] = { sha, rig_bytes: src.length, gameplay_sections: Object.keys(gameplay).filter(k => /^[A-Z]/.test(k)), sailing_sections: Object.keys(sail).filter(k => /^[A-Z]/.test(k)),
        hull: sail.HULL_FORM, sail_plan: sail.SAIL_PLAN, heel: { max: sail.HEEL.max_deg, k: sail.HEEL.k, sat: sail.HEEL.saturates_at_aws_kn }, polar: sail.POLAR_REFERENCE.model, vmg12: sail.POLAR_REFERENCE.best_vmg.find(v => v.tws === 12),
        envelope: env ? { all_inside_cell: env.all_inside_cell, union: env.union_everything, size: env.union_everything_size } : null, ms: Math.round(performance.now() - t0) };
      log(key, 'sha', sha.slice(0, 12), 'gameplay', gj.length, 'B', 'sailing', sj.length, 'B', 'ms', summary[key].ms); }
    await saveFile(dir + 'SHA256SUMS.txt', sums.map(s => s[0] + '  ' + s[1]).join('\n') + '\n');
    return summary;
  }

  root.SAIL_KIT = { HULLS, autoTrim, hullForm, envelope, sailing, write, G_SHAPE };
})(typeof globalThis !== 'undefined' ? globalThis : window);
