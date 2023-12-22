import { Textarea } from "@mui/joy";
import FormControl from "./FormControl.tsx";

interface FormTextareaProps {
  label: string;
  name: string;
  defaultValue?: string;
  required?: boolean;
  minRows?: number;
  maxRows?: number;
  helperText?: string;
}

const FormTextarea = ({
  label,
  name,
  defaultValue,
  required,
  minRows = 20,
  maxRows,
  helperText,
}: FormTextareaProps) => (
  <FormControl label={label} helperText={helperText}>
    <Textarea
      name={name}
      defaultValue={defaultValue}
      required={required}
      minRows={minRows}
      maxRows={maxRows}
    />
  </FormControl>
);

export default FormTextarea;
