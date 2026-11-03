// The delivered fleet registration and yard layout, extracted without changing their expressions.
(function(root){'use strict'; const MB=root.MarinaBoats,G=root.WharfGeo2,r3=v=>Math.round(v*1000)/1000;
  const LOB = {   // the register's boats (Data/Boats/Owners), by their variant descriptors
    lobStdHardtopNth: { size: 'standard', style: 'hardtop', region: 'northumberland', table: 'lobster' },
    lobStdOpenFundy: { size: 'standard', style: 'open', region: 'fundy', table: 'lobster' },
    lobInHardtopNfld: { size: 'inshore', style: 'hardtop', region: 'newfoundland', table: 'lobsterIn' },
    lobInOpenNth: { size: 'inshore', style: 'open', region: 'northumberland', table: 'lobsterIn' },
    lobStdHardtopFundy: { size: 'standard', style: 'hardtop', region: 'fundy', table: 'lobster' },
  };
  const wlOf = (st) => st.kz + 0.25 * st.dep;   // the boot band is 0 to 0.27 of the depth: the water just under its top
  function addFleet() {
    const R = root.LobsterBoatVariantsIso;
    if (R) for (const id in LOB) { const v = LOB[id];
      const V = () => R.resolve(Object.assign({}, v)), wl = () => wlOf(V().station(0.5));
      MB.TYPES[id] = { global: 'LobsterBoatVariantsIso', table: v.table,
        faces: (Rr) => Rr.faces(Object.assign({}, v)),
        mats: (Rr, p) => { const Q = Rr.paintRamps(p), o = { hull: Q.top, boot: Q.boot, cream: Q.house, deck: Rr.DECKF, grip: Rr.GRIP, glas: Rr.GLAS, blue: Q.stripe, steel: Rr.STEEL, iron: Rr.IRON }, M = {};
          for (const k in o) M[k] = { ramp: o[k], off: 0 }; M.blk = { ramp: Q.boot, off: -1 }; M.dark = { ramp: Q.boot, off: -2 }; return M; },
        paints: (Rr) => Rr.PAINTS.map(p => p.id), dwl: () => wl(), deck: () => r3(V().DECK - wl()), board: () => r3(V().DECK - wl()),
        note: 'boarded over the washboard' }; }
    const C = root.CapeIslanderIso;
    if (C) MB.TYPES.capeIslander = { global: 'CapeIslanderIso', table: 'cape', faces: (Rr) => Rr.faces(), mats: (Rr, p) => Rr.palette({ scheme: p }).mats,
      paints: (Rr) => Rr.schemeIds || [], dwl: (Rr) => wlOf(Rr.loft.station(0.5)), deck: (Rr) => r3(Rr.loft.DECK - wlOf(Rr.loft.station(0.5))),
      board: (Rr) => r3(Rr.loft.DECK - wlOf(Rr.loft.station(0.5))), note: 'boarded over the washboard' };
    const S = root.SportSkiffIso;
    if (S && S.allFaces && !MB.TYPES.sportSkiff) MB.TYPES.sportSkiff = { global: 'SportSkiffIso', table: 'sportSkiff', faces: (Rr) => Rr.allFaces(),
      mats: (Rr, p) => Rr.palette ? Rr.palette({ scheme: p }).mats : { hull: { ramp: Rr.PAINT, off: 0 }, trim: { ramp: Rr.TRIM, off: 0 }, steel: { ramp: Rr.STEEL, off: 0 }, deck: { ramp: Rr.DECKF, off: 0 }, canv: { ramp: Rr.CANV, off: 0 }, glas: { ramp: Rr.GLAS, off: 0 }, moto: { ramp: Rr.MOTO, off: 0 } },
      paints: (Rr) => Rr.schemeIds || ['default'], dwl: (Rr) => Rr.WATERLINE != null ? Rr.WATERLINE : 0, deck: () => 0.12, board: () => 0.6, note: 'boarded over the gunwale' };
  }
  addFleet();


const K=root.WharfRig2Kit,DECK=3.4,KZ=2.2,TR=4.4,KD=5.6,PI=Math.PI,HP=PI/2,L=root.NmcWharf.L,H={},yardD=root.NmcWharf.yardD,kitBed=root.NmcWharf.kitBed;
    const quay = (id, key, q, OX, o) => ({ id, key, at: [r3((q.x0 + q.x1) / 2 - OX), (q.face + q.back) / 2], rot: PI,
      o: Object.assign({ bays: Math.round((q.x1 - q.x0) / 4.2), bayLen: r3((q.x1 - q.x0) / Math.round((q.x1 - q.x0) / 4.2)), width: q.back - q.face, deckZ: KD, tideRange: TR }, o || {}) });
    { const OX = 198, g = DECK + KZ, SY = root.ShipyardGeo2;
      const part = (id, p, x, y, rot, args, Lp, Wp) => ({ id, key: 'syPart', at: [r3(x - OX), y], rot: rot || 0, o: { part: p, args: Object.assign({ g }, args), bayLen: Lp || 4, width: Wp || 4, faces: [], fixtures: [], deckZ: g, tideRange: TR } });
      const q = quay('yardFront', 'concreteQuay', L.yardFront, OX, { curb: 'none', bays: 7, bayLen: 4.0, width: 10, open: { water: [[-7.6, -4.4], [4.4, 7.6]] }, fixtures: [] });
      const pier = (id, x) => ({ id, key: 'steelPier', at: [r3(x - OX), r3(86 - L.liftPierLen / 2)], rot: -HP, o: { bays: 6, bayLen: 3.2, width: 2.6, faces: [], fixtures: [], deckZ: g, rail: 'none', curb: 'none', tideRange: TR, ends: { nx: 'join' } } });
      const mods = [q, pier('pierW', L.liftPiers[0]), pier('pierE', L.liftPiers[1])];
      if (SY) { const c = (L.liftPiers[0] + L.liftPiers[1]) / 2;
        mods.push(part('lift', 'lift', c, 75.4, PI, { tw: 6.0, wb: 5.8, hg: 7.8, hull: { type: 'capeIslander', paint: 'banks-white', keelZ: g - 1.0, cy: 0 }, slingY: [-3.6, 3.6], wash: -3.0 }, 13, 13));
        mods.push(part('slab', 'ground', 198, 111, 0, { w: 27.5, d: 30, surf: 'conc', J: 5.0, seed: 13 }, 27.5, 30));
        mods.push(part('hardA', 'hard', 191.5, 103.5, 0, { id: 'hardA', type: 'lobStdHardtopFundy', paint: 'gelcoat', hd: 0 }, 13, 5));
        mods.push(part('hardB', 'hard', 204.0, 113.0, 0, { id: 'hardB', type: 'sportSkiff', paint: 0, hd: 0 }, 9, 4));
        mods.push(part('hardC', 'hard', 208.2, 121.8, 0, { id: 'hardC', type: 'bowrider', paint: 'harbour-white', hd: PI }, 7, 3));
        mods.push(part('shed', 'shed', 199, 131.4, PI, { S: 11, Ln: 11, He: 5.4, Hr: 6.6, colour: 'red', rot: 0, doorW: 7, doorH: 5.0, craneY: 4.2, hookX: -2.0 }, 11, 11));
        mods.push(part('floodW', 'flood', 186, 97.6, 0, { h: 8 }, 1.6, 0.6), part('floodE', 'flood', 210, 124, 0, { h: 8 }, 1.6, 0.6));
        mods.push(part('fence', 'fence', 198, 0, 0, { pts: [[184.4, 98], [184.4, 136]], gate: [17, 26] }, 38, 0.3));   // the gate on the main road
        mods.push(part('props', 'props', 198, 0, 0, { items: [{ kind: 'stands', x: 208, y: 104, n: 4 }, { kind: 'blocking', x: 207.5, y: 118.5, n: 5 }, { kind: 'drums', x: 186.8, y: 127.2, n: 3 }] }, 1, 1)); }
      H.yard = { OX };
      K.define('nmc_yard', { label: 'NMC \u00b7 Harris & Sons boat yard', tideRange: TR, bed: (x, y) => { const X = x + OX; return yardD(X, y) < 0 && y > 86.2 ? g - 0.15 : kitBed(X, y); }, road: () => false, layout: () => ({ mods, gangs: [], lines: [] }) }); }

})(globalThis);
