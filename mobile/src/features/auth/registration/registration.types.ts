export type RegistrationForm = {
  fullName: string;
  phoneNumber: string;
  email: string;
  password: string;
  confirmPassword: string;
};

export type RegistrationField = keyof RegistrationForm;
export type RegistrationErrors = Partial<Record<RegistrationField, string>>;

export type CredentialPolicy = {
  requiredLength: number;
  maximumLength: number;
  requireDigit: boolean;
  requireLowercase: boolean;
  requireUppercase: boolean;
  requireNonAlphanumeric: boolean;
};

export type RegisterResponse = {
  accountId: number;
  phoneNumber: string;
  notification: { code: string; message: string };
};
