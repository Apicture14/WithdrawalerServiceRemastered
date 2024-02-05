using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace WithdrawalerService
{
    internal static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        static void Main()
        {
            
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new Service1()
            };
            ServiceBase.Run(ServicesToRun);
            
            /*
            foreach (var VARIABLE in Process.GetProcesses())
            {
                if (VARIABLE.ProcessName == "chrome")
                {
                    if (VARIABLE.MainWindowHandle == IntPtr.Zero)
                    {
                        continue;
                    }

                    Utils.SafeKill(VARIABLE);
                }
            }
            */
        }
    }
}