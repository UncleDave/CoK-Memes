import { css } from "@emotion/react";
import { PropsWithChildren } from "react";
import { Form } from "react-router-dom";

const LoreForm = ({ children }: PropsWithChildren) => (
  <Form method="put">
    {children}
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
