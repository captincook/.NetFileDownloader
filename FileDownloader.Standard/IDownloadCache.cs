//----------------------------------------------------------------------------------------------------
// <copyright company="Avira Operations GmbH & Co. KG and its licensors">
// © 2016 Avira Operations GmbH & Co. KG and its licensors.  All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------------

using System;
using System.Net;

namespace FileDownloader
{
    /// <summary>
    /// IDownloadCache interface
    /// </summary>
    public interface IDownloadCache
    {
        /// <summary>
        /// Invalidate cache for specific url
        /// </summary>
        /// <param name="uri">URI to invalidate</param>
        void Invalidate(Uri uri);

        /// <summary>
        /// Add new cache record
        /// </summary>
        /// <param name="uri">Source URI</param>
        /// <param name="path">Downloaded file path</param>
        /// <param name="headers">HTTP headers of the response</param>
        void Add(Uri uri, string path, WebHeaderCollection headers);

        /// <summary>
        /// Get the file from cache. Return file name if file is found in cache, null otherwise 
        /// </summary>
        /// <param name="uri">Source uri</param>
        /// <param name="headers">HTTP headers of the response</param>
        /// <returns>Path to file with cached resource</returns>
        string Get(Uri uri, WebHeaderCollection headers);
    }
}