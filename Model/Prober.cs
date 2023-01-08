﻿using System;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System.Threading.Tasks;
using DiskProbe.Extensions;
using System.Windows;

namespace DiskProbe.Model;

public class Prober
{
    private const string _workDirectoryPrefix = ".DiskProbe";
    private readonly string _workingDirectory = "";
    private readonly SHA1 _sha = SHA1.Create();
    private readonly Random _rand = new(DateTime.Now.Microsecond);
    private readonly CancellationTokenSource cts = new();
    private readonly DriveInfo _driveInfo;
    private readonly ProgressReporter _progressReporter;

    public Prober(string driveName, ProgressReporter progressReporter)
    {
        _progressReporter = progressReporter;
        _driveInfo = new(driveName);
        _workingDirectory = Path.Combine(_driveInfo.Name, _workDirectoryPrefix);
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern bool GetDiskFreeSpace(string lpRootPathName,
       out ulong lpSectorsPerCluster,
       out ulong lpBytesPerSector,
       out ulong lpNumberOfFreeClusters,
       out ulong lpTotalNumberOfClusters);

    public void Start() =>
        Task.Factory.StartNew(BeginCheck, cts.Token);

    public void Stop()
    {
        cts.Cancel();
        if (Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, true);
        }
    }

    private ulong GetBlockSize()
    {
        GetDiskFreeSpace(_driveInfo.Name, out var secPerCluster, out var bytePerSector, out var _, out var _);
        return secPerCluster * bytePerSector * (ulong)sbyte.MaxValue;
    }

    private void BeginCheck()
    {
        if (!Directory.Exists(_workingDirectory))
            Directory.CreateDirectory(_workingDirectory);

        var blockSize = GetBlockSize();
        var buffer = new byte[blockSize];
        var driveSize = _driveInfo.TotalSize;

        while ((ulong)_driveInfo.AvailableFreeSpace > 0)
        {
            if (cts.IsCancellationRequested) return;

            if (blockSize > (ulong)_driveInfo.AvailableFreeSpace && (ulong)_driveInfo.AvailableFreeSpace > 0)
                buffer = new byte[(ulong)_driveInfo.AvailableFreeSpace];

            _rand.NextBytes(buffer);
            var hash = _sha.ComputeHash(buffer);
            try
            {
                File.WriteAllBytes(Path.Combine(_workingDirectory, hash.ToHexString()), buffer);
            }
            catch (DirectoryNotFoundException) { }
            catch (FileNotFoundException) 
            {
                if (File.Exists(hash.ToHexString())) File.Delete(hash.ToHexString());
            }

            _progressReporter.Report((int)((driveSize - _driveInfo.AvailableFreeSpace) * 100 / driveSize));
            _progressReporter.Status($"{driveSize - _driveInfo.AvailableFreeSpace:N0} / {driveSize:N0} bytes filled.");
        }

        EndCheck();
    }

    private void EndCheck()
    {
        var files = Directory.GetFiles(_workingDirectory);
        var total = files.Length - 1;

        foreach (var f in files)
        {
            if (cts.IsCancellationRequested) break;

            var hash = _sha.ComputeHash(File.ReadAllBytes(f));

            if (hash.ToHexString() != Path.GetFileName(f))
            {
                _progressReporter.Status($"{Array.IndexOf(files, f)} part check failed.");
                MessageBox.Show("SHA1 sum of written data are not the same as read.", "SHA1 error");
                break;
            }
            else
            {
                _progressReporter.Report((int)((Array.IndexOf(files, f)) * 100 / total));
                _progressReporter.Status($"{Array.IndexOf(files, f)} / {total} part check OK.");
            }
        }
        Directory.Delete(_workingDirectory, true);
        _progressReporter.Done(false);
    }
}