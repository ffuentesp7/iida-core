using System.Globalization;
using System.Text;
using System.Xml.Linq;

using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;

using Iida.Shared.DataTransferObjects;
using Iida.Shared.Ran;

using Oware;

namespace Iida.Core.Scrapers;

internal class RanScraperForIidaR : IScraper {
	private readonly string _userFolder;
	private readonly IEnumerable<string> _dates;
	private readonly List<string> _entityIds;
	private readonly Parameters _parameters;
	public List<string> Paths { get; set; } = new List<string>();
	public RanScraperForIidaR(string userFolder, IEnumerable<string> dates, List<string> entityIds, Parameters parameters) {
		_userFolder = userFolder;
		_dates = dates;
		_parameters = parameters;
		_entityIds = entityIds;
	}
	public async Task Execute(Request request, double latitude, double longitude) {
		try {
			var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture) {
				HasHeaderRecord = true,
				Delimiter = ",",
				Encoding = Encoding.UTF8
			};
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
				var scene = 0;
				foreach (var date in _dates) {
					var url = $"{_parameters.Api}?ema_ia_id={closest}&dateFrom={DateOnly.ParseExact(date, "yyyy-MM-dd HH:mm:ss", null)}&dateTo={DateOnly.ParseExact(date, "yyyy-MM-dd HH:mm:ss", null).AddDays(1)}";
					Console.WriteLine($"Sending request to AGROMET: {url}...");
					var agrometRequest = new HttpRequestMessage {
						Method = HttpMethod.Get,
						RequestUri = new Uri(url),
					};
					var response = await client.SendAsync(agrometRequest);
					if (response.IsSuccessStatusCode) {
						Console.WriteLine($"Date: {date} Writing CSV file...");
						var xmlResponse = XElement.Parse(await response.Content.ReadAsStringAsync());
						var datas = xmlResponse.Elements("dato");
						var entries = new List<Entry>();
						foreach (var data in datas) {
							Console.WriteLine($"Date {date}: parsing XML data...");
							var variables = data.Value.Split('|');
							var dateTime = DateTime.ParseExact(variables[1], "yyyy-MM-dd H:mm:ss", CultureInfo.InvariantCulture);
							var dateOnly = dateTime.ToString("dd/MM/yyyy");
							var timeOnly = dateTime.ToString("HH:mm:ss");
							var averageAirTemperature = variables[3];
							var averageRelativeHumidity = variables[7];
							var maximumSolarRadiation = variables[11];
							var windDirection = variables[19];
							var entry = new Entry {
								Date = dateOnly,
								Time = timeOnly,
								AverageAirTemperature = averageAirTemperature,
								AverageRelativeHumidity = averageRelativeHumidity,
								MaximumSolarRadiation = maximumSolarRadiation,
								WindDirection = windDirection
							};
							entries.Add(entry);
						}
						Console.WriteLine($"Date {date}: Writing CSV file...");
						var path = Path.Combine(_userFolder, $"{_entityIds[scene]}", $"{_entityIds[scene++]}.csv");
						using var streamWriter = new StreamWriter(path);
						using var csvWriter = new CsvWriter(streamWriter, csvConfig);
						csvWriter.WriteHeader<Entry>();
						await csvWriter.NextRecordAsync();
						await csvWriter.WriteRecordsAsync(entries);
					} else {
						Console.WriteLine($"Date {date}: Error while getting data from AGROMET");
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
	internal class Entry {
		[Index(0), Name("Date")]
		public string? Date { get; set; }
		[Index(1), Name(name: "Time")]
		public string? Time { get; set; }
		[Index(2), Name("Rad")]
		public string? MaximumSolarRadiation { get; set; }
		[Index(3), Name("wind_dir")]
		public string? WindDirection { get; set; }
		[Index(4), Name("RH")]
		public string? AverageRelativeHumidity { get; set; }
		[Index(5), Name("temp")]
		public string? AverageAirTemperature { get; set; }
	}
}