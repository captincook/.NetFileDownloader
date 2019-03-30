//----------------------------------------------------------------------------------------------------
// <copyright company="Avira Operations GmbH & Co. KG and its licensors">
// © 2016 Avira Operations GmbH & Co. KG and its licensors.  All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------------

using System;
using System.Diagnostics;

namespace FileDownloader.Logging
{
    internal class LoggerFacade : ILogger, IDisposable
    {
        private readonly TraceSource traceSource;

        protected LoggerFacade(string name)
        {
            Name = name;
            this.traceSource = new TraceSource("Avira.FileDownloader");
        }

        public string Name { get; set; }

        /// <summary>
        /// Get a logger with a specified name.
        /// </summary>
        /// <param name="loggerName">The name of the logger.</param>
        /// <returns>Instance of logger.</returns>
        public static ILogger GetLogger(string loggerName)
        {
            return new LoggerFacade(loggerName);
        }

        /// <summary>
        /// Get a logger with the name of the calling class.
        /// </summary>
        /// <returns>Instance of logger.</returns>
        public static ILogger GetCurrentClassLogger()
        {
            string loggerName;
            Type declaringType;
            var framesToSkip = 1;
            do
            {
                var frame = new StackFrame(framesToSkip, false);
                var method = frame.GetMethod();
                declaringType = method.DeclaringType;
                if (declaringType == null)
                {
                    loggerName = method.Name;
                    break;
                }

                framesToSkip++;
                loggerName = declaringType.Name;
            }
            while (declaringType.Module.Name.Equals("mscorlib.dll", StringComparison.OrdinalIgnoreCase));

            return GetLogger(loggerName);
        }

        public void Debug(string message, params object[] args)
        {
            TraceEvent(TraceEventType.Verbose, message, args);
        }

        public void Info(string message, params object[] args)
        {
            TraceEvent(TraceEventType.Information, message, args);
        }

        public void Warn(string message, params object[] args)
        {
            TraceEvent(TraceEventType.Warning, message, args);
        }

        public void Error(string message, params object[] args)
        {
            TraceEvent(TraceEventType.Error, message, args);
        }

        public void Fatal(string message, params object[] args)
        {
            TraceEvent(TraceEventType.Critical, message, args);
        }

        public void Dispose()
        {
            this.traceSource.Close();
        }

        private void TraceEvent(TraceEventType eventType, string message, params object[] args)
        {
            string formattedMessage;
            if (args.Length > 0)
            {
                try
                {
                    formattedMessage = string.Format(message, args);
                }
                catch (FormatException)
                {
                    formattedMessage = message;
                }
            }
            else
            {
                formattedMessage = message;
            }

            this.traceSource.TraceEvent(eventType, 0, string.Format("[{0}] {1}", Name, formattedMessage));
            this.traceSource.Flush();
        }
    }
}