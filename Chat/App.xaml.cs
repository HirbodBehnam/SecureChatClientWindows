using System;
using System.Windows;
using Chat.Properties;

namespace Chat
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            StartupUri = string.IsNullOrWhiteSpace(Settings.Default.LoginData) ? new Uri("LoginWindow.xaml", UriKind.Relative) : new Uri("MainChatsWindow.xaml", UriKind.Relative);
        }
    }
}
