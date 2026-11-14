import { Alert, Button, Card, Col, DatePicker, Row, Select, Space, Typography } from 'antd';
import dayjs from 'dayjs';
import { useEffect, useState } from 'react';
import { api } from '../../services/api';
import { useCanPickBranch } from '../authentication/useCanPickBranch';

type Statistics = { actualReceivedVnd: number; advanceReceivedVnd: number };
type Branch = { id: number; name: string };
const money = (value: number) => `${value.toLocaleString('vi-VN')} đ`;

export default function FinanceStatisticsPage() {
  const canPickBranch = useCanPickBranch();
  const [from, setFrom] = useState<string>();
  const [to, setTo] = useState<string>();
  const [branchId, setBranchId] = useState<number>();
  const [branches, setBranches] = useState<Branch[]>([]);
  const [data, setData] = useState<Statistics>();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string>();
  const [revision, setRevision] = useState(0);

  useEffect(() => {
    if (!canPickBranch) return;
    api<{ branches: Branch[] }>('/api/appointments/options')
      .then(result => setBranches(result.branches))
      .catch(() => setBranches([]));
  }, [canPickBranch]);

  useEffect(() => {
    let active = true;
    const params = new URLSearchParams();
    if (from) params.set('from', from);
    if (to) params.set('to', to);
    if (branchId) params.set('branchId', String(branchId));
    setLoading(true);
    setError(undefined);
    api<Statistics>(`/api/finance/statistics?${params}`)
      .then(result => { if (active) setData(result); })
      .catch((failure: unknown) => { if (active) setError(failure instanceof Error ? failure.message : 'Không tải được thống kê.'); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [from, to, branchId, revision]);

  return <div className="system-settings-page">
    <Typography.Title level={2}>Thống kê tài chính</Typography.Title>
    <Typography.Paragraph type="secondary">Tiền thực thu chỉ gồm khoản đã xác nhận cho dịch vụ đã làm. Tiền cọc được ghi riêng ở Tiền nhận trước. Hiện thống kê sử dụng các giao dịch lịch sử đã ghi nhận.</Typography.Paragraph>
    <Space wrap style={{ marginBottom: 20 }}>
      <DatePicker aria-label="Từ ngày" placeholder="Từ ngày" format="DD/MM/YYYY" value={from ? dayjs(from) : null} onChange={value => {
        const next = value?.format('YYYY-MM-DD');
        setFrom(next);
        if (next && to && next > to) setTo(undefined);
      }} />
      <DatePicker aria-label="Đến ngày" placeholder="Đến ngày" format="DD/MM/YYYY" value={to ? dayjs(to) : null} minDate={from ? dayjs(from) : undefined} onChange={value => setTo(value?.format('YYYY-MM-DD'))} />
      {canPickBranch && branches.length > 1 && <Select allowClear aria-label="Chi nhánh" placeholder="Tất cả chi nhánh" style={{ minWidth: 200 }} value={branchId} onChange={setBranchId} options={branches.map(branch => ({ value: branch.id, label: branch.name }))} />}
      <Button onClick={() => setRevision(value => value + 1)}>Tải lại</Button>
    </Space>
    {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} />}
    <Row gutter={[16, 16]}>
      <Col xs={24} md={12}><Card loading={loading}><Typography.Text type="secondary">Tiền thực thu</Typography.Text><Typography.Title level={3} style={{ margin: '8px 0 0' }}>{money(data?.actualReceivedVnd ?? 0)}</Typography.Title></Card></Col>
      <Col xs={24} md={12}><Card loading={loading}><Typography.Text type="secondary">Tiền nhận trước</Typography.Text><Typography.Title level={3} style={{ margin: '8px 0 0' }}>{money(data?.advanceReceivedVnd ?? 0)}</Typography.Title></Card></Col>
    </Row>
  </div>;
}
