/* Hidden Harbours — SHOVEL tool rig (M2 bake recipe, ADR-0006 — TOOL KIT PASS 2).
   Same fixed 3/4 turntable as the fleet / character / rod (45deg steps, upper-left key, dither,
   1px keyline, no AA, 32 px = 1 m). Cell 112x112, pivot (56,72) = GRIP CENTRE (the right hand,
   low on the shaft) — pins to the character's baked handR anchor every frame, the outboard-motor
   mount pattern. ONE TIER: ash shaft + graphite D-grip + worn steel-edged blade (~1.04 m).
   Drives CharacterIso's 'dig' tool anim (10f raise/thrust/pry/toss @ 90 ms); pitch/yaw arrive in
   RADIANS from CharacterIso.tool(dir,{anim:'dig',frame}). Spoil (tossed dirt) is RUNTIME FX,
   never baked — launches ~f8 from tip(); project() maps character-local 3D points to screen px.
   rest:'ground' (flat prop, 8 headings via dir, pivot = ground under the grip) | 'stored'
   (planted upright, blade in the ground). tip(dir,opts) -> blade-tip cell px; tipLocal(opts) ->
   3D local tip. Exposes globalThis.ShovelIso = { W,H,pivot,SPEC,DIG,SPOIL,REST,behind,
   defaultElev,KEY,render,tip,tipLocal,project }. */
(function (root) {
  const S = 32, DEG = Math.PI / 180, DEFAULT_ELEV = 40;
  const W = 112, H = 112, cx = 56, cy = 72;
  const KEY = '#101a19';
  // fleet master ramps only — no invented colours
  const WOOD  = ['#33271b','#473627','#5e4630','#6b4f35','#8a6a48'];
  const GRAPH = ['#101317','#1d2127','#2b323a','#3d454e','#525c63'];
  const STEEL = ['#3a4148','#565f66','#7a858c','#9fabb1','#c3ced2'];
  const SPOIL = ['#241a11','#33271b','#473627','#5e4630'];   // tossed-dirt FX ramp (runtime only)
  const SPEC = { label:'SPADE', lenUp:0.375, lenDn:0.665, shaftRad:0.022,
    blurb:'ash shaft, graphite D-grip, worn steel edge — one tier, everybody digs the same dirt' };
  const DIG = { frames:10, ms:90, tossFrame:8 };   // mirrors CharacterIso.ANIMS.dig

  const GAIN = 3.0, BIAS = 2.7, EDGE = 0.12;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  const v_sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]], v_add=(a,b)=>[a[0]+b[0],a[1]+b[1],a[2]+b[2]];
  const v_mul=(a,s)=>[a[0]*s,a[1]*s,a[2]*s], v_norm=(a)=>{ const m=Math.hypot(a[0],a[1],a[2])||1; return [a[0]/m,a[1]/m,a[2]/m]; };
  const v_cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
  function tube(A,B2,rad,mat,b){
    const ax=v_norm(v_sub(B2,A)); let up=[0,0,1]; if(Math.abs(ax[2])>0.9) up=[0,1,0];
    const r=v_norm(v_cross(ax,up)), u=v_cross(r,ax);
    const ring=(P)=>[ v_add(v_add(P,v_mul(r,rad)),v_mul(u,rad)), v_add(v_add(P,v_mul(r,-rad)),v_mul(u,rad)),
                      v_add(v_add(P,v_mul(r,-rad)),v_mul(u,-rad)), v_add(v_add(P,v_mul(r,rad)),v_mul(u,-rad)) ];
    const r0=ring(A), r1=ring(B2), out=[];
    for(let k=0;k<4;k++){ const k2=(k+1)%4; out.push({v:[r0[k],r0[k2],r1[k2],r1[k]],mat,b:b||0,db:-0.05}); }
    out.push({v:r1.slice(),mat,b:b||0,db:-0.05});
    out.push({v:r0.slice().reverse(),mat,b:b||0,db:-0.05});
    return out;
  }

  // shovel-local frame: origin = grip (right hand); +s toward the BLADE, -s toward the D-grip.
  // n = horizontal across the shaft, dn = "blade-face up" perpendicular — so the blade plate
  // automatically faces up-forward when the shaft pitches down for the thrust, and lies flat
  // (face up) when the shovel rests on the ground at pitch 0.
  function frameOf(pitch, yaw, zOff){
    const cp=Math.cos(pitch), sp=Math.sin(pitch), cyw=Math.cos(yaw), syw=Math.sin(yaw);
    const D=[syw*cp, cyw*cp, sp], z0=zOff||0;
    let n=v_cross(D,[0,0,1]); if(Math.hypot(n[0],n[1],n[2])<1e-4) n=[1,0,0]; n=v_norm(n);
    let dn=v_norm(v_cross(n,D)); if(dn[2]<0){ dn=v_mul(dn,-1); n=v_mul(n,-1); }
    const at=(s)=>[D[0]*s, D[1]*s, D[2]*s + z0];
    return { D, n, dn, at };
  }
  function facesOf(fr){
    const F=[], add=(fs)=>{ for(const f of fs) F.push(f); };
    const q=(v,mat,b,db)=>F.push({v,mat,b:b||0,db:db||0});
    const {n,dn,at}=fr;
    const P=(s,wn,wd)=>v_add(v_add(at(s),v_mul(n,wn)),v_mul(dn,wd));
    // D-grip: two cheeks + crossbar
    add(tube(P(-0.375,-0.052,0), P(-0.375,0.052,0), 0.014, 'fitt', -0.1));
    for(const sgn of [-1,1]) add(tube(P(-0.295,sgn*0.036,0), P(-0.372,sgn*0.036,0), 0.011, 'fitt', -0.25));
    // ferrule collar (blade socket)
    add(tube(at(0.33), at(0.445), 0.027, 'fitt', -0.2));
    // blade: tapered plate, dished slightly below the shaft axis
    const s0=0.42, s1=0.665, w0=0.088, w1=0.048, e=0.009, dr=-0.012;
    const c00=P(s0, w0, dr+e), c01=P(s0,-w0, dr+e), c10=P(s1, w1, dr+e), c11=P(s1,-w1, dr+e);
    const b00=P(s0, w0, dr-e), b01=P(s0,-w0, dr-e), b10=P(s1, w1, dr-e), b11=P(s1,-w1, dr-e);
    q([c00,c10,c11,c01],'blade', 0.15,-0.02);   // face (up)
    q([b01,b11,b10,b00],'blade',-0.50,-0.02);   // underside
    q([b00,b10,c10,c00],'blade',-0.20,-0.03);   // side +n
    q([c01,c11,b11,b01],'blade',-0.20,-0.03);   // side -n
    q([c10,b10,b11,c11],'edge',  0.50,-0.04);   // worn steel tip edge
    q([c00,c01,b01,b00],'blade',-0.30,-0.03);   // shoulder (mostly under the collar)
    return F;
  }

  const MATS = { wood:{ramp:WOOD,off:0}, fitt:{ramp:GRAPH,off:0}, blade:{ramp:GRAPH,off:1}, edge:{ramp:STEEL,off:0} };
  const RINDEX = {}; [WOOD,GRAPH,STEEL].forEach(r=>r.forEach((c,i)=>{ RINDEX[c]={r,i}; }));

  function camBasis(opts){
    const dir=opts.dir||0, th=dir*Math.PI/4;
    const e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    return { ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e) };
  }
  function projVert(x,y,z,B){
    const xr=x*B.ct - y*B.stt, yr=x*B.stt + y*B.ct, zr=z;
    return { xr,yr,zr, sx:cx+xr*S, sy:cy-(yr*B.se+zr*B.ce)*S, d:(yr*B.ce-zr*B.se) };
  }
  function normal(a,b,c){
    const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
    const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m];
  }
  function shadeOf(nn, se, ce){ return nn[0]*LN[0] + (nn[1]*se+nn[2]*ce)*LN[1] + (-nn[1]*ce+nn[2]*se)*LN[2]; }

  function _paint(o, pitch, yaw, zOff){
    const B=camBasis(o);
    const fr=frameOf(pitch,yaw,zOff);
    const faces=facesOf(fr);
    const zbuf=new Float32Array(W*H).fill(Infinity);
    const col=new Array(W*H).fill(null);
    const dep=new Float32Array(W*H);
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
      let nn=normal(rv[0],rv[1],rv[2]);
      let sh=shadeOf(nn, B.se, B.ce); if(sh<0 && ((f.b||0)<=-1)) sh=shadeOf([-nn[0],-nn[1],-nn[2]], B.se, B.ce)*0.9;
      const fidx = sh*GAIN + BIAS + (f.b||0);
      const M = MATS[f.mat] || MATS.fitt;
      for(let tt=1;tt+1<rv.length;tt++) fillTri(rv[0],rv[tt],rv[tt+1]);
      function fillTri(a,b,c){
        const minX=Math.max(0,Math.floor(Math.min(a.sx,b.sx,c.sx)));
        const maxX=Math.min(W-1,Math.ceil(Math.max(a.sx,b.sx,c.sx)));
        const minY=Math.max(0,Math.floor(Math.min(a.sy,b.sy,c.sy)));
        const maxY=Math.min(H-1,Math.ceil(Math.max(a.sy,b.sy,c.sy)));
        const area=(b.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b.sy-a.sy);
        if(Math.abs(area)<1e-6) return;
        for(let y=minY;y<=maxY;y++) for(let x=minX;x<=maxX;x++){
          const px=x+0.5, py=y+0.5;
          let w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area;
          let w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area;
          let w2=1-w0-w1;
          if(w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const d=w0*a.d+w1*b.d+w2*c.d, deff=d-(f.db||0);
          const i=y*W+x;
          if(deff<zbuf[i]){
            zbuf[i]=deff; dep[i]=d;
            let base=Math.floor(fidx);
            let idx=base+((fidx-base)>BAYER[x&3][y&3]?1:0)+M.off;
            col[i]=M.ramp[Math.max(0,Math.min(M.ramp.length-1,idx))];
          }
        }
      }
    }
    // procedural shaft — unbroken 2px ash pole at any pitch (thin quads would gap)
    const sA=-0.295, sB=0.325, steps=Math.ceil((sB-sA)*S*2.4);
    const plot=(x,y,c,d)=>{ if(x<0||x>=W||y<0||y>=H||!c) return; const i=y*W+x; if(d-0.03<zbuf[i]){ zbuf[i]=d-0.03; dep[i]=d; col[i]=c; } };
    let px0=null, py0=null;
    for(let i=0;i<=steps;i++){
      const s=sA+(sB-sA)*i/steps, frac=(s-sA)/(sB-sA);
      const p=fr.at(s), v=projVert(p[0],p[1],p[2],B);
      const x=Math.floor(v.sx), y=Math.floor(v.sy);
      const idx=Math.max(0,Math.min(WOOD.length-1, frac<0.18 ? 2 : (frac>0.82 ? 4 : 3)));
      plot(x,y,WOOD[idx],v.d);
      if(px0!=null && (x!==px0||y!==py0)){
        if(Math.abs(x-px0)>=Math.abs(y-py0)) plot(x,y+1,WOOD[Math.max(0,idx-2)],v.d);
        else plot(x+1,y,WOOD[Math.max(0,idx-2)],v.d);
      }
      px0=x; py0=y;
    }
    const out=new Array(W*H).fill(null);
    for(let i=0;i<W*H;i++) out[i]=col[i];
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){
      const i=y*W+x; if(!col[i]) continue;
      for(const [dx,dy] of [[1,0],[0,1]]){
        const nx=x+dx, ny=y+dy; if(nx>=W||ny>=H) continue;
        const j=ny*W+nx; if(!col[j]) continue;
        if(Math.abs(dep[i]-dep[j])>EDGE){
          const far=dep[i]>dep[j]?i:j, e=RINDEX[col[far]];
          if(e && e.i>0) out[far]=e.r[Math.max(0,e.i-2)];
        }
      }
    }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){
      const i=y*W+x; if(out[i]) continue;
      let touch=false;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){
        const nx=x+dx, ny=y+dy;
        if(nx>=0&&nx<W&&ny>=0&&ny<H&&col[ny*W+nx]){ touch=true; break; }
      }
      if(touch) out[i]=KEY;
    }
    return out;
  }
  function _toRGBA(out){
    const rgba=new Uint8ClampedArray(W*H*4);
    for(let i=0;i<W*H;i++){
      const c=out[i]; if(!c){ rgba[i*4+3]=0; continue; }
      rgba[i*4]=parseInt(c.slice(1,3),16); rgba[i*4+1]=parseInt(c.slice(3,5),16);
      rgba[i*4+2]=parseInt(c.slice(5,7),16); rgba[i*4+3]=255;
    }
    return rgba;
  }

  function resolveR(dir, opts){
    opts=opts||{};
    const o=Object.assign({},opts,{dir});
    if(opts.rest==='ground')   // flat prop, blade face up; pivot = ground under the grip
      return { o, pitch:0, yaw:0, zOff:0.024 };
    if(opts.rest==='stored')   // planted upright, blade tip in the ground
      return { o, pitch:-78*DEG, yaw:0, zOff:0.60 };
    return { o, pitch:opts.pitch!=null?opts.pitch:-34*DEG,
             yaw:opts.yaw!=null?opts.yaw:8*DEG, zOff:0 };
  }
  function render(dir, opts){
    const {o,pitch,yaw,zOff}=resolveR(dir,opts);
    return _toRGBA(_paint(o,pitch,yaw,zOff));
  }
  function tipLocal(opts){
    const {pitch,yaw,zOff}=resolveR(0,opts);
    return frameOf(pitch,yaw,zOff).at(SPEC.lenDn);
  }
  function tip(dir, opts){
    const {o,pitch,yaw,zOff}=resolveR(dir,opts);
    const p=frameOf(pitch,yaw,zOff).at(SPEC.lenDn);
    const v=projVert(p[0],p[1],p[2],camBasis(o));
    return { x:v.sx, y:v.sy };
  }
  // character-local 3D point -> screen px OFFSET from the character's ground pivot (spoil FX)
  function project(dir, p, elev){
    const B=camBasis({dir, elev});
    const xr=p[0]*B.ct - p[1]*B.stt, yr=p[0]*B.stt + p[1]*B.ct;
    return { dx:xr*S, dy:-(yr*B.se+p[2]*B.ce)*S };
  }

  root.ShovelIso = { W, H, pivot:{x:cx,y:cy}, SPEC, DIG, SPOIL, REST:['ground','stored'],
    behind:[7,0,1],   // shovel layers under the sprite for the away facings (NW / N / NE)
    defaultElev:DEFAULT_ELEV, KEY,
    ramps:{ WOOD, GRAPH, STEEL },
    render, tip, tipLocal, project };
})(typeof globalThis!=='undefined'?globalThis:window);
