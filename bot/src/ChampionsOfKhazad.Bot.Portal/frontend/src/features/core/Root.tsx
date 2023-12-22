import { Container } from "@mui/joy";
import { Outlet } from "react-router-dom";

const Root = () => (
  <Container maxWidth="md" sx={{ pb: 2 }}>
    <Outlet />
  </Container>
);

export default Root;
