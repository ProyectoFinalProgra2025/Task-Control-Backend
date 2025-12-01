using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Services
{
    public class AzureBlobStorageService : IStorageService
    {
        private readonly string _connectionString;
        private readonly IConfiguration _config;

        public AzureBlobStorageService(IConfiguration config)
        {
            _config = config;
            _connectionString = _config["AzureBlobStorage:ConnectionString"];
        }

        public async Task<string> UploadFileAsync(IFormFile file, string containerName, string fileName = null)
        {
            var blobServiceClient = new BlobServiceClient(_connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync();

            fileName ??= Guid.NewGuid() + Path.GetExtension(file.FileName);
            var blobClient = containerClient.GetBlobClient(fileName);

            using (var stream = file.OpenReadStream())
            {
                await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = file.ContentType });
            }
            return blobClient.Uri.ToString();
        }

        public async Task<List<string>> UploadFilesAsync(List<IFormFile> files, string containerName)
        {
            var urls = new List<string>();
            foreach (var file in files)
            {
                var url = await UploadFileAsync(file, containerName);
                urls.Add(url);
            }
            return urls;
        }
    }
}
