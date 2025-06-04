import { GeneratedImageUserContract } from "./generated-image-contract.ts";

export default interface GeneratedImage {
  src: string;
  user: GeneratedImageUserContract;
  timestamp: Date;
  prompt: string;
}
