import { useAuth } from './AuthContext';

// Chỉ chủ phòng khám mới được chuyển/xem nhiều chi nhánh; nhân sự khác làm việc trong chi nhánh được gán
// và không thấy ô chọn chi nhánh (docs/specs/S58-BRANCHES-AND-CHAIRS.md §4).
export function useCanPickBranch(): boolean {
  const { user } = useAuth();
  return !!user?.effectiveRoles.includes('ClinicOwner');
}
