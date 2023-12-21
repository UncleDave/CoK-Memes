import { css } from "@emotion/react";
import { InputHTMLAttributes } from "react";

const Input = (props: InputHTMLAttributes<HTMLInputElement>) => (
  <input
    css={css`
      width: 100%;
    `}
    {...props}
  />
);

export default Input;
