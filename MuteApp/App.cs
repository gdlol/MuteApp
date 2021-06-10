using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
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
        [DllImport("User32.dll", SetLastError = true)]
        private static extern int GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

        private static IntPtr GetFocusWindow()
        {
            GUITHREADINFO guiInfo = default;
            guiInfo.cbSize = (uint)Marshal.SizeOf(guiInfo);
            int success = GetGUIThreadInfo(0, ref guiInfo);
            if (success == 0)
            {
                int error = Marshal.GetLastWin32Error();
                MessageBox.Show(new Win32Exception(error).Message);
                return IntPtr.Zero;
            }
            return guiInfo.hwndFocus;
        }

        private static int GetParentProcessId(int processId)
        {
            using var handle = Kernel32.CreateToolhelp32Snapshot(
                Kernel32.CreateToolhelp32SnapshotFlags.TH32CS_SNAPPROCESS,
                processId);
            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                throw new System.ComponentModel.Win32Exception(error);
            }

            if (Kernel32.Process32First(handle) is not Kernel32.PROCESSENTRY32 entry)
            {
                return -1;
            }

            while (true)
            {
                if (entry.th32ProcessID == processId)
                {
                    return entry.th32ParentProcessID;
                }
                if (Kernel32.Process32Next(handle) is Kernel32.PROCESSENTRY32 nextEntry)
                {
                    entry = nextEntry;
                }
                else
                {
                    break;
                }
            }
            return -1;
        }

        private static List<int> GetParents(int processId)
        {
            var list = new List<int>();
            while (true)
            {
                processId = GetParentProcessId(processId);
                if (processId == -1 || list.Contains(processId))
                {
                    break;
                }
                list.Add(processId);
            }
            return list;
        }

        public App()
        {
            HotkeyManager.Current.AddOrReplace("Mute", Key.F7, ModifierKeys.Alt, (sender, e) =>
            {
                var hWnd = GetFocusWindow();
                if (hWnd == IntPtr.Zero)
                {
                    return;
                }

                int threadId = User32.GetWindowThreadProcessId(hWnd, out int focusProcessId);
                using var process = Process.GetProcessById(focusProcessId);
                string title = User32.GetWindowText(hWnd);
                var focusProcessParents = GetParents(focusProcessId);

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
                            if (session.IsSystemSoundsSession)
                            {
                                continue;
                            }
                            int sessionProcessId = (int)session.GetProcessID;
                            if (sessionProcessId == focusProcessId
                                || focusProcessParents.Contains(sessionProcessId)
                                || GetParents(sessionProcessId).Contains(focusProcessId))
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
            Exit += (sender, e) =>
            {
                notifyIcon.Dispose();
            };
            contextMenu.Items.Add(exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit"));
            exitItem.Click += (sender, e) =>
            {
                Shutdown();
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
