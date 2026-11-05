import { Tabs, Typography } from 'antd';
import MaterialPurchasesPanel from './MaterialPurchasesPanel';
import { MaterialStockPanel, MaterialUsagePanel } from './MaterialStockPanels';
import './systemSettings.css';

// Main function "Dụng cụ - vật liệu": purchase log, stock and usage history (docs/specs/MATERIAL-PURCHASES.md, MATERIAL-STOCK.md).
// Catalog and categories stay in system settings.
export default function MaterialsPage() {
  return <div className="system-settings-page">
    <Typography.Title level={2}>Dụng cụ - vật liệu</Typography.Title>
    <Tabs destroyInactiveTabPane items={[
      { key: 'purchases', label: 'Nhập hàng', children: <MaterialPurchasesPanel /> },
      { key: 'stock', label: 'Tồn kho', children: <MaterialStockPanel /> },
      { key: 'usage', label: 'Lịch sử xuất dùng', children: <MaterialUsagePanel /> },
    ]} />
  </div>;
}
