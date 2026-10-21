import { Box, Container, LinearProgress } from "@mui/joy";
import {
  Outlet,
  ScrollRestoration,
  useLoaderData,
  useNavigation,
} from "react-router";
import Navigation from "./Navigation.tsx";
import type UserContract from "./user-contract.ts";

const Root = () => {
  const user = useLoaderData() as UserContract;
  const navigation = useNavigation();

  return (
    <Container maxWidth="xl" sx={{ pb: 2 }}>
      <Navigation user={user} />
      <Box
        component="main"
        aria-busy={navigation.state !== "idle"}
        sx={{ pt: 2 }}
      >
        {navigation.state === "loading" && (
          <LinearProgress
            aria-label="Loading page"
            thickness={3}
            sx={{
              position: "fixed",
              top: 0,
              left: 0,
              right: 0,
              zIndex: "snackbar",
              borderRadius: 0,
              pointerEvents: "none",
            }}
          />
        )}
        <Outlet />
      </Box>
      <ScrollRestoration />
    </Container>
  );
};

export default Root;
