using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Employees;
using SellingNewProduct.Infrastructure.SqlServer.Models;

namespace SellingNewProduct.Infrastructure.SqlServer.Mapping;

internal static class EmployeeMapper
{
    public static EmployeeRecord ToRecord(Employee theEmployee) => new()
    {
        Id = theEmployee.Id,
        FullName = theEmployee.FullName,
        Position = theEmployee.Position,
        HireDate = theEmployee.HireDate,
        UserId = theEmployee.UserId,
        Status = (int)theEmployee.Status,
        CreatedAtUtc = theEmployee.CreatedAtUtc,
        UpdatedAtUtc = theEmployee.UpdatedAtUtc
    };

    public static void MapInto(EmployeeRecord theTarget, Employee theSource)
    {
        theTarget.FullName = theSource.FullName;
        theTarget.Position = theSource.Position;
        theTarget.HireDate = theSource.HireDate;
        theTarget.UserId = theSource.UserId;
        theTarget.Status = (int)theSource.Status;
        theTarget.UpdatedAtUtc = theSource.UpdatedAtUtc;
    }

    public static Employee ToDomain(EmployeeRecord theRecord) => Employee.Rehydrate(
        theRecord.Id,
        theRecord.FullName,
        theRecord.Position,
        theRecord.HireDate,
        theRecord.UserId,
        (EntityStatus)theRecord.Status,
        theRecord.CreatedAtUtc,
        theRecord.UpdatedAtUtc);
}
