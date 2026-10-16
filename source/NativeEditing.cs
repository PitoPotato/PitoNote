using System;
using System.IO;
using System.Text;

namespace PitoNote {
    public static partial class Engine {
        public static void WriteNativeAssets() {
            Directory.CreateDirectory(Path.Combine(Resources,"Drafts"));
            AtomicWrite(Path.Combine(Resources,"NativeEdit.lua"),NativeEditScript);
            string path=Path.Combine(Resources,"Plugins","x64","PitoNoteSnap.dll");
            using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("PitoNote.PitoNoteSnap.dll"))
            using(var memory=new MemoryStream()){
                stream.CopyTo(memory);byte[] bytes=memory.ToArray();
                if(!File.Exists(path)||!System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes(path),bytes)){
                    Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,bytes);
                }
            }
        }
        // User text is read as a value inside Lua, never interpolated into executable code,
        // a Rainmeter command or an external command line. Full-length Unicode lives in files.
        const string NativeEditScript=@"
local cached = {}
local root, id

function Initialize()
    root = SKIN:GetVariable('@')
    id = SKIN:GetVariable('NoteId')
end

function InitializeContent()
    -- Rainmeter's own four-edge magnet is replaced by the per-note native module.
    SKIN:Bang('!SnapEdges',0)
    NormalizeAnchor()
    for _, field in ipairs({'Title', 'Body'}) do
        local suffix = id .. '.' .. string.lower(field) .. '.txt'
        local value = SKIN:ReadTextFile(root .. 'Drafts\\' .. suffix)
        if value == nil then value = SKIN:ReadTextFile(root .. 'Content\\' .. suffix) end
        value = value or ''
        cached[field] = value
        local meter = SKIN:GetMeter(field)
        if meter then meter:SetText(value) end
    end
    SKIN:Bang('!UpdateMeter', 'Title')
    SKIN:Bang('!UpdateMeter', 'Body')
    SKIN:Bang('!Redraw')
end

function NormalizeAnchor()
    local x,y=SKIN:GetX(),SKIN:GetY()
    SKIN:Bang('!SetAnchor',0,0)
    SKIN:Bang('!CommandMeasure','NoteScript','RestorePosition('..x..','..y..')')
end
function RestorePosition(x,y)
    SKIN:MoveWindow(x,y)
end
function ProbeSnapState()
    local measure=SKIN:GetMeasure('SnapController')
    SKIN:WriteTextFile(root..id..'.snap-check.txt',tostring(measure and measure:GetValue() or 0),'UTF-8')
end
function ProbeSnapLayout()
    local bounds=SKIN:GetMeter('WindowBounds')
    SKIN:WriteTextFile(root..id..'.snap-layout.txt',SKIN:GetVariable('NoteW')..'|'..SKIN:GetVariable('NoteH')..'|'..bounds:GetW()..'|'..bounds:GetH()..'|'..SKIN:GetW()..'|'..SKIN:GetH(),'UTF-8')
end

function Focus(field)
    SKIN:Bang('!CommandMeasure', 'AutoSave', 'Stop 1')
    SKIN:Bang('!CommandMeasure', 'AutoSave', 'Stop 2')
    SKIN:Bang('!CommandMeasure', 'AutoSave', field == 'Title' and 'Execute 1' or 'Execute 2')
end

function Capture(field)
    if field ~= 'Title' and field ~= 'Body' then return end
    local value = SKIN:ReplaceVariables('[$Input]')
    -- The native multiline control uses CRLF; value files may use LF.
    -- A newline representation change alone must not create an autosave process.
    if cached[field] ~= nil and value:gsub('\r\n', '\n') == cached[field]:gsub('\r\n', '\n') then return end
    local path = root .. 'Drafts\\' .. id .. '.' .. string.lower(field) .. '.txt'
    local ok, err = SKIN:WriteTextFile(path, value, 'UTF-8')
    if not ok then
        SKIN:Bang('!Log', 'PitoNote: cannot save note draft: ' .. (err or ''), 'Error')
        return
    end
    cached[field] = value
    SKIN:Bang('[""' .. root .. 'PitoNote.exe"" ""--commit-field"" ""' .. id .. '"" ""' .. field .. '""]')
end

function Dismiss(field)
    Capture(field)
    SKIN:Bang('!CommandMeasure', 'AutoSave', field == 'Title' and 'Stop 1' or 'Stop 2')
end

function DismissInput()
    SKIN:Bang('!TextEdit:Dismiss')
end

function FocusBody()
    SKIN:Bang('!TextEdit:Focus', 'Body')
end

function FocusTitle()
    SKIN:Bang('!TextEdit:Focus', 'Title')
end

-- Diagnostic: read the native meter through its real submit action, without injecting keys.
function VerifyNativeValue()
    cached['Body'] = nil
    SKIN:Bang('!TextEdit:Submit', 'Body')
end

function VerifyNativeAutosave()
    cached['Body'] = nil
    SKIN:Bang('!TextEdit:Focus', 'Body')
end

function VerifyNativeLongValue()
    local value = SKIN:ReadTextFile(root .. 'ProbeExpected.txt')
    if value == nil then return end
    SKIN:GetMeter('Body'):SetText(value)
    cached['Body'] = nil
    SKIN:Bang('!UpdateMeter', 'Body')
    SKIN:Bang('!TextEdit:Submit', 'Body')
end
";
        static void EditableText(StringBuilder output,string name,string font,double size,int weight,string color,string x,string y,string width,string height,Settings settings,bool title) {
            string capture="[!CommandMeasure NoteScript \"Capture('"+name+"')\"]";
            Section(output,name,"Meter=TextEdit","X="+x,"Y="+y,"W="+width,"H="+height,
                "FontFace="+font,"FontSize="+Num(size),"FontWeight="+weight,"FontColor="+color,
                "StringStyle="+(title?settings.TitleStyle:settings.FontStyle),"StringAlign="+(title?"LeftTop":settings.TextAlign+"Top"),
                "InitialText=","PlaceholderText="+Enc(title?"Title":"Write something to remember…"),
                "PlaceholderFontFace="+font,"PlaceholderFontSize="+Num(size),"PlaceholderFontColor="+color,
                "CaretColor="+color,"SelectionColor="+Rgb(settings.Accent,65),"FocusBorderWidth=0",
                "Multiline="+(title?0:1),"SubmitOnEnter="+(title?1:0),"MaxLength="+(title?160:100000),
                "ReadOnly="+(settings.ReadOnly?1:0),"ClipString=1","AntiAlias=1","SolidColor=0,0,0,1","DynamicVariables=1",
                "Hidden="+(title&&!settings.ShowTitle?1:0),"MouseActionCursorName=Text",
                "OnFocusAction=[!CommandMeasure NoteScript \"Focus('"+name+"')\"]",
                "OnSubmitAction="+capture,"OnDismissAction=[!CommandMeasure NoteScript \"Dismiss('"+name+"')\"]");
        }
        public static string BuildNote(Settings defaults,Note note,int cascade=0) {
            var settings=Effective(defaults,note);int width=NoteWidth(settings,note),height=NoteHeight(settings,note);
            string id=note==null?"":note.Id;int z=settings.Position=="Desktop"?-2:settings.Position=="Topmost"?1:0;
            var output=new StringBuilder();bool hidden=settings.ToolbarMode!="Always";
            output.AppendLine("; PitoNote 3.5.0. Native Rainmeter TextEdit; no external input window.");
            string dismiss="[!TextEdit:Dismiss]";
            string stopTimers="[!CommandMeasure AutoSave \"Stop 1\"][!CommandMeasure AutoSave \"Stop 2\"]";
            dismiss+=stopTimers;
            Section(output,"Rainmeter","AppVersion=5.0.0.5293","SyntaxVersion=2","Update=-1","AccurateText=1","DynamicWindowSize=1",
                "DefaultWindowX="+(189+cascade*28),"DefaultWindowY="+(152+cascade*28),"DefaultAnchorX=0","DefaultAnchorY=0","DefaultSnapEdges=0","DefaultAlwaysOnTop="+z,"DefaultDraggable="+(settings.Draggable?1:0),"DefaultKeepOnScreen=1",
                "OnRefreshAction=[!ZPos "+z+"][!Draggable "+(settings.Draggable?1:0)+"][!CommandMeasure NoteScript InitializeContent()][!CommandMeasure CatalogScript \"SetVisibility('"+id+"',1)\" \""+CatalogConfig+"\"]",
                "OnUnfocusAction="+stopTimers,"OnCloseAction="+dismiss+"[!CommandMeasure CatalogScript \"SetVisibility('"+id+"',0)\" \""+CatalogConfig+"\"]",
                "MouseOverAction="+(settings.ToolbarMode=="Hover"?"[!ShowMeterGroup NoteControls][!Redraw]":""),
                "MouseLeaveAction="+(settings.ToolbarMode=="Hover"?"[!HideMeterGroup NoteControls][!Redraw]":""),
                "ContextTitle=Note settings","ContextAction="+dismiss+Act("details",id),
                "ContextTitle2="+(settings.ReadOnly?"Unlock editing in Note settings":"Edit text"),"ContextAction2="+(settings.ReadOnly?Act("details",id):note!=null&&note.Collapsed?Act("desktop-edit",id):"[!TextEdit:Focus Body]"),
                "ContextTitle3=Hide note","ContextAction3="+dismiss+Act("hide",id),
                "ContextTitle4="+(note!=null&&note.Done?"Mark as incomplete":"Mark as complete"),"ContextAction4="+dismiss+Act("toggle",id),
                "ContextTitle5=Delete note…","ContextAction5="+dismiss+Act("delete",id),
                "ContextTitle6="+(note!=null&&note.Collapsed?"Expand note":"Collapse note"),"ContextAction6="+dismiss+Act("collapse",id),
                "ContextTitle7=Duplicate note","ContextAction7="+dismiss+Act("duplicate",id));
            Section(output,"Metadata","Name=PitoNote","Author=PitoNote; original Notes++ by raziEiL / Eclectic-Tech / killall-q","Version=3.5.0","License=CC BY-NC-SA 4.0");
            Section(output,"Variables","NoteId="+id,"NoteW="+width,"NoteH="+height,"Pad="+(note!=null&&note.Collapsed?Math.Min(16,settings.Padding):settings.Padding),"Radius="+(note!=null&&note.Collapsed?Math.Min(settings.Radius,height/2):settings.Radius),"BodyTop="+BodyTop(settings));
            Section(output,"NoteScript","Measure=Script","ScriptFile=#@#NativeEdit.lua","UpdateDivider=-1");
            Section(output,"SnapController","Measure=Plugin","Plugin=PitoNoteSnap","Scope="+Config,"SnapEnabled="+(settings.SmartSnap?1:0),"SnapDistance="+settings.SnapDistance,"SnapGap="+settings.SnapGap,"UpdateDivider=-1");
            Section(output,"AutoSave","Measure=ActionTimer","ActionList1=Repeat SaveTitle,1000,100000","ActionList2=Repeat SaveBody,1000,100000",
                "SaveTitle=[!TextEdit:Submit Title]","SaveBody=[!TextEdit:Submit Body]","UpdateDivider=-1");
            Section(output,"WindowBounds","Meter=Image","W=(#NoteW#+16)","H=(#NoteH#+16)","SolidColor=0,0,0,0","DynamicVariables=1");
            if(settings.Shadow==1)Section(output,"Shadow","Meter=Shape","X=8","Y=12","Shape=Rectangle 0,0,#NoteW#,#NoteH#,#Radius# | Fill Color 0,0,0,22 | StrokeWidth 0","DynamicVariables=1");
            Section(output,"Paper","Meter=Shape","X=8","Y=8","Shape=Rectangle 0,0,#NoteW#,#NoteH#,#Radius# | Fill Color "+Rgb(settings.Background,settings.Opacity)+" | StrokeWidth "+settings.BorderWidth+" | Stroke Color "+Rgb(settings.BorderColor,settings.BorderOpacity),"DynamicVariables=1");
            if(note!=null&&note.Collapsed){
                var compact=Clone(settings);compact.ShowTitle=true;compact.ReadOnly=true;compact.Padding=Math.Min(16,settings.Padding);
                EditableText(output,"Title",compact.TitleFontName,compact.TitleSize,compact.TitleWeight,Rgb(compact.TitleColor),"(8+#Pad#)","((#NoteH#-"+TitleHeight(compact)+")/2+8)","(#NoteW#-2*#Pad#-36)",TitleHeight(compact).ToString(),compact,true);
                Icon(output,"Expand","expand","(#NoteW#-30)","((#NoteH#-32)/2+8)",Rgb(settings.Accent).Substring(0,Rgb(settings.Accent).LastIndexOf(',')),"Expand note",dismiss+Act("collapse",id),"NoteControls",hidden);
                return output.ToString();
            }
            EditableText(output,"Title",settings.TitleFontName,settings.TitleSize,settings.TitleWeight,Rgb(settings.TitleColor),"(8+#Pad#)",TitleTop(settings).ToString(),"(#NoteW#-2*#Pad#)",TitleHeight(settings).ToString(),settings,true);
            EditableText(output,"Body",settings.FontName,settings.FontSize,settings.FontWeight,Rgb(settings.TextColor),"(8+#Pad#)","#BodyTop#","(#NoteW#-2*#Pad#)","(#NoteH#+8-#Pad#-#BodyTop#)",settings,false);
            string color=Rgb(settings.Accent);color=color.Substring(0,color.LastIndexOf(','));
            Section(output,"ToolbarSurface","Meter=Shape","X=(#NoteW#-130)","Y=(#NoteH#-28)","Shape=Rectangle 0,0,102,32,9 | Fill Color "+Rgb(settings.Background,245)+" | StrokeWidth 1 | Stroke Color "+Rgb(settings.Accent,22),"Group=NoteControls","Hidden="+(hidden?1:0),"DynamicVariables=1");
            Icon(output,"Complete",note!=null&&note.Done?"undo":"done","(#NoteW#-128)","(#NoteH#-28)",color,note!=null&&note.Done?"Mark as incomplete":"Mark as complete",dismiss+Act("toggle",id),"NoteControls",hidden);
            Icon(output,"Settings","gear","(#NoteW#-96)","(#NoteH#-28)",color,"Note settings",dismiss+Act("details",id),"NoteControls",hidden);
            Icon(output,"Close","close","(#NoteW#-64)","(#NoteH#-28)",color,"Hide note, keep content",dismiss+Act("hide",id),"NoteControls",hidden);
            if(note!=null) {
                string resize=dismiss+"[\"#@#PitoNote.exe\" \"--resize\" \""+id+"\" \"$MouseX$\" \"$MouseY$\"]";
                var grip=new System.Collections.Generic.List<string>{"Meter=Shape","X=(#NoteW#-16)","Y=(#NoteH#-16)","Shape=Rectangle 0,0,24,24 | Fill Color 0,0,0,1 | StrokeWidth 0","DynamicVariables=1","MouseActionCursorName=SIZE_NWSE","ToolTipText=Drag to resize this note","ToolTipFontFace=Segoe UI","LeftMouseDownAction="+resize};
                if(settings.ToolbarMode!="Hidden"){grip.Add("Shape2=Line 7,17,17,7 | Stroke Color "+Rgb(settings.Accent,160)+" | StrokeWidth 1.5");grip.Add("Shape3=Line 12,17,17,12 | Stroke Color "+Rgb(settings.Accent,160)+" | StrokeWidth 1.5");grip.Add("Group=NoteControls");grip.Add("Hidden="+(hidden?1:0));}
                Section(output,"ResizeGrip",grip.ToArray());
            }
            return output.ToString();
        }
    }
}
