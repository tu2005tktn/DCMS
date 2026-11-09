import { Stack } from 'expo-router';
import PrescriptionsScreen from '@/features/prescriptions/PrescriptionsScreen';

export default function PrescriptionsRoute() {
  return <>
    <Stack.Screen options={{ headerTitle: 'Đơn thuốc của tôi' }} />
    <PrescriptionsScreen />
  </>;
}
