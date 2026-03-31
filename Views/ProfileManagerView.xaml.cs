using System.Windows;
using System.Windows.Controls;
using GtavModManager.ViewModels;

namespace GtavModManager.Views
{
    public partial class ProfileManagerView : UserControl
    {
        public ProfileManagerView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is ProfileManagerViewModel vm)
                vm.CreateProfileRequested += ShowCreateProfileDialog;
        }

        private void ShowCreateProfileDialog()
        {
            var vm = DataContext as ProfileManagerViewModel;
            if (vm == null) return;

            string name = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter a name for the new profile:", "Create Profile", "");

            if (!string.IsNullOrWhiteSpace(name))
                vm.ConfirmCreateProfile(name);
        }
    }
}
