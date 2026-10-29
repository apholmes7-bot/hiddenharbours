// NMC roads (H4): G3's height and still water, read the way the game reads them (H3's decoder, tools/nmc-walls/g3.cjs).
// load(root): { at(x, y) the elevation PaintedHeightField gives, float32; stillZ(x, y) the still level of the texel under
// the point (NaN where its code is 0); wet(x, y, high) true where water stands over the ground at the tide `high`
// (water = max(tide, still), ADR 0046; at or below the tide counts, as the old roads' no-ford rule had it); the PNGs'
// sha256 }.
'use strict';
const path = require('path');
const g3 = require('../nmc-walls/g3.cjs');

const FOLDER = 'Assets/_Project/Data/Terrain/NineMileCreekGround';
const HEIGHT = FOLDER + '/NineMileCreekGround_Height.png', STILL = FOLDER + '/NineMileCreekGround_Still.png';
// NineMileCreekGround.Minimum / Maximum: a code's height is min + (max - min) * code / 65535
const MIN = -6, MAX = 6;

function load(root = '.') {
  const h = g3.decode(path.join(root, HEIGHT)), s = g3.decode(path.join(root, STILL));
  const fld = g3.field(h.codes), W = g3.W, H = g3.H;
  // the PNG's rows run from the north: row r covers y 280 - (r + 1) / 2 .. 280 - r / 2
  const texelOf = (x, y) => {
    const c = Math.floor((x + 380) * 2), r = Math.floor((280 - y) * 2);
    return c < 0 || r < 0 || c >= W || r >= H ? -1 : r * W + c;
  };
  const stillZ = (x, y) => { const i = texelOf(x, y); return i >= 0 && s.codes[i] ? MIN + (MAX - MIN) * s.codes[i] / 65535 : NaN; };
  const wet = (x, y, high) => { const z = fld.at(x, y), w = stillZ(x, y); return z <= high || z < w; };
  return { at: fld.at, stillZ, wet, texelOf, heightSha256: h.pixelsSha256, stillSha256: s.pixelsSha256 };
}

module.exports = { load, HEIGHT, STILL };
