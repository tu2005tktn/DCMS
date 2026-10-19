import { Alert, Button, DatePicker, Descriptions, Drawer, Empty, Form, Input, Modal, Select, Space, Spin, Tabs, Tag, Timeline, Typography, message } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { useEffect, useRef, useState } from 'react';
import {
  appointmentStatusLabels,
  formatDateTime,
  kindLabels,
  reasonLabels,
  statusColors,
  statusLabels,
  type ChangeReason,
  type StaffChangeCommand,
  type StaffChangeResult,
  type StaffResolveCommand,
  type StaffResolveResult,
  type SupportRequestDetail,
} from './types';
import { supportInboxApi } from './supportInbox.api';

type Props = {
  id: string | null;
  refreshToken: number;
  onClose: () => void;
  onChanged: () => void;
};

type ChangeFormValues = {
  newStartAt: Dayjs;
  reason: ChangeReason;
  notes?: string;
};

type RejectFormValues = {
  decisionReason: string;
  publicNote?: string;
};

/** Drawer chi tiết + command bar cho staff xử lý yêu cầu (TV4/TV5). */
export default function RequestDrawer({ id, refreshToken, onClose, onChanged }: Props) {
  const [toast, toastContext] = message.useMessage();
  const [modal, modalContext] = Modal.useModal();
  const [detail, setDetail] = useState<SupportRequestDetail | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [decision, setDecision] = useState<'resolve' | 'reject' | null>(null);
  const [actionNotice, setActionNotice] = useState<{ kind: 'success' | 'warning' | 'error'; message: string } | null>(null);
  const [changeForm] = Form.useForm<ChangeFormValues>();
  const [rejectForm] = Form.useForm<RejectFormValues>();
  const latest = useRef(0);
  const loadedId = useRef<string | null>(null);
  const retryKeys = useRef<Record<string, { fingerprint: string; key: string }>>({});

  function requestKey(action: string, fingerprint: string): string {
    const previous = retryKeys.current[action];
    if (previous?.fingerprint === fingerprint) return previous.key;
    const key = crypto.randomUUID();
    retryKeys.current[action] = { fingerprint, key };
    return key;
  }

  function finishRequest(action: string, errorCode?: string) {
    if (!errorCode || !['NETWORK', 'SERVER'].includes(errorCode)) delete retryKeys.current[action];
  }

  useEffect(() => {
    if (id == null) {
      latest.current += 1;
      setDetail(null);
      setError(null);
      setActionNotice(null);
      retryKeys.current = {};
      loadedId.current = null;
      return;
    }
    const shouldResetForms = loadedId.current !== id;
    const request = ++latest.current;
    setLoading(true);
    setError(null);
    setActionNotice(null);
    supportInboxApi.getRequest(id)
      .then(result => {
        if (request !== latest.current) return;
        setDetail(result);
        loadedId.current = id;
        if (shouldResetForms) {
          changeForm.resetFields();
          rejectForm.resetFields();
        }
      })
      .catch((failure: Error) => {
        if (request !== latest.current) return;
        setError(failure.message || 'Không thể tải chi tiết yêu cầu.');
      })
      .finally(() => {
        if (request === latest.current) setLoading(false);
      });
  }, [id, refreshToken, changeForm, rejectForm]);

  async function runChange(): Promise<StaffChangeResult | null> {
    if (!detail) return null;
    const values = await changeForm.validateFields().catch(() => null);
    if (!values) return null;
    const fingerprint = JSON.stringify({ id: detail.id, version: detail.expectedVersion, values });
    const command: StaffChangeCommand = {
      requestKey: requestKey('change', fingerprint),
      expectedVersion: detail.expectedVersion,
      reason: values.reason,
      notes: values.notes,
      newStartAt: detail.kind === 'Change' ? values.newStartAt.toDate().toISOString() : undefined,
    };
    setSaving(true); setError(null); setActionNotice(null);
    try {
      const result = await supportInboxApi.staffChange(detail.id, command);
      if (!result.ok) {
        finishRequest('change', result.errorCode);
        const messageText = result.errorMessage ?? 'Không thể thay đổi lịch.';
        setActionNotice({ kind: 'error', message: messageText });
        if (result.errorCode === 'STALE_VERSION') {
          const refreshed = await supportInboxApi.getRequest(detail.id).catch(() => null);
          if (refreshed) setDetail(refreshed);
        }
        return result;
      }
      finishRequest('change');
      const refreshed = result.request ?? await supportInboxApi.getRequest(detail.id);
      setDetail(refreshed);
      setActionNotice({ kind: 'success', message: 'Đã thay đổi lịch. Bây giờ bạn có thể xử lý yêu cầu.' });
      void toast.success(notificationMessage(result.notificationCode));
      onChanged();
      return result;
    } finally {
      setSaving(false);
    }
  }

  async function verifyRequester(): Promise<void> {
    if (!detail) return;
    setSaving(true); setError(null); setActionNotice(null);
    try {
      const fingerprint = JSON.stringify({ id: detail.id, version: detail.expectedVersion,
        notes: 'Đã đối chiếu người yêu cầu với thông tin đặt lịch.' });
      const refreshed = await supportInboxApi.verifyRequester(detail.id, {
        requestKey: requestKey('verify', fingerprint),
        expectedVersion: detail.expectedVersion,
        notes: 'Đã đối chiếu người yêu cầu với thông tin đặt lịch.',
      });
      finishRequest('verify');
      setDetail(refreshed);
      setActionNotice({ kind: 'success', message: 'Đã xác minh người yêu cầu.' });
      onChanged();
    } catch (failure) {
      const code = (failure as { status?: number }).status;
      if (code && code < 500) finishRequest('verify', 'BAD_REQUEST');
      const messageText = failure instanceof Error ? failure.message : 'Không thể xác minh người yêu cầu.';
      setActionNotice({ kind: 'error', message: messageText });
    } finally { setSaving(false); }
  }

  async function runResolve(decisionKind: 'Resolve' | 'Reject'): Promise<StaffResolveResult | null> {
    if (!detail) return null;
    const form = rejectForm;
    const values = decisionKind === 'Reject'
      ? await form.validateFields().catch(() => null)
      : ({ decisionReason: '', publicNote: undefined } as RejectFormValues);
    if (!values) return null;
    const fingerprint = JSON.stringify({ id: detail.id, version: detail.expectedVersion, decisionKind, values });
    const command: StaffResolveCommand = {
      requestKey: requestKey(`decision-${decisionKind}`, fingerprint),
      expectedVersion: detail.expectedVersion,
      decision: decisionKind,
      decisionReason: values.decisionReason,
      notes: values.publicNote,
    };
    setSaving(true); setError(null); setActionNotice(null);
    try {
      const result = await supportInboxApi.resolveRequest(detail.id, command);
      if (!result.ok || !result.request) {
        finishRequest(`decision-${decisionKind}`, result.errorCode);
        setActionNotice({ kind: 'error', message: result.errorMessage ?? 'Không thể xử lý yêu cầu.' });
        if (result.errorCode === 'STALE_VERSION') {
          const refreshed = await supportInboxApi.getRequest(detail.id).catch(() => null);
          if (refreshed) setDetail(refreshed);
        }
        return result;
      }
      finishRequest(`decision-${decisionKind}`);
      setDetail(result.request);
      setActionNotice({
        kind: 'success',
        message: decisionKind === 'Resolve' ? 'Đã xử lý yêu cầu thành công.' : 'Đã từ chối yêu cầu.',
      });
      void toast.success(notificationMessage(result.notificationCode));
      setDecision(null);
      form.resetFields();
      onChanged();
      return result;
    } finally {
      setSaving(false);
    }
  }

  function confirmResolve() {
    if (!detail) return;
    modal.confirm({
      title: 'Xử lý yêu cầu?',
      content: detail.kind === 'Change' || detail.kind === 'Cancel'
        ? 'Yêu cầu chỉ được xử lý khi lịch đã được thay đổi thành công trên hệ thống.'
        : 'Yêu cầu sẽ được đóng. Người yêu cầu sẽ nhận thông báo.',
      okText: 'Xử lý', cancelText: 'Hủy', okButtonProps: { loading: saving, type: 'primary' },
      onOk: async () => { await runResolve('Resolve'); },
    });
  }

  const open = id != null;
  const permission = detail?.permission;
  const canEdit = !!permission?.canEdit && !!permission?.branchMatch && (detail?.status === 'Pending' || detail?.status === 'InReview');
  const canResolve = !!permission?.canResolve && (detail?.status === 'Pending' || detail?.status === 'InReview');
  const isWithdrawn = detail?.status === 'Withdrawn';
  const showChangeForm = !!detail && (detail.kind === 'Change' || detail.kind === 'Cancel') && canEdit;
  const hasChangeCommitted = !!detail?.appointmentActionCommitted;

  return (
    <Drawer
      open={open}
      onClose={onClose}
      width={960}
      destroyOnClose
      title={detail
        ? <Space size="small">
          <Typography.Text strong>{detail.appointmentCode}</Typography.Text>
          <Tag color={statusColors[detail.status]}>{statusLabels[detail.status]}</Tag>
        </Space>
        : 'Chi tiết yêu cầu'}
    >
      {toastContext}{modalContext}
      {error && <Alert className="form-alert" type="error" showIcon message={error}
        action={detail && <Button size="small" onClick={() => { setError(null); }}>Đóng cảnh báo</Button>} />}

      {actionNotice && <Alert className="form-alert" showIcon type={actionNotice.kind === 'success' ? 'success' : actionNotice.kind === 'warning' ? 'warning' : 'error'} message={actionNotice.message} />}

      {loading && !detail && <div className="support-drawer-spin"><Spin /></div>}

      {detail && <Tabs
        items={[
          {
            key: 'overview',
            label: 'Tổng quan',
            children: <Spin spinning={loading}>
              <Descriptions title="Yêu cầu" column={2} size="small" bordered items={[
                { label: 'Mã yêu cầu', children: <Typography.Text strong>{detail.id}</Typography.Text> },
                { label: 'Loại', children: kindLabels[detail.kind] },
                { label: 'Trạng thái', children: <Tag color={statusColors[detail.status]}>{statusLabels[detail.status]}</Tag> },
                { label: 'Phiên bản', children: `v${detail.expectedVersion}` },
                { label: 'Người yêu cầu', children: <>{detail.requester.name}{detail.requester.isAnonymous && ' (vãng lai)'}</> },
                { label: 'SĐT liên hệ', children: detail.requester.phone ?? '—' },
                { label: 'Gửi lúc', children: formatDateTime(detail.createdAt) },
                { label: 'Cập nhật', children: formatDateTime(detail.updatedAt) },
                { label: 'Xác minh requester', children: detail.requesterVerifiedAt
                  ? `Đã xác minh — ${formatDateTime(detail.requesterVerifiedAt)}`
                  : <Tag color="warning">Chưa xác minh</Tag> },
                { label: 'Lý do bệnh nhân', children: detail.reason
                  ? <span className="multiline">{detail.reason}</span>
                  : <Typography.Text type="secondary">—</Typography.Text>, span: 2 },
                { label: 'Khung giờ đề xuất', children: detail.proposedChange?.newStartAt
                  ? formatDateTime(detail.proposedChange.newStartAt)
                  : <Typography.Text type="secondary">—</Typography.Text>, span: 2 },
              ]} />

              <Descriptions title="Lịch hẹn" className="support-section" column={2} size="small" bordered items={[
                { label: 'Mã lịch', children: <Typography.Text strong>{detail.appointment.code}</Typography.Text> },
                { label: 'Trạng thái lịch', children: appointmentStatusLabels[detail.appointment.status] ?? detail.appointment.status },
                { label: 'Chi nhánh', children: detail.appointment.branchName },
                { label: 'Nha sĩ', children: detail.appointment.dentistName },
                { label: 'Dịch vụ', children: detail.appointment.serviceName },
                { label: 'Thời lượng', children: `${detail.appointment.durationMinutes} phút` },
                { label: 'Bắt đầu', children: formatDateTime(detail.appointment.startAt) },
                { label: 'Kết thúc', children: formatDateTime(detail.appointment.endAt) },
                { label: 'Bệnh nhân', children: `${detail.appointment.attendee.name}${detail.appointment.attendee.relation === 'Relative' ? ' (người thân)' : ''}` },
                { label: 'Booker', children: <>{detail.appointment.booker.name}{detail.appointment.booker.isAnonymous && ' (vãng lai)'}</> },
                { label: 'Xác nhận tham dự', children: detail.appointment.confirmedToAttend === 'Yes' ? 'Đã xác nhận'
                  : detail.appointment.confirmedToAttend === 'No' ? 'Đã từ chối' : 'Chờ xác nhận' },
                { label: 'Phiên bản lịch', children: `v${detail.appointment.version}` },
              ]} />

              {(detail.decisionReason || detail.decidedAt) && <Descriptions title="Kết quả xử lý" className="support-section" column={1} size="small" bordered items={[
                { label: 'Người xử lý', children: detail.deciderName ? `${detail.deciderName} — ${formatDateTime(detail.decidedAt)}` : '—' },
                { label: 'Lý do (nội bộ)', children: detail.decisionReason ? <span className="multiline">{detail.decisionReason}</span> : '—' },
              ]} />}

              {!permission?.branchMatch && <Alert className="form-alert" type="warning" showIcon
                message="Bạn không thuộc chi nhánh của lịch hẹn này — chỉ đọc." />}
              {isWithdrawn && <Alert className="form-alert" type="info" showIcon
                message="Yêu cầu đã được rút bởi người gửi. Không thể resolve/reject." />}

              {showChangeForm && <>
                <Typography.Title level={5} className="support-section">
                  {detail.kind === 'Change' ? 'Đổi lịch' : 'Huỷ lịch'}
                </Typography.Title>
                <Form
                  form={changeForm}
                  layout="vertical"
                  initialValues={{
                    reason: detail.proposedChange?.reason ?? 'PatientRequest',
                    newStartAt: detail.proposedChange?.newStartAt
                      ? dayjs(detail.proposedChange.newStartAt)
                      : dayjs(detail.appointment.startAt),
                  }}
                >
                  {detail.kind === 'Change' && <Form.Item name="newStartAt" label="Khung giờ mới"
                    rules={[{ required: true, message: 'Vui lòng chọn khung giờ mới.' }]}>
                    <DatePicker
                      showTime={{ format: 'HH:mm', minuteStep: 5 }}
                      format="DD/MM/YYYY HH:mm"
                      style={{ width: '100%' }}
                      disabled={!canEdit || hasChangeCommitted}
                    />
                  </Form.Item>}
                  <Form.Item name="reason" label="Lý do thay đổi"
                    rules={[{ required: true, message: 'Vui lòng chọn lý do.' }]}>
                    <Select
                      disabled={!canEdit || hasChangeCommitted}
                      options={Object.entries(reasonLabels).map(([value, label]) => ({ value, label }))}
                    />
                  </Form.Item>
                  <Form.Item name="notes" label="Ghi chú nội bộ (tùy chọn)">
                    <Input.TextArea rows={2} maxLength={500} showCount disabled={!canEdit || hasChangeCommitted} />
                  </Form.Item>
                </Form>
                {hasChangeCommitted && <Alert className="form-alert" type="success" showIcon
                  message="Lịch đã được thay đổi thành công. Bây giờ bạn có thể xử lý yêu cầu." />}
              </>}

              {detail.kind === 'Change' && !hasChangeCommitted && canEdit && (
                <Typography.Paragraph type="warning">
                  Resolve chỉ được kích hoạt sau khi lịch được đổi thành công.
                </Typography.Paragraph>
              )}
            </Spin>,
          },
          {
            key: 'history',
            label: `Lịch sử (${detail.history.length})`,
            children: detail.history.length === 0
              ? <Empty description="Chưa có lịch sử." />
              : <Timeline items={detail.history.map(entry => ({
                children: <>
                  <Typography.Text strong>{historyActionLabel(entry.action)}</Typography.Text>
                  <Typography.Text type="secondary"> — {historyActorLabel(entry.actor)} — {formatDateTime(entry.at)}</Typography.Text>
                  {entry.after?.status && <div>Trạng thái lịch: <Tag>{appointmentStatusLabels[entry.after.status] ?? entry.after.status}</Tag></div>}
                  {entry.after?.startAt && <div>Thời gian mới: {formatDateTime(entry.after.startAt)}</div>}
                  {entry.after?.reason && <div>Lý do: {reasonLabels[entry.after.reason] ?? entry.after.reason}</div>}
                  {entry.notes && <div>Ghi chú: {entry.notes}</div>}
                </>,
              }))} />,
          },
          {
            key: 'commands',
            label: 'Xử lý',
            children: <>
              {!detail.requesterVerifiedAt && !isWithdrawn && canEdit && <Button loading={saving}
                onClick={() => void verifyRequester()}>
                Xác minh người yêu cầu
              </Button>}

              {!isWithdrawn && canEdit && showChangeForm && <Button type="primary" loading={saving} onClick={() => void runChange()}
                disabled={hasChangeCommitted || !detail.requesterVerifiedAt}>
                {hasChangeCommitted
                  ? 'Đã thay đổi lịch'
                  : detail.kind === 'Change' ? 'Đổi lịch' : 'Huỷ lịch'}
              </Button>}

              <Space wrap>
                <Button type="primary" disabled={!canResolve || isWithdrawn || saving ||
                  ((detail.kind === 'Change' || detail.kind === 'Cancel') && !hasChangeCommitted)}
                  onClick={confirmResolve}
                  title={!canResolve ? 'Bạn không có quyền xử lý yêu cầu này' :
                    isWithdrawn ? 'Yêu cầu đã rút' :
                    (detail.kind === 'Change' || detail.kind === 'Cancel') && !hasChangeCommitted
                      ? 'Cần đổi/huỷ lịch trước khi xử lý' : ''}>
                  Xử lý yêu cầu
                </Button>
                <Button danger disabled={!canResolve || isWithdrawn || saving} onClick={() => setDecision('reject')}
                  title={!canResolve ? 'Bạn không có quyền xử lý yêu cầu này' :
                    isWithdrawn ? 'Yêu cầu đã rút' : ''}>
                  Từ chối
                </Button>
              </Space>
            </>,
          },
        ]}
      />}

      <Modal
        open={decision === 'reject'}
        title="Từ chối yêu cầu"
        okText="Xác nhận từ chối"
        okButtonProps={{ danger: true, loading: saving }}
        cancelText="Hủy"
        onCancel={() => setDecision(null)}
        onOk={async () => { await runResolve('Reject'); }}
        destroyOnClose
      >
        <Form form={rejectForm} layout="vertical">
          <Form.Item name="decisionReason" label="Lý do từ chối"
            rules={[{ required: true, whitespace: true, message: 'Vui lòng nhập lý do.' },
              { max: 500, message: 'Lý do không được vượt quá 500 ký tự.' }]}>
            <Input.TextArea rows={3} maxLength={500} showCount placeholder="Ví dụ: khung giờ đã kín, đề nghị bệnh nhân chọn khung khác." />
          </Form.Item>
          <Form.Item name="publicNote" label="Nội dung gửi người dùng (tùy chọn)"
            extra="Không ghi thông tin hồ sơ của người khác hoặc ghi chú nội bộ.">
            <Input.TextArea rows={2} maxLength={500} showCount />
          </Form.Item>
        </Form>
      </Modal>
    </Drawer>
  );
}

// ----------------------------------------------------------------------------
// Helpers
// ----------------------------------------------------------------------------

function notificationMessage(code: string | undefined): string {
  switch (code) {
    case 'MSG31': return 'Đã đổi lịch và thông báo cho bệnh nhân.';
    case 'MSG32': return 'Đã gửi thông báo xác nhận cho bệnh nhân.';
    case 'MSG33': return 'Đã gửi lời nhắc cho bệnh nhân.';
    case 'MSG34': return 'Đã đổi lịch và thông báo cho bệnh nhân.';
    case 'MSG35': return 'Đã huỷ lịch và thông báo cho bệnh nhân.';
    case 'MSG36': return 'Đã ghi nhận không đến.';
    case 'MSG37': return 'Đã xử lý yêu cầu và thông báo cho bệnh nhân.';
    case 'MSG38': return 'Đã từ chối yêu cầu và thông báo cho bệnh nhân.';
    case 'MSG94': return 'Đã ghi nhận thay đổi; đang gửi thông báo.';
    default: return 'Đã ghi nhận thao tác.';
  }
}

function historyActionLabel(action: string): string {
  switch (action) {
    case 'Created': return 'Tạo lịch';
    case 'Rescheduled': return 'Đổi lịch';
    case 'Cancelled': return 'Huỷ lịch';
    case 'Completed': return 'Hoàn thành';
    case 'NoShow': return 'Không đến';
    case 'ConfirmedToAttend': return 'Xác nhận tham dự';
    case 'RequestSubmitted': return 'Gửi yêu cầu';
    case 'RequestResolved': return 'Đã xử lý yêu cầu';
    case 'RequestRejected': return 'Đã từ chối yêu cầu';
    case 'RequestWithdrawn': return 'Đã rút yêu cầu';
    case 'ScheduleReduced': return 'Giảm giờ làm';
    default: return action;
  }
}

function historyActorLabel(actor: string): string {
  switch (actor) {
    case 'System': return 'Hệ thống';
    case 'Staff': return 'Nhân viên';
    case 'Patient': return 'Bệnh nhân';
    case 'Booker': return 'Người đặt lịch';
    default: return actor;
  }
}
