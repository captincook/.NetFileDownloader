//----------------------------------------------------------------------------------------------------
// <copyright company="Avira Operations GmbH & Co. KG and its licensors">
// © 2016 Avira Operations GmbH & Co. KG and its licensors.  All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------------

namespace FileDownloader
{
    /// <summary>
    /// Downloaded completed states
    /// </summary>
    public enum CompletedState
    {
        /// <summary>
        /// Download successful
        /// </summary>
        Succeeded,

        /// <summary>
        /// Download canceled
        /// </summary>
        Canceled,

        /// <summary>
        /// Download failed
        /// </summary>
        Failed
    }
}