using System;
using System.IO;
using System.Net;

namespace QuestomAssets.Download
{
    public interface IDownloadService
    {
        /// <summary>
        /// Downloads a file from the specified uri to the pathToSave
        /// </summary>
        /// <exception cref="IOException"></exception>
        /// <exception cref="WebException"></exception>
        /// <exception cref="UnauthorizedAccessException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        /// <exception cref="DirectoryNotFoundException"></exception>
        /// <param name="uri"></param>
        /// <param name="pathToSave"></param>
        void DownloadFile(Uri uri, string pathToSave);
    }
}