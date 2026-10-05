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
 public class Library { public List<Layout> Layouts = new List<Layout>(); public bool DragSnapEnabled; public string DragPreset="halves"; }
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
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h,uint message,IntPtr wParam,IntPtr lParam);
  [DllImport("shcore.dll")] public static extern int SetProcessDpiAwareness(int value);
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h,int attribute,out int value,int size);
  [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr h,int attribute,ref int value,int size);
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
   return Place(live,r,saved.Maximized);
  }
  public static bool Place(LiveWindow live,Rectangle r,bool maximized){
   if(!IsWindow(live.Handle))return false;
   if(IsZoomed(live.Handle)||IsIconic(live.Handle)){ShowWindowAsync(live.Handle,9);System.Threading.Thread.Sleep(100);}
   bool success=SetWindowPos(live.Handle,IntPtr.Zero,r.X,r.Y,r.Width,r.Height,0x0014|0x0400);
   if(maximized)ShowWindowAsync(live.Handle,3);
   return success;
  }
 }
 public static class Geometry {
  public static Rectangle Zone(Rectangle area,int col,int row,int cols,int rows){
   int left=area.Left+area.Width*col/cols,top=area.Top+area.Height*row/rows;
   return Rectangle.FromLTRB(left,top,area.Left+area.Width*(col+1)/cols,area.Top+area.Height*(row+1)/rows);
  }
  public static Rectangle Center(Rectangle area,Size size){int w=Math.Min(area.Width,size.Width),h=Math.Min(area.Height,size.Height);return new Rectangle(area.Left+(area.Width-w)/2,area.Top+(area.Height-h)/2,w,h);}
 }
 public class MonitorChoice {
  public Screen Screen;public int Number;
  public override string ToString(){return "Display "+Number+(Screen.Primary?" (primary)":"")+"  ·  "+Screen.WorkingArea.Width+" × "+Screen.WorkingArea.Height;}
 }
 public static class Theme {
  public static readonly Color Background=Color.FromArgb(18,22,30),Panel=Color.FromArgb(27,33,44),Field=Color.FromArgb(34,42,55),Text=Color.FromArgb(232,237,246),Muted=Color.FromArgb(158,174,195),Border=Color.FromArgb(60,74,94),Accent=Color.FromArgb(112,207,220);
  public static void Apply(Control c){
   c.ForeColor=Text;c.BackColor=Background;
   if(c is TextBoxBase||c is ListBox||c is ComboBox||c is NumericUpDown)c.BackColor=Field;
   var b=c as Button;if(b!=null){b.FlatStyle=FlatStyle.Flat;b.BackColor=Panel;b.FlatAppearance.BorderColor=Border;b.FlatAppearance.MouseOverBackColor=Color.FromArgb(44,60,76);b.FlatAppearance.MouseDownBackColor=Color.FromArgb(50,83,96);b.Cursor=Cursors.Hand;}
   var combo=c as ComboBox;if(combo!=null)combo.FlatStyle=FlatStyle.Flat;
   foreach(Control child in c.Controls)Apply(child);
  }
 }
 public class DarkButton:Button {
  bool hover;
  public DarkButton(){FlatStyle=FlatStyle.Flat;UseVisualStyleBackColor=false;}
  protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
  protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnPaint(PaintEventArgs e){
   using(var fill=new SolidBrush(hover&&Enabled?Theme.Field:Theme.Panel))e.Graphics.FillRectangle(fill,ClientRectangle);
   using(var border=new Pen(Focused?Theme.Accent:Theme.Border))e.Graphics.DrawRectangle(border,0,0,Width-1,Height-1);
   TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled?Theme.Text:Theme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
  }
 }
 public class DarkChoice:DarkButton {
  public List<object> Items=new List<object>();int selected=-1;
  public event EventHandler SelectedIndexChanged;
  public int SelectedIndex{get{return selected;}set{selected=value;Text=(SelectedItem==null?"Choose a window first":SelectedItem.ToString())+"  ▾";Invalidate();if(SelectedIndexChanged!=null)SelectedIndexChanged(this,EventArgs.Empty);}}
  public object SelectedItem{get{return selected>=0&&selected<Items.Count?Items[selected]:null;}set{SelectedIndex=Items.IndexOf(value);}}
  protected override void OnClick(EventArgs e){base.OnClick(e);var menu=new ContextMenuStrip{BackColor=Theme.Panel,ForeColor=Theme.Text,Renderer=new ToolStripProfessionalRenderer(new DarkMenuColors())};for(int i=0;i<Items.Count;i++){int index=i;menu.Items.Add(new ToolStripMenuItem(Items[i].ToString(),null,delegate{SelectedIndex=index;}){Checked=index==selected});}menu.Closed+=delegate{menu.Dispose();};if(menu.Items.Count>0)menu.Show(this,new Point(0,Height));else menu.Dispose();}
 }
 public class SnapChoice {public string Key,Label;public override string ToString(){return Label;}}
 public class NumberField:TextBox {
  public int Minimum,Maximum;
  public int Value{get{int value;if(!Int32.TryParse(Text,out value)||value<Minimum||value>Maximum)throw new InvalidOperationException("Enter a whole number between "+Minimum+" and "+Maximum+" for "+Name.Replace("Position","")+".");return value;}set{Text=value.ToString();}}
  public NumberField(){BorderStyle=BorderStyle.FixedSingle;TextAlign=HorizontalAlignment.Right;}
 }
 public static class Dialogs {
  public static DialogResult Show(string text,string title){return Show(null,text,title);}
  public static DialogResult Show(IWin32Window owner,string text,string title="Lattice",MessageBoxButtons buttons=MessageBoxButtons.OK,MessageBoxIcon icon=MessageBoxIcon.None){
   using(var form=new Form{Text=title,Size=new Size(560,290),StartPosition=owner==null?FormStartPosition.CenterScreen:FormStartPosition.CenterParent,MinimizeBox=false,MaximizeBox=false,FormBorderStyle=FormBorderStyle.FixedDialog,Font=new Font("Segoe UI",10)}){
    var body=new TextBox{Text=text,Multiline=true,ReadOnly=true,BorderStyle=BorderStyle.None,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,Margin=new Padding(20)};
    var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),RowCount=2};root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,44));form.Controls.Add(root);root.Controls.Add(body,0,0);
    var row=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};root.Controls.Add(row,0,1);
    var yes=new DarkButton{Text=buttons==MessageBoxButtons.YesNo?"Yes":"OK",DialogResult=buttons==MessageBoxButtons.YesNo?DialogResult.Yes:DialogResult.OK,Size=new Size(90,32)};row.Controls.Add(yes);form.AcceptButton=yes;
    if(buttons==MessageBoxButtons.YesNo){var no=new DarkButton{Text="No",DialogResult=DialogResult.No,Size=new Size(90,32)};row.Controls.Add(no);form.CancelButton=no;}else form.CancelButton=yes;
    Theme.Apply(form);body.BackColor=Theme.Background;form.Shown+=delegate{try{int dark=1;Native.DwmSetWindowAttribute(form.Handle,20,ref dark,4);}catch{}};
    return owner==null?form.ShowDialog():form.ShowDialog(owner);
   }
  }
 }
 public class DarkMenuColors:ProfessionalColorTable {
  public override Color ToolStripDropDownBackground{get{return Theme.Panel;}}
  public override Color ImageMarginGradientBegin{get{return Theme.Panel;}}
  public override Color ImageMarginGradientMiddle{get{return Theme.Panel;}}
  public override Color ImageMarginGradientEnd{get{return Theme.Panel;}}
  public override Color MenuItemSelected{get{return Theme.Field;}}
  public override Color MenuItemBorder{get{return Theme.Border;}}
  public override Color MenuBorder{get{return Theme.Border;}}
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
  DarkChoice monitor=new DarkChoice(),snapPreset=new DarkChoice();CheckBox dragEnabled=new CheckBox();DragSnapper dragSnapper;bool refreshingSnap;
  Label selectedTitle=new Label();NumberField posX=new NumberField(),posY=new NumberField(),sizeW=new NumberField(),sizeH=new NumberField();Control positionPanel;LiveWindow undoWindow;WindowRecord undoRecord;Timer settle=new Timer{Interval=350};
  public MainForm():this(null){}
  public MainForm(string libraryPath){
   Text="Lattice · Arrange your workspace";MinimumSize=new Size(960,790);Size=new Size(1080,920);StartPosition=FormStartPosition.CenterScreen;Font=new Font("Segoe UI",10);BackColor=Theme.Background;
   dataFile=libraryPath??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Lattice","layouts.xml");
   library=LoadLibrary();
   var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(22),ColumnCount=1,RowCount=7};root.RowStyles.Add(new RowStyle(SizeType.Absolute,48));root.RowStyles.Add(new RowStyle(SizeType.Absolute,34));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,244));root.RowStyles.Add(new RowStyle(SizeType.Absolute,70));root.RowStyles.Add(new RowStyle(SizeType.Absolute,52));root.RowStyles.Add(new RowStyle(SizeType.Absolute,60));Controls.Add(root);
   root.Controls.Add(new Label{Text="Lattice",Font=new Font("Segoe UI",23,FontStyle.Bold),AutoSize=true},0,0);
   root.Controls.Add(new Label{Text="Position your windows. Save your workspace. Bring it back.",AutoSize=true},0,1);
   var split=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,32));split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,68));root.Controls.Add(split,0,2);
   var left=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,Padding=new Padding(0,0,15,0)};left.RowStyles.Add(new RowStyle(SizeType.Absolute,28));left.RowStyles.Add(new RowStyle(SizeType.Percent,100));left.RowStyles.Add(new RowStyle(SizeType.Absolute,48));split.Controls.Add(left,0,0);
   left.Controls.Add(new Label{Text="SAVED LAYOUTS",AutoSize=true},0,0);layouts.Dock=DockStyle.Fill;layouts.BorderStyle=BorderStyle.FixedSingle;left.Controls.Add(layouts,0,1);
   var layoutButtons=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(0,7,0,0)};layoutButtons.Controls.Add(Button("Restore",Restore));layoutButtons.Controls.Add(Button("Delete",Delete));left.Controls.Add(layoutButtons,0,2);
   var right=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3};right.RowStyles.Add(new RowStyle(SizeType.Absolute,28));right.RowStyles.Add(new RowStyle(SizeType.Percent,100));right.RowStyles.Add(new RowStyle(SizeType.Absolute,48));split.Controls.Add(right,1,0);
   right.Controls.Add(new Label{Text="SELECT A ROW TO POSITION · Check windows to save",AutoSize=true},0,0);windows.Name="WindowList";windows.Dock=DockStyle.Fill;windows.CheckOnClick=false;windows.HorizontalScrollbar=true;windows.BorderStyle=BorderStyle.FixedSingle;right.Controls.Add(windows,0,1);windows.SelectedIndexChanged+=delegate{ReadSelected();};
   var refresh=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(0,7,0,0)};refresh.Controls.Add(Button("Refresh",RefreshWindows));refresh.Controls.Add(Button("Select all",delegate{for(int i=0;i<windows.Items.Count;i++)windows.SetItemChecked(i,true);}));refresh.Controls.Add(Button("Clear",delegate{for(int i=0;i<windows.Items.Count;i++)windows.SetItemChecked(i,false);}));right.Controls.Add(refresh,0,2);
   positionPanel=BuildPositionPanel();root.Controls.Add(positionPanel,0,3);
   var snapRow=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2};snapRow.RowStyles.Add(new RowStyle(SizeType.Absolute,39));snapRow.RowStyles.Add(new RowStyle(SizeType.Percent,100));var snapControls=new FlowLayoutPanel{Dock=DockStyle.Fill};dragEnabled.Name="EnableDragSnap";dragEnabled.Text="Snap while dragging";dragEnabled.AutoSize=true;dragEnabled.Margin=new Padding(0,8,15,0);snapControls.Controls.Add(dragEnabled);snapPreset.Name="DragPreset";snapPreset.Width=330;snapPreset.Height=32;snapControls.Controls.Add(snapPreset);snapControls.Controls.Add(Button("Use selected layout",UseLayoutForDragging));snapRow.Controls.Add(snapControls,0,0);snapRow.Controls.Add(new Label{Text="Drag a title bar to highlight a zone. Release to snap. Escape or right-click bypasses snapping for that drag.",Dock=DockStyle.Fill},0,1);root.Controls.Add(snapRow,0,4);
   var saveRow=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(0,10,0,0)};saveRow.Controls.Add(new Label{Text="Layout name",AutoSize=true,Margin=new Padding(0,6,10,0)});name.Name="LayoutName";name.Width=290;saveRow.Controls.Add(name);saveRow.Controls.Add(Button("Save layout",SaveLayout));saveRow.Controls.Add(Button("Hide to tray",delegate{Hide();}));root.Controls.Add(saveRow,0,5);
   status.Dock=DockStyle.Fill;status.Padding=new Padding(0,10,0,0);root.Controls.Add(status,0,6);
   layouts.SelectedIndexChanged+=delegate{Layout l=layouts.SelectedItem as Layout;if(l!=null){name.Text=l.Name;status.Text=l.Windows.Count+" saved windows. Restore moves matching open windows; closed apps stay closed.";}};
   tray=new NotifyIcon{Icon=SystemIcons.Application,Text="Lattice",Visible=true};tray.DoubleClick+=delegate{ShowMain();};RefreshLayouts();RefreshWindows();
   Theme.Apply(this);status.ForeColor=Theme.Muted;selectedTitle.ForeColor=Theme.Accent;
   dragSnapper=new DragSnapper(GetDragZones,delegate(string message){status.Text=message;});
   dragEnabled.Checked=library.DragSnapEnabled;dragEnabled.CheckedChanged+=delegate{UpdateDragSettings();};snapPreset.SelectedIndexChanged+=delegate{if(!refreshingSnap)UpdateDragSettings();};
   try{dragSnapper.SetEnabled(library.DragSnapEnabled);}catch(Exception ex){dragEnabled.Checked=false;status.Text=ex.Message;}
   settle.Tick+=delegate{settle.Stop();ReadSelected();};
   FormClosing+=delegate(object s,FormClosingEventArgs e){if(!exiting&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();tray.ShowBalloonTip(2000,"Lattice","Still available in the system tray. Use its menu to quit.",ToolTipIcon.Info);}};
  }
  protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);try{int dark=1;if(Native.DwmSetWindowAttribute(Handle,20,ref dark,4)!=0)Native.DwmSetWindowAttribute(Handle,19,ref dark,4);}catch{}}
  protected override void WndProc(ref Message message){if(message.Msg==0x8001){ShowMain();return;}base.WndProc(ref message);}
  Control BuildPositionPanel(){
   var group=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,Padding=new Padding(0,8,0,0)};
   group.RowStyles.Add(new RowStyle(SizeType.Absolute,28));group.RowStyles.Add(new RowStyle(SizeType.Absolute,37));group.RowStyles.Add(new RowStyle(SizeType.Absolute,40));group.RowStyles.Add(new RowStyle(SizeType.Absolute,40));group.RowStyles.Add(new RowStyle(SizeType.Absolute,42));group.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   selectedTitle.Text="Select an open window above to position it";selectedTitle.AutoEllipsis=true;selectedTitle.Dock=DockStyle.Fill;selectedTitle.Font=new Font(Font,FontStyle.Bold);group.Controls.Add(selectedTitle,0,0);
   var displayRow=new FlowLayoutPanel{Dock=DockStyle.Fill};displayRow.Controls.Add(new Label{Text="Place on",AutoSize=true,Margin=new Padding(0,5,12,0)});monitor.Name="TargetMonitor";monitor.Width=310;monitor.Height=32;displayRow.Controls.Add(monitor);displayRow.Controls.Add(Button("Move to display",MoveToMonitor));displayRow.Controls.Add(Button("Undo last move",UndoMove));group.Controls.Add(displayRow,0,1);
   var halves=new FlowLayoutPanel{Dock=DockStyle.Fill};halves.Controls.Add(Button("Left half",delegate{Snap(0,0,2,1);}));halves.Controls.Add(Button("Right half",delegate{Snap(1,0,2,1);}));halves.Controls.Add(Button("Top half",delegate{Snap(0,0,1,2);}));halves.Controls.Add(Button("Bottom half",delegate{Snap(0,1,1,2);}));halves.Controls.Add(Button("Center",CenterWindow));halves.Controls.Add(Button("Maximize",delegate{ApplyPosition(TargetScreen().WorkingArea,true);}));group.Controls.Add(halves,0,2);
   var quarters=new FlowLayoutPanel{Dock=DockStyle.Fill};quarters.Controls.Add(Button("Top left",delegate{Snap(0,0,2,2);}));quarters.Controls.Add(Button("Top right",delegate{Snap(1,0,2,2);}));quarters.Controls.Add(Button("Bottom left",delegate{Snap(0,1,2,2);}));quarters.Controls.Add(Button("Bottom right",delegate{Snap(1,1,2,2);}));quarters.Controls.Add(Button("Left third",delegate{Snap(0,0,3,1);}));quarters.Controls.Add(Button("Middle third",delegate{Snap(1,0,3,1);}));quarters.Controls.Add(Button("Right third",delegate{Snap(2,0,3,1);}));group.Controls.Add(quarters,0,3);
   var exact=new FlowLayoutPanel{Dock=DockStyle.Fill};AddNumber(exact,"X",posX,-100000,100000);AddNumber(exact,"Y",posY,-100000,100000);AddNumber(exact,"Width",sizeW,1,100000);AddNumber(exact,"Height",sizeH,1,100000);exact.Controls.Add(Button("Apply position",ApplyCustom));group.Controls.Add(exact,0,4);
   group.Controls.Add(new Label{Text="Presets act on the selected row. X/Y use desktop pixels; checkboxes only choose what a saved layout includes.",Dock=DockStyle.Fill,AutoEllipsis=true},0,5);
   return group;
  }
  void AddNumber(FlowLayoutPanel row,string label,NumberField field,int min,int max){row.Controls.Add(new Label{Text=label,AutoSize=true,Margin=new Padding(0,6,6,0)});field.Name="Position"+label;field.Minimum=min;field.Maximum=max;field.Value=min<0?0:640;field.Width=91;field.Margin=new Padding(0,3,14,3);row.Controls.Add(field);}
  LiveWindow SelectedWindow(){var w=windows.SelectedItem as LiveWindow;if(w==null||!Native.IsWindow(w.Handle))throw new InvalidOperationException("Select an open window. If it has closed, click Refresh.");return w;}
  Screen TargetScreen(){var target=monitor.SelectedItem as MonitorChoice;return target==null?Screen.PrimaryScreen:Screen.AllScreens.FirstOrDefault(s=>s.DeviceName==target.Screen.DeviceName)??Screen.PrimaryScreen;}
  void ReadSelected(){
   var w=windows.SelectedItem as LiveWindow;bool valid=w!=null&&Native.IsWindow(w.Handle);
   selectedTitle.Text=valid?"POSITION  ·  "+w.Title:"Select an open window above to position it";
   if(!valid)return;
   try{var record=Native.Capture(w);SetValue(posX,record.X);SetValue(posY,record.Y);SetValue(sizeW,record.Width);SetValue(sizeH,record.Height);monitor.Items.Clear();int i=0;foreach(var screen in Screen.AllScreens){var choice=new MonitorChoice{Screen=screen,Number=++i};monitor.Items.Add(choice);if(screen.DeviceName==record.Monitor)monitor.SelectedItem=choice;}if(monitor.SelectedIndex<0&&monitor.Items.Count>0)monitor.SelectedIndex=0;}catch(Exception ex){status.Text=ex.Message;}
  }
  void SetValue(NumberField field,int value){field.Value=Math.Max(field.Minimum,Math.Min(field.Maximum,value));}
  void Snap(int col,int row,int cols,int rows){ApplyPosition(Geometry.Zone(TargetScreen().WorkingArea,col,row,cols,rows),false);}
  void CenterWindow(){var current=Native.Capture(SelectedWindow());ApplyPosition(Geometry.Center(TargetScreen().WorkingArea,new Size(current.Width,current.Height)),false);}
  void MoveToMonitor(){var current=Native.Capture(SelectedWindow());ApplyPosition(Native.Target(current,TargetScreen().WorkingArea),current.Maximized);}
  void ApplyCustom(){Rectangle requested=new Rectangle((int)posX.Value,(int)posY.Value,(int)sizeW.Value,(int)sizeH.Value);bool visible=Screen.AllScreens.Any(s=>Rectangle.Intersect(s.WorkingArea,requested).Width>=Math.Min(100,requested.Width)&&Rectangle.Intersect(s.WorkingArea,requested).Height>=Math.Min(40,requested.Height));if(!visible)throw new InvalidOperationException("That position would put the window off screen. Choose coordinates on a connected display.");ApplyPosition(requested,false);}
  void ApplyPosition(Rectangle r,bool maximized){var w=SelectedWindow();var previous=Native.Capture(w);if(!Native.Place(w,r,maximized))throw new InvalidOperationException("Windows could not move this app. If it runs as administrator, run Lattice as administrator too.");undoWindow=w;undoRecord=previous;status.Text="Position requested for "+w.ProcessName+". You can save the arrangement as a layout below.";settle.Stop();settle.Start();}
  void UndoMove(){if(undoWindow==null||undoRecord==null)return;if(!Native.Move(undoWindow,undoRecord))throw new InvalidOperationException("The previous window could not be restored. It may have closed.");undoWindow=null;undoRecord=null;status.Text="Previous position restored.";settle.Stop();settle.Start();}
  void RefreshSnapChoices(){refreshingSnap=true;try{snapPreset.Items.Clear();snapPreset.Items.Add(new SnapChoice{Key="halves",Label="Preset: Halves"});snapPreset.Items.Add(new SnapChoice{Key="thirds",Label="Preset: Thirds"});snapPreset.Items.Add(new SnapChoice{Key="quarters",Label="Preset: Quarters"});foreach(var layout in library.Layouts)snapPreset.Items.Add(new SnapChoice{Key="layout:"+layout.Name,Label="Layout: "+layout.Name});int index=snapPreset.Items.FindIndex(x=>((SnapChoice)x).Key==library.DragPreset);snapPreset.SelectedIndex=index<0?0:index;}finally{refreshingSnap=false;}}
  void UpdateDragSettings(){if(refreshingSnap||dragSnapper==null)return;bool oldEnabled=library.DragSnapEnabled;string oldPreset=library.DragPreset;try{dragSnapper.SetEnabled(dragEnabled.Checked);library.DragSnapEnabled=dragEnabled.Checked;library.DragPreset=((SnapChoice)snapPreset.SelectedItem).Key;Persist();status.Text=dragEnabled.Checked?"Drag snapping is on. Escape or right-click bypasses the current drag.":"Drag snapping is off.";}catch(Exception ex){library.DragSnapEnabled=oldEnabled;library.DragPreset=oldPreset;refreshingSnap=true;dragEnabled.Checked=oldEnabled;refreshingSnap=false;try{dragSnapper.SetEnabled(oldEnabled);}catch{}RefreshSnapChoices();Dialogs.Show(this,ex.Message,"Drag snapping");}}
  void UseLayoutForDragging(){var layout=layouts.SelectedItem as Layout;if(layout==null)throw new InvalidOperationException("Save or select a layout first. Its saved window rectangles will become your drag zones.");snapPreset.SelectedIndex=snapPreset.Items.FindIndex(x=>((SnapChoice)x).Key=="layout:"+layout.Name);dragEnabled.Checked=true;status.Text="Drag any open window into the saved rectangles from \""+layout.Name+"\". Escape/right-click bypasses snapping.";}
  List<Rectangle> GetDragZones(Point pointer){
   var screen=Screen.FromPoint(pointer);var area=screen.WorkingArea;var choice=snapPreset.SelectedItem as SnapChoice;string key=choice==null?"halves":choice.Key;var result=new List<Rectangle>();
   if(key.StartsWith("layout:")){var layout=library.Layouts.FirstOrDefault(l=>"layout:"+l.Name==key);if(layout!=null)foreach(var saved in layout.Windows){var target=Screen.AllScreens.FirstOrDefault(s=>s.DeviceName==saved.Monitor)??Screen.PrimaryScreen;if(target.DeviceName==screen.DeviceName)result.Add(saved.Maximized?area:Native.Target(saved,area));}return result.Distinct().ToList();}
   int cols=key=="thirds"?3:2,rows=key=="quarters"?2:1;for(int row=0;row<rows;row++)for(int col=0;col<cols;col++)result.Add(Geometry.Zone(area,col,row,cols,rows));return result;
  }
  Button Button(string text,Action action){var b=new DarkButton{Text=text,AutoSize=true,Height=32};b.Click+=delegate{try{action();}catch(Exception ex){Dialogs.Show(this,ex.Message,"Lattice",MessageBoxButtons.OK,MessageBoxIcon.Error);}};return b;}
  Library LoadLibrary(){try{if(File.Exists(dataFile)){using(var s=File.OpenRead(dataFile))return (Library)new XmlSerializer(typeof(Library)).Deserialize(s);}}catch(Exception ex){Dialogs.Show("Your saved layouts could not be read. The original file will be kept.\n"+ex.Message,"Lattice");try{File.Copy(dataFile,dataFile+".unreadable-"+DateTime.Now.ToString("yyyyMMddHHmmss"));}catch{throw;}}return new Library();}
  void Persist(){Directory.CreateDirectory(Path.GetDirectoryName(dataFile));string temp=dataFile+".tmp";using(var s=File.Create(temp))new XmlSerializer(typeof(Library)).Serialize(s,library);if(File.Exists(dataFile))File.Replace(temp,dataFile,dataFile+".bak");else File.Move(temp,dataFile);}
  void RefreshLayouts(){string selected=(layouts.SelectedItem as Layout??new Layout()).Name;layouts.Items.Clear();foreach(var l in library.Layouts)layouts.Items.Add(l);if(selected!=null)for(int i=0;i<layouts.Items.Count;i++)if(((Layout)layouts.Items[i]).Name==selected)layouts.SelectedIndex=i;RefreshSnapChoices();
   var menu=new ContextMenuStrip{BackColor=Theme.Panel,ForeColor=Theme.Text,Renderer=new ToolStripProfessionalRenderer(new DarkMenuColors())};menu.Items.Add("Open Lattice",null,delegate{ShowMain();});foreach(var l in library.Layouts){Layout chosen=l;menu.Items.Add("Restore: "+l.Name,null,delegate{try{RestoreLayout(chosen);}catch(Exception ex){Dialogs.Show(ex.Message,"Lattice");}});}menu.Items.Add(new ToolStripSeparator());menu.Items.Add("Quit",null,delegate{exiting=true;Close();});var old=tray.ContextMenuStrip;tray.ContextMenuStrip=menu;if(old!=null)old.Dispose();}
  void ShowMain(){Show();WindowState=FormWindowState.Normal;Activate();}
  void RefreshWindows(){IntPtr selected=windows.SelectedItem is LiveWindow?((LiveWindow)windows.SelectedItem).Handle:IntPtr.Zero;var uncheckedHandles=new HashSet<IntPtr>();for(int i=0;i<windows.Items.Count;i++)if(!windows.GetItemChecked(i))uncheckedHandles.Add(((LiveWindow)windows.Items[i]).Handle);windows.Items.Clear();foreach(var w in Native.Windows()){int i=windows.Items.Add(w,!uncheckedHandles.Contains(w.Handle));if(w.Handle==selected)windows.SelectedIndex=i;}if(windows.SelectedIndex<0&&windows.Items.Count>0)windows.SelectedIndex=0;ReadSelected();status.Text=windows.Items.Count+" open windows found. Select a row and use the positioning controls below.";}
  void SaveLayout(){string title=name.Text.Trim();if(title.Length==0){Dialogs.Show(this,"Enter a layout name first.");return;}if(windows.CheckedItems.Count==0){Dialogs.Show(this,"Check at least one window to save.");return;}Layout old=library.Layouts.FirstOrDefault(l=>String.Equals(l.Name,title,StringComparison.OrdinalIgnoreCase));if(old!=null&&Dialogs.Show(this,"Replace the saved layout \""+old.Name+"\"?","Save layout",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
   var layout=new Layout{Name=title};foreach(LiveWindow w in windows.CheckedItems)if(Native.IsWindow(w.Handle))layout.Windows.Add(Native.Capture(w));if(layout.Windows.Count==0){Dialogs.Show(this,"Those windows have closed. Refresh and try again.");return;}
   var before=new List<Layout>(library.Layouts);try{if(old!=null)library.Layouts.Remove(old);library.Layouts.Add(layout);Persist();}catch{library.Layouts=before;throw;}RefreshLayouts();layouts.SelectedItem=layout;status.Text="Saved \""+title+"\" with "+layout.Windows.Count+" windows.";
  }
  void Delete(){Layout l=layouts.SelectedItem as Layout;if(l==null)return;if(Dialogs.Show(this,"Delete layout \""+l.Name+"\"?","Delete layout",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;var before=new List<Layout>(library.Layouts);try{library.Layouts.Remove(l);Persist();}catch{library.Layouts=before;throw;}RefreshLayouts();status.Text="Layout deleted.";}
  void Restore(){Layout l=layouts.SelectedItem as Layout;if(l==null){Dialogs.Show(this,"Select a saved layout first.");return;}RestoreLayout(l);}
  void RestoreLayout(Layout layout){var matches=Matcher.Resolve(layout,Native.Windows());int moved=0,failed=0;foreach(var match in matches){if(Native.Move(match.Live,match.Saved))moved++;else failed++;}int missing=layout.Windows.Count-matches.Count;
   string report="\""+layout.Name+"\": requested positions for "+moved+" windows.";if(missing>0)report+=" "+missing+" missing or ambiguous.";if(failed>0)report+=" "+failed+" could not be moved (check app permissions).";status.Text=report;
   if(!Visible)tray.ShowBalloonTip(3500,"Layout restored",report,ToolTipIcon.Info);else if(missing>0||failed>0)Dialogs.Show(this,report+"\n\nIf several windows belong to the same app, their titles must match the saved layout. Elevated apps may require running Lattice as administrator.","Restore results");
   settle.Stop();settle.Start();
  }
  protected override void Dispose(bool disposing){if(disposing){settle.Dispose();if(dragSnapper!=null)dragSnapper.Dispose();if(tray!=null){tray.Visible=false;tray.Dispose();}}base.Dispose(disposing);}
 }
 public static class Program {
  [STAThread] public static int Main(string[] args){
   if(args.Contains("--self-test"))return Tests.Run();
   try{Native.SetProcessDpiAwareness(2);}catch{Native.SetProcessDPIAware();}
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   bool created;using(var instance=new System.Threading.Mutex(true,@"Local\Castald0.Lattice.WindowManager",out created)){
    if(!created){Native.EnumWindows(delegate(IntPtr h,IntPtr unused){var text=new System.Text.StringBuilder(256);Native.GetWindowText(h,text,text.Capacity);if(text.ToString()=="Lattice · Arrange your workspace"){Native.PostMessage(h,0x8001,IntPtr.Zero,IntPtr.Zero);return false;}return true;},IntPtr.Zero);return 0;}
    try{Application.Run(new MainForm());return 0;}catch(Exception ex){Dialogs.Show(ex.Message,"Lattice could not start");return 1;}finally{instance.ReleaseMutex();}
   }
  }
 }
 public static class Tests {
  static void Assert(bool condition,string label){if(!condition)throw new Exception(label);}
  public static int Run(){try{
   DragSnapTests.Run();
   var saved=new WindowRecord{ProcessName="editor",Title="A",X=-1800,Y=100,Width=800,Height=600,WorkX=-1920,WorkY=0,WorkWidth=1920,WorkHeight=1080};
   Assert(Native.Target(saved,new Rectangle(-1920,0,1920,1080))==new Rectangle(-1800,100,800,600),"negative monitor coordinates");
   Rectangle fallback=Native.Target(saved,new Rectangle(0,0,1280,720));Assert(fallback.Left>=0&&fallback.Right<=1280&&fallback.Bottom<=720,"disconnected monitor mapping");
   var oddArea=new Rectangle(-1919,35,1919,1043);Rectangle last=Geometry.Zone(oddArea,2,0,3,1);Assert(last.Right==oddArea.Right&&last.Bottom==oddArea.Bottom,"thirds reach work area edges");Assert(Geometry.Zone(oddArea,0,0,3,1).Right==Geometry.Zone(oddArea,1,0,3,1).Left,"thirds have no rounding gaps");Assert(Geometry.Zone(oddArea,1,1,2,2).Right==oddArea.Right&&Geometry.Zone(oddArea,1,1,2,2).Bottom==oddArea.Bottom,"quadrant boundaries");Assert(Geometry.Center(oddArea,new Size(4000,3000))==oddArea,"oversize centered window stays on screen");
   var l=new Layout{Name="Test",Windows=new List<WindowRecord>{saved}};
   var exact=new LiveWindow{ProcessName="editor",Title="A",Handle=new IntPtr(1)};var other=new LiveWindow{ProcessName="editor",Title="B",Handle=new IntPtr(2)};
   Assert(Matcher.Resolve(l,new List<LiveWindow>{other,exact})[0].Live==exact,"exact title first");
   Assert(Matcher.Resolve(l,new List<LiveWindow>{other}).Count==1,"single dynamic title");
   Assert(Matcher.Resolve(l,new List<LiveWindow>{other,new LiveWindow{ProcessName="editor",Title="C"}}).Count==0,"ambiguous windows skipped");
   l.Windows.Add(new WindowRecord{ProcessName="editor",Title="D"});Assert(Matcher.Resolve(l,new List<LiveWindow>{other}).Count==0,"ambiguous saved entries skipped");
   var serializer=new XmlSerializer(typeof(Library));using(var ms=new MemoryStream()){serializer.Serialize(ms,new Library{Layouts=new List<Layout>{l}});ms.Position=0;Assert(((Library)serializer.Deserialize(ms)).Layouts[0].Windows[0].X==-1800,"layout persistence");}
   File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"self-test-result.txt"),"PASS: monitor coordinates, fallback scaling, exact and dynamic window matching, ambiguity handling, XML persistence, preset geometry, drag zone selection, bypass persistence, and next-drag reset.");return 0;
  }catch(Exception ex){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"self-test-result.txt"),"FAIL: "+ex);return 1;}}
 }
}
