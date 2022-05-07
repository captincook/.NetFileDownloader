//----------------------------------------------------------------------------------------------------
// <copyright company="Avira Operations GmbH & Co. KG and its licensors">
// © 2016 Avira Operations GmbH & Co. KG and its licensors.  All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------------

using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using FileDownloader.Logging;

namespace FileDownloader
{
    [DesignerCategory("Code")]
    internal class DownloadWebClient : WebClient
    {
        private readonly ILogger logger = LoggerFacade.GetCurrentClassLogger();
        private readonly CookieContainer cookieContainer = new CookieContainer();
        private WebResponse webResponse;
        private long position;

        private TimeSpan timeout = TimeSpan.FromMinutes(2);

        public bool HasResponse
        {
            get { return this.webResponse != null; }
        }

        public bool IsPartialResponse
        {
            get
            {
                var response = this.webResponse as HttpWebResponse;
                return response != null && response.StatusCode == HttpStatusCode.PartialContent;
            }
        }

        public void OpenReadAsync(Uri address, long newPosition)
        {
            this.position = newPosition;
            OpenReadAsync(address);
        }

        public string GetOriginalFileNameFromDownload()
        {
            if (this.webResponse == null)
            {
                return null;
            }

            try
            {
                var contentDisposition = this.webResponse.Headers.GetContentDisposition();
                if (contentDisposition != null)
                {
                    var filename = contentDisposition.FileName;
                    if (!string.IsNullOrEmpty(filename))
                    {
                        return Path.GetFileName(filename);
                    }
                }
                return Path.GetFileName(this.webResponse.ResponseUri.LocalPath);
            }
            catch (Exception ex)
            {
                this.logger.Warn("Can't get the name of the downloading file. Exception: {0}", ex.Message);
                return null;
            }
        }

        protected override WebResponse GetWebResponse(WebRequest request)
        {
            var response = base.GetWebResponse(request);
            this.webResponse = response;
            this.logger.Debug("Recieved web response for sync call, IsFromCache: {0}, url {1}", response != null && response.IsFromCache, request.RequestUri);
            return response;
        }

        protected override WebResponse GetWebResponse(WebRequest request, IAsyncResult result)
        {
            var response = base.GetWebResponse(request, result);
            this.webResponse = response;

            this.logger.Debug("Recieved web response for async call, IsFromCache: {0}, url {1}", response != null && response.IsFromCache, request.RequestUri);
            return response;
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            var request = base.GetWebRequest(address);

            if (request != null)
            {
                request.Timeout = (int)this.timeout.TotalMilliseconds;
            }

            var webRequest = request as HttpWebRequest;
            if (webRequest == null)
            {
                return request;
            }

            webRequest.ReadWriteTimeout = (int)this.timeout.TotalMilliseconds;
            webRequest.Timeout = (int)this.timeout.TotalMilliseconds;
            if (this.position != 0)
            {
                webRequest.AddRange((int)this.position);
                webRequest.Accept = "*/*";
            }
            webRequest.CookieContainer = this.cookieContainer;
            return request;
        }
    }
}