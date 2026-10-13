// Domestic Vietnamese mobile format used by UC-33. Format is not proof of ownership.
const vietnameseMobilePattern = /^0[35789][0-9]{8}$/;

export function normalizeVietnameseMobile(value: string): string {
  return value.trim();
}

export function isVietnameseMobile(value: string): boolean {
  return vietnameseMobilePattern.test(normalizeVietnameseMobile(value));
}
