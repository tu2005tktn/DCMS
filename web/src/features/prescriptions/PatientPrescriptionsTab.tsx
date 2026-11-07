import { Alert, AutoComplete, Button, Card, Col, DatePicker, Divider, Empty, Form, Input, InputNumber, Modal, Row, Select, Space, Spin, Table, Typography, message } from 'antd';
import dayjs from 'dayjs';
import { useEffect, useRef, useState } from 'react';
import { api } from '../../services/api';
import { useAuth } from '../authentication/AuthContext';
import { formatDate } from '../patient-links/types';
import type { PatientDetail } from '../patients/types';
import { useDiagnosisOptions } from '../examinations/useDiagnosisOptions';

type Medicine = { id: string; code: string; name: string; strength?: string; baseUnit: string; packaging?: string; defaultInstructions?: string };
type Line = { medicineId: string; quantity?: number; instructions?: string; unit?: string };
type Prescription = { id: string; patientId: number; branchId: number; prescribedOn: string; diagnosis: string; otherDiagnosis?: string; advice?: string; prescriberName: string; version: number; items: (Line & { id: string; medicineName: string; medicineCode: string; strength?: string; dosageForm?: string })[] };
type FormValues = { branchId: number; prescribedOn: dayjs.Dayjs; diagnosis: string; otherDiagnosis?: string; advice?: string; items: Line[] };
type MedicineDraft = { code: string; name: string; activeIngredient?: string; strength?: string; dosageForm?: string; baseUnit: string; packaging?: string; referencePrice?: number | null; instructions?: string };
type MedicineDto = Medicine & { activeIngredient?: string; dosageForm?: string; route?: string; referencePrice?: number | null; instructions?: string | null };

export default function PatientPrescriptionsTab({ patient, active }: { patient: PatientDetail; active: boolean }) {
  const auth = useAuth();
  const canManage = auth.user?.permissions.includes('prescriptions.manage') ?? false;
  const canAddMedicine = canManage && (auth.user?.permissions.includes('catalog.medicines.manage') ?? false);
  const [context, setContext] = useState<{ branchIds: number[]; prescriberName: string }>();
  const [form] = Form.useForm<FormValues>();
  const branchId = Form.useWatch('branchId', form);
  const { options: diagnosisCatalog } = useDiagnosisOptions(branchId);
  const [rows, setRows] = useState<Prescription[]>([]);
  const [medicines, setMedicines] = useState<Medicine[]>([]);
  const [selected, setSelected] = useState<Prescription | null>(null);
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string>();
  const [notice, holder] = message.useMessage();
  const [medicineModalOpen, setMedicineModalOpen] = useState(false);
  const [medicineSaving, setMedicineSaving] = useState(false);
  const [medicineError, setMedicineError] = useState<string>();
  const [medicineTargetIndex, setMedicineTargetIndex] = useState(0);
  const [medicineForm] = Form.useForm<MedicineDraft>();
  const pendingMedicine = useRef<{ fingerprint: string; requestKey: string } | undefined>(undefined);

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
    } : { branchId: context?.branchIds[0], prescribedOn: dayjs(), diagnosis: '', otherDiagnosis: '', advice: '', items: [{ medicineId: '', instructions: '' }] });
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
  async function addMedicine() {
    if (!canAddMedicine || medicineSaving) return;
    let values: MedicineDraft;
    try { values = await medicineForm.validateFields(); } catch { return; }
    const entry = {
      code: values.code.trim(), name: values.name.trim(), activeIngredient: values.activeIngredient?.trim() || null,
      strength: values.strength?.trim() || null, dosageForm: values.dosageForm?.trim() || null, baseUnit: values.baseUnit.trim(),
      route: null, packaging: values.packaging?.trim() || null, contraindications: null, allergyWarnings: null,
      instructions: values.instructions?.trim() || null, referencePrice: values.referencePrice ?? null,
      prescriptionEligible: true, internalStockTracked: false,
    };
    const fingerprint = JSON.stringify(entry);
    if (pendingMedicine.current?.fingerprint !== fingerprint) pendingMedicine.current = { fingerprint, requestKey: crypto.randomUUID() };
    setMedicineSaving(true); setMedicineError(undefined);
    try {
      const created = await api<MedicineDto>('/api/system-settings/settings/catalogs/medicines', {
        method: 'POST', body: JSON.stringify({ requestKey: pendingMedicine.current.requestKey, entry }),
      });
      pendingMedicine.current = undefined;
      const option: Medicine = { ...created, defaultInstructions: created.instructions ?? '' };
      setMedicines(current => [option, ...current.filter(m => m.id !== option.id)]);
      const targetIndex = medicineTargetIndex;
      form.setFieldValue(['items', targetIndex, 'medicineId'], option.id);
      form.setFieldValue(['items', targetIndex, 'unit'], option.baseUnit);
      form.setFieldValue(['items', targetIndex, 'instructions'], option.defaultInstructions ?? '');
      setMedicineModalOpen(false); medicineForm.resetFields();
    } catch (e) { setMedicineError((e as Error).message || 'Không thể lưu thuốc vào danh mục.'); }
    finally { setMedicineSaving(false); }
  }
  return <Card title={open ? (selected ? 'SỬA ĐƠN THUỐC' : 'ĐƠN THUỐC') : 'Đơn thuốc'} extra={open
    ? <Button onClick={() => setOpen(false)}>Quay lại danh sách</Button>
    : canManage && <Button type="primary" onClick={() => show(null)}>+ Kê đơn thuốc</Button>}>
    {holder}
    {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} />}
    {open ? <>
      <section aria-label="Thông tin khách hàng" style={{ padding: 16, background: '#fafafa', borderRadius: 8, marginBottom: 20 }}>
        <Typography.Title level={5} style={{ marginTop: 0 }}>KHÁCH HÀNG</Typography.Title>
        <Row gutter={[16, 12]}>
          <Col xs={24} md={12}><Typography.Text type="secondary">Họ và tên</Typography.Text><div><Typography.Text strong>{patient.fullName}</Typography.Text></div></Col>
          <Col xs={24} md={12}><Typography.Text type="secondary">MSKH</Typography.Text><div><Typography.Text strong>{patient.patientCode}</Typography.Text></div></Col>
          <Col xs={24} md={12}><Typography.Text type="secondary">Ngày sinh</Typography.Text><div><Typography.Text strong>{patient.birthDate ? formatDate(patient.birthDate) : '—'}</Typography.Text></div></Col>
          <Col xs={24} md={12}><Typography.Text type="secondary">Giới tính</Typography.Text><div><Typography.Text strong>{patient.gender ?? '—'}</Typography.Text></div></Col>
        </Row>
      </section>
      <Form form={form} layout="vertical" onFinish={values => void save(values)} disabled={!canManage}>
        <Row gutter={16}>
          <Col xs={24} md={12}>
            <Form.Item name="branchId" label="Chi nhánh" rules={[{ required: true, message: 'Chọn chi nhánh' }]}>
              <Select options={(context?.branchIds ?? (selected ? [selected.branchId] : [])).map(id => ({ value: id, label: `Chi nhánh ${id}` }))} />
            </Form.Item>
          </Col>
          <Col xs={24} md={12}>
        <Form.Item name="diagnosis" label="Chẩn đoán" rules={[{ required: true, whitespace: true, message: 'Nhập chẩn đoán' }, { max: 1000, message: 'Tối đa 1.000 ký tự.' }]}>
              <AutoComplete options={diagnosisCatalog.filter(x => x.value !== 'Chưa phát hiện bất thường').map(x => ({ value: x.value, label: x.label }))}
                filterOption={(input, option) => String(option?.label ?? '').toLocaleLowerCase('vi').includes(input.toLocaleLowerCase('vi'))}
                placeholder="Chọn chẩn đoán hoặc nhập nội dung" maxLength={1000} />
            </Form.Item>
          </Col>
          <Col span={24}><Form.Item name="otherDiagnosis" label="Chẩn đoán khác"><Input maxLength={1000} /></Form.Item></Col>
        </Row>
        <Divider orientation="left"><Typography.Text strong>CHỈ ĐỊNH</Typography.Text></Divider>
        <Form.List name="items">{(fields, { add, remove }) => <>
          {fields.map((field, index) => <Card key={field.key} size="small" style={{ marginBottom: 12, background: '#fff' }}>
            <Row gutter={12} align="top">
              <Col xs={24} md={canAddMedicine ? 15 : 16}>
                <Form.Item name={[field.name, 'medicineId']} label="Tên thuốc" rules={[{ required: true, message: 'Chọn thuốc' }]}>
                  <Select showSearch optionFilterProp="label" placeholder="Tìm thuốc trong danh mục"
                    options={[...medicines.map(m => ({ value: m.id, label: `${m.name}${m.strength ? ` · ${m.strength}` : ''}` })), ...(selected?.items ?? []).filter(i => !medicines.some(m => m.id === i.medicineId)).map(i => ({ value: i.medicineId, label: `${i.medicineName}${i.strength ? ` · ${i.strength}` : ''}` }))]}
                    onChange={id => { const m = medicines.find(x => x.id === id); form.setFieldValue(['items', index, 'unit'], m?.baseUnit); form.setFieldValue(['items', index, 'instructions'], m?.defaultInstructions ?? ''); }} />
                </Form.Item>
              </Col>
              {canAddMedicine && <Col xs={24} md={3}><Form.Item label=" "><Button aria-label="Thêm thuốc vào danh mục" title="Thêm thuốc vào danh mục" onClick={() => { setMedicineTargetIndex(index); setMedicineError(undefined); setMedicineModalOpen(true); }}>+</Button></Form.Item></Col>}
              <Col xs={12} md={3}><Form.Item name={[field.name, 'quantity']} label="Số lượng" rules={[{ required: true, message: 'Nhập số lượng' }, { type: 'integer', min: 1, max: 10000, message: 'Nhập số nguyên từ 1 đến 10.000.' }]}><InputNumber min={1} max={10000} precision={0} style={{ width: '100%' }} /></Form.Item></Col>
              <Col xs={12} md={3}><Form.Item name={[field.name, 'unit']} label="Đơn vị"><Input readOnly /></Form.Item></Col>
            </Row>
            <Row gutter={12} align="middle">
              <Col flex="auto"><Form.Item name={[field.name, 'instructions']} label="Hướng dẫn sử dụng" style={{ marginBottom: 0 }}><Input maxLength={1000} placeholder="Hướng dẫn riêng cho thuốc này" /></Form.Item></Col>
              {canManage && fields.length > 1 && <Col><Button danger onClick={() => remove(field.name)}>Xóa dòng</Button></Col>}
            </Row>
          </Card>)}
          {canManage && <Button type="primary" onClick={() => add({ medicineId: '', instructions: '' })}>+ THÊM</Button>}
        </>}</Form.List>
        <Divider orientation="left"><Typography.Text strong>GHI CHÚ/LỜI DẶN</Typography.Text></Divider>
        <Form.Item name="advice"><Input.TextArea rows={3} maxLength={4000} placeholder="Lời dặn chung của bác sĩ" /></Form.Item>
        <Divider orientation="left"><Typography.Text strong>NGÀY & BÁC SĨ KÊ ĐƠN</Typography.Text></Divider>
        <Row gutter={16} align="top">
          <Col xs={24} md={12}><Form.Item name="prescribedOn" label="Ngày" rules={[{ required: true, message: 'Chọn ngày kê đơn.' }]}><DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} /></Form.Item></Col>
          <Col xs={24} md={12}><Form.Item label="Bác sĩ"><Input readOnly value={selected?.prescriberName ?? context?.prescriberName ?? auth.user?.fullName ?? 'Tài khoản hiện tại'} /></Form.Item></Col>
        </Row>
        <Space wrap>
          {canManage && <Button type="primary" htmlType="submit" loading={busy}>Lưu lại</Button>}
          <Button onClick={() => setOpen(false)} disabled={busy}>Quay lại</Button>
        </Space>
      </Form>
      <Typography.Text type="secondary" style={{ display: 'block', marginTop: 16 }}>Chưa kết nối cổng đơn thuốc quốc gia.</Typography.Text>
    </> : loading ? <Spin /> : rows.length ? <Table rowKey="id" dataSource={rows} pagination={{ pageSize: 10 }} columns={[
      { title: 'Ngày', dataIndex: 'prescribedOn', render: (v: string) => formatDate(v) },
      { title: 'Chẩn đoán', dataIndex: 'diagnosis' },
      { title: 'Mã đơn thuốc', dataIndex: 'id', render: (v: string) => v.slice(0, 8).toUpperCase() },
      { title: 'Bác sĩ', dataIndex: 'prescriberName' },
      { title: '', render: (_, row) => <Button onClick={() => show(row)}>{canManage ? 'Xem / sửa' : 'Xem đơn'}</Button> },
    ]} /> : <Empty description="Chưa có đơn thuốc trong hồ sơ." />}
    <Modal open={medicineModalOpen} title="Thuốc, dược phẩm, sản phẩm y tế" okText="Lưu lại" cancelText="Quay lại"
      confirmLoading={medicineSaving} okButtonProps={{ disabled: !canAddMedicine }} onCancel={() => { if (!medicineSaving) setMedicineModalOpen(false); }} onOk={() => void addMedicine()}>
      {medicineError && <Alert type="error" showIcon message="Không lưu được thuốc" description={medicineError} style={{ marginBottom: 16 }} />}
      <Form form={medicineForm} layout="horizontal" labelCol={{ xs: { span: 24 }, sm: { span: 8 } }} wrapperCol={{ xs: { span: 24 }, sm: { span: 16 } }} initialValues={{ referencePrice: 0 }} preserve={false}>
        <Form.Item name="name" label="Tên" rules={[{ required: true, whitespace: true, message: 'Nhập tên thuốc.' }, { max: 200 }]}><Input autoFocus maxLength={200} placeholder="Ví dụ: Paracetamol 500mg" /></Form.Item>
        <Form.Item name="code" label="Mã thuốc/mã sản phẩm" rules={[{ required: true, whitespace: true, message: 'Nhập mã thuốc hoặc mã sản phẩm.' }, { max: 40 }, { pattern: /^[A-Za-z0-9][A-Za-z0-9._-]*$/, message: 'Mã chỉ gồm chữ, số, dấu chấm, gạch dưới hoặc gạch ngang.' }]}><Input maxLength={40} placeholder="Ví dụ: PARA-500" onBlur={event => medicineForm.setFieldValue('code', event.target.value.trim().toUpperCase())} /></Form.Item>
        <Form.Item name="activeIngredient" label="Hoạt chất/biệt dược" rules={[{ max: 200 }]}><Input maxLength={200} /></Form.Item>
        <Form.Item name="strength" label="Hàm lượng" rules={[{ max: 200 }]}><Input maxLength={200} /></Form.Item>
        <Form.Item name="dosageForm" label="Dạng bào chế" rules={[{ max: 200 }]}><Input maxLength={200} /></Form.Item>
        <Form.Item name="baseUnit" label="Đơn vị tính/đóng gói" rules={[{ required: true, whitespace: true, message: 'Nhập đơn vị tính.' }, { max: 40 }]}><Input maxLength={40} placeholder="Ví dụ: viên, gói, ml" /></Form.Item>
        <Form.Item name="packaging" label="Quy cách đóng gói" rules={[{ max: 160 }]}><Input maxLength={160} placeholder="Ví dụ: Hộp 20 viên" /></Form.Item>
        <Form.Item name="referencePrice" label="Đơn giá (₫)" rules={[{ type: 'integer', min: 0, max: 1_000_000_000_000, message: 'Nhập giá nguyên không âm.' }]}><InputNumber min={0} precision={0} step={1000} style={{ width: '100%' }} /></Form.Item>
        <Form.Item name="instructions" label="Hướng dẫn sử dụng" rules={[{ max: 1000 }]}><Input.TextArea rows={2} maxLength={1000} /></Form.Item>
      </Form>
    </Modal>
  </Card>;
}
