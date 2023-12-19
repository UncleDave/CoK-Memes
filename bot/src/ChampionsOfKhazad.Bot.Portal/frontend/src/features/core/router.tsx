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
        element: <div />,
        loader: () => fetch("/api/lore"),
      },
      {
        path: "lore/:name",
        element: <EditLorePage />,
        loader: ({ params }) => fetch(`/api/lore/${params.name}`),
        action: async ({ params, request }) =>
          fetch(`/api/lore/${params.name}`, {
            method: "PUT",
            body: await request.formData(),
          }),
      },
    ],
  },
]);

export default router;
