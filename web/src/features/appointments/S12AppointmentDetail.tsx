import { useParams } from 'react-router-dom';
import { Spin } from 'antd';

const S12AppointmentDetail = () => {
  const { id } = useParams();

  return (
    <div className="appointment-detail">
      <Spin tip="Đang tải chi tiết lịch hẹn...">
        <div className="detail-content">
          <h2>Chi tiết lịch hẹn</h2>
          <p>ID Lịch hẹn: {id}</p>
          <p>Đây là thành phần S12 thuộc TV5 - Chi tiết lịch hẹn</p>
          <p>Trong thực tế, thành phần này sẽ hiển thị chi tiết lịch hẹn và cho phép sửa đổi trong phạm vi cho phép.</p>
        </div>
      </Spin>
    </div>
  );
};

export default S12AppointmentDetail;
