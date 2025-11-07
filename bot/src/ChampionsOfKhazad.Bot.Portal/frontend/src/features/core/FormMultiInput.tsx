import { Add, Delete } from "@mui/icons-material";
import { FormLabel, IconButton, Input, Stack } from "@mui/joy";
import { forwardRef, useCallback, useRef, useState } from "react";

interface InputRowProps {
  id: string;
  value: string;
  name: string;
  removeValue: (key: string) => void;
  shouldFocus?: boolean;
}

const InputRow = forwardRef<HTMLInputElement, InputRowProps>(
  ({ id, value, name, removeValue, shouldFocus }, ref) => {
    const localRef = useRef<HTMLInputElement>(null);

    const handleRef = useCallback(
      (element: HTMLInputElement | null) => {
        // Set the local ref
        localRef.current = element;

        // Forward the ref if provided
        if (ref) {
          if (typeof ref === "function") {
            ref(element);
          } else {
            (ref as React.MutableRefObject<HTMLInputElement | null>).current =
              element;
          }
        }

        // Focus if needed
        if (shouldFocus && element) {
          element.focus();
        }
      },
      [ref, shouldFocus],
    );

    return (
      <Stack direction="row" gap={1}>
        <Input
          slotProps={{ input: { ref: handleRef } }}
          name={name}
          defaultValue={value}
          sx={{ flexGrow: 1 }}
        />
        <IconButton onClick={() => removeValue(id)}>
          <Delete />
        </IconButton>
      </Stack>
    );
  },
);

InputRow.displayName = "InputRow";

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
  const [lastAddedKey, setLastAddedKey] = useState<string | null>(null);

  const addValue = useCallback(() => {
    const newKey = crypto.randomUUID();
    setValues((x) => [...x, { key: newKey, value: "" }]);
    setLastAddedKey(newKey);
  }, []);

  const removeValue = useCallback(
    (key: string) => {
      setValues((x) => x.filter((y) => y.key !== key));
      if (key === lastAddedKey) {
        setLastAddedKey(null);
      }
    },
    [lastAddedKey],
  );

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
          shouldFocus={key === lastAddedKey}
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
