// A SetPieceDef adapter for the delivered yard. Each module keeps its own sprite, solids and walk surface.
(function(root){
  'use strict';
  const W=root.WharfRig2,G=root.WharfGeo2,K=root.WharfRig2Kit;
  const VERSION='1.0.0',PPU=32,IMPORT_SIZE_CAP=4096,FACINGS=8,ORIGIN=198,DATUM=5.6;
  const clone=o=>JSON.parse(JSON.stringify(o)), round=n=>Math.round(n*1e6)/1e6;
  const source=K.HARBOURS.nmc_yard, layout=source.layout(), parts={};
  for(const input of layout.mods){
    const m=clone(input), at=[ORIGIN+m.at[0],m.at[1]];
    // These two argument lists are region coordinates; the other modules use their own frame.
    if(m.id==='fence'){
      at[0]=m.o.args.pts[0][0]; at[1]=(m.o.args.pts[0][1]+m.o.args.pts[1][1])/2;
      m.o.args.pts=m.o.args.pts.map(p=>[p[0]-at[0],p[1]-at[1]]);
    }
    if(m.id==='props'){
      const items=m.o.args.items;
      at[0]=(Math.min(...items.map(p=>p.x))+Math.max(...items.map(p=>p.x)))/2;
      at[1]=(Math.min(...items.map(p=>p.y))+Math.max(...items.map(p=>p.y)))/2;
      for(const p of items){p.x-=at[0];p.y-=at[1];}
    }
    m.at=[0,0]; m.o.deckZ-=DATUM;
    if(m.o.args)m.o.args.g-=DATUM;
    if(m.o.args&&m.o.args.hull&&m.o.args.hull.keelZ!=null)m.o.args.hull.keelZ-=DATUM;
    const id='structure.nmc_yard_'+m.id.replace(/[A-Z]/g,c=>'_'+c.toLowerCase()), modelKey='harbour:nmc_yard_'+m.id;
    K.define('nmc_yard_'+m.id,{label:'Wharf boat yard: '+m.id,tideRange:source.tideRange,
      bed:(x,y)=>source.bed(x+at[0]-ORIGIN,y+at[1])-DATUM,
      layout:()=>({mods:[m],gangs:[],lines:[]})});
    parts[m.id]={id,key:m.id,at,module:m,modelKey};
  }
  const KEYS=Object.keys(parts), r2=(p,a)=>[round(p[0]*Math.cos(a)-p[1]*Math.sin(a)),round(p[0]*Math.sin(a)+p[1]*Math.cos(a))];
  function data(key){
    const p=parts[key];if(!p)throw new Error('No yard part '+key);
    const model=W.model(p.modelKey,{season:'summer',stage:'weathered',tide:0}), mod=model.modules[0], rec=mod.s._sy||[];
    const pts=model.faces.flatMap(f=>f.v),bounds={};
    for(const [i,k] of ['x','y','z'].entries())bounds[k]=[round(Math.min(...pts.map(p=>p[i]))),round(Math.max(...pts.map(p=>p[i])))];
    return {p,model,mod,rec,bounds,pts};
  }
  function boxPoly(x0,y0,x1,y1){return [[x0,y0],[x1,y0],[x1,y1],[x0,y1]];}
  function wall(a,b,half){const dx=b[0]-a[0],dy=b[1]-a[1],n=Math.hypot(dx,dy),x=-dy/n*half,y=dx/n*half;return [[a[0]+x,a[1]+y],[b[0]+x,b[1]+y],[b[0]-x,b[1]-y],[a[0]-x,a[1]-y]];}
  function gameplay(key,options){
    options=options||{}; const {p,model,mod,rec,bounds,pts}=data(key),turn=q=>r2(q,mod.rot),colliders=[],walks=[],anchors={};
    const solid=pts=>colliders.push({type:'polygon',blocks:'walk',pts:pts.map(turn)});
    for(const r of rec){
      if(r.k==='block')solid(r.poly);
      if(r.k==='walk')walks.push({poly:r.poly.map(turn),z:r.z,surface:r.surface});
      if(r.k==='verb'&&r.id==='shed.door'){const xy=turn(r.stand);anchors.door=[...xy,r.stand[2]];}
      if(r.k==='wall'){
        const d=[r.b[0]-r.a[0],r.b[1]-r.a[1]],L=Math.hypot(...d),P=t=>[r.a[0]+d[0]*t/L,r.a[1]+d[1]*t/L];let start=0;
        for(const gap of [...r.gaps,[L,L]]){if(gap[0]>start)solid(wall(P(start),P(gap[0]),0.05));start=gap[1];}
      }
      if(r.k==='fenceRun'){
        const L=Math.hypot(r.b[0]-r.a[0],r.b[1]-r.a[1]),gate=rec.find(q=>q.k==='gate');
        const P=t=>[r.a[0]+(r.b[0]-r.a[0])*t/L,r.a[1]+(r.b[1]-r.a[1])*t/L];
        for(const [a,b] of gate?[[0,gate.span[0]],[gate.span[1],L]]:[[0,L]])solid(wall(P(a),P(b),0.045));
      }
    }
    if(['yardFront','pierW','pierE'].includes(key)){
      const hx=mod.s.L/2,hy=mod.s.W/2;walks.push({poly:boxPoly(-hx,-hy,hx,hy).map(turn),z:mod.s.deckZ,surface:'concrete'});
    }
    if(key==='floodW'||key==='floodE')solid(boxPoly(-0.3,-0.3,0.3,0.3));
    if(key==='shed'){
      // Furniture's floor-reaching geometry supplies its own footprint, separate from the open wall ring.
      const groups={};for(const f of model.faces){if(!['bench','shelves','compressor','cart','drum'].includes(f.part))continue;(groups[f.part]||(groups[f.part]=[])).push(...f.v);}
      for(const v of Object.values(groups))if(Math.min(...v.map(q=>q[2]))<1.2)colliders.push({type:'polygon',blocks:'walk',pts:boxPoly(Math.min(...v.map(q=>q[0])),Math.min(...v.map(q=>q[1])),Math.max(...v.map(q=>q[0])),Math.max(...v.map(q=>q[1])))});
    }
    const cells=Array.from({length:FACINGS},(_,dir)=>{
      const C=W.cam(dir,PPU),q=pts.map(p=>W.proj(p,C)),x=q.map(p=>p[0]),y=q.map(p=>p[1]);
      const x0=Math.min(...x),y0=Math.min(...y);
      return {dir,w:Math.ceil(Math.max(...x)-x0)+9,h:Math.ceil(Math.max(...y)-y0)+9,pivot:[4-Math.floor(x0),4-Math.floor(y0)]};
    });
    return {schema:'hidden-harbours/nmc-shipyard-part@1',generatedBy:'NmcShipyard '+VERSION,key,id:p.id,name:'Wharf boat yard: '+key,defType:'SetPieceDef',scene:'keyscene.nmc_wharf',
      units:'metres in the part frame, deck datum zero',bounds,layer:walks.length&&colliders.length===0?'floor':'raised',walk:walks.length?'on':'solid',mount:'ground',colliders,
      facings:Array.from({length:FACINGS},(_,dir)=>({dir,showFaces:String(dir),bearingDeg:dir*45,cameraSees:'yard part'})),cells,anchors,options,
      placement:{row:'harbour.yard',at:p.at,elevation:3.4,dir:0},walkSurfaces:walks,records:rec};
  }
  function render(key,dir,o){o=o||{};const p=parts[key],fr=W.frame(p.modelKey,{dir,ppu:PPU,season:'summer',stage:'weathered',tide:0});
    const rgba=W.view(fr,'lit',o.sky||W.REF_SKY,{outline:!!o.outline});const out={w:fr.w,h:fr.h,px:fr.px,py:fr.py,rgba};W.clearCache();return out;}
  root.NmcShipyard={VERSION,PPU,IMPORT_SIZE_CAP,FACINGS,KEYS,parts,gameplay,render,basis:()=>({se:W.SE,ce:W.CE})};
})(globalThis);
