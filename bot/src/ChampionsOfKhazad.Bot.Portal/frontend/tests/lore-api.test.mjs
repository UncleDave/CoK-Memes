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

for (const discordUserId of ["9007199254740993", "9223372036854775807"]) {
  test(`member actions round-trip Discord user ID ${discordUserId} exactly`, async () => {
    const fetchMock = mock.method(globalThis, "fetch", async (_, options) =>
      options?.method
        ? new Response(null, { status: 204 })
        : Response.json({ name: "Member", discordUserId }),
    );
    const member = await (await requestApi("/api/lore/Member")).json();
    const form = new FormData();
    form.set("name", member.name);
    form.set("discordUserId", member.discordUserId);

    for (const mutation of [
      api.createMemberLore(form),
      api.updateMemberLore(member.name, form),
    ]) {
      const result = await submitLore(mutation);
      assert.equal(result.status, 302);
    }

    const bodies = fetchMock.mock.calls
      .slice(1)
      .map((call) => JSON.parse(call.arguments[1].body));
    assert.deepEqual(
      bodies.map((body) => body.discordUserId),
      [discordUserId, discordUserId],
    );
  });
}

test("clearing a member Discord user ID sends an explicit empty string", async () => {
  const fetchMock = mock.method(
    globalThis,
    "fetch",
    async () => new Response(null, { status: 204 }),
  );
  const form = new FormData();
  form.set("discordUserId", "");

  const result = await submitLore(api.updateMemberLore("Member", form));

  assert.equal(result.status, 302);
  assert.equal(
    JSON.parse(fetchMock.mock.calls[0].arguments[1].body).discordUserId,
    "",
  );
});

test("legacy member actions omit a missing Discord user ID instead of clearing it", async () => {
  const fetchMock = mock.method(
    globalThis,
    "fetch",
    async () => new Response(null, { status: 204 }),
  );

  await api.createMemberLore(new FormData());
  await api.updateMemberLore("Member", new FormData());

  for (const call of fetchMock.mock.calls) {
    assert.equal(
      Object.hasOwn(JSON.parse(call.arguments[1].body), "discordUserId"),
      false,
    );
  }
});

test("duplicate Discord links retain the draft and show the member conflict", async () => {
  const message =
    "Discord user ID 9223372036854775807 is already linked to Other Member. Clear that member's Discord user ID before linking it here.";
  mock.method(globalThis, "fetch", async () =>
    Response.json({ message }, { status: 409 }),
  );
  const form = new FormData();
  form.set("discordUserId", "9223372036854775807");

  const result = await submitLore(api.updateMemberLore("Member", form));

  assert.deepEqual(result.data, { error: message });
  assert.equal(result.init.status, 409);
  assert.equal(form.get("discordUserId"), "9223372036854775807");
});

test("invalid Discord links show the actionable validation error without redirecting", async () => {
  const message =
    "Discord user ID must contain only decimal digits, be greater than 0, and be at most 9223372036854775807. Leave it blank to unlink this member.";
  mock.method(globalThis, "fetch", async () =>
    Response.json({ message }, { status: 400 }),
  );
  const form = new FormData();
  form.set("discordUserId", "9223372036854775808");

  const result = await submitLore(api.createMemberLore(form));

  assert.deepEqual(result.data, { error: message });
  assert.equal(result.init.status, 400);
});

test("concurrent member changes show the reload action instead of a success redirect", async () => {
  const message =
    "This lore changed before your update could be saved. Reload the entry and try again.";
  mock.method(globalThis, "fetch", async () =>
    Response.json({ message }, { status: 409 }),
  );
  const form = new FormData();
  form.set("biography", "Unsaved biography");

  const result = await submitLore(api.updateMemberLore("Member", form));

  assert.deepEqual(result.data, { error: message });
  assert.equal(result.init.status, 409);
  assert.equal(form.get("biography"), "Unsaved biography");
});
