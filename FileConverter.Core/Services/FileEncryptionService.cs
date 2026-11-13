
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FileConverter.Core.Services
{
    public class FileEncryptionService
    {
        private const int SaltSize = 16;
        private const int IvSize = 16;
        private const int EncryptionKeySize = 32;
        private const int AuthenticationKeySize = 32;
        private const int Iterations = 200000;
        private const int MacSize = 32;
        private const int BlockSize = 16;

        private static readonly byte[] FileHeader =
            Encoding.ASCII.GetBytes("FCENC002");

        public void EncryptFile(
            string inputFile,
            string outputFile,
            string password,
            Action<double> progress = null)
        {
            ValidateArguments(inputFile, outputFile, password);

            if (!File.Exists(inputFile))
                throw new FileNotFoundException(
                    "Input file not found.", inputFile);

            if (File.Exists(outputFile))
                throw new IOException(
                    "The output file already exists.");

            byte[] salt = new byte[SaltSize];
            byte[] iv = new byte[IvSize];

            using (RandomNumberGenerator rng =
                RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
                rng.GetBytes(iv);
            }

            byte[] derivedKey = null;
            byte[] encryptionKey = null;
            byte[] authenticationKey = null;

            bool outputCreated = false;

            try
            {
                DeriveKeys(
                    password,
                    salt,
                    out derivedKey,
                    out encryptionKey,
                    out authenticationKey);

                using (var input = File.OpenRead(inputFile))
                using (var aes = Aes.Create())
                using (var encryptor = aes.CreateEncryptor(encryptionKey, iv))
                using (var output = new FileStream(outputFile, FileMode.CreateNew,
                    FileAccess.ReadWrite, FileShare.None))
                {
                    outputCreated = true;
                    output.Write(FileHeader, 0, FileHeader.Length);
                    output.Write(salt, 0, salt.Length);
                    output.Write(iv, 0, iv.Length);

                    long inputLength = Math.Max(1, input.Length);
                    using (var crypto = new CryptoStream(input, encryptor, CryptoStreamMode.Read))
                    {
                        byte[] buffer = new byte[81920];
                        int count;
                        while ((count = crypto.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            output.Write(buffer, 0, count);
                            progress?.Invoke(0.5 * input.Position / inputLength);
                        }
                    }

                    output.Position = 0;
                    using (var hmac = new HMACSHA256(authenticationKey))
                    {
                        byte[] tag = ComputeHmacWithoutTag(output, output.Length, hmac,
                            fraction => progress?.Invoke(0.5 + 0.5 * fraction));
                        output.Write(tag, 0, tag.Length);
                        ClearKey(tag);
                    }
                }
            }
            catch
            {
                if (outputCreated && File.Exists(outputFile))
                    File.Delete(outputFile);

                throw;
            }
            finally
            {
                ClearKey(derivedKey);
                ClearKey(encryptionKey);
                ClearKey(authenticationKey);
            }
        }

        public void DecryptFile(
            string inputFile,
            string outputFile,
            string password,
            Action<double> progress = null)
        {
            ValidateArguments(inputFile, outputFile, password);

            if (!File.Exists(inputFile))
                throw new FileNotFoundException(
                    "Encrypted file not found.", inputFile);

            if (File.Exists(outputFile))
                throw new IOException(
                    "The output file already exists.");

            byte[] salt = new byte[SaltSize];
            byte[] iv = new byte[IvSize];
            byte[] storedTag = new byte[MacSize];

            byte[] derivedKey = null;
            byte[] encryptionKey = null;
            byte[] authenticationKey = null;

            bool outputCreated = false;

            try
            {
                long dataOffset =
                    FileHeader.Length + SaltSize + IvSize;

                using (FileStream input = File.OpenRead(inputFile))
                {
                    long cipherLength = input.Length - dataOffset - MacSize;
                    if (cipherLength <= 0 || cipherLength % BlockSize != 0)
                        throw new InvalidDataException("Invalid encrypted file length.");

                    byte[] header = new byte[FileHeader.Length];
                    ReadExactly(input, header);
                    if (!ByteArraysEqual(header, FileHeader))
                        throw new InvalidDataException("Unrecognized encrypted file format.");

                    ReadExactly(input, salt);
                    ReadExactly(input, iv);
                    input.Position = dataOffset + cipherLength;
                    ReadExactly(input, storedTag);

                    DeriveKeys(password, salt, out derivedKey, out encryptionKey,
                        out authenticationKey);

                    input.Position = 0;
                    using (var hmac = new HMACSHA256(authenticationKey))
                    {
                        byte[] calculatedTag = ComputeHmacWithoutTag(
                            input, dataOffset + cipherLength, hmac, fraction => progress?.Invoke(0.5 * fraction));
                        if (!ConstantTimeEquals(calculatedTag, storedTag))
                            throw new CryptographicException(
                                "Incorrect password or the encrypted file has been modified.");
                    }

                    input.Position = dataOffset;
                    using (Aes aes = Aes.Create())
                    using (var decryptor = aes.CreateDecryptor(encryptionKey, iv))
                    using (var output = new FileStream(outputFile, FileMode.CreateNew, FileAccess.Write))
                    {
                        outputCreated = true;
                        using (var crypto = new CryptoStream(output, decryptor, CryptoStreamMode.Write))
                        {
                            byte[] buffer = new byte[8192];
                            long totalCipherBytes = cipherLength;
                            while (cipherLength > 0)
                            {
                                int count = input.Read(buffer, 0, (int)Math.Min(buffer.Length, cipherLength));
                                if (count == 0)
                                    throw new EndOfStreamException();
                                crypto.Write(buffer, 0, count);
                                cipherLength -= count;
                                progress?.Invoke(1 - 0.5 * cipherLength / totalCipherBytes);
                            }
                        }
                    }
                }
            }
            catch
            {
                if (outputCreated && File.Exists(outputFile))
                    File.Delete(outputFile);

                throw;
            }
            finally
            {
                ClearKey(derivedKey);
                ClearKey(encryptionKey);
                ClearKey(authenticationKey);
                ClearKey(storedTag);
            }
        }

        private static void ValidateArguments(
            string inputFile,
            string outputFile,
            string password)
        {
            if (string.IsNullOrWhiteSpace(inputFile))
                throw new ArgumentException(
                    "An input file is required.");

            if (string.IsNullOrWhiteSpace(outputFile))
                throw new ArgumentException(
                    "An output file is required.");

            if (string.IsNullOrEmpty(password))
                throw new ArgumentException(
                    "A password is required.");

            string fullInput =
                Path.GetFullPath(inputFile);

            string fullOutput =
                Path.GetFullPath(outputFile);

            if (string.Equals(
                fullInput,
                fullOutput,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException(
                    "Input and output files must be different.");
            }
        }

        private static void DeriveKeys(
            string password,
            byte[] salt,
            out byte[] derivedKey,
            out byte[] encryptionKey,
            out byte[] authenticationKey)
        {
            using (Rfc2898DeriveBytes derivation =
                new Rfc2898DeriveBytes(
                    password,
                    salt,
                    Iterations))
            {
                derivedKey = derivation.GetBytes(
                    EncryptionKeySize + AuthenticationKeySize);
            }

            encryptionKey = new byte[EncryptionKeySize];
            authenticationKey = new byte[AuthenticationKeySize];

            Buffer.BlockCopy(
                derivedKey,
                0,
                encryptionKey,
                0,
                EncryptionKeySize);

            Buffer.BlockCopy(
                derivedKey,
                EncryptionKeySize,
                authenticationKey,
                0,
                AuthenticationKeySize);
        }

        private static byte[] ComputeHmacWithoutTag(
            Stream input,
            long bytesToHash,
            HMACSHA256 hmac,
            Action<double> progress)
        {
            byte[] buffer = new byte[8192];

            long remaining = bytesToHash;

            while (remaining > 0)
            {
                int bytesToRead =
                    (int)Math.Min(buffer.Length, remaining);

                int bytesRead =
                    input.Read(buffer, 0, bytesToRead);

                if (bytesRead <= 0)
                    throw new EndOfStreamException(
                        "Unexpected end of encrypted file.");

                hmac.TransformBlock(
                    buffer,
                    0,
                    bytesRead,
                    buffer,
                    0);

                remaining -= bytesRead;
                progress?.Invoke(1 - (double)remaining / bytesToHash);
            }

            hmac.TransformFinalBlock(
                new byte[0],
                0,
                0);

            return hmac.Hash;
        }

        private static void ReadExactly(
            Stream input,
            byte[] buffer)
        {
            int offset = 0;

            while (offset < buffer.Length)
            {
                int bytesRead = input.Read(
                    buffer,
                    offset,
                    buffer.Length - offset);

                if (bytesRead <= 0)
                    throw new EndOfStreamException(
                        "The encrypted file is incomplete.");

                offset += bytesRead;
            }
        }

        private static bool ConstantTimeEquals(
            byte[] first,
            byte[] second)
        {
            if (first == null || second == null)
                return false;

            if (first.Length != second.Length)
                return false;

            int difference = 0;

            for (int i = 0; i < first.Length; i++)
            {
                difference |= first[i] ^ second[i];
            }

            return difference == 0;
        }

        private static bool ByteArraysEqual(
            byte[] first,
            byte[] second)
        {
            if (first == null || second == null)
                return false;

            if (first.Length != second.Length)
                return false;

            for (int i = 0; i < first.Length; i++)
            {
                if (first[i] != second[i])
                    return false;
            }

            return true;
        }

        private static void ClearKey(byte[] value)
        {
            if (value != null)
                Array.Clear(value, 0, value.Length);
        }
    }
}
