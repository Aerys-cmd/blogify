using Blogify.Web.Models;

namespace Blogify.Web.Models.Posts;

public sealed class PostCategory
{
    private PostCategory() { }

    internal PostCategory(Guid postId, Guid categoryId)
    {
        PostId = postId;
        CategoryId = categoryId;
    }

    public Guid PostId { get; private init; }
    public Guid CategoryId { get; private init; }
    public Post? Post { get; private set; }
    public Category? Category { get; private set; }
}

