namespace LaoDai.BattleSpirits.Integration
{
    /// <summary>
    /// Coordinates the existing Boot curtain with the independently loaded Main UI.
    /// Tokens prevent a late completion from an earlier boot from revealing Main.
    /// </summary>
    public static class BattleStartupState
    {
        private static readonly object Gate = new object();
        private static int generation;
        private static bool active;
        private static bool managersReady;
        private static bool menuReady;
        private static bool wallpaperReady;
        private static string failureMessage;

        public static int CurrentToken { get { lock (Gate) return generation; } }
        public static bool IsActive { get { lock (Gate) return active; } }
        public static bool IsReady { get { lock (Gate) return Ready; } }
        public static bool HasFailed { get { lock (Gate) return active && failureMessage != null; } }
        public static string FailureMessage { get { lock (Gate) return failureMessage; } }

        private static bool Ready => active && failureMessage == null
            && managersReady && menuReady && wallpaperReady;

        public static int Begin()
        {
            lock (Gate)
            {
                generation = generation == int.MaxValue ? 1 : generation + 1;
                active = true;
                managersReady = false;
                menuReady = false;
                wallpaperReady = false;
                failureMessage = null;
                return generation;
            }
        }

        public static void MarkManagersReady(int token)
        {
            lock (Gate)
                if (Accepts(token)) managersReady = true;
        }

        public static void MarkMenuReady(int token)
        {
            lock (Gate)
                if (Accepts(token)) menuReady = true;
        }

        public static void MarkWallpaperReady(int token)
        {
            lock (Gate)
                if (Accepts(token)) wallpaperReady = true;
        }

        public static void Fail(int token, string message)
        {
            lock (Gate)
            {
                if (!Accepts(token) || failureMessage != null) return;
                failureMessage = string.IsNullOrWhiteSpace(message)
                    ? "Không thể khởi động. Hãy mở lại trò chơi."
                    : message;
            }
        }

        public static void Complete(int token)
        {
            lock (Gate)
                if (Accepts(token) && Ready) active = false;
        }

        public static void Cancel(int token)
        {
            lock (Gate)
                if (Accepts(token)) active = false;
        }

        private static bool Accepts(int token) => active && token != 0 && token == generation;
    }
}
