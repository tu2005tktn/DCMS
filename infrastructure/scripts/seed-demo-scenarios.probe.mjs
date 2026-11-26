import fs from 'node:fs';
const env = Object.fromEntries(fs.readFileSync('.env','utf8').split(/\r?\n/).filter(l=>l&&!l.startsWith('#')&&l.includes('=')).map(l=>{const i=l.indexOf('=');return [l.slice(0,i).trim(),l.slice(i+1).trim().replace(/^"|"$/g,'')]}));
const api='http://localhost:5080';
async function login(u){const r=await fetch(api+'/api/auth/sign-in',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({userName:u,password:env.DEV_SEED_PASSWORD})});return {Authorization:'Bearer '+(await r.json()).accessToken,'content-type':'application/json'};}
const a=await login('assistant.demo'), d=await login('dentist.demo');
for (const [h,p] of [[a,'/api/patients?search=DEMO&pageSize=3'],[d,'/api/prescriptions/medicine-options?branchId=1'],[d,'/api/prescriptions/patients/38/context']]) {
  const x=await fetch(api+p,{headers:h}); console.log(p,x.status,(await x.text()).slice(0,700));
}
