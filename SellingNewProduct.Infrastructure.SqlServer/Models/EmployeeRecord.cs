namespace SellingNewProduct.Infrastructure.SqlServer.Models;

/// <summary>SQL Server persistence model for Employee.</summary>
internal sealed class EmployeeRecord : BaseRecord
{
    public string FullName { get; set; } = default!;
    public string Position { get; set; } = default!;
    public DateTime HireDate { get; set; }
    public Guid UserId { get; set; }
}
