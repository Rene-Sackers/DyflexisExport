using Google.Apis.Calendar.v3.Data;

namespace DyflexisExport.Extensions
{
	public static class EventExtensions
	{
		public static string ToLogInfo(this Event @event) =>
			$"({@event.Description}) [{@event.Start?.DateTime:d}] {@event.Start?.DateTime:t} -> {@event.End?.DateTime:t} - {@event.Summary}";
	}
}
