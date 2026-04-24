using System;
using System.Windows;
using CabinetDesigner.Presentation.ViewModels;

namespace CabinetDesigner.Presentation.Views;

public partial class AlphaLimitationsDialog : Window
{
    public AlphaLimitationsDialog(AlphaLimitationsDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }
}
