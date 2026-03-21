using DiscordRPC;
using BachRadio.Rpc.Models;
using BachRadio.Rpc.Services;

namespace BachRadio.Rpc;

class Program
{
    public const string DiscordAppId = "1483207456120377486";
    public const string MusicStatusUrl = "https://bachtran.dev/api/music";
    private const long PositionDeltaThresholdMs = 60_000;

    private static DiscordRpcClient? _discordClient;
    private static readonly StatusBuffer _buffer = new();
    private static MusicStatus? _previousStatus;
    private static NotifyIcon? _trayIcon;

    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Setup system tray icon
        _trayIcon = new NotifyIcon()
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "BachRadio RPC - Starting..."
        };

        var contextMenu = new ContextMenuStrip();
        var exitMenuItem = new ToolStripMenuItem("Exit", null, (s, e) =>
        {
            Application.Exit();
        });
        contextMenu.Items.Add(exitMenuItem);
        _trayIcon.ContextMenuStrip = contextMenu;

        // 1. Setup Discord
        _discordClient = new DiscordRpcClient(DiscordAppId);
        _discordClient.Initialize();
        UpdateTrayIcon("Connected to Discord");

        // 2. Start HTTP polling loop
        Task.Run(async () =>
        {
            using var http = new HttpClient();
            while (true)
            {
                try
                {
                    var json = await http.GetStringAsync(MusicStatusUrl);
                    var status = System.Text.Json.JsonSerializer.Deserialize<MusicStatus>(json);

                    if (status != null)
                    {
                        _buffer.Update(status);

                        if (HasTrackStatusChanged(_previousStatus, status))
                        {
                            UpdatePresence();
                            UpdateTrayIcon(status);
                            _previousStatus = status;
                        }
                    }
                }
                catch (Exception ex)
                {
                    UpdateTrayIcon($"Error: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromSeconds(15));
            }
        });

        // 3. Handle cleanup on exit
        Application.ApplicationExit += (s, e) =>
        {
            _trayIcon?.Dispose();
            _discordClient?.Dispose();
        };

        Application.Run();
    }

    static bool HasTrackStatusChanged(MusicStatus? previous, MusicStatus? current)
    {
        if (previous == null || current == null) return true;
        if (previous.Playing != current.Playing) return true;
        if (previous.Paused != current.Paused) return true;

        var positionDeltaMs = Math.Abs(current.Position - previous.Position);

        return previous.Track.Uri != current.Track.Uri || positionDeltaMs > PositionDeltaThresholdMs;
    }

    static void UpdatePresence()
    {
        var (status, lastUpdate, hasData) = _buffer.Get();
        if (!hasData || status == null || _discordClient == null) return;

        if (!status.Playing || status.Paused)
        {
            _discordClient.ClearPresence();
            return;
        }

        var curPresence = new RichPresence()
        {
            Type = ActivityType.Listening,
            StatusDisplay = StatusDisplayType.Details,
            Details = status.Track.Title,
            DetailsUrl = status.Track.Uri,
            State = status.Track.Author,
            Assets = new Assets()
            {
                LargeImageKey = status.Track.ArtworkUrl,
            }
        };

        if (!status.Track.IsStream)
        {
            var elapsed = DateTime.Now - lastUpdate;
            var start = DateTime.UtcNow.AddMilliseconds(-(status.Position + elapsed.TotalMilliseconds));
            var end = start.AddMilliseconds(status.Track.Length);
            curPresence.Timestamps = new Timestamps(start, end);
        }
        _discordClient.SetPresence(curPresence);
    }

    static void UpdateTrayIcon(string discordStatus)
    {
        if (_trayIcon == null) return;

        var tooltip = $"Discord: {discordStatus}\nTrack: Not playing";
        _trayIcon.Text = tooltip.Length > 63 ? tooltip.Substring(0, 60) + "..." : tooltip;
    }

    static void UpdateTrayIcon(MusicStatus status)
    {
        if (_trayIcon == null) return;

        var discordStatus = _discordClient?.IsInitialized == true ? "Connected" : "Disconnected";
        string trackInfo;

        if (!status.Playing)
        {
            trackInfo = "Not playing";
        }
        else
        {
            var trackDisplay = $"{status.Track.Title} - {status.Track.Author}";
            trackInfo = $"▶ {trackDisplay}";
        }

        var tooltip = $"Discord: {discordStatus}\nTrack: {trackInfo}";
        _trayIcon.Text = tooltip.Length > 63 ? tooltip.Substring(0, 60) + "..." : tooltip;
    }
}