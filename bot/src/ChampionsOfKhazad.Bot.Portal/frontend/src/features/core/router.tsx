import { createBrowserRouter, redirect } from "react-router-dom";
import EditLorePage from "../lore/EditLore.page.tsx";
import LorePage from "../lore/Lore.page.tsx";
import api from "./api.ts";
import Root from "./Root.tsx";

const router = createBrowserRouter([
  {
    path: "/",
    element: <Root />,
    children: [
      {
        path: "lore",
        element: <LorePage />,
        loader: () => fetch("/api/lore"),
      },
      {
        path: "lore/:name",
        element: <EditLorePage />,
        loader: ({ params }) => fetch(`/api/lore/${params.name}`),
        action: async ({ params, request }) => {
          await api.update(`/api/guild-lore/${params.name}`, request);
          return redirect("/lore");
        },
      },
    ],
  },
]);

export default router;

// TODO: Action for saving member lore
