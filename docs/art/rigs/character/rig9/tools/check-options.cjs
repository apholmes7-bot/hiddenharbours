/* tools/check-options.cjs — THE OPTIONS DATA FILE (data/options.v9.json) and the triangle budget.
     node tools/check-options.cjs           regenerate in memory, compare with the committed data/options.v9.json, write reports/options.{json,txt}
     node tools/check-options.cjs --write   also rewrite data/options.v9.json
   Everything is read off the rig: the option lists (OPTIONS, FIELDS), the creator's labels, the parts and materials each option
   puts in the mesh (by building it), the ramps (palettes, and each material's shading), the budget (by counting triangles over
   every garment, bottom, hat, hair style, beard, eye shape and head size). Nothing is typed in but the labels. */
'use strict';
(function () {
function run(C, H) {
  const O = C.OPTIONS, P = C.palettes, GM = C.GARMENTS, BT = C.BOTTOMS, FISH = C.BUILDS.fisher, OUT = 'data/options.v9.json';
  const rigSha = H.sha256('Art/characterIsoRig9.js'), poseSha = H.sha256('Art/characterIsoRig9.poses.js');
  /* the labels the creator shows (Character Creator.dc.html, NAMES; anything not listed is the value capitalised) */
  const NAMES = { shape:['Broadest','Broad','Between','Curved','Widest hips'], height:['Short','Shorter','Middling','Taller','Tall'], weight:['Slight','Lean','Middling','Sturdy','Heavy'],
    head:{ round:'Round', oval:'Oval', square:'Square', long:'Long', heart:'Heart', wide:'Wide jaw', pear:'Pear' }, eyeShape:{ round:'Round', narrow:'Narrow', lidded:'Heavy-lidded' },
    hairStyle:{ crop:'Crop', mop:'Mop', bob:'Bob', long:'Long', bun:'Bun', ponytail:'Ponytail', buzz:'Buzz', bald:'Bald' },
    beard:{ none:'None', stubble:'Stubble', moustache:'Moustache', chinstrap:'Chinstrap', goatee:'Goatee', mutton:'Mutton chops', full:'Full', long:'Long' },
    hat:{ none:'None', watchcap:'Watch cap', souwester:"Sou'wester", ballcap:'Ball cap', kerchief:'Kerchief', flatcap:'Flat cap', hood:'Hood', tophat:'Top hat', bowler:'Bowler', captain:"Captain's cap", sunhat:'Sun hat', bucket:'Bucket hat', beret:'Beret' },
    hair:{ salt:'Salt and pepper' }, outfit:{ char:'Charcoal', oil:'Oilskin yellow' }, hatCol:{ char:'Charcoal', oil:'Oilskin yellow' } };
  const cap = (s) => String(s).charAt(0).toUpperCase() + String(s).slice(1);
  const labelOf = (k, v, i) => Array.isArray(NAMES[k]) ? NAMES[k][i] : k === 'garment' ? GM[v].label : k === 'bottom' ? BT[v].label : (NAMES[k] && NAMES[k][v]) || cap(v);
  /* axis: label, owner, category, the creator tab it sits on (null: not a control), kind */
  const AX = { shape:['Body shape','person','body','Body','body'], age:['Age','person','body',null,'body'], height:['Height','person','body','Body','body'], weight:['Weight','person','body','Body','body'],
    head:['Head shape','person','face','Face','geometry'], skin:['Skin','person','skin','Body','colour'], hair:['Hair colour','person','hair','Hair','colour'], hairStyle:['Hair','person','hair','Hair','geometry'],
    beard:['Beard','person','facial hair','Hair','geometry'], eyes:['Eye colour','person','eyes','Face','colour'], eyeShape:['Eyes','person','eyes','Face','geometry'],
    garment:['Tops and outfits','clothing','clothing','Clothes','geometry'], outfit:['Outfit colour','clothing','clothing','Clothes','colour'], shirt:['Shirt colour','clothing','clothing','Clothes','colour'],
    hat:['Hat','clothing','clothing','Hats','geometry'], hatCol:['Hat colour','clothing','clothing','Hats','colour'], apronCol:['Trim colour','clothing','clothing','Clothes','colour'], bottom:['Bottoms','clothing','clothing','Clothes','geometry'] };
  const base = (o) => Object.assign({}, FISH, { preset:null, label:'options' }, o || {});
  const B_ = (b) => C.buildOf(b), tri = (f) => f.v.length - 2, side = (p) => p.replace(/_(L|R)$/, '');
  const HEADP = { head:1, nose:1, ear:1, hair:1, hat:1, hood:1, beard:1, face:1 };
  const fnv = (s) => { let h = 0x811c9dc5; for (let i = 0; i < s.length; i++) { h ^= s.charCodeAt(i); h = Math.imul(h, 0x01000193) >>> 0; } return h.toString(16); };
  const faceSig = (f) => f.mat + '/' + f.part + '/' + (f.group || '') + '/' + f.v.map((p) => p.map((x) => x.toFixed(6)).join(',')).join(';');
  const meshSig = (b) => fnv(B_(b).mesh.faces.map(faceSig).join('|'));
  function inventory(b) { const inv = {};
    for (const f of B_(b).mesh.faces) { const k = side(f.part), e = inv[k] || (inv[k] = { tris:0, mats:{}, sig:'' }); e.tris += tri(f); e.mats[f.mat] = 1; e.sig += faceSig(f) + '|'; }
    for (const k of Object.keys(inv)) { inv[k].mats = Object.keys(inv[k].mats).sort(); inv[k].sig = fnv(inv[k].sig); } return inv; }

  /* ---------------- materials: where each one's colour comes from ---------------- */
  const shade = (b) => C.shadingContract(b).materials, M0 = shade(base()), COLOUR = ['skin','hair','eyes','outfit','shirt','hatCol','apronCol'];
  const src = {}, problems = [];
  for (const k of COLOUR) { const a = shade(base({ [k]:O[k][0] })), b = shade(base({ [k]:O[k][1] }));
    for (const m of Object.keys(a)) if (JSON.stringify(a[m]) !== JSON.stringify(b[m])) { if (src[m]) problems.push('material ' + m + ' changes with ' + src[m] + ' and ' + k); src[m] = k; } }
  const CONST = { boot:'BOOT', bootL:'BOOT', sole:'BOOT', hose:'BOOT', brass:'BRASS', belt:'LEATHER', ink:'INK[0]', ink2:'INK[1]', white:'EYE_WHITE' };
  const constRamp = {};
  for (const m of Object.keys(M0)) if (!src[m]) { if (!CONST[m]) problems.push('material ' + m + ' has no colour source'); else { const c = M0[m].fixed ? M0[m].color : JSON.stringify(M0[m].ramp);
    if (constRamp[CONST[m]] && constRamp[CONST[m]] !== c) problems.push(CONST[m] + ' is not one ramp'); constRamp[CONST[m]] = c; } }
  const hex2 = (c) => [1, 3, 5].map((i) => parseInt(c.slice(i, i + 2), 16));
  const mix = (a, b, t) => '#' + hex2(a).map((x, i) => Math.round(x + (hex2(b)[i] - x) * t).toString(16).padStart(2, '0')).join('');
  const INK0 = M0.ink.color;
  for (const s of O.skin) { const m = shade(base({ skin:s })), r = P.SKINS[s];
    if (m.lid.color !== r[1] || m.lip.color !== r[1] || m.mouth.color !== r[0] || JSON.stringify(m.skin.ramp) !== JSON.stringify(r)) problems.push('skin ' + s + ': derived colours differ'); }
  for (const h of O.hair) { const m = shade(base({ hair:h })); if (m.brow.color !== mix(P.HAIRS[h][0], INK0, 0.34)) problems.push('hair ' + h + ': brow is not mix(hair[0], ink, 0.34)'); }
  for (const e of O.eyes) if (shade(base({ eyes:e })).iris.color !== P.EYES[e]) problems.push('eyes ' + e + ': iris differs');
  const T6 = { gain:2.4, bias:2.7, lo:2, hi:5 }, T5 = { gain:2.4, bias:1.8, lo:1, hi:4 };
  const toneOf = (m) => ['gain','bias','lo','hi'].every((k) => m[k] === T6[k]) ? 'T6' : ['gain','bias','lo','hi'].every((k) => m[k] === T5[k]) ? 'T5' : null;
  const DERIVED = { lid:'skin ramp [1]', lip:'skin ramp [1]', mouth:'skin ramp [0]', brow:'mix(hair ramp [0], ink, 0.34)', iris:'the eye colour' };
  const RAMP_OF = { skin:'SKINS', hair:'HAIRS', outfit:'OUTFITS', shirt:'SHIRTS', hatCol:'HATCOLS', apronCol:'APRONS' };
  const materials = {};
  for (const m of Object.keys(M0).sort()) { const x = M0[m];
    if (x.fixed) materials[m] = { fixed:true, colour: src[m] ? DERIVED[m] : x.color, from: src[m] || CONST[m] };
    else { const t = toneOf(x); if (!t) problems.push('material ' + m + ': tone is neither T6 nor T5');
      materials[m] = { ramp: src[m] || CONST[m], tone:t, off:x.off, gain:x.gain, bias:x.bias, lo:x.lo, hi:x.hi }; } }
  const ramps = {};
  for (const [k, pal] of Object.entries(RAMP_OF)) for (const v of O[k]) ramps[k + '.' + v] = P[pal][v].slice();
  for (const v of O.eyes) ramps['eyes.' + v] = [P.EYES[v]];
  for (const [n, c] of Object.entries(constRamp)) ramps[n] = c.charAt(0) === '[' ? JSON.parse(c) : [c];
  const matsOfAxis = {}; for (const [m, k] of Object.entries(src)) (matsOfAxis[k] = matsOfAxis[k] || []).push(m);

  /* ---------------- the geometry space: every garment+bottom, every hat x hair x beard x eyes ---------------- */
  const GB = []; for (const g of O.garment) { if (GM[g].top) for (const b of O.bottom) GB.push([g, b]); else GB.push([g, 'trousers']); }
  const partMats = {}, add = (p, ms) => { const s = partMats[p] || (partMats[p] = {}); for (const m of ms) s[m] = 1; };
  const TB = {}, MB = {};
  for (const [g, bt] of GB) { let t = 0; const ms = {};
    for (const f of B_(base({ garment:g, bottom:bt })).mesh.faces) if (!HEADP[f.part]) { t += tri(f); ms[f.mat] = 1; add(side(f.part), [f.mat]); }
    TB[g + '/' + bt] = t; MB[g + '/' + bt] = Object.keys(ms).sort(); }
  const TF = {}, TBD = {}; let FIXED = null;
  for (const bd of O.beard) for (const es of O.eyeShape) { let face = 0, beard = 0, fixed = 0;
    for (const f of B_(base({ beard:bd, eyeShape:es })).mesh.faces) { if (f.part === 'face') face += tri(f); else if (f.part === 'beard') beard += tri(f); else if (f.part === 'nose' || f.part === 'ear') fixed += tri(f); }
    if (TF[es] != null && TF[es] !== face) problems.push('face tris depend on the beard'); TF[es] = face;
    if (TBD[bd] != null && TBD[bd] !== beard) problems.push('beard tris depend on the eyes'); TBD[bd] = beard;
    if (FIXED != null && FIXED !== fixed) problems.push('skull tris vary'); FIXED = fixed; }
  for (const hat of O.hat) for (const hs of O.hairStyle) for (const bd of O.beard) for (const es of O.eyeShape)
    for (const f of B_(base({ hat, hairStyle:hs, beard:bd, eyeShape:es })).mesh.faces) if (HEADP[f.part]) add(side(f.part), [f.mat]);
  /* hair + hat triangles by head size: they move a few triangles with the head's proportions, because a quad the rig finds
     non-planar is fanned into four. Head size = age class x shape x weight x head shape (height does not enter). */
  const AGE_CLASS = ['child','youth','adult'], nDims = 3 * 5 * 5 * 7, dimsIdx = (a, s, w, h) => ((a * 5 + s) * 5 + w) * 7 + h;
  /* 9.2: the skull is cut along each style's hairline, so its triangles ride with the hair (and with the head size) */
  const THH = H.cache ? H.cache('hairhat', () => hairHatTable()) : hairHatTable();
  function hairHatTable(){ const T = {};
  for (const hat of O.hat) for (const hs of O.hairStyle) { const arr = new Array(nDims);
    AGE_CLASS.forEach((age, a) => O.shape.forEach((shape, s) => O.weight.forEach((weight, w) => O.head.forEach((head, h) => { let t = 0;
      for (const f of B_(base({ age, shape, weight, head, hat, hairStyle:hs, beard:'none', eyeShape:'round', height:0 })).mesh.faces) if (f.part === 'head' || f.part === 'hair' || f.part === 'hat' || f.part === 'hood') t += tri(f);
      arr[dimsIdx(a, s, w, h)] = t; }))));
    T[hat + '|' + hs] = arr; }
  return T; }
  const ageClass = (age) => age === 'child' ? 0 : age === 'youth' ? 1 : 2;
  const predict = (b) => { const n = C.normBuild(b), g = GM[n.garment].top ? n.garment + '/' + n.bottom : n.garment + '/trousers';
    return TB[g] + FIXED + TF[n.eyeShape] + TBD[n.beard] + THH[n.hat + '|' + n.hairStyle][dimsIdx(ageClass(n.age), O.shape.indexOf(n.shape), O.weight.indexOf(n.weight), O.head.indexOf(n.head))]; };
  let seed = 9091, checked = 0, wrong = 0; const rnd = () => { seed = (Math.imul(seed, 1103515245) + 12345) >>> 0; return seed / 4294967296; };
  const sample = C.CAST.map((p) => C.normBuild(p));
  for (let i = 0; i < 600; i++) { const b = {}; for (const k of C.FIELDS) b[k] = O[k][Math.floor(rnd() * O[k].length)]; sample.push(b); }
  for (const b of sample) { checked++; if (predict(b) !== C.sizes(b).tris) wrong++; }
  if (wrong) problems.push('budget formula wrong on ' + wrong + ' of ' + checked + ' builds');

  /* ---------------- over budget ---------------- */
  const MAX = 1000, CRE0 = dimsIdx(1, 0, 0, 0), over = []; let cellsAll = 0, cellsCre = 0, cellsTotal = 0, maxTris = 0, maxAt = null;
  for (const [g, bt] of GB) for (const hat of O.hat) for (const hs of O.hairStyle) for (const bd of O.beard) for (const es of O.eyeShape) {
    const fixed = TB[g + '/' + bt] + FIXED + TF[es] + TBD[bd], arr = THH[hat + '|' + hs]; let lo = Infinity, hi = -Infinity, nA = 0, nC = 0, loC = Infinity, hiC = -Infinity;
    for (let i = 0; i < nDims; i++) { const t = fixed + arr[i]; if (t < lo) lo = t; if (t > hi) hi = t; if (i >= CRE0) { if (t < loC) loC = t; if (t > hiC) hiC = t; }
      if (t > MAX) { nA++; if (i >= CRE0) nC++; } if (t > maxTris) { maxTris = t; maxAt = { garment:g, bottom:bt, hat, hairStyle:hs, beard:bd, eyeShape:es, headDims:i }; } }
    cellsTotal += nDims; cellsAll += nA; cellsCre += nC;
    if (nA) over.push({ garment:g, bottom: GM[g].top ? bt : null, hat, hairStyle:hs, beard:bd, eyeShape:es, tris:[lo, hi], creatorTris:[loC, hiC],
      when: nA === nDims ? 'always' : 'head size: ' + nA + ' of ' + nDims + ' (creator ' + nC + ' of ' + (nDims - CRE0) + ')', creator: nC > 0 }); }
  const byGarment = {}; for (const r of over){ const k = r.garment + '/' + (r.bottom || 'trousers'), e = byGarment[k] || (byGarment[k] = { rows:0, always:0, maxTris:0 }); e.rows++; if (r.when === 'always') e.always++; e.maxTris = Math.max(e.maxTris, r.tris[1]); }
  const hairHat = {}; for (const [k, a] of Object.entries(THH)) { const lo = Math.min(...a), hi = Math.max(...a); hairHat[k] = lo === hi ? { tris:lo } : { min:lo, max:hi, byHeadSize:a }; }

  /* ---------------- per axis, per option: the parts and materials it puts in the mesh ---------------- */
  const probeBase = { bottom: base({ garment:'tee' }), hairStyle: base({ hat:'none' }) };
  const axes = [];
  for (const k of C.FIELDS) { const [label, owner, category, tab, kind] = AX[k], vals = O[k], ax = { id:k, label, owner, category, kind, creator: tab ? { tab, offered: k === 'age' ? false : true } : { tab:null, offered:false },
      default: FISH[k] };
    if (k === 'hair') ax.alsoColours = ['brows (brow = mix(hair ramp [0], ink, 0.34))', 'facial hair (beard, stubble and buzz take the hair ramp)'];
    if (k === 'age') ax.creator.note = 'not a control: a creator build keeps the age of the preset it starts from; the children are not offered';
    if (kind === 'body') { ax.drives = { parts:'every part (positions: bone lengths, ring widths)', materials:[] };
      ax.options = vals.map((v, i) => ({ id:k + '.' + v, value:v, label:labelOf(k, v, i) })); axes.push(ax); continue; }
    if (kind === 'colour') { const ms = (matsOfAxis[k] || []).slice().sort(), parts = Object.keys(partMats).filter((p) => ms.some((m) => partMats[p][m])).sort();
      ax.drives = { parts, materials:ms };
      ax.options = vals.map((v, i) => ({ id:k + '.' + v, value:v, label:labelOf(k, v, i), materials:ms, ramp: k === 'eyes' ? [P.EYES[v]] : ramps[k + '.' + v] }));
      axes.push(ax); continue; }
    const pb = probeBase[k] || base(), invs = vals.map((v) => inventory(Object.assign({}, pb, { [k]:v })));
    const partsAll = {}; invs.forEach((inv) => Object.keys(inv).forEach((p) => partsAll[p] = 1));
    const affected = Object.keys(partsAll).filter((p) => { const s = invs.map((inv) => inv[p] ? inv[p].sig : '-'); return s.some((x) => x !== s[0]); }).sort();
    const matsA = {}; ax.drives = { parts:affected };
    ax.options = vals.map((v, i) => { const inv = invs[i], parts = {}, ms = {};
      for (const p of affected) if (inv[p]) { parts[p] = inv[p].mats; inv[p].mats.forEach((m) => { ms[m] = 1; matsA[m] = 1; }); }
      const o = { id:k + '.' + v, value:v, label:labelOf(k, v, i) };
      if (k === 'garment') { o.short = GM[v].short || GM[v].label; o.top = !!GM[v].top; o.wears = GM[v].wear; if (GM[v].look) o.look = GM[v].look; }
      if (k === 'hat' && C.HATS[v] && C.HATS[v].look) o.look = C.HATS[v].look;
      o.parts = parts; o.materials = Object.keys(ms).sort(); o.ramps = Array.from(new Set(o.materials.map((m) => materials[m].ramp || materials[m].from))).sort();
      return o; });
    ax.drives.materials = Object.keys(matsA).sort(); axes.push(ax); }

  /* ---------------- rules: what the rig does not distinguish, what the creator does not offer ---------------- */
  const outfits = O.garment.filter((g) => !GM[g].top), bottomNoop = outfits.filter((g) => { const s = O.bottom.map((b) => meshSig(base({ garment:g, bottom:b }))); return s.every((x) => x === s[0]); });
  const sameHair = [];
  for (const hat of O.hat) { const groups = {}; for (const hs of O.hairStyle) { const s = meshSig(base({ hat, hairStyle:hs })); (groups[s] = groups[s] || []).push(hs); }
    for (const g of Object.values(groups)) if (g.length > 1) sameHair.push({ hat, hairStyles:g }); }
  const paints = (list, ms) => ms.some((m) => list.indexOf(m) >= 0), unpainted = [];
  for (const [g, bt] of GB) { const ms = MB[g + '/' + bt], none = [];
    for (const k of ['outfit','shirt','apronCol']) if (!paints(ms, matsOfAxis[k])) none.push(k);
    if (none.length) unpainted.push({ garment:g, bottom: GM[g].top ? bt : null, noEffect:none }); }
  const hatNoCol = O.hat.filter((h) => { const ms = {}; for (const f of B_(base({ hat:h })).mesh.faces) ms[f.mat] = 1; return !paints(Object.keys(ms), matsOfAxis.hatCol); });
  const rules = {
    budget: { maxTris:MAX, enforced:false, gate:'9.2: a report, not a rule. The golden budget check is gate:false; builds over 1,000 tris are allowed and the creator offers them all. Bones stay 28 (<= 32).',
      formula:'tris = body[garment/bottom] + ' + FIXED + ' (nose, ears) + face[eyeShape] + beard[beard] + hairAndHat[hat|hairStyle] (the skull cut along the style\'s hairline, the hair and the hat; at the head size: byHeadSize[headSizeIndex] where it varies)',
      headSizeIndex:'((ageClass x 5 + shape index) x 5 + weight index) x 7 + head index; ageClass child 0, youth 1, adult and elder 2; indexes into OPTIONS order. Height, colours and names do not enter.',
      checked:'the formula equals sizes(b).tris on ' + checked + ' builds (the ten presets and 600 drawn at random)',
      body:TB, fixedHead:FIXED, face:TF, beard:TBD, hairAndHat:hairHat,
      maxAnyBuild:{ tris:maxTris, at:maxAt },
      overBudgetCount:{ cells:cellsTotal, over:cellsAll, overCreator:cellsCre, note:'cells = garment/bottom x hat x hair style x beard x eye shape x head size; the creator\'s head sizes exclude the child class' },
      overBudget:null, overBudgetRows:over.length, overBudgetByGarment:byGarment, overBudgetNote:'9.2: over 1,000 is allowed, so the rows are counted per garment rather than listed (9.1 listed its 390)' },
    notOffered: [ { axis:'age', values:['child'], why:'Start from lists the eight adult, youth and elder presets; age is not a control, so a creator build is never a child. The rig builds children (Wharf boy, Wharf girl).' } ],
    sameFigure: [
      { axis:'bottom', when:{ garment:bottomNoop }, what:'an outfit brings its own bottom: every bottom gives the same mesh (buildKey still differs; normalise to trousers before storing)' },
      { axis:'hatCol', when:{ hat:hatNoCol }, what:'no hat material is painted' },
      { axis:'hairStyle', when:sameHair, what:'these hair styles give the same mesh under this hat' },
      { axis:'outfit | shirt | apronCol', when:unpainted, what:'the garment paints none of that colour\'s materials' } ],
    looks: { garments: Object.fromEntries(O.garment.filter((g) => GM[g].look).map((g) => [g, GM[g].look])), hats: Object.fromEntries(O.hat.filter((h) => C.HATS[h] && C.HATS[h].look).map((h) => [h, C.HATS[h].look])),
      note:'the palette the creator applies when you switch into the piece; the rig does not enforce it' },
    validation:'normBuild(b) replaces any field outside these options with the Fisher\'s; shape rounds to the nearest quarter; height and weight round and clamp to -2..2; name is cut to 16 characters' };

  const data = { schema:'hidden-harbours/character-options@1', rig:'characterIsoRig9.js', exportSymbol:'CharacterIso9', derivedFromRigSha256:rigSha, posesDerivedFromRigSha256:poseSha, revision:C.revision,
    authoring:'Generated by tools/check-options.cjs from CharacterIso9. Do not hand-edit: node tools/check-options.cjs --write',
    ids:'An option id is "<axis>.<value>", the value exactly as buildKey stores it (shape.0.25, height.-1, hat.watchcap). Axis ids are the buildKey field names, in FIELDS order. Ids are never reused.',
    owners:{ person:'the figure itself: body, skin, face, eyes, brows, hair, facial hair', clothing:'garments, bottoms, hats and their colours' },
    parts:'mesh part names with the _L / _R pair folded (upper = upper_L + upper_R). "face" is the eleven face groups; "head" is the skull.',
    fields:C.FIELDS, axes, materials, ramps, rules };
  const text = JSON.stringify(data, null, 1) + '\n', committed = H.exists(OUT) ? H.text(OUT) : null, same = committed === text;
  if (problems.length === 0 && !same) problems.push(OUT + (committed == null ? ' is missing' : ' differs from the regenerated file'));

  const L = [];
  L.push('check-options — ' + OUT + ' and the triangle budget', 'rig ' + rigSha + '  poses ' + poseSha, '');
  L.push(C.FIELDS.length + ' axes, ' + axes.reduce((s, a) => s + a.options.length, 0) + ' options, ' + Object.keys(materials).length + ' materials, ' + Object.keys(ramps).length + ' ramps.');
  for (const a of axes) L.push('  ' + (a.id + '            ').slice(0, 10) + (a.owner + '/' + a.category + '                ').slice(0, 22) + a.options.length + ' options  ' + (a.creator.offered ? 'creator: ' + a.creator.tab : 'not a creator control'));
  L.push('', 'Budget: ' + rules.budget.formula + '.', rules.budget.checked + '.', 'The most any build reaches: ' + maxTris + ' tris (' + [maxAt.garment, maxAt.hat, maxAt.hairStyle, maxAt.beard, maxAt.eyeShape].join(', ') + ').');
  L.push('Over ' + MAX + ': ' + over.length + ' garment/hat/hair/beard/eye rows (' + over.filter((r) => r.when === 'always').length + ' at every head size), ' + cellsAll + ' of ' + cellsTotal + ' cells; creator ' + cellsCre + '.');
  for (const [k, e] of Object.entries(byGarment)) L.push('  ' + (k + '                      ').slice(0, 22) + e.rows + ' rows (' + e.always + ' at every head size), up to ' + e.maxTris + ' tris');

  L.push('', 'Same figure, different buildKey:', '  bottom under ' + bottomNoop.join(', '), '  hatCol with hat ' + hatNoCol.join(', '));
  for (const s of sameHair) L.push('  hair ' + s.hairStyles.join(' = ') + ' under hat ' + s.hat);
  for (const u of unpainted) L.push('  ' + u.noEffect.join(', ') + ' with ' + u.garment + (u.bottom ? '+' + u.bottom : ''));
  L.push('', 'Not offered: age child (presets only).', '');
  L.push(OUT + ': ' + (same ? 'regenerated, byte-identical to the committed file' : committed == null ? 'MISSING (run with --write)' : 'DIFFERS from the regenerated file'));
  L.push(problems.length ? 'PROBLEMS:\n  ' + problems.join('\n  ') : 'OK — no problems.', '');
  return { name:'options', ok: problems.length === 0, text: L.join('\n'),
    json: { tool:'check-options', rig:rigSha, poses:poseSha, ok:problems.length === 0, problems, dataFile:OUT, dataIdentical:same, axes:axes.length, options:axes.reduce((s, a) => s + a.options.length, 0),
      budget:{ formulaChecked:checked, maxTris, maxAt, overRows:over.length, overCells:cellsAll, overCellsCreator:cellsCre, cells:cellsTotal } },
    data: { [OUT]: text } };
}
if (typeof module !== 'undefined' && module.exports) { module.exports = run; if (require.main === module) require('./_run.cjs').main(run); }
else (globalThis.HHTools = globalThis.HHTools || {}).options = run;
})();
