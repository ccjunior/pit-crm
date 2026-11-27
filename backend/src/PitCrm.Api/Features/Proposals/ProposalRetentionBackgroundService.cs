using PitCrm.Application.Proposals;

namespace PitCrm.Api.Features.Proposals;

public sealed class ProposalRetentionBackgroundService(IServiceScopeFactory scopeFactory,
    ILogger<ProposalRetentionBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IProposalRetentionProcessor>();
                await processor.ProcessDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Proposal timeline retention processing failed.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
