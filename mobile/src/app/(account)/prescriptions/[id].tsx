import { Stack } from 'expo-router';
import { CustomerPrescriptionDetailScreen } from '@/features/prescriptions/screens/CustomerPrescriptionsScreen';
export default function PrescriptionDetailRoute() { return <><Stack.Screen options={{ headerTitle: 'Chi tiết đơn thuốc' }} /><CustomerPrescriptionDetailScreen /></>; }
