using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Types;

/// <summary>
/// Default Roslyn-based implementation of <see cref="ITypeFileSplitter"/>.
/// </summary>
public sealed class TypeFileSplitter : ITypeFileSplitter
{
    /// <inheritdoc />
    public TypeFileAnalysis AnalyzeFile(
        SyntaxTree syntaxTree,
        string filePath,
        CodeCondenserSettings settings,
        FileCategory category = FileCategory.Production)
    {
        ArgumentNullException.ThrowIfNull(syntaxTree);
        ArgumentNullException.ThrowIfNull(filePath);
        settings ??= new CodeCondenserSettings();

        var root = syntaxTree.GetCompilationUnitRoot();

        if (TypeIgnoreMarker.IsFileIgnored(root))
        {
            return new TypeFileAnalysis
            {
                FilePath = filePath,
                Category = category,
                Action = TypeFileAction.None,
                Types = []
            };
        }

        var topLevelNodes = GetTopLevelTypeNodes(root);
        if (topLevelNodes.Count == 0)
        {
            return new TypeFileAnalysis
            {
                FilePath = filePath,
                Category = category,
                Action = TypeFileAction.None,
                Types = []
            };
        }

        var detectedTypes = new List<ExtractedTypeInfo>();
        var seenFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var directory = Path.GetDirectoryName(filePath);

        bool FileExistsOnDisk(string fname)
        {
            if (string.IsNullOrEmpty(directory)) return false;
            var candidate = Path.Combine(directory, fname);
            return !string.Equals(candidate, filePath, StringComparison.OrdinalIgnoreCase) && File.Exists(candidate);
        }

        foreach (var node in topLevelNodes)
        {
            var (name, kind, arity) = GetTypeDeclarationInfo(node);
            int loc = TypeLocCounter.CountLinesOfCode(node);
            var lineSpan = syntaxTree.GetLineSpan(node.Span);
            int startLine = lineSpan.StartLinePosition.Line + 1;
            int endLine = lineSpan.EndLinePosition.Line + 1;
            bool isSuppressed = TypeIgnoreMarker.IsIgnored(node);

            // Determine suggested file name
            string baseTargetName = name;
            string targetFileName = baseTargetName + ".cs";

            if (seenFileNames.Contains(targetFileName) || FileExistsOnDisk(targetFileName))
            {
                targetFileName = arity > 0 ? $"{baseTargetName}`{arity}.cs" : $"{baseTargetName}2.cs";
                int counter = 2;
                while (seenFileNames.Contains(targetFileName) || FileExistsOnDisk(targetFileName))
                {
                    counter++;
                    targetFileName = arity > 0 ? $"{baseTargetName}`{arity}_{counter}.cs" : $"{baseTargetName}{counter}.cs";
                }
            }
            seenFileNames.Add(targetFileName);

            bool isTolerated = loc <= settings.TypeLineTolerance;

            detectedTypes.Add(new ExtractedTypeInfo
            {
                TypeName = name,
                Kind = kind,
                Arity = arity,
                LinesOfCode = loc,
                StartLine = startLine,
                EndLine = endLine,
                TargetFileName = targetFileName,
                IsPrimary = false,
                IsTolerated = isTolerated,
                IsSuppressed = isSuppressed
            });
        }

        var currentFileName = Path.GetFileNameWithoutExtension(filePath);

        // Map topLevelNodes by type info for matching
        var nodeMap = new Dictionary<ExtractedTypeInfo, MemberDeclarationSyntax>();
        for (int i = 0; i < detectedTypes.Count && i < topLevelNodes.Count; i++)
        {
            nodeMap[detectedTypes[i]] = topLevelNodes[i];
        }

        // Select primary type:
        // 1. Matches file name (exact, partial aspect, or generic arity)
        // 2. Or first non-tolerated, non-suppressed type
        // 3. Or first type
        ExtractedTypeInfo? primary = detectedTypes.FirstOrDefault(t => IsFileNameMatching(currentFileName, t, nodeMap.GetValueOrDefault(t)))
            ?? detectedTypes.FirstOrDefault(t => !t.IsTolerated && !t.IsSuppressed)
            ?? detectedTypes.FirstOrDefault();

        var finalTypes = new List<ExtractedTypeInfo>(detectedTypes.Count);
        foreach (var t in detectedTypes)
        {
            bool isPrimary = ReferenceEquals(t, primary);
            finalTypes.Add(new ExtractedTypeInfo
            {
                TypeName = t.TypeName,
                Kind = t.Kind,
                Arity = t.Arity,
                LinesOfCode = t.LinesOfCode,
                StartLine = t.StartLine,
                EndLine = t.EndLine,
                TargetFileName = t.TargetFileName,
                IsPrimary = isPrimary,
                IsTolerated = isPrimary ? false : t.IsTolerated,
                IsSuppressed = t.IsSuppressed
            });
        }

        primary = finalTypes.FirstOrDefault(t => t.IsPrimary);

        var typesToExtract = finalTypes.Where(t => !t.IsPrimary && !t.IsTolerated && !t.IsSuppressed).ToList();
        var primaryNode = primary != null ? topLevelNodes.FirstOrDefault(n =>
        {
            var (name, _, arity) = GetTypeDeclarationInfo(n);
            return name == primary.TypeName && arity == primary.Arity;
        }) : null;
        bool nameMatches = primary != null && IsFileNameMatching(currentFileName, primary, primaryNode);

        TypeFileAction action = TypeFileAction.None;
        string? suggestedFileName = null;

        bool allTypesToleratedOrSuppressed = detectedTypes.Count > 1 && detectedTypes.All(t => t.IsTolerated || t.IsSuppressed);

        if (allTypesToleratedOrSuppressed)
        {
            action = TypeFileAction.None;
        }
        else if (typesToExtract.Count > 0)
        {
            if (!nameMatches && settings.EnableTypeRenaming && primary != null && !primary.IsSuppressed)
            {
                action = TypeFileAction.SplitAndRename;
                suggestedFileName = primary.TargetFileName;
            }
            else
            {
                action = TypeFileAction.Split;
            }
        }
        else if (!nameMatches && settings.EnableTypeRenaming && primary != null && !primary.IsSuppressed)
        {
            action = TypeFileAction.Rename;
            suggestedFileName = primary.TargetFileName;
        }

        return new TypeFileAnalysis
        {
            FilePath = filePath,
            Category = category,
            Action = action,
            SuggestedFileName = suggestedFileName,
            Types = finalTypes
        };
    }

    /// <inheritdoc />
    public SplitResult SplitFile(
        SyntaxTree syntaxTree,
        string filePath,
        TypeFileAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(syntaxTree);
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(analysis);

        var root = syntaxTree.GetCompilationUnitRoot();
        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;

        var nodesToExtract = new List<MemberDeclarationSyntax>();
        var topLevelNodes = GetTopLevelTypeNodes(root);

        var newFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Header trivia before any usings/namespaces (license, nullable, etc.)
        var fileHeaderTrivia = GetFileHeaderTrivia(root);

        // All usings from the file, deduplicated by exact text
        var allUsings = root.DescendantNodes().OfType<UsingDirectiveSyntax>()
            .GroupBy(u => u.ToString().Trim())
            .Select(g => g.First().WithoutTrivia())
            .ToList();

        foreach (var typeToExtract in analysis.TypesToExtract)
        {
            var node = topLevelNodes.FirstOrDefault(n =>
            {
                var (name, _, arity) = GetTypeDeclarationInfo(n);
                var lineSpan = syntaxTree.GetLineSpan(n.Span);
                int startLine = lineSpan.StartLinePosition.Line + 1;
                return name == typeToExtract.TypeName &&
                       arity == typeToExtract.Arity &&
                       (typeToExtract.StartLine == 0 || startLine == typeToExtract.StartLine);
            });

            if (node == null)
            {
                continue;
            }

            nodesToExtract.Add(node);

            // Generate new file for this type
            var targetPath = Path.Combine(directory, typeToExtract.TargetFileName);
            var newFileContent = GenerateNewFileContent(root, node, allUsings, fileHeaderTrivia);
            newFiles[targetPath] = newFileContent;
        }

        // Remove extracted nodes from original file
        string updatedPrimaryContent;
        if (nodesToExtract.Count > 0)
        {
            var updatedRoot = root.RemoveNodes(nodesToExtract, SyntaxRemoveOptions.KeepNoTrivia);
            updatedPrimaryContent = updatedRoot != null ? updatedRoot.ToFullString() : root.ToFullString();
        }
        else
        {
            updatedPrimaryContent = root.ToFullString();
        }

        var newPrimaryPath = filePath;
        if ((analysis.Action == TypeFileAction.Rename || analysis.Action == TypeFileAction.SplitAndRename) &&
            !string.IsNullOrWhiteSpace(analysis.SuggestedFileName))
        {
            newPrimaryPath = Path.Combine(directory, analysis.SuggestedFileName);
        }

        return new SplitResult
        {
            OriginalFilePath = filePath,
            PrimaryFilePath = newPrimaryPath,
            PrimaryFileContent = updatedPrimaryContent,
            NewFiles = newFiles
        };
    }

    private static bool IsFileNameMatching(string currentFileName, ExtractedTypeInfo type, MemberDeclarationSyntax? node)
    {
        if (type.TypeName.Equals(currentFileName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Generic arity convention (e.g. "MyClass`1" or "MyClass{T}")
        if (type.Arity > 0)
        {
            if (currentFileName.Equals($"{type.TypeName}`{type.Arity}", StringComparison.OrdinalIgnoreCase) ||
                currentFileName.StartsWith($"{type.TypeName}`", StringComparison.OrdinalIgnoreCase) ||
                currentFileName.StartsWith($"{type.TypeName}{{", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Aspect or sub-part convention (e.g. "OrderService.Validation", "OrderService.Handlers", "OrderService_Events")
        if (currentFileName.StartsWith(type.TypeName + ".", StringComparison.OrdinalIgnoreCase) ||
            currentFileName.StartsWith(type.TypeName + "_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Partial class convention: any file starting with type name when class is partial
        if (node != null && HasPartialModifier(node) &&
            currentFileName.StartsWith(type.TypeName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool HasPartialModifier(MemberDeclarationSyntax member)
    {
        SyntaxTokenList modifiers = member switch
        {
            BaseTypeDeclarationSyntax b => b.Modifiers,
            _ => default
        };

        return modifiers.Any(SyntaxKind.PartialKeyword);
    }

    private static List<MemberDeclarationSyntax> GetTopLevelTypeNodes(CompilationUnitSyntax root)
    {
        var result = new List<MemberDeclarationSyntax>();

        foreach (var member in root.Members)
        {
            if (member is BaseNamespaceDeclarationSyntax ns)
            {
                foreach (var nsMember in ns.Members)
                {
                    if (IsTypeDeclaration(nsMember) && !HasFileModifier(nsMember))
                    {
                        result.Add(nsMember);
                    }
                }
            }
            else if (IsTypeDeclaration(member) && !HasFileModifier(member))
            {
                result.Add(member);
            }
        }

        return result;
    }

    private static bool IsTypeDeclaration(MemberDeclarationSyntax member)
    {
        return member is ClassDeclarationSyntax or
            StructDeclarationSyntax or
            InterfaceDeclarationSyntax or
            EnumDeclarationSyntax or
            RecordDeclarationSyntax or
            DelegateDeclarationSyntax;
    }

    private static bool HasFileModifier(MemberDeclarationSyntax member)
    {
        SyntaxTokenList modifiers = member switch
        {
            BaseTypeDeclarationSyntax b => b.Modifiers,
            DelegateDeclarationSyntax d => d.Modifiers,
            _ => default
        };

        return modifiers.Any(SyntaxKind.FileKeyword);
    }

    private static (string Name, string Kind, int Arity) GetTypeDeclarationInfo(MemberDeclarationSyntax node)
    {
        return node switch
        {
            RecordDeclarationSyntax r => (
                r.Identifier.ValueText,
                r.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) ? "record struct" : "record",
                r.TypeParameterList?.Parameters.Count ?? 0),
            ClassDeclarationSyntax c => (c.Identifier.ValueText, "class", c.TypeParameterList?.Parameters.Count ?? 0),
            StructDeclarationSyntax s => (s.Identifier.ValueText, "struct", s.TypeParameterList?.Parameters.Count ?? 0),
            InterfaceDeclarationSyntax i => (i.Identifier.ValueText, "interface", i.TypeParameterList?.Parameters.Count ?? 0),
            EnumDeclarationSyntax e => (e.Identifier.ValueText, "enum", 0),
            DelegateDeclarationSyntax d => (d.Identifier.ValueText, "delegate", d.TypeParameterList?.Parameters.Count ?? 0),
            _ => ("Unknown", "unknown", 0)
        };
    }

    private static SyntaxTriviaList GetFileHeaderTrivia(CompilationUnitSyntax root)
    {
        var firstToken = root.GetFirstToken(includeZeroWidth: true);
        var leadingTrivia = firstToken.LeadingTrivia;

        var headerTrivia = new List<SyntaxTrivia>();
        foreach (var trivia in leadingTrivia)
        {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
                trivia.IsKind(SyntaxKind.NullableDirectiveTrivia) ||
                trivia.IsKind(SyntaxKind.PragmaWarningDirectiveTrivia) ||
                trivia.IsKind(SyntaxKind.WhitespaceTrivia) ||
                trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                headerTrivia.Add(trivia);
            }
            else
            {
                break;
            }
        }

        return SyntaxFactory.TriviaList(headerTrivia);
    }

    private static string GenerateNewFileContent(
        CompilationUnitSyntax root,
        MemberDeclarationSyntax typeNode,
        List<UsingDirectiveSyntax> allUsings,
        SyntaxTriviaList fileHeaderTrivia)
    {
        var newUnit = SyntaxFactory.CompilationUnit();

        // 1. Add usings
        newUnit = newUnit.WithUsings(SyntaxFactory.List(allUsings));

        // 2. Add namespace or direct member
        var containingNamespace = typeNode.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>();
        if (containingNamespace is FileScopedNamespaceDeclarationSyntax fileNs)
        {
            var newFileNs = SyntaxFactory.FileScopedNamespaceDeclaration(fileNs.Name.WithoutTrivia())
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                .AddMembers(typeNode);

            newUnit = newUnit.AddMembers(newFileNs);
        }
        else if (containingNamespace is NamespaceDeclarationSyntax blockNs)
        {
            var newBlockNs = SyntaxFactory.NamespaceDeclaration(blockNs.Name.WithoutTrivia())
                .AddMembers(typeNode);

            newUnit = newUnit.AddMembers(newBlockNs);
        }
        else
        {
            newUnit = newUnit.AddMembers(typeNode);
        }

        // Normalize and attach file header trivia
        var normalized = newUnit.NormalizeWhitespace();
        if (fileHeaderTrivia.Count > 0)
        {
            var firstToken = normalized.GetFirstToken();
            var combinedTrivia = fileHeaderTrivia.AddRange(firstToken.LeadingTrivia);
            normalized = normalized.ReplaceToken(firstToken, firstToken.WithLeadingTrivia(combinedTrivia));
        }

        return normalized.ToFullString() + Environment.NewLine;
    }
}
