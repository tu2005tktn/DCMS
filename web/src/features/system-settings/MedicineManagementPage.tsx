import { Typography } from 'antd';
import SupplyCatalogPanel from './SupplyCatalogPanel';
import './systemSettings.css';

export default function MedicineManagementPage() {
  return <main className="system-settings-page">
    <Typography.Title level={2}>Quản lý thuốc</Typography.Title>
    <SupplyCatalogPanel onlyMedicines />
  </main>;
}
