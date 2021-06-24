using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi.Clients;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Deployment = Microsoft.VisualStudio.Services.ReleaseManagement.WebApi.Deployment;

namespace DevOpsAPI.Pages
{
    public class FetchDataBase : ComponentBase, IDisposable
    {
        protected string alert;
        [Inject] protected IConfiguration Config { get; set; }
        [Inject] private IJSRuntime JSRuntime { get; set; }
        protected List<Build> Builds { get; set; }
        protected List<BuildRelease> BuildRelease { get; set; }
        protected List<Deployment> Releases { get; set; }
        protected BuildHttpClient buildclient;
        protected ReleaseHttpClient relclient;
        protected IPagedList<TeamProjectReference> projects;
        protected string TestString { get; set; }
        protected string MaxWaitTime { get; set; }
        protected string NumberOfAgents { get; set; }
        protected string NumberWaiting { get; set; }
        private Timer timer;
        protected int offset;

        protected override async Task OnInitializedAsync()
        {
            var creds = new VssBasicCredential(string.Empty, Config.GetSection("DevOpsPAT").Value);

            var connection = new VssConnection(new Uri(Config.GetSection("DevOpsURL").Value), creds);
            var projectclient = await connection.GetClientAsync<ProjectHttpClient>();
            projects = await projectclient.GetProjects();
            buildclient = await connection.GetClientAsync<BuildHttpClient>();
            relclient = await connection.GetClientAsync<ReleaseHttpClient>();
            if (Builds == null)
            {
                Builds = new();
            }
            if (BuildRelease == null) { BuildRelease = new(); }
            if (Releases == null) { Releases = new(); }
            await LoadData();
        }

        protected async Task GetLocalTime()
        {
            offset = await GetLocalOffset(JSRuntime);
            offset = -1 * offset;
        }

        public async Task<int> GetLocalOffset(IJSRuntime JSRuntime)
        {
            return await JSRuntime.InvokeAsync<int>("JsFunctions.offset");
        }

        protected async Task LoadData()
        {
            await GetLocalTime();
            TestString = $"Last Updated {DateTime.UtcNow.AddHours(offset).ToLongTimeString()}";

            await BuildLoad();

            await ReleaseLoad();
            NumberWaiting = $"Number of Waiting Jobs {CountWaitingJobs()}";
            NumberOfAgents = $"Number of Running Jobs {CountNumberOfAgents()}";
            MaxWaitTime = $"Max Wait Time {CalcMaxWaitTime()}";
        }

        protected string CalcMaxWaitTime()
        {
            if (!BuildRelease.Any(x => x.Finish == null && !x.Release))
            {
                return string.Empty;
            }
            else
            {
                try
                {
                    return BuildRelease
                            .OrderByDescending(x => x.Queue)
                            .Take(50)
                            .Where(x => x.Finish == null && !x.Release)
                            .Max(x => x.Wait)
                            .ToString(@"hh\:mm\:ss");
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        protected string CountNumberOfAgents()
        {
            return BuildRelease
                .OrderByDescending(x => x.Queue)
                .Take(50)
                .Count(x => x.Start != null && x.Finish == null && !x.Release)
                .ToString();
        }

        protected string CountWaitingJobs()
        {
            return BuildRelease
                .OrderByDescending(x => x.Queue)
                .Take(50)
                .Count(x => x.Start == null && x.Finish == null && !x.Release)
                .ToString();
        }

        protected async Task ReleaseLoad()
        {
            foreach (var proj in projects)
            {
                var rs = await relclient.GetDeploymentsAsync(proj.Name).ConfigureAwait(false);
                foreach (var r in rs)
                {
                    var old = Releases.FirstOrDefault(x => x.Id == r.Id);
                    var oldbr = BuildRelease.FirstOrDefault(x => x.Id == r.Id);
                    Releases.Remove(old);
                    BuildRelease.Remove(oldbr);
                    r.ProjectReference = new();
                    r.ProjectReference.Name = proj.Name;
                    Releases.Add(r);
                    var br = new BuildRelease
                    {
                        Id = r.Id,
                        Name = $"{r.ProjectReference.Name} / {r.Release.Name} / {r.ReleaseEnvironmentReference.Name} / {r.ReleaseDefinitionReference.Name}",
                        Queue = r.QueuedOn == DateTime.MinValue ? null : r.QueuedOn,
                        Start = r.StartedOn == DateTime.MinValue ? null : r.StartedOn,
                        Finish = r.CompletedOn == DateTime.MinValue ? null : r.CompletedOn,
                        Wait = ((r.StartedOn == DateTime.MinValue ? DateTime.UtcNow : r.StartedOn) - r.QueuedOn),
                        Build = (r.CompletedOn == DateTime.MinValue ? DateTime.UtcNow : r.CompletedOn) - (r.StartedOn == DateTime.MinValue ? DateTime.UtcNow : r.StartedOn),
                        Status = $"{r.OperationStatus} {r.DeploymentStatus}",
                        Release = true,
                        URL = $"{Config.GetSection("DevOpsURL").Value}/{proj.Name}/_releaseProgress?_a=release-pipeline-progress&releaseId={r.Release.Id}"
                    };
                    BuildRelease.Add(br);
                }
            }
        }

        protected async Task BuildLoad()
        {
            foreach (var proj in projects)
            {
                var bs = await buildclient.GetBuildsAsync(proj.Id);
                foreach (var b in bs)
                {
                    RemoveBuilds(b);

                    RemoveBuildRelease(b);

                    Builds.Add(b);
                    BuildRelease br = GetBuildRelease(proj, b);
                    BuildRelease.Add(br);
                }
            }
        }

        private BuildRelease GetBuildRelease(TeamProjectReference proj, Build build)
        {
            return new BuildRelease
            {
                Id = build.Id,
                Name = $"{build.Project.Name} / {build.BuildNumber}",
                Queue = build.QueueTime,
                Start = build.StartTime,
                Finish = build.FinishTime,
                Wait = ((build.StartTime ?? DateTime.UtcNow) - build.QueueTime).Value,
                Build = (build.FinishTime ?? DateTime.UtcNow) - (build.StartTime ?? DateTime.UtcNow),
                Status = build.Result.ToString(),
                Release = false,
                URL = $"{Config.GetSection("DevOpsURL").Value}/{proj.Name}/_build/results?buildId={build.Id}&view=results"
            };
        }

        private void RemoveBuilds(Build b)
        {
            if (Builds.Any())
            {
                var old = Builds.FirstOrDefault(x => x.Id == b.Id);
                if (old != null)
                {
                    Builds.Remove(old);
                }
            }
        }

        private void RemoveBuildRelease(Build b)
        {
            if (BuildRelease.Any())
            {
                var oldbr = BuildRelease.FirstOrDefault(x => x.Id == b.Id);
                if (oldbr != null)
                {
                    BuildRelease.Remove(oldbr);
                }
            }
        }

        protected string GetRowState(BuildRelease build)
        {
            if (build.Start != null && build.Finish == null)
            {
                alert = "alert-success";
            }
            else if (!(build.Status == "Succeeded" || build.Status == "Approved Succeeded" || build.Status == "NotStarted" || build.Status == string.Empty || build.Status == null) ||
                ((build.Finish ?? DateTime.UtcNow) - (build.Start ?? DateTime.UtcNow)).TotalMinutes > 60 ||
                ((build.Start ?? DateTime.UtcNow) - build.Queue).Value.TotalMinutes > 60)
            {
                alert = "alert-danger";
            }
            else if ((build.Queue != null && build.Start == null) ||
                ((build.Finish ?? DateTime.UtcNow) - (build.Start ?? DateTime.UtcNow)).TotalMinutes > 20 ||
                ((build.Start ?? DateTime.UtcNow) - build.Queue).Value.TotalMinutes > 20)
            {
                alert = "alert-warning";
            }
            else
            {
                alert = "";
            }
            return alert;
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await JSRuntime.InvokeAsync<object>("TestDataTablesRemove", "#results");
            await JSRuntime.InvokeAsync<object>("TestDataTablesAdd", "#results");
            if (firstRender)
            {
                timer = new Timer
                {
                    Interval = Config.GetValue<int>("Reload")
                };
                timer.Elapsed += OnTimerInterval;
                timer.AutoReset = true;
                timer.Enabled = true;
            }
            base.OnAfterRender(firstRender);
        }

        private void OnTimerInterval(object sender, ElapsedEventArgs e)
        {
            InvokeAsync(() => LoadData());
            InvokeAsync(() => StateHasChanged());
        }

        public void Dispose()
        {
            Dispose(true).GetAwaiter();
            GC.SuppressFinalize(this);
        }

        protected virtual async Task Dispose(bool disposing)
        {
            await JSRuntime.InvokeAsync<object>("TestDataTablesRemove", "#results");
            timer?.Dispose();
        }
    }
}
