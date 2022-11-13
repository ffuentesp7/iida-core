using System.Text.RegularExpressions;

using GeoJSON.Net.Geometry;

using Iida.Shared;
using Iida.Shared.Requests;

namespace Iida.Core.Scrapers;

internal partial class UsgsScraper : IScraper {
	public async Task Execute(Order? order, Configuration?[]? configurations) {
		try {
			var polygon = (Polygon)order.FeatureCollection.Features[0].Geometry;
			var lineStrings = polygon.Coordinates;
			foreach (var lineString in lineStrings) {
				var vertexes = lineString.Coordinates;
				var (latitude, longitude) = GetCentroid(vertexes);
				Console.WriteLine($"{latitude} - {longitude}");
			}
			//var googleCloudParameters = (Shared.GoogleCloud.Parameters?)configurations![0];
			//var usgsParameters = (Shared.Usgs.Parameters?)configurations[1];
			//var apiClient = new HttpClient {
			//	Timeout = TimeSpan.FromMinutes(10)
			//};
			//var cookies = new CookieContainer();
			//var websiteClient = new HttpClient(new HttpClientHandler {
			//	AllowAutoRedirect = false,
			//	CookieContainer = cookies
			//});
			//var downloadClient = new HttpClient(new HttpClientHandler { CookieContainer = cookies }) {
			//	Timeout = TimeSpan.FromMinutes(10)
			//};
			//var website = await websiteClient.GetAsync($"{usgsParameters!.Login}");
			//if (website.IsSuccessStatusCode) {
			//	var csrfRegex = CsrfRegex();
			//	var getSessionContent = await website.Content.ReadAsStringAsync();
			//	var csrf = csrfRegex.Matches(getSessionContent)[0].Value.Split('"')[3];
			//	var data = new[] {
			//		new KeyValuePair<string, string>("username", usgsParameters.Username!),
			//		new KeyValuePair<string, string>("password", usgsParameters.Password!),
			//		new KeyValuePair<string, string>("csrf", csrf)
			//	};
			//	var websiteLogin = await websiteClient.PostAsync($"{usgsParameters.Login}", new FormUrlEncodedContent(data));
			//	var apiClientResponse = await apiClient.PostAsJsonAsync($"{usgsParameters.Api}/login", new { username = usgsParameters.Username, password = usgsParameters.Password });
			//	if (apiClientResponse.IsSuccessStatusCode) {
			//		var token = JsonConvert.DeserializeObject<Shared.Usgs.LoginResponse>(await apiClientResponse.Content.ReadAsStringAsync());
			//		apiClient.DefaultRequestHeaders.Add("X-Auth-Token", token!.Data);
			//		var payload = new {
			//			datasetName = usgsParameters.Dataset,
			//			sceneFilter = new {
			//				acquisitionFilter = new {
			//					start = order!.Start,
			//					end = order!.End
			//				},
			//				spatialFilter = new {
			//					filterType = "mbr",
			//					lowerLeft = new {
			//						latitude = request.Latitude,
			//						longitude = request.Longitude,
			//					},
			//					upperRight = new {
			//						latitude = request.Latitude,
			//						longitude = request.Longitude,
			//					}
			//				}
			//			}
			//		};
			//	} else {
			//		Console.WriteLine("Error inicio de sesión en la API USGS");
			//	}
			//}
		} catch (HttpRequestException) {
			Console.WriteLine("Sin conexión");
		} catch (TaskCanceledException) {
			Console.WriteLine("Ejecución interrumpida");
		} catch (NullReferenceException) {
			Console.WriteLine("Error de programa: referencia nula");
		}
	}

	[GeneratedRegex("name=\"csrf\" value=\"(.+?)\"")]
	private static partial Regex CsrfRegex();
	private static (double latitude, double longitude) GetCentroid(IReadOnlyCollection<IPosition> vertexes) {
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