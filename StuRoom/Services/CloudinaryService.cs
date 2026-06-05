using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace StuRoom.Services;

public class CloudinaryService : ICloudinaryService
{
    private readonly Cloudinary? _cloudinary;
    private readonly string _webRootPath;
    private readonly bool _useLocalFallback;

    public CloudinaryService(IConfiguration configuration, IWebHostEnvironment env)
    {
        _webRootPath = env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        
        var section = configuration.GetSection("Cloudinary");
        var cloudName = section["CloudName"];
        var apiKey = section["ApiKey"];
        var apiSecret = section["ApiSecret"];

        if (string.IsNullOrWhiteSpace(cloudName) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
        {
            _useLocalFallback = true;
        }
        else
        {
            var account = new Account(cloudName, apiKey, apiSecret);
            _cloudinary = new Cloudinary(account) { Api = { Secure = true } };
        }
    }

    public async Task<(string Url, string PublicId)> UploadAsync(
        IFormFile file, string folder = "sturoom/rooms")
    {
        if (_useLocalFallback)
        {
            var uploadDir = Path.Combine(_webRootPath, "uploads", "rooms");
            if (!Directory.Exists(uploadDir))
            {
                Directory.CreateDirectory(uploadDir);
            }

            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(uploadDir, fileName);

            await using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            var urlPath = $"/uploads/rooms/{fileName}";
            var publicId = $"local_{fileName}";

            return (urlPath, publicId);
        }

        await using var stream = file.OpenReadStream();

        var uploadParams = new ImageUploadParams
        {
            File           = new FileDescription(file.FileName, stream),
            Folder         = folder,
            Transformation = new Transformation()
                .Width(1200).Height(900).Crop("limit").Quality("auto").FetchFormat("auto")
        };

        var result = await _cloudinary!.UploadAsync(uploadParams);

        if (result.Error != null)
            throw new InvalidOperationException($"Cloudinary upload failed: {result.Error.Message}");

        return (result.SecureUrl.ToString(), result.PublicId);
    }

    public async Task DeleteAsync(string publicId)
    {
        if (_useLocalFallback)
        {
            if (publicId != null && publicId.StartsWith("local_"))
            {
                var fileName = publicId.Substring("local_".Length);
                var filePath = Path.Combine(_webRootPath, "uploads", "rooms", fileName);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            return;
        }

        var deleteParams = new DeletionParams(publicId);
        await _cloudinary!.DestroyAsync(deleteParams);
    }
}
