// Echo Launcher -- the update channel's trust: latest.json and manifest.json are signed on the owner's PC
// (RSA-3072, SHA-256, PKCS#1 v1.5, over the exact bytes served) and the signature travels next to them as
// <file>.sig (base64 of the raw signature). The public key is compiled in (SigningKey.cs). A file without a
// valid signature is refused, whoever serves it: compromising the web host cannot ship code to players.
using System;
using System.Security.Cryptography;

namespace EchoLauncher
{
    public static partial class Signing
    {
        public static bool Verify(byte[] data, string signatureBase64, out string why)
        {
            why = "";
            byte[] sig;
            try { sig = Convert.FromBase64String((signatureBase64 ?? "").Trim()); }
            catch { why = "signature is not base64"; return false; }
            if (sig.Length != 384) { why = "signature is " + sig.Length + " bytes, not 384"; return false; }
            try
            {
                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.FromXmlString(PublicKeyXml);
                    if (rsa.VerifyData(data, "SHA256", sig)) return true;
                    why = "signature does not match the file";
                    return false;
                }
            }
            catch (Exception e) { why = e.Message; return false; }
        }
    }
}
