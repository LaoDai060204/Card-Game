# Battle Spirits LAN / direct-IP server

This console application runs the same authoritative rules and TCP protocol as
hosting from the Unity game. It does not require Unity Editor or YgoServer.
The shared runtime is compiled against **.NET Standard 2.1 / C# 9**; the console
host requires **.NET SDK 10** to build and .NET 10 to run.

From the repository root:

```bash
dotnet run --project Tools/BattleSpiritsServer/BattleSpirits.Server.csproj -c Release -- --catalog Data/BattleSpirits/cards.json --port 7777 --format Standard
```

The first build restores the pinned Newtonsoft.Json 13.0.3 package. The Unity
client uses the project's existing `com.unity.nuget.newtonsoft-json` package.
The host prints the chosen port and catalog fingerprint. Press **Ctrl+C** to
stop. `--help` lists arguments; port `0` selects a free port for local testing.
**Standard is the default**. It uses the supplied Standard comprehensive/manual
rules and requires cards with verified Block A eligibility. The maintained
Eternal rules are available explicitly with `--format Eternal`; choosing Eternal
does not change a Standard card's legality or grant support for its effects.

On each game client, open **CHƠI MẠNG**, enter this server's address and port,
connect, select a supported deck, and mark ready. The player assigned seat 0
starts the match after both players are ready. Both clients must have the same
catalog, **protocol version 3**, and rules format. A mismatched format is rejected
before a welcome or private state is sent. Every match snapshot identifies its
format. The current Unity game uses Standard, so use the default server format
with it; `--format Eternal` is intended for an explicitly Eternal client.

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

Playable pools are checked separately for each format. The Standard pool includes
verified vanilla Spirits and specifically registered Super Unleash Spirits;
the retained Eternal pool includes its reviewed BS01 effects. Imported official
card text remains browse-only until its behavior is implemented and tested.
BS01-only Eternal decks are rejected by a Standard host. Console startup also
fails early if the selected catalog cannot provide a legal supported 40-card
deck for the chosen format.

## Verification

```bash
dotnet run --project Tests/BattleSpirits/Network/BattleSpirits.Network.Tests.csproj -c Release -- Data/BattleSpirits/cards.json
python Tests/BattleSpirits/Network/test_console.py --dotnet /path/to/dotnet
```

The regression suite uses real TCP sockets and the production .NET Standard
runtime. It covers a complete supported match, private-state filtering, invalid
commands, catalog/version mismatches, room replacement, backpressure recovery
and stop/restart. Standard cases additionally cover exact format negotiation,
Eternal-card rejection, legal Block A decks, Second Main and a complete match.
The console smoke test launches both the default Standard host and an explicit
Eternal host, joins two clients and checks graceful shutdown. These tests run on Linux loopback; they do not
establish that two Windows Unity clients, graphics or a public Internet deployment
have been tested.
