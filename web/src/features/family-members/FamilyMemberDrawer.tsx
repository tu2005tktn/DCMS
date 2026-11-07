import { Alert, Button, Descriptions, Drawer, Form, Input, Select, Space } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { ApiError } from '../../services/api';
import { clinicToday, familyDate, familyGender, genderLabels, relationships, relationshipLabel, useFamilyApi, validBirthDate, type FamilyMember, type Gender } from './types';
type Values = { fullName: string; relationship: string; birthDate?: string; gender?: Gender; contactPhone?: string };
export default function FamilyMemberDrawer({ open, member, onClose, onSaved }: {
  open: boolean; member: FamilyMember | null; onClose: () => void; onSaved: (member: FamilyMember) => void;
}) {
  const request = useFamilyApi(), [form] = Form.useForm<Values>();
  const [busy, setBusy] = useState(false), [error, setError] = useState(''), [conflict, setConflict] = useState(false);
  const [latest, setLatest] = useState<FamilyMember | null>(null), [version, setVersion] = useState(member?.version);
  const dirty = useRef(false), sending = useRef(false), retry = useRef<{ payload: string; key: string } | null>(null);
  useEffect(() => {
    if (!open) return;
    form.setFieldsValue({ fullName: member?.fullName ?? '', relationship: member?.relationship, birthDate: member?.birthDate ?? '', gender: member?.gender ?? undefined, contactPhone: member?.contactPhone ?? '' });
    setVersion(member?.version); setError(''); setConflict(false); setLatest(null); dirty.current = false; retry.current = null;
  }, [open, member?.id, member?.version, form]);
  function close() { if (!busy && !sending.current && (!dirty.current || window.confirm('Bỏ các thay đổi chưa lưu?'))) onClose(); }
  useEffect(() => {
    if (!open) return;
    const unload = (e: BeforeUnloadEvent) => { if (dirty.current) e.preventDefault(); };
    window.addEventListener('beforeunload', unload); return () => window.removeEventListener('beforeunload', unload);
  }, [open]);
  async function save(values: Values) {
    if (sending.current) return;
    sending.current = true; setBusy(true); setError('');
    const body = { fullName: values.fullName.trim(), relationship: values.relationship, birthDate: values.birthDate || null,
      gender: values.gender || null, contactPhone: values.contactPhone?.trim() || null,
      ...(member ? { expectedVersion: version } : {}) };
    const payload = JSON.stringify(body);
    const key = retry.current?.payload === payload ? retry.current.key : crypto.randomUUID(); retry.current = { payload, key };
    try {
      const saved = await request<FamilyMember>(member ? `/${member.id}` : '', { method: member ? 'PUT' : 'POST', body: JSON.stringify({ ...body, requestKey: key }) });
      dirty.current = false; onSaved(saved);
    } catch (failure) { setError((failure as Error).message); setConflict(failure instanceof ApiError && failure.code === 'MSG111'); }
    finally { sending.current = false; setBusy(false); }
  }
  async function readLatest() {
    if (!member || sending.current) return;
    setBusy(true);
    try { setLatest(await request<FamilyMember>(`/${member.id}`)); } catch (failure) { setError((failure as Error).message); }
    finally { setBusy(false); }
  }
  return <Drawer title={member ? 'Sửa người thân' : 'Thêm người thân'} open={open} onClose={close} width={440}
    className="customer-family-drawer" maskClosable={!busy} keyboard={!busy} destroyOnClose>
    {error && <Alert type="error" showIcon message={error} className="form-alert" />}
    {conflict && <Button onClick={() => void readLatest()} loading={busy}>Xem phiên bản mới</Button>}
    {latest && <div className="customer-family-compare"><Descriptions title="Thông tin mới nhất" column={1} size="small" items={[
      { key: 'name', label: 'Họ tên', children: latest.fullName }, { key: 'relation', label: 'Quan hệ', children: relationshipLabel(latest.relationship) },
      { key: 'birth', label: 'Ngày sinh', children: familyDate(latest.birthDate) },
      { key: 'gender', label: 'Giới tính', children: familyGender(latest.gender) }, { key: 'phone', label: 'Số điện thoại liên hệ', children: latest.contactPhone || 'Chưa cung cấp' },
    ]} /><Space wrap><Button onClick={() => { form.setFieldsValue({ ...latest, birthDate: latest.birthDate ?? '', gender: latest.gender ?? undefined, contactPhone: latest.contactPhone ?? '' }); setVersion(latest.version); setLatest(null); setConflict(false); setError(''); retry.current = null; }}>Dùng thông tin mới</Button>
      <Button onClick={() => { setVersion(latest.version); setLatest(null); setConflict(false); setError(''); retry.current = null; }}>Giữ bản nhập để lưu lại</Button></Space></div>}
    <Form form={form} layout="vertical" onFinish={values => void save(values)} disabled={busy} onValuesChange={() => { dirty.current = true; }}>
      <Form.Item name="fullName" label="Họ tên" rules={[{ required: true, whitespace: true, message: 'Nhập họ tên người thân.' }, { max: 100, message: 'Họ tên tối đa 100 ký tự.' }]}><Input maxLength={100} autoComplete="off" /></Form.Item>
      <Form.Item name="relationship" label="Quan hệ" rules={[{ required: true, message: 'Chọn quan hệ.' }]}><Select options={relationships} placeholder="Chọn quan hệ" /></Form.Item>
      <Form.Item name="birthDate" label="Ngày sinh (không bắt buộc)" rules={[{ validator: async (_, value: string) => { if (!validBirthDate(value ?? '')) throw new Error('Ngày sinh không hợp lệ hoặc sau ngày hiện tại.'); } }]}><Input type="date" max={clinicToday()} /></Form.Item>
      <Form.Item name="gender" label="Giới tính" rules={[{ required: true, message: 'Chọn giới tính.' }]}><Select placeholder="Chọn giới tính" options={Object.entries(genderLabels).map(([value, label]) => ({ value, label }))} /></Form.Item>
      <Form.Item name="contactPhone" label="Số điện thoại liên hệ" rules={[{ required: true, whitespace: true, message: 'Nhập số điện thoại liên hệ.' }, { validator: async (_, value: string) => { if (value?.trim() && !/^0[35789][0-9]{8}$/.test(value.trim())) throw new Error('Nhập số di động Việt Nam gồm 10 chữ số.'); } }]}><Input type="tel" inputMode="tel" maxLength={10} autoComplete="tel" placeholder="Ví dụ: 0912345678" /></Form.Item>
      <Space><Button onClick={close}>Hủy</Button><Button type="primary" htmlType="submit" loading={busy}>Lưu</Button></Space>
    </Form>
  </Drawer>;
}
