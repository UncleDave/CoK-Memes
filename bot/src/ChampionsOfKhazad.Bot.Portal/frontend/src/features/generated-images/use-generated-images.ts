import GeneratedImage from "./generated-image.ts";
import { useEffect, useState, useCallback } from "react";
import fetchGeneratedImages from "./fetch-generated-images.ts";

type UseGeneratedImages = (
  initialImages: GeneratedImage[],
  initialSkip?: number,
  initialTake?: number,
  initialMine?: boolean,
  initialSortAscending?: boolean,
) => {
  images: GeneratedImage[];
  gridKey: string;
  mine?: boolean;
  sortAscending?: boolean;
  setSkip: (skip: number) => void;
  setTake: (take: number) => void;
  setMine: (mine: boolean) => void;
  setSortAscending: (sortAscending: boolean) => void;
};

const useGeneratedImages: UseGeneratedImages = (
  initialImages,
  initialSkip?,
  initialTake?,
  initialMine?,
  initialSortAscending?,
) => {
  const [images, setImages] = useState<GeneratedImage[]>(initialImages);
  const [skip, setSkip] = useState(initialSkip);
  const [take, setTake] = useState(initialTake);
  const [mine, setMine] = useState(initialMine);
  const [sortAscending, setSortAscending] = useState(initialSortAscending);
  const [gridKey, setGridKey] = useState(() => crypto.randomUUID());

  useEffect(() => {
    if (
      skip === undefined &&
      take === undefined &&
      mine === undefined &&
      sortAscending === undefined &&
      initialImages.length > 0
    ) {
      return;
    }

    const abortController = new AbortController();

    fetchGeneratedImages(
      skip,
      take,
      mine,
      sortAscending,
      abortController.signal,
    ).then((fetchedImages) => {
      setImages((existingImages) => [...existingImages, ...fetchedImages]);
    });

    return () => abortController.abort();
  }, [initialImages.length, mine, skip, sortAscending, take]);

  const resetImages = useCallback(() => {
    setSkip(0);
    setImages([]);
    setGridKey(crypto.randomUUID());
  }, []);

  const setMineCallback = useCallback(
    (newMine: boolean) => {
      setMine(newMine);
      resetImages();
    },
    [resetImages],
  );

  const setSortAscendingCallback = useCallback(
    (newSortAscending: boolean) => {
      setSortAscending(newSortAscending);
      resetImages();
    },
    [resetImages],
  );

  return {
    images,
    gridKey,
    mine,
    sortAscending,
    setSkip,
    setTake,
    setMine: setMineCallback,
    setSortAscending: setSortAscendingCallback,
  };
};

export default useGeneratedImages;
