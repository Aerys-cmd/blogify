namespace Blogify.Web.Services;

public sealed class StorageOptions
{
    public sealed class R2Options
    {
        public string? AccountId { get; set; }
        public string? AccessKeyId { get; set; }
        public string? SecretAccessKey { get; set; }
        public string? BucketName { get; set; }
        public string? PublicBaseUrl { get; set; }
    }

    public R2Options R2 { get; set; } = new();

    public int ImageQuality { get; set; } = 60;

    public int ThumbnailQuality { get; set; } = 45;
}
