import React, { useState, useEffect } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Button, Card, DatePicker, Dropdown, Menu, Space, Tag, Toast, message } from 'antd';
import { CalendarOutlined, ClockCircleOutlined, FilterOutlined, PlusOutlined } from '@ant-design/icons';
import { format, subDays, addDays, startOfWeek, endOfWeek, startOfDay, endOfDay } from 'date-fns';

const S10AppointmentCalendar = () => {
  const navigate = useNavigate();
  const [appointments, setAppointments] = useState([]);
  const [loading, setLoading] = useState(false);
  const [selectedDate, setSelectedDate] = useState(startOfDay(new Date()));
  const [viewType, setViewType] = useState<'day' | 'week'>('week');
  const [filters, setFilters] = useState({
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
      let startDate, endDate;
      
      if (viewType === 'day') {
        startDate = startOfDay(selectedDate);
        endDate = endOfDay(selectedDate);
      } else {
        startDate = startOfWeek(selectedDate, { weekStartsOn: 1 }); // Monday start
        endDate = endOfWeek(selectedDate, { weekStartsOn: 1 }); // Sunday end
      }

      setVisibleDateRange({ start: startDate, end: endDate });

      const response = await fetch(`/api/appointments?startDate=${startDate.toISOString()}&endDate=${endDate.toISOString()}${
        filters.branchId ? `&branchId=${filters.branchId}` : ''
      }${filters.dentistId ? `&dentistId=${filters.dentistId}` : ''}${
        filters.status ? `&status=${filters.status}` : ''
      }&pageSize=100`);

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
    setSelectedDate(prev => {
      if (viewType === 'day') {
        return subDays(prev, 1);
      } else {
        return subDays(prev, 7);
      }
    });
  };

  const handleNextPeriod = () => {
    setSelectedDate(prev => {
      if (viewType === 'day') {
        return addDays(prev, 1);
      } else {
        return addDays(prev, 7);
      }
    });
  };

  const handleViewChange = (type) => {
    setViewType(type);
    // Reset to current date when switching views
    setSelectedDate(new Date());
  };

  const handleFilterChange = (newFilters) => {
    setFilters(newFilters);
    fetchAppointments();
  };

  const handleDateSelect = (date) => {
    setSelectedDate(date);
  };

  const handleSlotClick = (dateTime) => {
    // Navigate to S11 with pre-filled date/time
    navigate(`/appointments/book?date=${dateTime.toISOString()}`);
  };

  const handleAppointmentClick = (appointmentId) => {
    // Navigate to S12 (TV5) for appointment details
    navigate(`/appointments/${appointmentId}`);
  };

  const generateTimeSlots = (startDate, endDate) => {
    const slots = [];
    const current = startOfDay(startDate);
    const end = endOfDay(endDate);

    while (current <= end) {
      slots.push({
        time: format(current, 'HH:mm'),
        dateTime: new Date(current),
        appointments: appointments.filter(apt => 
          new Date(apt.startTime) <= current && 
          new Date(apt.endTime) > current
        )
      });
      current.setMinutes(current.getMinutes() + 15); // 15-minute slots
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
            onClick={() => /* Open filter dialog */}
          >
            Lọc
          </Button>
          <ButtonGroup>
            <Button 
              size="small" 
              onClick={handlePrevPeriod}
              disabled={viewType === 'week' && startOfWeek(selectedDate) <= startOfWeek(new Date(), { weekStartsOn: 1 })}
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
          </ButtonGroup>
          <Space>
            <ButtonGroup>
              <Button 
                size="small" 
                active={viewType === 'day'}
                onClick={() => handleViewChange('day')}
              >
                Ngày
              </Button>
              <Button 
                size="small" 
                active={viewType === 'week'}
                onClick={() => handleViewChange('week')}
              >
                Tuần
              </Button>
            </ButtonGroup>
            <DatePicker 
              value={selectedDate} 
              onChange={handleDateSelect}
              showTime={{ format: 'HH:mm' }}
            />
          </Space>
        </Space>
      </div>

      <div className="calendar-grid">
        {/* Time axis */}
        <div className="time-axis">
          {[...Array(24)].map((_, hour) => (
            <div key={hour} className="time-slot">
              {String(hour).padStart(2, '0')}:00
            </div>
          ))}
        </div>

        {/* Date slots */}
        <div className="date-slots">
          {generateTimeSlots(
            visibleDateRange.start, 
            visibleDateRange.end
          ).map((slot, index) => (
            <div key={index} className="date-slot">
              <div className="time-label">{slot.time}</div>
              <div className="appointments">
                {slot.appointments.map(apt => (
                  <div 
                    key={apt.id}
                    className="appointment-card"
                    onClick={() => handleAppointmentClick(apt.id)}
                    title={`${apt.patientName} - ${apt.service}`}
                  >
                    <div className="patient-name">{apt.patientName}</div>
                    <div className="service">{apt.service}</div>
                    <div className="time">{format(new Date(apt.startTime), 'HH:mm')} - {format(new Date(apt.endTime), 'HH:mm')}</div>
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