import { Alert, Button, Card, Space, Spin, Tag } from 'antd';
import { ApiOutlined, CheckCircleFilled, CloudServerOutlined, DatabaseOutlined, WarningFilled } from '@ant-design/icons';
import { useEffect, useState } from 'react';
import { fetchHealth, type HealthResult } from '../services/health';
import BrandLogo from '../components/BrandLogo';

export default function HealthStatus() {
  const [result, setResult] = useState<HealthResult>({ kind: 'loading' });

  async function refresh() {
    setResult({ kind: 'loading' });
    setResult(await fetchHealth());
  }

  useEffect(() => { void refresh(); }, []);

  const api = result.kind === 'response' ? result.data.api : 'unavailable';
  const database = result.kind === 'response' ? result.data.database : 'unknown';
  const loading = result.kind === 'loading';

  function statusChip(kind: string) {
    if (kind === 'healthy') return <Tag color="success" icon={<CheckCircleFilled />}>healthy</Tag>;
    if (kind === 'unknown' || kind === 'unavailable') return <Tag icon={<WarningFilled />}>không xác định</Tag>;
    return <Tag color="error" icon={<WarningFilled />}>{kind}</Tag>;
  }

  return (
    <main className="auth-page">
      <div className="auth-shell auth-shell--narrow">
        <section className="auth-panel">
          <div className="auth-head">
            <span className="auth-eyebrow">
              <span className="auth-brand-logo" style={{ width: 30, height: 30, borderRadius: 9, display: 'inline-grid', placeItems: 'center' }}><BrandLogo size={16} /></span>
              DCMS · Phòng khám nha khoa
            </span>
            <h1>Trạng thái hệ thống</h1>
            <p>Trang này đọc trạng thái trực tiếp từ API và cơ sở dữ liệu PostgreSQL.</p>
          </div>
          <Card className="auth-card dcms-card" title="Tình trạng dịch vụ"
            extra={<Button onClick={() => void refresh()} loading={loading} icon={<CloudServerOutlined />}>Kiểm tra lại</Button>}>
            <Spin spinning={loading}>
              <Space direction="vertical" size={16} style={{ width: '100%' }}>
                <div className="status-row">
                  <span className="status-row-icon status-row-icon--primary"><ApiOutlined /></span>
                  <div className="status-row-meta">
                    <strong>API</strong>
                    <span>DCMS Web API</span>
                  </div>
                  <div className="status-row-value">{statusChip(api)}</div>
                </div>
                <div className="status-row">
                  <span className="status-row-icon status-row-icon--neutral"><DatabaseOutlined /></span>
                  <div className="status-row-meta">
                    <strong>PostgreSQL</strong>
                    <span>Cơ sở dữ liệu chính</span>
                  </div>
                  <div className="status-row-value">{statusChip(database)}</div>
                </div>
                {result.kind === 'error' && <Alert type="error" showIcon message={`Không thể gọi API: ${result.message}`} />}
                {result.kind === 'response' && result.httpStatus !== 200 &&
                  <Alert type="warning" showIcon message={`API đang hoạt động nhưng cơ sở dữ liệu chưa sẵn sàng (HTTP ${result.httpStatus}).`} />}
              </Space>
            </Spin>
          </Card>
          <p className="auth-foot">Trạng thái được đọc từ endpoint <code>/health</code> của API.</p>
        </section>
      </div>
    </main>
  );
}
