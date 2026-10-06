using System;
using System.Windows.Forms;
class ReopenFixture {
 [STAThread] static void Main(){using(var form=new Form{Text="Lattice disposable reopen fixture"})using(var timeout=new Timer{Interval=20000}){timeout.Tick+=delegate{form.Close();};timeout.Start();Application.Run(form);}}
}
