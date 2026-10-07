/* Hidden Harbours — ISO CHARACTER rig, PASS 10 (characterIsoRig10.js). Pass 9.2's rig, re-proportioned and stood on the pass-9 ground.
   PASS 10.0 — THE START (2026-09-30)
     PROPORTIONS (P10)  about seven heads: the Fisher stands 1.79 m to the crown (9.2: 1.54 m, four heads). Hip joint 0.905 m, knee
                0.485, shoulder 1.43; upper arm 0.30, forearm 0.27, so the fingertips hang at mid-thigh. Shoulders 0.93 of 9.2's, the
                torso 1.08 deeper (the profile reads), legs a little apart. The pose library is read in rig 9's Fisher frame (canonD),
                so reach, stride and foot spots scale with the longer limbs.
     THE HEAD   built at pass 9's size, then scaled about the head bone by kH (0.65; children 0.97 of that). The face is laid at 1/kH
                so it lands on single pixels: a 1 px eye each side of a 1 px nose column, one row for the eye, the moustache on
                the row below, the mouth on the next (fz: the face rows are pixel rows, RS metres apart). headMid is the front of the
                face at the eye line, so the head snap puts the eye row on a pixel centre at every facing.
     THE FEET   boots 0.71 of 9.2's width and lower, with a thinner sole: two feet with a gap at S instead of one dark slab.
     THE GROUND no keyline by default (ADR 0031; keyline:true is the A/B). render({ sky }) takes a WeatherSky sky and lights the figure
                as TerrainLight5 lights open ground in that weather (skyGrade). shadow({ sky }) is the cast shadow on the ground and the
                contact patch under the feet.
   PASS 10.1 — ONE BODY (2026-10-01)
     THE FACE   every face mark is a point mark (pt): it draws the one pixel under its centre, over the head's own skin, while the
                camera's azimuth is inside its role's range (az). A mark is built for one design camera, on the skull point that camera
                sees dx, dy pixels from headMid, so at the eight facings it lands on a pixel centre. Four roles (ROLES): front (both eyes,
                within 24 deg of ahead), diag (the near eye, 24-66), far (the far eye, column 1 only), side (profile, 66-114). At SE and
                SW the eyes are two single pixels either side of the bridge, on the eye row: 10.0 drew the near eye as a 2 px diagonal
                and lost the far eye between two rows. Brows and mouth take the same roles, so the mouth shows at the diagonals.
     ONE BODY   cloth and limbs shade on smooth normals (sn): a body face within 35 deg of vertical lights as if it were vertical, so
                torso, sleeves and legs of one material take one tone per facing instead of a band at every ring. The arm tapers from
                a deltoid that rounds the shoulder into the sleeve; the thigh tops fill the hip, so the legs carry the hip line down;
                the crotch ring is taken in; the vest covers the shoulders.
     SEX, FRAME a build has a sex (m, f) and a frame, the body type within it: m lean, average, broad, stocky, heavy; f slender,
                average, hourglass, pear, full. SEX is the base each sex starts from (the Fisher is the male base); FRAMES are the
                type's factors on it, and bust, belly and seat are metres on the torso's front and back. shape stays, read against the
                sex's own norm (0.25 m, 0.75 f). Age blends the sex base toward the Fisher's (a child is mostly unsexed).
     REACH      the deck work's world grips (hauler, bench, chop, lift, place, toss) are held in the arm's reach on any build; boarding
                bends to a low rail (poses: reachDown); the reach clip's bracing hand is blended in reach.
   PASS 10.2 — BEARDS, HAIR, WARDROBE, CAST (2026-10-01)
     BEARDS     ten styles (BEARDS): none, stubble, moustache, chinstrap, goatee, Van Dyke, sideburns, mutton chops, full, long. Each is
                the skull's lower rings painted, point marks laid for the eight facings like the face's (the moustache, the sideburn
                edge, the cheek fill), and for the chin beards a mass that stands in front of the collar, so the beard has a chin at 40 deg
                (10.1's collar hid the chin row; its goatee read as an open mouth and its mutton chops as a moustache). Stubble is the skin
                taken toward the hair, so it reads on any skin.
     HAIR       the crown's upper front a tone up (sheen); crop a higher hairline and short back and sides a tone down; mop three tufts on
                the crown; bob and long a curtain of hair beside each cheek (long's carries on to the shoulder); bun high on the crown;
                the ponytail a tied tail that holds its pixels in profile (10.1's dropped to one floating pixel); buzz the hair let toward
                the skin; bald a shine.
     WARDROBE   tops: Breton (one-row stripes on torso and sleeves), hoodie, cardigan, pea coat, life vest, singlet; outfits: chest
                waders, coveralls, greatcoat (with a scarf), wetsuit, sundress, pinafore; swimwear: trunks, briefs, swimsuit, bikini (bare
                feet; trunks and briefs give a woman a top); bottoms: cropped, long skirt. GARMENTS and BOTTOMS mark fem what randomBuild
                never puts on a man.
     CAST       twenty more NPCs (NPCS), so CAST is thirty; CAST10 is the first ten. randomBuild(rnd, { sex, age }) draws a build.
     THE KNEE   the shin's top ring is taken inside the thigh (0.052 of 0.057), so the knee has one surface in front: two bodies in the
                build space drew a knee pixel differently left and right (light check, S mirror).
   -------- pass 9's header --------
   Pass 9 (characterIsoRig9.js): pass 8's mesh, skeleton and clips, made parametric.
   -----------------------------------------------------------------------------------------------------------
   Pass 8 built one figure. Pass 9 builds any figure from a BUILD: body shape (a 0..1 slider from broad-shouldered
   to wide-hipped), age, height and weight steps, head shape, skin, hair colour and style, beard, eye colour and
   shape, garment, outfit and shirt colours, hat. The ten presets in CAST are builds; the character creator writes
   builds too. Every build is the same 28 bones and the same 53 clips.

   HOW A BUILD CHANGES THE FIGURE
     SKELETON   lengths come from age, shape and the height step (1 step = 1 px of crown at 32 px/m and 40 deg),
                built from the ground up: ankle, shin, thigh, torso landmarks, neck, head. Bind frames stay identity.
     MESH       the pass-8 mesh is re-authored as functions of the build: torso rings take hip / waist / chest /
                shoulder factors (shape) and girth (weight); limbs follow their bones; the head rings take a head
                shape. Face marks stay on the pass-8 pixel rows relative to the head bone at every head size.
     CLIPS      the pose library writes body-relative targets through K.bp (hands) and K.ft (feet), so a grip at
                the fisher's belt is at this build's belt. World contracts (railZ, rung, workZ, saddle, wheel) stay
                world metres and clamp to this build's reach, as before. Stride scales with leg length, so walk and
                run report their own ground speed per build; an engine plays them at moveSpeed / speed_mps.
   SECOND PASS ON THE LOOK
     hair styles are shells with a silhouette (fringe tufts, crown tuft, bun, tail) and stay off the front of the
     side plane, so E / W show a cheek, an eye and an ear; the far eye at the diagonals is drawn on the near eye's
     row (a far-role mark that only draws at that diagonal) instead of two rows up at the hair line; eyes carry
     their colour (pupil, iris, white); the shoulder yoke is a slope into the collar, not a shelf.
   PASS 9.1 — THE WARDROBE
     garments split into TOPS (tee, long sleeve, work shirt, jumper, vest, apron), which take a BOTTOM (trousers,
     shorts, skirt; the new `bottom` field), and OUTFITS that bring their own (overalls, oilskins, skirt and shawl,
     suit, tuxedo, dress, gown). Jackets flare to the hip over narrower trousers; the V, lapels, tie and bow tie are
     decals; apronCol is the trim colour (apron, tie, bow tie, sash). Six hats: top hat, bowler, captain's cap, sun
     hat, bucket hat, beret. The flat cap's crown now clears the skull. Every 9.0 build renders as before.
   PASS 9.2 — THE IMPORT
     the hair region: the skull under each style's hair takes the hair's material and every shell band follows the skull's rings, so no
     facing, pose or head turn shows scalp where the hair is (THE HAIR REGION); over and behind the ears is hair on every style.
     blink (BLINK, faceClips.blink) and look-at (LOOK: a neck + head turn and the gaze states eyes.left / eyes.right), both driven by
     the engine on top of any clip. The mounts keep the standing foot on the ground until the seat carries the body (CONTRACT.saddleFit
     is the saddle they are gated at). The load, lift and haul hands stay in reach in transit. The open mouth is three pixels wide. The
     hood's neck rides the neck bone. The collider is in the sidecar.
   PASS 10.3 — THE SEND-BACK (2026-10-03)
     DITHER, TOL, INK and AIM are exported beside SHADING, BLINK and LOOK, and paint() reads its tolerances from TOL. The rod's bend chords
     keep the rod's own roll (10.2 turned them 180 deg about the rod each time it passed vertical). A profile eye the hair covers moves a
     column forward (the square and wide heads); a knit brow wins over a lidded eye's lid; the mouth's centre goes a row up where the chin
     is too short for it. sizes() counts the bind mesh at 7 decimals, as the builds print it.
   The contract every pass keeps: axes, 32 px/m, 40 deg, 8 facings, the ANIMS table, world opts (saddleFit since 9.2); the cell is
   80 x 104, pivot (40, 90), since 10.0 (pass 9: 64 x 92, pivot (32, 82)).
   Load order: characterIsoRig10.js, characterIsoRig10.poses.js, characterIsoRig10.checks.js (optional).
   Exposes globalThis.CharacterIso10. */
(function (root) {
  'use strict';
  const DEG = Math.PI/180, TAU = Math.PI*2;

  /* ============================ cell · camera · light ============================ */
  const PX = 32, W = 80, H = 104, PIVOT = { x:40, y:90 }, ELEV = 40;   /* 10.0: a taller figure with a longer stride and a longer body lying down: 80 x 104 */
  const ORDER = ['N','NE','E','SE','S','SW','W','NW'];
  const vnorm = (a)=>{ const m=Math.hypot(a[0],a[1],a[2])||1; return [a[0]/m,a[1]/m,a[2]/m]; };
  const FLEET_KEY = [-0.42, 0.72, 0.52];
  /* screen basis (right, up, toward camera). The fleet vector without its left component: same height, same
     frontal share, no side. Vertical walls then shade alike whichever way they face, so FORM adds a view term —
     planes turned to the camera read one step above planes turned away. It is not a second light: it has no
     direction in the world, only toward the eye. */
  const SHADING = { key: vnorm([0, FLEET_KEY[1], FLEET_KEY[2]]), fleetKey: vnorm(FLEET_KEY), form: 0.5, formMid: 0.45,
    edge: 0.12, keyline: '#101a19', keylineMix: 0.22, keylineDefault: false, cull: 'back faces (toward-camera component of the flat normal <= 0)',
    step: 'clamp(round(s*gain + bias + b), lo, hi) + off, clamped to the ramp; s = n.key + form*(n.toward - formMid). lo..hi is four tones: shadow, base, light, highlight; the contour pass may take one below',
    headSnap: 'the head bone is moved in screen space so its centre sits on a pixel centre (<= 0.5 px)' };
  /* 10.3: what a port of the reference renderer needs besides SHADING, exported so the game reads it instead of copying it.
     DITHER: rig 10 never dithers (every tone is rounded: SHADING.step); the fleet rigs' 4x4 Bayer is given for a baker that shares one
     rasteriser. TOL: every tolerance paint() and the gates use; paint() reads them from here. */
  const DITHER = { used:false, bayer4:[[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]], index:'bayer4[x & 3][y & 3], x the cell column, y the cell row',
    threshold:'(m + 0.5) / 16', note:'no pixel of rig 10 is dithered: the step is clamp(round(...)), never a threshold against this matrix' };
  const TOL = { gate_m:1e-6, inside:1e-6, area:1e-9, cull:1e-4, markEdge:1e-4, depthScale:1e7, shadeScale:1e9, tieDepth:1e-9,
    use:{ gate_m:'the round trip, the loops, the hand-offs and the rocked skin are held equal to this, in metres',
      inside:'a pixel centre (x + 0.5, y + 0.5) is inside a triangle when each barycentric weight is >= -inside',
      area:'a triangle whose screen area is below this draws nothing', cull:'a face draws when its toward-camera component is > max(cull, minT); a point mark when it is > cull',
      markEdge:'a point mark whose projected centre is within markEdge of a pixel edge draws nothing',
      depthScale:'the z-test compares round((d - db) * depthScale) / depthScale', shadeScale:'the shade value s is round(s * shadeScale) / shadeScale before the step',
      tieDepth:'the A/B keyline takes its nearest neighbour by depth, ties within tieDepth going to the darker' } };
  /* 10.1: the parts that shade on smooth normals, and how far from vertical a face may lean and still count as vertical (35 deg: the waist's taper and the bust stay one tone with the chest; the yoke, steeper, keeps its own) */
  const SMOOTH=/^(torso|pelvis|skirt|shawl|upper_|fore_|thigh_|shin_|boot_|bib|strap|belt|vest|jacket|lapel|tie|apron)/, SMOOTH_Z=Math.sin(35*DEG);

  /* ============================ maths ============================ */
  const I3 = [1,0,0, 0,1,0, 0,0,1];
  const vadd=(a,b)=>[a[0]+b[0],a[1]+b[1],a[2]+b[2]], vsub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]];
  const vmul=(a,s)=>[a[0]*s,a[1]*s,a[2]*s], vdot=(a,b)=>a[0]*b[0]+a[1]*b[1]+a[2]*b[2];
  const vcross=(a,b)=>[a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]];
  const vlen=(a)=>Math.hypot(a[0],a[1],a[2]), vdist=(a,b)=>Math.hypot(a[0]-b[0],a[1]-b[1],a[2]-b[2]);
  const vlerp=(a,b,t)=>[a[0]+(b[0]-a[0])*t, a[1]+(b[1]-a[1])*t, a[2]+(b[2]-a[2])*t];
  const mV =(R,v)=>[R[0]*v[0]+R[3]*v[1]+R[6]*v[2], R[1]*v[0]+R[4]*v[1]+R[7]*v[2], R[2]*v[0]+R[5]*v[1]+R[8]*v[2]];
  const mTV=(R,v)=>[R[0]*v[0]+R[1]*v[1]+R[2]*v[2], R[3]*v[0]+R[4]*v[1]+R[5]*v[2], R[6]*v[0]+R[7]*v[1]+R[8]*v[2]];
  function mM(A,B){ const o=new Array(9); for(let c=0;c<3;c++){ const v=mV(A,[B[c*3],B[c*3+1],B[c*3+2]]); o[c*3]=v[0]; o[c*3+1]=v[1]; o[c*3+2]=v[2]; } return o; }
  function mTM(A,B){ const o=new Array(9); for(let c=0;c<3;c++){ const v=mTV(A,[B[c*3],B[c*3+1],B[c*3+2]]); o[c*3]=v[0]; o[c*3+1]=v[1]; o[c*3+2]=v[2]; } return o; }
  const cols=(x,y,z)=>[x[0],x[1],x[2], y[0],y[1],y[2], z[0],z[1],z[2]];
  const Rx=(a)=>{ const c=Math.cos(a), s=Math.sin(a); return [1,0,0, 0,c,s, 0,-s,c]; };
  const Ry=(a)=>{ const c=Math.cos(a), s=Math.sin(a); return [c,0,-s, 0,1,0, s,0,c]; };
  const Rz=(a)=>{ const c=Math.cos(a), s=Math.sin(a); return [c,s,0, -s,c,0, 0,0,1]; };
  /* pose angles in degrees: pitch > 0 leans the top toward +y, roll > 0 tips it toward +x, yaw > 0 turns the
     front toward +x (clockwise from above).  R = Rz(-yaw) · Ry(roll) · Rx(-pitch) */
  function eul(e){ if(!e) return I3.slice(); let R=Rx(-(e.pitch||0)*DEG); if(e.roll) R=mM(Ry(e.roll*DEG),R); if(e.yaw) R=mM(Rz(-e.yaw*DEG),R); return R; }
  /* a tool's attitude (rig 7 / RodIso): local +y along the tool, pitched up by p, then turned by y */
  const aimM=(p,y)=>mM(Rz(-y),Rx(p));
  const aimTo=(A,B)=>{ const d=vnorm(vsub(B,A)); return aimM(Math.asin(Math.max(-1,Math.min(1,d[2]))), Math.atan2(d[0],d[1])); };
  /* 10.3: the shortest turn taking unit u onto unit v (Rodrigues), and a frame R turned that way so its +y runs from A to B */
  function rotOnto(u,v){ const c=vdot(u,v), a=vcross(u,v), s=vlen(a); if(s<1e-12) return I3.slice(); const k=vmul(a,1/s), t=1-c;
    const col=(e)=>vadd(vadd(vmul(e,c), vmul(vcross(k,e),s)), vmul(k, t*vdot(k,e))); return cols(col([1,0,0]), col([0,1,0]), col([0,0,1])); }
  const onto=(R,A,B)=>mM(rotOnto(mV(R,[0,1,0]), vnorm(vsub(B,A))), R);
  function quatOf(R){
    const m00=R[0],m10=R[1],m20=R[2], m01=R[3],m11=R[4],m21=R[5], m02=R[6],m12=R[7],m22=R[8];
    const tr=m00+m11+m22; let x,y,z,w;
    if(tr>0){ const s=Math.sqrt(tr+1)*2; w=0.25*s; x=(m21-m12)/s; y=(m02-m20)/s; z=(m10-m01)/s; }
    else if(m00>m11 && m00>m22){ const s=Math.sqrt(1+m00-m11-m22)*2; w=(m21-m12)/s; x=0.25*s; y=(m01+m10)/s; z=(m02+m20)/s; }
    else if(m11>m22){ const s=Math.sqrt(1+m11-m00-m22)*2; w=(m02-m20)/s; x=(m01+m10)/s; y=0.25*s; z=(m12+m21)/s; }
    else { const s=Math.sqrt(1+m22-m00-m11)*2; w=(m10-m01)/s; x=(m02+m20)/s; y=(m12+m21)/s; z=0.25*s; }
    return [x,y,z,w];
  }
  function matOf(q){ const n=Math.hypot(q[0],q[1],q[2],q[3])||1, x=q[0]/n, y=q[1]/n, z=q[2]/n, w=q[3]/n;
    return [1-2*(y*y+z*z), 2*(x*y+z*w), 2*(x*z-y*w),  2*(x*y-z*w), 1-2*(x*x+z*z), 2*(y*z+x*w),  2*(x*z+y*w), 2*(y*z-x*w), 1-2*(x*x+y*y)]; }
  const qmul=(a,b)=>[ a[3]*b[0]+a[0]*b[3]+a[1]*b[2]-a[2]*b[1], a[3]*b[1]-a[0]*b[2]+a[1]*b[3]+a[2]*b[0],
                      a[3]*b[2]+a[0]*b[1]-a[1]*b[0]+a[2]*b[3], a[3]*b[3]-a[0]*b[0]-a[1]*b[1]-a[2]*b[2] ];
  /* intrinsic Z-Y-X Euler, degrees (rig 7's rotEuler) */
  function eulerZYX(R){ const m00=R[0], m10=R[1], m20=R[2], m01=R[3], m11=R[4], m21=R[5], m22=R[8];
    const sy=-m20, cy=Math.sqrt(Math.max(0,1-sy*sy)); let x,y,z;
    if(cy>1e-9){ y=Math.asin(Math.max(-1,Math.min(1,sy))); x=Math.atan2(m21,m22); z=Math.atan2(m10,m00); }
    else { y=sy>0?Math.PI/2:-Math.PI/2; x=0; z=Math.atan2(-m01,m11); }
    return [x/DEG, y/DEG, z/DEG].map(a=>+a.toFixed(6)); }
  const fk=(Wp,L)=>({ p:vadd(Wp.p, mV(Wp.R, L.p)), R:mM(Wp.R, L.R) });
  const fkp=(Wf,p)=>vadd(Wf.p, mV(Wf.R, p));
  const localOf=(Wp,Wc)=>({ p:mTV(Wp.R, vsub(Wc.p, Wp.p)), R:mTM(Wp.R, Wc.R) });
  /* 10.3: the JSON replacer every export prints with: numbers to 7 decimals */
  const R7=(k,v)=>typeof v==='number' ? +v.toFixed(7) : v;
  const clamp=(v,a,b)=>v<a?a:v>b?b:v, clamp01=(v)=>v<0?0:v>1?1:v, lerp=(a,b,t)=>a+(b-a)*t;
  const sm=(t)=>t<=0?0:t>=1?1:t*t*(3-2*t), seg=(t,a,b)=>sm((t-a)/((b-a)||1e-9)), frac=(t)=>((t%1)+1)%1;
  const sn=(u,ph)=>Math.sin(TAU*(u+(ph||0))), cs=(u,ph)=>Math.cos(TAU*(u+(ph||0)));
  const mixv=(a,b,t)=>Array.isArray(a)?a.map((x,i)=>x+(b[i]-x)*t):a+(b-a)*t;
  const bez=(A,C,B,t)=>{ const k=1-t; return [0,1,2].map(i=>k*k*A[i]+2*k*t*C[i]+t*t*B[i]); };
  const lin=(x,T)=>{ if(x<=T[0][0]) return T[0][1]; for(let i=0;i+1<T.length;i++) if(x<=T[i+1][0]){ const t=(x-T[i][0])/(T[i+1][0]-T[i][0]); return T[i][1]+(T[i+1][1]-T[i][1])*t; } return T[T.length-1][1]; };
  /* keyed track, smoothstep between keys */
  function keys(t,K){ if(t<=K[0][0]) return mixv(K[0][1],K[0][1],0);
    for(let i=0;i+1<K.length;i++){ const a=K[i], b=K[i+1]; if(t<=b[0]) return mixv(a[1],b[1],sm((t-a[0])/((b[0]-a[0])||1e-9))); }
    return mixv(K[K.length-1][1],K[K.length-1][1],0); }
  /* frame table: F[k] is the value AT frame k, linear between frames; mode is the clip's u mapping */
  function ftab(u,F,mode){ const n=F.length; const x = mode==='loop' ? frac(u)*n : mode==='settle' ? clamp01(u)*(n-1) : clamp(u*n,0,n-1);
    const i=Math.min(n-1,Math.floor(x)), f=x-i, j = mode==='loop' ? (i+1)%n : Math.min(n-1,i+1); return mixv(F[i],F[j],f); }
  const r4=(v)=>v.map(x=>+x.toFixed(4));

  /* ============================ the clip table (rig 6, to the frame and the millisecond) ============================ */
  const ANIMS = { idle:{frames:6,ms:170}, walk:{frames:8,ms:110}, run:{frames:6,ms:80}, hold:{frames:6,ms:170}, cast:{frames:10,ms:70},
    dig:{frames:10,ms:90}, balance:{frames:8,ms:150}, stagger:{frames:10,ms:90}, bite:{frames:6,ms:150}, strike:{frames:6,ms:80},
    reel:{frames:12,ms:90}, land:{frames:12,ms:100}, castBack:{frames:6,ms:90}, castRelease:{frames:8,ms:70},
    board:{frames:10,ms:90,oneShot:true}, boardDown:{frames:6,ms:95,oneShot:true}, haul:{frames:8,ms:120}, ladderDown:{frames:10,ms:110},
    hauler:{frames:8,ms:115}, bench:{frames:10,ms:130}, chop:{frames:8,ms:105}, lift:{frames:8,ms:105,oneShot:true},
    place:{frames:8,ms:110,oneShot:true}, toss:{frames:8,ms:95,oneShot:true}, swim:{frames:8,ms:130}, tread:{frames:6,ms:170},
    sleep:{frames:6,ms:640}, drive:{frames:6,ms:170}, reach:{frames:6,ms:100,oneShot:true,settle:true},
    astride:{frames:6,ms:170}, astrideStand:{frames:8,ms:120}, mountUp:{frames:16,ms:80,oneShot:true,settle:true},
    mountDown:{frames:14,ms:85,oneShot:true,settle:true}, mountCab:{frames:18,ms:80,oneShot:true,settle:true},
    mountCabDown:{frames:16,ms:85,oneShot:true,settle:true} };
  /* the shipped table as a string, so the timing gate compares against something that is not this object */
  const ANIMS_SHIPPED = 'idle 6/170 walk 8/110 run 6/80 hold 6/170 cast 10/70 dig 10/90 balance 8/150 stagger 10/90 bite 6/150 strike 6/80 reel 12/90 land 12/100 castBack 6/90 castRelease 8/70 board 10/90! boardDown 6/95! haul 8/120 ladderDown 10/110 hauler 8/115 bench 10/130 chop 8/105 lift 8/105! place 8/110! toss 8/95! swim 8/130 tread 6/170 sleep 6/640 drive 6/170 reach 6/100!s astride 6/170 astrideStand 8/120 mountUp 16/80!s mountDown 14/85!s mountCab 18/80!s mountCabDown 16/85!s';
  const GROUPS = { base:['idle','walk','run'], balance:['balance','stagger'], fishing:['hold','cast','castBack','castRelease','bite','strike','reel','land'],
    boarding:['board','boardDown','ladderDown'], work:['dig','haul'], deck:['hauler','lift','place','bench','chop','toss'], water:['swim','tread'],
    rest:['sleep'], cab:['drive'], handover:['reach'], saddle:['astride','astrideStand','mountUp','mountDown','mountCab','mountCabDown'] };
  const ANIM_MOUNT = { idle:'free', walk:'free', run:'free', balance:'free', stagger:'free', hold:'rod', cast:'rod', castBack:'rod', castRelease:'rod',
    bite:'rod', strike:'rod', reel:'rod', land:'rod', dig:'shovel', board:'free', boardDown:'free', haul:'free', ladderDown:'ladder',
    hauler:'warp', bench:'bench', chop:'knife', lift:'load', place:'load', toss:'load', swim:'water', tread:'water', sleep:'bed', drive:'wheel',
    reach:'rest', astride:'saddle', astrideStand:'saddle', mountUp:'saddle', mountDown:'saddle', mountCab:'saddle', mountCabDown:'saddle' };
  const CARRIES = {
    buckets:{ label:'PAILS', pin:'carry_L + carry_R', anims:['idle','walk','run','board','boardDown'] },
    tray:   { label:'TRAY',  pin:'carry_mid',         anims:['idle','walk','run','board','boardDown'] },
    helm:   { label:'HELM',  pin:'carry_mid + carry_L + carry_R', anims:['idle','walk'] },
    oars:   { label:'OARS',  pin:'carry_L + carry_R', anims:['idle','walk'] },
    pot:    { label:'POT',   pin:'carry_mid',         anims:['idle','walk','board','boardDown'] } };
  /* loops in the table that are authored as segments of a chain (hold -> castBack -> castRelease; bite -> strike -> reel -> land):
     their frames play in order and their wrap is never shown */
  const SEGMENTS = { castBack:'hold -> castBack -> castRelease -> hold', castRelease:'castBack -> castRelease -> hold', strike:'bite -> strike -> reel', land:'reel -> land -> hold' };
  const CARRY_ORDER = ['buckets','tray','helm','oars','pot'];
  /* the four stances rig 7 part 1 named — same frames and ms as the clip they ride */
  const CARRY_CLIPS = { helm_idle:{ anim:'idle', carry:'helm' }, helm_walk:{ anim:'walk', carry:'helm' },
                        oars_idle:{ anim:'idle', carry:'oars' }, oars_row:{ anim:'walk', carry:'oars' } };
  function clipDef(name){
    if(ANIMS[name]) return { name, anim:name, carry:null };
    if(CARRY_CLIPS[name]) return Object.assign({ name, stance:name }, CARRY_CLIPS[name]);
    const m=/^(\w+)\+(\w+)$/.exec(name||'');
    if(m && ANIMS[m[1]] && CARRIES[m[2]] && CARRIES[m[2]].anims.indexOf(m[1])>=0 && m[2]!=='helm' && m[2]!=='oars') return { name, anim:m[1], carry:m[2] };
    return null;
  }
  function clipNames(){ const o=Object.keys(ANIMS).concat(Object.keys(CARRY_CLIPS));
    for(const c of ['buckets','tray','pot']) for(const a of CARRIES[c].anims) o.push(a+'+'+c); return o; }
  const modeOf=(anim)=>{ const A=ANIMS[anim]; return A.settle ? 'settle' : A.oneShot ? 'once' : 'loop'; };
  const uOf=(anim,k)=>{ const A=ANIMS[anim]; return k/(A.settle ? Math.max(1,A.frames-1) : A.frames); };

  /* ============================ world contracts (rig 6's opts, same names, same defaults) ============================ */
  const CONTRACT = {
    railZ:{ def:0.55, min:0.06, max:1.30 }, rung:{ def:0.30, min:0.18, max:0.45 }, ladderW:0.45, standoff:0.275,
    workZ:{ hauler:1.05, bench:0.885, chop:0.775, lift:0.93, place:0.93, toss:0.93, min:0.45, max:1.25 }, tossRelease:0.52,
    drive:{ seatZ:0.40, wheelZ:0.655, wheelY:0.315 }, bedZ:0.30,
    saddle:{ seat:[0,-0.30,0.94], gripL:[-0.33,0.42,1.04], gripR:[0.33,0.42,1.04], pegL:[-0.27,-0.06,0.42], pegR:[0.27,-0.06,0.42] },
    /* 9.2: the saddle the six saddle clips are gated at. The default saddle's seat (0.94 m) is higher than any body in the build
       space can sit down on from the ground; this is the default scaled about the pivot, a size at which every creator body sits
       astride and mounts with its standing foot on the ground until the seat carries it (world fit: every scale in 0.34-0.43 does) */
    saddleFit:{ scale:0.40, reachX:0.60 },
    pegAnkle:0.055, reachX:1.0,
    reach:{ rests:{ ground:0.00, stowV:0.95, stowH:1.05 }, gripRise:0.095, arrive:0.62, release:0.72, want:[0.132,0.265] } };
  /* the default saddle scaled about the pivot (world opts for any clip) */
  /* ... and the idle spot beside it (reachX) moved in with it: 1.0 m at the default size, 0.60 m at 0.40 (linear) */
  function saddleOpts(scale, reachX){ const S=CONTRACT.saddle, k=+scale; return { saddle:{ seat:S.seat.map(v=>v*k), gripL:S.gripL.map(v=>v*k), gripR:S.gripR.map(v=>v*k), pegL:S.pegL.map(v=>v*k), pegR:S.pegR.map(v=>v*k) },
    reachX: reachX!=null ? +reachX : +(0.60+(k-0.40)*(0.40/0.60)).toFixed(4) }; }
  function ctxOf(cd, opts, B){
    const o=opts||{}, n=(v,d)=>(v!=null && isFinite(+v)) ? +v : d, A=cd.anim;
    const railReq=n(o.railZ, CONTRACT.railZ.def), railZ=clamp(railReq, CONTRACT.railZ.min, CONTRACT.railZ.max);
    const rungReq=n(o.rung, CONTRACT.rung.def), rung=clamp(rungReq, CONTRACT.rung.min, CONTRACT.rung.max);
    const wDef=CONTRACT.workZ[A]!=null ? CONTRACT.workZ[A] : 0.93, workReq=n(o.workZ, wDef), workZ=clamp(workReq, CONTRACT.workZ.min, CONTRACT.workZ.max);
    const dv=CONTRACT.drive, drive={ seatZ:clamp(n(o.seatZ,dv.seatZ),0.25,0.70), wheelZ:clamp(n(o.wheelZ,dv.wheelZ),0.45,0.95), wheelY:clamp(n(o.wheelY,dv.wheelY),0.20,0.45) };
    drive.clamped = drive.seatZ!==n(o.seatZ,dv.seatZ) || drive.wheelZ!==n(o.wheelZ,dv.wheelZ) || drive.wheelY!==n(o.wheelY,dv.wheelY);
    const s=o.saddle||null, S=CONTRACT.saddle, pt=(p,d)=>(Array.isArray(p) && p.length>2 && p.every(isFinite)) ? [+p[0],+p[1],+p[2]] : d.slice();
    const saddle={ seat:pt(s&&(s.seat||s.seatM), [S.seat[0],S.seat[1],n(s&&s.seatZ,S.seat[2])]), gripL:pt(s&&(s.gripL||s.gripLM),S.gripL), gripR:pt(s&&(s.gripR||s.gripRM),S.gripR),
      pegL:pt(s&&(s.pegL||s.pegLM),S.pegL), pegR:pt(s&&(s.pegR||s.pegRM),S.pegR), bench:!!(s&&s.bench), leanDeg:n(s&&s.leanDeg,0), supplied:!!s };
    const R=CONTRACT.reach, liftReq = o.lift!=null ? n(o.lift,0) : (o.rest && R.rests[o.rest]!=null ? R.rests[o.rest] : 0), lift=clamp(liftReq,0,1.25);
    const want = (Array.isArray(o.want) && o.want.length>2) ? o.want.map(Number) : [R.want[0], R.want[1], lift+R.gripRise];
    return { D:B.D, B, o, cd, anim:A, carry:cd.carry||null, mode:modeOf(A), railZ, railReq, rung, rungReq, ladderW:n(o.ladderW,CONTRACT.ladderW), standoff:CONTRACT.standoff,
      workZ, workReq, sheave:(Array.isArray(o.sheave)&&o.sheave.length===3) ? o.sheave.slice() : [0.24,0.34,workZ+0.26],
      drive, bedZ:n(o.bedZ,CONTRACT.bedZ), saddle, mountSide:o.mountSide!=null ? (+o.mountSide<0?-1:1) : (saddle.gripL[0]<=saddle.gripR[0]?-1:1),
      reachX:o.reachX!=null ? Math.abs(n(o.reachX,1)) : CONTRACT.reachX,
      reach:{ lift, requested:o.lift!=null||o.rest ? liftReq : null, clamped:lift!==liftReq, rest:(o.rest && R.rests[o.rest]!=null)?o.rest:null, want, arrive:R.arrive, release:R.release, gripRise:R.gripRise },
      tier:o.tier||'coast', stoop:NO_STOOP[A] ? 0 : B.D.stoop, k:kOf(B.D), bp:(p)=>{ const k=kOf(B.D); return [p[0]*k.x, p[1]*k.a, zmOf(B.D,p[2])]; }, ft:(x,y)=>{ const k=kOf(B.D); return [x*k.f, y*k.l, B.D.ankle]; } };
  }

  /* ============================ palettes (KTC master ramps, dark -> light) ============================ */
  const SKINS = { porcelain:['#7a4a3e','#9a6353','#b8806c','#d29e88','#e6bda9','#f4dccb'], fair:['#6b4028','#8a5636','#a8724a','#c98d63','#e0a981','#f0c9a2'],
    rose:['#75402f','#96573f','#b47254','#d18f6d','#e7ae8d','#f6cfb4'], olive:['#4d3c22','#68522f','#85703f','#a28c55','#bda872','#d6c497'],
    tan:['#59331f','#754628','#936036','#b07d4a','#cc9a63','#e2b57e'], bronze:['#4a2a17','#63391f','#80502c','#9d6a3d','#b98653','#d1a271'],
    umber:['#3a2116','#4f2f1e','#68422a','#835838','#9e7049','#b98c60'], deep:['#301c12','#45291a','#5e3a24','#7a4f31','#966843','#b08055'],
    ebony:['#221a1a','#332727','#473838','#5d4b48','#75625c','#8e7a70'] };
  const HAIRS = { blond:['#7a5a1c','#946218','#b07d1f','#e0b13a','#f2cf6a'], sand:['#6b5636','#877049','#a68d60','#c4ab7c','#ddc79c'],
    black:['#0e1114','#171b21','#232a32','#333c46','#4a545f'], brown:['#33271b','#473627','#5e4630','#6b4f35','#8a6a48'],
    auburn:['#3d1a14','#57271c','#732f21','#8f4029','#ab5738'], ginger:['#5e2013','#7d301c','#9c4327','#b85835','#d07048'],
    salt:['#2a2f31','#40474a','#5c656a','#7d878b','#a3adaf'], grey:['#6f7a78','#878b85','#a2a7a0','#bcc2ba','#cfd4cc'], white:['#8a908c','#a3a8a3','#bcc0ba','#d3d6cf','#dfe1db'] };
  const OUTFITS = { teal:['#0d3f3c','#14554e','#1c7367','#2ba39a','#49b8aa'], navy:['#0e1526','#172644','#223764','#2f4c88','#4166ac'],
    oil:['#946218','#b07d1f','#cf9d24','#e0b13a','#f2cf6a'], rust:['#4a100e','#7c1a15','#a8241b','#cf3626','#e2573c'],
    slate:['#1b2429','#28353c','#374852','#485c68','#5c7280'], moss:['#20291f','#2e3a2a','#3e4d37','#4f6045','#627457'],
    black:['#0a0c0f','#13171b','#1e2429','#2a3239','#38424b'], char:['#1c1e21','#292d31','#383d43','#4a5057','#5e656d'],
    denim:['#18263a','#233651','#314a6b','#436288','#5a7ca3'], khaki:['#4a3e28','#645539','#7f6e4b','#9b8a62','#b6a57c'],
    cream:['#6b5f49','#8a7c62','#a99b7e','#c6b99b','#ddd2b7'], wine:['#2c0d16','#461522','#621f30','#7e2f42','#9a4556'],
    plum:['#22152c','#342143','#492f5b','#604173','#79588b'], rose:['#5b2634','#7b3547','#9d4b5d','#bd6776','#d48790'],
    sky:['#223b53','#2f5371','#416d8f','#5988ab','#79a5c5'], orange:['#4a1d0c','#7a3112','#a8461a','#d4632a','#e8834a'] };
  const SHIRTS = { white:['#878b85','#a2a7a0','#bcc2ba','#cfd4cc','#dfe3dc','#eef0ea'], cream:['#8c6a45','#a98352','#c2a06b','#d8c290','#ead9ae','#f4e8c8'],
    navy:['#0e1526','#172644','#223764','#2f4c88','#4166ac','#5a80c2'], red:['#4a100e','#7c1a15','#a8241b','#cf3626','#e2573c','#ee7a55'],
    moss:['#27312c','#333f37','#414e43','#505e50','#616f60','#748172'], slate:['#20282c','#2d383e','#3d4a52','#4e5e68','#61737e','#7a8b96'],
    ochre:['#5e4416','#7b5a20','#96712c','#b18b3d','#c8a555','#dcc077'], black:['#08090b','#0f1215','#171b20','#21272d','#2c343b','#3a434b'],
    sky:['#24394f','#304c68','#3f6384','#537ea0','#6d99bb','#8db4d1'], rose:['#5c2b37','#7d3b4b','#9f5360','#c1707a','#d88f97','#eab0b4'] };
  const HATCOLS = { oil:['#946218','#b07d1f','#cf9d24','#e0b13a','#f2cf6a'], navy:['#0e1526','#172644','#223764','#2f4c88','#4166ac'],
    rust:['#4a100e','#7c1a15','#a8241b','#cf3626','#e2573c'], teal:['#0d3f3c','#14554e','#1c7367','#2ba39a','#49b8aa'],
    char:['#14181b','#1f262a','#2c353b','#3d484f','#505d65'], oat:['#5c5140','#786a53','#948468','#b09e80','#c9b899'],
    black:['#08090b','#111418','#1b2026','#272e36','#353e48'], white:['#80857f','#9ea39c','#babfb8','#d2d6cf','#e6e9e3'], straw:['#6e5629','#8c6f36','#ab8c47','#c8a95f','#dec57f'] };
  const APRONS = { canvas:['#4d4436','#665b48','#7f735c','#988b71','#b0a488'], rubber:['#2b2f33','#3b4147','#4c545b','#606a72','#78838c'], ochre:['#6b4d16','#8a6520','#a87f2b','#c39a3f','#d9b45c'],
    black:['#08090b','#111418','#1b2026','#272e36','#353e48'], red:['#4a100e','#7c1a15','#a8241b','#cf3626','#e2573c'], navy:['#0e1526','#172644','#223764','#2f4c88','#4166ac'],
    white:['#80857f','#9ea39c','#babfb8','#d2d6cf','#e6e9e3'], orange:['#4a1d0c','#7a3112','#a8461a','#d4632a','#e8834a'], yellow:['#6b5212','#8f6e16','#b8901d','#dcb534','#f0d062'] };
  const BOOT=['#101317','#1d2127','#2b323a','#3d454e','#525c63'], BRASS=['#5a4318','#7d6024','#a68333','#c9a548','#e3c46a'], LEATHER=['#241611','#38221a','#4d2f22','#63402d','#7c553c'];
  /* INK (exported in 10.3): INK[0] is the eye's dark and the brow's mix, INK[1] the half tone. SHADING.keyline is another colour. */
  const INK_ROLES = { 0:'material ink: the open eye and the gaze pupil; the brow is mix(hair ramp[0], INK[0], 0.34)', 1:'material ink2: the half-shut round and lidded eye; the round eye\'s gaze turned in, seen from the side',
    keyline:'SHADING.keyline #101a19 is the A/B keyline\'s tint (keylineDefault false), a colour of its own, not INK[0]', iris:'no rig 10 face is drawn in iris: since 10.0 the eye is one pixel of ink, so EYES only names the colour' };
  const INK=['#12181b','#243036'], EYES={ sea:'#2ba39a', sky:'#4166ac', bark:'#6b4f35', slate:'#8a969b', amber:'#e0b13a', moss:'#627457', umber:'#4f2f1e' };
  const EYE_WHITE='#e6e9e2';
  const hex2=(c)=>[parseInt(c.slice(1,3),16),parseInt(c.slice(3,5),16),parseInt(c.slice(5,7),16)];
  const mixHex=(a,b,t)=>{ const A=hex2(a), B2=hex2(b); return '#'+[0,1,2].map(i=>Math.round(A[i]+(B2[i]-A[i])*t).toString(16).padStart(2,'0')).join(''); };
  const T6={ gain:2.4, bias:2.7, lo:2, hi:5 }, T5={ gain:2.4, bias:1.8, lo:1, hi:4 };
  function makeMats(b){
    const skin=SKINS[b.skin], hair=HAIRS[b.hair], over=OUTFITS[b.outfit], shirt=SHIRTS[b.shirt], hat=HATCOLS[b.hatCol], apron=APRONS[b.apronCol];
    const M=(ramp,t,off)=>({ ramp, gain:t.gain, bias:t.bias, lo:t.lo, hi:t.hi, off:off||0 }), fixed=(c)=>({ ramp:[c], fixed:true });
    /* 10.2: stubble is the skin taken a third of the way to the hair a step down, so it reads on any skin; the buzz is the hair let a
       quarter toward the skin, so it reads as short hair over the scalp and not a painted cap */
    const stubR=skin.map((c,i)=>mixHex(c, hair[clamp(i-1,0,4)], 0.36)), buzzR=hair.map((c,i)=>mixHex(c, skin[clamp(i+1,0,5)], 0.24));
    return { skin:M(skin,T6), skinD:M(skin,T6,-1), skinL:M(skin,T6,1), stub:M(stubR,T6), hair:M(hair,T5), hairD:M(hair,T5,-1), hairL:M(hair,T5,1), beard:M(hair,T5), buzz:M(buzzR,T5,-1),
      over:M(over,T5), overD:M(over,T5,-1), overL:M(over,T5,1), shirt:M(shirt,T6), shirtD:M(shirt,T6,-1), collar:M(shirt,T6,1),
      hat:M(hat,T5), hatL:M(hat,T5,1), hatD:M(hat,T5,-1), apron:M(apron,T5), apronD:M(apron,T5,-1), belt:M(LEATHER,T5), hose:M(BOOT,T5,1),
      boot:M(BOOT,T5), bootL:M(BOOT,T5,1), sole:M(BOOT,T5,-2), brass:M(BRASS,T5,1),
      ink:fixed(INK[0]), ink2:fixed(INK[1]), white:fixed(EYE_WHITE), iris:fixed(EYES[b.eyes]), lid:fixed(skin[1]),
      brow:fixed(mixHex(hair[0], INK[0], 0.34)), lip:fixed(skin[1]), mouth:fixed(skin[0]) };
  }

  /* ============================ the build ============================ */
  /* 10.1: sexK and frameK blend SEX and FRAMES in by age; bustK scales the bust */
  const AGES = { child:{ height:0.74, head:0.95, leg:0.84, torso:1.03, limbR:0.86, shoulder:0.84, neckLen:0.55, stoop:0, sexK:0.2, frameK:0.5, bustK:0 },
                 youth:{ height:0.91, head:0.98, leg:1.00, torso:0.97, limbR:0.92, shoulder:0.93, neckLen:0.90, stoop:0, sexK:0.85, frameK:0.85, bustK:0.75 },
                 adult:{ height:1.00, head:1.00, leg:1.00, torso:1.00, limbR:1.00, shoulder:1.00, neckLen:1.00, stoop:0, sexK:1, frameK:1, bustK:1 },
                 elder:{ height:0.965, head:1.00, leg:0.97, torso:0.99, limbR:0.95, shoulder:0.96, neckLen:0.80, stoop:8, sexK:1, frameK:1, bustK:1 } };
  /* body shape: value at 0 (broad-shouldered) and at 1 (wide-hipped); the Fisher sits at 0.25, where every factor is 1 */
  const SHAPE = { shoulder:[1.05,0.86], chest:[1.03,0.93], waist:[1.03,0.84], hip:[0.95,1.16], hipX:[0.98,1.10], neck:[1.06,0.86], jaw:[1.06,0.84], limbR:[1.03,0.90], height:[1.012,0.955], head:[1.01,0.98] };
  const shapeF=(k,s)=>{ const v=SHAPE[k]; return s<=0.25 ? lerp(v[0],1,s/0.25) : lerp(1,v[1],(s-0.25)/0.75); };
  /* 10.1: SEX is the base a sex starts from (the Fisher is the male base, every factor 1; shape is the norm the shape slider is read
     against); FRAMES are the body types within a sex. A factor multiplies what it names (sh the shoulder ring, shX the shoulder joint,
     arm / thigh / calf / hand the limb girths, depth the torso front to back); bust, belly and seat are metres added to the torso. */
  const SEX = { m:{ label:'Man', shape:0.25 },
    f:{ label:'Woman', shape:0.75, height:0.94, torso:0.97, sh:0.86, shX:0.89, chest:0.92, waist:0.80, hip:1.12, hipX:1.06, depth:0.95,
        bust:0.030, seat:0.014, arm:0.84, thigh:1.06, calf:0.92, neck:0.80, jaw:0.86, head:0.97, hand:0.86, foot:0.88 } };
  const FRAMES = {
    m:{ lean:{ label:'Lean', sh:0.95, shX:0.96, chest:0.92, waist:0.90, hip:0.94, depth:0.90, arm:0.88, thigh:0.88, calf:0.90, neck:0.92, leg:1.02 },
        average:{ label:'Average' },
        broad:{ label:'Broad', sh:1.12, shX:1.08, chest:1.10, waist:0.96, hip:0.98, depth:1.06, arm:1.18, thigh:1.08, calf:1.06, neck:1.14, jaw:1.06, hand:1.08 },
        stocky:{ label:'Stocky', height:0.97, leg:0.94, sh:1.06, shX:1.04, chest:1.10, waist:1.10, hip:1.06, depth:1.12, arm:1.14, thigh:1.16, calf:1.12, neck:1.18, jaw:1.08, hand:1.06, foot:1.04 },
        heavy:{ label:'Heavy', sh:1.04, shX:1.04, chest:1.08, waist:1.16, hip:1.10, depth:1.18, belly:0.046, seat:0.010, arm:1.16, thigh:1.18, calf:1.10, neck:1.16, jaw:1.10 } },
    f:{ slender:{ label:'Slender', sh:0.96, shX:0.97, chest:0.92, waist:0.90, hip:0.92, depth:0.90, bust:-0.012, seat:-0.006, arm:0.88, thigh:0.86, calf:0.90, neck:0.94, leg:1.02 },
        average:{ label:'Average' },
        hourglass:{ label:'Hourglass', chest:1.02, waist:0.88, hip:1.08, bust:0.016, seat:0.008, thigh:1.08 },
        pear:{ label:'Pear', sh:0.95, chest:0.96, waist:0.98, hip:1.16, hipX:1.06, bust:-0.008, seat:0.016, thigh:1.18, calf:1.06 },
        full:{ label:'Full', sh:1.04, shX:1.03, chest:1.10, waist:1.14, hip:1.12, depth:1.14, bust:0.020, belly:0.026, seat:0.012, arm:1.16, thigh:1.16, calf:1.10, neck:1.08 } } };
  const FKEYS=['height','leg','torso','sh','shX','chest','waist','hip','hipX','depth','arm','thigh','calf','neck','jaw','head','hand','foot'], FADD=['bust','belly','seat'];
  function frameOf(b){ const ag=AGES[b.age]||AGES.adult, S0=SEX[b.sex]||SEX.m, Fr=(FRAMES[b.sex]||FRAMES.m)[b.frame]||{}, o={};
    for(const k of FKEYS) o[k]=lerp(1, S0[k]==null?1:S0[k], ag.sexK)*lerp(1, Fr[k]==null?1:Fr[k], ag.frameK);
    for(const k of FADD) o[k]=Math.max(0, ((S0[k]||0)*ag.sexK + (Fr[k]||0)*ag.frameK)*(k==='bust'?ag.bustK:1));
    return o; }
  /* head shapes: half-width multipliers per ring (chin, jaw, cheek, temple, crown, cap), chin depth, vertical stretch about the eye line, chamfer */
  const HEADS = { round:{ w:[1,1,1,1,1,1], chin:1, z:1, c:1 }, oval:{ w:[0.80,0.88,0.96,0.98,0.96,0.95], chin:0.95, z:1.05, c:1.1 },
    square:{ w:[1.35,1.15,1.03,1.02,1.07,1.20], chin:1.08, z:1, c:0.55 }, long:{ w:[0.92,0.92,0.93,0.94,0.94,0.94], chin:1, z:1.12, c:1 },
    heart:{ w:[0.60,0.80,1.02,1.08,1.06,1.02], chin:0.9, z:1.02, c:1 }, wide:{ w:[1.42,1.24,1.06,0.98,0.95,0.96], chin:1.06, z:0.97, c:0.8 },
    pear:{ w:[1.30,1.20,1.08,0.92,0.84,0.84], chin:1.04, z:1, c:0.9 } };
  const HR0=[[1.135,0.082,0.128,0.030,0.030],[1.195,0.140,0.152,0.100,0.050],[1.300,0.166,0.160,0.148,0.062],[1.425,0.160,0.150,0.156,0.066],[1.500,0.124,0.110,0.124,0.052],[1.522,0.074,0.060,0.078,0.030]];
  /* garments: band(i,k) is the material of torso band i (0 = belt line 0.66-0.70 ... 7 = collar slope) on octagon side k */
  const backSide=(k)=>(k>=4 && k<=6);
  /* top:true garments take a bottom; the rest bring their own. wear names the colour fields each one shows (outfit, shirt, trim = apronCol);
     '%' is the bottom's name. look is the palette the creator applies when you switch into it. */
  const GARMENTS = {
    tee:{ label:'T-shirt', top:true, sleeve:'short', collar:'crew', boot:0.15, band:()=> 'shirt', wear:{ shirt:'T-shirt' } },
    longsleeve:{ label:'Long sleeve', top:true, sleeve:'long', cuffMat:'shirtD', collar:'crew', boot:0.15, band:()=> 'shirt', wear:{ shirt:'Top' } },
    workshirt:{ label:'Work shirt', top:true, sleeve:'rolled', collar:'open', boot:0.15, belt:true, band:(i)=> i===0 ? 'belt' : 'shirt', wear:{ shirt:'Shirt' } },
    sweater:{ label:'Knit jumper', short:'Jumper', top:true, sleeve:'long', cuffMat:'shirtD', collar:'roll', boot:0.17, band:(i)=> i===0 ? 'shirtD' : 'shirt', wear:{ shirt:'Jumper' } },
    vest:{ label:'Quilted vest', short:'Vest', top:true, sleeve:'long', cuffMat:'shirtD', collar:'stand', collarMat:'over', boot:0.15, belt:true, vest:true,
      band:(i)=> i===0 ? 'belt' : 'over', wear:{ outfit:'Vest and %', shirt:'Shirt' } },   /* 10.1: over the shoulders too, the shirt in a V (9.2 opened the whole front plane) */
    apron:{ label:'Gutting apron', short:'Apron', top:true, sleeve:'short', collar:'crew', boot:0.24, belt:true, apron:true, band:(i)=> i===0 ? 'belt' : 'shirt', wear:{ shirt:'Shirt', trim:'Apron' } },
    /* 10.2: tops */
    breton:{ label:'Breton top', short:'Breton', top:true, sleeve:'long', cuffMat:'shirtD', collar:'crew', boot:0.15, stripes:true, band:()=> 'shirt', wear:{ shirt:'Top', trim:'Stripes' }, look:{ shirt:'white', apronCol:'navy' } },
    hoodie:{ label:'Hoodie', top:true, sleeve:'long', cuffMat:'shirtD', collar:'none', hem:true, hoodBack:true, pocket:true, boot:0.15, band:(i)=> i===0 ? 'shirtD' : 'shirt', wear:{ shirt:'Hoodie' } },
    cardigan:{ label:'Cardigan', top:true, sleeve:'long', sleeveMat:'over', cuffMat:'overD', collar:'crew', hem:true, placket:true, legMat:'overD', seatMat:'overD', boot:0.15, band:(i)=> i===0 ? 'overD' : 'over', wear:{ outfit:'Cardigan and %', shirt:'Shirt' } },
    peacoat:{ label:'Pea coat', top:true, sleeve:'long', sleeveMat:'over', cuffMat:'overD', collar:'stand', collarMat:'over', jacket:true, lapel:'overL', buttons:'brass', legMat:'overD', seatMat:'overD', boot:0.15, band:()=> 'over', wear:{ outfit:'Coat and %', shirt:'Shirt' }, look:{ outfit:'navy' } },
    lifevest:{ label:'Life vest', top:true, sleeve:'short', collar:'crew', boot:0.15, pfd:true, band:()=> 'shirt', wear:{ shirt:'T-shirt', trim:'Life vest' }, look:{ apronCol:'orange' } },
    singlet:{ label:'Singlet', top:true, sleeve:'none', collar:'none', boot:0.15, band:(i,k)=> i>=7 ? 'skin' : i===6 ? ((k===1||k===5) ? 'skin' : 'shirt') : 'shirt', wear:{ shirt:'Singlet' } },
    overalls:{ label:'Bib overalls', short:'Overalls', bib:0.985, brass:true, sleeve:'short', collar:'crew', boot:0.214, band:(i,k)=> i<=2 ? 'over' : i===3 ? (backSide(k)?'over':'shirt') : 'shirt', wear:{ outfit:'Overalls', shirt:'Shirt' } },
    oilskins:{ label:'Oilskins', bib:1.03, sleeve:'long', sleeveMat:'over', cuffMat:'overD', collar:'stand', collarMat:'over', boot:0.30, band:(i)=> i<=6 ? 'over' : 'shirt', wear:{ outfit:'Oilskins', shirt:'Shirt' } },
    skirt:{ label:'Skirt and shawl', short:'Shawl', fem:true, sleeve:'long', cuffMat:'shirtD', collar:'crew', boot:0.12, skirt:true, shawl:true, legMat:'hose', band:(i)=> i===0 ? 'overD' : 'shirt', wear:{ outfit:'Skirt and shawl', shirt:'Blouse' } },
    suit:{ label:'Suit', sleeve:'long', sleeveMat:'over', cuffMat:'shirt', collar:'stand', jacket:true, tie:'long', legMat:'overD', seatMat:'overD', boot:0.12, band:()=> 'over',
      wear:{ outfit:'Suit', shirt:'Shirt', trim:'Tie' }, look:{ outfit:'char', shirt:'white', apronCol:'red' } },
    tux:{ label:'Tuxedo', sleeve:'long', sleeveMat:'over', cuffMat:'shirt', collar:'stand', jacket:true, lapel:'overL', tie:'bow', legMat:'overD', seatMat:'overD', boot:0.12, band:()=> 'over',
      wear:{ outfit:'Tuxedo', shirt:'Shirt', trim:'Bow tie' }, look:{ outfit:'black', shirt:'white', apronCol:'black' } },
    dress:{ label:'Dress', fem:true, sleeve:'short', sleeveMat:'over', hemMat:'overD', collar:'none', skirt:true, legMat:'skin', boot:0.12,
      band:(i,k)=> i===1 ? 'apron' : (i>=6 && k<=2) ? 'skin' : 'over', wear:{ outfit:'Dress', trim:'Sash' } },
    gown:{ label:'Gown', fem:true, sleeve:'none', collar:'none', skirt:'long', legMat:'hose', boot:0.12, band:(i)=> i>=6 ? 'skin' : 'over', wear:{ outfit:'Gown' } },
    /* 10.2: outfits */
    waders:{ label:'Chest waders', short:'Waders', bib:1.00, sleeve:'long', cuffMat:'shirtD', collar:'crew', boot:0.32, bootMat:'overD', belt:true, band:(i,k)=> i===0 ? 'belt' : i<=3 ? 'over' : i===4 ? (backSide(k)?'over':'shirt') : 'shirt', wear:{ outfit:'Waders', shirt:'Shirt' }, look:{ outfit:'moss' } },
    coveralls:{ label:'Coveralls', sleeve:'long', sleeveMat:'over', cuffMat:'overD', collar:'open', collarMat:'over', boot:0.17, belt:true, pockets:true, band:(i)=> i===0 ? 'belt' : 'over', wear:{ outfit:'Coveralls' }, look:{ outfit:'navy' } },
    greatcoat:{ label:'Greatcoat', short:'Coat', sleeve:'long', sleeveMat:'over', cuffMat:'overD', collar:'scarf', jacket:true, lapel:'overL', buttons:'brass', coat:0.40, legMat:'overD', seatMat:'overD', boot:0.17, band:()=> 'over', wear:{ outfit:'Coat', shirt:'Shirt', trim:'Scarf' }, look:{ outfit:'char', apronCol:'red' } },
    wetsuit:{ label:'Wetsuit', sleeve:'long', sleeveMat:'over', cuffMat:'over', collar:'stand', collarMat:'over', boot:0.20, bootMat:'overD', band:(i,k)=> i===6 ? 'apron' : ((k===3||k===7) && i>=1 && i<=5) ? 'apron' : 'over', wear:{ outfit:'Wetsuit', trim:'Panels' }, look:{ outfit:'black', apronCol:'orange' } },
    sundress:{ label:'Sundress', fem:true, sleeve:'none', collar:'none', skirt:true, legMat:'skin', boot:0.10, band:(i,k)=> i>=7 ? 'skin' : i===6 ? ((k===1||k===5) ? 'skin' : 'over') : 'over', wear:{ outfit:'Sundress' } },
    pinafore:{ label:'Pinafore', fem:true, bib:0.99, sleeve:'long', cuffMat:'shirtD', collar:'crew', skirt:true, legMat:'hose', boot:0.12, band:(i,k)=> i<=3 ? 'over' : i===4 ? (backSide(k)?'over':'shirt') : 'shirt', wear:{ outfit:'Pinafore', shirt:'Blouse' } },
    /* 10.2: swimwear. Bare feet. Trunks and briefs give a woman a top (swimTop 'f'); the bikini always has one */
    trunks:{ label:'Swim trunks', short:'Trunks', swim:true, bare:true, sleeve:'none', collar:'none', shorts:true, swimTop:'f', boot:0.10, band:()=> 'skin', wear:{ outfit:'Trunks' }, look:{ outfit:'sky' } },
    briefs:{ label:'Swim briefs', short:'Briefs', swim:true, bare:true, sleeve:'none', collar:'none', legMat:'skin', swimTop:'f', boot:0.10, band:()=> 'skin', wear:{ outfit:'Briefs' }, look:{ outfit:'navy' } },
    swimsuit:{ label:'Swimsuit', fem:true, swim:true, bare:true, suit:true, sleeve:'none', collar:'none', legMat:'skin', boot:0.10, band:(i,k)=> i<=4 ? 'over' : i===5 ? ((k===1||k===5) ? 'skin' : 'over') : i===6 ? ((k===0||k===2||k===4||k===6) ? 'over' : 'skin') : 'skin', wear:{ outfit:'Swimsuit' }, look:{ outfit:'navy' } },
    bikini:{ label:'Bikini', fem:true, swim:true, bare:true, sleeve:'none', collar:'none', legMat:'skin', swimTop:'all', boot:0.10, band:()=> 'skin', wear:{ outfit:'Bikini' }, look:{ outfit:'rose' } } };
  /* 10.2: cropped trousers end mid-calf over a rolled cuff; fem marks what randomBuild never puts on a man */
  const BOTTOMS = { trousers:{ label:'Trousers' }, shorts:{ label:'Shorts', shorts:true }, cropped:{ label:'Cropped', cropped:true }, skirt:{ label:'Skirt', skirt:true, fem:true }, longskirt:{ label:'Long skirt', skirt:'long', fem:true } };
  /* the garment a build wears: an outfit as authored; a top with trousers as authored (so every 9.0 build is unchanged); a top with shorts or a skirt merged */
  function garmentOf(b){ const G=GARMENTS[b.garment]||GARMENTS.overalls, Bt=G.top ? (BOTTOMS[b.bottom]||BOTTOMS.trousers) : null, top=G.swimTop==='all' || (G.swimTop==='f' && b.sex==='f');
    if((!Bt || (!Bt.shorts && !Bt.skirt && !Bt.cropped)) && !top) return G; const o=Object.assign({}, G); if(top) o.swimTopOn=true; if(!Bt) return o;
    if(Bt.shorts) o.shorts=true; if(Bt.cropped) o.cropped=true; if(Bt.skirt){ o.skirt=Bt.skirt; o.legMat='hose'; o.boot=Math.min(G.boot,0.12); } return o; }
  /* hats: the canonical head z their lowest ring sits at, front / side / back */
  const HATS = { none:null, watchcap:{ F:1.432, S:1.40, B:1.36, e:0.024 }, souwester:{ F:1.455, S:1.42, B:1.38, e:0.024 }, ballcap:{ F:1.46, S:1.44, B:1.41, e:0.020 },
    kerchief:{ F:1.435, S:1.37, B:1.28, e:0.026 }, flatcap:{ F:1.45, S:1.44, B:1.43, e:0.028 }, hood:{ F:1.445, S:1.20, B:1.16, e:0.032 },
    tophat:{ F:1.445, S:1.445, B:1.445, e:0.022, h:0.22, look:{ hatCol:'black' } }, bowler:{ F:1.44, S:1.44, B:1.44, e:0.022, look:{ hatCol:'black' } },
    captain:{ F:1.445, S:1.445, B:1.445, e:0.024, look:{ hatCol:'white' } }, sunhat:{ F:1.462, S:1.462, B:1.462, e:0.020, look:{ hatCol:'straw' } },
    bucket:{ F:1.44, S:1.44, B:1.44, e:0.024 }, beret:{ F:1.455, S:1.455, B:1.455, e:0.018 } };
  /* eye shapes: cells [col0, col1, row0, row1, material] per state; col 1 is next to the nose, rows are the pass-8 face rows (3 upper, 4 lower).
     The default open eye is the wide read: a pupil (ink over iris) and a white beside it. */
  /* 10.0: the eye on a seven-head figure is one pixel: col 1 beside the 1 px nose column, row 3 (the eye row). open is the pupil,
     half a lighter mark, shut the lid's line, wide adds a white outside it; the gaze moves the pupil out a column on the side it looks to. */
  const EYE_SHAPES = {
    round: { open:[[1,1,3,3,'ink']], half:[[1,1,3,3,'ink2']], shut:[[1,2,3,3,'lid']], wide:[[1,1,3,3,'ink'],[2,2,3,3,'white']],
             gaze:{ out:{ near:[[2,2,3,3,'ink']], side:[[1,1,3,3,'ink']], far:[[1,1,3,3,'white']] }, in:{ near:'open', side:[[1,1,3,3,'ink2']] } } },
    narrow:{ open:[[1,1,3,3,'ink']], half:[[1,1,3,3,'lid']], shut:[[1,2,3,3,'lid']], wide:[[1,1,3,3,'ink'],[2,2,3,3,'white']],
             gaze:{ out:{ near:[[2,2,3,3,'ink']], side:[[1,1,3,3,'ink']], far:[[1,1,3,3,'white']] }, in:{ near:'open', side:[[1,1,3,3,'lid']] } } },
    lidded:{ open:[[1,1,3,3,'ink'],[1,2,2,2,'lid']], half:[[1,1,3,3,'ink2'],[1,2,2,2,'lid']], shut:[[1,2,3,3,'lid'],[1,2,2,2,'lid']], wide:[[1,1,3,3,'ink'],[2,2,3,3,'white']],
             gaze:{ out:{ near:[[2,2,3,3,'ink'],[1,2,2,2,'lid']], side:[[1,1,3,3,'ink']], far:[[1,1,3,3,'white'],[1,1,2,2,'lid']] }, in:{ near:'open', side:[[1,1,3,3,'lid']] } } } };
  /* THE HAIR REGION (9.2). Where each style's hair is, in canonical head z. The skull faces inside it take the hair's material (the
     buzz and the bald horseshoe are only that), so a gap in the hair shell shows hair, never scalp. F the front hairline (front plane),
     C the corners (front chamfers), T the temple in front of the ear, E the top of the ear, N the nape (back and behind the ear). On a side
     plane the boundary is T in front of the ear (y > EAR.front), E over it, N behind it (y < EAR.back). Everything below is meant to be
     skin: the face, the forehead below F, the temples below T, the ear, the nape below N. */
  const EAR = { front:0.016, back:-0.032, top:1.333, bottom:1.255 };
  /* 10.1: the front corners' hair starts at 1.39 (9.2: 1.34-1.36), so at the diagonals the near corner's hair clears the eye row */
  const HAIRLINES = { crop:{ F:1.452, C:1.39, N:1.245 }, mop:{ C:1.39, N:1.225 }, bob:{ C:1.39, N:1.20 }, long:{ C:1.39, N:1.20 }, bun:{ C:1.39, N:1.26 }, ponytail:{ C:1.39, N:1.26 },
    buzz:{ F:1.425, C:1.425, N:1.30, mat:'buzz' }, bald:{ horseshoe:[1.30,1.425], mat:'buzz' } };
  const HAIR_T = 1.425;
  function hairlineOf(style, fringeZ, earTop){ const h=HAIRLINES[style]; if(!h) return null;
    if(h.horseshoe) return { style, horseshoe:h.horseshoe.slice(), mat:h.mat, E:earTop, earFront:EAR.front, earBack:EAR.back };
    return { style, F:h.F!=null?h.F:fringeZ, C:h.C, T:HAIR_T, E:earTop, N:h.N, mat:h.mat||'hair', earFront:EAR.front, earBack:EAR.back }; }
  /* skull side k (0..7 the octagon sides, 8 the top): front 1, corners 0 and 2, sides 3 and 7, back 4 to 6 */
  const hairSide=(k)=>k===8?'top':k===1?'front':(k===0||k===2)?'corner':(k>=4&&k<=6)?'back':'side';
  function hairBound(HL, side, y){ if(!HL || HL.horseshoe) return null; if(side==='top') return -Infinity; if(side==='front') return HL.F; if(side==='corner') return HL.C; if(side==='back') return HL.N;
    return y>HL.earFront ? HL.T : y>=HL.earBack ? HL.E : HL.N; }
  function inHair(HL, side, y, zc){ if(!HL) return false; if(HL.horseshoe) return side==='back' && zc>=HL.horseshoe[0] && zc<=HL.horseshoe[1]; return zc>=hairBound(HL,side,y); }
  /* 10.2: BEARDS. A beard is up to three things. The skull's two lower rings painted: r0 the chin ring (1.135-1.195), r1 the jaw ring
     (1.195-1.30), by octagon side (1 front, 0 and 2 the front corners, 3 and 7 the sides, 4-6 the back); mat the material, bot the
     chin's underside. Point marks laid for the eight facings on the face rows (marks: row -1 brow, 0 eye, 1 moustache, 2 mouth; T the
     moustache, the nose column and one each side, the face's front in profile; E the edge on the ear's side, both edges from ahead;
     A every column the camera sees on the skull; tache the moustache's material where a row is filled). And a mass: a solid from the
     mouth row down past the chin, proud of the jaw, so at 40 deg a chin beard stands in front of the collar (w the jaw's half-width
     at its top, wMid and wEnd half-widths in pixels at the chin and the end, drop the end's rows below the chin row, fwd its lean). */
  const BEARDS = {
    none:{ label:'None' },
    stubble:{ label:'Stubble', r0:[0,1,2,3,4,5,6,7], r1:[0,1,2,3,7], mat:'stub', bot:'stub' },
    moustache:{ label:'Moustache', marks:{ 1:'T' } },
    chinstrap:{ label:'Chinstrap', r0:[0,1,2,3,7], r1:[3,7], bot:'beard', marks:{ 0:'E', 1:'E', 2:'E' }, mass:{ w:0.86, wMid:1.4, wEnd:1.3, drop:0.55, fwd:0 } },
    goatee:{ label:'Goatee', r0:[1], bot:'beard', mass:{ w:0.18, wMid:0.6, wEnd:0.45, drop:1.6, fwd:0.012 } },
    vandyke:{ label:'Van Dyke', r0:[1], bot:'beard', marks:{ 1:'T' }, mass:{ w:0.22, wMid:0.7, wEnd:0.3, drop:1.9, fwd:0.014 } },
    sideburns:{ label:'Sideburns', r1:[3,7], marks:{ '-1':'E', 0:'E' } },
    mutton:{ label:'Mutton chops', r0:[3,7], r1:[0,2,3,7], marks:{ '-1':'E', 0:'E', 1:'ET' } },
    full:{ label:'Full', r0:[0,1,2,3,7], r1:[0,1,2,3,7], bot:'beard', marks:{ 0:'E', 1:'A', 2:'A' }, tache:'hairD', mass:{ w:0.94, wMid:1.8, wEnd:1.3, drop:1.4, fwd:0.012 } },
    long:{ label:'Long', r0:[0,1,2,3,7], r1:[0,1,2,3,7], bot:'beard', marks:{ 0:'E', 1:'A', 2:'A' }, tache:'hairD', mass:{ w:0.94, wMid:1.8, wEnd:0.6, drop:3.6, fwd:0.020 } } };
  const OPTIONS = { shape:[0,0.25,0.5,0.75,1], height:[-2,-1,0,1,2], weight:[-2,-1,0,1,2], age:['child','youth','adult','elder'],
    head:Object.keys(HEADS), skin:Object.keys(SKINS), hair:Object.keys(HAIRS), hairStyle:['crop','mop','bob','long','bun','ponytail','buzz','bald'],
    beard:Object.keys(BEARDS), eyes:Object.keys(EYES), eyeShape:Object.keys(EYE_SHAPES),
    garment:Object.keys(GARMENTS), outfit:Object.keys(OUTFITS), shirt:Object.keys(SHIRTS), hat:Object.keys(HATS), hatCol:Object.keys(HATCOLS), apronCol:Object.keys(APRONS), bottom:Object.keys(BOTTOMS),
    sex:Object.keys(SEX), frame:Object.keys(FRAMES.m).concat(Object.keys(FRAMES.f).filter(k=>!FRAMES.m[k])) };
  /* 10.1: sex and frame are the last two fields (frame is checked against the sex's own list) */
  const FIELDS = ['shape','age','height','weight','head','skin','hair','hairStyle','beard','eyes','eyeShape','garment','outfit','shirt','hat','hatCol','apronCol','bottom','sex','frame'];

  /* ============================ the cast ============================ */
  const BUILDS = {
    fisher:  { label:'Fisher',     shape:0.25, age:'adult', height:0,  weight:0,  head:'round',  skin:'fair',      hair:'blond',  hairStyle:'mop',      beard:'stubble', eyes:'sea',   eyeShape:'round',  garment:'overalls',  outfit:'teal',  shirt:'white', hat:'none',      hatCol:'char', apronCol:'canvas' },
    ginny:   { label:'Ginny',      shape:0.75, age:'adult', height:0,  weight:-1, head:'heart',  skin:'rose',      hair:'ginger', hairStyle:'bob',      beard:'none',    eyes:'amber', eyeShape:'round',  garment:'overalls',  outfit:'rust',  shirt:'moss',  hat:'hood',      hatCol:'navy', apronCol:'canvas' },
    skipper: { label:'Skipper',    shape:0,    age:'elder', height:1,  weight:1,  head:'square', skin:'tan',       hair:'salt',   hairStyle:'crop',     beard:'full',    eyes:'slate', eyeShape:'lidded', garment:'oilskins',  outfit:'oil',   shirt:'navy',  hat:'souwester', hatCol:'oil',  apronCol:'canvas' },
    nan:     { label:'Nan',        shape:0.75, age:'elder', height:-1, weight:1,  head:'round',  skin:'porcelain', hair:'white',  hairStyle:'bun',      beard:'none',    eyes:'sky',   eyeShape:'lidded', garment:'skirt',     outfit:'slate', shirt:'cream', hat:'kerchief',  hatCol:'rust', apronCol:'canvas' },
    deckboss:{ label:'Deck boss',  shape:0,    age:'adult', height:2,  weight:2,  head:'wide',   skin:'deep',      hair:'black',  hairStyle:'buzz',     beard:'mutton',  eyes:'bark',  eyeShape:'narrow', garment:'vest',      outfit:'navy',  shirt:'slate', hat:'flatcap',   hatCol:'char', apronCol:'canvas' },
    packer:  { label:'Packer',     shape:1,    age:'adult', height:0,  weight:1,  head:'pear',   skin:'umber',     hair:'black',  hairStyle:'bun',      beard:'none',    eyes:'bark',  eyeShape:'round',  garment:'apron',     outfit:'slate', shirt:'white', hat:'kerchief',  hatCol:'teal', apronCol:'canvas' },
    cutter:  { label:'Cutter',     shape:0.75, age:'youth', height:-1, weight:-1, head:'oval',   skin:'olive',     hair:'auburn', hairStyle:'ponytail', beard:'none',    eyes:'moss',  eyeShape:'narrow', garment:'apron',     outfit:'moss',  shirt:'ochre', hat:'none',      hatCol:'oat',  apronCol:'rubber' },
    hand:    { label:'Deckhand',   shape:0.25, age:'youth', height:1,  weight:-1, head:'long',   skin:'bronze',    hair:'brown',  hairStyle:'crop',     beard:'none',    eyes:'bark',  eyeShape:'round',  garment:'workshirt', outfit:'slate', shirt:'ochre', hat:'ballcap',   hatCol:'navy', apronCol:'canvas' },
    boy:     { label:'Wharf boy',  shape:0.25, age:'child', height:0,  weight:0,  head:'round',  skin:'fair',      hair:'brown',  hairStyle:'mop',      beard:'none',    eyes:'sky',   eyeShape:'round',  garment:'sweater',   outfit:'navy',  shirt:'red',   hat:'watchcap',  hatCol:'navy', apronCol:'canvas' },
    girl:    { label:'Wharf girl', shape:0.5,  age:'child', height:0,  weight:-1, head:'oval',   skin:'ebony',     hair:'black',  hairStyle:'long',     beard:'none',    eyes:'umber', eyeShape:'round',  garment:'workshirt', outfit:'moss',  shirt:'cream', hat:'none',      hatCol:'oat',  apronCol:'canvas' } };
  for(const k of Object.keys(BUILDS)) BUILDS[k].bottom = BUILDS[k].garment==='skirt' ? 'skirt' : 'trousers';
  /* 10.1: each preset's sex and frame */
  const SEXFRAME = { fisher:['m','average'], ginny:['f','average'], skipper:['m','stocky'], nan:['f','full'], deckboss:['m','broad'], packer:['f','pear'], cutter:['f','slender'], hand:['m','lean'], boy:['m','average'], girl:['f','average'] };
  for(const k of Object.keys(SEXFRAME)){ BUILDS[k].sex=SEXFRAME[k][0]; BUILDS[k].frame=SEXFRAME[k][1]; }
  const CAST10 = ['fisher','ginny','skipper','nan','deckboss','packer','cutter','hand','boy','girl'];
  /* 10.2: twenty more of the game's cast. Each gives its sex, frame and bottom; a hat that covers the hair keeps its style for when it comes off */
  const NPC20 = {
    harbourmaster:{ label:'Harbourmaster', sex:'m', frame:'broad', shape:0.25, age:'elder', height:1, weight:1, head:'square', skin:'tan', hair:'grey', hairStyle:'crop', beard:'vandyke', eyes:'slate', eyeShape:'lidded', garment:'peacoat', bottom:'trousers', outfit:'navy', shirt:'white', hat:'captain', hatCol:'white', apronCol:'canvas' },
    lightkeeper:{ label:'Lightkeeper', sex:'f', frame:'slender', shape:0.75, age:'elder', height:1, weight:-1, head:'long', skin:'porcelain', hair:'white', hairStyle:'bun', beard:'none', eyes:'sea', eyeShape:'narrow', garment:'greatcoat', bottom:'trousers', outfit:'slate', shirt:'cream', hat:'watchcap', hatCol:'navy', apronCol:'ochre' },
    ferry:{ label:'Ferry captain', sex:'f', frame:'average', shape:0.75, age:'adult', height:0, weight:0, head:'oval', skin:'deep', hair:'black', hairStyle:'ponytail', beard:'none', eyes:'bark', eyeShape:'round', garment:'vest', bottom:'trousers', outfit:'navy', shirt:'white', hat:'captain', hatCol:'navy', apronCol:'canvas' },
    monger:{ label:'Fishmonger', sex:'m', frame:'heavy', shape:0.25, age:'adult', height:0, weight:2, head:'round', skin:'rose', hair:'ginger', hairStyle:'crop', beard:'full', eyes:'sky', eyeShape:'round', garment:'apron', bottom:'trousers', outfit:'char', shirt:'sky', hat:'flatcap', hatCol:'char', apronCol:'white' },
    mender:{ label:'Net mender', sex:'f', frame:'full', shape:0.75, age:'elder', height:-1, weight:1, head:'round', skin:'umber', hair:'grey', hairStyle:'bun', beard:'none', eyes:'umber', eyeShape:'lidded', garment:'cardigan', bottom:'longskirt', outfit:'moss', shirt:'cream', hat:'kerchief', hatCol:'rust', apronCol:'canvas' },
    boatwright:{ label:'Boatwright', sex:'m', frame:'stocky', shape:0.25, age:'adult', height:0, weight:1, head:'wide', skin:'bronze', hair:'brown', hairStyle:'mop', beard:'chinstrap', eyes:'bark', eyeShape:'narrow', garment:'singlet', bottom:'trousers', outfit:'khaki', shirt:'white', hat:'none', hatCol:'oat', apronCol:'canvas' },
    postie:{ label:'Postmistress', sex:'f', frame:'hourglass', shape:0.75, age:'adult', height:0, weight:0, head:'heart', skin:'fair', hair:'auburn', hairStyle:'bob', beard:'none', eyes:'moss', eyeShape:'round', garment:'pinafore', bottom:'trousers', outfit:'sky', shirt:'white', hat:'beret', hatCol:'navy', apronCol:'canvas' },
    grocer:{ label:'Grocer', sex:'m', frame:'heavy', shape:0.25, age:'elder', height:-1, weight:1, head:'pear', skin:'porcelain', hair:'white', hairStyle:'bald', beard:'moustache', eyes:'sky', eyeShape:'lidded', garment:'cardigan', bottom:'trousers', outfit:'wine', shirt:'white', hat:'none', hatCol:'char', apronCol:'canvas' },
    diver:{ label:'Diver', sex:'f', frame:'average', shape:0.75, age:'youth', height:1, weight:-1, head:'oval', skin:'ebony', hair:'black', hairStyle:'buzz', beard:'none', eyes:'umber', eyeShape:'narrow', garment:'wetsuit', bottom:'trousers', outfit:'black', shirt:'white', hat:'none', hatCol:'char', apronCol:'orange' },
    lobsterman:{ label:'Lobsterman', sex:'m', frame:'lean', shape:0.25, age:'adult', height:1, weight:-1, head:'long', skin:'fair', hair:'sand', hairStyle:'crop', beard:'stubble', eyes:'sea', eyeShape:'narrow', garment:'waders', bottom:'trousers', outfit:'moss', shirt:'red', hat:'ballcap', hatCol:'teal', apronCol:'canvas' },
    cafe:{ label:'Caf\u00e9 owner', sex:'f', frame:'pear', shape:0.75, age:'adult', height:-1, weight:1, head:'round', skin:'olive', hair:'brown', hairStyle:'long', beard:'none', eyes:'amber', eyeShape:'round', garment:'breton', bottom:'skirt', outfit:'slate', shirt:'cream', hat:'none', hatCol:'oat', apronCol:'red' },
    swimmer:{ label:'Swimmer', sex:'m', frame:'average', shape:0.25, age:'youth', height:1, weight:-1, head:'long', skin:'deep', hair:'black', hairStyle:'crop', beard:'none', eyes:'bark', eyeShape:'round', garment:'trunks', bottom:'trousers', outfit:'sky', shirt:'white', hat:'none', hatCol:'char', apronCol:'canvas' },
    oldsalt:{ label:'Old salt', sex:'m', frame:'average', shape:0, age:'elder', height:0, weight:0, head:'square', skin:'ebony', hair:'white', hairStyle:'bald', beard:'long', eyes:'slate', eyeShape:'lidded', garment:'peacoat', bottom:'trousers', outfit:'char', shirt:'cream', hat:'watchcap', hatCol:'char', apronCol:'canvas' },
    lifeguard:{ label:'Lifeguard', sex:'f', frame:'slender', shape:0.75, age:'adult', height:1, weight:0, head:'oval', skin:'tan', hair:'blond', hairStyle:'ponytail', beard:'none', eyes:'sea', eyeShape:'round', garment:'swimsuit', bottom:'trousers', outfit:'rust', shirt:'white', hat:'bucket', hatCol:'white', apronCol:'canvas' },
    painter:{ label:'Painter', sex:'m', frame:'lean', shape:0.25, age:'adult', height:0, weight:-1, head:'heart', skin:'porcelain', hair:'auburn', hairStyle:'mop', beard:'goatee', eyes:'moss', eyeShape:'round', garment:'breton', bottom:'cropped', outfit:'char', shirt:'white', hat:'beret', hatCol:'black', apronCol:'navy' },
    coastguard:{ label:'Coastguard', sex:'f', frame:'hourglass', shape:0.75, age:'adult', height:0, weight:0, head:'oval', skin:'bronze', hair:'brown', hairStyle:'ponytail', beard:'none', eyes:'bark', eyeShape:'narrow', garment:'lifevest', bottom:'trousers', outfit:'navy', shirt:'navy', hat:'ballcap', hatCol:'navy', apronCol:'orange' },
    mechanic:{ label:'Mechanic', sex:'m', frame:'stocky', shape:0.25, age:'adult', height:-1, weight:1, head:'wide', skin:'umber', hair:'black', hairStyle:'buzz', beard:'sideburns', eyes:'bark', eyeShape:'narrow', garment:'coveralls', bottom:'trousers', outfit:'slate', shirt:'white', hat:'none', hatCol:'char', apronCol:'canvas' },
    baker:{ label:'Baker', sex:'f', frame:'full', shape:0.75, age:'adult', height:0, weight:1, head:'round', skin:'rose', hair:'sand', hairStyle:'bun', beard:'none', eyes:'sky', eyeShape:'round', garment:'apron', bottom:'skirt', outfit:'sky', shirt:'cream', hat:'kerchief', hatCol:'white', apronCol:'white' },
    tourist:{ label:'Tourist', sex:'f', frame:'average', shape:0.75, age:'adult', height:0, weight:0, head:'heart', skin:'fair', hair:'blond', hairStyle:'bob', beard:'none', eyes:'sky', eyeShape:'round', garment:'sundress', bottom:'trousers', outfit:'rose', shirt:'white', hat:'sunhat', hatCol:'straw', apronCol:'canvas' },
    paperboy:{ label:'Paper boy', sex:'m', frame:'average', shape:0.25, age:'child', height:0, weight:0, head:'oval', skin:'olive', hair:'black', hairStyle:'mop', beard:'none', eyes:'umber', eyeShape:'round', garment:'hoodie', bottom:'shorts', outfit:'denim', shirt:'navy', hat:'watchcap', hatCol:'rust', apronCol:'canvas' } };
  Object.assign(BUILDS, NPC20);
  const NPCS = Object.keys(NPC20), CAST = CAST10.concat(NPCS);
  const READY = CAST.slice();
  function normBuild(b){
    if(typeof b==='string') b=Object.assign({ preset:b }, BUILDS[b]||{}); b=b||{};
    const D0=BUILDS.fisher, o={ label:b.label||'Custom', preset:BUILDS[b.preset] ? b.preset : null }, pick=(k)=>OPTIONS[k].indexOf(b[k])>=0 ? b[k] : D0[k];
    /* 10.1: the sex as given, else read off the shape (past the middle with no beard reads f); the frame from the sex's list, else average */
    const sx = (b.sex==='f'||b.sex==='m') ? b.sex : (b.shape!=null && isFinite(+b.shape) && +b.shape>0.5 && (!b.beard || b.beard==='none')) ? 'f' : 'm';
    const s = (b.shape!=null && isFinite(+b.shape)) ? +b.shape : SEX[sx].shape;
    o.shape=Math.round(clamp01(s)*4)/4; o.age=pick('age'); o.height=clamp(Math.round(+b.height||0),-2,2); o.weight=clamp(Math.round(+b.weight||0),-2,2);
    for(const k of FIELDS.slice(4)) if(k!=='sex' && k!=='frame') o[k]=pick(k);
    o.sex=sx; o.frame=FRAMES[sx][b.frame] ? b.frame : 'average';
    if(b.name!=null) o.name=String(b.name).slice(0,16);
    return o; }
  /* 10.2: randomBuild(rnd, o) draws a build, sex first. A man never draws a skirt or a dress (GARMENTS and BOTTOMS fem); a woman may draw
     anything, men's swimwear with a top. Beards only on adult and elder men (a youth: none, stubble or a moustache), grey hair mostly on
     elders, bald only on men past youth, a hat three draws in five, swimwear one in twelve. o: { sex, age } holds either; rnd: Math.random. */
  function randomBuild(rnd, o){ rnd=typeof rnd==='function' ? rnd : Math.random; o=o||{}; const r=(a)=>a[Math.min(a.length-1, Math.floor(rnd()*a.length))];
    const sex = o.sex==='m'||o.sex==='f' ? o.sex : r(['m','f']), age = OPTIONS.age.indexOf(o.age)>=0 ? o.age : r(['adult','adult','adult','youth','elder','child']);
    const swim = rnd()<1/12, garment = r(OPTIONS.garment.filter(k=>(sex==='f' || !GARMENTS[k].fem) && !!GARMENTS[k].swim===swim));
    const grey={ salt:1, grey:1, white:1 }, hairs = age==='elder' ? (rnd()<0.75 ? Object.keys(grey) : OPTIONS.hair) : OPTIONS.hair.filter(h=>!grey[h] || (h==='salt' && age==='adult'));
    const styles = OPTIONS.hairStyle.filter(h=>h!=='bald' || (sex==='m' && (age==='adult'||age==='elder')));
    const beard = sex==='f' || age==='child' ? 'none' : age==='youth' ? r(['none','none','stubble','moustache']) : (rnd()<0.4 ? 'none' : r(OPTIONS.beard.slice(1)));
    return normBuild({ sex, frame:r(Object.keys(FRAMES[sex])), age, height:r(OPTIONS.height), weight:r(OPTIONS.weight), head:r(OPTIONS.head), skin:r(OPTIONS.skin), hair:r(hairs), hairStyle:r(styles),
      beard, eyes:r(OPTIONS.eyes), eyeShape:r(OPTIONS.eyeShape), garment, bottom:r(OPTIONS.bottom.filter(k=>sex==='f' || !BOTTOMS[k].fem)), outfit:r(OPTIONS.outfit), shirt:r(OPTIONS.shirt),
      hat: rnd()<0.4 ? 'none' : r(OPTIONS.hat.slice(1)), hatCol:r(OPTIONS.hatCol), apronCol:r(OPTIONS.apronCol) }); }
  const buildKey=(n)=>'b:'+JSON.stringify(FIELDS.reduce((o,k)=>(o[k]=n[k],o),{}));
  const sameBuild=(a,b)=>FIELDS.every(k=>a[k]===b[k]);
  const HSTEP = 1/(PX*Math.cos(ELEV*DEG));
  function zmOf(D,z){ const T=D.zm; if(z<=T[0][0]) return z;
    for(let i=0;i+1<T.length;i++) if(z<=T[i+1][0]){ const a=T[i], c=T[i+1]; return a[1]+(c[1]-a[1])*(z-a[0])/(c[0]-a[0]); }
    const a=T[T.length-2], c=T[T.length-1]; return c[1]+(z-c[0])*(c[1]-a[1])/(c[0]-a[0]); }
  /* 10.0: the Fisher's proportions. leg = thigh + shin; tor scales pass 9's torso landmarks (hip joint to neck) by tor/0.58 */
  const P10 = { leg:0.83, thigh:0.42, shin:0.41, ankle:0.075, tor:0.642, upper:0.30, fore:0.27, palm:0.065, shX:0.186, hipX:0.088, depth:1.08,
    head:0.65, headAge:{ child:0.97, youth:1, adult:1, elder:1 } };
  /* one screen row of a vertical face, in metres: the face rows are this far apart */
  const RS = 1/(PX*Math.cos(ELEV*DEG));
  /* pass 9's face rows (canonical head z) and the pixel row each one is in 10.0, counted down from the eye row */
  const ROW9 = [1.411,1.3718,1.3325,1.2917,1.2463,1.201,1.155], ROWP = [-2,-1,0,0,1,2,3];
  function pOf(zc){ const n=ROW9.length; if(zc>=ROW9[0]) return ROWP[0]-(zc-ROW9[0])/(ROW9[0]-ROW9[1]);
    for(let i=0;i+1<n;i++) if(zc>=ROW9[i+1]){ const t=(ROW9[i]-zc)/(ROW9[i]-ROW9[i+1]); return ROWP[i]+(ROWP[i+1]-ROWP[i])*t; }
    return ROWP[n-1]+(ROW9[n-1]-zc)/(ROW9[n-2]-ROW9[n-1]); }
  /* proportions for every build, derived from P10 as pass 9 derived them from pass 8's Fisher. All lengths are constant in every clip. */
  function dimsOf(b){
    /* 10.1: the shape slider is read against the sex's own norm; X is the sex and frame (frameOf) */
    const ag=AGES[b.age]||AGES.adult, s=b.shape, w=b.weight, h=b.height, s0=(SEX[b.sex]||SEX.m).shape, sf=(k)=>shapeF(k,s)/shapeF(k,s0), HS=HEADS[b.head]||HEADS.round, X=frameOf(b);
    const hK=ag.height*sf('height')*X.height; let leg=P10.leg*hK*ag.leg*X.leg, tor=P10.tor*hK*ag.torso*X.torso; leg+=0.55*h*HSTEP; tor+=0.45*h*HSTEP;
    const kl=leg/P10.leg, kt=tor/0.58, kt10=tor/P10.tor, footK=(0.72+0.28*kl)*X.foot, ankle=P10.ankle*footK, thigh=P10.thigh*kl, shin=P10.shin*kl, hipZ=ankle+thigh+shin;
    const T=(dz)=>hipZ+dz*kt, pelvisZ=T(0.04), spineZ=T(0.10), chestZ=T(0.29), shZ=T(0.465), neckZ=T(0.525), headZ=neckZ+0.055*kt*ag.neckLen;
    const ka=(kl+kt10)/2, upper=P10.upper*ka, fore=P10.fore*ka, palmLen=P10.palm*(0.7+0.3*ka)*X.hand;
    const base=0.78+0.22*Math.min(1,hK), g=1+0.075*w;
    /* 10.1: weight goes into the belly and the limbs more than the waist's width */
    const tw={ hip:sf('hip')*X.hip*g*base, waist:sf('waist')*X.waist*(1+0.07*w)*base, chest:sf('chest')*X.chest*g*base, sh:sf('shoulder')*X.sh*ag.shoulder*(1+0.035*w)*base };
    const td=P10.depth*X.depth*g*base, belly=Math.max(0,w)*0.016+X.belly, bust=X.bust*(1+0.15*Math.max(0,w)), seat=X.seat+Math.max(0,w)*0.004;
    const shX=P10.shX*sf('shoulder')*X.shX*ag.shoulder*base*(1+0.045*w), hipX=P10.hipX*sf('hipX')*X.hipX*(1+0.06*w)*base, footX=0.095*hipX/0.085;
    const rL=ag.limbR*sf('limbR')*(1+0.06*w), rA=rL*X.arm*(1+0.02*w), rG=rL*X.thigh*(1+0.035*w), rC=rL*X.calf*(1+0.02*w), rH=rL*X.hand, neckR=sf('neck')*X.neck*(1+0.05*w)*base, jaw=sf('jaw')*X.jaw*(1+0.03*w);
    const headK=ag.head*sf('head')*X.head, zs=headK*HS.z, eyeZ=headZ+0.15, hz=(zc)=>eyeZ+(zc-1.31)*zs, kH=P10.head*(P10.headAge[b.age]||1);
    const HR=HR0.map((r,i)=>{ const jw=i<=1 ? jaw : i===2 ? (1+jaw)/2 : 1, wf=1+0.035*w*(i<=2?1:0.4);
      return [r[0], r[1]*HS.w[i]*headK*jw*wf, r[2]*headK*(i===0?HS.chin:1), r[3]*headK, Math.min(r[4]*HS.c*headK, r[1]*HS.w[i]*headK*jw*wf*0.7)]; });
    const zm=[[0,0],[0.075,ankle],[0.315,ankle+shin],[0.58,hipZ],[0.62,pelvisZ],[0.68,spineZ],[0.87,chestZ],[1.045,shZ],[1.105,neckZ],[1.16,headZ]];
    const hipHW=0.160*tw.hip, crownOff=hz(1.522)-headZ;
    const D={ pelvisZ, spineZ, chestZ, neckZ, headZ, hipX, hipZ, thigh, shin, ankle, shX, shZ, upper, fore, palmLen, footX,
      palm:[0,0,-palmLen], sole:[0,0.05*footK,-ankle], crown:[0,0.010*kH,crownOff*kH], headMid:[0,HR[2][2]*kH,0.15*kH], rodLen:1.25, heightM:headZ+(crownOff+0.015)*kH, kH,
      kl, kt, ka, footK, tw, td, belly, bust, seat, rL, rA, rG, rC, rH, neckR, jaw, headK, zs, eyeZ, HR, zm, hipHW, stoop:ag.stoop, sex:b.sex, frame:b.frame, X,
      hangX:Math.max(shX+0.006, hipHW+0.046*rA), armDrop:1.065*(upper+fore) };
    D.waistZ=zmOf(D,0.84);
    return D; }
  /* 10.0: the frame the pose library is authored in is rig 9's Fisher, so its body targets scale to the longer limbs */
  const D9={ shX:0.20, upper:0.225, fore:0.205, thigh:0.265, shin:0.24, footX:0.095 }, canonD=()=>D9;
  function kOf(D){ const D0=canonD(); return { x:D.shX/D0.shX, a:(D.upper+D.fore)/(D0.upper+D0.fore), l:(D.thigh+D.shin)/(D0.thigh+D0.shin), f:D.footX/D0.footX, foot:D.footK }; }

  /* ============================ skeleton ============================
     Bind frames are all identity; `rest` is each bone's local position (child minus parent at bind). */
  const HUMANOID = { Hips:'pelvis', Spine:'spine', Chest:'chest', Neck:'neck', Head:'head',
    LeftUpperArm:'shoulder_L', LeftLowerArm:'elbow_L', LeftHand:'hand_L', RightUpperArm:'shoulder_R', RightLowerArm:'elbow_R', RightHand:'hand_R',
    LeftUpperLeg:'hip_L', LeftLowerLeg:'knee_L', LeftFoot:'foot_L', RightUpperLeg:'hip_R', RightLowerLeg:'knee_R', RightFoot:'foot_R' };
  function skeletonOf(D){
    const bones=[], ix={};
    const add=(id,parent,p,kind)=>{ ix[id]=bones.length; bones.push({ id, parent: parent==null?-1:ix[parent], p:p.slice(), kind }); };
    add('root',null,[0,0,0],'root');
    add('pelvis','root',[0,0,D.pelvisZ],'deform'); add('spine','pelvis',[0,0,D.spineZ],'deform'); add('chest','spine',[0,0,D.chestZ],'deform');
    add('neck','chest',[0,0,D.neckZ],'deform'); add('head','neck',[0,0,D.headZ],'deform');
    for(const s of ['L','R']){ const x=(s==='L'?-1:1)*D.shX;
      add('shoulder_'+s,'chest',[x,0,D.shZ],'deform'); add('elbow_'+s,'shoulder_'+s,[x,0,D.shZ-D.upper],'deform'); add('hand_'+s,'elbow_'+s,[x,0,D.shZ-D.upper-D.fore],'deform'); }
    for(const s of ['L','R']){ const x=(s==='L'?-1:1)*D.hipX;
      add('hip_'+s,'pelvis',[x,0,D.hipZ],'deform'); add('knee_'+s,'hip_'+s,[x,0,D.hipZ-D.thigh],'deform'); add('foot_'+s,'knee_'+s,[x,0,D.ankle],'deform'); }
    for(const s of ['L','R']){ const g=vadd(bones[ix['hand_'+s]].p, D.palm);
      add('tool_'+s,'hand_'+s,g,'socket'); add('tool_'+s+'_1','tool_'+s,vadd(g,[0,0.40*D.rodLen,0]),'socket'); add('tool_'+s+'_2','tool_'+s+'_1',vadd(g,[0,0.70*D.rodLen,0]),'socket'); }
    for(const s of ['L','R']) add('carry_'+s,'hand_'+s,vadd(bones[ix['hand_'+s]].p, D.palm),'socket');
    add('carry_mid','chest',vlerp(vadd(bones[ix.hand_L].p,D.palm), vadd(bones[ix.hand_R].p,D.palm), 0.5),'socket');
    add('back','chest',[0,-0.125,D.chestZ+0.12],'socket');
    const rest=bones.map(b=>b.parent<0 ? b.p.slice() : vsub(b.p, bones[b.parent].p));
    return { bones, ix, rest, deform:bones.filter(b=>b.kind==='deform').length };
  }
  const BACK_R = aimTo([0,0,0], vnorm([-0.35,-0.12,0.93]));
  const SOCKETS = {
    tool_R:  'right palm. Rod, spade, knife, gaff: +y runs along the tool (RodIso\'s rod-local frame), pitched then turned as the clip\'s tool track says',
    tool_R_1:'rod bend, chord 1 — 0.40 of the tier length', tool_R_2:'rod bend, chord 2 — 0.70 of the tier length',
    tool_L:  'left palm, same attitude as tool_R while a tool is held (the dig shaft runs left palm to right palm)',
    tool_L_1:'left chord 1', tool_L_2:'left chord 2',
    carry_L: 'left pail handle / oar loom / wheel rim — left palm, swung about local x by the pendulum', carry_R:'right pail handle / oar loom / wheel rim',
    carry_mid:'tray, pot or wheel centre — midpoint of the palms, chest-parented, swung about local x', back:'slung rod (back mount) — fixed on the chest' };

  /* ============================ the mesh ============================
     Faces are convex polygons, CCW seen from outside, in bind metres. Face groups (the face) are appended last, contiguous,
     in GROUP_ORDER. Every coordinate below is a function of the build's dims D. */
  const GROUP_ORDER = ['eyes.open','eyes.half','eyes.shut','eyes.wide','eyes.left','eyes.right','brows.flat','brows.up','brows.knit','mouth.flat','mouth.open','mouth.grit','mouth.smile'];
  const FACE_SLOTS = { eyes:['open','half','shut','wide','left','right'], brows:['flat','up','knit'], mouth:['flat','open','grit','smile'] };
  /* BLINK (9.2): an eyes-slot overlay the engine plays on top of any clip, on its own clock. From the clip's eyes state: half 40 ms,
     shut 80 ms, half 40 ms, then the clip's state again. Each character waits a random interval in interval_ms between blinks (and
     starts at a random point in its first), so a crowd never blinks together; one blink in doubleChance is followed by a second after
     doubleGap_ms. A frame whose own eyes are shut (sleep) is left alone. Exported as a four-frame face clip (40 ms frames). */
  const BLINK = { slot:'eyes', frames:['half','shut','shut','half'], ms:40, steps:[{ eyes:'half', ms:40 },{ eyes:'shut', ms:80 },{ eyes:'half', ms:40 }],
    interval_ms:[2400,6000], firstBlink:'uniform in [0, a first interval drawn from interval_ms]', doubleChance:0.15, doubleGap_ms:120, skipIf:{ eyes:['shut'] },
    then:'the eyes state of the clip frame that is showing' };
  /* LOOK AT (9.2): a head turn the engine adds on top of any clip, and the gaze states eyes.left / eyes.right. yaw > 0 turns the face
     toward the figure's +x (its right), pitch > 0 tips it down (the rig's pitch sense: the top toward +y). The turn is split over the
     neck and the head and post-multiplied onto the clip's local rotations, before the deck rock (which pre-multiplies the head):
       neck.local = clip.neck.local * E(0.4 yaw, 0.4 pitch)     head.local = rock.head * clip.head.local * E(0.6 yaw, 0.6 pitch)
     with E(y, p) = eul({ yaw:y, pitch:p }) = Rz(-y) * Rx(-p). Limits: yaw -60..60, pitch -15 (up) .. 20 (down). */
  const LOOK = { bones:['neck','head'], split:{ neck:0.4, head:0.6 }, yaw:[-60,60], pitch:[-15,20], eyesBeyond_deg:8, headShare:0.7 };
  /* 10.3: THE AIM BAR. lookAt() is one pass, measured on the frame as the clip poses it, unturned. On the turned head two things move
     the aim off the target: the turn is split over two bones, each in its own frame (up to 1.6 deg; 2.3 on a stooped elder), and the
     head point the aim is taken from (headMid, 10.4 cm ahead of the head bone since 10.0; 2 cm in 9.2) swings with the head (up to
     2.5 deg for a target 2 m away). bar_deg holds every build, the elders included. */
  const AIM = { bar_deg:4, share:1, distance_m:2, bearings_deg:[-50,-30,-15,0,15,30,50], dz_m:[-0.35,0,0.25], clip:'idle', frame:0,
    targets:'from the head point (head bone * headMid) of idle f0, unturned: distance_m away at each bearing (from the figure\'s +y toward +x) and at each height dz; only the targets whose need is inside the limits',
    measure:'turn = lookAt(S, target, share), applied: the angle between the turned head bone\'s +y and the target seen from the turned head point',
    measured:'10.3 on the 30 builds: 2.82 to 2.98 deg, the seven elders 3.65 to 3.73 (9.2: 1.70, the skipper and Nan 2.40)' };
  function lookE(look, id){ const w=LOOK.split[id], y=clamp(+look.yaw||0, LOOK.yaw[0], LOOK.yaw[1]), p=clamp(+look.pitch||0, LOOK.pitch[0], LOOK.pitch[1]); return eul({ yaw:y*w, pitch:p*w }); }
  /* 10.1: the four roles of a point mark: its design camera (yaw, for the mark's eye g: +1 the right eye) and its screen column from
     headMid (c counts out from the nose column); it draws while the camera's azimuth is within az deg of the design camera's. FRONT both
     eyes from ahead, DIAG the near eye at its diagonal, FAR the far eye at the other diagonal (column 1 only), SIDE the eye in profile,
     one pixel further back. PT_UNDER: what a mark may draw over. FACE_EMPTY: groups that draw nothing on purpose. */
  const ROLES = { front:{ az:24, yaw:()=>180, dx:(g,c)=>-g*c }, diag:{ az:21, yaw:(g)=>180-45*g, dx:(g,c,s)=>-g*c-g*(s||0) },
    far:{ az:21, yaw:(g)=>180+45*g, dx:(g,c,s)=>-g*c+g*(s||0) }, side:{ az:24, yaw:(g)=>180-90*g, dx:(g,c,s)=>-g*(c+1-(s||0)) } };
  const PT_UNDER = { head:1, nose:1, beard:1, face:1 }, FACE_EMPTY = { 'brows.flat':'the fringe\'s edge is the flat brow' };
  function meshOf(b, D, SK){
    const ix=SK.ix, F=[], FG=[], GM=garmentOf(b), HAT=HATS[b.hat]||null;
    const R1=(id)=>[[ix[id],1]];
    const newell=(v)=>{ let x=0,y=0,z=0; for(let i=0;i<v.length;i++){ const a=v[i], c=v[(i+1)%v.length]; x+=(a[1]-c[1])*(a[2]+c[2]); y+=(a[2]-c[2])*(a[0]+c[0]); z+=(a[0]-c[0])*(a[1]+c[1]); } return [x,y,z]; };
    const cen=(v)=>{ const c=[0,0,0]; for(const p of v){ c[0]+=p[0]; c[1]+=p[1]; c[2]+=p[2]; } return vmul(c,1/v.length); };
    const same=(n,sk)=>Array.from({length:n},()=>sk);
    function put(v, sk, mat, part, o){ o=o||{}; let vv=v.map(p=>p.slice()), ss=sk.slice();
      if(o.inside){ const nn=newell(vv); if(vdot(nn, vsub(cen(vv), o.inside))<0){ vv.reverse(); ss.reverse(); } }
      /* 10.1: a body face within 35 deg of vertical carries a smooth shading normal: its own, turned level */
      let sn=null; if(!o.group && SMOOTH.test(part)){ const n=vnorm(newell(vv)), hh=Math.hypot(n[0],n[1]); if(Math.abs(n[2])<SMOOTH_Z && hh>1e-6) sn=[n[0]/hh,n[1]/hh,0]; }
      (o.group?FG:F).push({ v:vv, mat, b:o.b||0, db:o.db||0, part, group:o.group||null, minT:o.minT||0, side:o.side||0, pt:o.pt||0, az:o.az==null?null:o.az, sn, oh:o.oh||0, bone:ss.map(q=>q.map(p=>p.slice())) }); }
    function loft(part, rings, o){ o=o||{}; const n=rings[0].pts.length;
      for(let i=0;i+1<rings.length;i++){ const A=rings[i], Bq=rings[i+1];
        for(let k=0;k<n;k++){ const k2=(k+1)%n, mat=A.matOf?A.matOf(k):(A.mat||o.mat); if(!mat) continue;
          put([A.pts[k],A.pts[k2],Bq.pts[k2],Bq.pts[k]], [A.sk,A.sk,Bq.sk,Bq.sk], mat, part, { b:A.b||0 }); } }
      const T=rings[rings.length-1], B0=rings[0];
      if(o.top) put(T.pts, same(n,T.sk), o.top, part, { b:o.topB||0 });
      if(o.bot) put(B0.pts.slice().reverse(), same(n,B0.sk), o.bot, part, { b:o.botB||0 }); }
    const oct=(cx,cy,z,w,f,k,c)=>[[cx+w,cy+f-c,z],[cx+w-c,cy+f,z],[cx-w+c,cy+f,z],[cx-w,cy+f-c,z],[cx-w,cy-k+c,z],[cx-w+c,cy-k,z],[cx+w-c,cy-k,z],[cx+w,cy-k+c,z]];
    const hex=(cx,cy,z,r)=>{ const o=[]; for(let i=0;i<6;i++){ const a=i*Math.PI/3; o.push([cx+Math.cos(a)*r, cy+Math.sin(a)*r, z]); } return o; };
    const rect=(cx,y0,y1,z,w)=>[[cx+w,y0,z],[cx+w,y1,z],[cx-w,y1,z],[cx-w,y0,z]];
    function extrudeX(part, prof, xa, xb, sk, mat){ const n=prof.length, c=[(xa+xb)/2, cen(prof.map(p=>[p[0],p[1],0]))[0], cen(prof.map(p=>[p[0],p[1],0]))[1]];
      for(let i=0;i<n;i++){ const a=prof[i], d=prof[(i+1)%n]; put([[xa,a[0],a[1]],[xb,a[0],a[1]],[xb,d[0],d[1]],[xa,d[0],d[1]]], same(4,sk), mat, part, { inside:c }); }
      put(prof.map(p=>[xa,p[0],p[1]]), same(n,sk), mat, part, { inside:c }); put(prof.map(p=>[xb,p[0],p[1]]), same(n,sk), mat, part, { inside:c }); }
    function octa(part, c, r, sk, mat, matD){ const P=[[c[0]+r[0],c[1],c[2]],[c[0],c[1]+r[1],c[2]],[c[0]-r[0],c[1],c[2]],[c[0],c[1]-r[1],c[2]]], T=[c[0],c[1],c[2]+r[2]], Bt=[c[0],c[1],c[2]-r[2]];
      for(let i=0;i<4;i++){ const a=P[i], d=P[(i+1)%4]; put([a,d,T], same(3,sk), mat, part, { inside:c }); put([d,a,Bt], same(3,sk), matD||mat, part, { inside:c }); } }
    const PL=R1('pelvis'), SP=R1('spine'), CH=R1('chest'), BL=[[ix.spine,0.5],[ix.chest,0.5]], HD=R1('head'), NK=R1('neck');
    const zm=(z)=>zmOf(D,z), TW=D.tw, TD=D.td;

    /* ---------------- torso: rings in canonical z, widths from the build ---------------- */
    const wF=(z)=>lin(z,[[0.515,TW.hip],[0.70,TW.hip],[0.76,TW.waist],[0.84,TW.waist],[0.93,TW.chest],[0.99,TW.chest],[1.035,TW.sh],[1.12,TW.sh]]);
    const fA=(z)=>lin(z,[[0.70,0],[0.76,D.belly],[0.84,D.belly*0.7],[0.90,D.bust*0.5],[0.95,D.bust],[1.0,D.bust*0.6],[1.05,0]]);
    const kA=(z)=>lin(z,[[0.52,0],[0.60,D.seat],[0.68,D.seat*0.6],[0.76,0]]);   /* 10.1: the seat */
    const tr=(z,w,f,k,c,e)=>{ e=e||0; const W=wF(z); return oct(0,0,zm(z), w*W+e, f*TD+fA(z)+e, k*TD+kA(z)+e, c*Math.min(W,TD)+e*0.4); };
    const TP=[[0.660,0.146,0.098,0.090,0.042],[0.700,0.147,0.099,0.091,0.043],[0.760,0.148,0.100,0.092,0.044],[0.840,0.156,0.106,0.096,0.046],
              [0.930,0.163,0.112,0.099,0.049],[0.990,0.165,0.113,0.100,0.050],[1.035,0.161,0.106,0.096,0.056],[1.080,0.128,0.090,0.084,0.050],[1.112,0.084,0.064,0.064,0.030]];
    const TSK=[SP,SP,SP,BL,CH,CH,CH,CH,CH];
    const fyC=(z)=>lin(z,TP.map(r=>[r[0],r[2]]))*TD+fA(z), kyC=(z)=>lin(z,TP.map(r=>[r[0],r[3]]))*TD;
    /* a jacket's skirt is the torso's lowest ring moved down to the hip and let out; the trousers under it are taken in so they stay inside */
    const FL=GM.jacket||GM.hem, PM=GM.seatMat||'over', JTP=FL ? [[0.560,0.172,0.114,0.108,0.052]].concat(TP.slice(1)) : TP;   /* 10.2: hem flares like a jacket, without the V */
    /* 10.1: the crotch ring is taken in (0.118 wide, 0.080 deep), so the thigh tops carry the hip line down instead of a pelvis block standing on them */
    if(!GM.skirt) loft('pelvis', FL ? [ { pts:tr(0.515,0.118,0.080,0.088,0.040), sk:PL, mat:PM }, { pts:tr(0.600,0.150,0.098,0.098,0.047), sk:PL, mat:PM }, { pts:tr(0.690,0.138,0.092,0.086,0.042), sk:PL } ]
      : [ { pts:tr(0.515,0.118,0.080,0.088,0.040), sk:PL, mat:'over' }, { pts:tr(0.600,0.160,0.096,0.104,0.050), sk:PL, mat:'over' },
                    { pts:tr(0.700,0.154,0.102,0.096,0.046), sk:PL } ], { bot:'overD', top:PM });
    /* the yoke slopes into the collar in two steps (1.035 -> 1.08 steep, 1.08 -> 1.112 into the neck) and sits a tone down, so the
       shoulders read as shoulders and not a bright shelf */
    /* 10.2: a swim top is the torso's own surface between two extra rings, so in idle it cannot slip under the skin (a band laid over the
       chest did, between the waist ring and the chest's). The cup follows the bust: bn is the build's bust against a woman's average
       (frame and weight both move it), and the top reaches lower and higher as it grows. The swimsuit's neckline rises with it. */
    const SWT=!!GM.swimTopOn, SUIT=!!GM.suit, bn=D.bust/0.030, cz0=clamp(0.918-0.030*(bn-1),0.870,0.930), cz1=clamp(0.990+0.018*(bn-1),0.972,1.012);
    const ins=(rows,z)=>{ if(rows.some(r=>Math.abs(r[0]-z)<0.004)) return rows; const i=rows.findIndex(r=>r[0]>z); return rows.slice(0,i).concat([[z].concat([1,2,3,4].map(j=>lin(z, rows.map(r=>[r[0],r[j]]))))], rows.slice(i)); };
    const TROWS = SWT ? ins(ins(JTP,cz0),cz1) : SUIT ? ins(JTP,cz1) : JTP, TSKr = TROWS.map(r=>{ const j=JTP.findIndex(q=>q[0]===r[0]); return j>=0 ? TSK[j] : CH; });
    const swimMat=(z,k)=> SWT ? (z>cz0-0.001 && z<cz1-0.001 ? 'over' : 'skin') : z<cz1-0.001 ? 'over' : z<1.079 ? ((k===0||k===2||k===4||k===6) ? 'over' : 'skin') : 'skin';
    loft('torso', TROWS.map((r,i)=>({ pts:tr(r[0],r[1],r[2],r[3],r[4]), sk:TSKr[i], matOf:(k)=> (SWT||SUIT) ? swimMat(r[0],k) : GM.band(i,k), b:(r[0]>=1.034?-1:0) })), { top:GM.band(7,1), topB:-1 });
    const onChest=(x0,x1,z0,z1,e)=>[[x0*wF(z0),fyC(z0)+e,zm(z0)],[x1*wF(z0),fyC(z0)+e,zm(z0)],[x1*wF(z1),fyC(z1)+e,zm(z1)],[x0*wF(z1),fyC(z1)+e,zm(z1)]];
    const skAt=(z)=>z<0.835 ? SP : z<0.845 ? BL : CH;
    const chestQ=(x0,x1,z0,z1,e,mat,part,db)=>put(onChest(x0,x1,z0,z1,e), [skAt(z0),skAt(z0),skAt(z1),skAt(z1)], mat, part, { inside:[0,0,zm((z0+z1)/2)], db:db||0.010 });
    /* ---------------- 10.2: the wardrobe's new pieces ---------------- */
    const tpAt=(z)=>[1,2,3,4].map(j=>lin(z,JTP.map(r=>[r[0],r[j]]))), ringT=(z,e)=>{ const p=tpAt(z); return tr(z,p[0],p[1],p[2],p[3],e); };
    const zInv=(Z)=>{ let a=0.40, c=1.20; for(let i=0;i<40;i++){ const m=(a+c)/2; if(zm(m)<Z) a=m; else c=m; } return (a+c)/2; };
    const band1=(z0,z1,e,mat,part)=>{ const sk=z1<=0.835 ? SP : CH; loft(part||'torso',[ { pts:ringT(z0,e), sk, mat }, { pts:ringT(z1,e), sk } ], {}); };
    /* STRIPES (the Breton): rings one row tall (RS, a vertical face's row height at 40 deg) every second row, so they draw as clean rows
       at every facing; above the belt and below the yoke, each on one bone (never across the waist ring). The sleeves' are in the arms. */
    if(GM.stripes) for(let Z=zm(0.715); Z+RS<=zm(1.030); Z+=2*RS){ const z0=zInv(Z), z1=zInv(Z+RS); if(z1>0.826 && z0<0.854) continue; band1(z0,z1,0.004,'apron'); }
    /* the life vest: a buoyant shell round the chest in the trim colour, a strap round it a tone down, the zip down the front */
    if(GM.pfd){ const P=(z,e)=>({ pts:ringT(z,e), sk:z<0.835?SP:CH, mat:'apron' });
      loft('vest',[ P(0.775,0.016), P(0.860,0.026), P(0.935,0.028), P(0.995,0.026), P(1.040,0.020), P(1.074,0.012) ], { bot:'apronD', top:'apronD' });
      band1(zInv(zm(0.795)), zInv(zm(0.795)+RS), 0.030, 'apronD', 'vest'); chestQ(-0.020,0.020,0.86,1.04,0.030,'apronD','vest',0.012); }
    /* the hoodie: the hood gathered behind the neck, the pocket across the belly; coveralls: two chest pockets */
    if(GM.hoodBack){ const nk=D.neckR; loft('collar',[ { pts:oct(0,-0.030,zm(1.062),0.104*nk,0.028,0.098*nk,0.034), sk:CH, mat:'shirtD' }, { pts:oct(0,-0.040,zm(1.128),0.100*nk,0.022,0.102*nk,0.034), sk:CH, mat:'shirtD' }, { pts:oct(0,-0.046,zm(1.172),0.074*nk,0.012,0.078*nk,0.026), sk:CH } ], { top:'shirtD' }); }
    if(GM.pocket) chestQ(-0.088,0.088,0.668,0.760,0.008,'shirtD','pocket',0.014);
    if(GM.pockets) for(const g of [-1,1]) chestQ(g>0?0.032:-0.104, g>0?0.104:-0.032, 0.905, 0.968, 0.006, 'overD', 'pocket', 0.012);
    /* the cardigan's open front: the shirt in a strip from the V down to the hem */
    if(GM.placket) chestQ(-0.022,0.022,0.664,0.952,0.006,'shirt','vest',0.012);
    /* double-breasted: two columns of three buttons */
    if(GM.buttons) for(const z of [0.79,0.87,0.95]) for(const g of [-1,1]) chestQ(g>0?0.040:-0.062, g>0?0.062:-0.040, z-0.018, z+0.018, 0.009, GM.buttons, 'jacket', 0.016);
    /* the greatcoat's skirts, from the jacket's hip to above the knee, over the trousers (cloth, rigid on the pelvis like a skirt) */
    if(GM.coat){ const hk=Math.min(TW.hip,1.10), dk=Math.min(TD,1.08), r=(z,w,f,k,c)=>oct(0,0,zm(z),w*hk,f*dk,k*dk,c*dk);
      loft('skirt',[ { pts:r(GM.coat,0.205,0.156,0.146,0.064), sk:PL, mat:'overD' }, { pts:r(GM.coat+0.03,0.202,0.152,0.142,0.062), sk:PL, mat:'over' }, { pts:r(0.600,0.178,0.120,0.114,0.054), sk:PL } ], {}); }
    /* a bikini top (the bikini; trunks or briefs on a woman): a band round the bust and two straps up to the neck */
    if(GM.swimTopOn) for(const g of [-1,1]) chestQ(g>0?0.036:-0.066, g>0?0.066:-0.036, cz1-0.004, 1.062, 0.005, 'over', 'strap', 0.010);
    if(GM.bib){ const bt=GM.bib;
      /* the bib is narrower than the chest's front plane, so a column of shirt shows each side of it */
      put(onChest(-0.092,0.092,0.830,0.930,0.006), [BL,BL,CH,CH], 'over', 'bib', { inside:[0,0,zm(0.88)], db:0.010 });
      chestQ(-0.092,0.092,0.930,bt,0.006,'over','bib');
      chestQ(-0.092,0.092,bt-0.040,bt,0.009,'overD','bib',0.014);
      chestQ(-0.034,0.034,0.880,0.922,0.009,'overD','bib',0.014);
      if(GM.brass) for(const g of [-1,1]) chestQ(g>0?0.056:-0.094, g>0?0.094:-0.056, bt-0.037, bt+0.005, 0.012, 'brass', 'bib', 0.018);
      for(const g of [-1,1]){ const path=[[0.075,bt,fyC(bt)+0.007],[0.073,1.060,fyC(1.060)+0.007],[0.066,1.113,0.058*TD],[0.066,1.113,-0.058*TD],[0.070,1.060,-kyC(1.06)-0.005],[0.070,0.930,-kyC(0.93)-0.007]];
        for(let i=0;i+1<path.length;i++){ const a=path[i], c=path[i+1], w=0.017, xa=a[0]*wF(a[1]), xc=c[0]*wF(c[1]);
          put([[g*(xa-w),a[2],zm(a[1])],[g*(xa+w),a[2],zm(a[1])],[g*(xc+w),c[2],zm(c[1])],[g*(xc-w),c[2],zm(c[1])]], same(4,CH), 'over', 'strap', { inside:[0,0,zm((a[1]+c[1])/2)-0.06], db:0.012 }); } } }
    if(GM.belt) chestQ(-0.022,0.022,0.664,0.696,0.004,'brass','belt',0.012);
    if(GM.vest) for(const z of [0.80,0.90]) chestQ(-0.10,0.10,z-0.006,z+0.006,0.004,'overD','vest',0.008);
    /* the jacket's V: a quad down the yoke and a point on the chest, so it follows the chest's front; lapels under the shirt, the tie over it */
    if(GM.jacket || GM.vest || GM.placket){ const V=(xt,xm,zp,mat,db,part)=>{ const e=0.006, zt=1.078, zq=0.990, P=(x,z)=>[x*wF(z),fyC(z)+e,zm(z)];
        put([P(-xt,zt),P(xt,zt),P(xm,zq),P(-xm,zq)], same(4,CH), mat, part, { inside:[0,0,zm(0.96)], db });
        put([P(-xm,zq),P(xm,zq),P(0,zp)], same(3,CH), mat, part, { inside:[0,0,zm(0.96)], db }); };
      if(GM.vest) V(0.070,0.036,0.905,'shirt',0.014,'vest'); else if(GM.placket) V(0.064,0.028,0.950,'shirt',0.014,'vest'); else { if(GM.lapel) V(0.078,0.064,0.842,GM.lapel,0.010,'lapel');
      V(0.076, GM.lapel?0.040:0.046, GM.lapel?0.884:0.896, 'shirt', 0.014, 'jacket');
      if(GM.tie==='long'){ chestQ(-0.020,0.020,0.990,1.072,0.007,'apron','tie',0.020); chestQ(-0.020,0.020,0.878,0.990,0.007,'apron','tie',0.020); }
      if(GM.tie==='bow') chestQ(-0.046,0.046,1.030,1.070,0.007,'apron','tie',0.020); } }
    if(GM.apron){ const e=0.013;
      chestQ(-0.100,0.100,0.840,0.985,e,'apron','apron',0.016); chestQ(-0.100,0.100,0.700,0.840,e,'apron','apron',0.016);
      const yP=Math.max(fyC(0.70), 0.104*TD)+e, W=wF(0.70);
      put([[-0.108*W,yP+0.045,zm(0.36)],[0.108*W,yP+0.045,zm(0.36)],[0.104*W,yP,zm(0.70)],[-0.104*W,yP,zm(0.70)]], same(4,PL), 'apron', 'apron', { inside:[0,0,zm(0.5)], db:0.02 });
      put([[-0.108*W,yP+0.047,zm(0.36)],[0.108*W,yP+0.047,zm(0.36)],[0.108*W,yP+0.043,zm(0.40)],[-0.108*W,yP+0.043,zm(0.40)]], same(4,PL), 'apronD', 'apron', { inside:[0,0,zm(0.5)], db:0.024 });
      for(const g of [-1,1]){ const a=[g*0.085*wF(0.985),fyC(0.985)+e,zm(0.985)], c=[g*0.058*D.neckR,0.050*TD,zm(1.105)];
        put([[a[0]-0.012,a[1],a[2]],[a[0]+0.012,a[1],a[2]],[c[0]+0.012,c[1],c[2]],[c[0]-0.012,c[1],c[2]]], same(4,CH), 'apronD', 'apron', { inside:[0,0,zm(1.0)], db:0.016 }); } }
    if(GM.skirt){ const hk=Math.min(TW.hip,1.10), dk=Math.min(TD,1.08), r=(z,w,f,k,c)=>oct(0,0,zm(z),w*(z>0.6?TW.hip:hk),f*(z>0.6?TD:dk),k*(z>0.6?TD:dk),c*dk);
      loft('skirt', GM.skirt==='long' ? [ { pts:r(0.205,0.222,0.178,0.140,0.074), sk:PL, mat:'overD' }, { pts:r(0.235,0.219,0.175,0.138,0.072), sk:PL, mat:'over' },
                     { pts:r(0.47,0.192,0.146,0.128,0.062), sk:PL, mat:'over' }, { pts:r(0.70,0.158,0.106,0.100,0.047), sk:PL } ]
        : [ { pts:r(0.30,0.215,0.175,0.165,0.070), sk:PL, mat:'overD' }, { pts:r(0.335,0.211,0.170,0.160,0.068), sk:PL, mat:'over' },
                     { pts:r(0.55,0.188,0.140,0.135,0.060), sk:PL, mat:'over' }, { pts:r(0.70,0.158,0.106,0.100,0.047), sk:PL } ], {}); }
    if(GM.shawl){ loft('shawl',[ { pts:tr(0.965,0.165,0.113,0.100,0.050,0.016), sk:CH, mat:'overL' }, { pts:tr(1.095,0.110,0.080,0.076,0.040,0.012), sk:CH } ], {});
      put([[-0.075*wF(0.965),fyC(0.965)+0.018,zm(0.965)],[0.075*wF(0.965),fyC(0.965)+0.018,zm(0.965)],[0,fyC(0.895)+0.018,zm(0.895)]], same(3,CH), 'overL', 'shawl', { inside:[0,0,zm(0.93)], db:0.014 }); }
    /* collar and neck */
    const nw=D.neckR, cm=GM.collarMat||'collar';
    if(GM.collar==='roll') loft('collar',[ { pts:oct(0,0.002,zm(1.074),0.090*nw,0.084*nw,0.078*nw,0.030), sk:CH, mat:'shirtD' }, { pts:oct(0,0.002,zm(1.104),0.094*nw,0.087*nw,0.080*nw,0.032), sk:CH, mat:'shirt' },
                                          { pts:oct(0,0.002,zm(1.136),0.074*nw,0.068*nw,0.064*nw,0.024), sk:CH } ], {});
    else if(GM.collar==='none'){}
    else if(GM.collar==='scarf'){ loft('collar',[ { pts:oct(0,0.004,zm(1.064),0.100*nw,0.094*nw,0.088*nw,0.034), sk:CH, mat:'apronD' }, { pts:oct(0,0.004,zm(1.092),0.104*nw,0.098*nw,0.092*nw,0.036), sk:CH, mat:'apron' }, { pts:oct(0,0.004,zm(1.152),0.082*nw,0.078*nw,0.074*nw,0.030), sk:CH } ], { top:'apron' });
      for(const g of [-1,1]) chestQ(g>0?0.018:-0.058, g>0?0.058:-0.018, 0.900, 1.062, 0.016, 'apron', 'collar', 0.016); }   /* 10.2: the greatcoat's scarf, both ends down the front */
    else if(GM.collar==='stand') loft('collar',[ { pts:oct(0,0.002,zm(1.084),0.078*nw,0.072*nw,0.066*nw,0.026), sk:CH, mat:cm }, { pts:oct(0,0.002,zm(1.150),0.072*nw,0.066*nw,0.062*nw,0.024), sk:CH } ], {});
    else loft('collar',[ { pts:oct(0,0.002,zm(1.088),0.074*nw,0.068*nw,0.062*nw,0.024), sk:CH, mat:cm }, { pts:oct(0,0.002,zm(1.126),0.070*nw,0.064*nw,0.060*nw,0.022), sk:CH } ], {});
    if(GM.collar==='open') put([[-0.030*nw,fyC(1.070)+0.004,zm(1.070)],[0.030*nw,fyC(1.070)+0.004,zm(1.070)],[0,fyC(1.02)+0.004,zm(1.02)]], same(3,CH), 'skin', 'collar', { inside:[0,0,zm(1.0)], db:0.012 });
    loft('neck',[ { pts:hex(0,0.004,zm(1.090),0.052*nw), sk:NK, mat:'skin' }, { pts:hex(0,0.008,zm(1.19),0.050*nw), sk:NK } ], {});

    /* ---------------- arms: canonical z along each bone, radii by rL ---------------- */
    const elZ=D.shZ-D.upper, hZ=elZ-D.fore, rA=D.rA, rH=D.rH, SL=GM.sleeveMat||'shirt', CF=GM.cuffMat||'shirtD', HM=GM.hemMat||'shirtD';
    const aZ=(z)=> z>=1.045 ? D.shZ+(z-1.045)*rA : z>=0.82 ? elZ+(z-0.82)*(D.upper/0.225) : z>=0.615 ? hZ+(z-0.615)*(D.fore/0.205) : hZ+(z-0.615)*(D.palmLen/0.045);
    for(const s of ['L','R']){ const g=s==='L'?-1:1, x=g*D.shX, U=R1('shoulder_'+s), E=R1('elbow_'+s), Hh=R1('hand_'+s);
      /* 10.1: the arm tapers from a deltoid (0.060 at the joint) to the elbow and the wrist, and its cap is drawn in toward the neck,
         so the shoulder rounds off the yoke over the deltoid into the sleeve */
      const DL=b.age==='child' ? 0.3 : 1, dR=lerp(0.051,0.060,DL), H=(z,r,y)=>hex(x,(y||0)*rA,aZ(z),r*rA), cap={ pts:hex(x-g*0.010*DL*rA,0,aZ(lerp(1.054,1.058,DL)),lerp(0.028,0.032,DL)*rA), sk:U };   /* a child's deltoid is smaller (DL) */
      if(GM.sleeve==='short'){
        loft('upper_'+s,[ { pts:H(0.905,0.055), sk:U, mat:HM }, { pts:H(0.925,0.057), sk:U, mat:SL }, { pts:H(1.040,dR), sk:U, mat:SL, b:-1 }, cap ], { top:SL, topB:-1, bot:HM });
        loft('upper_'+s,[ { pts:H(0.812,0.045), sk:U, mat:'skin' }, { pts:H(0.915,0.051), sk:U } ], { bot:'skin' });
        loft('fore_'+s,[ { pts:H(0.630,0.036,0.002), sk:E, mat:'skin' }, { pts:H(0.740,0.043), sk:E, mat:'skin' }, { pts:H(0.835,0.045), sk:E } ], { top:'skin', bot:'skin' }); }
      else if(GM.sleeve==='rolled'){
        loft('upper_'+s,[ { pts:H(0.832,0.058), sk:U, mat:'collar' }, { pts:H(0.860,0.058), sk:U, mat:SL }, { pts:H(0.950,0.057), sk:U, mat:SL }, { pts:H(1.040,dR), sk:U, mat:SL, b:-1 }, cap ], { top:SL, topB:-1, bot:'shirtD' });
        loft('fore_'+s,[ { pts:H(0.630,0.036,0.002), sk:E, mat:'skin' }, { pts:H(0.740,0.043), sk:E, mat:'skin' }, { pts:H(0.845,0.045), sk:E } ], { bot:'skin' }); }
      else if(GM.sleeve==='none'){
        loft('upper_'+s,[ { pts:H(0.812,0.044), sk:U, mat:'skin' }, { pts:H(0.940,0.050), sk:U, mat:'skin' }, { pts:H(1.040,dR-0.006), sk:U, mat:'skin', b:-1 }, cap ], { top:'skin', topB:-1 });
        loft('fore_'+s,[ { pts:H(0.630,0.036,0.002), sk:E, mat:'skin' }, { pts:H(0.740,0.043), sk:E, mat:'skin' }, { pts:H(0.835,0.045), sk:E } ], { top:'skin', bot:'skin' }); }
      else {
        loft('upper_'+s,[ { pts:H(0.815,0.048), sk:U, mat:SL }, { pts:H(0.940,0.055), sk:U, mat:SL }, { pts:H(1.040,dR), sk:U, mat:SL, b:-1 }, cap ], { top:SL, topB:-1 });
        loft('fore_'+s,[ { pts:H(0.640,0.040,0.002), sk:E, mat:CF }, { pts:H(0.668,0.042,0.002), sk:E, mat:SL }, { pts:H(0.750,0.046), sk:E, mat:SL }, { pts:H(0.840,0.049), sk:E } ], { bot:CF });
        /* 10.2: the Breton's sleeves: the torso's stripe rhythm down each bone */
        if(GM.stripes) for(const [c0,c1,sk,P,part] of [[0.842,1.012,U,[[0.815,0.048],[0.940,0.055],[1.040,dR]],'upper_'],[0.676,0.806,E,[[0.640,0.040],[0.668,0.042],[0.750,0.046],[0.840,0.049]],'fore_']]){
          const w0=aZ(c0), w1=aZ(c1), cz=(Z)=>c0+(Z-w0)*(c1-c0)/(w1-w0);
          for(let Z=w1; Z-RS>=w0; Z-=2*RS){ const za=cz(Z-RS), zb=cz(Z); loft(part+s,[ { pts:H(za,lin(za,P)+0.004/rA), sk, mat:'apron' }, { pts:H(zb,lin(zb,P)+0.004/rA), sk } ], {}); } } }
      loft('hand_'+s,[ { pts:rect(x,-0.020*rH,0.032*rH,aZ(0.535),0.030*rH), sk:Hh, mat:'skin' }, { pts:rect(x,-0.028*rH,0.040*rH,aZ(0.565),0.036*rH), sk:Hh, mat:'skin' },
                       { pts:rect(x,-0.026*rH,0.036*rH,aZ(0.625),0.034*rH), sk:Hh } ], { bot:'skin', top:'skin' });
    }
    /* ---------------- legs ---------------- */
    const kneeZ=D.ankle+D.shin, rG=D.rG, rC=D.rC, LG=GM.legMat||'over', LGD=LG==='over'?'overD':LG, fk=D.footK;
    /* 10.1: the thigh fills the hip at its top (its outer edge meets the pelvis ring), tapers to the knee and is a little deeper than
       wide; the shin carries a calf, and the boot is let out round it */
    const hexE=(cx,cy,z,rx,ry)=>{ const o=[]; for(let i=0;i<6;i++){ const a=i*Math.PI/3; o.push([cx+Math.cos(a)*rx, cy+Math.sin(a)*ry, z]); } return o; };
    const thTop=Math.max(0.072*rG, 0.160*TW.hip-D.hipX+0.004);
    const lZ=(z)=> z>=0.315 ? kneeZ+(z-0.315)*(D.thigh/0.265) : D.ankle+(z-0.075)*(D.shin/0.24);
    const bt=GM.boot, shinLo=Math.min(0.19, bt-0.02);
    for(const s of ['L','R']){ const g=s==='L'?-1:1, x=g*D.hipX, T=R1('hip_'+s), Kn=R1('knee_'+s), Ft=R1('foot_'+s);
      const TM=GM.skirt?'over':LG, SM=GM.shorts?'skin':LG;
      loft('thigh_'+s,[ { pts:hexE(x,0.002,lZ(0.300),0.057*rG,0.060*rG), sk:T, mat:TM }, { pts:hexE(x,0.004,lZ(0.460),0.066*rG,0.071*rG), sk:T, mat:TM }, { pts:hexE(x,0.006,lZ(0.620),thTop*(TM==='skin'?0.94:1),thTop*1.08*(TM==='skin'?0.94:1)), sk:T } ], { bot:GM.skirt?'overD':LGD });   /* 10.2: a bare thigh's top sits inside the briefs, so the hip has one surface in front */
      /* 10.2: cropped trousers stop mid-calf over a rolled cuff; bare feet (swimwear) are skin to the sole; waders and wetsuits bring their own boot colour */
      if(GM.cropped) loft('shin_'+s,[ { pts:hex(x,0.002,lZ(shinLo),0.048*rC), sk:Kn, mat:'skin' }, { pts:hex(x,-0.002,lZ(0.196),0.055*rC), sk:Kn, mat:LGD }, { pts:hex(x,-0.003,lZ(0.222),0.061*rC), sk:Kn, mat:SM }, { pts:hex(x,0.002,lZ(0.332),0.052*rG), sk:Kn } ], { top:SM });
      else loft('shin_'+s,[ { pts:hex(x,0.002,lZ(shinLo),0.048*rC), sk:Kn, mat:SM }, { pts:hex(x,-0.004,lZ(0.235),0.058*rC), sk:Kn, mat:SM }, { pts:hex(x,0.002,lZ(0.332),0.052*rG), sk:Kn } ], { top:SM });
      const bare=!!GM.bare, BMa=bare?'skin':(GM.bootMat||'boot'), BLa=bare?'skin':(GM.bootMat||'bootL'), br=bare ? 0.86*rC : Math.max(0.9+0.1*rC, rC), zb0=bare?0.060:0.080, zb1=Math.max(zb0+0.008, bt-0.028);
      loft('boot_'+s,[ { pts:hex(x,0.004,lZ(zb0),0.058*br), sk:Kn, mat:BMa }, { pts:hex(x,0.004,lZ(zb1),0.061*br), sk:Kn, mat:BLa }, { pts:hex(x,0.004,lZ(bt),0.062*br), sk:Kn } ], { top:BLa });
      const fw=0.052*br;
      extrudeX('foot_'+s, [[-0.080,0.000],[0.175,0.000],[0.175,0.016],[-0.080,0.016]].map(p=>[p[0]*fk,p[1]*fk]), x-fw, x+fw, Ft, bare?'skinD':'sole');
      extrudeX('foot_'+s, [[-0.076,0.016],[0.168,0.016],[0.168,0.044],[0.112,0.086],[-0.076,0.096]].map(p=>[p[0]*fk,p[1]*fk]), x-fw+0.004, x+fw-0.004, Ft, BMa);
    }

    /* ---------------- the head: canonical head z (1.135 chin .. 1.522 cap) mapped about the eye line ---------------- */
    const HRb=D.HR, eyeZ=D.eyeZ, zs=D.zs, hz=(zc)=>eyeZ+(zc-1.31)*zs, zcOf=(zb)=>1.31+(zb-eyeZ)/zs, fz=(zc)=>D.eyeZ-pOf(zc)*RS/D.kH;
    const L_=(i)=>(zc)=>lin(zc, HRb.map(r=>[r[0], r[i]])), hwAt=L_(1), fAt=L_(2), kAt=L_(3), cAt=L_(4);
    const ringAt=(zc,e)=>oct(0,0,hz(zc), hwAt(zc)+e, fAt(zc)+e, kAt(zc)+e, cAt(zc)+e*0.4);
    const yFb=(zb)=>fAt(zcOf(zb)), hwb=(zb)=>hwAt(zcOf(zb)), fEdge=(zb)=>fAt(zcOf(zb))-cAt(zcOf(zb));
    const hc=[0,0,hz(1.30)], HSt=b.hairStyle, BD=b.beard;
    const topF = HAT ? HAT.F : 9;
    const fringeZ = Math.max(1.425, zcOf(fz(1.427)));               // never below the raised-brow row
    const EA = zcOf(fz(EAR.top)), HL = hairlineOf(HSt, fringeZ, EA);
    const BS=BEARDS[BD]||BEARDS.none, bmat=BS.mat||'beard';
    const skullMat=(i,k)=>{
      if(i===0) return BS.r0 && BS.r0.indexOf(k)>=0 ? bmat : 'skin';
      if(i===1) return BS.r1 && BS.r1.indexOf(k)>=0 ? bmat : 'skin';
      if(i===2){ if((HSt==='buzz'||HSt==='bald') && backSide(k)) return 'buzz'; return 'skin'; }
      return HSt==='buzz' ? 'buzz' : 'skin'; };
    /* the skull, cut along the hairline: a face that crosses a boundary is split there (and a side plane at the ear's front and back),
       and every piece inside the hair region takes the hair's material. The cuts lie in the faces' own planes, so the shape is unchanged. */
    const SKR=HRb.map(r=>oct(0,0,hz(r[0]),r[1],r[2],r[3],r[4]));
    const cutPoly=(P,ax,v)=>{ const lo=[], hi=[]; for(let i=0;i<P.length;i++){ const a=P[i], c=P[(i+1)%P.length], da=a[ax]-v, dc=c[ax]-v;
        if(da<=1e-12) lo.push(a); if(da>=-1e-12) hi.push(a);
        if((da<-1e-12 && dc>1e-12) || (da>1e-12 && dc<-1e-12)){ const t=da/(da-dc); const p=[a[0]+(c[0]-a[0])*t, a[1]+(c[1]-a[1])*t, a[2]+(c[2]-a[2])*t]; lo.push(p); hi.push(p); } }
      return [lo,hi].filter(Q=>Q.length>=3 && vlen(newell(Q))>1e-9); };
    const sideZones = HL && !HL.horseshoe ? [Math.min(HL.N,HL.E,HL.T), Math.max(HL.N,HL.E,HL.T)] : null;
    for(let i=0;i<5;i++){ const A=SKR[i], Bq=SKR[i+1], za=HRb[i][0], zb=HRb[i+1][0];
      for(let k=0;k<8;k++){ const k2=(k+1)%8, base=skullMat(i,k), sd=hairSide(k); let pieces=[[A[k],A[k2],Bq[k2],Bq[k]]];
        if(HL && !HL.horseshoe){
          if(sd==='side' && za<sideZones[1]-1e-6 && zb>sideZones[0]+1e-6) for(const yv of [EAR.front,EAR.back]){ const o=[]; for(const P of pieces) o.push(...cutPoly(P,1,yv)); pieces=o; }
          const o=[]; for(const P of pieces){ const zc=hairBound(HL, sd, cen(P)[1]); if(zc>za+1e-6 && zc<zb-1e-6) o.push(...cutPoly(P,2,hz(zc))); else o.push(P); } pieces=o; }
        for(const P of pieces){ const c=cen(P); put(P, same(P.length,HD), inHair(HL, sd, c[1], zcOf(c[2])) ? HL.mat : base, 'head', {}); } } }
    put(SKR[5], same(8,HD), inHair(HL,'top',0,1.522) ? HL.mat : 'skin', 'head', {});
    put(SKR[0].slice().reverse(), same(8,HD), BS.bot||'skinD', 'head', {});
    /* the nose: a four-plane wedge on the face rows; its tip stands 4 cm proud so the profile has one */
    const nz0=fz(1.226), nz1=fz(1.284), nwd=0.024*D.headK, nb=[[-nwd,nz1],[nwd,nz1],[nwd,nz0],[-nwd,nz0]].map(([x,z])=>[x,yFb(z),z]), tip=[0,yFb(fz(1.24))+0.040,fz(1.236)];
    for(let i=0;i<4;i++) put([nb[i],nb[(i+1)%4],tip], same(3,HD), i===2?'skinD':'skin', 'nose', { inside:hc });
    const earX=hwb(fz(1.295));
    for(const g of [-1,1]) extrudeX('ear', [[-0.032,fz(1.235)],[0.016,fz(1.235)],[0.016,fz(1.345)],[-0.032,fz(1.345)]], g>0?earX-0.008:-earX-0.018, g>0?earX+0.018:-earX+0.008, HD, 'skinD');

    /* ---------------- hair ---------------- */
    /* a shell band is cut at every skull ring it spans, so it stays e outside the skull all the way (9.1 ran one quad across the
       1.30 ring, and the skull showed through it at the back of the bob and the long) */
    const RZ=HRb.map(r=>r[0]), zcuts=(z0,z1)=>[z0].concat(RZ.filter(z=>z>z0+1e-6 && z<z1-1e-6), [z1]);
    const band=(zc0,zc1,e,sides,mat)=>{ if(zc0>=topF-0.004) return; if(zc1>topF+0.02) zc1=topF+0.02; const Z=zcuts(zc0,zc1);
      for(let j=0;j+1<Z.length;j++){ const A=ringAt(Z[j],e), Bq=ringAt(Z[j+1],e);
        for(const k of sides){ const k2=(k+1)%8; put([A[k],A[k2],Bq[k2],Bq[k]], same(4,HD), mat||'hair', 'hair', { inside:[0,0,hz((Z[j]+Z[j+1])/2)] }); } } };
    /* the side planes (3 at -x, 7 at +x) covered only behind yMax, so the front of the cheek stays skin in profile */
    const sidePart=(zc0,zc1,e,yMax,mat)=>{ if(zc0>=topF-0.004) return; if(zc1>topF+0.02) zc1=topF+0.02; const Z=zcuts(zc0,zc1);
      for(let j=0;j+1<Z.length;j++){ const A=ringAt(Z[j],e), Bq=ringAt(Z[j+1],e);
        for(const k of [3,7]){ const q=[A[k],A[(k+1)%8],Bq[(k+1)%8],Bq[k]].map(p=>[p[0],Math.min(p[1],yMax),p[2]]); if(Math.max(...q.map(p=>p[1]))-Math.min(...q.map(p=>p[1]))<0.01) continue;
          put(q, same(4,HD), mat||'hair', 'hair', { inside:[0,0,hz((Z[j]+Z[j+1])/2)] }); } } };
    /* over the ear (from its top, just behind its front edge) and behind it (down to the nape): the side of the head is hair */
    const overEar=(z1,e,mat)=>sidePart(EA-0.006, z1, e, EAR.front+0.004, mat), behindEar=(z0,z1,e,mat)=>sidePart(z0, z1, e, EAR.back, mat);
    const chamfTop=Math.max(1.425, fringeZ);                            // the corners meet the fringe on a small head too
    const hairCap=(e)=>{ if(HAT) return; const A=ringAt(1.500,e), Bq=oct(0,0,hz(1.522)+e*0.95, hwAt(1.522)+e*0.7, fAt(1.522)+e*0.7, kAt(1.522)+e*0.7, cAt(1.522)+0.006);
      for(let k=0;k<8;k++){ const k2=(k+1)%8; put([A[k],A[k2],Bq[k2],Bq[k]], same(4,HD), 'hair', 'hair', { inside:[0,0,hz(1.47)] }); } put(Bq, same(8,HD), 'hair', 'hair', { inside:[0,0,hz(1.40)] }); };
    const tet=(base, apex, mat)=>{ const c=cen(base.concat([apex])); for(let i=0;i<3;i++) put([base[i],base[(i+1)%3],apex], same(3,HD), mat||'hair', 'hair', { inside:c }); };
    const fanQ=(q,mat,part,o)=>{ const n=vnorm(vcross(vsub(q[1],q[0]),vsub(q[2],q[0]))), off=Math.abs(vdot(n,vsub(q[3],q[0])));
      if(off<0.0008) return put(q, same(4,HD), mat, part, o); const m=cen(q); for(let i=0;i<4;i++) put([q[i],q[(i+1)%4],m], same(3,HD), mat, part, o); };
    const yEye=(zb)=>fEdge(zb)-0.004/D.kH;                                // the front edge of the side plane, where the profile eye sits
    const yHair=yEye(fz(1.31))-0.050;
    const fringe=(e,tufts)=>{ if(fringeZ>=topF-0.004) return; band(fringeZ,1.50,e,[0,1,2]);
      if(tufts){ const zt=hz(fringeZ), yt=fAt(fringeZ)+e;
        for(const [xc,dz,w,m] of tufts) put([[xc-w,yt,zt+0.004],[xc+w,yt,zt+0.004],[xc,yt-0.004,zt-dz]], same(3,HD), m||'hair', 'hair', { inside:hc }); } };
    const hood = b.hat==='hood';
    /* 10.2: STYLE DETAIL. sheen: the crown's upper front a tone up (hairL), so the hair reads as a lit surface with a shape. slab: a box
       of hair (the inner face left out unless all). curtain: a slab beside each cheek from the temple to the jaw (bob, long; the long's
       carries on below the jaw to the shoulder), its front kept behind the profile eye, so from ahead the style frames the face and in
       profile the cheek and the eye stay clear. */
    const sheen=(e)=>{ if(!HAT) band(1.47,1.50,e+0.0015,[0,1,2],'hairL'); };
    const slab=(T,Bt,mat,all)=>{ const P=T.concat(Bt), c=cen(P), Q=[[1,2,6,5],[2,3,7,6],[4,5,6,7],[0,1,5,4],[0,3,2,1]]; if(all) Q.push([0,4,7,3]);
      for(const q of Q) put(q.map(i=>P[i]), same(4,HD), mat||'hair', 'hair', { inside:c }); };
    const curtain=(e,zc0,zc1,drop)=>{ if(zc0>=topF-0.004) return; zc0=Math.min(zc0,topF);
      const yf=(zc)=>Math.min(yEye(hz(zc))-0.044, fAt(zc)-cAt(zc)-0.012), yb=(zc)=>-kAt(zc)*0.30, xi=(zc)=>hwAt(zc)-0.014, xo=(zc,fl)=>hwAt(zc)+e+0.036+(fl||0);
      const ring=(zc,fl)=>{ const z=hz(zc), X=xo(zc,fl); return [[xi(zc),yb(zc),z],[X,yb(zc),z],[X,yf(zc),z],[xi(zc),yf(zc),z]]; };
      for(const g of [-1,1]){ const M=(R)=>R.map(p=>[g*p[0],p[1],p[2]]);
        slab(M(ring(zc0,0)), M(ring(zc1,0.012)));
        if(drop){ const zb=hz(zc1), X=xo(zc1,0.012), w=0.072, z2=zb-drop, yF=yf(zc1)-0.010, yB=yb(zc1)*0.4;
          slab(M([[X-w,yB,zb],[X,yB,zb],[X,yF,zb],[X-w,yF,zb]]), M([[X-w*0.9,yB+0.012,z2],[X-0.006,yB+0.012,z2],[X-0.006,yF-0.024,z2],[X-w*0.9,yF-0.024,z2]]), 'hair', true); } } };
    if(HSt==='crop'){ const e=0.010, fq=Math.max(fringeZ,1.452); if(fq<topF-0.004) band(fq,1.50,e,[0,1,2]); band(1.425,1.50,e,[3,4,5,6,7]); band(1.36,1.425,e,[4,5,6],'hairD'); band(1.39,Math.max(chamfTop,fq),e,[0,2],'hairD'); sidePart(1.36,1.425,e,yEye(fz(1.39))-0.03,'hairD');
      band(1.30,1.36,e,[4,5,6],'hairD'); overEar(1.36,e,'hairD'); behindEar(1.30,EA,e,'hairD'); behindEar(1.245,1.30,e,'hairD'); band(1.245,1.30,e,[4,5,6],'hairD'); hairCap(e); sheen(e); }   /* 10.2: short back and sides a tone down */
    else if(HSt==='mop'){ const e=0.022;
      if(!hood){ band(1.425,1.50,e,[3,4,5,6,7]); band(1.36,1.425,e,[4,5,6]); band(1.39,chamfTop,e,[0,2]); sidePart(1.36,1.425,e,yEye(fz(1.39))-0.035);
        band(1.28,1.36,e,[4,5,6]); overEar(1.36,e); behindEar(1.28,EA,e); behindEar(1.225,1.28,e,'hairD'); band(1.225,1.28,e,[4,5,6],'hairD'); hairCap(e);
        /* 10.2: three tufts across the crown (the middle one lit) and one at the back; the tufts over the ears stand out further */
        if(!HAT){ const zt=hz(1.522)+e*0.6, kb=kAt(1.51)+e;
          for(const [xc,yc,h,m] of [[-0.080,-0.012,0.070,'hair'],[0,0.010,0.084,'hairL'],[0.080,-0.012,0.070,'hair'],[0,-kb*0.62,0.060,'hairD']])
            tet([[xc-0.038,yc-0.032,zt],[xc+0.038,yc-0.032,zt],[xc,yc+0.036,zt]],[xc,yc-0.006,zt+h],m); }
        for(const g of [-1,1]){ const xw=g*(hwAt(1.30)+e); tet([[xw,-0.036,hz(1.300)],[xw,0.006,hz(1.300)],[xw,-0.024,hz(1.34)]],[g*(hwAt(1.28)+e+0.022),-0.004,hz(1.248)],'hairD'); } }
      fringe(e,[[-0.098,0.062,0.030,'hairD'],[0,0.052,0.026,'hair'],[0.098,0.062,0.030,'hairD']]); }
    else if(HSt==='bob'||HSt==='long'){ const e=0.020;
      if(!hood){ band(1.425,1.50,e,[3,4,5,6,7]); band(1.39,chamfTop,e,[0,2]); band(1.20,1.425,e,[4,5,6]); sidePart(1.20,1.425,e,yHair); hairCap(e);
        curtain(e, 1.425, 1.215, HSt==='long' ? 0.15 : 0);
        if(HSt==='long'){ const A=ringAt(1.20,e), zb=hz(1.20);   /* 10.2: down to the shoulder blades */
          for(const k of [3,4,5,6,7]){ const k2=(k+1)%8, P=[A[k],A[k2]].map(p=>[p[0],Math.min(p[1],yHair-0.02),p[2]]), Q=P.map(p=>[p[0]*1.08,p[1]-0.024,zb-0.21]);
            fanQ([P[0],P[1],Q[1],Q[0]], k===5?'hair':'hairD', 'hair', { inside:[0,0.04,zb-0.09] }); } }
        sheen(e); }
      fringe(e,[[0,0.010,0.050]]); if(!hood) band(fringeZ,fringeZ+0.024,e+0.0015,[0,1,2],'hairD'); }   /* 10.2: the fringe cut straight, its edge a tone down */
    else if(HSt==='bun'||HSt==='ponytail'){ const e=0.011;
      if(!hood){ fringe(e); band(1.425,1.50,e,[3,4,5,6,7]); band(1.36,1.425,e,[4,5,6]); band(1.39,chamfTop,e,[0,2]); sidePart(1.33,1.425,e,yEye(fz(1.38))-0.03); behindEar(1.26,1.33,e); band(1.26,1.36,e,[4,5,6]); hairCap(e); sheen(e);
        if(HSt==='bun'){ if(HAT) octa('hat',[0,-(kAt(1.47)+0.035),hz(1.472)],[0.050*D.headK,0.040,0.046*D.headK],HD,'hat','hatD');   /* 10.0: under a hat the bun is the hat's bump (9.2's showed between the kerchief's edges and read as a face at N) */
          else octa('hair',[0,-kAt(1.50)*0.42,hz(1.522)+0.040],[0.058*D.headK,0.054,0.050*D.headK],HD,'hair','hairD'); }   /* 10.2: high on the crown, so it shows over the head from ahead */
        else { /* 10.2: the tail starts inside the shell under a tie and hangs to the shoulders, wide enough to keep a pixel at the head's 0.65 (10.1's dropped to one floating pixel in profile) */
          const y0=-(kAt(1.42)+e), z0=hz(1.43), R=(z,w,yf,yb)=>[[-w,yb,z],[w,yb,z],[w,yf,z],[-w,yf,z]];
          slab(R(z0+0.014,0.048,y0+0.012,y0-0.046), R(z0-0.030,0.048,y0+0.012,y0-0.046), 'hairD', true);
          slab(R(z0-0.030,0.040,y0+0.004,y0-0.040), R(z0-0.250,0.026,y0-0.048,y0-0.086), 'hair', true);
          tet([[-0.026,y0-0.048,z0-0.250],[0.026,y0-0.048,z0-0.250],[0,y0-0.086,z0-0.250]],[0,y0-0.074,z0-0.310],'hairD'); } }
      else fringe(e); }
    else if(HSt==='buzz'){ /* painted on the skull above: the hair region, over and behind the ears included (10.2: the buzz material is the hair let toward the skin) */ }
    else if(HSt==='bald' && !HAT){ /* 10.2: a shine on the crown's front */ const A=SKR[4], Bq=SKR[5], q=[A[1],A[2],Bq[2],Bq[1]], m=cen(q); let n=vnorm(newell(q)); if(vdot(n,vsub(m,hc))<0) n=vmul(n,-1);
      put(q.map(p=>vadd(vlerp(m,p,0.45), vmul(n,0.004))), same(4,HD), 'skinL', 'head', { inside:hc }); }
    /* ---------------- hats ---------------- */
    if(HAT){ const e=HAT.e, hm='hat';
      const ringV=(zl,ee)=>{ const R=zl.map(zc=>ringAt(zc,ee)); return R.map((r,i)=>r[i]); };
      const lowZ=[(HAT.F+HAT.S)/2,HAT.F,HAT.F,(HAT.F+HAT.S)/2,(HAT.S+HAT.B)/2,HAT.B,HAT.B,(HAT.S+HAT.B)/2];
      /* a quad whose ring is tilted is not planar; it is fanned from its centre so its mirror image rasterises the same */
      const quad=(q,mat)=>{ const n=vnorm(vcross(vsub(q[1],q[0]),vsub(q[2],q[0]))), off=Math.abs(vdot(n,vsub(q[3],q[0])));
        if(off<0.0008) return put(q, same(4,HD), mat, 'hat', { inside:[0,0,hz(1.40)], db:0.004 }); const m=cen(q); for(let i=0;i<4;i++) put([q[i],q[(i+1)%4],m], same(3,HD), mat, 'hat', { inside:[0,0,hz(1.40)], db:0.004 }); };
      const shell=(zlA,zlB,ee,mat,sides)=>{ const A=ringV(zlA,ee), Bq=ringV(zlB,ee); for(const k of (sides||[0,1,2,3,4,5,6,7])){ const k2=(k+1)%8; quad([A[k],A[k2],Bq[k2],Bq[k]], mat); } return Bq; };
      const top=(zc,dz,ee,mat,fw)=>{ const P=oct(0,(fw||0)/2,hz(zc)+dz, hwAt(zc)+ee, fAt(zc)+ee+(fw||0)/2, kAt(zc)+ee-(fw||0)/2, cAt(zc)+ee*0.4); return P; };
      const cap=(A,P,mat)=>{ for(let k=0;k<8;k++){ const k2=(k+1)%8; put([A[k],A[k2],P[k2],P[k]], same(4,HD), mat, 'hat', { inside:[0,0,hz(1.44)], db:0.004 }); } put(P, same(8,HD), mat, 'hat', { inside:[0,0,hz(1.40)], db:0.004 }); };
      const up=(zl,d)=>zl.map(z=>Math.min(1.50,z+d));
      if(b.hat==='watchcap'){ const c1=up(lowZ,0.035); shell(lowZ,c1,e+0.004,'hatL'); const A=shell(c1,[1.50,1.50,1.50,1.50,1.50,1.50,1.50,1.50],e,hm); cap(A, top(1.522,0.030,e*0.6,hm), hm); }
      else if(b.hat==='souwester'){ const A=shell(lowZ,[1.50,1.50,1.50,1.50,1.50,1.50,1.50,1.50],e,hm); cap(A, top(1.522,0.024,e*0.7,hm), hm);
        const R=ringV(lowZ,e), out=[0.034,0.022,0.022,0.034,0.080,0.100,0.100,0.080], drop=[0.012,0.006,0.006,0.012,0.032,0.040,0.040,0.032], O=R.map((p,i)=>{ const d=vnorm([p[0],p[1],0]); return [p[0]+d[0]*out[i], p[1]+d[1]*out[i], p[2]-drop[i]]; });
        for(let k=0;k<8;k++){ const k2=(k+1)%8, q=[R[k],R[k2],O[k2],O[k]], m=cen(q); for(let i=0;i<4;i++) put([q[i],q[(i+1)%4],m], same(3,HD), 'hatL', 'hat', { inside:[m[0],m[1],m[2]-0.2] }); } }
      else if(b.hat==='ballcap'){ const A=shell(lowZ,[1.50,1.50,1.50,1.50,1.50,1.50,1.50,1.50],e,hm); cap(A, top(1.522,0.020,e*0.7,hm), hm);
        const zb=hz(HAT.F), yb=fAt(HAT.F)+e; put([[-0.075,yb-0.004,zb],[0.075,yb-0.004,zb],[0.062,yb+0.056,zb+0.006],[-0.062,yb+0.056,zb+0.006]], same(4,HD), 'hatL', 'hat', { inside:[0,yb,zb-0.2] }); }
      else if(b.hat==='kerchief'){ const A=shell(lowZ,[1.50,1.50,1.50,1.50,1.50,1.50,1.50,1.50],e,hm); cap(A, top(1.522,0.010,e*0.8,hm), hm);
        const yk=-(kAt(1.28)+e), zk=hz(1.28); tet([[-0.035,yk,zk+0.02],[0.035,yk,zk+0.02],[0,yk-0.025,zk+0.03]],[0,yk-0.012,zk-0.045],'hatD'); }
      else if(b.hat==='flatcap'){ const A=shell(lowZ,[1.49,1.49,1.49,1.49,1.49,1.49,1.49,1.49],e,hm); cap(A, top(1.50,0.022*zs+0.008,e+0.004,hm,0.05), hm);
        const zb=hz(HAT.F), yb=fAt(HAT.F)+e; put([[-0.068,yb-0.004,zb+0.004],[0.068,yb-0.004,zb+0.004],[0.056,yb+0.050,zb-0.006],[-0.056,yb+0.050,zb-0.006]], same(4,HD), 'hatD', 'hat', { inside:[0,yb,zb-0.2] }); }
      else if(b.hat==='hood'){ const Z=(v)=>[v,v,v,v,v,v,v,v];
        shell([1.16,1.16,1.16,1.16,1.16,1.16,1.16,1.16].map((z,i)=>z),Z(1.425),e,hm,[4,5,6]);
        const A=ringAt(1.16,e), Bq=ringAt(1.445,e), yM=yHair+0.012;   // 9.2: down to 1.16 like the back, so a turned head leaves no gap at the nape
        for(const k of [3,7]){ const q=[A[k],A[(k+1)%8],Bq[(k+1)%8],Bq[k]].map(p=>[p[0],Math.min(p[1],yM),p[2]]); put(q, same(4,HD), hm, 'hat', { inside:[0,0,hz(1.32)] }); }
        shell(Z(1.40),Z(1.445),e,hm,[0,2]); const T=shell(Z(1.445),Z(1.50),e,hm); shell(Z(1.425),Z(1.445),e,hm,[3,4,5,6,7]);
        cap(T, top(1.522,0.022,e*0.7,hm), hm);
        /* the hood's gathered neck rides the neck bone, reaches down inside the collar and up inside the hood, and is closed on top,
           so a full head turn never shows the neck through it */
        /* 10.0: taken in toward the smaller head (hk) and kept behind its chin (hf) */
        const hk=(1+D.kH)/2, hf=0.78;
        loft('hood',[ { pts:oct(0,-0.010,zm(1.030),0.122*nw*hk,0.090*nw*hf,0.108*nw*hk,0.040*hk), sk:NK, mat:'hatD' }, { pts:oct(0,-0.010,zm(1.150),0.104*nw*hk,0.096*nw*hf,0.096*nw*hk,0.036*hk), sk:NK, mat:'hatD' }, { pts:oct(0,-0.006,zm(1.205),0.090*nw*hk,0.090*nw*hf,0.084*nw*hk,0.030*hk), sk:NK } ], { top:'hatD' }); }
      else { /* the 9.1 hats sit level, so every ring is an octagon parallel to the head's and every quad between two rings is planar */
        const zb=HAT.F, z0=hz(zb), P={ w:hwAt(zb)+e, f:fAt(zb)+e, k:kAt(zb)+e, c:cAt(zb)+e*0.4 }, zTop=hz(1.522);
        const O8=(z,dw,df,dk)=>{ df=df==null?dw:df; dk=dk==null?dw:dk; return oct(0,0,z,P.w+dw,P.f+df,P.k+dk,P.c+0.586*Math.min(dw,df,dk)); };
        const wall=(A,Bq,mat)=>{ for(let k=0;k<8;k++){ const k2=(k+1)%8; quad([A[k],A[k2],Bq[k2],Bq[k]], mat); } };
        const lid=(T,mat)=>put(T, same(8,HD), mat, 'hat', { inside:[0,0,T[0][2]-0.2], db:0.004 });
        const brim=(A,O,mat)=>{ for(let k=0;k<8;k++){ const k2=(k+1)%8, q=[A[k],A[k2],O[k2],O[k]], m=cen(q); put(q, same(4,HD), mat, 'hat', { inside:[m[0],m[1],m[2]-0.2], db:0.004 }); } };
        if(b.hat==='tophat'){ const A1=O8(z0+0.034,0), T=O8(z0+HAT.h,0.008); wall(O8(z0,0),A1,'hatD'); wall(A1,T,hm); lid(T,hm); brim(O8(z0+0.004,0),O8(z0+0.004,0.050,0.026),hm); }
        else if(b.hat==='bowler'){ const A1=O8(z0+0.040,0.004); wall(O8(z0,0),A1,hm); cap(A1, top(1.522,0.040,e*0.5,hm), hm); brim(O8(z0+0.004,0),O8(z0+0.010,0.030,0.024),hm); }
        else if(b.hat==='captain'){ const A1=O8(z0+0.066,0), T=O8(Math.max(zTop+0.014,z0+0.100),0.020,0.026,0.014);
          wall(O8(z0,0),A1,'boot'); wall(A1,T,hm); lid(T,hm);
          put([[-0.068,P.f-0.004,z0+0.004],[0.068,P.f-0.004,z0+0.004],[0.056,P.f+0.050,z0-0.010],[-0.056,P.f+0.050,z0-0.010]], same(4,HD), 'boot', 'hat', { inside:[0,P.f,z0-0.2] });
          put([[-0.020,P.f+0.002,z0+0.014],[0.020,P.f+0.002,z0+0.014],[0.020,P.f+0.002,z0+0.056],[-0.020,P.f+0.002,z0+0.056]], same(4,HD), 'brass', 'hat', { inside:[0,0,z0], db:0.012 }); }
        else if(b.hat==='sunhat'){ const A1=O8(z0+0.034,-0.004); wall(O8(z0,0),A1,hm); cap(A1, top(1.522,0.026,e*0.6,hm), hm); brim(O8(z0+0.004,0),O8(z0-0.008,0.080,0.034,0.074),'hatL'); }
        else if(b.hat==='bucket'){ const T=O8(zTop+0.016,-0.014); wall(O8(z0,0),T,hm); lid(T,hm); brim(O8(z0+0.004,0),O8(z0-0.026,0.046,0.026,0.044),'hatD'); }
        else if(b.hat==='beret'){ const A1=O8(z0+0.018,0), W1=O8(z0+0.048,0.040,0.030,0.046), T=O8(zTop+0.022,-0.010);
          wall(O8(z0,0),A1,'hatD'); wall(A1,W1,hm); wall(W1,T,hm); lid(T,hm); } } }
    /* ---------------- 10.2: the beard's mass (BEARDS); its marks are laid with the face's, below ---------------- */
    if(BS.mass){ const M=BS.mass, px=1/(PX*D.kH), row=RS/D.kH, zT=hz(1.205), zC=hz(1.135), zB=fz(1.155)-M.drop*row, e=0.006;
      const wT=Math.max(hwAt(1.205)*M.w, 0.6*px), wC=Math.max(hwAt(1.135)*Math.min(1.2,M.w*1.25), (M.wMid||0.6)*px), wB=(M.wEnd||0.5)*px;
      const fT=fAt(1.205)+e, fC=fAt(1.135)+M.fwd*0.6+e, fB=fAt(1.135)+M.fwd, O=(z,w,f,yb)=>oct(0,0,z,w,f,-yb,Math.min(w,f-yb)*0.35);
      loft('beard', [ { pts:O(zB,wB,fB,fB-0.045), sk:HD, mat:'beard' }, { pts:O(zC,wC,fC,0), sk:HD, mat:'beard' }, { pts:O(zT,wT,fT,0), sk:HD } ], { bot:'hairD' }); }

    /* ---------------- THE FACE (10.1): point marks, each built for one design camera ---------------- */
    /* a mark's centre is the skull point its design camera sees dx, dy pixels from headMid (the head snap holds headMid on a pixel
       centre), lifted 2 mm toward that camera. It draws the one pixel under its centre (pt) while the camera's azimuth is within its
       role's az (ROLES). Where that camera sees no skull (the far eye's column 2, past the cheek) there is no mark. */
    const pxP=1/(PX*D.kH), ceE=Math.cos(ELEV*DEG), seE=Math.sin(ELEV*DEG), HMID=[0, HRb[2][2], eyeZ], STRI=[];
    for(let i=0;i<5;i++) for(let k=0;k<8;k++){ const k2=(k+1)%8, A=SKR[i], Bq=SKR[i+1]; STRI.push([A[k],A[k2],Bq[k2]],[A[k],Bq[k2],Bq[k]]); }
    const OCC=[]; for(const f of F) if(f.part==='hair'||f.part==='hat'||f.part==='hood'||f.part==='ear') for(let i=1;i+1<f.v.length;i++) OCC.push([f.v[0],f.v[i],f.v[i+1]]);
    const rayHit=(O,v,L)=>{ let best=null; for(const T of (L||STRI)){ const e1=vsub(T[1],T[0]), e2=vsub(T[2],T[0]), p=vcross(v,e2), det=vdot(e1,p); if(Math.abs(det)<1e-12) continue;
        const s0=vsub(O,T[0]), uu=vdot(s0,p)/det; if(uu<-1e-9 || uu>1+1e-9) continue; const q=vcross(s0,e1), ww=vdot(v,q)/det; if(ww<-1e-9 || uu+ww>1+1e-9) continue;
        const t=vdot(e2,q)/det; if(best===null || t>best) best=t; } return best; };
    const rayOf=(yawD,dx,dy)=>{ const Y=yawD*DEG, ct=Math.cos(-Y), st=Math.sin(-Y), u=[ct,-st,0]; return [vadd(HMID, vadd(vmul(u,dx*pxP), [0,0,-dy*pxP/ceE])), [-ceE*st,-ceE*ct,seE]]; };
    /* the far eye at a diagonal sits a column past the bridge; on a head too narrow for that (or where its hair or hat covers that pixel)
       the pair moves a column toward the near side: diagS 1 */
    const seenAt=(yawD,dx,dy)=>{ const [O,v]=rayOf(yawD,dx,dy), t=rayHit(O,v); if(t===null) return false; const t2=rayHit(O,v,OCC); return !(t2!==null && t2>t+0.004); };
    /* 10.3: the body round the head (collar, shoulders, straps, the neck), taken into the head's unscaled frame. A diagonal eye pair or a
       mouth pixel the body covers from its camera (a child's head sits low between the shoulders) moves as it does for the hair */
    const OCCB=[]; { const k=D.kH, h0=D.headZ, un=(p)=>[p[0]/k, p[1]/k, h0+(p[2]-h0)/k], HPs={ head:1, nose:1, ear:1, hair:1, hat:1, beard:1, face:1 };
      for(const f of F) if(!HPs[f.part] && f.v.some(p=>p[2]>D.shZ-0.12)) for(let i=1;i+1<f.v.length;i++) OCCB.push([un(f.v[0]),un(f.v[i]),un(f.v[i+1])]); }
    const seenB=(yawD,dx,dy)=>{ if(!seenAt(yawD,dx,dy)) return false; const [O,v]=rayOf(yawD,dx,dy), t=rayHit(O,v), t3=rayHit(O,v,OCCB); return !(t3!==null && t3>t+0.004); };
    /* the pair holds 0.08 px inside the cheek's edge, so the small turn and roll of a clip's head (idle: 0.5 deg, 0.8 deg) cannot take the far
       eye's pixel off the face (10.2: the Wharf girl at SW) */
    const pairAt=(sh)=>seenB(135, 1-sh, 0) && seenB(135, -1-sh, 0) && seenB(135, 1-sh+0.08, 0) && seenB(135, -1-sh-0.08, 0), diagS = pairAt(0) ? 0 : pairAt(1) ? 1 : 0;
    /* the face rows are anchored at headMid, on the centreline; the side plane is hwAt(1.31) nearer a profile camera, so at the same
       height it shows sideDy rows lower: a profile mark's row is the face row plus sideDy */
    const rowPx=(r)=>ROWP[r-1], sideDy=Math.round(seE*PX*D.kH*hwAt(1.31));
    /* 10.3: the profile eye sits a column further back than the front view's (SIDE). Where the hair, hat, hood or ear covers that pixel
       from the profile camera (the bob's curtain and the temple hair on the square and wide heads), it moves a column forward: sideS 1 */
    const sideAt=(sh)=>seenAt(90, -(2-sh), rowPx(3)+sideDy) && seenAt(270, 2-sh, rowPx(3)+sideDy), sideS = sideAt(0) ? 0 : sideAt(1) ? 1 : 0;
    function markAt(group, mat, role, yawD, dx, dy, g, db, oh){ const [O,v]=rayOf(yawD,dx,dy), t=rayHit(O,v); if(t===null) return false;
      const P=vadd(O, vmul(v, t+0.002/D.kH)), nh=vnorm([v[0],v[1],0]), a=[-nh[1],nh[0],0], q=0.25*pxP, cn=(sa,sb)=>vadd(P, vadd(vmul(a,sa*q), [0,0,sb*q]));
      put([cn(-1,-1),cn(1,-1),cn(1,1),cn(-1,1)], same(4,HD), mat, 'face', { inside:vsub(P,vmul(nh,0.01)), db:(db||0.022)*D.kH, group, side:g, pt:1, oh:oh||0, az:Math.cos(ROLES[role].az*DEG) }); return true; }
    function marks(group, cells, g, o){ o=o||{};
      const emit=(role, list)=>{ if(!list) return; const R=ROLES[role];
        for(const [c0,c1,r0,r1,mat] of list) for(let c=c0;c<=c1;c++) for(let r=Math.min(r0,r1);r<=Math.max(r0,r1);r++){ if(role==='far' && c>1) continue;
          markAt(group, mat, role, R.yaw(g), R.dx(g,c,role==='side'?sideS:diagS), rowPx(r)+(role==='side'?sideDy:0), g, o.bias || (o.db && (mat==='ink'||mat==='iris') ? o.db : 0), o.oh); } };
      emit('front', cells); emit('diag', cells); emit('far', o.far||cells); emit('side', o.side||cells); }
    const ES=EYE_SHAPES[b.eyeShape]||EYE_SHAPES.round;
    /* gaze: eyes.right turns the right eye's pupil out a column (away from the nose) and leaves the left eye's where the open eye has it;
       eyes.left the mirror. Where the eye turned out is the far eye at a diagonal, its pupil has gone round the face and that pixel shows
       white. In profile the eye turned toward the camera keeps its pupil and the eye turned away shows the half tone. The pupils of a gaze
       state win where a mark overlaps its neighbour (depth bias 0.024 against 0.022). */
    for(const st of FACE_SLOTS.eyes) for(const g of [-1,1]){
      if(st==='left' || st==='right'){ const L=ES.gaze[(st==='right')===(g>0) ? 'out' : 'in']; marks('eyes.'+st, L.near==='open' ? ES.open : L.near, g, { side:L.side, far:L.far, db:0.024 }); }
      else marks('eyes.'+st, ES[st], g); }
    const BR={ flat:[], up:[[1,2,1,1,'brow']], knit:[[1,1,2,2,'brow']] };   /* 10.0: the flat brow is the fringe's edge; knit is a pixel over each eye */
    /* 10.1: a brow may sit on the fringe's edge (oh). 10.3: and it wins over the eye's marks (depth bias 0.023 against 0.022), so knit
       shows over a lidded eye, whose lid is on the knit brow's pixel */
    for(const st of FACE_SLOTS.brows) for(const g of [-1,1]) marks('brows.'+st, BR[st], g, { oh:1, bias:0.023 });
    /* the mouth: the centre column on the mouth row from ahead and at both diagonals (moved with the eyes by diagS); grit adds a column
       each side, a row up where the chin is too narrow for it; smile's corners are a row up */
    /* 10.3: the centre column too goes a row up where the chin is too short for the mouth row, or the collar covers it (the heart head, a
       child's head); there it takes the nose's lowest pixel */
    const mouth=(group, cols, mat, up)=>{ for(const col of cols){ const g=Math.sign(col), c=Math.abs(col), dy=rowPx(6)-(g && up ? 1 : 0), at=(role,yw,dx,gg)=>(seenB(yw,dx,dy) && markAt(group,mat,role,yw,dx,dy,gg,0.045)) || markAt(group,mat,role,yw,dx,dy-1,gg,0.045);
      if(!g){ at('front', 180, 0, 0); at('diag', 135, -diagS, 0); at('diag', 225, diagS, 0); }
      else for(const role of ['front','diag','far']) at(role, ROLES[role].yaw(g), ROLES[role].dx(g,c,diagS), g); } };
    mouth('mouth.flat', [0], 'lip'); mouth('mouth.open', [0], 'mouth'); mouth('mouth.grit', [-1,0,1], 'mouth'); mouth('mouth.smile', [-1,0,1], 'lip', true);
    /* 10.2: THE BEARD'S MARKS (BEARDS.marks), laid like the face's: at each design camera, the columns of a face row it sees on the skull,
       then the style's columns among them. They sit under the eyes and the mouth (depth bias 0.018; the moustache 0.030, over the nose),
       and never on an eye's column (in profile, nor beside it). */
    if(BS.marks){ const vis=(yw,dy)=>{ const o=[]; for(let dx=-9;dx<=9;dx++) if(seenAt(yw,dx,dy)) o.push(dx); return o; };
      const CAMS=[['front',180,0,0,[-1,1]],['diag',135,-1,-diagS,[-1-diagS,1-diagS]],['diag',225,1,diagS,[1+diagS,-1+diagS]],['side',90,-1,null,[-(2-sideS)]],['side',270,1,null,[2-sideS]]];
      for(const [role,yw,ear,nose,eyes] of CAMS){ const prof=role==='side';
        for(const rs of Object.keys(BS.marks)){ const r=+rs, spec=BS.marks[rs], dy=r+(prof?sideDy:0), V=vis(yw,dy); if(!V.length) continue;
          const lo=V[0], hi=V[V.length-1], out=new Map(), add=(dx,mat,db)=>{ if(V.indexOf(dx)<0) return; if(r<=0 && eyes.some(c=>Math.abs(c-dx)<=(prof?1:0))) return; out.set(dx,[mat,db]); };
          if(spec.indexOf('A')>=0) for(const dx of V) add(dx,'beard',0.018);
          if(spec.indexOf('E')>=0){ if(ear<=0) add(lo,'beard',0.018); if(ear>=0) add(hi,'beard',0.018); }
          if(spec.indexOf('T')>=0 || (BS.tache && r===1)){ const tm=BS.tache||'beard'; if(prof) add(yw===90?hi:lo, tm, 0.030); else for(const dx of [nose-1,nose,nose+1]) add(dx, tm, 0.030); }
          for(const [dx,[mat,db]] of out) markAt(null, mat, role, yw, dx, dy, 0, db); } } }
    
    /* 10.0: the head was built at pass 9's size; scale it about the head bone to kH (the face marks were laid at 1/kH, so they land on 1 px) */
    { const HP={ head:1, nose:1, ear:1, hair:1, hat:1, beard:1, face:1 }, k=D.kH, hz0=D.headZ;
      for(const f of F.concat(FG)) if(HP[f.part]) f.v=f.v.map(p=>[p[0]*k, p[1]*k, hz0+(p[2]-hz0)*k]); }
    const faces=F.slice(), groups={};
    for(const gname of GROUP_ORDER){ const list=FG.filter(f=>f.group===gname); groups[gname]=[faces.length, list.length]; faces.push(...list); }
    return { faces, groups, slots:FACE_SLOTS };
  }

  /* ============================ build cache ============================ */
  const _cache={};
  function buildOf(build){
    if(build && build.sk && build.mesh && build.D) return build;
    let n, key;
    if(typeof build==='string'){ if(_cache[build]) return _cache[build];
      if(BUILDS[build]){ n=normBuild(build); key=build; }
      else if(build.slice(0,2)==='b:'){ n=normBuild(JSON.parse(build.slice(2))); key=build; }
      else throw new Error('characterIsoRig10: no preset "'+build+'" (CAST: '+CAST.join(', ')+')'); }
    else { n=normBuild(build||'fisher'); const p=n.preset ? normBuild(n.preset) : null; key = p && sameBuild(p,n) ? n.preset : buildKey(n); if(_cache[key]) return _cache[key]; }
    const ks=Object.keys(_cache); if(ks.length>60) for(const k of ks) if(!BUILDS[k]) delete _cache[k];
    const D=dimsOf(n), sk=skeletonOf(D);
    return (_cache[key]={ key, b:n, D, sk, mesh:meshOf(n,D,sk), mats:makeMats(n), gm:garmentOf(n) });
  }

  /* ============================ the solver ============================
     An intent (what the pose library writes) becomes bone LOCALS: legs by 2-bone IK in the pelvis frame, arms by
     2-bone IK in the chest frame (targets given in the chest frame, or in the world and read through the UNROCKED
     chest), sockets from the unrocked worlds. Rock then pre-multiplies three locals. Nothing downstream re-solves. */
  const hangC=(s,D)=>{ const g=s==='L'?-1:1; return [g*D.hangX, 0.018, (D.shZ-D.chestZ)-D.armDrop]; };
  function baseIntent(D){ return {
    pelvis:{ p:[0,0,D.pelvisZ-0.016], yaw:0, pitch:0, roll:0 }, spine:{}, chest:{}, neck:{}, head:{},
    legs:{ L:{ p:[-D.footX,0,D.ankle], yaw:-5 }, R:{ p:[D.footX,0,D.ankle], yaw:5 } },
    arms:{ L:{ c:hangC('L',D) }, R:{ c:hangC('R',D) } },
    face:{ eyes:'open', brows:'flat', mouth:'flat' }, tool:null, swing:{ L:0, R:0, mid:0 }, plant:{ L:true, R:true }, pins:{}, meta:{} }; }
  /* two-bone IK. S root joint, T target, lengths a/b, hint = where the middle joint should go. Bone frames: local z
     = back along the bone, local x = the hinge (sgn picks the side that is identity at rest: +1 legs, -1 arms). */
  function ik2(S,T,a,b,hint,sgn){
    const D0=vsub(T,S), d=vlen(D0), dn = d>1e-9 ? vmul(D0,1/d) : [0,0,-1];
    const dc=clamp(d, Math.abs(a-b)+1e-6, a+b-1e-7);
    let h=vsub(hint, vmul(dn, vdot(hint,dn))); let hl=vlen(h);
    if(hl<1e-6){ h=Math.abs(dn[2])<0.9 ? vcross(dn,[0,0,1]) : vcross(dn,[1,0,0]); hl=vlen(h); }
    h=vmul(h,1/hl);
    const ca=clamp((a*a+dc*dc-b*b)/(2*a*dc),-1,1), sa=Math.sqrt(Math.max(0,1-ca*ca));
    const M=vadd(S, vadd(vmul(dn,a*ca), vmul(h,a*sa))), E=vadd(S, vmul(dn,dc));
    const x0=vnorm(sgn>0 ? vcross(dn,h) : vcross(h,dn));
    const fr=(z)=>{ const x=vnorm(vsub(x0, vmul(z, vdot(x0,z)))); return cols(x, vcross(z,x), z); };
    return { Ru:fr(vnorm(vsub(S,M))), Rl:fr(vnorm(vsub(M,E))), M, E, err:Math.max(0, d-dc) };
  }
  const footR=(Lg)=>mM(Rz(-(Lg.yaw||0)*DEG), mM(Rx(-(Lg.pitch||0)*DEG), Ry((Lg.roll||0)*DEG)));
  function bodyOf(I,B){ const rest=B.sk.rest, ix=B.sk.ix;
    const Wp={ p:I.pelvis.p.slice(), R:eul(I.pelvis) }, Ws=fk(Wp,{ p:rest[ix.spine], R:eul(I.spine) }), Wc=fk(Ws,{ p:rest[ix.chest], R:eul(I.chest) });
    return { Wp, Ws, Wc }; }
  function armTarget(A, Wc){ const cw = A.c ? fkp(Wc, A.c) : null;
    const w = (A.w && A.c && A.k!=null) ? vlerp(cw, A.w, clamp01(A.k)) : (A.w || cw);
    return mTV(Wc.R, vsub(w, Wc.p)); }
  function fkAll(loc, SK){ const Wd=new Array(loc.length);
    for(let i=0;i<loc.length;i++){ if(!loc[i]) continue; const par=SK.bones[i].parent; Wd[i]= par<0 ? { p:loc[i].p.slice(), R:loc[i].R.slice() } : fk(Wd[par], loc[i]); }
    return Wd; }
  const ROCK_BONES=['spine','chest','head'], ROCK_W={ spine:0.45, chest:0.30, head:0.25 };
  /* rig 6's counterLean, with the pitch term turned to oppose the deck: a deck roll r tips the figure's top toward
     +x (camera roll), a deck pitch p tips it toward -y (camera pitch); the counter-lean answers both. */
  function counterLean(roll, pitch, k){ return { list:-(roll||0)*k, lean:(pitch||0)*k*0.9 }; }
  function rockDelta(rock){ const k = rock.counter==null ? 1 : +rock.counter, c=counterLean(rock.roll, rock.pitch, k), o={};
    for(const id of ROCK_BONES) o[id]=eul({ roll:c.list*ROCK_W[id], pitch:c.lean*ROCK_W[id] }); return o; }
  function solve(I, B, rock, look){
    const SK=B.sk, D=B.D, ix=SK.ix, rest=SK.rest, loc=new Array(SK.bones.length), err={};
    const { Wp, Wc } = bodyOf(I,B);
    loc[ix.root]={ p:[0,0,0], R:I3.slice() }; loc[ix.pelvis]={ p:Wp.p.slice(), R:Wp.R };
    for(const id of ['spine','chest','neck','head']) loc[ix[id]]={ p:rest[ix[id]], R:eul(I[id]) };
    if(look && (look.yaw || look.pitch)) for(const id of LOOK.bones) loc[ix[id]].R=mM(loc[ix[id]].R, lookE(look, id));
    for(const s of ['L','R']){ const g=s==='L'?-1:1, Lg=I.legs[s];
      const tW = Lg.P ? fkp(Wp, Lg.P) : Lg.p, tL=mTV(Wp.R, vsub(tW, Wp.p));
      /* the knee's pole: the hip-to-ankle line turned 90 deg forward about the hip's hinge. Foot below -> knee forward,
         foot ahead -> knee up, foot behind -> knee down; continuous everywhere a clip puts a foot below 50 deg above
         the hip. splay opens it outward. An explicit world `kn` still wins. */
      const hL=rest[ix['hip_'+s]], dn=vnorm(vsub(tL,hL)), hint = Lg.kn ? mTV(Wp.R, Lg.kn) : vadd(vcross([1,0,0],dn), [g*(Lg.splay==null?0.10:Lg.splay),0,0]);
      const K=ik2(hL, tL, D.thigh, D.shin, hint, 1);
      loc[ix['hip_'+s]]={ p:rest[ix['hip_'+s]], R:K.Ru }; loc[ix['knee_'+s]]={ p:rest[ix['knee_'+s]], R:mTM(K.Ru,K.Rl) };
      const Rsh=mM(Wp.R,K.Rl), Rf = Lg.rel==='shin' ? mM(Rsh, footR(Lg)) : Lg.rel==='pelvis' ? mM(Wp.R, footR(Lg)) : footR(Lg);
      loc[ix['foot_'+s]]={ p:rest[ix['foot_'+s]], R:mTM(Rsh,Rf) }; err['foot'+s]=K.err; }
    for(const s of ['L','R']){ const g=s==='L'?-1:1, A=I.arms[s];
      const K=ik2(rest[ix['shoulder_'+s]], armTarget(A,Wc), D.upper, D.fore+D.palmLen, A.el||[g*0.12,-1,-0.1], -1);
      loc[ix['shoulder_'+s]]={ p:rest[ix['shoulder_'+s]], R:K.Ru }; loc[ix['elbow_'+s]]={ p:rest[ix['elbow_'+s]], R:mTM(K.Ru,K.Rl) };
      loc[ix['hand_'+s]]={ p:rest[ix['hand_'+s]], R:eul(A.wrist) }; err['hand'+s]=K.err; }
    const W0=fkAll(loc, SK), palm=(s)=>fkp(W0[ix['hand_'+s]], D.palm);
    const T=I.tool, held=!!(T && T.held), Rt = held ? aimM((T.pitch||0)*DEG, (T.yaw||0)*DEG) : null;
    for(const s of ['L','R']){ const Wh=W0[ix['hand_'+s]], gp=palm(s), R0 = held ? Rt : Wh.R, Wt={ p:gp, R:R0 };
      loc[ix['tool_'+s]]=localOf(Wh,Wt);
      const len=(T && T.len) || D.rodLen, bend=(T && T.bend) || 0, Dd=mV(R0,[0,1,0]);
      const pt=(sl)=>{ const q=Math.max(0, sl/len-0.40)/0.60; return [gp[0]+Dd[0]*sl, gp[1]+Dd[1]*sl, gp[2]+Dd[2]*sl - bend*q*q*len*0.24]; };
      /* 10.3: a bend chord's frame is the frame before it turned the shortest way onto the chord, so the chords keep the rod's roll
         (10.2's aimTo took a world-up frame, which turned 180 deg about the rod each time the rod passed vertical) */
      const p1=pt(0.40*len), p2=pt(0.70*len), p3=pt(len), W1={ p:p1, R:bend ? onto(R0,p1,p2) : R0 }, W2={ p:p2, R:bend ? onto(W1.R,p2,p3) : R0 };
      loc[ix['tool_'+s+'_1']]=localOf(Wt,W1); loc[ix['tool_'+s+'_2']]=localOf(W1,W2);
      loc[ix['carry_'+s]]=localOf(Wh,{ p:gp, R:mM(Wh.R, Rx((I.swing[s]||0)*DEG)) }); }
    loc[ix.carry_mid]=localOf(W0[ix.chest], { p:vlerp(palm('L'),palm('R'),0.5), R:mM(W0[ix.chest].R, Rx((I.swing.mid||0)*DEG)) });
    loc[ix.back]={ p:rest[ix.back], R:BACK_R.slice() };
    if(rock){ const Q=rockDelta(rock); for(const id of ROCK_BONES) loc[ix[id]]={ p:loc[ix[id]].p, R:mM(Q[id], loc[ix[id]].R) }; }
    return { I, loc, W:fkAll(loc,SK), err, rock:rock||null };
  }
  function blendIntent(A,B2,w){ if(w<=0) return; const L=(x,y)=>(x||0)+((y||0)-(x||0))*w;
    A.pelvis.p=vlerp(A.pelvis.p,B2.pelvis.p,w); for(const k of ['yaw','pitch','roll']) A.pelvis[k]=L(A.pelvis[k],B2.pelvis[k]);
    for(const part of ['spine','chest','neck','head']) for(const k of ['yaw','pitch','roll']) A[part][k]=L(A[part][k],B2[part][k]);
    for(const s of ['L','R']){ A.legs[s].p=vlerp(A.legs[s].p,B2.legs[s].p,w); for(const k of ['yaw','pitch','roll']) A.legs[s][k]=L(A.legs[s][k],B2.legs[s][k]); } }

  /* ============================ clips ============================ */
  let POSES=null, CARRY_STYLE=null, POST=null;
  /* an elder's stoop rides every clip that hands to or from idle; the deck work, the water, the bed, the cab and the ladder set their own spine */
  const NO_STOOP={ ladderDown:1, swim:1, tread:1, sleep:1, hauler:1, bench:1, chop:1, lift:1, place:1, toss:1, drive:1 };
  /* 10.1: the deck work's world grips are held in the arm's reach on any build: a body the creator makes may be broader, shorter or
     taller than the pose library's frame, and a grip at workZ then sits past the hand (the rope or the item follows the hand) */
  const REACH_HELD={ hauler:1, bench:1, chop:1, lift:1, place:1, toss:1 };
  function holdInReach(I,B){ const D=B.D, sk=B.sk, Wc=bodyOf(I,B).Wc, arm=D.upper+D.fore+D.palmLen-0.004;
    for(const s of ['L','R']){ const A=I.arms[s]; if(!A || !A.w || A.c) continue; const sh=fkp(Wc, sk.rest[sk.ix['shoulder_'+s]]), d=vsub(A.w,sh), dl=vlen(d); if(dl>arm) A.w=vadd(sh, vmul(d, arm/dl)); } }
  function intentOf(name, u, B, opts){
    const cd=clipDef(name); if(!cd) throw new Error('characterIsoRig10: no clip "'+name+'"');
    if(!POSES) throw new Error('characterIsoRig10: load characterIsoRig10.poses.js');
    const K=ctxOf(cd, opts, B), I=baseIntent(B.D);
    POSES[cd.anim](u, I, K); if(cd.carry && CARRY_STYLE[cd.carry]) CARRY_STYLE[cd.carry](u, I, K);
    const so=NO_STOOP[cd.anim] ? 0 : B.D.stoop; if(so){ I.spine.pitch=(I.spine.pitch||0)+so*0.5; I.chest.pitch=(I.chest.pitch||0)+so*0.5; I.neck.pitch=(I.neck.pitch||0)-so*0.6; I.head.pitch=(I.head.pitch||0)-so*0.25; }
    if(REACH_HELD[cd.anim]) holdInReach(I,B);
    return { I, K, cd };
  }
  function evalClip(name, u, build, opts, rock, look){ const B=buildOf(build), X=intentOf(name, u, B, opts);
    const S=solve(X.I, B, rock, look); S.K=X.K; S.cd=X.cd; S.u=u; S.B=B; S.look=look||null; return S; }
  /* the look-at angles for a target point (figure frame, unrocked) on a solved frame without a turn: the target's direction and the
     clip's head forward, both in the chest frame; the turn is their difference, clamped to the limits; the eyes take what is left */
  function lookAt(S, target, share){ const B=S.B, ix=B.sk.ix, W=S.W, Rc=W[ix.chest].R, Wh=W[ix.head], eye=fkp(Wh, B.D.headMid), k=share==null ? LOOK.headShare : +share;
    const d=mTV(Rc, vsub(target, eye)), f=mTV(Rc, mV(Wh.R,[0,1,0])), yawOf=(v)=>Math.atan2(v[0],v[1])/DEG, pitchOf=(v)=>-Math.atan2(v[2],Math.hypot(v[0],v[1]))/DEG;
    let dy=yawOf(d)-yawOf(f); dy=((dy%360)+540)%360-180; const dp=pitchOf(d)-pitchOf(f);
    const yaw=clamp(dy*k, LOOK.yaw[0], LOOK.yaw[1]), pitch=clamp(dp*k, LOOK.pitch[0], LOOK.pitch[1]), ry=dy-yaw;
    return { yaw:+yaw.toFixed(3), pitch:+pitch.toFixed(3), need:{ yaw:+dy.toFixed(3), pitch:+dp.toFixed(3) }, residualYaw:+ry.toFixed(3), eyes: ry>LOOK.eyesBeyond_deg ? 'right' : ry<-LOOK.eyesBeyond_deg ? 'left' : 'open' }; }
  function exportLocals(loc, SK, prev){ const out={};
    loc.forEach((l,i)=>{ const id=SK.bones[i].id; let q=quatOf(l.R); const pq=prev && prev[id] && prev[id].rot;
      if(pq && (q[0]*pq[0]+q[1]*pq[1]+q[2]*pq[2]+q[3]*pq[3])<0) q=q.map(x=>-x);
      out[id]={ pos:l.p.slice(), rot:q, rotEuler:eulerZYX(l.R) }; }); return out; }
  const toolTrack=(I)=>I.tool ? { held:!!I.tool.held, kind:I.tool.kind||null, pitch:+(+I.tool.pitch||0).toFixed(3), yaw:+(+I.tool.yaw||0).toFixed(3), bend:+(+I.tool.bend||0).toFixed(4), len:I.tool.len||null, advisory:!!I.tool.advisory } : { held:false };
  function clip(name, build, opts){
    const cd=clipDef(name); if(!cd) return null; const A=ANIMS[cd.anim], B=buildOf(build), tracks=[]; let prev=null;
    for(let k=0;k<A.frames;k++){ const u=uOf(cd.anim,k), S=evalClip(name,u,B,opts), bones=exportLocals(S.loc,B.sk,prev); prev=bones;
      tracks.push({ frame:k, u:+u.toFixed(6), ms:k*A.ms, bones, face:Object.assign({},S.I.face), tool:toolTrack(S.I) }); }
    return { name, anim:cd.anim, carry:cd.carry||null, stance:cd.stance||null, build:B.key, frames:A.frames, ms:A.ms, duration:A.frames*A.ms,
      loop:!A.oneShot, settle:!!A.settle, mount:ANIM_MOUNT[cd.anim], pin: cd.carry ? CARRIES[cd.carry].pin : null, bones:B.sk.bones.map(b=>b.id), tracks };
  }
  function clips(build, opts){ return clipNames().map(n=>clip(n, build, opts)); }
  function skeleton(build){ const B=buildOf(build), S=B.sk;
    return S.bones.map((bn,i)=>({ id:bn.id, parent:bn.parent, kind:bn.kind, rest:{ pos:S.rest[i].slice(), rot:[0,0,0,1], rotEuler:[0,0,0] }, bind:{ pos:bn.p.slice(), rot:[0,0,0,1] } })); }
  function bindMesh(build){ return buildOf(build).mesh.faces; }

  /* ============================ gameplay pins ============================ */
  function pinsOf(S){ const B=S.B, W=S.W, ix=B.sk.ix, D=B.D, pt=(id,off)=>r4(off ? fkp(W[ix[id]],off) : W[ix[id]].p);
    const P={ handL:pt('hand_L',D.palm), handR:pt('hand_R',D.palm), footL:pt('foot_L',D.sole), footR:pt('foot_R',D.sole), hip:pt('pelvis'), head:pt('head',D.crown) };
    P.mid=r4(vlerp(P.handL,P.handR,0.5)); P.contact={ L:!!S.I.plant.L, R:!!S.I.plant.R };
    const ik={}; for(const k of Object.keys(S.err)) if(S.err[k]>1e-4) ik[k]=+S.err[k].toFixed(4); if(Object.keys(ik).length) P.ikShort=ik;
    if(S.I.tool && S.I.tool.held){ P.tool=Object.assign({ grip:P.handR }, toolTrack(S.I)); if(S.cd.anim==='dig') P.tool.grip2=P.handL; }
    for(const k of Object.keys(S.I.pins)) P[k]=S.I.pins[k];
    if(POST && POST[S.cd.anim]) POST[S.cd.anim](S, P, { pt, r4, fkp, vlerp, vsub, vlen, vnorm, vdist });
    return P; }
  const FRAME_DOC = { axes:'right-handed metres: +x right, +y forward (the facing), +z up; origin = cell pivot on the ground',
    rot:'unit quaternion [x,y,z,w], local to the parent bone', rotEuler:'the same rotation in degrees, intrinsic Z-Y-X', pos:'metres, local to the parent bone',
    unity:'pos (x, z, y); quaternion (-x, -z, -y, w)', bind:'every bind frame is identity; bind.pos is the joint in the figure frame', u:'k/frames, or k/(frames-1) on settle clips',
    face:'one group per slot switched on per frame: eyes.<state>, brows.<state>, mouth.<state>. The engine may replace the eyes state with the blink (faceClips.blink) and eyes.open with eyes.left / eyes.right (look)', rock:'deck rock is a live hull transform (camera roll/pitch); the counter-lean is the rock table, pre-multiplied onto spine, chest and head',
    marks:'10.1: a face mark (pt) draws the one pixel under its projected centre, over a pixel the head already covers (head, nose, beard or another mark) and nearer by its depth bias, while the horizontal angle between its normal and the camera is within acos(az); a centre exactly on a pixel edge draws nothing',
    smooth:'10.1: a face with sn (bind frame, skinned by its first bone) is lit on sn instead of its own normal; it is still culled on its own' };
  /* the collider (9.2, in the sidecar): the shoulder joint's distance from the centreline, and a footprint of twice that by twice the toe's
     reach ahead of the ankle; on the Fisher exactly 0.20 and 0.40 x 0.36. measured: the bind mesh's half-widths, whole and body only. */
  function colliderOf(build){ const B=buildOf(build), D=B.D, EX={ hair:1, hat:1, hood:1, face:1, nose:1, ear:1, beard:1 }, r3=(x)=>Math.round(x*1000)/1000; let bx=0, by=0, hx=0, hy=0;
    for(const f of B.mesh.faces){ const ex=!!(f.group || EX[f.part]);
      for(const p of f.v){ const ax=Math.abs(p[0]), ay=Math.abs(p[1]); if(ax>bx) bx=ax; if(ay>by) by=ay; if(!ex){ if(ax>hx) hx=ax; if(ay>hy) hy=ay; } } }
    return { collision_radius_m:r3(D.shX), footprint_m:[r3(2*D.shX), r3(2*0.18*D.footK)], from:{ shoulder_half_span_m:r3(D.shX), toe_reach_m:r3(0.18*D.footK) },
      measured:{ bind_half_width_m:[r3(bx),r3(by)], body_half_width_m:[r3(hx),r3(hy)] } }; }
  function blinkClip(){ const n=BLINK.frames.length; return { name:'blink', kind:'overlay', slot:BLINK.slot, frames:n, ms:BLINK.ms, duration:n*BLINK.ms, loop:false,
    tracks:BLINK.frames.map((e,k)=>({ frame:k, u:+(k/n).toFixed(6), ms:k*BLINK.ms, face:{ eyes:e } })), steps:BLINK.steps, then:BLINK.then,
    interval_ms:BLINK.interval_ms, firstBlink:BLINK.firstBlink, doubleChance:BLINK.doubleChance, doubleGap_ms:BLINK.doubleGap_ms, skipIf:BLINK.skipIf }; }
  function lookContract(){ return { bones:LOOK.bones, split:LOOK.split, yaw_deg:LOOK.yaw, pitch_deg:LOOK.pitch,
    sense:'yaw > 0 turns the face toward the figure\'s +x (its right); pitch > 0 tips it down (the rig\'s pitch: the top toward +y)',
    compose:'neck.local = clip.neck.local * E(split.neck * yaw, split.neck * pitch); head.local = rock.head * clip.head.local * E(split.head * yaw, split.head * pitch); E(y, p) = Rz(-y) * Rx(-p), angles in degrees, both clamped to the limits first',
    aim:'on the frame as the clip poses it (unrocked, no turn): d = the target minus the head point (head bone * headMid), f = the head bone\'s +y, both in the chest frame; need.yaw = atan2(d.x, d.y) - atan2(f.x, f.y) (wrapped to -180..180), need.pitch = -atan2(d.z, |d.xy|) + atan2(f.z, |f.xy|); turn = clamp(headShare * need) per axis',
    aimBar:{ deg:AIM.bar_deg, share:AIM.share, distance_m:AIM.distance_m, bearings_deg:AIM.bearings_deg, dz_m:AIM.dz_m, targets:AIM.targets, measure:AIM.measure },
    headShare:LOOK.headShare, eyes:{ left:'eyes.left', centre:'eyes.open', right:'eyes.right', beyond_deg:LOOK.eyesBeyond_deg, rule:'eyes.right when need.yaw - turn.yaw > beyond_deg, eyes.left when < -beyond_deg, else eyes.open; only in place of eyes.open (half, shut and wide stay as the clip has them); vertical gaze is the head pitch' } }; }
  function gameplay(build, opts){ const B=buildOf(build), out={};
    for(const name of clipNames()){ const cd=clipDef(name), A=ANIMS[cd.anim], fr=[];
      for(let k=0;k<A.frames;k++){ const u=uOf(cd.anim,k), S=evalClip(name,u,B,opts); fr.push(Object.assign({ frame:k, u:+u.toFixed(6) }, pinsOf(S))); }
      const S0=evalClip(name,0,B,opts);
      out[name]={ anim:cd.anim, carry:cd.carry||null, mount:ANIM_MOUNT[cd.anim], frames:A.frames, ms:A.ms, loop:!A.oneShot, settle:!!A.settle,
        speed_mps:S0.I.meta.speed!=null ? S0.I.meta.speed : null, handoff:S0.I.meta.handoff||null, pins:fr }; }
    const S=evalClip('idle',0,B,opts), P=pinsOf(S);
    return { schema:'hidden-harbours/character-gameplay@1', rig:'characterIsoRig10.js', exportSymbol:'CharacterIso10', revision:API.revision, build:B.key, buildSpec:B.b,
      playback:'walk and run stride with the legs: play them at rate = moveSpeed / clips[name].speed_mps so the planted foot does not skate', units:'metres',
      frame:{ origin:'the cell pivot on the ground under the figure', axes:'+x right, +y forward (facing), +z up', scale_px_per_m:PX, cell:[W,H], pivot:[PIVOT.x,PIVOT.y], elev_deg:ELEV,
        dirs:8, order:ORDER, dir0:'facing +y (N); 45 deg steps clockwise' },
      authoring:'Generated by CharacterIso10.gameplay() from the same pose library the clips are exported from. Do not hand-edit — re-generate.',
      extractor_contract:'per clip: pins per frame, in figure-frame metres, unrocked. An absent section means the clip does not use it (not an error). ikShort lists any limb that could not make its target, in metres.',
      figure:(()=>{ const Q=colliderOf(B.key); return { height_m:+(P.head[2]).toFixed(4), hip_z:P.hip[2], hands_hang_z:P.handL[2], footprint_m:Q.footprint_m, collision_radius_m:Q.collision_radius_m, collider_from:Q.from, collider_measured:Q.measured }; })(),
      contracts:CONTRACT, sockets:SOCKETS, overlays:{ blink:blinkClip(), look:lookContract() }, clips:out };
  }
  function sockets(build){ const B=buildOf(build);
    return { rig:'characterIsoRig10.js', build:B.key, sockets:B.sk.bones.filter(b=>b.kind==='socket').map(b=>({ id:b.id, parent:B.sk.bones[b.parent].id, rest:B.sk.rest[B.sk.ix[b.id]], purpose:SOCKETS[b.id] })),
      mounts:{ rod:'tool_R (+ tool_R_1, tool_R_2 for the bend); off hand on tool_L', shovel:'tool_R + tool_L, the shaft between the palms', knife:'tool_R',
        load:'carry_mid', warp:'none — draw the rope through handL / handR / sheave pins', bench:'none — the item pin is in the gameplay sidecar', rest:'tool_R until the release, then nothing',
        free:'carry_L / carry_R / carry_mid per the carry stance', water:'none', bed:'none', wheel:'carry pins unused; wheel grips in the sidecar', saddle:'none', ladder:'none' },
      carries:CARRIES, stances:CARRY_CLIPS }; }
  function rockTable(grid){ grid=grid||{ roll:[-15,-10,-5,0,5,10,15], pitch:[-15,-10,-5,0,5,10,15], counter:[0,0.5,1] }; const rows=[];
    for(const c of grid.counter) for(const r of grid.roll) for(const p of grid.pitch){ const Q=rockDelta({ roll:r, pitch:p, counter:c }), row={ roll:r, pitch:p, counter:c };
      for(const id of ROCK_BONES) row[id]={ rot:quatOf(Q[id]).map(x=>+x.toFixed(9)), rotEuler:eulerZYX(Q[id]) }; rows.push(row); }
    return { rig:'characterIsoRig10.js', bones:ROCK_BONES, weights:ROCK_W, compose:'local = delta * local (delta in the parent bone\'s frame); every other bone untouched',
      formula:'list = -roll*counter, lean = +pitch*counter*0.9 (deg); bone delta = Ry(list*w) * Rx(-lean*w)', keyed:'on the rock alone — no build, clip or frame', rows }; }
  function shadingContract(build){ const B=buildOf(build), m={};
    for(const [k,v] of Object.entries(B.mats)) m[k]=v.fixed ? { color:v.ramp[0], fixed:true } : { ramp:v.ramp, gain:v.gain, bias:v.bias, lo:v.lo, hi:v.hi, off:v.off };
    return Object.assign({}, SHADING, { materials:m }); }
  function sizes(build){ const B=buildOf(build), f=B.mesh.faces, tris=f.reduce((s,x)=>s+x.v.length-2,0), verts=f.reduce((s,x)=>s+x.v.length,0);
    const frames=clipNames().reduce((s,n)=>s+ANIMS[clipDef(n).anim].frames,0);
    /* 10.3: counted at 7 decimals, as the builds print it, so every engine counts the same bytes */
    return { bones:B.sk.bones.length, deform:B.sk.deform, faces:f.length, tris, verts, clipCount:clipNames().length, frames, bindBytes:JSON.stringify(f, R7).length }; }
  function exportBuild(build, opts){ const B=buildOf(build);
    return { rig:'characterIsoRig10', revision:API.revision, build:B.b, frame:FRAME_DOC, humanoid:HUMANOID, skeleton:skeleton(B.key), bindMesh:bindMesh(B.key),
      faceGroups:B.mesh.groups, faceSlots:FACE_SLOTS, shading:shadingContract(B.key), faceClips:[blinkClip()], look:lookContract(), clips:clips(B.key, opts) }; }

  /* ============================ the facet pass (reference renderer) ============================ */
  function camOf(o){ o=o||{}; const yaw=(o.yaw!=null ? o.yaw : (o.dir||0)*45)*DEG, e=(o.elev!=null?o.elev:ELEV)*DEG, S=o.px||PX, k=S/PX;
    return { ct:Math.cos(-yaw), st:Math.sin(-yaw), se:Math.sin(e), ce:Math.cos(e), cr:Math.cos((o.roll||0)*DEG), sr:Math.sin((o.roll||0)*DEG),
      cq:Math.cos((o.pitch||0)*DEG), sq:Math.sin((o.pitch||0)*DEG), S, W:Math.round(W*k), H:Math.round(H*k), cx:Math.round(PIVOT.x*k), cy:Math.round(PIVOT.y*k), heave:o.heave||0 }; }
  function proj(p, C){ const x=p[0], y=p[1], z=p[2], x1=x*C.cr+z*C.sr, z1=-x*C.sr+z*C.cr, y2=y*C.cq-z1*C.sq, z2=y*C.sq+z1*C.cq;
    const xr=x1*C.ct - y2*C.st, yr=x1*C.st + y2*C.ct;
    return { xr, yr, zr:z2, sx:C.cx+xr*C.S, sy:C.cy-(yr*C.se+z2*C.ce)*C.S - C.heave, d:yr*C.ce - z2*C.se }; }
  /* a direction through the same camera turn as proj (no translation) */
  const rotC=(v,C)=>{ const x1=v[0]*C.cr+v[2]*C.sr, z1=-v[0]*C.sr+v[2]*C.cr, y2=v[1]*C.cq-z1*C.sq, z2=v[1]*C.sq+z1*C.cq; return [x1*C.ct-y2*C.st, x1*C.st+y2*C.ct, z2]; };
  function posed(S, B, face){ const Wd=S.W, fc=face || S.I.face, act={}; act['eyes.'+fc.eyes]=1; act['brows.'+fc.brows]=1; act['mouth.'+fc.mouth]=1;
    const M=Wd.map((w,i)=>({ R:w.R, t:vsub(w.p, mV(w.R, B.sk.bones[i].p)) })), hIx=B.sk.ix.head, out=[];
    for(const f of B.mesh.faces){ if(f.group && !act[f.group]) continue;
      const v=f.v.map((p,k)=>{ const sk=f.bone[k]; if(sk.length===1){ const m=M[sk[0][0]]; return vadd(mV(m.R,p),m.t); }
        const o=[0,0,0]; for(const [bi,w] of sk){ const m=M[bi], q=vadd(mV(m.R,p),m.t); o[0]+=q[0]*w; o[1]+=q[1]*w; o[2]+=q[2]*w; } return o; });
      /* 10.1: the smooth normal rides the face's first bone (weighted where the face is blended) */
      let sn=null; if(f.sn){ const sk=f.bone[0]; if(sk.length===1) sn=mV(M[sk[0][0]].R, f.sn); else { sn=[0,0,0]; for(const [bi,w] of sk){ const q=mV(M[bi].R, f.sn); sn[0]+=q[0]*w; sn[1]+=q[1]*w; sn[2]+=q[2]*w; } sn=vnorm(sn); } }
      out.push({ v, mat:f.mat, b:f.b, db:f.db, part:f.part, group:f.group, minT:f.minT||0, side:f.side||0, pt:f.pt||0, oh:f.oh||0, az:f.az==null?null:f.az, sn, head:f.bone[0][0][0]===hIx, bx:f.v.reduce((s,p)=>s+p[0],0)/f.v.length }); }
    return out; }
  function paint(faces, C, MATS, o){ o=o||{};
    const Wd=C.W, Hd=C.H, N=Wd*Hd, zb=new Float32Array(N).fill(Infinity), dep=new Float32Array(N), mat=new Int16Array(N).fill(-1), stp=new Int8Array(N), fid=new Int32Array(N).fill(-1);
    const keysM=Object.keys(MATS), mIx={}; keysM.forEach((k,i)=>{ mIx[k]=i; });
    const K=SHADING.key, snap=o.snap||null, GR=o.grade||null, lt=new Uint8Array(N), pts=[];
    const nrmP=(P)=>{ let nx=0,ny=0,nz=0; for(let i=0;i<P.length;i++){ const a=P[i], c=P[(i+1)%P.length]; nx+=(a.yr-c.yr)*(a.zr+c.zr); ny+=(a.zr-c.zr)*(a.xr+c.xr); nz+=(a.xr-c.xr)*(a.yr+c.yr); }
      const nl=Math.hypot(nx,ny,nz)||1; return [nx/nl,ny/nl,nz/nl]; };
    const stepOf=(M,ux,uy,uz,b)=>{ if(M.fixed) return 0; const up=uy*C.se+uz*C.ce, tw=-uy*C.ce+uz*C.se, s=Math.round((ux*K[0]+up*K[1]+tw*K[2] + SHADING.form*(tw-SHADING.formMid))*TOL.shadeScale)/TOL.shadeScale;
      return Math.max(0, Math.min(M.ramp.length-1, clamp(Math.round(s*M.gain + M.bias + (b||0)), M.lo==null?0:M.lo, M.hi==null?99:M.hi) + (M.off||0))); };
    for(let fi=0; fi<faces.length; fi++){ const f=faces[fi]; if(f.pt){ pts.push(fi); continue; } const P=f.v.map(p=>proj(p,C));
      if(snap && f.head) for(const q of P){ q.sx+=snap[0]; q.sy+=snap[1]; }
      const [nx,ny,nz]=nrmP(P), toward=-ny*C.ce+nz*C.se; if(toward<=Math.max(TOL.cull, f.minT||0)) continue;
      /* 10.1: a face with a smooth normal is culled on its own normal and lit on the smooth one */
      const sv=f.sn ? rotC(f.sn,C) : null, ux=sv?sv[0]:nx, uy=sv?sv[1]:ny, uz=sv?sv[2]:nz;
      const M=MATS[f.mat]||MATS.skin, step=stepOf(M,ux,uy,uz,f.b), fl=GR&&GR.sunOn&&(ux*GR.L[0]+uy*GR.L[1]+uz*GR.L[2])>0.12?1:0;
      const mi=mIx[f.mat]!=null ? mIx[f.mat] : mIx.skin, db=f.db||0;
      for(let t=1;t+1<P.length;t++){ const a=P[0], bq=P[t], c=P[t+1];
        const area=(bq.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(bq.sy-a.sy); if(Math.abs(area)<TOL.area) continue;
        const x0=Math.max(0,Math.floor(Math.min(a.sx,bq.sx,c.sx))), x1=Math.min(Wd-1,Math.ceil(Math.max(a.sx,bq.sx,c.sx)));
        const y0=Math.max(0,Math.floor(Math.min(a.sy,bq.sy,c.sy))), y1=Math.min(Hd-1,Math.ceil(Math.max(a.sy,bq.sy,c.sy)));
        for(let y=y0;y<=y1;y++) for(let x=x0;x<=x1;x++){ const px=x+0.5, py=y+0.5;
          const w0=((bq.sx-px)*(c.sy-py)-(c.sx-px)*(bq.sy-py))/area, w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area, w2=1-w0-w1;
          if(w0<-TOL.inside||w1<-TOL.inside||w2<-TOL.inside) continue;
          const d=w0*a.d+w1*bq.d+w2*c.d, i=y*Wd+x, dq=Math.round((d-db)*TOL.depthScale)/TOL.depthScale; if(dq<zb[i]){ zb[i]=dq; dep[i]=d; mat[i]=mi; stp[i]=step; fid[i]=fi; lt[i]=fl; } } }
    }
    /* 10.1: the point marks (the face). Each draws the one pixel under its centre: over a pixel the head already covers (PT_UNDER), nearer
       by its depth bias, while the horizontal angle between its normal and the camera is within acos(az). A centre on a pixel edge draws
       nothing, so a mirrored head draws mirrored marks. */
    for(const fi of pts){ const f=faces[fi], P=f.v.map(p=>proj(p,C)); if(snap && f.head) for(const q of P){ q.sx+=snap[0]; q.sy+=snap[1]; }
      const [nx,ny,nz]=nrmP(P); if(-ny*C.ce+nz*C.se<=TOL.cull) continue; if(f.az!=null){ const hh=Math.hypot(nx,ny); if(hh<1e-6 || -ny/hh<=f.az) continue; }
      let cx=0, cy=0, cd=0; for(const q of P){ cx+=q.sx; cy+=q.sy; cd+=q.d; } cx/=P.length; cy/=P.length; cd/=P.length;
      if(Math.abs(cx-Math.round(cx))<TOL.markEdge || Math.abs(cy-Math.round(cy))<TOL.markEdge) continue;
      const X=Math.floor(cx), Y=Math.floor(cy); if(X<0||Y<0||X>=Wd||Y>=Hd) continue; const i=Y*Wd+X; if(fid[i]<0) continue; const up_=faces[fid[i]].part; if(!PT_UNDER[up_] && !(f.oh && up_==='hair')) continue;
      const dq=Math.round((cd-(f.db||0))*TOL.depthScale)/TOL.depthScale; if(dq>=zb[i]) continue; const M=MATS[f.mat]||MATS.skin;
      zb[i]=dq; dep[i]=cd; mat[i]=mIx[f.mat]!=null ? mIx[f.mat] : mIx.skin; stp[i]=stepOf(M,nx,ny,nz,f.b); fid[i]=fi; lt[i]=0; }
    /* inner contour: across a depth break the far pixel drops one step */
    const lit=stp.slice();
    if(o.edges!==false){ const drop=new Uint8Array(N);
      for(let y=0;y<Hd;y++) for(let x=0;x<Wd;x++){ const i=y*Wd+x; if(mat[i]<0) continue;
        for(const [dx,dy] of [[1,0],[0,1]]){ const X=x+dx, Y=y+dy; if(X>=Wd||Y>=Hd) continue; const j=Y*Wd+X; if(mat[j]<0) continue;
          if(Math.abs(dep[i]-dep[j])>SHADING.edge) drop[dep[i]>dep[j]?i:j]=1; } }
      for(let i=0;i<N;i++) if(drop[i] && !MATS[keysM[mat[i]]].fixed && stp[i]>0) stp[i]--; }
    const col=new Array(N).fill(null); for(let i=0;i<N;i++) if(mat[i]>=0){ const M=MATS[keysM[mat[i]]], n=M.ramp.length; let s=Math.min(n-1,stp[i]);
      if(GR){ if(!M.fixed) s=clamp(s+GR.db,0,n-1); col[i]=gradeHex(M.ramp[s], n>1 ? s/(n-1) : 0.5, lt[i], GR); } else col[i]=M.ramp[s]; }
    const out=col.slice();
    /* keyline: an empty pixel beside the figure takes the tint of its NEAREST filled neighbour (ties to the darker),
       a rule with no handedness, so a mirrored pose gets a mirrored outline */
    if(o.keyline===true) for(let y=0;y<Hd;y++) for(let x=0;x<Wd;x++){ const i=y*Wd+x; if(col[i]) continue; let src=null, sd=Infinity, sl=Infinity;
      for(const [dx,dy] of [[0,-1],[1,0],[-1,0],[0,1]]){ const X=x+dx, Y=y+dy; if(X<0||X>=Wd||Y<0||Y>=Hd) continue; const j=Y*Wd+X, c=col[j]; if(!c) continue;
        const lum=parseInt(c.slice(1,3),16)+parseInt(c.slice(3,5),16)+parseInt(c.slice(5,7),16);
        if(dep[j]<sd-TOL.tieDepth || (Math.abs(dep[j]-sd)<=TOL.tieDepth && lum<sl)){ src=c; sd=dep[j]; sl=lum; } }
      if(src) out[i]=mixHex(SHADING.keyline, src, SHADING.keylineMix); }
    const rgba=new Uint8ClampedArray(N*4);
    for(let i=0;i<N;i++){ const c=out[i]; if(!c) continue; rgba[i*4]=parseInt(c.slice(1,3),16); rgba[i*4+1]=parseInt(c.slice(3,5),16); rgba[i*4+2]=parseInt(c.slice(5,7),16); rgba[i*4+3]=255; }
    return { rgba, W:Wd, H:Hd, mat, step:stp, lit, face:fid, mats:keysM, faces }; }
  function paintSolved(S, B, o){ o=o||{}; const C=camOf(o), faces=posed(S,B,o.face); let snap=null;
    if(o.snapHead!==false){ const hq=proj(fkp(S.W[B.sk.ix.head], B.D.headMid), C); snap=[Math.round(hq.sx-0.5)+0.5-hq.sx, Math.round(hq.sy-0.5)+0.5-hq.sy]; }
    const GR=o.sky ? skyGrade(o.sky) : null, R=paint(faces, C, B.mats, { snap, keyline:o.keyline, edges:o.edges, grade:GR }); R.cam=C; R.snap=snap; R.grade=GR; return R; }
  function rockOf(o){ if(o.rock) return o.rock; if(!(o.roll||o.pitch)) return null; const c=o.counter==null?1:+o.counter; return c ? { roll:o.roll||0, pitch:o.pitch||0, counter:c } : null; }
  /* render({clip, frame | u, dir | yaw, build, opts, roll, pitch, counter, face, keyline, edges, px}) */
  function render(o){ o=o||{}; const B=buildOf(o.build), name=o.clip||'idle', cd=clipDef(name); if(!cd) throw new Error('characterIsoRig10: no clip "'+name+'"');
    const A=ANIMS[cd.anim], u = o.u!=null ? o.u : uOf(cd.anim, (((o.frame||0)%A.frames)+A.frames)%A.frames);
    const S=evalClip(name, u, B, o.opts, rockOf(o), o.look), R=paintSolved(S, B, o); R.S=S; return R; }
  /* ============================ 10.0: the terrain's light ============================
     sky = WeatherSky.at(...) or WeatherSky's preset result: sunF (east, south, up), sunI, skyI, expo, kc ka, ac aa amb, wash wa, fog fogC.
     The figure keeps its facet shading (authored at the afternoon key) and takes what open ground takes in the same weather: the band
     ladder (TerrainLight5.bandOff of an up-facing texel's light, against the afternoon's; never above it, at most two steps down), then
     TerrainLight5's grade: shadow tones toward ac by aa, light tones toward kc by ka (full on faces turned to the sun), wash, fog. */
  const bandOff5=(E)=>{ const r=E/0.995; return r>=1.25?1:r>=0.30?0:r>=0.14?-1:r>=0.06?-2:-3; };
  const E_NOON9 = 0.36*0.60 + Math.pow(Math.sin(55*DEG),1.25);
  function skyGrade(sky){ if(!sky) return null; const sF=sky.sunF||[0,0,1], sunI=sky.sunI||0, sunOn=sunI>0.01 && sF[2]>0.02;
    const E=(0.36*(sky.skyI==null?0.6:sky.skyI) + (sunOn ? sunI*Math.pow(Math.max(0,sF[2]),1.25) : 0))*(sky.expo||1);
    return { L:[sF[0],-sF[1],sF[2]], sunOn:sunOn && sunI>0.08, db:clamp(bandOff5(E)-bandOff5(E_NOON9),-2,0), E:+E.toFixed(3),
      kc:hex2(sky.kc||'#ffffff'), ac:hex2(sky.ac||'#000000'), wc:hex2(sky.wash||'#ffffff'), fc:hex2(sky.fogC||'#c3cdce'),
      ka:sky.ka||0, aa:sky.aa||0, amb:sky.amb||0, wa:sky.wa||0, fq:Math.min(4,(sky.fog||0)*4) }; }
  function gradeHex(c, u, lit, G){ const r=hex2(c), ta=G.aa*(1-u)*(1-G.amb*0.35), tk=G.ka*u*(lit?1:0.3);
    for(let k=0;k<3;k++){ r[k]+=(G.ac[k]-r[k])*ta; r[k]+=(G.kc[k]-r[k])*tk; if(G.wa) r[k]+=(G.wc[k]-r[k])*G.wa; if(G.fq) r[k]+=(G.fc[k]-r[k])*G.fq*0.17; }
    return '#'+r.map(v=>clamp(Math.round(v),0,255).toString(16).padStart(2,'0')).join(''); }
  /* THE CAST SHADOW on level ground through the pivot, from the sky's sun, and the contact patch round the feet (any weather).
     mask: 1 the sun's shadow, 2 the contact patch; cell pixels from (x0, y0), which may be negative (a low sun throws it far; maxLen m caps
     each point's throw). The engine sets the masked texels' sun visibility to 0 (TerrainLight5's vis), as a tree's cast shadow does. */
  function shadow(o){ o=o||{}; const B=buildOf(o.build), name=o.clip||'idle', cd=clipDef(name); if(!cd) throw new Error('characterIsoRig10: no clip "'+name+'"');
    const A=ANIMS[cd.anim], u = o.u!=null ? o.u : uOf(cd.anim, (((o.frame||0)%A.frames)+A.frames)%A.frames);
    const S=o.S||evalClip(name, u, B, o.opts, rockOf(o), o.look), C=camOf(o), faces=posed(S,B,o.face), sky=o.sky||null;
    const sun=!!(sky && (sky.sunI||0)>0.08 && sky.sunF && sky.sunF[2]>0.02), L=sun ? [sky.sunF[0],-sky.sunF[1],sky.sunF[2]] : null, tris=[];
    let x0=Infinity, x1=-Infinity, y0=Infinity, y1=-Infinity;
    const grow=(q)=>{ if(q[0]<x0)x0=q[0]; if(q[0]>x1)x1=q[0]; if(q[1]<y0)y0=q[1]; if(q[1]>y1)y1=q[1]; };
    if(sun){ const kx=-L[0]/L[2], ky=-L[1]/L[2], cap=o.maxLen||12, cl=(v)=>Math.max(-cap,Math.min(cap,v));
      for(const f of faces){ if(f.group) continue; const P=f.v.map(p=>{ const q=proj(p,C), z=Math.max(0,q.zr); return [C.cx+(q.xr+cl(kx*z))*C.S, C.cy-(q.yr+cl(ky*z))*C.se*C.S]; });
        for(let t=1;t+1<P.length;t++){ const T=[P[0],P[t],P[t+1]]; tris.push(T); T.forEach(grow); } } }
    const fp=['foot_L','foot_R'].map(id=>proj(fkp(S.W[B.sk.ix[id]], B.D.sole), C)), rx=(B.D.footX+0.12)*C.S, ry=0.17*C.se*C.S;
    const ex=(fp[0].sx+fp[1].sx)/2, ey=C.cy-((fp[0].yr+fp[1].yr)/2)*C.se*C.S; grow([ex-rx-1,ey-ry-1]); grow([ex+rx+1,ey+ry+1]);
    const X0=Math.floor(x0), Y0=Math.floor(y0), Wm=Math.ceil(x1)-X0+1, Hm=Math.ceil(y1)-Y0+1, mask=new Uint8Array(Wm*Hm);
    for(let y=0;y<Hm;y++) for(let x=0;x<Wm;x++){ const dx=(X0+x+0.5-ex)/rx, dy=(Y0+y+0.5-ey)/ry; if(dx*dx+dy*dy<=1) mask[y*Wm+x]=2; }
    for(const [a,b,c] of tris){ const area=(b[0]-a[0])*(c[1]-a[1])-(c[0]-a[0])*(b[1]-a[1]); if(Math.abs(area)<1e-9) continue;
      const bx0=Math.max(0,Math.floor(Math.min(a[0],b[0],c[0]))-X0), bx1=Math.min(Wm-1,Math.ceil(Math.max(a[0],b[0],c[0]))-X0);
      const by0=Math.max(0,Math.floor(Math.min(a[1],b[1],c[1]))-Y0), by1=Math.min(Hm-1,Math.ceil(Math.max(a[1],b[1],c[1]))-Y0);
      for(let y=by0;y<=by1;y++) for(let x=bx0;x<=bx1;x++){ const px=X0+x+0.5, py=Y0+y+0.5;
        const w0=((b[0]-px)*(c[1]-py)-(c[0]-px)*(b[1]-py))/area, w1=((c[0]-px)*(a[1]-py)-(a[0]-px)*(c[1]-py))/area, w2=1-w0-w1;
        if(w0>=-1e-6&&w1>=-1e-6&&w2>=-1e-6) mask[y*Wm+x]=1; } }
    return { mask, x0:X0, y0:Y0, W:Wm, H:Hm, sun, len_m: sun ? +(B.D.heightM*Math.hypot(L[0],L[1])/L[2]).toFixed(2) : 0 }; }
  const project=(p,o)=>{ const q=proj(p, camOf(o)); return { x:q.sx, y:q.sy, d:q.d }; };
  /* the build's hair region (THE HAIR REGION above), with its fringe and ear heights resolved */
  function hairline(build){ const B=buildOf(build), D=B.D, zcOf=(zb)=>1.31+(zb-D.eyeZ)/D.zs, fz=(zc)=>D.eyeZ-pOf(zc)*RS/D.kH;
    const HL=hairlineOf(B.b.hairStyle, Math.max(1.425, zcOf(fz(1.427))), zcOf(fz(EAR.top))); return HL ? Object.assign(HL, { earBottom:zcOf(fz(EAR.bottom)) }) : null; }
  const zcOfBuild=(B,z)=>1.31+((B.D.headZ+(z-B.D.headZ)/B.D.kH)-B.D.eyeZ)/B.D.zs;   /* 10.0: undo the head's scale first */

  const API = {
    rig:'characterIsoRig10', pass:10, revision:'10.3', PX, W, H, pivot:PIVOT, ELEV, order:ORDER, SHADING, DITHER, TOL, INK, INK_ROLES, EYE_WHITE, AIM, R7, BOTTOMS, garmentOf, BEARDS, randomBuild, CAST10, NPCS,
    ANIMS, ANIMS_SHIPPED, SEGMENTS, GROUPS, ANIM_MOUNT, CARRIES, CARRY_ORDER, CARRY_CLIPS, CONTRACT, BUILDS, CAST, READY, SOCKETS, HUMANOID,
    OPTIONS, FIELDS, AGES, SHAPE, SEX, FRAMES, frameOf, HEADS, GARMENTS, HATS, EYE_SHAPES, ROLES, PT_UNDER, FACE_EMPTY, HSTEP, normBuild, buildKey, dimsOf, kOf, zmOf,
    palettes:{ SKINS, HAIRS, OUTFITS, SHIRTS, HATCOLS, APRONS, EYES },
    GROUP_ORDER, FACE_SLOTS, ROCK_BONES, ROCK_W, FRAME_DOC, saddleOpts, colliderOf, blinkClip, lookContract, EAR, HAIRLINES, hairline, hairSide, hairBound, inHair, zcOf:zcOfBuild, BLINK, LOOK, lookAt, lookE,
    clipDef, clipNames, uOf, modeOf, buildOf, baseIntent, intentOf, solve, evalClip, rockDelta, counterLean, pinsOf, exportLocals,
    clip, clips, skeleton, bindMesh, gameplay, sockets, rockTable, shadingContract, sizes, exportBuild,
    camOf, proj, project, posed, paint, paintSolved, render, shadow, skyGrade, gradeHex, P10, RS, pOf, ROW9, ROWP, quatOf, matOf, qmul, eulerZYX, eul, fkAll,
    /* the pose library registers itself here (characterIsoRig10.poses.js) */
    definePoses(fn){ const lib=fn(LIB); POSES=lib.POSES; CARRY_STYLE=lib.CARRY_STYLE||{}; POST=lib.POST||{}; API.posesLoaded=true; API.poseNotes=lib.NOTES||{}; return API; },
    posesLoaded:false };
  const LIB = { DEG, TAU, I3, vadd, vsub, vmul, vdot, vcross, vlen, vdist, vnorm, vlerp, mV, mTV, mM, mTM, Rx, Ry, Rz, eul, aimM, aimTo, fk, fkp, localOf,
    clamp, clamp01, lerp, sm, seg, frac, sn, cs, mixv, bez, lin, keys, ftab, r4, hangC, baseIntent, bodyOf, blendIntent, CONTRACT, ANIMS, uOf, zmOf, kOf };
  root.CharacterIso10 = API;
})(typeof globalThis!=='undefined' ? globalThis : window);
