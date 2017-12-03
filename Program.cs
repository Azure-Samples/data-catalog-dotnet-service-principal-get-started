using System;
using System.Diagnostics;
using System.Text;
using Microsoft.IdentityModel.Clients.ActiveDirectory; // Install-Package Microsoft.IdentityModel.Clients.ActiveDirectory
using System.Net;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ConsoleApplication
{
    class Program
    {
        //TenantId and clientIDFromAzureAppRegistrationcan be seen from Azure AAD portal, 
        //see https://docs.microsoft.com/en-us/azure/azure-resource-manager/resource-group-create-service-principal-portal
        //on how to create secret key for an client application.
        //To authorize an application to perform as a catalog user, glossary admin, or catalog admin,
        //please add the service principal user in the format of {ClientAppId}@{TenantId} to the according list.
        private static string tenantId = "{TenantId}";
        private static string clientIDFromAzureAppRegistration = "{ClientAppId}";        
        private static string spsecret = "{SecretKeyForClientApp}";

        //Note: This example uses the "DefaultCatalog" keyword to update the user's default catalog.  You may alternately
        //specify the actual catalog name.
        private static string catalogName = "DefaultCatalog";

        private static string authorityUri = string.Format("https://login.windows.net/{0}", tenantId);
        private static string upn = clientIDFromAzureAppRegistration + "@" + tenantId;
        private static string registerUri = string.Format("https://api.azuredatacatalog.com/catalogs/{0}/views/tables?api-version=2016-03-30", catalogName);
        static AuthenticationResult authResult = null;
        static readonly Stopwatch watch = new Stopwatch();

        static void Main(string[] args)
        {
            for (int numtags = 1; numtags < 11; numtags += 100)
            {
                const int times = 10;
                Console.WriteLine("Num of tags: {0}", numtags);
                for (int i = 0; i < times; i++)
                {
                    string asset = SampleJson("OrdersSample" + Guid.NewGuid(), upn, numtags);
                    Console.Write("REG: ");
                    var id = RegisterDataAsset(asset);
                    Thread.Sleep(1000);
                    Console.Write("GET: ");
                    GetDataAsset(id);
                    Thread.Sleep(1000);
                    Console.Write("DEL: ");
                    DeleteDataAsset(id);
                    Thread.Sleep(1000);
                    Console.WriteLine();
                }
            }
            Console.ReadLine();
        }

        static async Task<AuthenticationResult> AccessToken()
        {
            if (authResult == null)
            {
                //Resource Uri for Data Catalog API
                string resourceUri = "https://api.azuredatacatalog.com";

                //To learn how to register a client app and get a Client ID, see https://msdn.microsoft.com/en-us/library/azure/mt403303.aspx#clientID   
                string clientId = clientIDFromAzureAppRegistration;

                //A redirect uri gives AAD more details about the specific application that it will authenticate.
                //Since a client app does not have an external service to redirect to, this Uri is the standard placeholder for a client app.
                string redirectUri = "https://login.live.com/oauth20_desktop.srf";

                //Create an instance of AuthenticationContext to acquire an Azure access token
                //OAuth2 authority Uri
                AuthenticationContext authContext = new AuthenticationContext(authorityUri);

                //Call AcquireToken to get an Azure token from Azure Active Directory token issuance endpoint
                //AcquireToken takes a Client Id that Azure AD creates when you register your client app.
                authResult = await authContext.AcquireTokenAsync(resourceUri, new ClientCredential(clientId, spsecret));
            }

            return authResult;
        }

        //Register data asset:
        // The Register Data Asset operation registers a new data asset 
        // or updates an existing one if an asset with the same identity already exists. 
        static string RegisterDataAsset(string json)
        {
            string dataAssetHeader = string.Empty;
            string fullUri = registerUri;
            //Create a POST WebRequest as a Json content type
            HttpWebRequest request = System.Net.WebRequest.Create(fullUri) as System.Net.HttpWebRequest;
            request.KeepAlive = true;
            request.Method = "POST";

            try
            {
                var response = SetRequestAndGetResponse(request, json);

                //Get the Response header which contains the data asset ID
                //The format is: tables/{data asset ID} 
                dataAssetHeader = response.Headers["Location"];
            }
            catch (WebException ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.Status);
                if (ex.Response != null)
                {
                    // can use ex.Response.Status, .StatusDescription
                    if (ex.Response.ContentLength != 0)
                    {
                        using (var stream = ex.Response.GetResponseStream())
                        {
                            using (var reader = new StreamReader(stream))
                            {
                                Console.WriteLine(reader.ReadToEnd());
                            }
                        }
                    }
                }
                return null;
            }
            return dataAssetHeader;
        }

        //Get data asset:
        // The Get Data Asset operation retrieves data asset by Id
        static JObject GetDataAsset(string assetUrl)
        {
            string fullUri = string.Format("{0}?api-version=2016-03-30", assetUrl);

            //Create a GET WebRequest as a Json content type
            HttpWebRequest request = WebRequest.Create(fullUri) as HttpWebRequest;
            request.KeepAlive = true;
            request.Method = "GET";
            request.Accept = "application/json;adc.metadata=full";

            try
            {
                var response = SetRequestAndGetResponse(request);
                //using (var reader = new StreamReader(response.GetResponseStream()))
                //{
                //    var itemPayload = reader.ReadToEnd();
                //    Console.WriteLine(itemPayload);
                //    return JObject.Parse(itemPayload);
                //}
            }
            catch (WebException ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.Status);
                if (ex.Response != null)
                {
                    // can use ex.Response.Status, .StatusDescription
                    if (ex.Response.ContentLength != 0)
                    {
                        using (var stream = ex.Response.GetResponseStream())
                        {
                            using (var reader = new StreamReader(stream))
                            {
                                Console.WriteLine(reader.ReadToEnd());
                            }
                        }
                    }
                }
            }

            return null;
        }
        
        //Delete data asset:
        // The Delete Data Asset operation deletes a data asset and all annotations (if any) attached to it. 
        static string DeleteDataAsset(string dataAssetUrl, string etag = null)
        {
            string responseStatusCode = string.Empty;

            //NOTE: To find the Catalog Name, sign into Azure Data Catalog, and choose User. You will see a list of Catalog names.          
            string fullUri = string.Format("{0}?api-version=2016-03-30", dataAssetUrl);

            //Create a DELETE WebRequest as a Json content type
            HttpWebRequest request = System.Net.WebRequest.Create(fullUri) as System.Net.HttpWebRequest;
            request.KeepAlive = true;
            request.Method = "DELETE";

            if (etag != null)
            {
                request.Headers.Add("If-Match", string.Format(@"W/""{0}""", etag));
            }

            try
            {
                //Get HttpWebResponse from GET request
                HttpWebResponse response = SetRequestAndGetResponse(request);
                responseStatusCode = response.StatusCode.ToString();
            }
            catch (WebException ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.Status);
                if (ex.Response != null)
                {
                    // can use ex.Response.Status, .StatusDescription
                    if (ex.Response.ContentLength != 0)
                    {
                        using (var stream = ex.Response.GetResponseStream())
                        {
                            using (var reader = new StreamReader(stream))
                            {
                                Console.WriteLine(reader.ReadToEnd());
                            }
                        }
                    }
                }
                return null;
            }

            return responseStatusCode;
        }

        static HttpWebResponse SetRequestAndGetResponse(HttpWebRequest request, string payload = null)
        {
            while (true)
            {
                //To authorize the operation call, you need an access token which is part of the Authorization header
                request.Headers.Add("Authorization", AccessToken().Result.CreateAuthorizationHeader());
                //Set to false to be able to intercept redirects
                request.AllowAutoRedirect = false;

                if (!string.IsNullOrEmpty(payload))
                {
                    byte[] byteArray = Encoding.UTF8.GetBytes(payload);
                    request.ContentLength = byteArray.Length;
                    request.ContentType = "application/json";
                    //Write JSON byte[] into a Stream
                    request.GetRequestStream().Write(byteArray, 0, byteArray.Length);
                }
                else
                {
                    request.ContentLength = 0;
                }

                watch.Restart();
                HttpWebResponse response = request.GetResponse() as HttpWebResponse;
                response.Close();
                watch.Stop();
                Console.Write(watch.ElapsedMilliseconds + "\t");

                // Requests to **Azure Data Catalog (ADC)** may return an HTTP 302 response to indicate
                // redirection to a different endpoint. In response to a 302, the caller must re-issue
                // the request to the URL specified by the Location response header. 
                if (response.StatusCode == HttpStatusCode.Redirect)
                {
                    string redirectedUrl = response.Headers["Location"];
                    HttpWebRequest nextRequest = WebRequest.Create(redirectedUrl) as HttpWebRequest;
                    nextRequest.Method = request.Method;
                    request = nextRequest;
                }
                else
                {
                    return response;
                }
            }
        }
        
        static string getTags(int num)
        {
            if (num == 0)
                return "";

            string tagformat = @"""tags"": [{0}]";

            string tag = @"
            {{
                ""properties"": {{
                    ""tag"": ""tag{0}"",
                    ""key"": ""tag{0}"",
                    ""fromSourceSystem"": false
                }}
            }}";

            string tags = "";
            int i = 0;
            for (; i < num - 1; i++)
            {
                tags += string.Format(tag, i) + ",";
            }
            tags += string.Format(tag, i);

            return string.Format(tagformat, tags);
        }

        static string SampleJson(string name, string upn, int num = 0)
        {
            return string.Format(@"
{{
    ""roles"": [
        {{
          ""role"": ""Owner"",
          ""members"": [
            {{
              ""objectId"": ""9475255d-a72e-41f3-8ef3-fd6fa83916fb""
            }}
          ]
        }}
      ],
    ""properties"" : {{
        ""fromSourceSystem"" : false,
        ""name"": ""{0}"",
        ""dataSource"": {{
            ""sourceType"": ""SQL Server"",
            ""objectType"": ""Table"",
        }},
        ""dsl"": {{
            ""protocol"": ""tds"",
            ""authentication"": ""windows"",
            ""address"": {{
                ""server"": ""test.contoso.com"",
                ""database"": ""Northwind"",
                ""schema"": ""dbo"",
                ""object"": ""{0}""
            }}
        }},
        ""lastRegisteredBy"": {{
            ""upn"": ""{1}"",
            ""firstName"": ""User1FirstName"",
            ""lastName"": ""User1LastName""
        }},
    }},
    ""annotations"" : {{
        ""schema"": {{
            ""properties"" : {{
                ""fromSourceSystem"" : false,
                ""columns"": [
                    {{
                        ""name"": ""OrderID"",
                        ""isNullable"": false,
                        ""type"": ""int"",
                        ""maxLength"": 4,
                        ""precision"": 10
                    }},
                    {{
                        ""name"": ""CustomerID"",
                        ""isNullable"": true,
                        ""type"": ""nchar"",
                        ""maxLength"": 10,
                        ""precision"": 0
                    }},
                    {{
                        ""name"": ""OrderDate"",
                        ""isNullable"": true,
                        ""type"": ""datetime"",
                        ""maxLength"": 8,
                        ""precision"": 23
                    }},
                ]
            }}
        }},
        {2}
    }}
}}
", name, upn, getTags(num));
        }
    }
}
