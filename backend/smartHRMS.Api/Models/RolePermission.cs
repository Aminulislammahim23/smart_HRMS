namespace smartHRMS.Api.Models
{
    public class RolePermission
    {
        public int Id { get; set; }

        public int RoleId { get; set; }
        public Role? Role { get; set; }

        public int PermissionId { get; set; }
        public Permission? Permission { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
