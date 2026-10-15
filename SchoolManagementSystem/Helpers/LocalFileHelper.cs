using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace SchoolManagementSystem.Helpers
{
    public class LocalFileHelper : IBlobHelper
    {
        private readonly string _storageFolder;

        public LocalFileHelper()
        {
            // Sets storage path to: YourProjectFolder/wwwroot/uploads
            _storageFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

            if (!Directory.Exists(_storageFolder))
            {
                Directory.CreateDirectory(_storageFolder);
            }
        }

        // 1. Overload for string path
        public async Task<Guid> UploadBlobAsync(string filePath, string containerName)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return Guid.Empty;
            }

            var fileId = Guid.NewGuid();
            var fileName = $"{fileId}{Path.GetExtension(filePath)}";
            var destinationPath = Path.Combine(_storageFolder, fileName);

            using (var sourceStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
            {
                await sourceStream.CopyToAsync(destinationStream);
            }

            return fileId;
        }

        // 2. Overload for IFormFile
        public async Task<Guid> UploadBlobAsync(IFormFile file, string containerName)
        {
            if (file == null || file.Length == 0)
            {
                return Guid.Empty;
            }

            var fileId = Guid.NewGuid();
            var fileName = $"{fileId}{Path.GetExtension(file.FileName)}";
            var destinationPath = Path.Combine(_storageFolder, fileName);

            using (var stream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
            {
                await file.CopyToAsync(stream);
            }

            return fileId;
        }

        // 3. Overload for byte[] array
        public async Task<Guid> UploadBlobAsync(byte[] buffer, string containerName)
        {
            if (buffer == null || buffer.Length == 0)
            {
                return Guid.Empty;
            }

            var fileId = Guid.NewGuid();
            // Defaulting to .jpg for image byte arrays; adjust if your app requires other formats
            var fileName = $"{fileId}.jpg";
            var destinationPath = Path.Combine(_storageFolder, fileName);

            await File.WriteAllBytesAsync(destinationPath, buffer);

            return fileId;
        }
    }
}
