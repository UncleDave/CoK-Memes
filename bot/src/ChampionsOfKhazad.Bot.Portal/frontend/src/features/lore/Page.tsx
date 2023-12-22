import { css } from "@emotion/react";
import { Sheet, Typography } from "@mui/joy";
import { PropsWithChildren } from "react";
import Breadcrumbs, { Crumb } from "../core/Breadcrumbs.tsx";

interface PageProps {
  title: string;
  crumbs: Crumb[];
}

const Page = ({ title, crumbs, children }: PropsWithChildren<PageProps>) => (
  <article>
    <Breadcrumbs crumbs={crumbs} />
    <Sheet
      color="primary"
      variant="outlined"
      css={css`
        padding: 10px;
      `}
    >
      <Typography
        level="h1"
        css={css`
          margin-bottom: 10px;
        `}
      >
        {title}
      </Typography>
      {children}
    </Sheet>
  </article>
);

export default Page;
