import { data, redirect } from "react-router";
import { ApiError } from "../core/request-api.ts";

const submitLore = async (mutation: Promise<Response>) => {
  try {
    await mutation;
    return redirect("/lore");
  } catch (error) {
    // Error status prevents loader revalidation from replacing the form after a failed mutation.
    return data(
      {
        error:
          error instanceof Error
            ? error.message
            : "The request failed. Please try again.",
      },
      { status: error instanceof ApiError ? error.status : 503 },
    );
  }
};

export default submitLore;
