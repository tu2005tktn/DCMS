import { Stack } from 'expo-router';
import VisitResultDetailScreen from '@/features/visit-results/VisitResultDetailScreen';

export default function VisitResultDetailRoute() {
  return <>
    <Stack.Screen options={{ headerTitle: 'Chi tiết lượt khám' }} />
    <VisitResultDetailScreen />
  </>;
}
