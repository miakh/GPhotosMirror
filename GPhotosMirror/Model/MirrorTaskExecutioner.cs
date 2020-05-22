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
        private long _allBytesUpload;

        private int _allFoldersUpload;

        private CancellationTokenSource _cancellationTokenSource;
        public Func<Task> EndingAction;
        private readonly SimplePriorityQueue<MirrorTask, int> _mirrorTasks = new SimplePriorityQueue<MirrorTask, int>();

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
                if (_allFoldersUpload == 0)
                {
                    return "";
                }

                //use double in constructor to get byte size instead of bite size
                return
                    $"Uploaded {_allFoldersUpload} files ({new ByteSize((double)_allBytesUpload)})";
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

        public async Task Execute()
        {
            IsExecuting = true;
            RefreshProgress();

            Log.Information("Uploading process has started.");
            _cancellationTokenSource = new CancellationTokenSource();
            if (StartAction != null)
            {
                await StartAction?.Invoke();
            }

            try
            {
                while (_mirrorTasks.Count > 0)
                {
                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();
                    var task = _mirrorTasks.Dequeue();
                    await task.Proceed();
                    if (task is UploadPhotosTask uploadPhotoTask)
                    {
                        _allFoldersUpload += 1;
                        _allBytesUpload += uploadPhotoTask.FileSize;
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
                }
                else
                {
                    throw;
                }

            }
            catch (Exception e)
            {
                Log.Error($"Uploading process has stopped.");
                Log.Error($"{e}");
            }

            if (_allBytesUpload == 0)
            {
                Log.Information($"No folder has been uploaded.");
            }
            else
            {
                Log.Information($"Uploaded {_allFoldersUpload} folders ({new ByteSize((double)_allBytesUpload)}).");
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

            _mirrorTasks.Clear();
            _allFoldersUpload = 0;
            _allBytesUpload = 0;

            _cancellationTokenSource.Cancel();
            IsStoppingExecution = false;
            IsExecuting = false;
        }

        public void Enqueue(MirrorTask mt)
        {
            if (mt is OpenOrCreateAlbumTask)
            {
                _mirrorTasks.Enqueue(mt, 1);
            }
            else
            {
                _mirrorTasks.Enqueue(mt, 0);
            }
        }

        public MirrorTask Dequeue() => _mirrorTasks.Dequeue();

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
