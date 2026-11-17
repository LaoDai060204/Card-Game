# TCP transport regression checks

From the repository root with .NET SDK 10 in PATH:

```bash
python3 Tools/Tests/TcpTransport/run.py
```

The runner extracts the production `TcpHelper` connection, send, receive,
disconnect and initial-handshake methods, plus the production packet reader,
connector and fixed-width UTF-16 writer. It compiles them as C# 9 with the real
network enums. Unity views/logging are controlled stand-ins; transport uses real
loopback TCP sockets. A protected connect hook provides a deliberately pending
operation for a deterministic timeout check; a delayed reader exercises an old
session finishing after reconnect.

The 18 scenarios cover connection success/refusal/invalid ports/timeouts,
socket cleanup, fragmented frames, clean/truncated EOF, empty frames, fixed-field
widths/terminators/padding, long Vietnamese and emoji text, embedded NUL,
PlayerInfo/JoinGame wire sizes, duplicate joins/senders, background worker and
join-gate behavior, large frames, protocol length overflow, reconnect queues and
late old-session completion, stale main-frame disconnect cleanup and failed
connector isolation. This is a transport regression check, not a Unity
Play test, actual remote server test or completed duel.
