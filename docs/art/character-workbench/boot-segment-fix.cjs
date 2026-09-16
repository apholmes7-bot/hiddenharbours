// Reviewed upstream correction, applied to the frozen preview snapshot only.
// Production adoption requires the actual Unity rebake and freshness/contact checks.
const before=`          const bz=P.ankleZ+G.bootZ*hS, t=(bz-a[2])/Math.max(0.001,(knee[2]-a[2]));
          top=[a[0]+(knee[0]-a[0])*t, a[1]+(knee[1]-a[1])*t, bz];`;
const after=`          const bz=P.ankleZ+G.bootZ*hS, dz=knee[2]-a[2], projected=(bz-a[2])/dz;
          // Keep the standing boot height when it intersects the shin. Raised/inverted
          // shins need a length-based fallback on the ankle-to-knee segment (all axes).
          const valid=dz>0.01 && projected>=0 && projected<=1;
          const t=valid ? projected : Math.min(1,G.bootZ*hS/(Math.hypot(knee[0]-a[0],knee[1]-a[1],dz)||1));
          top=[a[0]+(knee[0]-a[0])*t, a[1]+(knee[1]-a[1])*t, valid ? bz : a[2]+dz*t];`;
function applyBootSegmentCorrection(source){
  const normalized=source.replace(/\r\n/g,'\n');
  if(normalized.split(before).length!==2)throw Error('Boot correction expected one original two-line segment');
  return normalized.replace(before,after);
}
function productionPatch(){
  return 'diff --git a/docs/art/rigs/characterIsoRig7.js b/docs/art/rigs/characterIsoRig7.js\n--- a/docs/art/rigs/characterIsoRig7.js\n+++ b/docs/art/rigs/characterIsoRig7.js\n@@ -228,5 +228,9 @@\n         } else {\n'+before.split('\n').map(x=>'-'+x).join('\n')+'\n'+after.split('\n').map(x=>'+'+x).join('\n')+'\n         }\n         bootB=[a[0],a[1],a[2]+0.006];\n';
}
module.exports={applyBootSegmentCorrection,productionPatch};
if(require.main===module){require('fs').writeFileSync(require('path').join(__dirname,'production-boot-segment.patch'),productionPatch());}
