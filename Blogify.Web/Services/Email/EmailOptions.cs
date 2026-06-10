namespace Blogify.Web.Services.Email;

public sealed class EmailOptions
{
    public bool Enabled { get; set; }
    public string PublicBaseUrl { get; set; } = "https://localhost:5001";
    public string FromAddress { get; set; } = "no-reply@blogify.local";
    public string FromName { get; set; } = "Blogify";
    public int QueueCapacity { get; set; } = 100;
}
