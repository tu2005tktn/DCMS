import { Stack } from 'expo-router';
import AppointmentDetailScreen from '@/features/appointments/screens/AppointmentDetailScreen';

export default function AppointmentDetailRoute() {
  return <>
    <Stack.Screen options={{ headerTitle: 'Chi tiết lịch hẹn' }} />
    <AppointmentDetailScreen />
  </>;
}
