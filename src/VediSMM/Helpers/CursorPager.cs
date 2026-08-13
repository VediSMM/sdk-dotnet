using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using VediSMM.Errors;

namespace VediSMM.Helpers;

/// <summary>Lazily follows opaque cursor values and rejects loops.</summary>
/// <typeparam name="T">Item type.</typeparam>
public sealed class CursorPager<T> : IAsyncEnumerable<T>
{
    private readonly Func<string?, CancellationToken, Task<Page<T>>> fetchPageAsync;
    private readonly string? initialCursor;

    /// <summary>Creates a lazy pager.</summary>
    /// <param name="fetchPageAsync">Page callback receiving only the prior opaque cursor.</param>
    public CursorPager(Func<string?, CancellationToken, Task<Page<T>>> fetchPageAsync)
        : this(fetchPageAsync, null)
    {
    }

    /// <summary>Creates a lazy pager beginning with a supplied opaque cursor.</summary>
    /// <param name="fetchPageAsync">Page callback receiving only the prior opaque cursor.</param>
    /// <param name="initialCursor">Opaque cursor used for the first page.</param>
    public CursorPager(Func<string?, CancellationToken, Task<Page<T>>> fetchPageAsync, string? initialCursor)
    {
        this.fetchPageAsync = fetchPageAsync ?? throw new ArgumentNullException(nameof(fetchPageAsync));
        this.initialCursor = initialCursor;
    }

    /// <inheritdoc />
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        EnumerateAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);

    private async IAsyncEnumerable<T> EnumerateAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (initialCursor is not null)
        {
            seen.Add(initialCursor);
        }

        string? cursor = initialCursor;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Page<T> page = await fetchPageAsync(cursor, cancellationToken).ConfigureAwait(false)
                ?? throw new PaginationException("VediSMM page callback returned null.");
            foreach (T item in page.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
            }

            if (page.NextCursor is null)
            {
                yield break;
            }

            if (page.NextCursor.Length == 0 || !seen.Add(page.NextCursor))
            {
                throw new PaginationException("VediSMM cursor pagination repeated or returned an empty cursor.");
            }

            cursor = page.NextCursor;
        }
    }
}
