using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Lattice {
 public static class Targets {
  public static bool Office(string process){return String.Equals(process,"WINWORD",StringComparison.OrdinalIgnoreCase)||String.Equals(process,"EXCEL",StringComparison.OrdinalIgnoreCase);}
  public static bool Link(string target){Uri uri;return !String.IsNullOrWhiteSpace(target)&&!Path.IsPathRooted(target)&&Uri.TryCreate(target,UriKind.Absolute,out uri)&&!uri.IsFile;}
  public static string FilePath(string target){Uri uri;if(Uri.TryCreate(target,UriKind.Absolute,out uri)&&uri.IsFile)return uri.LocalPath;return Environment.ExpandEnvironmentVariables(target??"");}
  public static bool SameFile(string a,string b){if(String.IsNullOrWhiteSpace(a)||String.IsNullOrWhiteSpace(b))return false;try{return String.Equals(Path.GetFullPath(FilePath(a)).TrimEnd('\\'),Path.GetFullPath(FilePath(b)).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase);}catch{return false;}}
  public static bool Matches(WindowRecord saved,LiveWindow live){
   if(String.IsNullOrWhiteSpace(saved.Target))return false;
   if(Office(saved.ProcessName)&&!Link(saved.Target))return SameFile(saved.Target,live.DocumentPath);
   return String.IsNullOrWhiteSpace(saved.TitleHint)||live.Title.IndexOf(saved.TitleHint,StringComparison.OrdinalIgnoreCase)>=0;
  }
  public static void Validate(WindowRecord record){
   if(record.MatchMode!="target")return;
   if(String.IsNullOrWhiteSpace(record.Target))throw new InvalidOperationException("Choose a file or paste an app link for this rule.");
   if(Link(record.Target)){var scheme=new Uri(record.Target).Scheme.ToLowerInvariant();if(!new[]{"http","https","msteams","ms-teams","outlook","ms-outlook","ms-word","ms-excel","onenote"}.Contains(scheme))throw new InvalidOperationException("Use a file path or a supported web, Teams, Outlook, Word, Excel, or OneNote link.");if(Office(record.ProcessName)&&(scheme=="http"||scheme=="https"))throw new InvalidOperationException("For desktop Word/Excel, choose a local or synced file, or use a Word/Excel app link. A plain web link may open in your browser instead.");}
   else{
    if(!Path.IsPathRooted(FilePath(record.Target)))throw new InvalidOperationException("Use the full file path, or choose the file with Browse.");
    if(record.ProcessName.IndexOf("teams",StringComparison.OrdinalIgnoreCase)>=0)throw new InvalidOperationException("For Teams, use a Teams file, chat, or channel link. A local file path opens its registered app instead.");
    if(!Office(record.ProcessName)&&String.IsNullOrWhiteSpace(record.TitleHint))throw new InvalidOperationException("For a specific file in this app, enter title text that identifies its window. Word and Excel files are identified automatically by full path.");
   }
  }
  public static void Launch(WindowRecord record){
   Validate(record);bool link=Link(record.Target);string target=link?record.Target:FilePath(record.Target);
   if(!link&&!File.Exists(target))throw new FileNotFoundException("File not found: "+target);
   ProcessStartInfo start;
   if(!link&&Office(record.ProcessName)&&!String.IsNullOrWhiteSpace(record.ExecutablePath)&&File.Exists(record.ExecutablePath))start=new ProcessStartInfo(record.ExecutablePath,"\""+target.Replace("\"","")+"\""){UseShellExecute=true};
   else start=new ProcessStartInfo(target){UseShellExecute=true};
   using(var process=Process.Start(start)){}
  }
 }
 public static class OfficeDocuments {
  [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent,Native.EnumProc callback,IntPtr data);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h,System.Text.StringBuilder name,int size);
  [DllImport("oleacc.dll")] static extern int AccessibleObjectFromWindow(IntPtr h,uint objectId,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out object result);
  static void Release(object value){if(value!=null&&Marshal.IsComObject(value))try{Marshal.ReleaseComObject(value);}catch{}}
  public static string ReadPath(LiveWindow window){
   if(!Targets.Office(window.ProcessName))return null;
   string wanted=String.Equals(window.ProcessName,"WINWORD",StringComparison.OrdinalIgnoreCase)?"_WwG":"EXCEL7";IntPtr pane=IntPtr.Zero;
   EnumChildWindows(window.Handle,delegate(IntPtr h,IntPtr unused){var name=new System.Text.StringBuilder(100);GetClassName(h,name,name.Capacity);if(name.ToString()==wanted){pane=h;return false;}return true;},IntPtr.Zero);
   if(pane==IntPtr.Zero)return null;object native=null,sheet=null,document=null;
   try{
    Guid dispatch=new Guid("00020400-0000-0000-C000-000000000046");if(AccessibleObjectFromWindow(pane,0xfffffff0,ref dispatch,out native)!=0||native==null)return null;
    if(wanted=="_WwG")document=((dynamic)native).Document;else{sheet=((dynamic)native).ActiveSheet;document=((dynamic)sheet).Parent;}
    string full=(string)((dynamic)document).FullName;return Path.IsPathRooted(full)?full:null;
   }catch{return null;}finally{Release(document);Release(sheet);Release(native);}
  }
  public static async Task<string> ReadPathAsync(LiveWindow live){var work=Task.Run(()=>ReadPath(live));if(await Task.WhenAny(work,Task.Delay(1800))!=work)return null;return await work;}
 }
 public class RestoreOutcome {
  public List<Match> Matches=new List<Match>();public List<LiveWindow> Open=new List<LiveWindow>();public Dictionary<WindowRecord,string> Errors=new Dictionary<WindowRecord,string>();
 }
 public static class RestoreRunner {
  public static async Task<RestoreOutcome> Prepare(Layout layout,Action<string> progress,Action<WindowRecord> launcher=null,Func<List<LiveWindow>> windowProvider=null){
   if(launcher==null)launcher=Targets.Launch;if(windowProvider==null)windowProvider=Native.Windows;
   var outcome=new RestoreOutcome();var probes=new Dictionary<IntPtr,Task<string>>();var launched=new HashSet<WindowRecord>();var deadline=DateTime.UtcNow.AddSeconds(25);bool first=true;
   do{
    outcome.Open=windowProvider();var documentApps=new HashSet<string>(layout.Windows.Where(w=>w.MatchMode=="target"&&Targets.Office(w.ProcessName)&&!Targets.Link(w.Target)).Select(w=>w.ProcessName),StringComparer.OrdinalIgnoreCase);
    var work=new List<Task<string>>();foreach(var live in outcome.Open.Where(w=>documentApps.Contains(w.ProcessName)&&String.IsNullOrEmpty(w.DocumentPath))){Task<string> probe;if(!probes.TryGetValue(live.Handle,out probe)||probe.IsCompleted){probe=Task.Run(()=>OfficeDocuments.ReadPath(live));probes[live.Handle]=probe;}work.Add(probe);}
    if(work.Count>0)await Task.WhenAny(Task.WhenAll(work),Task.Delay(1800));
    foreach(var live in outcome.Open){Task<string> probe;if(probes.TryGetValue(live.Handle,out probe)&&probe.Status==TaskStatus.RanToCompletion)live.DocumentPath=probe.Result;}
    outcome.Matches=Matcher.Resolve(layout,outcome.Open);
    if(first){foreach(var saved in layout.Windows.Where(w=>w.MatchMode=="target"&&w.OpenOnRestore)){
      bool verified=Targets.Office(saved.ProcessName)&&!Targets.Link(saved.Target)&&outcome.Matches.Any(m=>m.Saved==saved);
      if(verified)continue;try{progress("Opening "+saved.Target+" …");launcher(saved);launched.Add(saved);}catch(Exception ex){outcome.Errors[saved]=ex.Message;}
     }first=false;if(launched.Count>0){await Task.Delay(1200);continue;}}
    bool waiting=launched.Any(saved=>!outcome.Matches.Any(m=>m.Saved==saved));if(!waiting)break;progress("Waiting for the requested file or app window …");await Task.Delay(600);
   }while(DateTime.UtcNow<deadline);
   // Never position a generic app window for a target whose launch failed.
   outcome.Matches.RemoveAll(m=>outcome.Errors.ContainsKey(m.Saved));return outcome;
  }
 }
 public class RulesForm:Form {
  public List<WindowRecord> Records;ListBox list=new ListBox();DarkChoice mode=new DarkChoice();TextBox target=new TextBox(),hint=new TextBox();CheckBox launch=new CheckBox(),maximized=new CheckBox();Label app=new Label(),help=new Label();WindowRecord current;bool loading;
  public RulesForm(Layout layout,int selectedIndex=0){
   Records=layout.Windows.Select(w=>w.Copy()).ToList();Text="App and file rules — "+layout.Name;Size=new Size(930,600);MinimumSize=Size;StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",10);MinimizeBox=false;MaximizeBox=false;
   var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),ColumnCount=2};root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,255));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));Controls.Add(root);
   list.Dock=DockStyle.Fill;list.BorderStyle=BorderStyle.FixedSingle;list.HorizontalScrollbar=true;root.Controls.Add(list,0,0);foreach(var record in Records)list.Items.Add(record);
   var right=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(15,0,0,0),RowCount=11};foreach(int height in new[]{36,40,26,38,36,28,36,36,34,70,42})right.RowStyles.Add(new RowStyle(SizeType.Absolute,height));root.Controls.Add(right,1,0);
   app.Dock=DockStyle.Fill;app.Font=new Font(Font,FontStyle.Bold);right.Controls.Add(app,0,0);mode.Name="MatchMode";mode.Dock=DockStyle.Fill;mode.Items.Add("App position — any document or view");mode.Items.Add("Specific file or link");right.Controls.Add(mode,0,1);
   right.Controls.Add(new Label{Text="File path or app link",AutoSize=true},0,2);target.Name="TargetPath";target.Dock=DockStyle.Fill;right.Controls.Add(target,0,3);
   var browseRow=new FlowLayoutPanel{Dock=DockStyle.Fill};browseRow.Controls.Add(MakeButton("Browse file",Browse));browseRow.Controls.Add(MakeButton("Use current Word/Excel file",ReadCurrent));right.Controls.Add(browseRow,0,4);
   right.Controls.Add(new Label{Text="Window title contains (optional for links / other apps)",AutoSize=true},0,5);hint.Dock=DockStyle.Fill;hint.Name="TitleHint";right.Controls.Add(hint,0,6);
   launch.Text="Open this file or link when restoring";launch.AutoSize=true;launch.Name="OpenOnRestore";right.Controls.Add(launch,0,7);
   maximized.Name="RestoreMaximized";maximized.Text="Restore maximized (fill this display)";maximized.AutoSize=true;right.Controls.Add(maximized,0,8);
   help.Text="App position accepts any document. Word/Excel files match by full path. Teams uses app links; Outlook files and other apps use a title hint. Links open through their registered app.";help.Dock=DockStyle.Fill;right.Controls.Add(help,0,9);
   var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};buttons.Controls.Add(MakeButton("Save rules",Save));buttons.Controls.Add(MakeButton("Cancel",delegate{DialogResult=DialogResult.Cancel;Close();}));right.Controls.Add(buttons,0,10);
   list.SelectedIndexChanged+=delegate{StoreCurrent();current=list.SelectedItem as WindowRecord;LoadCurrent();};mode.SelectedIndexChanged+=delegate{if(!loading){bool specific=mode.SelectedIndex==1;target.Enabled=hint.Enabled=launch.Enabled=specific;if(specific&&current!=null&&current.MatchMode!="target")launch.Checked=true;}};
   Theme.Apply(this);help.ForeColor=Theme.Muted;app.ForeColor=Theme.Accent;if(list.Items.Count>0)list.SelectedIndex=Math.Min(selectedIndex,list.Items.Count-1);
   Shown+=delegate{try{int dark=1;Native.DwmSetWindowAttribute(Handle,20,ref dark,4);}catch{}};
  }
  Button MakeButton(string text,Action action){var b=new DarkButton{Text=text,AutoSize=true,Height=32};b.Click+=delegate{try{action();}catch(Exception ex){Dialogs.Show(this,ex.Message,"App and file rules");}};return b;}
  void StoreCurrent(){if(current==null||loading)return;current.MatchMode=mode.SelectedIndex==1?"target":"app";current.Target=target.Text.Trim();current.TitleHint=hint.Text.Trim();current.OpenOnRestore=launch.Checked&&current.MatchMode=="target";current.Maximized=maximized.Checked;}
  void LoadCurrent(){if(current==null)return;loading=true;try{app.Text=AppNames.Friendly(current.ProcessName)+" position";mode.SelectedIndex=current.MatchMode=="target"?1:0;target.Text=current.Target;hint.Text=current.TitleHint;launch.Checked=current.OpenOnRestore;maximized.Checked=current.Maximized;target.Enabled=hint.Enabled=launch.Enabled=mode.SelectedIndex==1;}finally{loading=false;}}
  void Browse(){using(var dialog=new OpenFileDialog{Title="Choose the file for this saved position",CheckFileExists=true,Filter="Documents and workbooks|*.docx;*.doc;*.docm;*.xlsx;*.xls;*.xlsm;*.xlsb;*.csv;*.pdf;*.msg;*.ics|All files|*.*"})if(dialog.ShowDialog(this)==DialogResult.OK){mode.SelectedIndex=1;target.Text=dialog.FileName;if(current!=null&&!Targets.Office(current.ProcessName)&&String.IsNullOrWhiteSpace(hint.Text))hint.Text=Path.GetFileNameWithoutExtension(dialog.FileName);}}
  async void ReadCurrent(){if(current==null)return;var selected=current;var match=Matcher.Resolve(new Layout{Windows=new List<WindowRecord>{new WindowRecord{ProcessName=current.ProcessName,WindowHandle=current.WindowHandle,ProcessId=current.ProcessId,ProcessStartUtcTicks=current.ProcessStartUtcTicks,Title=current.Title}}},Native.Windows()).FirstOrDefault();string path=match==null?null:await OfficeDocuments.ReadPathAsync(match.Live);if(IsDisposed||current!=selected)return;if(String.IsNullOrWhiteSpace(path)){Dialogs.Show(this,"Couldn't read a saved Word or Excel file from that window. Use Browse, or save the document in Office first.");return;}mode.SelectedIndex=1;target.Text=path;}
  void Save(){StoreCurrent();foreach(var record in Records)Targets.Validate(record);DialogResult=DialogResult.OK;Close();}
 }
 public static class RuleTests {
  static void Check(bool value,string message){if(!value)throw new Exception(message);}
  public static void Run(){
   var any=new WindowRecord{ProcessName="WINWORD",Title="Yesterday's document"};var specific=new WindowRecord{ProcessName="WINWORD",MatchMode="target",Target=@"C:\Work\Budget.docx",OpenOnRestore=true};
   var budget=new LiveWindow{ProcessName="WINWORD",Title="Budget",DocumentPath=@"C:\Work\Budget.docx",Handle=new IntPtr(40)};var different=new LiveWindow{ProcessName="WINWORD",Title="Budget",DocumentPath=@"C:\Elsewhere\Budget.docx",Handle=new IntPtr(41)};
   var layout=new Layout{Windows=new List<WindowRecord>{any,specific}};var matches=Matcher.Resolve(layout,new List<LiveWindow>{budget,different});Check(matches.Single(m=>m.Saved==specific).Live==budget&&matches.Single(m=>m.Saved==any).Live==different,"specific file reserves its window before app position");
   Check(Matcher.Resolve(new Layout{Windows=new List<WindowRecord>{specific}},new List<LiveWindow>{different}).Count==0,"same filename in another folder is not the requested file");
   budget.DocumentPath=null;Check(!Targets.Matches(specific,budget),"unverified Office document does not match by title alone");budget.DocumentPath=specific.Target;
   int launches=0;var outcome=RestoreRunner.Prepare(new Layout{Windows=new List<WindowRecord>{specific}},delegate{},delegate{launches++;},()=>new List<LiveWindow>{budget}).GetAwaiter().GetResult();Check(launches==0&&outcome.Matches.Count==1,"already-open verified document is not reopened");
   bool opened=false;outcome=RestoreRunner.Prepare(new Layout{Windows=new List<WindowRecord>{specific}},delegate{},delegate{launches++;opened=true;},()=>opened?new List<LiveWindow>{budget}:new List<LiveWindow>()).GetAwaiter().GetResult();Check(launches==1&&outcome.Matches.Count==1,"missing file is launched once then matched");
   outcome=RestoreRunner.Prepare(new Layout{Windows=new List<WindowRecord>{specific}},delegate{},delegate{throw new IOException("Test launch failure");},()=>new List<LiveWindow>()).GetAwaiter().GetResult();Check(outcome.Errors.Count==1&&outcome.Matches.Count==0,"failed launch is reported without selecting another file");
   var link=new WindowRecord{ProcessName="ms-teams",MatchMode="target",Target="https://teams.microsoft.com/l/channel/example",TitleHint="Research",OpenOnRestore=true};Check(Targets.Matches(link,new LiveWindow{Title="Research | Teams"})&&!Targets.Matches(link,new LiveWindow{Title="Other | Teams"}),"link title hint narrows the target window");
  }
 }
}
