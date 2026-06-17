using Blogify.Web.Models;

namespace Blogify.Web.Models.Posts;

public sealed class PostTag
{
    private PostTag() { }

    internal PostTag(Guid postId, Guid tagId)
    {
        PostId = postId;
        TagId = tagId;
    }

    public Guid PostId { get; private init; }
    public Guid TagId { get; private init; }

    public Post? Post { get; private set; }
    public Tag? Tag { get; private set; }
}
