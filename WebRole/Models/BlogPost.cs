using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace WebRole.Models
{
    public class BlogPost
    {
        public string ItemId { get; set; }

        public string ShareId { get; set; }

        public DateTime Created { get; set; }

        public DateTime Updated { get; set; }

        public string FileName { get; set; }

        public string Extension { get; set; }

        public string MimeType { get; set; }

        public string Title { get; set; }

        public string Slug { get; set; }

        public static Tuple<BlogPost, bool> TryCreateBlogPost(object fileName, object created, object updated, object itemId,
            object mimeType, string shareId)
        {
            BlogPost resultPost = null;
            bool succeeded = false;
            
            try
            {
                string fileNameString = fileName.ToString();
                string createdString = created.ToString();
                string updatedString = updated.ToString();
                string uriString = itemId.ToString();
                string mimeTypeString = mimeType.ToString();
                succeeded = !string.IsNullOrWhiteSpace(fileNameString)
                    && !string.IsNullOrWhiteSpace(createdString)
                    && !string.IsNullOrWhiteSpace(updatedString)
                    && !string.IsNullOrWhiteSpace(uriString)
                    && !string.IsNullOrWhiteSpace(mimeTypeString)
                    && !string.IsNullOrWhiteSpace(shareId);
                if (succeeded)
                {
                    resultPost = new BlogPost(fileNameString, createdString, updatedString, uriString, shareId, mimeTypeString);
                }
            }
            catch (Exception e)
            {
                // Ignore
            }

            return Tuple.Create(resultPost, succeeded);
        }

        public BlogPost(string fileName, string created, string updated, string itemId, string shareId, string mimeType)
        {
            FileName = fileName;
            Extension = Path.GetExtension(fileName);
            Title = Path.GetFileNameWithoutExtension(fileName);
            Slug = ToUrlSlug(Title);
            Created = DateTime.Parse(created);
            Updated = DateTime.Parse(updated);
            ItemId = itemId;
            ShareId = shareId;
            MimeType = mimeType;
        }

        /// <summary>
        /// see http://joancaron.com/slug-csharp-url-friendly/
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string ToUrlSlug(string value)
        {
            //First to lower case
            value = value.ToLowerInvariant();

            //Remove all accents
            var bytes = Encoding.GetEncoding("Cyrillic").GetBytes(value);
            value = Encoding.ASCII.GetString(bytes);

            //Replace spaces
            value = Regex.Replace(value, @"\s", "-", RegexOptions.Compiled);

            //Remove invalid chars
            value = Regex.Replace(value, @"[^\w\s\p{Pd}]", "", RegexOptions.Compiled);

            //Trim dashes from end
            value = value.Trim('-', '_');

            //Replace double occurences of - or \_
            value = Regex.Replace(value, @"([-_]){2,}", "$1", RegexOptions.Compiled);

            return value;
        }
    } 
}