using System;
using System.Windows;
using System.Diagnostics;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace Rockstar
{
    public class TwitterClient : INotifyPropertyChanged
    {
        // This event facilitates databinding between this twitter client and the xaml window that displays its properties
        public event PropertyChangedEventHandler PropertyChanged;

        // This is a simple class representing a twitter status update ("tweet"), but for simplicity 
        // it only contains the fields that this simple twitter client cares about
        public class AbridgedTweet
        {
            // Construct an abridged tweet from the json token representing that tweet
            public AbridgedTweet(JToken jsonTweetToken)
            {
                CreatedDate = jsonTweetToken["created_at"].ToString();
                ScreenName = jsonTweetToken["user"]["screen_name"].ToString();
                Text = jsonTweetToken["text"].ToString();
            }

            public string CreatedDate { get; set; }
            public string ScreenName { get; set; }
            public string Text { get; set; }
        }
        
        // This is a collection of all tweets we have received from twitter, ordered by CreatedDate
        public ObservableCollection<AbridgedTweet> tweets;

        // These indicate the oldest and the newest tweets we have received from twittered, 
        // and are used to get even older and even newer tweets
        public long newestTweetID;
        public long oldestTweetID;

        // This is the interface we use for sending http requests and receiving http responses
        public HttpClient httpClient;

        // This is the interface we use for authorizing our application with twitter 
        // and building authorization headers for http requests to twitter
        public OAuth.Manager oauthManager;

        private bool loggedIn;

        // These properties are written so the xaml for the main window can databind to them. I chose not to spend 
        // time designing a cleaner approach to writing these properties, and there are definetly cleaner approaches. 
        // I could spend a long time just studying how C# databinding works in order to improve these few lines of code.

        public Visibility VisibleIfLoggedIn { get { return LoggedIn ? Visibility.Visible : Visibility.Collapsed; } }
        public Visibility CollapsedIfLoggedIn { get { return LoggedIn ? Visibility.Collapsed : Visibility.Visible; } }
        public bool NotLoggedIn { get { return !LoggedIn; } }

        public bool LoggedIn
        {
            get { return loggedIn; }

            set
            {
                loggedIn = value;

                if (PropertyChanged != null)
                {
                    // I am aware that this is super gross, see above comment
                    PropertyChanged(this, new PropertyChangedEventArgs("LoggedIn"));
                    PropertyChanged(this, new PropertyChangedEventArgs("NotLoggedIn"));
                    PropertyChanged(this, new PropertyChangedEventArgs("VisibleIfLoggedIn"));
                    PropertyChanged(this, new PropertyChangedEventArgs("CollapsedIfLoggedIn"));
                }
            }
        }

        private string status;
        public string Status
        {
            get { return status; }
            set
            {
                status = value;

                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("Status"));
                }
            }
        }

        public TwitterClient()
        {
            tweets = new ObservableCollection<AbridgedTweet>();
            httpClient = new HttpClient();
            oauthManager = new OAuth.Manager();

            LoggedIn = false;
            Status = "OK";

            // The consumer key and secret are specific to this app and were obtained by registration with Twitter.com
            oauthManager["consumer_key"] = "tUitLCoAeRPXTQ9QujmxMjvI2";
            oauthManager["consumer_secret"] = "lLvZNB4ihc5ZJJCWOB3X33ySMHwn5WJ0f0NmvH9gkNIkTTaD8W";

            // If we have previously authorized this app and still have our access token, use it
            if (System.IO.File.Exists("access_token.txt") && System.IO.File.Exists("access_token_secret.txt"))
            {
                oauthManager["token"] = System.IO.File.ReadAllText("access_token.txt");
                oauthManager["token_secret"] = System.IO.File.ReadAllText("access_token_secret.txt");

                if (VerifyCredentials())
                {
                    LoggedIn = true;

                    GetInitialTweets();
                }
            }
        }

        public void Login()
        {
            Debug.Assert(!LoggedIn);

            // OAuthManager can be put into a bad state if we get a request token but bail before getting
            // an access token, then try to get a second new request token. These two lines put us back
            // in the correct state.
            oauthManager["token"] = "";
            oauthManager["token_secret"] = "";

            // Open the login dialog
            var loginWindow = new LoginWindow(this);

            // If the login dialog is succesful, log us in
            if (loginWindow.ShowDialog() == true)
            {
                Status = "OK";
                LoggedIn = true;

                // Store token and secret so we don't need to log in the next time we run this app
                System.IO.File.WriteAllText("access_token.txt", oauthManager["token"]);
                System.IO.File.WriteAllText("access_token_secret.txt", oauthManager["token_secret"]);

                GetInitialTweets();
            }
        }

        public void Logout()
        {
            Debug.Assert(LoggedIn);

            Status = "OK";
            LoggedIn = false;

            System.IO.File.Delete("access_token.txt");
            System.IO.File.Delete("access_token_secret.txt");

            tweets.Clear();
        }

        public void OpenAuthorizationWebPage()
        {
            // Acquire a request token and store it in oauth["token"]
            oauthManager.AcquireRequestToken("https://api.twitter.com/oauth/request_token", "POST");

            // Open the Twitter authorization page in a web broswer, using the request token we just acquired
            Process.Start("https://api.twitter.com/oauth/authorize?oauth_token=" + oauthManager["token"]);
        }

        public bool AuthenticatePIN(string pin)
        {
            bool authenticated = false;

            // Acquire an access token using the pin
            try
            {
                oauthManager.AcquireAccessToken("https://api.twitter.com/oauth/access_token", "POST", pin);

                authenticated = VerifyCredentials();
            }
            catch (System.Net.WebException)
            {
                // AcquireAccessToken throws an exception if the pin is invalid, so this block of code just
                // assumes that this was the problem. Obviously this could be improved.
                Status = "Invalid pin, please try again";
            }
            
            // Verify that the access token we received works
            return authenticated;
        }

        public bool VerifyCredentials()
        {
            var resourceUrl = "https://api.twitter.com/1.1/account/verify_credentials.json";

            // Create a request message for the tweets resourceUrl
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, resourceUrl);

            // Build the OAuth authorization header and add it to the message
            httpRequest.Headers.Add("Authorization", oauthManager.GenerateAuthzHeader(resourceUrl, "GET"));

            var httpResponse = httpClient.SendAsync(httpRequest).Result;

            return httpResponse.IsSuccessStatusCode;
        }

        public JArray GetTweets(string resourceUrl)
        {
            // Create a request message for the tweets resourceUrl
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, resourceUrl);

            // Build the OAuth authorization header and add it to the message
            httpRequest.Headers.Add("Authorization", oauthManager.GenerateAuthzHeader(resourceUrl, "GET"));

            // Send the request (.Result makes this call synchronous, though I am very uncertain if it is
            // the best approach for doing that)
            var httpResponse = httpClient.SendAsync(httpRequest).Result;
            
            if (!httpResponse.IsSuccessStatusCode)
            {
                Status = "Twitter.com failure - " + httpResponse.ReasonPhrase;

                return null;
            }
            else
            {
                Status = "OK";

                string httpResponseContentString = httpResponse.Content.ReadAsStringAsync().Result;

                return JArray.Parse(httpResponseContentString);
            }
        }

        public void PostTweet(string tweetText)
        {
            var resourceUrl = "https://api.twitter.com/1.1/statuses/update.json?status=" + Uri.EscapeDataString(tweetText);

            // Create a request message for the resourceUrl we built for posting this tweet
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, resourceUrl);

            // Build the OAuth authorization header and add it to the message
            httpRequest.Headers.Add("Authorization", oauthManager.GenerateAuthzHeader(resourceUrl, "POST"));

            // Send the request (.Result makes this call synchronous, though I am very uncertain if it is
            // the right approach for doing that)
            var httpResponse = httpClient.SendAsync(httpRequest).Result;

            if (!httpResponse.IsSuccessStatusCode)
            {
                Status = "Twitter.com failure - " + httpResponse.ReasonPhrase;
            }
            else
            {
                Status = "OK";

                GetNewerTweets();
            }
        }

        public void GetInitialTweets()
        {
            JArray jsonTweetsArray = GetTweets("https://api.twitter.com/1.1/statuses/home_timeline.json");

            if (jsonTweetsArray != null)
            {
                foreach (var token in jsonTweetsArray)
                {
                    tweets.Add(new AbridgedTweet(token));
                }

                newestTweetID = jsonTweetsArray.First["id"].ToObject<long>();
                oldestTweetID = jsonTweetsArray.Last["id"].ToObject<long>();
            }
        }

        public void GetNewerTweets()
        {
            JArray jsonTweetsArray = GetTweets("https://api.twitter.com/1.1/statuses/home_timeline.json" + "?since_id=" + newestTweetID);

            if (jsonTweetsArray != null)
            { 
                foreach (var token in jsonTweetsArray)
                {
                    tweets.Insert(0, new AbridgedTweet(token));
                }

                newestTweetID = jsonTweetsArray.First["id"].ToObject<long>();
            }
        }

        public void GetOlderTweets()
        {
            JArray jsonTweetsArray = GetTweets("https://api.twitter.com/1.1/statuses/home_timeline.json" + "?max_id=" + oldestTweetID);

            if (jsonTweetsArray != null)
            {
                foreach (var token in jsonTweetsArray)
                {
                    tweets.Add(new AbridgedTweet(token));
                }

                oldestTweetID = jsonTweetsArray.Last["id"].ToObject<long>();
            }
        }
    }
}
