using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Priority_Queue;
using PuppeteerSharp;

namespace GPhotosMirror
{
    public class MirrorTask : FastPriorityQueueNode
    {
        protected readonly Page page;
        protected readonly LiteInstance _liteInstance;

        public MirrorTask(Page page, LiteInstance liteInstance)
        {
            this.page = page;
            _liteInstance = liteInstance;
        }
        public async virtual Task Proceed(CancellationToken ct = default)
        {

        }

    }

    public class OpenOrCreateAlbumTask:MirrorTask
    {
        public string LocalFolder { get; }
        public MirrorTaskExecutioner MTaskExecutioner { get; }

        public override async Task Proceed(CancellationToken ct = default)
        {
            //if localParent contains files
            var localFiles = Directory.GetFiles(LocalFolder).FilterPhotosVideos();
            var filesUp = _liteInstance.GetFilesFromDirectory(LocalFolder);
            IEnumerable<string> filesToGoUp = null;
            if (filesUp != null)
            {
                filesToGoUp = localFiles.Except(filesUp.Select(f => f.LocalPath));
            }
            else
            {
                filesToGoUp = localFiles;
            }

            var filesToGoUpList = filesToGoUp.ToList();
            if (filesToGoUpList.Any())
            {
                var createAlbumTask = new CreateAlbumTask(LocalFolder, page, _liteInstance);
                var dirUp = _liteInstance.LiteDirectories.FindOne(d => d.LocalPath == LocalFolder);
                
                if(dirUp!=null)
                {
                    var response = await page.GoToAsync(Constants.GOOGLE_PHOTOS_ALBUM_URL + dirUp.Link,
                        WaitUntilNavigation.Networkidle0);
                    if (!response.Ok)
                    {
                        dirUp.Link = null;
                        _liteInstance.LiteDirectories.Upsert(dirUp);
                    }
                }
                if (dirUp?.Link == null)
                {
                    if (!page.Url.Equals(Constants.GOOGLE_PHOTOS_URL_SEARCH))
                    {
                        await page.GoToAsync(Constants.GOOGLE_PHOTOS_URL_SEARCH, WaitUntilNavigation.Networkidle0);
                    }

                    var folderName = Path.GetFileName(LocalFolder);
                    
                    //try to find or create album named like localParent
                    await page.Keyboard.PressAsync("/");
                    var searchFocused =
                        await page.WaitForExpressionAsync(
                            "document.activeElement==document.querySelector('INPUT.Ax4B8.ZAGvjd')");
                    await page.Keyboard.TypeAsync(folderName);

                    var searchHintArea = await page.WaitForSelectorAsync(".u3WVdc.jBmls[data-expanded=true]",
                        Constants.NoTimeoutOptions);
                    await page.WaitForTimeoutAsync(Constants.LongTimeout);

                    if (searchHintArea == null)
                    {
                        MTaskExecutioner.Enqueue(createAlbumTask);
                    }
                    else
                    {
                        var searchHints = await searchHintArea.QuerySelectorAllAsync(".MkjOTb.oKubKe.lySfNc");

                        var remoteAlbums = (await page.EvaluateExpressionAsync(
                            @"let hello = function() {
                            let elem = document.querySelector('.u3WVdc.jBmls[data-expanded=true]');
                            let folders = elem.querySelectorAll('.lROwub');
                            folders = Array.from(folders);
                            return folders.map(f => f.textContent);
                            };
                            hello();")).ToObject<string[]>();
                        var clickIndex = Array.IndexOf(remoteAlbums, folderName);
                        if (clickIndex == -1)
                        {
                            MTaskExecutioner.Enqueue(createAlbumTask);
                        }
                        else
                        {
                            //album already exist
                            var albumLink = 
                            await page.EvaluateFunctionAsync("(t)=> t.getAttribute('data-album-media-key')",searchHints[clickIndex]);
                            var stringLink =  albumLink.ToObject<string>();
                            var response = await page.GoToAsync(Constants.GOOGLE_PHOTOS_ALBUM_URL + stringLink,
                                WaitUntilNavigation.Networkidle0);
                            
                        }
                    }
                }
                

                var uploadPhotos = new UploadPhotosTask(filesToGoUpList, LocalFolder, page, _liteInstance);
                MTaskExecutioner.Enqueue(uploadPhotos);
            }

            var localFolders = Directory.GetDirectories(LocalFolder);
            foreach (var folder in localFolders)
            {
                var newOpenCreate = new OpenOrCreateAlbumTask(folder, MTaskExecutioner, page, _liteInstance);
                MTaskExecutioner.Enqueue(newOpenCreate);
            }
        }
        public OpenOrCreateAlbumTask(string localFolder, MirrorTaskExecutioner mTaskExecutioner, Page page, LiteInstance liteInstance) : base(page, liteInstance)
        {
            LocalFolder = localFolder;
            MTaskExecutioner = mTaskExecutioner;
        }
    }

    public class CreateAlbumTask:MirrorTask
    {
        public string LocalFolder { get; }

        public override async Task Proceed(CancellationToken ct = default)
        {
            await page.GoToAsync(Constants.GOOGLE_PHOTOS_URL);
            var newButton = (await page.EvaluateExpressionAsync(
                @"let newButton = function() {
                let elem = document.querySelectorAll('.U26fgb.JRtysb.WzwrXb.YI2CVc.G6iPcb.m6aMje.ML2vC')[0];
                elem.click();
            };
            newButton();"));
            //new item menu
            //await page.WaitForSelectorAsync("DIV.JPdR6b.e5Emjc.s2VtY.qjTEB");
            var newAlbum = await page.WaitForSelectorAsync("DIV.JPdR6b.e5Emjc.s2VtY.qjTEB SPAN.z80M1.o7Osof.mDKoOe");
            //await page.WaitForTimeoutAsync(Constants.ShortTimeout);
            await newAlbum.PressAsync("Enter");
            await page.WaitForNavigationAsync(new NavigationOptions() {WaitUntil = new []{WaitUntilNavigation.Networkidle0 } });

            await page.WaitForSelectorAsync("TEXTAREA.ajQY2.v3oaBb");
            var textArea = await page.QuerySelectorAsync("TEXTAREA.ajQY2.v3oaBb");
            //force focus
            await textArea.FocusAsync();
            await page.WaitForTimeoutAsync(Constants.ShortTimeout);

            var localFolderName = Path.GetFileName(LocalFolder);
            await page.Keyboard.TypeAsync(localFolderName);
            await page.Keyboard.PressAsync("Enter");
            await page.WaitForSelectorAsync($"TEXTAREA.ajQY2.v3oaBb[initial-data-value=\"{localFolderName}\"]");
        }
        public CreateAlbumTask( string localFolder, Page page, LiteInstance liteInstance) : base(page, liteInstance)
        {
            LocalFolder = localFolder;
        }
    }

    public class UploadPhotosTask : MirrorTask
    {
        private readonly IEnumerable<string> _localFilesPaths;
        private readonly string _parent;

        public long FileSize
        {
            get;
        }

        public override async Task Proceed(CancellationToken ct = default)
        {
            var link = page.Url.Substring(page.Url.LastIndexOf("/", StringComparison.Ordinal) + 1);
            _liteInstance.DirectoryUp(_parent, link);
            //handles both scenarios:
            //1. add photos to empty album
            //2. add photos to album with at least one photo

            await page.WaitForSelectorAsync(".VfPpkd-LgbsSe.VfPpkd-LgbsSe-OWXEXe-k8QpJ.nCP5yc.AjY5Oe");
            var addPhotoButton = (await page.EvaluateExpressionAsync(@"
            let addPhotos = function() {
            let elemArr = document.querySelectorAll('.VfPpkd-LgbsSe.VfPpkd-LgbsSe-OWXEXe-k8QpJ.nCP5yc.AjY5Oe');
            if (elemArr.length < 2) {
                elemArr = document.querySelectorAll('.VfPpkd-Bz112c-LgbsSe.yHy1rc.eT1oJ.cx6Jyd');
                elemArr[elemArr.length - 2].click();
            } else {
                elemArr[elemArr.length - 1].click();
            }
            };
            addPhotos();"));
            //await addPhotoButton.ClickAsync();
            await page.WaitForSelectorAsync(".VfPpkd-LgbsSe.ksBjEc.lKxP2d");
            var filesFromPC = await page.QuerySelectorAsync(".VfPpkd-LgbsSe.ksBjEc.lKxP2d");
            await filesFromPC.PressAsync("Enter");
            await page.WaitForSelectorAsync("input[type=file]");
            await page.QuerySelectorAsync("input[type=file]");
            var fileInput = await page.QuerySelectorAsync("input[type=file]");
            await fileInput.UploadFileAsync(_localFilesPaths.ToArray());

            //upload box showed
            await page.WaitForSelectorAsync(".aHPraf.zPNfib", Constants.NoTimeoutOptions);
            //upload box hidden
            await page.WaitForSelectorAsync(".aHPraf.zPNfib", Constants.NoTimeoutOptionsHidden);
            var errorBox = await page.QuerySelectorAsync(".WjkDEe.zPNfib");
            if (errorBox == null)
            {
                _liteInstance.FilesUp(_localFilesPaths, _parent);
            }
            else
            {
                //some error
            }
        }

        public UploadPhotosTask(IEnumerable<string> localFilesPaths, string parent, Page page, LiteInstance liteInstance) : base(page, liteInstance)
        {
            _localFilesPaths = localFilesPaths;
            _parent = parent;
            FileSize = localFilesPaths.Select(f=>new FileInfo(f).Length).Sum();
        }
    }
}
