using System.IO;
using UnityEngine;

namespace RFIDBaggage.Utilities
{
    public static class StreamingAssetsPathUtility
    {
        public static bool TryBuildFilePath(string relativePath, out string fullPath)
        {
            fullPath = string.Empty;

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return false;
            }

            string normalizedRelativePath = NormalizeSeparators(relativePath.Trim());
            string streamingAssetsRoot = NormalizeSeparators(Application.streamingAssetsPath);

            if (Path.IsPathRooted(normalizedRelativePath))
            {
                if (!normalizedRelativePath.StartsWith(streamingAssetsRoot))
                {
                    return false;
                }

                fullPath = normalizedRelativePath;
                return true;
            }

            if (normalizedRelativePath.StartsWith("StreamingAssets/"))
            {
                normalizedRelativePath = normalizedRelativePath.Substring("StreamingAssets/".Length);
            }

            fullPath = NormalizeSeparators(Path.Combine(Application.streamingAssetsPath, normalizedRelativePath));
            return true;
        }

        public static bool TryBuildFileUri(string relativePath, out string fileUri)
        {
            fileUri = string.Empty;

            if (!TryBuildFilePath(relativePath, out string fullPath))
            {
                return false;
            }

            fileUri = new System.Uri(fullPath).AbsoluteUri;
            return true;
        }

        public static bool FileExists(string relativePath)
        {
            return TryBuildFilePath(relativePath, out string fullPath) && File.Exists(fullPath);
        }

        private static string NormalizeSeparators(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}
