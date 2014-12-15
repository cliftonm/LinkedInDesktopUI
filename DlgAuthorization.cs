using System;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Forms;

namespace LinkedInDesktopUI
{
	// The code in this method extracted from the LinkedIn.NET's LNTest DlgAuthorization.cs.

    // Windows form with WebBrowser control - wbAuth - for performing authorization on LinkedIn
    public partial class DlgAuthorization : Form
    {
        public DlgAuthorization(string authLink)
        {
            InitializeComponent();
            // parse authorization link
            var qs = parseResponse(authLink);
            // store state for further use
            if (qs["state"] != null)
            {
                _State = qs["state"];
            }
            // store redirect URL for further use
            if (qs["redirect_uri"] != null)
            {
                _RedirectUri = new Uri(qs["redirect_uri"]);
            }
            // navigate to authorization link
            wbAuth.Navigate(new Uri(authLink));
        }

        private readonly string _State;
        private readonly Uri _RedirectUri;

        // property for handling authorization code
        public string AuthorizationCode { get; private set; }
        // properties for handling possible errors
        public string OauthError { get; private set; }
        public string OauthErrorDescription { get; private set; }

        private void DlgAuthorization_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel;
        }

        private NameValueCollection parseResponse(string response)
        {
            var nvc = new NameValueCollection();
            if (response.StartsWith("?")) response = response.Substring(1);
            var arr1 = response.Split('&');
            foreach (var arr2 in arr1.Select(s => s.Split('=')).Where(arr2 => arr2.Length == 2))
            {
                nvc.Add(arr2[0].Trim(), arr2[1].Trim());
            }
            return nvc;
        }

        private void wbAuth_Navigated(object sender, WebBrowserNavigatedEventArgs e)
        {
            // resize broser control in order to to force the windows to show vertical scrollbar
            // LinkedIn authorization always fill the whole browser, so this is the only way to show all fields and buttons in window of suitable size without stretching it to screen height
            wbAuth.Height = ClientSize.Height * 2;
            // chack whether we are in needed end point
            if (e.Url.Scheme != _RedirectUri.Scheme || e.Url.Host != _RedirectUri.Host ||
                e.Url.AbsolutePath != _RedirectUri.AbsolutePath) return;
            var queryParams = e.Url.Query;
            if (queryParams.Length <= 1) return;
            // parse query
            var qs = parseResponse(queryParams);
            // check state parameter
            if (qs["state"] == null) DialogResult = DialogResult.Cancel;
            if (qs["state"] != _State) DialogResult = DialogResult.Cancel;
            // check code parameter
            if (qs["code"] != null)
            {
                // store code parameter and close the window
                AuthorizationCode = qs["code"];
                DialogResult = DialogResult.OK;
            }
            // check for possible errors
            else if (qs["error"] != null)
            {
                OauthError = qs["error"];
                if (qs["error_description"] != null)
                {
                    OauthErrorDescription = qs["error_description"];
                }
                DialogResult = DialogResult.Cancel;
            }
        }
    }
}
