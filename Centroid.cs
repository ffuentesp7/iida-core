using GeoJSON.Net.Geometry;

namespace Iida.Core;

internal class Centroid {
	public static (double latitude, double longitude) Calculate(IReadOnlyCollection<IPosition> vertexes) {
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