import { css, Global } from "@emotion/react";

const GlobalStyles = () => (
  <Global
    styles={css`
      body {
        background-color: #0d1117;
        color: #e6edf3;
        font-family:
          -apple-system,
          BlinkMacSystemFont,
          avenir next,
          avenir,
          segoe ui,
          helvetica neue,
          helvetica,
          Cantarell,
          Ubuntu,
          roboto,
          noto,
          arial,
          sans-serif;
      }

      textarea {
        color: #848d97;
      }
    `}
  />
);

export default GlobalStyles;
