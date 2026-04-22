using System;
using System.Windows;
using CabinetDesigner.Presentation.ViewModels;

namespace CabinetDesigner.App;

public partial class MainWindow : Window
{
    public MainWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }
}
