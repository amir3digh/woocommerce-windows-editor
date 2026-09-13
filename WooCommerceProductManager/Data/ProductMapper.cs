using WooCommerceProductManager.Data;
using WooCommerceProductManager.Models;

namespace WooCommerceProductManager.Data;

public static class ProductMapper
{
    public static Product ToProduct(this ProductEntity entity)
    {
        var images = string.IsNullOrWhiteSpace(entity.ImageUrl)
            ? new List<ProductImage>()
            : [new ProductImage { Src = entity.ImageUrl }];

        return new Product
        {
            Id = entity.WooCommerceId,
            LocalId = entity.LocalId,
            Name = entity.Name,
            Sku = entity.Sku,
            RegularPrice = entity.RegularPrice,
            SalePrice = entity.SalePrice,
            Price = entity.Price,
            ManageStock = entity.ManageStock,
            StockQuantity = entity.StockQuantity,
            StockStatus = entity.StockStatus,
            DateModified = entity.DateModified,
            Images = images,
            IsDirty = entity.IsDirty
        };
    }

    public static void CopyFromRemote(this ProductEntity entity, Product remote, DateTimeOffset syncedAt)
    {
        entity.WooCommerceId = remote.Id;
        entity.Name = remote.Name;
        entity.Sku = remote.Sku;
        entity.RegularPrice = remote.RegularPrice;
        entity.SalePrice = remote.SalePrice;
        entity.Price = remote.Price;
        entity.ManageStock = remote.ManageStock;
        entity.StockQuantity = remote.StockQuantity;
        entity.StockStatus = remote.StockStatus;
        entity.ImageUrl = remote.FirstImageUrl;
        entity.DateModified = remote.DateModified;
        entity.LastSyncedAt = syncedAt;
        entity.IsDirty = false;
    }

    public static void ApplyLocalEdits(this ProductEntity entity, Product edited, bool isDirty)
    {
        entity.Name = edited.Name;
        entity.Sku = edited.Sku;
        entity.RegularPrice = edited.RegularPrice;
        entity.SalePrice = edited.SalePrice;
        entity.Price = edited.Price;
        entity.StockQuantity = edited.ManageStock ? edited.StockQuantity : entity.StockQuantity;
        entity.StockStatus = edited.StockStatus;
        entity.IsDirty = isDirty;
        if (!isDirty)
        {
            entity.LastSyncedAt = DateTimeOffset.Now;
            entity.DateModified = edited.DateModified;
            if (!string.IsNullOrWhiteSpace(edited.FirstImageUrl))
            {
                entity.ImageUrl = edited.FirstImageUrl;
            }
        }
    }
}
