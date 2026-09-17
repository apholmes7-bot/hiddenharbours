/* Hidden Harbours — shared INTERIOR PLACEMENT engine (art side).
   globalThis.InteriorPlacer

   Extracted from Art/rowhouseUnitIsoRig.js so every interior rig places furniture the same way and
   the collision rules live in ONE place. Art/walkupUnitIsoRig.js uses it; the rowhouse rig is the
   original home of this code and should adopt this module on its next pass.

   LANDINGS ARE RESERVED BEFORE FURNITURE (manor/cottage precedent, 2026-09-16). clearFor() takes a
   `landings` list alongside the flights and wells, and landingsOf() derives the pair from a flight
   record. Reserving the flight alone is not enough: the 1.05 m of floor you arrive on and step off
   onto is walkable route, and a dresser placed there is a stair you cannot use. The manor pass
   reserved landings BEFORE furnishings for exactly this reason and it is the rule here now.
   reserveStand() applies the same rule to a published anchor: the pad an NPC stands on to use a
   fixture is held against the next piece into the room.

   WHAT IT OWNS (the rules that took a lot of debugging to get right):
     · props come from PropIso.emit() — this module never models geometry
     · ctx.clear  keep-clear rects: walls, doorway zones, stair flights, stairwell voids
       ctx.taken  props already placed on this storey
       BOTH are per-storey and must be reset by the host at the top of each storey pass. A
       partition or doorway that is not storey-filtered reserves space on floors it is not on —
       that is what put walls through beds and left bedrooms with nowhere for a headboard.
     · hits() guards non-finite rects. Every comparison against NaN is false, so a malformed rect
       would otherwise read as "overlaps everything" and silently reject every prop in the unit.
     · a room rect's edge is the CENTRELINE of the wall on it, so the default margin and inset
       clear half a partition or flush pieces bury themselves in the plaster
     · run-length props (counter, sofa, bench, shelf, table, vanity, rug) SHRINK to the wall they
       are given rather than refusing to appear
     · PropIso props flagged headToWall (the bed) face their wall instead of the room, and may
       slide ALONG a wall but never be pulled off it — a headboard 0.6 m into the room was the
       defect the pull-off retry introduced
     · every rejection is recorded with a reason, so a rig can publish what it could not place
       instead of leaving a bare wall unexplained

   USE:  const PL = InteriorPlacer.create({ boxSolid, slab, r3, clamp });
         PL.put(out, ctx, room, z, 'bed', {variant:0}, {wall:'rear', along:0.5});
         PL.putAny(out, ctx, room, z, 'dresser', {}, ['left','front','right'], {along:0.24});
   ctx must carry: { clear:[], taken:[], rejects:[], blockers:[], anchors:[], mats:{}, pn:0,
                     storey, weather, night }. */
(function (root) {
  function create(H){
    const boxSolid = H.boxSolid, slab = H.slab, r3 = H.r3, clamp = H.clamp;

    function P(out, ctx, o){
      boxSolid(out, o.x0,o.x1, o.y0,o.y1, o.z0,o.z1, o.mat, o.tex||null, o.b||0, o.topB!=null?o.topB:0.25);
      if(o.blocker!==false) ctx.blockers.push({ kind:o.kind||'prop', storey:ctx.storey,
        x0:r3(o.x0),x1:r3(o.x1),y0:r3(o.y0),y1:r3(o.y1), h:r3(o.z1) });
    }
    function A(ctx, verb, room, x, y, z, extra){
      ctx.anchors.push(Object.assign({ verb, room, storey:ctx.storey, x:r3(x), y:r3(y), z:r3(z||0) }, extra||{}));
    }
    function finite(r){ return Number.isFinite(r.x0)&&Number.isFinite(r.x1)&&Number.isFinite(r.y0)&&Number.isFinite(r.y1); }
    function hits(a, c){ if(!finite(c)||!finite(a)) return false;
      return !(a.x1<=c.x0+0.02 || a.x0>=c.x1-0.02 || a.y1<=c.y0+0.02 || a.y0>=c.y1-0.02); }
    function blocked(ctx, rect){
      for(const c of ctx.clear) if(hits(rect,c)) return c.tag||'clearance';
      for(const t of ctx.taken) if(hits(rect,t)) return t.name||'a prop';
      return null;
    }

    const WALLFACE = { rear:'S', front:'N', left:'E', right:'W' };
    const OPPOSITE = { N:'S', S:'N', E:'W', W:'E' };

    function put(out, ctx, r, z, name, opts, pl){
      const PI = root.PropIso; if(!PI || !PI.emit) return null;
      pl = pl || {};
      const spec = PI.PROPS[name];
      let face = pl.face || WALLFACE[pl.wall] || 'N';
      if(spec && spec.headToWall && pl.wall && pl.wall!=='free') face = OPPOSITE[face] || face;
      const o = Object.assign({ weather:ctx.weather, night:ctx.night }, opts||{});
      const rot = (face==='E'||face==='W');
      if(spec && spec.runBase!=null && o.len!=null && pl.fitRun!==false){
        const avail = ((pl.wall==='left'||pl.wall==='right') ? (r.y1-r.y0) : (r.x1-r.x0))
                      - 2*(pl.margin!=null?pl.margin:0.13);
        const maxLen = (avail - spec.runBase) / spec.runK;
        if(maxLen < o.len) o.len = Math.max(0, maxLen);
      }
      const fp = PI.footprint(name, o);
      const fw = rot?fp.d:fp.w, fd = rot?fp.w:fp.d;
      const m = pl.margin!=null?pl.margin:0.13, ins = pl.inset!=null?pl.inset:0.12;
      const rw = r.x1-r.x0, rd = r.y1-r.y0;
      const onX = (pl.wall==='rear' || pl.wall==='front'), onY = (pl.wall==='left' || pl.wall==='right');
      const rej=(why)=>{ ctx.rejects.push({ prop:name, room:(pl.room||r.id||'?'), storey:ctx.storey, why }); return null; };
      if(onX && (fw > rw-2*m || fd > rd-0.10)) return rej('room too small: needs '+fw.toFixed(2)+'x'+fd.toFixed(2)+', room '+rw.toFixed(2)+'x'+rd.toFixed(2));
      if(onY && (fd > rd-2*m || fw > rw-0.10)) return rej('room too small: needs '+fw.toFixed(2)+'x'+fd.toFixed(2)+', room '+rw.toFixed(2)+'x'+rd.toFixed(2));
      const cand=(t, dIns)=>{
        const off = ins + (dIns||0);
        let px, py;
        if(onX){ px = r.x0+m+fw/2 + Math.max(0, rw-2*m-fw)*t;
                 py = pl.wall==='rear' ? r.y0+off+fd/2 : r.y1-off-fd/2; }
        else if(onY){ py = r.y0+m+fd/2 + Math.max(0, rd-2*m-fd)*t;
                      px = pl.wall==='left' ? r.x0+off+fw/2 : r.x1-off-fw/2; }
        else { px = r.x0 + rw*(pl.fx!=null?pl.fx:0.5); py = r.y0 + rd*(pl.fy!=null?pl.fy:0.5); }
        px += pl.dx||0; py += pl.dy||0;
        return { cx:px, cy:py, x0:px-fw/2, x1:px+fw/2, y0:py-fd/2, y1:py+fd/2 };
      };
      let rect = cand(pl.along!=null?pl.along:0.5);
      const first = !pl.ignoreClear && blocked(ctx, rect);
      if(first){
        let ok=null;
        const pullOff = (spec && spec.headToWall) ? [0] : [0, 0.24, 0.46];
        if(onX||onY){ outerW: for(const dIns of pullOff){
            for(const t of [0.5, 0, 1, 0.25, 0.75, 0.12, 0.88, 0.38, 0.62]){
              const c=cand(t, dIns);
              if(c.x0<r.x0+0.03||c.x1>r.x1-0.03||c.y0<r.y0+0.03||c.y1>r.y1-0.03) continue;
              if(!blocked(ctx,c)){ ok=c; break outerW; } } } }
        else { const bx=pl.fx!=null?pl.fx:0.5, by=pl.fy!=null?pl.fy:0.5;
          outer: for(const dy of [0, -0.10, 0.10, -0.20, 0.20, -0.30, 0.30])
            for(const dx of [0, -0.10, 0.10, -0.18, 0.18]){
              const t=clamp(bx+dx,0.10,0.90), v=clamp(by+dy,0.10,0.90);
              const c={ cx:r.x0+rw*t+(pl.dx||0), cy:r.y0+rd*v+(pl.dy||0) };
              c.x0=c.cx-fw/2; c.x1=c.cx+fw/2; c.y0=c.cy-fd/2; c.y1=c.cy+fd/2;
              if(c.x0<r.x0+0.04||c.x1>r.x1-0.04||c.y0<r.y0+0.04||c.y1>r.y1-0.04) continue;
              if(!blocked(ctx,c)){ ok=c; break outer; } } }
        if(!ok) return rej('no clear slot — blocked by '+first);
        rect=ok;
      }
      const prefix = 'q'+(ctx.pn++)+'_';
      const em = PI.emit(name, o, { x:rect.cx, y:rect.cy, z, face, prefix });
      for(const fc of em.faces) out.push(fc);
      for(const k in em.mats) ctx.mats[k] = em.mats[k];
      rect.name=name; rect.face=face; rect.h=em.h;
      ctx.blockers.push({ kind:name, storey:ctx.storey, x0:r3(rect.x0), x1:r3(rect.x1),
                          y0:r3(rect.y0), y1:r3(rect.y1), h:r3(em.h) });
      if(!pl.ignoreClear) ctx.taken.push(rect);
      return rect;
    }

    // try a list of walls in order; a piece that lands is not reported as unplaced
    function putAny(out, ctx, r, z, name, opts, walls, pl){
      const mark = ctx.rejects.length;
      for(const wl of walls){
        const got = put(out, ctx, r, z, name, opts, Object.assign({}, pl||{}, {wall:wl}));
        if(got){ ctx.rejects.length = mark; return got; }
      }
      return null;
    }

    // a floor slab with rectangular holes cut out of it (stairwell / lift shaft voids)
    function slabMinus(out, x0,x1,y0,y1, z, mat, bias, tex, voids){
      let rects=[[x0,x1,y0,y1]];
      for(const v of (voids||[])){
        if(!v) continue;
        const next=[];
        for(const q of rects){
          const a0=q[0],a1=q[1],b0=q[2],b1=q[3];
          if(v.x1<=a0+0.001||v.x0>=a1-0.001||v.y1<=b0+0.001||v.y0>=b1-0.001){ next.push(q); continue; }
          const cx0=Math.max(a0,v.x0), cx1=Math.min(a1,v.x1), cy0=Math.max(b0,v.y0), cy1=Math.min(b1,v.y1);
          if(b0<cy0-0.001) next.push([a0,a1,b0,cy0]);
          if(cy1<b1-0.001) next.push([a0,a1,cy1,b1]);
          if(a0<cx0-0.001) next.push([a0,cx0,cy0,cy1]);
          if(cx1<a1-0.001) next.push([cx1,a1,cy0,cy1]);
        }
        rects=next;
      }
      for(const q of rects) if(q[1]-q[0]>0.02 && q[3]-q[2]>0.02)
        slab(out, [[q[0],q[2]],[q[1],q[2]],[q[1],q[3]],[q[0],q[3]]], z, mat, bias, tex);
    }

    // the hole a flight needs in the plate above it, published by the stair itself so the plan and
    // the cut-out can never disagree
    function stairVoid(st){
      if(!st) return null;
      return { x0:st.x0-0.05, x1:st.x1+0.05, y0:st.voidY0-0.05, y1:st.voidY1 };
    }

    // The two pieces of floor a flight needs kept clear: the DEPARTURE apron at its foot and the
    // ARRIVAL apron past its head. Every flight in this family climbs in +y (dir rises_to_front /
    // rises_to_street), so the foot is at y0 and the head at y1. Returned tagged, so a rejected
    // prop names the landing rather than a bare 'clearance'.
    const LANDING_D = 1.05;
    function landingsOf(st, depth){
      if(!st) return [];
      const d = depth!=null ? depth : LANDING_D;
      const x0 = st.x0-0.10, x1 = st.x1+0.10;
      return [
        { tag:'the bottom landing', end:'bottom', x0, x1, y0:st.y0-d, y1:st.y0 },
        { tag:'the top landing',    end:'top',    x0, x1, y0:st.y1,   y1:st.y1+d },
      ];
    }

    // segment an axis run into the pieces left after its door gaps are removed
    function segsOf(a0, a1, gaps){
      let list=[[a0,a1]];
      for(const [g0,g1] of (gaps||[])){
        const next=[];
        for(const [s0,s1] of list){
          if(g1<=s0 || g0>=s1){ next.push([s0,s1]); continue; }
          if(s0<g0) next.push([s0,g0]);
          if(g1<s1) next.push([g1,s1]);
        }
        list=next;
      }
      return list;
    }
    function drawPartition(out, ctx, p, z0, z1, t, mat, bias, topB){
      const b0 = bias!=null?bias:0.40, bt = topB!=null?topB:0.45;
      for(const [s0,s1] of segsOf(p.a0,p.a1,p.gaps)){
        if(s1-s0<0.02) continue;
        if(p.axis==='x'){ boxSolid(out, s0,s1, p.plane-t/2,p.plane+t/2, z0,z1, mat, null, b0, bt);
          ctx.blockers.push({kind:'wall', storey:ctx.storey, x0:r3(s0),x1:r3(s1), y0:r3(p.plane-t/2), y1:r3(p.plane+t/2), h:r3(z1-z0)}); }
        else { boxSolid(out, p.plane-t/2,p.plane+t/2, s0,s1, z0,z1, mat, null, b0, bt);
          ctx.blockers.push({kind:'wall', storey:ctx.storey, x0:r3(p.plane-t/2),x1:r3(p.plane+t/2), y0:r3(s0),y1:r3(s1), h:r3(z1-z0)}); }
      }
    }
    // Door open 180deg, FLAT against the wall beside its opening. A leaf swung into the room reads
    // as a stray half-wall and collides with whatever stands against that wall.
    function drawLeaf(out, d, z0, hLeaf, mat, t){
      const th=0.042, lw=d.clearW*0.92, off=(t||0.12)/2+th/2+0.004;
      if(d.axis==='x'){ const e=d.c-d.clearW/2;
        boxSolid(out, e-lw, e, d.plane+off-th/2, d.plane+off+th/2, z0, z0+hLeaf, mat, null, 0.15, 0.4); }
      else { const e=d.c-d.clearW/2;
        boxSolid(out, d.plane+off-th/2, d.plane+off+th/2, e-lw, e, z0, z0+hLeaf, mat, null, 0.15, 0.4); }
    }

    // THE FLOOR A PUBLISHED ANCHOR STANDS ON, held against the next piece in the room. The landing
    // rule applied to furniture: an approach is not part of a footprint, so nothing stopped a
    // dresser standing exactly where you get into the bed it serves. On captains2 one did, and the
    // audit published no_clear_spot for a bed in a generous 10.4 m2 room.
    //
    // It is a PAD, not a strip. Reserving the bed's whole length on both sides reads as tidy and is
    // wrong: it takes the wall the wardrobe needs and evicts it from every 2.9 m bedroom in the
    // building (180 unplaced pieces, measured). The audit only ever marches a 0.22 m body to a
    // point, so a 0.52 m pad on that point is exactly the floor that has to stay empty.
    //
    // Call it straight after the piece lands, BEFORE the loose furniture goes in.
    function reserveStand(ctx, pts, rad, tag){
      const R = rad!=null ? rad : 0.26;
      for(const p of (pts||[])){
        if(!p || !Number.isFinite(p[0]) || !Number.isFinite(p[1])) continue;
        const c={ tag: tag||'a standing spot', x0:p[0]-R, x1:p[0]+R, y0:p[1]-R, y1:p[1]+R };
        ctx.clear.push(c);
        if(ctx.clearAll) ctx.clearAll.push(Object.assign({storey:ctx.storey}, c));
      }
    }

    // push this storey's keep-clear rects: partitions, doorways, flights, wells. TAGGED, so a
    // rejection reason names what actually blocked the piece.
    function clearFor(ctx, opts){
      const t=(opts.partT||0.12)/2+0.02;
      for(const p of (opts.parts||[])){
        if(p.storey!==ctx.storey) continue;
        if(p.axis==='x') ctx.clear.push({ tag:'a wall', x0:p.a0, x1:p.a1, y0:p.plane-t, y1:p.plane+t });
        else ctx.clear.push({ tag:'a wall', x0:p.plane-t, x1:p.plane+t, y0:p.a0, y1:p.a1 });
      }
      for(const d of (opts.doors||[])){
        if(d.storey!==ctx.storey) continue;
        const half=d.clearW/2+0.06, dep=0.46;
        if(d.axis==='x') ctx.clear.push({ tag:'a doorway', x0:d.c-half, x1:d.c+half, y0:d.plane-dep, y1:d.plane+dep });
        else ctx.clear.push({ tag:'a doorway', x0:d.plane-dep, x1:d.plane+dep, y0:d.c-half, y1:d.c+half });
      }
      for(const v of (opts.voids||[])) if(v) ctx.clear.push(Object.assign({tag:'the stairwell'}, v));
      for(const f of (opts.flights||[])) if(f) ctx.clear.push({ tag:'the flight',
        x0:f.x0-0.05, x1:f.x1+0.05, y0:f.y0-0.05, y1:f.y1+0.05 });
      for(const l of (opts.landings||[])) if(l) ctx.clear.push(l);
      for(const s of (opts.shafts||[])) if(s) ctx.clear.push(Object.assign({tag:'the lift shaft'}, s));
    }

    return { P, A, hits, blocked, finite, put, putAny, slabMinus, stairVoid, landingsOf, LANDING_D,
             reserveStand, segsOf, drawPartition, drawLeaf, clearFor, WALLFACE, OPPOSITE };
  }
  root.InteriorPlacer = { create };
})(typeof globalThis!=='undefined'?globalThis:window);
