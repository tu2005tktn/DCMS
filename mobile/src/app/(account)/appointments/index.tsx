import { Stack } from 'expo-router';
import MyAppointmentsScreen from '@/features/appointments/screens/MyAppointmentsScreen';

export default function MyAppointmentsRoute() {
  return <>
    <Stack.Screen options={{ headerTitle: 'Lịch hẹn của tôi' }} />
    <MyAppointmentsScreen />
  </>;
}
