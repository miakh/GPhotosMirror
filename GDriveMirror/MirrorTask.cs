using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PuppeteerSharp;

namespace GDriveMirror
{
    public class MirrorTask
    {
        protected readonly Page page;

        public MirrorTask(Page page)
        {
            this.page = page;
        }
        public async virtual Task Proceed()
        {

        }
    }

    public class CreateAlbumTask:MirrorTask
    {
        public string LocalFoldername { get; }

        public override async Task Proceed()
        {
            await page.GoToAsync(Constants.GOOGLE_PHOTOS_URL);
            var newButton = (await page.EvaluateExpressionAsync(
                @"let newButton = function() {
                let elem = document.querySelectorAll('.U26fgb.JRtysb.WzwrXb.YI2CVc.G6iPcb.m6aMje.ML2vC')[0];
                elem.click();
            };
            newButton();"));
            // var createButton = (await page.QuerySelectorAllAsync(".U26fgb.c7fp5b.FS4hgd.LcqyIb.m6aMje.WNmljc")).First();
            // await createButton.ClickAsync();

            await page.Keyboard.PressAsync("ArrowDown");
           await page.WaitForTimeoutAsync(Constants.LongTimeout);

            await page.Keyboard.PressAsync("Enter");
            await page.WaitForNavigationAsync(new NavigationOptions() {WaitUntil = new []{WaitUntilNavigation.Networkidle0 } });
            await page.Keyboard.TypeAsync(LocalFoldername);
        }
        public CreateAlbumTask(Page page, string localFoldername) : base(page)
        {
            LocalFoldername = localFoldername;
        }
    }

    public class UploadPhotosTask : MirrorTask
    {
        private readonly string[] _localFilesPaths;

        public long FileSize
        {
            get;
        }

        public override async Task Proceed()
        {
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
            await filesFromPC.ClickAsync();
            await page.WaitForSelectorAsync("input[type=file]");
            await page.QuerySelectorAsync("input[type=file]");
            var fileInput = await page.QuerySelectorAsync("input[type=file]");
            await fileInput.UploadFileAsync(_localFilesPaths);
            await page.WaitForSelectorAsync("div.gsckL", Constants.NoTimeoutOptions);
            await page.WaitForSelectorAsync("div.gsckL", Constants.NoTimeoutOptionsHidden);
            //await page.WaitForSelectorAsync("DIV.yKzHyd:not([style])", Constants.NoTimeoutOptions);

            //await page.WaitForSelectorAsync("DIV[jsname='qHptJd'][style='display: none;']", Constants.NoTimeoutOptions); div.gsckL
        }

        public UploadPhotosTask(Page page, string[] localFilesPaths) : base(page)
        {
            _localFilesPaths = localFilesPaths;
            FileSize = localFilesPaths.Select(f=>new FileInfo(f).Length).Sum();
        }
    }
}
