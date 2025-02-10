import { Button, Stack } from "@mui/joy";
import { PropsWithChildren } from "react";
import { Form, Link } from "react-router";

const LoreForm = ({ children }: PropsWithChildren) => (
  <Form method="put">
    <Stack spacing={1}>
      {children}
      <Stack direction="row" justifyContent="space-between">
        <Button component={Link} to="/lore">
          Cancel
        </Button>
        <Button type="submit">Save</Button>
      </Stack>
    </Stack>
  </Form>
);

export default LoreForm;
