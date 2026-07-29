/* Bakes the Rock Iso sprite sheets + RockIso.json sidecar + a to-scale lineup preview.
   run_script harness:
     (0,eval)(await readFile('Art/rockIsoRig.js'));
     (0,eval)(await readFile('Art/_rockBake.js'));
     await ROCK_BAKE({ createCanvas, saveFile, log, dir:'export/shore-rock-rig-kit/rock',
                       stones:['sandstone'], tides:['dry','wet','awash'] });
   One sheet per species × stone × tide: variant COLS × 3 dress ROWS, cell = SPECIES[k].cell,
   every cell pivot-aligned (bottom-centre ground contact). No water, no foam — the shader owns
   both (ADR-0010/0012/0023); `awash` sheets are CLIPPED at the sea plane, not flooded. */
globalThis.ROCK_BAKE = async function (o) {
  const R = o.R || globalThis.RockIso;
  const { createCanvas, saveFile } = o;
  const log = o.log || (() => {});
  const dir = (o.dir || 'Art/Sprites/Shore/Rock').replace(/\/$/, '');
  const stones = o.stones || Object.keys(R.STONES);
  const tides = o.tides || R.TIDES;
  const keys = o.species || Object.keys(R.SPECIES);
  const DRESS = R.DRESS;                       // ['bare','barnacled','weeded'] — the sheet rows

  const cvOf = (r) => { const c = createCanvas(r.w, r.h);
    c.getContext('2d').putImageData(new ImageData(new Uint8ClampedArray(r.rgba), r.w, r.h), 0, 0); return c; };
  const sheet = (w, h) => { const c = createCanvas(w, h); const x = c.getContext('2d');
    x.imageSmoothingEnabled = false; return [c, x]; };

  const written = [];
  for (const stone of stones) for (const tide of tides) for (const k of keys) {
    const S = R.SPECIES[k], cols = S.variants.length, rows = DRESS.length;
    const W = S.cell.w * cols, H = S.cell.h * rows;
    if (W > 2048 || H > 2048) throw new Error(`${k} sheet ${W}x${H} exceeds 2048 — Unity downscales silently`);
    const [cv, ctx] = sheet(W, H);
    for (let d = 0; d < rows; d++) for (let i = 0; i < cols; i++) {
      const r = R.render(k, { variant: i, dress: d, stone }, tide);
      ctx.drawImage(cvOf(r), i * S.cell.w, d * S.cell.h);
    }
    const path = `${dir}/${k}_${stone}_${tide}.png`;
    await saveFile(path, cv); written.push(`${path}  ${W}\u00d7${H}`);
  }
  log(written.join('\n'));

  // ---- sidecar: the contract the engine reads (anchors at each variant's shipped dress) -------
  const out = {
    kit: 'Rock Iso', ppu: R.PPU,
    camera: { elev: R.ELEV, depthSquash: +R.Q.toFixed(3), heightScale: +R.HZ.toFixed(3),
      note: '\u00be from the south at 40\u00b0, orthographic (ADR-0006/0022). Depth key along the view axis.' },
    pivot: 'bottom-centre ground contact \u2014 the tile the rock stands on, NOT the bbox centre',
    keyline: R.KEYLINE, water: 'none baked \u2014 shader owns sea, foam, spray and tide-pool fill',
    stones: Object.keys(R.STONES).map(s => ({ key: s, name: R.STONES[s].name,
      bedding: R.STONES[s].bedding, columnar: !!R.STONES[s].columnar, vein: !!R.STONES[s].vein })),
    tides: R.TIDES, dress: DRESS,
    sheets: { layout: 'variant cols \u00d7 dress rows', naming: '<Species>_<stone>_<tide>.png', baked: { stones, tides } },
    species: {},
  };
  for (const k of keys) {
    const S = R.SPECIES[k];
    out.species[k] = { name: S.name, note: S.note, cell: S.cell, reads: S.reads,
      sheet: { cols: S.variants.length, rows: DRESS.length, w: S.cell.w * S.variants.length, h: S.cell.h * DRESS.length },
      variants: S.variants.map((v, i) => {
        const r = R.render(k, { variant: i, stone: 'sandstone' }, 'dry');
        return { id: k + String.fromCharCode(65 + i), col: i, dressRow: v.dress,
          params: r.params, topM: r.topM, anchors: r.anchors };
      }) };
  }
  await saveFile(`${dir}/RockIso.json`, JSON.stringify(out, null, 2));

  // ---- lineup preview: every shipped cell on one tide line, to scale ------------------------
  if (o.preview) {
    const pad = 10, z = o.previewZoom || 3;
    let w = pad, h = 0;
    for (const k of keys) { const S = R.SPECIES[k];
      w += S.variants.length * (S.cell.w + 6) + 14; h = Math.max(h, S.cell.h); }
    const [pv, px] = sheet(w * z, (h + 34) * z); px.scale(z, z);
    px.fillStyle = '#171b1d'; px.fillRect(0, 0, w, h + 34);
    const base = h + 12;
    px.fillStyle = '#241d18'; px.fillRect(0, base, w, h + 34 - base);   // ground plane
    let x = pad;
    for (const k of keys) { const S = R.SPECIES[k];
      for (let i = 0; i < S.variants.length; i++) {
        const awash = S.variants[i].waterline > 0;
        const r = R.render(k, { variant: i, stone: 'sandstone' }, awash ? 'awash' : 'dry');
        const gy = base, dy = Math.round(gy - r.anchors.footprint.ground.y);
        if (awash) { px.fillStyle = '#22343a';                      // sea patch only under a clipped rock
          px.fillRect(x - 3, dy + r.h - 4, r.w + 6, h + 34 - (dy + r.h - 4)); }
        px.save(); px.globalAlpha = 0.28; px.fillStyle = '#0c1519';
        px.beginPath(); px.ellipse(x + r.w / 2, gy, r.anchors.footprint.rx * 32, r.anchors.footprint.ry * 32 * 0.643, 0, 0, Math.PI * 2);
        px.fill(); px.restore();
        px.drawImage(cvOf(r), x, dy);
        x += S.cell.w + 6;
      }
      x += 14;
    }
    await saveFile(o.preview, pv);
  }
  return written.length;
};
