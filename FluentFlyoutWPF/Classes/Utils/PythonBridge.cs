using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace FluentFlyout.Classes.Utils;

public static class PythonBridge
{
    public static bool IsPluginMode { get; private set; }
    private static readonly HttpClient _httpClient = new();
    private static DispatcherTimer? _pollTimer;

    // Events to notify MainWindow when data changes
    public static Action<string, string, BitmapImage?, bool>? OnStateUpdated;

    public static void Initialize(bool isPluginMode)
    {
        IsPluginMode = isPluginMode;
        if (!IsPluginMode) return;

        // Poll the Python server every 1 second
        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _pollTimer.Tick += async (s, e) => await PollPythonServerAsync();
        _pollTimer.Start();
    }

    private static async Task PollPythonServerAsync()
    {
        try
        {
            var response = await _httpClient.GetStringAsync("http://127.0.0.1:8000/api/widget/state");
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            string title = root.GetProperty("title").GetString() ?? "Nothing playing";
            bool isPlaying = root.GetProperty("isPlaying").GetBoolean();
            string iconUrl = root.GetProperty("icon").GetString() ?? "";

            // Convert URL/Base64 to BitmapImage
            BitmapImage? icon = await BitmapHelper.GetThumbnailFromUrlAsync(iconUrl);

            // Send to MainWindow
            OnStateUpdated?.Invoke(title, "Musicthingy Server", icon, isPlaying);
        }
        catch
        {
            // Server offline or not responding
            OnStateUpdated?.Invoke("Disconnected", "Waiting for server...", null, false);
        }
    }

    public static async Task SendCommandAsync(string action)
    {
        if (!IsPluginMode) return;
        try
        {
            var json = $"{{\"action\": \"{action}\"}}";
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            await _httpClient.PostAsync("http://127.0.0.1:8000/api/widget/command", content);

            // Instantly poll to get the new state (e.g. playing -> paused)
            await PollPythonServerAsync();
        }
        catch { }
    }
}