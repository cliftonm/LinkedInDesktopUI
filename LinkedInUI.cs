using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using LinkedIn.NET;
using LinkedIn.NET.Options;
using LinkedIn.NET.Groups;

namespace LinkedInDesktopUI
{
	public static class ExtensionMethods
	{
		public static string LimitLength(this string s, int len)
		{
			string ret = s;

			if (s.Length > len)
			{
				ret = s.Substring(0, len - 3) + "...";
			}

			return ret;
		}
	}

	public partial class LinkedInUI : Form
	{
		protected LinkedInClient linkedInClient;
		protected string consumerKey;
		protected string consumerSecret;
		protected string accessToken;
		protected bool haveAccessToken;
		protected LinkedInGroupPost selectedPost;
		protected TreeNode selectedPostNode;
		const string CRLF = "\r\n";
		const string REDIRECT_URL = "http://pnotes.sourceforge.net/auth.htm";
		const string STATE = "DCEEFWF45453sdffef424";

		public LinkedInUI()
		{
			InitializeComponent();
			LoadConfiguration();
			InitializeClient();
			btnComment.Enabled = false;

			if (String.IsNullOrEmpty(accessToken))
			{
				Authenticate();
			}
			else
			{
				LoadGroups();
			}
		}

		protected void MakeComment(object sender, EventArgs args)
		{
			AddComment(selectedPost, tbComment.Text);
		}

		protected void OnNodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
		{
			StringBuilder sb = new StringBuilder();

			if (e.Node.Tag is LinkedInGroup)
			{
				LinkedInGroup group = (LinkedInGroup)e.Node.Tag;
				sb.Append("Group: " + group.Name);
				sb.Append(CRLF);
				sb.Append(CRLF);
				sb.Append("Category: " + group.Category);
				sb.Append(CRLF);
				sb.Append(CRLF);
				sb.Append("Description: " + group.ShortDescription);
				btnComment.Enabled = false;
			}
			else if (e.Node.Tag is LinkedInGroupPost)
			{
				LinkedInGroupPost post = (LinkedInGroupPost)e.Node.Tag;
				sb.Append("Title: " + post.Title);
				sb.Append(CRLF);
				sb.Append(CRLF);
				sb.Append("By: " + post.Creator.FirstName + " " + post.Creator.LastName);
				sb.Append(CRLF);
				sb.Append(CRLF);
				sb.Append("On: " + post.CreationTime.ToString());
				sb.Append(CRLF);
				sb.Append(CRLF);
				sb.Append("Summary: " + post.Summary);
				btnComment.Enabled = true;
				selectedPost = post;
				selectedPostNode = e.Node;
			}
			else if (e.Node.Tag is LinkedInGroupComment)
			{
				LinkedInGroupComment comment = (LinkedInGroupComment)e.Node.Tag;
				sb.Append("By: " + comment.Creator.FirstName + " " + comment.Creator.LastName);
				sb.Append(CRLF);
				sb.Append(CRLF);
				sb.Append("On: " + comment.CreationTime.ToString());
				sb.Append(CRLF);
				sb.Append(CRLF);
				sb.Append(comment.Text);
				btnComment.Enabled = true;
			}

			tbInfo.Text = sb.ToString();
		}

		protected void OnNodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
		{
			if (e.Node.Tag is LinkedInGroup)
			{
				LinkedInGroup group = (LinkedInGroup)e.Node.Tag;
				LoadPostsForGroup(e.Node, group);
			}
			else if (e.Node.Tag is LinkedInGroupPost)
			{
				LinkedInGroupPost post = (LinkedInGroupPost)e.Node.Tag;
				LoadCommentsForPost(e.Node, post);
			}
		}

		/// <summary>
		/// Load the configuration and check for the access token.
		/// </summary>
		protected void LoadConfiguration()
		{
			try
			{
				string[] linkedInConfig = File.ReadAllLines("linkedin.config");
				consumerKey = linkedInConfig[0];
				consumerSecret = linkedInConfig[1];

				if (linkedInConfig.Length == 3)
				{
					accessToken = linkedInConfig[2];
					haveAccessToken = true;
				}
			}
			catch (Exception ex)
			{
				EmitException("LinkedIn.config file is missing or corrupt." + "\r\n" + ex.Message);
			}
		}

		protected void SaveConfiguration()
		{
			File.WriteAllLines("linkedin.config", new string[]
			{
				consumerKey,
				consumerSecret,
				accessToken,
			});
		}

		protected void InitializeClient()
		{
			if ((!String.IsNullOrEmpty(consumerKey)) && (!String.IsNullOrEmpty(consumerSecret)))
			{
				linkedInClient = new LinkedInClient(consumerKey, consumerSecret);

				if (haveAccessToken)
				{
					linkedInClient.AccessToken = accessToken;
				}
			}
		}

		// The code in this method is heavily borrowed from the LinkedIn.NET's LNTest "authenticate" method (DlgExample.cs)
		protected void Authenticate()
		{
			if (linkedInClient != null)
			{
				var options = new LinkedInAuthorizationOptions
				{
					RedirectUrl = REDIRECT_URL,
					Permissions = LinkedInPermissions.Connections | LinkedInPermissions.ContactsInfo |
								  LinkedInPermissions.EmailAddress | LinkedInPermissions.FullProfile |
								  LinkedInPermissions.GroupDiscussions | LinkedInPermissions.Messages |
								  LinkedInPermissions.Updates,
					State = STATE
				};

				//create new instance of authorization dialog using authorization link built by _Client
				var dlgAuth = new DlgAuthorization(linkedInClient.GetAuthorizationUrl(options));

				if (dlgAuth.ShowDialog(this) == DialogResult.OK)
				{
					//get access token using authorization code received
					var response = linkedInClient.GetAccessToken(dlgAuth.AuthorizationCode, REDIRECT_URL);

					if (response.Result != null && response.Status == LinkedInResponseStatus.OK)
					{
						accessToken = response.Result.AccessToken;
						SaveConfiguration();
					}
				}
				else
				{
					//show error information
					MessageBox.Show(dlgAuth.OauthErrorDescription, dlgAuth.OauthError);
				}
			}
		}

		protected async void LoadGroups()
		{
			if (haveAccessToken)
			{
				tvGroups.Nodes.Clear();
				tvGroups.Nodes.Add("Loading...");
				LinkedInGetGroupOptions options = new LinkedInGetGroupOptions();
				options.GroupOptions.SelectAll();
				
				LinkedInResponse<IEnumerable<LinkedInGroup>> result = await Task.Run(() => linkedInClient.GetMemberGroups(options));

				if (result.Result != null && result.Status == LinkedInResponseStatus.OK)
				{
					ShowMemberGroups(result);
				}
				else
				{
					ReRun(result.Status, result.Message);
				}
			}
		}

		protected async void LoadPostsForGroup(TreeNode node, LinkedInGroup group)
		{
			LinkedInGetGroupPostsOptions options = new LinkedInGetGroupPostsOptions();
			options.PostOptions.SelectAll();
			options.GroupId = group.Id;
			ShowLoading(node);

			await Task.Run(() => group.LoadPosts(options));

			ShowGroupPosts(node, group);
			node.ExpandAll();
		}

		protected async void LoadCommentsForPost(TreeNode node, LinkedInGroupPost post)
		{
			LinkedInGetGroupPostCommentsOptions options = new LinkedInGetGroupPostCommentsOptions();
			options.CommentOptions.SelectAll();
			options.PostId = post.Id;
			ShowLoading(node);

			await Task.Run(() => post.LoadComments(options));

			ShowGroupPostComments(node, post);
			node.ExpandAll();
		}

		protected void ShowMemberGroups(LinkedInResponse<IEnumerable<LinkedInGroup>> result)
		{
			tvGroups.Nodes.Clear();

			foreach (LinkedInGroup group in result.Result)
			{
				TreeNode node = tvGroups.Nodes.Add(group.Name);
				node.Tag = group;
			}
		}

		protected void ShowGroupPosts(TreeNode node, LinkedInGroup group)
		{
			node.Nodes.Clear();

			foreach (LinkedInGroupPost post in group.Posts)
			{
				TreeNode childNode = node.Nodes.Add(post.Title);
				childNode.Tag = post;
			}
		}

		protected void ShowGroupPostComments(TreeNode node, LinkedInGroupPost post)
		{
			node.Nodes.Clear();

			foreach (LinkedInGroupComment comment in post.Comments)
			{
				TreeNode childNode = node.Nodes.Add(comment.Text.LimitLength(64));
				childNode.Tag = comment;
			}
		}

		protected void AddComment(LinkedInGroupPost post, string comment)
		{
			post.Comment(comment);

			// After posting the comment, add it to the post's comment collection, select it, and display the comment in the info box.
			tbComment.Text = "(posted)";
			TreeNode childNode = selectedPostNode.Nodes.Add(comment.LimitLength(64));
			tvGroups.SelectedNode = childNode;
			tbInfo.Text = comment;
		}

		protected void ShowLoading(TreeNode node)
		{
			node.Nodes.Clear();
			node.Nodes.Add("Loading...");
			node.ExpandAll();
		}

		protected void ReRun(LinkedInResponseStatus status, string message)
		{
			switch (status)
			{
				case LinkedInResponseStatus.ExpiredToken:
				case LinkedInResponseStatus.InvalidAccessToken:
				case LinkedInResponseStatus.UnauthorizedAction:
					Authenticate();
					break;

				default:
					MessageBox.Show(message);
					break;
			}
		}

		protected void EmitException(string exception)
		{
			MessageBox.Show(exception, "An Error Has Occurred", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}
}
