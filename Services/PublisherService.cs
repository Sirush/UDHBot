using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using MailKit.Net.Smtp;
using MimeKit;
using Newtonsoft.Json;

namespace DiscordBot.Services
{
    public class PublisherService
    {
        private readonly DiscordSocketClient _client;
        private readonly DatabaseService _databaseService;

        private readonly Dictionary<uint, string> _verificationCodes;

        public PublisherService(DiscordSocketClient client, DatabaseService databaseService)
        {
            _client = client;
            _databaseService = databaseService;
            _verificationCodes = new Dictionary<uint, string>();
        }

        public async Task PostAd(uint id)
        {
            (uint, ulong) ad = _databaseService.GetPublisherAd(id);
            await PublisherAdvertising(ad.Item1, ad.Item2);
        }

        private async Task PublisherAdvertising(uint packageId, ulong userid)
        {
            Console.WriteLine("pub1 " + packageId);
            var package = await GetPackage(packageId);
            var packageHead = await GetPackageHead(packageId);
            var packagePrice = await GetPackagePrice(packageId);
            Console.WriteLine("pub2");
            var r = await GetPublisherAdvertisting(userid, package, packageHead, packagePrice);
            Console.WriteLine("pub3");

            var channel = _client.GetChannel(Settings.GetUnityNewsChannel()) as ISocketMessageChannel;
            await channel.SendFileAsync(r.Item2, "image.jpg", r.Item1);
        }

        /*
        DailyObject => https://www.assetstore.unity3d.com/api/en-US/sale/results/10.json
        PackageOBject => https://www.assetstore.unity3d.com/api/en-US/content/overview/[PACKAGEID].json
        PriceObject (€) => https://www.assetstore.unity3d.com/api/en-US/content/price/[PACKAGEID].json
        PackageHead => https://www.assetstore.unity3d.com/api/en-US/head/package/[PACKAGEID].json
        blogfeed (xml) => https://blogs.unity3d.com/feed/  
        */

        public async Task<DailyObject> GetDaily()
        {
            using (var httpClient = new HttpClient())
            {
                var json = await httpClient.GetStringAsync(
                    "https://www.assetstore.unity3d.com/api/en-US/sale/results/10.json");
                return JsonConvert.DeserializeObject<DailyObject>(json);
            }
        }

        private async Task<PackageObject> GetPackage(uint packageId)
        {
            using (var httpClient = new HttpClient())
            {
                var json = await httpClient.GetStringAsync(
                    $"https://www.assetstore.unity3d.com/api/en-US/content/overview/{packageId}.json");
                return JsonConvert.DeserializeObject<PackageObject>(json);
            }
        }

        private async Task<PackageHeadObject> GetPackageHead(uint packageId)
        {
            using (var httpClient = new HttpClient())
            {
                var json = await httpClient.GetStringAsync(
                    $"https://www.assetstore.unity3d.com/api/en-US/head/package/{packageId}.json");
                return JsonConvert.DeserializeObject<PackageHeadObject>(json);
            }
        }

        private async Task<PriceObject> GetPackagePrice(uint packageId)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.Timeout = TimeSpan.FromSeconds(10);
                var json = await httpClient.GetStringAsync(
                    $"https://www.assetstore.unity3d.com/api/en-US/content/price/{packageId}.json");
                return JsonConvert.DeserializeObject<PriceObject>(json);
            }
        }

        private async Task<(string, Stream)> GetPublisherAdvertisting(ulong userid, PackageObject package,
            PackageHeadObject packageHead, PriceObject packagePrice)
        {
            var descStrippedHtml = Regex.Replace(package.content.description, "<.*?>", string.Empty);
            descStrippedHtml = Regex.Replace(descStrippedHtml, "&nbsp;", string.Empty);
            
            var sb = new StringBuilder();
            sb.Append("**--- Publisher everyday Advertising ---**\n\n");
            sb.Append($"Today's daily advertisting goes to {_client.GetUser(userid).Mention} (**{packageHead.result.publisher}**)\n");
            sb.Append($"With their package : {packageHead.result.title}, priced at {packagePrice.price}\n");
            sb.Append("For any inquiry you can contact them here on **Unity Developer Hub** by mentioning them in the chat or PM.\n\n");
            sb.Append("*Rating* ");
            for (var i = 0; i < package.content.rating.average; i++)
                sb.Append("★");
            sb.Append($"(:bust_in_silhouette:{package.content.rating.count})\n");
            sb.Append($"Unity Asset Store Link - https://www.assetstore.unity3d.com/en/#!/content/{package.content.link.id}?utm_source=udh&utm_medium=discord\n");
            sb.Append($"```{descStrippedHtml.Substring(0, 250)}[...]```\n");
            sb.Append("To be part of this kind of advertising use `!pInfo` for more informations.");
            //TODO: add image

            Stream image;
            using (var httpClient = new HttpClient())
            {
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                image = await httpClient.GetStreamAsync($"https:{package.content.keyimage.big}");
                //image = ImageSharp.Image.Load(img);
            }
            return (sb.ToString(), image);
        }

        public async Task<(bool, string)> VerifyPackage(uint packageId)
        {
            Console.WriteLine("enters verify package");
            var package = await GetPackage(packageId);
            if (package.content == null) //Package doesn't exist
                return (false, $"The package id {packageId} doesn't exist.");
            if (package.content.publisher.support_email.Length < 2)
                return (false, "Your package must have a support email defined to be validated.");

            var name = (await GetPackageHead(packageId)).result.publisher;

            Console.WriteLine("before sending verification code");
            
            await SendVerificationCode(name, package.content.publisher.support_email, packageId);
            Console.WriteLine("after sending verification code");
            return (true,
                "An email with a validation code was sent. Please type !verify *packageId* *code* to validate your package.\nThis code will be valid for 30 minutes."
                );
        }

        private async Task SendVerificationCode(string name, string email, uint packageId)
        {
            Console.WriteLine("mail");
            var random = new byte[9];
            var rand = RandomNumberGenerator.Create();
            rand.GetBytes(random);

            var code = Convert.ToBase64String(random);

            _verificationCodes.Add(packageId, code);
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Unity Developer Hub", SettingsHandler.LoadValueString("gmail", JsonFile.Settings)));
            message.To.Add(new MailboxAddress(name, email));
            message.Subject = "Unity Developer Hub Package Validation";
            message.Body = new TextPart("plain")
            {
                Text = @"Here's your validation code : " + code
            };

            using (var client = new SmtpClient())
            {
                await client.ConnectAsync("smtp.gmail.com", 587);

                client.AuthenticationMechanisms.Remove("XOAUTH2");
                await client.AuthenticateAsync("unitydeveloperhub", SettingsHandler.LoadValueString("gmailPassword", JsonFile.Settings));

                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }

            //TODO: Delete code after 30min
        }

        public async Task<string> ValidatePackageWithCode(IUser user, uint packageId, string code)
        {
            if (!_verificationCodes.TryGetValue(packageId, out var c))
                return "An error occured while trying to validate your package. Please verify your packageId is valid";
            if (c != code)
                return "The verification code is not valid. Please verify it and try again.";

            var u = user as SocketGuildUser;
            IRole publisher = u.Guild.GetRole(SettingsHandler.LoadValueUlong("publisherRoleID", JsonFile.Settings));
            await u.AddRoleAsync(publisher);
            _databaseService.AddPublisherPackage(user.Username, user.DiscriminatorValue.ToString(), user.Id.ToString(), packageId);

            return "Your package has been verified and added to the daily advertisement list.";
        }
    }
}