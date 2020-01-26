using System.Media;
using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;

namespace Chat
{
    /// <summary>
    /// Interaction logic for AddChatDialog.xaml
    /// </summary>
    public partial class AddChatDialog : UserControl
    {
        public AddChatDialog()
        {
            InitializeComponent();
            
        }

        private void OkButtonClicked(object sender, RoutedEventArgs e)
        {
            if (Validation.UsernameValidation(IdTextBox.Text))
                DialogHost.CloseDialogCommand.Execute(true,null);
            else
                SystemSounds.Beep.Play();
        }
    }
}
