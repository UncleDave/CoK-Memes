import {
  Button,
  DialogActions,
  DialogContent,
  DialogTitle,
  Modal,
  ModalDialog,
} from "@mui/joy";
import { useId } from "react";

interface UnsavedChangesDialogProps {
  open: boolean;
  onKeepEditing: () => void;
  onDiscard: () => void;
}

const UnsavedChangesDialog = ({
  open,
  onKeepEditing,
  onDiscard,
}: UnsavedChangesDialogProps) => {
  const titleId = useId();
  const descriptionId = useId();

  return (
    <Modal open={open} onClose={onKeepEditing}>
      <ModalDialog
        role="alertdialog"
        aria-labelledby={titleId}
        aria-describedby={descriptionId}
      >
        <DialogTitle id={titleId}>Discard unsaved changes?</DialogTitle>
        <DialogContent id={descriptionId}>
          Your changes will be lost if you leave this page.
        </DialogContent>
        <DialogActions>
          <Button
            autoFocus
            variant="plain"
            color="neutral"
            onClick={onKeepEditing}
          >
            Keep editing
          </Button>
          <Button color="danger" onClick={onDiscard}>
            Discard changes
          </Button>
        </DialogActions>
      </ModalDialog>
    </Modal>
  );
};

export default UnsavedChangesDialog;
