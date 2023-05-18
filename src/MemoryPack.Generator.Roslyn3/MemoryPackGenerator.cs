using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.IO;
using System.Text;
using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace MemoryPack.Generator;

[Generator(LanguageNames.CSharp)]
public partial class MemoryPackGenerator : ISourceGenerator
{
    public const string MemoryPackableAttributeFullName = "MemoryPack.MemoryPackableAttribute";
    public const string GenerateTypeScriptAttributeFullName = "MemoryPack.GenerateTypeScriptAttribute";

    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(SyntaxContextReceiver.Create);
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxContextReceiver is not SyntaxContextReceiver receiver || receiver.ClassDeclarations.Count == 0)
        {
            return;
        }

        var logPath = string.Empty;

        try
        {
            // 突貫対応でSerializeInfoを吐き出す場所を直接書いちゃう

            // 正規表現でフルパスからログを吐き出すパスを計算する
            const string pattern = @"^(.*[\\\/])?client[\\\/]Assets[\\\/]";
            // 対象のdllに所属する最初のcsファイルのフルパスを取得
            var firstCSFullPath = context.Compilation.Assembly.Modules.First().Locations.First().GetMappedLineSpan().Path;
            var match = Regex.Match(firstCSFullPath, pattern);
            var path = match.Value;
            // Assets/は削除
            path = Regex.Replace(path, @"\\", @"/");
            logPath = path.Replace(@"Assets/", string.Empty) + "MemoryPackLogs";
            // File.WriteAllText(@"D:\hogehoge.txt", logPath);
        } catch(Exception e)
        {
            Console.WriteLine(e);
        }

        var compiation = context.Compilation;
        var generateContext = new GeneratorContext(context);

        if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.MemoryPackGenerator_DebugNonUnityMode", out var nonUnity))
        {
            generateContext.IsForUnity = !bool.Parse(nonUnity);
        }

        foreach (var syntax in receiver.ClassDeclarations)
        {
            Generate(syntax, compiation, logPath, generateContext);
        }
    }

    class SyntaxContextReceiver : ISyntaxContextReceiver
    {
        internal static ISyntaxContextReceiver Create()
        {
            return new SyntaxContextReceiver();
        }

        public HashSet<TypeDeclarationSyntax> ClassDeclarations { get; } = new();

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            var node = context.Node;
            if (node is ClassDeclarationSyntax
                     or StructDeclarationSyntax
                     or RecordDeclarationSyntax
                     or InterfaceDeclarationSyntax)
            {
                var typeSyntax = (TypeDeclarationSyntax)node;
                if (typeSyntax.AttributeLists.Count > 0)
                {
                    var attr = typeSyntax.AttributeLists.SelectMany(x => x.Attributes)
                        .FirstOrDefault(x =>
                        {
                            var packable = x.Name.ToString() is "MemoryPackable" or "MemoryPackableAttribute" or "MemoryPack.MemoryPackable" or "MemoryPack.MemoryPackableAttribute";
                            if (packable) return true;
                            var formatter = x.Name.ToString() is "MemoryPackUnionFormatter" or "MemoryPackUnionFormatterAttribute" or "MemoryPack.MemoryPackUnionFormatter" or "MemoryPack.MemoryPackUnionFormatterAttribute";
                            return formatter;
                        });
                    if (attr != null)
                    {
                        ClassDeclarations.Add(typeSyntax);
                    }
                }
            }
        }
    }

    class GeneratorContext : IGeneratorContext
    {
        GeneratorExecutionContext context;

        public GeneratorContext(GeneratorExecutionContext context)
        {
            this.context = context;
        }

        public CancellationToken CancellationToken => context.CancellationToken;

        public LanguageVersion LanguageVersion => LanguageVersion.CSharp9; // No IncrementalGenerator is C# 9.0

        public bool IsNet7OrGreater => false; // No IncrementalGenerator is always not NET7

        public bool IsForUnity { get; set; } = true;

        public void AddSource(string hintName, string source)
        {
            context.AddSource(hintName, source);
        }

        public void ReportDiagnostic(Diagnostic diagnostic)
        {
            context.ReportDiagnostic(diagnostic);
        }
    }
}
