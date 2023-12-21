import { css } from "@emotion/react";
import { PropsWithChildren } from "react";

const Panel = ({ children }: PropsWithChildren) => (
  <div
    css={css`
      border: 1px solid #30363d;
      border-radius: 6px;
      padding: 10px;
    `}
  >
    {children}
  </div>
);

export default Panel;
