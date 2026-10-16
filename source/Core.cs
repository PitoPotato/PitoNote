using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace PitoNote {
    [DataContract] public class Note {
        [DataMember] public string Id = Guid.NewGuid().ToString("N");
        [DataMember] public string Title = "";
        [DataMember] public string Body = "";
        [DataMember] public string Theme = "Default";
        [DataMember] public string FontName = "";
        [DataMember] public double FontSize = 0;
        [DataMember] public bool Done = false;
        [DataMember] public bool Archived = false;
        [DataMember] public bool Pinned = true;
        [DataMember] public bool Collapsed = false;
        [DataMember] public int Width = 0;
        [DataMember] public int Height = 0;
        [DataMember] public Settings Appearance;
    }
    [DataContract] public class Settings {
        [DataMember] public string FontName = "Segoe UI";
        [DataMember] public double FontSize = 12;
        [DataMember] public double TitleSize = 14;
        [DataMember] public string FontStyle = "Normal";
        [DataMember] public int Width = 356;
        [DataMember] public int CardHeight = 220;
        [DataMember] public int Radius = 18;
        [DataMember] public int Padding = 18;
        [DataMember] public int Gap = 12;
        [DataMember] public int PageSize = 3;
        [DataMember] public int Opacity = 255;
        [DataMember] public int BorderWidth = 1;
        [DataMember] public int Shadow = 1;
        [DataMember] public string Theme = "Yellow";
        [DataMember] public string Background = "FFF2B2";
        [DataMember] public string TextColor = "302D26";
        [DataMember] public string Accent = "66572E";
        [DataMember] public string BoardColor = "FBFAF7";
        [DataMember] public bool HideDone = false;
        [DataMember] public bool ShowHeader = true;
        [DataMember] public bool Draggable = true;
        [DataMember] public string Position = "Desktop";
        [DataMember] public string BoardTitle = "Notes";
        [DataMember] public string ToolbarMode = "Hover";
        [DataMember] public string TitleFontName = "Segoe UI";
        [DataMember] public string TitleStyle = "Normal";
        [DataMember] public string TitleColor = "302D26";
        [DataMember] public string BorderColor = "66572E";
        [DataMember] public int BorderOpacity = 32;
        [DataMember] public int FontWeight = 400;
        [DataMember] public int TitleWeight = 600;
        [DataMember] public string TextAlign = "Left";
        [DataMember] public int TitleGap = 6;
        [DataMember] public bool ShowTitle = true;
        [DataMember] public bool ReadOnly = false;
        [OnDeserializing] void NewOptionDefaults(StreamingContext context) {
            TitleFontName="Segoe UI";TitleStyle="Normal";TitleColor="302D26";BorderColor="66572E";BorderOpacity=32;
            FontWeight=400;TitleWeight=600;TextAlign="Left";TitleGap=6;ShowTitle=true;ToolbarMode="Hover";
        }
    }
    [DataContract] public class Store {
        [DataMember] public int Version = 3;
        [DataMember] public Settings Settings = new Settings();
        [DataMember] public List<Note> Notes = new List<Note>();
        [DataMember] public int Page = 0;
    }
    public class Palette {
        public string Bg, Text, Accent;
        public Palette(string bg, string text, string accent) { Bg=bg; Text=text; Accent=accent; }
    }
    public static partial class Engine {
        public static string Root;
        public static string Resources { get { return Path.Combine(Root, "@Resources"); } }
        public static string DataPath { get { return Path.Combine(Resources, "data.json"); } }
        public static string Config { get { return new DirectoryInfo(Root).Name; } }
        public static readonly string Rainmeter = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Rainmeter", "Rainmeter.exe");
        public static readonly Dictionary<string,Palette> Themes = new Dictionary<string,Palette> {
            {"Yellow", new Palette("FFF2B2","302D26","66572E")},
            {"Pink", new Palette("FFE2E8","452D36","8E3E60")},
            {"Blue", new Palette("DCEEFF","25394D","305E8D")},
            {"Green", new Palette("E0F1DB","293B2D","3F6848")},
            {"Purple", new Palette("EDE3FF","382D4B","6D508B")},
            {"Paper", new Palette("FFFFFF","27272A","53535C")},
            {"Dark", new Palette("292C31","F4F4F5","B4CEF5")}
        };
        static Dictionary<string,FontFamily> installedFonts;
        static Dictionary<string,FontFamily> InstalledFonts() {
            if(installedFonts!=null)return installedFonts;
            var found=new Dictionary<string,FontFamily>(StringComparer.OrdinalIgnoreCase);
            foreach(var family in Fonts.SystemFontFamilies)found[family.Source]=family;
            // WPF's system enumeration can omit per-user fonts on Windows 11.
            string userFonts=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Microsoft","Windows","Fonts");
            if(Directory.Exists(userFonts))foreach(var family in Fonts.GetFontFamilies(new Uri(userFonts+Path.DirectorySeparatorChar)))foreach(string name in family.FamilyNames.Values)if(!found.ContainsKey(name))found[name]=family;
            installedFonts=found;return found;
        }
        public static string[] FontNames() {return InstalledFonts().Keys.OrderBy(s=>s,StringComparer.CurrentCultureIgnoreCase).ToArray();}
        public static FontFamily ResolveFont(string name){FontFamily family;return InstalledFonts().TryGetValue(name,out family)?family:new FontFamily(name);}
        public static void AtomicWrite(string path, string contents) {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            Encoding encoding=Path.GetExtension(path).Equals(".ini",StringComparison.OrdinalIgnoreCase)?Encoding.Unicode:(Encoding)new UTF8Encoding(true);
            File.WriteAllText(temp, contents, encoding);
            try { if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path); }
            finally { if(File.Exists(temp)) File.Delete(temp); }
        }
        public static string Json(Store s) {
            using(var ms=new MemoryStream()) { new DataContractJsonSerializer(typeof(Store)).WriteObject(ms,s); return Encoding.UTF8.GetString(ms.ToArray()); }
        }
        public static Store Parse(string text) {
            using(var ms=new MemoryStream(Encoding.UTF8.GetBytes(text))) {
                var s=(Store)new DataContractJsonSerializer(typeof(Store)).ReadObject(ms);
                Validate(s); return s;
            }
        }
        public static Store Load() {
            if(File.Exists(DataPath)) {var saved=Parse(File.ReadAllText(DataPath,Encoding.UTF8));ReadDrafts(saved);return saved;}
            var s=new Store();
            string original=Path.Combine(Directory.GetParent(Root).FullName,"Notes-pp","Notes-pp","Notes.txt");
            if(File.Exists(original)) ImportLegacy(s,original);
            Save(s,false); return s;
        }
        public static void Save(Store s, bool snapshot=true) {
            Validate(s);
            if(snapshot && File.Exists(DataPath)) {
                string history=Path.Combine(Resources,"Backups"); Directory.CreateDirectory(history);
                File.Copy(DataPath,Path.Combine(history,DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".json"));
                // Keep history: never silently delete a user's previous revision.
            }
            AtomicWrite(DataPath,Json(s));
            WriteCatalogIndex(s);
        }
        public static void Validate(Store s) {
            if(s==null || s.Settings==null || s.Notes==null) throw new InvalidDataException("This file does not contain valid PitoNote data.");
            Upgrade(s);
            if(s.Version!=3) throw new InvalidDataException("Unsupported data version.");
            var t=s.Settings;
            if(string.IsNullOrEmpty(t.ToolbarMode))t.ToolbarMode="Hover";
            if(!new[]{"Hover","Hidden","Always"}.Contains(t.ToolbarMode))throw new InvalidDataException("Invalid icon visibility mode.");
            ValidateOptions(t);
            CheckFont(t.FontName); CheckColor(t.Background); CheckColor(t.TextColor); CheckColor(t.Accent); CheckColor(t.BoardColor);
            if(!new[]{"Normal","Bold","Italic","BoldItalic"}.Contains(t.FontStyle)) throw new InvalidDataException("Invalid font style.");
            if(!new[]{"Desktop","Normal","Topmost"}.Contains(t.Position)) throw new InvalidDataException("Invalid window layer.");
            if(t.BoardTitle==null || t.BoardTitle.Length>80) throw new InvalidDataException("The panel title must be 80 characters or fewer.");
            if(s.Notes.Count>1000) throw new InvalidDataException("A data store can contain up to 1,000 notes.");
            var ids=new HashSet<string>();
            foreach(var n in s.Notes) {
                if(n==null || n.Id==null || !Regex.IsMatch(n.Id,"^[a-f0-9]{32}$") || !ids.Add(n.Id)) throw new InvalidDataException("Invalid or duplicate note ID.");
                if(n.Body==null || n.Title==null || n.Body.Length>100000 || n.Title.Length>160) throw new InvalidDataException("Invalid note text (title: 160 characters; body: 100,000 characters).");
                if(n.Theme!="Default" && !Themes.ContainsKey(n.Theme ?? "")) throw new InvalidDataException("Invalid note theme.");
                if(!string.IsNullOrEmpty(n.FontName)) CheckFont(n.FontName);
                if(n.FontSize!=0) Check(n.FontSize,8,36,"Note font size");
                if(n.Width!=0) Check(n.Width,220,1600,"Note width");
                if(n.Height!=0) Check(n.Height,180,1400,"Note height");
                ValidateOptions(n.Appearance);
            }
            s.Page=Math.Max(0,s.Page);
        }
        static void Check(double n,double lo,double hi,string name) { if(double.IsNaN(n) || double.IsInfinity(n) || n<lo || n>hi) throw new InvalidDataException(name+" must be between "+lo+" and "+hi+"."); }
        static void CheckFont(string name) { if(string.IsNullOrWhiteSpace(name) || name.Length>200 || name.IndexOfAny(new[]{'\r','\n','"','#','[',']'})>=0) throw new InvalidDataException("Invalid font name."); }
        static void CheckColor(string s) { if(s==null || !Regex.IsMatch(s,"^[0-9a-fA-F]{6}$")) throw new InvalidDataException("Colors must contain 6 HEX digits, for example FFF2B2."); }
        public static void ApplyTheme(Settings s,string theme) { var p=Themes[theme]; s.Theme=theme; s.Background=p.Bg; s.TextColor=p.Text; s.TitleColor=p.Text;s.Accent=p.Accent;s.BorderColor=p.Accent; s.BoardColor=theme=="Dark" ? "202226" : "FBFAF7"; }
        public static Palette Colors(Settings s,Note n) {var t=n!=null&&n.Appearance!=null?n.Appearance:s;return new Palette(t.Background,t.TextColor,t.Accent);}
        public static void ImportLegacy(Store s,string path) {
            string text=File.ReadAllText(path,Encoding.Default);
            foreach(Match m in Regex.Matches(text,@"(?m)^Item(\d+)=(.*)$")) {
                string body=m.Groups[2].Value.Trim().Replace("#CRLF#","\r\n").Replace("#*CRLF*#","\r\n");
                if(!string.IsNullOrWhiteSpace(body)) s.Notes.Add(new Note {Body=body,Title=body.Split('\n')[0].Trim().Substring(0,Math.Min(80,body.Split('\n')[0].Trim().Length))});
            }
        }
        public static void Bang(string args) { Native.Command(args); }
        public static void Refresh(Store s) {
            Bang("!DeactivateConfig \""+Config+"\\Board\"");
            Bang("!DeactivateConfig \""+Config+"\\Launcher\"");
            Bang("!RefreshApp");
            System.Threading.Thread.Sleep(120);
            foreach(var n in s.Notes) {
                string config=Config+"\\Notes\\"+n.Id;
                if(n.Pinned && !n.Archived && (!Effective(s.Settings,n).HideDone || !n.Done)) {
                    if(Native.SkinHandle(config)!=IntPtr.Zero) Bang("!Refresh \""+config+"\"");
                    else Bang("!ActivateConfig \""+config+"\" \"Note.ini\"");
                } else Bang("!DeactivateConfig \""+config+"\"");
            }
            string control=Config+"\\Control";
            if(Native.SkinHandle(control)!=IntPtr.Zero) Bang("!Refresh \""+control+"\"");
            else Bang("!ActivateConfig \""+control+"\" \"Control.ini\"");
            NotifyCatalog();
        }
        public static void Sync(Store s, bool geometry=false) {
            Native.WithDataLock(()=>{
                if(!geometry && File.Exists(DataPath)) {
                    var current=Load();
                    foreach(var n in s.Notes) { var match=current.Notes.FirstOrDefault(x=>x.Id==n.Id); if(match!=null) {n.Width=match.Width;n.Height=match.Height;} }
                }
                Save(s);Render(s);
            });
            Refresh(s);
        }
        public static void Pin(Store s,Note n) {
            n.Pinned=!n.Pinned; Sync(s);
        }
        public static Note CreateNote() {
            Note created=null;
            Native.WithDataLock(()=> {var latest=Load();created=NewNote(latest.Settings);latest.Notes.Insert(0,created);Save(latest);WriteNativeAssets();WriteContent(created);AtomicWrite(Path.Combine(Root,"Notes",created.Id,"Note.ini"),BuildNote(latest.Settings,created));});
            RefreshNote(Load(),created.Id);return created;
        }
        public static bool SaveInline(string id,string title,string body) {
            bool changed=false;
            Native.WithDataLock(()=> {
                var latest=Load();var n=latest.Notes.FirstOrDefault(x=>x.Id==id);
                if(n==null)throw new InvalidDataException("This note is no longer in the data store.");
                if(n.Title==title && n.Body==body)return;
                n.Title=title;n.Body=body;Save(latest);Render(latest);changed=true;
            });
            return changed;
        }
        public static string Enc(string text) {
            // Encode user text as Rainmeter character references: #variables# and [!Bangs]
            // must never become executable skin syntax. Newlines are generated, not interpolated.
            var b=new StringBuilder();
            foreach(char c in (text??"").Replace("\r\n","\n").Replace('\r','\n')) {
                if(c=='\n') b.Append("#CRLF#");
                else if(c=='\t') b.Append("    ");
                else if(c>=32) b.Append("[\\x"+((int)c).ToString("X4")+"]");
            }
            return b.ToString();
        }
        static string Rgb(string hex,int alpha=255) { var c=(Color)ColorConverter.ConvertFromString("#"+hex); return c.R+","+c.G+","+c.B+","+alpha; }
        static string Num(double n) { return n.ToString("0.##",CultureInfo.InvariantCulture); }
        static void Section(StringBuilder b,string name,params string[] lines) { b.AppendLine("["+name+"]"); foreach(var l in lines)b.AppendLine(l); b.AppendLine(); }
        static string Act(string verb,string id="") { return "[\"#@#PitoNote.exe\" \""+verb+"\""+(id==""?"":" \""+id+"\"")+"]"; }
        static void Text(StringBuilder b,string name,string value,int x,int y,int w,int h,double size,string font,string color,string action=null,string style="Normal") {
            var l=new List<string>{"Meter=String","X="+x,"Y="+y,"W="+w,"H="+h,"FontFace="+font,"FontSize="+Num(size),"FontColor="+color,"StringStyle="+style,"AntiAlias=1","ClipString=1","Text="+Enc(value),"SolidColor=0,0,0,1"};
            if(action!=null) { l.Add("LeftMouseUpAction="+action); l.Add("MouseActionCursor=1"); }
            Section(b,name,l.ToArray());
        }
        static void Rect(StringBuilder b,string name,int x,int y,int w,int h,int radius,string fill,string stroke="0,0,0,0",int border=0) {
            Section(b,name,"Meter=Shape","X="+x,"Y="+y,"Shape=Rectangle 1,1,"+(w-2)+","+(h-2)+","+radius+" | Fill Color "+fill+" | StrokeWidth "+border+" | Stroke Color "+stroke);
        }
        static void Button(StringBuilder b,string name,string label,int x,int y,int w,string bg,string fg,string action) {
            Section(b,name+"Bg","Meter=Shape","X="+x,"Y="+y,"Shape=Rectangle 0,0,"+w+",32,9 | Fill Color "+bg+" | StrokeWidth 0","LeftMouseUpAction="+action,"MouseActionCursor=1","MouseOverAction=[!SetOption "+name+"Bg Shape \"Rectangle 0,0,"+w+",32,9 | Fill Color "+fg.Substring(0,fg.LastIndexOf(','))+",35 | StrokeWidth 0\"][!UpdateMeter "+name+"Bg][!Redraw]","MouseLeaveAction=[!SetOption "+name+"Bg Shape \"Rectangle 0,0,"+w+",32,9 | Fill Color "+bg+" | StrokeWidth 0\"][!UpdateMeter "+name+"Bg][!Redraw]");
            Text(b,name,label,x+8,y+6,w-16,23,10,"Segoe UI",fg,action);
        }
        public static void Render(Store store) {
            WriteNativeAssets();
            WriteCatalogAssets();WriteCatalogIndex(store);
            AtomicWrite(Path.Combine(Root,"Control","Control.ini"),BuildControl(store.Settings));
            for(int i=0;i<store.Notes.Count;i++) {
                var n=store.Notes[i];
                WriteContent(n);
                AtomicWrite(Path.Combine(Root,"Notes",n.Id,"Note.ini"),BuildNote(store.Settings,n,(store.Notes.Count-1-i)%8));
            }
        }
        public static int NoteWidth(Settings t,Note n) {return n!=null&&n.Width>0?n.Width:Effective(t,n).Width;}
        public static string BodyPreview(string body) {
            // Rainmeter's INI value buffer is finite; the complete Unicode note stays in JSON.
            // Keep this escaped value under 4096 characters, including character references.
            if(body.Length<=450)return body;
            int end=450;if(char.IsHighSurrogate(body[end-1]))end--;
            return body.Substring(0,end)+"…";
        }
        public static int MinNoteHeight(Settings t) {return Math.Max(180,2*t.Padding+(t.ShowTitle?TitleHeight(t)+t.TitleGap:0)+40);}
        public static int CollapsedHeight(Settings t){return Math.Max(52,TitleHeight(t)+2*Math.Min(16,t.Padding));}
        public static int NoteHeight(Settings t,Note n) {t=Effective(t,n);return n!=null&&n.Collapsed?CollapsedHeight(t):Math.Max(MinNoteHeight(t),n!=null&&n.Height>0?n.Height:t.CardHeight);}
        public static int TitleTop(Settings t) {return 8+t.Padding;}
        public static int TitleHeight(Settings t) {return (int)Math.Ceiling(t.TitleSize*96/72*1.4);}
        public const int TitleBodyGap=6;
        public static int BodyTop(Settings t) {return TitleTop(t)+(t.ShowTitle?TitleHeight(t)+t.TitleGap:0);}
        public static int ContentHeight(Settings t,Note n) {return NoteHeight(t,n)-2*t.Padding;}
        static void DynamicText(StringBuilder b,string name,string text,string x,string y,string w,string h,string font,double size,string color,string action,string style="Normal") {
            Section(b,name,"Meter=String","X="+x,"Y="+y,"W="+w,"H="+h,"FontFace="+font,"FontSize="+Num(size),"FontColor="+color,"FontWeight="+(style.Contains("Bold")?600:400),"StringStyle="+(style.Contains("Italic")?"Italic":"Normal"),"Text="+Enc(text),"AntiAlias=1","ClipString=1","SolidColor=0,0,0,1","DynamicVariables=1","LeftMouseUpAction="+action,"MouseActionCursorName=Text","ToolTipText=Click to edit directly","ToolTipFontFace=Segoe UI","ToolTipFontSize=10");
        }
        static void Icon(StringBuilder b,string name,string type,string x,string y,string color,string tooltip,string action,string group=null,bool hidden=false) {
            var lines=new List<string>{"Meter=Shape","X="+x,"Y="+y,"Shape=Rectangle 0,0,32,32,8 | Fill Color "+color+",0 | StrokeWidth 0","DynamicVariables=1","MouseActionCursorName=Hand","ToolTipText="+tooltip,"ToolTipFontFace=Segoe UI","ToolTipFontSize=10","ToolTipType=0","LeftMouseUpAction="+action};
            if(group!=null){lines.Add("Group="+group);lines.Add("Hidden="+(hidden?1:0));}
            string stroke=" | Fill Color 0,0,0,0 | StrokeWidth 1.7 | Stroke Color "+color+",220 | StrokeStartCap Round | StrokeEndCap Round | StrokeLineJoin Round";
            var shapes=new List<string>();
            if(type=="add") {shapes.Add("Line 16,10,16,22");shapes.Add("Line 10,16,22,16");}
            else if(type=="close") {shapes.Add("Line 11,11,21,21");shapes.Add("Line 21,11,11,21");}
            else if(type=="edit") {lines.Add("Pen=10,19 | LineTo 10,22 | LineTo 13,22 | LineTo 23,12 | LineTo 20,9 | ClosePath 1");shapes.Add("Path Pen");shapes.Add("Line 18,11,21,14");}
            else if(type=="done") {lines.Add("Check=9,16 | LineTo 14,21 | LineTo 23,11");shapes.Add("Path Check");}
            else if(type=="undo") {lines.Add("Undo=11,10 | LineTo 7,14 | LineTo 11,18");shapes.Add("Path Undo");shapes.Add("Line 8,14,19,14");shapes.Add("Arc 19,14,19,23,4,4,0,1,0");}
            else if(type=="list") {shapes.Add("Line 12,10,23,10");shapes.Add("Line 12,16,23,16");shapes.Add("Line 12,22,23,22");shapes.Add("Ellipse 8,10,0.5");shapes.Add("Ellipse 8,16,0.5");shapes.Add("Ellipse 8,22,0.5");}
            else if(type=="gear") {shapes.Add("Ellipse 16,16,5");shapes.Add("Ellipse 16,16,1.2");shapes.Add("Line 16,7,16,10");shapes.Add("Line 16,22,16,25");shapes.Add("Line 7,16,10,16");shapes.Add("Line 22,16,25,16");shapes.Add("Line 9.6,9.6,11.6,11.6");shapes.Add("Line 20.4,20.4,22.4,22.4");shapes.Add("Line 9.6,22.4,11.6,20.4");shapes.Add("Line 20.4,11.6,22.4,9.6");}
            else if(type=="expand") {shapes.Add("Line 10,11,16,17");shapes.Add("Line 16,17,22,11");shapes.Add("Line 10,17,16,23");shapes.Add("Line 16,23,22,17");}
            else if(type=="up") {shapes.Add("Line 10,19,16,13");shapes.Add("Line 16,13,22,19");}
            else if(type=="down") {shapes.Add("Line 10,13,16,19");shapes.Add("Line 16,19,22,13");}
            else if(type=="copy") {shapes.Add("Rectangle 12,12,13,13,1");shapes.Add("Line 8,20,8,7");shapes.Add("Line 8,7,20,7");}
            else if(type=="archive") {shapes.Add("Rectangle 7,7,18,5,1");shapes.Add("Rectangle 9,12,14,13,1");shapes.Add("Line 13,16,19,16");}
            else if(type=="trash") {shapes.Add("Line 8,10,24,10");shapes.Add("Rectangle 12,6,8,4,1");lines.Add("Bin=10,10 | LineTo 11,26 | LineTo 21,26 | LineTo 22,10");shapes.Add("Path Bin");shapes.Add("Line 14,14,14,22");shapes.Add("Line 18,14,18,22");}
            for(int i=0;i<shapes.Count;i++)lines.Add("Shape"+(i+2)+"="+shapes[i]+stroke);
            lines.Add("MouseOverAction=[!SetOption "+name+" Shape \"Rectangle 0,0,32,32,8 | Fill Color "+color+",24 | StrokeWidth 0\"][!UpdateMeter "+name+"][!Redraw]");
            lines.Add("MouseLeaveAction=[!SetOption "+name+" Shape \"Rectangle 0,0,32,32,8 | Fill Color "+color+",0 | StrokeWidth 0\"][!UpdateMeter "+name+"][!Redraw]");
            Section(b,name,lines.ToArray());
        }
        public static string BuildControl(Settings t) {
            var b=new StringBuilder();
            Section(b,"Rainmeter","Update=-1","AccurateText=1","DynamicWindowSize=1","DefaultWindowX=41","DefaultWindowY=39","DefaultAlwaysOnTop=-2","OnRefreshAction=[!ActivateConfig \""+CatalogConfig+"\" \"Catalog.ini\"]","ContextTitle=New note","ContextAction="+Act("new"),"ContextTitle2=General settings","ContextAction2="+Act("settings"),"ContextTitle3=Manage notes","ContextAction3="+Act("manage"));
            Section(b,"Metadata","Name=PitoNote controls","Version=3.4.0");
            Section(b,"Variables","RootConfig="+Config);
            Section(b,"ControlScript","Measure=Script","ScriptFile=#@#ControlList.lua","UpdateDivider=-1");
            Rect(b,"Surface",4,4,238,44,13,"246,246,242,245","50,50,45,28",1);
            Section(b,"Brand","Meter=Image","ImageName=#@#Brand\\PitoNote-64.png","X=14","Y=14","W=24","H=24");
            Text(b,"Label","PitoNote",46,16,78,22,10,"Segoe UI","55,55,48,255");
            Icon(b,"Add","add","130","10","70,70,61","New note",Act("new"));
            Icon(b,"NotesList","list","166","10","70,70,61","All notes · visible, hidden and archived","[!CommandMeasure ControlScript ToggleCatalog()]");
            Icon(b,"Details","gear","202","10","70,70,61","General settings & note management",Act("settings"));
            return b.ToString();
        }
        public static double Contrast(string a,string b) {
            Func<string,double> lum=hex=>{var c=(Color)ColorConverter.ConvertFromString("#"+hex); var v=new[]{c.R/255.0,c.G/255.0,c.B/255.0}.Select(n=>n<=0.04045?n/12.92:Math.Pow((n+0.055)/1.055,2.4)).ToArray();return v[0]*0.2126+v[1]*0.7152+v[2]*0.0722;};
            double l1=lum(a),l2=lum(b);return (Math.Max(l1,l2)+0.05)/(Math.Min(l1,l2)+0.05);
        }
    }
}
