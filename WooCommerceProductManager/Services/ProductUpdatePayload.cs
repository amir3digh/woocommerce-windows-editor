using System.Text.Json.Nodes;
using WooCommerceProductManager.Models;

namespace WooCommerceProductManager.Services;

public sealed class ProductEditValues
{
    public required string Name { get; init; }

    public required string Sku { get; init; }

    public required string RegularPrice { get; init; }

    public required string SalePrice { get; init; }

    public int? StockQuantity { get; init; }

    public required string StockStatus { get; init; }

    public bool ManageStock { get; init; }
}

public static class ProductUpdatePayload
{
    public static string? TryBuildChangedJson(Product original, ProductEditValues edited)
    {
        var body = new JsonObject();

        if (!string.Equals(original.Name, edited.Name, StringComparison.Ordinal))
        {
            body["name"] = edited.Name;
        }

        var originalSku = original.Sku ?? string.Empty;
        if (!string.Equals(originalSku, edited.Sku, StringComparison.Ordinal))
        {
            body["sku"] = edited.Sku;
        }

        if (!string.Equals(NormalizePrice(original.RegularPrice), edited.RegularPrice, StringComparison.Ordinal))
        {
            body["regular_price"] = edited.RegularPrice;
        }

        if (!string.Equals(NormalizePrice(original.SalePrice), edited.SalePrice, StringComparison.Ordinal))
        {
            body["sale_price"] = edited.SalePrice;
        }

        if (!string.Equals(original.StockStatus ?? string.Empty, edited.StockStatus, StringComparison.Ordinal))
        {
            body["stock_status"] = edited.StockStatus;
        }

        if (edited.ManageStock && original.StockQuantity != edited.StockQuantity)
        {
            if (edited.StockQuantity is int quantity)
            {
                body["stock_quantity"] = quantity;
            }
            else
            {
                body["stock_quantity"] = null;
            }
        }

        return body.Count == 0 ? null : body.ToJsonString();
    }

    public static string BuildFullJson(Product local)
    {
        var body = new JsonObject
        {
            ["name"] = local.Name,
            ["sku"] = local.Sku ?? string.Empty,
            ["regular_price"] = NormalizePrice(local.RegularPrice),
            ["sale_price"] = NormalizePrice(local.SalePrice),
            ["stock_status"] = local.StockStatus ?? "instock"
        };

        if (local.ManageStock && local.StockQuantity is int quantity)
        {
            body["stock_quantity"] = quantity;
        }

        return body.ToJsonString();
    }

    public static Product ToLocalProduct(Product original, ProductEditValues edited)
    {
        var sale = string.IsNullOrWhiteSpace(edited.SalePrice) ? string.Empty : edited.SalePrice;
        return new Product
        {
            Id = original.Id,
            LocalId = original.LocalId,
            Name = edited.Name,
            Sku = string.IsNullOrWhiteSpace(edited.Sku) ? null : edited.Sku,
            RegularPrice = edited.RegularPrice,
            SalePrice = sale,
            Price = string.IsNullOrWhiteSpace(sale) ? edited.RegularPrice : sale,
            ManageStock = original.ManageStock,
            StockQuantity = original.ManageStock ? edited.StockQuantity : original.StockQuantity,
            StockStatus = edited.StockStatus,
            DateModified = original.DateModified,
            Images = original.Images,
            IsDirty = true
        };
    }

    private static string NormalizePrice(string? price)
    {
        if (string.IsNullOrWhiteSpace(price))
        {
            return string.Empty;
        }

        return WooCommerceProductManager.Helpers.WooCommercePriceParser.TryParseNonNegative(
            price,
            allowEmpty: true,
            out var apiValue,
            out _)
            ? apiValue
            : price.Trim();
    }
}
