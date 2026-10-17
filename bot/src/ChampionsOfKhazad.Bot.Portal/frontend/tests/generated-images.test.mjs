import assert from "node:assert/strict";
import { afterEach, mock, test } from "node:test";
import {
  createImageState,
  imageReducer,
} from "../src/features/generated-images/generated-images-state.ts";
import fetchGeneratedImages from "../src/features/generated-images/fetch-generated-images.ts";

afterEach(() => mock.restoreAll());

const images = [{ filename: "one.png" }, { filename: "two.png" }];

test("the route loader's initial images do not request a duplicate page", () => {
  const state = createImageState(images);
  assert.equal(state.request.id, 0);
  assert.equal(state.images, images);
  assert.equal(state.isLoading, false);
});

test("filter requests stay loading until the matching response settles", () => {
  const state = imageReducer(createImageState(images), {
    type: "query",
    value: "dwarf",
  });
  assert.equal(state.isLoading, true);
  assert.equal(
    imageReducer(state, {
      type: "loaded",
      requestId: state.request.id,
      images: [],
    }).isLoading,
    false,
  );
  const failed = imageReducer(state, {
    type: "failed",
    requestId: state.request.id,
    error: "Unavailable",
  });
  assert.equal(failed.isLoading, false);
  assert.equal(imageReducer(failed, { type: "retry" }).isLoading, true);
});

test("repeating the current search preserves images and does not request another page", () => {
  let state = imageReducer(createImageState(images), {
    type: "query",
    value: "dwarf",
  });
  state = imageReducer(state, {
    type: "loaded",
    requestId: state.request.id,
    images,
  });
  assert.equal(imageReducer(state, { type: "query", value: "dwarf" }), state);
  assert.equal(
    imageReducer(createImageState(images), { type: "query", value: "" }).images,
    images,
  );
});

test("filter changes atomically clear the old grid and restart paging", () => {
  const initial = createImageState(images, { skip: 20, take: 16 });
  const state = imageReducer(initial, {
    type: "filter",
    filter: "mine",
    value: true,
  });
  assert.deepEqual(state.images, []);
  assert.equal(state.gridKey, initial.gridKey + 1);
  assert.deepEqual(state.request.parameters, { skip: 0, take: 16, mine: true });
  assert.equal(
    imageReducer(state, { type: "filter", filter: "mine", value: true }),
    state,
  );
});

test("repeating a page request does not duplicate the load", () => {
  const state = imageReducer(createImageState(images), {
    type: "page",
    skip: 2,
    take: 16,
  });
  assert.equal(imageReducer(state, { type: "page", skip: 2, take: 16 }), state);
});

test("stale successes and failures cannot change a newer filter's grid", () => {
  const initial = createImageState(images, { skip: 2 });
  const state = imageReducer(initial, { type: "query", value: "new query" });
  assert.equal(
    imageReducer(state, {
      type: "loaded",
      requestId: initial.request.id,
      images,
    }),
    state,
  );
  assert.equal(
    imageReducer(state, {
      type: "failed",
      requestId: initial.request.id,
      error: "Old failure",
    }),
    state,
  );
});

test("a failed load retains existing images and can retry the same page", () => {
  let state = createImageState(images, { skip: 2, take: 2 });
  state = imageReducer(state, {
    type: "failed",
    requestId: state.request.id,
    error: "Unavailable",
  });
  assert.equal(state.images, images);
  assert.equal(state.error, "Unavailable");
  assert.equal(imageReducer(state, { type: "page", skip: 4 }), state);
  const retried = imageReducer(state, { type: "retry" });
  assert.equal(retried.error, null);
  assert.equal(retried.request.id, state.request.id + 1);
  assert.equal(retried.request.parameters, state.request.parameters);
});

test("a short final page stops further loading and a new search re-enables it", () => {
  let state = createImageState(images, { skip: 2, take: 2 });
  state = imageReducer(state, {
    type: "loaded",
    requestId: state.request.id,
    images: [],
  });
  assert.equal(state.hasMore, false);
  assert.equal(imageReducer(state, { type: "page", skip: 4 }), state);
  assert.equal(
    imageReducer(state, { type: "query", value: "another" }).hasMore,
    true,
  );
});

test("image requests encode filters, pass cancellation and map response contracts", async () => {
  const controller = new AbortController();
  const fetchMock = mock.method(globalThis, "fetch", async () =>
    Response.json([
      {
        prompt: "Dwarf & newspaper",
        filename: "one.png",
        timestamp: "2026-10-17T12:00:00Z",
        user: { name: "Tester", avatarUrl: "avatar.png" },
      },
    ]),
  );
  const result = await fetchGeneratedImages(
    20,
    16,
    true,
    true,
    "dwarf & paper",
    controller.signal,
  );
  const [url, options] = fetchMock.mock.calls[0].arguments;
  assert.equal(
    new URL(url, "http://localhost").searchParams.get("query"),
    "dwarf & paper",
  );
  assert.equal(options.signal, controller.signal);
  assert.equal(result[0].filename, "one.png");
  assert.ok(result[0].timestamp instanceof Date);
});

test("failed and aborted image requests reject instead of returning a successful empty page", async () => {
  mock.method(
    globalThis,
    "fetch",
    async () => new Response(null, { status: 503 }),
  );
  await assert.rejects(fetchGeneratedImages(), /Failed to load images: 503/);
  mock.method(globalThis, "fetch", async () => {
    throw new DOMException("Aborted", "AbortError");
  });
  await assert.rejects(fetchGeneratedImages(), { name: "AbortError" });
});
