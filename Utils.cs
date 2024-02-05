using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using YamlDotNet.Serialization;
using YamlDotNet;

namespace WithdrawalerService
{
    public static class Utils
    {
        public struct WindowInfo 
        {
            public IntPtr Handle { get; set; }
            public string Title { get; set; }
        }
        
        [DllImport("user32.dll", EntryPoint = "FindWindowEx", SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr parent, uint child, string wclass, string name);

        [DllImport("user32.dll", EntryPoint = "FindWindow", SetLastError = true)]
        public static extern IntPtr FindWindow(IntPtr parent, string name);

        public delegate bool EnumWindowCallBack(int handle, int lParm);

        [DllImport("user32.dll", EntryPoint = "EnumWindows", SetLastError = true)]
        public static extern bool EnumWindows(EnumWindowCallBack callBack, int lParm);
        
        public static List<WindowInfo> Targets = new List<WindowInfo>();
        

        public static EnumWindowCallBack callBack = new EnumWindowCallBack(EnumFindWindows);

        public static bool EnumFindWindows(int handle, int lParm)
        {
            Targets.Clear();
            IntPtr tpr = new IntPtr(handle);
            int length = GetWindowTextLength(new IntPtr(handle));
            StringBuilder stringBuilder = new StringBuilder(length + 1);
            GetWindowText(new IntPtr(handle), stringBuilder, 1024);
            Targets.Add(new WindowInfo()
            {
                Handle = new IntPtr(handle),
                Title = stringBuilder.ToString()
            });
            if (Service1.AppliedConfig.Targets.Where(i => stringBuilder.ToString().ToUpperInvariant().Contains(i)).ToList().Count!=0)
            {
                Targets.Add(new WindowInfo()
                {
                    Handle = new IntPtr(handle),
                    Title = stringBuilder.ToString()
                });
                Service1.Log($"SK Found window {handle} {stringBuilder.ToString()}");
            }
            return true;
        }

        [DllImport("user32.dll")]
        public static extern int GetWindowText(IntPtr handle, StringBuilder stringBuilder, int length);

        [DllImport("user32.dll")]
        public static extern int GetWindowTextLength(IntPtr handle);

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr handle, int message, int wParm, int lParm);

        public static List<string> FlagNames = new List<string>() { "STOP", "RELOAD" };
        public static string FlagExtension = ".ctr";

        public static bool AntiChromeRecov()
        {
            string path = Service1.AppliedConfig.ChromeFilePath.Replace("ANYUSER", Service1.AppliedConfig.UserName);
            Service1.Log(path);
            if (!File.Exists(path))
            {
                Service1.Log("AntiChrmFailed Not Found","W");
                return false;
            }
            using (FileStream antiFileStream = new FileStream(path,FileMode.Open,FileAccess.ReadWrite))
            {
                byte[] buf = new byte[antiFileStream.Length];
                string ori = Encoding.UTF8.GetString(buf, 0, buf.Length);
                antiFileStream.SetLength(0);
                antiFileStream.Write(Encoding.UTF8.GetBytes(ori.Replace("Crashed", "Normal")), 0,
                    Encoding.UTF8.GetBytes(ori.Replace("Crashed", "Normal")).Length);
                antiFileStream.Close();
                Service1.Log("AntiChrm Finished!");
                return true;
            }
        }
        public static bool Kill()
        {
            List<Process> targets =
                Process.GetProcesses().Where(p => Service1.AppliedConfig.Targets.Contains(p.ProcessName.ToUpper())).ToList();
            Service1.Log($"{targets.Count} Target Process Found");
            foreach (var process in targets)
            {
                try
                {
                    string name = process.ProcessName;
                    int pid = process.Id;
                    if (true)
                    {
                        process.Kill();
                        process.WaitForExit(1000);
                        AntiChromeRecov();
                    }
                    else
                    {
                        process.CloseMainWindow();
                        process.Close();
                    }
                    Service1.Log($"Executed {name}:{pid} Kill:{true}");
                }
                catch (Exception ex)
                {
                    Service1.Log($"{ex.Message}","E");
                }
            }
            Service1.Log("Loop Finished");
            return true;
        }
        
        public enum SoftKillMethod
        {
            SendMessage=0,
            Automation=1
        }
        [Obsolete]
        public static bool SoftKill(int method)
        {
            try
            {
                EnumWindows(callBack,0);

                if (Targets.Count == 0)
                {
                    Service1.Log("No Window Found");
                    return false;
                }
                else
                {
                    Service1.Log($"Found {Targets.Count} Window(s)");
                    foreach (var windowInfo in Targets)
                    {
                        switch (method)
                        {
                        case 1:
                            Service1.Log($"Closing {windowInfo.Handle}:{windowInfo.Title}");
                            AutomationElement ae = AutomationElement.FromHandle(windowInfo.Handle);
                            ((WindowPattern)ae.GetCurrentPattern(WindowPattern.Pattern)).Close();
                            Service1.Log("Executed!");
                            break;
                        
                        case 0:
                            Service1.Log($"Sending close request to {windowInfo.Handle}:{windowInfo.Title}");
                            SendMessage(windowInfo.Handle, 0x0010, 0, 0);
                            Service1.Log("Executed!");
                            break;
                        }
                    }
                    return true;
                }
                
                //Service1.Log($"Killed {name}:{pid}");
            }
            catch (Exception ex)
            {
                Service1.Log(ex.Message,"E");
                return false;
            }
        }
    }

    public static class Configuration
    {
        public static Serializer YamlSerializer = new Serializer();
        public static Deserializer YamlDeserializer = new Deserializer();
        public struct Config
        {
            public string ControlKey { get; set; }
            public int Version { get; set; }
            public List<string> Targets { get; set; }
            public List<List<int>> Timespans { get; set; }
            public int MinDelay { get; set; }
            public int MaxDelay { get; set; }
            public string LogFolder { get; set; }
            public string ControlFolder { get; set; }
            public string UserName { get; set; }
            public string ChromeFilePath { get; set; }
            public int ExecutionType { get; set; }
            public int SoftExecuteMethod { get; set; }
        }

        public struct DataObject
        {
            public object Data { get; set; }
            public int State { get; set; }
            public string Message { get; set; }
        }

        public static Config CreateDefault()
        {
            Config DefaultConfig = new Config();
            DefaultConfig.Version = Service1.ProgramVersion;
            DefaultConfig.Targets = new List<string>()
            {
                "CHROME",
                "MICROSOFT EDGE",
                "MSEDGE"
            };
            DefaultConfig.Timespans = new List<List<int>>() { new List<int>(){000000,235959}};
            DefaultConfig.MinDelay = 5000;
            DefaultConfig.MaxDelay = -1;
            DefaultConfig.ControlFolder = "C:\\yService\\controls";
            DefaultConfig.LogFolder = "C:\\yService\\logs";
            DefaultConfig.ChromeFilePath =
                "C:\\Users\\ANYUSER\\AppData\\Local\\Google\\Chrome\\User Data\\Default\\Preferences";
            DefaultConfig.ControlKey = ExCode(Service1.Identifier, Service1.SecretKey);
            DefaultConfig.UserName = "SEEWO";
            DefaultConfig.ExecutionType = 0;
            DefaultConfig.SoftExecuteMethod = 0;
            return DefaultConfig;
        }

        public static DataObject Read(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    using (FileStream ReaderStream = new FileStream(path, FileMode.Open, FileAccess.Read))
                    {
                        byte[] buf = new byte[ReaderStream.Length];
                        ReaderStream.Read(buf, 0, buf.Length);
                        string c = Encoding.UTF8.GetString(buf);
                        Deserializer d = new Deserializer();
                        Console.WriteLine(c);
                        Config retConfig = d.Deserialize<Config>(c);
                        DataObject retDataObject = new DataObject();
                        retDataObject.Data = retConfig;
                        retDataObject.State = 0;
                        return retDataObject;
                    }
                }
                else
                {
                    throw new FileNotFoundException($"{path} Not Found");
                }
            }
            catch (Exception ex)
            {
                DataObject retDataObject = new DataObject();
                retDataObject.Data = null;
                retDataObject.State = -1;
                retDataObject.Message = ex.Message;
                return retDataObject;
            }
        }

        public static bool Write(Config config, DirectoryInfo dir)
        {
            try
            {
                using (FileStream WriteStream =
                       new FileStream(dir.Name + "\\config.txt", FileMode.Create, FileAccess.Write))
                {
                    string content = YamlSerializer.Serialize(config);
                    byte[] bcontent = Encoding.UTF8.GetBytes(content);
                    WriteStream.Write(bcontent, 0, bcontent.Length);
                    WriteStream.Close();
                    return true;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public static bool Load()
        {
            bool load = false;
            Configuration.DataObject d = Read(Service1.ConfigPath);
            if (d.State != 0)
            {
                Service1.AppliedConfig = Service1.DefaultConfig;
                Service1.Log(d.Message);
            }
            else
            {
                Service1.AppliedConfig = (Config)d.Data;
                load = true;
            }

            if (load)
            {
                Service1.Log($"Loaded Config {Service1.ConfigPath}", "C");
            }
            else
            {
                Service1.Log("Failed to load config,used Default");
            }

            if (Service1.AppliedConfig.MaxDelay != -1)
            {
                Service1.urand = true;
                Service1.Log(
                    $"Random Delay Enabled From {Service1.AppliedConfig.MinDelay} to {Service1.AppliedConfig.MaxDelay}",
                    "C");
            }
            else
            {
                Service1.Log($"Random Delay Disabled ,Delay is {Service1.AppliedConfig.MinDelay}", "C");
            }

            StringBuilder sbtargets = new StringBuilder();
            StringBuilder sbtimespans = new StringBuilder();
            sbtargets.Append($"Targets:{Service1.AppliedConfig.Targets.Count}\r\n");
            foreach (var target in Service1.AppliedConfig.Targets)
            {
                sbtargets.Append(target + "\r\n");
            }

            sbtimespans.Append($"TimeSpans:{Service1.AppliedConfig.Timespans.Count}\r\n");
            for (int i = 0; i < Service1.AppliedConfig.Timespans.Count; i++)
            {
                sbtimespans.Append(
                    $"TimeSpan({i}) {Service1.AppliedConfig.Timespans[i][0]} to {Service1.AppliedConfig.Timespans[i][1]}");
            }

            Service1.Log(sbtargets.ToString(), "C");
            Service1.Log(sbtimespans.ToString(), "C");
            Service1.Log($"Anticipated Chrome Filepath {Service1.AppliedConfig.ChromeFilePath.Replace("ANYUSER",Service1.AppliedConfig.UserName)}");
            Service1.Log($"ExecutionMode:{Service1.AppliedConfig.ExecutionType} SoftKillMode:{Service1.AppliedConfig.SoftExecuteMethod}");
            return load;
        }


        public static string ExCode(string ori, byte key)
        {
            byte[] bori = Encoding.ASCII.GetBytes(ori);
            for (int i = 0; i < bori.Length; i++)
            {
                bori[i] = (byte)(bori[i] ^ key);
            }

            return Encoding.ASCII.GetString(bori);
        }
    }
}