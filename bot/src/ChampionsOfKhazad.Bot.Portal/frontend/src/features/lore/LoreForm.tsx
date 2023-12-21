import { css } from "@emotion/react";
import { Form } from "react-router-dom";
import LoreContract from "./lore-contract.ts";

interface LoreFormProps {
  lore: LoreContract;
}

const LoreForm = ({ lore }: LoreFormProps) => (
  <Form action={`guild-lore/${lore.name}/update`}>
    <textarea
      name="content"
      defaultValue={lore.content}
      rows={20}
      css={css`
        width: 100%;
      `}
    ></textarea>
  </Form>
);

export default LoreForm;
