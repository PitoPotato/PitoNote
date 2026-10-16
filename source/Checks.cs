using System;
using System.IO;
using System.Linq;
using System.Text;
namespace PitoNote {
 public static class Checks {
  public static int CreateProbe(string root){
   Engine.Root=root;if(File.Exists(Engine.DataPath))throw new IOException("Probe data already exists; refusing to overwrite.");
   var store=new Store();store.Settings.FontName=store.Settings.TitleFontName="Segoe UI";store.Settings.CardHeight=280;store.Settings.Position="Normal";
   var note=Engine.NewNote(store.Settings);note.Title="Kiểm tra nhập native";note.Body="Tiếng Việt: ă â ê ô ơ ư đ\n#Width# [!Quit] \" ' | ; = 100% 😀\n[$Input] #CRLF# [Body]";store.Notes.Add(note);Engine.Save(store,false);Engine.Render(store);return 0;
  }
  static int count;static StringBuilder report;
  static void Assert(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);count++;report.AppendLine("PASS: "+name);}
  static bool Reject(Action action){try{action();return false;}catch{return true;}}
  public static int Run(string root){
   Engine.Root=root;count=0;report=new StringBuilder();Directory.CreateDirectory(root);
   try{
    var store=new Store();var a=Engine.NewNote(store.Settings);a.Title="Tiếng Việt ă â ê ô ơ ư đ";a.Body="Dòng 1\r\n#Width# [!Quit] \" ' | ; = 100% 😀";store.Notes.Add(a);
    var b=Engine.NewNote(store.Settings);b.Title="Note B";store.Notes.Add(b);Engine.Save(store,false);Engine.Render(store);
    var loaded=Engine.Load();Assert(loaded.Version==3&&loaded.Notes[0].Body==a.Body,"Version 3 Unicode / literal content round-trip");
    Assert(!object.ReferenceEquals(a.Appearance,store.Settings)&&!object.ReferenceEquals(a.Appearance,b.Appearance),"Independent defaults and note snapshots");
    string oldJson=System.Text.RegularExpressions.Regex.Replace(Engine.Json(loaded),"\"(?:SmartSnap|SnapDistance|SnapGap)\":(?:true|false|[0-9]+),?","");
    oldJson=oldJson.Replace(",}","}");var oldStore=Engine.Parse(oldJson);
    Assert(oldStore.Settings.SmartSnap&&oldStore.Settings.SnapDistance==10&&oldStore.Settings.SnapGap==12,"Pre-snapping settings receive compatible defaults");
    Assert(oldStore.Notes.All(x=>x.Appearance.SmartSnap&&x.Appearance.SnapDistance==10&&x.Appearance.SnapGap==12)&&oldStore.Notes[0].Body==a.Body,"Old per-note snapshots migrate without altering content");
    string config=Path.Combine(root,"Notes",a.Id,"Note.ini"),ini=File.ReadAllText(config);
    Assert(Native.IniValue(config,"Title","Meter")=="TextEdit"&&Native.IniValue(config,"Body","Meter")=="TextEdit","Title and body are native TextEdit meters");
    Assert(!ini.Contains("inline")&&!ini.Contains("[Add]")&&!ini.Contains("[AllNotes]"),"No overlay editor or global controls on notes");
    Assert(!ini.Contains("[!Quit]")&&!ini.Contains("#Width#"),"User content never becomes skin command syntax");
    Assert(File.ReadAllText(Path.Combine(Engine.Resources,"Content",a.Id+".body.txt"))==a.Body,"Full Unicode content lives in a value file");
    Assert(Native.IniValue(config,"Rainmeter","AppVersion")=="5.0.0.5293","Native input declares the required Rainmeter version");
    Assert(!Native.IniValue(config,"Body","OnSubmitAction").Contains("$Input"),"User text is not interpolated into executable commands");
    Assert(File.ReadAllText(Path.Combine(Engine.Resources,"NativeEdit.lua")).Contains("SKIN:ReplaceVariables('[$Input]')"),"Lua reads current input as data");
    Assert(Native.IniValue(config,"AutoSave","ActionList2").Contains("1000"),"Timed native autosave");
    byte[] bytes=File.ReadAllBytes(config);Assert(bytes[0]==255&&bytes[1]==254,"Unicode skin encoding retained");
    Assert(Native.IniValue(config,"Settings","ToolTipText")=="Note settings","English tooltips survive Windows INI parsing");
    Assert(Native.IniValue(config,"SnapController","Plugin")=="PitoNoteSnap"&&Native.IniValue(config,"SnapController","SnapGap")=="12","Native snapping options reach the generated note");
    Assert(Native.IniValue(config,"Rainmeter","DefaultAnchorX")=="0"&&Native.IniValue(config,"Rainmeter","DefaultAnchorY")=="0"&&Native.IniValue(config,"Rainmeter","DefaultSnapEdges")=="0","New notes default to top-left anchors without competing built-in snapping");
    var defaults=Engine.Clone(store.Settings);Engine.ApplyTheme(defaults,"Dark");defaults.FontSize=22;defaults.Padding=35;defaults.SmartSnap=false;defaults.SnapDistance=20;defaults.SnapGap=24;Engine.SaveDefaults(defaults);
    loaded=Engine.Load();Assert(loaded.Settings.FontSize==22&&loaded.Notes[0].Appearance.FontSize==12,"Changing defaults never changes an existing note");
    Assert(File.ReadAllText(config)==ini,"Saving defaults does not rewrite old skins");
    var future=Engine.NewNote(loaded.Settings);Assert(future.Appearance.Theme=="Dark"&&future.Appearance.FontSize==22&&future.Appearance.Padding==35,"Future notes capture latest defaults");
    Assert(!future.Appearance.SmartSnap&&future.Appearance.SnapDistance==20&&future.Appearance.SnapGap==24&&loaded.Notes[0].Appearance.SmartSnap,"New snapping defaults affect only future notes");
    var changed=Engine.Clone(loaded.Notes[0].Appearance);Engine.ApplyTheme(changed,"Pink");changed.FontSize=18;changed.ReadOnly=true;changed.ShowTitle=false;changed.TextAlign="Right";changed.ToolbarMode="Hidden";
    changed.SnapDistance=6;changed.SnapGap=0;Engine.SaveAppearance(a.Id,changed,null,false);loaded=Engine.Load();
    Assert(loaded.Notes[0].Appearance.SnapDistance==6&&loaded.Notes[0].Appearance.SnapGap==0&&loaded.Notes[1].Appearance.SnapDistance==10&&loaded.Settings.SnapDistance==20,"Each note keeps independent snap distance and spacing");
    Assert(loaded.Notes[0].Appearance.ReadOnly&&loaded.Notes[0].Appearance.FontSize==18,"Per-note input and display settings persist");
    Assert(loaded.Notes[1].Appearance.FontSize==12&&loaded.Settings.FontSize==22,"Per-note edits leave other notes and defaults unchanged");
    Assert(Native.IniValue(config,"Body","ReadOnly")=="1"&&Native.IniValue(config,"Title","Hidden")=="1","Read-only and hidden title reach native meters");
    Assert(Native.IniValue(config,"Body","StringAlign")=="RightTop"&&Native.IniValue(config,"ResizeGrip","Shape2")=="","Alignment and hidden icons reach the skin");
    var baseline=Engine.Clone(loaded.Notes[0].Appearance);var proposed=Engine.Clone(baseline);proposed.Background="ABCDEF";
    loaded.Notes[0].Width=620;loaded.Notes[0].Height=410;loaded.Notes[0].Appearance.Width=620;loaded.Notes[0].Appearance.CardHeight=410;Engine.Save(loaded);
    Engine.SaveAppearance(a.Id,proposed,baseline,false);loaded=Engine.Load();
    Assert(loaded.Notes[0].Width==620&&loaded.Notes[0].Height==410&&loaded.Notes[0].Appearance.Background=="ABCDEF","Color edits preserve concurrent note resize");
    string draft="Việt hóa: ă â ê ô ơ ư đ\n#Width# [!Quit] \" ' | ; = 100% 😀";
    Engine.AtomicWrite(Path.Combine(Engine.Resources,"Drafts",a.Id+".body.txt"),draft);
    Assert(Engine.Load().Notes[0].Body==draft,"Pending native drafts recover before helper commit");
    Engine.CommitField(a.Id,"Body");var committed=Engine.Parse(File.ReadAllText(Engine.DataPath));
    Assert(committed.Notes[0].Body==draft,"Native commit preserves literal Unicode");
    Assert(committed.Notes[0].Width==620&&committed.Notes[0].Appearance.Background=="ABCDEF","Native commit preserves geometry and appearance");
    Assert(Directory.GetFiles(Path.Combine(Engine.Resources,"Backups")).Length>0,"Recoverable previous revision for every save");
    var legacy=new Store{Version=2,Settings=new Settings{CardHeight=164,FontName="Lexend"}};
    legacy.Notes.Add(new Note{Title="Cũ",Body="Không đổi nội dung",Theme="Pink",FontName="Agency FB",FontSize=17});
    var migrated=Engine.Parse(Engine.Json(legacy));
    Assert(migrated.Version==3&&migrated.Notes[0].Appearance.FontName=="Agency FB"&&migrated.Notes[0].Appearance.TitleFontName=="Agency FB","Legacy private fonts become independent snapshots");
    Assert(migrated.Notes[0].Appearance.FontSize==17&&migrated.Notes[0].Appearance.Background==Engine.Themes["Pink"].Bg&&migrated.Notes[0].Height==220,"Legacy color, size and dimensions preserved");
    Assert(migrated.Notes[0].Body==legacy.Notes[0].Body,"Migration leaves text unchanged");
    foreach(var key in Engine.Themes.Keys){var theme=new Settings();Engine.ApplyTheme(theme,key);Engine.ValidateOptions(theme);Assert(Engine.Contrast(theme.Background,theme.TextColor)>=4.5,"Readable theme "+key);}
    var bad=Engine.Clone(store.Settings);bad.FontSize=double.NaN;Assert(Reject(()=>Engine.ValidateOptions(bad)),"NaN rejected");
    bad=Engine.Clone(store.Settings);bad.FontName="x\n[!Quit]";Assert(Reject(()=>Engine.ValidateOptions(bad)),"Font injection rejected");
    bad=Engine.Clone(store.Settings);bad.BorderColor="oops";Assert(Reject(()=>Engine.ValidateOptions(bad)),"Malformed color rejected");
    bad=Engine.Clone(store.Settings);bad.TextAlign="Unknown";Assert(Reject(()=>Engine.ValidateOptions(bad)),"Unknown alignment rejected");
    bad=Engine.Clone(store.Settings);bad.SnapDistance=0;Assert(Reject(()=>Engine.ValidateOptions(bad)),"Invalid snap distance rejected");
    bad=Engine.Clone(store.Settings);bad.SnapGap=81;Assert(Reject(()=>Engine.ValidateOptions(bad)),"Invalid note spacing rejected");
    var invalid=Engine.Parse(Engine.Json(store));invalid.Notes[0].Id="../outside";Assert(Reject(()=>Engine.Validate(invalid)),"Note path traversal rejected");
    Assert(Reject(()=>Engine.CommitField(a.Id,"../outside")),"Content field traversal rejected");
    Assert(Engine.FontNames().Contains("Segoe UI"),"Installed Windows fonts are available");
    report.AppendLine(count+" checks passed.");File.WriteAllText(Path.Combine(root,"checks.txt"),report.ToString());return 0;
   }catch(Exception ex){report.AppendLine(ex.ToString());File.WriteAllText(Path.Combine(root,"checks.txt"),report.ToString());return 1;}
  }
 }
}
