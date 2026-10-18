import { Redirect } from 'expo-router';

/**
 * Redirect route: forward from (home)/appointments to official S14 (account)/appointments.
 */
export default function AppointmentHubRedirect() {
  return <Redirect href="/(account)/appointments" />;
}
