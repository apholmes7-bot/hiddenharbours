/* Hidden Harbours — eye refinement 06. Original HeadIso provides customization geometry.
   Face marks are a head-local surface material, not camera-facing cards or displaced eye blocks.
   Coordinates are metres. A shared front-surface UV layout keeps every facial landmark centered. */
(function(root){
 const C={headScale:.97,pixelPhaseX:.5,eyeX:.067,eyeZ:.050,eyeW:.062,eyeH:.060,browZ:.103,
  eyeTop:.38,lashH:.009,closedLidH:.010,pupilW:.018,pupilH:.024,irisW:.0255,irisH:.033,irisLower:.012,gazeX:.008,gazeY:.007,
  mouthZ:-.100,mouthStepX:.029,mouthStepZ:.030,uvBottom:-.225,uvHeight:.46,
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
  // Deliberate chin planes rather than a long pinched point. The taper acts on the
  // same lower-face coordinates in skin, beard and hanging hair, preserving contact.
  // Eye line, crown, ear attachments and every skeleton/head anchor stay fixed.
  const chinLift=b.sex==='f'?.022:b.age==='child'?.018:.014;
  const chinWidth=b.sex==='f'?.32:.20;
  for(const f of F)for(const p of f.v){
    const z=(p[2]-center[2])/scale,t=clamp((-z-.110)/.102,0,1),smooth=t*t*(3-2*t);
    p[0]=center[0]+(p[0]-center[0])*(1+chinWidth*smooth);
    p[2]+=chinLift*smooth*scale;
  }
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
  const add=(v,mat,bias)=>F.push({v:v.map(p=>[center[0]+p[0]*scale,center[1]+p[1]*scale,center[2]+p[2]*scale]),mat,b:bias||0,part:'head'});
  // Small real nose and ears preserve readable profiles without the old spectacle-like eye blocks.
  const p=[[-.016,.106,.012],[.016,.106,.012],[.021,.109,-.044],[-.021,.109,-.044],[0,.141,-.026]];
  // The whole nose is skin: the light already separates its planes. The away-side plane alone landed on
 // the cheek's own ramp step at the front heading, so it carries a half-step darkening to part from it.
 for(const [i,ids] of [[0,[0,1,4]],[1,[1,2,4]],[2,[2,3,4]],[3,[3,0,4]]])add(ids.map(i=>p[i]),'skin',i===1?-.5:0);
  for(const side of [-1,1]){const x=side*.157;
   for(const [z,rx,ry,dz] of [[-.063,.022,.026,.053]]){
    const A=[x,0,z+dz],B=[x+side*rx,.002,z+.020],D=[x+side*rx,-.003,z-.023],E=[x,-.004,z-dz],front=[x,.030,z];
    add([A,B,front],'skin');add([B,D,front],'skin');add([D,E,front],'skin');add([E,A,front],'skin');
   }
  }
  return F;
 }
 // Palette indices for an atlas/material: 0 transparent, 1 lash, 2 sclera, 3 iris,
 // 4 brow, 5 lip, 6 mouth interior, 7 tooth, 8 lower lip, 9 lid skin, 10 pupil.
 function sample(u,v,state,build){
  const z=C.uvBottom+v*C.uvHeight;
  // The same physical x and z anchors are sampled on the curved front skin at every heading.
  const x=(u-.5)*.38,st=state||EXPRESSIONS.neutral,b=build||{},gx=clamp((st.gaze||[0,0])[0],-1,1),gy=clamp((st.gaze||[0,0])[1],-1,1);
  for(const side of [-1,1]){
   const ex=side*C.eyeX,dx=x-ex;
   const shape=b.eyeShape||'round',half=C.eyeW*.5*(shape==='wide'?1.12:shape==='sharp'?.96:1);
   const height=C.eyeH*(shape==='wide'?1.12:shape==='narrow'?.78:shape==='droop'?.90:1);
   const socketCurve=(Math.sqrt(Math.max(0,1-(x/.16)**2))-Math.sqrt(1-(C.eyeX/.16)**2))*.108*Math.tan(40*Math.PI/180);
   const lid=clamp(st.lid||0,0,1),eyeZ=C.eyeZ+socketCurve+(shape==='droop'?-side*dx*.12:shape==='sharp'?side*dx*.14:0);
   // Rounded corners and a gently capped upper lid shape the opening. The iris
   // sits in its centre; neither outer half is forced dark, keeping neutral gaze focused.
   const nx=dx/half,curve=Math.sqrt(Math.max(0,1-nx*nx)),closedZ=eyeZ-height*.18+height*.12*nx*nx;
   // Both rims meet the same closed curve, avoiding a disappearing socket then
   // a vertically displaced dash in the last blink frame.
   const bottom=(eyeZ-height*.48*curve)*(1-lid)+closedZ*lid;
   const top=(eyeZ+height*Math.min(C.eyeTop,.52*curve))*(1-lid)+closedZ*lid;
   if(Math.abs(dx)<half){
    if(lid>.87){if(Math.abs(z-closedZ)<C.closedLidH)return 1;}
    else if(z>=bottom&&z<=top){
     if(z>top-C.lashH&&side*dx>-.020)return 1;
     const pupilX=gx*C.gazeX,pupilZ=eyeZ+gy*C.gazeY;
     if(((dx-pupilX)/C.pupilW)**2+((z-pupilZ)/C.pupilH)**2<1)return 10;
     // The lower iris colour carries the opening beneath the pupil. This keeps
     // an oblique low-density eye connected without filling its upper light corners.
     if(((dx-pupilX)/C.irisW)**2+((z-pupilZ+C.irisLower)/C.irisH)**2<1)return 3;
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
  const mz=z-C.mouthZ-mouthCurve,mouth=st.mouth||'neutral',lip=.012,w=.048;
  if(Math.abs(mx)<.070&&Math.abs(mz)<.060){
   if(mouth==='neutral'||mouth==='closed'){const line=Math.abs(mx)>.026?.003:0;if(Math.abs(mx)<.037&&Math.abs(mz-line)<.014)return 5;}
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
  const iris=rgb(M.iris.ramp[0]),ink=rgb('#292627'),irisShade='#'+iris.map((v,i)=>Math.round(v*.38+ink[i]*.62).toString(16).padStart(2,'0')).join('');
  const lip=dark?s[2]:s[0];
  return [null,'#292627',dark?'#c9baa0':'#d9c8ab',irisShade,h[0],lip,'#492b2c','#e7d4ab',s[2],s[3],'#292627'];
 }
 root.CharacterHeadStudy={C,EXPRESSIONS,BROWS,AUTO,life,poseState,createHead,sample,colours};
})(globalThis);
