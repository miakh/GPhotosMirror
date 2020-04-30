using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;
using PuppeteerSharp;
using File = Google.Apis.Drive.v3.Data.File;

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
        private readonly string[] _localFilesPaths;
        public string LocalFoldername { get; }

        public override async Task Proceed()
        {
            var newButton = (await page.EvaluateExpressionAsync(
                @"let newButton = function() {
                let elem = document.querySelectorAll('.U26fgb.JRtysb.WzwrXb.YI2CVc.G6iPcb.m6aMje.ML2vC')[0];
                elem.click();
            };
            newButton();"));
            // var createButton = (await page.QuerySelectorAllAsync(".U26fgb.c7fp5b.FS4hgd.LcqyIb.m6aMje.WNmljc")).First();
            // await createButton.ClickAsync();

            await page.Keyboard.PressAsync("ArrowDown");
           await page.WaitForTimeoutAsync(500);

            await page.Keyboard.PressAsync("Enter");

            //await page.WaitForTimeoutAsync(500);
            //var newAlbumButton = (await page.EvaluateExpressionAsync(
            //    @"let newAlbumButton = function() {
            //    let elem = document.querySelectorAll('.z80M1.o7Osof.mDKoOe')[0];
            //    elem.click();
            //};
            //newAlbumButton();"));
            //await newAlbumButton.ClickAsync();
            await page.WaitForNavigationAsync(new NavigationOptions() {WaitUntil = new []{WaitUntilNavigation.Networkidle0 } });
            await page.Keyboard.TypeAsync(LocalFoldername);
            
            var addPhotoButton = (await page.EvaluateExpressionHandleAsync("let addPhotos = function(){let elemArr = document.querySelectorAll('.VfPpkd-LgbsSe.VfPpkd-LgbsSe-OWXEXe-k8QpJ.nCP5yc.AjY5Oe'); elemArr[elemArr.length-1].click(); }; addPhotos();"));
            //await addPhotoButton.ClickAsync();
            await page.WaitForSelectorAsync(".VfPpkd-LgbsSe.ksBjEc.lKxP2d");
            var filesFromPC = await page.QuerySelectorAsync(".VfPpkd-LgbsSe.ksBjEc.lKxP2d");
            await filesFromPC.ClickAsync();
            await page.WaitForSelectorAsync("input[type=file]");
            await page.QuerySelectorAsync("input[type=file]");
            var fileInput = await page.QuerySelectorAsync("input[type=file]");
            await fileInput.UploadFileAsync(_localFilesPaths);

        }
        public CreateAlbumTask(Page page, string localFoldername, string[] localFilesPaths) : base(page)
        {
            _localFilesPaths = localFilesPaths;
            LocalFoldername = localFoldername;
        }
    }

    public class UploadPhotoTask : MirrorTask
    {
        public string LocalFilePath { get; }
        public long FileSize
        {
            get;
        }

        public override async Task Proceed()
        {
            //var handle = await page.QuerySelectorAsync(".v9czFf");

            var newButton = await page.QuerySelectorAsync("button.RTMQvb");
            await newButton.ClickAsync();
            await newButton.PressAsync("ArrowDown");
            await newButton.PressAsync("ArrowDown");
            await newButton.PressAsync("Enter");
            await page.QuerySelectorAsync("input[type=file]");
            var fileInput = await page.QuerySelectorAsync("input[type=file]");
            await fileInput.UploadFileAsync(LocalFilePath);
        }

        public UploadPhotoTask(Page page, string localFilePath) : base(page)
        {
            LocalFilePath = localFilePath;
            FileSize = new FileInfo(LocalFilePath).Length;
        }
    }
}
