using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ByteSizeLib;
using GDriveMirror.Annotations;
using Priority_Queue;

namespace GDriveMirror
{
    public class MirrorTaskExecutioner:INotifyPropertyChanged
    {
        private SimplePriorityQueue<MirrorTask, int> MirrorTasks = new SimplePriorityQueue<MirrorTask, int>();
        private bool _isExecuting;

        public bool IsExecuting
        {
            get => _isExecuting;
            set
            {
                _isExecuting = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
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
            cancellationTokenSource = new CancellationTokenSource();
            IsExecuting = true;
            try
            {
                PreExecute();
                while (MirrorTasks.Count > 0)
                {
                    cancellationTokenSource.Token.ThrowIfCancellationRequested();
                    var task = MirrorTasks.Dequeue();
                    await task.Proceed();
                    if (task is UploadPhotosTask uploadPhotoTask)
                    {
                        RemainingBytesUpload -= uploadPhotoTask.FileSize;
                        RemainingFilesUpload -= 1;
                        RefreshProgress();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw e;
            }

            if (EndingAction != null)
            {
                await EndingAction?.Invoke();
            }

            IsStoppingExecution = false;
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

        public bool IsStoppingExecution
        {
            get => _isStoppingExecution;
            set
            {
                _isStoppingExecution = value;
                OnPropertyChanged();
            }
        }

        private CancellationTokenSource cancellationTokenSource;
        public async Task StopExecution()
        {
            IsStoppingExecution = true;
            cancellationTokenSource.Cancel();
        }

        private int AllFilesUpload;
        private int RemainingFilesUpload;
        private long AllBytesUpload;
        private long RemainingBytesUpload;
        private readonly Func<Task> EndingAction;
        private bool _isStoppingExecution;

        public MirrorTaskExecutioner(Func<Task> endingAction)
        {
            this.EndingAction = endingAction;
        }

        public void Enqueue(MirrorTask mt)
        {
            if (mt is OpenOrCreateAlbumTask)
            {
                MirrorTasks.Enqueue(mt, 1);
            }
            else
            {
                MirrorTasks.Enqueue(mt, 0);
            }
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