import { Alert, Button, Card, Empty, Form, Input, InputNumber, Select, Space, Spin, Table, Typography, message } from 'antd';
import { useEffect, useState } from 'react';
import { api } from '../../services/api';
import { useAuth } from '../authentication/AuthContext';
import type { PatientDetail } from '../patients/types';

type Medicine = { id: string; code: string; name: string; strength?: string; baseUnit: string; defaultInstructions?: string };
type Item = { id?: string; medicineId: string; medicineName?: string; medicineCode?: string; strength?: string; dosageForm?: string; unit?: string; quantity: number; instructions?: string };
type Prescription = { id: string; branchId: number; branchName: string; prescribedOn: string; diagnosis: string; otherDiagnosis?: string; advice?: string; prescriberName: string; version: number; items: Item[] };
type Context = { branchIds: number[]; prescriberName: string };
type Values = { items: Item[]; advice?: string };

export default function PatientPrescriptionsTab({ patient, active }: { patient: PatientDetail; active: boolean }) {
  const auth = useAuth();
  const canManage = auth.user?.permissions.includes('prescriptions.manage') ?? false;
  const [notice, holder] = message.useMessage();
  const [rows, setRows] = useState<Prescription[]>([]);
  const [context, setContext] = useState<Context>();
  const [medicines, setMedicines] = useState<Medicine[]>([]);
  const [form] = Form.useForm<Values>();
  const [editing, setEditing] = useState<Prescription | null>(null);
  const [editorOpen, setEditorOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string>();

  async function refresh() {
    setLoading(true); setError(undefined);
    try { setRows(await api<Prescription[]>(`/api/prescriptions/patients/${patient.id}`)); }
    catch (e) { setError((e as Error).message); }
    finally { setLoading(false); }
  }
  useEffect(() => { if (active) void refresh(); }, [active, patient.id]);
  useEffect(() => {
    if (!active || !canManage) return;
    void api<Context>(`/api/prescriptions/patients/${patient.id}/context`).then(async value => {
      setContext(value);
      if (value.branchIds[0]) setMedicines(await api<Medicine[]>(`/api/prescriptions/medicine-options?branchId=${value.branchIds[0]}`));
    }).catch(e => setError((e as Error).message));
  }, [active, canManage, patient.id]);

  function start(doc?: Prescription) {
    setError(undefined); setEditing(doc ?? null); setEditorOpen(true);
    form.setFieldsValue(doc ? { advice: doc.advice, items: doc.items.map(x => ({ ...x })) }
      : { advice: '', items: [{ medicineId: '', quantity: 1, instructions: '' }] });
  }
  async function save(values: Values) {
    if (!context?.branchIds[0]) { setError('Bác sĩ chưa được phân công chi nhánh để kê đơn.'); return; }
    setBusy(true); setError(undefined);
    try {
      await api(`/api/prescriptions/patients/${patient.id}${editing ? `/${editing.id}` : ''}`, {
        method: editing ? 'PUT' : 'POST',
        body: JSON.stringify({ requestKey: crypto.randomUUID(), expectedVersion: editing?.version ?? 0,
          branchId: editing?.branchId ?? context.branchIds[0], prescribedOn: editing?.prescribedOn ?? new Date().toISOString().slice(0, 10),
          diagnosis: editing?.diagnosis ?? '', otherDiagnosis: editing?.otherDiagnosis ?? null, advice: values.advice ?? null,
          items: values.items.map(x => ({ medicineId: x.medicineId, quantity: x.quantity, instructions: x.instructions ?? '' })) }),
      });
      setEditing(null); setEditorOpen(false); void notice.success('Đã lưu đơn thuốc'); await refresh();
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }

  return <Card title="Đơn thuốc" extra={canManage && !editorOpen ? <Button type="primary" onClick={() => start()}>+ Kê đơn thuốc</Button> : undefined}>
    {holder}
    {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} />}
    {editorOpen && <Typography.Title level={5}>{editing ? 'Sửa đơn thuốc' : 'Kê đơn thuốc'}</Typography.Title>}
    {editorOpen && <Form form={form} layout="vertical" onFinish={values => void save(values)}>
      <Form.List name="items">{(fields, { add, remove }) => <>
        {fields.map((field, index) => <Card key={field.key} size="small" style={{ marginBottom: 12 }}>
          <Space align="start" wrap style={{ width: '100%' }}>
            <Form.Item name={[field.name, 'medicineId']} label="Thuốc" rules={[{ required: true, message: 'Chọn thuốc' }]} style={{ minWidth: 280 }}>
              <Select showSearch optionFilterProp="label" placeholder="Tìm thuốc" options={medicines.map(m => ({ value: m.id, label: `${m.name}${m.strength ? ` · ${m.strength}` : ''}` }))}
                onChange={id => { const m = medicines.find(x => x.id === id); form.setFieldValue(['items', index, 'unit'], m?.baseUnit); form.setFieldValue(['items', index, 'instructions'], m?.defaultInstructions ?? ''); }} />
            </Form.Item>
            <Form.Item name={[field.name, 'quantity']} label="Số lượng" rules={[{ required: true }, { type: 'integer', min: 1, max: 10000 }]}><InputNumber min={1} max={10000} precision={0} /></Form.Item>
            <Form.Item name={[field.name, 'unit']} label="Đơn vị"><Input readOnly placeholder="Tự lấy" /></Form.Item>
            <Form.Item name={[field.name, 'instructions']} label="Cách dùng" style={{ flex: 1, minWidth: 220 }}><Input maxLength={1000} placeholder="Ví dụ: uống sau ăn" /></Form.Item>
            {fields.length > 1 && <Button danger style={{ marginTop: 30 }} onClick={() => remove(field.name)}>Xóa</Button>}
          </Space>
        </Card>)}
        <Button onClick={() => add({ medicineId: '', quantity: 1, instructions: '' })}>+ Thêm thuốc</Button>
      </>}</Form.List>
      <Form.Item name="advice" label="Lời dặn (không bắt buộc)" style={{ marginTop: 16 }}><Input.TextArea rows={2} maxLength={4000} /></Form.Item>
      <Space><Button type="primary" htmlType="submit" loading={busy}>Lưu đơn thuốc</Button><Button disabled={busy} onClick={() => { setEditing(null); setEditorOpen(false); setError(undefined); }}>Hủy</Button></Space>
    </Form>}
    {!editorOpen && (loading ? <Spin /> : rows.length ? <Table rowKey="id" dataSource={rows} pagination={{ pageSize: 8 }} columns={[
      { title: 'Ngày kê', dataIndex: 'prescribedOn', render: (v: string) => new Date(`${v}T00:00:00`).toLocaleDateString('vi-VN') },
      { title: 'Thuốc', render: (_, row: Prescription) => row.items.map(x => x.medicineName).join(', ') },
      { title: 'Bác sĩ', dataIndex: 'prescriberName' },
      { title: '', render: (_, row: Prescription) => canManage ? <Button onClick={() => start(row)}>Xem / sửa</Button> : null },
    ]} /> : <Empty description="Chưa có đơn thuốc. Bấm Kê đơn thuốc để bắt đầu." />)}
    {!editorOpen && rows.length > 0 && !canManage && rows.map(row => <Card key={row.id} size="small" style={{ marginTop: 12 }} title={`${row.prescribedOn} · ${row.branchName}`}>
      <Typography.Paragraph>{row.items.map(x => `${x.medicineName}: ${x.quantity} ${x.unit}${x.instructions ? ` — ${x.instructions}` : ''}`).join('\n')}</Typography.Paragraph>
      {row.advice && <Typography.Text>{row.advice}</Typography.Text>}
    </Card>)}
  </Card>;
}
