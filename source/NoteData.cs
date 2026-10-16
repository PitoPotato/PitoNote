using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;

namespace PitoNote {
    public static partial class Engine {
        public static Settings Clone(Settings value) {
            using(var stream=new MemoryStream()) {var serializer=new DataContractJsonSerializer(typeof(Settings));serializer.WriteObject(stream,value);stream.Position=0;return (Settings)serializer.ReadObject(stream);}
        }
        public static Settings Effective(Settings defaults,Note note) {return note!=null&&note.Appearance!=null?note.Appearance:defaults;}
        public static Note NewNote(Settings defaults) {
            var appearance=Clone(defaults);
            return new Note{Appearance=appearance,Width=appearance.Width,Height=Math.Max(MinNoteHeight(appearance),appearance.CardHeight)};
        }
        static void UpgradeOptions(Settings settings,bool legacy) {
            if(string.IsNullOrEmpty(settings.ToolbarMode))settings.ToolbarMode="Hover";
            if(!legacy)return;
            settings.TitleFontName=settings.FontName;settings.TitleColor=settings.TextColor;settings.BorderColor=settings.Accent;
            settings.FontWeight=settings.FontStyle.Contains("Bold")?600:400;settings.TitleWeight=600;
            settings.FontStyle=settings.FontStyle.Contains("Italic")?"Italic":"Normal";
            settings.TitleStyle="Normal";settings.TitleGap=6;settings.ShowTitle=true;
        }
        static void Upgrade(Store store) {
            if(store.Version!=2&&store.Version!=3)return;
            bool legacy=store.Version==2;
            if(legacy){store.Settings.CardHeight+=56;UpgradeOptions(store.Settings,true);}
            foreach(var note in store.Notes) {
                if(note==null)continue;
                if(note.Appearance==null) {
                    note.Appearance=Clone(store.Settings);
                    if(!string.IsNullOrEmpty(note.FontName))note.Appearance.FontName=note.Appearance.TitleFontName=note.FontName;
                    if(note.FontSize!=0)note.Appearance.FontSize=note.FontSize;
                    if(note.Theme!="Default"&&Themes.ContainsKey(note.Theme??""))ApplyTheme(note.Appearance,note.Theme);
                    if(note.Width==0)note.Width=note.Appearance.Width;
                    if(note.Height==0)note.Height=Math.Max(legacy?Math.Max(180,90+(int)Math.Ceiling(note.Appearance.TitleSize*96/72*1.4)+50):MinNoteHeight(note.Appearance),note.Appearance.CardHeight);
                    note.Appearance.Width=note.Width;note.Appearance.CardHeight=note.Height;
                }
                UpgradeOptions(note.Appearance,false);
            }
            UpgradeOptions(store.Settings,false);store.Version=3;
        }
        public static void ValidateOptions(Settings settings) {
            if(settings==null)throw new InvalidDataException("Note settings are missing.");
            Check(settings.SnapDistance,2,30,"Snap distance");Check(settings.SnapGap,0,80,"Note gap");
            Check(settings.Width,220,1600,"Width");Check(settings.CardHeight,180,1400,"Height");
            Check(settings.Radius,0,64,"Corner radius");Check(settings.Padding,4,60,"Padding");Check(settings.TitleGap,0,40,"Title spacing");
            Check(settings.FontSize,8,48,"Font size");Check(settings.TitleSize,8,48,"Title size");
            Check(settings.FontWeight,100,900,"Body weight");Check(settings.TitleWeight,100,900,"Title weight");
            Check(settings.Opacity,30,255,"Background opacity");Check(settings.BorderWidth,0,8,"Border width");Check(settings.BorderOpacity,0,255,"Border opacity");Check(settings.Shadow,0,1,"Shadow");
            CheckFont(settings.FontName);CheckFont(settings.TitleFontName);
            CheckColor(settings.Background);CheckColor(settings.TextColor);CheckColor(settings.TitleColor);CheckColor(settings.Accent);CheckColor(settings.BorderColor);
            if(!new[]{"Left","Center","Right"}.Contains(settings.TextAlign))throw new InvalidDataException("Invalid text alignment.");
            if(!new[]{"Normal","Bold","Italic","BoldItalic"}.Contains(settings.FontStyle)||!new[]{"Normal","Bold","Italic","BoldItalic"}.Contains(settings.TitleStyle))throw new InvalidDataException("Invalid font style.");
            if(!new[]{"Desktop","Normal","Topmost"}.Contains(settings.Position))throw new InvalidDataException("Invalid window layer.");
            if(!new[]{"Hover","Hidden","Always"}.Contains(settings.ToolbarMode))throw new InvalidDataException("Invalid icon visibility mode.");
            if(!Themes.ContainsKey(settings.Theme??""))throw new InvalidDataException("Invalid color palette.");
        }
        internal static string ContentPath(string id,string field,string folder) {
            if(!System.Text.RegularExpressions.Regex.IsMatch(id??"","^[a-f0-9]{32}$")||!new[]{"Title","Body"}.Contains(field))throw new InvalidDataException("Invalid content path.");
            return Path.Combine(Resources,folder,id+"."+field.ToLowerInvariant()+".txt");
        }
        public static void WriteContent(Note note) {
            AtomicWrite(ContentPath(note.Id,"Title","Content"),note.Title);AtomicWrite(ContentPath(note.Id,"Body","Content"),note.Body);
        }
        static string ReadTextRetry(string path) {
            for(int i=0;;i++){try{return File.ReadAllText(path,new UTF8Encoding(false,true));}catch(IOException){if(i>=5)throw;Thread.Sleep(30);}}
        }
        static void ReadDrafts(Store store) {
            foreach(var note in store.Notes)foreach(string field in new[]{"Title","Body"}) {
                string path=ContentPath(note.Id,field,"Drafts");if(!File.Exists(path))continue;
                string value=ReadTextRetry(path);if(value.Length>(field=="Title"?160:100000))throw new InvalidDataException("Draft exceeds the character limit: "+field);
                if(field=="Title")note.Title=value;else note.Body=value;
            }
        }
        public static void CommitField(string id,string field) {
            string path=ContentPath(id,field,"Drafts");if(!File.Exists(path))return;
            Native.WithDataLock(()=>{
                string value=ReadTextRetry(path);var persisted=Parse(File.ReadAllText(DataPath,Encoding.UTF8));
                var note=persisted.Notes.FirstOrDefault(n=>n.Id==id);if(note==null)return;
                if((field=="Title"?note.Title:note.Body)==value)return;
                ReadDrafts(persisted);Save(persisted);WriteContent(note);
                // Only files are updated. No skin refresh: focus, caret and undo remain intact.
                AtomicWrite(Path.Combine(Root,"Notes",note.Id,"Note.ini"),BuildNote(persisted.Settings,note));
            });
            NotifyCatalog();
        }
        public static void SaveDefaults(Settings settings) {
            ValidateOptions(settings);Native.WithDataLock(()=>{var latest=Load();latest.Settings=Clone(settings);Save(latest);});
        }
        public static void SaveAppearance(string id,Settings settings,Settings baseline=null,bool refresh=true) {
            ValidateOptions(settings);
            Native.WithDataLock(()=>{var latest=Load();var note=latest.Notes.FirstOrDefault(n=>n.Id==id);if(note==null)throw new InvalidDataException("Note not found.");
                if(baseline==null)note.Appearance=Clone(settings);
                else foreach(var field in typeof(Settings).GetFields())if(!object.Equals(field.GetValue(settings),field.GetValue(baseline)))field.SetValue(note.Appearance,field.GetValue(settings));
                note.Width=note.Appearance.Width;note.Height=Math.Max(MinNoteHeight(note.Appearance),note.Appearance.CardHeight);note.Appearance.CardHeight=note.Height;
                Save(latest);WriteNativeAssets();WriteContent(note);AtomicWrite(Path.Combine(Root,"Notes",id,"Note.ini"),BuildNote(latest.Settings,note));
            });
            if(refresh)RefreshNote(Load(),id);
        }
        public static void RefreshNote(Store store,string id) {
            var note=store.Notes.FirstOrDefault(n=>n.Id==id);if(note==null)return;
            string config=Config+"\\Notes\\"+id;var settings=Effective(store.Settings,note);
            if(note.Pinned&&!note.Archived&&(!settings.HideDone||!note.Done)) {
                if(Native.SkinHandle(config)==IntPtr.Zero)Bang("!ActivateConfig \""+config+"\" \"Note.ini\"");else Bang("!Refresh \""+config+"\"");
            }else Bang("!DeactivateConfig \""+config+"\"");
            NotifyCatalog();
        }
        public static void ChangeState(string id,string action) {
            Native.WithDataLock(()=>{var latest=Load();var note=latest.Notes.FirstOrDefault(n=>n.Id==id);if(note==null)return;
                if(action=="toggle")note.Done=!note.Done;
                else if(action=="hide")note.Pinned=false;
                else if(action=="pin"){note.Pinned=!note.Pinned;if(note.Pinned)note.Archived=false;}
                else if(action=="archive"){note.Archived=!note.Archived;note.Pinned=!note.Archived;}
                Save(latest);WriteContent(note);AtomicWrite(Path.Combine(Root,"Notes",id,"Note.ini"),BuildNote(latest.Settings,note));
            });RefreshNote(Load(),id);
        }
        public static void ImportStore(Store imported) {
            Validate(imported);Native.WithDataLock(()=>{
                var current=Load();foreach(var n in current.Notes.Where(n=>n.Pinned))Bang("!DeactivateConfig \""+Config+"\\Notes\\"+n.Id+"\"");
                Directory.CreateDirectory(Path.Combine(Resources,"Backups"));
                string drafts=Path.Combine(Resources,"Drafts");if(Directory.Exists(drafts))Directory.Move(drafts,Path.Combine(Resources,"Backups","drafts-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")));
                Save(imported);Render(imported);
            });Refresh(imported);
        }
    }
}
