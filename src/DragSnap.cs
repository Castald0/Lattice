using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Lattice {
 public class DragDecision {
  public bool Active {get;private set;}
  public bool Bypassed {get;private set;}
  public Rectangle? Target {get;private set;}
  public void Begin(){Active=true;Bypassed=false;Target=null;}
  public void Hover(IEnumerable<Rectangle> zones,Point pointer){
   if(!Active||Bypassed){Target=null;return;}
   // Nested custom zones prefer the smaller rectangle; ties prefer the nearer center.
   var candidates=zones.Where(r=>r.Contains(pointer)).OrderBy(r=>(long)r.Width*r.Height).ThenBy(r=>Math.Pow(pointer.X-(r.Left+r.Width/2.0),2)+Math.Pow(pointer.Y-(r.Top+r.Height/2.0),2)).ToList();
   Target=candidates.Count==0?(Rectangle?)null:candidates[0];
  }
  public void Bypass(){if(Active){Bypassed=true;Target=null;}}
  public Rectangle? End(){var result=Active&&!Bypassed?Target:null;Active=false;Target=null;return result;}
 }
 public static class DragNative {
  public delegate void WinEventProc(IntPtr hook,uint eventId,IntPtr window,int objectId,int childId,uint thread,uint time);
  public delegate IntPtr HookProc(int code,IntPtr message,IntPtr data);
  [StructLayout(LayoutKind.Sequential)] public struct CursorPoint{public int X,Y;}
  [DllImport("user32.dll",SetLastError=true)] public static extern IntPtr SetWinEventHook(uint min,uint max,IntPtr module,WinEventProc proc,uint process,uint thread,uint flags);
  [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr hook);
  [DllImport("user32.dll",SetLastError=true)] public static extern IntPtr SetWindowsHookEx(int id,HookProc proc,IntPtr module,uint thread);
  [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hook);
  [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);
  [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
  [DllImport("user32.dll")] public static extern bool GetCursorPos(out CursorPoint point);
  [DllImport("user32.dll",SetLastError=true)] public static extern IntPtr SendMessageTimeout(IntPtr h,uint message,IntPtr wParam,IntPtr lParam,uint flags,uint timeout,out IntPtr result);
  public static bool Down(int key){return (GetAsyncKeyState(key)&0x8000)!=0;}
 }
 public class SnapOverlay:Form {
  public SnapOverlay(){FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;BackColor=Theme.Accent;Opacity=0.30;TopMost=true;DoubleBuffered=true;}
  protected override bool ShowWithoutActivation{get{return true;}}
  protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x80|0x20;return p;}}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(var pen=new Pen(Color.White,5))e.Graphics.DrawRectangle(pen,2,2,Math.Max(1,Width-5),Math.Max(1,Height-5));using(var font=new Font("Segoe UI",13,FontStyle.Bold))TextRenderer.DrawText(e.Graphics,"Release to snap\nEsc / right-click to bypass",font,ClientRectangle,Color.Black,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak);}
 }
 public class DragSnapper:IDisposable {
  readonly Func<Point,List<Rectangle>> zones;readonly Action<string> report;readonly DragDecision decision=new DragDecision();readonly Timer timer=new Timer{Interval=20};readonly SnapOverlay overlay=new SnapOverlay();
  readonly DragNative.WinEventProc eventCallback;readonly DragNative.HookProc keyCallback,mouseCallback;
  IntPtr events,keyboard,mouse;LiveWindow dragged,pending;Rectangle initial,pendingTarget;DateTime applyAt;bool originallyMaximized,disposed;
  internal string LastState="Waiting for a title-bar drag";
  public bool Enabled{get{return events!=IntPtr.Zero;}}
  public DragSnapper(Func<Point,List<Rectangle>> getZones,Action<string> reportStatus){zones=getZones;report=reportStatus;eventCallback=OnEvent;keyCallback=OnKey;mouseCallback=OnMouse;timer.Tick+=Tick;}
  public void SetEnabled(bool enabled){
   if(disposed)return;if(!enabled){Stop();return;}if(Enabled)return;
   events=DragNative.SetWinEventHook(10,11,IntPtr.Zero,eventCallback,0,0,2);
   if(events==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error(),"Windows could not enable drag snapping.");
   timer.Start();
  }
  void Stop(){timer.Stop();Cancel();pending=null;if(events!=IntPtr.Zero){DragNative.UnhookWinEvent(events);events=IntPtr.Zero;}ReleaseInputHooks();}
  void OnEvent(IntPtr hook,uint eventId,IntPtr window,int objectId,int childId,uint thread,uint time){
   try{LastState="Window move event "+eventId;if(eventId==10)Begin(window);else if(eventId==11&&dragged!=null&&dragged.Handle==window)Finish();}catch(Exception ex){Cancel();report("Drag snapping paused for this drag: "+ex.Message);}
  }
  void Begin(IntPtr window){
   Cancel();pending=null;if(!Enabled||!DragNative.Down(1)){LastState="No left mouse button held";return;}
   DragNative.CursorPoint point;if(!DragNative.GetCursorPos(out point))return;
   IntPtr hit;long packed=((long)(point.Y&0xffff)<<16)|(uint)(point.X&0xffff);
   if(DragNative.SendMessageTimeout(window,0x84,IntPtr.Zero,new IntPtr(packed),2,60,out hit)==IntPtr.Zero||hit.ToInt32()!=2){LastState="Not a title-bar hit: "+hit;return;} // Caption moves only, never border resizing.
   dragged=Native.Windows().FirstOrDefault(w=>w.Handle==window);if(dragged==null)return;
   Native.Rect r;if(!Native.GetWindowRect(window,out r)){dragged=null;return;}initial=Rectangle.FromLTRB(r.Left,r.Top,r.Right,r.Bottom);originallyMaximized=Native.IsZoomed(window);
   decision.Begin();
   LastState="Tracking title-bar drag";
   keyboard=DragNative.SetWindowsHookEx(13,keyCallback,DragNative.GetModuleHandle(null),0);mouse=DragNative.SetWindowsHookEx(14,mouseCallback,DragNative.GetModuleHandle(null),0);
   if(keyboard==IntPtr.Zero||mouse==IntPtr.Zero){Cancel();report("Drag snapping could not monitor Escape/right-click. No snapping was applied.");return;}
   UpdateTarget();
  }
  IntPtr OnKey(int code,IntPtr message,IntPtr data){if(code>=0&&(message.ToInt32()==0x100||message.ToInt32()==0x104)&&Marshal.ReadInt32(data)==27)Bypass();return DragNative.CallNextHookEx(keyboard,code,message,data);}
  IntPtr OnMouse(int code,IntPtr message,IntPtr data){if(code>=0&&message.ToInt32()==0x204)Bypass();return DragNative.CallNextHookEx(mouse,code,message,data);}
  void Bypass(){decision.Bypass();overlay.Hide();}
  void UpdateTarget(bool checkSize=true){
   if(dragged==null)return;if(DragNative.Down(27)||DragNative.Down(2))Bypass();
   if(decision.Bypassed)return;
   Native.Rect r;if(!Native.GetWindowRect(dragged.Handle,out r)){Cancel();return;}
   // Additional guard for unusual app move/resize implementations.
   if(checkSize&&!originallyMaximized&&(Math.Abs((r.Right-r.Left)-initial.Width)>2||Math.Abs((r.Bottom-r.Top)-initial.Height)>2)){LastState="Resizing, not moving";Bypass();return;}
   DragNative.CursorPoint cursor;if(!DragNative.GetCursorPos(out cursor))return;Point p=new Point(cursor.X,cursor.Y);decision.Hover(zones(p),p);
   if(decision.Target.HasValue){overlay.Bounds=decision.Target.Value;if(!overlay.Visible)overlay.Show();}else overlay.Hide();
  }
  void Tick(object sender,EventArgs e){
   try{
    if(dragged!=null){if(!DragNative.Down(1))Finish();else UpdateTarget();}
    if(pending!=null&&DateTime.UtcNow>=applyAt){var live=pending;pending=null;if(Native.Place(live,pendingTarget,false))report("Snapped "+live.ProcessName+" to the highlighted zone.");else report("That app could not be snapped. Check whether it runs as administrator.");}
   }catch(Exception ex){Cancel();pending=null;report("Drag snapping: "+ex.Message);}
  }
  void Finish(){
   UpdateTarget(false);var target=decision.End();if(dragged!=null&&target.HasValue){pending=dragged;pendingTarget=target.Value;applyAt=DateTime.UtcNow.AddMilliseconds(100);}
   dragged=null;overlay.Hide();ReleaseInputHooks();
  }
  void Cancel(){decision.Bypass();decision.End();dragged=null;overlay.Hide();ReleaseInputHooks();}
  void ReleaseInputHooks(){if(keyboard!=IntPtr.Zero){DragNative.UnhookWindowsHookEx(keyboard);keyboard=IntPtr.Zero;}if(mouse!=IntPtr.Zero){DragNative.UnhookWindowsHookEx(mouse);mouse=IntPtr.Zero;}}
  public void Dispose(){if(disposed)return;Stop();disposed=true;timer.Dispose();overlay.Dispose();}
 }
 public static class DragSnapTests {
  public static void Run(){
   var state=new DragDecision();var zones=new List<Rectangle>{new Rectangle(-1000,0,500,800),new Rectangle(-500,0,500,800)};
   state.Begin();state.Hover(zones,new Point(-750,100));if(state.End()!=zones[0])throw new Exception("Drag target selection");
   state.Begin();state.Hover(zones,new Point(-250,100));state.Bypass();state.Hover(zones,new Point(-750,100));if(state.End().HasValue)throw new Exception("Cancellation must persist for the whole drag");
   state.Begin();state.Hover(zones,new Point(-250,100));if(state.End()!=zones[1])throw new Exception("Cancellation must reset for next drag");
   state.Begin();state.Hover(zones,new Point(100,900));if(state.End().HasValue)throw new Exception("Outside zones must not snap");
   zones.Add(new Rectangle(-900,50,200,200));state.Begin();state.Hover(zones,new Point(-800,100));if(state.End()!=zones[2])throw new Exception("Nested zone selection");
  }
 }
}
