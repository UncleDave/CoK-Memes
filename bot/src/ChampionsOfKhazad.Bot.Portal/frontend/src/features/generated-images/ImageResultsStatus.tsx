import { Alert, Button, Typography } from "@mui/joy";
import LoadingState from "../core/LoadingState.tsx";

interface ImageResultsStatusProps {
  imageCount: number;
  isLoading: boolean;
  error: string | null;
  mine?: boolean;
  query?: string;
  onRetry: () => void;
}

const ImageResultsStatus = ({
  imageCount,
  isLoading,
  error,
  mine,
  query,
  onRetry,
}: ImageResultsStatusProps) => {
  if (error)
    return (
      <Alert
        color="danger"
        role="alert"
        sx={{ mb: 2 }}
        endDecorator={<Button onClick={onRetry}>Retry</Button>}
      >
        {error}
      </Alert>
    );

  if (isLoading)
    return (
      <LoadingState
        label={imageCount ? "Loading more images…" : "Loading images…"}
      />
    );

  if (imageCount) return null;

  return (
    <Typography role="status" sx={{ py: 3 }}>
      {query
        ? "No images match your search."
        : mine
          ? "You haven’t generated any images yet."
          : "No images yet."}
    </Typography>
  );
};

export default ImageResultsStatus;
