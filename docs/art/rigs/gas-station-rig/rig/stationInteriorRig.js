/* Hidden Harbours — GAS STATION INTERIOR rig: the sales floor inside StationIso's storefront.
   Same turntable, same key, same 32 px = 1 m, RINGLESS per ADR-0031.

   SEAMLESS MEANS ONE CELL. This rig does not measure anything. It asks StationIso for the shell's
   published block and paints into `StationIso.cell('store', size, opts)` — the exterior's own quad and
   pivot. Blit the interior, blit the shell over it, and the doorway lines up to the pixel because
   there is only one cell in play. Nothing here can drift from the box it is inside: wall thickness,
   floor height, ceiling clear and the entry opening all come from `StationIso.shell()`.

   THE SECTION CUT IS PER FACING. A wall whose OUTSIDE is visible from this facing is not drawn at all
   — the camera is on that side of it, so it would bury the room. The two far walls keep their inside
   faces. That test is the rasteriser's own camera-facing test, not an approximation of it, so a wall
   is never half-cut.

   INTERIORS ASHORE DO NOT MOVE. The boat interiors ride their hull's rock(); a building is bolted to
   the ground and gets no such parameter. That one difference is why this is its own rig.

   Exposes globalThis.StationInterior = { render(size, dir, opts), cell(size, opts), mount(size, dir,
   name, opts), fixtures(size), interactables(size), gameplaySections(size), contract() }. */
(function (root) {
  const IS = root.IsoSolid;
  if (!IS) throw new Error('StationInterior requires IsoSolid — load Art/isoSolid.js first');
  const ST = root.StationIso;
  if (!ST) throw new Error('StationInterior requires StationIso — load Art/gasStationRig.js first (it owns the shell)');
  const { S, DEG, box, bar, tube, camBasis, proj, paint, mulberry } = IS;
  const DEFAULT_ELEV = ST.defaultElev, KEY = ST.KEY;

  // ---- ramps ---------------------------------------------------------------
  const TILE  = ['#5a5f5c', '#6d726e', '#7f8480', '#929792', '#a5aaa4', '#b9beb7', '#ced3cb'];   // sales-floor tile
  const LAM   = ['#4e5450', '#626861', '#767c74', '#8a9087', '#9ea49a', '#b3b9ad', '#c8cec1'];   // counter laminate
  const STEEL = ['#3a4148', '#565f66', '#7a858c', '#9fabb1', '#c3ced2'];
  const GRAPH = ['#101317', '#1d2127', '#2b323a', '#3d454e', '#525c63'];
  const DARK  = ['#0c1114', '#141b1f', '#1d262b', '#273238', '#333f46'];
  const CHROME = ST.ramps.CHROME, GLOW = ST.ramps.GLOW, YEL = ST.ramps.YEL;
  const BRASS = ['#463514', '#68501e', '#8b6d2c', '#ac8e46', '#cbb072'];
  /* Merchandise is deliberately DESATURATED. Saturated red, amber, blue and green are the grade code
     out on the forecourt; a shelf of drinks wearing those hues would read as fuel. */
  const P1 = ['#4a2d2a', '#63403a', '#7d554b', '#95695c', '#ad806f'];   // muted red
  const P2 = ['#25373f', '#334b55', '#43626d', '#557a86', '#6a939f'];   // muted blue
  const P3 = ['#2e3a28', '#3e4c34', '#4f5f42', '#617251', '#758761'];   // muted green
  const P4 = ['#4b4126', '#645732', '#7d6d41', '#948351', '#ab9963'];   // muted amber
  const PRODS = ['p1', 'p2', 'p3', 'p4', 'lam', 'graph'];

  function matsFor(lit) {
    return {
      tile: { ramp: TILE }, lam: { ramp: LAM }, steel: { ramp: STEEL }, graph: { ramp: GRAPH },
      dark: { ramp: DARK }, chrome: { ramp: CHROME }, brass: { ramp: BRASS }, yel: { ramp: YEL },
      wall: { ramp: TILE, off: lit ? 2 : 0 },
      p1: { ramp: P1 }, p2: { ramp: P2 }, p3: { ramp: P3 }, p4: { ramp: P4 },
      lamp: lit ? { ramp: GLOW, off: 3 } : { ramp: TILE, off: 1 },
      case: lit ? { ramp: GLOW, off: 1 } : { ramp: DARK, off: 2 },        // cooler / case interior
      glass: { ramp: DARK, off: 2 },
    };
  }

  /* Visible-from test, taken from the rasteriser rather than re-derived: a vertical face with outward
     normal (nx,ny) is camera-facing at dir d when -nx*sin(45d) + ny*cos(45d) < 0. */
  const faces = (nx, ny, dir) => (-nx * Math.sin(dir * Math.PI / 4) + ny * Math.cos(dir * Math.PI / 4)) < -0.05;

  // ---- the room ------------------------------------------------------------
  function fixtures(size) {
    const B = ST.shell(size), ix = B.inner.hw, iy = B.inner.hd, fz = B.floorZ;
    if (B.kiosk) {
      return {
        shell: B,
        counter: { x0: -ix + 0.10, x1: ix - 0.10, y: iy - 0.62, d: 0.58, h: 0.94 },
        list: [
          { id: 'till', label: 'TILL', action: 'buy', x: 0.20, y: iy - 0.62, z: fz + 0.94, wall: 'front' },
          { id: 'shelf', label: 'BACK SHELF', action: 'storage', x: 0, y: -iy + 0.22, z: fz + 1.30, wall: 'back' },
          { id: 'window', label: 'PAY WINDOW', action: 'pay', x: B.door.cx - 1.30, y: iy, z: fz + 1.10, wall: 'front' },
        ],
      };
    }
    /* Where things go, and why. Coolers take the whole back wall because a c-store's back wall IS the
       cooler wall. The service counter takes the -x wall so it faces the entry across the floor, and
       the gantry behind it is the only 2 m fixture in the room — anything that tall in the middle
       would wall the sales floor in half at this camera. Gondolas are two low island runs. */
    const cool = { x0: -ix + 0.20, x1: 1.30, y: -iy + 0.40, d: 0.78, h: 2.06, doors: 7 };
    const coffee = { x0: 2.10, x1: ix - 0.25, y: -iy + 0.36, d: 0.70, h: 0.94 };
    const till = { x: -ix + 0.62, y0: -1.30, y1: 2.45, d: 0.72, h: 0.98 };
    const gantry = { x: -ix + 0.16, y0: -0.20, y1: 2.45, d: 0.30, h: 2.10 };
    const gond = [{ y: -1.35, x0: -2.40, x1: 3.30 }, { y: 0.45, x0: -2.40, x1: 3.30 }];
    return {
      shell: B, cool, coffee, till, gantry, gond,
      list: [
        { id: 'till', label: 'SERVICE COUNTER', action: 'buy', x: till.x + 0.55, y: (till.y0 + till.y1) / 2, z: fz + till.h, wall: 'left' },
        { id: 'cooler', label: 'COOLER WALL', action: 'drinks', x: (cool.x0 + cool.x1) / 2, y: cool.y + cool.d / 2 + 0.35, z: fz + 1.10, wall: 'back' },
        { id: 'coffee', label: 'COFFEE / FOUNTAIN', action: 'coffee', x: (coffee.x0 + coffee.x1) / 2, y: coffee.y + 0.80, z: fz + coffee.h, wall: 'back' },
        { id: 'snacks', label: 'GONDOLA RUN', action: 'browse', x: (gond[0].x0 + gond[0].x1) / 2, y: gond[0].y + 0.70, z: fz + 0.95, wall: null },
        { id: 'hotcase', label: 'HOT CASE', action: 'food', x: till.x + 0.55, y: till.y1 - 0.45, z: fz + 1.34, wall: 'left' },
        { id: 'atm', label: 'ATM', action: 'cash', x: ix - 0.55, y: 2.30, z: fz + 1.05, wall: 'right' },
      ],
    };
  }

  function geo(size, dir, o) {
    const X = fixtures(size), B = X.shell, F = [];
    const ix = B.inner.hw, iy = B.inner.hd, fz = B.floorZ, cz = B.ceilZ, wt = B.wall;
    const rnd = mulberry(size.length * 131 + 977);
    // floor: tile field, darker walk band at the entry, a mat inside the door
    F.push(...box([0, 0, fz - 0.012], [ix, iy, 0.012], 'tile', 0.10, 0));
    F.push(...box([0, 0, fz - 0.004], [ix - 0.35, iy - 0.35, 0.006], 'tile', 0.30, -0.02));
    F.push(...box([B.door.cx, iy - 0.75, fz + 0.004], [B.door.w / 2 + 0.10, 0.75, 0.008], 'graph', -0.30, -0.04));
    // the walls that survive the cut, inside faces only
    const wall = (nx, ny) => {
      if (faces(nx, ny, dir)) return;                       // camera is outside this one: cut it
      if (ny) F.push(...box([0, ny * (iy + wt / 2), (fz + cz) / 2], [ix + wt, wt / 2, (cz - fz) / 2], 'wall', 0.02, 0));
      else F.push(...box([nx * (ix + wt / 2), 0, (fz + cz) / 2], [wt / 2, iy + wt, (cz - fz) / 2], 'wall', 0.05, 0));
      const bz = fz + 0.10;                                  // skirting
      if (ny) F.push(...box([0, ny * (iy - 0.012), bz], [ix, 0.012, 0.10], 'graph', 0.05, -0.03));
      else F.push(...box([nx * (ix - 0.012), 0, bz], [0.012, iy, 0.10], 'graph', 0.05, -0.03));
      /* The back wall carries the service door the shell puts on its outside face. Same block, same
         numbers, drawn from the other side — the two rigs cannot disagree about where the way out is. */
      if (ny === -1 && B.svc) {
        const V = B.svc;
        F.push(...box([V.cx, -iy + 0.018, fz + V.h / 2 + 0.045], [V.w / 2 + 0.065, 0.018, V.h / 2 + 0.065], 'graph', -0.15, -0.03));
        F.push(...box([V.cx, -iy + 0.036, fz + V.h / 2], [V.w / 2, 0.018, V.h / 2], 'wall', -0.06, -0.02));
        F.push(...box([V.cx, -iy + 0.058, fz + 1.02], [V.w / 2 - 0.09, 0.020, 0.030], 'steel', 0.50, -0.05));
        F.push(...box([V.cx, -iy + 0.044, fz + V.h + 0.16], [0.105, 0.024, 0.055], 'lamp', 0.80, -0.06));
      }
    };
    wall(0, -1); wall(0, 1); wall(-1, 0); wall(1, 0);
    // ceiling: not drawn. Recessed troffers hang where it would be, two rows of three.
    for (let r = -1; r <= 1; r += 2) for (let i = -1; i <= 1; i++)
      F.push(...box([i * ix * 0.52, r * iy * 0.42, cz - 0.14], [0.62, 0.085, 0.035], 'lamp', 0.75, -0.05));
    if (B.kiosk) {
      const C = X.counter;
      F.push(...box([(C.x0 + C.x1) / 2, C.y, fz + C.h / 2], [(C.x1 - C.x0) / 2, C.d / 2, C.h / 2], 'lam', 0.10, -0.02));
      F.push(...box([(C.x0 + C.x1) / 2, C.y, fz + C.h], [(C.x1 - C.x0) / 2 + 0.03, C.d / 2 + 0.03, 0.030], 'graph', 0.45, -0.04));
      F.push(...box([0.20, C.y - 0.10, fz + C.h + 0.11], [0.16, 0.13, 0.11], 'dark', 0.15, -0.06));         // till
      F.push(...box([0.20, C.y - 0.10, fz + C.h + 0.22], [0.13, 0.10, 0.014], 'lamp', 0.8, -0.08));
      F.push(...box([0, -iy + 0.18, fz + 1.28], [ix - 0.30, 0.16, 0.020], 'lam', 0.35, -0.03));             // back shelf
      for (let i = 0; i < 7; i++)
        F.push(...box([-ix + 0.45 + i * 0.42, -iy + 0.18, fz + 1.40], [0.10, 0.11, 0.11], PRODS[i % PRODS.length], 0.2, -0.05));
      F.push(...tube(-0.90, C.y - 0.55, fz, fz + 0.62, 0.055, 0.050, 8, 'steel', { caps: 'top', b: 0.2, db: -0.03 }));   // stool
      F.push(...tube(-0.90, C.y - 0.55, fz + 0.62, fz + 0.66, 0.170, 0.170, 10, 'graph', { caps: 'top', b: 0.4, db: -0.05 }));
      return { F, X };
    }
    // ---- cooler wall -------------------------------------------------------
    const C = X.cool, cw = (C.x1 - C.x0) / C.doors;
    F.push(...box([(C.x0 + C.x1) / 2, C.y, fz + C.h / 2], [(C.x1 - C.x0) / 2, C.d / 2, C.h / 2], 'graph', -0.10, -0.02));
    for (let i = 0; i < C.doors; i++) {
      const cx = C.x0 + cw * (i + 0.5);
      F.push(...box([cx, C.y + C.d / 2, fz + C.h / 2 + 0.06], [cw / 2 - 0.035, 0.020, C.h / 2 - 0.16], 'case', 0.25, -0.05));
      for (let r = 0; r < 4; r++)                                                                            // shelves of stock
        F.push(...box([cx, C.y + C.d / 2 - 0.10, fz + 0.30 + r * 0.44], [cw / 2 - 0.08, 0.055, 0.115], PRODS[(i + r) % PRODS.length], 0.15, -0.07));
      F.push(...box([cx, C.y + C.d / 2 + 0.022, fz + C.h / 2 + 0.06], [cw / 2 - 0.035, 0.014, C.h / 2 - 0.16], 'glass', 0.45, -0.08));
      F.push(...box([cx + cw / 2 - 0.020, C.y + C.d / 2 + 0.030, fz + C.h / 2], [0.022, 0.020, C.h / 2 - 0.10], 'chrome', 0.55, -0.10));
    }
    F.push(...box([(C.x0 + C.x1) / 2, C.y, fz + C.h + 0.055], [(C.x1 - C.x0) / 2 + 0.04, C.d / 2 + 0.04, 0.055], 'lam', 0.40, -0.04));
    F.push(...box([(C.x0 + C.x1) / 2, C.y + C.d / 2 + 0.02, fz + C.h + 0.16], [(C.x1 - C.x0) / 2 - 0.20, 0.020, 0.075], 'lamp', 0.85, -0.06));
    // ---- coffee / fountain -------------------------------------------------
    const K = X.coffee;
    F.push(...box([(K.x0 + K.x1) / 2, K.y, fz + K.h / 2], [(K.x1 - K.x0) / 2, K.d / 2, K.h / 2], 'lam', 0.10, -0.02));
    F.push(...box([(K.x0 + K.x1) / 2, K.y, fz + K.h], [(K.x1 - K.x0) / 2 + 0.03, K.d / 2 + 0.03, 0.030], 'graph', 0.45, -0.04));
    F.push(...box([(K.x0 + K.x1) / 2 - 0.30, K.y - 0.04, fz + K.h + 0.36], [0.42, K.d / 2 - 0.06, 0.36], 'steel', 0.20, -0.05));   // brewers
    for (let i = 0; i < 3; i++) {
      F.push(...box([(K.x0 + K.x1) / 2 - 0.62 + i * 0.32, K.y + K.d / 2 - 0.10, fz + K.h + 0.20], [0.085, 0.075, 0.20], 'dark', -0.10, -0.08));
      F.push(...box([(K.x0 + K.x1) / 2 - 0.62 + i * 0.32, K.y + K.d / 2 - 0.10, fz + K.h + 0.42], [0.10, 0.085, 0.030], 'chrome', 0.5, -0.10));
    }
    F.push(...box([K.x1 - 0.42, K.y - 0.02, fz + K.h + 0.44], [0.34, K.d / 2 - 0.08, 0.44], 'graph', 0.10, -0.05));                // fountain tower
    for (let i = 0; i < 4; i++)
      F.push(...box([K.x1 - 0.70 + i * 0.19, K.y + K.d / 2 - 0.06, fz + K.h + 0.30], [0.055, 0.045, 0.16], PRODS[i], 0.35, -0.09));
    for (let i = 0; i < 3; i++)                                                                                                     // cup stacks
      F.push(...tube(K.x0 + 0.24 + i * 0.20, K.y + K.d / 2 - 0.16, fz + K.h + 0.03, fz + K.h + 0.34, 0.052, 0.062, 8, 'lam', { caps: 'top', b: 0.45, db: -0.08 }));
    // ---- service counter, gantry, hot case, ATM ----------------------------
    const T = X.till, Gt = X.gantry;
    F.push(...box([T.x, (T.y0 + T.y1) / 2, fz + T.h / 2], [T.d / 2, (T.y1 - T.y0) / 2, T.h / 2], 'lam', 0.10, -0.02));
    F.push(...box([T.x, (T.y0 + T.y1) / 2, fz + T.h], [T.d / 2 + 0.04, (T.y1 - T.y0) / 2 + 0.04, 0.032], 'graph', 0.45, -0.04));
    F.push(...box([T.x + 0.10, T.y1 - 1.55, fz + T.h + 0.14], [0.14, 0.19, 0.14], 'dark', 0.15, -0.06));                            // till
    F.push(...box([T.x + 0.10, T.y1 - 1.55, fz + T.h + 0.30], [0.11, 0.16, 0.022], 'lamp', 0.85, -0.09));
    F.push(...box([T.x + 0.24, T.y1 - 2.05, fz + T.h + 0.09], [0.055, 0.075, 0.09], 'graph', 0.35, -0.07));                         // card terminal
    F.push(...box([Gt.x, (Gt.y0 + Gt.y1) / 2, fz + Gt.h / 2], [Gt.d / 2, (Gt.y1 - Gt.y0) / 2, Gt.h / 2], 'graph', -0.05, -0.02));   // gantry
    for (let r = 0; r < 5; r++) {
      const zz = fz + 0.42 + r * 0.38;
      F.push(...box([Gt.x + Gt.d / 2 - 0.02, (Gt.y0 + Gt.y1) / 2, zz], [0.030, (Gt.y1 - Gt.y0) / 2 - 0.04, 0.020], 'lam', 0.35, -0.05));
      for (let i = 0; i < 5; i++)
        F.push(...box([Gt.x + Gt.d / 2 - 0.03, Gt.y0 + 0.28 + i * ((Gt.y1 - Gt.y0 - 0.56) / 4), zz + 0.14], [0.055, 0.10, 0.115], PRODS[(r + i) % PRODS.length], 0.20, -0.07));
    }
    F.push(...box([T.x + 0.10, T.y1 - 0.45, fz + T.h + 0.28], [0.24, 0.36, 0.28], 'case', 0.25, -0.06));                            // hot case
    F.push(...box([T.x + 0.10, T.y1 - 0.45, fz + T.h + 0.58], [0.27, 0.39, 0.028], 'steel', 0.5, -0.08));
    F.push(...box([ix - 0.32, 2.30, fz + 0.70], [0.30, 0.34, 0.70], 'graph', 0.05, -0.02));                                          // ATM
    F.push(...box([ix - 0.60, 2.30, fz + 1.16], [0.030, 0.26, 0.20], 'case', 0.45, -0.06));
    F.push(...box([ix - 0.36, 2.30, fz + 1.46], [0.26, 0.30, 0.10], 'lamp', 0.7, -0.05));
    // ---- gondolas ----------------------------------------------------------
    X.gond.forEach((G, gi) => {
      const cx = (G.x0 + G.x1) / 2, hwid = (G.x1 - G.x0) / 2;
      F.push(...box([cx, G.y, fz + 0.055], [hwid, 0.46, 0.055], 'graph', -0.15, -0.02));                                             // base
      F.push(...box([cx, G.y, fz + 0.52], [hwid - 0.03, 0.06, 0.47], 'lam', 0.05, -0.03));                                           // spine
      for (const sy of [-1, 1]) for (let r = 0; r < 3; r++) {
        const zz = fz + 0.28 + r * 0.32;
        F.push(...box([cx, G.y + sy * 0.24, zz], [hwid - 0.05, 0.18, 0.018], 'lam', 0.35, -0.04));
        const n = 9;
        for (let i = 0; i < n; i++)
          F.push(...box([G.x0 + 0.24 + i * ((G.x1 - G.x0 - 0.48) / (n - 1)), G.y + sy * 0.24, zz + 0.115], [0.085, 0.13, 0.115], PRODS[(gi + r + i) % PRODS.length], 0.15, -0.06));
      }
      F.push(...box([cx, G.y, fz + 1.02], [hwid - 0.03, 0.30, 0.020], 'lam', 0.45, -0.05));                                          // top rail
      for (const sx of [-1, 1])                                                                                                      // end cap sign
        F.push(...box([cx + sx * hwid, G.y, fz + 0.86], [0.020, 0.24, 0.14], 'yel', 0.4, -0.06));
    });
    return { F, X };
  }

  // ---- render --------------------------------------------------------------
  const cell = (size, opts) => ST.cell('store', size, opts);
  function render(size, dir, opts) {
    opts = opts || {};
    size = ST.resolveSize('store', size);
    const c = cell(size, opts), G = geo(size, dir, opts);
    const F = G.F.map((f) => (f.db ? Object.assign({}, f, { db: -f.db }) : f));
    return paint(F, { W: c.W, H: c.H, cx: c.cx, cy: c.cy, dir,
      elev: opts.elev != null ? opts.elev : DEFAULT_ELEV,
      MATS: matsFor(opts.lit), keyline: opts.keyline, key: opts.key || KEY });
  }
  function mount(size, dir, name, opts) {
    opts = opts || {}; size = ST.resolveSize('store', size);
    const X = fixtures(size), c = cell(size, opts);
    const it = X.list.filter((e) => e.id === name)[0];
    if (!it) return null;
    const B = camBasis({ dir, elev: opts.elev != null ? opts.elev : DEFAULT_ELEV });
    const v = proj(it.x, it.y, it.z, B, c.cx, c.cy);
    return { x: +v.sx.toFixed(1), y: +v.sy.toFixed(1), depth: v.d, m: [it.x, it.y, it.z] };
  }

  // ---- gameplay ------------------------------------------------------------
  /* The room's own sections, for the storefront's sidecar. The walkable floor is generated from the
     same shell block the walls are built from, so SOLE and the paint can never disagree. Fit-out is
     published as obstructions with a height and a treatment, per the camper precedent: a 0.98 m
     counter is a waist block, a 2.10 m gantry is a wall, a 0.10 m mat is nothing. */
  const treat = (h) => (h <= 0.14 ? 'flat' : h <= 0.55 ? 'step_over' : h <= 1.20 ? 'waist_block' : 'wall');
  const F2 = (n) => +n.toFixed(2);
  function gameplaySections(size) {
    size = ST.resolveSize('store', size);
    const X = fixtures(size), B = X.shell, ix = B.inner.hw, iy = B.inner.hd, fz = B.floorZ;
    const obst = [];
    const add = (id, x0, x1, y0, y1, h, note) => obst.push({ id, footprint: [[F2(x0), F2(y0)], [F2(x1), F2(y0)], [F2(x1), F2(y1)], [F2(x0), F2(y1)]],
      height_above_sole_m: F2(h), treatment: treat(h), note });
    if (B.kiosk) {
      const C = X.counter;
      add('counter', C.x0, C.x1, C.y - C.d / 2, C.y + C.d / 2, C.h, 'pay counter across the front');
      add('back_shelf', -ix + 0.30, ix - 0.30, -iy, -iy + 0.34, 1.46, 'wall shelf, above head height at the sill');
    } else {
      const C = X.cool, K = X.coffee, T = X.till, Gt = X.gantry;
      add('cooler_wall', C.x0, C.x1, C.y - C.d / 2, C.y + C.d / 2, C.h, 'reach-in coolers, ' + C.doors + ' doors');
      add('coffee_run', K.x0, K.x1, K.y - K.d / 2, K.y + K.d / 2, K.h + 0.44, 'counter with brewers and fountain tower');
      add('service_counter', T.x - T.d / 2, T.x + T.d / 2, T.y0, T.y1, T.h, 'staff side is the -x aisle');
      add('gantry', Gt.x - Gt.d / 2, Gt.x + Gt.d / 2, Gt.y0, Gt.y1, Gt.h, 'the only wall-height fixture in the room');
      X.gond.forEach((G, i) => add('gondola_' + (i + 1), G.x0, G.x1, G.y - 0.46, G.y + 0.46, 1.04, 'double-sided island run'));
      add('atm', ix - 0.62, ix - 0.02, 1.96, 2.64, 1.56, 'against the +x wall');
    }
    return {
      SOLE: [{
        id: 'sales_floor', winding: 'ccw_from_above', z: F2(fz),
        polygon: [[F2(-ix), F2(-iy)], [F2(ix), F2(-iy)], [F2(ix), F2(iy)], [F2(-ix), F2(iy)]],
        surface: 'tile', clear_height_m: F2(B.clear),
        note: 'Inside face of the walls, generated from StationIso.shell(). One floor, one owner — the exterior file does not repeat it.',
        _notes: obst,
      }],
      INTERACT: interactables(size).INTERACT,
    };
  }
  /* reach_point is derived here from which fixture the item backs onto, then TESTED downstream:
     StationIso.auditReach runs every point in this list against the sales-floor polygon and the
     obstructions published beside it, and marches or nulls anything that does not clear. visible_facings
     is computed with the rasteriser's own camera-facing test on the wall the item stands against, so a
     wall-backed item is only listed for the facings where its wall survives the section cut. */
  function interactables(size) {
    size = ST.resolveSize('store', size);
    const X = fixtures(size), B = X.shell;
    const N = { front: [0, 1], back: [0, -1], left: [-1, 0], right: [1, 0] };
    const out = X.list.map((e) => {
      const dirs = [];
      for (let d = 0; d < 8; d++) {
        if (!e.wall) { dirs.push(ST.order[d]); continue; }
        const n = N[e.wall];
        if (!faces(n[0], n[1], d)) dirs.push(ST.order[d]);   // the wall it backs onto survives the cut
      }
      const off = e.wall === 'back' ? [0, 0.80] : e.wall === 'front' ? [0, -0.80] : e.wall === 'left' ? [0.80, 0] : e.wall === 'right' ? [-0.80, 0] : [0, 0.80];
      return { id: e.id, label: e.label, action: e.action, pos: [F2(e.x), F2(e.y), F2(e.z)],
        reach_point: [F2(e.x + off[0]), F2(e.y + off[1]), F2(B.floorZ)],
        visible_facings: dirs, level: 'sales_floor' };
    });
    return { INTERACT: out };
  }
  function contract() {
    return {
      rig: 'Art/stationInteriorRig.js', exposes: 'globalThis.StationInterior',
      cellOwner: 'Art/gasStationRig.js — StationIso.cell("store", size, opts)',
      unit: '32 px = 1 m', camera: '3/4, 45deg steps, elev 40deg (shared with the fleet and the forecourt)',
      origin: 'ground-centre of the storefront footprint, identical to the shell',
      sectionCut: 'per facing: a wall whose outside is camera-facing is not drawn',
      ceiling: 'not drawn; six recessed troffers hang where it would be',
      rock: 'none — a building ashore does not move (boat interiors do)',
      sizes: Object.keys(ST.TYPES.store.sizes).map((k) => {
        const B = ST.shell(k), c = cell(k, {});
        return { size: k, label: B.label, inner: [F2(B.inner.hw * 2), F2(B.inner.hd * 2)],
                 clear: F2(B.clear), floorZ: F2(B.floorZ), cell: { W: c.W, H: c.H }, pivot: { x: c.cx, y: c.cy } };
      }),
    };
  }

  root.StationInterior = { S, DEG, defaultElev: DEFAULT_ELEV, DIRS: 8, order: ST.order,
    ramps: { TILE, LAM, P1, P2, P3, P4 },
    cell, render, mount, fixtures, interactables, gameplaySections, contract };
})(typeof globalThis !== 'undefined' ? globalThis : window);
