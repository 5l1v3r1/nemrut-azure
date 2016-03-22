using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using MongoDB.Bson;
using MongoDB.Driver;
using WebRole.App_Start;
using Pour.Core.Library;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace WebRole
{
    public class MvcApplication : HttpApplication
    {
        public static ILogger Logger;

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            RegisterConfigurations();

            // Connect to Pour
            if (string.IsNullOrEmpty(AppConfig.Read("Nemrut.App.PourToken")))
            {
                Logger = LogManager.Connect();
            }
            else
            {
                Logger = LogManager.Connect(AppConfig.Read("Nemrut.App.PourToken"));
            }

            // Set Azure role name and id as context to attach each log message
            LogManager.SetContext("RoleName", RoleEnvironment.CurrentRoleInstance.Role.Name);
            LogManager.SetContext("RoleId", RoleEnvironment.CurrentRoleInstance.Id);

            // Log the initialization
            Logger.Info("Starting the service");

            ConnectToDb();
        }

        private void RegisterConfigurations()
        {
            AppConfig.Register("Nemrut.Live.LoginUrIFormat");
            AppConfig.Register("Nemrut.Live.GrantType");
            AppConfig.Register("Nemrut.Live.AuthUrl");
            AppConfig.Register("Nemrut.OneDrive.ClientId");
            AppConfig.Register("Nemrut.OneDrive.ClientSecret");
            AppConfig.Register("Nemrut.OneDrive.Scope");
            AppConfig.Register("Nemrut.OneDrive.RedirectUri");
            AppConfig.Register("Nemrut.OneDrive.AuthUriFormat");
            AppConfig.Register("Nemrut.App.LoginUriFormat", 
                "https://login.live.com/oauth20_authorize.srf?client_id={0}&scope={1}&response_type=code&redirect_uri={2}");
            AppConfig.Register("Nemrut.App.Name");
            AppConfig.Register("Nemrut.App.PourToken");
            AppConfig.Register("Nemrut.Mongo.ConnectionString");
            AppConfig.Register("Nemrut.Mongo.Name");
            AppConfig.Register("Nemrut.App.LoginUri", 
                string.Format(AppConfig.Read("Nemrut.App.LoginUriFormat"),
                    AppConfig.Read("Nemrut.OneDrive.ClientId"),
                    AppConfig.Read("Nemrut.OneDrive.Scope"),
                    AppConfig.Read("Nemrut.OneDrive.RedirectUri")));
            AppConfig.Register("Nemrut.App.SyncUriFormat", 
                string.Format("{0}{1}", AppConfig.Read("Nemrut.OneDrive.AuthUriFormat"),
                "drive/root:/nemrut:/children?access_token={0}"));
        }

        private void ConnectToDb()
        {
            MongoClient mongoClient = new MongoClient(AppConfig.Read("Nemrut.Mongo.ConnectionString"));
            IMongoDatabase db = mongoClient.GetDatabase("crowdy");
            Application.Add("Collection", db.GetCollection<BsonDocument>("nemrut-azure"));
        }
    }
}
