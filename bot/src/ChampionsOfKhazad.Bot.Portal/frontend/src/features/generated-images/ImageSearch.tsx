import { Box, Button, IconButton, Input, SvgIcon } from "@mui/joy";
import { useRef, useState } from "react";

interface ImageSearchProps {
  query?: string;
  onSearch: (query: string) => void;
}

const ImageSearch = ({ query, onSearch }: ImageSearchProps) => {
  const [searchInput, setSearchInput] = useState(query ?? "");
  const inputRef = useRef<HTMLInputElement>(null);

  const clearSearch = () => {
    setSearchInput("");
    onSearch("");
    inputRef.current?.focus();
  };

  return (
    <Box
      component="form"
      role="search"
      aria-label="Image search"
      onSubmit={(event) => {
        event.preventDefault();
        onSearch(searchInput.trim());
      }}
      sx={{ display: "flex", flexWrap: "wrap", gap: 1, mb: 2 }}
    >
      <Input
        placeholder="Search images by prompt…"
        value={searchInput}
        onChange={(event) => setSearchInput(event.target.value)}
        slotProps={{
          input: { ref: inputRef, "aria-label": "Search images by prompt" },
        }}
        endDecorator={
          <IconButton
            type="button"
            size="sm"
            variant="plain"
            color="neutral"
            aria-label="Clear image search"
            disabled={!searchInput && !query}
            onClick={clearSearch}
          >
            <SvgIcon>
              <path d="M19 6.41 17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z" />
            </SvgIcon>
          </IconButton>
        }
        sx={{ flex: "1 1 160px", minWidth: 0 }}
      />
      <Button type="submit">Search</Button>
    </Box>
  );
};

export default ImageSearch;
