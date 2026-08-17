/* Hidden Harbours — ISO SOLID KIT: the shared projector/rasteriser behind the deck-gear
   rigs (traps, buoys, trays, bait bins). Same fixed 3/4 turntable as the fleet and the
   insulated tote — 45deg steps CW, elev 40deg, 32 px = 1 m, dither via the fleet BAYER,
   depth-edge darkening, no AA. Constants (GAIN/BIAS/EDGE/LN) are copied verbatim from
   fishToteRig.js so a trap and a tote standing side by side shade identically.

   ADR 0031: art authored from 2026-08 bakes RINGLESS. `keyline:true` is kept as a live
   A/B only — KEYLINE_DEFAULT is false and every rig on this kit ships gated off.

   Faces are { v:[[x,y,z]...], mat, b, db, tag }:
     b   ramp-index bias (fake AO / material lift)
     db  depth bias (pull a face forward so it wins its own z-fight)
     tag arbitrary label; paint({emit}) can colour a SUBSET while every face still
         writes depth — that is how a trap's near mesh redraws OVER its own catch.

   Exposes globalThis.IsoSolid. */
(function (root) {
  const S = 32, DEG = Math.PI/180, DEFAULT_ELEV = 40;
  const GAIN = 3.0, BIAS = 2.7, EDGE = 0.12;
  const LN = (()=>{ const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));
  const KEYLINE_DEFAULT = false;
  const hex2rgb=(h)=>[parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)];
  const ID=(p)=>p;

  // ---- transforms ----------------------------------------------------------
  const rotZ=(a)=>{ const c=Math.cos(a), s=Math.sin(a);
    return (p)=>[p[0]*c-p[1]*s, p[0]*s+p[1]*c, p[2]]; };
  const rotY=(a)=>{ const c=Math.cos(a), s=Math.sin(a);
    return (p)=>[p[0]*c+p[2]*s, p[1], -p[0]*s+p[2]*c]; };
  const move=(dx,dy,dz)=>(p)=>[p[0]+dx,p[1]+dy,p[2]+dz];
  const chain=(...fns)=>(p)=>fns.reduce((q,f)=>f(q), p);

  // ---- solids --------------------------------------------------------------
  // centre c, half-extents h. Faces ordered top, bottom, +y, -y, +x, -x.
  function box(c,h,mat,b,db,xf,tag){
    xf=xf||ID;
    const P=(sx,sy,sz)=>xf([c[0]+sx*h[0], c[1]+sy*h[1], c[2]+sz*h[2]]);
    const f=(v)=>({v,mat,b:b||0,db:db||0,tag:tag||null});
    return [
      f([P(-1,-1,1),P(1,-1,1),P(1,1,1),P(-1,1,1)]),
      f([P(-1,1,-1),P(1,1,-1),P(1,-1,-1),P(-1,-1,-1)]),
      f([P(-1,1,1),P(1,1,1),P(1,1,-1),P(-1,1,-1)]),
      f([P(1,-1,1),P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1)]),
      f([P(1,1,1),P(1,-1,1),P(1,-1,-1),P(1,1,-1)]),
      f([P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1)]),
    ];
  }
  // a square-section bar from p0 to p1 (laths, wire runs, rails). r = half-thickness.
  function bar(p0,p1,r,mat,b,db,tag){
    const d=[p1[0]-p0[0],p1[1]-p0[1],p1[2]-p0[2]];
    const L=Math.hypot(d[0],d[1],d[2])||1;
    const u=d.map(v=>v/L);
    // any perpendicular basis
    let a=Math.abs(u[2])<0.9?[0,0,1]:[1,0,0];
    let n1=[u[1]*a[2]-u[2]*a[1], u[2]*a[0]-u[0]*a[2], u[0]*a[1]-u[1]*a[0]];
    const m1=Math.hypot(...n1)||1; n1=n1.map(v=>v/m1);
    const n2=[u[1]*n1[2]-u[2]*n1[1], u[2]*n1[0]-u[0]*n1[2], u[0]*n1[1]-u[1]*n1[0]];
    const rr=Array.isArray(r)?r:[r,r];
    const c=[(p0[0]+p1[0])/2,(p0[1]+p1[1])/2,(p0[2]+p1[2])/2];
    const P=(su,s1,s2)=>[
      c[0]+u[0]*su*L/2 + n1[0]*s1*rr[0] + n2[0]*s2*rr[1],
      c[1]+u[1]*su*L/2 + n1[1]*s1*rr[0] + n2[1]*s2*rr[1],
      c[2]+u[2]*su*L/2 + n1[2]*s1*rr[0] + n2[2]*s2*rr[1]];
    const f=(v)=>({v,mat,b:b||0,db:db||0,tag:tag||null});
    return [
      f([P(-1,-1,1),P(1,-1,1),P(1,1,1),P(-1,1,1)]),
      f([P(-1,1,-1),P(1,1,-1),P(1,-1,-1),P(-1,-1,-1)]),
      f([P(-1,1,1),P(1,1,1),P(1,1,-1),P(-1,1,-1)]),
      f([P(1,-1,1),P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1)]),
      f([P(1,1,1),P(1,-1,1),P(1,-1,-1),P(1,1,-1)]),
      f([P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1)]),
    ];
  }
  // tapered tube band: r0 at z0 -> r1 at z1. caps: 'none'|'top'|'bottom'|'both'.
  // biasOf(i) lets a caller alternate ramp bias per segment (rope lay, mesh weave).
  function tube(cx,cy,z0,z1,r0,r1,seg,mat,opts){
    opts=opts||{}; const xf=opts.xf||ID, caps=opts.caps||'none';
    const biasOf=opts.biasOf||(()=>opts.b||0);
    const sq=opts.sq||1;                       // y-squash for oval pots
    const F=[], A=(i)=>((i/seg)*Math.PI*2)+(opts.phase||0);
    const pt=(i,r,z)=>xf([cx+Math.cos(A(i))*r, cy+Math.sin(A(i))*r*sq, z]);
    for (let i=0;i<seg;i++){
      const j=(i+1)%seg;
      // wound so the side normal points OUTWARD (i→j is CCW about +z; bottom ring first)
      F.push({ v:[pt(i,r0,z0),pt(j,r0,z0),pt(j,r1,z1),pt(i,r1,z1)],
               mat, b:biasOf(i), db:opts.db||0, tag:opts.tag||null });
    }
    const cap=(z,r,rev)=>{ const v=[]; for(let i=0;i<seg;i++) v.push(pt(rev?seg-i-1:i,r,z));
      F.push({ v, mat, b:opts.capB!=null?opts.capB:(opts.b||0), db:opts.db||0, tag:opts.tag||null }); };
    if (caps==='top'||caps==='both') cap(z1,r1,false);
    if (caps==='bottom'||caps==='both') cap(z0,r0,true);
    return F;
  }
  // extrude a 2D polygon between z0 and z1 (arched trap tops, tray tapers)
  function prism(poly,z0,z1,mat,opts){
    opts=opts||{}; const xf=opts.xf||ID, F=[], n=poly.length;
    const p=(i,z)=>xf([poly[i][0],poly[i][1],z]);
    for (let i=0;i<n;i++){ const j=(i+1)%n;
      if (opts.open && j===0) continue;
      F.push({ v:[p(i,z1),p(j,z1),p(j,z0),p(i,z0)], mat, b:opts.b||0, db:opts.db||0, tag:opts.tag||null });
    }
    if (opts.caps){
      const top=[],bot=[]; for(let i=0;i<n;i++){ top.push(p(i,z1)); bot.push(p(n-i-1,z0)); }
      F.push({ v:top, mat, b:opts.capB!=null?opts.capB:(opts.b||0), db:opts.db||0, tag:opts.tag||null });
      F.push({ v:bot, mat, b:opts.capB!=null?opts.capB:(opts.b||0), db:opts.db||0, tag:opts.tag||null });
    }
    return F;
  }

  // ---- camera --------------------------------------------------------------
  function camBasis(opts){
    opts=opts||{};
    const th=-(opts.dir||0)*Math.PI/4;
    const e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    return { ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e) };
  }
  function proj(x,y,z,B,cx,cy){
    const xr=x*B.ct - y*B.stt, yr=x*B.stt + y*B.ct, zr=z;
    return { xr,yr,zr, sx:cx+xr*S, sy:cy-(yr*B.se+zr*B.ce)*S, d:(yr*B.ce-zr*B.se) };
  }
  function normal(a,b,c){
    const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
    const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m];
  }
  const shadeOf=(n,se,ce)=>n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2];

  // ---- raster --------------------------------------------------------------
  /* paint(faces, { W,H,cx,cy, MATS, dir, elev, keyline, key, emit, edge })
     MATS: { name: { ramp:[dark..light], off:0 } }
     emit(face, rotatedNormal, basis) -> bool. A face that fails `emit` still writes
     depth but no colour, so a near-panel overlay leaves its own holes transparent.
     Camera-facing test: n[1]*B.ce - n[2]*B.se < 0.        -> Uint8ClampedArray(W*H*4) */
  function paint(faces, o){
    const W=o.W, H=o.H, cx=o.cx, cy=o.cy, MATS=o.MATS;
    const B=camBasis(o), emit=o.emit||null, EDG=o.edge!=null?o.edge:EDGE;
    const RINDEX={}; const seen=new Set();
    for (const k in MATS){ const r=MATS[k]&&MATS[k].ramp; if(!r||seen.has(r))continue; seen.add(r);
      r.forEach((c,i)=>{ RINDEX[c]={r,i}; }); }
    const zbuf=new Float32Array(W*H).fill(Infinity);
    const col=new Array(W*H).fill(null);
    const dep=new Float32Array(W*H);
    for (const f of faces){
      const rv=f.v.map(([x,y,z])=>proj(x,y,z,B,cx,cy));
      const n=normal(rv[0],rv[1],rv[2]);
      const fidx=shadeOf(n,B.se,B.ce)*GAIN+BIAS+(f.b||0);
      const M=MATS[f.mat]||MATS[Object.keys(MATS)[0]];
      const show=emit?!!emit(f,n,B):true;
      for (let t=1;t+1<rv.length;t++) tri(rv[0],rv[t],rv[t+1]);
      function tri(a,b,c){
        const minX=Math.max(0,Math.floor(Math.min(a.sx,b.sx,c.sx)));
        const maxX=Math.min(W-1,Math.ceil(Math.max(a.sx,b.sx,c.sx)));
        const minY=Math.max(0,Math.floor(Math.min(a.sy,b.sy,c.sy)));
        const maxY=Math.min(H-1,Math.ceil(Math.max(a.sy,b.sy,c.sy)));
        const area=(b.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b.sy-a.sy);
        if (Math.abs(area)<1e-6) return;
        for (let y=minY;y<=maxY;y++) for (let x=minX;x<=maxX;x++){
          const px=x+0.5, py=y+0.5;
          const w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area;
          const w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area;
          const w2=1-w0-w1;
          if (w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const d=w0*a.d+w1*b.d+w2*c.d, deff=d-(f.db||0);
          const i=y*W+x;
          if (deff<zbuf[i]){
            zbuf[i]=deff; dep[i]=d;
            if (!show){ col[i]=null; continue; }
            // ramp position is normalised to the canonical 5-step scale, so a longer
            // ramp gives FINER steps at the same brightness (less checkerboard on big flats)
            const n=M.ramp.length;
            const pos = n===5 ? fidx : fidx*(n-1)/4;
            const base=Math.floor(pos);
            const k=base+((pos-base)>BAYER[x&3][y&3]?1:0)+(M.off||0);
            col[i]=M.ramp[Math.max(0,Math.min(n-1,k))];
          }
        }
      }
    }
    const out=col.slice();
    for (let y=0;y<H;y++) for (let x=0;x<W;x++){
      const i=y*W+x; if (!col[i]) continue;
      for (const [dx,dy] of [[1,0],[0,1]]){
        const nx=x+dx, ny=y+dy; if (nx>=W||ny>=H) continue;
        const j=ny*W+nx; if (!col[j]) continue;
        if (Math.abs(dep[i]-dep[j])>EDG){
          const far=dep[i]>dep[j]?i:j, e=RINDEX[col[far]];
          if (e && e.i>0) out[far]=e.r[Math.max(0,e.i-2)];
        }
      }
    }
    if (o.keyline!=null ? o.keyline : KEYLINE_DEFAULT){
      const KEY=o.key||'#101a19';
      for (let y=0;y<H;y++) for (let x=0;x<W;x++){
        const i=y*W+x; if (out[i]) continue;
        for (const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){
          const nx=x+dx, ny=y+dy;
          if (nx>=0&&nx<W&&ny>=0&&ny<H&&col[ny*W+nx]){ out[i]=KEY; break; }
        }
      }
    }
    const rgba=new Uint8ClampedArray(W*H*4);
    for (let i=0;i<W*H;i++){
      const c=out[i]; if (!c){ rgba[i*4+3]=0; continue; }
      const [r,g,b]=hex2rgb(c);
      rgba[i*4]=r; rgba[i*4+1]=g; rgba[i*4+2]=b; rgba[i*4+3]=255;
    }
    return rgba;
  }

  function mulberry(seed){ let a=seed>>>0; return function(){ a|=0; a=(a+0x6D2B79F5)|0;
    let t=Math.imul(a^(a>>>15),1|a); t=(t+Math.imul(t^(t>>>7),61|t))^t; return ((t^(t>>>14))>>>0)/4294967296; }; }

  root.IsoSolid = { S, DEG, DEFAULT_ELEV, GAIN, BIAS, EDGE, LN, BAYER, KEYLINE_DEFAULT,
    box, bar, tube, prism, rotZ, rotY, move, chain, camBasis, proj, paint, mulberry, hex2rgb };
})(typeof globalThis!=='undefined'?globalThis:window);
