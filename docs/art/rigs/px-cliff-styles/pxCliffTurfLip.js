/* pxCliffTurfLip.js: the turf lip of a clifftop, as one long strip. An add-on to the px kit's pixel language
   (pixelLanguage.js must be loaded first); installs globalThis.PxCliffTurfLip.

   What it draws, at ONE texel per pixel (the ground's own texel, 32 a metre), top row first:

     rows above the brow line   nothing, except a ragged fringe of blades standing over the edge, so the
                                meadow runs to the lip untouched and the line it ends on is never ruled
     the sod                    the turf mat rolled over the edge, torn away in places
     the hang                   clumps of long grass falling over the edge, short blades between them
     the undercut               the shade the mat throws on what is under it, darkest against the mat
     the apron                  the cut soil of the overburden under the mat: grain, stones, dangling roots,
                                and a torn lower edge that breaks up over the rock
     slumps and tufts           now and then a block of turf that has slid and clings lower, and small tufts

   Everything is periodic in the strip's length, so the strip tiles along a run. All sizes are texels and all
   of them are options (CliffTurfLipDef.OptionsJson):

     w, h        the strip's size in texels
     line        the row of the brow line (the rows above it are the inland fringe)
     fringe      [min, max] height of a fringe blade over the line
     fringeShare share of columns that carry a fringe blade
     sod         [min, max] thickness of the mat on the edge
     tornShare   share of the edge where the mat is torn away and the soil shows
     shade       [min, max] depth of the undercut shade
     clumpGap    [min, max] spacing between hanging clumps
     clumpHalf   [min, max] half width of a clump
     hang        [min, max] length of a clump's longest blade
     short       [min, max] length of the short blades between clumps
     apron       [min, max] how far below the line the soil runs ([0, 0] = none)
     stoneShare  share of soil texels that seed a stone
     rootShare   share of columns that dangle a root
     root        [min, max] length of a root
     slumpGap    [min, max] spacing between slumped blocks ([0, 0] = none)
     slumpDrop   [min, max] how far below the line a slumped block or a tuft clings
     tuftGap     mean spacing of small clinging tufts (0 = none)
     seed        the strip's seed
     grass       five colours, darkest to lightest      soil   five colours, darkest to lightest
     straw       the dead blade colour                  stone  a stone's colour
     shadeColour the undercut's colour                  shadeAlpha  [near, far] alpha of the undercut where
                                                        there is no soil under it, 0 to 255 */
(function (root) {
  'use strict';
  const P = root.PxLang;
  if (!P) throw new Error('pxCliffTurfLip.js: load pixelLanguage.js first');
  const { hash2, fbmP, h2r, clamp } = P;
  const FL = Math.floor;
  const DEFAULTS = {
    w: 2048, h: 96, line: 10, fringe: [1, 6], fringeShare: .55, sod: [4, 8], tornShare: .1, shade: [5, 11],
    clumpGap: [8, 26], clumpHalf: [4, 11], hang: [12, 34], short: [3, 8],
    apron: [30, 54], stoneShare: .012, rootShare: .06, root: [5, 16],
    slumpGap: [150, 330], slumpDrop: [16, 46], tuftGap: 46, seed: 9127,
    grass: ['#253832', '#334333', '#445338', '#565f40', '#707959'],
    soil: ['#2a1c16', '#3d2a20', '#573b30', '#6c4c38', '#8b6a50'],
    straw: '#9a8d58', stone: '#8a8478', shadeColour: '#160d0a', shadeAlpha: [190, 110],
  };

  function strip(opts) {
    const o = Object.assign({}, DEFAULTS, opts || {});
    const W = o.w | 0, H = o.h | 0, R = o.seed | 0, line = o.line | 0;
    const out = new Uint8Array(W * H * 4);
    const wx = (x) => ((x % W) + W) % W;
    const put = (x, y, hex, a) => {
      if (y < 0 || y >= H) return;
      const i = (y * W + wx(x)) * 4, c = h2r(hex);
      out[i] = c[0]; out[i + 1] = c[1]; out[i + 2] = c[2]; out[i + 3] = a == null ? 255 : a;
    };
    const alphaAt = (x, y) => (y < 0 || y >= H) ? 0 : out[(y * W + wx(x)) * 4 + 3];
    const rng = (a, b, s) => hash2(a, b, R + s);
    const pick = (rg, r) => rg[0] + FL(r * (rg[1] - rg[0] + 1));
    /* 1D noise along the strip with features about `size` texels long, and 2D noise with cells sx by sy */
    const noise = (x, size, s) => fbmP(wx(x) / W, .5, Math.max(1, Math.round(W / size)), 1, R + s, 2);
    const noise2 = (x, y, sx, sy, s) => fbmP(wx(x) / W, y / H, Math.max(1, Math.round(W / sx)), Math.max(1, Math.round(H / sy)), R + s, 2);
    const lerpR = (rg, t) => clamp(Math.round(rg[0] + t * (rg[1] - rg[0])), rg[0], rg[1]);
    const G = o.grass, S = o.soil;

    /* the edge: where the mat is torn, how thick the mat is, how deep the shade and the apron */
    const torn = new Uint8Array(W), sodTh = new Uint8Array(W), shadeTh = new Uint8Array(W), apron = new Int16Array(W);
    const hasApron = o.apron[1] > 0;
    for (let x = 0; x < W; x++) {
      torn[x] = noise(x, 26, 11) > 1 - o.tornShare * 1.6 ? 1 : 0;
      sodTh[x] = torn[x] ? 0 : lerpR(o.sod, noise(x, 12, 12) * 1.3 - .15);
      shadeTh[x] = lerpR(o.shade, noise(x, 15, 14) * 1.3 - .15);
      /* the apron's foot: long swells, short teeth, and a texel of chatter, so no two columns end level */
      const swell = noise(x, 90, 15), teeth = noise(x, 9, 16), chat = rng(x, 0, 17);
      apron[x] = hasApron ? Math.min(H - line - 2, Math.round(o.apron[0] + (o.apron[1] - o.apron[0]) * clamp(swell * 1.5 - .25, 0, 1) + (teeth - .5) * 9 + (chat - .5) * 2)) : 0;
    }

    /* the apron: cut soil. Grain is single texels gathered into soft blotches, darker in the damp under the
       mat and paler lower down where it has dried; stones sit proud with a shade texel under them */
    for (let x = 0; x < W; x++) {
      const d = apron[x];
      for (let k = 0; k < d; k++) {
        const y = line + k, f = k / Math.max(1, d - 1);
        /* the foot crumbles: the last rows drop texels, more the lower they are */
        const tail = d - k;
        if (tail <= 5 && rng(x, k, 21) < .16 * (6 - tail)) continue;
        const blot = noise2(x, y, 11, 5, 22), fine = rng(x, k, 23);
        let tone = 1.2 + 1.9 * blot + (fine - .5) * 1.3 + f * .5;
        const c = S[clamp(Math.round(tone), 1, 3)];
        put(x, y, fine > .975 ? S[4] : c);
      }
      /* the shade the apron's own foot throws on the rock: one or two texels, flat alpha */
      if (d > 0) for (let k = 0; k < 2; k++) if (rng(x, k, 24) < (k ? .45 : .9) && !alphaAt(x, line + d + k)) put(x, line + d + k, o.shadeColour, o.shadeAlpha[1]);
    }
    if (hasApron) for (let x = 0; x < W; x++) for (let k = 6; k < apron[x] - 4; k++) {
      if (rng(x, k, 25) >= o.stoneShare * .25) continue;
      const sw = 2 + FL(rng(x, k, 26) * 3), sh = 1 + FL(rng(x, k, 27) * 2), y = line + k;
      for (let dy = 0; dy < sh; dy++) for (let dx = 0; dx < sw; dx++) {
        if (sh > 1 && ((dy === 0 && dx === sw - 1) || (dy === sh - 1 && dx === 0))) continue;   /* knock the corners off */
        put(x + dx, y + dy, dy === 0 ? o.stone : S[4]);
      }
      for (let dx = 0; dx < sw; dx++) put(x + dx, y + sh, S[0]);
    }

    /* the undercut: against soil it is the soil's two darkest tones, against bare rock a flat alpha shade */
    for (let x = 0; x < W; x++) {
      const y0 = line + sodTh[x], d = shadeTh[x], near = Math.max(1, Math.round(d * .5));
      for (let k = 0; k < d; k++) {
        const far = k >= near;
        if (far && rng(x, k, 20) < .25 + .55 * (k - near) / Math.max(1, d - near)) continue;
        if (apron[x] > sodTh[x] + k) put(x, y0 + k, far ? S[0] : o.shadeColour);
        else put(x, y0 + k, o.shadeColour, far ? o.shadeAlpha[1] : o.shadeAlpha[0]);
      }
    }

    /* roots dangling out of the soil under the mat: pale where they catch the light, wandering a texel */
    for (let x = 0; x < W; x++) {
      if (rng(x, 1, 40) >= o.rootShare) continue;
      let xx = x; const len = pick(o.root, rng(x, 2, 40)), y0 = line + sodTh[x] + 1 + FL(rng(x, 3, 40) * 4);
      for (let k = 0; k < len; k++) {
        put(xx, y0 + k, k > len * .6 ? S[3] : S[4]);
        const r = rng(x, k, 41); if (r < .24) xx--; else if (r > .76) xx++;
      }
    }

    /* the mat */
    for (let x = 0; x < W; x++) for (let k = 0; k < sodTh[x]; k++) {
      const r = rng(x, k, 30);
      put(x, line + k, k === sodTh[x] - 1 ? G[0] : k === sodTh[x] - 2 ? (r < .5 ? G[1] : G[2]) : r < .16 ? G[3] : r > .84 ? G[1] : G[2]);
    }

    /* one blade: a 1 texel column falling from (x, y0), drifting a texel now and then, lighter to its tip */
    const blade = (x, y0, len, lean, sd) => {
      let xx = x;
      const tone = rng(x, sd, 50), dead = rng(x, sd, 51) < .12;
      for (let k = 0; k < len; k++) {
        const f = len <= 1 ? 1 : k / (len - 1);
        let c = tone < .25 ? G[1] : tone > .78 ? G[3] : G[2];
        if (k === 0) c = G[1];
        else if (f > .7) c = dead ? o.straw : tone > .5 ? G[4] : G[3];
        put(xx, y0 + k, c);
        const r = rng(x * 7 + k, sd, 52);
        if (k > 1 && r < .2) xx += lean;
      }
      return xx;
    };

    /* the short blades that hang everywhere the mat is whole, then the long clumps */
    for (let x = 0; x < W; x++) {
      if (torn[x] || rng(x, 3, 60) < .2) continue;
      blade(x, line + sodTh[x] - 1, 1 + pick(o.short, rng(x, 4, 60)), rng(x, 5, 60) < .5 ? -1 : 1, 61);
    }
    for (let cx = FL(rng(0, 1, 70) * o.clumpGap[1]), ci = 0; cx < W; ci++) {
      const half = pick(o.clumpHalf, rng(ci, 2, 70)), longest = pick(o.hang, rng(ci, 3, 70));
      if (!torn[wx(cx)]) for (let dx = -half; dx <= half; dx++) {
        const x = wx(cx + dx); if (torn[x]) continue;
        if (Math.abs(dx) < half && rng(ci * 31 + dx, 5, 70) < .07) continue;      /* a gap inside the clump */
        /* a tussock: longest in the middle, and its outer blades splay away from it */
        const e = 1 - (dx / (half + .5)) * (dx / (half + .5));
        const len = Math.max(3, Math.round(longest * (.3 + .7 * e) * (.72 + .28 * rng(ci * 31 + dx, 6, 70))));
        blade(cx + dx, line + Math.max(0, sodTh[x] - 2), len, dx < 0 ? -1 : dx > 0 ? 1 : (rng(ci, 4, 70) < .5 ? -1 : 1), 71 + ci);
        /* the shade a long blade drops on the soil beside it */
        if (len > 8 && rng(ci * 31 + dx, 8, 70) < .5) for (let k = 3; k < len - 2; k++) if (alphaAt(cx + dx + 1, line + sodTh[x] + k) === 255 && rng(dx, k, 72 + ci) < .5) {
          const i = ((line + sodTh[x] + k) * W + wx(cx + dx + 1)) * 4;
          if (out[i] > out[i + 1] + 14) put(cx + dx + 1, line + sodTh[x] + k, S[1]);   /* soil only, never a blade */
        }
      }
      cx += pick(o.clumpGap, rng(ci, 7, 70));
    }

    /* the fringe over the line: blades standing up over the edge, on the meadow's side */
    for (let x = 0; x < W; x++) {
      if (torn[x] || rng(x, 1, 80) >= o.fringeShare) continue;
      const len = pick(o.fringe, rng(x, 2, 80) * rng(x, 4, 80)), tone = rng(x, 3, 80);
      for (let k = 1; k <= len; k++) put(x, line - k, k === len ? (tone > .6 ? G[4] : G[3]) : tone < .3 ? G[1] : G[2]);
    }

    /* slumped blocks: a piece of the mat that slid and clings lower, soil under it, its own hang and shade */
    const slump = (cx, top, bw, bh, sd) => {
      for (let dx = 0; dx < bw; dx++) {
        const x = cx + dx, edge = dx === 0 || dx === bw - 1;
        const up = edge ? 1 + FL(rng(dx, sd, 89) * 2) : rng(dx, sd, 90) < .3 ? 1 : 0, th = Math.max(2, bh - up - (edge ? 1 : 0));
        const turf = Math.max(1, th - 2);
        for (let k = 0; k < th; k++) {
          const y = top + up + k, r = rng(dx, k, 91 + sd);
          if (k < turf) put(x, y, k === 0 ? (r < .5 ? G[3] : G[4]) : r < .2 ? G[3] : r > .8 ? G[1] : G[2]);
          else put(x, y, k === th - 1 ? S[0] : r < .4 ? S[1] : S[2]);
        }
        for (let k = 0; k < 2 + FL(rng(dx, sd, 92) * 3); k++) {
          const y = top + up + th + k;
          if (alphaAt(x, y) === 255) put(x, y, k < 2 ? S[0] : S[1]); else put(x, y, o.shadeColour, k < 2 ? o.shadeAlpha[0] : o.shadeAlpha[1]);
        }
        if (!edge && rng(dx, sd, 93) < .7) blade(x, top + up + turf - 1, 3 + FL(rng(dx, sd, 94) * 7), dx < bw / 2 ? -1 : 1, 95 + sd);
        if (!edge && rng(dx, sd, 96) < .4) put(x, top + up - 1, rng(dx, sd, 97) < .5 ? G[3] : G[2]);
      }
    };
    if (o.slumpGap[1] > 0) for (let cx = FL(rng(0, 1, 100) * o.slumpGap[0]), si = 0; cx < W - 24; si++) {
      slump(cx, line + pick(o.slumpDrop, rng(si, 2, 100)), 8 + FL(rng(si, 3, 100) * 12), 5 + FL(rng(si, 4, 100) * 4), si);
      cx += pick(o.slumpGap, rng(si, 5, 100));
    }
    if (o.tuftGap > 0) for (let x = FL(rng(0, 1, 110) * o.tuftGap), ti = 0; x < W; ti++) {
      const y = line + pick(o.slumpDrop, rng(ti, 2, 110)), bw = 2 + FL(rng(ti, 3, 110) * 4);
      for (let dx = 0; dx < bw; dx++) {
        if (rng(ti, dx, 115) < .5) put(x + dx, y - 1, G[3]);
        put(x + dx, y, rng(ti, dx, 111) < .5 ? G[3] : G[2]);
        put(x + dx, y + 1, G[1]);
        if (rng(ti, dx, 112) < .7) blade(x + dx, y + 1, 2 + FL(rng(ti, dx, 113) * 5), dx < bw / 2 ? -1 : 1, 114 + ti);
        else if (alphaAt(x + dx, y + 2) === 255) put(x + dx, y + 2, S[0]);
      }
      x += Math.max(8, Math.round(o.tuftGap * (.5 + rng(ti, 4, 110))));
    }
    return { W, H, rgba: out, line };
  }

  root.PxCliffTurfLip = { DEFAULTS, strip };
})(typeof globalThis !== 'undefined' ? globalThis : window);
