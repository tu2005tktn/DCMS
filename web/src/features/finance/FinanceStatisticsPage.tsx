import { Alert, Button, Card, Col, DatePicker, Input, Modal, Progress, Row, Select, Space, Table, Tag, Typography } from 'antd';
import dayjs from 'dayjs';
import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../../services/api';
import { useCanPickBranch } from '../authentication/useCanPickBranch';

type Statistics = {
  serviceAmountVnd: number;
  actualReceivedVnd: number;
  advanceReceivedVnd: number;
  remainingReceivableVnd: number;
  materialPurchaseVnd: number;
  profitVnd: number;
  profitableDossierCount: number;
  outstandingDossiers: OutstandingDossier[];
  serviceDossiers?: ServiceDossier[];
  actualPayments?: PaymentEntry[];
  advanceEntries?: PaymentEntry[];
  materialPurchases?: MaterialPurchaseEntry[];
  profitEntries?: ProfitEntry[];
};

type OutstandingDossier = {
  dossierId: string; dossierCode: string; patientId: number;
  patientName: string; patientCode: string; startedOn: string; totalVnd: number; paidVnd: number; dueVnd: number;
  version: number; canForfeitDeposit: boolean;
};

type ServiceDossier = {
  dossierId: string; dossierCode: string; patientId: number;
  patientName: string; patientCode: string; startedOn: string; totalVnd: number; paidVnd: number; dueVnd: number;
  state: string;
};

type PaymentEntry = {
  id: string; dossierId: string | null; dossierCode: string; patientId: number;
  patientName: string; patientCode: string; paidAt: string; method: string; note: string;
  amountVnd: number; canOpenDossier: boolean;
};

type MaterialPurchaseEntry = {
  id: string; enteredOn: string; materialName: string; categoryName: string; supplierName: string | null;
  quantity: number; unit: string; unitPriceVnd: number; totalVnd: number; paidVnd: number;
};

type ProfitEntry = {
  id: string; dossierId: string | null; dossierCode: string; patientId: number;
  patientName: string; patientCode: string; date: string; revenueVnd: number; costVnd: number;
  profitVnd: number; canOpenDossier: boolean;
};

type DetailModalKind = 'service' | 'actual' | 'advance' | 'outstanding' | 'materials' | 'profit';
type Branch = { id: number; name: string };
const money = (value: number) => `${value.toLocaleString('vi-VN')} đ`;
const stateLabels: Record<string, { label: string; color: string }> = {
  Open: { label: 'Chưa xong', color: 'gold' },
  Completed: { label: 'Hoàn tất', color: 'green' },
  Stopped: { label: 'Đã ngừng', color: 'default' },
  Cancelled: { label: 'Hủy phiếu', color: 'red' },
};

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
  const [activeModal, setActiveModal] = useState<DetailModalKind>();
  const [forfeitTarget, setForfeitTarget] = useState<OutstandingDossier>();
  const [forfeitReason, setForfeitReason] = useState('');
  const [forfeitBusy, setForfeitBusy] = useState(false);
  const [forfeitError, setForfeitError] = useState<string>();

  async function forfeitDeposit() {
    if (!forfeitTarget || !forfeitReason.trim() || forfeitBusy) return;
    setForfeitBusy(true); setForfeitError(undefined);
    try {
      await api(`/api/dossiers/${forfeitTarget.dossierId}/payments/no-show-forfeit`, {
        method: 'POST', body: JSON.stringify({ requestKey: crypto.randomUUID(),
          expectedVersion: forfeitTarget.version, reason: forfeitReason.trim() }),
      });
      setForfeitTarget(undefined); setForfeitReason(''); setRevision(value => value + 1);
    } catch (failure) { setForfeitError(failure instanceof Error ? failure.message : 'Không thể xóa phiếu.'); }
    finally { setForfeitBusy(false); }
  }

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

  const openLink = (patientId: number, dossierId: string | null, canOpen = true) =>
    canOpen && dossierId && patientId > 0
      ? <Link to={`/patients/${patientId}?tab=treatment&dossierId=${dossierId}`}>Mở phiếu</Link>
      : null;

  const metrics = useMemo(() => {
    const s = data ?? {
      serviceAmountVnd: 0, actualReceivedVnd: 0, advanceReceivedVnd: 0,
      remainingReceivableVnd: 0, materialPurchaseVnd: 0, profitVnd: 0,
      profitableDossierCount: 0, outstandingDossiers: [],
    };
    return [
      { key: 'service' as DetailModalKind, label: 'Số tiền cần thu dịch vụ', short: 'Cần thu DV', value: s.serviceAmountVnd, color: '#1677ff', bg: '#eff8ff', count: `${(s.serviceDossiers ?? []).length} phiếu` },
      { key: 'actual' as DetailModalKind, label: 'Tiền thực thu', short: 'Thực thu', value: s.actualReceivedVnd, color: '#12b76a', bg: '#ecfdf3', count: `${(s.actualPayments ?? []).length} giao dịch` },
      { key: 'advance' as DetailModalKind, label: 'Tiền nhận trước', short: 'Nhận trước', value: s.advanceReceivedVnd, color: '#06aed4', bg: '#ecfdff', count: `${(s.advanceEntries ?? []).length} biến động` },
      { key: 'outstanding' as DetailModalKind, label: 'Tiền còn cần thu', short: 'Còn cần thu', value: s.remainingReceivableVnd, color: '#f79009', bg: '#fffaeb', count: `${(s.outstandingDossiers ?? []).length} phiếu nợ` },
      { key: 'materials' as DetailModalKind, label: 'Tiền dụng cụ vật liệu', short: 'Vật liệu', value: s.materialPurchaseVnd, color: '#7a5af8', bg: '#f4f3ff', count: `${(s.materialPurchases ?? []).length} lần nhập` },
      { key: 'profit' as DetailModalKind, label: 'Lãi', short: 'Lãi ròng', value: s.profitVnd, color: s.profitVnd >= 0 ? '#16a34a' : '#ef4444', bg: s.profitVnd >= 0 ? '#f0fdf4' : '#fef2f2', count: `${s.profitableDossierCount ?? 0} phiếu hoàn tất` },
    ];
  }, [data]);

  const maxAbsValue = useMemo(() => Math.max(...metrics.map(m => Math.abs(m.value)), 1), [metrics]);
  const totalAbsValue = useMemo(() => metrics.reduce((acc, m) => acc + Math.abs(m.value), 0), [metrics]);
  const collectionRate = useMemo(() => {
    const service = data?.serviceAmountVnd ?? 0;
    const paid = Math.max(0, service - (data?.remainingReceivableVnd ?? 0));
    return service > 0 ? Math.min(100, Math.round((paid / service) * 100)) : 0;
  }, [data]);
  const profitMargin = useMemo(() => {
    const actual = data?.actualReceivedVnd ?? 0;
    const profit = data?.profitVnd ?? 0;
    return actual > 0 ? Math.round((profit / actual) * 100) : 0;
  }, [data]);

  const donutSegments = useMemo(() => {
    const r = 74;
    const c = 2 * Math.PI * r;
    let offset = 0;
    return metrics.map(m => {
      const abs = Math.abs(m.value);
      const ratio = totalAbsValue > 0 ? abs / totalAbsValue : 1 / metrics.length;
      const len = ratio * c;
      const seg = { ...m, abs, pct: totalAbsValue > 0 ? Math.round(ratio * 100) : 0, dashArray: `${Math.max(0, len - 2)} ${c}`, dashOffset: -offset };
      offset += len;
      return seg;
    });
  }, [metrics, totalAbsValue]);

  const formatCompact = (v: number) => {
    const abs = Math.abs(v);
    const sign = v < 0 ? '-' : '';
    if (abs >= 1_000_000_000) return `${sign}${(abs / 1_000_000_000).toFixed(1)} tỷ`;
    if (abs >= 1_000_000) return `${sign}${(abs / 1_000_000).toFixed(1)} tr`;
    if (abs >= 1_000) return `${sign}${Math.round(abs / 1_000)}k`;
    return `${v} đ`;
  };

  return <div className="system-settings-page">
    <Typography.Title level={2}>Thống kê tài chính</Typography.Title>
    <Typography.Paragraph type="secondary">Lọc phiếu điều trị theo ngày mở phiếu. Tiền dụng cụ vật liệu là tổng giá trị nhập hàng trong khoảng ngày được chọn của toàn phòng khám, vì dữ liệu nhập hàng chưa tách theo chi nhánh. Lãi chỉ ghi nhận khi phiếu đã phân bổ đủ tiền; tổng tiền nhập hàng được trừ một lần.</Typography.Paragraph>
    <Space wrap style={{ marginBottom: 20 }}>
      <DatePicker aria-label="Từ ngày mở phiếu" placeholder="Từ ngày mở phiếu" format="DD/MM/YYYY" value={from ? dayjs(from) : null} onChange={value => {
        const next = value?.format('YYYY-MM-DD');
        setFrom(next);
        if (next && to && next > to) setTo(undefined);
      }} />
      <DatePicker aria-label="Đến ngày mở phiếu" placeholder="Đến ngày mở phiếu" format="DD/MM/YYYY" value={to ? dayjs(to) : null} minDate={from ? dayjs(from) : undefined} onChange={value => setTo(value?.format('YYYY-MM-DD'))} />
      {canPickBranch && branches.length > 1 && <Select allowClear aria-label="Chi nhánh" placeholder="Tất cả chi nhánh" style={{ minWidth: 200 }} value={branchId} onChange={setBranchId} options={branches.map(branch => ({ value: branch.id, label: branch.name }))} />}
      <Button onClick={() => setRevision(value => value + 1)}>Tải lại</Button>
    </Space>
    {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} />}
    <Row gutter={[16, 16]}>
      {metrics.map(item => (
        <Col xs={24} md={8} key={item.key}>
          <Card
            loading={loading}
            hoverable
            onClick={() => setActiveModal(item.key)}
            style={{ cursor: 'pointer', borderTop: `4px solid ${item.color}` }}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <Typography.Text type="secondary">{item.label}</Typography.Text>
              <Tag style={{ margin: 0, borderRadius: 12, background: item.bg, color: item.color, borderColor: 'transparent', fontWeight: 600 }}>
                {item.count}
              </Tag>
            </div>
            <Typography.Title level={3} style={{ margin: '8px 0 6px', color: item.key === 'profit' && item.value < 0 ? '#ef4444' : undefined }}>
              {money(item.value)}
            </Typography.Title>
            <div style={{ height: 6, background: '#f2f4f7', borderRadius: 999, overflow: 'hidden' }}>
              <div style={{
                width: `${Math.max(4, Math.round((Math.abs(item.value) / maxAbsValue) * 100))}%`,
                height: '100%',
                background: item.color,
                borderRadius: 999,
                transition: 'width 0.35s ease',
              }} />
            </div>
          </Card>
        </Col>
      ))}
    </Row>

    <Row gutter={[16, 16]} style={{ marginTop: 16 }}>
      <Col xs={24} lg={15}>
        <Card
          loading={loading}
          title="Biểu đồ so sánh 6 chỉ số tài chính"
          extra={<Typography.Text type="secondary" style={{ fontSize: 12 }}>Nhấn vào từng cột để xem danh sách chi tiết</Typography.Text>}
        >
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(6, minmax(0, 1fr))', gap: 14, alignItems: 'end', height: 240, padding: '24px 8px 8px', borderBottom: '1px solid #eaecf0' }}>
            {metrics.map(item => {
              const heightPct = Math.max(8, Math.round((Math.abs(item.value) / maxAbsValue) * 100));
              return (
                <div
                  key={item.key}
                  onClick={() => setActiveModal(item.key)}
                  style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', height: '100%', justifyContent: 'flex-end', cursor: 'pointer' }}
                  title={`${item.label}: ${money(item.value)}`}
                >
                  <Typography.Text strong style={{ fontSize: 12, color: item.color, marginBottom: 6, whiteSpace: 'nowrap' }}>
                    {formatCompact(item.value)}
                  </Typography.Text>
                  <div
                    style={{
                      width: '100%',
                      maxWidth: 58,
                      height: `${heightPct}%`,
                      minHeight: 14,
                      background: `linear-gradient(180deg, ${item.color} 0%, ${item.color}cc 100%)`,
                      borderRadius: '8px 8px 2px 2px',
                      boxShadow: `0 6px 14px ${item.color}26`,
                      transition: 'height 0.35s ease, transform 0.2s ease',
                    }}
                  />
                </div>
              );
            })}
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(6, minmax(0, 1fr))', gap: 14, padding: '10px 8px 0' }}>
            {metrics.map(item => (
              <div
                key={item.key}
                onClick={() => setActiveModal(item.key)}
                style={{ textAlign: 'center', cursor: 'pointer' }}
              >
                <div style={{ display: 'inline-flex', alignItems: 'center', gap: 5, justifyContent: 'center' }}>
                  <span style={{ width: 8, height: 8, borderRadius: '50%', background: item.color, display: 'inline-block' }} />
                  <Typography.Text style={{ fontSize: 12, fontWeight: 600 }}>{item.short}</Typography.Text>
                </div>
                <div style={{ fontSize: 11, color: '#667085', marginTop: 2 }}>{money(item.value)}</div>
              </div>
            ))}
          </div>
        </Card>
      </Col>

      <Col xs={24} lg={9}>
        <Card loading={loading} title="Tỷ trọng dòng tiền & Hiệu suất thu">
          <div style={{ display: 'flex', alignItems: 'center', gap: 20, flexWrap: 'wrap', justifyContent: 'center' }}>
            <div style={{ position: 'relative', width: 184, height: 184, flex: '0 0 auto' }}>
              <svg width="184" height="184" viewBox="0 0 184 184" style={{ transform: 'rotate(-90deg)' }}>
                <circle cx="92" cy="92" r="74" fill="none" stroke="#f2f4f7" strokeWidth="20" />
                {donutSegments.map(seg => (
                  <circle
                    key={seg.key}
                    cx="92"
                    cy="92"
                    r="74"
                    fill="none"
                    stroke={seg.color}
                    strokeWidth="20"
                    strokeDasharray={seg.dashArray}
                    strokeDashoffset={seg.dashOffset}
                    style={{ cursor: 'pointer', transition: 'stroke-dasharray 0.35s ease' }}
                    onClick={() => setActiveModal(seg.key)}
                  >
                    <title>{`${seg.label}: ${money(seg.value)} (${seg.pct}%)`}</title>
                  </circle>
                ))}
              </svg>
              <div style={{ position: 'absolute', inset: 0, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', pointerEvents: 'none', textAlign: 'center', padding: 24 }}>
                <Typography.Text type="secondary" style={{ fontSize: 11 }}>Thực thu</Typography.Text>
                <Typography.Text strong style={{ fontSize: 14, color: '#101828' }}>{formatCompact(data?.actualReceivedVnd ?? 0)}</Typography.Text>
                <Typography.Text style={{ fontSize: 11, color: '#12b76a', fontWeight: 600 }}>Đạt {collectionRate}%</Typography.Text>
              </div>
            </div>

            <div style={{ flex: '1 1 180px', display: 'flex', flexDirection: 'column', gap: 6 }}>
              {donutSegments.map(seg => (
                <div
                  key={seg.key}
                  onClick={() => setActiveModal(seg.key)}
                  style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 8, padding: '4px 8px', borderRadius: 6, background: '#f9fafb', cursor: 'pointer' }}
                >
                  <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: 12 }}>
                    <span style={{ width: 9, height: 9, borderRadius: 2, background: seg.color }} />
                    {seg.short}
                  </span>
                  <Typography.Text strong style={{ fontSize: 12 }}>{seg.pct}%</Typography.Text>
                </div>
              ))}
            </div>
          </div>

          <div style={{ marginTop: 18, paddingTop: 14, borderTop: '1px solid #eaecf0', display: 'grid', gap: 10 }}>
            <div>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, marginBottom: 4 }}>
                <Typography.Text type="secondary">Tiến độ thu dịch vụ (Đã thu / Cần thu)</Typography.Text>
                <Typography.Text strong>{collectionRate}%</Typography.Text>
              </div>
              <Progress percent={collectionRate} showInfo={false} strokeColor="#12b76a" trailColor="#f2f4f7" size="small" />
            </div>
            <div>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, marginBottom: 4 }}>
                <Typography.Text type="secondary">Tỷ suất lợi nhuận trên thực thu</Typography.Text>
                <Typography.Text strong style={{ color: profitMargin >= 0 ? '#16a34a' : '#ef4444' }}>{profitMargin}%</Typography.Text>
              </div>
              <Progress percent={Math.min(100, Math.max(0, Math.abs(profitMargin)))} showInfo={false} strokeColor={profitMargin >= 0 ? '#16a34a' : '#ef4444'} trailColor="#f2f4f7" size="small" />
            </div>
          </div>
        </Card>
      </Col>
    </Row>

    <Modal title="Chi tiết số tiền cần thu dịch vụ" open={activeModal === 'service'} onCancel={() => setActiveModal(undefined)} footer={null} width={960}>
      <Table rowKey="dossierId" dataSource={data?.serviceDossiers ?? []} pagination={{ pageSize: 20 }} scroll={{ x: 760 }}
        columns={[
          { title: 'Phiếu', dataIndex: 'dossierCode' },
          { title: 'Bệnh nhân', key: 'patient', render: (_, row: ServiceDossier) => <>{row.patientName}<br /><Typography.Text type="secondary">{row.patientCode}</Typography.Text></> },
          { title: 'Ngày lập', dataIndex: 'startedOn', render: (value: string) => dayjs(value).format('DD/MM/YYYY') },
          { title: 'Trạng thái', dataIndex: 'state', render: (value: string) => <Tag color={stateLabels[value]?.color ?? 'default'}>{stateLabels[value]?.label ?? value}</Tag> },
          { title: 'Tổng cần thu', dataIndex: 'totalVnd', align: 'right' as const, render: money },
          { title: 'Đã trả', dataIndex: 'paidVnd', align: 'right' as const, render: money },
          { title: 'Còn thu', dataIndex: 'dueVnd', align: 'right' as const, render: money },
          { title: '', key: 'open', render: (_, row: ServiceDossier) => openLink(row.patientId, row.dossierId) },
        ]} />
    </Modal>

    <Modal title="Danh sách các lần thực thu" open={activeModal === 'actual'} onCancel={() => setActiveModal(undefined)} footer={null} width={960}>
      <Table rowKey="id" dataSource={data?.actualPayments ?? []} pagination={{ pageSize: 20 }} scroll={{ x: 780 }}
        columns={[
          { title: 'Thời gian thu', dataIndex: 'paidAt', render: (value: string) => dayjs(value).format('DD/MM/YYYY HH:mm') },
          { title: 'Phiếu', dataIndex: 'dossierCode' },
          { title: 'Bệnh nhân', key: 'patient', render: (_, row: PaymentEntry) => <>{row.patientName}<br /><Typography.Text type="secondary">{row.patientCode}</Typography.Text></> },
          { title: 'Hình thức', dataIndex: 'method', render: (value: string) => <Tag color={value.includes('bỏ cọc') ? 'volcano' : value.includes('cọc') ? 'blue' : 'green'}>{value}</Tag> },
          { title: 'Nội dung', dataIndex: 'note' },
          { title: 'Số tiền', dataIndex: 'amountVnd', align: 'right' as const, render: money },
          { title: '', key: 'open', render: (_, row: PaymentEntry) => openLink(row.patientId, row.dossierId, row.canOpenDossier) },
        ]} />
    </Modal>

    <Modal title="Danh sách các lần nhận trước & phân bổ cọc" open={activeModal === 'advance'} onCancel={() => setActiveModal(undefined)} footer={null} width={960}>
      <Table rowKey="id" dataSource={data?.advanceEntries ?? []} pagination={{ pageSize: 20 }} scroll={{ x: 780 }}
        columns={[
          { title: 'Thời gian', dataIndex: 'paidAt', render: (value: string) => dayjs(value).format('DD/MM/YYYY HH:mm') },
          { title: 'Phiếu', dataIndex: 'dossierCode' },
          { title: 'Bệnh nhân', key: 'patient', render: (_, row: PaymentEntry) => <>{row.patientName}<br /><Typography.Text type="secondary">{row.patientCode}</Typography.Text></> },
          { title: 'Loại giao dịch', dataIndex: 'method', render: (value: string, row: PaymentEntry) => <Tag color={row.amountVnd >= 0 ? 'blue' : 'orange'}>{value}</Tag> },
          { title: 'Nội dung', dataIndex: 'note' },
          { title: 'Biến động cọc', dataIndex: 'amountVnd', align: 'right' as const, render: (value: number) => <Typography.Text type={value >= 0 ? 'success' : 'warning'}>{value > 0 ? `+${money(value)}` : money(value)}</Typography.Text> },
          { title: '', key: 'open', render: (_, row: PaymentEntry) => openLink(row.patientId, row.dossierId, row.canOpenDossier) },
        ]} />
    </Modal>

    <Modal title="Phiếu điều trị còn cần thu" open={activeModal === 'outstanding'} onCancel={() => setActiveModal(undefined)} footer={null} width={900}>
      <Table rowKey="dossierId" dataSource={data?.outstandingDossiers ?? []} pagination={{ pageSize: 20 }} scroll={{ x: 720 }}
        columns={[
          { title: 'Phiếu', dataIndex: 'dossierCode' },
          { title: 'Bệnh nhân', key: 'patient', render: (_, row: OutstandingDossier) => <>{row.patientName}<br /><Typography.Text type="secondary">{row.patientCode}</Typography.Text></> },
          { title: 'Ngày lập', dataIndex: 'startedOn', render: (value: string) => dayjs(value).format('DD/MM/YYYY') },
          { title: 'Tổng phí', dataIndex: 'totalVnd', align: 'right' as const, render: money },
          { title: 'Đã trả', dataIndex: 'paidVnd', align: 'right' as const, render: money },
          { title: 'Còn thu', dataIndex: 'dueVnd', align: 'right' as const, render: money },
          { title: '', key: 'open', render: (_, row: OutstandingDossier) => openLink(row.patientId, row.dossierId) },
          { title: '', key: 'forfeit', render: (_, row: OutstandingDossier) => row.canForfeitDeposit &&
            <Button danger size="small" onClick={() => { setForfeitTarget(row); setForfeitError(undefined); }}>Khách không đến, bỏ cọc</Button> },
        ]} />
    </Modal>

    <Modal title="Danh sách các lần nhập dụng cụ vật liệu" open={activeModal === 'materials'} onCancel={() => setActiveModal(undefined)} footer={null} width={920}>
      <Table rowKey="id" dataSource={data?.materialPurchases ?? []} pagination={{ pageSize: 20 }} scroll={{ x: 760 }}
        columns={[
          { title: 'Ngày nhập', dataIndex: 'enteredOn', render: (value: string) => dayjs(value).format('DD/MM/YYYY') },
          { title: 'Vật liệu / Dụng cụ', dataIndex: 'materialName' },
          { title: 'Nhóm', dataIndex: 'categoryName' },
          { title: 'Nhà cung cấp', dataIndex: 'supplierName', render: (value: string | null) => value || '—' },
          { title: 'Số lượng', key: 'quantity', align: 'right' as const, render: (_, row: MaterialPurchaseEntry) => `${row.quantity.toLocaleString('vi-VN')} ${row.unit}` },
          { title: 'Đơn giá', dataIndex: 'unitPriceVnd', align: 'right' as const, render: money },
          { title: 'Thành tiền', dataIndex: 'totalVnd', align: 'right' as const, render: money },
          { title: 'Đã trả NCC', dataIndex: 'paidVnd', align: 'right' as const, render: money },
        ]} />
    </Modal>

    <Modal title="Chi tiết các khoản tính lãi" open={activeModal === 'profit'} onCancel={() => setActiveModal(undefined)} footer={null} width={920}>
      <Table rowKey="id" dataSource={data?.profitEntries ?? []} pagination={{ pageSize: 20 }} scroll={{ x: 760 }}
        columns={[
          { title: 'Ngày', dataIndex: 'date', render: (value: string) => dayjs(value).format('DD/MM/YYYY') },
          { title: 'Phiếu / Khoản mục', dataIndex: 'dossierCode' },
          { title: 'Bệnh nhân / Phạm vi', key: 'patient', render: (_, row: ProfitEntry) => <>{row.patientName}{row.patientCode && <><br /><Typography.Text type="secondary">{row.patientCode}</Typography.Text></>}</> },
          { title: 'Thu dịch vụ', dataIndex: 'revenueVnd', align: 'right' as const, render: money },
          { title: 'Chi phí (Labo / Vật liệu)', dataIndex: 'costVnd', align: 'right' as const, render: money },
          { title: 'Lãi ròng', dataIndex: 'profitVnd', align: 'right' as const, render: (value: number) => <Typography.Text type={value >= 0 ? 'success' : 'danger'}>{money(value)}</Typography.Text> },
          { title: '', key: 'open', render: (_, row: ProfitEntry) => openLink(row.patientId, row.dossierId, row.canOpenDossier) },
        ]} />
    </Modal>

    <Modal title="Xóa phiếu do khách không đến" open={!!forfeitTarget} okText="Ghi nhận bỏ cọc và xóa phiếu"
      okButtonProps={{ danger: true, disabled: !forfeitReason.trim(), loading: forfeitBusy }}
      onOk={() => void forfeitDeposit()} onCancel={() => { setForfeitTarget(undefined); setForfeitReason(''); setForfeitError(undefined); }}>
      <p>Phiếu {forfeitTarget?.dossierCode} sẽ được ẩn khỏi hồ sơ. Số dư cọc được ghi nhận là khoản khách bỏ cọc và không còn dùng để thanh toán.</p>
      <Input.TextArea aria-label="Lý do khách không đến" placeholder="Lý do khách không đến" maxLength={500}
        value={forfeitReason} onChange={event => setForfeitReason(event.target.value)} rows={3} />
      {forfeitError && <Alert type="error" showIcon message={forfeitError} style={{ marginTop: 12 }} />}
    </Modal>
  </div>;
}
