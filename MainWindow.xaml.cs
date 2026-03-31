using System.Windows;
using GtavModManager.ViewModels;

namespace GtavModManager
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}
