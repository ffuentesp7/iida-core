using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

using GeoJSON.Net.Geometry;

using Iida.Shared;
using Iida.Shared.Requests;
using Iida.Shared.Usgs;

using Newtonsoft.Json;

namespace Iida.Core.Scrapers;

internal partial class UsgsScraper : IScraper {
	public async Task Execute(Order? order, Configuration?[]? configurations) {
		try {
			var polygon = (Polygon)order.FeatureCollection.Features[0].Geometry;
			var lineString = polygon.Coordinates[0];
			var vertexes = lineString.Coordinates;
			var (latitude, longitude) = CalculateCentroid(vertexes);
			Console.WriteLine($"{latitude} - {longitude}");
			var googleCloudParameters = (Shared.GoogleCloud.Parameters?)configurations![0];
			var usgsParameters = (Shared.Usgs.Parameters?)configurations[1];
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
				var websiteLogin = await websiteClient.PostAsync($"{usgsParameters.Login}", new FormUrlEncodedContent(data));
				var apiClientResponse = await apiClient.PostAsJsonAsync($"{usgsParameters.Api}/login", new { username = usgsParameters.Username, password = usgsParameters.Password });
				if (apiClientResponse.IsSuccessStatusCode) {
					var token = JsonConvert.DeserializeObject<Shared.Usgs.LoginResponse>(await apiClientResponse.Content.ReadAsStringAsync());
					apiClient.DefaultRequestHeaders.Add("X-Auth-Token", token!.Data);
					var payload = new {
						datasetName = usgsParameters.Dataset,
						sceneFilter = new {
							acquisitionFilter = new {
								start = order!.Start,
								end = order!.End
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
					var apiClientResponse1 = await apiClient.PostAsJsonAsync($"{usgsParameters.Api}/scene-search", payload);
					if (apiClientResponse1.IsSuccessStatusCode) {
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
						var jsonResponse = JsonConvert.DeserializeObject<SearchSceneResponse>(await apiClientResponse1.Content.ReadAsStringAsync())!;
						Console.WriteLine(jsonResponse.sessionId);
					} else {
						Console.WriteLine("API USGS scene search error");
					}
					var logout = await apiClient.PostAsJsonAsync($"{usgsParameters.Api}/logout", string.Empty);
					if (logout.IsSuccessStatusCode) {
						Console.WriteLine("Logged out from USGS API");
					} else {
						Console.WriteLine("API USGS logout error");
					}
				} else {
					Console.WriteLine("API USGS login error");
				}
			}
		} catch (HttpRequestException) {
			Console.WriteLine("Disconnected");
		} catch (TaskCanceledException) {
			Console.WriteLine("Task canceled");
		} catch (NullReferenceException) {
			Console.WriteLine("Null reference detected");
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
}