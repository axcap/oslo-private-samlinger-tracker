using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

using System.IO;
using Azure.Storage.Blobs.Specialized;
using HtmlAgilityPack;
using System.Net.Http;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using System.Text;
using Newtonsoft.Json;

namespace Oslo_private_samlinger_tracker
{
    public static class Function
    {
        [FunctionName("Function")]
        public static async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            string siteUrl = "https://www.oslo.kommune.no/koronavirus/rad-og-regler-i-oslo/private-samlinger";
            var pageContent = new System.Net.WebClient().DownloadString(siteUrl);
            var pageDoc = new HtmlDocument();
            pageDoc.LoadHtml(pageContent);
            var current_html = pageDoc.DocumentNode.InnerText;

            string connectionString = Environment.GetEnvironmentVariable("STORAGE_CONNECTION_STRING");
            string containerName = "htmls";
            string blobName = "index.html";
            BlockBlobClient blockBlob = new BlockBlobClient(connectionString, containerName, blobName);
            var previous_html_content = ReadToEnd(blockBlob.Download().Value.Content);

            if (previous_html_content != current_html)
            {
                log.LogInformation("Update detected");
                var diff = GenerateDiff(previous_html_content, current_html);
                log.LogInformation(diff);

                using (var stream = GenerateStreamFromString(current_html))
                {
                    blockBlob.Upload(stream);
                }

                var post_url = Environment.GetEnvironmentVariable("SEND_EMAIL_URL");
                var client = new HttpClient();
                string payload = JsonConvert.SerializeObject(new
                {
                    diff
                });

                var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
                var response = await client.PostAsync(post_url, content);
                var responseString = await response.Content.ReadAsStringAsync();
                log.LogInformation($"Response string: {responseString}");
            }
            else
            {
                log.LogInformation("No updates detected");
            }
        }

        public static string GenerateDiff(string before, string after)
        {
            var diff = InlineDiffBuilder.Diff(before, after);
            var result = new System.Text.StringBuilder();

            foreach (var line in diff.Lines)
            {
                switch (line.Type)
                {
                    case ChangeType.Inserted:
                        result.AppendLine($"+ {line.Text}");
                        break;
                    case ChangeType.Deleted:
                        result.AppendLine($"- {line.Text}");
                        break;
                    default:
                        break;
                }
            }
            return result.ToString();
        }
        public static string ReadToEnd(System.IO.Stream stream)
        {
            long originalPosition = 0;

            if (stream.CanSeek)
            {
                originalPosition = stream.Position;
                stream.Position = 0;
            }

            try
            {
                byte[] readBuffer = new byte[4096];

                int totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = stream.Read(readBuffer, totalBytesRead, readBuffer.Length - totalBytesRead)) > 0)
                {
                    totalBytesRead += bytesRead;

                    if (totalBytesRead == readBuffer.Length)
                    {
                        int nextByte = stream.ReadByte();
                        if (nextByte != -1)
                        {
                            byte[] temp = new byte[readBuffer.Length * 2];
                            Buffer.BlockCopy(readBuffer, 0, temp, 0, readBuffer.Length);
                            Buffer.SetByte(temp, totalBytesRead, (byte)nextByte);
                            readBuffer = temp;
                            totalBytesRead++;
                        }
                    }
                }

                byte[] buffer = readBuffer;
                if (readBuffer.Length != totalBytesRead)
                {
                    buffer = new byte[totalBytesRead];
                    Buffer.BlockCopy(readBuffer, 0, buffer, 0, totalBytesRead);
                }
                var buffer_as_str = System.Text.Encoding.Default.GetString(buffer);
                return buffer_as_str;
            }
            finally
            {
                if (stream.CanSeek)
                {
                    stream.Position = originalPosition;
                }
            }
        }

        public static Stream GenerateStreamFromString(string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }
    }
}
