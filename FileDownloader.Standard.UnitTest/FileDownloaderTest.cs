﻿//----------------------------------------------------------------------------------------------------
// <copyright company="Avira Operations GmbH & Co. KG and its licensors">
// © 2016 Avira Operations GmbH & Co. KG and its licensors.  All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FileDownloader.UnitTest
{
    [TestClass]
    public class FileDownloaderTest : IDisposable
    {
        private readonly SomeFileDownloader fileDownloader = new SomeFileDownloader();
        private string tempDownloadFolderPath;
        private bool disposed;

        [TestCleanup]
        public void TestCleanup()
        {
            if (!string.IsNullOrEmpty(this.tempDownloadFolderPath) && Directory.Exists(this.tempDownloadFolderPath))
            {
                Directory.Delete(this.tempDownloadFolderPath, true);
            }

            this.tempDownloadFolderPath = null;
        }

        [TestMethod]
        public void ApplyNewFileName_OriginalFileNameIsNull_ReturnsDownloadedFilePath()
        {
            var fileName = this.fileDownloader.ApplyNewFileName(null, "c:\\DownloadPath.exe");

            Assert.AreEqual("c:\\DownloadPath.exe", fileName);
        }

        [TestMethod]
        public void ApplyNewFileName_OriginalFileNameIsEqualToDownloaded_ReturnsDownloadedFilePath()
        {
            var fileName = this.fileDownloader.ApplyNewFileName("DownloadPath.exe", "c:\\DownloadPath.exe");

            Assert.AreEqual("c:\\DownloadPath.exe", fileName);
        }

        [TestMethod]
        public void ApplyNewFileName_OriginalFileNameIsNotEqualToDownloaded_ReturnsOriginalFileNamePath()
        {
            var downloadedFilePath = CreateDownloadedFile("SomeFile.exe");
            var filePath = this.fileDownloader.ApplyNewFileName("OriginalFile.exe", downloadedFilePath);

            var expectedOriginalPath = Path.Combine(this.tempDownloadFolderPath, "OriginalFile.exe");
            Assert.AreEqual(expectedOriginalPath, filePath);
            Assert.IsTrue(File.Exists(filePath));
        }

        [TestMethod]
        public void ApplyNewFileName_OriginalFileNameAlreadyExistsInDownloadFolder_OverwritesExistingAndReturnsOriginalFileNamePath()
        {
            CreateDownloadedFile("OriginalFile.exe");
            var downloadedFilePath = CreateDownloadedFile("SomeFile.exe");

            var filePath = this.fileDownloader.ApplyNewFileName("OriginalFile.exe", downloadedFilePath);

            var expectedOriginalPath = Path.Combine(this.tempDownloadFolderPath, "OriginalFile.exe");
            Assert.AreEqual(expectedOriginalPath, filePath);
            Assert.IsTrue(File.Exists(filePath));
        }

        [TestMethod]
        public void ApplyNewFileName_OriginalFileNameAlreadyExistsInDownloadFolder_ReturnOriginalFileNameInNewTempSubFolder()
        {
            using (LockFile(CreateDownloadedFile("OriginalFile.msi")))
            {
                var downloadedFilePath = CreateDownloadedFile("SomeFile.exe");

                var filePath = this.fileDownloader.ApplyNewFileName("OriginalFile.msi", downloadedFilePath);

                Assert.AreEqual("OriginalFile.msi", Path.GetFileName(filePath));
                Assert.IsTrue(filePath.StartsWith(Path.GetDirectoryName(downloadedFilePath)));
                Assert.IsTrue(File.Exists(filePath));
            }
        }

        private FileStream LockFile(string path)
        {
            return File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }

        private string CreateDownloadedFile(string fileName)
        {
            if (this.tempDownloadFolderPath == null)
            {
                this.tempDownloadFolderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(this.tempDownloadFolderPath);
            }

            var filePath = Path.Combine(this.tempDownloadFolderPath, fileName);
            File.WriteAllText(filePath, string.Empty);

            return filePath;
        }

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    if (this.fileDownloader != null)
                    {
                        this.fileDownloader.Dispose();
                    }
                }

                this.disposed = true;
            }
        }
        #endregion IDisposable

        public class SomeFileDownloader : FileDownloader
        {
            public new string ApplyNewFileName(string originalFileName, string downloadedFilePath)
            {
                return base.ApplyNewFileName(downloadedFilePath, originalFileName);
            }
        }
    }
}
