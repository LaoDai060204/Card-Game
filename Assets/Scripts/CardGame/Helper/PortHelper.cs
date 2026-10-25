
using System.Collections.Generic;
using System.IO;
using System.Linq;

#if UNITY_EDITOR || (!UNITY_ANDROID && !UNITY_IOS)
using SFB;
#endif

namespace CardGame
{
    public class PortHelper
    {
        static List<string> filesToDelete = new List<string>();
        static string[] pictureFormat = new string[] { "image/png", "image/jpeg" };
        const string bgPath = Program.PATH_DIY + "Background.png";
        public static void ImportFiles()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            NativeFilePicker.PickMultipleFiles(MoveFilesToGame, null);
#else
            ChooseFiles();
#endif
        }

        public static void ImportBG()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            NativeFilePicker.PickFile(MovePictureToGameBG, pictureFormat);
#else
            ChooseBGPicture();
#endif
        }

        private static void ChooseFiles()
        {
#if UNITY_EDITOR || (!UNITY_ANDROID && !UNITY_IOS)

            var extensions = new[]
            {
                new ExtensionFilter(InterString.Get("All Files"), "*"),
                new ExtensionFilter(InterString.Get("Deck File"), "ydk"),
                new ExtensionFilter(InterString.Get("Replay File"), "yrp", "yrp3d"),
                new ExtensionFilter(InterString.Get("Expansion File"), "ypk"),
                new ExtensionFilter(InterString.Get("Database File"), "cdb"),
                new ExtensionFilter(InterString.Get("String File"), "conf"),
                new ExtensionFilter(InterString.Get("Image File"), "png", "jpg"),
                new ExtensionFilter(InterString.Get("Animated Card Art"), "mp4"),
            };
            StandaloneFileBrowser.OpenFilePanelAsync(InterString.Get("Please select a file to import"), "", extensions, true, (string[] paths) =>
            {
                CopyFilesToGame(paths);
            });

#endif
        }

        private static void ChooseBGPicture()
        {
#if UNITY_EDITOR || (!UNITY_ANDROID && !UNITY_IOS)

            var extensions = new[]
            {
                new ExtensionFilter(InterString.Get("Image File"), "png", "jpg")
            };
            StandaloneFileBrowser.OpenFilePanelAsync(InterString.Get("Please select a file to import"), "", extensions, false, (string[] paths) =>
            {
                CopyBGToGame(paths);
            });

#endif
        }

        private static void CopyBGToGame(IEnumerable<string> files)
        {
            foreach (string path in files)
            {
                if(File.Exists(bgPath))
                    File.Delete(bgPath);
                File.Copy(path, bgPath);
                MessageManager.Cast(InterString.Get("Background image imported successfully."));
                Program.instance.background_.Refresh();
            }
        }
        private static void MovePictureToGameBG(string file)
        {
            CopyBGToGame(new List<string> { file });
        }

        private static void CopyFilesToGame(IEnumerable<string> files)
        {
            bool newDataAdded = false;
            bool newArtVideosAdded = false;

            foreach (string path in files)
            {
                var fileName = Path.GetFileName(path);
                try
                {
                    if (path.ToLower().EndsWith(Program.EXPANSION_YDK))
                    {
                        File.Copy(path, Program.PATH_DECK + fileName, true);
                        MessageManager.Cast(InterString.Get("Deck \"[?]\" imported successfully.", fileName.Replace(Program.EXPANSION_YDK, string.Empty)));
                    }
                    else if (path.ToLower().EndsWith(Program.EXPANSION_YRP) || path.ToLower().EndsWith(Program.EXPANSION_YRP3D))
                    {
                        File.Copy(path, Program.PATH_REPLAY + fileName, true);
                        MessageManager.Cast(InterString.Get("Replay \"[?]\" imported successfully.", fileName));
                    }
                    else if (path.ToLower().EndsWith(".ypk") || path.ToLower().EndsWith(".zip") || path.ToLower().EndsWith(".cdb") || path.ToLower().EndsWith(".conf"))
                    {
                        File.Copy(path, Program.PATH_EXPANSIONS + fileName, true);
                        newDataAdded = true;
                        if (fileName.ToLower().EndsWith(".ypk") || fileName.ToLower().EndsWith(".zip"))
                            MessageManager.Cast(InterString.Get("Expansion file \"[?]\" imported successfully.", fileName));
                        else if (fileName.ToLower().EndsWith(".cdb"))
                            MessageManager.Cast(InterString.Get("Database file \"[?]\" imported successfully.", fileName));
                        else if (fileName.ToLower().EndsWith(".conf"))
                            MessageManager.Cast(InterString.Get("String file \"[?]\" imported successfully.", fileName));
                    }
                    else if (path.ToLower().EndsWith(Program.EXPANSION_PNG) || path.ToLower().EndsWith(Program.EXPANSION_JPG))
                    {
                        File.Copy(path, Program.PATH_ALT_ART + Path.GetFileName(path), true);
                        MessageManager.Cast(InterString.Get("Custom card art \"[?]\" imported successfully.", fileName));
                    }
                    else if (path.ToLower().EndsWith(Program.EXPANSION_MP4))
                    {
                        File.Copy(path, Program.PATH_VIDEO_ART + Path.GetFileName(path), true);
                        MessageManager.Cast(InterString.Get("Animated card art \"[?]\" imported successfully.", fileName));
                        newArtVideosAdded = true;
                    }
                }
                catch { }
            }

            if (newDataAdded)
                Program.instance.InitializeForDataChange();
        }

        private static void MoveFilesToGame(string[] files)
        {
            bool newDataAdded = false;
            bool newArtVideosAdded = false;
            foreach (string path in files)
            {
                try
                {
                    if (path.ToLower().EndsWith(Program.EXPANSION_YDK))
                        File.Move(path, Program.PATH_DECK + Path.GetFileName(path));
                    if (path.ToLower().EndsWith(Program.EXPANSION_YRP) || path.ToLower().EndsWith(Program.EXPANSION_YRP3D))
                        File.Move(path, Program.PATH_REPLAY + Path.GetFileName(path));
                    if (path.ToLower().EndsWith(".ypk") || path.ToLower().EndsWith(".zip") || path.ToLower().EndsWith(".cdb") || path.ToLower().EndsWith(".conf"))
                    {
                        File.Move(path, Program.PATH_EXPANSIONS + Path.GetFileName(path));
                        newDataAdded = true;
                    }
                    if (path.ToLower().EndsWith(Program.EXPANSION_PNG) || path.ToLower().EndsWith(Program.EXPANSION_JPG) || path.ToLower().EndsWith(".jpeg"))
                        File.Move(path, Program.PATH_ALT_ART + Path.GetFileName(path));
                    if (path.ToLower().EndsWith(Program.EXPANSION_MP4))
                    {
                        File.Move(path, Program.PATH_VIDEO_ART + Path.GetFileName(path));
                        newArtVideosAdded = true;
                    }
                }
                catch { }
            }

            if (newDataAdded)
                Program.instance.InitializeForDataChange();
        }

        private static void ExportResult(bool sucess)
        {
            if (sucess)
            {
                MessageManager.Cast(InterString.Get("Export successful."));
                foreach(var file in filesToDelete)
                    File.Delete(file);
                filesToDelete.Clear();
            }
            else
                MessageManager.Cast(InterString.Get("Export failed."));
        }

        public static void ExportAllDecks()
        {
            if (!Directory.Exists(Program.PATH_DECK))
                Directory.CreateDirectory(Program.PATH_DECK);
            var filePaths = Directory.GetFiles(Program.PATH_DECK);
            Export(filePaths);
        }
        public static void ExportAllReplays()
        {
            if (!Directory.Exists(Program.PATH_REPLAY))
                Directory.CreateDirectory(Program.PATH_REPLAY);
            var filePaths = Directory.GetFiles(Program.PATH_REPLAY);
            Export(filePaths);
        }
        public static void ExportAllPictures()
        {
            if (!Directory.Exists(Program.PATH_CARD_PIC))
                Directory.CreateDirectory(Program.PATH_CARD_PIC);
            var filePaths = Directory.GetFiles(Program.PATH_CARD_PIC);
            Export(filePaths, false);
        }

        private static void Export(string[] filePaths, bool copy = true)
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            NativeFilePicker.ExportMultipleFiles(filePaths, ExportResult);
            if(!copy)
                filesToDelete = filePaths.ToList();
#else
            StandaloneFileBrowser.OpenFolderPanelAsync(InterString.Get("Please select an export directory"), "", false, (string[] paths) =>
            {
                ExportFiles(paths, filePaths, copy);
            });
#endif
        }

        private static void ExportFiles(string[] result, string[] filePaths, bool copy = true)
        {
            try
            {
                foreach (var file in filePaths)
                {
                    if (copy)
                        File.Copy(file, Path.Combine(result.FirstOrDefault(), Path.GetFileName(file)));
                    else
                        File.Move(file, Path.Combine(result.FirstOrDefault(), Path.GetFileName(file)));
                }
                ExportResult(true);
            }
            catch
            {
                ExportResult(false);
            }
        }
    }

}
