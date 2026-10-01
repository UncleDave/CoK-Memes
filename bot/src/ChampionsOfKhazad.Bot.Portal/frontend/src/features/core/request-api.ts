export class ApiError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.status = status;
  }
}

const requestApi = async (
  path: string,
  options?: RequestInit,
): Promise<Response> => {
  const response = await fetch(path, options);
  if (response.ok) return response;

  const body: unknown = await response.json().catch(() => null);
  if (
    body !== null &&
    typeof body === "object" &&
    "message" in body &&
    typeof body.message === "string"
  ) {
    throw new ApiError(body.message, response.status);
  }

  throw new ApiError(
    `Request failed (${response.status}). Please try again.`,
    response.status,
  );
};

export default requestApi;
