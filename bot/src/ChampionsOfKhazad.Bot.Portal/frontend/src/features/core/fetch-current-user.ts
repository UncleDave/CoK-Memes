import requestApi from "./request-api.ts";
import type UserContract from "./user-contract.ts";

const fetchCurrentUser = async (
  signal?: AbortSignal,
): Promise<UserContract> => {
  const response = await requestApi("/api/me", { signal });
  return response.json();
};

export default fetchCurrentUser;
