using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Lattice;
class OfficeSmoke {
 static void Pump(int milliseconds){var end=DateTime.UtcNow.AddMilliseconds(milliseconds);while(DateTime.UtcNow<end){Application.DoEvents();Thread.Sleep(15);}}
 static void Entry(ZipArchive zip,string path,string text){using(var writer=new StreamWriter(zip.CreateEntry(path).Open(),new UTF8Encoding(false)))writer.Write(text);}
 static void CreateFile(string path,bool word){using(var zip=ZipFile.Open(path,ZipArchiveMode.Create)){
  string part=word?"word/document.xml":"xl/workbook.xml";
  Entry(zip,"_rels/.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\""+part+"\"/></Relationships>");
  Entry(zip,"[Content_Types].xml","<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/"+part+"\" ContentType=\"application/vnd.openxmlformats-officedocument."+(word?"wordprocessingml.document.main+xml":"spreadsheetml.sheet.main+xml")+"\"/>"+(word?"":"<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>")+"</Types>");
  if(word)Entry(zip,part,"<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>Lattice disposable integration fixture</w:t></w:r></w:p><w:sectPr/></w:body></w:document>");
  else{Entry(zip,part,"<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Fixture\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");Entry(zip,"xl/_rels/workbook.xml.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");Entry(zip,"xl/worksheets/sheet1.xml","<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>Lattice disposable integration fixture</t></is></c></row></sheetData></worksheet>");}
 }}
 static string Test(string exe,string directory,bool word){
  string file=Path.Combine(directory,word?"Lattice-test.docx":"Lattice-test.xlsx");CreateFile(file,word);var start=new ProcessStartInfo(exe,(word?"/n /q /a ":"/x /s ")+"\""+file+"\""){UseShellExecute=false,WindowStyle=ProcessWindowStyle.Minimized};
  using(var process=Process.Start(start)){LiveWindow document=null;try{
   var end=DateTime.UtcNow.AddSeconds(35);while(DateTime.UtcNow<end){foreach(var window in Native.Windows().Where(w=>w.ProcessId==process.Id)){string path=OfficeDocuments.ReadPathAsync(window).GetAwaiter().GetResult();if(Targets.SameFile(file,path)){document=window;break;}}if(document!=null)break;Thread.Sleep(300);}
   if(document==null)throw new Exception((word?"Word":"Excel")+": could not verify the disposable file in its new test process.");
   var saved=Native.Capture(document);saved.MatchMode="target";saved.Target=file;saved.OpenOnRestore=false;var outcome=RestoreRunner.Prepare(new Layout{Windows=new List<WindowRecord>{saved}},delegate{}).GetAwaiter().GetResult();if(outcome.Matches.Count!=1||outcome.Matches[0].Live.Handle!=document.Handle)throw new Exception("Specific file matched the wrong window.");
   var area=Screen.FromHandle(document.Handle).WorkingArea;var desired=new Rectangle(area.Left+80,area.Top+80,760,540);if(!Native.Place(document,desired,false))throw new Exception("Office placement failed");Pump(700);var actual=Native.Capture(document);if(actual.X!=desired.X||actual.Y!=desired.Y||actual.Width!=desired.Width||actual.Height!=desired.Height)throw new Exception("Office adjusted the requested test geometry.");
   return "PASS: "+(word?"Word":"Excel")+" opens the disposable file, exposes its full path, matches the requested document, and accepts the saved position.";
  }finally{if(document!=null)Native.PostMessage(document.Handle,0x10,IntPtr.Zero,IntPtr.Zero);if(!process.HasExited&&!process.WaitForExit(4000))process.Kill();}}
 }
 [STAThread] static int Main(string[] args){try{Native.SetProcessDpiAwareness(2);}catch{Native.SetProcessDPIAware();}string result=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"office-result.txt");var lines=new List<string>();int code=0;string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"office-fixtures",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);for(int i=0;i<2;i++){try{lines.Add(Test(args[i],folder,i==0));}catch(Exception ex){lines.Add("FAIL: "+ex.Message);code=1;}File.WriteAllLines(result,lines);}return code;}
}
