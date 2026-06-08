using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Media.Control;

// ITaskbarList3 COM Interface

[ComImport, Guid("56fdf344-fd6d-11d0-958a-006097c9a090"),
 ClassInterface(ClassInterfaceType.None)]
class TaskbarListCoClass { }

[ComImport, Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ITaskbarList3
{
    // ITaskbarList
    void HrInit();
    void AddTab(IntPtr hwnd);
    void DeleteTab(IntPtr hwnd);
    void ActivateTab(IntPtr hwnd);
    void SetActiveAlt(IntPtr hwnd);
    // ITaskbarList2
    void MarkFullscreenWindow(IntPtr hwnd,
        [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);
    // ITaskbarList3
    void SetProgressValue(IntPtr hwnd, ulong ullCompleted, ulong ullTotal);
    void SetProgressState(IntPtr hwnd, TBPFLAG tbpFlags);
}

enum TBPFLAG
{
    TBPF_NOPROGRESS    = 0,
    TBPF_INDETERMINATE = 1,
    TBPF_NORMAL        = 2,
    TBPF_ERROR         = 4,
    TBPF_PAUSED        = 8
}

// Main Entry Point

class Program
{
    static async Task Main()
    {
        Log("SpotifyTaskbarProgress starting...");

        // Initialize ITaskbarList3 via COM
        var taskbar = (ITaskbarList3)new TaskbarListCoClass();
        taskbar.HrInit();

        // Request SMTC manager from Windows
        var manager = await GlobalSystemMediaTransportControlsSessionManager
            .RequestAsync();

        Log("Running. Press Ctrl+C to stop.");
        Log("(Change OutputType to WinExe in .csproj to run without this window)");

        IntPtr lastHwnd  = IntPtr.Zero;
        TBPFLAG lastFlag = TBPFLAG.TBPF_NOPROGRESS;

        while (true)
        {
            try
            {
                // Find Spotify main window
                var hwnd = GetSpotifyHwnd();

                if (hwnd == IntPtr.Zero)
                {
                    await Task.Delay(1500);
                    continue;
                }

                // Clear progress bar if window changed
                if (hwnd != lastHwnd)
                {
                    taskbar.SetProgressState(hwnd, TBPFLAG.TBPF_NOPROGRESS);
                    lastHwnd = hwnd;
                }

                // Get SMTC session
                var session = manager.GetSessions()
                    .FirstOrDefault(s => s.SourceAppUserModelId.Contains(
                        "Spotify", StringComparison.OrdinalIgnoreCase));

                if (session is null)
                {
                    SetState(taskbar, hwnd, TBPFLAG.TBPF_NOPROGRESS,
                        ref lastFlag);
                    await Task.Delay(1500);
                    continue;
                }

                var playback = session.GetPlaybackInfo();
                var timeline = session.GetTimelineProperties();

                var duration = timeline.EndTime  - timeline.StartTime;
                var position = timeline.Position - timeline.StartTime;

                if (duration.TotalSeconds <= 0)
                {
                    await Task.Delay(500);
                    continue;
                }

                // Update taskbar progress
                var status = playback.PlaybackStatus;

                if (status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                {
                    // Extrapolate real position
                    var extra = DateTime.UtcNow - timeline.LastUpdatedTime.UtcDateTime;
                    var realPos = position + extra;
                    if (realPos > duration) realPos = duration;

                    SetState(taskbar, hwnd, TBPFLAG.TBPF_NORMAL, ref lastFlag);
                    taskbar.SetProgressValue(
                        hwnd,
                        (ulong)realPos.Ticks,
                        (ulong)duration.Ticks);
                }
                else if (status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused)
                {
                    SetState(taskbar, hwnd, TBPFLAG.TBPF_PAUSED, ref lastFlag);
                    taskbar.SetProgressValue(
                        hwnd,
                        (ulong)position.Ticks,
                        (ulong)duration.Ticks);
                }
                else
                {
                    SetState(taskbar, hwnd, TBPFLAG.TBPF_NOPROGRESS, ref lastFlag);
                }
            }
            catch (Exception ex)
            {
                Log($"[erro] {ex.Message}");
                await Task.Delay(1000);
            }

            await Task.Delay(500);
        }
    }

    // Avoid calling SetProgressState repeatedly unnecessarily
    static void SetState(ITaskbarList3 tb, IntPtr hwnd,
        TBPFLAG flag, ref TBPFLAG last)
    {
        if (flag == last) return;
        tb.SetProgressState(hwnd, flag);
        last = flag;
    }

    static IntPtr GetSpotifyHwnd()
    {
        // Spotify runs several processes. The one with MainWindowHandle is the main one
        foreach (var p in Process.GetProcessesByName("Spotify"))
        {
            try
            {
                if (p.MainWindowHandle != IntPtr.Zero)
                    return p.MainWindowHandle;
            }
            catch { /* process may have exited */ }
        }
        return IntPtr.Zero;
    }

    static void Log(string msg) =>
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
}
