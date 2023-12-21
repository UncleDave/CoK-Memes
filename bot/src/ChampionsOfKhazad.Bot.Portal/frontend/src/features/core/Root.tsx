import { css } from "@emotion/react";
import { Outlet } from "react-router-dom";

const Root = () => (
  <div
    css={css`
      margin: 20px auto 0;
      max-width: 800px;
    `}
  >
    <Outlet />
  </div>
);

export default Root;
