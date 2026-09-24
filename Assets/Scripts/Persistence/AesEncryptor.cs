using System;
using System.IO;
using System.Text;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Lightweight save obfuscator (XOR stream + managed FNV-1a checksum).
    ///
    /// WHY THIS EXISTS: the previous implementation used System.Security.Cryptography
    /// (Aes / Rfc2898DeriveBytes / SHA256) inside a *static field initializer*. On Android
    /// IL2CPP with the .NET Standard profile that API surface is not guaranteed to be
    /// implemented; a failure there is a TypeInitializationException thrown from inside a
    /// Unity native callback, which terminates the app during the first scene load
    /// (symptom: splash + one frame, then process exit).
    ///
    /// This implementation uses only managed primitives that always exist, so the save
    /// system can never take the game down. It is tamper *obfuscation*, not secrecy.
    /// </summary>
    public static class AesEncryptor
    {
        // Kept for API compatibility with the old class name.
        public const int KeySize = 256;
        public const int IvSize = 128;
        public const int SaltSize = 128;

        private const string Passphrase = "CIUR-UniversalRebellion-v1";
        private static readonly byte[] Salt = Encoding.UTF8.GetBytes("0F2C9E6A5D6B4F1C");
        private static readonly byte[] Magic = { (byte)'S', (byte)'B', (byte)'K', (byte)'1' };

        // ---- FNV-1a (managed, allocation free) -----------------------------------------
        private static uint Fnv1a(byte[] data, uint seed)
        {
            unchecked
            {
                uint hash = seed;
                for (int i = 0; i < data.Length; i++)
                {
                    hash ^= data[i];
                    hash *= 16777619u;
                }
                return hash;
            }
        }

        private static byte[] BuildKeyStream(uint round)
        {
            byte[] seed = new byte[Passphrase.Length + Salt.Length + 4];
            Encoding.UTF8.GetBytes(Passphrase, 0, Passphrase.Length, seed, 0);
            Buffer.BlockCopy(Salt, 0, seed, Passphrase.Length, Salt.Length);
            seed[seed.Length - 4] = (byte)(round & 0xFF);
            seed[seed.Length - 3] = (byte)((round >> 8) & 0xFF);
            seed[seed.Length - 2] = (byte)((round >> 16) & 0xFF);
            seed[seed.Length - 1] = (byte)((round >> 24) & 0xFF);
            return seed;
        }

        // ---- Transform ---------------------------------------------------------------
        private static byte[] Transform(byte[] data, uint round)
        {
            byte[] key = BuildKeyStream(round);
            byte[] result = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                // Non-linear mixing so identical plaintext blocks do not repeat.
                uint mixed = Fnv1a(key, (uint)i * 2654435761u + round);
                result[i] = (byte)(data[i] ^ (byte)(mixed & 0xFF) ^ (byte)((mixed >> 8) & 0xFF));
            }
            return result;
        }

        /// <summary>Encrypts a UTF-8 string into a magic-prefixed obfuscated blob.</summary>
        public static byte[] Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return Array.Empty<byte>();

            byte[] plain = Encoding.UTF8.GetBytes(plainText);
            uint checksum = Fnv1a(plain, 2166136261u);
            byte[] body = Transform(plain, checksum);

            byte[] result = new byte[Magic.Length + 4 + body.Length];
            Buffer.BlockCopy(Magic, 0, result, 0, Magic.Length);
            result[4] = (byte)(checksum & 0xFF);
            result[5] = (byte)((checksum >> 8) & 0xFF);
            result[6] = (byte)((checksum >> 16) & 0xFF);
            result[7] = (byte)((checksum >> 24) & 0xFF);
            Buffer.BlockCopy(body, 0, result, Magic.Length + 4, body.Length);
            return result;
        }

        /// <summary>Decrypts a blob produced by <see cref="Encrypt"/>; empty string if invalid.</summary>
        public static string Decrypt(byte[] cipherBytes)
        {
            if (cipherBytes == null || cipherBytes.Length <= Magic.Length + 4) return string.Empty;

            for (int i = 0; i < Magic.Length; i++)
            {
                if (cipherBytes[i] != Magic[i]) return string.Empty;
            }

            uint checksum = (uint)(cipherBytes[4]
                                   | (cipherBytes[5] << 8)
                                   | (cipherBytes[6] << 16)
                                   | (cipherBytes[7] << 24));

            int bodyLength = cipherBytes.Length - Magic.Length - 4;
            byte[] body = new byte[bodyLength];
            Buffer.BlockCopy(cipherBytes, Magic.Length + 4, body, 0, bodyLength);
            byte[] plain = Transform(body, checksum);

            if (Fnv1a(plain, 2166136261u) != checksum) return string.Empty;

            try
            {
                return Encoding.UTF8.GetString(plain);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>Writes the obfuscated payload atomically (temp file + replace).</summary>
        public static bool WriteFile(string path, byte[] payload)
        {
            if (string.IsNullOrEmpty(path) || payload == null || payload.Length == 0) return false;

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string temp = path + ".tmp";
                File.WriteAllBytes(temp, payload);
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
                return true;
            }
            catch (Exception)
            {
                // Never let persistence problems take the game down.
                return false;
            }
        }

        public static byte[] ReadFile(string path)
        {
            try
            {
                return !string.IsNullOrEmpty(path) && File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Stable hex digest used for integrity checks (managed FNV-1a).</summary>
        public static string ComputeHash(string input)
        {
            if (string.IsNullOrEmpty(input)) return "0";
            uint hash = Fnv1a(Encoding.UTF8.GetBytes(input), 2166136261u);
            return hash.ToString("x8");
        }
    }
}
