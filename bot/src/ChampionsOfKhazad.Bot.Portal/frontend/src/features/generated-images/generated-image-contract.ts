export interface GeneratedImageUserContract {
  name: string;
  avatarUrl: string;
}

export default interface GeneratedImageContract {
  prompt: string;
  user: GeneratedImageUserContract;
  timestamp: string;
  filename: string;
}
