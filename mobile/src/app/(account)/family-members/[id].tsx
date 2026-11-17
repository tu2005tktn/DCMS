import { Stack } from 'expo-router';
import FamilyMemberDetailScreen from '@/features/family-members/FamilyMemberDetailScreen';

export default function FamilyMemberDetailRoute() {
  return <>
    <Stack.Screen options={{ headerTitle: 'Người thân' }} />
    <FamilyMemberDetailScreen />
  </>;
}
