//----------------------------------------------------------------------------------------------------
// <copyright company="Avira Operations GmbH & Co. KG and its licensors">
// © 2016 Avira Operations GmbH & Co. KG and its licensors.  All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------------

using System;

namespace FileDownloader
{
    /// <summary>
    /// FileDownloader interface
    /// </summary>
    public interface IFileDownloader : IDisposable
    {
        /// <summary>
        /// Fired when download is finished, even if it's failed.
        /// </summary>
        event EventHandler<DownloadFileCompletedArgs> DownloadFileCompleted;

        /// <summary>
        /// Fired when download progress is changed.
        /// </summary>
        event EventHandler<DownloadFileProgressChangedArgs> DownloadProgressChanged;

        /// <summary>
        /// Gets or sets DNS Fallback Resolver instance. 
        /// </summary>
        IDnsFallbackResolver DnsFallbackResolver { get; set; }

        /// <summary>
        /// Gets or sets the delay between download attempts. 
        /// </summary>
        TimeSpan DelayBetweenAttempts { get; set; }

        /// <summary>
        /// Gets or sets the maximum waiting timeout for pending request to be finished. Default is 15 seconds.
        /// </summary>
        TimeSpan SafeWaitTimeout { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of download attempt.
        /// </summary>
        int MaxAttempts { get; set; }

        /// <summary>
        /// Gets the total bytes received so far
        /// </summary>
        long BytesReceived { get; }

        /// <summary>
        /// Gets the total bytes to receive
        /// </summary>
        long TotalBytesToReceive { get; }

        /// <summary>
        /// Start async download of source to destinationPath. destinationPath should be full path with file name.
        /// </summary>
        /// <param name="source">Source URI</param>
        /// <param name="destinationPath">Destination path</param>
        void DownloadFileAsync(Uri source, string destinationPath);

        /// <summary>
        /// Start download of source file to downloadDirectory. File would be saved with filename taken from server 
        /// </summary>
        /// <param name="source">Source URI</param>
        /// <param name="destinationDirectory">Destination directory</param>
        void DownloadFileAsyncPreserveServerFileName(Uri source, string destinationDirectory);

        /// <summary>
        /// Cancel current download
        /// </summary>
        void CancelDownloadAsync();
    }
}