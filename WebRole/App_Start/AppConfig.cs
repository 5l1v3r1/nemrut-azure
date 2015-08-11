using System;
using System.Collections.Generic;
using Microsoft.WindowsAzure;

namespace WebRole.App_Start
{
    public static class AppConfig
    {
        private static readonly Dictionary<string, string> Collection = new Dictionary<string, string>();

        public static void Register(string key, string defaultValue = null)
        {
            try
            {
                string value = CloudConfigurationManager.GetSetting(key);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    Collection.Add(key, value);
                }
                else if (!string.IsNullOrWhiteSpace(defaultValue))
                {
                    Collection.Add(key, defaultValue);
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public static string Read(string key)
        {
            string result;
            Collection.TryGetValue(key, out result);
            result = result ?? string.Empty;
            return result;
        }
    }
}