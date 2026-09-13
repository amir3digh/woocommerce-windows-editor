using Microsoft.EntityFrameworkCore;
using WooCommerceProductManager.Data;
using WooCommerceProductManager.Models;
using WooCommerceProductManager.Services;

namespace WooCommerceProductManager.Repositories;

public sealed class ProductRepository : IProductRepository
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public ProductRepository(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<ProductListPage> GetPageAsync(
        int page,
        int perPage,
        string? search,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var query = db.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(product =>
                product.Name.Contains(term) ||
                (product.Sku != null && product.Sku.Contains(term)));
        }

        var totalItems = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)perPage);

        var entities = await query
            .OrderBy(product => product.Name)
            .ThenBy(product => product.WooCommerceId)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ProductListPage
        {
            Products = entities.Select(entity => entity.ToProduct()).ToList(),
            Page = page,
            PerPage = perPage,
            TotalItems = totalItems,
            TotalPages = totalPages
        };
    }

    public async Task<RemoteUpsertResult> UpsertFromRemoteAsync(
        Product remote,
        DateTimeOffset syncedAt,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var existing = await db.Products
            .FirstOrDefaultAsync(product => product.WooCommerceId == remote.Id, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            var created = new ProductEntity();
            created.CopyFromRemote(remote, syncedAt);
            db.Products.Add(created);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return RemoteUpsertResult.Added;
        }

        if (existing.IsDirty)
        {
            return RemoteUpsertResult.Conflict;
        }

        existing.CopyFromRemote(remote, syncedAt);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return RemoteUpsertResult.Updated;
    }

    public async Task<Product?> SaveLocalEditsAsync(Product product, bool isDirty, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var existing = await db.Products
            .FirstOrDefaultAsync(entity => entity.LocalId == product.LocalId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            return null;
        }

        existing.ApplyLocalEdits(product, isDirty);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return existing.ToProduct();
    }

    public async Task<Product?> ReplaceWithRemoteAsync(Product remote, DateTimeOffset syncedAt, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var existing = await db.Products
            .FirstOrDefaultAsync(
                entity => entity.LocalId == remote.LocalId || entity.WooCommerceId == remote.Id,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            return null;
        }

        existing.CopyFromRemote(remote, syncedAt);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return existing.ToProduct();
    }

    public async Task<Product> InsertFromRemoteAsync(Product remote, DateTimeOffset syncedAt, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = new ProductEntity();
        entity.CopyFromRemote(remote, syncedAt);
        db.Products.Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity.ToProduct();
    }

    public async Task DeleteAsync(long localId, long wooCommerceId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var existing = await db.Products
            .FirstOrDefaultAsync(
                entity => entity.LocalId == localId || entity.WooCommerceId == wooCommerceId,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            return;
        }

        db.Products.Remove(existing);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
