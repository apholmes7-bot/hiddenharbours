/* Hidden Harbours — ROD BOBBER runtime-FX sprite (fishing iso, Wave 1).
   The line's float, purpose-made: the 2×3 runtime rects can't speak, and the lobster buoy
   (24×48 = 1 m of gear) reads as a moored marker, not tackle. This is a tiny hand-plotted
   sprite in the rod kit's own colours — RUST ramp body + white band + white tip, no invented
   colours, no AA, 1px #101a19 keyline on the above-water silhouette only.
   Cell 16×22, pivot (8,12) = THE WATERLINE POINT. Pixels at/below the pivot row are baked
   with an underwater tint (mixed toward water, alpha 165) so the runtime never clips —
   blit the cell at RodIso.project()'s surface point and the dip states just work.
   STATES (bite language): float 4f gentle roll · nibble 4f quick dips (fish mouthing) ·
   strike 4f thrash, pulled under (hook it now) · fly 2f airborne (cast arc — no waterline,
   full keyline). Replaces the two fillRects at the line end; the line still attaches at
   the stem top (pivot.y - 6 + dip when floating).
   Exposes globalThis.RodBobber = { W,H,pivot,KEY,STATES,ORDER,POSE,COLORS,render,sheetOrder }. */
(function (root) {
  const W = 16, H = 22, PX = 8, PY = 12;
  const KEY = '#101a19';
  // rod-kit master ramps only
  const C = { R:'#cf3626', RH:'#e2573c', RD:'#a8241b', W:'#eef0ea', WS:'#c3ced2', DK:'#25211a' };
  const WATER = '#123034', UW_MIX = 0.45, UW_A = 165;
  const STATES = {
    float:  { n:4, ms:240 },   // gentle roll — runtime adds the ±1px bob
    nibble: { n:4, ms:100 },   // quick shallow dips
    strike: { n:4, ms:110 },   // thrash + pulled under
    fly:    { n:2, ms:120 },   // airborne during the cast arc
  };
  const ORDER = ['float','nibble','strike','fly'];
  const POSE = {                                  // d = px below waterline, t = tilt
    float:  [{d:0,t:0},{d:0,t:-1},{d:0,t:0},{d:0,t:1}],
    nibble: [{d:1,t:0},{d:2,t:1},{d:1,t:-1},{d:0,t:0}],
    strike: [{d:2,t:1},{d:5,t:0},{d:3,t:-1},{d:6,t:0}],
    fly:    [{d:0,t:-1},{d:0,t:1}],
  };
  // body rows, top → bottom: white tip · stem · red egg (3 rows) · white band (2) · keel
  function rows(t){
    return [
      [0, [[t,'W']]],
      [1, [[t,'DK']]],
      [2, [[t-1,'RH'],[t,'R'],[t+1,'RD']]],
      [3, [[-2,'RH'],[-1,'R'],[0,'R'],[1,'R'],[2,'RD']]],
      [4, [[-2,'R'],[-1,'R'],[0,'R'],[1,'RD'],[2,'RD']]],
      [5, [[-2,'W'],[-1,'W'],[0,'W'],[1,'WS'],[2,'WS']]],
      [6, [[-1,'WS'],[0,'WS'],[1,'WS']]],
      [7, [[0,'DK']]],
    ];
  }
  const hex2 = (h)=>[parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)];
  const WRGB = hex2(WATER);
  function render(state, f){
    const poses = POSE[state] || POSE.float;
    const p = poses[((f%poses.length)+poses.length)%poses.length];
    const fly = state === 'fly';
    const grid = new Array(W*H).fill(null);      // 'matKey' | 'KEY'
    const yTop = PY - 6 + (fly ? 0 : p.d);
    for (const [dy, px] of rows(p.t)){
      const y = yTop + dy; if (y<0 || y>=H) continue;
      for (const [dx, m] of px){
        const x = PX + dx; if (x<0 || x>=W) continue;
        grid[y*W+x] = m;
      }
    }
    // keyline on the above-water silhouette (whole silhouette when airborne)
    const add=[];
    for (let y=0;y<H;y++) for (let x=0;x<W;x++){
      if (grid[y*W+x]) continue;
      if (!fly && y>=PY) continue;
      for (const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){
        const nx=x+dx, ny=y+dy;
        if (nx<0||nx>=W||ny<0||ny>=H) continue;
        const g = grid[ny*W+nx];
        if (g && g!=='KEY' && (fly || ny<PY)){ add.push([x,y]); break; }
      }
    }
    for (const [x,y] of add) grid[y*W+x]='KEY';
    const out = new Uint8ClampedArray(W*H*4);
    for (let y=0;y<H;y++) for (let x=0;x<W;x++){
      const g = grid[y*W+x]; if (!g) continue;
      const i = (y*W+x)*4;
      let rgb = g==='KEY' ? hex2(KEY) : hex2(C[g]);
      let a = 255;
      if (!fly && y>=PY && g!=='KEY'){           // baked underwater tint
        rgb = rgb.map((v,k)=>Math.round(v*(1-UW_MIX)+WRGB[k]*UW_MIX));
        a = UW_A;
      }
      out[i]=rgb[0]; out[i+1]=rgb[1]; out[i+2]=rgb[2]; out[i+3]=a;
    }
    return out;
  }
  // flat frame list for the sheet: float f0-3 · nibble f0-3 · strike f0-3 · fly f0-1
  function sheetOrder(){
    const o=[]; for (const s of ORDER) for (let f=0;f<STATES[s].n;f++) o.push({state:s,f});
    return o;
  }
  root.RodBobber = { W, H, pivot:{x:PX,y:PY}, KEY, STATES, ORDER, POSE, COLORS:C, render, sheetOrder };
})(typeof globalThis!=='undefined'?globalThis:window);
