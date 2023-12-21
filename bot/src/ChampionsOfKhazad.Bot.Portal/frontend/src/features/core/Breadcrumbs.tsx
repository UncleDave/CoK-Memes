import { css } from "@emotion/react";
import { Link } from "react-router-dom";

interface Crumb {
  label: string;
  to?: string;
}

interface BreadcrumbsProps {
  crumbs: Crumb[];
  className?: string;
}

const Breadcrumbs = ({ crumbs, className }: BreadcrumbsProps) => (
  <ul
    className={className}
    css={css`
      list-style: none;
      padding: 0;
    `}
  >
    {crumbs.map(({ label, to }) => (
      <li
        key={label}
        css={css`
          display: inline-block;
          margin-right: 0.5rem;

          &:not(:last-of-type)::after {
            content: ">";
            margin-left: 0.5rem;
          }
        `}
      >
        {to ? (
          <Link
            to={to}
            css={css`
              &:visited {
                color: #2f81f7;
              }
            `}
          >
            {label}
          </Link>
        ) : (
          label
        )}
      </li>
    ))}
  </ul>
);

export default Breadcrumbs;
