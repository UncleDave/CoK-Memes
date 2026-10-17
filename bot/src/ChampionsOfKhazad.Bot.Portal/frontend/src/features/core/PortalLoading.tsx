import { Container } from "@mui/joy";
import LoadingState from "./LoadingState.tsx";

const PortalLoading = () => (
  <Container maxWidth="xl">
    <LoadingState label="Loading portal…" />
  </Container>
);

export default PortalLoading;
