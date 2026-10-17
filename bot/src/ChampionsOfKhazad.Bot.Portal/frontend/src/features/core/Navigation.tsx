import { Box, Button, Stack, Typography } from "@mui/joy";
import { NavLink } from "react-router";
import type UserContract from "./user-contract.ts";

interface NavigationProps {
  user: UserContract;
}

const Navigation = ({ user }: NavigationProps) => (
  <Box
    component="header"
    sx={{
      display: "flex",
      alignItems: "center",
      flexWrap: "wrap",
      gap: 2,
      py: 2,
      borderBottom: "1px solid",
      borderColor: "divider",
    }}
  >
    <Typography level="title-lg">Champions of Khazad</Typography>
    <Stack
      component="nav"
      aria-label="Main navigation"
      direction="row"
      spacing={1}
      sx={{
        "& [aria-current='page']": {
          bgcolor: "primary.softBg",
          color: "primary.softColor",
        },
      }}
    >
      <Button component={NavLink} to="/images" variant="plain" color="neutral">
        Images
      </Button>
      {user.roles.includes("Admin") && (
        <Button component={NavLink} to="/lore" variant="plain" color="neutral">
          Lore
        </Button>
      )}
    </Stack>
  </Box>
);

export default Navigation;
