using System;
using LaoDai.BattleSpirits.Integration;

internal static class Entry
{
    private static int passed;

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Case(string name, Action test)
    {
        test();
        passed++;
        Console.WriteLine("PASS " + name);
    }

    private static void Mark(int token, int requirement)
    {
        switch (requirement)
        {
            case 0: BattleStartupState.MarkManagersReady(token); break;
            case 1: BattleStartupState.MarkMenuReady(token); break;
            case 2: BattleStartupState.MarkWallpaperReady(token); break;
            default: throw new ArgumentOutOfRangeException(nameof(requirement));
        }
    }

    private static void MarkAll(int token)
    {
        for (int requirement = 0; requirement < 3; requirement++) Mark(token, requirement);
    }

    public static int Main()
    {
        try
        {
            Case("Inactive startup does not report an unfinished or failed load", () =>
            {
                Assert(!BattleStartupState.IsActive && !BattleStartupState.IsReady && !BattleStartupState.HasFailed,
                    "A direct Main scene starts with an active, ready or failed Boot load.");
            });

            Case("Begin creates a fresh active generation", () =>
            {
                int token = BattleStartupState.Begin();
                Assert(token != 0 && BattleStartupState.CurrentToken == token, "Begin did not return its current nonzero generation.");
                Assert(BattleStartupState.IsActive && !BattleStartupState.IsReady && !BattleStartupState.HasFailed,
                    "A new load inherited completion or failure.");
                Assert(string.IsNullOrEmpty(BattleStartupState.FailureMessage), "A new load retained an error message.");
            });

            Case("An absent Boot token cannot release or fail the current startup", () =>
            {
                int token = BattleStartupState.Begin();
                MarkAll(0);
                BattleStartupState.Fail(0, "Direct Main callback");
                BattleStartupState.Complete(0);
                BattleStartupState.Cancel(0);
                Assert(BattleStartupState.CurrentToken == token && BattleStartupState.IsActive &&
                    !BattleStartupState.IsReady && !BattleStartupState.HasFailed,
                    "An unowned callback changed Boot's active load.");
            });

            for (int subset = 0; subset < 8; subset++)
            {
                int captured = subset;
                Case("Readiness requires all three prerequisites (mask " + captured + ")", () =>
                {
                    int token = BattleStartupState.Begin();
                    for (int requirement = 0; requirement < 3; requirement++)
                        if ((captured & (1 << requirement)) != 0) Mark(token, requirement);
                    Assert(BattleStartupState.IsActive && BattleStartupState.IsReady == (captured == 7),
                        "Loading can finish before managers, menu and wallpaper are all ready.");
                });
            }

            int[][] orders = {
                new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 },
                new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 }
            };
            foreach (int[] order in orders)
            {
                int[] captured = order;
                Case("Readiness is independent of callback order and duplicates (" + string.Join(",", captured) + ")", () =>
                {
                    int token = BattleStartupState.Begin();
                    for (int i = 0; i < captured.Length; i++)
                    {
                        Mark(token, captured[i]);
                        Mark(token, captured[i]);
                        Assert(BattleStartupState.IsReady == (i == 2), "A duplicated callback counted as another prerequisite.");
                    }
                });
            }

            Case("An initialization error remains failed after late success callbacks", () =>
            {
                int token = BattleStartupState.Begin();
                BattleStartupState.MarkManagersReady(token);
                BattleStartupState.Fail(token, "Menu prefab failed to load");
                MarkAll(token);
                BattleStartupState.Fail(token, "A later unrelated failure");
                BattleStartupState.Complete(token);
                Assert(BattleStartupState.HasFailed && !BattleStartupState.IsReady,
                    "Late callbacks or Complete dismissed a failed startup.");
                Assert(BattleStartupState.FailureMessage == "Menu prefab failed to load", "The first useful failure was replaced.");
            });

            Case("A failure before the loading screen closes revokes readiness", () =>
            {
                int token = BattleStartupState.Begin();
                MarkAll(token);
                Assert(BattleStartupState.IsReady, "Fixture did not reach readiness.");
                BattleStartupState.Fail(token, "Menu initialization failed");
                Assert(BattleStartupState.HasFailed && !BattleStartupState.IsReady, "A ready-but-uncommitted load swallowed a failure.");
            });

            Case("An empty failure still provides a readable startup error", () =>
            {
                foreach (string message in new[] { null, "", " \t\r\n" })
                {
                    int token = BattleStartupState.Begin();
                    BattleStartupState.Fail(token, message);
                    Assert(BattleStartupState.HasFailed && !BattleStartupState.IsReady &&
                        !string.IsNullOrWhiteSpace(BattleStartupState.FailureMessage),
                        "A failed load has no text for the retained loading screen.");
                }
            });

            Case("A new load resets an earlier failure and rejects every stale callback", () =>
            {
                int stale = BattleStartupState.Begin();
                BattleStartupState.Fail(stale, "Old failure");
                int current = BattleStartupState.Begin();
                Assert(current != stale && !BattleStartupState.HasFailed && string.IsNullOrEmpty(BattleStartupState.FailureMessage),
                    "A fresh generation retained the previous failure.");
                MarkAll(stale);
                BattleStartupState.Fail(stale, "Stale error");
                BattleStartupState.Cancel(stale);
                BattleStartupState.Complete(stale);
                Assert(BattleStartupState.CurrentToken == current && BattleStartupState.IsActive &&
                    !BattleStartupState.IsReady && !BattleStartupState.HasFailed, "An old scene changed the current load.");
                MarkAll(current);
                BattleStartupState.Complete(stale);
                Assert(BattleStartupState.IsActive && BattleStartupState.IsReady, "A stale completion closed the current loading screen.");
            });

            Case("Complete waits for readiness and ignores errors arriving after completion", () =>
            {
                int token = BattleStartupState.Begin();
                BattleStartupState.MarkManagersReady(token);
                BattleStartupState.MarkMenuReady(token);
                BattleStartupState.Complete(token);
                Assert(BattleStartupState.IsActive && !BattleStartupState.IsReady, "Complete bypassed the wallpaper barrier.");
                BattleStartupState.MarkWallpaperReady(token);
                BattleStartupState.Complete(token);
                Assert(!BattleStartupState.IsActive, "A ready loading screen could not complete.");
                BattleStartupState.Fail(token, "Late destroyed-scene callback");
                MarkAll(token);
                Assert(!BattleStartupState.IsActive && !BattleStartupState.HasFailed && string.IsNullOrEmpty(BattleStartupState.FailureMessage),
                    "A completed generation was revived by a late callback.");
            });

            Case("Cancel invalidates callbacks and the next Begin has a different token", () =>
            {
                int stale = BattleStartupState.Begin();
                BattleStartupState.Cancel(stale);
                MarkAll(stale);
                BattleStartupState.Fail(stale, "Cancelled wallpaper");
                Assert(!BattleStartupState.IsActive && !BattleStartupState.HasFailed, "A cancelled startup accepted late work.");
                int current = BattleStartupState.Begin();
                Assert(current != stale, "A cancelled generation was reused.");
                MarkAll(stale);
                Assert(!BattleStartupState.IsReady, "Cancelled callbacks released the next loading screen.");
                MarkAll(current);
                Assert(BattleStartupState.IsReady, "Cancelling the prior scene prevented the next scene from becoming ready.");
                BattleStartupState.Complete(current);
            });

            Console.WriteLine(passed + " startup state scenarios passed (actual production state, no Unity dependencies).");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
