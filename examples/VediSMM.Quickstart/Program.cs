using VediSMM;

using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
using var sdk = new VediSmm(new VediSmmClientOptions
{
    AccessToken = Environment.GetEnvironmentVariable("VEDISMM_ACCESS_TOKEN"),
});

ApiResponse response = await sdk.Accounts.ListAccountsAsync(
    cancellationToken: cancellation.Token);
Console.WriteLine(response.Data);
