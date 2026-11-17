using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace MDPro3.Net
{
    internal static unsafe class Dll
    {
        [DllImport("ygoserver", CallingConvention = CallingConvention.Cdecl)]
        public static extern int start_server([MarshalAs(UnmanagedType.LPUTF8Str)] string args);
        [DllImport("ygoserver", CallingConvention = CallingConvention.Cdecl)]
        public static extern void stop_server();
    }

    public class YgoServer
    {
        private const int StopTimeoutMilliseconds = 3000;
        private static readonly object Gate = new object();
        private static ServerSession current;
        private static string lastError = string.Empty;

        private sealed class ServerSession
        {
            public Thread Thread;
            public bool StopRequested;
            public bool NativeReturned;
            public bool StopWorkerFinished;
        }

        // Kept for existing callers; lifecycle decisions use the session under Gate.
        public static Thread serverThread;

        public static bool IsBusy
        {
            get { lock (Gate) return current != null; }
        }

        public static bool IsStopping
        {
            get { lock (Gate) return current != null && current.StopRequested; }
        }

        public static string LastError
        {
            get { lock (Gate) return lastError; }
        }

        public static void StartServer(string args)
        {
            TryStartServer(args);
        }

        public static bool TryStartServer(string args)
        {
            lock (Gate)
            {
                // Never tear down an active native server to make room for another one.
                // In particular, a pending native stop must not reach a newer server.
                if (current != null)
                    return false;
                if (string.IsNullOrWhiteSpace(args))
                {
                    lastError = "Local server arguments are empty.";
                    return false;
                }
                var session = new ServerSession();
                session.Thread = new Thread(() => RunServer(session, args))
                {
                    IsBackground = true,
                    Name = "MDPro3 local duel server"
                };
                current = session;
                serverThread = session.Thread;
                lastError = string.Empty;
                try
                {
                    session.Thread.Start();
                    return true;
                }
                catch (Exception exception)
                {
                    current = null;
                    serverThread = null;
                    lastError = "Unable to start the local server thread: " + exception.Message;
                    return false;
                }
            }
        }

        private static void RunServer(ServerSession session, string args)
        {
            try
            {
                lock (Gate)
                    if (session.StopRequested)
                        return;
                int result = Dll.start_server(args);
                if (result != 0)
                    SetError(session, "Local server exited with code " + result + ".");
            }
            catch (Exception exception)
            {
                SetError(session, "Unable to run the local server: " + exception.Message);
            }
            finally
            {
                lock (Gate)
                {
                    session.NativeReturned = true;
                    ReleaseCompletedSession(session);
                }
            }
        }

        public static void StopServer()
        {
            lock (Gate)
            {
                var session = current;
                if (session == null || session.StopRequested)
                    return;
                session.StopRequested = true;
                try
                {
                    new Thread(() => StopOnWorker(session))
                    {
                        IsBackground = true,
                        Name = "MDPro3 local server shutdown"
                    }.Start();
                }
                catch (Exception exception)
                {
                    // Fail closed: never call into native shutdown on the Unity caller.
                    lastError = "Unable to queue local server shutdown: " + exception.Message;
                }
            }
        }

        private static void StopOnWorker(ServerSession session)
        {
            try
            {
                var timeout = Stopwatch.StartNew();
                while (session.Thread.IsAlive && timeout.ElapsedMilliseconds < StopTimeoutMilliseconds)
                {
                    // The first request can precede native listener initialization.
                    // Repeating the idempotent stop after a short join closes that race.
                    Dll.stop_server();
                    if (session.Thread.Join(100))
                        break;
                }
                if (session.Thread.IsAlive)
                    SetError(session, "Local server shutdown timed out. A new local server cannot start until the old one exits.");
            }
            catch (Exception exception)
            {
                SetError(session, "Unable to stop the local server: " + exception.Message);
            }
            finally
            {
                lock (Gate)
                {
                    session.StopWorkerFinished = true;
                    ReleaseCompletedSession(session);
                }
            }
        }

        public static bool ServerRunning()
        {
            lock (Gate)
                return current != null && !current.StopRequested && current.Thread.IsAlive;
        }

        private static void SetError(ServerSession session, string message)
        {
            lock (Gate)
                if (ReferenceEquals(current, session))
                    lastError = message;
        }

        // Caller holds Gate. Both workers must finish before another native start.
        private static void ReleaseCompletedSession(ServerSession session)
        {
            if (!ReferenceEquals(current, session) || !session.NativeReturned
                || (session.StopRequested && !session.StopWorkerFinished))
                return;
            current = null;
            serverThread = null;
        }
    }
}
