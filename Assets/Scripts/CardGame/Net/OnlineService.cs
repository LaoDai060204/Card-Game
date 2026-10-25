using Cysharp.Threading.Tasks;
using CardGame.Duel.YGOSharp;
using Newtonsoft.Json;
using Percy;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace CardGame.Net
{
    public static class OnlineService
    {

        public static void Initialize()
        {
            _ = InitializeGenesysLflist();
            _ = InitializeMyCardAppsAsync();
            _ = InitializeIdChangeLog();
        }

        public static async UniTask InitializeAsync()
        {
            await InitializeGenesysLflist();
            await InitializeMyCardAppsAsync();
            await InitializeIdChangeLog();
        }

        #region Genesys lflist

        private const string URL_GENESYS_LFLIST = "https://cdntx.moecube.com/ygopro-genesys/lflist.conf";

        private static async UniTask InitializeGenesysLflist()
        {
            var eTag = await GetETagAsync(URL_GENESYS_LFLIST);
            if (string.IsNullOrEmpty(eTag))
                return;

            var configTag = Config.Get(GetLocalETagKey(URL_GENESYS_LFLIST), Config.EMPTY_STRING);
            if(!string.Equals(eTag, configTag, StringComparison.Ordinal))
            {
                Program.Debug("Update Genesys Lflist.");
                await DownloadGenesysLflist(eTag);
                BanlistManager.InitializeForGenesys();
            }
            else
                Program.Debug("Genesys Lflist do not need update.");
        }

        private static async UniTask DownloadGenesysLflist(string ETag)
        {
            using var request = UnityWebRequest.Get(URL_GENESYS_LFLIST);
            request.timeout = 15;

            try
            {
                await request.SendWebRequest();
            }
            catch
            {
                MessageManager.Cast(InterString.Get("Failed to download the Genesys banlist."));
                return;
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                File.WriteAllText(Program.PATH_DATA + BanlistManager.FILE_NAME_GENESYS, request.downloadHandler.text);
                Config.Set(GetLocalETagKey(URL_GENESYS_LFLIST), ETag);
                Config.Save();
            }
            else
                MessageManager.Cast(InterString.Get("Failed to download the Genesys banlist."));
        }

        #endregion

        #region MyCard Apps

        private const string URL_MYCARD_APPS = "https://cdntx.moecube.com/apps.json";
        private const string PATH_MYCARD_APPS = Program.PATH_DATA + "mycard_apps.json";
        public static MyCardNews myCardNews;

        private static async UniTask InitializeMyCardAppsAsync()
        {
            var eTag = await GetETagAsync(URL_MYCARD_APPS);
            if (!string.IsNullOrEmpty(eTag))
            {
                var configTag = Config.Get(GetLocalETagKey(URL_MYCARD_APPS), Config.EMPTY_STRING);
                if (!string.Equals(eTag, configTag, StringComparison.Ordinal))
                {
                    Program.Debug("Update MyCard Apps.");
                    await DownloadMyCardApps(eTag);
                }
                else
                    Program.Debug("MyCard Apps do not need update.");
            }

            ParseMyCardNews();
        }

        private static async UniTask DownloadMyCardApps(string ETag)
        {
            using var request = UnityWebRequest.Get(URL_MYCARD_APPS);
            request.timeout = 15;

            try
            {
                await request.SendWebRequest();
            }
            catch
            {
                Program.Debug("Failed to download MyCard apps.json.");
                return;
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                File.WriteAllText(PATH_MYCARD_APPS, request.downloadHandler.text);
                Config.Set(GetLocalETagKey(URL_MYCARD_APPS), ETag);
                Config.Save();
            }
            else
                Program.Debug("Failed to download MyCard apps.json.");
        }

        private static void ParseMyCardNews()
        {
            if (!File.Exists(PATH_MYCARD_APPS))
                return;

            var json = File.ReadAllText(PATH_MYCARD_APPS);
            json = json.Replace("\"news\":[]", "\"news\":{}");
            var apps = JsonConvert.DeserializeObject<MyCardApp[]>(json);
            foreach (var app in apps)
                if (app.id == "ygopro")
                {
                    myCardNews = app.news;
                    return;
                }
        }

        #endregion

        #region Id Change Log

        private const string URL_ID_CHANGE_LOG = "https://ygocdb-mirror.moecube.com/api/v0/idChangelog.jsonp";

        private static async UniTask InitializeIdChangeLog()
        {
            var eTag = await GetETagAsync(URL_ID_CHANGE_LOG);
            if(string.IsNullOrEmpty(eTag))
                return;
            var configTag = Config.Get(GetLocalETagKey(URL_ID_CHANGE_LOG), Config.EMPTY_STRING);
            if (!string.Equals(eTag, configTag, StringComparison.Ordinal))
            {
                Program.Debug("Update Id Change Log.");
                await DownloadIdChangeLog(eTag);
                //
            }
            else
                Program.Debug("Id Change Log do not need update.");
        }

        private static async UniTask DownloadIdChangeLog(string ETag)
        {
            using var request = UnityWebRequest.Get(URL_ID_CHANGE_LOG);
            request.timeout = 15;

            try
            {
                await request.SendWebRequest();
            }
            catch
            {
                MessageManager.Cast(InterString.Get("Failed to download IdChangeLog."));
                return;
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                File.WriteAllText(Program.PATH_DATA + YdkIdHelper.FILE_NAME, request.downloadHandler.text);
                Config.Set(GetLocalETagKey(URL_ID_CHANGE_LOG), ETag);
                Config.Save();
                YdkIdHelper.SetDirty();
            }
            else
                MessageManager.Cast(InterString.Get("Failed to download IdChangeLog."));
        }

        #endregion

        #region User Config Sync (Rarity.json)

        // User configuration endpoints of the online deck service:
        // GET  /api/mdpro3/user/config/{userId}
        // POST /api/mdpro3/user/config  body: {userId, config, extra1~5}
        // extra1 stores the last upload time of Rarity.json as UTC milliseconds for freshness comparison.
        private const string URL_USER_CONFIG = "http://rarnu.xyz:38383/api/mdpro3/user/config";
        private const string REQ_SOURCE_HEADER = "ReqSource";
        private const string REQ_SOURCE_VALUE = "MDPro3";
        private const string TOKEN_HEADER = "token";
        private const string JSON_CONTENT_TYPE = "application/json";

        private static bool raritySynced;

        // Startup sync entry point, called by InitializeAsync and after MyCard login succeeds (OnlineServant.LoginSuccessEvent).
        // CardRarity loads lazily; writing the server copy before its first load makes that copy take effect.
        // If it has already loaded, ApplyServerContent also refreshes the memory cache.
        public static async UniTask SyncRarityConfigAsync()
        {
            if (raritySynced || MyCard.account == null || MyCard.account.user.id == 0)
                return;
            raritySynced = true;
            try
            {
                await SyncRarityWithServer(MyCard.account.user.id, MyCard.account.token);
            }
            catch (Exception e)
            {
                raritySynced = false; // Allow another attempt at the next login after a failure
                Program.Debug("Rarity.json sync failed: " + e.Message);
            }
        }

        private static async UniTask SyncRarityWithServer(int userId, string token)
        {
            var (ok, remote) = await GetUserConfigAsync(userId, token);
            if (!ok)
            {
                // A network failure does not mean the server has no configuration; abort to avoid uploading over unknown data
                Program.Debug("Rarity.json sync aborted: the server is unreachable.");
                return;
            }
            var localPath = CardRarity.GetFilePath();
            var localText = File.Exists(localPath) ? File.ReadAllText(localPath) : null;

            // The server is reachable but has no configuration: upload the local copy
            if (remote == null || string.IsNullOrEmpty(remote.config))
            {
                if (!string.IsNullOrEmpty(localText))
                {
                    await UploadRarityAsync(userId, token, localText);
                    Program.Debug("Rarity.json sync: the server had no configuration; uploaded the local copy.");
                }
                else
                    Program.Debug("Rarity.json sync: neither local nor server data exists.");
                return;
            }

            // There is no local file: download the server copy
            if (string.IsNullOrEmpty(localText))
            {
                CardRarity.ApplyServerContent(remote.config);
                Program.Debug("Rarity.json sync: no local file existed; downloaded the server copy.");
                return;
            }

            // The contents match: no sync is needed
            if (localText == remote.config)
            {
                Program.Debug("Rarity.json sync: the contents match; no sync is needed.");
                return;
            }

            // Both copies exist and differ: compare timestamps and keep the newer copy
            var localMs = new DateTimeOffset(File.GetLastWriteTimeUtc(localPath), TimeSpan.Zero).ToUnixTimeMilliseconds();
            var remoteMs = ParseLong(remote.extra1);
            if (remoteMs > localMs)
            {
                CardRarity.ApplyServerContent(remote.config);
                Program.Debug("Rarity.json sync: replaced the local copy with the newer server copy.");
            }
            else
            {
                await UploadRarityAsync(userId, token, localText);
                Program.Debug("Rarity.json sync: uploaded the newer local copy.");
            }
        }

        // Return (ok, cfg): ok=false means a network or HTTP failure; cfg=null means no server configuration only when ok=true.
        private static async UniTask<(bool ok, UserConfigData cfg)> GetUserConfigAsync(int userId, string token)
        {
            using var request = UnityWebRequest.Get($"{URL_USER_CONFIG}/{userId}");
            request.timeout = 15;
            request.SetRequestHeader(REQ_SOURCE_HEADER, REQ_SOURCE_VALUE);
            request.SetRequestHeader(TOKEN_HEADER, token);

            try
            {
                await request.SendWebRequest();
            }
            catch (UnityWebRequestException e)
            {
                // UniTask throws UnityWebRequestException when result is not Success; include the status code and body for diagnosis
                var body = e.Text ?? "";
                Program.Debug($"Failed to fetch user configuration: HTTP {e.ResponseCode} {e.Error} {body}");
                return (false, null);
            }
            catch (Exception e)
            {
                Program.Debug("Failed to fetch user configuration: " + e.Message);
                return (false, null);
            }

            // Support responses with a data wrapper as well as unwrapped objects
            var response = JsonUtility.FromJson<UserConfigResponse>(request.downloadHandler.text);
            if (response == null)
                return (true, null);
            if (response.data != null && !string.IsNullOrEmpty(response.data.config))
                return (true, response.data);
            if (!string.IsNullOrEmpty(response.config))
                return (true, new UserConfigData
                {
                    userId = response.userId,
                    config = response.config,
                    extra1 = response.extra1,
                    extra2 = response.extra2,
                    extra3 = response.extra3,
                    extra4 = response.extra4,
                    extra5 = response.extra5
                });
            return (true, null);
        }

        private static async UniTask UploadRarityAsync(int userId, string token, string configJson)
        {
            var body = new PostUserConfigBody
            {
                userId = userId,
                config = configJson,
                extra1 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
            };

            var json = JsonUtility.ToJson(body);
            using var request = UnityWebRequest.Post(URL_USER_CONFIG, json, JSON_CONTENT_TYPE);
            request.timeout = 15;
            request.SetRequestHeader(REQ_SOURCE_HEADER, REQ_SOURCE_VALUE);
            request.SetRequestHeader("Content-Type", JSON_CONTENT_TYPE);
            request.SetRequestHeader(TOKEN_HEADER, token);

            try
            {
                await request.SendWebRequest();
            }
            catch (UnityWebRequestException e)
            {
                var respBody = e.Text ?? "";
                Program.Debug($"Failed to upload user configuration: HTTP {e.ResponseCode} {e.Error} {respBody}");
                return;
            }
            catch (Exception e)
            {
                Program.Debug("Failed to upload user configuration: " + e.Message);
                return;
            }

            var response = JsonUtility.FromJson<UserConfigPostResponse>(request.downloadHandler.text);
            if (response == null || response.code != 0)
                Program.Debug("User configuration upload was rejected: " + (response?.message ?? request.downloadHandler.text));
            else
                Program.Debug("User configuration uploaded successfully.");
        }

        private static long ParseLong(string value)
        {
            return long.TryParse(value, out var result) ? result : 0;
        }

        [Serializable]
        public class UserConfigData
        {
            public int userId;
            public string config;
            public string extra1;
            public string extra2;
            public string extra3;
            public string extra4;
            public string extra5;
        }

        [Serializable]
        public class UserConfigResponse
        {
            public int code;
            public string message;
            public int userId;
            public string config;
            public string extra1;
            public string extra2;
            public string extra3;
            public string extra4;
            public string extra5;
            public UserConfigData data;
        }

        [Serializable]
        public class UserConfigPostResponse
        {
            public int code;
            public string message;
        }

        [Serializable]
        public class PostUserConfigBody
        {
            public int userId;
            public string config;
            public string extra1 = string.Empty;
            public string extra2 = string.Empty;
            public string extra3 = string.Empty;
            public string extra4 = string.Empty;
            public string extra5 = string.Empty;
        }

        #endregion

        #region Online Tools

        private static string GetLocalETagKey(string url) => $"ETag_{url.GetHashCode()}";

        public static async UniTask<string> GetETagAsync(string url)
        {
            using var headRequest = UnityWebRequest.Head(url);
            headRequest.timeout = 8;
            try
            {
                await headRequest.SendWebRequest();
            }
            catch
            {
                return null;
            }

            if(headRequest.result != UnityWebRequest.Result.Success)
            {
                Program.Debug($"HEAD({url}) request failed: {headRequest.error}");
                return null;
            }

            var onlineETag = headRequest.GetResponseHeader("ETag")?.Trim();
            if (string.IsNullOrEmpty(onlineETag))
            {
                Program.Debug($"ETag not found({url}); server-side caching may be disabled");
                return null;
            }
            return onlineETag;
        }

        #endregion

    }
}
