using System;
using Microsoft.VisualBasic.FileIO;

namespace PictureOrganizer
{
    internal static class RecycleBinHelper
    {
        public static bool TrySendFileToRecycleBin(string filePath)
        {
            try
            {
                FileSystem.DeleteFile(filePath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.DoNothing);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
