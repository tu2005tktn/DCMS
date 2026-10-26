import { Alert, Button, Card, Empty, Form, Input, Modal, Select, Skeleton, Space, Tag, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api, type ApiError } from '../../services/api';
import { useAuth } from '../authentication/AuthContext';
import type { AppointmentOptions, DossierPage, TreatmentChange } from './types';
import { stateLabel } from './types';

type Values = { branchId: number; dentistId: number; diagnosis: string; note?: string };

export default function DossierList({ patientId }: { patientId: number }) {
  const auth = useAuth();
  const navigate = useNavigate();
  const [page, setPage] = useState<DossierPage | null>(null);
  const [options, setOptions] = useState<AppointmentOptions | null>(null);
  const [error, setError] = useState<ApiError | null>(null);
  const [revision, setRevision] = useState(0);
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [form] = Form.useForm<Values>();
  const createKey = useRef(crypto.randomUUID());
  const branchId = Form.useWatch('branchId', form);
  const canUse = auth.user?.effectiveRoles.includes('Dentist') ?? false;

  useEffect(() => {
    if (!canUse) return;
    let active = true;
    setPage(null); setError(null);
    Promise.all([api<DossierPage>(`/api/patients/${patientId}/dossiers?page=1&pageSize=20`),
      api<AppointmentOptions>('/api/appointments/options')])
      .then(([result, available]) => { if (active) { setPage(result); setOptions(available); } })
      .catch(failure => { if (active) setError(failure as ApiError); });
    return () => { active = false; };
  }, [patientId, revision, canUse]);

  async function create(values: Values) {
    if (busy) return;
    setBusy(true); setSaveError(null);
    try {
      const result = await api<TreatmentChange>(`/api/patients/${patientId}/dossiers`, {
        method: 'POST', body: JSON.stringify({ ...values, requestKey: createKey.current }),
      });
      setOpen(false); form.resetFields(); createKey.current = crypto.randomUUID();
      navigate(`/patients/${patientId}/dossiers/${result.dossier.id}`);
    } catch (failure) { setSaveError((failure as Error).message); }
    finally { setBusy(false); }
  }

  if (!canUse) return <Alert type="info" showIcon message="Hồ sơ điều trị chỉ dành cho bác sĩ được phân quyền." />;
  return <section className="treatment-list" aria-label="Các đợt điều trị">
    <div className="treatment-section-heading"><div>
      <Typography.Title level={4}>Các đợt điều trị</Typography.Title>
      <Typography.Paragraph type="secondary">Mỗi đợt có kế hoạch, lượt khám và tiến độ riêng.</Typography.Paragraph>
    </div><Button type="primary" onClick={() => { setSaveError(null); createKey.current = crypto.randomUUID(); setOpen(true); }}>Tạo đợt điều trị</Button></div>
    {error ? <Alert type="error" showIcon message={error.message}
      action={<Button onClick={() => setRevision(value => value + 1)}>Thử lại</Button>} />
      : !page ? <Skeleton active /> : page.items.length === 0
        ? <Empty description="Chưa có đợt điều trị. Bác sĩ có thể tạo đợt mới cho bệnh nhân này." />
        : <div className="treatment-list-cards">{page.items.map(item => <Card key={item.id} size="small">
          <div className="treatment-list-row"><div><Typography.Text strong>{item.code}</Typography.Text>
            <div>{item.diagnosis}</div><Typography.Text type="secondary">Kế hoạch phiên bản {item.planVersion || 'chưa có'}</Typography.Text></div>
            <Space><Tag>{stateLabel[item.state]}</Tag><Link to={`/patients/${patientId}/dossiers/${item.id}`}>Mở hồ sơ</Link></Space></div>
        </Card>)}</div>}
    <Modal open={open} title="Tạo đợt điều trị" okText="Tạo hồ sơ" okButtonProps={{ loading: busy }}
      onOk={() => form.submit()} onCancel={() => { if (!busy) { setOpen(false); createKey.current = crypto.randomUUID(); } }}>
      <Typography.Paragraph>Đợt điều trị có thể kéo dài nhiều buổi. Tạo hồ sơ chưa tạo lịch hẹn hoặc khoản thu.</Typography.Paragraph>
      {saveError && <Alert type="error" showIcon className="form-alert" message={saveError} />}
      <Form form={form} layout="vertical" onValuesChange={() => { createKey.current = crypto.randomUUID(); }}
        onFinish={values => void create(values)}>
        <Form.Item name="branchId" label="Chi nhánh" rules={[{ required: true, message: 'Chọn chi nhánh.' }]}>
          <Select options={options?.branches.map(x => ({ value: x.id, label: x.name })) ?? []} />
        </Form.Item>
        <Form.Item name="dentistId" label="Bác sĩ phụ trách" rules={[{ required: true, message: 'Chọn bác sĩ.' }]}>
          <Select disabled={!branchId} options={options?.dentists.filter(x => x.branchId === branchId)
            .map(x => ({ value: x.id, label: x.name })) ?? []} />
        </Form.Item>
        <Form.Item name="diagnosis" label="Chẩn đoán hoặc lý do điều trị" rules={[
          { required: true, whitespace: true, message: 'Nhập chẩn đoán hoặc lý do.' },
          { max: 4000, message: 'Không quá 4000 ký tự.' },
        ]}><Input.TextArea rows={3} maxLength={4000} /></Form.Item>
        <Form.Item name="note" label="Ghi chú" rules={[{ max: 4000, message: 'Không quá 4000 ký tự.' }]}>
          <Input.TextArea rows={2} maxLength={4000} /></Form.Item>
      </Form>
    </Modal>
  </section>;
}
