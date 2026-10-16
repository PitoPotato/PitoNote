using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace PitoNote {
    public static partial class Engine {
        public static string CatalogConfig {get{return Config+"\\Catalog";}}
        static string ScriptAsset(string name){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("PitoNote."+name))using(var reader=new StreamReader(stream,Encoding.UTF8))return reader.ReadToEnd();}
        public static void WriteCatalogAssets(){
            AtomicWrite(Path.Combine(Resources,"NativeCatalog.lua"),ScriptAsset("NativeCatalog.lua"));
            AtomicWrite(Path.Combine(Resources,"ControlList.lua"),ScriptAsset("ControlList.lua"));
            AtomicWrite(Path.Combine(Root,"Catalog","Catalog.ini"),BuildCatalog());
        }
        static string CatalogEscape(string value){return value.Replace("%","%25").Replace("|","%7C").Replace("\t","%09").Replace("\r","%0D").Replace("\n","%0A");}
        public static void WriteCatalogIndex(Store store){
            var data=new StringBuilder("PNCAT1|"+Guid.NewGuid().ToString("N")+"\n");
            foreach(var note in store.Notes){var t=note.Appearance;data.AppendLine(string.Join("|",new[]{note.Id,note.Pinned?"1":"0",note.Archived?"1":"0",note.Done?"1":"0",t.HideDone?"1":"0",t.ReadOnly?"1":"0",note.Collapsed?"1":"0",t.Background,t.TextColor,CatalogEscape(t.FontName),"1","1"}));}
            AtomicWrite(Path.Combine(Resources,"Catalog.tsv"),data.ToString());
        }
        public static void NotifyCatalog(){Bang("!CommandMeasure CatalogScript Reload() \""+CatalogConfig+"\"");}
        public static void ToggleNativeCatalog(){
            if(Native.SkinHandle(Config+"\\Control")==IntPtr.Zero)throw new IOException("Load the PitoNote toolbar in Rainmeter first.");
            Bang("!CommandMeasure ControlScript ToggleCatalog() \""+Config+"\\Control\"");
        }
        static string CatalogCall(string call){return "[!CommandMeasure CatalogScript \""+call+"\"]";}
        public static string BuildCatalog(){
            var b=new StringBuilder();Section(b,"Rainmeter","AppVersion=5.0.0.5293","SyntaxVersion=2","Update=1000 | Invisible 0","AccurateText=1","DynamicWindowSize=1","DefaultStartHidden=1","DefaultDraggable=0","DefaultAlwaysOnTop=2","DefaultKeepOnScreen=1","OnRefreshAction="+CatalogCall("InitializeView()"),"OnUnfocusAction="+CatalogCall("Unfocus()"),"MouseScrollUpAction="+CatalogCall("Scroll(-1)"),"MouseScrollDownAction="+CatalogCall("Scroll(1)"));
            Section(b,"Metadata","Name=PitoNote native catalog","Version=3.4.0","License=CC BY-NC-SA 4.0");
            var variables=new System.Collections.Generic.List<string>{"RootConfig="+Config,"PanelH=560"};for(int i=1;i<=8;i++)for(int a=1;a<=6;a++)variables.Add("Row"+i+"Ink"+a+"=48,45,38");Section(b,"Variables",variables.ToArray());
            Section(b,"CatalogScript","Measure=Script","ScriptFile=#@#NativeCatalog.lua");
            Section(b,"SearchTimer","Measure=ActionTimer","ActionList1=Repeat SubmitSearch,180,100000","SubmitSearch=[!TextEdit:Submit Search]","UpdateDivider=-1");
            Section(b,"Bounds","Meter=Image","W=380","H=#PanelH#","SolidColor=0,0,0,0","DynamicVariables=1");
            Section(b,"Shadow","Meter=Shape","X=8","Y=11","Shape=Rectangle 0,0,364,(#PanelH#-16),18 | Fill Color 0,0,0,28 | StrokeWidth 0","DynamicVariables=1");
            Section(b,"Paper","Meter=Shape","X=6","Y=6","Shape=Rectangle 0,0,364,(#PanelH#-16),18 | Fill Color 251,250,245 | StrokeWidth 1 | Stroke Color 218,215,204","DynamicVariables=1");
            Section(b,"Brand","Meter=Image","ImageName=#@#Brand\\PitoNote-64.png","X=22","Y=23","W=24","H=24");
            Text(b,"Heading","Notes",55,23,260,28,15,"Segoe UI","52,52,45,255",null,"Bold");
            Icon(b,"Close","close","330","17","100,97,86","Close note list",CatalogCall("Close()"));
            Text(b,"Summary","",22,58,336,22,8.5,"Segoe UI","100,97,86,255");
            Rect(b,"SearchPaper",22,85,336,36,10,"240,239,233,255","218,215,204,255",1);
            Section(b,"Search","Meter=TextEdit","X=33","Y=92","W=281","H=23","FontFace=Segoe UI","FontSize=10","FontColor=54,54,47","PlaceholderText=Search notes…","PlaceholderFontColor=124,121,110","InitialText=","Multiline=0","MaxLength=200","ReadOnly=0","FocusBorderWidth=0","CaretColor=54,54,47","SelectionColor=121,119,106,65","SolidColor=0,0,0,1","AntiAlias=1","OnFocusAction="+CatalogCall("SearchFocus()"),"OnSubmitAction="+CatalogCall("SearchSubmit()"),"OnDismissAction="+CatalogCall("SearchDismiss()"));
            Icon(b,"ClearSearch","close","323","87","119,116,105","Clear search",CatalogCall("ClearSearch()"));
            int x=22;string[] labels={"All","Visible","Hidden","Archived"};
            for(int i=0;i<4;i++){int width=i==1?89:63;Rect(b,"Filter"+i,x,131,width,28,8,"242,240,233,255");SectionAction(b,"Filter"+i,CatalogCall("Filter("+(i-1)+")"));Text(b,"FilterText"+i,labels[i],x+8,137,width-10,20,8.5,"Segoe UI","77,75,64,255",CatalogCall("Filter("+(i-1)+")"));x+=width+6;}
            for(int i=1;i<=8;i++){
                string row="Row"+i,select=CatalogCall("Select("+i+")");
                Text(b,row+"Group","",25,166,330,20,8.5,"Segoe UI","121,118,105,255",null,"Bold");
                Rect(b,row+"Card",22,192,336,61,13,"255,242,178,255","80,70,40,25",1);SectionAction(b,row+"Card",select);
                Text(b,row+"Title","",35,202,284,23,10,"Segoe UI","48,45,38,255",select,"Bold");
                Text(b,row+"Preview","",35,225,290,21,8.5,"Segoe UI","48,45,38,255",select);
                Text(b,row+"Chevron","⌄",328,209,20,24,13,"Segoe UI","48,45,38,255",select);
                for(int a=1;a<=6;a++){
                    string type=new[]{"close","edit","gear","copy","archive","trash"}[a-1];string ink=row+"Ink"+a;
                    Icon(b,row+"A"+a,type,(a==6?313:33+(a-1)*38).ToString(),"256","#"+ink+"#",new[]{"Show / hide","Edit on desktop","Note settings","Duplicate","Archive / restore","Delete note"}[a-1],CatalogCall("Run("+i+","+a+")"));
                }
            }
            Text(b,"Empty","",30,190,320,40,10,"Segoe UI","119,117,106,255");
            Text(b,"Page","",25,486,210,20,8.5,"Segoe UI","119,117,106,255");
            Icon(b,"Previous","up","279","480","100,97,86","Scroll up",CatalogCall("Scroll(-1)"));
            Icon(b,"Next","down","321","480","100,97,86","Scroll down",CatalogCall("Scroll(1)"));
            Text(b,"Feedback","",25,511,320,18,8,"Segoe UI","119,117,106,255");
            Text(b,"Manage","Manage notes  →",25,532,295,20,9,"Segoe UI","105,102,87,255",CatalogCall("Manage()"));
            return b.ToString();
        }
        // Append action options to a just-created meter section, not another INI section.
        static void SectionAction(StringBuilder b,string name,string action){b.AppendLine("LeftMouseUpAction="+action);b.AppendLine("MouseActionCursorName=Hand");}
    }
}
