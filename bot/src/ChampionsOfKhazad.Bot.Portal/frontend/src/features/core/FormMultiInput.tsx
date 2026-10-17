import { FormLabel, IconButton, Input, Stack, SvgIcon } from "@mui/joy";
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
            ref.current = element;
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
        <IconButton
          onClick={() => removeValue(id)}
          aria-label={`Remove ${name}`}
        >
          <SvgIcon>
            <path d="M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z" />
          </SvgIcon>
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

  const removeValue = useCallback((key: string) => {
    setValues((x) => x.filter((y) => y.key !== key));
    setLastAddedKey((current) => (current === key ? null : current));
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
          shouldFocus={key === lastAddedKey}
        />
      ))}
      <IconButton
        onClick={addValue}
        variant="outlined"
        sx={{ alignSelf: "start" }}
        aria-label={`Add ${name}`}
      >
        <SvgIcon>
          <path d="M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z" />
        </SvgIcon>
      </IconButton>
    </Stack>
  );
};

export default FormMultiInput;
