import { Button, Card, Flex, Space, Tag, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { fetchHealth, type HealthResult } from '../services/health';

const { Title, Text, Paragraph } = Typography;

export default function HealthStatus() {
  const [result, setResult] = useState<HealthResult>({ kind: 'loading' });

  async function refresh() {
    setResult({ kind: 'loading' });
    setResult(await fetchHealth());
  }

  useEffect(() => { void refresh(); }, []);

  const api = result.kind === 'response' ? result.data.api : 'unavailable';
  const database = result.kind === 'response' ? result.data.database : 'unknown';

  return (
    <main className="page">
      <div className="content">
        <Text className="eyebrow">DCMS · Development workspace</Text>
        <Title level={1}>DCMS</Title>
        <Paragraph className="intro">Nền tảng quản lý dữ liệu. Trang này đọc trạng thái trực tiếp từ API.</Paragraph>
        <Card title="Trạng thái hệ thống" extra={<Button onClick={() => void refresh()} loading={result.kind === 'loading'}>Kiểm tra lại</Button>}>
          <Flex vertical gap={16}>
            <Space><Text strong>API</Text><Tag color={api === 'healthy' ? 'success' : 'error'}>{result.kind === 'loading' ? 'Đang kiểm tra' : api}</Tag></Space>
            <Space><Text strong>PostgreSQL</Text><Tag color={database === 'healthy' ? 'success' : database === 'unknown' ? 'default' : 'error'}>{database}</Tag></Space>
            {result.kind === 'error' && <Text type="danger">Không thể gọi API: {result.message}</Text>}
            {result.kind === 'response' && result.httpStatus !== 200 && <Text type="warning">API đang hoạt động nhưng cơ sở dữ liệu chưa sẵn sàng (HTTP {result.httpStatus}).</Text>}
          </Flex>
        </Card>
      </div>
    </main>
  );
}

