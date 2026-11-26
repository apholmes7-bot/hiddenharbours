/* PIXEL CLIFF FACE â€” the COURSED sandstone style (the key-scene boards' cliffs).

   An ADD-ON to pxCliffFaceRig.js, which it does not edit: load it after the rig and it hands back a
   painter for DEF.sandstone. Swap that painter in for one face() call and the face is drawn in the
   boards' manner instead of the shipped one:
     - regular stacked courses of near-equal height, with a crisp dark bed line under each and a lit
       lip on top (the shipped bed is warped and gated; this one is straight, stepped a texel at a time);
     - every few courses a thin pale interbed (the silt accessory row), the grey-green band the boards
       run along the face;
     - brick-like vertical joints, one per block, dark with a lit right wall, staggered course to course;
     - blocks that stand proud or sit back as whole blocks (their own height), so the live light and the
       baked tiers facet the wall block by block.
   Everything ABOVE the painter (the form field, the tiers, the cast shadow, the channels, the index map)
   is the rig's own and untouched, so the face is co-registered with the shipped face of the same rock,
   aspect and batter, packs the SAME palette rows (the sandstone tiers + silt) and draws through any
   sandstone LUT. Only the texel pattern differs.

   Usage (the bake host):
     (0,eval)(pixelLanguage.js); (0,eval)(pxCliffFaceRig.js); (0,eval)(pxCliffCoursed.js);
     const keep = PxCliffFace.DEF.sandstone.paint;
     PxCliffFace.DEF.sandstone.paint = PxCliffCoursed.painter(opts);
     try { b = PxCliffFace.face('sandstone', A, 1, {slope}); ch = PxCliffFace.channels(b, {index:true}); }
     finally { PxCliffFace.DEF.sandstone.paint = keep; }

   opts (every tunable; the C# side passes them from a CliffFaceStyleDef asset):
     course   [min, max] course height in texels on a wall (a batter stretches it by 1/sin)
     pale     chance a course boundary carries a pale interbed
     paleTh   [min, max] interbed height in texels
     jsp      [min, max] vertical joint spacing in texels
     step     chance per segment that a bed line steps one texel up or down
     seg      bed-line segment length in texels (a whole number of them wraps the tile)
     proud    chance a block stands proud (and the same chance it sits back)
     relief   detail height of a proud block (the normal's facet strength)
     speck    chance of a one-texel tone speck inside a block
     jgate    chance a vertical joint is cut through its course (the rest are closed, the slab runs on)
     lip      chance a block's top row is a lit lip (a proud block always has one) */
(function (root) {
  'use strict';
  const P = root.PxLang, PF = root.PxCliffFace;
  if (!P || !PF) throw new Error('pxCliffCoursed.js: load pixelLanguage.js and pxCliffFaceRig.js first');
  const { hash2, clamp } = P;
  const FL = Math.floor;
  const DEFAULTS = { course: [6, 8], pale: .30, paleTh: [2, 3], jsp: [14, 24], step: .35, seg: 24,
                     proud: .22, relief: 1.4, speck: .05, jgate: .65, lip: .70 };

  function painter(opts) {
    const o = Object.assign({}, DEFAULTS, opts || {});
    return function paintCoursed(c) {
      const { t, n, m, R, st, A, pal } = c;
      const S = 1 / PF.SL.sin;                                   /* courses down a batter are 1/sin apart */
      const rd = A.read > 1.2 ? 1 : 0;

      /* the courses, top to bottom; a pale interbed is a course of its own */
      const courses = [];
      let y = 0, k = 0;
      while (y < m) {
        const pale = k > 0 && hash2(k, 1, R + 700) < o.pale && courses[courses.length - 1].pale === false;
        const rg = pale ? o.paleTh : o.course;
        let th = Math.max(2, Math.round((rg[0] + FL(hash2(k, 2, R + 700) * (rg[1] - rg[0] + 1))) * S));
        if (m - y - th < 3) th = m - y;
        courses.push({ y0: y, th, pale, tone: pale ? 3 : (hash2(k, 3, R + 700) < .35 ? 3 : 2), k });
        y += th; k++;
      }
      /* each bed line steps a texel at a time along s: whole segments, so the tile still wraps */
      const nSeg = Math.max(1, Math.round(n / o.seg));
      const lineOff = (ci, x) => {
        if (ci === 0) return 0;
        const sg = FL(((x % n) + n) % n * nSeg / n);
        const r = hash2(ci, sg, R + 710);
        return r < o.step * .5 ? -1 : r > 1 - o.step * .5 ? 1 : 0;
      };
      const courseAt = new Int16Array(n * m);
      for (let x = 0; x < n; x++) for (let ci = 0; ci < courses.length; ci++) {
        const a = courses[ci].y0 + lineOff(ci, x);
        const e = ci + 1 < courses.length ? courses[ci + 1].y0 + lineOff(ci + 1, x) : m;
        for (let yy = a; yy < e; yy++) courseAt[(((yy % m) + m) % m) * n + x] = ci;
      }
      /* the blocks of a course: joint positions, staggered by the course's own phase */
      const joints = courses.map((cs, ci) => {
        const sp = o.jsp[0] + hash2(ci, 4, R + 720) * (o.jsp[1] - o.jsp[0]);
        const nJ = Math.max(1, Math.round(n / sp)), ph = hash2(ci, 5, R + 720);
        const xs = [];
        for (let j = 0; j < nJ; j++) xs.push(Math.round((j + ph + (hash2(ci, j + 6, R + 720) - .5) * .5) * n / nJ));
        return xs.map(x => ((x % n) + n) % n).sort((a, b) => a - b);
      });
      const blockOf = (ci, x) => {
        const xs = joints[ci]; let b = 0;
        for (let j = 0; j < xs.length; j++) if (x >= xs[j]) b = j + 1;
        return b % xs.length;                                    /* the last block wraps into the first */
      };
      const blockP = (ci, b) => {
        if (courses[ci].pale) return 0;
        const r = hash2(ci * 131 + b, 7, R + 730);
        return r < o.proud ? 1 : r > 1 - o.proud ? -1 : 0;
      };
      const blockTone = (ci, b) => {
        const cs = courses[ci];
        if (cs.pale) return cs.tone;
        const r = hash2(ci * 131 + b, 8, R + 730);
        return clamp(cs.tone + (r < .18 ? -1 : r > .86 ? 1 : 0), 1, 3);
      };

      for (let yy = 0; yy < m; yy++) for (let x = 0; x < n; x++) {
        const ci = courseAt[yy * n + x], cs = courses[ci], b = blockOf(ci, x), p = blockP(ci, b);
        const pl = cs.pale ? pal.silt : pal.rock;
        let bd = blockTone(ci, b) + (p > 0 ? 1 : p < 0 ? -1 : 0);
        if (!cs.pale && hash2(x, yy, R + 740) < o.speck) bd += hash2(x, yy, R + 741) < .5 ? -1 : 1;
        const h = p * o.relief + (cs.pale ? -.4 : 0);
        t.set(x, yy, pl, clamp(bd, 0, 4), h);
        const up = courseAt[((yy - 1 + m) % m) * n + x], dn = courseAt[((yy + 1) % m) * n + x];
        if (up !== ci) { if (p > 0 || hash2(ci * 131 + b, 9, R + 730) < o.lip) t.set(x, yy, pl, clamp(bd + 1, 0, 4), h + .6, 1); }   /* the lit lip */
        else if (dn !== ci) {                                                     /* the bed line */
          t.set(x, yy, pal.rock, 0, h - 1.2, 1);
          if (rd && hash2(x, yy, R + 742) < .6) t.set(x, yy - 1, pl, clamp(bd - 1, 0, 4), h - .5, 1);
        }
      }
      /* the vertical joints: dark, a lit right wall where the aspect reads, a shaded left one */
      for (let ci = 0; ci < courses.length; ci++) {
        const cs = courses[ci]; if (cs.pale) continue;
        for (let jj = 0; jj < joints[ci].length; jj++) {
          const jx = joints[ci][jj];
          if (hash2(ci, jj, R + 750) >= o.jgate) continue;
          for (let yy = cs.y0 - 1; yy < cs.y0 + cs.th + 1; yy++) {
            const yw = ((yy % m) + m) % m;
            if (courseAt[yw * n + jx] !== ci) continue;
            const pl = pal.rock, bL = blockOf(ci, (jx - 1 + n) % n), bR = blockOf(ci, jx);
            t.set(jx, yw, pl, 0, t.h[yw * n + jx] - 1.3, 1);
            const pR = blockP(ci, bR), pLf = blockP(ci, bL);
            if (pR >= pLf) t.set(jx + 1, yw, pl, clamp(blockTone(ci, bR) + 1, 0, 4), t.h[t.i(jx + 1, yw)] + .3, 1);
            if (pLf > pR || rd) t.set(jx - 1, yw, pl, clamp(blockTone(ci, bL) - 1, 0, 4), t.h[t.i(jx - 1, yw)] - .3, 1);
          }
        }
      }
      /* weathering, as the shipped painter: dust on the windward lips, seep and moss under the lee */
      for (let yy = 0; yy < m; yy++) for (let x = 0; x < n; x++) {
        const i = yy * n + x;
        if (A.dust && t.band[i] >= 3 && t.pal[i] === pal.rock && hash2(x >> 1, yy, R + 80) < A.dust * .35) t.set(x, yy, pal.dust, 3, undefined, 1);
        if (A.lee && t.band[i] === 0 && hash2(x, yy, R + 81) < A.lee * .10) {
          const len = 2 + FL(hash2(x, yy, R + 82) * 5);
          for (let d = 1; d <= len; d++) { const j = t.i(x, yy + d); if (t.pal[j] !== pal.rock) break; t.set(x, yy + d, pal.moss, d < len / 2 ? 1 : 2, t.h[j], 1); }
        }
      }
      return courseAt;
    };
  }

  root.PxCliffCoursed = { DEFAULTS, painter };
})(typeof globalThis !== 'undefined' ? globalThis : window);
