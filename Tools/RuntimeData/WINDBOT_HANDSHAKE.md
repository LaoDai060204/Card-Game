# WindBot player-name handshake fix

`GameClient.OnConnected` previously passed its full bot name directly into
`WriteUnicode(name, 20)`. The field contains **20 UTF-16 code units (40 bytes)**,
including a terminating NUL. It can hold at most **19 code units of name text**.
`Red Dragon Archfiend` is 20 units, so the Editor/Debug writer correctly rejected
it. The same defect affects 102 of the 489 shipped bot profiles.

The client now normalizes only the outgoing PlayerInfo name:

- Keep the prefix that fits the 19-unit text budget.
- Stop at an embedded NUL and keep a valid surrogate pair together at the cut.
- Preserve the original `Username`, `WindBotInfo.Name`, deck, deck-file path and
  dialogue selection. Those values still identify the original bot/profile.

The user example is sent as `Red Dragon Archfien`; the full name remains in its
configuration and logging. The shared writer stays strict in Debug, the server
still reads exactly 40 bytes, and packet IDs, join version, password and framing
are unchanged. Increasing the field size would violate the existing protocol.

Backup before this change: `backup/2026-11-14-before-windbot-name-fix`, verified
on origin at `6d5dfb409f27d820ce9267ba4b3fe72da76cee91`.

## Verification

```bash
python Tools/Tests/WindBotHandshake.py --dotnet /path/to/dotnet
```

The optional harness uses .NET SDK 10 outside Unity and compiles the actual
GameClient, WindBotInfo, packet factory, enum and binary writer with C# 9. Only
the socket transport/event source and unrelated duel behavior are stubbed.

**Debug: 504/504; Release: 504/504.** Each run covers 15 boundary scenarios and
489 original bot profiles from ten locales. Checks include the exact reported
name, the prior Horus regression, short names, 19/20-unit boundaries, null/empty
names, embedded NUL, supplementary Unicode at the boundary, NUL padding,
unchanged profile fields and the one-time three-packet handshake. The strict
Debug writer still rejects an unnormalized oversized name.

The captured PlayerInfo packet is 41 bytes (one message-type byte plus the
40-byte name field), excluding transport length framing. JoinGame remains 49
bytes. No actual TCP connection, Unity Editor Play, AI duel, shader/render test
or Windows build was run for this change. Existing gameplay/AI startup issues
outside name serialization are not addressed by this fix.

After a normal update on `mdpro3-english`, reopen Unity 6000.0.24f1, start from
Boot and try SOLO with Red Dragon Archfiend or another long English bot name.
