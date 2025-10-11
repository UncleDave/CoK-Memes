import GeneratedImage from "./generated-image.ts";
import GeneratedImageContract from "./generated-image-contract.ts";

const path = "/api/generated-images/search";

const searchGeneratedImages = async (
  query: string,
  take?: number,
  mine?: boolean,
  abortSignal?: AbortSignal,
): Promise<GeneratedImage[]> => {
  const params = new URLSearchParams();

  params.append("query", query);
  if (take !== undefined) params.append("take", take.toString());
  if (mine) params.append("mine", "true");

  const url = `${path}?${params.toString()}`;

  const res = await fetch(url, { signal: abortSignal });

  if (!res.ok) {
    throw new Error("Failed to search images");
  }

  const contracts: GeneratedImageContract[] = await res.json();

  return contracts.map<GeneratedImage>((x) => new GeneratedImage(x));
};

export default searchGeneratedImages;
