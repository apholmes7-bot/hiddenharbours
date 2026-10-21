/* tools/check-colours.cjs — how many colour slots a character needs, counted the two ways the game asked for.
     node tools/check-colours.cjs      writes reports/colours.{json,txt}
   (a) PAINTED MATERIALS per preset: every material name the mesh paints (face groups included), each shade variant its own
       slot: skin / skinD / stub, over / overD / overL, shirt / shirtD / collar, boot / bootL / sole / hose, hair / hairD ...
       and the fixed face colours ink, ink2, white, iris, brow, lid, lip, mouth.
   (b) THE MOST ANY CREATOR BUILD CAN NEED, both ways: painted materials, and distinct ramps + fixed colours. The material set
       depends only on garment + bottom (the body) and hat x hair style x beard x eye shape (the head); both halves are
       enumerated, their union is taken for every pairing, and the split is verified against whole builds. For ramps the
       colours are chosen so that no two ramp sources coincide (the worst case); a fixed colour is counted once per value
       (lid and lip are both skin ramp [1]). Age is not a creator control and changes no material. Colours are v9's, unchanged. */
'use strict';
(function () {
function run(C, H) {
  const O = C.OPTIONS, P = C.palettes, GM = C.GARMENTS, FISH = C.BUILDS.fisher;
  const rigSha = H.sha256('Art/characterIsoRig9.js'), poseSha = H.sha256('Art/characterIsoRig9.poses.js'), problems = [];
  const base = (o) => Object.assign({}, FISH, { preset:null, label:'colours' }, o || {});
  const HEADP = { head:1, nose:1, ear:1, hair:1, hat:1, hood:1, beard:1, face:1 };
  const shade = (b) => C.shadingContract(b).materials, M0 = shade(base()), COLOUR = ['skin','hair','eyes','outfit','shirt','hatCol','apronCol'];
  /* where each material's colour comes from: the colour axis that changes it, or a constant ramp */
  const src = {};
  for (const k of COLOUR) { const a = shade(base({ [k]:O[k][0] })), b = shade(base({ [k]:O[k][1] }));
    for (const m of Object.keys(a)) if (JSON.stringify(a[m]) !== JSON.stringify(b[m])) src[m] = k; }
  const CONST = { boot:'BOOT', bootL:'BOOT', sole:'BOOT', hose:'BOOT', brass:'BRASS', belt:'LEATHER' };
  for (const m of Object.keys(M0)) if (!src[m] && !M0[m].fixed) { if (!CONST[m]) problems.push('no ramp source for ' + m); src[m] = CONST[m]; }
  const FIXED = Object.keys(M0).filter((m) => M0[m].fixed).sort();
  /* a fixed colour's value key: lid and lip are the same value (skin ramp [1]); mouth is skin ramp [0] */
  const VALUE = { ink:'ink', ink2:'ink2', white:'white', iris:'iris', brow:'brow', lid:'skin[1]', lip:'skin[1]', mouth:'skin[0]' };
  for (const m of FIXED) if (!VALUE[m]) problems.push('fixed material ' + m + ' not classified');
  const painted = (b) => { const s = {}; for (const f of C.buildOf(b).mesh.faces) s[f.mat] = 1; return Object.keys(s).sort(); };
  const count = (mats, contentOf) => { const ramps = {}, fixedV = {}, fixedM = [];
    for (const m of mats) { if (M0[m] && M0[m].fixed) { fixedM.push(m); fixedV[VALUE[m]] = 1; } else ramps[contentOf ? contentOf(m) : src[m]] = 1; }
    return { ramps:Object.keys(ramps).sort(), fixed:fixedM, fixedValues:Object.keys(fixedV).sort() }; };

  /* ---- (a) the ten presets ---- */
  const presets = {};
  for (const p of C.CAST) { const B = C.buildOf(p), mats = painted(p), sh = C.shadingContract(p).materials;
    const byContent = count(mats, (m) => JSON.stringify(sh[m].ramp)), bySource = count(mats);
    const rampSrc = {}; for (const m of mats) if (!sh[m].fixed) (rampSrc[src[m]] = rampSrc[src[m]] || []).push(m);
    presets[p] = { label:C.BUILDS[p].label, paintedMaterials:mats.length, materials:mats, shadeVariants:rampSrc, fixedColours:bySource.fixed,
      distinctRamps:byContent.ramps.length, rampSources:bySource.ramps, distinctFixedValues:byContent.fixedValues.length,
      rampsPlusFixed:byContent.ramps.length + byContent.fixedValues.length, rampsPlusFixedMaterials:byContent.ramps.length + bySource.fixed.length }; }

  /* ---- (b) the creator space ---- */
  const GB = []; for (const g of O.garment) { if (GM[g].top) for (const bt of O.bottom) GB.push([g, bt]); else GB.push([g, 'trousers']); }
  const MB = {}, MH = {};
  for (const [g, bt] of GB) { const s = {}; for (const f of C.buildOf(base({ garment:g, bottom:bt })).mesh.faces) if (!HEADP[f.part]) s[f.mat] = 1; MB[g + '/' + bt] = Object.keys(s); }
  for (const hat of O.hat) for (const hs of O.hairStyle) for (const bd of O.beard) for (const es of O.eyeShape) { const s = {};
    for (const f of C.buildOf(base({ hat, hairStyle:hs, beard:bd, eyeShape:es })).mesh.faces) if (HEADP[f.part]) s[f.mat] = 1; MH[[hat, hs, bd, es].join('|')] = Object.keys(s); }
  /* the split holds for whole builds: every field at random, 600 builds plus the ten presets */
  let seed = 4242, split = 0, splitBad = 0; const rnd = () => { seed = (Math.imul(seed, 1103515245) + 12345) >>> 0; return seed / 4294967296; };
  const sample = C.CAST.map((p) => C.normBuild(p)); for (let i = 0; i < 600; i++) { const b = {}; for (const k of C.FIELDS) b[k] = O[k][Math.floor(rnd() * O[k].length)]; sample.push(C.normBuild(b)); }
  for (const b of sample) { split++; const g = GM[b.garment].top ? b.garment + '/' + b.bottom : b.garment + '/trousers';
    const u = {}; for (const m of MB[g].concat(MH[[b.hat, b.hairStyle, b.beard, b.eyeShape].join('|')])) u[m] = 1;
    if (Object.keys(u).sort().join() !== painted(b).join()) splitBad++; }
  if (splitBad) problems.push('material split fails on ' + splitBad + ' of ' + split + ' builds');
  let maxP = 0, maxR = 0, maxRM = 0, total = 0; const histP = {}, histR = {}, witP = [], witR = [];
  for (const [gb, mb] of Object.entries(MB)) for (const [hk, mh] of Object.entries(MH)) { total++;
    const u = {}; for (const m of mb) u[m] = 1; for (const m of mh) u[m] = 1; const mats = Object.keys(u), n = mats.length, c = count(mats), r = c.ramps.length + c.fixedValues.length, rm = c.ramps.length + c.fixed.length;
    histP[n] = (histP[n] || 0) + 1; histR[r] = (histR[r] || 0) + 1;
    const [hat, hs, bd, es] = hk.split('|'), [g, bt] = gb.split('/'), row = { garment:g, bottom: GM[g].top ? bt : null, hat, hairStyle:hs, beard:bd, eyeShape:es };
    if (n > maxP) { maxP = n; witP.length = 0; } if (n === maxP) witP.push(Object.assign({ materials:mats.sort() }, row));
    if (r > maxR) { maxR = r; witR.length = 0; } if (r === maxR) witR.push(Object.assign({ rampSources:c.ramps, fixedValues:c.fixedValues }, row));
    if (rm > maxRM) maxRM = rm; }
  const pMax = Math.max(...Object.values(presets).map((x) => x.paintedMaterials)), pMin = Math.min(...Object.values(presets).map((x) => x.paintedMaterials));
  const json = { tool:'check-colours', rig:rigSha, poses:poseSha, ok:false, problems,
    definitions:{ painted:'distinct material names on the bind mesh, face groups included: each shade variant and each fixed face colour its own slot',
      rampsPlusFixed:'distinct ramps (by content for a preset; by source for the creator maximum, colours chosen so no two sources coincide) + fixed colours counted once per value (lid = lip = skin ramp [1], mouth = skin ramp [0])',
      rampsPlusFixedMaterials:'the same, but every fixed face material its own slot' },
    rampSources:src, fixedValue:VALUE,
    presets, presetRange:{ painted:[pMin, pMax] },
    creator:{ combinations:total, splitChecked:split, maxPainted:maxP, maxPaintedCount:witP.length, maxPaintedWitnesses:witP.slice(0, 12), paintedHistogram:histP,
      maxRampsPlusFixed:maxR, maxRampsPlusFixedMaterials:maxRM, maxRampsPlusFixedCount:witR.length, maxRampsPlusFixedWitnesses:witR.slice(0, 12), rampsPlusFixedHistogram:histR } };
  json.ok = problems.length === 0;
  const L = [], pad = (s, n) => (s + '                    ').slice(0, n);
  L.push('check-colours — colour slots per character (v9 colours, unchanged)', 'rig ' + rigSha + '  poses ' + poseSha, '');
  L.push('(a) painted materials per preset (each shade variant and each fixed face colour its own slot)');
  L.push('    preset      painted  ramps  fixed(values)  ramps+fixed');
  for (const [p, x] of Object.entries(presets)) L.push('    ' + pad(p, 12) + pad(String(x.paintedMaterials), 9) + pad(String(x.distinctRamps), 7) + pad(x.fixedColours.length + ' (' + x.distinctFixedValues + ')', 15) + x.rampsPlusFixed);
  L.push('    range ' + pMin + '–' + pMax + ' painted; ' + Object.entries(presets).filter(([, x]) => x.paintedMaterials === pMax).map(([p]) => p).join(', ') + ' at ' + pMax + '.');
  for (const [p, x] of Object.entries(presets)) L.push('      ' + pad(p, 10) + x.materials.join(' '));
  L.push('', '(b) the most any creator build can need (' + total + ' garment+bottom x hat x hair x beard x eyes combinations; colours free)');
  L.push('    painted materials:           ' + maxP + '   (' + witP.length + ' combinations reach it, e.g. ' + [witP[0].garment, witP[0].bottom, witP[0].hat, witP[0].hairStyle, witP[0].beard, witP[0].eyeShape].filter(Boolean).join(' / ') + ')');
  L.push('      ' + witP[0].materials.join(' '));
  L.push('    distinct ramps + fixed colours: ' + maxR + '   (fixed counted once per value; ' + maxRM + ' with every fixed face material its own slot)');
  L.push('      e.g. ' + [witR[0].garment, witR[0].bottom, witR[0].hat, witR[0].hairStyle, witR[0].beard, witR[0].eyeShape].filter(Boolean).join(' / ') + ': ramps ' + witR[0].rampSources.join(' ') + ' + fixed ' + witR[0].fixedValues.join(' '));
  L.push('    painted histogram: ' + Object.entries(histP).map(([k, v]) => k + ':' + v).join(' '));
  L.push('', 'split verified on ' + split + ' whole builds (the ten presets and 600 at random): ' + (splitBad ? splitBad + ' FAIL' : 'all equal'));
  L.push(problems.length ? 'PROBLEMS:\n  ' + problems.join('\n  ') : 'OK — no problems.', '');
  return { name:'colours', ok:json.ok, json, text:L.join('\n') };
}
if (typeof module !== 'undefined' && module.exports) { module.exports = run; if (require.main === module) require('./_run.cjs').main(run); }
else (globalThis.HHTools = globalThis.HHTools || {}).colours = run;
})();
