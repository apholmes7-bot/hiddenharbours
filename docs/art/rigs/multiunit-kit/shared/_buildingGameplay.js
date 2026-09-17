/* Hidden Harbours — shared BUILDING GAMEPLAY sections (art side).
   globalThis.BuildingGameplay

   Extracted from Art/rowhouseUnitIsoRig.js (the terrace was the first multi-unit dwelling and wrote
   all of this inline) so that every multi-unit interior rig emits the SAME sidecar sections from the
   same code. Art/walkupUnitIsoRig.js and Art/stackUnitIsoRig.js use it; the rowhouse rig now calls
   it too, so the three phases cannot drift into three dialects of one schema.

   WHAT IT OWNS
     · SOLE       one walkable polygon per room per storey, with wet / circulation flags, obstruction
                  _notes, and a stairwell published as a real `holes` entry
     · THRESHOLD  every interior door with its mechanism and keep_clear rule
     · STAIRS     the flight's kinematics, its landings, and the void it needs in the plate above
     · INTERACT   one entry per anchor, hosted on a real fixture, with a TESTED reach point
     · BLOCKERS   every wall, stair and prop with height and a treatment
     · auditReach the body-march that turns a requested standing spot into a tested one

   TWO RULES THAT ARE NOT NEGOTIABLE, both learned the hard way:

   1. SOLE CARRIES REAL HOLES, and only for voids. The folder rule is that deck polygons do not
      carry holes for furniture — obstructions ride in _notes. A stairwell is not furniture: an NPC
      who walks over one falls through a floor. The same rect is published as the flight's
      void_above, so the plan and the cut-out cannot disagree.

   2. REACH IS TESTED INSIDE THE ANCHOR'S OWN UNIT. An early terrace run marched across party walls
      and had units 2..N blocked by the neighbour's furniture. A point that cannot clear is
      published as null WITH a reason, never as a plausible lie.

   LANDINGS (new, 2026-09-16, manor/cottage precedent). STAIRS[].landings carries the departure and
   arrival aprons the placer reserved before furnishing, so the game can see that the route off a
   flight is floor and not a sideboard.

   USE:  BuildingGameplay.sections({ rooms, doors, stairs, voids, blockers, anchors, rejects,
                                     storeyZ, ceilH, roomH, storeyRise, unitOf, interiorRig }) */
(function (root) {
  const r3 = (n) => +(+n).toFixed(3);

  // the fixture each verb acts on, so an anchor hosts on the right prop
  const HOST_KINDS = { sleep:['bed'], cook:['stove','range'], wash_dishes:['sink','wetSink'],
    dine:['table','counter','stool'], sit:['sofa','bench','armchair'], toilet:['toilet'],
    bathe:['tub','shower'], vanity:['vanity','washstand'], laundry:['washer','washtub','dryer'],
    store:['wardrobe','shelf','icebox','fridge','crate','barrel','locker','table'], desk:['table'] };
  const VERB_ACTION = { sleep:'sleep_save', cook:'cook', wash_dishes:'wash_dishes', dine:'dine',
    sit:'sit', toilet:'toilet', bathe:'bathe', vanity:'wash', laundry:'laundry', store:'storage',
    desk:'desk', stair:'use_stair', entry:'enter', lift:'use_lift', mail:'collect_mail',
    balcony:'step_out', porch:'step_out' };
  const ROUTE_VERBS = { stair:1, entry:1, lift:1 };

  // reach audit — march a body out from the fitting along its anchor line until it clears every
  // blocker at that level. A point that cannot clear is published null WITH a reason.
  function auditReach(anchors, blockers, rooms, opt){
    const BODY=(opt&&opt.body)||0.22, ARM=(opt&&opt.arm)||1.20, STEP=0.06, TRIES=20;
    const sameUnit=(a,b)=>!a || !b || a===b;
    const hit=(x,y,st,skip,unit)=>{
      for(const q of blockers){
        if(q.storey!==st || q===skip) continue;
        if(unit && q.unit && q.unit!==unit) continue;    // never the neighbour's kit across a party wall
        if(q.kind!=='wall' && q.h<0.35) continue;        // a 0.30 m step-over is not a blocker
        if(x>q.x0-BODY && x<q.x1+BODY && y>q.y0-BODY && y<q.y1+BODY) return q.kind;
      }
      return null;
    };
    const inRoom=(x,y,st,unit)=>rooms.some(r=>r.storey===st && sameUnit(unit,r.unit) &&
      x>r.x0+0.05 && x<r.x1-0.05 && y>r.y0+0.05 && y<r.y1-0.05);
    let ok=0, moved=0, flipped=0, nulled=0;
    for(const a of anchors){
      const host=blockers.find(q=>q.storey===a.storey && q.kind===a.host && sameUnit(a.unit,q.unit));
      const same=rooms.filter(v=>v.storey===a.storey && sameUnit(a.unit,v.unit));
      const r=same.find(v=>a.x>=v.x0 && a.x<=v.x1 && a.y>=v.y0 && a.y<=v.y1)
           || same.map(v=>({v, d:Math.hypot((v.x0+v.x1)/2-a.x, (v.y0+v.y1)/2-a.y)}))
                  .sort((p,q)=>p.d-q.d).map(o=>o.v)[0];
      if(!r){ a.reach={tested:true, verdict:'no_room', point:null}; nulled++; continue; }
      const cx=(r.x0+r.x1)/2, cy=(r.y0+r.y1)/2;
      let vx=a.x-cx, vy=a.y-cy; const m=Math.hypot(vx,vy)||1; vx/=m; vy/=m;
      // into the room first (away from the fixture), then reversed, then the four axes
      const tries=[[-vx,-vy],[vx,vy],[1,0],[-1,0],[0,1],[0,-1]];
      let found=null, dist=0, which=0;
      for(let t=0;t<tries.length && !found;t++){
        for(let k=0;k<TRIES;k++){
          const d=STEP*(k+1); if(d>ARM) break;
          const px=a.x+tries[t][0]*d, py=a.y+tries[t][1]*d;
          if(!inRoom(px,py,a.storey,a.unit)) continue;
          if(!hit(px,py,a.storey,host,a.unit)){ found=[r3(px),r3(py)]; dist=d; which=t; break; }
        }
      }
      if(!found){
        a.reach={tested:true, verdict:'no_clear_spot', point:null, on:r.id||null,
                 body_r_m:BODY, arm_m:ARM}; nulled++;
      } else {
        const asked=Math.hypot(found[0]-a.x, found[1]-a.y)<0.07;
        const verdict = asked ? 'ok' : (which===1 ? 'flipped' : 'moved');
        a.reach={tested:true, verdict, point:found, on:r.id||null, dist_m:r3(dist),
                 moved_m: asked?0:r3(Math.hypot(found[0]-a.x, found[1]-a.y)),
                 body_r_m:BODY, arm_m:ARM};
        if(asked) ok++; else if(verdict==='flipped') flipped++; else moved++;
      }
    }
    return { checked:anchors.length, ok, moved, flipped, nulled };
  }

  function sections(I){
    const storeyZ = I.storeyZ, lvl = I.levelId || ((s)=>'storey_'+s);
    const rooms = I.rooms, blockers = I.blockers, voids = I.voids||[];
    const unitOf = I.unitOf || (()=>null);
    const zOf = (s)=>r3(storeyZ[s]!=null?storeyZ[s]:0);
    const roomHOf = (s)=>typeof I.roomH==='function' ? r3(I.roomH(s)) : r3(I.roomH);
    const riseOf  = (s)=>{ const v = typeof I.storeyRise==='function' ? I.storeyRise(s) : I.storeyRise;
                           return v==null ? null : r3(v); };

    // ---- SOLE
    const SOLE = rooms.map(r=>{
      const holes = voids.filter(v=>v.storey===r.storey &&
          v.x0>=r.x0-0.10 && v.x1<=r.x1+0.10 && v.y0>=r.y0-0.10 && v.y1<=r.y1+0.10)
        .map(v=>({ reason: v.tag==='the lift shaft' ? 'lift_shaft' : 'stairwell',
          polygon:[[r3(v.x0),r3(v.y0)],[r3(v.x1),r3(v.y0)],[r3(v.x1),r3(v.y1)],[r3(v.x0),r3(v.y1)]] }));
      const obstructions = blockers.filter(q=>q.storey===r.storey && q.kind!=='wall' && q.kind!=='stair' &&
          q.x0>=r.x0-0.20 && q.x1<=r.x1+0.20 && q.y0>=r.y0-0.20 && q.y1<=r.y1+0.20)
        .map(q=>({ what:q.kind, footprint:[[q.x0,q.y0],[q.x1,q.y1]], height_above_sole_m:q.h,
          treatment: q.h<0.32?'step_over' : (q.h<1.20?'waist_block':'wall') }));
      return { id:r.uid||r.id, unit:r.unit||null, level:lvl(r.storey), storey:r.storey, kind:r.kind,
        winding:'ccw_from_above', z:zOf(r.storey),
        polygon:[[r3(r.x0),r3(r.y0)],[r3(r.x1),r3(r.y0)],[r3(r.x1),r3(r.y1)],[r3(r.x0),r3(r.y1)]],
        area_m2:r.area!=null?r.area:r3((r.x1-r.x0)*(r.y1-r.y0)),
        room_height_m: roomHOf(r.storey),
        wet: !!(r.wet || r.kind==='bath' || r.kind==='ensuite' || r.kind==='powder' || r.kind==='laundry'),
        circulation: !!(r.route || r.kind==='hall' || r.kind==='landing' || r.kind==='corridor' ||
                        r.kind==='lobby' || r.kind==='stairhall' || r.kind==='vestibule' ||
                        r.kind==='backhall'),
        shared: !!r.core,
        holes: holes.length?holes:undefined,
        _notes: obstructions.length?obstructions:undefined };
    });

    // ---- THRESHOLD
    const THRESHOLD = [];
    for(const d of I.doors){
      if(d.kind==='entry') continue;                      // the street/corridor entry is the shell's
      const mech = d.kind==='open' ? 'opening'
                 : d.kind==='lift' ? 'lift_landing_doors'
                 : d.kind==='slide' ? 'sliding_single' : 'hinged_single';
      THRESHOLD.push({ id:(d.unit?d.unit+'.':'')+d.from+'->'+d.to, unit:d.unit||null, storey:d.storey,
        level:lvl(d.storey), from:d.from, to:d.to, axis:d.axis, plane:r3(d.plane), centre:r3(d.c),
        clear_width_m:r3(d.clearW), clear_height_m:r3(Math.min(2.10, I.ceilH-0.10)),
        mechanism:mech, shared:!!d.core,
        // the leaf is baked flat against the wall beside its opening, so the collider is the
        // opening, not a swept arc — there is no arc to sweep
        keep_clear: (d.kind==='open'||d.kind==='lift') ? null
                  : { rule:'leaf_parks_flat_against_wall', arc:null },
        default_state: d.kind==='open' ? 'permanently_open'
                     : d.kind==='lift' ? 'closed_until_called' : 'shut' });
    }

    // ---- STAIRS. Exact flights: steps x rise_m === floor_rise_m, no rounding drift.
    const STAIRS = I.stairs.map(s=>{
      const fr = s.floorRise!=null ? s.floorRise : s.steps*s.rise;
      const land = s.landings || [];
      const bot = land.find(l=>l.end==='bottom'), top = land.find(l=>l.end==='top');
      return { id:(s.unit?s.unit+'.':'')+(s.id||('stair_s'+s.storey)), unit:s.unit||null,
        from_level:lvl(s.storey), to_level:lvl(s.storey+1), shared:!!s.core,
        steps:s.steps, rise_m:s.rise, run_m:s.run, floor_rise_m:r3(fr),
        exact:{ rule:'steps x rise_m = floor_rise_m', product:r3(s.steps*s.rise) },
        direction:s.dir, footprint:[[s.x0,s.y0],[s.x1,s.y1]],
        bottom:{ x:r3((s.x0+s.x1)/2), y:r3(s.y0-0.25), z:zOf(s.storey) },
        top:{ x:r3((s.x0+s.x1)/2), y:r3(s.y1+0.25), z:zOf(s.storey+1) },
        handrail:{ side:'open_side', height_m:0.90 },
        landings: (bot||top) ? {
          departure: bot ? { level:lvl(s.storey), polygon:[[r3(bot.x0),r3(bot.y0)],[r3(bot.x1),r3(bot.y0)],[r3(bot.x1),r3(bot.y1)],[r3(bot.x0),r3(bot.y1)]] } : null,
          arrival:   top ? { level:lvl(s.storey+1), polygon:[[r3(top.x0),r3(top.y0)],[r3(top.x1),r3(top.y0)],[r3(top.x1),r3(top.y1)],[r3(top.x0),r3(top.y1)]] } : null,
          rule:'reserved before furnishing; no prop may stand here',
        } : undefined,
        void_above:{ polygon:[[s.x0,s.voidY0],[s.x1,s.voidY0],[s.x1,s.voidY1],[s.x0,s.voidY1]],
          rule:'plate above is cut open here; carries no wall and no furniture' } };
    });

    // ---- INTERACT
    const INTERACT = [];
    const seen = {};
    for(const a of I.anchors){
      const k=a.verb+'|'+a.room+'|'+a.storey+'|'+a.x+'|'+a.y; if(seen[k]) continue; seen[k]=1;
      if(ROUTE_VERBS[a.verb]) continue;                   // routes live in STAIRS / ELEVATOR / entry
      const unit = a.unit || unitOf(a.x, a.y, a.storey);
      const edgeD=(q)=>Math.hypot(Math.max(q.x0-a.x,0,a.x-q.x1), Math.max(q.y0-a.y,0,a.y-q.y1));
      const pool = blockers.filter(q=>q.storey===a.storey && q.kind!=='wall' && q.kind!=='stair' &&
        (!unit || !q.unit || q.unit===unit));
      const want = HOST_KINDS[a.verb]||null;
      const pick = (list)=>list.map(q=>({q, d:edgeD(q)})).sort((p,q)=>p.d-q.d)[0];
      const host = (want && pick(pool.filter(q=>want.indexOf(q.kind)>=0))) || pick(pool);
      INTERACT.push({ id:(unit?unit+'.':'')+a.room+'.'+a.verb+
          (a.seat!=null?'_'+a.seat:(a.side?'_'+a.side:'')),
        unit: unit||null, room:a.room, storey:a.storey, level:lvl(a.storey),
        action:VERB_ACTION[a.verb]||a.verb, verb:a.verb,
        host: host && host.d<1.6 ? host.q.kind : null,
        stand:{ x:a.x, y:a.y, z:a.z },
        size:a.size||undefined, side:a.side||undefined, seat:a.seat!=null?a.seat:undefined,
        fixture:a.fixture||a.what||undefined, basin:a.basin!=null?a.basin:undefined,
        shared: !!a.core || undefined });
    }
    const forAudit = INTERACT.map((e,i)=>({ x:e.stand.x, y:e.stand.y, storey:e.storey, unit:e.unit,
      room:e.room, host:e.host, _i:i }));
    const tally = auditReach(forAudit, blockers, rooms);
    forAudit.forEach(a=>{ INTERACT[a._i].reach=a.reach; });

    // ---- BLOCKERS
    const BLOCKERS = blockers.map(q=>({ what:q.kind, level:lvl(q.storey), storey:q.storey,
      unit:q.unit||null, footprint:[[q.x0,q.y0],[q.x1,q.y1]], height_above_sole_m:q.h,
      treatment: q.kind==='wall' ? 'wall' : q.kind==='stair' ? 'stair'
               : (q.h<0.32?'step_over' : (q.h<1.20?'waist_block':'wall')) }));

    return { SOLE, THRESHOLD, STAIRS, INTERACT, BLOCKERS, REACH_AUDIT:tally,
      _interiorNote:'SOLE, THRESHOLD, STAIRS, INTERACT and BLOCKERS are generated by '+
        (I.interiorRig||'the interior rig')+' off the same layout the bake draws, through '+
        'Art/_buildingGameplay.js. Furniture geometry is PropIso\'s — see propsDerivedFromRigSha256.',
      _unplaced:(I.rejects||[]).map(x=>({ prop:x.prop, room:x.room, storey:x.storey, why:x.why })) };
  }

  root.BuildingGameplay = { sections, auditReach, HOST_KINDS, VERB_ACTION, r3 };
})(typeof globalThis!=='undefined'?globalThis:window);
