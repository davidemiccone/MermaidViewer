using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace MermaidViewer;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        ApplicationConfiguration.Initialize();

        var initialPath = args.Length > 0 ? args[0] : null;
        var form = new MainForm(initialPath);
        Application.Run(form);
    }
}

internal sealed class MainForm : Form
{
    private readonly string? _initialPath;
    private readonly MenuStrip _menuStrip;
    private readonly ToolStrip _toolStrip;
    private readonly ToolStripButton _tbOpen;
    private readonly ToolStripButton _tbFit;
    private readonly ToolStripButton _tb100;
    private readonly ToolStripButton _tbZoomIn;
    private readonly ToolStripButton _tbZoomOut;
    private readonly ToolStripButton _tbReload;
    private readonly ToolStripButton _tbSettings;
    private readonly Panel _workspace;
    private readonly Panel _noTabsFill;
    private readonly TabControl _tabs;
    private readonly StatusStrip _statusStrip;
    private readonly ToolStripStatusLabel _statusMain;
    private readonly ToolStripStatusLabel _statusView;

    private CoreWebView2Environment? _sharedEnv;
    private int? _tabDragFrom;

    public MainForm(string? initialPath)
    {
        _initialPath = initialPath;
        Text = "Mermaid Viewer";
        ApplyApplicationIcon();
        Font = SystemFonts.MessageBoxFont;
        Size = new Size(1280, 840);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(720, 520);
        AutoScaleMode = AutoScaleMode.Dpi;
        KeyPreview = true;

        _menuStrip = BuildMenuStrip();
        _toolStrip = BuildToolStrip(out _tbOpen, out _tbFit, out _tb100, out _tbZoomIn, out _tbZoomOut, out _tbReload,
            out _tbSettings);

        _tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Padding = new Point(0, 0),
            ShowToolTips = true,
            SizeMode = TabSizeMode.Fixed,
            DrawMode = TabDrawMode.OwnerDrawFixed,
            Visible = false
        };
        _tabs.SelectedIndexChanged += (_, _) => SyncChromeFromTab();
        _tabs.DrawItem += Tabs_DrawItem;
        _tabs.MouseDown += Tabs_MouseDown;
        _tabs.MouseMove += Tabs_MouseMove;
        _tabs.MouseUp += Tabs_MouseUp;
        _tabs.ContextMenuStrip = BuildTabContextMenu();

        _noTabsFill = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SystemColors.Window,
            Margin = new Padding(0)
        };
        _workspace = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
        _workspace.Controls.Add(_noTabsFill);
        _workspace.Controls.Add(_tabs);

        _statusStrip = new StatusStrip { SizingGrip = false };
        _statusMain = new ToolStripStatusLabel
        {
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = ""
        };
        _statusView = new ToolStripStatusLabel
        {
            Spring = false,
            TextAlign = ContentAlignment.MiddleRight,
            Text = "Pronto."
        };
        _statusStrip.Items.Add(_statusMain);
        _statusStrip.Items.Add(_statusView);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(_menuStrip, 0, 0);
        layout.Controls.Add(_toolStrip, 0, 1);
        layout.Controls.Add(_workspace, 0, 2);
        layout.Controls.Add(_statusStrip, 0, 3);

        Controls.Add(layout);
        MainMenuStrip = _menuStrip;

        Load += (_, _) =>
        {
            ApplyToolbarImages();
            ApplyTabMetrics();
        };
        DpiChanged += (_, _) =>
        {
            ApplyToolbarImages();
            ApplyTabMetrics();
        };
        Shown += async (_, _) => await OnShownAsync();
        FormClosed += (_, _) => OnFormClosed();
        KeyDown += (_, e) => _ = HandleFormKeyDownAsync(e);
        MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                _tabDragFrom = null;
        };
    }

    private void ApplyApplicationIcon()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (string.IsNullOrEmpty(path)) return;
            using var sys = Icon.ExtractAssociatedIcon(path);
            if (sys is null) return;
            Icon = new Icon(sys, sys.Size);
        }
        catch
        {
            /* icona predefinita */
        }
    }

    private ContextMenuStrip BuildTabContextMenu()
    {
        var cms = new ContextMenuStrip();
        var closeItem = new ToolStripMenuItem("Chiudi &scheda", null, (_, _) => CloseCurrentTabOrClear());
        closeItem.ShortcutKeyDisplayString = "Ctrl+W";
        cms.Items.Add(closeItem);
        return cms;
    }

    private void ApplyTabMetrics()
    {
        if (!IsHandleCreated) return;
        var scale = DeviceDpi / 96f;
        var h = (int)Math.Round(28 * scale);
        var w = (int)Math.Round(168 * scale);
        _tabs.ItemSize = new Size(Math.Max(96, w), Math.Max(22, h));
        _tabs.Invalidate();
    }

    private static Rectangle TabCloseButtonBounds(Rectangle tabBounds, float dpiScale)
    {
        var sz = (int)Math.Round(16 * dpiScale);
        var pad = (int)Math.Round(3 * dpiScale);
        return new Rectangle(tabBounds.Right - sz - pad, tabBounds.Top + (tabBounds.Height - sz) / 2, sz, sz);
    }

    private int TabIndexAt(Point clientLocation)
    {
        for (var i = 0; i < _tabs.TabPages.Count; i++)
        {
            if (_tabs.GetTabRect(i).Contains(clientLocation))
                return i;
        }
        return -1;
    }

    private void Tabs_DrawItem(object? sender, DrawItemEventArgs e)
    {
        var g = e.Graphics;
        var bounds = e.Bounds;
        var selected = (e.State & DrawItemState.Selected) != 0;
        var scale = DeviceDpi / 96f;

        Color back = selected ? SystemColors.Window : SystemColors.Control;
        using (var br = new SolidBrush(back))
            g.FillRectangle(br, bounds);

        var page = _tabs.TabPages[e.Index];
        var closeR = TabCloseButtonBounds(bounds, scale);
        var textRect = new Rectangle(bounds.Left + (int)Math.Round(6 * scale), bounds.Top,
            Math.Max(8, bounds.Width - closeR.Width - (int)Math.Round(10 * scale)), bounds.Height);
        TextRenderer.DrawText(g, page.Text, _tabs.Font, textRect, SystemColors.ControlText,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

        using var xf = new Font(_tabs.Font.FontFamily, Math.Max(6f, _tabs.Font.SizeInPoints + 1.25f), FontStyle.Regular,
            GraphicsUnit.Point);
        TextRenderer.DrawText(g, "×", xf, closeR, Color.FromArgb(100, 100, 100),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        if (selected)
        {
            using var pen = new Pen(SystemColors.Highlight, 2f);
            g.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
        }
    }

    private void Tabs_MouseDown(object? sender, MouseEventArgs e)
    {
        var idx = TabIndexAt(e.Location);
        if (idx < 0) return;

        if (e.Button == MouseButtons.Left)
        {
            var tr = _tabs.GetTabRect(idx);
            if (TabCloseButtonBounds(tr, DeviceDpi / 96f).Contains(e.Location))
            {
                CloseTabAt(idx);
                return;
            }

            _tabDragFrom = idx;
            return;
        }

        if (e.Button != MouseButtons.Middle) return;
        CloseTabAt(idx);
    }

    private void Tabs_MouseMove(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || _tabDragFrom is not int from) return;
        var to = TabIndexAt(e.Location);
        if (to < 0 || to == from) return;
        MoveTabPage(from, to);
        _tabDragFrom = to;
    }

    private void Tabs_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            _tabDragFrom = null;
    }

    private void MoveTabPage(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex) return;
        if (fromIndex < 0 || toIndex < 0) return;
        if (fromIndex >= _tabs.TabPages.Count || toIndex >= _tabs.TabPages.Count) return;
        var p = _tabs.TabPages[fromIndex];
        _tabs.TabPages.Remove(p);
        _tabs.TabPages.Insert(toIndex, p);
        _tabs.SelectedTab = p;
    }

    private async Task OnShownAsync()
    {
        try
        {
            _sharedEnv = await GetSharedEnvAsync();
        }
        catch (Exception ex)
        {
            _statusView.Text = "WebView2 non disponibile.";
            MessageBox.Show(this,
                "Impossibile inizializzare WebView2.\n\n" +
                "Serve il runtime Microsoft Edge WebView2 (di solito già presente su Windows 11).\n\n" +
                ex.Message,
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        if (!string.IsNullOrWhiteSpace(_initialPath))
        {
            var p = _initialPath.Trim().Trim('"');
            if (File.Exists(p))
                await OpenFilePathAsync(p);
        }

        SyncChromeFromTab();
    }

    private void OnFormClosed()
    {
        foreach (TabPage p in _tabs.TabPages)
        {
            if (p is DiagramTabHost h)
                h.DisposeResources();
        }
    }

    private DiagramTabHost CreateDiagramTab()
    {
        var tab = new DiagramTabHost();
        tab.WebMessageRelay = OnTabWebMessage;
        return tab;
    }

    private async Task<CoreWebView2Environment> GetSharedEnvAsync()
    {
        if (_sharedEnv != null) return _sharedEnv;
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MermaidViewer",
            "WebView2");
        Directory.CreateDirectory(userDataFolder);
        _sharedEnv = await CoreWebView2Environment.CreateAsync(browserExecutableFolder: null, userDataFolder: userDataFolder);
        return _sharedEnv;
    }

    private DiagramTabHost? CurrentHost() => _tabs.SelectedTab as DiagramTabHost;

    private DiagramTabHost? FindTabByPath(string fullPath)
    {
        foreach (TabPage p in _tabs.TabPages)
        {
            if (p is not DiagramTabHost h || h.FilePath is null) continue;
            if (string.Equals(Path.GetFullPath(h.FilePath), fullPath, StringComparison.OrdinalIgnoreCase))
                return h;
        }
        return null;
    }

    private void SyncChromeFromTab()
    {
        var hasTabs = _tabs.TabPages.Count > 0;
        _tabs.Visible = hasTabs;
        _noTabsFill.Visible = !hasTabs;

        var h = CurrentHost();
        if (h is null)
        {
            Text = "Mermaid Viewer";
            _statusMain.Text = "";
            if (!hasTabs)
                _statusView.Text = "Pronto.";
            return;
        }

        if (h.FilePath is not null)
        {
            _statusMain.Text = h.FilePath;
            Text = $"Mermaid Viewer — {Path.GetFileName(h.FilePath)}";
        }
        else
        {
            _statusMain.Text = "";
            Text = "Mermaid Viewer";
        }
    }

    private void OnTabWebMessage(DiagramTabHost tab, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (_tabs.SelectedTab != tab) return;
        try
        {
            var raw = e.WebMessageAsJson;
            if (string.IsNullOrWhiteSpace(raw)) return;
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.TryGetProperty("type", out var t) && t.GetString() == "status" &&
                root.TryGetProperty("text", out var tx))
            {
                var msg = tx.GetString() ?? "";
                if (InvokeRequired)
                    BeginInvoke(() => _statusView.Text = msg);
                else
                    _statusView.Text = msg;
            }
        }
        catch
        {
            /* ignore */
        }
    }

    private async Task OpenFilePathAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            MessageBox.Show(this, "File non trovato.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_sharedEnv is null)
        {
            try { _sharedEnv = await GetSharedEnvAsync(); }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        var full = Path.GetFullPath(path);
        var existing = FindTabByPath(full);
        if (existing is not null)
        {
            _tabs.SelectedTab = existing;
            SyncChromeFromTab();
            return;
        }

        var cur = CurrentHost();
        if (cur is not null && cur.IsEmpty)
        {
            await cur.EnsureWebViewAsync(_sharedEnv);
            cur.SetFilePath(full);
            cur.QueueReloadFromDisk();
            SyncChromeFromTab();
            return;
        }

        var tab = CreateDiagramTab();
        _tabs.TabPages.Add(tab);
        _tabs.SelectedTab = tab;
        await tab.EnsureWebViewAsync(_sharedEnv);
        tab.SetFilePath(full);
        tab.QueueReloadFromDisk();
        SyncChromeFromTab();
    }

    private void CloseCurrentTabOrClear()
    {
        var i = _tabs.SelectedIndex;
        if (i < 0) return;
        CloseTabAt(i);
    }

    private void CloseTabAt(int index)
    {
        if (index < 0 || index >= _tabs.TabPages.Count) return;
        var p = _tabs.TabPages[index];
        if (p is not DiagramTabHost h) return;

        h.DisposeResources();
        _tabs.TabPages.Remove(h);
        h.Dispose();
        if (_tabs.TabPages.Count > 0 && _tabs.SelectedIndex < 0)
            _tabs.SelectedIndex = 0;
        SyncChromeFromTab();
    }

    private MenuStrip BuildMenuStrip()
    {
        var strip = new MenuStrip();

        var mFile = new ToolStripMenuItem("&File");
        var mOpen = new ToolStripMenuItem("&Apri…", null, (_, _) => Browse())
        {
            ShortcutKeys = Keys.Control | Keys.O
        };
        var mCloseTab = new ToolStripMenuItem("&Chiudi scheda", null, (_, _) => CloseCurrentTabOrClear())
        {
            ShortcutKeys = Keys.Control | Keys.W
        };
        var mExit = new ToolStripMenuItem("&Esci", null, (_, _) => Close());
        var mExportPng = new ToolStripMenuItem("Esporta come &PNG…", null, async (_, _) => await ExportPngAsync());
        var mExportSvg = new ToolStripMenuItem("Esporta come &SVG…", null, async (_, _) => await ExportSvgAsync());
        var mExportPdf = new ToolStripMenuItem("Esporta come &PDF…", null, async (_, _) => await ExportPdfAsync());
        mFile.DropDownItems.AddRange(new ToolStripItem[]
        {
            mOpen,
            mCloseTab,
            new ToolStripSeparator(),
            mExportPng,
            mExportSvg,
            mExportPdf,
            new ToolStripSeparator(),
            mExit
        });

        var mView = new ToolStripMenuItem("&Vista");
        var mFit = new ToolStripMenuItem("&Adatta tutto", null, async (_, _) => await RunJsCurrentAsync("window.__mermaidViewerFit && window.__mermaidViewerFit()"));
        var m100 = new ToolStripMenuItem("&100% (scala reale)", null, async (_, _) => await RunJsCurrentAsync("window.__mermaidViewerZoomReset && window.__mermaidViewerZoomReset()"));
        var mIn = new ToolStripMenuItem("Zoom &avanti", null, async (_, _) => await RunJsCurrentAsync("window.__mermaidViewerZoomIn && window.__mermaidViewerZoomIn()"));
        var mOut = new ToolStripMenuItem("Zoom &indietro", null, async (_, _) => await RunJsCurrentAsync("window.__mermaidViewerZoomOut && window.__mermaidViewerZoomOut()"));
        var mReload = new ToolStripMenuItem("&Ricarica da disco", null, async (_, _) => await ReloadCurrentFromDiskAsync())
        {
            ShortcutKeys = Keys.F5
        };
        mView.DropDownItems.AddRange(new ToolStripItem[]
        {
            mFit,
            m100,
            new ToolStripSeparator(),
            mIn,
            mOut,
            new ToolStripSeparator(),
            mReload
        });

        var mTools = new ToolStripMenuItem("S&trumenti");
        var mSettings = new ToolStripMenuItem("&Impostazioni…", null, (_, _) => OpenSettings());
        mTools.DropDownItems.Add(mSettings);

        var mHelp = new ToolStripMenuItem("&?");
        var mAbout = new ToolStripMenuItem("&Informazioni…", null, (_, _) => ShowAbout());
        mHelp.DropDownItems.Add(mAbout);

        strip.Items.AddRange(new ToolStripItem[] { mFile, mView, mTools, mHelp });
        return strip;
    }

    private ToolStrip BuildToolStrip(out ToolStripButton tbOpen, out ToolStripButton tbFit, out ToolStripButton tb100,
        out ToolStripButton tbZoomIn, out ToolStripButton tbZoomOut, out ToolStripButton tbReload,
        out ToolStripButton tbSettings)
    {
        var ts = new ToolStrip
        {
            GripStyle = ToolStripGripStyle.Hidden,
            Stretch = false,
            Padding = new Padding(6, 2, 6, 2)
        };

        tbOpen = Btn("Apri file (Ctrl+O)");
        tbFit = Btn("Adatta tutto il diagramma");
        tb100 = Btn("Scala 100% (1:1)");
        tbZoomIn = Btn("Zoom avanti (Ctrl++)");
        tbZoomOut = Btn("Zoom indietro (Ctrl+-)");
        tbReload = Btn("Ricarica file (F5)");
        tbSettings = Btn("Impostazioni");

        tbOpen.Click += (_, _) => Browse();
        tbFit.Click += async (_, _) => await RunJsCurrentAsync("window.__mermaidViewerFit && window.__mermaidViewerFit()");
        tb100.Click += async (_, _) => await RunJsCurrentAsync("window.__mermaidViewerZoomReset && window.__mermaidViewerZoomReset()");
        tbZoomIn.Click += async (_, _) => await RunJsCurrentAsync("window.__mermaidViewerZoomIn && window.__mermaidViewerZoomIn()");
        tbZoomOut.Click += async (_, _) => await RunJsCurrentAsync("window.__mermaidViewerZoomOut && window.__mermaidViewerZoomOut()");
        tbReload.Click += async (_, _) => await ReloadCurrentFromDiskAsync();
        tbSettings.Click += (_, _) => OpenSettings();

        ts.Items.Add(tbOpen);
        ts.Items.Add(new ToolStripSeparator());
        ts.Items.Add(tbFit);
        ts.Items.Add(tb100);
        ts.Items.Add(tbZoomIn);
        ts.Items.Add(tbZoomOut);
        ts.Items.Add(new ToolStripSeparator());
        ts.Items.Add(tbReload);
        ts.Items.Add(new ToolStripSeparator());
        ts.Items.Add(tbSettings);

        return ts;

        static ToolStripButton Btn(string tip) => new()
        {
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            ToolTipText = tip,
            AutoSize = true
        };
    }

    private void ApplyToolbarImages()
    {
        if (!IsHandleCreated) return;
        var s = ToolbarImages.IconSizeFor(this);
        _toolStrip.ImageScalingSize = new Size(s, s);
        ToolbarImages.ApplyToButton(_tbOpen, ToolbarImages.OpenFile(s));
        ToolbarImages.ApplyToButton(_tbFit, ToolbarImages.FitView(s));
        ToolbarImages.ApplyToButton(_tb100, ToolbarImages.ZoomActual(s));
        ToolbarImages.ApplyToButton(_tbZoomIn, ToolbarImages.ZoomIn(s));
        ToolbarImages.ApplyToButton(_tbZoomOut, ToolbarImages.ZoomOut(s));
        ToolbarImages.ApplyToButton(_tbReload, ToolbarImages.Reload(s));
        ToolbarImages.ApplyToButton(_tbSettings, ToolbarImages.SettingsGear(s));
    }

    private void Browse()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Scegli un file Mermaid",
            Filter = "Mermaid (*.mmd;*.mermaid;*.md)|*.mmd;*.mermaid;*.md|Tutti i file (*.*)|*.*"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK)
            return;
        _ = OpenFilePathAsync(dlg.FileName);
    }

    private async Task ReloadCurrentFromDiskAsync()
    {
        var h = CurrentHost();
        if (h?.FilePath is null) return;
        h.QueueReloadFromDisk();
        await Task.CompletedTask;
    }

    private async Task RunJsCurrentAsync(string expr)
    {
        var h = CurrentHost();
        if (h is null) return;
        await h.RunJsAsync(expr);
    }

    private async Task ExportPngAsync()
    {
        var h = CurrentHost();
        if (h is null || !h.WebReady) return;
        using var dlg = new SaveFileDialog
        {
            Title = "Esporta PNG",
            Filter = "Immagine PNG (*.png)|*.png|Tutti i file (*.*)|*.*",
            DefaultExt = "png",
            AddExtension = true
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            const string script = """
                (async function(){
                  try {
                    return await (window.__mermaidViewerGetDiagramPngBase64 && window.__mermaidViewerGetDiagramPngBase64(2));
                  } catch (e) { return ''; }
                })()
                """;
            var json = await h.WebView.CoreWebView2.ExecuteScriptAsync(script);
            var b64 = JsonSerializer.Deserialize<string>(json) ?? "";
            if (string.IsNullOrWhiteSpace(b64))
            {
                MessageBox.Show(this, "Impossibile generare il PNG dal diagramma.", Text, MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var bytes = Convert.FromBase64String(b64);
            await File.WriteAllBytesAsync(dlg.FileName, bytes);
            _statusView.Text = "PNG salvato.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task ExportSvgAsync()
    {
        var h = CurrentHost();
        if (h is null || !h.WebReady) return;
        using var dlg = new SaveFileDialog
        {
            Title = "Esporta SVG",
            Filter = "SVG (*.svg)|*.svg|Tutti i file (*.*)|*.*",
            DefaultExt = "svg",
            AddExtension = true
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            const string script =
                "(function(){ try { return (window.__mermaidViewerGetSvgXml && window.__mermaidViewerGetSvgXml()) || ''; } catch(e) { return ''; } })()";
            var json = await h.WebView.CoreWebView2.ExecuteScriptAsync(script);
            var xml = JsonSerializer.Deserialize<string>(json) ?? "";
            if (string.IsNullOrWhiteSpace(xml))
            {
                MessageBox.Show(this, "Nessun diagramma SVG da esportare.", Text, MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }
            await File.WriteAllTextAsync(dlg.FileName, xml, Encoding.UTF8);
            _statusView.Text = "SVG salvato.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task ExportPdfAsync()
    {
        var h = CurrentHost();
        if (h is null || !h.WebReady) return;
        using var dlg = new SaveFileDialog
        {
            Title = "Esporta PDF",
            Filter = "PDF (*.pdf)|*.pdf|Tutti i file (*.*)|*.*",
            DefaultExt = "pdf",
            AddExtension = true
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        CoreWebView2? core = null;
        try
        {
            core = h.WebView.CoreWebView2;
            const string prepScript = """
                (function(){
                  try { return window.__mermaidViewerPreparePrintExport && window.__mermaidViewerPreparePrintExport(); }
                  catch (e) { return { ok: false }; }
                })()
                """;
            var prepJson = await core.ExecuteScriptAsync(prepScript);
            using var prepDoc = JsonDocument.Parse(prepJson);
            var root = prepDoc.RootElement;
            if (!root.TryGetProperty("ok", out var okEl) || !okEl.GetBoolean())
            {
                MessageBox.Show(this, "Nessun diagramma da esportare in PDF.", Text, MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var wPx = root.GetProperty("widthPx").GetDouble();
            var hPx = root.GetProperty("heightPx").GetDouble();
            if (wPx <= 0 || hPx <= 0)
            {
                MessageBox.Show(this, "Dimensioni diagramma non valide per il PDF.", Text, MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            const double cssPxPerInch = 96.0;
            var ps = core.Environment.CreatePrintSettings();
            ps.MarginTop = 0;
            ps.MarginBottom = 0;
            ps.MarginLeft = 0;
            ps.MarginRight = 0;
            ps.ShouldPrintBackgrounds = true;
            ps.ScaleFactor = 1.0;
            ps.PageWidth = wPx / cssPxPerInch;
            ps.PageHeight = hPx / cssPxPerInch;
            ps.Orientation = wPx >= hPx
                ? CoreWebView2PrintOrientation.Landscape
                : CoreWebView2PrintOrientation.Portrait;

            var ok = await core.PrintToPdfAsync(dlg.FileName, ps);
            _statusView.Text = ok ? "PDF salvato." : "PDF non salvato.";
            if (!ok)
                MessageBox.Show(this, "Salvataggio PDF non riuscito.", Text, MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            if (core is not null)
            {
                try
                {
                    await core.ExecuteScriptAsync(
                        "(function(){ try { window.__mermaidViewerClearPrintExport && window.__mermaidViewerClearPrintExport(); } catch(e) {} })()");
                }
                catch
                {
                    /* ignore */
                }
            }
        }
    }

    private async Task HandleFormKeyDownAsync(KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.W)
        {
            e.Handled = true;
            CloseCurrentTabOrClear();
            return;
        }

        var h = CurrentHost();
        if (!e.Control || h is null || !h.WebReady) return;
        switch (e.KeyCode)
        {
            case Keys.Oemplus:
            case Keys.Add:
                e.Handled = true;
                await h.RunJsAsync("window.__mermaidViewerZoomIn && window.__mermaidViewerZoomIn()");
                break;
            case Keys.OemMinus:
            case Keys.Subtract:
                e.Handled = true;
                await h.RunJsAsync("window.__mermaidViewerZoomOut && window.__mermaidViewerZoomOut()");
                break;
            case Keys.D0:
            case Keys.NumPad0:
                e.Handled = true;
                await h.RunJsAsync("window.__mermaidViewerZoomReset && window.__mermaidViewerZoomReset()");
                break;
        }
    }

    private void OpenSettings()
    {
        using var dlg = new SettingsForm();
        dlg.ShowDialog(this);
    }

    private void ShowAbout()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        var ver = v is null ? "—" : $"{v.Major}.{v.Minor}.{v.Build}";
        MessageBox.Show(this,
            $"Mermaid Viewer — anteprima offline dei diagrammi Mermaid.\n\nVersione: {ver}",
            "Informazioni",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
}
