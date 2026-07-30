/* Hidden Harbours — TREE BAKE harness.  run_script recipe:
     (0,eval)(await readFile('Art/treeIsoRig2.js'));
     (0,eval)(await readFile('Art/_treeBake.js'));
     await TREE_BAKE({ createCanvas, saveFile, log });

   Batches the whole family headlessly — no clicking through the handoff page. Every sheet is a
   regular grid of 4 variant cols × 4 sway rows at that species+stage's measured cell, and each
   sheet can emit three co-registered channels:

     albedo  <Key>_<stage>_<season>.png         what ships
     mask    <Key>_<stage>_<season>_mask.png    R = key light · G = back rim · B = depth · A = coverage
     normal  <Key>_<stage>_<season>_normal.png  view-space normals (R = x, G = y-up, B = toward camera)

   Also writes Trees.json — the placement contract: per species+stage cell, pivot, true metres,
   sheet layout, and the camera/rule constants the sprites were built under.

   opts: { dir, species[], stages[], seasons[], channels[], createCanvas, saveFile, log } */
globalThis.TREE_BAKE = async function (o) {
  const R = globalThis.TreeRig2 || globalThis.TreeRig;   // pass 2 if loaded, else pass 1
  if (!R) throw new Error('TREE_BAKE: load treeIsoRig2.js (or treeIsoRig.js) first');
  const createCanvas = o.createCanvas, saveFile = o.saveFile, log = o.log || (() => {});
  const dir = (o.dir || 'Art/Foliage/Trees/').replace(/\/?$/, '/');
  const species = o.species || R.SPECIES.map(s => s.key);
  const stages = o.stages || R.STAGE_KEYS;
  const seasons = o.seasons || R.SEASONS;
  const channels = o.channels || ['albedo', 'mask', 'normal'];

  const put = (ctx, res, buf, x, y) => {
    const t = createCanvas(res.w, res.h);
    t.getContext('2d').putImageData(new ImageData(new Uint8ClampedArray(buf), res.w, res.h), 0, 0);
    ctx.imageSmoothingEnabled = false;
    ctx.drawImage(t, x, y);
  };

  const contract = {
    _note: 'Baked by _treeBake.js from the loaded tree rig. Regenerate rather than hand-edit.',
    ppu: R.PPU,
    camera: { name: 'ADR-0006/0022', view: '3/4 from S', elevDeg: R.ELEV, heightScale: +R.CE.toFixed(4), depthScale: +R.SE.toFixed(4) },
    light: { key: R.LIGHT.key, rim: R.LIGHT.rim, note: 'fixed upper-left key + back rim, screen space (art bible §1)' },
    rules: { rimPx: R.RIM_PX, minBodyPx: R.MIN_BODY, minClumpRadiusPx: R.MIN_R,
      note: 'a 2 px rim must leave 6 px of interior; nothing is emitted under a 5 px clump radius; the rim is gated on local mass thickness' },
    channels: { albedo: 'RGBA, binary alpha, no AA', mask: 'R=key light, G=back rim, B=depth, A=coverage', normal: 'view-space normal, R=x G=y-up B=toward camera' },
    sheet: { cols: R.VARIANTS, rows: R.SWAY, colAxis: 'variant', rowAxis: 'sway frame', fps: '5-7' },
    stages: R.STAGES,
    trees: {},
  };

  let files = 0, over = [];
  for (const key of species) {
    const sp = R.byKey[key];
    for (const stage of stages) {
      const size = R.STAGES[stage];
      const spec = R.sheetSpec(key, size);
      if (!spec.fits) over.push(key + '/' + stage + ' ' + spec.w + 'x' + spec.h);
      const [cw, ch] = spec.cell;

      for (const season of seasons) {
        const cvs = {}, ctxs = {};
        for (const c of channels) {
          cvs[c] = createCanvas(spec.w, spec.h);
          ctxs[c] = cvs[c].getContext('2d');
        }
        let worst = null;
        for (let v = 0; v < R.VARIANTS; v++) for (let f = 0; f < R.SWAY; f++) {
          const res = R.render(key, { variant: v, season, frame: f, stage });
          if (f === 0 && (!worst || res.report.thinPct > worst.thinPct)) worst = res.report;
          if (ctxs.albedo) put(ctxs.albedo, res, res.rgba, v * cw, f * ch);
          if (ctxs.mask) put(ctxs.mask, res, R.packMask(res), v * cw, f * ch);
          if (ctxs.normal) put(ctxs.normal, res, R.normalView(res), v * cw, f * ch);
        }
        const stem = dir + key + '_' + stage + '_' + season;
        for (const c of channels) {
          await saveFile(stem + (c === 'albedo' ? '' : '_' + c) + '.png', cvs[c]);
          files++;
        }
        const id = key + '/' + stage;
        contract.trees[id] = contract.trees[id] || {
          species: key, name: sp.name, latin: sp.latin, form: sp.form, stage,
          metres: spec.metres, cell: spec.cell, pivot: spec.pivot, nearFlarePad: spec.pad,
          sheet: [spec.w, spec.h], fitsUnity2048: spec.fits, seasons: [],
          audit: { thinPct: worst.thinPct, bodyRatio: worst.bodyRatio, despeckled: worst.despeckled, pass: worst.pass,
            underFloor: worst.underFloor },
        };
        contract.trees[id].seasons.push(season);
        log(stem + '  ' + spec.cell.join('x') + ' cell · pivot ' + spec.pivot.join(',') + ' · ' + spec.metres + ' m');
      }
    }
  }

  await saveFile(dir + 'Trees.json', JSON.stringify(contract, null, 1));
  log('— ' + files + ' sheet PNGs + Trees.json → ' + dir);
  if (over.length) log('!! OVER THE 2048 CAP (Unity will silently downscale): ' + over.join(', '));
  return { files, over, contract };
};
