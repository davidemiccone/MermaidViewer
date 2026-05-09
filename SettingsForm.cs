namespace MermaidViewer;

internal sealed class SettingsForm : Form
{
    private readonly CheckBox _mmd;
    private readonly CheckBox _mermaid;
    private readonly CheckBox _mermraid;
    private readonly Button _register;
    private readonly Button _unregister;
    private readonly Button _openDefaultApps;
    private readonly Label _note;

    public SettingsForm()
    {
        Text = "Settings";
        Font = SystemFonts.MessageBoxFont;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(560, 320);
        Padding = new Padding(12);

        Load += (_, _) =>
        {
            try
            {
                if (Owner is Form { Icon: { } ico })
                    Icon = new Icon(ico, ico.Size);
            }
            catch
            {
                /* icona predefinita */
            }
        };

        _mmd = new CheckBox { Text = "Associa .mmd", AutoSize = true, Margin = new Padding(0, 0, 0, 8) };
        _mermaid = new CheckBox { Text = "Associa .mermaid", AutoSize = true, Margin = new Padding(0, 0, 0, 8) };
        _mermraid = new CheckBox { Text = "Associa .mermraid (typo)", AutoSize = true, Margin = new Padding(0, 0, 0, 8) };

        _register = new Button { Text = "Registra associazioni", AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
        _unregister = new Button { Text = "Rimuovi", AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
        _openDefaultApps = new Button { Text = "Apri Default Apps…", AutoSize = true };

        _note = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            Text = "Nota: Windows può richiedere conferma utente per impostare l’app predefinita.\n" +
                   "Questa schermata registra il tipo file per l’utente corrente; se serve, scegli l’app da “Apri con…”."
        };

        var checks = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 12)
        };
        checks.Controls.Add(_mmd);
        checks.Controls.Add(_mermaid);
        checks.Controls.Add(_mermraid);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 12)
        };
        buttons.Controls.Add(_register);
        buttons.Controls.Add(_unregister);
        buttons.Controls.Add(_openDefaultApps);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(checks, 0, 0);
        root.Controls.Add(buttons, 0, 1);
        root.Controls.Add(_note, 0, 2);

        Controls.Add(root);

        Load += (_, _) => RefreshChecks();
        _register.Click += (_, _) => RegisterSelected();
        _unregister.Click += (_, _) => UnregisterSelected();
        _openDefaultApps.Click += (_, _) => FileAssociationHelper.OpenDefaultAppsSettings();
    }

    private void RefreshChecks()
    {
        _mmd.Checked = FileAssociationHelper.IsRegisteredFor(".mmd");
        _mermaid.Checked = FileAssociationHelper.IsRegisteredFor(".mermaid");
        _mermraid.Checked = FileAssociationHelper.IsRegisteredFor(".mermraid");
    }

    private void RegisterSelected()
    {
        var exts = GetSelectedExtensions();
        if (exts.Count == 0) return;

        try
        {
            FileAssociationHelper.Register(Application.ExecutablePath, exts);
            RefreshChecks();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UnregisterSelected()
    {
        var exts = GetSelectedExtensions();
        if (exts.Count == 0) return;

        try
        {
            FileAssociationHelper.Unregister(exts);
            RefreshChecks();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private List<string> GetSelectedExtensions()
    {
        var exts = new List<string>();
        if (!_mmd.Checked && !_mermaid.Checked && !_mermraid.Checked)
            return [".mmd", ".mermaid", ".mermraid"];

        if (_mmd.Checked) exts.Add(".mmd");
        if (_mermaid.Checked) exts.Add(".mermaid");
        if (_mermraid.Checked) exts.Add(".mermraid");
        return exts;
    }
}
