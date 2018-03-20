using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;

namespace FileHistoryRecovery
{
    class MyFileInfo
    {
        public FileInfo FileInfo;
        public string Folder;
        public string RelativePath;
        public DateTime BackupDate;
        public long FileSize;
        private static Regex Regex = new Regex(@"^(.*) \((\d\d\d\d_\d\d_\d\d \d\d_\d\d_\d\d) UTC\)(\.[^\.]*)$");
        private static CultureInfo culture = CultureInfo.InvariantCulture;

        public static MyFileInfo Create(string rootFolder, string fileName)
        {
            FileInfo fi = new FileInfo(rootFolder + fileName);
            var match = Regex.Match(fileName);
            if (!match.Success) return null;
            string datetimeStr = match.Groups[2].Value;
            if (datetimeStr == null) return null;
            MyFileInfo mfi = new MyFileInfo();
            if (fileName.Contains("\\"))
            {
                mfi.Folder = fileName.Substring(0, fileName.LastIndexOf('\\'));
            }
            else
            {
                mfi.Folder = "";
            }
            mfi.RelativePath = match.Groups[1].Value + match.Groups[3].Value;
            mfi.FileSize = fi.Length;
            mfi.BackupDate = DateTime.ParseExact(datetimeStr, "yyyy_MM_dd HH_mm_ss", culture);
            mfi.FileInfo = fi;
            return mfi;
        }

        public void CopyTo(string rootFolder)
        {
            string newPath = rootFolder + this.RelativePath;
            FileInfo fi = new FileInfo(newPath);
            try
            {
                if (fi.Exists) return;
                File.Copy(this.FileInfo.FullName, newPath);
                fi.Refresh();
                if (fi.Attributes.HasFlag(FileAttributes.ReadOnly)) {
                    fi.Attributes = fi.Attributes & ~FileAttributes.ReadOnly;
                }
            }
            catch (Exception ex)
            {
                try
                {
                    if (fi.Exists) fi.Delete();
                }
                catch (Exception ex2) { }
                throw;
            }
        }
    }
}
