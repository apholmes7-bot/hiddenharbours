/* Bakes every v8 shoreline sheet + doc preview for both styles. run_script harness:
   (0,eval) rockIsoRig.js, shoreIsoKitRig2.js, _shoreHeroScene.js, then this, then SHORE_BAKE(...). */
globalThis.SHORE_BAKE = async function (o) {
  const { K, RockIso, dory, createCanvas, saveFile } = o;
  const bake = globalThis.SHORE_HERO(K, { createCanvas, dory, RockIso });
  const cvOf = cell => { const c = createCanvas(cell.w, cell.h);
    c.getContext('2d').putImageData(new ImageData(new Uint8ClampedArray(cell.buf), cell.w, cell.h), 0, 0); return c; };
  const put = (ctx, cell, x, y) => ctx.drawImage(cvOf(cell), x, y);
  const sheet = (w, h) => { const c = createCanvas(w, h); const x = c.getContext('2d'); x.imageSmoothingEnabled = false; return [c, x]; };
  const MATS = K.GROUND, FP = K.FRINGE_PIECES, CP = K.CLIFF_PIECES;

  for (const st of ['nat', 'gfx']) {
    const cv = bake(st);
    const out = createCanvas(cv.width * 2, cv.height * 2), oc = out.getContext('2d');
    oc.imageSmoothingEnabled = false; oc.drawImage(cv, 0, 0, cv.width * 2, cv.height * 2);
    await saveFile(`gallery/shore-explore/v8-hero-${st}.png`, out);

    let [c1, x1] = sheet(128, 192);
    MATS.forEach((m, i) => { for (let t = 0; t < 4; t++) put(x1, K.ground(m, { gx: t, gy: i * 3, seed: 5, style: st }), t * 32, i * 32); });
    await saveFile(`Art/Tilesets/ShorelineIso2/${st}/Ground.png`, c1);

    let [c2, x2] = sheet(384, 96);
    ['grass', 'marram', 'sand'].forEach((m, r) => FP.forEach((p, i) => put(x2, K.fringe(m, p, { gx: i, gy: r, seed: 5, style: st }), i * 32, r * 32)));
    await saveFile(`Art/Tilesets/ShorelineIso2/${st}/Fringe.png`, c2);

    let [c3, x3] = sheet(320, 96);
    ['cap', 'mid', 'toe'].forEach((b, r) => {
      CP.forEach((p, i) => put(x3, K.cliff(p, { band: b, gx: i, gy: r * 32, seed: 5, style: st }), i * 32, r * 32));
      if (b === 'toe') put(x3, K.cliff('faceS', { band: 'toe', gx: 9, gy: 64, seed: 5, feature: 'cave', style: st }), 9 * 32, r * 32);
    });
    await saveFile(`Art/Tilesets/ShorelineIso2/${st}/Cliff.png`, c3);

    let [c4, x4] = sheet(288, 64);
    ['cap', 'toe'].forEach((b, r) => CP.forEach((p, i) => put(x4, K.dune(p, { band: b, gx: i, seed: 5, style: st }), i * 32, r * 32)));
    await saveFile(`Art/Tilesets/ShorelineIso2/${st}/Dune.png`, c4);

    let [c5, x5] = sheet(160, 32);
    K.CONTACT_PIECES.forEach((p, i) => put(x5, K.contact(p, { style: st }), i * 32, 0));
    await saveFile(`Art/Tilesets/ShorelineIso2/${st}/Contact.png`, c5);

    let [c6, x6] = sheet(6 * 64 * 2, 64 * 2); x6.scale(2, 2);
    MATS.forEach((m, i) => { for (let a = 0; a < 2; a++) for (let b = 0; b < 2; b++)
      put(x6, K.ground(m, { gx: a + i * 5, gy: b, seed: 5, style: st }), i * 64 + a * 32, b * 32); });
    await saveFile(`gallery/shore-explore/v8-mats-${st}.png`, c6);

    const pairs = [['grass', 'sand'], ['marram', 'sand'], ['sand', 'shingle'], ['sand', 'ripple'], ['grass', 'shelf']];
    let [c7, x7] = sheet(pairs.length * 64 * 2, 64 * 2); x7.scale(2, 2);
    pairs.forEach(([top, bot], i) => { for (let a = 0; a < 2; a++) {
      put(x7, K.ground(top, { gx: a + i * 4, gy: 0, seed: 5, style: st }), i * 64 + a * 32, 0);
      put(x7, K.ground(bot, { gx: a + i * 4, gy: 1, seed: 5, style: st }), i * 64 + a * 32, 32);
      put(x7, K.fringe(top, 'edN', { gx: a + i * 4, gy: 1, seed: 5, style: st }), i * 64 + a * 32, 32); } });
    await saveFile(`gallery/shore-explore/v8-fringe-${st}.png`, c7);

    let [c8, x8] = sheet(232 * 3, 178 * 3); x8.scale(3, 3);
    ['faceS', 'cornSW', 'cornSE'].forEach((p, i) => put(x8, K.column(p, 5, { seed: 5, style: st }), 4 + i * 36, 8));
    put(x8, K.column('faceS', 5, { seed: 9, feature: 'cave', style: st }), 112, 8);
    put(x8, K.column('diagSE', 5, { seed: 5, style: st }), 148, 8);
    ['cornSW', 'faceS'].forEach((p, i) => {
      put(x8, K.dune(p, { band: 'cap', gx: i, seed: 5, style: st }), 190 + i * 16, 62);
      put(x8, K.dune(p, { band: 'toe', gx: i, seed: 5, style: st }), 190 + i * 16, 94);
    });
    await saveFile(`gallery/shore-explore/v8-forms-${st}.png`, c8);
  }

  // stacked A/B compare for review
  return true;
};
