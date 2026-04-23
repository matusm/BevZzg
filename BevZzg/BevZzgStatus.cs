// Enumeration of possible synchronization states

using System;

namespace Bev.Zzg
{
    [Flags]
    public enum BevZzgStatus
    {
        Synchron = 0,
        TimeAsync = 1,
        DateAsync = 2,
        SysTimeChanged = 4,
        NoResponse = 8,
        NotConnected = 16,
        Unspecified = 32
    }
}

