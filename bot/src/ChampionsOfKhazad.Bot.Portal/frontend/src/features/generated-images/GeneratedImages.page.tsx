import Page from "../core/Page.tsx";
import ImageGrid from "./ImageGrid.tsx";
import { useLoaderData } from "react-router";
import GeneratedImage from "./generated-image.ts";
import { useInfiniteLoader } from "masonic";
import useGeneratedImages from "./use-generated-images.ts";
import { List, ListItem } from "@mui/joy";
import ChoiceChip from "../core/ChoiceChip.tsx";

const GeneratedImagesPage = () => {
  const {
    images,
    gridKey,
    mine,
    sortAscending,
    setSkip,
    setTake,
    setMine,
    setSortAscending,
  } = useGeneratedImages(useLoaderData() as GeneratedImage[]);

  const maybeLoadMore = useInfiniteLoader(async (startIndex, stopIndex) => {
    const skip = startIndex;
    const take = stopIndex - startIndex;

    setSkip(skip);
    setTake(take);
  });

  return (
    <Page title="Images">
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
      <ImageGrid images={images} onRender={maybeLoadMore} key={gridKey} />
    </Page>
  );
};

export default GeneratedImagesPage;
