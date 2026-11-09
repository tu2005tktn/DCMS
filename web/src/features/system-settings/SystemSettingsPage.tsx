import { Alert, Button, Card, Form, Input, Modal, Result, Select, Space, Table, Typography, Breadcrumb, Segmented } from 'antd';
import CommercialTreatmentPage from '../treatment/CommercialTreatmentPage';
import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { api } from '../../services/api';
import { useAuth } from '../authentication/AuthContext';
import GeneralSettingsPanel from './GeneralSettingsPanel';
import ReferenceCatalogPanel from './ReferenceCatalogPanel';
import SupplyCatalogPanel from './SupplyCatalogPanel';
import SettingsAuditPanel from './SettingsAuditPanel';
import { downloadExport } from './exportFile';
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
  const [search] = useSearchParams();
  const navigate = useNavigate();
  const [modal, modalContext] = Modal.useModal();

  const [capabilities, setCapabilities] = useState<SettingsCapabilities | null>(null);
  const [error, setError] = useState<string>();
  const [revision, setRevision] = useState(0);
  const [query, setQuery] = useState('');
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

    // 12. treatment-services (moved from the Chuyên môn menu)
    ...(has('treatment.prices.read') || has('treatment.commercial.read')
      ? [{ key: 'treatment-services', label: '12. Dịch vụ điều trị', children: <CommercialTreatmentPage /> }]
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

    // 12. settings history (DCMS-132)
    ...(capabilities.sections.some(x => x.key === 'audit') || has('settings.audit.read')
      ? [{ key: 'audit', label: '12. Lịch sử thiết lập', children: <SettingsAuditPanel /> }]
      : []),
  ];

  if (!tabs.length) {
    return <Result status="403" title="Bạn không có quyền thực hiện thao tác này (403 MSG06)." extra={<Link to="/">Về trang chủ</Link>} />;
  }

  // Current active section resolution: from route param :section, or ?tab= query param, or first available tab
  const requestedKey = section || search.get('tab');
  const current = tabs.find(x => x.key === requestedKey);

  function change(key: string) {
    const go = () => {
      dirty.current = false;
      navigate(key ? `/settings/${key}` : '/settings', { replace: !!key });
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

  const plain = (label: string) => label.replace(/^\d+\.\s*/, '');
  const groups = [
    { name: 'Phòng khám', keys: ['general', 'branches', 'customer-groups'] },
    { name: 'Danh mục', keys: ['diagnoses', 'services', 'treatment-services', 'medicines'] },
    { name: 'Vật liệu và labo', keys: ['material-suppliers', 'materials', 'lab-providers', 'lab-work-types'] },
    { name: 'Bảo mật', keys: ['access-groups'] },
  ].map(g => ({ ...g, items: g.keys.map(k => tabs.find(t => t.key === k)).filter((t): t is (typeof tabs)[number] => !!t) })).filter(g => g.items.length > 0);
  const fold = (v: string) => v.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').toLowerCase();
  const needle = fold(query.trim());
  const shown = groups.map(g => ({ ...g, items: g.items.filter(t => !needle || fold(plain(t.label)).includes(needle)) })).filter(g => g.items.length > 0);
  const group = current ? groups.find(g => g.items.some(t => t.key === current.key)) : undefined;

  return (
    <div className="system-settings-page">
      {modalContext}
      <Typography.Title level={2}>Thiết lập hệ thống</Typography.Title>
      {!current ? <>
        <Input.Search className="settings-search" allowClear aria-label="Tìm mục thiết lập" placeholder="Tìm mục thiết lập…" value={query} onChange={e => setQuery(e.target.value)} />
        {shown.length === 0 && <Typography.Paragraph type="secondary">Không có mục phù hợp.</Typography.Paragraph>}
        {shown.map(g => <section key={g.name} className="settings-group">
          <Typography.Title level={4}>{g.name}</Typography.Title>
          <div className="settings-card-grid">
            {g.items.map(t => <button key={t.key} type="button" className="settings-card" onClick={() => change(t.key)}>{plain(t.label)}</button>)}
          </div>
        </section>)}
      </> : <>
        <Button className="settings-back" aria-label="Quay lại thiết lập hệ thống" onClick={() => change('')}>← Quay lại</Button>
        <Breadcrumb className="settings-breadcrumb" items={[
          { title: <a onClick={() => change('')}>Thiết lập</a> },
          ...(group ? [{ title: group.name }] : []),
          { title: plain(current.label) },
        ]} />
        {group && group.items.length > 1 && <Segmented className="settings-switch" value={current.key} onChange={v => change(String(v))}
          options={group.items.map(t => ({ value: t.key, label: plain(t.label) }))} />}
        <div key={current.key}>{current.children}</div>
      </>}
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
  const [exportError, setExportError] = useState<string>();
  async function exportMaterials() {
    setExportError(undefined);
    const params = new URLSearchParams({ q: query }); if (categoryId) params.set('categoryId', categoryId);
    try { await downloadExport(`${base}/materials/export?${params}`, 'danh-muc-vat-lieu.xlsx'); }
    catch (e) { setExportError((e as Error).message); }
  }
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
    <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap', marginBottom: 16 }}>
      <Space wrap>
        <Button type={kind === 'materials' ? 'primary' : 'default'} onClick={() => tab('materials')}>Dụng cụ / Vật liệu</Button>
        <Button type={kind === 'material-categories' ? 'primary' : 'default'} onClick={() => tab('material-categories')}>Nhóm dụng cụ / vật liệu</Button>
      </Space>
      <Space wrap>
        <Input.Search allowClear maxLength={100} placeholder="Tìm theo tên..." value={search} style={{ width: 240 }}
          onChange={e => setSearch(e.target.value)} onSearch={v => { setQuery(v.trim()); setPage(1); }} />
        {kind === 'materials' && <Select allowClear placeholder="Lọc theo nhóm" style={{ width: 200 }} value={categoryId}
          onChange={v => { setCategoryId(v); setPage(1); }} options={categories?.map(c => ({ value: c.id, label: c.name }))} />}
        {canCreate && <Button type="primary" onClick={() => open()}>+ Thêm mới</Button>}
        {kind === 'materials' && user?.permissions.includes('catalog.materials.export') && <Button disabled={busy} onClick={() => void exportMaterials()}>Xuất Excel</Button>}
      </Space>
    </div>
    {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} />}
    {exportError && <Alert type="error" showIcon closable message={exportError} onClose={() => setExportError(undefined)} style={{ marginBottom: 16 }} />}
    {categoryError && <Alert type="warning" showIcon message={`Không tải được nhóm vật liệu: ${categoryError}`} style={{ marginBottom: 16 }} />}
    <Table
      loading={loading}
      dataSource={data?.items}
      rowKey="id"
      pagination={{ current: page, pageSize: size, total: data?.total ?? 0, showSizeChanger: true, pageSizeOptions: [20, 50, 100],
        onChange: (p, s) => { setPage(s !== size ? 1 : p); setSize(s); } }}
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
    <Modal open={deleting !== null} title={`Ngừng dùng "${deleting?.row.name ?? ''}"?`} okText="Ngừng dùng" okButtonProps={{ danger: true }}
      confirmLoading={busy} onOk={() => void remove()} onCancel={() => { if (!busy) { setDeleting(null); setDeleteError(undefined); } }}>
      {deleteError && <Alert type="error" showIcon message={deleteError} style={{ marginBottom: 12 }} />}
      <Typography.Paragraph>Mục sẽ không còn được chọn cho giao dịch mới; dữ liệu đã ghi nhận vẫn giữ nguyên.</Typography.Paragraph>
    </Modal>
  </div>;
}
