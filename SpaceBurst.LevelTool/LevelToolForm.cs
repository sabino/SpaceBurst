using SpaceBurst.RuntimeData;
using System.Drawing.Drawing2D;
using System.Numerics;
using System.Text.RegularExpressions;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace SpaceBurst.LevelTool
{
    public sealed class LevelToolForm : Form
    {
        private readonly string repoRoot;
        private readonly string levelsDirectory;
        private readonly Dictionary<string, EnemyArchetypeDefinition> archetypes;

        private readonly ComboBox stageComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 72 };
        private readonly TrackBar timelineTrackBar = new() { Dock = DockStyle.Fill, TickStyle = TickStyle.None, Maximum = 600 };
        private readonly Label timelineLabel = new() { AutoSize = true, Padding = new Padding(0, 8, 0, 0) };
        private readonly PropertyGrid stageGrid = new() { Dock = DockStyle.Fill };
        private readonly PropertyGrid sectionGrid = new() { Dock = DockStyle.Fill };
        private readonly PropertyGrid groupGrid = new() { Dock = DockStyle.Fill };
        private readonly PropertyGrid bossGrid = new() { Dock = DockStyle.Fill };
        private readonly PropertyGrid moodGrid = new() { Dock = DockStyle.Fill };
        private readonly ListBox sectionsList = new() { Dock = DockStyle.Fill };
        private readonly ListBox groupsList = new() { Dock = DockStyle.Fill };
        private readonly TextBox validationBox = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
        private readonly PreviewPanel previewPanel = new() { Dock = DockStyle.Fill };
        private readonly WinFormsTimer playbackTimer = new() { Interval = 100 };

        private StageDefinition currentStage;
        private string currentFilePath = string.Empty;

        public LevelToolForm()
        {
            repoRoot = FindRepositoryRoot();
            levelsDirectory = Path.Combine(repoRoot, "Levels");
            archetypes = LevelSerializer.LoadArchetypesFromFile(Path.Combine(levelsDirectory, "enemy-archetypes.json"))
                .Archetypes
                .ToDictionary(x => x.Id, x => x);

            Text = "SpaceBurst Level Tool";
            StartPosition = FormStartPosition.CenterScreen;
            Width = 1480;
            Height = 920;
            MinimumSize = new Size(1320, 840);

            BuildUi();
            WireEvents();

            for (int i = 1; i <= 50; i++)
                stageComboBox.Items.Add(i.ToString("00"));
            stageComboBox.SelectedIndex = 0;

            previewPanel.Archetypes = archetypes;
            LoadStage(GetStageFilePath(1));
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Controls.Add(root);

            var toolbar = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 9, Padding = new Padding(8) };
            for (int i = 0; i < 7; i++)
                toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            root.Controls.Add(toolbar, 0, 0);

            var loadButton = new Button { Text = "Load" };
            var openButton = new Button { Text = "Open..." };
            var saveButton = new Button { Text = "Save" };
            var validateButton = new Button { Text = "Validate" };
            var playButton = new Button { Text = "Play" };
            playButton.Name = "PlayButton";

            toolbar.Controls.Add(new Label { Text = "Stage", AutoSize = true, Padding = new Padding(0, 8, 4, 0) }, 0, 0);
            toolbar.Controls.Add(stageComboBox, 1, 0);
            toolbar.Controls.Add(loadButton, 2, 0);
            toolbar.Controls.Add(openButton, 3, 0);
            toolbar.Controls.Add(saveButton, 4, 0);
            toolbar.Controls.Add(validateButton, 5, 0);
            toolbar.Controls.Add(playButton, 6, 0);
            toolbar.Controls.Add(timelineTrackBar, 7, 0);
            toolbar.Controls.Add(timelineLabel, 8, 0);

            var mainSplit = new SplitContainer { Dock = DockStyle.Fill };
            root.Controls.Add(mainSplit, 0, 1);

            var leftSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
            mainSplit.Panel1.Controls.Add(leftSplit);

            var editors = new TabControl { Dock = DockStyle.Fill };
            editors.TabPages.Add(new TabPage("Stage") { Controls = { stageGrid } });
            editors.TabPages.Add(new TabPage("Section") { Controls = { sectionGrid } });
            leftSplit.Panel1.Controls.Add(editors);

            var listsSplit = new SplitContainer { Dock = DockStyle.Fill };
            leftSplit.Panel2.Controls.Add(listsSplit);

            listsSplit.Panel1.Controls.Add(BuildListPane("Sections", sectionsList, "Add Section", "Remove Section"));
            listsSplit.Panel2.Controls.Add(BuildListPane("Groups", groupsList, "Add Group", "Remove Group"));

            var rightTabs = new TabControl { Dock = DockStyle.Fill };
            rightTabs.TabPages.Add(new TabPage("Preview") { Controls = { previewPanel } });
            rightTabs.TabPages.Add(new TabPage("Group") { Controls = { groupGrid } });
            rightTabs.TabPages.Add(new TabPage("Boss") { Controls = { bossGrid } });
            rightTabs.TabPages.Add(new TabPage("Mood") { Controls = { moodGrid } });
            rightTabs.TabPages.Add(new TabPage("Validation") { Controls = { validationBox } });
            mainSplit.Panel2.Controls.Add(rightTabs);

            loadButton.Click += (_, __) => LoadStage(GetStageFilePath(GetSelectedStageNumber()));
            openButton.Click += (_, __) => OpenStageFromDialog();
            saveButton.Click += (_, __) => SaveStage();
            validateButton.Click += (_, __) => RefreshValidation();
            playButton.Click += (_, __) =>
            {
                if (playbackTimer.Enabled)
                {
                    playbackTimer.Stop();
                    playButton.Text = "Play";
                }
                else
                {
                    playbackTimer.Start();
                    playButton.Text = "Pause";
                }
            };
        }

        private Control BuildListPane(string title, ListBox listBox, string addLabel, string removeLabel)
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(8) };
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.Controls.Add(new Label { Text = title, AutoSize = true, Padding = new Padding(0, 0, 0, 4) }, 0, 0);
            panel.Controls.Add(listBox, 0, 1);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
            buttons.Controls.Add(new Button { Text = addLabel, Name = addLabel });
            buttons.Controls.Add(new Button { Text = removeLabel, Name = removeLabel });
            panel.Controls.Add(buttons, 0, 2);
            return panel;
        }

        private void WireEvents()
        {
            timelineTrackBar.Scroll += (_, __) => RefreshTimelineAndPreview();
            playbackTimer.Tick += (_, __) =>
            {
                timelineTrackBar.Value = timelineTrackBar.Value >= timelineTrackBar.Maximum ? 0 : timelineTrackBar.Value + 1;
                RefreshTimelineAndPreview();
            };

            stageGrid.PropertyValueChanged += (_, __) => AfterEdit();
            sectionGrid.PropertyValueChanged += (_, __) => AfterEdit();
            groupGrid.PropertyValueChanged += (_, __) => AfterEdit();
            bossGrid.PropertyValueChanged += (_, __) => AfterEdit();

            sectionsList.SelectedIndexChanged += (_, __) =>
            {
                sectionGrid.SelectedObject = SelectedSection();
                moodGrid.SelectedObject = SelectedSection()?.Mood ?? currentStage?.BackgroundMood;
                RefreshGroupList();
                previewPanel.SelectedSectionIndex = sectionsList.SelectedIndex;
                RefreshPreview();
            };

            groupsList.SelectedIndexChanged += (_, __) =>
            {
                SectionDefinition section = SelectedSection();
                groupGrid.SelectedObject = section != null && groupsList.SelectedIndex >= 0 && groupsList.SelectedIndex < section.Groups.Count ? section.Groups[groupsList.SelectedIndex] : null;
                RefreshPreview();
            };

            BindNamedButton(this, "Add Section", (_, __) => AddSection());
            BindNamedButton(this, "Remove Section", (_, __) => RemoveSection());
            BindNamedButton(this, "Add Group", (_, __) => AddGroup());
            BindNamedButton(this, "Remove Group", (_, __) => RemoveGroup());
        }

        private static void BindNamedButton(Control root, string name, EventHandler handler)
        {
            foreach (Control child in root.Controls)
            {
                if (child is Button button && button.Name == name)
                    button.Click += handler;

                BindNamedButton(child, name, handler);
            }
        }

        private void LoadStage(string path)
        {
            currentStage = LevelSerializer.LoadLevelFromFile(path);
            currentFilePath = path;
            stageGrid.SelectedObject = currentStage;
            bossGrid.SelectedObject = currentStage.Boss;
            moodGrid.SelectedObject = currentStage.BackgroundMood;
            UpdateStageSelectionFromPath(path);
            RefreshSectionList();
            sectionsList.SelectedIndex = currentStage.Sections.Count > 0 ? 0 : -1;
            RefreshTimelineMaximum();
            RefreshValidation();
            RefreshPreview();
            Text = $"SpaceBurst Level Tool - {Path.GetFileName(path)}";
        }

        private void SaveStage()
        {
            if (currentStage == null)
                return;

            SortSections();
            LevelSerializer.SaveLevelToFile(string.IsNullOrEmpty(currentFilePath) ? GetStageFilePath(currentStage.StageNumber) : currentFilePath, currentStage);
            RefreshSectionList();
            RefreshValidation();
        }

        private void OpenStageFromDialog()
        {
            using var dialog = new OpenFileDialog { InitialDirectory = levelsDirectory, Filter = "Stage JSON (*.json)|*.json", Title = "Open Stage JSON" };
            if (dialog.ShowDialog(this) == DialogResult.OK)
                LoadStage(dialog.FileName);
        }

        private void AddSection()
        {
            if (currentStage == null)
                return;

            float start = currentStage.Sections.Count == 0 ? 0.75f : currentStage.Sections.Max(x => x.StartSeconds) + 13f;
            currentStage.Sections.Add(new SectionDefinition
            {
                Label = $"Section {currentStage.Sections.Count + 1}",
                StartSeconds = start,
                DurationSeconds = 11.8f,
                Groups = new List<SpawnGroupDefinition> { DefaultGroup() },
            });
            AfterEdit();
            sectionsList.SelectedIndex = currentStage.Sections.Count - 1;
        }

        private void RemoveSection()
        {
            if (currentStage == null || sectionsList.SelectedIndex < 0 || currentStage.Sections.Count <= 1)
                return;

            int index = sectionsList.SelectedIndex;
            currentStage.Sections.RemoveAt(index);
            AfterEdit();
            sectionsList.SelectedIndex = Math.Min(index, sectionsList.Items.Count - 1);
        }

        private void AddGroup()
        {
            SectionDefinition section = SelectedSection();
            if (section == null)
                return;

            section.Groups.Add(DefaultGroup());
            AfterEdit();
            groupsList.SelectedIndex = section.Groups.Count - 1;
        }

        private void RemoveGroup()
        {
            SectionDefinition section = SelectedSection();
            if (section == null || groupsList.SelectedIndex < 0 || section.Groups.Count <= 1)
                return;

            int index = groupsList.SelectedIndex;
            section.Groups.RemoveAt(index);
            AfterEdit();
            groupsList.SelectedIndex = Math.Min(index, groupsList.Items.Count - 1);
        }

        private void AfterEdit()
        {
            SortSections();
            RefreshSectionList();
            RefreshGroupList();
            RefreshTimelineMaximum();
            RefreshValidation();
            RefreshPreview();
        }

        private void RefreshSectionList()
        {
            int selected = sectionsList.SelectedIndex;
            sectionsList.BeginUpdate();
            sectionsList.Items.Clear();
            if (currentStage != null)
            {
                foreach (SectionDefinition section in currentStage.Sections)
                    sectionsList.Items.Add($"{section.StartSeconds:0.0}s  {section.Label}{(section.Checkpoint ? " [CP]" : string.Empty)}");
            }
            sectionsList.EndUpdate();
            if (sectionsList.Items.Count > 0)
                sectionsList.SelectedIndex = Math.Clamp(selected, 0, sectionsList.Items.Count - 1);
        }

        private void RefreshGroupList()
        {
            int selected = groupsList.SelectedIndex;
            groupsList.BeginUpdate();
            groupsList.Items.Clear();
            SectionDefinition section = SelectedSection();
            if (section != null)
            {
                foreach (SpawnGroupDefinition group in section.Groups)
                    groupsList.Items.Add($"{group.ArchetypeId} x{group.Count} {(group.TargetY.HasValue ? group.TargetY.Value.ToString("0.00") : $"Lane {group.Lane}")}");
            }
            groupsList.EndUpdate();
            if (groupsList.Items.Count > 0)
                groupsList.SelectedIndex = Math.Clamp(selected, 0, groupsList.Items.Count - 1);
        }

        private void RefreshValidation()
        {
            if (currentStage == null)
                return;

            List<ValidationIssue> issues = LevelValidator.ValidateStage(currentStage, archetypes);
            validationBox.Text = issues.Count == 0 ? "No validation issues." : string.Join(Environment.NewLine, issues.Select(x => x.ToString()));
        }

        private void RefreshTimelineMaximum()
        {
            float duration = 12f;
            if (currentStage != null && currentStage.Sections.Count > 0)
            {
                duration = currentStage.Sections.Max(section =>
                {
                    float tail = section.Groups.Count == 0 ? section.DurationSeconds : section.Groups.Max(group => group.StartSeconds + LevelMath.EstimateGroupLifetimeSeconds(group, archetypes[group.ArchetypeId], 1280f));
                    return section.StartSeconds + Math.Max(section.DurationSeconds, tail);
                }) + 4f;

                if (currentStage.Boss != null)
                    duration += currentStage.Boss.IntroSeconds + 12f;
            }

            timelineTrackBar.Maximum = Math.Max(80, (int)Math.Ceiling(duration * 10f));
            if (timelineTrackBar.Value > timelineTrackBar.Maximum)
                timelineTrackBar.Value = timelineTrackBar.Maximum;
            RefreshTimelineAndPreview();
        }

        private void RefreshTimelineAndPreview()
        {
            timelineLabel.Text = $"Time {timelineTrackBar.Value / 10f:0.0}s";
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            previewPanel.Stage = currentStage;
            previewPanel.CurrentTimeSeconds = timelineTrackBar.Value / 10f;
            previewPanel.Invalidate();
        }

        private void SortSections()
        {
            if (currentStage == null)
                return;

            currentStage.Sections = currentStage.Sections.OrderBy(x => x.StartSeconds).ThenBy(x => x.Label ?? string.Empty).ToList();
            currentStage.CheckpointMarkers = currentStage.Sections.Where(x => x.Checkpoint).Select(x => x.StartSeconds).ToList();
        }

        private SectionDefinition SelectedSection()
        {
            return currentStage == null || sectionsList.SelectedIndex < 0 || sectionsList.SelectedIndex >= currentStage.Sections.Count
                ? null
                : currentStage.Sections[sectionsList.SelectedIndex];
        }

        private static SpawnGroupDefinition DefaultGroup()
        {
            return new SpawnGroupDefinition
            {
                ArchetypeId = "Walker",
                StartSeconds = 0f,
                Lane = 2,
                Count = 3,
                SpawnLeadDistance = 240f,
                SpawnIntervalSeconds = 0.22f,
                SpacingX = 84f,
                SpeedMultiplier = 1f,
                MovePatternOverride = MovePattern.StraightFlyIn,
                FirePatternOverride = FirePattern.None,
                Amplitude = 30f,
                Frequency = 1f,
            };
        }

        private int GetSelectedStageNumber() => stageComboBox.SelectedItem == null ? 1 : int.Parse(stageComboBox.SelectedItem.ToString() ?? "1");

        private string GetStageFilePath(int stageNumber) => Path.Combine(levelsDirectory, $"level-{stageNumber:00}.json");

        private void UpdateStageSelectionFromPath(string path)
        {
            Match match = Regex.Match(Path.GetFileName(path), @"level-(\d+)\.json", RegexOptions.IgnoreCase);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out int stageNumber))
                return;

            string value = stageNumber.ToString("00");
            int index = stageComboBox.Items.IndexOf(value);
            if (index >= 0)
                stageComboBox.SelectedIndex = index;
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo directory = new(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "SpaceBurst.sln")) && Directory.Exists(Path.Combine(directory.FullName, "Levels")))
                    return directory.FullName;
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("Could not locate the SpaceBurst repository root.");
        }

        private sealed class PreviewPanel : Panel
        {
            private static readonly Vector2 ArenaSize = new(1280f, 720f);
            public IDictionary<string, EnemyArchetypeDefinition> Archetypes { get; set; }
            public StageDefinition Stage { get; set; }
            public int SelectedSectionIndex { get; set; }
            public float CurrentTimeSeconds { get; set; }

            public PreviewPanel()
            {
                DoubleBuffered = true;
                BackColor = Color.FromArgb(18, 18, 26);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.Clear(Color.FromArgb(10, 12, 22));

                RectangleF arena = GetArenaBounds();
                BackgroundMoodDefinition mood = Stage?.BackgroundMood ?? new BackgroundMoodDefinition();
                using var arenaBrush = new SolidBrush(ParseColor(mood.PrimaryColor));
                using var border = new Pen(Color.FromArgb(74, 100, 126), 2f);
                using var lanePen = new Pen(Color.FromArgb(36, 54, 74), 1f);
                using var playerWindow = new SolidBrush(Color.FromArgb(38, 90, 132, 180));
                using var accentBrush = new SolidBrush(Color.FromArgb(42, ParseColor(mood.AccentColor)));

                e.Graphics.FillRectangle(arenaBrush, arena);
                e.Graphics.FillRectangle(accentBrush, new RectangleF(arena.Left, arena.Top, arena.Width, arena.Height * 0.12f));
                e.Graphics.FillRectangle(playerWindow, new RectangleF(arena.Left + arena.Width * 0.06f, arena.Top + arena.Height * 0.06f, arena.Width * 0.88f, arena.Height * 0.88f));
                for (int lane = 0; lane < 5; lane++)
                {
                    float y = LevelMath.GetLanePosition(ArenaSize, lane);
                    e.Graphics.DrawLine(lanePen, ToScreen(arena, new Vector2(0f, y)), ToScreen(arena, new Vector2(ArenaSize.X, y)));
                }
                e.Graphics.DrawRectangle(border, Rectangle.Round(arena));
                DrawSprite(e.Graphics, arena, new ProceduralSpriteDefinition
                {
                    PrimaryColor = "#D7F5FF",
                    SecondaryColor = "#5AAFCB",
                    AccentColor = "#FFB347",
                    Rows = new List<string>
                    {
                        "....##......",
                        "..######....",
                        ".##++++##...",
                        "##++CC++##..",
                        "##++++++##..",
                        ".##+**+##...",
                        "..######....",
                        "....##......",
                    }
                }, new Vector2(ArenaSize.X * 0.18f, ArenaSize.Y * 0.5f), 4f);

                if (Stage != null)
                {
                    DrawEventWindows(e.Graphics, arena);
                    for (int i = 0; i < Stage.Sections.Count; i++)
                    {
                        SectionDefinition section = Stage.Sections[i];
                        foreach (SpawnGroupDefinition group in section.Groups)
                            DrawGroup(e.Graphics, arena, section, group, i == SelectedSectionIndex ? 1f : 0.45f);
                    }

                    if (Stage.Boss != null && Archetypes != null && Archetypes.TryGetValue(Stage.Boss.ArchetypeId, out EnemyArchetypeDefinition bossArch))
                    {
                        float start = Stage.Sections.Max(x => x.StartSeconds + x.DurationSeconds) + Stage.Boss.IntroSeconds;
                        if (CurrentTimeSeconds >= start)
                        {
                            Vector2 spawn = new(ArenaSize.X + bossArch.SpawnLeadDistance, Stage.Boss.TargetY * ArenaSize.Y);
                            Vector2 bossPos = LevelMath.SamplePreviewPosition(Stage.Boss.MovePattern, spawn, CurrentTimeSeconds - start, bossArch.MoveSpeed, Stage.Boss.ArenaScrollSpeed, Stage.Boss.TargetY * ArenaSize.Y, bossArch.MovementAmplitude, bossArch.MovementFrequency);
                            DrawSprite(e.Graphics, arena, bossArch.Sprite, bossPos, 4f * bossArch.RenderScale);
                        }
                    }
                }

                using var textBrush = new SolidBrush(Color.FromArgb(228, 235, 242));
                string footer = Stage == null
                    ? "No stage loaded"
                    : $"{Stage.Name}    Scroll {Stage.ScrollSpeed:0}    Lives {Stage.StartingLives}    Ships {Stage.ShipsPerLife}    Time {CurrentTimeSeconds:0.0}s";
                e.Graphics.DrawString(footer, Font, textBrush, new PointF(arena.Left, arena.Bottom + 8f));
            }

            private void DrawEventWindows(Graphics graphics, RectangleF arena)
            {
                if (Stage == null || Stage.Sections.Count == 0)
                    return;

                float totalDuration = Math.Max(1f, Stage.Sections.Max(x => x.StartSeconds + x.DurationSeconds));
                foreach (SectionDefinition section in Stage.Sections)
                {
                    if (section.EventWindows == null)
                        continue;

                    foreach (RandomEventWindowDefinition window in section.EventWindows)
                    {
                        float start = (section.StartSeconds + window.StartSeconds) / totalDuration;
                        float width = Math.Max(0.01f, window.DurationSeconds / totalDuration);
                        RectangleF bar = new RectangleF(
                            arena.Left + arena.Width * start,
                            arena.Bottom - 14f,
                            arena.Width * width,
                            10f);
                        using var brush = new SolidBrush(Color.FromArgb(180, ParseColor(section.Mood?.AccentColor ?? Stage.BackgroundMood.AccentColor)));
                        graphics.FillRectangle(brush, bar);
                    }
                }
            }

            private void DrawGroup(Graphics graphics, RectangleF arena, SectionDefinition section, SpawnGroupDefinition group, float emphasis)
            {
                if (Archetypes == null || !Archetypes.TryGetValue(group.ArchetypeId, out EnemyArchetypeDefinition arch))
                    return;

                float groupStart = section.StartSeconds + group.StartSeconds;
                float duration = LevelMath.EstimateGroupLifetimeSeconds(group, arch, ArenaSize.X);
                float elapsed = CurrentTimeSeconds - groupStart;
                using var pathPen = new Pen(Color.FromArgb((int)(110 * emphasis), 120, 186, 222), emphasis < 0.8f ? 1.2f : 2f);

                for (int i = 0; i < group.Count; i++)
                {
                    Vector2 spawn = LevelMath.GetSpawnPoint(ArenaSize, group, i);
                    PointF? last = null;
                    for (int sample = 0; sample <= 18; sample++)
                    {
                        float t = duration * sample / 18f;
                        Vector2 pos = LevelMath.SamplePreviewPosition(group.MovePatternOverride ?? arch.MovePattern, spawn, t, arch.MoveSpeed * group.SpeedMultiplier, Stage?.ScrollSpeed ?? 0f, LevelMath.ResolveTargetY(ArenaSize, group), group.Amplitude > 0f ? group.Amplitude : arch.MovementAmplitude, group.Frequency > 0f ? group.Frequency : arch.MovementFrequency);
                        PointF screen = ToScreen(arena, pos);
                        if (last.HasValue)
                            graphics.DrawLine(pathPen, last.Value, screen);
                        last = screen;
                    }

                    if (elapsed < i * group.SpawnIntervalSeconds || elapsed > duration + i * group.SpawnIntervalSeconds)
                        continue;

                    Vector2 current = LevelMath.SamplePreviewPosition(group.MovePatternOverride ?? arch.MovePattern, spawn, Math.Max(0f, elapsed - i * group.SpawnIntervalSeconds), arch.MoveSpeed * group.SpeedMultiplier, Stage?.ScrollSpeed ?? 0f, LevelMath.ResolveTargetY(ArenaSize, group), group.Amplitude > 0f ? group.Amplitude : arch.MovementAmplitude, group.Frequency > 0f ? group.Frequency : arch.MovementFrequency);
                    DrawSprite(graphics, arena, arch.Sprite, current, 3.5f * arch.RenderScale);
                }
            }

            private void DrawSprite(Graphics graphics, RectangleF arena, ProceduralSpriteDefinition sprite, Vector2 center, float pixelScale)
            {
                if (sprite.Rows == null || sprite.Rows.Count == 0)
                    return;

                int width = sprite.Rows.Max(x => x.Length);
                int height = sprite.Rows.Count;
                float startX = center.X - width * pixelScale / 2f;
                float startY = center.Y - height * pixelScale / 2f;
                Color primary = ParseColor(sprite.PrimaryColor);
                Color secondary = ParseColor(sprite.SecondaryColor);
                Color accent = ParseColor(sprite.AccentColor);

                for (int y = 0; y < sprite.Rows.Count; y++)
                {
                    string row = sprite.Rows[y];
                    for (int x = 0; x < row.Length; x++)
                    {
                        char glyph = row[x];
                        if (glyph == '.' || char.IsWhiteSpace(glyph))
                            continue;

                        Color color = glyph is '+' or 'o' ? secondary : glyph is '*' or 'x' or '@' ? accent : primary;
                        RectangleF cell = RectangleF.FromLTRB(startX + x * pixelScale, startY + y * pixelScale, startX + (x + 1) * pixelScale, startY + (y + 1) * pixelScale);
                        using var brush = new SolidBrush(color);
                        graphics.FillRectangle(brush, ToScreen(arena, cell));
                    }
                }
            }

            private RectangleF GetArenaBounds()
            {
                float margin = 24f;
                float scale = Math.Min((ClientSize.Width - margin * 2f) / ArenaSize.X, (ClientSize.Height - margin * 2f - 30f) / ArenaSize.Y);
                SizeF size = new(ArenaSize.X * scale, ArenaSize.Y * scale);
                return new RectangleF((ClientSize.Width - size.Width) / 2f, 16f, size.Width, size.Height);
            }

            private static PointF ToScreen(RectangleF arena, Vector2 value) => new(arena.Left + value.X / ArenaSize.X * arena.Width, arena.Top + value.Y / ArenaSize.Y * arena.Height);

            private static RectangleF ToScreen(RectangleF arena, RectangleF value)
            {
                PointF tl = ToScreen(arena, new Vector2(value.Left, value.Top));
                PointF br = ToScreen(arena, new Vector2(value.Right, value.Bottom));
                return RectangleF.FromLTRB(tl.X, tl.Y, br.X, br.Y);
            }

            private static Color ParseColor(string hex)
            {
                try { return ColorTranslator.FromHtml(hex); } catch { return Color.White; }
            }
        }
    }
}
