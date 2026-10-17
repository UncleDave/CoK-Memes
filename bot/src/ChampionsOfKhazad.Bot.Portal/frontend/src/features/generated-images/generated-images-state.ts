import type GeneratedImage from "./generated-image.ts";

export interface ImageParameters {
  skip?: number;
  take?: number;
  mine?: boolean;
  sortAscending?: boolean;
  query?: string;
}

interface ImageState {
  images: GeneratedImage[];
  gridKey: number;
  request: { id: number; parameters: ImageParameters };
  error: string | null;
  hasMore: boolean;
}

type ImageAction =
  | { type: "page"; skip?: number; take?: number }
  | { type: "filter"; filter: "mine" | "sortAscending"; value: boolean }
  | { type: "query"; value: string }
  | { type: "loaded"; requestId: number; images: GeneratedImage[] }
  | { type: "failed"; requestId: number; error: string }
  | { type: "retry" };

export const createImageState = (
  images: GeneratedImage[],
  parameters: ImageParameters = {},
): ImageState => ({
  images,
  gridKey: 0,
  request: {
    id: Object.values(parameters).some((value) => value !== undefined) ? 1 : 0,
    parameters,
  },
  error: null,
  hasMore: true,
});

export const imageReducer = (
  state: ImageState,
  action: ImageAction,
): ImageState => {
  const parameters = state.request.parameters;
  switch (action.type) {
    case "loaded":
      if (action.requestId !== state.request.id) return state;
      return {
        ...state,
        images: [...state.images, ...action.images],
        hasMore: action.images.length >= (parameters.take ?? 20),
        error: null,
      };
    case "failed":
      return action.requestId === state.request.id
        ? { ...state, error: action.error }
        : state;
    case "retry":
      return {
        ...state,
        error: null,
        request: { ...state.request, id: state.request.id + 1 },
      };
    case "page": {
      if (!state.hasMore || state.error) return state;
      const next = {
        ...parameters,
        ...(action.skip === undefined ? {} : { skip: action.skip }),
        ...(action.take === undefined ? {} : { take: action.take }),
      };
      if (
        (next.skip ?? 0) === (parameters.skip ?? 0) &&
        (next.take ?? 20) === (parameters.take ?? 20)
      )
        return state;
      return {
        ...state,
        error: null,
        request: { id: state.request.id + 1, parameters: next },
      };
    }
    case "filter":
    case "query": {
      const filter = action.type === "query" ? "query" : action.filter;
      const current = parameters[filter] ?? (filter === "query" ? "" : false);
      if (current === action.value) return state;
      return {
        ...state,
        images: [],
        gridKey: state.gridKey + 1,
        error: null,
        hasMore: true,
        request: {
          id: state.request.id + 1,
          parameters: { ...parameters, skip: 0, [filter]: action.value },
        },
      };
    }
  }
};
