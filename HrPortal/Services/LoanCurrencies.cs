using HrPortal.Entities;

namespace HrPortal.Services
{
    /// <summary>
    /// How each loan currency is written: the code beside a figure,
    /// and the name used when the amount is spelled out in words.
    /// </summary>
    public static class LoanCurrencies
    {
        public static string Code(LoanCurrency currency) =>
            currency switch
            {
                LoanCurrency.ZiG => "ZiG",
                _ => "USD"
            };

        /// <summary>
        /// The figure with its code in front, as it would be written
        /// on the form.
        /// </summary>
        public static string Format(
            LoanCurrency currency,
            decimal amount) =>
            $"{Code(currency)} {amount:N2}";

        /// <summary>
        /// The amount spelled out, naming the currency rather than
        /// defaulting to dollars. ZiG reads the same either way.
        /// </summary>
        public static string InWords(
            LoanCurrency currency,
            decimal amount) =>
            currency switch
            {
                LoanCurrency.ZiG =>
                    AmountInWords.Convert(amount, "ZiG", "ZiG"),

                _ => AmountInWords.Convert(
                    amount, "US dollar", "US dollars")
            };
    }
}
