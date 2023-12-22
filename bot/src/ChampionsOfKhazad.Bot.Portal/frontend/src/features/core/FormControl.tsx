import {
  FormControl as JoyFormControl,
  FormHelperText,
  FormLabel,
} from "@mui/joy";
import { PropsWithChildren } from "react";

interface FormControlProps {
  label: string;
  helperText?: string;
}

const FormControl = ({
  children,
  label,
  helperText,
}: PropsWithChildren<FormControlProps>) => (
  <JoyFormControl>
    <FormLabel>{label}</FormLabel>
    {children}
    {helperText && <FormHelperText>{helperText}</FormHelperText>}
  </JoyFormControl>
);

export default FormControl;
