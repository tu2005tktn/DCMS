import { Alert, Button, Card, Empty, Form, Input, Modal, Result, Select, Space, Table, Tabs, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { api, ApiError } from '../../services/api';
import { useAuth } from '../authentication/AuthContext';
import GeneralSettingsPanel from './GeneralSettingsPanel';
import ReferenceCatalogPanel from './ReferenceCatalogPanel';
import SupplyCatalogPanel from './SupplyCatalogPanel';
import AccessGroupsPanel from './AccessGroupsPanel';
import BranchesPanel from './BranchesPanel';
import ClinicalServicesPage from '../finance/ClinicalServicesPage';
import type { SettingsCapabilities } from './generalSettings';
import './systemSettings.css';

type Kind = 'materials' | 'material-categories';
type Row = { id: string; name: string; version: number; categoryId?: string; categoryName?: string; itemCount?: number; usageUnit?: string | null };
type Page = { items: Row[]; total: number; page: number; pageSize: number };
type Values = { name: string; categoryId?: string; usageUnit?: string };
const base = '/api/system-settings';

async function allCategories() {
  const result: Row[] = [];
  for (let page = 1; ; page++) {
    const batch = await api<Page>(`${base}/material-categories?page=${page}&pageSize=100`);
    result.push(...batch.items);
    if (!batch.items.length || result.length >= batch.total) return result;
  }
}

// S58 shell (DCMS-120): 11 standard groups in exact order, deep link /settings/{section}.
export default function SystemSettingsPage() {
  const { user } = useAuth();
  const { section } = useParams<{ section?: string }>();
  const [search, setSearch] = useSearchParams();
  const navigate = useNavigate();
  const [modal, modalContext] = Modal.useModal();

  const [capabilities, setCapabilities] = useState<SettingsCapabilities | null>(null);
  const [error, setError] = useState<string>();
  const [revision, setRevision] = useState(0);
  const dirty = useRef(false);

  const owner = user?.effectiveRoles.includes('ClinicOwner') ?? false;
  const has = (p: string) => owner || (user?.permissions.includes(p) ?? false);

  useEffect(() => {
    let live = true;
    setError(undefined);
    void api<SettingsCapabilities>('/api/system-settings/settings/capabilities')
      .then(v => { if (live) setCapabilities(v); })
      .catch(e => { if (live) setError((e as Error).message); });
    return () => { live = false; };
  }, [revision]);

  if (error) return <Alert type="error" showIcon message={error} action={<Button onClick={() => setRevision(v => v + 1)}>Thử lại</Button>} />;
  if (!capabilities) return <Card loading />;

  // 11 standard groups in exact order
  const tabs = [
    // 1. general
    ...(capabilities.sections.some(x => x.key === 'general') || has('settings.general.read')
      ? [{ key: 'general', label: '1. Thiết lập chung', children: <GeneralSettingsPanel onDirtyChange={v => { dirty.current = v; }} /> }]
      : []),

    // 2. diagnoses
    ...(has('catalog.diagnoses.read') || has('settings.read')
      ? [{ key: 'diagnoses', label: '2. Chẩn đoán / Tên bệnh', children: <ReferenceCatalogPanel initialKind="diagnoses" /> }]
      : []),

    // 3. services
    ...(has('services.read')
      ? [{ key: 'services', label: '3. Dịch vụ, sản phẩm & giá', children: <ClinicalServicesPage /> }]
      : []),

    // 4. material-suppliers
    ...(has('catalog.material-suppliers.read')
      ? [{ key: 'material-suppliers', label: '4. Công ty cung cấp vật liệu', children: <SupplyCatalogPanel initialKey="suppliers" /> }]
      : []),

    // 5. materials
    ...(has('catalog.materials.read')
      ? [{ key: 'materials', label: '5. Loại dụng cụ / Vật liệu', children: <MaterialCatalog /> }]
      : []),

    // 6. lab-providers
    ...(has('catalog.lab-work-types.read') || has('catalog.material-suppliers.read')
      ? [{ key: 'lab-providers', label: '6. Công ty labo', children: <SupplyCatalogPanel initialKey="labs" /> }]
      : []),

    // 7. lab-work-types
    ...(has('catalog.lab-work-types.read')
      ? [{ key: 'lab-work-types', label: '7. Loại công việc labo', children: <SupplyCatalogPanel initialKey="lab-work-types" /> }]
      : []),

    // 8. customer-groups
    ...(has('catalog.customer-groups.read') || owner
      ? [{ key: 'customer-groups', label: '8. Nhóm khách hàng', children: <ReferenceCatalogPanel initialKind="customer-groups" /> }]
      : []),

    // 9. medicines
    ...(has('catalog.medicines.read')
      ? [{ key: 'medicines', label: '9. Thuốc / Dược phẩm', children: <SupplyCatalogPanel initialKey="medicines" /> }]
      : []),

    // 10. access-groups
    ...(user?.permissions.includes('access-groups.read') || owner
      ? [{ key: 'access-groups', label: '10. Phân quyền truy cập', children: <AccessGroupsPanel /> }]
      : []),

    // 11. branches
    ...(has('branches.read') || owner
      ? [{ key: 'branches', label: '11. Chi nhánh & Ghế điều trị', children: <BranchesPanel /> }]
      : []),
  ];

  if (!tabs.length) {
    return <Result status="403" title="Bạn không có quyền thực hiện thao tác này (403 MSG06)." extra={<Link to="/">Về trang chủ</Link>} />;
  }

  // Current active section resolution: from route param :section, or ?tab= query param, or first available tab
  const requestedKey = section || search.get('tab');
  const active = tabs.some(x => x.key === requestedKey) ? requestedKey! : tabs[0].key;

  function change(key: string) {
    const go = () => {
      dirty.current = false;
      navigate(`/settings/${key}`, { replace: true });
    };
    if (!dirty.current) {
      go();
      return;
    }
    modal.confirm({
      title: 'Bỏ thay đổi chưa lưu?',
      content: 'Thiết lập hoặc bản nhập vừa sửa sẽ không được lưu nếu bạn chuyển mục.',
      okText: 'Bỏ thay đổi',
      cancelText: 'Tiếp tục sửa',
      onOk: go,
    });
  }

  return (
    <div className="system-settings-page">
      {modalContext}
      <Typography.Title level={2}>Thiết lập hệ thống (oDental System Settings)</Typography.Title>
      <Tabs activeKey={active} onChange={change} destroyInactiveTabPane items={tabs} />
    </div>
  );
}

function MaterialCatalog() {
  const { user } = useAuth();
  const allowed = user?.accountType === 'Staff' && user.permissions.includes('catalog.materials.read');
  const canCreate = user?.permissions.includes('catalog.materials.create'), canUpdate = user?.permissions.includes('catalog.materials.update'), canDelete = user?.permissions.includes('catalog.materials.deactivate');
  const [kind, setKind] = useState<Kind>('materials'), [query, setQuery] = useState(''), [search, setSearch] = useState('');
  const [categoryId, setCategoryId] = useState<string>(), [page, setPage] = useState(1), [size, setSize] = useState(20);
  const [data, setData] = useState<Page | null>(null), [loading, setLoading] = useState(false), [error, setError] = useState<string>();
  const [categories, setCategories] = useState<Row[] | null>(null), [categoryError, setCategoryError] = useState<string>();
  const [revision, setRevision] = useState(0);
  const [dialog, setDialog] = useState<{ kind: Kind; row?: Row } | null>(null), [deleting, setDeleting] = useState<{kind: Kind; row: Row} | null>(null);
  const [formError, setFormError] = useState<string>(), [deleteError, setDeleteError] = useState<string>();
  const [busy, setBusy] = useState(false), guard = useRef(false);
  const pending = useRef<{ body: string; key: string } | null>(null), deletePending = useRef<{body: string; key: string} | null>(null);
  const [form] = Form.useForm<Values>();
  useEffect(() => {
    if (!allowed) return;
    let live = true; setLoading(true); setError(undefined);
    const params = new URLSearchParams({ q: query, page: String(page), pageSize: String(size) });
    if (kind === 'materials' && categoryId) params.set('categoryId', categoryId);
    void api<Page>(`${base}/${kind}?${params}`).then(v => {
      if (!live) return;
      if (!v.items.length && v.total && page > 1) { setPage(Math.max(1, Math.ceil(v.total/size))); return; }
      setData(v);
    }).catch(e => { if (live) { setError(e.message); setData(null); } }).finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, [allowed, kind, query, categoryId, page, size, revision]);
  useEffect(() => {
    if (!allowed) return;
    let live = true; setCategoryError(undefined);
    void allCategories().then(v => { if (live) setCategories(v); }).catch(e => { if (live) { setCategoryError(e.message); setCategories(null); } });
    return () => { live = false; };
  }, [allowed, revision]);
  function tab(value: string) { setKind(value as Kind); setPage(1); setQuery(''); setSearch(''); setCategoryId(undefined); setData(null); }
  function open(row?: Row) {
    pending.current = null; setFormError(undefined); form.resetFields();
    form.setFieldsValue({ name: row?.name ?? '', categoryId: row?.categoryId, usageUnit: row?.usageUnit ?? undefined }); setDialog({kind, row});
  }
  async function save(values: Values) {
    if (guard.current) return;
    const body = JSON.stringify(values);
    if (pending.current?.body !== body) pending.current = { body, key: crypto.randomUUID() };
    guard.current = true; setBusy(true); setFormError(undefined);
    const isEdit = dialog?.row !== undefined;
    const path = isEdit ? `${base}/${dialog!.kind}/${dialog!.row!.id}` : `${base}/${dialog!.kind}`;
    try {
      await api(path, { method: isEdit ? 'PUT' : 'POST', body: JSON.stringify({ ...values, requestKey: pending.current.key, expectedVersion: dialog?.row?.version ?? 0 }) });
      setDialog(null); setRevision(v => v + 1);
    } catch (e) { setFormError((e as Error).message); }
    finally { guard.current = false; setBusy(false); }
  }
  async function remove() {
    if (!deleting || guard.current) return;
    const body = JSON.stringify({ reason: 'Xóa danh mục' });
    if (deletePending.current?.body !== body) deletePending.current = { body, key: crypto.randomUUID() };
    guard.current = true; setBusy(true); setDeleteError(undefined);
    try {
      await api(`${base}/${deleting.kind}/${deleting.row.id}`, { method: 'DELETE', body: JSON.stringify({ requestKey: deletePending.current.key, expectedVersion: deleting.row.version }) });
      setDeleting(null); setRevision(v => v + 1);
    } catch (e) { setDeleteError((e as Error).message); }
    finally { guard.current = false; setBusy(false); }
  }
  if (!allowed) return <Result status="403" title="Bạn không có quyền xem danh mục này." />;
  return <div>
    <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
      <Space>
        <Button type={kind === 'materials' ? 'primary' : 'default'} onClick={() => tab('materials')}>Dụng cụ / Vật liệu</Button>
        <Button type={kind === 'material-categories' ? 'primary' : 'default'} onClick={() => tab('material-categories')}>Nhóm dụng cụ / vật liệu</Button>
      </Space>
      {canCreate && <Button type="primary" onClick={() => open()}>+ Thêm mới</Button>}
    </div>
    {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} />}
    <Table
      loading={loading}
      dataSource={data?.items}
      rowKey="id"
      columns={[
        { title: 'Tên', dataIndex: 'name' },
        ...(kind === 'materials' ? [{ title: 'Nhóm', dataIndex: 'categoryName' }, { title: 'Đơn vị sử dụng', dataIndex: 'usageUnit' }] : []),
        ...(canUpdate || canDelete ? [{
          title: 'Thao tác',
          render: (_: any, r: Row) => <Space>
            {canUpdate && <Button size="small" onClick={() => open(r)}>Sửa</Button>}
            {canDelete && <Button size="small" danger onClick={() => setDeleting({ kind, row: r })}>Ngừng dùng</Button>}
          </Space>
        }] : [])
      ]}
    />
    <Modal open={dialog !== null} title={dialog?.row ? 'Sửa' : 'Thêm mới'} onCancel={() => setDialog(null)} onOk={() => form.submit()} confirmLoading={busy}>
      {formError && <Alert type="error" showIcon message={formError} style={{ marginBottom: 12 }} />}
      <Form form={form} layout="vertical" onFinish={save}>
        <Form.Item name="name" label="Tên" rules={[{ required: true, message: 'Vui lòng nhập tên.' }]}><Input /></Form.Item>
        {dialog?.kind === 'materials' && (
          <>
            <Form.Item name="categoryId" label="Nhóm"><Select allowClear options={categories?.map(c => ({ value: c.id, label: c.name }))} /></Form.Item>
            <Form.Item name="usageUnit" label="Đơn vị sử dụng"><Input placeholder="cái, hộp, ống..." /></Form.Item>
          </>
        )}
      </Form>
    </Modal>
  </div>;
}
