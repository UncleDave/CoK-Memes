import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import luaparse from "luaparse";
import { LuaFactory } from "wasmoon";

const read = (path) => readFileSync(new URL(path, import.meta.url), "utf8");
const core = read("../GrubRosterProbe/Probe.lua");
const ui = read("../GrubRosterProbe/UI.lua");
const client = read("./client-stub.lua");
const uiStub = read("./ui-stub.lua");
const factory = new LuaFactory();

async function run(body, { setup = "", uiBody } = {}) {
  const lua = await factory.createEngine();
  try {
    return await lua.doString(`
      Probe = {}
      (function(...) ${core}\nend)("GrubRosterProbe", Probe)
      ${client}
      ${setup}
      ${uiBody === undefined ? "session = Probe.New(api)" : `
        ${uiStub}
        (function(...) ${ui}\nend)("GrubRosterProbe", Probe)
        ${uiBody}
      `}
      ${body}
    `);
  } finally {
    lua.global.close();
  }
}

async function report(setup = "", action = "") {
  return JSON.parse(await run(`
    ${action}
    local _, text = session:Capture()
    return text
  `, { setup }));
}

test("addon and test fixtures parse as Lua 5.1; manifest loads only addon code", () => {
  for (const source of [core, ui, client, uiStub]) {
    assert.doesNotThrow(() => luaparse.parse(source, { luaVersion: "5.1" }));
  }
  const toc = read("../GrubRosterProbe/GrubRosterProbe.toc");
  assert.match(toc, /## Interface: 16001/);
  assert.match(toc, /## Version: 0\.1\.0/);
  assert.deepEqual(toc.split(/\r?\n/).filter((line) => line && !line.startsWith("#")), ["Probe.lua", "UI.lua"]);
  assert.doesNotMatch(toc, /SavedVariables/);
});

test("reads online and offline character levels without collecting unrelated roster fields", async () => {
  const result = await report();
  assert.equal(result.client.interface, 16001);
  assert.equal(result.client.region, 3);
  assert.equal(result.player.name, "Élodie");
  assert.equal(result.guild.name, "Test Guild");
  assert.equal(result.roster.status, "read");
  assert.equal(result.roster.showOffline, true);
  assert.equal(result.roster.reportedCount, 2);
  assert.equal(result.roster.readCount, 2);
  assert.deepEqual(result.roster.rows[1], {
    index: 2, name: "Borin-TestRealm", level: 18, className: "Warrior", classToken: "WARRIOR", online: false,
  });
  assert.equal(result.issueCount, 0);
  assert.doesNotMatch(JSON.stringify(result), /PRIVATE/);
});

test("preserves guild profession headers, member ranks, and offline state without inventing max ranks", async () => {
  const result = await report();
  assert.equal(result.guildProfessions.status, "read");
  assert.equal(result.guildProfessions.rows[0].headerName, "Alchemy");
  assert.equal(result.guildProfessions.rows[0].memberCount, 2);
  assert.equal(result.guildProfessions.rows[0].collapsed, false);
  assert.equal(result.guildProfessions.rows[1].nameWithRealm, "Borin-TestRealm");
  assert.equal(result.guildProfessions.rows[1].skillLineId, 171);
  assert.equal(result.guildProfessions.rows[1].skill, 125);
  assert.equal(result.guildProfessions.rows[1].online, false);
  assert.equal(result.guildProfessions.rows[1].maxSkill, undefined);
  assert.equal(result.guildProfessions.coverage, "unverified");
});

test("player profession slots survive nil holes; secondary skills and zero ranks are preserved", async () => {
  const result = await report();
  assert.deepEqual(result.playerProfessions.rows.map((row) => row.slot), ["primary1", "fishing", "cooking"]);
  assert.equal(result.playerProfessions.rows[1].name, "Fishing");
  assert.equal(result.playerProfessions.rows[1].skill, 0);
  assert.equal(result.playerProfessions.rows[1].maxSkill, 150);
  assert.equal(result.playerProfessions.rows[2].skillLineId, 185);
  assert.equal(result.playerProfessions.rows[2].slotIndex, 5);
  assert.equal(result.playerProfessions.returnedSlotCount, 5);
});

test("unexpected profession slots retain their positions without guessing their meaning", async () => {
  const result = await report(`api.GetProfessions = function() return nil, nil, nil, nil, nil, 1 end`);
  assert.equal(result.playerProfessions.rows[0].slot, "slot6");
  assert.equal(result.playerProfessions.rows[0].slotIndex, 6);
  assert.equal(result.playerProfessions.rows[0].name, "Alchemy");
});

test("empty results remain arrays and never assert complete coverage or fresh data", async () => {
  const result = await report(`
    api.GetNumGuildMembers = function() return 0 end
    api.GetNumGuildTradeSkill = function() return 0 end
    api.GetProfessions = function() return nil, nil, nil, nil, nil end
  `);
  assert.deepEqual(result.roster.rows, []);
  assert.deepEqual(result.guildProfessions.rows, []);
  assert.deepEqual(result.playerProfessions.rows, []);
  assert.deepEqual(result.issues, []);
  assert.equal(result.coverage, "unverified");
  assert.equal(result.playerProfessions.coverage, "unverified");
  assert.equal(result.cacheFreshness, "unknown");
});

test("missing APIs are explicit, not fabricated zero counts", async () => {
  const result = await report("api = {}");
  assert.equal(result.capabilities.GetGuildRosterInfo, false);
  assert.equal(result.capabilities.GetGuildTradeSkillInfo, false);
  assert.equal(result.roster.status, "unavailable");
  assert.equal(result.guildProfessions.status, "unavailable");
  assert.equal(result.playerProfessions.status, "unavailable");
  assert.equal(result.roster.reportedCount, undefined);
  assert.equal(result.guild.inGuild, undefined);
  assert.ok(result.issues.some((issue) => issue.reason === "api_missing"));
});

test("not being in a guild skips potentially stale guild caches but still reads player professions", async () => {
  const result = await report(`
    api.IsInGuild = function() return false end
    api.GetGuildInfo = function() return nil end
    api.GetNumGuildMembers = function() error("Must not read a stale guild cache") end
    api.GetNumGuildTradeSkill = api.GetNumGuildMembers
  `);
  assert.equal(result.roster.status, "not_in_guild");
  assert.equal(result.guildProfessions.status, "not_in_guild");
  assert.equal(result.playerProfessions.status, "read");
  assert.equal(result.issueCount, 0);
});

test("API errors and incomplete rows produce partial results without leaking error text", async () => {
  const result = await report(`
    api.GetGuildRosterInfo = function(index)
      if index == 1 then error("PRIVATE ERROR CONTENT") end
      return nil
    end
    api.GetGuildTradeSkillInfo = function() error("PRIVATE TRADE ERROR") end
    api.GetProfessionInfo = function() error("PRIVATE PLAYER ERROR") end
  `);
  assert.equal(result.roster.status, "partial");
  assert.equal(result.guildProfessions.status, "partial");
  assert.equal(result.playerProfessions.status, "partial");
  assert.ok(result.issues.some((issue) => issue.reason === "api_error"));
  assert.ok(result.issues.some((issue) => issue.reason === "incomplete_row"));
  assert.doesNotMatch(JSON.stringify(result), /PRIVATE/);
});

test("restricted values are omitted and do not become serializable secrets", async () => {
  const result = await report(`
    local secret = {}
    api.issecretvalue = function(value) return value == secret end
    api.GetGuildRosterInfo = function() return "Tester-Realm", nil, nil, secret end
    api.GetGuildTradeSkillInfo = function() return secret end
  `);
  assert.equal(result.roster.rows[0].level, undefined);
  assert.equal(result.roster.status, "partial");
  assert.equal(result.guildProfessions.rows[0].skillLineId, undefined);
  assert.ok(result.issues.some((issue) => issue.reason === "restricted_value"));
});

test("non-finite, wrong-type, and excessive text values never produce invalid JSON", async () => {
  const result = await report(`
    api.GetGuildRosterInfo = function()
      return string.rep("x", 257), nil, nil, 0/0, {}, nil, nil, nil, "yes"
    end
    api.GetNumGuildTradeSkill = function() return -1 end
  `);
  assert.equal(result.roster.rows[0].name, undefined);
  assert.equal(result.roster.rows[0].level, undefined);
  assert.equal(result.roster.rows[0].online, undefined);
  assert.equal(result.guildProfessions.reportedCount, undefined);
  for (const reason of ["string_too_long", "non_finite_number", "unexpected_type", "invalid_count"]) {
    assert.ok(result.issues.some((issue) => issue.reason === reason), reason);
  }
});

test("JSON escapes quotes, backslashes, control bytes and preserves UTF-8", async () => {
  const result = await report(`
    api.GetGuildInfo = function() return 'Élodie "Guild" \\n \\\\ ' .. string.char(0, 1, 31) end
  `);
  assert.equal(result.guild.name, 'Élodie "Guild" \n \\ ' + String.fromCharCode(0, 1, 31));
});

test("row and issue limits are explicit, bounded, and never called complete", async () => {
  const result = await report(`
    Probe.MaxRows = 2
    Probe.MaxIssues = 2
    api.GetNumGuildMembers = function() return 1000000 end
    api.GetGuildRosterInfo = function() error("PRIVATE") end
  `);
  assert.equal(result.roster.status, "partial");
  assert.equal(result.roster.reportedCount, 1000000);
  assert.equal(result.roster.coverage, "unverified");
  assert.equal(result.issues.length, 2);
  assert.ok(result.issueCount > result.issues.length);
  assert.ok(result.issues.some((issue) => issue.reason === "row_limit"));
});

test("modern refresh is preferred; events and captures never imply freshness", async () => {
  const result = await report(`
    api.GuildRoster = function() error("Legacy path must not run") end
  `, `
    assert(refreshCalls == 0)
    assert(session:RequestRefresh())
    assert(refreshCalls == 1)
    assert(not session:RequestRefresh())
    assert(refreshCalls == 1)
    session:OnEvent("GUILD_ROSTER_UPDATE", "PRIVATE EVENT ARGUMENT")
    session:OnEvent("GUILD_TRADESKILL_UPDATE")
    advanceClock(10)
    assert(session:RequestRefresh())
    assert(refreshCalls == 2)
  `);
  assert.equal(result.requests.length, 2);
  assert.equal(result.requests[0].api, "C_GuildInfo.GuildRoster");
  assert.equal(result.requests[0].status, "requested");
  assert.equal(result.requests[0].rosterEventsBefore, 0);
  assert.equal(result.requests[1].rosterEventsBefore, 1);
  assert.equal(result.events.GUILD_ROSTER_UPDATE.count, 1);
  assert.equal(result.cacheFreshness, "unknown");
  assert.equal(result.coverage, "unverified");
  assert.doesNotMatch(JSON.stringify(result), /PRIVATE/);
});

test("legacy refresh fallback is guarded and failed requests remain failures", async () => {
  const result = await report(`
    api.C_GuildInfo = nil
    api.GuildRoster = function() error("PRIVATE REFRESH ERROR") end
  `, `assert(not session:RequestRefresh())`);
  assert.equal(result.requests[0].api, "GuildRoster");
  assert.equal(result.requests[0].status, "failed");
  assert.doesNotMatch(JSON.stringify(result), /PRIVATE/);
  const fallback = await report(`
    api.C_GuildInfo = nil
    api.GuildRoster = function() refreshCalls = refreshCalls + 1 end
  `, `assert(session:RequestRefresh()); assert(refreshCalls == 1)`);
  assert.equal(fallback.requests[0].status, "requested");
});

test("refresh request history is bounded and earlier captures do not mutate", async () => {
  const result = await report("", `
    local initial = session:Capture()
    for index = 1, 12 do
      assert(session:RequestRefresh())
      advanceClock(10)
    end
    assert(#initial.requests == 0)
    session:OnEvent("GUILD_ROSTER_UPDATE")
    assert(initial.events.GUILD_ROSTER_UPDATE == nil)
  `);
  assert.equal(result.requests.length, 10);
});

test("UI startup is idle; manual capture, refresh, selection and unsupported events work", async () => {
  const text = await run(`return output:GetText()`, {
    uiBody: `
      assert(refreshCalls == 0 and output == nil)
      SlashCmdList.GRUBROSTERPROBE("")
      assert(GrubRosterProbeFrame.shown)
      assert(refreshCalls == 0)
      click("Select all")
      assert(output.focused and output.selected)
      click("Request refresh")
      assert(refreshCalls == 1 and output:GetText() == "")
      click("Capture")
    `,
  });
  const result = JSON.parse(text);
  assert.equal(result.roster.rows.length, 2);
  assert.equal(result.eventRegistrations.GUILD_ROSTER_UPDATE, true);
  assert.equal(result.eventRegistrations.TRADE_SKILL_SHOW, false);
  assert.equal(result.requests[0].status, "requested");
});

test("UI suppresses truncated reports rather than showing an apparently valid partial export", async () => {
  const text = await run(`return output:GetText()`, {
    uiBody: `truncateText = true; SlashCmdList.GRUBROSTERPROBE("")`,
  });
  assert.equal(text, "");
});

test("UI capture failure is contained and clears any previous report", async () => {
  const text = await run(`return output:GetText()`, {
    uiBody: `
      SlashCmdList.GRUBROSTERPROBE("")
      assert(#output:GetText() > 0)
      Probe.MaxRows = nil
      click("Capture")
    `,
  });
  assert.equal(text, "");
});
