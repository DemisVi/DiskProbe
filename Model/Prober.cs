using System;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System.Threading.Tasks;
using DiskProbe.Extensions;

namespace DiskProbe.Model;

public class Prober
{
    private const string _workDirectoryPrefix = ".DiskProbe";
    private readonly SHA1 _sha = SHA1.Create();
    private readonly Random _rand = new(DateTime.Now.Microsecond);
    private readonly static object _lock = new();
    private CancellationTokenSource cts = new();
    private string _workingDirectory = "";
    private DriveInfo? _driveInfo;
    private ProgressReporter? _progressReporter;
    private static Prober? _instance;

    private Prober() { }

    public static Prober Instance => _instance ??= new Prober();

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern bool GetDiskFreeSpace(string lpRootPathName,
       out ulong lpSectorsPerCluster,
       out ulong lpBytesPerSector,
       out ulong lpNumberOfFreeClusters,
       out ulong lpTotalNumberOfClusters);

    public void Start(string driveName, ProgressReporter progressReporter)
    {
        _progressReporter = progressReporter;
        _driveInfo = new(driveName);
        _workingDirectory = Path.Combine(_driveInfo.Name, _workDirectoryPrefix);
        cts = new();

        Task.Factory.StartNew(BeginCheck, cts.Token);
    }

    public void Stop()
    {
        cts.Cancel();
        lock (_lock)
        {
            if (Directory.Exists(_workingDirectory))
            {
                Directory.Delete(_workingDirectory, true);
            }
        }
    }

    private ulong GetBlockSize()
    {
        if (_driveInfo is null) throw new NullReferenceException();

        GetDiskFreeSpace(_driveInfo.Name, out var secPerCluster, out var bytePerSector, out var _, out var _);
        return secPerCluster * bytePerSector * (ulong)sbyte.MaxValue;
    }

    private void BeginCheck()
    {
        if (_driveInfo is null) throw new NullReferenceException();
        if (_progressReporter is null) throw new NullReferenceException();

        if (!Directory.Exists(_workingDirectory))
            Directory.CreateDirectory(_workingDirectory);

        var blockSize = GetBlockSize();
        var buffer = new byte[blockSize];
        var driveSize = _driveInfo.TotalSize;

        while ((ulong)_driveInfo.AvailableFreeSpace > 0)
        {
            lock (_lock)
            {
                if (cts.IsCancellationRequested) return;

                if (blockSize > (ulong)_driveInfo.AvailableFreeSpace && (ulong)_driveInfo.AvailableFreeSpace > 0)
                    buffer = new byte[(ulong)_driveInfo.AvailableFreeSpace];

                _rand.NextBytes(buffer);
                var hash = _sha.ComputeHash(buffer);
                File.WriteAllBytes(Path.Combine(_workingDirectory, hash.ToHexString()), buffer);
            }

            _progressReporter.Report((int)((driveSize - _driveInfo.AvailableFreeSpace) * 100 / driveSize));
            _progressReporter.Status($"{driveSize - _driveInfo.AvailableFreeSpace:N0} / {driveSize:N0} bytes filled.");
        }

        EndCheck();
    }

    private void EndCheck()
    {
        if (_driveInfo is null) throw new NullReferenceException();
        if (_progressReporter is null) throw new NullReferenceException();

        var files = Directory.GetFiles(_workingDirectory);
        var total = files.Length - 1;
        var totalBad = 0;
        var totalBadBytes = 0L;

        foreach (var f in files)
        {
            if (cts.IsCancellationRequested) break;

            var hash = _sha.ComputeHash(File.ReadAllBytes(f));

            if (hash.ToHexString() != Path.GetFileName(f))
            {
                _progressReporter.Status($"{Array.IndexOf(files, f)} chunk check failed.");

                totalBadBytes += new FileInfo(f).Length;
                totalBad++;
            }
            else
            {
                _progressReporter.Report((int)((Array.IndexOf(files, f)) * 100 / total));
                _progressReporter.Status($"{Array.IndexOf(files, f)} / {total} chunk check OK.");
            }
        }

        StatusReport();

        lock (_lock)
        {
            if (cts.IsCancellationRequested) return;
            else if (Directory.Exists(_workingDirectory))
            {
                Directory.Delete(_workingDirectory, true);
            }
        }

        void StatusReport()
        {
            if (_progressReporter is null) throw new NullReferenceException();
            if (files is not null)
            {
                _progressReporter.Status($"Total chunks {files.Length - 1}.");
                _progressReporter.Status($"Total bad chunks {totalBad}.");
                if (totalBad > 0) _progressReporter.Status($"~ {totalBadBytes:N0} bytes lost.");
                _progressReporter.Done(false);

            }
        }
    }
}