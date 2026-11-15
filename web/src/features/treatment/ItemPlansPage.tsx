import { usePermissions } from '../authentication/usePermissions';
import { Alert, Button, Card, Empty, Input, Modal, Pagination, Popconfirm, Result, Select, Skeleton, Space, Table, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';

import { api, ApiError } from '../../services/api';
import { useAuth } from '../authentication/AuthContext';
import ItemPlanRowEditor from './ItemPlanRowEditor';
import { itemPlanApi, planMoney, type ItemPlan, type ItemPlanInput, type ItemPlanOptions, type ItemPlanPage, type ItemPlanRow } from './itemPlanTypes';
import './itemPlans.css';
import { useCanPickBranch } from '../authentication/useCanPickBranch';

export default function ItemPlansPage() {
  const { user } = useAuth();
  return user?.effectiveRoles.includes('Dentist') ? <ItemPlansWorkspace key={user.id} /> : <Result status="403" title="Bạn không có quyền quản lý kế hoạch điều trị." />;
}
function ItemPlansWorkspace() {
  const canPickBranch = useCanPickBranch();
  const has = usePermissions();
  const canManage = has('treatment.templates.manage');
  const [branches, setBranches] = useState<{ id: number; name: string }[]>([]), [branch, setBranch] = useState<number>();
  const [data, setData] = useState<ItemPlanPage | null>(null), [options, setOptions] = useState<ItemPlanOptions | null>(null);
  const [plan, setPlan] = useState<ItemPlan | null>(null), [editing, setEditing] = useState<ItemPlanRow | 'new' | null>(null);
  const [search, setSearch] = useState(''), [keyword, setKeyword] = useState(''), [page, setPage] = useState(1), [revision, setRevision] = useState(0);
  const [loading, setLoading] = useState(true), [busy, setBusy] = useState(false), [error, setError] = useState<ApiError | null>(null), [listError, setListError] = useState('');
  const [nameDialog, setNameDialog] = useState<'create' | 'rename' | null>(null), [name, setName] = useState('');
  const [dirty, setDirty] = useState(false), [latest, setLatest] = useState<ItemPlan | null>(null);
  const sending = useRef(false), retry = useRef(new Map<string, { payload: string; key: string }>());
  const canLeave = () => !dirty || window.confirm('Bỏ các thay đổi chưa lưu?');
  useEffect(() => {
    const warn = (e: BeforeUnloadEvent) => { if (dirty) e.preventDefault(); };
    const navigate = (e: MouseEvent) => { if ((e.target as HTMLElement).closest('a') && dirty && !window.confirm('Bỏ các thay đổi chưa lưu?')) { e.preventDefault(); e.stopPropagation(); } };
    window.addEventListener('beforeunload', warn); document.addEventListener('click', navigate, true);
    return () => { window.removeEventListener('beforeunload', warn); document.removeEventListener('click', navigate, true); };
  }, [dirty]);
  useEffect(() => {
    let active = true;
    api<{ branches: { id: number; name: string }[] }>('/api/appointments/options').then(x => { if (active) { setBranches(x.branches); setBranch(x.branches[0]?.id); setLoading(false); } })
      .catch(e => { if (active) { setListError(e.message); setLoading(false); } });
    return () => { active = false; };
  }, []);
  useEffect(() => {
    if (!branch) return;
    let active = true; setLoading(true); setListError('');
    Promise.all([api<ItemPlanPage>(`${itemPlanApi}?${new URLSearchParams({ branchId: String(branch), keyword, page: String(page), pageSize: '20' })}`),
      api<ItemPlanOptions>(`${itemPlanApi}/options?branchId=${branch}`)])
      .then(([list, opts]) => { if (active) { if (page > 1 && !list.items.length) setPage(p => p - 1); else setData(list); setOptions(opts); } })
      .catch(e => { if (active) setListError(e.message); }).finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [branch, keyword, page, revision]);
  async function open(id: string) {
    if (!canLeave() || sending.current) return;
    setBusy(true); setError(null);
    try { setPlan(await api<ItemPlan>(`${itemPlanApi}/${id}`)); setEditing(null); setDirty(false); setLatest(null); }
    catch (e) { setError(e as ApiError); } finally { setBusy(false); }
  }
  async function write(path: string, method: string, body: object, deleted = false) {
    if (!canManage || sending.current) return; sending.current = true; setBusy(true); setError(null);
    const payload = JSON.stringify(body), action = method + path;
    const previous = retry.current.get(action), key = previous?.payload === payload ? previous.key : crypto.randomUUID();
    retry.current.set(action, { payload, key });
    try {
      const saved = await api<ItemPlan>(path, { method, body: JSON.stringify({ ...body, requestKey: key }) });
      setPlan(deleted ? null : saved); setEditing(null); setNameDialog(null); setDirty(false); setLatest(null);
      retry.current.delete(action); setRevision(r => r + 1);
    } catch (e) { setError(e as ApiError); } finally { sending.current = false; setBusy(false); }
  }
  async function saveRow(value: ItemPlanInput) {
    if (!plan || !editing) return;
    await write(`${itemPlanApi}/${plan.id}/items${editing === 'new' ? '' : `/${editing.id}`}`, editing === 'new' ? 'POST' : 'PUT', { ...value, expectedVersion: plan.version });
  }
  function cancelEditing() { if (canLeave()) { setEditing(null); setDirty(false); setError(null); setLatest(null); } }
  const readLatest = () => { if (plan) void api<ItemPlan>(`${itemPlanApi}/${plan.id}`).then(setLatest).catch(e => setError(e)); };
  const comparison = latest && <Alert type="info" message={`Phiên bản mới: ${latest.name} · v${latest.version} · ${latest.itemCount} hạng mục · ${planMoney(latest.totalCostVnd)}`}
    description={<><div className="item-plan-comparison">{latest.items.map(x => <p key={x.id}><strong>{x.serviceName}</strong> · Răng: {x.teeth.join(', ') || '—'} · {x.estimatedMinutes ?? '—'} phút · {planMoney(x.costVnd)}<br />{x.note || '—'}<br />{x.dentistName || '—'}{x.assistantName ? ` / ${x.assistantName}` : ''}</p>)}</div>
      <Button onClick={() => { setPlan(latest); setLatest(null); setError(null); }}>Giữ bản nhập để lưu lại</Button></>} />;
  const heading = <thead><tr>{['Ngày', 'Răng', 'Điều trị/dịch vụ', 'Thời gian dự kiến (phút)', 'Ghi chú', 'Bác sĩ & phụ tá', 'Chi phí', 'Thao tác'].map(x => <th key={x} scope="col">{x}</th>)}</tr></thead>;
  return <main className="item-plans-page">
    <div className="item-plans-heading"><div><Typography.Title level={2}>Kế hoạch điều trị</Typography.Title></div>
      <Space wrap>{canPickBranch && branches.length > 1 && <Select aria-label="Chi nhánh" value={branch} disabled={busy}
        options={branches.map(x => ({ value: x.id, label: x.name }))} onChange={id => { if (canLeave()) { setBranch(id); setPlan(null); setEditing(null); setDirty(false); setPage(1); setData(null); setOptions(null); setError(null); } }} />}</Space></div>
    {listError && <Alert showIcon type="error" message={listError} action={<Button onClick={() => branch ? setRevision(r => r + 1) : window.location.reload()}>Thử lại</Button>} />}
    {!plan && error && <Alert showIcon type="error" message={`${error.code ?? ''}: ${error.message}`} description={error.code === 'MSG111' ? 'Bản nhập vẫn được giữ. Đọc phiên bản mới để đối chiếu trước khi lưu lại.' : undefined}
      action={error.code === 'MSG111' && plan ? <Button disabled={busy} onClick={readLatest}>Xem phiên bản mới</Button> : undefined} />}
    {!plan && !nameDialog && comparison}
    <Card title="Danh sách kế hoạch" extra={<Button type="primary" disabled={!canManage || !branch || busy} onClick={() => { if (canLeave()) { setEditing(null); setName(''); setNameDialog('create'); setDirty(false); setError(null); } }}>Tạo kế hoạch</Button>}>
      <Space.Compact className="item-plan-search"><Input aria-label="Tìm tên kế hoạch" value={search} maxLength={160} allowClear onChange={e => setSearch(e.target.value)} onPressEnter={() => { setKeyword(search.trim()); setPage(1); }} /><Button onClick={() => { setKeyword(search.trim()); setPage(1); }}>Tìm kiếm</Button></Space.Compact>
      {loading && !data ? <Skeleton active /> : <Table className="item-plan-list" rowKey="id" pagination={false} loading={loading} dataSource={data?.items ?? []}
        locale={{ emptyText: listError ? 'Chưa tải được danh sách.' : <Empty description="Chưa có kế hoạch điều trị." /> }} columns={[
          { title: 'Tên kế hoạch', dataIndex: 'name' }, { title: 'Số hạng mục', dataIndex: 'itemCount' },
          { title: 'Tổng chi phí', dataIndex: 'totalCostVnd', render: planMoney }, { title: 'Thao tác', render: (_, row) => <Button disabled={busy} onClick={() => void open(row.id)}>Mở</Button> },
        ]} />}
      {!!data?.total && <Pagination current={page} total={data.total} pageSize={20} showSizeChanger={false} onChange={setPage} />}
    </Card>
    <Modal open={!!plan} centered width={1180} destroyOnClose title={plan?.name} footer={null} styles={{ body: { maxHeight: '75vh', overflowY: 'auto' } }}
      onCancel={() => { if (!busy && canLeave()) { setPlan(null); setEditing(null); setDirty(false); setError(null); setLatest(null); } }}>
      {plan && <>
      {error && !nameDialog && <Alert showIcon type="error" message={`${error.code ?? ''}: ${error.message}`} description={error.code === 'MSG111' ? 'Bản nhập vẫn được giữ. Đọc phiên bản mới để đối chiếu trước khi lưu lại.' : undefined}
        action={error.code === 'MSG111' ? <Button disabled={busy} onClick={readLatest}>Xem phiên bản mới</Button> : undefined} style={{ marginBottom: 12 }} />}
      {!nameDialog && comparison}
      <Space wrap style={{ marginBottom: 12, justifyContent: 'space-between', width: '100%' }}>
        <span>{plan.itemCount} hạng mục · Tổng chi phí: <strong>{planMoney(plan.totalCostVnd)}</strong></span>
        <Space wrap><Button disabled={!canManage || busy || !!editing} onClick={() => { setName(plan.name); setNameDialog('rename'); setDirty(false); setError(null); }}>Sửa tên</Button>
          <Popconfirm title="Bạn có muốn xóa kế hoạch điều trị này không?" okText="Đồng ý" cancelText="Không đồng ý" onConfirm={() => void write(`${itemPlanApi}/${plan.id}`, 'DELETE', { expectedVersion: plan.version }, true)}>
            <Button danger disabled={!canManage || busy || !!editing}>Xóa kế hoạch</Button></Popconfirm></Space>
      </Space>
      <div className="item-plan-table-scroll"><table className="item-plan-table">{heading}<tbody>
        {plan.items.map(row => editing !== 'new' && editing?.id === row.id && options ?
          <ItemPlanRowEditor key={row.id} initial={row} options={options} busy={!canManage || busy} onDirty={() => setDirty(true)} onSave={saveRow} onCancel={cancelEditing} /> :
          <tr key={row.id}><td>{row.date ? row.date.split('-').reverse().join('/') : '—'}</td><td>{row.teeth.join(', ') || '—'}</td><td>{row.serviceName}</td><td>{row.estimatedMinutes ?? '—'}</td><td className="item-plan-note">{row.note || '—'}</td>
            <td>{row.dentistName || '—'}{row.assistantName && <div>Phụ tá: {row.assistantName}</div>}</td><td>{planMoney(row.costVnd)}</td><td><Space wrap>
              <Button disabled={!canManage || busy || !!editing} onClick={() => { setEditing(row); setDirty(false); setError(null); }}>Sửa</Button>
              <Popconfirm title="Bạn có muốn xóa hạng mục này không?" okText="Đồng ý" cancelText="Không đồng ý" onConfirm={() => void write(`${itemPlanApi}/${plan.id}/items/${row.id}`, 'DELETE', { expectedVersion: plan.version })}>
                <Button danger disabled={!canManage || busy || !!editing}>Xóa</Button></Popconfirm></Space></td></tr>)}
        {editing === 'new' && options && <ItemPlanRowEditor key={`new:${plan.id}`} initial={null} options={options} busy={!canManage || busy} onDirty={() => setDirty(true)} onSave={saveRow} onCancel={cancelEditing} />}
        {editing && editing !== 'new' && !plan.items.some(x => x.id === editing.id) && options && <ItemPlanRowEditor key={editing.id} initial={editing} options={options} busy={!canManage || busy} onDirty={() => setDirty(true)} onSave={saveRow} onCancel={cancelEditing} />}
        {!plan.items.length && editing !== 'new' && <tr><td colSpan={8}><Empty description="Chưa có hạng mục. Bấm Thêm hạng mục để nhập." /></td></tr>}
      </tbody></table></div>
      <Button type="primary" disabled={!canManage || busy || !!editing || !options || plan.itemCount >= 100} onClick={() => { setEditing('new'); setDirty(false); setError(null); }}>+ Thêm hạng mục</Button>
      </>}
    </Modal>
    <Modal title={nameDialog === 'create' ? 'Tạo kế hoạch điều trị' : 'Sửa tên kế hoạch'} open={!!nameDialog} onCancel={() => { if (!busy && canLeave()) { setNameDialog(null); setDirty(false); } }}
      footer={<Space><Button disabled={busy} onClick={() => { if (canLeave()) { setNameDialog(null); setDirty(false); } }}>Hủy</Button><Button type="primary" loading={busy} disabled={!canManage || !name.trim() || name.trim().length > 160} onClick={() => {
        if (nameDialog === 'create') void write(itemPlanApi, 'POST', { branchId: branch, name: name.trim() });
        else if (plan) void write(`${itemPlanApi}/${plan.id}`, 'PUT', { name: name.trim(), expectedVersion: plan.version });
      }}>Lưu</Button></Space>}>
      <label htmlFor="item-plan-name">Tên kế hoạch</label><Input id="item-plan-name" value={name} maxLength={160} disabled={busy} onChange={e => { setName(e.target.value); setDirty(true); }} />
      {error && <Alert type="error" message={error.message} action={error.code === 'MSG111' && plan ? <Button onClick={readLatest}>Xem phiên bản mới</Button> : undefined} />}
      {nameDialog && comparison}
    </Modal>
  </main>;
}
