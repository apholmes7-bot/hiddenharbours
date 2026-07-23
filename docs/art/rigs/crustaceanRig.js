/* Hidden Harbours — CRUSTACEAN rig (lobster + rock crab), scalable rebuild.
   The old lobster/crab art is fixed 48x48 plots — resampling it breaks the pixel art.
   This rig REPLOTS the same animals parametrically at any scale/heading (like the fish
   loft): geometry lives in metres, pixels re-derive per render, so tray keepers, deck
   crawlers, tote fills and trophy catches all come off one plotter. Palettes verbatim
   from lobsterRig / rockCrabRig (nothing new invented). Top-down 3/4 read (y x0.85),
   upper-left key, no AA, 1px keyline.
   Cell 64x64. POSES: walk 4f (legs paddle, tail sways) · rear · defend (claws up) ·
   held 2f (dangled by the back — pivot = THE GRIP, pins to CharacterIso hand anchors;
   claws droop, lobster tail curls under). pivot (32,36) = ground centre for everything
   except held, which uses hpivot (32,12). ang = heading in RADIANS (continuous — fills
   scatter it). hold(kind,scale) -> {mass,hands} like FishIso.
   Exposes globalThis.Crustacean = { W,H,pivot,hpivot,KINDS,POSES,render,hold }. */
(function (root) {
  const W=64, H=64, PX=32, PY=36, HPX=32, HPY=12, SQ=0.85;
  const OUT = { lobster:'#2a0f0b', crab:'#171a14' };
  const PAL = {
    lobster: {
      CARA:{mid:'#c33a29',hi:'#e5604a',sh:'#8f2519',dp:'#5c150e'},
      CLAW:{mid:'#d04434',hi:'#f06e55',sh:'#9a2b1d',dp:'#651710'},
      LEG:{mid:'#a83122',hi:'#c5493a',sh:'#6c1b11',dp:'#6c1b11'},
      RUST:{mid:'#e08a3e',hi:'#f4ab62',sh:'#a85c22',dp:'#a85c22'},
      CREAM:{mid:'#f0e2c4',hi:'#fbf1da',sh:'#c2a877',dp:'#c2a877'},
      EYE:{mid:'#1a0705',hi:'#1a0705',sh:'#1a0705',dp:'#1a0705'},
      SEP:{mid:'#5c150e',hi:'#5c150e',sh:'#3d0d08',dp:'#3d0d08'},
    },
    crab: {
      CARA:{mid:'#b25e3e',hi:'#cf7a52',sh:'#8a4530',dp:'#5f2c20'},
      CLAW:{mid:'#b25e3e',hi:'#cf7a52',sh:'#8a4530',dp:'#5f2c20'},
      LEG:{mid:'#8f4630',hi:'#ad5a3c',sh:'#5a281d',dp:'#5a281d'},
      RUST:{mid:'#ad5a3c',hi:'#cf7a52',sh:'#5a281d',dp:'#5a281d'},
      CREAM:{mid:'#cdb890',hi:'#ece0c8',sh:'#9c7f57',dp:'#9c7f57'},
      EYE:{mid:'#241512',hi:'#241512',sh:'#241512',dp:'#241512'},
      SEP:{mid:'#5f2c20',hi:'#5f2c20',sh:'#40190f',dp:'#40190f'},
    },
  };
  const KINDS=['lobster','crab'];
  const POSES={ walk:{n:4,ms:140}, rear:{n:1,ms:400}, defend:{n:1,ms:400}, held:{n:2,ms:420} };

  function newBuf(){ return { key:new Array(W*H).fill(''), mat:new Array(W*H).fill(null) }; }
  const idx=(x,y)=>y*W+x, inb=(x,y)=>x>=0&&x<W&&y>=0&&y<H;
  function put(b,x,y,m,k){ x=Math.round(x); y=Math.round(y); if(!inb(x,y))return;
    if(m!=='SEP'&&b.mat[idx(x,y)]==='SEP'){ b.mat[idx(x,y)]=m; b.key[idx(x,y)]=k||'mid'; return; }
    if(m==='SEP'&&b.mat[idx(x,y)]) return;
    b.key[idx(x,y)]=k||'mid'; b.mat[idx(x,y)]=m; }
  // frame helpers: u along the heading, v lateral (metres) -> screen px
  function frameFns(cx0, cy0, ang, s){
    const d=[Math.cos(ang),Math.sin(ang)], n=[-Math.sin(ang),Math.cos(ang)];
    const pt=(u,v)=>[cx0+(d[0]*u+n[0]*v)*s, cy0+(d[1]*u+n[1]*v)*s*SQ];
    return { pt };
  }
  function blob(b,pt,u,v,ru,rv,m){                 // axis-aligned-in-body ellipse
    const s2=0.5;
    for(let du=-ru;du<=ru;du+=0.008) for(let dv=-rv;dv<=rv;dv+=0.008){
      if((du*du)/(ru*ru)+(dv*dv)/(rv*rv)>1) continue;
      const p=pt(u+du,v+dv); put(b,p[0],p[1],m);
    }
  }
  function strip(b,pt,u0,v0,u1,v1,r0,r1,m){        // tapered strip in body coords
    const steps=Math.ceil(Math.hypot(u1-u0,v1-v0)*260)+2;
    for(let i=0;i<=steps;i++){ const t=i/steps;
      const u=u0+(u1-u0)*t, v=v0+(v1-v0)*t, r=r0+(r1-r0)*t;
      for(let dv=-r;dv<=r;dv+=0.008) for(let du=-r;du<=r;du+=0.012){
        if(du*du+dv*dv>r*r) continue;
        const p=pt(u+du,v+dv); put(b,p[0],p[1],m);
      }
    }
  }
  function dot(b,pt,u,v,m,k){ const p=pt(u,v); put(b,p[0],p[1],m,k); }

  function drawLobster(b, cx0, cy0, ang, s, pose, f){
    const {pt}=frameFns(cx0,cy0,ang,s);
    const ph=f/POSES.walk.n*2*Math.PI;
    const held=pose==='held', defend=pose==='defend', rear=pose==='rear';
    const sway= pose==='walk' ? Math.sin(ph)*0.014 : (held ? (f?0.012:-0.012) : 0);
    const curl= held?0.05:0;
    const tailEnd= held?-0.20:-0.26;
    const vt=(u)=> u<-0.05 ? ((-u-0.05)/0.21)*(sway) + ((-u-0.05)/0.21)*curl : 0;
    // separation silhouette — wide enough to leave a real dark rim at every scale
    strip(b,pt,tailEnd-0.025,vt(tailEnd),-0.04,0,0.050,0.072,'SEP');
    blob(b,pt,0.02,0,0.100,0.078,'SEP');
    // claws (shoulder -> arm -> claw)
    const spread= defend?0.115 : held?0.045 : 0.095;
    const reach = defend?0.135 : held?0.105 : 0.155;
    const cru   = defend?0.052 : 0.045;
    for(const sd of [-1,1]){
      const bob= pose==='walk' ? Math.sin(ph+sd)*0.006 : 0;
      strip(b,pt,0.08,sd*0.05,reach*0.72,sd*spread*0.85,0.020,0.024,'SEP');
      blob(b,pt,reach+bob,sd*spread,cru+0.026,0.056,'SEP');
    }
    // abdomen + carapace (slimmer than the sep rim)
    strip(b,pt,tailEnd,vt(tailEnd),-0.05,0,0.028,0.048,'CARA');
    blob(b,pt,0.02,0,0.075,0.055,'CARA');
    // tail fan
    for(const fv of [-0.028,0,0.028]) blob(b,pt,tailEnd-0.012,vt(tailEnd)+fv,0.024,0.018,'CARA');
    dot(b,pt,tailEnd-0.035,vt(tailEnd),'RUST');
    // segment seams
    for(const su of [-0.10,-0.16,-0.21]){ if(su<tailEnd) continue;
      for(let dv=-0.03;dv<=0.03;dv+=0.01) dot(b,pt,su,vt(su)+dv,'CARA','sh'); }
    // legs
    for(const sd of [-1,1]) for(let i=0;i<4;i++){
      const bu=0.045-i*0.036;
      const la=sd*(held?0.35+i*0.12 : 0.85+i*0.32);
      const step= pose==='walk' ? Math.sin(ph+i*1.4+sd*0.5)*0.018 : 0;
      const tu=bu+Math.cos(la)* -0.02 + step + (held?-0.05:0);
      const tv=sd*0.055 + Math.sin(la)*sd*0.045;
      strip(b,pt,bu,sd*0.05,tu,tv,0.007,0.005,'LEG');
      dot(b,pt,tu,tv,'RUST','sh');
    }
    // claws proper — thin arms, claws clear of the shell
    for(const sd of [-1,1]){
      const bob= pose==='walk' ? Math.sin(ph+sd)*0.006 : 0;
      const lift= rear&&sd>0 ? 0.02 : 0;
      strip(b,pt,0.08,sd*0.05,reach*0.72,sd*spread*0.85,0.010,0.013,'CLAW');
      blob(b,pt,reach+bob+lift,sd*spread,cru,0.032,'CLAW');
      dot(b,pt,reach+bob+lift+cru*0.7,sd*spread,'CREAM','hi');
      if(defend) dot(b,pt,reach+cru*0.5,sd*(spread+0.02),'CLAW','dp');   // gape
      dot(b,pt,0.10,sd*0.058,'RUST');                                    // band pip
    }
    // antennae — thin rust arcs sweeping back, clear of the claws
    for(const sd of [-1,1]) for(let i=0;i<=8;i++){ const t=i/8;
      dot(b,pt,0.085-t*0.28,sd*(0.028+t*0.065),'RUST', t>0.5?'sh':'mid'); }
    dot(b,pt,0.088,0.020,'EYE'); dot(b,pt,0.088,-0.020,'EYE');
    dot(b,pt,0.01,-0.03,'CARA','hi'); dot(b,pt,-0.01,0.02,'CARA','hi');  // shell glints
  }
  function drawCrab(b, cx0, cy0, ang, s, pose, f){
    const {pt}=frameFns(cx0,cy0,ang,s);
    const ph=f/POSES.walk.n*2*Math.PI;
    const held=pose==='held', defend=pose==='defend', rear=pose==='rear';
    blob(b,pt,0,0,0.105,0.080,'SEP');
    for(const sd of [-1,1]){
      const cu= defend?0.115:0.095, cv=sd*(defend?0.055:0.085);
      blob(b,pt,cu,cv,0.060,0.050,'SEP');
    }
    blob(b,pt,0,0,0.085,0.058,'CARA');
    dot(b,pt,0.05,0.028,'CARA','dp'); dot(b,pt,0.05,-0.028,'CARA','dp'); // front notches
    for(const sd of [-1,1]) for(let i=0;i<4;i++){
      const bu=0.03-i*0.036;
      const la= held? sd*(0.25+i*0.10) : sd*(1.05+i*0.28);
      const step= pose==='walk' ? Math.sin(ph+i*1.6+(sd>0?0:2.2))*0.02 : 0;
      const mu=bu+step-(held?0.04:0.01), mv=sd*(0.065+Math.abs(Math.sin(la))*0.05);
      strip(b,pt,bu,sd*0.06,mu,mv,0.007,0.006,'LEG');
      strip(b,pt,mu,mv,mu-(held?0.03:0.025),mv+sd*(held?0.008:0.028),0.006,0.004,'LEG');
      dot(b,pt,mu-(held?0.03:0.025),mv+sd*(held?0.008:0.028),'CREAM','sh');
    }
    for(const sd of [-1,1]){
      const lift= (rear&&sd>0)||defend;
      const cu= held?0.075 : lift?0.115:0.095;
      const cv=sd*( held?0.05 : lift?0.055:0.085);
      strip(b,pt,0.05,sd*0.05,cu*0.8,cv*0.85,0.012,0.015,'CLAW');
      blob(b,pt,cu,cv,lift?0.05:0.042,lift?0.038:0.032,'CLAW');
      dot(b,pt,cu+0.03,cv,'CREAM','hi');
      if(defend) dot(b,pt,cu+0.02,cv+sd*0.018,'CLAW','dp');
    }
    dot(b,pt,0.075,0.018,'EYE'); dot(b,pt,0.075,-0.018,'EYE');
    dot(b,pt,-0.02,-0.025,'CARA','hi');
  }
  function shade(b, cx0, cy0){
    for(let y=0;y<H;y++)for(let x=0;x<W;x++){
      const i=idx(x,y); if(b.key[i]!=='mid'||!b.mat[i]||b.mat[i]==='SEP') continue;
      const Lv=-((x-cx0)*0.7+(y-cy0)*0.6);
      b.key[i]= Lv>4?'hi': Lv>-4?'mid': Lv>-11?'sh':'dp';
    }
  }
  function outline(b){
    const add=[];
    for(let y=0;y<H;y++)for(let x=0;x<W;x++){ if(b.key[idx(x,y)])continue;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]) if(inb(x+dx,y+dy)&&b.key[idx(x+dx,y+dy)]&&b.mat[idx(x+dx,y+dy)]!=='__o'){ add.push([x,y]); break; } }
    for(const [x,y] of add){ b.key[idx(x,y)]='out'; b.mat[idx(x,y)]='__o'; }
  }
  const hex2=(h)=>[parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)];
  function toRGBA(b, kind){
    const P=PAL[kind], out=new Uint8ClampedArray(W*H*4);
    for(let i=0;i<W*H;i++){ const k=b.key[i]; if(!k){ out[i*4+3]=0; continue; }
      let hex;
      if(b.mat[i]==='__o'||k==='out') hex=OUT[kind];
      else { const mm=P[b.mat[i]]||P.CARA; hex= k==='hi'?mm.hi : k==='sh'?mm.sh : k==='dp'?mm.dp : mm.mid; }
      const [r,g,bl]=hex2(hex); out[i*4]=r; out[i*4+1]=g; out[i*4+2]=bl; out[i*4+3]=255; }
    return out;
  }
  function render(kind, opts){
    opts=opts||{};
    const k=KINDS.indexOf(kind)>=0?kind:'lobster';
    const pose=POSES[opts.pose]?opts.pose:'walk';
    const f=((opts.frame||0)%POSES[pose].n+POSES[pose].n)%POSES[pose].n;
    const scale=opts.scale||1, s=32*scale;
    const b=newBuf();
    let cx0=PX, cy0=PY, ang=opts.ang!=null?opts.ang:0.4;
    if(pose==='held'){ ang=Math.PI/2; cx0=HPX; cy0=HPY+0.04*s*SQ; }   // dangles head-down from the grip
    (k==='lobster'?drawLobster:drawCrab)(b, cx0, cy0, ang, s, pose, f);
    shade(b, cx0, cy0); outline(b);
    return toRGBA(b, k);
  }
  function hold(kind, scale){
    const base= kind==='crab'?0.4:0.7, s=scale||1;
    const mass=base*s*s*s;
    return { mass:Math.round(mass*10)/10, hands: mass>=2.2?2:1 };
  }
  root.Crustacean = { W, H, pivot:{x:PX,y:PY}, hpivot:{x:HPX,y:HPY}, KINDS, POSES, render, hold };
})(typeof globalThis!=='undefined'?globalThis:window);
