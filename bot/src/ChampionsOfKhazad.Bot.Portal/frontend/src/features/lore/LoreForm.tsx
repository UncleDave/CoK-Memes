import { Button, Stack } from "@mui/joy";
import { PropsWithChildren } from "react";
import { Form } from "react-router-dom";

const LoreForm = ({ children }: PropsWithChildren) => (
  <Form method="put">
    <Stack spacing={1}>
      {children}
      <Button type="submit" sx={{ alignSelf: "end" }}>
        Save
      </Button>
    </Stack>
  </Form>
);

export default LoreForm;
