/* Plant rig 5 bake contract. Load after plantIsoRig5.js and afPieces.js in Node or V8. */
(function(root) {
    'use strict';
    const R = root.PlantRig5;
    if (!R || R.SPECIES.length !== 50) throw Error('PlantRig5: expected 50 keys');
    const dormantStates = root.PlantDormant5.states;
    const dormant = (key, o) => dormantStates.some(s => s.species === key && s.stage === o.stage && s.season === o.season && s.variants.includes(o.variant));
    const channels = ['unlit', 'normal', 'light', 'detail'];
    const tide = [-0.85, 0, 0.5, 1, 2];
    function call(method, key, o, ...args) {
        if (!R.byKey[key]) throw Error('PlantRig5: unknown key ' + key);
        if (dormant(key, o)) return null;
        return R[method](key, Object.assign({ frame: 0, wind: { w: 0, gust: 0, dir: 1 } }, o), ...args);
    }
    const frame = (key, o) => call('frame', key, o);
    const model = (key, o) => call('model', key, o);
    const cellOf = (key, o) => call('cellOf', key, o);
    const sheet = (key, o, channel, sky) => call('sheet', key, o, channel, sky);
    function catalog() {
        const species = [];
        for (const sp of R.SPECIES) {
            const cells = [], dormantCells = [];
            for (const stage of R.STAGE_KEYS) for (const season of R.SEASONS)
            for (let variant = 0; variant < R.VARIANTS; variant++)
            for (let t = 0; t < (sp.fam === 'shore' ? tide.length : 1); t++) {
                const o = { stage, season, variant };
                if (sp.fam === 'shore') o.water = tide[t];
                const f = frame(sp.key, o);
                if (f === null) { dormantCells.push({ stage, season, variant, tide: t, state: 'DORMANT' }); continue; }
                if (!f.v.a.some(x => x !== 0)) throw Error('empty raster outside dormant data');
                cells.push({ stage, season, variant, tide: t, w: f.v.w, h: f.v.h,
                    pivot: [f.mdl.pivot.x, f.mdl.pivot.y], heightM: f.mdl.hmax / R.PPU });
            }
            // Pack cells by height, retaining each cell's own size and pivot.
            const ordered = cells.map((c, i) => ({ c, i })).sort((a, b) => b.c.h - a.c.h || b.c.w - a.c.w || a.i - b.i);
            let best = null;
            for (let limit = 128; limit <= 2048; limit += 32) {
                let x = 0, y = 0, row = 0, w = 0; const placed = []; let fits = true;
                for (const { c, i } of ordered) {
                    const cw = c.w + 2, ch = c.h + 2;
                    if (cw > limit) { fits = false; break; }
                    if (x + cw > limit) { y += row; x = 0; row = 0; }
                    if (y + ch > 2048) { fits = false; break; }
                    placed.push({ i, x: x + 1, y: y + 1 });
                    x += cw; row = Math.max(row, ch); w = Math.max(w, x);
                }
                const h = y + row;
                if (fits && (!best || w * h < best.w * best.h)) best = { w, h, placed };
            }
            if (!best) throw Error('PlantRig5: species exceeds one 2048 page');
            for (const p of best.placed) Object.assign(cells[p.i], { page: 0, x: p.x, y: p.y });
            const pages = [{ w: best.w, h: best.h }];
            species.push({ key: sp.key, family: sp.fam, habitat: sp.hab, standingHeightM: sp.h / R.PPU,
                tide: sp.fam === 'shore' ? tide : [], cells, pages, dormant: dormantCells });
            R.clearCache();
        }
        return { schema: 1, ppu: R.PPU, rigSha256: '1c71a9b2eecc28b395da4ce2a932ab150c2f96ec82880774a9afc66824483833',
            stages: R.STAGE_KEYS, seasons: R.SEASONS, variants: R.VARIANTS, channels,
            rigSheet: { cols: 4, rows: 4, windFrames: R.LOOP }, bakedWindFrames: 1, species };
    }
    function page(sp, pageIndex, channel) {
        if (!channels.includes(channel)) throw Error('PlantRig5: unknown channel');
        const p = sp.pages[pageIndex], out = new Uint8Array(p.w * p.h * 4);
        for (const c of sp.cells) {
            if (c.page !== pageIndex) continue;
            const o = { stage: c.stage, season: c.season, variant: c.variant };
            if (sp.tide.length) o.water = sp.tide[c.tide];
            const f = frame(sp.key, o), px = R.view(f, channel);
            if (f.v.w !== c.w || f.v.h !== c.h || f.mdl.pivot.x !== c.pivot[0] || f.mdl.pivot.y !== c.pivot[1])
                throw Error('PlantRig5: stale cell ' + sp.key);
            for (let y = 0; y < c.h; y++)
                out.set(px.subarray(y * c.w * 4, (y + 1) * c.w * 4), ((c.y + y) * p.w + c.x) * 4);
        }
        return out;
    }
    root.PlantBake5 = { catalog, page, frame, model, cellOf, sheet };
})(globalThis);
