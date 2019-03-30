//----------------------------------------------------------------------------------------------------
// <copyright company="Avira Operations GmbH & Co. KG and its licensors">
// © 2016 Avira Operations GmbH & Co. KG and its licensors.  All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------------

using System;
using System.IO;

namespace FileDownloader
{
    internal interface IStreamCopyWorker
    {
        event EventHandler<StreamCopyCompleteEventArgs> Completed;

        event EventHandler<StreamCopyProgressEventArgs> ProgressChanged;

        long Position { get; }

        void CopyAsync(Stream source, Stream destination, long sizeInBytes);

        void Cancel();
    }
}