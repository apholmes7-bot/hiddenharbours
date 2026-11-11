/* Hidden Harbours — MARINA RIG · THE BOATS (globalThis.MarinaBoats). Load after marinaRig.js and the hull rigs it uses.
   Pass 2 published berths as footprints and drew no hull in them. Here a hull is part of the harbour: its faces go into the same
   G-buffer as the docks, in a float group of its own, so it is lit by the same WeatherSky, shades and is shaded, takes snow and
   rain by its own porosity, rides the same sea (the waterplane average over its own length and beam, seaMotion) and is cut at
   its waterline by the same water shader, with the foam lace where it pierces the surface.

     sloop 88              Sloop88Iso         allFaces(0, stowed: main down under its cover, genoa and staysail furled, platform down)
     sloop 30              SloopIso           allFaces(0, stowed)
     sport fisher 53′      SportFisherIso3    convertible · faceList('stowed') + doorFaces(0)
     sport fisher 90′      SportFisherIso3    skybridge   · faceList('stowed') + doorFaces(0)
     bowrider              BowriderIso        allFaces({})
     go-fast               GoFastIso          allFaces({})

   The four allFaces exports were added to those rigs for this (Sloop88Iso, SloopIso, BowriderIso, GoFastIso): each returns the
   face list its own render() paints, and nothing a rig draws changed. A hull's face keeps its own bias; its material becomes a
   five-band ramp drawn from the rig's own palette for the paint asked for (the rig's colours, no new ones), with the weather of
   its class. Cabin, underbody and section faces are left out: the water covers what is under it.                              */
(function (root) {
  'use strict';
  const G = root.WharfGeo2, MG = root.MarinaGeo;
  if (!G || !MG) throw new Error('marinaBoats.js: load the wharf pass 2 files and marinaRig.js first');
  if (root.MarinaBoats) return;
  const clamp = G.clamp, r3 = (v) => Math.round(v * 1000) / 1000;
  const PROPS = (n) => /glas/.test(n) ? { por: 0, gloss: 0.95, hold: 0.3, cls: 'glass' }
    : /teak|deck|sole|nonskid/.test(n) ? { por: 0.25, gloss: 0.2, hold: 1, cls: 'wood' }
    : /canv|uph|leath|carpet|sail|batten|rope/.test(n) ? { por: 0.38, gloss: 0.05, hold: 0.9, cls: 'rope' }
    : /steel|iron|wire|mast|spar|moto|drv|eng|blk|dark/.test(n) ? { por: 0.03, gloss: 0.7, hold: 0.6, cls: 'metal' }
    : { por: 0.03, gloss: 0.6, hold: 0.8, cls: 'paint' };
  // the rig's ramp (5–7 steps) onto the five bands, rounded to its own colours: band 2 is a hull side in sky light, 3 a lit top
  const POS = [0.1, 0.3, 0.55, 0.8, 1.0];
  const ramp5 = (ramp, off) => { const n = ramp.length; return POS.map(p => ramp[clamp(Math.round(p * (n - 1)) + (off || 0), 0, n - 1)]); };

  const STOWED = { hoist: 0, cover: true, furl: 1, sfurl: 1, platform: 1, doorOpen: 0 };
  const PAL = { mats: (R, p) => R.palette({ scheme: p }).mats, paints: (R) => R.schemeIds || [] };
  const SF3 = (id) => ({ global: 'SportFisherIso3', table: id === 'skybridge' ? 'skybridge' : 'sportFisher',
    faces: (R) => { const H = R.byId(id); return H.faceList('stowed', false).concat(H.doorFaces(0)); },
    mats: (R, p) => { const P = R.paintRamps(p), M = { hull: P.top, boot: P.boot, stripe: P.stripe, cream: P.house, teak: R.TEAK, glas: R.GLAS, steel: R.STEEL, iron: R.IRON,
      carpet: R.CARPET, leath: R.LEATH, teakg: R.TEAKG, stone: R.STONE, whiteg: R.WHITEG, glassd: R.GLASSD }, o = {};
      for (const k in M) o[k] = { ramp: M[k], off: 0 }; o.blk = { ramp: P.boot, off: -1 }; o.dark = { ramp: P.boot, off: -2 }; return o; },
    paints: (R) => R.PAINTS.map(p => p.id), dwl: (R) => R.byId(id).DWL,
    deck: (R) => r3(R.byId(id).loft.DECK - R.byId(id).DWL), board: (R) => r3(R.byId(id).loft.DECK - R.byId(id).DWL),
    note: 'boarded through the transom door onto the cockpit sole' });
  const TYPES = {
    sloop88: Object.assign({ global: 'Sloop88Iso', table: 'sloop88', faces: (R) => R.allFaces(0, STOWED).faces, dwl: (R) => R.DWL,
      deck: (R) => r3(R.SOLE - R.DWL), board: (R) => r3(R.PLAT - R.DWL), note: 'boarded over the swim platform, down 0.20 m above the water' }, PAL),
    sloop30: Object.assign({ global: 'SloopIso', table: 'sloop30', faces: (R) => R.allFaces(0, STOWED).faces, dwl: (R) => R.DWL,
      deck: (R) => r3(R.SOLE - R.DWL), board: () => G.HULL.sloop30.gun, note: 'boarded over the rail onto the side deck' }, PAL),
    sportFisher: SF3('convertible'), skybridge: SF3('skybridge'),
    bowrider: Object.assign({ global: 'BowriderIso', table: 'bowrider', faces: (R) => R.allFaces({}), dwl: (R) => R.WATERLINE,
      deck: (R) => r3(Math.max(0.02, (R.DECK != null ? R.DECK : R.WATERLINE) - R.WATERLINE)), board: () => G.HULL.bowrider.gun, note: 'boarded over the gunwale' }, PAL),
    goFast: Object.assign({ global: 'GoFastIso', table: 'goFast', faces: (R) => R.allFaces({}), dwl: (R) => R.WATERLINE,
      deck: (R) => r3((R.SOLE != null ? R.SOLE : R.WATERLINE) - R.WATERLINE), board: () => G.HULL.goFast.gun, note: 'boarded over the gunwale' }, PAL),
  };
  // where each hull takes shore power and water, in its own frame (x starboard, y bow, z above the waterline), port side;
  // the kit mirrors it to the side that faces the pedestal
  const INLET = { sloop88: [-2.6, -10.2, 1.55], sloop30: [-1.05, -3.2, 0.95], sportFisher: [-2.05, -5.6, 1.05], skybridge: [-3.0, -10.6, 1.15], bowrider: [-0.95, -1.9, 0.62], goFast: [-0.95, -3.8, 0.72] };

  const CACHE = new Map();
  function loaded(type) { const T = TYPES[type]; return !!(T && root[T.global]); }
  function build(type, paint) {
    const T = TYPES[type]; if (!T) throw new Error('MarinaBoats: no hull "' + type + '"');
    const R = root[T.global]; if (!R) throw new Error('MarinaBoats: ' + T.global + ' is not loaded');
    const paints = T.paints(R) || [], p = paints.indexOf(paint) >= 0 ? paint : (paints[0] || 'default'), key = type + '|' + p;
    let B = CACHE.get(key); if (B) return B;
    const mats = T.mats(R, p), dwl = T.dwl(R), src = T.faces(R), faces = [], used = {}, keys = Object.keys(mats);
    const mk = (name) => { const nm = mats[name] ? name : (mats.paint ? 'paint' : mats.hull ? 'hull' : keys[0]), k = 'b.' + type + '.' + p + '.' + nm;
      if (!used[k]) { const m = mats[nm]; MG.addMat(k, ramp5(m.ramp, m.off), PROPS(nm)); used[k] = 1; } return k; };
    let x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9, z1 = -1e9;
    for (const f of src) { if (!f.v || f.v.length < 3 || f.lv === 'cabin' || f.lv === 'under' || f.lip || f.it) continue;
      const v = f.v.map(q => [q[0], q[1], q[2] - dwl]);
      for (const q of v) { if (q[0] < x0) x0 = q[0]; if (q[0] > x1) x1 = q[0]; if (q[1] < y0) y0 = q[1]; if (q[1] > y1) y1 = q[1]; if (q[2] > z1) z1 = q[2]; }
      faces.push({ v, mat: mk(f.mat), b: f.b || 0, db: f.db || 0 }); }
    const H = G.HULL[T.table] || {};
    B = { type, paint: p, paints, label: H.label || type, table: T.table, rig: T.global, faces, dwl, loa: H.loa, beam: H.beam, draft: H.draft, cls: H.cls, gun: H.gun,
      deck: T.deck(R), board: T.board(R), note: T.note, inlet: INLET[type], bbox: { x0: r3(x0), x1: r3(x1), y0: r3(y0), y1: r3(y1), z1: r3(z1) }, faceCount: faces.length };
    CACHE.set(key, B); return B;
  }
  // a hull at (cx, cy) in the harbour, its bow toward heading hd (radians, 0 = east), in float group gid
  function place(type, paint, cx, cy, hd, gid) {
    const B = build(type, paint), c = Math.cos(hd), s = Math.sin(hd), toH = (q) => [cx + q[0] * s + q[1] * c, cy - q[0] * c + q[1] * s, q[2]], out = [];
    for (const f of B.faces) out.push({ v: f.v.map(toH), mat: f.mat, b: f.b, db: f.db, uv: null, tex: null, part: 'boat', grp: gid, lay: 1 });
    return { faces: out, B, toH };
  }
  // shore power and water, from the pedestal to the inlet: a black cable and a blue hose draped to the deck, riding with the boat
  function feed(gid, sock, tap, inlet, fill, deckZ) {
    const out = [], lowC = Math.min(sock[2], inlet[2]), lowH = Math.min(tap[2], fill[2]), mid = (a, b) => (a[2] + b[2]) / 2;
    G.at('cable', gid, 1); MG.sagLine(out, sock, inlet, Math.max(0.12, mid(sock, inlet) - Math.min(lowC, deckZ + 0.05)), 12, 'blk', 0.018);
    G.at('hose', gid, 1); MG.sagLine(out, tap, fill, Math.max(0.12, mid(tap, fill) - Math.min(lowH, deckZ + 0.08)), 12, 'binB', 0.016);
    G.CTX.grp = 0; const len = (a, b) => Math.hypot(b[0] - a[0], b[1] - a[1], b[2] - a[2]);
    return { faces: out, cable_m: r3(len(sock, inlet) * 1.25 + 0.5), hose_m: r3(len(tap, fill) * 1.25 + 0.5) };
  }
  root.MarinaBoats = { TYPES, INLET, PROPS, ramp5, loaded, build, place, feed, cache: CACHE };
})(typeof globalThis !== 'undefined' ? globalThis : window);
