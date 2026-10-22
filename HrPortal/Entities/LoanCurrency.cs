namespace HrPortal.Entities
{
    /// <summary>
    /// The currencies a staff loan may be taken in. Zimbabwe's two
    /// legal tenders; anything else would need a code change.
    /// </summary>
    public enum LoanCurrency
    {
        /// <summary>United States dollar.</summary>
        Usd = 0,

        /// <summary>Zimbabwe Gold.</summary>
        ZiG = 1
    }
}
