using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace PitoNote {
    public static class NativeSnapChecks {
        static bool Wait(Func<bool> ready){for(int i=0;i<100;i++){if(ready())return true;Thread.Sleep(30);}return false;}
        public static int Run(string root){
            var report=new StringBuilder();int count=0;Process rainmeter=null;Engine.Root=Path.GetFullPath(root);
            Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception("FAIL: "+name);count++;report.AppendLine("PASS: "+name);};
            try{
                if(Directory.Exists(root))throw new IOException("Use a new private test directory.");
                var store=new Store();var first=Engine.NewNote(store.Settings);first.Title="Alignment target";first.Width=first.Appearance.Width=400;first.Height=first.Appearance.CardHeight=250;first.Appearance.Position="Normal";
                var second=Engine.NewNote(store.Settings);second.Title="Moving note";second.Width=second.Appearance.Width=280;second.Height=second.Appearance.CardHeight=180;second.Appearance.Position="Normal";
                store.Notes.Add(first);store.Notes.Add(second);Engine.Save(store,false);Engine.Render(store);
                string folder=Directory.GetParent(root).FullName,configPath=Path.Combine(folder,"Rainmeter.ini"),pluginFolder=Path.Combine(folder,"Plugins");
                Directory.CreateDirectory(pluginFolder);File.Copy(Path.Combine(Engine.Resources,"Plugins","x64","PitoNoteSnap.dll"),Path.Combine(pluginFolder,"PitoNoteSnap.dll"));
                foreach(var note in store.Notes){string ini=Path.Combine(root,"Notes",note.Id,"Note.ini");Engine.AtomicWrite(ini,File.ReadAllText(ini).Replace("[SnapController]","[SnapController]\r\nAllowDiagnostics=1"));}
                string a=Engine.Config+"\\Notes\\"+first.Id,b=Engine.Config+"\\Notes\\"+second.Id;
                Engine.AtomicWrite(configPath,"[Rainmeter]\r\nSkinPath="+folder+"\\\r\nDisableVersionCheck=1\r\nLogging=1\r\n["+a+"]\r\nActive=1\r\nWindowX=400\r\nWindowY=300\r\nSnapEdges=1\r\n["+b+"]\r\nActive=1\r\nWindowX=1040\r\nWindowY=480\r\nSnapEdges=1\r\n");
                rainmeter=Process.Start(new ProcessStartInfo{FileName=Engine.Rainmeter,Arguments="\""+configPath+"\"",UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});Native.TestProcessId=rainmeter.Id;
                check(Wait(()=>Native.SkinHandle(a)!=IntPtr.Zero&&Native.SkinHandle(b)!=IntPtr.Zero),"Private native notes load without touching the user instance");
                // Test notes may be covered by the editor. Keep their geometry/render path active.
                Native.Command("!ForceEnableVisibleMode \""+a+"\"");Native.Command("!ForceEnableVisibleMode \""+b+"\"");
                foreach(var note in store.Notes){
                    string config=Engine.Config+"\\Notes\\"+note.Id,path=Path.Combine(Engine.Resources,note.Id+".snap-check.txt");
                    Native.Command("[!UpdateMeasure SnapController \""+config+"\"][!CommandMeasure NoteScript ProbeSnapState() \""+config+"\"]");
                    check(Wait(()=>{try{return File.Exists(path)&&File.ReadAllText(path).Trim()=="1";}catch(IOException){return false;}}),"Native subclass is attached for "+note.Title);
                }
                Thread.Sleep(120);var target=Native.PhysicalBounds(a);double zoom=target.Width/416.0;int inset=(int)Math.Round(8*zoom),gap=(int)Math.Round(12*zoom),tolerance=1;
                Action<string,string> command=(config,call)=>Native.Command("!CommandMeasure SnapController \""+call+"\" \""+config+"\"");
                Action<int,int> move=(x,y)=>{command(b,"ProbeMove "+x+" "+y);Thread.Sleep(70);};
                int expectedX=target.Right-2*inset+gap;
                command(b,"ProbeBegin");move(expectedX-5,target.Top+6);
                var actual=Native.PhysicalBounds(b);report.AppendLine("Side-by-side: expected "+expectedX+","+target.Top+"; actual "+actual.Left+","+actual.Top);
                check(Math.Abs(actual.Left-expectedX)<=tolerance&&Math.Abs(actual.Top-target.Top)<=tolerance,"Actual Rainmeter window snaps to paper gap and top edge");
                int expectedY=target.Bottom-2*inset+gap;
                command(b,"ProbeEnd");command(b,"ProbeBegin");move(target.Left+6,expectedY+5);actual=Native.PhysicalBounds(b);
                check(Math.Abs(actual.Left-target.Left)<=tolerance&&Math.Abs(actual.Top-expectedY)<=tolerance,"Stacked unequal-size native notes snap to the same left edge");
                command(b,"ProbeEnd");move(target.Left+6,expectedY+5);actual=Native.PhysicalBounds(b);
                check(actual.Left==target.Left+6&&actual.Top==expectedY+5,"Programmatic placement outside a drag is never magnetized");
                Native.Command("!Hide \""+a+"\"");command(b,"ProbeBegin");move(expectedX-5,target.Top+6);actual=Native.PhysicalBounds(b);
                check(actual.Left==expectedX-5&&actual.Top==target.Top+6,"Hidden notes are excluded from native snap targets");command(b,"ProbeEnd");Native.Command("!Show \""+a+"\"");
                Native.Command("!SetZoomFactor 1.25 \""+b+"\"");Thread.Sleep(250);
                var scaled=Native.PhysicalBounds(b);int movingInset=(int)Math.Round(scaled.Width/296.0*8),scaledGap=(int)Math.Round(scaled.Width/296.0*12);
                int scaledX=target.Right-inset+scaledGap-movingInset,scaledY=target.Top+inset-movingInset;
                command(b,"ProbeBegin");move(scaledX-5,scaledY+6);actual=Native.PhysicalBounds(b);
                check(scaled.Width>296&&Math.Abs(actual.Left-scaledX)<=1&&Math.Abs(actual.Top-scaledY)<=1,"Different Rainmeter zoom factors align visible paper rather than window shadows");command(b,"ProbeEnd");
                Native.Command("!SetZoomFactor 1 \""+b+"\"");Thread.Sleep(250);move(target.Left+6,expectedY+5);
                command(b,"BeginResize");var anchor=Native.PhysicalBounds(b);
                Native.Command("!CommandMeasure NoteScript NormalizeAnchor() \""+b+"\"");Thread.Sleep(70);
                Native.Reflow(b,360,240);Thread.Sleep(300);actual=Native.PhysicalBounds(b);
                report.AppendLine("Resize: before "+anchor.Left+","+anchor.Top+" "+anchor.Width+"x"+anchor.Height+"; after "+actual.Left+","+actual.Top+" "+actual.Width+"x"+actual.Height);
                Native.Command("!CommandMeasure NoteScript ProbeSnapLayout() \""+b+"\"");Thread.Sleep(100);report.AppendLine("Layout: "+File.ReadAllText(Path.Combine(Engine.Resources,second.Id+".snap-layout.txt")));
                check(actual.Left==anchor.Left&&actual.Top==anchor.Top&&actual.Width>anchor.Width&&actual.Height>anchor.Height,"Resizing expands from a fixed top-left corner without snapping");
                command(b,"EndResize");Native.CommitSize(second.Id,360,240);Thread.Sleep(100);
                check(Native.PhysicalBounds(b).Left==anchor.Left&&Engine.Load().Notes[1].Width==360,"Saving resize dimensions does not refresh or move the native note");
                Native.Command("!SetOption SnapController SnapEnabled 0 \""+b+"\"");Native.Command("!UpdateMeasure SnapController \""+b+"\"");command(b,"ProbeBegin");move(target.Left+6,expectedY+5);actual=Native.PhysicalBounds(b);
                check(actual.Left==target.Left+6,"Per-note smart snapping can be disabled");command(b,"ProbeEnd");
                int helpers=Process.GetProcessesByName("PitoNote").Length;Thread.Sleep(100);check(Process.GetProcessesByName("PitoNote").Length==helpers,"Snapping launches no helper or background process");
                string log=Path.Combine(folder,"Rainmeter.log");check(!File.ReadAllText(log).Contains("ERRO"),"Rainmeter reports no plugin or Lua runtime errors");
                report.AppendLine(count+" native snap checks passed.");return 0;
            }catch(Exception ex){report.AppendLine(ex.ToString());return 1;}
            finally{if(rainmeter!=null&&!rainmeter.HasExited){Native.Command("!Quit");if(!rainmeter.WaitForExit(3000))rainmeter.Kill();}Native.TestProcessId=0;Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"native-snap-checks.txt"),report.ToString());}
        }
    }
}
