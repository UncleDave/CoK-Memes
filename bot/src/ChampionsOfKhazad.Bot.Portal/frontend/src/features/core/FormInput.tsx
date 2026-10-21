import { Input } from "@mui/joy";
import { InputHTMLAttributes } from "react";
import FormControl from "./FormControl.tsx";

interface FormInputProps {
  label: string;
  name: string;
  defaultValue?: string;
  required?: boolean;
  helperText?: string;
  disabled?: boolean;
  pattern?: string;
  maxLength?: number;
  inputMode?: InputHTMLAttributes<HTMLInputElement>["inputMode"];
}

const FormInput = ({
  label,
  name,
  defaultValue,
  required,
  helperText,
  disabled,
  pattern,
  maxLength,
  inputMode,
}: FormInputProps) => (
  <FormControl label={label} helperText={helperText}>
    <Input
      name={name}
      defaultValue={defaultValue}
      required={required}
      disabled={disabled}
      slotProps={{ input: { pattern, maxLength, inputMode } }}
    />
  </FormControl>
);

export default FormInput;
