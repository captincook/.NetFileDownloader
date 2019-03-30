//----------------------------------------------------------------------------------------------------
// <copyright company="Avira Operations GmbH & Co KG.">
// This file contains trade secrets of Avira Operations GmbH & Co KG. No part may be reproduced
// or transmitted in any form by any means or for any purpose without the express written permission
// of Avira Operations GmbH & Co KG.</copyright>
//----------------------------------------------------------------------------------------------------

using System;
using System.ComponentModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FileDownloader.UnitTest
{
    [TestClass]
    public class DownloadFileTest
    {
        [TestMethod]
        public void Download_file_raise_download_completed_event_custom_defined_download_completed_event_raised_correctly()
        {
            var downloadFileSucceeded = false;
            var downloadFile = new DownloadFileAccess();
            downloadFile.DownloadFileCompleted += (o, e) => downloadFileSucceeded = e.State == CompletedState.Succeeded;

            downloadFile.RaiseDownloadFileCompletedEvent(null, false, null);

            Assert.IsTrue(downloadFileSucceeded, "DownloadFileCompleted event not triggered correctly!");
        }

        [TestMethod]
        public void Download_file_raise_download_completed_event_custom_defined_download_file_cancelled_event_raised_correctly()
        {
            var downloadFileCanceled = false;
            var downloadFile = new DownloadFileAccess();
            downloadFile.DownloadFileCompleted += (o, e) => downloadFileCanceled = e.State == CompletedState.Canceled;

            downloadFile.RaiseDownloadFileCompletedEvent(null, true, null);

            Assert.IsTrue(downloadFileCanceled, "DownloadFileCancelled event not triggered correctly!");
        }

        [TestMethod]
        public void Download_file_raise_download_completed_event_custom_defined_download_file_error_event_raised_correctly()
        {
            var downloadFileFailed = false;
            var downloadFile = new DownloadFileAccess(0);
            downloadFile.DownloadFileCompleted += (o, e) => downloadFileFailed = e.State == CompletedState.Failed;

            downloadFile.RaiseDownloadFileCompletedEvent(new Exception(), false, null);

            Assert.IsTrue(downloadFileFailed, "DownloadFileError event not triggered correctly!");
        }

        [TestMethod]
        public void Download_file_completed_event_has_downdloadProgress()
        {
            DownloadFileCompletedArgs downloadFileCompletedArgs = null;
            var downloadFile = new DownloadFileAccess();
            downloadFile.BytesReceived = 5000;
            downloadFile.TotalBytesToReceive = 10000;
            downloadFile.DownloadFileCompleted += (o, e) => downloadFileCompletedArgs = e;

            downloadFile.RaiseDownloadFileCompletedEvent(null, false, null);

            Assert.IsNotNull(downloadFileCompletedArgs);
            Assert.AreEqual(50, downloadFileCompletedArgs.DownloadProgress);
        }

        [TestMethod]
        public void Download_file_completed_event_has_downloadSpeed()
        {
            DownloadFileCompletedArgs downloadFileCompletedArgs = null;
            var downloadFile = new DownloadFileAccess();
            downloadFile.DownloadFileCompleted += (o, e) => downloadFileCompletedArgs = e;

            downloadFile.BytesReceived = 1024 * 120;
            downloadFile.DownloadStartTime = DateTime.Now.AddSeconds(-60.0);

            downloadFile.RaiseDownloadFileCompletedEvent(null, false, null);

            Assert.IsNotNull(downloadFileCompletedArgs);
            Assert.AreEqual(2, downloadFileCompletedArgs.DownloadSpeedInKiloBytesPerSecond);
        }

        [TestMethod]
        public void Download_file_completed_and_nothing_is_downloaded_downloadSpeedIsZero()
        {
            DownloadFileCompletedArgs downloadFileCompletedArgs = null;
            var downloadFile = new DownloadFileAccess();
            downloadFile.DownloadFileCompleted += (o, e) => downloadFileCompletedArgs = e;

            downloadFile.BytesReceived = 0;
            downloadFile.TotalBytesToReceive = 10000;
            downloadFile.DownloadStartTime = DateTime.Now;

            downloadFile.RaiseDownloadFileCompletedEvent(null, false, null);

            Assert.IsNotNull(downloadFileCompletedArgs);
            Assert.AreEqual(0, downloadFileCompletedArgs.DownloadSpeedInKiloBytesPerSecond);
        }

        private class DownloadFileAccess : FileDownloader
        {
            public DownloadFileAccess()
            {
            }

            public DownloadFileAccess(int maxAttempts)
            {
                MaxAttempts = maxAttempts;
            }

            public void RaiseDownloadFileCompletedEvent(Exception error, bool cancelled, object userState)
            {
                OnDownloadCompleted(new DownloadWebClient(), new AsyncCompletedEventArgs(error, cancelled, userState));
            }
        }
    }
}
