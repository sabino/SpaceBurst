using System.Drawing.Drawing2D;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using SpaceBurst.RuntimeData;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace SpaceBurst.LevelTool
{
    public sealed partial class LevelToolForm : Form
    {
        private readonly string repoRoot;
        private readonly string levelsDirectory;
        private readonly Dictionary<string, EnemyArchetypeDefinition> archetypes;

        private readonly ComboBox stageComboBox = new ComboBox();
        private readonly Button loadStageButton = new Button();
        private readonly Button openButton = new Button();
        private readonly Button saveButton = new Button();
        private readonly Button validateButton = new Button();
        private readonly Button playPauseButton = new Button();
        private readonly TrackBar timelineTrackBar = new TrackBar();
        private readonly Label timelineLabel = new Label();
        private readonly TextBox levelNameTextBox = new TextBox();
        private readonly NumericUpDown introSecondsUpDown = new NumericUpDown();
        private readonly ListBox wavesListBox = new ListBox();
        private readonly Button addWaveButton = new Button();
        private readonly Button removeWaveButton = new Button();
        private readonly TextBox waveLabelTextBox = new TextBox();
        private readonly NumericUpDown waveStartUpDown = new NumericUpDown();
        private readonly CheckBox checkpointCheckBox = new CheckBox();
        private readonly ListBox groupsListBox = new ListBox();
        private readonly Button addGroupButton = new Button();
        private readonly Button removeGroupButton = new Button();
        private readonly PropertyGrid groupPropertyGrid = new PropertyGrid();
        private readonly PropertyGrid bossPropertyGrid = new PropertyGrid();
        private readonly TextBox validationTextBox = new TextBox();
        private readonly LevelPreviewPanel previewPanel = new LevelPreviewPanel();
        private readonly WinFormsTimer playbackTimer = new WinFormsTimer();

        private LevelDefinition currentLevel;
        private string currentFilePath;
        private bool suppressUiEvents;

        public LevelToolForm()
        {
            repoRoot = FindRepositoryRoot();
            levelsDirectory = Path.Combine(repoRoot, "Levels");
            archetypes = LevelSerializer.LoadArchetypesFromFile(Path.Combine(levelsDirectory, "enemy-archetypes.json"))
                .Archetypes
                .ToDictionary(x => x.Id, x => x);

            Text = "SpaceBurst Level Tool";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1280, 820);
            Width = 1440;
            Height = 900;

            InitializeLayout();
            InitializeEvents();

            for (int stage = 1; stage <= 50; stage++)
                stageComboBox.Items.Add(stage.ToString("00"));
            stageComboBox.SelectedIndex = 0;

            previewPanel.Archetypes = archetypes;
            playbackTimer.Interval = 100;
            playbackTimer.Tick += PlaybackTimerTick;

            LoadLevel(GetStageFilePath(1));
        }

        private void InitializeLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Controls.Add(root);

            var toolbar = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 9,
                Padding = new Padding(8),
            };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            root.Controls.Add(toolbar, 0, 0);

            stageComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            stageComboBox.Width = 70;
            loadStageButton.Text = "Load Stage";
            openButton.Text = "Open...";
            saveButton.Text = "Save";
            validateButton.Text = "Validate";
            playPauseButton.Text = "Play";

            timelineTrackBar.Dock = DockStyle.Fill;
            timelineTrackBar.Minimum = 0;
            timelineTrackBar.Maximum = 600;
            timelineTrackBar.TickStyle = TickStyle.None;

            timelineLabel.AutoSize = true;
            timelineLabel.TextAlign = ContentAlignment.MiddleRight;
            timelineLabel.Padding = new Padding(0, 8, 0, 0);

            toolbar.Controls.Add(new Label { Text = "Stage", AutoSize = true, Padding = new Padding(0, 8, 4, 0) }, 0, 0);
            toolbar.Controls.Add(stageComboBox, 1, 0);
            toolbar.Controls.Add(loadStageButton, 2, 0);
            toolbar.Controls.Add(openButton, 3, 0);
            toolbar.Controls.Add(saveButton, 4, 0);
            toolbar.Controls.Add(validateButton, 5, 0);
            toolbar.Controls.Add(playPauseButton, 6, 0);
            toolbar.Controls.Add(timelineTrackBar, 7, 0);
            toolbar.Controls.Add(timelineLabel, 8, 0);

            var mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Panel1MinSize = 100,
                Panel2MinSize = 100,
            };
            root.Controls.Add(mainSplit, 0, 1);

            var editorLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,
                Padding = new Padding(8),
            };
            editorLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45f));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 55f));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainSplit.Panel1.Controls.Add(editorLayout);

            var levelFields = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
            };
            levelFields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            levelFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            introSecondsUpDown.DecimalPlaces = 2;
            introSecondsUpDown.Increment = 0.05m;
            introSecondsUpDown.Maximum = 10m;
            introSecondsUpDown.Minimum = 0m;

            levelFields.Controls.Add(new Label { Text = "Level Name", AutoSize = true, Padding = new Padding(0, 7, 8, 0) }, 0, 0);
            levelFields.Controls.Add(levelNameTextBox, 1, 0);
            levelFields.Controls.Add(new Label { Text = "Intro Seconds", AutoSize = true, Padding = new Padding(0, 7, 8, 0) }, 0, 1);
            levelFields.Controls.Add(introSecondsUpDown, 1, 1);
            editorLayout.Controls.Add(levelFields, 0, 0);

            editorLayout.Controls.Add(new Label { Text = "Waves", AutoSize = true, Padding = new Padding(0, 4, 0, 4) }, 0, 1);

            wavesListBox.Dock = DockStyle.Fill;
            editorLayout.Controls.Add(wavesListBox, 0, 2);

            var waveButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
            };
            addWaveButton.Text = "Add Wave";
            removeWaveButton.Text = "Remove Wave";
            waveButtons.Controls.Add(addWaveButton);
            waveButtons.Controls.Add(removeWaveButton);
            editorLayout.Controls.Add(waveButtons, 0, 3);

            var waveFields = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
            };
            waveFields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            waveFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            waveStartUpDown.DecimalPlaces = 2;
            waveStartUpDown.Increment = 0.1m;
            waveStartUpDown.Maximum = 300m;
            waveStartUpDown.Minimum = 0m;

            checkpointCheckBox.Text = "Checkpoint";
            checkpointCheckBox.AutoSize = true;

            waveFields.Controls.Add(new Label { Text = "Wave Label", AutoSize = true, Padding = new Padding(0, 7, 8, 0) }, 0, 0);
            waveFields.Controls.Add(waveLabelTextBox, 1, 0);
            waveFields.Controls.Add(new Label { Text = "Start Seconds", AutoSize = true, Padding = new Padding(0, 7, 8, 0) }, 0, 1);
            waveFields.Controls.Add(waveStartUpDown, 1, 1);
            waveFields.Controls.Add(checkpointCheckBox, 1, 2);
            editorLayout.Controls.Add(waveFields, 0, 4);

            editorLayout.Controls.Add(new Label { Text = "Spawn Groups", AutoSize = true, Padding = new Padding(0, 4, 0, 4) }, 0, 5);

            var groupsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
            };
            groupsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            groupsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            groupsListBox.Dock = DockStyle.Fill;
            groupsLayout.Controls.Add(groupsListBox, 0, 0);

            var groupButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
            };
            addGroupButton.Text = "Add Group";
            removeGroupButton.Text = "Remove Group";
            groupButtons.Controls.Add(addGroupButton);
            groupButtons.Controls.Add(removeGroupButton);
            groupsLayout.Controls.Add(groupButtons, 0, 1);
            editorLayout.Controls.Add(groupsLayout, 0, 6);

            var rightSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                Panel1MinSize = 100,
                Panel2MinSize = 100,
            };
            mainSplit.Panel2.Controls.Add(rightSplit);

            previewPanel.Dock = DockStyle.Fill;
            rightSplit.Panel1.Controls.Add(previewPanel);

            var bottomTabs = new TabControl
            {
                Dock = DockStyle.Fill,
            };
            rightSplit.Panel2.Controls.Add(bottomTabs);

            groupPropertyGrid.Dock = DockStyle.Fill;
            bossPropertyGrid.Dock = DockStyle.Fill;

            validationTextBox.Dock = DockStyle.Fill;
            validationTextBox.Multiline = true;
            validationTextBox.ReadOnly = true;
            validationTextBox.ScrollBars = ScrollBars.Vertical;
            validationTextBox.Font = new Font(FontFamily.GenericMonospace, 9f);

            bottomTabs.TabPages.Add(new TabPage("Spawn Group") { Controls = { groupPropertyGrid } });
            bottomTabs.TabPages.Add(new TabPage("Boss") { Controls = { bossPropertyGrid } });
            bottomTabs.TabPages.Add(new TabPage("Validation") { Controls = { validationTextBox } });
        }

        private void InitializeEvents()
        {
            loadStageButton.Click += (_, __) => LoadLevel(GetStageFilePath(GetSelectedStageNumber()));
            openButton.Click += (_, __) => OpenLevelFromDialog();
            saveButton.Click += (_, __) => SaveCurrentLevel();
            validateButton.Click += (_, __) => RefreshValidation();

            playPauseButton.Click += (_, __) =>
            {
                if (playbackTimer.Enabled)
                {
                    playbackTimer.Stop();
                    playPauseButton.Text = "Play";
                }
                else
                {
                    playbackTimer.Start();
                    playPauseButton.Text = "Pause";
                }
            };

            timelineTrackBar.Scroll += (_, __) => RefreshTimelineAndPreview();

            levelNameTextBox.TextChanged += (_, __) =>
            {
                if (suppressUiEvents || currentLevel == null)
                    return;

                currentLevel.Name = levelNameTextBox.Text;
                RefreshValidation();
            };

            introSecondsUpDown.ValueChanged += (_, __) =>
            {
                if (suppressUiEvents || currentLevel == null)
                    return;

                currentLevel.IntroSeconds = (float)introSecondsUpDown.Value;
                RefreshValidation();
            };

            wavesListBox.SelectedIndexChanged += (_, __) => PopulateWaveControls();

            addWaveButton.Click += (_, __) =>
            {
                if (currentLevel == null)
                    return;

                float startSeconds = currentLevel.Waves.Count == 0 ? 0.8f : currentLevel.Waves.Max(x => x.StartSeconds) + 8f;
                currentLevel.Waves.Add(new WaveDefinition
                {
                    Label = string.Concat("Wave ", (currentLevel.Waves.Count + 1).ToString()),
                    StartSeconds = startSeconds,
                    Groups = new List<SpawnGroupDefinition> { CreateDefaultGroup() },
                });

                SortWaves();
                RefreshWaveList();
                wavesListBox.SelectedIndex = Math.Max(0, currentLevel.Waves.FindIndex(x => Math.Abs(x.StartSeconds - startSeconds) < 0.001f));
                RefreshValidation();
            };

            removeWaveButton.Click += (_, __) =>
            {
                if (currentLevel == null || wavesListBox.SelectedIndex < 0 || currentLevel.Waves.Count <= 1)
                    return;

                int removedIndex = wavesListBox.SelectedIndex;
                currentLevel.Waves.RemoveAt(removedIndex);
                RefreshWaveList();
                wavesListBox.SelectedIndex = Math.Min(Math.Max(0, removedIndex), wavesListBox.Items.Count - 1);
                RefreshValidation();
            };

            waveLabelTextBox.TextChanged += (_, __) => ApplyWaveEdits();
            waveStartUpDown.ValueChanged += (_, __) => ApplyWaveEdits();
            checkpointCheckBox.CheckedChanged += (_, __) => ApplyWaveEdits();

            groupsListBox.SelectedIndexChanged += (_, __) => PopulateGroupEditors();

            addGroupButton.Click += (_, __) =>
            {
                WaveDefinition wave = GetSelectedWave();
                if (wave == null)
                    return;

                wave.Groups.Add(CreateDefaultGroup());
                RefreshGroupList();
                groupsListBox.SelectedIndex = wave.Groups.Count - 1;
                RefreshValidation();
            };

            removeGroupButton.Click += (_, __) =>
            {
                WaveDefinition wave = GetSelectedWave();
                if (wave == null || groupsListBox.SelectedIndex < 0 || wave.Groups.Count <= 1)
                    return;

                int removedIndex = groupsListBox.SelectedIndex;
                wave.Groups.RemoveAt(removedIndex);
                RefreshGroupList();
                groupsListBox.SelectedIndex = Math.Min(Math.Max(0, removedIndex), groupsListBox.Items.Count - 1);
                RefreshValidation();
            };

            groupPropertyGrid.PropertyValueChanged += (_, __) =>
            {
                RefreshGroupList();
                RefreshPreview();
                RefreshValidation();
            };

            bossPropertyGrid.PropertyValueChanged += (_, __) =>
            {
                UpdateTimelineMaximum();
                RefreshPreview();
                RefreshValidation();
            };
        }

        private void PlaybackTimerTick(object sender, EventArgs e)
        {
            if (timelineTrackBar.Value >= timelineTrackBar.Maximum)
            {
                timelineTrackBar.Value = 0;
                playbackTimer.Stop();
                playPauseButton.Text = "Play";
            }
            else
            {
                timelineTrackBar.Value = Math.Min(timelineTrackBar.Maximum, timelineTrackBar.Value + 1);
            }

            RefreshTimelineAndPreview();
        }

        private void OpenLevelFromDialog()
        {
            using var dialog = new OpenFileDialog
            {
                InitialDirectory = levelsDirectory,
                Filter = "Level JSON (*.json)|*.json",
                Title = "Open Level JSON",
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
                LoadLevel(dialog.FileName);
        }

        private void LoadLevel(string path)
        {
            currentLevel = LevelSerializer.LoadLevelFromFile(path);
            currentFilePath = path;

            suppressUiEvents = true;
            levelNameTextBox.Text = currentLevel.Name;
            introSecondsUpDown.Value = (decimal)currentLevel.IntroSeconds;
            suppressUiEvents = false;

            UpdateStageSelectionFromPath(path);
            RefreshWaveList();
            wavesListBox.SelectedIndex = currentLevel.Waves.Count > 0 ? 0 : -1;
            bossPropertyGrid.SelectedObject = currentLevel.Boss;
            UpdateTimelineMaximum();
            RefreshTimelineAndPreview();
            RefreshValidation();
            Text = string.Concat("SpaceBurst Level Tool - ", Path.GetFileName(path));
        }

        private void SaveCurrentLevel()
        {
            if (currentLevel == null)
                return;

            ApplyWaveEdits();
            SortWaves();
            LevelSerializer.SaveLevelToFile(currentFilePath ?? GetStageFilePath(currentLevel.LevelNumber), currentLevel);
            RefreshWaveList();
            RefreshValidation();
        }

        private void PopulateWaveControls()
        {
            WaveDefinition wave = GetSelectedWave();
            suppressUiEvents = true;
            if (wave == null)
            {
                waveLabelTextBox.Text = string.Empty;
                waveStartUpDown.Value = 0m;
                checkpointCheckBox.Checked = false;
            }
            else
            {
                waveLabelTextBox.Text = wave.Label ?? string.Empty;
                waveStartUpDown.Value = (decimal)wave.StartSeconds;
                checkpointCheckBox.Checked = wave.Checkpoint;
            }
            suppressUiEvents = false;

            RefreshGroupList();
            previewPanel.SelectedWaveIndex = wavesListBox.SelectedIndex;
            RefreshPreview();
        }

        private void ApplyWaveEdits()
        {
            if (suppressUiEvents)
                return;

            WaveDefinition wave = GetSelectedWave();
            if (wave == null)
                return;

            wave.Label = waveLabelTextBox.Text;
            wave.StartSeconds = (float)waveStartUpDown.Value;
            wave.Checkpoint = checkpointCheckBox.Checked;

            SortWaves();
            RefreshWaveList();
            SelectWaveByReference(wave);
            UpdateTimelineMaximum();
            RefreshPreview();
            RefreshValidation();
        }

        private void RefreshWaveList()
        {
            int selectedIndex = wavesListBox.SelectedIndex;

            wavesListBox.BeginUpdate();
            wavesListBox.Items.Clear();
            if (currentLevel != null)
            {
                for (int i = 0; i < currentLevel.Waves.Count; i++)
                {
                    WaveDefinition wave = currentLevel.Waves[i];
                    string checkpoint = wave.Checkpoint ? " [CP]" : string.Empty;
                    wavesListBox.Items.Add(string.Concat(wave.StartSeconds.ToString("0.0"), "s  ", wave.Label ?? string.Concat("Wave ", (i + 1).ToString()), checkpoint));
                }
            }
            wavesListBox.EndUpdate();

            if (wavesListBox.Items.Count == 0)
                return;

            wavesListBox.SelectedIndex = Math.Clamp(selectedIndex, 0, wavesListBox.Items.Count - 1);
        }

        private void RefreshGroupList()
        {
            WaveDefinition wave = GetSelectedWave();

            groupsListBox.BeginUpdate();
            groupsListBox.Items.Clear();
            if (wave != null)
            {
                foreach (SpawnGroupDefinition group in wave.Groups)
                    groupsListBox.Items.Add(DescribeGroup(group));
            }
            groupsListBox.EndUpdate();

            if (groupsListBox.Items.Count > 0)
                groupsListBox.SelectedIndex = Math.Clamp(groupsListBox.SelectedIndex, 0, groupsListBox.Items.Count - 1);
            else
                groupPropertyGrid.SelectedObject = null;

            RefreshPreview();
        }

        private void PopulateGroupEditors()
        {
            WaveDefinition wave = GetSelectedWave();
            if (wave == null || groupsListBox.SelectedIndex < 0 || groupsListBox.SelectedIndex >= wave.Groups.Count)
            {
                groupPropertyGrid.SelectedObject = null;
                return;
            }

            groupPropertyGrid.SelectedObject = wave.Groups[groupsListBox.SelectedIndex];
            RefreshPreview();
        }

        private void RefreshValidation()
        {
            if (currentLevel == null)
                return;

            List<ValidationIssue> issues = LevelValidator.ValidateLevel(currentLevel, archetypes);
            validationTextBox.Text = issues.Count == 0
                ? "No validation issues."
                : string.Join(Environment.NewLine, issues.Select(x => x.ToString()));

            RefreshPreview();
        }

        private void RefreshPreview()
        {
            previewPanel.Level = currentLevel;
            previewPanel.CurrentTimeSeconds = timelineTrackBar.Value / 10f;
            previewPanel.SelectedWaveIndex = wavesListBox.SelectedIndex;
            previewPanel.Invalidate();
        }

        private void RefreshTimelineAndPreview()
        {
            timelineLabel.Text = string.Concat("Time ", (timelineTrackBar.Value / 10f).ToString("0.0"), "s");
            RefreshPreview();
        }

        private void UpdateTimelineMaximum()
        {
            float duration = 8f;
            if (currentLevel != null && currentLevel.Waves.Count > 0)
            {
                duration = currentLevel.Waves.Max(x => x.StartSeconds + GetWaveDuration(x)) + 6f;
                if (currentLevel.Boss != null)
                    duration += 14f;
            }

            timelineTrackBar.Maximum = Math.Max(80, (int)Math.Ceiling(duration * 10f));
            if (timelineTrackBar.Value > timelineTrackBar.Maximum)
                timelineTrackBar.Value = timelineTrackBar.Maximum;
            RefreshTimelineAndPreview();
        }

        private float GetWaveDuration(WaveDefinition wave)
        {
            if (wave == null || wave.Groups.Count == 0)
                return 0f;

            return wave.Groups.Max(x => x.TravelDuration + x.DelayBetweenSpawns * Math.Max(0, x.Count - 1));
        }

        private void SortWaves()
        {
            if (currentLevel == null)
                return;

            currentLevel.Waves = currentLevel.Waves
                .OrderBy(x => x.StartSeconds)
                .ThenBy(x => x.Label ?? string.Empty)
                .ToList();
        }

        private void SelectWaveByReference(WaveDefinition wave)
        {
            int index = currentLevel?.Waves.IndexOf(wave) ?? -1;
            if (index >= 0)
                wavesListBox.SelectedIndex = index;
        }

        private WaveDefinition GetSelectedWave()
        {
            if (currentLevel == null || wavesListBox.SelectedIndex < 0 || wavesListBox.SelectedIndex >= currentLevel.Waves.Count)
                return null;

            return currentLevel.Waves[wavesListBox.SelectedIndex];
        }

        private SpawnGroupDefinition CreateDefaultGroup()
        {
            return new SpawnGroupDefinition
            {
                ArchetypeId = "Walker",
                Count = 3,
                Formation = FormationType.Line,
                EntrySide = EntrySide.Top,
                PathType = PathType.Straight,
                AnchorX = 0.3f,
                AnchorY = 0.2f,
                Spacing = 68f,
                DelayBetweenSpawns = 0.16f,
                TravelDuration = 3.2f,
                SpeedMultiplier = 1f,
            };
        }

        private string DescribeGroup(SpawnGroupDefinition group)
        {
            return string.Concat(
                group.ArchetypeId,
                " x",
                group.Count.ToString(),
                "  ",
                group.Formation.ToString(),
                " / ",
                group.PathType.ToString(),
                " @ ",
                group.AnchorX.ToString("0.00"),
                ", ",
                group.AnchorY.ToString("0.00"));
        }

        private int GetSelectedStageNumber()
        {
            if (stageComboBox.SelectedItem == null)
                return 1;

            return int.Parse(stageComboBox.SelectedItem.ToString() ?? "1");
        }

        private string GetStageFilePath(int stageNumber)
        {
            return Path.Combine(levelsDirectory, string.Concat("level-", stageNumber.ToString("00"), ".json"));
        }

        private void UpdateStageSelectionFromPath(string path)
        {
            Match match = Regex.Match(Path.GetFileName(path), @"level-(\d+)\.json", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int stageNumber))
            {
                string stageText = stageNumber.ToString("00");
                int index = stageComboBox.Items.IndexOf(stageText);
                if (index >= 0)
                    stageComboBox.SelectedIndex = index;
            }
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                string solutionPath = Path.Combine(directory.FullName, "SpaceBurst.sln");
                string levelsPath = Path.Combine(directory.FullName, "Levels");
                if (File.Exists(solutionPath) && Directory.Exists(levelsPath))
                    return directory.FullName;

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate the SpaceBurst repository root.");
        }

        private sealed class LevelPreviewPanel : Panel
        {
            private static readonly Vector2 previewArenaSize = new Vector2(800f, 600f);

            public IDictionary<string, EnemyArchetypeDefinition> Archetypes { get; set; }

            public LevelDefinition Level { get; set; }

            public int SelectedWaveIndex { get; set; }

            public float CurrentTimeSeconds { get; set; }

            public LevelPreviewPanel()
            {
                DoubleBuffered = true;
                BackColor = Color.FromArgb(18, 18, 26);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.Clear(Color.FromArgb(18, 18, 26));

                RectangleF arena = GetArenaBounds();
                using var arenaBrush = new SolidBrush(Color.FromArgb(22, 28, 40));
                using var arenaBorder = new Pen(Color.FromArgb(74, 100, 126), 2f);
                using var gridPen = new Pen(Color.FromArgb(36, 54, 74), 1f);

                e.Graphics.FillRectangle(arenaBrush, arena);
                for (int x = 1; x < 8; x++)
                {
                    float xPos = arena.Left + arena.Width * x / 8f;
                    e.Graphics.DrawLine(gridPen, xPos, arena.Top, xPos, arena.Bottom);
                }
                for (int y = 1; y < 6; y++)
                {
                    float yPos = arena.Top + arena.Height * y / 6f;
                    e.Graphics.DrawLine(gridPen, arena.Left, yPos, arena.Right, yPos);
                }
                e.Graphics.DrawRectangle(arenaBorder, Rectangle.Round(arena));

                DrawPlayerStart(e.Graphics, arena);

                if (Level == null)
                    return;

                for (int waveIndex = 0; waveIndex < Level.Waves.Count; waveIndex++)
                {
                    WaveDefinition wave = Level.Waves[waveIndex];
                    bool selectedWave = waveIndex == SelectedWaveIndex;
                    float emphasis = selectedWave ? 1f : 0.48f;

                    for (int groupIndex = 0; groupIndex < wave.Groups.Count; groupIndex++)
                        DrawSpawnGroup(e.Graphics, arena, wave, wave.Groups[groupIndex], groupIndex, emphasis);
                }

                if (Level.Boss != null)
                    DrawBossPreview(e.Graphics, arena, Level.Boss);

                using var textBrush = new SolidBrush(Color.FromArgb(220, 232, 240));
                string footer = string.Concat(
                    "Preview time: ",
                    CurrentTimeSeconds.ToString("0.0"),
                    "s    ",
                    Level.Name ?? "Untitled",
                    "    ",
                    Level.Waves.Count.ToString(),
                    " waves");
                e.Graphics.DrawString(footer, Font, textBrush, new PointF(arena.Left, arena.Bottom + 8f));
            }

            private RectangleF GetArenaBounds()
            {
                float margin = 24f;
                float maxWidth = ClientSize.Width - margin * 2f;
                float maxHeight = ClientSize.Height - margin * 2f - 30f;
                float scale = Math.Min(maxWidth / previewArenaSize.X, maxHeight / previewArenaSize.Y);

                SizeF size = new SizeF(previewArenaSize.X * scale, previewArenaSize.Y * scale);
                return new RectangleF(
                    (ClientSize.Width - size.Width) / 2f,
                    16f,
                    size.Width,
                    size.Height);
            }

            private void DrawPlayerStart(Graphics graphics, RectangleF arena)
            {
                Vector2 center = new Vector2(previewArenaSize.X / 2f, previewArenaSize.Y / 2f);
                PointF point = ToScreen(arena, center);
                using var brush = new SolidBrush(Color.FromArgb(250, 248, 248));
                using var pen = new Pen(Color.FromArgb(252, 168, 60), 2f);

                RectangleF bounds = new RectangleF(point.X - 9f, point.Y - 9f, 18f, 18f);
                graphics.FillEllipse(brush, bounds);
                graphics.DrawEllipse(pen, bounds);
                graphics.DrawString("Player", Font, brush, new PointF(point.X + 12f, point.Y - 8f));
            }

            private void DrawSpawnGroup(Graphics graphics, RectangleF arena, WaveDefinition wave, SpawnGroupDefinition group, int groupIndex, float emphasis)
            {
                IReadOnlyList<Vector2> offsets = LevelMath.GetFormationOffsets(group.Formation, group.Count, group.Spacing);
                Color baseColor = GetGroupColor(group.ArchetypeId, emphasis);
                using var pathPen = new Pen(Color.FromArgb((int)(100 * emphasis), baseColor), emphasis < 0.8f ? 1.4f : 2.2f);
                using var anchorBrush = new SolidBrush(Color.FromArgb((int)(180 * emphasis), baseColor));
                using var spawnBrush = new SolidBrush(Color.FromArgb((int)(120 * emphasis), baseColor));
                using var unitBrush = new SolidBrush(Color.FromArgb((int)(230 * emphasis), baseColor));

                float groupElapsed = CurrentTimeSeconds - wave.StartSeconds;
                bool isActive = groupElapsed >= 0f;
                float progress = group.TravelDuration <= 0.01f
                    ? 1f
                    : Math.Clamp(groupElapsed / group.TravelDuration, 0f, 1f);

                for (int i = 0; i < offsets.Count; i++)
                {
                    Vector2 offset = offsets[i];
                    Vector2 anchor = LevelMath.GetAnchorPoint(previewArenaSize, group.AnchorX, group.AnchorY, offset);
                    Vector2 spawn = LevelMath.GetSpawnPoint(group.EntrySide, previewArenaSize, anchor, offset, 90f);
                    PointF anchorPoint = ToScreen(arena, anchor);
                    PointF spawnPoint = ToScreen(arena, spawn);

                    graphics.FillEllipse(anchorBrush, anchorPoint.X - 4f, anchorPoint.Y - 4f, 8f, 8f);
                    graphics.FillRectangle(spawnBrush, spawnPoint.X - 3f, spawnPoint.Y - 3f, 6f, 6f);

                    PointF? lastPoint = null;
                    for (int sample = 0; sample <= 20; sample++)
                    {
                        float sampleProgress = sample / 20f;
                        Vector2 pathPosition = LevelMath.SamplePreviewPath(group.PathType, spawn, anchor, sampleProgress, i);
                        PointF point = ToScreen(arena, pathPosition);
                        if (lastPoint.HasValue)
                            graphics.DrawLine(pathPen, lastPoint.Value, point);
                        lastPoint = point;
                    }

                    if (!isActive)
                        continue;

                    Vector2 current = LevelMath.SamplePreviewPath(group.PathType, spawn, anchor, progress, i);
                    PointF currentPoint = ToScreen(arena, current);
                    float radius = GetUnitRadius(group.ArchetypeId);
                    graphics.FillEllipse(unitBrush, currentPoint.X - radius, currentPoint.Y - radius, radius * 2f, radius * 2f);
                }
            }

            private void DrawBossPreview(Graphics graphics, RectangleF arena, BossDefinition boss)
            {
                Vector2 anchor = LevelMath.GetAnchorPoint(previewArenaSize, boss.AnchorX, boss.AnchorY, Vector2.Zero);
                Vector2 spawn = LevelMath.GetSpawnPoint(boss.EntrySide, previewArenaSize, anchor, Vector2.Zero, 120f);
                float lastWaveEnd = Level.Waves.Count == 0 ? 0f : Level.Waves.Max(x => x.StartSeconds + x.Groups.Max(g => g.TravelDuration));
                float bossStart = lastWaveEnd + 2f;
                float progress = Math.Clamp((CurrentTimeSeconds - bossStart) / 3f, 0f, 1f);
                Vector2 current = Vector2.Lerp(spawn, anchor, progress);
                PointF spawnPoint = ToScreen(arena, spawn);
                PointF anchorPoint = ToScreen(arena, anchor);
                PointF currentPoint = ToScreen(arena, current);

                using var bossPen = new Pen(Color.FromArgb(245, 226, 113, 46), 3f);
                using var bossBrush = new SolidBrush(Color.FromArgb(220, 226, 113, 46));
                using var labelBrush = new SolidBrush(Color.FromArgb(232, 255, 243, 228));

                graphics.DrawLine(bossPen, spawnPoint, anchorPoint);
                float radius = 18f + boss.RenderScale * 6f;
                graphics.FillEllipse(bossBrush, currentPoint.X - radius, currentPoint.Y - radius, radius * 2f, radius * 2f);
                graphics.DrawString(string.Concat(boss.DisplayName, " (", boss.HitPoints.ToString(), " HP)"), Font, labelBrush, new PointF(currentPoint.X + radius + 6f, currentPoint.Y - 10f));
            }

            private Color GetGroupColor(string archetypeId, float emphasis)
            {
                Color color = archetypeId switch
                {
                    "Walker" => Color.FromArgb(70, 202, 222),
                    "Interceptor" => Color.FromArgb(250, 209, 72),
                    "Destroyer" => Color.FromArgb(236, 114, 77),
                    "Turret" => Color.FromArgb(118, 204, 125),
                    "Bulwark" => Color.FromArgb(186, 154, 106),
                    _ => Color.FromArgb(200, 200, 200),
                };

                return Color.FromArgb(Math.Clamp((int)(255 * emphasis), 80, 255), color);
            }

            private float GetUnitRadius(string archetypeId)
            {
                if (Archetypes != null && Archetypes.TryGetValue(archetypeId, out EnemyArchetypeDefinition archetype))
                    return Math.Clamp(archetype.CollisionRadius / 3f, 5f, 14f);

                return 8f;
            }

            private static PointF ToScreen(RectangleF arena, Vector2 value)
            {
                return new PointF(
                    arena.Left + value.X / previewArenaSize.X * arena.Width,
                    arena.Top + value.Y / previewArenaSize.Y * arena.Height);
            }
        }
    }
}
