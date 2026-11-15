import { Alert, Button, Card, Col, DatePicker, Input, Modal, Row, Select, Space, Table, Tag, Typography } from 'antd';
import dayjs from 'dayjs';
import { useEffect, useState } from 'react';
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
      <Col xs={24} md={8}><Card loading={loading} hoverable onClick={() => setActiveModal('service')} style={{ cursor: 'pointer' }}><Typography.Text type="secondary">Số tiền cần thu dịch vụ</Typography.Text><Typography.Title level={3} style={{ margin: '8px 0 0' }}>{money(data?.serviceAmountVnd ?? 0)}</Typography.Title></Card></Col>
      <Col xs={24} md={8}><Card loading={loading} hoverable onClick={() => setActiveModal('actual')} style={{ cursor: 'pointer' }}><Typography.Text type="secondary">Tiền thực thu</Typography.Text><Typography.Title level={3} style={{ margin: '8px 0 0' }}>{money(data?.actualReceivedVnd ?? 0)}</Typography.Title></Card></Col>
      <Col xs={24} md={8}><Card loading={loading} hoverable onClick={() => setActiveModal('advance')} style={{ cursor: 'pointer' }}><Typography.Text type="secondary">Tiền nhận trước</Typography.Text><Typography.Title level={3} style={{ margin: '8px 0 0' }}>{money(data?.advanceReceivedVnd ?? 0)}</Typography.Title></Card></Col>
      <Col xs={24} md={8}><Card loading={loading} hoverable onClick={() => setActiveModal('outstanding')} style={{ cursor: 'pointer' }}><Typography.Text type="secondary">Tiền còn cần thu</Typography.Text><Typography.Title level={3} style={{ margin: '8px 0 0' }}>{money(data?.remainingReceivableVnd ?? 0)}</Typography.Title></Card></Col>
      <Col xs={24} md={8}><Card loading={loading} hoverable onClick={() => setActiveModal('materials')} style={{ cursor: 'pointer' }}><Typography.Text type="secondary">Tiền dụng cụ vật liệu</Typography.Text><Typography.Title level={3} style={{ margin: '8px 0 0' }}>{money(data?.materialPurchaseVnd ?? 0)}</Typography.Title></Card></Col>
      <Col xs={24} md={8}><Card loading={loading} hoverable onClick={() => setActiveModal('profit')} style={{ cursor: 'pointer' }}><Typography.Text type="secondary">Lãi</Typography.Text><Typography.Title level={3} style={{ margin: '8px 0 0' }}>{money(data?.profitVnd ?? 0)}</Typography.Title></Card></Col>
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
