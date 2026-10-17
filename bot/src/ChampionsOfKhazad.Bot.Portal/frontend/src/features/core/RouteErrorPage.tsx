import { Alert, Button, Stack } from "@mui/joy";
import { Link, useLocation, useRevalidator, useRouteError } from "react-router";
import Page from "./Page.tsx";
import getRouteErrorDetails from "./route-error-details.ts";

const RouteErrorPage = () => {
  const { title, message, signInRequired } =
    getRouteErrorDetails(useRouteError());
  const revalidator = useRevalidator();
  const location = useLocation();

  return (
    <Page title={title}>
      <Stack spacing={2}>
        <Alert color="danger" role="alert">
          {message}
        </Alert>
        <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
          {signInRequired ? (
            <Button onClick={() => window.location.reload()}>
              Reload portal
            </Button>
          ) : (
            <Button
              onClick={() => void revalidator.revalidate()}
              loading={revalidator.state === "loading"}
            >
              Retry
            </Button>
          )}
          {location.pathname !== "/images" && (
            <Button component={Link} to="/images" variant="outlined">
              Go to Images
            </Button>
          )}
        </Stack>
      </Stack>
    </Page>
  );
};

export default RouteErrorPage;
