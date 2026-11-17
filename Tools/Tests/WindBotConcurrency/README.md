# Embedded WindBot concurrency

Run `python3 Tools/Tests/WindBotConcurrency/run.py` with .NET SDK 10 in PATH.
The harness compiles actual `WindBot.Program`, argument parsing, `WindBotInfo`,
`NamedCardsManager` and card classes. Controlled SQLite readers pause partway
through database loading; controlled clients capture each bot's options.

Ten cases exercise overlapping startup, command/file precedence, complete
catalog publication, retained data after a failed reload, expansion override
order, concurrent readers/reloaders, and independent worker Random instances.
Database I/O and network transport are stand-ins. This is not a Unity Play or
full bot-duel test; real transport is covered separately by `TcpTransport`.
