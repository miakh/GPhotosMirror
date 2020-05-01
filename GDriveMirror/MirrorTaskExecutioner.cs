using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ByteSizeLib;
using GDriveMirror.Annotations;

namespace GDriveMirror
{
    public class MirrorTaskExecutioner:INotifyPropertyChanged
    {
        private Queue<MirrorTask> MirrorTasks = new Queue<MirrorTask>();
        private bool _isExecuting;

        public MirrorTaskExecutioner()
        {
        }

        public bool IsExecuting
        {
            get => _isExecuting;
            set
            {
                _isExecuting = value;
                OnPropertyChanged();
            }
        }

        public void PreExecute()
        {
            foreach (var mirrorTask in MirrorTasks)
            {
                if (mirrorTask is UploadPhotosTask uploadPhotoTask)
                {
                    AllBytesUpload += uploadPhotoTask.FileSize;
                    AllFilesUpload += 1;
                }
            }

            RemainingBytesUpload = AllBytesUpload;
            RemainingFilesUpload = AllFilesUpload;
            RefreshProgress();
        }
        public async Task Execute()
        {
            IsExecuting = true;

            while (MirrorTasks.Count>0)
            {
                var task = MirrorTasks.Dequeue();
                await task.Proceed();
                if (task is UploadPhotosTask uploadPhotoTask)
                {
                    RemainingBytesUpload -= uploadPhotoTask.FileSize;
                    RemainingFilesUpload -= 1;
                    RefreshProgress();
                }
            }
            IsExecuting = false;
        }

        private void RefreshProgress()
        {
            OnPropertyChanged(nameof(ProgressPretty));
        }

        public string ProgressPretty
        {
            get
            {
                //use double in constructor to get byte size instead of bite size
                return $"Uploaded ({AllFilesUpload - RemainingFilesUpload}/{AllFilesUpload}). {new ByteSize((double)AllBytesUpload- RemainingBytesUpload)} from {new ByteSize((double)AllBytesUpload)}";
            }
        }

        public async Task StopExecution()
        {

        }

        private int AllFilesUpload;
        private int RemainingFilesUpload;
        private long AllBytesUpload;
        private long RemainingBytesUpload; 

        public void Enqueue(MirrorTask mt)
        {
            MirrorTasks.Enqueue(mt);
        }

        public MirrorTask Dequeue()
        {
            return MirrorTasks.Dequeue();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}