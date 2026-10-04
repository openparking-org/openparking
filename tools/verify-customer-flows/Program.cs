using Microsoft.Extensions.Hosting;
using OpenParking.Tests;

await using var app = await CustomerFlowTestHost.StartAsync(5099);
Console.WriteLine("Disposable customer-flow API: " + CustomerFlowTestHost.Address(app));
Console.WriteLine("Verification tokens: verification-admin / verification-driver. No production data or email is used.");
await app.WaitForShutdownAsync();
