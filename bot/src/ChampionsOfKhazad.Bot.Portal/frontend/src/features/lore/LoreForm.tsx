import { css } from "@emotion/react";
import { Form } from "react-router-dom";
import LoreContract from "./lore-contract.ts";

interface LoreFormProps {
  lore: LoreContract;
}

const LoreForm = ({ lore }: LoreFormProps) => (
  <Form method="put">
    <textarea
      name="content"
      defaultValue={lore.content}
      rows={20}
      css={css`
        width: 100%;
        resize: vertical;
      `}
    />
    <button
      css={css`
        display: block;
        margin-left: auto;
      `}
    >
      Save
    </button>
  </Form>
);

export default LoreForm;
