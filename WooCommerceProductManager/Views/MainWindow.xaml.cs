using System.Windows;
using WooCommerceProductManager.ViewModels;

namespace WooCommerceProductManager.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
