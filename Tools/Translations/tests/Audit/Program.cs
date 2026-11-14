using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
internal static class SourceAudit
{
    static string root,baseline;
    static string Git(params string[] args)
    {var start=new ProcessStartInfo("git"){WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true};foreach(string a in args)start.ArgumentList.Add(a);using var p=Process.Start(start);string output=p.StandardOutput.ReadToEnd();p.WaitForExit();if(p.ExitCode!=0)throw new Exception(p.StandardError.ReadToEnd());return output;}
    static bool Han(string value)=>Regex.IsMatch(value,"[\u3400-\u9fff]");
    static string Shape(SyntaxNode node)=>string.Join(" ",node.DescendantTokens().Select(t=>t.IsKind(SyntaxKind.StringLiteralToken)||t.IsKind(SyntaxKind.InterpolatedStringTextToken)?"<text>":t.Text));
    static int Main(string[] args)
    {
        root=Path.GetFullPath(args.Length>0?args[0]:".");baseline=args.Length>1?args[1]:"e9ba63fa6";
        var files=Git("diff","--name-only",baseline).Split('\n',StringSplitOptions.RemoveEmptyEntries).Concat(Git("ls-files","--others","--exclude-standard").Split('\n',StringSplitOptions.RemoveEmptyEntries)).Where(p=>p.StartsWith("Assets/",StringComparison.Ordinal)&&p.EndsWith(".cs",StringComparison.Ordinal)).Distinct().OrderBy(p=>p).ToArray();
        var profiles=new[]{new[]{"UNITY_EDITOR","UNITY_EDITOR_WIN","UNITY_STANDALONE","UNITY_STANDALONE_WIN"},new[]{"UNITY_EDITOR","UNITY_EDITOR_WIN","UNITY_ANDROID"},new[]{"UNITY_STANDALONE","UNITY_STANDALONE_WIN"}};
        int errors=0;var executableChanges=new List<string>();var suspiciousStrings=new List<object>();
        foreach(string file in files)
        {
            string after=File.ReadAllText(Path.Combine(root,file));
            foreach(var profile in profiles)
            {
                var tree=CSharpSyntaxTree.ParseText(after,new CSharpParseOptions(LanguageVersion.CSharp9,preprocessorSymbols:profile.Concat(new[]{"UNITY_6000_0_OR_NEWER","UNITY_2021_3_OR_NEWER","UNITY_64","ENABLE_INPUT_SYSTEM"})),file);
                foreach(var d in tree.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error)){Console.WriteLine(d);errors++;}
            }
            if(Han(after)){Console.WriteLine("Raw Han remains in "+file);errors++;}
            var auditOptions=new CSharpParseOptions(LanguageVersion.CSharp9,preprocessorSymbols:profiles[0].Concat(new[]{"UNITY_6000_0_OR_NEWER","UNITY_2021_3_OR_NEWER","UNITY_64","ENABLE_INPUT_SYSTEM"}));
            var current=CSharpSyntaxTree.ParseText(after,auditOptions).GetRoot();
            foreach(var token in current.DescendantTokens().Where(t=>t.IsKind(SyntaxKind.StringLiteralToken)||t.IsKind(SyntaxKind.CharacterLiteralToken)||t.IsKind(SyntaxKind.InterpolatedStringTextToken)))
                if(Han(token.ValueText)){Console.WriteLine("Escaped Han remains in "+file);errors++;}
            string before;
            try{before=Git("show",baseline+":"+file);}catch{executableChanges.Add(file+" (new)");continue;}
            var original=CSharpSyntaxTree.ParseText(before,auditOptions).GetRoot();
            var oldNs=original.DescendantNodes().OfType<NamespaceDeclarationSyntax>().Select(n=>n.Name.ToString());
            var newNs=current.DescendantNodes().OfType<NamespaceDeclarationSyntax>().Select(n=>n.Name.ToString());
            if(!oldNs.SequenceEqual(newNs)){Console.WriteLine("Namespace changed: "+file);errors++;}
            if(Shape(original)!=Shape(current))executableChanges.Add(file);
            var oldStrings=original.DescendantTokens().Where(t=>t.IsKind(SyntaxKind.StringLiteralToken)).ToArray();
            var newStrings=current.DescendantTokens().Where(t=>t.IsKind(SyntaxKind.StringLiteralToken)||t.IsKind(SyntaxKind.CharacterLiteralToken)||t.IsKind(SyntaxKind.InterpolatedStringTextToken)).ToArray();
            if(oldStrings.Length==newStrings.Length)
                for(int i=0;i<oldStrings.Length;i++)
                {
                    if(oldStrings[i].ValueText==newStrings[i].ValueText||!Han(oldStrings[i].ValueText))continue;
                    bool translated=oldStrings[i].Parent.Ancestors().OfType<InvocationExpressionSyntax>().Any(n=>n.Expression.ToString().StartsWith("InterString.",StringComparison.Ordinal));
                    if(!translated)suspiciousStrings.Add(new{file,line=oldStrings[i].GetLocation().GetLineSpan().StartLinePosition.Line+1,before=oldStrings[i].ValueText,after=newStrings[i].ValueText,context=oldStrings[i].Parent.Parent.ToString()});
                }
        }
        string output=Path.Combine(Path.GetTempPath(),"mdpro3-deep-english");Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output,"roslyn-audit.json"),JsonSerializer.Serialize(new{files=files.Length,profiles=profiles.Length,errors,executableChanges,suspiciousStrings},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((errors==0?"PASS":"FAIL")+": "+files.Length+" changed production C# files x "+profiles.Length+" C# 9 profiles; "+errors+" syntax/source-language errors.");
        Console.WriteLine("Executable-token differences (review separately): "+string.Join(", ",executableChanges));return errors==0?0:1;
    }
}
