import { Add, Delete } from "@mui/icons-material";
import { FormLabel, IconButton, Input, Stack } from "@mui/joy";
import { useCallback, useState } from "react";

interface InputRowProps {
  id: string;
  value: string;
  name: string;
  removeValue: (key: string) => void;
}

const InputRow = ({ id, value, name, removeValue }: InputRowProps) => (
  <Stack direction="row" gap={1}>
    <Input name={name} defaultValue={value} sx={{ flexGrow: 1 }} />
    <IconButton onClick={() => removeValue(id)}>
      <Delete />
    </IconButton>
  </Stack>
);

interface FormMultiInputProps {
  label: string;
  name: string;
  defaultValues?: string[];
}

const FormMultiInput = ({
  label,
  name,
  defaultValues,
}: FormMultiInputProps) => {
  const [values, setValues] = useState(
    () =>
      defaultValues?.map((x) => ({ key: crypto.randomUUID(), value: x })) ?? [],
  );

  const addValue = useCallback(() => {
    setValues((x) => [...x, { key: crypto.randomUUID(), value: "" }]);
  }, []);

  const removeValue = useCallback((key: string) => {
    setValues((x) => x.filter((y) => y.key !== key));
  }, []);

  return (
    <Stack gap={1}>
      <FormLabel>{label}</FormLabel>
      {values.map(({ key, value }) => (
        <InputRow
          key={key}
          id={key}
          value={value}
          name={name}
          removeValue={removeValue}
        />
      ))}
      <IconButton
        onClick={addValue}
        variant="outlined"
        sx={{ alignSelf: "start" }}
      >
        <Add />
      </IconButton>
    </Stack>
  );
};

export default FormMultiInput;
