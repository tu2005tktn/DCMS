import { Stack } from 'expo-router';
import { CustomerPrescriptionsScreen } from '@/features/prescriptions/screens/CustomerPrescriptionsScreen';
export default function PrescriptionsRoute() { return <><Stack.Screen options={{ headerTitle: 'Đơn thuốc của tôi' }} /><CustomerPrescriptionsScreen /></>; }
