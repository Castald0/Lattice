using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using Lattice;
class SmokeTest {
 [STAThread] static int Main(string[] args){
  try{Native.SetProcessDpiAwareness(2);}catch{Native.SetProcessDPIAware();}
  Application.EnableVisualStyles();
  if(args.Length>0){using(var fixture=new Form()){fixture.Text="Lattice discovery fixture";var timer=new Timer{Interval=15000};timer.Tick+=delegate{fixture.Close();};timer.Start();Application.Run(fixture);timer.Dispose();}return 0;}
  try {
   using(var child=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Application.ExecutablePath,"--fixture"){UseShellExecute=false,CreateNoWindow=true})){
    try{bool found=false;for(int i=0;i<50;i++){if(Native.Windows().Exists(w=>w.Title=="Lattice discovery fixture")){found=true;break;}System.Threading.Thread.Sleep(100);}if(!found)throw new Exception("External window discovery failed");}finally{if(!child.HasExited){child.CloseMainWindow();if(!child.WaitForExit(2000))child.Kill();}}
   }
   using(var f=new Form()){f.Text="Lattice movement test";f.StartPosition=FormStartPosition.Manual;var a=Screen.PrimaryScreen.WorkingArea;f.Bounds=new Rectangle(a.Left+40,a.Top+40,500,350);f.Show();Application.DoEvents();
    var live=new LiveWindow{Handle=f.Handle,ProcessName="SmokeTest",Title=f.Text};var saved=Native.Capture(live);f.Location=new Point(a.Left+150,a.Top+180);Application.DoEvents();
    if(!Native.Move(live,saved))throw new Exception("Move rejected");Application.DoEvents();var restored=Native.Capture(live);
    if(restored.X!=saved.X||restored.Y!=saved.Y||restored.Width!=saved.Width||restored.Height!=saved.Height)throw new Exception("Restored bounds mismatch");
    f.WindowState=FormWindowState.Maximized;Application.DoEvents();var max=Native.Capture(live);if(!max.Maximized)throw new Exception("Maximized state not captured");f.WindowState=FormWindowState.Normal;Application.DoEvents();Native.Move(live,max);Application.DoEvents();if(!Native.IsZoomed(f.Handle))throw new Exception("Maximized state not restored");f.Close();
   }
   using(var main=new MainForm()){main.Show();Application.DoEvents();using(var bitmap=new Bitmap(main.Width,main.Height)){main.DrawToBitmap(bitmap,new Rectangle(0,0,main.Width,main.Height));bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview.png"));}main.Hide();}
   File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"smoke-result.txt"),"PASS: external application window discovery; real test window position and size restoration; maximized state capture and restoration; main interface construction and rendering.");return 0;
  }catch(Exception ex){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"smoke-result.txt"),ex.ToString());return 1;}
 }
}
