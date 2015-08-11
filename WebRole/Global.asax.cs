using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using MongoDB.Bson;
using MongoDB.Driver;
using WebRole.App_Start;

namespace WebRole
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            RegisterConfigurations();

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
