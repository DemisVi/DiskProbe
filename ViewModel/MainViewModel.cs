using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Management;
using DiskProbe.Commands;
using System.Windows.Input;
using DiskProbe.Model;
using System.Windows.Controls;
using System.Windows.Data;

namespace DiskProbe.ViewModel
{
    internal class MainViewModel : INotifyPropertyChanged
    {
        private object _lock = new();

        private readonly ManagementEventWatcher? _watcher;
        private readonly BackgroundWorker _proberWorker = new();
        public event PropertyChangedEventHandler? PropertyChanged;
        private ProgressReporter _progressReporter = new();
        private Prober prober = Prober.Instance;

        public MainViewModel()
        {
            _watcher = new ManagementEventWatcher("select * from win32_devicechangeevent");
            _watcher.EventArrived += (_, _) => DiskList = DriveInfo.GetDrives()
                                                                   //.Where(x => x.DriveType == DriveType.Removable)
                                                                   .Select(x => x.Name)
                                                                   .ToList();
            _watcher.Start();
            DiskList = DriveInfo.GetDrives()
                                //.Where(x => x.DriveType == DriveType.Removable)
                                .Select(x => x.Name)
                                .ToList();

            _progressReporter.OnProgress += ProgressReporter_OnProgress;
            _progressReporter.OnStatus += ProgressReporter_OnStatus;
            _progressReporter.OnDone += ProgressReporter_OnDone;
            _proberWorker.DoWork += ProberWorker_DoWork;

            BindingOperations.EnableCollectionSynchronization(LogBox, _lock);
        }

        public ObservableCollection<string> LogBox { get; set; } = new();

        private bool _isProberRunning = false;
        public bool IsProberRunningInvert { get => !_isProberRunning; }
        public bool IsProberRunning
        {
            get => _isProberRunning; set
            {
                ChangeProperty(ref _isProberRunning, value);
                OnPropertyChanged(nameof(IsProberRunningInvert));
            }
        }

        private List<string>? _drives;
        public List<string>? DiskList { get => _drives; set => ChangeProperty(ref _drives, value); }

        private string _selectedDisk = "";
        public string SelectedDisk
        {
            get => _selectedDisk; 
            set
            {
                LogBox.Clear();
                ChangeProperty(ref _selectedDisk, value);
                var di = new DriveInfo(SelectedDisk);
                LogBox.Insert(0, string.Format("{0}. Free space: {1:N2}/{2:N2} Gb",
                                         di.IsReady ? "Ready" : "Not Ready",
                                         (float)di.TotalFreeSpace / Constants.bytesInGb,
                                         (float)di.TotalSize / Constants.bytesInGb));
                LogBox.Insert(0, string.Format("{0} ({1}) {2} {3}",
                                         (string.IsNullOrEmpty(di.VolumeLabel) ? "No Label" : di.VolumeLabel),
                                         di.Name,
                                         di.DriveFormat,
                                         di.DriveType));
            }
        }

        private int _progressBarValue = 0;
        public int ProgressBarValue
        {
            get => _progressBarValue; 
            set
            {
                ChangeProperty(ref _progressBarValue, value);
                OnPropertyChanged(nameof(ProgressBarPercent));
            }
        }
        public string ProgressBarPercent { get => _progressBarValue + "%"; }

        private Command? _execute;
        public ICommand Execute => _execute ??= new Command(PerformExecute, x => !string.IsNullOrEmpty(SelectedDisk));

        private Command? _abort;
        public ICommand Abort => _abort ??= new Command(PerformAbort, x => !string.IsNullOrEmpty(SelectedDisk));

        private bool ChangeProperty<T>(ref T prop, T newValue, [CallerMemberName] string? memberName = null)
        {
            if (Equals(prop, newValue)) return false;
            else
            {
                prop = newValue;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(memberName));
                return true;
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? memberName = null) => 
            PropertyChanged?.Invoke(this, new(memberName ?? string.Empty));

        private void PerformExecute(object? parameter)
        {
            prober.Start(SelectedDisk, _progressReporter);
            IsProberRunning = true;
        }

        private void PerformAbort(object? obj)
        {
            prober.Stop();
            ProgressBarValue = 0;
            IsProberRunning = false;
            LogBox.Insert(0, "Operation aborted");
        }

        private void ProgressReporter_OnProgress(int obj) => ProgressBarValue = obj;

        private void ProgressReporter_OnStatus(string obj) => LogBox.Insert(0, obj);

        private void ProgressReporter_OnDone(bool obj) => IsProberRunning = obj;

        private void ProberWorker_DoWork(object? sender, DoWorkEventArgs e)
        {
            prober.Start(SelectedDisk, _progressReporter);
        }
    }
}
