# Optional news and avatar image failures

The reported image URL is supplied by the upstream English MyCard news feed,
not a hardcoded project picture or a duel/card resource:

- Feed: `https://cdntx.moecube.com/apps.json`
- Record: application `ygopro`, `news.en-US[0]`
- Title: **MyCard V3 User Guide**
- Thumbnail: `http://pic.4j4j.cn/upload/pic/20140327/af650cf463.jpg`
- Article: `https://ygobbs2.com/t/MyCard-V3-User-Guide/82335`

On 2026-11-14 the public feed still referenced this image and DNS lookup for its
host failed in the audit environment, matching the user's error. The cached
public-feed snapshot SHA-256 was
`b9880bc11ab5e40e401de9b67c08d1850c0429e543d5cf5c671cba85b92fb923`.
The client cannot restore an unavailable upstream host; the original image will
require its owner to repair or replace that URL.

## Client behavior

`Tools.DownloadImageAsync` keeps its public Task/cancellation API and delegates
to `Utility/RemoteImageDownloader.cs`. The optional-image downloader validates
absolute HTTP(S) URLs, sets Unity's request timeout to ten seconds and disposes
requests. DNS/HTTP/decoding failures return null with an informative warning.
Repeated warnings use a bounded 128-URL cache; failed downloads are not cached,
so later retries can recover. Explicit cancellation aborts and propagates the
cancellation; stopped Play aborts without assigning a late result. TLS verification
and the original HTTP/HTTPS scheme are retained.

News entries retain their **English title and source link** when a thumbnail
fails. A built-in black texture supplies a neutral local background, fitting
the existing dark news panel. There is no substituted remote picture or Chinese
news fallback. The same placeholder is used for a not-yet-loaded image.

News reloads snapshot their entries, cancel the previous generation and release
only owned downloaded textures. A superseded result cannot repaint the latest
feed. Built-in fallback textures are never destroyed. Closing the panel before
its first image arrives keeps it closed. Destroying the owner cancels pending
work and discards late images. A failed optional account-avatar task leaves the
existing avatar rather than accessing a faulted Task.Result.

A warning about the original image URL can still appear; the source fault remains
visible in diagnostics while the news item remains usable. This change does not
hide unrelated account, gameplay, card-data or transport errors.

## Verification and limits

```bash
dotnet run --project Tools/Tests/RemoteImages/RemoteImages.Tests.csproj -c Release
dotnet run --project Tools/Translations/tests/EnglishMigration.Tests.csproj -c Release -- /path/to/Card-Game
```

The optional harnesses use .NET SDK 10 outside Unity. **21 downloader scenarios**
compile the actual helper with controlled Unity/request APIs; **23 locale/news
scenarios** compile the actual NewsManager and localization helpers with controlled
image tasks. These include fallback, captions/links/indexes, empty feeds, reload,
ownership, cancellation, stale completion, closed panels and stopped Play. The
original twelve English-localization/news scenarios remain covered.

Four changed production C# files pass three C# 9 syntax profiles. Public feed
content and host DNS were inspected read-only; the request tests are simulated.
**Unity Editor Play, native texture decoding/rendering, real request timing, device
input and Windows player builds have not been verified here.** Existing metadata,
card images/data, platform bundles, rules and network packets are unchanged.

Backup before this change: `backup/2026-11-14-before-remote-image-fallback`, verified
on origin at `d750d615332cf92e602f5c576bbf27e648288f5e`.
