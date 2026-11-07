/* Hidden Harbours — MARINA RIG · THE KIT (globalThis.MarinaKit). Load after the wharf pass 2 files, marinaRig.js, marinaBoats.js
   and the hull rigs. Three marinas built with pass 2's define() / snap() / gangwayFrom(), their boats in their berths, and the
   gameplay sidecar: pass 2's sections, then what a marina adds.

     harbour:basin      a concrete quay with its gate, lamps and waste station; a gangway down to a 27 m main walkway on the dock
                        variant asked for (timber · cube · composite · concrete · drum); the office snapped to it; nine slips on
                        fingers, bow in; a concrete T-head with a sport fisher alongside, the yacht pedestal and the pump-out
     harbour:yachtQuay  the quay for the big hulls: a 32 m, 4 m wide concrete pontoon off the quay, the 90′ and 53′ sport fishers,
                        the sloop 88 and a go-fast moored stern-to (Mediterranean), bow lines down to the ground chain, a
                        63 / 125 A pedestal and two stern bollards each
     harbour:venue      the floating venue off its quay: bar, restaurant, stage and dance floor; two boats in for dinner

   Every hull in a berth has an outlet and a tap on its own dock, the cable and hose drawn from the pedestal to its inlet
   (UTILITIES); every gangway has a security gate at its head (GATES); every lamp is published with what it lights (LIGHTS).  */
(function (root) {
  'use strict';
  const G = root.WharfGeo2, K = root.WharfRig2Kit, W = root.WharfRig2, V = root.WharfVerbs2, MG = root.MarinaGeo, MB = root.MarinaBoats;
  if (!G || !K || !W || !V || !MG || !MB) throw new Error('marinaKit.js: load the wharf pass 2 files, marinaRig.js and marinaBoats.js first');
  if (root.MarinaKit) return;
  const PI = Math.PI, PI2 = PI / 2, r2 = (v) => Math.round(v * 100) / 100, r3 = (v) => Math.round(v * 1000) / 1000, clamp = G.clamp;
  const TR = 1.2, DECK = 2.2, WANT = V.REACH.want, MARINAS = {}, PICK = ['variant', 'season', 'stage', 'age', 'tideRange', 'tide', 'curb', 'rail', 'hull', 'growth'];
  const rot2 = (v, a) => [v[0] * Math.cos(a) - v[1] * Math.sin(a), v[0] * Math.sin(a) + v[1] * Math.cos(a)];
  const toH = (m, x, y) => { const q = rot2([x, y], m.rot || 0); return [m.at[0] + q[0], m.at[1] + q[1]]; };

  // ============================ PIECES ====================================================================================
  function quay(bays, items, open) { const Wd = 4.0; return { id: 'quay', key: 'quay', at: [0, 1.0 - Wd / 2], rot: 0,
    o: { face: 'concrete', bays, bayLen: 3.0, width: Wd, faces: [], curb: 'none', rail: 'pipe', railSides: ['water'], open: { water: open }, fixtures: [], deckZ: DECK, tideRange: TR, marina: { items } } }; }
  const gateAt = (x) => ({ kind: 'gate', id: 'gate', x, y: 1.35, rot: 0 }), post = (x) => ({ kind: 'lampPost', x, y: 1.2, rot: 0 });
  const waste = (x) => [{ kind: 'binScreen', x: x + 1.8, y: 0.9, rot: 0, w: 4.9 }, { kind: 'binGeneral', id: 'binGeneral0', x, y: -0.35, rot: 0 }, { kind: 'binGeneral', id: 'binGeneral1', x: x + 0.7, y: -0.35, rot: 0 },
    { kind: 'binRecycle', id: 'binRecycle', x: x + 1.4, y: -0.35, rot: 0 }, { kind: 'bottleBank', id: 'bottleBank', x: x + 2.4, y: -0.35, rot: 0 }, { kind: 'oilTank', id: 'oilTank', x: x + 3.6, y: -0.35, rot: 0 }];
  const fingerLen = (loa) => clamp(Math.ceil(0.75 * loa / 2.4 - 1e-9) * 2.4, 4.8, 9.0);
  // a finger snapped to the walkway at walkway-local x, on its water (+1, west) or shore (−1, east) side
  function finger(walk, hx, side, x, len, id, marina) {
    const bl = walk.o.bayLen, i = clamp(Math.round((x + hx) / bl), 0, walk.o.bays), sl = r3(x - (-hx + i * bl)), bays = Math.max(1, Math.ceil(len / 3.0 - 1e-9)), bayLen = r2(len / bays);
    return K.snap(walk, (side > 0 ? 'water' : 'shore') + i, { id, key: 'finger', o: { hull: walk.o.hull, bays, bayLen, width: 1.2, faces: [], fixtures: [], guideFace: 'end', tideRange: TR, marina: marina || { items: [], cleats: [] } } }, 'nx', { slide: sl });
  }
  // a hull alongside a finger, bow in to the walkway, on the finger's north side; its pedestal at the finger root, cleats, a tip light
  function slip(fin, type, paint, id, walkHalf) {
    const B = MB.build(type, paint), L = fin.o.bays * fin.o.bayLen, east = Math.abs(fin.rot) < 0.1, hd = east ? PI : 0, side = east ? 1 : -1;
    const root0 = toH(fin, -L / 2, 0), beamSide = east ? -B.bbox.x0 : B.bbox.x1, cy = r3(root0[1] + 0.6 + 0.45 + beamSide), cx = r3(east ? walkHalf + 0.6 + B.bbox.y1 : -walkHalf - 0.6 - B.bbox.y1);
    const M = fin.o.marina, px = -L / 2 + 1.6, xb = r3(Math.min(L / 2 - 0.6, Math.max(-L / 2 + 1.2, L / 2 - 1.4)));
    M.items.push({ kind: 'pedestal2', id: 'pedestal', x: px, y: 0, rot: PI2, serves: [id] }, { kind: 'dockLight', x: r3(L / 2 - 0.25), y: 0, rot: 0 });
    for (const [x, lead] of [[-L / 2 + 0.55, 'bow'], [-L / 2 + L * 0.45, 'spring'], [L / 2 - 0.35, 'stern']]) M.cleats.push({ x: r3(x), y: side * 0.30, side, berth: id, lead });
    return { id, type, paint, at: [cx, cy], heading: hd, mooring: 'slip', dock: fin.id, boardAt: [xb, r3(side * 0.30)], feed: { module: fin.id, pedestal: 'pedestal', outlet: 0, tap: 0, mirror: !east } };
  }
  // extent from the modules and the hulls' footprints
  function extentOf(L, tideRange) { let x0 = 1e9, y0 = 1e9, x1 = -1e9, y1 = -1e9; const add = (X, Y) => { x0 = Math.min(x0, X); x1 = Math.max(x1, X); y0 = Math.min(y0, Y); y1 = Math.max(y1, Y); };
    for (const m of L.mods) { const s = G.resolve(m.key, Object.assign({ tideRange }, m.o)); for (const [x, y] of [[-s.L / 2, -s.W / 2], [s.L / 2, -s.W / 2], [s.L / 2, s.W / 2], [-s.L / 2, s.W / 2]]) { const q = toH(m, x, y); add(q[0], q[1]); } }
    for (const b of (L.boats || [])) { if (!MB.loaded(b.type)) continue; const B = MB.build(b.type, b.paint), c = Math.cos(b.heading), s = Math.sin(b.heading);
      for (const [x, y] of [[B.bbox.x0, B.bbox.y0], [B.bbox.x1, B.bbox.y0], [B.bbox.x1, B.bbox.y1], [B.bbox.x0, B.bbox.y1]]) add(b.at[0] + x * s + y * c, b.at[1] - x * c + y * s); }
    return [Math.floor(x0 - 2), Math.floor(y0 - 5), Math.ceil(x1 + 2), Math.ceil(y1 + 3)]; }
  function defineMarina(id, def) { MARINAS[id] = def; if (!def.extent) def.extent = extentOf(def.layout({ tideRange: def.tideRange }), def.tideRange); return K.define(id, def); }

  // ============================ THE BASIN =================================================================================
  const W_SLIPS = [['sloop30', 'atlantic-navy'], ['bowrider', 'harbour-white'], ['sloop30', 'oyster-bone'], ['goFast', 'corsa']];
  const E_SLIPS = [['bowrider', 'summer-navy'], ['goFast', 'hornet'], ['sloop30', 'gelcoat-white'], ['goFast', 'biscayne'], ['bowrider', 'cranberry']];
  function basinLayout(b) {
    const hull = b.hull || 'composite', fb = MG.FREEBOARD[hull] || 0.45, wide = 2.5, half = wide / 2, bays = 8, bayLen = 3.375, hx = bays * bayLen / 2;
    const q = quay(12, [gateAt(0), post(-14.5), post(-7), post(6.5), post(14.5), ...waste(-12.4), { kind: 'ringPost', id: 'ring', x: 3.4, y: 1.3, rot: 0 }], [[-1.65, 1.65]]);
    const gw = K.gangwayFrom(q, 'water6', { freeboard: fb, floatGrp: 1 });
    const lights = [-10.5, -4.5, 1.5, 7.5, 12.8].map((x, k) => ({ kind: 'dockLight', x, y: (k % 2 ? 1 : -1) * (half - 0.2), rot: 0 }));
    const walk = { id: 'walk', key: 'float', at: [0, r3(gw.edgeAt[1] + hx)], rot: PI2, grp: 1, o: { hull, bays, bayLen, width: wide, freeboard: fb, faces: [], fixtures: [], guideFace: 'none', swimLadder: true, chain: true,
      curb: hull === 'plastic' ? 'yellow' : 'wood', tideRange: TR, marina: { conduits: ['water', 'shore'], items: lights, cleats: [] } } };
    const oc = -8.9, oi = Math.round((oc + hx) / bayLen), osl = r3(oc - (-hx + oi * bayLen));
    const office = K.snap(walk, 'water' + oi, { id: 'office', key: 'floatingOffice', o: { hull, guideFace: 'none', tideRange: TR, faces: [], fixtures: [],
      marina: { items: [{ kind: 'ringPost', id: 'ring', x: 2.9, y: 1.62, rot: 0 }, { kind: 'dockLight', x: 3.35, y: 2.15, rot: 0 }, { kind: 'dockLight', x: 3.35, y: -2.15, rot: 0 }] } } }, 'nx', { slide: osl });
    const mods = [q, walk, office], boats = [];
    const run = (list, side, x0, tag) => { let x = x0, last = null;
      list.forEach(([t, p], k) => { if (!MB.loaded(t)) return; const B = MB.build(t, p), fin = finger(walk, hx, side, x, fingerLen(B.loa), 'finger' + tag + k); mods.push(fin);
        boats.push(slip(fin, t, p, 'slip' + tag + k, half)); x += 1.2 + (B.bbox.x1 - B.bbox.x0) + 0.9; last = B; });
      if (last && x < hx - 0.7) mods.push(finger(walk, hx, side, x, fingerLen(last.loa), 'finger' + tag + list.length, { items: [{ kind: 'dockLight', x: r3(fingerLen(last.loa) / 2 - 0.25), y: 0, rot: 0 }], cleats: [] })); };
    run(W_SLIPS, 1, -5.1, 'W'); run(E_SLIPS, -1, -10.7, 'E');
    const thM = { conduits: ['shore'], items: [{ kind: 'pedestalY', id: 'pedestal', x: 2.6, y: 0.75, rot: 0, serves: ['tee0'] }, { kind: 'pumpOut', id: 'pumpOut', x: -5.2, y: 0.4, rot: 0 },
      { kind: 'dockLight', x: -6.95, y: -1.3, rot: 0 }, { kind: 'dockLight', x: 6.95, y: -1.3, rot: 0 }, { kind: 'dockLight', x: -6.95, y: 1.3, rot: 0 }, { kind: 'ringPost', id: 'ring', x: 5.6, y: 0.85, rot: 0 },
      { kind: 'dockBox', id: 'box', x: -2.4, y: -0.55, rot: PI }], cleats: [[-6.3, 'stern'], [-2.2, 'spring'], [2.2, 'spring'], [6.3, 'bow']].map(([x, lead]) => ({ x, y: 1.2, side: 1, berth: 'tee0', lead })) };
    const thead = K.snap(walk, 'px', { id: 'thead', key: 'float', o: { hull: 'concrete', bays: 4, bayLen: 3.6, width: 3.0, faces: [], fixtures: [], guideFace: 'none', swimLadder: true, chain: true, tideRange: TR, marina: thM } }, 'shore2');
    mods.push(thead);
    if (MB.loaded('sportFisher')) { const B = MB.build('sportFisher', 'navy'), cy = r3(thead.at[1] + 1.5 + 0.15 + B.bbox.x1);
      boats.push({ id: 'tee0', type: 'sportFisher', paint: 'navy', at: [r3(thead.at[0] + 0.4), cy], heading: 0, mooring: 'alongside', dock: 'thead', boardAt: [-3.6, 1.15], feed: { module: 'thead', pedestal: 'pedestal', outlet: 1, tap: 1, mirror: true } }); }
    return { mods, gangs: [gw], boats };
  }

  // ============================ THE YACHT QUAY ============================================================================
  const YACHTS = [['skybridge', 'gelcoat'], ['sloop88', 'gelcoat-white'], ['sportFisher', 'gunmetal'], ['goFast', 'blaze']];
  function yachtLayout() {
    const fb = 0.55, q = quay(13, [gateAt(0), post(-16), post(-8), post(8), post(17.5), ...waste(10.4), { kind: 'ringPost', id: 'ring', x: -3.6, y: 1.3, rot: 0 }], [[-1.65, 1.65]]);
    const gw = K.gangwayFrom(q, 'water6', { slide: 1.5, freeboard: fb, floatGrp: 1 }), hy = 2.0;
    const items = [-13.8, -8, -3, 3, 8, 13.8].map(x => ({ kind: 'dockLight', x, y: -1.75, rot: 0 })).concat([{ kind: 'pumpOut', id: 'pumpOut', x: 11.6, y: -0.6, rot: PI }, { kind: 'dockBox', id: 'box0', x: -6.8, y: -0.8, rot: PI }, { kind: 'dockBox', id: 'box1', x: 5.2, y: -0.8, rot: PI }]);
    const pon = { id: 'pontoon', key: 'float', at: [0, r3(gw.edgeAt[1] + hy)], rot: 0, grp: 1, o: { hull: 'concrete', bays: 8, bayLen: 3.6, width: 4.0, freeboard: fb, faces: [], fixtures: [], guideFace: 'shore', swimLadder: false, chain: true, curb: 'yellow', tideRange: TR,
      marina: { conduits: ['shore', 'water'], items, cleats: [] } } }, boats = [];
    let x = -14.4 + 0.9;
    YACHTS.forEach(([t, p], k) => { if (!MB.loaded(t)) return; const B = MB.build(t, p), port = -B.bbox.x0, stbd = B.bbox.x1, cxl = r3(x + port), id = 'yacht' + k;
      pon.o.marina.items.push({ kind: 'pedestalY', id: 'pedestal' + k, x: r3(cxl + stbd + 0.62), y: 1.25, rot: 0, serves: [id] });
      for (const bx of [cxl - port + 0.55, cxl + stbd - 0.55]) pon.o.marina.cleats.push({ x: r3(bx), y: hy - 0.60, side: 1, type: 'bollard', berth: id, lead: 'stern' });
      boats.push({ id, type: t, paint: p, at: [cxl, r3(pon.at[1] + hy + 0.9 - B.bbox.y0)], heading: PI2, mooring: 'stern-to', dock: 'pontoon', boardAt: [cxl, hy - 0.35], bowChain: true,
        feed: { module: 'pontoon', pedestal: 'pedestal' + k, outlet: B.cls === 'C' ? 1 : 0, tap: 0, mirror: true } });
      x = cxl + stbd + 1.7; });
    return { mods: [q, pon], gangs: [gw], boats };
  }

  // ============================ THE VENUE =================================================================================
  function venueLayout() {
    const fb = 0.55, q = quay(10, [gateAt(-3), post(-11), post(-6.5), post(0.5), post(8.5), post(13), ...waste(3.4)], [[-4.65, -1.35]]);
    const gw = K.gangwayFrom(q, 'water4', { freeboard: fb, floatGrp: 1 }), hx = 9, hy = 6, boats = [];
    const vm = { items: [{ kind: 'dockLight', x: -8.7, y: -5.7, rot: 0 }, { kind: 'dockLight', x: 8.7, y: -5.7, rot: 0 }, { kind: 'ringPost', id: 'ring', x: 8.3, y: 3.2, rot: -PI2 },
        { kind: 'pedestal2', id: 'pedestalS', x: 2.1, y: -5.37, rot: PI, serves: ['visit0'] }, { kind: 'pedestal2', id: 'pedestalE', x: 8.37, y: -3.9, rot: -PI2, serves: ['visit1'] }],
      cleats: [{ x: 2.9, y: -5.7, side: -1, berth: 'visit0', lead: 'stern' }, { x: 5.0, y: -5.7, side: -1, berth: 'visit0', lead: 'spring' }, { x: 7.3, y: -5.7, side: -1, berth: 'visit0', lead: 'bow' },
        { x: 8.7, y: -5.0, side: -1, ang: PI2, berth: 'visit1', lead: 'stern' }, { x: 8.7, y: -1.2, side: -1, ang: PI2, berth: 'visit1', lead: 'spring' }, { x: 8.7, y: 1.4, side: -1, ang: PI2, berth: 'visit1', lead: 'bow' }] };
    const venue = { id: 'venue', key: 'floatingVenue', at: [0, r3(gw.edgeAt[1] + hy)], rot: 0, grp: 1, o: { hull: 'concrete', freeboard: fb, gangX: -3, faces: [], fixtures: [], guideFace: 'shore', swimLadder: false, chain: true, tideRange: TR, marina: vm } };
    if (MB.loaded('bowrider')) { const B = MB.build('bowrider', 'champagne'); boats.push({ id: 'visit0', type: 'bowrider', paint: 'champagne', at: [4.9, r3(venue.at[1] - hy - 0.15 + B.bbox.x0)], heading: 0, mooring: 'alongside', dock: 'venue', boardAt: [4.4, -5.65], feed: { module: 'venue', pedestal: 'pedestalS', outlet: 0, tap: 0, mirror: false } }); }
    if (MB.loaded('goFast')) { const B = MB.build('goFast', 'venom'); boats.push({ id: 'visit1', type: 'goFast', paint: 'venom', at: [r3(hx + 0.15 - B.bbox.x0), r3(venue.at[1] - 1.4)], heading: PI2, mooring: 'alongside', dock: 'venue', boardAt: [8.65, -2.6], feed: { module: 'venue', pedestal: 'pedestalE', outlet: 0, tap: 0, mirror: false } }); }
    return { mods: [q, venue], gangs: [gw], boats };
  }

  const bedAt = (deep) => (x, y) => y < 1.0 ? DECK : deep - Math.min(0.5, Math.max(0, y - 1) * 0.012);
  const road = (x, y) => y < -3.4 && y > -6.1;
  defineMarina('basin', { label: 'Marina basin', tideRange: TR, bed: bedAt(-2.8), road, fetch: () => 0.28,
    note: 'A concrete quay with its security gate, lamp posts and waste station; a gangway down to a 27 m main walkway on the dock variant chosen (DOCKS); the office snapped to it; nine slips on fingers, bow in, each with its pedestal; a concrete T-head with a 53′ sport fisher alongside, the yacht pedestal and the pump-out. PEI tides, 1.2 m.',
    layout: basinLayout });
  defineMarina('yachtQuay', { label: 'Yacht quay', tideRange: TR, bed: bedAt(-6.2), road, fetch: () => 0.32,
    note: 'The quay for the big hulls: a 32 × 4 m concrete pontoon off the quay, the 90′ skybridge and 53′ convertible sport fishers, the sloop 88 and a go-fast moored stern-to, bow lines to the ground chain; a 63 / 125 A yacht pedestal and two stern bollards each, the pump-out at the end. 6.2 m of water for the 88’s 4.6 m keel.',
    layout: yachtLayout });
  defineMarina('venue', { label: 'Floating venue', tideRange: TR, bed: bedAt(-3.5), road, fetch: () => 0.3,
    note: 'The floating bar, restaurant and concert pontoon off its own quay and gate: the bar under a pavilion with the kitchen pass, six tables and three standing ones under festoon strings, a stage 0.45 m up with a lit truss, the dance floor, two boats in for dinner and plugged in.',
    layout: venueLayout });

  // ============================ THE HARBOUR HOOK: hulls, feeds, lamps =====================================================
  const bh0 = K.buildHarbour;
  K.buildHarbour = function (id, o) { const mdl = bh0(id, o), H = K.HARBOURS[id]; if (MARINAS[id] && H) finish(mdl, H, o || {}); return mdl; };
  function finish(mdl, H, o) {
    const base = { tideRange: H.tideRange }; for (const k of PICK) if (o[k] != null) base[k] = o[k];
    const L = H.layout(base), byId = {}; mdl.modules.forEach(M => { byId[M.id] = M; });
    mdl.lamps = []; mdl.boats = []; mdl.feeds = []; mdl.marina = true;
    for (const M of mdl.modules) for (const l of (M.s._lamps || [])) { const q = M.toH(l.x, l.y); mdl.lamps.push(Object.assign({}, l, { x: r3(q[0]), y: r3(q[1]), grp: M.grp || 0, module: M.id })); }
    let gid = 20;
    for (const b of (L.boats || [])) { if (!MB.loaded(b.type)) continue;
      const g = gid++, P = MB.place(b.type, b.paint, b.at[0], b.at[1], b.heading, g), B = P.B; for (const f of P.faces) mdl.faces.push(f);
      const grp = { id: g, kind: 'boat', members: [b.id], chains: [], fb: B.deck, ax: b.heading, cx: b.at[0], cy: b.at[1], Lx: B.loa, By: B.beam, boat: b.id };
      if (b.bowChain) { const a = P.toH([0, B.bbox.y1 - 1.0, Math.max(0.6, B.deck + 0.3)]), ax = a[0] + Math.cos(b.heading) * 7.5, ay = a[1] + Math.sin(b.heading) * 7.5;
        grp.chains.push({ attach: a, anchor: [r3(ax), r3(ay), r3(H.bed(ax, ay) + 0.3)] }); }
      mdl.groups[g] = grp;
      const rec = { id: b.id, type: b.type, paint: B.paint, label: B.label, rig: B.rig, cls: B.cls, loa: B.loa, beam: B.beam, draft: B.draft, deck: B.deck, board: B.board, bbox: B.bbox, grp: g,
        at: b.at, heading: b.heading, mooring: b.mooring, dock: b.dock, boardAt: b.boardAt, note: B.note, faces: B.faceCount };
      const M = b.feed && byId[b.feed.module], f = M && (M.s._mfix || []).find(r => r.id === b.feed.pedestal);
      if (M && f) { const F2 = MG.FIX[f.kind], c = Math.cos(f.rot), sn = Math.sin(f.rot), fx = (x, y) => [f.x + x * c - y * sn, f.y + x * sn + y * c], n = F2.power.length, oi = b.feed.outlet || 0;
        const so = WANT[0] + (oi - (n - 1) / 2) * Math.min(0.18, F2.w * 0.5), to = WANT[0] + (b.feed.tap ? 1 : -1) * (F2.w / 2 - 0.05);
        const s0 = fx(so, WANT[1] - 0.02), t0 = fx(to, WANT[1] - 0.02), sH = M.toH(s0[0], s0[1]), tH = M.toH(t0[0], t0[1]);
        const sock = [sH[0], sH[1], f.z + MG.Z_SOCKET], tap = [tH[0], tH[1], f.z + MG.Z_TAP], il = B.inlet.slice(); if (b.feed.mirror) il[0] = -il[0];
        const inlet = P.toH(il), fill = P.toH([il[0], il[1] + 0.35, il[2] - 0.05]), FD = MB.feed(g, sock, tap, inlet, fill, f.z); for (const q of FD.faces) mdl.faces.push(q);
        rec.utilities = { pedestal: M.id + '.' + f.id, kind: f.kind, outlet: oi, rating: F2.power[oi], tap: b.feed.tap || 0, cable_m: FD.cable_m, hose_m: FD.hose_m };
        mdl.feeds.push({ boat: b.id, pedestal: M.id + '.' + f.id, rating: F2.power[oi], socket: sock.map(r3), tap: tap.map(r3), inlet: inlet.map(r3), fill: fill.map(r3), cable_m: FD.cable_m, hose_m: FD.hose_m, grp: g }); }
      mdl.boats.push(rec); }
  }

  // ============================ GAMEPLAY ==================================================================================
  // pass 2's gameplay() reads the 16-frame rock of every group from sixteen full frames; a marina has a dozen hulls, so the other
  // fifteen are asked for their motion only (seaMotion, the same numbers frame() would give) while it runs
  function motionOnly(key, o) { const mdl = W.model(key, o), dir = (((o.dir | 0) % 8) + 8) % 8, C = W.cam(dir, o.ppu || W.PPU), tide = o.tide != null ? +o.tide : mdl.s.tide, fi = (((o.frame | 0) % 16) + 16) % 16;
    const wind = Object.assign({ w: 0.2, gust: 0.4, dir: 1 }, o.wind || {}), sea = W.seaOf(o, wind), motion = {};
    for (const g in mdl.groups) { const m = W.seaMotion(sea, mdl.groups[g], C, fi); motion[g] = Object.assign({ z0: tide + m.heave }, m); } return { motion, mdl }; }
  function gameplay(key, o) {
    o = o || {}; const fr0 = W.frame, fi0 = (((o.frame | 0) % 16) + 16) % 16; let gp;
    W.frame = function (k2, o2) { o2 = o2 || {}; return k2 === key && ((((o2.frame | 0) % 16) + 16) % 16) !== fi0 ? motionOnly(k2, o2) : fr0(k2, o2); };
    try { gp = K.gameplay(key, o); } finally { W.frame = fr0; }
    return augment(gp, fr0(key, o), key, o);
  }
  const FITS = V.FITS, inR = (v, R) => v >= R[0] - 1e-9 && v <= R[1] + 1e-9, fitRow = (param, v) => { const R = FITS[param]; return { param, value: v, range: R || null, ok: R ? inR(v, R) : true }; };
  const WHAT = { rest: 'reach', load: 'place · lift · toss', bench: 'bench', knife: 'chop' };
  const lampRule = 'on = clamp((0.36 − day) / 0.20, 0, 1), day = sunI · max(0, sun.z) (0 by moonlight) + 0.45 · skyI: off by day and in overcast, 0.7 at dusk, 1 at night';
  function augment(gp, fr, key, o) {
    const mdl = fr.mdl, VB = gp.VERBS || (gp.VERBS = []), FX = gp.FIXTURES || (gp.FIXTURES = []), BL = gp.BLOCKERS, SP = V.stepPlan, byId = {}; mdl.modules.forEach(M => { byId[M.id] = M; });
    const yawOf = (d) => r2(Math.atan2(d[0], d[1]) / G.DEG), zOf = (M, x, y, z) => { if (!M.s.floating) return z; const P = fr.poses[M.grp]; return P ? P.fwd([x, y, z])[2] : z + fr.tide; };
    const recs = [], used = {};
    for (const M of mdl.modules) { const dirH = (v) => rot2(v, M.rot || 0);
      for (const f of (M.s._mfix || [])) { const K2 = MG.FIX[f.kind] || V.FIX[f.kind]; if (!K2) continue; used[f.kind] = K2;
        const fw0 = [-Math.sin(f.rot), Math.cos(f.rot)], rt0 = [Math.cos(f.rot), Math.sin(f.rot)], so = K2.standOff || [0, 0], lx = f.x + rt0[0] * so[0] + fw0[0] * so[1], ly = f.y + rt0[1] * so[0] + fw0[1] * so[1];
        const q = M.toH(lx, ly), fwd = dirH(fw0), z = zOf(M, lx, ly, f.z), id = M.id + '.' + f.id, rides = M.s.floating ? M.grp : null;
        const rec = { id, kind: f.kind, label: K2.label, module: M.id, stand: [r2(q[0]), r2(q[1]), r3(z)], yaw: yawOf(fwd), height: K2.h, rides, fits: K2.fit.map(([p, v]) => fitRow(p, v)) };
        if (f.meta) rec.meta = f.meta; if (f.serves) rec.serves = f.serves;
        if (K2.d > 0) { const hw = K2.w / 2, rt = [fwd[1], -fwd[0]], a0 = M.toH(f.x, f.y), ox = /^pedestal/.test(f.kind) ? WANT[0] : 0, pt = (u, v) => [r2(a0[0] + rt[0] * u + fwd[0] * v), r2(a0[1] + rt[1] * u + fwd[1] * v)];
          rec.footprint = [pt(ox - hw, K2.stand), pt(ox + hw, K2.stand), pt(ox + hw, K2.stand + K2.d), pt(ox - hw, K2.stand + K2.d)]; BL.push({ shape: 'poly', poly: rec.footprint, h: K2.h, kind: f.kind, module: M.id, rides }); }
        FX.push(rec); recs.push({ rec, M, f, K: K2 });
        for (const v of (K2.verbs || [])) { const S = MG.SEQ[v] || V.SEQ[v]; if (S) VB.push(Object.assign({ id: id + '.' + v, verb: v, fixture: id, module: M.id, stand: rec.stand, yaw: rec.yaw, rides, fits: rec.fits }, S())); } } }
    // surfaces pass 2 does not know
    for (const w of gp.WALK) { const M = byId[w.module]; if (!M) continue; if (M.s.hull === 'composite' && M.s.floating) w.surface = 'composite boards'; if (M.s.family === 'office' || M.s.family === 'venue') w.surface = M.s.hull === 'concrete' ? 'concrete' : w.surface || 'deck'; }
    // ---- BERTHS and BOARD for the hulls in their berths ----
    const berths = [], board = [], tieByBerth = {};
    for (const t of gp.TIE) { const m = /\.marina\.(.+)$/.exec(t.berth || ''); if (m) (tieByBerth[m[1]] || (tieByBerth[m[1]] = [])).push(t.id); }
    for (const b of (mdl.boats || [])) { const P = fr.poses[b.grp], M = byId[b.dock], hd = [Math.cos(b.heading), Math.sin(b.heading)], bid = 'marina.' + b.id;
      const ctr = P ? P.fwd([b.at[0], b.at[1], 0]) : [b.at[0], b.at[1], fr.tide], soleZ = P ? P.fwd([b.at[0], b.at[1], b.board])[2] : fr.tide + b.board;
      const dz = M ? zOf(M, b.boardAt[0], b.boardAt[1], M.s.floating ? M.s.freeboard : M.s.deckZ) : fr.tide + 0.5, at2 = M ? M.toH(b.boardAt[0], b.boardAt[1]) : b.at, step = r3(soleZ - dz);
      const aboard = SP(step, { stepBox: true }), ashore = SP(-step, {}), C = G.CLASSES[b.cls] || G.CLASSES.B, c = hd[0], s = hd[1];
      let bu = -1e9; for (const fx of [0, 0.5, 1]) for (const fy of [0, 0.5, 1]) { const x = b.bbox.x0 + (b.bbox.x1 - b.bbox.x0) * fx, y = b.bbox.y0 + (b.bbox.y1 - b.bbox.y0) * fy; bu = Math.max(bu, mdl.bed(b.at[0] + x * s + y * c, b.at[1] - x * c + y * s)); }
      const floatsFrom = r2(bu + b.draft + C.ukc), yaw = yawOf(M ? rot2([0, 1], M.rot || 0) : [0, 1]);
      berths.push({ id: bid, marina: true, hull: b.type, label: b.label, rig: b.rig, paint: b.paint, cls: b.cls, loa: b.loa, beam: b.beam, draft: b.draft, mooring: b.mooring, dock: b.dock, rides: b.grp,
        footprint: { centre: [r2(ctr[0]), r2(ctr[1])], heading: hd.map(r3), length: b.loa, width: b.beam }, bedUnder: r2(bu), depthAtDatum: r2(-bu), afloatAtDatum: floatsFrom <= 0, floatsFrom, afloatNow: fr.tide >= floatsFrom,
        ties: tieByBerth[b.id] || [], utilities: b.utilities || null, boarding: b.note });
      board.push({ berth: bid, hull: b.type, at: [r2(at2[0]), r2(at2[1])], yaw, deckZ: r3(dz), hullSoleZ: r3(soleZ), step, clip: aboard.how === 'ladder' ? 'ladder' : aboard.first || 'none', opts: aboard.opts,
        fitsCreatorBodies: aboard.fits && ashore.fits, how: { aboard: aboard.how, ashore: ashore.how }, aboard, ashore, ladder: null, rides: M && M.s.floating ? M.grp : null, hullRides: b.grp, note: 'the dock and the hull both float: the step is the same at every tide, less the rock' });
      const fitsOf = (p) => p.seq.filter(q => q.clip === 'board' || q.clip === 'boardDown').map(q => fitRow(q.clip === 'board' ? 'stepUp' : q.reverse ? 'stepUpNoGrip' : 'stepDown', q.opts.railZ));
      VB.push({ id: bid + '.stepAboard', verb: 'stepAboard', berth: bid, hull: b.type, stand: [r2(at2[0]), r2(at2[1]), r3(dz)], yaw, rides: M && M.s.floating ? M.grp : null, step, how: aboard.how, seq: aboard.seq, fits: fitsOf(aboard), fitsCreatorBodies: aboard.fits });
      VB.push({ id: bid + '.stepAshore', verb: 'stepAshore', berth: bid, hull: b.type, stand: [r2(at2[0]), r2(at2[1]), r3(soleZ)], yaw: r2(yaw + 180), rides: b.grp, step: r3(-step), how: ashore.how, seq: ashore.seq, fits: fitsOf(ashore), fitsCreatorBodies: ashore.fits }); }
    gp.BERTHS = gp.BERTHS.concat(berths); gp.BOARD = gp.BOARD.concat(board);
    // ---- UTILITIES ----
    const peds = recs.filter(r => /^pedestal/.test(r.f.kind)).map(r => ({ id: r.rec.id, kind: r.f.kind, label: r.K.label, at: r.rec.stand, yaw: r.rec.yaw, outlets: r.K.power.map((p, k) => ({ id: k, rating: p, z: MG.Z_SOCKET })), taps: r.K.taps, tapZ: MG.Z_TAP, light: true, serves: [] }));
    for (const f of (mdl.feeds || [])) { const p = peds.find(q => q.id === f.pedestal); if (p) p.serves.push({ berth: 'marina.' + f.boat, rating: f.rating, cable_m: f.cable_m, hose_m: f.hose_m }); }
    const nB = (mdl.boats || []).length, nP = (mdl.boats || []).filter(b => b.utilities && b.utilities.rating).length;
    gp.UTILITIES = { rule: 'every berth: one power outlet and one fresh-water tap on a pedestal on its own dock; the float carries its water main and power conduit along the fascia', pedestals: peds,
      feeds: (mdl.feeds || []).map(f => Object.assign({}, f, { grp: undefined, rides: f.grp, note: 'cable and hose ride with the hull (the pedestal end moves with the dock by the difference of the two rocks, under 2 cm)' })),
      berths: nB, withPower: nP, withWater: nP, ok: nB === nP };
    // ---- LIGHTS ----
    const L0 = mdl.lamps || [].concat(...mdl.modules.map(M => (M.s._lamps || []).map(l => { const q = M.toH(l.x, l.y); return Object.assign({}, l, { x: q[0], y: q[1], grp: M.grp || 0, module: M.id }); })));
    const lampsAt = L0.map((l, k) => { const P = l.grp ? fr.poses[l.grp] : null, p = P ? P.fwd([l.x, l.y, l.z]) : [l.x, l.y, l.z];
      return { id: 'lamp' + k, kind: l.kind, module: l.module, at: p.map(r3), radius: l.r, intensity: l.i, colour: l.c, cycle: l.cyc ? { colours: l.cyc, phase: l.ph, framesPerStep: 4 } : undefined, rides: l.grp || null, fixture: l.fixture || undefined }; });
    gp.LIGHTS = { rule: lampRule, falloff: 'E += intensity · (0.25 + 0.75 · max(0, n·l)) · (1 − (d / radius)²)² · on; the lamp’s colour tints what it lights by E share; lenses, bulbs and windows glow at on', lamps: lampsAt, count: lampsAt.length };
    // ---- GATES ----
    const gates = recs.filter(r => r.f.kind === 'gate').map(r => { const { M, f } = r, c = Math.cos(f.rot), sn = Math.sin(f.rot), P = (x, y) => { const q = M.toH(f.x + x * c - y * sn, f.y + x * sn + y * c); return [r2(q[0]), r2(q[1])]; }, z = r3(zOf(M, f.x, f.y, f.z));
      const shut = [P(-0.6, 0.30), P(0.6, 0.30)], fence = [[P(-1.6, 0.30), P(-0.69, 0.30)], [P(0.69, 0.30), P(1.6, 0.30)], [P(-1.6, 0.30), P(-1.6, 1.45)], [P(1.6, 0.30), P(1.6, 1.45)]];
      for (const [a, b2] of fence) BL.push({ shape: 'wall', a, b: b2, h: 2.05, kind: 'gate fence', module: M.id });
      BL.push({ shape: 'wall', a: shut[0], b: shut[1], h: 2.05, kind: 'gate leaf', door: true, opens: 'openGate', module: M.id });
      const gwN = (gp.WALK || []).filter(w => w.kind === 'gangway' && w.hinge).map(w => Math.hypot(w.hinge[0] - (shut[0][0] + shut[1][0]) / 2, w.hinge[1] - (shut[0][1] + shut[1][1]) / 2)).sort((a, b2) => a - b2)[0];
      return { id: r.rec.id, module: M.id, opening: { a: shut[0], b: shut[1], width: 1.2, z }, reader: { stand: r.rec.stand, yaw: r.rec.yaw, z: r3(z + MG.Z_TAP), verb: r.rec.id + '.openGate' }, leaf: { hinge: shut[0], swing: 'in, to the quay', closer_s: 4, state: 'shut' },
        fence, wings: 'mesh, 1.15 m out over the water, spiked', lamp: true, guards: gwN != null && gwN < 2.5 ? 'the gangway at ' + r2(gwN) + ' m' : null }; });
    gp.GATES = gates;
    // ---- WASTE ----
    gp.WASTE = recs.filter(r => /^(binGeneral|binRecycle|bottleBank|oilTank|pumpOut)$/.test(r.f.kind)).map(r => ({ id: r.rec.id, kind: r.f.kind, label: r.K.label, stand: r.rec.stand, yaw: r.rec.yaw, verbs: r.K.verbs.map(v => r.rec.id + '.' + v), fits: r.rec.fits }));
    // ---- OFFICE and VENUE ----
    const poly = (M, pts) => pts.map(([x, y]) => M.toH(x, y).map(r2));
    gp.OFFICE = mdl.modules.filter(M => M.s._office).map(M => { const O = M.s._office;
      for (const [a, b2] of [[O.building[0], O.building[1]], [O.building[1], O.building[2]], [O.building[3], O.building[0]]]) BL.push({ shape: 'wall', a: M.toH(a[0], a[1]).map(r2), b: M.toH(b2[0], b2[1]).map(r2), h: O.walls, kind: 'office wall', module: M.id, rides: M.grp });
      return { module: M.id, rides: M.grp, building: poly(M, O.building), door: poly(M, O.door), hatch: poly(M, O.hatch), hatchZ: O.hatchZ, walls: O.walls, ridge: O.ridge,
        staff: recs.filter(r => r.M === M && r.f.kind === 'officeDesk').map(r => r.rec.id), customers: recs.filter(r => r.M === M && r.f.kind === 'serviceHatch').map(r => r.rec.id), note: 'the attendant works the counter (bench 0.80) facing the hatch; boaters check in across the shelf (reach 0.95)' }; });
    gp.VENUE = mdl.modules.filter(M => M.s._venue).map(M => { const Vn = M.s._venue, up = SP(Vn.stage.deckZ, {}), dn = SP(-Vn.stage.deckZ, {}), role = (k) => recs.filter(r => r.M === M && r.f.kind === k).map(r => r.rec.id);
      BL.push({ shape: 'poly', poly: poly(M, [Vn.bar.counter[0], [Vn.bar.counter[1][0], Vn.bar.counter[0][1]], Vn.bar.counter[1], [Vn.bar.counter[0][0], Vn.bar.counter[1][1]]]), h: 0.95, kind: 'bar counter', module: M.id, rides: M.grp });
      BL.push({ shape: 'poly', poly: poly(M, [Vn.kitchen[0], [Vn.kitchen[1][0], Vn.kitchen[0][1]], Vn.kitchen[1], [Vn.kitchen[0][0], Vn.kitchen[1][1]]]), h: 2.6, kind: 'kitchen', module: M.id, rides: M.grp });
      gp.WALK.push({ module: M.id, kind: 'stage', poly: poly(M, Vn.stage.poly), surface: 'plank', z: r3(zOf(M, 0, 0, Vn.top) + Vn.stage.deckZ), rides: M.grp, step: { up: up, down: dn, at: M.toH(Vn.stage.steps.at[0], Vn.stage.steps.at[1]).map(r2), treads: 3 } });
      return { module: M.id, rides: M.grp, bar: { counter: poly(M, Vn.bar.counter), counterZ: 0.95, workZ: 0.80, staff: role('barWork'), patrons: role('barFront'), stools: role('barStool'), pass: role('kitchenPass') },
        dining: { tables: role('dineTable'), standing: role('highTable'), seatZ: 0.45, tableZ: 0.75 }, stage: { poly: poly(M, Vn.stage.poly), deckZ: Vn.stage.deckZ, trussZ: Vn.stage.trussZ, step: { up: up.how, down: dn.how }, singer: role('micStand'), keys: role('keysStand'), drums: role('conga') },
        dance: { floor: poly(M, [Vn.dance[0], [Vn.dance[1][0], Vn.dance[0][1]], Vn.dance[1], [Vn.dance[0][0], Vn.dance[1][1]]]), spots: role('danceSpot') }, pavilionRoofZ: Vn.roofZ }; });
    // ---- the v9.2 fit, the requests, the checks ----
    const rows = []; for (const k in used) { const K2 = used[k]; for (const [p, v] of (K2.fit || [])) rows.push(Object.assign({ fixture: K2.label, what: WHAT[p] || p }, fitRow(p, v))); }
    gp.CHAR_FIT = (gp.CHAR_FIT || []).concat(rows); gp.CHAR_REQUESTS = (gp.CHAR_REQUESTS || []).concat(MG.REQUESTS);
    gp.CHECKS = checks(gp, mdl, fr);
    gp.schema = 'hidden-harbours/marina-gameplay@1'; gp.extends = 'hidden-harbours/wharf-gameplay@2'; gp.rig = (gp.rig || '') + ' + marinaRig.js + marinaBoats.js + marinaKit.js';
    gp.MARINA = { docks: o.hull || (mdl.harbour ? 'composite' : mdl.s.hull), dockLabel: MG.HULL_LABEL[o.hull || (mdl.harbour ? 'composite' : mdl.s.hull)] || null, hulls: (mdl.boats || []).length, lamps: L0.length, lampRule };
    return gp;
  }
  // footprints as oriented rectangles; separating-axis overlap
  function rect(cx, cy, hd, L, B) { const c = Math.cos(hd), s = Math.sin(hd), u = [c, s], v = [-s, c]; return [[1, 1], [1, -1], [-1, -1], [-1, 1]].map(([a, b]) => [cx + u[0] * a * L / 2 + v[0] * b * B / 2, cy + u[1] * a * L / 2 + v[1] * b * B / 2]); }
  function overlap(A, B) { for (const P of [A, B]) for (let i = 0; i < 4; i++) { const p = P[i], q = P[(i + 1) % 4], n = [q[1] - p[1], p[0] - q[0]];
    let a0 = 1e9, a1 = -1e9, b0 = 1e9, b1 = -1e9; for (const r of A) { const d = r[0] * n[0] + r[1] * n[1]; a0 = Math.min(a0, d); a1 = Math.max(a1, d); } for (const r of B) { const d = r[0] * n[0] + r[1] * n[1]; b0 = Math.min(b0, d); b1 = Math.max(b1, d); }
    if (a1 < b0 + 1e-6 || b1 < a0 + 1e-6) return false; } return true; }
  function checks(gp, mdl, fr) {
    const out = [], add = (id, ok, what, detail) => out.push({ id, ok: !!ok, what, detail });
    const B = mdl.boats || [], U = gp.UTILITIES;
    add('utilities', U && U.ok, 'every berth has an outlet and a tap', U ? U.withPower + ' of ' + U.berths + ' berths plugged in and on the water' : '—');
    const bad = (gp.FIXTURES || []).filter(f => (f.fits || []).some(q => !q.ok)); add('fixtures', !bad.length, 'every fixture a figure uses fits every creator body', (gp.FIXTURES || []).length + ' fixtures, ' + bad.length + ' outside the fit');
    const bb = (gp.BOARD || []).filter(b => b.fitsCreatorBodies === false); add('boarding', !bb.length, 'every step aboard and ashore fits every creator body', (gp.BOARD || []).length + ' berths, ' + bb.length + ' unfit' + (bb.length ? ' (' + bb.map(b => b.berth).join(', ') + ')' : ''));
    const R = B.map(b => ({ id: b.id, r: rect(b.at[0] + Math.cos(b.heading) * (b.bbox.y0 + b.bbox.y1) / 2, b.at[1] + Math.sin(b.heading) * (b.bbox.y0 + b.bbox.y1) / 2, b.heading, b.bbox.y1 - b.bbox.y0, b.bbox.x1 - b.bbox.x0) })), hits = [];
    for (let i = 0; i < R.length; i++) for (let j = i + 1; j < R.length; j++) if (overlap(R[i].r, R[j].r)) hits.push(R[i].id + '·' + R[j].id);
    for (const M of mdl.modules) { if (M.s.family === 'gangway') continue; const m = rect(M.at[0], M.at[1], M.rot || 0, M.s.L - 0.02, M.s.W - 0.02); for (const b of R) if (overlap(b.r, m)) hits.push(b.id + '·' + M.id); }
    add('clearance', !hits.length, 'no hull overlaps another hull or a dock', hits.length ? hits.join(', ') : B.length + ' hulls clear');
    const dry = (gp.BERTHS || []).filter(b => b.marina && !b.afloatAtDatum); add('depth', !dry.length, 'every hull floats at chart datum', dry.length ? dry.map(b => b.id + ' from ' + b.floatsFrom).join(', ') : 'all afloat at datum');
    const gws = (gp.WALK || []).filter(w => w.kind === 'gangway'); add('gangways', gws.every(w => w.toeOnFloat_m ? w.toeOnFloat_m.datum >= 0.3 && w.toeOnFloat_m.highestWater >= 0.3 : true), 'every gangway toe stays 0.3 m inside its float', gws.map(w => w.module + ' ' + (w.toeOnFloat_m ? w.toeOnFloat_m.datum + ' / ' + w.toeOnFloat_m.highestWater + ' m' : '—')).join(' · ') || 'none');
    const G2 = gp.GATES || []; add('gates', gws.length <= G2.filter(g => g.guards).length, 'every gangway has a gate at its head', G2.length + ' gate' + (G2.length === 1 ? '' : 's') + ' · ' + gws.length + ' gangway' + (gws.length === 1 ? '' : 's'));
    return out;
  }
  function fitReport() { const rows = []; for (const k in MG.FIX) { const K2 = MG.FIX[k]; for (const [p, v] of K2.fit) rows.push(Object.assign({ fixture: K2.label, what: WHAT[p] || p }, fitRow(p, v))); }
    rows.push(Object.assign({ fixture: 'venue stage · three treads', what: 'step up, no grip' }, fitRow('stepUpNoGrip', 0.45)), Object.assign({ fixture: 'venue stage', what: 'step down' }, fitRow('stepDown', 0.45))); return rows; }
  const SCENES = [['harbour:basin', 'Marina basin'], ['harbour:yachtQuay', 'Yacht quay'], ['harbour:venue', 'Floating venue'],
    ['compositeMarina', 'Composite float'], ['cubeMarina', 'Cube float'], ['timberMarina', 'Timber float'], ['drumMarina', 'Drum float'], ['yachtPontoon', 'Yacht pontoon'],
    ['floatingOffice', 'Floating office'], ['floatingVenue', 'Venue pontoon'], ['gateQuay', 'Security gate'], ['wasteQuay', 'Waste station']];
  root.MarinaKit = { MARINAS, SCENES, TR, DECK, gameplay, augment, motionOnly, fitReport, checks, finish, defineMarina, basinLayout, yachtLayout, venueLayout, W_SLIPS, E_SLIPS, YACHTS, lampRule };
})(typeof globalThis !== 'undefined' ? globalThis : window);
