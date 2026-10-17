import { isRouteErrorResponse } from "react-router";
import { ApiError } from "./request-api.ts";

const getRouteErrorDetails = (error: unknown) => {
  const status =
    isRouteErrorResponse(error) || error instanceof ApiError ? error.status : 0;

  switch (status) {
    case 401:
      return {
        title: "Sign in required",
        message: "Reload the portal to sign in again.",
        signInRequired: true,
      };
    case 403:
      return {
        title: "Access denied",
        message: "You don’t have access to this page.",
        signInRequired: false,
      };
    case 404:
      return {
        title: "Page not found",
        message: "This page could not be found.",
        signInRequired: false,
      };
    default:
      return {
        title: "Unable to load page",
        message:
          "Something went wrong while loading this page. Please try again.",
        signInRequired: false,
      };
  }
};

export default getRouteErrorDetails;
