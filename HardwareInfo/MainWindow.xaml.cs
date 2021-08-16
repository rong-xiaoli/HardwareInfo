using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.Windows.Threading;
using System.Diagnostics;
using HardwareInfo;
using System.Threading;
using OpenHardwareMonitor.Hardware;
using System.IO;
using System.ComponentModel;

namespace HardwareInfo
{
	/// <summary>
	/// MainWindow.xaml 的交互逻辑
	/// </summary>
	public partial class MainWindow : Window
	{
		public static MainWindow mainWindow;

		public MainWindow()
		{
			InitializeComponent();
			RefreshTimeLabel.Content = RefreshTime.Value.ToString();
			mainWindow = this;
			DispatcherHelper.Initialize();
            if (GlobalVar.DebugMode)
            {
                Log.Debug("DispatcherHelper initialized. ");
            }
			GlobalVar.IsTotalInitialized = true;
            Log.Info("Component initialization complete. ");
		}

		private void MainForm_Loaded(object sender, RoutedEventArgs e)
		{
			//Initializing.
			Functions.Initialize.Log_Initialize();
			Functions.Initialize.OHWM_Initialize();
            Log.Info("Log & OHWM module initialized. ");

			//Checking platform.
			if (Functions.InitializeCheck.x32x64check())
			{
                Log.Warn("x32 platform detected! ");
				MessageBox.Show("Warning: This application is NOT THREADSAFE in x32 platform. Swiching to x32 mode...", "Platform Securety Check:");
				GlobalVar.x32Mode = true;
				GlobalVar.x32Swich = true;
				x32SwichCheckBox.IsEnabled = false;
                if (GlobalVar.DebugMode)
                {
                    Log.Debug("x32Swich locked. ");
                }
			}
			else
			{
				x32SwichCheckBox.IsEnabled = true;
				GlobalVar.x32Swich = false;
                Log.Info("x64 platform detected. ");
			}
			//Initialize CPU and GPU info.
			GlobalVar.CPUInfo = Functions.GetInfo.x64GetInfo.ListTCPU();
			GlobalVar.GPUInfo = Functions.GetInfo.x64GetInfo.ListTGPU();
            Log.Info("CPU, GPU info gathered. ");

			Functions.Initialize.CPUBriefInfo BriefInfo = Functions.Initialize.UI_Initialize();
            Log.Info("BriefInfo collected. ");
			AddressWidthValue.Content = BriefInfo.AddressWidth;
			ArchitectureValue.Content = BriefInfo.Architecture;
			CpuStatusValue.Content = BriefInfo.CpuStatus;
			CurrentClockSpeedValue.Content = BriefInfo.CurrentClockSpeed;
			//CurrentVoltageValue.Content = BriefInfo.CurrentVoltage;
			MaxClockSpeedValue.Content = BriefInfo.MaxClockSpeed;
            Log.Info("BriefInfo initialized. ");
			foreach (string str in GlobalVar.GPUInfo)
			{
				GPUInfoTextBox.AppendText(str);

			}
            Log.Info("GPU info displayed");
            
			//Refresh start. 
			Threads.UIThread.Start();
            Log.Info("UIThread started. ");

            Log.Info("Initialization complete. Main thread working... ");
		}

		private void MainForm_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
            Log.Info("MainForm closing. ");
            if (Threads.UIThread.ThreadState==System.Threading.ThreadState.Aborted)
            {
                Log.Error("UIThread is already aborted! ");
            }
            else
            {
                Threads.UIThread.Abort();
                if (GlobalVar.DebugMode)
                {
                    Log.Debug("UIThread aborted. ");
                }
            }
            GlobalVar.Refresh.Dispose();
            if (GlobalVar.DebugMode)
            {
                Log.Debug("\"Refresh\" disposed. ");
            }
            Log.Info("Disposing log stream, shutting down...");
            GlobalVar.Log.Writer.Flush();
            GlobalVar.Log.Writer.Dispose();
		}

		private void RefreshTime_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			if ((int)e.NewValue > 5000 || (int)e.NewValue < 100) 
			{
				if ((int)e.NewValue > 5000) 
				{
                    Log.Warn("Value: \"RefreshTime\" Out of range:100~5000, current value: " + e.NewValue.ToString() + ", setting 5000 instead. ");
					GlobalVar.RefreshSpeed = 5000;
				}
				if ((int)e.NewValue < 100) 
				{
                    Log.Warn("Value: \"RefreshTime\" Out of range:100~5000, current value: " + e.NewValue.ToString() + ", setting 100" +
                        " instead. ");
                    GlobalVar.RefreshSpeed = 100;
				}
			}
			else GlobalVar.RefreshSpeed = (int)e.NewValue;
            if (GlobalVar.DebugMode)
            {
                Log.Debug("RefreshTime value changed: from" + e.OldValue.ToString() + " to " + e.NewValue.ToString() + ". ");
            }
            if (GlobalVar.IsTotalInitialized == true)
			{
				RefreshTimeLabel.Content = e.NewValue;
                if (GlobalVar.DebugMode)
                {
                    Log.Debug("RefreshTimeLabel value changed. ");

                }
			}
            else
            {
                try
                {
                    Log.Error("Trying to access RefreshTimeLabel before initialization completed. ");
                }
                catch (Exception)
                {
                    
                }
            }
		}

		private void X32SwichCheckBox_Click(object sender, RoutedEventArgs e)
		{
			GlobalVar.x32Swich = (bool)x32SwichCheckBox.IsChecked;
			if ((bool)x32SwichCheckBox.IsChecked)
			{
                Log.Info("x32Swich on. ");
				GlobalVar.Processor = new Functions.ProcessorUsage();
                if (GlobalVar.DebugMode)
                {
                    Log.Debug("Processor resources initialized. ");
                }
			}
			else
			{
                Log.Info("x32Swich off. ");
				GlobalVar.Processor = null;
                if (GlobalVar.DebugMode)
                {
                    Log.Debug("Processor resources released. ");
                }
			}
		}

		private void CPUTickerCheckBox_Click(object sender, RoutedEventArgs e)
		{
			if ((bool)CPUTickerCheckBox.IsChecked)
			{
                GlobalVar.Refresh.Set();
                Log.Info("CPUTicker enabled. ");
			}
			else
			{
                GlobalVar.Refresh.Reset();
                Log.Info("CPUTicker disabled. ");
			}
		}

		/// <summary>
		/// ShouAdvanceInfo button clicked. 
		/// </summary>
		private void ShowAdvanceInfo_Click(object sender, RoutedEventArgs e)
		{
            if (GlobalVar.DebugMode)
            {
                Log.Debug("Gathering AdvCPUInfo. ");
            }
			GlobalVar.AdvCPUInfo = Functions.GetInfo.x64GetInfo.ListTCPU();
            if (GlobalVar.DebugMode)
            {
                Log.Debug("AdvCPUInfo gathered. ");
            }
            foreach (string str in GlobalVar.AdvCPUInfo)
			{
				CPUAdvancedInfo.AppendText(str);
			}
            Log.Info("AdvancedInfo showed. ");
		}
    }
}