// Labeled pass05/pass06 eye plates, sampled at real density and enlarged without smoothing.
const fs = require('fs'), path = require('path');
const { png } = require('./sources/face-render.cjs');
const { loadPair, renderHead, sourceHashes, output } = require('./check-eye-refinement.cjs');
const pair = loadPair(), plates = [];
fs.mkdirSync(output, { recursive: true });
const font = {
 A:'01110 10001 10001 11111 10001 10001 10001', B:'11110 10001 10001 11110 10001 10001 11110',
 C:'01111 10000 10000 10000 10000 10000 01111', D:'11110 10001 10001 10001 10001 10001 11110',
 E:'11111 10000 10000 11110 10000 10000 11111', F:'11111 10000 10000 11110 10000 10000 10000',
 G:'01111 10000 10000 10111 10001 10001 01111', H:'10001 10001 10001 11111 10001 10001 10001',
 I:'111 010 010 010 010 010 111', J:'00111 00010 00010 00010 10010 10010 01100',
 K:'10001 10010 10100 11000 10100 10010 10001', L:'10000 10000 10000 10000 10000 10000 11111',
 M:'10001 11011 10101 10101 10001 10001 10001', N:'10001 11001 10101 10011 10001 10001 10001',
 O:'01110 10001 10001 10001 10001 10001 01110', P:'11110 10001 10001 11110 10000 10000 10000',
 Q:'01110 10001 10001 10001 10101 10010 01101', R:'11110 10001 10001 11110 10100 10010 10001',
 S:'01111 10000 10000 01110 00001 00001 11110', T:'11111 00100 00100 00100 00100 00100 00100',
 U:'10001 10001 10001 10001 10001 10001 01110', V:'10001 10001 10001 10001 10001 01010 00100',
 W:'10001 10001 10001 10101 10101 10101 01010', X:'10001 10001 01010 00100 01010 10001 10001',
 Y:'10001 10001 01010 00100 00100 00100 00100', Z:'11111 00001 00010 00100 01000 10000 11111',
 '0':'111 101 101 101 101 101 111', '1':'010 110 010 010 010 010 111',
 '2':'111 001 001 111 100 100 111', '3':'111 001 001 111 001 001 111',
 '4':'101 101 101 111 001 001 001', '5':'111 100 100 111 001 001 111',
 '6':'111 100 100 111 101 101 111', '7':'111 001 001 010 010 010 010',
 '8':'111 101 101 111 101 101 111', '9':'111 101 101 111 001 001 111',
 '.':'0 0 0 0 0 1 1', '-':'000 000 000 111 000 000 000', '/':'00001 00010 00010 00100 01000 01000 10000'
};
function canvas(w, h) {
  const pixels = new Uint8ClampedArray(w * h * 4);
  for (let i = 0; i < w * h; i++) pixels.set([237, 232, 219, 255], i * 4);
  return { pixels, w, h };
}
function label(target, text, x, y) {
  for (const letter of text.toUpperCase()) {
    const glyph = font[letter]?.split(' ');
    if (!glyph) { x += 4; continue; }
    glyph.forEach((row, j) => [...row].forEach((pixel, i) => {
      if (pixel === '1' && x + i < target.w && y + j < target.h)
        target.pixels.set([37, 58, 59, 255], ((y + j) * target.w + x + i) * 4);
    }));
    x += glyph[0].length + 1;
  }
}
function paste(target, source, ox, oy, zoom) {
  for (let y = 0; y < source.h * zoom; y++) for (let x = 0; x < source.w * zoom; x++) {
    const i = (Math.floor(y / zoom) * source.w + Math.floor(x / zoom)) * 4;
    if (source.pixels[i + 3]) target.pixels.set(source.pixels.subarray(i, i + 4), ((oy + y) * target.w + ox + x) * 4);
  }
}
function plate(name, rows, views, ppm, zoom, title) {
  const size = Math.ceil(ppm * .76), left = 96, top = 46, cellW = size * zoom + 8, cellH = size * zoom + 10;
  const board = canvas(left + cellW * views.length * 2, top + cellH * rows.length);
  label(board, title, 8, 7);
  label(board, '05 BEFORE / 06 REVISED - ' + ppm + ' PX/M - DISPLAY X' + zoom, 8, 20);
  views.forEach((view, column) => [0, 1].forEach(pass =>
    label(board, view.label + ' ' + (pass ? '06' : '05'), left + (column * 2 + pass) * cellW + 4, 35)));
  rows.forEach((row, y) => {
    label(board, row.label, 5, top + y * cellH + 8);
    views.forEach((view, x) => [0, 1].forEach(pass => {
      const rendered = renderHead(pair, pass, view.options?.key || row.key, { ppm, ...row.options, ...view.options });
      paste(board, rendered, left + (x * 2 + pass) * cellW, top + y * cellH, zoom);
    }));
  });
  png(board, path.join(output, name));
  plates.push({ File: name, Title: title, PixelsPerMetre: ppm, DisplayZoom: zoom, Rows: rows, Views: views });
}
const selected = ['fisher', 'ginny', 'deckboss', 'girl'];
const names = key => pair.characters[1][key].build.label.toUpperCase();
const castRows = pair.cast.map(key => ({ key, label: names(key) }));
plate('owner-eyes-64.png', selected.map(key => ({ key, label: names(key) })),
  [0, 25, 335].map(angle => ({ label: String(angle), options: { angle, phase: [.5, 0] } })),
  64, 3, 'EYE REFINEMENT - SAME FACE / POSE / SCALE');
plate('eye-phase-risk-32.png', selected.map(key => ({ key, label: names(key) })),
  [25, 335].flatMap(angle => [0, .5].map(y =>
    ({ label: angle + ' Y' + y, options: { angle, phase: [.5, y] } }))),
  32, 4, 'NEUTRAL PIXEL PHASE - X0.5 / VERTICAL Y0 OR Y0.5');
for (const ppm of [64, 32]) {
  for (const phaseX of [0, .5]) plate('eye-turns-' + ppm + '-phase-' + phaseX + '.png', castRows,
    [0, 25, 45, 90, 270, 315, 335].map(angle => ({ label: String(angle), options: { angle, phase: [phaseX, 0] } })),
    ppm, ppm === 64 ? 2 : 4, 'ALL CAST EYES - HORIZONTAL PIXEL PHASE ' + phaseX);
  plate('eye-expressions-' + ppm + '.png', Object.keys(pair.studies[1].EXPRESSIONS).map(expr =>
    ({ key: 'fisher', label: expr, options: { expr } })),
    selected.map(key => ({ label: names(key), options: { phase: [.5, 0], angle: 25, key } })),
    ppm, ppm === 64 ? 2 : 4, 'EYE EXPRESSIONS - 25 DEG');
  plate('eye-blink-' + ppm + '.png', selected.map(key => ({ key, label: names(key) })),
    [0, .25, .5, .75, .86, .88, 1].map(lid => ({ label: String(lid), options: { lid, phase: [.5, 0] } })),
    ppm, ppm === 64 ? 2 : 4, 'STAGED BLINK - LID CLOSURE 0 TO 1');
}
const intro = 'Each pair shows preserved pass05 before and revised pass06. Head geometry, identity, clothing materials, camera and crop are shared. The head is sampled at the labeled pixels per metre, then enlarged with nearest-neighbour display only. Horizontal phase shifts the raster sampling grid by half a pixel; it does not change gaze.';
const html = '<!doctype html><html lang="en"><meta charset="utf-8"><title>Eye refinement 05 / 06</title><style>body{font:16px system-ui;margin:24px;background:#ede8db;color:#263e42}p{max-width:95ch}img{image-rendering:pixelated;max-width:100%;height:auto}section{margin:30px 0}a{color:#285f63}</style><h1>Eye refinement 05 / 06</h1><p>' + intro + '</p>' + plates.map(p =>
  '<section><h2>' + p.Title + ' · ' + p.PixelsPerMetre + ' px/m</h2><a href="' + p.File + '"><img src="' + p.File + '" alt="' + p.Title + '; each pair is 05 before and 06 revised"></a></section>').join('') +
  '<p>Eye pixel counts in eye-checks.json are diagnostic observations, not an art score. Profile or hair/hat occlusion can hide an eye. These plates do not establish browser interaction, Unity rendering, gameplay contact or performance acceptance.</p></html>';
fs.writeFileSync(path.join(output, 'eye-comparison.html'), html);
fs.writeFileSync(path.join(output, 'eye-plates.json'), JSON.stringify({ Intro: intro, Plates: plates, SourceHashes: sourceHashes() }, null, 2) + '\n');
console.log(JSON.stringify({ Passed: true, Plates: plates.length, OwnerPlate: path.join(output, 'owner-eyes-64.png'), Review: path.join(output, 'eye-comparison.html') }));
