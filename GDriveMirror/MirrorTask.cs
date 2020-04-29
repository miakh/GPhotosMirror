using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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


    public class CreateFolderTask:MirrorTask
    {
        public string LocalFoldername { get; }

        public override async Task Proceed()
        {
            await page.Keyboard.DownAsync("Shift");
            await page.Keyboard.PressAsync("f");
            await page.Keyboard.UpAsync("Shift");
            await page.Keyboard.TypeAsync(LocalFoldername);
            await page.Keyboard.PressAsync("Enter");
            //wait to generate new folder
            var element = await page.WaitForSelectorAsync("DIV.WYuW0e.RDfNAe.GZwC2b.dPmH0b > DIV");
            await page.WaitForFunctionAsync("(d)=>d.getAttribute('aria-selected') == 'true'", element);
        }
        public CreateFolderTask(Page page, string localFoldername) : base(page)
        {
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
