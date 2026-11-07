import { Button, Descriptions, Divider, Modal, QRCode, Space, Spin, Table, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { api } from '../../services/api';
import type { PrescriptionPrintDto } from './types';

export default function PrescriptionPrintModal({
  visitId,
  open,
  onClose,
}: {
  visitId: string;
  open: boolean;
  onClose: () => void;
}) {
  const [data, setData] = useState<PrescriptionPrintDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string>();

  useEffect(() => {
    if (!open) return;
    setLoading(true);
    setError(undefined);
    api<PrescriptionPrintDto>(`/api/prescriptions/visits/${visitId}/print`)
      .then(setData)
      .catch((e: Error) => setError(e.message))
      .finally(() => setLoading(false));
  }, [open, visitId]);

  const handlePrint = () => {
    window.print();
  };

  return (
    <Modal
      open={open}
      onCancel={onClose}
      width={780}
      title="Đơn thuốc khám chữa bệnh (Quy chuẩn Bộ Y Tế)"
      footer={[
        <Button key="close" onClick={onClose}>
          Đóng
        </Button>,
        <Button key="print" type="primary" onClick={handlePrint} disabled={!data || loading}>
          In đơn thuốc (A4/A5)
        </Button>,
      ]}
    >
      {loading ? (
        <div style={{ textAlign: 'center', padding: 40 }}><Spin size="large" /></div>
      ) : error ? (
        <Typography.Text type="danger">{error}</Typography.Text>
      ) : data ? (
        <div className="prescription-print-document" style={{ padding: 16, backgroundColor: '#fff', color: '#111' }}>
          {/* Header Phòng khám */}
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', borderBottom: '2px solid #333', paddingBottom: 12, marginBottom: 16 }}>
            <div>
              <Typography.Title level={4} style={{ margin: 0, textTransform: 'uppercase', color: '#0958d9' }}>
                {data.clinicName}
              </Typography.Title>
              <Typography.Text strong>{data.branchName}</Typography.Text>
              <div>Địa chỉ: {data.branchAddress}</div>
              <div>Hotline tư vấn: {data.branchPhone}</div>
            </div>
            <div style={{ textAlign: 'right' }}>
              <QRCode value={data.verificationQrData} size={80} style={{ margin: '0 auto' }} />
              <div style={{ fontSize: 11, color: '#666', marginTop: 4 }}>Mã xác thực đơn</div>
            </div>
          </div>

          <div style={{ textAlign: 'center', marginBottom: 20 }}>
            <Typography.Title level={3} style={{ margin: 0, textTransform: 'uppercase', letterSpacing: 1 }}>
              ĐƠN THUỐC
            </Typography.Title>
            <Typography.Text type="secondary" italic>(Đơn thuốc mua ngoài tại các cơ sở bán lẻ thuốc hợp pháp)</Typography.Text>
          </div>

          {/* Thông tin Bệnh nhân */}
          <Descriptions size="small" column={2} bordered={false} style={{ marginBottom: 16 }}>
            <Descriptions.Item label="Họ và tên người bệnh"><strong>{data.patientName}</strong></Descriptions.Item>
            <Descriptions.Item label="Mã người bệnh"><code>{data.patientCode}</code></Descriptions.Item>
            <Descriptions.Item label="Năm sinh">{data.birthYear ?? '—'}</Descriptions.Item>
            <Descriptions.Item label="Giới tính">{data.gender ?? '—'}</Descriptions.Item>
            <Descriptions.Item label="Địa chỉ" span={2}>{data.address}</Descriptions.Item>
            <Descriptions.Item label="Chẩn đoán lâm sàng" span={2}>
              <strong style={{ color: '#c41d7f' }}>{data.diagnosis}</strong>
            </Descriptions.Item>
          </Descriptions>

          {/* Bảng Danh mục thuốc */}
          <Typography.Title level={5} style={{ marginBottom: 8 }}>Chỉ định dùng thuốc:</Typography.Title>
          <Table
            size="small"
            pagination={false}
            rowKey="index"
            dataSource={data.items}
            columns={[
              { title: 'STT', dataIndex: 'index', width: 50, align: 'center' },
              {
                title: 'Tên thuốc & Hàm lượng',
                render: (_, r) => (
                  <div>
                    <strong>{r.medicineName}</strong> {r.strength && <span>({r.strength})</span>}
                    {r.activeIngredient && <div style={{ fontSize: 12, color: '#666' }}>Hoạt chất: {r.activeIngredient}</div>}
                    <div style={{ fontSize: 13, color: '#0958d9', marginTop: 2 }}>{r.dosageInstruction}</div>
                  </div>
                ),
              },
              {
                title: 'Số lượng',
                width: 120,
                align: 'right',
                render: (_, r) => <strong>{r.quantity} {r.unit}</strong>,
              },
            ]}
          />

          {/* Lời dặn của bác sĩ */}
          <div style={{ marginTop: 16, padding: 12, backgroundColor: '#f5f5f5', borderRadius: 6 }}>
            <Typography.Text strong>Lời dặn của bác sĩ điều trị:</Typography.Text>
            <div style={{ marginTop: 4, whiteStyle: 'pre-wrap' }}>{data.generalAdvice}</div>
          </div>

          <Divider style={{ margin: '16px 0' }} />

          {/* Chữ ký & Ngày kê đơn */}
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginTop: 12 }}>
            <div style={{ fontSize: 12, color: '#555', maxWidth: 300 }}>
              <div>* Đơn thuốc có giá trị mua trong vòng 05 ngày kể từ ngày kê đơn.</div>
              <div>* Không tự ý ngưng thuốc hoặc đổi thuốc khi chưa có ý kiến của bác sĩ.</div>
            </div>
            <div style={{ textAlign: 'center', minWidth: 220 }}>
              <div>Ngày {new Date(data.prescribedAt).getDate()} tháng {new Date(data.prescribedAt).getMonth() + 1} năm {new Date(data.prescribedAt).getFullYear()}</div>
              <div style={{ fontWeight: 'bold', marginTop: 4 }}>BÁC SĨ ĐIỀU TRỊ</div>
              <div style={{ fontStyle: 'italic', fontSize: 12, color: '#888' }}>(Ký và ghi rõ họ tên)</div>
              <div style={{ height: 60, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <span style={{ fontFamily: 'cursive', fontSize: 18, color: '#1677ff' }}>{data.doctorName}</span>
              </div>
              <div><strong>{data.doctorName}</strong></div>
              {data.doctorLicenseNumber && <div style={{ fontSize: 12, color: '#666' }}>CCHN: {data.doctorLicenseNumber}</div>}
            </div>
          </div>
        </div>
      ) : null}
    </Modal>
  );
}
