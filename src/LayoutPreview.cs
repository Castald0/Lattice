using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace Lattice {
 public static class AppNames {
  public static string Friendly(string name){switch((name??"").ToLowerInvariant()){case "winword":return "Word";case "excel":return "Excel";case "outlook":case "olk":return "Outlook";case "teams":case "ms-teams":return "Teams";case "msedge":return "Edge";case "chrome":return "Chrome";case "windowsterminal":return "Terminal";default:return name??"App";}}
 }
 public class LayoutPreview:Panel {
  Layout source;WindowRecord selected;readonly List<Tuple<Rectangle,WindowRecord>> hitBoxes=new List<Tuple<Rectangle,WindowRecord>>();
  public event EventHandler SelectionChanged;
  public Layout Source{get{return source;}set{source=value;if(source==null||!source.Windows.Contains(selected))selected=source==null?null:source.Windows.FirstOrDefault();Invalidate();}}
  public WindowRecord Selected{get{return selected;}}
  public LayoutPreview(){DoubleBuffered=true;BackColor=Theme.Panel;ResizeRedraw=true;Cursor=Cursors.Hand;}
  public static Rectangle SavedBounds(WindowRecord w){return w.Maximized?new Rectangle(w.WorkX,w.WorkY,Math.Max(1,w.WorkWidth),Math.Max(1,w.WorkHeight)):new Rectangle(w.X,w.Y,Math.Max(1,w.Width),Math.Max(1,w.Height));}
  protected override void OnPaint(PaintEventArgs e){
   base.OnPaint(e);var g=e.Graphics;g.Clear(Theme.Panel);hitBoxes.Clear();
   using(var border=new Pen(Theme.Border))g.DrawRectangle(border,0,0,Math.Max(1,Width-1),Math.Max(1,Height-1));
   if(source==null||source.Windows.Count==0){TextRenderer.DrawText(g,"Save or select a layout to see its monitor map here.",Font,ClientRectangle,Theme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak);return;}
   var monitors=source.Windows.GroupBy(w=>w.Monitor??"").Select(group=>group.First()).ToList();Rectangle union=new Rectangle(monitors[0].WorkX,monitors[0].WorkY,Math.Max(1,monitors[0].WorkWidth),Math.Max(1,monitors[0].WorkHeight));foreach(var m in monitors)union=Rectangle.Union(union,new Rectangle(m.WorkX,m.WorkY,Math.Max(1,m.WorkWidth),Math.Max(1,m.WorkHeight)));
   float scale=Math.Min((Width-32f)/Math.Max(1,union.Width),(Height-70f)/Math.Max(1,union.Height));if(scale<=0)return;float ox=(Width-union.Width*scale)/2f-union.X*scale,oy=35+(Height-70-union.Height*scale)/2f-union.Y*scale;
   Func<Rectangle,Rectangle> project=r=>new Rectangle((int)(ox+r.X*scale),(int)(oy+r.Y*scale),Math.Max(1,(int)(r.Width*scale)),Math.Max(1,(int)(r.Height*scale)));
   TextRenderer.DrawText(g,"SAVED: "+source.Name,Font,new Rectangle(12,8,Width-24,22),Theme.Text,TextFormatFlags.EndEllipsis);
   int number=0;foreach(var m in monitors){number++;var rect=project(new Rectangle(m.WorkX,m.WorkY,Math.Max(1,m.WorkWidth),Math.Max(1,m.WorkHeight)));using(var fill=new SolidBrush(Theme.Background))g.FillRectangle(fill,rect);using(var border=new Pen(Theme.Muted,2))g.DrawRectangle(border,rect);TextRenderer.DrawText(g,"Display "+number,Font,new Rectangle(rect.X+4,rect.Y+3,rect.Width-8,20),Theme.Muted,TextFormatFlags.EndEllipsis);}
   foreach(var w in source.Windows.OrderBy(w=>w==selected?1:0)){
    var rect=project(SavedBounds(w));hitBoxes.Add(Tuple.Create(rect,w));bool active=w==selected;using(var fill=new SolidBrush(Color.FromArgb(active?100:48,Theme.Accent)))g.FillRectangle(fill,rect);using(var outline=new Pen(active?Theme.Accent:Color.FromArgb(88,126,152),active?3:1))g.DrawRectangle(outline,rect);
    var text=new Rectangle(rect.X+7,rect.Y+24,Math.Max(1,rect.Width-14),Math.Max(1,rect.Height-28));TextRenderer.DrawText(g,AppNames.Friendly(w.ProcessName)+(w.Maximized?"\nMAXIMIZED":"")+(w.MatchMode=="target"?"\nSpecific file/link":""),Font,text,Theme.Text,TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis);
   }
   string footer=selected==null?"Click a window to inspect its saved state":AppNames.Friendly(selected.ProcessName)+"  ·  "+(selected.Maximized?"Maximized on its saved display":selected.Width+" × "+selected.Height+" at "+selected.X+", "+selected.Y);TextRenderer.DrawText(g,footer,Font,new Rectangle(12,Height-27,Width-24,22),Theme.Accent,TextFormatFlags.EndEllipsis);
  }
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);var hit=hitBoxes.Where(p=>p.Item1.Contains(e.Location)).OrderBy(p=>(long)p.Item1.Width*p.Item1.Height).FirstOrDefault();if(hit==null)return;selected=hit.Item2;Invalidate();if(SelectionChanged!=null)SelectionChanged(this,EventArgs.Empty);}
 }
}
