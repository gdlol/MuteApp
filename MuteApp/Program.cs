using System;
using System.Windows;

namespace MuteApp
{
    public class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                var app = new App();
                app.Run();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }
    }
}
