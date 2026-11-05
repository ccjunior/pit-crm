using PitCrm.Domain.Demands;

namespace PitCrm.Application.Demands;

public interface IDemandStore
{
    Task<DemandCreateResult> CreateAsync(Demand demand, DemandActivity initialActivity, CancellationToken cancellationToken);
}
