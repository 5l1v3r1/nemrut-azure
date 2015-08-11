using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using MarkdownSharp;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Newtonsoft.Json;
using WebRole.App_Start;
using WebRole.Models;

namespace WebRole.Controllers
{
    public class HomeController : Controller
    {
        private User UserInSession { get { return Session["user"] as User; } set { Session.Add("user", value); } }

        private static readonly WebClient Client = new WebClient();

        private static readonly Markdown MdSharp = new Markdown();

        private static readonly IMongoCollection<BsonDocument> Collection =
            System.Web.HttpContext.Current.Application["Collection"] as IMongoCollection<BsonDocument>;

        public async Task<ActionResult> Index()
        {
            if (!IsUserLoggedIn())
            {
                ViewData["LoginUri"] = AppConfig.Read("Nemrut.App.LoginUri");
                return View();
            }

            var userIsInSession = await DoesUserHaveSession();
            return userIsInSession ?
                RedirectToRoute(new { Controller = "Home", Action = "Home" }) :
                RedirectToRoute(new { Controller = "Home", Action = "GetStarted" });
        }

        public async Task<RedirectToRouteResult> Login(string code)
        {
            string errorMessage, userId, accessToken;
            bool authenticated = Authenticate(code, out errorMessage, out userId, out accessToken);
            if (authenticated)
            {
                Session.Add("user_id", userId);
                Session.Add("access_token", accessToken);
            }

            var userIsInSession = authenticated && await DoesUserHaveSession(userId);
            return userIsInSession ? 
                RedirectToRoute(new { Controller = "Home", Action = "Home" }) :
                RedirectToRoute(new { Controller = "Home", Action = "GetStarted" });
        }

        public ActionResult Logout()
        {
            Session.RemoveAll();
            return RedirectToRoute(new { Controller = "Home", Action = "Index" });
        }

        public async Task<ActionResult> Home()
        {
            if (IsUserLoggedIn())
            {
                var userIsInSession = await DoesUserHaveSession();
                if (userIsInSession)
                {
                    ViewData["Title"] = UserInSession.Name;
                    ViewData["Subtitle"] = UserInSession.Username;
                    ViewData["Username"] = UserInSession.Username;

                    var posts = BsonSerializer.Deserialize<IList<BlogPost>>(UserInSession.PostsBson.ToJson());
                    ViewData["Posts"] = posts;
                    ViewData["HasPost"] = posts.Any();
                    return View();
                }
            }

            return RedirectToRoute(new { Controller = "Home", Action = "Index" });
        }

        public async Task<ActionResult> User(string username)
        {
            var user = await GetUser(new BsonDocument { { "username", username } });
            if (user != null)
            {
                IList<BlogPost> posts = BsonSerializer.Deserialize<IList<BlogPost>>(user.PostsBson.ToJson());
                ViewData["Title"] = user.Name;
                ViewData["Subtitle"] = user.Username;
                ViewData["Posts"] = posts;
                ViewData["HasPost"] = posts.Any();
            }
            else
            {
                ViewData["Title"] = "404";
                ViewData["Subtitle"] = "Error";
                ViewData["Error"] = "User couldn't be found";                
            }

            return View();
        }

        public async Task<RedirectToRouteResult> Sync()
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToRoute(new { Controller = "Home", Action = "Index" });
            }

            await SyncOneDrive();
            return RedirectToRoute(new { Controller = "Home", Action = "Home" });
        }

        public async Task<ActionResult> GetStarted()
        {
            if (IsUserLoggedIn())
            {
                var userRegistered = await DoesUserHaveSession();
                if (!userRegistered)
                {
                    return View();
                }
            }
            
            return RedirectToRoute(new { Controller = "Home", Action = "Index" });
        }

        [HttpPost]
        public async Task<ViewResult> GetStarted(string username, string name, string agreement)
        {
            try
            {
                ViewData["Username"] = username;
                ViewData["Name"] = name;

                if (!ValidateInput(username, 4, 16))
                {
                    throw new Exception("Given username is not valid.");
                }

                if (!ValidateInput(name, 5))
                {
                    throw new Exception("Given name is not valid.");
                }

                if (!ValidateInput(agreement, 1, 10, "on"))
                {
                    throw new Exception("Please accept the agreement.");
                }

                bool alreadyRegistered = await IsUsernameRegistered(username);
                if (alreadyRegistered)
                {
                    throw new Exception("This username is already registered.");
                }

                bool registered = await AddUser(username, name);
                if (!registered)
                {
                    throw new Exception("Registration failed. Please try again.");   
                }

                string errorMessage;
                bool createdAppFolder = CreateAppFolder(out errorMessage);
                if (!createdAppFolder)
                {
                    ViewData["Warning"] = "Failed to create the app folder. " + errorMessage;
                }

                return View("Home");
            }
            catch (Exception e)
            {
                ViewData["Error"] = e.Message;
                return View("GetStarted");
            }
        }

        public async Task<ViewResult> Post(string username, string slug)
        {
            var user = await GetUser(new BsonDocument {{"username", username}});
            if (user != null)
            {
                IList<BlogPost> posts = BsonSerializer.Deserialize<IList<BlogPost>>(user.PostsBson.ToJson());

                if (string.IsNullOrWhiteSpace(slug))
                {
                    ViewData["Title"] = user.Name;
                    ViewData["Subtitle"] = user.Username;    
                }
                else
                {
                    BlogPost post = posts.Single(postFromDb => slug.Equals(postFromDb.Slug));
                    if (post != null)
                    {
                        ViewData["Title"] = post.Title;
                        ViewData["Subtitle"] = user.Username;

                        try
                        {
                            string uri = string.Format("{0}{1}", 
                                AppConfig.Read("Nemrut.OneDrive.AuthUriFormat"), "shares/" + post.ShareId + "/items/"
                                + post.ItemId + "/content");
                            
                            if (post.Extension.Equals(".md"))
                            {
                                ViewData["Content"] = MdSharp.Transform(Client.DownloadString(uri));
                            }
                            else if (post.Extension.Equals(".docx"))
                            {
                                //Stream stream = new MemoryStream(Client.DownloadData(uri));
                                //WordprocessingDocument doc = WordprocessingDocument.Open(stream, false);
                                //XElement html = HtmlConverter.ConvertToHtml(doc, new HtmlConverterSettings
                                //{
                                //    PageTitle = "Test"
                                //});
                                //ViewData["Content"] = html.ToStringNewLineOnAttributes();
                                ViewData["Content"] = "Not yet supported!";
                            }
                            else
                            {
                                ViewData["Content"] =
                                    Client.DownloadString(uri).Replace(Environment.NewLine, "<br />");
                            }
                        }
                        catch (Exception e)
                        {
                            ViewData["Error"] = "Something went wrong. Error: " + e.Message;
                        }
                        return View();
                    }
                }
            }
            
            ViewData["Title"] = "404";
            ViewData["Subtitle"] = "Error";
            ViewData["Error"] = "User couldn't be found";
            return View();
        }

        #region Private Helpers

        private string GetAuthUri(string code)
        {
            return string.Format("{0}?{1}={2}&{3}={4}&{5}={6}&{7}={8}&{9}={10}",
                AppConfig.Read("Nemrut.Live.AuthUrl"),
                "client_id", AppConfig.Read("Nemrut.OneDrive.ClientId"),
                "client_secret", AppConfig.Read("Nemrut.OneDrive.ClientSecret"),
                "redirect_uri", AppConfig.Read("Nemrut.OneDrive.RedirectUri"),
                "grant_type", AppConfig.Read("Nemrut.Live.GrantType"),
                "code", code);
        }

        private bool Authenticate(string code, out string errorMessage, out string userId, out string accessToken)
        {
            userId = string.Empty;
            accessToken = string.Empty;

            var response = MakeWebRequest<Dictionary<string, string>>(GetAuthUri(code), out errorMessage);
            if (response != null)
            {
                foreach (KeyValuePair<string, string> entry in response)
                {
                    if (!string.IsNullOrWhiteSpace(entry.Key) && !string.IsNullOrWhiteSpace(entry.Value))
                    {
                        if (entry.Key.Equals("user_id"))
                        {
                            userId = entry.Value;
                        }

                        if (entry.Key.Equals("access_token"))
                        {
                            accessToken = entry.Value;
                        }
                    }
                }
            }

            return !string.IsNullOrWhiteSpace(userId) && !string.IsNullOrWhiteSpace(accessToken);
        }

        private async Task SyncOneDrive()
        {
            string syncUri = string.Format(AppConfig.Read("Nemrut.App.SyncUriFormat"), Session["access_token"]);
            string errorMessage;
            var response = MakeWebRequest<Dictionary<string, object>>(syncUri, out errorMessage);
            await UpdateUser(ProcessFiles(response));
        }

        private IList<BlogPost> ProcessFiles(Dictionary<string, object> response)
        {
            if (response == null || !response.ContainsKey("value"))
            {
                throw new InvalidOperationException("Couldn't fetch files from OneDrive.");
            }

            var filesFromResponse = JsonConvert.DeserializeObject<IEnumerable<object>>(response["value"].ToString());
            if (filesFromResponse == null)
            {
                throw new InvalidOperationException("Couldn't fetch files from OneDrive.");
            }

            IList<BlogPost> posts = new List<BlogPost>();
            foreach (object fileResponseObject in filesFromResponse)
            {
                IDictionary<string, object> fileResponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(fileResponseObject.ToString());
                if (fileResponse != null
                    && fileResponse.ContainsKey("id")
                    && fileResponse.ContainsKey("createdDateTime")
                    && fileResponse.ContainsKey("lastModifiedDateTime")
                    && fileResponse.ContainsKey("name")
                    && fileResponse.ContainsKey("file"))
                {
                    IDictionary<string, object> fileInfoResponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(fileResponse["file"].ToString());
                    if (fileInfoResponse != null
                        && fileInfoResponse.ContainsKey("mimeType"))
                    {
                        string errorMessage;
                        string shareId = CreateFileLink(fileResponse["id"].ToString(), out errorMessage);
                        if (!string.IsNullOrWhiteSpace(shareId))
                        {
                            Tuple<BlogPost, bool> result = BlogPost.TryCreateBlogPost(fileResponse["name"],
                                fileResponse["createdDateTime"],
                                fileResponse["lastModifiedDateTime"],
                                fileResponse["id"],                                
                                fileInfoResponse["mimeType"],
                                shareId);
                            if (result.Item2)
                            {
                                posts.Add(result.Item1);
                            }
                        }
                    }
                }
            }

            return posts;
        }

        private T MakeWebRequest<T>(string uri, out string errorMessage)
        {
            try
            {
                errorMessage = string.Empty;
                WebRequest tokenRequest = WebRequest.Create(uri);
                WebResponse tokenResponse = tokenRequest.GetResponse();
                Stream responseStream = tokenResponse.GetResponseStream();
                if (responseStream != null)
                {
                    StreamReader reader = new StreamReader(responseStream);
                    return JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
                }

                throw new Exception("The response is null.");
            }
            catch (Exception e)
            {
                errorMessage = string.Format("The request operation is failed. Error: {0}", e.Message);
                return default(T);
            }
        }

        private bool IsUserLoggedIn()
        {
            return Session != null && Session["user_id"] != null && Session["access_token"] != null;
        }

        private async Task<bool> DoesUserHaveSession(string userId = null)
        {
            if (UserInSession == null)
            {
                if (string.IsNullOrWhiteSpace(userId))
                {
                    userId = Session["user_id"].ToString();
                }
                UserInSession = await GetUser(new BsonDocument { { "_id", userId } });
            }

            return UserInSession != null;
        }

        private async Task<User> GetUser(BsonDocument filter)
        {
            try
            {
                var result = await Collection.Find(filter).ToListAsync();
                var userDoc = result.FirstOrDefault();
                if (userDoc != null)
                {
                    User user = new User
                    {
                        Id = userDoc.GetValue("_id", "Id couldn't be found!").AsString,
                        Name = userDoc.GetValue("name", "Name couldn't be found!").AsString,
                        Username = userDoc.GetValue("username", "Username couldn't be found!").AsString,
                        PostsBson = userDoc.GetValue("posts", new BsonArray())
                    };
                    return user;
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        private async Task<bool> IsUsernameRegistered(string username)
        {
            var result = await Collection.Find(new BsonDocument { { "username", username } }).ToListAsync();
            return result.Any();
        }

        private async Task UpdateUser(IList<BlogPost> posts)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("username", UserInSession.Username);
            BsonArray postsAsBson = new BsonArray();
            foreach (BlogPost post in posts)
            {
                postsAsBson.Add(post.ToBsonDocument());
            }
            var update = Builders<BsonDocument>.Update.Set("posts", postsAsBson);
            await Collection.UpdateOneAsync(filter, update);
            UserInSession.PostsBson = postsAsBson;
        }

        private async Task<bool> AddUser(string username, string name)
        {
            string userId = Session["user_id"].ToString();
            BsonDocument newUser = new BsonDocument{ {"_id", userId }, { "name", name }, { "username", username } };
            await Collection.InsertOneAsync(newUser);
            Session["user"] = new User
            {
                Id = userId,
                Name = name,
                Username = username
            };
            return true;
        }

        private const string CreateFolderRequestBody = "{'name': 'nemrut', 'folder': {} }";

        private bool CreateAppFolder(out string errorMessage)
        {
            try
            {
                errorMessage = string.Empty;
                WebRequest request = WebRequest.Create(string.Format("{0}{1}", AppConfig.Read("Nemrut.OneDrive.AuthUriFormat"), "drive/root/children"));
                request.Method = "POST";
                request.ContentType = "application/json";
                using (StreamWriter writer = new StreamWriter(request.GetRequestStream()))
                {
                    writer.Write(CreateFolderRequestBody);
                    writer.Flush();
                    writer.Close();
                }
                request.Headers.Add(HttpRequestHeader.Authorization,
                    string.Format("Bearer {0}", Session["access_token"]));
                request.GetResponse();
            }
            catch (Exception e)
            {
                errorMessage = e.Message;
                return false;
            }
            return true;
        }

        private const string CreateFileLinkRequestBody = "{'type': 'view' }";

        private string CreateFileLink(string id, out string errorMessage)
        {
            try
            {
                errorMessage = string.Empty;
                WebRequest request = WebRequest.Create(
                    string.Format("{0}{1}", AppConfig.Read("Nemrut.OneDrive.AuthUriFormat"), "drive/items/" + id + "/action.createLink"));
                request.Method = "POST";
                request.ContentType = "application/json";
                using (StreamWriter writer = new StreamWriter(request.GetRequestStream()))
                {
                    writer.Write(CreateFileLinkRequestBody);
                    writer.Flush();
                    writer.Close();
                }
                request.Headers.Add(HttpRequestHeader.Authorization,
                    string.Format("Bearer {0}", Session["access_token"]));
                WebResponse response = request.GetResponse();
                Stream responseStream = response.GetResponseStream();
                if (responseStream != null)
                {
                    StreamReader reader = new StreamReader(responseStream);
                    string content = reader.ReadToEnd();
                    Dictionary<string, object> responseJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(content);
                    if (responseJson.ContainsKey("shareId"))
                    {
                        return responseJson["shareId"].ToString();
                    }
                }
            }
            catch (Exception e)
            {
                errorMessage = e.Message;
            }

            return string.Empty;
        }

        private bool ValidateInput(string value, int min = 0, int max = 255, string equality = null)
        {
            bool isValid = value != null && 
                (equality != null && value.Equals(equality, StringComparison.InvariantCultureIgnoreCase)
                || (value.Length > min && value.Length <= max));
            return isValid;
        }

        #endregion
    }
}