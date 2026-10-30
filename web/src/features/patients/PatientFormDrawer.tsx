import { Alert, Button, Checkbox, Drawer, Form, Input, Modal, Select, Space, Table, Tag, Typography } from 'antd';
import type { PatientReference } from './types';
import { useEffect, useRef, useState } from 'react';
import { api, ApiError } from '../../services/api';
import { codeExample, type PatientFormPolicy } from '../system-settings/generalSettings';
import { formatDate } from '../patient-links/types';
import { genderLabels, type DuplicateCandidate, type Gender, type PatientChange, type PatientDetail } from './types';

type Values = {
  patientCode?: string; fullName: string; birthDate?: string; gender?: Gender; phone?: string; address?: string;
  guardianName?: string; guardianRelationship?: string; guardianPhone?: string; note?: string;
  profileDate?: string; sourceId?: string; customerGroupId?: string; occupationId?: string; intakeHistoryIds?: string[]; intakeHistoryOther?: string;
};
type Option = { id: string; code: string | null; name: string };
type CatalogKind = 'patient-sources' | 'customer-groups' | 'occupations' | 'intake-history';

// Active choices from the S58 catalog, plus a value already on the record even if it was deactivated later (SET-01).
const choices = (options: Option[] | undefined, kept: (PatientReference | null | undefined)[]) => [
  ...(options ?? []).map(x => ({ value: x.id, label: x.name })),
  ...kept.filter((x): x is PatientReference => !!x && !(options ?? []).some(o => o.id === x.id))
    .map(x => ({ value: x.id, label: `${x.name}${x.active ? '' : ' (ngừng dùng)'}` })),
];

const blank = (value?: string | null) => value?.trim() ? value.trim() : null;
const today = () => new Date(Date.now() + 7 * 3600_000).toISOString().slice(0, 10);

// Shared S05 create / S06 edit form (UC-38, UC-09). Possible duplicates must be reviewed; nothing is merged.
export default function PatientFormDrawer({ open, patient, onClose, onSaved }: {
  open: boolean; patient: PatientDetail | null; onClose: () => void; onSaved: (change: PatientChange) => void;
}) {
  const [form] = Form.useForm<Values>();
  const [modal, modalContext] = Modal.useModal();
  const [busy, setBusy] = useState(false);
  // DCMS-122: one requestKey per create payload, kept across a lost response so the retry replays instead of issuing a second code.
  const pendingCreate = useRef<{ payload: string; key: string } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [duplicates, setDuplicates] = useState<DuplicateCandidate[] | null>(null);
  const [reviewed, setReviewed] = useState(false);
  const [policy, setPolicy] = useState<PatientFormPolicy | null>(null);
  const [policyError, setPolicyError] = useState<string | null>(null);
  const [catalogs, setCatalogs] = useState<Partial<Record<CatalogKind, Option[]>>>({});
  const [catalogError, setCatalogError] = useState<string | null>(null);
  const editing = patient !== null;
  const required = policy?.requiredFields;
  const mode = policy?.patientCode.mode;

  async function loadCatalogs() {
    setCatalogError(null);
    try {
      const kinds: CatalogKind[] = ['patient-sources', 'customer-groups', 'occupations', 'intake-history'];
      const loaded = await Promise.all(kinds.map(kind => api<Option[]>(`/api/system-settings/reference-options?kind=${kind}`)));
      setCatalogs(Object.fromEntries(kinds.map((kind, i) => [kind, loaded[i]])));
    } catch (failure) { setCatalogError((failure as Error).message); }
  }

  // S58: required fields and code mode come from the clinic policy; the server rechecks the same version at save.
  async function loadPolicy() {
    setPolicyError(null);
    try { setPolicy(await api<PatientFormPolicy>('/api/patients/form-policy')); }
    catch (failure) { setPolicy(null); setPolicyError((failure as Error).message); }
  }

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    form.setFieldsValue(patient ? {
      fullName: patient.fullName, birthDate: patient.birthDate ?? undefined, gender: patient.gender ?? undefined,
      phone: patient.phone ?? undefined, address: patient.address ?? undefined, guardianName: patient.guardianName ?? undefined,
      guardianRelationship: patient.guardianRelationship ?? undefined, guardianPhone: patient.guardianPhone ?? undefined,
      note: patient.note ?? undefined, profileDate: patient.profileDate ?? undefined, sourceId: patient.source?.id,
      customerGroupId: patient.customerGroup?.id, occupationId: patient.occupation?.id, intakeHistoryIds: patient.intakeHistory.map(x => x.id),
      intakeHistoryOther: patient.intakeHistoryOther ?? undefined,
    } : { profileDate: today() });
    setError(null); setDuplicates(null); setReviewed(false);
    void loadPolicy(); void loadCatalogs();
  }, [open, patient]);

  function close() {
    if (!form.isFieldsTouched()) { onClose(); return; }
    modal.confirm({ title: 'Bỏ thay đổi chưa lưu?', content: 'Thông tin vừa nhập sẽ không được lưu.', okText: 'Bỏ thay đổi', cancelText: 'Tiếp tục sửa', onOk: onClose });
  }

  function create(payload: object) {
    const text = JSON.stringify(payload);
    if (pendingCreate.current?.payload !== text) pendingCreate.current = { payload: text, key: crypto.randomUUID() };
    return api<PatientChange>('/api/patients', { method: 'POST', body: JSON.stringify({ ...payload, requestKey: pendingCreate.current.key }) });
  }

  async function submit(values: Values) {
    setBusy(true); setError(null);
    try {
      const body = {
        fullName: values.fullName, birthDate: values.birthDate || null, gender: values.gender ?? null, phone: blank(values.phone),
        address: blank(values.address), guardianName: blank(values.guardianName), guardianRelationship: blank(values.guardianRelationship),
        guardianPhone: blank(values.guardianPhone), note: blank(values.note),
        profileDate: values.profileDate || null, sourceId: values.sourceId ?? null, customerGroupId: values.customerGroupId ?? null,
        occupationId: values.occupationId ?? null, intakeHistoryIds: values.intakeHistoryIds ?? [], intakeHistoryOther: blank(values.intakeHistoryOther),
      };
      // Show suspected duplicates first; the server rechecks and refuses an unreviewed save.
      if (!reviewed) {
        const found = await api<DuplicateCandidate[]>('/api/patients/duplicates', {
          method: 'POST', body: JSON.stringify({ fullName: body.fullName, birthDate: body.birthDate, phone: body.phone, excludeId: patient?.id ?? null }),
        });
        if (found.length > 0) { setDuplicates(found); return; }
      }
      const policyVersion = policy?.version ?? null;
      const change = editing
        ? await api<PatientChange>(`/api/patients/${patient.id}`, { method: 'PUT', body: JSON.stringify({ ...body, version: patient.version, duplicatesReviewed: true, policyVersion }) })
        : await create({ ...body, patientCode: mode === 'AutoOnly' ? null : blank(values.patientCode), duplicatesReviewed: true, policyVersion });
      pendingCreate.current = null;
      onSaved(change);
    } catch (failure) {
      const apiError = failure as ApiError;
      if (!(apiError instanceof ApiError) || apiError.status !== 0) pendingCreate.current = null;
      if (apiError.field === 'policyVersion') {
        // Keep the draft; reload the policy so the new required fields show before the next save (SET-AT-06).
        await loadPolicy();
        setError('Thiết lập hồ sơ vừa thay đổi. Vui lòng kiểm tra lại các trường bắt buộc.');
        void form.validateFields().catch(() => undefined);
      } else {
        setError(apiError.message);
        if (apiError.field) form.setFields([{ name: apiError.field as keyof Values, errors: [apiError.message] }]);
      }
    }
    finally { setBusy(false); }
  }

  return <Drawer open={open} width={720} onClose={close} destroyOnClose title={editing ? `Sửa thông tin hành chính · ${patient.patientCode}` : 'Thêm hồ sơ bệnh nhân'}
    extra={<Space><Button onClick={close}>Hủy</Button>
      <Button type="primary" loading={busy} disabled={!policy || (duplicates !== null && duplicates.length > 0 && !reviewed)} onClick={() => form.submit()}>Lưu</Button></Space>}>
    {modalContext}
    {policyError && <Alert className="form-alert" type="error" showIcon message={`Không tải được thiết lập hồ sơ: ${policyError}`}
      action={<Button size="small" onClick={() => void loadPolicy()}>Thử lại</Button>} />}
    {error && <Alert className="form-alert" type="error" message={error} showIcon />}
    {catalogError && <Alert className="form-alert" type="warning" showIcon message={`Không tải được danh mục: ${catalogError}`}
      action={<Button size="small" onClick={() => void loadCatalogs()}>Thử lại</Button>} />}
    {duplicates && duplicates.length > 0 && <Alert className="form-alert" type="warning" showIcon message="Có hồ sơ nghi trùng"
      description={<>
        <Typography.Paragraph>Hệ thống chỉ gợi ý, không tự kết luận là cùng một người. Hãy đối chiếu trước khi lưu. Nếu đúng là người đã có hồ sơ, hủy và mở hồ sơ đó.</Typography.Paragraph>
        <Table<DuplicateCandidate> size="small" rowKey={x => x.patient.id} pagination={false} dataSource={duplicates} columns={[
          // Plain text: a new tab would not share the in-memory web session.
          { title: 'Mã', render: (_, x) => <Typography.Text strong>{x.patient.patientCode}</Typography.Text> },
          { title: 'Họ tên', render: (_, x) => <>{x.patient.fullName}{x.patient.isArchived && <Tag className="patient-tag">Ngừng sử dụng</Tag>}</> },
          { title: 'Ngày sinh', render: (_, x) => formatDate(x.patient.birthDate) },
          { title: 'SĐT', render: (_, x) => x.patient.phone ?? '—' },
          { title: 'Lý do', render: (_, x) => x.reasons.map(reason => <Tag key={reason} color="orange">{reason}</Tag>) },
        ]} />
        <Checkbox className="patient-review" checked={reviewed} onChange={e => setReviewed(e.target.checked)}>
          Tôi đã đối chiếu, đây là người khác với các hồ sơ trên</Checkbox>
      </>} />}
    <Form<Values> form={form} layout="vertical" onFinish={values => void submit(values)}
      onValuesChange={changed => { if ('fullName' in changed || 'birthDate' in changed || 'phone' in changed) { setDuplicates(null); setReviewed(false); } }}>
      <div className="patient-form-grid">
        {editing
          ? <Form.Item label="Mã bệnh nhân"><Input value={patient.patientCode} disabled /></Form.Item>
          : <Form.Item name="patientCode" label="Mã bệnh nhân"
            extra={mode === 'ManualOnly' ? 'Phòng khám yêu cầu nhập mã thủ công.'
              : mode === 'AutoOnly' ? `Hệ thống tự cấp mã (${codeExample(policy?.patientCode.prefix ?? '')}...).`
              : `Để trống để hệ thống tự cấp mã (${codeExample(policy?.patientCode.prefix ?? 'BN')}...).`}
            rules={[{ required: mode === 'ManualOnly', whitespace: true, message: 'Vui lòng nhập Mã bệnh nhân.' },
              { max: 40, message: 'Mã bệnh nhân không được vượt quá 40 ký tự.' },
              { pattern: /^[A-Za-z0-9][A-Za-z0-9._-]*$/, message: 'Mã bệnh nhân không đúng định dạng.' }]}>
            <Input maxLength={40} disabled={mode === 'AutoOnly'} placeholder={mode === 'ManualOnly' ? undefined : 'Tự động'} /></Form.Item>}
        <Form.Item name="fullName" label="Họ tên" rules={[{ required: true, whitespace: true, message: 'Vui lòng nhập Họ tên.' },
          { max: 100, message: 'Họ tên không được vượt quá 100 ký tự.' }]}><Input maxLength={100} /></Form.Item>
        <Form.Item name="birthDate" label="Ngày sinh" extra={required?.birthDate ? undefined : 'Để trống nếu chưa rõ.'}
          rules={[{ required: required?.birthDate, message: 'Vui lòng nhập Ngày sinh.' }, { validator: (_, value?: string) => !value || value <= today() ? Promise.resolve() : Promise.reject(new Error('Ngày sinh không đúng định dạng.')) }]}>
          <Input type="date" max={today()} /></Form.Item>
        <Form.Item name="gender" label="Giới tính" rules={[{ required: required?.gender, message: 'Vui lòng nhập Giới tính.' }]}><Select allowClear placeholder="Chưa rõ"
          options={(Object.keys(genderLabels) as Gender[]).map(value => ({ value, label: genderLabels[value] }))} /></Form.Item>
        <Form.Item name="phone" label="Số điện thoại" extra="Có thể dùng chung với người khác; không dùng để gộp hồ sơ."
          rules={[{ required: required?.phone, whitespace: true, message: 'Vui lòng nhập Số điện thoại.' },
            { max: 20, message: 'Số điện thoại không được vượt quá 20 ký tự.' },
            { pattern: /^\+?[0-9 ().-]+$/, message: 'Số điện thoại không đúng định dạng.' }]}><Input maxLength={20} /></Form.Item>
        <Form.Item name="address" label="Địa chỉ" rules={[{ required: required?.address, whitespace: true, message: 'Vui lòng nhập Địa chỉ.' }, { max: 255, message: 'Địa chỉ không được vượt quá 255 ký tự.' }]}><Input maxLength={255} /></Form.Item>
      </div>
      <Typography.Title level={5}>Thông tin bổ sung</Typography.Title>
      <div className="patient-form-grid">
        <Form.Item label="Ngày lập hồ sơ" required={required?.profileDate}>
          <Space.Compact block>
            <Form.Item name="profileDate" noStyle rules={[{ required: required?.profileDate, message: 'Vui lòng nhập Ngày lập hồ sơ.' },
              { validator: (_, value?: string) => !value || value <= today() ? Promise.resolve() : Promise.reject(new Error('Ngày lập hồ sơ không được ở tương lai.')) }]}>
              <Input type="date" max={today()} /></Form.Item>
            <Button onClick={() => form.setFieldValue('profileDate', today())}>Hôm nay</Button>
          </Space.Compact>
        </Form.Item>
        <Form.Item name="sourceId" label="Nguồn" rules={[{ required: required?.source, message: 'Vui lòng chọn Nguồn.' }]}>
          <Select allowClear showSearch optionFilterProp="label" placeholder="Chưa chọn" loading={!catalogs['patient-sources'] && !catalogError}
            options={choices(catalogs['patient-sources'], [patient?.source])} /></Form.Item>
        <Form.Item name="customerGroupId" label="Phân nhóm khách hàng" rules={[{ required: required?.customerGroup, message: 'Vui lòng chọn Phân nhóm khách hàng.' }]}>
          <Select allowClear showSearch optionFilterProp="label" placeholder="Chưa chọn" loading={!catalogs['customer-groups'] && !catalogError}
            options={choices(catalogs['customer-groups'], [patient?.customerGroup])} /></Form.Item>
        <Form.Item name="occupationId" label="Nghề nghiệp">
          <Select allowClear showSearch optionFilterProp="label" placeholder="Chưa chọn" loading={!catalogs.occupations && !catalogError}
            options={choices(catalogs.occupations, [patient?.occupation])} /></Form.Item>
      </div>
      <Form.Item name="intakeHistoryIds" label="Tiền sử (bệnh nhân tự khai)" extra="Thông tin tham khảo; nha sĩ vẫn ghi nhận tình trạng sức khỏe khi khám.">
        <Checkbox.Group options={choices(catalogs['intake-history'], patient?.intakeHistory ?? [])} /></Form.Item>
      <Form.Item name="intakeHistoryOther" label="Tiền sử khác" rules={[{ max: 1000, message: 'Tiền sử khác không được vượt quá 1000 ký tự.' }]}>
        <Input.TextArea rows={2} maxLength={1000} showCount /></Form.Item>
      <Typography.Title level={5}>Người giám hộ</Typography.Title>
      <Typography.Paragraph type="secondary">Nhập khi bệnh nhân cần người đại diện. Thông tin này không cấp quyền xem hồ sơ qua ứng dụng.</Typography.Paragraph>
      <div className="patient-form-grid">
        <Form.Item name="guardianName" label="Tên người giám hộ" dependencies={['guardianRelationship', 'guardianPhone']}
          rules={[{ max: 160, message: 'Tên người giám hộ không được vượt quá 160 ký tự.' },
            ({ getFieldValue }) => ({ validator: (_, value?: string) => value?.trim() || !(getFieldValue('guardianRelationship')?.trim() || getFieldValue('guardianPhone')?.trim())
              ? Promise.resolve() : Promise.reject(new Error('Vui lòng nhập tên người giám hộ.')) })]}><Input maxLength={160} /></Form.Item>
        <Form.Item name="guardianRelationship" label="Quan hệ với bệnh nhân" dependencies={['guardianName']}
          rules={[{ max: 40, message: 'Quan hệ với bệnh nhân không được vượt quá 40 ký tự.' },
            ({ getFieldValue }) => ({ validator: (_, value?: string) => value?.trim() || !getFieldValue('guardianName')?.trim()
              ? Promise.resolve() : Promise.reject(new Error('Vui lòng nhập quan hệ với bệnh nhân.')) })]}><Input maxLength={40} placeholder="Ví dụ: Mẹ" /></Form.Item>
        <Form.Item name="guardianPhone" label="SĐT người giám hộ" rules={[{ max: 20, message: 'SĐT người giám hộ không được vượt quá 20 ký tự.' },
          { pattern: /^\+?[0-9 ().-]+$/, message: 'Số điện thoại người giám hộ không đúng định dạng.' }]}><Input maxLength={20} /></Form.Item>
      </div>
      <Form.Item name="note" label="Ghi chú hành chính" rules={[{ max: 1000, message: 'Ghi chú không được vượt quá 1000 ký tự.' }]}>
        <Input.TextArea rows={3} maxLength={1000} showCount /></Form.Item>
    </Form>
  </Drawer>;
}
