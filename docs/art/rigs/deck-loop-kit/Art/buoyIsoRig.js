/* Hidden Harbours — BUOY ISO RIG. The spar buoy rebuilt onto the fleet turntable
   (Art/isoSolid.js): 45deg steps CW, elev 40, 32 px = 1 m, dither, no AA, RINGLESS
   (ADR 0031), gated by the exported KEYLINE_DEFAULT; {keyline:true} — or {outline:true}, the
   spelling the rest of the repo uses — kept as the A/B.

   Supersedes the flat 24x48 buoyRig.js sprite. The eight registered colour schemes are
   carried over verbatim — each fisher's pots are marked in that boat's colours, and no
   two read alike, as buoy law requires. Ramps are derived from each scheme colour by
   lighten/darken only, so nothing new enters the fleet palette.

   Shape: keel toggle -> egg float in two paint bands -> neck -> spindle -> ball top.
   ~1.23 m overall. Cell 36x58, pivot (18,50) = ground under the keel, so a buoy stands
   on a deck slot, a washboard, or a wharf plank on the same contract as a trap.

   Exposes globalThis.BuoyIso = { W,H,pivot,FLEET,SCHEMES,LABELS,render(scheme,dir,opts),
     sheet(scheme,opts) }. */
(function (root) {
  const K = root.IsoSolid;
  const W = 36, H = 58, cx = 18, cy = 50;
  const KEY = '#0f1614';
  // ADR 0031 — the ring is retired; the silhouette is carried by the form's own dark side.
  // BORROWED, NOT COPIED: isoSolid.paint owns the only ring pass this kit has, so a second
  // `false` declared here could drift away from the pass it claims to gate. The guard keeps the
  // turntable optional at load, as it is today (a missing IsoSolid renders wrong, it does not throw).
  const KEYLINE_DEFAULT = K ? K.KEYLINE_DEFAULT : false;
  // The A/B arm. `keyline` is this kit's name for it; `outline` is the name every OTHER gated rig
  // in the repo uses (#463, #477), and a caller reaching for the familiar one used to get a
  // silently ringless render. Both are accepted. Returns undefined when neither is given, so the
  // one default lives at the pass rather than being re-decided here.
  const keylineOpt = (o) => o.keyline!=null ? o.keyline : (o.outline!=null ? o.outline : undefined);

  const FLEET = ['Dory','Punt','CapeIslander','LobsterBoat','SideDragger','SternTrawler','CoastalPacket','Tanker'];
  const SCHEMES = {
    Dory:         { pri:'#74543a', sec:'#d8c8a0', top:'#b5403a' },
    Punt:         { pri:'#2ba39a', sec:'#eef0ea', top:'#e0b13a' },
    CapeIslander: { pri:'#7fa08c', sec:'#2a3b52', top:'#eef0ea' },
    LobsterBoat:  { pri:'#b5403a', sec:'#eef0ea', top:'#1d2226' },
    SideDragger:  { pri:'#9c4a3c', sec:'#e0b13a', top:'#2a3b52' },
    SternTrawler: { pri:'#4a5a66', sec:'#e0862e', top:'#eef0ea' },
    CoastalPacket:{ pri:'#9c2f24', sec:'#4e7d52', top:'#e0d8c4' },
    Tanker:       { pri:'#8a8f94', sec:'#9c2f24', top:'#1d2226' },
  };
  const LABELS = { Dory:'DORY', Punt:'PUNT', CapeIslander:'CAPE ISLANDER', LobsterBoat:'LOBSTER BOAT',
    SideDragger:'SIDE DRAGGER', SternTrawler:'STERN TRAWLER', CoastalPacket:'COASTAL PACKET', Tanker:'TANKER' };
  const DARK = ['#0d1114','#161b1f','#222a2f','#313b41','#455158'];

  const clamp=(v)=>Math.max(0,Math.min(255,Math.round(v)));
  const hx=(n)=>n.toString(16).padStart(2,'0');
  function rampOf(hex){
    const [r,g,b]=K.hex2rgb(hex);
    const f=(m,lift)=>'#'+hx(clamp(r*m+lift))+hx(clamp(g*m+lift))+hx(clamp(b*m+lift));
    return [f(0.42,-6), f(0.62,-2), hex, f(1.14,10), f(1.30,26)];
  }
  const RC={};
  const ramp=(hex)=> RC[hex] || (RC[hex]=rampOf(hex));

  function facesOf(){
    const F=[], push=(a)=>{ for(const f of a) F.push(f); };
    // keel toggle + short stem
    push(K.tube(0,0,0.00,0.075, 0.070,0.056, 12,'DARK',{caps:'both',b:-0.3}));
    push(K.tube(0,0,0.075,0.150, 0.030,0.048, 10,'DARK',{caps:'none',b:-0.1}));
    // egg float — lower band PRI
    push(K.tube(0,0,0.150,0.260, 0.048,0.130, 14,'PRI',{caps:'none',b:0}));
    push(K.tube(0,0,0.260,0.420, 0.130,0.155, 14,'PRI',{caps:'none',b:0}));
    // dark separator ring
    push(K.tube(0,0,0.420,0.435, 0.156,0.156, 14,'DARK',{caps:'none',b:-0.5}));
    // upper band SEC
    push(K.tube(0,0,0.435,0.560, 0.155,0.132, 14,'SEC',{caps:'none',b:0.1}));
    push(K.tube(0,0,0.560,0.660, 0.132,0.062, 14,'SEC',{caps:'top',b:0.25,capB:0.6}));
    // spindle + ball top
    push(K.tube(0,0,0.660,1.100, 0.024,0.022, 8,'TOP',{caps:'none',b:0.35}));
    push(K.tube(0,0,1.100,1.165, 0.022,0.056, 10,'TOP',{caps:'none',b:0.4}));
    push(K.tube(0,0,1.165,1.225, 0.056,0.026, 10,'TOP',{caps:'top',b:0.5,capB:0.7}));
    // rope eye at the keel
    push(K.bar([0.05,0,0.030],[0.115,0.02,0.050],0.014,'ROPE',0.4,0.01));
    push(K.bar([0.115,0.02,0.050],[0.10,-0.03,0.095],0.013,'ROPE',0.2,0.01));
    return F;
  }
  let _F=null;
  function render(scheme, dir, opts){
    opts=opts||{};
    const s=SCHEMES[scheme]||SCHEMES.LobsterBoat;
    if(!_F) _F=facesOf();
    return K.paint(_F, {
      W,H,cx,cy, dir, elev:opts.elev, key:KEY,
      keyline: keylineOpt(opts),
      MATS:{ PRI:{ramp:ramp(s.pri)}, SEC:{ramp:ramp(s.sec)}, TOP:{ramp:ramp(s.top)},
             DARK:{ramp:DARK}, ROPE:{ramp:['#5a4326','#7a6242','#a98f66','#cdbe97','#e3d8b8']} },
    });
  }
  function sheet(scheme, opts){ const o=[]; for(let d=0;d<8;d++) o.push(render(scheme,d,opts)); return o; }

  root.BuoyIso = { W,H, pivot:{x:cx,y:cy}, KEY, KEYLINE_DEFAULT, FLEET, SCHEMES, LABELS,
    defaultElev:K.DEFAULT_ELEV, render, sheet };
})(typeof globalThis!=='undefined'?globalThis:window);
