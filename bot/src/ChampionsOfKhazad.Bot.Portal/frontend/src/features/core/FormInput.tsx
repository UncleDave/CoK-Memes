import { Input } from "@mui/joy";
import FormControl from "./FormControl.tsx";

interface FormInputProps {
  label: string;
  name: string;
  defaultValue?: string;
  required?: boolean;
  helperText?: string;
}

const FormInput = ({
  label,
  name,
  defaultValue,
  required,
  helperText,
}: FormInputProps) => (
  <FormControl label={label} helperText={helperText}>
    <Input name={name} defaultValue={defaultValue} required={required} />
  </FormControl>
);

export default FormInput;
