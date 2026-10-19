import { Redirect, useLocalSearchParams } from 'expo-router';

/**
 * Redirect route: forward from (home)/appointments/[id] to official S16 (account)/appointments/[code].
 */
export default function AppointmentDetailRedirect() {
  const params = useLocalSearchParams<{ id?: string; code?: string }>();
  const code = params.code || params.id || '';
  return <Redirect href={`/(account)/appointments/${encodeURIComponent(code)}`} />;
}
