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

      textarea,
      input {
        color: #848d97;
        border: 1px solid #30363d;
        border-radius: 6px;

        &:focus {
          outline: 1px solid #2f81f7;
          border-color: #2f81f7;
        }
      }
    `}
  />
);

export default GlobalStyles;
