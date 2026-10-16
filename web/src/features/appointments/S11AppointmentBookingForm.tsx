import React, { useState, useEffect } from 'react';
import { useNavigate, useLocation, useParams } from 'react-router-dom';
import {
  Form,
  Input,
  Select,
  DatePicker,
  TimePicker,
  Button,
  Radio,
  Switch,
  Modal,
  message,
  Spin
} from 'antd';
import {
  CalendarOutlined,
  UserOutlined,
  PhoneOutlined,
  ShopOutlined,
  IDCardOutlined,
  CheckCircleOutlined,
  ExclamationCircleOutlined
} from '@ant-design/icons';

const S11AppointmentBookingForm = () => {
  const navigate = useNavigate();
  const location = useLocation();
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [previewMode, setPreviewMode] = useState(false);
  const [previewData, setPreviewData] = useState(null);
  const [availableSlots, setAvailableSlots] = useState([]);

  // Initialize form with default values or from URL parameters
  useEffect(() => {
    const presetValues = {};
    
    // If coming from calendar with a specific time slot
    const urlParams = new URLSearchParams(location.search);
    if (urlParams.has('date')) {
      const selectedDate = new Date(urlParams.get('date'));
      presetValues.appointmentDate = selectedDate;
      presetValues.startTime = selectedDate;
    }
    
    if (Object.keys(presetValues).length > 0) {
      form.setFieldsValue(presetValues);
    }
  }, [location, form]);

  const handleFormFinish = async (values) => {
    setPreviewData({
      ...values,
      // Calculate end time
      endTime: new Date(
        values.startTime.getTime() + 
        (values.durationInMinutes * 60000)
      )
    });
    setPreviewMode(true);
  };

  const handlePreviewCancel = () => {
    setPreviewMode(false);
    setPreviewData(null);
  };

  const handlePreviewConfirm = async () => {
    if (!previewData) return;
    
    setLoading(true);
    try {
      const appointmentData = {
        startTime: previewData.startTime.toISOString(),
        durationInMinutes: previewData.durationInMinutes,
        service: previewData.service,
        reason: previewData.reason,
        source: previewData.source,
        patientId: previewData.patientId,
        relativeId: previewData.relativeId || undefined,
        dentistId: previewData.dentistId,
        branchId: previewData.branchId,
        chairId: previewData.chairId
      };

      const response = await fetch('/api/appointments', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(appointmentData)
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.message || 'Failed to create appointment');
      }

      const result = await response.json();
      
      message.success('Đặt lịch thành công!');
      setPreviewMode(false);
      setPreviewData(null);
      form.resetFields();
      
      // Navigate back to calendar or show success
      navigate('/appointments');
    } catch (error) {
      console.error('Error creating appointment:', error);
      message.error(error.message || 'Failed to create appointment');
    } finally {
      setLoading(false);
    }
  };

  const handleFormValuesChange = (changedValues, allValues) => {
    // Validate duration based on selected time
    if (changedValues.startTime || changedValues.durationInMinutes) {
      // In a real implementation, we would check availability with the backend
      // For now, we'll just validate the duration constraints
      const duration = allValues.durationInMinutes;
      if (duration && (duration < 15 || duration > 480 || duration % 15 !== 0)) {
        form.setFields({
          durationInMinutes: {
            errors: [new Error('Thời gian phải là bội số của 15 phút, từ 15-480 phút')]
          }
        });
      }
    }
  };

  return (
    <Form
      form={form}
      layout="vertical"
      onFinish={handleFormFinish}
      onValuesChange={handleFormValuesChange}
      autoComplete="off"
    >
      <Form.Item label="Loại lịch">
        <Radio.Group
          name="appointmentType"
          options=[
            { label: 'Lịch thường', value: 'standard' },
            { label: 'Lịch langsung', value: 'walk-in' },
            { label: 'Lịch tái khám', value: 'follow-up' }
          ]
        />
      </Form.Item>

      <Form.Item label="Nguồn">
        <Select
          name="source"
          options=[
            { label: 'Điện thoại', value: 'phone' },
            { label: 'Zalo', value: 'zalo' },
            { label: 'Facebook', value: 'facebook' },
            { label: 'Trực tiếp', value: 'in-person' },
            { label: 'Ứng dụng', value: 'app' }
          ]
          placeholder="Chọn nguồn"
        />
      </Form.Item>

      <Form.Item label="Thông tin người đặt">
        <Form.Item label="Họ và tên">
          <Input name="bookerName" placeholder="Nhập họ và tên người đặt" />
        </Form.Item>
        <Form.Item label="Số điện thoại">
          <Input name="bookerPhone" placeholder="Nhập số điện thoại" />
        </Form.Item>
      </Form.Item>

      <Form.Item label="Thông tin người khám">
        <Form.Item label="Họ và tên">
          <Input name="patientName" placeholder="Nhập họ và tên người khám" />
        </Form.Item>
        <Form.Item label="Ngày sinh">
          <DatePicker name="patientBirthDate" />
        </Form.Item>
        <Form.Item label="Giới tính">
          <Select name="patientGender">
            <Select.Option value="male">Nam</Select.Option>
            <Select.Option value="female">Nữ</Select.Option>
            <Select.Option value="other">Khác</Select.Option>
          </Select>
        </Form.Item>
      </Form.Item>

      <Form.Item label="Dịch vụ/Lý do">
        <Form.Item label="Dịch vụ">
          <Input name="service" placeholder="Nhập dịch vụ" />
        </Form.Item>
        <Form.Item label="Lý do khám">
          <Input.TextArea name="reason" rows={3} placeholder="Nhập lý do khám" />
        </Form.Item>
      </Form.Item>

      <Form.Item label="Thông tin đặt lịch">
        <Form.Item label="Chi nhánh">
          <Select name="branchId" placeholder="Chọn chi nhánh">
            <Select.Option value="1">Chi nhánh 1</Select.Option>
            <Select.Option value="2">Chi nhánh 2</Select.Option>
            <Select.Option value="3">Chi nhánh 3</Select.Option>
          </Select>
        </Form.Item>
        <Form.Item label="Bác sĩ">
          <Select name="dentistId" placeholder="Chọn bác sĩ">
            <Select.Option value="1">Bác sĩ A</Select.Option>
            <Select.Option value="2">Bác sĩ B</Select.Option>
            <Select.Option value="3">Bác sĩ C</Select.Option>
          </Select>
        </Form.Item>
        <Form.Item label="Ghế thăm khám">
          <Select name="chairId" placeholder="Chọn ghế">
            <Select.Option value="1">Ghế 1</Select.Option>
            <Select.Option value="2">Ghế 2</Select.Option>
            <Select.Option value="3">Ghế 3</Select.Option>
          </Select>
        </Form.Item>
      </Form.Item>

      <Form.Item label="Thời gian">
        <Form.Item label="Ngày">
          <DatePicker name="appointmentDate" />
        </Form.Item>
        <Form.Item label="Giờ bắt đầu">
          <TimePicker name="startTime" format="HH:mm" />
        </Form.Item>
        <Form.Item label="Thời gian (phút)">
          <Input.Number
            name="durationInMinutes"
            min={15}
            max={480}
            step={15}
            placeholder="Nhập thời gian (15-480 phút, bội số của 15)"
          />
        </Form.Item>
      </Form.Item>

      <Form.Item>
        <Button type="primary" htmlType="submit" loading={loading}>
          Xem trước
        </Button>
        <Button style={{ marginLeft: 8 }} onClick={() => form.resetFields()}>
          Nhập lại
        </Button>
      </Form.Item>
    </Form>

    {/* Preview Modal */}
    <Modal
      title="Xem trước lịch hẹn"
      visible={previewMode}
      onCancel={handlePreviewCancel}
      footer={[
        <Button key="back" onClick={handlePreviewCancel}>
          Quay lại
        </Button>,
        <Button key="confirm" type="primary" loading={loading} onClick={handlePreviewConfirm}>
          Xác nhận đặt lịch
        </Button>
      ]}
    >
      {previewData && (
        <div className="appointment-preview">
          <div className="preview-section">
            <h4>Thông tin đặt lịch</h4>
            <p><strong>Nguồn:</strong> {previewData.source}</p>
            <p><strong>Người đặt:</strong> {previewData.bookerName}</p>
            <p><strong>Số điện thoại:</strong> {previewData.bookerPhone}</p>
          </div>
          
          <div className="preview-section">
            <h4>Thông tin người khám</h4>
            <p><strong>Họ và tên:</strong> {previewData.patientName}</p>
            <p><strong>Ngày sinh:</strong> {previewData.patientBirthDate?.toLocaleDateString()}</p>
            <p><strong>Giới tính:</strong> {previewData.patientGender}</p>
          </div>
          
          <div className="preview-section">
            <h4>Lịch khám</h4>
            <p><strong>Dịch vụ:</strong> {previewData.service}</p>
            <p><strong>Lý do:</strong> {previewData.reason}</p>
            <p><strong>Chi nhánh:</strong> {previewData.branchId}</p>
            <p><strong>Bác sĩ:</strong> {previewData.dentistId}</p>
            <p><strong>Ghế:</strong> {previewData.chairId}</p>
            <p><strong>Thời gian:</strong> {previewData.startTime.toLocaleString()} - {previewData.endTime.toLocaleString()}</p>
            <p><strong>Thời gian thực hiện:</strong> {previewData.durationInMinutes} phút</p>
          </div>
          
          {/* Check if appointment is within 60 minutes for exception reason */}
          {(previewData.startTime.getTime() - Date.now()) < 3600000 && (
            <div className="preview-section">
              <h4>Lý do ngoại lệ (ví ur)</h4>
              <Form.Item label="Lý do ngoại lệ">
                <Input.TextArea 
                  name="exceptionReason" 
                  rows={3} 
                  placeholder="Nhập lý do đặt lịch gần hơn 60 phút"
                />
              </Form.Item>
            </div>
          )}
        </div>
      )}
    </Modal>
  );
};

export default S11AppointmentBookingForm;