import { Alert, Button, Card, Col, Drawer, Form, Input, InputNumber, Modal, Row, Segmented, Select, Space, Table, Tag, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api, ApiError } from '../../services/api';
import { usePermissions } from '../authentication/usePermissions';
import { doseSummary, mealLabel, type MedicineOption, type PrescriptionTemplate, type PrescriptionTemplateLine } from './types';

type LineFormValues = {
  medicineId: string;
  route?: string;
  morningDose: string;
  noonDose: string;
  afternoonDose: string;
  eveningDose: string;
  mealTiming: string;
  days: number;
  quantity: number;
  instructions?: string;
};

type TemplateFormValues = { code: string; name: string; diagnosisKind?: string; description?: string };
type StatusFilter = 'Active' | 'Inactive' | 'All';

const numericDose = (value?: string) => {
  const n = Number((value ?? '').replace(',', '.').trim());
  return Number.isFinite(n) && n >= 0 ? n : NaN;
};

// Global master data (docs/specs/PRESCRIPTION-MANAGEMENT.md §2.2): not tied to the selected branch.
export default function PrescriptionTemplatesPage() {
  const has = usePermissions();
  const canManage = has('prescriptions.manage');

  const [templates, setTemplates] = useState<PrescriptionTemplate[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string>();
  const [search, setSearch] = useState('');
  const [diagnosis, setDiagnosis] = useState('');
  const [status, setStatus] = useState<StatusFilter>('Active');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  const [drawerOpen, setDrawerOpen] = useState(false);
  const [editingTemplate, setEditingTemplate] = useState<PrescriptionTemplate | null>(null);
  const [templateLines, setTemplateLines] = useState<PrescriptionTemplateLine[]>([]);
  const [drawerDirty, setDrawerDirty] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<ApiError>();

  const [stateTarget, setStateTarget] = useState<PrescriptionTemplate>();
  const [stateReason, setStateReason] = useState('');
  const [stateBusy, setStateBusy] = useState(false);
  const [stateError, setStateError] = useState<string>();

  const [medicineOptions, setMedicineOptions] = useState<MedicineOption[]>([]);
  const [medicineQuery, setMedicineQuery] = useState('');
  const [medicineLoading, setMedicineLoading] = useState(false);
  const [templateForm] = Form.useForm<TemplateFormValues>();
  const [lineForm] = Form.useForm<LineFormValues>();
  const pending = useRef<{ fingerprint: string; key: string } | null>(null);
  const savingRef = useRef(false);

  const loadTemplates = async () => {
    setLoading(true);
    setError(undefined);
    try {
      const query = new URLSearchParams({ status, page: String(page), pageSize: String(pageSize) });
      if (search.trim()) query.set('search', search.trim());
      if (diagnosis.trim()) query.set('diagnosis', diagnosis.trim());
      setTemplates(await api<PrescriptionTemplate[]>(`/api/prescriptions/templates?${query}`));
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { void loadTemplates(); }, [search, diagnosis, status, page, pageSize]);

  useEffect(() => {
    if (!drawerOpen) return;
    let active = true;
    setMedicineLoading(true);
    const timer = window.setTimeout(() => {
      void api<MedicineOption[]>(`/api/prescriptions/medicine-options?${new URLSearchParams({ q: medicineQuery })}`)
        .then(rows => { if (active) setMedicineOptions(rows); })
        .catch(() => { if (active) setMedicineOptions([]); })
        .finally(() => { if (active) setMedicineLoading(false); });
    }, 250);
    return () => { active = false; window.clearTimeout(timer); };
  }, [drawerOpen, medicineQuery]);

  const openDrawer = (t: PrescriptionTemplate | null) => {
    setEditingTemplate(t);
    setTemplateLines(t ? [...t.lines] : []);
    templateForm.resetFields();
    if (t) templateForm.setFieldsValue({ code: t.code, name: t.name, diagnosisKind: t.diagnosisKind, description: t.description });
    lineForm.resetFields();
    setSaveError(undefined);
    setDrawerDirty(false);
    pending.current = null;
    setDrawerOpen(true);
  };

  const closeDrawer = () => {
    if (saving) return;
    if (drawerDirty && !window.confirm('Đóng và bỏ các thay đổi chưa lưu của mẫu đơn?')) return;
    setDrawerOpen(false);
  };

  const recalculate = () => {
    const v = lineForm.getFieldsValue();
    const doses = [v.morningDose, v.noonDose, v.afternoonDose, v.eveningDose].map(numericDose);
    if (doses.some(Number.isNaN)) return;
    const daily = doses.reduce((a, b) => a + b, 0);
    if (daily > 0 && v.days > 0) lineForm.setFieldValue('quantity', Math.min(10000, Math.ceil(daily * v.days)));
  };

  const addLineToTemplate = (values: LineFormValues) => {
    const med = medicineOptions.find(o => o.id === values.medicineId);
    if (!med) return;
    setTemplateLines(current => [...current, {
      id: crypto.randomUUID(),
      templateId: editingTemplate?.id ?? '',
      medicineId: med.id,
      medicineCode: med.code,
      medicineName: med.name,
      activeIngredient: med.activeIngredient,
      strength: med.strength,
      dosageForm: med.dosageForm,
      unit: med.baseUnit,
      route: values.route || med.route || 'Uống',
      morningDose: values.morningDose || '0',
      noonDose: values.noonDose || '0',
      afternoonDose: values.afternoonDose || '0',
      eveningDose: values.eveningDose || '0',
      mealTiming: values.mealTiming || 'AfterMeal',
      days: values.days || 1,
      quantity: values.quantity,
      instructions: values.instructions ?? med.defaultInstructions ?? '',
      sortOrder: current.length,
    }]);
    setDrawerDirty(true);
    lineForm.resetFields();
  };

  const moveLine = (index: number, delta: number) => {
    setTemplateLines(current => {
      const next = [...current];
      const target = index + delta;
      if (target < 0 || target >= next.length) return current;
      [next[index], next[target]] = [next[target], next[index]];
      return next;
    });
    setDrawerDirty(true);
  };

  const saveTemplate = async (values: TemplateFormValues) => {
    if (savingRef.current) return;
    if (templateLines.length === 0) {
      setSaveError(new ApiError(400, 'MSG02', 'Mẫu đơn thuốc cần ít nhất một thuốc.'));
      return;
    }
    const body = {
      expectedVersion: editingTemplate?.version ?? 0,
      code: values.code.trim(),
      name: values.name.trim(),
      diagnosisKind: values.diagnosisKind?.trim() || null,
      description: values.description?.trim() || null,
      lines: templateLines.map((l, i) => ({
        medicineId: l.medicineId, route: l.route, morningDose: l.morningDose, noonDose: l.noonDose,
        afternoonDose: l.afternoonDose, eveningDose: l.eveningDose, mealTiming: l.mealTiming,
        days: l.days, quantity: l.quantity, instructions: l.instructions, sortOrder: i,
      })),
    };
    const target = editingTemplate ? `/api/prescriptions/templates/${editingTemplate.id}` : '/api/prescriptions/templates';
    const fingerprint = JSON.stringify({ target, ...body });
    if (pending.current?.fingerprint !== fingerprint) pending.current = { fingerprint, key: crypto.randomUUID() };

    savingRef.current = true;
    setSaving(true);
    setSaveError(undefined);
    try {
      await api(target, { method: editingTemplate ? 'PUT' : 'POST', body: JSON.stringify({ ...body, requestKey: pending.current.key }) });
      pending.current = null;
      setDrawerDirty(false);
      setDrawerOpen(false);
      void loadTemplates();
    } catch (e) {
      setSaveError(e as ApiError);
    } finally {
      savingRef.current = false;
      setSaving(false);
    }
  };

  const changeState = async () => {
    if (!stateTarget || stateBusy) return;
    const active = !stateTarget.isActive;
    if (!active && !stateReason.trim()) return;
    setStateBusy(true);
    setStateError(undefined);
    try {
      await api(`/api/prescriptions/templates/${stateTarget.id}/state`, {
        method: 'POST',
        body: JSON.stringify({ requestKey: crypto.randomUUID(), expectedVersion: stateTarget.version, active, reason: stateReason.trim() || null }),
      });
      setStateTarget(undefined);
      setStateReason('');
      void loadTemplates();
    } catch (e) {
      const err = e as ApiError;
      setStateError(err.code === 'MSG111' ? 'Mẫu vừa được thay đổi ở nơi khác. Đóng hộp thoại và tải lại danh sách.' : err.message);
    } finally {
      setStateBusy(false);
    }
  };

  const total = (page - 1) * pageSize + templates.length + (templates.length === pageSize ? 1 : 0);

  return (
    <main className="page">
      <Space wrap style={{ marginBottom: 16, justifyContent: 'space-between', width: '100%' }}>
        <div>
          <Typography.Title level={2} style={{ margin: 0 }}>Mẫu đơn thuốc</Typography.Title>
          <Typography.Text type="secondary">Danh sách thuốc, liều dùng gợi ý theo chẩn đoán thường gặp; dùng chung cho mọi chi nhánh.</Typography.Text>
        </div>
        {canManage && <Button type="primary" onClick={() => openDrawer(null)}>Tạo mẫu đơn thuốc</Button>}
      </Space>

      <Card style={{ marginBottom: 16 }}>
        <Row gutter={[16, 12]}>
          <Col xs={24} md={8}>
            <Input.Search placeholder="Tìm theo mã hoặc tên mẫu..." allowClear maxLength={100} onSearch={v => { setPage(1); setSearch(v); }} />
          </Col>
          <Col xs={24} md={8}>
            <Input.Search placeholder="Lọc theo chẩn đoán gợi ý..." allowClear maxLength={100} onSearch={v => { setPage(1); setDiagnosis(v); }} />
          </Col>
          <Col xs={24} md={8}>
            <Segmented value={status} onChange={v => { setPage(1); setStatus(v as StatusFilter); }}
              options={[{ value: 'Active', label: 'Đang dùng' }, { value: 'Inactive', label: 'Ngừng dùng' }, { value: 'All', label: 'Tất cả' }]} />
          </Col>
        </Row>
      </Card>

      {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} action={<Button onClick={() => void loadTemplates()}>Tải lại</Button>} />}

      <Table
        loading={loading}
        rowKey="id"
        dataSource={templates}
        scroll={{ x: 720 }}
        pagination={{
          current: page, pageSize, total, showSizeChanger: true, pageSizeOptions: [20, 50, 100], simple: false,
          onChange: (p, size) => { setPage(size !== pageSize ? 1 : p); setPageSize(size); },
        }}
        columns={[
          { title: 'Mã mẫu', dataIndex: 'code', width: 130, render: v => <code>{v}</code> },
          {
            title: 'Tên mẫu đơn thuốc',
            dataIndex: 'name',
            render: (v, r) => (
              <div>
                <strong>{v}</strong>
                {r.diagnosisKind && <Tag color="magenta" style={{ marginLeft: 8 }}>{r.diagnosisKind}</Tag>}
                {r.description && <div style={{ fontSize: 12, color: '#666' }}>{r.description}</div>}
              </div>
            ),
          },
          { title: 'Số thuốc', width: 100, align: 'center', render: (_, r) => r.lines.length },
          { title: 'Trạng thái', width: 120, render: (_, r) => (r.isActive ? <Tag color="success">Đang dùng</Tag> : <Tag>Ngừng dùng</Tag>) },
          ...(canManage
            ? [{
                title: 'Thao tác',
                width: 200,
                render: (_: unknown, r: PrescriptionTemplate) => (
                  <Space>
                    <Button size="small" onClick={() => openDrawer(r)}>Sửa</Button>
                    <Button size="small" danger={r.isActive} onClick={() => { setStateTarget(r); setStateReason(''); setStateError(undefined); }}>
                      {r.isActive ? 'Ngừng dùng' : 'Khôi phục'}
                    </Button>
                  </Space>
                ),
              }]
            : []),
        ]}
      />

      <Modal
        open={!!stateTarget}
        title={stateTarget?.isActive ? `Ngừng dùng mẫu "${stateTarget?.name}"` : `Khôi phục mẫu "${stateTarget?.name}"`}
        okText={stateTarget?.isActive ? 'Ngừng dùng' : 'Khôi phục'}
        okButtonProps={{ danger: stateTarget?.isActive, disabled: !!stateTarget?.isActive && !stateReason.trim() }}
        confirmLoading={stateBusy}
        onOk={() => void changeState()}
        onCancel={() => { if (!stateBusy) setStateTarget(undefined); }}
      >
        <Typography.Paragraph>
          {stateTarget?.isActive
            ? 'Mẫu sẽ không còn hiện khi kê đơn. Đơn đã kê từ mẫu này giữ nguyên.'
            : 'Mẫu sẽ hiện lại trong danh sách chọn khi kê đơn.'}
        </Typography.Paragraph>
        {stateError && <Alert type="error" showIcon message={stateError} style={{ marginBottom: 12 }} />}
        <Input.TextArea rows={2} maxLength={500} showCount value={stateReason} onChange={e => setStateReason(e.target.value)}
          placeholder={stateTarget?.isActive ? 'Lý do (bắt buộc)' : 'Lý do (không bắt buộc)'} aria-label="Lý do" />
      </Modal>

      <Drawer
        open={drawerOpen}
        onClose={closeDrawer}
        width={780}
        title={editingTemplate ? `Sửa mẫu: ${editingTemplate.name}` : 'Tạo mẫu đơn thuốc'}
        extra={<Button type="primary" onClick={() => templateForm.submit()} loading={saving} disabled={saving}>Lưu mẫu</Button>}
      >
        {saveError && (
          <Alert type="error" showIcon style={{ marginBottom: 16 }}
            message={saveError.code === 'MSG111' ? 'Mẫu đã được thay đổi ở nơi khác. Nội dung bạn nhập vẫn được giữ; đóng và mở lại để xem bản mới trước khi lưu.' : saveError.message} />
        )}

        <Form form={templateForm} layout="vertical" onFinish={values => void saveTemplate(values)} onValuesChange={() => setDrawerDirty(true)} disabled={saving}>
          <Row gutter={12}>
            <Col xs={24} sm={8}>
              <Form.Item name="code" label="Mã mẫu" rules={[{ required: true, whitespace: true, message: 'Nhập mã mẫu.' }, { max: 40, message: 'Tối đa 40 ký tự.' }]}>
                <Input disabled={!!editingTemplate} placeholder="VD: RX-NHO-RANG-KHON" />
              </Form.Item>
            </Col>
            <Col xs={24} sm={16}>
              <Form.Item name="name" label="Tên mẫu" rules={[{ required: true, whitespace: true, message: 'Nhập tên mẫu.' }, { max: 200, message: 'Tối đa 200 ký tự.' }]}>
                <Input placeholder="VD: Đơn thuốc sau nhổ răng khôn" />
              </Form.Item>
            </Col>
          </Row>
          <Form.Item name="diagnosisKind" label="Chẩn đoán gợi ý" rules={[{ max: 100, message: 'Tối đa 100 ký tự.' }]}>
            <Input placeholder="VD: Sau nhổ răng số 8, Viêm quanh cuống, Cấy ghép Implant" />
          </Form.Item>
          <Form.Item name="description" label="Ghi chú lâm sàng" rules={[{ max: 1000, message: 'Tối đa 1.000 ký tự.' }]}>
            <Input.TextArea rows={2} placeholder="Trường hợp áp dụng, lưu ý khi dùng mẫu..." />
          </Form.Item>
        </Form>

        <Typography.Title level={5} style={{ marginTop: 16 }}>Thuốc trong mẫu ({templateLines.length}/30)</Typography.Title>
        <Table
          size="small"
          pagination={false}
          rowKey="id"
          dataSource={templateLines}
          scroll={{ x: 620 }}
          columns={[
            {
              title: 'Thuốc',
              render: (_, l) => (
                <div>
                  <strong>{l.medicineName}</strong>
                  <div style={{ fontSize: 12, color: '#666' }}>{[l.medicineCode, l.strength, l.dosageForm, l.route].filter(Boolean).join(' · ')}</div>
                </div>
              ),
            },
            {
              title: 'Liều dùng',
              render: (_, l) => (
                <div>{doseSummary(l)}<div><Tag>{mealLabel(l.mealTiming)}</Tag><Tag>{l.days} ngày</Tag></div></div>
              ),
            },
            { title: 'Số lượng', width: 100, render: (_, l) => <strong>{l.quantity} {l.unit}</strong> },
            {
              title: '',
              width: 150,
              render: (_, l, index) => (
                <Space size={4}>
                  <Button size="small" disabled={saving || index === 0} onClick={() => moveLine(index, -1)} aria-label="Lên">↑</Button>
                  <Button size="small" disabled={saving || index === templateLines.length - 1} onClick={() => moveLine(index, 1)} aria-label="Xuống">↓</Button>
                  <Button size="small" danger disabled={saving} onClick={() => { setTemplateLines(c => c.filter(x => x.id !== l.id)); setDrawerDirty(true); }}>Xóa</Button>
                </Space>
              ),
            },
          ]}
        />

        <Card size="small" title="Thêm thuốc vào mẫu" style={{ marginTop: 16 }}>
          <Form
            form={lineForm}
            layout="vertical"
            disabled={saving || templateLines.length >= 30}
            initialValues={{ morningDose: '1', noonDose: '0', afternoonDose: '0', eveningDose: '1', mealTiming: 'AfterMeal', days: 5, quantity: 10 }}
            onValuesChange={changed => { if (['morningDose', 'noonDose', 'afternoonDose', 'eveningDose', 'days'].some(k => k in changed)) recalculate(); }}
            onFinish={addLineToTemplate}
          >
            <Form.Item name="medicineId" label="Thuốc" rules={[{ required: true, message: 'Chọn thuốc.' }]}>
              <Select
                showSearch
                filterOption={false}
                loading={medicineLoading}
                onSearch={v => setMedicineQuery(v.slice(0, 100))}
                placeholder="Tìm mã, tên biệt dược hoặc hoạt chất..."
                options={medicineOptions.map(o => ({ value: o.id, label: [o.code, o.name, o.activeIngredient, o.strength].filter(Boolean).join(' · ') }))}
                onChange={id => {
                  const opt = medicineOptions.find(o => o.id === id);
                  if (opt) lineForm.setFieldsValue({ route: opt.route || undefined, instructions: opt.defaultInstructions ?? '' });
                }}
              />
            </Form.Item>
            <Row gutter={12}>
              <Col xs={12} sm={6}>
                <Form.Item name="route" label="Đường dùng">
                  <Select allowClear placeholder="Theo danh mục" options={['Uống', 'Bôi', 'Súc miệng', 'Ngậm', 'Nhỏ', 'Đặt'].map(value => ({ value, label: value }))} />
                </Form.Item>
              </Col>
              <Col xs={12} sm={6}>
                <Form.Item name="mealTiming" label="Thời điểm">
                  <Select options={[{ value: 'AfterMeal', label: 'Sau ăn' }, { value: 'BeforeMeal', label: 'Trước ăn' }, { value: 'WithMeal', label: 'Trong bữa ăn' }]} />
                </Form.Item>
              </Col>
              <Col xs={12} sm={6}>
                <Form.Item name="days" label="Số ngày" rules={[{ required: true, message: 'Nhập số ngày.' }]}>
                  <InputNumber min={1} max={365} precision={0} style={{ width: '100%' }} />
                </Form.Item>
              </Col>
              <Col xs={12} sm={6}>
                <Form.Item name="quantity" label="Tổng số lượng" rules={[{ required: true, message: 'Nhập số lượng.' }]}>
                  <InputNumber min={1} max={10000} precision={0} style={{ width: '100%' }} />
                </Form.Item>
              </Col>
            </Row>
            <Row gutter={12}>
              {([['morningDose', 'Sáng'], ['noonDose', 'Trưa'], ['afternoonDose', 'Chiều'], ['eveningDose', 'Tối']] as const).map(([name, label]) => (
                <Col xs={12} sm={6} key={name}>
                  <Form.Item name={name} label={label} rules={[{ max: 20, message: 'Tối đa 20 ký tự.' }]}><Input placeholder="0" /></Form.Item>
                </Col>
              ))}
            </Row>
            <Form.Item name="instructions" label="Lời dặn" rules={[{ max: 1000, message: 'Tối đa 1.000 ký tự.' }]}>
              <Input.TextArea rows={2} placeholder="Lời dặn dùng thuốc..." />
            </Form.Item>
            <Button type="dashed" htmlType="submit" block>Thêm thuốc vào mẫu</Button>
          </Form>
        </Card>
      </Drawer>
    </main>
  );
}
