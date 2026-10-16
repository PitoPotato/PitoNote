using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;

namespace PitoNote {
    public static class NativeUpgradeChecks {
        static bool Wait(Func<bool> ready){for(int i=0;i<50;i++){if(ready())return true;Thread.Sleep(100);}return false;}
        public static int Run(string root){
            var report=new StringBuilder();int count=0;Process rainmeter=null;Engine.Root=Path.GetFullPath(root);
            Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception("FAIL: "+name);count++;report.AppendLine("PASS: "+name);};
            Action onlyPrivate=()=>{var running=Process.GetProcessesByName("Rainmeter");if(rainmeter==null||running.Length!=1||running[0].Id!=rainmeter.Id)throw new IOException("Expected only the isolated Rainmeter test process. No user instance is controlled.");};
            try{
                if(Process.GetProcessesByName("Rainmeter").Length!=0)throw new IOException("Close Rainmeter before running this isolated integration test.");
                if(Directory.Exists(root))throw new IOException("Use a new private test directory.");
                var store=new Store();store.Settings.FontName=store.Settings.TitleFontName="Segoe UI";store.Settings.Position="Normal";
                var note=Engine.NewNote(store.Settings);note.Title="PitoNote · kiểm tra riêng";note.Body="Tiếng Việt ă â ê ô ơ ư đ 😀\n#Width# [!Quit]";note.Height=note.Appearance.CardHeight=320;store.Notes.Add(note);Engine.Save(store,false);Engine.Render(store);
                File.Copy(System.Reflection.Assembly.GetExecutingAssembly().Location,Path.Combine(Engine.Resources,"PitoNote.exe"));LogoAssets.Export(Path.Combine(Engine.Resources,"Brand"));
                string configPath=Path.Combine(Directory.GetParent(root).FullName,"Rainmeter.ini");if(File.Exists(configPath))throw new IOException("Private configuration already exists.");
                string config="[Rainmeter]\r\nSkinPath="+Directory.GetParent(root).FullName+"\\\r\nDisableVersionCheck=1\r\nLogging=0\r\n["+Engine.Config+"\\Control]\r\nActive=1\r\nWindowX=80\r\nWindowY=80\r\nAlwaysOnTop=0\r\n["+Engine.Config+"\\Notes\\"+note.Id+"]\r\nActive=1\r\nWindowX=400\r\nWindowY=160\r\nAlwaysOnTop=0\r\n";
                Engine.AtomicWrite(configPath,config);
                rainmeter=Process.Start(new ProcessStartInfo{FileName=Engine.Rainmeter,Arguments="\""+configPath+"\"",UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
                check(Wait(()=>Native.SkinHandle(Engine.Config+"\\Notes\\"+note.Id)!=IntPtr.Zero),"Real Rainmeter loads the private note using a valid INI path");onlyPrivate();
                string target=Engine.Config+"\\Notes\\"+note.Id;var full=Native.Bounds(target);
                Engine.SetCollapsed(note.Id,true);check(Wait(()=>Native.Bounds(target).Height<full.Height),"Real native note shrinks to the compact title bar");
                var compact=Native.Bounds(target);check(compact.Width==full.Width&&Engine.Load().Notes[0].Height==320,"Native collapse retains width and full saved height");
                onlyPrivate();Engine.SetCollapsed(note.Id,false);check(Wait(()=>Native.Bounds(target).Height==full.Height),"Real native note expands to its previous dimensions");
                onlyPrivate();Engine.SetCollapsed(note.Id,true);Engine.EditOnDesktop(note.Id);check(Wait(()=>Native.Bounds(target).Height==full.Height)&&!Engine.Load().Notes[0].Collapsed,"Edit navigation expands the actual note without a popup editor");
                Thread.Sleep(1200);
                check(Engine.Load().Notes[0].Body==note.Body,"Focus and collapse do not rewrite text just to convert line endings");
                onlyPrivate();var copy=Engine.DuplicateNote(note.Id);check(Wait(()=>Native.SkinHandle(Engine.Config+"\\Notes\\"+copy.Id)!=IntPtr.Zero),"Duplicate creates and loads a real independent native note");
                onlyPrivate();Engine.ToggleNativeCatalog();check(Wait(()=>Native.Visible(Engine.CatalogConfig)),"Resident Rainmeter catalog opens without a WPF process");
                Native.Command("!CommandMeasure CatalogScript Close() \""+Engine.CatalogConfig+"\"");onlyPrivate();
                check(Process.GetProcessesByName("Rainmeter").Length==1,"No malformed secondary Rainmeter instance was created");
                report.AppendLine(count+" native upgrade checks passed.");return 0;
            }catch(Exception ex){report.AppendLine(ex.ToString());return 1;}
            finally{
                if(rainmeter!=null&&!rainmeter.HasExited){var running=Process.GetProcessesByName("Rainmeter");if(running.Length==1&&running[0].Id==rainmeter.Id){Native.Command("!Quit");rainmeter.WaitForExit(3000);}if(!rainmeter.HasExited)rainmeter.Kill();}
                Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"native-upgrade-checks.txt"),report.ToString());
            }
        }
    }
}
