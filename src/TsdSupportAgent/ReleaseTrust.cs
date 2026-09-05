using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

static class ReleaseTrust
{
    public const string KeyId = "9d2757f9ea9f5375";

    const string PublicKeyPem = """
-----BEGIN PUBLIC KEY-----
MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEbfHaaJKTejekptuwlBatnPtE40Fw
OnLpMw9LH19mUNL++vLJCW86wAclshC4RiynjDVvKPeDcT1K+nCeNxVVPw==
-----END PUBLIC KEY-----
""";

    public static bool VerifyManifest(string manifestJson, string signatureBase64)
    {
        try
        {
            var signature = Convert.FromBase64String(signatureBase64.Trim());
            using var key = ECDsa.Create();
            key.ImportFromPem(PublicKeyPem);
            return key.VerifyData(
                Encoding.UTF8.GetBytes(manifestJson.Trim()),
                signature,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence);
        }
        catch
        {
            return false;
        }
    }

    public static bool VerifyBinary(string manifestJson, string binaryPath, out string reason)
    {
        reason = "";
        try
        {
            using var doc = JsonDocument.Parse(manifestJson);
            var root = doc.RootElement;
            var expectedSha = root.GetProperty("sha256").GetString() ?? "";
            var expectedSize = root.GetProperty("binary_size").GetInt64();

            var info = new FileInfo(binaryPath);
            if (!info.Exists) { reason = "binary_missing"; return false; }
            if (info.Length != expectedSize) { reason = "size_mismatch"; return false; }

            using var stream = File.OpenRead(binaryPath);
            var actualSha = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            if (!string.Equals(actualSha, expectedSha, StringComparison.OrdinalIgnoreCase))
            {
                reason = "sha256_mismatch";
                return false;
            }

            reason = "ok";
            return true;
        }
        catch
        {
            reason = "manifest_or_binary_invalid";
            return false;
        }
    }
}
