import { Input } from "@mui/joy";
import FormControl from "./FormControl.tsx";

interface FormInputProps {
  label: string;
  name: string;
  defaultValue?: string;
  required?: boolean;
  helperText?: string;
  disabled?: boolean;
}

const FormInput = ({
  label,
  name,
  defaultValue,
  required,
  helperText,
  disabled,
}: FormInputProps) => (
  <FormControl label={label} helperText={helperText}>
    <Input
      name={name}
      defaultValue={defaultValue}
      required={required}
      disabled={disabled}
    />
  </FormControl>
);

export default FormInput;
