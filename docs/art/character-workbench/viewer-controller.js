(function(){
  const root=document.getElementById('hh-cast-viewer'), $=s=>root.querySelector(s);
  const E=globalThis.CastViewerEngine,H=globalThis.CharacterHeadStudy;
  const bounds=__BOUNDS__, cast=E.cast.map(k=>E.create(k));
  const beforeCast=new Map(E.cast.map(k=>[k,E.create(k,{finish:false,headStudy:globalThis.CharacterHeadPass04})]));
  const state={anim:'idle',u:0,seconds:0,angle:25,speed:1,ppm:64,playing:false,spinning:false,spinStart:0,spinElapsed:0,selected:'fisher',view:'cast',expr:'auto',talk:false,wardrobe:'original',pass:'after'};
  const W=globalThis.CharacterWardrobeProof,wardrobes=new Map(),beforeWardrobes=new Map();
  if(W){
    for(const preset of W.presets){
      const option=document.createElement('option');option.value=preset.Id;option.textContent=preset.DisplayName;
      $('[data-wardrobe]').append(option);wardrobes.set(preset.Id,W.create(cast.find(c=>c.key==='fisher'),preset.Recipe));
      beforeWardrobes.set(preset.Id,W.create(beforeCast.get('fisher'),preset.Recipe));
    }
  }
  const gallery=$('[data-gallery]'),cards=new Map();
  const names={idle:'Idle',walk:'Walk',run:'Run',hold:'Hold rod',cast:'Cast',dig:'Dig',balance:'Balance',stagger:'Stagger',bite:'Bite',strike:'Strike',reel:'Reel',land:'Land fish',castBack:'Back cast',castRelease:'Release cast',board:'Board',boardDown:'Step down',haul:'Haul',ladderDown:'Climb ladder',hauler:'Work hauler',bench:'Work bench',chop:'Chop',lift:'Lift',place:'Place',toss:'Toss',swim:'Swim',tread:'Tread water',sleep:'Sleep',drive:'Drive',reach:'Reach',astride:'Ride seated',astrideStand:'Ride standing',mountUp:'Mount',mountDown:'Dismount',mountCab:'Enter cab',mountCabDown:'Exit cab'};
  const groups={Movement:['idle','walk','run','balance','stagger'],Fishing:['hold','cast','castBack','castRelease','bite','strike','reel','land'],Work:['dig','haul','hauler','bench','chop','lift','place','toss','reach'],Boarding:['board','boardDown','ladderDown'],Water:['swim','tread'],Rest:['sleep'],Vehicles:['drive','astride','astrideStand','mountUp','mountDown','mountCab','mountCabDown']};
  for(const [group,anims] of Object.entries(groups)){const optgroup=document.createElement('optgroup');optgroup.label=group;for(const key of anims){if(!E.animations[key])continue;const opt=document.createElement('option');opt.value=key;opt.textContent=names[key]||key;optgroup.append(opt);}$('[data-clip]').append(optgroup);}
  for(const c of cast){
    const cell=document.createElement('div');cell.className='cast-person';
    const canvas=document.createElement('canvas');canvas.setAttribute('role','img');canvas.setAttribute('aria-label',c.build.label+' animated character');canvas.dataset.cast=c.key;
    const btn=document.createElement('button');btn.type='button';btn.className='btn btn-ghost';btn.textContent=c.build.label;btn.dataset.inspect=c.key;btn.addEventListener('click',()=>inspect(c.key));
    cell.append(canvas,btn);gallery.append(cell);cards.set(c.key,canvas);enableDrag(canvas,()=>inspect(c.key));
    const option=document.createElement('option');option.value=c.key;option.textContent=c.build.label;$('[data-character]').append(option);
  }
  function draw(canvas,faces,c,st,headCenter){
    const ppm=state.ppm,elev=40,bb=bounds[state.anim],p=Math.ceil(ppm*.16);
    let w,h,cx,cy;
    if(headCenter){w=h=Math.ceil(ppm*.72);cx=w/2+.5;cy=h/2;faces=faces.filter(f=>f.part==='head').map(f=>({...f,v:f.v.map(v=>v.map((x,i)=>x-headCenter[i]))}));}
    else {w=Math.ceil(bb.width*ppm)+2*p;h=Math.ceil(bb.height*ppm)+2*p;cx=w/2+.5;cy=p-bb.minY*ppm;}
    const result=raster(faces,c.mats,{w,h,cx,cy,scale:ppm,angle:state.angle,elev,mode:'proposal',outline:true,surface:(u,v)=>(c.headStudy||H).sample(u,v,st,c.headBuild),surfaceColours:c.colours});
    // Rasterize at the chosen density; pixelated CSS preserves hard edges when fitting the cell.
    canvas.width=w;canvas.height=h;canvas.getContext('2d').putImageData(new ImageData(result.pixels,w,h),0,0);
  }
  let renders=0,lastRenderMs=0;
  function render(){
    const begin=performance.now(),visible=state.view==='cast'?cast:cast.filter(c=>c.key===state.selected);
    for(const [i,base] of visible.entries()){
      const outfitMap=state.pass==='before'?beforeWardrobes:wardrobes;
      const c=base.key==='fisher'&&state.wardrobe!=='original'?outfitMap.get(state.wardrobe):state.pass==='before'?beforeCast.get(base.key):base;
      const posed=E.pose(c,state.anim,state.u);
      const options={anim:state.anim,seed:17+E.cast.indexOf(c.key)*31,talk:state.talk};if(state.expr!=='auto')options.expr=state.expr;
      const face=(c.headStudy||H).life(state.seconds,options);
      if(state.view==='cast')draw(cards.get(c.key),posed.faces,c,face);
      else {draw($('[data-body]'),posed.faces,c,face);draw($('[data-head]'),posed.faces,c,face,posed.head);}
    }
    $('[data-angle-value]').textContent=Math.round(state.angle)+'°';$('[data-angle]').value=state.angle;
    const anim=E.animations[state.anim];$('[data-frame-value]').textContent=(Math.min(anim.frames,1+Math.floor(state.u*(anim.settle?anim.frames-1:anim.frames))))+' / '+anim.frames;
    $('[data-frame]').value=Math.round(state.u*1000);lastRenderMs=performance.now()-begin;renders++;
  }
  function status(){
    const clip=E.animations[state.anim],name=state.view==='cast'?'Full cast':cast.find(c=>c.key===state.selected).build.label;
    $('[data-status]').textContent=name+' · '+(state.playing?'Playing':'Paused')+' · '+(clip.oneShot?'Single action':'Repeating animation')+(state.spinning?' · Rotating':'');
    $('[data-play]').textContent=state.playing?'Pause':'Play';$('[data-play]').setAttribute('aria-pressed',String(state.playing));
    $('[data-spin]').textContent=state.spinning?'Stop rotation':'Rotate 360°';$('[data-spin]').setAttribute('aria-pressed',String(state.spinning));
  }
  function inspect(key){state.selected=key;state.view='inspect';$('[data-character]').value=key;$('[data-view]').value='inspect';syncView();}
  function syncWardrobe(){
    $('[data-wardrobe-controls]').hidden=!W;
    $('[data-wardrobe]').disabled=state.selected!=='fisher';
    $('[data-wardrobe-fit]').hidden=!W;
    $('[data-wardrobe-fit]').textContent=state.selected==='fisher'?'Clothing study supports the Fisher body only. Other fits and in-game wardrobe are pending.':'Original cast clothing only. This body has no approved modular fit yet.';
  }
  function syncView(){gallery.hidden=state.view!=='cast';$('[data-inspector]').hidden=state.view!=='inspect';syncWardrobe();status();render();}
  let raf=0,last=0,lastDraw=0;
  function schedule(){if(!raf&&(state.playing||state.spinning)){last=0;raf=requestAnimationFrame(tick);}}
  function tick(now){raf=0;if(!state.playing&&!state.spinning)return;if(!last)last=now;const dt=Math.min((now-last)/1000,.1);last=now;
    if(state.playing){state.seconds+=dt*state.speed;const a=E.animations[state.anim],duration=a.frames*a.ms/1000;state.u+=dt*state.speed/duration;if(state.u>=1){if(a.oneShot){state.u=1;state.playing=false;status();}else state.u%=1;}}
    if(state.spinning){state.spinElapsed+=dt;state.angle=(state.spinStart+Math.min(1,state.spinElapsed/8)*360)%360;if(state.spinElapsed>=8){state.angle=state.spinStart;state.spinning=false;status();}}
    if(now-lastDraw>=(state.view==='cast'?80:42)||(!state.playing&&!state.spinning)){render();lastDraw=now;}
    if(state.playing||state.spinning)raf=requestAnimationFrame(tick);
  }
  $('[data-play]').addEventListener('click',()=>{if(!state.playing&&state.u>=1)state.u=0;state.playing=!state.playing;status();schedule();});
  $('[data-spin]').addEventListener('click',()=>{state.spinning=!state.spinning;state.spinStart=state.angle;state.spinElapsed=0;status();schedule();});
  $('[data-clip]').value=state.anim;
  $('[data-clip]').addEventListener('change',e=>{state.anim=e.target.value;state.u=0;state.seconds=0;status();render();});
  $('[data-view]').addEventListener('change',e=>{state.view=e.target.value;syncView();});
  $('[data-character]').addEventListener('change',e=>{state.selected=e.target.value;syncWardrobe();status();render();});
  $('[data-wardrobe]').addEventListener('change',e=>{state.wardrobe=e.target.value;status();render();});
  $('[data-speed]').addEventListener('change',e=>state.speed=+e.target.value);
  $('[data-density]').addEventListener('change',e=>{state.ppm=+e.target.value;render();});
  $('[data-pass]').addEventListener('change',e=>{state.pass=e.target.value;render();});
  $('[data-angle]').addEventListener('input',e=>{state.angle=+e.target.value;state.spinning=false;status();render();});
  $('[data-frame]').addEventListener('input',e=>{state.u=+e.target.value/1000;state.seconds=state.u*E.animations[state.anim].frames*E.animations[state.anim].ms/1000;state.playing=false;status();render();});
  $('[data-expression]').addEventListener('change',e=>{state.expr=e.target.value;render();});
  $('[data-talk]').addEventListener('change',e=>{state.talk=e.target.checked;render();});
  function enableDrag(canvas,onTap){let pointer=null;canvas.addEventListener('pointerdown',e=>{pointer={x:e.clientX,y:e.clientY,angle:state.angle,moved:false};canvas.setPointerCapture(e.pointerId);});
    canvas.addEventListener('pointermove',e=>{if(!pointer)return;const dx=e.clientX-pointer.x;if(Math.abs(dx)>5)pointer.moved=true;if(pointer.moved){state.angle=((pointer.angle+dx*1.2)%360+360)%360;state.spinning=false;status();render();}});
    canvas.addEventListener('pointerup',()=>{if(pointer&&!pointer.moved&&onTap)onTap();pointer=null;});canvas.addEventListener('pointercancel',()=>pointer=null);
  }
  enableDrag($('[data-body]'));enableDrag($('[data-head]'));
  document.addEventListener('visibilitychange',()=>{if(document.hidden){state.playing=false;state.spinning=false;status();}});
  // Expose read-only diagnostics for the accompanying browser checks.
  root.castDiagnostics=()=>({state:{...state},renderCount:renders,lastRenderMs,cast:cast.map(c=>({key:c.key,faces:c.faces.length,bones:c.bind.bones.length}))});
  syncView();root.dataset.ready='true';
})();
