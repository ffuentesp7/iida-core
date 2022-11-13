using System.Globalization;

using Iida.Shared.Agromet;
using Iida.Shared.Requests;

using Oware;

namespace Iida.Core.Scrapers;

internal class AgrometScraper : IScraper {
	private readonly string _tempFolder;
	private readonly IEnumerable<string> _dates;
	private readonly Parameters _parameters;
	public List<string> Paths { get; set; } = new List<string>();
	public AgrometScraper(string tempFolder, IEnumerable<string> dates, Parameters parameters) {
		_tempFolder = tempFolder;
		_dates = dates;
		_parameters = parameters;
	}
	public async Task Execute(Order order, double latitude, double longitude) {
		try {
			Console.WriteLine("Getting HTTP client ready...");
			var client = new HttpClient {
				Timeout = TimeSpan.FromMinutes(int.Parse(_parameters.Timeout!))
			};
			Console.WriteLine("Getting closest station...");
			var latLngUtmConverter = new LatLngUTMConverter(string.Empty);
			var utmCoordinates = latLngUtmConverter.convertLatLngToUtm(latitude, longitude);
			var closestRequest = await client.GetAsync($"{_parameters.Location}?ema_f_utmx={utmCoordinates.Easting.ToString(CultureInfo.InvariantCulture)}&ema_f_utmy={utmCoordinates.Northing.ToString(CultureInfo.InvariantCulture)}&ema_f_lat={latitude.ToString(CultureInfo.InvariantCulture)}&ema_f_lon={longitude.ToString(CultureInfo.InvariantCulture)}");
			Console.WriteLine($"{_parameters.Location}?ema_f_utmx={utmCoordinates.Easting.ToString(CultureInfo.InvariantCulture)}&ema_f_utmy={utmCoordinates.Northing.ToString(CultureInfo.InvariantCulture)}&ema_f_lat={latitude.ToString(CultureInfo.InvariantCulture)}&ema_f_lon={longitude.ToString(CultureInfo.InvariantCulture)}");
			if (closestRequest.IsSuccessStatusCode) {
				var closest = await closestRequest.Content.ReadAsStringAsync();
				Console.WriteLine($"Closest station ID: {closest}");
				foreach (var date in _dates) {
					var url = $"{_parameters.Api}?ema_ia_id={closest}&dateFrom={DateOnly.ParseExact(date, "yyyy-MM-dd HH:mm:ss", null)}&dateTo={DateOnly.ParseExact(date, "yyyy-MM-dd HH:mm:ss", null).AddDays(1)}";
					Console.WriteLine($"Sending request to AGROMET: {url}...");
					var agrometRequest = new HttpRequestMessage {
						Method = HttpMethod.Get,
						RequestUri = new Uri(url),
					};
					var response = await client.SendAsync(agrometRequest);
					if (response.IsSuccessStatusCode) {
						Console.WriteLine(await response.Content.ReadAsStringAsync());
					} else {
						Console.WriteLine("Error while getting data from AGROMET");
					}
				}
			} else {
				Console.Write("Error getting closest station");
			}
		} catch (HttpRequestException) {
			Console.WriteLine("Disconnected");
		} catch (TaskCanceledException) {
			Console.WriteLine("Task canceled");
		} catch (NullReferenceException) {
			Console.WriteLine("Null reference detected");
		} catch (ArgumentNullException) {
			Console.WriteLine("Null argument detected");
		} catch (FormatException) {
			Console.WriteLine("Invalid format detected");
		}
	}
}