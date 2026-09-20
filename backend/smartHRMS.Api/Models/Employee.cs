namespace smartHRMS.Api.Models
{
    public class Employee
    {
        public int Id { get; set; }

        public string EmployeeCode { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public string? Address { get; set; }

        public DateTime JoiningDate { get; set; }
        public int DepartmentId { get; set; }
        public Department? Department { get; set; }
        public int DesignationId { get; set; }
        public Designation? Designation { get; set; }
        public int? UserId { get; set; }
        public User? User { get; set; }

        public string Status { get; set; } = "Active";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}