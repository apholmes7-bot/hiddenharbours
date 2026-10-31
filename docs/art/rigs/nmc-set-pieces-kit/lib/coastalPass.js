/* Coastal heritage pass. Pure rig geometry/materials; no canvas, DOM, or random state.
   Load after PropIso and before the design renders. Existing rigs work without this file.
   enabled=false disables this treatment; host structural stair fixes remain. Dimensions are metres.
   Furniture is a proposed cottage layout; approach/collider data is published separately. */
(function(root){
  const PALETTE={
    slate:['#14232c','#21323b','#2d414b','#3b505b','#4b626c','#607782'],
    stone:['#403f36','#56554a','#6c6b5c','#83816e','#9c9880','#b5af93'],
    join:['#273b36','#344c44','#486153','#617761','#7b8e73','#9eaa8b'],
    ivory:['#575747','#6e6b55','#89846a','#a7a082','#c3b997','#dbceaa'],
    oak:['#30271d','#463727','#604b33','#7a6141','#927850','#b2996d'],
    brass:['#40341e','#5e4c29','#7d6837','#a08a4e','#bca565','#d6c48b'],
    iron:['#19262b','#26383d','#3a4d50','#516566','#687d79','#899790'],
    warm:['#83522b','#b4793c','#d5a156','#ebc87b','#f4dfa4','#fff0c0'],
    leaf:['#213b33','#304b3b','#405f47','#547754','#6c8b60','#829969'],
    clay:['#483b2d','#62503a','#7e6748','#9d815c','#b69a70','#cbb58d'],
    green:['#243d38','#334f47','#466659','#5c7b67','#77957b','#93ad91'],
    red:['#422a2c','#5b3637','#754547','#915a59','#ab7570','#c28f83'],
    blue:['#2b4148','#3b5660','#4f6e79','#69858b','#85a0a0','#a4b9b1'],
    linen:['#5b5846','#747059','#90886c','#aca286','#c5b99a','#ddd0b3'],
    tile:['#4a524d','#5d675d','#737c6c','#8b917d','#a5a993','#bdbea5'],
    // camera-first pass: the same slate a value lighter, so a lit slope reads as slate rather than a dark mass
    slateLit:['#1a2931','#26383f','#33474f','#435a62','#566f76','#6f878c'],
    // phase 2b: the live look's other roofs
    asph:['#1f2226','#2a2e33','#373c42','#464c53','#575e66','#6a727a'],
    cedar:['#3b3029','#4d3f34','#615043','#766354','#8b7766','#a08b79'],
    tin:['#56636a','#6b7a81','#83939a','#9eaeb4','#b8c7cc','#d0dde0'],
    tinRed:['#4a1f1c','#5f2824','#78332d','#8f4037','#a65244','#bb6656'],
    tinGreen:['#1f3a33','#294a41','#355c50','#446f61','#578374','#6d9888'],
  };
  const ROOFPAL={slate:'slateLit',asphaltGrey:'asph',asphaltBrown:'cedar',metal:'tin',metalRed:'tinRed',metalGreen:'tinGreen'};
  // camera-first pass: a door that stands out from its walls (the joinery stays green on the shutters)
  const DOORPAINT={sage:'red',white:'join',red:'blue',greyShingle:'blue',cream:'join',blue:'red',yellow:'green',green:'ivory',grey:'red',teal:'ivory',charcoal:'red'};
  const TUNING={floorTexture:0.48,trimAge:0.58,cutawayHeight:1.15,approachRadius:0.28,routeHalfWidth:0.44,
    wallMargin:0.13,propGap:0.06,pipeWidth:0.075,gutterWidth:0.12,lanternHeight:0.38};
  const ROOMS={drawing:'green',library:'green',study:'blue',dining:'red',morning:'linen',billiard:'green',
    bed:'blue',nursery:'linen',servant:'linen',dressing:'linen',bath:'tile',wc:'tile',
    kitchen:'tile',scullery:'tile',pantry:'linen',vestibule:'linen',landing:'linen',stairhall:'linen',passage:'linen'};
  const mix=(a,b,t)=>{const r=[];for(let k=1;k<6;k+=2)r.push(Math.round(parseInt(a.slice(k,k+2),16)*(1-t)+parseInt(b.slice(k,k+2),16)*t).toString(16).padStart(2,'0'));return '#'+r.join('');};
  const ramp=(name,night)=>PALETTE[name].map(c=>night?mix(c,'#152b32',.34):c);
  const enabled=()=>root.CoastalPass.enabled;
  function face(v,mat,b,db,layer){return {v,mat,b:b||0,db:db||0,uv:null,tex:null,flat:false,layer:layer||'furniture'};}
  function box(out,x0,x1,y0,y1,z0,z1,mat){
    out.push(face([[x0,y0,z0],[x1,y0,z0],[x1,y0,z1],[x0,y0,z1]],mat,-.15),face([[x1,y1,z0],[x0,y1,z0],[x0,y1,z1],[x1,y1,z1]],mat,.15),
      face([[x1,y0,z0],[x1,y1,z0],[x1,y1,z1],[x1,y0,z1]],mat),face([[x0,y1,z0],[x0,y0,z0],[x0,y0,z1],[x0,y1,z1]],mat,-.3),
      face([[x0,y0,z1],[x1,y0,z1],[x1,y1,z1],[x0,y1,z1]],mat,.1));
  }
  function flat(out,x0,x1,y0,y1,z,mat,layer,db){out.push(face([[x0,y0,z],[x1,y0,z],[x1,y1,z],[x0,y1,z]],mat,-1.9,db||0,layer||'floor'));}
  function materials(M,b){for(const k of Object.keys(PALETTE))M['cp_'+k]={ramp:ramp(k,b.night)};M.cp_shadow={ramp:['#343a31']};}
  function setMat(M,key,pal,b){if(M[key])M[key]=Object.assign({},M[key],{ramp:ramp(pal,b.night)});}
  function emit(out,M,name,opts,p,layer){
    const PI=root.PropIso;if(!PI)throw Error('CoastalPass needs Art/interiorPropRig.js');
    const em=PI.emit(name,opts,Object.assign({},p,{prefix:'cp_prop'+out.length+'_'}));
    for(const f of em.faces){f.layer=layer||'furniture';out.push(f);}Object.assign(M,em.mats);return em;
  }
  function lantern(out,x,y,z,axis){
    const h=TUNING.lanternHeight,w=.16;
    box(out,x-w,x+w,y-w,y+w,z,z+h,'cp_iron');
    if(axis==='x')box(out,x-w-.012,x+w+.012,y-w+.045,y+w-.045,z+.05,z+h-.06,'cp_warm');
    else box(out,x-w+.045,x+w-.045,y-w-.012,y+w+.012,z+.05,z+h-.06,'cp_warm');
    box(out,x-.20,x+.20,y-.20,y+.20,z+h,z+h+.07,'cp_iron');
    box(out,x-.09,x+.09,y-.09,y+.09,z+h+.07,z+h+.15,'cp_iron');
  }
  function planter(out,x,y,z){
    box(out,x-.29,x+.29,y-.29,y+.29,z,z+.43,'cp_clay');
    box(out,x-.33,x+.33,y-.33,y+.33,z+.38,z+.47,'cp_clay');
    for(const [dx,dy,h]of [[0,0,.90],[-.22,.04,.60],[.19,.1,.71],[.05,-.20,.65]]){
      const r=.18;out.push(face([[x+dx-r,y+dy,z+.43],[x+dx,y+dy-r,z+.43],[x+dx,y+dy,z+h]],'cp_leaf'),
        face([[x+dx,y+dy-r,z+.43],[x+dx+r,y+dy,z+.43],[x+dx,y+dy,z+h]],'cp_leaf',.4),
        face([[x+dx+r,y+dy,z+.43],[x+dx,y+dy+r,z+.43],[x+dx,y+dy,z+h]],'cp_leaf',-.2),
        face([[x+dx,y+dy+r,z+.43],[x+dx-r,y+dy,z+.43],[x+dx,y+dy,z+h]],'cp_leaf',-.4));
    }
  }
  function lampTag(faces,s0){for(let i=s0;i<faces.length;i++){faces[i].tag='entry.lantern';if(faces[i].mat==='cp_warm')faces[i].em='lantern';}}
  function exterior(kind,faces,M,b,opts){
    const live=kind==='house'&&!opts.classic;
    materials(M,b);setMat(M,'roof',live?(ROOFPAL[b.roof]||'slateLit'):'slate',b);setMat(M,'iron','iron',b);setMat(M,'stone','stone',b);setMat(M,'plinth','stone',b);
    setMat(M,'dress','stone',b);setMat(M,'trim',kind==='manor'?'join':'ivory',b);setMat(M,'dormer',kind==='manor'?'join':'ivory',b);
    setMat(M,'door',kind==='manor'?'oak':live?(b.doorPaint||DOORPAINT[b.body]||'join'):'join',b);setMat(M,'shutter','join',b);
    if(live)M.cp_shut={ramp:ramp(b.shutters&&PALETTE[b.shutters]?b.shutters:'join',b.night)};
    if(M.body){const old=M.body.ramp;M.body={ramp:old.map((c,i)=>mix(c,PALETTE.stone[Math.min(5,i)],kind==='manor'?.35:.15))};}
    for(const f of faces){if(f.tex&&['body','roof','plinth'].includes(f.mat)){const t=f.tex,k=live?(f.mat==='roof'?.9:f.mat==='body'?.85:.8):.62;f.tex=(u,v)=>t(u,v)*k;}}
    const hw=b.Wd/2,hl=b.Ln/2,p=TUNING.pipeWidth,g=TUNING.gutterWidth;
    // Gutter lengths stop at the existing roof perimeter; drains are outside corner trim.
    const ezX=x=>(live&&x<0&&b.eaveL!=null)?b.eaveL:b.eaveZ;   // phase 2: a saltbox's rear gutter hangs at its low eave
    for(const x of [-hw-.34,hw+.34])box(faces,x-g/2,x+g/2,-hl-.20,hl+.20,ezX(x)-.15,ezX(x)-.02,'cp_iron');
    const Y=live&&root.HouseIso.dooryard?root.HouseIso.dooryard(opts):null;
    if(!Y)for(const x of [-hw-.14,hw+.14])box(faces,x-p/2,x+p/2,-hl-.12,-hl-.12+p,.18,b.eaveZ-.10,'cp_iron');
    else box(faces,Y.backPipe.x-p/2,Y.backPipe.x+p/2,Y.backPipe.y-p/2,Y.backPipe.y+p/2,.18,ezX(Y.backPipe.x)-.10,'cp_iron');
    const e=kind==='house'?root.HouseIso.entrance(opts):{axis:'y',x:0,y:hl+(b.accent==='tower'?1.75:0),z:b.fH};
    if(live){
      // the host draws its own portico and lantern for an eave or side door; a door under the front porch keeps the pair
      if(e.axis==='y'&&e.facing==='+Y'){const s0=faces.length;lantern(faces,e.x-1.03,e.y+.23,e.z+1.70,'y');lantern(faces,e.x+1.03,e.y+.23,e.z+1.70,'y');lampTag(faces,s0);}
    }else if(e.axis==='y'){
      lantern(faces,e.x-1.03,e.y+.23,e.z+1.70,'y');lantern(faces,e.x+1.03,e.y+.23,e.z+1.70,'y');
      if(kind==='manor'){planter(faces,-1.75,e.y+.50,0);planter(faces,1.75,e.y+.50,0);}
    }else{
      lantern(faces,e.x+.23,e.y-.85,e.z+1.72,'x');
      // A modest weather hood for a bare side entrance. Keep the existing step opening.
      box(faces,e.x,e.x+.66,e.y-.88,e.y+.88,e.z+2.33,e.z+2.45,'cp_slate');
      for(const sy of [-.7,.7])box(faces,e.x+.02,e.x+.10,e.y+sy-.05,e.y+sy+.05,e.z+2.00,e.z+2.34,'cp_ivory');
    }
    if(kind==='house'){
      // Derive ground-floor shutters from the actual glass faces, not a duplicate window grid.
      const seen={},shutM=live?'cp_shut':'cp_join';for(const f of (live&&b.shutters==='none')?[]:faces.slice()){
        if(f.mat!=='glass'||f.v.length!==4)continue;
        const xs=f.v.map(v=>v[0]),ys=f.v.map(v=>v[1]),zs=f.v.map(v=>v[2]);
        const z0=Math.min(...zs),z1=Math.max(...zs),x0=Math.min(...xs),x1=Math.max(...xs),y0=Math.min(...ys),y1=Math.max(...ys);
        if(z0>2.0||z1-z0<.9)continue;
        const ax=x1-x0<.001?'x':y1-y0<.001?'y':null;if(!ax)continue;
        const c=ax==='x'?(y0+y1)/2:(x0+x1)/2,pl=ax==='x'?x0:y0,key=ax+'|'+pl.toFixed(2)+'|'+c.toFixed(2);if(seen[key])continue;seen[key]=1;
        const a=f.v[0],v=f.v[1],w=f.v[2],normal=ax==='x'?(v[1]-a[1])*(w[2]-a[2])-(v[2]-a[2])*(w[1]-a[1]):(v[2]-a[2])*(w[0]-a[0])-(v[0]-a[0])*(w[2]-a[2]);
        const n=-Math.sign(normal)||1,plane=pl+n*.055,half=(ax==='x'?y1-y0:x1-x0)/2;
        for(const side of [-1,1]){const lo=c+side*(half+.24)-.15,hi=lo+.30;
          if(ax==='x')box(faces,plane-.025,plane+.025,lo,hi,z0,z1,shutM);else box(faces,lo,hi,plane-.025,plane+.025,z0,z1,shutM);
          for(let j=1;j<5;j++){const zz=z0+(z1-z0)*j/5;
            if(ax==='x')box(faces,plane-.035,plane+.035,lo+.025,hi-.025,zz,zz+.025,'cp_iron');else box(faces,lo+.025,hi-.025,plane-.035,plane+.035,zz,zz+.025,'cp_iron');
          }
        }
      }
      if(Y){
        // camera-first: the goods stand on the side the camera sees, at heights character v9.2 works at (HouseIso.stations)
        const s0=faces.length,w=Y.woodpile,alongY=(w.y1-w.y0)>(w.x1-w.x0),L0=alongY?w.y0:w.x0,L1=alongY?w.y1:w.x1,D0=alongY?w.x0:w.y0,D1=alongY?w.x1:w.y1;
        const bx=(a0,a1,d0,d1,z0,z1,mat)=>alongY?box(faces,d0,d1,a0,a1,z0,z1,mat):box(faces,a0,a1,d0,d1,z0,z1,mat);
        bx(L0,L1,D0,D1,0,.12,'cp_oak');                                     // plank base on bearers
        for(const a of [L0,L1-.07])bx(a,a+.07,D0,D1,.12,w.top+.14,'cp_oak');  // end posts
        const rows=4,rh=(w.top-.12)/rows;
        for(let r=0;r<rows;r++){const z=.12+r*rh,off=(r&1)*.11;for(let a=L0+.07+off;a+.2<L1-.07;a+=.22)bx(a,a+.2,D0+.02,D1-.02,z+.01,z+rh,'cp_oak');}
        for(let i=s0;i<faces.length;i++)faces[i].tag='yard.woodpile';
        const s1=faces.length;emit(faces,M,'barrel',{wood:'driftwood',weather:.5},{x:Y.barrel.x,y:Y.barrel.y,z:0,face:'S'});
        const top=root.PropIso.height('barrel',{wood:'driftwood',weather:.5});
        box(faces,Y.pipe.x-p/2,Y.pipe.x+p/2,Y.pipe.y-p/2,Y.pipe.y+p/2,top+.04,b.eaveZ-.10,'cp_iron');
        for(let i=s1;i<faces.length;i++)faces[i].tag='yard.barrel';
      }else{
      // Firewood and a rain barrel on the rear service side, away from the entry.
      emit(faces,M,'barrel',{wood:'driftwood',weather:.5},{x:-hw-.58,y:-hl+1.0,z:0,face:'N'});
      for(let row=0;row<3;row++)for(let i=0;i<4-row;i++){
        const y=-hl+2.15+i*.27+row*.13,z=.1+row*.21;
        box(faces,-hw-.83,-hw-.22,y,y+.21,z,z+.19,'cp_oak');
      }
      }
    }
    return faces;
  }
  const intersects=(a,b,g=0)=>a.x0<b.x1+g&&a.x1>b.x0-g&&a.y0<b.y1+g&&a.y1>b.y0-g;
  function cottageStair(b){
    if(b.coastalStoreys<2)return null;
    const rise=Math.min(b.wallH-.35,2.5+b.size*.6),n=Math.ceil(rise/.19),going=.26,width=1.05,slab=.24,headroom=2;
    const x0=-.90,x1=x0+width,y0=-b.Ln/2+1.30,y1=y0+n*going;
    const covered=Math.max(0,Math.floor((rise-slab-headroom)/(rise/n)));
    const opening={x0:x0-.06,x1:x1+.06,y0,y1:y1-covered*going};
    const topLanding={x0,x1,y0:y0-1.02,y1:y0},bottomLanding={x0,x1,y0:y1,y1:y1+1.02};
    return {id:'cottage_stair',axis:'y',ascending:'negativeY',x0,x1,y0,y1,steps:n,going,riser:rise/n,
      floorRise:rise,slabThickness:slab,headroom,opening,topLanding,bottomLanding,
      bottom:{x:(x0+x1)/2,y:y1+.52,z:0},top:{x:(x0+x1)/2,y:y0-.52,z:rise}};
  }
  function minus(rect,hole){
    if(!hole||!intersects(rect,hole))return [rect];
    const x0=Math.max(rect.x0,hole.x0),x1=Math.min(rect.x1,hole.x1),y0=Math.max(rect.y0,hole.y0),y1=Math.min(rect.y1,hole.y1);
    return [{x0:rect.x0,x1:rect.x1,y0:rect.y0,y1:y0},{x0:rect.x0,x1:rect.x1,y0:y1,y1:rect.y1},
      {x0:rect.x0,x1:x0,y0,y1},{x0:x1,x1:rect.x1,y0,y1}].filter(r=>r.x1-r.x0>.001&&r.y1-r.y0>.001);
  }
  function cottageStairFaces(out,s,upper){
    const start=out.length;
    if(!upper){
      for(let i=0;i<s.steps;i++){
        const ya=s.y1-(i+1)*s.going,yb=ya+s.going,z=(i+1)*s.riser;
        box(out,s.x0,s.x1,ya,yb,Math.max(0,z-s.riser-.025),z,'cp_oak');
        flat(out,s.x0+.17,s.x1-.17,ya+.018,yb-.018,z+.005,'cp_red','stair',.005);
        for(const x of [s.x0-.035,s.x1+.035])box(out,x-.022,x+.022,ya+.05,ya+.085,z,z+.90,'cp_join');
      }
      for(const x of [s.x0-.035,s.x1+.035])out.push(face([[x-.035,s.y1,s.riser+.94],[x+.035,s.y1,s.riser+.94],
        [x+.035,s.y0,s.floorRise+.94],[x-.035,s.y0,s.floorRise+.94]],'cp_ivory',.2));
    }else{
      const v=s.opening;
      // Three guarded sides; the rear edge opens directly onto the top landing.
      for(const x of [v.x0-.025,v.x1+.025]){
        box(out,x-.035,x+.035,v.y0,v.y1,.88,.98,'cp_join');
        for(let y=v.y0+.06;y<v.y1;y+=.18)box(out,x-.022,x+.022,y,y+.035,0,.9,'cp_iron');
      }
      box(out,v.x0,v.x1,v.y1,v.y1+.065,.88,.98,'cp_join');
      for(let x=v.x0+.06;x<v.x1;x+=.18)box(out,x,x+.035,v.y1,v.y1+.045,0,.9,'cp_iron');
      // Visible final treads below this floor make the descending connection legible.
      for(let i=0;i<4;i++){const ya=s.y0+i*s.going,z=-i*s.riser;
        box(out,s.x0,s.x1,ya,ya+s.going,z-.045,z,'cp_oak');}
      for(const x of [v.x0-.06,v.x1])box(out,x,x+.06,v.y0,v.y1,-s.slabThickness,0,'cp_oak');
      box(out,v.x0,v.x1,v.y1,v.y1+.06,-s.slabThickness,0,'cp_oak');
    }
    for(let i=start;i<out.length;i++)out[i].layer='stair';
  }
  function cottagePlan(b){
    const hw=b.Wd/2,hl=b.Ln/2,m=TUNING.wallMargin+(b.wt||.16),box0={x0:-hw+m,x1:hw-m,y0:-hl+m,y1:hl-m};
    const stair=b.program==='school'?null:cottageStair(b),up=b.storey==='upper',routes=[],blockers=[],props=[],rejected=[],zones={};
    /* phase 2b: rooms, not one big room. Ground floor of a two-storey house: the kitchen at the back by the hearth and the
       stack, the parlour at the front by the door, and a partition between them that stops at the stair (the way through is
       beside it). Upstairs: a back bedroom round the stairwell and a front bedroom behind a partition with a doorway. A house
       with no upper storey: kitchen at the back, bedroom at the front. */
    const part=(y,x0,x1)=>{if(x1-x0>.05)blockers.push({kind:'partition',x0,x1,y0:y-.06,y1:y+.06});},zone=(y0,y1)=>({x0:box0.x0,x1:box0.x1,y0,y1});
    if(b.hearth)blockers.push({kind:'hearth',x0:-.94,x1:.94,y0:-hl,y1:-hl+1.02});
    if(stair)blockers.push(Object.assign({kind:up?'well':'stair'},up?stair.opening:{x0:stair.x0-.08,x1:stair.x1+.08,y0:stair.y0,y1:stair.y1}));
    let yd;
    if(!up&&b.program==='school'){yd=box0.y1+.2;routes.push({x0:-.6,x1:.6,y0:-hl,y1:hl});}   // phase 3: the schoolroom is one room, an aisle down the middle to the teacher
    else if(!up){yd=-hl+b.Ln*.47;if(b.doorWall==='E'||b.doorWall==='W')yd=Math.min(yd,(b.doorAt||0)-1.05);   // an eave door opens into the front room, never onto the partition
      if(stair){const gc=Math.min(hw-.75,(stair.x1+.08+hw)/2);part(yd,-hw,stair.x0-.9);part(yd,stair.x1+.08,gc-.5);part(yd,gc+.5,hw);   // two ways through: beside the stair, and a doorway on the far side
        routes.push({x0:stair.x0-.85,x1:stair.x0-.08,y0:-hl+1.02,y1:hl-.45},stair.bottomLanding,{x0:gc-.45,x1:gc+.45,y0:yd-.8,y1:yd+.8});}
      else{const gc=hw*.4;part(yd,-hw,gc-.55);part(yd,gc+.55,hw);routes.push({x0:gc-.45,x1:gc+.45,y0:yd-.8,y1:yd+.8});}
      const dW=b.doorWall||'N',dA=b.doorAt||0;   // inside the front door, wherever the shell puts it
      routes.push(dW==='N'?{x0:dA-.48,x1:dA+.48,y0:hl-1.5,y1:hl}:dW==='S'?{x0:dA-.48,x1:dA+.48,y0:-hl,y1:-hl+1.5}:dW==='E'?{x0:hw-1.5,x1:hw,y0:dA-.48,y1:dA+.48}:{x0:-hw,x1:-hw+1.5,y0:dA-.48,y1:dA+.48});
    }else{const well=stair?stair.opening:null;yd=well?Math.min(well.y1+.3,hl-2.4):-hl+b.Ln*.5;
      if(b.Ln>=7.2){const gc=well?Math.min(hw-.7,well.x1+.75):hw*.35;part(yd,-hw,gc-.5);part(yd,gc+.5,hw);routes.push({x0:gc-.45,x1:gc+.45,y0:yd-.8,y1:yd+.8});
        if(well)routes.push({x0:well.x1+.1,x1:Math.min(box0.x1,well.x1+1.0),y0:stair.topLanding.y0,y1:yd+.6});}
      if(stair)routes.push(stair.topLanding);
    }
    zones.back=zone(box0.y0,yd-.1);zones.front=zone(yd+.1,box0.y1);
    function add(id,name,face,positions,options,verb,zone){
      const Z=zone||box0,fp=root.PropIso.footprint(name,options),rot=face==='E'||face==='W',w=rot?fp.d:fp.w,d=rot?fp.w:fp.d,h=root.PropIso.height(name,options);
      // Search spare wall-side slots in the prop's own room when the staircase displaces a preferred furnishing.
      if(!['chairA','chairB'].includes(id))for(let y=Z.y0+d/2+.04;y<Z.y1-d/2;y+=.28)for(const x of [Z.x0+w/2+.04,Z.x1-w/2-.04])positions.push([x,y]);
      for(const [x,y]of positions){const rect={x0:x-w/2,x1:x+w/2,y0:y-d/2,y1:y+d/2};
        if(rect.x0<Z.x0||rect.x1>Z.x1||rect.y0<Z.y0||rect.y1>Z.y1)continue;
        if(blockers.some(a=>intersects(a,rect,TUNING.propGap))||routes.some(a=>intersects(a,rect)))continue;
        if(b.prof){const zAt=x=>{for(let i=1;i<b.prof.length;i++){const a=b.prof[i-1],v=b.prof[i];if(x>=a[0]&&x<=v[0])return a[1]+(v[1]-a[1])*(x-a[0])/(v[0]-a[0]);}return 0;};if(Math.min(zAt(rect.x0),zAt(rect.x1))<h+.12)continue;}
        const p={id,name,face,x,y,z:b.fZ||0,options,verb,rect,height:h};props.push(p);blockers.push(Object.assign({kind:name,id},rect));return p;
      }
      rejected.push({id,name,reason:'No slot clear of walls, props, route and roof clearance'});return null;
    }
    const K=zones.back,P=zones.front,X0=box0.x0,X1=box0.x1,Y0=box0.y0,Y1=box0.y1,winY=[],mid=Z=>(Z.y0+Z.y1)/2,inZ=(Z,y,pad)=>y>Z.y0+pad&&y<Z.y1-pad;
    {const nL=Math.max(1,Math.round((b.Ln/2.4)*(.5+(b.winD!=null?b.winD:.6)))),a=-hl+(b.wt||.16),c=hl-(b.wt||.16);for(let i=0;i<nL;i++)winY.push(a+(c-a)*((i+.5)/nL));}
    const bedO=(f)=>({variant:1,wood:'pine',fabric:f,fabric2:'cream'});
    if(!up&&b.program==='school'){
      // pupils face the teacher's desk and the blackboard on the far gable; a stove to one side, books and the clock on the walls
      add('teacher','writingDesk','S',[[0,Y0+.75],[-.9,Y0+.75]],{wood:'oak'},'teach',K);
      add('teacherChair','chair','S',[[0,Y0+.2],[-.9,Y0+.2]],{wood:'pine'},'sit',K);
      add('stove','stove','W',[[X1-.35,Y0+1.2],[X1-.35,mid(K)]],{wood:'oak'},'tend',K);
      add('books','bookcase','E',[[X0+.18,Y0+1.3],[X0+.18,mid(K)]],{wood:'oak'},'store',K);
      add('clock','longClock','N',[[X1-.35,Y1-.15],[X0+.35,Y1-.15]],{wood:'walnut'},'look',K);
      let n=0;for(let r=0;r<4;r++)for(const sx of [-1,1]){const y=Y0+2.1+r*1.25;if(y>Y1-1.2)continue;const x=sx*(.62+.74);
        if(add('desk'+n,'table','N',[[x,y]],{len:0,wood:'pine'},'study',K))add('bench'+n,'bench','S',[[x,y+.6]],{wood:'pine'},'sit',K);n++;}
    }else if(!up){
      // kitchen: the range by the stack (beside the hearth, or at the chimney wall), the sink under a side window, a hutch for the dishes, the icebox, the table
      const rx=b.hearth?1.66:0;
      add('range','stove','S',[[rx,Y0+.36],[-rx,Y0+.36],[1.7,Y0+.36],[-1.7,Y0+.36],[X1-.62,Y0+.36]],{wood:'oak'},'cook',K);
      add('sink','sink','W',winY.filter(y=>inZ(K,y,.5)).map(y=>[X1-.3,y]).concat([[X1-.3,mid(K)]]),{paint:'sage',worktop:'slate'},'wash_dishes',K);
      add('hutch','hutch','E',[[X0+.25,mid(K)],[X0+.25,K.y0+.7]],{paint:'sage',wood:'oak'},'store',K);
      add('icebox','icebox','S',[[-1.7,Y0+.3],[X0+.36,Y0+.3]],{},'store',K);
      const tx=stair?Math.max(stair.x1+.95,.9):.4,dining=add('table','table','N',[[tx,mid(K)+.25],[tx,mid(K)-.25],[.9,K.y1-.9]],{len:0,wood:'oak'},'dine',K);
      if(dining){add('chairA','chair','S',[[dining.x,dining.y-.75]],{wood:'pine'},'sit',K);add('chairB','chair','N',[[dining.x,dining.y+.75]],{wood:'pine'},'sit',K);}
      if(stair){
        // parlour: a settee on the side wall, an armchair facing it, books, the long clock by the door
        add('settee','settee','W',[[X1-.4,mid(P)],[X1-.4,P.y1-1.3]],{fabric:'red',fabric2:'cream',wood:'walnut'},'sit',P);
        add('armchair','armchair','E',[[X1-1.95,mid(P)+.3],[.6,P.y1-1.0]],{fabric:'green',fabric2:'cream',wood:'walnut'},'sit',P);
        add('bookcase','bookcase','E',[[X0+.18,mid(P)+.4],[X0+.18,P.y1-1.1]],{wood:'oak'},'store',P);
        add('clock','longClock','N',[[X0+.35,Y1-.15],[X1-.35,Y1-.15]],{wood:'walnut'},'look',P);
        add('seaChest','seaChest','E',[[X0+.26,P.y0+.7]],{wood:'oak'},'store',P);
      }else{
        // no upper storey: the bedroom is the front room
        add('bed','bed','N',[[X0+.8,Y1-1.03],[X1-.8,Y1-1.03]],bedO('blue'),'sleep',P);
        add('dresser','dresser','W',[[X1-.26,mid(P)]],{wood:'oak'},'store',P);
        add('seaChest','seaChest','N',[[0,Y1-.26]],{wood:'oak'},'store',P);
      }
    }else{
      // back bedroom round the stairwell (the bed beside the well, a washstand and a dresser on the knee wall); front bedroom behind the partition
      const well=stair?stair.opening:null,lx=well?Math.max(X0+.78,(X0+well.x0)/2):X0+.78;
      add('bed','bed','S',[[lx,well?(well.y0+well.y1)/2:mid(K)],[X1-.8,mid(K)]],bedO('blue'),'sleep',K);
      add('washstand','washstand','W',[[X1-.25,K.y0+.8],[X1-.25,mid(K)]],{wood:'oak'},'wash',K);
      add('dresser','dresser','W',[[X1-.26,K.y1-.8],[X1-.26,mid(K)]],{wood:'oak'},'store',K);
      const nb=Math.max(1,Math.min(3,(b.shell&&b.shell.beds)||3));   // the household: shell.beds (one bed in the back room, the rest in front)
      if(b.Ln>=7.2){
        if(nb>=2)add('bed2','bed','N',[[-hw*.45,Y1-1.03],[hw*.45,Y1-1.03],[X0+.8,Y1-1.03]],bedO('red'),'sleep',P);
        else{const dk=add('desk','writingDesk','N',[[-hw*.4,Y1-.4],[hw*.4,Y1-.4]],{wood:'oak'},'desk',P);if(dk)add('deskChair','chair','S',[[dk.x,dk.y-.62]],{wood:'pine'},'sit',P);}
        if(nb>=3&&b.Wd>=6.3)add('bed3','bed','N',[[hw*.45,Y1-1.03],[-hw*.45,Y1-1.03]],bedO('green'),'sleep',P);
        add('chest','seaChest','S',[[0,P.y0+.35],[X1-.5,P.y0+.35]],{wood:'oak'},'store',P);
      }else add('chest','seaChest','N',[[0,Y1-.26]],{wood:'oak'},'store',K);
    }
    // Conservative 2D grid traversal, with blockers expanded by the player's radius.
    const radius=TUNING.approachRadius,step=.12,nx=Math.floor((box0.x1-box0.x0)/step)+1,ny=Math.floor((box0.y1-box0.y0)/step)+1;
    const free=(x,y)=>x-radius>=box0.x0&&x+radius<=box0.x1&&y-radius>=box0.y0&&y+radius<=box0.y1&&!blockers.some(v=>intersects({x0:x-radius,x1:x+radius,y0:y-radius,y1:y+radius},v));
    const open=new Uint8Array(nx*ny),visited=new Uint8Array(nx*ny),queue=[];
    for(let j=0;j<ny;j++)for(let i=0;i<nx;i++)if(free(box0.x0+i*step,box0.y0+j*step))open[j*nx+i]=1;
    const dW0=b.doorWall||'N',dA0=b.doorAt||0,seed=stair&&b.storey==='upper'?stair.top:(dW0==='N'?{x:dA0,y:hl-.75}:dW0==='S'?{x:dA0,y:-hl+.75}:dW0==='E'?{x:hw-.75,y:dA0}:{x:-hw+.75,y:dA0});
    const si=Math.round((seed.x-box0.x0)/step),sj=Math.round((seed.y-box0.y0)/step),start=sj*nx+si;
    if(open[start]){visited[start]=1;queue.push(start);}
    for(let q=0;q<queue.length;q++){const k=queue[q],i=k%nx,j=Math.floor(k/nx);for(const [dx,dy]of [[1,0],[-1,0],[0,1],[0,-1]]){const ii=i+dx,jj=j+dy,kk=jj*nx+ii;if(ii<0||jj<0||ii>=nx||jj>=ny||!open[kk]||visited[kk])continue;visited[kk]=1;queue.push(kk);}}
    const reachable=(x,y)=>{if(!free(x,y))return false;const i=Math.round((x-box0.x0)/step),j=Math.round((y-box0.y0)/step);for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){const ii=i+dx,jj=j+dy;if(ii<0||jj<0||ii>=nx||jj>=ny||!visited[jj*nx+ii])continue;const tx=box0.x0+ii*step,ty=box0.y0+jj*step;let clear=true;for(let t=1;t<=4;t++)if(!free(x+(tx-x)*t/4,y+(ty-y)*t/4)){clear=false;break;}if(clear)return true;}return false;};
    // Object targets, standing points, and seated poses have different meanings.
    const approaches=props.map(p=>{
      const d=.40,r=TUNING.approachRadius,rx=p.rect;
      const sides={N:[[p.x,rx.y0-d]],S:[[p.x,rx.y1+d]],E:[[rx.x1+d,p.y]],W:[[rx.x0-d,p.y]]},choices=sides[p.face].concat(...Object.values(sides));
      for(let x=rx.x0+.08;x<rx.x1;x+=.16)choices.push([x,rx.y0-d],[x,rx.y1+d]);
      for(let y=rx.y0+.08;y<rx.y1;y+=.16)choices.push([rx.x0-d,y],[rx.x1+d,y]);
      let approach=null;for(const [x,y]of choices){if(reachable(x,y)){approach={x,y,z:p.z,radius:r};break;}}
      return {id:p.id,verb:p.verb,target:{x:p.x,y:p.y,z:p.z},approach};
    });
    for(let i=approaches.length-1;i>=0;i--)if(!approaches[i].approach&&!['bed','range','sink','table','cupboard'].includes(approaches[i].id)){
      const id=approaches[i].id;rejected.push({id,reason:'Omitted because no reachable standing point remains'});
      approaches.splice(i,1);props.splice(props.findIndex(p=>p.id===id),1);const j=blockers.findIndex(p=>p.id===id);if(j>=0)blockers.splice(j,1);
    }
    const stairAccess=stair?{bottom:b.storey==='upper'?null:reachable(stair.bottom.x,stair.bottom.y),top:b.storey==='upper'?reachable(stair.top.x,stair.top.y):null}:null;
    return {schema:'coastal-cottage-layout/2',props,blockers,routes,approaches,rejected,stair,stairAccess,
      navigation:{radius,gridStep:step,reachableCells:queue.length,start:{x:box0.x0+si*step,y:box0.y0+sj*step}},
      note:'Interior-local metres. Pair floors using identical type, size, shape and pitch. Stair top z is relative to the ground floor; upper sprite floor z is zero. Navigation checks floor approaches; Unity locomotion and door animation remain integration work.'};
  }
  function carpet(out,x0,x1,y0,y1,z,col,layer){
    flat(out,x0,x1,y0,y1,z,'cp_'+col,layer,.012);
    const t=.09;for(const r of [[x0+.10,x1-.10,y0+.10,y0+.10+t],[x0+.10,x1-.10,y1-.10-t,y1-.10],
      [x0+.10,x0+.10+t,y0+.10,y1-.10],[x1-.10-t,x1-.10,y0+.10,y1-.10]])flat(out,...r,z+.002,'cp_linen',layer,.02);
  }
  // a horizontal axis-aligned quad less a rectangle, uv carried over (a named floor keeps the stairwell open)
  function clipQuad(f,hole){
    if(!hole||f.v.length!==4)return [f];const xs=f.v.map(v=>v[0]),ys=f.v.map(v=>v[1]),z=f.v[0][2];
    if(f.v.some(v=>Math.abs(v[2]-z)>1e-6))return [f];
    const R={x0:Math.min(...xs),x1:Math.max(...xs),y0:Math.min(...ys),y1:Math.max(...ys)};if(!intersects(R,hole))return [f];
    const near=(cx,cy)=>{let k=0,d=1e9;f.v.forEach((v,i)=>{const e=Math.hypot(v[0]-cx,v[1]-cy);if(e<d){d=e;k=i;}});return f.uv[k];};
    const at=(x,y)=>{const u00=near(R.x0,R.y0),u10=near(R.x1,R.y0),u11=near(R.x1,R.y1),u01=near(R.x0,R.y1),s=(x-R.x0)/((R.x1-R.x0)||1),t=(y-R.y0)/((R.y1-R.y0)||1);
      return [0,1].map(k=>u00[k]*(1-s)*(1-t)+u10[k]*s*(1-t)+u11[k]*s*t+u01[k]*(1-s)*t);};
    return minus(R,hole).map(r=>Object.assign({},f,{v:[[r.x0,r.y0,z],[r.x1,r.y0,z],[r.x1,r.y1,z],[r.x0,r.y1,z]],
      uv:f.uv?[at(r.x0,r.y0),at(r.x1,r.y0),at(r.x1,r.y1),at(r.x0,r.y1)]:null}));
  }
  function cottage(faces,M,b,opts){
    if(b.fam!=='house')return faces;
    // 09-16 Job 2 (owner: a named floor wins): with no floor named the pass lays its own tile / oak bands as before
    const named=!!(opts&&opts.floor!=null);
    materials(M,b);setMat(M,'plaster','linen',b);setMat(M,'paper','green',b);setMat(M,'trim','ivory',b);setMat(M,'wood','join',b);setMat(M,'stone','stone',b);
    const hw=b.Wd/2,hl=b.Ln/2,plan=cottagePlan(b),out=[];
    for(const f of faces){
      if((f.layer==='floor'&&!named)||f.layer==='stair'||f.layer?.startsWith('dv'))continue;
      if(f.layer==='floor'&&b.storey==='upper'&&plan.stair){for(const q of clipQuad(f,plan.stair.opening))out.push(q);continue;}
      if(plan.stair&&f.layer==='beam'&&b.storey!=='upper')continue;
      if(f.tex&&['plaster','paper','wood'].includes(f.mat)){const t=f.tex;f.tex=(u,v)=>t(u,v)*.48;}
      out.push(f);
    }
    const bands=b.storey==='upper'?[[ -hl,hl,'oak']]:[[-hl,0,'tile'],[0,hl,'oak']];
    const ph=b.section?b.cutH:TUNING.cutawayHeight;   // phase 2b: a section cuts the partitions at the same sill height as the outer walls
    for(const p of plan.blockers.filter(p=>p.kind==='partition')){const start=out.length;box(out,p.x0,p.x1,p.y0,p.y1,0,ph,'cp_linen');if(b.section)box(out,p.x0-.012,p.x1+.012,p.y0-.012,p.y1+.012,ph,ph+.03,'cp_ivory');for(let i=start;i<out.length;i++)out[i].layer='partition';}
    if(!named)for(const [ya,yb,pal]of bands)for(const r of minus({x0:-hw,x1:hw,y0:ya,y1:yb},b.storey==='upper'?plan.stair?.opening:null)){const {x0,x1,y0,y1}=r;const f=face([[x0,y0,0],[x1,y0,0],[x1,y1,0],[x0,y1,0]],'cp_'+pal,-2.2,0,'floor');
      f.uv=f.v.map(v=>[v[0],v[1]]);f.tex=pal==='tile'?(u,v)=>((u% .68+.68)%.68<.025||(v%.68+.68)%.68<.025)?-1:0:
        (u,v)=>((u%.28+.28)%.28<.018)?-1:0;out.push(f);}
    for(const p of plan.props){
      flat(out,p.rect.x0-.07,p.rect.x1+.07,p.rect.y0-.07,p.rect.y1+.07,.007,'cp_shadow','floor',.004);
      const em=emit(out,M,p.name,Object.assign({weather:.22,night:b.night},p.options),p,'furniture_'+p.id);
      // A table earns its clutter: crockery and a bread board remain attached to that surface.
      if(p.name==='table'){
        box(out,p.x-.31,p.x+.05,p.y-.17,p.y+.02,.752,.78,'cp_oak');
        for(const dx of [-.3,.32])box(out,p.x+dx-.085,p.x+dx+.085,p.y+.13,p.y+.26,.752,.79,'cp_ivory');
      }
    }
    carpet(out,.45,hw-.4,hl-2.75,hl-1.85,.013,'red','floor');
    if(plan.stair)cottageStairFaces(out,plan.stair,b.storey==='upper');
    return out;
  }
  function manorInterior(faces,M,b,ctx){
    materials(M,b);setMat(M,'wall','linen',b);setMat(M,'join','ivory',b);setMat(M,'panel','oak',b);setMat(M,'damask','red',b);
    setMat(M,'floor','oak',b);setMat(M,'hallFloor','tile',b);setMat(M,'wet','tile',b);
    const out=[],rooms=b.L.rooms;
    const roomAt=(x,y,level)=>rooms.find(r=>r.level===level&&x>=r.x0-.05&&x<=r.x1+.05&&y>=r.y0-.05&&y<=r.y1+.05);
    for(const f of faces){
      if(f.propKind==='rug')continue;
      const xs=f.v.map(v=>v[0]),ys=f.v.map(v=>v[1]),zs=f.v.map(v=>v[2]);
      const x0=Math.min(...xs),x1=Math.max(...xs),y0=Math.min(...ys),y1=Math.max(...ys),z0=Math.min(...zs),z1=Math.max(...zs);
      let level=0;for(const li of b.levelList)if(z0>=b.L.levels[li].floorZ+li*b.explode-.15)level=li;
      const room=roomAt((x0+x1)/2,(y0+y1)/2,level),pal=ROOMS[room?.kind]||'linen';
      if(f.mat==='wall'&&z1-z0>.3){
        // Split axis-aligned wall faces by room boundary, so the same shell carries distinct rooms.
        const alongX=y1-y0<.001,alongY=x1-x0<.001;
        if(alongX||alongY){let split=false;
          const va=f.v[0],vb=f.v[1],vc=f.v[2],nx=(vb[1]-va[1])*(vc[2]-va[2])-(vb[2]-va[2])*(vc[1]-va[1]),ny=(vb[2]-va[2])*(vc[0]-va[0])-(vb[0]-va[0])*(vc[2]-va[2]);
          for(const r of rooms.filter(r=>r.level===level)){
            if(nx*((r.x0+r.x1-x0-x1)/2)+ny*((r.y0+r.y1-y0-y1)/2)<-.001)continue;
            const match=alongX?Math.min(Math.abs(r.y0-y0),Math.abs(r.y1-y0))<.20:Math.min(Math.abs(r.x0-x0),Math.abs(r.x1-x0))<.20;
            if(!match)continue;const lo=Math.max(alongX?x0:y0,alongX?r.x0:r.y0),hi=Math.min(alongX?x1:y1,alongX?r.x1:r.y1);if(hi-lo<.03)continue;
            const nf=Object.assign({},f,{mat:'cp_'+(ROOMS[r.kind]||'linen'),v:f.v.map(v=>alongX?[Math.max(lo,Math.min(hi,v[0])),v[1],v[2]]:[v[0],Math.max(lo,Math.min(hi,v[1])),v[2]])});out.push(nf);split=true;
          }if(split)continue;
        }
      }
      if(f.mat==='damask')f.mat='cp_'+(pal==='linen'?'green':pal);
      if(['floor','hallFloor','wet'].includes(f.mat)){
        f.b-=1.15;if(f.tex){const t=f.tex;f.tex=(u,v)=>t(u,v)*TUNING.floorTexture;}
      }
      out.push(f);
    }
    for(const r of rooms.filter(r=>b.levelList.includes(r.level))){const z=b.L.levels[r.level].floorZ+r.level*b.explode;
      if(['drawing','library','dining','bed','study'].includes(r.kind)){
        const w=Math.min(2.8,(r.x1-r.x0)*.64),d=Math.min(3.0,(r.y1-r.y0)*.55),x=(r.x0+r.x1)/2,y=(r.y0+r.y1)/2;
        carpet(out,x-w/2,x+w/2,y-d/2,y+d/2,z+.012,ROOMS[r.kind]==='blue'?'blue':ROOMS[r.kind]==='green'?'red':'green');
      }
      if(['vestibule','landing','stairhall'].includes(r.kind)){
        const half=Math.min(.50,(r.x1-r.x0)*.15),x=(r.x0+r.x1)/2;
        carpet(out,x-half,x+half,r.y0+.35,r.y1-.35,z+.013,'red');
      }
    }
    // Dark contact patches sit on each actual floor, without changing gameplay blockers.
    for(const p of ctx.blockers){if(['wall','stair','well','rug'].includes(p.kind)||!p.room)continue;
      const z=b.L.levels[p.level]?.floorZ+p.level*b.explode;if(!Number.isFinite(z))continue;
      flat(out,p.x0-.035,p.x1+.035,p.y0-.035,p.y1+.035,z+.006,'cp_shadow');
    }
    // Decorative floor patches must retain the same stairwell holes as the structural floor.
    const clipped=[];
    for(const f of out){
      if(f.layer!=='floor'||f.v.length!==4){clipped.push(f);continue;}
      const z=f.v[0][2],lv=b.levelList.find(i=>Math.abs(z-(b.L.levels[i].floorZ+i*b.explode))<.04);
      if(lv==null){clipped.push(f);continue;}
      let rects=[{x0:Math.min(...f.v.map(v=>v[0])),x1:Math.max(...f.v.map(v=>v[0])),y0:Math.min(...f.v.map(v=>v[1])),y1:Math.max(...f.v.map(v=>v[1]))}];
      for(const v of b.L.levels[lv].voids||[]){const next=[];for(const r of rects){
        if(!intersects(r,v)){next.push(r);continue;}const x0=Math.max(r.x0,v.x0),x1=Math.min(r.x1,v.x1),y0=Math.max(r.y0,v.y0),y1=Math.min(r.y1,v.y1);
        next.push({x0:r.x0,x1:r.x1,y0:r.y0,y1:y0},{x0:r.x0,x1:r.x1,y0:y1,y1:r.y1},
          {x0:r.x0,x1:x0,y0,y1},{x0:x1,x1:r.x1,y0,y1});
      }rects=next.filter(r=>r.x1-r.x0>.002&&r.y1-r.y0>.002);}
      for(const r of rects)clipped.push(Object.assign({},f,{v:[[r.x0,r.y0,z],[r.x1,r.y0,z],[r.x1,r.y1,z],[r.x0,r.y1,z]]}));
    }
    return clipped;
  }
  function apply(kind,faces,M,b,opts,ctx){if(!enabled()||opts?.coastalPass===false)return faces;
    if(kind==='manorInterior')return manorInterior(faces,M,b,ctx);
    if(kind==='cottage')return cottage(faces,M,b,opts);
    return exterior(kind,faces,M,b,opts||{});
  }
  /* ======================= LIVE LIGHT (camera-first pass, 2026-09-25) =======================
     The tree rig's pass-4 law (TreeRig4, WharfRig2): light is not baked. A host rig rasterises its faces to a G-buffer
     (material + structure band, normal, model position, depth, tag, emitter) and relights it from any WeatherSky.at()
     sky: sky x visibility x up-facing + sun x N.L x shadow + ground bounce, banded on one ladder, then the sky's grade.
     Sky visibility from nine occlusion maps; a sun map per sky, so eaves, porch roofs, hoods and chimneys shade the
     house itself; castShadow() on the ground (levels 1 canopy, 2 partial, 3 full, as TreeRig4); rain, fog, snow and a
     sun rim when backlit. NIGHT: emitters (window glass, the door's lights, lanterns, a fire) glow at the level the host
     gives each one, and point lamps add a warm term that is banded with the rest. No dither; ringless unless o.outline.
     Shared by HouseIso and InteriorIso through this companion, which both already load first. Pure: no DOM, no clock. */
  const light=(function(){
    const D2R=Math.PI/180,clamp=(v,a,b)=>v<a?a:v>b?b:v;
    const h2r=h=>[parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)];
    const nrm=v=>{const L=Math.hypot(v[0],v[1],v[2])||1;return [v[0]/L,v[1]/L,v[2]/L];};
    const crs=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
    const CE0=Math.cos(40*D2R),SE0=Math.sin(40*D2R);
    const toView=w=>[w[0],-w[2]*CE0+w[1]*SE0,w[2]*SE0+w[1]*CE0];
    const dirOf=(az,el)=>{const c=Math.cos(el*D2R);return [Math.sin(az*D2R)*c,-Math.cos(az*D2R)*c,Math.sin(el*D2R)];};
    const REF_SUN=dirOf(290,55);
    const REF_SKY={name:'Afternoon \u00b7 reference key',time:14,key:'sun',sunW:REF_SUN,sunV:toView(REF_SUN),sunI:1,sunI0:1,skyI:0.60,expo:1,kc:'#fff0cf',ac:'#1d3b4a',ka:0.16,aa:0.30,
      rc:'#bcd6e2',ra:0.14,wash:'#fff4dd',wa:0.03,amb:0.62,fogC:'#c3cdce',skyC:'#b0c9d8',fog:0,wet:0,snow:0};
    const NIGHT_SUN=dirOf(125,42);
    const NIGHT_SKY={name:'Night \u00b7 moon',time:22.75,key:'moon',sunW:NIGHT_SUN,sunV:toView(NIGHT_SUN),sunI:0.22,sunI0:0.22,skyI:0.30,expo:1,kc:'#a8bce0',ac:'#101a33',ka:0.22,aa:0.62,
      rc:'#7f98c8',ra:0.30,wash:'#16223f',wa:0.46,amb:0.36,fogC:'#4a5670',skyC:'#27365a',fog:0,wet:0,snow:0};
    const UNLIT_SKY={name:'unlit',sunW:[0,0,1],sunV:toView([0,0,1]),sunI:0,skyI:1.25,expo:1,kc:'#ffffff',ac:'#000000',ka:0,aa:0,wash:'#ffffff',wa:0,amb:1,fogC:'#ffffff',fog:0,wet:0,snow:0,grade:false};
    const KEYLINE='#101d21',SNOWR=['#5c7180','#7d93a0','#a8bcc4','#cfdde1','#eef4f4'].map(h2r);
    const WARM=['#5c3a1a','#93602b','#c88b3d','#e6b35a','#f6d68b','#fff0c2'].map(h2r), FIRE=['#5a1e0c','#8f3312','#c85a1c','#ec8a2c','#f9b552','#ffe08e'].map(h2r);
    function faceBand(E){return E>=0.72?1:E>=0.24?0:E>=0.095?-1:E>=0.04?-2:-3;}
    const hsh=(a,b,c,s)=>{let h=Math.imul(a|0,374761393)+Math.imul(b|0,668265263)+Math.imul(c|0,1440670441)+Math.imul(s|0,1274126177)|0;h^=h>>>13;h=Math.imul(h,1103515245)|0;h^=h>>>16;return (h>>>0)/4294967296;};
    function vn3(x,y,z,s){const xi=Math.floor(x),yi=Math.floor(y),zi=Math.floor(z),fx=x-xi,fy=y-yi,fz=z-zi,u=fx*fx*(3-2*fx),v=fy*fy*(3-2*fy),w=fz*fz*(3-2*fz);
      const L=(a,b,t)=>a+(b-a)*t,c=(i,j,k)=>hsh(xi+i,yi+j,zi+k,s);
      return L(L(L(c(0,0,0),c(1,0,0),u),L(c(0,1,0),c(1,1,0),u),v),L(L(c(0,0,1),c(1,0,1),u),L(c(0,1,1),c(1,1,1),u),v),w);}
    // what weather does to a material, by name (wet darkening, rain glint, how well snow holds)
    const MPROP={roof:[.12,.55,1],cp_slate:[.12,.55,1],slate:[.12,.55,1],body:[.22,.05,.3],lower:[.22,.05,.3],trim:[.12,.12,.8],cp_ivory:[.12,.12,.8],
      stone:[.32,.1,.9],cp_stone:[.32,.1,.9],plinth:[.32,.1,.9],wood:[.36,.08,1],cp_oak:[.36,.08,1],cp_join:[.14,.2,.8],brick:[.3,.08,.9],glass:[0,1,0],glassHi:[0,1,0],
      door:[.08,.3,.2],lite:[0,1,0],pot:[.3,.08,.9],cp_shut:[.14,.2,.8],cp_iron:[.02,.6,.6],cp_warm:[0,.9,0],dark:[0,0,0],cp_clay:[.3,.1,.9],cp_leaf:[.2,.2,.7]};
    /* a model: the faces and materials one set of options builds. Occlusion maps are in model metres, so one model serves
       all eight facings; the host caches it per geometry. faces: {v, mat, b, db, uv, tex, tag?, em?} */
    function model(faces,MATS,o){
      o=o||{};const keys=Object.keys(MATS),MI={},MA=[];
      keys.forEach((k,i)=>{MI[k]=i;const r=MATS[k].ramp.map(h2r),p=MPROP[k]||MPROP[k.replace(/^cp_prop\d+_/,'')]||[.2,.1,.7],n=r.length;
        MA.push({key:k,ramp:r,n,base:n>=6?3:n>=5?2:Math.max(0,Math.floor((n-1)/2)),por:p[0],gloss:p[1],hold:p[2]});});
      const TAGS=[''],TI={'':0},EM=[''],EI={'':0};
      const fs=faces.map(f=>{let ti=0,ei=0;
        if(f.tag){if(TI[f.tag]==null){TI[f.tag]=TAGS.length;TAGS.push(f.tag);}ti=TI[f.tag];}
        if(f.em){if(EI[f.em]==null){EI[f.em]=EM.length;EM.push(f.em);}ei=EI[f.em];}
        return {v:f.v,mi:MI[f.mat]!=null?MI[f.mat]:(MI.body!=null?MI.body:0),b:f.b||0,db:f.db||0,uv:f.uv,tex:f.tex,ti,ei,fire:!!f.fire};});
      return {faces:fs,MA,MI,TAGS,TI,EM,EI,ao:null,sun:new Map(),aoRes:o.aoRes||12,sunRes:o.sunRes||24};
    }
    function occMap(faces,L,res){
      const w=nrm(L),hint=Math.abs(w[2])<0.95?[0,0,1]:[1,0,0],u=nrm(crs(hint,w)),v=crs(w,u);
      let u0=1e9,u1=-1e9,v0=1e9,v1=-1e9;const tri=[];
      for(const f of faces){const pv=f.v.map(q=>[q[0]*u[0]+q[1]*u[1]+q[2]*u[2],q[0]*v[0]+q[1]*v[1]+q[2]*v[2],q[0]*w[0]+q[1]*w[1]+q[2]*w[2]]);
        for(const q of pv){if(q[0]<u0)u0=q[0];if(q[0]>u1)u1=q[0];if(q[1]<v0)v0=q[1];if(q[1]>v1)v1=q[1];}tri.push(pv);}
      if(!tri.length){u0=v0=0;u1=v1=1;}u0-=2/res;v0-=2/res;
      const gw=Math.max(1,Math.ceil((u1-u0)*res)+4),gh=Math.max(1,Math.ceil((v1-v0)*res)+4),T=new Float32Array(gw*gh).fill(-1e9);
      for(const pv of tri)for(let t=1;t+1<pv.length;t++){
        const A=pv[0],B=pv[t],C=pv[t+1],ax=(A[0]-u0)*res,ay=(A[1]-v0)*res,bx=(B[0]-u0)*res,by=(B[1]-v0)*res,cx=(C[0]-u0)*res,cy=(C[1]-v0)*res;
        const area=(bx-ax)*(cy-ay)-(cx-ax)*(by-ay),sg=area<0?-1:1,lab=Math.hypot(bx-ax,by-ay)||1,lbc=Math.hypot(cx-bx,cy-by)||1,lca=Math.hypot(ax-cx,ay-cy)||1;
        const tmin=Math.min(A[2],B[2],C[2]),tmax=Math.max(A[2],B[2],C[2]),deg=Math.abs(area)<1e-6;
        const X0=Math.max(0,Math.floor(Math.min(ax,bx,cx)-1)),X1=Math.min(gw-1,Math.ceil(Math.max(ax,bx,cx)+1)),Y0=Math.max(0,Math.floor(Math.min(ay,by,cy)-1)),Y1=Math.min(gh-1,Math.ceil(Math.max(ay,by,cy)+1));
        for(let y=Y0;y<=Y1;y++)for(let x=X0;x<=X1;x++){const px=x+.5,py=y+.5;
          const e0=((bx-ax)*(py-ay)-(by-ay)*(px-ax))*sg,e1=((cx-bx)*(py-by)-(cy-by)*(px-bx))*sg,e2=((ax-cx)*(py-cy)-(ay-cy)*(px-cx))*sg;
          if(e0/lab<-0.6||e1/lbc<-0.6||e2/lca<-0.6)continue;
          let tv;if(deg)tv=tmax;else{const wa=clamp(e1/Math.abs(area),0,1),wb=clamp(e2/Math.abs(area),0,1),wc=clamp(1-wa-wb,0,1);tv=clamp(wa*A[2]+wb*B[2]+wc*C[2],tmin,tmax);}
          const i=y*gw+x;if(tv>T[i])T[i]=tv;}}
      return {u,v,w,u0,v0,res,gw,gh,T};
    }
    function litIn(M,x,y,z,bias){const pu=x*M.u[0]+y*M.u[1]+z*M.u[2],pv=x*M.v[0]+y*M.v[1]+z*M.v[2],iu=Math.floor((pu-M.u0)*M.res),iv=Math.floor((pv-M.v0)*M.res);
      if(iu<0||iv<0||iu>=M.gw||iv>=M.gh)return 1;return (x*M.w[0]+y*M.w[1]+z*M.w[2])>=M.T[iv*M.gw+iu]-bias?1:0;}
    const SKYD=[[0,0,1]];for(let k=0;k<8;k++){const a=k*Math.PI/4+0.2,c=Math.cos(32*D2R);SKYD.push([Math.cos(a)*c,Math.sin(a)*c,Math.sin(32*D2R)]);}
    function aoMaps(mdl){if(!mdl.ao)mdl.ao=SKYD.map(d=>occMap(mdl.faces,d,mdl.aoRes));return mdl.ao;}
    // world [east, south, up] -> model metres at this camera
    const toLocal=(C,w)=>[w[0]*C.ct-w[1]*C.st,-w[0]*C.st-w[1]*C.ct,w[2]];
    function unproj(sx,sy,z,C){const xr=(sx-C.ox)/C.S,yr=(-(sy-C.oy)/C.S-z*C.ce)/C.se;return [xr*C.ct+yr*C.st,-xr*C.st+yr*C.ct,z];}
    /* C = {ct, st, se, ce, S, ox, oy}: the host's own projection (sx = ox + xr S, sy = oy - (yr se + z ce) S, depth yr ce - z se). */
    function frame(mdl,C,W,H,o){
      o=o||{};const N=W*H,gb={a:new Uint8Array(N),fid:new Int32Array(N).fill(-1),mi:new Uint16Array(N),sb:new Int8Array(N),nx:new Float32Array(N),ny:new Float32Array(N),nz:new Float32Array(N),
        X:new Float32Array(N),Y:new Float32Array(N),Z:new Float32Array(N),d:new Float32Array(N).fill(1e9),tg:new Uint8Array(N),em:new Uint16Array(N)};
      const V=[0,-C.ce,C.se],only=o.only||null;
      for(let fi=0;fi<mdl.faces.length;fi++){const f=mdl.faces[fi];if(only&&!only(f,mdl))continue;const nv=f.v.length;if(nv<3)continue;
        const P=f.v.map(p=>{const xr=p[0]*C.ct-p[1]*C.st,yr=p[0]*C.st+p[1]*C.ct;return [C.ox+xr*C.S,C.oy-(yr*C.se+p[2]*C.ce)*C.S,yr*C.ce-p[2]*C.se,xr,yr,p[2]];});
        let n0=0,n1=0,n2=0;for(let k=0;k<nv;k++){const a=P[k],b=P[(k+1)%nv];n0+=(a[4]-b[4])*(a[5]+b[5]);n1+=(a[5]-b[5])*(a[3]+b[3]);n2+=(a[3]-b[3])*(a[4]+b[4]);}
        let n=nrm([n0,n1,n2]);if(n[0]*V[0]+n[1]*V[1]+n[2]*V[2]<0)n=[-n[0],-n[1],-n[2]];
        const nl=[n[0]*C.ct+n[1]*C.st,-n[0]*C.st+n[1]*C.ct,n[2]],bias=clamp(Math.round(f.b),-2,1),tex=f.tex,uv=f.uv;
        for(let t=1;t+1<nv;t++){const a=P[0],b=P[t],c=P[t+1],area=(b[0]-a[0])*(c[1]-a[1])-(c[0]-a[0])*(b[1]-a[1]);if(Math.abs(area)<1e-7)continue;
          const X0=Math.max(0,Math.floor(Math.min(a[0],b[0],c[0]))),X1=Math.min(W-1,Math.ceil(Math.max(a[0],b[0],c[0]))),Y0=Math.max(0,Math.floor(Math.min(a[1],b[1],c[1]))),Y1=Math.min(H-1,Math.ceil(Math.max(a[1],b[1],c[1])));
          const A=f.v[0],B=f.v[t],Cc=f.v[t+1],ua=uv?uv[0]:null,ub=uv?uv[t]:null,uc=uv?uv[t+1]:null;
          for(let y=Y0;y<=Y1;y++)for(let x=X0;x<=X1;x++){const px=x+.5,py=y+.5,w0=((b[0]-px)*(c[1]-py)-(c[0]-px)*(b[1]-py))/area,w1=((c[0]-px)*(a[1]-py)-(a[0]-px)*(c[1]-py))/area,w2=1-w0-w1;
            if(w0<-0.001||w1<-0.001||w2<-0.001)continue;const d=w0*a[2]+w1*b[2]+w2*c[2],i=y*W+x;if(d-f.db>=gb.d[i])continue;
            let sbv=bias;if(tex&&uv)sbv+=Math.round(tex(w0*ua[0]+w1*ub[0]+w2*uc[0],w0*ua[1]+w1*ub[1]+w2*uc[1]));
            gb.d[i]=d-f.db;gb.a[i]=1;gb.fid[i]=fi;gb.mi[i]=f.mi;gb.sb[i]=sbv<-3?-3:sbv>1?1:sbv;gb.nx[i]=nl[0];gb.ny[i]=nl[1];gb.nz[i]=nl[2];
            gb.X[i]=w0*A[0]+w1*B[0]+w2*Cc[0];gb.Y[i]=w0*A[1]+w1*B[1]+w2*Cc[1];gb.Z[i]=w0*A[2]+w1*B[2]+w2*Cc[2];gb.tg[i]=f.ti;gb.em[i]=f.ei;}}}
      if(!o.keepSpecks)for(let y=0;y<H;y++)for(let x=0;x<W;x++){const i=y*W+x;if(!gb.a[i])continue;
        const k=(x>0&&gb.a[i-1])+(x<W-1&&gb.a[i+1])+(y>0&&gb.a[i-W])+(y<H-1&&gb.a[i+W]);if(!k){gb.a[i]=0;gb.fid[i]=-1;gb.d[i]=1e9;}}
      const fr={mdl,C,W,H,gb,MA:mdl.MA};if(!o.noSky)skyVis(fr);return fr;
    }
    function skyVis(fr){const gb=fr.gb,N=fr.W*fr.H,sv=new Uint8Array(N),AO=aoMaps(fr.mdl);
      for(let i=0;i<N;i++){if(!gb.a[i])continue;const nx=gb.nx[i],ny=gb.ny[i],nz=gb.nz[i],X=gb.X[i]+nx*.03,Y=gb.Y[i]+ny*.03,Z=gb.Z[i]+nz*.03;let acc=0,wt=0;
        for(let k=0;k<SKYD.length;k++){const d=SKYD[k],wk=nx*d[0]+ny*d[1]+nz*d[2];if(wk<=0)continue;wt+=wk;const tn=Math.min(10,Math.sqrt(Math.max(0,1-wk*wk))/Math.max(wk,.05));acc+=wk*litIn(AO[k],X+nx*.05*tn,Y+ny*.05*tn,Z+nz*.05*tn,.06+.07*tn);}   // phase 2: slope-scaled normal offset, so a roof at a grazing sky direction does not stripe itself
        sv[i]=wt>1e-3?Math.round(acc/wt*255):0;}
      fr.sv=sv;}
    function sunMap(mdl,L){const key=L.map(q=>Math.round(q*300)).join(',');let S=mdl.sun.get(key);
      if(!S){S=occMap(mdl.faces,L,mdl.sunRes);if(mdl.sun.size>12)mdl.sun.delete(mdl.sun.keys().next().value);mdl.sun.set(key,S);}return S;}
    // phase 2: the cap was 4, which left grazing roof planes striped (shadow acne); slope-scaled to 12 with a normal offset
    const sunBias=nl=>.03+.045*Math.min(12,Math.sqrt(Math.max(0,1-nl*nl))/Math.max(nl,.05)), sunOff=nl=>.03+.09*(1-nl);
    /* o.emit(name) -> 0..1 glow for each emitter; o.lights [{p:[x,y,z] model m, c:'#hex', I, r}] warm point lamps */
    function relight(fr,sky,o){
      o=o||{};sky=sky||REF_SKY;const gb=fr.gb,W=fr.W,H=fr.H,N=W*H,MA=fr.MA,out=new Uint8ClampedArray(N*4),unlit=sky.grade===false&&!sky.sunI;
      const Lw=sky.sunW||REF_SKY.sunW,Lv=sky.sunV||toView(Lw),sunI=sky.sunI==null?1:sky.sunI,skyI=sky.skyI==null?.6:sky.skyI,expo=sky.expo||1;
      const snow=sky.snow||0,wet=sky.wet||0,fog=sky.fog||0,grade=sky.grade!==false&&o.grade!==false,sunOn=sunI>.01&&Lw[2]>.02;
      const Ll=nrm(toLocal(fr.C,Lw)),SM=sunOn?sunMap(fr.mdl,Ll):null,gB=.13*(sunI*Math.max(0,Lw[2])+.35*skyI)*(1+.8*snow),snowK=Math.round(snow*254),cover=snow>.02;
      let lx=Lv[0],ly=Lv[1];const ll=Math.hypot(lx,ly);if(ll>1e-3){lx/=ll;ly/=ll;}
      const backlit=sunOn&&Lv[2]<-.12&&sunI>.12,kc=h2r(sky.kc||'#ffffff'),ac=h2r(sky.ac||'#000000'),wc=h2r(sky.wash||'#ffffff'),fc=h2r(sky.fogC||'#c3cdce');
      const EM=fr.mdl.EM,elv=new Float32Array(EM.length);if(o.emit)for(let k=1;k<EM.length;k++)elv[k]=clamp(+o.emit(EM[k])||0,0,1);
      const lamps=(o.lights||[]).filter(l=>l&&l.I>.005),lcol=lamps.map(l=>h2r(l.c||'#ffc774')),IB=o.indoor||null;
      const cache=new Map(),P0=MA.length;
      const colour=(pid,bi,lit,fq,spc,wk,wq,li)=>{const key=(((((((pid*6+bi)*2+lit)*5+fq)*3+spc)*3+wk)*4+wq)*16)+(li&15);let c=cache.get(key);if(c)return c;
        const R=pid<P0?MA[pid].ramp:SNOWR,n=R.length,j=clamp(bi,0,n-1),r=R[j].slice(),top=R[n-1];
        if(spc===1)for(let k=0;k<3;k++)r[k]=top[k]+(kc[k]-top[k])*.55;else if(spc===2)for(let k=0;k<3;k++)r[k]=top[k]+(255-top[k])*.6;
        if(grade){const u=j/Math.max(1,n-1),ta=(sky.aa||0)*(1-u)*(1-(sky.amb||0)*.35),tk=(sky.ka||0)*u*(lit?1:.3),wd=wk===2?.30:wk===1?.12:0;
          for(let k=0;k<3;k++){r[k]+=(ac[k]-r[k])*ta*(1-wq*.3);r[k]+=(kc[k]-r[k])*tk;r[k]*=1-wd;if(sky.wa)r[k]+=(wc[k]-r[k])*sky.wa*(1-wq*.35);if(fq)r[k]+=(fc[k]-r[k])*fq*.17;}}
        if(wq&&lcol.length){const L=lcol[li%lcol.length],t=[0,.2,.36,.5][wq];for(let k=0;k<3;k++)r[k]+=(L[k]-r[k])*t;}
        c=[clamp(Math.round(r[0]),0,255),clamp(Math.round(r[1]),0,255),clamp(Math.round(r[2]),0,255)];cache.set(key,c);return c;};
      const E=new Float32Array(N),EL=new Float32Array(N),LI=new Uint8Array(N),LT=new Uint8Array(N);
      for(let i=0;i<N;i++){if(!gb.a[i])continue;const nx=gb.nx[i],ny=gb.ny[i],nz=gb.nz[i],X=gb.X[i],Y=gb.Y[i],Z=gb.Z[i],sv=fr.sv[i]/255,nl=nx*Ll[0]+ny*Ll[1]+nz*Ll[2];
        const so=nl>0?sunOff(nl):0,vis=SM&&nl>0?litIn(SM,X+nx*so,Y+ny*so,Z+nz*so,sunBias(nl)):0,sun=sunI*(nl>0?Math.pow(nl,1.25)*Math.min(1,nl/.14):0)*vis;
        const ind=IB&&X>=IB[0]&&X<=IB[1]&&Y>=IB[2]&&Y<=IB[3]&&Z>-.02,sK=ind?IB[4]:1,kK=ind?IB[5]:1;   // o.indoor: inside a room the sun is a window's, the sky a soft fill
        E[i]=(.36*skyI*kK*(.30+.70*sv)*(.45+.55*Math.max(0,nz))+sun*sK+gB*(.5-.5*nz)*(.5+.5*sv))*expo;LT[i]=sun*sK>.22?1:0;
        if(lamps.length){let s=0,best=0,bi=0;for(let k=0;k<lamps.length;k++){const l=lamps[k],dx=l.p[0]-X,dy=l.p[1]-Y,dz=l.p[2]-Z,d2=dx*dx+dy*dy+dz*dz,r=l.r||3;if(d2>r*r)continue;
            const d=Math.sqrt(d2)||1e-3,ndl=(nx*dx+ny*dy+nz*dz)/d;if(ndl<=0.02)continue;const q=1-d/r,v=l.I*(.35+.65*ndl)*q*q;s+=v;if(v>best){best=v;bi=k;}}
          EL[i]=s;LI[i]=bi;}}
      for(let y=0;y<H;y++)for(let x=0;x<W;x++){const i=y*W+x;if(!gb.a[i])continue;
        const m=MA[gb.mi[i]],nz=gb.nz[i],Z=gb.Z[i],ek=gb.em[i],el=ek?elv[ek]:0;let o4=i*4,c;
        if(el>.04){const f=fr.mdl.faces[gb.fid[i]],R=f.fire?FIRE:WARM,j=clamp(1+Math.round(el*2.6)+gb.sb[i]+(gb.sb[i]>0?1:0),0,5);c=R[j];
          if(fog>.01){const k2=fog*.25;c=[c[0]+(fc[0]-c[0])*k2,c[1]+(fc[1]-c[1])*k2,c[2]+(fc[2]-c[2])*k2];}
          out[o4]=c[0];out[o4+1]=c[1];out[o4+2]=c[2];out[o4+3]=255;continue;}
        const tot=E[i]+EL[i],db=unlit?0:faceBand(tot),lit=LT[i];let pid=gb.mi[i],bi=m.base+gb.sb[i]+db,spc=0,wk=0,wq=0;
        const dd=gb.d[i];let crev=false;
        if(x>0&&gb.a[i-1]&&dd-gb.d[i-1]>.16)crev=true;else if(x<W-1&&gb.a[i+1]&&dd-gb.d[i+1]>.16)crev=true;else if(y>0&&gb.a[i-W]&&dd-gb.d[i-W]>.16)crev=true;else if(y<H-1&&gb.a[i+W]&&dd-gb.d[i+W]>.16)crev=true;
        if(crev&&!unlit)bi-=1;
        if(!unlit&&wet>.02){const wp=wet*m.por;wk=wp>.21?2:wp>.06?1:0;if(wet>.3&&lit&&nz>.5&&bi>=m.base+1&&hsh(x,y,7,17)<wet*m.gloss*.35)spc=2;}
        if(cover&&m.hold>0&&nz>.3&&fr.sv[i]>46){const e=.60*vn3(gb.X[i]*1.6,gb.Y[i]*1.6,Z*1.6,17)+.34*vn3(gb.X[i]*5.5,gb.Y[i]*5.5,Z*5.5,29)+(1-nz)*1.4+(1-fr.sv[i]/255)*.7+(1-m.hold)*.6;
          const k2=(e+.12)/1.3;if(k2<1&&Math.ceil(k2*254)<=snowK){pid=P0;bi=3+db+(nz>.9?1:0)-(fr.sv[i]<115?1:0);wk=0;spc=0;}}
        if(backlit&&pid!==P0&&nz>.25){const edge=(x===0||!gb.a[i-1])||(x===W-1||!gb.a[i+1])||(y===0||!gb.a[i-W]);
          if(edge){const sxn=gb.nx[i]*fr.C.ct-gb.ny[i]*fr.C.st;if(sxn*lx<-.05||y===0||!gb.a[i-W]){spc=1;bi=m.n-1;}}}
        if(EL[i]>.02){const wf=EL[i]/(tot+1e-6);wq=wf>.62?3:wf>.36?2:wf>.14?1:0;}
        let fq=0;if(fog>.01){const hf=clamp(Z/9,0,1);fq=Math.round(clamp(fog*(.62+.38*(1-hf)),0,1)*4);}
        c=colour(pid,Math.max(0,bi),lit,fq,spc,wk,wq,LI[i]);out[o4]=c[0];out[o4+1]=c[1];out[o4+2]=c[2];out[o4+3]=255;}
      if(o.outline){const k=h2r(KEYLINE),a=gb.a;for(let y=0;y<H;y++)for(let x=0;x<W;x++){const i=y*W+x;if(a[i])continue;
        if((x>0&&a[i-1])||(x<W-1&&a[i+1])||(y>0&&a[i-W])||(y<H-1&&a[i+W])){out[i*4]=k[0];out[i*4+1]=k[1];out[i*4+2]=k[2];out[i*4+3]=255;}}}
      return out;
    }
    /* shade level on the plane z at model point (x,y): 3 full sun shadow (2 under a thin sun), 2 partial, 1 canopy (sky
       straight down blocked: stays under overcast), 0 open */
    function shadeAt(fr,sky,x,y,z){sky=sky||REF_SKY;const sunI=sky.sunI==null?1:sky.sunI,Lw=sky.sunW||REF_SKY.sunW;let L=0;
      if(sunI>.04&&Lw[2]>.03){const SM=sunMap(fr.mdl,nrm(toLocal(fr.C,Lw)));let b=0;for(const [dx,dy]of [[-.05,-.05],[.05,-.05],[-.05,.05],[.05,.05]])b+=litIn(SM,x+dx,y+dy,z+.02,.05)?0:1;
        if(b>=3)L=sunI>.25?3:2;else if(b>=1)L=2;}
      if(L<1&&!litIn(aoMaps(fr.mdl)[0],x,y,z+.02,.1))L=1;return L;}
    // the ground plane under the cell (same canvas as the frame): o.z (default 0), o.box = [x0,x1,y0,y1] model m to test
    function castShadow(fr,sky,o){o=o||{};sky=sky||REF_SKY;const z=o.z||0,W=fr.W,H=fr.H,lv=new Uint8Array(W*H),C=fr.C,Lw=sky.sunW||REF_SKY.sunW;
      let bx=o.box;if(!bx){let x0=1e9,x1=-1e9,y0=1e9,y1=-1e9,zt=0;for(const f of fr.mdl.faces)for(const p of f.v){if(p[0]<x0)x0=p[0];if(p[0]>x1)x1=p[0];if(p[1]<y0)y0=p[1];if(p[1]>y1)y1=p[1];if(p[2]>zt)zt=p[2];}
        const Ll=nrm(toLocal(C,Lw)),k=Ll[2]>.03?zt/Math.max(Ll[2],.14):0,ex=Math.abs(Ll[0])*k+.5,ey=Math.abs(Ll[1])*k+.5;bx=[x0-ex,x1+ex,y0-ey,y1+ey];}
      for(let yy=0;yy<H;yy++)for(let xx=0;xx<W;xx++){const p=unproj(xx+.5,yy+.5,z,C);if(p[0]<bx[0]||p[0]>bx[1]||p[1]<bx[2]||p[1]>bx[3])continue;lv[yy*W+xx]=shadeAt(fr,sky,p[0],p[1],z);}
      return {x0:0,y0:0,w:W,h:H,lv,z};}
    function view(fr,ch,sky,o){
      if(!ch||ch==='lit')return relight(fr,sky||REF_SKY,o);if(ch==='unlit')return relight(fr,UNLIT_SKY,o);
      const gb=fr.gb,N=fr.W*fr.H,out=new Uint8ClampedArray(N*4);let d0=1e9,d1=-1e9;if(ch==='depth')for(let i=0;i<N;i++)if(gb.a[i]){d0=Math.min(d0,gb.d[i]);d1=Math.max(d1,gb.d[i]);}
      const SM=ch==='sun'?sunMap(fr.mdl,nrm(toLocal(fr.C,(sky||REF_SKY).sunW))):null;
      for(let i=0;i<N;i++){if(!gb.a[i])continue;let r=128,g=128,b=128;
        if(ch==='normal'){const C=fr.C,xr=gb.nx[i]*C.ct-gb.ny[i]*C.st,yr=gb.nx[i]*C.st+gb.ny[i]*C.ct;r=(xr*.5+.5)*255;g=(yr*.5+.5)*255;b=(gb.nz[i]*.5+.5)*255;}
        else if(ch==='ao')r=g=b=fr.sv[i];
        else if(ch==='sun'){const nl=gb.nx[i]*SM.w[0]*0+gb.nx[i]*nrm(toLocal(fr.C,(sky||REF_SKY).sunW))[0]+gb.ny[i]*nrm(toLocal(fr.C,(sky||REF_SKY).sunW))[1]+gb.nz[i]*nrm(toLocal(fr.C,(sky||REF_SKY).sunW))[2];
          const v=nl>0&&litIn(SM,gb.X[i]+gb.nx[i]*.03,gb.Y[i]+gb.ny[i]*.03,gb.Z[i]+gb.nz[i]*.03,sunBias(nl))?nl:0;r=24+v*231;g=22+v*200;b=30+v*120;}
        else if(ch==='height')r=g=b=clamp(gb.Z[i]/14,0,1)*255;
        else if(ch==='depth')r=g=b=255-(gb.d[i]-d0)/Math.max(1e-3,d1-d0)*255;
        else if(ch==='tags'){const t=gb.tg[i];if(t){r=60+180*hsh(t,1,3,5);g=60+180*hsh(t,2,3,5);b=60+180*hsh(t,3,3,5);}else r=g=b=40;}
        else if(ch==='emit'){const e=gb.em[i];if(e){r=235;g=190*hsh(e,4,1,2)+40;b=60;}else r=g=b=30;}
        else if(ch==='mat'){const m=fr.MA[gb.mi[i]],c=m.ramp[clamp(m.base+gb.sb[i],0,m.n-1)];r=c[0];g=c[1];b=c[2];}
        out[i*4]=r;out[i*4+1]=g;out[i*4+2]=b;out[i*4+3]=255;}
      return out;}
    // how dark it is for lamps: 0 in daylight, 1 by moonlight (drives window glow and the lanterns)
    function lampNeed(sky){sky=sky||REF_SKY;if(sky.need!=null)return sky.need;const L=sky.sunW||REF_SKY.sunW,s0=sky.key==='moon'?0:(sky.sunI0!=null?sky.sunI0:(sky.sunI||0));
      const illum=s0*Math.max(.05,L[2])+(sky.skyI||0)*.5*(1-.35*(sky.cloud||0));return clamp((.52-illum)/.34,0,1);}
    // without WeatherSky loaded, a clock alone still says how dark it is (full dark 20:00-06:00, daylight 09:30-16:30)
    const clockNeed=t=>clamp((Math.abs(((+t%24)+24)%24-13)-3.5)/3.5,0,1);
    function skyOf(o){o=o||{};if(o.sky)return o.sky;const WS=root.WeatherSky;
      if(o.time!=null&&WS)return WS.at({time:o.time,cloud:o.cloud,rain:o.rain,fog:o.fog,snow:o.snow,wind:o.wind});
      if(o.time!=null){const k=clockNeed(o.time);return Object.assign({},k>=0.5?NIGHT_SKY:REF_SKY,{time:+o.time,need:+k.toFixed(3)});}
      if(o.night)return NIGHT_SKY;return REF_SKY;}
    return {REF_SKY,NIGHT_SKY,UNLIT_SKY,KEYLINE,WARM,FIRE,SKYD,faceBand,model,frame,relight,castShadow,shadeAt,view,toLocal,unproj,occMap,litIn,lampNeed,skyOf,dirOf,toView};
  })();

  root.CoastalPass={enabled:true,version:'3.2.0',PALETTE,TUNING,ROOMS,apply,cottagePlan,cottageStair,light};
})(typeof globalThis!=='undefined'?globalThis:window);
