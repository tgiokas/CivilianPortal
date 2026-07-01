namespace CitizenPortal.Application.Extensions
{
    public static class DateTimeExtensions
    {
        public static DateTime ToGreekDateTime(this DateTime value) 
        {
            var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "GTB Standard Time" : "Europe/Athens");

            return TimeZoneInfo.ConvertTimeFromUtc(value, timeZoneInfo);
        }
    }
}
