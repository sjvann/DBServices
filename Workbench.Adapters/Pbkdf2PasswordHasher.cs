using System.Security.Cryptography;
using System.Text;
using Workbench.Core.Ports;

namespace Workbench.Adapters;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    public string Hash(string secret)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(secret, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return "pbkdf2$100000$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);
    }

    public bool Verify(string secret, string verifier)
    {
        try
        {
            var parts = verifier.Split('$');
            if (parts.Length != 4 || parts[0] != "pbkdf2")
                return false;
            var iterations = int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(secret, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentOutOfRangeException or ArgumentException or OverflowException)
        {
            return false;
        }
    }
}
