namespace HrPortal.Security
{
    /// <summary>
    /// A record that belongs to one employee.
    ///
    /// Implementing this is what makes a record subject to the
    /// ownership rules: the authorization handler and the query
    /// scope both work in terms of this interface rather than
    /// naming each entity, so a new personal record type is
    /// covered by declaring the interface on it.
    /// </summary>
    public interface IEmployeeOwned
    {
        /// <summary>
        /// The employee whose record this is. Not the person who
        /// created or uploaded it.
        /// </summary>
        int EmployeeId { get; }
    }
}
