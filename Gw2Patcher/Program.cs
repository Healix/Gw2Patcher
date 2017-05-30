using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Gw2Patcher
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                for (int i = 0, l = args.Length; i < l; i++)
                {
                    if (args[i] == "-port")
                    {
                        Settings.ServerPort = int.Parse(args[i + 1]);
                    }
                }
            }
            catch { }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new UI.formMain());
        }
    }
}
