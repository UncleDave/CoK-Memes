frames = {}
UISpecialFrames = {}
SlashCmdList = {}
UIParent = {}
ChatFontNormal = {}

local methods = {}
for _, name in ipairs({
    "SetSize", "SetPoint", "SetFrameStrata", "SetClampedToScreen", "SetMovable",
    "EnableMouse", "RegisterForDrag", "StartMoving", "StopMovingOrSizing", "SetBackdrop",
    "SetMultiLine", "SetAutoFocus", "SetFontObject", "SetWidth", "SetHeight",
    "SetMaxLetters", "SetCursorPosition", "SetJustifyH",
}) do methods[name] = function() end end

function methods:SetScript(name, callback) self.scripts[name] = callback end
function methods:SetText(text)
    self.text = text
    if self.kind == "EditBox" and truncateText then self.text = text:sub(1, 20) end
end
function methods:GetText() return self.text end
function methods:SetScrollChild(child) self.child = child end
function methods:Show() self.shown = true end
function methods:Hide() self.shown = false end
function methods:SetFocus() self.focused = true end
function methods:ClearFocus() self.focused = false end
function methods:HighlightText() self.selected = true end
function methods:RegisterEvent(event)
    if event == "TRADE_SKILL_SHOW" then error("Unknown event on this test client") end
    self.events[event] = true
end
function methods:CreateFontString() return CreateFrame("FontString") end

function CreateFrame(kind, name)
    local frame = setmetatable({ kind = kind, scripts = {}, events = {} }, { __index = methods })
    frames[#frames + 1] = frame
    if name then _G[name] = frame end
    if kind == "EditBox" then output = frame end
    return frame
end

function click(label)
    for _, frame in ipairs(frames) do
        if frame.kind == "Button" and frame.text == label then
            frame.scripts.OnClick(frame)
            return
        end
    end
    error("Button not found: " .. label)
end

for key, value in pairs(api) do _G[key] = value end
