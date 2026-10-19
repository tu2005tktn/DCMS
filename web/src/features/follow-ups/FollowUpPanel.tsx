import { Alert, Button, Card, Collapse, Descriptions, Drawer, Empty, Form, Input, Modal, Select, Skeleton, Space, Tag, Tooltip, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api, type ApiError } from '../../services/api';
import BookingDrawer from '../appointments/BookingDrawer';
import {
  clinicToday, formatClock, formatDate, localDate, newRequestKey,
  type AppointmentOptions, type AppointmentResult, type StaffAppointmentDetail,
} from '../appointments/types';
import type { PlanStep } from '../treatment/progressionTypes';
import type { TreatmentDossier } from '../treatment/types';
import {
  durationOptions, purposeLabels, statusColors, statusLabels,
  type FollowUpChange, type FollowUpHandoff, type FollowUpInstruction, type FollowUpPurpose,
} from './types';

type CreateForm = {
  itemId: string; stepId: string; purpose: FollowUpPurpose; reason: string; guidance?: string;
  earliestOn?: string; targetOn?: string; latestOn?: string; durationMinutes?: number;
  dentistId?: number; capability?: string;
};
type ReviseForm = { earliestOn?: string; targetOn?: string; latestOn?: string; reason: string };
type ActionKind = 'complete' | 'cancel' | 'supersede';
type ActionState = { kind: ActionKind; instruction: FollowUpInstruction };

const actionLabels: Record<ActionKind, string> = {
  complete: 'Xác nhận hoàn thành', cancel: 'Hủy chỉ định', supersede: 'Thay thế chỉ định',
};

// S24 (docs/specs/S24-FOLLOW-UP-INSTRUCTION-WEB.md): chỉ gọi API instruction thật của DCMS-97,
// trạng thái hiển thị luôn lấy từ server; Find slots chỉ mở S11 với bối cảnh chỉ định.
export default function FollowUpPanel({ dossier }: { dossier: TreatmentDossier }) {
  const [instructions, setInstructions] = useState<FollowUpInstruction[] | null>(null);
  const [options, setOptions] = useState<AppointmentOptions | null>(null);
  const [steps, setSteps] = useState<PlanStep[]>([]);
  const [listError, setListError] = useState<ApiError | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [drawer, setDrawer] = useState<{ mode: 'create' } | { mode: 'revise'; instruction: FollowUpInstruction } | null>(null);
  const [action, setAction] = useState<ActionState | null>(null);
  const [busy, setBusy] = useState(false);
  const [submitError, setSubmitError] = useState<ApiError | null>(null);
  const [booking, setBooking] = useState<(FollowUpHandoff & { prefillDate: string }) | null>(null);
  const [appointmentById, setAppointmentById] = useState<Record<string, StaffAppointmentDetail | 'error'>>({});
  const [revision, setRevision] = useState(0);
  const [createForm] = Form.useForm<CreateForm>();
  const [reviseForm] = Form.useForm<ReviseForm>();
  const [actionForm] = Form.useForm<{ reason: string }>();
  const createKey = useRef(newRequestKey());
  const reviseKey = useRef(newRequestKey());
  const actionKey = useRef(newRequestKey());
  const watchedItem = Form.useWatch('itemId', createForm);
  const watchedStep = Form.useWatch('stepId', createForm);
  const watchedEarliest = Form.useWatch('earliestOn', createForm);
  const watchedTarget = Form.useWatch('targetOn', createForm);

  const canCreate = dossier.state === 'Open' && dossier.planState === 'Committed';
  const agreedItems = dossier.items.filter(x => x.decision === 'Agreed');
  const itemSteps = steps.filter(x => x.itemId === watchedItem);
  const selectedStep = itemSteps.find(x => x.id === watchedStep) ?? null;
  const watchedInstruction = drawer?.mode === 'revise' ? drawer.instruction : null;

  function merge(instruction: FollowUpInstruction) {
    setInstructions(current => [...(current ?? []).filter(x => x.id !== instruction.id), instruction]
      .sort((a, b) => a.targetOn.localeCompare(b.targetOn) || a.id.localeCompare(b.id)));
  }

  async function refreshList(): Promise<FollowUpInstruction[]> {
    const list = await api<FollowUpInstruction[]>(`/api/follow-up-instructions?patientId=${dossier.patientId}`);
    setInstructions(list);
    setListError(null);
    return list;
  }

  useEffect(() => {
    let live = true;
    setListError(null); setInstructions(null); setAppointmentById({});
    Promise.all([
      api<FollowUpInstruction[]>(`/api/follow-up-instructions?patientId=${dossier.patientId}`),
      api<AppointmentOptions>('/api/appointments/options'),
      api<PlanStep[]>(`/api/dossiers/${dossier.id}/steps`),
    ]).then(([list, available, planSteps]) => {
      if (!live) return;
      setInstructions(list); setOptions(available); setSteps(planSteps);
      list.filter(x => x.status === 'Booked' && x.activeAppointmentId).forEach(x => void loadAppointment(x));
    }).catch(failure => { if (live) setListError(failure as ApiError); });
    return () => { live = false; };
  }, [dossier.patientId, dossier.id, dossier.version, revision]);

  function openCreate() {
    setSubmitError(null); setNotice(null);
    createForm.resetFields();
    createForm.setFieldsValue({
      durationMinutes: 30,
      dentistId: options?.dentists.some(x => x.id === dossier.dentistId && x.branchId === dossier.branchId)
        ? dossier.dentistId : undefined,
    });
    createKey.current = newRequestKey();
    setDrawer({ mode: 'create' });
  }

  function openRevise(instruction: FollowUpInstruction) {
    setSubmitError(null); setNotice(null);
    reviseForm.resetFields();
    reviseForm.setFieldsValue({
      earliestOn: instruction.earliestOn, targetOn: instruction.targetOn, latestOn: instruction.latestOn,
    });
    reviseKey.current = newRequestKey();
    setDrawer({ mode: 'revise', instruction });
  }

  function openAction(kind: ActionKind, instruction: FollowUpInstruction) {
    setSubmitError(null); setNotice(null);
    actionForm.resetFields();
    actionKey.current = newRequestKey();
    setAction({ kind, instruction });
  }

  async function submitCreate(values: CreateForm) {
    if (busy) return;
    setBusy(true); setSubmitError(null);
    try {
      const result = await api<FollowUpChange>('/api/follow-up-instructions', {
        method: 'POST', body: JSON.stringify({
          patientId: dossier.patientId, branchId: dossier.branchId,
          source: { dossierId: dossier.id, itemId: values.itemId, stepId: values.stepId, visitId: null, activityId: null },
          purpose: values.purpose, reason: values.reason.trim(), guidance: values.guidance?.trim() || null,
          earliestOn: values.earliestOn, targetOn: values.targetOn, latestOn: values.latestOn,
          durationMinutes: values.durationMinutes, dentistId: values.dentistId ?? null,
          capability: values.capability?.trim() || null, requestKey: createKey.current,
        }),
      });
      merge(result.instruction);
      setNotice(result.notification.message);
      setDrawer(null);
      createKey.current = newRequestKey();
    } catch (failure) {
      const error = failure as ApiError;
      setSubmitError(error);
      // Lỗi mạng giữ nguyên requestKey để gửi lại không tạo trùng; server từ chối 4xx thì đổi key.
      if (error.status !== 0) createKey.current = newRequestKey();
    } finally { setBusy(false); }
  }

  async function submitRevise(values: ReviseForm) {
    if (busy || !watchedInstruction) return;
    setBusy(true); setSubmitError(null);
    try {
      const result = await api<FollowUpChange>(`/api/follow-up-instructions/${watchedInstruction.id}/revise-window`, {
        method: 'POST', body: JSON.stringify({
          expectedVersion: watchedInstruction.version,
          earliestOn: values.earliestOn, targetOn: values.targetOn, latestOn: values.latestOn,
          reason: values.reason.trim(), requestKey: reviseKey.current,
        }),
      });
      merge(result.instruction);
      setNotice(result.notification.message);
      setDrawer(null);
      reviseKey.current = newRequestKey();
    } catch (failure) {
      const error = failure as ApiError;
      setSubmitError(error);
      if (error.status !== 0) reviseKey.current = newRequestKey();
      if (error.code === 'MSG111') {
        // Xung đột phiên bản: tải lại bản mới từ server, giữ nguyên dữ liệu form để đối chiếu.
        try {
          const fresh = (await refreshList()).find(x => x.id === watchedInstruction.id);
          if (fresh) setDrawer({ mode: 'revise', instruction: fresh });
        } catch { /* lỗi tải lại được hiển thị ở danh sách */ }
      }
    } finally { setBusy(false); }
  }

  async function submitAction(values: { reason: string }) {
    if (busy || !action) return;
    setBusy(true); setSubmitError(null);
    try {
      const result = await api<FollowUpChange>(`/api/follow-up-instructions/${action.instruction.id}/${action.kind}`, {
        method: 'POST', body: JSON.stringify({
          expectedVersion: action.instruction.version, reason: values.reason.trim(), requestKey: actionKey.current,
        }),
      });
      merge(result.instruction);
      setNotice(result.notification.message);
      setAction(null);
      actionKey.current = newRequestKey();
    } catch (failure) {
      const error = failure as ApiError;
      setSubmitError(error);
      if (error.status !== 0) actionKey.current = newRequestKey();
      if (error.code === 'MSG111') {
        try {
          const fresh = (await refreshList()).find(x => x.id === action.instruction.id);
          if (fresh) setAction({ ...action, instruction: fresh });
        } catch { /* lỗi tải lại được hiển thị ở danh sách */ }
      }
    } finally { setBusy(false); }
  }

  // Find slots: chỉ đọc context chỉ định + dữ liệu bệnh nhân, không đổi trạng thái.
  async function findSlots(instruction: FollowUpInstruction) {
    let patientLabel = `Bệnh nhân #${instruction.patientId}`;
    try {
      const patient = await api<{ fullName: string }>(`/api/patients/${instruction.patientId}`);
      if (patient?.fullName) patientLabel = patient.fullName;
    } catch { /* vẫn mở S11 với mã bệnh nhân khi không đọc được hồ sơ */ }
    const today = clinicToday();
    const date = today > instruction.latestOn ? instruction.latestOn
      : today < instruction.earliestOn ? instruction.earliestOn
        : instruction.targetOn >= today ? instruction.targetOn : today;
    setSubmitError(null);
    setBooking({
      instructionId: instruction.id, version: instruction.version, patientId: instruction.patientId,
      patientLabel, branchId: instruction.branchId, dentistId: instruction.dentistId,
      durationMinutes: instruction.durationMinutes, earliestOn: instruction.earliestOn,
      targetOn: instruction.targetOn, latestOn: instruction.latestOn, prefillDate: date,
    });
  }

  // Trạng thái Booked chỉ đến từ đọc lại server sau khi Appointment đã commit.
  async function handleBooked(result: AppointmentResult) {
    setBooking(null);
    setNotice(result.notification.message);
    try {
      const list = await refreshList();
      list.filter(x => x.status === 'Booked' && x.activeAppointmentId).forEach(x => void loadAppointment(x));
    }
    catch (failure) { setListError(failure as ApiError); }
  }

  async function loadAppointment(instruction: FollowUpInstruction) {
    const id = instruction.activeAppointmentId;
    if (!id || appointmentById[id]) return;
    try {
      const detail = await api<StaffAppointmentDetail>(`/api/appointments/${id}`);
      setAppointmentById(current => ({ ...current, [id]: detail }));
    } catch {
      setAppointmentById(current => ({ ...current, [id]: 'error' }));
    }
  }

  function sourceText(instruction: FollowUpInstruction): string {
    const { source } = instruction;
    if (source.dossierId) {
      const item = dossier.items.find(x => x.id === source.itemId);
      const step = steps.find(x => x.id === source.stepId);
      const itemText = item ? `${item.serviceName}${item.region ? ` · ${item.region}` : ''}` : 'hạng mục kế hoạch';
      const stepText = step ? step.name : 'bước điều trị';
      return `Kế hoạch ${dossier.code} · ${itemText} · ${stepText}`;
    }
    if (source.visitId) return `Lượt khám${source.activityId ? ` · hoạt động ${source.activityId.slice(0, 8)}` : ''}`;
    return 'Chưa rõ nguồn';
  }

  function dentistText(id: number | null): string {
    if (!id) return 'Không chỉ định nha sĩ cụ thể';
    return options?.dentists.find(x => x.id === id)?.name ?? `Nha sĩ #${id}`;
  }

  const listEmpty = instructions !== null && instructions.length === 0;

  return <Card title="Chỉ định quay lại (S24)" className="follow-up-panel" extra={<Space wrap>
    <Typography.Text type="secondary">Trạng thái do máy chủ quyết định</Typography.Text>
    <Tooltip title={canCreate ? undefined : 'Cần đợt điều trị đang mở và kế hoạch đã chốt.'}>
      <Button type="primary" disabled={!canCreate || instructions === null} onClick={openCreate}>Tạo chỉ định</Button>
    </Tooltip>
  </Space>}>
    {notice && <Alert type="success" showIcon message={notice} style={{ marginBottom: 16 }} />}
    {listError && <Alert type="error" showIcon message={listError.message} style={{ marginBottom: 16 }}
      action={<Button size="small" onClick={() => setRevision(x => x + 1)}>Thử lại</Button>}
      description={listError.status === 0 ? 'Mất kết nối — dữ liệu chỉ định chưa được tải.' : undefined} />}
    {instructions === null
      ? (listError ? null : <Skeleton active />)
      : listEmpty
        ? <Empty description="Chưa có chỉ định quay lại cho bệnh nhân này." />
        : <Collapse items={instructions.map(instruction => ({
          key: instruction.id,
          label: <Space wrap>
            <Typography.Text strong>{purposeLabels[instruction.purpose]}</Typography.Text>
            <Tag color={statusColors[instruction.status]}>{statusLabels[instruction.status]}</Tag>
            {instruction.overdue && <Tag color="red">Quá hạn</Tag>}
            <Typography.Text type="secondary">
              {formatDate(instruction.earliestOn)} – {formatDate(instruction.latestOn)} · {instruction.durationMinutes} phút
            </Typography.Text>
          </Space>,
          children: <div>
            <Descriptions column={{ xs: 1, md: 2 }} size="small" bordered items={[
              { label: 'Lý do', children: instruction.reason },
              { label: 'Hướng dẫn', children: instruction.guidance || '—' },
              { label: 'Cửa sổ chỉ định', children: `Sớm nhất ${formatDate(instruction.earliestOn)} · Mục tiêu ${formatDate(instruction.targetOn)} · Muộn nhất ${formatDate(instruction.latestOn)}` },
              { label: 'Thời lượng giữ chỗ', children: `${instruction.durationMinutes} phút (reservedMinutes)` },
              { label: 'Nha sĩ phụ trách', children: dentistText(instruction.dentistId) },
              { label: 'Năng lực yêu cầu', children: instruction.capability || '—' },
              { label: 'Nguồn (chỉ đọc)', children: sourceText(instruction) },
              { label: 'Phiên bản', children: `v${instruction.version}` },
            ]} />
            {instruction.status === 'Booked' && instruction.activeAppointmentId && (() => {
              const info = appointmentById[instruction.activeAppointmentId!];
              return <Alert type="info" showIcon style={{ marginTop: 12 }}
                message={info && info !== 'error'
                  ? `Lịch hẹn ${info.appointment.code} · ${formatDate(localDate(info.appointment.startAt))} ${formatClock(info.appointment.startAt)} · ${statusLabels[info.appointment.status as keyof typeof statusLabels] ?? info.appointment.status}`
                  : info === 'error' ? 'Không tải được chi tiết lịch hẹn đang gắn.'
                    : 'Đang tải lịch hẹn đang gắn…'} />;
            })()}
            <Alert type="warning" showIcon style={{ marginTop: 12 }} message="Lịch sử cửa sổ/phiên bản trước chưa có endpoint đọc từ máy chủ (DCMS-97)."
              description="Chỉ trạng thái hiện tại do server trả về được hiển thị; không có lịch sử suy diễn từ client." />
            <Space wrap style={{ marginTop: 16 }}>
              {instruction.allowedActions.includes('findSlots') &&
                <Button type="primary" onClick={() => void findSlots(instruction)}>Tìm khung giờ (S11)</Button>}
              {instruction.allowedActions.includes('reviseWindow') &&
                <Button onClick={() => openRevise(instruction)}>Sửa cửa sổ</Button>}
              {instruction.allowedActions.includes('complete') &&
                <Button onClick={() => openAction('complete', instruction)}>{actionLabels.complete}</Button>}
              {instruction.allowedActions.includes('cancel') &&
                <Button danger onClick={() => openAction('cancel', instruction)}>Hủy chỉ định</Button>}
              {instruction.allowedActions.includes('supersede') &&
                <Button onClick={() => openAction('supersede', instruction)}>Thay thế</Button>}
            </Space>
            {instruction.allowedActions.length === 0 &&
              <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0 }}>
                Trạng thái {statusLabels[instruction.status]} không còn thao tác nào.
              </Typography.Paragraph>}
          </div>,
        }))} />}

    <Drawer open={drawer?.mode === 'create'} title="Tạo chỉ định quay lại (S24)" width={640} destroyOnClose
      onClose={() => setDrawer(null)} extra={<Space>
        <Button disabled={busy} onClick={() => setDrawer(null)}>Hủy</Button>
        <Button type="primary" loading={busy} onClick={() => createForm.submit()}>Lưu chỉ định</Button>
      </Space>}>
      {submitError && <Alert type="error" showIcon className="form-alert" message={submitError.message}
        description={submitError.code === 'MSG144'
          ? 'Nguồn chưa sẵn sàng — dữ liệu form được giữ nguyên, thử lại sau.'
          : submitError.code === 'MSG06'
            ? 'Bạn không có quyền ghi chỉ định tại chi nhánh này — dữ liệu form được giữ nguyên.'
            : submitError.code === 'MSG233'
              ? 'Bước điều trị đã có chỉ định đang hiệu lực — dữ liệu form được giữ nguyên.'
              : undefined} />}
      <Typography.Paragraph type="secondary">
        Nguồn chỉ định (chỉ đọc): đợt điều trị {dossier.code} · chi nhánh {dossier.branchId} · bệnh nhân #{dossier.patientId}.
        Nguồn lượt khám/hoạt động dành cho module lâm sàng nên chưa hiển thị ở đây.
      </Typography.Paragraph>
      <Form form={createForm} layout="vertical" onFinish={values => void submitCreate(values)}
        onValuesChange={() => { createKey.current = newRequestKey(); setSubmitError(null); }}>
        <div className="patient-form-grid">
          <Form.Item name="purpose" label="Mục đích (purpose)" rules={[{ required: true, message: 'Vui lòng chọn Mục đích.' }]}>
            <Select options={Object.entries(purposeLabels).map(([value, label]) => ({ value, label }))} />
          </Form.Item>
          <Form.Item name="itemId" label="Hạng mục kế hoạch đã đồng ý" rules={[{ required: true, message: 'Vui lòng chọn Hạng mục.' }]}
            extra={agreedItems.length === 0 ? 'Kế hoạch chưa có hạng mục đã đồng ý.' : undefined}>
            <Select placeholder="Chọn hạng mục"
              options={agreedItems.map(item => ({ value: item.id, label: `${item.serviceName} · ${item.region || 'Chưa chọn vùng'}` }))}
              onChange={() => createForm.setFieldsValue({ stepId: undefined as unknown as string })} />
          </Form.Item>
          <Form.Item name="stepId" label="Bước điều trị (step)" rules={[{ required: true, message: 'Vui lòng chọn Bước điều trị.' }]}
            extra={!watchedItem ? 'Chọn hạng mục trước.'
              : itemSteps.length === 0 ? 'Hạng mục này chưa có bước điều trị ở kế hoạch hiện tại.' : undefined}>
            <Select placeholder="Chọn bước" disabled={!watchedItem}
              options={itemSteps.map(step => ({ value: step.id, label: `${step.name} · giữ chỗ ${step.reservedMinutes} phút` }))} />
          </Form.Item>
          <Form.Item name="dentistId" label="Nha sĩ phụ trách" extra="Mặc định theo bác sĩ của đợt điều trị; để trống nếu không yêu cầu cụ thể.">
            <Select allowClear placeholder="Không yêu cầu cụ thể"
              options={(options?.dentists ?? []).filter(x => x.branchId === dossier.branchId)
                .map(x => ({ value: x.id, label: x.name }))} />
          </Form.Item>
        </div>
        {selectedStep && <Alert type="info" showIcon style={{ marginBottom: 16 }}
          message={`Bước ${selectedStep.name}: ước tính ${selectedStep.estimatedMinutes} phút · giữ chỗ ${selectedStep.reservedMinutes} phút.`}
          description="Thời lượng chỉ định do bạn chọn bên dưới, không tự suy từ thời lượng ước tính." />}
        <div className="patient-form-grid">
          <Form.Item name="earliestOn" label="Ngày sớm nhất (earliestOn)" rules={[{ required: true, message: 'Vui lòng nhập Ngày sớm nhất.' }]}>
            <Input type="date" />
          </Form.Item>
          <Form.Item name="targetOn" label="Ngày mục tiêu (targetOn)" dependencies={['earliestOn']}
            rules={[{ required: true, message: 'Vui lòng nhập Ngày mục tiêu.' },
              { validator: (_, value?: string) => !value || !watchedEarliest || value >= watchedEarliest
                ? Promise.resolve() : Promise.reject(new Error('Ngày mục tiêu phải từ ngày sớm nhất trở đi.')) }]}>
            <Input type="date" />
          </Form.Item>
          <Form.Item name="latestOn" label="Ngày muộn nhất (latestOn)" dependencies={['targetOn']}
            rules={[{ required: true, message: 'Vui lòng nhập Ngày muộn nhất.' },
              { validator: (_, value?: string) => !value || !watchedTarget || value >= watchedTarget
                ? Promise.resolve() : Promise.reject(new Error('Ngày muộn nhất phải từ ngày mục tiêu trở đi.')) }]}>
            <Input type="date" />
          </Form.Item>
          <Form.Item name="durationMinutes" label="Thời lượng giữ chỗ (reservedMinutes)"
            extra="15–480 phút, bội số của 15 (hợp đồng nhân viên)."
            rules={[{ required: true, message: 'Vui lòng chọn Thời lượng.' },
              { validator: (_, value?: number) => !value || (value >= 15 && value <= 480 && value % 15 === 0)
                ? Promise.resolve() : Promise.reject(new Error('Thời lượng phải là bội số 15 trong khoảng 15–480 phút.')) }]}>
            <Select showSearch options={durationOptions} />
          </Form.Item>
        </div>
        <Form.Item name="capability" label="Năng lực nha sĩ yêu cầu (capability)"
          rules={[{ max: 160, message: 'Năng lực không được vượt quá 160 ký tự.' }]}>
          <Input maxLength={160} placeholder="Để trống nếu không yêu cầu" />
        </Form.Item>
        <Form.Item name="reason" label="Lý do (reason)"
          rules={[{ required: true, whitespace: true, message: 'Vui lòng nhập Lý do.' },
            { max: 2000, message: 'Lý do không được vượt quá 2000 ký tự.' }]}>
          <Input.TextArea rows={3} maxLength={2000} showCount />
        </Form.Item>
        <Form.Item name="guidance" label="Hướng dẫn cho lần tái khám (instructions)"
          rules={[{ max: 4000, message: 'Hướng dẫn không được vượt quá 4000 ký tự.' }]}>
          <Input.TextArea rows={3} maxLength={4000} showCount placeholder="Để trống nếu không có" />
        </Form.Item>
      </Form>
    </Drawer>

    <Drawer open={drawer?.mode === 'revise'} title="Sửa cửa sổ chỉ định (S24)" width={560} destroyOnClose
      onClose={() => setDrawer(null)} extra={<Space>
        <Button disabled={busy} onClick={() => setDrawer(null)}>Hủy</Button>
        <Button type="primary" loading={busy} onClick={() => reviseForm.submit()}>Lưu thay đổi</Button>
      </Space>}>
      {submitError && <Alert type="error" showIcon className="form-alert" message={submitError.message}
        description={submitError.code === 'MSG111' ? 'Phiên bản đã thay đổi — dữ liệu nhập được giữ lại, bấm lưu lần nữa để dùng bản mới.' : undefined} />}
      {watchedInstruction && <Typography.Paragraph type="secondary">
        {purposeLabels[watchedInstruction.purpose]} · {statusLabels[watchedInstruction.status]} · v{watchedInstruction.version}
        · thời lượng {watchedInstruction.durationMinutes} phút (giữ nguyên).
      </Typography.Paragraph>}
      <Form form={reviseForm} layout="vertical" onFinish={values => void submitRevise(values)}
        onValuesChange={() => { reviseKey.current = newRequestKey(); setSubmitError(null); }}>
        <div className="patient-form-grid">
          <Form.Item name="earliestOn" label="Ngày sớm nhất" rules={[{ required: true, message: 'Vui lòng nhập Ngày sớm nhất.' }]}>
            <Input type="date" />
          </Form.Item>
          <Form.Item name="targetOn" label="Ngày mục tiêu" dependencies={['earliestOn']}
            rules={[{ required: true, message: 'Vui lòng nhập Ngày mục tiêu.' },
              { validator: (_, value?: string) => {
                const earliest = reviseForm.getFieldValue('earliestOn');
                return !value || !earliest || value >= earliest
                  ? Promise.resolve() : Promise.reject(new Error('Phải từ ngày sớm nhất trở đi.'));
              } }]}>
            <Input type="date" />
          </Form.Item>
          <Form.Item name="latestOn" label="Ngày muộn nhất" dependencies={['targetOn']}
            rules={[{ required: true, message: 'Vui lòng nhập Ngày muộn nhất.' },
              { validator: (_, value?: string) => {
                const target = reviseForm.getFieldValue('targetOn');
                return !value || !target || value >= target
                  ? Promise.resolve() : Promise.reject(new Error('Phải từ ngày mục tiêu trở đi.'));
              } }]}>
            <Input type="date" />
          </Form.Item>
        </div>
        <Form.Item name="reason" label="Lý do thay đổi (bắt buộc)"
          rules={[{ required: true, whitespace: true, message: 'Vui lòng nhập Lý do.' },
            { max: 1000, message: 'Lý do không được vượt quá 1000 ký tự.' }]}>
          <Input.TextArea rows={3} maxLength={1000} />
        </Form.Item>
      </Form>
    </Drawer>

    <Modal open={action !== null} title={action ? actionLabels[action.kind] : ''}
      okText="Xác nhận" okButtonProps={{ loading: busy }} onOk={() => actionForm.submit()}
      onCancel={() => { if (!busy) { setAction(null); setSubmitError(null); } }}>
      {submitError && <Alert type="error" showIcon className="form-alert" message={submitError.message}
        description={submitError.code === 'MSG111' ? 'Phiên bản đã thay đổi — dữ liệu nhập được giữ lại, bấm xác nhận lần nữa để dùng bản mới.'
          : submitError.code === 'MSG230' ? 'Chỉ định không cho phép thao tác này (đã kết thúc hoặc bị chặn) — xem lại trạng thái.'
            : submitError.code === 'MSG233' ? 'Chỉ định đang gắn với lịch hẹn — hủy/đổi lịch trước khi thay đổi chỉ định.'
              : undefined} />}
      {action && <Typography.Paragraph type="secondary">
        {purposeLabels[action.instruction.purpose]} · {statusLabels[action.instruction.status]} · v{action.instruction.version}
      </Typography.Paragraph>}
      <Form form={actionForm} layout="vertical" onFinish={values => void submitAction(values)}
        onValuesChange={() => { actionKey.current = newRequestKey(); setSubmitError(null); }}>
        <Form.Item name="reason" label="Lý do (bắt buộc)"
          rules={[{ required: true, whitespace: true, message: 'Vui lòng nhập Lý do.' },
            { max: 1000, message: 'Lý do không được vượt quá 1000 ký tự.' }]}>
          <Input.TextArea rows={3} maxLength={1000} />
        </Form.Item>
      </Form>
    </Modal>

    <BookingDrawer open={booking !== null} followUp={booking}
      prefill={booking ? { branchId: booking.branchId, dentistId: booking.dentistId ?? undefined, date: booking.prefillDate } : null}
      options={options} onClose={() => setBooking(null)} onBooked={result => void handleBooked(result)} />
  </Card>;
}
