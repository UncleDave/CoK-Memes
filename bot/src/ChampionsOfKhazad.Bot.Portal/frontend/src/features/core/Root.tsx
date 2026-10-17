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
          <LinearProgress aria-label="Loading page" sx={{ mb: 2 }} />
        )}
        <Outlet />
      </Box>
      <ScrollRestoration />
    </Container>
  );
};

export default Root;
