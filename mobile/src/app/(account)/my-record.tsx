import { Stack } from 'expo-router';
import MyRecordScreen from '@/features/patient-link/screens/MyRecordScreen';

export default function MyRecordRoute() {
  return <>
    <Stack.Screen options={{ headerTitle: 'Hồ sơ của tôi' }} />
    <MyRecordScreen />
  </>;
}
