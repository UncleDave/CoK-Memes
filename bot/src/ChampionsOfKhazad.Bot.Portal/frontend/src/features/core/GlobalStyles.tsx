import { css, Global } from "@emotion/react";

const GlobalStyles = () => (
  <Global
    styles={css`
      body {
        background-color: #0d1117;
      }
    `}
  />
);

export default GlobalStyles;
