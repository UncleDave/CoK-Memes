import { CircularProgress, Stack, Typography } from "@mui/joy";

interface LoadingStateProps {
  label: string;
}

const LoadingState = ({ label }: LoadingStateProps) => (
  <Stack
    role="status"
    direction="row"
    spacing={1.5}
    alignItems="center"
    sx={{ py: 3 }}
  >
    <CircularProgress size="sm" aria-label={label} />
    <Typography>{label}</Typography>
  </Stack>
);

export default LoadingState;
