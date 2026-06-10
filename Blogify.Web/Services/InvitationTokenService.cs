using System.Security.Cryptography;
using System.Text;

namespace Blogify.Web.Services;

public static class InvitationTokenService
{
    public static string CreateToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
