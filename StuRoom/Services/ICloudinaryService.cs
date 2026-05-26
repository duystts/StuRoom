namespace StuRoom.Services;

public interface ICloudinaryService
{
    /// <summary>Upload a file and return (secureUrl, publicId).</summary>
    Task<(string Url, string PublicId)> UploadAsync(IFormFile file, string folder = "sturoom/rooms");

    /// <summary>Delete an asset by its publicId.</summary>
    Task DeleteAsync(string publicId);
}
