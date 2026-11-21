namespace PersonalPresentationWebsite.Configuration
{
    public sealed class BankTransferSettings
    {
        public string ModalIdentifier { get; set; } = string.Empty;

        public string Iban { get; set; } = string.Empty;

        public string Swift { get; set; } = string.Empty;
    }
}
