import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, DatePicker, Space, message } from 'antd';
import { FilterOutlined, PlusOutlined } from '@ant-design/icons';

type Appointment = {
  id: number;
  patientName: string;
  service: string;
  startTime: string;
  endTime: string;
};

type Filters = {
  branchId: number | null;
  dentistId: number | null;
  status: string | null;
};

const startOfDay = (date: Date) => {
  const d = new Date(date);
  d.setHours(0, 0, 0, 0);
  return d;
};

const endOfDay = (date: Date) => {
  const d = new Date(date);
  d.setHours(23, 59, 59, 999);
  return d;
};

const startOfWeek = (date: Date) => {
  const d = new Date(date);
  const day = d.getDay() === 0 ? 6 : d.getDay() - 1;
  d.setDate(d.getDate() - day);
  d.setHours(0, 0, 0, 0);
  return d;
};

const endOfWeek = (date: Date) => {
  const d = new Date(date);
  const day = d.getDay() === 0 ? 6 : d.getDay() - 1;
  d.setDate(d.getDate() - day + 6);
  d.setHours(23, 59, 59, 999);
  return d;
};

const subDays = (date: Date, n: number) => {
  const d = new Date(date);
  d.setDate(d.getDate() - n);
  return d;
};

const addDays = (date: Date, n: number) => {
  const d = new Date(date);
  d.setDate(d.getDate() + n);
  return d;
};

const formatHM = (date: Date) => {
  const h = String(date.getHours()).padStart(2, '0');
  const m = String(date.getMinutes()).padStart(2, '0');
  return `${h}:${m}`;
};

const S10AppointmentCalendar = () => {
  const navigate = useNavigate();
  const [appointments, setAppointments] = useState<Appointment[]>([]);
  const [loading, setLoading] = useState(false);
  const [selectedDate, setSelectedDate] = useState(startOfDay(new Date()));
  const [viewType, setViewType] = useState<'day' | 'week'>('week');
  const [filters] = useState<Filters>({
    branchId: null,
    dentistId: null,
    status: null
  });
  const [visibleDateRange, setVisibleDateRange] = useState({
    start: startOfDay(new Date()),
    end: endOfDay(new Date())
  });

  useEffect(() => {
    fetchAppointments();
  }, [selectedDate, viewType, filters]);

  const fetchAppointments = async () => {
    setLoading(true);
    try {
      let startDate: Date;
      let endDate: Date;

      if (viewType === 'day') {
        startDate = startOfDay(selectedDate);
        endDate = endOfDay(selectedDate);
      } else {
        startDate = startOfWeek(selectedDate);
        endDate = endOfWeek(selectedDate);
      }

      setVisibleDateRange({ start: startDate, end: endDate });

      const url = new URL(`/api/appointments?startDate=${startDate.toISOString()}&endDate=${endDate.toISOString()}&pageSize=100`);
      if (filters.branchId) url.searchParams.set('branchId', String(filters.branchId));
      if (filters.dentistId) url.searchParams.set('dentistId', String(filters.dentistId));
      if (filters.status) url.searchParams.set('status', filters.status);

      const response = await fetch(url.toString());

      if (!response.ok) throw new Error('Failed to fetch appointments');

      const data = await response.json();
      setAppointments(data);
    } catch (error) {
      console.error('Error fetching appointments:', error);
      message.error('Failed to load appointments');
    } finally {
      setLoading(false);
    }
  };

  const handlePrevPeriod = () => {
    setSelectedDate((prev: Date) => {
      if (viewType === 'day') {
        return subDays(prev, 1);
      }
      return subDays(prev, 7);
    });
  };

  const handleNextPeriod = () => {
    setSelectedDate((prev: Date) => {
      if (viewType === 'day') {
        return addDays(prev, 1);
      }
      return addDays(prev, 7);
    });
  };

  const handleViewChange = (type: 'day' | 'week') => {
    setViewType(type);
    setSelectedDate(new Date());
  };

  const handleDateSelect = (date: Date) => {
    setSelectedDate(date);
  };

  const handleSlotClick = (dateTime: Date) => {
    navigate(`/appointments/book?date=${dateTime.toISOString()}`);
  };

  const handleAppointmentClick = (appointmentId: number) => {
    navigate(`/appointments/${appointmentId}`);
  };

  const generateTimeSlots = (startDate: Date, endDate: Date) => {
    const slots: Array<{ time: string; dateTime: Date; appointments: Appointment[] }> = [];
    const current = startOfDay(startDate);
    const end = endOfDay(endDate);

    while (current <= end) {
      slots.push({
        time: formatHM(current),
        dateTime: new Date(current),
        appointments: appointments.filter((apt) =>
          new Date(apt.startTime) <= current && new Date(apt.endTime) > current
        )
      });
      current.setMinutes(current.getMinutes() + 15);
    }

    return slots;
  };

  if (loading) return <div>Loading appointments...</div>;

  return (
    <div className="appointment-calendar">
      <div className="calendar-header">
        <Space>
          <Button
            size="small"
            icon={<PlusOutlined />}
            onClick={() => navigate('/appointments/book')}
          >
            Đặt lịch mới
          </Button>
          <Button
            size="small"
            icon={<FilterOutlined />}
            onClick={() => { /* Open filter dialog */ }}
          >
            Lọc
          </Button>
          <Space>
            <Button
              size="small"
              onClick={handlePrevPeriod}
              disabled={viewType === 'week' && startOfWeek(selectedDate) <= startOfWeek(new Date())}
            >
              Trước
            </Button>
            <Button
              size="small"
              onClick={() => setSelectedDate(new Date())}
            >
              Hôm nay
            </Button>
            <Button
              size="small"
              onClick={handleNextPeriod}
            >
              Tiếp theo
            </Button>
          </Space>
          <Space>
            <Space>
              <Button
                size="small"
                onClick={() => handleViewChange('day')}
              >
                Ngày
              </Button>
              <Button
                size="small"
                onClick={() => handleViewChange('week')}
              >
                Tuần
              </Button>
            </Space>
            <DatePicker
              value={selectedDate}
              onChange={handleDateSelect}
              showTime={{ format: 'HH:mm' }}
            />
          </Space>
        </Space>
      </div>

      <div className="calendar-grid">
        <div className="time-axis">
          {[...Array(24)].map((_, hour) => (
            <div key={hour} className="time-slot">
              {String(hour).padStart(2, '0')}:00
            </div>
          ))}
        </div>

        <div className="date-slots">
          {generateTimeSlots(visibleDateRange.start, visibleDateRange.end).map((slot, index) => (
            <div key={index} className="date-slot">
              <div className="time-label">{slot.time}</div>
              <div className="appointments">
                {slot.appointments.map((apt) => (
                  <div
                    key={apt.id}
                    className="appointment-card"
                    onClick={() => handleAppointmentClick(apt.id)}
                    title={`${apt.patientName} - ${apt.service}`}
                  >
                    <div className="patient-name">{apt.patientName}</div>
                    <div className="service">{apt.service}</div>
                    <div className="time">{formatHM(new Date(apt.startTime))} - {formatHM(new Date(apt.endTime))}</div>
                  </div>
                ))}
                {!slot.appointments.length && (
                  <div
                    className="empty-slot"
                    onClick={() => handleSlotClick(slot.dateTime)}
                  >
                    Trống
                  </div>
                )}
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
};

export default S10AppointmentCalendar;
