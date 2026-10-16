-- The toolbar only talks to a resident Rainmeter skin. No executable on open.
function ToggleCatalog()
    local target = SKIN:GetVariable('RootConfig') .. '\\Catalog'
    local x, y, w, h = SKIN:GetX(), SKIN:GetY(), SKIN:GetW(), SKIN:GetH()
    local function number(name, fallback)
        return tonumber(SKIN:ReplaceVariables(name)) or fallback
    end
    w=number('[$SkinZoomedW]',w);h=number('[$SkinZoomedH]',h)
    local left = number('[$DisplayWorkAreaX]', number('#WORKAREAX#', 0))
    local top = number('[$DisplayWorkAreaY]', number('#WORKAREAY#', 0))
    local width = number('[$DisplayWorkAreaW]', number('#WORKAREAWIDTH#', 1920))
    local height = number('[$DisplayWorkAreaH]', number('#WORKAREAHEIGHT#', 1080))
    local command = string.format('Toggle(%g,%g,%g,%g,%g,%g,%g,%g)', x,y,w,h,left,top,width,height)
    if SKIN:GetVariable('Diagnostics','0')=='1' then SKIN:WriteTextFile(SKIN:GetVariable('@')..'ControlProbe.txt',target..'|'..command,'UTF-8') end
    SKIN:Bang('!CommandMeasure', 'CatalogScript', command, target)
end
