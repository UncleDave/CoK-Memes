import assert from "node:assert/strict";
import { after, afterEach, before, mock, test } from "node:test";
import { createElement } from "react";
import { renderToString } from "react-dom/server";
import { CssVarsProvider } from "@mui/joy";
import { createMemoryRouter, RouterProvider } from "react-router";
import { createServer } from "vite";
import fetchCurrentUser from "../src/features/core/fetch-current-user.ts";

let server;
before(async () => {
  server = await createServer({ server: { middlewareMode: true, ws: false } });
});
after(async () => server?.close());
afterEach(() => mock.restoreAll());

const createNavigationRouter = async (roles, path = "/images") => {
  const { default: Navigation } = await server.ssrLoadModule(
    "/src/features/core/Navigation.tsx",
  );
  return createMemoryRouter(
    [
      {
        path: "*",
        element: createElement(Navigation, { user: { roles } }),
      },
    ],
    { initialEntries: [path] },
  );
};

const renderNavigation = (router) =>
  renderToString(
    createElement(
      CssVarsProvider,
      null,
      createElement(RouterProvider, { router }),
    ),
  );

test("members see the active Images link without admin navigation", async () => {
  const router = await createNavigationRouter([]);
  try {
    const html = renderNavigation(router);
    assert.match(html, /<nav[^>]*aria-label="Main navigation"/);
    assert.match(html, /<a[^>]*aria-current="page"[^>]*href="\/images"/);
    assert.doesNotMatch(html, /href="\/lore"/);
  } finally {
    router.dispose();
  }
});

test("admins can navigate between Images and Lore", async () => {
  const router = await createNavigationRouter(["Admin"]);
  try {
    const html = renderNavigation(router);
    assert.match(html, /href="\/images"/);
    assert.match(html, /href="\/lore"/);
    assert.equal((html.match(/aria-current="page"/g) ?? []).length, 1);

    await router.navigate("/lore");
    const loreHtml = renderNavigation(router);
    assert.match(loreHtml, /<a[^>]*aria-current="page"[^>]*href="\/lore"/);
    assert.equal((loreHtml.match(/aria-current="page"/g) ?? []).length, 1);

    await router.navigate("/images");
    assert.match(
      renderNavigation(router),
      /<a[^>]*aria-current="page"[^>]*href="\/images"/,
    );
  } finally {
    router.dispose();
  }
});

for (const path of ["/lore/new?type=member", "/lore/Grim"]) {
  test(`Lore stays active on ${path}`, async () => {
    const router = await createNavigationRouter(["Admin"], path);
    try {
      const html = renderNavigation(router);
      assert.match(html, /<a[^>]*aria-current="page"[^>]*href="\/lore"/);
      assert.equal((html.match(/aria-current="page"/g) ?? []).length, 1);
    } finally {
      router.dispose();
    }
  });
}

test("the current-user request reads roles and forwards cancellation", async () => {
  const controller = new AbortController();
  const fetchMock = mock.method(globalThis, "fetch", async () =>
    Response.json({ roles: ["Admin"] }),
  );

  assert.deepEqual(await fetchCurrentUser(controller.signal), {
    roles: ["Admin"],
  });
  assert.deepEqual(fetchMock.mock.calls[0].arguments, [
    "/api/me",
    { signal: controller.signal },
  ]);
});

test("a failed current-user request does not imply admin access", async () => {
  mock.method(
    globalThis,
    "fetch",
    async () => new Response(null, { status: 403 }),
  );
  await assert.rejects(fetchCurrentUser(), { status: 403 });
});
