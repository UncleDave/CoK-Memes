local _, Probe = ...
local session = Probe.New(_G)
local window, edit, status

local function capture()
    local ok, report, text = pcall(session.Capture, session)
    if not ok then
        edit:SetText("")
        status:SetText("Capture failed. No report generated; check the client API behaviour.")
        return
    end
    edit:SetText(text)
    edit:SetCursorPosition(0)
    if edit:GetText() ~= text then
        edit:SetText("")
        status:SetText("Report exceeds the client text box limit; no partial export shown.")
        return
    end
    status:SetText(string.format(
        "%d roster rows; %d guild profession rows; %d issues. Coverage unverified.",
        #report.roster.rows, #report.guildProfessions.rows, report.issueCount
    ))
end

local function refresh()
    local _, message = session:RequestRefresh()
    edit:SetText("")
    status:SetText(message)
end

local function button(parent, label, x, action)
    local control = CreateFrame("Button", nil, parent, "UIPanelButtonTemplate")
    control:SetSize(150, 26)
    control:SetPoint("BOTTOMLEFT", x, 15)
    control:SetText(label)
    control:SetScript("OnClick", action)
end

local function show()
    if not window then
        window = CreateFrame("Frame", "GrubRosterProbeFrame", UIParent, "BackdropTemplate")
        window:SetSize(780, 540)
        window:SetPoint("CENTER")
        window:SetFrameStrata("DIALOG")
        window:SetClampedToScreen(true)
        window:SetMovable(true)
        window:EnableMouse(true)
        window:RegisterForDrag("LeftButton")
        window:SetScript("OnDragStart", window.StartMoving)
        window:SetScript("OnDragStop", window.StopMovingOrSizing)
        window:SetBackdrop({
            bgFile = "Interface\\DialogFrame\\UI-DialogBox-Background",
            edgeFile = "Interface\\DialogFrame\\UI-DialogBox-Border",
            tile = true, tileSize = 32, edgeSize = 32,
            insets = { left = 8, right = 8, top = 8, bottom = 8 },
        })
        table.insert(UISpecialFrames, "GrubRosterProbeFrame")

        local title = window:CreateFontString(nil, "OVERLAY", "GameFontNormalLarge")
        title:SetPoint("TOPLEFT", 20, -18)
        title:SetText("Grub Roster Probe")
        local close = CreateFrame("Button", nil, window, "UIPanelCloseButton")
        close:SetPoint("TOPRIGHT", -5, -5)

        local instructions = window:CreateFontString(nil, "OVERLAY", "GameFontHighlightSmall")
        instructions:SetPoint("TOPLEFT", 20, -48)
        instructions:SetText("Capture reads the current cache. Select all, then Ctrl+C to copy the JSON report.")

        local scroll = CreateFrame("ScrollFrame", nil, window, "UIPanelScrollFrameTemplate")
        scroll:SetPoint("TOPLEFT", 20, -75)
        scroll:SetPoint("BOTTOMRIGHT", -42, 90)
        edit = CreateFrame("EditBox", nil, scroll)
        edit:SetMultiLine(true)
        edit:SetAutoFocus(false)
        edit:SetFontObject(ChatFontNormal)
        edit:SetWidth(710)
        edit:SetHeight(350)
        edit:SetMaxLetters(0)
        edit:SetScript("OnEscapePressed", function(self) self:ClearFocus() end)
        scroll:SetScrollChild(edit)

        status = window:CreateFontString(nil, "OVERLAY", "GameFontHighlightSmall")
        status:SetPoint("BOTTOMLEFT", 20, 55)
        status:SetWidth(730)
        status:SetJustifyH("LEFT")
        button(window, "Request refresh", 20, refresh)
        button(window, "Capture", 180, capture)
        button(window, "Select all", 340, function()
            edit:SetFocus()
            edit:HighlightText()
        end)
    end
    window:Show()
end

local events = CreateFrame("Frame")
for _, event in ipairs(Probe.Events) do
    -- A beta client may not recognise every event.
    session.registrations[event] = pcall(events.RegisterEvent, events, event)
end
events:SetScript("OnEvent", function(_, event) session:OnEvent(event) end)

SLASH_GRUBROSTERPROBE1 = "/grubprobe"
SlashCmdList.GRUBROSTERPROBE = function(argument)
    local command = (argument or ""):match("^%s*(.-)%s*$"):lower()
    if command ~= "" and command ~= "capture" and command ~= "refresh" then
        print("Grub Roster Probe: /grubprobe [capture|refresh]")
        return
    end
    show()
    if command == "refresh" then refresh() else capture() end
end
