import { Alert, Button, Space, Switch, Table, Tag, Typography } from 'antd';
import { useCallback, useEffect, useState } from 'react';
import { api, ApiError } from '../../services/api';

// docs/specs/S58-BRANCHES-AND-CHAIRS.md, "Nhân sự chi nhánh" (BUG-02).
type Member = {
  employeeId: number; employeeCode: string; fullName: string; userName: string; roles: string[];
  isMember: boolean; active: boolean; canDentist: boolean; canManageSchedules: boolean;
};
type Members = { branchId: number; version: number; members: Member[] };

const roleLabels: Record<string, string> = { ClinicOwner: 'Chủ phòng khám', Dentist: 'Nha sĩ', ClinicAssistant: 'Trợ lý phòng khám', SystemAdmin: 'Quản trị hệ thống' };

export default function BranchMembersSection({ branchId, canManage, onChanged }: { branchId: number; canManage: boolean; onChanged?: () => void }) {
  const [data, setData] = useState<Members>();
  const [busy, setBusy] = useState<number>();
  const [error, setError] = useState<string>();

  const load = useCallback(async () => {
    try { setData(await api<Members>(`/api/settings/branches/${branchId}/members`)); setError(undefined); }
    catch (e) { setError(e instanceof ApiError ? e.message : 'Không tải được nhân sự chi nhánh.'); }
  }, [branchId]);
  useEffect(() => { void load(); }, [load]);

  async function save(m: Member, change: Partial<Pick<Member, 'active' | 'canDentist' | 'canManageSchedules'>>) {
    if (!data) return;
    setBusy(m.employeeId); setError(undefined);
    try {
      const next = await api<Members>(`/api/settings/branches/${branchId}/members/${m.employeeId}`, {
        method: 'PUT', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ requestKey: crypto.randomUUID(), expectedVersion: data.version,
          active: change.active ?? m.active, canDentist: change.canDentist ?? m.canDentist, canManageSchedules: change.canManageSchedules ?? m.canManageSchedules }),
      });
      setData(next); onChanged?.();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Không lưu được nhân sự chi nhánh.');
      if (e instanceof ApiError && e.status === 409) void load();
    } finally { setBusy(undefined); }
  }

  const working = data?.members.filter(m => m.isMember && m.active).length ?? 0;
  return <section aria-label="Nhân sự chi nhánh" style={{ marginTop: 24 }}>
    <Typography.Title level={5} style={{ margin: '0 0 8px' }}>Nhân sự chi nhánh ({working} đang làm việc)</Typography.Title>
    {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 8 }} />}
    <Table<Member> rowKey="employeeId" size="small" pagination={false} loading={!data && !error} dataSource={data?.members ?? []}
      columns={[
        { title: 'Nhân viên', render: (_, m) => <><strong>{m.fullName}</strong><br /><Typography.Text type="secondary">@{m.userName} · {m.employeeCode}</Typography.Text></> },
        { title: 'Vai trò', render: (_, m) => m.roles.map(r => <Tag key={r}>{roleLabels[r] ?? r}</Tag>) },
        { title: 'Trạng thái', render: (_, m) => !m.isMember ? <Tag>Chưa thuộc chi nhánh</Tag> : m.active ? <Tag color="green">Đang làm việc</Tag> : <Tag color="default">Ngừng</Tag> },
        { title: 'Khám như nha sĩ', render: (_, m) => <Switch size="small" aria-label={`Khám như nha sĩ ${m.fullName}`} checked={m.canDentist}
            disabled={!canManage || !m.isMember || busy !== undefined} onChange={v => void save(m, { canDentist: v })} /> },
        { title: 'Quản lý lịch', render: (_, m) => <Switch size="small" aria-label={`Quản lý lịch ${m.fullName}`} checked={m.canManageSchedules}
            disabled={!canManage || !m.isMember || busy !== undefined} onChange={v => void save(m, { canManageSchedules: v })} /> },
        ...(canManage ? [{ title: 'Thao tác', render: (_: unknown, m: Member) => <Space>
          {!m.isMember || !m.active
            ? <Button size="small" type="primary" loading={busy === m.employeeId} aria-label={`${m.isMember ? 'Làm việc lại' : 'Thêm vào chi nhánh'} ${m.fullName}`}
                onClick={() => void save(m, { active: true, canDentist: m.isMember ? m.canDentist : m.roles.includes('Dentist') })}>{m.isMember ? 'Làm việc lại' : 'Thêm vào chi nhánh'}</Button>
            : <Button size="small" danger loading={busy === m.employeeId} aria-label={`Ngừng ${m.fullName}`} onClick={() => void save(m, { active: false })}>Ngừng</Button>}
        </Space> }] : []),
      ]} />
  </section>;
}
