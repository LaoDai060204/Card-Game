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

    private static void WaitAtTitle(BattleTitleFlow flow)
    {
        flow.Ready();
        for (int frame = 0; frame < 5; frame++) flow.Advance(0.1f, false, false);
    }

    private static BattleTitleFlow BeginReveal()
    {
        var flow = new BattleTitleFlow();
        WaitAtTitle(flow);
        flow.Advance(0.016f, true, true);
        Assert(flow.Phase == BattleTitlePhase.Revealing, "A deliberate start press could not begin the reveal.");
        return flow;
    }

    private static void RunTitleFlowTests()
    {
        Case("Title cannot accept input or reveal before loading finishes", () =>
        {
            var flow = new BattleTitleFlow();
            for (int frame = 0; frame < 100; frame++) flow.Advance(0.1f, frame % 2 == 0, frame % 2 == 0);
            Assert(flow.Phase == BattleTitlePhase.Loading && flow.RevealProgress == 0,
                "A preloading input or elapsed time revealed the unfinished menu.");
        });

        Case("Ready leaves the title waiting indefinitely for an explicit start press", () =>
        {
            var flow = new BattleTitleFlow();
            flow.Ready();
            for (int frame = 0; frame < 1000; frame++) flow.Advance(0.1f, false, false);
            Assert(flow.Phase == BattleTitlePhase.WaitingForStart && flow.RevealProgress == 0,
                "The title automatically entered the menu without a player action.");
        });

        Case("A held startup key must be released before a fresh start press can enter", () =>
        {
            var flow = new BattleTitleFlow();
            flow.Advance(0.1f, true, true);
            flow.Ready();
            for (int frame = 0; frame < 10; frame++) flow.Advance(0.1f, frame == 5, true);
            Assert(flow.Phase == BattleTitlePhase.WaitingForStart, "A key held through loading skipped the title.");
            flow.Advance(0.016f, false, false);
            flow.Advance(0.016f, true, true);
            Assert(flow.Phase == BattleTitlePhase.Revealing, "Releasing and pressing again failed to enter.");
        });

        Case("A release observed only during loading does not arm the later title", () =>
        {
            var flow = new BattleTitleFlow();
            flow.Advance(0.1f, false, false);
            flow.Ready();
            for (int frame = 0; frame < 10; frame++) flow.Advance(0.1f, false, true);
            flow.Advance(0.016f, true, true);
            Assert(flow.Phase == BattleTitlePhase.WaitingForStart, "Pre-title neutral input armed a held startup button.");
        });

        Case("An early start press is discarded rather than buffered past the title dwell", () =>
        {
            var flow = new BattleTitleFlow();
            flow.Ready();
            flow.Advance(0.01f, false, false);
            flow.Advance(0.01f, true, true);
            Assert(flow.Phase == BattleTitlePhase.WaitingForStart, "A press before the minimum title duration was accepted.");
            for (int frame = 0; frame < 10; frame++) flow.Advance(0.1f, false, true);
            Assert(flow.Phase == BattleTitlePhase.WaitingForStart, "An early press was buffered until the title timeout.");
            flow.Advance(0.01f, false, false);
            flow.Advance(0.01f, true, true);
            Assert(flow.Phase == BattleTitlePhase.Revealing, "A later deliberate press was lost with the earlier press.");
        });

        Case("A fresh press begins the reveal with a still-covered menu", () =>
        {
            var flow = BeginReveal();
            Assert(flow.RevealProgress == 0, "The start input skipped the beginning of the fade.");
            flow.Advance(0.05f, false, false);
            Assert(flow.Phase == BattleTitlePhase.Revealing && flow.RevealProgress > 0 && flow.RevealProgress < 1,
                "A normal frame did not produce a partial fade.");
        });

        Case("Reveal progresses monotonically to one and completes exactly once", () =>
        {
            var flow = BeginReveal();
            float previous = flow.RevealProgress;
            for (int frame = 0; frame < 30; frame++)
            {
                flow.Advance(0.05f, frame % 2 == 0, frame % 2 == 0);
                Assert(float.IsFinite(flow.RevealProgress) && flow.RevealProgress >= previous && flow.RevealProgress <= 1,
                    "Repeated input reversed, overflowed or invalidated the reveal.");
                previous = flow.RevealProgress;
            }
            Assert(flow.Phase == BattleTitlePhase.Complete && flow.RevealProgress == 1,
                "A completed fade did not expose the menu fully.");
            flow.Ready();
            flow.Advance(1, true, true);
            Assert(flow.Phase == BattleTitlePhase.Complete && flow.RevealProgress == 1,
                "A completed title re-entered loading or restarted its animation.");
        });

        Case("Repeated readiness notifications do not reset the waiting title or its input arm", () =>
        {
            var flow = new BattleTitleFlow();
            WaitAtTitle(flow);
            flow.Ready();
            flow.Advance(0.016f, true, true);
            Assert(flow.Phase == BattleTitlePhase.Revealing, "A duplicate ready callback reset the readable duration or neutral input.");
            flow.Advance(0.1f, false, false);
            float progress = flow.RevealProgress;
            flow.Ready();
            Assert(flow.Phase == BattleTitlePhase.Revealing && flow.RevealProgress == progress,
                "A duplicate ready callback reset an in-progress fade.");
        });

        Case("Invalid or paused frame durations cannot advance an unfinished reveal", () =>
        {
            var flow = BeginReveal();
            foreach (float delta in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                flow.Advance(delta, false, false);
                Assert(flow.Phase == BattleTitlePhase.Revealing && flow.RevealProgress == 0,
                    "A zero, negative or non-finite frame duration changed the fade.");
            }
            flow.Advance(0.1f, false, false);
            Assert(flow.RevealProgress > 0 && flow.RevealProgress < 1, "Valid time did not recover from an invalid frame.");
        });

        Case("A stalled frame is bounded instead of skipping the title or full reveal", () =>
        {
            var flow = new BattleTitleFlow();
            flow.Ready();
            flow.Advance(float.MaxValue, false, false);
            flow.Advance(0, true, true);
            Assert(flow.Phase == BattleTitlePhase.WaitingForStart, "A single long frame skipped the minimum readable duration.");
            WaitAtTitle(flow);
            flow.Advance(0.016f, true, true);
            flow.Advance(float.MaxValue, false, false);
            Assert(flow.Phase == BattleTitlePhase.Revealing && flow.RevealProgress > 0 && flow.RevealProgress < 1,
                "A long frame skipped the entire fade into the menu.");
        });

        Case("Unscaled frame time controls the complete duration without depending on input repetition", () =>
        {
            var flow = BeginReveal();
            float elapsed = 0;
            while (elapsed + 0.05f < BattleTitleFlow.RevealSeconds - 0.0001f)
            {
                flow.Advance(0.05f, false, false);
                elapsed += 0.05f;
                Assert(flow.Phase == BattleTitlePhase.Revealing, "The reveal completed before its configured duration.");
            }
            flow.Advance(0.1f, false, false);
            Assert(flow.Phase == BattleTitlePhase.Complete && flow.RevealProgress == 1,
                "The reveal stayed incomplete after enough unscaled frame time.");
        });

        Case("A new title instance does not inherit a prior completed entry", () =>
        {
            var prior = BeginReveal();
            for (int frame = 0; frame < 10; frame++) prior.Advance(0.1f, false, false);
            Assert(prior.Phase == BattleTitlePhase.Complete, "Fixture did not complete its first entry.");
            var next = new BattleTitleFlow();
            Assert(next.Phase == BattleTitlePhase.Loading && next.RevealProgress == 0,
                "A new Play session inherited old title state.");
            WaitAtTitle(next);
            Assert(next.Phase == BattleTitlePhase.WaitingForStart, "A previous entry automatically accepted the next title.");
        });
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

            int startupStateCases = passed;
            RunTitleFlowTests();
            Console.WriteLine(startupStateCases + " startup state + " + (passed - startupStateCases)
                + " title flow scenarios passed (actual production source, no Unity dependencies).");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
