using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
namespace PitoNote {
 public static class PopupChecks {
  public static int Run(string root){
   var report=new StringBuilder();int count=0;Directory.CreateDirectory(root);
   try{
    Engine.Root=root;var store=Engine.Load();if(store.Notes.Count==0){store.Notes.Add(Engine.NewNote(store.Settings));Engine.Save(store,false);}
    var app=new Application();
    foreach(string id in new[]{(string)null,store.Notes[0].Id}){
     var window=new MainWindow(store,id);window.CheckPopup((ok,name)=>{if(!ok)throw new Exception("FAIL: "+name);count++;report.AppendLine("PASS: "+(id==null?"Defaults: ":"Per-note: ")+name);});
    }
    report.AppendLine(count+" popup checks passed.");File.WriteAllText(Path.Combine(root,"popup-checks.txt"),report.ToString());app.Shutdown();return 0;
   }catch(Exception ex){report.AppendLine(ex.ToString());File.WriteAllText(Path.Combine(root,"popup-checks.txt"),report.ToString());return 1;}
  }
 }
 public partial class MainWindow {
  internal void CheckPopup(Action<bool,string> verify){
   string original=Engine.Json(store),originalFile=File.ReadAllText(Engine.DataPath);
   verify(Engine.Json(new Store{Settings=ReadSettings()})==Engine.Json(new Store{Settings=savedSettings}),"Controls preserve existing values");
   verify(settingsTabs.Items.Count==(IsNote?5:6),"Focused categories; manager belongs only to general settings");
   verify(IsNote?managementTab==null:managementList!=null&&!managementList.OffersContentEditing,"Manager has no note content editing controls");
   verify(fields.Count==30&&!fields.ContainsKey("Title")&&!fields.ContainsKey("Body"),"30 appearance options, no popup content editor");
   verify(Title.Contains("PitoNote")&&!Title.Contains("Notes++"),"Window uses the PitoNote brand");
   verify(IsNote?noteDelete!=null&&noteDelete.Content.ToString()=="Delete note…":noteDelete==null,"Delete is explicit and belongs only to an individual note");
   verify(!changedSettings&&!applySettings.IsEnabled&&!undoSettings.IsEnabled,"No pending edits at startup");
   foreach(var size in new[]{new Size(1000,600),new Size(852,460)})foreach(TabItem page in settingsTabs.Items){
    page.IsSelected=true;var root=(FrameworkElement)Content;root.Measure(size);root.Arrange(new Rect(size));root.UpdateLayout();
    verify(page==managementTab?managementList.ActualHeight>150&&((FrameworkElement)applySettings.Parent).Visibility==Visibility.Collapsed:applySettings.ActualWidth>=150,"Useful layout in "+page.Header+" at "+size.Width+"px");
   }
   foreach(string key in Engine.Themes.Keys){SetText("Theme",key);var value=ReadSettings();verify(value.Background==Engine.Themes[key].Bg&&value.TitleColor==Engine.Themes[key].Text,"Palette mapping "+key);}
   FillSettings();((CheckBox)fields["ReadOnly"]).IsChecked=true;Changed();verify(ReadSettings().ReadOnly,"Native input lock can be set");
   FillSettings();SetText("Radius","65");bool rejected=false;try{ReadSettings();}catch(InvalidDataException){rejected=true;}verify(rejected&&!applySettings.IsEnabled,"Invalid geometry blocked inline");
   FillSettings();SetText("Background","XYZ");rejected=false;try{ReadSettings();}catch(InvalidDataException){rejected=true;}verify(rejected&&!applySettings.IsEnabled,"Invalid HEX blocked inline");
   FillSettings();SetText("FontName","Segoe UI");verify(ReadSettings().FontName=="Segoe UI"&&previewBody.FontFamily.FamilyNames.Values.Contains("Segoe UI"),"Installed Windows font previews correctly");
   verify(settingsTabs.Items.Cast<TabItem>().All(tab=>new[]{"Typography","Colors","Layout","Desktop","Status","Manage notes","Data"}.Contains(tab.Header.ToString())),"Every settings category is English");
   verify(Title==(IsNote?"Note settings — PitoNote":"General settings — PitoNote"),"Window title is English in the active scope");
   FillSettings();SetText("TitleFontName","Not-An-Installed-Font");rejected=false;try{ReadSettings();}catch(InvalidDataException){rejected=true;}verify(rejected,"Invalid title font rejected");
   FillSettings();verify(!changedSettings&&!applySettings.IsEnabled,"Undo restores saved settings");
   verify(Engine.Json(store)==original&&File.ReadAllText(Engine.DataPath)==originalFile,"Preview never mutates saved data");
   settingsTabs.SelectedIndex=0;
   ManagementChecks.Render((FrameworkElement)Content,1000,600,Path.Combine(Engine.Root,IsNote?"note-settings-preview.png":"general-settings-preview.png"));
  }
 }
}
