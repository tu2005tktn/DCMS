import { Stack } from 'expo-router';
import PrescriptionDetailScreen from '@/features/prescriptions/PrescriptionDetailScreen';

export default function PrescriptionDetailRoute() {
  return <>
    <Stack.Screen options={{ headerTitle: 'Chi tiết đơn thuốc' }} />
    <PrescriptionDetailScreen />
  </>;
}
