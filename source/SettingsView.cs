using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace PitoNote {
    public class SettingOption {
        public string Key { get; set; }
        public string Label { get; set; }
        public override string ToString() {return Label;}
    }

    public partial class MainWindow {
        Button applySettings,undoSettings;
        TextBlock settingsState;
        TextBlock paletteCaption;
        TabControl settingsTabs;
        TabItem managementTab;
        NoteListView managementList;
        bool ManagementActive {get{return managementTab!=null&&managementTab.IsSelected;}}

        public void ShowManagement(){if(managementTab!=null){managementTab.IsSelected=true;managementList.Refresh();}}
        void SetManagementScope(bool active){
            if(IsNote)return;
            Title=active?"Manage notes — PitoNote":"General settings — PitoNote";
            pageHeading.Text=active?"Manage notes":"New-note defaults";
            pageSubtitle.Text=active?"Show, hide or archive notes, or open their individual settings.":"Applies only to notes created from now on.";
            scopeCaption.Text=active?"All notes":"Existing notes unchanged";
            status.Text=active?"Select a note to see its actions. Edit content directly on the desktop.":"Each note keeps its own settings from the moment it is created.";
        }
        bool filteringFont;
        readonly Dictionary<string,Button> colorSwatches=new Dictionary<string,Button>();
        readonly Dictionary<string,Button> paletteButtons=new Dictionary<string,Button>();
        readonly Dictionary<string,string> fieldLabels=new Dictionary<string,string>();
        readonly Dictionary<string,double[]> numberRanges=new Dictionary<string,double[]>();

        UIElement BuildSettings() {
            var dock=new DockPanel();
            var footer=new DockPanel{Margin=new Thickness(0,16,0,0)};DockPanel.SetDock(footer,Dock.Bottom);dock.Children.Add(footer);
            var actions=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(actions,Dock.Right);footer.Children.Add(actions);
            undoSettings=Btn("Revert",()=>{changedSettings=false;ReloadFromDisk();});undoSettings.Margin=new Thickness(0,0,8,0);actions.Children.Add(undoSettings);
            applySettings=Btn(IsNote?"Save note settings":"Save defaults",()=>SaveSettings(),true);applySettings.MinWidth=150;applySettings.ToolTip="Ctrl+S";actions.Children.Add(applySettings);
            settingsState=Label("",12,false,"626570");settingsState.VerticalAlignment=VerticalAlignment.Center;footer.Children.Add(settingsState);
            var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(18)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(272)});dock.Children.Add(grid);
            settingsTabs=new TabControl();AutomationProperties.SetName(settingsTabs,"Settings categories");grid.Children.Add(settingsTabs);

            var typography=SettingsPage("Typography");
            var content=Section(typography,"Body","Use any font installed in Windows. Type part of its name to search.");
            AddFont(content,"FontName","Body font");
            var sizeWeight=Columns(content,2);AddNumber(sizeWeight[0],"FontSize","Font size · pt",8,48);AddNumber(sizeWeight[1],"FontWeight","Weight · 100–900",100,900,100);
            var styles=Columns(content,2);AddChoice(styles[0],"FontStyle","Style",new[]{"Normal","Italic"});AddChoice(styles[1],"TextAlign","Alignment",new[]{"Left","Center","Right"});

            var titleOptions=new StackPanel();var titleExpander=new Expander{Header="Title · font & spacing",Content=titleOptions,Padding=new Thickness(16),IsExpanded=false};
            var titleSurface=Surface(titleExpander,0);titleSurface.Margin=new Thickness(0,0,0,12);typography.Children.Add(titleSurface);
            AddCheck(titleOptions,"ShowTitle","Show title");AddFont(titleOptions,"TitleFontName","Title font");
            var titleSizes=Columns(titleOptions,2);AddNumber(titleSizes[0],"TitleSize","Title size · pt",8,48);AddNumber(titleSizes[1],"TitleWeight","Title weight",100,900,100);
            var titleStyles=Columns(titleOptions,2);AddChoice(titleStyles[0],"TitleStyle","Title style",new[]{"Normal","Italic"});AddNumber(titleStyles[1],"TitleGap","Body gap · px",0,40);

            var colors=SettingsPage("Colors");
            var palette=Section(colors,"Palette","Choose a palette, then fine-tune the colors below.");
            var swatches=new WrapPanel{Margin=new Thickness(0,0,0,8)};palette.Children.Add(swatches);
            foreach(var pair in Engine.Themes){
                string key=pair.Key;var swatch=Btn(ThemeLabel(key),()=>((ComboBox)fields["Theme"]).SelectedValue=key);
                swatch.Content=new Border{Width=24,Height=24,CornerRadius=new CornerRadius(12),Background=Brush(pair.Value.Bg),BorderBrush=Brush(pair.Value.Accent),BorderThickness=new Thickness(1)};
                swatch.Width=40;swatch.Height=40;swatch.Padding=new Thickness(6);swatch.Margin=new Thickness(0,0,4,0);swatch.ToolTip=ThemeLabel(key);paletteButtons[key]=swatch;swatches.Children.Add(swatch);
            }
            var theme=new ComboBox{DisplayMemberPath="Label",SelectedValuePath="Key",Visibility=Visibility.Collapsed};
            foreach(string key in Engine.Themes.Keys)theme.Items.Add(new SettingOption{Key=key,Label=ThemeLabel(key)});
            fields["Theme"]=theme;fieldLabels["Theme"]="Palette";palette.Children.Add(theme);paletteCaption=Label("",12,false,"626570");palette.Children.Add(paletteCaption);
            theme.SelectionChanged+=(s,e)=>{
                if(filling)return;string key=GetText("Theme");if(!Engine.Themes.ContainsKey(key))return;
                var temporary=Engine.Clone(savedSettings);Engine.ApplyTheme(temporary,key);
                filling=true;try{foreach(string name in new[]{"Background","TextColor","TitleColor","Accent","BorderColor"})SetText(name,(string)typeof(Settings).GetField(name).GetValue(temporary));}finally{filling=false;}Changed();
            };
            var custom=Section(colors,"Custom colors","Choose a color or enter a HEX value.");
            var row1=Columns(custom,3);AddColor(row1[0],"Background","Background");AddColor(row1[1],"TextColor","Body");AddColor(row1[2],"TitleColor","Title");
            var row2=Columns(custom,2);AddColor(row2[0],"BorderColor","Border");AddColor(row2[1],"Accent","Icons & selection");
            var opacities=Columns(custom,2);AddNumber(opacities[0],"Opacity","Background opacity · /255",30,255);AddNumber(opacities[1],"BorderOpacity","Border opacity · /255",0,255);

            var layout=SettingsPage("Layout");
            var dimensions=Section(layout,"Dimensions",IsNote?"Size of this note only.":"Starting size for notes created from now on.");
            var sizes=Columns(dimensions,2);AddNumber(sizes[0],"Width","Width · px",220,1600);AddNumber(sizes[1],"CardHeight","Height · px",180,1400);
            Hint(dimensions,"You can also drag the bottom-right corner to resize the note on your desktop.");
            var shape=Section(layout,"Shape & spacing","Fine-tune spacing and borders.");
            var shapeRow=Columns(shape,2);AddNumber(shapeRow[0],"Radius","Corner radius · px",0,64);AddNumber(shapeRow[1],"Padding","Padding · px",4,60);
            var borderRow=Columns(shape,2);AddNumber(borderRow[0],"BorderWidth","Border width · px",0,8);AddCheck(borderRow[1],"Shadow","Soft shadow");

            var desktop=SettingsPage("Desktop");
            var behavior=Section(desktop,"Display & interaction",IsNote?"Applies only to this note.":"Starting behavior for new notes.");
            AddChoice(behavior,"ToolbarMode","Note icons",new[]{"Hover","Hidden","Always"});
            AddChoice(behavior,"Position","Window layer",new[]{"Desktop","Normal","Topmost"});
            AddCheck(behavior,"Draggable","Allow dragging");AddCheck(behavior,"ReadOnly","Lock text editing");
            AddCheck(behavior,"HideDone","Hide note when marked complete");
            Hint(behavior,"Hiding icons does not lock editing. Right-click → Note settings to reopen this window.");
            var snapping=Section(desktop,"Snap & align","Align visible paper edges, independent of shadows and note sizes.");
            AddCheck(snapping,"SmartSnap","Smart snapping while dragging");
            var snapOptions=Columns(snapping,2);AddNumber(snapOptions[0],"SnapDistance","Snap distance · px",2,30);AddNumber(snapOptions[1],"SnapGap","Note gap · px",0,80);
            Hint(snapping,"Prefers left/top alignment and even gaps. Hold Ctrl or Shift to place freely. Resizing keeps the top-left corner fixed.");

            if(IsNote) {
                var actionsPage=SettingsPage("Status");
                var state=Section(actionsPage,"This note","Edit note content directly on the desktop.");
                noteState=Label("",13,false,"626570");state.Children.Add(noteState);
                noteDone=Btn("",()=>ChangeNoteState("toggle"));noteVisibility=Btn("",()=>ChangeNoteState("pin"));noteArchive=Btn("",()=>ChangeNoteState("archive"));
                foreach(var button in new[]{noteDone,noteVisibility,noteArchive}){button.HorizontalAlignment=HorizontalAlignment.Left;button.Margin=new Thickness(0,6,0,6);state.Children.Add(button);}
                noteCollapse=Btn("",()=>{Engine.SetCollapsed(editingId);store=Engine.Load();UpdateNoteActions();});
                noteDuplicate=Btn("Duplicate note",()=>{Engine.DuplicateNote(editingId);status.Text="An independent copy was created. The original is unchanged.";});
                foreach(var button in new[]{noteCollapse,noteDuplicate}){button.HorizontalAlignment=HorizontalAlignment.Left;button.Margin=new Thickness(0,6,0,6);state.Children.Add(button);}
                UpdateNoteActions();
                var defaultHint=Section(actionsPage,"Independent of new-note defaults","Changing defaults does not change this note’s colors, fonts or layout.");
                Hint(defaultHint,"You can open settings for several notes at once.");
                var deletion=Section(actionsPage,"Delete note","Hiding keeps a note in your list. Deleting removes it from PitoNote.");
                noteDelete=Btn("Delete note…",()=>{if(DeleteNoteUI.ConfirmAndDelete(editingId,this)){changedSettings=false;Close();}});
                noteDelete.HorizontalAlignment=HorizontalAlignment.Left;noteDelete.Margin=new Thickness(0,6,0,8);
                if(!highContrast){noteDelete.Foreground=Brush("A12430");noteDelete.Background=Brush("FFF0F1");noteDelete.BorderBrush=Brush("E8BFC5");}
                deletion.Children.Add(noteDelete);Hint(deletion,"Deletion always asks for confirmation. Backups are kept in Backups / Deleted.");
            } else {
                var manager=new Grid{Margin=new Thickness(0,8,0,0)};
                manager.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});manager.RowDefinitions.Add(new RowDefinition());
                var hint=Label("Manage note status here. The gear opens each note’s settings.",12,false,"626570");hint.Margin=new Thickness(0,0,0,12);manager.Children.Add(hint);
                managementList=new NoteListView(false);managementList.Navigate+=(verb,id)=>NoteNavigation.Launch(verb,id);Grid.SetRow(managementList,1);manager.Children.Add(managementList);
                managementTab=new TabItem{Header="Manage notes",Padding=new Thickness(12,9,12,9),Content=manager};AutomationProperties.SetName(managementTab,"Manage notes");settingsTabs.Items.Add(managementTab);
                var data=SettingsPage("Data");
                var transfer=Section(data,"Backup & restore","Keep a backup of all notes and new-note defaults.");
                var transferActions=new WrapPanel();transfer.Children.Add(transferActions);
                var export=Btn("Export data…",Export);export.Margin=new Thickness(0,0,8,8);transferActions.Children.Add(export);
                var import=Btn("Import / restore…",Import);import.Margin=new Thickness(0,0,0,8);transferActions.Children.Add(import);
                var backups=Section(data,"Local history","The previous version is backed up before each saved change.");
                var open=Btn("Open backup folder",()=>{string path=Path.Combine(Engine.Resources,"Backups");Directory.CreateDirectory(path);System.Diagnostics.Process.Start(path);});open.HorizontalAlignment=HorizontalAlignment.Left;backups.Children.Add(open);
                var hidden=Btn("Manage all notes",ShowManagement);hidden.HorizontalAlignment=HorizontalAlignment.Left;hidden.Margin=new Thickness(0,10,0,0);backups.Children.Add(hidden);
                Hint(backups,"Notes are stored locally. No account or cloud sync.");
            }

            var right=new StackPanel();Grid.SetColumn(right,2);grid.Children.Add(right);
            var sample=new StackPanel();right.Children.Add(Surface(sample,16));
            sample.Children.Add(Label("PREVIEW",11,true,"626570"));sample.Children.Add(Label(IsNote?"Current note":"A new note",17,true));
            Hint(sample,"Preview changes before saving.");
            preview=new Border{Margin=new Thickness(0,10,0,14),MinHeight=180,MaxHeight=310,ClipToBounds=true};sample.Children.Add(preview);
            var text=new Grid{ClipToBounds=true};text.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});text.RowDefinitions.Add(new RowDefinition());preview.Child=text;
            previewTitle=Label(IsNote&&!string.IsNullOrWhiteSpace(Current().Title)?Current().Title:"Things to remember",17,true);previewTitle.TextWrapping=TextWrapping.NoWrap;previewTitle.TextTrimming=TextTrimming.CharacterEllipsis;text.Children.Add(previewTitle);
            string body=IsNote&&!string.IsNullOrWhiteSpace(Current().Body)?Current().Body:"One small task today,\na little space for tomorrow.\n\nYour ideas belong here.";
            previewBody=Label(body.Length>500?body.Substring(0,500)+"…":body,15);previewBody.TextTrimming=TextTrimming.CharacterEllipsis;previewBody.Margin=new Thickness(0);Grid.SetRow(previewBody,1);text.Children.Add(previewBody);
            previewContrast=Label("",12);sample.Children.Add(previewContrast);info=Label("",12,false,"626570");sample.Children.Add(info);
            var scope=Surface(Label(IsNote?"Saving here changes this note only. Other notes and defaults stay unchanged.":"Saving defaults leaves existing notes unchanged. Only new notes use these settings.",12,false,"626570"),14);scope.Margin=new Thickness(0,12,0,0);right.Children.Add(scope);
            if(!IsNote)settingsTabs.SelectionChanged+=(s,e)=>{
                if(e.Source!=settingsTabs)return;bool active=ManagementActive;
                right.Visibility=active?Visibility.Collapsed:Visibility.Visible;
                grid.ColumnDefinitions[1].Width=new GridLength(active?0:18);grid.ColumnDefinitions[2].Width=new GridLength(active?0:272);
                actions.Visibility=active?Visibility.Collapsed:Visibility.Visible;settingsState.Visibility=active?Visibility.Collapsed:Visibility.Visible;
                footer.Visibility=active?Visibility.Collapsed:Visibility.Visible;SetManagementScope(active);if(active)managementList.Refresh();
            };
            return dock;
        }

        Button noteDone,noteVisibility,noteArchive,noteDelete,noteCollapse,noteDuplicate;
        TextBlock noteState;
        void ChangeNoteState(string action){Engine.ChangeState(editingId,action);store=Engine.Load();UpdateNoteActions();status.Text="Note status updated.";}
        void UpdateNoteActions(){
            if(noteState==null)return;var note=Current();if(note==null)return;
            noteState.Text=note.Archived?"Archived":note.Pinned?"Visible on desktop":"Hidden; content kept";
            noteDone.Content=note.Done?"Mark as incomplete":"Mark as complete";noteVisibility.Content=note.Pinned?"Hide from desktop":"Show on desktop";noteArchive.Content=note.Archived?"Restore note":"Archive note";
            if(noteCollapse!=null){noteCollapse.Content=note.Collapsed?"Expand note":"Collapse to title bar";AutomationProperties.SetName(noteCollapse,noteCollapse.Content.ToString());}
            foreach(var button in new[]{noteDone,noteVisibility,noteArchive})AutomationProperties.SetName(button,button.Content.ToString());
        }
        void AddFont(Panel parent,string key,string label){
            var font=Combo(fonts,label,true);font.IsTextSearchEnabled=false;font.StaysOpenOnEdit=true;fields[key]=font;fieldLabels[key]=label;Field(parent,label,font);
            font.SelectionChanged+=(s,e)=>Changed();
            font.AddHandler(TextBox.TextChangedEvent,new TextChangedEventHandler((s,e)=>{
                if(filling||filteringFont)return;var edit=font.Template.FindName("PART_EditableTextBox",font) as TextBox;
                if(edit!=null&&edit.IsKeyboardFocusWithin){string query=edit.Text;filteringFont=true;try{font.Items.Filter=o=>((string)o).IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0;font.IsDropDownOpen=font.Items.Count>0&&!fonts.Contains(query,StringComparer.OrdinalIgnoreCase);}finally{filteringFont=false;}}
                Changed();
            }));
            font.DropDownClosed+=(s,e)=>{if(!filteringFont){filteringFont=true;try{font.Items.Filter=null;}finally{filteringFont=false;}}};
        }

        StackPanel SettingsPage(string title) {
            var panel=new StackPanel();var scroll=new ScrollViewer {Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(0,0,8,0)};
            var tab=new TabItem {Header=title,Padding=new Thickness(12,9,12,9),Content=scroll};AutomationProperties.SetName(tab,title);settingsTabs.Items.Add(tab);return panel;
        }
        StackPanel Section(Panel parent,string title,string subtitle) {
            var p=new StackPanel();var card=Surface(p);card.Margin=new Thickness(0,0,0,12);parent.Children.Add(card);
            p.Children.Add(Label(title,17,true));var sub=Label(subtitle,12,false,"626570");sub.Margin=new Thickness(0,0,0,16);p.Children.Add(sub);return p;
        }
        void Hint(Panel parent,string text) {var hint=Label(text,12,false,"626570");hint.Margin=new Thickness(0,0,0,12);parent.Children.Add(hint);}
        StackPanel[] Columns(Panel parent,int count) {
            var grid=new Grid();parent.Children.Add(grid);var result=new StackPanel[count];
            for(int i=0;i<count;i++) {
                if(i>0)grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(12)});
                grid.ColumnDefinitions.Add(new ColumnDefinition());result[i]=new StackPanel();Grid.SetColumn(result[i],i*2);grid.Children.Add(result[i]);
            }
            return result;
        }
        static string ThemeLabel(string value) {
            switch(value){case "Yellow":return "Soft yellow";case "Pink":return "Pink";case "Blue":return "Sky blue";case "Green":return "Green";case "Purple":return "Purple";case "Paper":return "Paper";case "Dark":return "Dark";default:return value;}
        }
        static string ChoiceLabel(string value) {
            switch(value){case "Normal":return "Normal";case "Bold":return "Bold";case "Italic":return "Italic";case "BoldItalic":return "Bold italic";
                case "Hover":return "Show on hover";case "Hidden":return "Hide all icons";case "Always":return "Always visible";
                case "Desktop":return "On desktop";case "Topmost":return "Always on top";
                case "Left":return "Left";case "Center":return "Center";case "Right":return "Right";default:return ThemeLabel(value);}
        }
        void AddNumber(Panel parent,string key,string label,int min,int max,int step=1) {
            var tb=Input(label);tb.ToolTip="Value between "+min+" and "+max;tb.VerticalContentAlignment=VerticalAlignment.Center;
            numberRanges[key]=new double[]{min,max};fieldLabels[key]=label;fields[key]=tb;
            var row=new DockPanel();
            var steps=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(6,0,0,0)};DockPanel.SetDock(steps,Dock.Right);row.Children.Add(steps);
            foreach(int amount in new[]{-1,1}) {
                int delta=amount*step;var button=Btn(amount<0?"−":"+",()=>{
                    double n;if(!double.TryParse(tb.Text,NumberStyles.Float,CultureInfo.CurrentCulture,out n))n=min;
                    tb.Text=Math.Max(min,Math.Min(max,n+delta)).ToString(CultureInfo.CurrentCulture);
                });button.MinHeight=36;button.Width=26;button.Padding=new Thickness(0);button.Margin=new Thickness(2,0,0,0);button.ToolTip=(amount<0?"Decrease ":"Increase ")+label;AutomationProperties.SetName(button,button.ToolTip.ToString());steps.Children.Add(button);
            }
            tb.MinWidth=42;row.Children.Add(tb);Field(parent,label,row);tb.TextChanged+=(s,e)=>Changed();
        }
        void AddChoice(Panel parent,string key,string label,IEnumerable<string> options) {
            var combo=new ComboBox {MaxDropDownHeight=270,DisplayMemberPath="Label",SelectedValuePath="Key"};
            foreach(string option in options)combo.Items.Add(new SettingOption{Key=option,Label=ChoiceLabel(option)});
            AutomationProperties.SetName(combo,label);fieldLabels[key]=label;fields[key]=combo;Field(parent,label,combo);combo.SelectionChanged+=(s,e)=>Changed();
        }
        void AddCheck(Panel parent,string key,string label) {
            var check=new CheckBox {Content=new TextBlock{Text=label,TextWrapping=TextWrapping.Wrap},Margin=new Thickness(0,8,0,8),VerticalAlignment=VerticalAlignment.Center};
            fields[key]=check;fieldLabels[key]=label;AutomationProperties.SetName(check,label);parent.Children.Add(check);check.Click+=(s,e)=>Changed();
        }
        void AddColor(Panel parent,string key,string label) {
            var row=new DockPanel();var pick=Btn("Choose color: "+label,()=>PickColor(key));pick.Width=36;pick.MinHeight=38;pick.Padding=new Thickness(5);pick.Margin=new Thickness(0,0,6,0);pick.ToolTip="Choose color: "+label;
            pick.Content=new Border{Width=24,Height=24,CornerRadius=new CornerRadius(5),BorderBrush=Brush("B5B8C2"),BorderThickness=new Thickness(1)};colorSwatches[key]=pick;DockPanel.SetDock(pick,Dock.Left);row.Children.Add(pick);
            var hex=Input(label+" · HEX");hex.MaxLength=6;hex.MinWidth=62;hex.Padding=new Thickness(7,8,5,8);hex.FontFamily=new FontFamily("Consolas");hex.FontSize=13;fields[key]=hex;fieldLabels[key]=label;row.Children.Add(hex);Field(parent,label,row);hex.TextChanged+=(s,e)=>Changed();
        }
        string GetText(string key) {
            var combo=fields[key] as ComboBox;
            if(combo!=null){var option=combo.SelectedItem as SettingOption;return option!=null?option.Key:string.IsNullOrEmpty(combo.SelectedValuePath)?combo.Text:Convert.ToString(combo.SelectedValue,CultureInfo.CurrentCulture);}
            return ((TextBox)fields[key]).Text;
        }
        void SetText(string key,string value) {
            var combo=fields[key] as ComboBox;
            if(combo==null)((TextBox)fields[key]).Text=value;
            else if(!string.IsNullOrEmpty(combo.SelectedValuePath))combo.SelectedValue=value;
            else if(combo.IsEditable)combo.Text=value;
            else combo.SelectedItem=value;
        }
        void FillSettings() {
            filling=true;
            try {
                foreach(var font in fields.Where(p=>p.Key.EndsWith("FontName")).Select(p=>p.Value).OfType<ComboBox>())font.Items.Filter=null;
                foreach(var pair in fields) {
                    var value=typeof(Settings).GetField(pair.Key).GetValue(savedSettings);
                    var check=pair.Value as CheckBox;
                    if(check!=null)check.IsChecked=value is bool?(bool)value:(int)value==1;
                    else SetText(pair.Key,Convert.ToString(value,CultureInfo.CurrentCulture));
                }
                changedSettings=false;
            } finally {filling=false;}
            UpdatePreview();
        }
        InvalidDataException FieldError(string key,string message) {
            var control=fields[key] as Control;
            if(control!=null){if(!highContrast)control.BorderBrush=Brush("B73D48");control.ToolTip=message;}
            return new InvalidDataException(message);
        }
        Settings ReadSettings() {
            var settings=Engine.Clone(savedSettings);
            foreach(var item in fields) {var control=item.Value as Control;if(control!=null){control.ClearValue(Control.BorderBrushProperty);control.ToolTip=numberRanges.ContainsKey(item.Key)?"Value between "+numberRanges[item.Key][0]+" and "+numberRanges[item.Key][1]:null;}}
            foreach(var pair in fields) {
                var property=typeof(Settings).GetField(pair.Key);
                var check=pair.Value as CheckBox;
                if(check!=null){bool value=check.IsChecked==true;property.SetValue(settings,property.FieldType==typeof(bool)?(object)value:value?1:0);continue;}
                string text=GetText(pair.Key).Trim();string label=fieldLabels.ContainsKey(pair.Key)?fieldLabels[pair.Key]:pair.Key;
                if(numberRanges.ContainsKey(pair.Key)) {
                    double n;var range=numberRanges[pair.Key];
                    if(!double.TryParse(text,NumberStyles.Float,CultureInfo.CurrentCulture,out n)||double.IsNaN(n)||double.IsInfinity(n))throw FieldError(pair.Key,label+": enter a valid number.");
                    if(n<range[0]||n>range[1])throw FieldError(pair.Key,label+": enter a value between "+range[0]+" and "+range[1]+".");
                    if(property.FieldType==typeof(int)&&n!=Math.Truncate(n))throw FieldError(pair.Key,label+": enter a whole number.");
                    property.SetValue(settings,property.FieldType==typeof(int)?(object)(int)n:n);
                } else {
                    if(colorSwatches.ContainsKey(pair.Key)&&!System.Text.RegularExpressions.Regex.IsMatch(text,"^[0-9a-fA-F]{6}$"))throw FieldError(pair.Key,label+": enter 6 HEX digits, for example FFF2B2.");
                    if(pair.Key.EndsWith("FontName")&&!fonts.Contains(text,StringComparer.OrdinalIgnoreCase))throw FieldError(pair.Key,"Choose an installed font from the list.");
                    if(text.Length==0)throw FieldError(pair.Key,"Select "+label.ToLower(CultureInfo.CurrentCulture)+".");
                    property.SetValue(settings,text);
                }
            }
            Engine.ValidateOptions(settings);return settings;
        }
        void Changed() {
            if(filling||preview==null)return;
            try {var proposed=ReadSettings();changedSettings=fields.Keys.Any(key=>!object.Equals(typeof(Settings).GetField(key).GetValue(proposed),typeof(Settings).GetField(key).GetValue(savedSettings)));}
            catch {changedSettings=true;}
            UpdatePreview();
        }
        void UpdatePreview() {
            if(preview==null)return;
            foreach(var pair in colorSwatches) {
                try {((Border)pair.Value.Content).Background=Brush(GetText(pair.Key));}catch{}
            }
            try {
                var settings=ReadSettings();
                if(!highContrast) {
                    var background=Brush(settings.Background);background.Opacity=settings.Opacity/255.0;preview.Background=background;var border=Brush(settings.BorderColor);border.Opacity=settings.BorderOpacity/255.0;preview.BorderBrush=border;
                    previewTitle.Foreground=Brush(settings.TitleColor);previewBody.Foreground=Brush(settings.TextColor);
                    preview.Effect=settings.Shadow==1?new DropShadowEffect{BlurRadius=18,Opacity=.12,ShadowDepth=4}:null;
                } else {preview.Background=SystemColors.WindowBrush;preview.BorderBrush=SystemColors.WindowTextBrush;previewTitle.Foreground=SystemColors.WindowTextBrush;previewBody.Foreground=SystemColors.WindowTextBrush;}
                preview.Height=Math.Max(180,Math.Min(310,settings.CardHeight));preview.CornerRadius=new CornerRadius(settings.Radius);preview.BorderThickness=new Thickness(settings.BorderWidth);preview.Padding=new Thickness(settings.Padding);
                previewBody.FontFamily=Engine.ResolveFont(settings.FontName);previewTitle.FontFamily=Engine.ResolveFont(settings.TitleFontName);
                previewBody.FontSize=settings.FontSize*96/72;previewTitle.FontSize=settings.TitleSize*96/72;
                previewBody.FontWeight=FontWeight.FromOpenTypeWeight(settings.FontWeight);previewBody.FontStyle=settings.FontStyle.Contains("Italic")?FontStyles.Italic:FontStyles.Normal;
                previewBody.TextAlignment=settings.TextAlign=="Center"?TextAlignment.Center:settings.TextAlign=="Right"?TextAlignment.Right:TextAlignment.Left;
                previewTitle.FontWeight=FontWeight.FromOpenTypeWeight(settings.TitleWeight);previewTitle.FontStyle=settings.TitleStyle.Contains("Italic")?FontStyles.Italic:FontStyles.Normal;
                previewTitle.Visibility=settings.ShowTitle?Visibility.Visible:Visibility.Collapsed;previewTitle.Margin=new Thickness(0,0,0,settings.TitleGap);
                double contrast=Engine.Contrast(settings.Background,settings.TextColor);
                previewContrast.Text=contrast.ToString("0.0")+":1 · "+(contrast>=4.5?"Good contrast":"Consider increasing contrast");
                if(!highContrast)previewContrast.Foreground=Brush(contrast>=4.5?"34634A":"8B591C");
                info.Text=settings.FontName+" · "+settings.FontSize+" pt\n"+settings.Width+" × "+settings.CardHeight+" px";
                paletteCaption.Text="Selected: "+ThemeLabel(settings.Theme);
                foreach(var pair in paletteButtons)if(!highContrast){pair.Value.BorderBrush=Brush(pair.Key==settings.Theme?"4F5FCC":"FFFFFF");pair.Value.Background=Brush(pair.Key==settings.Theme?"EEF0FF":"FFFFFF");}
                UpdateSettingsState(true,null);
            } catch(Exception ex) {
                previewContrast.Text=ex.Message;if(!highContrast)previewContrast.Foreground=Brush("A3343A");UpdateSettingsState(false,ex.Message);
            }
        }
        bool SaveSettings() {
            try {
                var settings=ReadSettings();if(IsNote)Engine.SaveAppearance(editingId,settings,savedSettings);else Engine.SaveDefaults(settings);
                changedSettings=false;ReloadFromDisk();UpdatePreview();
                status.Text=IsNote?"Note settings saved at "+DateTime.Now.ToString("HH:mm"):"Defaults saved. Existing notes have not changed.";return true;
            } catch(Exception ex) {UpdatePreview();status.Text=ex.Message;return false;}
        }
        void UpdateSettingsState(bool valid,string error) {
            if(applySettings==null)return;
            applySettings.IsEnabled=valid&&changedSettings;undoSettings.IsEnabled=changedSettings;
            settingsState.Text=!valid?"Check the highlighted values":changedSettings?"Unsaved changes":"Up to date • Ctrl+S to save";
            if(!highContrast)settingsState.Foreground=Brush(!valid?"A3343A":changedSettings?"4756B9":"626570");
            settingsState.ToolTip=error;
        }
    }
}
