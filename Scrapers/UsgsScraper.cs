using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;

using GeoJSON.Net.Geometry;

using ICSharpCode.SharpZipLib.Tar;

using Iida.Shared;
using Iida.Shared.GoogleCloud;
using Iida.Shared.Requests;
using Iida.Shared.Usgs;

using Newtonsoft.Json;

namespace Iida.Core.Scrapers;

internal partial class UsgsScraper : IScraper {
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0063:Use simple 'using' statement", Justification = "Download breaks if using the simplified using statement")]
	public async Task Execute(Order order, Configuration[] configurations) {
		try {
			Console.WriteLine("Calculating centroid of polygon...");
			var polygon = (Polygon)order.FeatureCollection!.Features[0].Geometry;
			var lineString = polygon.Coordinates[0];
			var vertexes = lineString.Coordinates;
			var (latitude, longitude) = CalculateCentroid(vertexes);
			Console.WriteLine($"Centroid calculated: ({latitude}; {longitude})");
			var googleCloudParameters = (Shared.GoogleCloud.Parameters)configurations[0];
			var googleCloudStorage = new GoogleCloudStorage(googleCloudParameters);
			var usgsParameters = (Shared.Usgs.Parameters)configurations[1];
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
			var website = await websiteClient.GetAsync($"{usgsParameters!.Login}");
			if (website.IsSuccessStatusCode) {
				var csrfRegex = CsrfRegex();
				var getSessionContent = await website.Content.ReadAsStringAsync();
				var csrf = csrfRegex.Matches(getSessionContent)[0].Value.Split('"')[3];
				var data = new[] {
					new KeyValuePair<string, string>("username", usgsParameters.Username!),
					new KeyValuePair<string, string>("password", usgsParameters.Password!),
					new KeyValuePair<string, string>("csrf", csrf)
				};
				Console.WriteLine("Logging in...");
				var websiteLogin = await websiteClient.PostAsync($"{usgsParameters.Login}", new FormUrlEncodedContent(data));
				var apiClientResponse = await apiClient.PostAsJsonAsync($"{usgsParameters.Api}/login", new { username = usgsParameters.Username, password = usgsParameters.Password });
				if (apiClientResponse.IsSuccessStatusCode) {
					Console.WriteLine("Logged in successfully");
					var token = JsonConvert.DeserializeObject<Shared.Usgs.LoginResponse>(await apiClientResponse.Content.ReadAsStringAsync());
					apiClient.DefaultRequestHeaders.Add("X-Auth-Token", token!.Data);
					Console.WriteLine("Preparing scene search payload...");
					var payload = new {
						datasetName = usgsParameters.Dataset,
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
					var apiClientResponse1 = await apiClient.PostAsJsonAsync($"{usgsParameters.Api}/scene-search", payload);
					if (apiClientResponse1.IsSuccessStatusCode) {
						Console.WriteLine("Scene search successful");
						var dataProductId = usgsParameters.Dataset switch {
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
							if (double.Parse(result.cloudCover!) > double.Parse(usgsParameters.CloudCover!)) {
								Console.WriteLine($"Scene exceeds maximum cloud cover ({usgsParameters.CloudCover}%)");
								continue;
							}
							Console.WriteLine($"Scene {result.entityId}: Scraping download website...");
							var downloadWebsiteResponse = await websiteClient.GetAsync($"{usgsParameters.SearchScene}/{dataProductId!.Value}/{result.entityId}");
							if (downloadWebsiteResponse.IsSuccessStatusCode) {
								var resultDataProductIdRegex = DataProductIdRegex();
								var queryContent = await downloadWebsiteResponse.Content.ReadAsStringAsync();
								var resultDataProductIdMatch = resultDataProductIdRegex.Match(queryContent);
								var resultDataProductId = resultDataProductIdMatch.Value.Split('"')[1];
								var downloadUrl = $"{usgsParameters.DownloadScene}/{resultDataProductId}/{result.entityId}/EE/";
								var downloadPath = $"{Path.Combine(Path.GetTempPath(), "iida")}";
								try {
									Directory.Delete(downloadPath, true);
									Console.WriteLine($"Scene {result.entityId}: Deleted old download directory");
								} catch {
									Console.WriteLine($"Scene {result.entityId}: No old download directory found");
								}
								Console.WriteLine($"Scene {result.entityId}: downloading scene...");
								try {
									using (var download = await downloadClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead)) {
										using (var from = await download.Content.ReadAsStreamAsync()) {
											using (var to = File.OpenWrite(Path.Combine(downloadPath, "bands.tar"))) {
												await from.CopyToAsync(to);
											}
										}
									}
									Console.WriteLine($"Scene {result.entityId}: Download successful. Extracting TAR");
									var tar = TarArchive.CreateInputTarArchive(File.OpenRead(Path.Combine(downloadPath, "bands.tar")), Encoding.UTF8);
									tar.ExtractContents($"{Path.Combine(Path.GetTempPath(), "iida", "bands")}");
									tar.Close();
									Console.WriteLine($"Scene {result.entityId}: Preparing scene files...");
									var sceneFiles = Directory.GetFiles($"{Path.Combine(Path.GetTempPath(), "iida", "bands")}");
									foreach (var sceneFile in sceneFiles) {
										using var file = File.OpenRead(sceneFile);
										var fileName = new FileInfo(sceneFile).Name;
										var filePathOnBucket = $"{result.entityId}/{fileName}";
										Console.WriteLine($"Scene {result.entityId}: Uploading {fileName} to bucket {googleCloudParameters!.StorageBucket}...");
										_ = await googleCloudStorage.UploadFileAsync(file, filePathOnBucket);
										Console.WriteLine($"Scene {result.entityId}: file {fileName}: Upload complete");
									}
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
					var logout = await apiClient.PostAsJsonAsync($"{usgsParameters.Api}/logout", string.Empty);
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