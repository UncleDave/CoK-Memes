import { List, ListItem, ListItemButton, ListItemContent } from "@mui/joy";
import { Link, useLoaderData } from "react-router";
import Page from "../core/Page.tsx";

const LorePage = () => {
  const lore = useLoaderData() as Array<{ name: string }>;

  return (
    <Page title="Lore" crumbs={[{ label: "Lore" }]}>
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
    </Page>
  );
};

export default LorePage;
