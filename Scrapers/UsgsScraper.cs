using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;

using GeoJSON.Net.Geometry;

using ICSharpCode.SharpZipLib.Tar;

using Iida.Shared.Requests;
using Iida.Shared.Usgs;

using Newtonsoft.Json;

namespace Iida.Core.Scrapers;

internal partial class UsgsScraper : IScraper {
	private readonly string _tempFolder;
	private readonly Parameters _parameters;
	public UsgsScraper(string tempFolder, Parameters parameters) {
		_tempFolder = tempFolder;
		_parameters = parameters;
	}
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0063:Use simple 'using' statement", Justification = "Download breaks if using the simplified using statement")]
	public async Task<(IEnumerable<string>, IEnumerable<string>)> Execute(Order order) {
		var dates = new List<string>();
		var paths = new List<string>();
		try {
			Console.WriteLine("Calculating centroid of polygon...");
			var polygon = (Polygon)order.FeatureCollection!.Features[0].Geometry;
			var lineString = polygon.Coordinates[0];
			var vertexes = lineString.Coordinates;
			var (latitude, longitude) = CalculateCentroid(vertexes);
			Console.WriteLine($"Centroid calculated: ({latitude}; {longitude})");
			Console.WriteLine("Getting HTTP clients ready...");
			var apiClient = new HttpClient {
				Timeout = TimeSpan.FromMinutes(10)
			};
			var cookies = new CookieContainer();
			var websiteClient = new HttpClient(new HttpClientHandler {
				AllowAutoRedirect = false,
				CookieContainer = cookies
			});
			var downloadClient = new HttpClient(new HttpClientHandler { CookieContainer = cookies }) {
				Timeout = TimeSpan.FromMinutes(10)
			};
			Console.WriteLine("Scraping login website...");
			var website = await websiteClient.GetAsync($"{_parameters!.Login}");
			if (website.IsSuccessStatusCode) {
				var csrfRegex = CsrfRegex();
				var getSessionContent = await website.Content.ReadAsStringAsync();
				var csrf = csrfRegex.Matches(getSessionContent)[0].Value.Split('"')[3];
				var data = new[] {
					new KeyValuePair<string, string>("username", _parameters.Username!),
					new KeyValuePair<string, string>("password", _parameters.Password!),
					new KeyValuePair<string, string>("csrf", csrf)
				};
				Console.WriteLine("Logging in...");
				var websiteLogin = await websiteClient.PostAsync($"{_parameters.Login}", new FormUrlEncodedContent(data));
				var apiClientResponse = await apiClient.PostAsJsonAsync($"{_parameters.Api}/login", new { username = _parameters.Username, password = _parameters.Password });
				if (apiClientResponse.IsSuccessStatusCode) {
					Console.WriteLine("Logged in successfully");
					var token = JsonConvert.DeserializeObject<Shared.Usgs.LoginResponse>(await apiClientResponse.Content.ReadAsStringAsync());
					apiClient.DefaultRequestHeaders.Add("X-Auth-Token", token!.Data);
					Console.WriteLine("Preparing scene search payload...");
					var payload = new {
						datasetName = _parameters.Dataset,
						sceneFilter = new {
							acquisitionFilter = new {
								start = order.Start,
								end = order.End
							},
							spatialFilter = new {
								filterType = "mbr",
								lowerLeft = new {
									latitude,
									longitude,
								},
								upperRight = new {
									latitude,
									longitude,
								}
							}
						}
					};
					Console.WriteLine("Searching for scenes...");
					var apiClientResponse1 = await apiClient.PostAsJsonAsync($"{_parameters.Api}/scene-search", payload);
					if (apiClientResponse1.IsSuccessStatusCode) {
						Console.WriteLine("Scene search successful");
						var dataProductId = _parameters.Dataset switch {
							"landsat_tm_c1" => DataProductId.LandsatTmC1,
							"landsat_etm_c1" => DataProductId.LandsatEtmC1,
							"landsat_8_c1" => DataProductId.Landsat8C1,
							"landsat_tm_c2_l1" => DataProductId.LandsatTmC2L1,
							"landsat_etm_c2_l1" => DataProductId.LandsatEtmC2L1,
							"landsat_ot_c2_l1" => DataProductId.LandsatOtC2L1,
							"landsat_tm_c2_l2" => DataProductId.LandsatTmC2L2,
							"landsat_etm_c2_l2" => DataProductId.LandsatEtmC2L2,
							"landsat_ot_c2_l2" => DataProductId.LandsatOtC2L2,
							"sentinel_2a" => DataProductId.Sentinel2a,
							_ => null
						};
						var searchSceneResponse = JsonConvert.DeserializeObject<SearchSceneResponse>(await apiClientResponse1.Content.ReadAsStringAsync());
						Console.WriteLine($"Found {searchSceneResponse!.data!.recordsReturned} scenes");
						foreach (var result in searchSceneResponse.data.results!) {
							Console.WriteLine($"Checking scene {result!.entityId}...");
							if (double.Parse(result.cloudCover!) > double.Parse(_parameters.CloudCover!)) {
								Console.WriteLine($"Scene exceeds maximum cloud cover ({_parameters.CloudCover}%)");
								continue;
							}
							Console.WriteLine($"Scene {result.entityId}: Scraping download website...");
							var downloadWebsiteResponse = await websiteClient.GetAsync($"{_parameters.SearchScene}/{dataProductId!.Value}/{result.entityId}");
							if (downloadWebsiteResponse.IsSuccessStatusCode) {
								var resultDataProductIdRegex = DataProductIdRegex();
								var queryContent = await downloadWebsiteResponse.Content.ReadAsStringAsync();
								var resultDataProductIdMatch = resultDataProductIdRegex.Match(queryContent);
								var resultDataProductId = resultDataProductIdMatch.Value.Split('"')[1];
								var downloadUrl = $"{_parameters.DownloadScene}/{resultDataProductId}/{result.entityId}/EE/";
								try {
									var downloadPath = $"{Path.Combine(_tempFolder, result.entityId!)}";
									_ = Directory.CreateDirectory(downloadPath);
									Console.WriteLine($"Scene {result.entityId}: downloading scene from {downloadUrl}...");
									using (var download = await downloadClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead)) {
										using (var from = await download.Content.ReadAsStreamAsync()) {
											using (var to = File.OpenWrite(Path.Combine(downloadPath, $"{result.entityId}.tar"))) {
												await from.CopyToAsync(to);
											}
										}
									}
									Console.WriteLine($"Scene {result.entityId}: Download successful. Extracting TAR");
									var tar = TarArchive.CreateInputTarArchive(File.OpenRead(Path.Combine(downloadPath, $"{result.entityId}.tar")), Encoding.UTF8);
									tar.ExtractContents($"{Path.Combine(Path.GetTempPath(), "iida", $"{result.entityId}")}");
									tar.Close();
									paths.Add(downloadPath);
									Console.WriteLine($"Scene {result.entityId}: complete");
								} catch {
									Console.WriteLine("Something happened while processing the scene/files");
								}
							} else {
								Console.WriteLine("Error scraping download website");
							}
						}
					} else {
						Console.WriteLine("Error searching scenes");
					}
					var logout = await apiClient.PostAsJsonAsync($"{_parameters.Api}/logout", string.Empty);
					if (logout.IsSuccessStatusCode) {
						Console.WriteLine("Logged out successfully");
					} else {
						Console.WriteLine("Error logging out");
					}
				} else {
					Console.WriteLine("Error logging in");
				}
			} else {
				Console.WriteLine("Error scraping login website");
			}
		} catch (HttpRequestException) {
			Console.WriteLine("Disconnected");
		} catch (TaskCanceledException) {
			Console.WriteLine("Task canceled");
		} catch (NullReferenceException) {
			Console.WriteLine("Null reference detected");
		} catch (ArgumentNullException) {
			Console.WriteLine("Null argument detected");
		}
		return (dates, paths);
	}

	[GeneratedRegex("name=\"csrf\" value=\"(.+?)\"")]
	private static partial Regex CsrfRegex();

	private static (double latitude, double longitude) CalculateCentroid(IReadOnlyCollection<IPosition> vertexes) {
		var x = 0.0;
		var y = 0.0;
		var z = 0.0;
		foreach (var vertex in vertexes) {
			var latitude = vertex.Latitude;
			var longitude = vertex.Longitude;
			latitude *= Math.PI / 180;
			longitude *= Math.PI / 180;
			x += Math.Cos(latitude) * Math.Cos(longitude);
			y += Math.Cos(latitude) * Math.Sin(longitude);
			z += Math.Sin(latitude);
		}
		var centroidLongitude = Math.Atan2(y, x);
		var hyperbola = Math.Sqrt(x * x + y * y);
		var centroidLatitude = Math.Atan2(z, hyperbola);
		centroidLatitude *= 180 / Math.PI;
		centroidLongitude *= 180 / Math.PI;
		return (centroidLatitude, centroidLongitude);
	}

	[GeneratedRegex("data-productId=\"(.+?)\"")]
	private static partial Regex DataProductIdRegex();
}