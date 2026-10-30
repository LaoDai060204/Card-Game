# Battle Spirits LAN / direct-IP server

This console application runs the same authoritative rules and TCP protocol as
hosting from the Unity game. It does not require Unity Editor or YgoServer.
The shared runtime is compiled against **.NET Standard 2.1 / C# 9**; the console
host requires **.NET SDK 10** to build and .NET 10 to run.

From the repository root:

```bash
dotnet run --project Tools/BattleSpiritsServer/BattleSpirits.Server.csproj -c Release -- --catalog Data/BattleSpirits/cards.json --port 7777
```

The first build restores the pinned Newtonsoft.Json 13.0.3 package. The Unity
client uses the project's existing `com.unity.nuget.newtonsoft-json` package.
The host prints the chosen port and catalog fingerprint. Press **Ctrl+C** to
stop. `--help` lists arguments; port `0` selects a free port for local testing.

On each game client, open **CHƠI MẠNG**, enter this server's address and port,
connect, select a supported deck, and mark ready. The player assigned seat 0
starts the match after both players are ready. Both clients must have the same
catalog and protocol version.

- On the host computer itself, use `127.0.0.1`.
- On a second computer in the LAN, use the host computer's IPv4 address from
  `ipconfig`, for example `192.168.1.10`, and allow TCP 7777 through the host's
  Windows Firewall for that network.
- A direct Internet connection uses a reachable public server address and the
  same protocol. Router forwarding, public addressing or a VPN are deployment
  choices; this tool does not provision them or provide a matchmaking service.

The server has one two-player room. It validates decks, readiness, actor identity
and every match action. Player snapshots contain that player's hand plus public
counts and field/discard information; they do not contain the opponent's hand,
deck order or shuffle seed. A lost connection concedes an active match. A new
connection enters a fresh lobby rather than inheriting the disconnected player's
private state. Reconnection into an active match and accounts are not implemented.

The current playable pool is the supported vanilla Spirit subset. New official
ability cards in the catalog can be browsed, but the server rejects them in a
match deck until their behavior is implemented and tested.

## Verification

```bash
dotnet run --project Tests/BattleSpirits/Network/BattleSpirits.Network.Tests.csproj -c Release -- Data/BattleSpirits/cards.json
python Tests/BattleSpirits/Network/test_console.py --dotnet /path/to/dotnet
```

The regression suite uses real TCP sockets and the production .NET Standard
runtime. It covers a complete supported match, private-state filtering, invalid
commands, catalog/version mismatches, room replacement, backpressure recovery
and stop/restart. The console smoke test launches this executable and joins two
clients before shutting it down. These tests run on Linux loopback; they do not
establish that two Windows Unity clients, graphics or a public Internet deployment
have been tested.
