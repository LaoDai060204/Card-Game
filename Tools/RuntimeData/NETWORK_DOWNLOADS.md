# Optional network metadata and expansion downloads

The English repair keeps installed expansion packs until a replacement has
downloaded completely and passed its size/range checks. Download failures,
incorrect or empty range responses, and cancelled coroutines dispose requests,
remove their private temporary file, and release the busy UI state. A first
download creates the missing `Expansions` directory. Completed updates publish
their ETag and installed language together.

Manual package URLs must use HTTP or HTTPS and end in `.zip` or `.ypk` in the URL
path. Signed query parameters are preserved in the request but excluded from
the Windows filename. Windows-invalid filenames and path separators are rejected
on every platform. HTTPS uses Unity's certificate verification; the previous
accept-all certificate retry path has been removed.

The original `ElderLich/TransSuperpre` version endpoint returned HTTP 404 during
this repair. Its known retired defaults are skipped without discarding existing
cards or rewriting a user's configured custom URL. Metadata requests and package
downloads have bounded timeouts. Version responses must contain a plausible
nonempty first line rather than an HTML error page.

MyCard feed parsing accepts the service's absent-news forms (`null`, `[]`, or a
missing property). Malformed remote data does not replace the valid cache.
Initialization signals completion even without available news, allowing the menu
loader to finish. Request diagnostics omit URL credentials, query and fragment.

The OpenUPM scoped registry uses `https://package.openupm.com`; package versions
and the existing lockfile are unchanged. The cloud proxy blocked that domain
during this task, so live Unity package restoration still requires access to
`package.openupm.com` in the environment's network settings.

Run from the repository root with Python 3 and .NET SDK 10 in PATH:

```bash
python3 Tools/Tests/validate_network_metadata.py
python3 Tools/Tests/DownloadLifecycle.py
```

The first harness executes 82 production URL/version/news/download-helper checks,
including real filesystem writes and failed-publication preservation. The second
compiles and runs production C# 9 coroutine methods through 11 controlled
transport scenarios. These checks do not run Unity, contact live MyCard services,
perform TLS handshakes through Unity, or replace Windows Play/build validation.
