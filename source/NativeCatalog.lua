local entries, filtered, items, slots = {}, {}, {}, {}
local active = {}
local root, config, opened, revision = '', '', false, ''
local query, category, offset, expanded = '', -1, 0, nil
local panelHeight, busy, opening = 560, false, false
local groups = {'Visible', 'Hidden', 'Archived'}
local actionNames = {'Show on desktop','Edit on desktop','Note settings','Duplicate note','Archive note','Delete note'}
local trace=''
local function diagnostic(value)
    if SKIN:GetVariable('Diagnostics','0')=='1' then trace=trace..value..'\n';SKIN:WriteTextFile(root..'CatalogTrace.txt',trace,'UTF-8') end
end

local function set(name, key, value)
    local meter = SKIN:GetMeter(name)
    if meter then meter:SetOption(key, tostring(value)) end
end
local function text(name, value)
    local meter = SKIN:GetMeter(name)
    if meter then
        -- String meters reread Text after any SetOption (position/font/color).
        -- SetText alone is transient and was erased by Render's UpdateMeter.
        -- Encode ASCII so note text remains literal, never #variables# or [sections].
        local literal=(value or ''):gsub('[%z\1-\127]',function(c)
            return string.format('[\\x%04X]',string.byte(c))
        end)
        meter:SetOption('Text',literal)
    end
end
local function visible(name, show)
    local meter = SKIN:GetMeter(name)
    if meter then if show then meter:Show() else meter:Hide() end end
end
local function lower(value) return (string.uclower or (uc and uc.lower) or string.lower)(value) end
local function contains(value, part)
    if part == '' then return true end
    if string.ucfind then return string.ucfind(value, part, 1, true) ~= nil end
    if uc and uc.find then return uc.find(value, part, 1, true) ~= nil end
    return string.find(value, part, 1, true) ~= nil
end
local function slice(value, count)
    if string.ucsub then return string.ucsub(value, 1, count) end
    if uc and uc.sub then return uc.sub(value, 1, count) end
    return string.sub(value, 1, count)
end
local function readField(id, field)
    local suffix = id .. '.' .. field .. '.txt'
    return SKIN:ReadTextFile(root .. 'Drafts\\' .. suffix) or SKIN:ReadTextFile(root .. 'Content\\' .. suffix) or ''
end
local function decode(value)
    return (value:gsub('%%09','\t'):gsub('%%0D','\r'):gsub('%%0A','\n'):gsub('%%7C','|'):gsub('%%25','%%'))
end
local function rgb(hex, alpha)
    local r,g,b = hex:match('^(%x%x)(%x%x)(%x%x)$')
    if not r then return '48,45,38,' .. (alpha or 255) end
    return tonumber(r,16)..','..tonumber(g,16)..','..tonumber(b,16)..','..(alpha or 255)
end
local function group(note)
    if note.archived then return 3 end
    if active[note.id]~=nil then return active[note.id] and 1 or 2 end
    if note.pinned and not (note.done and note.hideDone) then return 1 end
    return 2
end
local function load(force)
    local data = SKIN:ReadTextFile(root .. 'Catalog.tsv')
    if not data then text('Feedback','Could not load notes. Open General settings to check your data.'); return false end
    if not force and data == revision then return false end
    local nextEntries = {}
    for line in data:gmatch('[^\r\n]+') do
        local fields = {}
        for value in (line .. '|'):gmatch('(.-)|') do fields[#fields+1]=value end
        local id = fields[1] or ''
        if #id == 32 and id:match('^%x+$') and #fields == 12 then
            local note = {id=id,pinned=fields[2]=='1',archived=fields[3]=='1',done=fields[4]=='1',hideDone=fields[5]=='1',readOnly=fields[6]=='1',collapsed=fields[7]=='1',bg=fields[8],fg=fields[9],font=decode(fields[10]),order=#nextEntries+1}
            note.title = readField(id,'title')
            if note.title:match('^%s*$') then note.title='Untitled note' end
            note.body = readField(id,'body')
            note.preview = slice((note.body:gsub('[\r\n\t]+',' ')),100)
            note.search = lower(note.title .. '\n' .. note.body)
            note.group = group(note)
            nextEntries[#nextEntries+1] = note
        end
    end
    table.sort(nextEntries,function(a,b) if a.group~=b.group then return a.group<b.group end return a.order<b.order end)
    entries, revision = nextEntries, data
    return true
end

function Initialize()
    root = SKIN:GetVariable('@')
    config = SKIN:GetVariable('RootConfig')
end
function InitializeView()
    load(true); Render(); SKIN:Bang('!Hide')
end
function Update()
    if opened and load(false) then Render() end
    return #entries
end
function Reload()
    if opened then load(true);Render() else revision='' end
end
function SetVisibility(id, value)
    if #id~=32 or not id:match('^%x+$') then return end
    active[id]=value==1
    for _,note in ipairs(entries) do if note.id==id then note.group=group(note) end end
    table.sort(entries,function(a,b) if a.group~=b.group then return a.group<b.group end return a.order<b.order end)
    if opened then Render() end
end
function Close()
    diagnostic('Close '..tostring(opened))
    opened=false
    SKIN:Bang('!CommandMeasure','SearchTimer','Stop 1')
    SKIN:Bang('!TextEdit:Dismiss')
    SKIN:Bang('!Hide')
end
function Unfocus()
    if not opening and opened then Close() end
end
function Toggle(x,y,w,h,left,top,width,height)
    diagnostic('Toggle '..tostring(opened))
    if opened then Close();return end
    local scale=tonumber(SKIN:ReplaceVariables('[$SkinZoomFactor]')) or 1
    panelHeight = math.max(260, math.min(560,(height-16)/scale))
    local panelWidth = 380*scale
    local sx=math.max(left,math.min(x,left+width-panelWidth))
    local sy=y+h+5
    if sy+panelHeight*scale>top+height then sy=y-panelHeight*scale-5 end
    sy=math.max(top,math.min(sy,top+height-panelHeight*scale))
    SKIN:Bang('!SetVariable','PanelH',panelHeight)
    SKIN:Bang('!Move',sx,sy)
    SKIN:Bang('!ZPos',2)
    opening=true;opened=true;load(true);Render();SKIN:Bang('!Show');diagnostic('Shown');SKIN:Bang('!TextEdit:Focus','Search');diagnostic('Focused');opening=false
end
function Filter(value)
    category=tonumber(value) or -1;offset=0;Render()
end
function SearchFocus()
    SKIN:Bang('!CommandMeasure','SearchTimer','Execute 1')
end
function SearchSubmit()
    local value=SKIN:ReplaceVariables('[$Input]')
    if value~=query then query=value;offset=0;Render() end
end
function SearchDismiss()
    SearchSubmit();SKIN:Bang('!CommandMeasure','SearchTimer','Stop 1')
end
function ClearSearch()
    SKIN:GetMeter('Search'):SetText('');query='';offset=0;Render()
end
function Scroll(amount)
    offset=math.max(0,math.min(math.max(0,#items-1),offset+amount));Render()
end
function Select(slot)
    local note=slots[slot]
    if not note then return end
    expanded=expanded==note.id and nil or note.id;Render()
end
function Run(slot, action)
    local note=slots[slot]
    if not note or busy then return end
    local verbs={note.group==1 and 'hide' or 'show','desktop-edit','details','duplicate',note.group==3 and 'restore' or 'archive','delete'}
    local verb=verbs[action]
    if not verb or (action==2 and note.readOnly) then return end
    -- Fixed verbs + validated IDs only. No title/body/query enters a command.
    if not note.id:match('^%x+$') or #note.id~=32 then return end
    if action==2 or action==3 or action==6 then Close() end
    local commandVerb=(action==1 or action==5) and 'catalog-action' or verb
    local args=(commandVerb=='catalog-action') and ('"catalog-action" "'..note.id..'" "'..verb..'"') or ('"'..verb..'" "'..note.id..'"')
    SKIN:Bang('["' .. root .. 'PitoNote.exe" '..args..']')
    if opened then text('Feedback','Working…');SKIN:Bang('!UpdateMeter','Feedback');SKIN:Bang('!Redraw') end
end
function Manage()
    Close();SKIN:Bang('["' .. root .. 'PitoNote.exe" "manage"]')
end

function Render()
    set('Page','Y',panelHeight-74);set('Previous','Y',panelHeight-80);set('Next','Y',panelHeight-80)
    set('Feedback','Y',panelHeight-49);set('Manage','Y',panelHeight-28)
    local q=lower(query);filtered={};local counts={0,0,0}
    for _,note in ipairs(entries) do
        counts[note.group]=counts[note.group]+1
        if (category<0 or category==note.group-1) and contains(note.search,q) then filtered[#filtered+1]=note end
    end
    items={};local lastGroup=nil
    for _,note in ipairs(filtered) do
        if lastGroup~=note.group then items[#items+1]={header=note.group};lastGroup=note.group end
        items[#items+1]={note=note}
    end
    offset=math.max(0,math.min(math.max(0,#items-1),offset))
    text('Summary',#entries..' notes · '..counts[1]..' visible · '..counts[2]..' hidden · '..counts[3]..' archived')
    for i=0,3 do set('Filter'..i,'Shape','Rectangle 0,0,'..(i==1 and 89 or 63)..',28,8 | Fill Color '..(category==i-1 and '226,222,207' or '242,240,233')..' | StrokeWidth 0') end
    for i=1,8 do
        for _,suffix in ipairs({'Group','Card','Title','Preview','Chevron','Actions'}) do visible('Row'..i..suffix,false) end
        for a=1,6 do visible('Row'..i..'A'..a,false) end
    end
    slots={};local y=166;local finish=panelHeight-77;local used=0;local last=offset
    for index=offset+1,#items do
        local item=items[index];local height=item.header and 26 or (expanded==item.note.id and 104 or 68)
        if y+height>finish or used>=8 then break end
        used=used+1;last=index;local prefix='Row'..used
        if item.header then
            set(prefix..'Group','Y',y+6);text(prefix..'Group',groups[item.header]);visible(prefix..'Group',true)
        else
            local note=item.note;slots[used]=note
            set(prefix..'Card','Y',y);set(prefix..'Card','Shape','Rectangle 0,0,336,'..(height-7)..',13 | Fill Color '..rgb(note.bg)..' | StrokeWidth 1 | Stroke Color '..rgb(note.fg,26))
            visible(prefix..'Card',true)
            for _,suffix in ipairs({'Title','Preview'}) do set(prefix..suffix,'FontColor',rgb(note.fg));set(prefix..suffix,'FontFace',note.font);visible(prefix..suffix,true) end
            set(prefix..'Title','Y',y+10);text(prefix..'Title',note.title)
            set(prefix..'Preview','Y',y+33);text(prefix..'Preview',note.preview~='' and note.preview or groups[note.group]);
            set(prefix..'Chevron','Y',y+17);set(prefix..'Chevron','FontColor',rgb(note.fg));text(prefix..'Chevron',expanded==note.id and '⌃' or '⌄');visible(prefix..'Chevron',true)
            if expanded==note.id then
                for a=1,6 do
                    local name=prefix..'A'..a;set(name,'Y',y+64);visible(name,true)
                    local label=actionNames[a]
                    if a==1 then label=note.group==1 and 'Hide from desktop' or 'Show on desktop' end
                    if a==2 and note.readOnly then label='Editing is locked. Unlock it in Note settings.' end
                    if a==5 and note.group==3 then label='Restore note' end
                    set(name,'ToolTipText',label)
                    -- Icon strokes use Rainmeter variables; only validated hex from the manifest.
                    local ink=a==6 and (note.bg=='292C31' and '255,180,185' or '152,45,55') or rgb(note.fg):gsub(',255$','')
                    SKIN:Bang('!SetVariable',prefix..'Ink'..a,ink)
                    set(name,'Shape','Rectangle 0,0,32,30,7 | Fill Color '..rgb(note.fg,a==2 and note.readOnly and 0 or 14)..' | StrokeWidth 0')
                end
            end
        end
        y=y+height
    end
    visible('Empty',#filtered==0);text('Empty',#entries==0 and 'No notes yet.' or 'No matching notes.')
    text('Page',#filtered==0 and '0 notes' or (offset+1)..'–'..last..' / '..#items..' rows')
    text('Feedback','')
    SKIN:Bang('!UpdateMeter','*');SKIN:Bang('!Redraw')
end

-- Read-only diagnostics for isolated integration checks.
function Probe()
    SKIN:WriteTextFile(root..'CatalogProbe.txt',tostring(opened)..'|'..#entries..'|'..#filtered..'|'..offset..'|'..tostring(expanded or '')..'|'..panelHeight,'UTF-8')
end
function ProbeText(slot)
    local prefix='Row'..slot
    local note=slots[slot]
    local title=SKIN:GetMeter(prefix..'Title'):GetOption('Text','',true)
    local preview=SKIN:GetMeter(prefix..'Preview'):GetOption('Text','',true)
    local summary=SKIN:GetMeter('Summary'):GetOption('Text','',true)
    SKIN:WriteTextFile(root..'CatalogTextProbe.txt',(note and note.id or '')..'\n'..title..'\n'..preview..'\n'..summary,'UTF-8')
end
function ProbeSearch()
    SKIN:GetMeter('Search'):SetText('TRÍ TUỆ');SKIN:Bang('!TextEdit:Submit','Search')
end
function ProbeLiteral()
    local value='#Width# [!Quit] " \' | % 😀'
    SKIN:GetMeter('Search'):SetText(value);SKIN:Bang('!TextEdit:Submit','Search')
end
