import {
  Alert,
  Button,
  Card,
  Col,
  Drawer,
  Form,
  Input,
  InputNumber,
  Modal,
  Popconfirm,
  Row,
  Select,
  Space,
  Switch,
  Table,
  Tag,
  Typography,
} from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api, ApiError } from '../../services/api';
import { usePermissions } from '../authentication/usePermissions';
import type { MedicineOption, PrescriptionTemplate, PrescriptionTemplateLine } from './types';

type LineFormValues = {
  medicineId: string;
  route: string;
  morningDose: string;
  noonDose: string;
  afternoonDose: string;
  eveningDose: string;
  mealTiming: string;
  days: number;
  quantity: number;
  instructions?: string;
};

type TemplateFormValues = {
  code: string;
  name: string;
  diagnosisKind?: string;
  description?: string;
};

export default function PrescriptionTemplatesPage() {
  const has = usePermissions();
  const canManage = has('prescriptions.manage');

  const [templates, setTemplates] = useState<PrescriptionTemplate[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string>();
  const [search, setSearch] = useState('');
  const [diagnosisFilter, setDiagnosisFilter] = useState<string>();

  // Drawer edit/create
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [editingTemplate, setEditingTemplate] = useState<PrescriptionTemplate | null>(null);
  const [templateLines, setTemplateLines] = useState<PrescriptionTemplateLine[]>([]);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<ApiError>();

  // Line form inside drawer
  const [medicineOptions, setMedicineOptions] = useState<MedicineOption[]>([]);
  const [medicineQuery, setMedicineQuery] = useState('');
  const [medicineLoading, setMedicineLoading] = useState(false);
  const [templateForm] = Form.useForm<TemplateFormValues>();
  const [lineForm] = Form.useForm<LineFormValues>();
  const pending = useRef<{ fingerprint: string; key: string } | null>(null);

  const loadTemplates = async () => {
    setLoading(true);
    setError(undefined);
    try {
      const query = new URLSearchParams();
      if (search) query.set('search', search);
      if (diagnosisFilter) query.set('diagnosis', diagnosisFilter);
      const rows = await api<PrescriptionTemplate[]>(`/api/prescriptions/templates?${query}`);
      setTemplates(rows);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadTemplates();
  }, [search, diagnosisFilter]);

  // Load medicine options
  useEffect(() => {
    if (!drawerOpen) return;
    let active = true;
    setMedicineLoading(true);
    const timer = setTimeout(() => {
      void api<MedicineOption[]>(`/api/prescriptions/medicine-options?branchId=1&q=${medicineQuery}`)
        .then(rows => { if (active) setMedicineOptions(rows); })
        .catch(() => { if (active) setMedicineOptions([]); })
        .finally(() => { if (active) setMedicineLoading(false); });
    }, 250);
    return () => { active = false; clearTimeout(timer); };
  }, [drawerOpen, medicineQuery]);

  const openCreate = () => {
    setEditingTemplate(null);
    setTemplateLines([]);
    templateForm.resetFields();
    lineForm.resetFields();
    setSaveError(undefined);
    setDrawerOpen(true);
  };

  const openEdit = (t: PrescriptionTemplate) => {
    setEditingTemplate(t);
    setTemplateLines([...t.lines]);
    templateForm.setFieldsValue({
      code: t.code,
      name: t.name,
      diagnosisKind: t.diagnosisKind,
      description: t.description,
    });
    lineForm.resetFields();
    setSaveError(undefined);
    setDrawerOpen(true);
  };

  const handleLineDoseChange = () => {
    const values = lineForm.getFieldsValue();
    const m = parseFloat(values.morningDose) || 0;
    const n = parseFloat(values.noonDose) || 0;
    const a = parseFloat(values.afternoonDose) || 0;
    const e = parseFloat(values.eveningDose) || 0;
    const days = values.days || 1;
    const daily = m + n + a + e;
    if (daily > 0) {
      lineForm.setFieldValue('quantity', Math.ceil(daily * days));
    }
  };

  const addLineToTemplate = (values: LineFormValues) => {
    const med = medicineOptions.find(o => o.id === values.medicineId);
    if (!med) return;

    const newLine: PrescriptionTemplateLine = {
      id: crypto.randomUUID(),
      templateId: editingTemplate?.id ?? '',
      medicineId: med.id,
      medicineCode: med.Code ?? med.code,
      medicineName: med.Name ?? med.name,
      activeIngredient: med.ActiveIngredient ?? med.activeIngredient,
      strength: med.Strength ?? med.strength,
      dosageForm: med.DosageForm ?? med.dosageForm,
      unit: med.BaseUnit ?? med.baseUnit,
      route: values.route || 'Uống',
      morningDose: values.morningDose || '0',
      noonDose: values.noonDose || '0',
      afternoonDose: values.afternoonDose || '0',
      eveningDose: values.eveningDose || '0',
      mealTiming: values.mealTiming || 'AfterMeal',
      days: values.days || 1,
      quantity: values.quantity,
      instructions: values.instructions ?? med.defaultInstructions ?? '',
      sortOrder: templateLines.length + 1,
    };

    setTemplateLines(current => [...current, newLine]);
    lineForm.resetFields();
  };

  const removeLineFromTemplate = (lineId: string) => {
    setTemplateLines(current => current.filter(l => l.id !== lineId));
  };

  const saveTemplate = async (values: TemplateFormValues) => {
    if (templateLines.length === 0) {
      Modal.error({ title: 'Thiếu dòng thuốc', content: 'Mẫu đơn thuốc cần có ít nhất một loại thuốc.' });
      return;
    }

    setSaving(true);
    setSaveError(undefined);
    const body = {
      expectedVersion: editingTemplate?.version ?? 0,
      code: values.code,
      name: values.name,
      diagnosisKind: values.diagnosisKind,
      description: values.description,
      lines: templateLines.map((l, i) => ({
        medicineId: l.medicineId,
        route: l.route,
        morningDose: l.morningDose,
        noonDose: l.noonDose,
        afternoonDose: l.afternoonDose,
        eveningDose: l.eveningDose,
        mealTiming: l.mealTiming,
        days: l.days,
        quantity: l.quantity,
        instructions: l.instructions,
        sortOrder: i + 1,
      })),
    };

    const target = editingTemplate ? `/api/prescriptions/templates/${editingTemplate.id}` : '/api/prescriptions/templates';
    const fingerprint = JSON.stringify({ target, ...body });
    if (pending.current?.fingerprint !== fingerprint) {
      pending.current = { fingerprint, key: crypto.randomUUID() };
    }

    try {
      await api(target, {
        method: editingTemplate ? 'PUT' : 'POST',
        body: JSON.stringify({ ...body, requestKey: pending.current.key }),
      });
      setDrawerOpen(false);
      void loadTemplates();
    } catch (e) {
      setSaveError(e as ApiError);
    } finally {
      setSaving(false);
    }
  };

  const toggleState = async (t: PrescriptionTemplate, active: boolean) => {
    try {
      await api(`/api/prescriptions/templates/${t.id}/state`, {
        method: 'POST',
        body: JSON.stringify({
          requestKey: crypto.randomUUID(),
          expectedVersion: t.version,
          active,
          reason: active ? 'Kích hoạt lại mẫu' : 'Ngừng sử dụng mẫu',
        }),
      });
      void loadTemplates();
    } catch (e) {
      Modal.error({ title: 'Lỗi cập nhật trạng thái', content: (e as Error).message });
    }
  };

  return (
    <main className="page">
      <Space wrap style={{ marginBottom: 16, justifyContent: 'space-between', width: '100%' }}>
        <div>
          <Typography.Title level={2} style={{ margin: 0 }}>
            Mẫu đơn thuốc chuẩn (Prescription Templates)
          </Typography.Title>
          <Typography.Text type="secondary">
            Thiết lập sẵn danh sách thuốc, liều dùng theo chẩn đoán (Nhổ răng, Viêm quanh cuống, Cấy ghép Implant...)
          </Typography.Text>
        </div>
        {canManage && (
          <Button type="primary" onClick={openCreate}>
            Tạo mẫu đơn thuốc mới
          </Button>
        )}
      </Space>

      <Card style={{ marginBottom: 16 }}>
        <Row gutter={16}>
          <Col xs={24} sm={12} md={8}>
            <Input.Search
              placeholder="Tìm theo tên mẫu hoặc mã..."
              allowClear
              onSearch={setSearch}
            />
          </Col>
          <Col xs={24} sm={12} md={8}>
            <Select
              allowClear
              placeholder="Lọc theo chẩn đoán..."
              style={{ width: '100%' }}
              onChange={setDiagnosisFilter}
              options={[
                { value: 'Nhổ răng', label: 'Nhổ răng khôn / răng ngầm' },
                { value: 'Viêm quanh cuống', label: 'Viêm quanh cuống / tủy' },
                { value: 'Cấy ghép Implant', label: 'Cấy ghép Implant' },
                { value: 'Viêm nướu', label: 'Viêm nướu / nha chu' },
              ]}
            />
          </Col>
        </Row>
      </Card>

      {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} />}

      <Table
        loading={loading}
        rowKey="id"
        dataSource={templates}
        columns={[
          {
            title: 'Mã mẫu',
            dataIndex: 'code',
            width: 120,
            render: v => <code>{v}</code>,
          },
          {
            title: 'Tên mẫu đơn thuốc',
            dataIndex: 'name',
            render: (v, r) => (
              <div>
                <strong>{v}</strong>
                {r.diagnosisKind && (
                  <Tag color="magenta" style={{ marginLeft: 8 }}>
                    {r.diagnosisKind}
                  </Tag>
                )}
                {r.description && <div style={{ fontSize: 12, color: '#666' }}>{r.description}</div>}
              </div>
            ),
          },
          {
            title: 'Số loại thuốc',
            width: 120,
            align: 'center',
            render: (_, r) => <Tag color="blue">{r.lines.length} loại</Tag>,
          },
          {
            title: 'Trạng thái',
            width: 140,
            render: (_, r) => (
              <Switch
                checked={r.isActive}
                disabled={!canManage}
                checkedChildren="Đang dùng"
                unCheckedChildren="Ngừng dùng"
                onChange={checked => void toggleState(r, checked)}
              />
            ),
          },
          ...(canManage
            ? [
                {
                  title: 'Thao tác',
                  width: 120,
                  render: (_: unknown, r: PrescriptionTemplate) => (
                    <Button size="small" onClick={() => openEdit(r)}>
                      Chỉnh sửa
                    </Button>
                  ),
                },
              ]
            : []),
        ]}
      />

      {/* Drawer Create / Edit */}
      <Drawer
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        width={760}
        title={editingTemplate ? `Sửa mẫu đơn thuốc: ${editingTemplate.name}` : 'Tạo mẫu đơn thuốc mới'}
        extra={
          <Button type="primary" onClick={() => templateForm.submit()} loading={saving}>
            Lưu mẫu đơn thuốc
          </Button>
        }
      >
        {saveError && (
          <Alert
            type="error"
            showIcon
            message={saveError.message}
            style={{ marginBottom: 16 }}
          />
        )}

        <Form
          form={templateForm}
          layout="vertical"
          onFinish={saveTemplate}
          initialValues={{ code: `RX-TMP-${Math.floor(100 + Math.random() * 900)}` }}
        >
          <Row gutter={12}>
            <Col span={8}>
              <Form.Item name="code" label="Mã mẫu" rules={[{ required: true, message: 'Nhập mã.' }]}>
                <Input disabled={!!editingTemplate} />
              </Form.Item>
            </Col>
            <Col span={16}>
              <Form.Item name="name" label="Tên mẫu đơn thuốc" rules={[{ required: true, message: 'Nhập tên mẫu.' }]}>
                <Input placeholder="Ví dụ: Đơn thuốc sau nhổ răng khôn" />
              </Form.Item>
            </Col>
          </Row>

          <Form.Item name="diagnosisKind" label="Chẩn đoán lâm sàng gợi ý">
            <Input placeholder="Ví dụ: Sau nhổ răng số 8, Cấy Implant, Viêm tủy..." />
          </Form.Item>

          <Form.Item name="description" label="Ghi chú lâm sàng cho mẫu">
            <Input.TextArea rows={2} placeholder="Mô tả trường hợp áp dụng hoặc lưu ý lâm sàng..." />
          </Form.Item>
        </Form>

        <Typography.Title level={5} style={{ marginTop: 24, marginBottom: 12 }}>
          Danh sách thuốc trong mẫu ({templateLines.length})
        </Typography.Title>

        <Table
          size="small"
          pagination={false}
          rowKey="id"
          dataSource={templateLines}
          columns={[
            {
              title: 'Thuốc',
              render: (_, l) => (
                <div>
                  <strong>{l.medicineName}</strong>
                  <div style={{ fontSize: 12, color: '#666' }}>
                    {[l.medicineCode, l.strength, l.dosageForm].filter(Boolean).join(' · ')}
                  </div>
                </div>
              ),
            },
            {
              title: 'Liều dùng',
              render: (_, l) => (
                <div>
                  {[
                    l.morningDose !== '0' ? `Sáng ${l.morningDose}` : null,
                    l.noonDose !== '0' ? `Trưa ${l.noonDose}` : null,
                    l.afternoonDose !== '0' ? `Chiều ${l.afternoonDose}` : null,
                    l.eveningDose !== '0' ? `Tối ${l.eveningDose}` : null,
                  ].filter(Boolean).join(', ') || 'Chỉ dẫn'}
                  <div>
                    <Tag>{l.mealTiming === 'BeforeMeal' ? 'Trước ăn' : l.mealTiming === 'WithMeal' ? 'Trong ăn' : 'Sau ăn'}</Tag>
                    <Tag>{l.days} ngày</Tag>
                  </div>
                </div>
              ),
            },
            {
              title: 'Số lượng',
              render: (_, l) => <strong>{l.quantity} {l.unit}</strong>,
            },
            {
              title: '',
              width: 60,
              render: (_, l) => (
                <Button size="small" danger onClick={() => removeLineFromTemplate(l.id)}>
                  Xóa
                </Button>
              ),
            },
          ]}
        />

        {/* Add Line Form */}
        <Card size="small" title="Thêm thuốc vào mẫu" style={{ marginTop: 16 }}>
          <Form
            form={lineForm}
            layout="vertical"
            initialValues={{
              route: 'Uống',
              morningDose: '1',
              noonDose: '0',
              afternoonDose: '0',
              eveningDose: '1',
              mealTiming: 'AfterMeal',
              days: 5,
              quantity: 10,
            }}
            onValuesChange={handleLineDoseChange}
            onFinish={addLineToTemplate}
          >
            <Form.Item name="medicineId" label="Chọn thuốc" rules={[{ required: true, message: 'Chọn thuốc.' }]}>
              <Select
                showSearch
                filterOption={false}
                loading={medicineLoading}
                onSearch={setMedicineQuery}
                placeholder="Tìm mã hoặc tên thuốc..."
                options={medicineOptions.map(o => ({
                  value: o.id,
                  label: [o.code, o.name, o.strength, o.dosageForm].filter(Boolean).join(' · '),
                }))}
                onChange={id => {
                  const opt = medicineOptions.find(o => o.id === id);
                  if (opt) {
                    lineForm.setFieldsValue({
                      route: opt.route || 'Uống',
                      instructions: opt.defaultInstructions ?? '',
                    });
                  }
                }}
              />
            </Form.Item>

            <Row gutter={12}>
              <Col span={6}>
                <Form.Item name="route" label="Đường dùng">
                  <Select
                    options={[
                      { value: 'Uống', label: 'Uống' },
                      { value: 'Bôi', label: 'Bôi' },
                      { value: 'Súc miệng', label: 'Súc miệng' },
                      { value: 'Ngậm', label: 'Ngậm' },
                    ]}
                  />
                </Form.Item>
              </Col>
              <Col span={6}>
                <Form.Item name="mealTiming" label="Thời điểm">
                  <Select
                    options={[
                      { value: 'AfterMeal', label: 'Sau ăn' },
                      { value: 'BeforeMeal', label: 'Trước ăn' },
                      { value: 'WithMeal', label: 'Trong ăn' },
                    ]}
                  />
                </Form.Item>
              </Col>
              <Col span={6}>
                <Form.Item name="days" label="Số ngày">
                  <InputNumber min={1} max={30} style={{ width: '100%' }} />
                </Form.Item>
              </Col>
              <Col span={6}>
                <Form.Item name="quantity" label="Tổng SL">
                  <InputNumber min={1} max={1000} style={{ width: '100%' }} />
                </Form.Item>
              </Col>
            </Row>

            <Row gutter={12}>
              <Col span={6}>
                <Form.Item name="morningDose" label="Sáng">
                  <Input placeholder="1 viên" />
                </Form.Item>
              </Col>
              <Col span={6}>
                <Form.Item name="noonDose" label="Trưa">
                  <Input placeholder="0" />
                </Form.Item>
              </Col>
              <Col span={6}>
                <Form.Item name="afternoonDose" label="Chiều">
                  <Input placeholder="0" />
                </Form.Item>
              </Col>
              <Col span={6}>
                <Form.Item name="eveningDose" label="Tối">
                  <Input placeholder="1 viên" />
                </Form.Item>
              </Col>
            </Row>

            <Form.Item name="instructions" label="Lời dặn mặc định">
              <Input.TextArea rows={2} placeholder="Lời dặn dùng thuốc..." />
            </Form.Item>

            <Button type="dashed" htmlType="submit" block>
              + Thêm thuốc này vào danh sách mẫu
            </Button>
          </Form>
        </Card>
      </Drawer>
    </main>
  );
}
