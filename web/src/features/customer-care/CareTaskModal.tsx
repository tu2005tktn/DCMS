import { Alert, Button, DatePicker, Form, Input, Modal, Select, Space, Tooltip, Typography } from 'antd';
import { EllipsisOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { useEffect, useRef, useState } from 'react';
import { ApiError } from '../../services/api';
import { customerCareApi } from './customerCare.api';
import { clinicParts, statusLabels, toDueAt, type CareBranch, type CareOptions, type CarePatient, type CareStatus, type CareWorklistItem } from './types';

// S60 "Thêm chăm sóc" (docs/specs/S60-CUSTOMER-CARE-WEB.vi.md §4). Edit keeps patient and type fixed.
// `branches` are the branches the user may write in; a branch field appears only when there are two or more.
type Values = { branchId: number; date: Dayjs; hour: number; minute: number; patientId?: number; ownerAccountId?: number; type?: string; content: string; status?: CareStatus };
const hours = Array.from({ length: 24 }, (_, h) => ({ value: h, label: String(h).padStart(2, '0') }));

export default function CareTaskModal({ open, options, branches, initialBranchId, editing, canManageTypes, onManageTypes, onClose, onSaved }: {
  open: boolean; options: CareOptions; branches: CareBranch[]; initialBranchId?: number; editing?: CareWorklistItem; canManageTypes: boolean;
  onManageTypes: () => void; onClose: () => void; onSaved: () => void;
}) {
  const [form] = Form.useForm<Values>();
  const [patients, setPatients] = useState<CarePatient[]>([]), [searching, setSearching] = useState(false), [searchError, setSearchError] = useState<string>();
  const [busy, setBusy] = useState(false), [error, setError] = useState<ApiError>();
  const [confirm, confirmContext] = Modal.useModal();
  const key = useRef<{ fingerprint: string; value: string } | null>(null), timer = useRef<number>(undefined), searchOrder = useRef(0);
  const manualTypes = options.types.filter(x => !x.system);
  const branchId = Form.useWatch('branchId', form);
  const branch = branches.find(b => b.id === branchId) ?? branches[0];
  const minuteValue = Form.useWatch('minute', form);
  const minutes = [...new Set([...Array.from({ length: 12 }, (_, i) => i * 5), ...(minuteValue == null ? [] : [minuteValue])])].sort((a, b) => a - b)
    .map(m => ({ value: m, label: String(m).padStart(2, '0') }));

  useEffect(() => {
    if (!open) return;
    key.current = null; setError(undefined); setPatients([]); setSearchError(undefined); form.resetFields();
    const start = branches.find(b => b.id === (editing?.task.branchId ?? initialBranchId)) ?? branches[0];
    form.setFieldsValue({ branchId: start?.id });
    if (editing) {
      const at = clinicParts(editing.task.dueAt);
      form.setFieldsValue({ date: dayjs(at.date), hour: at.hour, minute: at.minute, ownerAccountId: editing.task.ownerAccountId ?? undefined,
        content: editing.task.content, status: editing.task.status });
    } else {
      // Default: today, current clinic time rounded up to 5 minutes, owner = signed-in account.
      const now = clinicParts(new Date(Date.now() + 4 * 60 * 1000 + 59 * 1000).toISOString());
      const minute = Math.floor(now.minute / 5) * 5;
      form.setFieldsValue({ date: dayjs(now.date), hour: now.hour, minute,
        ownerAccountId: start?.staff.some(x => x.accountId === options.actorAccountId) ? options.actorAccountId : undefined });
    }
  }, [open, editing]);

  function search(term: string) {
    window.clearTimeout(timer.current);
    if (!term.trim()) { setPatients([]); return; }
    timer.current = window.setTimeout(() => {
      const order = ++searchOrder.current; setSearching(true); setSearchError(undefined);
      customerCareApi.patients(branch.id, term.trim()).then(rows => { if (order === searchOrder.current) setPatients(rows); })
        .catch(e => { if (order === searchOrder.current) { setPatients([]); setSearchError((e as Error).message); } })
        .finally(() => { if (order === searchOrder.current) setSearching(false); });
    }, 300);
  }

  async function save() {
    const v = await form.validateFields();
    const dueAt = toDueAt(v.date.format('YYYY-MM-DD'), v.hour, v.minute);
    const payload = editing
      ? { expectedVersion: editing.task.version, status: v.status!, ownerAccountId: v.ownerAccountId ?? null, dueAt, content: v.content.trim() }
      : { patientId: v.patientId!, branchId: v.branchId, type: v.type!, content: v.content.trim(), ownerAccountId: v.ownerAccountId ?? null, dueAt };
    // Same payload keeps the same requestKey, so a retry after a lost response cannot create a second ticket.
    const fingerprint = JSON.stringify(payload);
    if (key.current?.fingerprint !== fingerprint) key.current = { fingerprint, value: crypto.randomUUID() };
    setBusy(true); setError(undefined);
    try {
      if (editing) await customerCareApi.update(editing.task.id, { ...(payload as Parameters<typeof customerCareApi.update>[1]), requestKey: key.current.value });
      else await customerCareApi.create({ ...(payload as Parameters<typeof customerCareApi.create>[0]), requestKey: key.current.value });
      onSaved();
    } catch (e) { setError(e as ApiError); } finally { setBusy(false); }
  }

  function cancel() {
    if (!form.isFieldsTouched() || busy) { if (!busy) onClose(); return; }
    void confirm.confirm({ title: 'Bỏ nội dung đang nhập?', okText: 'Bỏ và đóng', cancelText: 'Ở lại', okButtonProps: { danger: true }, onOk: onClose });
  }

  return <Modal open={open} title={editing ? 'Sửa chăm sóc' : 'Thêm chăm sóc'} onCancel={cancel} maskClosable={false} width={640} destroyOnClose
    footer={[<Button key="cancel" onClick={cancel} disabled={busy}>Hủy</Button>,
      <Button key="save" type="primary" loading={busy} onClick={() => void save()}>Lưu</Button>]}>
    {confirmContext}
    {error && <Alert type="error" showIcon style={{ marginBottom: 16 }} message={error.message}
      description={error.code === 'MSG111' ? 'Phiếu vừa được người khác cập nhật. Đóng hộp thoại, tải lại danh sách rồi sửa lại.' : undefined} />}
    <Form form={form} layout="horizontal" labelCol={{ span: 6 }} wrapperCol={{ span: 18 }} disabled={busy}>
      <Form.Item label="Chi nhánh" name="branchId" hidden={editing != null || branches.length < 2} rules={[{ required: true, message: 'Chọn chi nhánh.' }]}>
        <Select aria-label="Chi nhánh tạo phiếu" options={branches.map(b => ({ value: b.id, label: b.name }))}
          onChange={(next: number) => {
            // Patients and owners are per branch: reset them when the branch changes.
            setPatients([]); const target = branches.find(b => b.id === next);
            form.setFieldsValue({ patientId: undefined, ownerAccountId: target?.staff.some(x => x.accountId === options.actorAccountId) ? options.actorAccountId : undefined });
          }} />
      </Form.Item>
      <Form.Item label="Ngày" required style={{ marginBottom: 0 }}>
        <Space wrap>
          <Form.Item name="date" rules={[{ required: true, message: 'Vui lòng chọn ngày.' }]}>
            <DatePicker aria-label="Ngày chăm sóc" format="DD/MM/YYYY" allowClear={false} /></Form.Item>
          <Typography.Text strong>Giờ</Typography.Text>
          <Form.Item name="hour" rules={[{ required: true, message: 'Chọn giờ.' }]}><Select aria-label="Giờ" style={{ width: 80 }} options={hours} /></Form.Item>
          <Form.Item name="minute" rules={[{ required: true, message: 'Chọn phút.' }]}><Select aria-label="Phút" style={{ width: 80 }} options={minutes} /></Form.Item>
        </Space>
      </Form.Item>
      {editing
        ? <Form.Item label="Khách hàng"><Typography.Text>{editing.patientCode} · {editing.patientName}{editing.phone ? ` · ${editing.phone}` : ''}</Typography.Text></Form.Item>
        : <Form.Item label="Khách hàng" name="patientId" rules={[{ required: true, message: 'Vui lòng chọn khách hàng.' }]}
            extra={searchError ?? 'Chỉ gợi ý khách đã hoặc đang khám tại chi nhánh này.'} validateStatus={searchError ? 'error' : undefined}>
            <Select aria-label="Khách hàng" showSearch filterOption={false} onSearch={search} loading={searching} allowClear
              placeholder="Nhập mã, tên hoặc SĐT để chọn khách hàng" notFoundContent={searching ? 'Đang tìm…' : 'Không có khách phù hợp'}
              options={patients.map(p => ({ value: p.patientId,
                label: `${p.patientCode} · ${p.fullName}${p.phone ? ` · ${p.phone}` : ''} · khám ${clinicParts(p.lastVisitAt).date.split('-').reverse().join('/')}` }))} />
          </Form.Item>}
      <Form.Item label="Người phụ trách" name="ownerAccountId">
        <Select aria-label="Người phụ trách" allowClear placeholder="Chưa giao" options={branch.staff.map(x => ({ value: x.accountId, label: x.name }))} />
      </Form.Item>
      {editing
        ? <Form.Item label="Loại chăm sóc"><Typography.Text>{editing.typeName}</Typography.Text></Form.Item>
        : <Form.Item label="Loại chăm sóc" required
            extra={manualTypes.length ? undefined : 'Chưa có loại chăm sóc. Nhờ Chủ phòng khám thêm ở Thiết lập hệ thống › 2. Chẩn đoán / Tên bệnh › Loại chăm sóc khách hàng.'}>
            <Space.Compact style={{ width: '100%' }}>
              <Form.Item name="type" noStyle rules={[{ required: true, message: 'Vui lòng chọn loại chăm sóc.' }]}>
                <Select aria-label="Loại chăm sóc" placeholder="Chọn loại chăm sóc" options={manualTypes.map(x => ({ value: x.code, label: x.name }))} />
              </Form.Item>
              {canManageTypes && <Tooltip title="Quản lý loại chăm sóc"><Button aria-label="Quản lý loại chăm sóc" icon={<EllipsisOutlined />} onClick={onManageTypes} /></Tooltip>}
            </Space.Compact>
          </Form.Item>}
      <Form.Item label="Nội dung" name="content" rules={[{ required: true, whitespace: true, message: 'Vui lòng nhập nội dung.' }]}>
        <Input.TextArea aria-label="Nội dung" rows={5} maxLength={4000} showCount />
      </Form.Item>
      {editing && <Form.Item label="Trạng thái" name="status" rules={[{ required: true }]}>
        <Select aria-label="Trạng thái" options={(Object.keys(statusLabels) as CareStatus[]).map(s => ({ value: s, label: statusLabels[s] }))} />
      </Form.Item>}
    </Form>
  </Modal>;
}
