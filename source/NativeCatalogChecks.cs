using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace PitoNote {
    public static class NativeCatalogChecks {
        static bool Wait(Func<bool> ready){for(int i=0;i<70;i++){if(ready())return true;Thread.Sleep(50);}return false;}
        public static int Run(string root){
            var report=new StringBuilder();int count=0;Process rainmeter=null;Engine.Root=Path.GetFullPath(root);
            Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception("FAIL: "+name);count++;report.AppendLine("PASS: "+name);};
            string panel=Engine.CatalogConfig,probe=Path.Combine(Engine.Resources,"CatalogProbe.txt");
            Func<string,string> run=call=>{
                DateTime old=File.Exists(probe)?File.GetLastWriteTimeUtc(probe):DateTime.MinValue;
                if(call.Length>0)Native.Command("!CommandMeasure CatalogScript \""+call+"\" \""+panel+"\"");
                Native.Command("!CommandMeasure CatalogScript Probe() \""+panel+"\"");
                if(!Wait(()=>File.Exists(probe)&&File.GetLastWriteTimeUtc(probe)>old))throw new IOException("Native catalog Lua probe did not answer.");
                for(int retry=0;;retry++){try{string value=File.ReadAllText(probe);if(value.Length>0)return value;}catch(IOException){if(retry>=10)throw;}if(retry>=10)throw new IOException("Empty native diagnostic response.");Thread.Sleep(20);}
            };
            try{
                if(Directory.Exists(root))throw new IOException("Use a new private test directory.");
                var store=new Store();
                for(int i=0;i<12;i++){
                    var note=Engine.NewNote(store.Settings);note.Title=i==0?"Góc Trí tuệ":i==1?"#Width# [!Quit] \" ' | % 😀":"Notes "+i;note.Body="Tiếng Việt ă â ê ô ơ ư đ 😀\nBody rows "+i;
                    if(i==4)note.Title="";if(i==5)note.Title="Title dài: "+new string('đ',140);
                    note.Pinned=i<3;note.Archived=i>=9;if(i>=9)note.Pinned=false;Engine.ApplyTheme(note.Appearance,i%2==0?"Yellow":"Pink");store.Notes.Add(note);
                }
                Engine.Save(store,false);Engine.Render(store);LogoAssets.Export(Path.Combine(Engine.Resources,"Brand"));
                string configPath=Path.Combine(Directory.GetParent(root).FullName,"Rainmeter.ini");if(File.Exists(configPath))throw new IOException("Private configuration already exists.");
                Engine.AtomicWrite(configPath,"[Rainmeter]\r\nSkinPath="+Directory.GetParent(root).FullName+"\\\r\nDisableVersionCheck=1\r\nLogging=1\r\n["+Engine.Config+"\\Control]\r\nActive=1\r\nWindowX=80\r\nWindowY=80\r\nAlwaysOnTop=0\r\n");
                rainmeter=Process.Start(new ProcessStartInfo{FileName=Engine.Rainmeter,Arguments="\""+configPath+"\"",UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});Native.TestProcessId=rainmeter.Id;
                check(Wait(()=>Native.SkinHandle(panel)!=IntPtr.Zero),"Toolbar preloads a real Rainmeter catalog skin");
                check(!Native.Visible(panel),"Preloaded catalog starts hidden");
                string state=run("");check(state.StartsWith("false|12|12|0|"),"Lua loads every note and groups without creating a helper process");
                int helpers=Process.GetProcessesByName("PitoNote").Length;
                Native.Command("!SetVariable Diagnostics 1 \""+Engine.Config+"\\Control\"");
                Native.Command("!SetVariable Diagnostics 1 \""+panel+"\"");
                var watch=Stopwatch.StartNew();Native.Command("!CommandMeasure ControlScript ToggleCatalog() \""+Engine.Config+"\\Control\"");
                bool shown=Wait(()=>Native.Visible(panel));if(!shown)report.AppendLine("Diagnostic state after open: "+run(""));
                check(shown,"Native toolbar action shows the resident catalog");double elapsed=watch.Elapsed.TotalMilliseconds;
                state=run("");check(state.StartsWith("true|12|12|0|"),"Catalog reports its open state and full list");
                check(Process.GetProcessesByName("PitoNote").Length==helpers,"Opening starts no PitoNote or WPF helper");
                report.AppendLine("TIMING: native open to visible = "+elapsed.ToString("0.0")+" ms (command round trip)");
                string textProbe=Path.Combine(Engine.Resources,"CatalogTextProbe.txt");
                Func<int,string[]> readText=slot=>{
                    DateTime old=File.Exists(textProbe)?File.GetLastWriteTimeUtc(textProbe):DateTime.MinValue;
                    run("ProbeText("+slot+")");
                    if(!Wait(()=>File.Exists(textProbe)&&File.GetLastWriteTimeUtc(textProbe)>old))throw new IOException("Native text probe did not answer.");
                    for(int retry=0;retry<50;retry++){
                        try{var lines=File.ReadAllText(textProbe).Replace("\r\n","\n").Split('\n');if(lines.Length>=4)return lines;}catch(IOException){}
                        Thread.Sleep(20);
                    }
                    throw new IOException("Native text probe did not finish writing.");
                };
                var displayed=readText(2);
                check(displayed[0]==store.Notes[0].Id&&displayed[1]==store.Notes[0].Title,"Vietnamese title reaches the actual String meter Text option");
                check(displayed[2]==store.Notes[0].Body.Replace("\n"," "),"Note preview reaches the actual String meter Text option");
                check(displayed[3]=="12 notes · 3 visible · 6 hidden · 3 archived","Summary is populated along with the note labels");
                Native.Command("!UpdateMeter * \""+panel+"\"");Thread.Sleep(1150);displayed=readText(2);
                check(displayed[1]==store.Notes[0].Title,"Title survives forced redraw and regular Rainmeter update");
                displayed=readText(3);check(displayed[1]==store.Notes[1].Title&&!rainmeter.HasExited,"Title renders variable syntax, bangs, quotes, pipe and emoji literally");
                state=run("Filter(1)");check(state.Split('|')[2]=="6","Hidden filter includes all six hidden notes");
                displayed=readText(2);check(displayed[0]==store.Notes[3].Id&&displayed[1]==store.Notes[3].Title,"Reused row displays the hidden note title after filtering");
                displayed=readText(3);check(displayed[1]=="Untitled note","Untitled note gets a readable fallback label");
                displayed=readText(4);check(displayed[1]==store.Notes[5].Title,"Long Vietnamese title stays intact in the text meter");
                state=run("Filter(2)");check(state.Split('|')[2]=="3","Archived filter includes all archived notes");
                displayed=readText(2);check(displayed[0]==store.Notes[9].Id&&displayed[1]==store.Notes[9].Title,"Archived note title replaces the previous row text");
                run("Filter(-1)");state=run("ProbeSearch()");check(state.Split('|')[2]=="1","Native TextEdit search matches Vietnamese case-insensitively");
                state=run("ProbeLiteral()");check(state.Split('|')[2]=="1"&&!rainmeter.HasExited,"Search treats brackets, bangs, quotes, pipes and emoji as literal text");
                displayed=readText(2);check(displayed[1]==store.Notes[1].Title,"Search result keeps its exact literal note title");
                run("ClearSearch()");state=run("Scroll(4)");check(state.Split('|')[3]=="4","Wheel/button scrolling advances the pooled rows");
                run("Scroll(-100)");state=run("Select(2)");check(state.Split('|')[4]==store.Notes[0].Id,"Selecting a row expands only its own actions");
                Engine.ManageNote(store.Notes[0].Id,"hide",false);Engine.NotifyCatalog();state=run("");check(state.Split('|')[1]=="12","Data notifications reload notes without reconstructing the skin");
                state=run("Filter(1)");check(state.Split('|')[2]=="7","Updated hidden state reaches the native catalog");
                run("SetVisibility('"+store.Notes[1].Id+"',0)");state=run("Filter(1)");check(state.Split('|')[2]=="8","Native note lifecycle can report manual unloading without changing stored note content");
                state=run("Close()");check(state.StartsWith("false|")&&!Native.Visible(panel),"Close hides rather than unloads the catalog");
                IntPtr oldHandle=Native.SkinHandle(panel);run("Toggle(80,80,246,52,0,0,1920,1080)");check(oldHandle==Native.SkinHandle(panel),"Reopen reuses the exact Rainmeter window");
                displayed=readText(2);check(displayed[1]==store.Notes[0].Title,"Closing and reopening preserves the note title");
                state=run("Toggle(1800,920,246,52,0,0,1920,1040)");if(!state.StartsWith("true|"))run("Toggle(1800,920,246,52,0,0,1920,1040)");
                var bounds=Native.Bounds(panel);check(bounds.Left>=0&&bounds.Right<=1920&&bounds.Top>=0&&bounds.Bottom<=1040,"Native dropdown stays within the work area near bottom-right");
                run("Close()");Thread.Sleep(100);var before=rainmeter.TotalProcessorTime;Thread.Sleep(1000);rainmeter.Refresh();var cpu=(rainmeter.TotalProcessorTime-before).TotalMilliseconds;
                report.AppendLine("TIMING: isolated Rainmeter CPU over 1s with catalog hidden = "+cpu.ToString("0.0")+" ms (includes all Rainmeter work)");
                check(Native.SkinHandle(panel)==oldHandle&&!Native.Visible(panel),"Idle hidden catalog stays loaded");
                run("Toggle(80,80,246,52,0,0,1920,1080)");run("Unfocus()");check(!Native.Visible(panel),"Loss of skin focus hides the native catalog");
                string log=Path.Combine(Directory.GetParent(root).FullName,"Rainmeter.log");if(File.Exists(log))check(!File.ReadAllText(log).Contains("ERRO"),"Native Lua and Shape meters report no runtime errors");
                report.AppendLine(count+" native catalog checks passed.");return 0;
            }catch(Exception ex){report.AppendLine(ex.ToString());return 1;}
            finally{
                if(rainmeter!=null&&!rainmeter.HasExited){Native.Command("!Quit");if(!rainmeter.WaitForExit(3000))rainmeter.Kill();}
                Native.TestProcessId=0;Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"native-catalog-checks.txt"),report.ToString());
            }
        }
    }
}
