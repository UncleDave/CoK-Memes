import Page from "../core/Page.tsx";
import ImageGrid from "./ImageGrid.tsx";
import { useLoaderData } from "react-router";
import GeneratedImage from "./generated-image.ts";
import { useInfiniteLoader } from "masonic";
import useGeneratedImages from "./use-generated-images.ts";
import { List, ListItem } from "@mui/joy";
import ChoiceChip from "../core/ChoiceChip.tsx";
import ImageResultsStatus from "./ImageResultsStatus.tsx";
import ImageSearch from "./ImageSearch.tsx";

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

  const maybeLoadMore = useInfiniteLoader((startIndex, stopIndex) => {
    if (error || isLoading || !hasMore) return;
    const skip = startIndex;
    const take = stopIndex - startIndex + 1;

    setSkip(skip);
    setTake(take);
  });

  return (
    <Page title="Images">
      <ImageSearch query={query} onSearch={setQuery} />
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
