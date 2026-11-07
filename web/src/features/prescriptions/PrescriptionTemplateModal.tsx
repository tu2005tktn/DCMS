import { Alert, Button, Empty, Modal, Select, Space, Spin, Table, Tag, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { api } from '../../services/api';
import type { PrescriptionTemplate } from './types';

export default function PrescriptionTemplateModal({
  open,
  onClose,
  onApply,
}: {
  open: boolean;
  onClose: () => void;
  onApply: (templateId: string) => Promise<void>;
}) {
  const [templates, setTemplates] = useState<PrescriptionTemplate[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string>();
  const [selectedId, setSelectedId] = useState<string>();
  const [applying, setApplying] = useState(false);

  useEffect(() => {
    if (!open) return;
    setLoading(true);
    setError(undefined);
    api<PrescriptionTemplate[]>('/api/prescriptions/templates')
      .then(rows => {
        setTemplates(rows);
        if (rows.length > 0 && !selectedId) setSelectedId(rows[0].id);
      })
      .catch((e: Error) => setError(e.message))
      .finally(() => setLoading(false));
  }, [open]);

  const selectedTemplate = templates.find(t => t.id === selectedId);

  const handleApply = async () => {
    if (!selectedId) return;
    setApplying(true);
    try {
      await onApply(selectedId);
      onClose();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setApplying(false);
    }
  };

  return (
    <Modal
      open={open}
      title="Chọn mẫu đơn thuốc chuẩn"
      onCancel={onClose}
      width={720}
      footer={[
        <Button key="cancel" onClick={onClose} disabled={applying}>
          Hủy
        </Button>,
        <Button
          key="apply"
          type="primary"
          onClick={handleApply}
          loading={applying}
          disabled={!selectedTemplate || selectedTemplate.lines.length === 0}
        >
          Áp dụng mẫu đơn thuốc
        </Button>,
      ]}
    >
      <Typography.Paragraph type="secondary">
        Chọn mẫu đơn thuốc được thiết lập sẵn theo chẩn đoán lâm sàng để tự động nạp các thuốc vào đơn của bệnh nhân.
      </Typography.Paragraph>

      {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 12 }} />}

      {loading ? (
        <div style={{ textAlign: 'center', padding: 30 }}><Spin /></div>
      ) : (
        <>
          <div style={{ marginBottom: 16 }}>
            <Typography.Text strong>Mẫu đơn thuốc: </Typography.Text>
            <Select
              style={{ width: '100%', marginTop: 6 }}
              placeholder="Chọn mẫu đơn thuốc..."
              value={selectedId}
              onChange={setSelectedId}
              options={templates.map(t => ({
                value: t.id,
                label: `${t.name} ${t.diagnosisKind ? `(${t.diagnosisKind})` : ''}`,
              }))}
            />
          </div>

          {selectedTemplate ? (
            <div>
              {selectedTemplate.description && (
                <div style={{ marginBottom: 12, padding: 8, backgroundColor: '#f0f5ff', borderRadius: 6 }}>
                  <Typography.Text italic>{selectedTemplate.description}</Typography.Text>
                </div>
              )}

              <Typography.Title level={5} style={{ margin: '8px 0' }}>
                Danh sách thuốc trong mẫu ({selectedTemplate.lines.length} loại thuốc):
              </Typography.Title>

              <Table
                size="small"
                pagination={false}
                rowKey="id"
                dataSource={selectedTemplate.lines}
                columns={[
                  {
                    title: 'Tên thuốc',
                    render: (_, r) => (
                      <div>
                        <strong>{r.medicineName}</strong>
                        <div style={{ fontSize: 12, color: '#666' }}>
                          {[r.medicineCode, r.strength, r.dosageForm].filter(Boolean).join(' · ')}
                        </div>
                      </div>
                    ),
                  },
                  {
                    title: 'Liều dùng / ngày',
                    render: (_, r) => (
                      <div>
                        {[
                          r.morningDose !== '0' ? `Sáng ${r.morningDose}` : null,
                          r.noonDose !== '0' ? `Trưa ${r.noonDose}` : null,
                          r.afternoonDose !== '0' ? `Chiều ${r.afternoonDose}` : null,
                          r.eveningDose !== '0' ? `Tối ${r.eveningDose}` : null,
                        ].filter(Boolean).join(', ') || 'Theo chỉ dẫn'}
                        <div>
                          <Tag color="cyan">
                            {r.mealTiming === 'BeforeMeal' ? 'Trước ăn' : r.mealTiming === 'WithMeal' ? 'Trong ăn' : 'Sau ăn'}
                          </Tag>
                          <Tag>{r.days} ngày</Tag>
                        </div>
                      </div>
                    ),
                  },
                  {
                    title: 'Tổng số lượng',
                    width: 110,
                    align: 'right',
                    render: (_, r) => <strong>{r.quantity} {r.unit}</strong>,
                  },
                  {
                    title: 'Lời dặn',
                    dataIndex: 'instructions',
                    ellipsis: true,
                  },
                ]}
              />
            </div>
          ) : (
            <Empty description="Chưa có mẫu đơn thuốc nào được chọn." />
          )}
        </>
      )}
    </Modal>
  );
}
