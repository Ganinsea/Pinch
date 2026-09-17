using System;
using System.Drawing;
using System.Windows.Forms;

namespace ImageCompressorFloat
{
    internal sealed class SettingsForm : Form
    {
        private readonly Action<PinchSettings> save;
        private PinchSettings draft;
        private readonly Button background;
        private readonly Button titleColor;
        private readonly Label backgroundHex;
        private readonly Label titleHex;
        private readonly TrackBar opacity;
        private readonly Label opacityValue;
        private readonly NumericUpDown threshold;
        private readonly ComboBox location;
        private readonly TextBox directory;
        private readonly Button browse;
        private readonly CheckBox onTop;
        private readonly CheckBox notifications;
        private readonly NumericUpDown resetSeconds;
        private readonly ToolTip toolTip = new ToolTip();

        public SettingsForm(PinchSettings current, Action<PinchSettings> save)
        {
            this.save = save; draft = current.Copy();
            Text = "Pinch \u8bbe\u7f6e - V" + AppInfo.Version;
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(500, 350); MinimumSize = Size;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen; ShowInTaskbar = false;
            Font = new Font("Microsoft YaHei UI", 9f); Padding = new Padding(12);
            TabControl tabs = new TabControl { Dock = DockStyle.Fill };
            TableLayoutPanel appearance = Page(tabs, "\u5916\u89c2");
            TableLayoutPanel compression = Page(tabs, "\u538b\u7f29\u4e0e\u4fdd\u5b58");
            TableLayoutPanel behavior = Page(tabs, "\u884c\u4e3a");
            background = Swatch("\u9009\u62e9\u6d6e\u7a97\u5e95\u8272"); titleColor = Swatch("\u9009\u62e9\u6807\u9898\u680f\u989c\u8272");
            backgroundHex = new Label { AutoSize = true, Margin = new Padding(10,8,0,0) };
            titleHex = new Label { AutoSize = true, Margin = new Padding(10,8,0,0) };
            AddRow(appearance, "\u6d6e\u7a97\u5e95\u8272", ColorRow(background, backgroundHex));
            AddRow(appearance, "\u6807\u9898\u680f\u989c\u8272", ColorRow(titleColor, titleHex));
            opacity = new TrackBar { Minimum = 30, Maximum = 100, TickFrequency = 10, SmallChange = 5, LargeChange = 10, Dock = DockStyle.Fill, AccessibleName = "\u4e0d\u900f\u660e\u5ea6" };
            opacityValue = new Label { Width = 48, TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Right };
            Panel opacityRow = new Panel { Dock = DockStyle.Fill }; opacityRow.Controls.Add(opacity); opacityRow.Controls.Add(opacityValue);
            AddRow(appearance, "\u4e0d\u900f\u660e\u5ea6", opacityRow);
            opacity.ValueChanged += delegate { opacityValue.Text = opacity.Value + "%"; };
            background.Click += delegate { PickColor(true); }; titleColor.Click += delegate { PickColor(false); };

            threshold = new NumericUpDown { Minimum = .10M, Maximum = 50M, DecimalPlaces = 2, Increment = .10M, Width = 130, Anchor = AnchorStyles.Left, AccessibleName = "\u538b\u7f29\u4e0a\u9650 MB" };
            AddRow(compression, "\u538b\u7f29\u4e0a\u9650 (MB)", threshold);
            toolTip.SetToolTip(threshold, "1 MB = 1,000,000 \u5b57\u8282");
            location = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, AccessibleName = "\u4fdd\u5b58\u4f4d\u7f6e" };
            location.Items.AddRange(new object[] { "\u539f\u56fe\u76ee\u5f55", "\u684c\u9762", "\u81ea\u5b9a\u4e49\u6587\u4ef6\u5939" });
            AddRow(compression, "\u4fdd\u5b58\u4f4d\u7f6e", location);
            toolTip.SetToolTip(location, "\u9009\u62e9\u539f\u56fe\u76ee\u5f55\u65f6\uff0c\u526a\u8d34\u677f\u4f4d\u56fe\u4fdd\u5b58\u5230\u684c\u9762");
            TableLayoutPanel folder = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            folder.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); folder.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,76));
            directory = new TextBox { Dock = DockStyle.Fill, AccessibleName = "\u81ea\u5b9a\u4e49\u4fdd\u5b58\u6587\u4ef6\u5939" };
            browse = new Button { Text = "\u6d4f\u89c8...", Dock = DockStyle.Fill };
            folder.Controls.Add(directory,0,0); folder.Controls.Add(browse,1,0); AddRow(compression, "\u6587\u4ef6\u5939", folder);
            location.SelectedIndexChanged += delegate { directory.Enabled = browse.Enabled = location.SelectedIndex == (int)SaveLocation.Custom; };
            browse.Click += delegate {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog { Description = "\u9009\u62e9\u538b\u7f29\u56fe\u7247\u4fdd\u5b58\u4f4d\u7f6e", SelectedPath = directory.Text, ShowNewFolderButton = true })
                    if (dialog.ShowDialog(this) == DialogResult.OK) directory.Text = dialog.SelectedPath;
            };
            onTop = new CheckBox { Text = "\u603b\u5728\u6700\u524d", AutoSize = true, Anchor = AnchorStyles.Left };
            notifications = new CheckBox { Text = "\u663e\u793a\u5b8c\u6210\u901a\u77e5", AutoSize = true, Anchor = AnchorStyles.Left };
            resetSeconds = new NumericUpDown { Minimum = 5, Maximum = 300, Increment = 5, Width = 130, Anchor = AnchorStyles.Left, AccessibleName = "\u63d0\u793a\u4fdd\u7559\u79d2\u6570" };
            AddRow(behavior, "\u7a97\u53e3\u7f6e\u9876", onTop); AddRow(behavior, "\u901a\u77e5", notifications); AddRow(behavior, "\u63d0\u793a\u4fdd\u7559 (\u79d2)", resetSeconds);

            FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0,10,0,0) };
            Button ok = new Button { Text = "\u4fdd\u5b58", Size = new Size(82,30) };
            Button cancel = new Button { Text = "\u53d6\u6d88", Size = new Size(82,30), DialogResult = DialogResult.Cancel };
            Button defaults = new Button { Text = "\u6062\u590d\u9ed8\u8ba4", Size = new Size(100,30) };
            buttons.Controls.Add(ok); buttons.Controls.Add(cancel); buttons.Controls.Add(defaults);
            Controls.Add(tabs); Controls.Add(buttons); AcceptButton = ok; CancelButton = cancel;
            cancel.Click += delegate { Close(); };
            defaults.Click += delegate { draft = new PinchSettings(); LoadValues(); };
            ok.Click += delegate {
                try
                {
                    draft.OpacityPercent = opacity.Value; draft.MaxOutputBytes = (long)(threshold.Value * 1000000M);
                    draft.Location = (SaveLocation)location.SelectedIndex; draft.CustomDirectory = directory.Text.Trim();
                    draft.AlwaysOnTop = onTop.Checked; draft.Notifications = notifications.Checked; draft.ResetSeconds = (int)resetSeconds.Value;
                    draft.Validate(); save(draft.Copy()); DialogResult = DialogResult.OK; Close();
                }
                catch (Exception error) { MessageBox.Show(this, error.Message, "Pinch", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
            LoadValues();
        }
        private static TableLayoutPanel Page(TabControl tabs, string name)
        {
            TabPage page = new TabPage(name) { Padding = new Padding(10), UseVisualStyleBackColor = true };
            TableLayoutPanel table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(4) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,130)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            page.Controls.Add(table); tabs.TabPages.Add(page); return table;
        }
        private static void AddRow(TableLayoutPanel table, string text, Control control)
        {
            int row = table.RowCount++; table.RowStyles.Add(new RowStyle(SizeType.Absolute,58));
            table.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft },0,row);
            table.Controls.Add(control,1,row); control.Margin = new Padding(3,10,3,10);
        }
        private Button Swatch(string name)
        {
            Button button = new Button { Width = 34, Height = 30, FlatStyle = FlatStyle.Flat, UseVisualStyleBackColor = false, AccessibleName = name };
            button.FlatAppearance.BorderColor = SystemColors.ControlDark; toolTip.SetToolTip(button,name); return button;
        }
        private static FlowLayoutPanel ColorRow(Button button, Label label)
        {
            FlowLayoutPanel row = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            row.Controls.Add(button); row.Controls.Add(label); return row;
        }
        private void PickColor(bool isBackground)
        {
            using (ColorDialog dialog = new ColorDialog { FullOpen = true, Color = isBackground ? draft.Background : draft.TitleColor })
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    if (isBackground) draft.Background = dialog.Color; else draft.TitleColor = dialog.Color;
                    UpdateColors();
                }
        }
        private void UpdateColors()
        {
            background.BackColor = draft.Background; titleColor.BackColor = draft.TitleColor;
            backgroundHex.Text = PinchSettings.ColorHex(draft.Background); titleHex.Text = PinchSettings.ColorHex(draft.TitleColor);
        }
        private void LoadValues()
        {
            UpdateColors(); opacity.Value = draft.OpacityPercent; opacityValue.Text = opacity.Value + "%";
            threshold.Value = draft.MaxOutputBytes / 1000000M; location.SelectedIndex = (int)draft.Location; directory.Text = draft.CustomDirectory;
            onTop.Checked = draft.AlwaysOnTop; notifications.Checked = draft.Notifications; resetSeconds.Value = draft.ResetSeconds;
        }
        protected override void Dispose(bool disposing) { if (disposing) toolTip.Dispose(); base.Dispose(disposing); }
    }
}
