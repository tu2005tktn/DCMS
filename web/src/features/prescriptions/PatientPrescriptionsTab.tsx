import { Alert, Button, Card, DatePicker, Empty, Form, Input, InputNumber, Modal, Select, Space, Spin, Table, Typography, message } from 'antd';
import dayjs from 'dayjs';
import { useEffect, useState } from 'react';
import { api } from '../../services/api';
import { useAuth } from '../authentication/AuthContext';
import { formatDate } from '../patient-links/types';
import type { PatientDetail } from '../patients/types';

type Medicine = { id: string; code: string; name: string; strength?: string; baseUnit: string; packaging?: string; defaultInstructions?: string };
type Line = { medicineId: string; quantity: number; instructions?: string; unit?: string };
type Prescription = { id: string; patientId: number; branchId: number; prescribedOn: string; diagnosis: string; otherDiagnosis?: string; advice?: string; prescriberName: string; version: number; items: (Line & { id: string; medicineName: string; medicineCode: string; strength?: string; dosageForm?: string })[] };
type FormValues = { branchId: number; prescribedOn: dayjs.Dayjs; diagnosis: string; otherDiagnosis?: string; advice?: string; items: Line[] };

export default function PatientPrescriptionsTab({ patient, active }: { patient: PatientDetail; active: boolean }) {
  const auth = useAuth();
  const canManage = auth.user?.permissions.includes('prescriptions.manage') ?? false;
  const [context, setContext] = useState<{ branchIds: number[]; prescriberName: string }>();
  const [form] = Form.useForm<FormValues>();
  const branchId = Form.useWatch('branchId', form);
  const [rows, setRows] = useState<Prescription[]>([]);
  const [medicines, setMedicines] = useState<Medicine[]>([]);
  const [selected, setSelected] = useState<Prescription | null>(null);
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string>();
  const [notice, holder] = message.useMessage();

  async function refresh() {
    setLoading(true); setError(undefined);
    try { setRows(await api<Prescription[]>(`/api/prescriptions/patients/${patient.id}`)); }
    catch (e) { setError((e as Error).message); }
    finally { setLoading(false); }
  }
  useEffect(() => { if (active) void refresh(); }, [active, patient.id]);
  useEffect(() => { if (active && canManage) void api<{ branchIds: number[]; prescriberName: string }>(`/api/prescriptions/patients/${patient.id}/context`).then(setContext).catch(e => setError((e as Error).message)); }, [active, canManage, patient.id]);
  useEffect(() => {
    if (!open || !branchId) return;
    void api<Medicine[]>(`/api/prescriptions/medicine-options?branchId=${branchId}`).then(setMedicines).catch(e => setError((e as Error).message));
  }, [open, branchId]);
  function show(doc: Prescription | null) {
    setSelected(doc); setError(undefined); setOpen(true);
    form.setFieldsValue(doc ? {
      branchId: doc.branchId, prescribedOn: dayjs(doc.prescribedOn), diagnosis: doc.diagnosis, otherDiagnosis: doc.otherDiagnosis,
      advice: doc.advice, items: doc.items.map(i => ({ medicineId: i.medicineId, quantity: i.quantity, instructions: i.instructions, unit: i.unit })),
    } : { branchId: context?.branchIds[0], prescribedOn: dayjs(), diagnosis: '', otherDiagnosis: '', advice: '', items: [{ medicineId: '', quantity: 1, instructions: '' }] });
  }
  async function save(values: FormValues) {
    setBusy(true); setError(undefined);
    try {
      if (!values.branchId) throw new Error('Tài khoản chưa được phân công bác sĩ kê đơn tại chi nhánh.');
      await api(`/api/prescriptions/patients/${patient.id}${selected ? `/${selected.id}` : ''}`, {
        method: selected ? 'PUT' : 'POST', body: JSON.stringify({ requestKey: crypto.randomUUID(), expectedVersion: selected?.version ?? 0,
          branchId: values.branchId, prescribedOn: values.prescribedOn.format('YYYY-MM-DD'),
          diagnosis: values.diagnosis, otherDiagnosis: values.otherDiagnosis, advice: values.advice,
          items: values.items.map(x => ({ medicineId: x.medicineId, quantity: x.quantity, instructions: x.instructions })) }),
      });
      setOpen(false); void notice.success('Đã lưu đơn thuốc'); await refresh();
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }
  return <Card title="Đơn thuốc" extra={canManage && <Button type="primary" onClick={() => show(null)}>+ Kê đơn thuốc</Button>}>
    {holder}
    {error && !open && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} />}
    {loading ? <Spin /> : rows.length ? <Table rowKey="id" dataSource={rows} pagination={{ pageSize: 10 }} columns={[
      { title: 'Ngày', dataIndex: 'prescribedOn', render: (v: string) => formatDate(v) },
      { title: 'Chẩn đoán', dataIndex: 'diagnosis' },
      { title: 'Mã đơn thuốc', dataIndex: 'id', render: (v: string) => v.slice(0, 8).toUpperCase() },
      { title: 'Bác sĩ', dataIndex: 'prescriberName' },
      { title: '', render: (_, row) => <Button onClick={() => show(row)}>Xem đơn</Button> },
    ]} /> : <Empty description="Chưa có đơn thuốc trong hồ sơ." />}
    <Modal open={open} width={960} title="ĐƠN THUỐC" onCancel={() => setOpen(false)} onOk={() => form.submit()}
      okText="Lưu lại" okButtonProps={{ disabled: !canManage }} confirmLoading={busy} destroyOnClose>
      {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} />}
      <Typography.Title level={5}>KHÁCH HÀNG</Typography.Title>
      <Space wrap size="large"><span>Họ và tên: <b>{patient.fullName}</b></span><span>MSKH: <b>{patient.patientCode}</b></span>
        <span>Ngày sinh: <b>{patient.birthDate ? formatDate(patient.birthDate) : '—'}</b></span><span>Giới tính: <b>{patient.gender ?? '—'}</b></span></Space>
      <Form form={form} layout="vertical" onFinish={values => void save(values)} style={{ marginTop: 24 }} disabled={!canManage}>
        <Form.Item name="branchId" label="Chi nhánh" rules={[{ required: true, message: 'Chọn chi nhánh' }]}><Select options={(context?.branchIds ?? (selected ? [selected.branchId] : [])).map(id => ({ value: id, label: `Chi nhánh ${id}` }))} /></Form.Item>
        <Form.Item name="diagnosis" label="Chẩn đoán" rules={[{ required: true, whitespace: true, message: 'Nhập chẩn đoán' }]}><Input maxLength={1000} /></Form.Item>
        <Form.Item name="otherDiagnosis" label="Chẩn đoán khác"><Input maxLength={1000} /></Form.Item>
        <Typography.Title level={5}>CHỈ ĐỊNH</Typography.Title>
        <Form.List name="items">{(fields, { add, remove }) => <>
          {fields.map((field, index) => <Card key={field.key} size="small" style={{ marginBottom: 8 }}>
            <Space wrap align="start">
              <Form.Item name={[field.name, 'medicineId']} label="Tên thuốc" rules={[{ required: true, message: 'Chọn thuốc' }]} style={{ minWidth: 360 }}>
                <Select showSearch optionFilterProp="label" options={[...medicines.map(m => ({ value: m.id, label: `${m.name}${m.strength ? ` · ${m.strength}` : ''}` })), ...(selected?.items ?? []).filter(i => !medicines.some(m => m.id === i.medicineId)).map(i => ({ value: i.medicineId, label: `${i.medicineName}${i.strength ? ` · ${i.strength}` : ''}` }))]}
                  onChange={id => { const m = medicines.find(x => x.id === id); form.setFieldValue(['items', index, 'unit'], m?.baseUnit); form.setFieldValue(['items', index, 'instructions'], m?.defaultInstructions ?? ''); }} />
              </Form.Item>
              <Form.Item name={[field.name, 'unit']} label="Đơn vị"><Input readOnly style={{ width: 120 }} /></Form.Item>
              <Form.Item name={[field.name, 'quantity']} label="Số lượng" rules={[{ required: true, message: 'Nhập số lượng' }]}><InputNumber min={1} max={10000} precision={0} /></Form.Item>
              {canManage && <Button danger onClick={() => remove(field.name)} disabled={fields.length === 1}>Xóa</Button>}
            </Space>
            <Form.Item name={[field.name, 'instructions']} label="Hướng dẫn sử dụng" style={{ marginBottom: 0 }}><Input maxLength={1000} /></Form.Item>
          </Card>)}
          {canManage && <Button onClick={() => add({ medicineId: '', quantity: 1, instructions: '' })}>+ Thêm thuốc</Button>}
        </>}</Form.List>
        <Typography.Title level={5} style={{ marginTop: 24 }}>GHI CHÚ / LỜI DẶN</Typography.Title>
        <Form.Item name="advice"><Input.TextArea rows={3} maxLength={4000} /></Form.Item>
        <Typography.Title level={5}>NGÀY & BÁC SĨ KÊ ĐƠN</Typography.Title>
        <Space wrap><Form.Item name="prescribedOn" label="Ngày" rules={[{ required: true }]}><DatePicker format="DD/MM/YYYY" /></Form.Item>
          <span>Bác sĩ: <b>{selected?.prescriberName ?? context?.prescriberName ?? auth.user?.fullName ?? 'Tài khoản hiện tại'}</b></span></Space>
      </Form>
      <Typography.Text type="secondary">Chưa kết nối cổng đơn thuốc quốc gia.</Typography.Text>
    </Modal>
  </Card>;
}
