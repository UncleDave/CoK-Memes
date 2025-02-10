import { Container } from "@mui/joy";
import { Outlet, ScrollRestoration } from "react-router";

const Root = () => (
  <Container maxWidth="md" sx={{ pb: 2 }}>
    <Outlet />
    <ScrollRestoration />
  </Container>
);

export default Root;
