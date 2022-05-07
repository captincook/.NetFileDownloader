//----------------------------------------------------------------------------------------------------
// <copyright company="Avira Operations GmbH & Co. KG and its licensors">
// © 2016 Avira Operations GmbH & Co. KG and its licensors.  All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------------

using System;

namespace FileDownloader
{
    internal static class EventHandlerExtensions
    {
        public static void SafeInvoke<T>(this EventHandler<T> evt, object sender, T e) where T : EventArgs
        {
            if (evt != null)
            {
                evt(sender, e);
            }
        }

        public static void SafeInvoke(this EventHandler evt, object sender, EventArgs e)
        {
            if (evt != null)
            {
                evt(sender, e);
            }
        }
    }
}