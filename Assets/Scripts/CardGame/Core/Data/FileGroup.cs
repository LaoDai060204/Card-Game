
using CardGame.IO;
using System.Collections.Generic;

namespace CardGame.Data
{
    public class FileGroup
    {
        /// <summary>
        /// Description
        /// </summary>
        public string Note;
        /// <summary>
        /// A trailing slash indicates a directory; otherwise this is a file path.
        /// </summary>
        public string[] Paths;
        /// <summary>
        /// File extensions to index within each directory.
        /// </summary>
        public string[] Extensions;

        public bool FileExists(string filename)
        {
            foreach (var p in Paths)
                if (p.EndsWith("/"))
                    foreach (var ext in Extensions)
                    {
                        var name = System.IO.Path.Combine(p, filename + ext);
                        if(FileManager.FileExists(name))
                            return true;
                    }
            return false;
        }
    }

    public class CardFileGroup : FileGroup
    {
        public bool CardExists(int code)
        {
            return FileExists(code.ToString());
        }

        public string GetCardPath(int code)
        {
            foreach (var p in Paths)
                if (p.EndsWith("/"))
                    foreach (var ext in Extensions)
                    {
                        var name = System.IO.Path.Combine(p, code + ext);
                        if (FileManager.FileExists(name))
                            return name;
                    }
            return null;
        }
    }
}