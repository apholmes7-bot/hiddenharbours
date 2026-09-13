// Eye-only pass05/pass06 invariants and descriptive raster evidence. Node stdlib only.
const fs = require('fs'), path = require('path'), vm = require('vm');
const assert = require('assert/strict'), crypto = require('crypto');
const { loadStudy } = require('./load-study.cjs');
const { raster } = require('./sources/face-render.cjs');
const dir = __dirname, output = path.join(dir, 'review', 'eyes');
const plain = value => JSON.parse(JSON.stringify(value));
const equal = (a, b, message) => assert.deepEqual(plain(a), plain(b), message);
const eyeInks = new Set([1, 2, 3, 10]);

function loadPair() {
  const contexts = [loadStudy().context, loadStudy().context];
  vm.runInContext(fs.readFileSync(path.join(dir, 'sources/face-rig-pass05.js'), 'utf8'), contexts[0]);
  const studies = contexts.map(c => c.CharacterHeadStudy);
  const cast = Array.from(contexts[1].CastViewerEngine.cast);
  const characters = contexts.map((c, pass) => Object.fromEntries(cast.map(key =>
    [key, c.CastViewerEngine.create(key, { headStudy: studies[pass] })])));
  const heads = characters.map((all, pass) => Object.fromEntries(cast.map(key =>
    [key, studies[pass].createHead(all[key].headBuild, [0, 0, 0])])));
  return { contexts, studies, cast, characters, heads };
}

function renderHead(pair, pass, key, { ppm, angle = 0, phase = [0, 0], expr = 'neutral', gaze = [0, 0], lid, tagEyes = false }) {
  const H = pair.studies[pass], character = pair.characters[pass][key];
  const state = H.life(0, { expr, gaze, ...(lid === undefined ? {} : { lid }) });
  const colours = Array.from(character.colours), size = Math.ceil(ppm * .76);
  // Separate the two eyes in the diagnostic buffer without changing any rendered colour.
  if (tagEyes) for (const ink of eyeInks) colours[ink + 16] = colours[ink];
  return raster(pair.heads[pass][key], character.mats, {
    w: size, h: size, cx: size / 2 + phase[0], cy: size / 2 + phase[1],
    scale: ppm, angle, elev: 40, mode: 'proposal', outline: true,
    surface: (u, v) => {
      const ink = H.sample(u, v, state, character.headBuild);
      return tagEyes && u >= .5 && eyeInks.has(ink) ? ink + 16 : ink;
    }, surfaceColours: colours
  });
}

function sourceHashes() {
  const files = ['sources/face-rig-pass05.js', 'sources/face-rig.js', 'sources/headIsoRig3.js',
    'sources/eyeIsoRig.js', 'sources/characterIsoRig6.js', 'sources/characterIsoRig6.hands.js',
    'sources/characterIsoRig7.js', 'sources/proposal.js', 'sources/face-render.cjs',
    'boot-segment-fix.cjs', 'load-study.cjs', 'cast-engine.js', 'character-finish.js',
    'character-finish.json', 'check-eye-refinement.cjs', 'review-eyes.cjs'];
  return Object.fromEntries(files.map(file => [file, crypto.createHash('sha256')
    .update(fs.readFileSync(path.join(dir, file), 'utf8').replaceAll('\r\n', '\n')).digest('hex')]));
}

function run() {
  const pair = loadPair(), [before, after] = pair.studies;
  const expressions = Object.keys(after.EXPRESSIONS);
  equal(before.EXPRESSIONS, after.EXPRESSIONS, 'Expression intents changed');
  equal(before.BROWS, after.BROWS, 'Brow customization definitions changed');
  equal(before.AUTO, after.AUTO, 'Animation expression selection changed');
  let geometryVertices = 0, geometryFaces = 0, stateSamples = 0, poseStateSamples = 0;
  for (const key of pair.cast) {
    const a = pair.characters[0][key], b = pair.characters[1][key];
    for (const field of ['build', 'headBuild', 'bind', 'faces', 'mats'])
      equal(a[field], b[field], key + ': eye refinement changed ' + field);
    equal(pair.heads[0][key], pair.heads[1][key], key + ': head geometry, topology or UVs changed');
    for (const face of pair.heads[1][key]) {
      geometryFaces++; geometryVertices += face.v.length;
      assert(face.v.every(p => p.every(Number.isFinite)), key + ': nonfinite head');
    }
    assert.equal(a.colours.length, b.colours.length, 'Face palette indices changed');
    for (let i = 0; i < a.colours.length; i++) if (!eyeInks.has(i))
      assert.equal(a.colours[i], b.colours[i], key + ': non-eye palette index ' + i + ' changed');
  }
  for (const expr of expressions) for (const seed of [0, 17, 48, 296]) for (let i = 0; i < 600; i++) {
    const options = { expr, seed, talk: i % 2 === 0 }, t = i * .05;
    equal(before.life(t, options), after.life(t, options), 'Blink, gaze, expression or speech timing changed');
    const quiet = after.life(t, { expr, seed }), speaking = after.life(t, { expr, seed, talk: true });
    for (const field of ['brow', 'raise', 'lid', 'gaze']) equal(quiet[field], speaking[field], 'Speech moved ' + field);
    stateSamples++;
  }
  for (const anim of Object.keys(pair.contexts[1].CastViewerEngine.animations))
    for (const u of [0, .25, .5, .75, 1]) for (const talk of [false, true]) {
      equal(before.poseState(anim, u, { talk, seed: 17, loop: 2 }),
        after.poseState(anim, u, { talk, seed: 17, loop: 2 }), 'Clip face intent changed');
      poseStateSamples++;
    }

  const resolution = 192;
  function mask(H, state, build = {}) {
    const result = new Uint8Array(resolution * resolution);
    for (let y = 0; y < resolution; y++) for (let x = 0; x < resolution; x++)
      result[y * resolution + x] = H.sample((x + .5) / resolution, (y + .5) / resolution, state, build);
    return result;
  }
  const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
  assert.equal(new Set(expressions.map(expr => hash(mask(after, after.life(0, { expr, gaze: [0, 0] }))))).size,
    8, 'Expression surface masks collapsed');
  const speech = new Map();
  for (let i = 0; i < 70; i++) {
    const state = after.life(i / 62, { talk: true, gaze: [0, 0] });
    speech.set(state.mouth, hash(mask(after, state)));
  }
  assert.equal(speech.size, 4); assert.equal(new Set(speech.values()).size, 4, 'Speech mouths collapsed');
  let gazeDirections = 0, closedShapes = 0;
  const blinkStages = [0, .25, .5, .75, .86, .88, 1], blinkMasks = [];
  for (const shape of ['round', 'wide', 'sharp', 'narrow', 'droop']) {
    const build = { eyeShape: shape, browShape: 'none' };
    const closed = mask(after, after.life(0, { lid: 1, gaze: [0, 0] }), build);
    assert(!closed.some(ink => [2, 3, 10].includes(ink)), shape + ': closed blink leaks eye material');
    closedShapes++;
    const stages = blinkStages.map(lid => mask(after, after.life(0, { lid, gaze: [0, 0] }), build));
    for (const side of [0, 1]) {
      const onSide = i => Number(i % resolution >= resolution / 2) === side;
      for (let stage = 0; stage < stages.length; stage++) {
        let marks = 0, opening = 0;
        for (let i = 0; i < stages[stage].length; i++) if (onSide(i)) {
          if (eyeInks.has(stages[stage][i])) marks++;
          if ([2, 3, 10].includes(stages[stage][i])) opening++;
        }
        assert(marks, shape + ': eye marks disappeared during staged blink');
        blinkMasks.push({ Shape: shape, Side: side, Lid: blinkStages[stage], EyeMarkPixels: marks, OpeningPixels: opening });
      }
      let overlap = 0;
      for (let i = 0; i < stages[4].length; i++)
        if (onSide(i) && eyeInks.has(stages[4][i]) && eyeInks.has(stages[5][i])) overlap++;
      assert(overlap, shape + ': last open lid and closed dash became spatially disjoint');
    }
    const masks = [[0, 0], [-1, 0], [1, 0], [0, -1], [0, 1]].map(gaze =>
      mask(after, { gaze, lid: 0, mouth: 'neutral' }, build));
    for (const moved of masks.slice(1)) for (let i = 0; i < moved.length; i++)
      assert.equal(eyeInks.has(moved[i]), eyeInks.has(masks[0][i]), shape + ': gaze moved the socket');
    for (const side of [0, 1]) {
      function centroid(bytes) {
        let x = 0, y = 0, count = 0;
        for (let i = 0; i < bytes.length; i++) if (bytes[i] === 10 && Number(i % resolution >= resolution / 2) === side) {
          count++; x += i % resolution; y += Math.floor(i / resolution);
        }
        assert(count, shape + ': pupil vanished from the surface mask');
        return [x / count, y / count];
      }
      const p = masks.map(centroid);
      assert(p[1][0] < p[0][0] && p[2][0] > p[0][0], shape + ': horizontal gaze lost');
      assert(p[3][1] < p[0][1] && p[4][1] > p[0][1], shape + ': vertical gaze lost');
      gazeDirections += 4;
    }
  }

  const matrix = { Densities: [64, 32], Headings: [0, 25, 45, 90, 270, 315, 335],
    PixelPhases: [[0, 0], [.5, 0], [0, .5], [.5, .5]], Expressions: ['neutral', 'smile', 'grit', 'weary', 'oh'] };
  const counters = () => ({ Renders: 0, EyeSamples: 0, ScleraWithoutIrisOrPupil: 0,
    VisibleOpeningWithoutPupil: 0, NoOpeningVisible: 0 });
  const reports = [counters(), counters()], rows = [];
  let clipped = 0, empty = 0, neutral32EyeSamples = 0;
  for (const key of pair.cast) for (const ppm of matrix.Densities) for (const angle of matrix.Headings)
    for (const phase of matrix.PixelPhases) for (const expr of matrix.Expressions) for (const pass of [0, 1]) {
      const rendered = renderHead(pair, pass, key, { ppm, angle, phase, expr, tagEyes: true });
      const eyes = [{ Lash: 0, Sclera: 0, Iris: 0, Pupil: 0 }, { Lash: 0, Sclera: 0, Iris: 0, Pupil: 0 }];
      for (const ink of rendered.featureBuffer) {
        const side = ink >= 16 ? 1 : 0, value = ink >= 16 ? ink - 16 : ink;
        const name = { 1: 'Lash', 2: 'Sclera', 3: 'Iris', 10: 'Pupil' }[value];
        if (name) eyes[side][name]++;
      }
      let pixels = 0, touchesEdge = false;
      for (let y = 0; y < rendered.h; y++) for (let x = 0; x < rendered.w; x++)
        if (rendered.pixels[(y * rendered.w + x) * 4 + 3]) {
          pixels++; if (x === 0 || y === 0 || x === rendered.w - 1 || y === rendered.h - 1) touchesEdge = true;
        }
      if (!pixels) empty++; if (touchesEdge) clipped++;
      const totals = reports[pass]; totals.Renders++;
      for (const eye of eyes) {
        totals.EyeSamples++;
        if (eye.Sclera && !eye.Iris && !eye.Pupil) totals.ScleraWithoutIrisOrPupil++;
        if (eye.Sclera + eye.Iris + eye.Pupil > 0 && !eye.Pupil) totals.VisibleOpeningWithoutPupil++;
        if (!eye.Sclera && !eye.Iris && !eye.Pupil) totals.NoOpeningVisible++;
        if (pass === 1 && ppm === 32 && expr === 'neutral' && [0, 25, 335].includes(angle)) {
          neutral32EyeSamples++;
          // Regression for the observed hollow eye corner at phase [.5,.5]. This
          // targets that neutral viewing subset; it is not a general art score.
          assert(!(eye.Sclera && !eye.Iris && !eye.Pupil),
            key + ': hollow neutral eye returned at 32 px/m, angle ' + angle + ', phase ' + phase);
        }
      }
      rows.push({ Pass: pass ? '06' : '05', Cast: key, PixelsPerMetre: ppm, Angle: angle, Phase: phase, Expression: expr, Eyes: eyes });
    }
  assert.equal(empty, 0, 'Empty head raster'); assert.equal(clipped, 0, 'Clipped head raster');
  const report = { Passed: true, Before: 'sources/face-rig-pass05.js', After: 'sources/face-rig.js',
    Cast: pair.cast, HeadFacesCompared: geometryFaces, HeadVerticesCompared: geometryVertices,
    IdentityBindWeightsTopologyAndUVsExact: true, NonEyePaletteExact: true,
    FaceStatePairs: stateSamples, ClipStatePairs: poseStateSamples, DistinctExpressions: 8,
    DistinctSpeechMouths: 4, GazeDirectionsChecked: gazeDirections, FullyClosedShapes: closedShapes,
    StagedBlinkMasks: blinkMasks,
    Neutral32FrontObliqueEyeSamples: neutral32EyeSamples, Neutral32ScleraOnlyEyes: 0,
    Matrix: matrix, RasterByPass: reports, EmptyRasters: empty, ClippedRasters: clipped,
    Limits: ['Per-eye pixel counts describe this finite sample matrix; they are not an attractiveness or approval score.',
      'No opening visible can be normal profile/hair/hat occlusion or a nearly closed expression. Inspect the labeled plates.',
      'No browser interaction, Unity camera/shader, animation contact or performance acceptance is established.'],
    SourceHashes: sourceHashes(), RasterSamples: rows };
  fs.mkdirSync(output, { recursive: true });
  fs.writeFileSync(path.join(output, 'eye-checks.json'), JSON.stringify(report, null, 2) + '\n');
  console.log(JSON.stringify({ Passed: true, Cast: pair.cast.length, HeadFacesCompared: geometryFaces,
    HeadVerticesCompared: geometryVertices, FaceStatePairs: stateSamples, ClipStatePairs: poseStateSamples,
    GazeDirectionsChecked: gazeDirections, Neutral32FrontObliqueEyeSamples: neutral32EyeSamples,
    Neutral32ScleraOnlyEyes: 0, RasterByPass: reports, EmptyRasters: empty, ClippedRasters: clipped,
    Report: path.join(output, 'eye-checks.json') }));
}

module.exports = { loadPair, renderHead, sourceHashes, output };
if (require.main === module) run();
