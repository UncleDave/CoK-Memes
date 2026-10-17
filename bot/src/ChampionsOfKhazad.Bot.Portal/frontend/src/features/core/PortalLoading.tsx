import { Box, Container, Skeleton, Stack } from "@mui/joy";
import { useLocation } from "react-router";
import LoadingState from "./LoadingState.tsx";
import Navigation from "./Navigation.tsx";
import Page from "./Page.tsx";

const PortalLoading = () => {
  const { pathname } = useLocation();
  const isImages = pathname === "/" || pathname === "/images";
  const isLore = pathname === "/lore" || pathname.startsWith("/lore/");
  const title = isImages ? "Images" : isLore ? "Lore" : "Portal";

  return (
    <Container maxWidth="xl" sx={{ pb: 2 }}>
      <Navigation user={{ roles: [] }} />
      <Box component="main" aria-busy="true" sx={{ pt: 2 }}>
        <Page title={title}>
          <LoadingState label={`Loading ${title.toLowerCase()}…`} compact />
          <Stack spacing={2} aria-hidden="true">
            <Skeleton variant="rectangular" height={36} />
            <Stack direction="row" spacing={1}>
              <Skeleton variant="rectangular" width={100} height={36} />
              <Skeleton variant="rectangular" width={100} height={36} />
            </Stack>
            {isImages ? (
              <Box
                sx={{
                  display: "grid",
                  gridTemplateColumns: {
                    xs: "1fr",
                    sm: "repeat(2, 1fr)",
                    lg: "repeat(4, 1fr)",
                  },
                  gap: 1,
                }}
              >
                {Array.from({ length: 8 }, (_, index) => (
                  <Skeleton
                    key={index}
                    variant="rectangular"
                    sx={{ width: "100%", height: "auto", aspectRatio: "1 / 1" }}
                  />
                ))}
              </Box>
            ) : (
              Array.from({ length: 6 }, (_, index) => (
                <Skeleton key={index} variant="rectangular" height={48} />
              ))
            )}
          </Stack>
        </Page>
      </Box>
    </Container>
  );
};

export default PortalLoading;
