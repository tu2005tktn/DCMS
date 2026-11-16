import { Stack, useLocalSearchParams } from 'expo-router';
import FamilyMemberFormScreen from '@/features/family-members/FamilyMemberFormScreen';

export default function FamilyMemberFormRoute() {
  const { id } = useLocalSearchParams<{ id?: string }>();
  return <>
    <Stack.Screen options={{ headerTitle: id ? 'Sửa người thân' : 'Thêm người thân' }} />
    <FamilyMemberFormScreen />
  </>;
}
