import GeneratedImage from "./generated-image.ts";
import { useEffect, useReducer, useCallback } from "react";
import fetchGeneratedImages from "./fetch-generated-images.ts";
import { createImageState, imageReducer } from "./generated-images-state.ts";

const useGeneratedImages = (
  initialImages: GeneratedImage[],
  initialSkip?: number,
  initialTake?: number,
  initialMine?: boolean,
  initialSortAscending?: boolean,
  initialQuery?: string,
) => {
  const [state, dispatch] = useReducer(
    imageReducer,
    {
      initialImages,
      initialSkip,
      initialTake,
      initialMine,
      initialSortAscending,
      initialQuery,
    },
    (initial) =>
      createImageState(initial.initialImages, {
        skip: initial.initialSkip,
        take: initial.initialTake,
        mine: initial.initialMine,
        sortAscending: initial.initialSortAscending,
        query: initial.initialQuery,
      }),
  );
  const { request } = state;

  useEffect(() => {
    // The route loader has already fetched the initial, unfiltered page.
    if (request.id === 0) return;
    const abortController = new AbortController();
    const { skip, take, mine, sortAscending, query } = request.parameters;

    fetchGeneratedImages(
      skip,
      take,
      mine,
      sortAscending,
      query,
      abortController.signal,
    )
      .then((images) => {
        if (!abortController.signal.aborted)
          dispatch({ type: "loaded", requestId: request.id, images });
      })
      .catch((error: unknown) => {
        if (!abortController.signal.aborted)
          dispatch({
            type: "failed",
            requestId: request.id,
            error:
              error instanceof Error ? error.message : "Failed to load images.",
          });
      });

    return () => abortController.abort();
  }, [request]);

  const setSkip = useCallback(
    (skip: number) => dispatch({ type: "page", skip }),
    [],
  );
  const setTake = useCallback(
    (take: number) => dispatch({ type: "page", take }),
    [],
  );
  const setMine = useCallback(
    (value: boolean) => dispatch({ type: "filter", filter: "mine", value }),
    [],
  );
  const setSortAscending = useCallback(
    (value: boolean) =>
      dispatch({ type: "filter", filter: "sortAscending", value }),
    [],
  );
  const setQuery = useCallback(
    (value: string) => dispatch({ type: "query", value }),
    [],
  );
  const retry = useCallback(() => dispatch({ type: "retry" }), []);

  return {
    images: state.images,
    gridKey: state.gridKey.toString(),
    error: state.error,
    hasMore: state.hasMore,
    isLoading: state.isLoading,
    mine: request.parameters.mine,
    sortAscending: request.parameters.sortAscending,
    query: request.parameters.query,
    setSkip,
    setTake,
    setMine,
    setSortAscending,
    setQuery,
    retry,
  };
};

export default useGeneratedImages;
