import { createBrowserRouter, Navigate } from "react-router";
import EditLorePage from "../lore/EditLore.page.tsx";
import LorePage from "../lore/Lore.page.tsx";
import api from "./api.ts";
import Root from "./Root.tsx";
import GeneratedImagesPage from "../generated-images/GeneratedImages.page.tsx";
import fetchGeneratedImages from "../generated-images/fetch-generated-images.ts";
import requestApi from "./request-api.ts";
import submitLore from "../lore/submit-lore.ts";

const router = createBrowserRouter([
  {
    path: "/",
    element: <Root />,
    children: [
      {
        index: true,
        element: <Navigate to="/images" replace={true} />,
      },
      {
        path: "lore",
        element: <LorePage />,
        loader: ({ request }) =>
          requestApi("/api/lore", { signal: request.signal }),
      },
      {
        path: "lore/new",
        element: <EditLorePage />,
        action: async ({ request }) => {
          const formData = await request.formData();
          const url = new URL(request.url);
          const type = url.searchParams.get("type");

          return submitLore(
            type === "member"
              ? api.createMemberLore(formData)
              : api.createGuildLore(formData),
          );
        },
      },
      {
        path: "lore/:name",
        element: <EditLorePage />,
        loader: ({ params, request }) =>
          requestApi(`/api/lore/${encodeURIComponent(params.name!)}`, {
            signal: request.signal,
          }),
        action: async ({ params, request }) => {
          const formData = await request.formData();

          if (request.method === "DELETE") {
            return submitLore(api.deleteLore(params.name!));
          }

          return submitLore(
            formData.has("mainCharacter")
              ? api.updateMemberLore(params.name!, formData)
              : api.updateGuildLore(params.name!, formData),
          );
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
