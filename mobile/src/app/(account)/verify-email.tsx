import { Redirect } from 'expo-router';

export default function LegacyEmailVerificationRoute() {
  return <Redirect href="/(account)/home" />;
}
