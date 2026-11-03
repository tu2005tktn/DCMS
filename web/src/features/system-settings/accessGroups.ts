export type GrantOption = { key: string; label: string; parent: string | null; requiredRole: string | null; allowedBaseRoles: string[] };
export type AccessGroup = { id: string; code: string; name: string; description: string | null; baseRole: string; active: boolean; version: number; grants: string[]; assignedCount: number };
export type StaffAccess = { employeeId: number; accountId: number; name: string; active: boolean; accountVersion: number; groupId: string | null; assignmentVersion: number; branchIds: number[]; roles: string[]; membershipBranchIds: number[] };
export type AccessCapabilities = { owner: boolean; permissions: string[]; branches: { id: number; name: string }[] };
export type GroupValues = { code: string; name: string; description: string; baseRole: string; grants: string[]; reason: string };
export type GroupPreview = { added: string[]; removed: string[]; affected: number };

// Keep the old aggregate permission readable; edits use the three independently enforced actions.
export function editableGrants(grants: string[], options: GrantOption[]) {
  const values = new Set(grants);
  if (values.has('patients.manage')) ['patients.create', 'patients.update', 'patients.archive'].forEach(key => values.add(key));
  if (values.has('accounts.manage')) ['accounts.read', 'accounts.create', 'accounts.update', 'accounts.state', 'accounts.audit'].forEach(key => values.add(key));
  for (const key of [...values]) {
    let parent = options.find(option => option.key === key)?.parent;
    while (parent) { values.add(parent); parent = options.find(option => option.key === parent)?.parent; }
  }
  return [...values].filter(key => options.some(option => option.key === key));
}
export function toggleGrant(values: string[], key: string, checked: boolean, options: GrantOption[]) {
  const next = new Set(values);
  if (checked) {
    let current: string | null = key;
    while (current) { next.add(current); current = options.find(option => option.key === current)?.parent ?? null; }
  } else {
    const remove = (parent: string) => { next.delete(parent); options.filter(option => option.parent === parent).forEach(option => remove(option.key)); };
    remove(key);
  }
  return [...next];
}
