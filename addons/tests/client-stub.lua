local clock = 100
api = {
    WOW_PROJECT_ID = 99,
    GetTime = function() return clock end,
    GetServerTime = function() return 1792600000 + clock end,
    GetBuildInfo = function() return "1.60.1", "70009", "Oct 20 2026", 16001 end,
    GetLocale = function() return "enGB" end,
    GetCurrentRegion = function() return 3 end,
    GetRealmName = function() return "Test Realm" end,
    UnitFullName = function() return "Élodie", "Test Realm" end,
    IsInGuild = function() return true end,
    GetGuildInfo = function() return "Test Guild", "PRIVATE RANK" end,
    GetGuildRosterShowOffline = function() return true end,
    GetNumGuildMembers = function() return 2 end,
    GetGuildRosterInfo = function(index)
        return index == 1 and "Élodie-TestRealm" or "Borin-TestRealm",
            "PRIVATE RANK", 1, index == 1 and 20 or 18, "Warrior", "PRIVATE ZONE",
            "PRIVATE PUBLIC NOTE", "PRIVATE OFFICER NOTE", index == 1, 0, "WARRIOR",
            999, 1, false, false, 8, "PRIVATE GUID"
    end,
    GetNumGuildTradeSkill = function() return 2 end,
    GetGuildTradeSkillInfo = function(index)
        if index == 1 then
            return 171, false, 123, "Alchemy", 1, 1, 2
        end
        return 171, false, 123, nil, nil, nil, nil, "Borin", "Borin-TestRealm",
            "Warrior", false, "PRIVATE PROFESSION ZONE", 125, "WARRIOR"
    end,
    GetProfessions = function() return 1, nil, nil, 4, 5 end,
    GetProfessionInfo = function(index)
        local names = { [1] = "Alchemy", [4] = "Fishing", [5] = "Cooking" }
        local ids = { [1] = 171, [4] = 356, [5] = 185 }
        return names[index], 123, index == 4 and 0 or 125, 150, 1, 0, ids[index]
    end,
    issecretvalue = function() return false end,
}
refreshCalls = 0
api.C_GuildInfo = { GuildRoster = function() refreshCalls = refreshCalls + 1 end }
function advanceClock(seconds) clock = clock + seconds end

-- Collection must not alter filters, query recipes, write notes, or send messages.
local function forbidden() error("Unexpected write or communication") end
api.SetGuildRosterShowOffline = forbidden
api.SendChatMessage = forbidden
api.SendAddonMessage = forbidden
api.GuildRosterSetPublicNote = forbidden
api.GuildRosterSetOfficerNote = forbidden
