import assert from "node:assert/strict";
import { after, before, test } from "node:test";
import { createElement } from "react";
import { renderToString } from "react-dom/server";
import { CssVarsProvider } from "@mui/joy";
import { createMemoryRouter, RouterProvider } from "react-router";
import { createServer } from "vite";
import filterLore from "../src/features/lore/filter-lore.ts";

const lore = [
  { name: "Guild History" },
  { name: "Grim" },
  { name: "Leaf ?#%" },
];

test("lore search matches names case-insensitively and ignores outer whitespace", () => {
  assert.deepEqual(filterLore(lore, "  gRI  "), [lore[1]]);
  assert.deepEqual(filterLore(lore, "i"), [lore[0], lore[1]]);
  assert.deepEqual(filterLore(lore, "?#%"), [lore[2]]);
  assert.equal(filterLore(lore, "   "), lore);
});

test("lore search returns no matches without modifying entries or their order", () => {
  const original = structuredClone(lore);
  assert.deepEqual(filterLore(lore, "unknown"), []);
  assert.deepEqual(filterLore([], "Grim"), []);
  assert.deepEqual(lore, original);
});

let server;
before(async () => {
  server = await createServer({ server: { middlewareMode: true, ws: false } });
});
after(async () => server?.close());

const renderLorePage = async (entries) => {
  const { default: LorePage } = await server.ssrLoadModule(
    "/src/features/lore/Lore.page.tsx",
  );
  const router = createMemoryRouter(
    [{ id: "lore", path: "/lore", element: createElement(LorePage) }],
    {
      initialEntries: ["/lore"],
      hydrationData: { loaderData: { lore: entries } },
    },
  );
  try {
    return renderToString(
      createElement(
        CssVarsProvider,
        null,
        createElement(RouterProvider, { router }),
      ),
    );
  } finally {
    router.dispose();
  }
};

test("the lore catalog exposes labelled search and encoded edit links", async () => {
  const html = await renderLorePage(lore);
  assert.match(html, /role="search"[^>]*aria-label="Lore search"/);
  assert.match(html, /<input[^>]*aria-label="Search lore by name"/);
  assert.match(html, /aria-label="Clear lore search"/);
  assert.match(html, /3 entries/);
  assert.match(html, /href="\/lore\/Leaf%20%3F%23%25"/);
});

test("an empty catalog retains both creation actions", async () => {
  const html = await renderLorePage([]);
  assert.match(html, /No lore yet\./);
  assert.match(html, /Create Guild Lore/);
  assert.match(html, /Create Member Lore/);
  assert.doesNotMatch(html, /No lore matches/);
});
