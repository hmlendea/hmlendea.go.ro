using System.Collections.Generic;

namespace PersonalPresentationWebsite.Configuration
{
    public sealed class FundingSettings
    {
        public static string SectionName => "funding";

        public string PatreonUsername { get; set; } = string.Empty;

        public string RevolutUsername { get; set; } = string.Empty;

        public string PayPalHostedButtonId { get; set; } = string.Empty;

        public string GitHubSponsorsUsername { get; set; } = string.Empty;

        public string LiberaPayUsername { get; set; } = string.Empty;

        public string BuyMeACoffeeUsername { get; set; } = string.Empty;

        public string KofiUsername { get; set; } = string.Empty;

        public BankAccountSettings SaltBank { get; set; } = new();

        public IEnumerable<FundingChannelSettings> Channels
            => BuildChannels();

        private IEnumerable<FundingChannelSettings> BuildChannels()
            => [.. LinkedChannels(), .. BankChannels()];

        private IEnumerable<FundingChannelSettings> LinkedChannels()
            => [
                .. LinkedChannel(
                    FundingChannelNames.Patreon,
                    "patreon",
                    PatreonUsername,
                    $"https://patreon.com/{PatreonUsername}"),
                .. LinkedChannel(
                    FundingChannelNames.Revolut,
                    "revolut",
                    RevolutUsername,
                    $"https://revolut.me/{RevolutUsername}"),
                .. LinkedChannel(
                    FundingChannelNames.PayPal,
                    "paypal",
                    PayPalHostedButtonId,
                    $"https://paypal.com/donate?hosted_button_id={PayPalHostedButtonId}"),
                .. LinkedChannel(
                    FundingChannelNames.GitHubSponsors,
                    "github",
                    GitHubSponsorsUsername,
                    $"https://github.com/sponsors/{GitHubSponsorsUsername}"),
                .. LinkedChannel(
                    FundingChannelNames.LiberaPay,
                    "liberapay",
                    LiberaPayUsername,
                    $"https://liberapay.com/{LiberaPayUsername}/donate"),
                .. LinkedChannel(
                    FundingChannelNames.BuyMeACoffee,
                    "buymeacoffee",
                    BuyMeACoffeeUsername,
                    $"https://buymeacoffee.com/{BuyMeACoffeeUsername}"),
                .. LinkedChannel(
                    FundingChannelNames.KoFi,
                    "kofi",
                    KofiUsername,
                    $"https://ko-fi.com/{KofiUsername}")
            ];

        private IEnumerable<FundingChannelSettings> BankChannels()
            => BankChannel(FundingChannelNames.SaltBank, "saltbank", SaltBank);

        private static IEnumerable<FundingChannelSettings> BankChannel(
            string name,
            string slug,
            BankAccountSettings account)
        {
            if (string.IsNullOrWhiteSpace(account.Iban))
            {
                return [];
            }

            return
            [
                new FundingChannelSettings
                {
                    Name = name,
                    Image = ImagePathFor(slug),
                    BankTransfer = new BankTransferSettings
                    {
                        ModalId = $"modal-{slug}",
                        Iban = account.Iban,
                        Swift = account.Swift
                    }
                }
            ];
        }

        private static IEnumerable<FundingChannelSettings> LinkedChannel(
            string name,
            string slug,
            string identifier,
            string url)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return [];
            }

            return
            [
                new FundingChannelSettings
                {
                    Name = name,
                    Image = ImagePathFor(slug),
                    Url = url
                }
            ];
        }

        private static string ImagePathFor(string slug)
            => $"../assets/img/funding/{slug}.png";
    }
}
