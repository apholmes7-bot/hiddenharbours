/* buildingLifecycleRig.js — CONSTRUCTION PHASES + DERELICTION, shared by every iso building rig.

   This is not a rig. It is a PASS that runs between a rig's build(b) and its paint(): it takes the
   finished building's own face list and returns the same building at an earlier or a later point in
   its life. Because it reads the real faces, EVERY build config is covered by construction — the
   frame that goes up is the frame of the building the player actually chose, gable triangles, wings,
   dormers and all, and the ruin left behind is that same building's footprint.

   PHASES (6 + finished)
     site        cleared pad, corner stakes + string lines, first material drop
     foundation  piers / stem wall / slab, sills laid
     frame       studs, plates, corner posts, gable studs — walls framed, open to the sky
     rafters     roof framed: rafters, ridge board, collar ties. Still open.
     sheathed    sheathing on walls + roof deck; rough openings cut; tarps; staging up
     cladding    roof on, siding going up from the sill — a clad line partway up the wall
     finished    untouched — the rig's own output

   DECAY (4 + sound)
     neglected   paint gone chalky, moss, shingle patches lost to bare sheathing, weeds at the sill
     abandoned   windows boarded or gone black, doors off, siding gaps, roof patchy, vines, junk
     collapsing  a roof slope stove in with rafters showing, sagging ridge, walls leaning, a wall
                 section down in a heap of planks, holes right through
     ruin        roof gone, walls broken off ragged at knee-to-chest height, foundation, weeds, debris
     burnt:true  a modifier on any decay — chars every ramp, strips the roof, sooty stub timbers

   USE, in a rig's render(), immediately after faces=build(b):
     const LC=root.BuildingLifecycle;
     if (LC && LC.active(opts)) { const r=LC.apply(faces, MATS, b, opts); faces=r.faces; b=r.b; }
   apply() adds its own material keys to MATS in place and returns a cloned build for post().

   Faces are the shared record {v,mat,b,db,uv,tex,flat}; world units are metres, z up, ground z=0,
   32 px = 1 m — identical across wharfBuildingRig / houseIsoRig / shopfrontRig. */
(function (root) {
  const PHASES = ['site','foundation','frame','rafters','sheathed','cladding','finished'];
  const PHASE_LABEL = {
    site:'Staked', foundation:'Foundation', frame:'Framed', rafters:'Roof framed',
    sheathed:'Sheathed', cladding:'Siding up', finished:'Finished' };
  const PHASE_NOTE = {
    site:'pad cleared, corners staked, line strung, first drop of material',
    foundation:'piers and stem wall poured, sills bolted down',
    frame:'studs, plates and corner posts up — open to the sky',
    rafters:'rafters and ridge board on, collar ties in',
    sheathed:'walls and roof deck sheathed, openings cut, staging up',
    cladding:'roof on and watertight, siding going up from the sill',
    finished:'trimmed out and painted' };
  const DECAY = ['sound','neglected','abandoned','collapsing','ruin'];
  const DECAY_LABEL = { sound:'Sound', neglected:'Neglected', abandoned:'Abandoned',
    collapsing:'Collapsing', ruin:'Ruin' };
  const DECAY_NOTE = {
    sound:'kept up', neglected:'chalky paint, moss, a few courses of shingle gone',
    abandoned:'boarded windows, doors off, siding gaps, weeds to the sill',
    collapsing:'roof stove in, ridge sagging, a wall down',
    ruin:'walls broken off ragged, foundation and weeds' };
  const DW = { sound:0, neglected:0.28, abandoned:0.55, collapsing:0.8, ruin:1 };

  // ---- material ramps this pass introduces (KTC family, dark -> light) --------
  const LUMBER = ['#6e5a3a','#877049','#a2895c','#bba173','#d0b98e','#e0cda8'];
  const SHEATH = ['#5a4c39','#6d5d46','#827155','#978567','#ab9a7c','#bcac91'];
  const DECK   = ['#2a2722','#37332c','#464137','#565044','#666052','#777061'];
  const CONC   = ['#4a4842','#5f5d55','#77746a','#8f8b7f','#a4a094','#b8b3a6'];
  const DIRT   = ['#3b3227','#4a4031','#5b4f3c','#6d6049','#7f7157','#918266'];
  const GRAVEL = ['#454542','#56564f','#6a695f','#7d7c71','#909083','#a3a395'];
  const TARP   = ['#1f3340','#2a4657','#375a6e','#467086','#57869d','#6b9db4'];
  const WEED   = ['#2a3323','#38432c','#485438','#586646','#6a7855','#7d8b66'];
  const MOSS   = ['#2c3527','#3a4531','#48553c','#576549','#667657','#768767'];
  const RUSTC  = ['#3a1c10','#4f2614','#6a3620','#84462b','#9c5b3a','#b2724c'];
  const CHAR   = ['#131313','#1d1b19','#282523','#34302b','#413c35','#4f4941'];
  const LINE   = ['#b8a97e','#c8bb92','#d6cba6','#e2dab8','#ece6c8','#f4f0d6'];
  const PIPE   = ['#2a2f33','#3c454b','#525c63','#6d777e','#889298','#a2acb1'];
  const VOID   = ['#0e1114','#13171b','#181d22'];

  // ---- tiny vector + colour kit ----------------------------------------------
  const sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]];
  const add=(a,b)=>[a[0]+b[0],a[1]+b[1],a[2]+b[2]];
  const mul=(a,s)=>[a[0]*s,a[1]*s,a[2]*s];
  const dot=(a,b)=>a[0]*b[0]+a[1]*b[1]+a[2]*b[2];
  const cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
  const norm=(a)=>{const m=Math.hypot(a[0],a[1],a[2])||1;return [a[0]/m,a[1]/m,a[2]/m];};
  const nrmOf=(v)=>norm(cross(sub(v[1],v[0]),sub(v[2],v[0])));
  const lerp3=(a,b,t)=>[a[0]+(b[0]-a[0])*t,a[1]+(b[1]-a[1])*t,a[2]+(b[2]-a[2])*t];
  const lerp2=(a,b,t)=>[a[0]+(b[0]-a[0])*t,a[1]+(b[1]-a[1])*t];
  const mid=(ps)=>{const c=[0,0,0];for(const p of ps){c[0]+=p[0];c[1]+=p[1];c[2]+=p[2];}return mul(c,1/ps.length);};
  function mulberry32(a){return function(){a|=0;a=a+0x6D2B79F5|0;let t=Math.imul(a^a>>>15,1|a);t=t+Math.imul(t^t>>>7,61|t)^t;return((t^t>>>14)>>>0)/4294967296;};}
  const h2r=(h)=>[parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)];
  const r2h=(r,g,b)=>{const f=(n)=>Math.max(0,Math.min(255,Math.round(n))).toString(16).padStart(2,'0');return '#'+f(r)+f(g)+f(b);};
  const mix=(a,b,t)=>{const A=h2r(a),B=h2r(b);return r2h(A[0]+(B[0]-A[0])*t,A[1]+(B[1]-A[1])*t,A[2]+(B[2]-A[2])*t);};
  const desat=(h,t)=>{const [r,g,b]=h2r(h);const l=0.3*r+0.59*g+0.11*b;return r2h(r+(l-r)*t,g+(l-g)*t,b+(l-b)*t);};

  const F=(v,mat,b,db,uv,tex,flat)=>({v,mat,b:b||0,db:db||0,uv:uv||null,tex:tex||null,flat:!!flat});

  // outward-wound quad: flips winding so the normal points away from `c`
  function q(out,p0,p1,p2,p3,mat,bias,c){
    if(c && dot(nrmOf([p0,p1,p2]), sub(mid([p0,p1,p2,p3]),c))<0) out.push(F([p3,p2,p1,p0],mat,bias));
    else out.push(F([p0,p1,p2,p3],mat,bias));
  }
  function box(out,x0,x1,y0,y1,z0,z1,mat,bias){
    const c=[(x0+x1)/2,(y0+y1)/2,(z0+z1)/2];
    q(out,[x0,y0,z0],[x1,y0,z0],[x1,y0,z1],[x0,y0,z1],mat,bias,c);
    q(out,[x1,y1,z0],[x0,y1,z0],[x0,y1,z1],[x1,y1,z1],mat,bias,c);
    q(out,[x0,y1,z0],[x0,y0,z0],[x0,y0,z1],[x0,y1,z1],mat,bias,c);
    q(out,[x1,y0,z0],[x1,y1,z0],[x1,y1,z1],[x1,y0,z1],mat,bias,c);
    q(out,[x0,y0,z1],[x1,y0,z1],[x1,y1,z1],[x0,y1,z1],mat,bias,c);
  }
  // a rectangular stick of section w x h running p0 -> p1 (studs, rafters, pipe, planks)
  function bar(out,p0,p1,w,h,mat,bias){
    const d=norm(sub(p1,p0)); if(!isFinite(d[0])) return;
    const ref=Math.abs(d[2])>0.9?[1,0,0]:[0,0,1];
    const u=norm(cross(d,ref)), v=norm(cross(d,u));
    const c=[ add(mul(u,-w/2),mul(v,-h/2)), add(mul(u,w/2),mul(v,-h/2)),
              add(mul(u,w/2),mul(v,h/2)),   add(mul(u,-w/2),mul(v,h/2)) ];
    const ctr=mid([p0,p1]);
    for(let i=0;i<4;i++){ const j=(i+1)&3;
      q(out,add(p0,c[i]),add(p0,c[j]),add(p1,c[j]),add(p1,c[i]),mat,bias,ctr); }
    q(out,add(p1,c[0]),add(p1,c[1]),add(p1,c[2]),add(p1,c[3]),mat,bias,ctr);
    q(out,add(p0,c[3]),add(p0,c[2]),add(p0,c[1]),add(p0,c[0]),mat,bias,ctr);
  }
  function slab(out,x0,x1,y0,y1,z,mat,bias){
    out.push(F([[x0,y0,z],[x1,y0,z],[x1,y1,z],[x0,y1,z]],mat,bias||0));
  }

  // bilinear point / uv inside a quad face: u along v0->v1, w along v0->v3
  function bp(f,u,w){ return lerp3(lerp3(f.v[0],f.v[1],u), lerp3(f.v[3],f.v[2],u), w); }
  function buv(f,u,w){ return f.uv ? lerp2(lerp2(f.uv[0],f.uv[1],u), lerp2(f.uv[3],f.uv[2],u), w) : null; }
  function cell(f,u0,u1,w0,w1,mat,bias,tex){
    const v=[bp(f,u0,w0),bp(f,u1,w0),bp(f,u1,w1),bp(f,u0,w1)];
    const uv=f.uv?[buv(f,u0,w0),buv(f,u1,w0),buv(f,u1,w1),buv(f,u0,w1)]:null;
    const t = tex===undefined ? f.tex : tex;
    return F(v, mat||f.mat, bias!=null?bias:f.b, f.db, t?uv:null, t, f.flat);
  }
  function subdiv(f,nu,nw){
    if(f.v.length!==4) return [f];
    const out=[];
    for(let i=0;i<nu;i++) for(let j=0;j<nw;j++) out.push(cell(f,i/nu,(i+1)/nu,j/nw,(j+1)/nw));
    return out;
  }

  // ---- textures ---------------------------------------------------------------
  const sheathTex=(u,v)=>{ const a=((u%1.22)+1.22)%1.22, b=((v%0.62)+0.62)%0.62;
    return (a<0.045||b<0.04)?-2:0; };
  const deckTex=(u,v)=>{ const a=((v%0.26)+0.26)%0.26; return a<0.038?-1:0; };
  const plyTex=(u,v)=>{ const a=((u%0.62)+0.62)%0.62; return a<0.04?-1:0; };

  // ---- survey: read the finished building back out of its own faces -----------
  const WALLMAT = { body:1, lower:1, stone:1, cinder:1, brick:1, galv:1, rust:1, sign:0 };
  function survey(faces, b){
    b = b||{};
    let x0=1e9,x1=-1e9,y0=1e9,y1=-1e9,z1=-1e9;
    for(const f of faces) for(const p of f.v){
      if(p[0]<x0)x0=p[0]; if(p[0]>x1)x1=p[0];
      if(p[1]<y0)y0=p[1]; if(p[1]>y1)y1=p[1]; if(p[2]>z1)z1=p[2]; }
    const fH = b.fH!=null ? b.fH : 0.22;
    const walls=[], roofs=[], tops=[];
    for(const f of faces){
      const n=nrmOf(f.v), zs=f.v.map(p=>p[2]);
      const fz0=Math.min.apply(null,zs), fz1=Math.max.apply(null,zs);
      if(f.mat==='roof' && f.v.length===4 && Math.abs(n[2])<0.99 && !f.flat && fz1>fH+0.9){ roofs.push(f); continue; }
      if(WALLMAT[f.mat] && !f.flat && !f.db && Math.abs(n[2])<0.3 && fz1-fz0>0.8 && fz0<=fH+0.45){
        walls.push(f); if(f.v.length===4 && Math.abs(f.v[2][2]-f.v[3][2])<0.02) tops.push(fz1); }
    }
    tops.sort((a,c)=>a-c);
    const eaveZ = b.eaveZ!=null ? b.eaveZ : (tops.length ? tops[Math.floor(tops.length/2)] : fH+2.4);
    const ridgeZ = b.ridgeZ!=null ? Math.max(b.ridgeZ,eaveZ+0.2) : Math.max(z1, eaveZ+0.6);
    // bottom edge (outward-ordered) of every real wall — the true plan outline, wings and all
    const lines=[];
    for(const f of walls){
      const s=f.v.map((p,i)=>({p,i})).sort((a,c)=>a.p[2]-c.p[2]);
      const a=s[0], c=s[1]; if(Math.abs(a.p[2]-c.p[2])>0.25) continue;
      const p=(a.i<c.i||(a.i===0&&c.i===f.v.length-1))?[a.p,c.p]:[c.p,a.p];
      const L=Math.hypot(p[1][0]-p[0][0],p[1][1]-p[0][1]);
      if(L<0.5) continue;
      lines.push({ a:p[0], b:p[1], L, n:nrmOf(f.v), f });
    }
    lines.sort((a,c)=>c.L-a.L);
    return { x0,x1,y0,y1,z1, fH, eaveZ, ridgeZ, walls, roofs, lines,
             cx:(x0+x1)/2, cy:(y0+y1)/2, span:Math.max(x1-x0,y1-y0) };
  }

  // top of a wall face at parameter t along its bottom edge (handles gable triangles)
  function topAt(f, t){
    const v=f.v;
    if(v.length===4) return v[3][2] + (v[2][2]-v[3][2])*t;
    const zs=v.map(p=>p[2]), i=zs.indexOf(Math.max.apply(null,zs));
    const apex=v[i], base=v.filter((p,k)=>k!==i);
    const L=Math.hypot(base[1][0]-base[0][0],base[1][1]-base[0][1])||1;
    const ta=((apex[0]-base[0][0])*(base[1][0]-base[0][0])+(apex[1]-base[0][1])*(base[1][1]-base[0][1]))/(L*L);
    const tt=Math.max(0.001,Math.min(0.999,ta));
    return t<tt ? base[0][2]+(apex[2]-base[0][2])*(t/tt)
                : apex[2]+(base[1][2]-apex[2])*((t-tt)/(1-tt));
  }

  // ============================= CONSTRUCTION ==================================
  function sitePad(out, S, rnd, wet){
    const m=0.95, x0=S.x0-m, x1=S.x1+m, y0=S.y0-m, y1=S.y1+m;
    slab(out, x0,x1, y0,y1, 0.012, 'lcGravel', -0.2);
    for(let i=0;i<7;i++){                       // churned mud + ruts
      const w=0.5+rnd()*1.5, h=0.35+rnd()*1.1;
      const px=x0+rnd()*(x1-x0-w), py=y0+rnd()*(y1-y0-h);
      slab(out, px,px+w, py,py+h, 0.02, 'lcDirt', -0.3-rnd()*0.5);
    }
    if(wet){ const ry=y0+ (y1-y0)*0.30;
      slab(out, x0,x1, ry,ry+0.26, 0.024, 'lcDirt', -1.1);
      slab(out, x0,x1, ry+0.92,ry+1.18, 0.024, 'lcDirt', -1.1); }
  }
  function stakesAndLines(out, S){
    const m=0.3, pts=[[S.x0-m,S.y0-m],[S.x1+m,S.y0-m],[S.x1+m,S.y1+m],[S.x0-m,S.y1+m]];
    const z=0.52;
    for(const p of pts) bar(out,[p[0],p[1],0],[p[0],p[1],z+0.14],0.07,0.07,'lcLumber',0.2);
    for(let i=0;i<4;i++){ const a=pts[i], b=pts[(i+1)&3];
      bar(out,[a[0],a[1],z],[b[0],b[1],z],0.035,0.035,'lcLine',1.4); }
  }
  function lumberStack(out, x, y, rot, n, mat, rnd){
    const L=2.6, w=0.62, t=0.075, c=Math.cos(rot), s=Math.sin(rot);
    for(let i=0;i<n;i++){
      const z=0.06+i*t, j=(rnd()-0.5)*0.12;
      const a=[x-c*L/2+j, y-s*L/2, z], b=[x+c*L/2+j, y+s*L/2, z];
      bar(out,a,b,w,t*0.92,mat, i%2?0.15:-0.1);
    }
  }
  function sheetStack(out, x, y, rnd){
    for(let i=0;i<4;i++) box(out, x-0.62,x+0.62, y-0.45,y+0.45, 0.05+i*0.055, 0.095+i*0.055, 'lcSheath', i%2?0.1:-0.15);
  }
  function sawhorse(out, x, y, rot){
    const c=Math.cos(rot), s=Math.sin(rot), L=1.15, h=0.72, sp=0.34;
    for(const e of [-1,1]){
      const ex=x+c*L/2*e, ey=y+s*L/2*e;
      bar(out,[ex-s*sp,ey+c*sp,0],[ex,ey,h],0.06,0.06,'lcLumber',0);
      bar(out,[ex+s*sp,ey-c*sp,0],[ex,ey,h],0.06,0.06,'lcLumber',-0.2);
    }
    bar(out,[x-c*L/2,y-s*L/2,h],[x+c*L/2,y+s*L/2,h],0.11,0.09,'lcLumber',0.35);
  }
  function toolClutter(out, x, y, rnd){
    box(out,x,x+0.42,y,y+0.34,0,0.36,'lcSteel',0.1);                  // a box of tools
    box(out,x+0.55,x+0.83,y+0.1,y+0.38,0,0.3,'lcRust',-0.1);          // a bucket
    bar(out,[x-0.2,y-0.5,0.05],[x+1.1,y-0.32,0.05],0.09,0.05,'lcLumber',0);
  }
  function scaffold(out, S, line, rnd, hi){
    const L=line.L, n=Math.max(2,Math.round(L/1.85)), o=0.78;
    const dx=(line.b[0]-line.a[0])/L, dy=(line.b[1]-line.a[1])/L;
    const ox=line.n[0]*o, oy=line.n[1]*o;
    const H=Math.min(hi, S.eaveZ+0.55);
    const P=(t,e)=>[line.a[0]+dx*L*t+ox*e, line.a[1]+dy*L*t+oy*e, 0];
    for(let i=0;i<=n;i++){ const t=i/n;
      for(const e of [0.42,1]){ const p=P(t,e); bar(out,[p[0],p[1],0],[p[0],p[1],H],0.075,0.075,'lcPipe',0.15); }
    }
    for(const lz of [1.62, 3.24, 4.86]){
      if(lz>H-0.35) break;
      for(const e of [0.42,1]){
        const a=P(0,e), b=P(1,e);
        bar(out,[a[0],a[1],lz],[b[0],b[1],lz],0.06,0.06,'lcPipe',0.3);
      }
      const a0=P(0,0.42), a1=P(0,1), b0=P(1,0.42), b1=P(1,1);
      for(let k=0;k<3;k++){ const u=(k+0.5)/3;                        // deck planks
        const pa=[a0[0]+(b0[0]-a0[0])*(k/3), a0[1]+(b0[1]-a0[1])*(k/3)];
        const pb=[a0[0]+(b0[0]-a0[0])*((k+1)/3), a0[1]+(b0[1]-a0[1])*((k+1)/3)];
        void u; void pa; void pb;
      }
      const seg=6;
      for(let k=0;k<seg;k++){
        const t0=k/seg, t1=(k+1)/seg;
        const c0=P(t0,0.5), c1=P(t1,0.5), d0=P(t0,0.95), d1=P(t1,0.95);
        out.push(F([[c0[0],c0[1],lz+0.06],[c1[0],c1[1],lz+0.06],[d1[0],d1[1],lz+0.06],[d0[0],d0[1],lz+0.06]],'lcLumber',k%2?0.25:0.05));
      }
    }
    // one raking brace, and a ladder at the near end
    const b0=P(0,1), b1=P(0.55,1);
    bar(out,[b0[0],b0[1],0.1],[b1[0],b1[1],Math.min(H,3.3)],0.05,0.05,'lcPipe',-0.15);
    const l0=P(0.08,1.05), lt=Math.min(H+0.3,S.eaveZ+0.8);
    for(const e of [-0.16,0.16]){
      bar(out,[l0[0]-dy*e,l0[1]+dx*e,0],[l0[0]-dy*e+ox*0.28,l0[1]+dx*e+oy*0.28,lt],0.05,0.05,'lcLumber',0.2);
    }
    for(let r=1;r*0.34<lt;r++){ const z=r*0.34, t=z/lt;
      const a=[l0[0]-dy*-0.16+ox*0.28*t, l0[1]+dx*-0.16+oy*0.28*t, z];
      const b=[l0[0]-dy*0.16+ox*0.28*t, l0[1]+dx*0.16+oy*0.28*t, z];
      bar(out,a,b,0.05,0.035,'lcLumber',0.4);
    }
  }
  function tarpOn(out, line, S, rnd, from){
    const L=line.L, seg=5, top=Math.min(S.eaveZ-0.15, from+2.2), bot=Math.max(S.fH+0.15, top-2.3);
    const dx=(line.b[0]-line.a[0])/L, dy=(line.b[1]-line.a[1])/L;
    const t0=0.12+rnd()*0.2, t1=t0+0.35+rnd()*0.2;
    for(let k=0;k<seg;k++){
      const u0=t0+(t1-t0)*(k/seg), u1=t0+(t1-t0)*((k+1)/seg);
      const bulge=(u)=>0.10+0.13*Math.sin((u-t0)/(t1-t0)*Math.PI);
      const o0=0.07+bulge(u0), o1=0.07+bulge(u1);
      const p0=[line.a[0]+dx*L*u0+line.n[0]*o0, line.a[1]+dy*L*u0+line.n[1]*o0];
      const p1=[line.a[0]+dx*L*u1+line.n[0]*o1, line.a[1]+dy*L*u1+line.n[1]*o1];
      const sag=0.16*Math.sin((k+0.5)/seg*Math.PI);
      out.push(F([[p0[0],p0[1],bot-sag],[p1[0],p1[1],bot-sag],[p1[0],p1[1],top],[p0[0],p0[1],top]],
        'lcTarp', k%2?0.2:-0.05));
    }
  }
  function sills(out, S){
    for(const l of S.lines){
      bar(out,[l.a[0],l.a[1],S.fH+0.07],[l.b[0],l.b[1],S.fH+0.07],0.24,0.14,'lcLumber',0.3);
    }
  }
  function foundation(out, S, hasReal){
    if(hasReal) return;                                   // the rig drew its own piers/blocks
    for(const l of S.lines){
      bar(out,[l.a[0],l.a[1],0],[l.b[0],l.b[1],0],0.42,S.fH*2,'lcConc',0);
    }
    slab(out, S.x0+0.2,S.x1-0.2, S.y0+0.2,S.y1-0.2, S.fH*0.55, 'lcConc', -0.35);
  }
  function studWalls(out, S, rnd){
    const SP=0.62;
    for(const l of S.lines){
      const n=Math.max(2,Math.round(l.L/SP)), z0=S.fH+0.14;
      const dx=(l.b[0]-l.a[0]), dy=(l.b[1]-l.a[1]);
      const tops=[];
      for(let i=0;i<=n;i++){ const t=i/n;
        const x=l.a[0]+dx*t, y=l.a[1]+dy*t;
        const tz=Math.max(z0+0.4, topAt(l.f,t)-0.09);
        tops.push([x,y,tz]);
        const dbl=(i===0||i===n)?1.7:1;                    // corner posts double up
        bar(out,[x,y,z0],[x,y,tz],0.10*dbl,0.14,'lcLumber', i%2?0.1:-0.05);
      }
      // top plate follows the wall head, so gable ends get their raked plate
      for(let i=0;i<n;i++) bar(out,tops[i],tops[i+1],0.13,0.10,'lcLumber',0.3);
      bar(out,[l.a[0],l.a[1],S.fH+0.14],[l.b[0],l.b[1],S.fH+0.14],0.13,0.09,'lcLumber',0.35);
      // one row of blocking / a let-in brace
      const bz=S.fH+ (S.eaveZ-S.fH)*0.52;
      bar(out,[l.a[0],l.a[1],bz],[l.b[0],l.b[1],bz],0.10,0.09,'lcLumber',0.05);
      if(l.L>3){ const t=0.18+rnd()*0.2;
        bar(out,[l.a[0]+dx*t,l.a[1]+dy*t,S.fH+0.2],[l.a[0]+dx*(t+0.42),l.a[1]+dy*(t+0.42),Math.min(S.eaveZ,topAt(l.f,t+0.42))-0.2],0.08,0.06,'lcLumber',-0.1); }
    }
  }
  // rafters read straight off the finished roof planes, so any roof shape frames correctly
  function roofFrame(out, S, rnd, sparse){
    for(const f of S.roofs){
      const e01=sub(f.v[1],f.v[0]), e03=sub(f.v[3],f.v[0]);
      const slopeAlong01 = Math.abs(e01[2])>Math.abs(e03[2]);
      const len = slopeAlong01 ? Math.hypot(e03[0],e03[1],e03[2]) : Math.hypot(e01[0],e01[1],e01[2]);
      const n=Math.max(2,Math.round(len/0.72));
      const nz=nrmOf(f.v), off=mul(nz,-0.075);
      for(let i=0;i<=n;i++){
        const t=i/n;
        if(sparse && rnd()<0.18) continue;
        const p0 = slopeAlong01 ? bp(f,0,t) : bp(f,t,0);
        const p1 = slopeAlong01 ? bp(f,1,t) : bp(f,t,1);
        bar(out, add(p0,off), add(p1,off), 0.09,0.17,'lcLumber', i%2?0.12:-0.06);
      }
      // ridge / eave members along the high and low edges
      const hi = slopeAlong01 ? [bp(f,1,0),bp(f,1,1)] : [bp(f,0,1),bp(f,1,1)];
      const lo = slopeAlong01 ? [bp(f,0,0),bp(f,0,1)] : [bp(f,0,0),bp(f,1,0)];
      const pick = hi[0][2]>lo[0][2] ? hi : lo;
      bar(out, add(pick[0],off), add(pick[1],off), 0.13,0.22,'lcLumber',0.3);
    }
    // collar ties across at the eave line — length taken from the real perpendicular extent, so a tie
    // can never poke out the far side into thin air
    const l=S.lines[0];
    if(l && S.ridgeZ-S.eaveZ>0.8){
      const n=Math.max(1,Math.round(l.L/1.9)), z=S.eaveZ+(S.ridgeZ-S.eaveZ)*0.42;
      const dx=(l.b[0]-l.a[0])/n, dy=(l.b[1]-l.a[1])/n;
      let dmax=0;
      for(const c of [[S.x0,S.y0],[S.x1,S.y0],[S.x1,S.y1],[S.x0,S.y1]]){
        const dd=-((c[0]-l.a[0])*l.n[0]+(c[1]-l.a[1])*l.n[1]); if(dd>dmax) dmax=dd; }
      const w=Math.max(0.9, dmax*0.88);
      for(let i=1;i<n;i++){
        const x=l.a[0]+dx*i, y=l.a[1]+dy*i;
        bar(out,[x-l.n[0]*0.1,y-l.n[1]*0.1,z],[x-l.n[0]*w,y-l.n[1]*w,z],0.09,0.13,'lcLumber',0.05);
      }
    }
  }

  const OPENING = { glass:1, glassHi:1, door:1 };
  const DROP_ON_BUILD = { sign:1, glassHi:1 };

  function buildPhase(phase, faces, S, rnd){
    const out=[];
    const hasFound = faces.some(f=>(f.mat==='stone'||f.mat==='cinder'||f.mat==='brick')
      && Math.max.apply(null,f.v.map(p=>p[2]))<=S.fH+0.75);
    const foundFaces = faces.filter(f=>(f.mat==='stone'||f.mat==='cinder'||f.mat==='brick'||f.mat==='wood')
      && Math.max.apply(null,f.v.map(p=>p[2]))<=S.fH+0.8);
    const wet = phase!=='cladding';

    if(phase==='site'){
      sitePad(out,S,rnd,wet); stakesAndLines(out,S);
      lumberStack(out,S.x1+1.5,S.cy-0.6,0.25,6,'lcLumber',rnd);
      sawhorse(out,S.x0-1.5,S.cy+0.9,1.1);
      toolClutter(out,S.x0-1.9,S.y0-0.4,rnd);
      return out;
    }
    sitePad(out,S,rnd,wet);
    if(phase!=='cladding') stakesAndLines(out,S);

    if(phase==='foundation'){
      for(const f of foundFaces) out.push(f);
      foundation(out,S,hasFound); sills(out,S);
      lumberStack(out,S.x1+1.6,S.cy,0.15,7,'lcLumber',rnd);
      lumberStack(out,S.x1+1.6,S.cy+1.5,0.15,4,'lcLumber',rnd);
      sawhorse(out,S.x0-1.6,S.cy-0.7,1.25);
      toolClutter(out,S.x0-2.0,S.y1+0.2,rnd);
      return out;
    }
    for(const f of foundFaces) out.push(f);
    foundation(out,S,hasFound); sills(out,S);

    if(phase==='frame' || phase==='rafters'){
      studWalls(out,S,rnd);
      if(phase==='rafters'){ roofFrame(out,S,rnd,false); if(S.lines[0]) scaffold(out,S,S.lines[0],rnd,S.eaveZ+0.5); }
      else if(S.lines[1]) scaffold(out,S,S.lines[1],rnd,S.eaveZ-0.4);
      lumberStack(out,S.x1+1.6,S.cy,0.15,phase==='frame'?5:3,'lcLumber',rnd);
      sheetStack(out,S.x1+1.7,S.cy+1.7,rnd);
      sawhorse(out,S.x0-1.6,S.cy-0.7,1.25);
      toolClutter(out,S.x0-2.0,S.y1+0.2,rnd);
      return out;
    }

    // --- sheathed / cladding: the real shell, re-materialled ---
    const cladZ = S.fH + (S.eaveZ-S.fH)*0.54;
    for(const f of faces){
      const zs=f.v.map(p=>p[2]);
      const fz0=Math.min.apply(null,zs), fz1=Math.max.apply(null,zs);
      if(fz1<=S.fH+0.8 && (f.mat==='stone'||f.mat==='cinder'||f.mat==='brick'||f.mat==='wood')) continue; // already in
      if(DROP_ON_BUILD[f.mat]) continue;
      const isRoof=f.mat==='roof';
      const isWall=WALLMAT[f.mat] && !f.flat && !f.db;
      const isOpen=OPENING[f.mat];

      if(isRoof){
        if(phase==='sheathed') out.push(F(f.v,'lcDeck',f.b,f.db,f.uv,f.uv?deckTex:null,f.flat));
        else out.push(f);
        continue;
      }
      if(isOpen){
        const keep = phase==='cladding' && fz0<cladZ+0.15;
        if(f.mat==='glassHi'){ if(keep) out.push(f); continue; }
        if(keep) out.push(f);
        else out.push(F(f.v,'lcVoid',-0.6,f.db,null,null,f.flat));      // rough opening
        continue;
      }
      if(f.mat==='trim'){
        if(phase==='cladding' && fz0<cladZ) out.push(f);
        continue;
      }
      if(isWall){
        if(phase==='sheathed'){
          out.push(F(f.v,'lcSheath',f.b,f.db,f.uv,f.uv?sheathTex:null,f.flat)); continue;
        }
        if(f.v.length!==4 || fz0>=cladZ){                                // gables clad last
          out.push(F(f.v,'lcSheath',f.b,f.db,f.uv,f.uv?sheathTex:null,f.flat)); continue;
        }
        if(fz1<=cladZ){ out.push(f); continue; }
        const t=(cladZ-fz0)/(fz1-fz0);
        const vert = Math.abs(f.v[0][2]-f.v[3][2])>0.2;
        if(!vert){ out.push(f); continue; }
        const lower=cell(f,0,1,0,t), upper=cell(f,0,1,t,1);
        out.push(lower);
        out.push(F(upper.v,'lcSheath',upper.b,upper.db,upper.uv,upper.uv?sheathTex:null,upper.flat));
        continue;
      }
      out.push(f);
    }
    if(phase==='sheathed'){
      if(S.lines[0]) scaffold(out,S,S.lines[0],rnd,S.eaveZ+0.4);
      if(S.lines[1]) tarpOn(out,S.lines[1],S,rnd,S.fH+1.1);
      sheetStack(out,S.x1+1.7,S.cy,rnd);
      lumberStack(out,S.x1+1.7,S.cy+1.6,0.15,4,'lcLumber',rnd);
    } else {
      if(S.lines[1]) scaffold(out,S,S.lines[1],rnd,S.eaveZ+0.3);
      lumberStack(out,S.x1+1.6,S.cy+1.1,0.15,3,'lcLumber',rnd);
      sheetStack(out,S.x1+1.7,S.cy-0.7,rnd);
    }
    sawhorse(out,S.x0-1.6,S.cy-0.7,1.25);
    toolClutter(out,S.x0-2.0,S.y1+0.2,rnd);
    return out;
  }

  // ================================= DECAY =====================================
  function weeds(out, x, y, h, rnd, mat){
    const n=3+((rnd()*3)|0);
    for(let i=0;i<n;i++){
      const a=rnd()*Math.PI, r=0.14+rnd()*0.2, hh=h*(0.55+rnd()*0.7);
      const dx=Math.cos(a)*r, dy=Math.sin(a)*r;
      out.push(F([[x-dx*0.35,y-dy*0.35,0],[x+dx*0.35,y+dy*0.35,0],[x+dx,y+dy,hh]],mat||'lcWeed',(i%2)?0.25:-0.15));
      out.push(F([[x-dy*0.3,y+dx*0.3,0],[x+dy*0.3,y-dx*0.3,0],[x-dx*0.7,y-dy*0.7,hh*0.8]],mat||'lcWeed',(i%2)?-0.1:0.3));
    }
  }
  function debris(out, x, y, r, rnd, mat, n){
    for(let i=0;i<n;i++){
      const a=rnd()*Math.PI*2, d=rnd()*r, L=0.5+rnd()*1.5;
      const px=x+Math.cos(a)*d, py=y+Math.sin(a)*d, th=rnd()*Math.PI;
      const lo=0.035+rnd()*0.05;                    // one end always ON the ground; the other rests on the heap
      bar(out,[px-Math.cos(th)*L/2,py-Math.sin(th)*L/2,lo],
              [px+Math.cos(th)*L/2,py+Math.sin(th)*L/2,lo+(rnd()<0.45?0.06+rnd()*0.26:0)],
              0.14+rnd()*0.1, 0.055, mat, (i%3)-1.1);
    }
  }
  function boardsOver(out, f, rnd, mat){
    for(let k=0;k<2;k++){
      const w0=0.16+k*0.42, w1=w0+0.24;
      const sk=(rnd()-0.5)*0.16;
      const v=[bp(f,-0.06,w0+sk),bp(f,1.06,w0),bp(f,1.06,w1),bp(f,-0.06,w1+sk)];
      out.push(F(v.map(p=>[p[0],p[1],p[2]]), mat, k?0.35:0.05, (f.db||0)+0.14, null,null,true));
    }
  }

  function decayPass(faces, S, decay, burnt, rnd){
    const d=DW[decay]||0, out=[];
    const ruin=decay==='ruin', coll=decay==='collapsing', aband=decay==='abandoned'||coll||ruin;
    const stripRoof = ruin || burnt;
    // a stove-in quadrant for 'collapsing', chosen deterministically
    const holeSide = rnd()<0.5 ? 1 : -1;
    const leanK = coll?0.055:(ruin?0:d*0.012);
    const sagK  = coll?0.42:(aband?0.16:d*0.14);
    const stubZ = (x,y)=>{                                   // ragged break line for a ruin
      const n=Math.sin(x*1.7+y*0.9)*0.42+Math.sin(x*5.3-y*3.1)*0.30
             +Math.sin(y*9.7+x*2.3)*0.22+Math.sin(x*17.3+y*11.1)*0.14;
      return S.fH + 0.86 + n*0.82;                           // some bays gone to the sill, one corner up
    };
    // NOTHING FLOATS. One damage side, shared by the roof hole, the fallen wall and everything that
    // was carried on either — decided on y so roof and wall agree whichever way the ridge runs.
    const brokeAt=(p)=>{
      if(!coll) return false;
      const ragged = S.fH + 0.86 + 0.46*Math.abs(Math.sin(p[0]*2.3+p[1]*1.7));
      return (p[1]-S.cy)*holeSide > S.span*0.17 && p[2] > ragged;
    };
    const carried=(f,zs)=>{                                  // roof furniture over the hole: it went too
      if(!coll || f.mat==='roof') return false;
      const c=mid(f.v);
      return Math.min.apply(null,zs) > S.eaveZ-0.35 && (c[1]-S.cy)*holeSide > 0;
    };
    const warp=(p)=>{
      let [x,y,z]=p;
      if(z>S.fH){
        const t=(z-S.fH)/Math.max(0.6,S.ridgeZ-S.fH);
        x += leanK*t*t*S.span*0.6;
        y += leanK*0.4*t*t*S.span*0.6;
        if(z>S.eaveZ){ const u=(z-S.eaveZ)/Math.max(0.4,S.ridgeZ-S.eaveZ);
          z -= sagK*u*(1.1-Math.abs((y-S.cy)/Math.max(1,(S.y1-S.y0)/2))*0.5); }
      }
      return [x,y,z];
    };
    const W=(f)=>F(f.v.map(warp),f.mat,f.b,f.db,f.uv,f.tex,f.flat);

    for(const f of faces){
      const zs=f.v.map(p=>p[2]);
      const fz0=Math.min.apply(null,zs), fz1=Math.max.apply(null,zs);
      const isRoof=f.mat==='roof';
      const isWall=WALLMAT[f.mat] && !f.flat && !f.db && fz1-fz0>0.6;
      const isFound=fz1<=S.fH+0.8;

      if(isRoof || f.mat==='lcDeck'){
        if(stripRoof) continue;
        const cells=subdiv(f, 6, 4);
        for(const c of cells){
          const cc=mid(c.v), r=rnd();
          const inHole = coll && ((cc[1]-S.cy)*holeSide>0) && r<0.78;   // rafters below hold the rest
          if(inHole) continue;                                  // stove in — rafters show through
          if(r < d*0.30) out.push(W(F(c.v,'lcSheath',c.b-0.15,c.db,c.uv,c.uv?plyTex:null,c.flat)));
          else if(r < d*0.30+0.06*d*2) out.push(W(F(c.v,'lcMoss',c.b-0.1,c.db,null,null,c.flat)));
          else if(coll && r>0.94) out.push(W(F(c.v,'lcVoid',-0.8,c.db,null,null,c.flat)));
          else out.push(W(c));
        }
        continue;
      }
      if(f.mat==='glass'||f.mat==='glassHi'){
        if(ruin) continue;
        if(carried(f,zs) || brokeAt(mid(f.v))) continue;      // that wall is on the ground
        if(f.mat==='glassHi'){ if(!aband && rnd()>d*0.7) out.push(W(f)); continue; }
        if(!aband){ if(rnd()<d*0.5) out.push(W(F(f.v,'lcVoid',-0.5,f.db,null,null,f.flat)));
                    else out.push(W(f)); continue; }
        const r=rnd();
        if(r<0.42){ out.push(W(F(f.v,'lcSheath',0.1,f.db,null,null,f.flat))); boardsOver(out,W(f),rnd,'lcSheath'); }
        else if(r<0.72){ out.push(W(F(f.v,'lcVoid',-0.7,f.db,null,null,f.flat))); boardsOver(out,W(f),rnd,'lcSheath'); }
        else out.push(W(F(f.v,'lcVoid',-0.8,f.db,null,null,f.flat)));
        continue;
      }
      if(f.mat==='door'){
        if(ruin) continue;
        if(carried(f,zs) || brokeAt(mid(f.v))) continue;
        if(aband && rnd()<0.7) out.push(W(F(f.v,'lcVoid',-0.7,f.db,null,null,f.flat)));
        else out.push(W(f));
        continue;
      }
      if(f.mat==='trim'||f.mat==='sign'){
        if(ruin) continue;
        if(carried(f,zs) || brokeAt(mid(f.v))) continue;
        if(aband && rnd()<0.45) continue;                       // trim rotted off
        out.push(W(f)); continue;
      }
      if(isWall && !isFound){
        if(ruin){
          if(f.v.length!==4){                                   // gambrel / gothic gable polygons
            const m0=mid(f.v), lim=stubZ(m0[0],m0[1]);
            if(Math.min.apply(null,zs)>=lim) continue;
            out.push(F(f.v.map(p=>[p[0],p[1],Math.min(p[2],lim)]), burnt?'lcChar':f.mat, f.b,f.db,f.uv,f.tex,f.flat));
            continue;
          }
          const cols=subdiv(f, 10, 1);
          for(const c of cols){
            const cc=mid(c.v), lim=stubZ(cc[0],cc[1]);
            const czs=c.v.map(p=>p[2]);
            const cz0=Math.min.apply(null,czs);
            if(cz0>=lim) continue;
            const t=(lim-cz0)/Math.max(0.1,Math.max.apply(null,czs)-cz0);
            if(t<=0.02) continue;
            const kept=cell(c,0,1,0,Math.min(1,t));
            out.push(F(kept.v,burnt?'lcChar':kept.mat,kept.b,kept.db,kept.uv,kept.tex,kept.flat));
          }
          continue;
        }
        if(!aband){
          if(d>0.2){ const cells=subdiv(f,4,3);
            for(const c of cells){ const r=rnd();
              if(r<d*0.10) out.push(W(F(c.v,'lcSheath',c.b-0.2,c.db,c.uv,c.uv?plyTex:null,c.flat)));
              else out.push(W(c)); }
          } else out.push(W(f));
          continue;
        }
        const cells=subdiv(f, 8, 4);
        for(const c of cells){
          const cc=mid(c.v), r=rnd();
          if(brokeAt(cc)) continue;                              // that wall is down in the heap
          if(r<0.05+d*0.10) out.push(W(F(c.v,'lcVoid',-0.7,c.db,null,null,c.flat)));    // hole through
          else if(r<0.12+d*0.26) out.push(W(F(c.v,'lcSheath',c.b-0.25,c.db,c.uv,c.uv?plyTex:null,c.flat)));
          else out.push(W(c));
        }
        continue;
      }
      if(ruin){                                                  // the floor is a rubble-filled cellar hole
        const nz=nrmOf(f.v);
        if(Math.abs(nz[2])>0.8 && fz1<=S.fH+0.5 && fz1>=S.fH-0.35 && f.v.length===4){
          for(const c of subdiv(f,7,7)){ const r=rnd();
            out.push(F(c.v, r<0.16?'lcSheath':(r<0.24?(burnt?'lcChar':'lcMoss'):'lcDirt'),
              r<0.5?-0.25:0.15, c.db, null, null, c.flat)); }
          continue;
        }
      }
      if(ruin && !isFound && fz0>S.fH+0.1) continue;             // everything else is gone
      if(carried(f,zs)) continue;                                // cupolas, vents, booms: not floating
      out.push(W(f));
    }

    // ---- what decay adds ------------------------------------------------------
    if(coll || (burnt && aband && !ruin)) roofFrame(out, S, rnd, true);
    if(ruin){
      for(const l of S.lines){                                   // stub posts standing in the rubble
        const n=Math.max(1,Math.round(l.L/1.6));
        for(let i=0;i<=n;i++){ if(rnd()<0.55) continue;
          const t=i/n, x=l.a[0]+(l.b[0]-l.a[0])*t, y=l.a[1]+(l.b[1]-l.a[1])*t;
          bar(out,[x,y,S.fH],[x+(rnd()-0.5)*0.2,y+(rnd()-0.5)*0.2,S.fH+0.5+rnd()*1.3],0.11,0.13,burnt?'lcChar':'lcLumber',-0.2);
        }
      }
      debris(out,S.cx,S.cy,S.span*0.62,rnd,burnt?'lcChar':'lcSheath',26);
    }
    if(coll){
      debris(out,S.cx+S.span*0.34*-holeSide,S.cy,S.span*0.4,rnd,burnt?'lcChar':'lcSheath',18);
      debris(out,S.cx,S.cy+S.span*0.3*holeSide,S.span*0.3,rnd,'lcRust',5);
    }
    if(aband){
      debris(out,S.x1+0.9,S.y0+0.6,1.2,rnd,'lcRust',6);
      debris(out,S.x0-0.9,S.y1-0.5,1.0,rnd,burnt?'lcChar':'lcSheath',5);
    }
    if(d>0){
      const per=Math.max(6,Math.round((S.span)*(1.2+d*2.6)));
      for(let i=0;i<per;i++){
        const e=rnd(), t=rnd();
        let x,y;
        if(e<0.5){ x=S.x0+(S.x1-S.x0)*t; y=(rnd()<0.5?S.y0:S.y1)+(rnd()-0.5)*0.5; }
        else     { y=S.y0+(S.y1-S.y0)*t; x=(rnd()<0.5?S.x0:S.x1)+(rnd()-0.5)*0.5; }
        weeds(out,x,y,0.28+d*0.85+rnd()*0.4,rnd, rnd()<0.25?'lcMoss':'lcWeed');
      }
      if(ruin) for(let i=0;i<10;i++)
        weeds(out,S.x0+(S.x1-S.x0)*rnd(),S.y0+(S.y1-S.y0)*rnd(),0.5+rnd()*0.8,rnd,'lcWeed');
    }
    return out;
  }

  // ---- materials --------------------------------------------------------------
  function addMats(MATS, b, decay, burnt){
    const night=!!(b&&b.night), d=DW[decay]||0;
    const dim=(r)=>night?r.map(c=>mix(c,'#1b2733',0.42)):r;
    const age=(r)=>d?r.map(c=>mix(desat(c,d*0.4),'#6f6a5f',d*0.2)):r;
    const put=(k,r)=>{ MATS[k]={ramp:dim(age(r))}; };
    put('lcLumber',LUMBER); put('lcSheath',SHEATH); put('lcDeck',DECK); put('lcConc',CONC);
    put('lcDirt',DIRT); put('lcGravel',GRAVEL); put('lcTarp',TARP); put('lcWeed',WEED);
    put('lcMoss',MOSS); put('lcRust',RUSTC); put('lcLine',LINE); put('lcPipe',PIPE);
    MATS.lcChar={ramp:dim(CHAR)}; MATS.lcVoid={ramp:dim(VOID)};
    if(burnt){
      const soot=(r)=>r.map((c,i)=>mix(desat(c,0.85),'#151210',0.74+0.05*(5-i)));
      for(const k of ['body','lower','roof','trim','wood','door','sign','lcLumber','lcSheath','lcDeck','stone','cinder','brick','galv','rust'])
        if(MATS[k]) MATS[k]={ramp:soot(MATS[k].ramp), off:MATS[k].off};
    }
  }

  // ---- entry ------------------------------------------------------------------
  function normPhase(p){ return PHASES.indexOf(p)>=0 ? p : 'finished'; }
  function normDecay(p){ return DECAY.indexOf(p)>=0 ? p : 'sound'; }
  function active(opts){
    if(!opts) return false;
    return (opts.phase && opts.phase!=='finished') || (opts.decay && opts.decay!=='sound') || !!opts.burnt;
  }
  function apply(faces, MATS, b, opts){
    opts=opts||{};
    const phase=normPhase(opts.phase), decay=normDecay(opts.decay), burnt=!!opts.burnt;
    if(phase==='finished' && decay==='sound' && !burnt) return { faces, b };
    const S=survey(faces,b);
    root.BuildingLifecycle._last=S;
    const seed=((S.span*971)|0) ^ ((S.eaveZ*613)|0) ^ (PHASES.indexOf(phase)*7919) ^ (DECAY.indexOf(decay)*104729) ^ ((opts.seed|0)*31);
    const rnd=mulberry32(seed||7);
    addMats(MATS, b, decay, burnt);
    let out = phase==='finished' ? faces.slice() : buildPhase(phase, faces, S, rnd);
    if(decay!=='sound' || burnt) out = decayPass(out, S, decay, burnt, rnd);
    const bb = Object.assign({}, b, {
      weather: Math.min(1, (b&&b.weather!=null?b.weather:0.4) + DW[decay]*0.55),
    });
    return { faces: out, b: bb };
  }

  root.BuildingLifecycle = {
    PHASES, DECAY, PHASE_LABEL, PHASE_NOTE, DECAY_LABEL, DECAY_NOTE,
    active, apply, survey, mulberry32,
    RAMPS: { LUMBER, SHEATH, DECK, CONC, DIRT, GRAVEL, TARP, WEED, MOSS, RUSTC, CHAR, LINE, PIPE },
  };
})(typeof globalThis!=='undefined'?globalThis:window);
