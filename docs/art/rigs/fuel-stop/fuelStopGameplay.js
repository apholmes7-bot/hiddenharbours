/* NMC H9: sibling export contract. The four delivered files remain unchanged. */
(function (root) {
  'use strict';
  const VERSION = '1.0.0', PPU = 32, F = root.FuelStop;
  if (!F) throw new Error('Load fuelStopRig.js before fuelStopGameplay.js');
  const IDS = {
    store: 'station.nmc_store', canopy: 'station.nmc_canopy', truck: 'station.nmc_semi_lane',
    pylon: 'station.nmc_pylon', propane: 'station.nmc_propane', air: 'station.nmc_air',
    vacuum: 'station.nmc_vacuum', bench: 'station.nmc_bench', firewood: 'station.nmc_firewood',
    iceBox: 'prop.nmc_ice_box', dumpster: 'prop.nmc_dumpster', lampPole: 'prop.nmc_lamp_pole',
    stopSign: 'prop.nmc_stop_sign', picnic: 'prop.nmc_picnic', wheelStop: 'prop.nmc_wheel_stop',
    bin: 'prop.nmc_bin', barSign: 'prop.nmc_bar_sign'
  };
  const KEYS = F.list();
  const PIECES = Object.fromEntries(KEYS.map(key => [key, {id: IDS[key], name: F.KINDS[key].label,
    defType: 'SetPieceDef', scene: 'keyscene.nmc_fuel_stop'}]));
  const turn = p => [-p[0], -p[1], p[2] || 0];
  function footprint(key) {
    const f = F.footprint(key);
    return [[[-f[0], -f[2]], [-f[1], -f[2]], [-f[1], -f[3]], [-f[0], -f[3]]]];
  }
  const spec = (key, options) => {
    if (!PIECES[key]) throw new Error('Unknown fuel stop piece: ' + key);
    return Object.assign({}, options || {});
  };
  function verbs(key, options) {
    const result = F.stations(key, options).map(q => Object.assign({}, q, {
      at: turn(q.at), stand: turn(q.stand)
    }));
    const extra = {pylon: ['prices_read', 'read'], propane: ['buy_propane', 'buy'], iceBox: ['buy_ice', 'buy']}[key];
    if (extra) result.push({id: extra[0], verb: extra[1], at: [0, 0, 0], stand: [0, 0, 0]});
    return result;
  }
  const cellCache = new Map();
  function cells(key, options) {
    const cacheKey = key + JSON.stringify(options);
    if (!cellCache.has(cacheKey)) cellCache.set(cacheKey, Array.from({length: 8}, (_, dir) => {
      const f = F.frame(key, dir, options);
      return {dir, w: f.W, h: f.H, pivot: [f.C.ox, f.C.oy]};
    }));
    return cellCache.get(cacheKey);
  }
  function gameplay(key, options) {
    const o = spec(key, options), p = PIECES[key];
    const vertices = F.frame(key, 4, o).mdl.faces.flatMap(f => f.v);
    const extent = axis => [Math.min(...vertices.map(v => v[axis])), Math.max(...vertices.map(v => v[axis]))];
    const anchors = {};
    for (const v of verbs(key, o)) { anchors[v.id] = v.at; anchors[v.id + '.stand'] = v.stand; }
    return {schema: 'hidden-harbours/fuel-stop-piece@1', generatedBy: 'FuelStopGameplay ' + VERSION,
      key, id: p.id, name: p.name, defType: p.defType, scene: p.scene,
      units: 'metres, model frame rotated by pi from FuelStop plan metres',
      bounds: {x: extent(0), y: extent(1), z: extent(2)},
      layer: 'ground', walk: 'around', mount: 'ground',
      colliders: /^(store|canopy|truck)$/.test(key) ? [] :
        footprint(key).map(pts => ({type: 'polygon', blocks: 'walk', pts})),
      facings: Array.from({length: 8}, (_, dir) => ({dir, showFaces: String(dir), bearingDeg: dir * 45,
        cameraSees: 'fuel stop piece'})), cells: cells(key, o), anchors, options: o,
      light: /^(store|canopy|truck|pylon|iceBox|lampPole)$/.test(key) ?
        {emitters: ['neon', 'led', 'glow'], when: 'FuelStop.levels(CoastalPass.light.lampNeed(sky))'} : null,
      notes: 'Footprint bounds are not walk blockers: canopy lanes and store doors must remain open. Collision and light placement are verified in the scene phase.'};
  }
  root.FuelStopGameplay = {VERSION, PPU, KEYS, PIECES, spec, gameplay, verbs, turn,
    footprint,
    render: (key, dir, o) => F.render(key, dir, o)};
})(typeof globalThis !== 'undefined' ? globalThis : this);
