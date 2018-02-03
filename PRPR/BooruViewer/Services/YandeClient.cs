﻿using PRPR.BooruViewer.Models.Global;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Web.Http;
using Windows.Web.Http.Filters;
using Windows.Web.Http.Headers;

namespace PRPR.BooruViewer.Services
{
    public class YandeClient
    {
        private static string HashPassword(string password)
        {
            var hashAlgorithmProvider = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Sha1);

            var passwordBuffer = CryptographicBuffer.ConvertStringToBinary($"choujin-steiner--{password}--", BinaryStringEncoding.Utf8);
            var bufferHash = hashAlgorithmProvider.HashData(passwordBuffer);

            return CryptographicBuffer.EncodeToHexString(bufferHash);
        }

        private static async Task<string> GetAuthenticityToken()
        {
            try
            {
                // Log out
                HttpWebRequest logoutRequest = WebRequest.CreateHttp($"https://yande.re/user/logout");
                logoutRequest.CookieContainer = new CookieContainer();
                using (HttpWebResponse logResponse = (HttpWebResponse)(await logoutRequest.GetResponseAsync()))
                {
                    using (Stream stream = logResponse.GetResponseStream())
                    {
                        StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                        string responseString = reader.ReadToEnd();

                        var x = logResponse.Cookies;
                        foreach (Cookie item in x)
                        {
                            Debug.WriteLine(WebUtility.UrlDecode(item.Value));
                        }
                    }
                }
                

                HttpBaseProtocolFilter filter = new HttpBaseProtocolFilter();
                filter.CacheControl.WriteBehavior = HttpCacheWriteBehavior.NoCache;
                filter.CacheControl.ReadBehavior = HttpCacheReadBehavior.MostRecent;
                filter.AllowUI = false;
                var hc = new Windows.Web.Http.HttpClient(filter);
                var str = await hc.GetStringAsync(new Uri($"https://yande.re/user/login"));
                var start = str.IndexOf("<meta name=\"csrf-token\" content=\"") + "<meta name=\"csrf-token\" content=\"".Length;
                var end = str.IndexOf("\" />", start);
                str = str.Substring(start, end - start);
                return WebUtility.UrlEncode(str);
            }
            catch (Exception ex)
            {
                return "";
            }
        }

        private static async Task<VoteType> GetVoteAsync(int postId, string userName, string passwordHash)
        {
            try
            {
                HttpWebRequest loginRequest = WebRequest.CreateHttp($"https://yande.re/post/vote.xml?login={userName}&password_hash={passwordHash}&id={postId}");
                loginRequest.Method = "POST";
                loginRequest.ContentType = "application/x-www-form-urlencoded";
                loginRequest.Headers["Accept-Encoding"] = "gzip, deflate";


                using (HttpWebResponse logResponse = (HttpWebResponse)(await loginRequest.GetResponseAsync()))
                {
                    using (Stream stream = logResponse.GetResponseStream())
                    {
                        StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                        string responseString = reader.ReadToEnd();

                        return responseString.Contains("3") ? VoteType.Favorite : VoteType.None;
                    }
                }
            }
            catch (Exception ex)
            {
                return VoteType.None;
            }
        }

        public static async Task VoteAsync(int postId, string userName, string passwordHash, VoteType score)
        {
            HttpWebRequest loginRequest = WebRequest.CreateHttp($"https://yande.re/post/vote.xml?login={userName}&password_hash={passwordHash}&id={postId}&score={(int)score}");
            loginRequest.Method = "POST";
            loginRequest.ContentType = "application/x-www-form-urlencoded";
            loginRequest.Headers["Accept-Encoding"] = "gzip, deflate";


            using (HttpWebResponse logResponse = (HttpWebResponse)(await loginRequest.GetResponseAsync()))
            {
                using (Stream stream = logResponse.GetResponseStream())
                {
                    StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                    string responseString = reader.ReadToEnd();
                }
            }
        }
        
        private static Rect Normalize(Size imageSize, Rect cropRect)
        {
            return new Rect(
                cropRect.Left / imageSize.Width,
                cropRect.Top / imageSize.Height,
                cropRect.Width / imageSize.Width,
                cropRect.Height / imageSize.Height);
        }

        public static async Task SetAvatarAsync(string postId, Size imageSize, Rect cropRect)
        {
            string token = "";

            var rect = Normalize(imageSize, cropRect);
            var s = $"authenticity_token={token}post_id={postId}&left={rect.Left}&right={rect.Right}&top={rect.Top}&bottom={rect.Bottom}&commit=Set+avatar";

            var uri = $"https://yande.re/user/set_avatar/{postId}";

            // TODO: implement setting avatar
            throw new NotImplementedException();
        }


        public static async Task<bool> SignInAsync(string userName, string password)
        {
            try
            {
                string id = null;
                var token = await GetAuthenticityToken();

                string requestBody = $"authenticity_token={token}&url=&user%5Bname%5D={userName}&user%5Bpassword%5D={password}&commit=Login";
                
                
                var httpClient = new HttpClient(new HttpBaseProtocolFilter());
                var message = new HttpRequestMessage(new HttpMethod("POST"), new Uri($"https://yande.re/user/authenticate"))
                {
                    Content = new HttpStringContent(requestBody)
                };
                message.Content.Headers.ContentType = new HttpMediaTypeHeaderValue("application/x-www-form-urlencoded");
                var response = await httpClient.SendRequestAsync(message);
                var responseString = await response.Content.ReadAsStringAsync();

                
                var start = responseString.IndexOf("/user/show/") + "/user/show/".Length;
                if (start < 11)
                {
                    YandeSettings.Current.UserID = id;
                    YandeSettings.Current.UserName = userName;
                    YandeSettings.Current.PasswordHash = HashPassword(password);
                    return false;
                }
                var end = responseString.IndexOf("\">My Profile<", start);
                id = responseString.Substring(start, end - start);


                
                YandeSettings.Current.UserID = id;
                YandeSettings.Current.UserName = userName;
                YandeSettings.Current.PasswordHash = HashPassword(password);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        
        public static void SignOut()
        {
            YandeSettings.Current.UserID = null;
            YandeSettings.Current.UserName = null;
            YandeSettings.Current.PasswordHash = null;
        }
        
        public static async Task SubmitCommentAsync()
        {
            // TODO: submit the comment to the server
            throw new NotImplementedException();
        }
        
        public static async Task AddFavoriteAsync(int postId)
        {
            await VoteAsync(postId, YandeSettings.Current.UserName, YandeSettings.Current.PasswordHash, VoteType.Favorite);
        }

        public static async Task RemoveFavoriteAsync(int postId)
        {
            await VoteAsync(postId, YandeSettings.Current.UserName, YandeSettings.Current.PasswordHash, VoteType.None);
        }

        public static async Task<bool> CheckFavorited(int postId)
        {
            return (await GetVoteAsync(postId, YandeSettings.Current.UserName, YandeSettings.Current.PasswordHash)) == VoteType.Favorite;
        }        
    }


    public enum VoteType
    {
        
        Bad = -1,
        None = 0,
        Good = 1,
        Great = 2,
        Favorite = 3

    }
}
