import { css } from "@emotion/react";
import { TextareaHTMLAttributes } from "react";

const TextArea = (props: TextareaHTMLAttributes<HTMLTextAreaElement>) => (
  <textarea
    rows={20}
    css={css`
      width: 100%;
      resize: vertical;
    `}
    {...props}
  />
);

export default TextArea;
