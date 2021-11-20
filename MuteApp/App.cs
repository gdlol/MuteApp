using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using NAudio.CoreAudioApi;
using NHotkey.Wpf;
using PInvoke;

namespace MuteApp;

public class App : Application
{
    [DllImport("User32.dll", SetLastError = true)]
    private static extern int GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

    private static IntPtr GetFocusWindow()
    {
        GUITHREADINFO guiInfo = default;
        guiInfo.cbSize = (uint)Marshal.SizeOf(guiInfo);
        int result = GetGUIThreadInfo(0, ref guiInfo);
        if (result == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        return guiInfo.hwndFocus;
    }

    private static (bool success, DateTimeOffset startTime) TryGetProcessStartTime(int processId)
    {
        using var handle = Kernel32.OpenProcess(
            Kernel32.ProcessAccess.PROCESS_QUERY_LIMITED_INFORMATION,
            false,
            processId);
        if (handle.IsInvalid)
        {
            return (false, default);
        }
        if (Kernel32.GetProcessTimes(handle, out var creation, out var exit, out var kernel, out var user))
        {
            return (true, DateTimeOffset.FromFileTime(creation));
        }
        else
        {
            return (false, default);
        }
    }

    private static IEnumerable<Kernel32.PROCESSENTRY32> EnumerateProcessEntries()
    {
        using var handle = Kernel32.CreateToolhelp32Snapshot(
            Kernel32.CreateToolhelp32SnapshotFlags.TH32CS_SNAPPROCESS,
            0);
        if (handle.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        if (Kernel32.Process32First(handle) is Kernel32.PROCESSENTRY32 entry)
        {
            yield return entry;

            while (Kernel32.Process32Next(handle) is Kernel32.PROCESSENTRY32 nextEntry)
            {
                yield return nextEntry;
            }
        }
    }

    private static int[] GetParents(int processId)
    {
        var entries = EnumerateProcessEntries().ToArray();
        (bool success, int parentProcessId) TryGetParentProcessId(int processId)
        {
            foreach (var entry in entries)
            {
                if (entry.th32ProcessID == processId)
                {
                    return (true, entry.th32ParentProcessID);
                }
            }
            return (false, default);
        }

        var parents = new List<int>();
        int childProcessId = processId;
        if (TryGetProcessStartTime(childProcessId) is (true, var childStartTime))
        {
            while (TryGetParentProcessId(childProcessId) is (true, int parentProcessId))
            {
                if (TryGetProcessStartTime(parentProcessId) is not (true, var parentStartTime)
                    || parentStartTime > childStartTime)
                {
                    break;
                }
                parents.Add(parentProcessId);
                childProcessId = parentProcessId;
                childStartTime = parentStartTime;
            }
        }
        return parents.ToArray();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string? applicationName = Assembly.GetExecutingAssembly().GetName().Name;
        string? executablePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (executablePath is null)
        {
            throw new InvalidOperationException(nameof(executablePath));
        }
        System.Windows.Forms.NotifyIcon notifyIcon;
        System.Windows.Forms.ContextMenuStrip contextMenu;
        System.Windows.Forms.ToolStripMenuItem exitItem;

        notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(executablePath),
            Text = applicationName,
            Visible = true,
            ContextMenuStrip = contextMenu = new System.Windows.Forms.ContextMenuStrip()
        };
        Exit += (sender, e) => notifyIcon.Dispose();
        contextMenu.Items.Add(exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit"));
        exitItem.Click += (sender, e) => Shutdown();

        HotkeyManager.Current.AddOrReplace("Mute", Key.F7, ModifierKeys.Alt, (sender, e) =>
        {
            IntPtr hWnd;
            try
            {
                hWnd = GetFocusWindow();
            }
            catch (Win32Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }
            if (hWnd == IntPtr.Zero)
            {
                return;
            }

            int threadId = User32.GetWindowThreadProcessId(hWnd, out int focusProcessId);
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
    }

    public void RunSingleInstance()
    {
        string? assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
        if (assemblyName is null)
        {
            throw new InvalidOperationException(nameof(assemblyName));
        }
        using var handle = new EventWaitHandle(
            false,
            EventResetMode.AutoReset,
            assemblyName,
            out bool createdNew);
        if (createdNew)
        {
            Run();
        }
    }
}
