using System;
using System.Windows.Forms;

namespace AlwaysOnTopApp
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
// dotnet publish -c Release -r win-x64 --self-contained
