export interface AuthenticateRequest {
  username: string;
  password: string;
  state: string;
}

export interface CreateSessionRequest {
  code: string;
  state: string;
}

export interface AuthenticationRedirectResponse {
  callbackUrl: string;
}

export interface SessionResponse {
  username: string;
  sessionId: string;
  createdAt: string;
  expiresAt: string;
  remainingSeconds: number;
  status: string;
}

export interface ApiErrorResponse {
  message: string;
}

export interface HealthResponse {
  status: string;
}