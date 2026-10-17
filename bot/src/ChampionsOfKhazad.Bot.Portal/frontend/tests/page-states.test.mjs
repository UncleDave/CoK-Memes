import assert from "node:assert/strict";
import { after, before, test } from "node:test";
import { createElement } from "react";
import { renderToString } from "react-dom/server";
import { CssVarsProvider } from "@mui/joy";
import { createMemoryRouter, RouterProvider } from "react-router";
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

test("initial portal loading is announced", async () => {
  const { default: PortalLoading } = await server.ssrLoadModule(
    "/src/features/core/PortalLoading.tsx",
  );
  const html = render(createElement(PortalLoading));
  assert.match(html, /role="status"/);
  assert.match(html, /Loading portal…/);
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
