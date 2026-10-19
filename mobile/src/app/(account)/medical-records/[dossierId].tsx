import { Stack } from 'expo-router';
import MedicalRecordDetailScreen from '@/features/medical-records/MedicalRecordDetailScreen';

export default function MedicalRecordDetailRoute() {
  return <>
    <Stack.Screen options={{ headerTitle: 'Chi tiết hồ sơ' }} />
    <MedicalRecordDetailScreen />
  </>;
}
