using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using NAudio.CoreAudioApi;
using NHotkey.Wpf;
using PInvoke;

namespace MuteApp
{
    public class App : Application
    {
        public App()
        {
            HotkeyManager.Current.AddOrReplace("Mute", Key.F7, ModifierKeys.Alt, (sender, e) =>
            {
                var hWnd = User32.GetForegroundWindow();
                if (hWnd == IntPtr.Zero)
                {
                    return;
                }
                int threadId = User32.GetWindowThreadProcessId(hWnd, out int processId);
                using var process = Process.GetProcessById(processId);
                string title = User32.GetWindowText(hWnd);

                using var deviceEnumerator = new MMDeviceEnumerator();
                foreach (var device in deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                {
                    using var _ = device;
                    var sessions = device.AudioSessionManager?.Sessions;
                    if (sessions is not null)
                    {
                        for (int i = 0; i < sessions.Count; i++)
                        {
                            var session = sessions[i];
                            if (session.GetProcessID == processId)
                            {
                                var volume = session.SimpleAudioVolume;
                                volume.Mute = !volume.Mute;
                            }
                        }
                    }
                }
            });

            string applicationName = Assembly.GetExecutingAssembly().GetName().Name;
            System.Windows.Forms.NotifyIcon notifyIcon;
            System.Windows.Forms.ContextMenuStrip contextMenu;
            System.Windows.Forms.ToolStripMenuItem exitItem;

            notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName),
                Text = applicationName,
                Visible = true,
                ContextMenuStrip = contextMenu = new System.Windows.Forms.ContextMenuStrip()
            };
            contextMenu.Items.Add(exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit"));

            exitItem.Click += (sender, e) =>
            {
                notifyIcon.Visible = false;
                Environment.Exit(0);
            };
        }

        public void RunSingleInstance()
        {
            string assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
            using var handle = new EventWaitHandle(
                false,
                EventResetMode.AutoReset,
                assemblyName,
                out bool createdNew);
            if (createdNew)
            {
                Run();
            }
            else
            {
                handle.Set();
            }
        }
    }
}
