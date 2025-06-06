import GeneratedImageContract, {
  GeneratedImageUserContract,
} from "./generated-image-contract.ts";

const imageRoot = "https://images.championsofkhazad.com";
const imagePath = "generated-images";

export default class GeneratedImage {
  prompt: string;
  user: GeneratedImageUserContract;
  timestamp: Date;
  filename: string;

  get url(): string {
    return `${imageRoot}/${imagePath}/${this.filename}`;
  }

  get srcSet(): string {
    return `
      ${imageRoot}/cdn-cgi/image/fit=scale-down,width=240/${imagePath}/${this.filename} 240w,
      ${imageRoot}/cdn-cgi/image/fit=scale-down,width=320/${imagePath}/${this.filename} 320w,
      ${imageRoot}/cdn-cgi/image/fit=scale-down,width=480/${imagePath}/${this.filename} 480w
    `;
  }

  constructor(contract: GeneratedImageContract) {
    this.prompt = contract.prompt;
    this.user = contract.user;
    this.timestamp = new Date(contract.timestamp);
    this.filename = contract.filename;
  }
}
