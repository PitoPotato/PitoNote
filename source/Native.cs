using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace PitoNote {
    // Production resize controller: observes the user's drag, never injects input.
    public static partial class Native {
        [StructLayout(LayoutKind.Sequential)] struct CopyData {public IntPtr Kind;public int Size;public IntPtr Data;}
        [StructLayout(LayoutKind.Sequential)] public struct Rect {public int Left,Top,Right,Bottom;public int Width{get{return Right-Left;}} public int Height{get{return Bottom-Top;}}}
        [StructLayout(LayoutKind.Sequential)] struct Point {public int X,Y;}
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string cls,string name);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string name);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint processId);
        internal static int TestProcessId;
        static IntPtr RainWindow(string cls,string title){
            if(TestProcessId==0)return FindWindow(cls,title);
            IntPtr current=IntPtr.Zero;while((current=FindWindowEx(IntPtr.Zero,current,cls,title))!=IntPtr.Zero){uint pid;GetWindowThreadProcessId(current,out pid);if(pid==TestProcessId)return current;}return IntPtr.Zero;
        }
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SendMessageTimeout(IntPtr hwnd,uint msg,IntPtr wParam,ref CopyData data,uint flags,uint timeout,out IntPtr result);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out Rect rect);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out Point point);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr ctx);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int w,int h,uint flags);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern uint GetPrivateProfileString(string section,string key,string fallback,StringBuilder result,uint size,string path);
        public static string IniValue(string path,string section,string key){var b=new StringBuilder(4096);GetPrivateProfileString(section,key,"",b,4096,path);return b.ToString();}
        static IntPtr Send(IntPtr hwnd,int kind,string text) {
            if(hwnd==IntPtr.Zero)return IntPtr.Zero;
            IntPtr ptr=Marshal.StringToHGlobalUni(text);
            try {var data=new CopyData{Kind=new IntPtr(kind),Size=(text.Length+1)*2,Data=ptr};IntPtr result;return SendMessageTimeout(hwnd,0x004A,IntPtr.Zero,ref data,2,700,out result)==IntPtr.Zero?IntPtr.Zero:result;}
            finally{Marshal.FreeHGlobal(ptr);}
        }
        public static void Command(string command){Send(RainWindow("DummyRainWClass","Rainmeter control window"),1,command);}
        public static IntPtr SkinHandle(string config){return Send(RainWindow("RainmeterTrayClass",null),5101,config);}
        internal static bool Visible(string config){return IsWindowVisible(SkinHandle(config));}
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
        public static void FocusSkin(string config){var hwnd=SkinHandle(config);if(hwnd!=IntPtr.Zero)SetForegroundWindow(hwnd);}
        public static Rect Bounds(string config){Rect r;if(!GetWindowRect(SkinHandle(config),out r))throw new IOException("Could not find the open note: "+config);return r;}
        public static Rect PhysicalBounds(string config) {
            IntPtr old=IntPtr.Zero;try{old=SetThreadDpiAwarenessContext(new IntPtr(-4));return Bounds(config);}finally{if(old!=IntPtr.Zero)SetThreadDpiAwarenessContext(old);}
        }
        public static void PositionEditor(IntPtr hwnd,int x,int y,int width,int height) {SetWindowPos(hwnd,IntPtr.Zero,x,y,width,height,0x0014);}
        public static IntPtr WaitForNote(string id) {
            for(int i=0;i<40;i++){var hwnd=SkinHandle(Engine.Config+"\\Notes\\"+id);if(hwnd!=IntPtr.Zero)return hwnd;Thread.Sleep(25);}
            return IntPtr.Zero;
        }
        public static void WithDataLock(Action action) {
            string key=Convert.ToBase64String(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(Engine.Root.ToLowerInvariant()))).Replace("/","").Replace("+","").Substring(0,20);
            using(var mutex=new Mutex(false,"Local\\PitoNote-data-"+key)) {
                bool taken=false;try {try{taken=mutex.WaitOne(5000);}catch(AbandonedMutexException){taken=true;}if(!taken)throw new IOException("Data is being saved. Please try again.");action();}finally{if(taken)mutex.ReleaseMutex();}
            }
        }
        public static void Reflow(string config,int w,int h) {
            string suffix=" \""+config+"\"]";
            Command("[!SetVariable NoteW "+w+suffix+"[!SetVariable NoteH "+h+suffix+"[!UpdateMeter *"+suffix+"[!Redraw"+suffix);
        }
        public static void CommitSize(string id,int w,int h) {
            WithDataLock(()=> {var latest=Engine.Load();var n=latest.Notes.FirstOrDefault(x=>x.Id==id);if(n==null)return;n.Width=w;n.Height=h;n.Appearance.Width=w;n.Appearance.CardHeight=h;Engine.Save(latest);Engine.Render(latest);});
            Command("!Refresh \""+Engine.Config+"\\Notes\\"+id+"\"");
        }
        public static void Resize(string id,double offsetX,double offsetY) {
            var s=Engine.Load();var n=s.Notes.FirstOrDefault(x=>x.Id==id);if(n==null||!n.Pinned||n.Archived||n.Collapsed)return;var appearance=Engine.Effective(s.Settings,n);
            IntPtr oldDpi=IntPtr.Zero;try{oldDpi=SetThreadDpiAwarenessContext(new IntPtr(-4));}catch(EntryPointNotFoundException){}
            string config=Engine.Config+"\\Notes\\"+id;
            Rect r=Bounds(config);int initialW=Engine.NoteWidth(s.Settings,n),initialH=Engine.NoteHeight(s.Settings,n);
            double sx=r.Width/(double)(initialW+16),sy=r.Height/(double)(initialH+16);
            double anchorX=r.Left+(initialW-16+Math.Max(0,Math.Min(24,offsetX)))*sx;
            double anchorY=r.Top+(initialH-16+Math.Max(0,Math.Min(24,offsetY)))*sy;
            int width=initialW,height=initialH;bool changed=false;
            Command("!Draggable 0 \""+config+"\"");
            try {
                var end=DateTime.UtcNow.AddMinutes(2);
                while((GetAsyncKeyState(1)&0x8000)!=0 && DateTime.UtcNow<end) {
                    Point cursor;if(!GetCursorPos(out cursor))break;
                    int nextW=Math.Max(220,Math.Min(1600,initialW+(int)Math.Round((cursor.X-anchorX)/sx)));
                    int nextH=Math.Max(Engine.MinNoteHeight(appearance),Math.Min(1400,initialH+(int)Math.Round((cursor.Y-anchorY)/sy)));
                    if(nextW!=width||nextH!=height){width=nextW;height=nextH;changed=true;Reflow(config,width,height);}
                    Thread.Sleep(16);
                }
                if(changed)CommitSize(id,width,height);
            } finally {
                Command("!Draggable "+(appearance.Draggable?1:0)+" \""+config+"\"");
                if(oldDpi!=IntPtr.Zero)SetThreadDpiAwarenessContext(oldDpi);
            }
        }
        public static int VerifyLayout(string path) {
            var s=Engine.Load();var n=s.Notes.FirstOrDefault(x=>x.Pinned&&!x.Archived);if(n==null)throw new IOException("No visible note to verify.");
            string config=Engine.Config+"\\Notes\\"+n.Id;
            int w=Engine.NoteWidth(s.Settings,n),h=Engine.NoteHeight(s.Settings,n);
            Rect before=Bounds(config);
            try {
                Reflow(config,w+80,h+60);Thread.Sleep(250);Rect resized=Bounds(config);
                Reflow(config,w,h);Thread.Sleep(150);Rect restored=Bounds(config);
                bool ok=resized.Width>before.Width&&resized.Height>before.Height&&restored.Width==before.Width&&restored.Height==before.Height;
                File.WriteAllText(path,"Before="+before.Width+"x"+before.Height+"\r\nResized="+resized.Width+"x"+resized.Height+"\r\nRestored="+restored.Width+"x"+restored.Height+"\r\nPASS="+ok);
                return ok?0:1;
            }finally{Reflow(config,w,h);}
        }
    }
}
