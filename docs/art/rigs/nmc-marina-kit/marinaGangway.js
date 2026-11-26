/* NMC marina kit - the water-side gangway's top-down read (adds to globalThis.WharfGeo2).
   The shared gangway body (wharfRig2.fam.js rampBody) shades its treads, deck plate and truss one band dark,
   so from straight above the whole span reads as one dark grating slab on dark water. The marina gangways
   re-band their aluminium faces after the body is built: lighter weathered treads over the dark deck plate
   (the gaps between treads keep the tread rhythm), hand and mid rails one band light so the sides read as
   two distinct lines, posts and top chords at the plain tone, the lower truss left dark under the walkway.
   Geometry, spans, landings and anchors are unchanged: only each face's band bias moves. */
(function (root) {
  'use strict';
  const G = root.WharfGeo2; if (!G || !G.gangwayFaces) throw new Error('marinaGangway.js: load wharfRig2.fam.js first');
  if (G.gangwayFaces.marinaLook) return;
  // rampBody's aluminium band biases -> the marina look. Keys are the body's own literals.
  const REBAND = new Map([
    [-0.85, 0.5],   // treads: weathered aluminium walking surface, one band light
    [-0.3, 0.5],    // hand and mid rails: light lines along both sides
    [-0.4, 0.0],    // truss top chord
    [-0.5, 0.0],    // rail posts
  ]);
  const base = G.gangwayFaces;
  function gangwayFaces(out, ...rest) {
    const start = out.length, r = base.call(this, out, ...rest);
    for (let i = start; i < out.length; i++) { const f = out[i]; if (f.mat === 'alum' && REBAND.has(f.b)) f.b = REBAND.get(f.b); }
    return r;
  }
  gangwayFaces.marinaLook = true;
  G.gangwayFaces = gangwayFaces;
})(globalThis);
