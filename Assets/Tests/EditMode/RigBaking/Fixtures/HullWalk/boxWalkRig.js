(function(root) {
  const ids={hull:0,deck:1,room:2,top:3}, roles={wall:0,roof:1,sole:2};
  const face=(lv,ro,z,it=false)=>({v:[[-1,-2,z],[1,-2,z],[1,2,z],[-1,2,z]],mat:it?'lining':'paint',b:0.125,db:0.03125,lv,ro,it});
  const door={id:'entry',kind:'slide',face:'aft',y:-2,x0:-0.5,x1:0.5,z0:1,z1:2,sillZ:1,leaf:'doorFaces(t)',lv:'room',ro:'wall',clear:{width:1,height:1},opensTo:'starboard',clearAt:1,travel:1};
  const geometry=()=>({schema:'hidden-harbours/hull-geometry@1',rig:'boxWalkRig.js',exportSymbol:'BoxWalk',hull:'box_walk',units:'metres',frame:'+x starboard, +y bow, +z up',ids,roles,ghostRoles:['wall'],dwlZ:0,
    levels:[{id:'deck',deck:'deck',sole:0,ceil:null,kind:'open'},{id:'room',deck:'room',sole:1,ceil:2,kind:'hard'},{id:'top',deck:'top',sole:2,ceil:3,kind:'hard'}],
    cutaway:{gate:{deck:[],room:['top','room:wall','room:roof'],top:['top:roof']},ghost:{deck:[],room:['top','room:roof'],top:['top:roof']},shows:{deck:null,room:'room',top:'top'}},door,parts:[],states:{}});
  root.BoxWalk={id:'box_walk',W:64,H:64,PX:8,pivot:{x:32,y:48},defaultElev:30,GAIN:4,BIAS:0,LN:[0,0,1],KEY:'#000000',
    faceList:()=>[face(0,0,-1),face(1,2,0),face(2,0,1),face(2,1,2),face(3,1,3)],
    interiorFaces:()=>[face(2,2,1,true),face(3,2,2,true)],
    doorFaces:t=>{const f=face(2,0,1);f.v=f.v.map(v=>[v[0]+t,v[1],v[2]]);return [f];},geometry,
    matsFor:()=>({MATS:{paint:{ramp:['#112233','#445566'],off:0,dith:1},lining:{ramp:['#778899','#aabbcc'],off:0,dith:1}}}),
    ROCK:{frames:1,rollA:0,pitchA:0,heaveA:0,period:1}};
})(globalThis);
