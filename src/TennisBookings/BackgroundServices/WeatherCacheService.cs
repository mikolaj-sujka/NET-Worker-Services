using Microsoft.Extensions.Options;
using TennisBookings.External;

namespace TennisBookings.BackgroundServices;

public class WeatherCacheService : BackgroundService
{
	private readonly IWeatherApiClient _weatherApiClient;
	private readonly IDistributedCache<WeatherResult> _cache;
	private readonly ILogger<WeatherCacheService> _logger;

	private readonly int _minutesToCache;
	private readonly int _refreshIntervalInSeconds;

	public WeatherCacheService(IWeatherApiClient weatherApiClient, IDistributedCache<WeatherResult> cache,
		ILogger<WeatherCacheService> logger, IOptionsMonitor<ExternalServicesConfiguration> options)
	{
		_weatherApiClient = weatherApiClient ?? throw new ArgumentNullException(nameof(weatherApiClient));
		_cache = cache ?? throw new ArgumentNullException(nameof(cache));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		_minutesToCache = options.Get(ExternalServicesConfiguration.WeatherApi).MinsToCache;
		_refreshIntervalInSeconds = _minutesToCache > 1 ? (_minutesToCache - 1) * 60 : 30;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while(!stoppingToken.IsCancellationRequested)
		{
			var forecast = await _weatherApiClient.GetWeatherForecastAsync("London", stoppingToken);

			if (forecast is not null)
			{
				var currentWeather = new WeatherResult
				{
					City = "London",
					Weather = forecast.Weather
				};

				var cacheKey = $"current_weather_{
					DateTime.UtcNow:yyyy_MM_dd}";

				_logger.LogInformation("Caching weather for {City} with key {CacheKey}", forecast.City, cacheKey);

				await _cache.SetAsync(cacheKey, currentWeather, _minutesToCache);
			}

			await Task.Delay(TimeSpan.FromSeconds(_refreshIntervalInSeconds), stoppingToken);
		}
	}
}
