using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace MermaidViewer;

/// <summary>Scheda con WebView2, file opzionale e ricarica con debounce.</summary>
internal sealed class DiagramTabHost : TabPage
{
    private readonly WebView2 _webView;
    private readonly System.Windows.Forms.Timer _debounce;
    private FileSystemWatcher? _watcher;
    private string _pendingDiagram = "";
    private bool _webReady;

    public DiagramTabHost()
    {
        Text = "Senza file";
        ToolTipText = "Nessun file aperto in questa scheda.";
        UseVisualStyleBackColor = true;

        _webView = new WebView2 { Dock = DockStyle.Fill, Margin = new Padding(0) };
        Controls.Add(_webView);

        _debounce = new System.Windows.Forms.Timer { Interval = 200 };
        _debounce.Tick += async (_, _) =>
        {
            _debounce.Stop();
            await RenderAsync(_pendingDiagram);
        };
    }

    public WebView2 WebView => _webView;

    public string? FilePath { get; private set; }

    public bool IsEmpty => FilePath is null;

    public bool WebReady => _webReady;

    /// <summary>Notifica messaggi di stato dalla pagina (filtrare in base alla scheda selezionata).</summary>
    public Action<DiagramTabHost, CoreWebView2WebMessageReceivedEventArgs>? WebMessageRelay { get; set; }

    public async Task EnsureWebViewAsync(CoreWebView2Environment environment)
    {
        if (_webReady) return;

        await _webView.EnsureCoreWebView2Async(environment);

        _webView.CoreWebView2.Settings.IsWebMessageEnabled = true;
        _webView.CoreWebView2.WebMessageReceived += (_, e) => WebMessageRelay?.Invoke(this, e);

        _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
        _webView.ZoomFactor = 1.0;

        var mermaidJs = EmbeddedAssets.MermaidMinJs;
        await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(mermaidJs);
        await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(WebScripts.Bootstrap);

        _webView.NavigateToString(WebScripts.HostHtml);
        _webReady = true;

        try
        {
            await _webView.CoreWebView2.ExecuteScriptAsync("window.__mermaidViewerClear && window.__mermaidViewerClear();");
        }
        catch
        {
            /* ignore */
        }
    }

    public void SetFilePath(string fullPath)
    {
        FilePath = fullPath;
        Text = Path.GetFileName(fullPath);
        ToolTipText = fullPath;

        _watcher?.Dispose();
        _watcher = new FileSystemWatcher(Path.GetDirectoryName(fullPath)!)
        {
            Filter = Path.GetFileName(fullPath),
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
        };
        _watcher.Changed += OnWatcherEvent;
        _watcher.Created += OnWatcherEvent;
        _watcher.Renamed += OnWatcherEvent;
    }

    public void ClearFile()
    {
        FilePath = null;
        Text = "Senza file";
        ToolTipText = "Nessun file aperto in questa scheda.";
        _watcher?.Dispose();
        _watcher = null;
        _pendingDiagram = "";
        _debounce.Stop();
        _ = ClearWebDiagramAsync();
    }

    private async Task ClearWebDiagramAsync()
    {
        if (!_webReady) return;
        try
        {
            await _webView.CoreWebView2.ExecuteScriptAsync("window.__mermaidViewerClear && window.__mermaidViewerClear();");
        }
        catch
        {
            /* ignore */
        }
    }

    private void OnWatcherEvent(object sender, FileSystemEventArgs e)
    {
        if (TopLevelControl is not Control c) return;
        if (c.IsDisposed) return;
        if (c.InvokeRequired)
        {
            try { c.BeginInvoke(QueueReloadFromDisk); }
            catch { }
        }
        else
            QueueReloadFromDisk();
    }

    public void QueueReloadFromDisk()
    {
        if (FilePath is null || !File.Exists(FilePath)) return;
        var text = ReadAllTextRobust(FilePath);
        _pendingDiagram = text;
        _debounce.Stop();
        _debounce.Start();
    }

    public async Task RenderAsync(string mermaidText)
    {
        if (!_webReady) return;
        try
        {
            var json = JsonSerializer.Serialize(new { mermaid = mermaidText });
            await _webView.CoreWebView2.ExecuteScriptAsync($"window.__mermaidViewerRender({json});");
        }
        catch
        {
            /* ignore */
        }
    }

    public async Task RunJsAsync(string expr)
    {
        if (!_webReady) return;
        try { await _webView.CoreWebView2.ExecuteScriptAsync(expr); }
        catch { }
    }

    public void DisposeResources()
    {
        _watcher?.Dispose();
        _watcher = null;
        _debounce.Stop();
    }

    private static string ReadAllTextRobust(string path)
    {
        for (var i = 0; i < 6; i++)
        {
            try { return File.ReadAllText(path, Encoding.UTF8); }
            catch { Thread.Sleep(40); }
        }
        return File.ReadAllText(path, Encoding.UTF8);
    }
}
