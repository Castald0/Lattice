using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace Lattice {
 public class WindowRecord {
  public string ProcessName, Title, Monitor;
  public int X,Y,Width,Height,WorkX,WorkY,WorkWidth,WorkHeight;
  public bool Maximized;
 }
 public class Layout { public string Name; public List<WindowRecord> Windows = new List<WindowRecord>(); public override string ToString(){return Name;} }
 public class Library { public List<Layout> Layouts = new List<Layout>(); }
 public class LiveWindow { public IntPtr Handle; public string ProcessName,Title; public override string ToString(){return ProcessName+" — "+Title;} }
 public static class Native {
  public delegate bool EnumProc(IntPtr h, IntPtr p);
  [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
  [StructLayout(LayoutKind.Sequential)] public struct Point { public int X,Y; }
  [StructLayout(LayoutKind.Sequential)] public struct Placement { public int Length,Flags,Show; public Point Min,Max; public Rect Normal; }
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback,IntPtr p);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h,System.Text.StringBuilder text,int count);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
  [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h,uint cmd);
  [DllImport("user32.dll",EntryPoint="GetWindowLongW")] public static extern int GetWindowLong(IntPtr h,int index);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out Rect rect);
  [DllImport("user32.dll")] public static extern bool GetWindowPlacement(IntPtr h,ref Placement p);
  [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr h,int command);
  [DllImport("user32.dll",SetLastError=true)] public static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("shcore.dll")] public static extern int SetProcessDpiAwareness(int value);
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h,int attribute,out int value,int size);
  public static List<LiveWindow> Windows() {
   var result=new List<LiveWindow>(); int own=Process.GetCurrentProcess().Id;
   EnumWindows(delegate(IntPtr h,IntPtr unused){
    if(!IsWindowVisible(h)||GetWindow(h,4)!=IntPtr.Zero||(GetWindowLong(h,-20)&0x80)!=0) return true;
    int cloaked=0; try{DwmGetWindowAttribute(h,14,out cloaked,4);}catch{} if(cloaked!=0)return true;
    uint pid; GetWindowThreadProcessId(h,out pid); if(pid==own)return true;
    var text=new System.Text.StringBuilder(2048); GetWindowText(h,text,text.Capacity); if(text.Length==0)return true;
    try { var process=Process.GetProcessById((int)pid); string name=process.ProcessName; process.Dispose();
     if(name=="explorer" && (text.ToString()=="Program Manager"))return true;
     result.Add(new LiveWindow{Handle=h,ProcessName=name,Title=text.ToString()});
    }catch{} return true;
   },IntPtr.Zero); return result.OrderBy(w=>w.ProcessName).ThenBy(w=>w.Title).ToList();
  }
  public static WindowRecord Capture(LiveWindow w){
   Rect r; if(!GetWindowRect(w.Handle,out r))throw new IOException("Cannot read window: "+w.Title);
   bool zoom=IsZoomed(w.Handle); Screen screen=Screen.FromHandle(w.Handle); Rectangle area=screen.WorkingArea;
   if(zoom||IsIconic(w.Handle)){
    Placement p=new Placement();p.Length=Marshal.SizeOf(typeof(Placement));
    if(GetWindowPlacement(w.Handle,ref p)){
     r=p.Normal;
     // WINDOWPLACEMENT uses workspace coordinates for ordinary top-level windows.
     if((GetWindowLong(w.Handle,-20)&0x80)==0){int dx=area.Left-screen.Bounds.Left,dy=area.Top-screen.Bounds.Top;r.Left+=dx;r.Right+=dx;r.Top+=dy;r.Bottom+=dy;}
     zoom=zoom||(p.Flags&2)!=0;
    }
   }
   return new WindowRecord{ProcessName=w.ProcessName,Title=w.Title,Monitor=screen.DeviceName,X=r.Left,Y=r.Top,Width=r.Right-r.Left,Height=r.Bottom-r.Top,WorkX=area.X,WorkY=area.Y,WorkWidth=area.Width,WorkHeight=area.Height,Maximized=zoom};
  }
  public static Rectangle Target(WindowRecord w,Rectangle area){
   double sx=(double)area.Width/Math.Max(1,w.WorkWidth),sy=(double)area.Height/Math.Max(1,w.WorkHeight);
   int width=Math.Min(area.Width,Math.Max(Math.Min(200,area.Width),(int)Math.Round(w.Width*sx)));
   int height=Math.Min(area.Height,Math.Max(Math.Min(100,area.Height),(int)Math.Round(w.Height*sy)));
   int x=area.X+(int)Math.Round((w.X-w.WorkX)*sx),y=area.Y+(int)Math.Round((w.Y-w.WorkY)*sy);
   return new Rectangle(Math.Max(area.Left,Math.Min(x,area.Right-width)),Math.Max(area.Top,Math.Min(y,area.Bottom-height)),width,height);
  }
  public static bool Move(LiveWindow live,WindowRecord saved){
   if(!IsWindow(live.Handle))return false;
   Screen monitor=Screen.AllScreens.FirstOrDefault(s=>s.DeviceName==saved.Monitor)??Screen.PrimaryScreen;
   Rectangle r=Target(saved,monitor.WorkingArea);
   if(IsZoomed(live.Handle)||IsIconic(live.Handle)){ShowWindowAsync(live.Handle,9);System.Threading.Thread.Sleep(100);}
   bool success=SetWindowPos(live.Handle,IntPtr.Zero,r.X,r.Y,r.Width,r.Height,0x0014|0x0400);
   if(saved.Maximized)ShowWindowAsync(live.Handle,3);
   return success;
  }
 }
 public class Match { public WindowRecord Saved; public LiveWindow Live; }
 public static class Matcher {
  public static List<Match> Resolve(Layout layout,List<LiveWindow> live){
   var result=new List<Match>();var available=new List<LiveWindow>(live);var remaining=new List<WindowRecord>(layout.Windows);
   foreach(var saved in layout.Windows){var exact=available.FirstOrDefault(w=>String.Equals(w.ProcessName,saved.ProcessName,StringComparison.OrdinalIgnoreCase)&&w.Title==saved.Title);if(exact!=null){result.Add(new Match{Saved=saved,Live=exact});available.Remove(exact);remaining.Remove(saved);}}
   // Dynamic titles are safe to match by application only when the match is unambiguous.
   foreach(var saved in remaining){var candidates=available.Where(w=>String.Equals(w.ProcessName,saved.ProcessName,StringComparison.OrdinalIgnoreCase)).ToList();if(candidates.Count==1&&remaining.Count(w=>String.Equals(w.ProcessName,saved.ProcessName,StringComparison.OrdinalIgnoreCase))==1){result.Add(new Match{Saved=saved,Live=candidates[0]});available.Remove(candidates[0]);}}
   return result;
  }
 }
 public class MainForm:Form {
  Library library; readonly string dataFile; ListBox layouts=new ListBox(); CheckedListBox windows=new CheckedListBox();TextBox name=new TextBox();Label status=new Label(); NotifyIcon tray; bool exiting;
  public MainForm(){
   Text="Lattice";MinimumSize=new Size(820,570);Size=new Size(1000,670);StartPosition=FormStartPosition.CenterScreen;Font=new Font("Segoe UI",10);BackColor=Color.FromArgb(244,247,251);
   dataFile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Lattice","layouts.xml");
   library=LoadLibrary();
   var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(22),ColumnCount=1,RowCount=5};root.RowStyles.Add(new RowStyle(SizeType.Absolute,48));root.RowStyles.Add(new RowStyle(SizeType.Absolute,34));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,52));root.RowStyles.Add(new RowStyle(SizeType.Absolute,60));Controls.Add(root);
   root.Controls.Add(new Label{Text="Lattice",Font=new Font("Segoe UI",23,FontStyle.Bold),AutoSize=true},0,0);
   root.Controls.Add(new Label{Text="Save your workspace. Bring it back when you need it.",AutoSize=true,ForeColor=Color.FromArgb(75,88,108)},0,1);
   var split=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,32));split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,68));root.Controls.Add(split,0,2);
   var left=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,Padding=new Padding(0,0,15,0)};left.RowStyles.Add(new RowStyle(SizeType.Absolute,28));left.RowStyles.Add(new RowStyle(SizeType.Percent,100));left.RowStyles.Add(new RowStyle(SizeType.Absolute,48));split.Controls.Add(left,0,0);
   left.Controls.Add(new Label{Text="SAVED LAYOUTS",AutoSize=true},0,0);layouts.Dock=DockStyle.Fill;layouts.BorderStyle=BorderStyle.FixedSingle;left.Controls.Add(layouts,0,1);
   var layoutButtons=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(0,7,0,0)};layoutButtons.Controls.Add(Button("Restore",Restore));layoutButtons.Controls.Add(Button("Delete",Delete));left.Controls.Add(layoutButtons,0,2);
   var right=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3};right.RowStyles.Add(new RowStyle(SizeType.Absolute,28));right.RowStyles.Add(new RowStyle(SizeType.Percent,100));right.RowStyles.Add(new RowStyle(SizeType.Absolute,48));split.Controls.Add(right,1,0);
   right.Controls.Add(new Label{Text="OPEN WINDOWS — check what to save",AutoSize=true},0,0);windows.Dock=DockStyle.Fill;windows.CheckOnClick=true;windows.HorizontalScrollbar=true;windows.BorderStyle=BorderStyle.FixedSingle;right.Controls.Add(windows,0,1);
   var refresh=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(0,7,0,0)};refresh.Controls.Add(Button("Refresh",RefreshWindows));refresh.Controls.Add(Button("Select all",delegate{for(int i=0;i<windows.Items.Count;i++)windows.SetItemChecked(i,true);}));refresh.Controls.Add(Button("Clear",delegate{for(int i=0;i<windows.Items.Count;i++)windows.SetItemChecked(i,false);}));right.Controls.Add(refresh,0,2);
   var saveRow=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(0,10,0,0)};saveRow.Controls.Add(new Label{Text="Layout name",AutoSize=true,Margin=new Padding(0,6,10,0)});name.Width=290;saveRow.Controls.Add(name);saveRow.Controls.Add(Button("Save layout",SaveLayout));saveRow.Controls.Add(Button("Hide to tray",delegate{Hide();}));root.Controls.Add(saveRow,0,3);
   status.Dock=DockStyle.Fill;status.ForeColor=Color.FromArgb(55,72,94);status.Padding=new Padding(0,10,0,0);root.Controls.Add(status,0,4);
   layouts.SelectedIndexChanged+=delegate{Layout l=layouts.SelectedItem as Layout;if(l!=null){name.Text=l.Name;status.Text=l.Windows.Count+" saved windows. Restore moves matching open windows; closed apps stay closed.";}};
   tray=new NotifyIcon{Icon=SystemIcons.Application,Text="Lattice",Visible=true};tray.DoubleClick+=delegate{ShowMain();};RefreshLayouts();RefreshWindows();
   FormClosing+=delegate(object s,FormClosingEventArgs e){if(!exiting&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();tray.ShowBalloonTip(2000,"Lattice","Still available in the system tray. Use its menu to quit.",ToolTipIcon.Info);}};
  }
  Button Button(string text,Action action){var b=new Button{Text=text,AutoSize=true,Height=32,FlatStyle=FlatStyle.System};b.Click+=delegate{try{action();}catch(Exception ex){MessageBox.Show(this,ex.Message,"Lattice",MessageBoxButtons.OK,MessageBoxIcon.Error);}};return b;}
  Library LoadLibrary(){try{if(File.Exists(dataFile)){using(var s=File.OpenRead(dataFile))return (Library)new XmlSerializer(typeof(Library)).Deserialize(s);}}catch(Exception ex){MessageBox.Show("Your saved layouts could not be read. The original file will be kept.\n"+ex.Message,"Lattice");try{File.Copy(dataFile,dataFile+".unreadable-"+DateTime.Now.ToString("yyyyMMddHHmmss"));}catch{throw;}}return new Library();}
  void Persist(){Directory.CreateDirectory(Path.GetDirectoryName(dataFile));string temp=dataFile+".tmp";using(var s=File.Create(temp))new XmlSerializer(typeof(Library)).Serialize(s,library);if(File.Exists(dataFile))File.Replace(temp,dataFile,dataFile+".bak");else File.Move(temp,dataFile);}
  void RefreshLayouts(){string selected=(layouts.SelectedItem as Layout??new Layout()).Name;layouts.Items.Clear();foreach(var l in library.Layouts)layouts.Items.Add(l);if(selected!=null)for(int i=0;i<layouts.Items.Count;i++)if(((Layout)layouts.Items[i]).Name==selected)layouts.SelectedIndex=i;
   var menu=new ContextMenuStrip();menu.Items.Add("Open Lattice",null,delegate{ShowMain();});foreach(var l in library.Layouts){Layout chosen=l;menu.Items.Add("Restore: "+l.Name,null,delegate{try{RestoreLayout(chosen);}catch(Exception ex){MessageBox.Show(ex.Message,"Lattice");}});}menu.Items.Add(new ToolStripSeparator());menu.Items.Add("Quit",null,delegate{exiting=true;Close();});var old=tray.ContextMenuStrip;tray.ContextMenuStrip=menu;if(old!=null)old.Dispose();}
  void ShowMain(){Show();WindowState=FormWindowState.Normal;Activate();}
  void RefreshWindows(){windows.Items.Clear();foreach(var w in Native.Windows())windows.Items.Add(w,true);status.Text=windows.Items.Count+" open windows found. Arrange them, check the ones to include, then save a layout.";}
  void SaveLayout(){string title=name.Text.Trim();if(title.Length==0){MessageBox.Show(this,"Enter a layout name first.");return;}if(windows.CheckedItems.Count==0){MessageBox.Show(this,"Check at least one window to save.");return;}Layout old=library.Layouts.FirstOrDefault(l=>String.Equals(l.Name,title,StringComparison.OrdinalIgnoreCase));if(old!=null&&MessageBox.Show(this,"Replace the saved layout \""+old.Name+"\"?","Save layout",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
   var layout=new Layout{Name=title};foreach(LiveWindow w in windows.CheckedItems)if(Native.IsWindow(w.Handle))layout.Windows.Add(Native.Capture(w));if(layout.Windows.Count==0){MessageBox.Show(this,"Those windows have closed. Refresh and try again.");return;}
   var before=new List<Layout>(library.Layouts);try{if(old!=null)library.Layouts.Remove(old);library.Layouts.Add(layout);Persist();}catch{library.Layouts=before;throw;}RefreshLayouts();layouts.SelectedItem=layout;status.Text="Saved \""+title+"\" with "+layout.Windows.Count+" windows.";
  }
  void Delete(){Layout l=layouts.SelectedItem as Layout;if(l==null)return;if(MessageBox.Show(this,"Delete layout \""+l.Name+"\"?","Delete layout",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;var before=new List<Layout>(library.Layouts);try{library.Layouts.Remove(l);Persist();}catch{library.Layouts=before;throw;}RefreshLayouts();status.Text="Layout deleted.";}
  void Restore(){Layout l=layouts.SelectedItem as Layout;if(l==null){MessageBox.Show(this,"Select a saved layout first.");return;}RestoreLayout(l);}
  void RestoreLayout(Layout layout){var matches=Matcher.Resolve(layout,Native.Windows());int moved=0,failed=0;foreach(var match in matches){if(Native.Move(match.Live,match.Saved))moved++;else failed++;}int missing=layout.Windows.Count-matches.Count;
   string report="\""+layout.Name+"\": requested positions for "+moved+" windows.";if(missing>0)report+=" "+missing+" missing or ambiguous.";if(failed>0)report+=" "+failed+" could not be moved (check app permissions).";status.Text=report;
   if(!Visible)tray.ShowBalloonTip(3500,"Layout restored",report,ToolTipIcon.Info);else if(missing>0||failed>0)MessageBox.Show(this,report+"\n\nIf several windows belong to the same app, their titles must match the saved layout. Elevated apps may require running Lattice as administrator.","Restore results");
  }
  protected override void Dispose(bool disposing){if(disposing&&tray!=null){tray.Visible=false;tray.Dispose();}base.Dispose(disposing);}
 }
 public static class Program {
  [STAThread] public static int Main(string[] args){
   if(args.Contains("--self-test"))return Tests.Run();
   try{Native.SetProcessDpiAwareness(2);}catch{Native.SetProcessDPIAware();}
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   try{Application.Run(new MainForm());return 0;}catch(Exception ex){MessageBox.Show(ex.Message,"Lattice could not start");return 1;}
  }
 }
 public static class Tests {
  static void Assert(bool condition,string label){if(!condition)throw new Exception(label);}
  public static int Run(){try{
   var saved=new WindowRecord{ProcessName="editor",Title="A",X=-1800,Y=100,Width=800,Height=600,WorkX=-1920,WorkY=0,WorkWidth=1920,WorkHeight=1080};
   Assert(Native.Target(saved,new Rectangle(-1920,0,1920,1080))==new Rectangle(-1800,100,800,600),"negative monitor coordinates");
   Rectangle fallback=Native.Target(saved,new Rectangle(0,0,1280,720));Assert(fallback.Left>=0&&fallback.Right<=1280&&fallback.Bottom<=720,"disconnected monitor mapping");
   var l=new Layout{Name="Test",Windows=new List<WindowRecord>{saved}};
   var exact=new LiveWindow{ProcessName="editor",Title="A",Handle=new IntPtr(1)};var other=new LiveWindow{ProcessName="editor",Title="B",Handle=new IntPtr(2)};
   Assert(Matcher.Resolve(l,new List<LiveWindow>{other,exact})[0].Live==exact,"exact title first");
   Assert(Matcher.Resolve(l,new List<LiveWindow>{other}).Count==1,"single dynamic title");
   Assert(Matcher.Resolve(l,new List<LiveWindow>{other,new LiveWindow{ProcessName="editor",Title="C"}}).Count==0,"ambiguous windows skipped");
   l.Windows.Add(new WindowRecord{ProcessName="editor",Title="D"});Assert(Matcher.Resolve(l,new List<LiveWindow>{other}).Count==0,"ambiguous saved entries skipped");
   var serializer=new XmlSerializer(typeof(Library));using(var ms=new MemoryStream()){serializer.Serialize(ms,new Library{Layouts=new List<Layout>{l}});ms.Position=0;Assert(((Library)serializer.Deserialize(ms)).Layouts[0].Windows[0].X==-1800,"layout persistence");}
   File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"self-test-result.txt"),"PASS: monitor coordinates, fallback scaling, exact and dynamic window matching, ambiguity handling, XML persistence.");return 0;
  }catch(Exception ex){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"self-test-result.txt"),"FAIL: "+ex);return 1;}}
 }
}
