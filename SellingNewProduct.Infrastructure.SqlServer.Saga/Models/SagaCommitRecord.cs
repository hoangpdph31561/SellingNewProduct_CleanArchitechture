namespace SellingNewProduct.Infrastructure.SqlServer.Saga.Models;

/// <summary>
/// Durable proof that the SQL pivot committed for a saga. Written by <c>SqlSagaParticipant</c> INSIDE
/// the same business transaction as the order update, so this row exists if and only if the pivot
/// commit succeeded. The recovery worker reads it to tell a successful saga from one interrupted
/// after the Mongo commit. Lives in <c>AppDbContext</c> (NOT the saga-log context) precisely so it is
/// atomic with the business commit.
/// </summary>
internal sealed class SagaCommitRecord
{
    public Guid SagaId { get; set; }
    public DateTime CommittedUtc { get; set; }
}
