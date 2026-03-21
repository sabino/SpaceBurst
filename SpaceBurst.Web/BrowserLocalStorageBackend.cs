using Microsoft.JSInterop;
using System;
using System.Collections.Generic;

namespace SpaceBurst
{
    internal sealed class BrowserLocalStorageBackend : IStorageBackend
    {
        private const string StorageDisplayPrefix = "localStorage://spaceburst/";
        private readonly IJSInProcessRuntime jsRuntime;

        public BrowserLocalStorageBackend(IJSInProcessRuntime jsRuntime)
        {
            this.jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        }

        public bool Exists(string logicalPath)
        {
            return jsRuntime.Invoke<bool>("spaceBurstHost.storageExists", Normalize(logicalPath));
        }

        public string ReadAllText(string logicalPath)
        {
            return jsRuntime.Invoke<string>("spaceBurstHost.storageRead", Normalize(logicalPath)) ?? string.Empty;
        }

        public void WriteAllText(string logicalPath, string contents)
        {
            jsRuntime.InvokeVoid("spaceBurstHost.storageWrite", Normalize(logicalPath), contents ?? string.Empty);
        }

        public void Delete(string logicalPath)
        {
            jsRuntime.InvokeVoid("spaceBurstHost.storageDelete", Normalize(logicalPath));
        }

        public IEnumerable<string> ListKeys(string prefix)
        {
            return jsRuntime.Invoke<string[]>("spaceBurstHost.storageList", Normalize(prefix)) ?? Array.Empty<string>();
        }

        public string GetDisplayPath(string logicalPath)
        {
            string normalized = Normalize(logicalPath);
            return normalized.Length == 0
                ? StorageDisplayPrefix.TrimEnd('/')
                : string.Concat(StorageDisplayPrefix, normalized);
        }

        private static string Normalize(string logicalPath)
        {
            if (string.IsNullOrWhiteSpace(logicalPath))
                return string.Empty;

            string[] parts = logicalPath
                .Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries);

            var safeParts = new List<string>(parts.Length);
            for (int index = 0; index < parts.Length; index++)
            {
                string current = parts[index].Trim();
                if (current.Length == 0 || current == "." || current == "..")
                    continue;

                safeParts.Add(current);
            }

            return string.Join("/", safeParts);
        }
    }
}
