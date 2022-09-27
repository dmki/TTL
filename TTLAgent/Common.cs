using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTLAgent
{
    internal static class Common
    {
        public static bool IsNTFS(string targetDir)
        {
            bool networkDrive = targetDir.StartsWith(@"\\");
            if (networkDrive) return false;
            var targetDriveInfo = new DriveInfo(targetDir);
            //targetFreeSpace = targetDriveInfo.AvailableFreeSpace;//Important, not TotalFreeSpace, as it disregards quota
            return targetDriveInfo.DriveFormat == "NTFS";
        }

        public static bool IsCompressed(string path)
        {
            if (!File.Exists(path)) return true;
            FileInfo fi = new FileInfo(path);
            return ((fi.Attributes & FileAttributes.Compressed) != 0);
        }
    }
}
