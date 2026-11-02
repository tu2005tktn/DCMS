import { Alert, Button, Empty, Modal, Pagination, Select, Spin, Table } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api, ApiError } from '../../services/api';
import { itemPlanApi, planMoney, type ItemPlan, type ItemPlanPage } from './itemPlanTypes';
export default function ApplyItemPlanModal({ branchId, dossierId, version, onClose, onApplied, onReload }: {
  branchId: number; dossierId: string; version: number; onClose: () => void; onApplied: () => void; onReload: () => Promise<void>;
}) {
  const [list, setList] = useState<ItemPlanPage | null>(null), [source, setSource] = useState<ItemPlan | null>(null);
  const [id, setId] = useState<string>(), [keyword, setKeyword] = useState(''), [page, setPage] = useState(1), [revision, setRevision] = useState(0);
  const [loading, setLoading] = useState(false), [busy, setBusy] = useState(false), [error, setError] = useState<ApiError | null>(null), [stale, setStale] = useState(false);
  const guard = useRef(false), retry = useRef<{ payload: string; key: string } | null>(null);
  useEffect(() => {
    let active = true; const timer = setTimeout(() => {
      setLoading(true);
      void api<ItemPlanPage>(`${itemPlanApi}?${new URLSearchParams({ branchId: String(branchId), keyword, page: String(page), pageSize: '20' })}`)
        .then(x => { if (active) setList(x); }).catch(e => { if (active) setError(e); }).finally(() => { if (active) setLoading(false); });
    }, 250); return () => { active = false; clearTimeout(timer); };
  }, [branchId, keyword, page, revision]);
  useEffect(() => {
    if (!id) return; let active = true; setSource(null); setError(null); setStale(false); setLoading(true);
    void api<ItemPlan>(`${itemPlanApi}/${id}`).then(x => { if (active) setSource(x); }).catch(e => { if (active) setError(e); }).finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [id, revision]);
  async function apply() {
    if (!source || guard.current || stale) return; guard.current = true; setBusy(true); setError(null);
    const body = { itemPlanId: source.id, expectedItemPlanVersion: source.version, expectedVersion: version }, payload = JSON.stringify(body);
    if (retry.current?.payload !== payload) retry.current = { payload, key: crypto.randomUUID() };
    try { await api(`/api/dossiers/${dossierId}/sheet-items/from-item-plan`, { method: 'POST', body: JSON.stringify({ ...body, requestKey: retry.current.key }) }); onApplied(); }
    catch (e) { setError(e as ApiError); if (e instanceof ApiError && (e.code === 'MSG111' || e.status === 404)) setStale(true); }
    finally { guard.current = false; setBusy(false); }
  }
  return <Modal open title="Chọn kế hoạch điều trị" width={1000} onCancel={() => { if (!busy) onClose(); }} keyboard={!busy} maskClosable={!busy}
    footer={<><Button disabled={busy} onClick={onClose}>Hủy</Button><Button type="primary" loading={busy} disabled={!source?.items.length || loading || stale} onClick={() => void apply()}>Áp dụng</Button></>}>
    <p>Các hạng mục sẽ được thêm vào cuối bảng hiện tại. Bác sĩ phụ trách sử dụng bác sĩ của phiếu.</p>
    {error && <Alert type="error" showIcon message={error.message} action={<Button disabled={busy} onClick={() => { void onReload().then(() => { setRevision(x => x + 1); setError(null); }).catch(setError); }}>Tải lại nguồn</Button>} />}
    <Select aria-label="Chọn kế hoạch điều trị" style={{ width: '100%' }} showSearch filterOption={false} placeholder="Tìm và chọn kế hoạch" value={id} disabled={busy} loading={loading}
      onSearch={value => { setKeyword(value); setPage(1); }} onChange={setId} options={(list?.items ?? []).map(x => ({ value: x.id, label: `${x.name} · ${x.itemCount} hạng mục` }))} />
    {!!list?.total && <Pagination current={page} total={list.total} pageSize={20} showSizeChanger={false} onChange={setPage} />}
    {loading && <Spin />}
    {source && <><p><strong>{source.name}</strong> · Tổng chi phí: {planMoney(source.totalCostVnd)}</p><Table rowKey="id" size="small" pagination={false} dataSource={source.items} scroll={{ x: 1000 }}
      locale={{ emptyText: <Empty description="Kế hoạch chưa có hạng mục." /> }} columns={[
        { title: 'Ngày', render: (_, r) => r.date?.split('-').reverse().join('/') || '—' }, { title: 'Răng', render: (_, r) => r.teeth.join(', ') || '—' },
        { title: 'Điều trị/dịch vụ', dataIndex: 'serviceName' }, { title: 'Thời gian dự kiến (phút)', dataIndex: 'estimatedMinutes' }, { title: 'Ghi chú', dataIndex: 'note' },
        { title: 'Bác sĩ & phụ tá', render: (_, r) => <>Bác sĩ của phiếu{r.assistantName && <div>{r.assistantName}</div>}</> }, { title: 'Chi phí', dataIndex: 'costVnd', render: planMoney },
      ]} /></>}
  </Modal>;
}
