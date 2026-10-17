import Page from "../core/Page.tsx";
import ImageGrid from "./ImageGrid.tsx";
import { useLoaderData } from "react-router";
import GeneratedImage from "./generated-image.ts";
import { useInfiniteLoader } from "masonic";
import useGeneratedImages from "./use-generated-images.ts";
import { List, ListItem, Input } from "@mui/joy";
import ChoiceChip from "../core/ChoiceChip.tsx";
import { useState } from "react";
import ImageResultsStatus from "./ImageResultsStatus.tsx";

const GeneratedImagesPage = () => {
  const {
    images,
    gridKey,
    mine,
    sortAscending,
    query,
    error,
    hasMore,
    isLoading,
    retry,
    setSkip,
    setTake,
    setMine,
    setSortAscending,
    setQuery,
  } = useGeneratedImages(useLoaderData() as GeneratedImage[]);

  const [searchInput, setSearchInput] = useState(query ?? "");

  const maybeLoadMore = useInfiniteLoader((startIndex, stopIndex) => {
    if (error || isLoading || !hasMore) return;
    const skip = startIndex;
    const take = stopIndex - startIndex + 1;

    setSkip(skip);
    setTake(take);
  });

  const handleSearchChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    setSearchInput(event.target.value);
  };

  const handleSearchKeyDown = (
    event: React.KeyboardEvent<HTMLInputElement>,
  ) => {
    if (event.key === "Enter") {
      setQuery(searchInput);
    }
  };

  const handleSearchClear = () => {
    setSearchInput("");
    setQuery("");
  };

  return (
    <Page title="Images">
      <Input
        placeholder="Search images by prompt..."
        value={searchInput}
        onChange={handleSearchChange}
        onKeyDown={handleSearchKeyDown}
        endDecorator={
          searchInput && (
            <span
              onClick={handleSearchClear}
              style={{ cursor: "pointer", padding: "0 8px" }}
            >
              ✕
            </span>
          )
        }
        sx={{ mb: 2 }}
      />
      <List orientation="horizontal" sx={{ mb: 2, p: 0 }}>
        <ListItem sx={{ mr: 2 }}>
          <ChoiceChip
            label="Only mine"
            value={mine ?? false}
            onChange={setMine}
          />
        </ListItem>
        <ListItem>
          <ChoiceChip
            label="Oldest first"
            value={sortAscending ?? false}
            onChange={setSortAscending}
          />
        </ListItem>
      </List>
      <ImageResultsStatus
        imageCount={images.length}
        isLoading={isLoading}
        error={error}
        mine={mine}
        query={query}
        onRetry={retry}
      />
      {images.length > 0 && (
        <ImageGrid images={images} onRender={maybeLoadMore} key={gridKey} />
      )}
    </Page>
  );
};

export default GeneratedImagesPage;
