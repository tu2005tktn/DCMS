import { Stack } from 'expo-router';
import MedicalRecordsScreen from '@/features/medical-records/MedicalRecordsScreen';

export default function MedicalRecordsRoute() {
  return <>
    <Stack.Screen options={{ headerTitle: 'Hồ sơ y tế của tôi' }} />
    <MedicalRecordsScreen />
  </>;
}
