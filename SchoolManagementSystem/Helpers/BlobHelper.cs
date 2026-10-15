using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace SchoolManagementSystem.Helpers
{
    public class BlobHelper : IBlobHelper
    {
        private readonly BlobServiceClient _serviceClient;
        private readonly string _defaultContainer;

        public BlobHelper(BlobServiceClient serviceClient, string containerName)
        {
            _serviceClient = serviceClient ?? throw new ArgumentNullException(nameof(serviceClient));
            _defaultContainer = string.IsNullOrWhiteSpace(containerName) ? "app-container" : containerName;

            var c = _serviceClient.GetBlobContainerClient(_defaultContainer);
            c.CreateIfNotExists(PublicAccessType.None);
        }

        private BlobContainerClient GetContainer(string? containerName)
            => _serviceClient.GetBlobContainerClient(string.IsNullOrWhiteSpace(containerName) ? _defaultContainer : containerName!);

        // IBlobHelper implementations

        public async Task<Guid> UploadBlobAsync(IFormFile file, string containerName)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));

            var ext = Path.GetExtension(file.FileName) ?? string.Empty;
            var id = Guid.NewGuid();
            var blobName = $"{id}{ext}";

            var container = GetContainer(containerName);
            var blob = container.GetBlobClient(blobName);

            using var stream = file.OpenReadStream();
            await blob.UploadAsync(stream, overwrite: true);

            return id;
        }

        public async Task<Guid> UploadBlobAsync(byte[] file, string containerName)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));

            var id = Guid.NewGuid();
            var blobName = id.ToString();

            var container = GetContainer(containerName);
            using var ms = new MemoryStream(file);
            await container.GetBlobClient(blobName).UploadAsync(ms, overwrite: true);

            return id;
        }

        public async Task<Guid> UploadBlobAsync(string image, string containerName)
        {
            if (string.IsNullOrWhiteSpace(image)) throw new ArgumentNullException(nameof(image));

            byte[] bytes;
            string ext = string.Empty;

            if (image.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                // data:[<mediatype>][;base64],<data>
                var comma = image.IndexOf(',');
                if (comma < 0) throw new ArgumentException("Invalid data URI.", nameof(image));

                var meta = image.Substring(5, comma - 5); // skip "data:"
                var parts = meta.Split(';', StringSplitOptions.RemoveEmptyEntries);
                var mime = parts.Length > 0 ? parts[0] : string.Empty;

                // basic mime -> extension mapping
                ext = mime switch
                {
                    "image/png" => ".png",
                    "image/jpeg" => ".jpg",
                    "image/jpg" => ".jpg",
                    "image/gif" => ".gif",
                    _ => string.Empty
                };

                var base64 = image.Substring(comma + 1);
                bytes = Convert.FromBase64String(base64);
            }
            else
            {
                // treat as URL - download
                using var client = new HttpClient();
                bytes = await client.GetByteArrayAsync(image);
                // extension not guaranteed
            }

            return await UploadBlobAsync(bytes, containerName) with { };
        }

        // Backwards-compatible helpers (kept for other code)
        private BlobContainerClient _cachedContainer => GetContainer(null);

        public async Task UploadAsync(string blobName, Stream content)
        {
            if (string.IsNullOrWhiteSpace(blobName)) throw new ArgumentNullException(nameof(blobName));
            if (content == null) throw new ArgumentNullException(nameof(content));

            content.Position = 0;
            var client = _cachedContainer.GetBlobClient(blobName);
            await client.UploadAsync(content, overwrite: true);
        }

        public async Task<Stream?> DownloadAsync(string blobName)
        {
            if (string.IsNullOrWhiteSpace(blobName)) return null;

            var client = _cachedContainer.GetBlobClient(blobName);
            if (await client.ExistsAsync())
            {
                var res = await client.DownloadAsync();
                var ms = new MemoryStream();
                await res.Value.Content.CopyToAsync(ms);
                ms.Position = 0;
                return ms;
            }
            return null;
        }

        public async Task DeleteAsync(string blobName)
        {
            if (string.IsNullOrWhiteSpace(blobName)) return;

            var client = _cachedContainer.GetBlobClient(blobName);
            await client.DeleteIfExistsAsync();
        }
    }
}