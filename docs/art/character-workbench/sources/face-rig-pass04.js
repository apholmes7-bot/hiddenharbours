/* Hidden Harbours — minor head refinement 04. Original HeadIso provides customization geometry.
   Face marks are a head-local surface material, not camera-facing cards or displaced eye blocks.
   Coordinates are metres. A shared front-surface UV layout keeps every facial landmark centered. */
(function(root){
 const C={headScale:.97,pixelPhaseX:.5,eyeX:.071,eyeZ:.053,eyeW:.065,eyeH:.035,browZ:.102,
  mouthZ:-.108,mouthStepX:.029,mouthStepZ:.030,uvBottom:-.225,uvHeight:.46,
  blinkMin:2.4,blinkSpan:2.0,blinkDuration:.19};
 const EXPRESSIONS={
  neutral:{brow:0,raise:0,lid:0,mouth:'neutral'},smile:{brow:0,raise:0,lid:.12,mouth:'smile',cheek:1},
  grin:{brow:0,raise:.15,lid:.24,mouth:'grin',cheek:1},frown:{brow:1,raise:0,lid:0,mouth:'frown'},
  worry:{brow:-1,raise:.75,lid:0,mouth:'worry'},oh:{brow:-1,raise:1,lid:0,mouth:'oh'},
  grit:{brow:1,raise:0,lid:.28,mouth:'grit'},weary:{brow:-.4,raise:0,lid:.45,mouth:'weary'}
 };
 const BROWS={natural:{tilt:0,lift:0},flat:{tilt:0,lift:-.018},raised:{tilt:0,lift:.018},angled:{tilt:1,lift:0},worried:{tilt:-1,lift:0},thick:{tilt:0,lift:0,thick:1},none:null};
 const AUTO={strike:'grit',reel:'grit',stagger:'oh',run:'grit',land:'grin',dig:'grit'};
 const clamp=(v,a,b)=>Math.min(b,Math.max(a,v));
 const hash=n=>{const v=Math.sin(n*127.1+311.7)*43758.5453;return v-Math.floor(v);};
 function life(t,options){
  const o=options||{},expr=o.expr||AUTO[o.anim]||'neutral',base=EXPRESSIONS[expr]||EXPRESSIONS.neutral,seed=o.seed||0;
  let elapsed=0,index=0,blink=0;
  while(index<4096){const gap=C.blinkMin+C.blinkSpan*hash(index*11.3+seed),dur=C.blinkDuration;
   if(t<elapsed+gap)break;
   if(t<elapsed+gap+dur){const u=(t-elapsed-gap)/dur;blink=u<.30?u/.30:u<.64?1:(1-u)/.36;break;}
   const twice=hash(index*17.7+seed)>.78,next=elapsed+gap+dur+.09;
   if(twice&&t<next+dur*.8){if(t>=next){const u=(t-next)/(dur*.8);blink=u<.30?u/.30:u<.64?1:(1-u)/.36;}break;}
   elapsed+=gap+dur+(twice?.09+dur*.8:0);index++;
  }
  let gaze=o.gaze;
  if(!gaze){let a=0,k=0;while(k<4096){const hold=.72+1.05*hash(k*3.7+seed);if(a+hold>t)break;a+=hold;k++;}
   gaze=[[0,0],[.8,0],[0,0],[-.8,0],[0,.5],[0,0]][Math.floor(hash(k*5.3+seed)*6)];}
  const mouth=o.talk?['closed','ah','ee','closed','oh','ah','closed'][Math.floor(t*6.2)%7]:base.mouth;
  if(o.anim==='sleep')return {...base,gaze:[0,0],lid:1,mouth:'neutral',expr};
  return {...base,gaze,lid:o.lid!=null?o.lid:Math.max(base.lid,blink),mouth,expr};
 }
 function poseState(anim,u,options){
  const o=options||{},expr=o.expr||AUTO[anim]||'neutral';
  const st=root.HeadIso.loopLook(anim,u,expr,!!o.talk,o.loop,o.seed);
  if(o.talk)st.mouth=life(((o.loop||0)+u)*1.02,{expr,talk:true,seed:o.seed}).mouth;
  if(anim==='sleep'){st.lid=1;st.gaze=[0,0];st.mouth='neutral';}
  return {...st,expr};
 }
 function uvFor(v,hc,scale){
  const x=(v[0]-hc[0])/scale,y=(v[1]-hc[1])/scale,z=(v[2]-hc[2])/scale;
  return [.5+x/.38,(z-C.uvBottom)/C.uvHeight];
 }
 function createHead(build,hc){
  const H=root.HeadIso,b={...build},center=hc||[0,0,0],scale=C.headScale*(build.headSize||1);
  b.jaw=b.jaw||(b.sex==='m'?1.12:.92);
  let F=H.facesOf(center,b,{gaze:[0,0],lid:0,brow:0,mouth:'neutral'},1,scale).map(f=>({...f,v:f.v.map(p=>p.slice()),part:'head'}));
  // Original customization geometry stays available; the un-hatted mop gets a clean swept shell.
  if(b.hairStyle==='mop'&&(!b.hat||b.hat==='none')){
   F=F.filter(f=>f.mat!=='hair');const prof=H.skullProf(1,1,b.jaw||1.1),rings=[];
   for(const t of [0,.12,.24,.36,.48,.60,.72,.82,.91,1])rings.push(Array.from({length:16},(_,i)=>{
    const a=i*Math.PI/8,front=Math.sin(a),edge=front>0?.105+.035*front+.023*Math.cos(a):-.025+.09*front;
    const z=edge+(.202-edge)*t,rx=H.profR(prof,Math.min(z,.174),1),ry=H.profR(prof,Math.min(z,.174),2),cap=z>.174?Math.max(.06,(.208-z)/.034):1;
    return[center[0]+Math.cos(a)*(rx+.019)*cap*scale,center[1]+Math.sin(a)*(ry+.018)*cap*scale,center[2]+z*scale];
   }));
   for(let j=0;j<rings.length-1;j++)for(let i=0;i<16;i++){const k=(i+1)%16;F.push({v:[rings[j][i],rings[j][k],rings[j+1][k],rings[j+1][i]],mat:'hair',b:0,part:'head'});}
   F.push({v:rings.at(-1),mat:'hair',b:0,part:'head'});
  }
  for(const f of F){
   if(['skin','stub','beard','beardD'].includes(f.mat)&&f.v.reduce((s,v)=>s+v[1]-center[1],0)>0){
    f.uv=f.v.map(v=>uvFor(v,center,scale));
    f.faceSurface=true;
   }
  }
  const add=(v,mat)=>F.push({v:v.map(p=>[center[0]+p[0]*scale,center[1]+p[1]*scale,center[2]+p[2]*scale]),mat,b:0,part:'head'});
  // Small real nose and ears preserve readable profiles without the old spectacle-like eye blocks.
  const p=[[-.016,.106,.012],[.016,.106,.012],[.021,.109,-.044],[-.021,.109,-.044],[0,.141,-.026]];
  for(const [i,ids] of [[0,[0,1,4]],[1,[1,2,4]],[2,[2,3,4]],[3,[3,0,4]]])add(ids.map(i=>p[i]),i===2?'noseShadow':i===0?'noseLight':'skin');
  for(const side of [-1,1]){const x=side*.157;
   for(const [z,rx,ry,dz] of [[-.063,.022,.026,.053]]){
    const A=[x,0,z+dz],B=[x+side*rx,.002,z+.020],D=[x+side*rx,-.003,z-.023],E=[x,-.004,z-dz],front=[x,.030,z];
    add([A,B,front],'skin');add([B,D,front],'skin');add([D,E,front],'skin');add([E,A,front],'skin');
   }
  }
  return F;
 }
 // Palette indices for an atlas/material: 0 transparent, 1 lash, 2 sclera, 3 iris,
 // 4 brow, 5 lip, 6 mouth interior, 7 tooth, 8 lower lip, 9 lid skin.
 function sample(u,v,state,build){
  const z=C.uvBottom+v*C.uvHeight;
  // The same physical x and z anchors are sampled on the curved front skin at every heading.
  const x=(u-.5)*.38,st=state||EXPRESSIONS.neutral,b=build||{},gx=clamp((st.gaze||[0,0])[0],-1,1),gy=clamp((st.gaze||[0,0])[1],-1,1);
  for(const side of [-1,1]){
   const ex=side*C.eyeX,dx=x-ex;
   const shape=b.eyeShape||'round',width=C.eyeW*(shape==='wide'?1.15:shape==='sharp'?.96:1),half=width/2;
   let height=C.eyeH*(shape==='wide'?1.18:shape==='narrow'?.66:shape==='droop'?.85:1);
   const socketCurve=(Math.sqrt(Math.max(0,1-(x/.16)**2))-Math.sqrt(1-(C.eyeX/.16)**2))*.108*Math.tan(40*Math.PI/180);
   const lid=clamp(st.lid||0,0,1),eyeZ=C.eyeZ+socketCurve+(shape==='droop'?-side*dx*.20:shape==='sharp'?side*dx*.22:0);
   const open=height*(1-lid),bottom=eyeZ-height*.70,top=bottom+open;
   if(Math.abs(dx)<=half){
    if(lid>.87){if(Math.abs(z-(bottom+height*.25))<.010)return 1;}
    else if(z>=bottom&&z<=top+.010){
     if(z>top-.003)return 1;
     // Rounded eye corners remain only skin; the sockets never move with pupil gaze.
     if(Math.abs(dx)>half*.83&&z<bottom+.010)return 0;
     if(st.cheek&&side*dx>0&&z<bottom+.008)return 9;
     const pupilX=gx*(half-.016),pupilZ=bottom+open*.49+gy*.007;
     if(Math.abs(dx-pupilX)<.011&&Math.abs(z-pupilZ)<.014)return 1;
     if(Math.abs(dx-pupilX)<.015&&Math.abs(z-pupilZ)<.018)return 3;
     return 2;
    }
   }
   const brow=BROWS[b.browShape||'natural'];
   if(brow&&Math.abs(dx)<half+.005){
    const tilt=clamp((st.brow||0)+brow.tilt,-1.5,1.5),y=C.browZ+socketCurve+brow.lift+(st.raise||0)*.014+side*dx*tilt*.58;
    if(Math.abs(z-y)<(brow.thick?.013:.008))return 4;
   }
  }
  // Distinct mouth silhouettes. Width stays centered on x=0 across expressions and speech.
  const mx=x,jaw=b.jaw||(b.sex==='m'?1.12:.92),jT=(C.mouthZ+.146)/.068;
  const mouthRx=.108*jaw+(.140-.108*jaw)*jT,mouthRy=.084+.016*jT;
  const mouthCurve=mouthRy*(Math.sqrt(Math.max(0,1-(mx/mouthRx)**2))-1)*Math.tan(40*Math.PI/180);
  const mz=z-C.mouthZ-mouthCurve,mouth=st.mouth||'neutral',lip=.009,w=.048;
  if(Math.abs(mx)<.070&&Math.abs(mz)<.060){
   if(mouth==='neutral'||mouth==='closed'){if(Math.abs(mx)<.034&&Math.abs(mz)<lip)return 5;}
   else if(mouth==='smile'){const line=Math.abs(mx)>.021?.024:-.010;if(Math.abs(mx)<.052&&Math.abs(mz-line)<.012)return 5;}
   else if(mouth==='frown'){const line=.016-.029*Math.pow(Math.abs(mx)/w,1.3);if(Math.abs(mx)<w&&Math.abs(mz-line)<lip)return 5;}
   else if(mouth==='worry'){const line=.009-Math.abs(mx)*.29+mx*.13;if(Math.abs(mx)<.039&&Math.abs(mz-line)<lip)return 5;}
   else if(mouth==='weary'){if(Math.abs(mx)<.034&&Math.abs(mz+mx*.14)<lip)return 5;}
   else if(mouth==='grin'||mouth==='grit'||mouth==='ee'){
    const halfW=mouth==='grit'?.046:.052,halfH=mouth==='grit'?.016:.026;
    if(Math.abs(mx)<halfW&&Math.abs(mz)<halfH){if(Math.abs(mx)>halfW-.008||Math.abs(mz)>halfH-.007)return 5;return mz>-.007?7:6;}
   } else if(mouth==='oh'||mouth==='ah'){
    const rx=mouth==='oh'?.024:.037,rz=mouth==='oh'?.034:.043,rr=(mx/rx)**2+(mz/rz)**2;
    if(rr<1){if(rr>.56)return 5;return 6;}
   }
  }
  return 0;
 }
 function colours(build){const H=root.HeadIso,M=H.makeMats(build).MATS,s=M.skin.ramp,h=M.hair.ramp;
  const rgb=hex=>[1,3,5].map(i=>parseInt(hex.slice(i,i+2),16)),mid=rgb(s[3]);const dark=mid[0]*.2126+mid[1]*.7152+mid[2]*.0722<105;
  const iris=rgb(M.iris.ramp[0]),ink=rgb('#243038'),irisShade='#'+iris.map((v,i)=>Math.round(v*.4+ink[i]*.6).toString(16).padStart(2,'0')).join('');
  return [null,'#243038',dark?'#e4d6b8':'#ead8b4',irisShade,h[0],s[0],'#492b2c','#e7d4ab',s[2],s[3]];
 }
 root.CharacterHeadStudy={C,EXPRESSIONS,BROWS,AUTO,life,poseState,createHead,sample,colours};
})(globalThis);
