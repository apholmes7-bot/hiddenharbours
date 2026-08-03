/* Hidden Harbours — WHEEL RIG. The centre-console skiff's steering wheel as its own spinnable
   control layer, the way tillerRig.js is the dory's tiller: one destroyer wheel, own cell, own hub
   pivot, nothing else in the frame. consoleRig.js draws a wheel as part of its dash; this is the
   wheel the PLAYER grabs — it owns the spin physics as well as the pixels, so the game, the helm
   view and any bake all agree on what a given wheel angle means.

   DRAWN AT ANGLE, NOT ROTATED, AND PER PIXEL. A baked sprite spun with a rotation matrix takes its
   highlights round with it, so the light appears to orbit the boat. Here every pixel of the cell is
   solved for what it is part of — rim section, spoke, turned knob, hub — and shaded from a FIXED
   upper-left key with a cylinder cross-section term. Nothing is stroked, so there are no gaps and
   every pixel lands on a ramp step.

   SPIN MODEL (rig-owned, so it is the same everywhere):
     lock-to-lock  = turns * 360 deg each way (1.5 turns default — cable steer on a 7 m skiff)
     steer         = deg / lock, clamped to -1..+1
     rudder/motor  = steer * maxRudder (32 deg, matching the punt's outboard swing)
     step(s,dt)    = coast with friction, HARD STOP at lock with a small bounce, optional self-centre
     clicks(deg)   = knobs passed since centre, for the per-knob tick the audio hooks into
   A working cable helm has no return spring: released, it coasts and HOLDS. selfCentre>0 opts into a
   springy arcade feel instead.

   Exposes globalThis.WheelRig = { W,H,pivot,R,RIMW,KNOBS,SPOKES,KNOBL, RIMS,rimIds,defaultRim,
     maxRudder, degFromSteer, steerFromDeg, rudderDeg, lockDeg, clicks, step,
     paint(ctx,cx,cy,o), render(o), sheet(n,o), GOLD,STEEL,GRAPH,RUBBER,TEAK }. */
(function (root) {
  const W = 256, H = 256, PVX = 128, PVY = 128;      // cell + hub pivot (centre)
  const R = 92, RIMW = 15;                           // rim centreline radius / section thickness
  const KNOBS = 8, SPOKES = 8, KNOBL = 19;           // turned handles, one per spoke
  const DEG = Math.PI/180;
  const rI = R - Math.round(RIMW/2), rO = R + Math.round(RIMW/2);
  const HUBR = 24;

  // ---- palettes (cool graphite + rubber, shared idiom with tillerRig/consoleRig) ----
  const GRAPH  = ['#12171b','#1b2228','#28323a','#3a4750','#4f5e68','#6b7c86'];
  const RUBBER = ['#0b0e11','#11161a','#1a2126','#252e35','#333f47','#42505a'];
  const STEEL  = ['#232a30','#39434b','#556069','#7a8892','#9db0b8'];
  const TEAK   = ['#2b2016','#3f2f20','#57402a','#6d5134','#8a6a42','#a5834f'];
  const GOLD   = ['#7a5a1c','#a8842a','#e0b13a'];    // fleet cove gold — the king spoke
  const TEAL   = '#7fd6c9';

  const RIMS = {
    rubber: { name:'Rubber Grip', rim:RUBBER, knob:GRAPH, spoke:STEEL, note:'stock helm — moulded rim, graphite knobs' },
    teak:   { name:'Oiled Teak',  rim:TEAK,   knob:TEAK,  spoke:STEEL, note:'turned teak rim and handles' },
    steel:  { name:'Chromed',     rim:STEEL,  knob:STEEL, spoke:STEEL, note:'polished stainless, bright rim' },
  };
  const DEFAULT_RIM = 'rubber';

  const cv=(w,h)=>{ const c=document.createElement('canvas'); c.width=Math.round(w); c.height=Math.round(h); return c; };
  const clamp=(v,a,b)=>v<a?a:v>b?b:v;
  const clamp01=(v)=>v<0?0:v>1?1:v;
  const LIGHT = (()=>{ const v=[-0.60,-0.72]; const m=Math.hypot(v[0],v[1]); return [v[0]/m,v[1]/m]; })();
  const LZ = 0.62;                                    // light's out-of-screen component
  const hex2rgb=(h)=>[parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)];
  function rampRGB(ramp){ return ramp.map(hex2rgb); }
  const RGB={}; [['GRAPH',GRAPH],['RUBBER',RUBBER],['STEEL',STEEL],['TEAK',TEAK],['GOLD',GOLD]].forEach(([k,r])=>RGB[k]=rampRGB(r));
  function rgbOf(ramp){ if(!ramp.__rgb) Object.defineProperty(ramp,'__rgb',{value:rampRGB(ramp),enumerable:false}); return ramp.__rgb; }
  const pickRGB=(ramp,t)=>rgbOf(ramp)[clamp(Math.round(clamp01(t)*(ramp.length-1)),0,ramp.length-1)];

  // cylinder shading: q = lateral offset across the section (-1..1), axis dir (ax,ay) in screen space
  function cylShade(q, ax, ay){
    const px=-ay, py=ax;                              // perpendicular = across the barrel
    const nz=Math.sqrt(Math.max(0,1-q*q));
    const d = (px*q)*LIGHT[0] + (py*q)*LIGHT[1] + nz*LZ;
    return clamp01(0.16 + 0.84*clamp01(d));
  }

  // ---- the wheel, solved per pixel at a given angle under a fixed light ----
  function bake(deg, o){
    o=o||{};
    const M = RIMS[RIMS[o.rim]?o.rim:DEFAULT_RIM];
    const RIM=M.rim, KN=M.knob, SP=M.spoke;
    const c=cv(W,H), g=c.getContext('2d');
    const img=g.createImageData(W,H), d=img.data;
    const spokeAng=[], knobAx=[];
    for(let k=0;k<SPOKES;k++){
      const a=(deg + k*(360/SPOKES))*DEG;
      spokeAng.push({ a, ax:Math.sin(a), ay:-Math.cos(a), king:k===0 });
    }
    const put=(i,rgb,al)=>{ d[i]=rgb[0]; d[i+1]=rgb[1]; d[i+2]=rgb[2]; d[i+3]=al==null?255:al; };
    const kingA=spokeAng[0].a*180/Math.PI;
    const angDiff=(a,b)=>{ let x=(a-b)%360; if(x>180)x-=360; if(x<-180)x+=360; return x; };
    const outer=rO+KNOBL+3;
    for(let y=0;y<H;y++){
      for(let x=0;x<W;x++){
        const dx=x+0.5-PVX, dy=y+0.5-PVY;
        const r=Math.hypot(dx,dy);
        if(r>outer) continue;
        const i=(y*W+x)*4;
        let rgb=null;
        // --- hub (top of the stack) ---
        if(r<=HUBR+4){
          const nx=dx/(r||1), ny=dy/(r||1);
          const face=clamp01(0.5+0.5*(nx*LIGHT[0]+ny*LIGHT[1]));
          if(r>HUBR+2)      rgb=pickRGB(GRAPH,0.02);
          else if(r>HUBR-1) rgb=pickRGB(GRAPH,0.22+0.5*face);
          else if(r>HUBR*0.55) rgb=pickRGB(GRAPH,0.34+0.5*face);
          else if(r>HUBR*0.42) rgb=pickRGB(GRAPH,0.06);
          else              rgb=pickRGB(GRAPH,0.40+0.45*face);
          // four screws on the boss, turning with the wheel
          for(let k=0;k<4;k++){
            const a=(deg+45+k*90)*DEG, sx=Math.sin(a)*HUBR*0.74, sy=-Math.cos(a)*HUBR*0.74;
            const q=Math.hypot(dx-sx,dy-sy);
            if(q<3.2) rgb = q<1.6 ? pickRGB(STEEL,0.8) : pickRGB(STEEL,0.25);
          }
          put(i,rgb); continue;
        }
        // --- turned knobs (over the rim) ---
        let done=false;
        for(const s of spokeAng){
          const t0=rO-4, t1=rO+KNOBL;
          const along=dx*s.ax+dy*s.ay;
          if(along<t0-1||along>t1+1) continue;
          const across=dx*(-s.ay)+dy*s.ax;
          const u=clamp01((along-t0)/(t1-t0));
          const prof=0.30+0.70*Math.sin(Math.PI*clamp01((u+0.10)/1.10));    // waist → belly → cap
          const hw=1.9+3.9*prof;
          if(Math.abs(across)>hw) continue;
          const q=across/hw;
          let t=cylShade(q, s.ax, s.ay);
          if(u>0.93) t*=0.55;                                              // end cap turns away
          const collar = u<0.16;
          rgb = collar && s.king ? pickRGB(GOLD, 0.25+0.7*t) : pickRGB(KN, 0.08+0.80*t);
          if(Math.abs(across)>hw-1) rgb=pickRGB(KN,0.04);                  // 1px dark edge
          put(i,rgb); done=true; break;
        }
        if(done) continue;
        // --- rim section ---
        if(r>=rI&&r<=rO){
          const s=(r-rI)/(rO-rI);
          const nx=dx/r, ny=dy/r;
          const face=clamp01(0.5+0.5*(nx*LIGHT[0]+ny*LIGHT[1]));           // which way round the ring
          const sec=1-Math.abs(s-0.40)*1.55;                               // rounded cross-section
          let t=clamp01(0.10+0.62*face+0.30*sec);
          if(s<0.07||s>0.93) t=0.04;                                       // keyline edges
          const aDeg=Math.atan2(dx,-dy)*180/Math.PI;
          rgb = Math.abs(angDiff(aDeg,kingA))<7.5 ? pickRGB(GOLD,0.15+0.85*t) : pickRGB(RIM,t);
          put(i,rgb); continue;
        }
        // --- spokes (under the rim) ---
        for(const s of spokeAng){
          const along=dx*s.ax+dy*s.ay;
          if(along<HUBR-2||along>rI+2) continue;
          const across=dx*(-s.ay)+dy*s.ax;
          const u=clamp01((along-HUBR)/(rI-HUBR));
          const hw=2.9-0.7*u;                                              // tapers outboard
          if(Math.abs(across)>hw) continue;
          const q=across/hw;
          const t=cylShade(q, s.ax, s.ay);
          rgb = Math.abs(across)>hw-0.9 ? pickRGB(SP,0.05) : pickRGB(SP,0.10+0.85*t);
          put(i,rgb); done=true; break;
        }
        if(done) continue;
      }
    }
    g.putImageData(img,0,0);
    if(o.grip){                                                            // hand on the wheel
      g.strokeStyle=TEAL; g.lineWidth=1;
      g.beginPath(); g.arc(PVX,PVY,rO+KNOBL+2.5,0,Math.PI*2); g.stroke();
    }
    return c;
  }

  // small cache so a live spin does not re-solve the same angle twice
  const _cache={}; let _order=[];
  function cell(deg, o){
    o=o||{};
    const key=[Math.round(deg*2)/2, o.rim||DEFAULT_RIM, o.grip?'g':'-'].join('|');
    if(_cache[key]) return _cache[key];
    const c=bake(deg,o); _cache[key]=c; _order.push(key);
    if(_order.length>96){ delete _cache[_order.shift()]; }
    return c;
  }
  function paint(ctx, cx, cy, o){
    o=o||{};
    const deg=o.deg!=null?o.deg:degFromSteer(o.steer,o.turns);
    ctx.imageSmoothingEnabled=false;
    ctx.drawImage(cell(deg,o), Math.round(cx-PVX), Math.round(cy-PVY));
    return ctx;
  }

  // ---- spin model ----
  const maxRudder = 32;
  const lockDeg = (turns)=> (turns==null?1.5:turns)*360;
  const degFromSteer = (steer, turns)=> clamp(steer==null?0:steer,-1,1)*lockDeg(turns);
  const steerFromDeg = (deg, turns)=> clamp((deg||0)/lockDeg(turns), -1, 1);
  const rudderDeg = (steer, max)=> clamp(steer==null?0:steer,-1,1)*(max==null?maxRudder:max);
  const clicks = (deg)=> Math.floor(Math.abs(deg||0)/(360/KNOBS));
  // s={deg,vel} in degrees and deg/s. Returns a NEW state plus what happened this tick.
  function step(s, dt, o){
    o=o||{};
    const turns=o.turns==null?1.5:o.turns, lock=lockDeg(turns);
    const friction=o.friction==null?2.4:o.friction, spring=o.selfCentre||0;
    let deg=s.deg||0, vel=s.vel||0, hit=0;
    vel *= Math.exp(-friction*dt);
    if(spring>0) vel += -deg*spring*dt;
    deg += vel*dt;
    if(deg>lock){ deg=lock; if(vel>0) hit=1; vel*=-0.10; }
    if(deg<-lock){ deg=-lock; if(vel<0) hit=-1; vel*=-0.10; }
    if(Math.abs(vel)<0.7) vel=0;
    if(spring>0 && Math.abs(deg)<0.6 && Math.abs(vel)<2){ deg=0; vel=0; }
    return { deg, vel, hit, steer:steerFromDeg(deg,turns), clicks:clicks(deg) };
  }

  function render(o){
    o=o||{};
    const deg=o.deg!=null?o.deg:degFromSteer(o.steer,o.turns);
    return bake(deg,o);
  }
  // one row of N frames across the lock, for a game that wants a flipbook instead of a live draw
  function sheet(n, o){
    n=n||9; o=o||{};
    const c=cv(W*n,H), g=c.getContext('2d');
    g.imageSmoothingEnabled=false;
    for(let i=0;i<n;i++){
      const steer=-1+2*i/(n-1);
      g.drawImage(bake(degFromSteer(steer,o.turns),o), i*W, 0);
    }
    return c;
  }

  root.WheelRig = { W, H, pivot:{x:PVX,y:PVY}, R, RIMW, KNOBS, SPOKES, KNOBL,
    RIMS, rimIds:Object.keys(RIMS), defaultRim:DEFAULT_RIM,
    maxRudder, lockDeg, degFromSteer, steerFromDeg, rudderDeg, clicks, step,
    paint, render, sheet, GOLD, STEEL, GRAPH, RUBBER, TEAK };
})(typeof globalThis!=='undefined'?globalThis:window);
