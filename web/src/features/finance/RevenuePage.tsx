import { Alert, Button, Card, Col, DatePicker, Empty, Row, Select, Space, Table, Typography } from 'antd';
import dayjs from 'dayjs';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../../services/api';
import './finance.css';
import { useCanPickBranch } from '../authentication/useCanPickBranch';

type RevenueItem = { id: string; source: 'Finance' | 'Treatment'; sourceId?: string; patientId: number; patientName: string;
  branchId: number; branchName: string; receivedAt: string; method: string; amountVnd: number };
type Revenue = { from: string; to: string; totals: { receivedVnd: number; cashVnd: number; transferVnd: number; pendingVnd: number };
  daily: { date: string; cashVnd: number; transferVnd: number }[]; total: number; items: RevenueItem[] };
type Branch = { id: number; name: string };
const money = (n: number) => `${n.toLocaleString('vi-VN')} ₫`;
const today = () => new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());

export default function RevenuePage() {
  const canPickBranch = useCanPickBranch();
  const [branches, setBranches] = useState<Branch[]>([]);
  const [branchId, setBranchId] = useState<number>();
  const [from, setFrom] = useState(() => `${today().slice(0, 7)}-01`);
  const [to, setTo] = useState(today);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [revision, setRevision] = useState(0);
  const [data, setData] = useState<Revenue>();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string>();
  useEffect(() => { void api<{ branches: Branch[] }>('/api/finance/capabilities').then(x => setBranches(x.branches)).catch(e => setError((e as Error).message)); }, []);
  useEffect(() => { setPage(1); }, [from, to, branchId]);
  useEffect(() => {
    let live = true;
    if (from > to) { setError('Ngày bắt đầu phải trước ngày kết thúc.'); return; }
    const params = new URLSearchParams({ from, to, page: String(page), pageSize: String(pageSize) });
    if (branchId) params.set('branchId', String(branchId));
    setLoading(true); setError(undefined);
    void api<Revenue>(`/api/finance/revenue?${params}`).then(x => { if (live) setData(x); })
      .catch(e => { if (live) setError((e as Error).message); })
      .finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, [from, to, branchId, page, pageSize, revision]);
  const max = Math.max(1, ...data?.daily.map(x => x.cashVnd + x.transferVnd) ?? []);
  return <main className="finance-page">
    <Space wrap><Typography.Title level={2}>Doanh thu đã thu</Typography.Title><Button onClick={() => setRevision(x => x + 1)}>Tải lại</Button></Space>
    <Typography.Paragraph type="secondary">Tiền đã xác nhận từ thu ngân và phiếu điều trị. Khoản chuyển khoản đang chờ được hiển thị riêng.</Typography.Paragraph>
    <Space wrap style={{ marginBottom: 20 }}>
      <Select aria-label="Khoảng thời gian" style={{ width: 150 }} defaultValue="month" onChange={v => {
        const d = dayjs(today());
        if (v === 'today') { setFrom(today()); setTo(today()); }
        if (v === 'week') { setFrom(d.subtract((d.day() + 6) % 7, 'day').format('YYYY-MM-DD')); setTo(d.add(6 - ((d.day() + 6) % 7), 'day').format('YYYY-MM-DD')); }
        if (v === 'month') { setFrom(d.startOf('month').format('YYYY-MM-DD')); setTo(d.endOf('month').format('YYYY-MM-DD')); }
      }} options={[{ value: 'today', label: 'Hôm nay' }, { value: 'week', label: 'Tuần này' }, { value: 'month', label: 'Tháng này' }, { value: 'custom', label: 'Tùy chọn' }]} />
      <DatePicker aria-label="Từ ngày" value={dayjs(from)} format="DD/MM/YYYY" onChange={v => v && setFrom(v.format('YYYY-MM-DD'))} />
      <DatePicker aria-label="Đến ngày" value={dayjs(to)} format="DD/MM/YYYY" onChange={v => v && setTo(v.format('YYYY-MM-DD'))} />
      {canPickBranch && branches.length > 1 && <Select aria-label="Chi nhánh doanh thu" allowClear placeholder="Tất cả chi nhánh" style={{ minWidth: 210 }} value={branchId}
        onChange={setBranchId} options={branches.map(x => ({ value: x.id, label: x.name }))} />}
    </Space>
    {error && <Alert type="error" showIcon message={error} action={<Button size="small" onClick={() => setRevision(x => x + 1)}>Thử lại</Button>} />}
    <Row gutter={[12, 12]}>{[
      ['Đã thu', data?.totals.receivedVnd], ['Tiền mặt', data?.totals.cashVnd],
      ['Chuyển khoản', data?.totals.transferVnd], ['Đang chờ', data?.totals.pendingVnd],
    ].map(([label, value]) => <Col xs={12} lg={6} key={String(label)}><Card><Typography.Text type="secondary">{label}</Typography.Text>
      <Typography.Title level={4} style={{ margin: '6px 0 0' }}>{money(Number(value ?? 0))}</Typography.Title></Card></Col>)}</Row>
    <Card title="Tiền đã thu theo ngày" style={{ marginTop: 20 }} loading={loading}>
      {data?.daily.length ? <div style={{ display: 'flex', gap: 12, alignItems: 'end', overflowX: 'auto', minHeight: 165 }}>
        {data.daily.map(x => <div key={x.date} title={`${x.date}: ${money(x.cashVnd + x.transferVnd)}`} style={{ minWidth: 54, textAlign: 'center' }}>
          <div style={{ height: 125, display: 'flex', flexDirection: 'column', justifyContent: 'end' }}>
            <div style={{ height: Math.max(2, x.transferVnd / max * 125), background: '#46a6ff' }} />
            <div style={{ height: Math.max(2, x.cashVnd / max * 125), background: '#36ad74' }} />
          </div><small>{dayjs(x.date).format('DD/MM')}</small></div>)}
      </div> : <Empty description="Chưa có khoản thu trong khoảng thời gian này" />}
      <Typography.Text type="secondary">■ Tiền mặt &nbsp; <span style={{ color: '#46a6ff' }}>■</span> Chuyển khoản</Typography.Text>
    </Card>
    <Card title="Chứng từ đã thu" style={{ marginTop: 20 }}>
      <Table rowKey={x => `${x.source}-${x.id}`} loading={loading} dataSource={data?.items ?? []} scroll={{ x: 850 }}
        pagination={{ current: page, pageSize, total: data?.total ?? 0, showSizeChanger: true, pageSizeOptions: [20, 50, 100], onChange: (p, s) => { setPage(p); setPageSize(s); } }}
        columns={[{ title: 'Ngày nhận', dataIndex: 'receivedAt', render: (x: string) => new Date(x).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) },
          { title: 'Chi nhánh', dataIndex: 'branchName' }, { title: 'Khách hàng', dataIndex: 'patientName', render: (x: string, r) => <Link to={`/patients/${r.patientId}`}>{x}</Link> },
          { title: 'Nguồn', dataIndex: 'source', render: (x: string) => x === 'Treatment' ? 'Điều trị' : 'Thu ngân' },
          { title: 'Phương thức', dataIndex: 'method', render: (x: string) => x === 'Cash' ? 'Tiền mặt' : 'Chuyển khoản' },
          { title: 'Số tiền', dataIndex: 'amountVnd', align: 'right', render: money },
          { title: '', render: (_, r) => r.sourceId && <Link to={r.source === 'Treatment' ? `/patients/${r.patientId}?tab=treatment` : `/visits/${r.sourceId}/payments`}>Xem</Link> }]}
      />
    </Card>
  </main>;
}
