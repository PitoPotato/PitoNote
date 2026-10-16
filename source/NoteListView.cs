using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Path=System.IO.Path;

namespace PitoNote {
    // Management only: the sole editable field here is the search query.
    public class NoteListView:UserControl {
        public event Action<string,string> Navigate;
        public event Action<bool> BusyChanged;
        readonly bool dropdown,live;
        readonly StackPanel rows=new StackPanel();
        readonly TextBox search=new TextBox();
        readonly TextBlock watermark=new TextBlock(),summary=new TextBlock(),feedback=new TextBlock();
        readonly ScrollViewer scroll=new ScrollViewer();
        readonly Dictionary<int,Button> filters=new Dictionary<int,Button>();
        readonly DispatcherTimer timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};
        readonly DispatcherTimer pending=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(180)};
        FileSystemWatcher watcher;
        Store snapshot;bool dataDirty=true;string dataSignature="";DateTime nextRead=DateTime.MinValue;
        internal int DataLoads {get;private set;}
        sealed class CachedRow {public NoteEntry Entry;public Border Root;public Button Header;public TextBlock Title,Preview;public string State;public List<Button> Actions=new List<Button>();}
        readonly Dictionary<string,CachedRow> cards=new Dictionary<string,CachedRow>();
        readonly Dictionary<int,TextBlock> headings=new Dictionary<int,TextBlock>();
        internal Border RowFor(string id){CachedRow row;return cards.TryGetValue(id,out row)?row.Root:null;}
        List<NoteEntry> entries=new List<NoteEntry>();
        string expandedId,signature="";int group=-1,limit=50;bool busy;
        public bool Busy {get{return busy;}}
        internal int EntryCount {get{return entries.Count;}}
        internal string ExpandedId {get{return expandedId;}}
        internal bool OffersContentEditing {get{return dropdown;}}
        internal List<NoteEntry> Filtered {get{return NoteCatalog.Filter(entries,search.Text,group);}}

        public static SolidColorBrush Ink(string hex){return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#"+hex));}
        public NoteListView(bool dropdown,bool live=true) {
            this.dropdown=dropdown;this.live=live;FontFamily=Engine.ResolveFont("Segoe UI");FontSize=13;
            if(!SystemParameters.HighContrast)Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(@"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
 <Style TargetType='Button'>
  <Setter Property='Background' Value='Transparent'/><Setter Property='Foreground' Value='#403F36'/><Setter Property='BorderBrush' Value='Transparent'/><Setter Property='BorderThickness' Value='1'/>
  <Setter Property='Padding' Value='7,5'/><Setter Property='MinHeight' Value='30'/><Setter Property='Cursor' Value='Hand'/><Setter Property='HorizontalContentAlignment' Value='Center'/>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'>
   <Border x:Name='Box' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='8'>
    <ContentPresenter Margin='{TemplateBinding Padding}' HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center'/>
   </Border><ControlTemplate.Triggers>
    <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Box' Property='Opacity' Value='0.72'/></Trigger>
    <Trigger Property='IsPressed' Value='True'><Setter TargetName='Box' Property='Opacity' Value='0.52'/></Trigger>
    <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Box' Property='BorderBrush' Value='{Binding Foreground, RelativeSource={RelativeSource TemplatedParent}}'/></Trigger>
    <Trigger Property='IsEnabled' Value='False'><Setter TargetName='Box' Property='Opacity' Value='0.35'/></Trigger>
   </ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType='TextBox'>
  <Setter Property='Background' Value='#F0EFE9'/><Setter Property='Foreground' Value='#36362F'/><Setter Property='BorderBrush' Value='#DCDAD0'/><Setter Property='BorderThickness' Value='1'/><Setter Property='Padding' Value='10,4'/><Setter Property='MinHeight' Value='36'/>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='TextBox'>
   <Border x:Name='Box' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='10'><ScrollViewer x:Name='PART_ContentHost' Margin='{TemplateBinding Padding}'/></Border>
   <ControlTemplate.Triggers><Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='Box' Property='BorderBrush' Value='#79776A'/></Trigger></ControlTemplate.Triggers>
  </ControlTemplate></Setter.Value></Setter>
 </Style>
</ResourceDictionary>"));
            var root=new Grid();root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Content=root;
            summary.FontSize=12;summary.TextWrapping=TextWrapping.Wrap;summary.Margin=new Thickness(0,0,0,10);summary.Foreground=SystemParameters.HighContrast?SystemColors.WindowTextBrush:Ink("77756A");root.Children.Add(summary);
            var searchGrid=new Grid{Margin=new Thickness(0,0,0,8)};Grid.SetRow(searchGrid,1);root.Children.Add(searchGrid);searchGrid.Children.Add(search);
            search.MaxLength=200;AutomationProperties.SetName(search,"Search notes by title or content");
            watermark.Text="Search notes…";watermark.Margin=new Thickness(12,9,12,8);watermark.Foreground=SystemParameters.HighContrast?SystemColors.GrayTextBrush:Ink("7C796E");watermark.IsHitTestVisible=false;searchGrid.Children.Add(watermark);
            search.TextChanged+=(s,e)=>{watermark.Visibility=search.Text.Length==0?Visibility.Visible:Visibility.Collapsed;limit=50;Rebuild();scroll.ScrollToTop();};
            var filterRow=new WrapPanel{Margin=new Thickness(0,0,0,8)};Grid.SetRow(filterRow,2);root.Children.Add(filterRow);
            for(int i=-1;i<3;i++){int key=i;var b=TextButton(i<0?"All":i==0?"Visible":i==1?"Hidden":"Archived",()=>{group=key;limit=50;Rebuild();scroll.ScrollToTop();});b.FontSize=11;b.Margin=new Thickness(0,0,4,3);filters[key]=b;filterRow.Children.Add(b);}
            scroll.Content=rows;scroll.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;scroll.HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled;scroll.CanContentScroll=false;scroll.Padding=new Thickness(0,0,4,0);Grid.SetRow(scroll,3);root.Children.Add(scroll);
            feedback.FontSize=11;feedback.TextWrapping=TextWrapping.Wrap;feedback.Margin=new Thickness(0,7,0,0);feedback.Foreground=SystemParameters.HighContrast?SystemColors.WindowTextBrush:Ink("6B685D");Grid.SetRow(feedback,4);root.Children.Add(feedback);
            Loaded+=(s,e)=>{if(live){Watch();timer.Start();}Refresh(true);};Unloaded+=(s,e)=>{timer.Stop();pending.Stop();if(watcher!=null){watcher.Dispose();watcher=null;}};
            timer.Tick+=(s,e)=>{if(!busy)Refresh();};pending.Tick+=(s,e)=>{pending.Stop();if(!busy)Refresh();};
            Refresh();
        }
        void Watch(){
            if(watcher!=null||!Directory.Exists(Engine.Resources))return;
            watcher=new FileSystemWatcher(Engine.Resources){IncludeSubdirectories=true,NotifyFilter=NotifyFilters.LastWrite|NotifyFilters.FileName|NotifyFilters.Size};
            FileSystemEventHandler changed=(s,e)=>Invalidate(e.FullPath);watcher.Changed+=changed;watcher.Created+=changed;watcher.Deleted+=changed;
            watcher.Renamed+=(s,e)=>{Invalidate(e.OldFullPath);Invalidate(e.FullPath);};watcher.Error+=(s,e)=>Invalidate(Engine.DataPath);watcher.EnableRaisingEvents=true;
        }
        void Invalidate(string path){
            if(!path.Equals(Engine.DataPath,StringComparison.OrdinalIgnoreCase)&&!string.Equals(Path.GetDirectoryName(path),Path.Combine(Engine.Resources,"Drafts"),StringComparison.OrdinalIgnoreCase))return;
            if(Dispatcher.HasShutdownStarted)return;
            try{Dispatcher.BeginInvoke(new Action(()=>{dataDirty=true;if(IsLoaded){pending.Stop();pending.Start();}}));}catch(InvalidOperationException){}
        }
        Button TextButton(string label,Action action){var b=new Button{Content=label};AutomationProperties.SetName(b,label);b.Click+=(s,e)=>action();return b;}
        static TextBlock Text(string value,double size,Brush color,bool bold=false){return new TextBlock{Text=value,FontSize=size,Foreground=color,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,TextTrimming=TextTrimming.CharacterEllipsis,TextWrapping=TextWrapping.NoWrap};}
        public void FocusSearch(){search.Focus();search.SelectAll();}
        public void Refresh(bool force=false) {
            try{if(force||dataDirty||snapshot==null||DateTime.UtcNow>=nextRead){snapshot=Engine.Load();dataSignature=Engine.Json(snapshot);dataDirty=false;DataLoads++;nextRead=DateTime.UtcNow.AddSeconds(15);}
                var next=NoteCatalog.Build(snapshot,live?(Func<Note,bool>)(n=>n.Pinned&&!n.Archived&&Native.SkinHandle(Engine.Config+"\\Notes\\"+n.Id)!=IntPtr.Zero):null);
                string changed=dataSignature+"|"+string.Join(",",next.Select(n=>n.Id+":"+n.Group));if(changed==signature)return;signature=changed;entries=next;
                if(!entries.Any(n=>n.Id==expandedId))expandedId=null;Rebuild();
            }catch(Exception ex){feedback.Text="Could not load notes: "+ex.Message;}
        }
        internal void SetSnapshot(Store store){entries=NoteCatalog.Build(store);Rebuild();}
        internal void SetFilter(string query,int category){search.Text=query;group=category;Rebuild();}
        internal void Expand(string id){expandedId=expandedId==id?null:id;Rebuild();}
        void Rebuild() {
            double offset=scroll.VerticalOffset;var filtered=Filtered;var desired=new List<UIElement>();string focusedId=null,focusedAction=null;
            foreach(var pair in cards)if(pair.Value.Root.IsKeyboardFocusWithin){focusedId=pair.Key;var focused=Keyboard.FocusedElement as Button;focusedAction=focused==null?null:focused.Tag as string;break;}
            foreach(string id in cards.Keys.Where(id=>!entries.Any(n=>n.Id==id)).ToArray())cards.Remove(id);
            summary.Text=entries.Count+" notes · "+entries.Count(n=>n.Group==0)+" visible · "+entries.Count(n=>n.Group==1)+" hidden · "+entries.Count(n=>n.Group==2)+" archived";
            foreach(var pair in filters){if(!SystemParameters.HighContrast){pair.Value.Background=Ink(group==pair.Key?"E6E3D9":"F5F3ED");pair.Value.Foreground=Ink("4D4B40");}AutomationProperties.SetHelpText(pair.Value,group==pair.Key?"Selected filter":"Filter notes");}
            if(filtered.Count==0){var empty=Text(entries.Count==0?"No notes yet.":"No matching notes.",13,SystemParameters.HighContrast?SystemColors.WindowTextBrush:Ink("77756A"));empty.Margin=new Thickness(4,24,0,24);desired.Add(empty);}
            int shown=0;
            foreach(var section in filtered.Take(limit).GroupBy(n=>n.Group)) {
                TextBlock header;if(!headings.TryGetValue(section.Key,out header)){header=Text("",11,SystemParameters.HighContrast?SystemColors.WindowTextBrush:Ink("797669"),true);header.Margin=new Thickness(3,9,0,7);headings[section.Key]=header;}
                header.Text=NoteCatalog.GroupNames[section.Key]+" · "+filtered.Count(n=>n.Group==section.Key);desired.Add(header);
                foreach(var entry in section){desired.Add(StableCard(entry));shown++;}
            }
            if(shown<filtered.Count){var more=TextButton("Show more · remaining: "+(filtered.Count-shown)+" notes",()=>{limit+=50;Rebuild();});more.Margin=new Thickness(0,8,0,0);desired.Add(more);}
            var keep=new HashSet<UIElement>(desired);for(int i=rows.Children.Count-1;i>=0;i--)if(!keep.Contains(rows.Children[i]))rows.Children.RemoveAt(i);
            for(int i=0;i<desired.Count;i++){if(i<rows.Children.Count&&rows.Children[i]==desired[i])continue;rows.Children.Remove(desired[i]);rows.Children.Insert(i,desired[i]);}
            scroll.ScrollToVerticalOffset(offset);
            CachedRow focusedRow;if(focusedId!=null&&cards.TryGetValue(focusedId,out focusedRow)&&desired.Contains(focusedRow.Root)){var target=focusedAction=="header"?focusedRow.Header:focusedRow.Actions.FirstOrDefault(b=>(string)b.Tag==focusedAction);if(target!=null&&Keyboard.FocusedElement!=target)target.Focus();}
        }
        Border StableCard(NoteEntry entry){
            string state=entry.Group+"|"+entry.Note.Done+"|"+entry.Note.Collapsed+"|"+(expandedId==entry.Id)+"|"+Engine.Json(new Store{Settings=entry.Note.Appearance});CachedRow cached;
            if(!cards.TryGetValue(entry.Id,out cached)||cached.State!=state){var root=Card(entry);cards[entry.Id].State=state;return root;}
            cached.Entry.Note=entry.Note;cached.Entry.Group=entry.Group;cached.Entry.Order=entry.Order;cached.Title.Text=entry.Title;cached.Title.ToolTip=entry.Title;
            cached.Preview.Text=entry.Preview.Length==0?entry.Status:entry.Preview;AutomationProperties.SetName(cached.Header,entry.Title+" · "+entry.Status+" · actions");
            foreach(var action in cached.Actions)AutomationProperties.SetName(action,Convert.ToString(action.ToolTip)+" · "+entry.Title);return cached.Root;
        }
        Border Card(NoteEntry entry) {
            var settings=entry.Note.Appearance;Brush fg=SystemParameters.HighContrast?SystemColors.WindowTextBrush:Ink(dropdown?settings.TextColor:"302D26");
            var content=new StackPanel();var card=new Border{Background=SystemParameters.HighContrast?SystemColors.WindowBrush:Ink(dropdown?settings.Background:"FFFFFF"),BorderBrush=SystemParameters.HighContrast?SystemColors.WindowTextBrush:new SolidColorBrush(Color.FromArgb(38,60,56,40)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(dropdown?14:11),Margin=new Thickness(0,0,0,8),Child=content};
            var record=new CachedRow{Entry=entry,Root=card};cards[entry.Id]=record;
            var header=new Button{Tag="header",HorizontalContentAlignment=HorizontalAlignment.Stretch,Padding=new Thickness(12,10,12,10),Foreground=fg,Background=Brushes.Transparent};record.Header=header;AutomationProperties.SetName(header,entry.Title+" · "+entry.Status+" · actions");AutomationProperties.SetHelpText(header,"Click or press Enter to show actions for this note");
            var layout=new DockPanel();var chevron=Glyph(expandedId==entry.Id?"up":"down",fg,13);chevron.VerticalAlignment=VerticalAlignment.Center;chevron.Margin=new Thickness(10,0,0,0);DockPanel.SetDock(chevron,Dock.Right);layout.Children.Add(chevron);
            var words=new StackPanel();var title=Text(entry.Title,13.5,fg,true);title.ToolTip=entry.Title;if(dropdown)title.FontFamily=Engine.ResolveFont(settings.FontName);words.Children.Add(title);
            string preview=entry.Preview.Length==0?entry.Status:entry.Preview;var excerpt=Text(preview,11.5,fg);excerpt.Opacity=.76;excerpt.Margin=new Thickness(0,4,0,0);if(dropdown)excerpt.FontFamily=Engine.ResolveFont(settings.FontName);words.Children.Add(excerpt);layout.Children.Add(words);header.Content=layout;header.Click+=(s,e)=>Expand(entry.Id);content.Children.Add(header);
            record.Title=title;record.Preview=excerpt;
            if(expandedId==entry.Id) {
                var actionRow=new DockPanel{Margin=new Thickness(10,0,10,9),LastChildFill=false};
                Brush danger=SystemParameters.HighContrast?SystemColors.WindowTextBrush:Ink(dropdown&&Engine.Contrast(settings.Background,"982D37")<3?"FFB4B9":"982D37");var delete=ActionButton("Delete note","trash",entry,"delete",danger);DockPanel.SetDock(delete,Dock.Right);actionRow.Children.Add(delete);
                var buttons=new WrapPanel();actionRow.Children.Add(buttons);
                var show=ActionButton(entry.Group==0?"Hide from desktop":"Show on desktop",entry.Group==0?"hide":"open",entry,entry.Group==0?"hide":"show",fg);if(entry.Note.Done&&settings.HideDone)show.ToolTip="Show again, keep completion status and disable auto-hide for this note";buttons.Children.Add(show);
                if(dropdown){var edit=ActionButton("Edit on desktop","edit",entry,"desktop-edit",fg);edit.IsEnabled=!settings.ReadOnly;ToolTipService.SetShowOnDisabled(edit,true);if(settings.ReadOnly)edit.ToolTip="Editing is locked. Unlock it in Note settings.";buttons.Children.Add(edit);}
                buttons.Children.Add(ActionButton("Note settings","settings",entry,"details",fg));
                buttons.Children.Add(ActionButton("Duplicate note","copy",entry,"duplicate",fg));
                buttons.Children.Add(ActionButton(entry.Group==2?"Restore note":"Archive note",entry.Group==2?"restore":"archive",entry,entry.Group==2?"restore":"archive",fg));
                content.Children.Add(actionRow);
            }
            return card;
        }
        Button ActionButton(string label,string icon,NoteEntry entry,string verb,Brush fg){var b=new Button{Tag=verb,Content=Glyph(icon,fg,17),Width=34,Height=32,Padding=new Thickness(7),Margin=new Thickness(0,0,4,0),Foreground=fg,Background=SystemParameters.HighContrast?SystemColors.ControlBrush:new SolidColorBrush(Color.FromArgb(15,60,56,40)),ToolTip=label};AutomationProperties.SetName(b,label+" · "+entry.Title);b.Click+=(s,e)=>Perform(entry,verb);cards[entry.Id].Actions.Add(b);return b;}
        void SetBusy(bool value){busy=value;if(BusyChanged!=null)BusyChanged(value);}
        void Perform(NoteEntry entry,string verb) {
            if(busy)return;SetBusy(true);
            try {
                if(verb=="details"||verb=="desktop-edit"){if(Navigate!=null)Navigate(verb,entry.Id);return;}
                if(verb=="delete") {if(!DeleteNoteUI.ConfirmAndDelete(entry.Id,Window.GetWindow(this)))return;feedback.Text="Note deleted. A pre-deletion backup has been kept locally.";}
                else if(verb=="duplicate"){Engine.DuplicateNote(entry.Id);feedback.Text="An independent copy was created on the desktop.";}
                else {Engine.ManageNote(entry.Id,verb);feedback.Text=verb=="hide"?"Note hidden. Its content has been kept.":verb=="archive"?"Note archived.":"Note shown on the desktop.";}
                Refresh(true);
            } catch(Exception ex){feedback.Text=ex.Message;}
            finally{SetBusy(false);}
        }
        public static FrameworkElement Glyph(string kind,Brush ink,double size=17) {
            string data;
            switch(kind){
                case "open":data="M 8,4 L 4,4 L 4,20 L 20,20 L 20,16 M 12,3 L 21,3 L 21,12 M 21,3 L 11,13";break;
                case "hide":data="M 3,3 L 21,21 M 3,12 Q 12,0 21,12 Q 12,24 3,12 M 9,9 Q 15,9 15,15";break;
                case "edit":data="M 5,16 L 5,20 L 9,20 L 21,8 L 17,4 Z M 14,7 L 18,11";break;
                case "settings":data="M 12,2 L 12,5 M 12,19 L 12,22 M 2,12 L 5,12 M 19,12 L 22,12 M 5,5 L 7,7 M 17,17 L 19,19 M 5,19 L 7,17 M 17,7 L 19,5 M 18,12 A 6,6 0 1 1 6,12 A 6,6 0 1 1 18,12 M 14,12 A 2,2 0 1 1 10,12 A 2,2 0 1 1 14,12";break;
                case "trash":data="M 4,6 L 20,6 M 9,6 L 9,3 L 15,3 L 15,6 M 6,6 L 7,21 L 17,21 L 18,6 M 10,10 L 10,17 M 14,10 L 14,17";break;
                case "archive":data="M 3,4 L 21,4 L 21,8 L 3,8 Z M 5,8 L 5,21 L 19,21 L 19,8 M 9,12 L 15,12";break;
                case "copy":data="M 8,8 L 21,8 L 21,21 L 8,21 Z M 4,16 L 3,16 L 3,3 L 16,3 L 16,4";break;
                case "restore":data="M 8,4 L 3,9 L 8,14 M 4,9 L 15,9 Q 22,9 21,17 Q 20,22 12,21";break;
                case "up":data="M 5,15 L 12,8 L 19,15";break;
                default:data="M 5,9 L 12,16 L 19,9";break;
            }
            var path=new System.Windows.Shapes.Path{Data=Geometry.Parse(data),Stroke=ink,StrokeThickness=1.6,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,Stretch=Stretch.Uniform,Width=size,Height=size,IsHitTestVisible=false};return path;
        }
    }
}
