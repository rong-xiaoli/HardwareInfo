using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using System.Windows.Threading;
using System.Management;
using System.Diagnostics;
using HardwareInfo;
using System.Threading;
using OpenHardwareMonitor.Hardware;
using System.Text.RegularExpressions;
using System.Security.Principal;
using System.ComponentModel;
using System.Windows;

namespace HardwareInfo
{
	public static class GlobalVar
	{
		/// <summary>
		/// Variable of debug mode. 
		/// </summary>
		public static bool DebugMode = false;
		/// <summary>
		/// The log class. Including the constant vars. 
		/// </summary>
		public static class Log
		{
			public static string Name
			{
				get
				{
					return
						DateTime.Now.Year.ToString()
				+ "-" + DateTime.Now.Month.ToString()
				+ "-" + DateTime.Now.Day.ToString()
				+ "." + DateTime.Now.Hour.ToString()
				+ "." + DateTime.Now.Minute.ToString()
				+ ".log";
				}
				set { throw new Exceptions.UnexpectedUsageException("The log file name could not be setted manually"); }
			}
            public static bool IsLogAvailable = false;
			public static string Path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\HardwareInfo";
			public static StreamWriter Writer;
		}
		/// <summary>
		/// Is this computer x32?
		/// </summary>
		public static bool x32Mode = false;
		/// <summary>
		/// Is app using x32 methods?
		/// </summary>
		public static bool x32Swich = true;

        //ProgressBar refreshing vars. 
		public static int RefreshSpeed = 1000;
		public static List<string> CPUInfo = new List<string>();
		public static List<string> GPUInfo = new List<string>();
		public static List<string> AdvCPUInfo = new List<string>();

        public static ManualResetEvent Refresh = new ManualResetEvent(false);

		//ProcessorCounter vars.
		public static Functions.ProcessorUsage Processor;

		//OpenHardwareMonitor vars.
		public static Functions.UpdateVisitor visitor = new Functions.UpdateVisitor();
		public static Computer computer = new Computer();

		//Initialization vars. 
		public static bool IsTotalInitialized = false;
	}
	public class Functions
	{
		//Info collectiong class. 
		/// <summary>
		/// https://www.cnblogs.com/mschen/p/8031110.html
		/// </summary>
		public class ProcessorUsage
		{
			protected object syncLock = new object();
			protected PerformanceCounter counter;
			protected int lastSample;
			protected DateTime lastSampleTime;

			public ProcessorUsage()
			{
				counter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
			}
			public int GetCurrentValue()
			{
				if ((DateTime.UtcNow - lastSampleTime).TotalMilliseconds > GlobalVar.RefreshSpeed)
				{
					lock (syncLock)
					{
						if ((DateTime.UtcNow - lastSampleTime).TotalMilliseconds > GlobalVar.RefreshSpeed)
						{
							lastSample = (int)counter.NextValue();
							lastSampleTime = DateTime.UtcNow;
						}
					}
				}
				return lastSample;
			}
		}
		public class UpdateVisitor : IVisitor
		{
			//MPL Licence v2.0 was included in the readme.md 
			/*
			 * This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
			 * If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
			 * If it is not possible or desirable to put the notice in a particular file, 
			 * then You may include the notice in a location(such as a LICENSE file in a relevant directory) 
			 * where a recipient would be likely to look for such a notice.
			 */
			public void VisitComputer(IComputer computer)
			{
				computer.Traverse(this);
			}
			public void VisitHardware(IHardware hardware)
			{
				hardware.Update();
				foreach (IHardware subHardware in hardware.SubHardware)
				{
					subHardware.Accept(this);
				}
			}
			public void VisitSensor(ISensor sensor) { }
			public void VisitParameter(IParameter parameter) { }
		}
		/// <summary>
		/// Class for CPU & GPU Params.
		/// </summary>
		public static class MClassName
		{
			public static string CPU = "Win32_Processor";
			public static string GPU = "Win32_VideoController";
		}

		/// <summary>
		/// Initialization & checking methods. 
		/// </summary>
		public static class Initialize
		{
			public struct CPUBriefInfo
			{
				public string AddressWidth;
				public string Architecture;
				public string CpuStatus;
				public string CurrentVoltage;
				public string CurrentClockSpeed;
				public string MaxClockSpeed;
			}
			/// <summary>
			/// OpenHardwareMonitorLib initialize
			/// </summary>
			public static void OHWM_Initialize()
			{
				//Enable CPU, GPU, RAM, HDD
				GlobalVar.computer.CPUEnabled = true;
				GlobalVar.computer.GPUEnabled = true;
				GlobalVar.computer.RAMEnabled = true;
				GlobalVar.computer.HDDEnabled = true;
                if (GlobalVar.DebugMode)
                {
                    Log.Debug("OHWM enabled. ");
                }
				//Initialize vars. 
				GlobalVar.computer.Open();
                Log.Info("OHWM initialized. ");
			}
			/// <summary>
			/// Log function initialize.
			/// </summary>
			public static void Log_Initialize()
			{
				try
				{
					if (!Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)+"\\HardwareInfo"))
					{
						Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)+"\\HardwareInfo");
					}
                    GlobalVar.Log.Writer = new StreamWriter(GlobalVar.Log.Path + GlobalVar.Log.Name)
                    {
                        AutoFlush = true
                    };
                    Log.Info("Log system initialized. ");
                }
				catch (Exception)
				{
					throw; 
				}
			}
			/// <summary>
			/// User interface data initialize. Returns the information data: 
			/// <para/>
			/// AddressWidth;
			/// Architecture;
			/// CpuStatus;
			/// CurrentVoltage;
			/// CurrentClockSpeed;
			/// MaxClockSpeed.
			/// </summary>
			/// <returns>A string array. </returns>
			public static CPUBriefInfo UI_Initialize()
			{
				CPUBriefInfo ret;
				//initialize ret, otherwise compile fails.
				ret.AddressWidth = "";
				ret.Architecture = "";
				ret.CpuStatus = "";
				ret.CurrentClockSpeed = "";
				ret.CurrentVoltage = "";
				ret.MaxClockSpeed = "";
                if (GlobalVar.DebugMode)
                {
                    Log.Debug("UI initialization ret initialized. ");
                }

				ManagementObjectCollection MOC = GetInfo.x64GetInfo.MOCCPU();
				foreach (ManagementObject mo in MOC)
				{
                    if (GlobalVar.DebugMode)
                    {
                        Log.Debug("MO +1 ");
                    }
					foreach (PropertyData pd in mo.Properties)
					{
						if (mo[pd.Name] != null && mo[pd.Name].ToString() != "")
						{
							switch (pd.Name)
							{
								default:
									break;
								case ("AddressWidth"):
									ret.AddressWidth = mo[pd.Name].ToString();
									break;
								case ("Architecture"):
									ret.Architecture = mo[pd.Name].ToString();
									break;
								case ("CpuStatus"):
									ret.CpuStatus = Data.CpuStatus((UInt16)mo[pd.Name]);
									break;
								case ("CurrentVoltage"):
									ret.CurrentVoltage = (((UInt16)mo[pd.Name]) / 10).ToString();
									break;
								case ("CurrentClockSpeed"):
									ret.CurrentClockSpeed = mo[pd.Name].ToString() + "MHz";
									break;
								case ("MaxClockSpeed"):
									ret.MaxClockSpeed = mo[pd.Name].ToString() + "MHz";
									break;
							}
						}
					}
				}
                Log.Info("UI initialized. ");
				return ret;
			}
		}
		/// <summary>
		/// Initial checking functions.
		/// </summary>
		public static class InitializeCheck
		{
			/// <summary>
			/// Check if CPU is x32 or x64.
			/// </summary>
			public static bool x32x64check()
			{
				return !Directory.Exists("C:\\Program Files (x86)");
			}
		}
		
		/// <summary>
		/// Info gathering. 
		/// </summary>
		public static class GetInfo
		{
			//Info collecting methods: CIM. 
			public static class x64GetInfo
			{
				public static ManagementObjectCollection MOCCPU()
				{
					ManagementClass managementClass = new ManagementClass(MClassName.CPU);
					ManagementObjectCollection ret = managementClass.GetInstances();
					return ret;
				}
				public static ManagementObjectCollection MOCGPU()
				{
					ManagementClass mc = new ManagementClass(MClassName.GPU);
					ManagementObjectCollection ret = mc.GetInstances();
					return ret;
				}

				/// <summary>
				/// Get CPU info from CIM.
				/// <para>Return in format: List</para>
				/// </summary>
				/// <returns>CPU info. </returns>
				public static List<string> ListTCPU()
				{
					List<string> ManagementList = new List<string>();
					ManagementObjectCollection managementObjectCollection = MOCCPU();
					foreach (ManagementObject mo in managementObjectCollection)
					{
						foreach (PropertyData pd in mo.Properties)
						{
							if (mo[pd.Name] != null && mo[pd.Name].ToString() != "")
							{
								ManagementList.Add(string.Format("{0}:{1}\n", pd.Name, mo[pd.Name]));
							}
						}
					}
					return ManagementList;
				}
				/// <summary>
				/// Gets GPU info from CIM.
				/// <para>Return in format: List</para>
				/// </summary>
				/// <returns>GPU info</returns>
				public static List<string> ListTGPU()
				{
					List<string> ret = new List<string>();
					ManagementObjectCollection moc = MOCGPU();
					foreach (ManagementObject mo in moc)
					{
						foreach (PropertyData pd in mo.Properties)
						{
							if (mo[pd.Name] != null && mo[pd.Name].ToString() != "")
							{
								ret.Add(string.Format("{0}:{1}\n", pd.Name, mo[pd.Name]));
							}
						}
					}
					return ret;
				}
			}
			public static class x32GetInfo
			{
				/// <summary>
				/// Get CPU info from OHWM. Return in format: List
				/// </summary>
				/// <returns>CPU info</returns>
				public static List<string> OHWMGetCPUInfo()
				{
					List<string> ret = new List<string>();
					GlobalVar.computer.Accept(GlobalVar.visitor);
					for (int i = 0; i < GlobalVar.computer.Hardware.Length; i++)
					{
						if (GlobalVar.computer.Hardware[i].HardwareType==HardwareType.CPU)
						{
							for (int j = 0; j < GlobalVar.computer.Hardware[i].Sensors.Length; j++)
							{
								if (GlobalVar.computer.Hardware[i].Sensors[j].Value==null)
								{
									System.Windows.Forms.MessageBox.Show(
										"Sensor value is null. Recommended to retry in Administrator mode. ",
										"Caution",
										System.Windows.Forms.MessageBoxButtons.OK,
										System.Windows.Forms.MessageBoxIcon.Information
										);
								}
								ret.Add(GlobalVar.computer.Hardware[i].Sensors[j].Name + ":" + GlobalVar.computer.Hardware[i].Sensors[j].Value.ToString() + "\n");
							}
						}
					}
					return ret;
				}
				/// <summary>
				/// Get GPU info from OHWM. Return in format: List
				/// </summary>
				/// <returns>GOU info</returns>
				public static List<string> OHWMGetGPUInfo()
				{
					List<string> ret = new List<string>();
					GlobalVar.computer.Accept(GlobalVar.visitor);
					for (int i = 0; i < GlobalVar.computer.Hardware.Length; i++)
					{
						if (GlobalVar.computer.Hardware[i].HardwareType==HardwareType.GpuAti)
						{
							for (int j = 0; j < GlobalVar.computer.Hardware[i].Sensors.Length; j++)
							{
								ret.Add(GlobalVar.computer.Hardware[i].Sensors[j].Name + ":" + GlobalVar.computer.Hardware[i].Sensors[j].Value.ToString() + "\n");
							}
						}
						else if (GlobalVar.computer.Hardware[i].HardwareType==HardwareType.GpuNvidia)
						{
							for (int j = 0; j < GlobalVar.computer.Hardware[i].Sensors.Length; j++)
							{
								ret.Add(GlobalVar.computer.Hardware[i].Sensors[j].Name + ":" + GlobalVar.computer.Hardware[i].Sensors[j].Value.ToString() + "\n");
							}
						}
					}
					return ret;
				}
			}
		}
		
		/// <summary>
		/// Usage collecting methods. 
		/// </summary>
		public static class UsageRate
		{
			/// <summary>
			/// CPU usage: default route.
			/// </summary>
			/// <returns>Value of CPU usage(double). </returns>
			public static double CPU()
			{
				if (GlobalVar.x32Swich)
				{
					return x32CPU();
				}
				else
				{
					return x64CPU();
				}
			}
			/// <summary>
			/// CPU usage. 
			/// </summary>
			/// <param name="x32Mode">In x32 mode?</param>
			public static double CPU(bool x32Mode)
			{
				if (x32Mode)
				{
					return x32CPU();
				}
				else
				{
					return x64CPU();
				}
			}
			private static double x32CPU()
			{
				double ret=0;
				List<string> res = new List<string>();
				GlobalVar.computer.Accept(GlobalVar.visitor);
				for (int i = 0; i < GlobalVar.computer.Hardware.Length; i++)
				{
					if (GlobalVar.computer.Hardware[i].HardwareType==HardwareType.CPU)
					{
						for (int j = 0; j < GlobalVar.computer.Hardware[i].Sensors.Length; j++)
						{
							if (GlobalVar.computer.Hardware[i].Sensors[j].SensorType==SensorType.Load)
							{
								ret = (double)GlobalVar.computer.Hardware[i].Sensors[j].Value;
							}
						}
					}
				}
				return ret;
			}
			private static double x64CPU()
			{
				List<string> res = new List<string>();
				double ret = 0;
				res = GetInfo.x64GetInfo.ListTCPU();
				string con1 = @"(?<grp0>(?<=LoadPercentage:).+)";
				foreach (string source in res)
				{
					foreach (Match match1 in Regex.Matches(source,con1))
					{
						if (ret!=0)
						{
							throw new Exceptions.UnexpectedResultException("Unexpected 2 values in a single result");
						}
						try
						{
							ret = Convert.ToDouble(match1.Value);
						}
						catch (FormatException fe)
						{
							Log.Fatal("Regex failed to match the CPU load percent. ");
							if (GlobalVar.DebugMode)
							{
								Log.Debug(fe.Message);
								throw fe;
							}
						}
					}
				}
				return ret;
			}
		}

		/// <summary>
		/// Generate crash report. 
		/// </summary>
		public class CrashReport
		{
			internal protected string[] CrashMessage =
			{
				"Is this my fault?\n",
				"Ohh, that's bad...\n",
				"I think I didn't notice...\n",
				"x(\n",
				"X.X\n",
				"Where is my KeBugCheckEx()?\n",
				"1,2,3... Where is the next exception?\n"
			};
			Random rand = new Random();
			private const string Windows2000 = "5.0";
			private const string WindowsXP = "5.1";
			private const string Windows2003 = "5.2";
			private const string Windows2008 = "6.0";
			private const string Windows7 = "6.1";
			private const string Windows8OrWindows81 = "6.2";
			private const string Windows10 = "10.0";
			public string ProgramVersion
			{
				get
				{
					return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
				}
			}
			public Exception exception;
			public string SystemVersion
			{
				get
				{
					switch (System.Environment.OSVersion.Version.Major + "." + System.Environment.OSVersion.Version.Minor)
					{
						case Windows2000:
							return "Windows2000";
						case WindowsXP:
							return "WindowsXP";
						case Windows2003:
							return "Windows2003";
						case Windows2008:
							return "Windows2008";
						case Windows7:
							return "Windows7";
						case Windows8OrWindows81:
							return "Windows8 or Windows8.1";
						case Windows10:
							return "Windows10";
						default:
							return "Unknown system";
					}
				}
			}
			public string message;
			public CrashReport(Exception TriggerException)
			{
				exception = TriggerException;
				message = CrashMessage[rand.Next(CrashMessage.Length)];
			}
		}
	}
	public class Exceptions
	{
		public class UnexpectedResultException: ApplicationException
		{
			public UnexpectedResultException() { }
			public UnexpectedResultException(string message) { }
			public UnexpectedResultException(string message,Exception innerException) { }
		}
		
		public class UnexpectedRouteException:ApplicationException
		{
			public UnexpectedRouteException() { }
			public UnexpectedRouteException(string message) { }
			public UnexpectedRouteException(string message,Exception innerException) { }
		}

		public class UnexpectedUsageException:ApplicationException
		{
			public UnexpectedUsageException() { }
			public UnexpectedUsageException(string message) { }
			public UnexpectedUsageException(string message,Exception innerException) { }
		}

		public class UnexpectedLogicException:ApplicationException
		{
			public UnexpectedLogicException() { }
			public UnexpectedLogicException(string message) { }
			public UnexpectedLogicException(string message,Exception innerException) { }
		}

		public class UnexpectedInputException:ApplicationException
		{
			public UnexpectedInputException() { }
			public UnexpectedInputException(string message) { }
			public UnexpectedInputException(string message,Exception innerException) { }
		}
	}
	public static class Log
	{
		/// <summary>
		/// Write a debug message to the log flow. 
		/// </summary>
		/// <param name="message">The debug message. </param>
		public static void Debug(string message)
		{
			GlobalVar.Log.Writer.WriteLine("[Debug]:" + message + "\n");
		}
		/// <summary>
		/// Write a log to the log flow. Log level: Info
		/// </summary>
		/// <param name="message">Info message. </param>
		public static void Info(string message)
		{
			GlobalVar.Log.Writer.WriteLine("[Info]: " + message + "\n");
		}
		/// <summary>
		/// Write a log to the log flow. Log level: Warn. 
		/// </summary>
		/// <param name="message">Warn message. </param>
		public static void Warn(string message)
		{
			GlobalVar.Log.Writer.WriteLine("[Warn]: " + message + "\n");
		}
		/// <summary>
		/// Write a log to the log flow. Log level: Error. 
		/// </summary>
		/// <param name="message">Error message. </param>
		public static void Error(string message)
		{
			GlobalVar.Log.Writer.WriteLine("[Error]:" + message + "\n");
		}
		/// <summary>
		/// Write a log to the log flow. Log level: Fatal. 
		/// </summary>
		/// <param name="message">Fatal message. </param>
		public static void Fatal(string message)
		{
			GlobalVar.Log.Writer.WriteLine("[Fatal]:" + message + "\n");
		}
        /// <summary>
        /// <para> Ready to exit and generate a report. DO NOT EXECUTE LOG METHODS AFTER EXECUTED. </para>
        ///  If other info need to be recorded, use the other method. 
        /// </summary>
        /// <param name="exception">The trigger exception. </param>
        /// <param name="furtherinfo">Comments of the exception. </param>
        /// <param name="stopcode">The stopcode of exiting. </param>
        public static void Exception(Exception exception, string furtherinfo, int stopcode)
		{
			System.Windows.Forms.MessageBox.Show("A fatal error occured. Please send the log file and the crash report to the author. ", "Application crashed!");
			if (!Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)+"\\HardwareInfo\\"))
			{
				Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)+"\\HardwareInfo");
			}
			StreamWriter ew = new StreamWriter(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)+"\\HardwareInfo\\CrashReport.txt");
			GlobalVar.Log.Writer.Flush();
			GlobalVar.Log.Writer.Dispose();
			//StreamWriter Cleaned.
			Functions.CrashReport cr = new Functions.CrashReport(exception);
			ew.WriteLine(
				"+--------------------------------+\n" +
				"|          Crash report          |\n" +
				"+--------------------------------+\n" +
				cr.message +
				"Ooops, something went wrong. Please submit this file to the author. \n" +
				"-=-=-=-=-=PC Information=-=-=-=-=-" +
				"Program Version: " + cr.ProgramVersion + "\n" +
				"Platform: \n" +
				cr.SystemVersion +
				"\nFurther information: \n" +
				furtherinfo
				+"\n\n\nThanks for your support. "
				);
			ew.Flush();
            ew.Dispose();
            Threads.UIThread.Abort();
            GlobalVar.Refresh.Dispose();
            Environment.Exit(stopcode);
		}
        /// <summary>
        /// Log an exception. 
        /// </summary>
        /// <param name="exception">The trigger exception. </param>
        /// <param name="furtherinfo">Comments of the exception. </param>
        /// <param name="stopcode">The stopcode of exiting. </param>
        /// <param name="IsCrashNeeded">If need, set to true. </param>
        public static void Exception(Exception exception, string furtherinfo, int stopcode, bool IsCrashNeeded)
        {
            if (IsCrashNeeded)
            {
                GlobalVar.Log.Writer.Write("[----------Exception----------]\n");
                GlobalVar.Log.Writer.Write("Exception: " + exception + "\n");
                GlobalVar.Log.Writer.Write("Exception stack: \n");
                GlobalVar.Log.Writer.Write(exception.StackTrace + "\n");
                GlobalVar.Log.Writer.Write("[------Continue running------]");
            }
            else
            {
                Exception(exception, furtherinfo, stopcode);
            }
        }
	}
	public static class Data
	{
		public static string CpuStatus(UInt16 StatusCode)
		{
			switch (StatusCode)
			{
				default:
					throw new Exceptions.UnexpectedInputException("Unexpected route \"default\" while querying CpuStatus.");
				case (1):
					return "Enabled";
				case (2):
					return "Disabled by User via BIOS Setup";
				case (3):
					return "Disabled By BIOS (POST Error)";
				case (4):
					return "Idle";
				case (5):
					return "Unexpected code:5(Reserved)";
				case (6):
					return "Unexpected code:6(Reserved)";
				case (7):
					return "Unexpected code:7(Other)";
			}
		}
	}
	public static class DispatcherHelper
	{
		public static Dispatcher dispatcher
		{
			get;
			private set;
		}
		public static void CheckBeginInvokeOnUI(Action action)
		{
			if (dispatcher.CheckAccess())
			{
				action();
			}
			else
			{
				dispatcher.Invoke(action);
			}
		}
		public static void Initialize()
		{
			if (dispatcher != null) 
			{
				return;
			}

#if SILVERLIGHT
			dispatcher = Deployment.Current.Dispatcher;
#else
			dispatcher = Dispatcher.CurrentDispatcher;
#endif
		}
	}
	public static class Threads
	{
        public static Thread UIThread = new Thread(ThreadFunc.UIThreadFunc)
        {
            IsBackground = true, Priority = ThreadPriority.BelowNormal
        };
		public static class ThreadFunc
		{
			public static void UIThreadFunc()
			{
                try
                {
                    double status = 0;
			    	while (true)
			    	{
			       		Thread.Sleep(GlobalVar.RefreshSpeed);
                        GlobalVar.Refresh.WaitOne();
                        status = Functions.UsageRate.CPU();
				    	DispatcherHelper.CheckBeginInvokeOnUI(() =>
					    {
                         MainWindow.mainWindow.CPUUsageBar.Value = status;
			    		});
		    		}
                }
                catch (ThreadAbortException)
                {
                    
                }
                catch(Exception e)
                {
                    Log.Exception(e, "Unknown further info. ", (int)StopCode.UnknownReason, true);
                }
			}
		}
	}
    public enum StopCode
    {
        NormalExit=0,
        RouteNotExpected,
        ResultNotExpected,
        LogicChaos,
        InputNotExpected,
        UsageNotExpected,
        LogStreamDisposed,
        ThreadAborted,
        ValueOutOfRange,
        LibraryNotFound,
        StackOverFlow,
        InitializeFailed,
        UnknownReason=-1
    }
}
