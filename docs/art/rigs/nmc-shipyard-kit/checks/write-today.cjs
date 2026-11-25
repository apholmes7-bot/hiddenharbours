'use strict';
const fs=require('fs'),path=require('path'),vm=require('vm'),crypto=require('crypto');
const KIT=path.resolve(__dirname,'..'),ROOT=path.resolve(KIT,'../../../..');
const SCRIPTS=['../nmc-set-pieces-kit/lib/weatherSky.js','../nmc-set-pieces-kit/lib/coastalPass.js',
  'wharfRig2.geo.js','wharfRig2.fam.js','wharfRig2.verbs.js','wharfRig2.js','wharfRig2.kit.js',
  'marinaRig.js','bowriderIsoRig.js','sportSkiffIsoRig.js','lobsterBoatVariantsIsoRig.js','capeIslanderIsoRig.js',
  'marinaBoats.js','shipyardRig2.js','nmcWharf.js','yard-layout.js','nmcShipyard.js'];
const context=vm.createContext({console});
for(const f of SCRIPTS)vm.runInContext(fs.readFileSync(path.join(KIT,f),'utf8'),context,{filename:f});
const SP=context.NmcShipyard,sha=b=>crypto.createHash('sha256').update(b).digest('hex');
const target=path.join(ROOT,'Assets/_Project/Art/Sprites/NineMileCreek/Yard/nmcShipyard.today.json');
function plan(w,h,count,cap){for(let cols=count;cols>=1;cols--)if(count%cols===0&&cols*w<=cap&&count/cols*h<=cap)return {cols,rows:count/cols,w:cols*w,h:count/cols*h};throw new Error('No sheet grid for '+w+' x '+h);}
function writeToday(){
 const pieces=SP.KEYS.map(key=>({key,id:SP.parts[key].id,scene:'keyscene.nmc_wharf',options:{},variants:[],gameplay:SP.gameplay(key,{})}));
 const plans=pieces.map(p=>{const w=Math.max(...p.gameplay.cells.map(c=>c.w)),h=Math.max(...p.gameplay.cells.map(c=>c.h));return {key:p.key,cell:[w,h],...plan(w,h,SP.FACINGS,SP.IMPORT_SIZE_CAP)};});
 const t={schema:'hidden-harbours/nmc-shipyard-today@1',kit:{folder:'docs/art/rigs/nmc-shipyard-kit',global:'NmcShipyard',version:SP.VERSION,
 scripts:SCRIPTS.map(f=>({path:f,sha256Lf:sha(Buffer.from(fs.readFileSync(path.join(KIT,f),'utf8').replace(/\r\n/g,'\n')))}))},
 bake:{sky:{time:14,cloud:0.1,wind:0.3},lit:0,outline:false,facings:SP.FACINGS,importSizeCap:SP.IMPORT_SIZE_CAP},leftOut:[],pieces};
 fs.mkdirSync(path.dirname(target),{recursive:true});fs.writeFileSync(target,JSON.stringify(t,null,1)+'\n');return {today:t,plans};
}
if(require.main===module){const r=writeToday();console.log('SHEET PLANS: PASS',r.plans.length);for(const p of r.plans)console.log(p.key,p.cols+'x'+p.rows,p.w+'x'+p.h);}
module.exports={KIT,ROOT,SCRIPTS,context,SP,plan,writeToday};
