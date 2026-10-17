export type ServerMessage = { code: string; message: string };

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    /** Stable message code from the API (for example MSG108); empty when the server gave none. */
    public readonly code: string = '',
    public readonly serverMessage?: ServerMessage,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}
