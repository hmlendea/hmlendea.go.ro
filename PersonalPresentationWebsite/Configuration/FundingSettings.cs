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
        {
            List<FundingChannelSettings> channels = [];

            AddLinkedChannels(channels);
            AddBankChannels(channels);

            return channels;
        }

        private void AddLinkedChannels(List<FundingChannelSettings> channels)
        {
            AddLinkedChannel(
                channels,
                "Patreon",
                "patreon",
                PatreonUsername,
                $"https://patreon.com/{PatreonUsername}"
            );
            AddLinkedChannel(
                channels,
                "Revolut",
                "revolut",
                RevolutUsername,
                $"https://revolut.me/{RevolutUsername}"
            );
            AddLinkedChannel(
                channels,
                "PayPal",
                "paypal",
                PayPalHostedButtonId,
                $"https://www.paypal.com/donate?hosted_button_id={PayPalHostedButtonId}"
            );
            AddLinkedChannel(
                channels,
                "GitHub Sponsors",
                "github",
                GitHubSponsorsUsername,
                $"https://github.com/sponsors/{GitHubSponsorsUsername}"
            );
            AddLinkedChannel(
                channels,
                "LiberaPay",
                "liberapay",
                LiberaPayUsername,
                $"https://liberapay.com/{LiberaPayUsername}/donate"
            );
            AddLinkedChannel(
                channels,
                "Buy Me a Coffee",
                "buymeacoffee",
                BuyMeACoffeeUsername,
                $"https://www.buymeacoffee.com/{BuyMeACoffeeUsername}"
            );
            AddLinkedChannel(
                channels,
                "Ko-fi",
                "kofi",
                KofiUsername,
                $"https://ko-fi.com/{KofiUsername}"
            );
        }

        private void AddBankChannels(List<FundingChannelSettings> channels)
        {
            AddBankChannel(channels, "Salt Bank", "saltbank", SaltBank);
        }

        private static void AddBankChannel(
            List<FundingChannelSettings> channels,
            string name,
            string slug,
            BankAccountSettings account
        )
        {
            if (string.IsNullOrWhiteSpace(account.Iban))
            {
                return;
            }

            channels.Add(new FundingChannelSettings
            {
                Name = name,
                Image = ImagePathFor(slug),
                BankTransfer = new BankTransferSettings
                {
                    ModalIdentifier = $"modal-{slug}",
                    Iban = account.Iban,
                    Swift = account.Swift
                }
            });
        }

        private static void AddLinkedChannel(
            List<FundingChannelSettings> channels,
            string name,
            string slug,
            string identifier,
            string url
        )
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return;
            }

            channels.Add(new FundingChannelSettings
            {
                Name = name,
                Image = ImagePathFor(slug),
                Url = url
            });
        }

        private static string ImagePathFor(string slug)
            => $"../assets/img/funding/{slug}.png";
    }
}
