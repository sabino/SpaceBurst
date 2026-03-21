using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SpaceBurst
{
    internal sealed class PlatformCapabilities
    {
        public PlatformCapabilities(
            bool supportsWindowedDisplayModes,
            bool supportsTextInput,
            bool supportsMouseCursor,
            bool supportsTouch,
            bool supportsGamepad,
            bool audioRequiresUserGesture,
            bool preferDepth16RenderTargets,
            bool supportsScreenCapture)
        {
            SupportsWindowedDisplayModes = supportsWindowedDisplayModes;
            SupportsTextInput = supportsTextInput;
            SupportsMouseCursor = supportsMouseCursor;
            SupportsTouch = supportsTouch;
            SupportsGamepad = supportsGamepad;
            AudioRequiresUserGesture = audioRequiresUserGesture;
            PreferDepth16RenderTargets = preferDepth16RenderTargets;
            SupportsScreenCapture = supportsScreenCapture;
        }

        public bool SupportsWindowedDisplayModes { get; }
        public bool SupportsTextInput { get; }
        public bool SupportsMouseCursor { get; }
        public bool SupportsTouch { get; }
        public bool SupportsGamepad { get; }
        public bool AudioRequiresUserGesture { get; }
        public bool PreferDepth16RenderTargets { get; }
        public bool SupportsScreenCapture { get; }

        public static PlatformCapabilities CreateDesktop()
        {
            return new PlatformCapabilities(
                supportsWindowedDisplayModes: true,
                supportsTextInput: true,
                supportsMouseCursor: true,
                supportsTouch: false,
                supportsGamepad: true,
                audioRequiresUserGesture: false,
                preferDepth16RenderTargets: false,
                supportsScreenCapture: true);
        }

        public static PlatformCapabilities CreateAndroid()
        {
            return new PlatformCapabilities(
                supportsWindowedDisplayModes: false,
                supportsTextInput: false,
                supportsMouseCursor: false,
                supportsTouch: true,
                supportsGamepad: true,
                audioRequiresUserGesture: false,
                preferDepth16RenderTargets: false,
                supportsScreenCapture: true);
        }
    }

    internal interface IStorageBackend
    {
        bool Exists(string logicalPath);
        string ReadAllText(string logicalPath);
        void WriteAllText(string logicalPath, string contents);
        void Delete(string logicalPath);
        IEnumerable<string> ListKeys(string prefix);
        string GetDisplayPath(string logicalPath);
    }

    internal interface ITextAssetProvider
    {
        string ReadAllText(string logicalPath);
    }

    internal interface IAudioStartGate
    {
        bool RequiresUserGesture { get; }
        bool IsReady { get; }
        void NotifyPrimaryGesture();
    }

    internal static class PlatformServices
    {
        private static bool initialized;

        public static PlatformCapabilities Capabilities { get; private set; }
        public static IStorageBackend Storage { get; private set; }
        public static ITextAssetProvider TextAssets { get; private set; }
        public static IAudioStartGate AudioStartGate { get; private set; }

        static PlatformServices()
        {
            Capabilities = PlatformCapabilities.CreateDesktop();
            Storage = CreateDefaultFileStorageBackend();
            TextAssets = new TitleContainerTextAssetProvider();
            AudioStartGate = ImmediateAudioStartGate.Instance;
        }

        public static void Initialize(
            PlatformCapabilities capabilities,
            IStorageBackend storage,
            ITextAssetProvider textAssets,
            IAudioStartGate audioStartGate)
        {
            Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
            Storage = storage ?? throw new ArgumentNullException(nameof(storage));
            TextAssets = textAssets ?? throw new ArgumentNullException(nameof(textAssets));
            AudioStartGate = audioStartGate ?? ImmediateAudioStartGate.Instance;
            initialized = true;
        }

        public static void EnsureInitialized()
        {
            if (initialized)
                return;

#if BLAZORGL
            throw new InvalidOperationException("PlatformServices.Initialize must be called before starting the browser build.");
#elif ANDROID
            Initialize(
                PlatformCapabilities.CreateAndroid(),
                CreateDefaultFileStorageBackend(),
                new TitleContainerTextAssetProvider(),
                ImmediateAudioStartGate.Instance);
#else
            Initialize(
                PlatformCapabilities.CreateDesktop(),
                CreateDefaultFileStorageBackend(),
                new TitleContainerTextAssetProvider(),
                ImmediateAudioStartGate.Instance);
#endif
        }

        public static IStorageBackend CreateDefaultFileStorageBackend()
        {
            string documentsBaseDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "SpaceBurst");
            string legacyBaseDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SpaceBurst");

            return new FileStorageBackend(documentsBaseDirectory, legacyBaseDirectory);
        }

        public static ITextAssetProvider CreateDefaultTextAssetProvider()
        {
            return new TitleContainerTextAssetProvider();
        }

        public static IAudioStartGate CreateImmediateAudioStartGate()
        {
            return ImmediateAudioStartGate.Instance;
        }

        private sealed class TitleContainerTextAssetProvider : ITextAssetProvider
        {
            public string ReadAllText(string logicalPath)
            {
                using Stream stream = TitleContainer.OpenStream((logicalPath ?? string.Empty).Replace('\\', '/'));
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
        }

        private sealed class ImmediateAudioStartGate : IAudioStartGate
        {
            public static ImmediateAudioStartGate Instance { get; } = new ImmediateAudioStartGate();

            public bool RequiresUserGesture
            {
                get { return false; }
            }

            public bool IsReady
            {
                get { return true; }
            }

            public void NotifyPrimaryGesture()
            {
            }
        }
    }

    internal sealed class FileStorageBackend : IStorageBackend
    {
        private readonly string baseDirectory;
        private readonly string legacyBaseDirectory;
        private bool initialized;

        public FileStorageBackend(string baseDirectory, string legacyBaseDirectory)
        {
            this.baseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory));
            this.legacyBaseDirectory = string.IsNullOrWhiteSpace(legacyBaseDirectory) ? null : legacyBaseDirectory;
        }

        public bool Exists(string logicalPath)
        {
            EnsureInitialized();
            return File.Exists(MapPath(logicalPath));
        }

        public string ReadAllText(string logicalPath)
        {
            EnsureInitialized();
            return File.ReadAllText(MapPath(logicalPath));
        }

        public void WriteAllText(string logicalPath, string contents)
        {
            EnsureInitialized();
            string path = MapPath(logicalPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? baseDirectory);
            File.WriteAllText(path, contents ?? string.Empty);
        }

        public void Delete(string logicalPath)
        {
            EnsureInitialized();
            string path = MapPath(logicalPath);
            if (File.Exists(path))
                File.Delete(path);
        }

        public IEnumerable<string> ListKeys(string prefix)
        {
            EnsureInitialized();
            if (!Directory.Exists(baseDirectory))
                return Array.Empty<string>();

            string normalizedPrefix = NormalizeLogicalPath(prefix);
            return Directory.GetFiles(baseDirectory, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(baseDirectory, path).Replace('\\', '/'))
                .Where(path => normalizedPrefix.Length == 0 || path.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        public string GetDisplayPath(string logicalPath)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(logicalPath))
                return baseDirectory;

            return MapPath(logicalPath);
        }

        private void EnsureInitialized()
        {
            if (initialized)
                return;

            initialized = true;
            Directory.CreateDirectory(baseDirectory);
            Directory.CreateDirectory(Path.Combine(baseDirectory, "config"));

            if (!string.IsNullOrWhiteSpace(legacyBaseDirectory) && Directory.Exists(legacyBaseDirectory))
                CopyMissingRecursive(legacyBaseDirectory, baseDirectory);
        }

        private string MapPath(string logicalPath)
        {
            string normalized = NormalizeLogicalPath(logicalPath);
            if (normalized.Length == 0)
                return baseDirectory;

            string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return Path.Combine(new[] { baseDirectory }.Concat(segments).ToArray());
        }

        private static string NormalizeLogicalPath(string logicalPath)
        {
            if (string.IsNullOrWhiteSpace(logicalPath))
                return string.Empty;

            string[] parts = logicalPath
                .Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries);

            var safeParts = new List<string>(parts.Length);
            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (trimmed.Length == 0 || trimmed == ".")
                    continue;
                if (trimmed == "..")
                    continue;

                safeParts.Add(trimmed);
            }

            return string.Join("/", safeParts);
        }

        private static void CopyMissingRecursive(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);

            foreach (string sourceFile in Directory.GetFiles(sourceDirectory))
            {
                string destinationFile = Path.Combine(destinationDirectory, Path.GetFileName(sourceFile));
                if (!File.Exists(destinationFile))
                    File.Copy(sourceFile, destinationFile);
            }

            foreach (string sourceSubdirectory in Directory.GetDirectories(sourceDirectory))
            {
                string destinationSubdirectory = Path.Combine(destinationDirectory, Path.GetFileName(sourceSubdirectory));
                CopyMissingRecursive(sourceSubdirectory, destinationSubdirectory);
            }
        }
    }
}
