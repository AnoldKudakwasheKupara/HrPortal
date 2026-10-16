namespace HrPortal.Entities
{
    /// <summary>
    /// A post in the organisation, as opposed to the person filling it.
    ///
    /// Job titles used to be free text on each employee, so the same
    /// post could be spelled three ways and nobody could ask how many
    /// of them there were or which were empty.
    /// </summary>
    public class Position
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        /// <summary>
        /// The department the post sits in, where it belongs to one.
        /// </summary>
        public int? DepartmentId { get; set; }

        /// <summary>
        /// How many of this post the organisation is budgeted for.
        /// Left blank when there is no fixed number, in which case
        /// nothing is ever reported as vacant.
        /// </summary>
        public int? Establishment { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        public Department? Department { get; set; }

        public ICollection<Employee> Employees { get; set; }
            = new List<Employee>();
    }
}
