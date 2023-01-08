using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace DiskProbe.Extensions;

public static class HashExtension
{
    public static string ToHexString(this byte[] array) => Convert.ToHexString(array).ToLower();
}
