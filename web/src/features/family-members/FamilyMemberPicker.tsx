import { Alert, Button, Select, Spin } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { familyDate, relationshipLabel, useFamilyApi, type FamilyMember, type FamilyPage } from './types';
export default function FamilyMemberPicker({ selected, onChange }: {
  selected: FamilyMember | null; onChange: (member: FamilyMember | null) => void;
}) {
  const request = useFamilyApi();
  const loadingPage = useRef(true);
  const [search, setSearch] = useState(''), [keyword, setKeyword] = useState(''), [page, setPage] = useState(1);
  const [items, setItems] = useState<FamilyMember[]>([]), [total, setTotal] = useState(0), [busy, setBusy] = useState(false), [error, setError] = useState(''), [revision, setRevision] = useState(0);
  useEffect(() => { if (search === keyword) return; const timer = setTimeout(() => { setKeyword(search); setPage(1); }, 300); return () => clearTimeout(timer); }, [search, keyword]);
  useEffect(() => {
    let active = true; const controller = new AbortController(); loadingPage.current = true; setBusy(true); setError('');
    if (page === 1) setItems([]);
    void request<FamilyPage>(`?${new URLSearchParams({ keyword, page: String(page), pageSize: '20' })}`, { signal: controller.signal })
      .then(result => { if (active) { setTotal(result.total); setItems(previous => page === 1 ? result.items : [...previous, ...result.items.filter(x => !previous.some(p => p.id === x.id))]); } })
      .catch(failure => { if (active) setError((failure as Error).message); }).finally(() => { if (active) { loadingPage.current = false; setBusy(false); } });
    return () => { active = false; controller.abort(); };
  }, [keyword, page, revision, request]);
  const options = selected && !items.some(x => x.id === selected.id) ? [selected, ...items] : items;
  return <div className="customer-family-picker">
    <label htmlFor="booking-family-member">Người thân đã lưu</label>
    <Select id="booking-family-member" showSearch filterOption={false} value={selected?.id} placeholder="Chọn người thân đã lưu" onSearch={setSearch} loading={busy}
      onChange={id => { onChange(options.find(x => x.id === id) ?? null); setSearch(''); }}
      options={options.map(x => ({ value: x.id, label: `${x.fullName} · ${relationshipLabel(x.relationship)}${x.birthDate ? ` · ${familyDate(x.birthDate)}` : ''}` }))}
      onPopupScroll={event => { const element = event.currentTarget; if (!loadingPage.current && search === keyword && !busy && !error && items.length < total && element.scrollTop + element.clientHeight >= element.scrollHeight - 20) { loadingPage.current = true; setPage(p => p + 1); } }}
      notFoundContent={busy ? <Spin size="small" /> : 'Không có người thân phù hợp'} />
    <p className="customer-form-note">Danh sách được quản lý tại mục Người thân của tôi.</p>
    {error && <Alert type="error" showIcon message={error} action={<Button onClick={() => setRevision(r => r + 1)}>Thử lại</Button>} />}
  </div>;
}
