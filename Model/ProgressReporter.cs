using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiskProbe.Model;

public class ProgressReporter : IProgress<int>
{
    public event Action<int>? OnProgress;
    public event Action<string>? OnStatus;
    public event Action<bool>? OnDone;

    public void Report(int percent) => OnProgress?.Invoke(percent);
    public void Status(string status) => OnStatus?.Invoke(status);
    public void Done(bool done) => OnDone?.Invoke(done);

}
