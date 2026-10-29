using System.Collections.Generic;

namespace MDPro3.Data
{
    public class FileGroupConfig
    {
        public static readonly string SavePath = Program.PATH_DATA + "FileGroups.json";

        public string[] zipExtensions = new string[] { ".zip", ".ypk" };
        public void Save()
        {
            Json.SaveToFile(SavePath, this);
        }

        public bool IsZipFile(string path)
        {
            if(zipExtensions == null || zipExtensions.Length == 0)
                return false;
            foreach (var ext in zipExtensions)
            {
                if (path.EndsWith(ext, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public FileGroup Expansions = new()
        {
            Note = "ZIP archives, including expansion packs.",
            Paths = new string[] { "Expansions/" },
            Extensions = new string[] { ".zip", ".ypk" },
        };

        public FileGroup ScriptZip = new()
        {
            Note = "Default Lua script archive.",
            Paths = new string[] { "Data/script.zip" },
            Extensions = new string[] { ".zip" },
        };

        public FileGroup CardScript = new()
        {
            Note = "Card scripts.",
            Paths = new string[] { "script/" },
            Extensions = new string[] { ".lua" },
        };

        public CardFileGroup CardIllust = new()
        {
            Note = "Card artwork.",
            Paths = new string[] { "Picture/Art/", "art/" },
            Extensions = new string[] { ".jpg" },
        };

        public CardFileGroup CardIllust2 = new()
        {
            Note = "Card artwork: the image inside the frame for overframe cards, or an override for the default artwork of other cards.",
            Paths = new string[] { "Picture/Art2/", "art2/" },
            Extensions = new string[] { ".png" },
        };

        public CardFileGroup CardCloseup = new()
        {
            Note = "Monster cutout artwork displayed above monster cards on the duel field.",
            Paths = new string[] { "Picture/Closeup/", "closeup/" },
            Extensions = new string[] { ".png" },
        };

        public CardFileGroup CardOverframe = new()
        {
            Note = "Overframe card images. JPG files are treated as complete overframe card images.",
            Paths = new string[] { "Picture/OverFrame/", "overframe/" },
            Extensions = new string[] { ".jpg" },
        };

        public CardFileGroup CardOverframe2 = new()
        {
            Note = "Overframe card images. PNG files are treated as transparent artwork; 512x1024 PNG files are treated as complete transparent card images exported by Master Duel.",
            Paths = new string[] { "Picture/OverFrame/", "overframe/" },
            Extensions = new string[] { ".png" },
        };

        public CardFileGroup CardPicture = new()
        {
            Note = "Complete card images, including artwork loaded from expansion packs.",
            Paths = new string[] { "pics/" },
            Extensions = new string[] { ".jpg" },
        };

        public CardFileGroup CardVideo = new()
        {
            Note = "Animated card artwork videos. Videos inside ZIP archives must be extracted before use; the first Paths entry is the default extraction directory. Files already present in Paths[0] are not extracted again.",
            Paths = new string[] { "Video/Art/", "video/" },
            Extensions = new string[] { ".mp4" },
        };

        public List<FileGroup> GetList()
        {
            if (Config.GetBool("Expansions", true))
                return new List<FileGroup>()
                    { Expansions, ScriptZip, CardScript, CardIllust, CardIllust2, CardCloseup, CardOverframe, CardOverframe2, CardPicture, CardVideo };
            else
                return new List<FileGroup>()
                    { ScriptZip, CardScript, CardIllust, CardIllust2, CardCloseup, CardOverframe, CardOverframe2, CardPicture, CardVideo };
        }
    }
}


