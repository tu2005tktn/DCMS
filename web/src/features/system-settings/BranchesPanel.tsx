import { Alert, Button, Col, Divider, Drawer, Form, Input, Modal, Row, Select, Space, Spin, Table, Tag, Typography, Upload, message } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, apiBlob, ApiError, type Blocker } from '../../services/api';
import { usePermissions } from '../authentication/usePermissions';

type BranchDto = {
  id: number;
  code: string;
  name: string;
  address?: string;
  phone?: string;
  email?: string;
  website?: string;
  logoAssetId?: string;
  timeZoneId: string;
  bankCode?: string;
  accountNumber?: string;
  payeeName?: string;
  slotIntervalMinutes: number;
  bookingSettingsVersion: number;
  isActive: boolean;
  version: number;
  chairCount: number;
};

type ChairDto = { id: number; branchId: number; code: string; name: string; isActive: boolean; version: number };
type BranchDetailDto = Omit<BranchDto, 'chairCount'> & { chairs: ChairDto[] };
type ImpactPreviewResult = { allowed: boolean; blockers: Blocker[]; impact: string };
type BranchForm = { code: string; name: string; address?: string; phone?: string; email?: string; website?: string; timeZoneId: string; bankCode?: string; accountNumber?: string; payeeName?: string };
type ChairForm = { code: string; name: string };
type StateTarget = { kind: 'branch'; branch: BranchDto } | { kind: 'chair'; chair: ChairDto; branchId: number };

const base = '/api/settings/branches';

const blockerLabels: Record<string, string> = { Appointments: 'Lịch hẹn', Visits: 'Lượt khám', Reservations: 'Giữ chỗ', TreatmentDossiers: 'Hồ sơ điều trị', LabOrders: 'Phiếu labo' };

function BlockerList({ blockers }: { blockers: Blocker[] }) {
  return (
    <ul style={{ paddingLeft: 18, margin: 0 }}>
      {blockers.map((b, i) => (
        <li key={i} style={{ marginBottom: 4 }}>
          <Tag color="red">{b.count}</Tag><strong>{blockerLabels[b.kind] ?? b.kind}:</strong> {b.message} <Link to={b.navigationUrl}>Mở để xử lý</Link>
        </li>
      ))}
    </ul>
  );
}

// docs/specs/S58-BRANCHES-AND-CHAIRS.md §1.5
export default function BranchesPanel() {
  const [toast, toastContext] = message.useMessage();
  const has = usePermissions();
  const canManage = has('branches.manage');
  const canManageChairs = has('chairs.manage');

  const [branches, setBranches] = useState<BranchDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string>();

  const [drawerOpen, setDrawerOpen] = useState(false);
  const [selected, setSelected] = useState<BranchDetailDto | null>(null);
  const [branchForm] = Form.useForm<BranchForm>();
  const [savingBranch, setSavingBranch] = useState(false);
  const [branchError, setBranchError] = useState<ApiError>();
  const [branchDirty, setBranchDirty] = useState(false);

  const [logoAssetId, setLogoAssetId] = useState<string>();
  const [logoUrl, setLogoUrl] = useState<string>();
  const [logoUploading, setLogoUploading] = useState(false);

  const [stateTarget, setStateTarget] = useState<StateTarget>();
  const [preview, setPreview] = useState<ImpactPreviewResult>();
  const [previewError, setPreviewError] = useState<ApiError>();
  const [previewLoading, setPreviewLoading] = useState(false);
  const [stateReason, setStateReason] = useState('');
  const [stateBusy, setStateBusy] = useState(false);

  const [chairModalOpen, setChairModalOpen] = useState(false);
  const [editingChair, setEditingChair] = useState<ChairDto | null>(null);
  const [chairForm] = Form.useForm<ChairForm>();
  const [savingChair, setSavingChair] = useState(false);
  const [chairError, setChairError] = useState<ApiError>();

  const pending = useRef<{ fingerprint: string; key: string } | null>(null);
  const keyFor = (fingerprint: string) => {
    if (pending.current?.fingerprint !== fingerprint) pending.current = { fingerprint, key: crypto.randomUUID() };
    return pending.current.key;
  };

  const loadBranches = async () => {
    setLoading(true);
    setError(undefined);
    try { setBranches(await api<BranchDto[]>(base)); }
    catch (e) { setError((e as Error).message); }
    finally { setLoading(false); }
  };

  useEffect(() => { void loadBranches(); }, []);

  // Logo bytes come through the authenticated API and are shown via a revocable blob: URL.
  useEffect(() => {
    if (!selected || !logoAssetId) { setLogoUrl(undefined); return; }
    let url: string | undefined;
    let active = true;
    apiBlob(`${base}/${selected.id}/logo/${logoAssetId}`)
      .then(blob => { if (active) { url = URL.createObjectURL(blob); setLogoUrl(url); } })
      .catch(() => { if (active) setLogoUrl(undefined); });
    return () => { active = false; if (url) URL.revokeObjectURL(url); };
  }, [selected?.id, logoAssetId]);

  const fillForm = (detail: BranchDetailDto) => {
    branchForm.setFieldsValue({
      code: detail.code, name: detail.name, address: detail.address, phone: detail.phone, email: detail.email, website: detail.website,
      timeZoneId: detail.timeZoneId || 'Asia/Ho_Chi_Minh', bankCode: detail.bankCode, accountNumber: detail.accountNumber, payeeName: detail.payeeName,
    });
    setLogoAssetId(detail.logoAssetId);
  };

  const refreshDetail = async (id: number, keepForm = false) => {
    const detail = await api<BranchDetailDto>(`${base}/${id}`);
    setSelected(detail);
    if (!keepForm) { fillForm(detail); setBranchDirty(false); }
    return detail;
  };

  const openBranch = async (b: BranchDto | null) => {
    if (drawerOpen && branchDirty && !window.confirm('Bỏ thay đổi chưa lưu của chi nhánh đang mở?')) return;
    setBranchError(undefined);
    pending.current = null;
    branchForm.resetFields();
    if (!b) {
      setSelected(null);
      setLogoAssetId(undefined);
      branchForm.setFieldsValue({ timeZoneId: 'Asia/Ho_Chi_Minh' });
      setBranchDirty(false);
      setDrawerOpen(true);
      return;
    }
    try { await refreshDetail(b.id); setDrawerOpen(true); }
    catch (e) { toast.error((e as Error).message); }
  };

  const closeDrawer = () => {
    if (savingBranch || logoUploading) return;
    if (branchDirty && !window.confirm('Đóng và bỏ thay đổi chưa lưu?')) return;
    setDrawerOpen(false);
  };

  const saveBranch = async (values: BranchForm) => {
    if (savingBranch) return;
    const body = {
      expectedVersion: selected?.version ?? 0,
      code: values.code.trim(), name: values.name.trim(),
      address: values.address?.trim() || null, phone: values.phone?.trim() || null, email: values.email?.trim() || null,
      website: values.website?.trim() || null, logoAssetId: logoAssetId ?? null, timeZoneId: values.timeZoneId || 'Asia/Ho_Chi_Minh',
      bankCode: values.bankCode?.trim() || null, accountNumber: values.accountNumber?.trim() || null, payeeName: values.payeeName?.trim() || null,
    };
    const target = selected ? `${base}/${selected.id}` : base;
    const requestKey = keyFor(JSON.stringify({ target, ...body }));
    setSavingBranch(true);
    setBranchError(undefined);
    try {
      const saved = await api<BranchDetailDto>(target, { method: selected ? 'PUT' : 'POST', body: JSON.stringify({ ...body, requestKey }) });
      pending.current = null;
      setSelected(saved);
      fillForm(saved);
      setBranchDirty(false);
      toast.success(selected ? 'Đã lưu chi nhánh.' : 'Đã tạo chi nhánh. Có thể tải logo và thêm ghế.');
      void loadBranches();
    } catch (e) {
      const err = e as ApiError;
      setBranchError(err);
      // Keep what the user typed; only refresh the server copy (version) for comparison.
      if (err.code === 'MSG111' && selected) void refreshDetail(selected.id, true).catch(() => undefined);
    } finally {
      setSavingBranch(false);
    }
  };

  const uploadLogo = async (file: File) => {
    if (!selected) { toast.warning('Lưu chi nhánh trước khi tải logo.'); return false; }
    if (!['image/png', 'image/jpeg', 'image/webp'].includes(file.type) || file.size > 2 * 1024 * 1024) {
      toast.error('Chỉ nhận ảnh PNG, JPEG hoặc WebP tối đa 2 MiB.');
      return false;
    }
    setLogoUploading(true);
    const form = new FormData();
    form.append('file', file, file.name);
    try {
      const result = await api<{ assetId: string }>(`${base}/${selected.id}/logo`, { method: 'POST', body: form });
      setLogoAssetId(result.assetId);
      setBranchDirty(true);
      toast.success('Đã tải logo. Bấm "Lưu chi nhánh" để áp dụng.');
    } catch (e) {
      toast.error((e as Error).message);
    } finally {
      setLogoUploading(false);
    }
    return false;
  };

  const openState = async (target: StateTarget) => {
    const active = target.kind === 'branch' ? !target.branch.isActive : !target.chair.isActive;
    setStateTarget(target);
    setPreview(undefined);
    setPreviewError(undefined);
    setStateReason('');
    setPreviewLoading(true);
    pending.current = null;
    const path = target.kind === 'branch' ? `${base}/${target.branch.id}/state-preview` : `${base}/${target.branchId}/chairs/${target.chair.id}/state-preview`;
    const version = target.kind === 'branch' ? target.branch.version : target.chair.version;
    try {
      setPreview(await api<ImpactPreviewResult>(path, {
        method: 'POST', body: JSON.stringify({ active, requestKey: crypto.randomUUID(), expectedVersion: version, reason: null }),
      }));
    } catch (e) {
      setPreviewError(e as ApiError);
    } finally {
      setPreviewLoading(false);
    }
  };

  const confirmState = async () => {
    if (!stateTarget || stateBusy) return;
    const isBranch = stateTarget.kind === 'branch';
    const active = isBranch ? !stateTarget.branch.isActive : !stateTarget.chair.isActive;
    if (!active && !stateReason.trim()) return;
    const path = isBranch ? `${base}/${stateTarget.branch.id}/state` : `${base}/${stateTarget.branchId}/chairs/${stateTarget.chair.id}/state`;
    const body = { active, expectedVersion: isBranch ? stateTarget.branch.version : stateTarget.chair.version, reason: stateReason.trim() || null };
    const requestKey = keyFor(JSON.stringify({ path, ...body }));
    setStateBusy(true);
    try {
      await api(path, { method: 'POST', body: JSON.stringify({ ...body, requestKey }) });
      pending.current = null;
      toast.success(active ? 'Đã khôi phục hoạt động.' : 'Đã ngừng hoạt động.');
      setStateTarget(undefined);
      void loadBranches();
      if (selected && (isBranch ? stateTarget.branch.id === selected.id : stateTarget.branchId === selected.id)) void refreshDetail(selected.id, branchDirty);
    } catch (e) {
      const err = e as ApiError;
      setPreviewError(err);
      if (err.blockers?.length) setPreview({ allowed: false, blockers: err.blockers, impact: err.message });
    } finally {
      setStateBusy(false);
    }
  };

  const openChair = (chair: ChairDto | null) => {
    setEditingChair(chair);
    setChairError(undefined);
    pending.current = null;
    chairForm.resetFields();
    chairForm.setFieldsValue(chair
      ? { code: chair.code, name: chair.name }
      : {});
    setChairModalOpen(true);
  };

  const saveChair = async (values: ChairForm) => {
    if (!selected || savingChair) return;
    const body = { expectedVersion: editingChair?.version ?? 0, code: values.code.trim(), name: values.name.trim() };
    const target = editingChair ? `${base}/${selected.id}/chairs/${editingChair.id}` : `${base}/${selected.id}/chairs`;
    const requestKey = keyFor(JSON.stringify({ target, ...body }));
    setSavingChair(true);
    setChairError(undefined);
    try {
      await api(target, { method: editingChair ? 'PUT' : 'POST', body: JSON.stringify({ ...body, requestKey }) });
      pending.current = null;
      toast.success('Đã lưu ghế.');
      setChairModalOpen(false);
      void refreshDetail(selected.id, branchDirty);
      void loadBranches();
    } catch (e) {
      setChairError(e as ApiError);
    } finally {
      setSavingChair(false);
    }
  };

  const stateActive = stateTarget ? (stateTarget.kind === 'branch' ? !stateTarget.branch.isActive : !stateTarget.chair.isActive) : false;
  const stateName = stateTarget ? (stateTarget.kind === 'branch' ? `chi nhánh "${stateTarget.branch.name}"` : `ghế "${stateTarget.chair.name}"`) : '';
  const errorText = (e: ApiError) =>
    e.code === 'MSG111' ? 'Dữ liệu đã được thay đổi ở nơi khác. Phần bạn nhập vẫn được giữ; kiểm tra lại rồi lưu tiếp.'
      : e.code === 'MSG144' && e.status === 503 ? 'Chưa thể kiểm tra nghĩa vụ phiếu Labo vì module Labo chưa sẵn sàng. Thao tác ngừng chi nhánh bị chặn để bảo đảm an toàn.'
      : e.message;

  return (
    <div className="branches-panel">
      {toastContext}
      <Space wrap style={{ marginBottom: 16, justifyContent: 'space-between', width: '100%' }}>
        <div>
          <Typography.Title level={3} style={{ margin: 0 }}>Chi nhánh & ghế điều trị</Typography.Title>
          <Typography.Text type="secondary">Thông tin cơ sở, logo, tài khoản nhận chuyển khoản và ghế điều trị của từng chi nhánh.</Typography.Text>
        </div>
        {canManage && <Button type="primary" onClick={() => void openBranch(null)}>Thêm chi nhánh</Button>}
      </Space>

      {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} action={<Button onClick={() => void loadBranches()}>Tải lại</Button>} />}

      <Table
        loading={loading}
        rowKey="id"
        dataSource={branches}
        scroll={{ x: 820 }}
        pagination={false}
        columns={[
          { title: 'Mã', dataIndex: 'code', width: 110, render: v => <code>{v}</code> },
          {
            title: 'Chi nhánh',
            dataIndex: 'name',
            render: (v, r) => <div><strong>{v}</strong>{r.address && <div style={{ fontSize: 12, color: '#666' }}>{r.address}</div>}</div>,
          },
          { title: 'Liên hệ', render: (_, r) => <div style={{ fontSize: 13 }}>{r.phone && <div>{r.phone}</div>}{r.email && <div>{r.email}</div>}</div> },
          {
            title: 'Tài khoản chuyển khoản',
            render: (_, r) => (r.accountNumber
              ? <div><Tag color="geekblue">{r.bankCode}</Tag>{r.accountNumber}{r.payeeName && <div style={{ fontSize: 12, color: '#888' }}>{r.payeeName}</div>}</div>
              : <Typography.Text type="secondary">Chưa cấu hình</Typography.Text>),
          },
          { title: 'Ghế đang dùng', width: 120, align: 'center', dataIndex: 'chairCount' },
          { title: 'Trạng thái', width: 140, render: (_, r) => (r.isActive ? <Tag color="success">Hoạt động</Tag> : <Tag>Ngừng hoạt động</Tag>) },
          {
            title: 'Thao tác',
            width: 190,
            render: (_, r) => (
              <Space size="small">
                <Button size="small" onClick={() => void openBranch(r)}>{canManage ? 'Sửa' : 'Xem'}</Button>
                {canManage && (
                  <Button size="small" danger={r.isActive} onClick={() => void openState({ kind: 'branch', branch: r })}>
                    {r.isActive ? 'Ngừng' : 'Khôi phục'}
                  </Button>
                )}
              </Space>
            ),
          },
        ]}
      />

      <Drawer
        open={drawerOpen}
        onClose={closeDrawer}
        width={780}
        title={selected ? `Chi nhánh: ${selected.name}` : 'Thêm chi nhánh'}
        extra={canManage && <Button type="primary" onClick={() => branchForm.submit()} loading={savingBranch} disabled={logoUploading}>Lưu chi nhánh</Button>}
      >
        {!canManage && <Alert type="info" showIcon message="Bạn chỉ có quyền xem chi nhánh." style={{ marginBottom: 16 }} />}
        {selected && !selected.isActive && <Alert type="warning" showIcon message="Chi nhánh đang ngừng hoạt động: không nhận ghế mới và không nhận lịch hẹn." style={{ marginBottom: 16 }} />}
        {branchError && <Alert type="error" showIcon message={errorText(branchError)} style={{ marginBottom: 16 }} />}

        <Form form={branchForm} layout="vertical" onFinish={values => void saveBranch(values)} onValuesChange={() => setBranchDirty(true)} disabled={!canManage || savingBranch}>
          <Typography.Title level={5}>Thông tin chung</Typography.Title>
          <Row gutter={12}>
            <Col xs={24} sm={8}>
              <Form.Item name="code" label="Mã chi nhánh" rules={[{ required: true, whitespace: true, message: 'Nhập mã chi nhánh.' },
                { pattern: /^[A-Za-z0-9_-]{1,40}$/, message: 'Chỉ chữ, số, gạch ngang/dưới; tối đa 40 ký tự.' }]}>
                <Input placeholder="CS-CAUGIAY" />
              </Form.Item>
            </Col>
            <Col xs={24} sm={16}>
              <Form.Item name="name" label="Tên chi nhánh" rules={[{ required: true, whitespace: true, message: 'Nhập tên chi nhánh.' }, { max: 160, message: 'Tối đa 160 ký tự.' }]}>
                <Input placeholder="Cơ sở Cầu Giấy" />
              </Form.Item>
            </Col>
          </Row>
          <Form.Item name="address" label="Địa chỉ" rules={[{ max: 1000, message: 'Tối đa 1.000 ký tự.' }]}>
            <Input.TextArea rows={2} />
          </Form.Item>
          <Row gutter={12}>
            <Col xs={24} sm={8}>
              <Form.Item name="phone" label="Điện thoại" rules={[{ pattern: /^\+?[0-9 .()-]{1,20}$/, message: 'Số điện thoại không hợp lệ.' }]}><Input /></Form.Item>
            </Col>
            <Col xs={24} sm={8}>
              <Form.Item name="email" label="Email" rules={[{ type: 'email', message: 'Email không hợp lệ.' }, { max: 254 }]}><Input /></Form.Item>
            </Col>
            <Col xs={24} sm={8}>
              <Form.Item name="website" label="Website" rules={[{ type: 'url', message: 'Nhập địa chỉ http(s) hợp lệ.' }, { max: 255 }]}><Input placeholder="https://" /></Form.Item>
            </Col>
          </Row>
          <Form.Item name="timeZoneId" label="Múi giờ" rules={[{ required: true }]}>
            <Select options={[{ value: 'Asia/Ho_Chi_Minh', label: 'Asia/Ho_Chi_Minh (GMT+7)' }, { value: 'Asia/Bangkok', label: 'Asia/Bangkok (GMT+7)' }]} />
          </Form.Item>

          <Divider />
          <Typography.Title level={5}>Logo chi nhánh</Typography.Title>
          <div style={{ marginBottom: 16, display: 'flex', alignItems: 'center', gap: 16, flexWrap: 'wrap' }}>
            <div style={{ width: 100, height: 100, border: '1px dashed #d9d9d9', borderRadius: 6, display: 'flex', alignItems: 'center', justifyContent: 'center', overflow: 'hidden' }}>
              {logoAssetId && !logoUrl ? <Spin size="small" />
                : logoUrl ? <img src={logoUrl} alt="Logo chi nhánh" style={{ maxWidth: '100%', maxHeight: '100%', objectFit: 'contain' }} />
                : <Typography.Text type="secondary">Chưa có logo</Typography.Text>}
            </div>
            <div>
              <Space wrap>
                <Upload beforeUpload={file => uploadLogo(file)} showUploadList={false} accept="image/png,image/jpeg,image/webp" disabled={!selected || !canManage || logoUploading}>
                  <Button loading={logoUploading} disabled={!selected || !canManage}>Tải logo</Button>
                </Upload>
                {logoAssetId && canManage && <Button disabled={logoUploading} onClick={() => { setLogoAssetId(undefined); setBranchDirty(true); }}>Gỡ logo</Button>}
              </Space>
              <div style={{ fontSize: 12, color: '#888', marginTop: 4 }}>
                {selected ? 'PNG, JPEG hoặc WebP tối đa 2 MiB. Máy chủ kiểm tra định dạng thật và xóa metadata.' : 'Lưu chi nhánh trước, sau đó tải logo.'}
              </div>
            </div>
          </div>

          <Divider />
          <Typography.Title level={5}>Tài khoản nhận chuyển khoản</Typography.Title>
          <Typography.Paragraph type="secondary">Nhập đủ cả ba trường hoặc để trống cả ba.</Typography.Paragraph>
          <Row gutter={12}>
            <Col xs={24} sm={8}><Form.Item name="bankCode" label="Mã ngân hàng" rules={[{ max: 20 }]}><Input placeholder="VCB, MB, TCB..." /></Form.Item></Col>
            <Col xs={24} sm={8}><Form.Item name="accountNumber" label="Số tài khoản" rules={[{ pattern: /^[0-9]{1,50}$/, message: 'Chỉ gồm chữ số.' }]}><Input inputMode="numeric" /></Form.Item></Col>
            <Col xs={24} sm={8}><Form.Item name="payeeName" label="Tên chủ tài khoản" rules={[{ max: 160 }]}><Input /></Form.Item></Col>
          </Row>
        </Form>

        {selected && (
          <>
            <Divider />
            <Space style={{ justifyContent: 'space-between', width: '100%', marginBottom: 12 }} wrap>
              <Typography.Title level={5} style={{ margin: 0 }}>Ghế điều trị ({selected.chairs.filter(c => c.isActive).length}/{selected.chairs.length} đang dùng)</Typography.Title>
              {canManageChairs && selected.isActive && <Button size="small" type="primary" onClick={() => openChair(null)}>Thêm ghế</Button>}
            </Space>
            <Typography.Paragraph type="secondary" style={{ fontSize: 12 }}>
              Mỗi ghế chỉ tiếp nhận một bệnh nhân tại một thời điểm; thời gian chờ thuốc tê hoặc chờ kết quả vẫn giữ ghế.
            </Typography.Paragraph>
            <Table
              size="small"
              pagination={false}
              rowKey="id"
              dataSource={selected.chairs}
              columns={[
                { title: 'Mã ghế', dataIndex: 'code', width: 110, render: v => <code>{v}</code> },
                { title: 'Tên ghế', dataIndex: 'name' },
                { title: 'Trạng thái', width: 110, render: (_, c) => (c.isActive ? <Tag color="success">Đang dùng</Tag> : <Tag>Tạm ngừng</Tag>) },
                ...(canManageChairs
                  ? [{
                      title: 'Thao tác',
                      width: 170,
                      render: (_: unknown, c: ChairDto) => (
                        <Space size="small">
                          <Button size="small" onClick={() => openChair(c)}>Sửa</Button>
                          <Button size="small" danger={c.isActive} onClick={() => void openState({ kind: 'chair', chair: c, branchId: selected.id })}>
                            {c.isActive ? 'Ngừng' : 'Khôi phục'}
                          </Button>
                        </Space>
                      ),
                    }]
                  : []),
              ]}
            />
          </>
        )}
      </Drawer>

      <Modal
        open={!!stateTarget}
        onCancel={() => { if (!stateBusy) setStateTarget(undefined); }}
        title={stateActive ? `Khôi phục ${stateName}` : `Ngừng hoạt động ${stateName}`}
        okText={stateActive ? 'Khôi phục' : 'Ngừng hoạt động'}
        okButtonProps={{ danger: !stateActive, disabled: previewLoading || !preview?.allowed || (!stateActive && !stateReason.trim()) }}
        confirmLoading={stateBusy}
        onOk={() => void confirmState()}
      >
        {previewLoading ? <div style={{ textAlign: 'center', padding: 20 }}><Spin /></div> : (
          <>
            {previewError && !preview?.blockers.length && <Alert type="error" showIcon message={errorText(previewError)} style={{ marginBottom: 12 }} />}
            {preview && (
              preview.allowed
                ? <Alert type="success" showIcon message={preview.impact} style={{ marginBottom: 12 }} />
                : <Alert type="error" showIcon message="Còn nghĩa vụ cần xử lý trước" description={<><div style={{ marginBottom: 8 }}>{preview.impact}</div><BlockerList blockers={preview.blockers} /></>} style={{ marginBottom: 12 }} />
            )}
            {preview?.allowed && (
              <Input.TextArea rows={2} maxLength={1000} showCount value={stateReason} onChange={e => setStateReason(e.target.value)}
                aria-label="Lý do" placeholder={stateActive ? 'Lý do (không bắt buộc)' : 'Lý do ngừng hoạt động (bắt buộc)'} />
            )}
          </>
        )}
      </Modal>

      <Modal
        open={chairModalOpen}
        onCancel={() => { if (!savingChair) setChairModalOpen(false); }}
        title={editingChair ? `Sửa ghế: ${editingChair.name}` : 'Thêm ghế điều trị'}
        onOk={() => chairForm.submit()}
        okText="Lưu ghế"
        confirmLoading={savingChair}
      >
        {chairError && <Alert type="error" showIcon message={errorText(chairError)} style={{ marginBottom: 12 }} />}
        <Form form={chairForm} layout="vertical" onFinish={values => void saveChair(values)} disabled={savingChair}>
          <Form.Item name="code" label="Mã ghế" rules={[{ required: true, whitespace: true, message: 'Nhập mã ghế.' }, { pattern: /^[A-Za-z0-9_-]{1,40}$/, message: 'Chỉ chữ, số, gạch ngang/dưới; tối đa 40 ký tự.' }]}>
            <Input placeholder="GHE-01" />
          </Form.Item>
          <Form.Item name="name" label="Tên ghế" rules={[{ required: true, whitespace: true, message: 'Nhập tên ghế.' }, { max: 160, message: 'Tối đa 160 ký tự.' }]}>
            <Input placeholder="Ghế 01 - Tổng quát" />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
