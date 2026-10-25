using System.Collections.Generic;

namespace CardGame.Data
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
            Note = "ZIP files such as expansion archives.",
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
            Note = "Alternate card artwork: the inner illustration for over-frame cards, or a shortcut to replace standard artwork for other cards.",
            Paths = new string[] { "Picture/Art2/", "art2/" },
            Extensions = new string[] { ".png" },
        };

        public CardFileGroup CardCloseup = new()
        {
            Note = "Monster closeups shown above monster cards on the duel field.",
            Paths = new string[] { "Picture/Closeup/", "closeup/" },
            Extensions = new string[] { ".png" },
        };

        public CardFileGroup CardOverframe = new()
        {
            Note = "Over-frame card images; JPG files are treated as complete over-frame images.",
            Paths = new string[] { "Picture/OverFrame/", "overframe/" },
            Extensions = new string[] { ".jpg" },
        };

        public CardFileGroup CardOverframe2 = new()
        {
            Note = "Over-frame images; PNG files are treated as transparent images. A 512x1024 PNG is treated as a complete Master Duel card image with alpha.",
            Paths = new string[] { "Picture/OverFrame/", "overframe/" },
            Extensions = new string[] { ".png" },
        };

        public CardFileGroup CardPicture = new()
        {
            Note = "Complete card images loaded from expansion packs.",
            Paths = new string[] { "pics/" },
            Extensions = new string[] { ".jpg" },
        };

        public CardFileGroup CardVideo = new()
        {
            Note = "Animated card video files. Videos in ZIP archives must be extracted before use. The first Paths entry is the default extraction directory, and files already present there are not extracted again.",
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


