// Stable catalog identity stored with a clinical diagnosis, including historical snapshots.
export type ReferenceSnapshot = { id: string; version: number; name: string; code: string | null };
