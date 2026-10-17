import assert from "node:assert/strict";
import { after, before, test } from "node:test";
import { createElement } from "react";
import { renderToString } from "react-dom/server";
import { CssVarsProvider } from "@mui/joy";
import { createServer } from "vite";

let server;
before(async () => {
  server = await createServer({ server: { middlewareMode: true } });
});
after(async () => server?.close());

test("a selected filter icon renders inside the application's Joy theme", async () => {
  const { default: ChoiceChip } = await server.ssrLoadModule(
    "/src/features/core/ChoiceChip.tsx",
  );
  const html = renderToString(
    createElement(
      CssVarsProvider,
      null,
      createElement(ChoiceChip, {
        label: "Only mine",
        value: true,
        onChange: () => {},
      }),
    ),
  );
  assert.match(html, /Only mine/);
  assert.match(html, /checked/);
  assert.match(html, /<svg/);
});

test("multi-input add and remove icons render inside the Joy theme", async () => {
  const { default: FormMultiInput } = await server.ssrLoadModule(
    "/src/features/core/FormMultiInput.tsx",
  );
  const html = renderToString(
    createElement(
      CssVarsProvider,
      null,
      createElement(FormMultiInput, {
        label: "Aliases",
        name: "aliases",
        defaultValues: ["Grimbles"],
      }),
    ),
  );
  assert.match(html, /Grimbles/);
  assert.match(html, /Add aliases/);
  assert.match(html, /Remove aliases/);
  assert.equal((html.match(/<svg/g) ?? []).length, 2);
});
