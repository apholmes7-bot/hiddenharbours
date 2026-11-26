/* PIXEL CLIFF FACE - the COURSED sandstone style (the key-scene boards' cliffs).

   An ADD-ON to pxCliffFaceRig.js, which it does not edit: load it after the rig and it hands back a
   painter for DEF.sandstone. Swap that painter in for one face() call and the face is drawn in the
   boards' manner instead of the shipped one:
     - many thin stacked courses of near-equal height, each with a crisp bed line under it, near level
       (stepped a texel at a time over long segments; the shipped bed is warped and gated);
     - thin pale interbeds (the silt accessory row), never two in a row;
     - per-course tone, a broad stain, specks, thin flaggy partings, short hairline joints and small
       chips and sockets inside every block, so no block is one flat fill;
     - brick-like vertical joints at irregular spacing, staggered course to course, dark with a lit wall;
     - a few blocks proud (lit lip) and fewer sat back. A sat-back block is SHADE, not black: its fill
       keeps its course tone and only its top row (under the course above) drops to the shadow band.
   Everything ABOVE the painter (the form field, the tiers, the cast shadow, the channels, the index map)
   is the rig's own and untouched, so the face is co-registered with the shipped face of the same rock,
   aspect and batter, packs the SAME palette rows (the sandstone tiers + silt + moss + dust) and draws
   through any sandstone LUT. Only the texel pattern differs.

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
     proud    chance a block stands proud
     sunk     chance a block sits back (shade, never black)
     relief   detail height of a proud block (the normal's facet strength)
     speck    chance of a one-texel tone speck inside a block
     stain    share of the face a broad stain darkens one band
     tick     chance per texel column of a short hairline joint inside a course
     part     chance a row inside a course carries a thin darker parting
     chip     mean spacing of small chips and sockets in texels (0 = none)
     jgate    chance a vertical joint is cut through its course (the rest are closed, the slab runs on)
     lip      chance a block's top row is a lit lip (a proud block always has one) */
(function (root) {
  'use strict';
  const P = root.PxLang, PF = root.PxCliffFace;
  if (!P || !PF) throw new Error('pxCliffCoursed.js: load pixelLanguage.js and pxCliffFaceRig.js first');
  const { hash2, clamp, fbmP, sites } = P;
  const FL = Math.floor;
  const fb = (u, v, cx, cy, s) => fbmP(u, v, Math.max(1, Math.round(cx)), Math.max(1, Math.round(cy)), s, 3);
  const DEFAULTS = { course: [3, 6], pale: .18, paleTh: [1, 2], jsp: [18, 44], step: .12, seg: 48,
                     proud: .10, sunk: .05, relief: .8, speck: .06, stain: .30, tick: .05, part: .35,
                     chip: 9, jgate: .55, lip: .35 };

  function painter(opts) {
    const o = Object.assign({}, DEFAULTS, opts || {});
    return function paintCoursed(c) {
      const { t, n, m, R, A, pal } = c;
      const S = 1 / PF.SL.sin;                                   /* courses down a batter are 1/sin apart */
      const rd = A.read > 1.2 ? 1 : 0;

      /* the courses, top to bottom; a pale interbed is a course of its own */
      const courses = [];
      let y = 0, k = 0;
      while (y < m) {
        const pale = k > 0 && hash2(k, 1, R + 700) < o.pale && courses[courses.length - 1].pale === false;
        const rg = pale ? o.paleTh : o.course;
        let th = Math.max(1, Math.round((rg[0] + FL(hash2(k, 2, R + 700) * (rg[1] - rg[0] + 1))) * S));
        if (m - y - th < 2) th = m - y;
        const r = hash2(k, 3, R + 700);
        courses.push({ y0: y, th, pale, tone: pale ? 3 : (r < .30 ? 3 : r > .88 ? 1 : 2), k });
        y += th; k++;
      }
      /* each bed line steps a texel at a time along s over long segments: near level, still wraps */
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
      /* the blocks of a course: joints at irregular spacing, staggered by the course's own phase */
      const joints = courses.map((cs, ci) => {
        const sp = o.jsp[0] + hash2(ci, 4, R + 720) * (o.jsp[1] - o.jsp[0]);
        const nJ = Math.max(1, Math.round(n / sp)), ph = hash2(ci, 5, R + 720);
        const xs = [];
        for (let j = 0; j < nJ; j++) xs.push(Math.round((j + ph + (hash2(ci, j + 6, R + 720) - .5) * .7) * n / nJ));
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
        return r < o.proud ? 1 : r > 1 - o.sunk ? -1 : 0;
      };
      const blockTone = (ci, b) => {
        const cs = courses[ci];
        if (cs.pale) return cs.tone;
        const r = hash2(ci * 131 + b, 8, R + 730);
        return clamp(cs.tone + (r < .15 ? -1 : r > .85 ? 1 : 0), 1, 3);
      };

      for (let yy = 0; yy < m; yy++) for (let x = 0; x < n; x++) {
        const ci = courseAt[yy * n + x], cs = courses[ci], b = blockOf(ci, x), p = blockP(ci, b);
        const pl = cs.pale ? pal.silt : pal.rock;
        let bd = blockTone(ci, b) + (p > 0 ? 1 : 0);              /* a sat-back block keeps its tone */
        if (!cs.pale && fb(x / n, yy / m, 6, 4, R + 760) > 1 - o.stain * .5) bd -= 1;   /* broad stain */
        if (hash2(x, yy, R + 740) < o.speck) bd += hash2(x, yy, R + 741) < .5 ? -1 : 1;
        const grain = (hash2(x >> 1, yy, R + 743) - .5) * .25;
        const h = p * o.relief + (cs.pale ? -.3 : 0) + grain;
        t.set(x, yy, pl, clamp(bd, 1, 4), h);
        const up = courseAt[((yy - 1 + m) % m) * n + x], dn = courseAt[((yy + 1) % m) * n + x];
        if (up !== ci) {
          const pUp = blockP(up, blockOf(up, x));
          if (p < 0 || pUp > p) t.set(x, yy, pl, 1, h - .5, 1);                           /* shade under the course above */
          else if (p > 0 || hash2(ci * 131 + b, 9, R + 730) < o.lip) t.set(x, yy, pl, clamp(bd + 1, 1, 4), h + .4, 1);   /* lit lip */
        } else if (dn !== ci) {                                                           /* the bed line, crisp */
          t.set(x, yy, pal.rock, p > 0 || cs.pale ? 0 : 1, h - .8, 1);
        } else if (!cs.pale && cs.th >= 4 && (yy - cs.y0) % 2 === 1 && hash2(ci, yy, R + 744) < o.part &&
                   fb(x / n, yy / m, 8, 2, R + 745) > .45) {
          t.set(x, yy, pl, clamp(bd - 1, 1, 4), h - .15, 1);                              /* thin flaggy parting */
        }
      }
      /* the vertical joints: dark, a lit right wall where the aspect reads, a shaded left one */
      for (let ci = 0; ci < courses.length; ci++) {
        const cs = courses[ci]; if (cs.pale) continue;
        for (let jj = 0; jj < joints[ci].length; jj++) {
          const jx = joints[ci][jj];
          if (hash2(ci, jj, R + 750) >= o.jgate) continue;
          for (let yy = cs.y0; yy < cs.y0 + cs.th; yy++) {
            const yw = ((yy % m) + m) % m;
            if (courseAt[yw * n + jx] !== ci) continue;
            const pl = pal.rock, bL = blockOf(ci, (jx - 1 + n) % n), bR = blockOf(ci, jx);
            t.set(jx, yw, pl, 0, t.h[yw * n + jx] - 1.0, 1);
            const pR = blockP(ci, bR), pLf = blockP(ci, bL);
            if (pR >= pLf && rd) t.set(jx + 1, yw, pl, clamp(blockTone(ci, bR) + 1, 1, 4), t.h[t.i(jx + 1, yw)] + .2, 1);
            if (pLf > pR) t.set(jx - 1, yw, pl, clamp(blockTone(ci, bL) - 1, 1, 4), t.h[t.i(jx - 1, yw)] - .2, 1);
          }
        }
        /* short hairline joints inside the course: 1 to 3 texels, irregular, a shade not a cut */
        for (let x = 0; x < n; x++) {
          if (hash2(x, ci, R + 770) >= o.tick) continue;
          const len = 1 + FL(hash2(x, ci, R + 771) * 3), y0 = cs.y0 + FL(hash2(x, ci, R + 772) * Math.max(1, cs.th - len));
          for (let d = 0; d < len; d++) {
            const yw = (((y0 + d) % m) + m) % m;
            if (courseAt[yw * n + x] !== ci) break;
            t.set(x, yw, pal.rock, clamp(t.band[yw * n + x] - 1, 1, 4), t.h[yw * n + x] - .4, 1);
          }
        }
      }
      /* small chips (proud, lit) and sockets (plucked, a shade with a lit lower lip) */
      if (o.chip > 0) for (const s of sites(n, m, o.chip, .9, R + 780)) {
        const ci = courseAt[s.y * n + s.x];
        if (courses[ci].pale || s.r > .7) continue;
        const w = 1 + FL(s.r2 * 3), hh = 1 + FL(hash2(s.x, s.y, R + 781) * 2);
        const proud = hash2(s.x, s.y, R + 782) < .5;
        for (let dy = 0; dy < hh; dy++) for (let dx = 0; dx < w; dx++) {
          const x = (s.x + dx) % n, yw = (s.y + dy) % m, i = yw * n + x;
          if (courseAt[i] !== ci || t.pal[i] !== pal.rock) continue;
          if (proud) t.set(x, yw, pal.rock, clamp(t.band[i] + (dy === 0 ? 1 : 0), 1, 4), t.h[i] + .5, 1);
          else t.set(x, yw, pal.rock, dy === 0 ? 1 : clamp(t.band[i] - 1, 1, 4), t.h[i] - .5, 1);
        }
        if (!proud) { const yw = (s.y + hh) % m, i = yw * n + s.x; if (courseAt[i] === ci) t.set(s.x, yw, pal.rock, clamp(t.band[i] + 1, 1, 4), t.h[i], 1); }
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
