using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class IntegrationAudit
{
    private static readonly List<Finding> findings = new List<Finding>();
    private static readonly HashSet<string> memberOwners = new HashSet<string>(StringComparer.Ordinal)
    {
        "MDPro3.Duel.YGOSharp.CardsManager", "MDPro3.Duel.YGOSharp.PacksManager",
        "MDPro3.BattleSpirits.BattleIntegration", "MDPro3.BattleSpirits.BattleCatalog",
        "MDPro3.BattleSpirits.BattleDeckStorage", "MDPro3.BattleSpirits.BattleCardAdapter"
    };
    private static string root;
    private sealed class Finding
    {
        public string severity { get; set; }
        public string check { get; set; }
        public string file { get; set; }
        public int line { get; set; }
        public string message { get; set; }
    }
    private static void Add(string check, string message, SyntaxNode node = null, string path = "", string severity = "error")
    {
        findings.Add(new Finding { severity = severity, check = check, message = message,
            file = node == null ? path : Path.GetRelativePath(root, node.SyntaxTree.FilePath),
            line = node == null ? 0 : node.GetLocation().GetLineSpan().StartLinePosition.Line + 1 });
    }

    public static int Main(string[] args)
    {
        root = Path.GetFullPath(args[0]);
        var changed = JsonSerializer.Deserialize<string[]>(File.ReadAllText(args[1]));
        bool incomplete = bool.Parse(args[3]);
        var player = new CSharpParseOptions(LanguageVersion.CSharp9, preprocessorSymbols: new[] { "UNITY_6000_0_OR_NEWER", "UNITY_6000_0", "UNITY_STANDALONE", "UNITY_STANDALONE_WIN", "UNITY_64" });
        var editor = player.WithPreprocessorSymbols(player.PreprocessorSymbolNames.Concat(new[] { "UNITY_EDITOR", "UNITY_EDITOR_WIN" }));
        int syntaxFiles = 0;
        foreach (string path in changed.Where(File.Exists))
        {
            syntaxFiles++;
            foreach (var options in new[] { player, editor })
            {
                var parsed = CSharpSyntaxTree.ParseText(File.ReadAllText(path), options, path);
                foreach (var error in parsed.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
                    Add("CSharp9Syntax", error.ToString(), path: Path.GetRelativePath(root, path));
            }
        }
        var allSources = Directory.GetFiles(Path.Combine(root, "Assets"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "Editor" + Path.DirectorySeparatorChar)).ToArray();
        var trees = allSources.Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), player, path)).ToArray();
        var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path)).ToList();
        var compilation = CSharpCompilation.Create("BattleSpiritsPartialSourceModel", trees, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        int membersChecked = 0, invocationsChecked = 0;
        var memberNames = new HashSet<string>(new[] { "CardsManager", "PacksManager", "BattleIntegration", "BattleCatalog", "BattleDeckStorage", "BattleCardAdapter" });
        foreach (var tree in trees)
        {
            var accesses = tree.GetRoot().DescendantNodes().OfType<MemberAccessExpressionSyntax>()
                .Where(access => memberNames.Contains(access.Expression.ToString().Split('.').Last())).ToArray();
            if (accesses.Length == 0) continue;
            var model = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
            foreach (var access in accesses)
            {
                var type = model.GetSymbolInfo(access.Expression).Symbol as INamedTypeSymbol;
                if (type == null || !memberOwners.Contains(type.ToDisplayString())) continue;
                membersChecked++;
                var members = type.GetMembers(access.Name.Identifier.ValueText);
                if (members.Length == 0)
                {
                    Add("CrossFileMember", type.ToDisplayString() + " has no member " + access.Name.Identifier.ValueText, access);
                    continue;
                }
                if (!(access.Parent is InvocationExpressionSyntax invocation)) continue;
                invocationsChecked++;
                int count = invocation.ArgumentList.Arguments.Count;
                var overloads = members.OfType<IMethodSymbol>().ToArray();
                if (!overloads.Any(method => count >= method.Parameters.Count(p => !p.IsOptional && !p.IsParams)
                    && (method.Parameters.LastOrDefault()?.IsParams == true || count <= method.Parameters.Length)))
                    Add("CrossFileArguments", type.ToDisplayString() + "." + access.Name.Identifier.ValueText + " has no overload accepting " + count + " arguments", access);
            }
        }
        int entryMethods = AuditEntries(compilation, trees);
        CheckTraversalRegressions();
        CheckRequiredTypes(compilation, incomplete);
        CheckPublicContract(compilation, incomplete);
        CheckBootstrapIsolation(trees, incomplete);
        RunNativeRegressions(trees, refs);
        var errors = findings.Count(f => f.severity == "error");
        var report = new { status = errors == 0 ? "PASS" : "FAIL", changedCSharpFiles = syntaxFiles,
            syntaxConfigurations = new[] { "Windows player C#9", "Windows editor C#9" },
            partialSemanticSourceFiles = trees.Length, auditedStaticMemberReferences = membersChecked,
            auditedStaticInvocationArities = invocationsChecked, sourceMethodsReachedFromNativeEntries = entryMethods,
            findings = findings, unityEditorPlayOrBuildTested = false,
            limitations = "Partial source semantics do not replace a Unity compile. Entry traversal covers source calls and constant BattleSpiritsMode guards, not reflection, serialized UnityEvents or asset bundles." };
        File.WriteAllText(args[2], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(report.status + ": " + syntaxFiles + " changed/new C# assets parsed under player+editor C#9; " + membersChecked
            + " static member references and " + invocationsChecked + " call arities resolved; " + entryMethods + " entry-reachable source methods checked.");
        foreach (var finding in findings.Take(45)) Console.WriteLine(finding.severity + " " + finding.check + " " + finding.file + ":" + finding.line + " " + finding.message);
        Console.WriteLine("Report: " + args[2] + "; Unity Play/build not run.");
        return errors == 0 ? 0 : 1;
    }

    private static void RunNativeRegressions(SyntaxTree[] trees, List<PortableExecutableReference> refs)
    {
        MethodDeclarationSyntax Method(string type, string name) => trees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Single(method => (method.Parent as TypeDeclarationSyntax)?.Identifier.ValueText == type && method.Identifier.ValueText == name);
        var rename = Method("ReplaySelectorUI", "ReplayRename");
        var play = Method("SoloSelectorUI", "OnPlay");
        string source = @"
using System;
using System.IO;
using System.Collections.Generic;
namespace NativeRegressionProbe {
public class ReplaySelectorUI {
public string Selected; public int prints;
private void Print() { prints++; }
" + rename.ToFullString() + @"
public void Rename(string value) { ReplayRename(value); }
}
public class SuperScrollView { public int selected; public List<int> items = new List<int>(); }
public class GameObject { public bool activeSelf; }
public class Button { public GameObject gameObject = new GameObject(); }
public class SoloSelector { public enum Condition { ForSolo, ForRoom }; public static Condition condition; }
public class SoloCalls { public int calls; public int lastIndex; public void ShowSoloPreview(int index, bool custom) { calls++; lastIndex=index; } public void StartAIForRoom(int index,bool custom) { calls++; lastIndex=index; } }
public class Program { public static Program instance = new Program(); public SoloCalls solo = new SoloCalls(); }
public class SoloSelectorUI {
public SuperScrollView superScrollView; public Button ButtonDeck = new Button();
" + play.ToFullString() + @"
}
public static class Regression {
public static int Run() {
int assertions=0;
string temporary=Path.Combine(Path.GetTempPath(), ""bs-replay-rename-""+Guid.NewGuid().ToString(""N""));
Directory.CreateDirectory(temporary);
try {
string old=Path.Combine(temporary,""original.bsreplay.json""); File.WriteAllText(old,""replay"");
var replay=new ReplaySelectorUI { Selected=old }; replay.Rename(""renamed"");
if(!File.Exists(Path.Combine(temporary,""renamed.bsreplay.json"")) || Directory.GetFiles(temporary,""*.bsreplay.json"").Length!=1 || replay.prints!=1) throw new Exception(""Replay compound extension round trip failed""); assertions++;
replay.Selected=Path.Combine(temporary,""renamed.bsreplay.json""); replay.Rename(""../outside"");
if(replay.prints!=1 || !File.Exists(replay.Selected)) throw new Exception(""Unsafe replay rename was not guarded""); assertions++;
replay.Rename(""prompt-name.bsreplay"");
if(!File.Exists(Path.Combine(temporary,""prompt-name.bsreplay.json"")) || File.Exists(Path.Combine(temporary,""prompt-name.bsreplay.bsreplay.json""))) throw new Exception(""The native rename prompt duplicated the compound extension""); assertions++;
replay.Selected=Path.Combine(temporary,""prompt-name.bsreplay.json""); replay.Rename(""full-name.bsreplay.json"");
if(!File.Exists(Path.Combine(temporary,""full-name.bsreplay.json""))) throw new Exception(""Full replay suffix was duplicated""); assertions++;
} finally { Directory.Delete(temporary,true); }
var ui=new SoloSelectorUI();
ui.OnPlay(); if(Program.instance.solo.calls!=0) throw new Exception(""Null solo list launched""); assertions++;
ui.superScrollView=new SuperScrollView(); ui.OnPlay(); if(Program.instance.solo.calls!=0) throw new Exception(""Empty solo list launched""); assertions++;
ui.superScrollView.items.Add(1); ui.superScrollView.selected=-1; ui.OnPlay(); if(Program.instance.solo.calls!=0) throw new Exception(""Negative solo selection launched""); assertions++;
ui.superScrollView.selected=1; ui.OnPlay(); if(Program.instance.solo.calls!=0) throw new Exception(""Out-of-range solo selection launched""); assertions++;
ui.superScrollView.selected=0; ui.OnPlay(); if(Program.instance.solo.calls!=1 || Program.instance.solo.lastIndex!=0) throw new Exception(""Valid solo selection did not launch""); assertions++;
SoloSelector.condition=SoloSelector.Condition.ForRoom; ui.OnPlay(); if(Program.instance.solo.calls!=2) throw new Exception(""Valid room selection did not dispatch""); assertions++;
return assertions;
}}
}";
        var probe = CSharpCompilation.Create("NativeMethodRegression_" + Guid.NewGuid().ToString("N"),
            new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp9)) }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var memory = new MemoryStream();
        var result = probe.Emit(memory);
        if (!result.Success)
        { Add("NativeMethodRegression", "Production method-body compilation failed: " + string.Join("; ", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))); return; }
        try
        {
            var assembly = Assembly.Load(memory.ToArray());
            int count = (int)assembly.GetType("NativeRegressionProbe.Regression").GetMethod("Run").Invoke(null, null);
            Console.WriteLine("PASS: " + count + " executable native replay-rename and asynchronous/empty solo-list regressions (actual production method bodies).");
        }
        catch (Exception error)
        { Add("NativeMethodRegression", (error.InnerException ?? error).ToString()); }
    }

    private static void CheckRequiredTypes(CSharpCompilation compilation, bool incomplete)
    {
        foreach (string name in new[] { "MDPro3.BattleSpirits.BattleIntegration", "MDPro3.BattleSpirits.BattleSpiritsMode", "MDPro3.BattleSpirits.BattleGameSession", "MDPro3.BattleSpirits.BattleDuelServant", "MDPro3.BattleSpirits.BattleDuelUI" })
            if (compilation.GetTypeByMetadataName(name) == null) Add("RequiredIntegrationType", "Missing " + name, severity: incomplete ? "warning" : "error");
    }

    private static void CheckPublicContract(CSharpCompilation compilation, bool incomplete)
    {
        void Method(string owner, string name, string returns, bool isStatic, params string[] parameters)
        {
            var type = compilation.GetTypeByMetadataName(owner);
            if (type == null) return;
            bool found = type.GetMembers(name).OfType<IMethodSymbol>().Any(method => method.DeclaredAccessibility == Accessibility.Public
                && method.IsStatic == isStatic && method.ReturnType.ToDisplayString() == returns
                && method.Parameters.Select(parameter => (parameter.RefKind == RefKind.Out ? "out " : "") + parameter.Type.ToDisplayString()).SequenceEqual(parameters));
            if (!found) Add("PublicSessionContract", owner + "." + name + "(" + string.Join(", ", parameters) + ") -> " + returns + " is missing or has incompatible public signature", severity: incomplete ? "warning" : "error");
        }
        string integration = "MDPro3.BattleSpirits.BattleIntegration";
        Method(integration, "Initialize", "void", true, "MDPro3.Program");
        Method(integration, "StartSolo", "bool", true, "string", "string");
        Method(integration, "StartPuzzle", "bool", true, "string");
        Method(integration, "PlayReplay", "bool", true, "string");
        Method(integration, "PreviewCutscene", "bool", true, "string");
        Method(integration, "Host", "bool", true, "int", "string");
        Method(integration, "Join", "bool", true, "string", "int", "string");
        Method(integration, "GetReplayFiles", "string[]", true);
        Method(integration, "Retry", "bool", true);
        Method(integration, "ResetPuzzle", "bool", true);
        Method(integration, "Concede", "void", true);
        string session = "MDPro3.BattleSpirits.BattleGameSession";
        Method(session, "Send", "bool", false, "MDPro3.BattleSpirits.Core.BattleCommand", "out string");
        Method(session, "Tick", "void", false, "float");
        Method(session, "StartSolo", "void", false, "MDPro3.BattleSpirits.Core.DeckDefinition", "MDPro3.BattleSpirits.Core.DeckDefinition", "int", "string");
        Method(session, "StartPuzzle", "void", false, "MDPro3.BattleSpirits.Puzzles.PuzzleDefinition");
        Method(session, "ResetPuzzle", "bool", false);
        Method(session, "Retry", "bool", false);
        Method(session, "Concede", "void", false);
        Method(session, "StartHost", "void", false, "int", "MDPro3.BattleSpirits.Core.DeckDefinition", "string");
        Method(session, "Join", "void", false, "string", "int", "MDPro3.BattleSpirits.Core.DeckDefinition", "string");
        Method(session, "StartReplay", "void", false, "string");
        Method(session, "StepReplay", "void", false, "int");
        Method(session, "Dispose", "void", false);
        Method(session, "ReplayFiles", "string[]", true, "string");
        Method("MDPro3.Program", "RegisterServant", "void", false, "MDPro3.Servant.Servant");
        Method("MDPro3.BattleSpirits.BattleDuelServant", "Bind", "void", false, session);
        Method("MDPro3.BattleSpirits.BattleDuelServant", "PreviewBoss", "void", false, "string");
        var mode = compilation.GetTypeByMetadataName("MDPro3.BattleSpirits.BattleSpiritsMode");
        if (mode != null && !mode.GetMembers("IsEnabled").OfType<IFieldSymbol>().Any(field => field.IsConst && field.ConstantValue is true))
            Add("DirectBattleSpiritsMode", "BattleSpiritsMode.IsEnabled must remain a constant true value with no runtime game toggle.");
    }

    private static void CheckBootstrapIsolation(SyntaxTree[] trees, bool incomplete)
    {
        var program = trees.FirstOrDefault(tree => tree.FilePath.EndsWith(Path.Combine("Game", "Program.cs"), StringComparison.Ordinal));
        if (program == null) return;
        var register = program.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault(method => method.Identifier.ValueText == "InitializeAllServants");
        if (register == null) return;
        foreach (var call in Executable(register).OfType<InvocationExpressionSyntax>())
            if (call.Expression.ToString() == "servants.Add" && call.ArgumentList.Arguments.Any(argument => new[] { "ocgcore", "room" }.Contains(argument.Expression.ToString())))
                Add("DirectBattleSpiritsBootstrap", "Legacy servant remains in active initialization/per-frame list: " + call.ArgumentList, call, severity: incomplete ? "warning" : "error");
    }

    private static int AuditEntries(CSharpCompilation compilation, SyntaxTree[] trees)
    {
        // Root lifecycle methods explicitly: calls through Manager/Servant base types do
        // not identify runtime overrides in this deliberately partial semantic model.
        var entryTypes = new HashSet<string>(new[] {
            "MainMenu", "MainMenuUI", "SoloSelector", "SoloSelectorUI", "OnlineServant", "OnlineServantUI",
            "PuzzlePortal", "PuzzleSelector", "PuzzleSelectorUI", "PuzzlePortalUI", "ReplaySelector", "ReplaySelectorUI",
            "CutinViewer", "CutinViewerUI", "MateViewer", "MateViewerUI", "PageLegacy", "PageHost",
            "DeckSelector", "DeckSelectorUI", "DeckEditor", "DeckEditorUI", "DeckBrowser", "DeckBrowserUI",
            "Appearance", "AppearanceUI", "SettingServant", "SettingServantUI", "BattleDuelServant", "BattleDuelUI",
            "AudioManager", "BackgroundManager", "UIManager", "TextureManager", "CameraManager", "MessageManager",
            "BattleRuntimeDriver", "SelectionToggle_Deck", "SelectionToggle_Solo", "SelectionToggle_Puzzle",
            "SelectionToggle_Replay", "SelectionToggle_AppearanceItem", "SelectionToggle_SearchOrder",
            "SelectionButton_CardInCollection", "SelectionButton_CardInDeck", "SelectionButton_CardInfoType",
            "SelectionButton_DeckSelector", "CardActionMenu", "CardCollectionView", "DeckView", "CardDetailView",
            "DeckPickup", "PickupCardSelection", "CardInfoDetail", "PopupSearchFilter"
        });
        var queue = new Queue<(SyntaxNode node, string chain)>();
        foreach (var tree in trees)
        foreach (var method in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            string type = (method.Parent as TypeDeclarationSyntax)?.Identifier.ValueText;
            bool bootstrap = (type == "Boot" && method.Identifier.ValueText == "Start")
                || (type == "Program" && new[] { "Awake", "Initialize", "ReadParams", "InitializeAllManagers", "InitializeAllServants", "Update", "OnApplicationQuit" }.Contains(method.Identifier.ValueText));
            if ((entryTypes.Contains(type ?? "") && (method.Modifiers.Any(SyntaxKind.PublicKeyword) || method.Modifiers.Any(SyntaxKind.ProtectedKeyword)
                || new[] { "Awake", "Start", "OnEnable", "Update", "OnDestroy" }.Contains(method.Identifier.ValueText))) || bootstrap)
                queue.Enqueue((method, type + "." + method.Identifier.ValueText));
        }
        var visited = new HashSet<string>();
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            string key = current.node.SyntaxTree.FilePath + ":" + current.node.SpanStart;
            if (!visited.Add(key)) continue;
            var model = compilation.GetSemanticModel(current.node.SyntaxTree, ignoreAccessibility: true);
            foreach (var node in Executable(current.node))
            {
                if (node is AssignmentExpressionSyntax assignment
                    && assignment.Left.ToString().EndsWith("returnServant", StringComparison.Ordinal)
                    && assignment.Right.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>().Any(access =>
                        access.ToString().EndsWith(".room", StringComparison.Ordinal) || access.ToString().EndsWith(".ocgcore", StringComparison.Ordinal)))
                    Add("ActiveLegacyLaunch", current.chain + " assigns a return target to a legacy duel servant.", assignment);
                if (node is InvocationExpressionSyntax call)
                {
                    string expression = call.Expression.ToString();
                    var target = model.GetSymbolInfo(call).Symbol as IMethodSymbol;
                    string owner = target?.ContainingType.ToDisplayString() ?? string.Empty;
                    string name = target?.Name ?? string.Empty;
                    bool forbidden = expression.EndsWith("YgoServer.StartServer", StringComparison.Ordinal)
                        || expression.EndsWith("WindBot.Program.Main", StringComparison.Ordinal)
                        || expression.EndsWith("TcpHelper.LinkStart", StringComparison.Ordinal)
                        || expression.EndsWith("TcpHelper.CtosMessage_JoinGame", StringComparison.Ordinal)
                        || expression.EndsWith("TcpHelper.CtosMessage_UpdateDeck", StringComparison.Ordinal)
                        || expression.EndsWith("TcpHelper.PerFrameFunction", StringComparison.Ordinal)
                        || (owner == "MDPro3.Net.YgoServer" && name == "StartServer")
                        || (owner == "WindBot.Program" && (name == "Main" || name == "RunFromArgs" || name == "Run"))
                        || (owner == "MDPro3.TcpHelper" && (name == "LinkStart" || name.StartsWith("CtosMessage_", StringComparison.Ordinal) || name == "PerFrameFunction"))
                        || (owner == "MDPro3.PercyOCG" && new[] { "StartPuzzle", "StartAI", "StartDuel" }.Contains(name))
                        || (owner == "MDPro3.Net.OnlineService" && (name == "Initialize" || name == "InitializeAsync"))
                        || (owner == "MDPro3.Net.OnlineDeck" && new[] { "LikeDeck", "UpdatePublicState", "UpdateDeck", "DeleteDeck", "SyncDeck" }.Contains(name))
                        || (expression.EndsWith("ShiftToServant", StringComparison.Ordinal) && call.ArgumentList.Arguments.Any(a =>
                            a.Expression.ToString().EndsWith(".ocgcore", StringComparison.Ordinal) || a.Expression.ToString().EndsWith(".room", StringComparison.Ordinal)));
                    if (forbidden) Add("ActiveLegacyLaunch", current.chain + " -> " + expression, call);
                    if (call.Expression is MemberAccessExpressionSyntax audioAccess
                        && audioAccess.Expression.ToString().EndsWith("Addressables", StringComparison.Ordinal)
                        && audioAccess.Name is GenericNameSyntax audioMethod
                        && audioMethod.Identifier.ValueText == "LoadAssetAsync"
                        && audioMethod.TypeArgumentList.Arguments.Any(argument => argument.ToString().EndsWith("AudioClip", StringComparison.Ordinal)))
                        Add("ActiveLegacyAudioAsset", current.chain + " still loads an asset-bundle AudioClip instead of the Battle Spirits audio library.", call);
                }
                // Include method groups registered as native event callbacks, not only direct invocations.
                if (!(node is InvocationExpressionSyntax || node is IdentifierNameSyntax || node is MemberAccessExpressionSyntax)) continue;
                var symbol = model.GetSymbolInfo(node).Symbol as IMethodSymbol;
                if (symbol == null) continue;
                foreach (var source in symbol.DeclaringSyntaxReferences)
                {
                    var declaration = source.GetSyntax();
                    if (declaration.SyntaxTree.FilePath.Contains(Path.Combine("Assets", "Scripts", "MDPro3")) || declaration.SyntaxTree.FilePath.Contains(Path.Combine("Assets", "BattleSpirits")))
                        queue.Enqueue((declaration, current.chain + " -> " + symbol.ContainingType.Name + "." + symbol.Name));
                }
            }
        }
        return visited.Count;
    }

    private static void CheckTraversalRegressions()
    {
        string[] VisibleCalls(string body) => Executable(CSharpSyntaxTree.ParseText("class Probe { void Run() { " + body + " } }")
            .GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single()).OfType<InvocationExpressionSyntax>().Select(call => call.Expression.ToString()).ToArray();
        if (!VisibleCalls("if (BattleSpiritsMode.IsEnabled) { LaunchBattle(); return; } LaunchOld();").SequenceEqual(new[] { "LaunchBattle" }))
            Add("TraversalRegression", "Always-on guard did not suppress the old launch after its unconditional return.");
        if (!VisibleCalls("if (!BattleSpiritsMode.IsEnabled) { LaunchOld(); } else { LaunchBattle(); } After();").SequenceEqual(new[] { "LaunchBattle", "After" }))
            Add("TraversalRegression", "Negative mode guard branch pruning failed.");
        if (!VisibleCalls("if (Unknown()) return; LaunchOld();").SequenceEqual(new[] { "Unknown", "LaunchOld" }))
            Add("TraversalRegression", "Unknown guards must not hide potentially reachable launches.");
        if (!VisibleCalls("Register(() => { LaunchOld(); });").SequenceEqual(new[] { "Register", "LaunchOld" }))
            Add("TraversalRegression", "Registered lambda callbacks were not visited.");
    }

    private static bool? ModeValue(ExpressionSyntax expression)
    {
        if (expression is ParenthesizedExpressionSyntax parenthesized) return ModeValue(parenthesized.Expression);
        if (expression is PrefixUnaryExpressionSyntax prefix && prefix.IsKind(SyntaxKind.LogicalNotExpression))
        { var value = ModeValue(prefix.Operand); return value.HasValue ? !value.Value : (bool?)null; }
        if (expression.IsKind(SyntaxKind.TrueLiteralExpression)) return true;
        if (expression.IsKind(SyntaxKind.FalseLiteralExpression)) return false;
        if (expression.ToString().EndsWith("BattleSpiritsMode.IsEnabled", StringComparison.Ordinal)) return true;
        return null;
    }

    private static IEnumerable<SyntaxNode> Executable(SyntaxNode node)
    {
        if (node == null) yield break;
        yield return node;
        if (node is IfStatementSyntax conditional)
        {
            foreach (var child in Executable(conditional.Condition)) yield return child;
            var value = ModeValue(conditional.Condition);
            if (value != false) foreach (var child in Executable(conditional.Statement)) yield return child;
            if (value != true && conditional.Else != null) foreach (var child in Executable(conditional.Else.Statement)) yield return child;
            yield break;
        }
        if (node is BlockSyntax block)
        {
            foreach (var statement in block.Statements)
            {
                foreach (var child in Executable(statement)) yield return child;
                if (AlwaysStops(statement)) break;
            }
            yield break;
        }
        foreach (var child in node.ChildNodes()) foreach (var descendant in Executable(child)) yield return descendant;
    }

    private static bool AlwaysStops(StatementSyntax statement)
    {
        if (statement is ReturnStatementSyntax || statement is ThrowStatementSyntax || statement is YieldStatementSyntax yield && yield.IsKind(SyntaxKind.YieldBreakStatement)) return true;
        if (statement is BlockSyntax block) return block.Statements.Any(AlwaysStops);
        if (statement is IfStatementSyntax conditional)
        {
            var value = ModeValue(conditional.Condition);
            if (value == true) return AlwaysStops(conditional.Statement);
            if (value == false) return conditional.Else != null && AlwaysStops(conditional.Else.Statement);
            return conditional.Else != null && AlwaysStops(conditional.Statement) && AlwaysStops(conditional.Else.Statement);
        }
        return false;
    }
}
