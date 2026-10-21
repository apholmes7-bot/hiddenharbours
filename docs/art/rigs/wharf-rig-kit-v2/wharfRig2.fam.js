/* Hidden Harbours — WHARF RIG, PASS 2 · THE FAMILIES (adds to globalThis.WharfGeo2).
   quay · pier · crib · float · finger · gangway · slipway · riprap · dolphin · catwalk, the presets, spec
   resolution (TreeRig4's parameters: variant 0–3 · season · stage + the structure's own dimensions), the
   fittings laid out from the berths, the module sockets, and the two things rebuilt every frame (a gangway
   solved between a fixed hinge and a moving float, and a float's mooring chain).                         */
(function (root) {
  'use strict';
  const G = root.WharfGeo2; if (!G) throw new Error('wharfRig2.fam.js: load Art/wharfRig2.geo.js first');
  const { clamp, mulberry32, CTX, at, F, wall, slab, box, tube, pipe, beam, torusZ, decalY, decalZ, frame, matAtZ, zSplit, isBarn, texFor,
    bandedPile, weedFringe, bandedWall, sheetWall, blockWall, FIT, ladderAt, tyreAt, foamAt, panelAt, cleatAt, bollardAt, ringAt, railRun, bullRail, cutRuns,
    dolphinCluster, rubbleStone, DECK, plankDeck, underFrame, icicles, berthPlan, formTex, grainTex, sawnTex, plankTex, ribTex, rockTex, rustTex, treadTex, cubeTex,
    gratingTex, blockTex, paveTex, slabTex, iceTex } = G;

  const STAGES = { new: 0.05, seasoned: 0.35, weathered: 0.65, derelict: 0.95 }, STAGE_KEYS = ['new', 'seasoned', 'weathered', 'derelict'];
  const SEASONS = ['summer', 'autumn', 'winter'], VARIANTS = 4;
  const FLOATING = { float: 1, finger: 1 };
  // a float carries its growth at its OWN waterline (z = 0 in its frame), not on the datum frame
  function floatFrame(T) { return Object.assign({}, T, { stainTop: 0.10, barnTop: -0.03, barnBot: -0.30, weedTop: 0.03, weedBot: -0.55, iceBot: -0.03, iceTop: 0.07, rimeBot: 0.0, rimeTop: 0.22, collar: -99, mid: 0 }); }

  // ============================ GANGWAY BODY (built along +x, then placed) ==========================
  function rampBody(out, x0, run, wid, topZ, botZ, T, s) {
    const hw = wid / 2, x1 = x0 + run, dz = topZ - botZ, trussD = 0.30, zAt = (x) => topZ - dz * ((x - x0) / run), Gw = FIT.gang, slopeL = Math.hypot(run, dz);
    at('gangway', CTX.grp, 1);
    for (const sy of [-1, 1]) { const y = sy * (hw - 0.03);
      for (const off of [0, -trussD]) tube(out, [x0, y, zAt(x0) + off - 0.04], [x1, y, zAt(x1) + off - 0.04], 0.035, 6, 'alum', off ? -0.9 : -0.4, null, true);
      const nd = Math.max(4, Math.round(run / 0.9));
      for (let i = 0; i < nd; i++) { const xa = x0 + run * (i / nd), xb = x0 + run * ((i + 1) / nd), up = i % 2 === 0;
        tube(out, [xa, y, zAt(xa) - (up ? trussD : 0) - 0.04], [xb, y, zAt(xb) - (up ? 0 : trussD) - 0.04], 0.022, 5, 'alum', -1.1, null, false); }
      const ns = Math.max(3, Math.round(run / 1.6)) + 1;
      for (let i = 0; i < ns; i++) { const x = x0 + run * (i / (ns - 1)); pipe(out, x, y, 0.026, zAt(x), zAt(x) + Gw.rail, 'alum', -0.5, 8); }
      for (const h of [Gw.rail, Gw.mid]) tube(out, [x0, y, zAt(x0) + h], [x1, y, zAt(x1) + h], 0.024, 8, 'alum', -0.3, null, true);
    }
    at('gangway', CTX.grp, 0);
    const nt = Math.max(6, Math.round(slopeL / Gw.tread));
    for (let i = 0; i < nt; i++) { const xa = x0 + run * (i / nt), xb = x0 + run * ((i + 0.86) / nt);
      out.push(F([[xa, -hw + 0.04, zAt(xa)], [xb, -hw + 0.04, zAt(xb)], [xb, hw - 0.04, zAt(xb)], [xa, hw - 0.04, zAt(xa)]], 'alum', -0.85, 0, [[0, 0], [0.26, 0], [0.26, wid], [0, wid]], treadTex(0.30))); }
    out.push(F([[x0, -hw + 0.04, zAt(x0) - 0.03], [x1, -hw + 0.04, zAt(x1) - 0.03], [x1, hw - 0.04, zAt(x1) - 0.03], [x0, hw - 0.04, zAt(x0) - 0.03]], 'alum', -1.3));
    box(out, x0 - 0.22, x0 + 0.06, -hw, hw, topZ - 0.06, topZ + 0.02, 'galv', 0.1, rustTex());                       // hinge plate
    out.push(F([[x1, -hw + 0.06, botZ + 0.01], [x1 + 0.45, -hw + 0.06, botZ + 0.01], [x1 + 0.45, hw - 0.06, botZ + 0.01], [x1, hw - 0.06, botZ + 0.01]], 'galv', 0.1, 0.02)); // toe plate
    for (const sy of [-1, 1]) tube(out, [x1 - 0.10, sy * (hw - 0.10), botZ + 0.06], [x1 - 0.10, sy * (hw - 0.02), botZ + 0.06], 0.075, 10, 'iron', 0.05, null, true);
    return Math.atan2(dz, run);
  }
  function xform(faces, rot, tx, ty, tz) { const c = Math.cos(rot), s = Math.sin(rot);
    for (const f of faces) f.v = f.v.map(p => [tx + p[0] * c - p[1] * s, ty + p[0] * s + p[1] * c, p[2] + (tz || 0)]); return faces; }
  // a gangway of fixed length L hinged at `hinge`, heading (ux, uy), landing on a deck at landZ: the span shortens
  // as the drop grows, so the toe rolls along the float deck (rollers), the hinge never moves
  function gangwayFaces(out, hinge, ux, uy, L, landZ, wid, T, s) {
    const dz = Math.max(0, hinge[2] - landZ), span = Math.sqrt(Math.max(0.25, L * L - dz * dz)), tmp = [];
    rampBody(tmp, 0, span, wid, hinge[2], landZ, T, s); xform(tmp, Math.atan2(uy, ux), hinge[0], hinge[1], 0);
    for (const f of tmp) out.push(f);
    return { span: +span.toFixed(3), dz: +dz.toFixed(3), angDeg: +(Math.atan2(dz, span) / G.DEG).toFixed(2), land: [hinge[0] + ux * span, hinge[1] + uy * span, landZ] };
  }
  // the ramp length that makes 1:3 (18.43°) at chart datum: drop / sin, rounded UP to 0.1 m so the slope never exceeds it
  function gangLength(dropAtDatum) { return clamp(Math.ceil(dropAtDatum / Math.sin(FIT.gang.maxSlope * G.DEG) * 10 - 1e-9) / 10, 4, 24); }
  function chainFaces(out, p0, p1, slack) {
    const n = 9; at('chain', 0, 0);
    for (let i = 0; i < n; i++) { const t0 = i / n, t1 = (i + 1) / n, cz = (a) => p0[2] + (p1[2] - p0[2]) * a - Math.sin(a * Math.PI) * slack;
      tube(out, [p0[0] + (p1[0] - p0[0]) * t0, p0[1] + (p1[1] - p0[1]) * t0, cz(t0)], [p0[0] + (p1[0] - p0[0]) * t1, p0[1] + (p1[1] - p0[1]) * t1, cz(t1)], 0.030, 5, 'galv', 0.05, null, false); }
  }

  // ============================ FAMILIES ===================================================================
  const FAMILIES = {
    quay: { label: 'quay', note: 'solid face on fill: mass concrete, driven steel or timber sheet, or granite ashlar — the deep-water berth', kind: 'quay', faces: ['water'],
      dims: { bays: [1, 12, 4], bayLen: [2.4, 4.2, 3.0], width: [4, 14, 7] },
      build(out, s, T) {
        const L = s.L, hx = L / 2, hy = s.W / 2, top = s.deckZ, base = Math.min(s.bedMin, -0.4), kind = s.face === 'steelSheet' ? 'steel' : s.face === 'timberSheet' ? 'timber' : null;
        CTX.ux = hx; at('face', 0, 0);
        const sides = [[-hx, hy, hx, hy, 0.0], [hx, -hy, -hx, -hy, -0.55]];
        if (s.ends.px !== 'join') sides.push([hx, hy, hx, -hy, -0.15]); if (s.ends.nx !== 'join') sides.push([-hx, -hy, -hx, hy, -0.3]);
        for (const [x0, y0, x1, y1, b] of sides) { if (kind) sheetWall(out, x0, y0, x1, y1, base, top, kind, T, s, b); else if (s.face === 'block') blockWall(out, x0, y0, x1, y1, base, top - 0.30, T, s, b); else bandedWall(out, x0, y0, x1, y1, base, top - 0.24, 'conc', T, s, b); }
        const cm = s.face === 'block' ? 'block' : 'conc', ex = (e) => e === 'join' ? 0 : 0.06, cx0 = -hx - ex(s.ends.nx), cx1 = hx + ex(s.ends.px), ct = s.face === 'block' ? 0.30 : kind ? 0.22 : 0.24;
        at('coping', 0, 0); box(out, cx0, cx1, -hy - 0.06, hy + 0.06, top - ct, top, cm, 0.10, s.face === 'block' ? blockTex() : formTex(0.5), true);
        at('deck', 0, 0); slab(out, [[cx0, -hy - 0.06], [cx1, -hy - 0.06], [cx1, hy + 0.06], [cx0, hy + 0.06]], top, cm, 0.06, s.face === 'block' ? paveTex() : slabTex(s.bayLen, 2.4));
        if (!kind) { at('face', 0, 0); const wz = top - 0.62;                                                        // rubbing strake
          for (const g of zSplit(wz - 0.10, wz + 0.10, 'wood', T, s)) for (const [a, b] of cutRuns(-hx, hx, s.open.water)) box(out, a, b, hy + 0.02, hy + 0.14, g.a, g.b, g.mat, 0.1, sawnTex(), true);
          for (let i = 0; i < s.bays; i++) { const x = -hx + (i + 0.5) * s.bayLen, z = Math.max(base + 0.2, T.mid + 0.35); decalY(out, hy, 1, x - 0.08, x + 0.08, z, z + 0.14, 'concS', -1.1, null, 0.04); } }
        icicles(out, -hx, hx, hy + 0.07, top - ct, s);
      } },

    pier: { label: 'timber pile pier', note: 'piles → caps → stringers → planking, X-braced bents; open, sheeted or on steel piles; plank or concrete deck', kind: 'pier', faces: ['water', 'shore'],
      dims: { bays: [1, 12, 4], bayLen: [2.4, 3.6, 3.0], width: [2.4, 12, 4.2] },
      build(out, s, T) {
        const L = s.L, hx = L / 2, hy = s.W / 2, top = s.deckZ, concD = s.deck === 'concrete', dT = concD ? 0.32 : DECK.plank + DECK.stringer[1];
        const capTop = top - dT, pileTop = capTop - DECK.cap[1], steelP = s.struct === 'steelPile', pileMat = steelP ? (s.age > 0.55 ? 'rust' : 'steel') : 'pole', pileR = steelP ? s.pileR * 0.9 : s.pileR;
        const across = s.W > 8 ? 4 : s.W > 4.6 ? 3 : 2, pileY = []; for (let k = 0; k < across; k++) pileY.push(-hy + 0.42 + (s.W - 0.84) * (k / (across - 1)));
        CTX.ux = hx; const rnd = mulberry32(s.variant * 313 + 17);
        for (let i = s.ends.nx === 'join' ? 1 : 0; i <= s.bays; i++) { const x = -hx + i * s.bayLen;
          at('pile', 0, 0); for (const y of pileY) bandedPile(out, x, y, pileR, s.bedAt(x, y), pileTop, pileMat, T, s, steelP ? 12 : 10, s.capIron && !steelP ? 'iron' : null);
          at('cap', 0, 0); for (const g of zSplit(pileTop, capTop, concD ? 'conc' : 'wood', T, s)) box(out, x - DECK.cap[0] / 2, x + DECK.cap[0] / 2, -hy + 0.18, hy - 0.18, g.a, g.b, g.mat, 0.02, concD ? formTex(0.5) : sawnTex(), true);
          if (s.brace && !steelP) { at('brace', 0, 0); const bed = Math.max(s.bedAt(x, pileY[0]), s.bedAt(x, pileY[across - 1])), zA = Math.max(bed + 0.35, Math.min(pileTop - 0.55, T.mid + 0.25)), zB = pileTop - 0.35, y0 = pileY[0], y1 = pileY[across - 1];
            if (zB - zA > 0.4) for (const [pa, pb] of [[[y0, zA], [y1, zB]], [[y1, zA], [y0, zB]]]) tube(out, [x, pa[0], pa[1]], [x, pb[0], pb[1]], 0.05 * 0.62 * 2, 4, matAtZ((pa[1] + pb[1]) / 2, 'wood', T, s), -0.35, null, true); }
        }
        if (s.brace) { at('waler', 0, 0); for (const y of [pileY[0], pileY[across - 1]]) { const wz = Math.max(s.bedMin + 0.5, Math.min(pileTop - 0.9, T.mid + 0.55));
          for (const g of zSplit(wz - 0.09, wz + 0.09, 'wood', T, s)) box(out, -hx - (s.ends.nx === 'join' ? 0 : 0.05), hx + (s.ends.px === 'join' ? 0 : 0.05), y - (y > 0 ? 0.02 : 0.16), y + (y > 0 ? 0.16 : 0.02), g.a, g.b, g.mat, -0.2, sawnTex(), true);
          if (s.season === 'autumn') for (let k = 0; k < Math.max(1, s.bays - 1); k++) { if (rnd() < 0.45) continue; const x = -hx + rnd() * L; at('weed', 0, 0);
            tube(out, [x, y + (y > 0 ? 0.17 : -0.17), wz + 0.05], [x + 0.04, y + (y > 0 ? 0.2 : -0.2), wz - 0.35 - rnd() * 0.3], 0.03, 5, 'weed', -0.5, null, false); at('waler', 0, 0); } } }
        if (s.struct === 'sheeted') { at('face', 0, 0); sheetWall(out, -hx, hy - 0.02, hx, hy - 0.02, s.bedMin, capTop - 0.02, steelP ? 'steel' : 'timber', T, s, 0.1); }
        if (concD) { at('deck', 0, 0); const cx0 = -hx - (s.ends.nx === 'join' ? 0 : 0.05), cx1 = hx + (s.ends.px === 'join' ? 0 : 0.05);
          box(out, cx0, cx1, -hy, hy, capTop, top, 'conc', 0.05, formTex(0.5), true); slab(out, [[cx0, -hy], [cx1, -hy], [cx1, hy], [cx0, hy]], top, 'conc', 0.06, slabTex(s.bayLen, 2.0)); s._holes = [];
        } else { underFrame(out, -hx, hx, -hy, hy, top, s, T); s._holes = plankDeck(out, -hx, hx, -hy, hy, top, s, T); }
        icicles(out, -hx, hx, hy + 0.02, top - DECK.plank, s);
      } },

    crib: { label: 'stone-filled crib wharf', note: 'drift-pinned log crib on rubble ballast, plank or cast cap — the Atlantic Canada vernacular', kind: 'crib', faces: ['water'],
      dims: { bays: [1, 8, 3], bayLen: [2.6, 4.2, 3.2], width: [3.6, 10, 5] },
      build(out, s, T) {
        const L = s.L, hx = L / 2, hy = s.W / 2, top = s.deckZ, t = 0.28, cribTop = top - DECK.plank - 0.30, base = s.bedMin, courses = Math.max(3, Math.round((cribTop - base) / t)), rnd = mulberry32(s.variant * 7717 + 13);
        CTX.ux = hx; at('crib', 0, 0);
        const xa = -hx, xb = hx, n = Math.max(2, s.bays + 1);
        for (let c = 0; c < courses; c++) { const z0 = base + c * t, z1 = Math.min(cribTop, z0 + t * 0.94); if (z0 >= cribTop) break;
          const m = matAtZ((z0 + z1) / 2, 'wood', T, s), bump = isBarn(m) ? 0.02 : 0, zc = (z0 + z1) / 2;
          if (c % 2 === 0) { for (const y of [-hy + t / 2, hy - t / 2]) tube(out, [xa, y, zc], [xb, y, zc], t * 0.48 + bump, 8, m, isBarn(m) ? 0.3 : 0, grainTex(0.3), true); }
          else { for (let i = s.ends.nx === 'join' ? 1 : 0; i < n; i++) { const x = -hx + L * (i / (n - 1)); tube(out, [x, -hy - 0.10, zc], [x, hy + 0.10, zc], t * 0.46 + bump, 8, m, isBarn(m) ? 0.3 : 0, grainTex(0.3), true); }
            for (const y of [-hy + t / 2, hy - t / 2]) tube(out, [xa, y, zc], [xb, y, zc], t * 0.34, 6, m, isBarn(m) ? 0.2 : -0.3, null, false); }
          if (c % 2 === 1 && c > 1) for (let i = 0; i <= s.bays; i++) pipe(out, -hx + i * s.bayLen, hy - t / 2, 0.022, z1 - 0.02, z1 + 0.10, matAtZ(z1, 'galv', T, s), 0.2, 6);
          if (s.growth.weed && T.on.weed && Math.abs(zc - T.weedTop) < t) weedFringe(out, hx * 0.55, hy - t / 2, t * 0.5, T.weedTop, s);
        }
        at('fill', 0, 0); const fillZ = cribTop - 0.10;
        for (let i = 0; i < Math.max(10, s.bays * 7); i++) { const px = -hx + 0.5 + rnd() * (L - 1), py = -hy + 0.55 + rnd() * (s.W - 1.1), rr = 0.16 + rnd() * 0.22, m = matAtZ(fillZ, 'stone', T, s);
          pipe(out, px, py, rr, fillZ - rr * 0.6, fillZ + rr * 0.45 * (0.4 + rnd()), m, -0.2 + rnd() * 0.5, 6, rockTex()); }
        at('frame', 0, 0); for (let i = 0; i <= s.bays; i++) { const x = -hx + i * s.bayLen; box(out, x - 0.14, x + 0.14, -hy + 0.1, hy - 0.1, cribTop, top - DECK.plank, matAtZ(cribTop, 'wood', T, s), 0.0, sawnTex(), true); }
        if (s.cap === 'concrete') { at('deck', 0, 0); box(out, -hx - 0.05, hx + 0.05, -hy - 0.05, hy + 0.05, top - 0.26, top, 'conc', 0.08, formTex(0.5), true); slab(out, [[-hx - 0.05, -hy - 0.05], [hx + 0.05, -hy - 0.05], [hx + 0.05, hy + 0.05], [-hx - 0.05, hy + 0.05]], top, 'conc', 0.06, formTex(0.9)); s._holes = []; }
        else s._holes = plankDeck(out, -hx, hx, -hy, hy, top, s, T);
        icicles(out, -hx, hx, hy + 0.02, top - DECK.plank, s);
      } },

    float: { label: 'floating dock', note: 'rides the tide and the sea on guide piles: timber + foam, HDPE cubes, concrete pontoon or poly drums', kind: 'float', faces: ['water', 'shore'],
      dims: { bays: [1, 8, 3], bayLen: [2.4, 3.6, 3.0], width: [1.8, 4.0, 2.4] },
      build(out, s, T) { floatBody(out, s, T, s.grp || 1); } },

    finger: { label: 'finger float', note: 'the slip between two boats: a narrow float pinned to a main float, one pile at its tip', kind: 'finger', faces: ['water', 'shore'],
      dims: { bays: [1, 4, 2], bayLen: [2.4, 3.0, 3.0], width: [0.9, 1.5, 1.2] },
      build(out, s, T) { floatBody(out, s, T, s.grp || 1); } },

    gangway: { label: 'gangway', note: 'aluminium ramp, fixed length, hinged on the wharf, its toe rolling on the float — re-solved every frame', kind: 'gangway', faces: [],
      dims: { bays: [1, 1, 1], bayLen: [4, 24, 8], width: [1.1, 1.6, 1.24] },
      build(out, s, T) { at('gangway', s.grp || 2, 0); const land = s.landZ, r = gangwayFaces(out, [-s.L / 2, 0, s.deckZ], 1, 0, s.L, land, s.W, T, s); s._gang = r; } },

    slipway: { label: 'slipway', note: 'ribbed concrete ramp into the water; the tide decides how much of it launches', kind: 'slipway', faces: [],
      dims: { bays: [1, 1, 1], bayLen: [6, 20, 10], width: [2.5, 6, 3.6] },
      build(out, s, T) {
        const L = s.L, hw = s.W / 2, topZ = s.deckZ, toeZ = s.toeZ, x0 = -L / 2, dz = topZ - toeZ, zAt = (x) => topZ - dz * ((x - x0) / L), n = Math.max(8, Math.round(L / 0.9)), kerb = 0.26, kh = 0.22;
        CTX.ux = L / 2; at('deck', 0, 0);
        for (let i = 0; i < n; i++) { const xa = x0 + L * (i / n), xb = x0 + L * ((i + 1) / n), za = zAt(xa), zb = zAt(xb), m = matAtZ((za + zb) / 2, 'conc', T, s);
          out.push(F([[xa, -hw + kerb, za], [xb, -hw + kerb, zb], [xb, hw - kerb, zb], [xa, hw - kerb, za]], m, 0.05, 0, [[xa, -hw], [xb, -hw], [xb, hw], [xa, hw]], ribTex(0.30)));
          for (const sy of [-1, 1]) { const yi = sy * (hw - kerb), ye = sy * hw;
            out.push(F([[xa, yi, za + kh], [xb, yi, zb + kh], [xb, ye, zb + kh], [xa, ye, za + kh]], m, 0.30, 0, [[xa, 0], [xb, 0], [xb, kerb], [xa, kerb]], ribTex(0.9)));
            out.push(F(sy > 0 ? [[xa, yi, za], [xb, yi, zb], [xb, yi, zb + kh], [xa, yi, za + kh]] : [[xb, yi, zb], [xa, yi, za], [xa, yi, za + kh], [xb, yi, zb + kh]], m, -0.55));
            out.push(F(sy > 0 ? [[xa, ye, za + kh], [xb, ye, zb + kh], [xb, ye, zb - 0.46], [xa, ye, za - 0.46]] : [[xb, ye, zb + kh], [xa, ye, za + kh], [xa, ye, za - 0.46], [xb, ye, zb - 0.46]], matAtZ(zb - 0.2, 'conc', T, s), sy > 0 ? -0.15 : -0.7, 0, [[0, 0], [xb - xa, 0], [xb - xa, kh + 0.46], [0, kh + 0.46]], formTex())); }
          if (s.slipRails) for (const sy of [-1, 1]) { const y = sy * hw * 0.40, mw = matAtZ((za + zb) / 2, 'wood', T, s);
            out.push(F([[xa, y - 0.09, za + 0.13], [xb, y - 0.09, zb + 0.13], [xb, y + 0.09, zb + 0.13], [xa, y + 0.09, za + 0.13]], mw, 0.22, 0, [[xa, 0], [xb, 0], [xb, 0.18], [xa, 0.18]], grainTex(0.3)));
            out.push(F([[xb, y + 0.09, zb + 0.13], [xb, y + 0.09, zb], [xa, y + 0.09, za], [xa, y + 0.09, za + 0.13]], mw, -0.5)); } }
        at('face', 0, 0); box(out, x0 - 0.28, x0, -hw, hw, Math.max(s.bedMin, topZ - 1.4), topZ + 0.10, matAtZ(topZ, 'conc', T, s), -0.1, formTex(0.5));
      } },

    riprap: { label: 'armour-stone edge', note: 'graded rock: revetment, breakwater mound or rock-filled sheet-pile cell', kind: 'riprap', faces: [],
      dims: { bays: [1, 10, 4], bayLen: [2, 4, 3], width: [2.5, 8, 4] },
      build(out, s, T) {
        const L = s.L, hx = L / 2, crest = s.deckZ, toe = s.bedMin, hy = s.W / 2, rnd = mulberry32(s.variant * 4441 + 907), SM = s.stone === 'sandstone' ? 'sandst' : 'stone', cell = s.mound === 'sheetCell', bw = s.mound === 'breakwater';
        const zPlane = bw ? (y) => crest - (Math.abs(y) / hy) * (crest - toe) * 0.92 : (y) => crest - ((y + hy) / s.W) * (crest - toe);
        CTX.ux = hx; at('stone', 0, 0);
        if (cell) { const capZ = crest - 0.55; at('face', 0, 0);
          for (const [x0, y0, x1, y1, b] of [[-hx, hy, hx, hy, 0.05], [hx, -hy, -hx, -hy, -0.55], [hx, hy, hx, -hy, -0.15], [-hx, -hy, -hx, hy, -0.3]]) sheetWall(out, x0, y0, x1, y1, toe, capZ + 0.20, 'steel', T, s, b);
          at('deck', 0, 0); slab(out, [[-hx, -hy], [hx, -hy], [hx, hy], [-hx, hy]], capZ, matAtZ(capZ, 'conc', T, s), -0.15, formTex(0.6));
          at('stone', 0, 0); const gs = 0.52, gnx = Math.max(3, Math.round(L / gs)), gny = Math.max(3, Math.round(s.W / gs));
          for (let j = 0; j < gny; j++) for (let i = 0; i < gnx; i++) { const x = -hx + (i + 0.5) * (L / gnx) + (rnd() - 0.5) * gs * 0.9, y = -hy + (j + 0.5) * (s.W / gny) + (rnd() - 0.5) * gs * 0.8; if (x < -hx || x > hx) continue;
            const rr = 0.26 + Math.pow(rnd(), 1.5) * 0.36, heap = 1 - Math.abs(y) / (hy + 0.4); rubbleStone(out, x, y, capZ + 0.10 + heap * 0.55 + rnd() * 0.14, rr, matAtZ(capZ + 0.4, SM, T, s), rnd, 0); }
          return; }
        const steps = Math.max(6, Math.round(L / 0.9));
        for (let i = 0; i < steps; i++) { const xa = -hx + L * (i / steps), xb = -hx + L * ((i + 1) / steps), ya = -hy + 0.15, yb = hy - 0.15, m = matAtZ((zPlane(ya) + zPlane(yb)) / 2, SM, T, s);
          out.push(F([[xa, ya, zPlane(ya) - 0.16], [xb, ya, zPlane(ya) - 0.16], [xb, yb, zPlane(yb) - 0.16], [xa, yb, zPlane(yb) - 0.16]], m, -0.5, 0, [[xa, ya], [xb, ya], [xb, yb], [xa, yb]], rockTex())); }
        for (const g of [{ r: [0.14, 0.22], step: 0.30, lift: -0.05, b: -0.95 }, { r: [0.34, 0.58], step: 0.72, lift: 0.08, b: 0.0 }]) {
          const nx = Math.max(2, Math.round(L / g.step)), ny = Math.max(2, Math.round(s.W / g.step));
          for (let j = 0; j < ny; j++) for (let i = 0; i < nx; i++) { const x = -hx + (i + 0.5) * (L / nx) + (rnd() - 0.5) * g.step * 1.05, y = -hy + (j + 0.5) * (s.W / ny) + (rnd() - 0.5) * g.step * 0.85;
            if (x < -hx || x > hx || y < -hy || y > hy) continue; const rr = g.r[0] + Math.pow(rnd(), 1.6) * (g.r[1] - g.r[0]), zs = zPlane(y) + g.lift + rr * 0.30 + (rnd() - 0.5) * 0.16; if (zs > crest + 0.14) continue;
            const crown = rubbleStone(out, x, y, zs, rr, matAtZ(zs, SM, T, s), rnd, g.b); if (s.growth.weed && T.on.weed && Math.abs(zs - T.weedTop) < 0.28 && rnd() < 0.45) weedFringe(out, x, y, rr * 0.8, crown - rr * 0.3, s); } }
        if (!bw && s.crestBeam !== false) { at('coping', 0, 0); box(out, -hx, hx, -hy - 0.35, -hy + 0.20, crest - 0.42, crest, matAtZ(crest, 'conc', T, s), -0.05, formTex(0.5)); }
      } },

    dolphin: { label: 'dolphin', note: 'timber pile cluster, or steel piles under a cast cap: breasting (panel fender) or mooring (bollard) for the big hulls', kind: 'jetty', faces: [],
      dims: { bays: [1, 1, 1], bayLen: [1.2, 8, 3.6], width: [1.2, 8, 3.6] },
      build(out, s, T) {
        const W = s.W, hw = W / 2, top = s.deckZ;
        if (s.style === 'timber') { dolphinCluster(out, 0, 0, top + FIT.dolphin.above, T, s, W > 2.4 ? 7 : 3); s._holes = []; return; }
        const capT = 1.1, cz0 = top - capT, n = W >= 4.5 ? 3 : 2, pr = 0.30 + 0.04 * n; at('pile', 0, 0);
        for (let i = 0; i < n; i++) for (let j = 0; j < n; j++) { const x = -hw + 0.55 + (W - 1.1) * (i / (n - 1)), y = -hw + 0.55 + (W - 1.1) * (j / (n - 1)); bandedPile(out, x, y, pr, s.bedAt(x, y), cz0 + 0.1, s.age > 0.55 ? 'rust' : 'steel', T, s, 12, null); }
        at('cap', 0, 0); for (const [x0, y0, x1, y1, b] of [[-hw, hw, hw, hw, 0.05], [hw, -hw, -hw, -hw, -0.55], [hw, hw, hw, -hw, -0.15], [-hw, -hw, -hw, hw, -0.3]]) bandedWall(out, x0, y0, x1, y1, cz0, top, 'conc', T, s, b, formTex(0.5));
        at('deck', 0, 0); slab(out, [[-hw, -hw], [hw, -hw], [hw, hw], [-hw, hw]], top, 'conc', 0.06, slabTex(W, W));
        at('face', 0, 0); decalY(out, hw, 1, -hw + 0.1, hw - 0.1, top - 0.34, top - 0.08, 'red', 0.1, null, 0.03);
                const R = s.open || {}; at('rail', 0, 1);
        for (const [nm, x0, y0, x1, y1] of [['shore', -hw + 0.1, -hw + 0.1, hw - 0.1, -hw + 0.1], ['nx', -hw + 0.1, -hw + 0.1, -hw + 0.1, hw - 0.1], ['px', hw - 0.1, -hw + 0.1, hw - 0.1, hw - 0.1]])
          if (!(R[nm] && R[nm].length)) railRun(out, x0, y0, x1, y1, top, 'pipe');
        s._holes = [];
      } },

    catwalk: { label: 'catwalk', note: 'steel truss walkway on grating, rails 1.0 / 0.5 m — spans dolphin to dolphin without piles', kind: 'catwalk', faces: [],
      dims: { bays: [1, 1, 1], bayLen: [4, 30, 12], width: [1.0, 1.6, 1.2] },
      build(out, s, T) {
        const L = s.L, hx = L / 2, hw = s.W / 2, z = s.deckZ, depth = 0.9; CTX.ux = hx;
        at('deck', 0, 0); slab(out, [[-hx, -hw], [hx, -hw], [hx, hw], [-hx, hw]], z, 'galv', 0.05, gratingTex());
        for (const sy of [-1, 1]) { const y = sy * hw; at('truss', 0, 1);
          tube(out, [-hx, y, z + 1.0], [hx, y, z + 1.0], 0.03, 8, 'galv', 0.2, null, true); tube(out, [-hx, y, z + 0.5], [hx, y, z + 0.5], 0.022, 6, 'galv', 0.1, null, true);
          at('truss', 0, 0); tube(out, [-hx, y, z - depth], [hx, y, z - depth], 0.045, 6, 'galv', -0.6, null, true); tube(out, [-hx, y, z - 0.03], [hx, y, z - 0.03], 0.04, 6, 'galv', -0.4, null, true);
          const nb = Math.max(2, Math.round(L / 2)); for (let i = 0; i <= nb; i++) { const x = -hx + L * (i / nb); at('truss', 0, 1); pipe(out, x, y, 0.028, z - depth, z + 1.0, 'galv', -0.2, 6);
            if (i < nb) { at('truss', 0, 0); const xb = -hx + L * ((i + 1) / nb); tube(out, [x, y, i % 2 ? z - depth : z - 0.03], [xb, y, i % 2 ? z - 0.03 : z - depth], 0.025, 5, 'galv', -0.9, null, false); } }
          at('truss', 0, 0); decalY(out, y, sy, -hx, hx, z, z + 0.10, 'galv', -0.2, null, 0.02); }
      } },
  };

  // floats and fingers: waterline at z = 0 in their own frame, the group rides it; guide piles and the anchor are fixed
  function floatBody(out, s, T, g) {
    const L = s.L, hx = L / 2, hy = s.W / 2, top = s.freeboard, FT = floatFrame(T), hull = s.hull; CTX.ux = hx; at('float', g, 0);
    if (hull === 'plastic') {
      const cz0 = top - 0.40;
      for (const zz of zSplit(cz0, top, 'hdpe', FT, s)) for (const [x0, y0, x1, y1, b] of [[-hx, hy, hx, hy, 0.05], [hx, -hy, -hx, -hy, -0.5], [hx, hy, hx, -hy, -0.15], [-hx, -hy, -hx, hy, -0.3]]) wall(out, x0, y0, x1, y1, zz.a, zz.b, zz.mat, cubeTex(0.5), b);
      slab(out, [[-hx, -hy], [hx, -hy], [hx, hy], [-hx, hy]], cz0, 'hdpe', -0.95);
      at('deck', g, 0); slab(out, [[-hx, -hy], [hx, -hy], [hx, hy], [-hx, hy]], top, 'hdpeDeck', 0.12, cubeTex(0.5));
      for (let x = -hx + 0.5; x < hx - 0.01; x += 0.5) for (const y of [-hy + 0.25, hy - 0.25]) pipe(out, x, y, 0.045, top, top + 0.035, 'hdpeDeck', 0.3, 6);
    } else if (hull === 'concrete') {
      const z0 = -0.55;
      for (const zz of zSplit(z0, top - 0.02, 'conc', FT, s)) for (const [x0, y0, x1, y1, b] of [[-hx, hy, hx, hy, 0.05], [hx, -hy, -hx, -hy, -0.5], [hx, hy, hx, -hy, -0.15], [-hx, -hy, -hx, hy, -0.3]]) wall(out, x0, y0, x1, y1, zz.a, zz.b, zz.mat, formTex(0.4), b);
      at('deck', g, 0); slab(out, [[-hx, -hy], [hx, -hy], [hx, hy], [-hx, hy]], top, 'conc', 0.10, slabTex(s.bayLen, 99));
      at('float', g, 0); for (const sy of [-1, 1]) box(out, -hx, hx, sy > 0 ? hy : -hy - 0.14, sy > 0 ? hy + 0.14 : -hy, top - 0.30, top - 0.04, 'wood', 0.1, sawnTex(), false);
    } else {
      const fz1 = top - 0.05, fz0 = fz1 - 0.26;
      box(out, -hx, hx, hy - 0.10, hy, fz0, fz1, 'wood', 0.05, sawnTex(), true); box(out, -hx, hx, -hy, -hy + 0.10, fz0, fz1, 'wood', -0.4, sawnTex(), true);
      box(out, -hx, -hx + 0.10, -hy, hy, fz0, fz1, 'wood', -0.2, sawnTex(), true); box(out, hx - 0.10, hx, -hy, hy, fz0, fz1, 'wood', 0.0, sawnTex(), true);
      const nj = Math.max(2, Math.round(L / 0.8)); for (let i = 1; i < nj; i++) { const x = -hx + L * (i / nj); box(out, x - 0.045, x + 0.045, -hy + 0.1, hy - 0.1, fz0 + 0.04, fz1, 'wood', -0.5, null, true); }
      if (hull === 'drum') { const n = Math.max(1, Math.round(L / 1.0)), zc = fz0 - 0.24;
        for (let i = 0; i < n; i++) for (const yr of [-hy * 0.5, hy * 0.5]) { const xa = -hx + (L / n) * i + 0.07; for (const zz of zSplit(zc - 0.29, zc + 0.29, 'poly', FT, s)) tube(out, [xa, yr, zc], [xa + L / n - 0.14, yr, zc], 0.29, 10, zz.mat, -0.1, null, true); }
      } else { const nb = Math.max(2, Math.round(L / 2.4)), bw = Math.min(2.1, L / nb - 0.22), bd = Math.min(0.62, s.W * 0.55), bh = 0.42;
        for (let i = 0; i < nb; i++) { const cx = -hx + (L / nb) * (i + 0.5), z1 = fz0 + 0.02, z0 = z1 - bh;
          for (const zz of zSplit(z0, z1, 'poly', FT, s)) box(out, cx - bw / 2, cx + bw / 2, -bd / 2, bd / 2, zz.a, zz.b, zz.mat, -0.15, null, true);
          slab(out, [[cx - bw / 2, -bd / 2], [cx + bw / 2, -bd / 2], [cx + bw / 2, bd / 2], [cx - bw / 2, bd / 2]], z0, matAtZ(z0, 'poly', FT, s), -0.9); } }
      at('deck', g, 0); slab(out, [[-hx, -hy], [hx, -hy], [hx, hy], [-hx, hy]], top, 'plank', 0.12, plankTex(0.20));
      at('float', g, 0); for (const sy of [-1, 1]) box(out, -hx, hx, sy > 0 ? hy : -hy - 0.06, sy > 0 ? hy + 0.06 : -hy, top - 0.20, top - 0.05, 'rubber', 0.05, null, false);   // rub strip: the boats' contact band
    }
    if (s.curb === 'yellow') { at('deck', g, 0); for (const sy of [-1, 1]) decalZ(out, top + 0.003, -hx + 0.02, hx - 0.02, sy > 0 ? hy - 0.10 : -hy + 0.02, sy > 0 ? hy - 0.02 : -hy + 0.10, 'yel', 0.1, null, 0.02); }
    if (s.landing != null) { at('deck', g, 0); const lx = s.landing; decalZ(out, top + 0.006, lx - 0.3, lx + 1.3, -0.7, 0.7, 'galv', 0.0, rustTex(), 0.03); }
    if (s.swimLadder && s.family === 'float') { const xL = s.ends.px !== 'join' ? hx - 0.8 : -hx + 0.8; ladderAt(out, xL, hy, 1, top, FT, s, -0.85); s._swimX = xL; }
    // FIXED: guide piles (datum frame) — the hoops ride the float
    s._guides = [];
    const gf = s.guideFace || (s.family === 'finger' ? 'end' : 'shore');
    if (s.guidePiles && gf !== 'none') {
      const pts = gf === 'end' ? [[hx + 0.34, 0]] : (s.bays > 1 ? [-hx + 0.6, hx - 0.6] : [0]).map(x => [x, gf === 'water' ? hy + 0.34 : -hy - 0.34]);
      for (const [gx, gy] of pts) { at('guide', 0, 0); bandedPile(out, gx, gy, s.pileR, s.bedAt(gx, gy), T.hhw + s.guideAbove, 'pole', T, s, 10, s.capIron ? 'iron' : null);
        at('hoop', g, 0); torusZ(out, gx, gy, top + 0.22, s.pileR + 0.075, 0.045, 12, 5, 'galv', 0.25);
        const ey = gf === 'end' ? 0 : (gy > 0 ? hy : -hy), ex = gf === 'end' ? hx : gx;
        if (gf === 'end') box(out, hx - 0.02, gx - s.pileR - 0.04, -0.05, 0.05, top + 0.16, top + 0.28, 'galv', 0.1, null, true);
        else box(out, gx - 0.05, gx + 0.05, Math.min(ey, gy + (gy > 0 ? -s.pileR - 0.04 : s.pileR + 0.04)), Math.max(ey, gy + (gy > 0 ? -s.pileR - 0.04 : s.pileR + 0.04)), top + 0.16, top + 0.28, 'galv', 0.1, null, true);
        s._guides.push({ x: gx, y: gy, r: s.pileR, top: T.hhw + s.guideAbove, hoopZ: top + 0.22, ex }); } }
    if (s.chain && s.family === 'float') { at('anchor', 0, 0); const ax = -hx - 0.9, ay = -hy - 1.1, bz = s.bedAt(ax, ay);
      box(out, ax - 0.34, ax + 0.34, ay - 0.30, ay + 0.30, bz, bz + 0.34, matAtZ(bz + 0.17, 'conc', T, s), -0.3, formTex(0.4)); s._chain = { anchor: [ax, ay, bz + 0.34], attach: [-hx + 0.14, -hy + 0.06, top - 0.30] }; }
    at('float', g, 0);
  }

  // ============================ PRESETS (one module each; harbours are in wharfRig2.js) =====================
  const PRESETS = {
    lowPier: { family: 'pier', clearance: 0.6, bays: 4, bayLen: 2.6, width: 3.0, brace: false, capIron: false },
    tallPier: { family: 'pier', bays: 5, bayLen: 2.8, width: 4.2 },
    sheetedPier: { family: 'pier', struct: 'sheeted', bays: 5, width: 4.6 },
    steelPier: { family: 'pier', struct: 'steelPile', deck: 'concrete', bays: 5, width: 6.0, curb: 'yellow', rail: 'pipe' },
    concreteQuay: { family: 'quay', face: 'concrete', bays: 6, bayLen: 3.0, width: 8 },
    torbayQuay: { family: 'quay', face: 'steelSheet', curb: 'yellow', bays: 6, bayLen: 3.0, width: 7 },
    timberQuay: { family: 'quay', face: 'timberSheet', bays: 5, bayLen: 3.0, width: 6.5 },
    blockQuay: { family: 'quay', face: 'block', bays: 5, bayLen: 3.0, width: 8, curb: 'none' },
    logCrib: { family: 'crib', cap: 'plank' }, cappedCrib: { family: 'crib', cap: 'concrete', curb: 'yellow' },
    timberFloat: { family: 'float', hull: 'timber' }, plasticFloat: { family: 'float', hull: 'plastic', width: 2.5, curb: 'yellow' },
    concreteFloat: { family: 'float', hull: 'concrete', width: 3.0, bays: 4 }, drumFloat: { family: 'float', hull: 'drum', bays: 2 },
    finger: { family: 'finger', bays: 2 },
    gangway: { family: 'gangway' }, slipway: { family: 'slipway' },
    graniteEdge: { family: 'riprap', stone: 'granite', mound: 'revetment' }, redEdge: { family: 'riprap', stone: 'sandstone', mound: 'revetment' },
    breakwater: { family: 'riprap', stone: 'granite', mound: 'breakwater', width: 6, bays: 6 }, sheetCell: { family: 'riprap', mound: 'sheetCell', width: 5, bays: 5 },
    timberDolphin: { family: 'dolphin', style: 'timber', width: 1.4 }, breastingDolphin: { family: 'dolphin', style: 'steel', role: 'breasting', width: 5.0 },
    mooringDolphin: { family: 'dolphin', style: 'steel', role: 'mooring', width: 3.6 }, catwalk: { family: 'catwalk', run: 14 },
  };
  const DEFAULTS = {
    variant: 0, season: 'summer', stage: 'weathered', tideRange: 1.8, tide: null, bed: -1.6, clearance: 1.0, deckZ: null,
    pileR: 0.16, brace: true, capIron: true, curb: 'wood', rail: 'none', railSides: ['shore', 'ends'], guidePiles: true, guideAbove: 1.6, chain: true,
    slipRails: true, run: null, toeZ: null, freeboard: null, fittings: null, growth: null, berths: 'auto', swimLadder: true, landing: null,
    face: 'concrete', struct: 'open', deck: 'plank', cap: 'plank', hull: 'timber', stone: 'granite', mound: 'revetment', style: 'steel', role: 'breasting',
    ends: null, open: null, faces: null, landZ: null, grp: null,
  };
  const FIT_DEFAULT = {
    quay: { ladder: 'auto', fender: 'auto', cleat: 'auto', bollard: 'auto', ring: 'auto' }, pier: { ladder: 'auto', fender: 'auto', cleat: 'auto', bollard: 'auto', ring: 'auto' },
    crib: { ladder: 'auto', fender: 'auto', cleat: 'auto', bollard: 'auto', ring: 'auto' }, float: { ladder: 0, fender: 0, cleat: 'auto', bollard: 0, ring: 0 },
    finger: { ladder: 0, fender: 0, cleat: 'auto', bollard: 0, ring: 0 }, gangway: {}, slipway: { cleat: 1, ring: 1 }, riprap: {}, dolphin: {}, catwalk: {},
  };
  const clampF = (v, a, b) => Math.max(a, Math.min(b, +v || 0)), clampI = (v, a, b) => Math.max(a, Math.min(b, Math.round(+v || 0)));
  function resolve(key, opts) {
    let o = opts || {};
    if (PRESETS[key]) { o = Object.assign({}, PRESETS[key], o); key = PRESETS[key].family; }
    const family = FAMILIES[key] ? key : 'pier', Fm = FAMILIES[family], D = Fm.dims, s = Object.assign({}, DEFAULTS, o);
    s.family = family; s.kind = Fm.kind; s.label = Fm.label;
    s.variant = clampI(s.variant, 0, VARIANTS - 1); s.season = SEASONS.indexOf(s.season) >= 0 ? s.season : 'summer';
    if (typeof o.age === 'number') { s.age = clamp(o.age, 0, 1); s.stage = s.age < 0.2 ? 'new' : s.age < 0.5 ? 'seasoned' : s.age < 0.8 ? 'weathered' : 'derelict'; }
    else { s.stage = STAGES[s.stage] != null ? s.stage : 'weathered'; s.age = STAGES[s.stage]; }
    s.bays = clampI(s.bays != null ? s.bays : D.bays[2], D.bays[0], D.bays[1]);
    s.bayLen = +clampF(s.bayLen != null ? s.bayLen : D.bayLen[2], D.bayLen[0], D.bayLen[1]).toFixed(2);
    s.W = +clampF(o.width != null ? o.width : D.width[2], D.width[0], D.width[1]).toFixed(2);
    if (family === 'float' && s.hull === 'plastic') s.W = Math.max(1.5, Math.round(s.W / 0.5) * 0.5);
    const runF = { gangway: 1, slipway: 1, catwalk: 1 }[family];
    if (runF) { s.run = clampF(s.run != null ? s.run : D.bayLen[2], D.bayLen[0], D.bayLen[1]); s.L = s.run; s.bays = 1; s.bayLen = s.L; }
    else if (family === 'dolphin') { s.L = s.W; s.bays = 1; s.bayLen = s.W; }
    else s.L = +(s.bays * s.bayLen).toFixed(2);
    s.tideRange = clampF(s.tideRange, 0.3, 14); s.tide = o.tide != null ? +o.tide : s.tideRange * 0.55;
    s.growth = Object.assign({ stain: true, barn: true, weed: true, ice: true }, o.growth || {});
    s.fittings = Object.assign({}, FIT_DEFAULT[family], o.fittings || {});
    s.ends = Object.assign({ nx: 'free', px: 'free' }, o.ends || {});
    s.open = Object.assign({ water: [], shore: [], nx: [], px: [] }, o.open || {});
    s.faces = o.faces || Fm.faces.slice();
    const bedF = typeof s.bed === 'function' ? s.bed : null, bedN = bedF ? 0 : Math.min(+s.bed, -0.2);
    s.bedAt = bedF ? (x, y) => bedF(x, y) : () => bedN;
    { let m = 1e9; const hx = s.L / 2, hy = s.W / 2; for (let i = 0; i <= 8; i++) for (let j = 0; j <= 4; j++) m = Math.min(m, s.bedAt(-hx + s.L * i / 8, -hy + s.W * j / 4)); s.bedMin = m; }
    s.floating = !!FLOATING[family];
    if (s.floating) s.freeboard = clampF(s.freeboard != null ? s.freeboard : ({ plastic: 0.32, concrete: 0.50 }[s.hull] || (family === 'finger' ? 0.40 : 0.45)), 0.25, 0.8);
    const auto = Math.round((s.tideRange + s.clearance) / 0.05) * 0.05;
    if (family === 'gangway') { s.deckZ = o.deckZ != null ? +o.deckZ : auto; s.landZ = o.landZ != null ? +o.landZ : s.tide + 0.45; s.W = +clampF(o.width != null ? o.width : FIT.gang.clear + 0.14, 1.1, 1.6).toFixed(2); }
    else if (family === 'slipway') { s.toeZ = s.toeZ != null ? +s.toeZ : Math.max(s.bedMin + 0.1, -0.8); s.deckZ = clampF(o.deckZ != null ? o.deckZ : s.tideRange + 0.4, s.toeZ + 0.6, s.toeZ + s.L * 0.30); }
    else if (family === 'riprap') s.deckZ = o.deckZ != null ? +o.deckZ : s.tideRange + 0.4;
    else if (!s.floating) s.deckZ = o.deckZ != null ? +o.deckZ : auto;
    else s.deckZ = s.freeboard;
    return s;
  }

  // ============================ FITTINGS FROM THE BERTHS =====================================================
  // A ladder belongs to a berth (mid-berth on a fixed face; floats carry a swim ladder), fenders by class at the
  // quarters, lines on cleats or bollards at the bow, stern and springs. Nothing lands on a ladder, a join or a
  // gangway: every item takes the nearest free spot within 1.5 m of where it wants to be, or is dropped.
  function placements(s, T) {
    const L = s.L, hx = L / 2, hy = s.W / 2, top = s.floating ? s.freeboard : s.deckZ, f = s.fittings;
    const P = { ladders: [], tyres: [], foams: [], panels: [], fenderPiles: [], cleats: [], bollards: [], rings: [], rails: [], berths: {}, gaps: { water: [], shore: [] }, top, hx, hy, L };
    // a steel dolphin's fittings are laid out here, so gameplay publishes them: breasting, its panel fenders and a bollard for
    // the spring and breast lines; mooring, one bollard for the head or stern line. A timber cluster is a fender, not a deck.
    if (s.family === 'dolphin') { if (s.style !== 'timber') { const hw = s.W / 2;
        if (s.role === 'breasting') { const np = s.W >= 4 ? 2 : 1; for (let k = 0; k < np; k++) P.panels.push({ x: np === 1 ? 0 : (k ? 1 : -1) * s.W * 0.24, y: hw, side: 1, face: 'water', berth: null });
          P.bollards.push({ x: 0, y: +(-hw + 0.9).toFixed(2), z: top, side: 1, face: 'water', berth: null, lead: 'breast' }); }
        else P.bollards.push({ x: 0, y: 0.1, z: top, side: 1, face: 'water', berth: null, lead: 'head' }); }
      return P; }
    if ({ gangway: 1, riprap: 1, catwalk: 1 }[s.family]) return P;
    const depth = -s.bedMin, kind = s.kind === 'finger' ? 'finger' : s.kind;
    for (const face of s.faces) {
      const side = face === 'water' ? 1 : -1, yF = side * hy, open = s.open[face] || [], taken = open.map(o => [o[0] - 0.3, o[1] + 0.3]);
      const free = (x, r) => x - r > -hx + 0.25 && x + r < hx - 0.25 && !taken.some(t => x + r > t[0] && x - r < t[1]);
      const put = (x, r) => { for (let d = 0; d <= 1.5; d += 0.1) for (const sg of [1, -1]) { const xx = x + sg * d; if (free(xx, r)) { taken.push([xx - r, xx + r]); return +xx.toFixed(2); } } return null; };
      let bp = s.berthPlanned && s.berthPlanned[face];
      if (!bp) {
        // berths only where the water is: the berth line 1.5 m off the face must sit at least 1.0 m under highest water; the depth
        // a segment offers is the shallowest bed under its hulls (1.5 and 3.5 m out), so a drying berth takes hulls that can dry
        const off1 = side * (hy + 1.5), off2 = side * (hy + 3.5), dry = []; let a = null;
        for (let x = -hx; x <= hx + 1e-6; x += 0.25) { const d = s.bedAt(x, off1) > s.tideRange - 1.0; if (d && a == null) a = x - 0.125; if (!d && a != null) { dry.push([a, x - 0.125]); a = null; } }
        if (a != null) dry.push([a, hx + 0.1]);
        // a face over a sloping bed is cut where the depth crosses what each class needs afloat at datum (0.56 · 1.30 · 2.00 ·
        // 4.70 m), pieces under 3 m going to their shallower neighbour, so the big hulls take the deep end and the dinghies the rest
        const bedL = (x) => Math.max(s.bedAt(x, side * (hy + 0.5)), s.bedAt(x, off1), s.bedAt(x, off2)), band = (x) => [0.56, 1.3, 2.0, 4.7].filter(t => -bedL(x) >= t).length;
        const runs = cutRuns(-hx, hx, open.map(q => [q[0] - 0.4, q[1] + 0.4]).concat(dry)).filter(r => r[1] - r[0] >= 3), segs = [];
        for (const r of runs) { const parts = []; let a0 = r[0], bd = band(r[0]);
          for (let x = r[0] + 0.25; x < r[1] - 1e-6; x += 0.25) { const b2 = band(x); if (b2 !== bd) { parts.push({ r: [a0, x], bd }); a0 = x; bd = b2; } } parts.push({ r: [a0, r[1]], bd });
          while (parts.length > 1) { let k = -1; parts.forEach((p, i) => { if (p.r[1] - p.r[0] < 3 && (k < 0 || p.r[1] - p.r[0] < parts[k].r[1] - parts[k].r[0])) k = i; }); if (k < 0) break;
            const Lp = parts[k - 1], Rp = parts[k + 1], j = !Lp ? k + 1 : !Rp ? k - 1 : (Lp.bd <= Rp.bd ? k - 1 : k + 1), lo = Math.min(k, j);
            parts.splice(lo, 2, { r: [parts[lo].r[0], parts[lo + 1].r[1]], bd: Math.min(parts[lo].bd, parts[lo + 1].bd) }); }
          for (const p of parts) segs.push(p.r); }
        const list = []; let used = 0, tot = 0;
        for (const r of segs) { let mb = -1e9; for (let x = r[0]; x <= r[1] + 1e-6; x += 0.5) mb = Math.max(mb, bedL(x));
          const b = berthPlan(r[0], r[1], kind, s.berths, Math.min(depth, -mb), s.tideRange); for (const e of b.list) { if (!e.fits) continue; e.id = 'berth' + list.length; list.push(e); } used += b.utilisation * b.faceLen; tot += b.faceLen; }
        bp = { list, faceLen: +(2 * hx).toFixed(2), kind, utilisation: tot ? +(used / tot).toFixed(2) : 0, segments: segs.map(r => r.map(v => +v.toFixed(2))), dry: dry.map(r => r.map(v => +v.toFixed(2))) }; }
      P.berths[face] = bp;
      if (!s.floating && f.ladder !== 0) for (const b of bp.list) { const x = put(b.x, FIT.ladder.gap / 2); if (x != null) { P.ladders.push({ x, y: yF, side, face, berth: b.id }); P.gaps[face].push([x - FIT.ladder.gap / 2, x + FIT.ladder.gap / 2]); } }
      for (const o of open) P.gaps[face].push(o);
      for (const b of bp.list) {
        if (!s.floating && f.fender !== 0) for (const fx of b.fenderX) { const r = b.cls === 'D' ? FIT.panel.w / 2 + 0.1 : 0.55, x = put(fx, r); if (x == null) continue;
          const kindF = s.family === 'quay' && b.cls === 'D' ? 'panel' : b.cls === 'A' ? 'foam' : 'tyre';
          (kindF === 'panel' ? P.panels : kindF === 'foam' ? P.foams : P.tyres).push({ x, y: yF, side, face, berth: b.id }); }
        const big = b.cls === 'C' || b.cls === 'D';
        for (let k = 0; k < b.lines.length; k++) { const lx = b.lines[k], useB = big && !s.floating && (k === 0 || k === b.lines.length - 1);
          const x = put(lx, useB ? 0.35 : 0.32); if (x == null) continue;
          (useB ? P.bollards : P.cleats).push({ x, y: yF - side * (useB ? FIT.bollard.set : FIT.cleat.set), z: top, side, face, berth: b.id, lead: k === 0 ? 'bow' : k === b.lines.length - 1 ? 'stern' : 'spring' }); }
        if (b.cls === 'A' && !s.floating && f.ring !== 0) { const x = put(b.x + 1.1, 0.3); if (x != null) P.rings.push({ x, y: yF - side * FIT.ring.set, z: top, side, face, berth: b.id }); }
      }
      if (s.family === 'quay' && !bp.list.some(b => b.cls === 'D')) for (let i = 0; i <= s.bays; i++) { const x = -hx + i * s.bayLen; if (!taken.some(t => x > t[0] - 0.2 && x < t[1] + 0.2) && Math.abs(x) < hx - 0.1) P.fenderPiles.push({ x, y: yF + side * 0.16, side, face }); }
    }
    if (s.rail && s.rail !== 'none') {
      const R = s.railSides || [], seg = (x0, y0, x1, y1, gaps) => { for (const [a, b] of cutRuns(Math.min(x0, x1), Math.max(x0, x1), gaps)) if (b - a > 0.4) P.rails.push([a, y0, b, y1]); };
      if (R.indexOf('shore') >= 0 && s.faces.indexOf('shore') < 0) seg(-hx + 0.1, -hy + 0.08, hx - 0.1, -hy + 0.08, s.open.shore);
      if (R.indexOf('water') >= 0 && s.faces.indexOf('water') < 0) seg(-hx + 0.1, hy - 0.08, hx - 0.1, hy - 0.08, s.open.water);
      if (R.indexOf('ends') >= 0) { if (s.ends.nx !== 'join') P.rails.push([-hx + 0.08, -hy + 0.1, -hx + 0.08, hy - 0.1]); if (s.ends.px !== 'join') P.rails.push([hx - 0.08, -hy + 0.1, hx - 0.08, hy - 0.1]); }
    }
    return P;
  }
  function addFittings(out, s, T) {
    const P = placements(s, T), FT = s.floating ? floatFrame(T) : T, g = s.floating ? (s.grp || 1) : 0, top = P.top;
    CTX.grp = g; P.tyreRecs = [];
    for (const l of P.ladders) ladderAt(out, l.x, l.y, l.side, top, FT, s);
    for (const t of P.tyres) { CTX.grp = s.floating ? g : 0; P.tyreRecs.push(tyreAt(out, t.x, t.y, t.side, top, s.fenderZ === 'water' ? Math.min(top - 0.28, T.w + 0.75) : top - 0.28, FT, s)); }
    CTX.grp = g;
    for (const t of P.foams) foamAt(out, t.x, t.y, t.side, top - 0.20, FT, s);
    for (const t of P.panels) panelAt(out, t.x, t.y, t.side, top, FT, s);
    for (const p of P.fenderPiles) { at('fender', g, 0); bandedPile(out, p.x, p.y, 0.14, Math.max(s.bedAt(p.x, p.y), -0.9), top - 0.30, 'pole', FT, s, 9); }
    for (const c of P.cleats) cleatAt(out, c.x, c.y, top, 0);
    for (const b of P.bollards) bollardAt(out, b.x, b.y, top);
    for (const r of P.rings) ringAt(out, r.x, r.y, top);
    for (const r of P.rails) { if (s.age > 0.8 && G.hash2(Math.round(r[0] * 10), s.variant) < 0.35) continue; railRun(out, r[0], r[1], r[2], r[3], top, s.rail); }
    if (!s.floating && s.curb !== 'none' && { pier: 1, crib: 1, quay: 1 }[s.family]) {
      const mat = s.curb === 'yellow' ? 'yel' : 'wood', hx = P.hx, hy = P.hy;
      for (const face of s.faces) { const sy = face === 'water' ? 1 : -1; bullRail(out, -hx + (s.ends.nx === 'join' ? 0 : 0.05), hx - (s.ends.px === 'join' ? 0 : 0.05), sy * (hy - FIT.bull.w / 2 - 0.02), top, mat, P.gaps[face]); } }
    if (G.addFixtures) P.fixtures = G.addFixtures(out, s, T, P, g, top);
    CTX.grp = 0;
    return P;
  }
  // module sockets: two ends at deck height, and one per bay boundary on each side (for a T-join, a gangway or a finger)
  function sockets(s) {
    const hx = s.L / 2, hy = s.W / 2, z = s.floating ? s.freeboard : s.deckZ, t = s.floating ? 'float' : s.family === 'gangway' ? 'ramp' : 'fixed', out = [];
    if (s.family === 'gangway') return [{ id: 'hinge', at: [-hx, 0, s.deckZ], n: [-1, 0, 0], w: s.W, type: 'fixed' }, { id: 'toe', at: [hx, 0, s.landZ], n: [1, 0, 0], w: s.W, type: 'float' }];
    out.push({ id: 'nx', at: [-hx, 0, z], n: [-1, 0, 0], w: s.W, type: t, joined: s.ends.nx === 'join' }, { id: 'px', at: [hx, 0, z], n: [1, 0, 0], w: s.W, type: t, joined: s.ends.px === 'join' });
    const nb = s.family === 'dolphin' ? 1 : s.bays;
    for (let i = 0; i <= nb; i++) { const x = -hx + i * (s.L / nb); out.push({ id: 'water' + i, at: [x, hy, z], n: [0, 1, 0], w: +(s.L / nb).toFixed(2), type: t, face: 'water' }, { id: 'shore' + i, at: [x, -hy, z], n: [0, -1, 0], w: +(s.L / nb).toFixed(2), type: t, face: 'shore' }); }
    return out.map(k => Object.assign(k, { at: k.at.map(v => +v.toFixed(3)) }));
  }

  Object.assign(G, { STAGES, STAGE_KEYS, SEASONS, VARIANTS, FLOATING, floatFrame, rampBody, xform, gangwayFaces, gangLength, chainFaces, FAMILIES, PRESETS, DEFAULTS, FIT_DEFAULT, resolve, placements, addFittings, sockets });
})(typeof globalThis !== 'undefined' ? globalThis : window);
