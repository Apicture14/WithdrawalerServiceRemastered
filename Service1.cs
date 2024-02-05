using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace WithdrawalerService
{
    public partial class Service1 : ServiceBase
    {
        public static int ProgramVersion = 1;
        public static string Identifier = "WITHDRAWALER";
        public static byte ControlKey = 0x6A;

        public Timer WorkerTimer = new Timer();
        public Timer ListenerTimer = new Timer();

        public static Configuration.Config DefaultConfig = Configuration.CreateDefault();
        public static Configuration.Config AppliedConfig;

        public static FileStream LogFile = new FileStream($"C:\\yService\\logs\\log {DateTime.Now.ToString("HH-mm-ss")}.txt",
            FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        public static Random rand = new Random();
        public static bool urand = false;

        public static string ConfigPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\config.txt";

        public Dictionary<string, int> Records = new Dictionary<string, int>();

        public Service1()
        {
            InitializeComponent();

            CanHandlePowerEvent = true;
            CanShutdown = true;

            Configuration.Load();

            if (!Directory.Exists(AppliedConfig.LogFolder))
            {
                Directory.CreateDirectory(AppliedConfig.LogFolder);
            }else if (!Directory.Exists(AppliedConfig.ControlFolder))
            {
                Directory.CreateDirectory(AppliedConfig.ControlFolder);
            }
            
            WorkerTimer.Interval = AppliedConfig.MinDelay;
            WorkerTimer.AutoReset = true;
            WorkerTimer.Elapsed += new ElapsedEventHandler(Run);
            ListenerTimer.Interval = 1000;
            ListenerTimer.AutoReset = true;
            ListenerTimer.Elapsed += new ElapsedEventHandler(Listen);

            
            
            Log("Program Initialized");
        }

        public static void Log(string text, string level = "I")
        {
            List<byte[]> buffer = new List<byte[]>();
            byte[] btext = Encoding.UTF8.GetBytes($"[{DateTime.Now.ToShortTimeString()}] <{level}> {text}\r\n");
            LogFile.Write(btext,0,btext.Length);
            LogFile.Flush();
        }
    

    protected override void OnStart(string[] args)
        {
            WorkerTimer.Start();
            ListenerTimer.Start();
            Log("Timers started!");
        }

        protected override void OnStop()
        {
            Log("Stopping...");
            WorkerTimer.Stop();
            ListenerTimer.Stop();
            LogFile.Close();
        }

        protected override void OnShutdown()
        {
            Log("System Shutting down","W");
            LogFile.Close();
            WorkerTimer.Stop();
            ListenerTimer.Stop();
            base.OnShutdown();
        }

        public void Run(object s, ElapsedEventArgs e)
        {
            WorkerTimer.Stop();
            bool Killed = false;
            bool Found = false;
            int Time = Convert.ToInt32(DateTime.Now.ToString("HHmmss"));
            foreach (var timespan in AppliedConfig.Timespans)
            {
                if (Time >= timespan.Item1 && Time < timespan.Item2)
                {
                    Log($"In routine({AppliedConfig.Timespans.IndexOf(timespan)})");
                    List<Process> targets =
                        Process.GetProcesses().Where(p => AppliedConfig.Targets.Contains(p.ProcessName)).ToList();
                    foreach (var targetProcess in targets)
                    {
                        if (targetProcess.MainWindowHandle == IntPtr.Zero)
                        {
                            continue;
                        }
                        Configuration.DataObject data = Utils.SafeKill(targetProcess);
                        if (data.State == 0)
                        {
                            if (Records.Keys.Contains(((List<string>)data.Data)[0]))
                            {
                                Records[((List<string>)data.Data)[0]] = 1;
                            }
                            else
                            {
                                Records[((List<string>)data.Data)[0]]++;
                            }
                            Log($"Killed {((List<string>)data.Data)[0]}:{((List<string>)data.Data)[1]} ,times {Records[((List<string>)data.Data)[0]]}");
                        }
                        else
                        {
                            Log(data.Message,"E");
                        }
                    }
                }
                else
                {
                    Log("Idle Waiting for next schedule");
                }
            }

            if (urand)
            {
                WorkerTimer.Interval = rand.Next(AppliedConfig.MinDelay, AppliedConfig.MaxDelay);
            }
            else
            {
                WorkerTimer.Interval = AppliedConfig.MinDelay;
            }
            WorkerTimer.Start();
        }

        public void Listen(object s, ElapsedEventArgs e)
        {
            foreach (var Flag in Utils.FlagNames)
            {
                string path = AppliedConfig.ControlFolder + $"\\{Flag}{Utils.FlagExtension}";
                if (File.Exists(path))
                {
                    ListenerTimer.Stop();
                    Log("Control Received,verifying","C");
                    if (Identifier != Configuration.ExCode(File.ReadAllText(path),ControlKey))
                    {
                        if (File.Exists(path + ".x"))
                        {
                            File.Delete(path+".x");    
                        }
                        File.Copy(path,path+".x");
                        File.Delete(path);
                        Log("Invalid control,ignored","C");
                        continue;
                    }
                    Log("Valid control, Implementing");
                    switch (Flag)
                    {
                        case "STOP": 
                        {
                            Log("STOP REQUESTED,Stopping");
                            this.Stop();
                            break;
                        }
                        case "RELOAD":
                        {
                            Log("RELOAD REQUESTING,INVOKING");
                            Configuration.Load();
                            break;
                        }
                    }
                }
            }
            ListenerTimer.Interval = 1000;
            ListenerTimer.Start();
        } 
        
    }
}
