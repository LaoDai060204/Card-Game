using System;
using System.IO;

namespace MDPro3.Net
{
    public static class NetUtil
    {

        public static bool IsValidUrl(string inputUrl)
        {
            if(string.IsNullOrEmpty(inputUrl))
                return false;

            if (!inputUrl.StartsWith("http://") && !inputUrl.StartsWith("https://"))
                inputUrl = "http://" + inputUrl;

            if (!Uri.TryCreate(inputUrl, UriKind.Absolute, out Uri uriResult))
                return false;

            if (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps)
                return false;

            return true;
        }

        public static bool IsValidDownloadUrl(string inputUrl, string[] supportExtensions)
        {
            return TryGetDownloadFileName(inputUrl, supportExtensions, out _);
        }

        public static bool TryGetDownloadFileName(string inputUrl, string[] supportExtensions, out string fileName)
        {
            fileName = null;
            if (supportExtensions == null
                || !Uri.TryCreate(inputUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return false;

            // A signed URL's query is not part of the Windows filename. Reject
            // escaped path separators and Windows-invalid names on every OS.
            var name = Uri.UnescapeDataString(Path.GetFileName(uri.AbsolutePath));
            if (string.IsNullOrWhiteSpace(name) || name.EndsWith(".", StringComparison.Ordinal)
                || name.EndsWith(" ", StringComparison.Ordinal)
                || name.IndexOfAny("<>:\"/\\|?*".ToCharArray()) >= 0)
                return false;
            foreach (var character in name)
                if (char.IsControl(character))
                    return false;

            var stem = name.Split('.')[0].ToUpperInvariant();
            if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL"
                || (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal)
                    || stem.StartsWith("LPT", StringComparison.Ordinal)) && stem[3] >= '1' && stem[3] <= '9'))
                return false;

            var extension = Path.GetExtension(name);
            foreach (var supported in supportExtensions)
                if (!string.IsNullOrEmpty(supported)
                    && string.Equals(extension, supported, StringComparison.OrdinalIgnoreCase))
                {
                    fileName = name;
                    return true;
                }
            return false;
        }

        public static bool TryCommitDownload(string temporaryPath, string targetPath)
        {
            try
            {
                // Both paths are in the same directory. Publish the completed
                // file atomically, retaining any installed pack if this fails.
                if (File.Exists(targetPath))
                    File.Replace(temporaryPath, targetPath, null);
                else
                    File.Move(temporaryPath, targetPath);
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return false;
            }
        }

        public static bool TrySaveDownload(byte[] data, string targetPath)
        {
            if (data == null || data.Length == 0)
                return false;
            var temporaryPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".download";
            try
            {
                var directory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllBytes(temporaryPath, data);
                return TryCommitDownload(temporaryPath, targetPath);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return false;
            }
            finally
            {
                DeleteTemporaryDownload(temporaryPath);
            }
        }

        public static void DeleteTemporaryDownload(string temporaryPath)
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                // A cleanup failure must not mask the result or leave the UI busy.
            }
        }

        public static bool IsExpectedRange(string contentRange, long start, long end, long total, long dataLength)
        {
            return start >= 0 && end >= start && total > end && dataLength == end - start + 1
                && string.Equals(contentRange?.Trim(), $"bytes {start}-{end}/{total}", StringComparison.OrdinalIgnoreCase);
        }

    }
}
