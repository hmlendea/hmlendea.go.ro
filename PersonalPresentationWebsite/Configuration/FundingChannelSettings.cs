namespace PersonalPresentationWebsite.Configuration
{
    public sealed class FundingChannelSettings
    {
        public string Name { get; set; } = string.Empty;

        public string Image { get; set; } = string.Empty;

        public string Url { get; set; } = string.Empty;

        public BankTransferSettings? BankTransfer { get; set; }
    }
}
