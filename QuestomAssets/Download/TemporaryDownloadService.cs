using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace QuestomAssets.Download
{
    /// <summary>
    /// Downloads to a temporary file and ensures the file is not the same before copying it over to the target location
    /// </summary>
    public class TemporaryDownloadService : IDownloadService
    {
        private readonly HttpClient _client;

        public TemporaryDownloadService(HttpClient client)
        {
            _client = client;
        }

        public void DownloadFile(Uri uri, string pathToSave)
        {
            var fileName = pathToSave.GetFilenameFwdSlash();
            var tempPath = Path.GetTempPath().CombineFwdSlash(fileName);
            Log.LogMsg($"Attempting to download: {uri} to tempPath: {tempPath}");
            try
            {
                var t = _client.GetStreamAsync(uri).ContinueWith(async task =>
                {
                    if (task.Exception != null || task.IsFaulted || task.IsCanceled)
                    {
                        return;
                    }
                    using (var s = File.OpenWrite(tempPath))
                        await task.Result?.CopyToAsync(s);
                    if (File.Exists(pathToSave))
                    {
                        var f1 = new FileInfo(pathToSave);
                        if (f1.Length == task.Result?.Length)
                        {
                            using (var hash = HashAlgorithm.Create())
                            {
                                byte[] tempHash;
                                byte[] originalHash;
                                using (FileStream fs1 = f1.OpenRead())
                                {
                                    tempHash = hash.ComputeHash(task.Result);
                                    originalHash = hash.ComputeHash(fs1);
                                }
                                if (tempHash.SequenceEqual(originalHash))
                                {
                                    return;
                                }
                            }
                        }
                    }
                    using (var s = File.OpenWrite(pathToSave))
                        await task.Result?.CopyToAsync(s);
                    // Check actual file contents to ensure the temp file is more up to date than the old file
                    // This is slow... Only do this once every 5 minutes (?)
                });
                t.Wait();
            }
            catch (Exception)
            {
                // Handle?
            }
        }
    }
}