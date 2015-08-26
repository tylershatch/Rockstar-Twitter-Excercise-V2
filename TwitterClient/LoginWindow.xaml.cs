using System.Windows;

namespace Rockstar
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        TwitterClient m_twitterClient;

        public LoginWindow(TwitterClient twitterClient)
        {
            InitializeComponent();

            m_twitterClient = twitterClient;

            twitterClient.OpenAuthorizationWebPage();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (m_twitterClient.AuthenticatePIN(pinTextBox.Text))
            {
                this.DialogResult = true;
            }
            else
            {
                this.DialogResult = false;
            }
        }
    }
}
