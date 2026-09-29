/* Hidden Harbours — St Peters DOORYARDS (phase 4, 2026-09-26). Composes a whole lot for each building on the village
   plan: the fence the plan names round the lot, a gate where the front walk meets the street side, the walk itself from
   the gate to the door's approach, and the dressing that fits the household. Placement is in the building's own model
   metres (the frame HouseIso / Shopfront anchors use), so the game can drop the lot with the building's facing.
   Needs HouseIso (+ CoastalPass for the live light), Shopfront for the two shops, and YardIso.

   YardLots.LOTS[name]   the plan's lot: rig, fence, gate, walk, dressing, margins
   YardLots.plan(name)   { lot, items:[{name, opts, x, y, rot, tag}], walk:[[x,y]..], gate:{x,y}, check }
                          rot = quarter turns in the building's frame (0 or 1); check = walkability from the gate
   YardLots.compose(name, dir, o) -> { rgba, w, h, ox, oy }  a board: the building and its yard, painter-sorted */
(function (root) {
  const LOTS = {
    sage_cottage:    { rig:'house', fence:'picketPanel', gate:'picketGate', paint:'white', walk:'steppingStones', dressing:['lilac','borderBed','clothesline','birdbath'] },
    school:          { rig:'house', fence:'postRail', gate:'open', paint:'white', walk:'gravelApron', dressing:['flagpole','bench','swingSet'] },
    harbour_gable:   { rig:'house', fence:'stoneWall', gate:'stonePillar', walk:'steppingStones', dressing:['trapBench','buoyPost','anchorSet','fernBed'] },
    general_store:   { rig:'shop', fence:null, walk:'gravelApron', dressing:['farmStand','barrelPlanter','bench'] },
    post_office:     { rig:'shop', fence:null, walk:'gravelApron', dressing:['bench','flagpole','barrelPlanter'] },
    cream_saltbox:   { rig:'house', fence:'wireFence', gate:'open', walk:'steppingStones', dressing:['vegRows','compostBin','trapBench'] },
    white_gable:     { rig:'house', fence:'picketPanel', gate:'picketGate', paint:'white', walk:'steppingStones', dressing:['swingSet','sandbox','clothesline','islandBed'] },
    red_saltbox:     { rig:'house', fence:'postRail', gate:'open', paint:'white', walk:'steppingStones', dressing:['roseBush','birdbath','raspberryRow'] },
    white_farmhouse: { rig:'house', fence:'picketPanel', gate:'picketGate', paint:'white', walk:'steppingStones', dressing:['vegRows','potatoDrills','rhubarb','clothesline'] },
  };
  const M={front:4.2, side:2.6, back:2.8, walkHalf:0.75, gap:0.35};
  const inR=(q,x,y,r)=>x+r>q.x0&&x-r<q.x1&&y+r>q.y0&&y-r<q.y1;
  const hit=(a,b,g)=>a.x0<b.x1+g&&a.x1>b.x0-g&&a.y0<b.y1+g&&a.y1>b.y0-g;
  function building(name){ const L=LOTS[name], H=root.HouseIso, SF=root.Shopfront;
    if(L.rig==='house'){ const c=H.CAST[name], o=c.opts, a=H.anchors(c.facing,o);
      return { o, facing:c.facing, R:H.footprint(o), ap:a.approach.m, path:a.entryPath.map(p=>p.m), door:[a.entrance.x,a.entrance.y], show:a.show }; }
    const c=SF.CAST[name], o=c.opts, d=SF.dims(o), hw=d.Wd/2, hl=d.Ln/2, sf=o.storefront, dx=sf==='plate'?-d.Wd*0.28:sf==='narrow'?-d.Wd*0.24:0;
    return { o, facing:c.facing, R:[{id:'shop',x0:-hw-0.1,x1:hw+0.1,y0:-hl-0.1,y1:hl+2.4,walk:false}], ap:[dx,hl+3.0], path:[[dx,hl+3.0],[dx,hl+5.0]], door:[dx,hl], show:'+Y' }; }
  function plan4(name){
    const L=LOTS[name], B=building(name), YI=root.YardIso, items=[], R=B.R;
    let x0=1e9,x1=-1e9,y0=1e9,y1=-1e9; for(const q of R){ x0=Math.min(x0,q.x0); x1=Math.max(x1,q.x1); y0=Math.min(y0,q.y0); y1=Math.max(y1,q.y1); }
    // the street side is where the entry path leads; the lot runs M.front past the building that way
    const P=B.path, e=P[P.length-1], s=P.length>1?[e[0]-P[P.length-2][0], e[1]-P[P.length-2][1]]:[0,1], sn=Math.abs(s[0])>Math.abs(s[1])?[Math.sign(s[0]),0]:[0,Math.sign(s[1])||1];
    const lot={x0:x0-M.side, x1:x1+M.side, y0:y0-M.back, y1:y1+M.side};
    if(sn[1]>0) lot.y1=y1+M.front; else if(sn[1]<0) lot.y0=y0-M.front; else if(sn[0]>0) lot.x1=x1+M.front; else lot.x0=x0-M.front;
    // the gate: where the path, carried on, crosses the street edge of the lot
    const gate = sn[1]? {x:e[0], y:sn[1]>0?lot.y1:lot.y0} : {x:sn[0]>0?lot.x1:lot.x0, y:e[1]};
    const walk=[B.ap]; for(const p of P.slice(1)) walk.push(p); walk.push([gate.x,gate.y]);
    const corridor=[]; for(let i=0;i+1<walk.length;i++){ const a=walk[i], b=walk[i+1]; corridor.push({x0:Math.min(a[0],b[0])-M.walkHalf, x1:Math.max(a[0],b[0])+M.walkHalf, y0:Math.min(a[1],b[1])-M.walkHalf, y1:Math.max(a[1],b[1])+M.walkHalf}); }
    const taken=R.map(q=>({x0:q.x0-0.25,x1:q.x1+0.25,y0:q.y0-0.25,y1:q.y1+0.25})).concat(corridor);
    const put=(nm,opts,x,y,rot,tag)=>{ const f=YI.footprint(nm,opts), w=rot?f.d:f.w, d=rot?f.w:f.d; const r={x0:x-w/2,x1:x+w/2,y0:y-d/2,y1:y+d/2}; items.push({name:nm,opts,x:+x.toFixed(3),y:+y.toFixed(3),rot,tag,rect:r}); return r; };
    // FENCE: panels round the lot, the street edge open at the gate
    if(L.fence){ const fo={paint:L.paint||null, kept:0.85, len:0.5}, fw=YI.footprint(L.fence,fo).w;
      const edges=[{a:[lot.x0,lot.y0],b:[lot.x1,lot.y0]},{a:[lot.x1,lot.y0],b:[lot.x1,lot.y1]},{a:[lot.x1,lot.y1],b:[lot.x0,lot.y1]},{a:[lot.x0,lot.y1],b:[lot.x0,lot.y0]}];
      for(const E of edges){ const dx=E.b[0]-E.a[0], dy=E.b[1]-E.a[1], len=Math.hypot(dx,dy), n=Math.max(1,Math.round(len/fw)), rot=Math.abs(dx)>Math.abs(dy)?0:1;
        for(let i=0;i<n;i++){ const t=(i+0.5)/n, x=E.a[0]+dx*t, y=E.a[1]+dy*t;
          if(Math.hypot(x-gate.x,y-gate.y)<fw*0.55) continue;
          put(L.fence, Object.assign({},fo,{len:Math.max(0,Math.min(1,((len/n)/YI.footprint(L.fence,{len:0}).w-1)/0.9))}), x, y, rot, 'yard.fence'); } }
      if(L.gate==='picketGate') put('picketGate',{paint:L.paint||null,variant:1,kept:0.85},gate.x,gate.y,sn[1]?0:1,'yard.gate');
      else if(L.gate==='stonePillar') for(const k of [-1,1]) put('stonePillar',{},gate.x+(sn[1]?k*0.95:0),gate.y+(sn[1]?0:k*0.95),0,'yard.gate'); }
    // WALK: stepping stones or gravel along each leg, gate to door
    for(let i=0;i+1<walk.length;i++){ const a=walk[i], b=walk[i+1], dx=b[0]-a[0], dy=b[1]-a[1], len=Math.hypot(dx,dy); if(len<0.3) continue;
      const step=YI.footprint(L.walk,{len:0.3}).w, n=Math.max(1,Math.round(len/step)), rot=Math.abs(dx)>Math.abs(dy)?0:1;
      for(let k=0;k<n;k++){ const t=(k+0.5)/n; put(L.walk,{len:0.3,kept:0.8},a[0]+dx*t,a[1]+dy*t,rot,'yard.walk'); } }
    // DRESSING: the first free spot in the yard ring, front yard first for flowers, the back and sides for work
    const front=(q)=> sn[1]>0?q.y0>y1: sn[1]<0?q.y1<y0: sn[0]>0?q.x0>x1:q.x1<x0;
    const flowers={lilac:1,borderBed:1,birdbath:1,islandBed:1,roseBush:1,fernBed:1,flagpole:1,bench:1,barrelPlanter:1,farmStand:1};
    for(const nm of L.dressing){ const f=YI.footprint(nm,{}); let done=false;
      for(const wantFront of (flowers[nm]?[true,false]:[false,true])){ if(done) break;
        for(let yy=lot.y0+0.6; yy<lot.y1-0.6 && !done; yy+=0.4) for(let xx=lot.x0+0.6; xx<lot.x1-0.6 && !done; xx+=0.4){
          for(const rot of [0,1]){ const w=rot?f.d:f.w, d=rot?f.w:f.d, r={x0:xx-w/2,x1:xx+w/2,y0:yy-d/2,y1:yy+d/2};
            if(r.x0<lot.x0+0.35||r.x1>lot.x1-0.35||r.y0<lot.y0+0.35||r.y1>lot.y1-0.35) continue;
            if(front(r)!==wantFront) continue;
            if(taken.some(q=>hit(q,r,M.gap))||items.some(it=>it.tag==='yard.dress'&&hit(it.rect,r,M.gap))) continue;
            put(nm,{kept:0.85,paint:L.paint||null},xx,yy,rot,'yard.dress'); done=true; break; } } } }
    // walkability: from the gate, every dressing piece has a free standing point beside it
    const blk=R.filter(q=>!q.walk).concat(items.filter(it=>it.tag!=='yard.walk').map(it=>it.rect)), st=0.15, r=0.25, nx=Math.ceil((lot.x1-lot.x0)/st)+1, ny=Math.ceil((lot.y1-lot.y0)/st)+1, seen=new Uint8Array(nx*ny), Q=[];
    const free=(x,y)=>x>lot.x0-0.9&&x<lot.x1+0.9&&y>lot.y0-0.9&&y<lot.y1+0.9&&!blk.some(q=>inR(q,x,y,r)&&!(q.x1-q.x0<1.4&&q.y1-q.y0<1.4&&Math.hypot((q.x0+q.x1)/2-gate.x,(q.y0+q.y1)/2-gate.y)<0.2));
    const cell=(x,y)=>[Math.round((x-lot.x0)/st),Math.round((y-lot.y0)/st)];
    const g0=[gate.x-sn[0]*0.6, gate.y-sn[1]*0.6], [gi,gj]=cell(g0[0],g0[1]); if(gi>=0&&gj>=0&&gi<nx&&gj<ny){ seen[gj*nx+gi]=1; Q.push(gj*nx+gi); }
    for(let q=0;q<Q.length;q++){ const k=Q[q], i=k%nx, j=(k-i)/nx; for(const [a,b] of [[1,0],[-1,0],[0,1],[0,-1]]){ const ii=i+a, jj=j+b; if(ii<0||jj<0||ii>=nx||jj>=ny) continue; const kk=jj*nx+ii; if(seen[kk]) continue; if(!free(lot.x0+ii*st,lot.y0+jj*st)) continue; seen[kk]=1; Q.push(kk); } }
    const reach=(x,y)=>{ const [i,j]=cell(x,y); return i>=0&&j>=0&&i<nx&&j<ny&&!!seen[j*nx+i]; };
    const dress=items.filter(it=>it.tag==='yard.dress').map(it=>{ const q=it.rect, c=[[(q.x0+q.x1)/2,q.y0-0.45],[(q.x0+q.x1)/2,q.y1+0.45],[q.x0-0.45,(q.y0+q.y1)/2],[q.x1+0.45,(q.y0+q.y1)/2]]; return {name:it.name, reachable:c.some(p=>reach(p[0],p[1]))}; });
    const check={ door:reach(B.ap[0],B.ap[1]), dressing:dress, placed:L.dressing.filter(n=>items.some(it=>it.name===n&&it.tag==='yard.dress')).length, wanted:L.dressing.length };
    return { name, facing:B.facing, lot, gate, walk, items:items.map(({rect,...it})=>it), check, building:B };
  }
  // a board: the building and its yard, painter-sorted by depth; props face with the building's quarter turns
  function compose4(name, dir, o){ o=o||{}; const pl=plan4(name), B=pl.building, L=LOTS[name], H=root.HouseIso, SF=root.Shopfront, YI=root.YardIso, CPL=root.CoastalPass.light;
    const Rg=L.rig==='house'?H:SF, sky=o.night?CPL.NIGHT_SKY:CPL.REF_SKY, dz=dir==null?B.facing:dir, w=o.w||1400, h=o.h||1100, ox=w/2, oy=h*0.58;
    const th=dz*Math.PI/4, ct=Math.cos(th), st=Math.sin(th), se=Math.sin(40*Math.PI/180), ce=Math.cos(40*Math.PI/180), S=32;
    const scr=(x,y)=>[ox+(x*ct-y*st)*S, oy-((x*st+y*ct)*se)*S], depth=(x,y)=>x*st+y*ct;
    const out=new Uint8ClampedArray(w*h*4), g=o.night?[30,44,40]:[93,125,73]; for(let i=0;i<w*h;i++){ out[i*4]=g[0]; out[i*4+1]=g[1]; out[i*4+2]=g[2]; out[i*4+3]=255; }
    const blit=(px,W,Hh,pvx,pvy,sx,sy,shade)=>{ const bx=Math.round(sx-pvx), by=Math.round(sy-pvy); for(let y=0;y<Hh;y++){ const yy=by+y; if(yy<0||yy>=h) continue; for(let x=0;x<W;x++){ const xx=bx+x; if(xx<0||xx>=w) continue; const i=(y*W+x)*4, j=(yy*w+xx)*4;
      if(shade){ const lv=shade[y*W+x]; if(lv){ const k=[1,.86,.74,.64][lv]; out[j]*=k; out[j+1]*=k; out[j+2]*=k; } continue; }
      if(px[i+3]){ out[j]=px[i]; out[j+1]=px[i+1]; out[j+2]=px[i+2]; } } } };
    const fr=Rg.frame(dz,B.o), bpx=Rg.relight(fr,sky,o.night?{time:21}:{}), sh=Rg.castShadow(fr,sky), [hx,hy]=scr(0,0);
    blit(null,Rg.W,Rg.H,Rg.pivot.x,Rg.pivot.y,hx,hy,sh.lv);
    const draws=pl.items.map(it=>({d:depth(it.x,it.y)+(it.tag==='yard.walk'?50:0), it})); draws.push({d:depth(0,0), house:true});
    draws.sort((a,b)=>b.d-a.d);
    for(const D of draws){ if(D.house){ blit(bpx,Rg.W,Rg.H,Rg.pivot.x,Rg.pivot.y,hx,hy); continue; }
      const it=D.it, fd=(dz+(it.rot?2:0))%8, px=YI.renderLive(it.name,fd,Object.assign({},it.opts,o.night?{night:true}:{})), [sx,sy]=scr(it.x,it.y); blit(px,YI.W,YI.H,YI.pivot.x,YI.pivot.y,sx,sy); }
    return { rgba:out, w, h, plan:pl };
  }
  // ======================= PHASE 5 · OUTBUILDINGS, WATER, THE WORK YARD, ROUTES FOR ROAD KIT v4 (2026-09-26) =======================
  /* Each lot gets its outbuildings and its water. Pieces stand in the side yard the camera sees (never behind the house,
     never across its show face), their doors to the south, clear of each other's sightlines. Crops go where the sun is,
     work goes where the camera looks. Every path is a RoadKit4 route: the front walk (walk: stones or gravel), a trodden
     footpath to each outbuilding and to the water, and a rutted lane to the barn. plan(name, {outbuildings:false}) and
     compose(name, dir, {outbuildings:false}) give phase 4 exactly. */
  const OUT5 = {
    sage_cottage:    { work:'E', out:[{kind:'shed', opts:{body:'sage', siding:'clapboard', roof:'asphaltBrown', trim:'ivory', doorPaint:'red', weather:0.5}}], water:{kind:'well', opts:{roof:'asphaltBrown'}} },
    school:          { work:'W', out:[{kind:'woodshed', opts:{size:1, roof:'slate'}}], water:{kind:'pump'} },
    harbour_gable:   { work:'W', out:[{kind:'shed', opts:{body:'teal', siding:'shingle', roof:'metalRed', trim:'white', doorPaint:'ivory', weather:0.45}}], water:{kind:'pump'} },
    general_store:   { work:'E', out:[{kind:'shed', opts:{size:1, body:'yellow', siding:'clapboard', roof:'metal', trim:'white', doorPaint:'join'}}], water:null },
    post_office:     { out:[], water:null },
    cream_saltbox:   { work:'E', out:[{kind:'henHouse', opts:{body:'cream', siding:'clapboard', roof:'metalGreen', trim:'white', doorPaint:'red'}}], water:{kind:'pump'} },
    white_gable:     { work:'E', out:[{kind:'woodshed', opts:{roof:'asphaltGrey'}}], water:{kind:'well', opts:{roof:'asphaltGrey'}} },
    red_saltbox:     { work:'E', out:[{kind:'henHouse', opts:{body:'whitewash', roof:'metal', trim:'white', doorPaint:'blue'}}], water:{kind:'pump'} },
    white_farmhouse: { work:'E', out:[{kind:'barn', opts:{roof:'metal'}, drive:true}, {kind:'woodshed', opts:{roof:'slate'}}], water:{kind:'well', opts:{roof:'slate'}} },
  };
  const D5=Math.PI/180, SE5=Math.sin(40*D5), CE5=Math.cos(40*D5);
  const m2w=(v,dz)=>{ const a=dz*Math.PI/4, c=Math.cos(a), s=Math.sin(a); return [v[0]*c-v[1]*s, v[0]*s+v[1]*c]; };
  const w2m=(v,dz)=>{ const a=dz*Math.PI/4, c=Math.cos(a), s=Math.sin(a); return [v[0]*c+v[1]*s, -v[0]*s+v[1]*c]; };
  const Q4=(p,k)=>k===1?[-p[1],p[0]]:k===2?[-p[0],-p[1]]:k===3?[p[1],-p[0]]:[p[0],p[1]];
  const rotR=(q,k,t)=>{ const a=Q4([q.x0,q.y0],k), b=Q4([q.x1,q.y1],k); return {x0:Math.min(a[0],b[0])+t[0], x1:Math.max(a[0],b[0])+t[0], y0:Math.min(a[1],b[1])+t[1], y1:Math.max(a[1],b[1])+t[1]}; };
  const bboxR=(rs)=>rs.reduce((a,q)=>({x0:Math.min(a.x0,q.x0),x1:Math.max(a.x1,q.x1),y0:Math.min(a.y0,q.y0),y1:Math.max(a.y1,q.y1)}),{x0:1e9,x1:-1e9,y0:1e9,y1:-1e9});
  const growR=(q,g)=>({x0:q.x0-g,x1:q.x1+g,y0:q.y0-g,y1:q.y1+g});
  const segD=(p,a,b)=>{ const vx=b[0]-a[0], vy=b[1]-a[1], L2=vx*vx+vy*vy||1e-9, t=Math.max(0,Math.min(1,((p[0]-a[0])*vx+(p[1]-a[1])*vy)/L2)); return [Math.hypot(p[0]-a[0]-vx*t,p[1]-a[1]-vy*t),[a[0]+vx*t,a[1]+vy*t]]; };
  const polyD=(p,P)=>{ if(P.length===1) return Math.hypot(p[0]-P[0][0],p[1]-P[0][1]); let d=1e9; for(let i=0;i+1<P.length;i++) d=Math.min(d,segD(p,P[i],P[i+1])[0]); return d; };
  const nearestOn=(p,P)=>{ let best=[1e9,P[0]]; for(let i=0;i+1<P.length;i++){ const r=segD(p,P[i],P[i+1]); if(r[0]<best[0]) best=r; } return best[1].slice(); };
  const segHits=(a,b,q)=>{ const L=Math.hypot(b[0]-a[0],b[1]-a[1]), n=Math.max(2,Math.ceil(L/0.1)); for(let i=0;i<=n;i++){ const x=a[0]+(b[0]-a[0])*i/n, y=a[1]+(b[1]-a[1])*i/n; if(x>q.x0&&x<q.x1&&y>q.y0&&y<q.y1) return true; } return false; };
  const wHit=(a,b)=>a.e0<b.e1&&a.e1>b.e0&&a.n0<b.n1&&a.n1>b.n0;
  const SUNS=[[120,38],[180,62],[240,38]].map(([az,el])=>({v:[Math.sin(az*D5),Math.cos(az*D5)],t:Math.tan(el*D5)}));
  const SURF5={ stones:{label:'stepping stones',foot:'stone',grip:0.8}, gravel:{label:'gravel',foot:'gravel',grip:0.66}, trodden:{label:'trodden earth',foot:'earth',grip:0.7}, lane:{label:'dirt lane',foot:'earth',grip:0.68}, planks:{label:'plank walk',foot:'plank',grip:0.74}, shell:{label:'crushed shell',foot:'shell',grip:0.66} };
  const GARDEN={vegRows:1,potatoDrills:1,rhubarb:1,raspberryRow:1};
  const FRONT5={lilac:1,borderBed:1,birdbath:1,islandBed:1,roseBush:1,fernBed:1,flagpole:1,bench:1,barrelPlanter:1,farmStand:1};
  const DOORTAGS={woodshed:['show.cordwood'],shed:['door.leaf'],barn:['door.leaf'],henHouse:['door.leaf'],well:['yard.bucket'],pump:['yard.trough','yard.pump']};
  function plan5(name){
    const L=LOTS[name], X=OUT5[name]||{out:[],water:null}, B=building(name), YI=root.YardIso, OB=root.Outbuilding, dz=B.facing, R=B.R, items=[];
    let x0=1e9,x1=-1e9,y0=1e9,y1=-1e9; for(const q of R){ x0=Math.min(x0,q.x0); x1=Math.max(x1,q.x1); y0=Math.min(y0,q.y0); y1=Math.max(y1,q.y1); }
    const P=B.path, e=P[P.length-1], s=P.length>1?[e[0]-P[P.length-2][0], e[1]-P[P.length-2][1]]:[0,1], sn=Math.abs(s[0])>Math.abs(s[1])?[Math.sign(s[0]),0]:[0,Math.sign(s[1])||1];
    const lot={x0:x0-M.side, x1:x1+M.side, y0:y0-M.back, y1:y1+M.side};
    if(sn[1]>0) lot.y1=y1+M.front; else if(sn[1]<0) lot.y0=y0-M.front; else if(sn[0]>0) lot.x1=x1+M.front; else lot.x0=x0-M.front;
    const edgeKey=sn[1]>0?'y1':sn[1]<0?'y0':sn[0]>0?'x1':'x0';
    let ridge=8; if(L.rig==='house'){ const H=root.HouseIso, c=H.CAST[name], a=H.anchors(c.facing,c.opts); ridge=(H.pivot.y-a.ridge.y)/(CE5*32); }
    const wbox=(q)=>{ const c=[[q.x0,q.y0],[q.x1,q.y0],[q.x0,q.y1],[q.x1,q.y1]].map(p=>m2w(p,dz)); return {e0:Math.min(...c.map(p=>p[0])),e1:Math.max(...c.map(p=>p[0])),n0:Math.min(...c.map(p=>p[1])),n1:Math.max(...c.map(p=>p[1]))}; };
    const HW=wbox({x0,x1,y0,y1}), hc=[(HW.e0+HW.e1)/2,(HW.n0+HW.n1)/2], nT=hc[1]+0.15*(HW.n1-HW.n0);
    const sideOf=(v)=>Math.abs(v[0])>Math.abs(v[1])?(v[0]>0?'E':'W'):(v[1]>0?'N':'S'), street=sideOf(m2w(sn,dz));
    let work=X.work||'E'; if(work===street) work=work==='E'?'W':'E'; const workSign=work==='E'?1:work==='W'?-1:0;
    const dimsOf=(q)=>OB.dims(q.kind,q.opts||{}), wmax=Math.max(0,...X.out.map(q=>{ const d=dimsOf(q); return Math.max(d.w,d.d); }));
    const gw={E:1.4,W:1.4,N:1.6,S:1.4}; gw[work]+=wmax+2.4; gw[street]=0;
    const region=Object.assign({},lot); for(const k of ['E','W','N','S']){ const v=w2m(k==='E'?[1,0]:k==='W'?[-1,0]:k==='N'?[0,1]:[0,-1],dz), g=gw[k];
      if(Math.abs(v[0])>0.5){ if(v[0]>0) region.x1+=g; else region.x0-=g; } else { if(v[1]>0) region.y1+=g; else region.y0-=g; } }
    const gate=sn[1]?{x:e[0], y:sn[1]>0?lot.y1:lot.y0}:{x:sn[0]>0?lot.x1:lot.x0, y:e[1]};
    const walk=[B.ap]; for(const p of P.slice(1)) walk.push(p); walk.push([gate.x,gate.y]);
    const corridor=[]; for(let i=0;i+1<walk.length;i++){ const a=walk[i], b=walk[i+1]; corridor.push({x0:Math.min(a[0],b[0])-M.walkHalf, x1:Math.max(a[0],b[0])+M.walkHalf, y0:Math.min(a[1],b[1])-M.walkHalf, y1:Math.max(a[1],b[1])+M.walkHalf}); }
    const hard=[...R.map(q=>growR(q,q.walk?0.45:1.0)), ...corridor, {x0:gate.x-1.3,x1:gate.x+1.3,y0:gate.y-1.3,y1:gate.y+1.3}], placed=[];
    // OUTBUILDINGS AND WATER: largest first, each at the best-scoring spot in the grown yard, door to the south
    const pieces=[...X.out.map((q,i)=>Object.assign({role:'out',i},q)), ...(X.water?[Object.assign({role:'water',i:0},X.water)]:[])];
    pieces.sort((a,b)=>((b.role==='out')-(a.role==='out'))||(dimsOf(b).w*dimsOf(b).d-dimsOf(a).w*dimsOf(a).d));
    for(const pc of pieces){ const op=Object.assign({},pc.opts||{}), fp=OB.footprint(pc.kind,op), st=OB.stations(pc.kind,op), ap0=st.approach, door=OB.layout(pc.kind,op).door;
      const rots=[0,1,2,3].filter(k=>m2w([0,1],(dz+2*k)%8)[1]<-0.3), half=Math.min(door.width,2.2)/2+0.3; let best=null;
      for(const k of rots){ const fr=fp.map(q=>rotR(q,k,[0,0])), bb0=bboxR(fr), apk=Q4(ap0,k), dk=Q4([door.x,door.y],k), nk=Q4([0,1],k), ek=nk[1]>0.5?'y1':nk[1]<-0.5?'y0':nk[0]>0.5?'x1':'x0';
        for(let yy=region.y0; yy<=region.y1; yy+=0.25) for(let xx=region.x0; xx<=region.x1; xx+=0.25){
          const bb={x0:bb0.x0+xx,x1:bb0.x1+xx,y0:bb0.y0+yy,y1:bb0.y1+yy}, ap=[apk[0]+xx,apk[1]+yy];
          if(bb.x0<region.x0+0.5||bb.x1>region.x1-0.5||bb.y0<region.y0+0.5||bb.y1>region.y1-0.5) continue;
          if(ap[0]<region.x0+0.4||ap[0]>region.x1-0.4||ap[1]<region.y0+0.4||ap[1]>region.y1-0.4) continue;
          if(hard.some(q=>hit(q,bb,0))||placed.some(p=>hit(growR(p.bb,1.0),bb,0))) continue;
          if(hard.some(q=>inR(q,ap[0],ap[1],0.3))||placed.some(p=>inR(p.bb,ap[0],ap[1],0.5))) continue;
          let lane=null; if(pc.drive){ const E_=region[ek]; lane=ek[0]==='y'?{x0:ap[0]-1.5,x1:ap[0]+1.5,y0:Math.min(ap[1],E_),y1:Math.max(ap[1],E_)}:{x0:Math.min(ap[0],E_),x1:Math.max(ap[0],E_),y0:ap[1]-1.5,y1:ap[1]+1.5};
            if(R.some(q=>hit(growR(q,0.3),lane,0))) continue; }
          const wb=wbox(bb), dW=m2w([dk[0]+xx,dk[1]+yy],dz), sight={e0:dW[0]-half,e1:dW[0]+half,n0:dW[1]-3.2,n1:dW[1]-0.2};
          const ov=Math.max(0,Math.min(wb.e1,HW.e1)-Math.max(wb.e0,HW.e0))/Math.max(0.1,wb.e1-wb.e0); let sc=0;
          if(ov>0.02) sc+= wb.n0>HW.n0-0.3 ? 70*ov : 26+30*ov;                                      // behind the house, or across its show face
          sc+=0.5*Math.abs((wb.n0+wb.n1)/2-nT);
          const ec=(wb.e0+wb.e1)/2-hc[0]; if(workSign&&Math.sign(ec)!==workSign) sc+=pc.role==='water'?3:14;
          sc+=(pc.role==='water'?1.3:pc.kind==='barn'?0.15:pc.kind==='woodshed'?0.6:0.35)*polyD(ap,walk);
          if(placed.some(p=>wHit(p.sight,wb)||wHit(sight,p.wb))) sc+=25;                          // no piece in another's doorway view
          if(pc.drive) sc+=0.3*Math.abs((ek[0]==='y'?ap[1]:ap[0])-region[ek]);
          if(!best||sc<best.sc-1e-9) best={sc,x:xx,y:yy,k,bb,ap,wb,sight,lane,ek,door:[dk[0]+xx,dk[1]+yy],R:fr.map((q,i)=>Object.assign({walk:!!fp[i].walk},{x0:q.x0+xx,x1:q.x1+xx,y0:q.y0+yy,y1:q.y1+yy}))};
        } }
      if(!best) continue;
      const it={name:pc.kind, id:pc.role==='water'?pc.kind:pc.kind+(pc.i?'_'+pc.i:''), opts:op, x:+best.x.toFixed(3), y:+best.y.toFixed(3), rot:best.k, facing:(dz+2*best.k)%8, tag:pc.role==='water'?'yard.water':'yard.outbuilding', ob:true, role:pc.role, drive:!!pc.drive,
        approach:best.ap.map(v=>+v.toFixed(3)), door:best.door.map(v=>+v.toFixed(3)), rect:best.bb, rects:best.R.filter(q=>!q.walk).map(({walk,...q})=>q), dims:dimsOf(pc)};
      items.push(it); placed.push({bb:best.bb, wb:best.wb, sight:best.sight, it}); if(best.lane){ it.driveEdge=best.ek; hard.push(growR(best.lane,0.1)); } }
    // the lot grows to hold them (the street edge stays where the gate is)
    const lot5=Object.assign({},lot); for(const p of placed){ const g=growR(p.bb,0.9), a=p.it.approach; lot5.x0=Math.min(lot5.x0,g.x0,a[0]-0.6); lot5.x1=Math.max(lot5.x1,g.x1,a[0]+0.6); lot5.y0=Math.min(lot5.y0,g.y0,a[1]-0.6); lot5.y1=Math.max(lot5.y1,g.y1,a[1]+0.6); }
    lot5[edgeKey]=lot[edgeKey];
    // ROUTES (RoadKit4): the front walk, a trodden footpath to each piece, a lane to the barn
    const routes=[{id:name+'.walk', cls:'walk', surf:L.walk==='gravelApron'?'gravel':'stones', w:L.walk==='gravelApron'?1.8:0.9, pts:walk.map(p=>p.slice())}], drives=[];
    for(const it of items){ const a=it.approach;
      if(it.drive){ const ek=it.driveEdge, edge=ek[0]==='y'?[a[0],lot5[ek]]:[lot5[ek],a[1]]; routes.push({id:name+'.'+it.id+'_drive', cls:'lane', pts:[edge,a,it.door], to:it.id}); drives.push(edge); continue; }
      const n=nearestOn(a,walk), blocks=R.filter(q=>!q.walk).map(q=>growR(q,0.35)).concat(placed.filter(p=>p.it!==it).map(p=>growR(p.bb,0.2)));
      let pts=[n,a,it.door];
      if(blocks.some(q=>segHits(n,a,q))){ const c1=[a[0],n[1]], c2=[n[0],a[1]], ok=(c)=>!blocks.some(q=>segHits(n,c,q)||segHits(c,a,q)); if(ok(c1)) pts=[n,c1,a,it.door]; else if(ok(c2)) pts=[n,c2,a,it.door]; }
      if(Math.hypot(n[0]-a[0],n[1]-a[1])<0.4) pts=[a,it.door];
      routes.push({id:name+'.'+it.id+'_path', cls:'footpath', w:1.0, pts, to:it.id}); }
    // FENCE round the grown lot, open at the gate and at the drive
    const put=(nm,opts,x,y,rot,tag)=>{ const f=YI.footprint(nm,opts), w=rot?f.d:f.w, d=rot?f.w:f.d, r={x0:x-w/2,x1:x+w/2,y0:y-d/2,y1:y+d/2}; items.push({name:nm,opts,x:+x.toFixed(3),y:+y.toFixed(3),rot,tag,rect:r}); return r; };
    if(L.fence){ const fo={paint:L.paint||null, kept:0.85, len:0.5}, fw=YI.footprint(L.fence,fo).w, gaps=[{x:gate.x,y:gate.y,r:fw*0.55}].concat(drives.map(d=>({x:d[0],y:d[1],r:1.75})));
      const edges=[{a:[lot5.x0,lot5.y0],b:[lot5.x1,lot5.y0]},{a:[lot5.x1,lot5.y0],b:[lot5.x1,lot5.y1]},{a:[lot5.x1,lot5.y1],b:[lot5.x0,lot5.y1]},{a:[lot5.x0,lot5.y1],b:[lot5.x0,lot5.y0]}];
      for(const E of edges){ const dx=E.b[0]-E.a[0], dy=E.b[1]-E.a[1], len=Math.hypot(dx,dy), n=Math.max(1,Math.round(len/fw)), rot=Math.abs(dx)>Math.abs(dy)?0:1;
        for(let i=0;i<n;i++){ const t=(i+0.5)/n, x=E.a[0]+dx*t, y=E.a[1]+dy*t; if(gaps.some(g=>Math.hypot(x-g.x,y-g.y)<g.r)) continue;
          put(L.fence, Object.assign({},fo,{len:Math.max(0,Math.min(1,((len/n)/YI.footprint(L.fence,{len:0}).w-1)/0.9))}), x, y, rot, 'yard.fence'); } }
      if(L.gate==='picketGate') put('picketGate',{paint:L.paint||null,variant:1,kept:0.85},gate.x,gate.y,sn[1]?0:1,'yard.gate');
      else if(L.gate==='stonePillar') for(const k of [-1,1]) put('stonePillar',{},gate.x+(sn[1]?k*0.95:0),gate.y+(sn[1]?0:k*0.95),0,'yard.gate'); }
    // DRESSING: flowers to the front, crops where the sun is, work where the camera sees it
    const legs=(pts,hw)=>{ const o=[]; for(let i=0;i+1<pts.length;i++){ const a=pts[i], b=pts[i+1]; o.push({x0:Math.min(a[0],b[0])-hw,x1:Math.max(a[0],b[0])+hw,y0:Math.min(a[1],b[1])-hw,y1:Math.max(a[1],b[1])+hw}); } return o; };
    const taken=[...R.map(q=>growR(q,0.25)), ...corridor, ...routes.slice(1).flatMap(r=>legs(r.pts,r.cls==='lane'?1.6:0.7)), ...placed.map(p=>growR(p.bb,0.35)), ...items.filter(q=>q.ob).map(q=>({x0:q.approach[0]-0.7,x1:q.approach[0]+0.7,y0:q.approach[1]-0.7,y1:q.approach[1]+0.7}))];
    const occ=[...R.filter(q=>!q.walk).map(q=>Object.assign({h:ridge*0.9},q)), ...placed.map(p=>Object.assign({h:p.it.dims.h*0.85},p.bb))];
    const shade=(r)=>{ let n=0,sh=0; const pts=[[(r.x0+r.x1)/2,(r.y0+r.y1)/2],[r.x0+0.1,r.y0+0.1],[r.x1-0.1,r.y0+0.1],[r.x0+0.1,r.y1-0.1],[r.x1-0.1,r.y1-0.1]];
      for(const S of SUNS){ const d=w2m(S.v,dz); for(const p of pts){ n++; for(let t=0.3;t<16;t+=0.3){ const x=p[0]+d[0]*t, y=p[1]+d[1]*t, z=t*S.t; if(z>ridge) break; if(occ.some(o=>o.h>z&&x>o.x0&&x<o.x1&&y>o.y0&&y<o.y1)){ sh++; break; } } } } return sh/n; };
    const front=(q)=> sn[1]>0?q.y0>y1: sn[1]<0?q.y1<y0: sn[0]>0?q.x0>x1:q.x1<x0;
    const sunlit={};
    for(const nm of L.dressing){ const f=YI.footprint(nm,{}), kind=GARDEN[nm]?'garden':FRONT5[nm]?'front':'work'; let best=null;
      for(let yy=lot5.y0+0.6; yy<lot5.y1-0.6; yy+=0.4) for(let xx=lot5.x0+0.6; xx<lot5.x1-0.6; xx+=0.4) for(const rot of [0,1]){
        const w=rot?f.d:f.w, d=rot?f.w:f.d, r={x0:xx-w/2,x1:xx+w/2,y0:yy-d/2,y1:yy+d/2};
        if(r.x0<lot5.x0+0.35||r.x1>lot5.x1-0.35||r.y0<lot5.y0+0.35||r.y1>lot5.y1-0.35) continue;
        if(taken.some(q=>hit(q,r,M.gap))||items.some(it=>it.tag==='yard.dress'&&hit(it.rect,r,M.gap))) continue;
        const wb=wbox(r), ov=Math.max(0,Math.min(wb.e1,HW.e1)-Math.max(wb.e0,HW.e0))/Math.max(0.1,wb.e1-wb.e0), behind=wb.n0>HW.n0-0.3, fr_=front(r); let sc=0;
        if(ov>0.02) sc+=behind?40*ov:(kind==='front'?0:18*ov);
        if(placed.some(p=>wHit(p.sight,wb))) sc+=20;
        if(kind==='front'){ if(!fr_) sc+=20; sc+=0.4*Math.abs(polyD([xx,yy],walk)-1.6); }
        else if(kind==='garden'){ sc+=16*shade(r); if(fr_) sc+=8; }
        else { if(fr_) sc+=12; sc+=0.25*Math.abs((wb.n0+wb.n1)/2-nT); }
        for(const it of items) if(it.tag==='yard.dress'){ const dd=Math.hypot(it.x-xx,it.y-yy); if(dd<2.6) sc+=(2.6-dd)*2; }
        if(!best||sc<best.sc-1e-9) best={sc,xx,yy,rot,r};
      }
      if(best){ items.push({name:nm, opts:{kept:0.85,paint:L.paint||null}, x:+best.xx.toFixed(3), y:+best.yy.toFixed(3), rot:best.rot, tag:'yard.dress', rect:best.r}); if(kind==='garden') sunlit[nm]=+(1-shade(best.r)).toFixed(2); } }
    // CHECK: from the gate, the door, every outbuilding's approach and every piece's side
    const blk=R.filter(q=>!q.walk).concat(items.flatMap(it=>it.rects||[it.rect])), st=0.15, rr=0.25, nx=Math.ceil((lot5.x1-lot5.x0+1.8)/st)+1, ny=Math.ceil((lot5.y1-lot5.y0+1.8)/st)+1, seen=new Uint8Array(nx*ny), Qb=[];
    const bx0=lot5.x0-0.9, by0=lot5.y0-0.9, free=(x,y)=>!blk.some(q=>inR(q,x,y,rr)&&!(q.x1-q.x0<1.4&&q.y1-q.y0<1.4&&Math.hypot((q.x0+q.x1)/2-gate.x,(q.y0+q.y1)/2-gate.y)<0.2)), cell=(x,y)=>[Math.round((x-bx0)/st),Math.round((y-by0)/st)];
    const g0=[gate.x-sn[0]*0.6, gate.y-sn[1]*0.6], [gi,gj]=cell(g0[0],g0[1]); if(gi>=0&&gj>=0&&gi<nx&&gj<ny){ seen[gj*nx+gi]=1; Qb.push(gj*nx+gi); }
    for(let q=0;q<Qb.length;q++){ const k=Qb[q], i=k%nx, j=(k-i)/nx; for(const [a,b] of [[1,0],[-1,0],[0,1],[0,-1]]){ const ii=i+a, jj=j+b; if(ii<0||jj<0||ii>=nx||jj>=ny) continue; const kk=jj*nx+ii; if(seen[kk]) continue; if(!free(bx0+ii*st,by0+jj*st)) continue; seen[kk]=1; Qb.push(kk); } }
    const reach=(x,y)=>{ const [i,j]=cell(x,y); for(let dj=-1;dj<=1;dj++) for(let di=-1;di<=1;di++){ const ii=i+di, jj=j+dj; if(ii>=0&&jj>=0&&ii<nx&&jj<ny&&seen[jj*nx+ii]) return true; } return false; };
    const sideReach=(q)=>[[(q.x0+q.x1)/2,q.y0-0.45],[(q.x0+q.x1)/2,q.y1+0.45],[q.x0-0.45,(q.y0+q.y1)/2],[q.x1+0.45,(q.y0+q.y1)/2]].some(p=>reach(p[0],p[1]));
    const obCheck=items.filter(q=>q.ob).map(q=>{ const S=OB.stations(q.name,q.opts); return {id:q.id, kind:q.name, facing:q.facing, doorFaces:OB.placement(q.name,q.opts).facings.find(f=>f.dir===q.facing)?.doorFaces||null, approach:reach(q.approach[0],q.approach[1]),
      stations:S.stations.map(s2=>({id:s2.id, clip:s2.clip, reachable:!!s2.reachable, fits:s2.fits!==false}))}; });
    const dress=items.filter(it=>it.tag==='yard.dress').map(it=>({name:it.name, reachable:sideReach(it.rect)}));
    const check={ door:reach(B.ap[0],B.ap[1]), outbuildings:obCheck, dressing:dress, placed:L.dressing.filter(n=>items.some(it=>it.name===n&&it.tag==='yard.dress')).length, wanted:L.dressing.length, sunlit,
      pieces:{placed:items.filter(q=>q.ob).length, wanted:pieces.length} };
    for(const r of routes){ r.pts=r.pts.map(p=>[+p[0].toFixed(3),+p[1].toFixed(3)]); r.world=r.pts.map(p=>{ const w=m2w(p,dz); return [+w[0].toFixed(3),+w[1].toFixed(3)]; }); r.scenePx=r.world.map(p=>[+(p[0]*32).toFixed(1),+(-p[1]*32).toFixed(1)]);
      const sf=r.surf||(r.cls==='footpath'?'trodden':r.cls==='lane'?'lane':'shell'), T=(root.RoadKit4&&root.RoadKit4.SURF&&root.RoadKit4.SURF[sf])||SURF5[sf]||{}; r.surface=sf; r.foot=T.foot||null; r.grip=T.grip!=null?T.grip:null; }
    return { name, facing:B.facing, work, lot:lot5, lot4:lot, gate, walk, routes, items:items.map(({rect,...it})=>it), check, building:B, ridge:+ridge.toFixed(2) };
  }
  /* the pass-9 floor with the routes painted in by RoadKit4, lit by TerrainLight5 under the same sky (plan px, 32 px = 1 m) */
  function v4floor(routes, w, h, sky, seed){ const K=root.PxKit, K8=root.PxKit8, TL=root.TerrainLight5, R4=root.RoadKit4, N=w*h;
    const net=R4.network(routes,{w,h,seed}), Z=['grass','dirt'], zi={grass:0,dirt:1}, zone=new Uint8Array(N); R4.zones(net,zone,zi);
    const seen=[0,0]; for(let i=0;i<N;i++) seen[zone[i]]=1;
    const defs=Z.filter((k,i)=>seen[i]&&K.MATS[k]).map(k=>({key:k,step:1,seed:(K.MATS[k].seed||1)+seed*13,R:(x,y)=>x>=0&&y>=0&&x<w&&y<h&&zone[y*w+x]===zi[k]}));
    const fl=K8.floor(w,h,defs,seed+5); R4.paint(fl.tile,net);
    const G=TL.gbuf(fl.tile,{relief:1.2*K.DETAIL,wrap:false,far:(y)=>Math.max(0,Math.min(1,1-y/h))}); R4.patchG(G,net); R4.faces(net,fl.tile,G);
    const lv=new Uint8Array(N), lit=TL.relight(G,sky,{frame:0,lv}); R4.weather(G,net,sky,lit,{lv,frame:0}); const flit=R4.relightFaces(net,sky,{lv,frame:0}); return R4.composite(net,lit,flit); }
  /* a board: every sprite z-composited by its own depth buffer (the building, outbuildings, water, fence, dressing), on the
     v4 floor when the pass-9 kit, WeatherSky and RoadKit4 are loaded (else flat grass with the routes drawn flat).
     o.time (14 day, 21 with o.night), o.floor:false, o.pad. Returns visibility: the unhidden share of each door. */
  function compose5(name, dir, o){ o=o||{}; const t0=Date.now(), pl=plan5(name), B=pl.building, L=LOTS[name], H=root.HouseIso, SF=root.Shopfront, YI=root.YardIso, OB=root.Outbuilding, CPL=root.CoastalPass.light;
    const Rg=L.rig==='house'?H:SF, dz=dir==null?B.facing:dir, time=o.time!=null?+o.time:(o.night?21:14), sky=o.sky||CPL.skyOf({time,cloud:o.cloud!=null?o.cloud:0.15});
    const th=dz*Math.PI/4, ct=Math.cos(th), st=Math.sin(th), S=32, Ew=(x,y)=>x*ct-y*st, Nw=(x,y)=>x*st+y*ct, sp=[];
    const push=(rig,fr,px,sh,x,y,meta)=>{ const gb=fr.gb, W=fr.W, Hh=fr.H; let a0=1e9,a1=-1e9,b0=1e9,b1=-1e9;
      for(let yy=0;yy<Hh;yy++){ const r=yy*W; for(let xx=0;xx<W;xx++) if(gb.a[r+xx]){ if(xx<a0)a0=xx; if(xx>a1)a1=xx; if(yy<b0)b0=yy; if(yy>b1)b1=yy; } }
      if(a0<=a1) sp.push({fr,px,sh,E:Ew(x,y),N:Nw(x,y),pv:rig.pivot,W,H:Hh,bb:[a0,a1,b0,b1],meta}); };
    { const fr=Rg.frame(dz,B.o), px=Rg.relight(fr,sky,{time}), sh=Rg.castShadow(fr,sky); push(Rg,fr,px,sh,0,0,{id:'building',tags:['door.leaf']}); }
    for(const it of pl.items){
      if(it.ob){ const fd=(dz+2*it.rot)%8, op=Object.assign({},it.opts,{time}), fr=OB.frame(it.name,fd,op), px=OB.relight(fr,sky,{time}), sh=OB.castShadow(fr,sky); push(OB,fr,px,sh,it.x,it.y,{id:it.id,kind:it.name,tags:DOORTAGS[it.name]}); continue; }
      const fd=(dz+(it.rot?2:0))%8, fr=YI.frame(it.name,fd,it.opts||{}), px=CPL.relight(fr,sky,{}), sh=CPL.castShadow(fr,sky); push(YI,fr,px,sh,it.x,it.y,{id:it.name}); }
    let X0=1e9,X1=-1e9,Y0=1e9,Y1=-1e9; const sx=(E)=>E*S, sy=(N)=>-N*SE5*S;
    for(const q of sp){ const a=sx(q.E)-q.pv.x, b=sy(q.N)-q.pv.y; X0=Math.min(X0,a+q.bb[0]); X1=Math.max(X1,a+q.bb[1]); Y0=Math.min(Y0,b+q.bb[2]); Y1=Math.max(Y1,b+q.bb[3]); }
    const lt=pl.lot; for(const [x,y] of [[lt.x0,lt.y0],[lt.x1,lt.y0],[lt.x0,lt.y1],[lt.x1,lt.y1]]){ X0=Math.min(X0,sx(Ew(x,y))); X1=Math.max(X1,sx(Ew(x,y))); Y0=Math.min(Y0,sy(Nw(x,y))); Y1=Math.max(Y1,sy(Nw(x,y))); }
    const pad=o.pad!=null?o.pad:28, ox=Math.round(pad-X0), oy=Math.round(pad-Y0), w=Math.round(X1-X0+2*pad), h=Math.round(Y1-Y0+2*pad), N=w*h, out=new Uint8ClampedArray(N*4);
    const Emin=-ox/S, Emax=(w-ox)/S, Nmax=oy/(SE5*S), Nmin=(oy-h)/(SE5*S), gr=o.night?[30,44,40]:[93,125,73];
    let floor='flat'; const v4=o.floor!==false&&root.RoadKit4&&root.TerrainLight5&&root.PxKit8&&root.WeatherSky;
    if(v4){ const PW=Math.ceil((Emax-Emin)*32)+2, PH=Math.ceil((Nmax-Nmin)*32)+2, toP=(p)=>[+((p[0]-Emin)*32).toFixed(2),+((Nmax-p[1])*32).toFixed(2)];
      const rts=pl.routes.map(r=>{ const q={id:r.id,cls:r.cls,pts:r.pts.map(p=>toP(m2w(p,dz)))}; if(r.surf) q.surf=r.surf; if(r.w) q.w=r.w; return q; });
      const fl=v4floor(rts,PW,PH,sky,61+name.length*7);
      for(let by=0;by<h;by++) for(let bx=0;bx<w;bx++){ const E=(bx+0.5-ox)/S, Nn=(oy-by-0.5)/(SE5*S), px_=Math.floor((E-Emin)*32), py_=Math.floor((Nmax-Nn)*32), j=(by*w+bx)*4;
        if(px_<0||py_<0||px_>=PW||py_>=PH){ out[j]=gr[0]; out[j+1]=gr[1]; out[j+2]=gr[2]; out[j+3]=255; continue; } const k=(py_*PW+px_)*4; out[j]=fl[k]; out[j+1]=fl[k+1]; out[j+2]=fl[k+2]; out[j+3]=255; }
      floor='v4'; }
    else { const COL={footpath:[112,86,58],lane:[104,82,56],gravel:[122,118,104],stones:[128,130,122]};
      for(let by=0;by<h;by++) for(let bx=0;bx<w;bx++){ const E=(bx+0.5-ox)/S, Nn=(oy-by-0.5)/(SE5*S), j=(by*w+bx)*4; let c=gr;
        for(const r of pl.routes){ const d=polyD([E,Nn],r.world), hw=(r.w||(r.cls==='lane'?2.6:1.0))/2;
          if(r.cls==='lane'){ if(Math.abs(d-0.66)<0.2) c=COL.lane; } else if(d<hw){ c=r.cls==='footpath'?COL.footpath:COL[r.surf]||COL.gravel; } }
        out[j]=c[0]; out[j+1]=c[1]; out[j+2]=c[2]; out[j+3]=255; } }
    const lvB=new Uint8Array(N), at=(q)=>[Math.round(ox+q.E*S-q.pv.x), Math.round(oy-q.N*SE5*S-q.pv.y)];
    for(const q of sp){ const [ax,ay]=at(q), Lv=q.sh.lv, W=q.W; for(let yy=0;yy<q.H;yy++){ const Y=ay+yy; if(Y<0||Y>=h) continue; for(let xx=0;xx<W;xx++){ const l=Lv[yy*W+xx]; if(!l) continue; const Xx=ax+xx; if(Xx<0||Xx>=w) continue; const i=Y*w+Xx; if(l>lvB[i]) lvB[i]=l; } } }
    for(let i=0;i<N;i++) if(lvB[i]){ const k=[1,.86,.74,.64][lvB[i]]; out[i*4]*=k; out[i*4+1]*=k; out[i*4+2]*=k; }
    const zb=new Float32Array(N).fill(1e9), own=new Int16Array(N).fill(-1);
    sp.forEach((q,si)=>{ const [ax,ay]=at(q), gb=q.fr.gb, W=q.W, dN=q.N*CE5, px=q.px; for(let yy=q.bb[2];yy<=q.bb[3];yy++){ const Y=ay+yy; if(Y<0||Y>=h) continue; for(let xx=q.bb[0];xx<=q.bb[1];xx++){ const i=yy*W+xx; if(!gb.a[i]||!px[i*4+3]) continue; const Xx=ax+xx; if(Xx<0||Xx>=w) continue;
      const j=Y*w+Xx, d=gb.d[i]+dN; if(d>=zb[j]) continue; zb[j]=d; own[j]=si; out[j*4]=px[i*4]; out[j*4+1]=px[i*4+1]; out[j*4+2]=px[i*4+2]; } } });
    const visibility={}; sp.forEach((q,si)=>{ const tg=q.meta.tags; if(!tg) return; const T=q.fr.mdl.TAGS, want=new Set(tg.map(t=>T.indexOf(t)).filter(i=>i>0)); if(!want.size) return;
      const [ax,ay]=at(q), gb=q.fr.gb, W=q.W; let tot=0, vis=0; for(let yy=q.bb[2];yy<=q.bb[3];yy++) for(let xx=q.bb[0];xx<=q.bb[1];xx++){ const i=yy*W+xx; if(!gb.a[i]||!want.has(gb.tg[i])) continue; const Xx=ax+xx, Y=ay+yy; if(Xx<0||Y<0||Xx>=w||Y>=h) continue; tot++; if(own[Y*w+Xx]===si) vis++; }
      visibility[q.meta.id]=tot?+(vis/tot).toFixed(3):null; });
    return { rgba:out, w, h, ox, oy, plan:pl, visibility, floor, sprites:sp.length, ms:Date.now()-t0 };
  }
  const plan=(name,o)=>(o&&o.outbuildings===false)?plan4(name):plan5(name);
  const compose=(name,dir,o)=>(o&&o.outbuildings===false)?compose4(name,dir,o):compose5(name,dir,o);
  root.YardLots = { LOTS, OUT5, plan, compose, plan4, compose4, routes:(name)=>plan5(name).routes };
})(typeof globalThis!=='undefined'?globalThis:window);
