import { Alert, Button, Stack, Typography } from "@mui/joy";
import { PropsWithChildren } from "react";
import { Form, Link, useActionData, useNavigation } from "react-router";
import useUnsavedLore from "./use-unsaved-lore.ts";
import UnsavedChangesDialog from "./UnsavedChangesDialog.tsx";

const LoreForm = ({
  children,
  isCreating = false,
}: PropsWithChildren<{ isCreating?: boolean }>) => {
  const actionData = useActionData<{ error?: string }>();
  const navigation = useNavigation();
  const isSubmitting = navigation.state === "submitting";
  const { formRef, isDirty, updateDirty, blocker } = useUnsavedLore();

  return (
    <Form
      ref={formRef}
      method={isCreating ? "post" : "put"}
      onInput={updateDirty}
      onChange={updateDirty}
    >
      <Stack spacing={1}>
        {actionData?.error && (
          <Alert color="danger" role="alert">
            {actionData.error}
          </Alert>
        )}
        {children}
        {isDirty && (
          <Typography level="body-sm" color="warning" role="status">
            Unsaved changes
          </Typography>
        )}
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
      <UnsavedChangesDialog
        open={blocker.state === "blocked"}
        onKeepEditing={() => {
          if (blocker.state === "blocked") blocker.reset();
        }}
        onDiscard={() => {
          if (blocker.state === "blocked") blocker.proceed();
        }}
      />
    </Form>
  );
};

export default LoreForm;
