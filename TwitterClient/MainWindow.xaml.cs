using System.Windows;

namespace Rockstar
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public TwitterClient twitterClient;

        public MainWindow()
        {
            InitializeComponent();

            twitterClient = new TwitterClient();
            tweetList.ItemsSource = twitterClient.tweets;

            this.DataContext = twitterClient;
        }

        private void Button_GetNewerTweets(object sender, RoutedEventArgs e)
        {
            twitterClient.GetNewerTweets();
        }

        private void Button_GetOlderTweets(object sender, RoutedEventArgs e)
        {
            twitterClient.GetOlderTweets();
        }

        private void Button_PostTweets(object sender, RoutedEventArgs e)
        {
            twitterClient.PostTweet(tweetTextBox.Text);
        }

        private void MenuItem_Login(object sender, RoutedEventArgs e)
        {
            twitterClient.Login();
        }

        private void MenuItem_Logout(object sender, RoutedEventArgs e)
        {
            twitterClient.Logout();
        }
    }
}
