using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PitoNote {
    public static class ManagementChecks {
        static IEnumerable<T> Children<T>(DependencyObject parent) where T:DependencyObject {
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++) {
                var child=VisualTreeHelper.GetChild(parent,i);if(child is T)yield return (T)child;
                foreach(var nested in Children<T>(child))yield return nested;
            }
        }
        static void Layout(FrameworkElement element,int width,int height) {element.Measure(new Size(width,height));element.Arrange(new System.Windows.Rect(0,0,width,height));element.UpdateLayout();}
        internal static void Render(FrameworkElement element,int width,int height,string path) {
            Layout(element,width,height);var image=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);image.Render(element);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var stream=File.Create(path))encoder.Save(stream);
        }
        public static int Run(string root) {
            var report=new StringBuilder();int count=0;Directory.CreateDirectory(root);
            Action<bool,string> verify=(ok,name)=>{if(!ok)throw new Exception("FAIL: "+name);count++;report.AppendLine("PASS: "+name);};
            try {
                Engine.Root=root;if(File.Exists(Engine.DataPath))throw new IOException("Use a new private test directory.");
                var store=new Store();
                var visible=Engine.NewNote(store.Settings);visible.Title="Góc Trí tuệ";visible.Body="Một ý tưởng nhỏ cho ngày mới.";
                var hidden=Engine.NewNote(store.Settings);hidden.Title="Ý tưởng để dành";hidden.Body="Tiếng Việt: ă â ê ô ơ ư đ 😀";hidden.Pinned=false;Engine.ApplyTheme(hidden.Appearance,"Pink");
                var archived=Engine.NewNote(store.Settings);archived.Title="Dự án đã xong";archived.Body="Lưu lại để tham khảo khi cần.";archived.Archived=true;archived.Pinned=false;Engine.ApplyTheme(archived.Appearance,"Dark");
                var done=Engine.NewNote(store.Settings);done.Title="Công việc hoàn thành";done.Body="Vẫn giữ trạng thái hoàn thành khi mở lại.";done.Done=true;done.Appearance.HideDone=true;Engine.ApplyTheme(done.Appearance,"Blue");
                store.Notes.AddRange(new[]{hidden,archived,visible,done});Engine.Save(store,false);Engine.Render(store);
                string defaults=Engine.Json(new Store{Settings=store.Settings});
                var entries=NoteCatalog.Build(store);
                verify(entries.Select(n=>n.Id).SequenceEqual(new[]{visible.Id,hidden.Id,done.Id,archived.Id}),"Visible, hidden, archived groups; stable ordering within each group");
                verify(entries.Count(n=>n.Group==0)==1&&entries.Count(n=>n.Group==1)==2&&entries.Count(n=>n.Group==2)==1,"Completed auto-hidden notes are discoverable");
                verify(NoteCatalog.Build(store,n=>false).All(n=>n.Group!=0),"Actual unloaded skins are not labelled visible");
                verify(NoteCatalog.Filter(entries,"TRÍ TUỆ").Single().Id==visible.Id,"Vietnamese case-insensitive title search");
                verify(NoteCatalog.Filter(entries,"ă â ê").Single().Id==hidden.Id,"Search includes the full note body");
                verify(NoteCatalog.Filter(entries,"",2).Single().Id==archived.Id,"Archive filter is distinct");
                verify(NoteCatalog.Filter(entries,"không tồn tại").Count==0,"Empty search result is explicit");
                var surrogate=new NoteEntry{Note=new Note{Body=new string('a',109)+"😀abc"}};verify(!char.IsHighSurrogate(surrogate.Preview[surrogate.Preview.Length-2]),"Preview never cuts a Unicode surrogate pair");
                Engine.AtomicWrite(Path.Combine(Engine.Resources,"Drafts",hidden.Id+".body.txt"),"Bản nháp chưa hợp nhất #Width# [!Quit] 😀");
                Engine.ManageNote(hidden.Id,"show",false);var loaded=Engine.Load();var reopened=loaded.Notes.First(n=>n.Id==hidden.Id);
                verify(reopened.Pinned&&!reopened.Archived&&reopened.Body.Contains("[!Quit]"),"Opening hidden notes merges pending drafts as literal data");
                verify(Engine.Json(new Store{Settings=loaded.Settings})==defaults&&Engine.Json(new Store{Notes=new List<Note>{loaded.Notes.First(n=>n.Id==visible.Id)}})==Engine.Json(new Store{Notes=new List<Note>{visible}}),"Management leaves defaults and other notes untouched");
                Engine.ManageNote(hidden.Id,"hide",false);verify(!Engine.Load().Notes.First(n=>n.Id==hidden.Id).Pinned,"Hide is idempotent, not a visibility toggle");
                Engine.ManageNote(hidden.Id,"hide",false);verify(!Engine.Load().Notes.First(n=>n.Id==hidden.Id).Pinned,"Repeated hide never accidentally reopens a note");
                Engine.ManageNote(archived.Id,"restore",false);verify(Engine.Load().Notes.First(n=>n.Id==archived.Id).Pinned&&!Engine.Load().Notes.First(n=>n.Id==archived.Id).Archived,"Restore removes archive and shows the note");
                Engine.ManageNote(done.Id,"show",false);var reopenedDone=Engine.Load().Notes.First(n=>n.Id==done.Id);verify(reopenedDone.Done&&!reopenedDone.Appearance.HideDone,"Reopen completed note without silently undoing completion");
                Engine.ManageNote(hidden.Id,"archive",false);verify(Engine.Load().Notes.First(n=>n.Id==hidden.Id).Archived&&!Engine.Load().Notes.First(n=>n.Id==hidden.Id).Pinned,"Archive retains data and removes the desktop skin");
                bool rejected=false;try{Engine.ManageNote("../outside","show",false);}catch(InvalidDataException){rejected=true;}verify(rejected,"Management validates IDs before file operations");
                rejected=false;try{Engine.ManageNote(visible.Id,"unknown",false);}catch(InvalidDataException){rejected=true;}verify(rejected,"Unknown management actions cannot alter data");
                rejected=false;try{Engine.ManageNote(Guid.NewGuid().ToString("N"),"show",false);}catch(InvalidDataException){rejected=true;}verify(rejected,"Missing notes are never recreated by stale actions");
                var locked=Engine.Load();locked.Notes.First(n=>n.Id==hidden.Id).Appearance.ReadOnly=true;Engine.Save(locked);rejected=false;try{Engine.EditOnDesktop(hidden.Id);}catch(InvalidDataException){rejected=true;}verify(rejected,"Edit navigation respects read-only notes");
                Engine.Save(store);Engine.Render(store);string viewBaseline=Engine.Json(Engine.Load());
                string control=Path.Combine(root,"Control","Control.ini");verify(Native.IniValue(control,"NotesList","LeftMouseUpAction").Contains("ToggleCatalog()")&&!Native.IniValue(control,"NotesList","LeftMouseUpAction").Contains("PitoNote.exe"),"List icon toggles Rainmeter directly without a helper");
                verify(Native.IniValue(control,"Rainmeter","ContextTitle3")=="Manage notes","Toolbar context menu also opens management");
                string native=Path.Combine(root,"Catalog","Catalog.ini");
                verify(Native.IniValue(native,"Rainmeter","DefaultStartHidden")=="1","Native catalog preloads hidden");
                verify(Native.IniValue(native,"Rainmeter","Update")=="1000 | Invisible 0","Hidden native catalog has no regular update work");
                var app=new Application();var catalogPreview=new NoteListView(true,false);catalogPreview.SetSnapshot(store);catalogPreview.Expand(hidden.Id);
                var content=(FrameworkElement)catalogPreview;Layout(content,376,550);
                var names=Children<Button>(content).Select(AutomationProperties.GetName).ToList();
                verify(names.Any(n=>n.StartsWith("Show on desktop"))&&names.Any(n=>n.StartsWith("Edit on desktop"))&&names.Any(n=>n.StartsWith("Note settings"))&&names.Any(n=>n.StartsWith("Delete note")),"Expanded dropdown row has reopen, direct-edit, settings and delete actions");
                verify(Children<TextBox>(content).Count()==1,"Dropdown contains search only; no overlay note editor");
                verify(Native.IniValue(native,"Search","Meter")=="TextEdit","Catalog search is a native Rainmeter input meter");
                catalogPreview.SetFilter("Ý tưởng",1);verify(catalogPreview.Filtered.Count==1&&catalogPreview.Filtered[0].Id==hidden.Id,"View combines query and state filter");
                catalogPreview.SetFilter("",-1);catalogPreview.Expand(hidden.Id);verify(catalogPreview.ExpandedId==null,"Clicking the same row collapses its actions");
                var manager=new NoteListView(false,false);manager.SetSnapshot(store);manager.Expand(archived.Id);Layout(manager,800,460);
                names=Children<Button>(manager).Select(AutomationProperties.GetName).ToList();verify(!manager.OffersContentEditing&&!names.Any(n=>n.StartsWith("Edit on desktop"))&&names.Any(n=>n.StartsWith("Note settings")),"General manager offers navigation to per-note settings, never inline editing");
                verify(Children<TextBox>(manager).Count()==1,"General manager has no title/body editor fields");
                Render(manager,800,460,Path.Combine(root,"manager-preview.png"));
                var many=new Store();for(int i=0;i<120;i++){var n=Engine.NewNote(many.Settings);n.Title="Notes "+i;many.Notes.Add(n);}manager.SetSnapshot(many);Layout(manager,800,460);
                verify(manager.EntryCount==120&&Children<Button>(manager).Any(b=>AutomationProperties.GetName(b).StartsWith("Show more")),"Long lists retain all records and expose load-more instead of truncation");
                manager.SetFilter("Notes 119",-1);verify(manager.Filtered.Single().Id==many.Notes[119].Id,"Search reaches notes beyond the first page");
                verify(Engine.Json(Engine.Load())==viewBaseline,"Opening and filtering views never changes saved notes");
                report.AppendLine(count+" management checks passed.");File.WriteAllText(Path.Combine(root,"management-checks.txt"),report.ToString());app.Shutdown();return 0;
            }catch(Exception ex){report.AppendLine(ex.ToString());File.WriteAllText(Path.Combine(root,"management-checks.txt"),report.ToString());return 1;}
        }
    }
}
