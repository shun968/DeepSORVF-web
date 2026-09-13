namespace CleanArchitecture.Domain.Geometry;

// Geodesic calculations on the WGS84 ellipsoid, using Vincenty's formulae.
//
// The Python original uses two different libraries for this: geopy's `geodesic` (Karney)
// in count_distance, and pyproj's Geod.fwd (also Karney) in data_pre. Vincenty agrees
// with Karney to well under a millimetre at the ranges this pipeline works with (AISPRO
// keeps vessels within 2 nautical miles of the camera), so the results are interchangeable
// here — but they will not match digit-for-digit. Vincenty's inverse formula is also known
// not to converge for near-antipodal points; those cannot occur for a camera and a vessel
// in the same harbour, so the iteration simply returns its last estimate instead of
// reporting failure.
public static class GeoMath
{
    private const double SemiMajorAxisMeters = 6378137.0;
    private const double Flattening = 1 / 298.257223563;
    private const double SemiMinorAxisMeters = (1 - Flattening) * SemiMajorAxisMeters;
    private const double ConvergenceThreshold = 1e-12;
    private const int MaxIterations = 200;

    public static double DistanceMeters(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        var u1 = Math.Atan((1 - Flattening) * Math.Tan(ToRadians(latitude1)));
        var u2 = Math.Atan((1 - Flattening) * Math.Tan(ToRadians(latitude2)));
        var sinU1 = Math.Sin(u1);
        var cosU1 = Math.Cos(u1);
        var sinU2 = Math.Sin(u2);
        var cosU2 = Math.Cos(u2);
        var longitudeDifference = ToRadians(longitude2 - longitude1);

        var lambda = longitudeDifference;
        double sinSigma = 0;
        double cosSigma = 0;
        double sigma = 0;
        double cosSquaredAlpha = 0;
        double cos2SigmaM = 0;

        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            var sinLambda = Math.Sin(lambda);
            var cosLambda = Math.Cos(lambda);
            var northSouthTerm = (cosU1 * sinU2) - (sinU1 * cosU2 * cosLambda);
            sinSigma = Math.Sqrt((cosU2 * sinLambda * cosU2 * sinLambda) + (northSouthTerm * northSouthTerm));
            if (sinSigma < ConvergenceThreshold)
            {
                return 0;
            }

            cosSigma = (sinU1 * sinU2) + (cosU1 * cosU2 * cosLambda);
            sigma = Math.Atan2(sinSigma, cosSigma);
            var sinAlpha = cosU1 * cosU2 * sinLambda / sinSigma;
            cosSquaredAlpha = 1 - (sinAlpha * sinAlpha);
            // cos²α is zero on an equatorial line, where 2σm is undefined.
            cos2SigmaM = cosSquaredAlpha < ConvergenceThreshold
                ? 0
                : cosSigma - (2 * sinU1 * sinU2 / cosSquaredAlpha);
            var c = Flattening / 16 * cosSquaredAlpha * (4 + (Flattening * (4 - (3 * cosSquaredAlpha))));
            var previousLambda = lambda;
            lambda = longitudeDifference
                + ((1 - c) * Flattening * sinAlpha
                    * (sigma + (c * sinSigma * (cos2SigmaM + (c * cosSigma * (-1 + (2 * cos2SigmaM * cos2SigmaM)))))));

            if (Math.Abs(lambda - previousLambda) < ConvergenceThreshold)
            {
                break;
            }
        }

        var uSquared = cosSquaredAlpha
            * ((SemiMajorAxisMeters * SemiMajorAxisMeters) - (SemiMinorAxisMeters * SemiMinorAxisMeters))
            / (SemiMinorAxisMeters * SemiMinorAxisMeters);
        var a = 1 + (uSquared / 16384 * (4096 + (uSquared * (-768 + (uSquared * (320 - (175 * uSquared)))))));
        var b = uSquared / 1024 * (256 + (uSquared * (-128 + (uSquared * (74 - (47 * uSquared))))));
        var deltaSigma = DeltaSigma(b, sinSigma, cosSigma, cos2SigmaM);

        return SemiMinorAxisMeters * a * (sigma - deltaSigma);
    }

    // Initial great-circle bearing, ported from the Python original's getDegree. This is
    // the spherical formula, not Vincenty's ellipsoidal azimuth — kept as-is so the
    // camera's horizontal angle matches the original pipeline's.
    public static double InitialBearingDegrees(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        var radLatitude1 = ToRadians(latitude1);
        var radLatitude2 = ToRadians(latitude2);
        var longitudeDifference = ToRadians(longitude2 - longitude1);

        var y = Math.Sin(longitudeDifference) * Math.Cos(radLatitude2);
        var x = (Math.Cos(radLatitude1) * Math.Sin(radLatitude2))
            - (Math.Sin(radLatitude1) * Math.Cos(radLatitude2) * Math.Cos(longitudeDifference));

        return (ToDegrees(Math.Atan2(y, x)) + 360) % 360;
    }

    public static (double Latitude, double Longitude) Destination(
        double latitude,
        double longitude,
        double bearingDegrees,
        double distanceMeters)
    {
        var alpha1 = ToRadians(bearingDegrees);
        var sinAlpha1 = Math.Sin(alpha1);
        var cosAlpha1 = Math.Cos(alpha1);

        var tanU1 = (1 - Flattening) * Math.Tan(ToRadians(latitude));
        var cosU1 = 1 / Math.Sqrt(1 + (tanU1 * tanU1));
        var sinU1 = tanU1 * cosU1;
        var sigma1 = Math.Atan2(tanU1, cosAlpha1);

        var sinAlpha = cosU1 * sinAlpha1;
        var cosSquaredAlpha = 1 - (sinAlpha * sinAlpha);
        var uSquared = cosSquaredAlpha
            * ((SemiMajorAxisMeters * SemiMajorAxisMeters) - (SemiMinorAxisMeters * SemiMinorAxisMeters))
            / (SemiMinorAxisMeters * SemiMinorAxisMeters);
        var a = 1 + (uSquared / 16384 * (4096 + (uSquared * (-768 + (uSquared * (320 - (175 * uSquared)))))));
        var b = uSquared / 1024 * (256 + (uSquared * (-128 + (uSquared * (74 - (47 * uSquared))))));

        var angularDistance = distanceMeters / (SemiMinorAxisMeters * a);
        var sigma = angularDistance;
        double sinSigma = 0;
        double cosSigma = 0;
        double cos2SigmaM = 0;

        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            cos2SigmaM = Math.Cos((2 * sigma1) + sigma);
            sinSigma = Math.Sin(sigma);
            cosSigma = Math.Cos(sigma);
            var deltaSigma = DeltaSigma(b, sinSigma, cosSigma, cos2SigmaM);
            var previousSigma = sigma;
            sigma = angularDistance + deltaSigma;

            if (Math.Abs(sigma - previousSigma) < ConvergenceThreshold)
            {
                break;
            }
        }

        var southNorthTerm = (sinU1 * sinSigma) - (cosU1 * cosSigma * cosAlpha1);
        var destinationLatitude = Math.Atan2(
            (sinU1 * cosSigma) + (cosU1 * sinSigma * cosAlpha1),
            (1 - Flattening) * Math.Sqrt((sinAlpha * sinAlpha) + (southNorthTerm * southNorthTerm)));
        var lambda = Math.Atan2(
            sinSigma * sinAlpha1,
            (cosU1 * cosSigma) - (sinU1 * sinSigma * cosAlpha1));
        var c = Flattening / 16 * cosSquaredAlpha * (4 + (Flattening * (4 - (3 * cosSquaredAlpha))));
        var longitudeDifference = lambda
            - ((1 - c) * Flattening * sinAlpha
                * (sigma + (c * sinSigma * (cos2SigmaM + (c * cosSigma * (-1 + (2 * cos2SigmaM * cos2SigmaM)))))));

        var destinationLongitude = longitude + ToDegrees(longitudeDifference);

        return (ToDegrees(destinationLatitude), ((destinationLongitude + 540) % 360) - 180);
    }

    private static double DeltaSigma(double b, double sinSigma, double cosSigma, double cos2SigmaM) =>
        b * sinSigma
        * (cos2SigmaM
            + (b / 4
                * ((cosSigma * (-1 + (2 * cos2SigmaM * cos2SigmaM)))
                    - (b / 6 * cos2SigmaM * (-3 + (4 * sinSigma * sinSigma)) * (-3 + (4 * cos2SigmaM * cos2SigmaM))))));

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;

    private static double ToDegrees(double radians) => radians * 180 / Math.PI;
}
