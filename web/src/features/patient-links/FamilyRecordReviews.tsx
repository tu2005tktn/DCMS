import { Alert, Button, Checkbox, Descriptions, Drawer, Empty, Form, Input, Select, Space, Spin, Table, Tabs, Tag, Timeline } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api } from '../../services/api';
import { usePermissions } from '../authentication/usePermissions';
import { familyDate, relationshipLabel, clinicToday } from '../family-members/types';
import { accessLabels, scopeLabels, recordDateTime, type FamilyAccess, type FamilyCandidate, type FamilyReview, type RecordPage } from '../family-members/recordTypes';

type QueueRow = { id: string; code: string; accountUserName: string; accountFullName: string; fullName: string; status: string; requestedAt: string };
type Audit = { action: string; actorName: string; reason: string | null; occurredAt: string };
type VerifyValues = { branchId: number; scopes: string[]; expiresOn: string; sharingBasis: string; verificationNote: string; consentConfirmed: boolean };
const dateAfter = (days: number) => new Intl.DateTimeFormat('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date(Date.now() + days * 86400000));
const nameKey = (value: string) => value.trim().normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/gi, 'd').replace(/\s+/g, ' ').toUpperCase();

export default function FamilyRecordReviews() {
  const [status, setStatus] = useState('Pending'), [keyword, setKeyword] = useState(''), [query, setQuery] = useState('');
  const [page, setPage] = useState(1), [revision, setRevision] = useState(0), [data, setData] = useState<RecordPage<QueueRow> | null>(null);
  const [busy, setBusy] = useState(false), [error, setError] = useState(''), [selected, setSelected] = useState<string | null>(null);
  useEffect(() => {
    let active = true; setBusy(true); setError(''); setData(null);
    void api<RecordPage<QueueRow>>(`/api/family-record-reviews?${new URLSearchParams({ status, keyword: query, page: String(page), pageSize: '20' })}`)
      .then(value => { if (active) setData(value); }).catch(failure => { if (active) setError((failure as Error).message); }).finally(() => { if (active) setBusy(false); });
    return () => { active = false; };
  }, [status, query, page, revision]);
  return <section>
    <h2>Quyền xem hồ sơ người thân</h2><p>Đối chiếu người thân với hồ sơ bệnh nhân và ghi nhận căn cứ chia sẻ trước khi cấp quyền.</p>
    <Space wrap style={{ marginBottom: 16 }}>
      <Select aria-label="Trạng thái quyền người thân" value={status} style={{ width: 230 }} options={[{ value: 'All', label: 'Tất cả' }, ...Object.entries(accessLabels).map(([value, label]) => ({ value, label }))]} onChange={value => { setStatus(value); setPage(1); }} />
      <Input.Search aria-label="Tìm yêu cầu người thân" placeholder="Họ tên, tài khoản hoặc mã yêu cầu" maxLength={100} value={keyword} onChange={e => setKeyword(e.target.value)} onSearch={() => { setQuery(keyword.trim()); setPage(1); }} enterButton="Tìm kiếm" />
      <Button onClick={() => setRevision(v => v + 1)}>Tải lại</Button>
    </Space>
    {error && <Alert type="error" showIcon message={error} action={<Button onClick={() => setRevision(v => v + 1)}>Thử lại</Button>} />}
    <Table<QueueRow> rowKey="id" dataSource={data?.items ?? []} loading={busy} scroll={{ x: 800 }} locale={{ emptyText: <Empty description={error ? 'Chưa tải được yêu cầu.' : 'Không có yêu cầu phù hợp.'} /> }}
      pagination={{ current: page, pageSize: 20, total: data?.total ?? 0, showSizeChanger: false, onChange: setPage }}
      columns={[{ title: 'Mã yêu cầu', dataIndex: 'code' }, { title: 'Người yêu cầu', render: (_, row) => <>{row.accountFullName}<br />{row.accountUserName}</> },
        { title: 'Người thân', dataIndex: 'fullName' }, { title: 'Gửi lúc', render: (_, row) => recordDateTime(row.requestedAt) },
        { title: 'Trạng thái', render: (_, row) => <Tag>{accessLabels[row.status]}</Tag> },
        { title: 'Thao tác', render: (_, row) => <Button onClick={() => setSelected(row.id)}>Đối chiếu</Button> }]} />
    <Drawer title="Xác minh quyền xem hồ sơ người thân" open={selected !== null} onClose={() => setSelected(null)} width={900} destroyOnClose>
      {selected && <FamilyReviewContent key={selected} id={selected} onChanged={() => setRevision(v => v + 1)} />}
    </Drawer>
  </section>;
}

function FamilyReviewContent({ id, onChanged }: { id: string; onChanged: () => void }) {
  const can = usePermissions(), canVerify = can('patient-links.verify');
  const [detail, setDetail] = useState<FamilyReview | null>(null), [error, setError] = useState(''), [busy, setBusy] = useState(true), [saving, setSaving] = useState(false), [revision, setRevision] = useState(0);
  const [candidates, setCandidates] = useState<FamilyCandidate[]>([]), [patientId, setPatientId] = useState<number | null>(null), [searching, setSearching] = useState(false);
  const [decisionReason, setDecisionReason] = useState(''), [publicNote, setPublicNote] = useState(''), [audit, setAudit] = useState<Audit[] | null>(null);
  const [form] = Form.useForm<VerifyValues>();
  const pending = useRef<{ text: string; key: string } | null>(null), sending = useRef(false), generation = useRef(0);
  useEffect(() => {
    let active = true; setBusy(true); setError(''); setDetail(null); setCandidates([]); setPatientId(null); setAudit(null); form.resetFields();
    void api<FamilyReview>(`/api/family-record-reviews/${id}`).then(value => { if (active) setDetail(value); })
      .catch(failure => { if (active) setError((failure as Error).message); }).finally(() => { if (active) setBusy(false); });
    return () => { active = false; generation.current++; };
  }, [id, revision, form]);
  async function search(keyword: string) {
    if (!keyword.trim()) { setError('Nhập mã hồ sơ hoặc họ tên để tra cứu.'); return; }
    const current = ++generation.current; setSearching(true); setError(''); setCandidates([]); setPatientId(null); form.setFieldValue('branchId', undefined);
    try { const value = await api<FamilyCandidate[]>(`/api/family-record-reviews/${id}/patients?keyword=${encodeURIComponent(keyword.trim())}`); if (current === generation.current) setCandidates(value); }
    catch (failure) { if (current === generation.current) setError((failure as Error).message); }
    finally { if (current === generation.current) setSearching(false); }
  }
  async function submit(action: string, payload: object) {
    if (sending.current || !detail) return; sending.current = true; setSaving(true); setError('');
    const text = JSON.stringify({ id, action, payload, expectedVersion: detail.access.version });
    if (pending.current?.text !== text) pending.current = { text, key: crypto.randomUUID() };
    try {
      await api<FamilyAccess>(`/api/family-record-reviews/${id}/${action}`, { method: 'POST', body: JSON.stringify({ ...payload, expectedVersion: detail.access.version, requestKey: pending.current.key }) });
      pending.current = null; setDecisionReason(''); setPublicNote(''); setRevision(v => v + 1); onChanged();
    } catch (failure) { setError((failure as Error).message); }
    finally { sending.current = false; setSaving(false); }
  }
  async function loadAudit() {
    try { setAudit(await api<Audit[]>(`/api/family-record-reviews/${id}/audit`)); }
    catch (failure) { setError((failure as Error).message); }
  }
  const patient = candidates.find(x => x.id === patientId);
  const matches = !!(detail && patient && nameKey(patient.fullName) === nameKey(detail.access.fullName)
    && (!detail.access.birthDate || detail.access.birthDate === patient.birthDate)
    && (!detail.access.patientCode || detail.access.patientCode.toUpperCase() === patient.patientCode.toUpperCase()));
  return <div className="family-record-content">
    {error && <Alert type="error" message={error} action={<Button onClick={() => setRevision(v => v + 1)}>Tải lại yêu cầu</Button>} />}
    {busy && <Spin />}
    {detail && <Tabs onChange={key => { if (key === 'audit') void loadAudit(); }} items={[
      { key: 'review', label: 'Đối chiếu và quyết định', children: <div className="family-record-content">
        <Tag color={detail.access.status === 'Verified' ? 'green' : 'gold'}>{accessLabels[detail.access.status]}</Tag>
        <Descriptions bordered column={1} size="small" items={[
          { label: 'Người yêu cầu', children: `${detail.accountFullName} (${detail.accountUserName})` },
          { label: 'Người thân', children: `${detail.access.fullName} · ${relationshipLabel(detail.relationship)}` },
          { label: 'Ngày sinh', children: familyDate(detail.access.birthDate) }, { label: 'Mã hồ sơ khai báo', children: detail.access.patientCode || 'Chưa cung cấp' },
          { label: 'Thông tin đối chiếu', children: detail.access.matchingInformation || '—' },
          ...(detail.patient ? [{ label: 'Hồ sơ đã cấp', children: `${detail.patient.patientCode} · ${detail.patient.fullName}` }, { label: 'Phạm vi', children: detail.access.scopes.map(scope => scopeLabels[scope]).join(', ') }, { label: 'Hết hạn', children: recordDateTime(detail.access.expiresAt) }] : []),
        ]} />
        {detail.verificationNote && <p><strong>Kết quả đối chiếu: </strong>{detail.verificationNote}</p>}
        {detail.decisionReason && <p><strong>Lý do: </strong>{detail.decisionReason}</p>}
        {canVerify && detail.access.status === 'Pending' && <>
          <Input.Search aria-label="Tra cứu hồ sơ người thân" defaultValue={detail.access.patientCode || detail.access.fullName} maxLength={100} loading={searching} enterButton="Tra cứu hồ sơ" onSearch={value => void search(value)} />
          <Table<FamilyCandidate> size="small" rowKey="id" dataSource={candidates} pagination={false} scroll={{ x: 500 }} rowSelection={{ type: 'radio', selectedRowKeys: patientId ? [patientId] : [], onChange: keys => { const value = Number(keys[0]); setPatientId(value); form.setFieldValue('branchId', candidates.find(c => c.id === value)?.branchIds[0]); } }}
            columns={[{ title: 'Mã hồ sơ', dataIndex: 'patientCode' }, { title: 'Họ tên', dataIndex: 'fullName' }, { title: 'Ngày sinh', render: (_, row) => familyDate(row.birthDate) }]} />
          {patient && !matches && <Alert type="warning" message="Họ tên, ngày sinh hoặc mã hồ sơ không khớp thông tin người thân. Không thể cấp quyền cho hồ sơ này." />}
          <Form form={form} layout="vertical" initialValues={{ scopes: [], expiresOn: dateAfter(90), consentConfirmed: false }} onFinish={values => {
            if (!patient || !matches) return;
            const { expiresOn, ...payload } = values;
            void submit('verify', { ...payload, patientId: patient.id, expiresAt: new Date(`${expiresOn}T23:59:59+07:00`).toISOString() });
          }}>
            <Form.Item name="branchId" label="Chi nhánh được chia sẻ" rules={[{ required: true }]}><Select aria-label="Chi nhánh được chia sẻ" options={detail.branches.filter(b => patient?.branchIds.includes(b.id)).map(b => ({ value: b.id, label: b.name }))} /></Form.Item>
            <Form.Item name="scopes" label="Phạm vi được xem" rules={[{ type: 'array', min: 1, required: true, message: 'Chọn ít nhất một phạm vi.' }]}><Checkbox.Group options={Object.entries(scopeLabels).map(([value, label]) => ({ value, label }))} /></Form.Item>
            <Form.Item name="expiresOn" label="Có hiệu lực đến ngày" rules={[{ required: true }]}><Input type="date" min={clinicToday()} max={dateAfter(364)} /></Form.Item>
            <Form.Item name="sharingBasis" label="Căn cứ chia sẻ" rules={[{ required: true }]}><Select options={[{ value: 'PatientConsent', label: 'Sự đồng ý của bệnh nhân' }, { value: 'LegalRepresentative', label: 'Căn cứ đại diện đã được phòng khám kiểm tra' }]} /></Form.Item>
            <Form.Item name="verificationNote" label="Kết quả đối chiếu và bằng chứng đã kiểm tra" rules={[{ required: true, whitespace: true }]}><Input.TextArea maxLength={1000} showCount /></Form.Item>
            <Form.Item name="consentConfirmed" valuePropName="checked" rules={[{ validator: (_, value) => value ? Promise.resolve() : Promise.reject(new Error('Cần xác nhận đã kiểm tra căn cứ chia sẻ.')) }]}><Checkbox>Đã đối chiếu danh tính và kiểm tra sự đồng ý/căn cứ đại diện</Checkbox></Form.Item>
            <Button type="primary" htmlType="submit" disabled={!matches} loading={saving}>Cấp quyền xem hồ sơ</Button>
          </Form>
        </>}
        {canVerify && ['Pending', 'Verified'].includes(detail.access.status) && <div className="family-record-content">
          <label>Lý do {detail.access.status === 'Pending' ? 'từ chối' : 'thu hồi'}<Input.TextArea aria-label="Lý do quyết định quyền người thân" value={decisionReason} maxLength={500} onChange={e => setDecisionReason(e.target.value)} /></label>
          <label>Nội dung gửi người yêu cầu (không bắt buộc)<Input.TextArea aria-label="Nội dung công khai quyền người thân" value={publicNote} maxLength={500} onChange={e => setPublicNote(e.target.value)} /></label>
          <Button danger disabled={!decisionReason.trim()} loading={saving} onClick={() => void submit(detail.access.status === 'Pending' ? 'reject' : 'revoke', { decisionReason: decisionReason.trim(), publicNote: publicNote.trim() || null })}>{detail.access.status === 'Pending' ? 'Từ chối yêu cầu' : 'Thu hồi quyền'}</Button>
        </div>}
      </div> },
      { key: 'audit', label: 'Lịch sử xử lý', children: audit ? <Timeline items={audit.map((item, index) => ({ key: index, children: <>{recordDateTime(item.occurredAt)} · {item.actorName}<br />{item.action}{item.reason ? ` · ${item.reason}` : ''}</> }))} /> : <Spin /> },
    ]} />}
  </div>;
}
