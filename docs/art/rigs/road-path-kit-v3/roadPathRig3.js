/* Hidden Harbours — ROAD / PATH / SIDEWALK / APRON kit  (v3 — 2026-08-12 "stand it up" rebuild).

   v2 fixed light, palette and ground transparency, but it still READ TOP-DOWN: its vertical scale
   (12 px = 1 m) was borrowed from wharfKitRig's water faces, less than half the vertical every
   OTHER rig in the repo draws. A kerb was 1.5 px. Nothing cast a shadow. Every edge was a straight
   tile edge. v3 keeps v2's contracts (blob-47, global-metre patterns, verbatim ramps, ringless,
   one shared key) and rebuilds the RENDERER:

   · VERTICAL = THE TURNTABLE'S.  ZS = cos(40°)·32 ≈ 24.5 px/m — the same ruler as every building
     wall (houseIso / wharfBuilding / shopBuilding / utilityIso) and ShoreIso2's cliff bands
     (32 px ≈ 1.3 m). A 155 mm sidewalk kerb is now a 4 px face, a 0.65 m quay-apron drop is the
     16 px face wharfKit's own quay cap shows.
   · WHARFKIT FACE LANGUAGE.  Height shows the way the ground kits already show it: SOUTH edges
     carry the vertical FACE; north edges a lit rim arris; east silhouettes a darkened arris.
     No four-sided micro-boxes.
   · OCCLUSION IS BAKED (ShoreIso2 v8 canon).  Every face seats on an alpha contact-shadow band
     falling down-screen, so a sidewalk sits ON the street the way a pole or a building sits on
     its pad. Shadow pixels ship as real alpha — composite the tile with blending on.
   · ABUTMENT.  opts.abut marks sides where a DIFFERENT paved surface continues (sidewalk↔road).
     Those edges get the shared kerb face and nothing else — no verge, no nibble, no soil gutter
     between a carriageway and its own footway (v2's mainStreet tell).
   · NOT A PERFECT TILE.  Free edges wander on clumpy global-metre noise (soft surfaces ±0.12 m,
     kerbed ±0.05 m) — coherent across seams, so the wobble never cracks. And render() has a
     sibling: renderFree(surface, opts) takes a COVERAGE FIELD sampler (0..1 in global metres)
     instead of neighbour booleans, so a painted path can curve, fork, fatten and thin with no
     grid in it at all. Same shading, same edges, same kerbs, edge lines follow the curve.
   · SPILL FOLLOWS THE GROUND.  opts.ground ('grass'|'dirt'|'sand'|'shingle'|'deck'|'none')
     picks the spill material — dirt clumps no longer land on clean shingle or a wharf deck.

   CELL — 32 × 64. Ground square (z = 0) is rows 10..41; rows 0..9 headroom (boardwalk decking
   0.35 m ≈ 9 px); rows 42..63 take drops + shadow. Blit at (tileX·32, tileY·32 − 10).
   COMPOSITE IN TWO PASSES, painter N→S:  pass 1 blit rows 0..41 of every cell; pass 2 blit the
   SKIRT (rows 42..63, faces + drops + shadows) of every cell in the same order. The skirt of a
   cell overlaps the plan square of the cell below it; pass 2 keeps faces in front of that
   neighbour's flat top. Shadow pixels have alpha < 255 — draw with normal blending.

   API (globalThis.RoadKit3): TILE 32 · CELL_H 64 · HEAD 10 · SKIRT 42 · S 32 · ZS 24.5
     SURFACES / WEAR / PROFILES / PIECES / GROUNDS / SURF / PAL / MATS — as v2
     render(surface, opts)      opts = v2's { con, diag, axis, wear, profile, piece, markings,
                                drop, kerb, gx, gy, seed, cross } PLUS
                                abut:{n,e,s,w}  abutZ:{n,e,s,w metres}  ground:'grass'|…
     renderFree(surface, opts)  opts = { cov:(u,v m)=>0..1, gx, gy, seed, wear, profile, kerb,
                                axis, ground, markings:['edge'] only } — uncached
     renderGround(mat, opts) · gameplay(surface, opts) · BLOB47 · canon · fromMask · topPoly     */
(function (root) {
  'use strict';
  const TILE = 32, CELL_H = 64, HEAD = 10, SKIRT = HEAD + TILE, S = 32, ZS = 24.5;
  const DEG = Math.PI / 180, LIGHT_ELEV = 40;

  // ---- harbour master ramps — lifted verbatim, identical to v2. Do not fork. ------------------
  const CONCRETE = ['#55564f','#686962','#7c7d74','#919287','#a6a79b','#bbbcae'];
  const ASPHALT  = ['#1e2124','#272b2f','#33383c','#41474b','#51585c','#646b6f'];
  const GRAVEL   = ['#3b3a33','#4b493f','#5c5a4d','#6f6c5c','#83806e','#979382'];
  const DIRT     = ['#2f2419','#3d2f21','#4b3b2a','#5a4934','#6b5941','#7c6a50'];
  const GRASS    = ['#1f2a18','#2a3820','#35462a','#425536','#516644','#627955'];
  const ROCK     = ['#252b31','#3a434c','#525a64','#6a727b','#848c94','#9aa2a9'];
  const BRICK    = ['#3a201a','#552b20','#6e3728','#874634','#a05743','#b96b55'];
  const PLANK    = ['#454037','#565045','#676054','#7a7265','#8d8477','#a1988a'];
  const WOOD     = ['#553d23','#6b4e30','#7f603b','#957749','#ab8d5f','#c1a578'];
  const POLE     = ['#33291f','#42342a','#523f33','#63503f','#75604c','#8a7460'];
  const YEL      = ['#6a4d17','#8e6a20','#b8923c','#cfa945','#e0bd58','#eed37e'];
  const WHITEP   = ['#4f5452','#666b68','#7f847f','#989c96','#b1b5ae','#cbcec6'];
  const SALT     = ['#7d7f78','#93958c','#a8aa9f','#bcbeb2','#cfd1c4','#e2e4d6'];
  const IRON     = ['#111216','#1c1e23','#2a2d33','#3a3e46','#4d525a','#636970'];
  const SAND     = ['#6f4529','#8f5c35','#b07846','#cb995e','#e0b87d','#efd5a3'];
  function hex2rgb(h){ return [parseInt(h.slice(1,3),16), parseInt(h.slice(3,5),16), parseInt(h.slice(5,7),16)]; }
  function rgb2hex(r,g,b){ const h=(n)=>Math.max(0,Math.min(255,Math.round(n))).toString(16).padStart(2,'0'); return '#'+h(r)+h(g)+h(b); }
  function mix(a,b,t){ const A=hex2rgb(a), B=hex2rgb(b); return rgb2hex(A[0]+(B[0]-A[0])*t, A[1]+(B[1]-A[1])*t, A[2]+(B[2]-A[2])*t); }
  const OILED = DIRT.map((c,i)=>mix(c, ASPHALT[i], 0.44));
  const PAL = { CONCRETE, ASPHALT, GRAVEL, DIRT, GRASS, ROCK, BRICK, PLANK, WOOD, POLE, YEL, WHITEP, SALT, IRON, SAND, OILED };

  // ---- one shared key (recipe + constants identical to the whole family) ----------------------
  const GAIN = 3.1, BIAS = 2.55;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));
  const LSE = Math.sin(LIGHT_ELEV*DEG), LCE = Math.cos(LIGHT_ELEV*DEG);
  function shadeOf(n){ return n[0]*LN[0] + (n[1]*LSE + n[2]*LCE)*LN[1] + (-n[1]*LCE + n[2]*LSE)*LN[2]; }
  function shadeSlope(sx, sy){ const m = Math.hypot(sx, sy, 1); return shadeOf([sx/m, sy/m, 1/m]); }
  const FLAT_SHADE = shadeSlope(0,0);            // ≈0.906 · the family's lit ground plane
  const FACE_SHADE = shadeOf([0,-1,0]);          // ≈−0.066 · a south face — 3 ramp steps darker

  function hash(a,b,s){ let h=(a*374761393 + b*668265263 + ((s|0))*1274126177)|0;
    h=Math.imul(h^(h>>>13),1274126177); return ((h^(h>>>16))>>>0)/4294967296; }
  const fract=(v)=>v-Math.floor(v), clamp=(v,a,b)=>v<a?a:v>b?b:v;
  const ss=(x,a,b)=>{ const t=clamp((x-a)/(b-a),0,1); return t*t*(3-2*t); };
  function clumpN(u,v,seed){ const cu=Math.floor(u*11), cv=Math.floor(v*11), fu=u*11-cu, fv=v*11-cv;
    const a=hash(cu,cv,seed), b=hash(cu+1,cv,seed), c=hash(cu,cv+1,seed), d=hash(cu+1,cv+1,seed);
    const su=fu*fu*(3-2*fu), sv=fv*fv*(3-2*fv);
    return (a+(b-a)*su) + ((c+(d-c)*su)-(a+(b-a)*su))*sv; }

  // ---- surface relief fields (unchanged craft from v2, global metres) -------------------------
  function cellOf(u,v,size,sd){ const cu=Math.floor(u/size), cv=Math.floor(v/size);
    let best=1e9,bi=0,bj=0,bx=0,by=0;
    for(let j=-1;j<=1;j++) for(let i=-1;i<=1;i++){ const gu=cu+i, gv=cv+j;
      const px=(gu+0.25+hash(gu,gv,sd)*0.5)*size, py=(gv+0.25+hash(gu,gv,sd+7)*0.5)*size;
      const d=(u-px)*(u-px)+(v-py)*(v-py); if(d<best){best=d;bi=gu;bj=gv;bx=px;by=py;} }
    return {i:bi,j:bj,cx:bx,cy:by,d:Math.sqrt(best)}; }
  function dome(u,v,size,sd,crown,joint){ const c=cellOf(u,v,size,sd);
    const r=size*0.52, t=clamp(c.d/r,0,1.4), k=crown*t*2.2;
    const dx=c.d>1e-5?(u-c.cx)/c.d:0, dy=c.d>1e-5?(v-c.cy)/c.d:0;
    const tint=(hash(c.i,c.j,sd+3)*2.0-1.0);
    if(t>1) return [dx*joint*-1.6, dy*joint*-1.6, -2.2+tint*0.3];
    return [dx*k, dy*k, tint]; }
  function grain(u,v,sd,amp){ const X=Math.floor(u*S), Y=Math.floor(v*S);
    return [(hash(X,Y,sd)*2-1)*amp,(hash(X,Y,sd+11)*2-1)*amp,0]; }
  const FIELDS = {
    dirt(u,v,c){ const X=Math.floor(u*S),Y=Math.floor(v*S),h=hash(X,Y,c.seed+1);
      let sx=0,sy=0,tint=h<0.14?-1:h<0.30?1:0; if(h<0.045)tint=-2;
      const across=c.axis==='h'?v:u;
      const rut=Math.sin((across+0.24)*Math.PI*2/0.62), rs=Math.cos((across+0.24)*Math.PI*2/0.62)*0.9*c.ruts;
      if(c.axis==='h')sy+=rs; else sx+=rs; tint+=rut<-0.55?-1:0;
      const g=grain(u,v,c.seed+2,0.55); return [sx+g[0],sy+g[1],tint]; },
    oiledDirt(u,v,c){ const r=FIELDS.dirt(u,v,c); return [r[0]*0.7,r[1]*0.7,r[2]*0.6+(hash(Math.floor(u*S),Math.floor(v*S),c.seed+9)<0.10?1:0)]; },
    gravel(u,v,c){ const d=dome(u,v,0.095,c.seed+4,1.15,0.8); return [d[0],d[1],d[2]*0.9]; },
    shell(u,v,c){ const cl=cellOf(u,v,0.075,c.seed+5), face=hash(cl.i,cl.j,c.seed+6), tilt=(face-0.5)*1.5;
      const edge=cl.d>0.075*0.44; return [tilt,(hash(cl.i,cl.j,c.seed+8)-0.5)*1.0, edge?-1.6:(face>0.72?1:face<0.22?-1:0)]; },
    sand(u,v,c){ const ph=u*6.1+Math.sin(v*1.9)*1.2+c.seed*0.7, rip=Math.sin(ph), sx=Math.cos(ph)*0.30;
      const g=grain(u,v,c.seed+12,0.38); return [sx+g[0],g[1],rip>0.86?1:rip<-0.86?-1:0]; },
    concrete(u,v,c){ const near=(t)=>t<0.03||t>0.97; let sx=0,sy=0,tint=0;
      if(near(fract(u))){sx=(fract(u)<0.5?-1:1)*1.5;tint=-2;} if(near(fract(v))){sy=(fract(v)<0.5?-1:1)*1.5;tint=-2;}
      const X=Math.floor(u*S),Y=Math.floor(v*S),h=hash(X,Y,c.seed+13); if(!tint)tint=h<0.07?-1:h<0.13?1:0;
      const g=grain(u,v,c.seed+14,0.30); return [sx+g[0],sy+g[1],tint]; },
    apron(u,v,c){ const f=fract(v/0.62); let sy=0,tint=0;
      if(f<0.055){sy=-1.4;tint=-2;} if(fract(u/1.22)<0.03)tint=Math.min(tint,-1);
      if(hash(Math.floor(u*S),Math.floor(v*S),c.seed+15)<0.24)tint-=1;
      const g=grain(u,v,c.seed+16,0.26); return [g[0],sy+g[1],tint]; },
    asphalt(u,v,c){ const X=Math.floor(u*S),Y=Math.floor(v*S),h=hash(X,Y,c.seed+17);
      let tint=h<0.13?1:h<0.20?2:h<0.30?-1:0; if(hash(Math.floor(u*4),Math.floor(v*4),c.seed+18)<0.16)tint-=1;
      const g=grain(u,v,c.seed+19,0.48); return [g[0],g[1],tint]; },
    cobble(u,v,c){ return dome(u,v,0.16,c.seed+20,1.35,1.1); },
    brick(u,v,c){ const bw=0.205,bh=0.098,course=Math.floor(v/bh),off=(course&1)?bw*0.5:0;
      const lu=fract((u+off)/bw), lv=fract(v/bh);
      if(lu<0.075||lv<0.10) return [lu<0.075?-1.5:0, lv<0.10?-1.5:0, -2.4];
      const cu=(lu-0.5)*2, cv=(lv-0.5)*2, b=Math.floor((u+off)/bw);
      return [cu*0.75, cv*0.55, (hash(b,course,c.seed+21)*2.4-1.2)]; },
    boardwalk(u,v,c){ const along=c.axis==='h'?u:v, pw=0.152, lp=fract(along/pw), idx=Math.floor(along/pw);
      const gap=lp<0.085, cup=(lp-0.54)*1.05;
      const tint=(hash(idx,Math.floor((c.axis==='h'?v:u)/1.6),c.seed+22)*2.0-1.0);
      const sA=gap?-2.0:cup, sB=grain(u,v,c.seed+23,0.22)[0];
      return c.axis==='h'?[sA,sB,gap?-2.6:tint]:[sB,sA,gap?-2.6:tint]; },
    grass(u,v,c){ const g=grain(u,v,c.seed+30,0.7), cl=cellOf(u,v,0.28,c.seed+31);
      return [g[0],g[1],(hash(cl.i,cl.j,c.seed+32)*2.2-1.1)+(hash(Math.floor(u*S),Math.floor(v*S),c.seed+33)<0.16?1:0)]; },
    shingle(u,v,c){ const d=dome(u,v,0.20,c.seed+34,1.5,1.2); return [d[0],d[1],d[2]]; },
  };
  function wearWrap(base,c){ const wf=c.wf; if(wf<=0.001) return (u,v)=>base(u,v,c);
    return function(u,v){ const r=base(u,v,c); let sx=r[0],sy=r[1],tint=r[2];
      tint-=(hash(Math.floor(u*S)>>1,Math.floor(v*S)>>1,c.seed+60)<wf*0.22)?1:0;
      const A=5.44,B=4.16,C=1.92;
      const f=Math.sin(u*A+c.seed)+Math.sin(v*B+c.seed*1.7)+Math.sin((u+v)*C+c.seed*2.3);
      const g=f-Math.round(f), w=0.05+wf*0.055;
      if(Math.abs(g)<w){ const du=A*Math.cos(u*A+c.seed)+C*Math.cos((u+v)*C+c.seed*2.3);
        const dv=B*Math.cos(v*B+c.seed*1.7)+C*Math.cos((u+v)*C+c.seed*2.3);
        const m=Math.hypot(du,dv)||1, s=Math.sign(g)||1;
        sx+=-s*(du/m)*2.0; sy+=-s*(dv/m)*2.0;
        if(Math.abs(g)<w*0.35)tint-=2;
        if(wf>0.72&&hash(Math.floor(u*S),Math.floor(v*S),c.seed+63)<0.13)tint=-3; }
      if(wf>0.72){ const P=0.7,pu=Math.floor(u/P),pv=Math.floor(v/P);
        if(hash(pu,pv,c.seed+64)<0.30){ const cxp=(pu+0.2+hash(pu,pv,c.seed+65)*0.6)*P, cyp=(pv+0.2+hash(pu,pv,c.seed+66)*0.6)*P;
          const rr=0.055+hash(pu,pv,c.seed+67)*0.075, dx=u-cxp,dy=v-cyp,d=Math.hypot(dx,dy);
          if(d<rr){ const k=2.6*(d/rr); sx+=(dx/(d||1))*k; sy+=(dy/(d||1))*k; tint-=2-Math.round((d/rr)*1.4); } } }
      return [sx,sy,tint]; }; }

  // ---- surfaces (v2 table; boardwalk now a real 0.35 m deck on piles) --------------------------
  const SURF = {
    dirt      : { label:'dirt',       ramp:DIRT,     seat:-2.35, lift:0.010, field:'dirt',      edge:'soft',   marks:false, grip:0.72, foot:'earth',  ruts:1.0 },
    oiledDirt : { label:'oiled dirt', ramp:OILED,    seat:-2.75, lift:0.012, field:'oiledDirt', edge:'soft',   marks:false, grip:0.80, foot:'earth',  ruts:0.7 },
    gravel    : { label:'gravel',     ramp:GRAVEL,   seat:-2.10, lift:0.030, field:'gravel',    edge:'soft',   marks:false, grip:0.68, foot:'gravel', ruts:0.8 },
    shell     : { label:'clam shell', ramp:SALT,     seat:-1.85, lift:0.028, field:'shell',     edge:'soft',   marks:false, grip:0.66, foot:'shell',  ruts:0.6 },
    sand      : { label:'sand',       ramp:SAND,     seat:-2.20, lift:0.014, field:'sand',      edge:'soft',   marks:false, grip:0.55, foot:'sand',   ruts:0.4 },
    concrete  : { label:'concrete',   ramp:CONCRETE, seat:-1.75, lift:0.030, field:'concrete',  edge:'kerb',   marks:true,  grip:0.90, foot:'stone',  ruts:0 },
    apron     : { label:'wharf apron',ramp:CONCRETE, seat:-1.45, lift:0.026, field:'apron',     edge:'kerb',   marks:true,  grip:0.88, foot:'stone',  ruts:0 },
    asphalt   : { label:'asphalt',    ramp:ASPHALT,  seat:-2.05, lift:0.042, field:'asphalt',   edge:'kerb',   marks:true,  grip:0.94, foot:'stone',  ruts:0.3 },
    cobble    : { label:'cobble',     ramp:ROCK,     seat:-2.15, lift:0.048, field:'cobble',    edge:'kerb',   marks:false, grip:0.74, foot:'stone',  ruts:0.2 },
    brick     : { label:'brick',      ramp:BRICK,    seat:-2.15, lift:0.040, field:'brick',     edge:'kerb',   marks:false, grip:0.80, foot:'stone',  ruts:0 },
    boardwalk : { label:'boardwalk',  ramp:PLANK,    seat:-1.90, lift:0.350, field:'boardwalk', edge:'timber', marks:false, grip:0.78, foot:'plank',  ruts:0 },
  };
  const KERB_SEAT=-1.75, TIMBER_SEAT=-2.10;
  const SURFACES=['dirt','oiledDirt','gravel','shell','sand','concrete','apron','asphalt','cobble','brick','boardwalk'];
  const WEAR=['new','worn','cracked'], PROFILES=['path','sidewalk','lane','road2'], PIECES=['road','apron','slipway','driveway','shoulder'];
  const PROFILE_Z={ path:0.00, sidewalk:0.125, lane:0.02, road2:0.02 };
  const WOB={ soft:0.12, kerb:0.05, timber:0.035 };
  const wearNum=(w)=>w==='cracked'?1:w==='worn'?0.5:0;
  const SPILL={ grass:{ramp:DIRT,i:2}, dirt:{ramp:DIRT,i:2}, sand:{ramp:SAND,i:1}, shingle:{ramp:ROCK,i:2}, deck:null, none:null };

  // ---- blob-47 top polygon (v2, verbatim) ------------------------------------------------------
  function topPoly(con,diag,vg,gx,gy,seed){
    const H=0.5, r=vg*1.15, f=vg*1.55;
    const J=(a,b,k)=>(hash(a,b,seed+70)*2-1)*k, out=[];
    const corners=[
      {sx:+1,sy:+1,xs:'e',ys:'n',dg:'ne',fromX:true},{sx:-1,sy:+1,xs:'w',ys:'n',dg:'nw',fromX:false},
      {sx:-1,sy:-1,xs:'w',ys:'s',dg:'sw',fromX:true},{sx:+1,sy:-1,xs:'e',ys:'s',dg:'se',fromX:false}];
    for(const c of corners){
      const cx=!!con[c.xs], cy=!!con[c.ys], dg=!!diag[c.dg];
      // jitter hashed on the GLOBAL corner, so both cells sharing it agree exactly
      const gcx=gx+(c.sx>0?1:0), gcy=gy+(c.sy>0?1:0);
      const jx=J(gcx,gcy,vg*0.30), jy=J(gcy,gcx,vg*0.30);
      let pts;
      if(cx&&cy&&dg)pts=[[0,0]];
      else if(cx&&cy)pts=[[0,f],[f*0.30,f*0.30],[f,0]];
      else if(cx&&!cy)pts=[[0,vg+jy]];
      else if(!cx&&cy)pts=[[vg+jx,0]];
      else pts=[[vg+jx,vg+jx+r],[vg+jx+r*0.30,vg+jy+r*0.30],[vg+jy+r,vg+jy]];
      if(!c.fromX)pts=pts.slice().reverse();
      for(const p of pts)out.push([c.sx*(H-p[0]),c.sy*(H-p[1])]);
    }
    return out;
  }
  function pointIn(poly,x,y){ let inside=false;
    for(let i=0,j=poly.length-1;i<poly.length;j=i++){ const a=poly[i],b=poly[j];
      if(((a[1]>y)!==(b[1]>y))&&(x<(b[0]-a[0])*(y-a[1])/(b[1]-a[1])+a[0]))inside=!inside; }
    return inside; }
  const E=1e-6;
  function segSide(p,q){ // which cell border a segment lies on, or null
    if(Math.abs(p[0]-0.5)<E&&Math.abs(q[0]-0.5)<E)return 'e';
    if(Math.abs(p[0]+0.5)<E&&Math.abs(q[0]+0.5)<E)return 'w';
    if(Math.abs(p[1]-0.5)<E&&Math.abs(q[1]-0.5)<E)return 'n';
    if(Math.abs(p[1]+0.5)<E&&Math.abs(q[1]+0.5)<E)return 's';
    return null; }

  // ---- markings (v2, phased on global metres) --------------------------------------------------
  function markAt(u,v,lx,ly,c){
    const M=c.markings; if(!M||!M.length)return null;
    const across=c.axis==='h'?ly:lx, along=c.axis==='h'?u:v;
    const L=c.cross.l, Rt=c.cross.r, half=(L+Rt+1)/2, rel=across-(Rt-L)/2;
    const has=(k)=>M.indexOf(k)>=0, W=0.048;
    if(has('crosswalk')){ if(fract(along/0.50)<0.52&&Math.abs(rel)<half-0.07)return WHITEP; return null; }
    if(has('stop')&&Math.abs(along-Math.round(along))<0.06&&Math.abs(rel)<half-0.06)return WHITEP;
    if(has('edge')){ if(L===0&&Math.abs(rel+(half-0.15))<W)return WHITEP;
      if(Rt===0&&Math.abs(rel-(half-0.15))<W)return WHITEP; }
    if(has('centerDouble')){ const a=Math.abs(rel); if(a>0.022&&a<0.070)return YEL; }
    if(has('centerDash')&&Math.abs(rel)<W&&fract(along/1.7)<0.56)return YEL;
    if(has('curb')&&Math.abs(Math.abs(across)-0.47)<0.05)return YEL;
    return null; }

  // =============================================================================================
  // THE MARCH — one renderer for grid tiles, freeform coverage and preview ground.
  // Near-plan columns walked N→S; a drop between rows extrudes a face at ZS px/m; faces seat on
  // alpha contact shadow. South faces carry the height; north edges get a lit rim arris.
  // =============================================================================================
  function march(o){
    const N=TILE*CELL_H;
    const rbuf=new Array(N).fill(null), ibuf=new Int16Array(N), sbuf=new Uint8Array(N);
    const zarr=new Float32Array(TILE*(TILE+1)), darr=new Float32Array(TILE*(TILE+1));
    const spec=o.spec, fieldFn=o.fieldFn, seed=o.seed;
    const kerbTimber=spec.edge==='timber';
    const kerbRamp=kerbTimber?WOOD:CONCRETE, kerbSeat=kerbTimber?TIMBER_SEAT:KERB_SEAT;
    const spill=o.spill;
    const put=(x,y,ramp,idx)=>{ if(y<0||y>=CELL_H)return; const i=y*TILE+x;
      rbuf[i]=ramp; ibuf[i]=clamp(idx,0,ramp.length-1); };
    const shade=(x,y,alpha)=>{ if(y<0||y>=CELL_H)return; const i=y*TILE+x;
      if(rbuf[i]){ if(rbuf[i]===spill?.ramp) ibuf[i]=Math.max(0,ibuf[i]-1); return; }
      if(alpha>sbuf[i])sbuf[i]=alpha; };
    const paintBase={ [WHITEP]:4.4, [YEL]:3.6 };
    for(let x=0;x<TILE;x++){
      const lx=(x+0.5)/TILE-0.5, u=o.u0+lx;
      let pz=null, psy=0, pkerb=false, pkind='free', pside='s';
      for(let r=0;r<=TILE;r++){
        const ly=0.5-(r+0.5)/TILE, v=o.v0+ly;
        let smp=null;
        if(r<TILE || o.openS===false){
          const q=o.at(lx, r<TILE?ly:-0.5-0.5/TILE, u, r<TILE?v:o.v0-0.5-0.5/TILE);
          if(q)smp=q;
        }
        if(r===TILE && o.contS){ // S neighbour carries on: close without a face
          pz=null; break; }
        if(smp){
          let {z,d,kind,side,kerb}=smp;
          const sy=HEAD+r-Math.round(z*ZS);
          if(r<TILE){ zarr[r*TILE+x]=z; darr[r*TILE+x]=d; }
          // nibble: soft surfaces crumble at the free edge
          let skipTop=false;
          if(spec.edge==='soft'&&kind==='free'&&d<0.045&&clumpN(u*2.3,v*2.3,seed+96)>0.46+d*9) skipTop=true;
          if(pz!=null && sy>psy+1){ // drop going S within/at surface: face or slope
            const interior=o.slope&&d>0.10;
            for(let fy=psy+1;fy<sy;fy++){
              if(interior){ // slipway ramp: fill with the SAME shading as the top texels (fall is baked in the field)
                const rr=fieldFn(u,v); const fi=shadeSlope(rr[0],rr[1])*GAIN+BIAS+spec.seat+rr[2];
                put(x,fy,spec.ramp,Math.floor(fi)+((fi-Math.floor(fi))>BAYER[x&3][fy&3]?1:0)); }
              else{ const fr=pkerb?kerbRamp:(o.faceRamp||spec.ramp), fseat=pkerb?kerbSeat:spec.seat;
                let ft=faceTex(o,u,fy-psy,pkerb,kerbTimber);
                if(ft===null){ shade(x,fy,120); continue; }
                let fi=FACE_SHADE*GAIN+BIAS+fseat*0.4+ft;
                if(fy===sy-1)fi-=1;
                put(x,fy,ft===-99?POLE:fr,Math.floor(fi)+((fi-Math.floor(fi))>BAYER[x&3][fy&3]?1:0)); }
            }
          }
          if(!skipTop){
            const rr=fieldFn(u,v);
            let ramp=kerb?kerbRamp:spec.ramp, seatv=kerb?kerbSeat:spec.seat;
            let fsx=rr[0],fsy=rr[1],tint=rr[2];
            if(kerb){ const along=o.axis==='h'?u:v; // kerbstone run: joints on ~0.9 m
              tint=fract(along/0.92)<0.055?-2:(hash(Math.floor(u*S),Math.floor(v*S),seed+82)<0.16?-1:0); fsx=0;fsy=0; }
            let fi=shadeSlope(fsx,fsy)*GAIN+BIAS+seatv+tint;
            if(pz==null&&z>0.03)fi+=1;                       // N rim arris on a raised slab
            if(pz!=null&&psy>sy)fi+=0.6;                     // rising step: lit arris
            // markings on true top only
            if(!kerb&&spec.marks&&o.marks){
              const m=o.marks(u,v,lx,ly,d);
              if(m){ const wr=hash(Math.floor(u*S),Math.floor(v*S),seed+90);
                if(wr>=o.wf*0.42){ const delta=clamp(Math.round(fi-(FLAT_SHADE*GAIN+BIAS+seatv)),-2,1);
                  ramp=m; fi=paintBase[m]+delta+(wr<0.3?-1:0); } } }
            const base=Math.floor(fi);
            put(x,sy,ramp,base+((fi-base)>BAYER[x&3][sy&3]?1:0));
          }
          pz=z; psy=sy; pkerb=kerb; pkind=kind; pside=side;
        } else {
          if(pz!=null){ // exiting S: the face, down to ground / drop / abutting surface
            let zBot = pkind==='abut' ? (o.abutZ[pside]??0) : (o.drop>0?-o.drop:0);
            if(zBot < pz-0.015){
              const syBot=HEAD+r-Math.round(zBot*ZS)-1;
              for(let fy=psy+1;fy<=syBot;fy++){
                const fr=pkerb?kerbRamp:(o.faceRamp||spec.ramp), fseat=pkerb?kerbSeat:spec.seat;
                let ft=faceTex(o,u,fy-psy,pkerb,kerbTimber);
                if(ft===null){ shade(x,fy,120); continue; }
                let fi=FACE_SHADE*GAIN+BIAS+fseat*0.4+ft;
                if(fy===syBot)fi-=1;
                put(x,fy,ft===-99?POLE:fr,Math.floor(fi)+((fi-Math.floor(fi))>BAYER[x&3][fy&3]?1:0));
              }
              const SH=[165,105,55];
              for(let k=0;k<3;k++)shade(x,syBot+1+k,SH[k]);
            } else if(pkind!=='abut' && pz>0.03){ const SH=[120,70];
              for(let k=0;k<2;k++)shade(x,psy+1+k,SH[k]); }
          }
          // off-surface: soil / shingle spill against free edges — never over a face already drawn
          if(r<TILE&&spill&&!rbuf[(HEAD+r)*TILE+x]){
            const q=o.off(lx,ly);
            if(q&&q.kind==='free'){ const g=-q.d;
              if(g>=0.03&&g<=0.20){ const t=1-(g-0.03)/0.17;
                if(clumpN(u,v,seed+96)<=0.30+(1-t)*0.62){
                  const pick=hash(o.gx*64+x,o.gy*64+r,seed+97);
                  put(x,HEAD+r,pick<0.22?spec.ramp:spill.ramp,pick<0.22?2:spill.i+(pick<0.6?0:1)); } } }
          }
          pz=null;
        }
      }
    }
    // east silhouette arris: left-lit family — the east edge of raised work darkens one step
    for(let r=0;r<TILE;r++)for(let x=0;x<TILE-1;x++){
      const y=HEAD+r, i=y*TILE+x;
      if(!rbuf[i])continue;
      const zi=zarr[r*TILE+x], zj=zarr[r*TILE+x+1];
      const j=y*TILE+x+1;
      if(zi>0.04&&(!rbuf[j]&&darr[r*TILE+x]<0.12)) ibuf[i]=Math.max(0,ibuf[i]-1);
      else if(rbuf[j]&&zi-zj>0.05) ibuf[j]=Math.max(0,ibuf[j]-1);
    }
    const data=new Uint8ClampedArray(N*4);
    for(let i=0;i<N;i++){
      if(rbuf[i]){ const [R,G,B]=hex2rgb(rbuf[i][clamp(ibuf[i],0,rbuf[i].length-1)]);
        data[i*4]=R;data[i*4+1]=G;data[i*4+2]=B;data[i*4+3]=255; }
      else if(sbuf[i]){ data[i*4]=10;data[i*4+1]=14;data[i*4+2]=16;data[i*4+3]=sbuf[i]; }
    }
    return { data, w:TILE, h:CELL_H };
  }
  function pkindFree(o){ return true; }
  // face texture: returns tint delta, -99 = pile column (POLE ramp), null = open under-deck air
  function faceTex(o,u,fy,kerbFace,timber){
    if(o.spec.field==='boardwalk'&&!kerbFace){
      if(fy<=2) return fract(u/1.6)<0.04?-1.5:(hash(Math.floor(u*S),fy,o.seed+83)<0.2?-1:0);   // rim joist
      if(fract(u/1.15)<0.14) return -99;                                                       // pile
      return null; }                                                                           // shaded air
    if(kerbFace||o.faceRamp===CONCRETE||o.spec.field==='apron'||o.spec.field==='concrete')
      return fract(u/(kerbFace?0.92:0.62))<0.05?-1.5:(hash(Math.floor(u*S),fy,o.seed+80)<0.22?-1:0);
    return hash(Math.floor(u*S),fy,o.seed+81)<0.25?-1:0;
  }

  // ---- distance to the blob polygon, with edge kinds -------------------------------------------
  function edgeSet(poly, abut){
    const segs=[];
    for(let k=0;k<poly.length;k++){
      const p=poly[k], q=poly[(k+1)%poly.length], side=segSide(p,q);
      if(side===null){ segs.push({p,q,kind:'free',side:'s'}); continue; }
      if(abut&&abut[side]) segs.push({p,q,kind:'abut',side});
      // connected border: no edge at all — the neighbour carries on
    }
    return segs;
  }
  function distKind(poly,segs,lx,ly){
    if(!segs.length) return { d: 1, kind:'none', side:'s' };
    let dmin=9, kind='free', side='s';
    for(const s of segs){
      const dx=s.q[0]-s.p[0], dy=s.q[1]-s.p[1], L2=dx*dx+dy*dy||1;
      let t=((lx-s.p[0])*dx+(ly-s.p[1])*dy)/L2; t=clamp(t,0,1);
      const ex=s.p[0]+dx*t-lx, ey=s.p[1]+dy*t-ly, dd=Math.hypot(ex,ey);
      if(dd<dmin){ dmin=dd; kind=s.kind; side=s.side; }
    }
    return { d: pointIn(poly,lx,ly)?dmin:-dmin, kind, side };
  }

  // =============================================================================================
  // RENDER — grid tile (blob-47 + abutment)
  // =============================================================================================
  const CACHE=new Map(), CACHE_MAX=2600;
  function render(surface, opts){
    opts=opts||{};
    const S0=SURF[surface]?surface:'asphalt', spec=SURF[S0];
    const con=Object.assign({n:false,e:false,s:false,w:false},opts.con||{});
    const diag=Object.assign({ne:false,nw:false,se:false,sw:false},opts.diag||{});
    const abut=Object.assign({n:false,e:false,s:false,w:false},opts.abut||{});
    const abutZ=Object.assign({n:0,e:0,s:0,w:0},opts.abutZ||{});
    const wear=opts.wear||'new', profile=opts.profile||'road2', piece=opts.piece||'road';
    const markings=opts.markings||[], ground=opts.ground||'grass';
    const gx=opts.gx|0, gy=opts.gy|0, seed=(opts.seed|0)||7;
    const drop=opts.drop!=null?opts.drop:(piece==='apron'?0.65:piece==='slipway'?0.75:0);
    const kerbOn=opts.kerb!=null?opts.kerb:(profile==='sidewalk'||piece==='apron'||spec.edge==='timber');
    const key=[S0,wear,profile,piece,markings.join(','),gx,gy,seed,drop,kerbOn?1:0,ground,
      (opts.cross&&opts.cross.l)|0,(opts.cross&&opts.cross.r)|0,
      con.n?1:0,con.e?1:0,con.s?1:0,con.w?1:0,diag.ne?1:0,diag.nw?1:0,diag.se?1:0,diag.sw?1:0,
      abut.n?1:0,abut.e?1:0,abut.s?1:0,abut.w?1:0,
      Math.round(abutZ.n*40),Math.round(abutZ.e*40),Math.round(abutZ.s*40),Math.round(abutZ.w*40),
      opts.axis||''].join('|');
    const hit=CACHE.get(key); if(hit)return hit;

    const axis=opts.axis||((con.n||con.s)?((con.e||con.w)?'x':'v'):'h');
    const wf=wearNum(wear);
    const ctx={axis,seed,wf,ruts:spec.ruts,markings,gx,gy,cross:Object.assign({l:0,r:0},opts.cross||{})};
    const zBase=(piece==='boardwalk'?0:PROFILE_Z[profile]||0)+spec.lift;
    const vg=piece==='shoulder'?0.175:0.135;
    const conEff={n:con.n||abut.n, e:con.e||abut.e, s:con.s||abut.s, w:con.w||abut.w};
    const poly=topPoly(conEff,diag,vg,gx,gy,seed);
    const segs=edgeSet(poly,abut);
    let fieldFn=wearWrap(FIELDS[spec.field],ctx);
    if(piece==='slipway'){ const base=fieldFn; fieldFn=(u,v)=>{ const r=base(u,v);
      const f=fract(v/0.30), rib=f<0.28?-1.1:f<0.36?0.9:0;
      const jig=(hash(Math.floor(u*S),Math.floor(v*S),seed+41)-0.5)*0.9;
      return [r[0], r[1]*0.7+1.5+rib, r[2]+(f<0.28?-1:0)+jig]; }; }
    const u0=gx+0.5, v0=gy+0.5;
    const wob=WOB[spec.edge]||0.05;
    const camber=(spec.marks&&(profile==='lane'||profile==='road2')&&piece==='road')?0.05:0;
    const KW=0.115, KUP=kerbOn?(spec.edge==='timber'?0.03:0.045):0;

    const at=(lx,ly,u,v)=>{
      let {d,kind,side}=distKind(poly,segs,lx,ly);
      if(kind==='free')d+=(clumpN(u*1.3,v*1.3,seed+95)-0.5)*wob;
      if(d<=0)return null;
      let z=zBase*ss(d,0,0.05), kerb=false;
      if(kerbOn&&d<KW&&kind!=='none'){ z=zBase+KUP; kerb=true; }
      if(camber)z+=camber*ss(d,0.12,1.1);
      if(piece==='slipway')z=zBase-drop*clamp(0.5-ly,0,1)+( Math.abs(lx)>0.40?0.10:0 );
      if(piece==='driveway')z=zBase*ss(d,0,0.05);
      return {z,d,kind,side,kerb:kerb&&piece!=='slipway'};
    };
    const off=(lx,ly)=>{ const q=distKind(poly,segs,lx,ly); return q; };
    const marks=(u,v,lx,ly,d)=>markAt(u,v,u-u0,v-v0,ctx);
    const out=march({ spec,fieldFn,seed,gx,gy,u0,v0,axis,wf,
      at,off,marks,drop:(piece==='apron'||piece==='slipway')?drop:0,
      abutZ, contS:con.s, openS:true, slope:piece==='slipway',
      faceRamp:(piece==='apron'||kerbOn&&spec.edge!=='timber')?CONCRETE:null,
      spill:(piece!=='slipway')?(piece==='shoulder'?{ramp:ROCK,i:2}:SPILL[ground]):null });
    if(CACHE.size>CACHE_MAX)CACHE.clear();
    CACHE.set(key,out);
    return out;
  }

  // =============================================================================================
  // RENDER — freeform coverage field ("doesn't have to be a perfect tile")
  // =============================================================================================
  function renderFree(surface, opts){
    opts=opts||{};
    const S0=SURF[surface]?surface:'dirt', spec=SURF[S0];
    const cov=opts.cov||(()=>0);
    const gx=opts.gx|0, gy=opts.gy|0, seed=(opts.seed|0)||7;
    const wear=opts.wear||'new', profile=opts.profile||'path';
    const wf=wearNum(wear), axis=opts.axis||'v';
    const ctx={axis,seed,wf,ruts:0,markings:[],gx,gy,cross:{l:0,r:0}};
    const fieldFn=wearWrap(FIELDS[spec.field],ctx);
    const zBase=(PROFILE_Z[profile]||0)+spec.lift;
    const kerbOn=opts.kerb!=null?opts.kerb:(profile==='sidewalk'||spec.edge==='timber');
    const KW=0.115, KUP=kerbOn?(spec.edge==='timber'?0.03:0.045):0;
    const u0=gx+0.5, v0=gy+0.5;
    const wob=(WOB[spec.edge]||0.05)*0.6;
    const dOf=(u,v)=>{ const c=cov(u,v);
      const g1=(cov(u+0.11,v)-c)/0.11, g2=(cov(u,v+0.11)-c)/0.11;
      const g=Math.max(Math.hypot(g1,g2),0.9);
      let d=(c-0.5)/g; return clamp(d,-0.8,0.9); };
    const at=(lx,ly,u,v)=>{
      let d=dOf(u,v)+(clumpN(u*1.6,v*1.6,seed+95)-0.5)*wob;
      if(d<=0)return null;
      let z=zBase*ss(d,0,0.05), kerb=false;
      if(kerbOn&&d<KW){ z=zBase+KUP; kerb=true; }
      return {z,d,kind:'free',side:'s',kerb};
    };
    const wantEdge=spec.marks&&(profile==='lane'||profile==='road2');
    const marks=(u,v,lx,ly,d)=>{ if(wantEdge&&Math.abs(d-0.18)<0.048)return WHITEP; return null; };
    return march({ spec,fieldFn,seed,gx,gy,u0,v0,axis,wf,
      at, off:(lx,ly)=>({d:dOf(u0+lx,v0+ly),kind:'free',side:'s'}), marks,
      drop:0, abutZ:{n:0,e:0,s:0,w:0}, contS:false, openS:true, slope:false,
      faceRamp:kerbOn&&spec.edge!=='timber'?CONCRETE:null,
      spill:SPILL[opts.ground||'grass'] });
  }

  // ---- preview ground (the game composites ShoreIso2 / the grass tileset) ----------------------
  const GROUNDS=['grass','dirt','sand','shingle'];
  const GROUND_MAT={grass:GRASS,dirt:DIRT,sand:SAND,shingle:ROCK};
  const GROUND_SEAT={grass:-2.20,dirt:-2.35,sand:-2.20,shingle:-2.15};
  function renderGround(mat,opts){
    opts=opts||{}; const m=GROUND_MAT[mat]?mat:'grass';
    const gx=opts.gx|0, gy=opts.gy|0, seed=(opts.seed|0)||7;
    const key='G|'+m+'|'+gx+'|'+gy+'|'+seed; const hit=CACHE.get(key); if(hit)return hit;
    const ctx={axis:'v',seed,wf:0,ruts:0,markings:[],gx,gy,cross:{l:0,r:0}};
    const f=FIELDS[m==='shingle'?'shingle':m], seat=GROUND_SEAT[m], ramp=GROUND_MAT[m];
    const data=new Uint8ClampedArray(TILE*CELL_H*4);
    for(let r=0;r<TILE;r++)for(let x=0;x<TILE;x++){
      const u=gx+(x+0.5)/TILE, v=gy+1-(r+0.5)/TILE;
      const rr=f(u,v,ctx);
      const fi=shadeSlope(rr[0],rr[1])*GAIN+BIAS+seat+rr[2];
      const base=Math.floor(fi), idx=clamp(base+((fi-base)>BAYER[x&3][r&3]?1:0),0,ramp.length-1);
      const [R,G,B]=hex2rgb(ramp[idx]); const i=((HEAD+r)*TILE+x)*4;
      data[i]=R;data[i+1]=G;data[i+2]=B;data[i+3]=255;
    }
    const out={data,w:TILE,h:CELL_H};
    if(CACHE.size>CACHE_MAX)CACHE.clear();
    CACHE.set(key,out); return out;
  }

  // ---- blob-47 (verbatim) -----------------------------------------------------------------------
  const BIT={n:1,e:2,s:4,w:8,ne:16,se:32,sw:64,nw:128};
  function canon(mask){ let m=mask;
    if(!(m&BIT.n&&m&BIT.e))m&=~BIT.ne; if(!(m&BIT.s&&m&BIT.e))m&=~BIT.se;
    if(!(m&BIT.s&&m&BIT.w))m&=~BIT.sw; if(!(m&BIT.n&&m&BIT.w))m&=~BIT.nw; return m; }
  function fromMask(mask){
    const con={n:!!(mask&BIT.n),e:!!(mask&BIT.e),s:!!(mask&BIT.s),w:!!(mask&BIT.w)};
    const diag={ne:!!(mask&BIT.ne),se:!!(mask&BIT.se),sw:!!(mask&BIT.sw),nw:!!(mask&BIT.nw)};
    const axis=(con.n||con.s)?((con.e||con.w)?'x':'v'):((con.e||con.w)?'h':'i');
    return {con,diag,axis}; }
  const BLOB47=(()=>{ const seen=new Set(),list=[];
    for(let m=0;m<256;m++){ const c=canon(m); if(seen.has(c))continue; seen.add(c);
      const f=fromMask(c);
      const n=(f.con.n?1:0)+(f.con.e?1:0)+(f.con.s?1:0)+(f.con.w?1:0);
      const label=n===0?'isolated':n===1?'cap':n===2?(((f.con.n&&f.con.s)||(f.con.e&&f.con.w))?'straight':'bend'):n===3?'tee':'cross';
      list.push({mask:c,con:f.con,diag:f.diag,axis:f.axis,label}); }
    return list.sort((a,b)=>a.mask-b.mask); })();

  // ---- gameplay sidecar (metres; v3 heights) ----------------------------------------------------
  function gameplay(surface,opts){
    opts=opts||{};
    const spec=SURF[surface]||SURF.asphalt;
    const con=Object.assign({n:false,e:false,s:false,w:false},opts.con||{});
    const profile=opts.profile||'road2', piece=opts.piece||'road';
    const wear=opts.wear||'new', wf=wearNum(wear);
    const zTop=(PROFILE_Z[profile]||0)+spec.lift;
    const poly=topPoly(con,Object.assign({},opts.diag||{}),piece==='shoulder'?0.115:0.085,opts.gx|0,opts.gy|0,(opts.seed|0)||7)
      .map(p=>[+p[0].toFixed(3),+p[1].toFixed(3)]);
    const axis=opts.axis||((con.n||con.s)?((con.e||con.w)?'x':'v'):'h');
    const drop=opts.drop!=null?opts.drop:(piece==='apron'?0.65:piece==='slipway'?0.75:0);
    const kerb=opts.kerb!=null?opts.kerb:(profile==='sidewalk'||piece==='apron');
    const blockers=[];
    if(kerb){ const kz=zTop+0.045,kw=0.115;
      if(!con.n)blockers.push({kind:'kerb',poly:[[-0.5,0.5-kw],[0.5,0.5-kw],[0.5,0.5],[-0.5,0.5]],z:[zTop,+kz.toFixed(3)]});
      if(!con.s)blockers.push({kind:'kerb',poly:[[-0.5,-0.5],[0.5,-0.5],[0.5,-0.5+kw],[-0.5,-0.5+kw]],z:[zTop,+kz.toFixed(3)]});
      if(!con.e)blockers.push({kind:'kerb',poly:[[0.5-kw,-0.5],[0.5,-0.5],[0.5,0.5],[0.5-kw,0.5]],z:[zTop,+kz.toFixed(3)]});
      if(!con.w)blockers.push({kind:'kerb',poly:[[-0.5,-0.5],[-0.5+kw,-0.5],[-0.5+kw,0.5],[-0.5,0.5]],z:[zTop,+kz.toFixed(3)]}); }
    const centre=axis==='v'?[[0,-0.5],[0,0.5]]:axis==='h'?[[-0.5,0],[0.5,0]]:[[0,-0.5],[0,0.5],[-0.5,0],[0.5,0]];
    return { surface,piece,profile,wear,axis,
      walk:{poly,z:+zTop.toFixed(3),surface:spec.foot,grip:+(spec.grip*(1-wf*0.18)).toFixed(3),
        slope:piece==='slipway'?+(drop/1).toFixed(3):0, wet:piece==='slipway'?'below mid tide':'rain only'},
      drive: spec.marks||piece!=='road'
        ? {centreline:centre,width:profile==='road2'?0.92:profile==='lane'?0.76:0.62,
           grip:+(spec.grip*(1-wf*0.22)).toFixed(3),speed:profile==='road2'?1:0.8}
        : {centreline:centre,width:0.58,grip:+(spec.grip*(1-wf*0.22)).toFixed(3),speed:0.6},
      blockers,
      drop:drop>0?{edge:'s',height:+drop.toFixed(2),fall:piece==='apron'?'deck':'water'}:null,
      foot:spec.foot };
  }

  root.RoadKit3 = {
    TILE, CELL_H, HEAD, SKIRT, S, ZS, LIGHT_ELEV,
    SURFACES, WEAR, PROFILES, PIECES, GROUNDS, SURF, PAL, PROFILE_Z,
    render, renderFree, renderGround, gameplay,
    BLOB47, canon, fromMask, topPoly,
    MATS: PAL,
    clearCache(){ CACHE.clear(); },
  };
})(typeof globalThis!=='undefined'?globalThis:window);
