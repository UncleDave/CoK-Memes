local _, Probe = ...

Probe.Version = "0.1.0"
Probe.MaxRows = 5000
Probe.MaxIssues = 50
Probe.Events = {
    "GUILD_ROSTER_UPDATE",
    "GUILD_TRADESKILL_UPDATE",
    "PLAYER_GUILD_UPDATE",
    "SKILL_LINES_CHANGED",
    "TRADE_SKILL_LIST_UPDATE",
    "TRADE_SKILL_SHOW",
}

local capabilities = {
    "C_GuildInfo.GuildRoster", "GuildRoster", "IsInGuild", "GetGuildInfo",
    "GetNumGuildMembers", "GetGuildRosterInfo", "GetGuildRosterShowOffline",
    "GetNumGuildTradeSkill", "GetGuildTradeSkillInfo", "GetProfessions",
    "GetProfessionInfo", "GetBuildInfo", "GetLocale", "GetCurrentRegion",
    "GetRealmName", "UnitFullName", "GetServerTime", "GetTime", "issecretvalue",
}

local arrayTag = {}
local function array()
    return setmetatable({}, arrayTag)
end

local function quote(value)
    return '"' .. value:gsub('[%z\1-\31\\"]', function(character)
        if character == '"' then return '\\"' end
        if character == '\\' then return '\\\\' end
        return string.format("\\u%04x", string.byte(character))
    end) .. '"'
end

local function json(value)
    local kind = type(value)
    if kind == "string" then return quote(value) end
    if kind == "boolean" then return value and "true" or "false" end
    if kind == "number" then return string.format("%.17g", value) end
    assert(kind == "table", "Unsupported diagnostic value")
    local result = {}
    if getmetatable(value) == arrayTag then
        for index, item in ipairs(value) do result[index] = json(item) end
        return "[" .. table.concat(result, ",") .. "]"
    end
    local keys = {}
    for key in pairs(value) do keys[#keys + 1] = key end
    table.sort(keys)
    for _, key in ipairs(keys) do result[#result + 1] = quote(key) .. ":" .. json(value[key]) end
    return "{" .. table.concat(result, ",") .. "}"
end

local Session = {}
Session.__index = Session

function Probe.New(api)
    return setmetatable({ api = api, events = {}, registrations = {}, requests = array() }, Session)
end

function Session:Resolve(path)
    local value = self.api
    for segment in path:gmatch("[^.]+") do
        if type(value) ~= "table" then return nil end
        value = value[segment]
    end
    if type(value) == "function" then return value end
end

function Session:Issue(api, reason)
    if not self.issues then return end
    self.issueCount = self.issueCount + 1
    if #self.issues < Probe.MaxIssues then
        self.issues[#self.issues + 1] = { api = api, reason = reason }
    end
end

local function pack(...)
    return { n = select("#", ...), ... }
end

function Session:Call(path, ...)
    local method = self:Resolve(path)
    if not method then
        self:Issue(path, "api_missing")
        return nil
    end
    local result = pack(pcall(method, ...))
    if not result[1] then
        -- Error text can contain arbitrary game data; never export it.
        self:Issue(path, "api_error")
        return nil
    end
    return result
end

function Session:Scalar(value, kind, field)
    local isSecret = self:Resolve("issecretvalue")
    if isSecret then
        local ok, secret = pcall(isSecret, value)
        if not ok or secret then
            self:Issue(field, "restricted_value")
            return nil
        end
    end
    if value == nil then return nil end
    if type(value) ~= kind then
        self:Issue(field, "unexpected_type")
        return nil
    end
    if kind == "number" and (value ~= value or value == math.huge or value == -math.huge) then
        self:Issue(field, "non_finite_number")
        return nil
    end
    if kind == "string" and #value > 256 then
        self:Issue(field, "string_too_long")
        return nil
    end
    return value
end

function Session:Read(path, kind, ...)
    local result = self:Call(path, ...)
    return result and self:Scalar(result[2], kind, path)
end

function Session:OnEvent(event)
    local previous = self.events[event]
    self.events[event] = {
        count = (previous and previous.count or 0) + 1,
        lastSeenAt = self:Read("GetServerTime", "number"),
    }
end

function Session:RequestRefresh()
    local now = self:Read("GetTime", "number")
    if not now then return false, "Client clock unavailable; no request sent." end
    if self.lastRequestAt and now - self.lastRequestAt < 10 then
        return false, "Wait 10 seconds between refresh requests."
    end
    if self:Read("IsInGuild", "boolean") ~= true then
        return false, "Guild membership unavailable; no request sent."
    end
    self.lastRequestAt = now
    local path = self:Resolve("C_GuildInfo.GuildRoster") and "C_GuildInfo.GuildRoster" or "GuildRoster"
    local request = {
        api = path,
        requestedAt = self:Read("GetServerTime", "number"),
        rosterEventsBefore = self.events.GUILD_ROSTER_UPDATE and self.events.GUILD_ROSTER_UPDATE.count or 0,
        tradeskillEventsBefore = self.events.GUILD_TRADESKILL_UPDATE and self.events.GUILD_TRADESKILL_UPDATE.count or 0,
    }
    request.status = self:Call(path) and "requested" or "failed"
    if #self.requests == 10 then table.remove(self.requests, 1) end
    self.requests[#self.requests + 1] = request
    if request.status == "failed" then return false, "Roster refresh API missing or failed; Capture shows capabilities." end
    return true, "Refresh requested. Wait a few seconds, then Capture."
end

function Session:Count(path)
    local count = self:Read(path, "number")
    if not count then return nil end
    if count < 0 or count % 1 ~= 0 then
        self:Issue(path, "invalid_count")
        return nil
    end
    if count > Probe.MaxRows then self:Issue(path, "row_limit") end
    return count
end

function Session:CollectRoster(inGuild)
    local section = { status = "unavailable", coverage = "unverified", rows = array() }
    if inGuild == false then section.status = "not_in_guild"; return section end
    section.showOffline = self:Read("GetGuildRosterShowOffline", "boolean")
    section.reportedCount = self:Count("GetNumGuildMembers")
    if not section.reportedCount or not self:Resolve("GetGuildRosterInfo") then return section end
    section.status = "read"
    for index = 1, math.min(section.reportedCount, Probe.MaxRows) do
        local values = self:Call("GetGuildRosterInfo", index)
        if values then
            -- Deliberately ignore rank, location, notes, achievements, and GUIDs.
            local row = {
                index = index,
                name = self:Scalar(values[2], "string", "roster.name"),
                level = self:Scalar(values[5], "number", "roster.level"),
                className = self:Scalar(values[6], "string", "roster.className"),
                online = self:Scalar(values[10], "boolean", "roster.online"),
                classToken = self:Scalar(values[12], "string", "roster.classToken"),
            }
            if not row.name or not row.level then
                self:Issue("GetGuildRosterInfo", "incomplete_row")
                section.status = "partial"
            end
            section.rows[#section.rows + 1] = row
        else
            section.status = "partial"
        end
    end
    if section.reportedCount > Probe.MaxRows then section.status = "partial" end
    section.readCount = #section.rows
    return section
end

function Session:CollectGuildProfessions(inGuild)
    local section = { status = "unavailable", coverage = "unverified", rows = array() }
    if inGuild == false then section.status = "not_in_guild"; return section end
    section.reportedCount = self:Count("GetNumGuildTradeSkill")
    if not section.reportedCount or not self:Resolve("GetGuildTradeSkillInfo") then return section end
    section.status = "read"
    for index = 1, math.min(section.reportedCount, Probe.MaxRows) do
        local values = self:Call("GetGuildTradeSkillInfo", index)
        if values then
            local row = {
                index = index,
                skillLineId = self:Scalar(values[2], "number", "guildProfession.skillLineId"),
                collapsed = self:Scalar(values[3], "boolean", "guildProfession.collapsed"),
                headerName = self:Scalar(values[5], "string", "guildProfession.headerName"),
                onlineCount = self:Scalar(values[6], "number", "guildProfession.onlineCount"),
                visibleCount = self:Scalar(values[7], "number", "guildProfession.visibleCount"),
                memberCount = self:Scalar(values[8], "number", "guildProfession.memberCount"),
                name = self:Scalar(values[9], "string", "guildProfession.name"),
                nameWithRealm = self:Scalar(values[10], "string", "guildProfession.nameWithRealm"),
                online = self:Scalar(values[12], "boolean", "guildProfession.online"),
                skill = self:Scalar(values[14], "number", "guildProfession.skill"),
                classToken = self:Scalar(values[15], "string", "guildProfession.classToken"),
            }
            -- Preserve header rows and indexes; never invent a member's profession mapping.
            section.rows[#section.rows + 1] = row
            if not row.name and not row.nameWithRealm and not row.headerName then
                self:Issue("GetGuildTradeSkillInfo", "incomplete_row")
                section.status = "partial"
            end
        else
            section.status = "partial"
        end
    end
    if section.reportedCount > Probe.MaxRows then section.status = "partial" end
    section.readCount = #section.rows
    return section
end

function Session:CollectPlayerProfessions()
    local section = { status = "unavailable", coverage = "unverified", rows = array() }
    local slots = self:Call("GetProfessions")
    if not slots or not self:Resolve("GetProfessionInfo") then return section end
    section.status = "read"
    section.returnedSlotCount = slots.n - 1
    -- The first five labels follow Forever's profession-book UI; additional slots
    -- are deliberately unnamed rather than inheriting another client's meaning.
    local labels = { "primary1", "primary2", "firstAid", "fishing", "cooking" }
    -- GetProfessions has nil holes: do not iterate it with ipairs or #.
    for slot = 1, math.min(math.max(#labels, section.returnedSlotCount), 10) do
        local professionIndex = self:Scalar(slots[slot + 1], "number", "playerProfession.index")
        if professionIndex then
            local values = self:Call("GetProfessionInfo", professionIndex)
            if values then
                local row = {
                    slot = labels[slot] or ("slot" .. slot),
                    slotIndex = slot,
                    name = self:Scalar(values[2], "string", "playerProfession.name"),
                    skill = self:Scalar(values[4], "number", "playerProfession.skill"),
                    maxSkill = self:Scalar(values[5], "number", "playerProfession.maxSkill"),
                    skillLineId = self:Scalar(values[8], "number", "playerProfession.skillLineId"),
                }
                section.rows[#section.rows + 1] = row
                if not row.name then section.status = "partial" end
            else
                section.status = "partial"
            end
        end
    end
    if section.returnedSlotCount > 10 then
        self:Issue("GetProfessions", "slot_limit")
        section.status = "partial"
    end
    return section
end

function Session:Capture()
    self.issues, self.issueCount = array(), 0
    local report = {
        format = "grub-roster-probe", schemaVersion = 1, addonVersion = Probe.Version,
        capturedAt = self:Read("GetServerTime", "number"),
        cacheFreshness = "unknown", coverage = "unverified",
        capabilities = {}, events = {}, eventRegistrations = {}, requests = array(),
        client = {}, player = {}, guild = {},
    }
    for _, path in ipairs(capabilities) do report.capabilities[path] = self:Resolve(path) ~= nil end
    for event, value in pairs(self.events) do
        report.events[event] = { count = value.count, lastSeenAt = value.lastSeenAt }
    end
    for event, registered in pairs(self.registrations) do report.eventRegistrations[event] = registered end
    for _, request in ipairs(self.requests) do
        report.requests[#report.requests + 1] = {
            api = request.api, status = request.status, requestedAt = request.requestedAt,
            rosterEventsBefore = request.rosterEventsBefore, tradeskillEventsBefore = request.tradeskillEventsBefore,
        }
    end
    local build = self:Call("GetBuildInfo")
    if build then
        report.client.version = self:Scalar(build[2], "string", "client.version")
        report.client.build = self:Scalar(build[3], "string", "client.build")
        report.client.interface = self:Scalar(build[5], "number", "client.interface")
    end
    report.client.projectId = self:Scalar(self.api.WOW_PROJECT_ID, "number", "client.projectId")
    report.client.locale = self:Read("GetLocale", "string")
    report.client.region = self:Read("GetCurrentRegion", "number")
    report.client.realm = self:Read("GetRealmName", "string")
    report.guild.inGuild = self:Read("IsInGuild", "boolean")
    report.guild.name = self:Read("GetGuildInfo", "string", "player")
    local player = self:Call("UnitFullName", "player")
    if player then
        report.player.name = self:Scalar(player[2], "string", "player.name")
        report.player.realm = self:Scalar(player[3], "string", "player.realm")
    end
    report.roster = self:CollectRoster(report.guild.inGuild)
    report.guildProfessions = self:CollectGuildProfessions(report.guild.inGuild)
    report.playerProfessions = self:CollectPlayerProfessions()
    report.issues, report.issueCount = self.issues, self.issueCount
    self.issues = nil
    return report, json(report)
end
