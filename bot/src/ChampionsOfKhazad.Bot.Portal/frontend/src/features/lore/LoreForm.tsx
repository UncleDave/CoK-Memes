import { Button, Stack } from "@mui/joy";
import { PropsWithChildren } from "react";
import { Form, Link } from "react-router";

const LoreForm = ({ children }: PropsWithChildren) => (
  <Form method="put">
    <Stack spacing={1}>
      {children}
      <Stack direction="row" justifyContent="space-between">
        <Stack direction="row" spacing={1}>
          <Button component={Link} to="/lore">
            Cancel
          </Button>
          <Button
            type="submit"
            name="intent"
            value="delete"
            color="danger"
            formMethod="delete"
          >
            Delete
          </Button>
        </Stack>
        <Button type="submit">Save</Button>
      </Stack>
    </Stack>
  </Form>
);

export default LoreForm;
