using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace PitoNote {
    public static class Program {
        public static MainWindow Window;
        static Mutex singleton;
        [STAThread] public static int Main(string[] args) {
            try {
                Engine.Root=Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).FullName;
                if(args.Length>0&&args[0]=="--selftest")return Checks.Run(args.Length>1?args[1]:Path.Combine(Path.GetTempPath(),"PitoNote-check-"+Guid.NewGuid().ToString("N")));
                if(args.Length>1&&args[0]=="--verify-delete")return DeleteChecks.Run(args[1]);
                if(args.Length>1&&args[0]=="--verify-management")return ManagementChecks.Run(args[1]);
                if(args.Length>1&&args[0]=="--verify-upgrade")return UpgradeChecks.Run(args[1]);
                if(args.Length>1&&args[0]=="--verify-native-upgrade")return NativeUpgradeChecks.Run(args[1]);
                if(args.Length>1&&args[0]=="--verify-native-catalog")return NativeCatalogChecks.Run(args[1]);
                if(args.Length>1&&args[0]=="--export-brand"){LogoAssets.Export(args[1]);return 0;}
                if(args.Length>1&&args[0]=="--stage-release"){
                    string clean=Path.GetFullPath(args[1]);
                    if(Directory.Exists(clean)||File.Exists(clean))throw new IOException("Release staging requires a new directory.");
                    Engine.Root=clean;var empty=new Store();Engine.Save(empty,false);Engine.Render(empty);LogoAssets.Export(Path.Combine(Engine.Resources,"Brand"));return 0;
                }
                if(args.Length>1&&args[0]=="--verify-settings")return PopupChecks.Run(args[1]);
                if(args.Length>1&&args[0]=="--create-native-probe")return Checks.CreateProbe(args[1]);
                if(args.Length>1&&args[0]=="--verify-layout")return Native.VerifyLayout(args[1]);
                if(args.Length>2&&args[0]=="--commit-field"){Engine.CommitField(args[1],args[2]);return 0;}
                if(args.Length>3&&args[0]=="--resize"){Native.Resize(args[1],double.Parse(args[2],CultureInfo.InvariantCulture),double.Parse(args[3],CultureInfo.InvariantCulture));return 0;}
                string verb=args.Length>0?args[0]:"settings",id=args.Length>1?args[1]:"";
                if(verb=="list"){Engine.ToggleNativeCatalog();return 0;}
                if(verb=="catalog-action"&&args.Length==3){Engine.ManageNote(args[1],args[2]);return 0;}
                if(verb=="desktop-edit"){Engine.EditOnDesktop(id);return 0;}
                if(verb=="collapse"){Engine.SetCollapsed(id);return 0;}
                if(verb=="duplicate"){Engine.DuplicateNote(id);return 0;}
                if(verb=="picker")verb="manage";
                if(verb=="delete"){DeleteNoteUI.ConfirmAndDelete(id);return 0;}
                if(verb=="--initialize"){Native.WithDataLock(()=>{var s=Engine.Load();Engine.Save(s);Engine.Render(s);});return 0;}
                if(verb=="--refresh"){var s=Engine.Load();Engine.Render(s);Engine.Refresh(s);return 0;}
                if(verb=="new"){
                    var note=Engine.CreateNote();Native.WaitForNote(note.Id);
                    Engine.Bang("!CommandMeasure NoteScript "+(note.Appearance.ShowTitle?"FocusTitle()":"FocusBody()")+" \""+Engine.Config+"\\Notes\\"+note.Id+"\"");return 0;
                }
                if(verb=="inline"||verb=="inline-title"){
                    Engine.ContentPath(id,"Body","Content");Engine.Bang("!CommandMeasure NoteScript "+(verb=="inline-title"?"FocusTitle()":"FocusBody()")+" \""+Engine.Config+"\\Notes\\"+id+"\"");return 0;
                }
                if(Quick(Engine.Load(),verb,id))return 0;
                string noteId=(verb=="details"||verb=="edit")?id:null;
                string pipe=PipeName(noteId);bool first;
                singleton=new Mutex(true,"Local\\"+pipe,out first);
                if(!first){
                    using(var client=new NamedPipeClientStream(".",pipe,PipeDirection.Out)){client.Connect(2500);using(var writer=new StreamWriter(client))writer.WriteLine(verb=="manage"?"manage":"activate");}return 0;
                }
                var app=new Application();Window=new MainWindow(Engine.Load(),noteId);
                if(verb=="manage")Window.ShowManagement();
                var listener=new Thread(()=>{
                    while(true){try{using(var server=new NamedPipeServerStream(pipe,PipeDirection.In,1,PipeTransmissionMode.Byte,PipeOptions.None)){
                        server.WaitForConnection();using(var reader=new StreamReader(server)){string command=reader.ReadLine();app.Dispatcher.BeginInvoke(new Action(()=>Window.Handle(command,noteId)));}
                    }}catch{return;}}
                });listener.IsBackground=true;listener.Start();
                app.DispatcherUnhandledException+=(s,e)=>{MessageBox.Show(e.Exception.Message,"PitoNote",MessageBoxButton.OK,MessageBoxImage.Error);e.Handled=true;};
                return app.Run(Window);
            }catch(Exception ex){
                try{Directory.CreateDirectory(Engine.Resources);File.AppendAllText(Path.Combine(Engine.Resources,"error.log"),DateTime.Now+" "+ex+Environment.NewLine);}catch{}
                if(args.Length==0||args[0]!="--commit-field")MessageBox.Show("Could not complete this action.\n"+ex.Message+"\nYour data and drafts have been preserved.","PitoNote",MessageBoxButton.OK,MessageBoxImage.Error);
                return 1;
            }finally{if(singleton!=null){try{singleton.ReleaseMutex();}catch{}singleton.Dispose();}}
        }
        public static bool Quick(Store store,string verb,string id) {
            if(!new[]{"toggle","hide","pin","archive"}.Contains(verb))return false;Engine.ChangeState(id,verb);return true;
        }
        public static string PipeName(string id){string identity=Engine.Root.ToLowerInvariant()+"|"+(id??"defaults");string key=Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("/","").Replace("+","").Replace("=","").Substring(0,22);return "PitoNote-v3-"+key;}
        public static void NotifyDeleted(string id){try{using(var client=new NamedPipeClientStream(".",PipeName(id),PipeDirection.Out)){client.Connect(150);using(var writer=new StreamWriter(client))writer.WriteLine("deleted");}}catch(TimeoutException){}catch(IOException){}}
    }

    public partial class MainWindow:Window {
        Store store;
        readonly string editingId;
        Settings savedSettings;
        bool filling,changedSettings;
        string[] fonts;
        readonly Dictionary<string,FrameworkElement> fields=new Dictionary<string,FrameworkElement>();
        Border preview;
        TextBlock previewTitle,previewBody,previewContrast,info,status,pageHeading,pageSubtitle,scopeCaption;
        readonly bool highContrast=SystemParameters.HighContrast;
        bool IsNote {get{return editingId!=null;}}
        Note Current(){return store.Notes.FirstOrDefault(n=>n.Id==editingId);}
        public MainWindow(Store data,string noteId=null) {
            store=data;editingId=noteId;fonts=Engine.FontNames();
            if(IsNote&&Current()==null)throw new InvalidDataException("This note could not be found.");
            savedSettings=Engine.Clone(IsNote?Current().Appearance:store.Settings);
            if(IsNote){savedSettings.Width=Current().Width;savedSettings.CardHeight=Current().Height;}
            Title=IsNote?"Note settings — PitoNote":"General settings — PitoNote";
            Width=IsNote?1000:1060;Height=770;MinWidth=900;MinHeight=640;
            WindowStartupLocation=WindowStartupLocation.CenterScreen;FontFamily=new FontFamily("Segoe UI");FontSize=14;
            LogoAssets.SetWindowIcon(this);
            Background=highContrast?SystemColors.WindowBrush:Brush("F5F6FA");Foreground=highContrast?SystemColors.WindowTextBrush:Brush("282A32");
            UseLayoutRounding=true;SnapsToDevicePixels=true;TextOptions.SetTextFormattingMode(this,TextFormattingMode.Display);
            MaxHeight=SystemParameters.WorkArea.Height;MaxWidth=SystemParameters.WorkArea.Width;StyleControls();
            var root=new DockPanel{Margin=new Thickness(24,22,24,16)};Content=root;
            var header=new DockPanel{Margin=new Thickness(0,0,0,18)};DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
            scopeCaption=Label(IsNote?"This note only":"Existing notes unchanged",12,false,"565D70");
            var badge=new Border{CornerRadius=new CornerRadius(10),Padding=new Thickness(10,6,10,6),VerticalAlignment=VerticalAlignment.Center,
                Background=highContrast?SystemColors.ControlBrush:Brush("E9ECF4"),Child=scopeCaption};
            DockPanel.SetDock(badge,Dock.Right);header.Children.Add(badge);
            var brand=LogoAssets.Image(34);brand.Margin=new Thickness(0,4,12,0);DockPanel.SetDock(brand,Dock.Left);header.Children.Add(brand);
            var heading=new StackPanel();header.Children.Add(heading);pageHeading=Label(IsNote?"Note settings":"New-note defaults",24,true);heading.Children.Add(pageHeading);
            var subtitle=Label(IsNote?(string.IsNullOrWhiteSpace(Current().Title)?"Untitled note":Current().Title):"Applies only to notes created from now on.",13,false,"626570");pageSubtitle=subtitle;
            subtitle.TextWrapping=TextWrapping.NoWrap;subtitle.TextTrimming=TextTrimming.CharacterEllipsis;heading.Children.Add(subtitle);
            status=Label(IsNote?"Edit text directly on the desktop note, not in this window.":"Each note keeps its own settings from the moment it is created.",12,false,"626570");status.Margin=new Thickness(2,12,0,0);DockPanel.SetDock(status,Dock.Bottom);root.Children.Add(status);
            root.Children.Add(BuildSettings());FillSettings();
            PreviewKeyDown+=(s,e)=>{if(e.Key==Key.S&&Keyboard.Modifiers.HasFlag(ModifierKeys.Control)&&!ManagementActive){SaveSettings();e.Handled=true;}};
            Closing+=(s,e)=>{if(changedSettings&&MessageBox.Show(this,"You have unsaved settings. Close and discard these changes?","Unsaved settings",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)e.Cancel=true;};
            Activated+=(s,e)=>ReloadFromDisk();
        }
        static SolidColorBrush Brush(string hex){return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#"+hex));}
        TextBlock Label(string text,double size=14,bool bold=false,string color=null){
            var label=new TextBlock{Text=text,FontSize=size,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,5)};
            if(bold)label.FontWeight=FontWeights.SemiBold;if(color!=null&&!highContrast)label.Foreground=Brush(color);return label;
        }
        Button Btn(string text,Action action,bool primary=false){
            var button=new Button{Content=text,MinHeight=36};AutomationProperties.SetName(button,text);
            if(primary&&!highContrast){button.Background=Brush("4F5FCC");button.Foreground=Brush("FFFFFF");}
            button.Click+=(s,e)=>{try{action();}catch(Exception ex){MessageBox.Show(this,ex.Message,"PitoNote",MessageBoxButton.OK,MessageBoxImage.Warning);}};return button;
        }
        TextBox Input(string name,bool multiline=false){
            var box=new TextBox{MinHeight=36};AutomationProperties.SetName(box,name);
            if(multiline){box.AcceptsReturn=true;box.TextWrapping=TextWrapping.Wrap;box.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;box.VerticalContentAlignment=VerticalAlignment.Top;}return box;
        }
        ComboBox Combo(System.Collections.Generic.IEnumerable<string> items,string name,bool editable=false){
            var combo=new ComboBox{IsEditable=editable,IsTextSearchEnabled=true,MaxDropDownHeight=260};foreach(string item in items)combo.Items.Add(item);AutomationProperties.SetName(combo,name);return combo;
        }
        void Field(Panel parent,string text,FrameworkElement input){parent.Children.Add(Label(text,12,true));input.Margin=new Thickness(0,0,0,12);parent.Children.Add(input);}
        void PickColor(string key){
            using(var dialog=new System.Windows.Forms.ColorDialog()){dialog.FullOpen=true;
                try{var color=(Color)ColorConverter.ConvertFromString("#"+GetText(key));dialog.Color=System.Drawing.Color.FromArgb(color.R,color.G,color.B);}catch{}
                if(dialog.ShowDialog()==System.Windows.Forms.DialogResult.OK)SetText(key,dialog.Color.R.ToString("X2")+dialog.Color.G.ToString("X2")+dialog.Color.B.ToString("X2"));
            }
        }
        void Export(){var dialog=new SaveFileDialog{Filter="PitoNote data (*.json)|*.json",FileName="PitoNote-"+DateTime.Now.ToString("yyyyMMdd")+".json"};
            if(dialog.ShowDialog(this)==true){File.WriteAllText(dialog.FileName,Engine.Json(Engine.Load()),new UTF8Encoding(true));status.Text="Data exported successfully.";}}
        void Import(){
            if(changedSettings&&MessageBox.Show(this,"Restoring data will discard unsaved defaults. Continue?","Unsaved settings",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
            var dialog=new OpenFileDialog{Filter="PitoNote data (*.json)|*.json",InitialDirectory=Path.Combine(Engine.Resources,"Backups")};
            if(dialog.ShowDialog(this)!=true)return;var imported=Engine.Parse(File.ReadAllText(dialog.FileName,Encoding.UTF8));
            if(MessageBox.Show(this,"Restore "+imported.Notes.Count+" notes from the selected file? Your current data will be backed up.","Restore data",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            Engine.ImportStore(imported);changedSettings=false;ReloadFromDisk();status.Text="Data restored. A backup of the previous data has been kept.";
        }
        void ReloadFromDisk(){
            if(changedSettings)return;store=Engine.Load();
            if(IsNote&&Current()==null)return;
            savedSettings=Engine.Clone(IsNote?Current().Appearance:store.Settings);
            if(IsNote){savedSettings.Width=Current().Width;savedSettings.CardHeight=Current().Height;}FillSettings();
        }
        internal bool EditsNote(string id){return editingId==id;}
        public void Handle(string verb,string id){if(verb=="deleted"){changedSettings=false;Close();return;}ReloadFromDisk();if(verb=="manage")ShowManagement();if(WindowState==WindowState.Minimized)WindowState=WindowState.Normal;Show();Activate();}
    }
}
