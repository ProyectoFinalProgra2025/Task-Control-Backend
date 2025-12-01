using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Services.Interfaces
{
    public interface IStorageService
    {
        Task<string> UploadFileAsync(IFormFile file, string containerName, string fileName = null);
        Task<List<string>> UploadFilesAsync(List<IFormFile> files, string containerName);
    }
}