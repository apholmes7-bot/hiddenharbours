/* Hidden Harbours — BOAT CUTAWAY rig (pass 1, 2026-09-02). One module, many hulls (the
   boatInteriorRig precedent): the DEPTH-CORRECT section composite that replaces the proxy
   "room sprite pasted over a culled exterior" — which painted the room's sole across the near
   hull side, left the roof arch and aerials floating over an open room, and read as a box set
   down on the boat rather than a room inside it.

   THE RULE SET (what the engine mirrors):
     1. LEVEL    every face tagged with the level goes, plus its lid and every level whose sole sits
                 at/above its ceiling (cullAbove) — exactly render(dir,{cullLevels}) today.
     2. RIGGING  rigging whose LOWEST vertex is at/above the culled level's ceiling stood on the lid
                 that just vanished — it goes too ('cull'). 'keep' is the old floating behaviour.
     3. BITE     the hull's NEAR side (outward normal toward the camera in plan — the fleet's facing
                 test) is sectioned alongside the level's footprint down to soleZ + sill (0.60 m):
                 the hull's own shell becomes the knee-high stub wall of the dollhouse cut. The top
                 of the stub and its two ends get a light section CAP with a dark rim, so the cut
                 reads as a cut and not as damage. Hulls whose shell is already below the cut plane
                 (ship bridges) are untouched — the clip is a no-op there.
     4. DEPTH    exterior and room are merged per PIXEL by depth: both renders now carry rgba.dep.
                 The stub occludes the room's floor edge, the room's far walls occlude the far side
                 deck, nothing overlays anything it is behind. Where the two sources meet across a
                 depth step > 0.30 m the far pixel takes the hull's key colour — the sprite outline
                 continues along the cut.

   Byte discipline: with no opts.cutaway every hull renders byte-identical. rgba.dep is a property
   on the returned array — invisible to every existing consumer.

   PASS 2 (same day) — three rule refinements from the fleet scan, no new semantics:
     1b. cullAbove means STACKED OVER: a higher level clear of the cut level's footprint in plan stays
         (the 53's flybridge over the salon while the below flat is cut). footprint() reads a plan
         published by geometry() for open decks (rec.y0/y1) when HOUSE.decks has none.
     2b. every level lifted by cullAbove contributes its own roof (its sole, if open) to the lid list,
         so rigging that stood on it drops too (the 90's tower on the skylounge, the dragger's radar).
     3b. the section CAP is emitted only where the shell actually reached the cut plane — a room above
         the sheer (ship bridge, skylounge) no longer grows a floating cap strip.
     5b. only an enclosed ROOM (ceilingZ set, footprint not open) is looked THROUGH.

   Exposes globalThis.BoatCutaway = { DEFAULTS, filter(faces, E, opts), composite(dir, opts),
     footprint(E, level, rec), cutSet(geometry, level, cullAbove, E), nearSides(dir), resolveEnv(opts) }. */
(function (root) {
  const DEFAULTS = { sill:0.60, bite:true, rigging:'cull', cullAbove:true, cap:true, edge:'key', capW:0.12, through:'end' };
  const WT = 0.07;                                       // the interior's wall inset — the room's sole ends here
  const hFace = (n, dir) => { const th=(dir||0)*Math.PI/4; return n[0]*Math.sin(th) + n[1]*Math.cos(th); };
  const nearSides = (dir) => [-1, 1].filter(s => hFace([s,0,0], dir) < -0.12);
  const hex = (c) => [parseInt(c.slice(1,3),16), parseInt(c.slice(3,5),16), parseInt(c.slice(5,7),16)];

  // ---- polygon clipping (one half-space at a time; keep where fn(p) >= 0) ----
  function tidy(p){
    const q=[]; for(const v of p){ const l=q[q.length-1]; if(!l || Math.hypot(v[0]-l[0], v[1]-l[1], v[2]-l[2]) > 1e-5) q.push(v); }
    if(q.length>1){ const f=q[0], l=q[q.length-1]; if(Math.hypot(f[0]-l[0], f[1]-l[1], f[2]-l[2]) <= 1e-5) q.pop(); }
    if(q.length<3) return null;
    // the painters take the face normal from the first three vertices — start on the widest corner
    let best=-1, bi=0;
    for(let i=0;i<q.length;i++){ const a=q[i], b=q[(i+1)%q.length], c=q[(i+2)%q.length];
      const u=[b[0]-a[0], b[1]-a[1], b[2]-a[2]], v=[c[0]-a[0], c[1]-a[1], c[2]-a[2]];
      const m=Math.hypot(u[1]*v[2]-u[2]*v[1], u[2]*v[0]-u[0]*v[2], u[0]*v[1]-u[1]*v[0]);
      if(m>best){ best=m; bi=i; } }
    if(best<1e-9) return null;
    return q.slice(bi).concat(q.slice(0,bi));
  }
  function clip(poly, fn){
    const out=[], n=poly.length;
    for(let i=0;i<n;i++){ const a=poly[i], b=poly[(i+1)%n], da=fn(a), db=fn(b);
      if(da>=0) out.push(a);
      if((da>=0)!==(db>=0)){ const t=da/(da-db); out.push([a[0]+(b[0]-a[0])*t, a[1]+(b[1]-a[1])*t, a[2]+(b[2]-a[2])*t]); } }
    return tidy(out);
  }
  function newell(v){ let nx=0,ny=0,nz=0; for(let i=0;i<v.length;i++){ const a=v[i], b=v[(i+1)%v.length];
      nx+=(a[1]-b[1])*(a[2]+b[2]); ny+=(a[2]-b[2])*(a[0]+b[0]); nz+=(a[0]-b[0])*(a[1]+b[1]); } return [nx,ny,nz]; }
  function orient(v, want){ const n=newell(v); return (n[0]*want[0]+n[1]*want[1]+n[2]*want[2] < 0) ? v.slice().reverse() : v; }

  // the bite: the pieces of one hull face that survive a box cut on side s, y0..y1, above zCut
  function bite(f, s, y0, y1, zCut){
    const P=f.v;
    if(!P.some(p=>p[1]>y0) || !P.some(p=>p[1]<y1) || !P.some(p=>p[2]>zCut) || !P.some(p=>p[0]*s>0.02)) return [f];
    const out=[], push=(v)=>{ if(v) out.push(Object.assign({}, f, {v})); };
    push(clip(P, p=>y0-p[1]));                              // aft of the footprint — whole
    const R=clip(P, p=>p[1]-y0); if(!R) return out;
    push(clip(R, p=>p[1]-y1));                              // forward of it — whole
    const R2=clip(R, p=>y1-p[1]); if(!R2) return out;
    push(clip(R2, p=>zCut-p[2]));                           // below the cut — the stub
    const R3=clip(R2, p=>p[2]-zCut); if(!R3) return out;
    push(clip(R3, p=>-p[0]*s));                             // a face spanning the centreline keeps its far half
    return out;
  }

  // ---- what the level occupies in plan (from the published HOUSE block, never re-derived) ----
  function footprint(E, level, rec){
    const H = E.HOUSE || (E.loft && E.loft.house); if(!H || !rec) return null;
    const lo=(a,b)=>Math.min(a,b), hi=(a,b)=>Math.max(a,b);
    if(H.kind==='ship' || H.kind==='sport'){
      const d=H.decks && H.decks[level];
      if(!d || d.y0==null){                                    // an open / external deck (the 53's flybridge coaming): geometry() may still publish its plan
        if(rec.y0!=null && rec.y1!=null) return { y0:lo(rec.y0,rec.y1), y1:hi(rec.y0,rec.y1), hx:null, soleZ:rec.soleZ, plate:true, open:true };
        return null; }
      const y1 = d.y1!=null ? d.y1 : (d.front ? d.front.yBot : null); if(y1==null) return null;
      return { y0:lo(d.y0,y1), y1:hi(d.y0,y1), hx:null, soleZ:rec.soleZ, plate:true };
    }
    if(level==='house' && H.yAft!=null) return { y0:lo(H.yAft,H.yFwd), y1:hi(H.yAft,H.yFwd), hx:(y)=>H.hxAt(y)-WT, soleZ:rec.soleZ };
    if(level==='cuddy' && H.cuddy)    return { y0:lo(H.cuddy.y0,H.cuddy.y1), y1:hi(H.cuddy.y0,H.cuddy.y1), hx:null, soleZ:rec.soleZ };
    return null;
  }
  function cutSet(g, level, cullAbove, E){
    const rec=g.levels.filter(l=>l.id===level)[0]; if(!rec) return null;
    const set=new Set([level]);
    if(rec.ceiling && rec.ceiling.lid) set.add(rec.ceiling.lid);
    if(cullAbove!==false && rec.ceilingZ!=null){ const fp=E ? footprint(E, level, rec) : null;
      for(const o of g.levels){ if(o.id===level || o.soleZ==null || o.soleZ < rec.ceilingZ-0.06) continue;
        // STACKED means OVER it: a level that sits higher but clear of the footprint in plan (the 53's
        // flybridge over the salon while the below flat is cut) stays. Unknown plans keep the height test.
        const fo=(fp && E) ? footprint(E, o.id, o) : null;
        if(fp && fo && (fo.y1 < fp.y0-0.3 || fo.y0 > fp.y1+0.3)) continue;
        set.add(o.id); } }
    return set;
  }

  // ---- the section cap: the top of the stub and its two ends, in the hull's own lightest paint ----
  function capFaces(E, fp, s, zCut, dir, c){
    const L=E.loft, out=[], capMat=c.capMat || E.capMat || (E.CREAM ? 'cream' : (E.WHITE ? 'white' : 'hull')), rimMat=c.rimMat || 'iron';
    const xo=(y)=>L.halfAtZ(y, zCut);
    const xi=(y)=>{ const inner = fp.hx ? fp.hx(y) : L.halfAtZ(y, fp.soleZ)-WT; return Math.min(inner, xo(y)-c.capW); };
    const face=(v, want, mat, b, db)=>({ v:orient(v, want), mat, b, db, lv:'hull', cap:true });
    const N=Math.max(4, Math.round((fp.y1-fp.y0)/0.35));
    for(let i=0;i<N;i++){
      const ya=fp.y0+(fp.y1-fp.y0)*i/N, yb=fp.y0+(fp.y1-fp.y0)*(i+1)/N;
      if(Math.max(L.sheerZ(ya), L.sheerZ(yb)) <= zCut+0.01) continue;   // the shell never reached the cut plane here (ship bridges, sport skylounges): nothing was bitten, no cap
      const oa=xo(ya), ob=xo(yb), ia=xi(ya), ib=xi(yb);
      if(oa-ia>0.02 || ob-ib>0.02)
        out.push(face([[s*ia,ya,zCut],[s*oa,ya,zCut],[s*ob,yb,zCut],[s*ib,yb,zCut]], [0,0,1], capMat, 2.6, 0.04));
      out.push(face([[s*(oa-0.05),ya,zCut+0.006],[s*(oa+0.012),ya,zCut+0.006],[s*(ob+0.012),yb,zCut+0.006],[s*(ob-0.05),yb,zCut+0.006]], [0,0,1], rimMat, 0.0, 0.07));
    }
    // the ends: vertical section faces where the hull rises again, only when they face the camera
    for(const [y, ny] of [[fp.y0,-1],[fp.y1,1]]){
      if(hFace([0,ny,0], dir) >= -0.12) continue;
      const zs=L.sheerZ(y); if(zs<=zCut+0.05) continue;
      const o0=xo(y), i0=xi(y), oS=L.halfAtZ(y, zs), iS=Math.min(fp.hx ? fp.hx(y) : oS-c.capW, oS-c.capW);
      out.push(face([[s*i0,y,zCut],[s*o0,y,zCut],[s*oS,y,zs],[s*iS,y,zs]], [0,ny,0], capMat, 1.4, 0.04));
      out.push(face([[s*(o0-0.05),y+ny*0.006,zCut],[s*(o0+0.012),y+ny*0.006,zCut],[s*(oS+0.012),y+ny*0.006,zs],[s*(oS-0.05),y+ny*0.006,zs]], [0,ny,0], rimMat, 0.0, 0.07));
    }
    return out;
  }

  /* plan(E, opts) — everything a cut needs to know, resolved once: the cut set, the footprint, the
     near sides, and the rooms you look THROUGH. Rule 5, the look-through: when the camera faces an
     end of the room squarely (N/S; through:'all' admits the diagonals) and another room sits on that
     end — the cuddy forward of the wheelhouse, the wheelhouse aft of the cuddy — that room's
     enclosure and lid would be the only thing between the camera and the sole. They go too, and
     composite() paints that room as well, so the boat reads as one sectioned model, not a hole. */
  function plan(E, opts){
    const co = opts && opts.cutaway; if(!co) return null;
    const c=Object.assign({}, DEFAULTS, typeof co==='object' ? co : {}); if(!c.level || !E.geometry) return null;
    const g=E.geometry(); const rec=g.levels.filter(l=>l.id===c.level)[0]; if(!rec) return null;
    const set=cutSet(g, c.level, c.cullAbove, E), dir=opts.dir||0;
    const fp=footprint(E, c.level, rec), sides=(fp && c.bite) ? nearSides(dir) : [];
    const through=[], lids=[], lidClips=[];
    const lidOf=(r, f)=>{ const id=r.ceiling && r.ceiling.lid; if(id && f){ set.delete(id); lidClips.push({ lv:id, y0:f.y0, y1:f.y1 }); } };
    if(fp && rec.ceilingZ!=null) lids.push({ y0:fp.y0, y1:fp.y1, z:rec.ceilingZ });
    lidOf(rec, fp);
    // 2b. every level lifted ABOVE this one (cullAbove) takes its own roof with it: rigging that stood on
    //     that roof — the 90's tower on the skylounge, the dragger's radar on the wheelhouse — goes too.
    for(const o of g.levels){ if(o.id===c.level || !set.has(o.id)) continue; const fo=footprint(E, o.id, o); if(!fo) continue;
      lids.push({ y0:fo.y0, y1:fo.y1, z: o.ceilingZ!=null ? o.ceilingZ : o.soleZ }); }
    if(fp && c.through && c.through!=='none'){ const T = c.through==='all' ? 0.12 : 0.80;
      for(const o of g.levels){ if(o.id===c.level || set.has(o.id) || o.ceilingZ==null) continue; const fo=footprint(E, o.id, o); if(!fo || fo.open) continue;   // only an enclosed ROOM is looked through
        const fwd = fo.y0 >= fp.y1-0.3 && hFace([0,1,0], dir) < -T, aft = fo.y1 <= fp.y0+0.3 && hFace([0,-1,0], dir) < -T;
        if(!fwd && !aft) continue;
        through.push(o.id); set.add(o.id); lidOf(o, fo);
        if(o.ceilingZ!=null) lids.push({ y0:fo.y0, y1:fo.y1, z:o.ceilingZ }); } }
    return { c, g, rec, set, dir, fp, sides, through, lids, lidClips, zCut:rec.soleZ+c.sill, roofZ:rec.ceilingZ };
  }

  /* filter(faces, E, opts) — called by each hull's render() when opts.cutaway is set.
     opts.cutaway: { level, sill, bite, rigging:'cull'|'keep', cullAbove, cap, capMat, through } (DEFAULTS fill the rest). */
  function filter(fl, E, opts){
    const P=plan(E, opts); if(!P) return fl;
    const { c, set, dir, fp, sides, zCut, lids, lidClips }=P;
    const stoodOnLid=(f)=>{ let lo=f.v[0]; for(const p of f.v) if(p[2]<lo[2]) lo=p;
      return lids.some(l=> lo[2]>=l.z-0.35 && lo[1]>=l.y0-0.3 && lo[1]<=l.y1+0.3); };
    // 1b. a LID is lifted over the room's footprint only — the foredeck forward of the cuddy, the main
    //     deck aft of the below-deck flat, stay: no sky through the bow, no hollow tub.
    const lidPieces=(f, L)=>{ const P=f.v, y0=L.y0-0.05, y1=L.y1+0.05;
      if(!P.some(p=>p[1]>y0) || !P.some(p=>p[1]<y1)) return [f];
      const out=[], push=(v)=>{ if(v) out.push(Object.assign({}, f, {v})); };
      push(clip(P, p=>y0-p[1])); push(clip(P, p=>p[1]-y1)); return out; };
    const out=[];
    for(const f of fl){
      if(set.has(f.lv)) continue;                                                        // 1. the level, the levels above (+5. the look-through)
      const L=lidClips.filter(x=>x.lv===f.lv)[0]; if(L){ out.push.apply(out, lidPieces(f, L)); continue; }
      if(f.lv==='rigging' && c.rigging==='cull' && stoodOnLid(f)) continue;               // 2. rigging whose foot stood on a vanished lid
      if(fp && sides.length && f.lv==='hull'){                                            // 3. the bite
        let pieces=[f]; for(const s of sides){ const nx=[]; for(const q of pieces) nx.push.apply(nx, bite(q, s, fp.y0, fp.y1, zCut)); pieces=nx; }
        out.push.apply(out, pieces); continue; }
      out.push(f);
    }
    if(fp && sides.length && c.cap) for(const s of sides) out.push.apply(out, capFaces(E, fp, s, zCut, dir, c));
    return out;
  }

  // ---- the composite: exterior (sectioned) + room, merged by depth, one sprite in the hull cell ----
  function resolveEnv(opts){
    const BI=root.BoatInterior, meta=BI && BI.HULLS[opts.hull]; if(!meta) return null;
    let E=root[meta.sym]; if(!E) return null;
    if(meta.pick && E.byId) E=E.byId(meta.pick);
    if(meta.variantAware && E.interiorEnv) E=E.interiorEnv(opts.variant||null);
    return E;
  }
  function composite(dir, opts){
    opts=opts||{}; const BI=root.BoatInterior, E=resolveEnv(opts); if(!E || !BI) return null;
    const c=Object.assign({}, DEFAULTS, opts.cutaway||{}, { level:opts.level });
    const motion={ roll:opts.roll, pitch:opts.pitch, heave:opts.heave };
    const base=Object.assign({ doorOpen:opts.doorOpen, paint:opts.paint }, motion, opts.variant||{});
    const ext=E.render(dir, Object.assign({}, base, { cutaway:c }));
    const P=E.geometry ? plan(E, Object.assign({}, base, { dir, cutaway:c })) : null, levels=[opts.level].concat(P ? P.through : []);
    const rooms=levels.map(L=>BI.render(dir, Object.assign({ hull:opts.hull, level:L, variant:opts.variant||null, night:!!opts.night, focus:opts.focus||null, doorOpen:opts.doorOpen }, motion)));
    const W=E.W, H=E.H, n=W*H, out=new Uint8ClampedArray(n*4), dep=new Float32Array(n), src=new Uint8Array(n);
    const de=P ? ext.dep : null;                 // no level tags on this hull (the sport fishers): nothing was culled, so the room is pasted over — the V1 fallback
    for(let i=0;i<n;i++){ const j=i*4; let pick=0, best=Infinity, from=null;
      if(ext[j+3]){ pick=1; best=de?de[i]:Infinity; from=ext; }
      for(const room of rooms){ if(!room[j+3]) continue; const d=(de&&room.dep)?room.dep[i]:-Infinity; if(pick===0 || d<best){ pick=2; best=d; from=room; } }
      if(pick){ out[j]=from[j]; out[j+1]=from[j+1]; out[j+2]=from[j+2]; out[j+3]=255; dep[i]=isFinite(best)?best:(pick===1&&de?de[i]:0); }
      src[i]=pick; }
    if(c.edge==='key' && de){                                                            // 4. the outline continues along the cut
      const K=hex(E.KEY || (E.loft && E.loft.shade && E.loft.shade.KEY) || '#0d1013'), mark=new Uint8Array(n), EDGE=0.30;
      for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!src[i]) continue;
        for(const [dx,dy] of [[1,0],[0,1]]){ const nx=x+dx, ny=y+dy; if(nx>=W||ny>=H) continue; const k=ny*W+nx;
          if(!src[k] || src[k]===src[i] || !isFinite(dep[i]) || !isFinite(dep[k])) continue;
          if(Math.abs(dep[i]-dep[k])>EDGE) mark[dep[i]>dep[k] ? i : k]=1; } }
      for(let i=0;i<n;i++) if(mark[i]){ const j=i*4; out[j]=K[0]; out[j+1]=K[1]; out[j+2]=K[2]; }
    }
    out.dep=dep; out.src=src; out.W=W; out.H=H; out.levels=levels; return out;
  }

  root.BoatCutaway = { DEFAULTS, filter, plan, composite, footprint, cutSet, nearSides, resolveEnv, WT };
})(typeof globalThis!=='undefined' ? globalThis : window);
