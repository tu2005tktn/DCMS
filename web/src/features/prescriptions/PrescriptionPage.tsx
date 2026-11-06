import { Alert, Space, Spin, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../../services/api';
import type { Workspace } from '../examinations/types';
import PrescriptionPanel from './PrescriptionPanel';

export default function PrescriptionPage() {
  const {visitId}=useParams();
  const [workspace,setWorkspace]=useState<Workspace>(),[error,setError]=useState<string>();
  const [dirty,setDirty]=useState(false),[busy,setBusy]=useState(false);
  useEffect(()=>{
    let active=true;setWorkspace(undefined);setError(undefined);
    if(visitId)void api<Workspace>(`/api/visits/${visitId}/examination`).then(value=>{if(active)setWorkspace(value);}).catch(e=>{if(active)setError((e as Error).message);});
    return ()=>{active=false;};
  },[visitId]);
  useEffect(()=>{
    const unload=(event:BeforeUnloadEvent)=>{if(dirty||busy)event.preventDefault();};
    const navigate=(event:MouseEvent)=>{if((dirty||busy)&&(event.target as HTMLElement).closest('a')&&(busy||!window.confirm('Rời đơn thuốc và bỏ phần nhập chưa lưu?'))){event.preventDefault();event.stopPropagation();}};
    window.addEventListener('beforeunload',unload);document.addEventListener('click',navigate,true);
    return ()=>{window.removeEventListener('beforeunload',unload);document.removeEventListener('click',navigate,true);};
  },[dirty,busy]);
  if(error)return <Alert type="error" message={error}/>;
  if(!workspace||workspace.visit.visitId!==visitId)return <Spin/>;
  const readOnly=workspace.visit.status!=='InProgress'||!workspace.allowedActions.includes('prescribe');
  return <main className="page"><Space wrap><Typography.Title level={2}>Đơn thuốc mua ngoài</Typography.Title><Link to={`/visits/${visitId}`}>Về lượt khám</Link></Space>
    <PrescriptionPanel key={visitId} visitId={visitId!} branchId={workspace.visit.branchId} readOnly={readOnly} initiallyExpanded onDirtyChange={setDirty} onBusyChange={setBusy}/>
  </main>;
}
