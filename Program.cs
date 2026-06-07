using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Media.Control;

// ── ITaskbarList3 COM ────────────────────────────────────────────────────────

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
    TBPF_PAUSED        = 8   // barra amarela — pausado
}

// ── Main ─────────────────────────────────────────────────────────────────────

class Program
{
    static async Task Main()
    {
        Log("SpotifyTaskbarProgress a arrancar...");

        // Inicializar ITaskbarList3 via COM
        var taskbar = (ITaskbarList3)new TaskbarListCoClass();
        taskbar.HrInit();

        // Pedir manager da SMTC ao Windows
        var manager = await GlobalSystemMediaTransportControlsSessionManager
            .RequestAsync();

        Log("A correr. Ctrl+C para parar.");
        Log("(Muda OutputType para WinExe no .csproj para correr sem esta janela)");

        IntPtr lastHwnd  = IntPtr.Zero;
        TBPFLAG lastFlag = TBPFLAG.TBPF_NOPROGRESS;

        while (true)
        {
            try
            {
                // ── Encontrar janela principal do Spotify ──────────────────
                var hwnd = GetSpotifyHwnd();

                if (hwnd == IntPtr.Zero)
                {
                    await Task.Delay(1500);
                    continue;
                }

                // Limpar barra se a janela mudou
                if (hwnd != lastHwnd)
                {
                    taskbar.SetProgressState(hwnd, TBPFLAG.TBPF_NOPROGRESS);
                    lastHwnd = hwnd;
                }

                // ── Ler sessão SMTC ────────────────────────────────────────
                var session = manager.GetCurrentSession();

                if (session is null ||
                    !session.SourceAppUserModelId.Contains(
                        "Spotify", StringComparison.OrdinalIgnoreCase))
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

                // ── Atualizar taskbar ──────────────────────────────────────
                var status = playback.PlaybackStatus;

                if (status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                {
                    // Extrapolar posição real: SMTC não actualiza every frame
                    // então adicionamos o tempo passado desde o último update
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

    // Evitar chamar SetProgressState repetidamente sem necessidade
    static void SetState(ITaskbarList3 tb, IntPtr hwnd,
        TBPFLAG flag, ref TBPFLAG last)
    {
        if (flag == last) return;
        tb.SetProgressState(hwnd, flag);
        last = flag;
    }

    static IntPtr GetSpotifyHwnd()
    {
        // Spotify corre vários processos; o que tem MainWindowHandle é o principal
        foreach (var p in Process.GetProcessesByName("Spotify"))
        {
            try
            {
                if (p.MainWindowHandle != IntPtr.Zero)
                    return p.MainWindowHandle;
            }
            catch { /* processo pode ter saído */ }
        }
        return IntPtr.Zero;
    }

    static void Log(string msg) =>
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
}
