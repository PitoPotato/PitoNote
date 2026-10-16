using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PitoNote {
    public class NoteEntry {
        public Note Note;public int Group,Order;
        public string Id {get{return Note.Id;}}
        public string Title {get{return string.IsNullOrWhiteSpace(Note.Title)?"Untitled note":Note.Title;}}
        public string Preview {get{string s=(Note.Body??"").Replace("\r"," ").Replace("\n"," ").Trim();int length=s.Length>110&&char.IsHighSurrogate(s[109])?109:110;return s.Length>110?s.Substring(0,length)+"…":s;}}
        public string Status {get{return (Group==2?"Archived":Group==0?(Note.Done?"Visible · completed":"Visible"):Note.Done&&Note.Appearance.HideDone?"Hidden because completed":"Hidden")+(Note.Collapsed?" · collapsed":"");}}
    }
    public static class NoteCatalog {
        public static readonly string[] GroupNames={"Visible","Hidden","Archived"};
        public static List<NoteEntry> Build(Store store,Func<Note,bool> visible=null) {
            return store.Notes.Select((n,i)=>new NoteEntry{Note=n,Order=i,Group=n.Archived?2:(visible==null?n.Pinned&&(!n.Done||!n.Appearance.HideDone):visible(n))?0:1}).OrderBy(n=>n.Group).ThenBy(n=>n.Order).ToList();
        }
        public static List<NoteEntry> Filter(IEnumerable<NoteEntry> entries,string query,int group=-1) {
            query=(query??"").Trim();return entries.Where(e=>(group<0||e.Group==group)&&(query.Length==0||e.Title.IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0||e.Note.Body.IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0)).ToList();
        }
    }
    public static partial class Engine {
        public static void ManageNote(string id,string action,bool refresh=true) {
            ContentPath(id,"Body","Content");if(!new[]{"show","hide","archive","restore"}.Contains(action))throw new InvalidDataException("Invalid management action.");
            // Dismiss in the skin's own scope so the latest input is captured before changing state.
            if(refresh&&Native.SkinHandle(Config+"\\Notes\\"+id)!=IntPtr.Zero)Bang("!CommandMeasure NoteScript DismissInput() \""+Config+"\\Notes\\"+id+"\"");
            Native.WithDataLock(()=>{var latest=Load();var note=latest.Notes.FirstOrDefault(n=>n.Id==id);if(note==null)throw new InvalidDataException("This note has been deleted.");
                if(action=="show"||action=="restore"){note.Pinned=true;note.Archived=false;if(note.Done&&note.Appearance.HideDone)note.Appearance.HideDone=false;}
                else if(action=="hide")note.Pinned=false;
                else {note.Archived=true;note.Pinned=false;}
                Save(latest);WriteContent(note);AtomicWrite(Path.Combine(Root,"Notes",id,"Note.ini"),BuildNote(latest.Settings,note));
            });if(refresh)RefreshNote(Load(),id);
        }
        public static void EditOnDesktop(string id) {
            var note=Load().Notes.FirstOrDefault(n=>n.Id==id);if(note==null)throw new InvalidDataException("Note not found.");
            if(note.Appearance.ReadOnly)throw new InvalidDataException("Editing is locked for this note. Unlock it in Note settings.");
            if(note.Collapsed)SetCollapsed(id,false);
            if(Native.SkinHandle(Config+"\\Notes\\"+id)==IntPtr.Zero)ManageNote(id,"show");
            Native.WaitForNote(id);Native.FocusSkin(Config+"\\Notes\\"+id);
            Bang("!CommandMeasure NoteScript FocusBody() \""+Config+"\\Notes\\"+id+"\"");
        }
    }
    public static class NoteNavigation {
        public static void Launch(string verb,string id="") {
            if(id.Length>0&&!System.Text.RegularExpressions.Regex.IsMatch(id,"^[a-f0-9]{32}$"))throw new InvalidDataException("Invalid note ID.");
            if(!new[]{"details","desktop-edit","manage","list"}.Contains(verb))throw new InvalidDataException("Invalid navigation action.");
            Process.Start(new ProcessStartInfo{FileName=Path.Combine(Engine.Resources,"PitoNote.exe"),Arguments=verb+(id.Length==0?"":" "+id),UseShellExecute=false,CreateNoWindow=true});
        }
    }
}
