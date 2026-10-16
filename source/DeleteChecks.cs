using System;
using System.IO;
using System.Linq;
using System.Text;
namespace PitoNote {
    public static class DeleteChecks {
        public static int Run(string root) {
            var report=new StringBuilder();int count=0;Directory.CreateDirectory(root);
            Action<bool,string> verify=(ok,name)=>{if(!ok)throw new Exception("FAIL: "+name);count++;report.AppendLine("PASS: "+name);};
            try {
                Engine.Root=root;if(File.Exists(Engine.DataPath))throw new IOException("Use a new private test directory.");
                var store=new Store();var a=Engine.NewNote(store.Settings);a.Title="Xóa riêng — Tiếng Việt";a.Body="Body ă â ê ô ơ ư đ 😀";var b=Engine.NewNote(store.Settings);b.Title="Giữ lại";store.Notes.Add(a);store.Notes.Add(b);Engine.Save(store,false);Engine.Render(store);
                string beforeB=Engine.Json(new Store{Settings=store.Settings,Notes=new System.Collections.Generic.List<Note>{b}});
                string config=File.ReadAllText(Path.Combine(root,"Notes",a.Id,"Note.ini"));
                verify(config.Contains("ContextTitle5=Delete note…")&&config.Contains("\"delete\""),"Deletion is exposed in the note context menu");
                verify(config.Contains("\"hide\"")&&config.Contains("Hide note, keep content"),"Hide remains separate from delete");
                verify(config.Contains("PitoNote.exe")&&!config.Contains("NotesStudio")&&!config.Contains("NotesPlus"),"Native actions use only the PitoNote helper");
                Engine.AtomicWrite(Path.Combine(Engine.Resources,"Drafts",a.Id+".body.txt"),"Bản nháp mới nhất");
                string archive=Engine.DeleteNote(a.Id,false);var loaded=Engine.Load();
                verify(loaded.Notes.Count==1&&loaded.Notes[0].Id==b.Id,"Deletion removes the note instead of hiding it");
                verify(Engine.Json(new Store{Settings=loaded.Settings,Notes=loaded.Notes})==beforeB,"Other notes and defaults are untouched");
                verify(!Directory.Exists(Path.Combine(root,"Notes",a.Id)),"Deleted note skin directory leaves the active tree");
                verify(!File.Exists(Path.Combine(Engine.Resources,"Content",a.Id+".body.txt"))&&!File.Exists(Path.Combine(Engine.Resources,"Content",a.Id+".title.txt")),"Deleted content files are removed from the active tree");
                verify(!File.Exists(Path.Combine(Engine.Resources,"Drafts",a.Id+".body.txt")),"Deleted draft cannot resurrect the note");
                var saved=Engine.Parse(File.ReadAllText(Path.Combine(archive,"data-before-delete.json")));
                verify(saved.Notes.Count==2&&saved.Notes.First(n=>n.Id==a.Id).Body=="Bản nháp mới nhất","Recovery snapshot includes the latest pending draft");
                verify(File.Exists(Path.Combine(archive,"Skin","Note.ini"))&&File.Exists(Path.Combine(archive,"Drafts",a.Id+".body.txt")),"Original skin and draft are recoverable");
                verify(Engine.DeleteNote(a.Id,false)==null,"Repeated deletion is an idempotent no-op");
                Engine.AtomicWrite(Path.Combine(Engine.Resources,"Drafts",a.Id+".body.txt"),"Late autosave");Engine.CommitField(a.Id,"Body");
                verify(Engine.Load().Notes.Count==1,"Late autosave never recreates a deleted record");
                bool rejected=false;try{Engine.DeleteNote("../outside",false);}catch(InvalidDataException){rejected=true;}verify(rejected,"Path traversal is rejected before file operations");
                rejected=false;try{Engine.SaveAppearance(a.Id,Engine.Clone(store.Settings),null,false);}catch(InvalidDataException){rejected=true;}verify(rejected,"Stale settings cannot recreate a deleted note");
                Engine.Render(Engine.Load());verify(!Directory.Exists(Path.Combine(root,"Notes",a.Id)),"Refresh does not regenerate deleted skins");
                report.AppendLine(count+" deletion checks passed.");File.WriteAllText(Path.Combine(root,"delete-checks.txt"),report.ToString());return 0;
            }catch(Exception ex){report.AppendLine(ex.ToString());File.WriteAllText(Path.Combine(root,"delete-checks.txt"),report.ToString());return 1;}
        }
    }
}
