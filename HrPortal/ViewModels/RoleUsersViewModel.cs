namespace HrPortal.ViewModels
{
    public class RoleUsersViewModel
    {
        public int RoleId { get; set; }

        public string RoleName { get; set; } = string.Empty;

        /// <summary>
        /// The company the role is being given at, or null for a role
        /// that holds everywhere.
        /// </summary>
        public string? CompanyName { get; set; }

        public List<RoleUserViewModel> Users { get; set; } = new();
    }

    public class RoleUserViewModel
    {
        public int UserId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public bool IsInRole { get; set; }
    }
}
