import { Alert, Button, Checkbox, Drawer, Empty, Form, Input, Modal, Select, Space, Spin, Table, Tabs, Tag, Typography } from 'antd';
import { useEffect, useRef, useState, type ReactNode } from 'react';
import { api, ApiError } from '../../services/api';
import { useAuth } from '../authentication/AuthContext';
import { roleLabels } from '../accounts/types';
import { editableGrants, toggleGrant, type AccessCapabilities, type AccessGroup, type GrantOption, type GroupPreview, type GroupValues, type StaffAccess } from './accessGroups';
import './accessGroups.css';

const base = '/api/system-settings/access-groups';
type Data = { groups: AccessGroup[]; staff: StaffAccess[]; options: GrantOption[]; capabilities: AccessCapabilities };
type Editor = { row?: AccessGroup; values: GroupValues; writable: boolean };
type Audit = { id: string; actorId: number; target: string; action: string; reason: string; at: string };
const roles = ['ClinicAssistant', 'Dentist', 'Admin'];
const emptyValues = (): GroupValues => ({ code: '', name: '', description: '', baseRole: 'ClinicAssistant', grants: [], reason: '' });

export default function AccessGroupsPanel() {
  const auth = useAuth();
  const [dialogs, dialogContext] = Modal.useModal();
  const [data, setData] = useState<Data>(), [loadError, setLoadError] = useState<string>(), [loading, setLoading] = useState(false);
  const [editor, setEditor] = useState<Editor>(), [preview, setPreview] = useState<GroupPreview>(), [query, setQuery] = useState('');
  const [failure, setFailure] = useState<ApiError>(), [busy, setBusy] = useState(false);
  const [assignment, setAssignment] = useState<{ staff: StaffAccess; groupId?: string; branches: number[]; reason: string }>();
  const [stateChange, setStateChange] = useState<{ group: AccessGroup; reason: string }>();
  const [audit, setAudit] = useState<{ rows?: Audit[]; error?: string }>();
  const generation = useRef(0), guard = useRef(false), original = useRef('');
  const pending = useRef<{ fingerprint: string; key: string } | undefined>(undefined);
  const can = (key: string) => auth.user?.permissions.includes(key) ?? false;
  const mayEdit = !!editor?.writable && can('access-groups.manage');
  const ownGroup = data?.staff.find(staff => staff.accountId === auth.user?.id)?.groupId;
  async function load() {
    const token = ++generation.current; setLoading(true); setLoadError(undefined);
    try {
      const [groups, staff, options, capabilities] = await Promise.all([
        api<AccessGroup[]>(base + '/'), api<StaffAccess[]>(base + '/staff'), api<GrantOption[]>(base + '/grant-options'), api<AccessCapabilities>('/api/system-settings/access-capabilities'),
      ]);
      if (token !== generation.current) return;
      const value = { groups, staff, options, capabilities }; setData(value); return value;
    } catch (error) { if (token === generation.current) setLoadError((error as Error).message); }
    finally { if (token === generation.current) setLoading(false); }
  }
  const permissionKey = auth.user?.permissions.join('|');
  useEffect(() => { void load(); return () => { generation.current++; }; }, [permissionKey]);
  function commandKey(fingerprint: string) {
    if (pending.current?.fingerprint !== fingerprint) pending.current = { fingerprint, key: crypto.randomUUID() };
    return pending.current.key;
  }
  function open(group?: AccessGroup) {
    if (!data) return;
    const values = group ? { code: group.code, name: group.name, description: group.description ?? '', baseRole: group.baseRole, grants: editableGrants(group.grants, data.options), reason: '' } : emptyValues();
    original.current = JSON.stringify(values); pending.current = undefined; setPreview(undefined); setFailure(undefined); setQuery('');
    setEditor({ row: group, values, writable: can('access-groups.manage') && (!group || group.id !== ownGroup) });
  }
  function change(patch: Partial<GroupValues>) {
    setEditor(value => value ? { ...value, values: { ...value.values, ...patch } } : value); setPreview(undefined); setFailure(undefined); pending.current = undefined;
  }
  async function closeEditor() {
    if (busy || !editor) return;
    if (editor.writable && JSON.stringify(editor.values) !== original.current && !await dialogs.confirm({ title: 'Bỏ thay đổi phân quyền chưa lưu?', okText: 'Bỏ thay đổi', cancelText: 'Tiếp tục sửa' })) return;
    setEditor(undefined);
  }
  async function run(action: () => Promise<void>) {
    if (guard.current) return; guard.current = true; setBusy(true); setFailure(undefined);
    try { await action(); } catch (error) { setFailure(error as ApiError); }
    finally { guard.current = false; setBusy(false); }
  }
  async function save() {
    if (!editor || !mayEdit) return;
    const { row, values } = editor;
    if (!values.name.trim() || !values.code.trim() || row && !values.reason.trim()) { setFailure(new ApiError(400, 'MSG02', 'Nhập tên, mã phân quyền và lý do thay đổi (khi sửa).')); return; }
    const body = { entry: { code: values.code.trim(), name: values.name.trim(), baseRole: values.baseRole, description: values.description.trim() || null, grants: values.grants }, expectedVersion: row?.version ?? 0, reason: values.reason.trim() || null };
    const requestKey = commandKey(JSON.stringify({ id: row?.id, body }));
    await run(async () => {
      if (row && !preview) { setPreview(await api<GroupPreview>(`${base}/${row.id}/preview`, { method: 'POST', body: JSON.stringify({ ...body, requestKey }) })); return; }
      await api(`${base}/${row?.id ?? ''}`, { method: row ? 'PUT' : 'POST', body: JSON.stringify({ ...body, requestKey }) });
      setEditor(undefined); pending.current = undefined; await load();
    });
  }
  async function reloadEditor() {
    if (!editor?.row) return;
    await run(async () => {
      const latest = await api<AccessGroup>(`${base}/${editor.row!.id}`);
      setEditor(current => current ? { ...current, row: latest } : current); setPreview(undefined); pending.current = undefined;
    });
  }
  async function assign() {
    if (!assignment || !data || !can('staff.access.assign')) return;
    const group = data.groups.find(group => group.id === assignment.groupId);
    if (!group || !assignment.branches.length || !assignment.reason.trim()) { setFailure(new ApiError(400, 'MSG02', 'Chọn nhóm quyền, chi nhánh và nhập lý do gán nhóm.')); return; }
    const body = { groupId: group.id, expectedVersion: assignment.staff.accountVersion, groupVersion: group.version, branchIds: assignment.branches, reason: assignment.reason.trim() };
    const requestKey = commandKey(JSON.stringify({ employeeId: assignment.staff.employeeId, body }));
    await run(async () => {
      await api(`/api/system-settings/staff/${assignment.staff.employeeId}/access-group`, { method: 'PUT', body: JSON.stringify({ ...body, requestKey }) });
      setAssignment(undefined); pending.current = undefined; await load();
    });
  }
  async function reloadAssignment() {
    await run(async () => {
      const latest = await load(); if (!latest) return;
      setAssignment(current => { const staff = latest.staff.find(staff => staff.employeeId === current?.staff.employeeId); return current && staff ? { ...current, staff } : current; });
      pending.current = undefined;
    });
  }
  async function saveState() {
    if (!can('access-groups.manage')) return;
    if (!stateChange || !stateChange.reason.trim()) { setFailure(new ApiError(400, 'MSG02', 'Nhập lý do đổi trạng thái nhóm.')); return; }
    const group = stateChange.group, body = { active: !group.active, expectedVersion: group.version, reason: stateChange.reason.trim() };
    const requestKey = commandKey(JSON.stringify({ id: group.id, body }));
    await run(async () => { await api(`${base}/${group.id}/state`, { method: 'POST', body: JSON.stringify({ ...body, requestKey }) }); setStateChange(undefined); pending.current = undefined; await load(); });
  }
  async function openAudit() {
    setAudit({});
    try { const page = await api<{ items: Audit[] }>('/api/system-settings/settings/audit?section=access-groups&pageSize=100'); setAudit({ rows: page.items }); }
    catch (error) { setAudit({ error: (error as Error).message }); }
  }
  const failureView = failure && <Alert className="access-group-error" role="alert" type="error" showIcon message={failure.message} />;
  if (!data) return <>{loadError ? <Alert type="error" message={loadError} action={<Button onClick={() => void load()}>Thử lại phân quyền</Button>} /> : <Spin />}</>;
  const canGrant = (option: GrantOption) => !!editor && option.allowedBaseRoles.includes(editor.values.baseRole) && (data.capabilities.owner || can(option.key));
  const matches = (option: GrantOption) => `${option.label} ${option.key}`.toLocaleLowerCase('vi').includes(query.toLocaleLowerCase('vi'));
  const visible = (option: GrantOption): boolean => !query || matches(option) || data.options.some(child => child.parent === option.key && visible(child));
  function tree(parent: string | null, parentMatches = false): ReactNode {
    const order = ['appointments.read','working-schedules.read','booking-settings.read','patients.read','reception.read','appointments.support.handle','examinations.read','treatment.read','treatment.templates.read','treatment.prices.read','finance.read','services.read','care-tasks.read','notifications.read','accounts.read','settings.read'];
    const entries = data!.options.filter(option => option.parent === parent && (parentMatches || visible(option)));
    if (parent === null) entries.sort((a, b) => order.indexOf(a.key) - order.indexOf(b.key));
    return entries.map(option => <div className={parent ? 'access-grant-child' : 'access-grant-section'} key={option.key}>
      <Checkbox checked={editor?.values.grants.includes(option.key)} disabled={busy || !mayEdit || !canGrant(option)}
        onChange={event => change({ grants: toggleGrant(editor!.values.grants, option.key, event.target.checked, data!.options) })}>
        <span className={!parent ? 'access-grant-heading' : undefined}>{option.label}</span>
        {!option.allowedBaseRoles.includes(editor?.values.baseRole ?? '') && <Typography.Text type="secondary"> · Theo vai trò chuyên môn khác</Typography.Text>}
      </Checkbox>
      {tree(option.key, parentMatches || !!query && matches(option))}
    </div>);
  }
  const grantLabel = (key: string) => data.options.find(option => option.key === key)?.label ?? (key === 'patients.manage' ? 'Quản lý hành chính bệnh nhân (quyền cũ)' : key);
  const groupOptions = assignment ? data.groups.filter(group => group.active && assignment.staff.roles.includes(group.baseRole)
    && (data.capabilities.owner || group.grants.every(grant => can(grant)))).map(group => ({ value: group.id, label: group.name })) : [];
  const editorAction = !mayEdit ? 'Đóng' : editor?.row && !preview ? 'Xem trước thay đổi' : 'Lưu phân quyền';
  return <section aria-label="Phân quyền động">
    {dialogContext}
    <Typography.Paragraph>Thiết lập nhóm quyền theo từng màn hình và thao tác, sau đó gán cho nhân viên cùng phạm vi chi nhánh nghiệp vụ.</Typography.Paragraph>
    <Space wrap className="access-group-toolbar"><Button loading={loading} disabled={busy} onClick={() => void load()}>Tải lại phân quyền</Button>
      {can('access-groups.manage') && <Button type="primary" disabled={busy} onClick={() => open()}>Thêm nhóm quyền</Button>}
      {can('settings.audit.read') && <Button onClick={() => void openAudit()}>Lịch sử phân quyền</Button>}
    </Space>
    {loadError && <Alert type="error" showIcon message={loadError} />}
    <Tabs items={[
      { key: 'groups', label: 'Nhóm quyền', children: <Table<AccessGroup> rowKey="id" dataSource={data.groups} loading={loading} scroll={{ x: 850 }} columns={[
        { title: 'Tên phân quyền', render: (_, group) => <><Typography.Text strong>{group.name}</Typography.Text><br /><Typography.Text type="secondary">{group.code}</Typography.Text></> },
        { title: 'Vai trò nền', render: (_, group) => roleLabels[group.baseRole] ?? group.baseRole },
        { title: 'Nhân viên', dataIndex: 'assignedCount' },
        { title: 'Trạng thái', render: (_, group) => <Tag color={group.active ? 'green' : 'default'}>{group.active ? 'Đang dùng' : 'Ngừng dùng'}</Tag> },
        { title: 'Thao tác', render: (_, group) => <Space wrap><Button aria-label={`Thiết lập ${group.name}`} onClick={() => open(group)}>Thiết lập</Button>
          {can('access-groups.manage') && group.id !== ownGroup && <Button disabled={busy || group.active && group.assignedCount > 0}
            title={group.active && group.assignedCount > 0 ? 'Chuyển nhân viên sang nhóm khác trước khi ngừng nhóm.' : undefined}
            aria-label={`${group.active ? 'Ngừng nhóm' : 'Khôi phục nhóm'} ${group.name}`} onClick={() => { setFailure(undefined); pending.current = undefined; setStateChange({ group, reason: '' }); }}>{group.active ? 'Ngừng dùng' : 'Khôi phục'}</Button>}</Space> },
      ]} /> },
      { key: 'staff', label: 'Gán cho nhân viên', children: <Table<StaffAccess> rowKey="employeeId" dataSource={data.staff} loading={loading} scroll={{ x: 850 }} columns={[
        { title: 'Nhân viên', render: (_, staff) => <>{staff.name}{!staff.active && <Tag>Đã khóa</Tag>}</> },
        { title: 'Vai trò', render: (_, staff) => staff.roles.map(role => roleLabels[role] ?? role).join(', ') },
        { title: 'Nhóm quyền', render: (_, staff) => data.groups.find(group => group.id === staff.groupId)?.name ?? 'Theo quyền vai trò hiện hành' },
        { title: 'Chi nhánh nghiệp vụ', render: (_, staff) => staff.branchIds.map(id => data.capabilities.branches.find(branch => branch.id === id)?.name ?? `#${id}`).join(', ') || 'Chưa có' },
        { title: 'Thao tác', render: (_, staff) => can('staff.access.assign') && <Button aria-label={`Gán quyền ${staff.name}`}
          disabled={busy || staff.accountId === auth.user?.id || staff.roles.includes('ClinicOwner')}
          onClick={() => { setFailure(undefined); pending.current = undefined; setAssignment({ staff, groupId: staff.groupId ?? undefined, branches: [...staff.branchIds], reason: '' }); }}>Gán nhóm quyền</Button> },
      ]} /> },
    ]} />
    <Modal open={!!editor} title="Thiết lập phân quyền" width={980} className="access-group-modal" onCancel={() => void closeEditor()}
      okText={editorAction} okButtonProps={{ 'aria-label': editorAction, disabled: busy }} cancelText="Hủy" confirmLoading={busy}
      closable={!busy} cancelButtonProps={{ disabled: busy }} onOk={() => mayEdit ? void save() : void closeEditor()}>
      {editor && <><Form layout="vertical" disabled={!mayEdit || busy}>
        <div className="access-group-fields"><Form.Item label="Tên phân quyền" required><Input aria-label="Tên phân quyền" value={editor.values.name} maxLength={160} onChange={e => change({ name: e.target.value })} /></Form.Item>
          <Form.Item label="Mã phân quyền" required><Input aria-label="Mã phân quyền" value={editor.values.code} maxLength={40} onChange={e => change({ code: e.target.value })} /></Form.Item></div>
        <Form.Item label="Vai trò nền" required><Select aria-label="Vai trò nền" value={editor.values.baseRole} disabled={!mayEdit || busy || !!editor.row?.assignedCount}
          options={roles.map(value => ({ value, label: roleLabels[value] ?? value }))} onChange={baseRole => change({ baseRole, grants: editor.values.grants.filter(key => data.options.find(option => option.key === key)?.allowedBaseRoles.includes(baseRole)) })} /></Form.Item>
        <Form.Item label="Mô tả"><Input.TextArea aria-label="Mô tả phân quyền" value={editor.values.description} maxLength={1000} onChange={e => change({ description: e.target.value })} /></Form.Item>
      </Form>
      {!mayEdit && <Alert type="info" message={editor.row?.id === ownGroup ? 'Bạn đang thuộc nhóm này. Thay đổi cần được thực hiện bởi người quản lý khác.' : 'Bạn có quyền xem nhóm phân quyền.'} />}
      <Input.Search aria-label="Tìm quyền" placeholder="Tìm màn hình hoặc thao tác" allowClear value={query} onChange={e => setQuery(e.target.value)} />
      <div className="access-grant-list" aria-label="Danh sách quyền">{tree(null)}</div>
      <Typography.Paragraph type="secondary">Quyền thao tác vẫn tuân theo vai trò chuyên môn và phạm vi chi nhánh được giao.</Typography.Paragraph>
      {editor.row && <Form.Item label="Lý do thay đổi phân quyền" required={mayEdit}><Input.TextArea aria-label="Lý do thay đổi phân quyền" value={editor.values.reason} maxLength={1000} disabled={busy || !mayEdit} onChange={e => change({ reason: e.target.value })} /></Form.Item>}
      {preview && <Alert type="info" showIcon message={`Ảnh hưởng ${preview.affected} nhân viên`} description={<>
        <div>Thêm: {preview.added.map(grantLabel).join('; ') || 'Không có'}</div><div>Thu hồi: {preview.removed.map(grantLabel).join('; ') || 'Không có'}</div>
      </>} />}
      {failureView}{failure?.code === 'MSG111' && <Button disabled={busy} onClick={() => void reloadEditor()}>Đọc lại phiên bản nhóm, giữ bản nhập</Button>}
      </>}
    </Modal>
    <Modal open={!!assignment} title={`Gán nhóm quyền · ${assignment?.staff.name ?? ''}`} okText="Lưu nhóm cho nhân viên" okButtonProps={{ 'aria-label': 'Lưu nhóm cho nhân viên', disabled: busy || !can('staff.access.assign') }} cancelText="Hủy" confirmLoading={busy}
      closable={!busy} cancelButtonProps={{ disabled: busy }} onCancel={() => { if (!busy) setAssignment(undefined); }} onOk={() => void assign()}>
      {assignment && <Form layout="vertical" disabled={busy || !can('staff.access.assign')}>
        <Form.Item label="Nhóm quyền" required><Select aria-label="Nhóm quyền nhân viên" value={assignment.groupId} options={groupOptions} onChange={groupId => { setAssignment({ ...assignment, groupId }); setFailure(undefined); }} /></Form.Item>
        {!groupOptions.length && <Alert type="info" message="Chưa có nhóm đang dùng phù hợp với vai trò nhân viên. Tạo nhóm phù hợp trước khi gán." />}
        <Form.Item label="Chi nhánh nghiệp vụ" required><Select mode="multiple" aria-label="Chi nhánh nghiệp vụ" value={assignment.branches}
          options={data.capabilities.branches.filter(branch => assignment.staff.membershipBranchIds.includes(branch.id)).map(branch => ({ value: branch.id, label: branch.name }))}
          onChange={branches => { setAssignment({ ...assignment, branches }); setFailure(undefined); }} /></Form.Item>
        <Typography.Paragraph type="secondary">Chọn trong các chi nhánh nhân viên đã tham gia và bạn được quản lý.</Typography.Paragraph>
        <Form.Item label="Lý do gán nhóm" required><Input.TextArea aria-label="Lý do gán nhóm" value={assignment.reason} maxLength={1000} onChange={e => { setAssignment({ ...assignment, reason: e.target.value }); setFailure(undefined); }} /></Form.Item>
        {failureView}{failure?.code === 'MSG111' && <Button disabled={busy} onClick={() => void reloadAssignment()}>Đọc lại phiên bản nhân viên và nhóm</Button>}
      </Form>}
    </Modal>
    <Modal open={!!stateChange} title={`${stateChange?.group.active ? 'Ngừng dùng' : 'Khôi phục'} nhóm quyền`} okText="Xác nhận trạng thái" okButtonProps={{ 'aria-label': 'Xác nhận trạng thái', disabled: busy || !can('access-groups.manage') }} cancelText="Hủy" confirmLoading={busy}
      closable={!busy} cancelButtonProps={{ disabled: busy }} onCancel={() => { if (!busy) setStateChange(undefined); }} onOk={() => void saveState()}>
      <Typography.Paragraph>{stateChange?.group.name}. Nhóm còn nhân viên phải được chuyển hết nhân viên trước khi ngừng.</Typography.Paragraph>
      <Form.Item label="Lý do đổi trạng thái" required><Input.TextArea aria-label="Lý do đổi trạng thái" disabled={busy || !can('access-groups.manage')} maxLength={1000} value={stateChange?.reason}
        onChange={e => { setStateChange(value => value ? { ...value, reason: e.target.value } : value); setFailure(undefined); }} /></Form.Item>
      {failureView}{failure?.code === 'MSG111' && <Button disabled={busy} onClick={() => void run(async () => { const group = await api<AccessGroup>(`${base}/${stateChange!.group.id}`); setStateChange(value => value ? { ...value, group } : value); pending.current = undefined; })}>Đọc lại trạng thái nhóm</Button>}
    </Modal>
    <Drawer open={!!audit} title="Lịch sử phân quyền" aria-label="Lịch sử phân quyền" width="min(900px, 100vw)" onClose={() => setAudit(undefined)}>
      {audit?.error ? <Alert type="error" message={audit.error} action={<Button onClick={() => void openAudit()}>Tải lại lịch sử</Button>} /> : <Table<Audit> rowKey="id" loading={!audit?.rows} dataSource={audit?.rows} locale={{ emptyText: <Empty description="Chưa có thay đổi phân quyền." /> }} columns={[
        { title: 'Thời gian', render: (_, item) => new Date(item.at).toLocaleString('vi-VN') },
        { title: 'Người thực hiện', render: (_, item) => data.staff.find(staff => staff.accountId === item.actorId)?.name ?? `#${item.actorId}` },
        { title: 'Thao tác', render: (_, item) => ({ 'group.save': 'Thiết lập nhóm', 'group.assign': 'Gán nhóm', 'group.state': 'Đổi trạng thái' })[item.action] ?? item.action },
        { title: 'Lý do', dataIndex: 'reason' },
      ]} />}
      <Typography.Paragraph type="secondary">100 thay đổi gần nhất trong phạm vi được cấp quyền.</Typography.Paragraph>
    </Drawer>
  </section>;
}
