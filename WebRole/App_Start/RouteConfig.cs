﻿using System.Web.Mvc;
using System.Web.Routing;

namespace WebRole
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                name: "RouteWithoutControllerName",
                url: "{action}/{username}/{slug}",
                defaults: new { controller = "Home", action = "Index", username = UrlParameter.Optional, slug = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
