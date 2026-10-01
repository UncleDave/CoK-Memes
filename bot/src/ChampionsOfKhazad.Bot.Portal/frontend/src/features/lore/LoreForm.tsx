import { Alert, Button, Stack } from "@mui/joy";
import { PropsWithChildren } from "react";
import { Form, Link, useActionData, useNavigation } from "react-router";

const LoreForm = ({
  children,
  isCreating = false,
}: PropsWithChildren<{ isCreating?: boolean }>) => {
  const actionData = useActionData<{ error?: string }>();
  const navigation = useNavigation();
  const isSubmitting = navigation.state === "submitting";

  return (
    <Form method={isCreating ? "post" : "put"}>
      <Stack spacing={1}>
        {actionData?.error && (
          <Alert color="danger" role="alert">
            {actionData.error}
          </Alert>
        )}
        {children}
        <Stack direction="row" justifyContent="space-between">
          <Stack direction="row" spacing={1}>
            <Button component={Link} to="/lore">
              Cancel
            </Button>
            {!isCreating && (
              <Button
                type="submit"
                name="intent"
                value="delete"
                color="danger"
                formMethod="delete"
                disabled={isSubmitting}
              >
                Delete
              </Button>
            )}
          </Stack>
          <Button type="submit" disabled={isSubmitting}>
            Save
          </Button>
        </Stack>
      </Stack>
    </Form>
  );
};

export default LoreForm;
