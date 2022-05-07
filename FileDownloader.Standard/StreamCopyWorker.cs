//----------------------------------------------------------------------------------------------------
// <copyright company="Avira Operations GmbH & Co. KG and its licensors">
// © 2016 Avira Operations GmbH & Co. KG and its licensors.  All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Threading;
using FileDownloader.Logging;

namespace FileDownloader
{
    internal class StreamCopyWorker : IStreamCopyWorker, IDisposable
    {
        private const int DefaultBufferSize = 1024 * 1024;
        private readonly ILogger logger = LoggerFacade.GetCurrentClassLogger();
        private readonly ManualResetEvent streamCopyFinished = new ManualResetEvent(true);
        private readonly System.Timers.Timer progressUpdateTimer;
        private readonly TimeSpan safeWaitTimeout;
        private readonly int copyBufferSize;

        private Stream sourceStream;
        private Stream destinationStream;
        private long previousReportedBytesReceived;
        private bool disposed;
        private long totalBytes;
        private CompletedState completedState;
        private int workerState = (int)WorkerState.NotStarted;

        public StreamCopyWorker()
            : this(TimeSpan.FromMilliseconds(500), DefaultBufferSize, TimeSpan.FromSeconds(15))
        {
        }

        internal StreamCopyWorker(TimeSpan progressUpdateInterval, int copyBufferSize, TimeSpan safeWaitTimeout)
        {
            this.copyBufferSize = copyBufferSize;
            this.safeWaitTimeout = safeWaitTimeout;

            this.progressUpdateTimer = new System.Timers.Timer(progressUpdateInterval.TotalMilliseconds);
            this.progressUpdateTimer.Elapsed += OnProgressUpdateTimerElapsed;
        }

        public event EventHandler<StreamCopyCompleteEventArgs> Completed;

        public event EventHandler<StreamCopyProgressEventArgs> ProgressChanged;

        private enum WorkerState
        {
            NotStarted,
            Started,
            Canceled,
            Finished
        }

        public long Position { get; private set; }

        public void CopyAsync(Stream source, Stream destination, long sizeInBytes)
        {
            if (ChangeState(WorkerState.Started) == false)
            {
                return;
            }

            this.sourceStream = source;
            this.destinationStream = destination;
            this.totalBytes = sizeInBytes;

            ThreadPool.QueueUserWorkItem(stateInfo => RunCopyProcess());
        }

        public void Cancel()
        {
            if (ChangeState(WorkerState.Canceled) == false)
            {
                return;
            }

            this.logger.Debug("StreamCopyWorker is finishing background thread...");
            if (!this.streamCopyFinished.WaitOne(this.safeWaitTimeout))
            {
                this.logger.Warn("StreamCopyWorker failed to finish background thread in timely manner.");
                return;
            }

            ////We may reach this code when cancel is requested BEFORE the RunCopyProcess
            ////There are moments when worker is not started, but still can be cancelled.
            FinalizeStream(ref this.destinationStream);
            FinalizeStream(ref this.sourceStream);

            this.logger.Debug("StreamCopyWorker cancelled.");
        }

        private void OnCompleted(StreamCopyCompleteEventArgs args)
        {
            Completed.SafeInvoke(this, args);
        }

        private void OnProgressChanged(StreamCopyProgressEventArgs args)
        {
            ProgressChanged.SafeInvoke(this, args);
        }

        private void RunCopyProcess()
        {
            if (!InitializeCopyProcess())
            {
                return;
            }

            Exception error = null;
            try
            {
                Copy();
                EmitFinalProgress();
            }
            catch (Exception ex)
            {
                this.logger.Warn("StreamCopyWorker caught exception: {0}", ex.Message);
                this.completedState = CompletedState.Failed;
                error = ex;
            }

            FinalizeCopyProcess(error);
        }

        private bool InitializeCopyProcess()
        {
            this.logger.Debug("Starting StreamCopyWorker thread...");

            if (!this.streamCopyFinished.WaitOne(this.safeWaitTimeout))
            {
                this.logger.Error("Failed to start StreamCopyWorker thread.");
                return false;
            }
            this.streamCopyFinished.Reset();
            this.logger.Debug("StreamCopyWorker thread started.");

            Position = 0;
            ChangeState(WorkerState.Started);

            this.progressUpdateTimer.Start();
            return true;
        }

        private void Copy()
        {
            var buffer = new byte[this.copyBufferSize];
            using (var binaryWriter = new BinaryWriter(this.destinationStream))
            {
                int readBytes;
                while ((readBytes = this.sourceStream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    binaryWriter.Write(buffer, 0, readBytes);
                    Position = this.destinationStream.Position;

                    if (GetState() == WorkerState.Canceled || GetState() == WorkerState.Finished)
                    {
                        this.logger.Debug("StreamCopyWorker cancelled.");
                        this.completedState = CompletedState.Canceled;
                        return;
                    }
                }

                this.completedState = CompletedState.Succeeded;
            }
        }

        private void EmitFinalProgress()
        {
            ////the last direct call to deliver most accurate and actual progress without timer
            ////without this we often have not 100% on the end of download
            if (this.completedState == CompletedState.Succeeded)
            {
                if (Position != this.totalBytes)
                {
                    throw new Exception(string.Format("Stream incomplete. Expected size: {0}, actual size {1}", this.totalBytes, Position));
                }

                OnProgressChanged(new StreamCopyProgressEventArgs { BytesReceived = Position });
            }
        }

        private void FinalizeCopyProcess(Exception error)
        {
            this.progressUpdateTimer.Stop();

            FinalizeStream(ref this.sourceStream);
            FinalizeStream(ref this.destinationStream);

            ChangeState(WorkerState.Finished);

            this.logger.Debug("StreamCopyWorker Completed.");
            this.streamCopyFinished.Set();
            OnCompleted(new StreamCopyCompleteEventArgs { CompleteState = this.completedState, Exception = error });
        }

        private void FinalizeStream(ref Stream stream)
        {
            if (stream == null)
            {
                return;
            }

            try
            {
                stream.Close();
                stream.Dispose();
                stream = null;
            }
            catch (Exception ex)
            {
                this.logger.Warn("StreamCopyWorker is not able to dispose stream. Exception: {0}", ex.Message);
            }
        }

        private void OnProgressUpdateTimerElapsed(object sender, EventArgs eventArgs)
        {
            if (GetState() != WorkerState.Started)
            {
                return;
            }

            if (Position != this.previousReportedBytesReceived)
            {
                this.previousReportedBytesReceived = Position;
                OnProgressChanged(new StreamCopyProgressEventArgs { BytesReceived = Position });
            }
        }

        private bool ChangeState(WorkerState newState)
        {
            if (newState == WorkerState.Finished)
            {
                Interlocked.Exchange(ref this.workerState, (int)newState);
                return true;
            }
            return Interlocked.CompareExchange(ref this.workerState, (int)newState, (int)newState - 1) == (int)newState - 1;
        }

        private WorkerState GetState()
        {
            return (WorkerState)this.workerState;
        }

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
                    this.progressUpdateTimer.Dispose();
                    this.streamCopyFinished.Close();
                    ChangeState(WorkerState.Finished);
                    FinalizeStream(ref this.sourceStream);
                    FinalizeStream(ref this.destinationStream);
                }
                this.disposed = true;
            }
        }
    }
}