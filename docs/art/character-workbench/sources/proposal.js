/* Art study, not a production replacement. Rig-7 bone IDs and metre space are preserved.
   Build a revised Fisher bind mesh; skin it using the original clips. */
(function(root){
 const T={headScale:[.88,.94,.87],bodyWidth:1.13,bodyDepth:1.15,legFullness:1.16,bootFullness:1.22};
 const ramps={
  over:['#13393e','#205b5e','#36867d','#69a48e'],overD:['#142f38','#22484e','#316961','#4b8771'],
  shirt:['#26384e','#405970','#6e8797','#acb8b2'],sleeve:['#26384e','#405970','#6e8797','#acb8b2'],
  skin:['#744134','#aa6548','#d29765','#efbe86'],hair:['#4f3924','#79572b','#ae873c','#d5b359'],
  boot:['#131e2b','#263545','#435563','#667987'],sole:['#111b28','#1c2b3a','#334859','#516576'],
  brass:['#684626','#91672d','#c39343','#e4be67'],face:['#1c2930'],lip:['#774637'],ear:['#af684a']
 };
 function create(mesh){
  const F=[],sk=mesh.skeletonWorld,IX=Object.fromEntries(sk.map((b,i)=>[b.id,i]));
  const pos=id=>sk[IX[id]].pos;
  const face=(v,mat,bone,part)=>F.push({v,mat,b:0,db:0,part,bone:v.map(()=>[[IX[bone],1]])});
  function box(c,s,mat,bone,part){const p=(x,y,z)=>[c[0]+s[0]*x,c[1]+s[1]*y,c[2]+s[2]*z];
   const quads=[[[1,1,1],[-1,1,1],[-1,-1,1],[1,-1,1]],[[1,-1,-1],[-1,-1,-1],[-1,1,-1],[1,1,-1]],[[1,1,-1],[-1,1,-1],[-1,1,1],[1,1,1]],[[-1,-1,-1],[1,-1,-1],[1,-1,1],[-1,-1,1]],[[1,-1,-1],[1,1,-1],[1,1,1],[1,-1,1]],[[-1,1,-1],[-1,-1,-1],[-1,-1,1],[-1,1,1]]];
   quads.forEach(q=>face(q.map(x=>p(...x)),mat,bone,part));
  }
  function lathe(c,rows,mat,bone,part,seg=10){
   const ring=rows.map(([z,rx,ry])=>Array.from({length:seg},(_,i)=>{const a=(i+.5)/seg*Math.PI*2;return[c[0]+Math.cos(a)*rx,c[1]+Math.sin(a)*ry,c[2]+z];}));
   for(let k=0;k<ring.length-1;k++)for(let i=0;i<seg;i++){const j=(i+1)%seg;face([ring[k][i],ring[k][j],ring[k+1][j],ring[k+1][i]],mat,bone,part);}
   face(ring[0].slice().reverse(),mat,bone,part);face(ring.at(-1),mat,bone,part);
  }
  const hc=pos('head');
  const headTransform=([x,y,z])=>{let dx=x-hc[0],dy=y-hc[1],dz=z-hc[2];
   // Compact crown; preserve a broader jaw rather than the old pointed chin.
   if(dz<-.13)dx*=1+Math.min(.28,(-dz-.13)*3.4);
   return[hc[0]+dx*T.headScale[0],hc[1]+dy*T.headScale[1],hc[2]+dz*T.headScale[2]-.025];
  };
  for(const f of mesh.bindMesh){
   if(['inseam','torso','pelvis','neck'].includes(f.part)||f.part.startsWith('hand_')||(f.part==='head'&&f.mat==='hair'))continue;
   const n=JSON.parse(JSON.stringify(f));
   if(n.part==='head'){
    n.v=n.v.map(headTransform);
    if(n.mat==='hair') n.v=n.v.map(([x,y,z])=>[x,y,Math.min(z,1.478)+.018*(x+.06)]);
    if(n.mat==='stub') n.mat='skin';
   } else if(/^(thigh|shin)_/.test(n.part)){
    const center=n.part.endsWith('_L')?-.0819:.0819;
    n.v=n.v.map(([x,y,z])=>[center+(x-center)*T.legFullness,y*1.1,z]);
   } else if(/^(boot|foot)/.test(n.part)){
    const center=n.part.endsWith('_L')?-.075668:.075668;
    n.v=n.v.map(([x,y,z])=>[center+(x-center)*T.bootFullness,y*1.1,z]);n.mat=n.mat==='bootL'?'boot':n.mat;
   } else if(/^(upper|fore)_/.test(n.part)){
    const center=n.part.endsWith('_L')?-.184:.184;
    n.v=n.v.map(([x,y,z])=>[center+(x-center)*1.20,y*1.1,z]);
    if(n.part.startsWith('upper'))n.mat='sleeve';
   }
   F.push(n);
  }
  // Connected garment masses with large, uninterrupted value areas.
  lathe([0,0,0],[[.565,.128,.087],[.63,.15,.103],[.76,.157,.11]],'over','pelvis','pelvis');
  lathe([0,0,0],[[.68,.153,.108],[.80,.167,.119],[.92,.192,.119],[1.00,.193,.099],[1.051,.108,.070]],'shirt','torso','torso');
  lathe([0,0,0],[[.675,.156,.112],[.747,.169,.123],[.79,.169,.124]],'over','torso','torso');
  // Curved front bib: shirt remains visible on either side.
  const rows=[[.777,.122,.128],[.865,.121,.132],[.966,.105,.119]];
  for(let j=0;j<2;j++)for(let i=0;i<4;i++){
   const pt=(r,k)=>{const [z,rx,ry]=rows[r],a=(-.78+k*.39);return[Math.sin(a)*rx,Math.cos(a)*ry,z];};
   face([pt(j,i),pt(j,i+1),pt(j+1,i+1),pt(j+1,i)],'over','torso','bib');
  }
  for(const s of [-1,1]){
   box([s*.075,.104,1.00],[.019,.013,.049],'over','torso','strap_front');
   box([s*.075,.115,.956],[.017,.008,.014],'brass','torso','buckle');
   box([s*.075,.003,1.047],[.019,.107,.012],'over','torso','strap_shoulder');
   face([[s*.029,-.125,.785],[s*.055,-.127,.785],[s*.094,-.087,1.047],[s*.056,-.087,1.047]],'over','torso','strap_back');
  }
  box([.005,.135,.87],[.040,.006,.028],'overD','torso','bib_pocket');
  box([.005,.143,.895],[.041,.003,.004],'over','torso','bib_pocket_welt');
  // One repaired knee, one readable patch: intentional wear, no all-over speckle.
  box([-.08,.087,.349],[.036,.004,.040],'overD','knee_L','knee_patch');
  lathe([0,0,0],[[1.035,.060,.055],[1.10,.055,.049],[1.17,.047,.043]],'skin','neck','neck',8);
  lathe([0,0,0],[[1.037,.069,.067],[1.063,.069,.067]],'shirt','torso','collar',8);
  // Real head-attached face geometry, no camera-facing stamp.
  // One swept hair shell, replacing the onion crown and its projecting top plug.
  const hairRings=[];
  const skull=[[-.15,.12,.086],[-.078,.14,.10],[-.004,.157,.11],[.048,.16,.11],[.098,.146,.099],[.144,.098,.068],[.174,.038,.028],[.203,0,0]];
  const rad=(z,j)=>{for(let i=1;i<skull.length;i++)if(z<=skull[i][0]){const p=skull[i-1],q=skull[i],t=(z-p[0])/(q[0]-p[0]);return p[j]+(q[j]-p[j])*t;}return 0;};
  for(const t of [0,.12,.24,.36,.48,.60,.72,.82,.90,.96,1]){
   hairRings.push(Array.from({length:16},(_,i)=>{const a=i/16*Math.PI*2,front=Math.sin(a);
    const base=front>=0?.012+.057*front+.022*Math.cos(a):-.02+.107*front;
    const z=base+(.203-base)*t;
    return headTransform([hc[0]+Math.cos(a)*(rad(z,1)+.017),hc[1]+Math.sin(a)*(rad(z,2)+.017),hc[2]+z]);}));
  }
  for(let j=0;j<hairRings.length-1;j++)for(let i=0;i<16;i++){const k=(i+1)%16;face([hairRings[j][i],hairRings[j][k],hairRings[j+1][k],hairRings[j+1][i]],'hair','head','hair');}
  face(hairRings.at(-1),'hair','head','hair');
  const addHeadBox=(c,s,mat,part)=>{let begin=F.length;box(c,s,mat,'head',part);for(let i=begin;i<F.length;i++)F[i].v=F[i].v.map(headTransform);};
  for(const s of [-1,1]){
   addHeadBox([s*.065,.124,hc[2]-.040],[.020,.010,.024],'face','eye');
   addHeadBox([s*.064,.112,hc[2]+.017],[.027,.008,.008],'hair','brow');
   const begin=F.length;lathe([s*.153,0,hc[2]-.06],[[-.040,.015,.018],[0,.023,.025],[.036,.012,.018]],'ear','head','ear',6);for(let i=begin;i<F.length;i++)F[i].v=F[i].v.map(headTransform);
  }
  const nose=[[-.023,.104,hc[2]-.009],[.023,.104,hc[2]-.009],[.029,.102,hc[2]-.092],[-.029,.102,hc[2]-.092],[0,.157,hc[2]-.072]].map(headTransform);
  for(const q of [[0,1,4],[1,2,4],[2,3,4],[3,0,4]])face(q.map(i=>nose[i]),'skin','head','nose');
  addHeadBox([0,.096,hc[2]-.127],[.027,.007,.005],'lip','mouth');
  // Palm and thumb masses retain the existing hand-bone attachment.
  for(const side of ['L','R']){
   const c=pos('hand_'+side),s=side==='L'?-1:1;
   lathe(c,[[-.047,.033,.025],[-.025,.045,.032],[.025,.044,.03],[.047,.029,.026]],'skin','hand_'+side,'hand_'+side,8);
   lathe([c[0]-s*.039,c[1]+.008,c[2]+.006],[[-.026,.014,.018],[0,.019,.022],[.024,.012,.015]],'skin','hand_'+side,'thumb_'+side,6);
  }
  return F;
 }
 root.CharacterArtStudy={create,T,ramps};
})(globalThis);
