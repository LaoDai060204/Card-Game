using System;
using System.Collections.Concurrent;
using System.IO;

namespace UnityEngine
{
    public static class Debug
    {
        public static readonly ConcurrentQueue<string> Messages = new ConcurrentQueue<string>();
        public static void Log(object value) => Messages.Enqueue(value.ToString());
        public static void LogError(object value) => Log(value);
        public static void LogException(Exception value) => Log(value);
        public static void LogFormat(string format, params object[] values) => Log(string.Format(format, values));
    }
}
namespace MDPro3.Duel.YGOSharp { public sealed class Deck { } }
namespace MDPro3.Net
{
    public static class YgoServer { public static bool Running; public static bool ServerRunning() => Running; }
    public static class MyCard { public const string duelUrl = "mycard.invalid"; }
}
namespace MDPro3.Servant
{
    public static class RoomServant { public static bool FromHandTest; }
    public static class OcgCore { public static bool mycardDuel; }
}
namespace MDPro3
{
    public static class Config { public const int ClientVersion = 0x1353; }
    public static class InterString { public static string Get(string text) => text; }
    public static class MessageManager
    {
        public static string messageFromSubString;
        public static void Cast(string text) { }
    }
    public sealed class View { public bool showing; }
    public sealed class Room
    {
        public volatile bool duelEnded;
        public bool showing;
        public int LastChat, ChatCount;
        public void OnExit() { }
        @@ROOM_METHODS@@
    }
    public sealed class DuelView
    {
        public bool showing;
        public void StocMessage_TimeLimit(BinaryReader reader) { }
        public void StocMessage_TeammateSurrender() { }
        public void ForceMSquit() { }
    }
    public sealed class App
    {
        public readonly Room room = new Room();
        public readonly DuelView ocgcore = new DuelView();
        public readonly View online = new View { showing = true }, solo = new View(), deckEditor = new View();
        public void ShiftToServant(object servant) { }
    }
    public static class Program
    {
        public static volatile bool Running = true;
        public static readonly App instance = new App();
    }
}
