import { Alert, Button, Sheet, Typography } from "@mui/joy";
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
  if (error || isLoading) {
    const status = error ? (
      <Alert
        color="danger"
        role="alert"
        endDecorator={<Button onClick={onRetry}>Retry</Button>}
      >
        {error}
      </Alert>
    ) : (
      <LoadingState
        label={imageCount ? "Loading more images…" : "Loading images…"}
        compact={imageCount > 0}
      />
    );

    if (!imageCount) return status;

    return (
      <Sheet
        variant="outlined"
        sx={{
          position: "fixed",
          bottom: 16,
          left: "50%",
          transform: "translateX(-50%)",
          maxWidth: "calc(100% - 32px)",
          width: "max-content",
          zIndex: "snackbar",
          boxShadow: "md",
          borderRadius: "sm",
          px: error ? 0 : 2,
        }}
      >
        {status}
      </Sheet>
    );
  }

  if (imageCount > 0) return null;

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
