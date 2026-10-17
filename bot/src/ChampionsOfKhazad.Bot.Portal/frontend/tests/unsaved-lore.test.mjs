import assert from "node:assert/strict";
import { test } from "node:test";
import { createMemoryRouter } from "react-router";
import {
  getLoreFormSnapshot,
  shouldBlockLoreNavigation,
} from "../src/features/lore/unsaved-lore.ts";

const form = (...entries) => {
  const data = new FormData();
  for (const [key, value] of entries) data.append(key, value);
  return data;
};

test("lore snapshots detect edits and exact restoration", () => {
  const data = form(["content", "Original lore"]);
  const original = getLoreFormSnapshot(data);
  data.set("content", "Edited lore");
  assert.notEqual(getLoreFormSnapshot(data), original);
  data.set("content", "Original lore");
  assert.equal(getLoreFormSnapshot(data), original);
});

test("snapshots retain repeated alias/role values, order, and empty new rows", () => {
  const data = form(
    ["aliases", "Grim"],
    ["aliases", "Grimbles"],
    ["roles", "Raider"],
  );
  const original = getLoreFormSnapshot(data);
  data.append("aliases", "");
  assert.notEqual(getLoreFormSnapshot(data), original);
  data.delete("aliases");
  data.append("aliases", "Grimbles");
  data.append("aliases", "Grim");
  assert.notEqual(getLoreFormSnapshot(data), original);
  assert.equal(
    getLoreFormSnapshot(
      form(["aliases", "Grim"], ["aliases", "Grimbles"], ["roles", "Raider"]),
    ),
    original,
  );
});

test("only dirty forms block leaving the current path or creation type", () => {
  const current = { pathname: "/lore/Guild", search: "" };
  const images = { pathname: "/images", search: "" };
  assert.equal(shouldBlockLoreNavigation(false, current, images), false);
  assert.equal(shouldBlockLoreNavigation(true, current, images), true);
  assert.equal(shouldBlockLoreNavigation(true, current, current), false);
  assert.equal(
    shouldBlockLoreNavigation(
      true,
      { pathname: "/lore/new", search: "?type=guild" },
      { pathname: "/lore/new", search: "?type=member" },
    ),
    true,
  );
});

test("blocked navigation can keep the draft or proceed to the requested page", async () => {
  const router = createMemoryRouter([{ path: "*" }], {
    initialEntries: ["/lore/Guild"],
  });
  router.getBlocker("lore", ({ currentLocation, nextLocation }) =>
    shouldBlockLoreNavigation(true, currentLocation, nextLocation),
  );
  try {
    await router.navigate("/images");
    assert.equal(router.state.location.pathname, "/lore/Guild");
    const blocked = router.state.blockers.get("lore");
    assert.equal(blocked.state, "blocked");
    blocked.reset();
    assert.equal(router.state.location.pathname, "/lore/Guild");
    assert.equal(router.state.blockers.get("lore").state, "unblocked");

    await router.navigate("/images");
    router.state.blockers.get("lore").proceed();
    assert.equal(router.state.location.pathname, "/images");
  } finally {
    router.dispose();
  }
});
