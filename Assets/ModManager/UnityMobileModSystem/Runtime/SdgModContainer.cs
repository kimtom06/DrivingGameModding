using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MobileModSystem
{
    /// <summary>
    /// Outer container used by .sdgmod files.
    ///
    /// Legacy format (v1): the .sdgmod file itself is a normal ZIP archive.
    /// Current format (v2): the ZIP payload is AES-256-CBC encrypted and authenticated
    /// with HMAC-SHA256. The existing manifest format inside the ZIP is unchanged.
    ///
    /// This prevents casual extraction by simply renaming .sdgmod to .zip.
    /// It is not intended to be unbreakable DRM because the game must contain the
    /// decryption material in order to load mods at runtime.
    /// </summary>
    public static class SdgModContainer
    {
        public enum PackageKind
        {
            Unknown,
            LegacyZip,
            EncryptedV2
        }

        private static readonly byte[] ContainerMagic =
        {
            (byte)'S', (byte)'D', (byte)'G', (byte)'M',
            (byte)'O', (byte)'D', (byte)'2', 0
        };

        private const byte ContainerVersion = 2;
        private const int SaltLength = 16;
        private const int IvLength = 16;
        private const int TagLength = 32;
        private const int HeaderLength = 52;
        private const int CopyBufferSize = 1024 * 128;

        // Split into two parts so the final master secret is not stored as one
        // contiguous byte array in the binary. This is only light obfuscation.
        private static readonly byte[] MasterSecretPartA =
        {
            0xA4, 0x19, 0x73, 0xD8, 0x2F, 0xB1, 0x6C, 0x05,
            0x9D, 0xE2, 0x40, 0x7A, 0xC7, 0x31, 0x58, 0xBE,
            0x16, 0x8B, 0xF0, 0x22, 0x69, 0xD5, 0x3C, 0x91,
            0x4E, 0xAA, 0x0D, 0xF7, 0x35, 0x62, 0xCB, 0x84
        };

        private static readonly byte[] MasterSecretPartB =
        {
            0x3C, 0xD6, 0x0A, 0x47, 0xB9, 0x28, 0xF5, 0x9E,
            0x61, 0x14, 0xD3, 0x80, 0x2B, 0xEC, 0x97, 0x45,
            0xAF, 0x30, 0x5D, 0xC8, 0x12, 0x7F, 0xE6, 0x0B,
            0x93, 0x54, 0xBA, 0x26, 0xD1, 0x08, 0x70, 0x3F
        };

        private static readonly byte[] EncryptionLabel =
            Encoding.UTF8.GetBytes("SDGMOD2-ENCRYPTION");

        private static readonly byte[] AuthenticationLabel =
            Encoding.UTF8.GetBytes("SDGMOD2-AUTHENTICATION");

        public static PackageKind DetectPackageKind(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return PackageKind.Unknown;

            using (FileStream stream = new FileStream(
                       path,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read,
                       4096,
                       FileOptions.SequentialScan))
            {
                if (stream.Length >= ContainerMagic.Length)
                {
                    byte[] prefix = new byte[ContainerMagic.Length];
                    ReadExactly(stream, prefix, 0, prefix.Length);

                    if (BytesEqual(prefix, ContainerMagic))
                        return PackageKind.EncryptedV2;
                }

                // Legacy .sdgmod files are ordinary ZIP archives. All standard ZIP
                // signatures begin with ASCII "PK" (local header, empty archive,
                // or data descriptor variants).
                stream.Position = 0;
                int first = stream.ReadByte();
                int second = stream.ReadByte();
                if (first == 'P' && second == 'K')
                    return PackageKind.LegacyZip;
            }

            return PackageKind.Unknown;
        }

        public static void EncryptZipToSdgMod(string zipPath, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
                throw new FileNotFoundException("ZIP payload was not found.", zipPath);
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Output path is empty.", nameof(outputPath));

            string outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            string cipherTempPath = Path.Combine(
                string.IsNullOrEmpty(outputDirectory) ? Path.GetTempPath() : outputDirectory,
                "sdgmod_cipher_" + Guid.NewGuid().ToString("N") + ".tmp");

            string outputTempPath = outputPath + ".tmp_" + Guid.NewGuid().ToString("N");

            byte[] salt = new byte[SaltLength];
            byte[] iv = new byte[IvLength];
            byte[] encryptionKey = null;
            byte[] authenticationKey = null;

            try
            {
                FillRandom(salt);
                FillRandom(iv);

                DeriveKeys(salt, out encryptionKey, out authenticationKey);

                EncryptFileToCipherTemp(
                    zipPath,
                    cipherTempPath,
                    encryptionKey,
                    iv);

                long cipherLength = new FileInfo(cipherTempPath).Length;
                if (cipherLength <= 0 || (cipherLength % 16L) != 0L)
                    throw new InvalidDataException("Encrypted SDGMOD payload has an invalid length.");

                byte[] header = BuildHeader(salt, iv, cipherLength);

                using (FileStream output = new FileStream(
                           outputTempPath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None,
                           CopyBufferSize,
                           FileOptions.SequentialScan))
                {
                    output.Write(header, 0, header.Length);

                    using (FileStream cipherInput = new FileStream(
                               cipherTempPath,
                               FileMode.Open,
                               FileAccess.Read,
                               FileShare.Read,
                               CopyBufferSize,
                               FileOptions.SequentialScan))
                    {
                        cipherInput.CopyTo(output, CopyBufferSize);
                    }
                }

                byte[] tag = ComputeHmacForFilePrefix(
                    outputTempPath,
                    HeaderLength + cipherLength,
                    authenticationKey);

                using (FileStream output = new FileStream(
                           outputTempPath,
                           FileMode.Append,
                           FileAccess.Write,
                           FileShare.None))
                {
                    output.Write(tag, 0, tag.Length);
                }

                if (File.Exists(outputPath))
                    File.Delete(outputPath);

                File.Move(outputTempPath, outputPath);
            }
            finally
            {
                SafeDelete(cipherTempPath);
                SafeDelete(outputTempPath);
                ClearBytes(encryptionKey);
                ClearBytes(authenticationKey);
                ClearBytes(salt);
                ClearBytes(iv);
            }
        }

        public static void DecryptSdgModToZip(string packagePath, string outputZipPath)
        {
            if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
                throw new FileNotFoundException("SDGMOD package was not found.", packagePath);
            if (string.IsNullOrWhiteSpace(outputZipPath))
                throw new ArgumentException("Output ZIP path is empty.", nameof(outputZipPath));

            byte[] header = new byte[HeaderLength];
            byte[] salt = null;
            byte[] iv = null;
            byte[] encryptionKey = null;
            byte[] authenticationKey = null;

            string tempOutputPath = outputZipPath + ".tmp_" + Guid.NewGuid().ToString("N");

            try
            {
                long cipherLength;

                using (FileStream input = new FileStream(
                           packagePath,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.Read,
                           CopyBufferSize,
                           FileOptions.SequentialScan))
                {
                    if (input.Length < HeaderLength + TagLength + 16L)
                        throw new InvalidDataException("SDGMOD v2 file is too small.");

                    ReadExactly(input, header, 0, header.Length);
                    ParseHeader(header, out salt, out iv, out cipherLength);

                    long expectedLength = HeaderLength + cipherLength + TagLength;
                    if (input.Length != expectedLength)
                        throw new InvalidDataException("SDGMOD v2 file length is invalid.");

                    if (cipherLength <= 0 || (cipherLength % 16L) != 0L)
                        throw new InvalidDataException("SDGMOD v2 encrypted payload length is invalid.");
                }

                DeriveKeys(salt, out encryptionKey, out authenticationKey);

                byte[] expectedTag = new byte[TagLength];
                using (FileStream input = new FileStream(
                           packagePath,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.Read,
                           4096,
                           FileOptions.SequentialScan))
                {
                    input.Position = HeaderLength + cipherLength;
                    ReadExactly(input, expectedTag, 0, expectedTag.Length);
                }

                byte[] actualTag = ComputeHmacForFilePrefix(
                    packagePath,
                    HeaderLength + cipherLength,
                    authenticationKey);

                if (!FixedTimeEquals(expectedTag, actualTag))
                    throw new InvalidDataException(
                        "SDGMOD v2 authentication failed. The file is damaged or was modified.");

                string outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputZipPath));
                if (!string.IsNullOrEmpty(outputDirectory))
                    Directory.CreateDirectory(outputDirectory);

                using (FileStream input = new FileStream(
                           packagePath,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.Read,
                           CopyBufferSize,
                           FileOptions.SequentialScan))
                using (Aes aes = Aes.Create())
                {
                    aes.KeySize = 256;
                    aes.BlockSize = 128;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = encryptionKey;
                    aes.IV = iv;

                    input.Position = HeaderLength;

                    using (ICryptoTransform decryptor = aes.CreateDecryptor())
                    using (FileStream output = new FileStream(
                               tempOutputPath,
                               FileMode.CreateNew,
                               FileAccess.Write,
                               FileShare.None,
                               CopyBufferSize,
                               FileOptions.SequentialScan))
                    using (CryptoStream crypto = new CryptoStream(
                               new LimitedReadStream(input, cipherLength),
                               decryptor,
                               CryptoStreamMode.Read))
                    {
                        crypto.CopyTo(output, CopyBufferSize);
                    }
                }

                // A valid decrypted package must itself be a ZIP archive.
                if (DetectPackageKind(tempOutputPath) != PackageKind.LegacyZip)
                    throw new InvalidDataException("Decrypted SDGMOD payload is not a ZIP archive.");

                if (File.Exists(outputZipPath))
                    File.Delete(outputZipPath);

                File.Move(tempOutputPath, outputZipPath);
            }
            catch (CryptographicException exception)
            {
                throw new InvalidDataException(
                    "SDGMOD v2 could not be decrypted. The file is damaged or incompatible.",
                    exception);
            }
            finally
            {
                SafeDelete(tempOutputPath);
                ClearBytes(encryptionKey);
                ClearBytes(authenticationKey);
                ClearBytes(salt);
                ClearBytes(iv);
            }
        }

        private static void EncryptFileToCipherTemp(
            string inputPath,
            string outputPath,
            byte[] encryptionKey,
            byte[] iv)
        {
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = encryptionKey;
                aes.IV = iv;

                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                using (FileStream input = new FileStream(
                           inputPath,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.Read,
                           CopyBufferSize,
                           FileOptions.SequentialScan))
                using (FileStream output = new FileStream(
                           outputPath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None,
                           CopyBufferSize,
                           FileOptions.SequentialScan))
                using (CryptoStream crypto = new CryptoStream(
                           output,
                           encryptor,
                           CryptoStreamMode.Write))
                {
                    input.CopyTo(crypto, CopyBufferSize);
                    crypto.FlushFinalBlock();
                }
            }
        }

        private static byte[] BuildHeader(byte[] salt, byte[] iv, long cipherLength)
        {
            using (MemoryStream memory = new MemoryStream(HeaderLength))
            using (BinaryWriter writer = new BinaryWriter(memory, Encoding.UTF8, true))
            {
                writer.Write(ContainerMagic);
                writer.Write(ContainerVersion);
                writer.Write((byte)0);      // flags
                writer.Write((ushort)0);    // reserved
                writer.Write(salt);
                writer.Write(iv);
                writer.Write(cipherLength);
                writer.Flush();

                byte[] header = memory.ToArray();
                if (header.Length != HeaderLength)
                    throw new InvalidOperationException("Internal SDGMOD header size mismatch.");

                return header;
            }
        }

        private static void ParseHeader(
            byte[] header,
            out byte[] salt,
            out byte[] iv,
            out long cipherLength)
        {
            if (header == null || header.Length != HeaderLength)
                throw new InvalidDataException("SDGMOD v2 header is invalid.");

            using (MemoryStream memory = new MemoryStream(header, false))
            using (BinaryReader reader = new BinaryReader(memory, Encoding.UTF8, true))
            {
                byte[] magic = reader.ReadBytes(ContainerMagic.Length);
                if (!BytesEqual(magic, ContainerMagic))
                    throw new InvalidDataException("Not an SDGMOD v2 container.");

                byte version = reader.ReadByte();
                if (version != ContainerVersion)
                    throw new InvalidDataException(
                        "Unsupported SDGMOD container version: " + version);

                byte flags = reader.ReadByte();
                ushort reserved = reader.ReadUInt16();
                if (flags != 0 || reserved != 0)
                    throw new InvalidDataException("Unsupported SDGMOD v2 container flags.");

                salt = reader.ReadBytes(SaltLength);
                iv = reader.ReadBytes(IvLength);
                cipherLength = reader.ReadInt64();

                if (salt.Length != SaltLength || iv.Length != IvLength)
                    throw new InvalidDataException("SDGMOD v2 header is truncated.");
            }
        }

        private static void DeriveKeys(
            byte[] salt,
            out byte[] encryptionKey,
            out byte[] authenticationKey)
        {
            byte[] masterSecret = GetMasterSecret();

            try
            {
                encryptionKey = DeriveKey(masterSecret, salt, EncryptionLabel);
                authenticationKey = DeriveKey(masterSecret, salt, AuthenticationLabel);
            }
            finally
            {
                ClearBytes(masterSecret);
            }
        }

        private static byte[] DeriveKey(byte[] masterSecret, byte[] salt, byte[] label)
        {
            byte[] input = new byte[salt.Length + label.Length];
            Buffer.BlockCopy(salt, 0, input, 0, salt.Length);
            Buffer.BlockCopy(label, 0, input, salt.Length, label.Length);

            try
            {
                using (HMACSHA256 hmac = new HMACSHA256(masterSecret))
                    return hmac.ComputeHash(input);
            }
            finally
            {
                ClearBytes(input);
            }
        }

        private static byte[] GetMasterSecret()
        {
            if (MasterSecretPartA.Length != MasterSecretPartB.Length)
                throw new InvalidOperationException("SDGMOD master secret is invalid.");

            byte[] result = new byte[MasterSecretPartA.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = (byte)(MasterSecretPartA[i] ^ MasterSecretPartB[i]);

            return result;
        }

        private static byte[] ComputeHmacForFilePrefix(
            string filePath,
            long bytesToHash,
            byte[] key)
        {
            if (bytesToHash < 0)
                throw new ArgumentOutOfRangeException(nameof(bytesToHash));

            using (HMACSHA256 hmac = new HMACSHA256(key))
            using (FileStream input = new FileStream(
                       filePath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read,
                       CopyBufferSize,
                       FileOptions.SequentialScan))
            {
                if (bytesToHash > input.Length)
                    throw new InvalidDataException("SDGMOD authentication range exceeds file length.");

                byte[] buffer = new byte[CopyBufferSize];
                long remaining = bytesToHash;

                while (remaining > 0)
                {
                    int request = (int)Math.Min(buffer.Length, remaining);
                    int read = input.Read(buffer, 0, request);
                    if (read <= 0)
                        throw new EndOfStreamException("Unexpected end of SDGMOD while authenticating.");

                    hmac.TransformBlock(buffer, 0, read, buffer, 0);
                    remaining -= read;
                }

                hmac.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return hmac.Hash;
            }
        }

        private static void FillRandom(byte[] buffer)
        {
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                rng.GetBytes(buffer);
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null)
                return false;

            int difference = a.Length ^ b.Length;
            int count = Math.Min(a.Length, b.Length);

            for (int i = 0; i < count; i++)
                difference |= a[i] ^ b[i];

            return difference == 0;
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }

            return true;
        }

        private static void ReadExactly(Stream stream, byte[] buffer, int offset, int count)
        {
            int total = 0;
            while (total < count)
            {
                int read = stream.Read(buffer, offset + total, count - total);
                if (read <= 0)
                    throw new EndOfStreamException("Unexpected end of file.");
                total += read;
            }
        }

        private static void SafeDelete(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }

        private static void ClearBytes(byte[] bytes)
        {
            if (bytes != null)
                Array.Clear(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Read-only wrapper that exposes only a fixed number of bytes from the
        /// underlying stream. Used to prevent the authentication tag from being
        /// passed into the AES decryptor.
        /// </summary>
        private sealed class LimitedReadStream : Stream
        {
            private readonly Stream inner;
            private long remaining;

            public LimitedReadStream(Stream inner, long length)
            {
                this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
                if (length < 0)
                    throw new ArgumentOutOfRangeException(nameof(length));
                remaining = length;
            }

            public override bool CanRead => inner.CanRead;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => remaining;

            public override long Position
            {
                get => 0;
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (remaining <= 0)
                    return 0;

                int requested = (int)Math.Min(count, remaining);
                int read = inner.Read(buffer, offset, requested);
                if (read > 0)
                    remaining -= read;
                return read;
            }

            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
