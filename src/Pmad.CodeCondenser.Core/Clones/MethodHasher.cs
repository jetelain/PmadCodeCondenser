using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pmad.CodeCondenser.Core.Clones;

/// <summary>
/// Computes a SHA256 cryptographic hash of a normalized method declaration after canonical whitespace formatting.
/// </summary>
public sealed class MethodHasher : IMethodHasher
{
    /// <inheritdoc />
    public string ComputeHash(MethodDeclarationSyntax normalizedMethod)
    {
        ArgumentNullException.ThrowIfNull(normalizedMethod);

        var normalizedString = normalizedMethod.NormalizeWhitespace("", "").ToFullString();
        int byteCount = Encoding.UTF8.GetByteCount(normalizedString);
        byte[]? rented = null;
        Span<byte> utf8Bytes = byteCount <= 1024
            ? stackalloc byte[byteCount]
            : (rented = System.Buffers.ArrayPool<byte>.Shared.Rent(byteCount));

        try
        {
            int written = Encoding.UTF8.GetBytes(normalizedString, utf8Bytes);
            Span<byte> hash = stackalloc byte[32];
            SHA256.HashData(utf8Bytes[..written], hash);
            return Convert.ToHexStringLower(hash);
        }
        finally
        {
            if (rented != null)
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }
}
