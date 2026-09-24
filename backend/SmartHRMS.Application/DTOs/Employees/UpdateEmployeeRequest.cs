public class UpdateEmployeeRequest
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public DateTime JoiningDate { get; set; }

    public bool IsActive { get; set; }
}