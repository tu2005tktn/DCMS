import { Alert, Button, Modal, QRCode, Segmented, Spin } from 'antd';
import { useEffect, useState } from 'react';
import { api } from '../../services/api';
import type { PrescriptionPrintDto } from './types';
import './prescriptionPrint.css';

type PaperSize = 'A5' | 'A4';

// docs/specs/PRESCRIPTION-MANAGEMENT.md §3.9 — external prescription layout; only the document area is printed.
export function PrescriptionDocument({ data }: { data: PrescriptionPrintDto }) {
  const at = new Date(data.prescribedAt);
  return (
    <div className="rx-doc">
      {data.isDraft && <div className="rx-doc-watermark" aria-hidden>BẢN NHÁP<br />CHƯA CÓ GIÁ TRỊ</div>}
      <div className="rx-doc-header">
        <div className="rx-doc-brand">
          {data.logoDataUrl && <img src={data.logoDataUrl} alt="Logo chi nhánh" />}
          <div>
            <div className="rx-doc-branch">{data.branchName}</div>
            {data.branchAddress && <div>Địa chỉ: {data.branchAddress}</div>}
            {data.branchPhone && <div>Điện thoại: {data.branchPhone}</div>}
          </div>
        </div>
        {data.verificationQrData && (
          <div className="rx-doc-qr">
            <QRCode value={data.verificationQrData} size={84} bordered={false} />
            <div>Mã xác thực: {data.verificationCode}</div>
          </div>
        )}
      </div>

      <div className="rx-doc-title">
        <h2>ĐƠN THUỐC</h2>
        <div><i>Đơn thuốc mua ngoài — phòng khám không bán thuốc</i></div>
        {data.revision > 1 && <div>Bản điều chỉnh lần {data.revision - 1}</div>}
      </div>

      <div className="rx-doc-patient">
        <div>Họ tên: <strong>{data.patientName}</strong></div>
        <div>Mã BN: <strong>{data.patientCode}</strong></div>
        <div>Năm sinh: {data.birthYear ?? '……'}</div>
        <div>Giới tính: {data.gender ?? '……'}</div>
        <div className="rx-wide">Địa chỉ: {data.address || '……………………………………'}</div>
        <div className="rx-wide">Chẩn đoán: <strong>{data.diagnosis || '……………………………………'}</strong></div>
      </div>

      <table className="rx-doc-items">
        <thead>
          <tr><th className="rx-num">STT</th><th>Tên thuốc — hàm lượng — cách dùng</th><th className="rx-qty">Số lượng</th></tr>
        </thead>
        <tbody>
          {data.items.map(item => (
            <tr key={item.index}>
              <td className="rx-num">{item.index}</td>
              <td>
                <strong>{item.medicineName}</strong>{item.strength ? ` ${item.strength}` : ''}{item.dosageForm ? ` (${item.dosageForm})` : ''}
                {item.activeIngredient && <div>Hoạt chất: {item.activeIngredient}</div>}
                <div>{item.dosageInstruction}</div>
                {item.instructions && <div><i>{item.instructions}</i></div>}
              </td>
              <td className="rx-qty"><strong>{item.quantity} {item.unit}</strong><div>({item.quantityInWords} {item.unit})</div></td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="rx-doc-advice"><strong>Lời dặn của bác sĩ:</strong>{'\n'}{data.generalAdvice || 'Dùng thuốc đúng chỉ dẫn. Tái khám khi có dấu hiệu bất thường.'}</div>

      <div className="rx-doc-footer">
        <div className="rx-doc-notice">
          <div>Mang đơn này đến hiệu thuốc để mua thuốc. Phòng khám không bán thuốc tại chỗ.</div>
          <div>Không tự ý ngưng hoặc đổi thuốc khi chưa hỏi ý kiến bác sĩ.</div>
        </div>
        <div className="rx-doc-sign">
          <div>Ngày {at.getDate()} tháng {at.getMonth() + 1} năm {at.getFullYear()}</div>
          <strong>BÁC SĨ KÊ ĐƠN</strong>
          <div><i>(Ký, ghi rõ họ tên)</i></div>
          <div className="rx-sign-space" />
          <strong>{data.doctorName}</strong>
          <div>CCHN: {data.doctorLicenseNumber || '……………………'}</div>
        </div>
      </div>
    </div>
  );
}

export default function PrescriptionPrintModal({ visitId, open, onClose }: { visitId: string; open: boolean; onClose: () => void }) {
  const [data, setData] = useState<PrescriptionPrintDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string>();
  const [paper, setPaper] = useState<PaperSize>('A5');

  useEffect(() => {
    if (!open) return;
    let active = true;
    setLoading(true);
    setError(undefined);
    setData(null);
    api<PrescriptionPrintDto>(`/api/prescriptions/visits/${visitId}/print`)
      .then(value => { if (active) setData(value); })
      .catch((e: Error) => { if (active) setError(e.message); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [open, visitId]);

  const print = () => {
    const style = document.createElement('style');
    style.textContent = `@page { size: ${paper} portrait; margin: 10mm; }`;
    document.head.appendChild(style);
    document.documentElement.classList.add('rx-printing');
    const cleanup = () => {
      document.documentElement.classList.remove('rx-printing');
      style.remove();
      window.removeEventListener('afterprint', cleanup);
    };
    window.addEventListener('afterprint', cleanup);
    window.print();
  };

  return (
    <Modal
      open={open}
      onCancel={onClose}
      width={820}
      title="In đơn thuốc"
      footer={[
        <Segmented key="paper" value={paper} onChange={v => setPaper(v as PaperSize)} options={['A5', 'A4']} style={{ marginRight: 8 }} />,
        <Button key="close" onClick={onClose}>Đóng</Button>,
        <Button key="print" type="primary" onClick={print} disabled={!data || loading}>In khổ {paper}</Button>,
      ]}
    >
      {loading ? <div style={{ textAlign: 'center', padding: 40 }}><Spin /></div>
        : error ? <Alert type="error" showIcon message={error} />
        : data ? (
          <>
            {data.isDraft && <Alert type="warning" showIcon style={{ marginBottom: 12 }} message="Đơn chưa hoàn tất kê đơn: bản in có dấu BẢN NHÁP và không có mã xác thực." />}
            <PrescriptionDocument data={data} />
          </>
        ) : null}
    </Modal>
  );
}
