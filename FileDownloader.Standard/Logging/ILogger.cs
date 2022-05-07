//----------------------------------------------------------------------------------------------------
// <copyright company="Avira Operations GmbH & Co. KG and its licensors">
// © 2016 Avira Operations GmbH & Co. KG and its licensors.  All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------------

namespace FileDownloader.Logging
{
    internal interface ILogger
    {
        /// <summary>
        /// Gets the name of the logger.
        /// </summary>
        /// <value>The name of the logger. </value>
        string Name { get; }

        /// <summary>
        /// Log format message at DEBUG level.
        /// </summary>
        /// <param name="message">The message with or without  format placeholders.</param>
        /// <param name="args">The optional parameter list for the format message.</param>
        void Debug(string message, params object[] args);

        /// <summary>
        /// Log format message at INFO level.
        /// </summary>
        /// <param name="message">The message with or without format placeholders.</param>
        /// <param name="args">The optional parameter list for the format message.</param>
        void Info(string message, params object[] args);

        /// <summary>
        /// Log format message at TRACE level.
        /// </summary>
        /// <param name="message">The message with or without format placeholders.</param>
        /// <param name="args">The optional parameter list for the format message.</param>
        void Warn(string message, params object[] args);

        /// <summary>
        /// Log format message at ERROR level.
        /// </summary>
        /// <param name="message">The message with format placeholders.</param>
        /// <param name="args">The optional parameter list for the format message.</param>
        void Error(string message, params object[] args);

        /// <summary>
        /// Log format message at FATAL level.
        /// </summary>
        /// <param name="message">The message with or without format placeholders.</param>
        /// <param name="args">The optional parameter list for the format message.</param>
        void Fatal(string message, params object[] args);
    }
}