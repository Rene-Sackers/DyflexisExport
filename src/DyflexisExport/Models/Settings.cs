using Google.Apis.Auth.OAuth2.Responses;

namespace DyflexisExport.Models
{
	public class Settings
	{
		public string Username { get; set; }

		public string Password { get; set; }

		public string Url { get; set; }

		public string GoogleClientId { get; set; }

		public string GoogleClientSecret { get; set; }

		public TokenResponse GoogleApiTokenResponse { get; set; }

		public string TargetCalendarId { get; set; }

		public int ScrapeMonthCount { get; set; } = 3;

		public bool IsRunningSetup { get; set; }
	}
}
