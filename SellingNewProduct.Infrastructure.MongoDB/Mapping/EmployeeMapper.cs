using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Employees;
using SellingNewProduct.Infrastructure.MongoDB.Models;

namespace SellingNewProduct.Infrastructure.MongoDB.Mapping;

internal static class EmployeeMapper
{
    public static EmployeeDocument ToDocument(Employee theEmployee) => new()
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

    public static void MapInto(EmployeeDocument theTarget, Employee theSource)
    {
        theTarget.FullName = theSource.FullName;
        theTarget.Position = theSource.Position;
        theTarget.HireDate = theSource.HireDate;
        theTarget.UserId = theSource.UserId;
        theTarget.Status = (int)theSource.Status;
        theTarget.UpdatedAtUtc = theSource.UpdatedAtUtc;
    }

    public static Employee ToDomain(EmployeeDocument theDocument) => Employee.Rehydrate(
        theDocument.Id,
        theDocument.FullName,
        theDocument.Position,
        theDocument.HireDate,
        theDocument.UserId,
        (EntityStatus)theDocument.Status,
        theDocument.CreatedAtUtc,
        theDocument.UpdatedAtUtc);
}
