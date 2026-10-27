import { Alert, Button, Empty, InputNumber, Modal, Select, Spin } from 'antd';
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { useEffect, useRef, useState } from 'react';
import { api } from '../../services/api';
import './materialNotePicker.css';

type Material = { id: string; name: string; categoryId: string; categoryName: string; version: number };
type MaterialPage = { items: Material[]; total: number; page: number; pageSize: number };
type Line = { key: string; material?: Material; quantity: string | null };
const label = (m: Material) => `${m.name} (${m.categoryName})`;
const blank = (): Line => ({ key: crypto.randomUUID(), quantity: '1' });

function MaterialSelect({ value, disabledIds, index, onChange }: {
  value?: Material; disabledIds: string[]; index: number; onChange: (m: Material) => void;
}) {
  const [query, setQuery] = useState('');
  const [open, setOpen] = useState(false);
  const [items, setItems] = useState<Material[]>([]), [page, setPage] = useState(0), [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false), [error, setError] = useState<string>(), [failedPage, setFailedPage] = useState(1);
  const generation = useRef(0), inFlight = useRef(false);
  async function load(nextPage: number, token: number) {
    if (inFlight.current || generation.current !== token) return;
    inFlight.current = true; setLoading(true); setError(undefined);
    try {
      const params = new URLSearchParams({q:query.trim(),page:String(nextPage),pageSize:'20'});
      const result = await api<MaterialPage>(`/api/system-settings/materials?${params}`);
      if (generation.current !== token) return;
      setItems(previous => [...new Map([...(nextPage === 1 ? [] : previous), ...result.items].map(m => [m.id,m])).values()]);
      setPage(result.page); setTotal(result.total);
    } catch (e) { if (generation.current === token) { setFailedPage(nextPage); setError((e as Error).message); } }
    finally { if (generation.current === token) { inFlight.current = false; setLoading(false); } }
  }
  useEffect(() => {
    const token = ++generation.current; inFlight.current = false;
    setItems([]); setPage(0); setTotal(0); setError(undefined); setLoading(true);
    const timer = window.setTimeout(() => void load(1,token), query ? 250 : 0);
    return () => { window.clearTimeout(timer); generation.current++; };
  }, [query]);
  const options = [...new Map([...items, ...(value ? [value] : [])].map(m => [m.id,m])).values()];
  const more = page > 0 && items.length < total;
  return <div className="material-note-select">
    <Select aria-label={`Tên dụng cụ/vật liệu ${index+1}`} value={value?.id} virtual={false} showSearch filterOption={false}
      placeholder="Tìm tên dụng cụ/vật liệu" loading={loading} onSearch={setQuery} onDropdownVisibleChange={setOpen}
      popupMatchSelectWidth={false} popupClassName="material-note-select-popup" dropdownStyle={{width:420,maxWidth:'calc(100vw - 32px)'}}
      options={options.map(m => ({value:m.id,label:label(m),title:label(m),disabled:disabledIds.includes(m.id)}))}
      onChange={id => { const m = options.find(o => o.id === id); if (m) onChange(m); setQuery(''); }}
      notFoundContent={loading ? <Spin size="small" /> : error ? 'Không tải được danh mục.' : <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Không có dụng cụ/vật liệu phù hợp." />}
      onPopupScroll={e => { const el = e.currentTarget; if (!loading && !error && more && el.scrollTop+el.clientHeight >= el.scrollHeight-24) void load(page+1,generation.current); }}
      dropdownRender={menu => <>{menu}{page > 0 && <div className="material-note-select-footer">
        {error ? <Alert type="error" message={error} action={<Button size="small" onMouseDown={e=>e.preventDefault()} onClick={()=>void load(failedPage,generation.current)}>Thử lại danh mục</Button>} /> : loading ? <Spin size="small" /> : more ? <Button type="link" size="small" onMouseDown={e=>e.preventDefault()} onClick={()=>void load(page+1,generation.current)}>Tải thêm vật liệu</Button> : null}
      </div>}{page === 0 && error && <Alert type="error" message={error} action={<Button size="small" onMouseDown={e=>e.preventDefault()} onClick={()=>void load(failedPage,generation.current)}>Thử lại danh mục</Button>} />}</>} />
    {error && !open && <Alert type="error" showIcon message={error} action={<Button size="small" onClick={()=>void load(failedPage,generation.current)}>Thử lại danh mục</Button>} />}
  </div>;
}

function quantityText(value: string | null) {
  if (!value || !/^\d+(?:\.\d{1,3})?$/.test(value) || !Number.isFinite(Number(value)) || Number(value) <= 0) return null;
  const [integer, fraction = ''] = value.split('.');
  const decimals = fraction.replace(/0+$/, '');
  return integer.replace(/^0+(?=\d)/, '') + (decimals ? `,${decimals}` : '');
}

export default function MaterialNotePicker({ note, onApply, onCancel }: {
  note: string; onApply: (note: string) => void; onCancel: () => void;
}) {
  const [lines, setLines] = useState<Line[]>(() => [blank()]);
  const [error, setError] = useState<string>(), [errors, setErrors] = useState<Record<string,string>>({});
  const applied = useRef(false);
  function update(key: string, patch: Partial<Line>) {
    setLines(values => values.map(v => v.key === key ? {...v,...patch} : v)); setError(undefined); setErrors({});
  }
  function submit() {
    if (applied.current) return;
    const failures: Record<string,string> = {}; const ids = new Set<string>();
    for (const row of lines) {
      if (!row.material) failures[row.key] = 'Vui lòng chọn dụng cụ/vật liệu.';
      else if (ids.has(row.material.id)) failures[row.key] = 'Vật liệu đã được chọn. Hãy sửa số lượng ở dòng hiện có.';
      if (row.material) ids.add(row.material.id);
      if (!quantityText(row.quantity)) failures[row.key] = 'Nhập số lượng dương, tối đa 3 chữ số thập phân.';
    }
    setErrors(failures);
    if (!lines.length) { setError('Vui lòng thêm ít nhất một vật liệu.'); return; }
    if (Object.keys(failures).length) return;
    const block = `Vật liệu dùng: ${lines.map(v => `${label(v.material!)} × ${quantityText(v.quantity)}`).join('; ')}.`;
    const combined = note ? `${note}\n\n${block}` : block;
    if (combined.length > 1000) { setError(`Ghi chú sau khi thêm có ${combined.length} ký tự, vượt giới hạn 1.000. Vui lòng giảm nội dung hoặc số vật liệu.`); return; }
    applied.current = true; onApply(combined);
  }
  return <Modal open title="Vật liệu dùng" width={760} className="material-note-modal" okText="Lưu" cancelText="Hủy"
    okButtonProps={{'aria-label':'Lưu vật liệu'}} onOk={submit} onCancel={onCancel} destroyOnClose>
    <p>Chọn dụng cụ/vật liệu và số lượng để bổ sung vào ghi chú.</p>
    {error && <Alert type="error" showIcon message={error} />}
    <div className="material-note-columns" aria-hidden="true"><span>Tên dụng cụ/vật liệu</span><span>Số lượng</span><span /></div>
    {lines.map((row,index) => <div key={row.key} className="material-note-line" aria-label={`Vật liệu ${index+1}`}>
      <MaterialSelect value={row.material} index={index} disabledIds={lines.filter(v=>v.key!==row.key&&v.material).map(v=>v.material!.id)} onChange={m=>update(row.key,{material:m})} />
      <InputNumber<string> stringMode aria-label={`Số lượng vật liệu ${index+1}`} value={row.quantity} step="1" decimalSeparator="," changeOnBlur={false}
        status={errors[row.key] ? 'error' : undefined} onChange={v=>update(row.key,{quantity:v})} />
      <Button type="text" danger icon={<DeleteOutlined />} aria-label={`Xóa dòng vật liệu ${index+1}`} onClick={()=>{setLines(v=>v.filter(x=>x.key!==row.key));setErrors({});setError(undefined);}} />
      {errors[row.key] && <small className="material-note-line-error" role="alert">{errors[row.key]}</small>}
    </div>)}
    <Button aria-label="Thêm vật liệu" icon={<PlusOutlined />} onClick={()=>{setLines(v=>[...v,blank()]);setError(undefined);}}>Thêm vật liệu</Button>
    <p className="material-note-help">Ghi chú hiện tại: {note.length}/1.000 ký tự. Bấm ✓ của hàng để lưu ghi chú vào phiếu.</p>
  </Modal>;
}
