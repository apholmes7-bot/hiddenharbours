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
  };
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
  function exterior(kind,faces,M,b,opts){
    materials(M,b);setMat(M,'roof','slate',b);setMat(M,'iron','iron',b);setMat(M,'stone','stone',b);setMat(M,'plinth','stone',b);
    setMat(M,'dress','stone',b);setMat(M,'trim',kind==='manor'?'join':'ivory',b);setMat(M,'dormer',kind==='manor'?'join':'ivory',b);
    setMat(M,'door',kind==='manor'?'oak':'join',b);setMat(M,'shutter','join',b);
    if(M.body){const old=M.body.ramp;M.body={ramp:old.map((c,i)=>mix(c,PALETTE.stone[Math.min(5,i)],kind==='manor'?.35:.15))};}
    for(const f of faces){if(f.tex&&['body','roof','plinth'].includes(f.mat)){const t=f.tex;f.tex=(u,v)=>t(u,v)*.62;}}
    const hw=b.Wd/2,hl=b.Ln/2,p=TUNING.pipeWidth,g=TUNING.gutterWidth;
    // Gutter lengths stop at the existing roof perimeter; drains are outside corner trim.
    for(const x of [-hw-.34,hw+.34])box(faces,x-g/2,x+g/2,-hl-.20,hl+.20,b.eaveZ-.15,b.eaveZ-.02,'cp_iron');
    for(const x of [-hw-.14,hw+.14])box(faces,x-p/2,x+p/2,-hl-.12,-hl-.12+p,.18,b.eaveZ-.10,'cp_iron');
    const e=kind==='house'?root.HouseIso.entrance(opts):{axis:'y',x:0,y:hl+(b.accent==='tower'?1.75:0),z:b.fH};
    if(e.axis==='y'){
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
      const seen={};for(const f of faces.slice()){
        if(f.mat!=='glass'||f.v.length!==4)continue;
        const xs=f.v.map(v=>v[0]),ys=f.v.map(v=>v[1]),zs=f.v.map(v=>v[2]);
        const z0=Math.min(...zs),z1=Math.max(...zs),x0=Math.min(...xs),x1=Math.max(...xs),y0=Math.min(...ys),y1=Math.max(...ys);
        if(z0>2.0||z1-z0<.9)continue;
        const ax=x1-x0<.001?'x':y1-y0<.001?'y':null;if(!ax)continue;
        const c=ax==='x'?(y0+y1)/2:(x0+x1)/2,pl=ax==='x'?x0:y0,key=ax+'|'+pl.toFixed(2)+'|'+c.toFixed(2);if(seen[key])continue;seen[key]=1;
        const a=f.v[0],v=f.v[1],w=f.v[2],normal=ax==='x'?(v[1]-a[1])*(w[2]-a[2])-(v[2]-a[2])*(w[1]-a[1]):(v[2]-a[2])*(w[0]-a[0])-(v[0]-a[0])*(w[2]-a[2]);
        const n=-Math.sign(normal)||1,plane=pl+n*.055,half=(ax==='x'?y1-y0:x1-x0)/2;
        for(const side of [-1,1]){const lo=c+side*(half+.24)-.15,hi=lo+.30;
          if(ax==='x')box(faces,plane-.025,plane+.025,lo,hi,z0,z1,'cp_join');else box(faces,lo,hi,plane-.025,plane+.025,z0,z1,'cp_join');
          for(let j=1;j<5;j++){const zz=z0+(z1-z0)*j/5;
            if(ax==='x')box(faces,plane-.035,plane+.035,lo+.025,hi-.025,zz,zz+.025,'cp_iron');else box(faces,lo+.025,hi-.025,plane-.035,plane+.035,zz,zz+.025,'cp_iron');
          }
        }
      }
      // Firewood and a rain barrel on the rear service side, away from the entry.
      emit(faces,M,'barrel',{wood:'driftwood',weather:.5},{x:-hw-.58,y:-hl+1.0,z:0,face:'N'});
      for(let row=0;row<3;row++)for(let i=0;i<4-row;i++){
        const y=-hl+2.15+i*.27+row*.13,z=.1+row*.21;
        box(faces,-hw-.83,-hw-.22,y,y+.21,z,z+.19,'cp_oak');
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
    const stair=cottageStair(b),nd=Math.min(2,b.dividers|0),dividers=[];
    for(let i=0;i<nd;i++)dividers.push({y:-hl+b.wt+(b.Ln-2*b.wt)*(i+1)/(nd+1),gap:stair?1.20:(i%2?-1:1)*hw*.34,w:Math.min(1.15,b.Wd*.30)});
    const routes=[],pts=[{x:0,y:hl-.25}];
    for(const d of dividers.slice().reverse())pts.push({x:d.gap,y:d.y+.7},{x:d.gap,y:d.y-.7});
    pts.push({x:stair?1.2:0,y:-hl+1.65});
    for(let i=1;i<pts.length;i++){const a=pts[i-1],c=pts[i],r=TUNING.routeHalfWidth;
      // A rectilinear route: change x first, then y. Rectangles are explicit for validation.
      routes.push({x0:Math.min(a.x,c.x)-r,x1:Math.max(a.x,c.x)+r,y0:a.y-r,y1:a.y+r});
      routes.push({x0:c.x-r,x1:c.x+r,y0:Math.min(a.y,c.y)-r,y1:Math.max(a.y,c.y)+r});
    }
    const blockers=[],props=[],rejected=[];
    if(b.hearth)blockers.push({kind:'hearth',x0:-.94,x1:.94,y0:-hl,y1:-hl+1.02});
    for(const d of dividers){for(const r of [{x0:-hw,x1:d.gap-d.w/2,y0:d.y-.12,y1:d.y+.12},
      {x0:d.gap+d.w/2,x1:hw,y0:d.y-.12,y1:d.y+.12}])for(const q of minus(r,stair?{x0:stair.x0-.90,x1:stair.x1+.08,y0:stair.y0-1.02,y1:stair.y1+1.02}:null))blockers.push(Object.assign({kind:'partition'},q));}
    if(stair){blockers.push(Object.assign({kind:b.storey==='upper'?'well':'stair'},b.storey==='upper'?stair.opening:{x0:stair.x0-.08,x1:stair.x1+.08,y0:stair.y0,y1:stair.y1}));
      routes.push(stair.topLanding,stair.bottomLanding,{x0:stair.x0-.85,x1:stair.x0-.08,y0:-hl+1.02,y1:hl-.45});}
    function add(id,name,face,positions,options,verb){
      const fp=root.PropIso.footprint(name,options),rot=face==='E'||face==='W',w=rot?fp.d:fp.w,d=rot?fp.w:fp.d,h=root.PropIso.height(name,options);
      // Search spare wall-side slots when the staircase displaces a preferred furnishing.
      if(!['chairA','chairB'].includes(id))for(let y=box0.y0+d/2+.04;y<box0.y1-d/2;y+=.28)for(const x of [box0.x0+w/2+.04,box0.x1-w/2-.04])positions.push([x,y]);
      for(const [x,y]of positions){const rect={x0:x-w/2,x1:x+w/2,y0:y-d/2,y1:y+d/2};
        if(rect.x0<box0.x0||rect.x1>box0.x1||rect.y0<box0.y0||rect.y1>box0.y1)continue;
        if(blockers.some(a=>intersects(a,rect,TUNING.propGap))||routes.some(a=>intersects(a,rect)))continue;
        if(b.prof){const zAt=x=>{for(let i=1;i<b.prof.length;i++){const a=b.prof[i-1],v=b.prof[i];if(x>=a[0]&&x<=v[0])return a[1]+(v[1]-a[1])*(x-a[0])/(v[0]-a[0]);}return 0;};if(Math.min(zAt(rect.x0),zAt(rect.x1))<h+.12)continue;}
        const p={id,name,face,x,y,z:b.fZ||0,options,verb,rect,height:h};props.push(p);blockers.push(Object.assign({kind:name,id},rect));return p;
      }
      rejected.push({id,name,reason:'No slot clear of walls, props, route and roof clearance'});return null;
    }
    const left=-hw+.95,right=hw-.95,rear=-hl+.85,front=hl-1.45;
    if(b.storey==='upper'){
      add('bed','bed','S',[[left,0],[left,1.2],[right,0]],{variant:1,wood:'pine',fabric:'blue',fabric2:'cream'},'sleep');
      add('chest','seaChest','N',[[0,-hl+1.0],[right,1.8],[left,-hl+1.0]],{wood:'oak'},'store');
      add('desk','writingDesk','E',[[left,-hl+1.3],[right,hl-1.5]],{wood:'oak'},'desk');
      add('chair','chair','W',[[left+1.05,-hl+1.3],[right-1.0,hl-1.5]],{wood:'pine'},'sit');
    }else{
      add('range','stove','S',[[left,rear],[right,rear]],{wood:'oak'},'cook');
      add('sink','sink','W',[[right,-hl+2.2],[right,-hl+.85]],{paint:'sage',worktop:'slate'},'wash_dishes');
      add('cupboard','cupboard','S',[[hw*.48,rear],[left,-.65],[left,hl-.8]],{paint:'sage',wood:'oak'},'store');
      const dining=add('table','table','N',[[left,0],[-.9,-hl+2.35],[-1.4,-hl+2.7],[-.65,-1.6]],{len:0,wood:'oak'},'dine');
      if(dining){add('chairA','chair','S',[[dining.x,dining.y-.75]],{wood:'pine'},'sit');add('chairB','chair','N',[[dining.x,dining.y+.75]],{wood:'pine'},'sit');}
      add('bed','bed','S',[[left,front],[left,hl-1.8]],{variant:1,wood:'pine',fabric:'blue',fabric2:'cream'},'sleep');
      add('dresser','dresser','S',[[left+.8,nd?dividers[nd-1].y+.6:hl-3.0],[.4,nd?dividers[nd-1].y+.6:hl-3.0],[left,hl-.65]],{wood:'oak'},'store');
      add('armchair','armchair','W',[[right,hl-1.2],[right,hl-2.5]],{fabric:'red',fabric2:'cream',wood:'walnut'},'sit');
      add('seaChest','seaChest','W',[[right,nd?dividers[nd-1].y+.95:hl-3],[right,hl-2.4]],{wood:'oak'},'store');
    }
    // Conservative 2D grid traversal, with blockers expanded by the player's radius.
    const radius=TUNING.approachRadius,step=.12,nx=Math.floor((box0.x1-box0.x0)/step)+1,ny=Math.floor((box0.y1-box0.y0)/step)+1;
    const free=(x,y)=>x-radius>=box0.x0&&x+radius<=box0.x1&&y-radius>=box0.y0&&y+radius<=box0.y1&&!blockers.some(v=>intersects({x0:x-radius,x1:x+radius,y0:y-radius,y1:y+radius},v));
    const open=new Uint8Array(nx*ny),visited=new Uint8Array(nx*ny),queue=[];
    for(let j=0;j<ny;j++)for(let i=0;i<nx;i++)if(free(box0.x0+i*step,box0.y0+j*step))open[j*nx+i]=1;
    const seed=stair&&b.storey==='upper'?stair.top:{x:0,y:hl-.75};
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
  function cottage(faces,M,b){
    if(b.fam!=='house')return faces;
    materials(M,b);setMat(M,'plaster','linen',b);setMat(M,'paper','green',b);setMat(M,'trim','ivory',b);setMat(M,'wood','join',b);setMat(M,'stone','stone',b);
    const hw=b.Wd/2,hl=b.Ln/2,plan=cottagePlan(b),out=[];
    for(const f of faces){
      if(f.layer==='floor'||f.layer==='stair'||f.layer?.startsWith('dv'))continue;
      if(plan.stair&&f.layer==='beam'&&b.storey!=='upper')continue;
      if(f.tex&&['plaster','paper','wood'].includes(f.mat)){const t=f.tex;f.tex=(u,v)=>t(u,v)*.48;}
      out.push(f);
    }
    const bands=b.storey==='upper'?[[ -hl,hl,'oak']]:[[-hl,0,'tile'],[0,hl,'oak']];
    for(const p of plan.blockers.filter(p=>p.kind==='partition')){const start=out.length;box(out,p.x0,p.x1,p.y0,p.y1,0,TUNING.cutawayHeight,'cp_linen');for(let i=start;i<out.length;i++)out[i].layer='partition';}
    for(const [ya,yb,pal]of bands)for(const r of minus({x0:-hw,x1:hw,y0:ya,y1:yb},b.storey==='upper'?plan.stair?.opening:null)){const {x0,x1,y0,y1}=r;const f=face([[x0,y0,0],[x1,y0,0],[x1,y1,0],[x0,y1,0]],'cp_'+pal,-2.2,0,'floor');
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
    if(kind==='cottage')return cottage(faces,M,b);
    return exterior(kind,faces,M,b,opts||{});
  }
  root.CoastalPass={enabled:true,version:'3.1.0',PALETTE,TUNING,ROOMS,apply,cottagePlan,cottageStair};
})(typeof globalThis!=='undefined'?globalThis:window);
