import { Container } from "@mui/joy";
import { Outlet, ScrollRestoration } from "react-router";

const Root = () => (
  <Container maxWidth="xl" sx={{ pb: 2 }}>
    <Outlet />
    <ScrollRestoration />
  </Container>
);

export default Root;
