/* Hidden Harbours — parametric ISO dory (M2 bake recipe, ADR-0006).
   Pre-rendered 3D -> 8-direction sprite sheet. A little flat-bottomed Banks dory modelled
   from an offsets table, viewed through a fixed 3/4 turntable camera (elevation 40deg default,
   adjustable per render — higher shows more deck) and rotated in 45deg steps. Flat-facet shading from a fixed
   upper-left key (screen space, per the art bible), z-buffered, explicit ordered dither,
   1px keyline post-pass, NO AA. Palette-clamped to the existing Dory wood ramp.
   32 px = 1 m. Empty hull (rower/oars are separate sprites).

   BUILD AXIS: render(dir,{build:'oars'|'motor'}). 'oars' = the pulling boat as baked before (unchanged
   pixels). 'motor' = the outboard upgrade: a STERN BENCH (new seating position, tiller in reach) plus a
   transom MOTOR BOARD (outer clamp pad + inner doubler + cheeks + bolts) that strengthens the narrow
   dory transom for a clamp-on outboard. Oars, thole pins and both rowing thwarts stay (motor is an
   upgrade, not a conversion). The small outboard itself is its own overlay layer, doryMotorRig.js
   (globalThis.DoryMotor — a little tiller two-stroke, cell 196x164 pivot (98,92), pivot-aligned to
   this hull) — this rig bakes the clamp point it registers to: motorMount(dir,opts).

   Cell 160x156 per direction; boat origin (amidships, keel bottom, centreline) projects to
   the SAME screen pivot for every heading, so a direction swap never shifts placement.
   Exposes globalThis.DoryIso = { W, H, PX, DIRS, pivot, order, ROCK, rock(i), render(dir, {elev,roll,pitch,heave}) -> Uint8ClampedArray }.
*/
(function (root) {
  const PX = 32, S = 32;                 // px per metre / projection scale
  const W = 160, H = 156, cx = 80, cy = 88;   // cell + pivot (= projection of boat origin); taller cell + lower pivot for the higher camera
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;               // camera elevation, degrees above the horizon (higher = more deck). Overridable per render.
  const ROCK = { frames: 8, rollA: 5.0, pitchA: 3.0, heaveA: 1.6, period: 2.6 };  // gentle wave-rock loop
  function rockMotion(i, frames){        // one frame of the rock cycle -> {roll,pitch,heave}
    frames = frames || ROCK.frames;
    const a = 2*Math.PI*(((i%frames)+frames)%frames)/frames;
    return { roll: ROCK.rollA*Math.sin(a), pitch: ROCK.pitchA*Math.sin(a+Math.PI/2), heave: ROCK.heaveA*Math.sin(a) };
  }
  const L = 4.5, TH = 0.035, FLOOR = 0.06, SEAT = 0.30, OARLOCK_U = 0.31;
  const STERN_U = 0.13;                  // stern-bench station (motor build) -> y = -1.665 m, 0.58 m fwd of the transom
  const NSEG = 18, NST = 3;              // length segments / side strakes

  // wood ramp dark->light (sampled from Art/Boats/Dory.png) + keyline + iron
  const RAMP = ['#33271b','#473627','#5e4630','#6b4f35','#8a6a48','#9a7853','#a98352'];
  const KEY = '#1c140d';
  const IRON = ['#20180f','#2a2014','#3a2c1c'];
  const GAIN = 3.0, BIAS = 2.7;
  // light (screen basis: right, up, toward-camera) — upper-left key, a touch frontal
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  // ---- offsets table: stern(0) -> bow(8). [sheerHalf, bottomHalf, depth, keelZ] (m) ----
  const T = [
    [0.44,0.17,0.56,0.14],
    [0.60,0.23,0.50,0.06],
    [0.70,0.26,0.47,0.02],
    [0.74,0.26,0.46,0.00],
    [0.75,0.25,0.46,0.00],
    [0.72,0.23,0.47,0.01],
    [0.63,0.18,0.49,0.05],
    [0.45,0.11,0.53,0.14],
    [0.05,0.025,0.60,0.26],
  ];
  const lerp = (a,b,t)=>a+(b-a)*t;
  function station(u){
    const f=Math.max(0,Math.min(8,u*8)), i=Math.min(7,Math.floor(f)), fr=f-i;
    const A=T[i], B=T[i+1];
    return { ws:lerp(A[0],B[0],fr), wb:lerp(A[1],B[1],fr), dep:lerp(A[2],B[2],fr),
             kz:lerp(A[3],B[3],fr), y:-L/2+u*L };
  }
  // hull-skin point. side=+/-1, frac 0(bottom)->1(sheer). inset -> inner skin.
  function skin(side,u,frac,inset){
    const st=station(u);
    const ws=st.ws-(inset?TH:0), wb=st.wb-(inset?TH*0.6:0), dep=st.dep-(inset?0.02:0);
    return [ side*lerp(wb,ws,frac), st.y, st.kz+lerp(0,dep,frac) ];
  }
  function floorPt(side,u){ const st=station(u); return [ side*(st.wb-TH*0.6)*0.94, st.y, st.kz+FLOOR ]; }
  // inner half-width AT a given height — thwarts/benches must fit the flare at their LOWEST face,
  // otherwise the ends poke out through the planking (the hull narrows as you go down).
  function halfAt(u,z,inset){
    const st=station(u);
    const ws=st.ws-(inset?TH:0), wb=st.wb-(inset?TH*0.6:0), dep=st.dep-(inset?0.02:0);
    return lerp(wb, ws, Math.max(0,Math.min(1,(z-st.kz)/dep)));
  }

  // ---- face list (boat space), built once ----
  const F = [];    // hull + rowing fit-out (both builds)
  const FM = [];   // motor-upgrade extras (build:'motor')
  const face=(v,mat,b,db)=>F.push({v,mat:mat||'wood',b:b||0,db:db||0});
  const faceM=(v,mat,b,db)=>FM.push({v,mat:mat||'wood',b:b||0,db:db||0});
  function boxFaces(c,h,mat,b,db){       // axis-aligned block, outward winding
    const P=(sx,sy,sz)=>[c[0]+sx*h[0], c[1]+sy*h[1], c[2]+sz*h[2]];
    const f=(v)=>({v,mat,b:b||0,db:db||0});
    return [
      f([P(-1,-1,1),P(1,-1,1),P(1,1,1),P(-1,1,1)]),
      f([P(-1,1,-1),P(1,1,-1),P(1,-1,-1),P(-1,-1,-1)]),
      f([P(-1,1,1),P(1,1,1),P(1,1,-1),P(-1,1,-1)]),
      f([P(1,-1,1),P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1)]),
      f([P(1,1,1),P(1,-1,1),P(1,-1,-1),P(1,1,-1)]),
      f([P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1)]),
    ];
  }
  (function build(){
    for(const side of [-1,1]){
      for(let i=0;i<NSEG;i++){
        const u0=i/NSEG, u1=(i+1)/NSEG;
        for(let k=0;k<NST;k++){
          const f0=k/NST, f1=(k+1)/NST;
          // outer strake
          face([skin(side,u0,f0),skin(side,u1,f0),skin(side,u1,f1),skin(side,u0,f1)],'wood',0);
          // clinker seam (thin, biased forward so it wins the coplanar test)
          if(k<NST-1){ const fs=f1-0.06;
            face([skin(side,u0,fs),skin(side,u1,fs),skin(side,u1,f1),skin(side,u0,f1)],'wood',-2.2,0.02); }
          // inner strake (darker interior)
          face([skin(side,u1,f0,1),skin(side,u0,f0,1),skin(side,u0,f1,1),skin(side,u1,f1,1)],'wood',-1.1);
        }
        // bottom (underside) + interior floor
        face([floorPt(-1,u0),floorPt(1,u0),floorPt(1,u1),floorPt(-1,u1)],'wood',-0.4);
        face([skin(-1,u0,0),skin(-1,u1,0),skin(1,u1,0),skin(1,u0,0)],'wood',-1.0);
        // gunwale cap (flat rail top)
        const oa=skin(side,u0,1),ob=skin(side,u1,1),ia=skin(side,u0,1,1),ib=skin(side,u1,1,1);
        const inb=(p)=>[p[0]-side*TH*1.3,p[1],p[2]];
        face([oa,ob,inb(ib),inb(ia)],'wood',0.4,0.03);
      }
    }
    // transom: vertical stern board — top corners sit exactly on the aftmost sheer, so the corner closes with no crack
    const SL=skin(-1,0,1), SR=skin(1,0,1), BR=skin(1,0,0), BL=skin(-1,0,0);
    face([SL,SR,BR,BL],'wood',-0.8);
    // (bow closes as a sharp stem where the two skins meet at the centreline — no separate post)
    // thwarts (two seats)
    for(const u of [0.34,0.60]){
      const st=station(u), zTop=st.kz+SEAT, zBot=zTop-0.05, bd=0.20, hx=halfAt(u,zBot,1)-0.006;
      const y0=st.y-bd/2, y1=st.y+bd/2;
      face([[-hx,y0,zTop],[hx,y0,zTop],[hx,y1,zTop],[-hx,y1,zTop]],'wood',0.6);   // top
      face([[-hx,y1,zTop],[hx,y1,zTop],[hx,y1,zBot],[-hx,y1,zBot]],'wood',-1.2);  // aft face
      face([[hx,y0,zTop],[-hx,y0,zTop],[-hx,y0,zBot],[hx,y0,zBot]],'wood',-0.4);  // fwd face
    }
    // bottom boards (two battens flanking the keel line — footing over the bilge, and they read the length)
    for(const sx of [-1,1]){
      const NB=7, uA=0.17, uB=0.80;
      for(let i=0;i<NB;i++){
        const ua=uA+(uB-uA)*i/NB, ub=uA+(uB-uA)*(i+1)/NB;
        const sa=station(ua), sb=station(ub);
        const xa0=sx*0.06, xa1=sx*0.14, za=sa.kz+FLOOR+0.03, zb=sb.kz+FLOOR+0.03;
        const q=[[xa0,sa.y,za],[xa1,sa.y,za],[xa1,sb.y,zb],[xa0,sb.y,zb]];
        face(sx>0?q:[q[1],q[0],q[3],q[2]],'wood',0.35,0.03);
      }
    }
    // breasthook (the plate that closes the stem) + the stem-head RING BOLT the painter lives on
    face([skin(-1,0.93,1),skin(1,0.93,1),skin(1,1,1),skin(-1,1,1)],'wood',0.9,0.04);
    function ringBolt(x,y,z,ro,ri,tk,b){    // 4-bar iron ring; nothing under the middle, so the hole reads as wood
      const bar=(ro-ri)/2, off=(ro+ri)/2;
      F.push(...boxFaces([x,y+off,z],[ro,bar,tk],'iron',b,-0.05));                     // fwd bar
      F.push(...boxFaces([x,y-off,z],[ro,bar,tk],'iron',b-0.3,-0.05));                 // aft bar
      F.push(...boxFaces([x-off,y,z],[bar,ri,tk],'iron',b,-0.05));                     // port bar
      F.push(...boxFaces([x+off,y,z],[bar,ri,tk],'iron',b-0.25,-0.05));                // stbd bar
    }
    (function(){ const st=station(0.965); ringBolt(0, st.y, st.kz+st.dep+0.03, 0.052, 0.022, 0.015, 0.6); })();
    // quarter knees (sheer-level plates tying the transom corners into the sides)
    for(const side of [-1,1]){
      const c=skin(side,0,1), f=skin(side,0.085,1), inb=[side*0.15,-L/2,c[2]];
      face(side>0?[c,f,inb]:[c,inb,f],'wood',0.8,0.04);
    }
    // port-quarter ring bolt through the knee — the stern line when she lies alongside a dock
    ringBolt(-0.30, -2.16, T[0][3]+T[0][2]+0.026, 0.05, 0.018, 0.014, 0.5);
    // oarlock thole pins (amidships, both sides)
    for(const side of [-1,1]){
      const st=station(OARLOCK_U), x=side*(st.ws-TH*0.5), zt2=st.kz+st.dep;
      face([[x-0.03,st.y-0.03,zt2+0.12],[x+0.03,st.y-0.03,zt2+0.12],[x+0.03,st.y+0.03,zt2+0.12],[x-0.03,st.y+0.03,zt2+0.12]],'iron',0.5,0.02);
      face([[x-0.03,st.y+0.03,zt2+0.12],[x+0.03,st.y+0.03,zt2+0.12],[x+0.03,st.y+0.03,zt2],[x-0.03,st.y+0.03,zt2]],'iron',-0.5,0.02);
    }
  })();

  // ---- motor upgrade (build:'motor') ----
  // A clamp-on outboard needs two things this dory hasn't got: somewhere aft to SIT with the tiller in
  // reach, and enough transom to clamp to. TR = transom plane + sheer height at the stern.
  const TR = { y:-L/2, zTop:T[0][3]+T[0][2], halfW:T[0][0] };
  const BOARD = { halfW:0.30, t:0.045, rise:0.10, drop:0.16 };   // clamp pad: 0.60 x 0.26 m, 45 mm thick, stands 100 mm proud of the sheer
  (function buildMotor(){
    // stern bench — the new seating position (deeper than a rowing thwart so it reads as a seat, not a brace)
    const st=station(STERN_U), zTop=st.kz+SEAT, zBot=zTop-0.055, bd=0.26, hx=halfAt(STERN_U,zBot,1)-0.006;
    const y0=st.y-bd/2, y1=st.y+bd/2;
    faceM([[-hx,y0,zTop],[hx,y0,zTop],[hx,y1,zTop],[-hx,y1,zTop]],'wood',0.75);   // top (fresh board, lit)
    faceM([[-hx,y1,zTop],[hx,y1,zTop],[hx,y1,zBot],[-hx,y1,zBot]],'wood',-1.2);   // fwd face
    faceM([[hx,y0,zTop],[-hx,y0,zTop],[-hx,y0,zBot],[hx,y0,zBot]],'wood',-0.4);   // aft face
    // outer motor board: bolted across the transom, standing proud of the sheer = the clamp face.
    // Written face-by-face (not a plain block) so the fresh top cap catches the key and the pad reads
    // as an added board from every heading, including the stern-away ones where only its top edge shows.
    const zT=TR.zTop+BOARD.rise, zB=TR.zTop-BOARD.drop, yA=TR.y-BOARD.t*2, yF=TR.y, hw=BOARD.halfW;
    faceM([[-hw,yA,zT],[hw,yA,zT],[hw,yF,zT],[-hw,yF,zT]],'wood',0.95,-0.06);      // top cap
    faceM([[hw,yA,zT],[-hw,yA,zT],[-hw,yA,zB],[hw,yA,zB]],'wood',0.45,-0.05);       // aft clamp face
    faceM([[hw,yF,zT],[hw,yA,zT],[hw,yA,zB],[hw,yF,zB]],'wood',0.2,-0.05);         // stbd end grain
    faceM([[-hw,yA,zT],[-hw,yF,zT],[-hw,yF,zB],[-hw,yA,zB]],'wood',0.2,-0.05);     // port end grain
    // inner doubler + two cheeks — spread the clamp load into the transom
    FM.push(...boxFaces([0, TR.y+0.05, TR.zTop-0.09],[0.275, 0.05, 0.07],'wood',-0.15,-0.05));
    for(const sx of [-1,1]) FM.push(...boxFaces([sx*0.245, TR.y+0.115, TR.zTop-0.1075],[0.035, 0.065, 0.0625],'wood',-0.35,-0.05));
    // bolt heads through the board (4)
    for(const sx of [-1,1]) for(const bzz of [TR.zTop-0.09, TR.zTop+0.045])
      FM.push(...boxFaces([sx*0.205, TR.y-BOARD.t*2-0.004, bzz],[0.022, 0.006, 0.022],'iron',0.6,-0.06));
    // motor lanyard eye on the inner doubler (the safety line that keeps a kicker out of the harbour)
    FM.push(...boxFaces([-0.115, TR.y+0.05, TR.zTop-0.005],[0.025,0.02,0.03],'iron',0.55,-0.06));
  })();

  // ---- shared rasterizer ----
  function normal(a,b,c){
    const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
    const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m];
  }
  function shadeOf(n, se, ce){
    let sh = n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2];
    return sh;
  }
  function camBasis(opts){
    const dir=opts.dir||0, th=dir*Math.PI/4;
    const e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    const roll=(opts.roll||0)*DEG, pitch=(opts.pitch||0)*DEG;
    return { ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e),
      cr:Math.cos(roll), sr:Math.sin(roll), cq:Math.cos(pitch), sq:Math.sin(pitch), heave:(opts.heave||0) };
  }
  function projVert(x,y,z,B){
    const x1=x*B.cr+z*B.sr, z1=-x*B.sr+z*B.cr;      // roll about fore-aft (Y)
    const y2=y*B.cq - z1*B.sq, z2=y*B.sq + z1*B.cq;  // pitch about beam (X)
    const xr=x1*B.ct - y2*B.stt, yr=x1*B.stt + y2*B.ct, zr=z2;
    return { xr,yr,zr, sx:cx+xr*S, sy:cy-(yr*B.se+zr*B.ce)*S - B.heave, d:(yr*B.ce-zr*B.se) };
  }
  function _paint(faces, opts, doEdge){
    const B=camBasis(opts);
    const zbuf=new Float32Array(W*H).fill(Infinity);
    const col=new Array(W*H).fill(null);
    const dep=new Float32Array(W*H);
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
      let n=normal(rv[0],rv[1],rv[2]);
      let sh=shadeOf(n, B.se, B.ce); if(sh<0 && ((f.b||0)<=-1)) sh=shadeOf([-n[0],-n[1],-n[2]], B.se, B.ce)*0.9; // interior two-sided
      const fidx = sh*GAIN + BIAS + (f.b||0);
      for(let t=1;t+1<rv.length;t++) fillTri(rv[0],rv[t],rv[t+1]);
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
            let base=Math.floor(fidx); const fr=fidx-base;
            let idx=base+(fr>BAYER[x&3][y&3]?1:0);
            if(f.mat==='iron'){ col[i]=IRON[Math.max(0,Math.min(2,idx-2))]; }
            else { col[i]=RAMP[Math.max(0,Math.min(6,idx))]; }
          }
        }
      }
    }
    const out=new Array(W*H).fill(null);
    for(let i=0;i<W*H;i++) out[i]=col[i];
    if(doEdge){
      // inner depth-edge separation: darken the farther side of a big depth step
      for(let y=0;y<H;y++) for(let x=0;x<W;x++){
        const i=y*W+x; if(!col[i]) continue;
        for(const [dx,dy] of [[1,0],[0,1]]){
          const nx=x+dx, ny=y+dy; if(nx>=W||ny>=H) continue;
          const j=ny*W+nx; if(!col[j]) continue;
          if(Math.abs(dep[i]-dep[j])>0.30){
            const far=dep[i]>dep[j]?i:j, cc=col[far], k=RAMP.indexOf(cc);
            if(k>0) out[far]=RAMP[Math.max(0,k-2)];
          }
        }
      }
    }
    // keyline outline (external)
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
  function render(dir, opts){
    opts = (typeof opts==='number') ? {elev:opts} : (opts||{});
    const motor = opts.build==='motor' || opts.motor===true;
    return _toRGBA(_paint(motor ? F.concat(FM) : F, Object.assign({}, opts, {dir}), true));
  }

  // outboard clamp point — top centre of the motor board's aft (clamp) face, in hull-cell coords.
  // The small-motor layer (later pass on the outboard rig) registers its swivel axis to this point.
  const MOUNT = { x:0, y:TR.y-BOARD.t*2, z:TR.zTop+BOARD.rise };
  function motorMount(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), p=projVert(MOUNT.x, MOUNT.y-0.01, MOUNT.z, B);
    return { x:p.sx, y:p.sy };
  }
  // seated operator anchor: stern-bench top centre (motor build)
  const HELM = (()=>{ const st=station(STERN_U); return { x:0, y:st.y, z:st.kz+SEAT }; })();
  function helmSeat(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), p=projVert(HELM.x, HELM.y, HELM.z, B);
    return { x:p.sx, y:p.sy };
  }

  // ---- oars (separate overlay layer, same camera + pivot as the hull) ----
  const OAR = { rowFrames: 8, states: ['row','resting','trailing'] };
  const O_LOUT=1.45, O_LIN=0.85, O_TS=0.055, O_BLEN=0.5, O_BW=0.12;   // outboard/inboard len, loom radius, blade len/max half-width (m)
  const v_sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]], v_add=(a,b)=>[a[0]+b[0],a[1]+b[1],a[2]+b[2]];
  const v_mul=(a,s)=>[a[0]*s,a[1]*s,a[2]*s], v_norm=(a)=>{const m=Math.hypot(a[0],a[1],a[2])||1;return [a[0]/m,a[1]/m,a[2]/m];};
  const v_cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
  function oarlockPt(side){ const st=station(OARLOCK_U); return [ side*(st.ws-TH*0.15), st.y, st.kz+st.dep+0.04 ]; }
  function oarDir(side, sweepDeg, dipDeg){ const f=sweepDeg*DEG, p=dipDeg*DEG; return [ side*Math.cos(f)*Math.cos(p), -Math.sin(f)*Math.cos(p), -Math.sin(p) ]; }
  function oarPose(state, t){
    if(state==='resting')  return { sweep:-82, dip:-7 };   // shipped fore, lying along the gunwale
    if(state==='trailing') return { sweep: 76, dip: 15 };  // dragging aft in the water
    const tt=(((t||0)%1)+1)%1, a=2*Math.PI*tt;             // row cycle: blade traces an ellipse (dip+sweep)
    return { sweep: 30*Math.sin(a), dip: 6 + 22*Math.cos(a) };
  }
  function buildOar(side, pose){
    const O=oarlockPt(side), d=oarDir(side,pose.sweep,pose.dip);
    const Bt=v_add(O, v_mul(d, O_LOUT));                    // blade tip
    const Hp=v_sub(O, v_mul(d, O_LIN));                     // handle (straight lever through the lock)
    const ax=v_norm(v_sub(Bt,Hp)); let up=[0,0,1]; if(Math.abs(ax[2])>0.9) up=[0,1,0];
    const r=v_norm(v_cross(ax,up)), u=v_cross(r,ax);
    const ring=(P,rad)=>[ v_add(v_add(P,v_mul(r,rad)),v_mul(u,rad)), v_add(v_add(P,v_mul(r,-rad)),v_mul(u,rad)),
                          v_add(v_add(P,v_mul(r,-rad)),v_mul(u,-rad)), v_add(v_add(P,v_mul(r,rad)),v_mul(u,-rad)) ];
    const P0=v_add(Hp,v_mul(ax,0.02)), P1=v_sub(Bt,v_mul(ax,O_BLEN)), faces=[];
    const r0=ring(P0,O_TS), r1=ring(P1,O_TS);
    for(let k=0;k<4;k++){ const k2=(k+1)%4; faces.push({v:[r0[k],r0[k2],r1[k2],r1[k]],mat:'wood',b:0.3,db:-0.15}); }  // loom
    // paddle-shaped blade: narrow throat -> shoulder swell -> rounded tip (width profile along blade)
    const prof=[[0,0.05],[0.25,0.085],[0.6,O_BW],[0.85,O_BW*0.92],[1,0.035]];
    let prev=null;
    for(const [s,w] of prof){
      const P=v_add(P1,v_mul(ax,O_BLEN*s)), e0=v_add(P,v_mul(r,w)), e1=v_add(P,v_mul(r,-w));
      if(prev){ const q=[prev[0],e0,e1,prev[1]], sh=-0.35-0.1*s;
        faces.push({v:q,mat:'wood',b:sh,db:-0.2}); faces.push({v:[q[3],q[2],q[1],q[0]],mat:'wood',b:sh,db:-0.2}); }
      prev=[e0,e1];
    }
    return faces;
  }
  function sidePose(opts, side){ const o=(side<0?opts.port:opts.star)||{}; return oarPose(o.state||opts.state||'row', (o.t!==undefined?o.t:opts.t)||0); }
  function oarFaces(opts){ const ss = opts.side==='port'?[-1]:opts.side==='star'?[1]:[-1,1];
    return ss.reduce((f,s)=>f.concat(buildOar(s,sidePose(opts,s))),[]); }
  function renderOars(dir, opts){
    opts = (typeof opts==='number') ? {elev:opts} : (opts||{});
    return _toRGBA(_paint(oarFaces(opts), Object.assign({}, opts, {dir}), false));
  }
  function oarHandles(dir, opts){   // per-side handle screen position (cell coords) for attaching a character's hands
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), res=[];
    for(const side of [-1,1]){ const pose=sidePose(opts,side), O=oarlockPt(side), d=oarDir(side,pose.sweep,pose.dip);
      const Hp=v_sub(O,v_mul(d,O_LIN)), p=projVert(Hp[0],Hp[1],Hp[2],B); res.push({side:side<0?'port':'star', x:p.sx, y:p.sy}); }
    return res;
  }

  // ---- fish-tub deck mount (one, between the thwarts — cargo rides behind the rower) ----
  const TUBS = [ {x:0,y:-0.14} ].map(m=>{ const st=station((m.y+L/2)/L); return {x:m.x,y:m.y,z:st.kz+FLOOR}; });
  function tubMounts(dir, opts){   // hull-cell px anchors; pass rock(i) so anchors ride the wave (incl. heave)
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts);
    return TUBS.map(m=>{ const p=projVert(m.x,m.y,m.z,B); return {x:p.sx, y:p.sy}; });
  }

  // bow painter eye — mooring / tow / docking line attach (both builds), in hull-cell coords
  const PAINTER = (()=>{ const st=station(0.965); return { x:0, y:st.y, z:st.kz+st.dep+0.055 }; })();
  function painter(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), p=projVert(PAINTER.x, PAINTER.y, PAINTER.z, B);
    return { x:p.sx, y:p.sy };
  }
  // port-quarter eye — the stern line when she lies alongside (both builds)
  const STERN_EYE = { x:-0.30, y:-2.16, z:T[0][3]+T[0][2]+0.045 };
  function sternEye(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), p=projVert(STERN_EYE.x, STERN_EYE.y, STERN_EYE.z, B);
    return { x:p.sx, y:p.sy };
  }
  // pilot foot-contact on the sole — rowing station (feet planted to work the oars), or the motor
  // station just forward of the stern bench when build:'motor'. Rides the wave.
  const PILOT = { x:0, y:-0.30 };
  const PILOT_MOTOR = { x:0, y:-1.28 };
  function pilotStand(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const P = (opts.build==='motor' || opts.motor===true) ? PILOT_MOTOR : PILOT;
    const st=station((P.y+L/2)/L), B=camBasis(opts), p=projVert(P.x, P.y, st.kz+FLOOR, B);
    return { x:p.sx, y:p.sy };
  }

  root.DoryIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:cy}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'], BUILDS:['oars','motor'], defaultBuild:'oars',
    RAMP, KEY, IRON, render, ROCK, rock:rockMotion,
    renderOars, oarHandles, OAR, TUBS, tubMounts, PILOT, PILOT_MOTOR, pilotStand,
    STERN_U, BOARD, MOUNT, motorMount, HELM, helmSeat, PAINTER, painter, STERN_EYE, sternEye };
})(typeof globalThis!=='undefined'?globalThis:window);
