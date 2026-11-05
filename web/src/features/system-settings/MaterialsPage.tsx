import { Typography } from 'antd';
import MaterialPurchasesPanel from './MaterialPurchasesPanel';
import './systemSettings.css';

// Main function "Dụng cụ - vật liệu": purchase log only (docs/specs/MATERIAL-PURCHASES.md). Catalog and categories stay in system settings.
export default function MaterialsPage() {
  return <div className="system-settings-page">
    <Typography.Title level={2}>Dụng cụ - vật liệu</Typography.Title>
    <MaterialPurchasesPanel />
  </div>;
}
