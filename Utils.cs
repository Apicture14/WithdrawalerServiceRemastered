using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Automation;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;

namespace WithdrawalerService
{
    public static class Utils
    {
        
        public static List<string> FlagNames = new List<string>() { "STOP", "RELOAD" };
        public static string FlagExtension = ".ctr";
        
        public static Configuration.DataObject Kill(Process process)
        {
            try
            {
                string name = process.ProcessName;
                int pid = process.Id;
                
                process.CloseMainWindow();
                process.Close();
                process.Kill();
                process.WaitForExit(1000);
                Configuration.DataObject retData = new Configuration.DataObject();
                retData.Data = new List<string>() { name, pid.ToString() };
                retData.State = 0;
                return retData;
            }
            catch (Exception ex)
            {
                Configuration.DataObject retData = new Configuration.DataObject();
                retData.Data = null;
                retData.State = -1;
                retData.Message = ex.Message;
                return retData;
            }
        }

        public static Configuration.DataObject SafeKill(Process process)
        {
            try
            {
                    string name = process.ProcessName;
                    int pid = process.Id;
                    if (process.MainWindowHandle == IntPtr.Zero)
                    {
                        //throw new ArgumentException($"{name}:{pid} has no window");
                        Configuration.DataObject d = new Configuration.DataObject();
                        d.Data = null;
                        d.State = -1;
                        d.Message = "Jumped";
                    }

                    AutomationElement ae = AutomationElement.FromHandle(process.MainWindowHandle);
                    ((WindowPattern)ae.GetCurrentPattern(WindowPattern.Pattern)).Close();
                    Configuration.DataObject retData = new Configuration.DataObject();
                    retData.Data = new List<string>() { name, pid.ToString() };
                    retData.State = 0;
                    return retData;
                    //Service1.Log($"Killed {name}:{pid}");
            }
            catch (Exception ex)
            {
                Configuration.DataObject retData = new Configuration.DataObject();
                retData.Data = null;
                retData.State = -1;
                retData.Message = ex.Message;
                return retData;
            }
            
        }
    }

    public static class Configuration
    {
        public struct Config
        {
            public string Identifier { get; set; }
            public int Version { get; set; }
            public List<string> Targets { get; set; }
            public List<Tuple<int,int>> Timespans { get; set; }
            public int MinDelay { get; set; }
            public int MaxDelay { get; set; }
            public string LogFolder { get; set; }
            public string ControlFolder { get; set; }
            
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
                "chrome",
                "Microsoft Edge",
                "msedge",
            };
            DefaultConfig.Timespans = new List<Tuple<int, int>>() { new Tuple<int, int>(000000, 235959) };
            DefaultConfig.MinDelay = 5000;
            DefaultConfig.MaxDelay = -1;
            DefaultConfig.ControlFolder = "C:\\yService\\controls";
            DefaultConfig.LogFolder = "C:\\yService\\logs";
            DefaultConfig.Identifier = ExCode(Service1.Identifier, Service1.ControlKey);
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
                        byte[] ReadBuffer = new byte[ReaderStream.Length];
                        string content = Encoding.UTF8.GetString(ReadBuffer, 0, ReadBuffer.Length);
                        Config retConfig = JsonConvert.DeserializeObject<Config>(content);
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

        public static bool Write(Config config,DirectoryInfo dir)
        {
            try
            {
                using (FileStream WriteStream =
                       new FileStream(dir.Name + "\\config.txt", FileMode.Create, FileAccess.Write))
                {
                    string content = JsonConvert.SerializeObject(config,Formatting.Indented);
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
            Configuration.DataObject d = Configuration.Read(Service1.ConfigPath);
            if (d.State != 0)
            {
                Service1.AppliedConfig = Service1.DefaultConfig;
            }
            else
            {
                Service1.AppliedConfig = (Configuration.Config)d.Data;
                load = true;
            }
            if (load)
            {
                Service1.Log($"Loaded Config {Service1.ConfigPath}","C");
            }

            if (Service1.AppliedConfig.MaxDelay != -1)
            {
                Service1.urand = true;
                Service1.Log($"Random Delay Enabled From {Service1.AppliedConfig.MinDelay} to {Service1.AppliedConfig.MaxDelay}","C");
            }
            else
            {
                Service1.Log($"Random Delay Disabled ,Delay is {Service1.AppliedConfig.MinDelay}","C");
            }

            StringBuilder sbtargets = new StringBuilder();
            StringBuilder sbtimespans = new StringBuilder();
            sbtargets.Append($"Targets:{Service1.AppliedConfig.Targets.Count}\r\n");
            foreach (var target in Service1.AppliedConfig.Targets)
            {
                sbtargets.Append(target + "\r\n");
            }
            sbtimespans.Append($"TimeSpans:{Service1.AppliedConfig.Timespans.Count}\r\n");
            for (int i=0;i<Service1.AppliedConfig.Timespans.Count;i++)
            {
                sbtimespans.Append($"TimeSpan({i}) {Service1.AppliedConfig.Timespans[i].Item1} to {Service1.AppliedConfig.Timespans[i].Item2}");
            }
            Service1.Log(sbtargets.ToString(),"C");
            Service1.Log(sbtimespans.ToString(),"C");
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