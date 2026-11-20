static class FieldAuditGps
{
    public const double MatchRadiusMeters=75;
    public static bool Valid(double latitude,double longitude,double accuracy)=>
        double.IsFinite(latitude)&&latitude is >= -90 and <= 90&&
        double.IsFinite(longitude)&&longitude is >= -180 and <= 180&&
        !(latitude==0&&longitude==0)&&double.IsFinite(accuracy)&&accuracy>0;

    public static bool ValidCapture(double latitude,double longitude,double accuracy,DateTimeOffset? captured,DateTimeOffset now)=>
        Valid(latitude,longitude,accuracy)&&captured.HasValue&&captured.Value<=now.AddMinutes(2);

    public static bool Matches(double latitude,double longitude,double accuracy,double referenceLatitude,double referenceLongitude,double referenceAccuracy)
    {
        if(!Valid(latitude,longitude,accuracy)||!Valid(referenceLatitude,referenceLongitude,referenceAccuracy))return false;
        var p=Math.PI/180;var dLat=(referenceLatitude-latitude)*p;var dLon=(referenceLongitude-longitude)*p;
        var h=Math.Pow(Math.Sin(dLat/2),2)+Math.Cos(latitude*p)*Math.Cos(referenceLatitude*p)*Math.Pow(Math.Sin(dLon/2),2);
        var distance=6371000*2*Math.Atan2(Math.Sqrt(h),Math.Sqrt(Math.Max(0,1-h)));
        return distance<=Math.Max(MatchRadiusMeters,accuracy+referenceAccuracy);
    }
}
