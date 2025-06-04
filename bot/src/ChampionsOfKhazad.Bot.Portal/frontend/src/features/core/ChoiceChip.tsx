import { Done } from "@mui/icons-material";
import { Box, Checkbox } from "@mui/joy";

interface ChoiceChipProps {
  label: string;
  value: boolean;
  onChange: (value: boolean) => void;
}

const ChoiceChip = ({ label, value, onChange }: ChoiceChipProps) => (
  <Box display="flex" gap={0.5}>
    {value && <Done fontSize="medium" color="primary" sx={{ zIndex: 2 }} />}
    <Checkbox
      checked={value}
      onChange={(event) => onChange(event.target.checked)}
      label={label}
      disableIcon
      overlay
      variant={value ? "soft" : "outlined"}
    />
  </Box>
);

export default ChoiceChip;
