import { Alert, Button, Layout, Space, Tag, Typography } from 'antd';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { clinicalPreviewReview, clinicalPreviewTreatment } from './clinicalPreview';
import './treatment.css';

export default function ClinicalPreviewPage() {
  const location = useLocation();
  const navigate = useNavigate();
  const treatment = location.pathname.endsWith('/treatment');
  return <Layout className="clinical-preview-shell">
    <header className="clinical-preview-header">
      <div><Tag color="orange">DỮ LIỆU GIẢ</Tag>
        <Typography.Title level={3}>Xem thử điều trị và tái khám</Typography.Title>
        <Typography.Text>BN-MOCK-001 · Trám răng 25 · Chữa tủy 46</Typography.Text></div>
      <Space wrap>
        <Button type={treatment ? 'primary' : 'default'} onClick={() => navigate(clinicalPreviewTreatment)}>S22 · Ghi việc đã làm</Button>
        <Button type={!treatment ? 'primary' : 'default'} onClick={() => navigate(clinicalPreviewReview)}>S63 · Đánh giá sau điều trị</Button>
        <Button onClick={() => window.location.reload()}>Đặt lại dữ liệu giả</Button>
      </Space>
    </header>
    <Alert className="clinical-preview-notice" type="warning" showIcon
      message="Chế độ xem thử, không lưu vào hồ sơ thật"
      description="Các thao tác chỉ thay đổi dữ liệu giả trong tab này. Tải lại trang để bắt đầu lại; không gửi yêu cầu tới API hoặc database dự án." />
    <main className="clinical-preview-content"><Outlet /></main>
  </Layout>;
}
