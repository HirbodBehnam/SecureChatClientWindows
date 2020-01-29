using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using Newtonsoft.Json;
using org.whispersystems.curve25519;

namespace Chat
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            TrustSslCheckBox.IsChecked = Properties.Settings.Default.TrustInvalidSSL;
        }

        private async void LoginButtonClicked(object sender, RoutedEventArgs e)
        {
            LoadingDialogSample loadingDialog = new LoadingDialogSample // simple dialog with a spinning indicator
            {
                LoadingTextText = {Text = "Connecting to server..."} // set the text of the dialog
            };
            Exception error = null;
            await DialogHost.Show(loadingDialog,"LoginDialogHost", delegate(object sender1, DialogOpenedEventArgs args) // show the dialog
            {
                string serverAddress = ServerUrlTxt.Text, username = UsernameTxt.Text, password = PasswordTxt.Password;
                bool trustEveryThing = TrustSslCheckBox.IsChecked.HasValue && TrustSslCheckBox.IsChecked.Value;
                Task.Factory.StartNew(async () => // connect to server from another thread
                {
                    // let's see to trust all certificates or not
                    if (trustEveryThing)
                        ServicePointManager.ServerCertificateValidationCallback = (a, b, c, d) => true;
                    else
                        ServicePointManager.ServerCertificateValidationCallback = null;
                    // at first get server's public RSA key
                    try
                    {
                        if (SharedStuff.ServerPublicKey == null)
                            using (var wc = new WebClient())
                            {
                                string key = wc.DownloadString("https://" + serverAddress + "/publicKey");
                                SharedStuff.ServerPublicKey = BouncyCastleHelper.ReadAsymmetricKeyParameter(key);
                            }
                    }
                    catch (Exception ex)
                    {
                        error = new Exception("Can't get public key",ex);
                        // close dialog on main thread (because it is not running from main thread)
                        await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => args.Session.Close()));
                        return;
                    }

                    // now login the user
                    string toSave;
                    try
                    {
                        // at first generate a ecdh curve25519 key pair
                        var curveKeys = SharedStuff.Curve.generateKeyPair();
                        // encrypt the password with server's public key
                        string rsaPassword = Convert.ToBase64String(BouncyCastleHelper.RsaEncrypt(SharedStuff.ServerPublicKey, password));
                        // build the request
                        var postValues = new FormUrlEncodedContent(new[]
                        {
                            new KeyValuePair<string, string>("username", username),
                            new KeyValuePair<string, string>("password", rsaPassword),
                            new KeyValuePair<string, string>("key", Convert.ToBase64String(curveKeys.getPublicKey())),
                        });
                        var result = await SharedStuff.Client.PostAsync("https://" + serverAddress + "/users/registerClient", postValues);
                        string resultContent = await result.Content.ReadAsStringAsync();
                        // parse the server result
                        var jsonRes = JsonConvert.DeserializeObject<JsonTypes.ServerStatus>(resultContent);
                        if (!jsonRes.Ok)
                            throw new Exception("Server returned an error: " + jsonRes.Message);
                        // save the login details
                        toSave = JsonConvert.SerializeObject(new JsonTypes.SavedData
                        {
                            Username = username,
                            Password = password,
                            PublicKey = Convert.ToBase64String(curveKeys.getPublicKey()),
                            PrivateKey = Convert.ToBase64String(curveKeys.getPrivateKey())
                        });
                        Properties.Settings.Default.Name = jsonRes.Message;
                        Properties.Settings.Default.ServerAddress = serverAddress;
                    }
                    catch (Exception ex)
                    {
                        error = new Exception("Can't login you",ex);
                        // close dialog on main thread (because it is not running from main thread)
                        await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => args.Session.Close()));
                        return;
                    }

                    // save the data (we had a successful login)
                    try
                    {
                        byte[] encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(toSave), null, DataProtectionScope.CurrentUser); // encrypt the data
                        Properties.Settings.Default.LoginData = Convert.ToBase64String(encrypted); // save data to application settings
                        Properties.Settings.Default.TrustInvalidSSL = trustEveryThing;
                        Properties.Settings.Default.Save();
                    }
                    catch (Exception ex)
                    {
                        error = new Exception("Cannot save data",ex);
                        // TODO: logout the user if this fails
                        // close dialog on main thread (because it is not running from main thread)
                        await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => args.Session.Close()));
                        return;
                    }

                    // close dialog on main thread (because it is not running from main thread)
                    await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => args.Session.Close()));
                });
            });
            // check for errors
            if (error != null)
            {
                ErrorDialogSample errorDialog = new ErrorDialogSample
                {
                    ErrorTitle = {Text = error.Message}, 
                    ErrorText = {Text = error.InnerException.Message} // Inner exception is never empty
                };
                await DialogHost.Show(errorDialog,"LoginDialogHost");
            }
            else // login the user
            {
                Hide();
                new MainChatsWindow().Show();
            }
        }
    }
}
