#nullable enable

#pragma warning disable CS1591, SA1600

using System;
using System.Buffers.Binary;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;

namespace MediaBrowser.Controller.Authentication;

public static class TotpHelper
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const int SecretLength = 20;
    private const int CodeDigits = 6;
    private const int TimeStepSeconds = 30;
    private const int VerificationWindow = 1;

    public static string GenerateSecret()
    {
        Span<byte> bytes = stackalloc byte[SecretLength];
        RandomNumberGenerator.Fill(bytes);
        return ToBase32(bytes);
    }

    public static bool VerifyCode(string secret, string? code, DateTimeOffset timestamp)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var normalizedCode = new string(code.Where(char.IsDigit).ToArray());
        if (normalizedCode.Length != CodeDigits)
        {
            return false;
        }

        var counter = timestamp.ToUnixTimeSeconds() / TimeStepSeconds;
        for (var offset = -VerificationWindow; offset <= VerificationWindow; offset++)
        {
            var expectedCode = GenerateCode(secret, counter + offset);
            if (CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.ASCII.GetBytes(expectedCode),
                System.Text.Encoding.ASCII.GetBytes(normalizedCode)))
            {
                return true;
            }
        }

        return false;
    }

    public static string BuildOtpAuthUri(string issuer, string accountName, string secret)
    {
        var label = Uri.EscapeDataString($"{issuer}:{accountName}");
        var encodedIssuer = Uri.EscapeDataString(issuer);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"otpauth://totp/{label}?secret={secret}&issuer={encodedIssuer}&algorithm=SHA1&digits={CodeDigits}&period={TimeStepSeconds}");
    }

    private static string GenerateCode(string secret, long counter)
    {
        Span<byte> counterBytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counterBytes, counter);

        using var hmac = new HMACSHA1(FromBase32(secret));
        var hash = hmac.ComputeHash(counterBytes.ToArray());
        var offset = hash[^1] & 0x0f;
        var binaryCode = ((hash[offset] & 0x7f) << 24)
            | ((hash[offset + 1] & 0xff) << 16)
            | ((hash[offset + 2] & 0xff) << 8)
            | (hash[offset + 3] & 0xff);

        return (binaryCode % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    private static string ToBase32(ReadOnlySpan<byte> bytes)
    {
        var outputLength = (int)Math.Ceiling(bytes.Length / 5d) * 8;
        return string.Create(outputLength, bytes.ToArray(), static (chars, state) =>
        {
            var bitBuffer = 0;
            var bitCount = 0;
            var index = 0;

            foreach (var value in state)
            {
                bitBuffer = (bitBuffer << 8) | value;
                bitCount += 8;

                while (bitCount >= 5)
                {
                    chars[index++] = Alphabet[(bitBuffer >> (bitCount - 5)) & 31];
                    bitCount -= 5;
                }
            }

            if (bitCount > 0)
            {
                chars[index++] = Alphabet[(bitBuffer << (5 - bitCount)) & 31];
            }

            while (index < chars.Length)
            {
                chars[index++] = '=';
            }
        }).TrimEnd('=');
    }

    private static byte[] FromBase32(string secret)
    {
        var cleanSecret = secret.Trim().TrimEnd('=').ToUpperInvariant();
        var bytes = new byte[cleanSecret.Length * 5 / 8];
        var bitBuffer = 0;
        var bitCount = 0;
        var index = 0;

        foreach (var character in cleanSecret)
        {
            var value = Alphabet.IndexOf(character, StringComparison.Ordinal);
            if (value < 0)
            {
                throw new FormatException("Invalid base32 secret.");
            }

            bitBuffer = (bitBuffer << 5) | value;
            bitCount += 5;

            if (bitCount >= 8)
            {
                bytes[index++] = (byte)((bitBuffer >> (bitCount - 8)) & 0xff);
                bitCount -= 8;
            }
        }

        return bytes;
    }
}
