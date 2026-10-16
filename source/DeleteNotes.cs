using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows;

namespace PitoNote {
    public static partial class Engine {
        // Remove from the actual store and skin tree, not a Pinned/Archived flag.
        // File moves are reversible, and a full pre-delete snapshot is kept first.
        public static string DeleteNote(string id,bool deactivate=true) {
            ContentPath(id,"Body","Content"); // Validate the ID before deriving any paths or bangs.
            if(deactivate&&Native.SkinHandle(Config+"\\Notes\\"+id)!=IntPtr.Zero)Bang("!DeactivateConfig \""+Config+"\\Notes\\"+id+"\"");
            string archive=null;
            Native.WithDataLock(()=>{
                var latest=Load();var note=latest.Notes.FirstOrDefault(n=>n.Id==id);if(note==null)return;
                string notesRoot=Path.GetFullPath(Path.Combine(Root,"Notes"))+Path.DirectorySeparatorChar;
                string noteFolder=Path.GetFullPath(Path.Combine(Root,"Notes",id));
                if(!noteFolder.StartsWith(notesRoot,StringComparison.OrdinalIgnoreCase))throw new IOException("Unsafe note path.");
                archive=Path.Combine(Resources,"Backups","Deleted",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+id+"-"+Guid.NewGuid().ToString("N").Substring(0,8));
                Directory.CreateDirectory(archive);AtomicWrite(Path.Combine(archive,"data-before-delete.json"),Json(latest));
                using(var stream=new MemoryStream()){new DataContractJsonSerializer(typeof(Note)).WriteObject(stream,note);AtomicWrite(Path.Combine(archive,"deleted-note.json"),Encoding.UTF8.GetString(stream.ToArray()));}
                AtomicWrite(Path.Combine(archive,"README.txt"),"PitoNote: deleted notes are kept here for recovery.\r\nThese files are not part of the active note list.\r\ndata-before-delete.json is a snapshot of ALL notes before deletion.\r\nImporting this file restores the entire snapshot, not just the deleted note. Export your current data before restoring.\r\ndeleted-note.json contains one note only; it is not a full data-store import file.\r\n");
                var moved=new List<Tuple<string,string,bool>>();
                Action<string,string,bool> move=(from,to,directory)=>{if(directory?!Directory.Exists(from):!File.Exists(from))return;if(directory)Directory.Move(from,to);else File.Move(from,to);moved.Add(Tuple.Create(from,to,directory));};
                try {
                    move(noteFolder,Path.Combine(archive,"Skin"),true);
                    foreach(string folder in new[]{"Content","Drafts"})foreach(string field in new[]{"Title","Body"}) {
                        string destination=Path.Combine(archive,folder,id+"."+field.ToLowerInvariant()+".txt");Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        move(ContentPath(id,field,folder),destination,false);
                    }
                    latest.Notes.Remove(note);Save(latest);
                } catch {
                    for(int i=moved.Count-1;i>=0;i--){var entry=moved[i];if(entry.Item3)Directory.Move(entry.Item2,entry.Item1);else File.Move(entry.Item2,entry.Item1);}
                    throw;
                }
            });
            if(archive!=null)NotifyCatalog();return archive;
        }
    }
    public static class DeleteNoteUI {
        public static bool ConfirmAndDelete(string id,Window owner=null) {
            var note=Engine.Load().Notes.FirstOrDefault(n=>n.Id==id);if(note==null)return false;
            string title=string.IsNullOrWhiteSpace(note.Title)?"Untitled note":note.Title;
            string message="Delete “"+title+"” from PitoNote?\n\nThis removes the note from PitoNote, rather than just hiding it on the desktop. A pre-deletion backup will be kept locally.";
            var answer=owner==null?MessageBox.Show(message,"Delete note — PitoNote",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No):MessageBox.Show(owner,message,"Delete note — PitoNote",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No);
            if(answer!=MessageBoxResult.Yes)return false;
            string archive=Engine.DeleteNote(id);if(archive==null)return false;
            if(!(owner is MainWindow)||!((MainWindow)owner).EditsNote(id))Program.NotifyDeleted(id);
            return true;
        }
    }
}
