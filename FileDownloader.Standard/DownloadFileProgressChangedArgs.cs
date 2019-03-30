//----------------------------------------------------------------------------------------------------
// <copyright company="Avira Operations GmbH & Co. KG and its licensors">
// © 2016 Avira Operations GmbH & Co. KG and its licensors.  All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------------

using System.ComponentModel;

namespace FileDownloader
{
    /// <summary>
    /// DownloadFileProgressChanged event args
    /// </summary>
    public class DownloadFileProgressChangedArgs : ProgressChangedEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DownloadFileProgressChangedArgs" /> class.
        /// </summary>
        /// <param name="progressPercentage">Progress percentage</param>
        /// <param name="bytesReceived">Bytes received so far</param>
        /// <param name="totalBytesToReceive">Total bytes to receive</param>
        public DownloadFileProgressChangedArgs(int progressPercentage, long bytesReceived, long totalBytesToReceive)
            : base(progressPercentage, null)
        {
            BytesReceived = bytesReceived;
            TotalBytesToReceive = totalBytesToReceive;
        }

        /// <summary>
        /// Gets the bytes received so far
        /// </summary>
        public long BytesReceived { get; private set; }

        /// <summary>
        /// Gets the total bytes to receive
        /// </summary>
        public long TotalBytesToReceive { get; private set; }
    }
}