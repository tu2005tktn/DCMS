import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { Alert, Button, Card, DatePicker, Empty, Form, Input, InputNumber, Modal, Select, Space, Spin, Table, message } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { useEffect, useMemo, useRef, useState } from 'react';
import { api } from '../../services/api';
import { useAuth } from '../authentication/AuthContext';
import type { PatientDetail } from '../patients/types';
import { formatDate, PatientGrid, ReadOnlySheet, type Item, type Prescription } from './PrescriptionSheet';
import './patientPrescriptions.css';

// Contract: docs/specs/S23-PATIENT-PRESCRIPTIONS.vi.md — form "Đơn thuốc" kê từ hồ sơ bệnh nhân.
type Medicine = { id: string; code: string; name: string; activeIngredient?: string | null; strength?: string | null; dosageForm?: string | null; baseUnit: string; defaultInstructions?: string | null };
type NewMedicineValues = { name: string; code: string; activeIngredient?: string; baseUnit: string; referencePrice?: number; instructions?: string };
type CreatedMedicine = Medicine & { instructions?: string | null };
type Branch = { id: number; name: string; address?: string | null; phone?: string | null; email?: string | null };
type Doctor = { employeeId: number; name: string; branchId: number };
type Context = { branchIds: number[]; prescriberName: string; prescriberEmployeeId: number; branches: Branch[]; doctors: Doctor[] };
type DiagnosisOption = { id: string; name: string; code?: string | null };
type Values = { branchId: number; diagnoses: string[]; otherDiagnosis?: string; items: Item[]; advice?: string; prescribedOn: Dayjs; prescriberEmployeeId: number };
type Sheet = { mode: 'create' | 'edit' | 'view'; doc?: Prescription; initial?: Partial<Values> };

const emptyItem = (): Item => ({ medicineId: '', unit: '', quantity: 1, instructions: '' });
const splitDiagnoses = (value?: string | null) => (value ?? '').split(';').map(x => x.trim()).filter(Boolean);

export default function PatientPrescriptionsTab({ patient, active }: { patient: PatientDetail; active: boolean }) {
  const auth = useAuth();
  const canManage = auth.user?.permissions.includes('prescriptions.manage') ?? false;
  const canAddMedicine = auth.user?.permissions.includes('catalog.medicines.create') ?? false;
  const [notice, holder] = message.useMessage();
  const [rows, setRows] = useState<Prescription[]>([]);
  const [context, setContext] = useState<Context>();
  const [medicines, setMedicines] = useState<Medicine[]>([]);
  const [diagnosisOptions, setDiagnosisOptions] = useState<DiagnosisOption[]>([]);
  const [form] = Form.useForm<Values>();
  const [sheet, setSheet] = useState<Sheet | null>(null);
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string>();
  const [sheetError, setSheetError] = useState<string>();
  const [catalogAddIndex, setCatalogAddIndex] = useState<number | null>(null);
  const [catalogSaving, setCatalogSaving] = useState(false);
  const [catalogError, setCatalogError] = useState<string>();
  const branchId = Form.useWatch('branchId', form);
  const [catalogForm] = Form.useForm<NewMedicineValues>();
  const pendingMedicineCreate = useRef<{ fingerprint: string; key: string } | undefined>(undefined);

  async function refresh() {
    setLoading(true); setError(undefined);
    try { setRows(await api<Prescription[]>(`/api/prescriptions/patients/${patient.id}`)); }
    catch (e) { setError((e as Error).message); }
    finally { setLoading(false); }
  }
  useEffect(() => { if (active) void refresh(); }, [active, patient.id]);
  useEffect(() => {
    if (!active || !canManage) return;
    void api<Context>(`/api/prescriptions/patients/${patient.id}/context`).then(setContext).catch(e => setError((e as Error).message));
    void api<DiagnosisOption[]>('/api/system-settings/reference-options?kind=diagnoses').then(setDiagnosisOptions).catch(() => setDiagnosisOptions([]));
  }, [active, canManage, patient.id]);
  useEffect(() => {
    if (!branchId || !sheet || sheet.mode === 'view') return;
    let alive = true;
    api<Medicine[]>(`/api/prescriptions/medicine-options?branchId=${branchId}`)
      .then(value => { if (alive) setMedicines(value); })
      .catch(e => { if (alive) setSheetError((e as Error).message); });
    return () => { alive = false; };
  }, [branchId, sheet]);

  const branch = useMemo(() => context?.branches.find(b => b.id === branchId), [context, branchId]);
  const doctors = useMemo(() => context?.doctors.filter(d => d.branchId === branchId) ?? [], [context, branchId]);
  const medicineOptions = useMemo(() => {
    const known = medicines.map(m => ({ value: m.id, label: `${m.name}${m.strength ? ` · ${m.strength}` : ''}` }));
    // Keep labels for medicines of an edited prescription that are no longer in the active catalog.
    const legacy = (sheet?.doc?.items ?? []).filter(x => !medicines.some(m => m.id === x.medicineId))
      .map(x => ({ value: x.medicineId, label: `${x.medicineName ?? x.medicineCode}${x.strength ? ` · ${x.strength}` : ''}` }));
    return [...known, ...legacy];
  }, [medicines, sheet]);

  function open(mode: Sheet['mode'], doc?: Prescription) {
    setSheetError(mode !== 'view' && !context?.branches.length ? 'Bác sĩ chưa được phân công chi nhánh để kê đơn.' : undefined);
    if (mode === 'view') { setSheet({ mode, doc }); return; }
    setSheet({ mode, doc, initial: doc ? {
      branchId: doc.branchId, diagnoses: splitDiagnoses(doc.diagnosis), otherDiagnosis: doc.otherDiagnosis ?? '', advice: doc.advice ?? '',
      prescribedOn: dayjs(doc.prescribedOn), prescriberEmployeeId: doc.prescriberEmployeeId, items: doc.items.map(x => ({ ...x })),
    } : {
      branchId: context?.branches[0]?.id, diagnoses: [], otherDiagnosis: '', advice: '', prescribedOn: dayjs(),
      prescriberEmployeeId: context?.prescriberEmployeeId, items: [emptyItem()],
    } });
  }
  function close() { if (!busy) { setSheet(null); setSheetError(undefined); } }

  function pickBranch(id: number) {
    const current = form.getFieldValue('prescriberEmployeeId') as number | undefined;
    const allowed = context?.doctors.filter(d => d.branchId === id) ?? [];
    if (!allowed.some(d => d.employeeId === current))
      form.setFieldValue('prescriberEmployeeId', allowed.find(d => d.employeeId === context?.prescriberEmployeeId)?.employeeId ?? allowed[0]?.employeeId);
  }

  async function save(values: Values) {
    const editing = sheet?.mode === 'edit' ? sheet.doc : undefined;
    setBusy(true); setSheetError(undefined);
    try {
      await api(`/api/prescriptions/patients/${patient.id}${editing ? `/${editing.id}` : ''}`, {
        method: editing ? 'PUT' : 'POST',
        body: JSON.stringify({
          requestKey: crypto.randomUUID(), expectedVersion: editing?.version ?? 0, branchId: values.branchId,
          prescribedOn: values.prescribedOn.format('YYYY-MM-DD'), diagnosis: (values.diagnoses ?? []).map(x => x.trim()).filter(Boolean).join('; '),
          otherDiagnosis: values.otherDiagnosis?.trim() || null, advice: values.advice?.trim() || null,
          prescriberEmployeeId: values.prescriberEmployeeId,
          items: values.items.map(x => ({ medicineId: x.medicineId, unit: x.unit ?? '', quantity: x.quantity, instructions: x.instructions ?? '' })),
        }),
      });
      setSheet(null); void notice.success('Đã lưu đơn thuốc'); await refresh();
    } catch (e) { setSheetError((e as Error).message); }
    finally { setBusy(false); }
  }

  function openCatalogAdd(index: number) {
    setCatalogError(undefined);
    catalogForm.resetFields();
    catalogForm.setFieldValue('referencePrice', 0);
    setCatalogAddIndex(index);
  }

  async function createCatalogMedicine() {
    if (!canAddMedicine || catalogSaving || catalogAddIndex === null) return;
    let values: NewMedicineValues;
    try { values = await catalogForm.validateFields(); } catch { return; }
    const entry = {
      code: values.code.trim(),
      name: values.name.trim(),
      activeIngredient: values.activeIngredient?.trim() || null,
      strength: null,
      dosageForm: null,
      baseUnit: values.baseUnit.trim(),
      route: null,
      packaging: null,
      contraindications: null,
      allergyWarnings: null,
      instructions: values.instructions?.trim() || null,
      referencePrice: values.referencePrice ?? null,
      prescriptionEligible: true,
      internalStockTracked: false,
    };
    const fingerprint = JSON.stringify(entry);
    if (pendingMedicineCreate.current?.fingerprint !== fingerprint) {
      pendingMedicineCreate.current = { fingerprint, key: crypto.randomUUID() };
    }

    setCatalogSaving(true);
    setCatalogError(undefined);
    try {
      const created = await api<CreatedMedicine>('/api/system-settings/settings/catalogs/medicines', {
        method: 'POST',
        body: JSON.stringify({ requestKey: pendingMedicineCreate.current.key, entry }),
      });
      pendingMedicineCreate.current = undefined;
      const medicine: Medicine = { ...created, defaultInstructions: created.instructions ?? '' };
      setMedicines(current => [medicine, ...current.filter(item => item.id !== medicine.id)]);
      form.setFieldValue(['items', catalogAddIndex, 'medicineId'], medicine.id);
      form.setFieldValue(['items', catalogAddIndex, 'unit'], medicine.baseUnit);
      if (!form.getFieldValue(['items', catalogAddIndex, 'instructions'])) {
        form.setFieldValue(['items', catalogAddIndex, 'instructions'], medicine.defaultInstructions ?? '');
      }
      setCatalogAddIndex(null);
      catalogForm.resetFields();
    } catch (e) {
      setCatalogError((e as Error).message || 'Không thể lưu thuốc vào danh mục.');
    } finally {
      setCatalogSaving(false);
    }
  }

  const doc = sheet?.doc;
  const header = sheet?.mode === 'view' && doc
    ? { name: doc.branchName, phone: doc.branchPhone, email: doc.branchEmail }
    : { name: branch?.name ?? '', phone: branch?.phone, email: branch?.email };
  const person = sheet?.mode === 'view' && doc
    ? { name: doc.patientName, code: doc.patientCode, birthDate: doc.patientBirthDate, gender: doc.patientGender }
    : { name: patient.fullName, code: patient.patientCode, birthDate: patient.birthDate, gender: patient.gender };

  return <Card title="Đơn thuốc" extra={canManage ? <Button type="primary" disabled={!context} onClick={() => open('create')}>+ Kê đơn thuốc</Button> : undefined}>
    {holder}
    {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} />}
    {loading ? <Spin /> : rows.length ? <Table rowKey="id" dataSource={rows} pagination={{ pageSize: 8 }} columns={[
      { title: 'Ngày kê', dataIndex: 'prescribedOn', width: 110, render: (v: string) => formatDate(v) },
      { title: 'Chẩn đoán', render: (_, row: Prescription) => [row.diagnosis, row.otherDiagnosis].filter(Boolean).join(' · ') || '—' },
      { title: 'Thuốc', render: (_, row: Prescription) => row.items.map(x => x.medicineName).join(', ') },
      { title: 'Bác sĩ kê đơn', dataIndex: 'prescriberName' },
      { title: '', width: 150, render: (_, row: Prescription) => <Space>
        <Button size="small" onClick={() => open('view', row)}>Xem</Button>
        {canManage && <Button size="small" disabled={!context} onClick={() => open('edit', row)}>Sửa</Button>}
      </Space> },
    ]} /> : <Empty description={canManage ? 'Chưa có đơn thuốc. Bấm Kê đơn thuốc để bắt đầu.' : 'Chưa có đơn thuốc.'} />}

    <Modal open={!!sheet} onCancel={close} width={920} centered destroyOnClose className="rx-modal" maskClosable={false}
      title={null} footer={sheet?.mode === 'view'
        ? <Button onClick={close}>Đóng</Button>
        : <Space><Button disabled={busy} onClick={close}>Hủy</Button><Button type="primary" loading={busy} onClick={() => form.submit()}>Lưu đơn thuốc</Button></Space>}>
      {sheet && (() => {
        const multiBranch = sheet.mode !== 'view' && (context?.branches.length ?? 0) > 1;
        const head = <header className="rx-head">
          <div className="rx-prescription-heading">
            <h2 className="rx-title">ĐƠN THUỐC</h2>
          </div>
          <div className="rx-clinic">
            {multiBranch
              ? <Form.Item name="branchId" noStyle>
                <Select size="small" aria-label="Chi nhánh kê đơn" style={{ minWidth: 220 }} onChange={pickBranch}
                  options={context!.branches.map(b => ({ value: b.id, label: b.name }))} />
              </Form.Item>
              : <strong>{header.name}</strong>}
            {(header.phone || header.email) && <div className="rx-contact">
              {header.phone && <span>☎ {header.phone}</span>}{header.email && <span>✉ {header.email}</span>}
            </div>}
          </div>
        </header>;
        const alert = sheetError && <Alert type="error" showIcon message={sheetError} style={{ margin: '12px 0' }} />;
        if (sheet.mode === 'view' && doc) return <div className="rx-sheet">{head}{alert}<ReadOnlySheet doc={doc} person={person} /></div>;
        return <Form form={form} key={doc?.id ?? 'new'} initialValues={sheet.initial} layout="horizontal" requiredMark={false}
          onFinish={values => void save(values)} className="rx-sheet rx-form">
          {head}{alert}
          {!multiBranch && <Form.Item name="branchId" hidden><InputNumber /></Form.Item>}
            <section className="rx-section">
              <h3>BỆNH NHÂN</h3>
              <PatientGrid person={person} />
              <div className="rx-row"><label htmlFor="rx-diagnoses">Chẩn đoán</label>
                <Form.Item name="diagnoses" noStyle>
                  <Select id="rx-diagnoses" mode="tags" allowClear placeholder="Chọn hoặc nhập chẩn đoán" tokenSeparators={[';']} maxTagTextLength={60}
                    options={diagnosisOptions.map(o => ({ value: o.name, label: o.code ? `${o.name} (${o.code})` : o.name }))} />
                </Form.Item></div>
              <div className="rx-row"><label htmlFor="rx-other">Chẩn đoán khác</label>
                <Form.Item name="otherDiagnosis" noStyle rules={[{ max: 1000 }]}><Input id="rx-other" maxLength={1000} /></Form.Item></div>
            </section>

            <section className="rx-section">
              <h3>CHỈ ĐỊNH</h3>
              <Form.List name="items">{(fields, { add, remove }) => <div className="rx-lines">
                {fields.map((field, index) => <div key={field.key} className="rx-line">
                  <div className="rx-line-body">
                    <div className="rx-line-top">
                      <Form.Item name={[field.name, 'medicineId']} rules={[{ required: true, message: 'Chọn thuốc' }]} className="rx-medicine">
                        <Select showSearch optionFilterProp="label" placeholder="Tên thuốc" aria-label={`Tên thuốc dòng ${index + 1}`} options={medicineOptions}
                          onChange={id => {
                            const m = medicines.find(x => x.id === id);
                            form.setFieldValue(['items', field.name, 'unit'], m?.baseUnit ?? '');
                            if (!form.getFieldValue(['items', field.name, 'instructions'])) form.setFieldValue(['items', field.name, 'instructions'], m?.defaultInstructions ?? '');
                          }} />
                      </Form.Item>
                      <Form.Item name={[field.name, 'unit']} rules={[{ required: true, whitespace: true, message: 'Nhập đơn vị thuốc' }, { max: 40 }]} className="rx-unit">
                        <Input maxLength={40} placeholder="Đơn vị tính/đóng gói" aria-label={`Đơn vị tính/đóng gói dòng ${index + 1}`} />
                      </Form.Item>
                      <Form.Item name={[field.name, 'quantity']} rules={[{ required: true, message: 'Nhập số lượng' }, { type: 'integer', min: 1, max: 10000, message: '1–10.000' }]} className="rx-qty">
                        <InputNumber min={1} max={10000} precision={0} placeholder="SL" aria-label={`Số lượng dòng ${index + 1}`} style={{ width: '100%' }} />
                      </Form.Item>
                      {canAddMedicine && <Button className="rx-catalog-add" aria-label="Thêm thuốc vào danh mục" title="Thêm thuốc vào danh mục"
                        disabled={busy} onClick={() => openCatalogAdd(field.name)}><PlusOutlined /></Button>}
                    </div>
                    <div className="rx-line-bottom">
                      <Form.Item name={[field.name, 'instructions']} rules={[{ max: 1000 }]} className="rx-instructions">
                        <Input maxLength={1000} placeholder="Hướng dẫn sử dụng" aria-label={`Hướng dẫn sử dụng dòng ${index + 1}`} />
                      </Form.Item>
                      <Button type="text" danger icon={<DeleteOutlined />} aria-label={`Xóa dòng ${index + 1}`}
                        disabled={fields.length <= 1} onClick={() => remove(field.name)} />
                    </div>
                  </div>
                </div>)}
                <div className="rx-add">
                  <Button className="rx-add-btn" disabled={fields.length >= 30} onClick={() => add(emptyItem())}>+ THÊM</Button>
                </div>
              </div>}</Form.List>
            </section>

            <section className="rx-section">
              <h3>GHI CHÚ/LỜI DẶN</h3>
              <Form.Item name="advice" noStyle rules={[{ max: 4000 }]}><Input.TextArea rows={2} maxLength={4000} aria-label="Ghi chú, lời dặn" /></Form.Item>
            </section>

            <section className="rx-section">
              <h3>NGÀY &amp; BÁC SĨ KÊ ĐƠN</h3>
              <div className="rx-grid">
                <div className="rx-row"><label>Ngày</label>
                  <Form.Item name="prescribedOn" noStyle rules={[{ required: true }]}>
                    <DatePicker format="DD/MM/YYYY" allowClear={false} aria-label="Ngày kê đơn" style={{ width: '100%' }} disabledDate={d => d.isAfter(dayjs(), 'day')} />
                  </Form.Item></div>
                <div className="rx-row"><label>Bác sĩ</label>
                  <Form.Item name="prescriberEmployeeId" noStyle rules={[{ required: true }]}>
                    <Select aria-label="Bác sĩ kê đơn" style={{ width: '100%' }} options={[
                      ...doctors.map(d => ({ value: d.employeeId, label: d.name })),
                      ...(doc && !doctors.some(d => d.employeeId === doc.prescriberEmployeeId) ? [{ value: doc.prescriberEmployeeId, label: doc.prescriberName }] : []),
                    ]} />
                  </Form.Item></div>
              </div>
            </section>
        </Form>;
      })()}
    </Modal>
    <Modal open={catalogAddIndex !== null} title="Thuốc, dược phẩm, sản phẩm y tế" width={560} centered destroyOnClose
      className="rx-medicine-modal" okText="LƯU LẠI" cancelText="QUAY LẠI" confirmLoading={catalogSaving}
      okButtonProps={{ disabled: !canAddMedicine }} onCancel={() => { if (!catalogSaving) { setCatalogAddIndex(null); setCatalogError(undefined); } }}
      onOk={() => void createCatalogMedicine()}>
      {catalogError && <Alert type="error" showIcon message="Không lưu được thuốc" description={catalogError} style={{ marginBottom: 16 }} />}
      <Form form={catalogForm} layout="horizontal" labelCol={{ span: 7 }} wrapperCol={{ span: 17 }} initialValues={{ referencePrice: 0 }} preserve={false}>
        <Form.Item name="name" label="Tên" rules={[{ required: true, whitespace: true, message: 'Nhập tên thuốc.' }, { max: 200, message: 'Tối đa 200 ký tự.' }]}>
          <Input autoFocus maxLength={200} placeholder="Ví dụ: Paracetamol (Panadol) 500mg" />
        </Form.Item>
        <Form.Item name="code" label="Mã thuốc/mã sản phẩm" rules={[{ required: true, whitespace: true, message: 'Nhập mã thuốc hoặc mã sản phẩm.' }, { max: 40, message: 'Tối đa 40 ký tự.' }, { pattern: /^[A-Za-z0-9][A-Za-z0-9._-]*$/, message: 'Mã chỉ gồm chữ, số, dấu chấm, gạch dưới hoặc gạch ngang.' }]}>
          <Input maxLength={40} placeholder="Ví dụ: PARA-500" onBlur={event => catalogForm.setFieldValue('code', event.target.value.trim().toUpperCase())} />
        </Form.Item>
        <Form.Item name="activeIngredient" label="Hoạt chất/biệt dược" rules={[{ max: 200, message: 'Tối đa 200 ký tự.' }]}>
          <Input maxLength={200} />
        </Form.Item>
        <Form.Item name="baseUnit" label="Đơn vị tính/đóng gói" rules={[{ required: true, whitespace: true, message: 'Nhập đơn vị tính.' }, { max: 40, message: 'Tối đa 40 ký tự.' }]}>
          <Input maxLength={40} placeholder="Ví dụ: Hộp 20 viên" />
        </Form.Item>
        <Form.Item name="referencePrice" label="Đơn giá (₫)" rules={[{ type: 'integer', min: 0, max: 1_000_000_000_000, message: 'Nhập giá nguyên không âm.' }]}>
          <InputNumber min={0} precision={0} step={1000} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item name="instructions" label="Hướng dẫn sử dụng" rules={[{ max: 1000, message: 'Tối đa 1.000 ký tự.' }]}>
          <Input maxLength={1000} />
        </Form.Item>
      </Form>
    </Modal>
  </Card>;
}
