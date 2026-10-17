import { CircularProgress, Stack, Typography } from "@mui/joy";

interface LoadingStateProps {
  label: string;
  compact?: boolean;
}

const LoadingState = ({ label, compact = false }: LoadingStateProps) => (
  <Stack
    role="status"
    direction="row"
    spacing={1.5}
    alignItems="center"
    sx={{ py: compact ? 1.5 : 3 }}
  >
    <CircularProgress size="sm" aria-label={label} />
    <Typography>{label}</Typography>
  </Stack>
);

export default LoadingState;
