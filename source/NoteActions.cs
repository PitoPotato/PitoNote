using System;
using System.IO;
using System.Linq;

namespace PitoNote {
    public static partial class Engine {
        static void FlushNoteInput(string id) {
            ContentPath(id,"Body","Content");string config=Config+"\\Notes\\"+id;
            if(Native.SkinHandle(config)!=IntPtr.Zero)Bang("!CommandMeasure NoteScript DismissInput() \""+config+"\"");
        }
        public static void SetCollapsed(string id,bool? value=null,bool refresh=true) {
            ContentPath(id,"Body","Content");if(refresh)FlushNoteInput(id);
            Native.WithDataLock(()=>{
                var latest=Load();var note=latest.Notes.FirstOrDefault(n=>n.Id==id);if(note==null)throw new InvalidDataException("This note has been deleted.");
                bool next=value??!note.Collapsed;if(next==note.Collapsed)return;note.Collapsed=next;
                Save(latest);WriteContent(note);AtomicWrite(Path.Combine(Root,"Notes",id,"Note.ini"),BuildNote(latest.Settings,note));
            });if(refresh)RefreshNote(Load(),id);
        }
        public static Note DuplicateNote(string id,bool refresh=true) {
            ContentPath(id,"Body","Content");if(refresh)FlushNoteInput(id);Note copy=null;
            Native.WithDataLock(()=>{
                var latest=Load();var source=latest.Notes.FirstOrDefault(n=>n.Id==id);if(source==null)throw new InvalidDataException("This note has been deleted.");
                copy=NewNote(source.Appearance);copy.Width=source.Width;copy.Height=source.Height;
                copy.Appearance.Width=copy.Width;copy.Appearance.CardHeight=copy.Height;
                string title=string.IsNullOrWhiteSpace(source.Title)?"Notes":source.Title;const string suffix=" (copy)";int length=Math.Min(title.Length,160-suffix.Length);
                if(length>0&&char.IsHighSurrogate(title[length-1]))length--;copy.Title=title.Substring(0,length)+suffix;copy.Body=source.Body;
                latest.Notes.Insert(latest.Notes.IndexOf(source)+1,copy);Save(latest);WriteNativeAssets();WriteContent(copy);
                AtomicWrite(Path.Combine(Root,"Notes",copy.Id,"Note.ini"),BuildNote(latest.Settings,copy,latest.Notes.IndexOf(copy)%8));
            });if(refresh)RefreshNote(Load(),copy.Id);return copy;
        }
    }
}
