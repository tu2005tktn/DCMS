import { lazy } from 'react';
import { Navigate } from 'react-router-dom';

// Lazy load components to improve performance
const S10AppointmentCalendar = lazy(() => import('../features/appointments/S10AppointmentCalendar'));
const S11AppointmentBookingForm = lazy(() => import('../features/appointments/S11AppointmentBookingForm'));
const S12AppointmentDetail = lazy(() => import('../features/appointments/S12AppointmentDetail')); // TV5 component

const AppointmentRoutes = () => {
  return (
    <>
      {/* S10: Web appointment calendar view */}
      <Route path="/appointments" element={<S10AppointmentCalendar />} />
      
      {/* S11: Front desk appointment booking form */}
      <Route path="/appointments/book" element={<S11AppointmentBookingForm />} />
      <Route path="/appointments/book/*" element={<S11AppointmentBookingForm />} /> // For pre-filled bookings
      
      {/* S12: Appointment detail view (TV5) */}
      <Route path="/appointments/:id" element={<S12AppointmentDetail />} />
      
      {/* Redirect root appointments to calendar */}
      <Route path="/" element={<Navigate to="/appointments" replace />} />
    </>
  );
};

export default AppointmentRoutes;