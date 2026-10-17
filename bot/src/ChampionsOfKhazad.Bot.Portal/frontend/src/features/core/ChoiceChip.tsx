import { Box, Checkbox, SvgIcon } from "@mui/joy";

interface ChoiceChipProps {
  label: string;
  value: boolean;
  onChange: (value: boolean) => void;
}

const ChoiceChip = ({ label, value, onChange }: ChoiceChipProps) => (
  <Box display="flex" gap={0.5}>
    {value && (
      <SvgIcon fontSize="md" color="primary" sx={{ zIndex: 2 }}>
        <path d="M9 16.17 4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z" />
      </SvgIcon>
    )}
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
