using System.Globalization;

namespace HrPortal.Services
{
    /// <summary>
    /// The loan application form asks for the amount in words as well
    /// as in figures, so the two can be checked against each other.
    /// </summary>
    public static class AmountInWords
    {
        private static readonly string[] Ones =
        {
            "zero", "one", "two", "three", "four", "five", "six",
            "seven", "eight", "nine", "ten", "eleven", "twelve",
            "thirteen", "fourteen", "fifteen", "sixteen", "seventeen",
            "eighteen", "nineteen"
        };

        private static readonly string[] Tens =
        {
            "", "", "twenty", "thirty", "forty", "fifty", "sixty",
            "seventy", "eighty", "ninety"
        };

        /// <summary>
        /// Renders an amount the way it would be written on a cheque,
        /// for example "one thousand two hundred and fifty dollars and
        /// forty cents".
        /// </summary>
        public static string Convert(
            decimal amount,
            string singular = "dollar",
            string plural = "dollars")
        {
            if (amount < 0)
                return "minus " + Convert(-amount, singular, plural);

            var whole = decimal.Truncate(amount);

            var cents = (int)Math.Round(
                (amount - whole) * 100,
                MidpointRounding.AwayFromZero);

            if (cents == 100)
            {
                whole += 1;
                cents = 0;
            }

            var words = WholeNumber(whole)
                + " "
                + (whole == 1 ? singular : plural);

            if (cents > 0)
            {
                words += " and "
                    + WholeNumber(cents)
                    + (cents == 1 ? " cent" : " cents");
            }

            return Capitalise(words);
        }

        private static string WholeNumber(decimal value)
        {
            if (value < 1)
                return Ones[0];

            var parts = new List<string>();

            foreach (var (size, name) in Scales)
            {
                if (value < size)
                    continue;

                var count = decimal.Truncate(value / size);
                value -= count * size;

                parts.Add(UnderThousand((int)count) + " " + name);
            }

            if (value >= 1)
            {
                if (parts.Count > 0 && value < 100)
                    parts.Add("and " + UnderThousand((int)value));
                else
                    parts.Add(UnderThousand((int)value));
            }

            return string.Join(" ", parts);
        }

        private static readonly (decimal Size, string Name)[] Scales =
        {
            (1_000_000_000m, "billion"),
            (1_000_000m, "million"),
            (1_000m, "thousand")
        };

        private static string UnderThousand(int value)
        {
            if (value >= 100)
            {
                var hundreds = Ones[value / 100] + " hundred";
                var rest = value % 100;

                return rest == 0
                    ? hundreds
                    : hundreds + " and " + UnderThousand(rest);
            }

            if (value < 20)
                return Ones[value];

            var tens = Tens[value / 10];
            var units = value % 10;

            return units == 0 ? tens : tens + "-" + Ones[units];
        }

        private static string Capitalise(string words) =>
            words.Length == 0
                ? words
                : char.ToUpper(words[0], CultureInfo.InvariantCulture)
                  + words[1..];
    }
}
