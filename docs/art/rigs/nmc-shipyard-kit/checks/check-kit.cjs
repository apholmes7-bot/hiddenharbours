'use strict';
const fs=require('fs'),crypto=require('crypto'),path=require('path');
const {SP,KIT,plan,writeToday,context}=require('./write-today.cjs');
let rows=0,bad=0;function check(ok,name){rows++;if(!ok)bad++;console.log((ok?'PASS ':'FAIL ')+name);}
const rig=fs.readFileSync(path.join(KIT,'shipyardRig2.js'));
check(rig.length===61024&&crypto.createHash('sha256').update(rig).digest('hex')==='f228320e85e8045d73a56058aa1ff82723320ff33d62a04c99e7986228eb3fb3','the delivered rig bytes');
check(SP.KEYS.join(',')==='yardFront,pierW,pierE,lift,slab,hardA,hardB,hardC,shed,floodW,floodE,fence,props','all thirteen module ids');
const sky=context.WeatherSky.at({time:14,cloud:0.1,wind:0.3});
const receipt=[];
for(const key of SP.KEYS){
 const g=SP.gameplay(key,{}),w=Math.max(...g.cells.map(c=>c.w)),h=Math.max(...g.cells.map(c=>c.h));
 check(!!plan(w,h,8,SP.IMPORT_SIZE_CAP),key+' sheet grid');
 check(!g.records.some(r=>r.missing),key+' has every required hull');
 for(let d=0;d<8;d++){
  const r=SP.render(key,d,{sky,lit:0,outline:false}),c=g.cells[d];
  check(r.w===c.w&&r.h===c.h&&r.px===c.pivot[0]&&r.py===c.pivot[1]&&r.rgba.length===r.w*r.h*4,key+' dir '+d+' native cell');
  receipt.push({key,dir:d,w:r.w,h:r.h,pivot:[r.px,r.py],sha256:crypto.createHash('sha256').update(r.rgba).digest('hex')});
 }
}
if(process.argv[2])fs.writeFileSync(process.argv[2],JSON.stringify(receipt,null,2)+'\n');
console.log('RESULT: '+(bad?'FAIL':'PASS')+' '+(rows-bad)+' of '+rows);process.exitCode=bad?1:0;
