using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Cstieg.JsonHelper;

namespace Cstieg.ShoppingCart.PayPal.Rest
{
    /// <summary>
    /// Client for PayPal API
    /// </summary>
    public class PayPalRestApiClient : PayPalApiClient
    {
        private static string payPalBaseURL = "https://api.sandbox.paypal.com/v1/";
        private HttpClient httpClient = new HttpClient();

        public AccessToken AccessToken { get; set; }
        public UserAccessToken UserAccessToken { get; set; }

        /// <summary>
        /// Constructor for PayPalApiClient which loads urls from paypal.json
        /// </summary>
        public PayPalRestApiClient() : base ()
        {
            // set up http client
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en_US"));
        }
        
        /// <summary>
        /// Gets user info from PayPal API after user login
        /// </summary>
        /// <param name="authorizationCode">Authorization code returned from user login</param>
        /// <returns>UserInfo object containing information about the user</returns>
        public async Task<UserInfo> GetUserInfo(string authorizationCode)
        {
            UserAccessToken accessToken = (UserAccessToken) await GetAccessToken<UserAccessToken>(authorizationCode);
            string jsonData = await PayPalCall("identity/openidconnect/userinfo?schema=openid", "", accessToken.AccessTokenString, "GET");
            return JsonConvert.DeserializeObject<UserInfo>(jsonData);
        }

        /// <summary>
        /// Posts an order to PayPal API given a shopping cart
        /// </summary>
        /// <param name="shoppingCart">Shopping cart object containing order</param>
        /// <returns>JSON data containing order id</returns>
        public async Task<string> PostOrder(ShoppingCart shoppingCart)
        {
            AccessToken accessToken = (AccessToken)await GetAccessToken<AccessToken>();
            string orderData = CreateOrder(shoppingCart);
            return await PayPalCall("payments/payment", orderData, accessToken.AccessTokenString);
        }

        /// <summary>
        /// Executes a payment created by PostOrder
        /// </summary>
        /// <param name="paymentId">The payment ID passed by user approval dialog on front end</param>
        /// <param name="payerId">The payer ID passed by user approval dialog on front end</param>
        public async Task ExecutePayment(string paymentId, string payerId)
        {
            AccessToken accessToken = (AccessToken)await GetAccessToken<AccessToken>();
            string postData = JsonConvert.SerializeObject(new
            {
                payer_id = payerId
            });
            var result = await PayPalCall("payments/payment/" + paymentId + "/execute", postData, accessToken.AccessTokenString);
            var orderId = new JsonDeserializer(result).GetString("id");
        }

        /// <summary>
        /// Gets an access token to use in making PayPal API calls.
        /// There are two types of access tokens:
        ///     1.  An AccessToken derived from the client id and secret, used in Payments API
        ///     2.  A UserAccessToken derived from authorization code created upon user login, used in Identity API
        /// The access token is cached, and refreshed as necessary.
        /// </summary>
        /// <typeparam name="T">Either AccessToken or UserAccessToken</typeparam>
        /// <param name="userCode">Authorization code obtained from user login which will be exchanged for UserAccessToken.  
        /// Leave empty if obtaining AccessToken.</param>
        /// <returns>AccessToken or UserAccessToken to be used in API calls</returns>
        private async Task<AccessTokenBase> GetAccessToken<T>(string userCode = "") where T : AccessTokenBase
        {
            PropertyInfo cachedTokenProperty;
            AccessTokenBase cachedToken;
            string url;
            FormUrlEncodedContent content;

            switch (typeof(T).Name)
            {
                case "AccessToken":
                    cachedTokenProperty = typeof(PayPalApiClient).GetProperty("AccessToken");
                    url = payPalBaseURL + "oauth2/token";
                    content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("grant_type", "client_credentials")
                    });
                    break;
                case "UserAccessToken":
                    if (userCode == "")
                    {
                        throw new ArgumentException("Must pass authorization code");
                    }
                    cachedTokenProperty = typeof(PayPalApiClient).GetProperty("UserAccessToken");
                    url = payPalBaseURL + "identity/openidconnect/tokenservice";
                    content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("grant_type", "authorization_code"),
                        new KeyValuePair<string, string>("code", userCode)
                    });
                    break;
                default:
                    throw new ArgumentException("Type T must derive from AccessTokenBase");
            }

            // Store access token to reuse until expires
            cachedToken = (T)cachedTokenProperty.GetValue(this);
            if (cachedToken != null && !cachedToken.IsExpired)
            {
                return cachedToken;
            }

            // Get new access token
            // Set up request parameters
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            httpClient.DefaultRequestHeaders.Authorization = GetAuthenticationHeader();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };

            // Make API call and get results
            var response = await httpClient.SendAsync(request);
            var result = response.Content.ReadAsStringAsync().Result;
            cachedToken = JsonConvert.DeserializeObject<T>(result);

            // store cached token as property
            cachedTokenProperty.SetValue(this, cachedToken);

            return cachedToken;
        }

        /// <summary>
        /// Gets a header used to authenticate PayPal API calls with client id and secret
        /// </summary>
        /// <returns>The AuthenticationHeaderValue object containing the authentication data</returns>
        private AuthenticationHeaderValue GetAuthenticationHeader()
        {
            var byteArray = Encoding.ASCII.GetBytes(ClientInfo.ClientId + ":" + ClientInfo.ClientSecret);
            return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        }

        /// <summary>
        /// Make API call to PayPal with specified data.  
        /// Generic wrapper generalizing elements needed by all API calls, such as getuserinfo.
        /// </summary>
        /// <param name="url">API endpoint URL being called</param>
        /// <param name="data">Data to pass to endpoint</param>
        /// <param name="accessToken">Access token authorizing access</param>
        /// <param name="method">GET or POST</param>
        /// <returns>String result of call</returns>
        private async Task<string> PayPalCall(string url, string data, string accessToken, string method = "POST")
        {
            // Check parameters
            if (!"POST|GET".Contains(method))
            {
                throw new ArgumentException("Argument 'method' must either be 'GET' or 'POST'");
            }
            if (method == "GET" && data != "")
            {
                throw new ArgumentException("A GET request cannot contain data!");
            }
            HttpMethod httpMethod = method == "GET" ? HttpMethod.Get : HttpMethod.Post;

            string fullUrl = payPalBaseURL + url;

            // Set up request parameters
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            HttpRequestMessage request = new HttpRequestMessage(httpMethod, fullUrl);
            if (method == "POST")
            {
                request.Content = new StringContent(data, Encoding.UTF8, "application/json");
            }

            // Make API call and get results
            var response = await httpClient.SendAsync(request);
            string result = response.Content.ReadAsStringAsync().Result;
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(result.ToString());
            }
            return result;
        }
    }
}
