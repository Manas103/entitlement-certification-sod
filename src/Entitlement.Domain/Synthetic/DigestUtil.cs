using System.Security.Cryptography;
using System.Text;

namespace Entitlement.Domain.Synthetic;

/// <summary>SHA-256 helper used to prove byte-identity of two independently generated artifacts.</summary>
public static class DigestUtil
{
    public static string Sha256Hex(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    public static string Sha256Hex(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content));
}
