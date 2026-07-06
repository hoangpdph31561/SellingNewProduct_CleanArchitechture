using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace SellingNewProduct.Infrastructure.Messaging.RabbitMq;

/// <summary>
/// Owns the single, lazily-opened RabbitMQ connection shared by the publisher and the consumer host
/// (a connection is thread-safe and expensive; channels are cheap and created per use). Opening is
/// deferred so the app still starts when the broker is not yet up.
/// </summary>
public sealed class RabbitMqConnectionProvider : IAsyncDisposable
{
    private readonly RabbitMqSettings mySettings;
    private readonly SemaphoreSlim myGate = new(1, 1);
    private IConnection? myConnection;

    public RabbitMqConnectionProvider(IOptions<RabbitMqSettings> theSettings)
    {
        mySettings = theSettings.Value;
    }

    public async Task<IConnection> GetConnectionAsync(CancellationToken theCancellationToken = default)
    {
        if (myConnection is { IsOpen: true })
        {
            return myConnection;
        }

        await myGate.WaitAsync(theCancellationToken);
        try
        {
            if (myConnection is { IsOpen: true })
            {
                return myConnection;
            }

            var aFactory = new ConnectionFactory
            {
                HostName = mySettings.HostName,
                Port = mySettings.Port,
                UserName = mySettings.UserName,
                Password = mySettings.Password
            };

            myConnection = await aFactory.CreateConnectionAsync(theCancellationToken);
            return myConnection;
        }
        finally
        {
            myGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (myConnection is not null)
        {
            await myConnection.DisposeAsync();
        }

        myGate.Dispose();
    }
}
