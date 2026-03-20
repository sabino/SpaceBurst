using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SpaceBurst.RuntimeData;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SpaceBurst
{
    sealed class ConsoleVariable
    {
        private readonly Func<string> getter;
        private readonly Func<string, bool> setter;

        public ConsoleVariable(string name, string description, bool cheatOnly, bool toggleable, Func<string> getter, Func<string, bool> setter)
        {
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
            CheatOnly = cheatOnly;
            Toggleable = toggleable;
            this.getter = getter ?? (() => string.Empty);
            this.setter = setter ?? (_ => false);
        }

        public string Name { get; }
        public string Description { get; }
        public bool CheatOnly { get; }
        public bool Toggleable { get; }

        public string GetValue()
        {
            return getter();
        }

        public bool TrySet(string value)
        {
            return setter(value ?? string.Empty);
        }
    }

    sealed class InputBindingStore
    {
        private static readonly Dictionary<string, Keys> keyAliases = new Dictionary<string, Keys>(StringComparer.OrdinalIgnoreCase)
        {
            ["`"] = Keys.OemTilde,
            ["~"] = Keys.OemTilde,
            ["tilde"] = Keys.OemTilde,
            ["grave"] = Keys.OemTilde,
            ["pgup"] = Keys.PageUp,
            ["pageup"] = Keys.PageUp,
            ["pgdn"] = Keys.PageDown,
            ["pagedown"] = Keys.PageDown,
            ["esc"] = Keys.Escape,
            ["return"] = Keys.Enter,
            ["ctrl"] = Keys.LeftControl,
            ["lctrl"] = Keys.LeftControl,
            ["rctrl"] = Keys.RightControl,
            ["shift"] = Keys.LeftShift,
            ["lshift"] = Keys.LeftShift,
            ["rshift"] = Keys.RightShift,
        };

        private readonly Dictionary<Keys, string> bindings = new Dictionary<Keys, string>();

        public IEnumerable<KeyValuePair<Keys, string>> AllBindings
        {
            get { return bindings.OrderBy(pair => pair.Key.ToString(), StringComparer.OrdinalIgnoreCase); }
        }

        public void Bind(Keys key, string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return;

            bindings[key] = command.Trim();
        }

        public bool Unbind(Keys key)
        {
            return bindings.Remove(key);
        }

        public bool TryGetCommand(Keys key, out string command)
        {
            return bindings.TryGetValue(key, out command);
        }

        public static bool TryParseKey(string token, out Keys key)
        {
            key = Keys.None;
            if (string.IsNullOrWhiteSpace(token))
                return false;

            string trimmed = token.Trim();
            if (keyAliases.TryGetValue(trimmed, out key))
                return true;

            return Enum.TryParse(trimmed, true, out key);
        }
    }

    sealed class ConsoleCommandRegistry
    {
        public delegate void CommandHandler(ConsoleState console, string[] args, string rawArgs);

        private readonly Dictionary<string, CommandHandler> commands = new Dictionary<string, CommandHandler>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ConsoleVariable> variables = new Dictionary<string, ConsoleVariable>(StringComparer.OrdinalIgnoreCase);

        public IEnumerable<string> CommandNames
        {
            get { return commands.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase); }
        }

        public IEnumerable<ConsoleVariable> Variables
        {
            get { return variables.Values.OrderBy(variable => variable.Name, StringComparer.OrdinalIgnoreCase); }
        }

        public void RegisterCommand(string name, CommandHandler handler)
        {
            if (string.IsNullOrWhiteSpace(name) || handler == null)
                return;

            commands[name.Trim()] = handler;
        }

        public void RegisterVariable(ConsoleVariable variable)
        {
            if (variable == null || string.IsNullOrWhiteSpace(variable.Name))
                return;

            variables[variable.Name] = variable;
        }

        public bool TryGetVariable(string name, out ConsoleVariable variable)
        {
            return variables.TryGetValue(name, out variable);
        }

        public bool TryExecuteCommand(ConsoleState console, string name, string[] args, string rawArgs)
        {
            if (commands.TryGetValue(name, out CommandHandler handler))
            {
                handler(console, args, rawArgs);
                return true;
            }

            return false;
        }
    }

    sealed class ConsoleState
    {
        private const int MaxLogLines = 256;
        private const int VisibleLogLines = 18;
        private const string ConfigFileName = "config.cfg";
        private const string AutoExecFileName = "autoexec.cfg";
        private const string LogFileName = "console.log";
        private const string HistoryFileName = "console-history.txt";

        private readonly Game1 game;
        private readonly CampaignDirector director;
        private readonly ConsoleCommandRegistry registry = new ConsoleCommandRegistry();
        private readonly InputBindingStore bindings = new InputBindingStore();
        private readonly List<string> logLines = new List<string>();
        private readonly List<string> commandHistory = new List<string>();
        private string inputBuffer = string.Empty;
        private string historyDraft = string.Empty;
        private int historyCursor = -1;
        private int scrollOffset;

        public ConsoleState(Game1 game, CampaignDirector director)
        {
            this.game = game;
            this.director = director;
            RegisterBuiltins();
            AppendLine("SpaceBurst developer console");
            AppendLine("Type 'help' for commands.");
            LoadStartupConfig();
        }

        public bool IsOpen { get; private set; }

        public bool CheatsEnabled
        {
            get { return DeveloperVisualSettings.CheatsEnabled; }
            set
            {
                DeveloperVisualSettings.CheatsEnabled = value;
                if (!value)
                    DeveloperVisualSettings.ShowBounds = false;
            }
        }

        public bool ShowBounds
        {
            get { return DeveloperVisualSettings.ShowBounds; }
            set { DeveloperVisualSettings.ShowBounds = value && CheatsEnabled; }
        }

        public void Update()
        {
#if ANDROID
            return;
#else
            if (Input.WasConsoleTogglePressed())
            {
                Toggle();
                Input.ConsumeKey(Keys.OemTilde);
                return;
            }

            if (IsOpen)
            {
                UpdateOpenConsole();
                return;
            }

            Keys[] pressedKeys = Input.GetPressedKeysThisFrame();
            for (int i = 0; i < pressedKeys.Length; i++)
            {
                Keys key = pressedKeys[i];
                if (!bindings.TryGetCommand(key, out string command) || string.IsNullOrWhiteSpace(command))
                    continue;

                Execute(command, false);
                Input.ConsumeKey(key);
            }
#endif
        }

        public void HandleTextInput(char character)
        {
#if ANDROID
            return;
#else
            if (!IsOpen)
                return;

            if (character == '`' || character == '~' || char.IsControl(character))
                return;

            inputBuffer += character;
#endif
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
        {
#if ANDROID
            return;
#else
            if (!IsOpen || spriteBatch == null || pixel == null)
                return;

            Rectangle bounds = new Rectangle(24, 20, Game1.VirtualWidth - 48, Math.Min(Game1.VirtualHeight - 40, 312));
            Rectangle inputBounds = new Rectangle(bounds.X + 14, bounds.Bottom - 38, bounds.Width - 28, 24);
            spriteBatch.Draw(pixel, bounds, Color.Black * 0.82f);
            spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 2), Color.Cyan * 0.48f);
            spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - 48, bounds.Width, 1), Color.White * 0.14f);
            BitmapFontRenderer.Draw(spriteBatch, pixel, "CONSOLE", new Vector2(bounds.X + 14, bounds.Y + 10), Color.White, 0.92f);

            int availableLines = Math.Max(0, Math.Min(VisibleLogLines, logLines.Count));
            int startIndex = Math.Max(0, logLines.Count - availableLines - scrollOffset);
            int y = bounds.Y + 32;
            for (int i = startIndex; i < logLines.Count - scrollOffset && i < startIndex + VisibleLogLines; i++)
            {
                BitmapFontRenderer.Draw(spriteBatch, pixel, Truncate(logLines[i], 132), new Vector2(bounds.X + 14, y), Color.White * 0.82f, 0.82f);
                y += 14;
            }

            string prompt = string.Concat("] ", inputBuffer);
            BitmapFontRenderer.Draw(spriteBatch, pixel, Truncate(prompt, 138), new Vector2(inputBounds.X, inputBounds.Y), Color.White, 0.92f);
            float pulse = 0.35f + 0.65f * MathF.Abs(MathF.Sin((float)Game1.GameTime.TotalGameTime.TotalSeconds * 4.2f));
            int caretX = inputBounds.X + Math.Max(2, (int)MathF.Round(8f * Truncate(prompt, 138).Length * 0.58f));
            spriteBatch.Draw(pixel, new Rectangle(caretX, inputBounds.Y - 1, 2, 14), Color.Cyan * pulse);
            BitmapFontRenderer.Draw(spriteBatch, pixel, "ENTER RUNS  ESC CLOSES  PGUP/PGDN SCROLL", new Vector2(bounds.Right - 328, bounds.Y + 10), Color.White * 0.55f, 0.62f);
#endif
        }

        public void Execute(string line, bool echoCommand = true)
        {
            string trimmed = (line ?? string.Empty).Trim();
            if (trimmed.Length == 0)
                return;

            if (echoCommand)
                AppendLine(string.Concat("] ", trimmed));

            commandHistory.Add(trimmed);
            historyCursor = -1;
            historyDraft = string.Empty;
            PersistentStorage.AppendConfigLine(HistoryFileName, trimmed);

            string[] tokens = Tokenize(trimmed);
            if (tokens.Length == 0)
                return;

            string name = tokens[0];
            string rawArgs = trimmed.Length > name.Length ? trimmed.Substring(name.Length).TrimStart() : string.Empty;
            string[] args = tokens.Skip(1).ToArray();

            if (registry.TryExecuteCommand(this, name, args, rawArgs))
                return;

            if (registry.TryGetVariable(name, out ConsoleVariable variable))
            {
                if (args.Length == 0)
                {
                    AppendLine(string.Concat(variable.Name, " = ", variable.GetValue()));
                    return;
                }

                if (variable.CheatOnly && !CheatsEnabled)
                {
                    AppendLine("sv_cheats 1 is required.");
                    return;
                }

                if (variable.TrySet(args[0]))
                    AppendLine(string.Concat(variable.Name, " = ", variable.GetValue()));
                else
                    AppendLine(string.Concat("Invalid value for ", variable.Name));
                return;
            }

            AppendLine(string.Concat("Unknown command: ", name));
        }

        internal void AppendLine(string line)
        {
            string safeLine = line ?? string.Empty;
            logLines.Add(safeLine);
            while (logLines.Count > MaxLogLines)
                logLines.RemoveAt(0);

            PersistentStorage.AppendConfigLine(LogFileName, safeLine);
        }

        internal bool TrySetVariable(string name, string value)
        {
            if (!registry.TryGetVariable(name, out ConsoleVariable variable))
                return false;

            if (variable.CheatOnly && !CheatsEnabled)
                return false;

            return variable.TrySet(value);
        }

        internal bool TryToggleVariable(string name)
        {
            if (!registry.TryGetVariable(name, out ConsoleVariable variable) || !variable.Toggleable)
                return false;

            if (variable.CheatOnly && !CheatsEnabled)
                return false;

            return variable.TrySet(variable.GetValue() == "0" ? "1" : "0");
        }

        internal IEnumerable<string> EnumerateCommandNames()
        {
            return registry.CommandNames;
        }

        internal IEnumerable<ConsoleVariable> EnumerateVariables()
        {
            return registry.Variables;
        }

        internal bool TryBindKey(string keyToken, string command)
        {
            if (!InputBindingStore.TryParseKey(keyToken, out Keys key) || key == Keys.None || key == Keys.OemTilde)
                return false;

            bindings.Bind(key, command);
            return true;
        }

        internal bool TryUnbindKey(string keyToken)
        {
            if (!InputBindingStore.TryParseKey(keyToken, out Keys key) || key == Keys.None)
                return false;

            return bindings.Unbind(key);
        }

        internal string GetBinding(string keyToken)
        {
            if (!InputBindingStore.TryParseKey(keyToken, out Keys key) || key == Keys.None)
                return string.Empty;

            return bindings.TryGetCommand(key, out string command) ? command : string.Empty;
        }

        internal void WriteConfig()
        {
            var lines = new List<string>
            {
                string.Concat("sv_cheats ", CheatsEnabled ? "1" : "0"),
                string.Concat("showbounds ", ShowBounds ? "1" : "0"),
            };

            foreach (KeyValuePair<Keys, string> binding in bindings.AllBindings)
                lines.Add(string.Concat("bind ", binding.Key, " \"", binding.Value.Replace("\"", "\\\""), "\""));

            PersistentStorage.WriteConfigLines(ConfigFileName, lines.ToArray());
            AppendLine(string.Concat("Wrote ", PersistentStorage.GetConfigFilePath(ConfigFileName)));
        }

        internal bool TryExecFile(string fileName)
        {
            string normalizedName = NormalizeConfigFileName(fileName);
            string path = PersistentStorage.GetConfigFilePath(normalizedName);
            if (!File.Exists(path))
                return false;

            string[] lines = PersistentStorage.ReadConfigLines(normalizedName);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal) || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                Execute(line, false);
            }

            AppendLine(string.Concat("Executed ", normalizedName));
            return true;
        }

        internal string GetVersionString()
        {
            Assembly assembly = game != null ? game.GetType().Assembly : typeof(ConsoleState).Assembly;
            Version version = assembly.GetName().Version;
            string informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informational))
                return informational;

            return version != null ? version.ToString() : "dev";
        }

        private void Toggle()
        {
            IsOpen = !IsOpen;
            scrollOffset = 0;
            if (IsOpen)
                AppendLine("Console opened.");
        }

        private void UpdateOpenConsole()
        {
            if (Input.WasKeyPressed(Keys.Escape))
            {
                IsOpen = false;
                return;
            }

            if (Input.WasKeyPressed(Keys.Enter))
            {
                string command = inputBuffer;
                inputBuffer = string.Empty;
                if (!string.IsNullOrWhiteSpace(command))
                    Execute(command);
                return;
            }

            if (Input.WasKeyPressed(Keys.Back))
            {
                if (inputBuffer.Length > 0)
                    inputBuffer = inputBuffer.Substring(0, inputBuffer.Length - 1);
                return;
            }

            if (Input.WasKeyPressed(Keys.Up))
            {
                if (commandHistory.Count == 0)
                    return;

                if (historyCursor < 0)
                    historyDraft = inputBuffer;

                historyCursor = historyCursor < 0
                    ? commandHistory.Count - 1
                    : Math.Max(0, historyCursor - 1);
                inputBuffer = commandHistory[historyCursor];
                return;
            }

            if (Input.WasKeyPressed(Keys.Down))
            {
                if (historyCursor < 0)
                    return;

                historyCursor++;
                if (historyCursor >= commandHistory.Count)
                {
                    historyCursor = -1;
                    inputBuffer = historyDraft;
                }
                else
                {
                    inputBuffer = commandHistory[historyCursor];
                }

                return;
            }

            if (Input.WasKeyPressed(Keys.PageUp))
            {
                scrollOffset = Math.Min(logLines.Count, scrollOffset + 3);
                return;
            }

            if (Input.WasKeyPressed(Keys.PageDown))
            {
                scrollOffset = Math.Max(0, scrollOffset - 3);
            }
        }

        private void LoadStartupConfig()
        {
            DeveloperVisualSettings.CheatsEnabled = false;
            DeveloperVisualSettings.ShowBounds = false;
            TryExecFile(ConfigFileName);
            TryExecFile(AutoExecFileName);
        }

        private void RegisterBuiltins()
        {
            registry.RegisterVariable(new ConsoleVariable(
                "sv_cheats",
                "Enable cheat-gated console commands.",
                false,
                true,
                () => CheatsEnabled ? "1" : "0",
                value =>
                {
                    if (!TryParseBool(value, out bool enabled))
                        return false;

                    CheatsEnabled = enabled;
                    return true;
                }));

            registry.RegisterVariable(new ConsoleVariable(
                "showbounds",
                "Show developer bounds for enhanced presentation tiers.",
                true,
                true,
                () => ShowBounds ? "1" : "0",
                value =>
                {
                    if (!TryParseBool(value, out bool enabled))
                        return false;

                    if (enabled && !CheatsEnabled)
                        return false;

                    ShowBounds = enabled;
                    return true;
                }));

            registry.RegisterCommand("help", (console, args, rawArgs) =>
            {
                console.AppendLine("Commands:");
                foreach (string name in console.EnumerateCommandNames())
                    console.AppendLine(string.Concat("  ", name));
                console.AppendLine("Variables:");
                foreach (ConsoleVariable variable in console.EnumerateVariables())
                    console.AppendLine(string.Concat("  ", variable.Name, " = ", variable.GetValue()));
            });

            registry.RegisterCommand("clear", (console, args, rawArgs) =>
            {
                console.logLines.Clear();
                console.AppendLine("Console cleared.");
            });

            registry.RegisterCommand("echo", (console, args, rawArgs) =>
            {
                console.AppendLine(rawArgs);
            });

            registry.RegisterCommand("version", (console, args, rawArgs) =>
            {
                console.AppendLine(string.Concat("SpaceBurst ", console.GetVersionString()));
            });

            registry.RegisterCommand("map", (console, args, rawArgs) =>
            {
                if (!console.CheatsEnabled)
                {
                    console.AppendLine("sv_cheats 1 is required.");
                    return;
                }

                if (args.Length < 1 || !int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int stageNumber))
                {
                    console.AppendLine("Usage: map <stage>");
                    return;
                }

                if (director != null && director.TryConsoleLoadStage(stageNumber))
                    console.AppendLine(string.Concat("Loaded stage ", stageNumber.ToString("00")));
                else
                    console.AppendLine("Could not load that stage.");
            });

            registry.RegisterCommand("viewmode", (console, args, rawArgs) =>
            {
                if (args.Length < 1)
                {
                    console.AppendLine("Usage: viewmode <2d|3d>");
                    return;
                }

                ViewMode requestedMode = args[0].Equals("3d", StringComparison.OrdinalIgnoreCase)
                    ? ViewMode.Chase3D
                    : ViewMode.SideScroller;

                if (director != null && director.TryConsoleSetViewMode(requestedMode))
                    console.AppendLine(string.Concat("viewmode = ", requestedMode == ViewMode.Chase3D ? "3d" : "2d"));
                else
                    console.AppendLine("Requested view mode is not available here.");
            });

            registry.RegisterCommand("detail", (console, args, rawArgs) =>
            {
                if (!console.CheatsEnabled)
                {
                    console.AppendLine("sv_cheats 1 is required.");
                    return;
                }

                if (args.Length < 1)
                {
                    console.AppendLine("Usage: detail <auto|pixel2d|voxelshell|hybridmesh|late3d>");
                    return;
                }

                PresentationTier? tier = ParsePresentationTier(args[0]);
                if (args[0].Equals("auto", StringComparison.OrdinalIgnoreCase))
                    tier = null;

                if (!args[0].Equals("auto", StringComparison.OrdinalIgnoreCase) && !tier.HasValue)
                {
                    console.AppendLine("Unknown detail tier.");
                    return;
                }

                if (director != null && director.TryConsoleSetPresentationOverride(tier))
                    console.AppendLine(string.Concat("detail = ", tier.HasValue ? tier.Value.ToString().ToLowerInvariant() : "auto"));
                else
                    console.AppendLine("Could not change detail tier.");
            });

            registry.RegisterCommand("bind", (console, args, rawArgs) =>
            {
                if (args.Length < 1)
                {
                    console.AppendLine("Usage: bind <key> <command>");
                    return;
                }

                SplitFirstToken(rawArgs, out string keyToken, out string commandText);
                if (string.IsNullOrWhiteSpace(commandText))
                {
                    string existing = console.GetBinding(keyToken);
                    console.AppendLine(existing.Length == 0
                        ? string.Concat(keyToken, " is unbound")
                        : string.Concat(keyToken, " = ", existing));
                    return;
                }

                commandText = Unquote(commandText);
                if (!console.TryBindKey(keyToken, commandText))
                {
                    console.AppendLine("Unknown or invalid key.");
                    return;
                }

                console.AppendLine(string.Concat("Bound ", keyToken, " to ", commandText));
            });

            registry.RegisterCommand("unbind", (console, args, rawArgs) =>
            {
                if (args.Length < 1)
                {
                    console.AppendLine("Usage: unbind <key>");
                    return;
                }

                if (console.TryUnbindKey(args[0]))
                    console.AppendLine(string.Concat("Unbound ", args[0]));
                else
                    console.AppendLine("Unknown key or no binding.");
            });

            registry.RegisterCommand("toggle", (console, args, rawArgs) =>
            {
                if (args.Length < 1)
                {
                    console.AppendLine("Usage: toggle <cvar>");
                    return;
                }

                if (console.TryToggleVariable(args[0]))
                    console.AppendLine(string.Concat(args[0], " toggled."));
                else
                    console.AppendLine("Could not toggle that variable.");
            });

            registry.RegisterCommand("exec", (console, args, rawArgs) =>
            {
                if (args.Length < 1)
                {
                    console.AppendLine("Usage: exec <file>");
                    return;
                }

                if (!console.TryExecFile(args[0]))
                    console.AppendLine("Could not execute that file.");
            });

            registry.RegisterCommand("writeconfig", (console, args, rawArgs) =>
            {
                console.WriteConfig();
            });
        }

        private static PresentationTier? ParsePresentationTier(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return null;

            return token.ToLowerInvariant() switch
            {
                "pixel2d" => PresentationTier.Pixel2D,
                "voxelshell" => PresentationTier.VoxelShell,
                "hybridmesh" => PresentationTier.HybridMesh,
                "late3d" => PresentationTier.Late3D,
                _ => null,
            };
        }

        private static bool TryParseBool(string value, out bool enabled)
        {
            string trimmed = (value ?? string.Empty).Trim();
            switch (trimmed)
            {
                case "1":
                case "true":
                case "on":
                case "yes":
                    enabled = true;
                    return true;
                case "0":
                case "false":
                case "off":
                case "no":
                    enabled = false;
                    return true;
                default:
                    enabled = false;
                    return false;
            }
        }

        private static string NormalizeConfigFileName(string fileName)
        {
            string trimmed = (fileName ?? string.Empty).Trim();
            if (trimmed.Length == 0)
                return ConfigFileName;

            if (!trimmed.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                trimmed += ".cfg";
            }

            return trimmed;
        }

        private static string[] Tokenize(string line)
        {
            var tokens = new List<string>();
            if (string.IsNullOrWhiteSpace(line))
                return tokens.ToArray();

            var builder = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char character = line[i];
                if (character == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (!inQuotes && char.IsWhiteSpace(character))
                {
                    if (builder.Length > 0)
                    {
                        tokens.Add(builder.ToString());
                        builder.Clear();
                    }

                    continue;
                }

                builder.Append(character);
            }

            if (builder.Length > 0)
                tokens.Add(builder.ToString());

            return tokens.ToArray();
        }

        private static void SplitFirstToken(string text, out string firstToken, out string remainder)
        {
            string trimmed = (text ?? string.Empty).Trim();
            int separator = trimmed.IndexOfAny(new[] { ' ', '\t' });
            if (separator < 0)
            {
                firstToken = trimmed;
                remainder = string.Empty;
                return;
            }

            firstToken = trimmed.Substring(0, separator);
            remainder = trimmed.Substring(separator).Trim();
        }

        private static string Unquote(string value)
        {
            string trimmed = (value ?? string.Empty).Trim();
            if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[trimmed.Length - 1] == '"')
                trimmed = trimmed.Substring(1, trimmed.Length - 2);
            return trimmed.Replace("\\\"", "\"");
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value ?? string.Empty;

            return value.Substring(0, Math.Max(0, maxLength - 3)) + "...";
        }
    }
}
