import {
  Button,
  List,
  ListItem,
  ListItemButton,
  ListItemContent,
  Stack,
} from "@mui/joy";
import { Link, useLoaderData } from "react-router";
import Page from "../core/Page.tsx";

const LorePage = () => {
  const lore = useLoaderData() as Array<{ name: string }>;

  return (
    <Page title="Lore" crumbs={[{ label: "Lore" }]}>
      <Stack spacing={2}>
        <Stack direction="row" spacing={1}>
          <Button component={Link} to="new?type=guild" variant="solid">
            Create Guild Lore
          </Button>
          <Button component={Link} to="new?type=member" variant="solid">
            Create Member Lore
          </Button>
        </Stack>
        <List>
          {lore.map((x) => (
            <ListItem key={x.name}>
              <ListItemButton component={Link} to={x.name}>
                <ListItemContent>{x.name}</ListItemContent>
                &gt;
              </ListItemButton>
            </ListItem>
          ))}
        </List>
      </Stack>
    </Page>
  );
};

export default LorePage;
