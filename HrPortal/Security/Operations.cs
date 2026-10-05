using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace HrPortal.Security
{
    public static class Operations
    {
        public static readonly OperationAuthorizationRequirement View =
            new() { Name = nameof(View) };

        public static readonly OperationAuthorizationRequirement Manage =
            new() { Name = nameof(Manage) };
    }
}
