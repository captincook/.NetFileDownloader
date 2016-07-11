## .NET File Downloader
This is a HTTP(S) file downloader with support of caching and resuming of partially downloaded files. 
The main goal of .NET File Downloader is to facilitate downloading of big files on bad internet connections. It supports resuming of partially downloaded files. So if the download is interrupted and restarted, only the remaining part of file would be downloaded again.
#### Why not simply use the WebClient from the .NET Framework? 
System.Net.WebClient has no support for resuming downloads. It might work fine for small files or over stable internet connections. Once you want to download bigger files via poor internet connections it is not sufficient anymore and downloads will fail.
### License
FileDownloader is open source software, licensed under the terms of MIT license. See [LICENSE](LICENSE) for details.
## Examples
### Create simple fileDownloader
```C#
    IFileDownloader fileDownloader = new FileDownloader.FileDownloader();
```
### Create fileDownloader with download cache
```C#
    IFileDownloader fileDownloader = new FileDownloader.FileDownloader(new DownloadCacheImplementation());
```
### Start download
```C#
    IFileDownloader fileDownloader = new FileDownloader.FileDownloader();
    fileDownloader.DownloadFileCompleted += DownloadFileCompleted;
    fileDownloader.DownloadFileAsync(picture, dowloadDestinationPath);
    
    void DownloadFileCompleted(object sender, DownloadFileCompletedArgs eventArgs)
    {
            if (eventArgs.State == CompletedState.Succeeded)
            {
                //download completed
            }
            else if (eventArgs.State == CompletedState.Failed)
            {
                //download failed
            }
    }    
```
### Report download progress
```C#
    FileDownlaoder fileDownloader = new FileDownloader.FileDownloader();
    fileDownloader.DownloadProgressChanged += OnDownloadProgressChanged;
    fileDownloader.DownloadFileAsync(picture, dowloadDestinationPath);

    void OnDownloadProgressChanged(object sender, DownloadFileProgressChangedArgs args)
    {
		Console.WriteLine("Downloaded {0} of {1} bytes", args.BytesReceived, args.TotalBytesToReceive)
    }

```

### Cancel download
```C#
    fileDownloader.CancelDownloadAsync()
```
### Resume download

 In order to resume download, IDownloadCache should be provided to the FileDownloader constructor. DownloadCache is used to store partially downloaded files. To resume a download, simply call one of the download file methods with the same URL.

## How to build FileDownlader
At least Visual Studio 2013 and .NET Framework 3.5 are required to build. There are no other dependencies. 
