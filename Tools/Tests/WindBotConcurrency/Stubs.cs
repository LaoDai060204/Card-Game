using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Threading;
using YGOSharp.OCGWrapper;

namespace MDPro3
{
    public static class Program { public const string PATH_EXPANSIONS = "missing-expansions/"; }
}
namespace MDPro3.Utility { public static class Language { public static string GetCardConfig() => "en-US"; } }
namespace MDPro3.IO
{
    public static class ResourceManager { public static List<string> GetAllCdbInZipPaths() => null; }
}
namespace WindBot
{
    public static class Logger
    {
        public static void WriteLine(string text) { }
        public static void WriteErrorLine(string text) { }
        public static void DebugWriteLine(string text) { }
    }
}
namespace WindBot.Game.AI
{
    public static class DecksManager { public static void Init() => ConcurrencyFixtures.BeforeDeckInit?.Invoke(); }
}
namespace WindBot.Game
{
    public sealed class FakeConnection { public bool IsConnected => false; }
    public sealed class GameClient
    {
        private readonly WindBotInfo info;
        public readonly FakeConnection Connection = new FakeConnection();
        public string Username => info.Name;
        public GameClient(WindBotInfo info) { this.info = info; }
        public void Start()
        {
            ConcurrencyFixtures.BeforeClientStart?.Invoke();
            string worker = Thread.CurrentThread.Name ?? "main";
            ConcurrencyFixtures.Started[worker] = info;
            ConcurrencyFixtures.Randoms[worker] = WindBot.Program.Rand;
        }
        public void Tick() { }
    }
}
namespace YGOSharp.OCGWrapper { public static class CardsManager { internal static Card GetCard(int id) => null; } }

internal static class ConcurrencyFixtures
{
    public static Action BeforeDeckInit, BeforeClientStart;
    public static readonly ConcurrentDictionary<string, WindBot.WindBotInfo> Started = new ConcurrentDictionary<string, WindBot.WindBotInfo>();
    public static readonly ConcurrentDictionary<string, Random> Randoms = new ConcurrentDictionary<string, Random>();
    public static readonly ConcurrentDictionary<string, Func<DbDataReader>> Sources = new ConcurrentDictionary<string, Func<DbDataReader>>();
    public static readonly ConcurrentDictionary<string, string> Opened = new ConcurrentDictionary<string, string>();

    public static string Register(string filename, Func<DbDataReader> reader)
    {
        string path = Path.GetFullPath(filename); File.WriteAllText(path, "database fixture"); Sources[path] = reader; return path;
    }
    public static DbDataReader Rows(string prefix, int count, Action<int> beforeRead = null)
    {
        var table = new DataTable();
        foreach (string name in new[] { "id", "ot", "alias" }) table.Columns.Add(name, typeof(int));
        table.Columns.Add("setcode", typeof(long));
        foreach (string name in new[] { "type", "level", "race", "attribute", "atk", "def" }) table.Columns.Add(name, typeof(int));
        table.Columns.Add("name", typeof(string)); table.Columns.Add("desc", typeof(string));
        for (int id = 1; id <= count; id++)
            table.Rows.Add(id, 0, 0, 0L, 1, 4, 1, 1, 1000, 1000, prefix + "-" + id, "Description " + id);
        return new ControlledReader(table.CreateDataReader(), beforeRead);
    }
}

internal sealed class ControlledReader : DbDataReader
{
    private readonly DbDataReader inner;
    private readonly Action<int> beforeRead;
    private int reads;
    public ControlledReader(DbDataReader inner, Action<int> beforeRead) { this.inner = inner; this.beforeRead = beforeRead; }
    public override bool Read() { beforeRead?.Invoke(++reads); return inner.Read(); }
    public override int Depth => inner.Depth;
    public override int FieldCount => inner.FieldCount;
    public override bool HasRows => inner.HasRows;
    public override bool IsClosed => inner.IsClosed;
    public override int RecordsAffected => inner.RecordsAffected;
    public override object this[int ordinal] => inner[ordinal];
    public override object this[string name] => inner[name];
    public override void Close() => inner.Close();
    public override bool NextResult() => inner.NextResult();
    public override string GetName(int ordinal) => inner.GetName(ordinal);
    public override int GetOrdinal(string name) => inner.GetOrdinal(name);
    public override string GetDataTypeName(int ordinal) => inner.GetDataTypeName(ordinal);
    public override Type GetFieldType(int ordinal) => inner.GetFieldType(ordinal);
    public override object GetValue(int ordinal) => inner.GetValue(ordinal);
    public override int GetValues(object[] values) => inner.GetValues(values);
    public override bool IsDBNull(int ordinal) => inner.IsDBNull(ordinal);
    public override bool GetBoolean(int ordinal) => inner.GetBoolean(ordinal);
    public override byte GetByte(int ordinal) => inner.GetByte(ordinal);
    public override long GetBytes(int ordinal, long offset, byte[] buffer, int bufferOffset, int length) => inner.GetBytes(ordinal, offset, buffer, bufferOffset, length);
    public override char GetChar(int ordinal) => inner.GetChar(ordinal);
    public override long GetChars(int ordinal, long offset, char[] buffer, int bufferOffset, int length) => inner.GetChars(ordinal, offset, buffer, bufferOffset, length);
    public override DateTime GetDateTime(int ordinal) => inner.GetDateTime(ordinal);
    public override decimal GetDecimal(int ordinal) => inner.GetDecimal(ordinal);
    public override double GetDouble(int ordinal) => inner.GetDouble(ordinal);
    public override float GetFloat(int ordinal) => inner.GetFloat(ordinal);
    public override Guid GetGuid(int ordinal) => inner.GetGuid(ordinal);
    public override short GetInt16(int ordinal) => inner.GetInt16(ordinal);
    public override int GetInt32(int ordinal) => inner.GetInt32(ordinal);
    public override long GetInt64(int ordinal) => inner.GetInt64(ordinal);
    public override string GetString(int ordinal) => inner.GetString(ordinal);
    public override IEnumerator GetEnumerator() => ((IEnumerable)inner).GetEnumerator();
}

namespace Mono.Data.Sqlite
{
    public sealed class SqliteConnection : IDisposable
    {
        internal readonly string Path;
        public SqliteConnection(string connectionString) { Path = System.IO.Path.GetFullPath(connectionString.Substring("Data Source=".Length)); }
        public void Open() { ConcurrencyFixtures.Opened[Thread.CurrentThread.Name ?? "main"] = Path; }
        public void Dispose() { }
    }
    public sealed class SqliteCommand : IDbCommand
    {
        private readonly SqliteConnection connection;
        public SqliteCommand(string text, SqliteConnection connection) { this.connection = connection; CommandText = text; }
        public IDataReader ExecuteReader() => ConcurrencyFixtures.Sources[connection.Path]();
        public IDataReader ExecuteReader(CommandBehavior behavior) => ExecuteReader();
        public void Dispose() { }
        public string CommandText { get; set; }
        public int CommandTimeout { get; set; }
        public CommandType CommandType { get; set; }
        public IDbConnection Connection { get; set; }
        public IDataParameterCollection Parameters => null;
        public IDbTransaction Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; }
        public void Cancel() => throw new NotSupportedException();
        public IDbDataParameter CreateParameter() => throw new NotSupportedException();
        public int ExecuteNonQuery() => throw new NotSupportedException();
        public object ExecuteScalar() => throw new NotSupportedException();
        public void Prepare() => throw new NotSupportedException();
    }
}
