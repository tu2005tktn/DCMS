import {
  Alert,
  Button,
  Card,
  Col,
  Descriptions,
  Divider,
  Drawer,
  Form,
  Image,
  Input,
  Modal,
  Popconfirm,
  Row,
  Space,
  Spin,
  Switch,
  Table,
  Tag,
  Typography,
  Upload,
  message,
} from 'antd';
import type { UploadFile } from 'antd/es/upload/interface';
import { useEffect, useRef, useState } from 'react';
import { api, ApiError } from '../../services/api';
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

type ChairDto = {
  id: number;
  branchId: number;
  code: string;
  name: string;
  openMinute: number;
  closeMinute: number;
  isActive: boolean;
  version: number;
};

type BranchDetailDto = BranchDto & {
  chairs: ChairDto[];
};

type BlockerItem = {
  kind: string;
  message: string;
  count: number;
  navigationUrl: string;
};

type ImpactPreviewResult = {
  allowed: boolean;
  blockers: BlockerItem[];
  impact: string;
};

export default function BranchesPanel() {
  const [toast, toastContext] = message.useMessage();
  const has = usePermissions();
  const canManage = has('branches.manage');

  const [branches, setBranches] = useState<BranchDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string>();

  // Drawer Edit/Create Branch
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [selectedBranch, setSelectedBranch] = useState<BranchDetailDto | null>(null);
  const [branchForm] = Form.useForm();
  const [savingBranch, setSavingBranch] = useState(false);
  const [branchError, setBranchError] = useState<ApiError>();

  // State Preview Modal for Branch
  const [previewModalOpen, setPreviewModalOpen] = useState(false);
  const [impactPreview, setImpactPreview] = useState<ImpactPreviewResult | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [targetBranch, setTargetBranch] = useState<BranchDto | null>(null);
  const [deactivateReason, setDeactivateReason] = useState('');

  // Chair Modal
  const [chairModalOpen, setChairModalOpen] = useState(false);
  const [editingChair, setEditingChair] = useState<ChairDto | null>(null);
  const [chairForm] = Form.useForm();
  const [savingChair, setSavingChair] = useState(false);

  // Logo upload state
  const [logoUploading, setLogoUploading] = useState(false);
  const [logoAssetId, setLogoAssetId] = useState<string | undefined>();

  const branchPending = useRef<{ fingerprint: string; key: string } | null>(null);

  const loadBranches = async () => {
    setLoading(true);
    setError(undefined);
    try {
      const rows = await api<BranchDto[]>('/api/system-settings/branches');
      setBranches(rows);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadBranches();
  }, []);

  const openBranchDetail = async (b: BranchDto) => {
    setBranchError(undefined);
    try {
      const detail = await api<BranchDetailDto>(`/api/system-settings/branches/${b.id}`);
      setSelectedBranch(detail);
      setLogoAssetId(detail.logoAssetId);
      branchForm.setFieldsValue({
        code: detail.code,
        name: detail.name,
        address: detail.address,
        phone: detail.phone,
        email: detail.email,
        website: detail.website,
        timeZoneId: detail.timeZoneId || 'Asia/Ho_Chi_Minh',
        bankCode: detail.bankCode,
        accountNumber: detail.accountNumber,
        payeeName: detail.payeeName,
      });
      setDrawerOpen(true);
    } catch (e) {
      toast.error((e as Error).message);
    }
  };

  const openCreateBranch = () => {
    setSelectedBranch(null);
    setLogoAssetId(undefined);
    branchForm.resetFields();
    branchForm.setFieldsValue({
      code: `CS-${branches.length + 1}`,
      timeZoneId: 'Asia/Ho_Chi_Minh',
    });
    setBranchError(undefined);
    setDrawerOpen(true);
  };

  const saveBranch = async (values: any) => {
    setSavingBranch(true);
    setBranchError(undefined);
    const body = {
      expectedVersion: selectedBranch?.version ?? 0,
      code: values.code,
      name: values.name,
      address: values.address,
      phone: values.phone,
      email: values.email,
      website: values.website,
      logoAssetId,
      timeZoneId: values.timeZoneId || 'Asia/Ho_Chi_Minh',
      bankCode: values.bankCode,
      accountNumber: values.accountNumber,
      payeeName: values.payeeName,
    };

    const target = selectedBranch ? `/api/system-settings/branches/${selectedBranch.id}` : '/api/system-settings/branches';
    const fingerprint = JSON.stringify({ target, ...body });
    if (branchPending.current?.fingerprint !== fingerprint) {
      branchPending.current = { fingerprint, key: crypto.randomUUID() };
    }

    try {
      const saved = await api<BranchDetailDto>(target, {
        method: selectedBranch ? 'PUT' : 'POST',
        body: JSON.stringify({ ...body, requestKey: branchPending.current.key }),
      });
      toast.success('Lưu chi nhánh thành công');
      setSelectedBranch(saved);
      void loadBranches();
    } catch (e) {
      setBranchError(e as ApiError);
    } finally {
      setSavingBranch(false);
    }
  };

  const checkBranchState = async (b: BranchDto, active: boolean) => {
    setTargetBranch(b);
    setPreviewLoading(true);
    setDeactivateReason('');
    try {
      const preview = await api<ImpactPreviewResult>(`/api/system-settings/branches/${b.id}/state-preview`, {
        method: 'POST',
        body: JSON.stringify({ active, requestKey: crypto.randomUUID(), expectedVersion: b.version, reason: '' }),
      });
      setImpactPreview(preview);
      setPreviewModalOpen(true);
    } catch (e) {
      toast.error((e as Error).message);
    } finally {
      setPreviewLoading(false);
    }
  };

  const confirmDeactivateBranch = async () => {
    if (!targetBranch) return;
    try {
      await api(`/api/system-settings/branches/${targetBranch.id}/state`, {
        method: 'POST',
        body: JSON.stringify({
          active: false,
          requestKey: crypto.randomUUID(),
          expectedVersion: targetBranch.version,
          reason: deactivateReason || 'Ngừng hoạt động chi nhánh',
        }),
      });
      toast.success('Đã ngừng hoạt động chi nhánh');
      setPreviewModalOpen(false);
      if (selectedBranch?.id === targetBranch.id) setDrawerOpen(false);
      void loadBranches();
    } catch (e) {
      toast.error((e as Error).message);
    }
  };

  const handleLogoUpload = async (file: File) => {
    if (!selectedBranch) {
      toast.warning('Vui lòng lưu chi nhánh trước khi tải logo.');
      return false;
    }
    setLogoUploading(true);
    const formData = new FormData();
    formData.append('file', file);
    try {
      const res = await fetch(`/api/system-settings/branches/${selectedBranch.id}/logo`, {
        method: 'POST',
        headers: {
          Authorization: `Bearer ${localStorage.getItem('token') || ''}`,
        },
        body: formData,
      });
      if (!res.ok) {
        const err = await res.json();
        throw new Error(err.message || 'Lỗi tải ảnh');
      }
      const data = await res.json();
      setLogoAssetId(data.assetId);
      toast.success('Đã kiểm tra định dạng và tải logo lên thành công');
    } catch (e) {
      toast.error((e as Error).message);
    } finally {
      setLogoUploading(false);
    }
    return false;
  };

  // Chair management
  const openChairCreate = () => {
    setEditingChair(null);
    chairForm.resetFields();
    chairForm.setFieldsValue({
      code: `GHE-${(selectedBranch?.chairs.length ?? 0) + 1}`,
      openMinute: 480,
      closeMinute: 1080,
    });
    setChairModalOpen(true);
  };

  const openChairEdit = (chair: ChairDto) => {
    setEditingChair(chair);
    chairForm.setFieldsValue({
      code: chair.code,
      name: chair.name,
      openMinute: chair.openMinute,
      closeMinute: chair.closeMinute,
    });
    setChairModalOpen(true);
  };

  const saveChair = async (values: any) => {
    if (!selectedBranch) return;
    setSavingChair(true);
    const body = {
      expectedVersion: editingChair?.version ?? 0,
      code: values.code,
      name: values.name,
      openMinute: values.openMinute,
      closeMinute: values.closeMinute,
    };
    const target = editingChair
      ? `/api/system-settings/branches/${selectedBranch.id}/chairs/${editingChair.id}`
      : `/api/system-settings/branches/${selectedBranch.id}/chairs`;
    try {
      await api(target, {
        method: editingChair ? 'PUT' : 'POST',
        body: JSON.stringify({ ...body, requestKey: crypto.randomUUID() }),
      });
      toast.success('Lưu ghế thành công');
      setChairModalOpen(false);
      void openBranchDetail(selectedBranch);
    } catch (e) {
      toast.error((e as Error).message);
    } finally {
      setSavingChair(false);
    }
  };

  const toggleChairState = async (chair: ChairDto, active: boolean) => {
    if (!selectedBranch) return;
    if (!active) {
      // Check chair preview
      try {
        const preview = await api<ImpactPreviewResult>(
          `/api/system-settings/branches/${selectedBranch.id}/chairs/${chair.id}/state-preview`,
          { method: 'POST', body: JSON.stringify({ active: false, requestKey: crypto.randomUUID(), expectedVersion: chair.version, reason: '' }) }
        );
        if (!preview.allowed) {
          Modal.error({
            title: 'Không thể ngừng hoạt động ghế (409 MSG205)',
            content: (
              <div>
                <p>Ghế đang có nghĩa vụ ràng buộc ngăn chặn thao tác:</p>
                <ul>{preview.blockers.map((b, i) => <li key={i}>{b.message}</li>)}</ul>
              </div>
            ),
          });
          return;
        }
      } catch (e) {
        toast.error((e as Error).message);
        return;
      }
    }

    try {
      await api(`/api/system-settings/branches/${selectedBranch.id}/chairs/${chair.id}/state`, {
        method: 'POST',
        body: JSON.stringify({ active, requestKey: crypto.randomUUID(), expectedVersion: chair.version, reason: active ? 'Kích hoạt lại ghế' : 'Ngừng ghế' }),
      });
      toast.success('Đã cập nhật trạng thái ghế');
      void openBranchDetail(selectedBranch);
    } catch (e) {
      toast.error((e as Error).message);
    }
  };

  return (
    <div className="branches-panel">
      {toastContext}
      <Space wrap style={{ marginBottom: 16, justifyContent: 'space-between', width: '100%' }}>
        <div>
          <Typography.Title level={3} style={{ margin: 0 }}>
            Quản lý Chi nhánh & Ghế điều trị (DCMS-124)
          </Typography.Title>
          <Typography.Text type="secondary">
            Cấu hình thông tin cơ sở, logo phòng khám, tài khoản ngân hàng chuyển khoản và phân bổ ghế điều trị vật lý.
          </Typography.Text>
        </div>
        {canManage && (
          <Button type="primary" onClick={openCreateBranch}>
            + Thêm chi nhánh mới
          </Button>
        )}
      </Space>

      {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} />}

      <Table
        loading={loading}
        rowKey="id"
        dataSource={branches}
        columns={[
          {
            title: 'Mã CS',
            dataIndex: 'code',
            width: 100,
            render: v => <code>{v}</code>,
          },
          {
            title: 'Tên chi nhánh / Cơ sở',
            dataIndex: 'name',
            render: (v, r) => (
              <div>
                <strong>{v}</strong>
                {r.address && <div style={{ fontSize: 12, color: '#666' }}>{r.address}</div>}
              </div>
            ),
          },
          {
            title: 'Liên hệ',
            render: (_, r) => (
              <div style={{ fontSize: 13 }}>
                {r.phone && <div>SĐT: {r.phone}</div>}
                {r.email && <div>Email: {r.email}</div>}
              </div>
            ),
          },
          {
            title: 'Tài khoản chuyển khoản',
            render: (_, r) => (
              r.accountNumber ? (
                <div>
                  <Tag color="geekblue">{r.bankCode}</Tag>
                  <span>{r.accountNumber}</span>
                  {r.payeeName && <div style={{ fontSize: 11, color: '#888' }}>{r.payeeName}</div>}
                </div>
              ) : <Typography.Text type="secondary">Chưa cấu hình</Typography.Text>
            ),
          },
          {
            title: 'Số ghế',
            width: 100,
            align: 'center',
            render: (_, r) => <Tag color="blue">{r.chairCount} ghế</Tag>,
          },
          {
            title: 'Trạng thái',
            width: 130,
            render: (_, r) => (
              r.isActive ? (
                <Tag color="success">Hoạt động</Tag>
              ) : (
                <Tag color="default">Ngừng hoạt động</Tag>
              )
            ),
          },
          {
            title: 'Thao tác',
            width: 140,
            render: (_, r) => (
              <Space size="small">
                <Button size="small" onClick={() => void openBranchDetail(r)}>
                  Chi tiết
                </Button>
                {canManage && r.isActive && (
                  <Button size="small" danger onClick={() => void checkBranchState(r, false)}>
                    Ngừng
                  </Button>
                )}
              </Space>
            ),
          },
        ]}
      />

      {/* Drawer Branch Detail & Edit */}
      <Drawer
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        width={780}
        title={selectedBranch ? `Chi tiết: ${selectedBranch.name}` : 'Thêm chi nhánh mới'}
        extra={
          canManage && (
            <Button type="primary" onClick={() => branchForm.submit()} loading={savingBranch}>
              Lưu chi nhánh
            </Button>
          )
        }
      >
        {branchError && (
          <Alert
            type="error"
            showIcon
            message={branchError.code === 'MSG111' ? 'Xung đột phiên bản dữ liệu. Bản nhập của bạn đã được giữ nguyên; vui lòng kiểm tra lại.' : branchError.message}
            style={{ marginBottom: 16 }}
          />
        )}

        <Form form={branchForm} layout="vertical" onFinish={saveBranch} disabled={!canManage}>
          <Typography.Title level={5}>1. Thông tin chung cơ sở</Typography.Title>
          <Row gutter={12}>
            <Col span={8}>
              <Form.Item name="code" label="Mã chi nhánh" rules={[{ required: true, message: 'Nhập mã chi nhánh.' }]}>
                <Input placeholder="CS-01" />
              </Form.Item>
            </Col>
            <Col span={16}>
              <Form.Item name="name" label="Tên chi nhánh" rules={[{ required: true, message: 'Nhập tên chi nhánh.' }]}>
                <Input placeholder="Chi nhánh Cầu Giấy" />
              </Form.Item>
            </Col>
          </Row>

          <Form.Item name="address" label="Địa chỉ chi nhánh">
            <Input.TextArea rows={2} placeholder="Số 123 đường Cầu Giấy, Hà Nội" />
          </Form.Item>

          <Row gutter={12}>
            <Col span={8}>
              <Form.Item name="phone" label="Số điện thoại hotline">
                <Input placeholder="024 1234 5678" />
              </Form.Item>
            </Col>
            <Col span={8}>
              <Form.Item name="email" label="Email liên hệ">
                <Input placeholder="caugiay@odental.vn" />
              </Form.Item>
            </Col>
            <Col span={8}>
              <Form.Item name="website" label="Website / Fanpage">
                <Input placeholder="https://odental.vn" />
              </Form.Item>
            </Col>
          </Row>

          <Divider />
          <Typography.Title level={5}>2. Logo chi nhánh (Tải lên an toàn)</Typography.Title>
          <div style={{ marginBottom: 16, display: 'flex', alignItems: 'center', gap: 16 }}>
            {logoAssetId ? (
              <Image
                width={100}
                height={100}
                style={{ objectFit: 'contain', border: '1px solid #d9d9d9', borderRadius: 6 }}
                src={`/api/assets/${logoAssetId}`}
                fallback="data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><rect width='100' height='100' fill='%23eee'/><text x='50%' y='50%' dominant-baseline='middle' text-anchor='middle' fill='%23999'>Logo</text></svg>"
              />
            ) : (
              <div style={{ width: 100, height: 100, border: '1px dashed #d9d9d9', display: 'flex', alignItems: 'center', justifyContent: 'center', borderRadius: 6 }}>
                <Typography.Text type="secondary">Chưa có logo</Typography.Text>
              </div>
            )}
            <div>
              <Upload
                beforeUpload={handleLogoUpload}
                showUploadList={false}
                accept=".png,.jpg,.jpeg,.webp"
                disabled={!selectedBranch || !canManage}
              >
                <Button loading={logoUploading} disabled={!selectedBranch || !canManage}>
                  Tải lên logo (PNG/JPEG/WebP ≤ 2MB)
                </Button>
              </Upload>
              <div style={{ fontSize: 12, color: '#888', marginTop: 4 }}>
                Hệ thống tự động kiểm tra magic bytes, loại bỏ metadata và nghiêm cấm SVG/script để bảo mật.
              </div>
            </div>
          </div>

          <Divider />
          <Typography.Title level={5}>3. Tài khoản ngân hàng chuyển khoản công khai</Typography.Title>
          <Row gutter={12}>
            <Col span={8}>
              <Form.Item name="bankCode" label="Ngân hàng">
                <Input placeholder="VCB / MB / TCB..." />
              </Form.Item>
            </Col>
            <Col span={8}>
              <Form.Item name="accountNumber" label="Số tài khoản">
                <Input placeholder="0123456789" />
              </Form.Item>
            </Col>
            <Col span={8}>
              <Form.Item name="payeeName" label="Tên người thụ hưởng">
                <Input placeholder="PHONG KHAM NHA KHOA ODENTAL" />
              </Form.Item>
            </Col>
          </Row>
        </Form>

        {selectedBranch && (
          <>
            <Divider />
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
              <Typography.Title level={5} style={{ margin: 0 }}>
                4. Danh sách ghế điều trị vật lý ({selectedBranch.chairs?.length ?? 0} ghế)
              </Typography.Title>
              {canManage && (
                <Button size="small" type="primary" onClick={openChairCreate}>
                  + Thêm ghế
                </Button>
              )}
            </div>

            <Table
              size="small"
              pagination={false}
              rowKey="id"
              dataSource={selectedBranch.chairs}
              columns={[
                { title: 'Mã ghế', dataIndex: 'code', width: 90, render: v => <code>{v}</code> },
                { title: 'Tên ghế', dataIndex: 'name' },
                {
                  title: 'Giờ hoạt động',
                  render: (_, c) => {
                    const openH = Math.floor(c.openMinute / 60);
                    const openM = c.openMinute % 60;
                    const closeH = Math.floor(c.closeMinute / 60);
                    const closeM = c.closeMinute % 60;
                    return `${openH.toString().padStart(2, '0')}:${openM.toString().padStart(2, '0')} - ${closeH.toString().padStart(2, '0')}:${closeM.toString().padStart(2, '0')}`;
                  },
                },
                {
                  title: 'Trạng thái',
                  width: 120,
                  render: (_, c) => (
                    <Switch
                      size="small"
                      checked={c.isActive}
                      disabled={!canManage}
                      onChange={checked => void toggleChairState(c, checked)}
                    />
                  ),
                },
                ...(canManage
                  ? [
                      {
                        title: 'Thao tác',
                        width: 90,
                        render: (_: unknown, c: ChairDto) => (
                          <Button size="small" onClick={() => openChairEdit(c)}>
                            Sửa
                          </Button>
                        ),
                      },
                    ]
                  : []),
              ]}
            />
          </>
        )}
      </Drawer>

      {/* State Preview Modal (Deactivate Check with Blockers) */}
      <Modal
        open={previewModalOpen}
        onCancel={() => setPreviewModalOpen(false)}
        title="Kiểm tra tác động ngừng hoạt động chi nhánh (State-Preview)"
        footer={[
          <Button key="cancel" onClick={() => setPreviewModalOpen(false)}>
            Hủy bỏ
          </Button>,
          <Button
            key="confirm"
            danger
            type="primary"
            disabled={!impactPreview?.allowed}
            onClick={confirmDeactivateBranch}
          >
            Xác nhận ngừng chi nhánh
          </Button>,
        ]}
      >
        {previewLoading ? (
          <div style={{ textAlign: 'center', padding: 20 }}><Spin /></div>
        ) : impactPreview ? (
          <div>
            {impactPreview.allowed ? (
              <Alert
                type="success"
                showIcon
                message="An toàn để ngừng hoạt động"
                description={impactPreview.impact}
                style={{ marginBottom: 16 }}
              />
            ) : (
              <Alert
                type="error"
                showIcon
                message="Phát hiện nghĩa vụ ràng buộc ngăn chặn thao tác (409 MSG205)"
                description={impactPreview.impact}
                style={{ marginBottom: 16 }}
              />
            )}

            {impactPreview.blockers.length > 0 && (
              <div style={{ marginBottom: 16 }}>
                <Typography.Text strong>Chi tiết các nghĩa vụ cần xử lý trước khi ngừng:</Typography.Text>
                <div style={{ marginTop: 8 }}>
                  {impactPreview.blockers.map((b, idx) => (
                    <div
                      key={idx}
                      style={{
                        padding: 8,
                        backgroundColor: '#fff1f0',
                        border: '1px solid #ffccc7',
                        borderRadius: 6,
                        marginBottom: 6,
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'center',
                      }}
                    >
                      <div>
                        <Tag color="red">{b.kind}</Tag>
                        <span>{b.message}</span>
                      </div>
                      <a href={b.navigationUrl} target="_blank" rel="noreferrer">
                        Điều hướng xử lý
                      </a>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {impactPreview.allowed && (
              <Form.Item label="Lý do ngừng hoạt động chi nhánh">
                <Input.TextArea
                  rows={2}
                  value={deactivateReason}
                  onChange={e => setDeactivateReason(e.target.value)}
                  placeholder="Nhập lý do lâm sàng / quản trị..."
                />
              </Form.Item>
            )}
          </div>
        ) : null}
      </Modal>

      {/* Chair Edit/Create Modal */}
      <Modal
        open={chairModalOpen}
        onCancel={() => setChairModalOpen(false)}
        title={editingChair ? `Sửa ghế: ${editingChair.name}` : 'Thêm ghế điều trị mới'}
        onOk={() => chairForm.submit()}
        confirmLoading={savingChair}
      >
        <Form form={chairForm} layout="vertical" onFinish={saveChair}>
          <Form.Item name="code" label="Mã ghế" rules={[{ required: true, message: 'Nhập mã ghế.' }]}>
            <Input placeholder="GHE-01" />
          </Form.Item>
          <Form.Item name="name" label="Tên ghế điều trị" rules={[{ required: true, message: 'Nhập tên ghế.' }]}>
            <Input placeholder="Ghế 01 - Khoa Tổng quát" />
          </Form.Item>
          <Row gutter={12}>
            <Col span={12}>
              <Form.Item name="openMinute" label="Giờ mở (phút từ 00:00)">
                <Input type="number" placeholder="480 (08:00)" />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="closeMinute" label="Giờ đóng (phút từ 00:00)">
                <Input type="number" placeholder="1080 (18:00)" />
              </Form.Item>
            </Col>
          </Row>
        </Form>
      </Modal>
    </div>
  );
}
