/* Nine Mile Creek, the gas station, redrawn for key scenes part 2c: the bake (globalThis.NfsBake2c). The wharf return's nfsBake.js
   with the redraw's load list and part 1's ground. Builds one tile of the frame and renders its skies.
     (0, eval)(await readFile('hh-nmc-key-scenes/fuel-stop/nfsBake2c.js')); await NfsBake2c.load({ readFile, readFileBinary });
     const P = await NfsBake2c.build(NfsBake2c.tile(0, 0)), R = await NfsBake2c.render(P, 'noon');   // R.rgba, R.w, R.h
   The frame (site metres, region = site + (-174, +87)) is x -12..64, u 0..69: 2432 x 2208 px, in 4 x 6 tiles of 608 x 368 (a tile builds
   and bakes a sky well inside a browser task's time; nfsBakePage2c.html bakes the whole frame). */
(function (root) {
  'use strict';
  const STP = 'hh-st-peters-key-scenes-return/', K2 = 'hh-nmc-key-scenes/', G1 = 'hh-nmc-ground-part1/tools/', PKG1 = 'export/HH-nmc-ground-part-1-2026-09-30/';
  root.NfsLoadList2c = ['export/road-path-kit-v4/roadPathRig4.js', K2 + 'fuel-stop/fuelStopRig.js', STP + 'pass6/fsKinds.js', G1 + 'nmcHeightTex.js', G1 + 'nmcGround.js', K2 + 'tools/nmcSceneViews.js', K2 + 'fuel-stop/nmcFuelStop.js'];
  const WEATHER = { clear: { cloud: 0.12 } };
  const PRESETS = [
    { id: 'noon', label: 'Noon', t: 12.0, weather: 'clear', season: 'summer' },
    { id: 'dusk', label: 'Dusk', t: 20.6, weather: 'clear', season: 'summer' },
    { id: 'night', label: 'Night', t: 22.5, weather: 'clear', season: 'summer' },
  ];
  const FRAME = { x0: -12, x1: 64, uTop: 69, uBot: 0 }, COLS = 4, ROWS = 6, TW = 19, TH = 11.5, SE = Math.sin(40 * Math.PI / 180), CE = Math.cos(40 * Math.PI / 180);
  async function load(io) {
    const ev = (src, f) => (0, eval)(src + '\n//# sourceURL=' + f);
    if (!root.__nfs2cLoaded) {
      for (const f of ['tools/stpLoadList.js', 'pass2/stpPanoLoadList.js']) ev(await io.readFile(STP + f), f);
      const files = [].concat(root.StpLoadList, root.StpPanoLoadList.filter(f => !/eastEnd\.js$/.test(f)), root.NfsLoadList2c), srcs = await Promise.all(files.map(f => io.readFile(f)));
      files.forEach((f, i) => ev(srcs[i], f)); root.__nfs2cLoaded = true; }
    if (!root.__nfs2cGround) { const T = root.NmcHeightTex, plan = JSON.parse(await io.readFile(PKG1 + 'ground/plan.json'));
      const H = await T.fromBytes(await (await io.readFileBinary(PKG1 + 'ground/NineMileCreekSeabed_HeightTex_plan.png')).arrayBuffer(), 'plan');
      const S = await T.fromBytes(await (await io.readFileBinary(PKG1 + 'ground/NineMileCreekSeabed_StillTex_plan.png')).arrayBuffer(), 'still');
      root.__nfs2cGround = { plan, E: (x, y) => H.E(x - 174, y + 87), S: (x, y) => S.E(x - 174, y + 87) }; }   // site metres to region metres
    const G = root.__nfs2cGround; root.NmcFuelStop.setGround(G.E, G.S, G.plan);
  }
  // tile (c, r): its frame, the floor's rows reaching far enough north and south for the ground and the sprites that stand in it
  function tile(c, r) { const x0 = FRAME.x0 + c * TW, uTop = FRAME.uTop - r * TH, uBot = uTop - TH;
    return { x0, x1: x0 + TW, uTop, uBot, padBelow: 8, yMin: (uBot - CE * 6.5) / SE - 4, yMax: (uTop + CE * 1.2) / SE + 4 }; }
  async function build(frame, onStep) { const def = Object.assign({}, root.NmcFuelStop, { frame: Object.assign({}, root.NmcFuelStop.frame, frame || {}) });
    const P = new root.StpPano.Pano(def, {}); for (const st of P.steps()) { if (onStep) onStep(st.label); await st.run(); } return P; }
  async function render(P, id, extra) {
    const pr = PRESETS.find(p => p.id === id); if (!pr) throw new Error('NfsBake2c: no preset ' + id);
    const o = Object.assign({ t: pr.t, weather: WEATHER[pr.weather], season: pr.season, variant: 'today', tide: 0 }, extra || {});
    P.errors = []; const R = await P.render(o); R.preset = pr; return R;
  }
  root.NfsBake2c = { PRESETS, WEATHER, FRAME, COLS, ROWS, TW, TH, load, tile, build, render };
})(typeof globalThis !== 'undefined' ? globalThis : this);
