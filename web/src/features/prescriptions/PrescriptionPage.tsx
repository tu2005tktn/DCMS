import { Alert, Card, Descriptions, Spin, Tag, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router-dom';
import { api } from '../../services/api';
import { useAuth } from '../authentication/AuthContext';
import { usePermissions } from '../authentication/usePermissions';
import type { Workspace } from '../examinations/types';
import { diagnosisText } from '../examinations/types';
import { formatDate } from '../appointments/types';
import { genderLabels } from '../patients/types';
import type { TreatmentDossier } from '../treatment/types';
import PrescriptionPanel from './PrescriptionPanel';
import './prescriptionPage.css';

export default function PrescriptionPage() {
  const {visitId}=useParams();
  const [searchParams]=useSearchParams();
  const has=usePermissions();
  const {user}=useAuth();
  const [workspace,setWorkspace]=useState<Workspace>(),[error,setError]=useState<string>();
  const [treatmentDiagnosis,setTreatmentDiagnosis]=useState('');
  const [dirty,setDirty]=useState(false),[busy,setBusy]=useState(false);
  useEffect(()=>{
    let active=true;setWorkspace(undefined);setError(undefined);
    if(visitId)void api<Workspace>(`/api/visits/${visitId}/examination`).then(value=>{if(active)setWorkspace(value);}).catch(e=>{if(active)setError((e as Error).message);});
    return ()=>{active=false;};
  },[visitId]);
  useEffect(()=>{
    let active=true;
    const dossierId=searchParams.get('sourceDossier');
    setTreatmentDiagnosis('');
    if(dossierId)void api<TreatmentDossier>(`/api/dossiers/${encodeURIComponent(dossierId)}`).then(value=>{
      if(active&&value.patientId===workspace?.patient.id&&value.branchId===workspace.visit.branchId)setTreatmentDiagnosis(value.diagnosis);
    }).catch(()=>undefined);
    return ()=>{active=false;};
  },[searchParams,workspace?.patient.id,workspace?.visit.branchId]);
  useEffect(()=>{
    const unload=(event:BeforeUnloadEvent)=>{if(dirty||busy)event.preventDefault();};
    const navigate=(event:MouseEvent)=>{if((dirty||busy)&&(event.target as HTMLElement).closest('a')&&(busy||!window.confirm('Rời đơn thuốc và bỏ phần nhập chưa lưu?'))){event.preventDefault();event.stopPropagation();}};
    window.addEventListener('beforeunload',unload);document.addEventListener('click',navigate,true);
    return ()=>{window.removeEventListener('beforeunload',unload);document.removeEventListener('click',navigate,true);};
  },[dirty,busy]);
  if(error)return <Alert type="error" message={error}/>;
  if(!workspace||workspace.visit.visitId!==visitId)return <Spin/>;
  const readOnly=workspace.visit.status!=='InProgress'||!workspace.allowedActions.includes('prescribe');
  const correctionAllowed=has('prescriptions.manage')&&(workspace.visit.status==='InProgress'||workspace.visit.status==='Completed');
  const patient=workspace.patient;
  const diagnoses=treatmentDiagnosis||diagnosisText(workspace.examination?.content ?? workspace.initialContent)||workspace.examination?.content.diagnosis||'';
  const prescribedOn=new Intl.DateTimeFormat('vi-VN',{timeZone:'Asia/Ho_Chi_Minh'}).format(new Date());
  return <main className="page prescription-page"><div className="prescription-page-heading">
    <div><Tag color="blue">KÊ ĐƠN THEO LƯỢT KHÁM</Tag><Typography.Title level={2}>Đơn thuốc mua ngoài</Typography.Title>
      <Typography.Text type="secondary">Đơn thuốc được lập sau khi bác sĩ khám hoặc điều trị cho bệnh nhân.</Typography.Text></div>
    <Link to={`/visits/${visitId}`}>Về lượt khám</Link>
  </div>
  <Card className="prescription-patient-card" size="small">
    <div className="prescription-patient-heading"><div><Typography.Text type="secondary">BỆNH NHÂN</Typography.Text>
      <Typography.Title level={4}>{patient.patientCode} · {patient.fullName}</Typography.Title></div>
      <Tag color={readOnly?'default':'processing'}>{readOnly?'CHỈ XEM':'ĐANG KÊ ĐƠN'}</Tag></div>
    <Descriptions size="small" column={{xs:1,sm:2,md:3}} items={[
      {label:'Ngày sinh',children:patient.birthDate?formatDate(patient.birthDate):'Chưa có'},
      {label:'Giới tính',children:patient.gender?genderLabels[patient.gender]:'Chưa rõ'},
      {label:'Chẩn đoán',children:diagnoses||'Chưa ghi nhận trong phiếu khám'},
      {label:'Ngày kê đơn',children:prescribedOn},
      {label:'Bác sĩ kê đơn',children:workspace.visit.dentistName||user?.fullName||'Bác sĩ phụ trách'},
    ]}/>
  </Card>
  {readOnly&&<Alert className="prescription-readonly-note" type="info" showIcon message="Lượt khám không còn ở trạng thái kê đơn mới. Đơn hiện có được mở ở chế độ xem; đơn đã ký có thể tạo bản điều chỉnh theo quyền."/>}
    <PrescriptionPanel key={visitId} visitId={visitId!} branchId={workspace.visit.branchId} readOnly={readOnly} correctionAllowed={correctionAllowed} initiallyExpanded onDirtyChange={setDirty} onBusyChange={setBusy}/>
  </main>;
}
