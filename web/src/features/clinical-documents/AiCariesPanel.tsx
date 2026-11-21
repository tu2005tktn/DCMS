import { Alert, Button, Empty, Select, Skeleton, Slider, Space, Table, Tag, Typography } from 'antd';
import { useEffect, useMemo, useState } from 'react';
import { ApiError } from '../../services/api';
import { usePermissions } from '../authentication/usePermissions';
import { documentsApi } from './documents.api';
import { formatStamp, newRequestKey, type AiAnalysis, type AiFinding, type DocumentItem } from './types';
import './aiCariesPanel.css';

const kindLabels: Record<string, string> = { caries: 'Nghi sâu răng', impacted_third_molar: 'Răng khôn ngầm' };
const percent = (n: number) => `${Math.round(n * 100)}%`;

export const supportsAiAnalysis = (d: DocumentItem) =>
  d.documentType === 'XRay' && d.fileState === 'Ready' && (d.contentType === 'image/jpeg' || d.contentType === 'image/png');

// AI decision support for one X-ray (docs/specs/AI-CARIES-DETECTION.vi.md §7). Boxes are drawn in original image pixels via an SVG
// viewBox, so they stay aligned at any display size.
export default function AiCariesPanel({ doc, onAnalyzed }: { doc: DocumentItem; onAnalyzed: (a: AiAnalysis) => void }) {
  const has = usePermissions();
  const analyses = useMemo(() => doc.aiAnalyses ?? [], [doc.aiAnalyses]);
  const [selectedId, setSelectedId] = useState<string | null>(analyses[0]?.analysisId ?? null);
  const [imageUrl, setImageUrl] = useState<string | null>(null);
  const [imageError, setImageError] = useState<string | null>(null);
  const [running, setRunning] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [minConfidence, setMinConfidence] = useState(0);
  const [hovered, setHovered] = useState<string | null>(null);

  useEffect(() => {
    let url: string | null = null; let active = true;
    documentsApi.content(doc.documentId, 'inline')
      .then(blob => { if (active) { url = URL.createObjectURL(blob); setImageUrl(url); } })
      .catch(e => { if (active) setImageError((e as ApiError).message); });
    return () => { active = false; if (url) URL.revokeObjectURL(url); };
  }, [doc.documentId]);

  useEffect(() => { if (!analyses.some(a => a.analysisId === selectedId)) setSelectedId(analyses[0]?.analysisId ?? null); }, [analyses, selectedId]);

  const analysis = analyses.find(a => a.analysisId === selectedId) ?? null;
  const findings = (analysis?.findings ?? []).filter(f => f.confidence >= minConfidence);
  const canRun = has('documents.review');

  async function run() {
    setRunning(true); setError(null);
    try { const a = await documentsApi.analyze(doc.documentId, newRequestKey()); onAnalyzed(a); setSelectedId(a.analysisId); }
    catch (e) { setError((e as ApiError).message); }
    finally { setRunning(false); }
  }

  return <div className="ai-caries">
    <Alert type="warning" showIcon className="form-alert"
      message="Kết quả AI chỉ mang tính gợi ý hỗ trợ, không thay thế chẩn đoán của bác sĩ."
      description="Mô hình được huấn luyện trên phim toàn cảnh (Panorama/OPG); với phim cận chóp hoặc cánh cắn, kết quả có thể không chính xác." />
    <Space wrap className="ai-caries-toolbar">
      {canRun && <Button type="primary" loading={running} onClick={() => void run()}>{analyses.length ? 'Phân tích lại' : 'Phân tích AI'}</Button>}
      {analyses.length > 1 && <Select style={{ minWidth: 260 }} value={selectedId ?? undefined} onChange={setSelectedId}
        options={analyses.map(a => ({ value: a.analysisId, label: `${formatStamp(a.requestedAt)} · ${a.findings.length} vùng` }))} />}
      {analysis && <Space size="small"><Typography.Text type="secondary">Ngưỡng hiển thị</Typography.Text>
        <Slider style={{ width: 140 }} min={0} max={0.9} step={0.05} value={minConfidence} onChange={setMinConfidence}
          tooltip={{ formatter: v => percent(v ?? 0) }} /></Space>}
    </Space>
    {running && <Alert type="info" showIcon className="form-alert" message="Đang phân tích phim, có thể mất vài giây đến vài chục giây…" />}
    {error && <Alert type="error" showIcon className="form-alert" message={error} />}
    {!analysis && !running && <Empty description={canRun ? 'Chưa có kết quả phân tích AI cho phim này.' : 'Chưa có kết quả phân tích AI. Bác sĩ có thể chạy phân tích.'} />}

    {imageError ? <Alert type="error" showIcon message={imageError} /> : !imageUrl ? <Skeleton.Image active /> :
      <div className="ai-caries-figure">
        <img src={imageUrl} alt={doc.fileName} />
        {analysis && <svg viewBox={`0 0 ${analysis.imageWidth} ${analysis.imageHeight}`} preserveAspectRatio="none" aria-hidden>
          {findings.map(f => <Box key={f.findingId} f={f} active={hovered === f.findingId} scale={analysis.imageWidth / 1000} />)}
        </svg>}
      </div>}

    {analysis && <>
      {analysis.findings.length === 0
        ? <Alert type="success" showIcon className="form-alert" message="AI không phát hiện vùng nghi sâu răng." description="Bác sĩ vẫn cần tự đánh giá phim." />
        : <Table<AiFinding> size="small" rowKey="findingId" pagination={false} dataSource={findings} className="patient-section"
            onRow={f => ({ onMouseEnter: () => setHovered(f.findingId), onMouseLeave: () => setHovered(null) })}
            columns={[
              { title: 'Vùng', dataIndex: 'findingId', width: 70 },
              { title: 'Loại', dataIndex: 'kind', render: k => <Tag color={k === 'caries' ? 'red' : 'blue'}>{kindLabels[k] ?? k}</Tag> },
              { title: 'Độ tin cậy', dataIndex: 'confidence', width: 110, render: c => percent(c) },
            ]} />}
      <Typography.Paragraph type="secondary" className="patient-section">
        {analysis.requestedByName} · {formatStamp(analysis.requestedAt)} · model {analysis.modelName} · {(analysis.elapsedMs / 1000).toFixed(1)} giây ({analysis.device}).
        Vị trí là gợi ý thô theo nửa phim, không phải số răng.
      </Typography.Paragraph>
    </>}
  </div>;
}

function Box({ f, active, scale }: { f: AiFinding; active: boolean; scale: number }) {
  const stroke = Math.max(2, 2.5 * scale);
  return <g className={active ? 'ai-box active' : 'ai-box'} data-kind={f.kind}>
    <rect x={f.x1} y={f.y1} width={f.x2 - f.x1} height={f.y2 - f.y1} strokeWidth={active ? stroke * 2 : stroke} />
    <text x={f.x1} y={Math.max(f.y1 - 4 * scale, 14 * scale)} fontSize={14 * scale}>{f.findingId} · {percent(f.confidence)}</text>
  </g>;
}
