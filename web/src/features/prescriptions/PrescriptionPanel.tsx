import { Alert, Button, Card, Col, Empty, Form, Input, InputNumber, Modal, Row, Select, Space, Spin, Table, Tag, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api, ApiError } from '../../services/api';
import { usePermissions } from '../authentication/usePermissions';
import PrescriptionPrintModal from './PrescriptionPrintModal';
import PrescriptionTemplateModal from './PrescriptionTemplateModal';
import type { ClinicalAllergyWarning, MedicineOption, PrescriptionLine } from './types';

type FormValues = {
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

type Props = {
  visitId: string;
  branchId: number;
  readOnly?: boolean;
  disabled?: boolean;
  initiallyExpanded?: boolean;
  onDirtyChange?: (dirty: boolean) => void;
  onBusyChange?: (busy: boolean) => void;
};

export default function PrescriptionPanel({
  visitId,
  branchId,
  readOnly = false,
  disabled = false,
  initiallyExpanded = false,
  onDirtyChange,
  onBusyChange,
}: Props) {
  const has = usePermissions();
  const canManage = has('prescriptions.manage');
  const effectiveReadOnly = readOnly || !canManage;

  const [lines, setLines] = useState<PrescriptionLine[] | null>(null);
  const [loadError, setLoadError] = useState<string>();
  const [expanded, setExpanded] = useState(initiallyExpanded);
  const [options, setOptions] = useState<MedicineOption[]>([]);
  const [query, setQuery] = useState('');
  const [optionsLoading, setOptionsLoading] = useState(false);
  const [optionsError, setOptionsError] = useState<string>();

  const [editing, setEditing] = useState<PrescriptionLine>();
  const [saveError, setSaveError] = useState<ApiError>();
  const [busy, setBusy] = useState(false);
  const [dirty, setDirty] = useState(false);

  // Modals
  const [templateModalOpen, setTemplateModalOpen] = useState(false);
  const [printModalOpen, setPrintModalOpen] = useState(false);
  const [safetyWarnings, setSafetyWarnings] = useState<ClinicalAllergyWarning[]>([]);

  const [form] = Form.useForm<FormValues>();
  const generation = useRef(0);
  const saving = useRef(false);
  const pending = useRef<{ fingerprint: string; key: string } | undefined>(undefined);
  const path = `/api/prescriptions/visits/${visitId}/lines`;

  useEffect(() => { onDirtyChange?.(dirty); }, [dirty, onDirtyChange]);
  useEffect(() => () => { onDirtyChange?.(false); }, [onDirtyChange]);
  useEffect(() => { onBusyChange?.(busy); }, [busy, onBusyChange]);
  useEffect(() => () => { onBusyChange?.(false); }, [onBusyChange]);

  const loadLines = async (token = generation.current) => {
    setLoadError(undefined);
    try {
      const rows = await api<PrescriptionLine[]>(path);
      if (token !== generation.current) return;
      setLines(rows);
      setExpanded(v => v || rows.length > 0);
      void checkSafety();
      return rows;
    } catch (e) {
      if (token === generation.current) setLoadError((e as Error).message);
    }
  };

  const checkSafety = async () => {
    try {
      const warnings = await api<ClinicalAllergyWarning[]>(`/api/prescriptions/visits/${visitId}/safety-check`);
      setSafetyWarnings(warnings);
    } catch {
      // Safety check non-blocking
    }
  };

  useEffect(() => {
    const token = ++generation.current;
    setLines(null);
    setEditing(undefined);
    setDirty(false);
    setBusy(false);
    saving.current = false;
    pending.current = undefined;
    setSaveError(undefined);
    setExpanded(initiallyExpanded);
    setQuery('');
    setOptions([]);
    form.resetFields();
    void loadLines(token);
    return () => { generation.current++; };
  }, [visitId, branchId]);

  useEffect(() => {
    if (effectiveReadOnly) {
      setEditing(undefined);
      setDirty(false);
      pending.current = undefined;
      form.resetFields();
    }
  }, [effectiveReadOnly, form]);

  useEffect(() => {
    if (!expanded || effectiveReadOnly) return;
    let active = true;
    setOptionsLoading(true);
    setOptionsError(undefined);
    const timer = window.setTimeout(() => {
      void api<MedicineOption[]>(`/api/prescriptions/medicine-options?${new URLSearchParams({ branchId: String(branchId), q: query })}`)
        .then(rows => { if (active) setOptions(rows); })
        .catch(e => { if (active) { setOptions([]); setOptionsError((e as Error).message); } })
        .finally(() => { if (active) setOptionsLoading(false); });
    }, 250);
    return () => { active = false; window.clearTimeout(timer); };
  }, [expanded, effectiveReadOnly, branchId, visitId, query]);

  const isFinalized = lines?.some(l => l.status === 'Finalized') ?? false;

  const reset = () => {
    form.resetFields();
    setEditing(undefined);
    setDirty(false);
    setSaveError(undefined);
    pending.current = undefined;
  };

  const edit = (line: PrescriptionLine) => {
    if (isFinalized) {
      Modal.warning({
        title: 'Đơn thuốc đã hoàn tất / Ký số',
        content: 'Đơn thuốc này đã được bác sĩ ký hoàn tất chính thức. Để bảo đảm an toàn hồ sơ bệnh án, không được sửa trực tiếp dòng thuốc đã ký.',
      });
      return;
    }
    if (dirty && !window.confirm('Bỏ phần kê đơn chưa lưu để sửa dòng thuốc này?')) return;
    setExpanded(true);
    setEditing(line);
    setSaveError(undefined);
    setDirty(false);
    pending.current = undefined;
    form.setFieldsValue({
      medicineId: line.medicineId,
      route: line.route,
      morningDose: line.morningDose,
      noonDose: line.noonDose,
      afternoonDose: line.afternoonDose,
      eveningDose: line.eveningDose,
      mealTiming: line.mealTiming,
      days: line.days,
      quantity: line.quantity,
      instructions: line.instructions,
    });
  };

  const deleteLine = async (line: PrescriptionLine) => {
    if (isFinalized) return;
    if (!window.confirm(`Xác nhận xóa thuốc "${line.medicineName}" khỏi đơn?`)) return;
    setBusy(true);
    try {
      await api(`${path}/${line.id}?requestKey=${crypto.randomUUID()}&expectedVersion=${line.version}`, { method: 'DELETE' });
      setLines(current => (current ?? []).filter(l => l.id !== line.id));
      void checkSafety();
    } catch (e) {
      setSaveError(e as ApiError);
    } finally {
      setBusy(false);
    }
  };

  // Recalculate quantity when doses or days change
  const handleDoseChange = () => {
    const values = form.getFieldsValue();
    const m = parseFloat(values.morningDose) || 0;
    const n = parseFloat(values.noonDose) || 0;
    const a = parseFloat(values.afternoonDose) || 0;
    const e = parseFloat(values.eveningDose) || 0;
    const days = values.days || 1;
    const dailyTotal = m + n + a + e;
    if (dailyTotal > 0) {
      const calculated = Math.ceil(dailyTotal * days);
      form.setFieldValue('quantity', calculated);
    }
  };

  const save = async (values: FormValues) => {
    if (effectiveReadOnly || disabled || saving.current || lines == null || loadError) return;
    const token = generation.current;
    const body = {
      expectedVersion: editing?.version ?? 0,
      medicineId: values.medicineId,
      route: values.route || 'Uống',
      morningDose: values.morningDose || '0',
      noonDose: values.noonDose || '0',
      afternoonDose: values.afternoonDose || '0',
      eveningDose: values.eveningDose || '0',
      mealTiming: values.mealTiming || 'AfterMeal',
      days: values.days || 1,
      quantity: values.quantity,
      instructions: values.instructions ?? '',
    };
    const target = editing ? `${path}/${editing.id}` : path;
    const fingerprint = JSON.stringify({ target, ...body });
    if (pending.current?.fingerprint !== fingerprint) {
      pending.current = { fingerprint, key: crypto.randomUUID() };
    }

    saving.current = true;
    setBusy(true);
    setSaveError(undefined);
    try {
      const saved = await api<PrescriptionLine>(target, {
        method: editing ? 'PUT' : 'POST',
        body: JSON.stringify({ ...body, requestKey: pending.current.key }),
      });
      if (token !== generation.current) return;
      setLines(current =>
        current?.some(line => line.id === saved.id)
          ? current.map(line => (line.id === saved.id ? saved : line))
          : [...(current ?? []), saved]
      );
      reset();
      void checkSafety();
    } catch (e) {
      if (token === generation.current) setSaveError(e as ApiError);
    } finally {
      if (token === generation.current) {
        saving.current = false;
        setBusy(false);
      }
    }
  };

  const applyTemplate = async (templateId: string) => {
    setBusy(true);
    setSaveError(undefined);
    try {
      const updatedLines = await api<PrescriptionLine[]>(`/api/prescriptions/visits/${visitId}/apply-template`, {
        method: 'POST',
        body: JSON.stringify({ requestKey: crypto.randomUUID(), expectedVersion: 0, templateId }),
      });
      setLines(updatedLines);
      void checkSafety();
    } catch (e) {
      setSaveError(e as ApiError);
    } finally {
      setBusy(false);
    }
  };

  const finalizePrescription = async () => {
    if (!lines || lines.length === 0) return;
    if (!window.confirm('Xác nhận hoàn tất và ký số đơn thuốc? Đơn thuốc sau khi ký sẽ được chuyển cho bệnh nhân xem trên ứng dụng và không thể sửa trực tiếp.')) return;
    setBusy(true);
    setSaveError(undefined);
    try {
      const finalizedLines = await api<PrescriptionLine[]>(`/api/prescriptions/visits/${visitId}/finalize`, {
        method: 'POST',
        body: JSON.stringify({ requestKey: crypto.randomUUID(), expectedVersion: 0, clinicalNote: 'Bác sĩ hoàn tất kê đơn' }),
      });
      setLines(finalizedLines);
      reset();
      Modal.success({
        title: 'Hoàn tất kê đơn thuốc thành công',
        content: 'Đơn thuốc đã chuyển trạng thái Finalized và sẵn sàng để in theo quy chuẩn Bộ Y Tế hoặc gửi bệnh nhân.',
      });
    } catch (e) {
      setSaveError(e as ApiError);
    } finally {
      setBusy(false);
    }
  };

  const selectedOptions = [...options];
  if (editing && !selectedOptions.some(o => o.id === editing.medicineId)) {
    selectedOptions.push({
      id: editing.medicineId,
      code: editing.medicineCode,
      name: editing.medicineName,
      strength: editing.strength,
      dosageForm: editing.dosageForm,
      baseUnit: editing.unit,
      route: editing.route,
    });
  }

  return (
    <section aria-label="Đơn thuốc khám">
      <Card
        size="small"
        title={
          <Space>
            <span>Đơn thuốc (Mua ngoài)</span>
            {isFinalized ? (
              <Tag color="success">ĐÃ HOÀN TẤT / KÝ SỐ</Tag>
            ) : lines && lines.length > 0 ? (
              <Tag color="processing">BẢN NHÁP (DRAFT)</Tag>
            ) : null}
          </Space>
        }
        style={{ margin: '16px 0', borderColor: isFinalized ? '#52c41a' : undefined }}
        extra={
          lines && !loadError ? (
            <Space>
              {lines.length > 0 && (
                <Button size="small" type="default" onClick={() => setPrintModalOpen(true)}>
                  In đơn thuốc (BYT)
                </Button>
              )}
              {!effectiveReadOnly && !isFinalized && (
                <Button size="small" onClick={() => setTemplateModalOpen(true)}>
                  Chọn mẫu đơn thuốc
                </Button>
              )}
              {!effectiveReadOnly && !isFinalized && lines.length > 0 && (
                <Button size="small" type="primary" onClick={finalizePrescription} loading={busy}>
                  Hoàn tất kê đơn (Ký số)
                </Button>
              )}
              <Button type="link" size="small" disabled={busy} aria-expanded={expanded} onClick={() => setExpanded(v => !v)}>
                {expanded ? 'Thu gọn' : lines.length ? 'Xem đơn thuốc' : 'Kê đơn thuốc'}
              </Button>
            </Space>
          ) : undefined
        }
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
          Đơn thuốc chỉ định mua ngoài tại hiệu thuốc tư nhân. Phòng khám không bán thuốc tại chỗ.
        </Typography.Paragraph>

        {/* Safety Warnings */}
        {safetyWarnings.map((w, idx) => (
          <Alert
            key={idx}
            type={w.warningType === 'Allergy' ? 'error' : 'warning'}
            showIcon
            message={w.warningType === 'Allergy' ? 'CẢNH BÁO AN TOÀN LÂM SÀNG: TIỀN SỬ DỊ ỨNG' : 'LƯU Ý LÂM SÀNG: TRÙNG LẶP HOẠT CHẤT'}
            description={w.message}
            style={{ marginBottom: 12 }}
          />
        ))}

        {loadError ? (
          <Alert
            type="error"
            showIcon
            message="Không tải được đơn thuốc"
            description={loadError}
            action={<Button onClick={() => void loadLines()}>Thử tải lại</Button>}
          />
        ) : lines == null ? (
          <Spin />
        ) : !expanded ? (
          <Typography.Text type="secondary">
            {lines.length ? `Đã kê ${lines.length} dòng thuốc.` : 'Chưa kê thuốc cho lượt khám này.'}
          </Typography.Text>
        ) : (
          <>
            {lines.length > 0 ? (
              <Table
                size="small"
                rowKey="id"
                pagination={false}
                dataSource={lines}
                scroll={{ x: 720 }}
                columns={[
                  {
                    title: 'STT',
                    width: 50,
                    render: (_, __, idx) => idx + 1,
                  },
                  {
                    title: 'Thuốc',
                    render: (_, line: PrescriptionLine) => (
                      <>
                        <strong>{line.medicineName}</strong>
                        <div>{[line.medicineCode, line.strength, line.dosageForm, line.route].filter(Boolean).join(' · ')}</div>
                      </>
                    ),
                  },
                  {
                    title: 'Liều dùng cụ thể',
                    render: (_, line: PrescriptionLine) => (
                      <div>
                        {[
                          line.morningDose !== '0' ? `Sáng ${line.morningDose}` : null,
                          line.noonDose !== '0' ? `Trưa ${line.noonDose}` : null,
                          line.afternoonDose !== '0' ? `Chiều ${line.afternoonDose}` : null,
                          line.eveningDose !== '0' ? `Tối ${line.eveningDose}` : null,
                        ].filter(Boolean).join(', ') || 'Theo chỉ dẫn'}
                        <div>
                          <Tag color="blue">
                            {line.mealTiming === 'BeforeMeal' ? 'Trước ăn' : line.mealTiming === 'WithMeal' ? 'Trong ăn' : 'Sau ăn'}
                          </Tag>
                          <Tag>{line.days} ngày</Tag>
                        </div>
                      </div>
                    ),
                  },
                  {
                    title: 'Số lượng',
                    width: 100,
                    render: (_, line: PrescriptionLine) => <strong>{line.quantity} {line.unit}</strong>,
                  },
                  {
                    title: 'Lời dặn',
                    dataIndex: 'instructions',
                  },
                  ...(!effectiveReadOnly && !isFinalized
                    ? [
                        {
                          title: 'Thao tác',
                          width: 140,
                          render: (_: unknown, line: PrescriptionLine) => (
                            <Space size="small">
                              <Button size="small" disabled={busy || disabled} onClick={() => edit(line)}>
                                Sửa
                              </Button>
                              <Button size="small" danger disabled={busy || disabled} onClick={() => deleteLine(line)}>
                                Xóa
                              </Button>
                            </Space>
                          ),
                        },
                      ]
                    : []),
                ]}
              />
            ) : (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Chưa có thuốc trong đơn" />
            )}

            {!effectiveReadOnly && !isFinalized && (
              <div style={{ marginTop: 16, paddingTop: 16, borderTop: '1px solid #f0f0f0' }}>
                <Typography.Title level={5}>
                  {editing ? `Sửa dòng thuốc: ${editing.medicineName}` : 'Thêm thuốc vào đơn'}
                </Typography.Title>

                {optionsError && (
                  <Alert
                    type="error"
                    message="Không tải được danh mục thuốc"
                    description={optionsError}
                    style={{ marginBottom: 12 }}
                  />
                )}

                {saveError && (
                  <Alert
                    role="alert"
                    type="error"
                    showIcon
                    message={
                      saveError.code === 'MSG111'
                        ? 'Đơn thuốc đã thay đổi trên máy chủ. Phần nhập của bạn được giữ nguyên; vui lòng kiểm tra lại trước khi lưu.'
                        : saveError.message
                    }
                    style={{ marginBottom: 12 }}
                  />
                )}

                <Form
                  form={form}
                  name={`prescription-${visitId}`}
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
                  disabled={busy || disabled}
                  onValuesChange={() => {
                    setDirty(true);
                    handleDoseChange();
                  }}
                  onFinish={save}
                >
                  <Form.Item name="medicineId" label="Thuốc kê đơn" rules={[{ required: true, message: 'Chọn thuốc cần kê.' }]}>
                    <Select
                      showSearch
                      filterOption={false}
                      loading={optionsLoading}
                      onSearch={setQuery}
                      placeholder="Tìm mã, biệt dược hoặc hoạt chất thuốc..."
                      options={selectedOptions.map(o => ({
                        value: o.id,
                        label: [o.code, o.name, o.strength, o.dosageForm].filter(Boolean).join(' · '),
                      }))}
                      onChange={medicineId => {
                        const opt = options.find(item => item.id === medicineId);
                        if (opt && !editing) {
                          form.setFieldsValue({
                            route: opt.route || 'Uống',
                            instructions: opt.defaultInstructions ?? '',
                          });
                        }
                      }}
                    />
                  </Form.Item>

                  <Row gutter={12}>
                    <Col xs={12} sm={6}>
                      <Form.Item name="route" label="Đường dùng">
                        <Select
                          options={[
                            { value: 'Uống', label: 'Uống' },
                            { value: 'Bôi', label: 'Bôi ngoài da / niêm mạc' },
                            { value: 'Súc miệng', label: 'Súc miệng' },
                            { value: 'Ngậm', label: 'Ngậm dưới lưỡi' },
                            { value: 'Nhỏ', label: 'Nhỏ' },
                            { value: 'Tiêm', label: 'Tiêm' },
                          ]}
                        />
                      </Form.Item>
                    </Col>
                    <Col xs={12} sm={6}>
                      <Form.Item name="mealTiming" label="Thời điểm uống">
                        <Select
                          options={[
                            { value: 'AfterMeal', label: 'Sau ăn' },
                            { value: 'BeforeMeal', label: 'Trước ăn' },
                            { value: 'WithMeal', label: 'Trong bữa ăn' },
                          ]}
                        />
                      </Form.Item>
                    </Col>
                    <Col xs={12} sm={6}>
                      <Form.Item name="days" label="Số ngày dùng" rules={[{ required: true, message: 'Nhập số ngày.' }]}>
                        <InputNumber min={1} max={365} precision={0} style={{ width: '100%' }} />
                      </Form.Item>
                    </Col>
                    <Col xs={12} sm={6}>
                      <Form.Item name="quantity" label="Tổng số lượng" rules={[{ required: true, message: 'Nhập số lượng.' }, { type: 'integer', min: 1, max: 10000, message: '1-10.000' }]}>
                        <InputNumber min={1} max={10000} precision={0} style={{ width: '100%' }} />
                      </Form.Item>
                    </Col>
                  </Row>

                  <Row gutter={12}>
                    <Col xs={6}>
                      <Form.Item name="morningDose" label="Sáng">
                        <Input placeholder="1 viên" />
                      </Form.Item>
                    </Col>
                    <Col xs={6}>
                      <Form.Item name="noonDose" label="Trưa">
                        <Input placeholder="0" />
                      </Form.Item>
                    </Col>
                    <Col xs={6}>
                      <Form.Item name="afternoonDose" label="Chiều">
                        <Input placeholder="0" />
                      </Form.Item>
                    </Col>
                    <Col xs={6}>
                      <Form.Item name="eveningDose" label="Tối">
                        <Input placeholder="1 viên" />
                      </Form.Item>
                    </Col>
                  </Row>

                  <Form.Item name="instructions" label="Lời dặn dùng thuốc" rules={[{ max: 1000, message: 'Lời dặn tối đa 1.000 ký tự.' }]}>
                    <Input.TextArea rows={2} maxLength={1000} placeholder="Ví dụ: Uống nhiều nước, không uống rượu bia trong thời gian dùng thuốc..." />
                  </Form.Item>

                  <Space wrap>
                    <Button type="primary" htmlType="submit" loading={busy} disabled={busy || disabled}>
                      {editing ? 'Lưu thay đổi thuốc' : 'Thêm vào đơn'}
                    </Button>
                    {editing && (
                      <Button disabled={busy || disabled} onClick={reset}>
                        Hủy sửa
                      </Button>
                    )}
                  </Space>
                </Form>
              </div>
            )}
          </>
        )}
      </Card>

      <PrescriptionTemplateModal
        open={templateModalOpen}
        onClose={() => setTemplateModalOpen(false)}
        onApply={applyTemplate}
      />

      <PrescriptionPrintModal
        visitId={visitId}
        open={printModalOpen}
        onClose={() => setPrintModalOpen(false)}
      />
    </section>
  );
}
