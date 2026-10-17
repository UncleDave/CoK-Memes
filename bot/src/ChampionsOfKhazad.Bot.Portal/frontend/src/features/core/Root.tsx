import { Box, Container } from "@mui/joy";
import { Outlet, ScrollRestoration, useLoaderData } from "react-router";
import Navigation from "./Navigation.tsx";
import type UserContract from "./user-contract.ts";

const Root = () => {
  const user = useLoaderData() as UserContract;

  return (
    <Container maxWidth="xl" sx={{ pb: 2 }}>
      <Navigation user={user} />
      <Box component="main" sx={{ pt: 2 }}>
        <Outlet />
      </Box>
      <ScrollRestoration />
    </Container>
  );
};

export default Root;
