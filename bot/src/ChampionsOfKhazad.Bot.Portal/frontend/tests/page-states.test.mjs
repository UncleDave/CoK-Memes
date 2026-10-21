import assert from "node:assert/strict";
import { after, before, test } from "node:test";
import { createElement } from "react";
import { renderToString } from "react-dom/server";
import { CssVarsProvider } from "@mui/joy";
import { createMemoryRouter, MemoryRouter, RouterProvider } from "react-router";
import { createServer } from "vite";
import { ApiError } from "../src/features/core/request-api.ts";
import getRouteErrorDetails from "../src/features/core/route-error-details.ts";

let server;
before(async () => {
  server = await createServer({ server: { middlewareMode: true, ws: false } });
});
after(async () => server?.close());

const render = (element) =>
  renderToString(createElement(CssVarsProvider, null, element));

const renderImageStatus = async (props) => {
  const { default: ImageResultsStatus } = await server.ssrLoadModule(
    "/src/features/generated-images/ImageResultsStatus.tsx",
  );
  return render(
    createElement(ImageResultsStatus, {
      imageCount: 0,
      isLoading: false,
      error: null,
      onRetry: () => {},
      ...props,
    }),
  );
};

test("empty image results distinguish a new gallery, search, and Only mine", async () => {
  assert.match(await renderImageStatus({}), /No images yet\./);
  assert.match(
    await renderImageStatus({ query: "dwarf" }),
    /No images match your search\./,
  );
  assert.match(
    await renderImageStatus({ mine: true }),
    /You haven’t generated any images yet\./,
  );
  assert.doesNotMatch(
    await renderImageStatus({ imageCount: 1 }),
    /role="status"|No images|generated any images/,
  );
});

test("loading never also claims that the results are empty", async () => {
  const html = await renderImageStatus({ isLoading: true });
  assert.match(html, /role="status"/);
  assert.match(html, /Loading images…/);
  assert.doesNotMatch(html, /No images/);
  assert.match(
    await renderImageStatus({ imageCount: 1, isLoading: true }),
    /Loading more images…/,
  );
});

test("image failures retain an alert and retry rather than an empty state", async () => {
  const html = await renderImageStatus({ error: "Image service unavailable" });
  assert.match(html, /role="alert"/);
  assert.match(html, /Image service unavailable/);
  assert.match(html, /Retry/);
  assert.doesNotMatch(html, /No images|Loading images/);
});

test("paging feedback stays outside the gallery layout once images are visible", async () => {
  for (const state of [
    { isLoading: true },
    { error: "Unable to load more images" },
  ]) {
    const html = await renderImageStatus({ imageCount: 20, ...state });
    assert.match(html, /position:fixed/);
    if (state.error) {
      assert.match(html, /role="alert"/);
      assert.match(html, /Retry/);
    } else {
      assert.match(html, /role="status"/);
      assert.match(html, /Loading more images…/);
    }
  }
  assert.doesNotMatch(
    await renderImageStatus({ isLoading: true }),
    /position:fixed/,
  );
  assert.doesNotMatch(
    await renderImageStatus({ error: "Unavailable" }),
    /position:fixed/,
  );
});

test("gallery images reserve a square fallback without overriding their natural aspect ratio", async () => {
  const { default: GridImage } = await server.ssrLoadModule(
    "/src/features/generated-images/GridImage.tsx",
  );
  const html = render(
    createElement(GridImage, {
      width: 300,
      image: {
        url: "https://example.com/image.png",
        prompt: "A dwarf at a forge",
        user: { name: "Tester", avatarUrl: "" },
        timestamp: new Date("2026-10-17T12:00:00Z"),
      },
    }),
  );
  assert.match(html, /aspect-ratio:auto\s+1\s*\/\s*1/);
  assert.match(html, /height:auto/);
  assert.match(html, /alt="A dwarf at a forge"/);
  assert.doesNotMatch(html, /min-height:140px|object-fit:cover/);
});

test("initial loading keeps the portal shell with an announced, page-shaped skeleton", async () => {
  const { default: PortalLoading } = await server.ssrLoadModule(
    "/src/features/core/PortalLoading.tsx",
  );
  for (const [path, title] of [
    ["/images", "Images"],
    ["/lore", "Lore"],
    ["/lore/Grim", "Lore"],
  ]) {
    const html = render(
      createElement(
        MemoryRouter,
        { initialEntries: [path] },
        createElement(PortalLoading),
      ),
    );
    assert.match(html, /Champions of Khazad/);
    assert.match(html, /aria-label="Main navigation"/);
    assert.match(html, /href="\/images"/);
    assert.doesNotMatch(html, /href="\/lore"/);
    assert.match(html, /role="status"/);
    assert.match(html, new RegExp(`Loading ${title.toLowerCase()}…`));
    assert.match(html, /<main[^>]*aria-busy="true"/);
    assert.match(html, /aria-hidden="true"/);
    assert.match(html, /MuiSkeleton-root/);
  }
});

test("route loading overlays progress without displacing the current page", async () => {
  const { default: Root } = await server.ssrLoadModule(
    "/src/features/core/Root.tsx",
  );
  const loading = Promise.withResolvers();
  const router = createMemoryRouter(
    [
      {
        id: "root",
        path: "/",
        loader: () => ({ roles: ["Admin"] }),
        element: createElement(Root),
        children: [
          {
            path: "images",
            element: createElement("h1", null, "Current images"),
          },
          {
            path: "lore",
            loader: () => loading.promise,
            element: createElement("h1", null, "Loaded lore"),
          },
        ],
      },
    ],
    {
      initialEntries: ["/images"],
      hydrationData: { loaderData: { root: { roles: ["Admin"] } } },
    },
  );
  let navigation;
  try {
    const idleHtml = render(createElement(RouterProvider, { router }));
    assert.match(idleHtml, /<main[^>]*aria-busy="false"/);
    assert.doesNotMatch(idleHtml, /aria-label="Loading page"/);

    navigation = router.navigate("/lore");
    assert.equal(router.state.navigation.state, "loading");
    const loadingHtml = render(createElement(RouterProvider, { router }));
    assert.match(loadingHtml, /aria-label="Main navigation"/);
    assert.match(loadingHtml, /<h1>Current images<\/h1>/);
    assert.doesNotMatch(loadingHtml, /Loaded lore/);
    assert.match(loadingHtml, /<main[^>]*aria-busy="true"/);
    assert.match(
      loadingHtml,
      /role="progressbar"[^>]*aria-label="Loading page"/,
    );
    assert.match(loadingHtml, /position:fixed;top:0;left:0;right:0/);
    assert.match(loadingHtml, /pointer-events:none/);

    loading.resolve(null);
    await navigation;
    const loadedHtml = render(createElement(RouterProvider, { router }));
    assert.match(loadedHtml, /<h1>Loaded lore<\/h1>/);
    assert.match(loadedHtml, /<main[^>]*aria-busy="false"/);
    assert.doesNotMatch(loadedHtml, /aria-label="Loading page"/);
  } finally {
    loading.resolve(null);
    await navigation;
    router.dispose();
  }
});

test("route errors distinguish session, permission, and missing-page failures", () => {
  assert.equal(
    getRouteErrorDetails(new ApiError("internal", 401)).title,
    "Sign in required",
  );
  assert.equal(
    getRouteErrorDetails(new ApiError("internal", 403)).title,
    "Access denied",
  );
  assert.equal(
    getRouteErrorDetails(new ApiError("internal", 404)).title,
    "Page not found",
  );
  assert.equal(
    getRouteErrorDetails({
      status: 404,
      statusText: "Not Found",
      internal: false,
      data: null,
    }).title,
    "Page not found",
  );
  assert.equal(
    getRouteErrorDetails(new Error("secret details")).title,
    "Unable to load page",
  );
  assert.doesNotMatch(
    getRouteErrorDetails(new Error("secret details")).message,
    /secret details/,
  );
});

for (const status of [401, 403, 404, 503]) {
  test(`the ${status} error screen offers the appropriate recovery action`, async () => {
    const { default: RouteErrorPage } = await server.ssrLoadModule(
      "/src/features/core/RouteErrorPage.tsx",
    );
    const { ApiError: SsrApiError } = await server.ssrLoadModule(
      "/src/features/core/request-api.ts",
    );
    const router = createMemoryRouter(
      [{ id: "page", path: "*", errorElement: createElement(RouteErrorPage) }],
      {
        initialEntries: ["/lore"],
        hydrationData: {
          errors: { page: new SsrApiError("internal", status) },
        },
      },
    );
    try {
      const html = render(createElement(RouterProvider, { router }));
      assert.match(html, /role="alert"/);
      assert.match(html, status === 401 ? /Reload portal/ : /Retry/);
      assert.match(html, /href="\/images"/);
      assert.doesNotMatch(html, /internal/);
    } finally {
      router.dispose();
    }
  });
}
