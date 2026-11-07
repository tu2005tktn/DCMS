import { Tabs, Typography } from 'antd';
import MaterialPurchasesPanel from './MaterialPurchasesPanel';
import { MaterialStockPanel, MaterialUsagePanel } from './MaterialStockPanels';
import './systemSettings.css';
import { usePermissions } from '../authentication/usePermissions';

// Main function "Dụng cụ - vật liệu": purchase log, stock and usage history (docs/specs/MATERIAL-PURCHASES.md, MATERIAL-STOCK.md).
// Catalog and categories stay in system settings.
export default function MaterialsPage() {
  const has = usePermissions();
  const tabs = [
    { key: 'purchases', label: 'Nhập hàng', permission: 'materials.purchases.read', children: <MaterialPurchasesPanel /> },
    { key: 'stock', label: 'Tồn kho', permission: 'materials.stock.read', children: <MaterialStockPanel /> },
    { key: 'usage', label: 'Lịch sử xuất dùng', permission: 'materials.usage.read', children: <MaterialUsagePanel /> },
  ].filter(tab => has(tab.permission));
  return <div className="system-settings-page">
    <Typography.Title level={2}>Dụng cụ - vật liệu</Typography.Title>
    <Tabs key={tabs.map(tab => tab.key).join(',')} destroyInactiveTabPane items={tabs} />
  </div>;
}
