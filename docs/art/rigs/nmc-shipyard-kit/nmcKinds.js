/* Nine Mile Creek wharf pass: the stage's extra kinds (globalThis.NmcKinds), loaded after StpPano and the kits.

   1. The working fleet as faces. MarinaBoats.TYPES gains the fleet's own hulls, read from their rigs' face lists and palettes
      (LobsterBoatVariantsIso.faces(v) and paintRamps(p); CapeIslanderIso.faces() and palette({scheme})). Nothing a rig draws
      changes. A hull so registered goes into a harbour's G-buffer as a float group, lit by the sky with the quays, cut at the
      waterline by the water, and the yard (ShipyardGeo2.landHull) can stand it on blocks.
   2. Pano.KINDS.harbour: a WharfRig2Kit / MarinaKit harbour as one sprite. Origin (OX, 0) in region metres, kit z = game z + 2.2.
   3. Pano.KINDS.nmcPiece: a piece of the NMC set pieces kit (NmcSetPieces), lit by CoastalPass.light.
   4. Pano.KINDS.flat: an old-key rig's own render (dory, punt, console skiff, the road vehicles), graded by the sky.        */
(function (root) {
  'use strict';
  const P = root.StpPano, MB = root.MarinaBoats, G = root.WharfGeo2;
  if (!P || !MB || !G) throw new Error('nmcKinds.js: load StpPano, the wharf kit and the marina kit first');
  const r3 = (v) => Math.round(v * 1000) / 1000;

  // ---- 1. the fleet's hulls -------------------------------------------------------------------------------------------
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

  // ---- 0. the scene hooks the pass 6 stage calls (afterGround, afterZones, afterPaint, afterPlacement), installed once
  { const PP = P.Pano.prototype;
    for (const [step, hook] of [['buildGround', 'afterGround'], ['buildZones', 'afterZones'], ['buildPaint', 'afterPaint'], ['buildPlacement', 'afterPlacement']]) {
      const f = PP[step]; if (!f || f.__hook) continue;
      const g = async function () { await f.call(this); if (this.def && this.def[hook]) await this.def[hook](this); }; g.__hook = true; PP[step] = g; } }

  // ---- 2. harbours ----------------------------------------------------------------------------------------------------
  P.Pano.KINDS.harbour = {
    build(sp, o) {
      const W2 = root.WharfRig2, fr = W2.frame('harbour:' + sp.harbour, { dir: 0, tide: o.tide + 2.2, frame: 0, wind: { w: 0.3, gust: 0.3, dir: 1 }, season: o.season === 'autumn' ? 'autumn' : 'summer', stage: sp.stage || 'weathered', variant: sp.variant | 0 });
      const Z = new Float32Array(fr.w * fr.h).fill(NaN); for (let i = 0; i < Z.length; i++) if (fr.gb.a[i]) Z[i] = fr.gb.Z[i] - 2.2;
      return { type: 'wharf', ext: P.Pano.KINDS.harbour, fr, Z, w: fr.w, h: fr.h, px: fr.px, py: fr.py, OX: sp.at[0] };
    },
    light(F, sky, sp, o) { const MS = root.MarinaStage; return MS ? MS.relight(F.fr, sky, { lampK: o.need || 0 }) : root.WharfRig2.relight(F.fr, sky); },
  };
  // ---- 3. the NMC set pieces --------------------------------------------------------------------------------------------
  P.Pano.KINDS.nmcPiece = {
    build(sp, o) { const SP = root.NmcSetPieces, fr = SP.frame(sp.piece, sp.dir | 0, Object.assign({}, sp.opts || {}));
      const Z = new Float32Array(fr.W * fr.H).fill(NaN); for (let i = 0; i < Z.length; i++) if (fr.gb.a[i]) Z[i] = fr.gb.Z[i];
      return { type: 'piece', ext: P.Pano.KINDS.nmcPiece, fr, Z, w: fr.W, h: fr.H, px: fr.cell.px, py: fr.cell.py }; },
    light(F, sky, sp, o) { return root.NmcSetPieces.relight(F.fr, sky, { lit: sp.emits ? (o.need || 0) : 0 }); },
  };
  // ---- 3b. a lamp's pool with no body of its own (the light a piece throws; the stage draws the pool and the glow) -----------
  P.Pano.KINDS.nmcLight = { build() { return { type: 'light', w: 0, h: 0, px: 0, py: 0 }; }, light() { return new Uint8ClampedArray(0); } };
  // ---- 4. old-key rigs, their own render graded by the sky ---------------------------------------------------------------
  const CROP = (rgba, w, h, px, py) => { let x0 = w, x1 = -1, y0 = h, y1 = -1; for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) if (rgba[(y * w + x) * 4 + 3]) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
    if (x1 < 0) return { w: 0, h: 0, px: 0, py: 0, rgba: new Uint8ClampedArray(0) }; const cw = x1 - x0 + 1, ch = y1 - y0 + 1, out = new Uint8ClampedArray(cw * ch * 4);
    for (let y = 0; y < ch; y++) out.set(rgba.subarray(((y + y0) * w + x0) * 4, ((y + y0) * w + x0 + cw) * 4), y * cw * 4); return { w: cw, h: ch, px: px - x0, py: py - y0, rgba: out }; };
  P.Pano.KINDS.flat = {
    build(sp) { const R = root[sp.rig]; if (!R) throw new Error('no rig ' + sp.rig); let a = R.render(sp.dir | 0, sp.opts || {}); if (a && a.rgba) a = a.rgba;
      const r = CROP(a, R.W, R.H, R.pivot.x, R.pivot.y); return Object.assign({ type: 'flat', ext: P.Pano.KINDS.flat }, r); },
    light(F, sky) { return P.tint(F.rgba, sky); },
  };
  root.NmcKinds = { LOB, addFleet };
})(typeof globalThis !== 'undefined' ? globalThis : this);
