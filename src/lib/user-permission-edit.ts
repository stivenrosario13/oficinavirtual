// Editing identity must not turn effective role defaults into stored overrides.
export function permissionsForUserSave(
  saved: Record<string, boolean>,
  initial: Record<string, boolean>,
  edited: Record<string, boolean>,
  replaceProfile = false,
): Record<string, boolean> {
  if (replaceProfile) return { ...edited, usesExplicitPermissions: true };
  const changed = Object.keys(edited).filter(key => edited[key] !== initial[key]);
  if (!changed.length) return { ...saved };
  return { ...initial, ...saved, ...Object.fromEntries(changed.map(key => [key, edited[key]])), usesExplicitPermissions: true };
}
