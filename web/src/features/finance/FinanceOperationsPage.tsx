import { Alert, Button, Card, Col, DatePicker, Descriptions, Drawer, Form, Input, InputNumber, Modal,
  Row, Select, Space, Table, Tabs, Tag, Typography, message } from 'antd';
import dayjs from 'dayjs';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../../services/api';
import { usePermissions } from '../authentication/usePermissions';
import './finance.css';

type Branch = { id: number; name: string };
type PaymentMethod = { branchId: number; code: string; name: string; active: boolean; version: number };
type Line = { id: string; chargeId: string; serviceName: string; serviceDate: string;
  grossVnd: number; discountVnd: number; netVnd: number; paidVnd: number };
type InvoicePayment = { id: string; source: string; method: string; state: string; amountVnd: number;
  receivedAt: string; chargeId: string; paymentVersion: number };
type Invoice = { id: string; number: string; patientId: number; patientName: string; branchId: number;
  visitId?: string; dossierId?: string; invoiceDate: string; dueDate: string; createdAt: string;
  grossVnd: number; discountVnd: number; netVnd: number; paidVnd: number; outstandingVnd: number;
  state: string; version: number; accountVersion: number; lines: Line[]; payments: InvoicePayment[] };
type Advance = { id: string; patientId: number; branchId: number; dossierId?: string; kind: string;
  paymentId: string; paymentState: string; method: string; amountVnd: number; allocatedVnd: number;
  refundedVnd: number; availableVnd: number; createdAt: string; version: number;
  paymentVersion: number; accountVersion: number };
type Refund = { id: string; number: string; patientId: number; branchId: number; invoiceId?: string;
  chargeId?: string; advanceId?: string; paymentId?: string; dossierPaymentId?: string;
  amountVnd: number; method: string; reason: string; state: string; requestedBy: number;
  approvedBy?: number; completedBy?: number; requestedAt: string; completedAt?: string; version: number };
type Charge = { id: string; patientId: number; patientName: string; branchId: number; visitId?: string;
  dossierId?: string; serviceName: string; serviceDate: string; amountVnd: number };
type Receivable = { patientId: number; patientName: string; branchId: number; outstandingVnd: number;
  oldestInvoiceDate: string; earliestDueDate?: string; invoiceCount: number; unbilledVnd: number };
type PlanSettlement = { dossierId: string; patientId: number; branchId: number; planVersion: number;
  plannedNetVnd: number; performedNetVnd: number; unperformedNetVnd: number;
  linkedAdvanceAvailableVnd: number; advanceForUnperformedServicesVnd: number;
  stages: { itemId: string; name: string; plannedOrder: number; plannedGrossVnd: number;
    plannedDiscountVnd: number; plannedNetVnd: number; performedNetVnd: number; unperformedNetVnd: number }[] };
type DebtEvent = { at: string; kind: string; sourceId: string; description: string;
  changeVnd: number; balanceVnd: number };
type Overview = { from: string; to: string; totals: { grossVnd: number; discountVnd: number;
  recognizedRevenueVnd: number; collectedVnd: number; refundedVnd: number; netCollectedVnd: number;
  outstandingVnd: number; unallocatedAdvanceVnd: number; advanceForUnperformedServicesVnd: number | null };
  reconciliationIssueCount: number };
type CollectionReport = { total: number; totals: { receivedVnd: number; byMethod: Record<string, number> };
  items: { id: string; source: string; patientId: number; patientName: string; receivedAt: string; paymentDate: string;
    method: string; amountVnd: number }[] };

const money = (value?: number | null) => value == null ? 'Chưa xác định' : `${value.toLocaleString('vi-VN')} đ`;
const date = (value: string) => dayjs(value).format('DD/MM/YYYY');
const moment = (value: string) => dayjs(value).format('DD/MM/YYYY HH:mm');
const methods = [
  { value: 'Cash', label: 'Tiền mặt' }, { value: 'Transfer', label: 'Chuyển khoản' },
  { value: 'POS', label: 'Thẻ/POS' }, { value: 'EWallet', label: 'Ví điện tử' },
  { value: 'Other', label: 'Khác' },
];
const methodName = (value: string) => methods.find(x => x.value === value)?.label ?? value;
const stateName: Record<string, string> = { Unpaid: 'Chưa thanh toán', PartiallyPaid: 'Thanh toán một phần',
  Paid: 'Đã thanh toán', PartiallyRefunded: 'Hoàn một phần', Refunded: 'Đã hoàn', Pending: 'Chờ xác nhận',
  Posted: 'Đã thu', Cancelled: 'Đã hủy', Requested: 'Chờ duyệt', Approved: 'Đã duyệt',
  Completed: 'Đã hoàn', Rejected: 'Từ chối' };
const post = <T,>(path: string, payload: object) => api<T>(path, { method: 'POST', body: JSON.stringify(payload) });
const requestKey = () => crypto.randomUUID();

export default function FinanceOperationsPage() {
  const has = usePermissions();
  const [branches, setBranches] = useState<Branch[]>([]);
  const [paymentMethods, setPaymentMethods] = useState<PaymentMethod[]>([]);
  const [editingMethod, setEditingMethod] = useState<PaymentMethod | null>(null);
  const [methodFormOpen, setMethodFormOpen] = useState(false);
  const [branchId, setBranchId] = useState<number>();
  const [from, setFrom] = useState(dayjs().startOf('month').format('YYYY-MM-DD'));
  const [to, setTo] = useState(dayjs().format('YYYY-MM-DD'));
  const [tab, setTab] = useState('overview');
  const [revision, setRevision] = useState(0);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [overview, setOverview] = useState<Overview>();
  const [collections, setCollections] = useState<CollectionReport>();
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [invoiceTotal, setInvoiceTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [selectedInvoice, setSelectedInvoice] = useState<Invoice>();
  const [advances, setAdvances] = useState<Advance[]>([]);
  const [receivables, setReceivables] = useState<Receivable[]>([]);
  const [selectedDebtor, setSelectedDebtor] = useState<Receivable>();
  const [debtHistory, setDebtHistory] = useState<DebtEvent[]>([]);
  const [refunds, setRefunds] = useState<Refund[]>([]);
  const [planId, setPlanId] = useState('');
  const [plan, setPlan] = useState<PlanSettlement>();
  const [unbilled, setUnbilled] = useState<Charge[]>([]);
  const [selectedCharges, setSelectedCharges] = useState<string[]>([]);
  const [invoiceFormOpen, setInvoiceFormOpen] = useState(false);
  const [advanceFormOpen, setAdvanceFormOpen] = useState(false);
  const [payFormOpen, setPayFormOpen] = useState(false);
  const [applyFormOpen, setApplyFormOpen] = useState(false);
  const [refundSource, setRefundSource] = useState<{ payment?: InvoicePayment; advance?: Advance }>();
  const [confirmingPayment, setConfirmingPayment] = useState<{ paymentId: string; paymentVersion: number; accountVersion: number }>();
  const [invoiceForm] = Form.useForm();
  const [advanceForm] = Form.useForm();
  const [paymentForm] = Form.useForm();
  const [applyForm] = Form.useForm();
  const [refundForm] = Form.useForm();
  const [confirmForm] = Form.useForm();
  const [methodForm] = Form.useForm();
  const methodOptions = paymentMethods.filter(x => x.active).map(x => ({ value: x.code, label: x.name }));
  const methodLabel = (code: string) => paymentMethods.find(x => x.code === code)?.name ?? methodName(code);

  const refresh = () => setRevision(x => x + 1);
  useEffect(() => { void api<{ branches: Branch[] }>('/api/finance/capabilities')
    .then(x => { setBranches(x.branches); setBranchId(x.branches[0]?.id); })
    .catch(e => setError((e as Error).message)); }, []);
  useEffect(() => { setPage(1); }, [branchId]);
  useEffect(() => {
    if (!branchId) return;
    let active = true;
    const query = `branchId=${branchId}`;
    const operations = [
      api<Overview>(`/api/finance/overview?${query}&from=${from}&to=${to}`).then(x => active && setOverview(x)),
      api<CollectionReport>(`/api/finance/revenue?${query}&from=${from}&to=${to}&page=1&pageSize=100`)
        .then(x => active && setCollections(x)),
      api<{ total: number; items: Invoice[] }>(`/api/finance/invoices?${query}&page=${page}&pageSize=20`)
        .then(x => { if (active) { setInvoices(x.items); setInvoiceTotal(x.total); } }),
      api<Advance[]>(`/api/finance/advances?${query}`).then(x => active && setAdvances(x)),
      api<Receivable[]>(`/api/finance/receivables?${query}`).then(x => active && setReceivables(x)),
      api<Refund[]>(`/api/finance/refunds?${query}`).then(x => active && setRefunds(x)),
      api<PaymentMethod[]>(`/api/finance/payment-methods?${query}`).then(x => active && setPaymentMethods(x)),
    ];
    setBusy(true); setError(undefined);
    void Promise.all(operations).catch(e => { if (active) setError((e as Error).message); })
      .finally(() => { if (active) setBusy(false); });
    return () => { active = false; };
  }, [branchId, from, to, page, revision]);
  useEffect(() => {
    if (!selectedInvoice) return;
    void api<Invoice>(`/api/finance/invoices/${selectedInvoice.id}`).then(setSelectedInvoice)
      .catch(e => setError((e as Error).message));
  }, [revision, selectedInvoice?.id]);

  const perform = async (action: () => Promise<unknown>, success: string) => {
    setBusy(true); setError(undefined);
    try { await action(); message.success(success); refresh(); return true; }
    catch (failure) { setError((failure as Error).message); return false; }
    finally { setBusy(false); }
  };
  const loadUnbilled = async () => {
    if (!branchId) return;
    try { setUnbilled(await api<Charge[]>(`/api/finance/unbilled-charges?branchId=${branchId}`));
      setSelectedCharges([]); invoiceForm.setFieldsValue({ invoiceDate: dayjs(), dueDate: dayjs() });
      setInvoiceFormOpen(true); }
    catch (failure) { setError((failure as Error).message); }
  };
  const loadPlan = async (id = planId) => {
    if (!id.trim()) return;
    try { setPlan(await api<PlanSettlement>(`/api/finance/plans/${encodeURIComponent(id.trim())}/settlement`));
      setPlanId(id.trim()); setTab('plan'); }
    catch (failure) { setError((failure as Error).message); }
  };
  const loadDebt = async (debtor: Receivable) => {
    try { setDebtHistory(await api<DebtEvent[]>(`/api/finance/receivables/${debtor.patientId}/history?branchId=${debtor.branchId}`));
      setSelectedDebtor(debtor); }
    catch (failure) { setError((failure as Error).message); }
  };
  const selectedRows = unbilled.filter(x => selectedCharges.includes(x.id));
  const selectedGroup = selectedRows[0];
  const onIssueInvoice = async () => {
    const values = await invoiceForm.validateFields();
    if (!branchId || !selectedGroup) return;
    const saved = await perform(() => post('/api/finance/invoices', { requestKey: requestKey(), patientId: selectedGroup.patientId,
      branchId, invoiceDate: values.invoiceDate.format('YYYY-MM-DD'), dueDate: values.dueDate.format('YYYY-MM-DD'),
      chargeIds: selectedCharges }), 'Đã lập hóa đơn');
    if (saved) setInvoiceFormOpen(false);
  };
  const onCreateAdvance = async () => {
    const values = await advanceForm.validateFields();
    if (!branchId) return;
    const saved = await perform(() => post('/api/finance/advances', { requestKey: requestKey(), branchId,
      patientId: values.patientId, dossierId: values.dossierId || null, kind: values.kind,
      method: values.method, amountVnd: values.amountVnd, payer: values.payer,
      note: values.note }), 'Đã ghi nhận khoản nhận trước');
    if (saved) { setAdvanceFormOpen(false); advanceForm.resetFields(); }
  };
  const onCollect = async () => {
    const values = await paymentForm.validateFields();
    if (!selectedInvoice) return;
    const line = selectedInvoice.lines.find(x => x.id === values.lineId);
    if (!line) return;
    const saved = await perform(() => post(`/api/finance/invoices/${selectedInvoice.id}/payments`, {
      requestKey: requestKey(), patientId: selectedInvoice.patientId,
      expectedAccountVersion: selectedInvoice.accountVersion, method: values.method,
      payer: values.payer, note: values.note, allocations: [{ chargeId: line.chargeId, amountVnd: values.amountVnd }],
    }), values.method === 'Cash' ? 'Đã thu tiền' : 'Đã tạo giao dịch chờ xác nhận');
    if (saved) { setPayFormOpen(false); paymentForm.resetFields(); }
  };
  const onApply = async () => {
    const values = await applyForm.validateFields();
    const advance = advances.find(x => x.id === values.advanceId);
    if (!advance) return;
    const saved = await perform(() => post(`/api/finance/advances/${advance.id}/allocations`, {
      requestKey: requestKey(), invoiceLineId: values.lineId, amountVnd: values.amountVnd,
      expectedVersion: advance.version }), 'Đã phân bổ tiền nhận trước');
    if (saved) { setApplyFormOpen(false); applyForm.resetFields(); }
  };
  const onRequestRefund = async () => {
    const values = await refundForm.validateFields();
    if (!refundSource) return;
    const payment = refundSource.payment;
    const saved = await perform(() => post('/api/finance/refunds', { requestKey: requestKey(),
      invoiceId: payment ? selectedInvoice?.id : null, chargeId: payment?.chargeId ?? null,
      advanceId: refundSource.advance?.id ?? null,
      paymentId: payment?.source === 'Receipt' ? payment.id : refundSource.advance?.paymentId ?? null,
      dossierPaymentId: payment?.source === 'Treatment' ? payment.id : null,
      amountVnd: values.amountVnd, method: values.method, reason: values.reason,
    }), 'Đã gửi yêu cầu hoàn tiền');
    if (saved) { setRefundSource(undefined); refundForm.resetFields(); }
  };
  const onSaveMethod = async () => {
    const values = await methodForm.validateFields();
    if (!branchId) return;
    const saved = await perform(() => post('/api/finance/payment-methods', { requestKey: requestKey(), branchId,
      code: editingMethod?.code ?? values.code, name: values.name, active: values.active,
      expectedVersion: editingMethod?.version ?? 0 }), 'Đã lưu phương thức thanh toán');
    if (saved) { setMethodFormOpen(false); setEditingMethod(null); methodForm.resetFields(); }
  };
  const decide = (refund: Refund, action: 'approve' | 'complete' | 'reject') => {
    Modal.confirm({ title: action === 'approve' ? 'Duyệt hoàn tiền?' : action === 'complete' ? 'Xác nhận đã hoàn tiền?' : 'Từ chối yêu cầu?',
      content: `${refund.number} — ${money(refund.amountVnd)}`, okText: 'Xác nhận', cancelText: 'Hủy',
      onOk: () => perform(() => post(`/api/finance/refunds/${refund.id}/${action}`,
        { requestKey: requestKey(), expectedVersion: refund.version,
          reason: action === 'reject' ? 'Không đủ điều kiện hoàn tiền' : null }), 'Đã cập nhật hoàn tiền'),
    });
  };

  const dashboard = <>
    <Typography.Paragraph type="secondary">Doanh thu theo ngày thực hiện dịch vụ; tiền thu và tiền hoàn theo ngày giao dịch. Công nợ và số dư nhận trước là số dư hiện tại.</Typography.Paragraph>
    {overview?.reconciliationIssueCount ? <Alert type="warning" showIcon message={`${overview.reconciliationIssueCount} khoản cần đối soát vì số đã phân bổ vượt nghĩa vụ.`} style={{ marginBottom: 16 }} /> : null}
    <Row gutter={[12, 12]}>{[
      ['Giá trị dịch vụ', overview?.totals.grossVnd], ['Giảm giá', overview?.totals.discountVnd],
      ['Doanh thu dịch vụ', overview?.totals.recognizedRevenueVnd], ['Tiền thực thu', overview?.totals.collectedVnd],
      ['Đã hoàn', overview?.totals.refundedVnd], ['Tiền thu ròng', overview?.totals.netCollectedVnd],
      ['Công nợ hiện tại', overview?.totals.outstandingVnd], ['Nhận trước chưa phân bổ', overview?.totals.unallocatedAdvanceVnd],
      ['Nhận trước cho dịch vụ chưa làm', overview?.totals.advanceForUnperformedServicesVnd],
    ].map(([label, value]) => <Col xs={24} sm={12} lg={8} key={String(label)}><Card size="small">
      <Typography.Text type="secondary">{label}</Typography.Text>
      <Typography.Title level={4} style={{ margin: '6px 0 0' }}>{money(value as number | null | undefined)}</Typography.Title>
    </Card></Col>)}</Row>
  </>;
  const invoiceTab = <>
    <Space style={{ marginBottom: 16 }}><Button type="primary" onClick={() => void loadUnbilled()}>Lập hóa đơn từ dịch vụ đã làm</Button>
      <Button onClick={refresh}>Tải lại</Button></Space>
    <Table rowKey="id" loading={busy} dataSource={invoices} scroll={{ x: 850 }}
      pagination={{ current: page, pageSize: 20, total: invoiceTotal, onChange: setPage }}
      columns={[{ title: 'Mã hóa đơn', dataIndex: 'number', render: (x: string, row: Invoice) => <Button type="link" onClick={() => setSelectedInvoice(row)}>{x}</Button> },
        { title: 'Bệnh nhân', dataIndex: 'patientName' }, { title: 'Ngày lập', dataIndex: 'invoiceDate', render: date },
        { title: 'Hạn thanh toán', dataIndex: 'dueDate', render: date },
        { title: 'Phải thu', dataIndex: 'netVnd', render: money, align: 'right' },
        { title: 'Còn nợ', dataIndex: 'outstandingVnd', render: money, align: 'right' },
        { title: 'Trạng thái', dataIndex: 'state', render: (x: string) => <Tag>{stateName[x] ?? x}</Tag> }]}
    />
  </>;
  const advanceTab = <>
    <Button type="primary" onClick={() => setAdvanceFormOpen(true)} style={{ marginBottom: 16 }}>Nhận tiền trước</Button>
    <Table rowKey="id" loading={busy} dataSource={advances} scroll={{ x: 850 }} pagination={{ pageSize: 20 }}
      columns={[{ title: 'Ngày nhận', dataIndex: 'createdAt', render: moment },
        { title: 'Bệnh nhân', dataIndex: 'patientId', render: (x: number) => <Link to={`/patients/${x}`}>#{x}</Link> },
        { title: 'Loại', dataIndex: 'kind', render: (x: string) => x === 'Deposit' ? 'Đặt cọc' : 'Ứng trước dịch vụ' },
        { title: 'Phương thức', dataIndex: 'method', render: methodLabel },
        { title: 'Đã nhận', dataIndex: 'amountVnd', render: money, align: 'right' },
        { title: 'Đã phân bổ', dataIndex: 'allocatedVnd', render: money, align: 'right' },
        { title: 'Còn dùng được', dataIndex: 'availableVnd', render: money, align: 'right' },
        { title: 'Trạng thái', dataIndex: 'paymentState', render: (x: string) => <Tag>{stateName[x] ?? x}</Tag> },
        { title: '', render: (_, row: Advance) => <Space>
          {row.dossierId && <Button size="small" onClick={() => void loadPlan(row.dossierId)}>Lộ trình</Button>}
          {row.availableVnd > 0 && <Button size="small" onClick={() => setRefundSource({ advance: row })}>Yêu cầu hoàn</Button>}
          {row.paymentState === 'Pending' && <Button size="small" onClick={() => setConfirmingPayment({ paymentId: row.paymentId,
            paymentVersion: row.paymentVersion, accountVersion: row.accountVersion })}>Xác nhận</Button>}
        </Space> }]}
    />
  </>;
  const debtTab = <Table rowKey={x => `${x.branchId}-${x.patientId}`} loading={busy} dataSource={receivables}
    columns={[{ title: 'Bệnh nhân', dataIndex: 'patientName', render: (x: string, row: Receivable) => <Link to={`/patients/${row.patientId}`}>{x}</Link> },
      { title: 'Số hóa đơn', dataIndex: 'invoiceCount' },
      { title: 'Phát sinh từ', dataIndex: 'oldestInvoiceDate', render: date },
      { title: 'Đến hạn sớm nhất', dataIndex: 'earliestDueDate', render: (x?: string) => x ? date(x) : 'Chưa lập hóa đơn' },
      { title: 'Chưa lập hóa đơn', dataIndex: 'unbilledVnd', render: money, align: 'right' },
      { title: 'Còn nợ', dataIndex: 'outstandingVnd', render: money, align: 'right' },
      { title: '', render: (_, row: Receivable) => <Button type="link" onClick={() => void loadDebt(row)}>Lịch sử</Button> }]}
  />;
  const refundTab = <Table rowKey="id" loading={busy} dataSource={refunds} scroll={{ x: 900 }} pagination={{ pageSize: 20 }}
    columns={[{ title: 'Mã hoàn', dataIndex: 'number' }, { title: 'Bệnh nhân', dataIndex: 'patientId' },
      { title: 'Ngày yêu cầu', dataIndex: 'requestedAt', render: moment },
      { title: 'Số tiền', dataIndex: 'amountVnd', render: money, align: 'right' },
      { title: 'Phương thức', dataIndex: 'method', render: methodLabel },
      { title: 'Lý do', dataIndex: 'reason' },
      { title: 'Trạng thái', dataIndex: 'state', render: (x: string) => <Tag>{stateName[x] ?? x}</Tag> },
      { title: 'Thao tác', render: (_, row: Refund) => <Space>
        {row.state === 'Requested' && <><Button size="small" onClick={() => decide(row, 'approve')}>Duyệt</Button>
          <Button size="small" danger onClick={() => decide(row, 'reject')}>Từ chối</Button></>}
        {row.state === 'Approved' && <Button size="small" type="primary" onClick={() => decide(row, 'complete')}>Đã hoàn tiền</Button>}
      </Space> }]}
  />;
  const planTab = <>
    <Space wrap style={{ marginBottom: 16 }}><Input aria-label="Mã phiếu điều trị" value={planId} onChange={e => setPlanId(e.target.value)}
      placeholder="Mã phiếu điều trị" style={{ width: 330 }} /><Button onClick={() => void loadPlan()}>Xem lộ trình</Button></Space>
    {plan && <><Row gutter={[12, 12]}>{[
      ['Giá trị kế hoạch đã duyệt', plan.plannedNetVnd], ['Dịch vụ đã làm', plan.performedNetVnd],
      ['Dịch vụ chưa làm', plan.unperformedNetVnd], ['Nhận trước chưa phân bổ', plan.linkedAdvanceAvailableVnd],
      ['Nhận trước gắn dịch vụ chưa làm', plan.advanceForUnperformedServicesVnd],
    ].map(([label, value]) => <Col xs={24} sm={12} lg={8} key={String(label)}><Card size="small"><Typography.Text type="secondary">{label}</Typography.Text>
      <Typography.Title level={4}>{money(value as number)}</Typography.Title></Card></Col>)}</Row>
      <Typography.Paragraph type="secondary" style={{ marginTop: 16 }}>Giá trị từng giai đoạn lấy từ hạng mục trong kế hoạch điều trị đã duyệt (phiên bản {plan.planVersion}); không chia đều tổng tiền.</Typography.Paragraph>
      <Table rowKey="itemId" dataSource={plan.stages} pagination={false} scroll={{ x: 800 }}
        columns={[{ title: 'Thứ tự', dataIndex: 'plannedOrder' }, { title: 'Giai đoạn/hạng mục', dataIndex: 'name' },
          { title: 'Giá trị kế hoạch', dataIndex: 'plannedNetVnd', render: money, align: 'right' },
          { title: 'Đã thực hiện', dataIndex: 'performedNetVnd', render: money, align: 'right' },
          { title: 'Chưa thực hiện', dataIndex: 'unperformedNetVnd', render: money, align: 'right' }]} />
    </>}
  </>;

  return <main className="finance-page">
    <Typography.Title level={2}>Quản lý tài chính</Typography.Title>
    <Space wrap style={{ marginBottom: 18 }}>
      <Select aria-label="Chi nhánh" placeholder="Chọn chi nhánh" value={branchId} onChange={setBranchId}
        style={{ minWidth: 220 }} options={branches.map(x => ({ value: x.id, label: x.name }))} />
      <DatePicker aria-label="Từ ngày" value={dayjs(from)} format="DD/MM/YYYY" onChange={x => x && setFrom(x.format('YYYY-MM-DD'))} />
      <DatePicker aria-label="Đến ngày" value={dayjs(to)} format="DD/MM/YYYY" onChange={x => x && setTo(x.format('YYYY-MM-DD'))} />
      <Button onClick={refresh}>Tải lại</Button>
    </Space>
    {error && <Alert type="error" showIcon message={error} closable onClose={() => setError(undefined)} style={{ marginBottom: 16 }} />}
    <Tabs activeKey={tab} onChange={setTab} items={[
      { key: 'overview', label: 'Tổng quan', children: dashboard },
      { key: 'invoices', label: 'Hóa đơn', children: invoiceTab },
      { key: 'advances', label: 'Tiền nhận trước', children: advanceTab },
      { key: 'plan', label: 'Lộ trình điều trị', children: planTab },
      { key: 'receivables', label: 'Công nợ', children: debtTab },
      { key: 'refunds', label: 'Hoàn tiền', children: refundTab },
      { key: 'reports', label: 'Báo cáo', children: <Tabs items={[
        { key: 'recognized', label: 'Doanh thu dịch vụ', children: <>
          <Descriptions bordered size="small" column={1} items={[
            { key: 'gross', label: 'Giá trị dịch vụ đã làm', children: money(overview?.totals.grossVnd) },
            { key: 'discount', label: 'Giảm giá', children: money(overview?.totals.discountVnd) },
            { key: 'net', label: 'Doanh thu đã ghi nhận', children: money(overview?.totals.recognizedRevenueVnd) },
          ]} /><Typography.Paragraph type="secondary">Lọc theo ngày thực hiện dịch vụ; tiền nhận trước không được cộng vào doanh thu.</Typography.Paragraph>
        </> },
        { key: 'collection', label: 'Thu theo phương thức', children: <>
          <Row gutter={[12, 12]}>{Object.entries(collections?.totals.byMethod ?? {}).map(([method, amount]) =>
            <Col xs={12} md={6} key={method}><Card size="small">{methodName(method)}<Typography.Title level={5}>{money(amount)}</Typography.Title></Card></Col>)}</Row>
          <Table rowKey={x => `${x.source}-${x.id}`} dataSource={collections?.items ?? []} pagination={{ pageSize: 20 }}
            columns={[{ title: 'Ngày thanh toán', dataIndex: 'paymentDate', render: date },
              { title: 'Bệnh nhân', dataIndex: 'patientName' },
              { title: 'Phương thức', dataIndex: 'method', render: methodLabel },
              { title: 'Số tiền', dataIndex: 'amountVnd', render: money, align: 'right' }]} />
          <Link to="/revenue">Xem toàn bộ {collections?.total ?? 0} giao dịch</Link>
        </> },
        { key: 'debts', label: 'Công nợ', children: debtTab },
        { key: 'advances', label: 'Nhận trước', children: advanceTab },
        { key: 'refunds', label: 'Hoàn tiền', children: refundTab },
      { key: 'methods', label: 'Phương thức', children: <>
        {has('settings.manage') && <Button type="primary" style={{ marginBottom: 16 }} onClick={() => {
          setEditingMethod(null); methodForm.setFieldsValue({ code: '', name: '', active: true }); setMethodFormOpen(true);
        }}>Thêm phương thức</Button>}
        <Table rowKey="code" dataSource={paymentMethods} pagination={false} columns={[
          { title: 'Mã', dataIndex: 'code' }, { title: 'Tên hiển thị', dataIndex: 'name' },
          { title: 'Trạng thái', dataIndex: 'active', render: (x: boolean) => x ? 'Đang dùng' : 'Ngừng dùng' },
          { title: '', render: (_, row: PaymentMethod) => has('settings.manage') && row.version > 0 &&
            <Button size="small" onClick={() => { setEditingMethod(row); methodForm.setFieldsValue(row); setMethodFormOpen(true); }}>Sửa</Button> },
        ]} />
      </> },
      ]} /> },
    ]} />

    <Modal title="Lập hóa đơn" open={invoiceFormOpen} onCancel={() => setInvoiceFormOpen(false)} onOk={() => void onIssueInvoice()}
      okText="Lập hóa đơn" okButtonProps={{ disabled: selectedCharges.length === 0 }} confirmLoading={busy} width={850}>
      <Typography.Paragraph type="secondary">Chọn các dịch vụ cùng bệnh nhân và cùng lượt khám hoặc phiếu điều trị. Mỗi dịch vụ chỉ được lập hóa đơn một lần.</Typography.Paragraph>
      <Table rowKey="id" size="small" dataSource={unbilled} scroll={{ y: 260, x: 700 }} pagination={{ pageSize: 10 }}
        rowSelection={{ selectedRowKeys: selectedCharges, onChange: keys => setSelectedCharges(keys.map(String)),
          getCheckboxProps: row => ({ disabled: !!selectedGroup &&
            (row.patientId !== selectedGroup.patientId || row.visitId !== selectedGroup.visitId || row.dossierId !== selectedGroup.dossierId) }) }}
        columns={[{ title: 'Bệnh nhân', dataIndex: 'patientName' }, { title: 'Dịch vụ', dataIndex: 'serviceName' },
          { title: 'Ngày làm', dataIndex: 'serviceDate', render: date },
          { title: 'Thành tiền', dataIndex: 'amountVnd', render: money, align: 'right' }]} />
      <Form form={invoiceForm} layout="vertical" style={{ marginTop: 12 }}>
        <Row gutter={12}><Col span={12}><Form.Item name="invoiceDate" label="Ngày lập hóa đơn" rules={[{ required: true }]}><DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={12}><Form.Item name="dueDate" label="Hạn thanh toán" rules={[{ required: true }]}><DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} /></Form.Item></Col></Row>
      </Form>
    </Modal>

    <Drawer title={selectedInvoice?.number ?? 'Hóa đơn'} open={!!selectedInvoice} onClose={() => setSelectedInvoice(undefined)} width={760}>
      {selectedInvoice && <>
        <Descriptions size="small" column={2} bordered items={[
          { key: 'patient', label: 'Bệnh nhân', children: selectedInvoice.patientName },
          { key: 'state', label: 'Trạng thái', children: stateName[selectedInvoice.state] ?? selectedInvoice.state },
          { key: 'issued', label: 'Ngày lập', children: date(selectedInvoice.invoiceDate) },
          { key: 'due', label: 'Đến hạn', children: date(selectedInvoice.dueDate) },
          { key: 'gross', label: 'Giá trị dịch vụ', children: money(selectedInvoice.grossVnd) },
          { key: 'discount', label: 'Giảm giá', children: money(selectedInvoice.discountVnd) },
          { key: 'paid', label: 'Đã phân bổ', children: money(selectedInvoice.paidVnd) },
          { key: 'debt', label: 'Còn nợ', children: money(selectedInvoice.outstandingVnd) },
        ]} />
        <Space style={{ margin: '16px 0' }}>
          <Button type="primary" disabled={selectedInvoice.outstandingVnd <= 0} onClick={() => {
            paymentForm.setFieldsValue({ payer: selectedInvoice.patientName, method: 'Cash' }); setPayFormOpen(true);
          }}>Thu tiền</Button>
          <Button disabled={selectedInvoice.outstandingVnd <= 0 || !advances.some(x => x.patientId === selectedInvoice.patientId && x.availableVnd > 0)}
            onClick={() => setApplyFormOpen(true)}>Dùng tiền nhận trước</Button>
        </Space>
        <Typography.Title level={5}>Dịch vụ</Typography.Title>
        <Table rowKey="id" size="small" pagination={false} dataSource={selectedInvoice.lines}
          columns={[{ title: 'Dịch vụ', dataIndex: 'serviceName' }, { title: 'Ngày làm', dataIndex: 'serviceDate', render: date },
            { title: 'Sau giảm giá', dataIndex: 'netVnd', render: money, align: 'right' },
            { title: 'Đã phân bổ', dataIndex: 'paidVnd', render: money, align: 'right' }]} />
        <Typography.Title level={5} style={{ marginTop: 22 }}>Lịch sử thanh toán</Typography.Title>
        <Table rowKey={x => `${x.source}-${x.id}-${x.chargeId}`} size="small" pagination={false} dataSource={selectedInvoice.payments}
          columns={[{ title: 'Thời gian', dataIndex: 'receivedAt', render: moment },
            { title: 'Nguồn', dataIndex: 'source', render: (x: string) => x === 'Advance' ? 'Tiền nhận trước' : x === 'Treatment' ? 'Phiếu điều trị' : 'Thu ngân' },
            { title: 'Phương thức', dataIndex: 'method', render: methodName },
            { title: 'Số tiền', dataIndex: 'amountVnd', render: money, align: 'right' },
            { title: 'Trạng thái', dataIndex: 'state', render: (x: string) => stateName[x] ?? x },
            { title: '', render: (_, row: InvoicePayment) => row.source === 'Receipt' && row.state === 'Pending'
              ? <Button size="small" onClick={() => setConfirmingPayment({ paymentId: row.id,
                paymentVersion: row.paymentVersion, accountVersion: selectedInvoice.accountVersion })}>Xác nhận</Button>
              : row.state === 'Posted' && row.source !== 'Advance' &&
                <Button size="small" onClick={() => setRefundSource({ payment: row })}>Yêu cầu hoàn</Button> }]} />
      </>}
    </Drawer>
    <Drawer title={`Lịch sử công nợ — ${selectedDebtor?.patientName ?? ''}`} open={!!selectedDebtor}
      onClose={() => setSelectedDebtor(undefined)} width={720}>
      <Table rowKey={x => `${x.kind}-${x.sourceId}-${x.at}`} dataSource={debtHistory} pagination={{ pageSize: 20 }}
        columns={[{ title: 'Thời gian', dataIndex: 'at', render: moment },
          { title: 'Nghiệp vụ', dataIndex: 'description' },
          { title: 'Biến động', dataIndex: 'changeVnd', render: money, align: 'right' },
          { title: 'Số dư', dataIndex: 'balanceVnd', render: money, align: 'right' }]} />
    </Drawer>

    <Modal title="Nhận tiền trước" open={advanceFormOpen} onCancel={() => setAdvanceFormOpen(false)} onOk={() => void onCreateAdvance()} okText="Lưu" confirmLoading={busy}>
      <Form form={advanceForm} layout="vertical" initialValues={{ kind: 'ServiceAdvance', method: 'Cash' }}>
        <Form.Item name="patientId" label="Mã bệnh nhân" rules={[{ required: true }]}><InputNumber min={1} style={{ width: '100%' }} /></Form.Item>
        <Form.Item name="dossierId" label="Mã phiếu điều trị (nếu có)"><Input /></Form.Item>
        <Form.Item name="kind" label="Loại" rules={[{ required: true }]}><Select options={[{ value: 'Deposit', label: 'Đặt cọc bảo đảm' }, { value: 'ServiceAdvance', label: 'Ứng trước dịch vụ' }]} /></Form.Item>
        <Form.Item name="amountVnd" label="Số tiền (đ)" rules={[{ required: true }]}><InputNumber min={1} precision={0} style={{ width: '100%' }} /></Form.Item>
        <Form.Item name="method" label="Phương thức" rules={[{ required: true }]}><Select options={methodOptions} /></Form.Item>
        <Form.Item name="payer" label="Người trả" rules={[{ required: true }]}><Input maxLength={160} /></Form.Item>
        <Form.Item name="note" label="Ghi chú"><Input.TextArea maxLength={1000} /></Form.Item>
      </Form>
    </Modal>
    <Modal title="Thu tiền hóa đơn" open={payFormOpen} onCancel={() => setPayFormOpen(false)} onOk={() => void onCollect()} okText="Tạo giao dịch" confirmLoading={busy}>
      <Form form={paymentForm} layout="vertical">
        <Form.Item name="lineId" label="Dịch vụ cần thu" rules={[{ required: true }]}><Select options={selectedInvoice?.lines.filter(x => x.netVnd > x.paidVnd).map(x => ({ value: x.id,
          label: `${x.serviceName} — còn ${money(x.netVnd - x.paidVnd)}` }))} /></Form.Item>
        <Form.Item name="amountVnd" label="Số tiền (đ)" rules={[{ required: true }]}><InputNumber min={1} precision={0} style={{ width: '100%' }} /></Form.Item>
        <Form.Item name="method" label="Phương thức" rules={[{ required: true }]}><Select options={methodOptions} /></Form.Item>
        <Form.Item name="payer" label="Người trả" rules={[{ required: true }]}><Input maxLength={160} /></Form.Item>
        <Form.Item name="note" label="Ghi chú"><Input.TextArea maxLength={1000} /></Form.Item>
      </Form>
    </Modal>
    <Modal title="Phân bổ tiền nhận trước" open={applyFormOpen} onCancel={() => setApplyFormOpen(false)} onOk={() => void onApply()} okText="Phân bổ" confirmLoading={busy}>
      <Form form={applyForm} layout="vertical">
        <Form.Item name="advanceId" label="Khoản nhận trước" rules={[{ required: true }]}><Select options={advances.filter(x => x.patientId === selectedInvoice?.patientId && x.branchId === selectedInvoice?.branchId &&
          (!x.dossierId || x.dossierId === selectedInvoice?.dossierId) && x.availableVnd > 0)
          .map(x => ({ value: x.id, label: `${x.kind === 'Deposit' ? 'Cọc' : 'Ứng trước'} — còn ${money(x.availableVnd)}` }))} /></Form.Item>
        <Form.Item name="lineId" label="Dịch vụ" rules={[{ required: true }]}><Select options={selectedInvoice?.lines.filter(x => x.netVnd > x.paidVnd)
          .map(x => ({ value: x.id, label: `${x.serviceName} — còn ${money(x.netVnd - x.paidVnd)}` }))} /></Form.Item>
        <Form.Item name="amountVnd" label="Số tiền phân bổ" rules={[{ required: true }]}><InputNumber min={1} precision={0} style={{ width: '100%' }} /></Form.Item>
      </Form>
    </Modal>
    <Modal title="Yêu cầu hoàn tiền" open={!!refundSource} onCancel={() => setRefundSource(undefined)} onOk={() => void onRequestRefund()} okText="Gửi yêu cầu" confirmLoading={busy}>
      <Form form={refundForm} layout="vertical" initialValues={{ method: 'Cash' }}>
        <Form.Item name="amountVnd" label="Số tiền hoàn" rules={[{ required: true }]}><InputNumber min={1} precision={0} style={{ width: '100%' }} /></Form.Item>
        <Form.Item name="method" label="Phương thức hoàn" rules={[{ required: true }]}><Select options={methodOptions} /></Form.Item>
        <Form.Item name="reason" label="Lý do" rules={[{ required: true }]}><Input.TextArea maxLength={1000} /></Form.Item>
      </Form>
    </Modal>
    <Modal title="Xác nhận đã nhận tiền" open={!!confirmingPayment} onCancel={() => setConfirmingPayment(undefined)}
      onOk={() => void confirmForm.validateFields().then(values => {
        if (!confirmingPayment) return;
        void perform(() => post(`/api/finance/payments/${confirmingPayment.paymentId}/confirm`, {
          requestKey: requestKey(), expectedVersion: confirmingPayment.paymentVersion,
          expectedAccountVersion: confirmingPayment.accountVersion,
          bankReference: values.reference, note: values.note }), 'Đã xác nhận giao dịch');
        setConfirmingPayment(undefined); confirmForm.resetFields();
      })} okText="Xác nhận" confirmLoading={busy}>
      <Typography.Paragraph type="secondary">Chỉ xác nhận sau khi đối chiếu giao dịch thực tế. Mã tham chiếu phải lấy từ chứng từ ngân hàng/POS/ví.</Typography.Paragraph>
      <Form form={confirmForm} layout="vertical">
        <Form.Item name="reference" label="Mã tham chiếu giao dịch" rules={[{ required: true }]}><Input maxLength={100} /></Form.Item>
        <Form.Item name="note" label="Ghi chú đối soát" rules={[{ required: true }]}><Input.TextArea maxLength={1000} /></Form.Item>
      </Form>
    </Modal>
    <Modal title={editingMethod ? 'Sửa phương thức' : 'Thêm phương thức'} open={methodFormOpen}
      onCancel={() => setMethodFormOpen(false)} onOk={() => void onSaveMethod()} okText="Lưu" confirmLoading={busy}>
      <Form form={methodForm} layout="vertical">
        <Form.Item name="code" label="Mã phương thức" rules={[{ required: true }]}><Input disabled={!!editingMethod} maxLength={32} /></Form.Item>
        <Form.Item name="name" label="Tên hiển thị" rules={[{ required: true }]}><Input maxLength={100} /></Form.Item>
        <Form.Item name="active" label="Trạng thái" rules={[{ required: true }]}><Select options={[
          { value: true, label: 'Đang dùng' }, { value: false, label: 'Ngừng dùng' }]} /></Form.Item>
      </Form>
    </Modal>
  </main>;
}
