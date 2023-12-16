import { createBrowserRouter } from "react-router-dom";
import EditLorePage from "../lore/EditLore.page.tsx";
import Root from "./Root.tsx";

const router = createBrowserRouter([
  {
    path: "/",
    element: <Root />,
    children: [
      {
        path: "lore",
      },
      {
        path: "lore/:name",
        element: <EditLorePage />,
        loader: ({ params }) => fetch(`/api/lore/${params.name}`),
      },
    ],
  },
]);

export default router;
