//----------------------------------------------------------------------------------------------------
// <copyright company="Avira Operations GmbH & Co. KG and its licensors">
// © 2016 Avira Operations GmbH & Co. KG and its licensors.  All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------------

using System;

namespace FileDownloader
{
    internal class StreamCopyProgressEventArgs : EventArgs
    {
        public long BytesReceived { get; set; }
    }
}