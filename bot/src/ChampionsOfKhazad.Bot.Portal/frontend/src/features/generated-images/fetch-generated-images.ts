import GeneratedImage from "./generated-image.ts";
import GeneratedImageContract from "./generated-image-contract.ts";

const path = "/api/generated-images";

const fetchGeneratedImages = async (
  skip?: number,
  take?: number,
  mine?: boolean,
  sortAscending?: boolean,
  abortSignal?: AbortSignal,
): Promise<GeneratedImage[]> => {
  const params = new URLSearchParams();

  if (skip !== undefined) params.append("skip", skip.toString());
  if (take !== undefined) params.append("take", take.toString());
  if (mine) params.append("mine", "true");
  if (sortAscending) params.append("sortAscending", "true");

  const url = params.size ? `${path}?${params.toString()}` : path;

  const res = await fetch(url, { signal: abortSignal });

  if (!res.ok) {
    throw new Error("Failed to load images");
  }

  const contracts: GeneratedImageContract[] = await res.json();

  return contracts.map<GeneratedImage>((x) => new GeneratedImage(x));
};

export default fetchGeneratedImages;
