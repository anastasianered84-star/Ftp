using System;

namespace Client
{
    public class FileItem
    {
        public string Name { get; set; }
        public string FullName { get; set; } 
        public string Type { get; set; }
        public string Size { get; set; }
        public bool IsDirectory { get; set; }

        public FileItem(string fullName, bool isDirectory, long size = 0)
        {
            FullName = fullName;
            Name = System.IO.Path.GetFileName(fullName.TrimEnd('/')); 
            IsDirectory = isDirectory;
            Type = isDirectory ? "Папка" : "Файл";

            if (isDirectory)
            {
                Size = "";
            }
            else
            {
                Size = FormatFileSize(size);
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double len = bytes;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}