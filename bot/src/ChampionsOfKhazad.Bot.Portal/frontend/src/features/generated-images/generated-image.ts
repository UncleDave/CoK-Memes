import { GeneratedImageUserContract } from "./generated-image-contract.ts";

export default interface GeneratedImage {
  filename: string;
  user: GeneratedImageUserContract;
  timestamp: Date;
  prompt: string;
}
