using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ByteSizeLib;
using Priority_Queue;
using PuppeteerSharp;
using Serilog;

namespace GPhotosMirror.Model
{
    public class MirrorTaskExecutioner : INotifyPropertyChanged
    {
        private bool _isExecuting;
        private bool _isStoppingExecution;
        private Func<Task> _startAction;
        private long AllBytesUpload;

        private int AllFilesUpload;

        private CancellationTokenSource cancellationTokenSource;
        public Func<Task> EndingAction;
        private readonly SimplePriorityQueue<MirrorTask, int> MirrorTasks = new SimplePriorityQueue<MirrorTask, int>();
        private long RemainingBytesUpload;
        private int RemainingFilesUpload;

        public bool IsExecuteButtonShowing => !IsExecuting || IsStoppingExecution;
        public bool IsExecuteButtonEnabled => StartAction != null && !IsStoppingExecution;

        public bool IsExecuting
        {
            get => _isExecuting;
            set
            {
                _isExecuting = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsExecuteButtonShowing));
            }
        }

        public string ProgressPretty
        {
            get
            {
                if (AllFilesUpload == 0)
                {
                    return "";
                }

                //use double in constructor to get byte size instead of bite size
                return
                    $"Uploaded {AllFilesUpload - RemainingFilesUpload} files ({new ByteSize((double)AllBytesUpload - RemainingBytesUpload)})";
            }
        }

        public bool IsStoppingExecution
        {
            get => _isStoppingExecution;
            set
            {
                _isStoppingExecution = value;
                OnPropertyChanged(nameof(IsExecuteButtonEnabled));
                OnPropertyChanged(nameof(IsExecuteButtonShowing));
            }
        }

        public Func<Task> StartAction
        {
            get => _startAction;
            set
            {
                _startAction = value;
                OnPropertyChanged(nameof(IsExecuteButtonEnabled));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

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
            Log.Information("Uploading process has started.");
            cancellationTokenSource = new CancellationTokenSource();
            if (StartAction != null)
            {
                await StartAction?.Invoke();
            }

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

            catch (PuppeteerException e)
            {
                if (e is TargetClosedException || e is NavigationException
                                               || e is ChromiumProcessException)
                {
                    //stop action
                    Log.Information("You have stopped the uploading process.");
                    return;
                }

                throw e;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Log.Error($"Uploading process has stopped.");
                Log.Error($"{e}");
            }

            if (AllFilesUpload == 0)
            {
                Log.Information($"No folder has been uploaded.");
            }
            else
            {
                Log.Information($"Uploaded {AllFilesUpload} files ({new ByteSize(AllBytesUpload)}).");
            }

            await StopExecution();
        }

        private void RefreshProgress() => OnPropertyChanged(nameof(ProgressPretty));

        public async Task StopExecution()
        {
            IsStoppingExecution = true;
            if (EndingAction != null)
            {
                await EndingAction?.Invoke();
            }

            MirrorTasks.Clear();
            cancellationTokenSource.Cancel();
            IsStoppingExecution = false;
            IsExecuting = false;
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

        public MirrorTask Dequeue() => MirrorTasks.Dequeue();

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
