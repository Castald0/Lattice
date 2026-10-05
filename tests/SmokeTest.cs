using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;
using Lattice;
class SmokeTest {
 static string dragResult="";
 [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
 [DllImport("user32.dll")] static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
 static IEnumerable<Control> All(Control parent){foreach(Control c in parent.Controls){yield return c;foreach(var nested in All(c))yield return nested;}}
 static void Pump(){var end=DateTime.UtcNow.AddMilliseconds(600);while(DateTime.UtcNow<end){Application.DoEvents();System.Threading.Thread.Sleep(15);}}
 static void Click(Form form,string title){var button=All(form).OfType<Button>().Single(b=>b.Text==title);button.PerformClick();Pump();}
 static void BoundsEqual(LiveWindow live,Rectangle expected,string label){Native.Rect actual;if(!Native.GetWindowRect(live.Handle,out actual)||actual.Left!=expected.Left||actual.Top!=expected.Top||actual.Right!=expected.Right||actual.Bottom!=expected.Bottom)throw new Exception(label+": unexpected window bounds");}
 static void TestDrag(Form main,LiveWindow fixture,Rectangle area,string cancel){
  Rectangle start=new Rectangle(area.Left+140,area.Top+140,440,320);Native.Place(fixture,start,false);SetForegroundWindow(fixture.Handle);Pump();
  SetCursorPos(start.Left+180,start.Top+12);mouse_event(2,0,0,0,UIntPtr.Zero);Pump();SetCursorPos(start.Left+200,start.Top+22);Pump();SetCursorPos(area.Left+area.Width*3/4,area.Top+area.Height/3);Pump();
  var snapper=typeof(MainForm).GetField("dragSnapper",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(main);var overlay=(SnapOverlay)typeof(DragSnapper).GetField("overlay",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(snapper);
  try{
   if(!overlay.Visible){var info=typeof(DragSnapper).GetField("LastState",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(snapper);DragNative.CursorPoint p;bool cursor=DragNative.GetCursorPos(out p);var captured=Native.Capture(fixture);throw new Exception("Real drag did not highlight a snap zone. "+info+"; cursor available="+cursor+" at "+p.X+","+p.Y+"; button="+DragNative.Down(1)+"; window="+captured.X+","+captured.Y+","+captured.Width+","+captured.Height);}
   if(cancel=="escape"){keybd_event(27,0,0,UIntPtr.Zero);keybd_event(27,0,2,UIntPtr.Zero);Pump();}
   if(cancel=="right"){mouse_event(8,0,0,0,UIntPtr.Zero);mouse_event(16,0,0,0,UIntPtr.Zero);Pump();}
  }finally{mouse_event(4,0,0,0,UIntPtr.Zero);Pump();}
  if(overlay.Visible)throw new Exception("Highlight remained after release");
  if(cancel==null)BoundsEqual(fixture,Geometry.Zone(area,1,0,2,1),"Real drag release");else{var actual=Native.Capture(fixture);if(actual.Width!=start.Width||actual.Height!=start.Height)throw new Exception("Cancelled drag was snapped: "+cancel);}
  keybd_event(27,0,0,UIntPtr.Zero);keybd_event(27,0,2,UIntPtr.Zero);Pump();
 }
 [STAThread] static int Main(string[] args){
  try{Native.SetProcessDpiAwareness(2);}catch{Native.SetProcessDPIAware();}
  Application.EnableVisualStyles();
  if(args.Length>0&&args[0]=="--fixture"){using(var fixture=new Form()){fixture.Text="Lattice discovery fixture";var timer=new Timer{Interval=90000};timer.Tick+=delegate{fixture.Close();};timer.Start();Application.Run(fixture);timer.Dispose();}return 0;}
  try {
   using(var child=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Application.ExecutablePath,"--fixture"){UseShellExecute=false,CreateNoWindow=true})){
    try{
     LiveWindow fixture=null;for(int i=0;i<50;i++){fixture=Native.Windows().FirstOrDefault(w=>w.Title=="Lattice discovery fixture");if(fixture!=null)break;System.Threading.Thread.Sleep(100);}if(fixture==null)throw new Exception("External window discovery failed");
     string data=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-layouts-"+Guid.NewGuid().ToString("N")+".xml");
     try{using(var main=new MainForm(data)){
      main.Show();Pump();var list=(CheckedListBox)main.Controls.Find("WindowList",true)[0];int index=-1;for(int i=0;i<list.Items.Count;i++)if(((LiveWindow)list.Items[i]).Handle==fixture.Handle)index=i;if(index<0)throw new Exception("Fixture missing from app UI");list.SelectedIndex=index;Pump();
      var a=Screen.FromHandle(fixture.Handle).WorkingArea;
      var positions=new[]{new{Label="Left half",Col=0,Row=0,Cols=2,Rows=1},new{Label="Right half",Col=1,Row=0,Cols=2,Rows=1},new{Label="Top half",Col=0,Row=0,Cols=1,Rows=2},new{Label="Bottom half",Col=0,Row=1,Cols=1,Rows=2},new{Label="Top left",Col=0,Row=0,Cols=2,Rows=2},new{Label="Top right",Col=1,Row=0,Cols=2,Rows=2},new{Label="Bottom left",Col=0,Row=1,Cols=2,Rows=2},new{Label="Bottom right",Col=1,Row=1,Cols=2,Rows=2},new{Label="Left third",Col=0,Row=0,Cols=3,Rows=1},new{Label="Middle third",Col=1,Row=0,Cols=3,Rows=1},new{Label="Right third",Col=2,Row=0,Cols=3,Rows=1}};
      foreach(var p in positions){Click(main,p.Label);BoundsEqual(fixture,Geometry.Zone(a,p.Col,p.Row,p.Cols,p.Rows),p.Label);}
      Click(main,"Undo last move");BoundsEqual(fixture,Geometry.Zone(a,1,0,3,1),"Undo");
      var exact=new Rectangle(a.Left+50,a.Top+60,500,350);((TextBox)main.Controls.Find("PositionX",true)[0]).Text=exact.X.ToString();((TextBox)main.Controls.Find("PositionY",true)[0]).Text=exact.Y.ToString();((TextBox)main.Controls.Find("PositionWidth",true)[0]).Text=exact.Width.ToString();((TextBox)main.Controls.Find("PositionHeight",true)[0]).Text=exact.Height.ToString();Click(main,"Apply position");BoundsEqual(fixture,exact,"Exact position");
      Click(main,"Center");BoundsEqual(fixture,Geometry.Center(a,exact.Size),"Center");Click(main,"Maximize");if(!Native.IsZoomed(fixture.Handle))throw new Exception("UI maximize failed");Click(main,"Left half");BoundsEqual(fixture,Geometry.Zone(a,0,0,2,1),"Snap from maximized");
      for(int i=0;i<list.Items.Count;i++)list.SetItemChecked(i,i==index);((TextBox)main.Controls.Find("LayoutName",true)[0]).Text="Focus workspace";Click(main,"Save layout");if(!File.Exists(data))throw new Exception("UI save did not persist");Click(main,"Right half");Click(main,"Restore");BoundsEqual(fixture,Geometry.Zone(a,0,0,2,1),"UI save and restore");
      if(main.BackColor!=Theme.Background||list.BackColor!=Theme.Field)throw new Exception("Dark theme missing");
      DragNative.CursorPoint oldCursor;bool inputAvailable=DragNative.GetCursorPos(out oldCursor);try{
       var enable=(CheckBox)main.Controls.Find("EnableDragSnap",true)[0];enable.Checked=true;Pump();
       var snapper=(DragSnapper)typeof(MainForm).GetField("dragSnapper",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(main);if(!snapper.Enabled)throw new Exception("Drag event hook was not installed");
       if(inputAvailable){TestDrag(main,fixture,a,null);TestDrag(main,fixture,a,"escape");TestDrag(main,fixture,a,"right");TestDrag(main,fixture,a,null);dragResult="PASS: physical drag highlighting, release snap, Escape/right-click bypass, and next-drag reactivation.";}
       else{if(args.Contains("--require-input"))throw new Exception("Physical drag tests require access to an interactive input desktop.");dragResult="SKIP: physical drag gestures; this session cannot access an input desktop. Drag decision/cancellation logic and event-hook registration were tested.";}
       main.Show();main.Activate();Click(main,"Use selected layout");var choice=(DarkChoice)main.Controls.Find("DragPreset",true)[0];if(!((SnapChoice)choice.SelectedItem).Key.StartsWith("layout:"))throw new Exception("Saved layout not selected as drag preset");
       var customZones=(List<Rectangle>)typeof(MainForm).GetMethod("GetDragZones",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(main,new object[]{new Point(a.Left+100,a.Top+100)});if(customZones.Count!=1||customZones[0]!=Geometry.Zone(a,0,0,2,1))throw new Exception("Saved rectangle not used as drag zone");
       enable.Checked=false;Pump();
      }finally{if(inputAvailable){mouse_event(4,0,0,0,UIntPtr.Zero);SetCursorPos(oldCursor.X,oldCursor.Y);}}
      using(var bitmap=new Bitmap(main.Width,main.Height)){main.DrawToBitmap(bitmap,new Rectangle(0,0,main.Width,main.Height));bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview.png"));}main.Hide();
     }}finally{if(File.Exists(data))File.Delete(data);if(File.Exists(data+".bak"))File.Delete(data+".bak");}
    }finally{if(!child.HasExited){child.CloseMainWindow();if(!child.WaitForExit(2000))child.Kill();}}
   }
   using(var f=new Form()){f.Text="Lattice movement test";f.StartPosition=FormStartPosition.Manual;var a=Screen.PrimaryScreen.WorkingArea;f.Bounds=new Rectangle(a.Left+40,a.Top+40,500,350);f.Show();Application.DoEvents();
    var live=new LiveWindow{Handle=f.Handle,ProcessName="SmokeTest",Title=f.Text};var saved=Native.Capture(live);f.Location=new Point(a.Left+150,a.Top+180);Application.DoEvents();
    if(!Native.Move(live,saved))throw new Exception("Move rejected");Application.DoEvents();var restored=Native.Capture(live);
    if(restored.X!=saved.X||restored.Y!=saved.Y||restored.Width!=saved.Width||restored.Height!=saved.Height)throw new Exception("Restored bounds mismatch");
    f.WindowState=FormWindowState.Maximized;Application.DoEvents();var max=Native.Capture(live);if(!max.Maximized)throw new Exception("Maximized state not captured");f.WindowState=FormWindowState.Normal;Application.DoEvents();Native.Move(live,max);Application.DoEvents();if(!Native.IsZoomed(f.Handle))throw new Exception("Maximized state not restored");f.Close();
   }
   File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"smoke-result.txt"),"PASS: external window discovery; all 11 preset buttons; exact position/size inputs; center; maximize; snap from maximized; undo; save/restore; drag event-hook registration; saved-layout drag zone geometry; dark theme.\r\n"+dragResult);return 0;
  }catch(Exception ex){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"smoke-result.txt"),ex.ToString());return 1;}
 }
}
