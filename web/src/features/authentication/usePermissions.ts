import { useAuth } from './AuthContext';

export function usePermissions() {
  const { user } = useAuth();
  return (permission: string) => user?.permissions.includes(permission) ?? false;
}
