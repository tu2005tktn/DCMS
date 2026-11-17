import { Stack } from 'expo-router';
import FamilyMembersScreen from '@/features/family-members/FamilyMembersScreen';

export default function FamilyMembersRoute() {
  return <>
    <Stack.Screen options={{ headerTitle: 'Người thân' }} />
    <FamilyMembersScreen />
  </>;
}
