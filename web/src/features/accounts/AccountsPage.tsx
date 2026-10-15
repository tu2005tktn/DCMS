import { Alert, Button, Card, Drawer, Empty, Form, Input, Modal, Select, Space, Table, Tag, Typography, message } from 'antd';
import type { TableProps } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api, type SystemMessage } from '../../services/api';
import { useAuth } from '../authentication/AuthContext';
import AccountEditor from './AccountEditor';
import AccountDetails from './AccountDetails';
import { type Account, type AccountPage, type AccountCatalog, formatAccountTime, roleLabels } from './types';

type Audit = { id: number; actorId: number; actorUserName: string; actorFullName: string; targetId: number; occurredAt: string; action: string; reason: string; beforeJson: string; afterJson: string };
export default function AccountsPage() {
  const auth = useAuth();
  const [toast, toastContext] = message.useMessage();
  const [data, setData] = useState<AccountPage>({ items: [], total: 0, page: 1, pageSize: 20 });
  const [roles, setRoles] = useState<string[]>([]);
  const [catalog, setCatalog] = useState<AccountCatalog | null>(null);
  const [role, setRole] = useState<string>();
  const [status, setStatus] = useState<string>();
  const [sort, setSort] = useState({ field: 'id', direction: 'asc' });
  const [detailId, setDetailId] = useState<number | null>(null);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [revision, setRevision] = useState(0);
  const [busy, setBusy] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [editor, setEditor] = useState<Account | null | undefined>(undefined);
  const [stateTarget, setStateTarget] = useState<Account | null>(null);
  const [stateBusy, setStateBusy] = useState(false);
  const stateSubmitting = useRef(false);
  const [stateError, setStateError] = useState<string | null>(null);
  const [reasonForm] = Form.useForm();
  const [auditTarget, setAuditTarget] = useState<Account | null>(null);
  const [audit, setAudit] = useState<Audit[]>([]);
  const [auditBusy, setAuditBusy] = useState(false);
  const [auditError, setAuditError] = useState<string | null>(null);
  const auditRequest = useRef(0);
  useEffect(() => {
    let active = true;
    setBusy(true); setError(null);
    const query = new URLSearchParams({ search, page: String(page), pageSize: String(pageSize), sortBy: sort.field, sortDirection: sort.direction });
    if (role) query.set('role', role);
    if (status) query.set('status', status);
    Promise.all([api<AccountPage>(`/api/accounts?${query}`), api<AccountCatalog>('/api/accounts/catalog')])
      .then(([result, currentCatalog]) => { if (active) { setData(result); setRoles(currentCatalog.assignableRoles); setCatalog(currentCatalog); } })
      .catch(failure => { if (active) { setError((failure as Error).message); void auth.refresh().catch(() => {}); } })
      .finally(() => { if (active) setBusy(false); });
    return () => { active = false; };
  }, [search, page, pageSize, role, status, sort.field, sort.direction, revision]);
  const tableChanged: TableProps<Account>['onChange'] = (pagination, _, sorter, extra) => {
    setPage(extra.action === 'sort' || pagination.pageSize !== pageSize ? 1 : pagination.current ?? 1);
    setPageSize(pagination.pageSize ?? 20);
    const selected = Array.isArray(sorter) ? sorter[0] : sorter;
    setSort({ field: selected.order ? String(selected.field) : 'id', direction: selected.order === 'descend' ? 'desc' : 'asc' });
  };
  async function changeState() {
    if (stateSubmitting.current || !stateTarget) return;
    stateSubmitting.current = true;
    try {
      const values = await reasonForm.validateFields();
      setStateBusy(true); setStateError(null);
      const result = await api<{ notification: SystemMessage }>(`/api/accounts/${stateTarget.id}/state`, {
        method: 'PUT', body: JSON.stringify({ version: stateTarget.version, locked: stateTarget.isActive, reason: values.reason }),
      });
      void toast.success(result.notification.message); setStateTarget(null); setRevision(value => value + 1);
      await auth.refresh();
    } catch (failure) { if (failure instanceof Error) setStateError(failure.message); }
    finally { stateSubmitting.current = false; setStateBusy(false); }
  }
  async function viewAudit(account: Account) {
    const request = ++auditRequest.current;
    setAuditTarget(account); setAudit([]); setAuditError(null); setAuditBusy(true);
    try { const items = await api<Audit[]>(`/api/accounts/${account.id}/audit`); if (request === auditRequest.current) setAudit(items); }
    catch (failure) { if (request === auditRequest.current) setAuditError((failure as Error).message); }
    finally { if (request === auditRequest.current) setAuditBusy(false); }
  }
  return <>
    {toastContext}
    <div className="page-heading"><div><Typography.Title level={2}>Quản lý tài khoản</Typography.Title>
      <Typography.Paragraph>Tạo tài khoản nhân viên, cấp vai trò và quản lý trạng thái truy cập.</Typography.Paragraph></div>
      <Button type="primary" onClick={() => setEditor(null)} disabled={roles.length === 0}>Tạo nhân viên</Button></div>
    <Card>
      <div className="table-tools"><Input.Search placeholder="Tìm mã, tên hoặc liên hệ" allowClear maxLength={160}
        onSearch={value => { setSearch(value); setPage(1); }} enterButton="Tìm kiếm" style={{ maxWidth: 460 }} />
        <Space wrap>
          <Select aria-label="Lọc vai trò" placeholder="Tất cả vai trò" allowClear value={role} style={{ minWidth: 190 }}
            options={catalog?.roles.map(value => ({ value, label: roleLabels[value] ?? value }))}
            onChange={value => { setRole(value); setPage(1); }} />
          <Select aria-label="Lọc trạng thái" placeholder="Tất cả trạng thái" allowClear value={status} style={{ minWidth: 170 }}
            options={catalog?.statuses.map(value => ({ value, label: value === 'Active' ? 'Hoạt động' : 'Đã khóa' }))}
            onChange={value => { setStatus(value); setPage(1); }} />
          <Button onClick={() => setRevision(value => value + 1)} loading={busy}>Tải lại</Button>
        </Space></div>
      {error && <Alert className="form-alert" type="error" message={error} showIcon />}
      <Table<Account> rowKey="id" dataSource={error ? [] : data.items} loading={busy} scroll={{ x: 1650 }} onChange={tableChanged}
        locale={{ emptyText: <Empty description={error ? 'Chưa tải được dữ liệu.' : 'Không tìm thấy kết quả phù hợp.'} /> }}
        pagination={{ current: page, pageSize, total: error ? 0 : data.total, showSizeChanger: true, pageSizeOptions: [20, 50, 100] }}
        columns={[
          { title: 'Mã tài khoản', dataIndex: 'id', sorter: true },
          { title: 'Mã nhân viên', dataIndex: 'employeeCode', sorter: true },
          { title: 'Tên đăng nhập', dataIndex: 'userName', sorter: true },
          { title: 'Họ tên', dataIndex: 'fullName', sorter: true },
          { title: 'Email', dataIndex: 'email', sorter: true },
          { title: 'Số điện thoại', dataIndex: 'phone', sorter: true },
          { title: 'Loại', render: (_, account) => account.accountType === 'Staff' ? 'Nhân viên' : 'Công khai' },
          { title: 'Vai trò', render: (_, account) => <Space wrap>{account.roles.map(role => <Tag key={role}>{roleLabels[role] ?? role}</Tag>)}</Space> },
          { title: 'Trạng thái', dataIndex: 'isActive', sorter: true, render: (_, account) => <Tag color={account.isActive ? 'success' : 'error'}>{account.isActive ? 'Hoạt động' : 'Đã khóa'}</Tag> },
          { title: 'Cập nhật lần cuối', dataIndex: 'updatedAt', sorter: true, render: formatAccountTime },
          { title: 'Thao tác', render: (_, account) => <Space>
            <Button size="small" onClick={() => setDetailId(account.id)}>Chi tiết</Button>
            {account.accountType === 'Staff' && <Button size="small" onClick={() => setEditor(account)}>Sửa</Button>}
            <Button size="small" danger={account.isActive} onClick={() => { setStateTarget(account); setStateError(null); reasonForm.resetFields(); }}>{account.isActive ? 'Khóa' : 'Mở khóa'}</Button>
            <Button size="small" onClick={() => void viewAudit(account)}>Audit</Button>
          </Space> },
        ]} />
    </Card>
    {detailId !== null && <AccountDetails id={detailId} onClose={() => setDetailId(null)} onEdit={account => { setDetailId(null); setEditor(account); }} />}
    {editor !== undefined && <AccountEditor account={editor} roles={roles} onClose={() => setEditor(undefined)} onSaved={notice => {
      setEditor(undefined); void toast.success(notice); setRevision(value => value + 1); void auth.refresh().catch(() => {});
    }} />}
    <Modal open={!!stateTarget} title={stateTarget?.isActive ? 'Xác nhận khóa tài khoản' : 'Xác nhận mở khóa tài khoản'}
      onCancel={() => { if (!stateSubmitting.current) setStateTarget(null); }} onOk={() => void changeState().catch(() => {})} confirmLoading={stateBusy}
      maskClosable={false} closable={!stateBusy} cancelButtonProps={{ disabled: stateBusy }} okText="Xác nhận" cancelText="Hủy">
      <Typography.Paragraph>{stateTarget?.userName}: {stateTarget?.isActive
        ? 'Các phiên hiện tại sẽ bị vô hiệu hóa ngay. Nếu khóa tài khoản của bạn, bạn sẽ cần một quản trị viên khác để mở khóa.'
        : 'Tài khoản có thể đăng nhập lại. Mở khóa không xác minh hoặc khôi phục liên kết bệnh án.'}</Typography.Paragraph>
      {stateError && <Alert className="form-alert" type="error" message={stateError} showIcon />}
      <Form form={reasonForm} layout="vertical" disabled={stateBusy}><Form.Item name="reason" label="Lý do" rules={[{ required: true, whitespace: true, message: 'Vui lòng nhập Lý do.' }]}>
        <Input.TextArea rows={3} maxLength={500} /></Form.Item></Form>
    </Modal>
    <Drawer open={!!auditTarget} title={`Audit · ${auditTarget?.userName ?? ''}`} width={640}
      onClose={() => { auditRequest.current++; setAuditTarget(null); }} loading={auditBusy}>
      {auditError && <Alert type="error" showIcon message={auditError} />}
      <Typography.Paragraph type="secondary">Tối đa 100 sự kiện mới nhất. Toàn bộ lịch sử được giữ trong hệ thống.</Typography.Paragraph>
      {auditError && auditTarget && <Button onClick={() => void viewAudit(auditTarget)}>Thử lại</Button>}
      {!auditBusy && !auditError && audit.length === 0 && <Empty description="Không tìm thấy kết quả phù hợp." />}
      {audit.map(entry => <Card key={entry.id} size="small" className="audit-entry" title={entry.action}>
        <Typography.Paragraph>{entry.actorFullName} ({entry.actorUserName}, #{entry.actorId}) · Đối tượng #{entry.targetId}<br />{formatAccountTime(entry.occurredAt)}<br />{entry.reason}</Typography.Paragraph>
        <details><summary>Xem dữ liệu trước và sau</summary><pre>{JSON.stringify({ before: JSON.parse(entry.beforeJson), after: JSON.parse(entry.afterJson) }, null, 2)}</pre></details>
      </Card>)}
    </Drawer>
  </>;
}
