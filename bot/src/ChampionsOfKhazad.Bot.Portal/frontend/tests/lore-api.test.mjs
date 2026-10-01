import assert from "node:assert/strict";
import { afterEach, mock, test } from "node:test";
import api from "../src/features/core/api.ts";
import requestApi from "../src/features/core/request-api.ts";
import submitLore from "../src/features/lore/submit-lore.ts";

afterEach(() => mock.restoreAll());

test("successful requests keep the response body available", async () => {
  const response = Response.json({ name: "Guild" });
  mock.method(globalThis, "fetch", async () => response);

  const result = await requestApi("/api/lore/Guild");

  assert.equal(result, response);
  assert.deepEqual(await result.json(), { name: "Guild" });
});

test("conflicting lore creation returns an inline error instead of a redirect", async () => {
  mock.method(globalThis, "fetch", async () =>
    Response.json(
      { message: "Lore with this name already exists." },
      { status: 409 },
    ),
  );
  const form = new FormData();
  form.set("name", "Guild");
  form.set("content", "Do not replace existing lore");

  const result = await submitLore(api.createGuildLore(form));

  assert.deepEqual(result.data, {
    error: "Lore with this name already exists.",
  });
  assert.equal(result.init.status, 409);
});

test("missing lore updates do not navigate away as if saved", async () => {
  mock.method(
    globalThis,
    "fetch",
    async () => new Response(null, { status: 404 }),
  );

  const result = await submitLore(
    api.updateGuildLore("Missing", new FormData()),
  );

  assert.deepEqual(result.data, {
    error: "Request failed (404). Please try again.",
  });
  assert.equal(result.init.status, 404);
});

test("non-JSON failures still produce a useful error", async () => {
  mock.method(
    globalThis,
    "fetch",
    async () => new Response("Service unavailable", { status: 503 }),
  );

  await assert.rejects(requestApi("/api/lore"), /Request failed \(503\)/);
});

test("network failures do not produce success redirects", async () => {
  mock.method(globalThis, "fetch", async () => {
    throw new Error("Network unavailable");
  });

  const result = await submitLore(api.deleteLore("Guild"));
  assert.deepEqual(result.data, {
    error: "Network unavailable",
  });
  assert.equal(result.init.status, 503);
});

test("successful lore mutations redirect to the list", async () => {
  mock.method(
    globalThis,
    "fetch",
    async () => new Response(null, { status: 204 }),
  );

  const result = await submitLore(
    api.updateMemberLore("Member", new FormData()),
  );

  assert.ok(result instanceof Response);
  assert.equal(result.status, 302);
  assert.equal(result.headers.get("Location"), "/lore");
});

test("lore names are encoded as a single URL component for all mutations", async () => {
  const fetchMock = mock.method(
    globalThis,
    "fetch",
    async () => new Response(null, { status: 204 }),
  );
  const name = "Name ?#%";
  const encoded = encodeURIComponent(name);

  await api.updateGuildLore(name, new FormData());
  await api.updateMemberLore(name, new FormData());
  await api.deleteLore(name);

  assert.deepEqual(
    fetchMock.mock.calls.map((call) => call.arguments[0]),
    [
      `/api/guild-lore/${encoded}`,
      `/api/member-lore/${encoded}`,
      `/api/lore/${encoded}`,
    ],
  );
});
