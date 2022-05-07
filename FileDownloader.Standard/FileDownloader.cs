//----------------------------------------------------------------------------------------------------
// <copyright company="Avira Operations GmbH & Co. KG and its licensors">
// © 2016 Avira Operations GmbH & Co. KG and its licensors.  All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------------

using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Threading;
using FileDownloader.Logging;

namespace FileDownloader
{
    /// <summary>
    /// Class used for downloading files. The .NET WebClient is used for downloading.
    /// </summary>
    public class FileDownloader : IFileDownloader
    {
        private readonly IDownloadCache downloadCache;
        private readonly ILogger logger = LoggerFacade.GetCurrentClassLogger();
        private readonly ManualResetEvent readyToDownload = new ManualResetEvent(true);
        private readonly System.Timers.Timer attemptTimer = new System.Timers.Timer();
        private readonly object cancelSync = new object();

        private bool isCancelled;
        private bool disposed;
        private bool useFileNameFromServer = true;
        private bool isFallback;

        private int attemptNumber;

        private string localFileName;
        private string destinationFileName;
        private string destinationFolder;

        private Uri fileSource;
        private StreamCopyWorker worker;
        private DownloadWebClient downloadWebClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileDownloader"/> class. No download cache would be used, resume is not supported
        /// </summary>
        public FileDownloader()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileDownloader"/> class.
        /// </summary>
        /// <param name="downloadCache">IDownloadCache instance</param>
        public FileDownloader(IDownloadCache downloadCache)
        {
            DnsFallbackResolver = null;

            MaxAttempts = 60;
            DelayBetweenAttempts = TimeSpan.FromSeconds(3);
            SafeWaitTimeout = TimeSpan.FromSeconds(15);
            SourceStreamReadTimeout = TimeSpan.FromSeconds(5);

            this.downloadCache = downloadCache;
            this.disposed = false;

            this.attemptTimer.Elapsed += OnDownloadAttemptTimer;
        }

        /// <summary>
        /// Fired when download is finished, even if it's failed.
        /// </summary>
        public event EventHandler<DownloadFileCompletedArgs> DownloadFileCompleted;

        /// <summary>
        /// Fired when download progress is changed.
        /// </summary>
        public event EventHandler<DownloadFileProgressChangedArgs> DownloadProgressChanged;

        /// <summary>
        /// Gets or sets the DNS fallback resolver. Default is null.
        /// </summary>
        public IDnsFallbackResolver DnsFallbackResolver { get; set; }

        /// <summary>
        /// Gets or sets the delay between download attempts. Default is 3 seconds. 
        /// </summary>
        public TimeSpan DelayBetweenAttempts { get; set; }

        /// <summary>
        /// Gets or sets the maximum waiting timeout for pending request to be finished. Default is 15 seconds.
        /// </summary>
        public TimeSpan SafeWaitTimeout { get; set; }

        /// <summary>
        /// Gets or sets the timeout for source stream. Default is 5 seconds.
        /// </summary>
        public TimeSpan SourceStreamReadTimeout { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of download attempts. Default is 60.
        /// </summary>
        public int MaxAttempts { get; set; }

        /// <summary>
        /// Gets the total bytes received so far
        /// </summary>
        public long BytesReceived { get; internal set; }

        /// <summary>
        /// Gets the total bytes to receive
        /// </summary>
        public long TotalBytesToReceive { get; internal set; }

        /// <summary>
        /// Gets or sets the time when download was started
        /// </summary>
        public DateTime DownloadStartTime { get; set; }

        private bool UseCaching
        {
            get
            {
                return this.downloadCache != null;
            }
        }

        /// <summary>
        /// Start async download of source to destinationPath
        /// </summary>
        /// <param name="source">Source URI</param>
        /// <param name="destinationPath">Full path with file name.</param>
        public void DownloadFileAsync(Uri source, string destinationPath)
        {
            DownloadFileAsync(source, destinationPath, false);
        }

        /// <summary>
        /// Start download of source file to downloadDirectory. File would be saved with filename taken from server 
        /// </summary>
        /// <param name="source">Source URI</param>
        /// <param name="destinationDirectory">Destination directory</param>
        public void DownloadFileAsyncPreserveServerFileName(Uri source, string destinationDirectory)
        {
            DownloadFileAsync(source, Path.Combine(destinationDirectory, Guid.NewGuid().ToString()), true);
        }

        /// <summary>
        /// Cancel current download
        /// </summary>
        public void CancelDownloadAsync()
        {
            lock (this.cancelSync)
            {
                if (this.isCancelled)
                {
                    return;
                }
                this.isCancelled = true;
            }

            this.logger.Debug("CancelDownloadAsync called.");
            if (this.worker != null)
            {
                this.worker.Cancel();
            }

            TriggerDownloadWebClientCancelAsync();
            DeleteDownloadedFile();  ////todo: maybe this is equal to InvalidateCache? Can we get rid of DeleteDownloadedFile ?

            this.readyToDownload.Set();
        }

        private void DeleteDownloadedFile()
        {
            FileHelpers.TryFileDelete(this.localFileName);
        }

        private void InvalidateCache(Uri uri)
        {
            if (!UseCaching)
            {
                return;
            }

            this.downloadCache.Invalidate(uri);
            this.logger.Debug("Cached resource was invalidated: {0}", uri);
        }

        private void DownloadFileAsync(Uri source, string destinationPath, bool useServerFileName)
        {
            if (!WaitSafeStart())
            {
                throw new Exception("Unable to start download because another request is still in progress.");
            }

            this.logger.Debug("DownloadFileAsync({0}, {1}) is called.", source, destinationPath);

            this.useFileNameFromServer = useServerFileName;
            this.fileSource = source;
            BytesReceived = 0;
            this.destinationFileName = destinationPath;
            this.destinationFolder = Path.GetDirectoryName(destinationPath);
            this.isCancelled = false;
            this.localFileName = string.Empty;

            DownloadStartTime = DateTime.Now;

            this.attemptNumber = 0;

            StartDownload();
        }

        private void OnDownloadAttemptTimer(object sender, EventArgs eventArgs)
        {
            StartDownload();
        }

        private void StartDownload()
        {
            if (IsCancelled())
            {
                return;
            }

            this.logger.Debug("FileDownloader attempt {0} of {1}.", this.attemptNumber, MaxAttempts);

            this.localFileName = ComposeLocalFilename();

            if (!UseCaching)
            {
                TriggerWebClientDownloadFileAsync();
                return;
            }

            TotalBytesToReceive = -1;
            var headers = GetHttpHeaders(this.fileSource);
            if (headers != null)
            {
                TotalBytesToReceive = headers.GetContentLength();
            }

            if (TotalBytesToReceive == -1)
            {
                TotalBytesToReceive = 0;
                this.logger.Warn("Received no Content-Length header from server for {0}. Cache is not used, Resume is not supported", this.fileSource);
                TriggerWebClientDownloadFileAsync();
            }
            else
            {
                ResumeDownload(headers);
            }
        }

        private void ResumeDownload(WebHeaderCollection headers)
        {
            this.isFallback = false;

            string downloadedFileName = GetDestinationFileName(headers);

            long downloadedFileSize;
            if (!FileHelpers.TryGetFileSize(downloadedFileName, out downloadedFileSize))
            {
                ////todo: handle this case in future. Now in case of error we simply proceed with downloadedFileSize=0
            }

            if (UseCaching)
            {
                this.downloadCache.Add(this.fileSource, this.localFileName, headers);
            }

            if (downloadedFileSize > TotalBytesToReceive)
            {
                InvalidateCache(this.fileSource);
            }

            if (downloadedFileSize != TotalBytesToReceive)
            {
                if (!FileHelpers.ReplaceFile(downloadedFileName, this.localFileName))
                {
                    InvalidateCache(this.fileSource);
                }

                Download(this.fileSource, this.localFileName, TotalBytesToReceive);
            }
            else
            {
                DownloadFromCache(downloadedFileName);
            }
        }

        private void DownloadFromCache(string cachedResource)
        {
            this.logger.Debug("Taking file from cache.");
            OnDownloadProgressChanged(this, new DownloadFileProgressChangedArgs(100, TotalBytesToReceive, TotalBytesToReceive));
            InvokeDownloadCompleted(CompletedState.Succeeded, cachedResource, null, true);
            this.readyToDownload.Set();
        }

        private void TriggerWebClientDownloadFileAsync()
        {
            this.logger.Debug("Falling back to legacy DownloadFileAsync.");
            try
            {
                this.isFallback = true;
                var destinationDirectory = Path.GetDirectoryName(this.localFileName);
                if (destinationDirectory != null && !Directory.Exists(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }
                TryCleanupExistingDownloadWebClient();

                this.downloadWebClient = CreateWebClient();
                this.downloadWebClient.DownloadFileAsync(this.fileSource, this.localFileName);
                this.logger.Debug("Download async started. Source: {0} Destination: {1}", this.fileSource, this.localFileName);
            }
            catch (Exception ex)
            {
                this.logger.Warn("Failed to download Source:{0}, Destination:{1}, Error:{2}.", this.fileSource, this.localFileName, ex.Message);
                if (!AttemptDownload())
                {
                    InvokeDownloadCompleted(CompletedState.Failed, this.localFileName, ex);
                }
            }
        }

        private DownloadWebClient CreateWebClient()
        {
            var webClient = new DownloadWebClient();
            webClient.DownloadFileCompleted += OnDownloadCompleted;
            webClient.DownloadProgressChanged += OnDownloadProgressChanged;
            webClient.OpenReadCompleted += OnOpenReadCompleted;
            return webClient;
        }

        private void TryCleanupExistingDownloadWebClient()
        {
            if (this.downloadWebClient == null)
            {
                return;
            }
            try
            {
                lock (this)
                {
                    if (this.downloadWebClient != null)
                    {
                        this.downloadWebClient.DownloadFileCompleted -= OnDownloadCompleted;
                        this.downloadWebClient.DownloadProgressChanged -= OnDownloadProgressChanged;
                        this.downloadWebClient.OpenReadCompleted -= OnOpenReadCompleted;
                        this.downloadWebClient.CancelAsync();
                        this.downloadWebClient.Dispose();
                        this.downloadWebClient = null;
                    }
                }
            }
            catch (Exception e)
            {
                this.logger.Warn("Error while cleaning up web client : {0}", e.Message);
            }
        }

        private bool AttemptDownload()
        {
            if (++this.attemptNumber <= MaxAttempts)
            {
                this.attemptTimer.Interval = DelayBetweenAttempts.TotalMilliseconds;
                this.attemptTimer.AutoReset = false;
                this.attemptTimer.Start();
                this.logger.Debug("Downloader scheduled next attempt in {0} seconds.", DelayBetweenAttempts.TotalSeconds);
                return true;
            }

            readyToDownload.Set();
            return false;
        }

        private string GetDestinationFileName(WebHeaderCollection headers)
        {
            if (!UseCaching)
            {
                this.logger.Debug("Not using cache. Source: {0} Destination: {1}", this.fileSource, this.localFileName);
                return this.localFileName;
            }

            var cachedDestinationPath = this.downloadCache.Get(this.fileSource, headers);
            if (cachedDestinationPath == null)
            {
                this.logger.Debug("No cache item found. Source: {0} Destination: {1}", this.fileSource, this.localFileName);
                DeleteDownloadedFile();
                return this.localFileName;
            }

            this.logger.Debug("Download resource was found in cache. Source: {0} Destination: {1}", this.fileSource, cachedDestinationPath);
            return cachedDestinationPath;
        }

        private string ComposeLocalFilename()
        {
            if (this.useFileNameFromServer)
            {
                return Path.Combine(this.destinationFolder, string.Format("{0}.tmp", Guid.NewGuid()));
            }
            return Path.Combine(this.destinationFolder, this.destinationFileName);
        }

        private void Download(Uri source, string fileDestination, long totalBytesToReceive)
        {
            try
            {
                long seekPosition;
                FileHelpers.TryGetFileSize(fileDestination, out seekPosition);

                TryCleanupExistingDownloadWebClient();
                this.downloadWebClient = CreateWebClient();
                this.downloadWebClient.OpenReadAsync(source, seekPosition);
                this.logger.Debug("Download started. Source: {0} Destination: {1} Size: {2}", source, fileDestination, totalBytesToReceive);
            }
            catch (Exception e)
            {
                this.logger.Debug("Download failed: {0}", e.Message);
                if (!AttemptDownload())
                {
                    InvokeDownloadCompleted(CompletedState.Failed, this.localFileName, e);
                }
            }
        }

        private WebHeaderCollection GetHttpHeaders(Uri source)
        {
            try
            {
                var webRequest = WebRequest.Create(source);
                webRequest.Method = WebRequestMethods.Http.Head;

                using (var webResponse = webRequest.GetResponse())
                {
                    return webResponse.Headers;
                }
            }
            catch (Exception e)
            {
                this.logger.Warn("Unable to read http headers for {0}: {1}; typeof(Exception)={2}", source, e.Message, e.GetType());
                return null;
            }
        }

        private void OnDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs args)
        {
            var e = new DownloadFileProgressChangedArgs(args.ProgressPercentage, args.BytesReceived, args.TotalBytesToReceive);

            OnDownloadProgressChanged(sender, e);
        }

        private void OnDownloadProgressChanged(object sender, DownloadFileProgressChangedArgs args)
        {
            if (BytesReceived < args.BytesReceived)
            {
                ////bytes growing? we have connection!
                this.attemptNumber = 1;
            }

            BytesReceived = args.BytesReceived;
            TotalBytesToReceive = args.TotalBytesToReceive;

            DownloadProgressChanged.SafeInvoke(sender, args);
        }

        private void InvokeDownloadCompleted(CompletedState downloadCompletedState, string fileName, Exception error = null, bool fromCache = false)
        {
            var downloadTime = fromCache ? TimeSpan.Zero : DateTime.Now.Subtract(DownloadStartTime);
            if (this.worker != null)
            {
                BytesReceived = this.worker.Position;
            }

            DownloadFileCompleted.SafeInvoke(this, new DownloadFileCompletedArgs(downloadCompletedState, fileName, this.fileSource, downloadTime, TotalBytesToReceive, BytesReceived, error));
        }

        private void OnOpenReadCompleted(object sender, OpenReadCompletedEventArgs args)
        {
            var webClient = sender as DownloadWebClient;
            if (webClient == null)
            {
                this.logger.Warn("Wrong sender in OnOpenReadCompleted: Actual:{0} Expected:{1}", sender.GetType(), typeof(DownloadWebClient));
                return;
            }

            lock (this.cancelSync)
            {
                if (this.isCancelled)
                {
                    this.logger.Debug("Download was cancelled.");
                    return;
                }

                if (!webClient.HasResponse)
                {
                    this.logger.Debug("DownloadWebClient returned no response.");
                    TriggerWebClientDownloadFileAsync();
                    return;
                }

                bool appendExistingChunk = webClient.IsPartialResponse;
                var destinationStream = CreateDestinationStream(appendExistingChunk);
                if (destinationStream != null)
                {
                    TrySetStreamReadTimeout(args.Result, (int)SourceStreamReadTimeout.TotalMilliseconds);

                    this.worker = new StreamCopyWorker();
                    this.worker.Completed += OnWorkerCompleted;
                    this.worker.ProgressChanged += OnWorkerProgressChanged;
                    this.worker.CopyAsync(args.Result, destinationStream, TotalBytesToReceive);
                }
            }
        }

        private bool TrySetStreamReadTimeout(Stream stream, int timeout)
        {
            try
            {
                stream.ReadTimeout = timeout;
                return true;
            }
            catch (Exception e)
            {
                this.logger.Warn("Unable to set read timeout for source stream {0}", e.Message);
                return false;
            }
        }

        private Stream CreateDestinationStream(bool append)
        {
            FileStream destinationStream = null;
            try
            {
                var destinationDirectory = Path.GetDirectoryName(this.localFileName);
                if (destinationDirectory != null && !Directory.Exists(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                destinationStream = new FileStream(this.localFileName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
                if (append)
                {
                    destinationStream.Seek(0, SeekOrigin.End);
                }
                else
                {
                    destinationStream.SetLength(0);
                }
            }
            catch (Exception ex)
            {
                if (destinationStream != null)
                {
                    destinationStream.Dispose();
                    destinationStream = null;
                }
                OnDownloadCompleted(this.downloadWebClient, new AsyncCompletedEventArgs(ex, false, null));
            }
            return destinationStream;
        }

        private void OnWorkerProgressChanged(object sender, StreamCopyProgressEventArgs eventArgs)
        {
            if (this.isCancelled)
            {
                return;
            }

            if (TotalBytesToReceive == 0)
            {
                return;
            }
            var progress = eventArgs.BytesReceived / TotalBytesToReceive;
            var progressPercentage = (int)(progress * 100);

            OnDownloadProgressChanged(this, new DownloadFileProgressChangedArgs(progressPercentage, eventArgs.BytesReceived, TotalBytesToReceive));
        }

        private void OnWorkerCompleted(object sender, StreamCopyCompleteEventArgs eventArgs)
        {
            try
            {
                OnDownloadCompleted(this.downloadWebClient, new AsyncCompletedEventArgs(eventArgs.Exception, eventArgs.CompleteState == CompletedState.Canceled, null));
            }
            finally
            {
                this.worker.ProgressChanged -= OnWorkerProgressChanged;
                this.worker.Completed -= OnWorkerCompleted;
                this.worker.Dispose();
            }
        }

        /// <summary>
        /// OnDownloadCompleted event handler
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="args">AsyncCompletedEventArgs instance</param>
        protected void OnDownloadCompleted(object sender, AsyncCompletedEventArgs args)
        {
            var webClient = sender as DownloadWebClient;
            if (webClient == null)
            {
                this.logger.Warn("Wrong sender in OnDownloadCompleted: Actual:{0} Expected:{1}", sender.GetType(), typeof(DownloadWebClient));
                InvokeDownloadCompleted(CompletedState.Failed, this.localFileName);
                return;
            }

            if (args.Cancelled)
            {
                this.logger.Debug("Download cancelled. Source: {0} Destination: {1}", this.fileSource, this.localFileName);
                DeleteDownloadedFile();

                InvokeDownloadCompleted(CompletedState.Canceled, this.localFileName);
                this.readyToDownload.Set();
            }
            else if (args.Error != null)
            {
                if (this.isFallback)
                {
                    DeleteDownloadedFile();
                }

                ////We may have NameResolutionFailure on internet connectivity problem.
                ////We don't use DnsFallbackResolver if we successfully started downloading, and then got internet problem.
                ////If we change [this.fileSource] here - we lose downloaded chunk in Cache (i.e. we create a new Cache item for new [this.fileSource]
                if (this.attemptNumber == 1 && DnsFallbackResolver != null && IsNameResolutionFailure(args.Error))
                {
                    var newFileSource = DnsFallbackResolver.Resolve(this.fileSource);
                    if (newFileSource != null)
                    {
                        this.fileSource = newFileSource;
                        this.logger.Debug("Download failed in case of DNS resolve error. Retry downloading with new source: {0}.", this.fileSource);
                        AttemptDownload();
                        return;
                    }
                }

                this.logger.Debug("Download failed. Source: {0} Destination: {1} Error: {2}", this.fileSource, this.localFileName, args.Error);

                if (!AttemptDownload())
                {
                    InvokeDownloadCompleted(CompletedState.Failed, null, args.Error);
                    this.readyToDownload.Set();
                }
            }
            else
            {
                if (this.useFileNameFromServer)
                {
                    this.localFileName = ApplyNewFileName(this.localFileName, webClient.GetOriginalFileNameFromDownload());
                }

                this.logger.Debug("Download completed. Source: {0} Destination: {1}", this.fileSource, this.localFileName);
                if (UseCaching)
                {
                    this.downloadCache.Add(this.fileSource, this.localFileName, webClient.ResponseHeaders);
                }

                ////we may have the destination file not immediately closed after downloading
                WaitFileClosed(this.localFileName, TimeSpan.FromSeconds(3));

                InvokeDownloadCompleted(CompletedState.Succeeded, this.localFileName, null);
                this.readyToDownload.Set();
            }
        }

        /// <summary>
        /// Rename oldFilePath to newFileName , placing file in same folder or in temporary folder if renaming failed. 
        /// </summary>
        /// <param name="oldFilePath">Full path and name of the file to be renamed</param>
        /// <param name="newFileName">New file name</param>
        /// <returns>Full path to renamed file</returns>
        protected virtual string ApplyNewFileName(string oldFilePath, string newFileName)
        {
            var downloadedFileName = Path.GetFileName(oldFilePath);
            var downloadDirectory = Path.GetDirectoryName(oldFilePath);

            if (newFileName == null || newFileName == downloadedFileName || downloadDirectory == null)
            {
                return oldFilePath;
            }

            var newFilePath = Path.Combine(downloadDirectory, newFileName);

            if (File.Exists(newFilePath))
            {
                try
                {
                    File.Delete(newFilePath);
                }
                catch (Exception)
                {
                    newFilePath = Path.Combine(CreateTempFolder(downloadDirectory), newFileName);
                }
            }

            if (newFilePath == oldFilePath)
            {
                return oldFilePath;
            }

            File.Move(oldFilePath, newFilePath);
            return newFilePath;
        }

        private void TriggerDownloadWebClientCancelAsync()
        {
            if (this.downloadWebClient != null)
            {
                this.downloadWebClient.CancelAsync();
                this.downloadWebClient.OpenReadCompleted -= OnOpenReadCompleted;
                this.logger.Debug("Successfully cancelled web client.");
            }
        }

        private string CreateTempFolder(string rootFolderPath)
        {
            while (true)
            {
                var folderPath = Path.Combine(rootFolderPath, Path.GetRandomFileName());
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                    return folderPath;
                }
            }
        }

        private bool IsNameResolutionFailure(Exception exception)
        {
            var webException = exception as WebException;
            return webException != null && webException.Status == WebExceptionStatus.NameResolutionFailure;
        }

        private bool WaitSafeStart()
        {
            this.logger.Debug("Calling DownloadFileAsync...");
            if (!this.readyToDownload.WaitOne(SafeWaitTimeout))
            {
                this.logger.Warn("Failed to call DownloadFileAsync, another request is in progress: Source:{0}, Destination:{1}", this.fileSource, this.localFileName);
                return false;
            }
            this.readyToDownload.Reset();
            return true;
        }

        private void WaitFileClosed(string fileName, TimeSpan waitTimeout)
        {
            var waitCounter = TimeSpan.Zero;
            while (waitCounter < waitTimeout)
            {
                try
                {
                    var fileHandle = File.Open(fileName, FileMode.Open, FileAccess.Read);
                    fileHandle.Close();
                    fileHandle.Dispose();
                    Thread.Sleep(500);
                    return;
                }
                catch (Exception)
                {
                    waitCounter = waitCounter.Add(TimeSpan.FromMilliseconds(500));
                    Thread.Sleep(500);
                }
            }
        }

        private bool IsCancelled()
        {
            lock (this.cancelSync)
            {
                if (this.isCancelled)
                {
                    this.logger.Debug("Download was cancelled.");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Do the actual dispose
        /// </summary>
        /// <param name="disposing">True if called from Dispose</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    if (this.readyToDownload.WaitOne(TimeSpan.FromMinutes(10)))
                    {
                        if (this.worker != null)
                        {
                            this.worker.Dispose();
                        }
                        if (this.downloadWebClient != null)
                        {
                            this.downloadWebClient.Dispose();
                        }
                        this.readyToDownload.Close();
                        this.attemptTimer.Dispose();
                    }
                }
                this.disposed = true;
            }
        }
    }
}