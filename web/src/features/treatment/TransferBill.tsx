import { Alert, Descriptions, QRCode, Tag } from 'antd';
import type { PaymentContext } from './sheetTypes';
import { money } from './types';
import type { PayOsOrder } from './sheetTypes';

const clinicDate = (value: string) => new Intl.DateTimeFormat('sv-SE', {
  timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit',
}).format(new Date(value));
const showDate = (value: string) => value.split('-').reverse().join('/');

export default function TransferBill({ context, paidOn, amounts, order }: {
  context: PaymentContext; paidOn: string; amounts: Record<string, number>; order: PayOsOrder;
}) {
  const total = Object.values(amounts).reduce((sum, value) => sum + value, 0);
  const todayLines = context.lines.filter(line => clinicDate(line.performedAt) === paidOn);
  const olderLines = context.lines.filter(line => clinicDate(line.performedAt) !== paidOn && (amounts[line.activityId] ?? 0) > 0);

  function rows(lines: PaymentContext['lines']) {
    return lines.map(line => <tr key={line.activityId}>
      <td><strong>{line.serviceName ?? 'Dịch vụ điều trị'}</strong><small>{showDate(clinicDate(line.performedAt))} · {line.dentistName}</small>
        {line.materials.length > 0 && <div className="transfer-bill-materials">
          Vật liệu đã dùng: {line.materials.map(m => `${m.name} (${m.quantity} ${m.unit ?? ''})`).join(', ')}
        </div>}</td>
      <td>{money(line.costVnd)}</td><td>{money(line.discountVnd)}</td>
      <td>{money(line.paidVnd)}</td><td><strong>{money(amounts[line.activityId] ?? 0)}</strong></td>
    </tr>);
  }

  return <section className="transfer-bill" aria-label="Bill chuyển khoản">
    {order.state === 'Paid' ? <Alert type="success" showIcon message="PayOS đã xác nhận thanh toán. Số tiền đã được ghi nhận vào phiếu điều trị." />
      : order.state === 'Review' ? <Alert type="warning" showIcon message="PayOS báo đã chuyển tiền nhưng số dư phiếu đã thay đổi. Cần đối soát trước khi ghi nhận." />
      : order.state === 'LinkError' ? <Alert type="error" showIcon message="Không tạo được mã QR PayOS. Hãy quay lại và thử lại." />
      : order.state === 'Creating' ? <Alert type="info" showIcon message="Đang tạo mã QR PayOS. Bill tự cập nhật." />
      : <Alert type="info" showIcon message="Đang chờ PayOS xác nhận. Bill tự cập nhật, bạn có thể để trang này mở trong lúc khách quét QR." />}
    <Descriptions size="small" column={{ xs: 1, sm: 2 }} items={[
      { key: 'dossier', label: 'Phiếu điều trị', children: context.dossierCode },
      { key: 'patient', label: 'Bệnh nhân', children: context.patientName },
      { key: 'date', label: 'Ngày điều trị trên bill', children: showDate(paidOn) },
      { key: 'order', label: 'Mã giao dịch PayOS', children: order.orderCode },
    ]} />
    <h3>Dịch vụ và vật liệu dùng trong ngày {showDate(paidOn)}</h3>
    <div className="transfer-bill-scroll"><table className="transfer-bill-table"><thead><tr>
      <th>Dịch vụ / vật liệu</th><th>Chi phí</th><th>Giảm giá</th><th>Đã trả</th><th>Chuyển lần này</th>
    </tr></thead><tbody>{rows(todayLines)}</tbody></table></div>
    {todayLines.length === 0 && <p>Không có dịch vụ được ghi nhận trong ngày thanh toán.</p>}
    {olderLines.length > 0 && <><h3>Dịch vụ ngày khác được thanh toán lần này</h3>
      <div className="transfer-bill-scroll"><table className="transfer-bill-table"><thead><tr>
        <th>Dịch vụ</th><th>Chi phí</th><th>Giảm giá</th><th>Đã trả</th><th>Chuyển lần này</th>
      </tr></thead><tbody>{rows(olderLines)}</tbody></table></div></>}
    <div className="transfer-bill-pay"><div><span>{order.state === 'Paid' ? 'Số tiền đã chuyển' : 'Số tiền cần chuyển'}</span><strong>{money(total)}</strong>
      {order.bankBin && <p>Ngân hàng BIN {order.bankBin} · STK {order.accountNumber}<br />{order.accountName}</p>}
      <Tag color={order.state === 'Paid' ? 'green' : order.state === 'Review' ? 'orange' : 'gold'}>
        {order.state === 'Paid' ? 'Đã thanh toán' : order.state === 'Review' ? 'Cần đối soát' : order.state === 'Creating' ? 'Đang tạo QR' : order.state === 'LinkError' ? 'Lỗi tạo QR' : 'Chờ thanh toán'}</Tag>
    </div>
      {order.qrCode && order.state === 'Pending' && <QRCode value={order.qrCode} size={220} aria-label={`Mã VietQR chuyển ${money(total)}`} />}
    </div>
    <p className="transfer-bill-footnote">Vật liệu là thông tin xuất dùng, không tính thêm vào số tiền thanh toán. Trạng thái được xác nhận từ máy chủ PayOS, không dựa vào ảnh chụp hoặc thao tác của người dùng.</p>
  </section>;
}
