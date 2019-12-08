using Helium.DataAccessLayer;
using Helium.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Helium
{
    public class CosmosHealthCheck : IHealthCheck
    {
        public static readonly string Description = "Cosmos DB Health Check";

        // TODO - /healthz doesn't appear in swagger

        private readonly ILogger _logger;
        private readonly IDAL _dal;

        // ignore nulls in json
        private static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">ILogger</param>
        /// <param name="dal">IDAL</param>
        public CosmosHealthCheck(ILogger<CosmosHealthCheck> logger, IDAL dal)
        {
            // save to member vars
            _logger = logger;
            _dal = dal;
        }

        /// <summary>
        /// Run the health check (IHealthCheck)
        /// </summary>
        /// <param name="context">HealthCheckContext</param>
        /// <param name="cancellationToken">CancellationToken</param>
        /// <returns></returns>
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            // dictionary
            var data = new Dictionary<string, object>();

            try
            {
                if (App.config != null)
                {
                    // get the current Cosmos key
                    data.Add("CosmosKey", App.config.GetValue<string>(Constants.CosmosKey).PadRight(5).Substring(0, 5).Trim() + "...");
                }

                data.Add("Instance", System.Environment.GetEnvironmentVariable("WEBSITE_ROLE_INSTANCE_ID") ?? "unknown");
                data.Add("Version", Helium.Version.AssemblyVersion);

                // Run each individual health check
                data.Add("GetGenresAsync", await GetGenresAsync());
                data.Add("GetActorByIdAsync", await GetActorByIdAsync("nm0000173"));
                data.Add("GetMovieByIdAsync", await GetMovieByIdAsync("tt0133093"));
                data.Add("SearchMoviesAsync", await SearchMoviesAsync("ring"));
                data.Add("SearchActorsAsync", await SearchActorsAsync("nicole"));
                data.Add("GetTopRatedMoviesAsync", await GetTopRatedMoviesAsync());

                HealthStatus status = HealthStatus.Healthy;

                foreach(var d in data.Values)
                {
                    if (status != HealthStatus.Unhealthy)
                    {
                        if (d is HealthzResult h && h.StatusCode != HealthStatus.Healthy)
                        {
                            status = h.StatusCode;
                        }
                    }
                }

                return new HealthCheckResult(status, Description, data: data);
            }

            catch (CosmosException ce)
            {
                // log and return Unhealthy
                _logger.LogError($"CosmosException:Healthz:{ce.StatusCode}:{ce.ActivityId}:{ce.Message}\n{ce}");

                data.Add("CosmosException", ce.Message);

                return new HealthCheckResult(HealthStatus.Unhealthy, Description, ce, data);
            }

            catch (System.AggregateException age)
            {
                var root = age.GetBaseException() ?? age;

                data.Add("AggregateException", root.Message);

                // log and return unhealthy
                _logger.LogError($"AggregateException|Healthz|{root.GetType()}|{root.Message}|{root.Source}|{root.TargetSite}");

                return new HealthCheckResult(HealthStatus.Unhealthy, Description, root, data);
            }

            catch (Exception ex)
            {
                // log and return unhealthy
                _logger.LogError($"Exception:Healthz\n{ex}");

                data.Add("Exception", ex.Message);

                return new HealthCheckResult(HealthStatus.Unhealthy, Description, ex, data);
            }
        }

        /// <summary>
        /// Run the Get Genres Healthcheck
        /// </summary>
        /// <returns>HealthzResult</returns>
        private async Task<HealthzResult> GetGenresAsync()
        {
            var result = new HealthzResult();

            Stopwatch sw = new Stopwatch();

            sw.Start();
            (await _dal.GetGenresAsync()).ToList<string>();
            sw.Stop();
            result.Uri = "/api/genres";
            result.StatusCode = HealthStatus.Healthy;
            result.TotalMilliseconds = sw.ElapsedMilliseconds;

            if (sw.ElapsedMilliseconds > 200)
            {
                result.StatusCode = HealthStatus.Degraded;
                result.Message = "Request exceeded expected duration";
            }

            return result;
        }

        /// <summary>
        /// Run the Get Movie by Id Healthcheck
        /// </summary>
        /// <returns>HealthzResult</returns>
        private async Task<HealthzResult> GetMovieByIdAsync(string movieId)
        {
            var result = new HealthzResult();

            Stopwatch sw = new Stopwatch();

            sw.Start();
            await _dal.GetMovieAsync(movieId);
            sw.Stop();
            result.Uri = string.Format($"/api/movies/{movieId}");
            result.StatusCode = HealthStatus.Healthy;
            result.TotalMilliseconds = sw.ElapsedMilliseconds;

            if (sw.ElapsedMilliseconds > 100)
            {
                result.StatusCode = HealthStatus.Degraded;
                result.Message = "Request exceeded expected duration";
            }

            return result;
        }

        /// <summary>
        /// Run the Search Movies Healthcheck
        /// </summary>
        /// <returns>HealthzResult</returns>
        private async Task<HealthzResult> SearchMoviesAsync(string query)
        {
            var result = new HealthzResult();

            Stopwatch sw = new Stopwatch();

            sw.Start();
            (await _dal.GetMoviesByQueryAsync(query)).ToList<Movie>();
            sw.Stop();
            result.Uri = string.Format($"/api/movies?q={query}");
            result.StatusCode = HealthStatus.Healthy;
            result.TotalMilliseconds = sw.ElapsedMilliseconds;

            if (sw.ElapsedMilliseconds > 100)
            {
                result.StatusCode = HealthStatus.Degraded;
                result.Message = "Request exceeded expected duration";
            }

            return result;
        }

        /// <summary>
        /// Run the Get Top Rated Movies Healthcheck
        /// </summary>
        /// <returns>HealthzResult</returns>
        private async Task<HealthzResult> GetTopRatedMoviesAsync()
        {
            var result = new HealthzResult();

            Stopwatch sw = new Stopwatch();

            sw.Start();
            (await _dal.GetMoviesByQueryAsync(string.Empty, toprated: true)).ToList<Movie>();
            sw.Stop();
            result.Uri = string.Format($"/api/movies?toprated=true");
            result.StatusCode = HealthStatus.Healthy;
            result.TotalMilliseconds = sw.ElapsedMilliseconds;

            if (sw.ElapsedMilliseconds > 100)
            {
                result.StatusCode = HealthStatus.Degraded;
                result.Message = "Request exceeded expected duration";
            }

            return result;
        }

        /// <summary>
        /// Run the Get Actor By Id Healthcheck
        /// </summary>
        /// <returns>HealthzResult</returns>
        private async Task<HealthzResult> GetActorByIdAsync(string actorId)
        {
            var result = new HealthzResult();

            Stopwatch sw = new Stopwatch();

            sw.Start();
            await _dal.GetActorAsync(actorId);
            sw.Stop();
            result.Uri = string.Format($"/api/actors/{actorId}");
            result.StatusCode = HealthStatus.Healthy;
            result.TotalMilliseconds = sw.ElapsedMilliseconds;

            if (sw.ElapsedMilliseconds > 100)
            {
                result.StatusCode = HealthStatus.Degraded;
                result.Message = "Request exceeded expected duration";
            }

            return result;
        }

        /// <summary>
        /// Run the Search Actors Healthcheck
        /// </summary>
        /// <returns>HealthzResult</returns>
        private async Task<HealthzResult> SearchActorsAsync(string query)
        {
            var result = new HealthzResult();

            Stopwatch sw = new Stopwatch();

            sw.Start();
            (await _dal.GetActorsByQueryAsync(query)).ToList<Actor>();
            sw.Stop();
            result.Uri = string.Format($"/api/actors?q={query}");
            result.StatusCode = HealthStatus.Healthy;
            result.TotalMilliseconds = sw.ElapsedMilliseconds;

            if (sw.ElapsedMilliseconds > 100)
            {
                result.StatusCode = HealthStatus.Degraded;
                result.Message = "Request exceeded expected duration";
            }

            return result;
        }

        /// <summary>
        /// Write the health check results as json
        /// </summary>
        /// <param name="httpContext">HttpContext</param>
        /// <param name="healthReport">HealthReport</param>
        /// <returns></returns>
        public static Task CustomResponseWriter(HttpContext httpContext, HealthReport healthReport)
        {
            // TODO - convert to use system.json with camel casing

            // TODO - statusCode is an int - convert to healthy, degraded, unhealthy

            httpContext.Response.ContentType = "application/json";

            // write the json
            return httpContext.Response.WriteAsync(JsonConvert.SerializeObject(healthReport, jsonSettings));
        }
    }
}