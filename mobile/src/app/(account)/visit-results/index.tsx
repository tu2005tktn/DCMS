import { Stack } from 'expo-router';
import VisitResultsScreen from '@/features/visit-results/VisitResultsScreen';

export default function VisitResultsRoute() {
  return <>
    <Stack.Screen options={{ headerTitle: 'Kết quả khám' }} />
    <VisitResultsScreen />
  </>;
}
