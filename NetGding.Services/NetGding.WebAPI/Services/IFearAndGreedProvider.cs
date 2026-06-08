using System.Threading;
using System.Threading.Tasks;
using NetGding.WebApi.Models;

namespace NetGding.WebApi.Services;

public interface IFearAndGreedProvider
{
    Task<FearAndGreedDto?> GetLatestAsync(CancellationToken ct = default);
}
