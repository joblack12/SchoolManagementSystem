using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace SchoolManagementSystem.Helpers
{
    public class LocalFileHelper : IBlobHelper
    {
        private readonly string _storageFolder;
        private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB limit

        // Allowlist of safe extensions and their valid file signature (magic bytes)
        private static readonly Dictionary<string, byte[][]> AllowedSignatures =
            new(StringComparer.OrdinalIgnoreCase)
            {
                { ".jpg",  new[] { new byte[] { 0xFF, 0xD8, 0xFF } } },
                { ".jpeg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } } },
                { ".png",  new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } } },
                { ".gif",  new[] { new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 }, new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 } } },
                { ".webp", new[] { new byte[] { 0x52, 0x49, 0x46, 0x46 } } }
            };

        public LocalFileHelper()
        {
            _storageFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

            if (!Directory.Exists(_storageFolder))
            {
                Directory.CreateDirectory(_storageFolder);
            }
        }

        // 1. Overload for string path (typically used by SeedDb)
        public async Task<Guid> UploadBlobAsync(string filePath, string containerName)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return Guid.Empty;
            }

            var extension = Path.GetExtension(filePath);
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length == 0 || fileInfo.Length > MaxFileSizeBytes || !AllowedSignatures.ContainsKey(extension))
            {
                throw new InvalidOperationException("Invalid file type or file exceeds the 5 MB limit.");
            }

            using (var sourceStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                if (!HasValidSignature(sourceStream, extension))
                {
                    throw new InvalidOperationException("File content does not match its extension.");
                }

                var fileId = Guid.NewGuid();
                var fileName = $"{fileId}{extension.ToLowerInvariant()}";
                var destinationPath = Path.Combine(_storageFolder, fileName);

                using (var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
                {
                    await sourceStream.CopyToAsync(destinationStream);
                }

                return fileId;
            }
        }

        // 2. Overload for IFormFile (user uploads)
        public async Task<Guid> UploadBlobAsync(IFormFile file, string containerName)
        {
            if (file == null || file.Length == 0)
            {
                return Guid.Empty;
            }

            if (file.Length > MaxFileSizeBytes)
            {
                throw new InvalidOperationException("File size exceeds the maximum allowed limit of 5 MB.");
            }

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrEmpty(extension) || !AllowedSignatures.ContainsKey(extension))
            {
                throw new InvalidOperationException("Unsupported file type. Only JPG, PNG, GIF, and WEBP images are allowed.");
            }

            using (var inputStream = file.OpenReadStream())
            {
                if (!HasValidSignature(inputStream, extension))
                {
                    throw new InvalidOperationException("Uploaded file content does not match a valid image signature.");
                }

                var fileId = Guid.NewGuid();
                var fileName = $"{fileId}{extension.ToLowerInvariant()}";
                var destinationPath = Path.Combine(_storageFolder, fileName);

                using (var outputStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
                {
                    await inputStream.CopyToAsync(outputStream);
                }

                return fileId;
            }
        }

        // 3. Overload for byte[] array
        public async Task<Guid> UploadBlobAsync(byte[] buffer, string containerName)
        {
            if (buffer == null || buffer.Length == 0)
            {
                return Guid.Empty;
            }

            if (buffer.Length > MaxFileSizeBytes)
            {
                throw new InvalidOperationException("File size exceeds the maximum allowed limit of 5 MB.");
            }

            using (var memoryStream = new MemoryStream(buffer))
            {
                if (!HasValidSignature(memoryStream, ".jpg"))
                {
                    throw new InvalidOperationException("Byte buffer does not contain a valid JPEG signature.");
                }
            }

            var fileId = Guid.NewGuid();
            var fileName = $"{fileId}.jpg";
            var destinationPath = Path.Combine(_storageFolder, fileName);

            await File.WriteAllBytesAsync(destinationPath, buffer);

            return fileId;
        }

        private static bool HasValidSignature(Stream stream, string extension)
        {
            if (!AllowedSignatures.TryGetValue(extension, out var signatures))
            {
                return false;
            }

            var maxHeaderLength = signatures.Max(s => s.Length);
            var headerBytes = new byte[maxHeaderLength];

            stream.Position = 0;
            var bytesRead = stream.Read(headerBytes, 0, maxHeaderLength);
            stream.Position = 0;

            return signatures.Any(sig =>
                bytesRead >= sig.Length &&
                headerBytes.Take(sig.Length).SequenceEqual(sig));
        }
    }
}
