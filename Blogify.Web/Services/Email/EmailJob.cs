namespace Blogify.Web.Services.Email;

public sealed record EmailJob(string Recipient, string Subject, string HtmlBody);
