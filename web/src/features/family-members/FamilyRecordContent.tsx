import { Alert, Button, Collapse, Descriptions, Form, Input, Popconfirm, Space, Spin, Tabs, Tag } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { ApiError } from '../../services/api';
import { familyDate, useFamilyApi } from './types';
import { accessLabels, recordDateTime, scopeLabels, type FamilyAccess, type FamilyAccessOverview, type RecordHeader } from './recordTypes';
import FamilyRecordSection from './FamilyRecordSection';

export default function FamilyRecordContent({ member }: { member: { id: string; fullName: string } }) {
  const request = useFamilyApi();
  const [overview, setOverview] = useState<FamilyAccessOverview | null>(null), [header, setHeader] = useState<RecordHeader | null>(null);
  const [error, setError] = useState(''), [busy, setBusy] = useState(true), [saving, setSaving] = useState(false), [revision, setRevision] = useState(0);
  const [patientCode, setPatientCode] = useState(''), [matchingInformation, setMatchingInformation] = useState('');
  const pending = useRef<{ text: string; key: string } | null>(null), sending = useRef(false);
  const base = `/${member.id}`;
  useEffect(() => {
    if (!header) return;
    const timer = window.setTimeout(() => { setHeader(null); setRevision(v => v + 1); }, Math.min(2147483647, Math.max(0, Date.parse(header.expiresAt) - Date.now())));
    return () => window.clearTimeout(timer);
  }, [header?.expiresAt]);
  useEffect(() => {
    const refresh = () => setRevision(v => v + 1);
    window.addEventListener('focus', refresh); const timer = window.setInterval(refresh, 15000);
    return () => { window.removeEventListener('focus', refresh); window.clearInterval(timer); };
  }, []);
  useEffect(() => {
    let active = true; const controller = new AbortController(); setError('');
    void request<FamilyAccessOverview>(`${base}/record-access`, { signal: controller.signal }).then(async value => {
      if (!active) return; setOverview(value);
      if (value.current?.status !== 'Verified') { setHeader(null); return; }
      const record = await request<RecordHeader>(`${base}/record`, { signal: controller.signal });
      if (active) setHeader(record);
    }).catch(failure => { if (active) { setHeader(null); setError((failure as Error).message); } }).finally(() => { if (active) setBusy(false); });
    return () => { active = false; controller.abort(); };
  }, [base, request, revision]);
  const denied = () => { setHeader(null); setRevision(v => v + 1); };
  async function mutate(action: 'submit' | 'withdraw', current?: FamilyAccess) {
    if (sending.current) return; sending.current = true; setSaving(true); setError('');
    const payload = action === 'submit' ? { patientCode: patientCode.trim() || null, matchingInformation: matchingInformation.trim() || null } : { expectedVersion: current!.version };
    const text = JSON.stringify({ action, id: current?.id, payload });
    if (pending.current?.text !== text) pending.current = { text, key: crypto.randomUUID() };
    try {
      await request(`${base}/record-access/requests${action === 'withdraw' ? `/${current!.id}/withdraw` : ''}`, { method: 'POST', body: JSON.stringify({ ...payload, requestKey: pending.current.key }) });
      pending.current = null; setHeader(null); setRevision(v => v + 1);
    } catch (failure) { setError((failure as Error).message); if (failure instanceof ApiError && [403, 404].includes(failure.status)) denied(); }
    finally { sending.current = false; setSaving(false); }
  }
  const current = overview?.current;
  const tabs = [{ key: 'appointments', label: 'Lịch đặt hộ', children: <FamilyRecordSection memberId={member.id} section="appointments" denied={denied} /> },
    ...(header ? header.scopes.map(scope => ({ key: scope.toLowerCase(), label: scopeLabels[scope], children: <FamilyRecordSection memberId={member.id} section={scope.toLowerCase()} denied={denied} /> })) : [])];
  return <div className="family-record-content">
    {!header && <Alert type="info" showIcon message="Người thân không cần tài khoản DCMS" description="Hồ sơ được xem mặc định sau khi phòng khám tiếp nhận đúng người thân từ lịch bạn đặt hộ. Bạn không cần gửi yêu cầu hoặc chờ duyệt riêng cho lịch này." />}
    {error && <Alert type="error" showIcon message={error} action={<Button onClick={() => setRevision(v => v + 1)}>Thử lại</Button>} />}
    {busy ? <Spin /> : overview && <>
      <div className="family-access-status"><Tag color={current?.status === 'Verified' ? 'green' : current?.status === 'Pending' ? 'gold' : undefined}>{current ? accessLabels[current.status] : 'Chưa có hồ sơ liên kết'}</Tag><Button size="small" onClick={() => setRevision(v => v + 1)}>Tải lại trạng thái</Button></div>
      {current?.publicNote && <Alert type="info" message={current.publicNote} />}
      {current?.status === 'Pending' && <p>Yêu cầu đã gửi. Khi người thân đến khám, vui lòng phối hợp với phòng khám để đối chiếu danh tính và xác nhận phạm vi chia sẻ.</p>}
      {!current && <p>Hồ sơ khám sẽ tự hiển thị sau khi người thân được tiếp nhận. Nếu đã có hồ sơ từ trước, bạn có thể yêu cầu phòng khám liên kết bên dưới.</p>}
      {overview.canRequest && <Collapse items={[{ key: 'existing-record', label: 'Liên kết hồ sơ đã có tại phòng khám', children: <Form layout="vertical" onFinish={() => void mutate('submit')}>
        <Form.Item label="Mã hồ sơ người thân (nếu biết)" htmlFor="family-access-patient-code"><Input id="family-access-patient-code" maxLength={40} value={patientCode} onChange={e => setPatientCode(e.target.value)} /></Form.Item>
        <Form.Item label="Thông tin giúp phòng khám đối chiếu" htmlFor="family-access-matching"><Input.TextArea id="family-access-matching" maxLength={1000} showCount value={matchingInformation} onChange={e => setMatchingInformation(e.target.value)} /></Form.Item>
        <Button type="primary" htmlType="submit" loading={saving}>Yêu cầu quyền xem hồ sơ</Button>
      </Form> }]} />}
      {current && ['Pending', 'Verified'].includes(current.status) && <Popconfirm title={current.status === 'Pending' ? 'Rút yêu cầu xác minh?' : 'Chấm dứt quyền xem hồ sơ?'} okText="Xác nhận" cancelText="Hủy" onConfirm={() => mutate('withdraw', current)}>
        <Button danger loading={saving}>{current.status === 'Pending' ? 'Rút yêu cầu' : 'Chấm dứt quyền truy cập'}</Button>
      </Popconfirm>}
      {header && <Descriptions bordered size="small" column={1} items={[
        { label: 'Bệnh nhân', children: `${header.fullName} · ${header.patientCode}` }, { label: 'Ngày sinh', children: familyDate(header.birthDate) },
        { label: 'Chi nhánh', children: header.branchName }, { label: 'Có hiệu lực đến', children: recordDateTime(header.expiresAt) },
      ]} />}
      <Tabs key={header ? `${current?.id}:${current?.version}` : 'without-access'} defaultActiveKey={header ? header.scopes[0]?.toLowerCase() : 'appointments'} destroyInactiveTabPane items={tabs} />
      {!!overview.history.length && <Collapse items={[{ key: 'history', label: 'Lịch sử yêu cầu', children: <Space direction="vertical">{overview.history.map(item => <div key={item.id}><Tag>{accessLabels[item.status]}</Tag>{recordDateTime(item.requestedAt)}{item.publicNote ? ` · ${item.publicNote}` : ''}</div>)}</Space> }]} />}
    </>}
  </div>;
}
