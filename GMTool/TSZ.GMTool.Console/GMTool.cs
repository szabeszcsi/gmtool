using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace TSZ.GMTool.CommandLine
{
    class GMTool
    {

        static string[] Scopes = { GmailService.Scope.MailGoogleCom };
        static string ApplicationName = "GMTool";

        static void Main(string[] args)
        {
            string searchFor = string.Empty;
            string beforeDate = string.Empty;
            int batchSize = 100;
            bool promptMsg = false;

            if (args.Length == 0)
                Console.WriteLine("Usage: [size] [from] [before] [prompt] e.g.: 100 facebook 2014/12/31 true");

            // call example: 100 facebook 2011/12/31 
            if (args.Length > 0 && args[0] != null)
            {
                Int32.TryParse(args[0], out int newbatchSize);
                batchSize = newbatchSize > 0 ? newbatchSize : batchSize;
            }

            if(args.Length > 0 && (args[1] != null && !string.IsNullOrEmpty(args[1])))
            {
                searchFor = args[1];
            }

            if (args.Length > 0 && args[2] != null && !string.IsNullOrEmpty(args[2]) && DateTime.TryParse(args[2], out DateTime selectedDate))
            {
                beforeDate = args[2];
            }

            if(args.Length > 0 && args[3]!= null)
                Boolean.TryParse(args[3], out promptMsg);


            UserCredential credential;
            using (var stream = new FileStream("client_secret.json", FileMode.Open, FileAccess.Read))
            {
                string credPath = System.Environment.GetFolderPath(
                    System.Environment.SpecialFolder.Personal);
                credPath = Path.Combine(credPath, ".credentials/GMTool.json");

                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                Console.WriteLine("Credential file saved to: " + credPath);
            }
           

            string query = string.Empty;

            if (!string.IsNullOrEmpty(searchFor))
                query += $"from:{searchFor}";

            if (!string.IsNullOrEmpty(beforeDate))
                query += $" before:{beforeDate}";

            Console.WriteLine($"The query is: {query}");
            Console.WriteLine("Proceed? (Y/N)");
            var readedKey = Console.ReadKey();

            if (readedKey.KeyChar.ToString().ToLower() == "n")
                return;


            // Create Gmail API service.
            var service = new GmailService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });


            IList<Message> messages;
            bool doWork = true;
            int iterator = 0;
            int batchnum = 0;

            while (doWork)
            {
                batchnum++;

                UsersResource.MessagesResource.ListRequest request = service.Users.Messages.List("me");
                request.Q = query; // "from:facebook before:2011/12/31";
#if DEBUG
                request.Q = "from:facebook before:2013/12/31";
#endif
                request.MaxResults = batchSize;

                messages = request.Execute().Messages;
                if (messages != null && messages.Count > 0 && iterator <= batchSize)
                {
                    Console.WriteLine($"Found: {messages.Count} messages in batch num #{batchnum}.");

                    if (promptMsg)
                    {
                        foreach (var msg in messages)
                        {
                            iterator++;

                            var getRequest = service.Users.Messages.Get("me", msg.Id);
                            var sentMsg = getRequest.Execute();
                            Console.WriteLine($"{iterator}/{messages.Count} - Msg: {sentMsg.Snippet}");

                            //UsersResource.MessagesResource.TrashRequest trashRequest = service.Users.Messages.Trash("me", msg.Id);
                            //var result = trashRequest.Execute();
                        }
                    }
                    else
                        iterator += messages.Count;
                   

                    Console.WriteLine($"DELETE ALL {messages.Count} MESSAGES IN CURRENT BATCH? (Y/N) -- !!! WARNING! YOU CANNOT UNDO THIS! !!!");
                    readedKey = Console.ReadKey();

                    if (readedKey.KeyChar.ToString().ToLower() == "n")
                        return;


                    var batchDeleteReq = new BatchDeleteMessagesRequest
                    {
                        Ids = messages.Select(e => e.Id).ToList()
                    };
                    var req = service.Users.Messages.BatchDelete(batchDeleteReq, "me");
                    var batchDeleteResult = req.Execute();

                    Console.WriteLine($"Deleted {messages.Count} messages in batch num# {batchnum}");
                }
                else
                {
                    doWork = false;
                }
            }

            Console.WriteLine($"TOTAL Deleted {iterator} messages.");
            Console.ReadLine();

        }
    }
}
