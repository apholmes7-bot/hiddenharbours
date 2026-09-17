/* Hidden Harbours — multi-unit kit writer + contract harness (art side).
   globalThis.MultiunitKit

   One writer for all three multi-unit phases. The terrace shipped with its own (Art/_rowhouseKit.js)
   because it was the only phase; there are three now, and three writers would have become three
   dialects of one schema the first time somebody fixed a number in only two of them.

   WHAT IT DOES
     · generates every committed sidecar from the live rigs, stamped with FIVE hashes
     · runs the contract: 22 assertions over every committed preset plus a grid per phase
     · returns the tally the kit READMEs are written from

   FIVE HASHES, because five renderers can drift:
     derivedFromRigSha256          the shell
     interiorDerivedFromRigSha256  the rooms
     propsDerivedFromRigSha256     the furniture      (interiorPropRig.js)
     placerDerivedFromRigSha256    where it lands     (_interiorPlacer.js)
     writerDerivedFromRigSha256    the sections       (_buildingGameplay.js)
   The terrace stamped the first three. The last two are shared files: a change to either moves every
   number in every phase while both rig hashes sit still. Art/_sidecarExport.js takes them from the
   bytes on disk at save time and refuses to write an unstamped file.

   USE (from a page with the rigs loaded — these run in the browser, not on node):
     await MultiunitKit.all()                 -> { files, tally, contract }
     await MultiunitKit.sidecar('stack','decker6')
     MultiunitKit.contract()                  -> { configurations, assertions, checks, failed, fails }

   Never edit an output. If a hash moves, re-run this. */
(function (root) {
  const TREAT = ['wall','stair','step_over','waist_block','flat','step_up'];
  const HEXR = /^[0-9a-f]{64}$/;

  function phases(){
    const R = root;
    const out = [];
    if(R.WalkupIso) out.push({ id:'walkup', kit:'walkup-rig-kit', stem:'walkupIsoRig',
      rig:R.WalkupIso, shell:'Art/walkupIsoRig.js', room:'Art/walkupUnitIsoRig.js',
      presets:Object.keys(R.WalkupIso.PRESETS) });
    if(R.StackFlatsIso) out.push({ id:'stack', kit:'stack-rig-kit', stem:'stackFlatsIsoRig',
      rig:R.StackFlatsIso, shell:'Art/stackFlatsIsoRig.js', room:'Art/stackUnitIsoRig.js',
      presets:Object.keys(R.StackFlatsIso.PRESETS) });
    if(R.RowhouseIso) out.push({ id:'rowhouse', kit:'rowhouse-rig-kit', stem:'rowhouseIsoRig',
      rig:R.RowhouseIso, shell:'Art/rowhouseIsoRig.js', room:'Art/rowhouseUnitIsoRig.js',
      presets:['millRow4','duplexPair','coastalRow4'] });
    return out;
  }

  // ---- the 22 assertions. Each one exists because something it catches actually happened.
  function checkOne(g){
    const C=[], ok=(n,c,d)=>C.push({ name:n, pass:!!c, detail:c?'':(d||'') });
    const SZ=(g.building||{}).storey_z||[], H=g.heights||{};
    ok('schema', g.schema==='hidden-harbours/building-gameplay@1', g.schema);
    ok('frame', g.frame && g.frame.scale_px_per_m===32 && g.frame.heading_independent===true);
    ok('unit count', g.UNITS.length===g.build.units, g.UNITS.length+' vs '+g.build.units);
    ok('unit boxes', g.UNITS.every(u=>u.interior_box && u.floor_area_m2>0));
    ok('slots = sleep anchors', g.UNITS.every(u=>u.resident_slots===
        g.INTERACT.filter(x=>x.unit===u.id && x.verb==='sleep').length));
    ok('sole polygons', g.SOLE.every(s=>s.polygon.length===4 &&
        Math.abs((s.polygon[1][0]-s.polygon[0][0])*(s.polygon[2][1]-s.polygon[1][1]))>0.01));
    ok('sole z = storey z', g.SOLE.every(s=>Math.abs(s.z-SZ[s.storey])<1e-6));
    ok('sole room height', g.SOLE.every(s=>Math.abs(s.room_height_m-H.room_height_m[s.storey])<1e-6));
    ok('holes inside sole', g.SOLE.every(s=>!s.holes || s.holes.every(hl=>
        hl.polygon[0][0]>=s.polygon[0][0]-0.15 && hl.polygon[1][0]<=s.polygon[1][0]+0.15 &&
        hl.polygon[0][1]>=s.polygon[0][1]-0.15 && hl.polygon[2][1]<=s.polygon[2][1]+0.15)));
    ok('door widths', g.THRESHOLD.every(t=>t.clear_width_m>=0.70),
       (g.THRESHOLD.filter(t=>t.clear_width_m<0.70)[0]||{}).id);
    ok('door levels', g.THRESHOLD.every(t=>t.storey==null || (t.storey>=0 && t.storey<g.build.storeys)));
    const flights=g.STAIRS.flatMap(s=>s.flights?s.flights:[s]);
    // the rounded-rise defect: a top tread that misses the plate it lands on
    ok('flights exact', flights.every(s=>!s.steps || Math.abs(s.steps*s.rise_m-s.floor_rise_m)<1e-9),
       JSON.stringify(flights.filter(s=>s.steps&&Math.abs(s.steps*s.rise_m-s.floor_rise_m)>=1e-9)[0]||''));
    const inner=g.STAIRS.filter(s=>s.void_above);
    ok('stair z span', inner.every(s=>Math.abs((s.top.z-s.bottom.z)-s.floor_rise_m)<1e-6));
    ok('stair rise = storey rise', inner.every(s=>{
        const f=+String(s.from_level).replace('storey_',''); 
        return Math.abs(s.floor_rise_m-H.storey_rise_m[f])<1e-6; }));
    ok('void_above', inner.every(s=>s.void_above.polygon.length===4));
    ok('landings reserved', inner.every(s=>s.landings && s.landings.departure && s.landings.arrival));
    ok('reach tested', g.INTERACT.every(x=>x.reach && x.reach.tested===true && x.reach.verdict));
    ok('reach not null', g.INTERACT.every(x=>x.reach.point!==null),
       (g.INTERACT.filter(x=>!x.reach.point)[0]||{}).id);
    ok('hosts exist', g.INTERACT.every(x=>!x.host ||
        g.BLOCKERS.some(q=>q.storey===x.storey && q.what===x.host)));
    ok('blocker treatments', g.BLOCKERS.every(q=>TREAT.indexOf(q.treatment)>=0),
       (g.BLOCKERS.filter(q=>TREAT.indexOf(q.treatment)<0)[0]||{}).treatment);
    ok('heights consistent', (H.storey_rise_m||[]).every((rise,s)=>rise===null ||
        Math.abs(H.room_height_m[s]+H.slab_thickness_m-rise)<1e-6));
    ok('nothing unplaced', !(g._unplaced&&g._unplaced.length),
       JSON.stringify((g._unplaced||[])[0]||''));
    return C;
  }

  // every committed preset, plus a grid over each rig's own space — a preset set that passes while
  // the space around it fails is a preset set somebody tuned by hand
  function configurations(){
    const R=root, out=[];
    for(const P of phases()) for(const k of P.presets) out.push({ phase:P.id, name:k, rig:P.rig, opts:P.rig.PRESETS[k] });
    if(R.WalkupIso) for(const tier of ['basic','luxury']) for(const units of [4,8,12])
      for(const storeys of [2,3]) for(const beds of [1,2,3])
        out.push({ phase:'walkup', name:'grid_'+tier+'_'+units+'u'+storeys+'s'+beds+'b',
                   rig:R.WalkupIso, opts:{tier,units,storeys,beds} });
    if(R.StackFlatsIso) for(const tier of ['porch','gambrel']) for(const perFloor of [1,2])
      for(const storeys of [2,3]) for(const beds of [1,2,3])
        out.push({ phase:'stack', name:'grid_'+tier+'_'+perFloor+'pf'+storeys+'s'+beds+'b',
                   rig:R.StackFlatsIso, opts:{tier,storeys,perFloor,beds} });
    if(R.RowhouseIso) for(const tier of ['basic','luxury']) for(const units of [2,4,6])
      for(const storeys of [2,3]) for(const beds of [1,2,3])
        out.push({ phase:'terrace', name:'grid_'+tier+'_'+units+'u'+storeys+'s'+beds+'b',
                   rig:R.RowhouseIso, opts:{tier,units,storeys,beds} });
    return out;
  }

  function contract(){
    const CONF=configurations(); let checks=0, failed=0; const fails=[];
    for(const c of CONF){
      let cs;
      try { cs=checkOne(c.rig.gameplayAll(c.opts)); }
      catch(e){ checks++; failed++; fails.push(c.phase+' '+c.name+' THREW '+e.message); continue; }
      for(const a of cs){ checks++; if(!a.pass){ failed++;
        fails.push(c.phase+' '+c.name+': '+a.name+(a.detail?' — '+a.detail:'')); } }
    }
    return { configurations:CONF.length, assertions:22, checks, failed, fails };
  }

  async function hashes(){
    const SE=root.SidecarExport;
    if(!SE) throw new Error('multiunit kit: Art/_sidecarExport.js must be loaded — nobody types a hash');
    const files=['Art/interiorPropRig.js','Art/_interiorPlacer.js','Art/_buildingGameplay.js',
      'Art/walkupIsoRig.js','Art/walkupUnitIsoRig.js','Art/stackFlatsIsoRig.js',
      'Art/stackUnitIsoRig.js','Art/rowhouseIsoRig.js','Art/rowhouseUnitIsoRig.js'];
    const H={};
    for(const f of files) { try { H[f]=await SE.rigSha256(f); } catch(e){ H[f]=null; } }
    return H;
  }

  // canonical stamp position: straight after `variant`, so a regenerated file diffs cleanly
  function stampFive(o, H, shell, room){
    const out={};
    for(const k of Object.keys(o)){
      out[k]=o[k];
      if(k==='variant'){
        out.derivedFromRigSha256=H[shell];
        out.interiorDerivedFromRigSha256=H[room];
        out.propsDerivedFromRigSha256=H['Art/interiorPropRig.js'];
        out.placerDerivedFromRigSha256=H['Art/_interiorPlacer.js'];
        out.writerDerivedFromRigSha256=H['Art/_buildingGameplay.js'];
      }
    }
    if(!HEXR.test(out.derivedFromRigSha256||'')) throw new Error('multiunit kit: unstamped sidecar is the defect');
    return out;
  }

  async function sidecar(phaseId, preset){
    const P=phases().find(p=>p.id===phaseId);
    if(!P) throw new Error('multiunit kit: no phase '+phaseId);
    const H=await hashes();
    return stampFive(P.rig.gameplayAll(P.rig.PRESETS[preset]), H, P.shell, P.room);
  }

  async function all(){
    const H=await hashes(), files={}, tally={};
    for(const P of phases()){
      tally[P.id]=[];
      for(const k of P.presets){
        const g=stampFive(P.rig.gameplayAll(P.rig.PRESETS[k]), H, P.shell, P.room);
        const json=JSON.stringify(g,null,1)+'\n';
        files['Art/gameplay/'+P.id+'/'+P.stem+'.'+k+'.gameplay.json']=json;
        files['export/'+P.kit+'/gameplay/'+P.stem+'.'+k+'.gameplay.json']=json;
        const ra=g.reach_audit||{};
        tally[P.id].push({ preset:k, variant:g.variant, units:g.UNITS.length,
          storeys:g.build.storeys, wd:g.building.width_m, ln:g.building.depth_m,
          slots:g.UNITS.reduce((n,u)=>n+(u.resident_slots||0),0),
          sole:g.SOLE.length, thr:g.THRESHOLD.length, stairs:g.STAIRS.length,
          inter:g.INTERACT.length, blk:g.BLOCKERS.length,
          reach:[ra.checked,ra.ok,(ra.moved||0)+(ra.flipped||0),ra.nulled].join('/'),
          kb:Math.round(json.length/1024) });
      }
    }
    return { files, tally, hashes:H, contract:contract() };
  }

  root.MultiunitKit = { all, sidecar, contract, configurations, checkOne, hashes, phases };
})(typeof globalThis!=='undefined'?globalThis:window);
