import { Alert, Input, Button, Radio, Select, Space, Spin, Typography } from 'antd';
import { Component, lazy, Suspense, useState, type ReactNode } from 'react';
import { findingText, type Finding } from './types';
import { surfaceOptions } from './odontogram';
import type { ReferenceSnapshot } from './diagnosisReferences';
import type { DiagnosisOption } from './useDiagnosisOptions';
const ToothModel = lazy(() => import('./ToothModel'));
class ModelBoundary extends Component<{ children: ReactNode; fallback: () => void }, { failed: boolean }> {
  state = { failed: false };
  static getDerivedStateFromError() { return { failed: true }; }
  render() { return this.state.failed ? <Alert type="warning" message="Không tải được mô hình 3D." action={<Button disabled={false} onClick={this.props.fallback}>Về sơ đồ 2D</Button>} /> : this.props.children; }
}
type Dentition = 'permanent' | 'primary' | 'mixed';
const permanentRows = [[18,17,16,15,14,13,12,11,21,22,23,24,25,26,27,28], [48,47,46,45,44,43,42,41,31,32,33,34,35,36,37,38]];
const primaryRows = [[55,54,53,52,51,61,62,63,64,65], [85,84,83,82,81,71,72,73,74,75]];
function initialDentition(findings: Finding[]): Dentition {
  const teeth = findings.flatMap(f => f.tooth ? [f.tooth] : []);
  return teeth.some(t => t >= 51) ? teeth.some(t => t < 51) ? 'mixed' : 'primary' : 'permanent';
}
function ToothIllustration({ tooth, lower }: { tooth: number; lower: boolean }) {
  const molar = tooth % 10 >= 4;
  return <svg viewBox="0 0 50 72" aria-hidden="true" className={lower ? 'tooth-svg' : 'tooth-svg upper'}>
    <path className="tooth-root" d={molar ? 'M10 33 Q9 61 17 67 L24 45 L32 67 Q41 59 40 33Z' : 'M15 32 Q15 52 25 68 Q36 51 35 32Z'} />
    <path className="tooth-crown" d={molar ? 'M9 10 Q16 3 25 8 Q36 3 41 11 L40 34 Q34 42 25 36 Q15 43 10 34Z' : 'M14 10 Q25 3 36 10 L38 32 Q25 41 12 32Z'} />
    <path className="tooth-groove" d={molar ? 'M16 17 L25 22 L34 17 M25 22 L25 31' : 'M18 16 Q25 12 32 16'} />
  </svg>;
}
export default function ToothChart({ findings, disabled, onApply, diagnosisOptions }: {
  findings: Finding[]; disabled: boolean; diagnosisOptions: DiagnosisOption[];
  onApply: (teeth: number[], condition: string, surfaces: string[], diagnoses: string[], references: ReferenceSnapshot[]) => void;
}) {
  const [dentition, setDentition] = useState<Dentition>(() => initialDentition(findings));
  const [view, setView] = useState('2d');
  const [labelRequest, setLabelRequest] = useState<{ tooth: number; sequence: number }>();
  const [selected, setSelected] = useState<number[]>([]); const [condition, setCondition] = useState('');
  const [diagnoses, setDiagnoses] = useState<string[]>([]);
  const [references,setReferences] = useState<ReferenceSnapshot[]>([]);
  const [surfaces, setSurfaces] = useState<string[]>([]);
  const recorded = [...new Set(findings.filter(f => findingText(f).trim()).flatMap(f => f.tooth ? [f.tooth] : []))];
  const hiddenFindings = findings.filter(f => f.tooth && dentition !== 'mixed' && (dentition === 'primary') !== (f.tooth >= 51)).length;
  const groups = [{ key: 'permanent', label: 'Răng vĩnh viễn', rows: permanentRows }, { key: 'primary', label: 'Răng sữa', rows: primaryRows }].filter(g => dentition === 'mixed' || g.key === dentition);
  function select(tooth: number) {
    if (disabled) return;
    setSelected(old => old.includes(tooth) ? old.filter(t => t !== tooth) : [...old, tooth]);
  }
  return <section aria-label="Sơ đồ răng" className="tooth-chart-section">
    <Typography.Title level={4}>Sơ đồ răng (FDI)</Typography.Title>
    <Space wrap className="odontogram-toolbar">
      <Radio.Group aria-label="Bộ răng hiển thị" value={dentition} disabled={false} onChange={e => setDentition(e.target.value)}>
        <Radio.Button value="permanent">Răng vĩnh viễn</Radio.Button><Radio.Button value="primary">Răng sữa</Radio.Button><Radio.Button value="mixed">Răng hỗn hợp</Radio.Button>
      </Radio.Group>
      <Radio.Group aria-label="Chế độ sơ đồ" value={view} disabled={false} onChange={e => { setView(e.target.value); setLabelRequest(undefined); }}>
        <Radio.Button value="2d">Sơ đồ 2D</Radio.Button><Radio.Button value="3d">Mô hình 3D</Radio.Button>
      </Radio.Group>
    </Space>
    <p>Bấm răng để chọn thêm, bấm lại để bỏ chọn.</p>
    <p className="tooth-legend"><span>○ Chưa có ghi nhận</span><span>● Có kết quả</span><span>◉ Đang chọn</span> · Chưa ghi nhận không có nghĩa là bình thường.</p>
    {hiddenFindings > 0 && <Alert type="info" message={`Có ${hiddenFindings} kết quả thuộc bộ răng đang ẩn. Nội dung vẫn được giữ trong danh sách bên dưới.`} action={<Button disabled={false} onClick={() => setDentition('mixed')}>Xem cả hai bộ răng</Button>} />}
    {view === '3d' && <ModelBoundary fallback={() => setView('2d')}><Suspense fallback={<Spin tip="Đang tải mô hình 3D" />}><ToothModel rows={groups.flatMap(g => g.rows)} selected={selected} recorded={recorded} disabled={disabled} onSelect={select} labelRequest={labelRequest} onFallback={() => setView('2d')} /></Suspense></ModelBoundary>}
    <div className={view === '3d' ? 'tooth-fdi-picker' : ''}>
      {view === '3d' && <p>Chọn theo số FDI (hỗ trợ bàn phím)</p>}
      {groups.map(group => <section key={group.key} aria-label={`Sơ đồ ${group.label.toLowerCase()}`} className="tooth-chart-group">
        <Typography.Title level={5}>{group.label} · {group.rows.flat().length} vị trí</Typography.Title>
        <div className="tooth-orientation"><span>Phải của bệnh nhân</span><span>Trái của bệnh nhân</span></div>
        <div className="tooth-chart">{group.rows.map((row, i) => <div key={i} className="tooth-jaw">
          <Typography.Text type="secondary">{i === 0 ? 'Hàm trên' : 'Hàm dưới'}</Typography.Text>
          <div className={`tooth-row${group.key === 'primary' ? ' tooth-row-primary' : ''}`}>
            {row.map(tooth => <button type="button" key={tooth} aria-label={`Răng ${tooth}`} aria-pressed={selected.includes(tooth)} title={`Răng ${tooth} · ${findings.filter(f => f.tooth === tooth && findingText(f).trim()).map(f => findingText(f)).join('; ') || 'Chưa có ghi nhận'}`} disabled={disabled} className={`tooth-button ${recorded.includes(tooth) ? 'recorded' : ''} ${selected.includes(tooth) ? 'picked' : ''}`} onClick={() => { select(tooth); if (view === '3d') setLabelRequest(old => ({ tooth, sequence: (old?.sequence ?? 0) + 1 })); }}>
              {view === '2d' && <ToothIllustration tooth={tooth} lower={i === 1} />}<span>{tooth}</span><small>{recorded.includes(tooth) ? '●' : '○'}</small>
            </button>)}
          </div>
        </div>)}</div>
      </section>)}
    </div>
    {!disabled && <div className="tooth-quick-entry">
      <Space wrap><span>Đang chọn: {selected.length ? selected.join(', ') : 'Chưa chọn răng'}</span><Button disabled={!selected.length} onClick={() => setSelected([])}>Bỏ chọn</Button></Space>
      <label htmlFor="quick-tooth-diagnoses">Chẩn đoán cho răng đang chọn</label>
      <Select id="quick-tooth-diagnoses" aria-label="Chẩn đoán cho răng đang chọn" mode="tags" value={diagnoses} options={diagnosisOptions.filter(o => o.value !== 'Chưa phát hiện bất thường')} onChange={values=>{setDiagnoses(values);setReferences(old=>old.filter(ref=>values.includes(ref.name)));}} onSelect={(_,option)=>{if(option.reference)setReferences(old=>[...old.filter(ref=>ref.id!==option.reference!.id||ref.name!==option.reference!.name),option.reference!]);}} placeholder="Chọn một hoặc nhiều chẩn đoán; không cần nhập lại ở Tổng kết" style={{ width: '100%' }} />
      <label htmlFor="quick-tooth-condition">Mô tả phát hiện / ghi chú bổ sung (nếu có)</label>
      <Input.TextArea id="quick-tooth-condition" aria-label="Ghi nhanh kết quả" value={condition} maxLength={2000} onChange={event => setCondition(event.target.value)} placeholder="Ghi khi cần bổ sung mô tả cho chẩn đoán đã chọn" />
      <Select aria-label="Bề mặt răng ghi nhanh" mode="multiple" placeholder="Bề mặt răng (nếu cần)" value={surfaces} options={surfaceOptions} onChange={setSurfaces} />
      <Button type="primary" disabled={!selected.length || (!condition.trim() && !diagnoses.length)} onClick={() => { onApply(selected, condition.trim(), surfaces, diagnoses, references); setCondition(''); setDiagnoses([]); setReferences([]); }}>Áp dụng cho {selected.length} răng</Button>
    </div>}
  </section>;
}
