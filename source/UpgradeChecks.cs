using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PitoNote {
    public static class UpgradeChecks {
        public static int Run(string root){
            var report=new StringBuilder();int count=0;Directory.CreateDirectory(root);
            Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception("FAIL: "+name);count++;report.AppendLine("PASS: "+name);};
            try{
                Engine.Root=root;if(File.Exists(Engine.DataPath))throw new IOException("Use a new private test directory.");
                var store=new Store();var a=Engine.NewNote(store.Settings);a.Title="Học tập";a.Body="Tiếng Việt ă â ê ô ơ ư đ\n#Width# [!Quit] 😀";a.Width=a.Appearance.Width=580;a.Height=a.Appearance.CardHeight=340;a.Appearance.ShowTitle=false;
                var b=Engine.NewNote(store.Settings);b.Title="Giữ nguyên";b.Body="Note B";store.Notes.AddRange(new[]{a,b});Engine.Save(store,false);Engine.Render(store);
                string defaults=Engine.Json(new Store{Settings=store.Settings}),beforeB=Engine.Json(new Store{Notes=new System.Collections.Generic.List<Note>{b}});
                string ini=Path.Combine(root,"Notes",a.Id,"Note.ini");
                Engine.AtomicWrite(Path.Combine(Engine.Resources,"Drafts",a.Id+".body.txt"),"Bản nháp mới nhất 😀");Engine.SetCollapsed(a.Id,true,false);
                var loaded=Engine.Load();var compact=loaded.Notes[0];
                check(compact.Collapsed&&compact.Width==580&&compact.Height==340&&compact.Appearance.CardHeight==340,"Collapse preserves full dimensions and appearance");
                check(compact.Body=="Bản nháp mới nhất 😀","Collapse captures pending native drafts");
                check(Engine.NoteHeight(loaded.Settings,compact)<compact.Height&&Engine.NoteHeight(loaded.Settings,compact)>=52,"Compact display height is independent of saved full height");
                check(Native.IniValue(ini,"Body","Meter")==""&&Native.IniValue(ini,"ResizeGrip","Meter")=="","Collapsed note renders no hidden input work surface or resize target");
                check(Native.IniValue(ini,"Title","Hidden")=="0"&&Native.IniValue(ini,"Title","ReadOnly")=="1","Collapsed title is readable even when full title is disabled");
                check(Native.IniValue(ini,"Expand","LeftMouseUpAction").Contains("\"collapse\"")&&Native.IniValue(ini,"Rainmeter","ContextAction2").Contains("desktop-edit"),"Compact note can expand or navigate to direct editing");
                string unchanged=File.ReadAllText(Engine.DataPath);Engine.SetCollapsed(a.Id,true,false);check(File.ReadAllText(Engine.DataPath)==unchanged,"Explicit repeated collapse is a no-op");
                Engine.SetCollapsed(a.Id,false,false);loaded=Engine.Load();check(!loaded.Notes[0].Collapsed&&Engine.NoteHeight(loaded.Settings,loaded.Notes[0])==340,"Expand restores the exact full height");
                check(Native.IniValue(ini,"Body","Meter")=="TextEdit"&&Native.IniValue(ini,"Title","Hidden")=="1","Expand restores native input and original title preference");
                loaded.Notes[0].Done=true;loaded.Notes[0].Pinned=false;loaded.Notes[0].Archived=true;loaded.Notes[0].Collapsed=true;Engine.Save(loaded);
                string source=Engine.Json(new Store{Notes=new System.Collections.Generic.List<Note>{loaded.Notes[0]}});
                var copy=Engine.DuplicateNote(a.Id,false);loaded=Engine.Load();
                check(copy.Id!=a.Id&&loaded.Notes.Count==3&&loaded.Notes[1].Id==copy.Id,"Duplicate gets a new ID immediately after its source");
                check(copy.Body=="Bản nháp mới nhất 😀"&&copy.Title=="Học tập (copy)","Duplicate copies latest text with an explicit title suffix");
                check(copy.Pinned&&!copy.Archived&&!copy.Done&&!copy.Collapsed,"Duplicate starts visible, expanded and not completed");
                check(copy.Width==580&&copy.Height==340&&Engine.Json(new Store{Settings=copy.Appearance})==Engine.Json(new Store{Settings=loaded.Notes[0].Appearance}),"Duplicate retains private appearance and dimensions");
                check(Engine.Json(new Store{Notes=new System.Collections.Generic.List<Note>{loaded.Notes[0]}})==source,"Duplication does not alter the source");
                check(Engine.Json(new Store{Settings=loaded.Settings})==defaults&&Engine.Json(new Store{Notes=new System.Collections.Generic.List<Note>{loaded.Notes[2]}})==beforeB,"Other notes and global defaults remain unchanged");
                var changed=Engine.Clone(copy.Appearance);Engine.ApplyTheme(changed,"Pink");Engine.SaveAppearance(copy.Id,changed,null,false);check(Engine.Load().Notes[0].Appearance.Theme=="Yellow","Duplicate appearance is independent from its source");
                loaded=Engine.Load();loaded.Notes[0].Title=new string('x',149)+"😀"+new string('y',9);Engine.Save(loaded);copy=Engine.DuplicateNote(a.Id,false);
                check(copy.Title.Length<=160&&!copy.Title.Any(c=>c=='\uFFFD'),"Long Unicode duplicate titles remain valid and bounded");
                bool rejected=false;try{Engine.SetCollapsed("../outside",true,false);}catch(InvalidDataException){rejected=true;}check(rejected,"Collapse rejects path traversal");
                rejected=false;try{Engine.DuplicateNote(Guid.NewGuid().ToString("N"),false);}catch(InvalidDataException){rejected=true;}check(rejected,"Stale duplication cannot recreate a deleted note");
                string control=File.ReadAllText(Path.Combine(root,"Control","Control.ini"));check(control.Contains("ToggleCatalog()")&&!control.Contains("Rainmeter.exe"),"Toolbar opens the catalog inside Rainmeter, without a helper command");
                check(File.ReadAllText(Path.Combine(Engine.Resources,"NativeEdit.lua")).Contains("value:gsub('\\r\\n', '\\n')"),"Autosave ignores native-only LF/CRLF representation changes");
                LogoAssets.Export(Path.Combine(Engine.Resources,"Brand"));check(File.Exists(Path.Combine(Engine.Resources,"Brand","PitoNote.svg")),"Original vector logo is exported");
                using(var stream=File.OpenRead(Path.Combine(Engine.Resources,"Brand","PitoNote.ico"))){var decoder=new IconBitmapDecoder(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);check(decoder.Frames.Count==7,"Windows icon contains seven native sizes");}
                var app=new Application();var view=new NoteListView(false,false);var firstLoad=view.DataLoads;view.Refresh();view.Refresh();check(view.DataLoads==firstLoad,"Unchanged list refresh does not reread JSON or all drafts");
                var snapshot=Engine.Load();view.SetSnapshot(snapshot);view.Expand(b.Id);var row=view.RowFor(b.Id);var other=view.RowFor(a.Id);
                var updated=Engine.Parse(Engine.Json(snapshot));updated.Notes.First(n=>n.Id==b.Id).Body="Dòng vừa sửa";view.SetSnapshot(updated);
                check(object.ReferenceEquals(row,view.RowFor(b.Id)),"Text changes update the existing card without replacing it");
                check(object.ReferenceEquals(other,view.RowFor(a.Id))&&view.ExpandedId==b.Id,"Unrelated rows and expanded state survive updates");
                view.SetFilter("Giữ nguyên",-1);view.SetFilter("",-1);check(object.ReferenceEquals(row,view.RowFor(b.Id)),"Filtering reuses note cards");
                var listPreview=new NoteListView(false,false);listPreview.SetSnapshot(updated);listPreview.Expand(b.Id);var content=(FrameworkElement)listPreview;content.Measure(new Size(800,550));content.Arrange(new Rect(0,0,800,550));content.UpdateLayout();
                var bitmap=new RenderTargetBitmap(376,550,96,96,PixelFormats.Pbgra32);bitmap.Render(content);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(root,"dropdown-3.2.png")))encoder.Save(stream);
                check(File.Exists(Path.Combine(root,"dropdown-3.2.png")),"General management view is rendered for visual review");
                report.AppendLine(count+" upgrade checks passed.");File.WriteAllText(Path.Combine(root,"upgrade-checks.txt"),report.ToString());app.Shutdown();return 0;
            }catch(Exception ex){report.AppendLine(ex.ToString());File.WriteAllText(Path.Combine(root,"upgrade-checks.txt"),report.ToString());return 1;}
        }
    }
}
