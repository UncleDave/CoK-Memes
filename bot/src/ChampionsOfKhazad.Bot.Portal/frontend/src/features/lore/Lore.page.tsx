import {
  Button,
  List,
  ListItem,
  ListItemButton,
  ListItemContent,
  Stack,
  Typography,
  Input,
  Box,
} from "@mui/joy";
import { Link, useLoaderData } from "react-router";
import Page from "../core/Page.tsx";
import { useRef, useState } from "react";
import filterLore from "./filter-lore.ts";

const LorePage = () => {
  const lore = useLoaderData() as Array<{ name: string }>;
  const [query, setQuery] = useState("");
  const inputRef = useRef<HTMLInputElement>(null);
  const filteredLore = filterLore(lore, query);

  return (
    <Page title="Lore" crumbs={[{ label: "Lore" }]}>
      <Stack spacing={2}>
        <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
          <Button component={Link} to="new?type=guild" variant="solid">
            Create Guild Lore
          </Button>
          <Button component={Link} to="new?type=member" variant="solid">
            Create Member Lore
          </Button>
        </Stack>
        <Box role="search" aria-label="Lore search">
          <Input
            placeholder="Search lore by name…"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            slotProps={{
              input: { ref: inputRef, "aria-label": "Search lore by name" },
            }}
            endDecorator={
              <Button
                type="button"
                size="sm"
                variant="plain"
                aria-label="Clear lore search"
                disabled={!query}
                onClick={() => {
                  setQuery("");
                  inputRef.current?.focus();
                }}
              >
                Clear
              </Button>
            }
          />
        </Box>
        <Typography level="body-sm" role="status">
          {lore.length === 0
            ? "No lore yet."
            : filteredLore.length === 0
              ? "No lore matches your search."
              : `${filteredLore.length} ${filteredLore.length === 1 ? "entry" : "entries"}`}
        </Typography>
        <List aria-label="Lore entries">
          {filteredLore.map((x) => (
            <ListItem key={x.name}>
              <ListItemButton component={Link} to={encodeURIComponent(x.name)}>
                <ListItemContent sx={{ overflowWrap: "anywhere" }}>
                  {x.name}
                </ListItemContent>
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
