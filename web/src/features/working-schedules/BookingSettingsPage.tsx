import { Alert, Button, Card, Checkbox, Empty, Input, InputNumber, Select, Space, Spin, Table, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api } from '../../services/api';

type Service = { id: number; name: string; durationMinutes: number; active: boolean; publicBookable: boolean; isInitialExamination?: boolean };
type Settings = { branchId: number; slotIntervalMinutes: number; version: number; services: Service[] };
export default function BookingSettingsPage() {
  const [branches, setBranches] = useState<{ id: number; name: string }[] | null>(null);
  const [branchId, setBranchId] = useState<number>();
  const [settings, setSettings] = useState<Settings | null>(null);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [revision, setRevision] = useState(0);
  const retry = useRef<{ fingerprint: string; key: string } | null>(null);
  useEffect(() => {
    let active = true;
    api<{ id: number; name: string }[]>('/api/booking-settings/options')
      .then(value => { if (active) { setBranches(value); setBranchId(current => value.some(b => b.id === current) ? current : value[0]?.id); } })
      .catch(failure => { if (active) setError((failure as Error).message); });
    return () => { active = false; };
  }, [revision]);
  useEffect(() => {
    if (!branchId) return;
    let active = true;
    setLoading(true); setSettings(null); setError(null); setNotice(null); setDirty(false); retry.current = null;
    api<Settings>(`/api/booking-settings/${branchId}`)
      .then(value => { if (active) setSettings(value); })
      .catch(failure => { if (active) setError((failure as Error).message); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [branchId, revision]);
  function update(value: Settings) { setSettings(value); setDirty(true); setNotice(null); }
  const valid = !!settings && Number.isInteger(settings.slotIntervalMinutes) && settings.slotIntervalMinutes >= 1 && settings.slotIntervalMinutes <= 120
    && settings.services.every(s => s.name.trim().length > 0 && s.name.trim().length <= 160 && Number.isInteger(s.durationMinutes) && s.durationMinutes >= 1 && s.durationMinutes <= 480);
  async function save() {
    if (!settings || !valid || saving) return;
    const payload = { expectedVersion: settings.version, slotIntervalMinutes: settings.slotIntervalMinutes, services: settings.services };
    const fingerprint = JSON.stringify(payload);
    if (retry.current?.fingerprint !== fingerprint) retry.current = { fingerprint, key: crypto.randomUUID() };
    setSaving(true); setError(null); setNotice(null);
    try {
      const result = await api<Settings>(`/api/booking-settings/${settings.branchId}`, { method: 'PUT',
        body: JSON.stringify({ ...payload, requestKey: retry.current.key }) });
      setSettings(result); setDirty(false); retry.current = null;
      setNotice('Đã lưu cấu hình. Các lịch hẹn đã đặt giữ nguyên thời lượng.');
    } catch (failure) { setError((failure as Error).message); }
    finally { setSaving(false); }
  }
  return <>
    <Typography.Title level={2}>Cài đặt đặt lịch</Typography.Title>
    <Typography.Paragraph>Khám lần đầu / đợt mới chỉ có dịch vụ Khám. Nhập bước chia giờ và thời lượng Khám tại đây. Các dịch vụ điều trị giữ riêng để dùng theo phác đồ của bệnh nhân.</Typography.Paragraph>
    {error && <Alert className="form-alert" type="error" showIcon message={error}
      action={<Button disabled={saving} onClick={() => setRevision(value => value + 1)}>{dirty ? 'Bỏ thay đổi và tải lại' : 'Thử lại'}</Button>} />}
    {notice && <Alert className="form-alert" type="success" showIcon message={notice} />}
    {!branches ? !error && <Spin /> : branches.length === 0 ? <Empty description="Bạn chưa có chi nhánh được phép cài đặt." /> : <Card>
      <Space wrap style={{ marginBottom: 24 }}>
        <Select aria-label="Chi nhánh cài đặt" style={{ width: 240 }} value={branchId} disabled={dirty || saving}
          options={branches.map(b => ({ value: b.id, label: b.name }))} onChange={setBranchId} />
        {dirty && <Button disabled={saving} onClick={() => setRevision(value => value + 1)}>Bỏ thay đổi</Button>}
      </Space>
      {loading ? <Spin /> : settings && <>
        <Space wrap style={{ marginBottom: 24 }}>
          <label htmlFor="booking-slot-interval">Bước chia slot (phút)</label>
          <InputNumber id="booking-slot-interval" min={1} max={120} precision={0} disabled={saving} value={settings.slotIntervalMinutes}
            onChange={value => update({ ...settings, slotIntervalMinutes: value ?? 0 })} />
          <Typography.Text type="secondary">1–120 phút; neo từ đầu mỗi ca làm việc.</Typography.Text>
        </Space>
        <Typography.Title level={4}>Dịch vụ khám / điều trị</Typography.Title>
        <Table<Service> rowKey={(_, index) => String(index)} pagination={false} dataSource={settings.services} scroll={{ x: 650 }}
          columns={[
            { title: 'Tên dịch vụ', render: (_, s, index) => <Input aria-label={`Tên dịch vụ ${index + 1}`} maxLength={160} disabled={saving || s.isInitialExamination} value={s.isInitialExamination ? 'Khám' : s.name}
              onChange={event => update({ ...settings, services: settings.services.map((v, i) => i === index ? { ...v, name: event.target.value } : v) })} /> },
            { title: 'Thời lượng (phút)', width: 190, render: (_, s, index) => <InputNumber aria-label={`Thời lượng dịch vụ ${index + 1}`} min={1} max={480} precision={0} disabled={saving} value={s.durationMinutes}
              onChange={value => update({ ...settings, services: settings.services.map((v, i) => i === index ? { ...v, durationMinutes: value ?? 0 } : v) })} /> },
            { title: 'Hoạt động', width: 120, render: (_, s, index) => <Checkbox aria-label={`Dịch vụ hoạt động ${index + 1}`} checked={s.active} disabled={saving}
              onChange={event => update({ ...settings, services: settings.services.map((v, i) => i === index ? { ...v, active: event.target.checked } : v) })} /> },
            { title: 'Dùng cho', width: 170, render: (_, s) => s.isInitialExamination ? 'Khám lần đầu / đợt mới' : 'Điều trị theo phác đồ' },
          ]} />
        <Space wrap style={{ marginTop: 24 }}>
          <Button disabled={saving || settings.services.length >= 500} onClick={() => update({ ...settings,
            services: [...settings.services, { id: 0, name: '', durationMinutes: 30, active: true, publicBookable: false }] })}>Thêm dịch vụ</Button>
          <Button type="primary" loading={saving} disabled={!dirty || !valid} onClick={() => void save()}>Lưu cài đặt</Button>
        </Space>
        <Typography.Paragraph type="secondary" style={{ marginTop: 16 }}>Thời lượng dịch vụ: 1–480 phút. Giờ nghỉ, lịch nha sĩ, ghế và các hẹn đã đặt vẫn được kiểm tra khi chọn giờ và xác nhận.</Typography.Paragraph>
      </>}
    </Card>}
  </>;
}
