export type Role = 'Admin' | 'Teacher' | 'Student';

export interface AuthDto {
  accessToken: string;
  role: Role;
  userId: string;
  expiresOn: string;
  refreshTokenExpiration: string;
}

export interface LoginRequest {
  email: string;
  password: string;
  role: Role;
}

export type TwoFactorMethod = 'Email' | 'SMS';

/** RFC 9457 problem details as emitted by the API, plus FluentValidation's `errors`. */
export interface ApiProblem {
  status: number;
  title?: string;
  detail?: string;
  code?: string;
  errors?: Record<string, string[]>;
}
