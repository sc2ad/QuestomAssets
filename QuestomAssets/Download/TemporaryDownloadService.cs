using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace QuestomAssets.Download
{
    /// <summary>
    /// Downloads to a temporary file and ensures the file is not the same before copying it over to the target location
    /// </summary>
    public class TemporaryDownloadService : IDownloadService
    {
        private readonly WebClient _client;

        public TemporaryDownloadService(WebClient client)
        {
            _client = client;
        }

        public void DownloadFile(Uri uri, string pathToSave)
        {
            var fileName = pathToSave.GetFilenameFwdSlash();
            var tempPath = Path.GetTempPath().CombineFwdSlash(fileName);
            try
            {
                _client.DownloadFileTaskAsync(uri, tempPath).ContinueWith(t =>
                {
                    if (t.Exception != null || t.IsFaulted || t.IsCanceled)
                    {
                        return;
                    }
                    // Check actual file contents to ensure the temp file is more up to date than the old file
                    // This is slow... Only do this once every 5 minutes (?)
                    if (File.Exists(pathToSave))
                    {
                        var f1 = new FileInfo(pathToSave);
                        var f2 = new FileInfo(tempPath);
                        if (f1.Length != f2.Length)
                        {
                            File.Copy(tempPath, pathToSave, true);
                        }
                        // Compare hashes after ensuring size equality
                        using (var hash = HashAlgorithm.Create())
                        {
                            byte[] tempHash;
                            byte[] originalHash;
                            using (FileStream fs1 = f1.OpenRead(), fs2 = f2.OpenRead())
                            {
                                tempHash = hash.ComputeHash(fs2);
                                originalHash = hash.ComputeHash(fs1);
                            }
                            if (!tempHash.SequenceEqual(originalHash))
                            {
                                File.Copy(tempPath, pathToSave, true);
                            }
                        }
                    }
                    else
                    {
                        File.Copy(tempPath, pathToSave, true);
                    }
                });
            }
            catch (Exception)
            {
                // Handle?
            }
        }
    }
}