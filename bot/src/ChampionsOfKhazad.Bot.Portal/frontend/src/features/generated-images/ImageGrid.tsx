import GeneratedImage from "./generated-image.ts";
import GridImage from "./GridImage.tsx";
import { Masonry } from "masonic";

interface ImageGridItemProps {
  index: number;
  data: GeneratedImage;
  width: number;
}

const ImageGridItem = ({ data, width }: ImageGridItemProps) => (
  <GridImage image={data} width={width} />
);

interface ImageGridProps {
  images: GeneratedImage[];
  onRender?: (
    startIndex: number,
    stopIndex: number,
    items: GeneratedImage[],
  ) => void;
}

const ImageGrid = ({ images, onRender }: ImageGridProps) => (
  <Masonry
    items={images}
    render={ImageGridItem}
    onRender={onRender}
    columnGutter={6}
    rowGutter={0}
    maxColumnCount={4}
  />
);

export default ImageGrid;
