import { NodeIO } from '../../.artifacts/odontogram-asset-tools/node_modules/@gltf-transform/core/dist/index.js';
import { ALL_EXTENSIONS } from '../../.artifacts/odontogram-asset-tools/node_modules/@gltf-transform/extensions/dist/index.js';
import { cloneDocument, dedup, prune, simplify, transformMesh, weld } from '../../.artifacts/odontogram-asset-tools/node_modules/@gltf-transform/functions/dist/index.js';
import { MeshoptSimplifier } from '../../.artifacts/odontogram-asset-tools/node_modules/meshoptimizer/index.js';
import * as THREE from '../../web/node_modules/three/build/three.module.js';
import fs from 'node:fs/promises';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
process.chdir(fileURLToPath(new URL('../../.artifacts/odontogram-asset-tools/', import.meta.url)));
const manifest = JSON.parse(await fs.readFile('../odontogram-assets/source-manifest.json','utf8'));
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS);
const out='../../web/public/models/dental';
await fs.mkdir(out+'/arch',{recursive:true}); await fs.mkdir(out+'/detail',{recursive:true});
await MeshoptSimplifier.ready;
const widths={2:[1.05,.85,.95,.95,.95,1.2,1.15,1.05],3:[.65,.73,.85,.9,.95,1.23,1.18,1.1],6:[.8,.65,.72,.95,1.1],7:[.5,.6,.65,.9,1.02]};
const records=[];
for(const item of manifest.models){
 const d=await io.read('../odontogram-assets/original/'+item.name+'.glb'); const root=d.getRoot();
 const quadrant=Math.floor(item.template/10),n=item.template%10;
 const upper=quadrant===2||quadrant===6, primary=quadrant>4;
 const sign=upper ? -1 : 1;
 const crownFraction=primary ? (n===3?.33:n>=4?.38:.4) : (n===3?.42:n>=4?.4:.44);
 const yaw=primary?0:[0,.12,.4,.7,.9,1.12,1.3,1.5][n-1];
 const rotate=new THREE.Matrix4().makeRotationY(-yaw);
 const objects=root.listNodes().filter(node=>node.getMesh()).map(node=>({mesh:node.getMesh(),world:new THREE.Matrix4().fromArray(node.getWorldMatrix())}));
 const points=[];
 for(const obj of objects) for(const prim of obj.mesh.listPrimitives()){
  const a=prim.getAttribute('POSITION').getArray();
  const matrix=rotate.clone().multiply(obj.world);
  for(let i=0;i<a.length;i+=3)points.push(new THREE.Vector3(a[i],a[i+1],a[i+2]).applyMatrix4(matrix));
 }
 const bounds=new THREE.Box3().setFromPoints(points),height=bounds.max.y-bounds.min.y;
 const neck=upper?bounds.min.y+height*crownFraction:bounds.max.y-height*crownFraction;
 const crownPoints=points.filter(p=>upper?p.y<neck:p.y>neck);
 const crownBounds=new THREE.Box3().setFromPoints(crownPoints);
 const width=widths[quadrant][n-1];const scale=width/(crownBounds.max.x-crownBounds.min.x);
 const center=crownBounds.getCenter(new THREE.Vector3());
 const transform=new THREE.Matrix4().makeScale(scale,scale,scale).multiply(new THREE.Matrix4().makeTranslation(-center.x,-neck,-center.z)).multiply(rotate);
 for(const obj of objects)transformMesh(obj.mesh,transform.clone().multiply(obj.world).elements);
 for(const node of [...root.listNodes()])node.dispose();for(const scene of [...root.listScenes()])scene.dispose();
 const scene=d.createScene();for(const [i,obj] of objects.entries())scene.addChild(d.createNode('Tooth_'+item.template+'_'+i).setMesh(obj.mesh));
 for(const mesh of root.listMeshes())for(const prim of mesh.listPrimitives()){
   prim.setAttribute('TANGENT',null); prim.setAttribute('TEXCOORD_0',null); prim.setAttribute('COLOR_0',null);
   const positions=prim.getAttribute('POSITION').getArray(); const colors=new Float32Array(positions.length);
   for(let i=0;i<positions.length;i+=3){
     const rootShade=Math.min(1,Math.max(0,-sign*positions[i+1]*2));
     const c=new THREE.Color().setRGB(.965-.12*rootShade,.948-.14*rootShade,.89-.17*rootShade,THREE.SRGBColorSpace);
     colors[i]=c.r;colors[i+1]=c.g;colors[i+2]=c.b;
   }
   prim.setAttribute('COLOR_0',d.createAccessor().setType('VEC3').setArray(colors).setBuffer(root.listBuffers()[0]));
 }
 for(const texture of [...root.listTextures()])texture.dispose();
 for(const material of root.listMaterials())material.setBaseColorFactor([1,1,1,1]).setMetallicFactor(0).setRoughnessFactor(.33);
 await d.transform(dedup(),prune(),weld());
 const arch=cloneDocument(d);
 for(const mesh of arch.getRoot().listMeshes())for(const prim of mesh.listPrimitives()){
  const pos=prim.getAttribute('POSITION').getArray(),indices=prim.getIndices(),a=indices.getArray(),kept=[];
  for(let i=0;i<a.length;i+=3){if([a[i],a[i+1],a[i+2]].some(index=>sign*pos[index*3+1]>=-.12))kept.push(a[i],a[i+1],a[i+2]);}
  indices.setArray(new Uint32Array(kept));
 }
 const count=doc=>doc.getRoot().listMeshes().reduce((sum,m)=>sum+m.listPrimitives().reduce((v,p)=>v+p.getIndices().getCount()/3,0),0);
 await d.transform(simplify({simplifier:MeshoptSimplifier,ratio:Math.min(1,60000/count(d)),error:.00015}),prune());
 await arch.transform(simplify({simplifier:MeshoptSimplifier,ratio:Math.min(1,9000/count(arch)),error:.001}),prune());
 const paths={arch:'arch/'+item.template+'.glb',detail:'detail/'+item.template+'.glb'};
 for(const [lod,path] of Object.entries(paths)) await io.write(out+'/'+path,lod==='arch'?arch:d);
 const record={...item,width,crownFraction,normalizationYaw:yaw,archTriangles:count(arch),detailTriangles:count(d),paths};
 for(const [lod,path] of Object.entries(paths)){const b=await fs.readFile(out+'/'+path);record[lod+'Bytes']=b.length;record[lod+'Sha256']=crypto.createHash('sha256').update(b).digest('hex');}
 records.push(record);console.log(item.template,record.archTriangles,record.detailTriangles,record.archBytes,record.detailBytes);
}
await fs.writeFile(out+'/manifest.json',JSON.stringify({repository:manifest.repository,adaptations:'World transforms baked; canonical cervical origin; uniform illustrative scale; orientation; separate crown LOD and full-tooth LOD; simplification; textures removed; illustrative enamel/root vertex colors. Opposite side mirrored by DCMS. Not patient data.',models:records},null,2));
console.log('MODELS_READY',records.length);
