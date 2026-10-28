import { useEffect, useState } from 'react';
import { api } from '../../services/api';
import type { ReferenceSnapshot } from './diagnosisReferences';

export type DiagnosisOption = { value: string; label: string; reference?: ReferenceSnapshot };
export function useDiagnosisOptions(branchId?: number) {
  const [options,setOptions]=useState<DiagnosisOption[]>([]),[error,setError]=useState<string>(),[revision,setRevision]=useState(0);
  useEffect(()=>{
    if(!branchId)return;let active=true;
    api<ReferenceSnapshot[]>(`/api/reference-options?kind=diagnoses&context=examination&branchId=${branchId}`).then(rows=>{
      if(active){setOptions([{value:'Chưa phát hiện bất thường',label:'Chưa phát hiện bất thường'},...rows.map(row=>({value:row.name,label:row.code?`${row.name} (${row.code})`:row.name,reference:{id:row.id,version:row.version,name:row.name,code:row.code}}))]);setError(undefined);}
    }).catch(e=>{if(active)setError((e as Error).message);});return()=>{active=false;};
  },[branchId,revision]);
  return {options,error,reload:()=>setRevision(value=>value+1)};
}
