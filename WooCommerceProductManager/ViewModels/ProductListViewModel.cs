using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WooCommerceProductManager.Models;
using WooCommerceProductManager.Services;

namespace WooCommerceProductManager.ViewModels;

public partial class ProductListViewModel : ObservableObject
{
    [ObservableProperty]
    private Product? _selectedProduct;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _perPage = ProductService.DefaultPageSize;

    [ObservableProperty]
    private int? _totalPages;

    [ObservableProperty]
    private int? _totalItems;

    public ObservableCollection<Product> Items { get; } = [];

    public bool HasProducts => Items.Count > 0;

    public bool HasNoProducts => Items.Count == 0 && !IsLoading;

    public bool CanGoPrevious => CurrentPage > 1;

    public bool CanGoNext =>
        TotalPages.HasValue
            ? CurrentPage < TotalPages.Value
            : Items.Count >= PerPage && Items.Count > 0;

    public string PageSummary
    {
        get
        {
            if (TotalPages is > 0)
            {
                return $"Page {CurrentPage} of {TotalPages}";
            }

            return $"Page {CurrentPage}";
        }
    }

    public ProductListViewModel()
    {
        Items.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasProducts));
            OnPropertyChanged(nameof(HasNoProducts));
            OnPropertyChanged(nameof(CanGoNext));
        };
    }

    public void ApplyPage(ProductListPage page, long? selectedLocalId = null)
    {
        Items.Clear();
        foreach (var product in page.Products)
        {
            Items.Add(product);
        }

        CurrentPage = page.Page;
        PerPage = page.PerPage;
        TotalItems = page.TotalItems;
        TotalPages = page.TotalPages;
        SelectedProduct = selectedLocalId is long id
            ? Items.FirstOrDefault(product => product.LocalId == id)
            : null;
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(PageSummary));
    }

    public void ReplaceProduct(Product updated)
    {
        var existing = Items.FirstOrDefault(product => product.LocalId == updated.LocalId);
        if (existing is null)
        {
            return;
        }

        var index = Items.IndexOf(existing);
        Items[index] = updated;
        if (SelectedProduct is null || SelectedProduct.LocalId == updated.LocalId)
        {
            SelectedProduct = updated;
        }
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(HasNoProducts));

    partial void OnCurrentPageChanged(int value)
    {
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(PageSummary));
    }

    partial void OnTotalPagesChanged(int? value)
    {
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(PageSummary));
    }
}
