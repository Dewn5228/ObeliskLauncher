using System.Runtime.InteropServices;

namespace ObeliskLauncher.Utils;

/// <summary>Consolidates function imports from Windows APIs and util methods using them.</summary>
static class WinAPI
{
    #region kernel32.dll
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern bool GetDiskFreeSpaceExW(string lpDirectoryName, out long lpFreeBytesAvailableToCaller, out long lpTotalNumberOfBytes, out long lpTotalNumberOfFreeBytes);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr LoadLibraryExW(string lpLibFileName, IntPtr hFile, uint dwFlags);
    #endregion
    /// <summary>Retrieves the amount of free space on specified disk.</summary>
    /// <param name="directory">A directory on the disk whose free space amount will be retrieved.</param>
    /// <returns>The amount of disk free space in bytes.</returns>
    public static long GetDiskFreeSpace(string directory) => GetDiskFreeSpaceExW(directory, out long freeSpace, out _, out _) ? freeSpace : 0;
}