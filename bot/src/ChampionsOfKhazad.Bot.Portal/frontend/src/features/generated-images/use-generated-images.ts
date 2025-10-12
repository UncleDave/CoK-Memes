import GeneratedImage from "./generated-image.ts";
import { useEffect, useState, useCallback } from "react";
import fetchGeneratedImages from "./fetch-generated-images.ts";

type UseGeneratedImages = (
  initialImages: GeneratedImage[],
  initialSkip?: number,
  initialTake?: number,
  initialMine?: boolean,
  initialSortAscending?: boolean,
  initialQuery?: string,
) => {
  images: GeneratedImage[];
  gridKey: string;
  mine?: boolean;
  sortAscending?: boolean;
  query?: string;
  setSkip: (skip: number) => void;
  setTake: (take: number) => void;
  setMine: (mine: boolean) => void;
  setSortAscending: (sortAscending: boolean) => void;
  setQuery: (query: string) => void;
};

const useGeneratedImages: UseGeneratedImages = (
  initialImages,
  initialSkip?,
  initialTake?,
  initialMine?,
  initialSortAscending?,
  initialQuery?,
) => {
  const [images, setImages] = useState<GeneratedImage[]>(initialImages);
  const [skip, setSkip] = useState(initialSkip);
  const [take, setTake] = useState(initialTake);
  const [mine, setMine] = useState(initialMine);
  const [sortAscending, setSortAscending] = useState(initialSortAscending);
  const [query, setQuery] = useState(initialQuery);
  const [gridKey, setGridKey] = useState(() => crypto.randomUUID());

  useEffect(() => {
    if (
      skip === undefined &&
      take === undefined &&
      mine === undefined &&
      sortAscending === undefined &&
      query === undefined &&
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
      query,
      abortController.signal,
    ).then((fetchedImages) => {
      setImages((existingImages) => [...existingImages, ...fetchedImages]);
    });

    return () => abortController.abort();
  }, [initialImages.length, mine, skip, sortAscending, take, query]);

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

  const setQueryCallback = useCallback(
    (newQuery: string) => {
      setQuery(newQuery);
      resetImages();
    },
    [resetImages],
  );

  return {
    images,
    gridKey,
    mine,
    sortAscending,
    query,
    setSkip,
    setTake,
    setMine: setMineCallback,
    setSortAscending: setSortAscendingCallback,
    setQuery: setQueryCallback,
  };
};

export default useGeneratedImages;
