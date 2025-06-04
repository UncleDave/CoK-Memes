import { createBrowserRouter, Navigate, redirect } from "react-router";
import EditLorePage from "../lore/EditLore.page.tsx";
import LorePage from "../lore/Lore.page.tsx";
import api from "./api.ts";
import Root from "./Root.tsx";
import GeneratedImagesPage from "../generated-images/GeneratedImages.page.tsx";
import fetchGeneratedImages from "../generated-images/fetch-generated-images.ts";

const router = createBrowserRouter([
  {
    path: "/",
    element: <Root />,
    children: [
      {
        index: true,
        element: <Navigate to="/lore" replace={true} />,
      },
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
          const formData = await request.formData();

          if (formData.has("mainCharacter")) {
            await api.updateMemberLore(params.name!, formData);
          } else {
            await api.updateGuildLore(params.name!, formData);
          }

          return redirect("/lore");
        },
      },
      {
        path: "images",
        element: <GeneratedImagesPage />,
        loader: () => fetchGeneratedImages(),
      },
    ],
  },
]);

export default router;
