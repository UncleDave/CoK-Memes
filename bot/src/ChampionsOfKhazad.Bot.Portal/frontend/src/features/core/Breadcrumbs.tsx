import { Link } from "react-router-dom";
import {
  Breadcrumbs as JoyBreadcrumbs,
  Link as JoyLink,
  Typography,
} from "@mui/joy";

export interface Crumb {
  label: string;
  to?: string;
}

interface BreadcrumbsProps {
  crumbs: Crumb[];
  className?: string;
}

const Breadcrumbs = ({ crumbs, className }: BreadcrumbsProps) => (
  <JoyBreadcrumbs separator=">" className={className}>
    {crumbs.map(({ label, to }) =>
      to ? (
        <JoyLink key={label} component={Link} to={to}>
          {label}
        </JoyLink>
      ) : (
        <Typography key={label}>{label}</Typography>
      ),
    )}
  </JoyBreadcrumbs>
);

export default Breadcrumbs;
