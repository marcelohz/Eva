using Hangfire;
using Hangfire.Common;
using Hangfire.States;

namespace Eva.Tests.Infrastructure;

internal sealed class TestBackgroundJobClient : IBackgroundJobClient
{
    public string Create(Job job, IState state) => Guid.NewGuid().ToString();

    public bool ChangeState(string jobId, IState state, string expectedState) => true;

    public bool Delete(string jobId) => true;

    public string Enqueue(Job job) => Guid.NewGuid().ToString();

    public string Schedule(Job job, TimeSpan delay) => Guid.NewGuid().ToString();

    public string Schedule(Job job, DateTimeOffset enqueueAt) => Guid.NewGuid().ToString();

    public string ContinueJobWith(string parentId, Job job) => Guid.NewGuid().ToString();

    public string ContinueJobWith(string parentId, Job job, IState nextState) => Guid.NewGuid().ToString();

    public string ContinueJobWith(string parentId, string queue, Job job, IState nextState) => Guid.NewGuid().ToString();

    public string Requeue(string jobId) => jobId;
}
