/* Hidden Harbours — DECK GEAR: the working furniture the catch passes through, on the
   fleet turntable (Art/isoSolid.js — 45deg CW, elev 40, 32 px = 1 m, dither, no AA).
   RINGLESS (ADR 0031); {keyline:true} kept as the A/B.

     baitbin        salt-bait barrel, lid on/off, herring showing
     baitbox        square bait bin — the one that lives under the washboard
     chopboard      bait-chopping board on its frame, knife, cut herring
     bandingtable   banding bench: pliers on a lanyard, a rail of bands, the gauge
     haulerstation  a section of starboard washboard with the hydraulic hauler, davit
                    and snatch block — where the warp comes aboard and the trap lands

   All in ONE cell, 80x76, pivot (40,60) = ground under the centre, so every piece of
   deck furniture drops on a deck slot on the same contract as a trap or a tote.

   Exposes globalThis.DeckGear = { W,H,pivot,KINDS,LABELS,FOOTPRINT,render(kind,dir,opts),
     slots(kind,dir), opening(kind,dir) }. */
(function (root) {
  const K = root.IsoSolid;
  const W = 80, H = 76, cx = 40, cy = 60;
  const KEY = '#0f1614';

  const R = {
    PLAST:['#152234','#1b2b42','#22384f','#2d4c71','#3a5f89','#4a76a3','#5b8cbd'],
    GREY :['#22282b','#2b3236','#333c40','#3f4a4e','#4a565b','#5a686d','#6b7a80','#8b9ba1'],
    STEEL:['#2c3539','#3a464b','#49555a','#5d6b6f','#71807f','#8b9a9b','#a6b6b8','#c6d2d3'],
    OAK  :['#241a11','#2f2216','#3a2a1c','#4d3925','#63482f','#78593a','#8c6a45','#a38052','#b9975f'],
    WHITE:['#4e5a57','#5d6a66','#6d7b76','#7e8c87','#8e9d97','#a1afa9','#b3c1ba','#c7d3cb','#dbe4dc'],
    HYD  :['#3a1a10','#4b2214','#5c2a17','#73351c','#8a4021','#9f4d27','#b45a2c','#c46b37','#d47c42'],
    RUBB :['#0b0e10','#0f1417','#141a1d','#1b2226','#212a2e','#293338','#313d42','#3b4a4d','#465458'],
    HERR :['#2b3439','#3a444a','#49555a','#5d6a6d','#71807f','#8b9b9b','#a6b6b8','#bac6c7','#cfdadb'],
    BAND :['#4a100e','#631511','#7c1a15','#921f18','#a8241b','#bc2d20','#cf3626','#d94631','#e2573c'],
    VOID :['#070b0c','#0a0e10','#0c1113','#0f1517','#121a1c','#161f22','#1a2427','#1f2a2d','#243033'],
    ROPE :['#5a4326','#6a5334','#7a6242','#927954','#a98f66','#bba77f','#cdbe97','#d8cba8','#e3d8b8'],
  };
  const KINDS = ['baitbin','baitbox','chopboard','bandingtable','haulerstation'];
  const LABELS = { baitbin:'SALT-BAIT BARREL', baitbox:'BAIT BIN', chopboard:'CHOPPING BOARD',
    bandingtable:'BANDING BENCH', haulerstation:'HAULER + WASHBOARD' };
  const FOOTPRINT = {                     // metres on the deck, and how tall it stands
    baitbin:{ l:0.64, w:0.64, h:0.88 }, baitbox:{ l:0.78, w:0.56, h:0.48 },
    chopboard:{ l:0.72, w:0.50, h:0.82 }, bandingtable:{ l:1.00, w:0.58, h:0.90 },
    haulerstation:{ l:1.50, w:0.62, h:1.36 },
  };
  const push=(A,B)=>{ for(const f of B) A.push(f); return A; };

  function baitbin(o){
    const F=[], lid=o.lid!=='off';
    push(F,K.tube(0,0,0.02,0.06,0.29,0.315,18,'PLAST',{caps:'bottom',b:-0.6}));
    for (let i=0;i<4;i++){                                    // ribbed drum — constant
      const z=0.06+i*0.195;                                   // radius, the ribs are light bands
      push(F,K.tube(0,0,z,z+0.165,0.315,0.315,18,'PLAST',{b:0}));
      push(F,K.tube(0,0,z+0.165,z+0.195,0.317,0.317,18,'PLAST',{b:0.8}));
    }
    if (lid){
      push(F,K.tube(0,0,0.84,0.875,0.335,0.30,18,'PLAST',{caps:'top',b:0.4,capB:0.7}));
      push(F,K.bar([-0.16,0,0.895],[0.16,0,0.895],0.022,'PLAST',0.9,0.01));
    } else {
      push(F,K.tube(0,0,0.84,0.865,0.335,0.335,18,'PLAST',{b:0.5}));
      push(F,K.tube(0,0,0.10,0.80,0.285,0.285,18,'VOID',{caps:'top',b:-1.4,capB:-1.5,db:0.02}));
      const rng=K.mulberry(17);                                // salted herring at the brim
      for (let i=0;i<14;i++){
        const a=rng()*6.28, r=rng()*0.24;
        push(F,K.bar([Math.cos(a)*r,Math.sin(a)*r,0.78+rng()*0.03],
          [Math.cos(a)*r+Math.cos(a+1.2)*0.13, Math.sin(a)*r+Math.sin(a+1.2)*0.13, 0.79+rng()*0.03],
          [0.036,0.018],'HERR',(i%3)-0.4,0.03));
      }
    }
    return F;
  }
  function baitbox(o){
    const F=[];
    push(F,K.box([0,0,0.045],[0.38,0.27,0.03],'GREY',-0.5,0,null,'base'));
    for (const sy of [-1,1]) push(F,K.box([0,sy*0.275,0.26],[0.39,0.02,0.19],'GREY',sy<0?-0.1:0.1,0,null,'shell'));
    for (const sx of [-1,1]) push(F,K.box([sx*0.385,0,0.26],[0.02,0.28,0.19],'GREY',sx<0?0.35:-0.2,0,null,'shell'));
    push(F,K.box([0,0,0.085],[0.36,0.25,0.008],'VOID',-1.2,0.015,null,'base'));
    push(F,K.box([0,0,0.455],[0.40,0.29,0.014],'GREY',0.4,0,null,'shell'));
    for (const y of [-0.16,0.16]) push(F,K.box([0,y,0.472],[0.36,0.022,0.006],'GREY',0.9,0.01,null,'shell'));
    push(F,K.box([0,0,0.472],[0.02,0.26,0.006],'GREY',0.9,0.01,null,'shell'));
    for (const sx of [-1,1]) push(F,K.box([sx*0.30,-0.278,0.38],[0.055,0.02,0.03],'VOID',-1.3,0.02,null,'shell'));
    return F;
  }
  function chopboard(o){
    const F=[];
    for (const sx of [-1,1]) for (const sy of [-1,1])
      push(F,K.box([sx*0.30,sy*0.20,0.36],[0.022,0.022,0.36],'STEEL',0,0,null,'base'));
    push(F,K.box([0,0,0.10],[0.30,0.21,0.014],'STEEL',-0.6,0,null,'base'));
    push(F,K.box([0,0,0.745],[0.35,0.245,0.028],'OAK',0.3,0,null,'shell'));   // the board
    for (const x of [-0.22,-0.07,0.08,0.23]) push(F,K.bar([x,-0.20,0.775],[x,0.20,0.775],0.006,'OAK',-0.5,0.012,'shell'));
    push(F,K.bar([-0.05,-0.05,0.79],[0.20,0.10,0.79],[0.028,0.006],'STEEL',0.6,0.02,'shell'));  // knife blade
    push(F,K.bar([0.20,0.10,0.79],[0.31,0.16,0.79],[0.022,0.018],'OAK',0.2,0.02,'shell'));      // handle
    const rng=K.mulberry(23);
    for (let i=0;i<7;i++){ const x=-0.26+rng()*0.28, y=-0.15+rng()*0.24;
      push(F,K.box([x,y,0.782],[0.035,0.022,0.012],'HERR',(i%3)-0.5,0.03,null,'shell')); }
    return F;
  }
  function bandingtable(o){
    const F=[];
    for (const sx of [-1,1]) for (const sy of [-1,1])
      push(F,K.box([sx*0.44,sy*0.24,0.42],[0.024,0.024,0.42],'STEEL',0,0,null,'base'));
    for (const sy of [-1,1]) push(F,K.bar([-0.44,sy*0.24,0.16],[0.44,sy*0.24,0.16],0.016,'STEEL',-0.4,0,'base'));
    push(F,K.box([0,0,0.86],[0.50,0.29,0.022],'WHITE',0.35,0,null,'shell'));       // scrubbed top
    push(F,K.box([0,0.29,0.895],[0.50,0.016,0.045],'WHITE',0.15,0,null,'shell'));  // fiddle rail
    // rail of bands, the pliers on their lanyard, the gauge
    push(F,K.bar([-0.40,0.20,0.905],[-0.06,0.20,0.905],0.020,'BAND',0.5,0.02,'shell'));
    for (let i=0;i<7;i++) push(F,K.tube(-0.38+i*0.05,0.20,0.888,0.925,0.028,0.028,8,'BAND',{b:0.3,db:0.03,tag:'shell'}));
    push(F,K.bar([0.06,-0.02,0.888],[0.30,0.10,0.888],[0.030,0.014],'STEEL',0.55,0.02,'shell'));
    push(F,K.bar([0.30,0.10,0.888],[0.40,0.19,0.888],[0.022,0.016],'BAND',0.4,0.025,'shell'));
    push(F,K.box([-0.12,-0.16,0.886],[0.10,0.03,0.008],'STEEL',0.5,0.02,null,'shell'));  // carapace gauge
    push(F,K.box([0,0,0.30],[0.46,0.26,0.012],'STEEL',-0.7,0,null,'base'));               // under shelf
    return F;
  }
  function haulerstation(o){
    const F=[];
    // washboard section — capped side deck, oak cap on a painted bulwark
    push(F,K.box([0,0.10,0.44],[0.74,0.28,0.44],'WHITE',-0.15,0,null,'base'));
    push(F,K.box([0,0.10,0.905],[0.76,0.30,0.026],'OAK',0.35,0,null,'shell'));
    for (let i=0;i<7;i++) push(F,K.bar([-0.70+i*0.235,-0.19,0.933],[-0.70+i*0.235,0.39,0.933],0.008,'OAK',-0.4,0.012,'shell'));
    // stanchion + hydraulic hauler drum
    push(F,K.box([-0.40,0.06,1.06],[0.045,0.045,0.16],'STEEL',0.1,0,null,'shell'));
    push(F,K.tube(-0.40,0.06,1.20,1.235,0.13,0.155,14,'HYD',{caps:'bottom',b:0.1,tag:'shell'}));
    push(F,K.tube(-0.40,0.06,1.235,1.31,0.115,0.115,14,'RUBB',{b:-0.3,tag:'shell'}));   // the sheave
    push(F,K.tube(-0.40,0.06,1.31,1.345,0.155,0.13,14,'HYD',{caps:'top',b:0.35,capB:0.45,tag:'shell'}));
    push(F,K.tube(-0.40,0.06,1.06,1.20,0.075,0.075,10,'HYD',{b:-0.1,tag:'shell'}));
    // davit arm outboard with a snatch block
    push(F,K.bar([-0.40,0.06,1.30],[-0.16,-0.30,1.34],0.034,'STEEL',0.3,0.01,'shell'));
    push(F,K.tube(-0.16,-0.30,1.24,1.33,0.062,0.062,10,'STEEL',{b:0.15,tag:'shell'}));
    push(F,K.bar([-0.16,-0.30,1.33],[-0.16,-0.30,1.36],0.02,'STEEL',0.4,0.01,'shell'));
    // the warp coming aboard, over the sheave and down to the deck
    push(F,K.bar([-0.16,-0.32,1.27],[-0.40,0.02,1.31],0.016,'ROPE',0.5,0.02,'shell'));
    push(F,K.bar([-0.40,0.02,1.31],[-0.34,0.14,0.95],0.016,'ROPE',0.35,0.02,'shell'));
    push(F,K.tube(0.34,0.10,0.94,0.985,0.16,0.16,12,'ROPE',{b:0.2,biasOf:(i)=>((i%2)?0.4:-0.2),tag:'shell'}));
    push(F,K.tube(0.34,0.10,0.985,1.03,0.145,0.145,12,'ROPE',{b:0.3,biasOf:(i)=>((i%2)?-0.2:0.4),tag:'shell'}));
    return F;
  }
  const BUILD={ baitbin, baitbox, chopboard, bandingtable, haulerstation };
  const CACHE={};
  function facesOf(kind, o){
    const k=kind+'|'+(o.lid||'');
    if (!CACHE[k]) CACHE[k]=(BUILD[kind]||baitbox)(o);
    return CACHE[k];
  }
  function render(kind, dir, opts){
    opts=opts||{};
    const MATS={}; for (const m in R) MATS[m]={ramp:R[m],off:0};
    if (opts.colour && R[opts.colour.toUpperCase()]) MATS.PLAST={ramp:R[opts.colour.toUpperCase()],off:0};
    return K.paint(facesOf(KINDS.indexOf(kind)>=0?kind:'baitbox', opts), {
      W,H,cx,cy, dir, elev:opts.elev, MATS, key:KEY,
      keyline: opts.keyline!=null?opts.keyline:false });
  }
  // where things land ON a piece of gear (bench top, board, washboard cap)
  const TOPS = { bandingtable:{z:0.885,hx:0.40,hy:0.22}, chopboard:{z:0.775,hx:0.28,hy:0.18},
                 haulerstation:{z:0.93,hx:0.58,hy:0.20}, baitbox:{z:0.47,hx:0.30,hy:0.20},
                 baitbin:{z:0.88,hx:0.20,hy:0.20} };

  /* ---- WHERE A PERSON STANDS ------------------------------------------------
     TOPS says where a THING lands on a piece of gear. STATIONS says where a PERSON stands to work
     it, which way they face, and which CharacterIso6 clip they play. Written here because it is a
     property of the furniture, not of the page that draws it — without this every consumer
     eyeballs a figure against a bench and every consumer gets a different answer.

       stand   the operator's ground contact in the gear's own local metres
       turn    EIGHTHS to ADD to the dir the gear was rendered at, to get the character's dir. Both
               rigs share the turntable (th = -dir*PI/4), so a figure rendered at the gear's own dir
               faces the same way the gear does; +4 turns it around to face the gear.
       workZ   the world metre the hands work at — pass it straight through as the clip's opts.workZ
       clear   footprint a deck planner must reserve for the operator, metres, BEFORE it packs pots

     The hauler is the one station whose operator faces OUTBOARD (turn 0 — same way as the gear):
     the warp comes in over the rail and the drum is worked from inboard of it. */
  const STATIONS = {
    bandingtable: { anim:'bench',  stand:[0, -0.52, 0], turn:0, workZ:0.885, clear:[0.62,0.58] },
    chopboard:    { anim:'chop',   stand:[0, -0.46, 0], turn:0, workZ:0.775, clear:[0.58,0.56] },
    baitbin:      { anim:'bench',  stand:[0, -0.54, 0], turn:0, workZ:0.865, clear:[0.58,0.56] },
    baitbox:      { anim:'bench',  stand:[0, -0.48, 0], turn:0, workZ:0.470, clear:[0.58,0.56] },
    haulerstation:{ anim:'hauler', stand:[-0.30, 0.44, 0], turn:4, workZ:1.05, clear:[0.66,0.60],
                    sheave:[-0.40, 0.06, 1.31] },
  };
  function station(kind, dir, opts){
    opts = opts||{};
    const T = STATIONS[kind]; if(!T) return null;
    const B = K.camBasis({dir, elev:opts.elev});
    const p = (v)=>{ const q=K.proj(v[0],v[1],v[2],B,cx,cy);
      return { dx:Math.round(q.sx-cx), dy:Math.round(q.sy-cy), d:q.d }; };
    const st = p(T.stand);
    const out = { kind, anim:T.anim, workZ:T.workZ, stand:T.stand, clear:T.clear, turn:T.turn,
      /* render the character at THIS dir and its pivot at (gear pivot + dx, + dy) */
      dir:((((dir|0)+T.turn)%8)+8)%8, dx:st.dx, dy:st.dy, depth:st.d };
    if(T.sheave){ const s=p(T.sheave); out.sheave={dx:s.dx, dy:s.dy}; out.sheaveLocal=T.sheave; }
    return out;
  }
  function surface(kind, dir, opts){
    opts=opts||{};
    const T=TOPS[kind]||TOPS.baitbox, B=K.camBasis({dir, elev:opts.elev});
    const v=K.proj(opts.ox||0, opts.oy||0, T.z, B, cx, cy);
    return { dx:Math.round(v.sx-cx), dy:Math.round(v.sy-cy), z:T.z, hx:T.hx, hy:T.hy };
  }
  root.DeckGear = { W,H, pivot:{x:cx,y:cy}, KEY, KINDS, LABELS, FOOTPRINT, RAMPS:R, TOPS, STATIONS,
    defaultElev:K.DEFAULT_ELEV, render, surface, station };
})(typeof globalThis!=='undefined'?globalThis:window);
