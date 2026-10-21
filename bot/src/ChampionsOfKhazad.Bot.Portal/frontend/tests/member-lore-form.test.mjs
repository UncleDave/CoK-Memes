import assert from "node:assert/strict";
import { after, before, test } from "node:test";
import { createElement } from "react";
import { renderToString } from "react-dom/server";
import { CssVarsProvider } from "@mui/joy";
import { createMemoryRouter, RouterProvider } from "react-router";
import { createServer } from "vite";

let server;
before(async () => {
  server = await createServer({ server: { middlewareMode: true, ws: false } });
});
after(async () => server?.close());

const renderMemberForm = async (discordUserId, isCreating = false) => {
  const { default: MemberLoreForm } = await server.ssrLoadModule(
    "/src/features/lore/MemberLoreForm.tsx",
  );
  const router = createMemoryRouter(
    [
      {
        path: "*",
        element: createElement(MemberLoreForm, {
          lore: {
            name: "Member",
            pronouns: "they",
            nationality: "UK",
            mainCharacter: "Character",
            biography: "Biography",
            aliases: [],
            roles: [],
            discordUserId,
          },
          isCreating,
        }),
      },
    ],
    { initialEntries: ["/lore/Member"] },
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

const discordInput = (html) =>
  html.match(/<input\b[^>]*name="discordUserId"[^>]*>/)?.[0];

test("member edits show the exact Discord user ID in an optional text input", async () => {
  const html = await renderMemberForm("9223372036854775807");
  const input = discordInput(html);
  assert.ok(input);
  assert.doesNotMatch(input, /type="(?:number|range)"/);
  assert.match(input, /value="9223372036854775807"/);
  assert.match(input, /pattern="0\*\[1-9\]\[0-9\]\*"/);
  assert.match(input, /maxLength="20"/);
  assert.match(input, /inputMode="numeric"/);
  assert.doesNotMatch(input, /required/);
  assert.match(html, /Discord user ID/);
  assert.match(html, /leave blank to unlink/);
});

test("older unlinked members render a blank Discord user ID", async () => {
  for (const discordUserId of [undefined, null]) {
    const input = discordInput(await renderMemberForm(discordUserId));
    assert.ok(input);
    assert.match(input, /value=""/);
  }
});

test("creating a member from existing lore never copies its Discord user ID", async () => {
  const input = discordInput(
    await renderMemberForm("9223372036854775807", true),
  );
  assert.ok(input);
  assert.match(input, /value=""/);
  assert.doesNotMatch(input, /9223372036854775807/);
});
