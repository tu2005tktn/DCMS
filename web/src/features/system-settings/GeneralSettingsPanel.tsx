import { Alert, Button, Card, Checkbox, Descriptions, Form, Input, List, Modal, Radio, Select, Skeleton, Space, Table, Tag, Typography, message } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api, ApiError } from '../../services/api';
import PatientCodeSequenceCard from './PatientCodeSequenceCard';
import { codeExample, codeModes, idleChoices, receiptFields, requiredFieldOptions, type GeneralHistoryItem, type GeneralHistoryPage,
  type GeneralPreview, type GeneralSettings, type GeneralSettingsDto } from './generalSettings';

const base = '/api/system-settings/settings/general';
const formatTime = (value: string | null) => value ? new Date(value).toLocaleString('vi-VN') : '—';
type Pending = { kind: 'save' | 'restore'; preview: GeneralPreview; body: Record<string, unknown>; sourceVersion?: number };

// S58 Thiết lập chung: explicit preview + reason + save; nothing is written on tick (UC-51 step 2–5).
export default function GeneralSettingsPanel({ onDirtyChange }: { onDirtyChange: (dirty: boolean) => void }) {
  const [form] = Form.useForm<GeneralSettings>();
  const [data, setData] = useState<GeneralSettingsDto | null>(null), [loading, setLoading] = useState(true), [loadError, setLoadError] = useState<string>();
  const [dirty, setDirty] = useState(false), [busy, setBusy] = useState(false), [error, setError] = useState<ApiError>();
  const [pending, setPending] = useState<Pending | null>(null), [reason, setReason] = useState(''), [reasonError, setReasonError] = useState<string>();
  const [history, setHistory] = useState<GeneralHistoryPage | null>(null), [historyError, setHistoryError] = useState<string>();
  const [historyPage, setHistoryPage] = useState(1), [historySize, setHistorySize] = useState(20), [historyRevision, setHistoryRevision] = useState(0);
  const requestKey = useRef<{ fingerprint: string; key: string } | null>(null), guard = useRef(false);
  const prefix = Form.useWatch(['patientCode', 'prefix'], form) ?? '';
  // Static Modal/message do not render under React 19 + antd 5; use the hook instances.
  const [modal, modalContext] = Modal.useModal();
  const [toast, toastContext] = message.useMessage();

  function markDirty(value: boolean) { setDirty(value); onDirtyChange(value); }

  // keepInput: after MSG111 only the version baseline is refreshed; the draft stays as typed.
  async function load(keepInput = false) {
    setLoading(!keepInput); setLoadError(undefined);
    try {
      const latest = await api<GeneralSettingsDto>(base);
      setData(latest); setError(undefined);
      if (!keepInput) { form.setFieldsValue(latest.settings); markDirty(false); }
    } catch (e) { setLoadError((e as Error).message); }
    finally { setLoading(false); }
  }
  useEffect(() => { void load(); }, []);
  useEffect(() => {
    let live = true; setHistoryError(undefined);
    void api<GeneralHistoryPage>(`${base}/history?page=${historyPage}&pageSize=${historySize}`)
      .then(v => { if (live) setHistory(v); }).catch(e => { if (live) setHistoryError((e as Error).message); });
    return () => { live = false; };
  }, [historyPage, historySize, historyRevision]);

  function showFailure(failure: ApiError) {
    setError(failure);
    if (failure.field && failure.field !== 'reason' && failure.field !== 'requestKey')
      form.setFields([{ name: failure.field.split('.') as never, errors: [failure.message] }]);
  }

  async function previewSave() {
    if (!data || guard.current) return;
    const settings = await form.validateFields().catch(() => null);
    if (!settings) return;
    guard.current = true; setBusy(true); setError(undefined);
    try {
      const body = { expectedVersion: data.version, settings: { ...data.settings, ...settings } };
      const preview = await api<GeneralPreview>(`${base}/preview`, { method: 'POST', body: JSON.stringify(body) });
      setPending({ kind: 'save', preview, body }); setReason(''); setReasonError(undefined);
    } catch (e) { showFailure(e as ApiError); }
    finally { guard.current = false; setBusy(false); }
  }

  async function previewRestore(item: GeneralHistoryItem) {
    if (!data || guard.current) return;
    guard.current = true; setBusy(true); setError(undefined);
    try {
      const body = { sourceVersion: item.version, expectedVersion: data.version };
      const preview = await api<GeneralPreview>(`${base}/restore-preview`, { method: 'POST', body: JSON.stringify(body) });
      setPending({ kind: 'restore', preview, body, sourceVersion: item.version }); setReason(''); setReasonError(undefined);
    } catch (e) { showFailure(e as ApiError); }
    finally { guard.current = false; setBusy(false); }
  }

  async function commit() {
    if (!pending || guard.current) return;
    const why = reason.trim();
    if (!why) { setReasonError('Vui lòng nhập Lý do.'); return; }
    // Same payload keeps the same requestKey so a retry after a timeout cannot write twice (SET-02).
    const fingerprint = JSON.stringify({ kind: pending.kind, body: pending.body, reason: why });
    if (requestKey.current?.fingerprint !== fingerprint) requestKey.current = { fingerprint, key: crypto.randomUUID() };
    guard.current = true; setBusy(true);
    try {
      const saved = await api<GeneralSettingsDto>(pending.kind === 'save' ? base : `${base}/restore`, {
        method: pending.kind === 'save' ? 'PUT' : 'POST', body: JSON.stringify({ ...pending.body, reason: why, requestKey: requestKey.current.key }),
      });
      setData(saved); form.setFieldsValue(saved.settings); markDirty(false); setPending(null); requestKey.current = null; setError(undefined);
      setHistoryPage(1); setHistoryRevision(v => v + 1);
      void toast.success(`Đã lưu Thiết lập chung, phiên bản ${saved.version}.`);
    } catch (e) {
      const failure = e as ApiError;
      if (failure.field === 'reason') setReasonError(failure.message);
      else { setPending(null); showFailure(failure); }
    } finally { guard.current = false; setBusy(false); }
  }

  if (loading) return <Skeleton active paragraph={{ rows: 8 }} />;
  if (loadError || !data) return <Alert type="error" showIcon message={loadError ?? 'Không tải được Thiết lập chung.'}
    action={<Button onClick={() => void load()}>Thử lại</Button>} />;

  return <div className="general-settings">
    {modalContext}{toastContext}
    <Descriptions size="small" column={{ xs: 1, sm: 3 }} items={[
      { key: 'version', label: 'Phiên bản', children: data.version === 0 ? 'Mặc định (chưa lưu lần nào)' : data.version },
      { key: 'effective', label: 'Hiệu lực từ', children: formatTime(data.effectiveAt) },
      { key: 'actor', label: 'Người lưu', children: data.actorName ?? '—' },
    ]} />
    {error && <Alert type="error" showIcon message={error.message}
      action={error.code === 'MSG111' ? <Button onClick={() => void load(true)}>Tải phiên bản mới</Button> : undefined} />}
    <Form<GeneralSettings> form={form} layout="vertical" disabled={busy} onValuesChange={() => markDirty(true)}>
      <Card size="small" title="Tự đăng xuất">
        <Form.Item name="staffIdleTimeoutMinutes" label="Tự đăng xuất khi không thao tác sau" extra="Chỉ áp dụng cho phiên nhân viên trên web; ứng dụng bệnh nhân có chính sách riêng.">
          <Select style={{ maxWidth: 240 }} options={idleChoices.map(value => ({ value, label: `${value} phút` }))} />
        </Form.Item>
        <Alert type="info" showIcon message="Áp dụng cho phiên nhân viên ngay sau khi lưu (DCMS-129); hoạt động đã ghi nhận không bị đặt lại." />
      </Card>
      <Card size="small" title="Hồ sơ bắt buộc">
        <Typography.Paragraph type="secondary">Áp dụng từ lần lưu hồ sơ bệnh nhân tiếp theo. Hồ sơ cũ vẫn xem và khám bình thường, không bị tự điền.</Typography.Paragraph>
        <div className="general-settings-grid">
          {requiredFieldOptions.map(([field, label, available]) =>
            <Form.Item key={field} name={['requiredFields', field]} valuePropName="checked"
              extra={available ? undefined : 'Hồ sơ bệnh nhân chưa có trường này.'}>
              <Checkbox disabled={!available}>{label}</Checkbox>
            </Form.Item>)}
        </div>
      </Card>
      <Card size="small" title="Mã bệnh nhân">
        <Form.Item name={['patientCode', 'mode']} label="Chế độ cấp mã">
          <Radio.Group><Space direction="vertical">
            {codeModes.map(mode => <Radio key={mode.value} value={mode.value}>{mode.label} <Typography.Text type="secondary">· {mode.help}</Typography.Text></Radio>)}
          </Space></Radio.Group>
        </Form.Item>
        <Form.Item name={['patientCode', 'prefix']} label="Tiền tố" extra={`Ví dụ mã tự động: ${codeExample(prefix)}. Chỉ áp dụng cho bệnh nhân tạo mới; mã đã cấp không đổi.`}
          rules={[{ pattern: /^\s*[A-Za-z0-9-]{0,12}\s*$/, message: 'Tiền tố gồm tối đa 12 chữ cái không dấu, số hoặc dấu gạch nối.' }]}>
          <Input style={{ maxWidth: 240 }} maxLength={14} placeholder="Không có tiền tố" />
        </Form.Item>
        <PatientCodeSequenceCard revision={data.version} />
      </Card>
      <Card size="small" title="Mẫu in phiếu thu">
        <Alert type="info" showIcon message="Áp dụng cho lần in phiếu thu tiếp theo (DCMS-130). Tùy chọn chỉ ẩn/hiện thông tin; số tiền, số phiếu và người nộp/thu của phiếu đã phát hành giữ nguyên." />
        <div className="general-settings-grid">
          {receiptFields.map(([field, label]) =>
            <Form.Item key={field} name={['receiptPrint', field]} valuePropName="checked"><Checkbox>{label}</Checkbox></Form.Item>)}
        </div>
      </Card>
    </Form>
    <Space className="general-settings-actions">
      <Button type="primary" loading={busy} disabled={!dirty} onClick={() => void previewSave()}>Xem trước và lưu</Button>
      <Button disabled={!dirty || busy} onClick={() => modal.confirm({ title: 'Bỏ thay đổi chưa lưu?', okText: 'Bỏ thay đổi', cancelText: 'Tiếp tục sửa',
        onOk: () => { form.setFieldsValue(data.settings); markDirty(false); setError(undefined); } })}>Hoàn tác</Button>
    </Space>

    <Card size="small" title="Lịch sử phiên bản">
      {historyError && <Alert type="error" showIcon message={historyError} action={<Button onClick={() => setHistoryRevision(v => v + 1)}>Thử lại</Button>} />}
      <Table<GeneralHistoryItem> rowKey="version" size="small" loading={!history && !historyError} dataSource={history?.items ?? []} scroll={{ x: 640 }}
        locale={{ emptyText: 'Chưa có phiên bản nào được lưu; hệ thống đang dùng giá trị mặc định.' }}
        pagination={{ current: historyPage, pageSize: historySize, total: history?.total ?? 0, showSizeChanger: true, pageSizeOptions: [20, 50, 100],
          onChange: (p, s) => { setHistoryPage(s !== historySize ? 1 : p); setHistorySize(s); } }}
        columns={[
          { title: 'Phiên bản', dataIndex: 'version', width: 100, render: (v: number, row) => <>{v}{row.restoredFromVersion && <Tag className="general-settings-tag">Khôi phục từ {row.restoredFromVersion}</Tag>}</> },
          { title: 'Thời điểm', dataIndex: 'effectiveAt', render: formatTime },
          { title: 'Người lưu', dataIndex: 'actorName' },
          { title: 'Lý do', dataIndex: 'reason' },
          { title: 'Thao tác', key: 'actions', width: 120, render: (_, row) => row.version === data.version ? <Tag color="green">Hiện hành</Tag>
            : <Button size="small" disabled={busy || dirty} onClick={() => void previewRestore(row)}>Khôi phục</Button> },
        ]} />
    </Card>

    <Modal open={!!pending} title={pending?.kind === 'restore' ? `Khôi phục nội dung phiên bản ${pending.sourceVersion}` : 'Xem trước thay đổi'}
      okText="Lưu" cancelText="Hủy" confirmLoading={busy} closable={!busy} cancelButtonProps={{ disabled: busy }}
      okButtonProps={{ disabled: pending?.preview.stale || pending?.preview.changes.length === 0 }}
      onCancel={() => { if (!busy) setPending(null); }} onOk={() => void commit()}>
      {pending?.preview.stale && <Alert type="warning" showIcon message="Thiết lập vừa được người khác lưu. Tải phiên bản mới rồi xem trước lại; bản nhập của bạn được giữ."
        action={<Button onClick={() => { setPending(null); void load(true); }}>Tải phiên bản mới</Button>} />}
      {pending && pending.preview.changes.length === 0 && <Alert type="info" showIcon message="Không có thay đổi so với phiên bản hiện hành." />}
      {pending && pending.preview.changes.length > 0 && <Table size="small" rowKey="field" pagination={false} dataSource={pending.preview.changes}
        columns={[{ title: 'Mục', dataIndex: 'label' }, { title: 'Trước', dataIndex: 'before' }, { title: 'Sau', dataIndex: 'after' }]} />}
      {pending && pending.preview.missingPatients.length > 0 && <Alert type="warning" showIcon message="Hồ sơ đang thiếu trường vừa bắt buộc"
        description={pending.preview.missingPatients.map(x => `${x.label}: ${x.count} hồ sơ`).join(' · ')} />}
      {pending && pending.preview.warnings.length > 0 && <List size="small" dataSource={pending.preview.warnings} renderItem={x => <List.Item>{x}</List.Item>} />}
      <Form layout="vertical" disabled={busy}>
        <Form.Item label="Lý do" required validateStatus={reasonError ? 'error' : undefined} help={reasonError}>
          <Input.TextArea rows={2} maxLength={500} showCount value={reason} onChange={e => { setReason(e.target.value); setReasonError(undefined); }} />
        </Form.Item>
      </Form>
    </Modal>
  </div>;
}
