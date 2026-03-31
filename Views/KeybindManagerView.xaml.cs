using System.Windows.Controls;
using GtavModManager.ViewModels;

namespace GtavModManager.Views
{
    public partial class KeybindManagerView : UserControl
    {
        public KeybindManagerView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is KeybindManagerViewModel vm)
                vm.EditKeybindRequested += ShowEditKeybindDialog;
        }

        private void ShowEditKeybindDialog(KeybindRowViewModel row)
        {
            var vm = DataContext as KeybindManagerViewModel;
            if (vm == null) return;

            string prompt = $"Enter new key binding for \"{row.Action}\" ({row.ModName}):\n" +
                            "Examples: F5   Ctrl+K   Shift+F6";

            string newValue = Microsoft.VisualBasic.Interaction.InputBox(
                prompt, "Edit Keybind", row.DisplayKey);

            if (!string.IsNullOrWhiteSpace(newValue) && newValue != row.DisplayKey)
                vm.ConfirmKeybindEdit(row, newValue);
        }
    }
}
