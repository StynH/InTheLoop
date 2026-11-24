using InTheLoop.Core.Messaging;
using InTheLoop.Core.Models;
using InTheLoop.Core.Storage;

namespace InTheLoop.Core.Features;

public sealed record GetReview(string ReviewId) : IRequest<Review?>;

internal sealed class GetReviewHandler(IReviewStore store) : IRequestHandler<GetReview, Review?>
{
    public Task<Review?> HandleAsync(GetReview request, CancellationToken cancellationToken) =>
        store.GetAsync(request.ReviewId.Trim(), cancellationToken);
}

public sealed record ListReviews(ReviewStatus? Status = null) : IRequest<IReadOnlyList<Review>>;

internal sealed class ListReviewsHandler(IReviewStore store) : IRequestHandler<ListReviews, IReadOnlyList<Review>>
{
    public async Task<IReadOnlyList<Review>> HandleAsync(ListReviews request, CancellationToken cancellationToken)
    {
        var reviews = await store.ListAsync(cancellationToken);
        return request.Status is { } status ? reviews.Where(r => r.Status == status).ToList() : reviews;
    }
}

public sealed record DeleteReview(string ReviewId) : IRequest<bool>;

internal sealed class DeleteReviewHandler(IReviewStore store) : IRequestHandler<DeleteReview, bool>
{
    public Task<bool> HandleAsync(DeleteReview request, CancellationToken cancellationToken) =>
        store.DeleteAsync(request.ReviewId, cancellationToken);
}
