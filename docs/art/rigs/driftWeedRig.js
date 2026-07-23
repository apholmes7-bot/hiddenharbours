/* Hidden Harbours — parametric DRIFT WEED family (surface-drift seaweed / flotsam decor).
   The fishing-kit pattern: parametric JS rig source → in-engine bake → ramp sets + gameplay
   sidecar (Art/Sprites/Shore/Drift/DriftWeed.json). NOT a turntable rig: flat water-surface
   clumps seen in the ¾ iso view (camera from the south, 40°) with NO heading — vertical
   foreshorten 0.72 baked into the shapes. 32 px = 1 m. No AA, binary alpha. Banded colour
   with ordered-dither band edges (no airbrush), hash mottle, 1 px soft keyline #1b2a22
   (the decor keyline — flowers/grass). Silhouette first: reads at gameplay zoom.

   SPECIES (North Atlantic, each a parametric GENERATOR, not fixed drawings):
     Bladderwrack — knobbly forking fronds with paired air bladders (the signature one)
     SugarKelp    — one long torn ribbon, puckered blade, dark stipe stub
     Eelgrass     — fine tuft of streaming blades
     TornMat      — mixed ragged raft: wrack bits + a kelp scrap + loose strands, holed

   PARAMS (uniform space, interpreted per species):
     sizeM 0.4–2 (clump span / kelp length) · fronds (blades / pieces / tail-strips)
     sprawl 0–1 (raggedness / wander) · bladders 0–1 (bladder / pucker / fleck density)
     seed — 3–4 discrete VARIANTS per species ship from fixed seeds; each variant is its
     own baked cell (NO mirroring assumptions).

   COLOUR VIA RAMPS ONLY — structure is seed-stable; the same build recolours per set:
     living   — per-species olive / brown-green / grass-green (guard-railed to the KTC
                master hexes: rockweed FLOAT ramp, DUNE #7f8a54, iris stem #3f7a52,
                ochre #7d6a3a/#968049 — nothing new invented, derivations noted inline)
     golden   — sun-golden kelp set; wet step = fleet gold #e0b13a verbatim
     bleached — one shared storm-bleached pale set; wet step = bone #e9e6df verbatim
   Every ramp carries a WET-SURFACE step (sky-glint specular on the upper-left rim).

   NO animation frames. Drift / bob / clumping / sway are runtime (shared wave field).

   ANCHORS AS DATA (per variant, computed from the built structure, shipped in the sidecar):
     buoy    — buoyancy centre (area centroid; where it sits on the water; the pivot)
     snags   — 2–3 outer-frond tips (catch on buoys / rocks / rope), ≥60° apart
     dragTail— the end that trails when drifting (kelp: the torn blade tip; else the
               longest frond — TornMat's is a _confirm ruling, see sidecar)

   Exposes globalThis.DriftWeed = { PPU, Q, KEYLINE, SPECIES, RAMPS,
     render(sp, opts, ramp) -> { w, h, rgba:Uint8ClampedArray, anchors, params } }
     opts: { variant: 0..n-1 } or a params object { seed, sizeM, fronds, sprawl, bladders }.
   Runs in the run_script sandbox (bake) and the browser (live preview). */
(function (root) {
  const PPU = 32, Q = 0.72, KEYLINE = '#1b2a22';

  const SPECIES = {
    Bladderwrack: { name:'Bladderwrack', latin:'Fucus vesiculosus', cell:{w:48,h:36},
      note:'Knobbly forking fronds, paired air bladders on the outer half',
      reads:{ fronds:'main fronds', bladders:'air-bladder density' },
      variants:[
        { seed:4101, sizeM:0.9,  fronds:6, sprawl:0.45, bladders:0.65 },
        { seed:4102, sizeM:1.25, fronds:8, sprawl:0.60, bladders:0.50 },
        { seed:4103, sizeM:0.6,  fronds:5, sprawl:0.35, bladders:0.85 },
        { seed:4104, sizeM:1.05, fronds:7, sprawl:0.80, bladders:0.60 } ] },
    SugarKelp: { name:'Sugar Kelp', latin:'Saccharina latissima', cell:{w:64,h:36},
      note:'One long torn ribbon — puckered blade, ruffled edge, dark stipe stub',
      reads:{ fronds:'torn tail strips', bladders:'blade pucker density' },
      variants:[
        { seed:4201, sizeM:1.6, fronds:2, sprawl:0.45, bladders:0.55 },
        { seed:4202, sizeM:2.0, fronds:3, sprawl:0.65, bladders:0.45 },
        { seed:4203, sizeM:1.1, fronds:1, sprawl:0.50, bladders:0.70 } ] },
    Eelgrass: { name:'Eelgrass', latin:'Zostera marina', cell:{w:32,h:24},
      note:'Fine tuft of thin blades, combed downstream by the drift',
      reads:{ fronds:'blades', bladders:'(none — ignored)' },
      variants:[
        { seed:4301, sizeM:0.7,  fronds:9,  sprawl:0.50, bladders:0 },
        { seed:4302, sizeM:0.9,  fronds:12, sprawl:0.70, bladders:0 },
        { seed:4303, sizeM:0.45, fronds:6,  sprawl:0.40, bladders:0 },
        { seed:4304, sizeM:0.8,  fronds:10, sprawl:0.90, bladders:0 } ] },
    TornMat: { name:'Torn Mat', latin:'mixed wrack raft', cell:{w:64,h:48},
      note:'Ragged mixed raft — wrack bits, a kelp scrap, loose strands, holed through',
      reads:{ fronds:'loose pieces', bladders:'bladder flecks' },
      variants:[
        { seed:4401, sizeM:1.4, fronds:5, sprawl:0.60, bladders:0.55 },
        { seed:4402, sizeM:2.0, fronds:7, sprawl:0.75, bladders:0.45 },
        { seed:4403, sizeM:1.15,fronds:4, sprawl:0.50, bladders:0.65 } ] },
  };

  // ramp = { out, dp, sh, mid, hi, wet, ac, acHi } — wet is the wet-surface glint step.
  // Guard-rail provenance in _prov; verbatim owner hexes marked (v).
  const LIVING = {
    Bladderwrack: { out:KEYLINE, dp:'#242f1c', sh:'#2e3b22', mid:'#3b4b2b', hi:'#586b38', wet:'#8b9b7b', ac:'#7d6a3a', acHi:'#968049',
      _prov:'dp..hi + ac/acHi verbatim seaweedRig FLOAT ramp; wet = mix(hi, sky #c9d6cc, .45)' },
    SugarKelp:    { out:KEYLINE, dp:'#33281a', sh:'#4a3a1f', mid:'#614e28', hi:'#7f6a35', wet:'#b39a63', ac:'#3a2e15', acHi:'#4a3a1f',
      _prov:'wet = flowers SEED #b39a63 (v); band steps darkened from ochre #7d6a3a line' },
    Eelgrass:     { out:KEYLINE, dp:'#1d3527', sh:'#2b4f38', mid:'#3f7a52', hi:'#5f9a68', wet:'#8fb391', ac:'#24402c', acHi:'#2b4f38',
      _prov:'mid = blue-flag stem #3f7a52 (v); steps light()/dark() of it' },
    TornMat:      { out:KEYLINE, dp:'#262c18', sh:'#333a1f', mid:'#454a28', hi:'#5f6533', wet:'#7f8a54', ac:'#7d6a3a', acHi:'#968049',
      _prov:'wet = DUNE #7f8a54 (v); ac/acHi verbatim seaweedRig ochre' },
  };
  const GOLDEN =  { out:KEYLINE, dp:'#4a3517', sh:'#63481e', mid:'#7f5f26', hi:'#a37f30', wet:'#e0b13a', ac:'#33250f', acHi:'#4a3517',
      _prov:'wet = fleet gold cove #e0b13a (v); band steps darkened from it' };
  const BLEACHED ={ out:KEYLINE, dp:'#5a5c4a', sh:'#74765f', mid:'#8f9077', hi:'#aeab90', wet:'#e9e6df', ac:'#9a8a68', acHi:'#b39a63',
      _prov:'wet = bone/white #e9e6df (v); acHi = flowers SEED #b39a63 (v)' };
  const RAMPS = {};
  for (const k in SPECIES) RAMPS[k] = { living: LIVING[k], golden: GOLDEN, bleached: BLEACHED };

  // ---- rng / hash / buffer --------------------------------------------------
  function mulberry(a){ return function(){ a|=0;a=a+0x6D2B79F5|0;let t=Math.imul(a^a>>>15,1|a);t=t+Math.imul(t^t>>>7,61|t)^t;return((t^t>>>14)>>>0)/4294967296; }; }
  function hash2(x,y,s){ let h=(x*374761393 + y*668265263 + s*2246822519)|0; h=Math.imul(h^(h>>>13),1274126177); return ((h^(h>>>16))>>>0)/4294967296; }
  const B4=[[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]];

  function Buf(w,h){ this.w=w; this.h=h; this.m=new Int8Array(w*h); }        // 0 empty, 1 weed, 2 accent
  Buf.prototype.i=function(x,y){ return y*this.w+x; };
  Buf.prototype.in=function(x,y){ return x>=0&&x<this.w&&y>=0&&y<this.h; };
  Buf.prototype.put=function(x,y,m){ x=Math.round(x); y=Math.round(y); if(this.in(x,y)) this.m[this.i(x,y)]=m; };
  function ell(b,cx,cy,rx,ry,m){ for(let y=Math.floor(cy-ry);y<=Math.ceil(cy+ry);y++)for(let x=Math.floor(cx-rx);x<=Math.ceil(cx+rx);x++){ const dx=(x-cx)/(rx+.001),dy=(y-cy)/(ry+.001); if(dx*dx+dy*dy<=1) b.put(x,y,m); } }
  function taper(b,x0,y0,x1,y1,r0,r1,m){ const dx=x1-x0,dy=y1-y0,L2=dx*dx+dy*dy||1; const mnx=Math.floor(Math.min(x0,x1)-Math.max(r0,r1)),mxx=Math.ceil(Math.max(x0,x1)+Math.max(r0,r1)),mny=Math.floor(Math.min(y0,y1)-Math.max(r0,r1)),mxy=Math.ceil(Math.max(y0,y1)+Math.max(r0,r1)); for(let y=mny;y<=mxy;y++)for(let x=mnx;x<=mxx;x++){ let t=((x-x0)*dx+(y-y0)*dy)/L2; t=Math.max(0,Math.min(1,t)); const px=x0+dx*t,py=y0+dy*t; if(Math.hypot(x-px,y-py)<=r0+(r1-r0)*t) b.put(x,y,m); } }
  function arc1(b,x0,y0,x1,y1,bow,m,thick){ // quadratic 1px-ish curve; bow = perp offset of control
    const mx=(x0+x1)/2, my=(y0+y1)/2, dx=x1-x0, dy=y1-y0, L=Math.hypot(dx,dy)||1;
    const px=-dy/L, py=dx/L, cx2=mx+px*bow, cy2=my+py*bow, N=Math.ceil(L*1.6);
    for(let i=0;i<=N;i++){ const t=i/N, u=1-t; const x=u*u*x0+2*u*t*cx2+t*t*x1, y=u*u*y0+2*u*t*cy2+t*t*y1;
      b.put(x,y,m); if(thick&&t<0.3) b.put(x,y+1,m); } }

  // ---- species generators (fill b, return tips [{x,y,tag}]) ---------------
  function genBladderwrack(b,p,rng){
    const cx=b.w/2, cy=b.h/2, R=Math.max(6, p.sizeM*PPU*0.4), tips=[];   // frond reach ~1.25R => span ~ sizeM
    ell(b,cx,cy,R*0.26,R*0.26*Q,1);
    const paths=[];
    function branch(x,y,a,len,w0,depth){
      const segs=4, pts=[];
      for(let s=0;s<segs;s++){ const sl=len/segs, na=a+(rng()-0.5)*(0.5+p.sprawl*0.9);
        const nx=x+Math.cos(na)*sl, ny=y+Math.sin(na)*sl*Q;
        taper(b,x,y,nx,ny, w0*(1-s/segs*0.4), w0*(1-(s+1)/segs*0.4), 1);
        pts.push([nx,ny]); x=nx; y=ny; a=na;
        if(depth<2 && s===Math.floor(segs/2)-1 && rng()<0.75)
          branch(x,y, a+(rng()<0.5?1:-1)*(0.45+rng()*0.5), len*0.52, w0*0.7, depth+1);
      }
      paths.push(pts); tips.push({x:Math.round(x),y:Math.round(y),tag:'frond'});
    }
    const n=Math.max(3,Math.round(p.fronds));
    for(let i=0;i<n;i++){ const a=(i/n)*Math.PI*2 + rng()*0.9;
      branch(cx+Math.cos(a)*R*0.2, cy+Math.sin(a)*R*0.2*Q, a, R*(0.62+rng()*0.42), 1.7, 0); }
    for(const pts of paths) for(let k=1;k<pts.length;k++)      // paired bladders, outer half
      if(k>=pts.length/2 && rng()<p.bladders){ const [x,y]=pts[k];
        ell(b,x-1,y,1.3,1.1*Q+0.4,2); if(rng()<0.6) ell(b,x+1.6,y+0.5,1.2,1.0*Q+0.4,2); }
    return tips;
  }

  function genSugarKelp(b,p,rng){
    const L=Math.round(Math.min(b.w-5, Math.max(12,p.sizeM*PPU*0.92))), cx=b.w/2, cy=b.h/2, x0=cx-L/2, tips=[];
    const ph=rng()*6.28, ph2=rng()*6.28, ph3=rng()*6.28, ph4=rng()*6.28, wScale=0.72+0.28*Math.min(2,p.sizeM)/2;
    const cLine=[], hwT=[], hwB=[];
    for(let x=0;x<=L;x++){ const t=x/L;
      cLine[x]=cy+(Math.sin(t*3.1+ph)*2.6+Math.sin(t*7.3+ph2)*1.1)*Q*(0.5+p.sprawl);
      const base=(0.8+4.8*Math.pow(Math.sin(Math.min(1,t*1.15)*Math.PI),0.65))*wScale;
      let wt=base*(1+0.15*Math.sin(t*L*1.05+ph3)), wb=base*(1+0.15*Math.sin(t*L*0.9+ph4)); // ruffled edges, asymmetric
      if(t>0.72 && rng()<p.sprawl*0.5){ const k=0.35+rng()*0.4; wt*=k; wb*=k; }           // torn bites near the tail
      if(t<0.08){ wt=Math.min(wt,1.2); wb=Math.min(wb,1.2); }                              // stipe stub
      hwT[x]=wt; hwB[x]=wb;
    }
    for(let x=0;x<=L;x++){ const t=x/L, c=cLine[x], hw=(hwT[x]+hwB[x])/2;
      const strips=t>0.78?Math.max(1,Math.round(p.fronds)):1;
      for(let y=Math.floor(c-hwT[x]);y<=Math.ceil(c+hwB[x]);y++){
        if(strips>1){ const g=((y-c+hw)/(2*hw))*strips; if(Math.abs(g-Math.round(g))<0.15) continue; } // tail split
        b.put(x0+x,y, t<0.08?2:1);
      }
    }
    if(p.sprawl>0.35){ const nh=1+(rng()<0.5?1:0);                        // punched holes
      for(let i=0;i<nh;i++){ const hx=x0+L*(0.3+rng()*0.4); ell(b,hx,cLine[Math.round(hx-x0)],1.4,1.1,0); } }
    const nd=Math.round(L*0.5*p.bladders);                                 // bullate puckers
    for(let i=0;i<nd;i++){ const x=Math.round(L*(0.15+rng()*0.75)), y=Math.round(cLine[x]+(rng()-0.5)*(hwT[x]+hwB[x])*0.55);
      if(b.in(x0+x,y)&&b.m[b.i(x0+x,y)]===1) b.put(x0+x,y,2); }
    let lo={y:1e9},hiP={y:-1e9};                                           // ruffle extremes = snag lobes
    for(let x=Math.round(L*0.25);x<L*0.85;x++){ const a={x:Math.round(x0+x),y:Math.round(cLine[x]-hwT[x])}, z={x:Math.round(x0+x),y:Math.round(cLine[x]+hwB[x])};
      if(a.y<lo.y)lo=a; if(z.y>hiP.y)hiP=z; }
    tips.push({x:Math.round(x0),y:Math.round(cLine[0]),tag:'stipe'});
    tips.push({x:Math.round(x0+L),y:Math.round(cLine[L]),tag:'tail'});
    tips.push({x:lo.x,y:lo.y,tag:'lobe'}); tips.push({x:hiP.x,y:hiP.y,tag:'lobe'});
    return tips;
  }

  function genEelgrass(b,p,rng){
    const cx=b.w/2, cy=b.h/2, R=Math.max(6,p.sizeM*PPU*0.5), tips=[];
    const bx=cx-R*0.5, by=cy+1, stream=-0.12+(rng()-0.5)*0.2;
    ell(b,bx,by,1.4,1.1*Q+0.4,2);
    const n=Math.max(4,Math.round(p.fronds));
    for(let i=0;i<n;i++){ const a=stream+(i/(n-1)-0.5)*(1.1+1.0*p.sprawl)+(rng()-0.5)*0.25;
      const len=R*(0.85+rng()*0.6), tx=bx+Math.cos(a)*len, ty=by+Math.sin(a)*len*Q;
      arc1(b,bx,by,tx,ty,(rng()-0.5)*len*0.6,1,false);
      if(i%2===0) tips.push({x:Math.round(tx),y:Math.round(ty),tag:'blade'});
    }
    tips.push({x:Math.round(bx-2),y:Math.round(by),tag:'root'});
    return tips;
  }

  function genTornMat(b,p,rng){
    const cx=b.w/2, cy=b.h/2, R=Math.max(8,p.sizeM*PPU*0.31), tips=[];   // strand reach ~1.55R => span ~ sizeM
    const nb=2+(p.sizeM>1.5?1:0);
    for(let i=0;i<nb;i++){ const ox=cx+(rng()-0.5)*R*0.9, oy=cy+(rng()-0.5)*R*0.7*Q, r=R*(0.3+rng()*0.22);
      ell(b,ox,oy,r,r*Q,1);
      for(let f=0;f<4;f++){ const a=rng()*6.28, tx=ox+Math.cos(a)*r*1.9, ty=oy+Math.sin(a)*r*1.9*Q;
        taper(b,ox,oy,tx,ty,1.6,0.4,1); if(rng()<0.5) tips.push({x:Math.round(tx),y:Math.round(ty),tag:'frond'}); } }
    { const a=rng()*6.28, sl=R*1.1, sx=cx-Math.cos(a)*sl/2, sy=cy-Math.sin(a)*sl/2*Q;   // kelp scrap
      const ex=cx+Math.cos(a)*sl/2, ey=cy+Math.sin(a)*sl/2*Q;
      taper(b,sx,sy,ex,ey,3.2,1.4,1); tips.push({x:Math.round(ex),y:Math.round(ey),tag:'scrap'}); }
    const ns=Math.max(2,Math.round(p.fronds*0.6));                          // loose strands over the edge
    for(let i=0;i<ns;i++){ const a=rng()*6.28, sx=cx+Math.cos(a)*R*0.5, sy=cy+Math.sin(a)*R*0.5*Q;
      const rr=R*(1.15+rng()*0.4), tx=cx+Math.cos(a)*rr, ty=cy+Math.sin(a)*rr*Q;
      arc1(b,sx,sy,tx,ty,(rng()-0.5)*10,1,false); if(rng()<0.7) tips.push({x:Math.round(tx),y:Math.round(ty),tag:'strand'}); }
    for(let i=0;i<2+Math.round(p.sprawl*2);i++) ell(b,cx+(rng()-0.5)*R*1.1, cy+(rng()-0.5)*R*0.8*Q, 1.2+rng()*1.3, (1+rng())*Q, 0); // holes
    let put=0,tries=0; const target=Math.round(6*p.bladders*(R/16));
    while(put<target&&tries++<200){ const x=Math.floor(cx+(rng()-0.5)*R*1.6), y=Math.floor(cy+(rng()-0.5)*R*1.2*Q);
      if(b.in(x,y)&&b.m[b.i(x,y)]===1){ b.put(x,y,2); put++; } }
    return tips;
  }

  const GEN = { Bladderwrack:genBladderwrack, SugarKelp:genSugarKelp, Eelgrass:genEelgrass, TornMat:genTornMat };

  // ---- light bands + dithered edges + wet glint + keyline -------------------
  function shade(b, seed){
    const {w,h,m}=b, band=new Int8Array(w*h).fill(-1), outl=new Uint8Array(w*h);
    const filled=(x,y)=>b.in(x,y)&&m[b.i(x,y)]>0;
    for(let y=0;y<h;y++)for(let x=0;x<w;x++){ const i=b.i(x,y); if(!m[i])continue;
      const ul=!filled(x-1,y)||!filled(x,y-1)||!filled(x-1,y-1);
      const dr=!filled(x+1,y)||!filled(x,y+1)||!filled(x+1,y+1);
      let v=0.52+(ul?0.30:0)-(dr?0.30:0)+(hash2(x,y,seed)-0.5)*0.22+((B4[y%4][x%4]+0.5)/16-0.5)*0.20;
      let bd=Math.max(0,Math.min(3,Math.floor(v*4)));
      if(bd===3&&ul&&hash2(x*3+1,y*7+2,seed)<0.55) bd=4;                    // wet-surface glint step
      band[i]=bd;
    }
    for(let y=0;y<h;y++)for(let x=0;x<w;x++){ const i=b.i(x,y); if(m[i])continue; // soft keyline, 8-conn
      for(const[dx,dy]of[[1,0],[-1,0],[0,1],[0,-1],[1,1],[-1,-1],[1,-1],[-1,1]])
        if(filled(x+dx,y+dy)){ outl[i]=1; break; } }
    return {band,outl};
  }

  function h2r(hx){ return [parseInt(hx.slice(1,3),16),parseInt(hx.slice(3,5),16),parseInt(hx.slice(5,7),16)]; }
  function toRGBA(b,band,outl,ramp,seed){
    const {w,h,m}=b, out=new Uint8ClampedArray(w*h*4), steps=[ramp.dp,ramp.sh,ramp.mid,ramp.hi,ramp.wet];
    for(let i=0;i<w*h;i++){ let hx=null;
      if(outl[i]) hx=ramp.out;
      else if(m[i]===2) hx=(band[i]>=3||hash2(i%w,(i/w)|0,seed+9)<0.3)?ramp.acHi:ramp.ac;
      else if(m[i]) hx=steps[band[i]];
      if(!hx){ out[i*4+3]=0; continue; }
      const c=h2r(hx); out[i*4]=c[0]; out[i*4+1]=c[1]; out[i*4+2]=c[2]; out[i*4+3]=255; }
    return out;
  }

  // ---- anchors ---------------------------------------------------------------
  function anchorsFor(b,tips,sp){
    const {w,h,m}=b; let sx=0,sy=0,n=0;
    for(let y=0;y<h;y++)for(let x=0;x<w;x++) if(m[b.i(x,y)]){ sx+=x; sy+=y; n++; }
    const bx=Math.round(sx/Math.max(1,n)), by=Math.round(sy/Math.max(1,n));
    const dist=t=>Math.hypot(t.x-bx,(t.y-by)/Q), angD=(a,c)=>{ let d=Math.abs(a-c)%(Math.PI*2); return Math.min(d,Math.PI*2-d); };
    const uniq=[]; for(const t of tips) if(!uniq.some(u=>Math.hypot(u.x-t.x,u.y-t.y)<3)) uniq.push(t);
    uniq.sort((a,c)=>dist(c)-dist(a));
    let tail = sp==='SugarKelp' ? (uniq.find(t=>t.tag==='tail')||uniq[0]) : uniq[0];
    const rest=uniq.filter(t=>t!==tail), snags=[];
    for(const t of rest){ const a=Math.atan2((t.y-by)/Q,t.x-bx);
      if(snags.every(s=>angD(a,Math.atan2((s.y-by)/Q,s.x-bx))>1.05) && dist(t)>3){ snags.push(t); if(snags.length===3) break; } }
    if(snags.length<2) for(const t of rest){ if(snags.indexOf(t)<0&&dist(t)>3){ snags.push(t); if(snags.length===2) break; } }
    const cl=(t)=>({x:Math.max(0,Math.min(w-1,t.x)), y:Math.max(0,Math.min(h-1,t.y))});
    return { buoy:{x:bx,y:by}, snags:snags.map(cl), dragTail:cl(tail) };
  }

  // ---- public ----------------------------------------------------------------
  function render(sp, opts, rampKey){
    const S=SPECIES[sp]||SPECIES.Bladderwrack, key=sp in SPECIES?sp:'Bladderwrack';
    let p; if(opts&&typeof opts.variant==='number') p=S.variants[Math.max(0,Math.min(S.variants.length-1,opts.variant))];
    else p=Object.assign({},S.variants[0],opts||{});
    p=Object.assign({},p,{sizeM:Math.max(0.4,Math.min(2,p.sizeM))});
    const b=new Buf(S.cell.w,S.cell.h), rng=mulberry(p.seed|0);
    const tips=GEN[key](b,p,rng);
    const {band,outl}=shade(b,p.seed|0);
    const ramp=(RAMPS[key]&&RAMPS[key][rampKey])||RAMPS[key].living;
    const rgba=toRGBA(b,band,outl,ramp,p.seed|0);
    return { w:S.cell.w, h:S.cell.h, rgba, anchors:anchorsFor(b,tips,key), params:p };
  }

  root.DriftWeed = { PPU, Q, KEYLINE, SPECIES, RAMPS, render };
})(typeof globalThis!=='undefined'?globalThis:window);
