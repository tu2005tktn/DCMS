import { Alert, Button, Card, Empty, Input, Pagination, Popconfirm, Space, Table } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { useCustomerAuth } from '../customer-auth/CustomerAuthContext';
import FamilyMemberDrawer from './FamilyMemberDrawer';
import FamilyMemberRecordsModal from './FamilyMemberRecordsModal';
import { familyDate, familyGender, relationshipLabel, useFamilyApi, type FamilyMember, type FamilyPage } from './types';
import './familyMembers.css';
export default function FamilyMembersPage() {
  const { state } = useCustomerAuth();
  return state.status === 'authenticated' ? <MemberList key={state.user.id} /> : null;
}
function MemberList() {
  const request = useFamilyApi();
  const [recordMember, setRecordMember] = useState<FamilyMember | null>(null);
  const [data, setData] = useState<FamilyPage | null>(null), [search, setSearch] = useState(''), [keyword, setKeyword] = useState('');
  const [page, setPage] = useState(1), [revision, setRevision] = useState(0), [busy, setBusy] = useState(false), [error, setError] = useState('');
  const [drawer, setDrawer] = useState(false), [editing, setEditing] = useState<FamilyMember | null>(null), [deleting, setDeleting] = useState<string | null>(null);
  const deletion = useRef<{ payload: string; key: string } | null>(null), sending = useRef(false);
  useEffect(() => {
    let active = true; const controller = new AbortController(); setBusy(true); setError(''); setData(null);
    void request<FamilyPage>(`?${new URLSearchParams({ keyword, page: String(page), pageSize: '20' })}`, { signal: controller.signal })
      .then(result => { if (active) { if (page > 1 && !result.items.length) setPage(p => p - 1); else setData(result); } })
      .catch(failure => { if (active) setError((failure as Error).message); }).finally(() => { if (active) setBusy(false); });
    return () => { active = false; controller.abort(); };
  }, [keyword, page, revision, request]);
  async function remove(member: FamilyMember) {
    if (sending.current) return; sending.current = true; setDeleting(member.id); setError('');
    const payload = JSON.stringify({ id: member.id, expectedVersion: member.version });
    const key = deletion.current?.payload === payload ? deletion.current.key : crypto.randomUUID(); deletion.current = { payload, key };
    try { await request(`/${member.id}`, { method: 'DELETE', body: JSON.stringify({ expectedVersion: member.version, requestKey: key }) }); deletion.current = null; setRevision(r => r + 1); }
    catch (failure) { setError((failure as Error).message); }
    finally { sending.current = false; setDeleting(null); }
  }
  const actions = (member: FamilyMember) => <Space wrap><Link to={`/customer/book?familyMemberId=${encodeURIComponent(member.id)}`}>Đặt lịch</Link>
    <Button onClick={() => setRecordMember(member)}>Xem hồ sơ</Button>
    <Button onClick={() => { setEditing(member); setDrawer(true); }}>Sửa</Button><Popconfirm title="Bạn có muốn xóa người thân này khỏi danh sách không?" okText="Đồng ý" cancelText="Không đồng ý" onConfirm={() => remove(member)}>
      <Button danger loading={deleting === member.id} disabled={deleting !== null && deleting !== member.id}>Xóa</Button></Popconfirm></Space>;
  return <section className="customer-family-page">
    <div className="customer-family-heading"><div><h1>Người thân của tôi</h1><p>Lưu thông tin để chọn nhanh khi đặt lịch hộ.</p></div><Button type="primary" onClick={() => { setEditing(null); setDrawer(true); }}>Thêm người thân</Button></div>
    <Card className="customer-family-search"><label htmlFor="family-search">Tìm theo họ tên</label><Space.Compact><Input id="family-search" value={search} maxLength={100} allowClear onChange={e => setSearch(e.target.value)} onPressEnter={() => { setKeyword(search.trim()); setPage(1); }} /><Button onClick={() => { setKeyword(search.trim()); setPage(1); }}>Tìm kiếm</Button></Space.Compact></Card>
    {error && <Alert type="error" showIcon message={error} action={<Button onClick={() => setRevision(r => r + 1)}>Thử lại</Button>} />}
    <div className="customer-family-table"><Table<FamilyMember> rowKey="id" loading={busy} dataSource={data?.items ?? []} pagination={false} scroll={{ x: 950 }}
      locale={{ emptyText: error ? 'Chưa tải được danh sách.' : <Empty description="Chưa có người thân phù hợp. Bạn có thể thêm người thân mới." /> }} columns={[
        { title: 'Họ tên', dataIndex: 'fullName' }, { title: 'Quan hệ', dataIndex: 'relationship', render: relationshipLabel },
        { title: 'Ngày sinh', dataIndex: 'birthDate', render: familyDate }, { title: 'Giới tính', dataIndex: 'gender', render: familyGender },
        { title: 'Số điện thoại liên hệ', dataIndex: 'contactPhone', render: value => value || 'Chưa cung cấp' }, { title: 'Thao tác', render: (_, member) => actions(member) },
      ]} /></div>
    <div className="customer-family-mobile" aria-busy={busy}>{busy ? <p role="status">Đang tải người thân…</p> : data?.items.length ? data.items.map(member => <Card key={member.id}><h2>{member.fullName}</h2><p>{relationshipLabel(member.relationship)} · {familyDate(member.birthDate)}</p><p>Giới tính: {familyGender(member.gender)}<br />Liên hệ: {member.contactPhone || 'Chưa cung cấp'}</p>{actions(member)}</Card>) : !error ? <Empty description="Chưa có người thân phù hợp. Bạn có thể thêm người thân mới." /> : null}</div>
    {!!data?.total && <Pagination current={page} total={data.total} pageSize={20} showSizeChanger={false} onChange={setPage} />}
    <FamilyMemberDrawer open={drawer} member={editing} onClose={() => setDrawer(false)} onSaved={() => { setDrawer(false); setRevision(r => r + 1); }} />
    <FamilyMemberRecordsModal member={recordMember} onClose={() => setRecordMember(null)} />
  </section>;
}
