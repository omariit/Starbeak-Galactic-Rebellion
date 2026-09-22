using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// AES-256-CBC JSON serializer. Writes an encrypted blob to
    /// Application.persistentDataPath/usr_profile.dat.
    /// </summary>
    public static class AesEncryptor
    {
        private const int KeySize = 256;
        private const int IvSize = 128;
        private const int SaltSize = 128;

        // Per-install key material. In production, replace with a device-specific
        // value bound to the install (e.g. SystemInfo.deviceUniqueIdentifier).
        private static readonly string Passphrase = "CIUR-UniversalRebellion-v1";
        private static readonly byte[] Salt =
            Encoding.UTF8.GetBytes("0F2C9E6A5D6B4F1C");

        private static readonly byte[] Key = DeriveKey(Passphrase, Salt);

        /// <summary>Encrypts a UTF-8 string into a length-prefixed byte array.</summary>
        public static byte[] Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return Array.Empty<byte>();

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] cipherBytes;

            using (Aes aes = Aes.Create())
            {
                aes.KeySize = KeySize;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = Key;
                aes.GenerateIV();

                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                using (MemoryStream memory = new MemoryStream(plainBytes.Length + IvSize / 8))
                {
                    memory.Write(aes.IV, 0, aes.IV.Length);
                    using (CryptoStream crypto = new CryptoStream(memory, encryptor, CryptoStreamMode.Write))
                    {
                        crypto.Write(plainBytes, 0, plainBytes.Length);
                        crypto.FlushFinalBlock();
                    }
                    cipherBytes = memory.ToArray();
                }
            }
            return cipherBytes;
        }

        /// <summary>Decrypts a previously encrypted byte array back to a UTF-8 string.</summary>
        public static string Decrypt(byte[] cipherBytes)
        {
            if (cipherBytes == null || cipherBytes.Length == 0) return string.Empty;

            int ivLength = IvSize / 8;
            if (cipherBytes.Length < ivLength) return string.Empty;

            byte[] iv = new byte[ivLength];
            Buffer.BlockCopy(cipherBytes, 0, iv, 0, ivLength);

            int cipherLength = cipherBytes.Length - ivLength;
            byte[] cipher = new byte[cipherLength];
            Buffer.BlockCopy(cipherBytes, ivLength, cipher, 0, cipherLength);

            try
            {
                using (Aes aes = Aes.Create())
                {
                    aes.KeySize = KeySize;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = Key;
                    aes.IV = iv;

                    using (ICryptoTransform decryptor = aes.CreateDecryptor())
                    using (MemoryStream memory = new MemoryStream(cipher))
                    using (CryptoStream crypto = new CryptoStream(memory, decryptor, CryptoStreamMode.Read))
                    using (StreamReader reader = new StreamReader(crypto, Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            catch (CryptographicException)
            {
                // Tampered or incompatible blob - never crash the game over a save file.
                return string.Empty;
            }
            catch (DecoderFallbackException)
            {
                return string.Empty;
            }
        }

        /// <summary>Writes the encrypted payload atomically (temp file + replace).</summary>
        public static bool WriteFile(string path, byte[] payload)
        {
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string temp = path + ".tmp";
                File.WriteAllBytes(temp, payload);
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        public static byte[] ReadFile(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        private static byte[] DeriveKey(string passphrase, byte[] salt)
        {
            using (Rfc2898DeriveBytes kdf = new Rfc2898DeriveBytes(
                Encoding.UTF8.GetBytes(passphrase), salt, 10000, HashAlgorithmName.SHA256))
            {
                return kdf.GetBytes(KeySize / 8);
            }
        }

        /// <summary>Hex digest used for integrity checking of blueprint configs.</summary>
        public static string ComputeHash(string input)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder builder = new StringBuilder(digest.Length * 2);
                for (int i = 0; i < digest.Length; i++) builder.Append(digest[i].ToString("x2"));
                return builder.ToString();
            }
        }
    }
}
