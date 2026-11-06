import concurrent.futures,hashlib,json,struct,subprocess
from pathlib import Path
root=Path('.artifacts/odontogram-assets/original');root.mkdir(parents=True,exist_ok=True)
base='https://raw.githubusercontent.com/sameralseedbth-Dental/dental-3d-models/b8f428bd81a19b7041e3aad149e7dac7f38fd269/'
upper=['maxillary_left_central_incisor','maxillary_lateral_incisor','maxillary_canine','maxillary_first_premolar','maxillary_second_premolar','maxillary_first_molar','maxillary_second_molar','maxillary_third_molar']
lower=['mandibular_left_central_incisor','mandibular_left_lateral_incisor','mandibular_left_canine','mandibular_first_premolar','mandibular_left_second_premolar','mandibular_first_molar','mandibular_second_molar','mandibular_third_molar']
primaryUpper=['primary_upper_left_central_incisor_fdi_61','primary_upper_left_lateral_incisor_fdi_62','primary_upper_left_canine_fdi_63','primary_upper_left_first_molar_fdi_64','primary_upper_left_second_molar_fdi_65']
primaryLower=['primary_lower_left_central_incisor_fdi_71','primary_lower_left_lateral_incisor_fdi_72','primary_lower_left_canine_fdi_73','primary_lower_left_first_molar_fdi_74','primary_lower_left_second_molar_fdi_75']
models=[dict(name=name,template=basefdi+i) for basefdi,names in [(21,upper),(31,lower),(61,primaryUpper),(71,primaryLower)] for i,name in enumerate(names)]
def fetch(item):
 name=item['name'];p=root/(name+'.glb')
 if not p.exists():subprocess.run(['curl.exe','--fail','--silent','--show-error',base+name+'.glb','--output',str(p)],check=True)
 b=p.read_bytes();n=struct.unpack_from('<I',b,12)[0];j=json.loads(b[20:20+n]);extra=j['asset']['extras'];source=extra['source'];uid=source.rsplit('-',1)[-1]
 meta=json.loads(subprocess.check_output(['curl.exe','--fail','--silent','--show-error','https://api.sketchfab.com/v3/models/'+uid]))
 assert meta['license']['slug']=='by' and 'by/4.0' in meta['license']['url'],(name,meta['license'])
 assert extra['license'].startswith('CC-BY-4.0'),(name,extra)
 result={**item,'source':source,'author':extra['author'],'license':meta['license']['url'],'sourceSha256':hashlib.sha256(b).hexdigest(),'sourceBytes':len(b),'sourceTriangles':sum(j['accessors'][pr['indices']]['count']//3 for m in j['meshes'] for pr in m['primitives'])}
 print(name,result['sourceTriangles'],len(b),flush=True);return result
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool: results=list(pool.map(fetch,models))
Path('.artifacts/odontogram-assets/source-manifest.json').write_text(json.dumps({'repository':base,'models':results},indent=2),encoding='utf-8')
print('LICENSED_MODELS',len(results),'BYTES',sum(x['sourceBytes'] for x in results),flush=True)
