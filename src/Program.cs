using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.Runtime.InteropServices;

namespace NihongoDeskMemo
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class WordItem
    {
        public string Japanese { get; set; }
        public string Pinyin { get; set; }
        public string Chinese { get; set; }
        public string Unit { get; set; }
        public int Weight { get; set; }
        public bool Marked { get; set; }
        public int SeenCount { get; set; }
        public string LastSeenUtc { get; set; }
        public string LastReviewTime { get; set; }
        public string NextReviewTime { get; set; }
        public int ReviewCount { get; set; }
        public int WrongCount { get; set; }
        public int CorrectStreak { get; set; }
        public double Difficulty { get; set; }
        public int IntervalDays { get; set; }

        public WordItem()
        {
            Japanese = string.Empty;
            Pinyin = string.Empty;
            Chinese = string.Empty;
            Unit = string.Empty;
            Weight = 1;
            LastSeenUtc = string.Empty;
            LastReviewTime = string.Empty;
            NextReviewTime = string.Empty;
            Difficulty = 5.0;
        }
    }

    public class WordDatabase
    {
        public List<WordItem> Words { get; set; }

        public WordDatabase()
        {
            Words = new List<WordItem>();
        }
    }

    public class AppConfig
    {
        public int RefreshSeconds { get; set; }
        public int RevealSeconds { get; set; }
        public bool AlwaysOnTop { get; set; }
        public int OpacityPercent { get; set; }
        public bool BackgroundTransparent { get; set; }
        public bool HideMeaningUntilClick { get; set; }
        public bool ShowRatingButtons { get; set; }
        public string PracticeUnit { get; set; }
        public int ReviewMode { get; set; }
        public int WindowX { get; set; }
        public int WindowY { get; set; }
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }

        public AppConfig()
        {
            RefreshSeconds = 30;
            RevealSeconds = 0;
            AlwaysOnTop = true;
            OpacityPercent = 100;
            BackgroundTransparent = true;
            HideMeaningUntilClick = true;
            ShowRatingButtons = true;
            PracticeUnit = string.Empty;
            ReviewMode = 0;
            WindowX = -1;
            WindowY = -1;
            WindowWidth = 300;
            WindowHeight = 240;
        }
    }

    internal static class Storage
    {
        public static readonly string AppDirectory =
            AppDomain.CurrentDomain.BaseDirectory;

        private static readonly string LegacyAppDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NihongoDeskMemo");

        public static readonly string DatabasePath = Path.Combine(AppDirectory, "words.xml");
        public static readonly string ConfigPath = Path.Combine(AppDirectory, "config.xml");
        private static readonly string LegacyDatabasePath = Path.Combine(LegacyAppDirectory, "words.xml");
        private static readonly string LegacyConfigPath = Path.Combine(LegacyAppDirectory, "config.xml");

        public static WordDatabase LoadDatabase()
        {
            EnsureDirectory();
            string path = File.Exists(DatabasePath) ? DatabasePath : LegacyDatabasePath;
            WordDatabase database = LoadXml<WordDatabase>(path, new WordDatabase());
            NormalizeDatabase(database);
            return database;
        }

        public static AppConfig LoadConfig()
        {
            EnsureDirectory();
            string path = File.Exists(ConfigPath) ? ConfigPath : LegacyConfigPath;
            AppConfig config = LoadXml<AppConfig>(path, new AppConfig());
            config.PracticeUnit = SafeTrim(config.PracticeUnit);
            if (config.ReviewMode < 0 || config.ReviewMode > 3)
            {
                config.ReviewMode = 0;
            }
            if (config.RefreshSeconds < 3)
            {
                config.RefreshSeconds = 3;
            }
            if (config.OpacityPercent < 45)
            {
                config.OpacityPercent = 100;
            }
            if (config.OpacityPercent > 100)
            {
                config.OpacityPercent = 100;
            }
            if (config.WindowWidth < 220)
            {
                config.WindowWidth = 300;
            }
            if (config.WindowHeight < 210)
            {
                config.WindowHeight = 210;
            }
            return config;
        }

        public static void SaveDatabase(WordDatabase database)
        {
            EnsureDirectory();
            NormalizeDatabase(database);
            SaveXml(DatabasePath, database);
        }

        public static void SaveConfig(AppConfig config)
        {
            EnsureDirectory();
            SaveXml(ConfigPath, config);
        }

        private static void EnsureDirectory()
        {
            if (!Directory.Exists(AppDirectory))
            {
                Directory.CreateDirectory(AppDirectory);
            }
        }

        private static void NormalizeDatabase(WordDatabase database)
        {
            if (database.Words == null)
            {
                database.Words = new List<WordItem>();
            }

            int i;
            for (i = database.Words.Count - 1; i >= 0; i--)
            {
                WordItem item = database.Words[i];
                if (item == null)
                {
                    database.Words.RemoveAt(i);
                    continue;
                }

                item.Japanese = SafeTrim(item.Japanese);
                item.Pinyin = SafeTrim(item.Pinyin);
                item.Chinese = SafeTrim(item.Chinese);
                item.Unit = SafeTrim(item.Unit);
                item.LastSeenUtc = item.LastSeenUtc ?? string.Empty;
                item.LastReviewTime = SafeTrim(item.LastReviewTime);
                item.NextReviewTime = SafeTrim(item.NextReviewTime);
                if (item.ReviewCount < 0) item.ReviewCount = 0;
                if (item.WrongCount < 0) item.WrongCount = 0;
                if (item.CorrectStreak < 0) item.CorrectStreak = 0;
                if (item.Difficulty <= 0) item.Difficulty = 5.0;
                if (item.Difficulty > 10) item.Difficulty = 10.0;
                if (item.IntervalDays < 0) item.IntervalDays = 0;
                if (item.Weight < 1)
                {
                    item.Weight = 1;
                }
                if (item.Weight > 80)
                {
                    item.Weight = 80;
                }
            }
        }

        public static string SafeTrim(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }

        public static List<string> GetUnits(WordDatabase database)
        {
            List<string> units = new List<string>();
            int i;
            for (i = 0; i < database.Words.Count; i++)
            {
                string unit = SafeTrim(database.Words[i].Unit);
                if (unit.Length == 0)
                {
                    continue;
                }

                bool exists = false;
                int j;
                for (j = 0; j < units.Count; j++)
                {
                    if (string.Equals(units[j], unit, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                {
                    units.Add(unit);
                }
            }
            units.Sort(StringComparer.CurrentCultureIgnoreCase);
            return units;
        }

        private static T LoadXml<T>(string path, T fallback)
        {
            if (!File.Exists(path))
            {
                return fallback;
            }

            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(T));
                    object value = serializer.Deserialize(stream);
                    if (value is T)
                    {
                        return (T)value;
                    }
                }
            }
            catch
            {
                return fallback;
            }

            return fallback;
        }

        private static void SaveXml<T>(string path, T value)
        {
            string tempPath = path + ".tmp";
            using (FileStream stream = File.Create(tempPath))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(T));
                serializer.Serialize(stream, value);
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }
            File.Move(tempPath, path);
        }
    }

    internal sealed class MainForm : Form
    {
        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int MA_ACTIVATE = 1;

        private static readonly Color TransparentColor = Color.FromArgb(255, 0, 255);
        private static readonly Color NormalBackColor = Color.FromArgb(249, 250, 251);
        private static readonly Color SoftBackColor = Color.FromArgb(239, 242, 246);

        private readonly Random random;
        private readonly Timer refreshTimer;
        private readonly Label japaneseLabel;
        private readonly Label pinyinLabel;
        private readonly Label chineseLabel;
        private readonly Label statusLabel;
        private readonly Panel contentPanel;
        private readonly Panel wordHostPanel;
        private readonly Panel detailsPanel;
        private readonly TableLayoutPanel buttonPanel;
        private readonly Button wrongButton;
        private readonly Button fuzzyButton;
        private readonly Button rememberedButton;
        private readonly Button masteredButton;
        private readonly Button libraryButton;
        private readonly Button settingsButton;
        private readonly Button hideButton;
        private readonly Button miniShowButton;
        private readonly ContextMenuStrip trayMenu;

        private WordDatabase database;
        private AppConfig config;
        private WordItem currentWord;
        private Point dragOffset;
        private Point dragStartScreen;
        private Control dragStartControl;
        private bool dragging;
        private bool dragMoved;
        private bool suppressNextClick;
        private bool answerVisible;
        private bool actionButtonsVisible;
        private bool pseudoHidden;
        private Rectangle normalBounds;



        public MainForm()
        {
            random = new Random();
            database = Storage.LoadDatabase();
            config = Storage.LoadConfig();

            Text = "Nihongo Desk Memo";
            MinimumSize = new Size(250, 210);
            Size = new Size(config.WindowWidth, config.WindowHeight);
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            ShowIcon = false;
            TopMost = config.AlwaysOnTop;
            KeyPreview = true;
            Opacity = 1.0;
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            StartPosition = FormStartPosition.Manual;

            contentPanel = new Panel();
            contentPanel.Dock = DockStyle.None;
            contentPanel.Resize += ContentPanelResize;
            contentPanel.MouseDown += DragSourceMouseDown;
            contentPanel.MouseMove += DragSourceMouseMove;
            contentPanel.MouseUp += DragSourceMouseUp;

            wordHostPanel = new Panel();
            wordHostPanel.Dock = DockStyle.None;
            wordHostPanel.Resize += WordHostPanelResize;
            wordHostPanel.MouseDown += DragSourceMouseDown;
            wordHostPanel.MouseMove += DragSourceMouseMove;
            wordHostPanel.MouseUp += DragSourceMouseUp;

            statusLabel = new Label();
            statusLabel.Dock = DockStyle.None;
            statusLabel.Height = 22;
            statusLabel.TextAlign = ContentAlignment.MiddleCenter;
            statusLabel.ForeColor = Color.FromArgb(103, 116, 139);
            statusLabel.Font = new Font("Microsoft YaHei UI", 8F, FontStyle.Regular, GraphicsUnit.Point);
            statusLabel.AutoEllipsis = true;
            statusLabel.MouseDown += DragSourceMouseDown;
            statusLabel.MouseMove += DragSourceMouseMove;
            statusLabel.MouseUp += DragSourceMouseUp;

            japaneseLabel = new Label();
            japaneseLabel.AutoSize = true;
            japaneseLabel.Dock = DockStyle.None;
            japaneseLabel.Padding = new Padding(12, 4, 12, 5);
            japaneseLabel.TextAlign = ContentAlignment.MiddleCenter;
            japaneseLabel.Font = new Font("Yu Gothic UI", 23F, FontStyle.Bold, GraphicsUnit.Point);
            japaneseLabel.ForeColor = Color.FromArgb(28, 35, 45);
            japaneseLabel.SizeChanged += JapaneseLabelSizeChanged;
            japaneseLabel.MouseDown += DragSourceMouseDown;
            japaneseLabel.MouseMove += DragSourceMouseMove;
            japaneseLabel.MouseUp += DragSourceMouseUp;

            detailsPanel = new Panel();
            detailsPanel.Dock = DockStyle.None;
            detailsPanel.Height = 58;
            detailsPanel.Resize += DetailsPanelResize;

            pinyinLabel = new Label();
            pinyinLabel.Dock = DockStyle.None;
            pinyinLabel.Height = 22;
            pinyinLabel.TextAlign = ContentAlignment.MiddleCenter;
            pinyinLabel.ForeColor = Color.FromArgb(87, 99, 118);
            pinyinLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            pinyinLabel.AutoEllipsis = true;
            pinyinLabel.Cursor = Cursors.Hand;
            pinyinLabel.MouseDown += ReadingRevealMouseDown;
            pinyinLabel.MouseDown += DragSourceMouseDown;
            pinyinLabel.MouseMove += DragSourceMouseMove;
            pinyinLabel.MouseUp += DragSourceMouseUp;

            chineseLabel = new Label();
            chineseLabel.Dock = DockStyle.None;
            chineseLabel.TextAlign = ContentAlignment.MiddleCenter;
            chineseLabel.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            chineseLabel.ForeColor = Color.FromArgb(54, 65, 82);
            chineseLabel.AutoEllipsis = true;
            chineseLabel.MouseDown += DragSourceMouseDown;
            chineseLabel.MouseMove += DragSourceMouseMove;
            chineseLabel.MouseUp += DragSourceMouseUp;

            detailsPanel.Controls.Add(chineseLabel);
            detailsPanel.Controls.Add(pinyinLabel);
            wordHostPanel.Controls.Add(japaneseLabel);
            contentPanel.Controls.Add(wordHostPanel);
            contentPanel.Controls.Add(detailsPanel);
            contentPanel.Controls.Add(statusLabel);
            detailsPanel.BringToFront();
            statusLabel.BringToFront();

            wrongButton = CreateButton("不会\r\n1");
            wrongButton.Click += WrongButtonClick;

            fuzzyButton = CreateButton("模糊\r\n2");
            fuzzyButton.Click += FuzzyButtonClick;

            rememberedButton = CreateButton("记得\r\n3");
            rememberedButton.Click += RememberedButtonClick;

            masteredButton = CreateButton("熟练\r\n4");
            masteredButton.Click += MasteredButtonClick;

            libraryButton = CreateButton("词库");
            libraryButton.Click += LibraryButtonClick;

            settingsButton = CreateButton("设置");
            settingsButton.Click += SettingsButtonClick;

            hideButton = CreateButton("隐藏");
            hideButton.Click += HideButtonClick;

            buttonPanel = new TableLayoutPanel();
            buttonPanel.Dock = DockStyle.None;
            buttonPanel.Height = 78;
            buttonPanel.ColumnCount = 12;
            buttonPanel.RowCount = 2;
            buttonPanel.Padding = new Padding(5, 3, 5, 5);
            int columnIndex;
            for (columnIndex = 0; columnIndex < 12; columnIndex++)
            {
                buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 8.333F));
            }
            buttonPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            buttonPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            buttonPanel.Controls.Add(wrongButton, 0, 0);
            buttonPanel.SetColumnSpan(wrongButton, 3);
            buttonPanel.Controls.Add(fuzzyButton, 3, 0);
            buttonPanel.SetColumnSpan(fuzzyButton, 3);
            buttonPanel.Controls.Add(rememberedButton, 6, 0);
            buttonPanel.SetColumnSpan(rememberedButton, 3);
            buttonPanel.Controls.Add(masteredButton, 9, 0);
            buttonPanel.SetColumnSpan(masteredButton, 3);
            buttonPanel.Controls.Add(libraryButton, 0, 1);
            buttonPanel.SetColumnSpan(libraryButton, 4);
            buttonPanel.Controls.Add(settingsButton, 4, 1);
            buttonPanel.SetColumnSpan(settingsButton, 4);
            buttonPanel.Controls.Add(hideButton, 8, 1);
            buttonPanel.SetColumnSpan(hideButton, 4);

            miniShowButton = CreateButton("显示");
            miniShowButton.Dock = DockStyle.None;
            miniShowButton.Visible = false;
            miniShowButton.Click += ShowButtonClick;
            miniShowButton.MouseDown += DragSourceMouseDown;
            miniShowButton.MouseMove += DragSourceMouseMove;
            miniShowButton.MouseUp += DragSourceMouseUp;

            Controls.Add(contentPanel);
            Controls.Add(buttonPanel);
            Controls.Add(miniShowButton);
            buttonPanel.BringToFront();
            miniShowButton.BringToFront();

            refreshTimer = new Timer();
            refreshTimer.Tick += RefreshTimerTick;

            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("导入词表", null, ImportMenuClick);
            trayMenu.Items.Add("词库管理", null, LibraryMenuClick);
            trayMenu.Items.Add("设置", null, SettingsButtonClick);
            trayMenu.Items.Add("隐藏/显示", null, ToggleHiddenMenuClick);
            trayMenu.Items.Add("退出", null, ExitMenuClick);
            ContextMenuStrip = trayMenu;

            Load += MainFormLoad;
            FormClosing += MainFormClosing;
            Resize += MainFormResize;
            KeyDown += MainFormKeyDown;
            LayoutMainControls();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_MOUSEACTIVATE)
            {
                m.Result = new IntPtr(MA_ACTIVATE);
                return;
            }

            base.WndProc(ref m);
        }

        private static Button CreateButton(string text)
        {
            Button button = new Button();
            button.Dock = DockStyle.Fill;
            button.Text = text;
            button.Margin = new Padding(2, 0, 2, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(202, 211, 222);
            button.BackColor = Color.White;
            button.ForeColor = Color.FromArgb(36, 44, 56);
            button.Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point);
            return button;
        }

        private void MainFormLoad(object sender, EventArgs e)
        {
            RestoreWindowLocation();
            ApplyConfig();
            ShowNextWord();
        }

        private void MainFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!pseudoHidden)
            {
                SaveCurrentWindowState();
            }
            Storage.SaveConfig(config);
            Storage.SaveDatabase(database);
        }

        private void MainFormResize(object sender, EventArgs e)
        {
            LayoutMainControls();
            FitText();
        }

        private void RestoreWindowLocation()
        {
            if (config.WindowX >= 0 && config.WindowY >= 0)
            {
                Location = new Point(config.WindowX, config.WindowY);
                return;
            }

            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.Right - Width - 24, area.Bottom - Height - 24);
        }

        private void SaveCurrentWindowState()
        {
            if (WindowState == FormWindowState.Normal)
            {
                config.WindowX = Location.X;
                config.WindowY = Location.Y;
                config.WindowWidth = Width;
                config.WindowHeight = Height;
            }
        }

        private void ApplyConfig()
        {
            TopMost = config.AlwaysOnTop;
            Opacity = 1.0;
            int refreshMs = Math.Max(3, config.RefreshSeconds) * 1000;
            refreshTimer.Interval = refreshMs;
            refreshTimer.Enabled = database.Words.Count > 0;
            ApplyBackgroundStyle();
            LayoutMainControls();
            FitText();
        }

        private void ApplyBackgroundStyle()
        {
            if (config.BackgroundTransparent || pseudoHidden)
            {
                TransparencyKey = TransparentColor;
                BackColor = TransparentColor;
                contentPanel.BackColor = TransparentColor;
                wordHostPanel.BackColor = TransparentColor;
                detailsPanel.BackColor = TransparentColor;
                buttonPanel.BackColor = TransparentColor;
                statusLabel.BackColor = TransparentColor;
                japaneseLabel.BackColor = Color.White;
                pinyinLabel.BackColor = Color.White;
                chineseLabel.BackColor = Color.White;
            }
            else
            {
                TransparencyKey = Color.Empty;
                BackColor = NormalBackColor;
                contentPanel.BackColor = NormalBackColor;
                wordHostPanel.BackColor = NormalBackColor;
                detailsPanel.BackColor = NormalBackColor;
                buttonPanel.BackColor = SoftBackColor;
                statusLabel.BackColor = NormalBackColor;
                japaneseLabel.BackColor = NormalBackColor;
                pinyinLabel.BackColor = NormalBackColor;
                chineseLabel.BackColor = NormalBackColor;
            }

            // 关键：按钮不能使用 TransparencyKey 颜色，否则会被系统当成透明洞，导致鼠标点不到。
            ApplySafeButtonStyle(wrongButton);
            ApplySafeButtonStyle(fuzzyButton);
            ApplySafeButtonStyle(rememberedButton);
            ApplySafeButtonStyle(masteredButton);
            ApplySafeButtonStyle(libraryButton);
            ApplySafeButtonStyle(settingsButton);
            ApplySafeButtonStyle(hideButton);
            ApplySafeButtonStyle(miniShowButton);
        }

        private static void ApplySafeButtonStyle(Button button)
        {
            if (button == null)
            {
                return;
            }

            button.UseVisualStyleBackColor = false;
            button.BackColor = Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(202, 211, 222);
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 247, 250);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(230, 235, 242);
        }

        private void LayoutMainControls()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
            {
                return;
            }

            if (pseudoHidden)
            {
                miniShowButton.Bounds = ClientRectangle;
                return;
            }

            int buttonHeight = 78;
            int contentHeight = Math.Max(0, ClientSize.Height - buttonHeight);
            contentPanel.Bounds = new Rectangle(0, 0, ClientSize.Width, contentHeight);
            buttonPanel.Bounds = new Rectangle(0, contentHeight, ClientSize.Width, buttonHeight);
            miniShowButton.Bounds = ClientRectangle;

            contentPanel.Visible = true;
            buttonPanel.Visible = actionButtonsVisible;
            if (actionButtonsVisible)
            {
                wrongButton.Visible = currentWord != null && answerVisible;
                fuzzyButton.Visible = currentWord != null && answerVisible;
                rememberedButton.Visible = currentWord != null && answerVisible;
                masteredButton.Visible = currentWord != null && answerVisible;
                buttonPanel.BringToFront();
            }
            LayoutContentControls();
        }

        private void ContentPanelResize(object sender, EventArgs e)
        {
            LayoutContentControls();
        }

        private void LayoutContentControls()
        {
            if (contentPanel.ClientSize.Width <= 0 || contentPanel.ClientSize.Height <= 0)
            {
                return;
            }

            int statusHeight = 22;
            int detailsHeight = detailsPanel.Visible ? detailsPanel.Height : 0;
            int wordTop = statusHeight;
            int wordHeight = Math.Max(0, contentPanel.ClientSize.Height - statusHeight - detailsHeight);

            statusLabel.Bounds = new Rectangle(0, 0, contentPanel.ClientSize.Width, statusHeight);
            wordHostPanel.Bounds = new Rectangle(0, wordTop, contentPanel.ClientSize.Width, wordHeight);
            detailsPanel.Bounds = new Rectangle(0, wordTop + wordHeight, contentPanel.ClientSize.Width, detailsHeight);

            wordHostPanel.BringToFront();
            detailsPanel.BringToFront();
            statusLabel.BringToFront();
            CenterJapaneseLabel();
            LayoutDetailsPanel();
        }

        private void FitText()
        {
            if (pseudoHidden)
            {
                return;
            }

            float wordSize = Math.Max(15F, Math.Min(25F, Width / 13F));
            float pinyinSize = Math.Max(8F, Math.Min(10F, Width / 31F));
            float meaningSize = Math.Max(8.5F, Math.Min(11F, Width / 25F));
            japaneseLabel.MaximumSize = new Size(Math.Max(140, wordHostPanel.ClientSize.Width - 18), 0);
            japaneseLabel.Font = new Font("Yu Gothic UI", wordSize, FontStyle.Bold, GraphicsUnit.Point);
            pinyinLabel.Font = new Font("Segoe UI", pinyinSize, FontStyle.Regular, GraphicsUnit.Point);
            chineseLabel.Font = new Font("Microsoft YaHei UI", meaningSize, FontStyle.Regular, GraphicsUnit.Point);
            CenterJapaneseLabel();
        }

        private void WordHostPanelResize(object sender, EventArgs e)
        {
            if (!pseudoHidden)
            {
                japaneseLabel.MaximumSize = new Size(Math.Max(140, wordHostPanel.ClientSize.Width - 18), 0);
                CenterJapaneseLabel();
            }
        }

        private void JapaneseLabelSizeChanged(object sender, EventArgs e)
        {
            CenterJapaneseLabel();
        }

        private void DetailsPanelResize(object sender, EventArgs e)
        {
            LayoutDetailsPanel();
        }

        private void CenterJapaneseLabel()
        {
            if (wordHostPanel == null || japaneseLabel == null)
            {
                return;
            }

            int x = Math.Max(0, (wordHostPanel.ClientSize.Width - japaneseLabel.Width) / 2);
            int y = Math.Max(0, (wordHostPanel.ClientSize.Height - japaneseLabel.Height) / 2);
            japaneseLabel.Location = new Point(x, y);
        }

        private void LayoutDetailsPanel()
        {
            if (detailsPanel == null || pinyinLabel == null || chineseLabel == null)
            {
                return;
            }

            int width = Math.Max(0, detailsPanel.ClientSize.Width);
            if (pinyinLabel.Visible)
            {
                int readingHeight = 26;
                pinyinLabel.Bounds = new Rectangle(0, 0, width, readingHeight);
                if (chineseLabel.Visible)
                {
                    chineseLabel.Bounds = new Rectangle(0, readingHeight, width, Math.Max(28, detailsPanel.ClientSize.Height - readingHeight));
                }
                else
                {
                    chineseLabel.Bounds = new Rectangle(0, readingHeight, width, 0);
                }
            }
            else
            {
                pinyinLabel.Bounds = new Rectangle(0, 0, width, 0);
                chineseLabel.Bounds = new Rectangle(0, 0, width, Math.Max(30, detailsPanel.ClientSize.Height));
            }
        }

        private void ShowNextWord()
        {
            if (database.Words.Count == 0)
            {
                currentWord = null;
                answerVisible = true;
                japaneseLabel.Text = "暂无词条";
                pinyinLabel.Text = string.Empty;
                pinyinLabel.Visible = false;
                chineseLabel.Text = "请在设置或词库中导入";
                chineseLabel.Visible = true;
                detailsPanel.Height = 58;
                detailsPanel.Visible = true;
                actionButtonsVisible = true;
                LayoutMainControls();
                statusLabel.Text = string.Empty;
                refreshTimer.Enabled = false;
                return;
            }

            currentWord = PickDueWord();
            if (currentWord == null)
            {
                answerVisible = false;
                japaneseLabel.Text = "暂无到期复习";
                DateTime nextDue;
                if (TryGetNextDueTime(out nextDue))
                {
                    pinyinLabel.Text = "下次：" + nextDue.ToLocalTime().ToString("MM-dd HH:mm");
                    pinyinLabel.Visible = true;
                }
                else
                {
                    pinyinLabel.Text = "当前练习单元没有词条";
                    pinyinLabel.Visible = true;
                }
                chineseLabel.Text = string.Empty;
                chineseLabel.Visible = false;
                detailsPanel.Visible = true;
                actionButtonsVisible = true;
                statusLabel.Text = config.PracticeUnit.Length == 0 ? "全部词库" : config.PracticeUnit;
                LayoutMainControls();
                refreshTimer.Enabled = true;
                return;
            }

            currentWord.SeenCount++;
            currentWord.LastSeenUtc = DateTime.UtcNow.ToString("o");

            answerVisible = !config.HideMeaningUntilClick;
            ApplyReviewModeDisplay();
            detailsPanel.Height = 58;
            detailsPanel.Visible = true;
            actionButtonsVisible = answerVisible;
            LayoutMainControls();
            statusLabel.Text = BuildStatusText();
            CenterJapaneseLabel();
        }

        private void ApplyReviewModeDisplay()
        {
            if (currentWord == null)
            {
                return;
            }

            if (config.ReviewMode == 1)
            {
                japaneseLabel.Text = currentWord.Chinese;
                pinyinLabel.Text = currentWord.Japanese;
                chineseLabel.Text = currentWord.Pinyin;
                pinyinLabel.Visible = answerVisible;
                chineseLabel.Visible = answerVisible;
            }
            else if (config.ReviewMode == 2)
            {
                japaneseLabel.Text = currentWord.Pinyin;
                pinyinLabel.Text = currentWord.Japanese;
                chineseLabel.Text = currentWord.Chinese;
                pinyinLabel.Visible = answerVisible;
                chineseLabel.Visible = answerVisible;
            }
            else
            {
                japaneseLabel.Text = currentWord.Japanese;
                pinyinLabel.Text = currentWord.Pinyin;
                chineseLabel.Text = currentWord.Chinese;
                pinyinLabel.Visible = config.ReviewMode == 0 || answerVisible;
                chineseLabel.Visible = answerVisible;
            }
        }

        private void RevealAnswer()
        {
            if (currentWord == null || answerVisible)
            {
                return;
            }

            answerVisible = true;
            actionButtonsVisible = true;
            ApplyReviewModeDisplay();
            detailsPanel.Visible = true;
            LayoutMainControls();
        }

        private string BuildStatusText()
        {
            if (currentWord == null)
            {
                return string.Empty;
            }

            string[] modeNames = new string[] { "日语认中文", "中文回忆日语", "假名回忆汉字", "汉字回忆读音" };
            return modeNames[config.ReviewMode] + "  ·  复习 " + currentWord.ReviewCount.ToString() +
                "  ·  连续 " + currentWord.CorrectStreak.ToString();
        }

        private WordItem PickDueWord()
        {
            int total = 0;
            int i;
            for (i = 0; i < database.Words.Count; i++)
            {
                if (IsInPracticeUnit(database.Words[i]) && IsEligibleForMode(database.Words[i]) && IsDue(database.Words[i]))
                {
                    total += ReviewPriority(database.Words[i]);
                }
            }

            if (total == 0)
            {
                return null;
            }

            int roll = random.Next(total);
            for (i = 0; i < database.Words.Count; i++)
            {
                if (!IsInPracticeUnit(database.Words[i]) || !IsEligibleForMode(database.Words[i]) || !IsDue(database.Words[i]))
                {
                    continue;
                }
                roll -= ReviewPriority(database.Words[i]);
                if (roll < 0)
                {
                    return database.Words[i];
                }
            }

            return null;
        }

        private static int ReviewPriority(WordItem item)
        {
            return Math.Max(1, Convert.ToInt32(Math.Round(item.Difficulty * 2.0)));
        }

        private bool IsEligibleForMode(WordItem item)
        {
            return (config.ReviewMode != 2 && config.ReviewMode != 3) ||
                Storage.SafeTrim(item.Pinyin).Length > 0;
        }

        private static bool IsDue(WordItem item)
        {
            DateTime next;
            return !TryParseUtc(item.NextReviewTime, out next) || next <= DateTime.UtcNow;
        }

        private bool TryGetNextDueTime(out DateTime nextDue)
        {
            nextDue = DateTime.MaxValue;
            bool found = false;
            int i;
            for (i = 0; i < database.Words.Count; i++)
            {
                if (!IsInPracticeUnit(database.Words[i]) || !IsEligibleForMode(database.Words[i]))
                {
                    continue;
                }
                DateTime next;
                if (TryParseUtc(database.Words[i].NextReviewTime, out next) && next < nextDue)
                {
                    nextDue = next;
                    found = true;
                }
            }
            return found;
        }

        private static bool TryParseUtc(string value, out DateTime result)
        {
            return DateTime.TryParse(
                Storage.SafeTrim(value),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out result);
        }

        private bool IsInPracticeUnit(WordItem item)
        {
            return config.PracticeUnit.Length == 0 ||
                string.Equals(Storage.SafeTrim(item.Unit), config.PracticeUnit, StringComparison.OrdinalIgnoreCase);
        }

        private void WrongButtonClick(object sender, EventArgs e)
        {
            RateCurrentWord(1);
        }

        private void FuzzyButtonClick(object sender, EventArgs e)
        {
            RateCurrentWord(2);
        }

        private void RememberedButtonClick(object sender, EventArgs e)
        {
            RateCurrentWord(3);
        }

        private void MasteredButtonClick(object sender, EventArgs e)
        {
            RateCurrentWord(4);
        }

        private void RateCurrentWord(int rating)
        {
            if (currentWord == null || !answerVisible)
            {
                return;
            }

            DateTime now = DateTime.UtcNow;
            currentWord.LastReviewTime = now.ToString("o");
            currentWord.ReviewCount++;

            if (rating == 1)
            {
                currentWord.WrongCount++;
                currentWord.CorrectStreak = 0;
                currentWord.Difficulty = Math.Min(10.0, currentWord.Difficulty + 1.0);
                currentWord.IntervalDays = 0;
                currentWord.NextReviewTime = now.AddMinutes(5).ToString("o");
                currentWord.Marked = true;
            }
            else if (rating == 2)
            {
                currentWord.CorrectStreak = 0;
                currentWord.Difficulty = Math.Min(10.0, currentWord.Difficulty + 0.3);
                currentWord.IntervalDays = 1;
                currentWord.NextReviewTime = now.AddDays(1).ToString("o");
            }
            else
            {
                currentWord.CorrectStreak++;
                currentWord.Difficulty = Math.Max(1.0, currentWord.Difficulty - (rating == 4 ? 0.5 : 0.25));
                currentWord.IntervalDays = NextCorrectInterval(currentWord.IntervalDays, rating == 4 ? 7 : 3);
                currentWord.NextReviewTime = now.AddDays(currentWord.IntervalDays).ToString("o");
            }

            Storage.SaveDatabase(database);
            ShowNextWord();
        }

        private static int NextCorrectInterval(int currentDays, int minimumDays)
        {
            int[] intervals = new int[] { 3, 7, 15, 30, 60 };
            int i;
            for (i = 0; i < intervals.Length; i++)
            {
                if (intervals[i] >= minimumDays && intervals[i] > currentDays)
                {
                    return intervals[i];
                }
            }
            return 60;
        }

        private void MainFormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F8)
            {
                RevealAnswer();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            int rating = 0;
            if (e.KeyCode == Keys.D1 || e.KeyCode == Keys.NumPad1) rating = 1;
            if (e.KeyCode == Keys.D2 || e.KeyCode == Keys.NumPad2) rating = 2;
            if (e.KeyCode == Keys.D3 || e.KeyCode == Keys.NumPad3) rating = 3;
            if (e.KeyCode == Keys.D4 || e.KeyCode == Keys.NumPad4) rating = 4;
            if (rating > 0 && answerVisible)
            {
                RateCurrentWord(rating);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void ReadingRevealMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                RevealAnswer();
            }
        }

        private void SettingsButtonClick(object sender, EventArgs e)
        {
            using (SettingsForm form = new SettingsForm(config, database))
            {
                form.ImportRequested += SettingsImportRequested;
                form.ClearWeightsRequested += SettingsClearWeightsRequested;
                form.LibraryRequested += SettingsLibraryRequested;
                DialogResult result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    ApplyConfig();
                    Storage.SaveConfig(config);
                    Storage.SaveDatabase(database);
                    ShowNextWord();
                }
            }
        }

        private void LibraryButtonClick(object sender, EventArgs e)
        {
            OpenLibrary(this);
        }

        private void LibraryMenuClick(object sender, EventArgs e)
        {
            OpenLibrary(this);
        }

        private void SettingsLibraryRequested(object sender, EventArgs e)
        {
            OpenLibrary(sender as IWin32Window);
        }

        private void OpenLibrary(IWin32Window owner)
        {
            using (WordLibraryForm form = new WordLibraryForm(database))
            {
                DialogResult result = form.ShowDialog(owner);
                if (result == DialogResult.OK || result == DialogResult.Cancel)
                {
                    Storage.SaveDatabase(database);
                    ApplyConfig();
                    ShowNextWord();
                }
            }
        }

        private void SettingsImportRequested(object sender, EventArgs e)
        {
            ImportWordsFromDialog(sender as IWin32Window);
        }

        private void SettingsClearWeightsRequested(object sender, EventArgs e)
        {
            int i;
            for (i = 0; i < database.Words.Count; i++)
            {
                database.Words[i].Weight = 1;
                database.Words[i].Marked = false;
                database.Words[i].LastReviewTime = string.Empty;
                database.Words[i].NextReviewTime = string.Empty;
                database.Words[i].ReviewCount = 0;
                database.Words[i].WrongCount = 0;
                database.Words[i].CorrectStreak = 0;
                database.Words[i].Difficulty = 5.0;
                database.Words[i].IntervalDays = 0;
            }
            Storage.SaveDatabase(database);
            MessageBox.Show(sender as IWin32Window, "复习进度已重置。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ImportMenuClick(object sender, EventArgs e)
        {
            ImportWordsFromDialog(this);
        }

        private void ToggleHiddenMenuClick(object sender, EventArgs e)
        {
            if (pseudoHidden)
            {
                RestoreFromPseudoHidden();
            }
            else
            {
                EnterPseudoHidden();
            }
        }

        private void HideButtonClick(object sender, EventArgs e)
        {
            EnterPseudoHidden();
        }

        private void ShowButtonClick(object sender, EventArgs e)
        {
            if (suppressNextClick)
            {
                suppressNextClick = false;
                return;
            }

            RestoreFromPseudoHidden();
        }

        private void EnterPseudoHidden()
        {
            if (pseudoHidden)
            {
                return;
            }

            pseudoHidden = true;
            normalBounds = Bounds;
            SaveCurrentWindowState();
            MinimumSize = new Size(64, 30);
            FormBorderStyle = FormBorderStyle.None;
            contentPanel.Visible = false;
            buttonPanel.Visible = false;
            miniShowButton.Visible = true;
            miniShowButton.BringToFront();
            Size = new Size(68, 30);
            LayoutMainControls();
            ApplyBackgroundStyle();
        }

        private void RestoreFromPseudoHidden()
        {
            if (!pseudoHidden)
            {
                return;
            }

            pseudoHidden = false;
            miniShowButton.Visible = false;
            contentPanel.Visible = true;
            buttonPanel.Visible = true;
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            MinimumSize = new Size(250, 210);
            Bounds = normalBounds.Width > 0 ? normalBounds : new Rectangle(Location, new Size(config.WindowWidth, config.WindowHeight));
            LayoutMainControls();
            ApplyBackgroundStyle();
            FitText();
        }

        private void ExitMenuClick(object sender, EventArgs e)
        {
            Close();
        }

        private void RefreshTimerTick(object sender, EventArgs e)
        {
            ShowNextWord();
        }

        private void ImportWordsFromDialog(IWin32Window owner)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "导入词表";
                dialog.Filter = "文本文件 (*.txt;*.csv)|*.txt;*.csv|所有文件 (*.*)|*.*";
                dialog.Multiselect = false;
                if (dialog.ShowDialog(owner) != DialogResult.OK)
                {
                    return;
                }

                ImportResult result = WordImporter.ImportFile(dialog.FileName, database);
                Storage.SaveDatabase(database);
                ApplyConfig();
                ShowNextWord();

                MessageBox.Show(
                    owner,
                    "新增 " + result.Added.ToString() + " 条，更新 " + result.Updated.ToString() + " 条，跳过 " + result.Skipped.ToString() + " 行。",
                    "导入完成",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private void DragSourceMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            Control source = sender as Control;
            if (source == null)
            {
                return;
            }

            dragging = true;
            dragMoved = false;
            dragStartControl = source;
            dragStartScreen = source.PointToScreen(e.Location);
            dragOffset = new Point(dragStartScreen.X - Location.X, dragStartScreen.Y - Location.Y);
        }

        private void DragSourceMouseMove(object sender, MouseEventArgs e)
        {
            if (!dragging)
            {
                return;
            }

            Control source = sender as Control;
            if (source == null)
            {
                return;
            }

            Point screen = source.PointToScreen(e.Location);
            int dx = Math.Abs(screen.X - dragStartScreen.X);
            int dy = Math.Abs(screen.Y - dragStartScreen.Y);
            if (!dragMoved && dx < 4 && dy < 4)
            {
                return;
            }

            dragMoved = true;
            Location = new Point(screen.X - dragOffset.X, screen.Y - dragOffset.Y);
        }

        private void DragSourceMouseUp(object sender, MouseEventArgs e)
        {
            if (pseudoHidden && dragMoved)
            {
                normalBounds = new Rectangle(Location, normalBounds.Size);
            }

            if (sender == miniShowButton && dragMoved)
            {
                suppressNextClick = true;
            }

            dragging = false;
            dragMoved = false;
            dragStartControl = null;
        }
    }

    internal sealed class SettingsForm : Form
    {
        private readonly AppConfig config;
        private readonly WordDatabase database;
        private readonly NumericUpDown refreshSeconds;
        private readonly CheckBox topMostCheckBox;
        private readonly CheckBox backgroundTransparentCheckBox;
        private readonly CheckBox hideMeaningUntilClickCheckBox;
        private readonly ComboBox reviewModeComboBox;
        private readonly ComboBox practiceUnitComboBox;
        private readonly Label wordCountLabel;

        public event EventHandler ImportRequested;
        public event EventHandler ClearWeightsRequested;
        public event EventHandler LibraryRequested;

        public SettingsForm(AppConfig appConfig, WordDatabase wordDatabase)
        {
            config = appConfig;
            database = wordDatabase;

            Text = "设置";
            ShowIcon = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(390, 354);
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Color.FromArgb(249, 250, 251);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(14);
            layout.ColumnCount = 2;
            layout.RowCount = 10;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            refreshSeconds = new NumericUpDown();
            refreshSeconds.Minimum = 3;
            refreshSeconds.Maximum = 3600;
            refreshSeconds.Value = Math.Max(3, config.RefreshSeconds);
            refreshSeconds.Dock = DockStyle.Fill;

            topMostCheckBox = new CheckBox();
            topMostCheckBox.Text = "窗口置顶";
            topMostCheckBox.Checked = config.AlwaysOnTop;
            topMostCheckBox.Dock = DockStyle.Fill;

            backgroundTransparentCheckBox = new CheckBox();
            backgroundTransparentCheckBox.Text = "仅背景透明";
            backgroundTransparentCheckBox.Checked = config.BackgroundTransparent;
            backgroundTransparentCheckBox.Dock = DockStyle.Fill;

            hideMeaningUntilClickCheckBox = new CheckBox();
            hideMeaningUntilClickCheckBox.Text = "答案需点击或按 F8 显示";
            hideMeaningUntilClickCheckBox.Checked = config.HideMeaningUntilClick;
            hideMeaningUntilClickCheckBox.Dock = DockStyle.Fill;

            reviewModeComboBox = new ComboBox();
            reviewModeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            reviewModeComboBox.Dock = DockStyle.Fill;
            reviewModeComboBox.Items.Add("日语认中文");
            reviewModeComboBox.Items.Add("中文回忆日语");
            reviewModeComboBox.Items.Add("假名回忆汉字");
            reviewModeComboBox.Items.Add("汉字回忆读音");
            reviewModeComboBox.SelectedIndex = Math.Max(0, Math.Min(3, config.ReviewMode));

            practiceUnitComboBox = new ComboBox();
            practiceUnitComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            practiceUnitComboBox.Dock = DockStyle.Fill;
            PopulatePracticeUnits();

            wordCountLabel = new Label();
            wordCountLabel.Dock = DockStyle.Fill;
            wordCountLabel.TextAlign = ContentAlignment.MiddleLeft;
            wordCountLabel.AutoEllipsis = true;
            wordCountLabel.Text = "词条 " + database.Words.Count.ToString() + " 条";

            Button importButton = MainFormButton("导入词表");
            importButton.Click += ImportButtonClick;

            Button libraryButton = MainFormButton("词库管理");
            libraryButton.Click += LibraryButtonClick;

            Button clearButton = MainFormButton("重置复习进度");
            clearButton.Click += ClearButtonClick;

            FlowLayoutPanel actionPanel = new FlowLayoutPanel();
            actionPanel.Dock = DockStyle.Fill;
            actionPanel.FlowDirection = FlowDirection.RightToLeft;

            Button okButton = MainFormButton("保存并关闭");
            okButton.Width = 98;
            okButton.Click += OkButtonClick;

            Button cancelButton = MainFormButton("取消");
            cancelButton.Width = 74;
            cancelButton.DialogResult = DialogResult.Cancel;

            actionPanel.Controls.Add(okButton);
            actionPanel.Controls.Add(cancelButton);

            AddLabel(layout, "刷新间隔", 0);
            layout.Controls.Add(refreshSeconds, 1, 0);
            AddLabel(layout, "固定窗口", 1);
            layout.Controls.Add(topMostCheckBox, 1, 1);
            AddLabel(layout, "背景", 2);
            layout.Controls.Add(backgroundTransparentCheckBox, 1, 2);
            AddLabel(layout, "意思显示", 3);
            layout.Controls.Add(hideMeaningUntilClickCheckBox, 1, 3);
            AddLabel(layout, "抽题方向", 4);
            layout.Controls.Add(reviewModeComboBox, 1, 4);
            AddLabel(layout, "练习单元", 5);
            layout.Controls.Add(practiceUnitComboBox, 1, 5);
            AddLabel(layout, "词库", 6);
            layout.Controls.Add(wordCountLabel, 1, 6);
            layout.Controls.Add(importButton, 0, 7);
            layout.Controls.Add(libraryButton, 1, 7);
            layout.Controls.Add(clearButton, 0, 8);
            layout.SetColumnSpan(actionPanel, 2);
            layout.Controls.Add(actionPanel, 0, 9);

            Controls.Add(layout);
            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        private static void AddLabel(TableLayoutPanel layout, string text, int row)
        {
            Label label = new Label();
            label.Text = text;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.ForeColor = Color.FromArgb(67, 77, 92);
            layout.Controls.Add(label, 0, row);
        }

        private static Button MainFormButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.Height = 28;
            button.Dock = DockStyle.Fill;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(202, 211, 222);
            button.BackColor = Color.White;
            return button;
        }

        private void PopulatePracticeUnits()
        {
            string selected = config.PracticeUnit;
            if (practiceUnitComboBox.SelectedIndex > 0 && practiceUnitComboBox.SelectedItem != null)
            {
                selected = Storage.SafeTrim(practiceUnitComboBox.SelectedItem.ToString());
            }
            practiceUnitComboBox.Items.Clear();
            practiceUnitComboBox.Items.Add("全部词库");
            List<string> units = Storage.GetUnits(database);
            int selectedIndex = 0;
            int i;
            for (i = 0; i < units.Count; i++)
            {
                practiceUnitComboBox.Items.Add(units[i]);
                if (string.Equals(units[i], selected, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i + 1;
                }
            }
            practiceUnitComboBox.SelectedIndex = selectedIndex;
        }

        private void ImportButtonClick(object sender, EventArgs e)
        {
            EventHandler handler = ImportRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
            wordCountLabel.Text = "词条 " + database.Words.Count.ToString() + " 条";
            PopulatePracticeUnits();
        }

        private void LibraryButtonClick(object sender, EventArgs e)
        {
            EventHandler handler = LibraryRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
            wordCountLabel.Text = "词条 " + database.Words.Count.ToString() + " 条";
            PopulatePracticeUnits();
        }

        private void ClearButtonClick(object sender, EventArgs e)
        {
            if (database.Words.Count == 0)
            {
                return;
            }

            DialogResult result = MessageBox.Show(
                this,
                "确定清空所有词条的复习进度、标记和权重吗？",
                "确认",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);

            if (result != DialogResult.OK)
            {
                return;
            }

            EventHandler handler = ClearWeightsRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void OkButtonClick(object sender, EventArgs e)
        {
            config.RefreshSeconds = Convert.ToInt32(refreshSeconds.Value);
            config.AlwaysOnTop = topMostCheckBox.Checked;
            config.BackgroundTransparent = backgroundTransparentCheckBox.Checked;
            config.HideMeaningUntilClick = hideMeaningUntilClickCheckBox.Checked;
            config.ReviewMode = Math.Max(0, reviewModeComboBox.SelectedIndex);
            config.PracticeUnit = practiceUnitComboBox.SelectedIndex <= 0
                ? string.Empty
                : Storage.SafeTrim(practiceUnitComboBox.SelectedItem as string);
            config.OpacityPercent = 100;
            config.RevealSeconds = 0;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    internal sealed class WordLibraryForm : Form
    {
        private readonly WordDatabase database;
        private readonly TextBox searchBox;
        private readonly DataGridView grid;
        private readonly Label countLabel;
        private readonly ComboBox unitBox;
        private bool refreshing;

        public WordLibraryForm(WordDatabase wordDatabase)
        {
            database = wordDatabase;

            Text = "词库管理";
            ShowIcon = false;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            ClientSize = new Size(900, 480);
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Color.FromArgb(249, 250, 251);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(12);
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));

            TableLayoutPanel searchPanel = new TableLayoutPanel();
            searchPanel.Dock = DockStyle.Fill;
            searchPanel.ColumnCount = 2;
            searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58F));
            searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            Label searchLabel = new Label();
            searchLabel.Text = "搜索";
            searchLabel.TextAlign = ContentAlignment.MiddleLeft;
            searchLabel.Dock = DockStyle.Fill;

            searchBox = new TextBox();
            searchBox.Dock = DockStyle.Fill;
            searchBox.TextChanged += SearchBoxTextChanged;

            searchPanel.Controls.Add(searchLabel, 0, 0);
            searchPanel.Controls.Add(searchBox, 1, 0);

            grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AutoGenerateColumns = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = true;
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.FixedSingle;
            grid.RowHeadersVisible = false;
            grid.CellEndEdit += GridCellEndEdit;
            grid.CellValueChanged += GridCellValueChanged;
            grid.CurrentCellDirtyStateChanged += GridCurrentCellDirtyStateChanged;

            AddTextColumn("Japanese", "单词", 140, false);
            AddTextColumn("Pinyin", "假名/读音", 140, false);
            AddTextColumn("Chinese", "意思", 260, true);
            AddTextColumn("Unit", "单元", 100, false);
            AddTextColumn("Weight", "权重", 60, false);
            AddCheckColumn("Marked", "标记", 54);
            AddTextColumn("SeenCount", "出现", 54, false).ReadOnly = true;
            AddTextColumn("LastReviewTime", "上次复习", 120, false).ReadOnly = true;
            AddTextColumn("NextReviewTime", "下次复习", 120, false).ReadOnly = true;
            AddTextColumn("ReviewCount", "复习", 54, false).ReadOnly = true;
            AddTextColumn("WrongCount", "错误", 54, false).ReadOnly = true;
            AddTextColumn("CorrectStreak", "连续", 54, false).ReadOnly = true;
            AddTextColumn("Difficulty", "难度", 60, false).ReadOnly = true;
            AddTextColumn("IntervalDays", "间隔天", 64, false).ReadOnly = true;

            countLabel = new Label();
            countLabel.Dock = DockStyle.Fill;
            countLabel.TextAlign = ContentAlignment.MiddleLeft;
            countLabel.ForeColor = Color.FromArgb(87, 99, 118);

            FlowLayoutPanel actionPanel = new FlowLayoutPanel();
            actionPanel.Dock = DockStyle.Fill;
            actionPanel.FlowDirection = FlowDirection.RightToLeft;

            Button closeButton = FormButton("关闭");
            closeButton.Width = 74;
            closeButton.Click += CloseButtonClick;

            Button saveButton = FormButton("保存");
            saveButton.Width = 74;
            saveButton.Click += SaveButtonClick;

            Button deleteButton = FormButton("删除");
            deleteButton.Width = 74;
            deleteButton.Click += DeleteButtonClick;

            Button editButton = FormButton("编辑");
            editButton.Width = 74;
            editButton.Click += EditButtonClick;

            Button addButton = FormButton("新增");
            addButton.Width = 74;
            addButton.Click += AddButtonClick;

            Button assignUnitButton = FormButton("划分单元");
            assignUnitButton.Width = 86;
            assignUnitButton.Click += AssignUnitButtonClick;

            unitBox = new ComboBox();
            unitBox.Width = 120;
            unitBox.DropDownStyle = ComboBoxStyle.DropDown;
            unitBox.Text = string.Empty;

            actionPanel.Controls.Add(closeButton);
            actionPanel.Controls.Add(saveButton);
            actionPanel.Controls.Add(deleteButton);
            actionPanel.Controls.Add(editButton);
            actionPanel.Controls.Add(addButton);
            actionPanel.Controls.Add(assignUnitButton);
            actionPanel.Controls.Add(unitBox);

            root.Controls.Add(searchPanel, 0, 0);
            root.Controls.Add(grid, 0, 1);
            root.Controls.Add(countLabel, 0, 2);
            root.Controls.Add(actionPanel, 0, 3);

            Controls.Add(root);
            FormClosing += WordLibraryFormClosing;
            Load += WordLibraryFormLoad;
        }

        private DataGridViewTextBoxColumn AddTextColumn(string name, string header, int width, bool fill)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.Name = name;
            column.HeaderText = header;
            column.Width = width;
            if (fill)
            {
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }
            grid.Columns.Add(column);
            return column;
        }

        private void AddCheckColumn(string name, string header, int width)
        {
            DataGridViewCheckBoxColumn column = new DataGridViewCheckBoxColumn();
            column.Name = name;
            column.HeaderText = header;
            column.Width = width;
            grid.Columns.Add(column);
        }

        private static Button FormButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.Height = 28;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(202, 211, 222);
            button.BackColor = Color.White;
            return button;
        }

        private void WordLibraryFormLoad(object sender, EventArgs e)
        {
            PopulateUnitBox();
            RefreshGrid();
        }

        private void PopulateUnitBox()
        {
            string current = Storage.SafeTrim(unitBox.Text);
            unitBox.Items.Clear();
            List<string> units = Storage.GetUnits(database);
            int i;
            for (i = 0; i < units.Count; i++)
            {
                unitBox.Items.Add(units[i]);
            }
            unitBox.Text = current;
        }

        private void SearchBoxTextChanged(object sender, EventArgs e)
        {
            CommitAllGridRows();
            RefreshGrid();
        }

        private void GridCurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (grid.IsCurrentCellDirty)
            {
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void GridCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (refreshing || e.RowIndex < 0)
            {
                return;
            }
            CommitGridRow(grid.Rows[e.RowIndex]);
        }

        private void GridCellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (refreshing || e.RowIndex < 0)
            {
                return;
            }
            CommitGridRow(grid.Rows[e.RowIndex]);
        }

        private void RefreshGrid()
        {
            refreshing = true;
            grid.Rows.Clear();

            string query = Storage.SafeTrim(searchBox.Text).ToLowerInvariant();
            int shown = 0;
            int i;
            for (i = 0; i < database.Words.Count; i++)
            {
                WordItem item = database.Words[i];
                if (!Matches(item, query))
                {
                    continue;
                }

                int rowIndex = grid.Rows.Add(
                    item.Japanese,
                    item.Pinyin,
                    item.Chinese,
                    item.Unit,
                    item.Weight.ToString(),
                    item.Marked,
                    item.SeenCount.ToString(),
                    FormatReviewTime(item.LastReviewTime),
                    FormatReviewTime(item.NextReviewTime),
                    item.ReviewCount.ToString(),
                    item.WrongCount.ToString(),
                    item.CorrectStreak.ToString(),
                    item.Difficulty.ToString("0.0"),
                    item.IntervalDays.ToString());
                grid.Rows[rowIndex].Tag = item;
                shown++;
            }

            countLabel.Text = "显示 " + shown.ToString() + " 条 / 共 " + database.Words.Count.ToString() + " 条";
            refreshing = false;
        }

        private static string FormatReviewTime(string value)
        {
            DateTime time;
            if (!DateTime.TryParse(
                Storage.SafeTrim(value),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out time))
            {
                return string.Empty;
            }
            return time.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        }

        private static bool Matches(WordItem item, string query)
        {
            if (query.Length == 0)
            {
                return true;
            }

            return Contains(item.Japanese, query) || Contains(item.Pinyin, query) ||
                Contains(item.Chinese, query) || Contains(item.Unit, query);
        }

        private static bool Contains(string value, string query)
        {
            if (value == null)
            {
                return false;
            }
            return value.ToLowerInvariant().Contains(query);
        }

        private void CommitAllGridRows()
        {
            grid.EndEdit();
            int i;
            for (i = 0; i < grid.Rows.Count; i++)
            {
                CommitGridRow(grid.Rows[i]);
            }
        }

        private void CommitGridRow(DataGridViewRow row)
        {
            if (row == null || row.Tag == null)
            {
                return;
            }

            WordItem item = row.Tag as WordItem;
            if (item == null)
            {
                return;
            }

            item.Japanese = CellText(row, "Japanese");
            item.Pinyin = CellText(row, "Pinyin");
            item.Chinese = CellText(row, "Chinese");
            item.Unit = CellText(row, "Unit");
            item.Weight = Clamp(ParseInt(CellText(row, "Weight"), item.Weight), 1, 80);
            item.Marked = CellBool(row, "Marked");

            row.Cells["Weight"].Value = item.Weight.ToString();
        }

        private static string CellText(DataGridViewRow row, string columnName)
        {
            object value = row.Cells[columnName].Value;
            return value == null ? string.Empty : value.ToString().Trim();
        }

        private static bool CellBool(DataGridViewRow row, string columnName)
        {
            object value = row.Cells[columnName].Value;
            if (value is bool)
            {
                return (bool)value;
            }
            return value != null && value.ToString().Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private static int ParseInt(string value, int fallback)
        {
            int parsed;
            if (int.TryParse(value, out parsed))
            {
                return parsed;
            }
            return fallback;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }
            if (value > max)
            {
                return max;
            }
            return value;
        }

        private void AddButtonClick(object sender, EventArgs e)
        {
            WordItem item = new WordItem();
            using (WordEditForm form = new WordEditForm(item, "新增词条"))
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
            }

            database.Words.Add(item);
            Storage.SaveDatabase(database);
            RefreshGrid();
        }

        private void EditButtonClick(object sender, EventArgs e)
        {
            WordItem item = SelectedWord();
            if (item == null)
            {
                return;
            }

            using (WordEditForm form = new WordEditForm(item, "编辑词条"))
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
            }

            Storage.SaveDatabase(database);
            RefreshGrid();
        }

        private void DeleteButtonClick(object sender, EventArgs e)
        {
            if (grid.SelectedRows.Count == 0)
            {
                return;
            }

            DialogResult result = MessageBox.Show(
                this,
                "确定删除选中的词条吗？",
                "确认",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);

            if (result != DialogResult.OK)
            {
                return;
            }

            List<WordItem> selected = new List<WordItem>();
            int i;
            for (i = 0; i < grid.SelectedRows.Count; i++)
            {
                WordItem item = grid.SelectedRows[i].Tag as WordItem;
                if (item != null && !selected.Contains(item))
                {
                    selected.Add(item);
                }
            }

            for (i = 0; i < selected.Count; i++)
            {
                database.Words.Remove(selected[i]);
            }

            Storage.SaveDatabase(database);
            RefreshGrid();
        }

        private void AssignUnitButtonClick(object sender, EventArgs e)
        {
            if (grid.SelectedRows.Count == 0)
            {
                MessageBox.Show(this, "请先选择需要划分的词条。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            CommitAllGridRows();
            string unit = Storage.SafeTrim(unitBox.Text);
            int i;
            for (i = 0; i < grid.SelectedRows.Count; i++)
            {
                WordItem item = grid.SelectedRows[i].Tag as WordItem;
                if (item != null)
                {
                    item.Unit = unit;
                }
            }

            Storage.SaveDatabase(database);
            PopulateUnitBox();
            RefreshGrid();
        }

        private WordItem SelectedWord()
        {
            if (grid.SelectedRows.Count > 0)
            {
                return grid.SelectedRows[0].Tag as WordItem;
            }
            if (grid.CurrentRow != null)
            {
                return grid.CurrentRow.Tag as WordItem;
            }
            return null;
        }

        private void SaveButtonClick(object sender, EventArgs e)
        {
            CommitAllGridRows();
            RemoveBlankWords();
            Storage.SaveDatabase(database);
            RefreshGrid();
            MessageBox.Show(this, "词库已保存。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void CloseButtonClick(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void WordLibraryFormClosing(object sender, FormClosingEventArgs e)
        {
            CommitAllGridRows();
            RemoveBlankWords();
            Storage.SaveDatabase(database);
        }

        private void RemoveBlankWords()
        {
            int i;
            for (i = database.Words.Count - 1; i >= 0; i--)
            {
                WordItem item = database.Words[i];
                if (item == null || Storage.SafeTrim(item.Japanese).Length == 0 || Storage.SafeTrim(item.Chinese).Length == 0)
                {
                    database.Words.RemoveAt(i);
                }
            }
        }
    }

    internal sealed class WordEditForm : Form
    {
        private readonly WordItem item;
        private readonly TextBox japaneseBox;
        private readonly TextBox pinyinBox;
        private readonly TextBox chineseBox;
        private readonly TextBox unitBox;
        private readonly NumericUpDown weightBox;
        private readonly CheckBox markedBox;

        public WordEditForm(WordItem wordItem, string title)
        {
            item = wordItem;

            Text = title;
            ShowIcon = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(420, 274);
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Color.FromArgb(249, 250, 251);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(14);
            layout.ColumnCount = 2;
            layout.RowCount = 7;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            japaneseBox = new TextBox();
            japaneseBox.Dock = DockStyle.Fill;
            japaneseBox.Text = item.Japanese;

            pinyinBox = new TextBox();
            pinyinBox.Dock = DockStyle.Fill;
            pinyinBox.Text = item.Pinyin;

            chineseBox = new TextBox();
            chineseBox.Dock = DockStyle.Fill;
            chineseBox.Multiline = true;
            chineseBox.ScrollBars = ScrollBars.Vertical;
            chineseBox.Text = item.Chinese;

            unitBox = new TextBox();
            unitBox.Dock = DockStyle.Fill;
            unitBox.Text = item.Unit;

            weightBox = new NumericUpDown();
            weightBox.Minimum = 1;
            weightBox.Maximum = 80;
            weightBox.Value = Math.Max(1, Math.Min(80, item.Weight));
            weightBox.Dock = DockStyle.Fill;

            markedBox = new CheckBox();
            markedBox.Text = "已标记";
            markedBox.Checked = item.Marked;
            markedBox.Dock = DockStyle.Fill;

            FlowLayoutPanel actionPanel = new FlowLayoutPanel();
            actionPanel.Dock = DockStyle.Fill;
            actionPanel.FlowDirection = FlowDirection.RightToLeft;

            Button okButton = FormButton("保存");
            okButton.Width = 74;
            okButton.Click += OkButtonClick;

            Button cancelButton = FormButton("取消");
            cancelButton.Width = 74;
            cancelButton.DialogResult = DialogResult.Cancel;

            actionPanel.Controls.Add(okButton);
            actionPanel.Controls.Add(cancelButton);

            AddLabel(layout, "单词", 0);
            layout.Controls.Add(japaneseBox, 1, 0);
            AddLabel(layout, "假名/读音", 1);
            layout.Controls.Add(pinyinBox, 1, 1);
            AddLabel(layout, "意思", 2);
            layout.Controls.Add(chineseBox, 1, 2);
            AddLabel(layout, "权重", 3);
            layout.Controls.Add(weightBox, 1, 3);
            AddLabel(layout, "单元", 4);
            layout.Controls.Add(unitBox, 1, 4);
            AddLabel(layout, "标记", 5);
            layout.Controls.Add(markedBox, 1, 5);
            layout.SetColumnSpan(actionPanel, 2);
            layout.Controls.Add(actionPanel, 0, 6);

            Controls.Add(layout);
            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        private static void AddLabel(TableLayoutPanel layout, string text, int row)
        {
            Label label = new Label();
            label.Text = text;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.ForeColor = Color.FromArgb(67, 77, 92);
            layout.Controls.Add(label, 0, row);
        }

        private static Button FormButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.Height = 28;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(202, 211, 222);
            button.BackColor = Color.White;
            return button;
        }

        private void OkButtonClick(object sender, EventArgs e)
        {
            string japanese = Storage.SafeTrim(japaneseBox.Text);
            string chinese = Storage.SafeTrim(chineseBox.Text);
            if (japanese.Length == 0 || chinese.Length == 0)
            {
                MessageBox.Show(this, "单词和意思不能为空。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            item.Japanese = japanese;
            item.Pinyin = Storage.SafeTrim(pinyinBox.Text);
            item.Chinese = chinese;
            item.Unit = Storage.SafeTrim(unitBox.Text);
            item.Weight = Convert.ToInt32(weightBox.Value);
            item.Marked = markedBox.Checked;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    internal sealed class ImportResult
    {
        public int Added;
        public int Updated;
        public int Skipped;
    }

    internal static class WordImporter
    {
        private static readonly char[] SimpleSeparators = new char[] { '\t', ',', '，', '=', '：', ':' };
        private static readonly string[] WideSeparators = new string[] { " - ", " -- ", " —— ", " — " };

        public static ImportResult ImportFile(string path, WordDatabase database)
        {
            ImportResult result = new ImportResult();
            string[] lines = File.ReadAllLines(path, DetectEncoding(path));
            Dictionary<string, WordItem> byJapanese = BuildIndex(database);

            int i;
            for (i = 0; i < lines.Length; i++)
            {
                WordItem item = ParseLine(lines[i]);
                if (item == null)
                {
                    result.Skipped++;
                    continue;
                }

                string key = NormalizeKey(item.Japanese);
                if (byJapanese.ContainsKey(key))
                {
                    WordItem existing = byJapanese[key];
                    existing.Pinyin = item.Pinyin;
                    existing.Chinese = item.Chinese;
                    result.Updated++;
                }
                else
                {
                    database.Words.Add(item);
                    byJapanese[key] = item;
                    result.Added++;
                }
            }

            return result;
        }

        private static Dictionary<string, WordItem> BuildIndex(WordDatabase database)
        {
            Dictionary<string, WordItem> map = new Dictionary<string, WordItem>();
            int i;
            for (i = 0; i < database.Words.Count; i++)
            {
                string key = NormalizeKey(database.Words[i].Japanese);
                if (!map.ContainsKey(key))
                {
                    map.Add(key, database.Words[i]);
                }
            }
            return map;
        }

        //private static WordItem ParseLine(string rawLine)
        //{
        //    if (rawLine == null)
        //    {
        //        return null;
        //    }

        //    string line = rawLine.Trim();
        //    if (line.Length == 0 || line.StartsWith("#"))
        //    {
        //        return null;
        //    }

        //    List<string> parts = SplitColumns(line);
        //    if (parts.Count < 2)
        //    {
        //        return null;
        //    }

        //    WordItem item = new WordItem();
        //    item.Japanese = parts[0];
        //    if (parts.Count >= 3)
        //    {
        //        item.Pinyin = parts[1];
        //        item.Chinese = JoinMeaning(parts, 2);
        //    }
        //    else
        //    {
        //        item.Pinyin = string.Empty;
        //        item.Chinese = parts[1];
        //    }

        //    item.Japanese = Storage.SafeTrim(item.Japanese);
        //    item.Pinyin = Storage.SafeTrim(item.Pinyin);
        //    item.Chinese = Storage.SafeTrim(item.Chinese);
        //    item.Weight = 1;
        //    item.Marked = false;

        //    if (item.Japanese.Length == 0 || item.Chinese.Length == 0)
        //    {
        //        return null;
        //    }

        //    return item;
        //}

        private static WordItem ParseLine(string rawLine)
        {
            if (rawLine == null)
            {
                return null;
            }

            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#"))
            {
                return null;
            }

            // 支持新标日格式：
            // ちゅうごくじん（中国人） 〔名〕 中国人
            // あなた 〔代〕 你
            WordItem textbookItem;
            if (TryParseTextbookLine(line, out textbookItem))
            {
                return textbookItem;
            }

            // 保留原来的导入格式：单词,读音,意思 / 单词=意思 / 单词：意思 等
            List<string> parts = SplitColumns(line);
            if (parts.Count < 2)
            {
                return null;
            }

            WordItem item = new WordItem();
            item.Japanese = parts[0];
            if (parts.Count >= 3)
            {
                item.Pinyin = parts[1];
                item.Chinese = JoinMeaning(parts, 2);
            }
            else
            {
                item.Pinyin = string.Empty;
                item.Chinese = parts[1];
            }

            return NormalizeImportedItem(item);
        }

        private static bool TryParseTextbookLine(string line, out WordItem item)
        {
            item = null;

            int partStart = line.IndexOf('〔');
            int partEnd = line.IndexOf('〕');
            if (partStart <= 0 || partEnd <= partStart)
            {
                return false;
            }

            string head = Storage.SafeTrim(line.Substring(0, partStart));
            string partOfSpeech = Storage.SafeTrim(line.Substring(partStart + 1, partEnd - partStart - 1));
            string meaning = Storage.SafeTrim(line.Substring(partEnd + 1));
            if (head.Length == 0 || meaning.Length == 0)
            {
                return false;
            }

            string reading = string.Empty;
            string displayWord = head;

            int bracketStart = head.IndexOf('（');
            int bracketEnd = head.LastIndexOf('）');
            if (bracketStart > 0 && bracketEnd > bracketStart)
            {
                reading = Storage.SafeTrim(head.Substring(0, bracketStart));
                string written = Storage.SafeTrim(head.Substring(bracketStart + 1, bracketEnd - bracketStart - 1));

                // 普通词：ちゅうごくじん（中国人） -> 大字显示：中国人，读音：ちゅうごくじん
                // 特殊词：アメリカじん（～人） -> 避免只显示“～人”
                if (written.Length > 0 && written.IndexOf('～') < 0 && written.IndexOf('~') < 0)
                {
                    displayWord = written;
                }
                else
                {
                    displayWord = head;
                }
            }

            item = new WordItem();
            item.Japanese = displayWord;
            item.Pinyin = reading;
            item.Chinese = "〔" + partOfSpeech + "〕 " + meaning;
            item = NormalizeImportedItem(item);
            return item != null;
        }

        private static WordItem NormalizeImportedItem(WordItem item)
        {
            if (item == null)
            {
                return null;
            }

            item.Japanese = Storage.SafeTrim(item.Japanese);
            item.Pinyin = Storage.SafeTrim(item.Pinyin);
            item.Chinese = Storage.SafeTrim(item.Chinese);
            item.Weight = 1;
            item.Marked = false;

            if (item.Japanese.Length == 0 || item.Chinese.Length == 0)
            {
                return null;
            }

            return item;
        }

        private static List<string> SplitColumns(string line)
        {
            int i;
            for (i = 0; i < SimpleSeparators.Length; i++)
            {
                if (line.IndexOf(SimpleSeparators[i]) >= 0)
                {
                    return CleanParts(line.Split(SimpleSeparators[i]));
                }
            }

            for (i = 0; i < WideSeparators.Length; i++)
            {
                int index = line.IndexOf(WideSeparators[i], StringComparison.Ordinal);
                if (index > 0)
                {
                    string left = line.Substring(0, index);
                    string right = line.Substring(index + WideSeparators[i].Length);
                    return CleanParts(new string[] { left, right });
                }
            }

            return new List<string>();
        }

        private static List<string> CleanParts(string[] rawParts)
        {
            List<string> parts = new List<string>();
            int i;
            for (i = 0; i < rawParts.Length; i++)
            {
                string value = Storage.SafeTrim(rawParts[i]);
                if (value.Length > 0)
                {
                    parts.Add(value);
                }
            }
            return parts;
        }

        private static string JoinMeaning(List<string> parts, int startIndex)
        {
            StringBuilder builder = new StringBuilder();
            int i;
            for (i = startIndex; i < parts.Count; i++)
            {
                if (builder.Length > 0)
                {
                    builder.Append("，");
                }
                builder.Append(parts[i]);
            }
            return builder.ToString();
        }

        private static string NormalizeKey(string value)
        {
            return Storage.SafeTrim(value).ToLowerInvariant();
        }

        private static Encoding DetectEncoding(string path)
        {
            byte[] bom = new byte[4];
            using (FileStream stream = File.OpenRead(path))
            {
                stream.Read(bom, 0, 4);
            }

            if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
            {
                return new UTF8Encoding(true);
            }
            if (bom[0] == 0xFF && bom[1] == 0xFE)
            {
                return Encoding.Unicode;
            }
            if (bom[0] == 0xFE && bom[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode;
            }

            return Encoding.UTF8;
        }
    }
}
